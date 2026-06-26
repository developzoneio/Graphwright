---
id: FEAT-GW-4
type: feature
status: approved
created: 2026-06-26
ticket: GW-4
ticket_url: https://trminhtrong.atlassian.net/browse/GW-4
ticket_snapshot: .specs/FEAT-GW-4/04-artifacts/ticket/GW-4.md
parent_epic: GW-1
title: MCP server scaffold — transport, tool registry, error envelope
---

# FEAT-GW-4 — MCP server scaffold: transport, tool registry, error envelope

> First implementation story of Graphwright. Greenfield: no source code, no `.sln`,
> no `.csproj` exists on disk yet. This story scaffolds the solution skeleton and the
> MCP server foundation. The internal logic of the 5 tools is explicitly **out of scope**.

## Why

Graphwright is a drop-in replacement for GitNexus in the Specwright workflow. Every
downstream capability — Roslyn indexing, `SymbolFinder`-backed reference and call-graph
queries, the SQLite + sqlite-vec store — is consumed through exactly 5 MCP tools with the
frozen `mcp__gitnexus__*` prefix (CLAUDE.md "MCP tool surface" section). Nothing can ship
until there is a server that can:

1. Speak the MCP protocol over the transports Specwright agents connect with (stdio and SSE).
2. Guarantee, at startup, that the tool surface is exactly the 5 contracted names — so the
   "zero-change Specwright compatibility" promise cannot silently drift.
3. Return every result in one consistent shape, so the 3 consuming agents
   (`sd-code-explorer`, `sd-debugger`, `sd-reviewer`) can parse success and failure
   uniformly without per-tool special-casing.

This story delivers that foundation and the 4-project layered skeleton
(`Graphwright.Domain` / `Application` / `Infrastructure` / `McpServer`, per CLAUDE.md
"Layer map") that hosts it. It unblocks every subsequent tool-implementation story under
epic GW-1. Business value: it converts the frozen contract in CLAUDE.md from a document
into an executable, self-asserting server boundary.

## What

The 5 tools are **registered as named, schema-described stubs** in this story. A stub
accepts its arguments, validates them at the envelope level if cheaply possible, and
returns a structured "not implemented" failure (or an empty contract-shaped success where
that is the honest answer). Actual Roslyn / SymbolFinder / SQLite behavior arrives in later
GW-1 stories.

### Scenario 1 — Registry asserts exactly 5 tools on startup

```gherkin
Given a clean build of the Graphwright.McpServer project
When the server initializes its tool registry
Then the registry contains exactly 5 tools
And their names are exactly:
  | mcp__gitnexus__list_symbols   |
  | mcp__gitnexus__get_file       |
  | mcp__gitnexus__find_references|
  | mcp__gitnexus__get_call_graph |
  | mcp__gitnexus__search         |
And startup fails fast (non-zero exit, logged reason) if the set differs in count or name
And no tool outside the mcp__gitnexus__ prefix is advertised
```

(Tool names and count are frozen by CLAUDE.md "MCP tool surface (Month 1 contract — frozen)";
"Do not add, rename, or remove tools in Month 1.")

### Scenario 2 — Success responses carry `"ok": true`

```gherkin
Given the server is running with the tool registry initialized
When a registered tool returns a successful result
Then the response envelope has "ok": true
And the success payload is carried under the envelope (not mixed with error fields)
And no "error" object is present on a success response
```

### Scenario 3 — Failures return a structured error envelope (no throw across the boundary)

```gherkin
Given the server is running
When a tool invocation fails for any reason
Then the response has "ok": false
And it contains an "error" object with exactly the fields: code, message, retryable
And "code" is one of:
  WORKSPACE_NOT_LOADED | SYMBOL_NOT_FOUND | FILE_NOT_FOUND |
  AMBIGUOUS_SYMBOL | INVALID_ARGUMENT | INTERNAL
And "retryable" is a boolean
And no unhandled exception propagates out of the tool-dispatch boundary
And an unexpected internal failure is mapped to code = INTERNAL (never leaked as a raw stack trace)
```

