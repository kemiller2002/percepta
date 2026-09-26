# Experiment provenance and blinding checklist (Percepta addendum)

Percepta-owned addendum to the ROS-managed `EXPERIMENT-TEMPLATE.md`, which
this repository must not edit (it is pinned by `.ros/installation.json`). Copy
the section below into every experiment record registered after 2026-09-26,
after `## Controls`.

Requirements: `requirements/PERCEPTA-EXPERIMENT-PROVENANCE.md` (PCT-036..PCT-043).

For new records:

- Leave the template's free-text `author_agent: replace-me` out of the record.
  It is a legacy, self-declared field (Praxis reports it as `legacy-declared`).
- Record the record's authorship as Praxis provenance instead:
  `ros provenance record --path research/experiments/<EX-ID>--<slug>.md --operation created --execution <EXE-...> --reason "..."`
  (requires a ROS release with `ros provenance`), unless the protocol shows the
  record to lanes or reviewers; then record authorship in the sealed ledger.
- Keep evaluator-only inputs (held-out cases, blinding key) in an
  `evaluation_only_inputs` front-matter list, stored under `sealed/` or
  `evaluator/` of the bundle.

```markdown
## Provenance and blinding

- [ ] Record visibility: this record is / is not shown to lanes or reviewers.
- [ ] Experimental identity variable (provider, model, runtime, or none):
- [ ] Execution bundle at `experiments/<EX-ID>/`; lane directories use neutral labels (`lane-a`, `lane-b`, ...).
- [ ] Blinding key and provenance ledger live only under `experiments/<EX-ID>/sealed/`.
- [ ] Lane `AGENTS.md`, briefs, prompts, frozen inputs, candidate outputs, and review packets carry no provenance block, execution key, actor variable, or provider/model/runtime name.
- [ ] Each lane, review, and evaluation execution is recorded in the sealed ledger as a Praxis contribution (explicit actor declaration; `unknown` when unknown).
- [ ] Self-identifying candidate output is stripped before review and the stripping is recorded as a `transformed` contribution.
- [ ] Provenance recorded at freeze time is listed with its SHA-256 in the freeze manifest.
- [ ] Unblinding happens only after blinded reviews are frozen.
- [ ] Actor identity is analysed only as the preregistered variable, never as evidence quality.
- [ ] `npm run test:core` passes (experiment leakage check).
```
