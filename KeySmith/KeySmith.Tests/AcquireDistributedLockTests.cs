using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nito.AsyncEx;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace KeySmith.Tests
{
    public class AcquireDistributedLockTests
    {
        static int value1;
        static int count;

        private readonly ITestOutputHelper output;

        public AcquireDistributedLockTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task TestAcquireDistributedLock()
        {
            var logger = new Mock<ILogger<RedisLockService>>();
            logger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<RedisLockService>(), It.IsAny<Exception>(), It.IsAny<Func<RedisLockService, Exception, string>>()))
                .Callback((LogLevel l, EventId i, RedisLockService s, Exception e, Func<RedisLockService, Exception, string> f) => output.WriteLine(f(s, e)));

            var config = new Mock<IOptions<KeySpaceConfiguration>>();
            config.SetupGet(c => c.Value).Returns(new KeySpaceConfiguration { ApplicationName = "MyApplicationName3" });
            var connection = ConfigurationHelper.GetConnection();
            var service = new RedisLockService(connection, config.Object, new RedisSerializer(), logger.Object);
            var key = new DistributedLockKey("MyApplicationName3", "top-lock3");

            value1 = 0;
            count = 0;

            await service.InvalidateAsync(key);

            try
            {
                var parallel = Parallel.For(0, 3, index =>
                {
                    AsyncContext.Run(() => Task.WhenAll(Enumerable.Range(0, 10).Select(i => GetLockAndWaitAsync((10 * index) + i, service, key))));
                });

                Assert.Equal(30, count);
                Assert.Equal(29 * 30 / 2, value1);
            }
            catch (Exception e)
            {
                throw new Exception($"Test failed: total={value1}", e);
            }
            finally
            {
                await service.InvalidateAsync(key);
            }
        }

        async Task GetLockAndWaitAsync(int index, RedisLockService service, DistributedLockKey key)
        {
            using (var locker = await service.AcquireDistributedLockAsync(key, TimeSpan.FromMilliseconds(20000)))
            {
                if (locker == null)
                {
                    throw new Exception("Concurrency problem");
                }
                Interlocked.Increment(ref count);
                Interlocked.Add(ref value1, index);
                await Task.Delay(10);
            }
        }
    }
}
