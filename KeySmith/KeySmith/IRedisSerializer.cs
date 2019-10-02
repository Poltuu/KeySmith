namespace KeySmith
{
    public interface IRedisSerializer
    {
        string Serialize<T>(T obj);
        T Deserialize<T>(string redisValue);
    }
}
