using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nito.AsyncEx;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KeySmith.Tests
{
    public class TryAcquireDistributedLockTests
    {
        static int? value1;
        static int count1;
        static int count2;

        [Fact]
        public async Task TestTryAcquireDistributedLock()
        {
            var config = new Mock<IOptions<KeySpaceConfiguration>>();
            config.SetupGet(c => c.Value).Returns(new KeySpaceConfiguration { ApplicationName = "MyApplicationName" });
            var connection = ConfigurationHelper.GetConnection();
            var service = new RedisLockService(connection, config.Object, new RedisSerializer(), new Mock<ILogger<RedisLockService>>().Object);
            var key = new DistributedLockKey("MyApplicationName", "top-lock");

            value1 = null;
            count1 = 0;
            count2 = 0;
            await service.InvalidateAsync(key);

            var parallel = Parallel.For(0, 5, index =>
            {
                AsyncContext.Run(() => Task.WhenAll(Enumerable.Range(0, 10).Select(i => GetLockAndWaitAsync(i, service, key))));
            });
            await service.InvalidateAsync(key);

            Assert.Equal(1, count2);
            Assert.Equal(50, count1);
            Assert.NotNull(value1);
        }

        async Task GetLockAndWaitAsync(int index, RedisLockService service, DistributedLockKey key)
        {
            Interlocked.Increment(ref count1);
            using (var locker = await service.TryAcquireDistributedLockAsync(key))
            {
                if (locker != null)
                {
                    if (value1 != null)
                    {
                        throw new Exception("Concurrency problem");
                    }
                    Interlocked.Increment(ref count2);
                    value1 = index;
                    await Task.Delay(1000);
                }
            }
        }

        static int count1_b;
        static int count2_b;

        [Fact]
        public async Task TestTryAcquireDistributedLockRelease()
        {
            var config = new Mock<IOptions<KeySpaceConfiguration>>();
            config.SetupGet(c => c.Value).Returns(new KeySpaceConfiguration { ApplicationName = "MyApplicationName2" });
            var connection = ConfigurationHelper.GetConnection();
            var service = new RedisLockService(connection, config.Object, new RedisSerializer(), new Mock<ILogger<RedisLockService>>().Object);
            var key = new DistributedLockKey("MyApplicationName2", "top-lock2");

            count1_b = 0;
            count2_b = 0;
            await service.InvalidateAsync(key);

            for (var i = 0; i < 12; i++)
            {
                await GetLockForNoTimeAsync(service, key);
            }
            await service.InvalidateAsync(key);

            Assert.Equal(12, count1_b);
            Assert.Equal(12, count2_b);
        }

        async Task GetLockForNoTimeAsync(RedisLockService service, DistributedLockKey key)
        {
            Interlocked.Increment(ref count1_b);
            using (var locker = await service.TryAcquireDistributedLockAsync(key))
            {
                if (locker != null)
                {
                    Interlocked.Increment(ref count2_b);
                    await Task.Delay(1);
                }
            }
        }
    }
}
