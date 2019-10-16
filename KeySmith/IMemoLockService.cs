using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KeySmith
{
    /// <summary>
    /// A service able to help memoization of a generator method through use of distributed lock on one redis instance
    /// </summary>
    public interface IMemoLockService
    {
        /// <summary>
        /// Try to use the distributed cache version of the result from the generator.
        /// <para></para>
        /// In case of problem, the generator is eventually used instead of the cached value.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="generator"></param>
        /// <param name="cancellationToken">This lets you cancel the waiting and the generation</param>
        /// <returns></returns>
        Task<RedisValue> MemoLockAsync(MemoKey key, Func<CancellationToken, Task<RedisValue>> generator, CancellationToken cancellationToken);
    }
}