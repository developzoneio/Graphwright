---
id: FEAT-GW-5
type: feature
phase: plan
created: 2026-07-02
spec: .specs/FEAT-GW-5/00-spec.md
impact: .specs/FEAT-GW-5/03-decisions.md
---

# FEAT-GW-5 — Implementation plan: Tool: list_symbols (SyntaxTree + SemanticModel)

## Scope framing (confirmed against 03-decisions.md, verified on disk 2026-07-02)

`03-decisions.md` independently confirms the spec's own finding: `src/Graphwright.Application`
has no source beyond generated `AssemblyInfo.cs`, and
`InfrastructureModule.RegisterServices` is a documented no-op. There is no
`ILanguageProvider`/`IWorkspaceLoader`/`IIndexer` seam and no `MSBuildWorkspace` usage anywhere
in `src/`. This plan resolves **OQ-1 as option (b)** from the spec, made concrete:

- This story does **not** load, open, or warm a real Roslyn project/solution
  (`MSBuildWorkspace`, `MSBuild.Locator`, file-system project discovery — all explicitly out of
  scope, and `Microsoft.Build.Locator` stays an unused package reference this story).
- This story **does** define `ILanguageProvider` as a contract-only Application-layer interface,
  and ships one concrete Infrastructure implementation, `RoslynLanguageProvider`, sufficient to
  exercise Scenarios 1–11a **against an in-memory `AdhocWorkspace`/`Solution` built entirely in
  test code** (never `MSBuildWorkspace`, never real project loading).
- Production DI (`InfrastructureModule.RegisterServices`) wires `RoslynLanguageProvider` over
  `RoslynWorkspaceSnapshot.NotLoaded()` — an intentionally unloaded snapshot. Calling
  `mcp__gitnexus__list_symbols` through the real server today therefore still returns
  `WORKSPACE_NOT_LOADED` end-to-end (Scenario 10), exactly as the stub did, but now through the
  real seam instead of a `ToolNotImplementedException`. **This story does not close the Month 1
  "availability probe" DoD item** — Scenario 12 is verified structurally (no reload observed)
  against a *loaded test fixture*, not against a warm production index. The warm index is a
  future indexer story's job; no task below builds it, and no task's acceptance implies
  otherwise.

This framing is deliberately stated up front because it is the one thing every downstream task
depends on: the seam is real and testable, the production data behind it is honestly empty.

## Open-question resolutions

### OQ-1 — workspace-loading seam (resolved above; concrete shapes below)

- `Graphwright.Application.LanguageProviders.ILanguageProvider` — one method,
  `Task<SymbolListResult> ListSymbolsAsync(ListSymbolsQuery query, CancellationToken ct)`. No
  `bool IsReady` property: readiness is expressed by the implementation throwing
  `WorkspaceNotLoadedException`, so callers (the tool) never need a separate readiness check —
  one call site, one failure mode, consistent with the "raise, don't branch" exception-based
  error model the rest of the codebase already uses.
- `Graphwright.Infrastructure.LanguageProviders.RoslynWorkspaceSnapshot` — a small immutable
  wrapper (`bool IsLoaded`, `Solution Solution`) with a `NotLoaded()` factory over an empty
  `AdhocWorkspace`. This is the "workspace-readiness signal" the spec says this story consumes
  but does not build (00-spec.md "Out of scope").
- `Graphwright.Infrastructure.LanguageProviders.RoslynLanguageProvider` — the only
  `Microsoft.CodeAnalysis.*`-touching type outside test code; constructor-injected with a
  `RoslynWorkspaceSnapshot`.

**Alternatives considered:**
- *Option (a) from the spec — GW-5 also loads a real test-project workspace via
  `MSBuildWorkspace`.* Rejected: `MSBuildWorkspace` bootstrap, `MSBuild.Locator` registration,
  and real project/solution discovery are non-trivial, environment-sensitive (SDK resolution,
  `.sln` parsing), and explicitly named as a separate concern in 00-spec.md "Out of scope". Doing
  it here would make this story's success depend on an unrelated, unspecced capability.
- *Deferring `ILanguageProvider` itself to the indexer story, shipping GW-5 as Scenario-10-only.*
  Rejected: the spec's own recommendation is to declare the seam now as a stable downstream
  target; waiting would leave `ListSymbolsTool` untestable for Scenarios 1–9/11/11a, which are
  the majority of this story's acceptance criteria and are fully testable without a real project
  loader.
