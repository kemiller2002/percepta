---
id: DF-PERCEPTA-2026-0001
title: Future Percepta experiments record sealed run provenance outside every lane and reviewer view
research_area: percepta-verification
status: accepted
created: 2026-09-26
decision_type: architecture
related_documents:
  - requirements/PERCEPTA-CONTRACTS.md
  - schemas/percepta-experiment-run.schema.json
  - docs/experiment-run-provenance.md
supersedes: []
superseded_by: []
tags:
  - provenance
  - experiments
  - blinding
confidence: medium
---

# Decision

Experiments preregistered after this decision record, for each lane, evaluator, and reviewer run, **who ran it and in which execution**. They record it as one sealed run record (`PCT-036`, `schemas/percepta-experiment-run.schema.json`) at `artifacts/percepta/experiment-NNNN/runs/<run>.json`.

The record embeds the Praxis provenance interchange record unchanged: a Praxis actor keyed by an execution. Percepta takes no dependency on Praxis code. `scripts/experiment-provenance/` implements a small local codec, and `npm run test:provenance` runs the vendored Praxis conformance fixtures through it.

The upstream contract is not restated here. See Praxis `RQ-ROS-2026-A010` (provenance is not attestation), `RQ-ROS-2026-A013` (interchange record), `RQ-ROS-2026-A014` (execution propagation), `RQ-ROS-2026-A015` (no silent stripping), and `DF-ROS-2026-A037`.

## Context

- EX-PERCEPTA-2026-0002 and 0003 compared providers and conditions. Their integrity manifests carry provider labels in free-form fields, and their lanes were blinded by instruction only.
- EX-PERCEPTA-2026-0004 is preregistered. Three of its lanes are still pending and branch from preregistration commit `f474c449`. Anything that reaches root agent instructions, lane inputs, or the evaluator could contaminate them.
- A telemetry execution record in this repository has already misattributed identity from a stale environment. Identity is self-reported and fallible, which is why it must never become evidence.

## Rules

1. **No backfill.** EX-PERCEPTA-2026-0001 through 0004 are not retrofitted. Their artifacts, integrity manifests, lanes, and preregistrations stay byte-identical. Nothing is inferred for them from git authorship, `author_agent`, branch names, or telemetry. EX-0004 is not required to produce run records.
2. **What is recorded.** Each run record holds one contribution. Its key is the run's execution: `ROS_EXECUTION_ID` when Praxis propagated one, otherwise `EXE-percepta.<run>`. Percepta never mints a Praxis-shaped `EXE-<timestamp>-<random>`. Its actor comes only from declared, whitelisted, non-secret identity. Anything not known is recorded as `"unknown"`.
3. **Where it lives.** Run records live only in evaluator-side artifacts under `artifacts/percepta/experiment-NNNN/runs/`. They never appear in:
   - lane-visible inputs (lane `AGENTS.md`, briefs, protocols, held-out cases);
   - lane outputs;
   - reviewer bundles (`candidate-NN/`, `semantic-review/`);
   - root agent instructions.
4. **When it is written.** A run record is written only after the first-pass freeze. It is sealed until the deterministic evidence has been hashed. It names that hashed manifest (`sealing.deterministicEvidenceManifest`), which never lists run records. So the evidence hash set cannot depend on identity.
5. **Identity is not evidence.** Nothing in `src/**` reads run records. Identity never changes evidence status, completion, scoring, or acceptance. Provider-stratified comparisons are secondary descriptive outcomes, computed after unsealing, and they never alter the primary outcome.
6. **Reviewers stay blind.** Reviewers never receive provider, model, execution, or condition. Run records are unsealed only after every blinded review has been recorded.
7. **Reject, do not drop.** Malformed run provenance fails `npm run test:provenance` and is never silently dropped. If a record uses a Praxis major version this codec does not support, the run-record schema rejects it until the codec is upgraded.

## Alternatives considered

- **Backfill EX-0002/0003 from integrity labels or telemetry.** Rejected: that would invent history. It would also rewrite hashed artifacts, and the known misattributed telemetry record shows such sources are unreliable.
- **Put identity in lane `NOTES.md` or evidence JSON.** Rejected: it would reach lanes, reviewers, or the evaluator, which breaks blinding and lets identity bias evidence.
- **Add a `provenance` block to `ros.json` now.** Deferred: ROS `requiredFrom` semantics could change validation for lane branches already in flight. Revisit after EX-0004 completes.
- **Edit `templates/research/EXPERIMENT-TEMPLATE.md`.** Rejected: that file is ROS tool-owned and is replaced on upgrade. The guidance lives in `docs/experiment-run-provenance.md` instead.

## Consequences

- Future provider-comparative experiments can attribute runs without weakening blinding.
- The leak guard and the frozen-hash checks in `npm run test:provenance` run for every experiment, not just future ones. While EX-0004 is pending, they also pin the lane-visible inputs to their preregistration blobs.
- Reversible: removing the convention deletes only the files it added, and no frozen artifact depends on it.

## Follow-up validation

- The first experiment that adopts this convention should confirm that a reviewer bundle contains no run provenance before unsealing. The leak guard enforces this automatically.
- After EX-0004 concludes, decide whether to add a `ros.json` provenance block.
