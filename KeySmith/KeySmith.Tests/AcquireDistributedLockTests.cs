using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KeySmith.Tests
{
    public class AcquireLockTests
    {
        [Fact]
        public async Task DefaultScenario()
        {
            var keySpace = new KeySpaceConfiguration { Root = "AcquireLockTests.DefaultScenario" };
            var config = new Mock<IOptions<KeySpaceConfiguration>>();
            config.SetupGet(c => c.Value).Returns(keySpace);
            var key = new DistributedLockKey(keySpace.Root, "lockName");

            using (var connection = ConfigurationHelper.GetNewConnection())
            {
                var service = new RedisLockService(connection, config.Object, new RedisSerializer(), new Mock<ILogger<RedisLockService>>().Object);
                await service.InvalidateAsync(key);
                try
                {
                    using (var locker = await service.AcquireDistributedLockAsync(key, TimeSpan.Zero))
                    {
                        Assert.NotNull(locker);
                        Assert.Null(await service.TryAcquireDistributedLockAsync(key));
                        await Assert.ThrowsAsync<TimeoutException>(() => service.AcquireDistributedLockAsync(key, TimeSpan.Zero));
                        await Assert.ThrowsAsync<TimeoutException>(() => service.AcquireDistributedLockAsync(key, new TimeSpan(0, 0, 0, 0, 50)));
                    }

                    //this is the time for the service to instruct that the previous lock request is actually freeing the lock in question
                    await Task.Delay(1000);

                    using (var locker = await service.TryAcquireDistributedLockAsync(key))
                    {
                        Assert.NotNull(locker);
                    }
                }
                finally
                {
                    await service.InvalidateAsync(key);
                }
            }
        }

        [Theory]
        [InlineData(1, 10, 1)]
        [InlineData(10, 1, 5)]
        public async Task ConcurrencyScenarioBlock(int waitForLock, int taskLength, int expectedResult)
        {
            var keySpace = new KeySpaceConfiguration { Root = "AcquireLockTests.ConcurrencyScenarioBlock" };
            var config = new Mock<IOptions<KeySpaceConfiguration>>();
            config.SetupGet(c => c.Value).Returns(keySpace);
            var key = new DistributedLockKey(keySpace.Root, "lockName");

            var result = 0;
            var timeOutExceptions = 0;
            var testConfig = new TestConfig
            {
                TaskLength = new TimeSpan(0, 0, taskLength),
                WaitForLock = new TimeSpan(0, 0, waitForLock),
                IncrementResult = () => Interlocked.Increment(ref result),
                IncrementTimeOut = () => Interlocked.Increment(ref timeOutExceptions)
            };
            var host = new HostBuilder().ConfigureServices(s =>
            {
                s.AddSingleton(p => ConfigurationHelper.GetNewConnection());
                s.AddSingleton<IRedisSerializer>(new RedisSerializer());
                s.AddSingleton(new Mock<ILogger<RedisLockService>>().Object);
                s.AddSingleton(key);
                s.AddSingleton<RedisLockService>();
                s.AddSingleton(testConfig);

                s.AddHostedService<AcquireLockHostedService>();
                s.AddHostedService<A2>();
                s.AddHostedService<A3>();
                s.AddHostedService<A4>();
                s.AddHostedService<A5>();
            }).Build();

            try
            {
                await host.Services.GetRequiredService<RedisLockService>().InvalidateAsync(key);
                //ACT
                await Task.WhenAny(host.RunAsync(), Task.Delay(10000));

                //ASSERT
                if (testConfig.Errors.Count != 0)
                {
                    throw new AggregateException(testConfig.Errors.ToArray());
                }
                Assert.Equal(expectedResult, result);
                Assert.Equal(5 - expectedResult, timeOutExceptions);
            }
            finally
            {
                //CLEAN
                await host.StopAsync();
            }
        }
    }

    public class TestConfig
    {
        public TimeSpan WaitForLock { get; set; }
        public TimeSpan TaskLength { get; set; }
        public Action IncrementResult { get; set; }
        public Action IncrementTimeOut { get; set; }
        public ConcurrentBag<Exception> Errors { get; set; } = new ConcurrentBag<Exception>();
    }

    public class A5 : AcquireLockHostedService { public A5(RedisLockService s, DistributedLockKey key, TestConfig t) : base(s, key, t) { } }
    public class A4 : AcquireLockHostedService { public A4(RedisLockService s, DistributedLockKey key, TestConfig t) : base(s, key, t) { } }
    public class A3 : AcquireLockHostedService { public A3(RedisLockService s, DistributedLockKey key, TestConfig t) : base(s, key, t) { } }
    public class A2 : AcquireLockHostedService { public A2(RedisLockService s, DistributedLockKey key, TestConfig t) : base(s, key, t) { } }
    public class AcquireLockHostedService : BackgroundService
    {
        private readonly RedisLockService _redisLockService;
        private readonly DistributedLockKey _key;
        private readonly TestConfig _testConfig;

        public AcquireLockHostedService(RedisLockService redisLockService, DistributedLockKey key, TestConfig testConfig)
        {
            _redisLockService = redisLockService ?? throw new ArgumentNullException(nameof(redisLockService));
            _key = key ?? throw new ArgumentNullException(nameof(key));
            _testConfig = testConfig ?? throw new ArgumentNullException(nameof(testConfig));
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return _redisLockService.InvalidateAsync(_key);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                using (var locker = await _redisLockService.AcquireDistributedLockAsync(_key, _testConfig.WaitForLock))
                {
                    _testConfig.IncrementResult();
                    await Task.Delay(_testConfig.TaskLength);
                }
            }
            catch (RedisTimeoutException e)
            {
                _testConfig.Errors.Add(e);
            }
            catch (TimeoutException)
            {
                _testConfig.IncrementTimeOut();
            }
            catch (Exception e)
            {
                _testConfig.Errors.Add(e);
            }
        }
    }
}