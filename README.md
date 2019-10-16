# KeySmith

**The fastest distributed locks and memo locks for Redis on one instance.**

## ILockService

```c#
    /// <summary>
    /// A service able to serve distributed lock on one redis instance
    /// </summary>
    public interface ILockService
    {
        /// <summary>
        /// Wait until a distributed lock on the given key is acquired and executes the callback.
        /// If the lock is lost, the <see cref="CancellationToken"/> given as a parameter to the callback will be canceled.
        /// This <see cref="CancellationToken"/> may also be canceled using the <paramref name="cancellationToken"/> parameter.
        /// The <paramref name="cancellationToken"/> may also trigger an early exit from the waiting stage.
        /// </summary>
        Task LockAsync(Key key, Func<CancellationToken, Task> callback, CancellationToken cancellationToken);

        Task<T> LockAsync<T>(Key key, Func<CancellationToken, Task<T>> callback, CancellationToken cancellationToken);
    }
```

The `ILockService` class is a distributed lock provided (that implements [the redlock algorithm](https://redis.io/topics/distlock) for one instance). It ensures that the provided callback is played once at a time across your system, as soon as possible.

It also uses the pub/sub mechanism of Redis to make sure your system is locked as little as possible. A queue on the redis instance is used to minimize the time each client is locked. You may also provide a `CancellationToken` to have better control over how long you want to wait.

On top of that (and since [redis does not guarantee delivery](https://stackoverflow.com/questions/23675394/redis-publish-subscribe-is-redis-guaranteed-to-deliver-the-message-even-under-m)), this system guarantees no deadlock, via a proactive protection system, that periodically checks everything is normal while you're waiting for the lock. For instance, if the keys expired due to a distant failure, this will restart the locking process.

## IMemoLockService

```c#
    /// <summary>
    /// A service able to help memoization of a generator method through use of distributed lock on one redis instance
    /// </summary>
    public interface IMemoLockService
    {
        /// <summary>
        /// Try to use the distributed cache version of the result from the generator. If absent, generates the value while guaranteing that no other process is doing the same job. Eventually PUB the result to every other processes interested by the result.
        /// In case of problem, the generator is eventually used instead of the cached value.
        /// </summary>
        Task<RedisValue> MemoLockAsync(MemoKey key, Func<CancellationToken, Task<RedisValue>> generator, CancellationToken cancellationToken);
    }

```

The `IMemoLockService`, inspired by [this talk](https://www.youtube.com/watch?v=BO-SKMS-D_g) and [this repository](https://github.com/kristoff-it/redis-memolock), is a mechanism to prevent different systems doing the same thing at the same time, and saving the result for futur uses. It is a mix of lock and [memoization](https://en.wikipedia.org/wiki/Memoization).

When holding the memolock, other processes are locked when trying to enter. Once the result is generated, it is saved and published on redis. Blocked processes get the result as fast as possible via pub/sub.

For instance, let's suppose that three requests that are 10 seconds apart, ask for the generation of the same report file, which takes 15 secondes. The first one takes the lock and starts generating the file. 10 seconds later, the second sees the lock taken, and waits until the end of the generation. 5 seconds later, the first process is done, caches the result, publishes the result to the waiting processes and frees the locks. Hence processes 2 "generated" the result in 5 seconds instead of 10. Finally, process 3 will find the generated outcome in redis, taking no time at all for generation.
