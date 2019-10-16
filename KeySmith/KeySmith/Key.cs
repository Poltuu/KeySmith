using StackExchange.Redis;
using System;

namespace KeySmith
{
    /// <summary>
    /// This class represents a distributed lock key
    /// </summary>
    public readonly struct Key
    {
        private readonly string _root;
        private readonly string _lockName;

        internal TimeSpan RedisKeyExpiration { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Key"/> class
        /// </summary>
        /// <param name="root">Root of redis key used for every necessary entry in redis</param>
        /// <param name="lockName">The name of the current lock key</param>
        /// <param name="redisKeyExpiration">Expiration of redis keys needed for the locking process</param>
        public Key(string root, string lockName, TimeSpan redisKeyExpiration)
        {
            _root = root;
            _lockName = lockName;

            if (redisKeyExpiration == TimeSpan.Zero)
            {
                throw new ArgumentException("RedisKeyExpiration must be a positive duration.");
            }

            RedisKeyExpiration = redisKeyExpiration;
        }

        internal string GetLockKey() => $"{_root}/lock:{_lockName}";
        internal string GetLockQueueKey() => $"{_root}/lockwaiting:{_lockName}";
        internal string GetLockQueueSetKey() => $"{_root}/lockwaitingset:{_lockName}";

        internal RedisChannel GetLockChannelKey() => new RedisChannel($"{_root}/locknotif:{_lockName}", RedisChannel.PatternMode.Literal);
    }
}