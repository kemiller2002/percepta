# Percepta Project Charter

## Mission

Create a reusable, typed mechanism for preserving product meaning when software user interfaces are implemented or modified by AI agents.

## Problem

General design guidance and component libraries constrain style and implementation vocabulary, but they do not guarantee that an agent will preserve:

- information architecture
- semantic hierarchy
- domain-state visibility
- action legality
- unresolved obligations
- user understanding

A technically correct implementation can therefore still be the wrong product.

## Thesis

Product-specific UI intent can be represented as explicit contracts and verified with evidence.

The contract should constrain meaning without unnecessarily constraining layout.

## Core principle

> The agent may choose implementation details, but it may not silently redefine product meaning.

## Non-goals

Percepta is not:

- a design system
- a CSS framework
- a component library
- a state-machine engine
- a replacement for accessibility standards
- a screenshot-only visual regression system
- a general end-to-end test framework

It composes with those systems.

## Design constraints

- F# is the canonical implementation language for the contract kernel.
- The core should minimize external dependencies.
- Contracts must have a human-readable projection.
- Validation should be deterministic wherever possible.
- Visual or LLM review may provide evidence but must not be the sole completion gate.
- Unknown verification capability must not be reported as success.
- Product semantics should be expressible without specifying pixel layouts.
- Accessibility is part of contract correctness, not a later enhancement.

## Initial success criterion

Three independent agents should be able to implement the same application screen with visibly different layouts while preserving the same tested product semantics.

If implementations satisfy the formal checks but humans still identify materially different product meaning, the missing invariant should be added to the contract system.
