using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Domain.Exceptions;
using Graphwright.McpServer.Contracts;

namespace Graphwright.McpServer.Tools;

/// <summary>
/// Stub for the frozen <c>mcp__gitnexus__search</c> tool (CLAUDE.md "MCP tool surface").
/// Will resolve symbol declarations matching a query via Roslyn's
/// <c>SymbolFinder.FindDeclarationsAsync</c>; that behavior lands in a later GW-1 story
/// (00-spec.md "Out of scope"). Until then this validates its required argument and reports
/// not-implemented (00-spec.md "What": "accepts its arguments, validates them at the
/// envelope level if cheaply possible, returns a structured not-implemented failure").
/// </summary>
public sealed class SearchTool : IGitnexusTool
{
    private const string INPUT_SCHEMA_JSON = """
        {
            "type": "object",
            "properties": {
                "query": {
                    "type": "string",
                    "description": "Symbol name or name fragment to search declarations for."
                }
            },
            "required": ["query"]
        }
        """;

    private static readonly JsonElement _cachedInputSchema = JsonDocument.Parse(INPUT_SCHEMA_JSON).RootElement.Clone();

    public string Name => GitnexusToolNames.SEARCH;

    public string Description =>
        "Resolves symbol declarations matching a query using Roslyn's SymbolFinder.FindDeclarationsAsync.";

    public JsonElement InputSchema => _cachedInputSchema;

    public Task<ToolSuccessEnvelope<JsonElement>> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        if (arguments.TryGetProperty("query", out var queryArgument) == false
            || queryArgument.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(queryArgument.GetString()))
        {
            throw new InvalidToolArgumentException("query", "must be a non-empty string");
        }

        throw new ToolNotImplementedException(Name);
    }
}
