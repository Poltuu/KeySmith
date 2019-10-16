
namespace KeySmith.Internals.Locks
{
    enum State
    {
        WaitingForKey,
        WithKey,
        Done
    }
}