using StackExchange.Redis;
using System;

namespace KeySmith.Internals
{
    class RedisLock : IDisposable
    {
        private readonly IDatabase _db;
        private readonly string _identifier;
        private readonly DistributedLockKey _key;
        private readonly LuaScript _disposeScript;

        private bool _disposedValue;

        public RedisLock(IDatabase db, string identifier, DistributedLockKey key, LuaScript disposeScript)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            _key = key ?? throw new ArgumentNullException(nameof(key));
            _disposeScript = disposeScript ?? throw new ArgumentNullException(nameof(disposeScript));
        }

        public void Dispose()
        {
            if (!_disposedValue)
            {
                _db.ScriptEvaluate(_disposeScript, new
                {
                    LockKey = _key.GetLockKey(),
                    LockNotifKey = _key.GetLockNotifKey(),
                    LockWaitingListKey = _key.GetLockWaitingListKey(),
                    Timeout = RedisLockService.DefaultLockTimeout.TotalSeconds,
                    Identifier = _identifier
                });
                _disposedValue = true;
            }
        }
    }
}
