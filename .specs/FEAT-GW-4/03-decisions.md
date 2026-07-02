# FEAT-GW-4 — Decisions & Impact Analysis

> Running log of impact findings and decisions for the MCP server scaffold story.

## Phase 5a diagnosis — lint gate failure (2026-07-02)

`dotnet format --verify-no-changes` failed on two fronts after T01–T19:
1. ENDOFLINE (LF vs CRLF) across all new .cs files — auto-fixed inline with `dotnet format`.
2. IDE1006 naming violations with no auto-fix: (a) `.editorconfig` rule `constants_all_upper`
   (constants at ANY accessibility must be ALL_UPPER, severity error) conflicts with analyzer
   CA1707 (no underscores in externally visible members), which had driven T06 to PascalCase
   public constants; (b) private `static readonly` test fields (e.g. `EXPECTED_NAMES`) violate
   `private_internal_fields_underscore` (`_camelCase`) — they are not `const`, so the ALL_UPPER
   constants rule does not apply to them.

Decision: the repo `.editorconfig` naming rules are the codified user standard and win over
CA1707. Fix routed through Phase 4 as T20: disable CA1707 repo-wide in `.editorconfig`, rename
public constants to ALL_UPPER, rename private static readonly test fields to `_camelCase`.

---

## Phase 2 — Impact analysis (sd-code-explorer, 2026-06-26)

GitNexus MCP index unavailable (greenfield, no built server). Read/Grep/Glob only. Transitive
callers and call graphs are N/A — there is no source code. This scaffold introduces 100% net-new
surface; "impact" here means the **external contract** the scaffold must satisfy and the **config
wiring** it must hook into.

### 1. Existing code state — greenfield confirmed

