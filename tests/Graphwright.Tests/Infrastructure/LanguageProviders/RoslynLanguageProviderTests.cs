using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Graphwright.Application.LanguageProviders;
using Graphwright.Domain.Exceptions;
using Graphwright.Infrastructure.LanguageProviders;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Graphwright.Tests.Infrastructure.LanguageProviders;

public class RoslynLanguageProviderTests
{
    // Line 1: using System;
    // Line 2: using System.Threading.Tasks;
    // Line 3: (blank)
    // Line 4: namespace Acme.Orders
    // Line 5: {
    // Line 6:     public class OrderService
    // Line 7:     {
    // Line 8:         private int _count;
    // Line 9: (blank)
    // Line 10:        public Task<int> GetTotalAsync(int orderId)
    // Line 11:        {
    // Line 12:            throw new NotImplementedException();
    // Line 13:        }
    // Line 14:    }
    // Line 15: }
    private const string ORDER_SERVICE_SOURCE = @"using System;
using System.Threading.Tasks;

namespace Acme.Orders
{
    public class OrderService
    {
        private int _count;

        public Task<int> GetTotalAsync(int orderId)
        {
            throw new NotImplementedException();
        }
    }
}
";

    [Fact]
    public async Task ListSymbolsAsyncThrowsWorkspaceNotLoadedExceptionForAnEmptyQueryWhenSnapshotIsNotLoaded()
    {
        var provider = new RoslynLanguageProvider(RoslynWorkspaceSnapshot.NotLoaded());
        var emptyQuery = new ListSymbolsQuery(null, null, null, includeGenerated: false, maxResults: 50);

        await Assert.ThrowsAsync<WorkspaceNotLoadedException>(
            () => provider.ListSymbolsAsync(emptyQuery, CancellationToken.None));
    }

    [Fact]
    public async Task ListSymbolsAsyncThrowsWorkspaceNotLoadedExceptionBeforeResolvingAnyPathWhenSnapshotIsNotLoaded()
    {
        var provider = new RoslynLanguageProvider(RoslynWorkspaceSnapshot.NotLoaded());
        var query = new ListSymbolsQuery("a/Foo.cs", null, null, includeGenerated: false, maxResults: 50);

        await Assert.ThrowsAsync<WorkspaceNotLoadedException>(
            () => provider.ListSymbolsAsync(query, CancellationToken.None));
    }

    [Fact]
    public async Task ListSymbolsAsyncThrowsSourceFileNotFoundExceptionForAWellFormedPathWithNoMatchingDocument()
    {
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(
            ("a/Foo.cs", "namespace A { public class Foo { } }"),
            ("b/Bar.cs", "namespace B { public class Bar { } }"));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery("a/Missing.cs", null, null, includeGenerated: false, maxResults: 50);

        var exception = await Assert.ThrowsAsync<SourceFileNotFoundException>(
            () => provider.ListSymbolsAsync(query, CancellationToken.None));

        Assert.Equal("a/Missing.cs", exception.FilePath);
    }

    [Fact]
    public async Task ListSymbolsAsyncScopesToADirectoryMatchingOnlyItsPathSegmentSubtree()
    {
        // Scenario 2 — path-segment boundary match: "a" must match "a/Foo.cs" and
        // "a/Sub/Bar.cs", but must NOT match "abc/Nope.cs" (raw string-prefix would wrongly
        // match) or "b/Baz.cs" (a sibling directory).
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(
            ("a/Foo.cs", "namespace A { public class Foo { } }"),
            ("a/Sub/Bar.cs", "namespace A.Sub { public class Bar { } }"),
            ("b/Baz.cs", "namespace B { public class Baz { } }"),
            ("abc/Nope.cs", "namespace Abc { public class Nope { } }"));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery("a", null, null, includeGenerated: false, maxResults: 50);

        var result = await provider.ListSymbolsAsync(query, CancellationToken.None);

        Assert.Contains(result.Results, symbol => symbol.Name == "Foo");
        Assert.Contains(result.Results, symbol => symbol.Name == "Bar");
        Assert.DoesNotContain(result.Results, symbol => symbol.Name == "Nope");
        Assert.DoesNotContain(result.Results, symbol => symbol.Name == "Baz");
    }

