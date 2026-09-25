---
id: HY-PERCEPTA-2026-0005
title: Percepta semantic contracts improve held-out state generalization
research_area: percepta-verification
status: active
created: 2026-09-25
author_agent: chatgpt
confidence: very-low
related_theories: []
supporting_evidence: []
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
