---
id: FEAT-GW-5
type: feature
status: in-progress
created: 2026-07-02
ticket: GW-5
ticket_url: https://trminhtrong.atlassian.net/browse/GW-5
ticket_snapshot: .specs/FEAT-GW-5/04-artifacts/ticket/GW-5.md
parent_epic: GW-1
title: "Tool: list_symbols (SyntaxTree + SemanticModel)"
---

# FEAT-GW-5 — Tool: list_symbols (SyntaxTree + SemanticModel)

> First real (non-stub) tool logic on top of the GW-26 solution skeleton and the GW-4 MCP
> server scaffold. `list_symbols` enumerates declared symbols — scoped to a file, a directory,
> or the whole workspace — backed by Roslyn `SyntaxTree` declarations plus `SemanticModel`
> resolution. It also doubles as the CLAUDE.md "availability probe" that `sd-code-explorer`
> uses to decide whether to trust the Graphwright MCP server before falling back.

## Why

Graphwright exists to be a zero-change, drop-in replacement for GitNexus behind exactly 5
frozen `mcp__gitnexus__*` tools (CLAUDE.md "MCP tool surface"). GW-4 and GW-26 built the
server that can register and dispatch those 5 tools, but every one of them is currently a stub
that throws `ToolNotImplementedException` (`src/Graphwright.McpServer/Tools/ListSymbolsTool.cs`).
No tool has ever returned a real symbol yet, which means:

1. **`sd-code-explorer` cannot trust the server.** CLAUDE.md names `list_symbols` on a single
   small file as the literal availability probe the agent runs before deciding whether to use
   Graphwright at all. Until this story ships, every Specwright session on the test project
   (SportsBook / `smp-jt-services`) falls back to slower, non-Roslyn tooling.
2. **No downstream tool can be validated end-to-end.** `find_references`, `get_call_graph`, and
   `search` all resolve to Roslyn symbols; `list_symbols` is the first vertical slice that
   proves the Roslyn-to-MCP-envelope path works for real source, not just for the envelope
   shape GW-4 already tested.
3. **Business value**: this is the smallest possible increment that converts Graphwright from
   "a server that speaks the protocol" into "a server that answers a real code-graph question,"
   which is the entire premise of the Month 1 Definition of Done (CLAUDE.md, GW-1 epic).

## What

`list_symbols` enumerates symbols declared in source, scoped by an optional `path` (file,
directory, or omitted for the whole workspace), filtered by an optional case-insensitive
`name_filter` and/or `kinds`, with generated code excluded by default and results capped and
ordered per the CLAUDE.md cross-cutting rules.

The existing stub's `InputSchema` (`ListSymbolsTool.cs`) currently declares a single required
`file` argument — that was GW-4's placeholder, not the GW-5 contract. This story replaces it
with the schema below and implements the real symbol-enumeration logic in place of the
`ToolNotImplementedException` throw.

**Input** (all optional except none — everything is optional per the ticket):
- `path?` — relative, forward-slash path to a file or directory; omitted means whole workspace.
- `name_filter?` — case-insensitive; matches by substring OR exact match (see Scenario 4).
- `kinds?` — array of symbol-kind literals to include (vocabulary: see Open question OQ-2).
- `include_generated?` — boolean, default `false`.
- `max_results?` — integer, default `50`, hard cap `50` (a caller-supplied value above 50 is
  clamped to 50, not rejected — mirrors CLAUDE.md "Result cap: 50 per call").

**Output** — `results: [{ name, kind, file, line, signature, container, accessibility }]`,
`truncated: bool`, `total_found: int`. `container` and `accessibility` are additive per the
ticket (present on every result; "additive" describes their arrival relative to the GitNexus
baseline contract, not conditional presence — confirmed as an assumption, see OQ-6).

### Scenario 1 — Enumerate symbols in a single file

```gherkin
Given a workspace whose index is loaded
And a source file with at least one namespace, one type, and one member declaration
When list_symbols is called with path set to that file's relative path
Then results contains one entry per declared symbol in that file
And each entry has { name, kind, file, line, signature, container, accessibility }
And file matches the input path exactly, using forward slashes, relative to the project root
And line is the 1-based declaration line from Location.GetLineSpan()
```

### Scenario 2 — Scope to a directory

```gherkin
Given a workspace whose index is loaded
And a directory containing multiple source files, each with declared symbols
When list_symbols is called with path set to that directory's relative path
Then results contains declared symbols from every source file under that directory
And no symbol from a file outside that directory subtree is included
```

