using StackExchange.Redis;
using System;

namespace KeySmith.Internals.Scripts.Parameters
{
    readonly struct MemoSetValueParameters
    {
        public readonly string MemoKey { get; }
        public readonly double MemoKeyExpiration { get; }
        public readonly RedisValue Value { get; }

        public readonly string MemoChannelKey { get; }

        public MemoSetValueParameters(MemoKey key, Exception e)
        {
            MemoKey = key.GetErrorKey();
            MemoKeyExpiration = key.ValueExpiration.TotalMilliseconds;
            Value = e.InnerException?.Message ?? e.Message;
            MemoChannelKey = key.GetErrorChannel();
        }

        public MemoSetValueParameters(MemoKey key, RedisValue value)
        {
            MemoKey = key.GetValueKey();
            MemoKeyExpiration = key.ValueExpiration.TotalMilliseconds;
            Value = value == RedisValue.Null ? RedisValue.EmptyString : value;
            MemoChannelKey = key.GetValueChannel();
        }
    }
}