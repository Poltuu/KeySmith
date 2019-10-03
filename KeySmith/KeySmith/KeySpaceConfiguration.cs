namespace KeySmith
{
    /// <summary>
    /// This class represents the configuration of redis key space
    /// </summary>
    public class KeySpaceConfiguration
    {
        /// <summary>
        /// Gets or sets the root for all redis keys
        /// </summary>
        public string Root { get; set; } = "";
    }
}