    [Fact]
    public async Task ListSymbolsAsyncScopesToTheWholeWorkspaceWhenPathIsNull()
    {
        // Scenario 3 — path omitted: results drawn from every in-scope document.
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(
            ("a/Foo.cs", "namespace A { public class Foo { } }"),
            ("a/Sub/Bar.cs", "namespace A.Sub { public class Bar { } }"),
            ("b/Baz.cs", "namespace B { public class Baz { } }"),
            ("abc/Nope.cs", "namespace Abc { public class Nope { } }"));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery(null, null, null, includeGenerated: false, maxResults: 50);

        var result = await provider.ListSymbolsAsync(query, CancellationToken.None);

        Assert.Contains(result.Results, symbol => symbol.Name == "Foo");
        Assert.Contains(result.Results, symbol => symbol.Name == "Bar");
        Assert.Contains(result.Results, symbol => symbol.Name == "Baz");
        Assert.Contains(result.Results, symbol => symbol.Name == "Nope");
    }

    [Fact]
    public async Task ListSymbolsAsyncExcludesBinObjNodeModulesAndGeneratedFilesByDefault()
    {
        // Scenario 6 — generated/excluded paths omitted by default.
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(
            ("a/Foo.cs", "namespace A { public class Foo { } }"),
            ("bin/Generated.cs", "namespace Gen { public class BinGenerated { } }"),
            ("obj/Generated.cs", "namespace Gen { public class ObjGenerated { } }"),
            ("node_modules/pkg/index.cs", "namespace Gen { public class NodeModulesGenerated { } }"),
            ("Model.g.cs", "namespace Gen { public class ModelGenerated { } }"));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery(null, null, null, includeGenerated: false, maxResults: 50);

        var result = await provider.ListSymbolsAsync(query, CancellationToken.None);

        Assert.Contains(result.Results, symbol => symbol.Name == "Foo");
        Assert.DoesNotContain(result.Results, symbol => symbol.Name == "BinGenerated");
        Assert.DoesNotContain(result.Results, symbol => symbol.Name == "ObjGenerated");
        Assert.DoesNotContain(result.Results, symbol => symbol.Name == "NodeModulesGenerated");
        Assert.DoesNotContain(result.Results, symbol => symbol.Name == "ModelGenerated");
    }

    [Fact]
    public async Task ListSymbolsAsyncIncludeGeneratedLiftsOnlyTheGCsExclusionNotBinObjNodeModules()
    {
        // Scenario 6 — include_generated=true lifts only the *.g.cs exclusion; bin/, obj/, and
        // node_modules/ are still excluded (CLAUDE.md "Exclude bin/, obj/, *.g.cs,
        // node_modules/" — these are a server-side filter, not something include_generated is
        // defined to override for build-output directories).
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(
            ("a/Foo.cs", "namespace A { public class Foo { } }"),
            ("bin/Generated.cs", "namespace Gen { public class BinGenerated { } }"),
            ("obj/Generated.cs", "namespace Gen { public class ObjGenerated { } }"),
            ("node_modules/pkg/index.cs", "namespace Gen { public class NodeModulesGenerated { } }"),
            ("Model.g.cs", "namespace Gen { public class ModelGenerated { } }"));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery(null, null, null, includeGenerated: true, maxResults: 50);

        var result = await provider.ListSymbolsAsync(query, CancellationToken.None);

        Assert.Contains(result.Results, symbol => symbol.Name == "Foo");
        Assert.Contains(result.Results, symbol => symbol.Name == "ModelGenerated");
        Assert.DoesNotContain(result.Results, symbol => symbol.Name == "BinGenerated");
        Assert.DoesNotContain(result.Results, symbol => symbol.Name == "ObjGenerated");
        Assert.DoesNotContain(result.Results, symbol => symbol.Name == "NodeModulesGenerated");
    }

