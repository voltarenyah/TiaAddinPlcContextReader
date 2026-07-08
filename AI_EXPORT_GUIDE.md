# AI Guide For Using PlcSourceExporter Outputs

This guide is for an AI agent that needs to read a TIA Portal V20 export produced by `PlcSourceExporter`.

The goal is not to regenerate the PLC program from the JSONL files. The goal is to answer practical engineering questions with good token efficiency while staying faithful to the exported source.

## Purpose

Use the generated files as a layered reasoning index:

1. use `model\plc-graph.sqlite` first for semantic graph queries
2. use compact JSON and JSONL files for lightweight inspection and cross-checking
3. use raw XML only when a question needs exact source confirmation
4. avoid guessing logic that is not directly supported by the export

The semantic model under `model\` is the source of truth for future AI analysis features.

The XML files under `Blocks`, `DB`, `UDT`, and `Tags` remain the source of truth for TIA import/export reconstruction and writeback workflows.

The JSON and JSONL files are read-only reasoning artifacts.

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
metadata.json
tags.json
udt.json
callgraph.json
calltree.md
networks.jsonl
references.jsonl
block-profiles.jsonl
optimization-hints.jsonl
PlcSourceExporter.log
```

## First Principles

When answering PLC questions from this export:

- prefer trustworthy coarse structure over speculative decompilation
- treat `SW.Blocks.CompileUnit` as the network boundary
- treat block XML as authoritative when a compact artifact and XML disagree
- do not assume read or write direction unless the export explicitly supports it
- do not treat optimization hints as defects automatically; they are leads, not proof

## Recommended Reading Order

For most questions, read files in this order:

