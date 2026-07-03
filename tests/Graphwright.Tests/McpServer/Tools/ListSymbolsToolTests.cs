using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Application.LanguageProviders;
using Graphwright.Domain.Exceptions;
using Graphwright.McpServer.Tools;
using Xunit;

namespace Graphwright.Tests.McpServer.Tools;

public class ListSymbolsToolTests
{
    private static readonly string[] _expectedSchemaPropertyNames =
        { "path", "name_filter", "kinds", "include_generated", "max_results" };

    private sealed class PoisonLanguageProvider : ILanguageProvider
    {
        public Task<SymbolListResult> ListSymbolsAsync(ListSymbolsQuery query, CancellationToken ct)
        {
            throw new InvalidOperationException("must not be called");
        }

        public Task<FileContentResult> GetFileAsync(GetFileQuery query, CancellationToken ct)
        {
            throw new InvalidOperationException("must not be called");
        }
    }

    private sealed class CapturingLanguageProvider : ILanguageProvider
    {
        private readonly SymbolListResult _result;

        public CapturingLanguageProvider(SymbolListResult result)
        {
            _result = result;
        }

        public ListSymbolsQuery? CapturedQuery { get; private set; }

        public Task<SymbolListResult> ListSymbolsAsync(ListSymbolsQuery query, CancellationToken ct)
        {
            CapturedQuery = query;
            return Task.FromResult(_result);
        }

        public Task<FileContentResult> GetFileAsync(GetFileQuery query, CancellationToken ct)
        {
            throw new NotSupportedException(
                "CapturingLanguageProvider.GetFileAsync is not exercised by get_file tests; this fake is " +
                "list_symbols-specific.");
        }
    }

    private sealed class ThrowingLanguageProvider : ILanguageProvider
    {
        private readonly GraphwrightException _exception;

        public ThrowingLanguageProvider(GraphwrightException exception)
        {
            _exception = exception;
        }

        public Task<SymbolListResult> ListSymbolsAsync(ListSymbolsQuery query, CancellationToken ct)
        {
            throw _exception;
        }

        public Task<FileContentResult> GetFileAsync(GetFileQuery query, CancellationToken ct)
        {
            throw new NotSupportedException(
                "ThrowingLanguageProvider.GetFileAsync is not exercised by get_file tests; this fake is " +
                "list_symbols-specific.");
        }
    }

    private static SymbolListResult EmptyResult()
    {
        return new SymbolListResult(Array.Empty<DeclaredSymbol>(), false, 0);
    }

    public static IEnumerable<object[]> InvalidArgumentCases()
    {
        yield return new object[] { """{"path": 123}""", "path" };
        yield return new object[] { """{"path": true}""", "path" };
        yield return new object[] { """{"name_filter": 123}""", "name_filter" };
        yield return new object[] { """{"name_filter": false}""", "name_filter" };
        yield return new object[] { """{"kinds": "method"}""", "kinds" };
        yield return new object[] { """{"kinds": [123]}""", "kinds" };
        yield return new object[] { """{"kinds": ["Method"]}""", "kinds" };
        yield return new object[] { """{"kinds": ["delegate"]}""", "kinds" };
        yield return new object[] { """{"include_generated": "true"}""", "include_generated" };
        yield return new object[] { """{"include_generated": 1}""", "include_generated" };
        yield return new object[] { """{"max_results": "50"}""", "max_results" };
        yield return new object[] { """{"max_results": 1.5}""", "max_results" };
        yield return new object[] { """{"max_results": 0}""", "max_results" };
        yield return new object[] { """{"max_results": -5}""", "max_results" };
        yield return new object[] { """{"path": "/etc/passwd"}""", "path" };
        yield return new object[] { """{"path": "C:/Windows"}""", "path" };
        yield return new object[] { """{"path": "../secret"}""", "path" };
        yield return new object[] { """{"path": "foo/../secret"}""", "path" };
    }