### Scenario 3 — Scope to the whole workspace (path omitted)

```gherkin
Given a workspace whose index is loaded
When list_symbols is called with no path argument
Then results is drawn from every source file in the loaded workspace
And the same include/exclude and cap rules apply as the scoped cases
```

### Scenario 4 — name_filter is case-insensitive substring or exact match

```gherkin
Given a workspace whose index is loaded
And symbols named "OrderService", "OrderRepository", and "PaymentGateway" exist in scope
When list_symbols is called with name_filter "order" (any case)
Then results contains "OrderService" and "OrderRepository"
And results does not contain "PaymentGateway"
When list_symbols is called with name_filter "OrderService" (exact, any case)
Then results contains exactly the symbols named "OrderService" (case-insensitive exact match)
```

### Scenario 5 — kinds filters the declaration kind

```gherkin
Given a workspace whose index is loaded
And the scope contains classes, interfaces, methods, and fields
When list_symbols is called with kinds set to ["method"]
Then results contains only method declarations
And no class, interface, or field entry is present
```

### Scenario 6 — Generated and excluded paths are omitted by default

```gherkin
Given a workspace whose index is loaded
And the scope includes files under bin/, obj/, node_modules/, and a *.g.cs file
When list_symbols is called with include_generated omitted (defaults to false)
Then no symbol from bin/, obj/, node_modules/, or any *.g.cs file appears in results
When list_symbols is called with include_generated set to true
Then *.g.cs symbols may appear in results
And bin/, obj/, and node_modules/ are still excluded
  (CLAUDE.md "Exclude bin/, obj/, *.g.cs, node_modules/" — these are a server-side filter,
  not something include_generated is defined to override for build-output directories)
```

### Scenario 7 — Result cap and truncation signal

```gherkin
Given a workspace whose index is loaded
And a scope containing more than 50 matching declared symbols
When list_symbols is called (default or explicit max_results, both capped at 50)
Then results contains at most 50 entries
And truncated is true
And total_found reflects the full count of matching symbols before the cap was applied
```

### Scenario 8 — Zero matches

```gherkin
Given a workspace whose index is loaded
When list_symbols is called with a name_filter or path that matches no declared symbol
Then results is an empty array
And total_found is 0
And truncated is false
And no error is raised — zero matches is a valid, honest result (CLAUDE.md "No invented data")
```

### Scenario 9 — Deterministic ordering

```gherkin
Given a workspace whose index is loaded
And matching symbols span multiple files and multiple lines within files
When list_symbols is called
Then results is ordered by file ascending, then by line ascending within each file
And repeated calls with the same inputs and the same workspace state return the same order
```

### Scenario 10 — Workspace not ready yet

```gherkin
Given the server has started but the workspace index has not finished loading
When list_symbols is called with any arguments
Then the response is "ok": false
And error.code is "WORKSPACE_NOT_LOADED"
And error.retryable is true
And no partial or best-effort results are returned
```

### Scenario 11 — Invalid arguments are rejected before any Roslyn work

```gherkin
Given a workspace whose index is loaded
When list_symbols is called with a path outside the workspace root, or an unrecognized kinds
  literal, or a non-string/non-integer type for a typed argument
Then the response is "ok": false
And error.code is "INVALID_ARGUMENT"
And error.retryable is false
And no Roslyn query is attempted for the invalid request
```

### Scenario 11a — Well-formed path that does not exist in the workspace

```gherkin
Given a workspace whose index is loaded
When list_symbols is called with a path that is under the workspace root and well-formed,
  but names a file or directory that does not exist in the workspace
Then the response is "ok": false
And error.code is "FILE_NOT_FOUND"
And error.retryable is false
```

(Distinguished from Scenario 11: a malformed or out-of-root `path` is a client input error
(`INVALID_ARGUMENT`); a well-formed, in-root `path` that simply does not resolve to anything in
the workspace is `FILE_NOT_FOUND` — the same code `get_file` is expected to use for its
analogous case, via the existing `SourceFileNotFoundException`
(`src/Graphwright.Domain/Exceptions/SourceFileNotFoundException.cs`). This keeps the two tools'
handling of a missing path consistent rather than leaving it to diverge by accident.)

### Scenario 12 — Availability probe on a single small file returns fast

```gherkin
Given the SportsBook-sized test project with a warm, already-loaded index
When sd-code-explorer calls list_symbols on a single small file as its availability probe
Then the response is returned before the agent's fallback timeout
And the server is not observed to build or re-load any part of the workspace during the call
  (CLAUDE.md "Availability probe": "must be served from a warm index")
```

