using KeySmith.Internals.Scripts.Parameters;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace KeySmith.Internals.Scripts
{
    interface IMemoScriptLibrary
    {
        Task<RedisValue[]> GetValuesAsync(RedisKey valueKey, RedisKey errorKey);
        Task PublishAsync(MemoSetValueParameters parameters);

        Task SubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue> handler);
        Task UnsubscribeAsync(RedisChannel channel, Action<RedisChannel, RedisValue> handler);
    }
}