---
id: FEAT-GW-4
type: feature
phase: plan
created: 2026-07-02
spec: .specs/FEAT-GW-4/00-spec.md
impact: .specs/FEAT-GW-4/03-decisions.md
---

# FEAT-GW-4 — Implementation plan: MCP server foundation

## Scope correction (impact analysis is stale)

The impact analysis (`03-decisions.md`, 2026-06-26) assumed greenfield. **FEAT-GW-26 has since
landed the solution skeleton** (commit `9e7a744`). Verified on disk 2026-07-02:

- `Graphwright.sln` + 5 projects exist; all 4 `src/` projects contain **zero `.cs` files**.
- `Directory.Build.props`: net8.0, `Nullable=enable`, `TreatWarningsAsErrors=true`,
  `EnableNETAnalyzers` (Recommended), `EnforceCodeStyleInBuild`, central package management.
- `Directory.Packages.props` pins: Roslyn Workspaces 4.14.0, MSBuild.Locator 1.11.2,
  MediatR 12.5.0, MS.Extensions.Hosting 8.0.1, Test.Sdk 17.14.1, xunit 2.9.3, runner 2.8.2.
- Project references wired per the layer map; `tests/Graphwright.Tests` references all 4.
- `project-config.json`: `commands.build`, `paths.src`, `paths.tests` already filled;
  `commands.{test,lint,coverage,run}` and `paths.docs` still placeholders.
- `.mcp.json` registers only `atlassian`; no gitnexus entry.

**This plan therefore does NOT create the skeleton.** Scope = foundation code inside it:
domain error contract, envelope + boundary mapping, self-asserting 5-tool registry,
schema-described stubs, stdio + SSE transports, tests, and config fill-in.

---

## Open-question resolutions (proposed — user confirms at Gate 2)

### OQ-3 — MCP SDK choice (decides everything downstream)

**Decision: official C# SDK — NuGet `ModelContextProtocol` (repo `modelcontextprotocol/csharp-sdk`)
plus `ModelContextProtocol.AspNetCore` for the HTTP/SSE transport.**

- Supports net8.0. stdio via `AddMcpServer().WithStdioServerTransport()` on a generic host;
  HTTP/SSE via `AddMcpServer().WithHttpTransport()` + `app.MapMcp()` on a `WebApplication`
  (AspNetCore companion package). Both transports serve the same registered server options,
  which is exactly the parity guarantee Scenario 4 needs.
- Tool registration supports both attributes (`[McpServerTool(Name = "...")]`) and
  **programmatic/low-level handlers** (`WithListToolsHandler` / `WithCallToolHandler`).
  **We use the low-level handlers**, because:
  1. Our 5 names carry double underscores and the `mcp__gitnexus__` prefix — we want them as
     verbatim string constants, never derived from method names.
  2. A single `CallTool` handler is the one choke point where the exception→envelope mapping
     and "no exception escapes the boundary" rule are enforced (Scenario 3).
  3. The `ListTools` handler is fed from our own `ToolRegistry`, giving the startup
     self-assertion an exact, testable source of truth (Scenario 1) — no assembly scanning
     that could silently advertise a 6th tool.
- **The SDK is prerelease-only.** Pin exactly: `ModelContextProtocol 0.3.0-preview.4` and
  `ModelContextProtocol.AspNetCore 0.3.0-preview.4` in `Directory.Packages.props`.
  If NuGet restore cannot resolve that exact version at implementation time, pin the nearest
  available `0.x` preview instead and record the substitution in `03-decisions.md` — never
  use a floating version.
- Alternatives rejected:
  - *Attribute-based registration*: names work, but dispatch is scattered per-method and the
    "exactly 5, nothing else" assertion has no natural hook.
  - *Hand-rolled JSON-RPC*: full control but reimplements handshake/framing/lifecycle;
    high maintenance for zero contract benefit.

### OQ-2 — Transport scope