    [Fact]
    public async Task ListSymbolsAsyncThrowsSourceFileNotFoundWhenPathTargetsAnExcludedFileDirectly()
    {
        // Scenario 6 applies regardless of how the scope was selected (batch-review SUGGEST
        // follow-up on FEAT-GW-5): a `path` that resolves to an exact document under bin/, obj/,
        // node_modules/, or a *.g.cs file is still a server-side-filtered path and must not be
        // surfaced just because the caller named it directly.
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(
            ("a/Foo.cs", "namespace A { public class Foo { } }"),
            ("bin/Generated.cs", "namespace Gen { public class BinGenerated { } }"),
            ("Model.g.cs", "namespace Gen { public class ModelGenerated { } }"));
        var provider = new RoslynLanguageProvider(snapshot);

        var binQuery = new ListSymbolsQuery("bin/Generated.cs", null, null, includeGenerated: false, maxResults: 50);
        var binException = await Assert.ThrowsAsync<SourceFileNotFoundException>(
            () => provider.ListSymbolsAsync(binQuery, CancellationToken.None));
        Assert.Equal("bin/Generated.cs", binException.FilePath);

        var generatedQuery = new ListSymbolsQuery("Model.g.cs", null, null, includeGenerated: false, maxResults: 50);
        await Assert.ThrowsAsync<SourceFileNotFoundException>(
            () => provider.ListSymbolsAsync(generatedQuery, CancellationToken.None));

        // include_generated=true lifts only the *.g.cs exclusion — the direct *.g.cs path now
        // resolves, but the direct bin/ path still does not (bin/obj/node_modules is
        // unconditional).
        var generatedIncludedQuery = new ListSymbolsQuery(
            "Model.g.cs", null, null, includeGenerated: true, maxResults: 50);
        var includedResult = await provider.ListSymbolsAsync(generatedIncludedQuery, CancellationToken.None);
        Assert.Contains(includedResult.Results, symbol => symbol.Name == "ModelGenerated");

        var binIncludedQuery = new ListSymbolsQuery("bin/Generated.cs", null, null, includeGenerated: true, maxResults: 50);
        await Assert.ThrowsAsync<SourceFileNotFoundException>(
            () => provider.ListSymbolsAsync(binIncludedQuery, CancellationToken.None));
    }

    [Fact]
    public async Task ListSymbolsAsyncExtractsTheNamespaceAndClassDeclaredInAResolvedDocument()
    {
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(
            ("a/Foo.cs", "namespace A { public class Foo { } }"),
            ("b/Bar.cs", "namespace B { public class Bar { } }"));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery("a/Foo.cs", null, null, includeGenerated: false, maxResults: 50);

        var result = await provider.ListSymbolsAsync(query, CancellationToken.None);

        Assert.Equal(2, result.Results.Count);
        Assert.Contains(result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Namespace && symbol.Name == "A");
        Assert.Contains(result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Class && symbol.Name == "Foo");
    }

