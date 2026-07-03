# FEAT-GW-6 — Decisions & impact analysis

## Impact analysis (sd-code-explorer, Phase 2)

### Direct callers (1-hop)

**GetFileTool:**
- `src/Graphwright.McpServer/DependencyInjection/McpServerModule.cs:52` - registered as `IGitnexusTool` singleton
- `tests/Graphwright.Tests/McpServer/Tools/StubToolTests.cs:16` - test data member instantiated `new GetFileTool()`
- `tests/Graphwright.Tests/McpServer/Dispatch/ToolDispatcherTests.cs:93` - test instantiation `new GetFileTool()`
- `tests/Graphwright.Tests/McpServer/Dispatch/ToolDispatcherTests.cs:110` - test instantiation `new GetFileTool()`
- `tests/Graphwright.Tests/McpServer/Registry/ToolRegistryTests.cs:59` - test instantiation `new GetFileTool()` in `RealStubToolsOutOfOrder()`

**ILanguageProvider:**
- `src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:65` - injected field `_languageProvider`
- `src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:67-72` - constructor parameter and null-check
- `src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs:39` - registered as singleton mapping to `RoslynLanguageProvider`
- `tests/Graphwright.Tests/McpServer/Tools/ListSymbolsToolTests.cs:19-25` - test fake `PoisonLanguageProvider`
- `tests/Graphwright.Tests/McpServer/Tools/ListSymbolsToolTests.cs:27-42` - test fake `CapturingLanguageProvider`
- `tests/Graphwright.Tests/McpServer/Tools/ListSymbolsToolTests.cs:45-57` - test fake `ThrowingLanguageProvider`
- `tests/Graphwright.Tests/McpServer/Registry/ToolRegistryTests.cs:16-24` - test fake `NeverInvokedLanguageProvider`

**RoslynLanguageProvider:**
- `src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs:39` - implements `ILanguageProvider`
- `tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs` - 17 test instantiations (lines 48, 58, 71, 91, 111, 132, 157, 180, 210, 224, 355, 414, 430, 463, 482, 498, 518)

**Tool dispatch path:**
- `src/Graphwright.McpServer/Dispatch/ToolDispatcher.cs:44` - tool lookup by name from injected `_tools` list
- `src/Graphwright.McpServer/Dispatch/ToolDispatcher.cs:71` - `tool.ExecuteAsync()` invocation within dispatch boundary
- `src/Graphwright.McpServer/Registry/ToolRegistry.cs:19` - tools re-ordered to frozen order in constructor
- `src/Graphwright.McpServer/Registry/ToolRegistry.cs:38-39` - contract assertion compares tool names against `GitnexusToolNames.FrozenOrderedNames`

### Transitive callers (2-3 hop)

**McpServerModule → tools → tool consumers:**
- `src/Graphwright.McpServer/Program.cs:98` - `McpServerModule.Instance.RegisterServices(services)` called from composition root
- `src/Graphwright.McpServer/Program.cs:53` - dispatcher used via MCP handler in stdio transport
- `src/Graphwright.McpServer/Program.cs:76` - dispatcher used via MCP handler in SSE transport
- `src/Graphwright.McpServer/Program.cs:109-112` - `ToolRegistry.AssertContract()` called at startup before serving requests

**InfrastructureModule → ILanguageProvider → McpServer:**
- `src/Graphwright.McpServer/Program.cs:97` - `InfrastructureModule.Instance.RegisterServices(services)` called before McpServerModule
- `src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs:38-39` - registers both `RoslynWorkspaceSnapshot.NotLoaded()` and `ILanguageProvider` → `RoslynLanguageProvider` as singletons

**Test composition paths:**
- `tests/Graphwright.Tests/McpServer/DependencyInjection/ListSymbolsCompositionTests.cs:38-39` - full container wiring replicates Program.cs path
- `tests/Graphwright.Tests/Infrastructure/DependencyInjection/InfrastructureModuleTests.cs:33` - resolves `ILanguageProvider` from built container

### Test coverage scan

