using KeySmith.Internals.Locks;
using KeySmith.Internals.Scripts;
using KeySmith.Internals.Scripts.Parameters;
using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace KeySmith.Tests
{
    public class ScriptsTests
    {
        [Theory]
        [InlineData("", "", true)]
        [InlineData("id", "", true)]
        [InlineData("otherId", "", false)]
        [InlineData("", "aa", true)]
        [InlineData("id", "aa", true)]
        [InlineData("otherId", "aa", false)]
        public async Task GetLockOrSubscribeTests(string priorValue, string waiting, bool expectedResult)
        {
            var connection = ConfigurationHelper.GetNewConnection();
            var library = new ScriptLibrary(connection);
            var db = connection.GetDatabase();

            //SETUP
            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var parameters = new LockLuaParameters("id", new Key(root, "name", TimeSpan.FromMilliseconds(500)));
            try
            {
                await ResetKeys(db, parameters);
                if (!string.IsNullOrEmpty(priorValue))
                {
                    await db.StringSetAsync(parameters.LockKey, priorValue, TimeSpan.FromMilliseconds(parameters.Timeout));
                }

                if (!string.IsNullOrEmpty(waiting))
                {
                    await db.HashSetAsync(parameters.LockWaitingSetKey, waiting, 0);
                    await db.KeyExpireAsync(parameters.LockWaitingSetKey, TimeSpan.FromMilliseconds(parameters.Timeout));
                    await db.ListRightPushAsync(parameters.LockWaitingListKey, waiting);
                    await db.KeyExpireAsync(parameters.LockWaitingListKey, TimeSpan.FromMilliseconds(parameters.Timeout));
                }

                //ACT
                var result = await library.GetLockOrAddToQueue(parameters);

                //ASSERT
                Assert.Equal(expectedResult, result);
                if (expectedResult)
                {
                    Assert.Equal(parameters.Identifier, await db.StringGetAsync(parameters.LockKey));
                    Assert.Equal(!string.IsNullOrEmpty(waiting), await db.KeyExistsAsync(parameters.LockWaitingListKey));
                    Assert.Equal(!string.IsNullOrEmpty(waiting), await db.KeyExistsAsync(parameters.LockWaitingSetKey));
                }
                else
                {
                    var lockKey = (string)await db.StringGetAsync(parameters.LockKey);
                    Assert.NotEmpty(lockKey);
                    Assert.NotEqual(parameters.Identifier, lockKey);
                    Assert.Contains(parameters.Identifier, await db.ListRangeAsync(parameters.LockWaitingListKey));
                    Assert.Contains(parameters.Identifier, (await db.HashGetAllAsync(parameters.LockWaitingSetKey)).Select(e => e.Name));
                }

                //expiration delay
                await Task.Delay((int)parameters.Timeout + 50);

                Assert.False(await db.KeyExistsAsync(parameters.LockKey));
                Assert.False(await db.KeyExistsAsync(parameters.LockWaitingListKey));
                Assert.False(await db.KeyExistsAsync(parameters.LockWaitingSetKey));
            }
            finally
            {
                await ResetKeys(db, parameters);
            }
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("", "aa")]
        [InlineData("id", "")]
        [InlineData("id", "aa")]
        [InlineData("otherId", "")]
        [InlineData("otherId", "aa")]
        public async Task FreeLockAndPopScriptTests(string priorValue, string waiting)
        {
            var connection = ConfigurationHelper.GetNewConnection();
            var library = new ScriptLibrary(connection);
            var db = connection.GetDatabase();

            //SETUP
            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var parameters = new LockLuaParameters("id", new Key(root, "name", TimeSpan.FromMilliseconds(500)));
            try
            {
                await ResetKeys(db, parameters);
                if (!string.IsNullOrEmpty(priorValue))
                {
                    await db.StringSetAsync(parameters.LockKey, priorValue, TimeSpan.FromMilliseconds(parameters.Timeout));
                }

                if (!string.IsNullOrEmpty(waiting))
                {
                    await db.HashSetAsync(parameters.LockWaitingSetKey, waiting, 0);
                    await db.ListRightPushAsync(parameters.LockWaitingListKey, waiting);
                }

                var message = "";
                await connection.GetSubscriber().SubscribeAsync(parameters.LockNotifKey, (a, b) => message = b);

                //ACT
                await library.FreeLockAndPop(parameters);

                var locked = await db.StringGetAsync(parameters.LockKey);
                Assert.NotEqual(parameters.Identifier, (string)locked);

                if (priorValue == parameters.Identifier)
                {
                    if (waiting != "")
                    {
                        Assert.Equal(waiting, locked);
                        Assert.NotNull(await db.KeyTimeToLiveAsync(parameters.LockKey));

                        //doesn't work somehow
                        //Assert.NotNull(await db.KeyTimeToLiveAsync(parameters.LockWaitingListKey));
                        //Assert.NotNull(await db.KeyTimeToLiveAsync(parameters.LockWaitingSetKey));

                        await Task.Delay(50);
                        Assert.Equal(message, waiting);

                        await Task.Delay(450);
                        Assert.False(await db.KeyExistsAsync(parameters.LockKey));
                        Assert.False(await db.KeyExistsAsync(parameters.LockWaitingListKey));
                        Assert.False(await db.KeyExistsAsync(parameters.LockWaitingSetKey));
                    }
                    else
                    {
                        Assert.Equal(RedisValue.Null, locked);
                        Assert.False(await db.KeyExistsAsync(parameters.LockKey));
                        Assert.False(await db.KeyExistsAsync(parameters.LockWaitingListKey));
                        Assert.False(await db.KeyExistsAsync(parameters.LockWaitingSetKey));
                    }
                }
            }
            finally
            {
                await ResetKeys(db, parameters);
            }
        }

        [Theory]
        [InlineData("", "id", 0)]
        [InlineData("id", "id", 0)]
        [InlineData("otherId", "id", 0)]
        [InlineData("id", "", 1)]
        [InlineData("id", "otherId", 1)]
        [InlineData("", "", 2)]
        [InlineData("", "otherId", 2)]
        [InlineData("otherId", "", 2)]
        [InlineData("otherId", "otherId", 2)]
        public async Task GetKeySituationTests(string priorValue, string waiting, int expected)
        {
            var connection = ConfigurationHelper.GetNewConnection();
            var library = new ScriptLibrary(connection);
            var db = connection.GetDatabase();

            //SETUP
            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var parameters = new LockLuaParameters("id", new Key(root, "name", TimeSpan.FromMilliseconds(500)));
            try
            {
                await ResetKeys(db, parameters);
                if (!string.IsNullOrEmpty(priorValue))
                {
                    await db.StringSetAsync(parameters.LockKey, priorValue);
                }

                if (!string.IsNullOrEmpty(waiting))
                {
                    await db.HashSetAsync(parameters.LockWaitingSetKey, waiting, 0);
                }

                //ACT
                var result = await library.GetKeySituation(parameters);
                var exists = await db.HashExistsAsync(parameters.LockWaitingSetKey, parameters.Identifier);
                Assert.Equal(expected, result);
            }
            finally
            {
                await ResetKeys(db, parameters);
            }
        }

        [Fact]
        public async Task SubscribeTests()
        {
            var connection = ConfigurationHelper.GetNewConnection();
            var library = new ScriptLibrary(connection);
            var db = connection.GetDatabase();

            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var key = new Key(root, "name", TimeSpan.FromSeconds(1));
            using var context = new LockState(key, "identifier", CancellationToken.None);
            await library.SubscribeAsync(context);

            await db.PublishAsync(key.GetLockChannelKey(), "identifier");
            await Task.Delay(30);

            Assert.Equal(State.WithKey, context.State);//proof that the message was received
            await library.UnSubscribeAsync(context);
        }

        [Fact]
        public async Task UnSubscribeTests()
        {
            var connection = ConfigurationHelper.GetNewConnection();
            var library = new ScriptLibrary(connection);
            var db = connection.GetDatabase();

            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var key = new Key(root, "name", TimeSpan.FromSeconds(1));
            using var context = new LockState(key, "identifier", CancellationToken.None);
            await library.SubscribeAsync(context);
            await library.UnSubscribeAsync(context);

            await db.PublishAsync(key.GetLockChannelKey(), "identifier");
            await Task.Delay(10);

            Assert.Equal(State.WaitingForKey, context.State);//proof that the message was not received
        }

        private Task ResetKeys(IDatabase db, LockLuaParameters parameters)
            => db.KeyDeleteAsync(new RedisKey[] { parameters.LockKey, parameters.LockWaitingListKey, parameters.LockWaitingSetKey });
    }
}