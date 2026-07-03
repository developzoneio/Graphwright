using System;
using System.Linq;
using Graphwright.Application.LanguageProviders;
using Xunit;

namespace Graphwright.Tests.Application.LanguageProviders;

public class DeclaredSymbolKindTests
{
    private static readonly string[] _expectedNames =
    {
        "Namespace",
        "Class",
        "Interface",
        "Struct",
        "Enum",
        "Method",
        "Property",
        "Field",
        "Event",
        "Constructor"
    };

    [Fact]
    public void ContainsExactlyTenMembers()
    {
        var actualNames = Enum.GetNames(typeof(DeclaredSymbolKind));

        Assert.Equal(10, actualNames.Length);
    }

    [Fact]
    public void ContainsExactlyTheClosedSetOfNames()
    {
        var actualNames = Enum.GetNames(typeof(DeclaredSymbolKind)).OrderBy(name => name, StringComparer.Ordinal);
        var expectedNames = _expectedNames.OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(expectedNames, actualNames);
    }
}
