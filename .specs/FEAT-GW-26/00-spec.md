---
id: FEAT-GW-26
type: feature
status: done
created: 2026-06-26
ticket: GW-26
ticket_url: https://trminhtrong.atlassian.net/browse/GW-26
ticket_snapshot: .specs/FEAT-GW-26/04-artifacts/ticket/GW-26.md
parent_epic: GW-1
title: Scaffold solution structure (Clean Architecture) — sln, 4 projects, inward-only references
---

# FEAT-GW-26 — Scaffold solution structure (Clean Architecture)

> Greenfield. No `.sln`, no `.csproj`, no source exists on disk yet. This story creates the
> physical solution skeleton — `Graphwright.sln` plus the four layered projects
> (`Domain` / `Application` / `Infrastructure` / `McpServer`), their inward-only references,
> shared MSBuild props, and an `.editorconfig`. It contains **no behavior**: no Roslyn code,
> no MCP transport, no tool logic. It is the home that every subsequent Month 1 story builds in.

## Why

The CLAUDE.md "Layer map" and constitution §1.1 declare a non-negotiable inside-out dependency
chain (`Domain` <- `Application` <- `Infrastructure` <- `McpServer`). Today that chain exists
only as prose. Every downstream Month 1 story — the MCP server scaffold (GW-4), the 5 tool
implementations, the Roslyn `DotNetProvider`, the SQLite + sqlite-vec store — assumes the four
projects already exist and that the dependency boundary is enforced by the project graph, not by
discipline alone.

GW-26 converts the declared architecture into an executable structure:

1. A buildable `Graphwright.sln` with the four projects, so `dotnet build` is a real gate from
   day one.
2. Project references wired strictly inward, so a layering violation becomes a **compile error**
   rather than a review comment.
3. A package-placement boundary, so Roslyn / SQLite never leak inward to Domain or Application,
   and the composition root (McpServer) is the only project that knows about Infrastructure.
4. Shared build settings (`Directory.Build.props`) and an `.editorconfig` so every later file is
   born under the same TFM, nullability, analyzer, and naming rules.

Business value: it makes the architecture self-enforcing and unblocks the entire GW-1 epic. The
ticket records GW-26 as a prerequisite for GW-4 through GW-12 and as a direct blocker of GW-4.

## What

This story produces files and project references only. There are no runtime scenarios because
there is no runtime behavior; the "When/Then" below assert **structure and build outcomes**,
which are the honest acceptance surface for a scaffold.

### Scenario 1 — Solution and four projects exist with the required TFM/settings

```gherkin
Given a clean checkout with no .sln or .csproj on disk
When the scaffold is created
Then a Graphwright.sln exists referencing exactly four projects:
  | Graphwright.Domain         |
  | Graphwright.Application    |
  | Graphwright.Infrastructure |
  | Graphwright.McpServer      |
And every project targets net8.0
And every project has <Nullable>enable</Nullable>
And every project has LangVersion = latest
And these shared settings are inherited from a root Directory.Build.props (not duplicated per project)
```

### Scenario 2 — Project references point strictly inward

```gherkin
Given the four projects exist
When project-to-project references are configured
Then Graphwright.Domain has no project references
And Graphwright.Application references Graphwright.Domain only
And Graphwright.Infrastructure references Graphwright.Application (and transitively Domain)
And Graphwright.McpServer references Graphwright.Application and Graphwright.Infrastructure
  (McpServer is the composition root / DI wiring layer)
And no project references a layer outer than itself
```

### Scenario 3 — Package placement honors the layer boundary

```gherkin
Given the four projects exist
When NuGet package references are placed
Then Graphwright.Domain has zero PackageReferences (dependency-free)
And no Microsoft.CodeAnalysis.* package is referenced outside Graphwright.Infrastructure
And Microsoft.Build.Locator and the sqlite-vec package are referenced only in Graphwright.Infrastructure
And MediatR is referenced only in Graphwright.Application
And the generic host + DI packages (Microsoft.Extensions.Hosting / DependencyInjection)
  live in Graphwright.McpServer
And the concrete MCP protocol SDK package is NOT pinned here (deferred — see OQ-3)
```

(Stated as verifiable boundary rules rather than a fixed dependency manifest, so the package set
can grow in later stories without re-litigating the boundary. The Roslyn / sqlite-vec / MediatR
placements come directly from the GW-26 acceptance criteria and CLAUDE.md "Layer map".)

### Scenario 4 — Build succeeds and layering is enforced by the compiler

```gherkin
Given the scaffold with references wired per Scenario 2
When dotnet build is run on Graphwright.sln
Then the build succeeds with zero errors and zero warnings
And TreatWarningsAsErrors is in effect (from Directory.Build.props)
And introducing a reference from an inner project to an outer project would fail the build
  (the boundary is enforced by the project reference graph, not by review alone)
```