**Files in target scope WITH direct tests:**
- `src/Graphwright.McpServer/Tools/ListSymbolsTool.cs` → `tests/Graphwright.Tests/McpServer/Tools/ListSymbolsToolTests.cs` (17 facts covering schema, validation, execution, exception propagation)
- `src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs` → `tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynLanguageProviderTests.cs` (18 facts covering scope resolution, symbol extraction, filtering)
- `src/Graphwright.McpServer/DependencyInjection/McpServerModule.cs` → `tests/Graphwright.Tests/McpServer/DependencyInjection/McpServerModuleTests.cs` (at least 2 facts for composition wiring)
- `src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs` → `tests/Graphwright.Tests/Infrastructure/DependencyInjection/InfrastructureModuleTests.cs` (at least 2 facts for composition wiring)
- `src/Graphwright.McpServer/Dispatch/ToolDispatcher.cs` → `tests/Graphwright.Tests/McpServer/Dispatch/ToolDispatcherTests.cs` (3+ facts covering success, exception mapping, tool lookup)
- `src/Graphwright.McpServer/Registry/ToolRegistry.cs` → `tests/Graphwright.Tests/McpServer/Registry/ToolRegistryTests.cs` (6 facts covering contract assertion, ordering, missing/extra/renamed tools)

**Files in target scope WITHOUT direct tests:**
- `src/Graphwright.McpServer/Tools/GetFileTool.cs` - **COVERAGE GAP**: no `GetFileToolTests.cs` exists. GW-5 precedent: `ListSymbolsToolTests.cs` mirrors `ListSymbolsTool.cs` exactly. **Gap to fill**: create `tests/Graphwright.Tests/McpServer/Tools/GetFileToolTests.cs` with the 16 scenarios per spec, mirroring `ListSymbolsToolTests` structure.
- `src/Graphwright.Application/LanguageProviders/ILanguageProvider.cs` - interface only; tested transitively via fakes in `ListSymbolsToolTests.cs` and the real implementation in `RoslynLanguageProviderTests.cs`. **Gap introduced by GW-6**: `GetFileAsync` addition requires new test fakes.

### DI / config grep

**ILanguageProvider registration:**
- `src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs:39` - `services.AddSingleton<ILanguageProvider, RoslynLanguageProvider>();`
- `src/Graphwright.McpServer/Program.cs:97` - `InfrastructureModule.Instance.RegisterServices(services);`

**GetFileTool registration:**
- `src/Graphwright.McpServer/DependencyInjection/McpServerModule.cs:52` - `services.AddSingleton<IGitnexusTool, GetFileTool>();`

**ToolDispatcher / ToolRegistry:**
- `src/Graphwright.McpServer/DependencyInjection/McpServerModule.cs:59-60` - bridges `IEnumerable<IGitnexusTool>` to `IReadOnlyList<IGitnexusTool>`
- `src/Graphwright.McpServer/DependencyInjection/McpServerModule.cs:62-63` - registers `ToolRegistry` and `ToolDispatcher` as singletons

**RoslynWorkspaceSnapshot registration:**
- `src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs:38` - `services.AddSingleton(RoslynWorkspaceSnapshot.NotLoaded());`

### Public API surface changes

**New public symbols:**
- `src/Graphwright.Application/LanguageProviders/ILanguageProvider.cs` - adds `Task<FileContentResult> GetFileAsync(GetFileQuery query, CancellationToken ct);` (parallel to `ListSymbolsAsync`)

**New public DTOs:**
- `src/Graphwright.Application/LanguageProviders/GetFileQuery.cs` (new) - mirrors `ListSymbolsQuery.cs`. Fields: `path` (required), `start_line?`, `end_line?`, `around_line?`, `context?` (default 2). Mutual-exclusivity validation stays in the tool handler, not the DTO (mirrors `ListSymbolsQuery`).
- `src/Graphwright.Application/LanguageProviders/FileContentResult.cs` (new) - mirrors `SymbolListResult.cs`. Fields: `file`, `start_line`, `end_line`, `total_lines`, `content`.

**GetFileTool.InputSchema changes:**
- `src/Graphwright.McpServer/Tools/GetFileTool.cs:19-30` - replaces path-only stub schema with the expanded `start_line?`/`end_line?`/`around_line?`/`context?` schema, `required: ["path"]` unchanged.

**Consumer changes to GetFileTool:**
- `src/Graphwright.McpServer/Tools/GetFileTool.cs:40-50` - `ExecuteAsync` signature unchanged; implementation replaces `ToolNotImplementedException` with real validation + `_languageProvider.GetFileAsync()` call.

