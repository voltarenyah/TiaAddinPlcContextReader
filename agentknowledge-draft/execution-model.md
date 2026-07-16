# Siemens PLC Execution Model Draft

## Core Model

A Siemens PLC program is not general application code that runs once from top to bottom and exits. It executes in recurring task contexts, typically through organization blocks such as `OB1`.

## Main Reasoning Rules

- A network is evaluated inside a scan context.
- The apparent left-to-right ladder view is still part of scan-based execution.
- Outputs and internal bits may be rewritten multiple times in one cycle by different logic locations.
- FB behavior depends on instance state, not only current inputs.

## Practical Interpretation

### Scan Cycle

Typical scan-oriented reasoning:

1. read current process inputs and relevant memory state
2. execute scheduled logic blocks
3. update outputs and memory
4. repeat on the next cycle

The exact scheduling depends on OB type and project structure.

### OB, FC, FB, DB

- `OB`: scheduling entry point
- `FC`: stateless function unless it reads or writes external memory
- `FB`: stateful logic unit with an instance DB
- `DB`: stored data, either global or bound to an FB instance

### Networks

In exported source, one `SW.Blocks.CompileUnit` usually corresponds to one network. Treat that as a useful logic boundary, but not as an isolated program with independent memory.

### Timers And Counters

Timers and counters must be interpreted as stateful elements across scans. A single snapshot of a rung does not fully explain their behavior.

### Branches

LAD and FBD branches represent logical structure, not CPU thread parallelism.

### Multiple Writers

If several networks write the same bit or member:

- do not summarize behavior from only one network
- do not assume one writer is the intended owner
- report competing writers when relevant

### Startup And Fault OBs

Some behavior is not in `OB1`. Initialization, diagnostics, fault recovery, and communication work may live in different OBs. Do not assume cyclic logic alone explains system behavior.
