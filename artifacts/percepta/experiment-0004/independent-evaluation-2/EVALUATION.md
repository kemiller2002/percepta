# EX-PERCEPTA-2026-0004: held-out semantic generalization (frozen first-pass evaluation)

This evaluation measured the frozen candidates only. No candidate, frozen input, lane instruction or branch was modified, merged or re-run after a change. All numbers below come from `results.json`. `tools/aggregate.py` derives that file from the frozen raw evidence, and `tools/validate.py` recomputes it by a separate code path (28/28 checks pass; see `validation.json`).

## 1. Identity and gate (checked before any held-out access)

- Frozen baseline: `f474c449c2bc84da5707bb123c36500490875869`. Preregistration blob: `b74f0a47…`.
- Gate evidence is in `raw/gate/`: `gate-result.json`, `gate-refs.txt` and `delta-<candidate>.txt`. All five conditions passed:
  - all four SHAs exist;
  - all four descend from the baseline;
  - each net delta is exactly `lanes/<lane>/index.html` and `lanes/<lane>/NOTES.md`, as the frozen lane `AGENTS.md` requires;
  - each branch resolves to its registered SHA;
  - every frozen input is blob-identical.
- `openai-treatment-a` has an intermediate commit `530dc4c` that adds only `index.html`. Its net delta is still the two permitted files.
- Only after the gate passed did the evaluator read the held-out file. Its blob is `b702a376bf5b8f3d33a9d348785c5b3b1c50e687`, verified by `git hash-object`. It holds **5 cases and 25 obligations** (7 + 5 + 6 + 3 + 4).
- `experiment/percepta-0004-openai-control-b` (`9af43e65…`) is not a registered candidate. It was never staged, rendered, reviewed or aggregated.

| Blind ID | Branch | SHA | Condition | Provider |
|---|---|---|---|---|
| Candidate V | experiment/percepta-0004-openai-control-a | c99950ba1cf79a8f590e0f050b3b4efadecc0ad1 | control | OpenAI |
| Candidate M | experiment/percepta-0004-openai-treatment-a | d104371f69ae4b184769e85296fd7d4c1cd85c76 | treatment | OpenAI |
| Candidate K | experiment/percepta-0004-claude-control-b | 6b3ef30b75aaab4812919b7f56e8085dbb0fe442 | control | Claude |
| Candidate R | experiment/percepta-0004-claude-treatment-b | ed5e15fd28d7c33dd4bd51014d1c1f4d62bdbb4b | treatment | Claude |

The mapping was sealed outside the working tree and outside the reviewer-visible area. It was unsealed at 20:46:52Z, four seconds after the judgments were frozen at 20:46:48Z (`candidate-mapping.json`).

## 2. Procedure

The procedure was declared in `EVALUATION-PROCEDURE.md` (hash and time in `procedure-declared-at.txt`) before any candidate was rendered.

1. **Staging.** Each `index.html` was staged byte-for-byte from `git show <SHA>:<path>`. Hashes were taken before and after evaluation and are identical (`raw/staged-index-sha256-*.txt`). Candidates were served over local HTTP.
2. **Deterministic, instrument A (repository verifier).** The verifier `src/Percepta.Cli verify`, built at the baseline, was run with the canonical contract. The 5 held-out states were passed verbatim as fixtures (`raw/verifier-fixtures/`). Outputs are in `raw/candidate-<X>/verifier/`.
3. **Deterministic, instrument B (runtime probe).** `tools/probe.mjs` ran every candidate × 5 states × 4 viewports (390x844, 820x1180, 1180x820, 1440x900). Each run:
   - loads a fresh page;
   - calls `window.__perceptaSetState(state)` with the frozen state;
   - records the observations, a full-page screenshot and console, page and request errors.

   At desktop size it also captures an ARIA snapshot, the rendered text, and an operation trace that clicks each operable control or link on a fresh page. Outputs are in `raw/candidate-<X>/probe/<case>/`.
4. **Blinded semantic review.** The review covered 20 units (4 blind candidates × 5 cases, in randomized order), and two independent reviewers judged every unit:
   - R1 was a fresh subagent on the session model;
   - R2 was a fresh subagent on a different model.

   Each unit's packet contained only four things: the blind ID, that state's rendered evidence, the single state, and its obligations (`semantic-review/packets/`; `packet-manifest.json` hashes every file, images included). No reviewer saw the provider, condition, branch, SHA, NOTES.md, source, deterministic results, other cases or the other reviewer's output. The access audit is in `semantic-review/judgments/R?/unit-*.tool-log.json`: every tool call stayed inside the reviewer's own packet, with 0 violations. Each final JSON was extracted programmatically from its reviewer's transcript (`*.raw.txt` and `*.json`), not retyped.
5. **Freeze.** `frozen-evidence.SHA256SUMS` covers 541 files (raw evidence, judgments, packets, procedure and tools); its own SHA-256 is `aba73776…`. The freeze was recorded at 20:46:48Z, before unblinding. No judgment was changed after unblinding.
6. **Scoring (declared beforehand).** An obligation is PASS only if both reviewers judged it PASS at 1440x900. A case is fully correct only if all of its obligations pass. Per-reviewer results are reported as sensitivity analyses, not as substitutes.

