using System;

namespace KeySmith.Internals
{
    struct GenerationResult<T>
    {
        public T Result { get; set; }
        public Exception Error { get; set; }

        public T GetResult()
        {
            if (Error != null)
            {
                throw Error;
            }

            return Result;
        }
    }
}
