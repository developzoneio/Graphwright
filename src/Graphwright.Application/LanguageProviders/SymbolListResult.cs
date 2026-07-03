using System.Collections.Generic;

namespace Graphwright.Application.LanguageProviders;

/// <summary>
/// The result DTO returned by <see cref="ILanguageProvider.ListSymbolsAsync"/>. Ordering
/// (file ascending, then line ascending) and the 50-result cap are enforced by the provider
/// implementation before this type is constructed (CLAUDE.md cross-cutting rules 5-6).
/// </summary>
public sealed class SymbolListResult
{
    public SymbolListResult(IReadOnlyList<DeclaredSymbol> results, bool truncated, int totalFound)
    {
        Results = results;
        Truncated = truncated;
        TotalFound = totalFound;
    }

    /// <summary>
    /// The declared symbols matching the query, capped at <see cref="ListSymbolsQuery.MaxResults"/>.
    /// </summary>
    public IReadOnlyList<DeclaredSymbol> Results { get; }

    /// <summary>
    /// Whether <see cref="TotalFound"/> exceeds <see cref="Results"/>'s length, signaling the
    /// cap was applied (CLAUDE.md cross-cutting rule 5).
    /// </summary>
    public bool Truncated { get; }

    /// <summary>
    /// The full count of matching symbols before the cap was applied.
    /// </summary>
    public int TotalFound { get; }
}
