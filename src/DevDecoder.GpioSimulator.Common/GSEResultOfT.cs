namespace DevDecoder.GpioSimulator.Common
{
    /// <summary>
    /// Represents the generic result of a GpioSimulatorEngine operation containing a value of type <typeparamref name="T"/>,
    /// or error metadata if the operation failed.
    /// </summary>
    /// <typeparam name="T">The type of the value returned by a successful operation.</typeparam>
    public record GSEResult<T> : GSEResult
    {
        /// <summary>
        /// Gets the value returned by a successful operation, or default if it failed.
        /// </summary>
        public T? Value { get; init; }

        /// <summary>
        /// Creates a successful result containing the specified value.
        /// </summary>
        /// <param name="value">The success value.</param>
        /// <returns>A successful generic operation result.</returns>
        public static new GSEResult<T> OK(T value) => 
            new GSEResult<T> { Success = true, Value = value };
        
        /// <summary>
        /// Creates a failed result with the specified error details.
        /// </summary>
        /// <param name="errorType">The category or type of the error (e.g. Exception name).</param>
        /// <param name="errorMessage">A descriptive error message.</param>
        /// <returns>A failed generic operation result.</returns>
        public static new GSEResult<T> Error(string errorType, string errorMessage) => 
            new GSEResult<T> { Success = false, ErrorType = errorType, ErrorMessage = errorMessage };

        /// <summary>
        /// Implicitly converts a generic result to a boolean indicating whether the operation succeeded.
        /// </summary>
        /// <param name="result">The result instance.</param>
        public static implicit operator bool(GSEResult<T> result) => result.Success;
    }
}
