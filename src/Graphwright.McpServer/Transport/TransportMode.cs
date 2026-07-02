namespace Graphwright.McpServer.Transport;

/// <summary>
/// The transport the server listens on, selected via the <c>--transport</c> command-line flag.
/// Both modes serve the identical 5-tool surface and envelope contract (00-spec.md Scenario 4:
/// "transport selection does not alter the tool surface or envelope contract").
/// </summary>
public enum TransportMode
{
    /// <summary>
    /// Standard input/output framing. First-class and the default (plan OQ-2) — it is how the
    /// 3 Specwright agents and the availability probe connect.
    /// </summary>
    Stdio,

    /// <summary>
    /// HTTP/Server-Sent-Events framing. Registered and reachable; deeper hardening deferred
    /// (plan OQ-2).
    /// </summary>
    Sse
}
