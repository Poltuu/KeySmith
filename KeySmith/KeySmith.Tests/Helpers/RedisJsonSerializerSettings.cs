using Newtonsoft.Json;

namespace KeySmith.Tests
{
    public class RedisJsonSerializerSettings : JsonSerializerSettings
    {
        public RedisJsonSerializerSettings()
        {
            TypeNameHandling = TypeNameHandling.Auto;
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            PreserveReferencesHandling = PreserveReferencesHandling.Objects;
            ObjectCreationHandling = ObjectCreationHandling.Replace;
        }
    }
}
