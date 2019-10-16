using System;

namespace KeySmith
{
    /// <summary>
    /// Represents errors from another process during value generation
    /// </summary>
    public sealed class GenerationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GenerationException"/> class
        /// </summary>
        /// <param name="message"></param>
        public GenerationException(string? message)
            : base($"An error has been raised during generation by another process: {message}")
        {
        }
    }
}