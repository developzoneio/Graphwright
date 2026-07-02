using System.Text.Json;
using Graphwright.McpServer.Contracts;

namespace Graphwright.McpServer.Dispatch;

/// <summary>
/// The outcome of a single <see cref="ToolDispatcher.DispatchAsync"/> call: exactly one of a
/// success envelope (<see cref="ToolSuccessEnvelope{TResult}"/> closed over
/// <see cref="JsonElement"/>) or a failure envelope (<see cref="ToolErrorEnvelope"/>) is ever
/// populated, mirroring the "ok: true xor ok: false" mutual exclusivity CLAUDE.md "Error
/// envelope" requires on the wire. Construction only happens through <see cref="FromSuccess"/>
/// / <see cref="FromError"/>, so a result can never hold both envelopes, or neither.
/// </summary>
public sealed class ToolDispatchResult
{
    public static ToolDispatchResult FromSuccess(ToolSuccessEnvelope<JsonElement> success)
    {
        return new ToolDispatchResult(success, null);
    }

    public static ToolDispatchResult FromError(ToolErrorEnvelope error)
    {
        return new ToolDispatchResult(null, error);
    }

    /// <summary>
    /// True when this result holds a <see cref="Success"/> envelope; false when it holds an
    /// <see cref="Error"/> envelope.
    /// </summary>
    public bool IsSuccess => Success is not null;

    /// <summary>
    /// The success envelope, present only when <see cref="IsSuccess"/> is true.
    /// </summary>
    public ToolSuccessEnvelope<JsonElement>? Success { get; }

    /// <summary>
    /// The error envelope, present only when <see cref="IsSuccess"/> is false.
    /// </summary>
    public ToolErrorEnvelope? Error { get; }

    /// <summary>
    /// Serializes whichever envelope this result holds to a <see cref="JsonElement"/>, so the
    /// transport layer never needs to branch on <see cref="IsSuccess"/> before sending a
    /// response on the wire.
    /// </summary>
    public JsonElement SerializeToJsonElement()
    {
        var json = IsSuccess ? JsonSerializer.Serialize(Success) : JsonSerializer.Serialize(Error);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private ToolDispatchResult(ToolSuccessEnvelope<JsonElement>? success, ToolErrorEnvelope? error)
    {
        Success = success;
        Error = error;
    }
}
