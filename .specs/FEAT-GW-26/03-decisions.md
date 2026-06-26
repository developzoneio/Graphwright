## Impact analysis (sd-code-explorer)

> Greenfield scaffold. No C# source, no solution, no build files exist on disk.
> Evidence: `Glob **/*.cs` -> "No files found"; `Glob` for `*.sln`/`*.csproj`/`Directory.Build.props`/`Directory.Packages.props`/`global.json`/`NuGet.config`/`.editorconfig` -> "No files found".

### Item-1 build-file scan (exists vs. will-be-created)

| File | Status | Spec-mandated? |
|---|---|---|
| `Graphwright.sln` | ABSENT -> CREATE | Yes (`00-spec.md:55-59`) |
| `src/Graphwright.Domain/Graphwright.Domain.csproj` | ABSENT -> CREATE | Yes (`00-spec.md:56`) |
| `src/Graphwright.Application/Graphwright.Application.csproj` | ABSENT -> CREATE | Yes (`00-spec.md:57`) |
| `src/Graphwright.Infrastructure/Graphwright.Infrastructure.csproj` | ABSENT -> CREATE | Yes (`00-spec.md:58`) |
| `src/Graphwright.McpServer/Graphwright.McpServer.csproj` | ABSENT -> CREATE | Yes (`00-spec.md:59`) |
| `Directory.Build.props` | ABSENT -> CREATE | Yes (`00-spec.md:63`, `00-spec.md:135`) |
| `.editorconfig` | ABSENT -> CREATE | Yes (`00-spec.md:108-120`, `00-spec.md:136`) |
| `Directory.Packages.props` | ABSENT -> CONDITIONAL | OQ-3 (`00-spec.md:170-175`) |
| `global.json` | ABSENT | Not spec-mandated |
| `NuGet.config` | ABSENT | Not spec-mandated |

`src/` directory: ABSENT. `tests/` directory: ABSENT.

### Direct callers (1-hop)

N/A — no source code exists (`Glob **/*.cs` -> zero files). Nothing references the to-be-created projects yet.

### Transitive callers (2-3 hop)

N/A — greenfield. GitNexus N/A (no symbols to index).

### Test coverage scan

N/A — no production code and no test projects exist. Whether to scaffold `tests/` now is OQ-6 (`00-spec.md:187-190`).

### DI / config grep — files to MODIFY

- `.claude/project-config.json:48` — `paths.src` placeholder `"<<e.g. src>>"`. GW-26 owns layout (OQ-1). Proposed: `"src"`.
- `.claude/project-config.json:49` — `paths.tests` placeholder `"<<e.g. tests>>"`. Proposed: `"tests"` (gated on OQ-6).
- `.claude/project-config.json:40` — `commands.build` placeholder. Must be real before build gate. Proposed: `"dotnet build Graphwright.sln -c Release"`.
- `.claude/project-config.json:41-44` — `commands.{test,lint,coverage,run}` remain placeholders; only `build` is in scope for this story.

### New files to CREATE

**Definite:**
- `Graphwright.sln` (repo root)
- `src/Graphwright.Domain/Graphwright.Domain.csproj` — zero refs, zero packages
- `src/Graphwright.Application/Graphwright.Application.csproj` — refs Domain only; MediatR here
- `src/Graphwright.Infrastructure/Graphwright.Infrastructure.csproj` — refs Application; Roslyn/Build.Locator/sqlite-vec here
- `src/Graphwright.McpServer/Graphwright.McpServer.csproj` — composition root; host/DI here
- `Directory.Build.props` — shared TFM/nullable/LangVersion/analyzers/TreatWarningsAsErrors
- `.editorconfig` — tooling-enforceable conventions

**Conditional (decision-gated):**
- `Directory.Packages.props` — central package management (OQ-3)
- `tests/` project(s) — (OQ-6; ties to arch-test OQ-5)
- Placeholder `.cs` type(s) — only if strictly needed to make a project compile

### Scope-overlap findings (FEAT-GW-4)

FEAT-GW-4 was authored before GW-26 and claims the solution-skeleton scope; GW-26 now owns it (`00-spec.md:230-236`). FEAT-GW-4 is approved and must NOT be edited by this task.

Line-level overlaps:
- Solution skeleton: `FEAT-GW-4:107-118` (Scenario 5) vs. `FEAT-GW-26:66-77` (Scenario 2). Owned by GW-26.
- `paths.src`/`tests`: `FEAT-GW-4:181-183` (OQ-5) vs. `FEAT-GW-26:161-165` (OQ-1). GW-26 resolves it.
- Build command: `FEAT-GW-4:161-165` (OQ-1) vs. `FEAT-GW-26:166-169` (OQ-2). Shared.

**HARD CONFLICT — McpServer reference graph (three sources disagree; not resolved here):**
- `FEAT-GW-26:74` — McpServer references Application **and** Infrastructure (composition root).
- `FEAT-GW-4:116` — McpServer references Application only, **not Infrastructure types directly**.
- `CLAUDE.md` Layer map — allowed deps = "Application (via DI)" only; Infrastructure not listed.

This may be a project-reference-vs-type-reference nuance (composition root project-references Infrastructure to register implementations but never imports Infrastructure types in code). The planner must decide which reading wins.

### Risk assessment

| Risk | Level | Notes |
|---|---|---|
| `project-config.json` `commands.build` still placeholder | MEDIUM-HIGH | Blocks Phase 4/5 build gate; must be updated in this story |
| Package version selection / central package management | MEDIUM | Wrong/unpinned versions surface only at restore/build (OQ-3) |
| McpServer reference-graph ambiguity | MEDIUM | Bakes layering decision into all downstream stories |
| `.editorconfig` over-promise on house rules | LOW | Spec already flags this; no hidden risk |

### Precedents & conventions

- No existing C# precedents — greenfield.
- Conventions authority: `CLAUDE.md` "C# coding conventions" + "Layer map". `constitution.md` §2.x/§4 are template placeholders; CLAUDE.md is authoritative for Month 1.
