using System;
using Graphwright.Domain.Errors;

namespace Graphwright.Domain.Exceptions;

/// <summary>
/// Raised when a requested source file does not exist in the indexed workspace. Named
/// "SourceFileNotFoundException" (rather than "FileNotFoundException") to avoid colliding
/// with <see cref="System.IO.FileNotFoundException"/>. Maps to
/// <see cref="GraphwrightErrorCode.FileNotFound"/>. Not retryable — the client must change
/// the path it asked for.
/// </summary>
public sealed class SourceFileNotFoundException : GraphwrightException
{
    public SourceFileNotFoundException(string filePath)
        : base($"Source file not found: '{filePath}'.")
    {
        FilePath = filePath;
    }

    public SourceFileNotFoundException(string filePath, Exception innerException)
        : base($"Source file not found: '{filePath}'.", innerException)
    {
        FilePath = filePath;
    }

    public override GraphwrightErrorCode Code => GraphwrightErrorCode.FileNotFound;

    public override bool IsRetryable => false;

    /// <summary>
    /// The relative source file path that could not be resolved.
    /// </summary>
    public string FilePath { get; }
}
