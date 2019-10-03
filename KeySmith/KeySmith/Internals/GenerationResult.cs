namespace KeySmith.Internals
{
    struct GenerationResult<T>
    {
        public T Result { get; set; }

        public string? ExceptionType { get; set; }
        public string? Message { get; set; }

        public T GetResult()
        {
            if (Message != null)
            {
                throw new GenerationException(ExceptionType ?? "", Message);
            }

            return Result;
        }
    }
}
