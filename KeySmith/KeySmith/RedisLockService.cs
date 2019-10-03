using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using KeySmith.Internals;

namespace KeySmith
{
    /// <summary>
    /// A service able to serve distributed lock on one redis instance
    /// </summary>
    public class RedisLockService : IDistributedLockService
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly ILogger<RedisLockService> _logger;
        private readonly IRedisSerializer _redisSerializer;

        /// <summary>
        /// Default ttl of a lock
        /// </summary>
        public static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromMinutes(10);

        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Subscriber>> _subscriptions = new ConcurrentDictionary<string, ConcurrentDictionary<string, Subscriber>>();
        private readonly ConcurrentDictionary<string, QueuedLock> _queueForLocks = new ConcurrentDictionary<string, QueuedLock>();

        /// <summary>
        ///  Initializes a new instance of the <see cref="RedisLockService"/> class
        /// </summary>
        /// <param name="redis"></param>
        /// <param name="configuration"></param>
        /// <param name="redisSerializer"></param>
        /// <param name="logger"></param>
        public RedisLockService(ConnectionMultiplexer redis, IOptions<KeySpaceConfiguration> configuration, IRedisSerializer redisSerializer, ILogger<RedisLockService> logger)
        {
            _redis = redis;
            _logger = logger;
            _redisSerializer = redisSerializer;

            Init(configuration.Value.Root);
        }

        ///<inheritdoc />
        public async Task InvalidateAsync(DistributedLockKey key)
        {
            await _redis.GetDatabase().ScriptEvaluateAsync(ScriptsLibrary.DeleteAllKeys, new { Key1 = key.GetKey(), Key2 = key.GetLockKey(), Key3 = key.GetLockNotifKey(), Key4 = key.GetLockWaitingListKey() }).ConfigureAwait(false);
        }

        ///<inheritdoc />
        public Task<IDisposable?> TryAcquireDistributedLockAsync(DistributedLockKey key)
        {
            return TryAcquireDistributedLockAsync(_redis.GetDatabase(), key, Guid.NewGuid().ToString(), false);
        }

        async Task<IDisposable?> TryAcquireDistributedLockAsync(IDatabase db, DistributedLockKey key, string identifier, bool subscribeIfUnavailable)
        {
            var lockKey = key.GetLockKey();
            if (subscribeIfUnavailable)
            {
                if ((bool)await db.ScriptEvaluateAsync(ScriptsLibrary.GetLockOrSubscribe, new
                {
                    LockWaitingListKey = key.GetLockWaitingListKey(),
                    Value = identifier,
                    Key = lockKey,
                    Timeout = DefaultLockTimeout.TotalSeconds
                }).ConfigureAwait(false))
                {
                    return new RedisLock(db, identifier, key);
                }
            }
            else if (await db.StringSetAsync(lockKey, identifier, DefaultLockTimeout, When.NotExists).ConfigureAwait(false))
            {
                return new RedisLock(db, identifier, key);
            }

            return null;
        }

        ///<inheritdoc />
        public Task<IDisposable> AcquireDistributedLockAsync(DistributedLockKey key, TimeSpan waitMaxTimeout)
        {
            return AcquireDistributedLockAsync(_redis.GetDatabase(), key, Guid.NewGuid().ToString(), waitMaxTimeout);
        }

        async Task<IDisposable> AcquireDistributedLockAsync(IDatabase db, DistributedLockKey key, string identifier, TimeSpan waitTimeout)
        {
            //identifier is saved locally, ready for sub message
            _logger.LogDebug($"Enqueue: {identifier}");
            var queuedLock = new QueuedLock
            {
                Completion = new TaskCompletionSource<IDisposable>(),
                GetLockFactory = () => new RedisLock(db, identifier, key)
            };
            _queueForLocks.AddOrUpdate(identifier, queuedLock, (s, v) => v);

            // no timeout => normal try
            //timeout => pub/sub on redis queue
            var noTimeout = waitTimeout == default;
            var result = await TryAcquireDistributedLockAsync(db, key, identifier, !noTimeout).ConfigureAwait(false);
            if (result != null)
            {
                _queueForLocks.TryRemove(identifier, out var _);
                queuedLock.Completion.TrySetCanceled();
                return result;
            }

            if (noTimeout)
            {
                throw new TimeoutException("The operation timed out.");
            }

            return await GetTimeoutTaskAsync(waitTimeout, identifier, c => queuedLock.Completion.Task, id =>
            {
                if (_queueForLocks.TryGetValue(identifier, out var qLock))
                {
                    qLock.Completion.TrySetCanceled();
                }
            }).ConfigureAwait(false);
        }

