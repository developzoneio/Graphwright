# FEAT-GW-5 — Retro log

T01: done - ILanguageProvider + DTOs added under Application, zero Roslyn refs, DeclaredSymbolKindTests green (2/2), full suite 89/89 green. Note: a stray dotnet run MCP server process held file locks; implementer stopped it to unblock the build.
T03: done - RoslynWorkspaceSnapshot (NotLoaded()/Loaded() factories) + RoslynWorkspaceTestFixtures (AdhocWorkspace, TRUSTED_PLATFORM_ASSEMBLIES refs) + substrate smoke test. 3/3 target, 92/92 full suite green.
T02: done - SymbolKindWireMapper (ToWireLiteral/TryFromWireLiteral, case-sensitive lowercase-exact), mirrors ExceptionEnvelopeMapper. 26/26 target tests green.
T04: done - RoslynLanguageProvider scope resolution (WorkspaceNotLoaded first, SourceFileNotFound on missing path). 5/5 target, 123/123 full suite green (includes concurrent T08 work).
T08: done - ListSymbolsTool rewritten: real optional-only schema, full validation before provider call (poison-provider proven), success mapping via SymbolKindWireMapper. 25/25 target green. Expected breakage in StubToolTests/ToolDispatcherTests/ToolRegistryTests (constructor signature change) deferred to T10-T13.
T13: done - StubToolTests drops ListSymbolsTool row, now covers 4 remaining stub tools. 20/20 green.
T12: done - ToolRegistryTests uses new NeverInvokedLanguageProvider nested fake. 7/7 green.
T09: done - InfrastructureModule registers RoslynWorkspaceSnapshot.NotLoaded() + ILanguageProvider->RoslynLanguageProvider. 4/4 green.
T11: done - ToolDispatcherTests swaps ListSymbolsTool for GetFileTool ("file"->"path"). 5/5 green.
T10: done - McpServerModuleTests composition helper registers InfrastructureModule before McpServerModule (mirrors Program.cs). 6/6 green.
T05: done - RoslynLanguageProvider symbol extraction core (MemberDeclarationSyntax walk, field/event carve-out, classification table). 7/7 target green. Corrected two plan assumptions with actual runtime behavior: D1 CSharpErrorMessageFormat includes full namespace qualification; OQ-6 namespaces actually report Accessibility.Public in Roslyn 4.14.0 (handled via explicit INamespaceSymbol guard forcing "not_applicable" per spec intent). Findings recorded in 03-decisions.md.
GAP (main thread, not in original 02-tasks.md): tests/Graphwright.Tests/McpServer/Transport/McpHandlerAdapterTests.cs was missed by 03-decisions.md's test coverage scan. Its BuildConfiguredProvider() only registered McpServerModule (not InfrastructureModule), and its "stub tool" test used ListSymbolsTool/LIST_SYMBOLS with a "file" arg exactly like ToolDispatcherTests did pre-T11. Fixed directly (mirrors T09/T10 registration-order fix + T11 tool-swap fix): added InfrastructureModule.Instance.RegisterServices before McpServerModule.Instance.RegisterServices, and swapped the stub-tool subject from LIST_SYMBOLS/"file" to GET_FILE/"path". Full suite 146/146 green after fix.
T06: done - directory/whole-workspace scope resolution + bin/obj/node_modules/.g.cs exclusion. 10/10 target, 149/149 full suite green.
T07: done - name_filter (case-insensitive substring), kinds allow-list, ordering (File ordinal, then Line), cap/truncation pipeline stage. 16/16 target, 155/155 full suite green. Critical path T01->T04->T05->T06->T07 complete.
T15: done - mcp-contract.md created at repo root, list_symbols fully documented (verified against InputSchema/payload/error codes), other 4 tools marked TBD.
T14: done - ListSymbolsCompositionTests: full-container Scenario-10 dispatch (WORKSPACE_NOT_LOADED/retryable), RoslynWorkspaceSnapshot singleton identity, Scenario-12 structural proxy (no snapshot mutation across calls). 3/3 target, 158/158 full suite green.

All 15 tasks complete. Phase 4 done.

Phase 5a (integration): dotnet test 158/158 green. dotnet format --verify-no-changes initially failed (ENDOFLINE/CHARSET on newly-created files, IDE1006 naming violation on 4 private const string fixture fields in RoslynLanguageProviderTests.cs using PascalCase instead of ALL_UPPER_CASE). Fixed: dotnet format auto-fixed line-endings/charset; renamed OrderServiceSource/VariousDeclarationsSource/OrderNamesSource/MixedKindsSource to ORDER_SERVICE_SOURCE/VARIOUS_DECLARATIONS_SOURCE/ORDER_NAMES_SOURCE/MIXED_KINDS_SOURCE. Re-verified: lint clean, 158/158 tests green.