**RoslynLanguageProvider.GetFileAsync implementation (new method, mirrors ListSymbolsAsync structure):**
- `_snapshot.IsLoaded` check → `WorkspaceNotLoadedException` (precedent: line 42-45)
- `ResolveScopedDocumentsOrThrow(path, includeGenerated: false)` reused for single-file resolution (precedent: line 51, 82-124)
- `document.GetTextAsync()` to read file content (NEW — no precedent in current code)
- `SourceText.Lines.Count` for `total_lines` (NEW — only `GetLineSpan` used today)
- `SyntaxNode.FindNode(TextSpan)` for bounded-snippet boundary-snapping (NEW — no precedent; requires `GetSyntaxRootAsync`)

### Risk assessment

**High risk — boundary-snapping logic (`GetFileAsync`, snippet mode):** Scenario 11/15 requires `SyntaxNode.FindNode(TextSpan)` to expand snippet window edges outward to complete statement/expression boundaries per-edge, with a header-region clamp and a hard ceiling at the enclosing `MemberDeclarationSyntax` span. Zero precedent in the codebase for this exact logic. Risk: off-by-one errors in line↔TextSpan conversion, incorrect node-containment checks, header-vs-body edge cases. Mitigation: spec's decided rule (00-spec.md Scenario 11/15) is concrete enough to implement directly; unit tests must cover both body-region and header-region cases with multi-line signatures.

**High risk — GetFileTool constructor signature change breaks 4 test call sites:** all currently call `new GetFileTool()` with no arguments:
- `tests/Graphwright.Tests/McpServer/Tools/StubToolTests.cs:16`
- `tests/Graphwright.Tests/McpServer/Dispatch/ToolDispatcherTests.cs:93`
- `tests/Graphwright.Tests/McpServer/Dispatch/ToolDispatcherTests.cs:110`
- `tests/Graphwright.Tests/McpServer/Registry/ToolRegistryTests.cs:59`
Precedent: GW-5 hit the exact same break when `ListSymbolsTool` went real (`ToolRegistryTests` already has a `NeverInvokedLanguageProvider` fake for this purpose — reuse the pattern). Mitigation: plan phase must enumerate all 4 sites; implementer updates each to inject a fake `ILanguageProvider`.

**Medium risk — `ToolDispatcherTests.cs:108-120` asserts "not implemented" on GetFileTool:** currently expects `new GetFileTool()` + valid `{"path":"..."}` to throw `ToolNotImplementedException`. Once real, this test must be re-pointed at a still-stubbed tool (`FindReferencesTool`, `GetCallGraphTool`, or `SearchTool`) — same move GW-5 made when it re-pointed this test from `ListSymbolsTool` to `GetFileTool`.

**Medium risk — `StubToolTests.cs` assumes 4 remaining stubs:** the `StubTools()` data member (lines 14-20) and its line-11 comment currently enumerate 4 tools; once `GetFileTool` goes real this drops to 3 (`FindReferencesTool`, `GetCallGraphTool`, `SearchTool`). Mitigation: straightforward line deletions, mirrors what GW-5 did when removing `ListSymbolsTool`'s row.

**Low risk — RoslynWorkspaceTestFixtures reuse:** spec mandates reusing GW-5's in-memory `Solution`/`SourceText` fixtures (`RoslynWorkspaceTestFixtures.BuildSnapshot()`, already populates `SourceText.From(...)` at line 71). No new fixture machinery needed.

**Low risk — exception mapping:** no new `GraphwrightException` subclass needed; `WorkspaceNotLoadedException`, `SourceFileNotFoundException`, `InvalidToolArgumentException` are already mapped by `ExceptionEnvelopeMapper` (`src/Graphwright.McpServer/Contracts/ExceptionEnvelopeMapper.cs:50-59`).

**Low risk — DI wiring:** no new registrations needed; existing `ILanguageProvider` → `RoslynLanguageProvider` singleton and existing `IEnumerable<IGitnexusTool>` bridging already satisfy `GetFileTool`'s new constructor shape.

### Precedents & conventions

