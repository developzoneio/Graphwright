using System;
using System.Linq;
using Graphwright.Domain.Errors;
using Graphwright.Domain.Exceptions;
using Xunit;

namespace Graphwright.Tests.Domain.Exceptions;

public class GraphwrightExceptionTests
{
    [Fact]
    public void WorkspaceNotLoadedExceptionIsRetryable()
    {
        var exception = new WorkspaceNotLoadedException();

        Assert.Equal(GraphwrightErrorCode.WorkspaceNotLoaded, exception.Code);
        Assert.True(exception.IsRetryable);
    }

    [Fact]
    public void SymbolNotFoundExceptionCarriesSymbolNameAndIsNotRetryable()
    {
        var exception = new SymbolNotFoundException("Graphwright.Domain.Foo");

        Assert.Equal(GraphwrightErrorCode.SymbolNotFound, exception.Code);
        Assert.False(exception.IsRetryable);
        Assert.Equal("Graphwright.Domain.Foo", exception.SymbolName);
        Assert.Contains("Graphwright.Domain.Foo", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceFileNotFoundExceptionCarriesFilePathAndIsNotRetryable()
    {
        var exception = new SourceFileNotFoundException("src/Graphwright.Domain/Foo.cs");

        Assert.Equal(GraphwrightErrorCode.FileNotFound, exception.Code);
        Assert.False(exception.IsRetryable);
        Assert.Equal("src/Graphwright.Domain/Foo.cs", exception.FilePath);
        Assert.Contains("src/Graphwright.Domain/Foo.cs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AmbiguousSymbolExceptionCarriesSymbolNameAndIsNotRetryable()
    {
        var exception = new AmbiguousSymbolException("Foo");

        Assert.Equal(GraphwrightErrorCode.AmbiguousSymbol, exception.Code);
        Assert.False(exception.IsRetryable);
        Assert.Equal("Foo", exception.SymbolName);
        Assert.Contains("Foo", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidToolArgumentExceptionCarriesArgumentNameAndReasonAndIsNotRetryable()
    {
        var exception = new InvalidToolArgumentException("symbolName", "must not be empty");

        Assert.Equal(GraphwrightErrorCode.InvalidArgument, exception.Code);
        Assert.False(exception.IsRetryable);
        Assert.Equal("symbolName", exception.ArgumentName);
        Assert.Equal("must not be empty", exception.Reason);
        Assert.Contains("symbolName", exception.Message, StringComparison.Ordinal);
        Assert.Contains("must not be empty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolNotImplementedExceptionMapsToInternalAndIsNotRetryable()
    {
        var exception = new ToolNotImplementedException("mcp__gitnexus__list_symbols");

        Assert.Equal(GraphwrightErrorCode.Internal, exception.Code);
        Assert.False(exception.IsRetryable);
        Assert.Equal("mcp__gitnexus__list_symbols", exception.ToolName);
        Assert.Contains("mcp__gitnexus__list_symbols", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DerivedExceptionsCoverTheClosedErrorCodeSetExactlyOnceEach()
    {
        // Every closed-set derived exception is listed here explicitly, so this test
        // fails the moment a code is added or dropped without a matching exception type.
        GraphwrightException[] exceptions =
        {
            new WorkspaceNotLoadedException(),
            new SymbolNotFoundException("probe"),
            new SourceFileNotFoundException("probe"),
            new AmbiguousSymbolException("probe"),
            new InvalidToolArgumentException("probe", "probe"),
            new ToolNotImplementedException("probe")
        };

        var actualCodes = exceptions.Select(exception => exception.Code).OrderBy(code => code).ToArray();
        var expectedCodes = Enum.GetValues<GraphwrightErrorCode>().OrderBy(code => code).ToArray();

        Assert.Equal(6, exceptions.Length);
        Assert.Equal(expectedCodes, actualCodes);
    }
}
