---
id: EV-PERCEPTA-2026-0002
title: Held-out semantic-state generalization pilot results (EX-PERCEPTA-2026-0004)
research_area: percepta-verification
evidence_type: test-result
status: accepted
source_title: EX-PERCEPTA-2026-0004 frozen first-pass evaluations 1 and 2
source_author: Two independent AI evaluation sessions (Claude Code), each with two blinded AI semantic reviewers
source_uri: https://github.com/kemiller2002/percepta/tree/main/artifacts/percepta/experiment-0004
source_date: 2026-09-27
retrieved: 2026-10-05
created: 2026-10-05
created_by_agent: anthropic/claude-code
confidence: low
supports:
  - HY-PERCEPTA-2026-0005
contradicts: []
related_theories: []
related_documents:
  - research/experiments/EX-PERCEPTA-2026-0004--held-out-semantic-generalization.md
  - research/hypotheses/HY-PERCEPTA-2026-0005--semantic-contract-held-out-generalization.md
  - research/decisions/DF-PERCEPTA-2026-0001--conclude-ex-0004-descriptive-support.md
  - artifacts/percepta/experiment-0004/EVALUATIONS.md
tags:
  - percepta
  - semantic-contracts
  - held-out-generalization
  - blinded-review
  - pilot
---

# Evidence Record

## Evidence summary

EX-PERCEPTA-2026-0004 asked whether access to the canonical Percepta semantic contract improves first-pass preservation of product meaning on held-out combinations of predicates and capability states that implementation sessions never saw. Runtime mechanics were held constant by a frozen product brief and runtime protocol.

Four first-pass candidates were built from baseline `f474c449c2bc84da5707bb123c36500490875869`: OpenAI control-a and treatment-a, and Claude control-b and treatment-b. Two independent evaluation sessions then scored each candidate against 5 frozen held-out cases with 25 semantic obligations (held-out file blob `b702a376bf5b8f3d33a9d348785c5b3b1c50e687`). Each evaluation used two blinded reviewers per item and a conjunctive scoring rule: an obligation passes only if both reviewers pass it, and a case is fully correct only if every obligation passes.

Both evaluations meet the preregistered support criterion: the treatment fully-correct held-out-case rate is higher than the control rate.

| | Evaluation 1 | Evaluation 2 |
|---|---|---|
| Treatment fully correct | 8/10 (0.80) | 10/10 (1.00) |
| Control fully correct | 6/10 (0.60) | 5/10 (0.50) |
| Criterion (treatment > control) | met | met |

The result is a small descriptive pilot (n = 2 implementations per condition, one session each). It is not statistically conclusive, and the preregistration says so.

## Exact claim supported or contradicted

HY-PERCEPTA-2026-0005:

> When coding agents receive the same product brief and exact runtime-state protocol, implementations that additionally receive a Percepta semantic contract will preserve intended human-facing semantics more reliably on previously unseen combinations of domain predicates and capability states.

The preregistered criterion in EX-PERCEPTA-2026-0004 is: "HY-PERCEPTA-2026-0005 receives descriptive support only when treatment has a higher fully-correct held-out-case rate than control." Both evaluations meet it. This record therefore gives the hypothesis **descriptive support**. It is not confirmation.

## Source provenance

- Repository: `kemiller2002/percepta`
- Preregistration: `research/experiments/EX-PERCEPTA-2026-0004--held-out-semantic-generalization.md`, frozen as blob `b74f0a475b99bca46fca1783099c467a361fa2bc`
- Baseline: `f474c449c2bc84da5707bb123c36500490875869`
- Frozen candidate commits:

| Lane | Branch | Commit | `index.html` SHA-256 (prefix) |
|---|---|---|---|
| treatment-a (OpenAI) | `experiment/percepta-0004-openai-treatment-a` | `d104371f69ae4b184769e85296fd7d4c1cd85c76` | `190736abd66a86f2` |
| control-a (OpenAI) | `experiment/percepta-0004-openai-control-a` | `c99950ba1cf79a8f590e0f050b3b4efadecc0ad1` | `b8ea011008d4079b` |
| treatment-b (Claude) | `experiment/percepta-0004-claude-treatment-b` | `ed5e15fd28d7c33dd4bd51014d1c1f4d62bdbb4b` | `d0223ea8137b4bee` |
| control-b (Claude) | `experiment/percepta-0004-claude-control-b` | `6b3ef30b75aaab4812919b7f56e8085dbb0fe442` | `51daaabb59f3988e` |

- Evaluation 1: `artifacts/percepta/experiment-0004/` (`EVALUATION.md`, `results.json`). Merged from PR #16. Raw evidence manifest `raw-evidence.SHA256SUMS` (SHA-256 `744fdc92…99f8270`, 653 files), frozen 2026-09-27T05:19:12Z, before unblinding.
- Evaluation 2: `artifacts/percepta/experiment-0004/independent-evaluation-2/` (`EVALUATION.md`, `results.json`). From PR #14 head `59f21222`. Manifest `frozen-evidence.SHA256SUMS` (SHA-256 `aba73776…`, 541 files), frozen 2026-09-26T20:46:48Z, before unblinding.
- Comparison of the two evaluations: `artifacts/percepta/experiment-0004/EVALUATIONS.md`.

## Relevant excerpt or data

Fully correct held-out cases per candidate (primary scoring, both reviewers must pass):

| Lane | Provider | Condition | Evaluation 1 | Evaluation 2 |
|---|---|---|---|---|
| treatment-a | OpenAI | treatment | 5/5 | 5/5 |
| control-a | OpenAI | control | 3/5 | 2/5 |
| treatment-b | Claude | treatment | 3/5 | 5/5 |
| control-b | Claude | control | 3/5 | 3/5 |

