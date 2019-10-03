using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KeySmith.Tests
{
    public class TryAcquireLockTests
    {
        [Fact]
        public async Task DefaultScenario()
        {
            var keySpace = new KeySpaceConfiguration { Root = "TryAcquireLockTests.DefaultScenario" };
            var config = new Mock<IOptions<KeySpaceConfiguration>>();
            config.SetupGet(c => c.Value).Returns(keySpace);
            var key = new DistributedLockKey(keySpace.Root, "lockName");

            using (var connection = ConfigurationHelper.GetNewConnection())
            {
                var service = new RedisLockService(connection, config.Object, new RedisSerializer(), new Mock<ILogger<RedisLockService>>().Object);

                await service.InvalidateAsync(key);
                try
                {
                    using (var locker = await service.TryAcquireDistributedLockAsync(key))
                    {
                        Assert.NotNull(locker);
                        Assert.Null(await service.TryAcquireDistributedLockAsync(key));
                    }

                    using (var locker = await service.TryAcquireDistributedLockAsync(key))
                    {
                        Assert.NotNull(locker);
                        Assert.Null(await service.TryAcquireDistributedLockAsync(key));
                    }
                }
                finally
                {
                    await service.InvalidateAsync(key);
                }
            }
        }

        public static int ConcurrencyScenarioBlockResult = 0;
        public static int ConcurrencyScenarioBlockedResult = 0;
        [Fact]
        public async Task ConcurrencyScenarioBlock()
        {
            var keySpace = new KeySpaceConfiguration { Root = "TryAcquireLockTests.ConcurrencyScenarioBlock" };
            var config = new Mock<IOptions<KeySpaceConfiguration>>();
            config.SetupGet(c => c.Value).Returns(keySpace);
            var key = new DistributedLockKey(keySpace.Root, "lockName");

            var host = new HostBuilder().ConfigureServices(s =>
            {
                s.AddSingleton(p => ConfigurationHelper.GetNewConnection());
                s.AddSingleton<IRedisSerializer>(new RedisSerializer());
                s.AddSingleton(new Mock<ILogger<RedisLockService>>().Object);
                s.AddSingleton(key);
                s.AddSingleton<RedisLockService>();

                s.AddHostedService<TryAcquireLockHostedService>();
                s.AddHostedService<T2>();
                s.AddHostedService<T3>();
                s.AddHostedService<T4>();
                s.AddHostedService<T5>();
            }).Build();

            try
            {
                await host.Services.GetRequiredService<RedisLockService>().InvalidateAsync(key);
                var services = host.Services.GetRequiredService<IEnumerable<IHostedService>>();
                ConcurrencyScenarioBlockResult = 0;
                ConcurrencyScenarioBlockedResult = 0;

                await Task.WhenAny(host.RunAsync(), Task.Delay(3000));

                Assert.Equal(1, ConcurrencyScenarioBlockResult);
                Assert.Equal(4, ConcurrencyScenarioBlockedResult);
            }
            finally
            {
                await host.StopAsync();
            }
        }
    }

    public class T5 : TryAcquireLockHostedService { public T5(RedisLockService s, DistributedLockKey key) : base(s, key) { } }
    public class T4 : TryAcquireLockHostedService { public T4(RedisLockService s, DistributedLockKey key) : base(s, key) { } }
    public class T3 : TryAcquireLockHostedService { public T3(RedisLockService s, DistributedLockKey key) : base(s, key) { } }
    public class T2 : TryAcquireLockHostedService { public T2(RedisLockService s, DistributedLockKey key) : base(s, key) { } }
    public class TryAcquireLockHostedService : BackgroundService
    {
        private readonly RedisLockService _redisLockService;
        private readonly DistributedLockKey _key;

        public TryAcquireLockHostedService(RedisLockService redisLockService, DistributedLockKey key)
        {
            _redisLockService = redisLockService;
            _key = key;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var locker = await _redisLockService.TryAcquireDistributedLockAsync(_key))
            {
                if (locker != null)
                {
                    Interlocked.Increment(ref TryAcquireLockTests.ConcurrencyScenarioBlockResult);
                    await Task.Delay(4000);
                }
                else
                {
                    Interlocked.Increment(ref TryAcquireLockTests.ConcurrencyScenarioBlockedResult);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return _redisLockService.InvalidateAsync(_key);
        }
    }
}