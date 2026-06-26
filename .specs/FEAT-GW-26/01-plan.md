# FEAT-GW-26 — Implementation plan

> Greenfield scaffold. Output is files and project references only; **no behavior**.
> This plan operationalizes the seven decisions taken at planning (resolving OQ-1..OQ-6 +
> the McpServer reference-graph conflict from `03-decisions.md`).

## Approach

Build the solution from the inside out, mirroring the dependency chain itself, so every project
exists before any project that references it:

```
T01 shared MSBuild foundation
  -> T02 Domain        (zero refs, zero packages)
  -> T03 Application   (-> Domain;            + MediatR)
  -> T04 Infrastructure(-> Application;       + Roslyn + Build.Locator)
  -> T05 McpServer     (-> Application + Infrastructure; + host/DI)  [class library in GW-26]
  -> T06 Tests         (-> all four;          + xUnit + Test SDK)
  -> T07 Graphwright.sln (assemble all 5, full Release build gate)
T08 .editorconfig         (independent root file)
T09 project-config.json   (independent config edit)
```

Central package management (`Directory.Packages.props`) is the single version-pinning point;
every project uses versionless `<PackageReference>`. The dependency boundary is enforced **by the
project reference graph** — an inner-to-outer reference is a compile error (Scenario 4), so no
architecture-test framework is added in Month 1 (OQ-5 deferred).

## Phased overview

| Phase | Purpose | Tasks |
|---|---|---|
| Foundation | Shared build props, central package versions, root config | T01, T08, T09 |
| Structure (inside-out) | The four production projects + their inward references | T02, T03, T04, T05 |
| Test home | One xUnit project referencing all four (future arch/unit tests) | T06 |
| Wiring + gate | Assemble the `.sln`, run the zero-warning Release build | T07 |

T08 and T09 carry no file dependency on the project tasks and may be done in parallel with the
Foundation phase; they are grouped under Foundation for review convenience.

## Sequencing rationale

- **T01 first, always.** `Directory.Build.props` + `Directory.Packages.props` sit at repo root and
  are auto-imported by every `.csproj` MSBuild evaluates. If a project is created before the props
  exist, it will be born without the shared TFM / nullability / analyzer / warnings-as-errors
  settings (Scenario 1, Scenario 5) and without a resolvable central package version.
- **Inside-out project order (T02 -> T05).** A `dotnet add reference` (or `<ProjectReference>`)
  requires the target project to exist. Application can only reference Domain after Domain exists,
  and so on up the chain. This order also makes each task's build self-checking: a project compiles
  against already-created inner projects.
- **T06 after all four production projects**, because the test project references all four
  (decision 6) and cannot restore until they exist.
- **T07 last** is the only writer of `Graphwright.sln`. Each project task creates its `.csproj`
  only; T07 adds all five to the solution and runs the full `dotnet build Graphwright.sln -c
  Release` gate. Concentrating solution-file mutation in one task keeps the project tasks
  conflict-free (no two tasks edit the `.sln`).
- **Critical path**: T01 -> T02 -> T03 -> T04 -> T05 -> T06 -> T07. T08 and T09 are off the
  critical path.

## Key decisions encoded (resolving the open questions)

1. **OQ-1 physical layout — `src/` + root `.sln`.** Production projects under `src/`,
   `Graphwright.sln` at repo root, the one test project under `tests/`. `project-config.json`
   `paths.src="src"`, `paths.tests="tests"` (T09). This also resolves FEAT-GW-4 OQ-5.
2. **OQ-2 build command.** `commands.build = "dotnet build Graphwright.sln -c Release"` (T09); this
   is also the gate command run in T07's acceptance. Shared with FEAT-GW-4 OQ-1.
3. **OQ-3 packages — add the boundary-defining set now, via central package management.**
   `Directory.Packages.props` pins exactly the four boundary packages (+ test SDK):
   - `Microsoft.CodeAnalysis.CSharp.Workspaces` 4.x (latest stable) — Infrastructure only.
   - `Microsoft.Build.Locator` 1.x (latest stable) — Infrastructure only.
   - `MediatR` 12.x (last MIT-licensed line; do **not** take 13.x) — Application only.
   - `Microsoft.Extensions.Hosting` 8.x — McpServer only.
   - Test SDK trio (`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`) — Tests only.
   **`sqlite-vec` is deferred** (see Risk R1), not pinned here. The MCP protocol SDK is deferred to
   GW-4 (out of scope per spec). The **boundary rule still stands**: when added later, `sqlite-vec`
   and any `Microsoft.Data.Sqlite` live only in Infrastructure. This mirrors the spec's own pattern
   of deferring undecided packages (MCP SDK) and the spec's note (lines 93-95) that the package set
   may grow in later stories without re-litigating the boundary.