**stdio is first-class** (default mode, full in-memory handshake integration test — it is how
the 3 Specwright agents and the availability probe connect). **SSE is registered and reachable
but minimal**: the `--transport sse` branch wires `WithHttpTransport()` + `MapMcp()` from the
*same* shared registration module, and one parity test (TestServer + SSE client) proves the
identical 5-tool surface and envelope. Deeper SSE hardening (auth, session resumption, load)
is deferred to a later GW-1 story if needed.

### OQ-4 — Envelope contract home

**Adopt the spec recommendation.** `GraphwrightErrorCode` (closed 6-value enum) and the
`GraphwrightException` hierarchy live in **Domain** (Application and Infrastructure can throw
them without reaching outward — §1.1 safe). The wire DTOs (`ToolSuccessEnvelope`,
`ToolErrorEnvelope`) and the `ExceptionEnvelopeMapper` live in **McpServer** — serialization is
a presentation concern; inner layers never see the wire shape. No stronger placement found:
putting the DTO in Application would leak `ok/error` wire vocabulary inward for no consumer.

### OQ-1 / OQ-5 — project-config commands and paths

| Key | Value |
|---|---|
| `commands.build` | `dotnet build Graphwright.sln -c Release` (already set — keep) |
| `commands.test` | `dotnet test Graphwright.sln` |
| `commands.lint` | `dotnet format Graphwright.sln --verify-no-changes` |
| `commands.coverage` | `dotnet test Graphwright.sln --collect:"XPlat Code Coverage"` (needs `coverlet.collector` pin — included in T01) |
| `commands.run` | `dotnet run --project src/Graphwright.McpServer` |
| `paths.src` / `paths.tests` | `src` / `tests` (already set — keep) |
| `paths.docs` | `docs` (folder created when first doc lands; harmless if empty now) |

Note: `commands.test` deliberately does NOT use `--no-build` — the workflow gates run test in
isolation and a stale-binary false-green is worse than a rebuild.

---

## Decision D2 — McpServer → Infrastructure reference (Scenario 5 tension)

`src/Graphwright.McpServer/Graphwright.McpServer.csproj:5` already references Infrastructure.
The spec says "McpServer references Application (composition via DI), not Infrastructure
*types* directly".

**Decision: keep the csproj reference — McpServer is the composition root — and constrain it.**

- The **only** Infrastructure symbol McpServer may touch is
  `InfrastructureModule.Instance.RegisterServices(services)`, called once in `Program.cs`.
- `InfrastructureModule` is an instance-based registrar exposed as a `static readonly` instance
  (honors the "no static classes with static methods" rule — DI *extension methods* would
  force a static class, so we do not use them for our own registration seams).
- Rationale: a composition root must reference implementations to register them; that is
  standard Clean Architecture and does not violate §1.1 (dependency direction is still
  inward-only for Domain/Application/Infrastructure). The alternative — a fifth "Bootstrap"
  project — buys nothing at this size and was rejected.
- The constraint is documented in `Program.cs` (comment) and in this plan; an automated
  architecture test is deferred (no cheap reflection-based check that survives the SDK's
  generated code; revisit when Infrastructure gains real types).

## Decision D4 — the "not implemented" stub failure

The error `code` set is closed; "NOT_IMPLEMENTED" is not in it. Stubs throw
`ToolNotImplementedException : GraphwrightException` which maps to **`INTERNAL`,
`retryable: false`**, with a message naming the tool and stating it is not yet implemented.
This keeps the closed set closed, is honest to callers, and gives the tests their
"representative not-yet-implemented case".

Naming note: the FILE_NOT_FOUND exception is `SourceFileNotFoundException` — avoids colliding
with `System.IO.FileNotFoundException`.

---

## Phased overview

| Phase | Tasks | Delivers |
|---|---|---|
| **Foundation** | T01–T05 | Package pins; Domain error enum + exception hierarchy; envelope DTOs; exception→envelope mapper |
| **Behavior** | T06–T10 | Tool abstraction + frozen name constants; 5 stub tools; dispatch boundary; self-asserting registry; Infrastructure registrar seam |
| **Wiring** | T11–T17 | McpServer csproj (exe + ASP.NET Core + SDK packages); shared DI module; SDK handler adapter; Program.cs stdio (fail-fast) + SSE branch; stdio integration test; SSE parity test |
| **Polish** | T18–T19 | project-config.json commands/paths; `.mcp.json` gitnexus entry |

