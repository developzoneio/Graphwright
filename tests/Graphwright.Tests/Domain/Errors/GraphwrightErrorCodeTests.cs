using System;
using System.Linq;
using Graphwright.Domain.Errors;
using Xunit;

namespace Graphwright.Tests.Domain.Errors;

public class GraphwrightErrorCodeTests
{
    private static readonly string[] _expectedNames =
    {
        "WorkspaceNotLoaded",
        "SymbolNotFound",
        "FileNotFound",
        "AmbiguousSymbol",
        "InvalidArgument",
        "Internal"
    };

    [Fact]
    public void ContainsExactlySixMembers()
    {
        var actualNames = Enum.GetNames(typeof(GraphwrightErrorCode));

        Assert.Equal(6, actualNames.Length);
    }

    [Fact]
    public void ContainsExactlyTheClosedSetOfNames()
    {
        var actualNames = Enum.GetNames(typeof(GraphwrightErrorCode)).OrderBy(name => name, StringComparer.Ordinal);
        var expectedNames = _expectedNames.OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(expectedNames, actualNames);
    }
}
