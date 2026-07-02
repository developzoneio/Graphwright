using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Domain.Exceptions;
using Graphwright.McpServer.Contracts;

namespace Graphwright.McpServer.Tools;

/// <summary>
/// Stub for the frozen <c>mcp__gitnexus__get_call_graph</c> tool (CLAUDE.md "MCP tool
/// surface"). Will return the callers of a symbol via Roslyn's
/// <c>SymbolFinder.FindCallersAsync</c>; that behavior lands in a later GW-1 story
/// (00-spec.md "Out of scope"). Until then this validates its required argument and reports
/// not-implemented (00-spec.md "What": "accepts its arguments, validates them at the
/// envelope level if cheaply possible, returns a structured not-implemented failure").
/// </summary>
public sealed class GetCallGraphTool : IGitnexusTool
{
    private const string INPUT_SCHEMA_JSON = """
        {
            "type": "object",
            "properties": {
                "symbol": {
                    "type": "string",
                    "description": "Fully qualified or simple name of the symbol to return callers for."
                }
            },
            "required": ["symbol"]
        }
        """;

    private static readonly JsonElement _cachedInputSchema = JsonDocument.Parse(INPUT_SCHEMA_JSON).RootElement.Clone();

    public string Name => GitnexusToolNames.GET_CALL_GRAPH;

    public string Description => "Returns the callers of a symbol using Roslyn's SymbolFinder.FindCallersAsync.";

    public JsonElement InputSchema => _cachedInputSchema;

    public Task<ToolSuccessEnvelope<JsonElement>> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        if (arguments.TryGetProperty("symbol", out var symbolArgument) == false
            || symbolArgument.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(symbolArgument.GetString()))
        {
            throw new InvalidToolArgumentException("symbol", "must be a non-empty string");
        }

        throw new ToolNotImplementedException(Name);
    }
}
