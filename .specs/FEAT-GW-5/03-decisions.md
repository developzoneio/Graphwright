# FEAT-GW-5 — Decisions & impact analysis

## Impact analysis (sd-code-explorer)

### Direct callers (1-hop)

- `src/Graphwright.McpServer/DependencyInjection/McpServerModule.cs:51` - `RegisterServices` -> registers `ListSymbolsTool` as singleton `IGitnexusTool`
- `src/Graphwright.McpServer/Dispatch/ToolDispatcher.cs:71` - `InvokeAndMapFailuresAsync` calls `tool.ExecuteAsync(arguments, ct)` on the ListSymbolsTool instance from the injected `IReadOnlyList<IGitnexusTool>`

### Transitive callers (2-3 hop)

- `src/Graphwright.McpServer/Program.cs:98` - `RegisterModules` calls `McpServerModule.Instance.RegisterServices(services)`
- `src/Graphwright.McpServer/Program.cs:57` - `RunStdioAsync` calls `TryAssertToolContractAsync(host.Services)` which resolves and validates the tool registry (line 109) before serving requests
- `src/Graphwright.McpServer/Program.cs:81` - `RunSseAsync` calls `TryAssertToolContractAsync(app.Services)` (same contract assertion)
- `src/Graphwright.McpServer/Transport/McpHandlerAdapter.cs:91` - `HandleCallToolAsync` calls `toolDispatcher.DispatchAsync(callParams.Name, arguments, ct)` which locates and invokes ListSymbolsTool
- `src/Graphwright.McpServer/Dispatch/ToolDispatcher.cs:54` - `DispatchAsync` calls `InvokeAndMapFailuresAsync(tool, arguments, ct)` which calls `tool.ExecuteAsync` (line 71)
- `src/Graphwright.McpServer/Dispatch/ToolDispatcher.cs:78` - Exception from `tool.ExecuteAsync` is caught and mapped via `ExceptionEnvelopeMapper.Instance.ToErrorEnvelope(ex)`

### Test coverage scan

**Files in scope with test coverage:**
- `src/Graphwright.McpServer/Tools/ListSymbolsTool.cs` -> `tests/Graphwright.Tests/McpServer/Tools/StubToolTests.cs` (tests stub behavior only: validates "file" required argument, throws ToolNotImplementedException with valid args)
- `src/Graphwright.McpServer/Contracts/ExceptionEnvelopeMapper.cs` -> `tests/Graphwright.Tests/McpServer/Contracts/ExceptionEnvelopeMapperTests.cs` (tests exception-to-envelope mapping; covers WorkspaceNotLoadedException, InvalidToolArgumentException, SourceFileNotFoundException)
- `src/Graphwright.McpServer/Dispatch/ToolDispatcher.cs` -> `tests/Graphwright.Tests/McpServer/Dispatch/ToolDispatcherTests.cs` (tests dispatch boundary; includes ListSymbolsTool stub dispatch scenarios)

**Test gaps:**
- No tests for ListSymbolsTool's real symbol-enumeration logic (implementation in GW-5)
- StubToolTests line 15 hardcodes "file" as required argument — will need update when InputSchema is replaced to `path?/name_filter?/kinds?/include_generated?/max_results?`

### DI / config grep

- `src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs:31-36` - `RegisterServices` is a no-op: `// Implementations arrive with the later GW-1 stories; the seam keeps Program.cs stable.` No ILanguageProvider, IWorkspaceLoader, IIndexer, or any Roslyn workspace registration exists.
- `src/Graphwright.McpServer/DependencyInjection/McpServerModule.cs:62-63` - `ToolRegistry` and `ToolDispatcher` are registered as singletons via explicit constructor injection (lines 62-63)

### Public API surface

- `src/Graphwright.McpServer/Tools/IGitnexusTool.cs:22-47` - Interface contract: `Name`, `Description`, `InputSchema`, `ExecuteAsync(JsonElement, CancellationToken)`
- `src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:19-30` - Current InputSchema: `{ "type": "object", "properties": { "file": { "type": "string" } }, "required": ["file"] }`
  - **Spec conflict**: The current schema requires `file` (line 28), but GW-5 spec (00-spec.md Input section) replaces this with optional `path?/name_filter?/kinds?/include_generated?/max_results?`
- `src/Graphwright.McpServer/Tools/ListSymbolsTool.cs:34-39` - Current tool advertises name `mcp__gitnexus__list_symbols` (line 34), description at line 37
- `src/Graphwright.McpServer/Contracts/ToolSuccessEnvelope.cs` - Return type for successful execution (generic over JsonElement per IGitnexusTool contract)

### Risk assessment

