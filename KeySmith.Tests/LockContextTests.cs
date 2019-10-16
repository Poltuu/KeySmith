using KeySmith.Internals.Locks;
using System;
using System.Threading;
using Xunit;

namespace KeySmith.Tests
{
    public class LockContextTests
    {
        [Theory]
        [InlineData("", State.WaitingForKey)]
        [InlineData("", State.WithKey)]
        [InlineData("", State.Done)]
        [InlineData("other", State.WaitingForKey)]
        [InlineData("other", State.WithKey)]
        [InlineData("other", State.Done)]
        [InlineData("key", State.WaitingForKey)]
        [InlineData("key", State.WithKey)]
        [InlineData("key", State.Done)]
        public void HandlerTests(string message, object initialState)
        {
            using var context = new LockState(new Key("", "", TimeSpan.FromSeconds(1)), "key", CancellationToken.None);
            switch (initialState)
            {
                case State.WithKey:
                    context.SetWithKey();
                    break;
                case State.Done:
                    context.SetDone();
                    break;
            }

            context.Handler(new StackExchange.Redis.RedisChannel(), message);

            switch (initialState)
            {
                case State.WaitingForKey:
                    if (context.Identifier == message)
                    {
                        Assert.Equal(State.WithKey, context.State);
                    }
                    break;
                case State.WithKey:
                case State.Done:
                    Assert.Equal(State.Done, context.State);
                    break;
            }
        }
    }
}