    [Fact]
    public async Task ListSymbolsAsyncExtractsExactlyFourEntriesFromTheOrderServiceFixture()
    {
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/OrderService.cs", ORDER_SERVICE_SOURCE));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery("a/OrderService.cs", null, null, includeGenerated: false, maxResults: 50);

        var result = await provider.ListSymbolsAsync(query, CancellationToken.None);

        Assert.Equal(4, result.Results.Count);

        // GetDeclaredSymbol on a dotted `namespace Acme.Orders { }` declaration returns the
        // innermost namespace symbol ("Orders"), whose ContainingSymbol is "Acme" (not the
        // global namespace) — confirmed against the actual Roslyn 4.14.0 output below.
        var namespaceSymbol = Assert.Single(result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Namespace);
        Assert.Equal("Orders", namespaceSymbol.Name);
        Assert.Equal("a/OrderService.cs", namespaceSymbol.File);
        Assert.Equal(4, namespaceSymbol.Line);
        Assert.Equal("Acme.Orders", namespaceSymbol.Signature);
        Assert.Equal("Acme", namespaceSymbol.Container);
        Assert.Equal("not_applicable", namespaceSymbol.Accessibility);

        var classSymbol = Assert.Single(result.Results, symbol => symbol.Name == "OrderService");
        Assert.Equal(DeclaredSymbolKind.Class, classSymbol.Kind);
        Assert.Equal("a/OrderService.cs", classSymbol.File);
        Assert.Equal(6, classSymbol.Line);
        Assert.Equal("Acme.Orders.OrderService", classSymbol.Signature);
        Assert.Equal("Acme.Orders", classSymbol.Container);
        Assert.Equal("public", classSymbol.Accessibility);

        var fieldSymbol = Assert.Single(result.Results, symbol => symbol.Name == "_count");
        Assert.Equal(DeclaredSymbolKind.Field, fieldSymbol.Kind);
        Assert.Equal("a/OrderService.cs", fieldSymbol.File);
        Assert.Equal(8, fieldSymbol.Line);
        Assert.Equal("Acme.Orders.OrderService._count", fieldSymbol.Signature);
        Assert.Equal("Acme.Orders.OrderService", fieldSymbol.Container);
        Assert.Equal("private", fieldSymbol.Accessibility);

        var methodSymbol = Assert.Single(result.Results, symbol => symbol.Name == "GetTotalAsync");
        Assert.Equal(DeclaredSymbolKind.Method, methodSymbol.Kind);
        Assert.Equal("a/OrderService.cs", methodSymbol.File);
        Assert.Equal(10, methodSymbol.Line);
        Assert.Equal("Acme.Orders.OrderService.GetTotalAsync(int)", methodSymbol.Signature);
        Assert.Equal("Acme.Orders.OrderService", methodSymbol.Container);
        Assert.Equal("public", methodSymbol.Accessibility);
    }

    // Line 1: using System;
    // Line 2: (blank)
    // Line 3: namespace Acme.Various
    // Line 4: {
    // Line 5:     public record class Widget(int Id);
    // Line 6: (blank)
    // Line 7:     public record struct Point(int X, int Y);
    // Line 8: (blank)
    // Line 9:     public struct Coordinates
    // Line 10:    {
    // Line 11:        public int Width;
    // Line 12:    }
    // Line 13: (blank)
    // Line 14:    public interface IWidget
    // Line 15:    {
    // Line 16:        void Render();
    // Line 17:    }
    // Line 18: (blank)
    // Line 19:    public enum Status
    // Line 20:    {
    // Line 21:        Active,
    // Line 22:        Inactive
    // Line 23:    }
    // Line 24: (blank)
    // Line 25:    public class WidgetFactory
    // Line 26:    {
    // Line 27:        static WidgetFactory()
    // Line 28:        {
    // Line 29:        }
    // Line 30: (blank)
    // Line 31:        public WidgetFactory()
    // Line 32:        {
    // Line 33:        }
    // Line 34: (blank)
    // Line 35:        public int Count { get; set; }
    // Line 36: (blank)
    // Line 37:        public event EventHandler? Changed;
    // Line 38:    }
    // Line 39: (blank)
    // Line 40:    public delegate void WidgetHandler(object sender);
    // Line 41: }
    private const string VARIOUS_DECLARATIONS_SOURCE = @"using System;

namespace Acme.Various
{
    public record class Widget(int Id);

    public record struct Point(int X, int Y);

    public struct Coordinates
    {
        public int Width;
    }

    public interface IWidget
    {
        void Render();
    }

    public enum Status
    {
        Active,
        Inactive
    }

    public class WidgetFactory
    {
        static WidgetFactory()
        {
        }

        public WidgetFactory()
        {
        }

        public int Count { get; set; }

        public event EventHandler? Changed;
    }

    public delegate void WidgetHandler(object sender);
}
";

