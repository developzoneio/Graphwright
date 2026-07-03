namespace Graphwright.Application.LanguageProviders;

/// <summary>
/// A single symbol declaration returned by <see cref="ILanguageProvider.ListSymbolsAsync"/>.
/// Plain DTO — no Roslyn types cross this boundary (CLAUDE.md "Layer map" DIP boundary);
/// <see cref="Line"/> is always the 1-based declaration line sourced from
/// <c>Location.GetLineSpan()</c> by the Infrastructure implementation.
/// </summary>
public sealed class DeclaredSymbol
{
    public DeclaredSymbol(
        string name,
        DeclaredSymbolKind kind,
        string file,
        int line,
        string signature,
        string container,
        string accessibility)
    {
        Name = name;
        Kind = kind;
        File = file;
        Line = line;
        Signature = signature;
        Container = container;
        Accessibility = accessibility;
    }

    /// <summary>
    /// The declared symbol's simple name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The declaration kind, from the closed <see cref="DeclaredSymbolKind"/> set.
    /// </summary>
    public DeclaredSymbolKind Kind { get; }

    /// <summary>
    /// Relative, forward-slash path from the project root (CLAUDE.md cross-cutting rule 2).
    /// </summary>
    public string File { get; }

    /// <summary>
    /// The 1-based declaration line (CLAUDE.md cross-cutting rule 1).
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// A short (1-5 line) rendering of the declaration signature.
    /// </summary>
    public string Signature { get; }

    /// <summary>
    /// The name of the containing type or namespace.
    /// </summary>
    public string Container { get; }

    /// <summary>
    /// The declared accessibility (e.g. "public", "private").
    /// </summary>
    public string Accessibility { get; }
}