### Scenario 5 — Shared conventions are encoded in build props and .editorconfig

```gherkin
Given the root Directory.Build.props and .editorconfig
When a developer adds a new file in any project
Then nullable reference types are enabled by default
And analyzers + warnings-as-errors apply uniformly
And .editorconfig encodes the tooling-enforceable conventions:
  4-space indent, 120-col guidance, I-prefixed interfaces, _camelCase private fields,
  ALL_UPPER_CASE constants, PascalCase public members
And the non-tooling-enforceable conventions (== false / == true negation, Async suffix)
  are recorded as written house rules, not silently assumed to be auto-enforced (see OQ-4)
```

## Success criteria

- `dotnet build` on `Graphwright.sln` succeeds for all four projects with zero warnings
  (warnings-as-errors on). Exact command pending — see OQ-2.
- The four projects exist with `net8.0`, `<Nullable>enable</Nullable>`, `LangVersion latest`,
  all inherited from a single root `Directory.Build.props` (no per-project duplication).
- Project reference graph matches Scenario 2 exactly: Domain (none) <- Application <-
  Infrastructure <- McpServer; no inner project references an outer one. Verified by inspecting
  the `.csproj` reference graph (by construction + review). Whether to add an automated
  architecture test (e.g. NetArchTest) is OQ-5.
- Package boundary holds per Scenario 3: Domain has zero packages; `Microsoft.CodeAnalysis.*`,
  `Microsoft.Build.Locator`, and sqlite-vec only in Infrastructure; MediatR only in Application;
  host/DI in McpServer; MCP protocol SDK not pinned here.
- A root `Directory.Build.props` carries shared TFM / nullable / analyzer / warnings-as-errors
  settings, and `.editorconfig` encodes the tooling-enforceable conventions from Scenario 5.
- The solution physically establishes the `src/` (and, if test projects are in scope, `tests/`)
  layout — see OQ-1 and OQ-6.

## Out of scope

- **All behavior.** No Roslyn `SyntaxTree` / `SemanticModel` / `SymbolFinder` code, no MCP
  transport (stdio/SSE), no tool registry, no error-envelope implementation, no SQLite /
  sqlite-vec store logic, no indexer / FileWatcher / Git integration. Projects are created
  empty (or with a minimal placeholder type only if required to compile).
- **The MCP server foundation itself** — transport, tool registry, error envelope. That is
  FEAT-GW-4. GW-26 only delivers the projects GW-4 builds in. (GW-26 blocks GW-4.)
- **Pinning the concrete MCP protocol SDK package** — that selection is GW-4's OQ-3.
- **Implementing `ILanguageProvider` / `IIndexer` / `IGraphStore`.** These interfaces may later
  be declared in Application as seams, but GW-26 adds no interface bodies or implementations
  unless a placeholder is strictly needed to make a project compile.
- **Test projects and a test framework**, unless OQ-6 decides to scaffold them now. The ticket
  lists only the four production projects.
- **A custom Roslyn analyzer to enforce `== false` / `== true` / `Async`-suffix** house rules.
  Authoring an analyzer is a behavior-bearing concern, not a scaffold concern (see OQ-4).
- **Filling `project-config.json` `commands.*` / `paths.*`** beyond what OQ-1/OQ-2 require to
  unblock the build gate.

## Open questions

- **OQ-1 (`paths.src` / `paths.tests` + physical layout — GW-26 owns this).** GW-26 creates the
  files, so it fixes the on-disk layout. Proposed: production projects under `src/`, the `.sln`
  at repo root. `project-config.json` `paths.src`/`tests` are still placeholders
  (`.claude/project-config.json:48-49`) and should be set to match. This resolves the
  open question carried as FEAT-GW-4 OQ-5. Needs user confirmation at planning.
- **OQ-2 (`commands.*` placeholders).** `project-config.json` `commands.{build,test,lint,...}`
  are still template placeholders (`.claude/project-config.json:40-44`). At minimum `build`
  must be a real `dotnet` invocation before the build gate can run. Proposed:
  `build: dotnet build Graphwright.sln -c Release`. Shared with FEAT-GW-4 OQ-1.
- **OQ-3 (eager package add vs. on-first-use, and version pinning).** Should the scaffold add
  the boundary packages (Roslyn, sqlite-vec, MediatR, host/DI) now even though nothing consumes
  them yet, or only declare the boundary and let each consuming story add its package? And which
  versions get pinned (central package management via `Directory.Packages.props`?). Recommend:
  add the boundary-defining packages now to make the boundary real and tested, prefer central
  package management, and exclude the MCP SDK (undecided). Confirm at planning.