    [Fact]
    public async Task ListSymbolsAsyncClassifiesRecordsStructsInterfacesEnumsConstructorsPropertiesAndFieldFormEvents()
    {
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/Various.cs", VARIOUS_DECLARATIONS_SOURCE));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery("a/Various.cs", null, null, includeGenerated: false, maxResults: 50);

        var result = await provider.ListSymbolsAsync(query, CancellationToken.None);

        // record class -> Class, with no special-casing.
        Assert.Contains(result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Class && symbol.Name == "Widget");

        // record struct -> Struct, with no special-casing.
        Assert.Contains(result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Struct && symbol.Name == "Point");

        // Plain struct -> Struct, plus its explicit field.
        Assert.Contains(
            result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Struct && symbol.Name == "Coordinates");
        Assert.Contains(result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Field && symbol.Name == "Width");

        // Interface, plus its method member.
        Assert.Contains(
            result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Interface && symbol.Name == "IWidget");
        Assert.Contains(result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Method && symbol.Name == "Render");

        // Enum, plus enum members appearing as Field entries.
        Assert.Contains(result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Enum && symbol.Name == "Status");
        Assert.Contains(result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Field && symbol.Name == "Active");
        Assert.Contains(result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Field && symbol.Name == "Inactive");

        // Both constructor kinds appear as one Constructor entry each.
        var constructors = result.Results.Where(symbol => symbol.Kind == DeclaredSymbolKind.Constructor).ToList();
        Assert.Equal(2, constructors.Count);
        Assert.Contains(constructors, symbol => symbol.Name == ".cctor");
        Assert.Contains(constructors, symbol => symbol.Name == ".ctor");

        // Property get/set accessors do NOT appear as separate entries — only the property itself.
        Assert.Single(result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Property && symbol.Name == "Count");
        Assert.DoesNotContain(
            result.Results, symbol => symbol.Name.Contains("Count", System.StringComparison.Ordinal)
                && symbol.Kind == DeclaredSymbolKind.Method);

        // Field-form event appears as exactly one Event entry (EventFieldDeclarationSyntax carve-out).
        Assert.Single(result.Results, symbol => symbol.Kind == DeclaredSymbolKind.Event && symbol.Name == "Changed");

        // Delegate declaration does not appear in results at all.
        Assert.DoesNotContain(result.Results, symbol => symbol.Name == "WidgetHandler");
    }

    private const string ORDER_NAMES_SOURCE = @"namespace Acme.Billing
{
    public class OrderService { }
    public class OrderRepository { }
    public class PaymentGateway { }
}
";

    [Fact]
    public async Task ListSymbolsAsyncNameFilterMatchesByCaseInsensitiveSubstring()
    {
        // Scenario 4 (substring half) — "order" (mixed case) matches "OrderService" and
        // "OrderRepository" but not "PaymentGateway".
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/Orders.cs", ORDER_NAMES_SOURCE));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery(null, "OrDeR", null, includeGenerated: false, maxResults: 50);

        var result = await provider.ListSymbolsAsync(query, CancellationToken.None);

        Assert.Contains(result.Results, symbol => symbol.Name == "OrderService");
        Assert.Contains(result.Results, symbol => symbol.Name == "OrderRepository");
        Assert.DoesNotContain(result.Results, symbol => symbol.Name == "PaymentGateway");
    }

    [Fact]
    public async Task ListSymbolsAsyncNameFilterMatchesExactNameCaseInsensitively()
    {
        // Scenario 4 (exact half) — "OrderService" (any case) matches exactly the symbol(s)
        // named "OrderService", case-insensitively.
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/Orders.cs", ORDER_NAMES_SOURCE));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery(null, "orderservice", null, includeGenerated: false, maxResults: 50);

        var result = await provider.ListSymbolsAsync(query, CancellationToken.None);

        var symbol = Assert.Single(result.Results);
        Assert.Equal("OrderService", symbol.Name);
    }

    private const string MIXED_KINDS_SOURCE = @"namespace Acme.Mixed
{
    public interface IWidget
    {
        void Render();
    }

    public class Widget : IWidget
    {
        private int _field;

        public void Render()
        {
        }
    }
}
";

    [Fact]
    public async Task ListSymbolsAsyncKindsFiltersToOnlyTheRequestedDeclarationKind()
    {
        // Scenario 5 — kinds = [Method] returns only Method entries; no class, interface, or
        // field entry is present.
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/Mixed.cs", MIXED_KINDS_SOURCE));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery(
            null, null, new[] { DeclaredSymbolKind.Method }, includeGenerated: false, maxResults: 50);

        var result = await provider.ListSymbolsAsync(query, CancellationToken.None);

