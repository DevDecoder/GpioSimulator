using System;

namespace DevDecoder.GpioSimulator.Common
{
    /// <summary>
    /// Represents the result of a GpioSimulatorEngine operation, indicating success or detailing error metadata if the operation failed.
    /// </summary>
    public record GSEResult
    {
        /// <summary>
        /// Gets a value indicating whether the operation succeeded.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>
        /// Gets the type or category of the error (e.g. standard exception name) if the operation failed.
        /// </summary>
        public string? ErrorType { get; init; }

        /// <summary>
        /// Gets the detailed message describing the failure if the operation failed.
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// A pre-allocated successful result.
        /// </summary>
        public static readonly GSEResult OK = new GSEResult { Success = true };
        
        /// <summary>
        /// Creates a failed result with the specified error type and message.
        /// </summary>
        /// <param name="errorType">The standard exception type name corresponding to the failure.</param>
        /// <param name="errorMessage">A descriptive message of why the operation failed.</param>
        /// <returns>A failed GSEResult instance.</returns>
        public static GSEResult Error(string errorType, string errorMessage) => 
            new GSEResult { Success = false, ErrorType = errorType, ErrorMessage = errorMessage };

        /// <summary>
        /// Implicitly converts a GSEResult to a boolean indicating its success status.
        /// </summary>
        /// <param name="result">The result to convert.</param>
        public static implicit operator bool(GSEResult result) => result.Success;

        /// <summary>
        /// Throws the appropriate exception if this result represents a failure, matching the stored error metadata.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown if the error type is ArgumentException.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the error type is InvalidOperationException.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown if the error type is UnauthorizedAccessException.</exception>
        /// <exception cref="Exception">Thrown for any other non-standard error types.</exception>
        public void ThrowIfError()
        {
            if (Success) return;
            if (ErrorType == "ArgumentException") throw new ArgumentException(ErrorMessage);
            if (ErrorType == "InvalidOperationException") throw new InvalidOperationException(ErrorMessage);
            if (ErrorType == "UnauthorizedAccessException") throw new UnauthorizedAccessException(ErrorMessage);
            throw new Exception(ErrorMessage);
        }
    }
}
