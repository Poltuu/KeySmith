using StackExchange.Redis;
using System;

namespace KeySmith.Tests
{
    public static class ConfigurationHelper
    {
        public static ConnectionMultiplexer GetNewConnection()
        {
            var redisConfig = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("CONNECTIONSTRINGS_REDIS") ?? "localhost:6379");
            return ConnectionMultiplexer.Connect(redisConfig);
        }
    }
}
