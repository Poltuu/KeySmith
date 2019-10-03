using StackExchange.Redis;

namespace KeySmith.Tests
{
    public static class ConfigurationHelper
    {
        public static ConnectionMultiplexer GetNewConnection()
        {
            //Environment.GetEnvironmentVariable("CONNECTIONSTRINGS_REDIS") ?? "redis:6379"
            var redisConfig = ConfigurationOptions.Parse("localhost:6379");
            return ConnectionMultiplexer.Connect(redisConfig);
        }
    }
}