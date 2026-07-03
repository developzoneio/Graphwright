using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Application.LanguageProviders;
using Graphwright.Domain.Exceptions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Graphwright.Infrastructure.LanguageProviders;

/// <summary>
/// Roslyn-backed <see cref="ILanguageProvider"/> implementation. Holds an immutable
/// <see cref="RoslynWorkspaceSnapshot"/>, resolves <see cref="ListSymbolsQuery.Path"/> against
/// its <see cref="Microsoft.CodeAnalysis.Solution"/> — a single file, a directory subtree, or
/// (when <see cref="ListSymbolsQuery.Path"/> is <c>null</c>) the whole workspace — and extracts
/// declared symbols by walking <see cref="MemberDeclarationSyntax"/> descendant nodes of each
/// resolved document's syntax root (01-plan.md Decision D2 — never a whole-
/// <see cref="Compilation"/> symbol-tree walk, so `line` stays anchored to the exact declaration
/// site). Directory/whole-workspace scoping excludes generated and build-output paths by default
/// (CLAUDE.md cross-cutting rule 3). The final pipeline stage filters by <c>name_filter</c>/
/// <c>kinds</c>, orders by file ascending then line ascending, and caps/truncates at
/// <see cref="ListSymbolsQuery.MaxResults"/> (CLAUDE.md cross-cutting rules 5-6; CLAUDE.md
/// "Layer map" DIP boundary: no Roslyn type crosses out of this class).
/// </summary>
public sealed class RoslynLanguageProvider : ILanguageProvider
{
    private static readonly string[] _excludedPathSegments = { "bin", "obj", "node_modules" };

    private readonly RoslynWorkspaceSnapshot _snapshot;

    public RoslynLanguageProvider(RoslynWorkspaceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _snapshot = snapshot;
    }

    public async Task<SymbolListResult> ListSymbolsAsync(ListSymbolsQuery query, CancellationToken ct)
    {
        if (_snapshot.IsLoaded == false)
        {
            throw new WorkspaceNotLoadedException();
        }

        ArgumentNullException.ThrowIfNull(query);

        ct.ThrowIfCancellationRequested();

        var documents = ResolveScopedDocumentsOrThrow(query.Path, query.IncludeGenerated);

        var declaredSymbols = new List<DeclaredSymbol>();

        foreach (var document in documents)
        {
            declaredSymbols.AddRange(await ExtractDeclaredSymbolsAsync(document, ct));
        }

        var orderedSymbols = FilterByNameAndKind(declaredSymbols, query.NameFilter, query.Kinds)
            .OrderBy(symbol => symbol.File, StringComparer.Ordinal)
            .ThenBy(symbol => symbol.Line)
            .ToList();

        var totalFound = orderedSymbols.Count;
        var cappedResults = orderedSymbols.Take(query.MaxResults).ToList();

        return new SymbolListResult(cappedResults, truncated: totalFound > cappedResults.Count, totalFound);
    }