## 3. Primary outcome

| Candidate | Condition / provider | Fully-correct held-out cases |
|---|---|---|
| M | treatment / OpenAI | **5 / 5** |
| R | treatment / Claude | **5 / 5** |
| K | control / Claude | **3 / 5** (fails pending-unverified, all-constraints) |
| V | control / OpenAI | **2 / 5** (fails combined-blocker-falsification, all-constraints, illegal-without-known-blocker) |

| Condition | Fully-correct / total | Rate |
|---|---|---|
| Treatment | 10 / 10 | 1.00 |
| Control | 5 / 10 | 0.50 |

**HY-PERCEPTA-2026-0005: descriptive support.** The treatment rate (1.00) is greater than the control rate (0.50). This is a small descriptive pilot: n = 2 candidates per condition, one session each. It is not statistically conclusive.

The result does not depend on the scoring rule. Scored by R1 alone, treatment is 1.00 against control 0.60. Scored by R2 alone, treatment is 1.00 against control 0.70. Either reviewer alone would also give support. All four reviewer disagreements concern control candidates.

## 4. Secondary outcomes

- **Semantic obligations passed / evaluated:** M 25/25, R 25/25, K 23/25, V 21/25. By condition, treatment 50/50 and control 44/50.
- **Failures by obligation** (scored FAIL):

| Candidate | Case | Obligation | Verdicts |
|---|---|---|---|
| V | combined-blocker-falsification | user can navigate from unavailable confirmation to blocking information | both FAIL. There is no link or operable control, the operation trace is empty, and the text only says "Review active unknowns…". |
| V | all-constraints | blocked action remains explained and navigable | both FAIL. The action is explained but cannot be navigated from. |
| V | illegal-without-known-blocker | UI does not invent a blocking unknown that was not supplied | R1 PASS, R2 FAIL |
| V | illegal-without-known-blocker | UI does not fabricate a domain-specific reason unsupported by active predicates | R1 PASS, R2 FAIL. R2 objects to "Review active unknowns, contradictions, obligations, and evidence for blocking context" appearing while every such card reads "No active … supplied". |
| K | pending-unverified | persistence is visibly pending | R1 FAIL, R2 PASS. R1 notes the header and Persistence panel read "Remote persistence: not reported" while github-write-is-pending is active; pending appears only as an obligation item. |
| K | all-constraints | pending persistence remains distinct from investigation resolution | R1 FAIL, R2 PASS, for the same reason as the row above. |

- **Deterministic required-category completion** (repository verifier; 6 required categories): R 5/6, and K, M and V 4/6 each. No candidate is complete.
  - All four fail `state-projection`. The verifier looks for `data-percepta-observation` ids that the frozen brief does not publish, and no candidate exposes them.
  - K, M and V also fail `interaction`: they have no `data-percepta-unavailable-reason-for` or `data-percepta-blocker-link-for` hook.
  - All four pass contract-validation, structural, accessibility and responsive.
  - The optional visual-regression and semantic-visual-review categories are Unavailable: there is no baseline, and no review input was supplied to the verifier.
  - As the preregistration states, mechanical completion does not substitute for semantic success. Candidate M, for example, scores 4/6 here but 5/5 semantically.
- **Per-state probe** (80 state × viewport runs):
  - The entry point exists and ran without throwing in every run.
  - Capability disabled/enabled status matched the supplied legality in every run.
  - The accessibility floor held in every run: lang, main, a single h1, no duplicate ids, named controls, and no colour-only state hooks.
  - No run had horizontal overflow.
  - The contract's per-breakpoint required regions were visible in every run.
  - There were 0 page errors, 0 console errors and 0 failed requests.
- **Provider-stratified:**
  - OpenAI: treatment M scored 5/5 against control V at 2/5, a difference of +3.
  - Claude: treatment R scored 5/5 against control K at 3/5, a difference of +2.
  - Each pair has one session per cell, so no provider-level inference is supported.
- **Forbidden or fabricated meaning.** These were reported by a single reviewer each; no unit had a defect reported by both.
  - K: the persistence state was suppressed ("Remote persistence: not reported" while a write is pending). R1 reported this twice.
  - K: invented domain-specific hypothesis content ("H1: Upstream payment service timeouts" and similar).
  - K: "3 of 3 listed explanations remain viable" was shown while hypothesis-is-falsified is active. R1 reported this twice.
  - V: the "Review active unknowns … for blocking context" text appears with no supporting predicates (R2).
  - M: "actions requiring an active hypothesis are unavailable" was shown while the capability is legal (R1, legal-with-history).
  - M: an "Investigation open" pill that no predicate supplies (R1).
  - R: "A hypothesis has been falsified; confirming a root cause needs an active hypothesis" is listed as a blocker (R1, in two cases).
  - These records are secondary. They changed an obligation verdict only where a reviewer also marked that obligation FAIL.
