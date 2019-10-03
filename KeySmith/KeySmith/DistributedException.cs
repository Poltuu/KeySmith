using System;

namespace KeySmith
{
    public sealed class DistributedException : Exception
    {
        public DistributedException(string exceptionType, string message)
            : base($"An distant error of type '{exceptionType}' has been raised during generation: {message}")
        {
        }
    }
}