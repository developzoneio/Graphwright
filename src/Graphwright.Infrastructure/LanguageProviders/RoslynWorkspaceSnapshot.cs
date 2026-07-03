using Microsoft.CodeAnalysis;

namespace Graphwright.Infrastructure.LanguageProviders;

/// <summary>
/// Immutable snapshot of a Roslyn <see cref="Microsoft.CodeAnalysis.Solution"/> plus its
/// load-readiness flag. This is the Infrastructure-internal representation an
/// <c>ILanguageProvider</c> implementation holds and swaps atomically as the workspace loads
/// (CLAUDE.md "Availability probe": the workspace must be warm before <c>list_symbols</c> is
/// served). No Roslyn type may cross out of <c>Graphwright.Infrastructure</c> — this class stays
/// behind the <c>ILanguageProvider</c> DIP boundary and is never referenced from Application or
/// McpServer.
/// </summary>
public sealed class RoslynWorkspaceSnapshot
{
    private RoslynWorkspaceSnapshot(bool isLoaded, Solution solution)
    {
        IsLoaded = isLoaded;
        Solution = solution;
    }

    /// <summary>
    /// Whether a real workspace has finished loading. <c>false</c> for the snapshot returned by
    /// <see cref="NotLoaded"/>.
    /// </summary>
    public bool IsLoaded { get; }

    /// <summary>
    /// The Roslyn solution backing this snapshot. Empty (no projects) when <see cref="IsLoaded"/>
    /// is <c>false</c>.
    /// </summary>
    public Solution Solution { get; }

    /// <summary>
    /// Returns an unloaded snapshot over an empty <see cref="AdhocWorkspace"/> solution. Callers
    /// (e.g. <c>ILanguageProvider</c> implementations before indexing completes) use this to
    /// signal <c>WORKSPACE_NOT_LOADED</c> honestly rather than returning best-effort results.
    /// </summary>
    public static RoslynWorkspaceSnapshot NotLoaded()
    {
        return new RoslynWorkspaceSnapshot(isLoaded: false, new AdhocWorkspace().CurrentSolution);
    }

    /// <summary>
    /// Returns a loaded snapshot over <paramref name="solution"/>. Used by real
    /// <c>ILanguageProvider</c> implementations once the workspace has finished loading, and by
    /// test fixtures that build an in-memory <see cref="Microsoft.CodeAnalysis.Solution"/> for
    /// unit tests.
    /// </summary>
    public static RoslynWorkspaceSnapshot Loaded(Solution solution)
    {
        return new RoslynWorkspaceSnapshot(isLoaded: true, solution);
    }
}
