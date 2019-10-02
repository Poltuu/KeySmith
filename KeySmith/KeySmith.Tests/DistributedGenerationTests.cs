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
    public class DistributedGenerationTests
    {
        class MyClass
        {
            public int Property { get; set; }
        }

        static int value1;

        [Fact]
        public async Task DistributedGenerationLockTests()
        {
            var config = new Mock<IOptions<KeySpaceConfiguration>>();
            config.SetupGet(c => c.Value).Returns(new KeySpaceConfiguration { ApplicationName = "MyApplicationName4" });
            var connection = ConfigurationHelper.GetConnection();
            var service = new RedisLockService(connection, config.Object, new RedisSerializer(), new Mock<ILogger<RedisLockService>>().Object);
            var key = new DistributedLockKey("MyApplicationName4", "top-lock4");

            value1 = 0;

            await service.InvalidateAsync(key);

            var parallel = Parallel.For(0, 10, index =>
            {
                var results = AsyncContext.Run(() => Task.WhenAll(Enumerable.Range(0, 10).Select(i => InstanciateWithFirstValue(service, key))));

                foreach (var result in results)
                {
                    Assert.Equal(23, result.Property);
                }
            });

            await service.InvalidateAsync(key);

            Assert.Equal(1, value1);
        }

        Task<MyClass> InstanciateWithFirstValue(RedisLockService service, DistributedLockKey key)
        {
            return service.GenerateOnlyOnceUsingDistributedLockAsync(key, async () =>
            {
                Interlocked.Increment(ref value1);
                await Task.Delay(20);
                return new MyClass { Property = 23 };
            }, TimeSpan.FromMilliseconds(10000));
        }

        static int value2;

        [Fact]
        public async Task DistributedFailedGenerationLockTests()
        {
            var config = new Mock<IOptions<KeySpaceConfiguration>>();
            config.SetupGet(c => c.Value).Returns(new KeySpaceConfiguration { ApplicationName = "MyApplicationName5" });
            var connection = ConfigurationHelper.GetConnection();
            var service = new RedisLockService(connection, config.Object, new RedisSerializer(), new Mock<ILogger<RedisLockService>>().Object);
            var key = new DistributedLockKey("MyApplicationName5", "top-lock5");

            value2 = 0;

            await service.InvalidateAsync(key);

            try
            {
                var parallel = Parallel.For(0, 10, index =>
                {
                    var results = AsyncContext.Run(() => Task.WhenAll(Enumerable.Range(0, 10).Select(i => Fail(service, key))));
                });
            }
            catch (AggregateException e)
            {
                Assert.Equal(10, e.InnerExceptions.Count);
                Assert.Single(e.InnerExceptions.OfType<ArgumentException>());
                Assert.Equal(9, e.InnerExceptions.OfType<AggregateException>().Count());

                foreach (var error in e.InnerExceptions.OfType<AggregateException>())
                {
                    Assert.Single(error.InnerExceptions);
                    Assert.IsType<ArgumentException>(error.InnerException);
                }
            }

            await service.InvalidateAsync(key);

            Assert.Equal(1, value2);
        }

        Task<MyClass> Fail(RedisLockService service, DistributedLockKey key)
        {
            return service.GenerateOnlyOnceUsingDistributedLockAsync<MyClass>(key, async () =>
            {
                Interlocked.Increment(ref value2);
                await Task.Delay(1);
                throw new ArgumentException("Big fail");
            }, TimeSpan.FromMilliseconds(10000));
        }
    }
}
