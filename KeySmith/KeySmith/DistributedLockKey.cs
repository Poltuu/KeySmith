using System;

namespace KeySmith
{
    /// <summary>
    /// This class represents a lock key
    /// </summary>
    public class DistributedLockKey
    {
        private readonly string _root;
        private readonly string _lockName;

        /// <summary>
        /// Gets the maximum waiting time before throwing a TimeoutException and leaving the queue for this lock.
        /// This represents the maximum time a process can be waiting for this key to be freed.
        /// This should be superior to <see cref="RedisKeyExpiration"/>
        /// </summary>
        public TimeSpan MaxWaitingTime { get; private set; }

        /// <summary>
        /// Gets the expiration for the redis key
        /// This represents the time when an error most likely occurred, and the redis key resets itself
        /// </summary>
        public TimeSpan RedisKeyExpiration { get; private set; }


        /// <summary>
        ///  Initializes a new instance of the <see cref="DistributedLockKey"/> class
        /// </summary>
        /// <param name="root"></param>
        /// <param name="lockName"></param>
        /// <param name="maxWaitingTime"></param>
        /// <param name="redisKeyExpiration"></param>
        public DistributedLockKey(string root, string lockName, TimeSpan maxWaitingTime, TimeSpan redisKeyExpiration)
        {
            _root = root;
            _lockName = lockName;
            MaxWaitingTime = maxWaitingTime;
            RedisKeyExpiration = redisKeyExpiration;
        }

        /// <summary>
        /// Returns the key holding the value. Used in instantiation scenario.
        /// </summary>
        /// <returns></returns>
        public string GetKey() => $"{_root}:{_lockName}";

        /// <summary>
        /// Returns the key holding the lock itself
        /// </summary>
        /// <returns></returns>
        public string GetLockKey() => $"{_root}/lock:{_lockName}";

        /// <summary>
        /// Returns the key used in pub/sub on the lock release
        /// </summary>
        /// <returns></returns>
        public string GetLockNotifKey() => $"{_root}/locknotif:{_lockName}";

        /// <summary>
        /// Returns the key holding the waiting list
        /// </summary>
        /// <returns></returns>
        public string GetLockWaitingListKey() => $"{_root}/lockwaiting:{_lockName}";
    }
}