        Assert.NotEmpty(result.Results);
        Assert.All(result.Results, symbol => Assert.Equal(DeclaredSymbolKind.Method, symbol.Kind));
    }

    [Fact]
    public async Task ListSymbolsAsyncCapsResultsAtMaxResultsAndSignalsTruncation()
    {
        // Scenario 7 — 60 matching symbols across multiple files, MaxResults = 50: at most 50
        // entries, Truncated == true, TotalFound == 60.
        var documents = Enumerable.Range(1, 60)
            .Select(index => ($"gen/File{index}.cs", $"public class GenClass{index} {{ }}"))
            .ToArray();
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(documents);
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery(null, null, null, includeGenerated: false, maxResults: 50);

        var result = await provider.ListSymbolsAsync(query, CancellationToken.None);

        Assert.Equal(50, result.Results.Count);
        Assert.True(result.Truncated);
        Assert.Equal(60, result.TotalFound);
    }

    [Fact]
    public async Task ListSymbolsAsyncReturnsEmptyResultsForANameFilterThatMatchesNothing()
    {
        // Scenario 8 — zero matches is a valid, honest result: no error, empty Results,
        // TotalFound 0, Truncated false.
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/Foo.cs", "public class Foo { }"));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery(
            null, "no-such-symbol-exists", null, includeGenerated: false, maxResults: 50);

        var result = await provider.ListSymbolsAsync(query, CancellationToken.None);

        Assert.Empty(result.Results);
        Assert.Equal(0, result.TotalFound);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task ListSymbolsAsyncOrdersResultsByFileThenLineDeterministicallyAcrossRepeatedCalls()
    {
        // Scenario 9 — ordered by file ascending (StringComparer.Ordinal), then line ascending
        // within each file; repeated calls with identical inputs return the same order.
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(
            ("z/Zeta.cs", "public class Z1 { }\npublic class Z2 { }"),
            ("a/Alpha.cs", "public class A1 { }"),
            ("m/Mid.cs", "public class M1 { }\npublic class M2 { }"));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new ListSymbolsQuery(null, null, null, includeGenerated: false, maxResults: 50);

        var expectedOrder = new[] { "A1", "M1", "M2", "Z1", "Z2" };

        var firstCall = await provider.ListSymbolsAsync(query, CancellationToken.None);
        var secondCall = await provider.ListSymbolsAsync(query, CancellationToken.None);

        Assert.Equal(expectedOrder, firstCall.Results.Select(symbol => symbol.Name).ToArray());
        Assert.Equal(expectedOrder, secondCall.Results.Select(symbol => symbol.Name).ToArray());
    }

    [Fact]
    public async Task GetFileAsyncThrowsWorkspaceNotLoadedExceptionForAnyArgumentShapeWhenSnapshotIsNotLoaded()
    {
        // Scenario 14 — workspace-not-ready is checked before any other work, for any argument
        // shape (whole-file, explicit-range, or bounded-snippet), mirroring ListSymbolsAsync.
        var provider = new RoslynLanguageProvider(RoslynWorkspaceSnapshot.NotLoaded());
        var wholeFileQuery = new GetFileQuery("a/Foo.cs", null, null, null, context: 2);
        var rangeQuery = new GetFileQuery("a/Foo.cs", 3, 6, null, context: 2);
        var snippetQuery = new GetFileQuery("a/Foo.cs", null, null, 10, context: 2);

        await Assert.ThrowsAsync<WorkspaceNotLoadedException>(
            () => provider.GetFileAsync(wholeFileQuery, CancellationToken.None));
        await Assert.ThrowsAsync<WorkspaceNotLoadedException>(
            () => provider.GetFileAsync(rangeQuery, CancellationToken.None));
        await Assert.ThrowsAsync<WorkspaceNotLoadedException>(
            () => provider.GetFileAsync(snippetQuery, CancellationToken.None));
    }

    [Fact]
    public async Task GetFileAsyncThrowsSourceFileNotFoundExceptionForAWellFormedPathWithNoMatchingDocument()
    {
        // Scenario 13 — well-formed path that does not exist in the workspace.
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(
            ("a/Foo.cs", "namespace A { public class Foo { } }"),
            ("b/Bar.cs", "namespace B { public class Bar { } }"));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new GetFileQuery("a/Missing.cs", null, null, null, context: 2);

        var exception = await Assert.ThrowsAsync<SourceFileNotFoundException>(
            () => provider.GetFileAsync(query, CancellationToken.None));

        Assert.Equal("a/Missing.cs", exception.FilePath);
    }

    [Fact]
    public async Task GetFileAsyncReturnsTheWholeFileWhenNoRangeOrAroundArgumentIsPresent()
    {
        // Scenario 1 — whole file: start_line 1, end_line/total_lines equal to the fixture's
        // actual compiled line count (via SourceText.Lines.Count, not a hand-computed count),
        // and content equal to the fixture's exact source text end-to-end.
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/OrderService.cs", ORDER_SERVICE_SOURCE));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new GetFileQuery("a/OrderService.cs", null, null, null, context: 2);

        var result = await provider.GetFileAsync(query, CancellationToken.None);

        var expectedTotalLines = SourceText.From(ORDER_SERVICE_SOURCE).Lines.Count;

        Assert.Equal("a/OrderService.cs", result.File);
        Assert.Equal(1, result.StartLine);
        Assert.Equal(expectedTotalLines, result.EndLine);
        Assert.Equal(expectedTotalLines, result.TotalLines);
        Assert.Equal(ORDER_SERVICE_SOURCE, result.Content);
    }

    [Fact]
    public async Task GetFileAsyncReturnsExactlyTheRequestedRangeForAnInBoundsStartAndEndLine()
    {
        // Scenario 2 — explicit start_line 3 / end_line 6, asserted against the fixture's
        // actual compiled text via 01-plan.md Decision D1's slicing rule.
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/OrderService.cs", ORDER_SERVICE_SOURCE));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new GetFileQuery("a/OrderService.cs", 3, 6, null, context: 2);

        var result = await provider.GetFileAsync(query, CancellationToken.None);

        var sourceText = SourceText.From(ORDER_SERVICE_SOURCE);
        var expectedSpan = TextSpan.FromBounds(sourceText.Lines[2].Start, sourceText.Lines[5].End);
        var expectedContent = sourceText.GetSubText(expectedSpan).ToString();

        Assert.Equal("a/OrderService.cs", result.File);
        Assert.Equal(3, result.StartLine);
        Assert.Equal(6, result.EndLine);
        Assert.Equal(expectedContent, result.Content);
    }

    [Fact]
    public async Task GetFileAsyncClampsEndLineToTotalLinesWhenOnlyEndLineOverflowsButStartLineIsInBounds()
    {
        // Scenario 9 — end_line greater than total_lines, start_line still in bounds: clamp
        // end_line down to total_lines rather than reject (symmetry with
        // ListSymbolsTool.ValidateOptionalMaxResults's clamp-not-reject precedent).
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/OrderService.cs", ORDER_SERVICE_SOURCE));
        var provider = new RoslynLanguageProvider(snapshot);
        var totalLines = SourceText.From(ORDER_SERVICE_SOURCE).Lines.Count;
        var query = new GetFileQuery("a/OrderService.cs", 3, totalLines + 100, null, context: 2);

        var result = await provider.GetFileAsync(query, CancellationToken.None);

        var sourceText = SourceText.From(ORDER_SERVICE_SOURCE);
        var expectedSpan = TextSpan.FromBounds(sourceText.Lines[2].Start, sourceText.Lines[totalLines - 1].End);
        var expectedContent = sourceText.GetSubText(expectedSpan).ToString();

        Assert.Equal(3, result.StartLine);
        Assert.Equal(totalLines, result.EndLine);
        Assert.Equal(expectedContent, result.Content);
    }

    [Fact]
    public async Task GetFileAsyncThrowsInvalidToolArgumentExceptionWhenStartLineItselfIsBeyondTotalLines()
    {
        // Scenario 9a — start_line itself beyond total_lines: the entire requested range names
        // no real line at all, so it is rejected rather than clamped (contrast Scenario 9 above).
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/OrderService.cs", ORDER_SERVICE_SOURCE));
        var provider = new RoslynLanguageProvider(snapshot);
        var totalLines = SourceText.From(ORDER_SERVICE_SOURCE).Lines.Count;
        var query = new GetFileQuery("a/OrderService.cs", totalLines + 1, totalLines + 5, null, context: 2);

        var exception = await Assert.ThrowsAsync<InvalidToolArgumentException>(
            () => provider.GetFileAsync(query, CancellationToken.None));

        Assert.Equal("start_line", exception.ArgumentName);
    }

    // Line 1: using System;
    // Line 2: (blank)
    // Line 3: namespace Acme.Snippets
    // Line 4: {
    // Line 5:     public class SnippetHost
    // Line 6:     {
    // Line 7:         public void Run() {
    // Line 8:             var a = 1;
    // Line 9:             var b = 2;
    // Line 10:            var c = 3;
    // Line 11:            var d = 4;
    // Line 12:            var e = 5;
    // Line 13:            var f = 6;
    // Line 14:            var g = 7;
    // Line 15:            var h = 8;
    // Line 16:            var i = 9;
    // Line 17:            var j = 10;
    // Line 18:        }
    // Line 19:    }
    // Line 20: }
    private const string SNIPPET_SOURCE = @"using System;

