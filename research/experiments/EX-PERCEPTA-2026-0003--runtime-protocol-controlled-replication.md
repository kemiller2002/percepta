---
id: EX-PERCEPTA-2026-0003
title: Runtime-protocol-controlled Percepta semantic-contract replication
research_area: percepta-verification
status: preregistered
created: 2026-09-25
author_agent: chatgpt
tests_hypotheses:
  - HY-PERCEPTA-2026-0003
  - HY-PERCEPTA-2026-0004
related_theories: []
inputs:
  - research/experiments/EX-PERCEPTA-2026-0003/frozen-product-brief.md
  - research/experiments/EX-PERCEPTA-2026-0003/frozen-runtime-protocol.md
  - .percepta/contracts/indy-init-investigation-workspace.json
outputs:
  - artifacts/percepta/experiment-0003/results.json
---

# Experiment

## Research question

When implementation sessions receive the same product requirements and the same explicit runtime-state protocol, does adding the Percepta semantic contract improve first-pass semantic compliance?

## Motivation

EX-PERCEPTA-2026-0002 produced control completion 0/2 and treatment completion 1/2, meeting the preregistered support condition for HY-PERCEPTA-2026-0003 descriptively. Post-experiment unblinding identified a design ambiguity: the successful treatment consumed the fixture/runtime shape directly, while the unsuccessful treatment did not consume the predicate set and interpreted capability legality differently. The runtime vocabulary was not equivalently explicit to controls.

This replication isolates the semantic-contract effect by freezing and supplying the same runtime protocol to every lane.

## Design

Four independent first-pass sessions start from one frozen baseline:

- OpenAI control A
- OpenAI treatment A
- Claude control B
- Claude treatment B

Every lane receives:
- the identical frozen product brief;
- the identical frozen runtime protocol defining `window.__perceptaSetState`, `predicates[]`, and authoritative `capabilities{}`;
- neutral implementation/evidence-hook mechanics necessary for deterministic observation.

Control lanes MUST NOT read the Percepta contract, generated Percepta semantic guidance, evaluator source, fixtures, sibling lanes, prior experiment implementations, or results.

Treatment lanes additionally receive the canonical Percepta semantic contract and permitted generated semantic guidance.

The runtime protocol MUST NOT reveal the mapping from predicates to required observations or the semantic constraints being tested. That mapping is the treatment variable.

## Primary outcome

Whole-lane first-pass required Percepta completion before corrective iteration.

Report both:
- completed lanes / lanes per condition;
- required evidence categories passed / total required evidence categories per condition.

The whole-lane completion comparison is primary. Category totals are diagnostic and MUST NOT replace it.

## Secondary outcomes

- required Failed and Unavailable categories;
- per-lane required-category pass count;
- individual state-projection failures;
- interaction/capability failures;
- changed-file count and churn;
- provider-stratified result;
- implementation diversity among compliant treatment outputs;
- corrective iterations after first-pass freeze, if later undertaken.

## Acceptance and interpretation

HY-PERCEPTA-2026-0003 receives replication support when treatment whole-lane first-pass completion rate is greater than control.

Because n=2 per condition, results remain descriptive and are not statistically conclusive.

HY-PERCEPTA-2026-0004 receives support only if at least two compliant treatment implementations differ materially in layout/composition under blinded review.

## Falsification

- If control completion matches or exceeds treatment completion, this replication does not support HY-PERCEPTA-2026-0003.
- If the treatment advantage from EX-0002 disappears after equalizing the runtime protocol, the EX-0002 result is consistent with runtime-information asymmetry rather than a demonstrated semantic-contract effect.
- Missing or Unavailable evidence is never converted to Passed.
- A provider-specific treatment/control difference must be reported rather than hidden by aggregate results.

## Blinding and independence

No implementation session may inspect a sibling lane, prior implementation, evaluator result, or EX-0002 implementation.

Control sessions may not inspect the Percepta contract or generated semantic guidance.

Treatment sessions may inspect only the explicitly permitted Percepta inputs.

No lane runs the experimental evaluator before its first-pass artifact is frozen.

## Frozen inputs

Before execution:
1. Copy/freeze the product brief for EX-0003.
2. Freeze `frozen-runtime-protocol.md`.
3. Record the canonical Percepta contract blob SHA.
4. Record evaluator, fixture, and baseline commit SHAs.
5. Create lane-specific AGENTS.md files with condition-specific access rules.
6. Verify that control-visible inputs contain no predicate-to-observation mapping.

After these are recorded, no input may change without a preregistered amendment made before any lane implementation exists.

## Evaluation

Use one evaluator version, contract, and fixture set for all four frozen artifacts. Stage artifacts byte-for-byte without merging implementation branches.

Record:
- lane commit SHA;
- implementation blob SHA;
- evaluator SHA/version;
- contract blob SHA;
- fixture blob SHAs;
- exit code;
- required evidence status;
- complete flag.

No corrective implementation occurs during measurement.

## Provider analysis

Report the paired descriptive comparisons separately:
- OpenAI control A vs OpenAI treatment A;
- Claude control B vs Claude treatment B.

Do not infer that a provider is superior from one session.

## Diversity analysis

Perform blinded composition review only if at least two treatment lanes satisfy all required semantic evidence. Reviewers receive anonymized outputs without condition labels or deterministic result summaries.

## Threats to validity

- n=2 per condition remains very small;
- one session per provider per condition;
- model/platform versions can vary;
- product brief and contract originate from the same research program;
- deterministic hooks may influence implementation;
- a shared runtime protocol improves both conditions and may reduce the absolute room for Percepta to add value.

## Stop condition

Preregistration and frozen-input preparation end before implementation begins. Do not start a lane until the baseline and all input blob SHAs are recorded.
