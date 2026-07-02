using System.Text.Json.Serialization;

namespace Graphwright.McpServer.Contracts;

/// <summary>
/// The "error" object nested inside <see cref="ToolErrorEnvelope"/>. Mirrors CLAUDE.md
/// "Error envelope" exactly: exactly three lowercase wire properties, no more, no less.
/// The <see cref="Code"/> value is already the ALL_UPPER wire string; converting from
/// <see cref="Graphwright.Domain.Errors.GraphwrightErrorCode"/> is the boundary mapper's
/// job, not this DTO's.
/// </summary>
public sealed class ToolError
{
    public ToolError(string code, string message, bool retryable)
    {
        Code = code;
        Message = message;
        Retryable = retryable;
    }

    /// <summary>
    /// The ALL_UPPER_SNAKE error code from the closed six-value set (e.g. "WORKSPACE_NOT_LOADED").
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; }

    /// <summary>
    /// Human-readable failure description. Never a raw stack trace.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; }

    /// <summary>
    /// Whether the client may retry the request that produced this failure.
    /// </summary>
    [JsonPropertyName("retryable")]
    public bool Retryable { get; }
}
