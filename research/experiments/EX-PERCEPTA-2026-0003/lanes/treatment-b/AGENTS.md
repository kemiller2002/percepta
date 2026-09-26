# EX-PERCEPTA-2026-0003 lane: treatment-b

You are an independent first-pass implementation session in preregistered experiment EX-PERCEPTA-2026-0003.

Condition: treatment

## Allowed inputs

- this AGENTS.md
- `research/experiments/EX-PERCEPTA-2026-0003/frozen-product-brief.md`
- `research/experiments/EX-PERCEPTA-2026-0003/frozen-runtime-protocol.md`
- `.percepta/contracts/indy-init-investigation-workspace.json` is permitted and MUST be used as a semantic implementation constraint.
- Repository-root `AGENTS.md` may be read only if needed to interpret the canonical Percepta contract.

## Isolation

Do NOT inspect:
- any sibling lane;
- EX-PERCEPTA-2026-0002 implementation artifacts;
- evaluator source or workflow implementation;
- `tests/fixtures/indy-init/`;
- experiment results/evidence;
- PRs, branches, commits, logs, screenshots, or artifacts revealing another implementation;
- any input not explicitly allowed above.

Do not run Percepta verification or the experimental evaluator before first-pass freeze.

## Output

Write exactly:
- `research/experiments/EX-PERCEPTA-2026-0003/lanes/treatment-b/index.html`
- `research/experiments/EX-PERCEPTA-2026-0003/lanes/treatment-b/NOTES.md`

Do not modify shared files.

Implement the frozen product brief using the frozen runtime protocol. Neutral evaluator hooks in the brief must correspond to real visible/user-operable meaning, never hidden pass markers.

Once ordinary local checks are complete, commit the two outputs and STOP. Do not correct the implementation from evaluator feedback.
