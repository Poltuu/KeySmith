using System.Threading;
using System.Threading.Tasks;

namespace KeySmith.Internals
{
    struct Subscriber
    {
        public string Identifier { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public TaskCompletionSource<string> Completion { get; set; }
    }
}
