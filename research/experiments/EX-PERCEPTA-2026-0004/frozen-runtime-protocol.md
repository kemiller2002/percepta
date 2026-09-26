# EX-PERCEPTA-2026-0004 frozen runtime protocol

This input is supplied identically to every implementation session.

The page MUST expose `window.__perceptaSetState(state)`.

The harness supplies an object:

```json
{
  "name": "opaque-state-name",
  "predicates": ["predicate-name"],
  "capabilities": {
    "capability-name": "legal"
  }
}
```

Semantics of this transport:

- `name` is opaque and MUST NOT be used to infer domain meaning.
- `predicates` is the authoritative set of active predicate names.
- `capabilities` is the authoritative capability-legality map.
- capability values are exactly `"legal"` or `"illegal"`.
- supplied capability legality MUST NOT be overridden by UI inference.
- multiple predicates may be active simultaneously.
- implementations MUST support combinations of predicates, including combinations not illustrated by implementation-time examples.
- absence of a predicate means only that the predicate is not active; it does not authorize inventing the opposite domain fact.

The runtime protocol intentionally does not define the human-facing semantic consequence of any predicate. That is part of the experimental treatment variable and/or product interpretation.

Implementation sessions MUST NOT inspect held-out evaluation fixtures or evaluator logic.