Semantic obligations passed (primary scoring):

| Condition | Evaluation 1 | Evaluation 2 |
|---|---|---|
| Treatment | 48/50 | 50/50 |
| Control | 45/50 | 44/50 |

Single-reviewer sensitivity (fully correct cases, treatment vs control):

| Reading | Evaluation 1 | Evaluation 2 |
|---|---|---|
| R1 alone | 8/10 vs 6/10 | 10/10 vs 6/10 |
| R2 alone | 10/10 vs 8/10 | 10/10 vs 7/10 |

Treatment is higher than control under all six readings (two evaluations, three scoring rules each).

Reviewer disagreements: 5 of 100 obligation judgments in Evaluation 1 and 4 of 100 in Evaluation 2. Under the conjunctive rule each disagreement counts as Failed.

Failures that both reviewers agreed on, in both evaluations: control-a has no user-operable path from the unavailable confirmation to the blocking information (cases `combined-blocker-falsification` and `all-constraints`).

Deterministic layer (repository verifier, 6 required categories): no candidate was deterministically complete in either evaluation. treatment-b passed 5/6; the other three passed 4/6. All four failed `state-projection`, because the frozen brief does not publish the observation hook IDs the verifier looks for.

## Interpretation

- The preregistered criterion is met in both independent evaluations, and the direction holds under every scoring rule reported. This is descriptive support for HY-PERCEPTA-2026-0005.
- The size of the effect depends on the evaluator. The two evaluations disagree mainly on treatment-b (3/5 vs 5/5) and control-a (3/5 vs 2/5). In Evaluation 1 the Claude pair ties at 3/5 each, so the whole condition difference comes from the OpenAI pair. In Evaluation 2 both pairs favour treatment (+3 OpenAI, +2 Claude).
- The one defect both evaluations agree on (control-a: unavailable action explained but not navigable to its blocker) is the kind of capability projection the contract makes explicit. This is consistent with the hypothesised mechanism, but one defect in one candidate cannot establish it.
- Treatment did not prevent fabricated meaning. Single reviewers flagged invented domain content or invented causal reasons in both treatment and control candidates. Evaluation 1's stricter reviewer failed treatment-b on two obligations for citing falsification as a reason for illegality.
- Deterministic completion did not track semantic success. treatment-a scored 5/5 semantically in both evaluations but 4/6 deterministically. As the preregistration states, mechanical completion is not evidence of semantic generalization.
- In Evaluation 1, two held-out cases (`legal-with-history`, `illegal-without-known-blocker`) did not discriminate, and every failure was in cases that combine a blocker with other predicates or involve pending persistence. In Evaluation 2, `legal-with-history` did not discriminate. control-a's failures on `illegal-without-known-blocker` there came from one reviewer only.

## Limitations

- n = 2 implementations per condition and one session per provider × condition. Contract effect and session-to-session variation cannot be separated.
- Only 5 held-out cases, of which some did not discriminate.
- All semantic reviewers are Claude-family AI models, the same vendor family as the Claude lanes. Blinding was enforced by instruction and audited, but not technically sandboxed. No human review was performed.
- The preregistration listed the evaluator and review procedure among the items to freeze before implementation, but none was frozen. Each evaluation declared its own procedure after the gate and before rendering any candidate. The direction of the result does not depend on the scoring rule, but the magnitude does depend on the evaluator.
- Rendering used Chromium 141 instead of the pinned 153, and the .NET SDK came from the Ubuntu archive. The same substitution applied to every candidate.
- The deterministic verifier checks hook vocabulary that the brief withheld from all lanes, so its categories mainly measure hook exposure.

## Counterevidence

- Evaluation 1, provider-stratified: the Claude treatment and control candidates tie at 3/5. That stratum gives no support.
- Under R2 alone in Evaluation 1, both Claude candidates score 5/5, which is also a tie.
- Reviewers flagged fabricated or contradictory meaning in treatment candidates as well as control candidates, so the contract did not eliminate that defect class.

## Reproduction or verification notes

Verified on 2026-10-05 for this record, without changing any evaluation file:

```bash
cd artifacts/percepta/experiment-0004
sha256sum -c --quiet raw-evidence.SHA256SUMS      # 653 files OK; manifest hash 744fdc92…99f8270
sha256sum -c --quiet SHA256SUMS                   # 664 files OK
sha256sum semantic-review/scores.blind.json       # matches scores.blind.sha256
cd independent-evaluation-2
sha256sum -c --quiet frozen-evidence.SHA256SUMS   # 541 files OK; manifest hash aba73776…
```

- Each frozen candidate commit descends from the baseline, changes only its lane's `index.html` and `NOTES.md`, and has an `index.html` SHA-256 that matches both evaluations' candidate mappings.
- `git hash-object experiments/EX-PERCEPTA-2026-0004/held-out-semantic-cases.json` gives `b702a376bf5b8f3d33a9d348785c5b3b1c50e687`, the blob both evaluations read.
- The primary outcome, the per-reviewer sensitivity, the obligation totals and the disagreement counts were recomputed for both evaluations directly from the raw reviewer judgments. The recomputation did not use either evaluation's aggregation code. It joined Evaluation 1's `semantic-review/raw/R?/item-*.json` through `item-key.json`, and Evaluation 2's `semantic-review/judgments/R?/unit-*.json` through each packet's `heldOutState`, checking every obligation list against the held-out file. Every number in this record matches.
