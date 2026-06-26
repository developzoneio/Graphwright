# FEAT-GW-26 — Retro

## Close-out

- Tasks completed: 9 (T01–T09)
- Surprises:
  - dotnet SDK 10.0.300 defaults `dotnet new sln` to `.slnx` format; required `--format sln` flag to produce `Graphwright.sln` (classic format). Future CI/scaffold scripts must pass this flag.
  - Package versions bumped to latest-within-major at restore (plan intention confirmed); 5 of 7 pinned versions moved (see T01 log).
  - Build.Locator 1.11.2 restored cleanly with no NU1701/native-extension warnings — R2 risk did not materialize.
- Deferred follow-ups:
  - Add `.gitignore` before first `git add` (no spec ID reserved; simple one-off).
  - Remove duplicate `ManagePackageVersionsCentrally` from `Directory.Build.props:13` (tiny cleanup; can fold into first GW-4 PR).
  - Fill `commands.{test,lint,coverage,run}` in project-config.json when FEAT-GW-4 is planned.
  - FEAT-GW-4/00-spec.md Scenario 5 + OQ-5 overlap with GW-26 scope — refine GW-4 to consume this skeleton when it enters planning.
- Constitution exceptions: none.
- Cost estimate: ~9 sd-implementer + 1 sd-spec-architect + 1 sd-code-explorer + 1 sd-reviewer subagent calls. No BLOCK rework.

## Batch review (Phase 5b)

- 🔴 BLOCK: 0
- 🟠 WARN: 0
- 🟡 SUGGEST (logged, not gated):
  1. No .gitignore — bin/obj artifacts now exist; next `git add` will sweep them. Resolve before first commit.
  2. `ManagePackageVersionsCentrally` declared in both Directory.Build.props:13 and Directory.Packages.props:4 (redundant; harmless).
  3. commands.{test,lint,coverage,run} and paths.docs still placeholder in project-config.json — by design (deferred to consuming stories).
  4. .editorconfig [*] section sets end_of_line=crlf repo-wide — fine for Windows, note for CI on Linux.
- 🟢 PASS: all enumerated checks (§1.1 layer graph, §6 forbidden patterns, package boundary, inherited settings, naming rules, analyzer settings, risks R1-R5, scope)

## Task log

T01: done - Directory.Build.props + Directory.Packages.props created; versions bumped to latest-within-major (Roslyn 4.14.0, Build.Locator 1.11.2, MediatR 12.5.0, Test.Sdk 17.14.1, xunit 2.9.3; Hosting 8.0.1 + xunit.runner 2.8.2 held)
T02: done - Graphwright.Domain.csproj created; zero refs, zero packages; 0 warnings build
T03: done - Graphwright.Application.csproj created; -> Domain + MediatR; 0 warnings build
T04: done - Graphwright.Infrastructure.csproj created; -> Application + Roslyn + Build.Locator; 0 warnings build (transient CS2012 file-lock on first attempt; clean on re-run)
T05: done - Graphwright.McpServer.csproj created (class library); -> Application + Infrastructure + host/DI; 0 warnings build
T06: done - Graphwright.Tests.csproj created; -> all 4 production projects + xUnit; 0 warnings build
T07: done - Graphwright.sln assembled (--format sln required; SDK 10.0.300 defaults to .slnx); dotnet build Graphwright.sln -c Release -> 0 errors, 0 warnings
T08: done - .editorconfig created; 4 naming rules at error severity; extra [*] and [*.json] blocks added (benign)
T09: done - project-config.json updated: paths.src="src", paths.tests="tests", commands.build set
