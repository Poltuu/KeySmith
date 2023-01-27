
namespace KeySmith.Internals.Scripts.Parameters
{
    internal readonly struct LockLuaParameters
    {
        public readonly string Identifier { get; }
        public readonly string LockKey { get; }
        public readonly double Timeout { get; }
        public readonly string LockWaitingListKey { get; }
        public readonly string LockWaitingSetKey { get; }
        public readonly string? LockNotifKey { get; }

        public LockLuaParameters(string identifier, Key key)
        {
            Identifier = identifier;
            LockKey = key.GetLockKey();
            LockWaitingListKey = key.GetLockQueueKey();
            LockWaitingSetKey = key.GetLockQueueSetKey();
            Timeout = key.RedisKeyExpiration.TotalMilliseconds;
            LockNotifKey = key.GetLockChannelKey();
        }
    }
}