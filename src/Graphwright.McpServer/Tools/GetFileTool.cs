using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Domain.Exceptions;
using Graphwright.McpServer.Contracts;

namespace Graphwright.McpServer.Tools;

/// <summary>
/// Stub for the frozen <c>mcp__gitnexus__get_file</c> tool (CLAUDE.md "MCP tool surface").
/// Will return a file's contents together with its <c>SyntaxTree</c> map; that behavior
/// lands in a later GW-1 story (00-spec.md "Out of scope"). Until then this validates its
/// required argument and reports not-implemented (00-spec.md "What": "accepts its
/// arguments, validates them at the envelope level if cheaply possible, returns a
/// structured not-implemented failure").
/// </summary>
public sealed class GetFileTool : IGitnexusTool
{
    private const string INPUT_SCHEMA_JSON = """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "Relative, forward-slash path to the file to return."
                }
            },
            "required": ["path"]
        }
        """;

    private static readonly JsonElement _cachedInputSchema = JsonDocument.Parse(INPUT_SCHEMA_JSON).RootElement.Clone();

    public string Name => GitnexusToolNames.GET_FILE;

    public string Description => "Returns a file's contents together with its SyntaxTree map.";

    public JsonElement InputSchema => _cachedInputSchema;

    public Task<ToolSuccessEnvelope<JsonElement>> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        if (arguments.TryGetProperty("path", out var pathArgument) == false
            || pathArgument.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(pathArgument.GetString()))
        {
            throw new InvalidToolArgumentException("path", "must be a non-empty string");
        }

        throw new ToolNotImplementedException(Name);
    }
}
