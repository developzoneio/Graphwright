using System;
using Graphwright.Domain.Errors;

namespace Graphwright.Domain.Exceptions;

/// <summary>
/// Raised by a stub tool that has not yet received its real implementation. The closed
/// error code set has no dedicated "not implemented" value, so this maps to
/// <see cref="GraphwrightErrorCode.Internal"/> and is not retryable — no client action
/// changes the outcome until the tool is implemented.
/// </summary>
public sealed class ToolNotImplementedException : GraphwrightException
{
    public ToolNotImplementedException(string toolName)
        : base($"Tool not implemented: '{toolName}'.")
    {
        ToolName = toolName;
    }

    public ToolNotImplementedException(string toolName, Exception innerException)
        : base($"Tool not implemented: '{toolName}'.", innerException)
    {
        ToolName = toolName;
    }

    public override GraphwrightErrorCode Code => GraphwrightErrorCode.Internal;

    public override bool IsRetryable => false;

    /// <summary>
    /// The name of the tool that has not yet been implemented.
    /// </summary>
    public string ToolName { get; }
}
