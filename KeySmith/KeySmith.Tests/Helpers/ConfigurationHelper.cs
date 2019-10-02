using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System;
using System.IO;

namespace KeySmith.Tests
{
    public static class ConfigurationHelper
    {
        private static ConnectionMultiplexer _connection;
        private static readonly object _lock = new object();

        public static ConnectionMultiplexer GetConnection()
        {
            if (_connection == null)
            {
                lock (_lock)
                {
                    if (_connection == null)
                    {
                        var connectionstring = Environment.GetEnvironmentVariable("CONNECTIONSTRINGS_REDIS") ?? "redis:6379";
                        var redisConfig = new ConfigurationOptions();
                        redisConfig.EndPoints.Add(connectionstring);
                        _connection = ConnectionMultiplexer.Connect(redisConfig);
                    }
                }
            }
            return _connection;
        }
    }
}