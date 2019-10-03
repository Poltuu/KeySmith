﻿namespace KeySmith
{
    /// <summary>
    /// This class represents a lock key
    /// </summary>
    public class DistributedLockKey
    {
        private readonly string _root;
        private readonly string _lockName;

        /// <summary>
        ///  Initializes a new instance of the <see cref="DistributedLockKey"/> class
        /// </summary>
        /// <param name="root"></param>
        /// <param name="lockName"></param>
        public DistributedLockKey(string root, string lockName)
        {
            _root = root;
            _lockName = lockName;
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
