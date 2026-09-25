# EX-PERCEPTA-2026-0003 — frozen first-pass evaluation

Measurement only. No implementation was edited, corrected, merged, or re-run after modification.

- Frozen baseline: `ca2fdd3c5657a9617ebc5dff854fc0f362e07ced` ("EX-PERCEPTA-2026-0003 register replication")
- Evaluation branch: `experiment/percepta-0003-evaluation` (created from the frozen baseline; no implementation branch merged)
- Evaluator: repository Percepta verifier (`src/Percepta.Cli` + `src/Percepta.Core` + `src/Percepta.Adapter.Playwright` at the baseline commit), `verify` command
- Contract: `.percepta/contracts/indy-init-investigation-workspace.json` (blob `integrity.json` → `inputs.canonicalContract`)
- Fixtures: `tests/fixtures/indy-init/blocked.json`, `tests/fixtures/indy-init/legal.json` (unchanged)

## 1. Integrity (recorded before any evaluation)

All four lanes passed. Full record: `integrity.json`.

| Lane branch | HEAD | ahead/behind baseline | Files changed | index.html blob | NOTES.md blob | Result |
|---|---|---|---|---|---|---|
| experiment/percepta-0003-openai-control-a | `32a4c422` | 1 / 0 | A `lanes/control-a/{index.html,NOTES.md}` | `e4b3eb41` | see integrity.json | pass |
| experiment/percepta-0003-openai-treatment-a | `bab9f626` | 1 / 0 | A `lanes/treatment-a/{index.html,NOTES.md}` | `737bed94` | see integrity.json | pass |
| experiment/percepta-0003-claude-control-b | `6b9c1d66` | 1 / 0 | A `lanes/control-b/{index.html,NOTES.md}` | `e472b979` | see integrity.json | pass |
| experiment/percepta-0003-claude-treatment-b | `c33098c6` | 1 / 0 | A `lanes/treatment-b/{index.html,NOTES.md}` | `63c05e3e` | see integrity.json | pass |

For each lane, the parent commit is the baseline. The contract, both fixtures, the runtime protocol, the product brief and the preregistration are blob-identical to the baseline.

## 2. Procedure

1. Opaque IDs `candidate-01..04` were assigned by a random shuffle. The key was sealed in scratch (`candidate-mapping.json` is its copy, committed only after unblinding).
2. Each frozen `index.html` was staged byte-for-byte from its lane commit (`git show <head>:<path>`) to `$SCRATCH/staging/<candidate>/index.html`, outside the repository and outside every lane. The SHA-256 was recorded before and after evaluation, and all 8 checks matched the source (`<candidate>/run.json`).
3. Every candidate ran the same command, with the same build, browser, contract and fixtures. The working directory was the repository root at the frozen baseline:
   `dotnet Percepta.Cli.dll verify --contract .percepta/contracts/indy-init-investigation-workspace.json --url <staged index.html> --fixture tests/fixtures/indy-init/blocked.json --fixture tests/fixtures/indy-init/legal.json --out artifacts/percepta/experiment-0003/<candidate>/evidence.json --screenshots artifacts/percepta/experiment-0003/<candidate>/screenshots`
4. stdout, stderr, exit code, the evidence JSON and screenshots were preserved per candidate. The raw evidence was then frozen: files made read-only and hashed into `raw-evidence.SHA256SUMS`, whose SHA-256 is `1ca16e2c3ecda82f878569d299c3ca2bab82b6332854e9a3b00d5215f177a5d3`. This happened before the blinded review and before unblinding.
5. Blinded composition review: see §5.
6. Unblinding and aggregation (`tools/aggregate.py`).

## 3. Deterministic results (raw, per candidate)

Required categories come from the contract: contract-validation, structural, state-projection, interaction, accessibility and responsive. Optional categories: visual-regression and semantic-visual-review.

| Candidate | Exit | complete | Required passed | contract-validation | structural | state-projection | interaction | accessibility | responsive | visual-regression (opt) | semantic-visual-review (opt) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| candidate-01 | 0 | true | 6/6 | Passed | Passed | Passed | Passed | Passed | Passed | Unavailable | Unavailable |
| candidate-02 | 0 | true | 6/6 | Passed | Passed | Passed | Passed | Passed | Passed | Unavailable | Unavailable |
| candidate-03 | 0 | true | 6/6 | Passed | Passed | Passed | Passed | Passed | Passed | Unavailable | Unavailable |
| candidate-04 | 0 | true | 6/6 | Passed | Passed | Passed | Passed | Passed | Passed | Unavailable | Unavailable |

