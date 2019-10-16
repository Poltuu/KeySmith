using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KeySmith.Tests
{
    public class IntegrationMemoTests
    {
        [Fact]
        public async Task SimpleScenario()
        {
            var provider = new ServiceCollection()
                .AddKeySmith(ConfigurationHelper.GetConfiguration())
                .BuildServiceProvider();

            var service = provider.GetRequiredService<IMemoLockService>();

            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var memoKey = new MemoKey(root, "name", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            var db = provider.GetRequiredService<ConnectionMultiplexer>().GetDatabase();
            await ResetKeys(db, memoKey);
            try
            {
                RedisValue answer = "answer";
                var result = await service.MemoLockAsync(memoKey, c => Task.FromResult(answer), CancellationToken.None);

                Assert.Equal(answer, result);
                Assert.True(await db.KeyExistsAsync(memoKey.GetValueKey()));//value is in redis
                Assert.False(await db.KeyExistsAsync(memoKey.GetErrorKey()));
            }
            finally
            {
                await ResetKeys(db, memoKey);
            }
        }

        [Fact]
        public async Task SimpleFailScenario()
        {
            var provider = new ServiceCollection()
                .AddKeySmith(ConfigurationHelper.GetConfiguration())
                .BuildServiceProvider();

            var service = provider.GetRequiredService<IMemoLockService>();

            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var memoKey = new MemoKey(root, "name", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

            var db = provider.GetRequiredService<ConnectionMultiplexer>().GetDatabase();
            await ResetKeys(db, memoKey);
            try
            {
                try
                {
                    await service.MemoLockAsync(memoKey, c => throw new NullReferenceException("oups"), CancellationToken.None);

                    throw new Exception("Nothing was thrown");
                }
                catch (NullReferenceException)
                {
                }
                catch (GenerationException)// we actually get it from redis before we had time to clean the setup.
                {
                }

                try
                {
                    RedisValue answer = "answer";
                    await service.MemoLockAsync(memoKey, c => Task.FromResult(answer), CancellationToken.None);

                    throw new Exception("Nothing was thrown");
                }
                catch (GenerationException)
                {
                }

                Assert.False(await db.KeyExistsAsync(memoKey.GetValueKey()));
                Assert.True(await db.KeyExistsAsync(memoKey.GetErrorKey()));
            }
            finally
            {
                await ResetKeys(db, memoKey);
            }
        }

        [Fact]
        public async Task ConcurrencyScenario()
        {
            var provider = new ServiceCollection()
                .AddKeySmith(ConfigurationHelper.GetConfiguration())
                .BuildServiceProvider();

            var service = provider.GetRequiredService<IMemoLockService>();

            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var memoKey = new MemoKey(root, "name", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            var db = provider.GetRequiredService<ConnectionMultiplexer>().GetDatabase();

            var concurrencyScenarioCounter = 0;
            var errorCount = 0;
            var counter = 0;
            await ResetKeys(db, memoKey);
            try
            {
                var tasks = Enumerable.Range(0, 100).Select(i => service.MemoLockAsync(memoKey, async c =>
                {
                    Interlocked.Increment(ref concurrencyScenarioCounter);
                    if (concurrencyScenarioCounter != 1)
                    {
                        Interlocked.Increment(ref errorCount);
                    }
                    Interlocked.Increment(ref counter);
                    await Task.Delay(10);
                    Interlocked.Decrement(ref concurrencyScenarioCounter);
                    return 42;
                }, CancellationToken.None));

                var answers = await Task.WhenAll(tasks);

                Assert.Equal(1, counter);
                Assert.Equal(0, errorCount);

                foreach (var answer in answers)
                {
                    Assert.Equal(42, answer);
                }

                Assert.False(await db.KeyExistsAsync(memoKey.GetErrorKey()));
                Assert.True(await db.KeyExistsAsync(memoKey.GetValueKey()));
            }
            finally
            {
                await ResetKeys(db, memoKey);
            }
        }

        private Task ResetKeys(IDatabase db, MemoKey memoKey)
        {
            var key = memoKey.GetLockKey();
            return db.KeyDeleteAsync(new RedisKey[] { memoKey.GetErrorKey(), memoKey.GetValueKey(), key.GetLockKey(), key.GetLockQueueKey(), key.GetLockQueueSetKey() });
        }
    }
}