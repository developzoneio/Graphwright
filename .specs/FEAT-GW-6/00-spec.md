---
id: FEAT-GW-6
type: feature
status: in-progress
created: 2026-07-03
ticket: GW-6
ticket_url: https://trminhtrong.atlassian.net/browse/GW-6
parent_epic: GW-1
title: "Tool: get_file (content + bounded snippet)"
---

# FEAT-GW-6 — Tool: get_file (content + bounded snippet)

> Second real (non-stub) tool logic on top of the GW-5 `ILanguageProvider` seam. `get_file`
> returns a file's whole content, an explicit 1-based inclusive line range, or a bounded
> snippet around a line — backed by file text plus Roslyn `SyntaxTree` for boundary-aware
> snippet framing. It is the tool `sd-code-explorer`/`sd-debugger` pair with a `file:line`
> produced by `list_symbols` (or `find_references`/`get_call_graph` later) to actually read
> the code at a definition site — the "definition routine" named in the ticket's acceptance
> criteria.

## Why

GW-5 shipped the first real tool (`list_symbols`) and, with it, the first concrete
`ILanguageProvider` seam (`Graphwright.Application.LanguageProviders.ILanguageProvider`,
`Graphwright.Infrastructure.LanguageProviders.RoslynLanguageProvider`,
`RoslynWorkspaceSnapshot`). `get_file` is still a stub today
(`src/Graphwright.McpServer/Tools/GetFileTool.cs:49` throws `ToolNotImplementedException`
after validating only that `path` is a non-empty string). That matters because:

1. **`list_symbols` alone is not useful.** It returns a `file:line` per declared symbol
   (00-spec.md for FEAT-GW-5, "Output"), but nothing today lets a Specwright agent turn that
   `file:line` into readable source. The ticket's own acceptance criterion — "Pairs with a
   `file:line` from `list_symbols` for the `definition` routine" — names this directly:
   `get_file` is the second half of the single most common code-explorer/debugger action
   (find a symbol, then read it).
2. **It is one of the 5 frozen tools and currently the least-done stub after `list_symbols`.**
   CLAUDE.md "MCP tool surface" freezes `get_file` as backed by "File + `SyntaxTree` map";
   until it has real logic, the Month 1 Definition of Done item "5 tools exposed... schemas
   match `mcp-contract.md` exactly" cannot close, and `sd-reviewer`/`sd-debugger` verification
   against `smp-jt-services` has nothing to read source with.
3. **Business value**: this is the smallest increment that converts "Graphwright can name
   where code is" (GW-5) into "Graphwright can show you the code" — the second of the two
   halves of a GitNexus-equivalent read path, per CLAUDE.md's drop-in-replacement premise.

## What

`get_file` returns a file's content in one of three mutually exclusive modes, selected by
which optional arguments are present, plus the always-required `path`:

- **Whole file** (no range/around arguments): the entire file content, `start_line = 1`,
  `end_line = total_lines`.
- **Explicit range** (`start_line` + `end_line`, always given as a pair): the exact 1-based
  inclusive line range requested. Partial overflow (`end_line` greater than `total_lines`, but
  `start_line` still in bounds) clamps `end_line` down to `total_lines` (Scenario 9). A range
  that names no real line at all (`start_line` itself greater than `total_lines`) is rejected
  as `INVALID_ARGUMENT` rather than silently clamped (Scenario 9a) — see the note under
  Scenario 9a for the distinction between the two cases.
