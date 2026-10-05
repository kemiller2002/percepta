---
id: DF-PERCEPTA-2026-0002
title: Mark HY-PERCEPTA-2026-0005 supported on the EX-PERCEPTA-2026-0004 evidence (owner decision)
status: accepted
type: decision-record
decision_type: research
research_area: percepta-verification
created: 2026-10-05
updated: 2026-10-05
confidence: low
decided_by: kemiller2002 (repository owner)
supporting_evidence:
  - EV-PERCEPTA-2026-0002
related_documents:
  - research/hypotheses/HY-PERCEPTA-2026-0005--semantic-contract-held-out-generalization.md
  - research/evidence/EV-PERCEPTA-2026-0002--held-out-semantic-generalization-results.md
  - research/experiments/EX-PERCEPTA-2026-0004--held-out-semantic-generalization.md
  - research/decisions/DF-PERCEPTA-2026-0001--conclude-ex-0004-descriptive-support.md
supersedes:
  - DF-PERCEPTA-2026-0001
superseded_by: []
tags:
  - percepta
  - hypothesis
  - held-out-generalization
---

# DF-PERCEPTA-2026-0002

## Context

DF-PERCEPTA-2026-0001 concluded EX-PERCEPTA-2026-0004 with descriptive support for HY-PERCEPTA-2026-0005. It kept the hypothesis `active` at `low` confidence instead of marking it `supported`.

The repository owner, kemiller2002, has decided that the hypothesis should be marked `supported`. This record documents that decision. No new evidence was collected for it, and it rests on EV-PERCEPTA-2026-0002 alone.

## Decision

1. **HY-PERCEPTA-2026-0005 status is `supported`.** This is the schema's status for a hypothesis whose evidence supports it (`schemas/hypothesis.schema.json`). The owner made the decision.
2. **Confidence stays `low`.** The repository's rules do not tie a confidence level to `supported`:
   - `docs/00-governance/AI-Repository-Operating-System.md` defines confidence as justified belief, separate from disposition.
   - `docs/00-governance/Agent-Operating-Manual.md` says acceptance means "supported enough for its decision context, not permanently proven".

   The evidence has not changed, so the confidence label stays where the evidence puts it. Under the Low band, that is weak support.
3. **This record supersedes DF-PERCEPTA-2026-0001.** These parts of DF-PERCEPTA-2026-0001 still stand and are restated here:
   - EX-PERCEPTA-2026-0004 is `completed`;
   - the verdict under the preregistered criterion is descriptive support;
   - both independent evaluations stand as evidence, with no post-hoc adjudication.

   The part that is replaced is the conclusion to keep HY-PERCEPTA-2026-0005 `active`.

## Evidence

EV-PERCEPTA-2026-0002 compares fully-correct held-out cases for treatment and control:

- Evaluation 1: 8/10 against 6/10.
- Evaluation 2: 10/10 against 5/10.
- Both meet the preregistered support criterion (treatment rate higher than control rate).
- Treatment is also higher under every single-reviewer reading.

## Limitations (unchanged by this decision)

- **n = 2 implementations per condition**, with one session per provider × condition. The design cannot separate a contract effect from session-to-session variation, and it is not statistically conclusive.
- **Claude-family reviewers only.** Every semantic reviewer in both evaluations was a Claude-family AI model, the same vendor family as the Claude lanes. No human review was performed.
- **The evaluator procedure was not frozen beforehand.** The preregistration required the evaluator and review procedure to be frozen before implementation, and none was. Each evaluation declared its own procedure after the gate and before rendering any candidate.
- **Evaluation 1 has a tie.** The Claude treatment and control candidates both scored 3/5. In that evaluation the whole condition difference comes from the OpenAI pair.

`supported` therefore means supported by one small pilot under its preregistered descriptive criterion. It does not mean the effect is established.

## Alternatives considered

- **Keep the hypothesis `active`, as DF-PERCEPTA-2026-0001 did.** The owner overrode this.
- **Raise confidence along with the status.** Rejected. No repository rule requires it, and the evidence has not changed.

## Consequences

- The hypothesis record and registries show HY-PERCEPTA-2026-0005 as `supported` at `low` confidence.
- Contradicting evidence from a future replication should be recorded as a new evidence record. It can move the hypothesis to `rejected` or `superseded` through a new decision record.
- The follow-up design that DF-PERCEPTA-2026-0001 recommended is still the way to raise confidence: more sessions per cell, a frozen evaluator procedure, and human or non-Claude review.

## Reversibility

Fully reversible. Only research record status changes.
