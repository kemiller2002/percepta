# EX-PERCEPTA-2026-0004: frozen first-pass evaluation

**Held-out semantic-state generalization.** This evaluation only measured the candidates. No candidate implementation was edited, repaired, regenerated, merged, or re-run after modification.

- Frozen baseline: `f474c449c2bc84da5707bb123c36500490875869` ("EX-PERCEPTA-2026-0004 register experiment")
- Hypothesis: HY-PERCEPTA-2026-0005
- Preregistration: `research/experiments/EX-PERCEPTA-2026-0004--held-out-semantic-generalization.md` (read at the baseline)
- Evaluation protocol (the evaluator's operationalization, fixed before any candidate ran): `EVALUATION-PROTOCOL.md`. SHA-256 `7b9faff7…73fb383`, frozen at 2026-09-27T05:06:56Z (`protocol.frozen-at.txt`).

## 1. Gate (recorded before any held-out-driven evaluation)

Full record: `integrity.json`. Gate verified at 2026-09-27T05:03:14Z, before the held-out cases were opened. `gatePassed: true`.

| Lane | Branch | Frozen commit | Exists | Descends from baseline | = branch head | Changes exactly lane `index.html` + `NOTES.md` | Frozen inputs blob-identical |
|---|---|---|---|---|---|---|---|
| control-a | experiment/percepta-0004-openai-control-a | `c99950ba1cf79a8f590e0f050b3b4efadecc0ad1` | yes | yes | yes | yes | yes |
| treatment-a | experiment/percepta-0004-openai-treatment-a | `d104371f69ae4b184769e85296fd7d4c1cd85c76` | yes | yes | yes | yes (2 commits: index.html, then NOTES.md) | yes |
| control-b | experiment/percepta-0004-claude-control-b | `6b3ef30b75aaab4812919b7f56e8085dbb0fe442` | yes | yes | yes | yes | yes |
| treatment-b | experiment/percepta-0004-claude-treatment-b | `ed5e15fd28d7c33dd4bd51014d1c1f4d62bdbb4b` | yes | yes | yes | yes | yes |

`experiment/percepta-0004-openai-control-b` (head `9af43e65…`) is not a preregistered candidate. It was excluded from every step. It was not staged, rendered, verified, or reviewed.

The held-out file blob `b702a376…` is identical at the baseline and at every candidate commit. Each candidate's `index.html` was staged byte-for-byte from `git show <frozenCommit>:…`. Its SHA-256 matched before and after every run (`deterministic/*/*/run.json`, `byteIdentical: true` for all 24 candidate runs).

## 2. Procedure

1. Opaque IDs `candidate-1..4` were assigned by a cryptographically random shuffle, sealed in scratch, and committed only after unblinding (`candidate-mapping.json`).
2. **Layer 1, deterministic.** The repository Percepta verifier was built from the baseline source in a detached worktree at `f474c449`. It ran with the canonical contract and the five held-out states, supplied verbatim as fixtures (`held-out-fixtures/`). Each candidate got one combined run with all five fixtures in registered order, plus one run per case. An infrastructure positive control (the repository reference page `tests/fixtures/indy-init/index.html`, same build, same fixtures) ran first, and the verifier worked end to end (`deterministic/positive-control/`).
3. **Layer 2, rendered evidence.** `tools/capture.mjs` loaded each candidate in each held-out state and captured full-page screenshots at 1440x900, 1180x820, 820x1180, and 390x844, plus accessibility-tree snapshots, a control inventory, and operability probes (every visible enabled control activated on a fresh page). The capture was identical for every candidate. All 20 items were captured, and the protocol call succeeded in every item (`capture/capture-summary.json`).
4. **Layer 3, blinded semantic review.** The 20 candidate × case items were relabelled `item-01..20` by a second random shuffle (`semantic-review/item-key.json`). Each item was reviewed by two independent AI reviewers, each in a fresh isolated session: **R1** (Claude Opus-family) and **R2** (Claude Sonnet-family). Each review received only its packet: screenshots, accessibility trees, controls, probes, the single state, and that case's obligations. Every review used the same prompt (`semantic-review/REVIEWER-PROMPT.md`). Reviewers received no provider, condition, branch, SHA, NOTES.md, source, deterministic result, or other reviewer's output. All 40 outputs passed schema validation, with every obligation present verbatim and in order (`semantic-review/review-validation.json`).
5. **Freeze.** All raw evidence (653 files) was hashed into `raw-evidence.SHA256SUMS` (SHA-256 `744fdc92…99f8270`) at 2026-09-27T05:19:12Z. It was made read-only, then scored blind (`semantic-review/scores.blind.json`, hash in `scores.blind.sha256`). It was unblinded at 2026-09-27T05:19:45Z (`unblinded-at.txt`). No judgment was changed at any point.
6. **Scoring rule** (fixed in the protocol before review): an obligation is Passed only if both R1 and R2 judged it `pass`. A case is fully correct only if every obligation passes.

## 3. Observed evidence

### 3.1 Deterministic (combined run, all five held-out fixtures)

| Lane | contract-validation | structural | state-projection | interaction | accessibility | responsive | Required passed |
|---|---|---|---|---|---|---|---|
| treatment-a (openai) | Passed | Passed | **Failed** | **Failed** | Passed | Passed | 4/6 |
| control-a (openai) | Passed | Passed | **Failed** | **Failed** | Passed | Passed | 4/6 |
| treatment-b (claude) | Passed | Passed | **Failed** | Passed | Passed | Passed | 5/6 |
| control-b (claude) | Passed | Passed | **Failed** | **Failed** | Passed | Passed | 4/6 |

No candidate was deterministically `complete`.

- The state-projection failures are all of the form "observation `<id>` is not visible": the verifier looks for `data-percepta-observation` hooks.
- The interaction failures are "no visible unavailable explanation" and "no visible path to blocking information": the verifier looks for `data-percepta-unavailable-reason-for` and `data-percepta-blocker-link-for` hooks.
- A diagnostic probe (`diagnostics/hook-presence-held-out-01.txt`) found no timing race: the results were identical after a 1.5 s wait. Three candidates emit none of these hooks. Treatment-b emits three of them.
- The frozen product brief names only the region hooks and the `confirm-root-cause` capability hook. It does not name observation IDs.
- These are candidate results under the frozen evaluator, not infrastructure failures. §5 explains what they do and do not measure.
- The per-case runs show `Unavailable` categories wherever a single fixture does not cover a predicate or legality. Per the protocol, those are coverage artifacts and are not scored.

### 3.2 Semantic review (individual judgments in `semantic-review/raw/`)

100 obligations were judged by each of the two reviewers (200 individual judgments). There were 5 disagreements, all of them R1 `fail` versus R2 `pass`:

| Lane | Case | Obligation | R1 | R2 |
|---|---|---|---|---|
| treatment-b | combined-blocker-falsification | unavailability has a visible reason | fail (fabricated: the page cites falsification as a reason for illegality) | pass |
| treatment-b | all-constraints | blocked action remains explained and navigable | fail (contradictory) | pass |
| control-b | pending-unverified | persistence is visibly pending | fail (contradictory: the persistence panel says "Remote persistence: not reported") | pass |
| control-b | all-constraints | no active meaning suppresses another active meaning | fail (contradictory: "Viable explanations: 3" and "3 of 3 remain viable" while a falsification is active) | pass |
| control-b | all-constraints | pending persistence remains distinct from investigation resolution | fail (contradictory: pending write filed only as an obligation, persistence panel "not reported") | pass |

Agreed failures (both reviewers `fail`, both `inaccessible-operation`):

| Lane | Case | Obligation |
|---|---|---|
| control-a | combined-blocker-falsification | user can navigate from unavailable confirmation to blocking information |
| control-a | all-constraints | blocked action remains explained and navigable |

In both, the only control on the page is the disabled confirm button. The reason text says "Review active unknowns…", but no operable path exists, and the probe list is empty.

Every other obligation was judged `pass` by both reviewers. The evidence cited for each pass is recorded per obligation.

## 4. Calculated results

### 4.1 Primary outcome (fully correct held-out cases)

| Candidate | Lane | Provider | Condition | Fully correct cases |
|---|---|---|---|---|
| candidate-1 | treatment-a | openai | treatment | **5 / 5** |
| candidate-2 | control-a | openai | control | **3 / 5** |
| candidate-3 | treatment-b | claude | treatment | **3 / 5** |
| candidate-4 | control-b | claude | control | **3 / 5** |

| Condition | Fully correct / held-out cases | Rate |
|---|---|---|
| Treatment | 8 / 10 | 0.80 |
| Control | 6 / 10 | 0.60 |

**HY-PERCEPTA-2026-0005: descriptive support under its preregistered criterion** (0.80 > 0.60). This is a small descriptive pilot (n = 2 implementations per condition, one session each). No statistical procedure was preregistered, and no significance is claimed.

Sensitivity (secondary; it does not replace the primary outcome):

| Reviewer | Treatment | Control | Treatment higher? |
|---|---|---|---|
| R1 alone | 8/10 | 6/10 | yes |
| R2 alone | 10/10 | 8/10 | yes |

### 4.2 Secondary outcomes

| Measure | Treatment | Control |
|---|---|---|
| Semantic obligations passed / evaluated | 48 / 50 | 45 / 50 |
| Deterministic required categories passed / evaluated (combined run) | 9 / 12 | 8 / 12 |
| Cases with responsive preservation at all 3 other viewports (both reviewers, among cases fully correct at desktop) | 8 | 6 |
| Obligation failures typed fabricated/contradictory | 2 | 3 |

Provider-stratified results (each cell is one implementation):

| Provider | Treatment fully correct | Control fully correct | Treatment obligations | Control obligations | Deterministic req. (T / C) |
|---|---|---|---|---|---|
| OpenAI (A) | 5/5 | 3/5 | 25/25 | 23/25 | 4/6 / 4/6 |
| Claude (B) | 3/5 | 3/5 | 23/25 | 22/25 | 5/6 / 4/6 |

Failures by obligation (primary scoring; full list in `results.json` → `secondaryOutcomes.failuresByObligation`): 7 obligation failures in total. By case: all-constraints 4, combined-blocker-falsification 2, pending-unverified 1. The only obligation that failed in more than one candidate is "blocked action remains explained and navigable" (all-constraints), which failed for control-a and treatment-b. No obligation in `legal-with-history` or `illegal-without-known-blocker` failed for any candidate.

Responsive preservation: no reviewer judged any obligation that passed at desktop as not preserved at another viewport. No candidate had horizontal overflow at any viewport.

Reviewer-reported fabricated or forbidden meaning (free-form, not tied to obligations, raw lists in `semantic-review/raw/`):

| Lane | R1 | R2 |
|---|---|---|
| treatment-a | 8 | 0 |
| control-a | 3 | 1 |
| treatment-b | 13 | 0 |
| control-b | 15 | 0 |

The reviewers are strongly asymmetric: R1 reported 39 such items and R2 reported 1. The main themes R1 raised:

- **Invented domain content not supplied by the state.** Treatment-b and control-b render named hypotheses, evidence, and an incident narrative. Treatment-a shows an "Investigation open" chip and a "LOCAL" persistence badge.
- **Viability claims under an active falsification.** Control-b shows "Viable explanations: 3".
- **Causal reasons for illegality inferred from predicates.** Treatment-b and control-b.

These counts are descriptive and are not part of the primary scoring.

Implementation churn (git, baseline → frozen commit; there were 0 corrective iterations in every lane):

| Lane | Commits | Files | Lines added | index.html lines |
|---|---|---|---|---|
| treatment-a | 2 | 2 | 48 | 26 |
| control-a | 1 | 2 | 120 | 95 |
| treatment-b | 1 | 2 | 615 | 532 |
| control-b | 1 | 2 | 567 | 516 |

## 5. Interpretation (clearly separated from the calculated results)

- The preregistered criterion is met, but the entire condition difference comes from one pair. The OpenAI treatment lane passed 5/5 cases and its control passed 3/5. The Claude pair tied at 3/5 each. With one session per cell, the result cannot separate a contract effect from session-to-session variation, and the provider-stratified data are mixed: an effect for OpenAI, no difference for Claude.
- The two control-a failures are a single defect that appears in two states: no user-operable path from the unavailable confirmation to the blocking information. The treatment-b failure (R1) is the reverse kind of error: treatment-b invents a causal reason (falsification) for the illegality. Treatment-b therefore did not avoid fabricated meaning. Its failures are counted in the primary outcome only because R1 alone failed them and the conjunctive rule applies.
- The deterministic layer and the semantic layer diverge sharply. Treatment-a, the only candidate with a perfect semantic score, fails 2/6 required deterministic categories because it emits no observation, reason, or blocker-link hooks. Treatment-b, the only candidate that passes interaction deterministically, loses two cases semantically. The brief withheld observation IDs, so the deterministic state-projection category measures access to hook vocabulary more than it measures preserved meaning. As the preregistration anticipated, deterministic completion is not a proxy for semantic generalization in this experiment.
- `legal-with-history` and `illegal-without-known-blocker` did not discriminate at all: every candidate passed them. Every observed failure occurs in the three cases that combine a blocker with other predicates, or that involve pending persistence.

**Speculation (not supported by this design):** the contract may help mainly by making the "navigate to blocker" and "explain unavailable" capability projections explicit. That would be consistent with control-a's only failures. It would not explain treatment-b's fabricated reason or the Claude tie.

## 6. Reviewer disagreements

There were 5 of 100 obligation-level disagreements (listed in §3.2 and in `results.json` → `reviewerDisagreements`). All went in the same direction: R1 was stricter. Under the conjunctive rule fixed in the protocol, all 5 count as Failed. Taking R2 alone would change the Claude pair to 5/5 vs 5/5 and the condition rates to 1.00 vs 0.80. Taking R1 alone gives the primary numbers. The direction of the condition comparison is the same under all three readings. No consensus was forced, and no post-hoc tie-break was applied.

## 7. Infrastructure problems and deviations (all disclosed)

1. **.NET SDK.** Microsoft hosts are unreachable, so the Ubuntu packages `dotnet-sdk-10.0` 10.0.112 and `dotnet-runtime-8.0` were used, the same substitution as in EX-0003. The build ran outside the repository, so the `global.json` pin (10.0.401) was not consulted. The file was not modified.
2. **Verifier source.** The verifier was built from the baseline commit, not current main (main changed `src/Percepta.Cli/Program.fs` after the baseline).
3. **Browser.** The verifier expects chromium-headless-shell r1243 (153.0.8010.12). The preinstalled r1194 (Chromium 141.0.7390.37) was used through a scratch symlink tree, identically for all candidates. `doctor` reported Browser OK.
4. **Evaluator-defined protocol.** No EX-0004 evaluator or semantic-review procedure was frozen at the baseline, although the preregistration's Integrity section lists "evaluator/review procedure" among the items to freeze. The layer operationalizations, the conjunctive two-reviewer rule, desktop as the primary judgment viewport, and the responsive definition were therefore fixed by this evaluation in `EVALUATION-PROTOCOL.md` after the held-out cases were read. They were fixed before any candidate was run.
5. **Reviewers are AI.** R1 and R2 are Claude-family subagents spawned by this evaluation session. They are not human, and they are the same vendor family as the Claude lanes. Their isolation was instruction-based, not sandboxed. A transcript audit found that every reviewer command stayed inside its packet and its own output file: no git, no source, no staging, no sealed paths. The harness automatically attached the repository `CLAUDE.md` pointer to each reviewer session. It contains no candidate information.
6. **Blinding cue.** One candidate (treatment-b, in the `illegal-without-known-blocker` state) visibly says "The current state reports no contract-recognised facts." This is a weak cue toward the treatment condition. It was not redacted, because the rendered evidence was not altered. Both reviewers passed every obligation in that item.
7. **Scope of the operability evidence.** The probes activated each visible, enabled control once at desktop. Operability at the other viewports was inferred from the accessibility tree, as the reviewer prompt instructed.
8. **ROS bookkeeping.** `./ros` could not obtain its v3.4.0 binary (HTTP 404 from the release host), and no EX-0004 evaluation work item exists. No `.ros` records were created or edited by hand. The commit contains evaluation artifacts only, the same scope as the EX-0003 evaluation commit.
9. **Gitignore.** `artifacts/percepta/` is gitignored, so the artifacts were force-added. `.gitignore` is unchanged.
10. **Baseline worktree side effects.** The verifier ran with a scratch worktree of the baseline as its working directory. No repository file was written by the verifier.

## 8. Limitations

- There are n = 2 implementations per condition and one session per provider × condition. The result is descriptive only, and the whole difference comes from one provider pair.
- There are only 5 held-out cases. Two of them did not discriminate.
- The reviewers are AI models from a single vendor family, with a strong strictness asymmetry between them. The conjunctive rule makes the primary outcome track the stricter reviewer.
- The deterministic verifier checks hook-level instrumentation that the brief did not disclose to control lanes. Its categories are not comparable to EX-0003's ceiling results.
- Rendering used Chromium 141, not the pinned 153.

## 9. Files

- `integrity.json`: the gate, recorded before evaluation.
- `EVALUATION-PROTOCOL.md` and `protocol.frozen-at.txt`: the evaluator operationalization.
- `environment.json`: toolchain, browser, assembly hashes, and reviewer identities.
- `held-out-fixtures/`: the five held-out states, verbatim.
- `deterministic/<subject>/<run>/{evidence.json,run.json,stdout.txt,stderr.txt,screenshots/}`: raw verifier output, including `positive-control/`.
- `diagnostics/hook-presence-held-out-01.txt`: the timing and hook diagnostic.
- `capture/<candidate>/<case>/`: rendered evidence (screenshots, accessibility trees, probes, `capture.json`).
- `semantic-review/REVIEWER-PROMPT.md`, `item-key.json`, `raw/R1|R2/item-NN.json`, `review-validation.json`: review inputs and raw judgments.
- `raw-evidence.SHA256SUMS` (+ `.sha256`, `raw-evidence.frozen-at.txt`): the frozen raw-evidence manifest.
- `semantic-review/scores.blind.json` (+ `.sha256`): blind scores.
- `candidate-mapping.json` and `unblinded-at.txt`: the unblinding key.
- `results.json`, `secondary-outcomes.json`, `summary.tsv`: aggregates.
- `tools/`: `integrity.py`, `evaluate.py`, `capture.mjs`, `diagnose-hooks.mjs`, `build_packets.py`, `validate_reviews.py`, `aggregate.py`.
- `SHA256SUMS`: hashes of every file above.