- No required category was Failed or Unavailable. There are no failed observations or assertions to report.
- Responsive: all four viewports (390x844, 820x1180, 1180x820, 1440x900) were evaluated for every candidate. No horizontal-overflow or region-discoverability failure was emitted.
- Optional visual-regression was `Unavailable` for every candidate, with the reason "No baseline exists for …", because the repository has no `.percepta/baselines`. Optional semantic-visual-review was `Unavailable` because no `--semantic-review` input was supplied to the verifier. Under the canonical contract (`required: false`), neither affects `complete`. Neither is treated as Passed.

## 4. Aggregate (after unblinding)

Candidate mapping: candidate-01 = OpenAI treatment A; candidate-02 = OpenAI control A; candidate-03 = Claude control B; candidate-04 = Claude treatment B.

| Condition | Completed lanes | Whole-lane completion rate | Required categories passed / evaluated | Failures by category |
|---|---|---|---|---|
| Control | 2/2 | 1.00 | 12/12 | none |
| Treatment | 2/2 | 1.00 | 12/12 | none |

Paired descriptive results:
- OpenAI: control A (candidate-02) was complete with 6/6; treatment A (candidate-01) was complete with 6/6. No difference.
- Claude: control B (candidate-03) was complete with 6/6; treatment B (candidate-04) was complete with 6/6. No difference.

These results do not support any provider comparison. There was one session per provider per condition.

Churn: each lane added exactly 2 files and deleted no lines (`secondary-outcomes.json` → `changedFilesAndChurn`). There were 0 corrective iterations.

## 5. Blinded composition-diversity review

Trigger: the prerequisite of at least two treatment implementations passing all required evidence was met, because all four candidates were complete.

Reviewer inputs: 16 full-page screenshots (4 candidates × 4 viewports, captured by the verifier in the first-fixture/blocked state). The candidates were relabeled a second time, at random, as `Screen-W..Z` (`semantic-review/review-relabel-key.json`). The reviewer got all four candidates, so it could not tell which pair was the treatment pair. It did not receive the provider, condition, branch, deterministic results, NOTES.md or source. It was asked only whether compositions differ materially while preserving the required semantics, and not to rank aesthetics. Raw output: `semantic-review/blinded-composition-review.json` (SHA-256 `99ea7a76a877f3feee3aecea4d4bf2ddad37f567d075cf2e5effc93d0b4c0931`).

Pair verdicts: W–X different; W–Y different; W–Z different; **X–Y not materially different**; X–Z different; Y–Z different.

After unblinding: X = candidate-04 (Claude treatment B) and Y = candidate-01 (OpenAI treatment A). The **treatment pair is the only pair judged not materially different.** The reviewer flagged this pair as borderline: both use the same 2-column main + sidebar topology at desktop and landscape, with the same main-column sequence. The differences are in portrait/phone ordering, sidebar order and per-hypothesis actions. The reviewer judged all four candidates to have the required regions visibly present.

## 6. Hypothesis interpretation (preregistered rules applied exactly)

- **HY-PERCEPTA-2026-0003: no replication support.** Treatment whole-lane first-pass completion (2/2 = 1.00) is not greater than control (2/2 = 1.00). Control equals treatment, which is the preregistered falsification condition for this replication. The result is descriptive only (n = 2 per condition) and not statistically conclusive.
- **HY-PERCEPTA-2026-0004: not supported.** The first condition was met: 2 treatment implementations pass all required evidence. The second was not: the blinded review did not find materially different composition for the treatment pair. The reviewer called the pair borderline, and "a stricter reader could call X-Y materially different". Per the preregistered threshold, that note is recorded but does not convert the verdict into support.

## 7. Cross-experiment comparison with EX-PERCEPTA-2026-0002 (interpretation, not primary measurement)

The EX-0002 outcome is taken as recorded in the EX-0003 preregistration's motivation section: control 0/2, treatment 1/2. EX-0002 records were not modified.

Did equalizing the runtime protocol change the treatment/control difference? Descriptively, yes. The EX-0002 difference (+1 lane for treatment) disappeared in EX-0003 (0 difference) once every lane got the same explicit runtime protocol. Both conditions reached the ceiling.