**High risk:**
- **OQ-1 blocker (workspace-loading seam missing)**: `src/Graphwright.Infrastructure/DependencyInjection/InfrastructureModule.cs:31-36` is empty. No `ILanguageProvider`, `IWorkspaceLoader`, or `IIndexer` interface exists in Application layer, and no Roslyn `MSBuildWorkspace` integration exists in Infrastructure layer. Until this seam is defined and a workspace-loading story lands, ListSymbolsTool can only raise `WORKSPACE_NOT_LOADED` (Scenario 10) — it cannot enumerate real symbols (Scenarios 1-9). **The spec (OQ-1) flags this as a blocker that "may block implementation if no such seam exists by the time this story is planned."** Recommend resolving OQ-1 as a design decision before GW-5 planning commits to scope.
- **StubToolTests hardcoded dependency on old InputSchema**: `tests/Graphwright.Tests/McpServer/Tools/StubToolTests.cs:15` expects `ListSymbolsTool` to require a `"file"` argument. When the tool's InputSchema is replaced (per spec), the test at line 47-60 will fail because `name_filter`, `kinds`, etc. are optional, not required. **Mitigation**: Update `StubToolTests.ToolsWithRequiredArguments` and the `InputSchemaIsAnObjectSchemaRequiringItsArgument` test (line 46-61) to reflect the new optional-only shape, or refactor the test data to handle optional-argument tools separately.

**Medium risk:**
- **Path-casing normalization (OQ-3)**: CLAUDE.md lists this as an unresolved project-wide open decision. The spec (00-spec.md Scenario 1 "file matches the input path exactly, using forward slashes, relative to the project root") and success criteria (line 226) mandate forward-slash paths, but does not specify Windows case normalization. Golden-file test assertions (Scenario 12 and others) will be OS-dependent without resolution. **Recommend**: ADR or design decision before test fixtures are written for this story.
- **`kinds` vocabulary undefined (OQ-2)**: The spec (00-spec.md "kinds?" line 56) defers the symbol-kind literal set to planning. No schema validation can be implemented until the closed set is defined. ExceptionEnvelopeMapper and domain exceptions already support `INVALID_ARGUMENT` (line 58 in ExceptionEnvelopeMapper.cs), so the mapping infrastructure is ready.
- **Multiple-tool test fixture brittleness**: StubToolTests uses a `StubTools()` generator (line 13-20) that currently yields 5 rows. If GW-5 changes ListSymbolsTool's schema and behavior but not the others, the parameterized tests will still run old assertions against the modified tool. **Mitigation**: Consider splitting ListSymbolsTool tests into a separate file once it becomes non-stub.

**Low risk:**
- **ExceptionEnvelopeMapper coverage**: ExceptionEnvelopeMapperTests (line 18-116) already tests all three exception types the spec requires (`WorkspaceNotLoadedException`, `InvalidToolArgumentException`, `SourceFileNotFoundException`). No mapper changes needed.
- **Tool registry and dispatch machinery**: ToolRegistry and ToolDispatcher are stable; they do not change in GW-5. ListSymbolsTool is purely a tool implementation, not a dispatch or registry change.

### Precedents & conventions

**Nearest similar implementations (to compare the workspace-loading seam):**
- None in src/ yet — GW-26 (FEAT-GW-26/00-spec.md) defined the layered solution structure but deferred all actual service implementations to later stories. GW-4 (FEAT-GW-4/00-spec.md) defined the tool dispatch boundary and exception envelope, but all tools are stubs.

**Conventions observed in target scope (Tool implementations):**
- **File placement**: Tools live in `src/Graphwright.McpServer/Tools/` alongside the interface `IGitnexusTool.cs` (observed in `ListSymbolsTool.cs`, `GetFileTool.cs`, `FindReferencesTool.cs`, etc. — evidence: `src/Graphwright.McpServer/Tools/*.cs`)
- **Input schema pattern**: Each tool caches its InputSchema as a static readonly JsonElement field and clones it on access (e.g., ListSymbolsTool line 32, ToolDispatcher test fakes line 20). Convention: `JsonDocument.Parse(jsonString).RootElement.Clone()` to avoid aliasing mutations.
- **Exception raising pattern**: Tools raise domain exceptions from `Graphwright.Domain.Exceptions` (e.g., InvalidToolArgumentException in ListSymbolsTool line 47) rather than returning error payloads. The dispatch boundary (ToolDispatcher) catches and maps them (evidence: ToolDispatcher.cs:74-80).
- **Async naming and cancellation**: All tool implementations use `ExecuteAsync` (not Execute or ExecuteToolAsync) and accept `CancellationToken ct` as the last parameter (observed in IGitnexusTool.cs:46, ListSymbolsTool.cs:41).
- **DI pattern**: Services are registered as singletons in a module's `RegisterServices(IServiceCollection)` instance method, following a no-static-class convention (McpServerModule.cs:47, InfrastructureModule.cs:31).

**Existing utilities relevant to GW-5 scope:**
- `ExceptionEnvelopeMapper.Instance` (line 15) - singleton mapper ready for use in GW-5 tool logic; no new mapping code needed
- `WorkspaceNotLoadedException`, `InvalidToolArgumentException`, `SourceFileNotFoundException` — all three domain exceptions already exist and are tested; GW-5 reuses them unchanged (per spec success criteria line 237-241)
- `ToolDispatcher` and `ToolRegistry` — both stable; GW-5 does not modify the dispatch or registration path