- *A single `RoslynLanguageProvider` that owns constructing its own `AdhocWorkspace` internally
  (no injected snapshot).* Rejected: makes "not loaded" inexpressible without a magic sentinel
  and makes tests unable to inject prebuilt fixtures cleanly. The injected `RoslynWorkspaceSnapshot`
  keeps the provider itself stateless and trivially testable.

### OQ-2 — `kinds` vocabulary (resolved: closed set, lowercase single-word literals)

`Graphwright.Application.LanguageProviders.DeclaredSymbolKind` — closed enum, exactly 10
PascalCase members: `Namespace, Class, Interface, Struct, Enum, Method, Property, Field, Event,
Constructor`. Wire literals (used in the `kinds` input array and the `kind` output field) are the
lowercase form of each name, unchanged (`namespace`, `class`, ... `constructor`) — no snake_case
needed since every member is already a single word, matching the existing JSON schema style used
elsewhere in the tool surface (e.g. error codes are ALL_UPPER, tool names are
`mcp__gitnexus__snake_case`; symbol kinds are plain lowercase words, their own small closed
vocabulary). An unrecognized `kinds` literal raises `INVALID_ARGUMENT` (Scenario 11) before any
provider call. The wire mapping lives in McpServer (`SymbolKindWireMapper`), mirroring where
`ExceptionEnvelopeMapper` already puts wire-format concerns — inner layers never see the wire
string, only the enum.

**Alternatives considered:** putting the wire mapping in Application (so both the DTO and its
wire form live together) — rejected, same reasoning `03-decisions.md`/GW-4's OQ-4 already
established for the error envelope: wire vocabulary is a presentation concern, and
`GraphwrightErrorCode` already sets the "enum in an inner layer, string mapping in McpServer"
precedent this story reuses.

### OQ-3 — path casing (out of scope; sidestepped in fixtures)

No production behavior added or changed by this story. All test fixtures added by this plan's
tasks use consistent, unambiguous casing (e.g. `a/Foo.cs`, `a/Sub/Bar.cs`, never two paths that
differ only by case) so no task's tests are OS-dependent. Ordering additionally uses
`StringComparer.Ordinal` (see T07) rather than an OS-default comparer, which is casing-stable
across Windows/POSIX by construction.

### OQ-4 — schema replacement safety

Confirmed by `03-decisions.md`: no code outside `ListSymbolsTool.cs` and its own test file
references the old `required: ["file"]` shape. Replacing it is safe; the ripple is entirely in
tests that construct `ListSymbolsTool` directly (see Risks and T11/T12/T13 below), not in any
other consumer of the schema.

### OQ-5 — latency budget (deferred, per spec recommendation)

No numeric budget defined or tested this story. Scenario 12 is covered structurally (T14): no
workspace rebuild/reload observed during a call. A dedicated perf spec picks up a hard number
once a warm index exists to measure against (constitution §3 "Performance prerequisite").

### OQ-6 — `container`/`accessibility` always present (resolved, concrete per-kind rules)

Confirmed reading: always present. Concrete rules (pinned so no implementer has to guess):

