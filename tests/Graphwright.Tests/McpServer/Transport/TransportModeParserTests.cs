using System;
using Graphwright.Domain.Exceptions;
using Graphwright.McpServer.Transport;
using Xunit;

namespace Graphwright.Tests.McpServer.Transport;

public class TransportModeParserTests
{
    private static readonly string[] _stdioArgs = { "--transport", "stdio" };

    private static readonly string[] _mixedCaseStdioArgs = { "--transport", "StDiO" };

    private static readonly string[] _sseArgs = { "--transport", "sse" };

    private static readonly string[] _unknownValueArgs = { "--transport", "bogus" };

    private static readonly string[] _danglingFlagArgs = { "--transport" };

    [Fact]
    public void ParseReturnsStdioWhenArgsIsEmpty()
    {
        var mode = TransportModeParser.Instance.Parse(Array.Empty<string>());

        Assert.Equal(TransportMode.Stdio, mode);
    }

    [Fact]
    public void ParseReturnsStdioWhenTransportFlagValueIsStdio()
    {
        var mode = TransportModeParser.Instance.Parse(_stdioArgs);

        Assert.Equal(TransportMode.Stdio, mode);
    }

    [Fact]
    public void ParseReturnsStdioWhenTransportFlagValueIsMixedCase()
    {
        var mode = TransportModeParser.Instance.Parse(_mixedCaseStdioArgs);

        Assert.Equal(TransportMode.Stdio, mode);
    }

    [Fact]
    public void ParseReturnsSseWhenTransportFlagValueIsSse()
    {
        var mode = TransportModeParser.Instance.Parse(_sseArgs);

        Assert.Equal(TransportMode.Sse, mode);
    }

    [Fact]
    public void ParseThrowsInvalidToolArgumentExceptionWhenTransportFlagValueIsUnknown()
    {
        var exception = Assert.Throws<InvalidToolArgumentException>(
            () => TransportModeParser.Instance.Parse(_unknownValueArgs));

        Assert.Equal("transport", exception.ArgumentName);
    }

    [Fact]
    public void ParseThrowsInvalidToolArgumentExceptionWhenTransportFlagHasNoValue()
    {
        var exception = Assert.Throws<InvalidToolArgumentException>(
            () => TransportModeParser.Instance.Parse(_danglingFlagArgs));

        Assert.Equal("transport", exception.ArgumentName);
    }

    [Fact]
    public void ParseThrowsArgumentNullExceptionWhenArgsIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => TransportModeParser.Instance.Parse(null!));
    }

    [Fact]
    public void InstanceIsTheSameSingletonAcrossCalls()
    {
        Assert.Same(TransportModeParser.Instance, TransportModeParser.Instance);
    }
}
