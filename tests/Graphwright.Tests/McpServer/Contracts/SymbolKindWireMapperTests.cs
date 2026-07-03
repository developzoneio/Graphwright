using System;
using Graphwright.Application.LanguageProviders;
using Graphwright.McpServer.Contracts;
using Xunit;

namespace Graphwright.Tests.McpServer.Contracts;

public class SymbolKindWireMapperTests
{
    public static TheoryData<DeclaredSymbolKind, string> KindToLiteral()
    {
        return new TheoryData<DeclaredSymbolKind, string>
        {
            { DeclaredSymbolKind.Namespace, "namespace" },
            { DeclaredSymbolKind.Class, "class" },
            { DeclaredSymbolKind.Interface, "interface" },
            { DeclaredSymbolKind.Struct, "struct" },
            { DeclaredSymbolKind.Enum, "enum" },
            { DeclaredSymbolKind.Method, "method" },
            { DeclaredSymbolKind.Property, "property" },
            { DeclaredSymbolKind.Field, "field" },
            { DeclaredSymbolKind.Event, "event" },
            { DeclaredSymbolKind.Constructor, "constructor" }
        };
    }

    [Theory]
    [MemberData(nameof(KindToLiteral))]
    public void ToWireLiteralReturnsExpectedLowercaseLiteral(DeclaredSymbolKind kind, string expectedLiteral)
    {
        var literal = SymbolKindWireMapper.Instance.ToWireLiteral(kind);

        Assert.Equal(expectedLiteral, literal);
    }

    [Theory]
    [MemberData(nameof(KindToLiteral))]
    public void TryFromWireLiteralRoundTripsBackToTheOriginalKind(DeclaredSymbolKind kind, string literal)
    {
        var wasParsed = SymbolKindWireMapper.Instance.TryFromWireLiteral(literal, out var parsedKind);

        Assert.True(wasParsed);
        Assert.Equal(kind, parsedKind);
    }

    [Theory]
    [InlineData("delegate")]
    [InlineData("Method")]
    [InlineData("")]
    public void TryFromWireLiteralReturnsFalseForUnrecognizedLiteralInsteadOfThrowing(string literal)
    {
        var wasParsed = SymbolKindWireMapper.Instance.TryFromWireLiteral(literal, out var parsedKind);

        Assert.False(wasParsed);
        Assert.Equal(default, parsedKind);
    }

    [Fact]
    public void TryFromWireLiteralReturnsFalseForNullInsteadOfThrowing()
    {
        var wasParsed = SymbolKindWireMapper.Instance.TryFromWireLiteral(null, out var parsedKind);

        Assert.False(wasParsed);
        Assert.Equal(default, parsedKind);
    }

    [Fact]
    public void ToWireLiteralThrowsArgumentOutOfRangeForUnmappedEnumValue()
    {
        var unmappedKind = (DeclaredSymbolKind)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => SymbolKindWireMapper.Instance.ToWireLiteral(unmappedKind));
    }

    [Fact]
    public void InstanceIsTheSameSingletonAcrossCalls()
    {
        Assert.Same(SymbolKindWireMapper.Instance, SymbolKindWireMapper.Instance);
    }
}
