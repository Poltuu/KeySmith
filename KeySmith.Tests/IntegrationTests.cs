using KeySmith.Internals.Scripts;
using StackExchange.Redis;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KeySmith.Tests
{
    public class IntegrationTests
    {
        [Fact]
        public async Task SimpleScenario()
        {
            var connection = ConfigurationHelper.GetNewConnection();
            var library = new ScriptLibrary(connection);
            var service = new LockService(library);

            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var key = new Key(root, "name", TimeSpan.FromSeconds(1));

            var result = false;
            var db = connection.GetDatabase();
            await ResetKeys(db, key);
            try
            {
                await service.LockAsync(key, c =>
                {
                    result = true;
                    return Task.CompletedTask;
                }, CancellationToken.None);

                Assert.True(result);
                Assert.False(await db.KeyExistsAsync(key.GetLockKey()));
                Assert.False(await db.KeyExistsAsync(key.GetLockQueueKey()));
                Assert.False(await db.KeyExistsAsync(key.GetLockQueueSetKey()));
            }
            finally
            {
                await ResetKeys(db, key);
            }
        }

        [Fact]
        public async Task CancelScenario()
        {
            var connection = ConfigurationHelper.GetNewConnection();
            var library = new ScriptLibrary(connection);
            var service = new LockService(library);

            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var key = new Key(root, "name", TimeSpan.FromSeconds(2));

            var db = connection.GetDatabase();
            await ResetKeys(db, key);
            try
            {
                using var cancel = new CancellationTokenSource();
                cancel.CancelAfter(500);
                try
                {
                    await service.LockAsync(key, c => Task.Delay(1000, c), cancel.Token);
                }
                catch (Exception e)
                {
                    Assert.IsType<TaskCanceledException>(e.InnerException?.InnerException ?? e.InnerException ?? e);
                }

                Assert.False(await db.KeyExistsAsync(key.GetLockKey()));
                Assert.False(await db.KeyExistsAsync(key.GetLockQueueKey()));
                Assert.False(await db.KeyExistsAsync(key.GetLockQueueSetKey()));
            }
            finally
            {
                await ResetKeys(db, key);
            }
        }

        [Fact]
        public async Task ConcurrencyScenario()
        {
            var connection = ConfigurationHelper.GetNewConnection();
            var library = new ScriptLibrary(connection);
            var service = new LockService(library);

            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var key = new Key(root, "name", TimeSpan.FromSeconds(300));

            var db = connection.GetDatabase();

            var concurrencyScenarioCounter = 0;
            var errorCount = 0;
            var counter = 0;
            await ResetKeys(db, key);
            try
            {
                var watch = new Stopwatch();
                var tasks = Enumerable.Range(0, 100).Select(i => service.LockAsync(key, async c =>
                 {
                     Interlocked.Increment(ref concurrencyScenarioCounter);
                     if (concurrencyScenarioCounter != 1)
                     {
                         watch.Stop();
                         Interlocked.Increment(ref errorCount);
                         throw new Exception($"Lock failed after {watch.ElapsedMilliseconds}");
                     }
                     Interlocked.Increment(ref counter);
                     await Task.Delay(10);
                     Interlocked.Decrement(ref concurrencyScenarioCounter);
                 }, CancellationToken.None));

                watch.Start();
                await Task.WhenAll(tasks);

                Assert.Equal(100, counter);
                Assert.Equal(0, errorCount);

                Assert.False(await db.KeyExistsAsync(key.GetLockKey()));
                Assert.False(await db.KeyExistsAsync(key.GetLockQueueKey()));
                Assert.False(await db.KeyExistsAsync(key.GetLockQueueSetKey()));
            }
            finally
            {
                await ResetKeys(db, key);
            }
        }

        private Task ResetKeys(IDatabase db, Key key)
            => db.KeyDeleteAsync(new RedisKey[] { key.GetLockKey(), key.GetLockQueueKey(), key.GetLockQueueSetKey() });
    }
}