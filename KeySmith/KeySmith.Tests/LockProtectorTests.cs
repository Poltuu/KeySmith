using KeySmith.Internals.Locks;
using KeySmith.Internals.Scripts;
using KeySmith.Internals.Scripts.Parameters;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KeySmith.Tests
{
    public class LockProtectorTests
    {
        [Fact]
        public void EnsureDisposeWorks()
        {
            var library = new Mock<IScriptLibrary>();
            using var context = new LockState(new Key("", "", TimeSpan.FromSeconds(1)), CancellationToken.None);

            using (var protector = new LockProtector(library.Object, context))
            {
            }

            Assert.Equal(State.Done, context.State);
        }

        [Fact]
        public async Task EnsureNoDeadLockAsyncShouldStop()
        {
            var library = new Mock<IScriptLibrary>();
            using var context = new LockState(new Key("", "", TimeSpan.FromSeconds(1)), CancellationToken.None);
            using var protector = new LockProtector(library.Object, context, false);

            context.SetDone();

            await protector.EnsureNoDeadLockAsync();
            Assert.True(true);
        }

        [Fact]
        public async Task EnsureNoDeadLockAsyncShouldStop2()
        {
            var library = new Mock<IScriptLibrary>();
            using var context = new LockState(new Key("", "", TimeSpan.FromSeconds(1)), CancellationToken.None);
            using var protector = new LockProtector(library.Object, context, false);

            context.SetDone(new Exception(""));

            await protector.EnsureNoDeadLockAsync();
            Assert.True(true);
        }

        [Theory]
        //we don't test 0 or 1, as it's basically waiting for ever
        [InlineData(2, State.Done)]
        [InlineData(2, State.WaitingForKey)]
        [InlineData(2, State.WithKey)]
        public async Task CheckSituationIsCorrectlyInterpreted(int value, object state)
        {
            var library = new Mock<IScriptLibrary>();
            using var context = new LockState(new Key("", "", TimeSpan.FromMilliseconds(100)), CancellationToken.None);
            switch (state)
            {
                case State.WithKey:
                    context.SetWithKey();
                    break;
                case State.Done:
                    context.SetDone();
                    break;
            }
            using var protector = new LockProtector(library.Object, context, false);
            library.Setup(l => l.GetLockOrAddToQueue(It.IsAny<LockLuaParameters>())).ReturnsAsync(() => true);
            library.Setup(l => l.GetKeySituation(It.IsAny<LockLuaParameters>())).ReturnsAsync(() => value);

            await protector.EnsureNoDeadLockAsync();
            switch (value)
            {
                case 2:
                    switch (context.State)
                    {
                        case State.WaitingForKey:
                            Assert.Equal(State.WithKey, context.State);
                            break;
                        case State.WithKey:
                        case State.Done:
                            Assert.Equal(State.Done, context.State);
                            break;
                    }
                    break;
            }
        }

        [Fact]
        public async Task CheckSituationIsCorrectlyInterpretedFailCase1()
        {
            var library = new Mock<IScriptLibrary>();
            using var context = new LockState(new Key("", "", TimeSpan.FromSeconds(1)), CancellationToken.None);
            using var protector = new LockProtector(library.Object, context, false);
            library.Setup(l => l.GetKeySituation(It.IsAny<LockLuaParameters>())).ReturnsAsync(() => throw new ApplicationException(""));

            await protector.EnsureNoDeadLockAsync();

            Assert.NotNull(context.WaitingTask.Exception?.InnerException);
            Assert.IsType<ApplicationException>(context.WaitingTask.Exception?.InnerException);
        }

        [Fact]
        public async Task CheckSituationIsCorrectlyInterpretedFailCase2()
        {
            var library = new Mock<IScriptLibrary>();
            using var context = new LockState(new Key("", "", TimeSpan.FromSeconds(1)), CancellationToken.None);
            using var protector = new LockProtector(library.Object, context, false);
            library.Setup(l => l.GetKeySituation(It.IsAny<LockLuaParameters>())).ReturnsAsync(() => 4);

            await protector.EnsureNoDeadLockAsync();

            Assert.NotNull(context.WaitingTask.Exception?.InnerException);
            Assert.IsType<NotImplementedException>(context.WaitingTask.Exception?.InnerException);
        }
    }
}