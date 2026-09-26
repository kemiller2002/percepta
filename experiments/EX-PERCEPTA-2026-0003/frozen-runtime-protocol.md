# EX-PERCEPTA-2026-0003 frozen runtime protocol

This document is an experimental input supplied identically to every control and treatment implementation session.

## Runtime entry point

The implementation MUST expose:

`window.__perceptaSetState(state)`

The harness calls this function with an object of this exact shape:

```json
{
  "name": "state-name",
  "predicates": ["predicate-name"],
  "capabilities": {
    "capability-name": "legal"
  }
}
```

## Semantics of the transport

- `name` is an opaque fixture identifier.
- `predicates` is a set of active predicate names. An implementation MUST consume the supplied set rather than requiring richer domain objects to be present.
- `capabilities` is an authoritative map from capability name to either `"legal"` or `"illegal"`.
- A supplied capability value MUST NOT be overridden by UI inference.
- `"legal"` means the named capability is executable.
- `"illegal"` means the named capability is not executable.
- The implementation may derive presentation data from these inputs, but MUST NOT reinterpret the transport values.

## Neutral observation hooks

The harness observes semantic output through attributes described by the product brief/lane instructions. This protocol defines only how state reaches the page. It does not define which predicates require which UI observations, which actions should be constrained by particular predicates, or the semantic relationships between states and presentation.

Those semantic requirements differ by experimental condition.

## Isolation

Do not inspect evaluator source, fixtures beyond this protocol, sibling lanes, other implementations, experiment results, or prior experiment implementations.
