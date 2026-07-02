using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Domain.Exceptions;
using Graphwright.McpServer.Contracts;

namespace Graphwright.McpServer.Tools;

/// <summary>
/// Stub for the frozen <c>mcp__gitnexus__list_symbols</c> tool (CLAUDE.md "MCP tool
/// surface"). Will list the symbols declared in a single source file via Roslyn's
/// <c>SyntaxTree</c> and <c>SemanticModel</c>; that behavior lands in a later GW-1 story
/// (00-spec.md "Out of scope"). Until then this validates its required argument and reports
/// not-implemented (00-spec.md "What": "accepts its arguments, validates them at the
/// envelope level if cheaply possible, returns a structured not-implemented failure").
/// </summary>
public sealed class ListSymbolsTool : IGitnexusTool
{
    private const string INPUT_SCHEMA_JSON = """
        {
            "type": "object",
            "properties": {
                "file": {
                    "type": "string",
                    "description": "Relative, forward-slash path to the file to list symbols for."
                }
            },
            "required": ["file"]
        }
        """;

    private static readonly JsonElement _cachedInputSchema = JsonDocument.Parse(INPUT_SCHEMA_JSON).RootElement.Clone();

    public string Name => GitnexusToolNames.LIST_SYMBOLS;

    public string Description =>
        "Lists the symbols declared in a source file using Roslyn's SyntaxTree and SemanticModel.";

    public JsonElement InputSchema => _cachedInputSchema;

    public Task<ToolSuccessEnvelope<JsonElement>> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        if (arguments.TryGetProperty("file", out var fileArgument) == false
            || fileArgument.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(fileArgument.GetString()))
        {
            throw new InvalidToolArgumentException("file", "must be a non-empty string");
        }

        throw new ToolNotImplementedException(Name);
    }
}
