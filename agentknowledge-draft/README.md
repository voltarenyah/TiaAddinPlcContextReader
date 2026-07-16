# Agent Knowledge Draft For Siemens PLC Exports

This folder is a draft semantic knowledge package intended to ship inside an export root as:

```text
agentknowledge\
```

Its purpose is to reduce wrong AI guesses when exported XML and translated YAML do not carry enough semantic meaning by themselves.

## Intended Use

An agent should use this folder in addition to:

1. `model\plc-graph.sqlite`
2. `model\AGENT_SQLITE_GUIDE.md`
3. `translate\program-blocks.yaml`
4. compact export artifacts such as `block-profiles.jsonl`

The knowledge files are read-only reasoning guidance. They do not replace source XML, translated YAML, or the semantic graph.

## Required Reasoning Rules

- If source facts and this folder disagree, prefer source facts and report the mismatch.
- If translated YAML is incomplete, use these files to explain semantics, not to invent missing logic.
- Distinguish observed structure from inferred intent.
- Distinguish command, feedback, permissive, mode, and fault signals.
- Never assume a safety function is equivalent to a standard permissive chain unless the source proves it.

## Suggested Read Order

1. `interpretation-rules.md`
2. `execution-model.md`
3. `instruction-catalog.yaml`
4. `data-types-and-memory.yaml`
5. `patterns-and-anti-patterns.md`
6. `project-conventions.yaml`
7. `examples\*.yaml`

## File Summary

- `instruction-catalog.yaml`: canonical instruction semantics and lookup aliases
- `data-types-and-memory.yaml`: Siemens variable classes, DB behavior, and persistence concepts
- `execution-model.md`: scan-cycle and runtime model
- `language-mapping.yaml`: LAD/FBD/SCL and export-shape mapping
- `safety-and-diagnostics.yaml`: guardrails against unsafe simplification
- `interpretation-rules.md`: mandatory analysis rules for the agent
- `patterns-and-anti-patterns.md`: common control patterns and common misreads
- `glossary.yaml`: short definitions for common PLC terms
- `project-conventions.yaml`: project-specific naming and signal conventions
- `examples\`: compact worked examples

## Draft Status

This package is intentionally generic. Before bundling it into production exports, fill in:

- project naming conventions
- wrapper FB semantics
- motion/axis state naming
- active-high vs active-low conventions
- safety ownership boundaries
