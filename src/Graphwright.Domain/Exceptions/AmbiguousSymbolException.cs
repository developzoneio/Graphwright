using System;
using Graphwright.Domain.Errors;

namespace Graphwright.Domain.Exceptions;

/// <summary>
/// Raised when a requested symbol name resolves to more than one candidate and the tool
/// cannot pick one deterministically. Maps to
/// <see cref="GraphwrightErrorCode.AmbiguousSymbol"/>. Not retryable — the client must
/// narrow the request (e.g. qualify the symbol further).
/// </summary>
public sealed class AmbiguousSymbolException : GraphwrightException
{
    public AmbiguousSymbolException(string symbolName)
        : base($"Symbol is ambiguous: '{symbolName}'.")
    {
        SymbolName = symbolName;
    }

    public AmbiguousSymbolException(string symbolName, Exception innerException)
        : base($"Symbol is ambiguous: '{symbolName}'.", innerException)
    {
        SymbolName = symbolName;
    }

    public override GraphwrightErrorCode Code => GraphwrightErrorCode.AmbiguousSymbol;

    public override bool IsRetryable => false;

    /// <summary>
    /// The symbol name that resolved to more than one candidate.
    /// </summary>
    public string SymbolName { get; }
}
