# Control B — implementation notes

Single self-contained `index.html` (embedded CSS + JS, no backend). Built only from
`frozen-product-brief.md` and this lane's `AGENTS.md`.

## Composition

- **Header**: the primary question is the page's only `h1`. The save-status panel
  (`persistence-state`) sits beside it so persistence is always in view.
- **Sticky section nav**: jump links with live counts (hypotheses, blocking unknowns,
  available actions…). This keeps hypotheses, unknowns and actions discoverable at 390px.
- **Left column**: triggering observation + source → "Current position" summary (what
  currently explains it, unranked; what to test next) → competing hypotheses.
- **Right column**: next actions (first, so it follows hypotheses when the layout stacks
  on narrow screens) → unknowns → obligations → contradictions → evidence.
- Two columns at ≥1100px, one column below that.

## Interaction and meaning decisions

- Hypotheses are grouped into parallel columns by standing (Confirmed / Active / Weakened /
  Falsified). They are not numbered or ranked, and no confidence percentages are shown.
  Falsified hypotheses stay on the board with a "✕ Falsified (retained)" text badge, a
  dotted border, hatching and strikethrough, so the state never depends on colour alone.
- Evidence ↔ hypothesis relations (supports ▲ / contradicts ▼ / weakens ◆ / related •)
  are shown from both sides and link to each other.
- Unknowns: unresolved ones come first with an "Unresolved" label, and blocking ones also get
  a "Blocking" label. Resolved unknowns collapse into a `<details>` but stay available.
- Actions: **Confirm root cause** is always shown. Unavailable actions keep a real
  `disabled` button with a 🔒 glyph and a visible "Unavailable: …" reason (linked with
  `aria-describedby`). They also get "Go to …" links that scroll to, focus and highlight the
  blocking unknown, obligation or contradiction. If the fixture marks the investigation as
  inactive (closed, archived, paused…), every action is disabled and a notice explains why.
- Persistence: the panel covers local / pending / synced / conflicted / failed, each with an
  icon, a label and an explanation, and shows a legend of all five with the current one
  marked. In every state other than synced, the panel says "Remote save not confirmed". If a
  fixture says synced but has outstanding writes, the panel shows pending.
  Clicking an available action only records the request in this browser and sets the state
  to "Local only". Nothing claims it was sent.
- There is no chat surface. The structured panels are the primary interface.

## Evaluator hooks

The region, capability, reason and blocker-link hooks sit on the visible elements that carry
that meaning. Observation hooks are added only when the state they describe is actually
rendered. For example, `falsified-hypothesis-retained` goes on a falsified card and
`no-false-remote-success` goes on the "Remote save not confirmed" sentence.

## Assumptions

- The fixture schema was not specified, so `window.__perceptaSetState(state)` normalizes
  tolerantly. It accepts a JSON string or object; optional wrapping in `state` or
  `investigation`; arrays or id-keyed maps; and common synonyms such as `status`/`state`,
  `refuted`→falsified, `blocking`/`severity`, `available`/`enabled`/`disabled`, and
  `blockedBy`/`blockers`.
- If the fixture gives no availability for confirm-root-cause, the page works it out. Confirm
  is unavailable while there are open blocking unknowns, open obligations (unless they are
  scoped to other actions) or unresolved contradictions, and the page lists those as its
  blockers.
- A built-in demo state is shown until a fixture is supplied.
- The code is written in a functional style: pure normalizers build a view model, and pure
  render functions turn it into HTML. Side effects are limited to `render`, navigation and
  one click handler.
