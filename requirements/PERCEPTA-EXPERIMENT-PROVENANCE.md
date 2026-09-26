---
id: PERCEPTA-EXPERIMENT-PROVENANCE
title: Provenance and blinding for future Percepta experiments
status: accepted
created: 2026-09-26
updated: 2026-09-26
requirements: [PCT-036, PCT-037, PCT-038, PCT-039, PCT-040, PCT-041, PCT-042, PCT-043]
related_documents:
  - requirements/PERCEPTA-CONTRACTS.md
  - templates/research/EXPERIMENT-PROVENANCE-CHECKLIST.md
  - experiments/README.md
  - "praxis:DF-ROS-2026-A036"
  - "praxis:DF-ROS-2026-A037"
  - "praxis:RQ-ROS-2026-A001"
  - "praxis:RQ-ROS-2026-A002"
  - "praxis:RQ-ROS-2026-A004"
  - "praxis:RQ-ROS-2026-A010"
  - "praxis:RQ-ROS-2026-A013"
  - "praxis:RQ-ROS-2026-A015"
  - "praxis:RQ-ROS-2026-A016"
  - "praxis:RQ-ROS-2026-A017"
  - "praxis:RQ-ROS-2026-A019"
---

# Percepta experiment provenance requirements

## Purpose

Percepta experiments compare independent implementation lanes, and the provider,
model, or runtime of a lane is often the experimental variable itself. Recording
who ran a lane is valuable provenance; showing it to a lane or to a blinded
reviewer would break the protocol.

These requirements apply Praxis agent provenance (DF-ROS-2026-A036,
DF-ROS-2026-A037, RQ-ROS-2026-A001..A019) to experiments **registered after
2026-09-26**. They do not redefine the Praxis actor, execution, contribution,
or interchange model; they decide only *where* that provenance may live inside a
Percepta experiment. Experiments EX-PERCEPTA-2026-0001 through
EX-PERCEPTA-2026-0004 are out of scope and are never modified (PCT-041).

## Bundle visibility layout (future experiments)

An experiment execution bundle is the directory `experiments/<EX-ID>/`
(see `experiments/README.md`). Its paths have exactly one of three audiences:

| Path inside the bundle | Audience |
|---|---|
| `sealed/**` | evaluator only: blinding key (lane-to-candidate mapping), provenance ledger, held-out cases |
| `evaluator/**` | evaluator only: evaluator implementation and unblinded analysis inputs |
| everything else (`frozen-*.md`, `briefs/**`, `prompts/**`, `lanes/<lane>/**` including `AGENTS.md` and candidate outputs, `review/**` anonymized review packets, bundle `README.md`) | lane-visible or reviewer-visible |

Anything not under `sealed/` or `evaluator/` is treated as visible. A protocol
that needs another evaluator-only location records it in the experiment record
and moves it under `sealed/` or `evaluator/` before freezing.

## PCT-036 Execution provenance for experiments

For every future experiment, the evaluator SHALL record which actor and which
execution ran each lane, each blinded review, and the evaluation, using the
Praxis actor and contribution model (RQ-ROS-2026-A001, RQ-ROS-2026-A002,
RQ-ROS-2026-A004) in the `praxis.provenance/1` interchange shape
(RQ-ROS-2026-A015).

Execution keys SHALL be Praxis `EXE-...` ids or `EXT-<system>.<run-id>` keys
(RQ-ROS-2026-A013). Actor values SHALL come only from explicit declarations
(execution envelope, CLI flags, `ROS_ACTOR*`, `ROS_TELEMETRY_*`,
`ROS_EXECUTION_ID`, or `ros provenance identity`) (RQ-ROS-2026-A016). Unknown
values SHALL be recorded as `unknown`, never guessed. No credentials SHALL be
recorded (RQ-ROS-2026-A017).

## PCT-037 Sealed provenance ledger

Execution provenance SHALL be stored in an evaluator-only ledger at
`experiments/<EX-ID>/sealed/provenance-ledger.json`, beside the sealed blinding
key. The ledger maps neutral lane and review labels to Praxis contributions.

The ledger SHALL NOT be copied, summarized, or referenced by path in any
lane-visible or reviewer-visible material.

## PCT-038 No identity in lane-visible or reviewer-visible material

Lane-visible and reviewer-visible material SHALL NOT contain provenance or
identity metadata. This covers lane `AGENTS.md` files, briefs, prompts, frozen
product briefs and runtime protocols, candidate outputs, and anonymized review
packets. In particular it SHALL NOT contain:

- a front-matter or JSON `provenance` block, a `praxis.provenance/...` schema tag,
  or legacy free-text author fields (`author_agent`, `created_by_agent`,
  `owner_agent`, `source_author`);
- Praxis execution or contributor keys (`EXE-...`, `EXT-...`, `CTB-...`);
- actor environment variable names (`ROS_ACTOR`, `ROS_ACTOR_KIND`,
  `ROS_TELEMETRY_PROVIDER`, `ROS_TELEMETRY_MODEL`, `ROS_TELEMETRY_RUNTIME`,
  `ROS_EXECUTION_ID`);
- provider, model, or runtime names in file content or in path names
  (for example lane directories SHALL use neutral labels such as `lane-a`,
  not `claude-treatment`).

Candidate outputs that self-identify (for example a generated comment naming
the model) SHALL be stripped of that identification by the evaluator before a
review packet is built, and the stripping SHALL be recorded in the sealed
ledger as a `transformed` contribution.

## PCT-039 Identity is the variable, not evidence quality

