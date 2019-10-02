using Newtonsoft.Json;

namespace KeySmith.Tests
{
    public class RedisSerializer : IRedisSerializer
    {
        protected static JsonSerializerSettings _jsonSettings = new RedisJsonSerializerSettings();

        public string Serialize<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, _jsonSettings);
        }

        public T Deserialize<T>(string redisValue)
        {
            return JsonConvert.DeserializeObject<T>(redisValue, _jsonSettings);
        }
    }
}
