# Specification preflight (`percepta spec-preflight`)

Finds contradictory and incomplete specifications before implementation starts, while a fix is still an owner decision rather than a mid-run defect.

It is a self-contained module (`src/Percepta.Core/Specification.fs`). It does not touch the screen-contract model.

```
percepta spec-preflight --machines state-machine-contracts.json --invariants invariants.json \
  --interfaces interface-specifications.json --catalog COMMAND-EVENT-CATALOG.md \
  [--refusals dokimos-refusals.json] [--out report.json]
```

Exit codes: 0 nothing blocking; 3 at least one blocking finding; 2 unreadable input.

The output is `percepta.specification-preflight/1`, with these sections: `blockingContradictions`, `missingInterfaces`, `missingDecisions`, `unsupportedVerificationObligations` (from `dokimos verification compile` refusals), `suggestedOwnerActions` and `warnings`.

## Rules

Every rule is generic: no requirement, transition or type id appears in the code.

| Rule | Kind | Detects |
|---|---|---|
| `rate-bound-vs-unbounded-loop` | `likely-contradiction`, blocking | An invariant bounds an effect per time window ("at most N X in T seconds"). A machine it governs has a reachable self-loop that emits X, and the loop's guard neither limits repetition (no counter below its limit) nor references time. Reports the shortest legal path to the loop plus N+1 repetitions as a counterexample. |
| `counter-guard-without-complement` | `missing-specification` | A (state, event) pair that is legal only below a counter's limit, or only at it, has no transition for the other side, so that input fails closed without a stated outcome. |
| `carrier-cannot-discriminate` | `missing-interface`, blocking | The catalog says every command is carried inside type T, and T (resolved through its fields) contains no string, no multi-case union and no opaque payload type. A consumer of T therefore cannot tell the commands apart. Lists every type that carries T, which is where the command body is lost. |

**Certainty.** A bound read from prose is reported as a `likely-contradiction`, never as `proven`. Every finding of these rules needs an owner decision, and the report says which decision.

## Evidence: Rehearsal 8 regression

The input is the frozen Indy Init v4.1.1 package, exactly as Rehearsal 8 received it: 5 machines, 97 transitions, 25 invariants, 78 types, 26 operations and 134 catalog commands. The run takes 0.3 s and exits 3.

| Finding | Rehearsal 8 record |
|---|---|
| INV-04 × SM-SAVE T-S31 and T-S32: at most 4 reconciliation reads in 60 s, versus user- and network-driven loops with no clock | DEV-R8-11 (left unresolved; INV04 failed at acceptance) |
| `CommandEnvelope` holds only identities and versions; the body is lost in `SaveInput.Edit`, `InvestigationSession.Unsaved`, `SaveEffect.ExportCommands`, `ConflictInput.ReapplyDone` and `Msg.DomainCommand` | DEV-R8-09 and CR-T17-01 (worked around in #51) |
| SM-SAVE (UnknownOutcome, ReconcileReadFailed) is specified only below the read limit | DEV-R8-12 (a stuck session, fixed in #48) |
| SM-SAVE (UnknownOutcome, CheckAgain) is specified only after the reads are spent | not raised in Rehearsal 8; plausibly an intended fail-closed, so it goes to the owner (non-blocking) |

No other finding was produced. Two false positives seen during development were fixed with general rules:
- An effect stated as `none: …` emits nothing, and effect names match as whole words.
- "reached" in "SM-CONFLICT reached Reapplied" refers to a state, not to a counter.

## Limitations

- Rate bounds are extracted from one prose form ("at most N X in|per|within T seconds"). A differently worded bound is not seen.
- Only self-loops are checked for unbounded repetition, not longer cycles.
- Type parsing covers records and unions in contract stubs, not generics or classes.
