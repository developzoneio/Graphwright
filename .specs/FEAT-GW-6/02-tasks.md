---
id: FEAT-GW-6
type: feature
phase: tasks
created: 2026-07-03
plan: .specs/FEAT-GW-6/01-plan.md
---

# FEAT-GW-6 — Task list

Conventions binding on EVERY task (from CLAUDE.md + `.specs/FEAT-GW-5` precedent): `== false` /
`== true` comparisons (never bare `!expr`); custom domain exceptions only, never a new exception
type without a plan decision; `Async` suffix + `CancellationToken ct` as last parameter on all
async methods; `#nullable enable` (inherited from `Directory.Build.props`); no static classes with
static methods (use `static readonly` instance, mirroring `ExceptionEnvelopeMapper` /
`InfrastructureModule` / `McpServerModule`); `if` always braced; 4-space indent; max 120-char
lines; comments on their own line, uppercase start, English. Test files live under
`tests/Graphwright.Tests/` mirroring the source folder structure. `Microsoft.CodeAnalysis.*` may
only appear in files under `src/Graphwright.Infrastructure/` or their direct test counterparts —
never in `src/Graphwright.Application/` or `src/Graphwright.McpServer/`.

**Note on T01→T02→T03 (read before starting)**: `Graphwright.Tests` is a single assembly that
references `Graphwright.Infrastructure` directly (`RoslynLanguageProviderTests` uses Infrastructure
types), so the *entire test suite* fails to build — not just the files that changed — until every
`ILanguageProvider` implementer compiles. Adding `GetFileAsync` to the interface (T01) breaks
`RoslynLanguageProvider` (Infrastructure, fixed in T03) and two pre-existing test-fake files
(fixed in T02) simultaneously. **T01, T02, and T03 form a strict, non-negotiable prefix**: none of
them individually leaves a runnable build, and no task's tests — including T01's own DTO
round-trip tests — are actually verifiable green until all three have landed. `Depends on` below
encodes this as a strict chain (T02 depends on T01; T03 depends on T01 **and** T02); treat T01+T02
as "in flight, not yet verified" until T03's completion proves the whole trio compiles and passes.
This is stated explicitly here and in `01-plan.md` Sequencing rationale — it is not an oversight.

## Checklist

- [ ] T01 — `GetFileQuery` + `FileContentResult` DTOs + `ILanguageProvider.GetFileAsync` (Application)
- [ ] T02 — Fix pre-existing `ILanguageProvider` test fakes to implement `GetFileAsync`
- [ ] T03 — `RoslynLanguageProvider.GetFileAsync`: resolution + WorkspaceNotLoaded + FileNotFound + whole-file mode
- [ ] T04 — `RoslynLanguageProvider.GetFileAsync`: explicit range mode (clamp + reject)
- [ ] T05 — `RoslynLanguageProvider.GetFileAsync`: bounded snippet base window
- [ ] T06 — `RoslynLanguageProvider.GetFileAsync`: boundary-snapping via SyntaxTree
- [ ] T07 — `GetFileTool`: new schema, validation, `ILanguageProvider` wiring
- [ ] T08 — `GetFileToolTests.cs` (new): validation + wiring/pass-through scenarios
- [ ] T09 — Fix `ToolDispatcherTests` (re-point both `GetFileTool` stub-behavior tests to `FindReferencesTool`)
- [ ] T10 — Fix `ToolRegistryTests` (`GetFileTool` needs a fake `ILanguageProvider` now)
- [ ] T11 — Fix `StubToolTests` (drop the `GetFileTool` row, 3 remaining stubs)
- [ ] T12 — End-to-end `WORKSPACE_NOT_LOADED` composition test for `get_file`

---

