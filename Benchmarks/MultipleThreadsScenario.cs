using BenchmarkDotNet.Attributes;
using KeySmith;
using Medallion.Threading.Sql;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class MultipleThreadsScenario
    {
        /// <summary>
        /// Change this to change task time
        /// </summary>
        private static readonly int _taskTime = 1;

        /// <summary>
        /// Change this to change how many thread are asking for the lock at the same time
        /// </summary>
        [Params(1, 2, 5, 10, 20, 50, 100)]
        public int Count { get; set; }

        //Native
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        //KeySmith
        private readonly ILockService _lockService;
        private readonly Key _key;

        //distributedLock
        private readonly SqlDistributedLock _sqlDistributedLock;

        //RedLockNET
        //private readonly RedLockFactory _redLockFactory;

        public MultipleThreadsScenario()
        {
            var services = new ServiceCollection();

            //KeySmith
            services.AddKeySmith("localhost:6379");

            var provider = services.BuildServiceProvider();

            _lockService = provider.GetRequiredService<ILockService>();
            _key = new Key(Guid.NewGuid().ToString(), "mylock", TimeSpan.FromMilliseconds(500));

            //TODO fix connectionstring
            _sqlDistributedLock = new SqlDistributedLock("lockName", "Data Source=localhost;Initial Catalog=WS_EXCHANGE_RATE;user=ilucca;password=ilucca;MultipleActiveResultSets=True");

            //RedLock
            //_redLockFactory = RedLockFactory.Create(new List<RedLockMultiplexer> { provider.GetRequiredService<ConnectionMultiplexer>() });
        }

        private static Task Times(int count, Func<Task> task) => Task.WhenAll(Enumerable.Range(0, count).Select(i => task()));

        [Benchmark]
        public Task Unsynchronized() => Times(Count, () => SharedTask(CancellationToken.None));

        [Benchmark]
        public Task Native() => Times(Count, NativeTask);
        async Task NativeTask()
        {
            await _semaphore.WaitAsync();
            try
            {
                await SharedTask(CancellationToken.None);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        [Benchmark]
        public Task KeySmith() => Times(Count, KeySmithTask);
        public Task KeySmithTask() => _lockService.LockAsync(_key, SharedTask, CancellationToken.None);

        [Benchmark]
        public Task SqlDistributedLock() => Times(Count, SqlDistributedLockTask);
        public async Task SqlDistributedLockTask()
        {
            using (var sqlLock = await _sqlDistributedLock.AcquireAsync())
            {
                await SharedTask(CancellationToken.None);
            }
        }

        //[Benchmark]
        //public Task RedLock() => Times(_count, RedLockTask);
        //public async Task RedLockTask()
        //{
        //    using (var locker = await _redLockFactory.CreateLockAsync("resource", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(10)))
        //    {
        //        if (!locker.IsAcquired)
        //        {
        //            throw new InvalidOperationException("Not testing properly");
        //        }
        //        await SharedTask(CancellationToken.None);
        //    }
        //}

        private static Task SharedTask(CancellationToken token) => Task.Delay(_taskTime);
    }
}