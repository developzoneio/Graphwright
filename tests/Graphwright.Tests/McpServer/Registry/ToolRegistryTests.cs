using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.McpServer.Contracts;
using Graphwright.McpServer.Registry;
using Graphwright.McpServer.Tools;
using Xunit;

namespace Graphwright.Tests.McpServer.Registry;

public class ToolRegistryTests
{
    private sealed class FakeTool : IGitnexusTool
    {
        private static readonly JsonElement _cachedInputSchema = JsonDocument.Parse("{}").RootElement.Clone();

        public FakeTool(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string Description => "A fake tool used to probe ToolRegistry.AssertContract.";

        public JsonElement InputSchema => _cachedInputSchema;

        public Task<ToolSuccessEnvelope<JsonElement>> ExecuteAsync(JsonElement arguments, CancellationToken ct)
        {
            using var payloadDocument = JsonDocument.Parse("{}");
            var envelope = new ToolSuccessEnvelope<JsonElement>(payloadDocument.RootElement.Clone());
            return Task.FromResult(envelope);
        }
    }

    // Deliberately out of frozen order, to prove Tools re-orders deterministically rather
    // than echoing injection order.
    private static IGitnexusTool[] RealStubToolsOutOfOrder()
    {
        return new IGitnexusTool[]
        {
            new SearchTool(),
            new GetCallGraphTool(),
            new ListSymbolsTool(),
            new FindReferencesTool(),
            new GetFileTool()
        };
    }

    [Fact]
    public void AssertContractDoesNotThrowForTheFiveRealStubTools()
    {
        var registry = new ToolRegistry(RealStubToolsOutOfOrder());

        var exception = Record.Exception(() => registry.AssertContract());

        Assert.Null(exception);
    }

    [Fact]
    public void ToolsExposesTheFiveRealStubToolsInFrozenOrderRegardlessOfInjectionOrder()
    {
        var registry = new ToolRegistry(RealStubToolsOutOfOrder());

        var actualNames = registry.Tools.Select(tool => tool.Name).ToArray();

        Assert.Equal(GitnexusToolNames.FrozenOrderedNames, actualNames);
    }

    [Fact]
    public void AssertContractThrowsToolContractViolationExceptionNamingTheMissingToolWhenOneIsRemoved()
    {
        var tools = RealStubToolsOutOfOrder().Where(tool => tool.Name != GitnexusToolNames.SEARCH).ToArray();
        var registry = new ToolRegistry(tools);

        var exception = Assert.Throws<ToolContractViolationException>(() => registry.AssertContract());

        Assert.Contains(GitnexusToolNames.SEARCH, exception.MissingNames);
        Assert.Empty(exception.ExtraNames);
    }

    [Fact]
    public void AssertContractThrowsToolContractViolationExceptionNamingTheExtraToolWhenASixthIsAdded()
    {
        var tools = RealStubToolsOutOfOrder()
            .Append((IGitnexusTool)new FakeTool("mcp__gitnexus__extra_tool"))
            .ToArray();
        var registry = new ToolRegistry(tools);

        var exception = Assert.Throws<ToolContractViolationException>(() => registry.AssertContract());

        Assert.Contains("mcp__gitnexus__extra_tool", exception.ExtraNames);
        Assert.Empty(exception.MissingNames);
    }

    [Fact]
    public void AssertContractThrowsToolContractViolationExceptionWhenAToolIsRenamed()
    {
        var tools = RealStubToolsOutOfOrder()
            .Where(tool => tool.Name != GitnexusToolNames.SEARCH)
            .Append((IGitnexusTool)new FakeTool("mcp__gitnexus__search_renamed"))
            .ToArray();
        var registry = new ToolRegistry(tools);

        var exception = Assert.Throws<ToolContractViolationException>(() => registry.AssertContract());

        Assert.Contains(GitnexusToolNames.SEARCH, exception.MissingNames);
        Assert.Contains("mcp__gitnexus__search_renamed", exception.ExtraNames);
    }

    [Fact]
    public void AssertContractThrowsToolContractViolationExceptionWhenAToolNameIsOutsideTheFrozenPrefix()
    {
        var tools = RealStubToolsOutOfOrder()
            .Where(tool => tool.Name != GitnexusToolNames.SEARCH)
            .Append((IGitnexusTool)new FakeTool("search"))
            .ToArray();
        var registry = new ToolRegistry(tools);

        var exception = Assert.Throws<ToolContractViolationException>(() => registry.AssertContract());

        Assert.Contains("search", exception.ExtraNames);
        Assert.Contains(GitnexusToolNames.SEARCH, exception.MissingNames);
    }

    [Fact]
    public void ExceptionMessageListsTheExpectedAndActualNameSets()
    {
        var tools = RealStubToolsOutOfOrder().Where(tool => tool.Name != GitnexusToolNames.SEARCH).ToArray();
        var registry = new ToolRegistry(tools);

        var exception = Assert.Throws<ToolContractViolationException>(() => registry.AssertContract());

        foreach (var expectedName in GitnexusToolNames.FrozenOrderedNames)
        {
            Assert.Contains(expectedName, exception.Message, StringComparison.Ordinal);
        }

        foreach (var actualName in tools.Select(tool => tool.Name))
        {
            Assert.Contains(actualName, exception.Message, StringComparison.Ordinal);
        }

        Assert.Contains("Missing:", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Extra:", exception.Message, StringComparison.Ordinal);
    }
}
