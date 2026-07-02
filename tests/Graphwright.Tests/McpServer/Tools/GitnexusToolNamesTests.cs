using Graphwright.McpServer.Tools;
using Xunit;

namespace Graphwright.Tests.McpServer.Tools;

public class GitnexusToolNamesTests
{
    private static readonly string[] _expectedOrderedNames =
    {
        "mcp__gitnexus__list_symbols",
        "mcp__gitnexus__get_file",
        "mcp__gitnexus__find_references",
        "mcp__gitnexus__get_call_graph",
        "mcp__gitnexus__search"
    };

    [Fact]
    public void FrozenOrderedNamesContainsExactlyFiveEntries()
    {
        Assert.Equal(5, GitnexusToolNames.FrozenOrderedNames.Count);
    }

    [Fact]
    public void FrozenOrderedNamesMatchesTheFrozenLiteralsInDeterministicOrder()
    {
        Assert.Equal(_expectedOrderedNames, GitnexusToolNames.FrozenOrderedNames);
    }
}
