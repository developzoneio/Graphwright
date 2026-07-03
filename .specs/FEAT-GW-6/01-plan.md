---
id: FEAT-GW-6
type: feature
phase: plan
created: 2026-07-03
spec: .specs/FEAT-GW-6/00-spec.md
impact: .specs/FEAT-GW-6/03-decisions.md
---

# FEAT-GW-6 — Implementation plan: Tool: get_file (content + bounded snippet)

## Scope framing (confirmed against 03-decisions.md, verified on disk 2026-07-03)

`03-decisions.md` confirms: `ILanguageProvider`/`RoslynLanguageProvider`/`RoslynWorkspaceSnapshot`
already exist (GW-5), production DI already registers `RoslynLanguageProvider` over
`RoslynWorkspaceSnapshot.NotLoaded()` unconditionally, and `GetFileTool` is today a
no-constructor-argument stub. This story is scoped exactly like GW-5 (00-spec.md "Out of
scope"): real logic implemented and tested against in-memory `Solution`/`SourceText` fixtures via
`RoslynWorkspaceTestFixtures`; no `MSBuildWorkspace`, no real project loading. Scenario 14
(`WORKSPACE_NOT_LOADED`) remains the only end-to-end-provable path against the real DI graph.

**The one wrinkle GW-6 has that GW-5 did not**: GW-5 introduced `ILanguageProvider` from
scratch, so there was no existing implementer to break. GW-6 *extends* an interface that already
has one production implementer (`RoslynLanguageProvider`) and four test-only implementers
(`PoisonLanguageProvider`/`CapturingLanguageProvider`/`ThrowingLanguageProvider` in
`ListSymbolsToolTests.cs`, `NeverInvokedLanguageProvider` in `ToolRegistryTests.cs`). Adding
`GetFileAsync` to `ILanguageProvider` (T01) breaks compilation of all five until each gains the
new member. This plan makes that breakage a first-class, immediately-following task (T02) rather
than an implementer discovering it mid-stream — see Sequencing rationale.

## Decisions (pinned, not left to implementation-time guessing)

### D1 — Content slicing (whole-file / explicit-range modes)

`content` for a resolved `[startLine, endLine]` (1-based, inclusive) is:

```csharp
var sourceText = await document.GetTextAsync(ct);
var span = TextSpan.FromBounds(sourceText.Lines[startLine - 1].Start, sourceText.Lines[endLine - 1].End);
var content = sourceText.GetSubText(span).ToString();
```

This includes every character between the two absolute offsets — i.e. all line breaks *between*
the selected lines are preserved verbatim — but stops at `endLine`'s own `.End` (before that
line's own trailing line-break, if any), so no extra blank line is invented past the requested
range. `total_lines = sourceText.Lines.Count` (00-spec.md "Output" — the same text model
`GetLineSpan` already relies on elsewhere, not a custom line-splitting implementation).

**Alternatives considered**: joining `sourceText.Lines[i].ToString()` with `Environment.NewLine` —
rejected, because it would normalize a file's actual line-ending style (`\n` vs `\r\n`) to the
test-runner's OS default instead of preserving the source's own bytes, silently failing on any
fixture with `\n` line endings run on Windows or vice versa.

As with GW-5's Decision D1 (signature format): T03's tests assert the *actual* compiled output
against a fixture with a known line-ending style; if the exact character sequence doesn't match
what's written above once compiled, the test (not the slicing rule) is what adjusts, and the
confirmed behavior is recorded here.

### D2 — Mode selection and the tool/provider validation split

`GetFileQuery` carries all four optional fields (`StartLine`, `EndLine`, `AroundLine`, `Context`)
plus the required `Path`; the provider infers mode from which are non-null, exactly mirroring
`ListSymbolsQuery`'s "caller guarantees validity, provider does not re-validate query structure"
convention (03-decisions.md "Conventions observed"):

- **Tool-owned validation** (no file access needed, purely structural — `INVALID_ARGUMENT` before
  any Roslyn/file-read work, Scenarios 5/6/7/8/12): required `path` present/non-blank/not-rooted/
  no `..` segment; `start_line`/`end_line` both-or-neither, both positive integers, `start_line <=
  end_line`; `around_line` positive integer if present; `context` positive integer if present;
  `start_line`/`end_line` pair and `around_line` mutually exclusive. `context` defaults to `2`
  when omitted (mirrors `ListSymbolsTool`'s `max_results` default-then-clamp precedent, but here
  there is no clamp — `context` is just an int with a default).
- **Provider-owned validation** (requires the actual file — `total_lines` is only known after
  resolving+reading it, Scenarios 9/9a/10/11/15): `end_line` partial-overflow clamp vs.
  `start_line`-itself-out-of-range reject (9/9a); snippet-window edge clamping at file boundaries
  (10); `SyntaxTree`-based boundary-snapping (11/15).

This split is why `GetFileToolTests` (T08) authors its own tests for Scenarios 1-8/12/13/14
directly (using fakes, no real Roslyn needed) while Scenarios 9/9a/10/11/15's *authoritative*
correctness proof lives in `RoslynLanguageProviderTests` (T04/T05/T06) — GetFileTool only proves
those flow through the JSON mapping unchanged, it does not re-derive them. This mirrors GW-5's own
split between `ListSymbolsToolTests` (schema/validation) and `RoslynLanguageProviderTests`
(Roslyn-specific extraction correctness) — see "Success criteria mapping" below.

### D3 — Boundary-snapping algorithm (Scenario 11/15), pinned per spec + verified Roslyn API

Confirmed via Context7 (`/dotnet/roslyn`): `SyntaxNode.FindNode(TextSpan span, bool
findInsideTrivia = false, bool getInnermostNodeForTie = false)` returns the innermost node whose
span contains `span`. Per-edge algorithm (00-spec.md Scenario 11, restated concretely):

1. Compute the raw window (`[rawStart, rawEnd]`) from T05 (context-window math, already clamped to
   `[1, totalLines]`).
2. **Per-edge expansion loop**, top edge (symmetric for bottom edge against `.End`):
   - `lineSpan = sourceText.Lines[currentStart - 1].Span` (excludes the line's own break).
   - `node = syntaxRoot.FindNode(lineSpan, getInnermostNodeForTie: true)`.
   - `nodeStartLine = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1`.
   - If `nodeStartLine < currentStart` (the raw edge splits this node — it doesn't sit at the
     node's own boundary): set `currentStart = nodeStartLine`, and repeat (the new, earlier edge
     may itself split a *larger* enclosing node — e.g. an `if` splits a block which splits a
     method body).
   - Stop when `nodeStartLine == currentStart` (edge already sits on a boundary) — this is why
     Scenario 3/4's already-clean windows are never expanded.
   - **Hard ceiling**: the loop never sets `currentStart`/`currentEnd` earlier/later than the
     enclosing `MemberDeclarationSyntax`'s own `GetLineSpan()` bounds, found via
     `node.FirstAncestorOrSelf<MemberDeclarationSyntax>()` (or `node` itself if it already is one).
3. **Header-region clamp** (Scenario 15), checked *after* per-edge expansion, only against
   `BaseMethodDeclarationSyntax` (covers `MethodDeclarationSyntax`, `ConstructorDeclarationSyntax`,
   operator/destructor declarations — every member shape with a `Body`/`ExpressionBody`/
   `SemicolonToken` triple) that the expanded window overlaps: if the window's start line falls at
   or before that member's own header (attributes through parameter list/constraint clauses) and
   the window would otherwise extend into the member's `Body`/`ExpressionBody`, clamp `endLine` to
   the line of the token immediately preceding whichever of `Body`/`ExpressionBody`/
   `SemicolonToken` is non-null on that member (in that priority order) — never pulling in the
   full body. Member shapes without a `Body`/`ExpressionBody`/`SemicolonToken` triple (plain
   fields, simple auto-properties) do not participate in this second check — only the per-edge
   `FindNode` expansion applies to them, which is sufficient because the spec's own worked example
   (Scenario 15) is a multi-line **method signature**.

**Risk carried forward from 03-decisions.md**: "Zero precedent in the codebase for this exact
logic... correctness depends on implementation-time testing against spec Scenarios 11 and 15." T06
is flagged High risk below; its fixtures must include both a body-region multi-line-statement case
(Scenario 11) and a header-region multi-line-signature case (Scenario 15) as the impact analysis
already recommends.

### D4 — No shared path-validation helper extraction

00-spec.md's "Input" section says `path` is "validated the same way `ListSymbolsTool.
ValidateOptionalPath` validates `path` today... except `path` is required here, not optional" —
this is a mirror-the-logic instruction, not a share-the-code instruction, and
`ListSymbolsTool.ValidateOptionalPath` is `private` to another class. **Decided**: `GetFileTool`
gets its own `ValidateRequiredPath` with the same rooted/`..`-segment body, duplicated rather than
extracted into a shared helper. Extracting a shared utility now would be an opportunistic refactor
inside a feature spec (constitution §6 forbids this without its own spec) — noted explicitly so a
reviewer doesn't flag the duplication as an oversight.

### D5 — No new Domain exceptions (confirmed, same as GW-5's D3)

`WorkspaceNotLoadedException`, `SourceFileNotFoundException`, `InvalidToolArgumentException` are
reused unchanged; `ExceptionEnvelopeMapper` needs no changes (00-spec.md Success criteria,
03-decisions.md "Low risk — exception mapping").

## OQ-8 (path-casing) — inherited, not re-opened

00-spec.md's own Open questions section already resolves this story's obligation concretely:
`get_file`'s `file` output field reuses `list_symbols`'s existing path-echoing convention
verbatim (the validated, forward-slash input path, echoed unchanged — same as `DeclaredSymbol.
File`). No task below introduces a new or divergent casing scheme. The underlying project-wide
ADR remains genuinely open and out of this plan's scope.

## Success criteria mapping (which test file proves which scenario)

| Scenario | Authoritative test | Task |
|---|---|---|
| 1 (whole file) | `GetFileToolTests` (mapping) + `RoslynLanguageProviderTests` (content/total_lines) | T03, T08 |
| 2 (explicit range) | `RoslynLanguageProviderTests` | T04 |
| 3 (default context) | `RoslynLanguageProviderTests` | T05 |
| 4 (explicit context) | `RoslynLanguageProviderTests` | T05 |
| 5 (mutual exclusivity) | `GetFileToolTests` | T08 |
| 6 (partial range) | `GetFileToolTests` | T08 |
| 7 (start > end) | `GetFileToolTests` | T08 |
| 8 (non-positive/wrong type) | `GetFileToolTests` | T08 |
| 9 (partial overflow clamp) | `RoslynLanguageProviderTests` | T04 |
| 9a (entirely out of bounds) | `RoslynLanguageProviderTests` | T04 |
| 10 (snippet edge clamp) | `RoslynLanguageProviderTests` | T05 |
| 11 (no mid-statement cut) | `RoslynLanguageProviderTests` (correctness) + `GetFileToolTests` (pass-through) | T06, T08 |
| 12 (path validation) | `GetFileToolTests` | T08 |
| 13 (FILE_NOT_FOUND) | `GetFileToolTests` (propagation) + `RoslynLanguageProviderTests` (raised) | T03, T08 |
| 14 (WORKSPACE_NOT_LOADED) | `GetFileToolTests` (propagation) + `RoslynLanguageProviderTests` (raised) + `GetFileCompositionTests` (end-to-end) | T03, T08, T12 |
| 15 (pairs with list_symbols) | `RoslynLanguageProviderTests` (header-clamp correctness) + `GetFileToolTests` (around_line forwarded unchanged) | T06, T08 |

This table is the plan's answer to "all 16 scenarios pass as automated tests against
`GetFileTool`" (00-spec.md Success criteria) — the guarantee spans the whole test suite the way
GW-5's did, not a single file.

## Risks flagged by 03-decisions.md — how this plan addresses them

- **High risk — boundary-snapping logic has zero precedent.** Addressed by D3 (algorithm pinned
  concretely, Roslyn API confirmed via Context7) and T06 being its own isolated task with both a
  body-region and a header-region fixture, so a wrong implementation fails a scoped test rather
  than surfacing confusingly in T08's tool-level pass-through tests.
- **High risk — `GetFileTool` constructor change breaks 4 test call sites.** Addressed by T09
  (`ToolDispatcherTests.cs:93,110`), T10 (`ToolRegistryTests.cs:59`), T11 (`StubToolTests.cs:16`) —
  one task each, same as GW-5's T11/T12/T13 precedent.
- **Medium risk — `ToolDispatcherTests.cs` asserts "not implemented" on `GetFileTool`.** Addressed
  by T09 re-pointing *both* of that file's `GetFileTool`-based tests (the invalid-argument case and
  the not-implemented case) to `FindReferencesTool` — the same move GW-5 made when it re-pointed
  from `ListSymbolsTool` to `GetFileTool`.
- **Medium risk — `StubToolTests.cs` assumes 4 remaining stubs.** Addressed by T11.
- **Risk not flagged by 03-decisions.md, found during planning — extending `ILanguageProvider`
  breaks its two existing *test-fake* implementers, not just `GetFileTool`'s call sites.**
  `ListSymbolsToolTests.cs`'s `PoisonLanguageProvider`/`CapturingLanguageProvider`/
  `ThrowingLanguageProvider` and `ToolRegistryTests.cs`'s `NeverInvokedLanguageProvider` all
  implement `ILanguageProvider` with only `ListSymbolsAsync`; adding `GetFileAsync` to the
  interface (T01) is a compile break for all four until each gains the new member. Addressed by
  T02, sequenced immediately after T01 specifically because of this.
- **Low risk — RoslynWorkspaceTestFixtures reuse; exception mapping; DI wiring.** Unchanged from
  GW-5's confirmation — no new fixture machinery, no new exception type, and `GetFileTool` piggy-
  backs on `InfrastructureModule`'s already-unconditional `ILanguageProvider` registration with
  zero DI changes needed (unlike GW-5, which had to add that registration in the first place).

## Phased overview

| Phase | Tasks | Delivers |
|---|---|---|
| **Foundation** | T01–T02 | `GetFileQuery`/`FileContentResult` DTOs + `ILanguageProvider.GetFileAsync` (Application); the two pre-existing `ILanguageProvider` test fakes updated to compile against the new member |
| **Behavior** | T03–T07 | `RoslynLanguageProvider.GetFileAsync` built up scenario-by-scenario (resolution/errors/whole-file → explicit range → snippet base window → boundary-snapping); `GetFileTool` rewritten to the real schema, validated, wired |
| **Wiring** | T09–T11 | The 3 broken pre-existing test files (beyond the fakes fixed in T02) repaired |
| **Test/Polish** | T08, T12 | `GetFileToolTests.cs` created; end-to-end `WORKSPACE_NOT_LOADED` composition test |

## Sequencing rationale

1. **T01 before everything.** Every other task either implements the new interface member (T03)
   or consumes it (T02, T07).
2. **T01 → T02 → T03 is a strict, non-negotiable prefix, not just "T02 immediately follows T01."**
   `Graphwright.Tests` is a single assembly that references `Graphwright.Infrastructure` directly
   (`RoslynLanguageProviderTests` uses Infrastructure types), so the *entire* test suite — not only
   the files that changed — fails to build the moment T01 adds `GetFileAsync` to `ILanguageProvider`,
   because both `RoslynLanguageProvider` (fixed in T03) and two test-fake files (fixed in T02) stop
   satisfying the interface simultaneously. `02-tasks.md` encodes this as a strict dependency chain
   (T02 depends on T01; T03 depends on T01 **and** T02) rather than two independent 1-deep
   branches off T01: no task's tests, including T01's own DTO round-trip tests, are actually
   verifiable green until all three have landed. Every later task (T04 onward) depends on T03
   either directly or transitively, precisely because T03 is the point the whole solution first
   compiles again.
3. **T03 → T04 → T05 → T06 is a strictly sequential single-writer chain** on
   `RoslynLanguageProvider.cs`, same reasoning as GW-5's T04→T07 chain: resolution/errors/whole-
   file first (something safe to build on), then explicit-range math (needs `total_lines` from
   T03's whole-file read to clamp/reject against), then snippet base window (simplest correct
   window math), then boundary-snapping last (the one step needing the `SyntaxTree`, building on a
   window T05 already computes correctly for the non-snapped case).
4. **T07 (`GetFileTool`) is developed against fakes, not the finished T04–T06 snippet logic** —
   like GW-5's T08, its own behavior only needs the interface (T01); its `Depends on: T03` in
   `02-tasks.md` exists so it is verified against a genuinely green build (the T01→T02→T03 prefix),
   not because it consumes T04–T06's snippet-math work. It can be *written* in parallel with
   T04–T06 once T03 lands, even though its own logic never calls into that code path.
5. **T08/T09/T10/T11 all depend on T07** (the constructor-signature change is what breaks
   T09/T10/T11; T08 is new and just needs the real schema to test against) — order among these
   four doesn't matter.
6. **T12 (end-to-end) depends on T07**, which transitively carries the real `WorkspaceNotLoadedException`
   path (T03) and the real tool (T07) it needs.
7. **No `mcp-contract.md` task.** 00-spec.md "Out of scope" explicitly carries this forward as "a
   separate cross-cutting task, not something this spec needs a decision on to proceed" — same
   posture as GW-5's own OQ-7. Not doing it here is a decision, not an oversight.

**Critical path**: T01 → T02 → T03 → T04 → T05 → T06 (6 tasks — the T01→T02→T03 foundation-landing
prefix plus the Roslyn snippet/boundary chain is the longest dependency chain in this story).
T07-rooted tasks (T08–T11) are each ≤2 deep from T03 and do not gate the critical path once T03
lands; T12 depends on T07 (transitively T03) and does not need the full T04–T06 chain.

## Risks

| Risk | Mitigation |
|---|---|
| **`SyntaxNode.FindNode(TextSpan)` behaves differently than the pinned per-edge algorithm assumes** (e.g. a blank line's `Span` is zero-length and behaves unexpectedly) | D3's algorithm pins the exact overload and parameters (confirmed via Context7, not guessed); T06's acceptance requires testing against the *actual* compiled behavior on both a body-region and header-region fixture, same "assert actual runtime output, fix the test not the format" discipline as GW-5's D1. |
| **`BaseMethodDeclarationSyntax`-only header-clamp scope misses a member shape the spec intends to cover** (e.g. a multi-line property declaration) | Scoped deliberately to what Scenario 15's own worked example is (a multi-line method signature) — D3 states explicitly that other member shapes only get per-edge expansion, not the header clamp, so this is a stated scope decision, not a silent gap. |
| **Content-slicing (D1) line-ending assumption is wrong for a given fixture** | Same "assert actual, don't guess" discipline as GW-5's D1; T03's tests assert against a fixture with a pinned, known line-ending style. |
| **Extending `ILanguageProvider` breaks fakes the impact analysis didn't enumerate** | Found during planning (see "Risks flagged... found during planning" above); T02 exists specifically because of this, sequenced right after T01. |
| **5 pre-existing test files/fakes break on the interface/constructor changes** | Enumerated explicitly (T02, T09, T10, T11) rather than discovered mid-implementation. |
| **`ExceptionEnvelopeMapper`/domain exceptions reused unchanged** | No task edits them (D5); T03's and T07's acceptance criteria require raising the *existing* exception types with no new mapping logic. |

## Constitution check (plan level)

- **§1.1 dependency direction / inversion** — `ILanguageProvider.GetFileAsync` is defined in
  Application (T01) and implemented in Infrastructure (T03–T06); `GetFileTool` (McpServer, T07)
  depends only on the Application interface, never on `Microsoft.CodeAnalysis.*` — verified by
  task-level acceptance (T01: zero new Roslyn references under `Graphwright.Application`).
- **§1.1 cross-layer data (DTOs only)** — `GetFileQuery`/`FileContentResult` (T01) are the only
  shapes crossing the Infrastructure→Application/McpServer boundary; no Roslyn `SourceText`/
  `SyntaxNode`/`Document` type ever leaves `RoslynLanguageProvider.cs`.
- **§6 catch-and-swallow** — no task adds a `catch`; `RoslynLanguageProvider` and `GetFileTool`
  raise, never catch (D5); the existing `ToolDispatcher` boundary (unchanged) does the only
  catching in the system.
- **§6 static singletons holding state** — no new stateful singleton introduced; `GetFileTool`
  is constructor-injected exactly like `ListSymbolsTool`.
- **§6 TODO/HACK** — no task leaves a code-comment marker; the one genuinely deferred piece
  (production wiring stays `NotLoaded()`, per Scope framing) is an honest exception path plus this
  spec's own "Out of scope" note, not a comment.
- **§6 opportunistic refactor** — D4 explicitly declines to extract a shared path-validation
  helper this story, precisely to avoid this.
- **§5.1 spec-driven** — this plan derives directly from the approved 00-spec.md; no scope added
  beyond what 00-spec.md's Decided language and Out-of-scope section already settle.
- **§3 quality bars** — every new/changed production file (T01, T03–T07) ships with a dedicated or
  extended test in the same task; the constitution's `<<80>>%` placeholder remains unfilled
  (carried-forward gap from GW-4/GW-26/GW-5, not resolved by this plan) but every task targets full
  behavioral coverage of its own acceptance criteria regardless of the numeric gate.
