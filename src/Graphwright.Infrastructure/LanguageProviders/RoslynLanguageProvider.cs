using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Application.LanguageProviders;
using Graphwright.Domain.Exceptions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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

    public async Task<FileContentResult> GetFileAsync(GetFileQuery query, CancellationToken ct)
    {
        if (_snapshot.IsLoaded == false)
        {
            throw new WorkspaceNotLoadedException();
        }

        ArgumentNullException.ThrowIfNull(query);

        ct.ThrowIfCancellationRequested();

        // get_file has no include_generated argument, so a bin/obj/node_modules/*.g.cs path is
        // treated as not-found, same server-side filter as list_symbols (Scenario 6 precedent).
        var documents = ResolveScopedDocumentsOrThrow(query.Path, includeGenerated: false);
        var document = documents[0];

        var sourceText = await document.GetTextAsync(ct);
        var totalLines = sourceText.Lines.Count;

        // Explicit-range mode: StartLine and EndLine are always given as a pair (T07/GetFileTool
        // guarantees this before the query is constructed), so their non-null-ness together
        // selects this mode over whole-file (00-spec.md "What" — mutually exclusive modes).
        if (query.StartLine != null && query.EndLine != null)
        {
            return BuildExplicitRangeResult(query, sourceText, totalLines);
        }

        // Bounded-snippet mode: AroundLine present selects a context window around that line
        // (00-spec.md Scenario 3/4/10), boundary-snapped outward to whole statement/expression
        // and member-header edges via the SyntaxTree (00-spec.md Scenario 11/15, 01-plan.md
        // Decision D3).
        if (query.AroundLine != null)
        {
            var syntaxRoot = await document.GetSyntaxRootAsync(ct);

            return BuildBoundedSnippetResult(query, sourceText, totalLines, syntaxRoot);
        }

        // Whole-file mode: no StartLine/EndLine/AroundLine present on the query, so the entire
        // document is served (00-spec.md Scenario 1).
        return new FileContentResult(query.Path, startLine: 1, endLine: totalLines, totalLines, sourceText.ToString());
    }

    private static FileContentResult BuildExplicitRangeResult(GetFileQuery query, SourceText sourceText, int totalLines)
    {
        var startLine = query.StartLine!.Value;

        // Scenario 9a: start_line itself beyond total_lines means the entire requested range
        // names no real line at all — reject rather than silently clamp/invent a manufactured
        // range (CLAUDE.md "No invented data").
        if (startLine > totalLines)
        {
            throw new InvalidToolArgumentException(
                "start_line", $"must not exceed the file's total line count ({totalLines}).");
        }

        // Scenario 9: end_line partially out of bounds clamps down to total_lines, symmetric
        // with ListSymbolsTool.ValidateOptionalMaxResults's clamp-not-reject precedent
        // (src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:182-200).
        var endLine = Math.Min(query.EndLine!.Value, totalLines);

        var content = SliceLines(sourceText, startLine, endLine);

        return new FileContentResult(query.Path, startLine, endLine, totalLines, content);
    }

    private static FileContentResult BuildBoundedSnippetResult(
        GetFileQuery query, SourceText sourceText, int totalLines, SyntaxNode? syntaxRoot)
    {
        // 00-spec.md Scenario 10: clamp at the file's edges, never padded past them to force a
        // fixed window count. This raw window is what 01-plan.md Decision D3's SyntaxTree
        // boundary-snapping (below) expands outward from, never replacing it.
        var rawStart = Math.Max(1, query.AroundLine!.Value - query.Context);
        var rawEnd = Math.Min(totalLines, query.AroundLine.Value + query.Context);

        // A null syntax root (mirrors ExtractDeclaredSymbolsAsync's defensive null-check style,
        // RoslynLanguageProvider.cs:246-254) means no boundary information is available — the
        // served window falls back to the raw, unexpanded one rather than crashing.
        if (syntaxRoot == null)
        {
            var rawContent = SliceLines(sourceText, rawStart, rawEnd);

            return new FileContentResult(query.Path, rawStart, rawEnd, totalLines, rawContent);
        }

        // 01-plan.md Decision D3: the MemberDeclarationSyntax enclosing AroundLine is the fixed
        // hard ceiling for both edges' per-edge expansion below. Fixing this once (rather than
        // re-deriving it per loop iteration) stops the loop at that member's own span even when
        // an intermediate FindNode lookup — e.g. on a blank inter-member line, which belongs to
        // no node's own Span at all — returns an outer ancestor such as the containing type
        // declaration; expansion must never cross into a sibling member because of that.
        var anchorLineSpan = sourceText.Lines[query.AroundLine.Value - 1].Span;
        var anchorNode = syntaxRoot.FindNode(anchorLineSpan, findInsideTrivia: false, getInnermostNodeForTie: true);
        var enclosingMember = anchorNode.FirstAncestorOrSelf<MemberDeclarationSyntax>() ?? anchorNode;
        var memberLineSpan = enclosingMember.GetLocation().GetLineSpan();
        var memberFloorLine = memberLineSpan.StartLinePosition.Line + 1;
        var memberCeilingLine = memberLineSpan.EndLinePosition.Line + 1;

        var expandedStart = ExpandStartLineOutward(syntaxRoot, sourceText, rawStart, memberFloorLine);
        var expandedEnd = ExpandEndLineOutward(syntaxRoot, sourceText, rawEnd, memberCeilingLine);

        // Scenario 15: header-region clamp, applied after per-edge expansion, scoped to
        // BaseMethodDeclarationSyntax members only (D3 explicitly scopes out plain
        // fields/auto-properties — a stated decision, not a gap).
        var clampedEnd = enclosingMember is BaseMethodDeclarationSyntax methodMember
            ? ApplyHeaderRegionClamp(methodMember, expandedStart, expandedEnd)
            : expandedEnd;

        var content = SliceLines(sourceText, expandedStart, clampedEnd);

        return new FileContentResult(query.Path, expandedStart, clampedEnd, totalLines, content);
    }

    private static int ExpandStartLineOutward(
        SyntaxNode syntaxRoot, SourceText sourceText, int rawStart, int memberFloorLine)
    {
        var currentStart = rawStart;

        while (currentStart > memberFloorLine)
        {
            var lineSpan = sourceText.Lines[currentStart - 1].Span;
            var node = syntaxRoot.FindNode(lineSpan, findInsideTrivia: false, getInnermostNodeForTie: true);
            var nodeStartLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            // The edge already sits on this node's own boundary — nothing left to expand. This
            // is why Scenario 3/4's already-clean windows are never expanded.
            if (nodeStartLine == currentStart)
            {
                return currentStart;
            }

            // The raw edge splits this node; expand outward and retry — the new, earlier edge
            // may itself split a larger enclosing node (an if splits a block which splits a
            // method body). Hard ceiling: never expand earlier than the enclosing member's span.
            currentStart = Math.Max(nodeStartLine, memberFloorLine);
        }

        return currentStart;
    }

    private static int ExpandEndLineOutward(
        SyntaxNode syntaxRoot, SourceText sourceText, int rawEnd, int memberCeilingLine)
    {
        var currentEnd = rawEnd;

        while (currentEnd < memberCeilingLine)
        {
            var lineSpan = sourceText.Lines[currentEnd - 1].Span;
            var node = syntaxRoot.FindNode(lineSpan, findInsideTrivia: false, getInnermostNodeForTie: true);
            var nodeEndLine = node.GetLocation().GetLineSpan().EndLinePosition.Line + 1;

            if (nodeEndLine == currentEnd)
            {
                return currentEnd;
            }

            // Hard ceiling: never expand later than the enclosing member's own span.
            currentEnd = Math.Min(nodeEndLine, memberCeilingLine);
        }

        return currentEnd;
    }

    private static int ApplyHeaderRegionClamp(BaseMethodDeclarationSyntax methodMember, int start, int end)
    {
        var headerEndLine = GetHeaderEndLineOrNull(methodMember);

        if (headerEndLine == null)
        {
            return end;
        }

        var memberStartLine = methodMember.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        // Scenario 15: the window's start line falls within the member's own header (at or after
        // the member's own start, at or before the header's own last line) but would otherwise
        // extend into the member's Body/ExpressionBody — clamp endLine to the header's own last
        // line, never pulling in the full body. A start line before the member even begins (e.g.
        // still inside a preceding sibling or the enclosing type's own header) must never trigger
        // this clamp — the window has nothing to do with this member's signature in that case.
        if (start >= memberStartLine && start <= headerEndLine.Value && end > headerEndLine.Value)
        {
            return headerEndLine.Value;
        }

        return end;
    }

    private static int? GetHeaderEndLineOrNull(BaseMethodDeclarationSyntax methodMember)
    {
        SyntaxToken tokenBeforeBody;

        if (methodMember.Body != null)
        {
            tokenBeforeBody = methodMember.Body.GetFirstToken().GetPreviousToken();
        }
        else if (methodMember.ExpressionBody != null)
        {
            tokenBeforeBody = methodMember.ExpressionBody.GetFirstToken().GetPreviousToken();
        }
        else if (methodMember.SemicolonToken.IsKind(SyntaxKind.SemicolonToken))
        {
            tokenBeforeBody = methodMember.SemicolonToken.GetPreviousToken();
        }
        else
        {
            // No Body/ExpressionBody/SemicolonToken present at all — nothing to clamp against.
            return null;
        }

        return tokenBeforeBody.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
    }

    private static string SliceLines(SourceText sourceText, int startLine, int endLine)
    {
        // 01-plan.md Decision D1: preserves every line break between the selected lines
        // verbatim (never normalizes to Environment.NewLine), stopping at endLine's own .End
        // so no extra blank line is invented past the requested range.
        var span = TextSpan.FromBounds(sourceText.Lines[startLine - 1].Start, sourceText.Lines[endLine - 1].End);

        return sourceText.GetSubText(span).ToString();
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
