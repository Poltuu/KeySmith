using KeySmith.Internals.Scripts;
using KeySmith.Internals.Scripts.Parameters;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;
using Xunit;

namespace KeySmith.Tests
{
    public class MemoScriptsTests
    {        
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("{json:true}")]
        [InlineData(23)]
        [InlineData(new byte[] { 1, 2, 3 })]
        public async Task PublishTests(object value)
        {
            var connection = ConfigurationHelper.GetNewConnection();
            var library = new MemoScriptLibrary(connection);
            var db = connection.GetDatabase();

            var timeout = 500;
            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var key = new MemoKey(root, "key", TimeSpan.FromMilliseconds(timeout), TimeSpan.FromSeconds(1));
            var parameters = new MemoSetValueParameters(key, RedisValue.Unbox(value));

            try
            {
                await ResetKeys(db, parameters);

                var message = RedisValue.Null;
                await connection.GetSubscriber().SubscribeAsync(parameters.MemoChannelKey, (c, v) => message = v) ;
                await library.PublishAsync(parameters);

                await Task.Delay(30);

                var expectedValue = value == null ? RedisValue.EmptyString : RedisValue.Unbox(value);
                Assert.Equal(expectedValue, message);
                Assert.Equal(expectedValue, await db.StringGetAsync(parameters.MemoKey));
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
            var library = new MemoScriptLibrary(connection);
            var db = connection.GetDatabase();

            var timeout = 500;
            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var key = new MemoKey(root, "key", TimeSpan.FromMilliseconds(timeout), TimeSpan.FromSeconds(1));
            var result = false;
            await library.SubscribeAsync(key.GetSubscribtionChannel(), (c, v) => result = true);

            await db.PublishAsync(key.GetValueChannel(), "identifier");
            await Task.Delay(30);

            Assert.True(result);
        }

        [Fact]
        public async Task UnSubscribeTests()
        {
            var connection = ConfigurationHelper.GetNewConnection();
            var library = new MemoScriptLibrary(connection);
            var db = connection.GetDatabase();

            var timeout = 500;
            var root = Guid.NewGuid().ToString().Substring(0, 8);
            var key = new MemoKey(root, "key", TimeSpan.FromMilliseconds(timeout), TimeSpan.FromSeconds(1));
            var result = false;
            void handler(RedisChannel c, RedisValue v) => result = true;
            await library.SubscribeAsync(key.GetSubscribtionChannel(), handler);
            await library.UnsubscribeAsync(key.GetSubscribtionChannel(), handler);

            await db.PublishAsync(key.GetValueChannel(), "identifier");
            await Task.Delay(30);

            Assert.False(result);
        }

        private Task ResetKeys(IDatabase db, MemoSetValueParameters parameters)
            => db.KeyDeleteAsync(new RedisKey[] { parameters.MemoKey, parameters.MemoChannelKey });
    }
}