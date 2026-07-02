using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Domain.Exceptions;
using Graphwright.McpServer.Contracts;
using Graphwright.McpServer.Tools;
using Microsoft.Extensions.Logging;

namespace Graphwright.McpServer.Dispatch;

/// <summary>
/// The single choke point where a named tool is looked up and invoked (00-spec.md Scenario 3:
/// "no unhandled exception propagates out of the tool-dispatch boundary"). An unmatched tool
/// name and any exception raised during <see cref="IGitnexusTool.ExecuteAsync"/> are both
/// transformed into a <see cref="ToolDispatchResult"/> via
/// <see cref="ExceptionEnvelopeMapper.Instance"/> — <see cref="DispatchAsync"/> never throws
/// (CLAUDE.md "every failure leaves as the envelope, never as a throw").
/// </summary>
public sealed partial class ToolDispatcher
{
    private readonly IReadOnlyList<IGitnexusTool> _tools;

    private readonly ILogger<ToolDispatcher> _logger;

    public ToolDispatcher(IReadOnlyList<IGitnexusTool> tools, ILogger<ToolDispatcher> logger)
    {
        _tools = tools;
        _logger = logger;
    }

    /// <summary>
    /// Finds the tool named <paramref name="toolName"/> in the injected tool set and invokes
    /// it. An unmatched name is mapped to an <see cref="InvalidToolArgumentException"/>-shaped
    /// error envelope without throwing; any exception thrown during invocation is caught and
    /// mapped by <see cref="InvokeAndMapFailuresAsync"/>, the one call site allowed to catch
    /// <see cref="Exception"/> broadly (coding standard: "catch at smallest scope").
    /// </summary>
    public async Task<ToolDispatchResult> DispatchAsync(string toolName, JsonElement arguments, CancellationToken ct)
    {
        var tool = _tools.FirstOrDefault(candidate => candidate.Name == toolName);
        if (tool is null)
        {
            LogUnknownToolRequested(toolName);

            var unknownToolException = new InvalidToolArgumentException("toolName", $"Unknown tool: '{toolName}'.");
            var unknownToolError = ExceptionEnvelopeMapper.Instance.ToErrorEnvelope(unknownToolException);
            return ToolDispatchResult.FromError(unknownToolError);
        }

        return await InvokeAndMapFailuresAsync(tool, arguments, ct);
    }

    // CA1031 fires on catching System.Exception; this is the one call site the spec requires
    // it at (00-spec.md Scenario 3: "no unhandled exception propagates out of the tool-dispatch
    // boundary"). This also covers OperationCanceledException — in this scaffold, cancellation
    // is a host-driven shutdown signal, and mapping it to INTERNAL via the same catch keeps the
    // boundary contract simple rather than adding a second, uncovered escape path.
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Tool-dispatch boundary must convert every failure to an envelope, never let it propagate (spec Scenario 3).")]
    private async Task<ToolDispatchResult> InvokeAndMapFailuresAsync(
        IGitnexusTool tool, JsonElement arguments, CancellationToken ct)
    {
        try
        {
            var success = await tool.ExecuteAsync(arguments, ct);
            return ToolDispatchResult.FromSuccess(success);
        }
        catch (Exception ex)
        {
            LogToolInvocationFailed(ex, tool.Name, arguments.GetRawText());

            var errorEnvelope = ExceptionEnvelopeMapper.Instance.ToErrorEnvelope(ex);
            return ToolDispatchResult.FromError(errorEnvelope);
        }
    }

    // CA1848: LoggerMessage source-generated delegates instead of the ILogger extension methods.
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Unknown tool requested. ToolName={ToolName}")]
    private partial void LogUnknownToolRequested(string toolName);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Tool invocation failed. ToolName={ToolName}, Arguments={Arguments}")]
    private partial void LogToolInvocationFailed(Exception exception, string toolName, string arguments);
}