namespace Acme.Snippets
{
    public class SnippetHost
    {
        public void Run() {
            var a = 1;
            var b = 2;
            var c = 3;
            var d = 4;
            var e = 5;
            var f = 6;
            var g = 7;
            var h = 8;
            var i = 9;
            var j = 10;
        }
    }
}
";

    [Fact]
    public async Task GetFileAsyncReturnsTheDefaultFiveLineWindowForABoundedSnippetOfSimpleStatements()
    {
        // Scenario 3 — bounded snippet with default context: lines 8-12 are each a single
        // simple statement, so this task's raw clamped window is exactly the served window
        // (SyntaxTree boundary-snapping is T06, layered on top later, not implemented here).
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/Snippet.cs", SNIPPET_SOURCE));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new GetFileQuery("a/Snippet.cs", null, null, 10, context: 2);

        var result = await provider.GetFileAsync(query, CancellationToken.None);

        Assert.Equal(8, result.StartLine);
        Assert.Equal(12, result.EndLine);
    }

    [Fact]
    public async Task GetFileAsyncReturnsAnElevenLineWindowForABoundedSnippetWithExplicitContextFive()
    {
        // Scenario 4 — bounded snippet with explicit context 5: an 11-line window.
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/Snippet.cs", SNIPPET_SOURCE));
        var provider = new RoslynLanguageProvider(snapshot);
        var query = new GetFileQuery("a/Snippet.cs", null, null, 10, context: 5);

        var result = await provider.GetFileAsync(query, CancellationToken.None);

        Assert.Equal(5, result.StartLine);
        Assert.Equal(15, result.EndLine);
    }

    [Fact]
    public async Task GetFileAsyncClampsTheBoundedSnippetWindowAtTheStartAndEndOfFileWithoutArtificialPadding()
    {
        // Scenario 10 — around_line 1 (and around_line N) with default context: the window
        // clamps at the file's edges rather than being padded out to a fixed line count on the
        // side that runs past the edge.
        var snapshot = RoslynWorkspaceTestFixtures.Instance.BuildSnapshot(("a/Snippet.cs", SNIPPET_SOURCE));
        var provider = new RoslynLanguageProvider(snapshot);
        var totalLines = SourceText.From(SNIPPET_SOURCE).Lines.Count;

        var startQuery = new GetFileQuery("a/Snippet.cs", null, null, 1, context: 2);
        var startResult = await provider.GetFileAsync(startQuery, CancellationToken.None);

        Assert.Equal(1, startResult.StartLine);
        Assert.Equal(3, startResult.EndLine);

        var endQuery = new GetFileQuery("a/Snippet.cs", null, null, totalLines, context: 2);
        var endResult = await provider.GetFileAsync(endQuery, CancellationToken.None);

        Assert.Equal(totalLines - 2, endResult.StartLine);
        Assert.Equal(totalLines, endResult.EndLine);
    }
}
