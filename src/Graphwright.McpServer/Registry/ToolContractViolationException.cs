using System;
using System.Collections.Generic;
using Graphwright.Domain.Errors;
using Graphwright.Domain.Exceptions;

namespace Graphwright.McpServer.Registry;

/// <summary>
/// Raised by <see cref="ToolRegistry.AssertContract"/> when the advertised tool set drifts
/// from the frozen 5-tool <c>mcp__gitnexus__*</c> surface (CLAUDE.md "MCP tool surface —
/// Month 1 contract — frozen"; 00-spec.md Scenario 1: "startup fails fast (non-zero exit,
/// logged reason) if the set differs in count or name"). Maps to
/// <see cref="GraphwrightErrorCode.Internal"/> — this is a startup configuration defect, not
/// something a client request can retry.
/// </summary>
public sealed class ToolContractViolationException : GraphwrightException
{
    public ToolContractViolationException(
        IReadOnlyList<string> expectedNames,
        IReadOnlyList<string> actualNames,
        IReadOnlyList<string> missingNames,
        IReadOnlyList<string> extraNames,
        IReadOnlyList<string> duplicateNames)
        : base(BuildMessage(expectedNames, actualNames, missingNames, extraNames, duplicateNames))
    {
        ExpectedNames = expectedNames;
        ActualNames = actualNames;
        MissingNames = missingNames;
        ExtraNames = extraNames;
        DuplicateNames = duplicateNames;
    }

    public ToolContractViolationException(
        IReadOnlyList<string> expectedNames,
        IReadOnlyList<string> actualNames,
        IReadOnlyList<string> missingNames,
        IReadOnlyList<string> extraNames,
        IReadOnlyList<string> duplicateNames,
        Exception innerException)
        : base(BuildMessage(expectedNames, actualNames, missingNames, extraNames, duplicateNames), innerException)
    {
        ExpectedNames = expectedNames;
        ActualNames = actualNames;
        MissingNames = missingNames;
        ExtraNames = extraNames;
        DuplicateNames = duplicateNames;
    }

    public override GraphwrightErrorCode Code => GraphwrightErrorCode.Internal;

    public override bool IsRetryable => false;

    /// <summary>
    /// The frozen 5-name set the registry was asserted against (CLAUDE.md "MCP tool surface").
    /// </summary>
    public IReadOnlyList<string> ExpectedNames { get; }

    /// <summary>
    /// The actual advertised tool names at the time of the violation, in registry order.
    /// </summary>
    public IReadOnlyList<string> ActualNames { get; }

    /// <summary>
    /// Frozen names that were not found among <see cref="ActualNames"/>.
    /// </summary>
    public IReadOnlyList<string> MissingNames { get; }

    /// <summary>
    /// Advertised names that are not part of the frozen set (includes renamed and
    /// wrong-prefix tools, since neither matches a frozen literal).
    /// </summary>
    public IReadOnlyList<string> ExtraNames { get; }

    /// <summary>
    /// Advertised names that appear more than once in <see cref="ActualNames"/>.
    /// </summary>
    public IReadOnlyList<string> DuplicateNames { get; }

    private static string BuildMessage(
        IReadOnlyList<string> expectedNames,
        IReadOnlyList<string> actualNames,
        IReadOnlyList<string> missingNames,
        IReadOnlyList<string> extraNames,
        IReadOnlyList<string> duplicateNames)
    {
        return "Tool registry contract violation: advertised tool set does not match the frozen "
            + $"mcp__gitnexus__* surface. Expected: [{string.Join(", ", expectedNames)}]. "
            + $"Actual: [{string.Join(", ", actualNames)}]. "
            + $"Missing: [{string.Join(", ", missingNames)}]. "
            + $"Extra: [{string.Join(", ", extraNames)}]. "
            + $"Duplicates: [{string.Join(", ", duplicateNames)}].";
    }
}
