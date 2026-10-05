---
id: DF-PERCEPTA-2026-0001
title: Conclude EX-PERCEPTA-2026-0004 with descriptive support for HY-PERCEPTA-2026-0005; keep the hypothesis active at low confidence
status: superseded
type: decision-record
decision_type: research
research_area: percepta-verification
created: 2026-10-05
updated: 2026-10-05
confidence: low
supporting_evidence:
  - EV-PERCEPTA-2026-0002
related_documents:
  - research/experiments/EX-PERCEPTA-2026-0004--held-out-semantic-generalization.md
  - research/hypotheses/HY-PERCEPTA-2026-0005--semantic-contract-held-out-generalization.md
  - research/evidence/EV-PERCEPTA-2026-0002--held-out-semantic-generalization-results.md
  - artifacts/percepta/experiment-0004/EVALUATIONS.md
supersedes: []
superseded_by:
  - DF-PERCEPTA-2026-0002
tags:
  - percepta
  - experiment
  - held-out-generalization
---

# DF-PERCEPTA-2026-0001

> **Superseded by DF-PERCEPTA-2026-0002 (2026-10-05).** The repository owner decided to mark HY-PERCEPTA-2026-0005 `supported`. That replaces item 4 below. Items 1 to 3 still stand and are restated in DF-PERCEPTA-2026-0002.

## Context

EX-PERCEPTA-2026-0004 was preregistered to test HY-PERCEPTA-2026-0005: whether a Percepta semantic contract improves first-pass preservation of product meaning on held-out state combinations. The four lanes ran and were frozen. Two independent evaluations then scored the frozen candidates (`artifacts/percepta/experiment-0004/` and `.../independent-evaluation-2/`). The experiment remained `active` because no evidence record or research decision had been written.

EV-PERCEPTA-2026-0002 records the results. Its integrity checks and its recomputation from raw reviewer judgments were rerun on 2026-10-05:

- Evaluation 1: treatment 8/10 fully correct cases, control 6/10.
- Evaluation 2: treatment 10/10, control 5/10.
- Both meet the preregistered criterion: treatment rate higher than control rate.

## Decision

1. **EX-PERCEPTA-2026-0004 is `completed`.** Every preregistered outcome was measured on the frozen candidates. Nothing further can be measured without new implementation sessions, which would be a new experiment.
2. **The verdict is descriptive support for HY-PERCEPTA-2026-0005**, per the preregistered criterion. It is not a confirmation. The preregistration describes the design as "a small descriptive pilot … not statistically conclusive".
3. **Both evaluations stand as evidence.** Neither replaces the other, no post-hoc adjudication is applied, and the evaluator disagreement on magnitude is reported, not resolved.
4. **HY-PERCEPTA-2026-0005 stays `active`, and its confidence rises from `very-low` to `low`.** EV-PERCEPTA-2026-0002 is listed as supporting evidence. The hypothesis is not moved to `supported`, for these reasons:
   - n = 2 implementations per condition;
   - the reviewers are AI models only;
   - the evaluator procedure was not frozen before implementation;
   - one evaluation shows no difference within the Claude pair.

## Alternatives considered

- **Mark HY-PERCEPTA-2026-0005 `supported`.** Rejected. The criterion is met, but the design cannot separate a contract effect from session-to-session variation. Moving to `supported` would overstate a two-session-per-condition pilot.
- **Leave EX-PERCEPTA-2026-0004 `active` until more data exists.** Rejected. The preregistered measurements are finished. More sessions would change the design, so they belong in a new preregistered experiment.
- **Pick one evaluation as authoritative.** Rejected. Both ran independently, both were frozen before unblinding, and their disagreement is itself evidence of how sensitive the outcome is to the evaluator.
- **Mark the outcome inconclusive.** Rejected. The preregistration defines the outcome as a binary descriptive comparison, and both evaluations meet the support branch. Its limits are stated in the evidence record instead.

## Consequences

- EX-PERCEPTA-2026-0004 is closed. Its frozen inputs, lanes and evaluation artifacts are not to be modified.
- Moving HY-PERCEPTA-2026-0005 beyond `low` confidence needs a follow-up experiment with:
  - more than one session per provider × condition;
  - an evaluator and semantic-review procedure frozen before implementation;
  - human or non-Claude-family semantic review, or both.
- The deterministic verifier's state-projection and interaction categories measure exposure of hook vocabulary that the brief withheld. Future generalization experiments should not treat those categories as semantic outcomes. This continues the boundary recorded in EV-PERCEPTA-2026-0001.

## Reversibility

Fully reversible. This decision changes only research record statuses and confidence. A later experiment may raise, lower or supersede the hypothesis status through a new evidence record and decision.
