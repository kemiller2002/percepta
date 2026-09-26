---
id: EX-PERCEPTA-2026-0004
title: Held-out semantic-state generalization
research_area: percepta-verification
status: preregistered
created: 2026-09-25
author_agent: chatgpt
tests_hypotheses:
  - HY-PERCEPTA-2026-0005
related_theories: []
inputs:
  - research/experiments/EX-PERCEPTA-2026-0004/frozen-product-brief.md
  - research/experiments/EX-PERCEPTA-2026-0004/frozen-runtime-protocol.md
  - .percepta/contracts/indy-init-investigation-workspace.json
evaluation_only_inputs:
  - research/experiments/EX-PERCEPTA-2026-0004/held-out-semantic-cases.json
outputs:
  - artifacts/percepta/experiment-0004/results.json
---

# Experiment

## Research question

With runtime mechanics held constant, does access to a Percepta semantic contract improve first-pass preservation of product meaning on combinations of states that implementation agents never see?

## Design

Four independent first-pass sessions:
- OpenAI control A
- OpenAI treatment A
- Claude control B
- Claude treatment B

All receive the same frozen product brief and runtime protocol. No implementation session may inspect held-out cases, evaluator implementation, prior experiment implementations, sibling lanes, or results.

Treatment receives the canonical Percepta contract. Control does not.

The product brief intentionally does not enumerate predicate-to-observation mappings or evaluator observation IDs.

## Held-out evaluation

The evaluation-only cases are frozen before implementation and MUST NOT be read by implementation sessions. They combine predicates and capability states in ways not illustrated during implementation.

Evaluation has two layers:

1. deterministic structural/runtime checks shared across all candidates;
2. blinded semantic review of each held-out state, asking whether the visible/user-operable UI preserves every preregistered semantic obligation for that case.

A semantic obligation is Passed only when directly supported by visible or operable evidence. Missing, ambiguous, fabricated, or contradictory meaning is Failed. No evaluator-driven correction is permitted.

## Primary outcome

Per candidate: number of held-out cases for which every semantic obligation passes.

Per condition: total fully-correct held-out cases / total held-out cases.

HY-PERCEPTA-2026-0005 receives descriptive support only when treatment has a higher fully-correct held-out-case rate than control.

## Secondary outcomes

- semantic obligations passed / evaluated;
- failures by semantic obligation;
- deterministic required-category completion;
- provider-stratified comparisons;
- forbidden/fabricated meaning defects;
- responsive preservation of held-out semantics;
- implementation churn.

## Anti-leakage rules

Implementation agents MUST NOT read:
- `held-out-semantic-cases.json`;
- evaluator code or semantic-review prompts;
- sibling lanes;
- prior EX-0002/EX-0003 implementations or screenshots;
- experiment results.

Lane instructions must repeat this prohibition.

The held-out file may exist in the repository because its blob must be frozen and auditable, but implementation agents are explicitly restricted from opening it.

## Blinding

Semantic reviewers receive anonymized rendered candidates and a single held-out state's semantic obligations. They do not receive provider, condition, branch, NOTES.md, source code, or deterministic results.

Where practical, use more than one independent reviewer. Record disagreements rather than forcing consensus. The primary scoring rule requires direct visible/operable evidence, not reviewer preference.

## Acceptance and falsification

Support: treatment fully-correct held-out-case rate > control rate.

No support: treatment rate <= control rate.

This is a small descriptive pilot and is not statistically conclusive.

Mechanical deterministic completion cannot substitute for semantic-generalization success.

## Integrity

Before implementation:
- freeze brief, runtime protocol, held-out cases, contract blob, lane instructions, evaluator/review procedure, and baseline SHA;
- record all blob SHAs;
- verify control-visible inputs do not expose the held-out cases or contract mappings.

Each lane branches from the same final preregistration commit and changes exactly its index.html and NOTES.md.

No experimental evaluator runs before first-pass freeze.

## Stop condition

Do not begin implementation until all inputs and lane instructions are frozen and registered.