        ///<inheritdoc />
        public async Task<T> GenerateOnlyOnceUsingDistributedLockAsync<T>(DistributedLockKey key, Func<Task<T>> generator, TimeSpan waitTimeout)
        {
            var db = _redis.GetDatabase();
            var resourceValue = (string)await db.StringGetAsync(key.GetKey()).ConfigureAwait(false);
            if (resourceValue != RedisValue.Null)
            {
                return Deserialize<T>(resourceValue);
            }

            var identifier = Guid.NewGuid().ToString();
            var resourceLock = await db.StringSetAsync(key.GetLockKey(), identifier, DefaultLockTimeout, When.NotExists).ConfigureAwait(false);
            if (resourceLock)
            {
                var value = await GenerateResult(generator).ConfigureAwait(false);
                var redisValue = _redisSerializer.Serialize(value) ?? ""; //null is not a valid redis value

                // Storage of the value on Redis and notification is handled by us and we can return the value immediately.
                await db.ScriptEvaluateAsync(ScriptsLibrary.SetAndPublish, new { Key = key.GetKey(), LockNotifKey = key.GetLockNotifKey(), Value = redisValue }).ConfigureAwait(false);

                return value.GetResult();
            }

            return await GetTimeoutTaskAsync(waitTimeout, identifier, c => WaitForNotification<T>(db, key, identifier, c), id =>
            {
                if (_subscriptions.TryGetValue(key.GetLockNotifKey(), out var dico))
                {
                    dico.TryRemove(id, out var _);
                }
            }).ConfigureAwait(false);
        }

        private T Deserialize<T>(string redisValue)
        {
            return _redisSerializer.Deserialize<GenerationResult<T>>(redisValue).GetResult();
        }

        private async Task<GenerationResult<T>> GenerateResult<T>(Func<Task<T>> generator)
        {
            try
            {
                return new GenerationResult<T> { Result = await generator().ConfigureAwait(false) };
            }
            catch (Exception e)
            {
                return new GenerationResult<T> { ExceptionType = (e.InnerException ?? e).GetType().FullName, Message = (e.InnerException ?? e).Message };
            }
        }

        private async Task<T> WaitForNotification<T>(IDatabase db, DistributedLockKey key, string identifier, CancellationToken cancellationToken)
        {
            var subscription = new Subscriber
            {
                Identifier = identifier,
                Completion = new TaskCompletionSource<string>(),
                CancellationToken = cancellationToken
            };

            var dico = new ConcurrentDictionary<string, Subscriber>(new[] { new KeyValuePair<string, Subscriber>(identifier, subscription) });

            _subscriptions.AddOrUpdate(key.GetLockNotifKey(), dico, (k, existingDico) => { existingDico.AddOrUpdate(identifier, subscription, (i, s) => s); return existingDico; });

            // Then check again if the key is not present, because we might have just missed the notification.
            var resourceLocation = await db.StringGetAsync(key.GetKey()).ConfigureAwait(false);
            if (resourceLocation != RedisValue.Null)
            {
                //cleanup
                HandleNotification(key.GetLockNotifKey(), resourceLocation);
            }

            var result = await subscription.Completion.Task.ConfigureAwait(false);
            return Deserialize<T>(result);
        }

        private void Init(string applicationName)
        {
            //this pattern properly handles multitenant scenario
            var channel = new RedisChannel($"{applicationName}*/locknotif:*", RedisChannel.PatternMode.Pattern);
            _redis.GetSubscriber().Subscribe(channel, HandleNotification);
        }

        private void HandleNotification(RedisChannel channel, RedisValue message)
        {
            if (_subscriptions.TryRemove(channel, out var waitList))
            {
                foreach (var queued in waitList.Values.Where(q => !q.CancellationToken.IsCancellationRequested))
                {
                    // Resolves the task.
                    queued.Completion.TrySetResult(message);
                }
            }

            if (_queueForLocks.TryRemove(message, out var queuedLock))
            {
                _logger.LogDebug($"Notification for {message} on channel {channel}");
                var result = queuedLock.GetLockFactory();
                if (!queuedLock.Completion.TrySetResult(result))
                {
                    result.Dispose();
                }
            }
        }

        static async Task<T> GetTimeoutTaskAsync<T>(TimeSpan timeout, string identifier, Func<CancellationToken, Task<T>> actionAsync, Action<string> timeoutCleanup)
        {
            using (var ctsDelay = new CancellationTokenSource())
            using (var ctsTask = new CancellationTokenSource())
            {
                var delayTask = Task.Delay(timeout, ctsDelay.Token);
                var task = actionAsync(ctsTask.Token);

                var result = await Task.WhenAny(task, delayTask).ConfigureAwait(false);
                if (result == delayTask)
                {
                    ctsTask.Cancel();
                    timeoutCleanup?.Invoke(identifier);
                    throw new TimeoutException($"The operation {identifier} timed out after {timeout}.");
                }

                ctsDelay.Cancel();
                if (task.Exception != null)
                {
                    throw task.Exception.InnerException;
                }
                return task.Result;
            }
        }
    }
}