- **Responsive preservation:**
  - Semantic: every obligation that passed at the desktop size was preserved at 1180x820, 820x1180 and 390x844 by both reviewers (K 23/23, M 25/25, R 25/25, V 21/21).
  - Obligations that failed at the desktop size also failed at the other viewports for the reviewer who failed them.
  - Deterministic: no overflow and all required regions visible in 20/20 state × viewport runs per candidate.
  - Limitation: the operation trace ran at 1440x900 only.
- **Implementation churn** (`git diff --numstat` baseline→SHA): every candidate changed 2 files and deleted 0 lines.

| Candidate | Lines added | Commits |
|---|---|---|
| V | 120 | 1 |
| M | 48 | 2 |
| K | 567 | 1 |
| R | 615 | 1 |

## 5. Reviewer disagreements

There were **4 primary disagreements** out of 100 obligation judgments per reviewer: the two for V on illegal-without-known-blocker and the two for K on persistence (see §4). There were also 12 disagreements in the responsive verdicts, all on those same 4 obligations. Every disagreement is preserved in `results.json` → `reviewerDisagreements`. Under the declared rule, each one scores FAIL. The preregistration defines no adjudication procedure, and none was applied.

## 6. Infrastructure failures

None. Every navigation succeeded, and there were no browser crashes and no probe or verifier harness exceptions. The verifier's exit code 3 means "evidence incomplete", which is a candidate result and not an infrastructure failure.

## 7. Deviations, threats and limitations

1. **Evaluator procedure not frozen before implementation.** The preregistration's Integrity section requires the "evaluator/review procedure" to be frozen before implementation, but no such procedure was registered in the repository. This evaluator declared its procedure after the gate and before rendering (`EVALUATION-PROCEDURE.md`). The declaration covers:
   - the desktop viewport as the primary judgment;
   - the both-reviewers-PASS scoring rule;
   - the probe design;
   - the reuse of the repository verifier.

   These are evaluator choices, not preregistered ones. The sensitivity analysis shows that the hypothesis verdict does not depend on the scoring rule.
2. **Reviewers are AI subagents, not humans.** Both are Claude-family models, the same vendor as the Claude lanes. The blinding was enforced by instruction, and the audit logs show it was followed, but it was not technically sandboxed.
3. **The deterministic instrument favours hook conventions.** The verifier's state-projection and interaction checks depend on hook attributes that the brief does not publish (see §4), so the mechanical completion numbers mainly measure hook exposure.
4. **Browser substitution, as in EX-0003.** The verifier used Chromium 141 (r1194) in place of Playwright 1.63's expected r1243. The download host is blocked, so a symlink tree was used. The .NET SDK 10.0.112 came from the Ubuntu archive, and the `global.json` pin was bypassed by building outside the repo; the file was not modified. The probe used Node Playwright 1.56 with the same Chromium 141. The same substitution applied to every candidate.
5. **The operation trace and ARIA snapshot were captured at 1440x900 only.** Responsive operability was judged from the screenshots.
6. **ROS/Praxis bookkeeping was not performed through the CLI.**
   - `./ros` could not fetch its v3.4.0 binary: the release asset returned 404.
   - Installing the Echelon/Praxis toolchain from its remote install script was denied by this session's permission policy.
   - Managed ROS state (`.ros/`, `registries/`) was deliberately not edited by hand.

   The `ROS validation` CI job may therefore flag this change as missing a work-item attribution. The recording needs to be completed by someone with the toolchain (see the PR notes).
7. **Read-only markers.** Running as root makes `chmod a-w` advisory only. The SHA-256 manifest is the actual integrity control.
8. **Gitignore.** `artifacts/percepta/` is gitignored at the baseline, so the evaluation files were force-added and `.gitignore` was not changed.
9. **Tile images not committed.** The packet screenshots are byte-copies of `raw/…png`. The tiles can be regenerated from those images with `tools/build_packets.py` (Pillow crop, 1100 px, 80 px overlap). Their hashes are in `packet-manifest.json`, but the tile images themselves are not committed, to limit repository size.

## 8. Files

- `EVALUATION-PROCEDURE.md` and `procedure-declared-at.txt`: the procedure declared before rendering.
- `raw/gate/`: gate evidence.
- `raw/candidate-<X>/verifier/`: the verifier's evidence, stdout, stderr, exit code and screenshots.
- `raw/candidate-<X>/probe/`: probe observations, full-page screenshots and operation-trace JPEGs.
- `raw/verifier-fixtures/` and `raw/staged-index-sha256-*.txt`: the fixtures and the staged-file hashes.
- `semantic-review/`: the reviewer prompt, packets (text parts), packet manifest, reviewer assignments, and raw and parsed judgments with tool-call audit logs.
- `frozen-evidence.SHA256SUMS`, `.sha256` and `frozen-at.txt`: the freeze record.
- `candidate-mapping.json` and `unblinded-at.txt`: the identity key, unsealed after the freeze.
- `results.json`: primary and secondary outcomes, plus every obligation-level record.
- `validation.json`: the independent recomputation.
- `final-integrity.json`: the end-of-run integrity checks.
- `environment.json`: the tool versions and assembly hashes.
- `tools/`: `probe.mjs`, `build_packets.py`, `extract_judgments.py`, `aggregate.py` and `validate.py`.
