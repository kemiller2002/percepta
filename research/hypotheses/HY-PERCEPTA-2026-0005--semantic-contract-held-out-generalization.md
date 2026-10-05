---
id: HY-PERCEPTA-2026-0005
title: Percepta semantic contracts improve held-out state generalization
research_area: percepta-verification
status: active
created: 2026-09-25
author_agent: chatgpt
confidence: low
updated: 2026-10-05
related_theories: []
supporting_evidence:
  - EV-PERCEPTA-2026-0002
contradicting_evidence: []
supersedes: []
superseded_by: []
---

# Hypothesis

When coding agents receive the same product brief and exact runtime-state protocol, implementations that additionally receive a Percepta semantic contract will preserve intended human-facing semantics more reliably on previously unseen combinations of domain predicates and capability states.

## Mechanism

A semantic contract should encode relationships among state, capability legality, blockers, obligations, evidence, persistence, and retained negative knowledge. If an implementation internalizes those relationships rather than matching known examples, it should generalize to combinations that were not enumerated during implementation.

## Falsification

This hypothesis is not supported if control implementations match or exceed treatment implementations on the preregistered held-out semantic-generalization outcome.

Mechanical hook presence alone is insufficient evidence for support.

## Evidence update (2026-10-05)

EX-PERCEPTA-2026-0004 is completed, and its results are recorded in EV-PERCEPTA-2026-0002. Both independent evaluations meet the preregistered criterion. Treatment had the higher fully-correct held-out-case rate in each: 8/10 against 6/10 in Evaluation 1, and 10/10 against 5/10 in Evaluation 2. This is descriptive support from a pilot with two implementations per condition.

Under DF-PERCEPTA-2026-0001, the status stays `active` and confidence moves from `very-low` to `low`. The hypothesis is not marked `supported` for these reasons:

- the sample is very small;
- every semantic reviewer was an AI model;
- the evaluator procedure was not frozen before implementation;
- Evaluation 1 found no difference within the Claude pair.
