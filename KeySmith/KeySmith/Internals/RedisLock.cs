using StackExchange.Redis;
using System;

namespace KeySmith.Internals
{
    class RedisLock : IDisposable
    {
        private readonly IDatabase _db;
        private readonly string _identifier;
        private readonly DistributedLockKey _key;

        private bool _disposedValue;

        public RedisLock(IDatabase db, string identifier, DistributedLockKey key)
        {
            _db = db;
            _identifier = identifier;
            _key = key;
        }

        public void Dispose()
        {
            if (!_disposedValue)
            {
                _db.ScriptEvaluate(ScriptsLibrary.FreeLockAndPop, new
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
