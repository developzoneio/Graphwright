using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Domain.Exceptions;
using Graphwright.McpServer.Tools;
using Xunit;

namespace Graphwright.Tests.McpServer.Tools;

// Covers the 4 remaining stub tools (ListSymbolsTool has real behavior and is tested separately).
public class StubToolTests
{
    public static IEnumerable<object[]> StubTools()
    {
        yield return new object[] { new GetFileTool(), GitnexusToolNames.GET_FILE, "path" };
        yield return new object[] { new FindReferencesTool(), GitnexusToolNames.FIND_REFERENCES, "symbol" };
        yield return new object[] { new GetCallGraphTool(), GitnexusToolNames.GET_CALL_GRAPH, "symbol" };
        yield return new object[] { new SearchTool(), GitnexusToolNames.SEARCH, "query" };
    }

    public static IEnumerable<object[]> ToolsWithExpectedNames()
    {
        foreach (var row in StubTools())
        {
            yield return new object[] { row[0], row[1] };
        }
    }

    public static IEnumerable<object[]> ToolsWithRequiredArguments()
    {
        foreach (var row in StubTools())
        {
            yield return new object[] { row[0], row[2] };
        }
    }

    [Theory]
    [MemberData(nameof(ToolsWithExpectedNames))]
    public void NameMatchesTheFrozenGitnexusToolNamesConstant(IGitnexusTool tool, string expectedName)
    {
        Assert.Equal(expectedName, tool.Name);
    }

    [Theory]
    [MemberData(nameof(ToolsWithRequiredArguments))]
    public void InputSchemaIsAnObjectSchemaRequiringItsArgument(IGitnexusTool tool, string requiredArgument)
    {
        Assert.Equal(JsonValueKind.Object, tool.InputSchema.ValueKind);
        Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
        Assert.True(tool.InputSchema.GetProperty("properties").TryGetProperty(requiredArgument, out _));

        var required = tool.InputSchema.GetProperty("required");
        var requiredNames = new List<string>();
        foreach (var element in required.EnumerateArray())
        {
            requiredNames.Add(element.GetString() ?? string.Empty);
        }

        Assert.Contains(requiredArgument, requiredNames);
    }

    [Theory]
    [MemberData(nameof(ToolsWithRequiredArguments))]
    public async Task ExecuteAsyncThrowsInvalidToolArgumentExceptionWhenRequiredArgumentIsMissing(
        IGitnexusTool tool, string requiredArgument)
    {
        using var emptyArguments = JsonDocument.Parse("{}");

        var exception = await Assert.ThrowsAsync<InvalidToolArgumentException>(
            () => tool.ExecuteAsync(emptyArguments.RootElement, CancellationToken.None));

        Assert.Equal(requiredArgument, exception.ArgumentName);
    }

    [Theory]
    [MemberData(nameof(ToolsWithRequiredArguments))]
    public async Task ExecuteAsyncThrowsInvalidToolArgumentExceptionWhenRequiredArgumentIsBlank(
        IGitnexusTool tool, string requiredArgument)
    {
        using var blankArguments = JsonDocument.Parse($"{{\"{requiredArgument}\":\"   \"}}");

        var exception = await Assert.ThrowsAsync<InvalidToolArgumentException>(
            () => tool.ExecuteAsync(blankArguments.RootElement, CancellationToken.None));

        Assert.Equal(requiredArgument, exception.ArgumentName);
    }

    [Theory]
    [MemberData(nameof(StubTools))]
    public async Task ExecuteAsyncThrowsToolNotImplementedExceptionNamingTheToolWhenArgumentsAreValid(
        IGitnexusTool tool, string expectedName, string requiredArgument)
    {
        using var validArguments = JsonDocument.Parse($"{{\"{requiredArgument}\":\"value\"}}");

        var exception = await Assert.ThrowsAsync<ToolNotImplementedException>(
            () => tool.ExecuteAsync(validArguments.RootElement, CancellationToken.None));

        Assert.Equal(expectedName, exception.ToolName);
    }
}
