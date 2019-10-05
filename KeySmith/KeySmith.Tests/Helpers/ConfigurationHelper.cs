using StackExchange.Redis;

namespace KeySmith.Tests
{
    public static class ConfigurationHelper
    {
        public static ConnectionMultiplexer GetNewConnection()
        {
            var redisConfig = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("CONNECTIONSTRINGS_REDIS") ?? "redis:6379");
            return ConnectionMultiplexer.Connect(redisConfig);
        }
    }
}