The EX-0003 preregistered falsification clause says that in this case the EX-0002 result "is consistent with runtime-information asymmetry rather than a demonstrated semantic-contract effect." Caveats:
- n = 2 per condition.
- The sessions and model versions differ between experiments.
- The shared runtime protocol plus the neutral hook list in the product brief may have left no headroom for the contract to add measurable value on this evaluator. The preregistration names this threat.
- The deterministic verifier checks hook-level observations. A deterministic pass does not establish that the product meaning is correct (compare HY-PERCEPTA-2026-0002). No semantic-meaning audit beyond the preregistered composition review was performed.

## 8. Deviations and threats (all disclosed)

1. **.NET SDK unavailable or substituted.** The container had no `dotnet`, and Microsoft download hosts (`builds.dotnet.microsoft.com`, `dotnetcli.*`) were blocked by network policy. I installed the Ubuntu archive packages `dotnet-sdk-10.0` 10.0.112 and `dotnet-runtime-8.0` 8.0.31. `global.json` pins 10.0.401 with `rollForward: latestPatch`, which 10.0.112 cannot satisfy. I built from a working directory outside the repository so `global.json` was not consulted. The file was not modified. The build succeeded with 0 warnings under `TreatWarningsAsErrors`.
2. **Invocation form.** Instead of `dotnet run --project … --configuration Release -- verify …`, I built the same project once in Release (`--artifacts-path` in scratch, so no `bin/`/`obj/` in the repo) and invoked `dotnet Percepta.Cli.dll verify …` with the repository root as the working directory. The CLI arguments and semantics are unchanged. Assembly hashes are in `environment.json`.
3. **Browser substituted.** Microsoft.Playwright 1.63.0 expects chromium-headless-shell revision 1243 (Chrome for Testing 153.0.8010.12). The repository's `install-browser` command was attempted and failed: the download was blocked. I used the preinstalled revision 1194 (Chromium 141.0.7390.37) through a scratch `PLAYWRIGHT_BROWSERS_PATH` symlink tree. It was applied identically to all four candidates. Rendering differences between Chromium 141 and 153 are a threat to exact reproducibility, not to cross-lane consistency. A transient convenience symlink was briefly created inside `/opt/pw-browsers` and removed before any evaluation. It had no effect on results.
4. **Staging location.** EX-0002 passed repo-relative lane paths. Here, byte-identical copies were staged at absolute scratch paths outside the repository and served through the verifier's built-in `file://` normalization, the same mechanism EX-0002 used. Hashes were verified before and after each run.
5. **`--screenshots` supplied.** This is an existing CLI option, used to write screenshots per candidate instead of the shared default `.percepta/evidence/…`. The check semantics are unchanged.
6. **Verifier side effect.** The verifier created an empty `.percepta/baselines/investigation-workspace/` directory, because it always creates the baseline directory. It was empty and untracked, and I removed it after the runs. `--update-baselines` was not used, so visual-regression is `Unavailable` for all four candidates.
7. **Gitignore.** `artifacts/percepta/` is gitignored at the baseline. The evaluation artifacts were force-added (`git add -f`) instead of changing `.gitignore`.
8. **Reviewer independence.** The blinded composition reviewer was an AI subagent spawned by this evaluation session, not a human. It saw only the relabeled screenshots. The evaluation session itself generated the candidate mapping, but it did not pass the mapping to the reviewer. The reviewer is the same vendor family as the Claude lanes. Screenshots show only the blocked-fixture state.
9. **Branch naming.** Artifacts are committed on `experiment/percepta-0003-evaluation` as instructed. The same commit is also pushed to this session's designated branch, `claude/percepta-0003-evaluation-j0x7s7`. Neither branch is merged.
10. **Manual intervention.** None on implementations, contract, fixtures or evaluator source. No evidence was edited. Aggregation reads the frozen evidence only.

## 9. Files

- `integrity.json`: identities, blob SHAs and branch checks, recorded before evaluation
- `candidate-mapping.json`: candidate ↔ branch key, committed after unblinding
- `environment.json`: runtime, SDK, browser and assembly hashes
- `candidate-0N/{evidence.json,stdout.txt,stderr.txt,exit-code.txt,run.json,screenshots/*.png}`: raw evidence
- `raw-evidence.SHA256SUMS` (+ `.sha256`, `raw-evidence.frozen-at.txt`): frozen raw-evidence manifest
- `semantic-review/`: blinded review output and relabel key
- `results.json`, `secondary-outcomes.json`, `summary.tsv`: aggregates
- `tools/`: the scripts used (`integrity.py`, `evaluate.py`, `aggregate.py`)
- `SHA256SUMS`: hashes of every file above, including aggregates