- `**/*.{cs,csproj,sln,fs,fsproj}` → **no files**. Zero .NET source/project/solution files.
- `{src,tests}/**/*` → **no files**. Neither `src/` nor `tests/` exists.
- The 4 layer projects (`Graphwright.Domain/Application/Infrastructure/McpServer`) exist only as
  config-level layer globs: [.claude/project-config.json:51-56](.claude/project-config.json#L51-L56) and CLAUDE.md "Layer map".
- `mcp-contract.md` — cited as frozen schema source-of-truth at CLAUDE.md "Month 1 Definition of Done"
  (`schemas match mcp-contract.md exactly`) and Key files table — **does not exist in the repo**.
  Same for `Graphwright-Project-Brief.md`. **The only on-disk contract source is CLAUDE.md.**

### 2. External consumers of the contract (must be satisfied byte-for-byte)

The 5 frozen tool names live in CLAUDE.md "MCP tool surface": `mcp__gitnexus__list_symbols`,
`get_file`, `find_references`, `get_call_graph`, `search`. Restated in
[00-spec.md:54-65](.specs/FEAT-GW-4/00-spec.md#L54-L65) and the ticket snapshot
[GW-4.md:26-27](.specs/FEAT-GW-4/04-artifacts/ticket/GW-4.md#L26-L27).

The error envelope shape + closed `code` set (`WORKSPACE_NOT_LOADED | SYMBOL_NOT_FOUND |
FILE_NOT_FOUND | AMBIGUOUS_SYMBOL | INVALID_ARGUMENT | INTERNAL`) live in CLAUDE.md "Error envelope".
Restated [00-spec.md:84-90](.specs/FEAT-GW-4/00-spec.md#L84-L90).

Cross-cutting output rules (exact 1-based `file:line`, relative forward-slash paths,
bin/obj/`*.g.cs`/node_modules exclusion, 1–5 line snippets, 50-result cap, deterministic ordering)
are in CLAUDE.md "Cross-cutting rules" — **out of scope for the scaffold** (they constrain real
tool output) but binding for later stories. The scaffold only fixes the **envelope** carrying them.

The 3 consuming agents (`sd-code-explorer`, `sd-debugger`, `sd-reviewer`) are **external**; their
definition files are NOT in this repo. `list_symbols` is the availability probe (CLAUDE.md
"Availability probe"). `gitnexus._use` describes their dependency at
[.claude/project-config.json:104-105](.claude/project-config.json#L104-L105).

### 3. Config / DI wiring touchpoints

- [.claude/project-config.json:103-106](.claude/project-config.json#L103-L106) — `mcp.gitnexus.enabled`
  is the toggle consumers read; the scaffold's server is what it activates. (Currently `false`.)
- [.mcp.json:1-8](.mcp.json#L1-L8) — registers **only** `atlassian` (http). **No** Graphwright/gitnexus
  stdio or SSE server entry exists yet. A server entry must be added for agents to reach it (transport = OQ-3).
- [.claude/project-config.json:39-45](.claude/project-config.json#L39-L45) — `commands.{build,test,lint,coverage,run}`
  all placeholders → **OQ-1**. No build/test/lint gate can run until filled.
- [.claude/project-config.json:47-49](.claude/project-config.json#L47-L49) — `paths.{src,tests,docs}`
  placeholders → **OQ-5**. Scaffold fixes layout (`src/`, `tests/`); keys must be set to match.
- [.claude/project-config.json:51-56](.claude/project-config.json#L51-L56) — `paths.layers[]` already
  names the 4 assemblies inside-out; the scaffold's project references must satisfy this ordering (§1.1).
- [.claude/project-config.json:58-62](.claude/project-config.json#L58-L62) — `paths.protected`
  (constitution.md, index.md, LICENSE) — not scaffold-touched.

### 4. Public API surface introduced (net-new foundation)

- 5 MCP tool registrations (named **stubs** with schemas), names frozen per CLAUDE.md.
- Success envelope DTO: `ok: true` + payload, no `error` object.
- Error envelope DTO: `ok: false` + `{code, message, retryable}`, `code` ∈ 6-value closed enum.
- Domain exceptions feeding the envelope (e.g. `SymbolNotFoundException`, `WorkspaceNotLoadedException`)
  — custom domain exceptions only, no result pattern, no generic `Exception`.
- 4 project assemblies: `Graphwright.{Domain,Application,Infrastructure,McpServer}`.
- Seam interfaces MAY be declared (not implemented) in Application: `ILanguageProvider`, `IIndexer`,
  `IGraphStore`.
- Tool-registry self-assertion (fail-fast on count/name drift) — a startup contract.
- Transport surface: stdio + SSE serving the identical 5 tools/envelope (SSE depth deferred — OQ-2).

### 5. Test coverage scan

- Zero existing tests. New test project must live under `tests/` (OQ-5; set `paths.tests`).
- Tests this scaffold must add: registry-asserts-exactly-5; success `ok:true`; each producible error
  `code` (`INVALID_ARGUMENT`, `INTERNAL`, + a not-implemented case); no-exception-escapes-boundary→INTERNAL;
  stdio+SSE parity.
- Coverage % gate blocked on OQ-1 (test command) and constitution §3 placeholders.

### 6. Risk assessment (ranked likelihood × impact)

| Risk | L×I | Note / mitigation |
|---|---|---|
| **Contract drift from CLAUDE.md frozen names/envelope** | High×High | `mcp-contract.md` missing; only prose to copy from. A typo in a tool name or `code` breaks zero-change Specwright compat for all 3 external agents. Mitigated by the self-asserting registry test. |
| **Build/test/lint commands unset (OQ-1)** | Certain×High | Blocks Phase 4/5 gates. Must fill `commands.*` with real `dotnet` commands. |
| **Envelope-type layer placement (OQ-4)** | Med×Med-High | DTO crosses McpServer (serialize) + inner layers (exceptions). Wrong placement forces an inner layer outward → violates §1.1. |
| **Transport/SDK choice (OQ-3) + SSE scope (OQ-2)** | Med×Med | Dictates how registry/envelope hook into dispatch and the `.mcp.json` server entry shape. |
| **`paths.src`/`paths.tests` placeholders (OQ-5)** | Certain×Low | Mechanical config string fix once layout fixed to `src/`+`tests/`. |
| **Missing `mcp-contract.md` / `Graphwright-Project-Brief.md`** | — | Documentation-completeness gap; folded into contract-drift risk. |

### Precedents & conventions

- **No in-repo precedents** (greenfield). The only precedent is the external GitNexus surface mirrored
  in CLAUDE.md. SportsBook test repo `smp-jt-services` is referenced but not present here.
- Conventions to follow (from docs, no code to sample): assembly naming `Graphwright.<Layer>`; tool
  naming `mcp__gitnexus__<snake_case>`; error `code` `ALL_UPPER_SNAKE` closed set; C# rules `== false`/
  `== true` over `!`, custom domain exceptions only, `Async` suffix + trailing `CancellationToken ct`,
  `#nullable enable`; test placement under `tests/`.

## T13 — actual MCP SDK API (0.3.0-preview.4)

Verified against the pinned packages on disk (`ModelContextProtocol` / `ModelContextProtocol.Core` /
`ModelContextProtocol.AspNetCore` `0.3.0-preview.4`) via reflection probes, not memory. The
plan's OQ-3 guess (`WithListToolsHandler` / `WithCallToolHandler` as builder extension methods)
matched exactly; no deviation was required.

- `Microsoft.Extensions.DependencyInjection.IMcpServerBuilder` — returned by
  `services.AddMcpServer(...)` (extension in the same namespace, package `ModelContextProtocol`).
- `Microsoft.Extensions.DependencyInjection.McpServerBuilderExtensions` (also package
  `ModelContextProtocol`, namespace `Microsoft.Extensions.DependencyInjection`) exposes:
  - `IMcpServerBuilder WithListToolsHandler(this IMcpServerBuilder builder, Func<RequestContext<ListToolsRequestParams>, CancellationToken, ValueTask<ListToolsResult>> handler)`
  - `IMcpServerBuilder WithCallToolHandler(this IMcpServerBuilder builder, Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult>> handler)`
  - Both are fluent (return the same builder) and, under the hood, set
    `McpServerOptions.Capabilities.Tools.ListToolsHandler` / `.CallToolHandler` via the options
    pipeline (confirmed by resolving `IOptions<McpServerOptions>>().Value.Capabilities.Tools.*Handler`
    after calling the extension methods on a bare `ServiceCollection`).
- `ModelContextProtocol.Server.RequestContext<TParams>` (package `ModelContextProtocol.Core`):
  - `RequestContext(IMcpServer server)` — `server` is **non-nullable** (verified via
    `NullabilityInfoContext`); it also reads `server.Services` inside the constructor to seed its
    own `Services` default, so any `IMcpServer` fake used in tests must have a non-throwing
    `Services` getter even if the test immediately overrides `Services` via object initializer.
  - `Services` (`IServiceProvider?`) and `Params` (`TParams?`) are both **nullable** per
    `NullabilityInfoContext`, even though the SDK always populates them for a real request; `Server`
    is non-nullable.
- `ModelContextProtocol.Protocol.Tool` — `Name`, `Description`, `InputSchema` (`JsonElement`) map
  1:1 onto `IGitnexusTool`'s members; no schema translation needed.
- `ModelContextProtocol.Protocol.CallToolRequestParams` — `Name` (non-nullable `string`),
  `Arguments` (`IReadOnlyDictionary<string, JsonElement>?`, nullable).
- `ModelContextProtocol.Protocol.CallToolResult` — `Content` (`IList<ContentBlock>`), `IsError`
  (`bool?`). The adapter never sets `IsError`; both `ok:true` and `ok:false` envelopes are carried
  as `Content`, never as a protocol-level error, per the plan's design guidance.
- `ModelContextProtocol.Protocol.TextContentBlock` — `new TextContentBlock { Text = "..." }`
  serializes as `{"type":"text","text":"..."}`; `Type` is fixed to `"text"` by the SDK, not
  settable.
- `IMcpServer` (package `ModelContextProtocol.Core`) extends `IMcpEndpoint` + `IAsyncDisposable`,
  with 6 own members (`ClientCapabilities`, `ClientInfo`, `ServerOptions`, `Services`,
  `LoggingLevel`, `RunAsync`) plus 4 inherited (`SessionId`, `SendRequestAsync`,
  `SendMessageAsync`, `RegisterNotificationHandler`) — relevant only to the test fake, not
  production code, since `McpHandlerAdapter`'s handlers read `RequestContext.Services` /
  `RequestContext.Params`, never `RequestContext.Server`.
