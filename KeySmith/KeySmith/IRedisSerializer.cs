namespace KeySmith
{
    /// <summary>
    /// A class able to serialize redis messages
    /// </summary>
    public interface IRedisSerializer
    {
        /// <summary>
        /// Serializes the object for redis
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        string Serialize<T>(T obj);

        /// <summary>
        /// Deserializes the value from redis
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="redisValue"></param>
        /// <returns></returns>
        T Deserialize<T>(string redisValue);
    }
}
