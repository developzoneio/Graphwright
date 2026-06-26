# Graphwright — CLAUDE.md

> Roslyn-native code graph engine for .NET, exposed as an MCP server.
> Drop-in replacement for GitNexus in the Specwright workflow.

---

## Project identity

- **Repo**: `developzoneio/graphwright`
- **License**: MIT
- **Current phase**: Month 1 — Drop-in MVP
- **Test project**: SportsBook (`smp-jt-services`)

---

## Architecture

```
MCP Server Layer (stdio/SSE)
   ↓
Query Engine (graph traversal + semantic)
   ↓
Graph Store (SQLite + sqlite-vec)
   ↓
ILanguageProvider  [DIP boundary — Application layer interface]
   ├── DotNetProvider (Roslyn)       ← Month 1 focus
   └── TreeSitterProvider            ← Month 2+
   ↓
Indexer + FileWatcher + Git integration
```

### Layer map

| Layer | Project | Allowed dependencies |
|---|---|---|
| `Domain` | `Graphwright.Domain` | None |
| `Application` | `Graphwright.Application` | Domain only |
| `Infrastructure` | `Graphwright.Infrastructure` | Application + Domain |
| `McpServer` | `Graphwright.McpServer` | Application (via DI) |

**Dependency rule is non-negotiable**: inner layers never reference outer layers.
`ILanguageProvider`, `IIndexer`, `IGraphStore` are defined in Application; implemented in Infrastructure.

---

## MCP tool surface (Month 1 contract — frozen)

The server exposes **exactly 5 tools** with the `mcp__gitnexus__*` prefix (zero-change Specwright compatibility).

| Tool | Roslyn primitive | Consumers |
|---|---|---|
| `mcp__gitnexus__list_symbols` | `SyntaxTree` + `SemanticModel` | code-explorer, debugger |
| `mcp__gitnexus__get_file` | File + `SyntaxTree` map | code-explorer, debugger |
| `mcp__gitnexus__find_references` | `SymbolFinder.FindReferencesAsync` | all 3 agents |
| `mcp__gitnexus__get_call_graph` | `SymbolFinder.FindCallersAsync` | code-explorer, debugger |
| `mcp__gitnexus__search` | `SymbolFinder.FindDeclarationsAsync` | all 3 agents |

Do not add, rename, or remove tools in Month 1. Schema changes require updating `mcp-contract.md` first.

### Cross-cutting rules (enforced on every tool)

1. **`file:line` must be exact** — always use `Location.GetLineSpan()`. Lines are **1-based**. Never approximate.
2. **Relative paths from project root** — forward slashes, never absolute, never backslash.
3. **Exclude `bin/`, `obj/`, `*.g.cs`, `node_modules/`** — filter server-side, never surface these.
4. **Snippets**: 1–5 lines maximum. Never truncate mid-expression.
5. **Result cap**: 50 per call. Signal overflow with `truncated: true` + `total_found`.
6. **Ordering**: `file` ascending → `line` ascending. Deterministic always.
7. **No invented data**: zero matches → empty `results[]` + `total_found: 0`.

### Error envelope (all tools must use this shape)

```json
{
  "ok": false,
  "error": {
    "code": "WORKSPACE_NOT_LOADED",
    "message": "...",
    "retryable": true
  }
}
```

Valid `code` values: `WORKSPACE_NOT_LOADED` | `SYMBOL_NOT_FOUND` | `FILE_NOT_FOUND` | `AMBIGUOUS_SYMBOL` | `INVALID_ARGUMENT` | `INTERNAL`

---

## Availability probe

`list_symbols` on a single small file is the **availability probe** used by `sd-code-explorer` before trusting the server. It must:
- Return before agent fallback timeout on a SportsBook-sized repo.
- Be served from a warm index (workspace must be ready before advertising readiness).
- Be the most robust tool — ship first, break last.

---

## C# coding conventions

- **Negation**: always `== false`, never `!expr`.
- **Boolean checks**: always `== true` on `TryParse`, `TryGetValue`, etc.
- **Exceptions**: custom domain exceptions (`SymbolNotFoundException`, `WorkspaceNotLoadedException`). No result pattern, no generic `Exception`.
- **Async**: suffix all async methods with `Async`. `CancellationToken ct` as last parameter, always.
- **Nullability**: `#nullable enable` everywhere. No `!` null-forgiving operator without a comment explaining why.

---

## What Graphwright is NOT responsible for

- **Raw text search** — that's grep's job. `mcp__gitnexus__search` resolves symbol *declarations* only.
- **Multi-language** — Month 1 is .NET only. `TreeSitterProvider` is a stub/interface.
- **GraphRAG / natural-language queries** — Month 3.
- **Neo4j or any external graph DB** — SQLite only, embeddability is required.

---

## Month 1 Definition of Done

- [ ] 5 tools exposed, `mcp__gitnexus__*` names, schemas match `mcp-contract.md` exactly.
- [ ] `find_references` + `get_call_graph` backed by `SymbolFinder`, bin/obj/generated excluded.
- [ ] Every output carries a Roslyn `Location` `file:line`.
- [ ] First `list_symbols` probe on SportsBook is fast enough to avoid agent fallback.
- [ ] All 3 agents (`sd-code-explorer`, `sd-debugger`, `sd-reviewer`) verified on `smp-jt-services`.

---

## Key files

| Path | Purpose |
|---|---|
| `mcp-contract.md` | Frozen tool schemas — source of truth for Month 1 |
| `Graphwright-Project-Brief.md` | Full project context and roadmap |
| `project-config.json` | Set `"gitnexus": { "enabled": true }` to activate |
| `.specs/` | Specwright spec files (dogfooding) |

---

## Open decisions (do not resolve without an ADR)

- **Path casing on Windows**: normalize `file` values for stable golden-file assertions across OSes.
- **Ambiguous symbol UX**: `AMBIGUOUS_SYMBOL` error vs. returning all overloads tagged — see `mcp-contract.md §6`.
- **Config key**: keep `gitnexus` in `project-config.json` for Month 1; revisit `codegraph` alias as Month 2 ADR.