4. **OQ-4 enforcement split — `.editorconfig` for tooling-enforceable rules only.** `.editorconfig`
   (T08) encodes: 4-space indent, 120-col guidance, `I`-prefixed interfaces, `_camelCase` private
   fields, `ALL_UPPER_CASE` constants, PascalCase public members. The non-tooling-enforceable house
   rules (`== false` / `== true` negation, `Async` suffix) are **recorded as written house rules**
   (CLAUDE.md is authoritative for Month 1), not silently assumed to be auto-enforced. A custom
   analyzer is out of scope.
5. **OQ-5 architecture test — deferred.** The reference graph enforces the boundary by construction
   (violation = compile error, Scenario 4). NetArchTest is a Month 2 concern. The one test project
   created here (T06) references all four projects precisely so a future arch test has a home.
6. **OQ-6 test project — scaffold exactly one now.** `tests/Graphwright.Tests` (xUnit), referencing
   all four production projects. Unblocks every later story that needs a test home (notably GW-4's
   testability needs) without scaffolding a per-project test matrix prematurely.
7. **McpServer reference graph — references BOTH Application AND Infrastructure.** This resolves the
   "HARD CONFLICT" flagged in `03-decisions.md` lines 67-72. Reconciliation:
   - The constitution §1.1 / CLAUDE.md rule forbids **inner -> outer** references. McpServer is the
     **outermost** layer, so McpServer -> Infrastructure is **outer -> inner = allowed**, not a
     violation.
   - CLAUDE.md's "Application (via DI)" describes **code-level usage** — application code must not
     import Infrastructure types — not the project-reference graph. The composition root must
     project-reference Infrastructure to **register** its implementations in the DI container.
   - Therefore: McpServer **project-references** Application + Infrastructure (T05); "no
     Infrastructure types leak into Application" remains a **code-discipline rule** for later
     stories, not a project-reference rule. This matches `00-spec.md:74`.

## Additional load-bearing decisions (made explicit so they don't read as oversights)

- **McpServer is a class library in GW-26, not an executable.** An SDK `Exe` with no entry point
  fails `CS5001` ("no entry point"). GW-26 is behavior-free, so McpServer ships as the default class
  library (no `OutputType`), holding the host/DI package reference but no `Program.cs`. **GW-4 owns
  the flip to `Exe` + the host bootstrap.** Keeping `Program.cs` out of GW-26 also avoids
  re-claiming the scope GW-26 explicitly hands to GW-4.
- **Analyzers are enabled via MSBuild properties, never a `PackageReference`.** `Directory.Build.props`
  sets `EnableNETAnalyzers=true`, `AnalysisMode=...`, and `EnforceCodeStyleInBuild=true` (so
  `.editorconfig` naming rules are checked at build, not only in-IDE). Using the SDK-implicit
  analyzers (properties, not packages) keeps **Domain dependency-free** (Scenario 3, line 83): an
  analyzer `PackageReference` in a root prop would be inherited by Domain and break "Domain has zero
  PackageReferences."
- **Empty class libraries build clean.** SDK class libraries with zero `.cs` files compile to an
  empty assembly with no warnings, so the zero-warning gate (Scenario 4) holds without placeholder
  types. No placeholder `.cs` is added to any project.

## Risks

| ID | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R1 | A `sqlite-vec` / native-extension package added under `TreatWarningsAsErrors` with no consuming code surfaces a restore/build warning (e.g. NU1701-class) and breaks the zero-warning gate. | Medium | High | **Defer it** (decision 3). Pin only the four boundary packages + test SDK now; add SQLite packages in the store-implementation story, Infrastructure-only. Boundary rule documented, not enforced by a phantom reference. |
| R2 | Pinned package versions drift from "latest stable in major" by the time of restore, or a transitive analyzer warning appears. | Medium | Medium | All versions live in one file (`Directory.Packages.props`, T01). T01 acceptance enumerates every version; implementer verifies latest stable within each declared major at restore. |
| R3 | McpServer reference-graph reading is baked into all downstream stories; choosing wrong forces rework across GW-4..GW-12. | Low (resolved) | High | Decision 7 + the explicit reconciliation above. McpServer -> {Application, Infrastructure} is outer->inner (allowed); "no Infrastructure types in Application code" stays a code-discipline rule. |
| R4 | `EnforceCodeStyleInBuild=true` + naming rules at `error` severity + `TreatWarningsAsErrors` could fail the build on the first real code. | Low (by design) | Low | Intended: that is the enforcement. For GW-26 there is no code, so no violations; the gate is green. |
| R5 | Scope bleed into FEAT-GW-4 (Program.cs, MCP SDK, transport). | Low | Medium | Hard scope line: McpServer stays a library, no `Program.cs`, MCP SDK not pinned. FEAT-GW-4/00-spec.md is **not edited** by this task (a separate refinement, flagged to the main thread, will later have GW-4 consume this skeleton). |

## Out of scope (restated)

No Roslyn / MCP / SQLite behavior; no `Program.cs`; no MCP protocol SDK pin; no `ILanguageProvider`
/ `IIndexer` / `IGraphStore` bodies; no architecture-test framework; no `commands.{test,lint,
coverage,run}` beyond `build`. FEAT-GW-4/00-spec.md is not edited.