(This scenario asserts the *tool's* behavior given a warm index; it does not itself deliver the
warm index — see Out of scope and Open question OQ-1.)

## Success criteria

- All 12 scenarios above pass as automated tests against `Graphwright.McpServer.Tools.ListSymbolsTool`
  (or its successor after this story's schema/logic replace the current stub).
- `list_symbols` returns real Roslyn-derived symbols: `name`, `kind`, `file`, `line`,
  `signature`, `container`, `accessibility`, with `line` sourced from
  `Location.GetLineSpan()` and 1-based (CLAUDE.md cross-cutting rule 1).
- `file` values are relative to the project root, forward-slash, and exclude `bin/`, `obj/`,
  `*.g.cs`, `node_modules/` by default (CLAUDE.md cross-cutting rules 2–3).
- Result cap is enforced at 50 with `truncated` + `total_found` signaling overflow
  (CLAUDE.md cross-cutting rule 5; ticket acceptance criteria).
- Ordering is `file` ascending then `line` ascending, deterministic across repeated calls
  (CLAUDE.md cross-cutting rule 6).
- Zero matches returns an empty `results` array and `total_found: 0`, never an error
  (CLAUDE.md cross-cutting rule 7; ticket acceptance criteria).
- `name_filter` supports case-insensitive substring and exact matching (ticket acceptance
  criteria, Scenario 4).
- `WORKSPACE_NOT_LOADED`, `INVALID_ARGUMENT`, and `FILE_NOT_FOUND` are the only error codes
  this tool produces, using the existing `WorkspaceNotLoadedException` /
  `InvalidToolArgumentException` / `SourceFileNotFoundException` domain types and the existing
  `ExceptionEnvelopeMapper` (no new mapping logic needed —
  `src/Graphwright.McpServer/Contracts/ExceptionEnvelopeMapper.cs` already covers all three
  codes).
- The `ListSymbolsTool.InputSchema` is updated to the real `path? / name_filter? / kinds? /
  include_generated? / max_results?` shape, replacing the GW-4 placeholder
  (`required: ["file"]`).
- `#nullable enable` maintained; async methods suffixed `Async` with `CancellationToken ct` as
  the last parameter; `== false` / `== true` negation style followed throughout new code
  (CLAUDE.md "C# coding conventions").
- Test coverage on changed lines meets the constitution §3 bar once that placeholder threshold
  is confirmed (see Constitution check) — unlike GW-26 (a behavior-free scaffold), this story
  introduces real logic, so §3 coverage is a live gate here, not N/A.
- Availability-probe scenario (Scenario 12) is verified against a warm index, with the
  warm-index guarantee itself supplied by whatever story resolves OQ-1.

## Out of scope

- **Loading, opening, or warming the Roslyn workspace/compilation itself** — `MSBuildWorkspace`
  bootstrap, project/solution discovery, incremental re-index on file change, and the
  performance engineering needed to guarantee a warm index on a SportsBook-sized repo. GW-4
  explicitly deferred "the availability-probe performance target" and "all Roslyn indexing."
  This story consumes whatever workspace-readiness signal exists; it does not build it.
  **This is a real cross-story dependency — see Open question OQ-1; it may block
  implementation if no such seam exists by the time this story is planned.**
- **`SQLite + sqlite-vec` graph store persistence.** `list_symbols` is specified as backed
  directly by `SyntaxTree` + `SemanticModel`, not by a cached/persisted graph. `IGraphStore`
  implementation is out of scope here regardless.
- **The other 4 frozen tools** (`get_file`, `find_references`, `get_call_graph`, `search`) —
  separate GW-1 stories, each still a stub today.
- **`FileWatcher` / incremental re-indexing on file change** — indexer concern, not this tool's.
- **Defining or hardening the `sd-code-explorer` fallback-timeout threshold** — that threshold
  lives in the Specwright agent, not in Graphwright; this story only makes the probe honest and
  fast against a warm index.
- **GraphRAG / natural-language queries, multi-language / `TreeSitterProvider`** — Month 2+/3
  per CLAUDE.md "What Graphwright is NOT responsible for."
- **Authoring/updating `mcp-contract.md`** as a full formal schema document — it does not yet
  exist on disk (see Open question OQ-7); this spec's Input/Output section is the interim
  source of truth for planning.

## Open questions

- **OQ-1 (blocks implementation — workspace-loading seam does not exist yet).** Confirmed by
  inspection: `src/Graphwright.Application` contains no source beyond generated
  `AssemblyInfo.cs`, and `src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs`
  registers nothing yet (`"Implementations arrive with the later GW-1 stories"`). There is no
  `ILanguageProvider`, `IWorkspaceLoader`, or `IIndexer` seam for this tool to query against.
  `list_symbols` cannot honestly return real symbols — only `WORKSPACE_NOT_LOADED` — until
  something loads a Roslyn workspace. Two options: (a) GW-5 itself defines the minimal
  Application-layer seam (e.g. `ILanguageProvider`) and a thin Infrastructure implementation
  sufficient to load the test project and back this one tool, or (b) a dedicated indexer story
  is a hard prerequisite and GW-5 is scoped to "given a loaded workspace, enumerate correctly,"
  with Scenario 10 as its only behavior until the prerequisite lands. Recommend (b) with the
  seam interface declared here as a contract-only addition (Application) so downstream stories
  have a stable target — but this needs explicit confirmation at planning, since it changes
  whether this story can close the Month 1 "availability probe" DoD item on its own.
- **OQ-2 (`kinds` vocabulary is undefined).** The ticket says `kinds?` without enumerating
  accepted values. Recommend a closed string-literal set mapped onto Roslyn `SymbolKind` /
  `TypeKind` (e.g. `namespace`, `class`, `interface`, `struct`, `enum`, `method`, `property`,
  `field`, `event`, `constructor`), with an unrecognized literal raising `INVALID_ARGUMENT`
  (Scenario 11). Confirm the exact list and naming (snake_case vs PascalCase) at planning.
- **OQ-3 (path-casing normalization on Windows — CLAUDE.md open decision, now live).**
  CLAUDE.md lists this as an unresolved open decision project-wide. This is the first tool
  whose `file` output makes it concrete: does `file` preserve on-disk casing, or normalize to a
  canonical case for stable golden-file assertions across OSes? Needs resolution (ADR or
  decision at planning) before golden tests for this tool are written, since test fixtures will
  otherwise be OS-dependent.
- **OQ-4 (input schema replaces the GW-4 placeholder — confirm no other consumer depends on the
  old shape).** `ListSymbolsTool.InputSchema` currently requires `file` (singular). No other
  code references that schema today (grep found only the tool's own file), so replacing it
  should be safe, but flagging explicitly since InputSchema is part of the advertised MCP
  contract external clients could already be probing.
- **OQ-5 (concrete latency budget for the availability probe).** CLAUDE.md says the probe
  "must return before agent fallback timeout" but states no number, and constitution §3's
  `API response time p95 < <<200>>ms` is still a template placeholder. Should this story define
  and test against a concrete latency budget, or is "no observed re-index during the call"
  (Scenario 12) a sufficient proxy for Month 1? Recommend deferring a hard number to a
  dedicated perf spec (constitution §3 "Performance prerequisite: optimization is blocked
  without a measured baseline") once a warm index exists to measure against.
- **OQ-6 (are `container` / `accessibility` always present, or genuinely optional?).** The
  ticket phrase "(+ additive `container`, `accessibility`)" is read here as "always present,
  added on top of the GitNexus baseline shape" rather than "sometimes omitted." Confirm this
  reading at planning; if wrong, Scenario 1's assertion needs to become conditional.
- **OQ-7 (`mcp-contract.md` does not exist on disk).** CLAUDE.md "Key files" names
  `mcp-contract.md` as the frozen schema source of truth and says "Schema changes require
  updating `mcp-contract.md` first," but no such file exists in the repo yet. Recommend this
  story (or a small preceding step) creates `mcp-contract.md` with the `list_symbols` schema as
  its first entry, establishing the pattern the remaining 4 tools will follow. Confirm at
  planning whether that file creation belongs to GW-5 or a separate cross-cutting task.

## Constitution check

- **§1.1 Dependency direction** — Respected by design: symbol-enumeration logic (Roslyn
  `SyntaxTree`/`SemanticModel`) belongs in `Graphwright.Infrastructure` behind an
  Application-defined seam (CLAUDE.md "Layer map": `ILanguageProvider` defined in Application,
  implemented in Infrastructure). The `McpServer` tool handler (`ListSymbolsTool`) depends only
  on the Application-layer abstraction, never on `Microsoft.CodeAnalysis.*` types directly —
  mirrors the package-placement boundary GW-26 already established (`Microsoft.CodeAnalysis.*`
  only in Infrastructure). **Depends on OQ-1 being resolved with a real seam** — until then this
  is a design intent, not yet an enforced boundary for this tool.
- **§1.1 Inversion** — Respected. The tool depends on an interface (`ILanguageProvider` or
  equivalent) it does not implement; Infrastructure provides the implementation via DI
  (`InfrastructureModule.RegisterServices`).
- **§1.1 Cross-layer data (DTOs only)** — Respected. Roslyn `ISymbol` / `Location` /
  `SemanticModel` objects must not cross into McpServer; the seam returns plain DTOs
  (`name, kind, file, line, signature, container, accessibility`), consistent with GW-4's
  `ToolSuccessEnvelope<JsonElement>` pattern of tool-owned payload shapes.
- **§6 Forbidden — catch-and-swallow** — Respected by reusing the existing dispatch-boundary
  pattern: this tool raises `WorkspaceNotLoadedException` / `InvalidToolArgumentException` (both
  already defined in `Graphwright.Domain.Exceptions`) rather than catching and swallowing; the
  existing `ExceptionEnvelopeMapper` maps them, unchanged.
- **§6 Forbidden — service locator / static singletons holding state** — Respected. Any new
  Infrastructure service is registered via `InfrastructureModule.RegisterServices` and injected
  through the constructor, per the existing GW-26/GW-4 pattern; no service-locator lookup.
- **§6 Forbidden — `// TODO` / `// HACK`** — To be honored: if OQ-1 forces a partial
  implementation (only Scenario 10 real, others behind the unresolved seam), that must be
  expressed as an honest `WorkspaceNotLoadedException` path and a spec-tracked follow-up, not a
  code comment.
- **§5.1 Spec-driven** — Satisfied; this spec precedes real tool-logic and schema changes
  spanning at least Application, Infrastructure, and McpServer.
- **§3 Quality bars (test coverage)** — **Live for this story**, unlike GW-26 (scaffold, no
  behavior) and GW-4 (stubs only). This is the first story with real business logic; the
  `>=80%-on-changed-lines` bar applies once §3's placeholder threshold is confirmed. Flagging
  as unresolved rather than silently inheriting GW-26's "N/A by nature" reasoning, which does
  not apply here. **Constitution gap** — §3's `<<80>>%` and `<<200>>ms` are still unfilled
  template placeholders in `.specs/constitution.md`; carried forward from FEAT-GW-4/GW-26.
  Consider the previously-recommended ADR to populate the constitution from CLAUDE.md. Not
  blocking for spec approval, but should be resolved before this story's plan sets a numeric
  coverage gate.
- **§2.x conventions (style / async / errors) & §4 tech stack** — Followed via CLAUDE.md
  (custom exceptions, `Async` suffix, `CancellationToken ct`, `== false`/`== true`,
  `#nullable enable`); same constitution-gap note as FEAT-GW-4/GW-26 applies (§2.1–§2.3, §4 are
  still template placeholders in `.specs/constitution.md`).

## Linked specs

- **Parent epic**: GW-1 — "Month 1: Drop-in MVP (GitNexus Replacement)"
  (https://trminhtrong.atlassian.net/browse/GW-1). Snapshot at
  `.specs/FEAT-GW-5/04-artifacts/ticket/related/GW-1.md`.
- **Ticket**: GW-5 — snapshot at `.specs/FEAT-GW-5/04-artifacts/ticket/GW-5.md`.
- **Prerequisite**: FEAT-GW-26 (`.specs/FEAT-GW-26/00-spec.md`, status: done) — supplies the
  4-project layered solution (`Domain` / `Application` / `Infrastructure` / `McpServer`) this
  story builds inside, including the package-placement boundary
  (`Microsoft.CodeAnalysis.*` confined to Infrastructure) this spec's Constitution check relies
  on.
- **Prerequisite**: FEAT-GW-4 (`.specs/FEAT-GW-4/00-spec.md`, status: done) — supplies the tool
  registry, dispatch boundary, and error envelope this story reuses unchanged
  (`ListSymbolsTool`, `IGitnexusTool`, `ExceptionEnvelopeMapper`, `WorkspaceNotLoadedException`,
  `InvalidToolArgumentException` all already exist in `src/`).
- **Sibling stories under GW-1** (not yet spec'd): `get_file`, `find_references`,
  `get_call_graph`, `search` — the remaining 4 frozen tools, each still a
  `ToolNotImplementedException` stub in `src/Graphwright.McpServer/Tools/`.
