using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
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
                        var config = new ConfigurationBuilder()
                            .SetBasePath(Path.GetDirectoryName(typeof(ConfigurationHelper).Assembly.Location))
                            .AddJsonFile("appsettings.json")
                            .Build();

                        var redisConfig = new ConfigurationOptions { Password = config["Redis:Password"] };
                        redisConfig.EndPoints.Add(config["Redis:Host"]);
                        _connection = ConnectionMultiplexer.Connect(redisConfig);
                    }
                }
            }
            return _connection;
        }
    }
}
