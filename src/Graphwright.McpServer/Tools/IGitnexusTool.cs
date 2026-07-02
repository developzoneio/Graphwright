using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.McpServer.Contracts;

namespace Graphwright.McpServer.Tools;

/// <summary>
/// Contract implemented by each of the 5 frozen <c>mcp__gitnexus__*</c> tools (CLAUDE.md
/// "MCP tool surface"). A tool describes itself for registration/discovery via
/// <see cref="Name"/>, <see cref="Description"/>, and <see cref="InputSchema"/>, and executes
/// via <see cref="ExecuteAsync"/>.
///
/// Payload convention: <see cref="ExecuteAsync"/> returns
/// <see cref="ToolSuccessEnvelope{TResult}"/> closed over <see cref="JsonElement"/> — the tool
/// produces its own tool-specific JSON payload, and the dispatch boundary (the single choke
/// point that also owns exception-to-<see cref="ToolErrorEnvelope"/> mapping) serializes it
/// onto the wire. A tool never constructs a <see cref="ToolErrorEnvelope"/> itself; failures
/// are raised as <see cref="Graphwright.Domain.Exceptions.GraphwrightException"/> and mapped
/// at the dispatch boundary.
/// </summary>
public interface IGitnexusTool
{
    /// <summary>
    /// The frozen wire name of this tool, e.g. <c>mcp__gitnexus__list_symbols</c>. Must be one
    /// of the literals in <see cref="GitnexusToolNames"/>.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description advertised to MCP clients during tool discovery.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The JSON Schema describing this tool's accepted <c>arguments</c> shape.
    /// </summary>
    JsonElement InputSchema { get; }

    /// <summary>
    /// Executes the tool against <paramref name="arguments"/>. Returns the tool-specific
    /// success payload wrapped in <see cref="ToolSuccessEnvelope{TResult}"/>; failures are
    /// raised as <see cref="Graphwright.Domain.Exceptions.GraphwrightException"/> for the
    /// dispatch boundary to catch and map, never returned as part of a success envelope.
    /// </summary>
    Task<ToolSuccessEnvelope<JsonElement>> ExecuteAsync(JsonElement arguments, CancellationToken ct);
}
