namespace KeySmith.Internals
{
    struct GenerationResult<T>
    {
        public T Result { get; set; }

        public string ExceptionType { get; set; }
        public string Message { get; set; }

        public T GetResult()
        {
            if (!string.IsNullOrEmpty(Message))
            {
                throw new DistributedException(ExceptionType, Message);
            }

            return Result;
        }
    }
}
