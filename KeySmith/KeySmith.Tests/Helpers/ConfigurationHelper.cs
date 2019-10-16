using StackExchange.Redis;
using System;

namespace KeySmith.Tests
{
    public static class ConfigurationHelper
    {
        public static string GetConfiguration()
            => Environment.GetEnvironmentVariable("CONNECTIONSTRINGS_REDIS") ?? "localhost:6379";

        public static ConnectionMultiplexer GetNewConnection()
        {
            var redisConfig = ConfigurationOptions.Parse(GetConfiguration());
            return ConnectionMultiplexer.Connect(redisConfig);
        }
    }
}
