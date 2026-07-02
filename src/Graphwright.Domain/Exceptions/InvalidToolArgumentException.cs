using System;
using Graphwright.Domain.Errors;

namespace Graphwright.Domain.Exceptions;

/// <summary>
/// Raised when a tool invocation carries an argument that fails validation at the
/// envelope boundary (missing, malformed, or out of range). Maps to
/// <see cref="GraphwrightErrorCode.InvalidArgument"/>. Not retryable with the same
/// arguments — the client must correct the request.
/// </summary>
public sealed class InvalidToolArgumentException : GraphwrightException
{
    public InvalidToolArgumentException(string argumentName, string reason)
        : base($"Invalid argument '{argumentName}': {reason}")
    {
        ArgumentName = argumentName;
        Reason = reason;
    }

    public InvalidToolArgumentException(string argumentName, string reason, Exception innerException)
        : base($"Invalid argument '{argumentName}': {reason}", innerException)
    {
        ArgumentName = argumentName;
        Reason = reason;
    }

    public override GraphwrightErrorCode Code => GraphwrightErrorCode.InvalidArgument;

    public override bool IsRetryable => false;

    /// <summary>
    /// The name of the tool argument that failed validation.
    /// </summary>
    public string ArgumentName { get; }

    /// <summary>
    /// The reason the argument failed validation.
    /// </summary>
    public string Reason { get; }
}
