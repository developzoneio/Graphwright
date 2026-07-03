namespace Graphwright.Application.LanguageProviders;

/// <summary>
/// The closed set of declaration kinds an <see cref="ILanguageProvider"/> may report for a
/// <see cref="DeclaredSymbol"/>. Mirrors the <c>kinds</c> filter vocabulary accepted by
/// <c>list_symbols</c> (CLAUDE.md "MCP tool surface"); an unrecognized wire literal is rejected
/// with <c>INVALID_ARGUMENT</c> before this enum is ever touched.
/// </summary>
public enum DeclaredSymbolKind
{
    Namespace,
    Class,
    Interface,
    Struct,
    Enum,
    Method,
    Property,
    Field,
    Event,
    Constructor
}
