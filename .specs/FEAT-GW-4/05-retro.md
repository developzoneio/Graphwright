# FEAT-GW-4 — Retro log

## Transitions

- 2026-07-02: draft -> approved (Gate 1 passed in prior session; carried in index).
- 2026-07-02: approved -> in-progress (Phase 3 started; plan + tasks authored).
- 2026-07-02: Gate 2 passed — plan approved by user ("Yes — approve plan", 19 tasks, 8 S + 11 M).

## Task log

- T01: done - 5 exact pins added to Directory.Packages.props; all versions verified on NuGet; restore green; no substitutions.
- T02: done - closed 6-member enum + 2 tests green; CA1707 forced test method rename (no underscores).
- T03: done - abstract base + 6 sealed exceptions (1:1 bijection over enum, test-enforced); 7/7 tests green, zero suppressions.
- T04: done - ToolSuccessEnvelope<TResult> + ToolErrorEnvelope/ToolError wire DTOs; 4/4 tests green; CA1822/CA1861 fixed idiomatically.
- T05: done - sealed Instance-pattern mapper; enum->ALL_UPPER lives only here; generic message for non-domain exceptions; 8/8 tests green.
- T06: done - IGitnexusTool adapted to Task<ToolSuccessEnvelope<JsonElement>>; frozen name constants PascalCase (CA1707 forbids public ALL_UPPER; wire strings verbatim); 2/2 tests, suite 23/23.
- T07: done - 5 sealed stubs with cached cloned schemas; arg validation -> InvalidToolArgumentException, else ToolNotImplementedException; 25/25 tests, suite 48/48; xUnit1026 fixed by splitting theory data sources.
- T08: done - dispatcher with smallest-scope boundary catch (CA1031 targeted suppression citing Scenario 3); CA1848 forced LoggerMessage source-gen; 5/5 tests, suite 53/53.
- T09: done - registry with deterministic order + full diff assertion (missing/extra/duplicates); ToolContractViolationException derives GraphwrightException(Internal); 7/7 tests, suite 60/60.
- T10: done - registrar seam with null guard; CA1822 targeted suppression citing plan D2; 3/3 tests green.
- T11: done - FrameworkReference + MCP SDK 0.3.0-preview.4 packages resolve and build green (63/63 tests); DEVIATION (authorized): OutputType=Exe deferred to T14 because CS5001 fires without Program.cs.
- T12: done - single registration source for both transports; IEnumerable->IReadOnlyList DI bridge; host owns AddLogging (documented); 6/6 tests, suite 69/69.
- T13: done - WithListToolsHandler/WithCallToolHandler matched plan prediction (verified by reflection, recorded in 03-decisions.md); envelope always travels as tool result content, never MCP protocol error; 5/5 tests, suite 74/74.
- T14: done - stdio host with stderr-only logging (stdout is protocol channel), fail-fast AssertContract before RunAsync, OutputType=Exe absorbed from T11; smoke check: bogus transport exits 1 with stderr reason; 8/8 tests, suite 82/82.
- T15: done - SSE branch via WithHttpTransport + MapMcp (API matched prediction, verified from nuget xml docs); shared RegisterModules/TryAssertToolContractAsync helpers remove branch duplication; SSE smoke boot on :5000 OK; suite 82/82.
- T16: done - in-memory handshake via WithStreamServerTransport + StreamClientTransport over paired Pipes (Console-bound stdio not redirectable in-test, documented); full handshake + 5-name list + INTERNAL/INVALID_ARGUMENT envelopes asserted; 3/3 tests, suite 85/85.
- T17: done - REAL TestServer parity achieved (SseClientTransport accepts injected HttpClient; downgrade fallback not needed); assertions mirror T16 literally; 2/2 tests, suite 87/87.
- T18: done - commands.test/lint/coverage/run + paths.docs filled; coverlet.collector reference added; coverage produces cobertura xml. KNOWN ISSUE for Phase 5a: `dotnet format --verify-no-changes` exits 2 (CHARSET/encoding on ~30 files + IDE1006 naming violations) — to fix at integration.
- T19: done - gitnexus stdio entry added to .mcp.json; real SDK client handshake through the exact entry command listed all 5 frozen tools.
- Phase 5a: full suite 87/87 green; lint failed (ENDOFLINE + IDE1006) -> ENDOFLINE auto-fixed inline; naming conflict routed as T20.
- T20: done - CA1707 disabled in .editorconfig (conflicts with constants_all_upper); tool-name constants ALL_UPPER; test fields _camelCase; build 0/0, tests 87/87, lint exit 0.
- Phase 5a (final): tests 87/87 exit 0; lint exit 0. Scope-creep .gitignore drift (removed .claude/scratchpad/ ignore) detected and reverted by main thread.
- Phase 5b: holistic review verdict 0 BLOCK / 0 WARN / 5 SUGGEST / 10 PASS areas.

