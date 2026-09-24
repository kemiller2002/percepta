---
id: HY-PERCEPTA-2026-0001
title: Deterministic Percepta verification detects machine-addressable UI contract violations
research_area: percepta-verification
status: supported
confidence: medium
created: 2026-09-24
author_agent: chatgpt
supporting_evidence:
  - EV-PERCEPTA-2026-0001
contradicting_evidence: []
related_theories: []
supersedes: []
superseded_by: []
---

# Hypothesis

## Statement

For violations of obligations that the Percepta contract exposes through deterministic browser-verification hooks, Percepta will detect at least 90% of intentionally injected faults and will never report the screen complete when the violated obligation is required.

## Mechanism

Percepta turns contract obligations into explicit browser checks against semantic regions, state observations, capability projections, accessibility invariants, and responsive behavior. Faults that remove or contradict those machine-addressable observations should therefore become deterministic Failed or Unavailable evidence rather than being hidden by visual plausibility.

## Predictions

- The unmodified canonical Indy Init fixture passes all required evidence.
- At least 90% of hook-addressable mutants fail the intended required verification category.
- No detected required mutation produces `Complete: true`.
- A passing semantic visual review does not override a deterministic required failure.

## Evidence that would support it

A mutation matrix showing the predicted detector category failing for at least 90% of machine-addressable faults, with completion false for each detected required fault.

## Evidence that would contradict it

- Multiple machine-addressable faults complete successfully.
- A required Failed or Unavailable result is still summarized as complete.
- Semantic visual review changes a deterministic failure into completion.

## Tests performed

See EX-PERCEPTA-2026-0001.

## Results

Pending.

## Falsification attempts

The experiment deliberately removes or corrupts the exact user-facing observations Percepta claims to verify.

## Current assessment

Active and untested.

## Next experiment

Independent agents implement the same screen contract without sharing implementation artifacts.
