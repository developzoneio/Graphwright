using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.McpServer.Dispatch;
using Graphwright.McpServer.Registry;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Graphwright.McpServer.Transport;

/// <summary>
/// Wires the MCP SDK's low-level <c>ListTools</c>/<c>CallTool</c> handlers to
/// <see cref="ToolRegistry"/> and <see cref="ToolDispatcher"/> (plan OQ-3: "we use the
/// low-level handlers", not attribute-based/assembly-scanning discovery, because our frozen
/// <c>mcp__gitnexus__*</c> names must reach the wire as verbatim string constants, and the
/// <c>CallTool</c> handler is the single choke point where the exception-to-envelope mapping
/// happens — 00-spec.md Scenario 3). Both the stdio and SSE transports call <see cref="Configure"/>
/// against the same shared <see cref="IMcpServerBuilder"/> registration, so neither transport
/// can advertise a different tool surface or envelope shape (00-spec.md Scenario 4).
/// </summary>
public sealed class McpHandlerAdapter
{
    public static readonly McpHandlerAdapter Instance = new();

    private McpHandlerAdapter()
    {
    }

    /// <summary>
    /// Wires <c>WithListToolsHandler</c> (fed from the request-scoped <see cref="ToolRegistry"/>)
    /// and <c>WithCallToolHandler</c> (delegating to the request-scoped <see cref="ToolDispatcher"/>)
    /// onto <paramref name="builder"/>. Names, descriptions, and input schemas are copied from
    /// <see cref="ToolRegistry"/> verbatim — CLAUDE.md "advertised names must match the table
    /// verbatim" (MCP tool surface).
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Registrar-style entry point is deliberately an instance member per project " +
            "no-static-class convention (plan D2), mirroring McpServerModule.RegisterServices.")]
    public IMcpServerBuilder Configure(IMcpServerBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .WithListToolsHandler(HandleListToolsAsync)
            .WithCallToolHandler(HandleCallToolAsync);
    }

    /// <summary>
    /// Projects <see cref="ToolRegistry.Tools"/> onto the SDK's <see cref="Tool"/> shape
    /// verbatim. No attribute-based or assembly-scanning discovery is used — the registry is
    /// the single, testable source of truth for what this handler advertises
    /// (00-spec.md Scenario 1).
    /// </summary>
    private static ValueTask<ListToolsResult> HandleListToolsAsync(
        RequestContext<ListToolsRequestParams> context, CancellationToken ct)
    {
        // The hosting IMcpServer always populates RequestContext.Services before invoking a
        // handler (RequestContext copies it from IMcpServer.Services); this null-forgiving
        // assertion documents that invariant rather than adding unreachable defensive code.
        var toolRegistry = context.Services!.GetRequiredService<ToolRegistry>();

        var tools = toolRegistry.Tools
            .Select(tool => new Tool
            {
                Name = tool.Name,
                Description = tool.Description,
                InputSchema = tool.InputSchema,
            })
            .ToList();

        return ValueTask.FromResult(new ListToolsResult { Tools = tools });
    }

    /// <summary>
    /// Delegates every call to <see cref="ToolDispatcher.DispatchAsync"/> — the single choke
    /// point where exceptions are mapped to the error envelope (00-spec.md Scenario 3) — and
    /// serializes whichever envelope comes back as the result's JSON text content. The
    /// envelope is always the content, success or failure, never an MCP protocol-level error,
    /// so callers parse one shape (00-spec.md Scenarios 2-3).
    /// </summary>
    private static async ValueTask<CallToolResult> HandleCallToolAsync(
        RequestContext<CallToolRequestParams> context, CancellationToken ct)
    {
        var toolDispatcher = context.Services!.GetRequiredService<ToolDispatcher>();

        // A CallTool request always carries its params; documented invariant, see the
        // null-forgiving comment on HandleListToolsAsync above for the matching Services case.
        var callParams = context.Params!;
        var arguments = SerializeArguments(callParams.Arguments);

        var dispatchResult = await toolDispatcher.DispatchAsync(callParams.Name, arguments, ct);
        var envelopeJson = dispatchResult.SerializeToJsonElement();

        return new CallToolResult
        {
            Content = new List<ContentBlock> { new TextContentBlock { Text = envelopeJson.GetRawText() } },
        };
    }

    private static JsonElement SerializeArguments(IReadOnlyDictionary<string, JsonElement>? arguments)
    {
        var argumentsJson = arguments is null ? "{}" : JsonSerializer.Serialize(arguments);
        using var document = JsonDocument.Parse(argumentsJson);
        return document.RootElement.Clone();
    }
}
