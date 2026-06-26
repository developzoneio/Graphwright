# FEAT-GW-26 — Atomic tasks

> Greenfield scaffold. All paths relative to repo root
> `D:\trong-workspace\my-ai\Graphwright`. Forward slashes used in file lists.
> Coding standards (CLAUDE.md + global): `#nullable enable`, `== false` / `== true`, custom domain
> exceptions, `Async` suffix + trailing `CancellationToken ct`, 4-space indent, 120-col, PascalCase
> public / `_camelCase` private / `ALL_UPPER_CASE` constants / `I`-prefixed interfaces. (No `.cs`
> files are authored in this story, so these apply to downstream stories; `.editorconfig` encodes
> the tooling-enforceable subset.)

## Execution order

Critical path: T01 -> T02 -> T03 -> T04 -> T05 -> T06 -> T07. T08, T09 are independent (parallel).

---

### [x] T01 - Shared MSBuild foundation (Directory.Build.props + Directory.Packages.props)

- Files: `Directory.Build.props`, `Directory.Packages.props`
- Layer: Cross-cutting
- Step type: foundation
- Test: `N/A — scaffold only` (validated transitively by T02-T07 builds)
- Acceptance: `Directory.Build.props` sets `TargetFramework=net8.0`, `Nullable=enable`,
  `LangVersion=latest`, `TreatWarningsAsErrors=true`, `EnableNETAnalyzers=true`,
  `AnalysisMode=Recommended` (or stricter), `EnforceCodeStyleInBuild=true`, and
  `ManagePackageVersionsCentrally=true` — with **no `<PackageReference>`** (analyzers via SDK
  properties only, so Domain inherits zero packages). `Directory.Packages.props` declares
  `<PackageVersion>` for **exactly** these and nothing else — write these literal versions:
  `Microsoft.CodeAnalysis.CSharp.Workspaces` `4.11.0`,
  `Microsoft.Build.Locator` `1.7.8`, `MediatR` `12.4.1` (never 13.x — licensing),
  `Microsoft.Extensions.Hosting` `8.0.1`, `Microsoft.NET.Test.Sdk` `17.11.1`,
  `xunit` `2.9.2`, `xunit.runner.visualstudio` `2.8.2`.
  No `sqlite-vec`, no `Microsoft.Data.Sqlite`, no MCP SDK. Implementer note: confirm each is still
  the latest **stable** release within its declared major at restore; bump the patch if a newer
  stable exists, but stay within the same major (4.x / 1.x / 12.x / 8.x).
- Depends on: none
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: none — greenfield. Structure dictated by `00-spec.md:60-64` (TFM/nullable/
  LangVersion inherited, not per-project), `00-spec.md:103` (TreatWarningsAsErrors),
  `00-spec.md:170-175` (central package management, deferred MCP SDK), and `01-plan.md` decision 3.

---

### [x] T02 - Graphwright.Domain project (dependency-free core)

- Files: `src/Graphwright.Domain/Graphwright.Domain.csproj`
- Layer: Domain
- Step type: foundation
- Test: `dotnet build src/Graphwright.Domain/Graphwright.Domain.csproj -c Release`
- Acceptance: SDK class library (`Microsoft.NET.Sdk`, no `OutputType`), no `<ProjectReference>`,
  **zero `<PackageReference>`**, no `.cs` files; builds with zero warnings, inheriting all settings
  from `Directory.Build.props` (nothing redeclared in the csproj).
- Depends on: T01
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: none — greenfield. Boundary per `00-spec.md:71` (Domain has no project references)
  and `00-spec.md:84` (Domain has zero PackageReferences).

---

### [x] T03 - Graphwright.Application project (-> Domain; + MediatR)

- Files: `src/Graphwright.Application/Graphwright.Application.csproj`
- Layer: Application
- Step type: foundation
- Test: `dotnet build src/Graphwright.Application/Graphwright.Application.csproj -c Release`
- Acceptance: SDK class library; `<ProjectReference>` to `Graphwright.Domain` **only**; versionless
  `<PackageReference Include="MediatR" />` (version from `Directory.Packages.props`); no other
  packages; no `.cs` files; builds with zero warnings.
- Depends on: T02
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: none — greenfield. Boundary per `00-spec.md:72` (Application references Domain only)
  and `00-spec.md:87` (MediatR only in Application).

---

### [x] T04 - Graphwright.Infrastructure project (-> Application; + Roslyn + Build.Locator)

- Files: `src/Graphwright.Infrastructure/Graphwright.Infrastructure.csproj`
- Layer: Infrastructure
- Step type: foundation
- Test: `dotnet build src/Graphwright.Infrastructure/Graphwright.Infrastructure.csproj -c Release`
- Acceptance: SDK class library; `<ProjectReference>` to `Graphwright.Application` (Domain inherited
  transitively, not re-declared); versionless `<PackageReference>` to
  `Microsoft.CodeAnalysis.CSharp.Workspaces` and `Microsoft.Build.Locator`; **no `sqlite-vec` /
  SQLite package** (deferred — `01-plan.md` R1); no `.cs` files; builds with zero warnings. No
  `Microsoft.CodeAnalysis.*` reference exists in any other project.
