---
key: GW-4
url: https://trminhtrong.atlassian.net/browse/GW-4
type: Story
status: To Do
priority: Medium
parent_epic: GW-1
reporter: Trong Tran
assignee: Trong Tran
created: 2026-06-21T16:51:39+07:00
updated: 2026-06-21T17:04:25+07:00
components: none
labels: none
linked_issues: none
fetched: 2026-06-26
---

# GW-4 — MCP server scaffold: transport, tool registry, error envelope

## Summary

MCP server scaffold — transport, tool registry, error envelope.

## Description

Build the MCP server foundation: stdio/SSE transport, tool registry asserting exactly 5
`mcp__gitnexus__*` tools on start, and the shared response envelope.

## Acceptance criteria

- Registry asserts exactly 5 tools with correct names on startup.
- Success responses carry `"ok": true`.
- Failures return structured error envelope (no throw):
  `code` one of `WORKSPACE_NOT_LOADED`, `SYMBOL_NOT_FOUND`, `FILE_NOT_FOUND`,
  `AMBIGUOUS_SYMBOL`, `INVALID_ARGUMENT`, `INTERNAL`, with `message` + `retryable`.

## Relationships

- **Parent epic**: GW-1 — "Month 1: Drop-in MVP (GitNexus Replacement)".
- **Related issues**: none.
- **Linked Confluence pages**: none.

## Comments

None.

---

_Snapshot of remote ticket state as of 2026-06-26. Re-fetch overwrites this file._