- **Bounded snippet** (`around_line` + optional `context`, default `2`): a window of
  `around_line - context` to `around_line + context` inclusive — 5 lines by default — expanded
  outward only as far as needed to avoid cutting a multi-line statement/expression in half; a
  window whose edges already fall on complete-statement boundaries is served unchanged (no
  expansion). ("Outward only, never inward" describes this per-edge statement/expression
  check specifically; the separate header-region clamp below is a distinct case that bounds
  the window to the member and can move an edge inward relative to the raw request — see
  Scenario 15.) Boundary-snapping is implemented with Roslyn's `SyntaxNode.FindNode(TextSpan)`,
  checked independently per edge: for the top edge, if the innermost node containing the
  `start` line begins on an earlier line (i.e. `start` splits that node rather than sitting at
  its beginning), the served `start_line` extends outward to that node's own start line;
  symmetrically for the bottom edge and `end_line` against that node's own end line. If the
  window instead overlaps a member's header region (attributes, modifiers, return type,
  identifier, type parameters, parameter list, constraint clauses), the served window clamps
  to that member's full header span instead — see Scenario 11 and Scenario 15 for the worked
  cases. This is the one place the Roslyn `SyntaxTree` is actually consulted, not just file
  text.

`start_line`/`end_line` as a pair and `around_line` are mutually exclusive; supplying both
shapes in the same call is an `INVALID_ARGUMENT` (Scenario 5). No range/around argument at all
means whole file (ticket: "No range/around → whole file").

This story replaces `GetFileTool`'s current stub schema/behavior (`path`-only, always throws
`ToolNotImplementedException`, `src/Graphwright.McpServer/Tools/GetFileTool.cs:19-30,49`) with
the real schema and logic below, the same way GW-5 replaced `ListSymbolsTool`'s placeholder
schema (FEAT-GW-5 00-spec.md "What").

**Input:**
- `path` (required) — relative, forward-slash path to a file; validated the same way
  `ListSymbolsTool.ValidateOptionalPath` validates `path` today
  (`src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:95-118`: reject rooted paths and `..`
  segments) — except `path` is required here, not optional.
- `start_line?` / `end_line?` — both-or-neither; 1-based inclusive.
- `around_line?` — 1-based line to center the snippet on.
- `context?` — integer, default `2`, only meaningful with `around_line`.

**Output** — `{ file, start_line, end_line, total_lines, content }`. `file` echoes the
validated, forward-slash, project-root-relative input path (CLAUDE.md cross-cutting rule 2).
`start_line`/`end_line` describe the actual range served (post-clamp/post-boundary-expansion),
not necessarily the literal requested numbers. `total_lines` is the file's total line count,
computed via Roslyn's `SourceText.Lines.Count` — the same text model `GetLineSpan` already
relies on for `start_line`/`end_line` elsewhere in this codebase, not a custom line-splitting
implementation. `content` is the raw source text for `start_line..end_line` inclusive.

### Scenario 1 — Whole file (no range/around arguments)

```gherkin
Given a workspace whose index is loaded
And a source file with N lines exists at a known relative path
When get_file is called with only path set to that file
Then the response is "ok": true
And result.start_line is 1
And result.end_line equals result.total_lines
And result.total_lines equals N
And result.content is the file's entire text
```

### Scenario 2 — Explicit start_line/end_line range

```gherkin
Given a workspace whose index is loaded
And a source file with at least 10 lines exists at a known relative path
When get_file is called with start_line 3 and end_line 6
Then result.start_line is 3
And result.end_line is 6
And result.content is exactly lines 3 through 6 inclusive
```

### Scenario 3 — Bounded snippet with default context (5-line window)

```gherkin
Given a workspace whose index is loaded
And a source file with at least 20 lines, where lines 8-12 form a single simple statement each
When get_file is called with around_line 10 and no context argument
Then result.start_line is 8
And result.end_line is 12
And result.content spans exactly that 5-line window
```

### Scenario 4 — Bounded snippet with explicit context

```gherkin
Given a workspace whose index is loaded
And a source file with at least 20 simple single-line statements
When get_file is called with around_line 10 and context 5
Then result.start_line is 5
And result.end_line is 15
And result.content spans exactly that 11-line window
```

### Scenario 5 — Mutual exclusivity: range pair and around_line together

```gherkin
Given a workspace whose index is loaded
When get_file is called with start_line, end_line, AND around_line all present
Then the response is "ok": false
And error.code is "INVALID_ARGUMENT"
And error.retryable is false
And no Roslyn or file-read work is attempted for the invalid request
```

