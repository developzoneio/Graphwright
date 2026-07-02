namespace Graphwright.Domain.Errors;

/// <summary>
/// The closed set of error codes carried on the MCP error envelope's "code" field.
/// Mirrors CLAUDE.md "Error envelope" wire values exactly; the ALL_UPPER wire strings
/// are produced only by the McpServer mapper, never here.
/// </summary>
public enum GraphwrightErrorCode
{
    WorkspaceNotLoaded,
    SymbolNotFound,
    FileNotFound,
    AmbiguousSymbol,
    InvalidArgument,
    Internal
}
