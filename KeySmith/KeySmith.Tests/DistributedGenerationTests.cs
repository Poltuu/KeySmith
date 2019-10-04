using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KeySmith.Tests
{
    public class MyClass
    {
        public int Property { get; set; }
    }

    public class GenerationTests
    {
        static int GenerationCount = 0;
        async Task<MyClass> Generator()
        {
            Interlocked.Increment(ref GenerationCount);
            await Task.Delay(10);
            return new MyClass { Property = 23 };
        }

        [Fact]
        public async Task DefaultScenario()
        {
            var keySpace = new KeySpaceConfiguration { Root = "GenerationTests.DefaultScenario" };
            var config = new Mock<IOptions<KeySpaceConfiguration>>();
            config.SetupGet(c => c.Value).Returns(keySpace);
            var key = new DistributedLockKey(keySpace.Root, "lockName", new TimeSpan(0, 0, 3), new TimeSpan(0, 10, 0));

            using (var connection = ConfigurationHelper.GetNewConnection())
            {
                var service = new RedisLockService(connection, config.Object, new RedisSerializer(), new Mock<ILogger<RedisLockService>>().Object);
                await service.InvalidateAsync(key);
                try
                {
                    GenerationCount = 0;
                    var t = service.GenerateOnlyOnceUsingDistributedLockAsync(key, Generator, new TimeSpan(0, 0, 3));
                    var t2 = service.GenerateOnlyOnceUsingDistributedLockAsync(key, Generator, new TimeSpan(0, 0, 3));
                    var t3 = service.GenerateOnlyOnceUsingDistributedLockAsync(key, Generator, new TimeSpan(0, 0, 3));
                    var t4 = service.GenerateOnlyOnceUsingDistributedLockAsync(key, Generator, new TimeSpan(0, 0, 3));

                    var results = await Task.WhenAll(t, t2, t3, t4);
                    var result = await service.GenerateOnlyOnceUsingDistributedLockAsync(key, Generator, new TimeSpan(0, 0, 3));

                    Assert.Equal(1, GenerationCount);
                    Assert.True(results.All(r => r.Property == 23));
                    Assert.Equal(23, result.Property);
                }
                finally
                {
                    await service.InvalidateAsync(key);
                }
            }
        }

        public static int ConcurrencyScenarioGenerator = 0;
        public static ConcurrentBag<MyClass> ConcurrencyScenarioGenerationResults = new ConcurrentBag<MyClass>();
        public static ConcurrentBag<Exception> ConcurrencyScenarioGenerationExceptions = new ConcurrentBag<Exception>();

        [Fact]
        public async Task ConcurrencyScenarioGeneration()
        {
            var keySpace = new KeySpaceConfiguration { Root = "GenerationTests.ConcurrencyScenarioGeneration" };
            var config = new Mock<IOptions<KeySpaceConfiguration>>();
            config.SetupGet(c => c.Value).Returns(keySpace);
            var key = new DistributedLockKey(keySpace.Root, "lockName", new TimeSpan(0, 0, 3), new TimeSpan(0, 10, 0));

            var host = new HostBuilder().ConfigureServices(s =>
            {
                s.AddSingleton(p => ConfigurationHelper.GetNewConnection());
                s.AddSingleton<IRedisSerializer>(new RedisSerializer());
                s.AddSingleton(new Mock<ILogger<RedisLockService>>().Object);
                s.AddSingleton(key);
                s.AddSingleton<RedisLockService>();

                s.AddHostedService<GeneratorHostedService>();
                s.AddHostedService<G2>();
                s.AddHostedService<G3>();
                s.AddHostedService<G4>();
                s.AddHostedService<G5>();
            }).Build();

            try
            {
                await host.Services.GetRequiredService<RedisLockService>().InvalidateAsync(key);
                var services = host.Services.GetRequiredService<IEnumerable<IHostedService>>();
                ConcurrencyScenarioGenerator = 0;
                ConcurrencyScenarioGenerationResults = new ConcurrentBag<MyClass>();
                ConcurrencyScenarioGenerationExceptions = new ConcurrentBag<Exception>();

                await Task.WhenAny(host.RunAsync(), Task.Delay(5000));

                if (ConcurrencyScenarioGenerationExceptions.Count != 0)
                {
                    throw new AggregateException(ConcurrencyScenarioGenerationExceptions.ToArray());
                }

                Assert.Equal(1, ConcurrencyScenarioGenerator);
                Assert.Equal(5, ConcurrencyScenarioGenerationResults.Count);
                Assert.True(ConcurrencyScenarioGenerationResults.All(r => r.Property == 23));
            }
            finally
            {
                await host.StopAsync();
            }
        }

        public static ConcurrentBag<Exception> ConcurrencyScenarioGenerationFails = new ConcurrentBag<Exception>();

        [Fact]
        public async Task ConcurrencyScenarioGenerationFailed()
        {
            var keySpace = new KeySpaceConfiguration { Root = "GenerationTests.ConcurrencyScenarioGenerationFailed" };
            var config = new Mock<IOptions<KeySpaceConfiguration>>();
            config.SetupGet(c => c.Value).Returns(keySpace);
            var key = new DistributedLockKey(keySpace.Root, "lockName", new TimeSpan(0, 0, 3), new TimeSpan(0, 10, 0));

            var host = new HostBuilder().ConfigureServices(s =>
            {
                s.AddSingleton(p => ConfigurationHelper.GetNewConnection());
                s.AddSingleton<IRedisSerializer>(new RedisSerializer());
                s.AddSingleton(new Mock<ILogger<RedisLockService>>().Object);
                s.AddSingleton(key);
                s.AddSingleton<RedisLockService>();

                s.AddHostedService<FailedGeneratorHostedService>();
                s.AddHostedService<F2>();
                s.AddHostedService<F3>();
                s.AddHostedService<F4>();
                s.AddHostedService<F5>();
            }).Build();

            try
            {
                await host.Services.GetRequiredService<RedisLockService>().InvalidateAsync(key);
                var services = host.Services.GetRequiredService<IEnumerable<IHostedService>>();

                ConcurrencyScenarioGenerationFails = new ConcurrentBag<Exception>();

                await Task.WhenAny(host.RunAsync(), Task.Delay(5000));

                Assert.Equal(5, ConcurrencyScenarioGenerationFails.Count);
                Assert.Equal(5, ConcurrencyScenarioGenerationFails.OfType<GenerationException>().Count());
            }
            finally
            {
                await host.StopAsync();
            }
        }
    }

    public class G5 : GeneratorHostedService { public G5(RedisLockService s, DistributedLockKey key) : base(s, key) { } }
    public class G4 : GeneratorHostedService { public G4(RedisLockService s, DistributedLockKey key) : base(s, key) { } }
    public class G3 : GeneratorHostedService { public G3(RedisLockService s, DistributedLockKey key) : base(s, key) { } }
    public class G2 : GeneratorHostedService { public G2(RedisLockService s, DistributedLockKey key) : base(s, key) { } }
    public class GeneratorHostedService : BackgroundService
    {
        private readonly RedisLockService _redisLockService;
        private readonly DistributedLockKey _key;

        public GeneratorHostedService(RedisLockService redisLockService, DistributedLockKey key)
        {
            _redisLockService = redisLockService;
            _key = key;
        }

        async Task<MyClass> Generator()
        {
            Interlocked.Increment(ref GenerationTests.ConcurrencyScenarioGenerator);
            await Task.Delay(1000);
            return new MyClass { Property = 23 };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var result = await _redisLockService.GenerateOnlyOnceUsingDistributedLockAsync(_key, Generator, new TimeSpan(0, 0, 3));
                GenerationTests.ConcurrencyScenarioGenerationResults.Add(result);
            }
            catch (Exception e)
            {
                GenerationTests.ConcurrencyScenarioGenerationExceptions.Add(e);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return _redisLockService.InvalidateAsync(_key);
        }
    }

    public class F5 : FailedGeneratorHostedService { public F5(RedisLockService s, DistributedLockKey key) : base(s, key) { } }
    public class F4 : FailedGeneratorHostedService { public F4(RedisLockService s, DistributedLockKey key) : base(s, key) { } }
    public class F3 : FailedGeneratorHostedService { public F3(RedisLockService s, DistributedLockKey key) : base(s, key) { } }
    public class F2 : FailedGeneratorHostedService { public F2(RedisLockService s, DistributedLockKey key) : base(s, key) { } }
    public class FailedGeneratorHostedService : BackgroundService
    {
        private readonly RedisLockService _redisLockService;
        private readonly DistributedLockKey _key;

        public FailedGeneratorHostedService(RedisLockService redisLockService, DistributedLockKey key)
        {
            _redisLockService = redisLockService;
            _key = key;
        }

        Task<MyClass> Generator() => throw new ApplicationException("aaa");

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var result = await _redisLockService.GenerateOnlyOnceUsingDistributedLockAsync(_key, Generator, new TimeSpan(0, 0, 3));
            }
            catch (Exception e)
            {
                GenerationTests.ConcurrencyScenarioGenerationFails.Add(e);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return _redisLockService.InvalidateAsync(_key);
        }
    }
}
