using System;
using System.Diagnostics.CodeAnalysis;
using Graphwright.Domain.Exceptions;

namespace Graphwright.McpServer.Transport;

/// <summary>
/// Parses the <c>--transport</c> command-line flag into a <see cref="TransportMode"/>. No
/// flag (and no args at all) defaults to <see cref="TransportMode.Stdio"/> (plan OQ-2: "stdio
/// is first-class"). The flag value is matched case-insensitively (`stdio`/`Stdio`/`STDIO` all
/// resolve to <see cref="TransportMode.Stdio"/>) since it is a human-typed CLI argument, not a
/// wire-protocol literal.
/// </summary>
public sealed class TransportModeParser
{
    private const string TRANSPORT_FLAG = "--transport";

    public static readonly TransportModeParser Instance = new();

    private TransportModeParser()
    {
    }

    /// <summary>
    /// Parses <paramref name="args"/> for the <c>--transport</c> flag. Throws
    /// <see cref="InvalidToolArgumentException"/> when the flag is present without a following
    /// value, or when its value is neither <c>stdio</c> nor <c>sse</c>.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Parser is deliberately an instance member per project no-static-class " +
            "convention (plan D2), mirroring McpServerModule.RegisterServices.")]
    public TransportMode Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var flagIndex = Array.IndexOf(args, TRANSPORT_FLAG);
        if (flagIndex < 0)
        {
            return TransportMode.Stdio;
        }

        var valueIndex = flagIndex + 1;
        if (valueIndex >= args.Length)
        {
            throw new InvalidToolArgumentException("transport", $"'{TRANSPORT_FLAG}' requires a value.");
        }

        var value = args[valueIndex];
        return value.ToUpperInvariant() switch
        {
            "STDIO" => TransportMode.Stdio,
            "SSE" => TransportMode.Sse,
            _ => throw new InvalidToolArgumentException(
                "transport", $"Unknown transport '{value}'. Expected 'stdio' or 'sse'.")
        };
    }
}
