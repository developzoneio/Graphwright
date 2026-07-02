using System.Collections.Generic;

namespace Graphwright.McpServer.Tools;

/// <summary>
/// The frozen set of 5 <c>mcp__gitnexus__*</c> tool names (CLAUDE.md "MCP tool surface —
/// Month 1 contract — frozen"). These literals are the single in-code source of truth for the
/// tool surface; the double underscores inside the string VALUES are load-bearing wire data,
/// copied verbatim. Do not add, rename, or remove entries without a spec amendment to
/// CLAUDE.md first.
///
/// Member naming note: the fields are ALL_UPPER_CASE, per the repo's <c>constants_all_upper</c>
/// naming rule (.editorconfig), which applies to constants at any accessibility, including
/// public ones. CA1707 ("remove underscores from externally visible member names") is disabled
/// repo-wide in <c>.editorconfig</c> because it conflicts with that rule.
/// </summary>
public sealed class GitnexusToolNames
{
    public const string LIST_SYMBOLS = "mcp__gitnexus__list_symbols";

    public const string GET_FILE = "mcp__gitnexus__get_file";

    public const string FIND_REFERENCES = "mcp__gitnexus__find_references";

    public const string GET_CALL_GRAPH = "mcp__gitnexus__get_call_graph";

    public const string SEARCH = "mcp__gitnexus__search";

    /// <summary>
    /// The 5 frozen tool names in CLAUDE.md "MCP tool surface" table order. Used by the
    /// startup self-assertion to verify the advertised registry is exactly this set.
    /// </summary>
    public static readonly IReadOnlyList<string> FrozenOrderedNames = new[]
    {
        LIST_SYMBOLS,
        GET_FILE,
        FIND_REFERENCES,
        GET_CALL_GRAPH,
        SEARCH
    };

    private GitnexusToolNames()
    {
    }
}
