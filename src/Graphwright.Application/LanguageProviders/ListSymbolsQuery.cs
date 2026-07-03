using System.Collections.Generic;

namespace Graphwright.Application.LanguageProviders;

/// <summary>
/// Immutable query DTO for <see cref="ILanguageProvider.ListSymbolsAsync"/>. Every field is
/// optional except <see cref="MaxResults"/>; the caller (the <c>list_symbols</c> tool handler)
/// is responsible for validating and clamping <see cref="MaxResults"/> into the 1-50 range
/// (CLAUDE.md cross-cutting rule 5) before this query is constructed — the provider does not
/// re-validate it.
/// </summary>
public sealed class ListSymbolsQuery
{
    public ListSymbolsQuery(
        string? path,
        string? nameFilter,
        IReadOnlyList<DeclaredSymbolKind>? kinds,
        bool includeGenerated,
        int maxResults)
    {
        Path = path;
        NameFilter = nameFilter;
        Kinds = kinds;
        IncludeGenerated = includeGenerated;
        MaxResults = maxResults;
    }

    /// <summary>
    /// Relative path to a file or directory to scope the search to; <c>null</c> means the
    /// whole workspace.
    /// </summary>
    public string? Path { get; }

    /// <summary>
    /// Case-insensitive substring or exact-match filter on <see cref="DeclaredSymbol.Name"/>;
    /// <c>null</c> means no name filtering.
    /// </summary>
    public string? NameFilter { get; }

    /// <summary>
    /// The set of declaration kinds to include; <c>null</c> means all kinds.
    /// </summary>
    public IReadOnlyList<DeclaredSymbolKind>? Kinds { get; }

    /// <summary>
    /// Whether generated (<c>*.g.cs</c>) source should be included.
    /// </summary>
    public bool IncludeGenerated { get; }

    /// <summary>
    /// The already-clamped maximum number of results to return, guaranteed by the caller to be
    /// in the 1-50 range.
    /// </summary>
    public int MaxResults { get; }
}
