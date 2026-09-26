# EX-PERCEPTA-2026-0004 — control-b (Claude) implementation notes

Independent first-pass implementation under the **control** condition.

## Inputs used
- `lanes/control-b/AGENTS.md`
- `frozen-product-brief.md`
- `frozen-runtime-protocol.md`

No Percepta semantic contract, generated semantic guidance, held-out cases, evaluator code/results,
sibling lanes, or prior-experiment implementations were inspected. The repository root `AGENTS.md`
was also not opened, to avoid any chance of it carrying semantic guidance beyond the permitted inputs.

## Design
- Single self-contained `index.html`, no external resources. Pure functions map
  `(state, predicate history, local action log) -> HTML`; one mutable cell and one render effect.
- `window.__perceptaSetState(state)` normalizes input (keeps only string predicates and
  capability values exactly `"legal"`/`"illegal"`) and re-renders. `name` is ignored.
- Predicate names are opaque, so each active predicate is shown verbatim (plus a humanized label)
  in the area its name most plausibly belongs to, using keyword heuristics
  (persistence, contradictions, unknowns, obligations, hypotheses, evidence, observation);
  unmatched predicates go to an "Other reported state" panel so nothing is dropped.
  Multiple simultaneous predicates are all rendered independently.
- Status badges are textual (Falsified / Weakened / Conflict / Unknown / Open / Reported / Active),
  with color only as reinforcement. No numeric confidence anywhere.
- A static case file supplies the triggering observation, its source, and three competing
  hypotheses. Hypotheses default to "Viable — open"; a predicate naming `h<N>` updates that
  hypothesis's status and links to the related state. Falsified hypotheses remain listed.
- Predicates that stop being active are kept in "Previously reported" / "No longer active" lists
  (historical fact, not an assertion of the opposite domain fact).
- Actions come solely from `capabilities`; legality is never inferred or overridden.
  `confirm-root-cause` is always shown (with `data-percepta-capability`); if the runtime does not
  supply it, it is shown as "legality not supplied" and unavailable. Illegal actions use
  `aria-disabled` (still focusable), explain that the runtime marks them not permitted, and link to
  the active unknowns/contradictions/obligations/non-ok persistence items that may be blocking them.
- Persistence separates **local work in this page** (actions clicked here, explicitly "local only")
  from **remote persistence**, which is only what the runtime reports; absent a report the page
  says it is not reported rather than implying it was saved.
- No chat surface.

## Ordinary checks performed (self-constructed states only)
Headless Chromium (Playwright) at 390x844, 820x1180, 1180x820, 1440x900 with three hand-made
states (empty; mixed hypothesis/evidence/unknown/obligation/persistence/unclassified predicates with
illegal confirm; falsified + contradiction + persisted with legal confirm):
no horizontal overflow, no duplicate ids, one `h1`, one `main`, all 8 instrumented regions present,
confirm-root-cause `aria-disabled` tracks supplied legality, no unnamed buttons/links, no page errors.

## Known limitations
- Predicate-to-area mapping is heuristic on names; unfamiliar vocabulary lands in "Other reported state".
- The blocker list for an illegal action shows active conditions that *may* block it; the protocol
  gives no causal link between predicates and capabilities.
