namespace PlcSourceExporter.Core;

public static class AiExportGuideBuilder
{
    public const string FileName = "AI_EXPORT_GUIDE.md";

    public static string Write(string exportRoot)
    {
        if (string.IsNullOrWhiteSpace(exportRoot))
        {
            throw new ArgumentException("Export root is required.", nameof(exportRoot));
        }

        Directory.CreateDirectory(exportRoot);
        var filePath = Path.Combine(exportRoot, FileName);
        File.WriteAllText(filePath, Content);
        return filePath;
    }

    private const string Content = """
# AI Guide For Using PlcSourceExporter Outputs

This guide is for an AI agent that needs to read a TIA Portal export produced by `PlcSourceExporter`.

The semantic graph under `model\` is the primary reasoning surface. The raw XML remains the import/export reconstruction format and the final proof source when exact rung, wire, or writeback details matter.

## Export Root

The exporter writes to:

```text
<TIA project folder>\UserFiles\export
```

Typical contents:

```text
Blocks\
DB\
UDT\
Tags\
model\
translate\
metadata.json
block-profiles.jsonl
optimization-hints.jsonl
AI_EXPORT_GUIDE.md
PlcSourceExporter.log
```

The `model\` folder contains:

```text
plc-graph.sqlite
schema.sql
AGENT_SQLITE_GUIDE.md
```

The `translate\` folder contains:

```text
program-blocks.yaml
```

## First Principles

When answering PLC questions from this export:

- use `model\plc-graph.sqlite` first for objects, reads, writes, calls, DBs, tags, and relationships
- use `model\AGENT_SQLITE_GUIDE.md` when an agent only has the SQLite model
- use `translate\program-blocks.yaml` only when exact network logic, contact polarity, branch grouping, constants, assignments, comparisons, or call enable logic is needed
- use `block-profiles.jsonl` for compact block-level summaries
- use `optimization-hints.jsonl` only as a review lead list
- use raw XML only as final proof when `translate\program-blocks.yaml` marks a network as `partial` or `untranslated`
- avoid guessing logic that is not directly supported by the export

Folder paths are metadata only. Do not model the project as a folder tree.

## Recommended Reading Order

For most questions, read files in this order:

1. `model\plc-graph.sqlite`
2. `model\AGENT_SQLITE_GUIDE.md`
3. `block-profiles.jsonl`
4. `optimization-hints.jsonl`
5. `translate\program-blocks.yaml` when exact network logic is needed
6. `metadata.json`
7. raw XML in `Blocks\`, `DB\`, `UDT\`, or `Tags\`

This order keeps token use low while preserving a path back to exact source.

## What Each File Is For

### `model\plc-graph.sqlite`

Use this as the semantic PLC graph model.

Good for:

- querying blocks, networks, variables, DBs, UDTs, tags, types, and IO addresses through one model
- answering caller/callee, read/write, type, instance, and connectivity questions
- finding source files and network indices for semantic facts
- building future AI analysis and code-generation features without depending on XML shape

The database uses generic graph tables:

- `graph_nodes`
- `graph_node_properties`
- `graph_edges`
- `graph_edge_properties`

### `model\schema.sql`

Use this to inspect the SQLite graph schema or migrate the model toward another graph database such as Neo4j.

### `model\AGENT_SQLITE_GUIDE.md`

Use this when `plc-graph.sqlite` is the only file available to an agent. It documents the graph mental model, relationship meanings, and SQL queries for calls, reads/writes, tags, IO, DB members, UDT members, and network execution order.

### `metadata.json`

Use this to understand what was exported and where each exported file came from.

Good for:

- listing all exported components
- mapping object names to source files
- identifying object category: `OB`, `FC`, `FB`, `DB`, `UDT`, `Tags`
- checking whether an object was exported or skipped

Do not use it as a logic summary. It is only export inventory and metadata.

### `translate\program-blocks.yaml`

Use this only as the compact network-logic layer.

Good for:

- seeing LAD/FBD logic translated to SCL-like assignments and calls
- checking contact polarity, serial/branch grouping, constants, comparisons, and assignments
- inspecting SCL source text preserved compactly when available
- finding whether a network translation is `exact`, `partial`, or `untranslated`

Do not use it as a graph database. It intentionally does not repeat standalone reads, writes, calls, call bindings, node IDs, edge IDs, or SQLite-equivalent relationship facts. Use `model\plc-graph.sqlite` for those.

If this file marks a network as `partial` or `untranslated`, use the matching raw XML as the final proof source before changing or recommending PLC logic.

### `block-profiles.jsonl`

Use this as the compact block summary layer.

Each line is one block profile.

Good for:

- understanding a block before drilling down
- seeing network count and call density
- identifying key reads, key writes, and key calls
- finding state-heavy blocks through timers, counters, latches, or instance DB usage

This file is the best first answer surface for:

- "what does this block mainly do?"
- "which blocks are stateful?"
- "which block is a likely optimization hotspot?"

### `optimization-hints.jsonl`

Use this as a conservative lead list.

Good for:

- prioritizing review
- finding multi-writer signals
- finding never-read writes
- finding never-written outputs
- spotting repeated call targets
- spotting scan-order-sensitive patterns

Treat each hint as:

- a review target
- not a proven bug

Always confirm important hints against:

1. `model\plc-graph.sqlite`
2. `block-profiles.jsonl`
3. raw XML if needed

### `PlcSourceExporter.log`

Use this only to verify export success and artifact freshness.

Good for:

- checking whether a run completed
- checking output file paths
- checking exported and skipped counts
- diagnosing failed add-in runs

Do not use it for logic reasoning.

## How To Answer Common PLC Questions

### 1. "What does this block do?"

Workflow:

1. query `model\plc-graph.sqlite` for the block's `CALLS`, `READS`, `WRITES`, `CONTAINS`, and type relationships
2. read the matching row in `block-profiles.jsonl`
3. use `translate\program-blocks.yaml` if exact network expressions or contact polarity are needed
4. cite block name, source file, and network index when available
5. use raw `Blocks\<name>.xml` only if the YAML translation is `partial` or `untranslated`

### 2. "Who writes this signal?"

Workflow:

1. query `model\plc-graph.sqlite` for `WRITES` edges to the signal node
2. inspect `sourceFile` and `networkIndex` edge properties
3. cross-check high-value answers in `block-profiles.jsonl`
4. use raw XML for final confirmation when a recommendation would change PLC logic

### 3. "Where is this FB instance called?"

Workflow:

1. query `model\plc-graph.sqlite` for `CALLS` and `INSTANCE_OF` relationships
2. inspect `instanceDb`, `sourceFile`, and `networkIndex` edge properties
3. confirm caller block and network title

### 4. "What logic touches this DB member?"

Workflow:

1. query `model\plc-graph.sqlite` for the DB member node
2. inspect incoming `READS` and `WRITES` edges
3. group results by block and network
4. open raw XML only for final confirmation

### 5. "What should be optimized first?"

Workflow:

1. read `optimization-hints.jsonl`
2. group by `kind`, `block`, and `target`
3. cross-check high-value findings in `model\plc-graph.sqlite` and `block-profiles.jsonl`
4. prioritize multi-writer signals, repeated patterns in state-heavy blocks, scan-order-sensitive writes, and outputs that are never written

## Current Limits

Do not overclaim beyond the current artifacts.

Known limits:

- no guaranteed full Boolean decompilation for every network form
- no guaranteed read or write classification for every LAD and FBD standalone access
- no complete state-machine reconstruction
- no safety-specific semantic layer beyond preserved language markers such as `F_LAD` and `F_FBD`
- no writeback through derived artifacts

If a question depends on exact rung structure, wire connectivity details, or nuanced instruction behavior, inspect `translate\program-blocks.yaml` first and then raw XML when the YAML confidence is not exact.

## Safe Answering Rules

When answering from the export:

- say "the export shows" rather than asserting hidden intent
- distinguish proven facts from engineering interpretation
- cite block name, network index, and source file when giving important conclusions
- state when a graph relationship is absent instead of assuming the PLC never uses an object
- use raw XML when a recommendation would otherwise be risky
""";
}
