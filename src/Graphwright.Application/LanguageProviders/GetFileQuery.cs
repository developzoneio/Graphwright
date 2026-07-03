namespace Graphwright.Application.LanguageProviders;

/// <summary>
/// Immutable query DTO for <see cref="ILanguageProvider.GetFileAsync"/>. Unlike
/// <see cref="ListSymbolsQuery.Path"/>, <see cref="Path"/> is required (non-null) here; the
/// caller (the <c>get_file</c> tool handler) is responsible for validating that
/// <see cref="Context"/> is already positive and defaulted to <c>2</c> when omitted, and that
/// the <see cref="StartLine"/>/<see cref="EndLine"/> pair is mutually exclusive with
/// <see cref="AroundLine"/>, before this query is constructed — the provider does not
/// re-validate query structure.
/// </summary>
public sealed class GetFileQuery
{
    public GetFileQuery(string path, int? startLine, int? endLine, int? aroundLine, int context)
    {
        Path = path;
        StartLine = startLine;
        EndLine = endLine;
        AroundLine = aroundLine;
        Context = context;
    }

    /// <summary>
    /// Relative, forward-slash path to the file to read; always required.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The 1-based inclusive start of an explicit range; <c>null</c> unless paired with
    /// <see cref="EndLine"/>.
    /// </summary>
    public int? StartLine { get; }

    /// <summary>
    /// The 1-based inclusive end of an explicit range; <c>null</c> unless paired with
    /// <see cref="StartLine"/>.
    /// </summary>
    public int? EndLine { get; }

    /// <summary>
    /// The 1-based line to center a bounded snippet on; <c>null</c> unless requesting a
    /// bounded-snippet mode. Mutually exclusive with <see cref="StartLine"/>/<see cref="EndLine"/>.
    /// </summary>
    public int? AroundLine { get; }

    /// <summary>
    /// The number of lines to include on each side of <see cref="AroundLine"/>; only meaningful
    /// when <see cref="AroundLine"/> is set.
    /// </summary>
    public int Context { get; }
}
