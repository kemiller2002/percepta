# Experiment run provenance (sealed)

Requirement `PCT-036` and decision `DF-PERCEPTA-2026-0001` cover this convention. It applies only to experiments preregistered after that decision. EX-PERCEPTA-2026-0001 through 0004 are not backfilled.

`templates/research/EXPERIMENT-TEMPLATE.md` is owned by ROS and is replaced on upgrade. So this guidance lives here. When you write a new experiment, copy the optional section below into its preregistration. Never copy it into a lane brief or lane `AGENTS.md`.

## Optional preregistration section

```markdown
## Run provenance (sealed)

- Each lane, evaluator, and reviewer run is recorded after the first-pass freeze
  as `artifacts/percepta/experiment-NNNN/runs/<run>.json`
  (`schemas/percepta-experiment-run.schema.json`, PCT-036).
- A run record is written only after the deterministic evidence manifest
  (`SHA256SUMS` or `integrity.json`) has been hashed. It names that manifest,
  and it stays sealed until every blinded review has been recorded.
- Lanes, lane outputs, and reviewer bundles never contain run provenance.
  Reviewers never learn provider, model, execution, or condition.
- Identity never changes evidence status, completion, or scoring.
  Provider-stratified results are secondary descriptive outcomes, computed
  after unsealing.
```

## Writing a run record

Only the evaluator writes run records, after the freeze. Use `scripts/experiment-provenance/experiment-run.mjs`:

```js
import { makeRunRecord } from './scripts/experiment-provenance/experiment-run.mjs';
import { actorFromEnvironment } from './scripts/experiment-provenance/praxis-provenance-record.mjs';

const record = makeRunRecord({
  experiment: 'EX-PERCEPTA-2026-0005',
  run: 'lane-control-a',
  role: 'lane',
  env: laneEnvironment,            // only ROS_EXECUTION_ID is read from it
  at: '2026-10-01T12:00:00.000Z',  // when the run started
  actor: actorFromEnvironment(laneDeclaredIdentity),
  firstPassFreezeCommit: '<40-hex freeze commit>',
  manifest: { path: 'artifacts/percepta/experiment-0005/SHA256SUMS', sha256: '<digest>' }
});
```

- **Execution.** The run is keyed by the Praxis execution when `ROS_EXECUTION_ID` was propagated to it. Otherwise it is keyed `EXE-percepta.<run>`.
- **Actor.** The actor comes from the run's declared identity only: `ROS_ACTOR_KIND`, `ROS_ACTOR`, `ROS_TELEMETRY_PROVIDER`, `ROS_TELEMETRY_MODEL`, and `ROS_TELEMETRY_RUNTIME`.
  - Anything undeclared is recorded as `unknown`.
  - GitHub Actions with nothing declared resolves to the `github/github-actions` automation actor.
  - Never copy identity from another run, from git authorship, or from the evaluator's own environment.
  - Never record secrets.
- **Validation.** `npm run test:provenance` validates every run record. It also checks:
  - that lane-visible and reviewer-visible files carry no structured provenance, such as execution IDs, actor fields, or model identifiers;
  - that frozen experiment digests still match.