Tests are interleaved: every behavior task carries its own unit test; T16/T17 are the
transport-level integration tests.

## Sequencing rationale

1. **T01 (package pins) first** — central package management means nothing compiles against
   the SDK until versions are pinned; it also unblocks T10 (DI abstractions) and T17
   (Mvc.Testing) with zero later edits to `Directory.Packages.props` (single-writer file).
2. **Domain before McpServer contracts** (T02–T03 before T05) — the mapper consumes the
   exception hierarchy.
3. **Abstraction before stubs before dispatcher before registry-in-DI** — each layer of the
   tool pipeline is unit-testable with fakes before the SDK enters the picture.
4. **Critical path**: T01 → T03 → T05 → T08 → T12 → T13 → T14 → T16. The SDK adapter (T13) is
   the riskiest task (prerelease API surface); everything before it is SDK-free by design, so
   an SDK API mismatch costs only T13–T17 rework, not the foundation.
5. **Config last** (T18–T19) — `commands.run` and the `.mcp.json` entry only mean something
   once `Program.cs` exists; filling them earlier would advertise a server that cannot start.

## Risks

| Risk | Mitigation |
|---|---|
| **Prerelease SDK API drift** (0.x preview; handler/builder signatures may differ from planning-time knowledge) | Exact version pin; SDK confined to T13–T17; T13 acceptance is "compiles + ListTools returns registry contents", so any drift surfaces immediately and locally. If `0.3.0-preview.4` is unavailable, pin nearest preview and log in `03-decisions.md`. |
| **Contract drift from CLAUDE.md** (`mcp-contract.md` still missing; tool names/codes copied from prose) | Names and codes exist exactly once each in code (`GitnexusToolNames`, `GraphwrightErrorCode`); registry self-assertion test (T09) and stdio handshake test (T16) both compare against the frozen literals. |
| **`TreatWarningsAsErrors` + Recommended analyzers** may reject SDK-idiomatic code (e.g. CA rules on async/naming) | Fix code, never suppress globally; a targeted `#pragma`/attribute suppression requires an inline justification comment. |
| **Scenario 4 SSE test flakiness** (SSE over TestServer in a preview SDK) | Parity test kept minimal (handshake + list tools + one call); if the preview SDK cannot run under TestServer, fall back to structural parity (assert both transport branches consume the identical `McpServerModule` registration) and log the downgrade in `03-decisions.md` — stdio coverage is unaffected. |
| **Schemas are scaffold-minimal** (`mcp-contract.md` does not exist to copy from) | Stub input schemas are the minimal per-tool argument shapes from CLAUDE.md's tool table; the per-tool implementation stories own schema finalization. Flagged for the GW-1 epic: author `mcp-contract.md`. |
| **Seam interfaces (`ILanguageProvider`, `IIndexer`, `IGraphStore`)** | Deliberately **excluded** — spec says "may" declare; deferring to the first real tool story avoids speculative interfaces that would be reshaped anyway. Application ships empty in this story. |

## Constitution check (plan level)

- **§1.1 dependency direction / inversion** — respected; D2 documents the composition-root
  exception explicitly rather than silently keeping the reference.
- **§6 catch-and-swallow** — dispatcher (T08) catches at the boundary, logs with tool name and
  argument values, and *transforms* to the envelope; never swallows.
- **§6 static singletons holding state** — registrar/mapper instances are `static readonly`
  and stateless; `ToolRegistry` holds the immutable frozen set (configuration-like).
- **§6 TODO/HACK** — stubs express "later" via `ToolNotImplementedException`, not comments.
- **§5.1** — this plan derives from the approved spec; no scope added beyond it except the
  stale-impact correction (scope *removed*: skeleton creation).
