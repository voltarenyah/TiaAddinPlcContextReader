# Interpretation Rules For AI Reasoning

These rules are mandatory when explaining Siemens PLC logic from exported artifacts.

## Always Do

- State what is directly observed versus what is inferred.
- Use canonical semantic names for instructions when possible.
- Check whether a signal is command, feedback, permissive, mode, fault, or internal memory.
- Check whether a block is an `FB` with state or an `FC` without instance state.
- Report ambiguity when source evidence is incomplete.

## Never Do

- Never guess machine intent from variable names alone.
- Never assume a `TON` is debounce, timeout, or startup delay without surrounding context.
- Never treat a negated contact as proof of normally closed field wiring.
- Never claim a coil is a physical output without checking the target.
- Never compress set/reset or stateful FB behavior into simple combinational logic unless the source proves it.

## Signal Interpretation Heuristics

- `Cmd`, `Req`, `Start`, `Open`, `Close`, `Enable` often indicate commands or requests.
- `Fb`, `Fbk`, `Ack`, `Done`, `Home`, `AtPos` often indicate feedback or state confirmation.
- `Perm`, `Ready`, `InterlockOk`, `Safe` often indicate permissives.
- `Fault`, `Alarm`, `Error`, `Trip` often indicate abnormal conditions.

These are hints only. Source structure overrides naming hints.

## Timer And Counter Rules

- Explain the trigger condition separately from the effect.
- Explain what resets the timer or counter.
- If reset is not visible in the same network, say so.

## Call Rules

- For an FB call, inspect parameter directions and instance data when available.
- If the call target is a project-specific wrapper block, do not assume standard behavior from the wrapper name alone.

## Multi-Writer Rules

- If one destination has multiple writers, call that out.
- Avoid definitive statements like "this bit means X" until writer ownership is clear.

## Safe Answer Style

Preferred wording:

- "The network shows..."
- "This suggests..."
- "This cannot be confirmed without checking..."
- "The source proves..."

Avoid wording:

- "This definitely means..." when evidence is partial
- "This is obviously..." when semantics depend on runtime context
