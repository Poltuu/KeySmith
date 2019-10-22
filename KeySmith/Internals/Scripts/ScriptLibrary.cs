using KeySmith.Internals.Locks;
using KeySmith.Internals.Scripts.Parameters;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KeySmith.Internals.Scripts
{
    class ScriptLibrary : IScriptLibrary
    {
        static readonly string GetLockOrAddToQueueScript = @"
            if redis.call('GET', @LockKey) == @Identifier then
                return 1
            end

            if redis.call('SET', @LockKey, @Identifier, 'PX', @Timeout, 'NX') then
                redis.call('PUBLISH', @LockNotifKey, @Identifier)
                return 1
            end

            if redis.call('EXISTS', @LockWaitingListKey) == 1 then
                redis.call('RPUSH', @LockWaitingListKey, @Identifier)
                redis.call('HSET', @LockWaitingSetKey, @Identifier, 0)
            else
                redis.call('RPUSH', @LockWaitingListKey, @Identifier)
                redis.call('HSET', @LockWaitingSetKey, @Identifier, 0)

                redis.call('PEXPIRE', @LockWaitingListKey, @Timeout)
                redis.call('PEXPIRE', @LockWaitingSetKey, @Timeout)
            end

            return 0
        ";

        static readonly string FreeLockAndPopScript = @"
            if redis.call('GET', @LockKey) == @Identifier then
                local next = redis.call('LPOP', @LockWaitingListKey)
                while next and redis.call('HDEL', @LockWaitingSetKey, next) == 0 do
                    next = redis.call('LPOP', @LockWaitingListKey)
                end

                if next then
                    redis.call('SET', @LockKey, next, 'PX', @Timeout)
                    redis.call('PEXPIRE', @LockWaitingSetKey, @Timeout)
                    redis.call('PEXPIRE', @LockWaitingListKey, @Timeout)
                    redis.call('PUBLISH', @LockNotifKey, next)
                else
                    redis.call('UNLINK', @LockKey)
                    redis.call('UNLINK', @LockWaitingListKey)
                    redis.call('UNLINK', @LockWaitingSetKey)
                end
            else
                redis.call('HDEL', @LockWaitingSetKey, @Identifier)
            end
        ";

        static readonly string GetKeySituationScript = @"
            if redis.call('HEXISTS', @LockWaitingSetKey, @Identifier) == 1 then
                return 0
            elseif redis.call('GET', @LockKey) == @Identifier then
                return 1
            else
                return 2
            end
        ";

        private readonly Lazy<Task<LoadedLuaScript>> GetLockOrAddToQueueLoaded;
        private readonly Lazy<Task<LoadedLuaScript>> FreeLockAndPopLoaded;
        private readonly Lazy<Task<LoadedLuaScript>> GetKeySituationLoaded;

        private Task<LoadedLuaScript> LoadScript(string script)
            => LuaScript.Prepare(script).LoadAsync(_connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints()[0]));

        private readonly ConnectionMultiplexer _connectionMultiplexer;

        public ScriptLibrary(ConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));

            GetLockOrAddToQueueLoaded = new Lazy<Task<LoadedLuaScript>>(() => LoadScript(GetLockOrAddToQueueScript), LazyThreadSafetyMode.ExecutionAndPublication);
            FreeLockAndPopLoaded = new Lazy<Task<LoadedLuaScript>>(() => LoadScript(FreeLockAndPopScript), LazyThreadSafetyMode.ExecutionAndPublication);
            GetKeySituationLoaded = new Lazy<Task<LoadedLuaScript>>(() => LoadScript(GetKeySituationScript), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public async Task<bool> GetLockOrAddToQueue(LockLuaParameters parameters)
            => (bool)await (await GetLockOrAddToQueueLoaded.Value.ConfigureAwait(false))
                .EvaluateAsync(_connectionMultiplexer.GetDatabase(), parameters).ConfigureAwait(false);

        public async Task FreeLockAndPop(LockLuaParameters parameters)
            => await (await FreeLockAndPopLoaded.Value.ConfigureAwait(false))
                .EvaluateAsync(_connectionMultiplexer.GetDatabase(), parameters, flags: CommandFlags.FireAndForget).ConfigureAwait(false);

        public async Task<int> GetKeySituation(LockLuaParameters parameters)
            => (int)await (await GetKeySituationLoaded.Value.ConfigureAwait(false))
                .EvaluateAsync(_connectionMultiplexer.GetDatabase(), parameters).ConfigureAwait(false);

        public Task SubscribeAsync(LockState state)
            => _connectionMultiplexer.GetSubscriber().SubscribeAsync(state.Key.GetLockChannelKey(), state.Handler);

        public Task UnSubscribeAsync(LockState state)
            => _connectionMultiplexer.GetSubscriber().UnsubscribeAsync(state.Key.GetLockChannelKey(), state.Handler, CommandFlags.FireAndForget);
    }
}