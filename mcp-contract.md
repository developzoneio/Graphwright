# Graphwright MCP Contract — Frozen tool schemas

> Source of truth per CLAUDE.md Key files; schema changes require updating this file first.

---

## `mcp__gitnexus__list_symbols`

Enumerates symbols declared in source, scoped by an optional `path` (file, directory, or the
whole workspace when omitted), filtered by an optional case-insensitive `name_filter` and/or
`kinds`, with generated code excluded by default. Backed by Roslyn `SyntaxTree` + `SemanticModel`
(CLAUDE.md "MCP tool surface"). This tool is also the CLAUDE.md "Availability probe" used by
`sd-code-explorer` before trusting the server.

### Input

| Property | Type | Required | Description |
|---|---|---|---|
| `path` | `string` | no | Relative, forward-slash path to a file or directory; omitted means whole workspace. |
| `name_filter` | `string` | no | Case-insensitive substring or exact-match filter on the declared symbol name. |
| `kinds` | `array<string>` | no | Symbol-kind literals to include; omitted means all kinds. Closed vocabulary — see below. |
| `include_generated` | `boolean` | no | Whether generated (`*.g.cs`) source is included. Defaults to `false`. |
| `max_results` | `integer` | no | Maximum results to return. Defaults to `50`; values above `50` are clamped to `50`. |

`kinds` vocabulary (closed set, unrecognized literal is rejected):

```
namespace, class, interface, struct, enum, method, property, field, event, constructor
```

### Output

```json
{
  "ok": true,
  "result": {
    "results": [
      {
        "name": "...",
        "kind": "...",
        "file": "...",
        "line": 0,
        "signature": "...",
        "container": "...",
        "accessibility": "..."
      }
    ],
    "truncated": false,
    "total_found": 0
  }
}
```

Per-result fields:

| Field | Type | Description |
|---|---|---|
| `name` | `string` | Declared symbol name. |
| `kind` | `string` | One of the `kinds` vocabulary literals. |
| `file` | `string` | Relative, forward-slash path from the project root. |
| `line` | `integer` | 1-based declaration line from `Location.GetLineSpan()`. |
| `signature` | `string` | Symbol signature. |
| `container` | `string` | Containing symbol's display name. |
| `accessibility` | `string` | Declared accessibility. |

`results` is ordered `file` ascending, then `line` ascending (CLAUDE.md cross-cutting rule 6).
`truncated` is `true` when `total_found` exceeds the applied `max_results` cap. Zero matches is a
valid result: empty `results[]`, `total_found: 0`, `truncated: false` (CLAUDE.md cross-cutting
rule 7).

### Errors

| Code | Retryable | Cause |
|---|---|---|
| `WORKSPACE_NOT_LOADED` | `true` | The workspace index has not finished loading. |
| `INVALID_ARGUMENT` | `false` | A `path` outside the workspace root, an unrecognized `kinds` literal, or a wrong-typed argument. |
| `FILE_NOT_FOUND` | `false` | A well-formed, in-root `path` that does not resolve to anything in the workspace. |

See CLAUDE.md "Error envelope" for the shared `{ ok, error: { code, message, retryable } }` shape.

---

## `mcp__gitnexus__get_file`

TBD - lands with its own story.

---

## `mcp__gitnexus__find_references`

TBD - lands with its own story.

---

## `mcp__gitnexus__get_call_graph`

TBD - lands with its own story.

---

## `mcp__gitnexus__search`

TBD - lands with its own story.
