# Percepta Architecture

## Responsibility boundary

```text
Ordo
  governs what is true and legal

Percepta
  governs what must be perceivable

Forma
  governs reusable presentation primitives

ROS / Praxis
  govern work, evidence, metrics, and completion
```

Percepta integrates with the Echelon stack, but its core contract model is intentionally independent.

## Layers

### Percepta.Core

A dependency-light F# library containing canonical types for:

- screens
- semantic regions
- hierarchy
- visibility
- capabilities
- state projections
- forbidden patterns
- breakpoints
- verification obligations

The core does not render UI and does not decide domain legality.

### Percepta.Foundation

The Echelon integration layer.

This layer may reference Aegis and later provide adapters for Ordo, ROS, Praxis, Forma, Limen, Visual Engineering, and Communication Engineering.

### Verification adapters

Adapters translate Percepta contracts into checks against a concrete UI technology.

Potential adapters include:

- semantic DOM
- browser automation
- accessibility tooling
- responsive capture
- visual regression
- vision-model semantic review

Adapters return evidence through a common verification model.

## Authority rule

Percepta must never create domain authority that belongs to the application or Ordo.

For example, Percepta may require that `ConfirmRootCause` be unavailable when illegal, but it does not decide whether that transition is legal.

The domain supplies legality. Percepta verifies that the interface faithfully exposes it.

## Contract lifecycle

```text
Application/domain intent
        |
        v
Percepta contract
        |
        +--> human specification
        +--> agent implementation guidance
        +--> structural checks
        +--> state-projection checks
        +--> interaction checks
        +--> accessibility obligations
        +--> responsive evidence
        +--> visual evidence
        |
        v
Verification result
        |
        v
governing completion decision
```

## Drift

Changes in domain state or UI implementation can invalidate contracts.

Future tooling should support traceability from:

```text
domain state/capability
        ->
screen contract
        ->
verification obligations
```

so affected contracts can be identified during change.
