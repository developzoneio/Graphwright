namespace Graphwright.Application.LanguageProviders;

/// <summary>
/// The result DTO returned by <see cref="ILanguageProvider.GetFileAsync"/>. <see cref="StartLine"/>
/// and <see cref="EndLine"/> describe the actual range served (post-clamp/post-boundary-expansion),
/// not necessarily the literal requested numbers (00-spec.md "Output").
/// </summary>
public sealed class FileContentResult
{
    public FileContentResult(string file, int startLine, int endLine, int totalLines, string content)
    {
        File = file;
        StartLine = startLine;
        EndLine = endLine;
        TotalLines = totalLines;
        Content = content;
    }

    /// <summary>
    /// Relative, forward-slash path from the project root, echoing the validated input path
    /// (CLAUDE.md cross-cutting rule 2).
    /// </summary>
    public string File { get; }

    /// <summary>
    /// The 1-based inclusive start line actually served.
    /// </summary>
    public int StartLine { get; }

    /// <summary>
    /// The 1-based inclusive end line actually served.
    /// </summary>
    public int EndLine { get; }

    /// <summary>
    /// The file's total line count, computed via <c>SourceText.Lines.Count</c>.
    /// </summary>
    public int TotalLines { get; }

    /// <summary>
    /// The raw source text for <see cref="StartLine"/> through <see cref="EndLine"/> inclusive.
    /// </summary>
    public string Content { get; }
}
