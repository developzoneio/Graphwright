using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Infrastructure.DependencyInjection;
using Graphwright.McpServer.DependencyInjection;
using Graphwright.McpServer.Tools;
using Graphwright.McpServer.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Graphwright.Tests.McpServer.Transport;

/// <summary>
/// End-to-end handshake test for the SSE/HTTP transport (00-spec.md Scenario 4: "Given the
/// server is started in SSE mode / When a client completes the MCP handshake and lists tools /
/// Then it sees the same 5 tools and the same envelope shapes"). Assertions are the literal
/// mirror of <see cref="StdioIntegrationTests"/> (T16) so parity between the two transport
/// branches is provable rather than assumed, per the plan's "kept minimal (handshake + list
/// tools + one call)" guidance (01-plan.md Risks).
/// </summary>
/// <remarks>
/// Hosting approach: <c>WebApplication.CreateBuilder</c> + <c>builder.WebHost.UseTestServer()</c>,
/// configured exactly as Program.cs's <c>RunSseAsync</c> branch (<see cref="InfrastructureModule"/>
/// and <see cref="McpServerModule"/> registration, then <see cref="McpHandlerAdapter"/> over
/// <c>WithHttpTransport()</c>, then <c>MapMcp()</c>). The SDK's HTTP client transport
/// (<see cref="SseClientTransport"/>) has a constructor overload that accepts an injected
/// <see cref="System.Net.Http.HttpClient"/>, so the client side connects through
/// <c>TestServer</c>'s in-memory client
/// (<see cref="Microsoft.AspNetCore.TestHost.HostBuilderTestServerExtensions.GetTestClient"/>)
/// rather than a real socket.
/// </remarks>
public class SseParityTests
{
    private static readonly TimeSpan _testTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task HandshakeCompletesAndListToolsReturnsExactlyTheFiveFrozenNamesWithSchemas()
    {
        using var cts = new CancellationTokenSource(_testTimeout);
        await using var connectedServer = await StartConnectedServerAsync(cts.Token);

        var tools = await connectedServer.Client.ListToolsAsync(cancellationToken: cts.Token);

        Assert.Equal(GitnexusToolNames.FrozenOrderedNames, tools.Select(tool => tool.Name).ToArray());
        Assert.All(tools, tool => Assert.NotEqual(JsonValueKind.Undefined, tool.ProtocolTool.InputSchema.ValueKind));
    }

    [Fact]
    public async Task CallToolWithValidArgumentsReturnsOkFalseInternalNotImplementedEnvelope()
    {
        using var cts = new CancellationTokenSource(_testTimeout);
        await using var connectedServer = await StartConnectedServerAsync(cts.Token);
        var arguments = new Dictionary<string, object?> { ["query"] = "Foo" };

        var result = await connectedServer.Client.CallToolAsync(GitnexusToolNames.SEARCH, arguments, cancellationToken: cts.Token);

        var envelope = ParseSingleTextContentAsJson(result);
        Assert.False(envelope.GetProperty("ok").GetBoolean());
        Assert.Equal("INTERNAL", envelope.GetProperty("error").GetProperty("code").GetString());
        Assert.False(envelope.GetProperty("error").GetProperty("retryable").GetBoolean());
        Assert.Contains(
            "not implemented",
            envelope.GetProperty("error").GetProperty("message").GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Wires a <see cref="WebApplication"/> configured exactly as Program.cs's
    /// <c>RunSseAsync</c> (modules + the SDK handler adapter + <c>WithHttpTransport()</c> +
    /// <c>MapMcp()</c>) over <see cref="TestServer"/>, starts it, and connects an
    /// <see cref="IMcpClient"/> through <see cref="TestServer"/>'s in-memory
    /// <see cref="System.Net.Http.HttpClient"/>. The returned <see cref="ConnectedServer"/>
    /// disposes both the client and the app together.
    /// </summary>
    private static async Task<ConnectedServer> StartConnectedServerAsync(CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        InfrastructureModule.Instance.RegisterServices(builder.Services);
        McpServerModule.Instance.RegisterServices(builder.Services);
        McpHandlerAdapter.Instance.Configure(builder.Services.AddMcpServer()).WithHttpTransport();

        var app = builder.Build();
        app.MapMcp();

        await app.StartAsync(cancellationToken);

        var httpClient = app.GetTestClient();
        var clientTransport = new SseClientTransport(
            new SseClientTransportOptions { Endpoint = httpClient.BaseAddress! },
            httpClient,
            loggerFactory: null,
            ownsHttpClient: true);
        var client = await McpClientFactory.CreateAsync(clientTransport, cancellationToken: cancellationToken);

        return new ConnectedServer(app, client);
    }

    private static JsonElement ParseSingleTextContentAsJson(CallToolResult result)
    {
        var textBlock = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        using var document = JsonDocument.Parse(textBlock.Text);
        return document.RootElement.Clone();
    }

    private sealed class ConnectedServer : IAsyncDisposable
    {
        private readonly WebApplication _app;

        public ConnectedServer(WebApplication app, IMcpClient client)
        {
            _app = app;
            Client = client;
        }

        public IMcpClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
