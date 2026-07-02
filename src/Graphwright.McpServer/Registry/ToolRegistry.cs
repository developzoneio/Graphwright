using System;
using System.Collections.Generic;
using System.Linq;
using Graphwright.McpServer.Tools;

namespace Graphwright.McpServer.Registry;

/// <summary>
/// Holds the tool set the server advertises and self-asserts that it is exactly the frozen
/// 5-tool <c>mcp__gitnexus__*</c> surface (CLAUDE.md "MCP tool surface — Month 1 contract —
/// frozen"; 00-spec.md Scenario 1). Constructed once via constructor injection with the
/// concrete <see cref="IGitnexusTool"/> instances the composition root wires up; the registry
/// itself holds no mutable runtime state.
/// </summary>
public sealed class ToolRegistry
{
    public ToolRegistry(IReadOnlyList<IGitnexusTool> tools)
    {
        Tools = BuildDeterministicOrder(tools);
    }

    /// <summary>
    /// The injected tools re-ordered to match <see cref="GitnexusToolNames.FrozenOrderedNames"/>
    /// table order (00-spec.md Scenario 1 "their names are exactly"). Any tool whose name is
    /// not part of the frozen set is appended afterward in ordinal name order, so
    /// <see cref="AssertContract"/> can still report it as an extra/unrecognized entry.
    /// </summary>
    public IReadOnlyList<IGitnexusTool> Tools { get; }

    /// <summary>
    /// Asserts that <see cref="Tools"/> is exactly the frozen 5-name
    /// <c>mcp__gitnexus__*</c> set: no missing name, no extra name (including wrong-prefix and
    /// renamed tools, which never match a frozen literal), and no duplicate name. Throws
    /// <see cref="ToolContractViolationException"/> on any violation so server startup can
    /// fail fast (00-spec.md Scenario 1: "startup fails fast (non-zero exit, logged reason) if
    /// the set differs in count or name").
    /// </summary>
    public void AssertContract()
    {
        var expectedNames = GitnexusToolNames.FrozenOrderedNames;
        var actualNames = Tools.Select(tool => tool.Name).ToArray();
        var actualNameSet = new HashSet<string>(actualNames, StringComparer.Ordinal);
        var expectedNameSet = new HashSet<string>(expectedNames, StringComparer.Ordinal);

        var missingNames = expectedNames
            .Where(expectedName => actualNameSet.Contains(expectedName) == false)
            .ToArray();

        var extraNames = actualNames
            .Distinct(StringComparer.Ordinal)
            .Where(actualName => expectedNameSet.Contains(actualName) == false)
            .OrderBy(actualName => actualName, StringComparer.Ordinal)
            .ToArray();

        var duplicateNames = actualNames
            .GroupBy(actualName => actualName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(duplicateName => duplicateName, StringComparer.Ordinal)
            .ToArray();

        if (missingNames.Length > 0 || extraNames.Length > 0 || duplicateNames.Length > 0)
        {
            throw new ToolContractViolationException(expectedNames, actualNames, missingNames, extraNames, duplicateNames);
        }
    }

    private static List<IGitnexusTool> BuildDeterministicOrder(IReadOnlyList<IGitnexusTool> tools)
    {
        var frozenOrder = GitnexusToolNames.FrozenOrderedNames;
        var frozenIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < frozenOrder.Count; index++)
        {
            frozenIndexByName[frozenOrder[index]] = index;
        }

        return tools
            .OrderBy(tool => frozenIndexByName.TryGetValue(tool.Name, out var frozenIndex) == true ? frozenIndex : int.MaxValue)
            .ThenBy(tool => tool.Name, StringComparer.Ordinal)
            .ToList();
    }
}
