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

### Percepta.Cli

The F# CLI is the repository-facing orchestration boundary.

It:

- discovers serialized contracts;
- validates and compiles them;
- runs configured verification adapters;
- merges deterministic and external evidence;
- emits versioned evidence reports;
- returns a non-zero exit code when required evidence is unacceptable.

### Percepta.Adapter.Playwright

The first rendered-UI adapter uses Microsoft Playwright.

It provides:

- semantic region checks;
- fixture-driven state projection checks;
- capability legality and explanation checks;
- deterministic accessibility checks;
- responsive viewport verification;
- screenshot capture;
- exact-baseline comparison.

Playwright is not referenced by Percepta.Core.

### External semantic visual review

Semantic visual review is ingested as external evidence.

A human or vision-capable reviewer may assess hierarchy and perceptual meaning that deterministic browser checks cannot fully prove. That evidence cannot override deterministic failures.

### Evidence boundary

Adapters return evidence through the common Percepta evidence model.

Percepta decides whether its declared verification obligations are satisfied. ROS or another governing system remains responsible for deciding whether repository work is complete.

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
