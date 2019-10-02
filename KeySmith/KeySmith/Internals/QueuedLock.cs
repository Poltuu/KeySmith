using System;
using System.Threading.Tasks;

namespace KeySmith.Internals
{
    struct QueuedLock
    {
        public TaskCompletionSource<IDisposable> Completion { get; set; }
        public Func<IDisposable> GetLockFactory { get; set; }
    }
}
