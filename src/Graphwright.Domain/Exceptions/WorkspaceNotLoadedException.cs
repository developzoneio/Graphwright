using System;
using Graphwright.Domain.Errors;

namespace Graphwright.Domain.Exceptions;

/// <summary>
/// Raised when a tool is invoked before the workspace index has finished loading.
/// Maps to <see cref="GraphwrightErrorCode.WorkspaceNotLoaded"/> and is retryable — the
/// client may simply wait for the index to become ready and resend the same request.
/// </summary>
public sealed class WorkspaceNotLoadedException : GraphwrightException
{
    private const string DEFAULT_MESSAGE = "Workspace has not finished loading; the index is not ready yet.";

    public WorkspaceNotLoadedException()
        : base(DEFAULT_MESSAGE)
    {
    }

    public WorkspaceNotLoadedException(string message)
        : base(message)
    {
    }

    public WorkspaceNotLoadedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override GraphwrightErrorCode Code => GraphwrightErrorCode.WorkspaceNotLoaded;

    public override bool IsRetryable => true;
}
