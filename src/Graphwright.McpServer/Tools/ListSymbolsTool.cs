using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Application.LanguageProviders;
using Graphwright.Domain.Exceptions;
using Graphwright.McpServer.Contracts;

namespace Graphwright.McpServer.Tools;

/// <summary>
/// The frozen <c>mcp__gitnexus__list_symbols</c> tool (CLAUDE.md "MCP tool surface").
/// Enumerates symbols declared in source, scoped by an optional <c>path</c> (file, directory,
/// or the whole workspace when omitted), filtered by an optional case-insensitive
/// <c>name_filter</c> and/or <c>kinds</c>, with generated code excluded by default
/// (00-spec.md "What"). Every argument — including <c>path</c> traversal/rooted rejection, the
/// closed <c>kinds</c> vocabulary, and <c>max_results</c> range/clamp — is validated before
/// <see cref="ILanguageProvider.ListSymbolsAsync"/> is ever invoked (00-spec.md Scenario 11).
/// </summary>
public sealed class ListSymbolsTool : IGitnexusTool
{
    private const int DEFAULT_MAX_RESULTS = 50;
    private const int HARD_CAP_MAX_RESULTS = 50;

    private const string INPUT_SCHEMA_JSON = """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "Relative, forward-slash path to a file or directory; omitted means whole workspace."
                },
                "name_filter": {
                    "type": "string",
                    "description": "Case-insensitive substring or exact-match filter on the declared symbol name."
                },
                "kinds": {
                    "type": "array",
                    "description": "Symbol-kind literals to include; omitted means all kinds.",
                    "items": {
                        "type": "string",
                        "enum": [
                            "namespace", "class", "interface", "struct", "enum",
                            "method", "property", "field", "event", "constructor"
                        ]
                    }
                },
                "include_generated": {
                    "type": "boolean",
                    "description": "Whether generated (*.g.cs) source is included. Defaults to false."
                },
                "max_results": {
                    "type": "integer",
                    "description": "Maximum results to return. Defaults to 50; values above 50 are clamped to 50."
                }
            }
        }
        """;

    private static readonly JsonElement _cachedInputSchema = JsonDocument.Parse(INPUT_SCHEMA_JSON).RootElement.Clone();

    private readonly ILanguageProvider _languageProvider;

    public ListSymbolsTool(ILanguageProvider languageProvider)
    {
        ArgumentNullException.ThrowIfNull(languageProvider);

        _languageProvider = languageProvider;
    }

    public string Name => GitnexusToolNames.LIST_SYMBOLS;

    public string Description =>
        "Lists symbols declared in source, scoped by an optional path, using Roslyn's SyntaxTree and SemanticModel.";

    public JsonElement InputSchema => _cachedInputSchema;

    public async Task<ToolSuccessEnvelope<JsonElement>> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        var path = ValidateOptionalPath(arguments);
        var nameFilter = ValidateOptionalString(arguments, "name_filter");
        var kinds = ValidateOptionalKinds(arguments);
        var includeGenerated = ValidateOptionalBoolean(arguments, "include_generated");
        var maxResults = ValidateOptionalMaxResults(arguments);

        var query = new ListSymbolsQuery(path, nameFilter, kinds, includeGenerated, maxResults);
        var result = await _languageProvider.ListSymbolsAsync(query, ct);

