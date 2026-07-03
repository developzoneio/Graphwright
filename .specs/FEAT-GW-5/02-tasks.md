---
id: FEAT-GW-5
type: feature
phase: tasks
created: 2026-07-02
plan: .specs/FEAT-GW-5/01-plan.md
---

# FEAT-GW-5 — Task list

Conventions binding on EVERY task (from CLAUDE.md + `.specs/FEAT-GW-4` precedent): `== false` /
`== true` comparisons (never bare `!expr`); custom domain exceptions only, never a new exception
type without a plan decision; `Async` suffix + `CancellationToken ct` as last parameter on all
async methods; `#nullable enable` (inherited from `Directory.Build.props`); no static classes with
static methods (use `static readonly` instance, mirroring `ExceptionEnvelopeMapper` /
`InfrastructureModule` / `McpServerModule`); `if` always braced; 4-space indent; max 120-char
lines; comments on their own line, uppercase start, English. Test files live under
`tests/Graphwright.Tests/` mirroring the source folder structure. `Microsoft.CodeAnalysis.*` may
only appear in files under `src/Graphwright.Infrastructure/` or their direct test counterparts —
never in `src/Graphwright.Application/` or `src/Graphwright.McpServer/`.

## Checklist

- [x] T01 — `ILanguageProvider` + DTOs (Application)
- [x] T02 — `SymbolKindWireMapper` (McpServer)
- [x] T03 — `RoslynWorkspaceSnapshot` + in-memory `AdhocWorkspace` test-fixture builder (Infrastructure)
- [x] T04 — `RoslynLanguageProvider`: scope resolution + WorkspaceNotLoaded + FileNotFound
- [x] T05 — `RoslynLanguageProvider`: symbol extraction core (single file)
- [x] T06 — `RoslynLanguageProvider`: directory/workspace scoping + generated-path exclusion
- [x] T07 — `RoslynLanguageProvider`: name_filter, kinds filter, ordering, cap/truncation
- [x] T08 — `ListSymbolsTool`: new schema, validation, `ILanguageProvider` wiring
- [x] T09 — `InfrastructureModule` registers the real (unloaded) `ILanguageProvider`
- [x] T10 — Fix `McpServerModuleTests` composition helper
- [x] T11 — Fix `ToolDispatcherTests` (swap `ListSymbolsTool` for `GetFileTool`)
- [x] T12 — Fix `ToolRegistryTests` (add a never-invoked `ILanguageProvider` fake)
- [x] T13 — Split `StubToolTests` (drop the `ListSymbolsTool` row)
- [x] T14 — End-to-end WORKSPACE_NOT_LOADED test + Scenario-12 structural proxy
- [x] T15 — Create `mcp-contract.md` with `list_symbols` as its first entry

---

