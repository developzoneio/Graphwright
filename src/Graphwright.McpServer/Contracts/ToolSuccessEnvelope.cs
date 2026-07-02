using System.Text.Json.Serialization;

namespace Graphwright.McpServer.Contracts;

/// <summary>
/// The wire envelope for a successful tool invocation: "ok": true plus the tool's own
/// result payload nested under "result". This type never declares an "error" property, so
/// the CLAUDE.md success/error mutual exclusivity rule is enforced by the shape itself
/// rather than by serialization configuration.
/// </summary>
/// <typeparam name="TResult">The shape of the tool-specific success payload.</typeparam>
public sealed class ToolSuccessEnvelope<TResult>
{
    public ToolSuccessEnvelope(TResult result)
    {
        Result = result;
    }

    /// <summary>
    /// Always true on this envelope; distinguishes it from <see cref="ToolErrorEnvelope"/>
    /// on the wire.
    /// </summary>
    [JsonPropertyName("ok")]
    public bool Ok { get; } = true;

    /// <summary>
    /// The tool-specific success payload.
    /// </summary>
    [JsonPropertyName("result")]
    public TResult Result { get; }
}
