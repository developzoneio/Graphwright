using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Infrastructure.DependencyInjection;
using Graphwright.McpServer.DependencyInjection;
using Graphwright.McpServer.Tools;
using Graphwright.McpServer.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace Graphwright.Tests.McpServer.Transport;

/// <summary>
/// End-to-end handshake test for the stdio transport (00-spec.md Scenario 4: "Given the server
/// is started in stdio mode / When a client completes the MCP handshake and lists tools / Then
/// it sees the same 5 tools and the same envelope shapes").
/// </summary>
/// <remarks>
/// Transport approach: in-memory, not process. The SDK's <c>WithStdioServerTransport()</c>
/// (used by Program.cs) binds real <see cref="Console"/> streams, which cannot be redirected
/// from inside the test process. This test therefore substitutes only the transport plumbing
/// with the SDK's documented in-memory sibling for testing — a pair of <see cref="Pipe"/>s
/// adapted to <see cref="System.IO.Stream"/>, wired via <c>WithStreamServerTransport</c> on the
/// server side and <see cref="StreamClientTransport"/> on the client side. Everything else is
/// configured exactly as Program.cs's stdio branch: <see cref="InfrastructureModule"/> and
/// <see cref="McpServerModule"/> registration followed by <see cref="McpHandlerAdapter"/>
/// configuration (mirrors McpHandlerAdapterTests, T13, which exercises the same handler wiring
/// directly rather than over a transport).
/// </remarks>
public class StdioIntegrationTests
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

    [Fact]
    public async Task CallToolWithMissingQueryReturnsInvalidArgumentEnvelope()
    {
        using var cts = new CancellationTokenSource(_testTimeout);
        await using var connectedServer = await StartConnectedServerAsync(cts.Token);
        var arguments = new Dictionary<string, object?>();

        var result = await connectedServer.Client.CallToolAsync(GitnexusToolNames.SEARCH, arguments, cancellationToken: cts.Token);

        var envelope = ParseSingleTextContentAsJson(result);
        Assert.False(envelope.GetProperty("ok").GetBoolean());
        Assert.Equal("INVALID_ARGUMENT", envelope.GetProperty("error").GetProperty("code").GetString());
    }

    /// <summary>
    /// Wires a host configured exactly as Program.cs's <c>RunStdioAsync</c> (modules + the SDK
    /// handler adapter), substitutes <c>WithStdioServerTransport()</c> for
    /// <c>WithStreamServerTransport</c> over a paired in-memory pipe, starts the host, and
    /// connects an <see cref="IMcpClient"/> to the other end of that pipe pair. The returned
    /// <see cref="ConnectedServer"/> disposes both the client and the host together.
    /// </summary>
    private static async Task<ConnectedServer> StartConnectedServerAsync(CancellationToken cancellationToken)
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();

        InfrastructureModule.Instance.RegisterServices(builder.Services);
        McpServerModule.Instance.RegisterServices(builder.Services);
        McpHandlerAdapter.Instance
            .Configure(builder.Services.AddMcpServer())
            .WithStreamServerTransport(clientToServer.Reader.AsStream(), serverToClient.Writer.AsStream());

        var host = builder.Build();
        await host.StartAsync(cancellationToken);

        var clientTransport = new StreamClientTransport(
            serverInput: clientToServer.Writer.AsStream(),
            serverOutput: serverToClient.Reader.AsStream());
        var client = await McpClientFactory.CreateAsync(clientTransport, cancellationToken: cancellationToken);

        return new ConnectedServer(host, client);
    }

    private static JsonElement ParseSingleTextContentAsJson(CallToolResult result)
    {
        var textBlock = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        using var document = JsonDocument.Parse(textBlock.Text);
        return document.RootElement.Clone();
    }

    private sealed class ConnectedServer : IAsyncDisposable
    {
        private readonly IHost _host;

        public ConnectedServer(IHost host, IMcpClient client)
        {
            _host = host;
            Client = client;
        }

        public IMcpClient Client { get; }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