### T01 - GetFileQuery + FileContentResult DTOs + ILanguageProvider.GetFileAsync (Application)
- Files: src/Graphwright.Application/LanguageProviders/GetFileQuery.cs; src/Graphwright.Application/LanguageProviders/FileContentResult.cs; src/Graphwright.Application/LanguageProviders/ILanguageProvider.cs; tests/Graphwright.Tests/Application/LanguageProviders/GetFileQueryTests.cs; tests/Graphwright.Tests/Application/LanguageProviders/FileContentResultTests.cs
- Layer: Application
- Step type: foundation
- Test: tests/Graphwright.Tests/Application/LanguageProviders/GetFileQueryTests.cs — constructor round-trips all 5 properties (`Path`, `StartLine`, `EndLine`, `AroundLine`, `Context`) exactly, including the `null` cases for the three optional line fields; tests/Graphwright.Tests/Application/LanguageProviders/FileContentResultTests.cs — constructor round-trips all 5 properties (`File`, `StartLine`, `EndLine`, `TotalLines`, `Content`) exactly
- Acceptance: `GetFileQuery` is an immutable DTO with `string Path` (non-null, required — unlike `ListSymbolsQuery.Path` which is nullable), `int? StartLine`, `int? EndLine`, `int? AroundLine`, `int Context` (the caller — T07 — guarantees `Context` is already validated-positive and defaulted to 2 when omitted, and guarantees the start/end-pair-vs-around_line mutual exclusivity, before this query is constructed — mirrors `ListSymbolsQuery`'s existing "provider does not re-validate query structure" convention); `FileContentResult` exposes `string File`, `int StartLine`, `int EndLine`, `int TotalLines`, `string Content`; `ILanguageProvider` gains `Task<FileContentResult> GetFileAsync(GetFileQuery query, CancellationToken ct)` alongside the existing `ListSymbolsAsync`; zero `Microsoft.CodeAnalysis.*` references anywhere under `src/Graphwright.Application/`; `#nullable enable`. **This task's own DTO round-trip tests do NOT compile/run at this point** — `RoslynLanguageProvider.cs` and two test-fake files stop satisfying `ILanguageProvider` the moment this interface member is added, and `Graphwright.Tests` is one assembly, so the whole suite is red until T02 and T03 both land too (see the note above the Checklist). Do not mark this task's tests "green" until T03's completion; this task's own acceptance is satisfied by the code existing and matching the shape above, verified retroactively once T03 lands
- Depends on: none
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: src/Graphwright.Application/LanguageProviders/ListSymbolsQuery.cs:1-55 and SymbolListResult.cs:1-34 — mirror immutable-DTO shape, XML-doc density, and constructor-sets-properties style exactly; src/Graphwright.Application/LanguageProviders/ILanguageProvider.cs:13-22 — add the new member alongside `ListSymbolsAsync` with the same "raises GraphwrightException subclasses, never a best-effort/partial result" doc-comment framing

### T02 - Fix pre-existing ILanguageProvider test fakes to implement GetFileAsync
- Files: tests/Graphwright.Tests/McpServer/Tools/ListSymbolsToolTests.cs; tests/Graphwright.Tests/McpServer/Registry/ToolRegistryTests.cs
- Layer: Presentation
- Step type: wiring
- Test: this task IS the test fix — all existing facts in both files stay green; no new fact is added; `PoisonLanguageProvider`, `CapturingLanguageProvider`, `ThrowingLanguageProvider` (`ListSymbolsToolTests.cs`) and `NeverInvokedLanguageProvider` (`ToolRegistryTests.cs`) each gain a `GetFileAsync` implementation so the solution compiles again
- Acceptance: `PoisonLanguageProvider.GetFileAsync` throws `InvalidOperationException("must not be called")`, mirroring its own `ListSymbolsAsync` sibling exactly; `CapturingLanguageProvider.GetFileAsync` and `ThrowingLanguageProvider.GetFileAsync` throw `NotSupportedException` with a message stating they are not exercised by `get_file` tests (these two fakes are `list_symbols`-specific and no `GetFileToolTests` fact constructs them); `NeverInvokedLanguageProvider.GetFileAsync` throws the same `InvalidOperationException` message pattern as its existing `ListSymbolsAsync`, restating that `ToolRegistry` tests exercise name/ordering/contract mechanics only; zero behavior change to any existing test. **This task alone still does not produce a green build** — `RoslynLanguageProvider.cs` (T03) has not yet gained `GetFileAsync`, so `Graphwright.Infrastructure` (and therefore the whole `Graphwright.Tests` assembly) is still red; this task's own "existing facts stay green" claim is verified retroactively once T03 lands, same caveat as T01
- Depends on: T01
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: tests/Graphwright.Tests/McpServer/Tools/ListSymbolsToolTests.cs:19-58 (this file's own three fakes) — mirror each fake's existing exception-throwing style for its new `GetFileAsync` member; tests/Graphwright.Tests/McpServer/Registry/ToolRegistryTests.cs:16-24 (`NeverInvokedLanguageProvider`) — mirror its existing message wording for the new member

### T03 - RoslynLanguageProvider.GetFileAsync: resolution + WorkspaceNotLoaded + FileNotFound + whole-file mode
- Files: src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs; tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs
- Layer: Infrastructure
- Step type: behavior
- Test: tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs — a provider built over `RoslynWorkspaceSnapshot.NotLoaded()` throws `WorkspaceNotLoadedException` for a `GetFileAsync` query with any argument shape (Scenario 14); a provider built over a fixture with documents `a/Foo.cs`/`b/Bar.cs`, queried with `Path = "a/Missing.cs"`, throws `SourceFileNotFoundException` with `FilePath == "a/Missing.cs"` (Scenario 13); a fixture document with a known, pinned line-ending style and N lines, queried with only `Path` set (no range/around arguments), returns a `FileContentResult` where `StartLine == 1`, `EndLine == N`, `TotalLines == N` (asserted via `SourceText.Lines.Count`, not a hand-computed count), and `Content` equals the fixture's exact source text end-to-end (Scenario 1)
- Acceptance: `GetFileAsync` throws `WorkspaceNotLoadedException` first, before any other work, when `_snapshot.IsLoaded == false` (mirrors `ListSymbolsAsync`'s existing check exactly); resolves `query.Path` via the existing private `ResolveScopedDocumentsOrThrow(query.Path, includeGenerated: false)` (`includeGenerated` hard-coded `false` — `get_file` has no `include_generated` argument, so a request for a `bin/`/`obj/`/`node_modules/`/`*.g.cs` path is treated as not-found, same server-side filter as `list_symbols`), taking the resolved list's single document (a directory-shaped `path` yielding multiple documents is out of scope for this story — not a spec scenario — and may deterministically take the first ordered document without further special-casing); for the whole-file mode (no `StartLine`/`EndLine`/`AroundLine` present on the query), reads `await document.GetTextAsync(ct)`, sets `TotalLines = sourceText.Lines.Count`, `StartLine = 1`, `EndLine = TotalLines`, `Content = sourceText.ToString()`; `File` echoes `query.Path` verbatim (00-spec.md OQ-8 resolution — same path-echoing convention as `list_symbols`); `#nullable enable`, `Async` suffix, `CancellationToken ct` last parameter. **This is the completion point of the T01→T02→T03 prefix**: test green here means the whole `Graphwright.Tests` assembly builds and passes, which is also the first point T01's DTO round-trip tests and T02's fake fixes are actually verified, not just this task's own new facts
- Depends on: T01, T02
- Conflicts with: none
- Complexity: M
- Reversibility: moderate
- Pattern refs: src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs:40-51 (`ListSymbolsAsync`'s workspace-check + `ResolveScopedDocumentsOrThrow` call) — mirror the same not-loaded-first ordering and reuse the exact same private method, do not re-implement scope resolution; src/Graphwright.Domain/Exceptions/WorkspaceNotLoadedException.cs and SourceFileNotFoundException.cs:13-35 — raise these unchanged domain types, never a new one

### T04 - RoslynLanguageProvider.GetFileAsync: explicit range mode (clamp + reject)
- Files: src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs; tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs
- Layer: Infrastructure
- Step type: behavior
- Test: tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs — a fixture with at least 10 lines, queried with `StartLine = 3, EndLine = 6`, returns `StartLine == 3`, `EndLine == 6`, `Content` equal to exactly lines 3-6 inclusive via 01-plan.md Decision D1's slicing rule, asserted against the fixture's actual compiled text (Scenario 2); a fixture with N lines, queried with `StartLine` within `[1, N]` and `EndLine > N`, returns `EndLine == N` (not the out-of-bounds requested value) and `Content` spanning `StartLine` through `N` (Scenario 9); the same fixture queried with `StartLine > N` throws `InvalidToolArgumentException`-free `INVALID_ARGUMENT`-mapped behavior — concretely, the provider throws a `GraphwrightException` subclass appropriate to "the entire requested range names no real line" (00-spec.md Scenario 9a: reject, not clamp) — record the exact exception type chosen (mirroring `InvalidToolArgumentException`'s existing two-argument shape, e.g. `argumentName: "start_line"`) in `03-decisions.md` once confirmed, since 00-spec.md pins the *behavior* (`INVALID_ARGUMENT`, non-retryable) but not which layer raises it
- Acceptance: when `query.StartLine`/`query.EndLine` are both non-null (explicit-range mode), after computing `totalLines` from the resolved document's `SourceText.Lines.Count`: if `query.StartLine > totalLines`, throw (Scenario 9a — reject, no clamp); else `effectiveEndLine = Math.Min(query.EndLine.Value, totalLines)` (Scenario 9 — clamp); `Content` sliced per Decision D1 over `[query.StartLine.Value, effectiveEndLine]`; result's `StartLine`/`EndLine` reflect the actual served range, not necessarily the literal requested numbers (00-spec.md "Output"); test green
- Depends on: T03
- Conflicts with: T03 (same production file, sequential edit — do not parallelize)
- Complexity: M
- Reversibility: moderate
- Pattern refs: 01-plan.md Decision D1 — content-slicing formula, implement exactly as pinned, adjust only the test literal (not the formula) if the actual compiled output differs; src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:182-200 (`ValidateOptionalMaxResults`'s clamp-not-reject pattern) — the *symmetry precedent* 00-spec.md Scenario 9 explicitly cites for why clamping (not rejecting) is correct here

### T05 - RoslynLanguageProvider.GetFileAsync: bounded snippet base window
- Files: src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs; tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs
- Layer: Infrastructure
- Step type: behavior
- Test: tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs — a fixture with at least 20 lines where lines 8-12 are each a single simple statement, queried with `AroundLine = 10` and no `Context`, returns `StartLine == 8`, `EndLine == 12` (Scenario 3, default `Context = 2` from the query, not re-defaulted here — T07 guarantees it arrives already `2`); the same shape of fixture queried with `AroundLine = 10, Context = 5` returns `StartLine == 5`, `EndLine == 15` (Scenario 4); a fixture with N lines queried with `AroundLine = 1` (and separately `AroundLine = N`) and default context returns `StartLine >= 1` and `EndLine <= N` with no artificial padding to force a fixed window count past the file's edges (Scenario 10)
- Acceptance: `rawStart = Math.Max(1, query.AroundLine.Value - query.Context)`, `rawEnd = Math.Min(totalLines, query.AroundLine.Value + query.Context)`; this task computes and serves exactly this clamped-at-file-edges window with no `SyntaxTree` consultation yet (that is T06 only, layered on top of this task's result, never replacing it — T06's per-edge expansion starts from this task's `[rawStart, rawEnd]`, per 01-plan.md Decision D3 step 1); `Content` sliced per Decision D1; test green
- Depends on: T04
- Conflicts with: T03, T04 (same production file, sequential edit)
- Complexity: S
- Reversibility: moderate
- Pattern refs: 00-spec.md Scenario 10 gherkin — "never less than 1... never more than N... not artificially padded" is this task's acceptance source verbatim

### T06 - RoslynLanguageProvider.GetFileAsync: boundary-snapping via SyntaxTree
- Files: src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs; tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs
- Layer: Infrastructure
- Step type: behavior
- Test: tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs — **body-region fixture** (Scenario 11): a method body containing a 3-line method-call statement (e.g. an invocation with arguments spread across 3 lines), `AroundLine` centered inside that statement such that T05's raw window would end partway through it — asserts the served `StartLine`/`EndLine` expand outward (never inward) to exactly that statement's own start/end lines, and separately asserts a window whose raw edges already sit on complete-statement boundaries (reusing T05's Scenario 3/4 fixtures) is served completely unchanged (proving the per-edge loop is a no-op when there's nothing to expand); **header-region fixture** (Scenario 15): a method with a signature spanning 3+ lines (multi-line parameter list), `AroundLine` set to the method's own declaration start line (the exact `list_symbols`-reported line for that method) — asserts the served window's `StartLine` equals the method's `MemberDeclarationSyntax` start line and `EndLine` equals the line of the token immediately preceding whichever of `Body`/`ExpressionBody`/`SemicolonToken` is present, NOT extending into the method body (proving the header clamp, not just the per-edge expansion, fires); **hard-ceiling fixture**: a window whose per-edge expansion would otherwise reach into an adjacent sibling member — asserts expansion stops at the enclosing `MemberDeclarationSyntax`'s own span and never crosses into the sibling
- Acceptance: implements 01-plan.md Decision D3 exactly — per-edge loop via `syntaxRoot.FindNode(sourceText.Lines[currentLine - 1].Span, getInnermostNodeForTie: true)`, comparing the found node's `GetLocation().GetLineSpan().StartLinePosition.Line + 1` (top edge) / end-line equivalent (bottom edge) against the current edge, looping until the edge already sits on a node boundary, capped by `node.FirstAncestorOrSelf<MemberDeclarationSyntax>()`'s own span; header-region clamp applied afterward, scoped to `BaseMethodDeclarationSyntax` members only (D3 explicitly scopes out plain fields/auto-properties — this is a stated decision, not a gap); this is the one place `Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax`/`BaseMethodDeclarationSyntax` is consulted for `get_file`, never crossing out of this class; test green
- Depends on: T05
- Conflicts with: T03, T04, T05 (same production file, sequential edit)
- Complexity: L
- Reversibility: moderate
- Pattern refs: 01-plan.md Decision D3 — the pinned per-edge/header-clamp/hard-ceiling algorithm and the Context7-confirmed `SyntaxNode.FindNode(TextSpan, bool, bool)` overload, implement exactly as written, do not re-derive; src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs:154-175 (`ExtractDeclaredSymbolsAsync`'s existing `GetSyntaxRootAsync`/`GetSemanticModelAsync` null-check pattern) — mirror the same defensive null-check style when fetching the syntax root for this method

### T07 - GetFileTool: new schema, validation, ILanguageProvider wiring
- Files: src/Graphwright.McpServer/Tools/GetFileTool.cs; tests/Graphwright.Tests/McpServer/Tools/GetFileToolTests.cs
- Layer: Presentation
- Step type: behavior
- Test: tests/Graphwright.Tests/McpServer/Tools/GetFileToolTests.cs (new file, created alongside this task; full scenario enumeration is T08's responsibility — this task's own acceptance is the schema/validation/wiring shape those scenarios exercise) — `InputSchema` is `type: object` with exactly the 5 properties `path, start_line, end_line, around_line, context` and `required: ["path"]`
- Acceptance: `public GetFileTool(ILanguageProvider languageProvider)`, `ArgumentNullException.ThrowIfNull(languageProvider)` (mirrors `ListSymbolsTool`'s constructor exactly); `InputSchema` replaces the GW-4 placeholder with the real shape; validation order — required non-blank `path` (rooted/`..`-segment rejected, 01-plan.md Decision D4: own copy of the check, not a shared extraction), `start_line`/`end_line` both-or-neither/positive-integer/`start_line <= end_line`, `around_line` positive-integer if present, `context` positive-integer if present (defaults to `2` when omitted), `start_line`/`end_line` pair mutually exclusive with `around_line` — runs to completion entirely before `ILanguageProvider.GetFileAsync` is ever called; provider exceptions (`WorkspaceNotLoadedException`, `SourceFileNotFoundException`) are never caught, only the mapped success path (`{ file, start_line, end_line, total_lines, content }`) is tool-owned code; `#nullable enable` / `== false`/`== true` / `Async` / `CancellationToken ct` last maintained; test green (by this point the T01→T02→T03 prefix has already landed, so this is a genuine, independently verifiable green build, not a retroactive one)
- Depends on: T03
- Conflicts with: none
- Complexity: L
- Reversibility: moderate
- Pattern refs: src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:67-93 (constructor + `ExecuteAsync` orchestration shape) and :95-118 (`ValidateOptionalPath`'s rooted/`..`-segment body, mirrored into this task's own required-path validator per Decision D4) — keep the `const string INPUT_SCHEMA_JSON` + cached-`JsonElement`-clone pattern and the validate-fully-before-provider-call discipline; src/Graphwright.Domain/Exceptions/InvalidToolArgumentException.cs:14-19 — constructor shape (`argumentName, reason`) for every validation failure raised

### T08 - GetFileToolTests.cs: validation + wiring/pass-through scenarios
- Files: tests/Graphwright.Tests/McpServer/Tools/GetFileToolTests.cs
- Layer: Presentation
- Step type: test
- Test: this task IS the test file (extends the schema/shape assertions T07 already seeded) — a "poison" fake `ILanguageProvider` (`GetFileAsync` throws `InvalidOperationException("must not be called")`) proves the provider is never invoked for: missing/blank/non-string/rooted/`..`-segment `path` (Scenario 12); `start_line` present without `end_line` or vice versa (Scenario 6); `start_line > end_line` (Scenario 7); any of `start_line`/`end_line`/`around_line`/`context` zero, negative, or non-integer (Scenario 8); `start_line`+`end_line`+`around_line` all present together (Scenario 5) — each case asserts `InvalidToolArgumentException` naming the offending argument; a "capturing" fake `ILanguageProvider` records the `GetFileQuery` it received and a canned `FileContentResult` it returns: whole-file call (`path` only) captures `StartLine/EndLine/AroundLine == null`, `Context == 2` (Scenario 1); `start_line=3,end_line=6` captures those exactly (Scenario 2); `around_line=10` with no `context` captures `Context == 2` (Scenario 3); `around_line=10,context=5` captures `Context == 5` (Scenario 4); in every capturing case, the canned `FileContentResult` maps to `{ "result": { "file", "start_line", "end_line", "total_lines", "content" } }` verbatim, including a canned result whose `start_line`/`end_line` already reflect an out-of-the-box expanded range (proving Scenario 11/15's pass-through: the tool does not re-derive or re-clamp what the provider already decided) and a case where `around_line` is set to a value matching a `list_symbols`-style declaration line, asserting the captured query's `AroundLine` equals that value unchanged (Scenario 15's tool-level half); a "throwing" fake `ILanguageProvider` proves `WorkspaceNotLoadedException` (Scenario 14) and `SourceFileNotFoundException` (Scenario 13) propagate out of `ExecuteAsync` unchanged, not caught
- Acceptance: every one of 00-spec.md's 16 scenarios is accounted for either directly in this file or, per 01-plan.md's Success criteria mapping table, in `RoslynLanguageProviderTests` (Scenarios 9/9a/10/11/15's authoritative correctness) — this file's own job is validation-before-provider-call and JSON-mapping fidelity, mirroring `ListSymbolsToolTests`'s existing `PoisonLanguageProvider`/`CapturingLanguageProvider`/`ThrowingLanguageProvider` naming and structure; test green
- Depends on: T07
- Conflicts with: none
- Complexity: L
- Reversibility: trivial
- Pattern refs: tests/Graphwright.Tests/McpServer/Tools/ListSymbolsToolTests.cs:1-70 (`PoisonLanguageProvider`/`CapturingLanguageProvider`/`ThrowingLanguageProvider` fakes and their theory-driven invalid-argument cases) — mirror fake naming, structure, and the "poison proves provider never called" technique exactly, adapted to `GetFileAsync`'s query/result shape

### T09 - Fix ToolDispatcherTests (re-point both GetFileTool stub-behavior tests to FindReferencesTool)
- Files: tests/Graphwright.Tests/McpServer/Dispatch/ToolDispatcherTests.cs
- Layer: Presentation
- Step type: wiring
- Test: this task IS the test fix — `DispatchAsyncReturnsInvalidArgumentEnvelopeForStubToolWithBadArguments` and `DispatchAsyncReturnsInternalEnvelopeWithNotImplementedMessageForStubToolWithValidArguments` (currently lines ~91-121, both currently `new GetFileTool()` with `"path"`) swap to `new FindReferencesTool()` and its required argument `"symbol"` in place of `"path"`; both tests exercise generic `ToolDispatcher`/stub-boundary behavior unrelated to `get_file` specifically, and `FindReferencesTool` remains an untouched single-required-argument not-implemented stub post-GW-6, so it is a like-for-like substitute — same move GW-5 made when it re-pointed this file from `ListSymbolsTool` to `GetFileTool`; all other tests in this file (`FakeSuccessTool`/`FakeThrowingTool`/unknown-tool-name cases) are untouched
- Acceptance: file no longer references `GetFileTool`; all facts in the file pass; test green
- Depends on: T07
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: src/Graphwright.McpServer/Tools/FindReferencesTool.cs — required-argument name `"symbol"` and its stub validation shape, used only to know what valid/invalid `arguments` payloads look like for the substitute tool

### T10 - Fix ToolRegistryTests (GetFileTool needs a fake ILanguageProvider now)
- Files: tests/Graphwright.Tests/McpServer/Registry/ToolRegistryTests.cs
- Layer: Presentation
- Step type: wiring
- Test: this task IS the test fix — `RealStubToolsOutOfOrder()` (currently line 59) constructs `new GetFileTool(new NeverInvokedLanguageProvider())`, reusing the same private nested fake this file already defines for `ListSymbolsTool` (T02 already gave it a compiling `GetFileAsync` member) rather than adding a second fake class; all existing facts in this file pass unchanged
- Acceptance: file compiles against the new constructor; no new fake class introduced — the existing `NeverInvokedLanguageProvider` is reused for both `ListSymbolsTool` and `GetFileTool` in the same array; test green
- Depends on: T07 (which transitively requires T01, T02, T03)
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: tests/Graphwright.Tests/McpServer/Registry/ToolRegistryTests.cs:51-61 (`RealStubToolsOutOfOrder`, this file's own construction site) — add the constructor argument in place, do not restructure the array

### T11 - Fix StubToolTests (drop the GetFileTool row, 3 remaining stubs)
- Files: tests/Graphwright.Tests/McpServer/Tools/StubToolTests.cs
- Layer: Presentation
- Step type: wiring
- Test: this task IS the test fix — `StubTools()` drops the `GetFileTool` row (currently line 16); the remaining 3 rows (`FindReferencesTool`/"symbol", `GetCallGraphTool`/"symbol", `SearchTool`/"query") and every test generated from them are unchanged and stay green, now covering exactly the 3 tools that remain genuine not-implemented stubs
- Acceptance: file no longer references `Graphwright.McpServer.Tools.GetFileTool`; the class-level comment (currently line 11, "Covers the 4 remaining stub tools...") is updated to "Covers the 3 remaining stub tools (`ListSymbolsTool` and `GetFileTool` have real behavior and are tested separately)"; test green
- Depends on: T07
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: this file's own `StubTools()`/`ToolsWithRequiredArguments()`/`ToolsWithExpectedNames()` generators (lines 14-36) — remove exactly the one row, keep the generator shape identical for the remaining 3

### T12 - End-to-end WORKSPACE_NOT_LOADED composition test for get_file
- Files: tests/Graphwright.Tests/McpServer/DependencyInjection/GetFileCompositionTests.cs
- Layer: Presentation
- Step type: test
- Test: tests/Graphwright.Tests/McpServer/DependencyInjection/GetFileCompositionTests.cs (this task IS the test) — build a full container via `InfrastructureModule.Instance.RegisterServices` + `McpServerModule.Instance.RegisterServices` + `AddLogging()` (mirrors `ListSymbolsCompositionTests`'s existing two-module composition helper, T10 from GW-5); resolve `ToolDispatcher`, dispatch `mcp__gitnexus__get_file` with `{"path":"src/Graphwright.Domain/Foo.cs"}`; assert the envelope is `ok: false`, `error.code == "WORKSPACE_NOT_LOADED"`, `error.retryable == true` (Scenario 14, proven through the exact composition-root path `Program.cs` uses, same as GW-5's T14 did for `list_symbols`)
- Acceptance: a comment in the test explicitly states that production wiring is `NotLoaded()` by design (00-spec.md Out of scope / 01-plan.md Scope framing) and this test does NOT prove Scenarios 1-13/15 end-to-end (those remain proven against in-memory fixtures per T03-T08); test green
- Depends on: T07 (which transitively requires T03)
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: tests/Graphwright.Tests/McpServer/DependencyInjection/ListSymbolsCompositionTests.cs:1-39 — reuse its two-module composition helper pattern rather than duplicating it; the file this task creates is `get_file`'s sibling, not a merge into the existing file, to keep each tool's end-to-end proof independently readable

---

## Dependency graph (critical path bolded)

```
T01 ─> T02 ─> T03 ──> T04 ──> T05 ──> T06
              │
              └─> T07 ──┬─> T08
                         ├─> T09
                         ├─> T10
                         ├─> T11
                         └─> T12
```

`T01 → T02 → T03` is a strict, non-negotiable prefix (see the note above the Checklist): no test
in the whole `Graphwright.Tests` assembly is verifiable until all three land, because
`RoslynLanguageProvider` and two test fakes all implement the interface T01 changes. Every other
task depends on T03 (T04-T06 directly; T07 directly, since `GetFileTool` needs a green build to
verify against; T08-T12 transitively through T07).

**Critical path**: T01 → T02 → T03 → T04 → T05 → T06 (6 tasks — the foundation-landing prefix plus
the Roslyn snippet/boundary-snapping chain is the deepest dependency chain in this story).
T07-rooted tasks (T08, T09, T10, T11, T12) are each ≤2 deep from T03 and do not gate the critical
path once T03 lands. The story is not "done" until every branch completes, but T06's branch alone
determines the longest single dependency chain.

## Complexity summary

| Complexity | Tasks | Count |
|---|---|---|
| S | T02, T05, T09, T10, T11, T12 | 6 |
| M | T01, T03, T04 | 3 |
| L | T06, T07, T08 | 3 |

Total: **12 tasks** (6 S + 3 M + 3 L).
