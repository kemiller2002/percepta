# EX-PERCEPTA-2026-0004 — evaluator procedure (declared before any candidate was rendered)

The preregistration (`research/experiments/EX-PERCEPTA-2026-0004--held-out-semantic-generalization.md`, blob `b74f0a47…`) fixes the primary outcome, the secondary outcome list, the blinding rule and the two evaluation layers. It does not register an evaluator program, viewport rule or adjudication rule. The operational choices below were written down after the gate passed and the held-out file was verified, but **before any candidate was rendered, probed or reviewed**. They are applied identically to every candidate.

## 0. Candidates and blinding

- Exactly four candidates, each staged byte-for-byte from `git show <registered SHA>:<lane>/index.html` into a scratch directory named only by a neutral blind letter (K, M, R, V assigned by `secrets.SystemRandom().shuffle`).
- The identity mapping is sealed outside the working tree and outside the reviewer-visible scratch area until all judgments are frozen.
- `experiment/percepta-0004-openai-control-b` is not a candidate and is never staged.

## 1. Deterministic layer (no semantic judgment)

Two independent instruments, both run against every candidate:

1. **Repository Percepta verifier** at the frozen baseline (`src/Percepta.Cli verify`), canonical contract `.percepta/contracts/indy-init-investigation-workspace.json`, with the five held-out states passed as `--fixture` in file order (each written verbatim as the `state` object of its case). This yields the contract's six required categories (contract-validation, structural, state-projection, interaction, accessibility, responsive). **Deterministic required-category completion** = number of required categories `Passed` out of 6; a candidate is complete only at 6/6. This is the same instrument and invocation form as EX-PERCEPTA-2026-0003.
2. **Per-state runtime probe** (`tools/probe.mjs`, Playwright + Chromium): for every candidate × held-out case × viewport in {390x844, 820x1180, 1180x820, 1440x900}, a fresh page loads the candidate over HTTP, calls `window.__perceptaSetState(state)` with the frozen state object exactly as stored, and records the entry point, the eight `data-percepta-region` hooks, the `confirm-root-cause` capability element and its disabled state, the unavailable-reason and blocker-link hooks, the accessibility floor, document overflow, the contract's per-breakpoint required regions, console errors, page errors and failed requests. It also takes a full-page screenshot. At 1440x900 it records an ARIA snapshot, the rendered `innerText`, and an operation trace: each visible, enabled control inside the legal-actions region, plus each visible in-page `#` link, is clicked on a fresh page. For each click, the trace records the resulting hash, scroll, focus and target text, plus a viewport screenshot.

Probe observations are recorded, not scored semantically. Instrumentation hooks are observation points only.

A failure is labelled **infrastructure** when the harness cannot load the page or the browser fails independently of candidate code. For example, the server is down, the browser crashes, or a navigation timeout occurs with no page error. A failure is labelled **candidate** when the page itself throws, lacks the entry point, or fails a check.

## 2. Semantic layer (blinded)

- Unit of review: one blind candidate × one held-out case. The reviewer gets only four things. The first is the blind candidate ID. The second is the rendered evidence for that state: four full-page screenshots, the desktop ARIA snapshot and rendered text, and the operation trace with its screenshots. The third is the single held-out state. The fourth is that case's obligations. The reviewer does not get the provider, condition, branch, SHA, NOTES.md, source, deterministic results, hook attributes or other cases.
- Two independent reviewers (R1, R2) review every unit. Each is a fresh AI subagent. R2 never sees R1's output. Judgments are returned as JSON and saved verbatim.
- Each obligation gets PASS or FAIL with evidence. Screenshots are authoritative for what is visible. ARIA snapshot and trace are authoritative for operability, meaning disabled state, names and navigation. Text that exists only in the accessibility tree and is not visible does not satisfy a "visible" obligation.
- **Viewport rule.** The primary judgment for each obligation is made at the contract's desktop reference viewport (1440x900). For every obligation, the reviewer also judges separately whether the meaning is preserved (discoverable and usable) at 390x844, 820x1180 and 1180x820. This separate judgment feeds only the secondary outcome "responsive preservation of held-out semantics".
- For every unit, reviewers also list any **forbidden/fabricated meaning** they see: invented blockers, invented domain reasons, unsupported resolution, unsupported remote success, capability authority overridden, opposite facts inferred from absent predicates, unsupported confidence, or suppressed/deleted meaning.

## 3. Scoring (applied after freeze)

- **Obligation score.** An obligation is PASS for scoring only if **both** reviewers judged it PASS. This follows the preregistered rule that "Passed only when directly supported… ambiguous… is Failed". A reviewer disagreement means the support is not unambiguous. Both judgments and every disagreement are preserved. Per-reviewer primary results are reported as a sensitivity analysis and are not a substitute outcome.
- **Primary.** Per candidate: the number of cases whose obligations are all PASS. Per condition: summed fully-correct cases / (2 candidates × 5 cases), pooled across providers as the preregistration states. **Support** for HY-PERCEPTA-2026-0005 only if the treatment rate is strictly greater than the control rate.
- Secondary outcomes use the preregistered list only. **Implementation churn** = `git diff --numstat <baseline> <candidate SHA>`: files changed, lines added and lines deleted, plus the number of commits from the baseline to the candidate. The preregistration gives no other definition.
- Judgments are frozen, meaning hashed and made read-only, before the mapping is unsealed. No judgment is changed after unblinding.
