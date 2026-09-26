# Experiment execution bundles

Execution prompts, frozen briefs, lane instructions, and other supporting experiment material live here.

Governed ROS research artifacts remain in `research/experiments/` and must satisfy the Praxis artifact contract. Keeping execution support files outside that artifact namespace prevents ordinary Markdown support documents from being interpreted as research records.

## Provenance and blinding (experiments registered after 2026-09-26)

Future bundles follow `requirements/PERCEPTA-EXPERIMENT-PROVENANCE.md` (PCT-036..PCT-043):

- `sealed/` and `evaluator/` are evaluator-only. The blinding key and the Praxis provenance ledger (`sealed/provenance-ledger.json`) live there.
- Everything else in a bundle is lane-visible or reviewer-visible and carries no provenance block, execution key, actor variable, or provider/model/runtime name. Lane directories use neutral labels.
- `npm run test:core` scans future bundles for such leakage. Existing bundles (EX-PERCEPTA-2026-0001..0004) are frozen, excluded by name, and never opened by the check.
