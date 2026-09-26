# EX-PERCEPTA-2026-0002 — frozen first-pass evaluation

Baseline / evaluator commit: `cb8b2f4324b9016773d4f5a6b97ec086d5b8d637`
Procedure: the `evaluate` job in `.github/workflows/percepta-experiment-0002.yml` (blob `2db3f642`), run locally with the same command and configuration for every lane. The only changes were to the toolchain; see "Threats to validity".
Evaluation window (UTC): see `integrity.json`.
Corrective iterations: 0. No implementation was modified and no lane was rerun.

## Lanes evaluated

| Lane | Provider | Condition | Commit evaluated | index.html blob | Exit | complete |
|---|---|---|---|---|---|---|
| control-a | openai | control | `c02ee0117a8f4e87deb639415f23d699fb1a28b2` | `05814717a8b2b3a6a5ed18199f6719be19c8ce20` | 3 | false |
| treatment-a | openai | treatment | `383ebf61f327c246639a55d8113d4974f6b32ad7` | `7bde239bb22eb903874b3b25598ad21aabcf6701` | 3 | false |
| control-b | claude | control | `1ced8888a1e11e43ee2025e2a2cd7144eaa788c6` | `dea205fe0493db838b499b0a8115cbfb1bfafe22` | 3 | false |
| treatment-b | claude | treatment | `3e8da52b8be6bbedd737c26ae8fc34b64aca0d78` (files unchanged since impl. commit `22026d3`) | `0e2b1a5a3290a9b6bdbf2c562d6bf0a4cfed3df1` | 0 | true |

All integrity checks passed:
- A, treatment-a and control-b are each one commit ahead of baseline, and each commit adds only that lane's `index.html` and `NOTES.md`.
- Treatment B's `index.html` and `NOTES.md` blobs match the verified frozen SHAs.
- Every staged copy hashes to its source blob, both before and after evaluation.

## Required evidence by lane

The contract requires six evidence categories (`*`). The two optional categories are Unavailable in every lane: visual-regression has no baseline, and semantic-visual-review was not supplied.

| Lane | contract-validation | structural | state-projection | interaction | accessibility | responsive | Required passing |
|---|---|---|---|---|---|---|---|
| control-a | Passed | Passed | **Failed** (11) | **Failed** (3) | Passed | Passed | 4/6 |
| treatment-a | Passed | Passed | **Failed** (11) | **Failed** (4) | Passed | Passed | 4/6 |
| control-b | Passed | Passed | **Failed** (8) | **Failed** (1) | Passed | Passed | 4/6 |
| treatment-b | Passed | Passed | Passed | Passed | Passed | Passed | 6/6 |

(n) = the number of individual problems the evaluator reported for that category. The full text is in `secondary-outcomes.json` and the raw `*.evidence.json`.

No required category was Unavailable in any lane.

## Primary outcome (preregistered)

- Control first-pass passes: **0 / 2** (0.0)
- Treatment first-pass passes: **1 / 2** (0.5)

**HY-PERCEPTA-2026-0003:** the preregistered support condition (treatment rate > control rate) **is met in this pilot**. With n = 2 per group this is descriptive only and is not statistically conclusive. The one-lane difference comes entirely from treatment-b (Claude). Treatment-a (OpenAI) failed the same two categories as both controls.

**HY-PERCEPTA-2026-0004:** only one treatment implementation (treatment-b) satisfies the required evidence. The hypothesis needs at least two, so it **cannot receive its preregistered support condition from this run**. No blinded composition review was prepared or performed.

## Observable changed files (implementation commit)

| Lane | Files | index.html lines | NOTES.md lines |
|---|---|---|---|
| control-a | 2 | 88 | 29 |
| treatment-a | 2 | 231 | 65 |
| control-b | 2 | 886 | 62 |
| treatment-b | 2 | 714 | 29 |

## Infrastructure vs implementation

There were no evaluation-infrastructure failures. All four runs completed and wrote evidence, and every stderr log is empty. The Failed results come from the evaluator's observation and capability checks against each page. The evaluator did not crash or report anything as missing. The harness itself was able to produce a pass, because treatment-b completed with every required category Passed.

## Threats to validity / deviations

1. **SDK pin.** The baseline `global.json` pins SDK 10.0.401, which the environment's network policy blocked. The evaluator was built with SDK 10.0.112 in a separate copy of the baseline tree in which only `global.json` changed. Evaluator sources, the contract, the fixtures and the package pins are byte-identical to the baseline. The preregistered workflow itself installs `10.0.x`.
2. **Browser source.** The Playwright CDN was blocked. The preregistered `install-browser` command was pointed (via `PLAYWRIGHT_DOWNLOAD_HOST`) at a local mirror of the same Chrome for Testing 153.0.8010.12 build, taken from Google's official CFT storage. The ffmpeg v1011 used is the same revision that was already preinstalled.
3. **Screenshots path.** Each lane's `.percepta/evidence` screenshots were copied into `screenshots/<lane>/` after its run, only to keep a record. This did not change the evaluator's configuration or inputs. No baselines were written because `--update-baselines` was not passed.
4. **Fixture vocabulary asymmetry (observation, not reinterpreted).** The predicate names that the `blocked.json` fixture sends to `window.__perceptaSetState` (for example `blocking-unknown-exists`) appear in the Percepta contract but not in the frozen product brief. The top-level fixture keys (`predicates`, `capabilities`) appear in neither. The control lanes therefore had no documented way to know the fixture schema. This may be part of the treatment effect, or it may be a confound in the instrument. Treatment-a received the contract and still failed the same checks.
5. **Treatment-b history.** Its implementation commit `22026d3` sits on top of `907ba67`, a bookkeeping commit made after the baseline, rather than directly on the baseline. The inputs it had (contract, root `AGENTS.md`, brief, lane `AGENTS.md`) are blob-identical to the baseline versions.
6. **Sample and design.** n = 2 per group, one session per provider per condition, and provider is confounded with lane.
