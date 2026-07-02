using System;
using Graphwright.Domain.Exceptions;
using Graphwright.McpServer.Contracts;
using Xunit;

namespace Graphwright.Tests.McpServer.Contracts;

public class ExceptionEnvelopeMapperTests
{
    private sealed class UnexpectedTestException : Exception
    {
        public UnexpectedTestException(string message)
            : base(message)
        {
        }
    }

    [Fact]
    public void WorkspaceNotLoadedExceptionMapsToWorkspaceNotLoadedWireCodeAndIsRetryable()
    {
        var exception = new WorkspaceNotLoadedException();

        var envelope = ExceptionEnvelopeMapper.Instance.ToErrorEnvelope(exception);

        Assert.False(envelope.Ok);
        Assert.Equal("WORKSPACE_NOT_LOADED", envelope.Error.Code);
        Assert.Equal(exception.Message, envelope.Error.Message);
        Assert.True(envelope.Error.Retryable);
    }

    [Fact]
    public void SymbolNotFoundExceptionMapsToSymbolNotFoundWireCodeAndIsNotRetryable()
    {
        var exception = new SymbolNotFoundException("Graphwright.Domain.Foo");

        var envelope = ExceptionEnvelopeMapper.Instance.ToErrorEnvelope(exception);

        Assert.False(envelope.Ok);
        Assert.Equal("SYMBOL_NOT_FOUND", envelope.Error.Code);
        Assert.Equal(exception.Message, envelope.Error.Message);
        Assert.False(envelope.Error.Retryable);
    }

    [Fact]
    public void SourceFileNotFoundExceptionMapsToFileNotFoundWireCodeAndIsNotRetryable()
    {
        var exception = new SourceFileNotFoundException("src/Graphwright.Domain/Foo.cs");

        var envelope = ExceptionEnvelopeMapper.Instance.ToErrorEnvelope(exception);

        Assert.False(envelope.Ok);
        Assert.Equal("FILE_NOT_FOUND", envelope.Error.Code);
        Assert.Equal(exception.Message, envelope.Error.Message);
        Assert.False(envelope.Error.Retryable);
    }

    [Fact]
    public void AmbiguousSymbolExceptionMapsToAmbiguousSymbolWireCodeAndIsNotRetryable()
    {
        var exception = new AmbiguousSymbolException("Foo");

        var envelope = ExceptionEnvelopeMapper.Instance.ToErrorEnvelope(exception);

        Assert.False(envelope.Ok);
        Assert.Equal("AMBIGUOUS_SYMBOL", envelope.Error.Code);
        Assert.Equal(exception.Message, envelope.Error.Message);
        Assert.False(envelope.Error.Retryable);
    }

    [Fact]
    public void InvalidToolArgumentExceptionMapsToInvalidArgumentWireCodeAndIsNotRetryable()
    {
        var exception = new InvalidToolArgumentException("symbolName", "must not be empty");

        var envelope = ExceptionEnvelopeMapper.Instance.ToErrorEnvelope(exception);

        Assert.False(envelope.Ok);
        Assert.Equal("INVALID_ARGUMENT", envelope.Error.Code);
        Assert.Equal(exception.Message, envelope.Error.Message);
        Assert.False(envelope.Error.Retryable);
    }

    [Fact]
    public void ToolNotImplementedExceptionMapsToInternalWireCodeAndIsNotRetryable()
    {
        var exception = new ToolNotImplementedException("mcp__gitnexus__list_symbols");

        var envelope = ExceptionEnvelopeMapper.Instance.ToErrorEnvelope(exception);

        Assert.False(envelope.Ok);
        Assert.Equal("INTERNAL", envelope.Error.Code);
        Assert.Equal(exception.Message, envelope.Error.Message);
        Assert.False(envelope.Error.Retryable);
    }

    [Fact]
    public void UnexpectedExceptionMapsToInternalWithGenericNonLeakingMessage()
    {
        var exception = new UnexpectedTestException("raw internal detail that must never leak");

        var envelope = ExceptionEnvelopeMapper.Instance.ToErrorEnvelope(exception);

        Assert.False(envelope.Ok);
        Assert.Equal("INTERNAL", envelope.Error.Code);
        Assert.False(envelope.Error.Retryable);
        Assert.NotEqual(exception.Message, envelope.Error.Message);
        Assert.DoesNotContain("raw internal detail", envelope.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("UnexpectedTestException", envelope.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("at Graphwright", envelope.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InstanceIsTheSameSingletonAcrossCalls()
    {
        Assert.Same(ExceptionEnvelopeMapper.Instance, ExceptionEnvelopeMapper.Instance);
    }
}
