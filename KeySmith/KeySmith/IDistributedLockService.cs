using System;
using System.Threading.Tasks;

namespace KeySmith
{
    /// <summary>
    /// Represents a class able to serve distributed lock on one redis instance
    /// </summary>
    public interface IDistributedLockService
    {
        /// <summary>
        /// Returns a disposable object if the distributed lock is acquired, null otherwise
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<IDisposable?> TryAcquireDistributedLockAsync(DistributedLockKey key);

        /// <summary>
        /// Wait until the lock is acquired. Throw a timeout exception otherwise
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<IDisposable> AcquireDistributedLockAsync(DistributedLockKey key);

        /// <summary>
        /// Get a value, or generate it using the generator function. The evaluation of the generator is guaranteed to be called only once across the system.
        /// Throw a timeout exception if the lock wasn't acquired or if the generator takes too much time.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="generator"></param>
        /// <param name="waitMaxTimeout"></param>
        /// <returns></returns>
        Task<T> GenerateOnlyOnceUsingDistributedLockAsync<T>(DistributedLockKey key, Func<Task<T>> generator, TimeSpan waitMaxTimeout);

        /// <summary>
        /// Invalidate the cache associated with a given key. This should be used if something went terribly wrong, or in some instantiation scenario, to force a new value
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task InvalidateAsync(DistributedLockKey key);
    }
}