1. `model\plc-graph.sqlite`
2. `block-profiles.jsonl`
3. `optimization-hints.jsonl`
4. `callgraph.json`
5. `networks.jsonl`
6. `references.jsonl`
7. `tags.json`
8. `udt.json`
9. raw XML in `Blocks\`, `DB\`, `UDT\`, or `Tags\`

This order keeps token use low while preserving a path back to exact source.

## What Each File Is For

### `model\plc-graph.sqlite`

Use this as the semantic PLC graph model.

Good for:

- querying blocks, networks, variables, DBs, UDTs, tags, types, and IO addresses through one model
- answering caller/callee, read/write, type, instance, and connectivity questions
- building future AI analysis and code-generation features without depending on XML shape

The database uses generic graph tables:

- `graph_nodes`
- `graph_node_properties`
- `graph_edges`
- `graph_edge_properties`

### `model\schema.sql`

Use this to inspect the SQLite graph schema or migrate the model toward another graph database such as Neo4j.

### `metadata.json`

Use this to understand what was exported and where each exported file came from.

Good for:

- listing all exported components
- mapping block names to source files
- identifying block category: `OB`, `FC`, `FB`, `DB`, `UDT`, `Tags`
- checking whether an object was exported or skipped

Do not use it as a logic summary. It is only export inventory and metadata.

### `tags.json`

Use this to answer tag-table questions without loading tag XML.

Good for:

- listing PLC tag tables
- finding tag names, addresses, and types
- tracing global tags used by multiple blocks

Use this before opening `Tags\*.xml`.

### `udt.json`

Use this to understand user-defined type structure.

Good for:

- expanding nested members
- understanding DB member paths
- explaining field names and types used in references

Use this before opening `UDT\*.xml`.

### `callgraph.json`

Use this for block-level navigation.

Good for:

- finding who calls a block
- finding which blocks a caller depends on
- checking FB instance DB usage
- identifying likely ownership boundaries

This file is the fastest way to answer:

- "where is this FB called?"
- "what are the main execution dependencies around this function?"

Do not use it to explain detailed Boolean logic.

### `calltree.md`

This is a human-readable companion to the JSON call graph.

Good for:

- quick visual orientation
- explaining the call tree in prose

For AI reasoning, prefer the JSON file first.

### `networks.jsonl`

Use this as the compact per-network summary index.

Each line is one `SW.Blocks.CompileUnit`.

Good for:

- locating networks with high access density
- locating networks that call a given FC or FB
- locating which networks read or write a signal
- narrowing XML review to a small set of candidate networks

Important fields:

- `block`
- `blockKind`
- `language`
- `sourceFile`
- `networkIndex`
- `title`
- `reads`
- `writes`
- `calls`
- `accessCount`
- `callCount`

This file is usually the best first answer surface for:

- "which network touches X?"
- "where is this block used?"
- "which parts of Main talk to this subsystem?"

### `references.jsonl`

Use this as the detailed network-to-symbol and network-to-block edge list.

Each line is one reference from one network to one target.

Good for:

- tracing read and write edges
- identifying parameter binding direction on calls
- checking whether a symbol is used as input, output, or inout
- finding all networks that touch a DB member or tag

Important fields:

- `from`
- `block`
- `networkIndex`
- `title`
- `to`
- `targetKind`
- `access`
- `scope`
- `parameter`
- `callee`
- `calleeBlockType`
- `instanceDb`
- `sourceFile`

Access meanings in this export:

- `read`: trusted call input binding
- `write`: trusted call output binding
- `inout`: trusted call inout binding
- `call`: block call edge
- `unknown`: reference exists, but direction should not be guessed

When `access` is `unknown`, do not claim the symbol is definitely read or written without checking XML.

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

1. `references.jsonl`
2. `networks.jsonl`
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

1. read `block-profiles.jsonl` for the block
2. inspect `keyReads`, `keyWrites`, `keyCalls`, `statefulElements`, and `instanceDbs`
3. read matching networks from `networks.jsonl`
4. use raw `Blocks\<name>.xml` only if the summary is not enough

### 2. "Who writes this signal?"

Workflow:

1. search `references.jsonl` for `to=<signal>`
2. filter `targetKind="symbol"`
3. look for `access="write"` and `access="inout"`
4. if only `unknown` exists, say direction is not proven by the current artifact
5. use `networks.jsonl` to see the surrounding network summaries

### 3. "Where is this FB instance called?"

Workflow:

1. search `references.jsonl` for `instanceDb=<instance name>`
2. search `callgraph.json` for the FB or instance
3. confirm caller block and network title

### 4. "What logic touches this DB member?"

Workflow:

1. search `references.jsonl` for exact member path
2. group results by block and network
3. use `networks.jsonl` to summarize reads and writes around those networks
4. open raw XML only for final confirmation

### 5. "What should be optimized first?"

Workflow:

1. read `optimization-hints.jsonl`
2. group by `kind`, `block`, and `target`
3. cross-check high-value findings in `block-profiles.jsonl`
4. prioritize:
   - multi-writer signals
   - repeated patterns in state-heavy blocks
   - scan-order-sensitive writes
   - outputs that are never written

## How To Use The Files Together

Use the files as a funnel:

1. `model\plc-graph.sqlite` gives the unified semantic graph
2. `block-profiles.jsonl` gives block intent
3. `optimization-hints.jsonl` gives review targets
4. `callgraph.json` gives inter-block structure
5. `networks.jsonl` gives network-level location
6. `references.jsonl` gives exact compact edges
7. XML gives final proof

This keeps answers efficient and grounded.

## What The Current Export Does Well

The current export is strong at:

- block inventory
- tags and UDT lookup
- call relationships
- network-level summaries
- symbol and block references
- conservative optimization leads

This is enough to answer many real machine-program questions without loading the full XML corpus.

## Current Limits

Do not overclaim beyond the current artifacts.

Known limits:

- no full Boolean decompilation
- no guaranteed read or write classification for all LAD and FBD standalone accesses
- no complete state-machine reconstruction
- no safety-specific semantic layer beyond preserved language markers such as `F_LAD` and `F_FBD`
- no writeback through JSONL artifacts

If a question depends on exact rung structure, wire connectivity details, or nuanced instruction behavior, inspect the raw XML.

## Safe Answering Rules

When answering from the export:

- say "the export shows" rather than asserting hidden intent
- distinguish proven facts from engineering interpretation
- cite block name, network index, and source file when giving important conclusions
- mention when a result is based on `unknown` access classification
- use raw XML when a recommendation would otherwise be risky

## Recommended AI Workflow

For a real engineering question, follow this loop:

1. identify the main block, signal, DB member, or FB instance
2. query the smallest artifact that can answer it
3. narrow to specific blocks and networks
4. state what is directly supported by the export
5. if needed, open the exact XML for final confirmation
6. only then suggest optimization or modification

This workflow preserves token efficiency and keeps suggestions practical.