(Error envelope shape and the closed set of `code` values are from CLAUDE.md "Error envelope
(all tools must use this shape)".)

### Scenario 4 — Transport: stdio and SSE both serve the same surface

```gherkin
Given the server is started in stdio mode
When a client completes the MCP handshake and lists tools
Then it sees the same 5 tools and the same envelope shapes
Given the server is started in SSE mode
When a client completes the MCP handshake and lists tools
Then it sees the same 5 tools and the same envelope shapes
And transport selection does not alter the tool surface or envelope contract
```

### Scenario 5 — Solution skeleton respects the layer dependency rule

```gherkin
Given the new solution with 4 projects
  (Graphwright.Domain, Graphwright.Application, Graphwright.Infrastructure, Graphwright.McpServer)
When project references are configured
Then Domain references nothing
And Application references Domain only
And Infrastructure references Application + Domain
And McpServer references Application (composition via DI), not Infrastructure types directly
And the envelope/error contract types live in a layer no inner layer would need to reach outward for
```

(Layer map and "Dependency rule is non-negotiable" from CLAUDE.md "Architecture" /
"Layer map"; mirrors constitution §1.1 dependency direction and §1.1 inversion.)

## Success criteria

- The solution builds: `dotnet build` succeeds for all 4 projects with the layered
  references wired per Scenario 5. (Exact command pending — see Open question OQ-1.)
- Tool registry self-assertion: server refuses to start (fail-fast, logged) unless the
  advertised set is exactly the 5 `mcp__gitnexus__*` names. Verified by an automated test.
- Every tool invocation returns the envelope: `ok: true` for success, or
  `ok: false` + `{ code, message, retryable }` for failure, with `code` constrained to the
  6-value closed set. Verified by automated tests covering at least one success path and
  each error `code` that the scaffold can produce (`INVALID_ARGUMENT`, `INTERNAL`, and a
  representative not-yet-implemented case).
- No exception escapes the tool-dispatch boundary; unexpected failures map to `INTERNAL`.
- Both stdio and SSE transports expose the identical tool surface and envelope (Scenario 4).
- The error-envelope and result contract are defined as domain exceptions + a mapping at the
  boundary — consistent with CLAUDE.md "Error envelope" and the custom-exception rule
  (CLAUDE.md "C# coding conventions": `SymbolNotFoundException`, `WorkspaceNotLoadedException`;
  no result pattern, no generic `Exception`).
- `#nullable enable` across all 4 projects (CLAUDE.md "C# coding conventions": nullability).

## Out of scope

- **Roslyn indexing, `SyntaxTree` / `SemanticModel` building, `SymbolFinder` usage** — the
  internal logic of all 5 tools. Stubs only here. (Later GW-1 stories.)
- **SQLite + sqlite-vec graph store** (`IGraphStore` implementation). Interface may be
  declared in Application as a seam, but no storage logic.
- **Indexer, FileWatcher, Git integration.**
- **The availability-probe performance target** for `list_symbols` (CLAUDE.md "Availability
  probe") — that requires a real warm index, which does not exist in the scaffold.
- **GraphRAG / natural-language queries, multi-language / TreeSitterProvider, Neo4j** —
  explicitly Month 2+/Month 3 per CLAUDE.md "What Graphwright is NOT responsible for".
- **The full cross-cutting output rules** (exact `file:line`, relative-path normalization,
  bin/obj/generated exclusion, 1–5 line snippets, 50-result cap, ordering) from CLAUDE.md
  "Cross-cutting rules" — these constrain real tool output and belong with the tool-logic
  stories. The scaffold only fixes the **envelope** that will carry that output.
- **Filling `project-config.json commands.*`** beyond what is needed to flag OQ-1.

## Open questions

- **OQ-1 (blocks Phase 4/5 testing).** `project-config.json` `commands.{build,test,lint,coverage,run}`
  are still template placeholders (`<<e.g. ...>>`, `.claude/project-config.json:40-44`). They
  MUST be filled with real `dotnet` invocations before any test/lint gate can run. Proposed
  defaults to confirm at planning: `build: dotnet build -c Release`,
  `test: dotnet test --no-build`, `lint: dotnet format --verify-no-changes`. Needs user confirmation.
- **OQ-2 (transport scope).** Does Month 1 require SSE to be fully wired, or is stdio the
  primary transport with SSE present-but-minimal? CLAUDE.md lists "stdio/SSE" but the
  availability-probe and all 3 agents connect via stdio in the Specwright workflow. Recommend:
  stdio first-class and tested; SSE registered and reachable but its deeper hardening deferred
  unless GW-1 requires it. Needs user confirmation.
- **OQ-3 (MCP SDK choice).** Which MCP server library backs the transport/handshake layer
  (e.g. the official C# MCP SDK vs. a hand-rolled JSON-RPC handler)? This determines how the
  registry and envelope hook into dispatch. To resolve at planning with current docs.
- **OQ-4 (envelope contract home — constitution gap).** The error envelope is a wire/DTO
  contract shared by McpServer (serialization) and the inner layers (which raise the domain
  exceptions mapped into it). CLAUDE.md says custom domain exceptions live in the inner
  layers, while serialization is an McpServer concern. Constitution §1.1 ("Cross-layer data:
  only DTOs") permits the envelope DTO to cross layers, but does not state where the
  canonical contract type lives. Recommend: domain exceptions + the closed `code` enum in
  Domain/Application; the wire-DTO + exception→envelope mapping in McpServer. Confirm at planning.
- **OQ-5 (`paths.src` / `paths.tests`).** `project-config.json paths.src`/`tests` are still
  placeholders (`.claude/project-config.json:48-49`). The scaffold fixes the physical layout
  (proposed `src/` and `tests/`); these keys should be set to match. Needs confirmation.

## Constitution check

- **§1.1 Dependency direction** — Respected. Scenario 5 enforces Domain ← Application ←
  Infrastructure, with McpServer composing over Application via DI and never referencing
  Infrastructure types directly. Mirrors CLAUDE.md "Layer map" / "Dependency rule is
  non-negotiable".
- **§1.1 Inversion** — Respected. `ILanguageProvider`, `IIndexer`, `IGraphStore` are defined
  in Application and implemented in Infrastructure (CLAUDE.md "Layer map"). The scaffold may
  declare these interfaces as seams; it implements none of them.
- **§1.1 Cross-layer data (DTOs only)** — Respected, with OQ-4 flagging where the envelope
  DTO contract type should canonically live. The envelope is a DTO; no framework objects
  cross inward.
- **§6 Forbidden — catch-and-swallow** — Respected. Scenario 3 requires that no unhandled
  exception escapes the dispatch boundary AND that unexpected failures map to `INTERNAL` with
  a logged reason — i.e. catch-transform-at-boundary, never catch-and-swallow.
- **§6 Forbidden — service locator / static singletons holding state** — Respected. Tool
  registry and dispatch are wired via constructor injection (CLAUDE.md conventions + §6). The
  registry holds the fixed tool set (configuration-like, immutable), not mutable runtime state.
- **§6 Forbidden — `// TODO` / `// HACK` in committed code** — Honored. Stub tools return a
  structured not-implemented envelope rather than carrying TODO markers; "implement later" is
  tracked by the GW-1 child stories, not by comments.
- **§5.1 Spec-driven** — Satisfied; this spec precedes the scaffold (a multi-file,
  cross-layer change).
- **§2.3 Error handling** — Partial / deferred-by-design. The constitution §2.3 body is still
  a placeholder template, but CLAUDE.md "C# coding conventions" gives concrete rules (custom
  domain exceptions, no result pattern, no generic `Exception`); the spec follows CLAUDE.md.
  **Constitution gap** — §2.3, §2.1, §2.2, §4 (tech stack), and §3 thresholds are unfilled
  template placeholders in `.specs/constitution.md`. The project's real conventions currently
  live only in CLAUDE.md. Consider a separate ADR to populate the constitution from CLAUDE.md
  so future specs are not forced to dual-source. Not blocking for FEAT-GW-4.
- **§3 Quality bars (coverage / warnings-as-errors)** — Partial. §3 thresholds are
  placeholders. Recommendation carried into Success criteria: enable `TreatWarningsAsErrors`
  and add automated tests for registry assertion and envelope shapes. Concrete coverage % is
  blocked on §3 being filled (see gap above) and on OQ-1 (test command).

## Linked specs

- **Parent epic**: GW-1 — "Month 1: Drop-in MVP (GitNexus Replacement)"
  (https://trminhtrong.atlassian.net/browse/GW-1).
- **Ticket**: GW-4 — snapshot at `.specs/FEAT-GW-4/04-artifacts/ticket/GW-4.md`.
- **Downstream (not yet created)**: the per-tool implementation stories under GW-1 that fill
  the 5 stubs scaffolded here (`list_symbols`, `get_file`, `find_references`,
  `get_call_graph`, `search`).
