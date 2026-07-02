using System;
using Graphwright.Domain.Errors;
using Graphwright.Domain.Exceptions;

namespace Graphwright.McpServer.Contracts;

/// <summary>
/// Maps a caught exception onto the wire-shaped <see cref="ToolErrorEnvelope"/>. This is the
/// only place a <see cref="GraphwrightErrorCode"/> member is converted to its ALL_UPPER wire
/// string (CLAUDE.md "Error envelope"). A known <see cref="GraphwrightException"/> surfaces
/// its own code, message, and retryable flag; any other exception is mapped defensively to
/// "INTERNAL" with a generic message — never the raw exception message or a stack trace
/// (00-spec.md Scenario 3: "unexpected internal failure is mapped to code = INTERNAL").
/// </summary>
public sealed class ExceptionEnvelopeMapper
{
    public static readonly ExceptionEnvelopeMapper Instance = new();

    // Instance (not const) field so ToErrorEnvelope genuinely reads instance state, keeping the
    // no-static-class Instance convention CA1822-clean without a suppression.
    private readonly string _genericInternalMessage = "An unexpected internal error occurred.";

    private ExceptionEnvelopeMapper()
    {
    }

    /// <summary>
    /// Maps <paramref name="ex"/> onto a <see cref="ToolErrorEnvelope"/>. Known
    /// <see cref="GraphwrightException"/> instances surface their own code, message, and
    /// retryable flag; any other exception is mapped defensively to "INTERNAL" with a
    /// generic, non-leaking message.
    /// </summary>
    public ToolErrorEnvelope ToErrorEnvelope(Exception ex)
    {
        if (ex is GraphwrightException graphwrightException)
        {
            var wireCode = ToWireCode(graphwrightException.Code);
            var error = new ToolError(wireCode, graphwrightException.Message, graphwrightException.IsRetryable);
            return new ToolErrorEnvelope(error);
        }

        var internalError = new ToolError(ToWireCode(GraphwrightErrorCode.Internal), _genericInternalMessage, false);
        return new ToolErrorEnvelope(internalError);
    }

    private static string ToWireCode(GraphwrightErrorCode code)
    {
        switch (code)
        {
            case GraphwrightErrorCode.WorkspaceNotLoaded:
                return "WORKSPACE_NOT_LOADED";
            case GraphwrightErrorCode.SymbolNotFound:
                return "SYMBOL_NOT_FOUND";
            case GraphwrightErrorCode.FileNotFound:
                return "FILE_NOT_FOUND";
            case GraphwrightErrorCode.AmbiguousSymbol:
                return "AMBIGUOUS_SYMBOL";
            case GraphwrightErrorCode.InvalidArgument:
                return "INVALID_ARGUMENT";
            case GraphwrightErrorCode.Internal:
                return "INTERNAL";
            default:
                // Defensive: an unmapped enum value must never fall through silently.
                return "INTERNAL";
        }
    }
}
