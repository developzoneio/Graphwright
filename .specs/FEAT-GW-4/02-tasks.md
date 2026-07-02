---
id: FEAT-GW-4
type: feature
phase: tasks
created: 2026-07-02
plan: .specs/FEAT-GW-4/01-plan.md
---

# FEAT-GW-4 — Task list

Conventions binding on EVERY task (from CLAUDE.md + global standards): `== false` / `== true`
comparisons (never bare `!expr`); custom domain exceptions only; `Async` suffix +
`CancellationToken ct` as last parameter on all async methods; `#nullable enable` (inherited
from Directory.Build.props — do not re-declare per file); no static classes with static methods
(use `static readonly` instance); `if` always braced; 4-space indent; max 120-char lines;
comments on own line, uppercase start, English. Test files live under `tests/Graphwright.Tests/`
mirroring the source folder structure.

## Checklist

- [x] T01 — Pin new package versions in Directory.Packages.props
- [x] T02 — GraphwrightErrorCode closed enum (Domain)
- [x] T03 — GraphwrightException hierarchy (Domain)
- [x] T04 — Envelope wire DTOs (McpServer)
- [x] T05 — ExceptionEnvelopeMapper (McpServer)
- [x] T06 — IGitnexusTool abstraction + frozen name constants
- [x] T07 — Five schema-described stub tools
- [x] T08 — ToolDispatcher boundary (no exception escapes)
- [x] T09 — ToolRegistry with exact-5 contract assertion
- [x] T10 — InfrastructureModule registrar seam
- [x] T11 — McpServer csproj: exe + ASP.NET Core + MCP SDK packages (OutputType=Exe deferred to T14 — CS5001 without Program.cs)
- [x] T12 — McpServerModule DI registration (shared by both transports)
- [x] T13 — MCP SDK handler adapter (ListTools / CallTool)
- [x] T14 — Program.cs: stdio host + fail-fast registry assertion (absorbed T11's deferred OutputType=Exe)
- [x] T15 — Program.cs: SSE branch (WebApplication + MapMcp)
- [x] T16 — stdio integration test (in-memory handshake)
- [x] T17 — SSE parity test (TestServer)
- [x] T18 — Fill project-config.json commands + paths.docs
- [x] T19 — Register gitnexus stdio server in .mcp.json
- [x] T20 — Lint-gate fix: naming-rule conformance (Phase 5a follow-up)

---

### T01 - Pin new package versions in Directory.Packages.props
- Files: Directory.Packages.props
- Layer: Presentation
- Step type: foundation
- Test: none new — acceptance is `dotnet restore Graphwright.sln` succeeding
- Acceptance: `Directory.Packages.props` pins exact versions for `ModelContextProtocol` (0.3.0-preview.4), `ModelContextProtocol.AspNetCore` (0.3.0-preview.4), `Microsoft.Extensions.DependencyInjection.Abstractions` (8.0.2), `Microsoft.AspNetCore.Mvc.Testing` (8.0.11), `coverlet.collector` (6.0.4); if an exact pin is unavailable on NuGet, substitute the nearest available version, keep it exact (no wildcards), and record the substitution in `03-decisions.md`; restore succeeds
- Depends on: none
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: Directory.Packages.props:8-14 — mirror the existing `<PackageVersion Include="..." Version="..." />` pin style, alphabetical-ish grouping

### T02 - GraphwrightErrorCode closed enum (Domain)
- Files: src/Graphwright.Domain/Errors/GraphwrightErrorCode.cs; tests/Graphwright.Tests/Domain/Errors/GraphwrightErrorCodeTests.cs
- Layer: Domain
- Step type: foundation
- Test: tests/Graphwright.Tests/Domain/Errors/GraphwrightErrorCodeTests.cs — asserts the enum contains exactly the 6 names WorkspaceNotLoaded, SymbolNotFound, FileNotFound, AmbiguousSymbol, InvalidArgument, Internal and nothing else
- Acceptance: enum with exactly 6 members exists in Domain; test proving the closed set is green
- Depends on: none
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: CLAUDE.md:84 — the 6 wire values `WORKSPACE_NOT_LOADED | SYMBOL_NOT_FOUND | FILE_NOT_FOUND | AMBIGUOUS_SYMBOL | INVALID_ARGUMENT | INTERNAL`; enum members are PascalCase, the ALL_UPPER wire strings are produced only by the McpServer mapper (T05)

### T03 - GraphwrightException hierarchy (Domain)
- Files: src/Graphwright.Domain/Exceptions/GraphwrightException.cs; src/Graphwright.Domain/Exceptions/WorkspaceNotLoadedException.cs; src/Graphwright.Domain/Exceptions/SymbolNotFoundException.cs; src/Graphwright.Domain/Exceptions/SourceFileNotFoundException.cs; src/Graphwright.Domain/Exceptions/AmbiguousSymbolException.cs; src/Graphwright.Domain/Exceptions/InvalidToolArgumentException.cs; src/Graphwright.Domain/Exceptions/ToolNotImplementedException.cs; tests/Graphwright.Tests/Domain/Exceptions/GraphwrightExceptionTests.cs
- Layer: Domain
- Step type: foundation
- Test: tests/Graphwright.Tests/Domain/Exceptions/GraphwrightExceptionTests.cs — each derived exception exposes the expected `Code` and `IsRetryable` (WorkspaceNotLoaded→retryable true; ToolNotImplemented→Internal, retryable false; others per type), and messages carry the offending value (symbol name, file path, tool name)
- Acceptance: abstract `GraphwrightException` (abstract `GraphwrightErrorCode Code`, `bool IsRetryable`) plus 6 sealed derived exceptions compile; no derived type maps outside the closed enum; test green
- Depends on: T02
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: CLAUDE.md:101 — exception naming precedent (`SymbolNotFoundException`, `WorkspaceNotLoadedException`; custom domain exceptions only, no result pattern); CLAUDE.md:77-79 — `WORKSPACE_NOT_LOADED` with `retryable: true` is the canonical retryable example

### T04 - Envelope wire DTOs (McpServer)
- Files: src/Graphwright.McpServer/Contracts/ToolSuccessEnvelope.cs; src/Graphwright.McpServer/Contracts/ToolErrorEnvelope.cs; src/Graphwright.McpServer/Contracts/ToolError.cs; tests/Graphwright.Tests/McpServer/Contracts/EnvelopeSerializationTests.cs
- Layer: Presentation
- Step type: foundation
- Test: tests/Graphwright.Tests/McpServer/Contracts/EnvelopeSerializationTests.cs — success serializes to `"ok": true` with payload and NO `error` property; error serializes to `"ok": false` with an `error` object containing exactly `code`, `message`, `retryable` (lowercase property names, `code` as ALL_UPPER string, `retryable` as JSON boolean)
- Acceptance: System.Text.Json round-trip of both envelopes matches the CLAUDE.md wire shape byte-for-byte on property names; test green
- Depends on: none
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: CLAUDE.md:71-84 — the frozen error envelope JSON (`ok`/`error.code`/`error.message`/`error.retryable`); mirror exactly, use `[JsonPropertyName]` for lowercase names

### T05 - ExceptionEnvelopeMapper (McpServer)
- Files: src/Graphwright.McpServer/Contracts/ExceptionEnvelopeMapper.cs; tests/Graphwright.Tests/McpServer/Contracts/ExceptionEnvelopeMapperTests.cs
- Layer: Presentation
- Step type: foundation
- Test: tests/Graphwright.Tests/McpServer/Contracts/ExceptionEnvelopeMapperTests.cs — each of the 6 `GraphwrightException` types maps to its ALL_UPPER wire code with its `IsRetryable`; any non-Graphwright `Exception` maps to `INTERNAL` + `retryable: false`; the mapped `message` for unexpected exceptions is generic (no exception message, no stack trace leaked)
- Acceptance: sealed mapper class exposed as `public static readonly ExceptionEnvelopeMapper Instance` (no static class); instance method `ToErrorEnvelope(Exception ex)` covers the full closed set; test green
- Depends on: T03, T04
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: CLAUDE.md:84 — enum-member → ALL_UPPER wire string mapping is defined here and nowhere else; .specs/FEAT-GW-4/00-spec.md:89 — "unexpected internal failure is mapped to code = INTERNAL (never leaked as a raw stack trace)"

### T06 - IGitnexusTool abstraction + frozen name constants
- Files: src/Graphwright.McpServer/Tools/IGitnexusTool.cs; src/Graphwright.McpServer/Tools/GitnexusToolNames.cs; tests/Graphwright.Tests/McpServer/Tools/GitnexusToolNamesTests.cs
- Layer: Presentation
- Step type: foundation
- Test: tests/Graphwright.Tests/McpServer/Tools/GitnexusToolNamesTests.cs — the frozen set contains exactly the 5 literal strings `mcp__gitnexus__list_symbols`, `mcp__gitnexus__get_file`, `mcp__gitnexus__find_references`, `mcp__gitnexus__get_call_graph`, `mcp__gitnexus__search`, in that deterministic order
- Acceptance: `IGitnexusTool` declares `string Name`, `string Description`, `JsonElement InputSchema`, `Task<ToolSuccessEnvelope> ExecuteAsync(JsonElement arguments, CancellationToken ct)`; `GitnexusToolNames` exposes the 5 `const string` names + a readonly frozen ordered set; test green
- Depends on: T04
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: CLAUDE.md:53-57 — the 5 frozen tool names, copy verbatim (double underscores are load-bearing); CLAUDE.md:102 — async signature convention (`Async` suffix, `CancellationToken ct` last)

### T07 - Five schema-described stub tools
- Files: src/Graphwright.McpServer/Tools/ListSymbolsTool.cs; src/Graphwright.McpServer/Tools/GetFileTool.cs; src/Graphwright.McpServer/Tools/FindReferencesTool.cs; src/Graphwright.McpServer/Tools/GetCallGraphTool.cs; src/Graphwright.McpServer/Tools/SearchTool.cs; tests/Graphwright.Tests/McpServer/Tools/StubToolTests.cs
- Layer: Presentation
- Step type: behavior
- Test: tests/Graphwright.Tests/McpServer/Tools/StubToolTests.cs — for each of the 5 stubs: `Name` equals its `GitnexusToolNames` constant; `InputSchema` is valid JSON with a `type: object` root and the tool's required argument (list_symbols: `file`; get_file: `path`; find_references: `symbol`; get_call_graph: `symbol`; search: `query`); missing/blank required argument throws `InvalidToolArgumentException`; structurally valid arguments throw `ToolNotImplementedException` naming the tool
- Acceptance: 5 sealed classes implement `IGitnexusTool`; each validates its required argument (`TryGetProperty(...) == false` → `InvalidToolArgumentException`) then throws `ToolNotImplementedException`; no TODO/HACK comments; test green
- Depends on: T03, T06
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: CLAUDE.md:53-57 — one stub per table row, name copied verbatim; .specs/FEAT-GW-4/00-spec.md:42-45 — stub contract ("accepts its arguments, validates them at the envelope level if cheaply possible, returns a structured not-implemented failure")

### T08 - ToolDispatcher boundary (no exception escapes)
- Files: src/Graphwright.McpServer/Dispatch/ToolDispatcher.cs; src/Graphwright.McpServer/Dispatch/ToolDispatchResult.cs; tests/Graphwright.Tests/McpServer/Dispatch/ToolDispatcherTests.cs
- Layer: Presentation
- Step type: behavior
- Test: tests/Graphwright.Tests/McpServer/Dispatch/ToolDispatcherTests.cs — fake success tool → `ok: true` envelope with payload and no error; stub with bad args → `ok: false` + `INVALID_ARGUMENT`; stub with valid args → `ok: false` + `INTERNAL` with not-implemented message; fake tool throwing `InvalidOperationException` → `ok: false` + `INTERNAL`, generic message, and the failure is logged with tool name + arguments; unknown tool name → `ok: false` + `INVALID_ARGUMENT`; no test path lets an exception propagate out of `DispatchAsync`
- Acceptance: `ToolDispatcher` (constructor-injected `IReadOnlyList<IGitnexusTool>`, `ILogger<ToolDispatcher>`) exposes `Task<ToolDispatchResult> DispatchAsync(string toolName, JsonElement arguments, CancellationToken ct)`; catch is at the invocation call only (smallest scope), transforms via `ExceptionEnvelopeMapper.Instance`, logs with offending values; test green
- Depends on: T05, T06, T07
- Conflicts with: none
- Complexity: M
- Reversibility: moderate
- Pattern refs: .specs/FEAT-GW-4/00-spec.md:88-89 — "no unhandled exception propagates out of the tool-dispatch boundary"; CLAUDE.md:71-84 — every failure leaves as the envelope, never as a throw

### T09 - ToolRegistry with exact-5 contract assertion
- Files: src/Graphwright.McpServer/Registry/ToolRegistry.cs; src/Graphwright.McpServer/Registry/ToolContractViolationException.cs; tests/Graphwright.Tests/McpServer/Registry/ToolRegistryTests.cs
- Layer: Presentation
- Step type: behavior
- Test: tests/Graphwright.Tests/McpServer/Registry/ToolRegistryTests.cs — registry over the 5 real stubs passes `AssertContract()`; 4 tools (one removed) → throws `ToolContractViolationException` naming the missing tool; 6 tools (fake extra) → throws naming the extra; a renamed tool → throws; a tool outside the `mcp__gitnexus__` prefix → throws; exception message lists expected vs actual sets
- Acceptance: `ToolRegistry` (constructor-injected `IReadOnlyList<IGitnexusTool>`) exposes `Tools` (deterministic order) and `AssertContract()` comparing against `GitnexusToolNames`; violation throws `ToolContractViolationException` (McpServer-local, derives from `GraphwrightException` with code Internal) carrying a logged-friendly diff; test green
- Depends on: T06, T07
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: .specs/FEAT-GW-4/00-spec.md:50-62 — Scenario 1 gherkin is the assertion spec (count, exact names, prefix, fail-fast); CLAUDE.md:49 — "exposes exactly 5 tools"

### T10 - InfrastructureModule registrar seam
- Files: src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs; src/Graphwright.Infrastructure/Graphwright.Infrastructure.csproj; tests/Graphwright.Tests/Infrastructure/DependencyInjection/InfrastructureModuleTests.cs
- Layer: Infrastructure
- Step type: foundation
- Test: tests/Graphwright.Tests/Infrastructure/DependencyInjection/InfrastructureModuleTests.cs — `InfrastructureModule.Instance.RegisterServices(new ServiceCollection())` completes without throwing and the collection builds a provider
- Acceptance: sealed `InfrastructureModule` with `public static readonly InfrastructureModule Instance` and instance method `RegisterServices(IServiceCollection services)` (empty body with an explanatory comment — implementations arrive in later GW-1 stories); csproj gains `Microsoft.Extensions.DependencyInjection.Abstractions` PackageReference (version from central pins); this is the ONLY Infrastructure symbol McpServer may reference (plan D2)
- Depends on: T01
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: src/Graphwright.McpServer/Graphwright.McpServer.csproj:8-10 — versionless `PackageReference` style under central package management; plan 01-plan.md "Decision D2" — registrar-instance pattern instead of static extension class

### T11 - McpServer csproj: exe + ASP.NET Core + MCP SDK packages
- Files: src/Graphwright.McpServer/Graphwright.McpServer.csproj
- Layer: Presentation
- Step type: wiring
- Test: none new — acceptance is `dotnet build Graphwright.sln -c Release` succeeding
- Acceptance: csproj gains `<OutputType>Exe</OutputType>`, `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, and versionless `PackageReference`s for `ModelContextProtocol` and `ModelContextProtocol.AspNetCore`; existing Application + Infrastructure project references untouched; solution restores and builds on net8.0
- Depends on: T01
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: src/Graphwright.McpServer/Graphwright.McpServer.csproj:8-10 — mirror the existing versionless PackageReference ItemGroup; tests/Graphwright.Tests/Graphwright.Tests.csproj:3-5 — PropertyGroup placement precedent

### T12 - McpServerModule DI registration (shared by both transports)
- Files: src/Graphwright.McpServer/DependencyInjection/McpServerModule.cs; tests/Graphwright.Tests/McpServer/DependencyInjection/McpServerModuleTests.cs
- Layer: Presentation
- Step type: wiring
- Test: tests/Graphwright.Tests/McpServer/DependencyInjection/McpServerModuleTests.cs — after `McpServerModule.Instance.RegisterServices(services)` the built provider resolves `ToolRegistry` containing exactly the 5 frozen names, `ToolDispatcher`, and `IReadOnlyList<IGitnexusTool>` with 5 entries; `AssertContract()` on the resolved registry passes
- Acceptance: sealed `McpServerModule` (`static readonly Instance`, `RegisterServices(IServiceCollection services)`) registers the 5 stubs as `IGitnexusTool` singletons plus `ToolRegistry` and `ToolDispatcher`; it is the single registration source both transports consume (parity by construction); test green
- Depends on: T08, T09, T10, T11
- Conflicts with: none
- Complexity: M
- Reversibility: moderate
- Pattern refs: src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs (T10) — mirror the registrar-instance shape exactly; .specs/FEAT-GW-4/00-spec.md:104 — "transport selection does not alter the tool surface"

### T13 - MCP SDK handler adapter (ListTools / CallTool)
- Files: src/Graphwright.McpServer/Transport/McpHandlerAdapter.cs; tests/Graphwright.Tests/McpServer/Transport/McpHandlerAdapterTests.cs
- Layer: Presentation
- Step type: wiring
- Test: tests/Graphwright.Tests/McpServer/Transport/McpHandlerAdapterTests.cs — the ListTools handler output contains exactly the 5 registry tools with their names and input schemas; the CallTool handler for a stub returns a result whose JSON content is the `ok: false` + `INTERNAL` not-implemented envelope; a CallTool for an unknown name returns the `INVALID_ARGUMENT` envelope (never an exception)
- Acceptance: sealed `McpHandlerAdapter` (`static readonly Instance`) exposes `Configure(IMcpServerBuilder builder)` wiring `WithListToolsHandler` (fed from `ToolRegistry`) and `WithCallToolHandler` (delegating to `ToolDispatcher`, serializing the envelope as the tool result content); no attribute-based tool discovery anywhere; compiles against the pinned SDK preview — if the preview's handler API differs from these method names, adapt minimally and record the actual API in `03-decisions.md`; test green
- Depends on: T11, T12
- Conflicts with: none
- Complexity: M
- Reversibility: moderate
- Pattern refs: .specs/FEAT-GW-4/01-plan.md "OQ-3" — low-level handler decision and its three reasons (verbatim names, single choke point, no assembly scanning); CLAUDE.md:53-57 — advertised names must match the table verbatim

### T14 - Program.cs: stdio host + fail-fast registry assertion
- Files: src/Graphwright.McpServer/Program.cs; src/Graphwright.McpServer/Transport/TransportMode.cs; src/Graphwright.McpServer/Transport/TransportModeParser.cs; tests/Graphwright.Tests/McpServer/Transport/TransportModeParserTests.cs
- Layer: Presentation
- Step type: wiring
- Test: tests/Graphwright.Tests/McpServer/Transport/TransportModeParserTests.cs — no args → Stdio; `--transport stdio` → Stdio; `--transport sse` → Sse; unknown value → throws `InvalidToolArgumentException` (mapped to exit 1 in Main)
- Acceptance: `Program.cs` parses transport mode; stdio path builds `Host.CreateApplicationBuilder`, calls `InfrastructureModule.Instance.RegisterServices` + `McpServerModule.Instance.RegisterServices`, configures `AddMcpServer().WithStdioServerTransport()` via `McpHandlerAdapter`, resolves `ToolRegistry` and calls `AssertContract()` BEFORE `RunAsync` — on violation logs the reason and returns exit code 1 (fail-fast, Scenario 1); a comment above the Infrastructure call documents the D2 composition-root constraint; SSE mode returns a not-yet-wired error exit until T15 lands
- Depends on: T13
- Conflicts with: T15
- Complexity: M
- Reversibility: moderate
- Pattern refs: .specs/FEAT-GW-4/00-spec.md:60 — "startup fails fast (non-zero exit, logged reason)"; .specs/FEAT-GW-4/01-plan.md "Decision D2" — only Infrastructure symbol allowed is `InfrastructureModule.Instance.RegisterServices`

### T15 - Program.cs: SSE branch (WebApplication + MapMcp)
- Files: src/Graphwright.McpServer/Program.cs
- Layer: Presentation
- Step type: wiring
- Test: covered by T17 (tests/Graphwright.Tests/McpServer/Transport/SseParityTests.cs); no new unit test in this task
- Acceptance: `--transport sse` builds `WebApplication.CreateBuilder`, applies the SAME two module registrations and `McpHandlerAdapter`, configures `AddMcpServer().WithHttpTransport()` + `app.MapMcp()`, and runs the same `AssertContract()` fail-fast before serving; zero tool/envelope code is duplicated between branches (both consume `McpServerModule` + `McpHandlerAdapter`); stdio behavior unchanged
- Depends on: T14
- Conflicts with: T14
- Complexity: M
- Reversibility: moderate
- Pattern refs: src/Graphwright.McpServer/Program.cs (T14) — mirror the stdio branch structure exactly (register modules → configure builder → assert → run); .specs/FEAT-GW-4/00-spec.md:97-104 — Scenario 4 parity requirement

### T16 - stdio integration test (in-memory handshake)
- Files: tests/Graphwright.Tests/McpServer/Transport/StdioIntegrationTests.cs
- Layer: Presentation
- Step type: test
- Test: tests/Graphwright.Tests/McpServer/Transport/StdioIntegrationTests.cs (this task IS the test)
- Acceptance: using the SDK's stream/in-memory client transport against a server configured exactly as Program's stdio branch (modules + adapter): MCP handshake completes; `tools/list` returns exactly the 5 frozen names with schemas; calling `mcp__gitnexus__search` with valid args returns the `ok: false` + `INTERNAL` not-implemented envelope; calling it with missing `query` returns `INVALID_ARGUMENT`; test green under `dotnet test Graphwright.sln`
- Depends on: T14
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: .specs/FEAT-GW-4/00-spec.md:97-100 — Scenario 4 stdio gherkin; tests/Graphwright.Tests/McpServer/Transport/McpHandlerAdapterTests.cs (T13) — reuse its server-configuration helper rather than duplicating setup

### T17 - SSE parity test (TestServer)
- Files: tests/Graphwright.Tests/McpServer/Transport/SseParityTests.cs; tests/Graphwright.Tests/Graphwright.Tests.csproj
- Layer: Presentation
- Step type: test
- Test: tests/Graphwright.Tests/McpServer/Transport/SseParityTests.cs (this task IS the test)
- Acceptance: tests csproj gains `Microsoft.AspNetCore.Mvc.Testing` PackageReference (central pin from T01); a TestServer hosting the SSE branch configuration serves the MCP handshake to an SDK client over its HttpClient; `tools/list` returns the SAME 5 names/schemas asserted in T16, and one stub call returns the identical envelope shape; if the pinned preview SDK cannot run under TestServer, downgrade to structural parity (assert both transport branches consume `McpServerModule` + `McpHandlerAdapter` as their only tool source) and record the downgrade in `03-decisions.md` (plan risk table); test green
- Depends on: T15, T16
- Conflicts with: none
- Complexity: M
- Reversibility: trivial
- Pattern refs: tests/Graphwright.Tests/McpServer/Transport/StdioIntegrationTests.cs (T16) — mirror its assertions so parity is literal (same expected names/envelope fixtures); .specs/FEAT-GW-4/00-spec.md:101-104 — Scenario 4 SSE gherkin

### T18 - Fill project-config.json commands + paths.docs
- Files: .claude/project-config.json
- Layer: Presentation
- Step type: polish
- Test: none new — acceptance verified by executing each filled command
- Acceptance: `commands.test` = `dotnet test Graphwright.sln`; `commands.lint` = `dotnet format Graphwright.sln --verify-no-changes`; `commands.coverage` = `dotnet test Graphwright.sln --collect:"XPlat Code Coverage"`; `commands.run` = `dotnet run --project src/Graphwright.McpServer`; `paths.docs` = `docs`; no `<<...>>` placeholders remain in `commands` or `paths`; `commands.build`, `paths.src`, `paths.tests` left as-is; each command exits 0 when run from repo root (coverage requires the T01 `coverlet.collector` pin referenced from the tests csproj — add that PackageReference here if T17 has not already)
- Depends on: T16
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: .claude/project-config.json:40 — mirror the already-filled `commands.build` string style (full solution path, no shorthand)

### T19 - Register gitnexus stdio server in .mcp.json
- Files: .mcp.json
- Layer: Presentation
- Step type: polish
- Test: none new — acceptance is a manual/scripted MCP client connect via the entry listing 5 tools
- Acceptance: `.mcp.json` gains a `gitnexus` entry of type `stdio` with `command: "dotnet"` and `args: ["run", "--project", "src/Graphwright.McpServer", "--", "--transport", "stdio"]`; the existing `atlassian` entry is untouched; starting the server through this entry completes the MCP handshake and advertises the 5 tools (project-config `mcp.gitnexus.enabled` is already `true` — no change needed there)
- Depends on: T14
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: .mcp.json:3-6 — mirror the existing `mcpServers` entry structure (key + type + connection fields)

### T20 - Lint-gate fix: naming-rule conformance (Phase 5a follow-up)
- Files: .editorconfig; src/Graphwright.McpServer/Tools/GitnexusToolNames.cs; src/Graphwright.McpServer/Tools/ListSymbolsTool.cs; src/Graphwright.McpServer/Tools/GetFileTool.cs; src/Graphwright.McpServer/Tools/FindReferencesTool.cs; src/Graphwright.McpServer/Tools/GetCallGraphTool.cs; src/Graphwright.McpServer/Tools/SearchTool.cs; src/Graphwright.McpServer/Registry/ToolRegistry.cs; tests/Graphwright.Tests/** (rename private static readonly fields + constant references only)
- Layer: Presentation
- Step type: polish
- Test: existing suite (87 tests) must stay green; `dotnet format Graphwright.sln --verify-no-changes` must exit 0
- Acceptance: `dotnet_diagnostic.CA1707.severity = none` added to .editorconfig (documented: conflicts with the repo's constants_all_upper naming rule); public constants in GitnexusToolNames renamed to ALL_UPPER (LIST_SYMBOLS, GET_FILE, FIND_REFERENCES, GET_CALL_GRAPH, SEARCH) with all usage sites updated; private static readonly fields in test files renamed to _camelCase per private_internal_fields_underscore rule; lint exits 0; suite green
- Depends on: T19
- Conflicts with: none
- Complexity: S
- Reversibility: trivial
- Pattern refs: .editorconfig:72-78 — constants_all_upper + private_internal_fields_underscore rules are the source of truth

---

## Dependency graph (critical path bolded)

```
T01 ──┬─> T10 ──────────────┐
      └─> T11 ──────────────┤
T02 ──> T03 ──┬─> T05 ──┐   │
T04 ──┬───────┘         │   │
      └─> T06 ──┬─> T07 ─┼──┤
                └─> T09 ─┴──┤
                (T05,T06,T07)└─> T08 ──> T12 ──> T13 ──> T14 ──┬─> T16 ──> T18
                                                               ├─> T15 ──> T17
                                                               └─> T19
```

**Critical path**: T01 → T11 → (T12 after T08/T09/T10) → T13 → T14 → T16.

## Complexity summary

| Complexity | Tasks | Count |
|---|---|---|
| S | T01, T02, T04, T06, T10, T11, T18, T19 | 8 |
| M | T03, T05, T07, T08, T09, T12, T13, T14, T15, T16, T17 | 11 |
| L | — | 0 |

Total: **19 tasks** (8 S + 11 M).