Phase 5b (batch review, sd-reviewer holistic): 0 BLOCK, 0 WARN, 3 SUGGEST, PASS on all constitution/CLAUDE.md checks (layer boundaries, forbidden patterns, negation style, Async/ct convention, output contract fidelity, ordering/cap pipeline, error precedence).

SUGGEST (logged as follow-ups, not addressed this story):
1. ToAccessibilityLiteral's ProtectedOrInternal->"protected_internal" and ProtectedAndInternal->"private_protected" branches (RoslynLanguageProvider.cs) have zero test coverage — no protected-internal/private-protected fixture exists. Follow-up: add fixture coverage for these two OQ-6 table branches.
2. Single-file `path` resolution (exact-match branch in ResolveScopedDocumentsOrThrow) bypasses the bin/obj/node_modules/.g.cs exclusion filter that directory/whole-workspace scoping enforces — a caller pointing `path` directly at an excluded file still gets a result. No scenario explicitly tests this interaction; flagged as an ambiguity worth a decision (ADR or follow-up story), not a defined violation.
3. Carried-forward cosmetic duplicate: `ManagePackageVersionsCentrally` declared in both Directory.Build.props and Directory.Packages.props (pre-existing since GW-26, still unresolved after GW-4 and GW-5).

Phase 5b follow-up (user-requested, addressed before close-out): Finding 2 (single-file path resolution bypassed the exclusion filter) fixed directly by main thread. RoslynLanguageProvider.cs's single-file exact-match branch now applies IsIncludedByDefault; an excluded file (bin/obj/node_modules/*.g.cs without include_generated) resolved by exact path now throws SourceFileNotFoundException instead of surfacing results, matching directory/whole-workspace scoping behavior. Added ListSymbolsAsyncThrowsSourceFileNotFoundWhenPathTargetsAnExcludedFileDirectly to RoslynLanguageProviderTests.cs (covers bin/ direct-path, *.g.cs direct-path default-excluded, *.g.cs direct-path with include_generated=true resolving, bin/ direct-path still excluded even with include_generated=true). Full suite 159/159 green, lint clean.

Findings 1 (accessibility test coverage) and 3 (MSBuild duplicate) remain logged as deferred follow-ups, not addressed this story per user's scoped "address finding 2" instruction.

Phase 5b follow-up #2 (user-requested): Finding 3 (duplicate ManagePackageVersionsCentrally) fixed. Removed the redundant declaration from Directory.Build.props, kept it in its canonical home Directory.Packages.props. Build/159 tests/lint all clean. This was a cosmetic pre-existing carry-forward from GW-26, unrelated to GW-5's own file scope, fixed as a direct one-line edit per explicit user request.

Finding 1 (accessibility test coverage for protected_internal/private_protected) remains logged as a deferred follow-up.

## Close-out summary

**Tasks completed**: 15/15 (T01-T15), all checked off in 02-tasks.md.

**Surprises encountered**:
- Two plan assumptions (01-plan.md) were wrong once verified against actual Roslyn 4.14.0 runtime behavior, both caught and corrected during T05: (1) D1's SymbolDisplayFormat.CSharpErrorMessageFormat worked example omitted namespace qualification that the real output includes; (2) OQ-6's premise that INamespaceSymbol.DeclaredAccessibility is always NotApplicable was wrong (Roslyn actually reports Public) — the *mandated output* ("not_applicable") was still honored via an explicit guard, so no spec/plan intent changed, only an internal assumption about *why*.
- A test file outside the original impact analysis and 01-plan.md's Risks list (McpHandlerAdapterTests.cs) broke for the same two root causes already anticipated for other files (missing InfrastructureModule registration, stale ListSymbolsTool-as-stub test subject). Fixed directly by the main thread, mirroring the T10/T11 fixes exactly.
- dotnet format --verify-no-changes caught line-ending/charset issues on new files (auto-fixed) and one naming-convention violation (4 test fixture const fields using PascalCase instead of the repo's ALL_UPPER_CASE convention for const fields) — fixed by rename.

**Deferred follow-ups** (no spec ID reserved; small enough to pick up ad hoc or fold into a future GW-1 story):
- Add test fixture coverage for RoslynLanguageProvider's Accessibility.ProtectedOrInternal ("protected_internal") and Accessibility.ProtectedAndInternal ("private_protected") branches — currently implemented per the OQ-6 fixed table but untested.

**Constitution exceptions taken**: none.

**Scope framing reminder** (from 01-plan.md, still true at close-out): production DI wires RoslynWorkspaceSnapshot.NotLoaded() by design. This story does NOT close the Month 1 "availability probe" DoD item — that requires a future indexer story to supply a warm, loaded workspace. mcp__gitnexus__list_symbols through the real server today still returns WORKSPACE_NOT_LOADED end-to-end, now through the real seam instead of a stub throw.

**Final state**: 159/159 tests green, dotnet format --verify-no-changes clean, batch review 0 BLOCK / 0 WARN / 3 SUGGEST (2 addressed before close-out, 1 deferred).
