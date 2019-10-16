using System;
using System.Threading;
using System.Threading.Tasks;

namespace KeySmith
{
    /// <summary>
    /// A service able to serve distributed lock on one redis instance
    /// </summary>
    public interface ILockService
    {
        /// <summary>
        /// Wait until a distributed lock on the given key is acquired and executes the callback.
        /// <para></para>
        /// If the lock is lost, the <see cref="CancellationToken"/> given as a parameter to the callback will be canceled.
        /// This <see cref="CancellationToken"/> may also be canceled using the <paramref name="cancellationToken"/> parameter.
        /// The <paramref name="cancellationToken"/> may also trigger an early exit from the waiting stage.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="TaskCanceledException">From <paramref name="cancellationToken"/> or losing redis key</exception>
        Task LockAsync(Key key, Func<CancellationToken, Task> callback, CancellationToken cancellationToken);

        /// <summary>
        /// Wait until a distributed lock on the given key is acquired and returns the value from the callback.
        /// <para></para>
        /// If the lock is lost, the <see cref="CancellationToken"/> given as a parameter to the callback will be canceled.
        /// This <see cref="CancellationToken"/> may also be canceled using the <paramref name="cancellationToken"/> parameter.
        /// The <paramref name="cancellationToken"/> may also trigger an early exit from the waiting stage.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="callback"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="TaskCanceledException">From <paramref name="cancellationToken"/> or losing redis key</exception>
        Task<T> LockAsync<T>(Key key, Func<CancellationToken, Task<T>> callback, CancellationToken cancellationToken);
    }
}