- Depends on: T03
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: none — greenfield. Boundary per `00-spec.md:73` (Infrastructure -> Application),
  `00-spec.md:85-86` (Roslyn + Build.Locator + sqlite-vec only in Infrastructure — sqlite-vec
  deferred per plan).

---

### [x] T05 - Graphwright.McpServer project (-> Application + Infrastructure; + host/DI) [class library]

- Files: `src/Graphwright.McpServer/Graphwright.McpServer.csproj`
- Layer: McpServer (composition root)
- Step type: wiring
- Test: `dotnet build src/Graphwright.McpServer/Graphwright.McpServer.csproj -c Release`
- Acceptance: SDK **class library** (no `OutputType` — the `Exe` flip + `Program.cs` belong to
  GW-4); `<ProjectReference>` to **both** `Graphwright.Application` and `Graphwright.Infrastructure`
  (outer -> inner, allowed; the composition root must reference Infrastructure to register its
  implementations — `01-plan.md` decision 7); versionless `<PackageReference>` to
  `Microsoft.Extensions.Hosting`; no MCP SDK; no `.cs` files; builds with zero warnings.
- Depends on: T04
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: none — greenfield. Boundary per `00-spec.md:74-75` (McpServer -> Application +
  Infrastructure, composition root) and `00-spec.md:88-89` (host/DI in McpServer);
  `01-plan.md` decision 7 + "class library in GW-26" decision.

---

### [x] T06 - Graphwright.Tests project (xUnit; -> all four production projects)

- Files: `tests/Graphwright.Tests/Graphwright.Tests.csproj`
- Layer: Cross-cutting (test host)
- Step type: test
- Test: `dotnet build tests/Graphwright.Tests/Graphwright.Tests.csproj -c Release`
- Acceptance: SDK test project (`IsPackable=false`); versionless `<PackageReference>` to
  `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`; `<ProjectReference>` to all four
  production projects (Domain, Application, Infrastructure, McpServer) so a future architecture test
  can assert the whole graph; no test `.cs` files yet (empty test project builds clean); builds with
  zero warnings.
- Depends on: T02, T03, T04, T05
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: none — greenfield. Rationale per `01-plan.md` decisions 5 & 6 (OQ-5/OQ-6: one test
  home now; arch test deferred to Month 2).

---

### [x] T07 - Assemble Graphwright.sln and run the Release build gate

- Files: `Graphwright.sln`
- Layer: Cross-cutting
- Step type: wiring
- Test: `dotnet build Graphwright.sln -c Release`
- Acceptance: `Graphwright.sln` at repo root contains **exactly** the five projects (4 production +
  Tests); the command MUST be **actually run** and observed to succeed with **zero errors and zero
  warnings** (done = "built and green", not "should build" — watch for NuGet-audit / NU1701-class
  warnings promoted to errors by `TreatWarningsAsErrors`); the reference graph matches Scenario 2
  (Domain has none; Application -> Domain; Infrastructure -> Application; McpServer -> Application +
  Infrastructure); no inner project references an outer one. T07 is the sole writer of the `.sln`.
- Depends on: T02, T03, T04, T05, T06
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: none — greenfield. Solution membership per `00-spec.md:55-59`; build gate per
  `00-spec.md:97-106,124`.

---

### [x] T08 - .editorconfig (tooling-enforceable conventions)

- Files: `.editorconfig`
- Layer: Cross-cutting
- Step type: foundation
- Test: `N/A — scaffold only` (rules exercised at build via `EnforceCodeStyleInBuild` once code
  exists in later stories)
- Acceptance: `.editorconfig` at repo root with `root = true`; encodes 4-space indent
  (`indent_size = 4`, `indent_style = space`), 120-col guidance (`guidelines`/`max_line_length`),
  and naming rules — `I`-prefixed interfaces, `_camelCase` private/internal fields,
  `ALL_UPPER_CASE` constants, PascalCase public members — at `error` severity. Does **not** attempt
  to encode `== false` / `== true` / `Async`-suffix (not expressible; recorded as house rules per
  OQ-4).
- Depends on: none
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: none — greenfield. Content dictated by `00-spec.md:108-120,136` (tooling-enforceable
  subset) and `01-plan.md` decision 4 (OQ-4 split).

---

### [x] T09 - Update project-config.json (paths + build command)

- Files: `.claude/project-config.json`
- Layer: Cross-cutting
- Step type: wiring
- Test: `N/A — config edit` (validity confirmed by T07 using the same build command)
- Acceptance: `paths.src = "src"`, `paths.tests = "tests"`,
  `commands.build = "dotnet build Graphwright.sln -c Release"`. `commands.{test,lint,coverage,run}`
  and `paths.docs` are left unchanged (out of scope per `00-spec.md:156-157`). No other keys edited;
  JSON remains valid.
- Depends on: none
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: `.claude/project-config.json:40` (build placeholder),
  `.claude/project-config.json:48-49` (paths.src/tests placeholders) — replace these exact lines;
  resolves OQ-1 / OQ-2 (`00-spec.md:161-169`).
