using System.Text.Json.Serialization;

namespace Graphwright.McpServer.Contracts;

/// <summary>
/// The wire envelope for a failed tool invocation: "ok": false plus a nested "error" object.
/// Mirrors CLAUDE.md "Error envelope (all tools must use this shape)" byte-for-byte. This
/// type never carries a success payload alongside the error.
/// </summary>
public sealed class ToolErrorEnvelope
{
    public ToolErrorEnvelope(ToolError error)
    {
        Error = error;
    }

    /// <summary>
    /// Always false on this envelope; distinguishes it from <see cref="ToolSuccessEnvelope{TResult}"/>
    /// on the wire.
    /// </summary>
    [JsonPropertyName("ok")]
    public bool Ok { get; } = false;

    /// <summary>
    /// The structured failure reason. Always present on this envelope.
    /// </summary>
    [JsonPropertyName("error")]
    public ToolError Error { get; }
}
