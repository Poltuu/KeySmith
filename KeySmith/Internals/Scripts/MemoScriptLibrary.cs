using KeySmith.Internals.Scripts.Parameters;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KeySmith.Internals.Scripts
{
    class MemoScriptLibrary : IMemoScriptLibrary
    {
        private static readonly string SetAndPublishScript = @"
            redis.call('SET', @MemoKey, @Value, 'PX', @MemoKeyExpiration)
            redis.call('PUBLISH', @MemoChannelKey, @Value)
        ";

        private readonly Lazy<Task<LoadedLuaScript>> SetAndPublish;

        private Task<LoadedLuaScript> LoadScript(string script)
            => LuaScript.Prepare(script).LoadAsync(_connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints()[0]));

        private readonly ConnectionMultiplexer _connectionMultiplexer;

        public MemoScriptLibrary(ConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
            SetAndPublish = new Lazy<Task<LoadedLuaScript>>(() => LoadScript(SetAndPublishScript), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public async Task PublishAsync(MemoSetValueParameters parameters)
            => await (await SetAndPublish.Value.ConfigureAwait(false))
                .EvaluateAsync(_connectionMultiplexer.GetDatabase(), parameters, flags: CommandFlags.FireAndForget).ConfigureAwait(false);

        public Task<RedisValue[]> GetValuesAsync(RedisKey valueKey, RedisKey errorKey)
            => _connectionMultiplexer.GetDatabase().StringGetAsync(new[] { valueKey, errorKey });

        public Task SubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue> handler)
            => _connectionMultiplexer.GetSubscriber().SubscribeAsync(channel, handler);

        public Task UnsubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue> handler)
            => _connectionMultiplexer.GetSubscriber().UnsubscribeAsync(channel, handler, flags: CommandFlags.FireAndForget);
    }
}