    private static IEnumerable<DeclaredSymbol> FilterByNameAndKind(
        IEnumerable<DeclaredSymbol> declaredSymbols, string? nameFilter, IReadOnlyList<DeclaredSymbolKind>? kinds)
    {
        // Scenario 4: a single Contains(..., OrdinalIgnoreCase) rule satisfies both substring and
        // exact-match matching — an exact match is trivially a substring of itself.
        return declaredSymbols
            .Where(symbol => nameFilter == null
                || symbol.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
            .Where(symbol => kinds == null || kinds.Contains(symbol.Kind));
    }

    private IReadOnlyList<Document> ResolveScopedDocumentsOrThrow(string? path, bool includeGenerated)
    {
        var allDocuments = _snapshot.Solution.Projects.SelectMany(project => project.Documents);

        if (path == null)
        {
            // Whole-workspace scoping (Scenario 3): every non-excluded document in the solution.
            return allDocuments.Where(document => IsIncludedByDefault(document, includeGenerated)).ToList();
        }

        var singleFileMatch = allDocuments.FirstOrDefault(candidate => candidate.FilePath == path);

        if (singleFileMatch != null)
        {
            // Single-file case: exact-path resolution (T04/T05), still subject to the same
            // bin/obj/node_modules/*.g.cs server-side filter as directory/whole-workspace scoping
            // (CLAUDE.md "these are a server-side filter" — Scenario 6 applies regardless of how
            // the scope was selected). An excluded file is treated as not found, never surfaced.
            if (IsIncludedByDefault(singleFileMatch, includeGenerated) == false)
            {
                throw new SourceFileNotFoundException(path);
            }

            return new[] { singleFileMatch };
        }

        var directoryPrefix = path + "/";

        // Path-segment boundary match ("a" must not match "abc/Foo.cs") — never a raw
        // string-prefix check.
        var directoryMatches = allDocuments
            .Where(candidate => candidate.FilePath != null
                && candidate.FilePath.StartsWith(directoryPrefix, StringComparison.Ordinal))
            .Where(document => IsIncludedByDefault(document, includeGenerated))
            .ToList();

        if (directoryMatches.Count == 0)
        {
            throw new SourceFileNotFoundException(path);
        }

        return directoryMatches;
    }

    private static bool IsIncludedByDefault(Document document, bool includeGenerated)
    {
        var filePath = document.FilePath;

        if (filePath == null)
        {
            return false;
        }

        var pathSegments = filePath.Split('/');

        // Scenario 6: "bin/, obj/, and node_modules/ are still excluded (CLAUDE.md 'Exclude
        // bin/, obj/, *.g.cs, node_modules/' — these are a server-side filter, not something
        // include_generated is defined to override for build-output directories)" — unconditional
        // regardless of includeGenerated.
        if (pathSegments.Any(segment => _excludedPathSegments.Contains(segment)))
        {
            return false;
        }

        if (includeGenerated == false && filePath.EndsWith(".g.cs", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static async Task<IReadOnlyList<DeclaredSymbol>> ExtractDeclaredSymbolsAsync(
        Document document, CancellationToken ct)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync(ct);
        var semanticModel = await document.GetSemanticModelAsync(ct);

        // Both are non-null for a real document with source text in a compiling project
        // (RoslynWorkspaceTestFixtures/the eventual production loader both guarantee this).
        if (syntaxRoot == null || semanticModel == null)
        {
            return Array.Empty<DeclaredSymbol>();
        }

        var declaredSymbols = new List<DeclaredSymbol>();

        foreach (var memberNode in syntaxRoot.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            AppendDeclaredSymbols(memberNode, semanticModel, declaredSymbols);
        }

        return declaredSymbols;
    }

    private static void AppendDeclaredSymbols(
        MemberDeclarationSyntax memberNode, SemanticModel semanticModel, List<DeclaredSymbol> declaredSymbols)
    {
        // GetDeclaredSymbol has no applicable overload for FieldDeclarationSyntax or
        // EventFieldDeclarationSyntax and returns null for both, by design — one declaration
        // can name multiple variables (e.g. `public int x, y;`). Descend into the declarator
        // list and resolve one symbol per declarator instead (01-plan.md Decision D2 carve-out).
        if (memberNode is FieldDeclarationSyntax fieldNode)
        {
            AppendVariableDeclaredSymbols(fieldNode.Declaration, semanticModel, declaredSymbols);
            return;
        }

        if (memberNode is EventFieldDeclarationSyntax eventFieldNode)
        {
            AppendVariableDeclaredSymbols(eventFieldNode.Declaration, semanticModel, declaredSymbols);
            return;
        }

        var symbol = semanticModel.GetDeclaredSymbol(memberNode);
        AppendIfClassified(symbol, memberNode.GetLocation(), declaredSymbols);
    }

    private static void AppendVariableDeclaredSymbols(
        VariableDeclarationSyntax declaration, SemanticModel semanticModel, List<DeclaredSymbol> declaredSymbols)
    {
        foreach (var variableDeclarator in declaration.Variables)
        {
            var symbol = semanticModel.GetDeclaredSymbol(variableDeclarator);
            AppendIfClassified(symbol, variableDeclarator.GetLocation(), declaredSymbols);
        }
    }

    private static void AppendIfClassified(ISymbol? symbol, Location location, List<DeclaredSymbol> declaredSymbols)
    {
        if (symbol == null)
        {
            return;
        }

        var kind = ClassifyOrNull(symbol);

        if (kind == null)
        {
            return;
        }

        declaredSymbols.Add(ToDeclaredSymbol(symbol, kind.Value, location));
    }

    private static DeclaredSymbolKind? ClassifyOrNull(ISymbol symbol)
    {
        return symbol switch
        {
            INamespaceSymbol => DeclaredSymbolKind.Namespace,
            INamedTypeSymbol namedTypeSymbol => ClassifyNamedTypeOrNull(namedTypeSymbol),
            IMethodSymbol methodSymbol => ClassifyMethodOrNull(methodSymbol),
            IPropertySymbol => DeclaredSymbolKind.Property,
            IFieldSymbol => DeclaredSymbolKind.Field,
            IEventSymbol => DeclaredSymbolKind.Event,
            _ => null
        };
    }

    private static DeclaredSymbolKind? ClassifyNamedTypeOrNull(INamedTypeSymbol namedTypeSymbol)
    {
        return namedTypeSymbol.TypeKind switch
        {
            TypeKind.Class => DeclaredSymbolKind.Class,
            TypeKind.Struct => DeclaredSymbolKind.Struct,
            TypeKind.Interface => DeclaredSymbolKind.Interface,
            TypeKind.Enum => DeclaredSymbolKind.Enum,

            // TypeKind.Delegate is a deliberate closed-vocabulary gap (01-plan.md OQ-2):
            // DeclaredSymbolKind has no Delegate member, so delegates are never emitted.
            _ => null
        };
    }

    private static DeclaredSymbolKind? ClassifyMethodOrNull(IMethodSymbol methodSymbol)
    {
        return methodSymbol.MethodKind switch
        {
            MethodKind.Ordinary => DeclaredSymbolKind.Method,
            MethodKind.Constructor => DeclaredSymbolKind.Constructor,
            MethodKind.StaticConstructor => DeclaredSymbolKind.Constructor,

            // PropertyGet/PropertySet/EventAdd/EventRemove/EventRaise/Destructor/Conversion/
            // UserDefinedOperator are all deliberately not emitted as separate entries.
            _ => null
        };
    }

    private static DeclaredSymbol ToDeclaredSymbol(ISymbol symbol, DeclaredSymbolKind kind, Location location)
    {
        var lineSpan = location.GetLineSpan();
        var signature = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        var container = symbol.ContainingSymbol?.ToDisplayString() ?? string.Empty;

        // Namespaces carry no C# access modifier. Roslyn 4.14.0's actual
        // INamespaceSymbol.DeclaredAccessibility reports Public (contradicting the "always
        // NotApplicable" assumption in 01-plan.md OQ-6 — recorded in 03-decisions.md), so this
        // is reported honestly as "not_applicable" regardless of the raw enum value
        // (CLAUDE.md "No invented data" — never surface a plausible-looking "public").
        var accessibility = symbol is INamespaceSymbol
            ? "not_applicable"
            : ToAccessibilityLiteral(symbol.DeclaredAccessibility);

        return new DeclaredSymbol(
            symbol.Name,
            kind,
            lineSpan.Path,
            lineSpan.StartLinePosition.Line + 1,
            signature,
            container,
            accessibility);
    }

    private static string ToAccessibilityLiteral(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "protected_internal",
            Accessibility.ProtectedAndInternal => "private_protected",
            _ => "not_applicable"
        };
    }
}