- **OQ-4 (how are `== false` / `== true` / `Async`-suffix enforced? — constitution gap).** These
  headline conventions (CLAUDE.md "C# coding conventions"; global coding standards) are **not
  expressible** in stock `.editorconfig` / built-in Roslyn analyzers. Options: (a) document them
  as written house rules for Month 1 and rely on review; (b) author a custom analyzer (separate,
  behavior-bearing story — out of scope here). Recommend (a) for the scaffold. This is a genuine
  gap: the ticket's wording "`.editorconfig` encoding the `== false` style" overpromises what
  `.editorconfig` can do.
- **OQ-5 (automated architecture test?).** The inward-only rule is enforced by the project
  reference graph (a violation is a compile error). Do we additionally want an architecture
  test (e.g. NetArchTest) to assert the boundary, or is by-construction + review sufficient for
  Month 1? An arch test would imply a test project (ties to OQ-6).
- **OQ-6 (scaffold test projects now?).** The ticket lists only the four production projects.
  Do we create `tests/` project(s) in GW-26 so later stories have a test home, or defer to the
  first story that needs tests? Recommend a minimal test project per the testability needs of
  GW-4; confirm at planning.

## Constitution check

- **§1.1 Dependency direction** — Respected, and this story is what *makes it enforceable*.
  Scenario 2 wires Domain <- Application <- Infrastructure <- McpServer; Scenario 4 makes an
  inner-to-outer reference a compile error. Mirrors CLAUDE.md "Layer map" / "Dependency rule is
  non-negotiable".
- **§1.1 Inversion** — Respected by deferral. `ILanguageProvider` / `IIndexer` / `IGraphStore`
  are defined in Application and implemented in Infrastructure (CLAUDE.md "Layer map"); GW-26
  creates the projects that host that inversion but implements none of it.
- **§1.1 Cross-layer data (DTOs only)** — Respected. No framework objects cross inward; Domain
  is dependency-free (Scenario 3). DTO/contract types live in Application per the ticket.
- **§6 Forbidden — service locator / static singletons with state** — Respected by construction.
  McpServer is the composition root and will use constructor injection (host/DI packages placed
  there, Scenario 3); no service locator or mutable static state is introduced by a scaffold.
- **§6 Forbidden — `// TODO` / `// HACK` in committed code** — Honored. Empty/placeholder
  projects carry no TODO markers; remaining work is tracked by the GW-1 child stories.
- **§5.1 Spec-driven** — Satisfied; this spec precedes a multi-file, cross-layer structural
  change.
- **§3 Quality bars (coverage)** — **N/A by nature.** A scaffold introduces no behavior lines,
  so the >=80%-on-changed-lines coverage gate has nothing to measure. The build-based criteria
  (zero-warning `dotnet build`, reference-graph assertions) replace it for this story. The
  warnings-as-errors bar from §3 *is* enforced (Scenario 4 / Directory.Build.props).
- **§2.x conventions (style / async / errors) & §4 tech stack** — Followed via CLAUDE.md, which
  carries the concrete rules. **Constitution gap (carried from FEAT-GW-4):** §2.1, §2.2, §2.3,
  §3 thresholds, and §4 in `.specs/constitution.md` are still unfilled template placeholders;
  the real conventions live only in CLAUDE.md, and MediatR (required by this ticket) appears in
  neither CLAUDE.md nor the constitution stack (§4) — only as a placeholder example in §1.2/§7.
  Consider a separate ADR to populate the constitution (stack incl. MediatR, style, async,
  thresholds) from CLAUDE.md. Not blocking for GW-26.
- **§2.x enforceability gap** — see OQ-4: `== false` / `== true` / `Async`-suffix are not
  enforceable via `.editorconfig`. The scaffold encodes what tooling can enforce and records the
  rest as house rules; closing this gap (custom analyzer) is a separate concern.

## Linked specs

- **Parent epic**: GW-1 — "Month 1: Drop-in MVP (GitNexus Replacement)"
  (https://trminhtrong.atlassian.net/browse/GW-1).
- **Ticket**: GW-26 — snapshot at `.specs/FEAT-GW-26/04-artifacts/ticket/GW-26.md`.
- **Blocks**: FEAT-GW-4 (`.specs/FEAT-GW-4/00-spec.md`, status: approved) — GW-26 blocks GW-4.
  **Scope-overlap flag:** FEAT-GW-4 was authored before GW-26 existed and currently claims the
  solution-skeleton scaffolding (its Scenario 5 + "4-project layered skeleton" framing, and its
  OQ-5 on `paths.src`/`tests`). GW-26 now **owns** that structural scope. Recommendation: when
  GW-4 next enters planning/refinement, refine it to *consume* the GW-26 skeleton and drop its
  Scenario 5 + OQ-5 (resolved here as OQ-1). FEAT-GW-4 is approved and is **not edited by this
  task** — this is a flag for the main thread, not a silent reconciliation.
- **Prerequisite for**: GW-4 through GW-12 (per the ticket) — all downstream Month 1 stories
  build inside the skeleton created here.
