using KeySmith.Internals.Scripts.Parameters;
using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KeySmith.Internals.Scripts
{
    class MemoScriptLibrary : IMemoScriptLibrary
    {
        private static readonly LuaScript SetAndPublishScript = LuaScript.Prepare(@"
            redis.call('SET', @MemoKey, @Value, 'PX', @MemoKeyExpiration)
            redis.call('PUBLISH', @MemoChannelKey, @Value)
        ");

        private readonly SemaphoreSlim LoadSetAndPublishLock = new SemaphoreSlim(1, 1);

        private LoadedLuaScript? SetAndPublishLoaded = null;

        private readonly ConnectionMultiplexer _connectionMultiplexer;

        public MemoScriptLibrary(ConnectionMultiplexer connectionMultiplexer)
        {
            _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
        }

        public async Task PublishAsync(MemoSetValueParameters parameters)
        {
            if (SetAndPublishLoaded == null)
            {
                await LoadSetAndPublishLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (SetAndPublishLoaded == null)
                    {
                        SetAndPublishLoaded = await SetAndPublishScript.LoadAsync(_connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints()[0])).ConfigureAwait(false);
                    }
                }
                finally
                {
                    LoadSetAndPublishLock.Release();
                }
            }

            await SetAndPublishLoaded.EvaluateAsync(_connectionMultiplexer.GetDatabase(), parameters, flags: CommandFlags.FireAndForget).ConfigureAwait(false);
        }

        public Task<RedisValue> GetValueAsync(RedisKey key)
            => _connectionMultiplexer.GetDatabase().StringGetAsync(key);

        public Task SubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue> handler)
            => _connectionMultiplexer.GetSubscriber().SubscribeAsync(channel, handler);

        public Task UnsubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue> handler)
            => _connectionMultiplexer.GetSubscriber().UnsubscribeAsync(channel, handler, flags: CommandFlags.FireAndForget);
    }
}