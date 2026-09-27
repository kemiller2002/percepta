# EX-PERCEPTA-2026-0004: independent evaluations

Two evaluation sessions evaluated the same four frozen candidates against the same five held-out cases. They ran independently, and neither saw the other's work before its own judgments were frozen. Both are kept as evidence. Neither replaces the other.

| | Evaluation 1 | Evaluation 2 |
|---|---|---|
| Location | this directory (`EVALUATION.md`, `results.json`) | `independent-evaluation-2/` |
| Source | PR #16, head `dc4ca479`, merged as `6f370c8` | PR #14, head `59f21222` |
| Original path | `artifacts/percepta/experiment-0004/` | `artifacts/percepta/experiment-0004/` |
| Primary scoring | obligation passes only if both reviewers pass it | obligation passes only if both reviewers pass it at 1440x900 |
| Treatment fully correct cases | 8/10 (0.80) | 10/10 (1.00) |
| Control fully correct cases | 6/10 (0.60) | 5/10 (0.50) |
| HY-PERCEPTA-2026-0005 | descriptive support | descriptive support |

Fully correct cases per candidate:

| Lane | Evaluation 1 | Evaluation 2 |
|---|---|---|
| treatment-a (OpenAI) | 5/5 | 5/5 |
| control-a (OpenAI) | 3/5 | 2/5 |
| treatment-b (Claude) | 3/5 | 5/5 |
| control-b (Claude) | 3/5 | 3/5 |

Notes:

- Evaluation 1 is at the preregistered output path (`results.json`) only because it merged first. That order is not a judgment about validity.
- Evaluation 2's files are byte-identical to PR #14 head `59f21222`, and every git blob ID matches. They are relocated but not edited. Its `frozen-evidence.SHA256SUMS` uses relative paths and verifies in place.
- Some of Evaluation 2's raw verifier logs (`raw/candidate-*/verifier/{evidence.json,stdout.txt}`) still name `artifacts/percepta/experiment-0004/...`. That was their output path when they ran, and it is left unedited.
- The preregistration did not freeze a semantic-review procedure, so each evaluator wrote its own before running candidates. The two evaluations agree on the direction of the result. Their disagreement, mainly on treatment-b and control-a, is evidence of how sensitive the outcome is to the evaluator and the reviewers.