## Review follow-ups (SUGGEST, logged 2026-07-02)

- S1: add inline constitution-§6-exception comments at the two `context.Services.GetRequiredService` sites in McpHandlerAdapter (framework-boundary service location, plan-sanctioned).
- S2: no direct automated test of Program.cs exit-code-1 fail-fast glue (assertion logic itself covered by ToolRegistryTests); consider a focused wiring test.
- S3: `!` null-forgiving operators in test code lack explanatory comments (production complies); decide whether the CLAUDE.md rule scopes to production only.
- S4: ParseSingleTextContentAsJson + ConnectedServer duplicated across transport tests — partly intentional (T17 literal parity); revisit if a TestSupport helper emerges.
- S5: cosmetic pin-ordering drift in Directory.Packages.props vs T01 "alphabetical-ish" pattern ref.

## Gate 3 status

- 2026-07-02: Gate 3 presented (tests 87/87, lint clean, 0 BLOCK / 0 WARN / 5 SUGGEST). User response pending — close-out NOT executed (constitution §5.3: silence is not approval). Spec remains in-progress.
- 2026-07-02: Gate 3 APPROVED by user ("yes"). Proceeding to close-out.

## Close-out (2026-07-02)

- **Tasks completed**: 20/20 — T01–T19 (planned) + T20 (Phase 5a lint-gate follow-up).
- **Surprises encountered**:
  - The impact analysis's greenfield premise was stale — FEAT-GW-26 had already delivered the solution skeleton; the plan was corrected up front to build inside it.
  - The prerelease MCP SDK (0.3.0-preview.4) matched every predicted API (WithListToolsHandler/WithCallToolHandler, WithHttpTransport/MapMcp, StreamClientTransport, SseClientTransport-with-injected-HttpClient) — the plan's riskiest assumption held; real TestServer SSE parity was achieved with no downgrade.
  - OutputType=Exe could not land with T11 (CS5001 without Program.cs) — absorbed into T14.
  - CA1707 vs the repo's constants_all_upper .editorconfig rule was a genuine conflict; resolved in T20 by disabling CA1707 (the .editorconfig codifies the user standard).
  - dotnet format flagged LF line endings across all new files (CRLF expected) — auto-fixed inline.
  - One scope-creep drift (.gitignore lost its .claude/scratchpad/ ignore) — detected at Phase 5, reverted.
- **Deferred follow-ups**: review SUGGESTs S1–S5 (see "Review follow-ups" above); no spec IDs reserved — S1/S2 are candidates to fold into the first per-tool implementation story under GW-1.
- **Constitution exceptions taken**: none. Targeted analyzer suppressions (CA1031 boundary catch per spec Scenario 3; CA1822 registrar seams per plan D2) are documented in-code and are convention-compliant, not exceptions.
- **Cost estimate**: ~1.5M subagent tokens across 1 planner + 21 implementer/reviewer invocations (per-agent usage logged in session).
- **Transition**: in-progress -> done (Gate 3 approval, 2026-07-02).