    [Fact]
    public void ConstructorThrowsArgumentNullExceptionWhenLanguageProviderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ListSymbolsTool(null!));
    }

    [Fact]
    public void InputSchemaIsAnObjectSchemaWithExactlyTheFiveOptionalProperties()
    {
        var tool = new ListSymbolsTool(new PoisonLanguageProvider());

        Assert.Equal(JsonValueKind.Object, tool.InputSchema.ValueKind);
        Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());

        var properties = tool.InputSchema.GetProperty("properties");
        var propertyNames = new List<string>();
        foreach (var property in properties.EnumerateObject())
        {
            propertyNames.Add(property.Name);
        }

        Assert.Equal(_expectedSchemaPropertyNames, propertyNames);

        if (tool.InputSchema.TryGetProperty("required", out var required) == true)
        {
            Assert.Equal(JsonValueKind.Array, required.ValueKind);
            Assert.Empty(required.EnumerateArray());
        }
    }

    [Theory]
    [MemberData(nameof(InvalidArgumentCases))]
    public async Task ExecuteAsyncThrowsInvalidToolArgumentExceptionNamingTheOffendingArgumentAndNeverCallsProvider(
        string argumentsJson, string expectedArgumentName)
    {
        var tool = new ListSymbolsTool(new PoisonLanguageProvider());
        using var arguments = JsonDocument.Parse(argumentsJson);

        var exception = await Assert.ThrowsAsync<InvalidToolArgumentException>(
            () => tool.ExecuteAsync(arguments.RootElement, CancellationToken.None));

        Assert.Equal(expectedArgumentName, exception.ArgumentName);
    }

    [Fact]
    public async Task ExecuteAsyncWithEmptyArgumentsPassesDefaultQueryToProvider()
    {
        var capturingProvider = new CapturingLanguageProvider(EmptyResult());
        var tool = new ListSymbolsTool(capturingProvider);
        using var arguments = JsonDocument.Parse("{}");

        await tool.ExecuteAsync(arguments.RootElement, CancellationToken.None);

        Assert.NotNull(capturingProvider.CapturedQuery);
        Assert.Null(capturingProvider.CapturedQuery!.Path);
        Assert.Null(capturingProvider.CapturedQuery.NameFilter);
        Assert.Null(capturingProvider.CapturedQuery.Kinds);
        Assert.False(capturingProvider.CapturedQuery.IncludeGenerated);
        Assert.Equal(50, capturingProvider.CapturedQuery.MaxResults);
    }

    [Fact]
    public async Task ExecuteAsyncClampsMaxResultsAboveFiftyInsteadOfRejecting()
    {
        var capturingProvider = new CapturingLanguageProvider(EmptyResult());
        var tool = new ListSymbolsTool(capturingProvider);
        using var arguments = JsonDocument.Parse("""{"max_results": 200}""");

        await tool.ExecuteAsync(arguments.RootElement, CancellationToken.None);

        Assert.Equal(50, capturingProvider.CapturedQuery!.MaxResults);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesWorkspaceNotLoadedExceptionUnchanged()
    {
        var expectedException = new WorkspaceNotLoadedException();
        var tool = new ListSymbolsTool(new ThrowingLanguageProvider(expectedException));
        using var arguments = JsonDocument.Parse("{}");

        var exception = await Assert.ThrowsAsync<WorkspaceNotLoadedException>(
            () => tool.ExecuteAsync(arguments.RootElement, CancellationToken.None));

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public async Task ExecuteAsyncPropagatesSourceFileNotFoundExceptionUnchanged()
    {
        var expectedException = new SourceFileNotFoundException("src/Missing.cs");
        var tool = new ListSymbolsTool(new ThrowingLanguageProvider(expectedException));
        using var arguments = JsonDocument.Parse("""{"path": "src/Missing.cs"}""");

        var exception = await Assert.ThrowsAsync<SourceFileNotFoundException>(
            () => tool.ExecuteAsync(arguments.RootElement, CancellationToken.None));

        Assert.Same(expectedException, exception);
    }

    [Fact]
    public async Task ExecuteAsyncMapsCannedResultToTheExactExpectedSuccessJsonShape()
    {
        var results = new[]
        {
            new DeclaredSymbol(
                "Graphwright",
                DeclaredSymbolKind.Namespace,
                "src/Graphwright.Domain/Foo.cs",
                1,
                "namespace Graphwright.Domain",
                string.Empty,
                "public"),
            new DeclaredSymbol(
                "Foo",
                DeclaredSymbolKind.Constructor,
                "src/Graphwright.Domain/Foo.cs",
                10,
                "public Foo()",
                "Foo",
                "public")
        };
        var cannedResult = new SymbolListResult(results, truncated: true, totalFound: 5);
        var tool = new ListSymbolsTool(new CapturingLanguageProvider(cannedResult));
        using var arguments = JsonDocument.Parse("{}");

        var envelope = await tool.ExecuteAsync(arguments.RootElement, CancellationToken.None);

        var expectedJson = JsonNode.Parse("""
            {
                "result": {
                    "results": [
                        {
                            "name": "Graphwright",
                            "kind": "namespace",
                            "file": "src/Graphwright.Domain/Foo.cs",
                            "line": 1,
                            "signature": "namespace Graphwright.Domain",
                            "container": "",
                            "accessibility": "public"
                        },
                        {
                            "name": "Foo",
                            "kind": "constructor",
                            "file": "src/Graphwright.Domain/Foo.cs",
                            "line": 10,
                            "signature": "public Foo()",
                            "container": "Foo",
                            "accessibility": "public"
                        }
                    ],
                    "truncated": true,
                    "total_found": 5
                }
            }
            """);
        var actualJson = JsonNode.Parse($$"""{"result": {{JsonSerializer.Serialize(envelope.Result)}} }""");

        Assert.True(JsonNode.DeepEquals(expectedJson, actualJson));
    }
}
