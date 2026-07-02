---
key: GW-5
url: https://trminhtrong.atlassian.net/browse/GW-5
type: Story
status: To Do
priority: Medium
parent_epic: GW-1
reporter: Trong Tran
assignee: Trong Tran
created: 2026-06-21T16:51:49+0700
updated: 2026-06-21T17:04:33+0700
components: none
labels: none
linked_issues: none
fetched: 2026-07-02
---

# GW-5 — Tool: list_symbols (SyntaxTree + SemanticModel)

## Summary

Tool: list_symbols (SyntaxTree + SemanticModel).

## Description

Implement `mcp__gitnexus__list_symbols` — enumerate declared symbols, optionally filtered by
name and kind, scoped to file / directory / whole workspace. Backed by `SyntaxTree`
declarations + `SemanticModel`. Also serves as the availability probe (single small file).

**Input:** `path?`, `name_filter?`, `kinds?`, `include_generated?` (default false),
`max_results?` (default 50, cap 50).

**Output per result:** `{name, kind, file, line, signature}` (+ additive `container`,
`accessibility`).

## Acceptance criteria

- Substring/exact case-insensitive `name_filter`.
- Caps at 50, signals `truncated: true` + `total_found`.
- Zero matches → empty `results`, `total_found: 0`.

## Relationships

- **Parent epic**: GW-1 — "Month 1: Drop-in MVP (GitNexus Replacement)". Snapshot at
  `related/GW-1.md`.
- **Related issues**: none (`issuelinks` empty, no subtasks).
- **Linked Confluence pages**: none (`getJiraIssueRemoteIssueLinks` returned empty).

## Comments

None.

---

_Snapshot of remote ticket state as of 2026-07-02 (fetched via Atlassian MCP). Re-fetch overwrites this file._
