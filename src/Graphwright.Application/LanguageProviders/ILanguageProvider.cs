using System.Threading;
using System.Threading.Tasks;

namespace Graphwright.Application.LanguageProviders;

/// <summary>
/// The Application-layer DIP boundary between the MCP tool handlers and language-specific
/// symbol resolution (CLAUDE.md "Layer map": defined here, implemented in
/// <c>Graphwright.Infrastructure</c> — e.g. <c>DotNetProvider</c> backed by Roslyn). No Roslyn
/// type may appear on this interface or anywhere under <c>Graphwright.Application</c>; only
/// plain DTOs cross the boundary.
/// </summary>
public interface ILanguageProvider
{
    /// <summary>
    /// Enumerates declared symbols matching <paramref name="query"/>. Implementations raise
    /// <see cref="Graphwright.Domain.Exceptions.GraphwrightException"/> subclasses for failure
    /// cases (e.g. workspace not loaded, path not found); this method never returns a
    /// best-effort or partial result on failure.
    /// </summary>
    Task<SymbolListResult> ListSymbolsAsync(ListSymbolsQuery query, CancellationToken ct);
}