        return ToEnvelope(result);
    }

    private static string? ValidateOptionalPath(JsonElement arguments)
    {
        var path = ValidateOptionalString(arguments, "path");
        if (path == null)
        {
            return null;
        }

        if (Path.IsPathRooted(path) == true)
        {
            throw new InvalidToolArgumentException("path", "must be a relative path, not rooted/absolute");
        }

        var segments = path.Split('/', '\\');
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                throw new InvalidToolArgumentException("path", "must not contain a '..' segment");
            }
        }

        return path;
    }

    private static string? ValidateOptionalString(JsonElement arguments, string argumentName)
    {
        if (arguments.TryGetProperty(argumentName, out var argument) == false)
        {
            return null;
        }

        if (argument.ValueKind != JsonValueKind.String)
        {
            throw new InvalidToolArgumentException(argumentName, "must be a string");
        }

        return argument.GetString();
    }

    private static List<DeclaredSymbolKind>? ValidateOptionalKinds(JsonElement arguments)
    {
        if (arguments.TryGetProperty("kinds", out var kindsArgument) == false)
        {
            return null;
        }

        if (kindsArgument.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidToolArgumentException("kinds", "must be an array of symbol-kind strings");
        }

        var kinds = new List<DeclaredSymbolKind>();
        foreach (var kindElement in kindsArgument.EnumerateArray())
        {
            if (kindElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidToolArgumentException("kinds", "each element must be a string");
            }

            var literal = kindElement.GetString();
            if (SymbolKindWireMapper.Instance.TryFromWireLiteral(literal, out var kind) == false)
            {
                throw new InvalidToolArgumentException("kinds", $"unrecognized symbol-kind literal '{literal}'");
            }

            kinds.Add(kind);
        }

        return kinds;
    }

    private static bool ValidateOptionalBoolean(JsonElement arguments, string argumentName)
    {
        if (arguments.TryGetProperty(argumentName, out var argument) == false)
        {
            return false;
        }

        if (argument.ValueKind != JsonValueKind.True && argument.ValueKind != JsonValueKind.False)
        {
            throw new InvalidToolArgumentException(argumentName, "must be a boolean");
        }

        return argument.GetBoolean();
    }

    private static int ValidateOptionalMaxResults(JsonElement arguments)
    {
        if (arguments.TryGetProperty("max_results", out var argument) == false)
        {
            return DEFAULT_MAX_RESULTS;
        }

        if (argument.ValueKind != JsonValueKind.Number || argument.TryGetInt32(out var maxResults) == false)
        {
            throw new InvalidToolArgumentException("max_results", "must be an integer");
        }

        if (maxResults <= 0)
        {
            throw new InvalidToolArgumentException("max_results", "must be greater than zero");
        }

        return Math.Min(maxResults, HARD_CAP_MAX_RESULTS);
    }

    private static ToolSuccessEnvelope<JsonElement> ToEnvelope(SymbolListResult result)
    {
        var payload = new ListSymbolsResultPayload(ToPayloads(result.Results), result.Truncated, result.TotalFound);
        var payloadElement = JsonSerializer.SerializeToElement(payload);

        return new ToolSuccessEnvelope<JsonElement>(payloadElement);
    }

    private static List<DeclaredSymbolPayload> ToPayloads(IReadOnlyList<DeclaredSymbol> symbols)
    {
        var payloads = new List<DeclaredSymbolPayload>(symbols.Count);
        foreach (var symbol in symbols)
        {
            payloads.Add(new DeclaredSymbolPayload(
                symbol.Name,
                SymbolKindWireMapper.Instance.ToWireLiteral(symbol.Kind),
                symbol.File,
                symbol.Line,
                symbol.Signature,
                symbol.Container,
                symbol.Accessibility));
        }

        return payloads;
    }

    /// <summary>
    /// The tool-owned success payload shape: <c>results / truncated / total_found</c>
    /// (00-spec.md "Output").
    /// </summary>
    private sealed class ListSymbolsResultPayload
    {
        public ListSymbolsResultPayload(IReadOnlyList<DeclaredSymbolPayload> results, bool truncated, int totalFound)
        {
            Results = results;
            Truncated = truncated;
            TotalFound = totalFound;
        }

        [JsonPropertyName("results")]
        public IReadOnlyList<DeclaredSymbolPayload> Results { get; }

        [JsonPropertyName("truncated")]
        public bool Truncated { get; }

        [JsonPropertyName("total_found")]
        public int TotalFound { get; }
    }

    /// <summary>
    /// The wire shape of a single declared symbol (00-spec.md "Output").
    /// </summary>
    private sealed class DeclaredSymbolPayload
    {
        public DeclaredSymbolPayload(
            string name, string kind, string file, int line, string signature, string container, string accessibility)
        {
            Name = name;
            Kind = kind;
            File = file;
            Line = line;
            Signature = signature;
            Container = container;
            Accessibility = accessibility;
        }

        [JsonPropertyName("name")]
        public string Name { get; }

        [JsonPropertyName("kind")]
        public string Kind { get; }

        [JsonPropertyName("file")]
        public string File { get; }

        [JsonPropertyName("line")]
        public int Line { get; }

        [JsonPropertyName("signature")]
        public string Signature { get; }

        [JsonPropertyName("container")]
        public string Container { get; }

        [JsonPropertyName("accessibility")]
        public string Accessibility { get; }
    }
}
