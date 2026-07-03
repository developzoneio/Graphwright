using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Infrastructure.DependencyInjection;
using Graphwright.McpServer.DependencyInjection;
using Graphwright.McpServer.Registry;
using Graphwright.McpServer.Tools;
using Graphwright.McpServer.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Xunit;

namespace Graphwright.Tests.McpServer.Transport;

public class McpHandlerAdapterTests
{
    private const string UNKNOWN_TOOL_NAME = "mcp__gitnexus__does_not_exist";

    // RequestContext<T>'s constructor requires a non-null IMcpServer and copies its Services
    // into the context by default (overridden per-test via the object initializer below); no
    // handler in McpHandlerAdapter touches the server itself, so every other member throws.
    private sealed class UnusedMcpServer : IMcpServer
    {
        public string SessionId => throw new NotSupportedException("Not used by McpHandlerAdapterTests.");

        public ClientCapabilities ClientCapabilities => throw new NotSupportedException("Not used by McpHandlerAdapterTests.");

        public Implementation ClientInfo => throw new NotSupportedException("Not used by McpHandlerAdapterTests.");

        public McpServerOptions ServerOptions => throw new NotSupportedException("Not used by McpHandlerAdapterTests.");

        public IServiceProvider Services { get; } = new ServiceCollection().BuildServiceProvider();

        public LoggingLevel? LoggingLevel => throw new NotSupportedException("Not used by McpHandlerAdapterTests.");

        public Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Not used by McpHandlerAdapterTests.");
        }

        public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Not used by McpHandlerAdapterTests.");
        }

        public IAsyncDisposable RegisterNotificationHandler(
            string method, Func<JsonRpcNotification, CancellationToken, ValueTask> handler)
        {
            throw new NotSupportedException("Not used by McpHandlerAdapterTests.");
        }

        public Task RunAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Not used by McpHandlerAdapterTests.");
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private static ServiceProvider BuildConfiguredProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        InfrastructureModule.Instance.RegisterServices(services);
        McpServerModule.Instance.RegisterServices(services);

        var builder = services.AddMcpServer();
        McpHandlerAdapter.Instance.Configure(builder);

        return services.BuildServiceProvider();
    }

    private static Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>> GetListToolsHandler(
        IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        return options.Capabilities!.Tools!.ListToolsHandler!;
    }

    private static Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>> GetCallToolHandler(
        IServiceProvider provider)
    {
        var options = provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        return options.Capabilities!.Tools!.CallToolHandler!;
    }

    private static RequestContext<ListToolsRequestParams> CreateListToolsContext(IServiceProvider provider)
    {
        return new RequestContext<ListToolsRequestParams>(new UnusedMcpServer())
        {
            Services = provider,
            Params = new ListToolsRequestParams(),
        };
    }

    private static RequestContext<CallToolRequestParams> CreateCallToolContext(
        IServiceProvider provider, string toolName, IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        return new RequestContext<CallToolRequestParams>(new UnusedMcpServer())
        {
            Services = provider,
            Params = new CallToolRequestParams { Name = toolName, Arguments = arguments },
        };
    }

    private static JsonElement ParseSingleTextContentAsJson(CallToolResult result)
    {
        var textBlock = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        using var document = JsonDocument.Parse(textBlock.Text);
        return document.RootElement.Clone();
    }

    [Fact]
    public void InstanceIsTheSameSingletonAcrossCalls()
    {
        Assert.Same(McpHandlerAdapter.Instance, McpHandlerAdapter.Instance);
    }

    [Fact]
    public void ConfigureThrowsArgumentNullExceptionWhenBuilderIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => McpHandlerAdapter.Instance.Configure(null!));
    }

    [Fact]
    public async Task ListToolsHandlerOutputContainsExactlyTheFiveRegistryToolsWithNamesAndInputSchemas()
    {
        using var provider = BuildConfiguredProvider();
        var toolRegistry = provider.GetRequiredService<ToolRegistry>();
        var listToolsHandler = GetListToolsHandler(provider);

        var result = await listToolsHandler(CreateListToolsContext(provider), CancellationToken.None);

        Assert.Equal(GitnexusToolNames.FrozenOrderedNames, result.Tools.Select(tool => tool.Name).ToArray());
        foreach (var tool in result.Tools)
        {
            var expectedTool = toolRegistry.Tools.Single(candidate => candidate.Name == tool.Name);
            Assert.Equal(expectedTool.Description, tool.Description);
            Assert.Equal(expectedTool.InputSchema.GetRawText(), tool.InputSchema.GetRawText());
        }
    }

    [Fact]
    public async Task CallToolHandlerForStubToolReturnsOkFalseInternalNotImplementedEnvelope()
    {
        using var provider = BuildConfiguredProvider();
        var callToolHandler = GetCallToolHandler(provider);
        using var argumentsDocument = JsonDocument.Parse("""{"path":"src/Graphwright.Domain/Foo.cs"}""");
        var arguments = new Dictionary<string, JsonElement>
        {
            ["path"] = argumentsDocument.RootElement.GetProperty("path").Clone(),
        };

        var result = await callToolHandler(
            CreateCallToolContext(provider, GitnexusToolNames.GET_FILE, arguments), CancellationToken.None);

        var envelope = ParseSingleTextContentAsJson(result);
        Assert.False(envelope.GetProperty("ok").GetBoolean());
        Assert.Equal("INTERNAL", envelope.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains(
            "not implemented",
            envelope.GetProperty("error").GetProperty("message").GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CallToolHandlerForUnknownToolNameReturnsInvalidArgumentEnvelopeWithoutThrowing()
    {
        using var provider = BuildConfiguredProvider();
        var callToolHandler = GetCallToolHandler(provider);
        var context = CreateCallToolContext(provider, UNKNOWN_TOOL_NAME, null);

        var exception = await Record.ExceptionAsync(async () => await callToolHandler(context, CancellationToken.None));
        Assert.Null(exception);

        var result = await callToolHandler(context, CancellationToken.None);
        var envelope = ParseSingleTextContentAsJson(result);
        Assert.False(envelope.GetProperty("ok").GetBoolean());
        Assert.Equal("INVALID_ARGUMENT", envelope.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains(UNKNOWN_TOOL_NAME, envelope.GetProperty("error").GetProperty("message").GetString(), StringComparison.Ordinal);
    }
}
