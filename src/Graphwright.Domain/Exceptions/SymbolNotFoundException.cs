using System;
using Graphwright.Domain.Errors;

namespace Graphwright.Domain.Exceptions;

/// <summary>
/// Raised when a requested symbol cannot be resolved anywhere in the indexed workspace.
/// Maps to <see cref="GraphwrightErrorCode.SymbolNotFound"/>. Not retryable — the client
/// must change the symbol it asked for.
/// </summary>
public sealed class SymbolNotFoundException : GraphwrightException
{
    public SymbolNotFoundException(string symbolName)
        : base($"Symbol not found: '{symbolName}'.")
    {
        SymbolName = symbolName;
    }

    public SymbolNotFoundException(string symbolName, Exception innerException)
        : base($"Symbol not found: '{symbolName}'.", innerException)
    {
        SymbolName = symbolName;
    }

    public override GraphwrightErrorCode Code => GraphwrightErrorCode.SymbolNotFound;

    public override bool IsRetryable => false;

    /// <summary>
    /// The symbol name that could not be resolved.
    /// </summary>
    public string SymbolName { get; }
}
