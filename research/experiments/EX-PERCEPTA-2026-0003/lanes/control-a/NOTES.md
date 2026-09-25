# EX-PERCEPTA-2026-0003 — control-a notes

Condition: control. Inputs used: the lane `AGENTS.md`, `frozen-product-brief.md`, `frozen-runtime-protocol.md`. No Percepta semantic contract or generated guidance was read.

## Composition / layout

- One static `index.html` with embedded CSS and JavaScript; no backend, no external resources.
- Header: single `h1`, the primary question shown prominently, then a short derived answer ("Currently explains: … / Test next: …") and a row of jump links to every region, so hypotheses, unknowns and next actions stay reachable at every viewport.
- `main` is a responsive grid:
  - under 800px: one column (observation → next actions → save status → hypotheses → evidence → unknowns → contradictions → obligations);
  - 800–1149px: hypotheses + evidence span the full width on top, the other panels in two columns below;
  - 1150px and up: three columns (observation / actions / save status | hypotheses / evidence | unknowns / contradictions / obligations).
- Each of the eight required regions is a `section` with a heading and the matching `data-percepta-region`.

## Reading of the brief

- Hypotheses are shown as an unordered list (`ul`) with an explicit note that they are held in parallel and not ranked. No confidence percentages anywhere.
- Status is shown as text plus a glyph plus a border style (Active ●, Weakened ◐ dashed, Falsified ✕ double border + strikethrough title), so it does not depend on color. Falsified hypotheses stay in the list with the evidence that falsified them.
- Evidence items list their relationship to specific hypotheses (supports / weakens / contradicts / falsifies), and each hypothesis lists the evidence that bears on it, cross-linked both ways.
- Contradictions, unknowns (blocking vs non-blocking, resolved vs unresolved) and obligations each have their own panel with an explicit empty state when nothing is present.
- Next actions are split into "Available now" and "Unavailable". Unavailable actions stay visible as disabled buttons, have an `aria-describedby` reason, and a "Go to blocker" link that scrolls to and focuses the blocking item.
- Save status always shows the current state (Local only / Pending / Synced / Conflicted / Failed) with a glyph and text, a plain-language explanation, a separate "Remote copy" line, and a legend of all five states with the current one marked. Only "Synced" says the remote copy is confirmed; "Pending" explicitly says it is not yet confirmed.
- Chat is not used.
- Pressing an available action records it in a local, in-browser list labelled "recorded locally, not sent to the remote store". It does not change persistence state or claim a remote write.

## Use of the runtime protocol

- `window.__perceptaSetState(state)` takes `{ name, predicates[], capabilities{} }`, copies it defensively and re-renders everything from it through pure functions (`readFacts` → `deriveModel` → view strings). Only the final DOM write is side-effecting.
- `name` is treated as opaque and only shown as "Workspace snapshot: …".
- `predicates` is consumed as a set of strings. Predicate names are not specified, so the page matches keywords (e.g. `falsif`, `weaken`, `contradict`, `unknown`/`unresolved`/`blocking`, `obligation`/`verif`, `pending`/`synced`/`conflict`/persistence `fail`, and `resolved`/`satisfied` forms) to switch parts of a fixed sample investigation on or off.
- `capabilities` is authoritative: each supplied `"legal"` capability becomes an enabled button and each `"illegal"` one a disabled button with a reason. The UI never overrides a supplied value. Only when `confirm-root-cause` is missing from the map does the page infer its availability (unavailable if any blocker is derived).
- Neutral hooks are placed on the element that carries the meaning, and only when that meaning is visible: e.g. `falsified-hypothesis-retained` on the falsified hypothesis card, `falsified-state-noncolor` on its text badge, `blocker-visible`/`unresolved-not-resolved` on the open blocking unknown and its "Unresolved" label, `verification-obligation-visible` on an unsatisfied obligation, `persistence-pending-visible` on the "Pending" badge, `no-false-remote-success` on the "Remote copy: not confirmed…" line (any non-synced state), and the confirm-root-cause capability/reason/blocker-link hooks on the real button, reason text and link.

## Assumptions

- The investigation content (the `indy init` failure, hypotheses H-A/H-B/H-C, evidence E1–E5, unknowns U1/U2, obligation O1) is illustrative sample data. The brief does not supply domain objects and the protocol says not to require them.
- Keyword matching of predicate names is a guess. Predicates that match no keyword have no visible effect.
- The blocker link targets the first derived blocker (blocking unknown → unsatisfied obligation → contradiction → several hypotheses still standing → unconfirmed persistence). If no blocker is derived, it links to the hypotheses panel.
- Before any `__perceptaSetState` call, a built-in demo state is rendered. Each call replaces it completely.

## Limitations

- Predicate interpretation relies on name heuristics and may not match what the fixtures intend.
- Sample content is fixed: predicates toggle predefined items rather than creating new ones.
- Actions do not change the investigation model; they are only recorded in the local list.
- The unavailable-reason text lists every derived blocker, even when the harness marks an action illegal for a reason the page can't see.

## Checks run (all self-built, no Percepta verify or evaluator)

- Loaded the page in headless Chromium at 390×844, 820×1180, 1180×820 and 1440×900 with the default state and four hand-built states (blocked, clear/legal, empty, conflicted with resolved items).
- Every combination: horizontal overflow 0, no duplicate ids, one `h1`, one `main`, `lang="en"`, all eight regions present, no unnamed buttons or links, no console or page errors.
- `confirm-root-cause` is disabled when supplied `illegal` and enabled when supplied `legal`. The reason and blocker link appear only when it is unavailable.
- Clicking the blocker link at 390px moved focus to the blocking unknown (`unknown-U1`).
- Looked at a full-page screenshot at 1440×900.