- **`accessibility`** = `symbol.DeclaredAccessibility` lowercased through a fixed table: `Public
  -> "public"`, `Private -> "private"`, `Protected -> "protected"`, `Internal -> "internal"`,
  `ProtectedOrInternal -> "protected_internal"`, `ProtectedAndInternal -> "private_protected"`,
  `NotApplicable -> "not_applicable"`. Namespace declarations always report `DeclaredAccessibility
  == NotApplicable` in Roslyn (namespaces carry no C# access modifier) — mapped honestly to
  `"not_applicable"` rather than inventing a plausible-looking `"public"` (CLAUDE.md "No invented
  data" cross-cutting rule, extended here to output *fields*, not just result sets).
- **`container`** = `symbol.ContainingSymbol?.ToDisplayString() ?? string.Empty`, and empty string
  also when the containing symbol is the global namespace (a top-level type's container is `""`,
  not `"<global namespace>"`).

### OQ-7 — `mcp-contract.md` does not exist (resolved: this story creates it)

T15 creates `mcp-contract.md` with `list_symbols` as its first fully-specified entry and the
remaining 4 tools as explicit `TBD` placeholders, so CLAUDE.md's "schema changes require updating
mcp-contract.md first" rule has a real target starting now.

## Decision D1 — signature format (pinned, not left open)

`Signature = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)` — Roslyn's own
built-in format for compiler-error-style qualified names (e.g. a `GetTotalAsync(int orderId)`
method on `OrderService` renders as `OrderService.GetTotalAsync(int)`). T05's tests assert the
*actual* runtime output as a literal string; if that literal doesn't match what's written above
once compiled against the pinned Roslyn version (4.14.0), the test (and only the test) adjusts to
match reality, and the final confirmed string is recorded in `03-decisions.md` — the *format
choice* itself does not change without a plan update.

## Decision D2 — enumeration strategy (pinned)

Walk `MemberDeclarationSyntax` descendant nodes of the resolved document's syntax root and call
`semanticModel.GetDeclaredSymbol(node)` per node — never a whole-`Compilation` symbol-tree walk.
This is what anchors `line` to the exact declaration site (CLAUDE.md cross-cutting rule 1) and
naturally yields one entry per partial declaration. Classification keys off the **returned
symbol's** `Kind`/`TypeKind`/`MethodKind` rather than the syntax node's C# keyword, so `record
class`/`record struct` fall out as `Class`/`Struct` with zero special-casing.

**Field/event-field carve-out (required, not optional):** `GetDeclaredSymbol` has no overload for
`FieldDeclarationSyntax` or `EventFieldDeclarationSyntax` and returns `null` for both — a plain
`public int x, y;` or `public event EventHandler E;` declares one symbol *per variable declarator*,
not per declaration statement. For these two node kinds specifically, descend into
`.Declaration.Variables` and call `GetDeclaredSymbol` on each `VariableDeclaratorSyntax` instead.
`EnumMemberDeclarationSyntax` and the property-style `EventDeclarationSyntax` (`event
EventHandler E { add; remove; }`) bind directly on the declaration node itself — no carve-out
needed for those two. `file` and `line` both come from `Location.GetLineSpan().Path`/`.Line`
(`Line` is 1-based, `.Path` — never a separately-derived string — so the two can never disagree).

Full classification table and exclusions (property/event accessors, delegates) are in
`02-tasks.md` T05 — pinned there rather than restated twice.

**Alternatives considered:** a `Compilation.GlobalNamespace` recursive symbol-tree walk (visiting
`INamespaceSymbol`/`INamedTypeSymbol` children) — rejected: loses the syntax-node anchor needed
for an honest per-declaration `line`, and requires re-deriving file/line from
`symbol.Locations.First()` per symbol anyway, which is strictly more indirect than starting from
the syntax node in the first place.

## Decision D3 — no new Domain exceptions

`WorkspaceNotLoadedException`, `SourceFileNotFoundException`, `InvalidToolArgumentException` are
reused unchanged (confirmed by `03-decisions.md`: all three exist, are tested, and
`ExceptionEnvelopeMapper` already maps all three codes). No task in this plan touches
`Graphwright.Domain.Exceptions` or `ExceptionEnvelopeMapper.cs`.

## Risks flagged by 03-decisions.md — how this plan addresses them

- **StubToolTests hardcodes `ListSymbolsTool` requiring `"file"`.** Addressed by T13 (remove the
  row) plus T11/T12, which is the fuller picture the impact analysis under-scoped: `03-decisions.md`
  only names `StubToolTests`, but `ListSymbolsTool` is also constructed directly (with no
  constructor args) in `ToolDispatcherTests.cs:93,110` and `ToolRegistryTests.cs:46`. All three
  files break the moment `ListSymbolsTool` gains a required `ILanguageProvider` constructor
  parameter; all three have dedicated tasks below (T11, T12, T13). A fourth, subtler breakage:
  `McpServerModuleTests.BuildRegisteredServices()` (T10) registers only `McpServerModule`, so
  resolving `IReadOnlyList<IGitnexusTool>` will fail with a missing-service exception once
  `ListSymbolsTool` needs `ILanguageProvider` and nothing in that test's container provides one.
- **`ExceptionEnvelopeMapper`/domain exceptions reused unchanged.** No task edits them (D3); T05
  and T08's acceptance criteria explicitly require raising the *existing* exception types with no
  new mapping logic.

## Phased overview

| Phase | Tasks | Delivers |
|---|---|---|
| **Foundation** | T01–T03 | `ILanguageProvider` + DTOs (Application, no Roslyn refs); `SymbolKindWireMapper` (McpServer); `RoslynWorkspaceSnapshot` + in-memory `AdhocWorkspace` test-fixture builder (Infrastructure) |
| **Behavior** | T04–T08 | `RoslynLanguageProvider` built up scenario-by-scenario (scope/errors -> extraction -> directory/exclusion -> filter/order/cap); `ListSymbolsTool` rewritten to the real schema, validated, wired to `ILanguageProvider` |
| **Wiring** | T09–T14 | DI registration (production = `NotLoaded()`); the 4 broken pre-existing test files repaired; end-to-end/Scenario-12-proxy test |
| **Polish** | T15 | `mcp-contract.md` created with `list_symbols` as its first entry |

## Sequencing rationale

1. **T01 before everything** — every other task either implements `ILanguageProvider` (T04) or
   consumes it (T02, T08, T09).
2. **T04 → T05 → T06 → T07 is a strictly sequential single-writer chain** on
   `RoslynLanguageProvider.cs`: scope resolution and error cases first (so extraction has
   something safe to extract *from*), then single-file extraction (the hardest, most
   classification-heavy piece), then directory/whole-workspace scoping and exclusion (needs
   extraction to exist to prove it filters correctly), then name/kind filtering + ordering + cap
   (needs multiple extracted results to filter/order/cap over). Each step's tests must stay green
   through the next.
3. **T08 (`ListSymbolsTool`) only needs T01 + T02, not the finished provider.** It is developed
   against a fake `ILanguageProvider` and can run in parallel with T04–T07 once T01/T02 land —
   flagged explicitly so the two chains aren't accidentally serialized.
4. **T09 (DI registration) only needs T03 + T04**, not the fully-filtered T07 provider — DI
   wiring cares that `RoslynLanguageProvider` exists and implements the interface, not that every
   scenario is finished. This keeps T09/T10/T14 off the critical path of T05–T07's classification
   work.
5. **T11/T12/T13 (repair the 3 broken pre-existing test files) all depend only on T08** (the
   constructor-signature change is what breaks them) — they can run in any order relative to each
   other and relative to T04–T07.
6. **T14 (end-to-end) needs T09 + T10** — the full two-module composition path.
7. **T15 (mcp-contract.md) last** — it documents the *final* schema; writing it before T08 lands
   risks documenting a schema that changes underneath it.

**Critical path**: T01 → T04 → T05 → T06 → T07 (5 tasks — the Roslyn extraction/filter chain is
the longest dependency chain in this story; every other branch is ≤4 deep from T01). The story is
not "done" until T07's branch AND the T08/T09-rooted branches all complete, but none of the other
branches individually gate T07.

## Risks

| Risk | Mitigation |
|---|---|
| **`SymbolDisplayFormat.CSharpErrorMessageFormat` output doesn't match the exact string assumed in D1** | T05's acceptance requires asserting the *actual* compiled-and-run output, not a guessed literal; any mismatch is a test-only fix, format choice is unchanged; record the confirmed literal in `03-decisions.md`. |
| **`GetDeclaredSymbol` returns `null` for a node kind not anticipated (e.g. `GlobalStatementSyntax` in top-level programs)** | D2's walk only visits `MemberDeclarationSyntax` nodes; a `null` result is skipped, never dereferenced — T05 acceptance states this defensively. |
| **AdhocWorkspace-built `Compilation` produces error types for BCL types if metadata references are wrong**, corrupting `signature`/`accessibility` assertions | T03 pins the exact technique: reference every assembly in `AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")`, giving the fixture the full running runtime's reference set with no new NuGet package and no hand-picked assembly list to get wrong. |
| **4 pre-existing test files break on the `ListSymbolsTool` constructor change, beyond the one `03-decisions.md` flagged** | Enumerated explicitly above (Risks section) and given one task each (T10–T13) instead of being discovered mid-implementation. |
| **This story is mistaken for "closing" the Month 1 availability-probe DoD item** | Stated up front in Scope framing and repeated in T14's acceptance/test comment: production wiring is `NotLoaded()`; Scenario 12 is a structural proxy against a *test* fixture, not a production warm index. |
| **`kinds` case-sensitivity inconsistency** — `name_filter` is case-insensitive (Scenario 4) but `kinds` literals are not specced as case-insensitive | T02 pins `kinds` matching as case-sensitive lowercase-exact (the vocabulary *is* lowercase, so a caller sending `"Method"` gets `INVALID_ARGUMENT`) — stated explicitly so it isn't silently inconsistent with `name_filter`'s different rule. |
| **`GetDeclaredSymbol` returns `null` for plain fields/field-form events** (no per-declaration overload exists; a `FieldDeclarationSyntax`/`EventFieldDeclarationSyntax` can declare multiple symbols) — a naive "call `GetDeclaredSymbol` on every `MemberDeclarationSyntax`, skip nulls" implementation silently drops every `field` result | Decision D2's field/event-field carve-out (descend into `.Declaration.Variables`, call `GetDeclaredSymbol` per `VariableDeclaratorSyntax`) is mandatory, not optional; T05's fixture includes a plain field and a field-form event specifically to prove extraction at the point the bug would be introduced, not three tasks later at T07 where `field`-kind filtering is tested. |
| **`AdhocWorkspace` fixture never actually produces a working C# `SemanticModel`** if `ProjectInfo` omits `LanguageNames.CSharp`/`CSharpParseOptions`, or if the `TRUSTED_PLATFORM_ASSEMBLIES` references don't resolve BCL types — T04–T07's tests would fail confusingly far from the actual cause | T03 (foundation) asserts `GetSemanticModelAsync()` is non-null AND that `compilation.GetTypeByMetadataName("System.Threading.Tasks.Task")` resolves, proving the extraction substrate works before any extraction logic is written on top of it. |

## Constitution check (plan level)

- **§1.1 dependency direction / inversion** — `ILanguageProvider` is defined in Application (T01)
  and implemented in Infrastructure (T03/T04–T07); `ListSymbolsTool` (McpServer, T08) depends only
  on the Application interface, never on `Microsoft.CodeAnalysis.*` — verified by task-level
  acceptance (T01: "zero `Microsoft.CodeAnalysis.*` references in `Graphwright.Application`") and
  achievable without any csproj change (Infrastructure already references the Roslyn packages
  from GW-26; McpServer never will).
- **§1.1 cross-layer data (DTOs only)** — `DeclaredSymbol`/`ListSymbolsQuery`/`SymbolListResult`
  (T01) are the only shapes that cross the Infrastructure→Application/McpServer boundary; no
  Roslyn `ISymbol`/`Location`/`SemanticModel` type ever leaves `RoslynLanguageProvider.cs`.
- **§6 catch-and-swallow** — no task adds a `catch`; `RoslynLanguageProvider` and `ListSymbolsTool`
  raise, never catch (D3); the existing `ToolDispatcher` boundary (unchanged) does the only
  catching in the system.
- **§6 static singletons holding state** — `RoslynWorkspaceSnapshot` (T03) is immutable
  configuration-like data (mirrors `ToolRegistry`'s already-accepted pattern), not mutable state;
  `SymbolKindWireMapper` (T02) mirrors `ExceptionEnvelopeMapper`'s existing `static readonly
  Instance` + stateless-instance-method shape exactly.
- **§6 TODO/HACK** — the one genuinely partial piece of this story (production wiring stays
  "not loaded") is expressed as an honest `WorkspaceNotLoadedException` path plus a spec-tracked
  follow-up (the future indexer story), never a code comment marker.
- **§5.1 spec-driven** — this plan derives directly from the approved 00-spec.md; no scope added
  beyond OQ-1's own recommended option (b), made concrete.
- **§3 quality bars** — every new production file (T01, T02, T03, T04–T08, T09) ships with a
  dedicated or extended test in the same task; the constitution's `<<80>>%` placeholder is still
  unfilled (carried-forward gap from GW-4/GW-26, not resolved by this plan) but every task targets
  full behavioral coverage of its own acceptance criteria regardless of the numeric gate.
