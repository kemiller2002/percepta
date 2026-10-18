---
id: RQ-PERCEPTA-VIS-001
title: Preregister visual-decision quality experiment
status: draft
version: 1.0.0
owners: [percepta-maintainers]
created: 2026-10-18
updated: 2026-10-18
priority: critical
related_documents:
  - https://github.com/kemiller2002/percepta/issues/36
tags: [experiment, visual-intent, preregistration, evidence]
derived_from: [INPUT-55fa8f7cf70c3cbb]
provenance:
  contributions:
    EXE-20261018T124842050Z-a670be90:
      operations: [created]
      at: 2026-10-18T12:53:00.522Z
      actor:
        kind: agent
        id: openai/codex
        provider: openai
        model: unknown
        runtime: codex
      reason: "Canonical ingestion of claimed requirement source"
---

# Preregister visual-decision quality experiment

This draft canonically records the requirements from Percepta issue #36.
Ingestion is separate from experiment approval, execution, and any conclusion.

## Research question

Does requiring an explicit product visual-intent decision plus valid
token-contract guidance before coding reduce unsupported tokens,
color/contrast defects, review corrections, and downstream rework compared
with current agent guidance? The experiment MUST estimate effect and cost
without assuming success.

## Requirements

- **PERCEPTA-VIS-001.1:** Preregister a controlled comparison before data collection: control = current agent/requirements workflow; treatment = pinned Ordo visual-decision IR plus published Forma contract/checks before implementation. Keep source baseline, requirements, task, worker/model, compute/resource budget, browser, package version, and evaluator criteria comparable.
- **PERCEPTA-VIS-001.2:** Use representative UI tasks from Summa and Signal, including deliberately attractive-but-wrong hierarchy and dark-theme failure opportunities. Freeze fixtures and evaluators, randomize order/assignment, and blind reviewers to the arm where feasible.
- **PERCEPTA-VIS-001.3:** Measure unsupported `--ef-*` references, unauthorized literals/fallbacks, WCAG contrast/state recognition, manual semantic/aesthetic ratings, human correction count, changed-file rework, accepted-completion rate, token/cost/runtime, CI/browser wait, and decision/handoff overhead.
- **PERCEPTA-VIS-001.4:** Independently score machine-detectable correctness and human design judgment. High contrast MUST NOT automatically equal an appropriate palette. Report inter-rater agreement, failures, and uncertainty.
- **PERCEPTA-VIS-001.5:** Publish a versioned protocol, source commit IDs, fixture manifests, raw/anonymized observations where permitted, and a conclusion that distinguishes observed effect from speculation. Record negative/null results and small-sample limits.
- **PERCEPTA-VIS-001.6:** Measure first-attempt work and final accepted work so shifted costs, including extra planning/checking versus downstream rework, remain visible.

## Acceptance criteria

- Preregistration includes hypotheses, primary/secondary metrics, stopping rule, and invalidation criteria before workers run.
- Two independent evaluators, or a justified equivalent, assess subjective aesthetics; objective tests are reproducible.
- Reproduction instructions support alternative workers and operating systems.
- Findings feed back into Visual Engineering #49, Ordo #92, Forma #159, and enforcement gates without declaring improvement absent evidence.

## Scope and authority

This is distinct from Percepta experiment #5, which assesses another
intervention. Implementation remains governed by Percepta
[#36](https://github.com/kemiller2002/percepta/issues/36).
