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

        private readonly SemaphoreSlim LoadGetLockOrAddToQueueScriptLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim LoadFreeLockAndPopLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim LoadGetKeySituationLock = new SemaphoreSlim(1, 1);

        private LoadedLuaScript? GetLockOrAddToQueueLoaded = null;
        private LoadedLuaScript? FreeLockAndPopLoaded = null;
        private LoadedLuaScript? GetKeySituationLoaded = null;

        private readonly ConnectionMultiplexer _connectionMultiplexer;

        public ScriptLibrary(ConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
        }

        public async Task<bool> GetLockOrAddToQueue(LockLuaParameters parameters)
        {
            if (GetLockOrAddToQueueLoaded == null)
            {
                await LoadGetLockOrAddToQueueScriptLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (GetLockOrAddToQueueLoaded == null)
                    {
                        GetLockOrAddToQueueLoaded = await LuaScript.Prepare(GetLockOrAddToQueueScript).LoadAsync(_connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints()[0])).ConfigureAwait(false);
                    }
                }
                finally
                {
                    LoadGetLockOrAddToQueueScriptLock.Release();
                }
            }
            return (bool)await GetLockOrAddToQueueLoaded.EvaluateAsync(_connectionMultiplexer.GetDatabase(), parameters).ConfigureAwait(false);
        }

        public async Task FreeLockAndPop(LockLuaParameters parameters)
        {
            if (FreeLockAndPopLoaded == null)
            {
                await LoadFreeLockAndPopLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (FreeLockAndPopLoaded == null)
                    {
                        FreeLockAndPopLoaded = await LuaScript.Prepare(FreeLockAndPopScript).LoadAsync(_connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints()[0])).ConfigureAwait(false);
                    }
                }
                finally
                {
                    LoadFreeLockAndPopLock.Release();
                }
            }

            await FreeLockAndPopLoaded.EvaluateAsync(_connectionMultiplexer.GetDatabase(), parameters, flags: CommandFlags.FireAndForget).ConfigureAwait(false);
        }

        public async Task<int> GetKeySituation(LockLuaParameters parameters)
        {
            if (GetKeySituationLoaded == null)
            {
                await LoadGetKeySituationLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (GetKeySituationLoaded == null)
                    {
                        GetKeySituationLoaded = await LuaScript.Prepare(GetKeySituationScript).LoadAsync(_connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints()[0])).ConfigureAwait(false);
                    }
                }
                finally
                {
                    LoadGetKeySituationLock.Release();
                }
            }

            return (int)await GetKeySituationLoaded.EvaluateAsync(_connectionMultiplexer.GetDatabase(), parameters).ConfigureAwait(false);
        }

        public Task SubscribeAsync(LockState state)
            => _connectionMultiplexer.GetSubscriber().SubscribeAsync(state.Key.GetLockChannelKey(), state.Handler);

        public Task UnSubscribeAsync(LockState state)
            => _connectionMultiplexer.GetSubscriber().UnsubscribeAsync(state.Key.GetLockChannelKey(), state.Handler, CommandFlags.FireAndForget);
    }
}