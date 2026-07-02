using System;
using Graphwright.Domain.Errors;

namespace Graphwright.Domain.Exceptions;

/// <summary>
/// Base type for every domain exception that the MCP error envelope mapper understands.
/// Only the sealed types in this namespace derive from it; each one owns exactly one
/// <see cref="GraphwrightErrorCode"/> from the closed six-member enum, so the McpServer
/// boundary can map any caught instance onto the frozen error envelope without a fallback
/// branch for an unknown code.
/// </summary>
public abstract class GraphwrightException : Exception
{
    protected GraphwrightException(string message)
        : base(message)
    {
    }

    protected GraphwrightException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// The closed error code this exception maps to on the MCP error envelope.
    /// </summary>
    public abstract GraphwrightErrorCode Code { get; }

    /// <summary>
    /// Whether the client may retry the request that produced this failure.
    /// </summary>
    public abstract bool IsRetryable { get; }
}
