using System;
using System.Diagnostics.CodeAnalysis;
using Graphwright.Application.LanguageProviders;

namespace Graphwright.McpServer.Contracts;

/// <summary>
/// Maps <see cref="DeclaredSymbolKind"/> to and from the lowercase wire literal used by the
/// <c>list_symbols</c> "kinds" filter (00-spec.md OQ-2: <c>namespace, class, interface, struct,
/// enum, method, property, field, event, constructor</c>). <see cref="TryFromWireLiteral"/>
/// matches case-sensitively against the exact lowercase literal — deliberately different from
/// <c>name_filter</c>, which matches case-insensitively (00-spec.md Scenario 4). The "kinds"
/// vocabulary is a closed set of protocol literals, not a human-typed search term, so an
/// unexpected case is an unrecognized literal rather than something to normalize away.
/// </summary>
public sealed class SymbolKindWireMapper
{
    public static readonly SymbolKindWireMapper Instance = new();

    private SymbolKindWireMapper()
    {
    }

    /// <summary>
    /// Maps <paramref name="kind"/> to its lowercase wire literal. Covers the full closed set of
    /// <see cref="DeclaredSymbolKind"/> members; an unmapped value can only be reached by adding
    /// a new enum member without updating this switch, or by an unsafe cast, so it throws rather
    /// than silently inventing a literal.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Mapper is deliberately an instance member per project no-static-class " +
            "convention, mirroring ExceptionEnvelopeMapper.")]
    public string ToWireLiteral(DeclaredSymbolKind kind)
    {
        switch (kind)
        {
            case DeclaredSymbolKind.Namespace:
                return "namespace";
            case DeclaredSymbolKind.Class:
                return "class";
            case DeclaredSymbolKind.Interface:
                return "interface";
            case DeclaredSymbolKind.Struct:
                return "struct";
            case DeclaredSymbolKind.Enum:
                return "enum";
            case DeclaredSymbolKind.Method:
                return "method";
            case DeclaredSymbolKind.Property:
                return "property";
            case DeclaredSymbolKind.Field:
                return "field";
            case DeclaredSymbolKind.Event:
                return "event";
            case DeclaredSymbolKind.Constructor:
                return "constructor";
            default:
                // Defensive: an unmapped enum value must never fall through silently.
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unrecognized DeclaredSymbolKind value.");
        }
    }

    /// <summary>
    /// Attempts to map <paramref name="literal"/> back to its <see cref="DeclaredSymbolKind"/>.
    /// Matching is case-sensitive lowercase-exact — deliberately different from the
    /// case-insensitive <c>name_filter</c> rule (see class remarks). Returns <c>false</c> for any
    /// unrecognized literal, including <c>null</c> or empty input; never throws.
    /// </summary>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Mapper is deliberately an instance member per project no-static-class " +
            "convention, mirroring ExceptionEnvelopeMapper.")]
    public bool TryFromWireLiteral(string? literal, out DeclaredSymbolKind kind)
    {
        switch (literal)
        {
            case "namespace":
                kind = DeclaredSymbolKind.Namespace;
                return true;
            case "class":
                kind = DeclaredSymbolKind.Class;
                return true;
            case "interface":
                kind = DeclaredSymbolKind.Interface;
                return true;
            case "struct":
                kind = DeclaredSymbolKind.Struct;
                return true;
            case "enum":
                kind = DeclaredSymbolKind.Enum;
                return true;
            case "method":
                kind = DeclaredSymbolKind.Method;
                return true;
            case "property":
                kind = DeclaredSymbolKind.Property;
                return true;
            case "field":
                kind = DeclaredSymbolKind.Field;
                return true;
            case "event":
                kind = DeclaredSymbolKind.Event;
                return true;
            case "constructor":
                kind = DeclaredSymbolKind.Constructor;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}