**Nearest similar implementations:**
1. `ListSymbolsTool` (`src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:23-93`) — identically shaped MCP tool; `GetFileTool` mirrors its constructor injection (line 65-72), validate-before-Roslyn-work discipline (line 83-87), exception propagation (line 90), payload mapping (line 202-249). Spec explicitly names it as the template (00-spec.md:81-84).
2. `RoslynLanguageProvider.ListSymbolsAsync` (`src/Graphwright.Infrastructure/LanguageProviders/RoslynLanguageProvider.cs:40-69`) — workspace-readiness check, file resolution via `ResolveScopedDocumentsOrThrow` (line 82-124) and `IsIncludedByDefault` (line 126-152), both reused directly by `GetFileAsync`.
3. `RoslynWorkspaceTestFixtures` (`tests/Graphwright.Tests/Infrastructure/LanguageProviders/RoslynWorkspaceTestFixtures.cs`) — in-memory `AdhocWorkspace` + `SourceText.From(...)` (line 71), reused unchanged for `GetFileAsync` tests.

**Conventions observed:**
- Naming: tool handlers `{ToolName}Tool.cs`; query/result DTOs `{Name}Query.cs` / `{Name}Result.cs`; providers `{LanguageName}LanguageProvider.cs`.
- Test naming: `ExecuteAsyncThrows{ExceptionType}When{Condition}` / `ExecuteAsyncReturns{ResultShape}When{Condition}` (tool tests); `{Method}Throws{ExceptionType}When{Condition}` (provider tests); fakes named `{Adjective}LanguageProvider`.
- Test placement mirrors source structure 1:1 (`McpServer/Tools/*ToolTests.cs`, `Infrastructure/LanguageProviders/*Tests.cs`, `*DependencyInjection/*CompositionTests.cs`).
- `== false` / `== true` negation throughout; custom domain exceptions only; `Async` suffix + trailing `CancellationToken ct`.
- Validation discipline: tool handlers validate all arguments before calling the provider; providers assume validated input and never re-validate query structure.

**Existing utilities relevant to this spec's scope:**
- `ListSymbolsTool.ValidateOptionalPath` (`src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:95-118`) — reused for `get_file`'s required-path validation (Scenario 12).
- `SourceFileNotFoundException` (`src/Graphwright.Domain/Exceptions/SourceFileNotFoundException.cs`) — already mapped to `FILE_NOT_FOUND`; reused for Scenario 13.
- `ExceptionEnvelopeMapper` (`src/Graphwright.McpServer/Contracts/ExceptionEnvelopeMapper.cs:15-66`) — no changes needed; all 3 exceptions this tool raises are already mapped.
- `RoslynWorkspaceSnapshot` (`src/Graphwright.Infrastructure/LanguageProviders/RoslynWorkspaceSnapshot.cs`) — `IsLoaded`/`Solution` reused identically to `ListSymbolsAsync`.

**Note:** GitNexus MCP server was disabled during this analysis; transitive callers and call graphs were determined via `Grep`/`Glob` (lexical matching + file structure inference) rather than semantic call-graph resolution. `GetFileAsync`'s `FindNode` boundary-snapping logic cannot be validated against actual Roslyn runtime behavior without execution — correctness depends on implementation-time testing against spec Scenarios 11 and 15.

## T04 implementation confirmation — Scenario 9a exception type

00-spec.md pinned the *behavior* for Scenario 9a (`start_line` itself beyond `total_lines`:
reject as `INVALID_ARGUMENT`, non-retryable) but not which layer/exception type raises it, since
`RoslynLanguageProvider` (Infrastructure) had never previously raised an `INVALID_ARGUMENT`-mapped
exception — only `WorkspaceNotLoadedException` and `SourceFileNotFoundException` were precedented
there. **Confirmed during T04 implementation**: `RoslynLanguageProvider.GetFileAsync` raises
`InvalidToolArgumentException(argumentName: "start_line", reason: "must not exceed the file's
total line count (<N>).")` directly — the same domain exception
(`src/Graphwright.Domain/Exceptions/InvalidToolArgumentException.cs`) `GetFileTool`/`ListSymbolsTool`
already raise for tool-owned, structural `INVALID_ARGUMENT` validation failures (01-plan.md
Decision D2's "provider-owned validation" category, Scenarios 9/9a/10/11/15) — no new exception
type introduced, and `ExceptionEnvelopeMapper` requires no change since this exception type is
already mapped. This means `InvalidToolArgumentException` is no longer exclusively a
tool-handler-raised type as of GW-6: `RoslynLanguageProvider` (Infrastructure) now raises it too,
for the one provider-owned validation failure (Scenario 9a) that 01-plan.md Decision D2 explicitly
assigns to the provider rather than the tool handler.