### Scenario 6 — Partial range is rejected, not treated as open-ended

```gherkin
Given a workspace whose index is loaded
When get_file is called with only start_line (no end_line), or only end_line (no start_line)
Then the response is "ok": false
And error.code is "INVALID_ARGUMENT"
And error.retryable is false
```

(Decided: the ticket frames `start_line`/`end_line` as "an explicit 1-based inclusive range,"
i.e. a pair, not two independently optional bounds; a lone `start_line` without `end_line` is
not "from start_line to EOF" — it is a validation error.)

### Scenario 7 — start_line greater than end_line

```gherkin
Given a workspace whose index is loaded
When get_file is called with start_line greater than end_line
Then the response is "ok": false
And error.code is "INVALID_ARGUMENT"
And error.retryable is false
```

### Scenario 8 — Non-positive or wrong-type line arguments are rejected

```gherkin
Given a workspace whose index is loaded
When get_file is called with start_line, end_line, around_line, or context that is zero,
  negative, or not an integer
Then the response is "ok": false
And error.code is "INVALID_ARGUMENT"
And error.retryable is false
```

### Scenario 9 — Explicit range partially out of bounds is clamped

```gherkin
Given a workspace whose index is loaded
And a source file with N lines
When get_file is called with end_line greater than N (start_line within bounds)
Then result.end_line is N, not the out-of-bounds requested value
And result.content spans start_line through N
```

(Decided: clamp rather than `INVALID_ARGUMENT`, for symmetry with `ListSymbolsTool`'s
`max_results` clamp-not-reject precedent,
`src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:182-200`. This only applies when
`start_line` is still in bounds — contrast Scenario 9a, where `start_line` itself is out of
range.)

### Scenario 9a — Explicit range entirely beyond end-of-file

```gherkin
Given a workspace whose index is loaded
And a source file with N lines
When get_file is called with start_line greater than N (so the entire requested range names
  no real line)
Then the response is "ok": false
And error.code is "INVALID_ARGUMENT"
And error.retryable is false
```

(Distinguished from Scenario 9: Scenario 9's `start_line` is still within bounds, so
clamping `end_line` down to `N` still serves a range the caller meaningfully asked for. Here
`start_line` itself is beyond `N`, so the entire requested range names no real line at all.
Decided: reject rather than clamp, consistent with CLAUDE.md "No invented data" — silently
returning a manufactured empty range at a line the caller never named would mask a caller bug
(e.g. an off-by-one against `total_lines`) instead of surfacing it.)

### Scenario 10 — Bounded snippet near start-of-file or end-of-file clamps the window

```gherkin
Given a workspace whose index is loaded
And a source file with N lines
When get_file is called with around_line 1 (or around_line N) and the default context
Then result.start_line is at least 1 (never less)
And result.end_line is at most N (never more)
And the window is not artificially padded to force a fixed 5-line count past the file's edges
```

### Scenario 11 — Bounded snippet never cuts a multi-line statement/expression in half

```gherkin
Given a workspace whose index is loaded
And a source file where the naive around_line +/- context window would end partway through
  a multi-line statement or expression (e.g. a method call split across 3 lines)
When get_file is called with around_line centered inside that multi-line construct
Then result.start_line and result.end_line expand outward (never inward) just far enough to
  include the whole statement/expression
And result.content never begins or ends mid-expression
```

