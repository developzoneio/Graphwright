using System;
using System.Collections.Generic;
using System.Text.Json;
using Graphwright.McpServer.Contracts;
using Xunit;

namespace Graphwright.Tests.McpServer.Contracts;

public class EnvelopeSerializationTests
{
    private static readonly string[] _expectedErrorPropertyNames = { "code", "message", "retryable" };

    private sealed class SamplePayload
    {
        public SamplePayload(string name, int count)
        {
            Name = name;
            Count = count;
        }

        public string Name { get; }

        public int Count { get; }
    }

    [Fact]
    public void SuccessEnvelopeSerializesOkTrueWithResultAndNoErrorProperty()
    {
        var envelope = new ToolSuccessEnvelope<SamplePayload>(new SamplePayload("probe", 3));

        var json = JsonSerializer.Serialize(envelope);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("probe", root.GetProperty("result").GetProperty("Name").GetString());
        Assert.Equal(3, root.GetProperty("result").GetProperty("Count").GetInt32());
        Assert.False(root.TryGetProperty("error", out _));
    }

    [Fact]
    public void SuccessEnvelopeRoundTripsThroughJson()
    {
        var envelope = new ToolSuccessEnvelope<SamplePayload>(new SamplePayload("probe", 3));

        var json = JsonSerializer.Serialize(envelope);
        var deserialized = JsonSerializer.Deserialize<ToolSuccessEnvelope<SamplePayload>>(json);

        Assert.NotNull(deserialized);
        Assert.True(deserialized!.Ok);
        Assert.Equal("probe", deserialized.Result.Name);
        Assert.Equal(3, deserialized.Result.Count);
    }

    [Fact]
    public void ErrorEnvelopeSerializesOkFalseWithErrorObjectContainingExactlyCodeMessageRetryable()
    {
        var envelope = new ToolErrorEnvelope(new ToolError("WORKSPACE_NOT_LOADED", "Workspace not loaded.", true));

        var json = JsonSerializer.Serialize(envelope);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.False(root.GetProperty("ok").GetBoolean());

        var error = root.GetProperty("error");
        var errorPropertyNames = new List<string>();
        foreach (var property in error.EnumerateObject())
        {
            errorPropertyNames.Add(property.Name);
        }

        errorPropertyNames.Sort(StringComparer.Ordinal);
        Assert.Equal(_expectedErrorPropertyNames, errorPropertyNames);
        Assert.Equal("WORKSPACE_NOT_LOADED", error.GetProperty("code").GetString());
        Assert.Equal("Workspace not loaded.", error.GetProperty("message").GetString());
        Assert.True(error.GetProperty("retryable").GetBoolean());
    }

    [Fact]
    public void ErrorEnvelopeRoundTripsThroughJson()
    {
        var envelope = new ToolErrorEnvelope(new ToolError("INTERNAL", "Unexpected failure.", false));

        var json = JsonSerializer.Serialize(envelope);
        var deserialized = JsonSerializer.Deserialize<ToolErrorEnvelope>(json);

        Assert.NotNull(deserialized);
        Assert.False(deserialized!.Ok);
        Assert.Equal("INTERNAL", deserialized.Error.Code);
        Assert.Equal("Unexpected failure.", deserialized.Error.Message);
        Assert.False(deserialized.Error.Retryable);
    }
}