### T01 - ILanguageProvider + DTOs (Application)
- Files: src/Graphwright.Application/LanguageProviders/ILanguageProvider.cs; src/Graphwright.Application/LanguageProviders/DeclaredSymbolKind.cs; src/Graphwright.Application/LanguageProviders/DeclaredSymbol.cs; src/Graphwright.Application/LanguageProviders/ListSymbolsQuery.cs; src/Graphwright.Application/LanguageProviders/SymbolListResult.cs; tests/Graphwright.Tests/Application/LanguageProviders/DeclaredSymbolKindTests.cs
- Layer: Application
- Step type: foundation
- Test: tests/Graphwright.Tests/Application/LanguageProviders/DeclaredSymbolKindTests.cs — asserts the enum contains exactly the 10 names `Namespace, Class, Interface, Struct, Enum, Method, Property, Field, Event, Constructor` and nothing else (mirrors GW-4's `GraphwrightErrorCodeTests` closed-set pattern)
- Acceptance: `ILanguageProvider` declares `Task<SymbolListResult> ListSymbolsAsync(ListSymbolsQuery query, CancellationToken ct)` only; `ListSymbolsQuery` is an immutable DTO with `string? Path`, `string? NameFilter`, `IReadOnlyList<DeclaredSymbolKind>? Kinds`, `bool IncludeGenerated`, `int MaxResults` (caller — T08 — guarantees this is already in the 1-50 range before calling); `SymbolListResult` exposes `IReadOnlyList<DeclaredSymbol> Results`, `bool Truncated`, `int TotalFound`; `DeclaredSymbol` exposes `string Name`, `DeclaredSymbolKind Kind`, `string File`, `int Line`, `string Signature`, `string Container`, `string Accessibility`; zero `Microsoft.CodeAnalysis.*` references anywhere under `src/Graphwright.Application/`; `#nullable enable`; test green
- Depends on: none
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: src/Graphwright.Domain/Exceptions/GraphwrightException.cs:1-34 — XML-doc density and one-type-per-file style to mirror for these new Application types; src/Graphwright.McpServer/Tools/IGitnexusTool.cs:22-47 — interface doc-comment convention (describe each member's contract and the DIP boundary it sits on)

### T02 - SymbolKindWireMapper (McpServer)
- Files: src/Graphwright.McpServer/Contracts/SymbolKindWireMapper.cs; tests/Graphwright.Tests/McpServer/Contracts/SymbolKindWireMapperTests.cs
- Layer: Presentation
- Step type: foundation
- Test: tests/Graphwright.Tests/McpServer/Contracts/SymbolKindWireMapperTests.cs — each of the 10 `DeclaredSymbolKind` members round-trips to its exact lowercase wire literal (`namespace, class, interface, struct, enum, method, property, field, event, constructor`) and back; an unrecognized literal (`"delegate"`, `"Method"` wrong-case, `""`) makes `TryFromWireLiteral` return `false` rather than throwing
- Acceptance: sealed `SymbolKindWireMapper` (`public static readonly SymbolKindWireMapper Instance`, private ctor — mirrors `ExceptionEnvelopeMapper`); `string ToWireLiteral(DeclaredSymbolKind kind)` covers the full closed set via a switch with a defensive default that never falls through silently; `bool TryFromWireLiteral(string literal, out DeclaredSymbolKind kind)` is **case-sensitive** lowercase-exact match (the `kinds` vocabulary is lowercase literals, not case-insensitive like `name_filter` — deliberately different rules, stated in the XML doc comment); test green
- Depends on: T01
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: src/Graphwright.McpServer/Contracts/ExceptionEnvelopeMapper.cs:33-67 — mirror exactly: static readonly Instance, private ctor, switch-with-defensive-default method shape; this task adds the wire-to-enum inverse direction `ExceptionEnvelopeMapper` does not need

### T03 - RoslynWorkspaceSnapshot + in-memory AdhocWorkspace test-fixture builder
- Files: src/Graphwright.Infrastructure/LanguageProviders/RoslynWorkspaceSnapshot.cs; tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynWorkspaceTestFixtures.cs; tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynWorkspaceSnapshotTests.cs
- Layer: Infrastructure
- Step type: foundation
- Test: tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynWorkspaceSnapshotTests.cs — `RoslynWorkspaceSnapshot.NotLoaded()` has `IsLoaded == false` and an empty `Solution`; a snapshot built via `RoslynWorkspaceTestFixtures` with one or more `(relativePath, sourceText)` pairs has `IsLoaded == true` and `Solution.Projects` contains exactly one project whose documents' `FilePath`s equal the supplied relative paths exactly; **substrate smoke test (required)**: for a fixture document, `document.GetSemanticModelAsync()` returns non-null, AND `semanticModel.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task")` returns non-null — proving the project's metadata references actually resolve real BCL types rather than silently producing error types (this is the one assertion that would catch a broken `ProjectInfo`/reference-list setup before T04–T07 build on top of it and fail confusingly far from the real cause)
- Acceptance: `RoslynWorkspaceSnapshot` (sealed, immutable) exposes `bool IsLoaded` and `Solution Solution`; `public static RoslynWorkspaceSnapshot NotLoaded()` returns an unloaded snapshot over `new AdhocWorkspace().CurrentSolution`; `RoslynWorkspaceTestFixtures` (test-only helper, lives under `tests/`, never `src/`) builds an `AdhocWorkspace`, adds one `ProjectInfo` with `LanguageNames.CSharp` and explicit `CSharpParseOptions`/`CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)` (an `AdhocWorkspace` project defaults to no language-specific options set — omitting these produces a project that cannot bind a real C# `SemanticModel`), whose `MetadataReferences` come from `((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(Path.PathSeparator)` mapped through `MetadataReference.CreateFromFile` (full running-runtime reference set, no new NuGet package, no hand-picked assembly list — so `ToDisplayString()` output on any fixture resolves real BCL types, not error types), and adds one `DocumentInfo` per caller-supplied `(relativePath, sourceText)` pair with `FilePath` set to that exact relative path; test fixture paths in this task and every task that reuses this builder use unambiguous, non-colliding casing (e.g. `a/Foo.cs`, never two paths differing only by case) per `01-plan.md` OQ-3 sidestep; test green
- Depends on: none
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: none — first Roslyn Workspaces code in the repo (governing constraint: `Microsoft.CodeAnalysis.*` confined to `Graphwright.Infrastructure`, already true of the untouched csproj package references from GW-26); `01-plan.md` Decision D2/T03 risk row — the `TRUSTED_PLATFORM_ASSEMBLIES` technique is the pinned approach, do not substitute a hand-picked reference list

### T04 - RoslynLanguageProvider: scope resolution + WorkspaceNotLoaded + FileNotFound
- Files: src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs; tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs
- Layer: Infrastructure
- Step type: behavior
- Test: tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs — a provider built over `RoslynWorkspaceSnapshot.NotLoaded()` throws `WorkspaceNotLoadedException` for any query including an empty one (Scenario 10); a provider built over a fixture with documents `a/Foo.cs` and `b/Bar.cs`, queried with `Path = "a/Missing.cs"` (well-formed, in-tree, no matching document) throws `SourceFileNotFoundException` with `FilePath == "a/Missing.cs"` (Scenario 11a); the same loaded provider queried with `Path = null` or `Path = "a/Foo.cs"` does not throw and returns a `SymbolListResult` with `Results` empty (symbol extraction itself lands in T05 — this task proves resolution/exception behavior only)
- Acceptance: `RoslynLanguageProvider(RoslynWorkspaceSnapshot snapshot) : ILanguageProvider`; `ListSymbolsAsync` throws `WorkspaceNotLoadedException` first, before any other validation, when `snapshot.IsLoaded == false`; when loaded, resolves a non-null `query.Path` against `snapshot.Solution`'s documents by exact relative `FilePath` match (single-file case only — directory/whole-workspace resolution is T06); zero matching documents for a non-null `Path` throws `SourceFileNotFoundException(query.Path)`; `#nullable enable`, `Async` suffix, `CancellationToken ct` last parameter; test green
- Depends on: T01, T03
- Conflicts with: none
- Complexity: M
- Reversibility: moderate
- Pattern refs: src/Graphwright.Domain/Exceptions/WorkspaceNotLoadedException.cs:11-33 and SourceFileNotFoundException.cs:13-35 — raise these unchanged domain types, never a new exception type; CLAUDE.md cross-cutting rule 1 (`Location.GetLineSpan()`, 1-based) applies starting T05, not here

### T05 - RoslynLanguageProvider: symbol extraction core (single file)
- Files: src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs; tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs
- Layer: Infrastructure
- Step type: behavior
- Test: tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs — a fixture document declaring `namespace Acme.Orders { public class OrderService { private int _count; public Task<int> GetTotalAsync(int orderId) { throw new NotImplementedException(); } } }` at `a/OrderService.cs`, queried by that path, returns exactly 4 entries (namespace, class, field `_count`, method — property/event/constructor cases are covered by the second fixture below); for each entry: `Kind` matches the declaration; `File` equals `Location.GetLineSpan().Path` for that declaration (`"a/OrderService.cs"`) exactly — `file` and `line` MUST come from the same `Location.GetLineSpan()` call, never independently derived, so they cannot disagree; `Line` is that same call's 1-based line (assert against the actual line number in the fixture source, not an assumption); `Signature` is asserted against the **actual runtime output** of `symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)` (01-plan.md Decision D1 — if the literal the implementer first writes doesn't match, fix the test literal to match reality and record the confirmed string in `03-decisions.md`, the format itself does not change); `Container` is `symbol.ContainingSymbol?.ToDisplayString() ?? string.Empty`, asserted `""` for the namespace itself (global-namespace-contained) and the namespace's display name for the class/field/method; `Accessibility` follows the fixed table in `01-plan.md` OQ-6 (assert `"not_applicable"` for the namespace entry, `"public"` for the class and method, `"private"` for the field); a second fixture with a `record class`, a `record struct`, a `struct`, an `interface`, an `enum` (with at least one member), a `static` constructor, an instance constructor, a property with `get`/`set`, a **field-form event** (`public event EventHandler Changed;`), and a `delegate` declaration proves: records classify by `TypeKind` (Class/Struct) with no special-casing; enum members appear as `Field` entries; both constructor kinds appear as one `Constructor` entry each; property `get`/`set` accessor methods do NOT appear as separate entries (only the property declaration itself does); the field-form event appears as exactly one `Event` entry (proving the `EventFieldDeclarationSyntax` carve-out below, not a silently-dropped result); the `delegate` declaration does NOT appear in results at all
- Acceptance: extraction walks `MemberDeclarationSyntax` descendant nodes of the resolved document's syntax root (01-plan.md Decision D2), never a whole-`Compilation` symbol-tree walk; for every visited node EXCEPT `FieldDeclarationSyntax` and `EventFieldDeclarationSyntax`, call `semanticModel.GetDeclaredSymbol(node)` directly and skip a `null` result without dereferencing it; for `FieldDeclarationSyntax` and `EventFieldDeclarationSyntax` specifically, `GetDeclaredSymbol(node)` has no applicable overload and returns `null` by design (one declaration can name multiple variables, e.g. `public int x, y;`) — instead descend into `node.Declaration.Variables` and call `semanticModel.GetDeclaredSymbol(variableDeclaratorSyntax)` once per declarator, emitting one entry per resulting symbol (this is mandatory, not an edge case — a plain field or field-form event is dropped entirely without it, and Scenario 5's `field`-kind filtering in T07 would then have nothing to filter); classification is by the **returned symbol's** `Kind`/`TypeKind`/`MethodKind`, never the syntax node's keyword; `IMethodSymbol` with `MethodKind` in `{Ordinary}` -> `Method`; `MethodKind` in `{Constructor, StaticConstructor}` -> `Constructor`; `MethodKind` in `{PropertyGet, PropertySet, EventAdd, EventRemove, EventRaise, Destructor, Conversion, UserDefinedOperator}` -> not emitted; `INamedTypeSymbol` with `TypeKind.Delegate` -> not emitted (a code comment states this is a deliberate closed-vocabulary gap, not an oversight); `IPropertySymbol` (including indexers, `IsIndexer == true`) -> `Property`; `IFieldSymbol` (from a variable declarator, including enum members which bind directly on `EnumMemberDeclarationSyntax` with no carve-out needed) -> `Field`; `IEventSymbol` (from a variable declarator for the field form, or bound directly on `EventDeclarationSyntax` for the property-accessor form) -> `Event`; `INamespaceSymbol` -> `Namespace`; `#nullable enable` / `Async` / `CancellationToken ct` last maintained; test green
- Depends on: T04
- Conflicts with: T04 (same production file, sequential edit — do not parallelize)
- Complexity: L
- Reversibility: moderate
- Pattern refs: CLAUDE.md cross-cutting rule 1 — "`file:line` must be exact — always use `Location.GetLineSpan()`. Lines are 1-based."; 01-plan.md Decision D1/D2 — signature format and enumeration strategy are pinned there, implement exactly as written, do not re-derive

### T06 - RoslynLanguageProvider: directory/workspace scoping + generated-path exclusion
- Files: src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs; tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs
- Layer: Infrastructure
- Step type: behavior
- Test: tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs — fixture with documents `a/Foo.cs`, `a/Sub/Bar.cs`, `b/Baz.cs`, `abc/Nope.cs` (each declaring at least one distinct-named type): `Path = "a"` returns declared symbols from `a/Foo.cs` and `a/Sub/Bar.cs` only — critically, NOT from `abc/Nope.cs` (proves segment-boundary matching, not raw string-prefix matching) and NOT from `b/Baz.cs` (Scenario 2); `Path = null` returns declared symbols from all in-scope documents (Scenario 3); a fixture additionally containing documents at `bin/Generated.cs`, `obj/Generated.cs`, `node_modules/pkg/index.cs`, and `Model.g.cs` — with `IncludeGenerated` omitted/false, none of those four contribute any result; with `IncludeGenerated = true`, `Model.g.cs`'s symbols may appear but the `bin/`/`obj/`/`node_modules/` documents' symbols still do not (Scenario 6)
- Acceptance: directory scoping matches documents whose `FilePath` starts with `"{path}/"` (path-segment boundary — `"a"` must not match `"abc/Foo.cs"`) or equals `path` exactly (single-file case, already covered by T04, unaffected by this task); whole-workspace scoping (`Path` omitted) enumerates every non-excluded document in `snapshot.Solution`; a document is excluded by default when any path segment equals `bin`, `obj`, or `node_modules`, or the file name ends with `.g.cs`; `IncludeGenerated = true` lifts only the `.g.cs` exclusion — `bin`/`obj`/`node_modules` segment exclusion is unconditional regardless of `IncludeGenerated` (00-spec.md Scenario 6, CLAUDE.md "these are a server-side filter, not something include_generated is defined to override for build-output directories"); test green
- Depends on: T05
- Conflicts with: T04, T05 (same production file, sequential edit)
- Complexity: M
- Reversibility: moderate
- Pattern refs: 00-spec.md:119-131 — Scenario 6 gherkin is the exclusion-rule source of truth, copy its wording into the exclusion-check code comment rather than re-deriving it

### T07 - RoslynLanguageProvider: name_filter, kinds filter, ordering, cap/truncation
- Files: src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs; tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs
- Layer: Infrastructure
- Step type: behavior
- Test: tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs — fixture with symbols named `OrderService`, `OrderRepository`, `PaymentGateway`: `NameFilter = "order"` (mixed case) returns the first two, not the third (Scenario 4 substring); `NameFilter = "OrderService"` (any case) returns exactly the symbol(s) named `OrderService` case-insensitively (Scenario 4 exact); fixture with classes, interfaces, methods, and fields, `Kinds = [DeclaredSymbolKind.Method]` returns only `Method` entries (Scenario 5); a fixture producing 60 matching declared symbols across multiple files/lines, queried with `MaxResults = 50`, returns at most 50 entries, `Truncated == true`, `TotalFound == 60` (Scenario 7); a query matching nothing returns empty `Results`, `TotalFound == 0`, `Truncated == false`, and does not throw (Scenario 8); results spanning multiple files/lines are ordered by `File` ascending via `StringComparer.Ordinal` then `Line` ascending, identically across repeated calls with identical inputs and workspace state (Scenario 9)
- Acceptance: `NameFilter` matching is `symbol.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)` — this single rule satisfies both halves of Scenario 4 (an exact match is trivially a substring of itself); `Kinds` filtering is an allow-list intersection, `null`/omitted `Kinds` means no kind filtering; final pipeline order is: resolve scope (T04/T06) -> extract (T05) -> filter by generated/name/kind (T06/this task) -> order by `File` (`StringComparer.Ordinal`) then `Line` -> compute `TotalFound` from the full filtered set -> take `query.MaxResults` -> set `Truncated = TotalFound > Results.Count`; test green
- Depends on: T06
- Conflicts with: T04, T05, T06 (same production file, sequential edit)
- Complexity: M
- Reversibility: moderate
- Pattern refs: 00-spec.md:133-163 — Scenarios 7, 8, 9 gherkin; CLAUDE.md cross-cutting rules 5–7 (result cap, ordering, no invented data) are this task's acceptance source

### T08 - ListSymbolsTool: new schema, validation, ILanguageProvider wiring
- Files: src/Graphwright.McpServer/Tools/ListSymbolsTool.cs; tests/Graphwright.Tests/McpServer/Tools/ListSymbolsToolTests.cs
- Layer: Presentation
- Step type: behavior
- Test: tests/Graphwright.Tests/McpServer/Tools/ListSymbolsToolTests.cs (new file) — `InputSchema` is `type: object` with exactly the 5 properties `path, name_filter, kinds, include_generated, max_results` and an empty/absent `required` array; for each of the following, `ExecuteAsync` throws `InvalidToolArgumentException` naming the offending argument, AND a "poison" fake `ILanguageProvider` (whose `ListSymbolsAsync` throws `InvalidOperationException("must not be called")`) proves the provider is never invoked: `path`/`name_filter` present but non-string; `kinds` present but not an array of strings, or containing an unrecognized literal (e.g. `"Method"` wrong-case, `"delegate"`); `include_generated` present but non-boolean; `max_results` present but non-integer, or `<= 0`; `path` that is rooted/absolute or contains a `..` segment; empty arguments object `{}` is valid (no exception) — a capturing fake `ILanguageProvider` records the `ListSymbolsQuery` it received and asserts `Path == null, NameFilter == null, Kinds == null, IncludeGenerated == false, MaxResults == 50`; `max_results = 200` is valid and the captured query has `MaxResults == 50` (clamped, not rejected); a fake provider that throws `WorkspaceNotLoadedException` propagates unchanged out of `ExecuteAsync` (tool does not catch it); a fake provider that throws `SourceFileNotFoundException` propagates unchanged; a fake provider returning a canned `SymbolListResult` (>=2 results spanning at least a `namespace` and a `constructor` kind, plus `Truncated = true`, `TotalFound = 5`) maps to a `ToolSuccessEnvelope<JsonElement>` whose JSON exactly matches `{ "result": { "results": [{"name":...,"kind":...,"file":...,"line":...,"signature":...,"container":...,"accessibility":...}, ...], "truncated": true, "total_found": 5 } }` with `kind` values as their lowercase wire literals (via `SymbolKindWireMapper`)
- Acceptance: `public ListSymbolsTool(ILanguageProvider languageProvider)`, `ArgumentNullException.ThrowIfNull(languageProvider)`; `InputSchema` replaces the GW-4 placeholder with the real optional-only shape (`path?, name_filter?, kinds?, include_generated?, max_results?`, `kinds` items constrained to the 10-literal enum in the JSON schema itself); validation runs to completion — types, `path` traversal/rooted rejection, `kinds` vocabulary via `SymbolKindWireMapper.Instance.TryFromWireLiteral`, `max_results` range with clamp-above-50/reject-at-or-below-0 — entirely before `ILanguageProvider.ListSymbolsAsync` is ever called (this is also the "invalid args + unloaded workspace -> INVALID_ARGUMENT, not WORKSPACE_NOT_LOADED" precedence rule, proven by the poison-provider tests never reaching the provider); provider exceptions are never caught, only the mapped success path is tool-owned code; `#nullable enable` / `== false`/`== true` / `Async` / `CancellationToken ct` last maintained; test green
- Depends on: T01, T02
- Conflicts with: none
- Complexity: L
- Reversibility: moderate
- Pattern refs: src/Graphwright.McpServer/Tools/ListSymbolsTool.cs (current stub, this task rewrites it) — keep the `const string INPUT_SCHEMA_JSON` + cached-`JsonElement`-clone pattern; src/Graphwright.Domain/Exceptions/InvalidToolArgumentException.cs:14-19 — constructor shape (`argumentName, reason`) for every validation failure raised

### T09 - InfrastructureModule registers the real (unloaded) ILanguageProvider
- Files: src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs; tests/Graphwright.Tests/Infrastructure/DependencyInjection/InfrastructureModuleTests.cs
- Layer: Infrastructure
- Step type: wiring
- Test: tests/Graphwright.Tests/Infrastructure/DependencyInjection/InfrastructureModuleTests.cs (extend existing) — after `RegisterServices`, the built provider resolves `ILanguageProvider`; calling `ListSymbolsAsync` on the resolved instance with any query throws `WorkspaceNotLoadedException` (proves production wiring is deliberately unloaded — 01-plan.md Scope framing); existing "builds without throwing" assertion still passes
- Acceptance: `RegisterServices` registers `RoslynWorkspaceSnapshot.NotLoaded()` as a singleton and `services.AddSingleton<ILanguageProvider, RoslynLanguageProvider>()`; the prior "Implementations arrive with the later GW-1 stories" comment is replaced with one stating this is the first real registration and that the snapshot is deliberately unloaded pending a future indexer story (00-spec.md Out of scope, 01-plan.md Scope framing); `[SuppressMessage("Performance", "CA1822...")]` justification comment kept/updated only if still accurate; test green
- Depends on: T03, T04
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: src/Graphwright.McpServer/DependencyInjection/McpServerModule.cs:51-55 — mirror the `services.AddSingleton<TInterface, TImplementation>()` registration style

### T10 - Fix McpServerModuleTests composition helper
- Files: tests/Graphwright.Tests/McpServer/DependencyInjection/McpServerModuleTests.cs
- Layer: Presentation
- Step type: wiring
- Test: this task IS the test fix — `BuildRegisteredServices()` now calls `InfrastructureModule.Instance.RegisterServices(services)` before `McpServerModule.Instance.RegisterServices(services)`, mirroring `Program.cs`'s real registration order; all 6 existing facts in this file stay green, including `RegisterServicesResolvesReadOnlyListOfGitnexusToolsWithFiveEntries`, which previously resolved `ListSymbolsTool` with zero constructor dependencies and now requires `ILanguageProvider` to be present in the same container
- Acceptance: `BuildRegisteredServices()` calls both modules in `Program.cs` order; no other change to this file; all existing assertions pass unmodified; test green
- Depends on: T08, T09
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: src/Graphwright.McpServer/Program.cs (both `RegisterServices` calls, in order) — mirror the real composition-root call order exactly, not merely "an order that happens to compile"

### T11 - Fix ToolDispatcherTests (swap ListSymbolsTool for GetFileTool)
- Files: tests/Graphwright.Tests/McpServer/Dispatch/ToolDispatcherTests.cs
- Layer: Presentation
- Step type: wiring
- Test: this task IS the test fix — `DispatchAsyncReturnsInvalidArgumentEnvelopeForStubToolWithBadArguments` and `DispatchAsyncReturnsInternalEnvelopeWithNotImplementedMessageForStubToolWithValidArguments` (currently lines ~91-121) swap `new ListSymbolsTool()` for `new GetFileTool()` and its required argument `"path"` in place of `"file"`; both tests exercise generic `ToolDispatcher`/stub-boundary behavior unrelated to `list_symbols`, and `GetFileTool` remains an untouched single-required-argument not-implemented stub post-GW-5, so it is a like-for-like substitute; all other tests in this file (`FakeSuccessTool`/`FakeThrowingTool`/unknown-tool-name cases) are untouched
- Acceptance: file no longer references `ListSymbolsTool`; all facts in the file pass; test green
- Depends on: T08
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: src/Graphwright.McpServer/Tools/GetFileTool.cs:19-30 — required-argument name `"path"` and its stub validation shape, used only to know what valid/invalid `arguments` payloads look like for the substitute tool

### T12 - Fix ToolRegistryTests (add a never-invoked ILanguageProvider fake)
- Files: tests/Graphwright.Tests/McpServer/Registry/ToolRegistryTests.cs
- Layer: Presentation
- Step type: wiring
- Test: this task IS the test fix — `RealStubToolsOutOfOrder()` (currently line 46) constructs `new ListSymbolsTool(new NeverInvokedLanguageProvider())`, a small private nested fake implementing `ILanguageProvider` whose `ListSymbolsAsync` throws `InvalidOperationException` if ever called (these tests exercise registry name/ordering/contract mechanics only, never `ExecuteAsync`); all existing facts in this file pass unchanged
- Acceptance: file compiles against the new constructor; the new fake mirrors this file's own existing `FakeTool` private-nested-class convention; test green
- Depends on: T08
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: tests/Graphwright.Tests/McpServer/Registry/ToolRegistryTests.cs:15-36 (this file's own `FakeTool`) — mirror the private-nested-fake style for the new `ILanguageProvider` fake

### T13 - Split StubToolTests (drop the ListSymbolsTool row)
- Files: tests/Graphwright.Tests/McpServer/Tools/StubToolTests.cs
- Layer: Presentation
- Step type: wiring
- Test: this task IS the test fix — `StubTools()` drops the `ListSymbolsTool` row (currently line 15); the remaining 4 rows (`GetFileTool`/"path", `FindReferencesTool`/"symbol", `GetCallGraphTool`/"symbol", `SearchTool`/"query") and every test generated from them are unchanged and stay green, now covering exactly the 4 tools that remain genuine not-implemented stubs
- Acceptance: file no longer references `Graphwright.McpServer.Tools.ListSymbolsTool`; class-level intent (comment or otherwise) reflects "the 4 remaining stub tools"; test green
- Depends on: T08
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: this file's own `StubTools()`/`ToolsWithRequiredArguments()`/`ToolsWithExpectedNames()` generators (lines 13-36) — remove exactly the one row, keep the generator shape identical for the remaining 4

### T14 - End-to-end WORKSPACE_NOT_LOADED test + Scenario-12 structural proxy
- Files: tests/Graphwright.Tests/McpServer/DependencyInjection/ListSymbolsCompositionTests.cs
- Layer: Presentation
- Step type: test
- Test: tests/Graphwright.Tests/McpServer/DependencyInjection/ListSymbolsCompositionTests.cs (this task IS the test) — build a full container via `InfrastructureModule.Instance.RegisterServices` + `McpServerModule.Instance.RegisterServices` + `AddLogging()` (mirrors `Program.cs` and `McpServerModuleTests`'s helper from T10); resolve `ToolDispatcher`, dispatch `mcp__gitnexus__list_symbols` with `{}` arguments; assert the envelope is `ok: false`, `error.code == "WORKSPACE_NOT_LOADED"`, `error.retryable == true` (Scenario 10, proven through the exact composition-root path `Program.cs` uses); separately, resolve `RoslynWorkspaceSnapshot` from the same container twice and assert the two resolutions are the same singleton instance (`Assert.Same`), and that two consecutive `ListSymbolsAsync` calls on the resolved `ILanguageProvider` each still throw `WorkspaceNotLoadedException` without any observable mutation of the snapshot (structural proxy for CLAUDE.md's "not observed to build or re-load any part of the workspace during the call" — Scenario 12, per 00-spec.md's own hedge that this scenario "does not itself deliver the warm index")
- Acceptance: a comment in the test explicitly states that production wiring is `NotLoaded()` by design (00-spec.md Out of scope / 01-plan.md OQ-1 resolution) and this story does NOT close the Month 1 availability-probe DoD item; test green
- Depends on: T09, T10
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: tests/Graphwright.Tests/McpServer/DependencyInjection/McpServerModuleTests.cs (T10) — reuse its two-module composition helper rather than duplicating it; tests/Graphwright.Tests/McpServer/Transport/StdioIntegrationTests.cs — precedent for a full-stack, envelope-level (not internals-level) dispatch assertion

### T15 - Create mcp-contract.md with list_symbols as its first entry
- Files: mcp-contract.md
- Layer: Presentation
- Step type: polish
- Test: none new — acceptance is manual review that the documented schema matches `ListSymbolsTool.InputSchema` (T08) property-for-property
- Acceptance: `mcp-contract.md` created at repo root with a "Frozen tool schemas" heading and a front-matter note: "Source of truth per CLAUDE.md Key files; schema changes require updating this file first"; first fully-documented entry is `mcp__gitnexus__list_symbols` — input (`path?, name_filter?, kinds?, include_generated?, max_results?`, closed `kinds` vocabulary listed as the 10 lowercase literals), output shape (`results[]` with all 7 per-result fields, `truncated`, `total_found`), and the 3 error codes it can produce (`WORKSPACE_NOT_LOADED`, `INVALID_ARGUMENT`, `FILE_NOT_FOUND`); the remaining 4 tools (`get_file, find_references, get_call_graph, search`) are listed as explicit `TBD - lands with its own story` placeholders so the file's existence does not imply false completeness
- Depends on: T08
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: CLAUDE.md "MCP tool surface" table and "Error envelope" section — mirror structure and wording; this file formalizes what CLAUDE.md already asserts informally

---

## Dependency graph (critical path bolded)

```
T01 ──┬─> T02 ──────────────────┐
      └─> T04 ──> T05 ──> T06 ──> T07
T03 ──┴─> T04                    │
T01,T02 ─────────────────────────┴─> T08 ──┬─> T09 (also needs T03,T04) ──┬─> T10 ──> T14
                                            ├─> T11                        │
                                            ├─> T12                       T09 ─┘
                                            ├─> T13
                                            └─> T15
```

**Critical path**: T01 → T04 → T05 → T06 → T07 (5 tasks — the Roslyn extraction/filter chain is
the deepest dependency chain). T09/T10/T14 depend only on T03/T04 (not the full T07 chain) and on
T08, so they can complete in parallel with T05–T07's classification work; T11/T12/T13/T15 depend
only on T08. The story is not "done" until every branch completes, but T07's branch alone
determines the longest single dependency chain.

## Complexity summary

| Complexity | Tasks | Count |
|---|---|---|
| S | T02, T09, T10, T11, T12, T13, T15 | 7 |
| M | T01, T03, T04, T06, T07, T14 | 6 |
| L | T05, T08 | 2 |

Total: **15 tasks** (7 S + 6 M + 2 L).