When provider, model, or runtime is the experimental variable, it SHALL be
recorded only in sealed or evaluator material and revealed only after the
blinded reviews are frozen.

Recorded identity is self-reported provenance (RQ-ROS-2026-A010). It SHALL NOT
increase or decrease the weight, confidence, or acceptability of any lane's
evidence (RQ-ROS-2026-A019). Analysis MAY group results by the preregistered
variable; it SHALL NOT treat an actor's identity as a quality signal.

## PCT-040 Freeze manifest covers provenance

Provenance recorded at or before freeze time SHALL be listed with its SHA-256
digest in the experiment's freeze manifest (the bundle's `integrity.json` or
`SHA256SUMS`), exactly like any other frozen input.

Provenance recorded after a freeze (for example lane executions) SHALL be
appended as new ledger entries or a new ledger file that is hashed at its own
freeze point. A hashed file SHALL NOT be edited to add provenance.

## PCT-041 No retroactive change

Preregistrations, frozen inputs and outputs, sealed blinding keys, freeze
manifests, results, and the existing experiments EX-PERCEPTA-2026-0001 through
EX-PERCEPTA-2026-0004 (including `experiments/EX-PERCEPTA-2026-0002/**`,
`experiments/EX-PERCEPTA-2026-0003/**`,
`research/experiments/EX-PERCEPTA-2026-0004/**`, and `artifacts/percepta/**`)
SHALL NOT be altered to add, correct, or remove provenance.

Legacy identity data SHALL stay as recorded. In particular, historical events
whose actor claims id `claude` while naming provider `openai` and runtime
`codex`, and free-text `author_agent` values, are legacy self-declarations: they
are flagged as legacy and inconsistent where reported, never rewritten,
reconciled, or backfilled.

## PCT-042 Experiment record provenance

The experiment record (`research/experiments/<EX-ID>--<slug>.md`) is protocol
material, not lane-visible material, unless its protocol says otherwise. Its
authorship SHALL be recorded as Praxis front-matter provenance with
`ros provenance record` (RQ-ROS-2026-A004), not with free-text `author_agent`.

Each protocol SHALL state whether the record is shown to lanes or reviewers.
If it is, the record SHALL NOT carry front-matter provenance and its
authorship SHALL be recorded in the sealed ledger instead.

## PCT-043 Automated leakage check

The repository SHALL include an automated check that scans every future
experiment bundle's lane-visible and reviewer-visible paths for the identity
signals listed in PCT-038, and fails when it finds any.

The check SHALL exclude the frozen experiments listed in PCT-041 by directory
name before opening any file, so that it never reads frozen or held-out
material, and SHALL ignore `sealed/**` and `evaluator/**`.

## Repository policy and templates

Percepta's `ros.json` declares `rosVersion` 3.4.0. That ROS release predates
the Praxis provenance policy (`ros.json` `provenance` block) and
`ros provenance`, so declaring the policy would not be enforced by the pinned
toolchain. In addition, `ros.json` and `templates/research/*-TEMPLATE.md` are
ROS-managed files pinned by `.ros/installation.json`; editing them makes
`./ros verify` fail. Therefore:

- the policy block is deliberately not added until `rosVersion` is raised to a
  Praxis release that implements it; at that point it SHALL use a
  `requiredFrom` date after the upgrade so that every existing artifact stays
  legacy;
- the managed `EXPERIMENT-TEMPLATE.md` (which still carries a free-text
  `author_agent` placeholder) is not edited here. New experiment records follow
  the Percepta-owned addendum
  `templates/research/EXPERIMENT-PROVENANCE-CHECKLIST.md`, which omits
  `author_agent` in favour of `ros provenance record` and adds the
  provenance and blinding checklist. Removing the placeholder from the managed
  template is an upstream Praxis template change.

## Traceability

| Requirement | Praxis source | Implementation | Verification |
|---|---|---|---|
| PCT-036 | RQ-ROS-2026-A001, A002, A004, A013, A015, A016, A017 | `templates/research/EXPERIMENT-PROVENANCE-CHECKLIST.md` | experiment checklist; `ros provenance audit` on the sealed ledger's contributions |
| PCT-037 | DF-ROS-2026-A037 | bundle layout above; `templates/research/EXPERIMENT-PROVENANCE-CHECKLIST.md` | `ExperimentBlinding` treats `sealed/**` as evaluator-only (Percepta.Core.Tests) |
| PCT-038 | RQ-ROS-2026-A019, DF-ROS-2026-A037 | `tests/Percepta.Core.Tests/ExperimentBlinding.fs` | Percepta.Core.Tests: leakage checks on synthetic bundles and on future bundles on disk |
| PCT-039 | RQ-ROS-2026-A010, RQ-ROS-2026-A019 | `templates/research/EXPERIMENT-PROVENANCE-CHECKLIST.md` | protocol review |
| PCT-040 | DF-ROS-2026-A037 | `templates/research/EXPERIMENT-PROVENANCE-CHECKLIST.md` | freeze manifest review |
| PCT-041 | DF-ROS-2026-A036 (legacy data) | `ExperimentBlinding.frozenExperiments` | Percepta.Core.Tests: frozen bundles excluded before any file is opened; `git diff --stat` on frozen paths |
| PCT-042 | RQ-ROS-2026-A004, RQ-ROS-2026-A005 | `templates/research/EXPERIMENT-PROVENANCE-CHECKLIST.md` | `ros provenance record` / `ros validate` |
| PCT-043 | RQ-ROS-2026-A018 | `tests/Percepta.Core.Tests/ExperimentBlinding.fs`, `Program.fs` | `npm run test:core` |
