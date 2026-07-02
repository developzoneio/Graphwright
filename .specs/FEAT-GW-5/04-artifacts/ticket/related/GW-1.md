---
key: GW-1
url: https://trminhtrong.atlassian.net/browse/GW-1
link_type: parent epic
type: Epic
status: To Do
priority: Medium
fetched: 2026-07-02
---

# GW-1 — Month 1: Drop-in MVP (GitNexus Replacement)

## Link type

GW-1 is the parent epic of GW-5.

## Summary

Roslyn-native MCP server exposing exactly 5 `mcp__gitnexus__*` tools as a zero-change
drop-in for GitNexus in the Specwright workflow.

## Description (Definition of Done)

1. MCP server exposes exactly the 5 tools, named `mcp__gitnexus__*`, with matching schemas.
2. `find_references` + `get_call_graph` run on `SymbolFinder`, excluding bin/obj/*.g.cs.
3. Every output carries a precise Roslyn `Location` `file:line`.
4. Indexing a SportsBook-sized repo is fast enough that the first probe doesn't trigger an
   agent fallback.
5. Enabled via `gitnexus.enabled: true`, with the 3 agents (`sd-code-explorer`,
   `sd-debugger`, `sd-reviewer`) verified on the real project.

---

_Related-ticket snapshot (1 hop) as of 2026-07-02. Re-fetch overwrites this file._
