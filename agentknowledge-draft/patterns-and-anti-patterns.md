# Common PLC Patterns And Anti-Patterns

## Seal-In Or Self-Hold

Meaning:
A command remains active after a momentary start condition because its own state is fed back into the permissive path.

Common appearance:
- start pushbutton or request bit
- parallel branch with current run bit
- stop or fault condition breaking the path

Common misread:
- Mistaking it for a pure one-scan command.

## Fault Latch With Reset

Meaning:
A fault condition is remembered until a reset condition clears it.

Common appearance:
- set path on abnormal condition
- separate reset path
- latch output used to inhibit operation

Common misread:
- Assuming the fault disappears automatically when the triggering condition clears.

## Command And Feedback Timeout

Meaning:
A command is issued and a timer checks whether expected feedback arrives in time.

Common appearance:
- command bit
- feedback-not-present condition
- `TON`
- resulting alarm or fault

Common misread:
- Interpreting the timer as startup delay instead of timeout supervision.

## Permissive Chain

Meaning:
Several readiness conditions must all be acceptable before an action is allowed.

Common appearance:
- multiple contacts in series
- no state memory by itself

Common misread:
- Treating every permissive chain as a safety chain.

## Interlock Chain

Meaning:
Action is blocked when another state or motion would be unsafe or invalid.

Common appearance:
- opposite direction not active
- other axis not busy
- clamp/door/home conditions

Common misread:
- Confusing a process interlock with simple readiness.

## Edge Triggered Request

Meaning:
A state change generates a pulse that starts another action.

Common appearance:
- rising edge detection
- pulse or single-scan request bit

Common misread:
- Reading it as sustained enable logic.

## MOVE-Based Value Selection

Meaning:
A value is copied into a target under one mode or branch.

Common appearance:
- compare or mode condition
- `MOVE` of setpoint or recipe value

Common misread:
- Assuming the destination only ever comes from one source.

## Anti-Pattern: Name-Only Interpretation

Bad reasoning:
"The tag is named `MotorReady`, so it must be feedback from the drive."

Why this fails:
The tag could be internal memory, a command mirror, or a derived permissive.

## Anti-Pattern: Box Means Device Behavior

Bad reasoning:
"The FB name contains `Axis`, so it definitely controls a servo enable sequence."

Why this fails:
The FB may be a wrapper, simulator, aggregator, or status decoder. Inspect callers, parameters, and instance data.
