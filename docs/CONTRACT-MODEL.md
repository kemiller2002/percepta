# Contract Model

## Screen

A screen is a meaningful user-facing workspace or view.

Each screen contract defines:

- stable identifier
- purpose
- primary user question
- semantic regions
- hierarchy
- capability projections
- state projections
- forbidden patterns
- breakpoint obligations
- required verification evidence

## Purpose

Purpose describes why the screen exists, not what components it contains.

Example:

> Investigate an observed failure without prematurely converging on an explanation.

## Primary user question

The screen should help the user answer a specific question.

Example:

> What currently explains the observation, and what should be tested next?

This gives hierarchy a semantic anchor.

## Region

A region is a semantic area of information or interaction.

Examples:

- observation
- hypotheses
- evidence
- unknowns
- obligations
- legal actions

A region is not necessarily a card, column, panel, or DOM element.

## Hierarchy

Initial hierarchy levels:

- Primary
- Secondary
- Tertiary
- Background

Hierarchy expresses semantic importance, not exact size.

## Visibility

A region may be:

- AlwaysVisible
- VisibleWhen(predicate)
- MayCollapse
- SecondaryNavigation

Critical blocking state should generally not be relegated to secondary navigation.

## Capability projection

Percepta does not determine domain legality.

It may require UI behavior such as:

- expose a capability
- enable it when legal
- disable it when illegal
- explain why it is unavailable
- navigate to blocking information

## State projection

A state projection maps a domain condition to required perceivable outcomes.

Example:

```text
Domain:
  hypothesis.state = Falsified

UI:
  hypothesis remains represented
  Falsified is perceivable without color alone
  active-only actions are unavailable
```

## Forbidden patterns

Forbidden patterns represent known semantic failures.

Initial examples:

- ChatOnlyPrimaryInterface
- HideBlockingObligation
- DeleteFalsifiedHypothesis
- ColorOnlyState
- OptimisticPersistenceSuccess
- UnsupportedConfidencePercentage
- LinearizeCompetingHypotheses

The catalog should remain extensible and evidence-driven.

## Breakpoint contract

A breakpoint contract states what semantics must survive reflow.

It should avoid exact CSS unless exact geometry is itself part of the product requirement.

Example:

> On a narrow viewport, blocking unknowns may move below hypotheses but may not move behind secondary navigation.

## Evidence requirements

Initial evidence kinds:

- ContractValidation
- Structural
- StateProjection
- Interaction
- Accessibility
- Responsive
- VisualRegression
- SemanticVisualReview
- AcceptedDeviation

A governing system may require specific evidence before a screen is complete.
