using StackExchange.Redis;
using System;

namespace KeySmith
{
    /// <summary>
    /// This class represents a distributed key for memoization
    /// </summary>
    public readonly struct MemoKey
    {
        private readonly string _root;
        private readonly string _lockName;

        internal readonly TimeSpan LockExpiration { get; }
        internal readonly TimeSpan ValueExpiration { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoKey"/> class
        /// </summary>
        /// <param name="root">Root of redis key used for every necessary entry in redis</param>
        /// <param name="lockName">The name of the current lock key</param>
        /// <param name="valueExpiration">Expiration of the redis key associated with the generated value</param>
        /// <param name="lockExpiration">Expiration of redis keys needed for the locking process</param>
        public MemoKey(string root, string lockName, TimeSpan valueExpiration, TimeSpan lockExpiration)
        {
            if (valueExpiration == TimeSpan.Zero)
            {
                throw new ArgumentException("LockExpiration must be a positive duration.");
            }
            if (lockExpiration == TimeSpan.Zero)
            {
                throw new ArgumentException("LockExpiration must be a positive duration.");
            }

            _root = root;
            _lockName = lockName;

            LockExpiration = lockExpiration;
            ValueExpiration = valueExpiration;
        }

        internal Key GetLockKey() => new Key(_root, _lockName, LockExpiration);

        internal string GetValueKey() => $"{_root}/{_lockName}";
        internal string GetErrorKey() => $"{_root}/error:{_lockName}";

        internal RedisChannel GetErrorChannel() => new RedisChannel($"{_root}/memoerrornotif:{_lockName}", RedisChannel.PatternMode.Literal);
        internal RedisChannel GetValueChannel() => new RedisChannel($"{_root}/memovaluenotif:{_lockName}", RedisChannel.PatternMode.Literal);
        internal RedisChannel GetSubscribtionChannel() => new RedisChannel($"{_root}/memo*notif:{_lockName}", RedisChannel.PatternMode.Pattern);
    }
}