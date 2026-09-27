# EX-PERCEPTA-2026-0004 evaluation protocol (fixed before any candidate was run)

This file operationalizes the parts of the preregistration
(`research/experiments/EX-PERCEPTA-2026-0004--held-out-semantic-generalization.md` at
`f474c449c2bc84da5707bb123c36500490875869`) that the preregistration leaves unspecified.
It was written and hashed after the frozen-candidate gate (`integrity.json`) passed and after the
held-out cases were read. It was written **before** any candidate was staged, rendered, or verified.
No frozen registered semantic-review procedure or evaluator for EX-0004 exists at the baseline. These rules are
therefore the evaluator's own. They are disclosed as such and are not changed after candidate output is observed.

## Inputs

- Candidates: the four frozen commits in `integrity.json`. Each candidate's `index.html` is staged
  byte-for-byte from `git show <frozenCommit>:<lane>/index.html` into scratch outside the repository.
  Hashes are checked before and after every run.
- `experiment/percepta-0004-openai-control-b` is excluded from every step.
- Held-out states: the five `state` objects in `held-out-semantic-cases.json` (blob `b702a376…`).
  They are written verbatim, one JSON file per case, to `held-out-fixtures/<case-id>.json`, and then supplied unchanged.
  Cases, obligations, and states are not edited, added, or removed.

## Blinding

- Opaque IDs `candidate-1..4` are assigned by a cryptographically random shuffle. The key is sealed in
  scratch and committed as `candidate-mapping.json` only after all semantic judgments are frozen.
- Reviewers never receive provider, condition, branch, SHA, NOTES.md, source, deterministic results,
  `data-percepta-*` attribute values (other than locating the one confirm control), or another reviewer's output.

## Layer 1: deterministic structural/runtime checks (identical for every candidate)

Evaluator: the repository Percepta verifier built from the baseline commit's source (`src/Percepta.Cli`),
run with the baseline worktree as the working directory and the canonical contract
`.percepta/contracts/indy-init-investigation-workspace.json`.

- **D-combined**: one run per candidate with all five held-out fixtures, in registered order (01..05).
  This run is the source of "deterministic required-category completion". The required categories come from
  the contract's `evidenceRequirements`: contract-validation, structural, state-projection, interaction,
  accessibility, and responsive.
- **D-per-case**: one run per candidate per held-out case, with only that case's fixture. When a category is
  `Unavailable` because that single fixture does not cover a predicate or capability legality, the reason is
  fixture coverage. It is recorded, and it is not scored as a candidate failure. `Failed` is always recorded as it is.
- **Infrastructure positive control**: before any candidate run, the same build is run with the same held-out fixtures
  against the repository reference page `tests/fixtures/indy-init/index.html`. If the verifier cannot run there,
  the candidate evaluation is stopped as an infrastructure failure.
- A candidate's deterministic failure is any `Failed` category, or a crash or timeout that the positive control shows
  the infrastructure does not have.

Deterministic results never substitute for semantic judgments, and they are not shown to reviewers.

## Layer 2: rendered evidence capture (generic, identical for every candidate)

A Node Playwright script (`tools/capture.mjs`) does the following for each candidate × case, on a fresh page:
load the staged page, wait for load, and call `window.__perceptaSetState(state)` with the verbatim held-out state.
It then captures:

1. Full-page screenshots at 1440x900 (desktop-reference), 1180x820, 820x1180, and 390x844. It also captures
   viewport-sized tiles of the desktop page, for legibility.
2. The Playwright ARIA snapshot of `body` at each viewport (the accessibility tree: roles, names, states, visible text).
3. An inventory of the visible interactive controls: role/tag, accessible name, disabled/aria-disabled, and the
   aria-describedby text.
4. The control carrying `data-percepta-capability="confirm-root-cause"` (the brief's neutral hook). The capture
   records its visible text, accessible name, disabled/aria-disabled, aria-describedby text, and the visible text
   of its nearest enclosing section or region.
5. Operability probes, each on a fresh page in the same state. (a) Every visible in-page link (`href^="#"`) is
   activated. The capture records the target element's text excerpt, its nearest heading, and whether it is
   visible in the viewport afterwards. (b) The confirm control is clicked, and the capture records whether the
   click was possible and the ARIA-snapshot difference afterwards. It never forces a click on a disabled control.
6. A page error / console error log.

The capture only reads the page. It never mutates the candidate beyond the protocol call and the user-equivalent
clicks listed above. If capture fails for infrastructure reasons, the failure is recorded, and that item is not
reviewed or scored.

## Layer 3: blinded semantic review

- One **review item** is one candidate × one held-out case (20 items). Each review is done in a fresh, isolated
  reviewer session. That session receives only that item's evidence packet (screenshots, ARIA snapshots, control
  inventory, probe results), that single state object, and that case's obligations, verbatim.
- Two independent AI reviewers review every item: **R1** and **R2**, run on different model sizes. A reviewer never
  sees another reviewer's output. Individual judgments are stored unmodified.
- For each obligation, the reviewer returns `pass` or `fail`, the direct visible or operable evidence, and, when the
  result is `fail`, a failure type: `missing`, `ambiguous`, `fabricated`, `contradictory`, `inaccessible-operation`,
  or `not-preserved`.
- An obligation is judged at desktop-reference (1440x900), with the operability probes. A `pass` requires direct
  visible or operable evidence. Intent, source, and "the user could infer it" earn no credit.
- Responsive secondary: for each obligation, the reviewer also judges whether it remains preserved at 1180x820,
  820x1180, and 390x844, using that viewport's screenshot and ARIA snapshot.
- The reviewer also lists any fabricated or forbidden meaning observed in the state. Examples: numerical confidence
  not supplied by the domain, claimed remote success, an invented blocker or domain reason, state conveyed by color
  alone, a deleted falsified hypothesis, or competing hypotheses linearized into one.

## Scoring (fixed now; not changed after unblinding)

- **Obligation outcome (primary scoring)**: an obligation is Passed only if **both** R1 and R2 judge it `pass`.
  Any other combination is Failed. A disagreement means the evidence was not unambiguously direct, which the
  preregistration scores as Failed ("ambiguous"). Every disagreement is recorded and reported.
- **Case fully correct**: every obligation of the case is Passed.
- **Primary outcome per candidate**: the number of fully correct cases, out of 5.
- **Per condition**: the sum of fully correct cases, divided by 10 (2 candidates × 5 cases).
- **HY-PERCEPTA-2026-0005 descriptive support** if and only if the treatment rate is greater than the control rate.
- **Sensitivity (secondary, never replaces primary)**: the primary outcome recomputed from R1 alone and from R2 alone.
- Secondary outcomes follow the preregistration: obligations passed/evaluated; failures by obligation; deterministic
  required-category completion; provider-stratified comparisons; fabricated or forbidden meaning defects (obligation
  failures typed `fabricated`/`contradictory`, plus reviewer-reported defects); responsive preservation (obligations
  that Passed at desktop and remain preserved at all three other viewports under both reviewers); and implementation
  churn (commits, files, and lines from the git history between baseline and frozen commit).

## Freezing

Raw evidence (deterministic outputs, capture packets, and individual reviewer judgments) is hashed into
`raw-evidence.SHA256SUMS` before unblinding. Aggregation reads only frozen raw files.