(This is the one behavior that requires the `SyntaxTree`, not just file text. Decided
boundary-snapping rule, checked independently per edge via `SyntaxNode.FindNode(TextSpan)`:
for the edge that falls inside this multi-line construct (e.g. the 3-line method call), the
innermost node containing that edge's line begins or ends on a different line than the raw
edge — i.e. the raw edge splits the node rather than sitting at its boundary — so the served
edge extends outward to that node's own start/end line. A window whose edges already sit on
complete-statement boundaries (no node split) is left unchanged, which is why this rule does
not also expand Scenario 3's or Scenario 4's already-clean windows. Hard ceiling: expansion
never crosses into a sibling member — it never expands past the enclosing
`MemberDeclarationSyntax`'s own full span. See Scenario 15 for the header-region case this
per-edge rule alone doesn't cover.)

### Scenario 12 — Path validation: rooted or traversal path is rejected

```gherkin
Given a workspace whose index is loaded
When get_file is called with a path that is rooted/absolute, or contains a ".." segment
Then the response is "ok": false
And error.code is "INVALID_ARGUMENT"
And error.retryable is false
```

(Mirrors `ListSymbolsTool.ValidateOptionalPath`,
`src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:103-115`, applied to a required rather
than optional `path`.)

### Scenario 13 — Well-formed path that does not exist in the workspace

```gherkin
Given a workspace whose index is loaded
When get_file is called with a path that is under the workspace root and well-formed, but
  names a file that does not exist in the workspace
Then the response is "ok": false
And error.code is "FILE_NOT_FOUND"
And error.retryable is false
```

(Consistent with the forward reference already recorded in FEAT-GW-5's spec: "the same code
`get_file` is expected to use for its analogous case, via the existing
`SourceFileNotFoundException`," FEAT-GW-5 00-spec.md Scenario 11a.)

### Scenario 14 — Workspace not ready yet

```gherkin
Given the server has started but the workspace index has not finished loading
When get_file is called with any arguments
Then the response is "ok": false
And error.code is "WORKSPACE_NOT_LOADED"
And error.retryable is true
And no partial or best-effort content is returned
```

### Scenario 15 — Pairs with list_symbols for the "definition" routine

```gherkin
Given a workspace whose index is loaded
And list_symbols has returned a result with a { file, line } for a declared symbol
When get_file is called with path set to that file and around_line set to that line
Then the response is "ok": true
And result.content includes the full declaration at that line, not a fragment cut off
  mid-signature
```

(This is the ticket's named acceptance criterion — "Pairs with a `file:line` from
`list_symbols` for the `definition` routine" — expressed as an integration scenario across
both tools rather than a unit-level assertion on `get_file` alone. `list_symbols` reports a
member's declaration start line, so a multi-line signature/attribute list starting at that
line is exactly the "cut off mid-signature" case the decided boundary-snapping rule (Scenario
11) must handle: because the raw window here overlaps the member's header region (attributes,
modifiers, return type, identifier, type parameters, parameter list, constraint clauses), the
served window clamps to that member's full header span — from the enclosing
`MemberDeclarationSyntax`'s own start line to the line of the token immediately preceding
`Body`/`ExpressionBody`/`SemicolonToken` — rather than either "member" granularity
(over-expanding to pull in the entire method body, breaking "bounded") or "statement"
granularity (under-expanding, since a multi-line method signature is not itself a statement
and would still get cut).)

## Success criteria

- All 16 scenarios above pass as automated tests against
  `Graphwright.McpServer.Tools.GetFileTool` (or its successor after this story's schema/logic
  replace the current stub).
- `get_file` returns real file content read through the loaded workspace, never a best-effort
  or partial read on failure (CLAUDE.md "No invented data").
- `file` is relative to the project root, forward-slash (CLAUDE.md cross-cutting rule 2), and
  echoes the validated input path.
- Whole-file and explicit-range modes return content of any length, with no upper bound in
  Month 1 — decided, deferred to a dedicated future PERF spec if a real measured need arises
  (see "Out of scope"). Only the bounded-snippet (`around_line`) mode is subject to a small
  default window; CLAUDE.md cross-cutting rule 4's "1-5 lines maximum" governs only that
  default-context snippet window, not a global cap on `get_file` output.
- Snippet windows never truncate mid-expression/mid-statement (ticket acceptance criteria;
  CLAUDE.md cross-cutting rule 4), implemented via the `SyntaxTree` rather than a purely
  line-counting heuristic.
- `start_line`/`end_line` pair and `around_line` are mutually exclusive; violating that, or
  supplying a partial range, or non-positive/wrong-type line arguments, all raise
  `INVALID_ARGUMENT` before any Roslyn or file-read work is attempted (mirrors
  `ListSymbolsTool`'s Scenario 11 validate-before-work discipline).
- Rooted/traversal `path` raises `INVALID_ARGUMENT`; a well-formed but non-existent `path`
  raises `FILE_NOT_FOUND` via the existing `SourceFileNotFoundException` — no new exception
  type needed (FEAT-GW-5 00-spec.md Scenario 11a forward reference).
- Workspace-not-ready raises `WORKSPACE_NOT_LOADED` via the existing
  `WorkspaceNotLoadedException`, consistent with `RoslynLanguageProvider.ListSymbolsAsync`'s
  existing `_snapshot.IsLoaded == false` check
  (`src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs:42-45`).
- `WORKSPACE_NOT_LOADED`, `INVALID_ARGUMENT`, and `FILE_NOT_FOUND` are the only error codes
  this tool produces; the existing `ExceptionEnvelopeMapper`
  (`src/Graphwright.McpServer/Contracts/ExceptionEnvelopeMapper.cs`) needs no changes.
- `GetFileTool.InputSchema` is updated to the real `path` (required) /
  `start_line?` / `end_line?` / `around_line?` / `context?` shape, replacing the GW-4
  placeholder (`required: ["path"]` with no range/snippet arguments,
  `src/Graphwright.McpServer/Tools/GetFileTool.cs:19-30`).
- A new `ILanguageProvider` method (e.g. `GetFileAsync`) is added in
  `Graphwright.Application.LanguageProviders`, alongside the existing `ListSymbolsAsync`,
  returning a plain DTO (e.g. `FileContentResult`) — no Roslyn type crosses into `McpServer`
  or `Application` (same DIP boundary GW-5 established;
  `src/Graphwright.Application/LanguageProviders/ILanguageProvider.cs:13-22`).
- `#nullable enable` maintained; async methods suffixed `Async` with `CancellationToken ct` as
  the last parameter; `== false` / `== true` negation style followed throughout new code
  (CLAUDE.md "C# coding conventions"; same bar GW-5 held itself to).
- Test coverage on changed lines meets the constitution §3 bar once that placeholder threshold
  is confirmed (see Constitution check) — same live gate as GW-5, not a scaffold exemption.

## Out of scope

- **Loading, opening, or warming the Roslyn workspace/compilation itself.** The
  `ILanguageProvider` / `RoslynWorkspaceSnapshot` seam now exists in code as of GW-5
  (`src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs:38-39` registers
  `RoslynWorkspaceSnapshot.NotLoaded()` and `RoslynLanguageProvider` unconditionally), but
  nothing yet populates it with a real `MSBuildWorkspace`/solution load. `get_file`, like
  `list_symbols` before it, consumes whatever workspace-readiness signal exists; it does not
  build it. **Decided**: GW-6 is scoped exactly like GW-5 — real logic implemented and tested
  against in-memory `Solution`/`SourceText` fixtures, reusing GW-5's existing
  `RoslynWorkspaceTestFixtures` pattern
  (`tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynWorkspaceTestFixtures.cs`).
  Scenario 14 (`WORKSPACE_NOT_LOADED`) remains the only end-to-end path provable against the
  real DI graph until a separate workspace-loading/indexer story lands — this is not a new
  blocker introduced by GW-6.
- **A whole-file/explicit-range size guard or streaming behavior.** Decided: no upper bound on
  `get_file`'s whole-file/explicit-range content size for Month 1 — CLAUDE.md cross-cutting
  rule 5 ("Result cap: 50 per call") governs multi-item `results[]` arrays for other tools, not
  a single file's `content` length. Deferred to a dedicated future PERF spec if a real
  SportsBook-sized file turns out to need one (constitution §3 "Performance prerequisite:
  optimization is blocked without a measured baseline").
- **`SQLite + sqlite-vec` graph store persistence** — `get_file` reads file text (and, for
  snippet boundary-snapping, the `SyntaxTree`) directly; `IGraphStore` is out of scope
  regardless, as it was for GW-5.
- **The other 3 remaining frozen tools** (`find_references`, `get_call_graph`, `search`) —
  separate GW-1 stories, each still a `ToolNotImplementedException` stub in
  `src/Graphwright.McpServer/Tools/`.
- **`FileWatcher` / incremental re-indexing on file change** — indexer concern, not this
  tool's, same as GW-5's framing.
- **The `sd-code-explorer`/`sd-debugger` "definition routine" orchestration itself** — this
  spec only guarantees `get_file` can be called with a `list_symbols`-sourced `file:line` and
  return a correct, non-truncated snippet (Scenario 15); it does not implement or change the
  calling agents' prompt logic.
- **GraphRAG / natural-language queries, multi-language / `TreeSitterProvider`** — Month 2+/3
  per CLAUDE.md "What Graphwright is NOT responsible for."
- **Authoring/updating `mcp-contract.md`** as a formal schema document. Decided (same posture
  as GW-5, which carried the same item unresolved as its own OQ-7): this remains a separate
  cross-cutting task, not something this spec needs a decision on to proceed; the file still
  does not exist on disk as of this spec.

## Open questions

- **OQ-8 (path-casing normalization on Windows — CLAUDE.md project-wide open decision, still
  live).** Carried forward unresolved from FEAT-GW-5's OQ-3. CLAUDE.md "Open decisions"
  explicitly reserves this for a project-wide ADR ("do not resolve without an ADR"), so this
  spec does not resolve the ADR itself. **Concrete, decided, non-blocking obligation scoped to
  this story**: `get_file`'s `file` output field must reuse `list_symbols`'s existing,
  already-implemented path-echoing convention verbatim — GW-6 introduces no new or divergent
  casing scheme of its own. GW-6 itself therefore has no ambiguity here; only the underlying
  project-wide path-casing ADR remains open.

## Constitution check

- **§1.1 Dependency direction** — Respected by design, extending GW-5's precedent exactly:
  file-read and syntax-boundary logic belongs in `Graphwright.Infrastructure`
  (`RoslynLanguageProvider` or a sibling class) behind the Application-defined
  `ILanguageProvider` seam. The `McpServer` tool handler (`GetFileTool`) depends only on
  `ILanguageProvider`, never on `Microsoft.CodeAnalysis.*` types directly — same boundary GW-5
  and GW-26 already established. The seam exists in code, but until a real workspace load
  lands (out of scope for this story — see "Out of scope"), this is enforced structurally and
  tested against in-memory fixtures, not yet exercised end-to-end against a real solution for
  anything but Scenario 14 — decided scoping, matching GW-5, not a gap introduced by this
  story.
- **§1.1 Inversion** — Respected. `GetFileTool` depends on `ILanguageProvider`'s new method,
  not on its Infrastructure implementation; DI registration continues through
  `InfrastructureModule.RegisterServices`
  (`src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs:34-40`).
- **§1.1 Cross-layer data (DTOs only)** — Respected. The new `ILanguageProvider` method must
  return a plain DTO (`file, start_line, end_line, total_lines, content`), not a Roslyn
  `Document`, `SourceText`, or `SyntaxTree` — mirrors `DeclaredSymbol`/`SymbolListResult`'s
  existing pattern (`src/Graphwright.Application/LanguageProviders/DeclaredSymbol.cs`,
  `SymbolListResult.cs`).
- **§6 Forbidden — catch-and-swallow** — Respected by reusing the existing
  exception-then-dispatch-boundary-mapping pattern: `WorkspaceNotLoadedException`,
  `InvalidToolArgumentException`, and `SourceFileNotFoundException` (all already defined in
  `Graphwright.Domain.Exceptions`) are raised, not caught-and-swallowed; the existing
  `ExceptionEnvelopeMapper` maps them unchanged.
- **§6 Forbidden — service locator / static singletons holding state** — Respected. Any new
  Infrastructure logic is exposed through the existing DI registration path
  (`InfrastructureModule.RegisterServices`), constructor-injected into `GetFileTool`, same as
  `ListSymbolsTool`'s existing constructor (`src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:67-72`).
- **§6 Forbidden — `// TODO` / `// HACK`** — To be honored: since only Scenario 14 is truly
  exercisable end-to-end at implementation time (decided scoping — see "Out of scope"), that
  must be expressed as an honest `WorkspaceNotLoadedException` path plus a spec-tracked
  follow-up (same discipline GW-5 held itself to), not a code comment.
- **§5.1 Spec-driven** — Satisfied; this spec precedes schema/logic changes spanning
  `Graphwright.Application`, `Graphwright.Infrastructure`, and `Graphwright.McpServer`.
- **§3 Quality bars (test coverage)** — Live for this story, same as GW-5: real business logic
  (line-range math, boundary clamping, syntax-boundary expansion), not a scaffold. The
  `>=80%-on-changed-lines` bar applies once §3's placeholder threshold is confirmed.
  **Constitution gap** — `.specs/constitution.md` §3 still has unfilled `<<80>>%` / `<<200>>ms`
  template placeholders; carried forward unresolved from FEAT-GW-4/GW-26/GW-5. Not blocking
  spec approval, but should be resolved before this story's plan sets a numeric coverage gate.
- **§2.x conventions (style / async / errors) & §4 tech stack** — Followed via CLAUDE.md
  (custom exceptions, `Async` suffix, `CancellationToken ct`, `== false`/`== true`,
  `#nullable enable`); same constitution-gap note as FEAT-GW-4/GW-26/GW-5 applies — §2.1-§2.3
  and §4 remain template placeholders in `.specs/constitution.md`, not yet populated from
  CLAUDE.md by the previously-recommended ADR.

## Linked specs

- **Parent epic**: GW-1 — "Month 1: Drop-in MVP (GitNexus Replacement)"
  (https://trminhtrong.atlassian.net/browse/GW-1).
- **Ticket**: GW-6 (https://trminhtrong.atlassian.net/browse/GW-6). Ticket content was
  supplied directly in full as part of this spec's invocation; no separate JIRA fetch or
  ticket snapshot was performed for this story (per task instruction), so there is no
  `.specs/FEAT-GW-6/04-artifacts/ticket/` snapshot directory.
- **Prerequisite**: FEAT-GW-26 (`.specs/FEAT-GW-26/00-spec.md`, status: done) — supplies the
  4-project layered solution this story builds inside.
- **Prerequisite**: FEAT-GW-4 (`.specs/FEAT-GW-4/00-spec.md`, status: done) — supplies the
  tool registry, dispatch boundary, and error envelope this story reuses unchanged
  (`GetFileTool`'s stub, `IGitnexusTool`, `ExceptionEnvelopeMapper`,
  `WorkspaceNotLoadedException`, `InvalidToolArgumentException` all already exist in `src/`).
- **Precedent**: FEAT-GW-5 (`.specs/FEAT-GW-5/00-spec.md`, status: done) — the first real tool
  built on the same `ILanguageProvider` seam this story extends
  (`ILanguageProvider.ListSymbolsAsync`, `RoslynWorkspaceSnapshot`, `RoslynLanguageProvider`),
  and the source of the `path`-validation, clamp-not-reject, and validate-before-Roslyn-work
  conventions this spec mirrors throughout. FEAT-GW-5's own Scenario 11a explicitly forward-
  references this story's `FILE_NOT_FOUND` behavior via `SourceFileNotFoundException`.
- **Sibling stories under GW-1** (not yet spec'd): `find_references`, `get_call_graph`,
  `search` — the remaining 3 frozen tools, each still a `ToolNotImplementedException` stub in
  `src/Graphwright.McpServer/Tools/`.
