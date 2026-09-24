---
id: HY-PERCEPTA-2026-0002
title: Semantic substitutions can escape deterministic Percepta hooks
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

A materially incorrect UI can preserve Percepta's current deterministic hooks and still pass all required deterministic checks when the error is primarily semantic content rather than structure, state legality, accessibility, or responsive behavior.

## Mechanism

Current deterministic checks prove that declared regions and observations are present and that capability/state behavior is exposed correctly. They do not yet prove that arbitrary human-language content inside a correctly marked region preserves the intended product meaning.

## Predictions

At least one semantic-content mutant that leaves contract markers and state behavior intact will remain complete when semantic visual review is not required.

Candidate escape mutants include:

- replacing the screen's primary question with a materially different question while preserving all hooks;
- replacing the content of one semantic region with misleading but structurally valid content while preserving its region identifier.

## Evidence that would support it

A mutant produces `Complete: true` while a human inspection shows the product meaning is materially wrong.

## Evidence that would contradict it

All semantic-substitution mutants are rejected by current deterministic evidence.

## Tests performed

See EX-PERCEPTA-2026-0001.

## Results

Pending.

## Falsification attempts

Mutations are chosen to preserve every known deterministic hook while changing user-facing meaning.

## Current assessment

Active and untested.

## Next experiment

Require a real human or vision-model semantic review and test whether it catches the escaped mutants without introducing unacceptable false positives.
