using StackExchange.Redis;

namespace KeySmith.Internals
{
    static class ScriptsLibrary
    {
        public static readonly LuaScript DeleteAllKeys = LuaScript.Prepare("redis.call('DEL', @Key1, @Key2, @Key3, @Key4)");

        public static readonly LuaScript SetAndPublish = LuaScript.Prepare(@"
            redis.call('SET', @Key, @Value)
            redis.call('PUBLISH', @LockNotifKey, @Value)
        ");

        public static readonly LuaScript GetLockOrSubscribe = LuaScript.Prepare(@"
            if (redis.call('SET', @Key, @Value, 'EX', @Timeout, 'NX')) then
                return 1
            end
            if not redis.call('EXISTS', @LockWaitingListKey) then
                redis.call('RPUSH', @LockWaitingListKey, @Value)
                redis.call('EXPIRE', @LockWaitingListKey, @Timeout)
            else
                redis.call('RPUSH', @LockWaitingListKey, @Value)
            end
            return 0
        ");

        public static readonly LuaScript FreeLockAndPop = LuaScript.Prepare(@"
        if redis.call('GET', @LockKey) == @Identifier then
            local next = redis.call('LPOP', @LockWaitingListKey)
            if next then
                redis.call('EXPIRE', @LockWaitingListKey, @Timeout)
                redis.call('SET', @LockKey, next, 'EX', @Timeout)
                redis.call('PUBLISH', @LockNotifKey, next)
            else
                redis.call('DEL', @LockKey)
                redis.call('DEL', @LockWaitingListKey)
            end
        end");
    }
}
