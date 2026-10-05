# EX-PERCEPTA-2026-0004 lane: treatment-a

Independent first-pass implementation. Condition: treatment.

## Permitted experimental inputs
- this AGENTS.md
- `research/experiments/EX-PERCEPTA-2026-0004/frozen-product-brief.md`
- `research/experiments/EX-PERCEPTA-2026-0004/frozen-runtime-protocol.md`
- `.percepta/contracts/indy-init-investigation-workspace.json` is permitted and MUST inform semantic implementation.
- Repository-root `AGENTS.md` may be read only if needed to interpret that contract.

## Strict isolation
MUST NOT inspect:
- `research/experiments/EX-PERCEPTA-2026-0004/held-out-semantic-cases.json`;
- evaluator code, semantic-review prompts, evaluation artifacts or results;
- sibling lanes;
- EX-PERCEPTA-2026-0002 or EX-PERCEPTA-2026-0003 implementations, screenshots, notes, or results;
- branches/PRs/history for another implementation.

Do not run the experimental evaluator before freeze.

## Output
Write exactly:
- `research/experiments/EX-PERCEPTA-2026-0004/lanes/treatment-a/index.html`
- `research/experiments/EX-PERCEPTA-2026-0004/lanes/treatment-a/NOTES.md`

Do not modify shared inputs. Ordinary local checks are allowed, including constructing your own runtime states, but do not seek or reconstruct held-out cases.

Commit the two outputs and STOP. No evaluator-driven correction.
