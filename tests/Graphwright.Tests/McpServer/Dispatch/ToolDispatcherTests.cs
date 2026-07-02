using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.McpServer.Contracts;
using Graphwright.McpServer.Dispatch;
using Graphwright.McpServer.Tools;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Graphwright.Tests.McpServer.Dispatch;

public class ToolDispatcherTests
{
    private const string UNKNOWN_TOOL_NAME = "mcp__gitnexus__does_not_exist";

    private sealed class FakeSuccessTool : IGitnexusTool
    {
        private static readonly JsonElement _cachedInputSchema = JsonDocument.Parse("{}").RootElement.Clone();

        public string Name => "mcp__gitnexus__fake_success";

        public string Description => "A fake tool that always succeeds.";

        public JsonElement InputSchema => _cachedInputSchema;

        public Task<ToolSuccessEnvelope<JsonElement>> ExecuteAsync(JsonElement arguments, CancellationToken ct)
        {
            using var payloadDocument = JsonDocument.Parse("""{"probe":"ok"}""");
            var envelope = new ToolSuccessEnvelope<JsonElement>(payloadDocument.RootElement.Clone());
            return Task.FromResult(envelope);
        }
    }

    private sealed class FakeThrowingTool : IGitnexusTool
    {
        private static readonly JsonElement _cachedInputSchema = JsonDocument.Parse("{}").RootElement.Clone();

        public string Name => "mcp__gitnexus__fake_throwing";

        public string Description => "A fake tool that always throws.";

        public JsonElement InputSchema => _cachedInputSchema;

        public Task<ToolSuccessEnvelope<JsonElement>> ExecuteAsync(JsonElement arguments, CancellationToken ct)
        {
            throw new InvalidOperationException("raw internal detail that must never leak");
        }
    }

    private sealed class RecordingLogger : ILogger<ToolDispatcher>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }

    [Fact]
    public async Task DispatchAsyncReturnsOkTrueEnvelopeWithPayloadAndNoErrorForSuccessfulTool()
    {
        var fakeSuccessTool = new FakeSuccessTool();
        var dispatcher = new ToolDispatcher(new[] { (IGitnexusTool)fakeSuccessTool }, new RecordingLogger());
        using var arguments = JsonDocument.Parse("{}");

        var result = await dispatcher.DispatchAsync(fakeSuccessTool.Name, arguments.RootElement, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.NotNull(result.Success);
        Assert.True(result.Success!.Ok);
        Assert.Equal("ok", result.Success.Result.GetProperty("probe").GetString());
    }

    [Fact]
    public async Task DispatchAsyncReturnsInvalidArgumentEnvelopeForStubToolWithBadArguments()
    {
        var stubTool = new ListSymbolsTool();
        var dispatcher = new ToolDispatcher(new[] { (IGitnexusTool)stubTool }, new RecordingLogger());
        using var missingRequiredArgument = JsonDocument.Parse("{}");

        var result = await dispatcher.DispatchAsync(stubTool.Name, missingRequiredArgument.RootElement, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Success);
        Assert.NotNull(result.Error);
        Assert.False(result.Error!.Ok);
        Assert.Equal("INVALID_ARGUMENT", result.Error.Error.Code);
        Assert.False(result.Error.Error.Retryable);
    }

    [Fact]
    public async Task DispatchAsyncReturnsInternalEnvelopeWithNotImplementedMessageForStubToolWithValidArguments()
    {
        var stubTool = new ListSymbolsTool();
        var dispatcher = new ToolDispatcher(new[] { (IGitnexusTool)stubTool }, new RecordingLogger());
        using var validArguments = JsonDocument.Parse("""{"file":"src/Graphwright.Domain/Foo.cs"}""");

        var result = await dispatcher.DispatchAsync(stubTool.Name, validArguments.RootElement, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INTERNAL", result.Error!.Error.Code);
        Assert.Contains("not implemented", result.Error.Error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(stubTool.Name, result.Error.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DispatchAsyncReturnsInternalEnvelopeAndLogsToolNameAndArgumentsWhenToolThrows()
    {
        var fakeThrowingTool = new FakeThrowingTool();
        var logger = new RecordingLogger();
        var dispatcher = new ToolDispatcher(new[] { (IGitnexusTool)fakeThrowingTool }, logger);
        using var arguments = JsonDocument.Parse("""{"probe":"value"}""");

        var result = await dispatcher.DispatchAsync(fakeThrowingTool.Name, arguments.RootElement, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INTERNAL", result.Error!.Error.Code);
        Assert.False(result.Error.Error.Retryable);
        Assert.DoesNotContain("raw internal detail", result.Error.Error.Message, StringComparison.Ordinal);

        var loggedError = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Contains(fakeThrowingTool.Name, loggedError.Message, StringComparison.Ordinal);
        Assert.Contains("probe", loggedError.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(loggedError.Exception);
    }

    [Fact]
    public async Task DispatchAsyncReturnsInvalidArgumentEnvelopeForUnknownToolName()
    {
        var dispatcher = new ToolDispatcher(Array.Empty<IGitnexusTool>(), new RecordingLogger());
        using var arguments = JsonDocument.Parse("{}");

        var result = await dispatcher.DispatchAsync(UNKNOWN_TOOL_NAME, arguments.RootElement, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("INVALID_ARGUMENT", result.Error!.Error.Code);
        Assert.Contains(UNKNOWN_TOOL_NAME, result.Error.Error.Message, StringComparison.Ordinal);
    }
}
