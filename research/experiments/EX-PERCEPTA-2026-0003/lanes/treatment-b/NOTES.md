# EX-PERCEPTA-2026-0003 · treatment-b · implementation notes

Single self-contained `index.html` (embedded CSS + JS, no network, no backend).

## Composition / layout

- **Header (inside `<main>`):** one `h1`, the primary question shown prominently in a callout, then two compact status cards: *Investigation status* (Open / Unresolved / Resolved) and the **persistence-state** region (current state + separate "This browser" and "GitHub" rows).
- **Jump navigation** (sticky on phones): links to every visible region with counts; the Unknowns link says "blocking" when a blocking unknown exists.
- **Two-column workspace (≥760px):**
  - main column: observation → hypotheses → contradictions (when present) → evidence;
  - side column: unknowns → next actions (legal-actions) → obligations (when present).
  Unknowns come before actions so the blocker sits above the action it blocks.
- **Single column (<760px):** the column wrappers become `display: contents`, and CSS `order` gives this reading order: observation, hypotheses, unknowns, actions, contradictions, obligations, evidence.
- Hypotheses are an unordered, auto-fit card grid with an explicit note: "Order does not imply rank, likelihood, or sequence; no confidence scores are assigned."

## Interpretation of the frozen brief

- The domain content is a static sample record about an `indy init` EACCES failure on CI runners: 3 hypotheses (LOCK, RACE, NODE), up to 5 evidence items, 2 unknowns, and up to 2 obligations. Runtime predicates decide which items appear and what state each is in. The record is illustrative. It does not come from any fixture.
- "What should be tested next" is answered by a derived "Test next" suggestion at the top of the actions region. Each non-falsified hypothesis also has a "Plan discriminating test" disclosure.
- Chat is not used anywhere.

## Use of the shared runtime protocol

- `window.__perceptaSetState(state)` normalises the input without reinterpreting it:
  - `name` becomes an opaque label, shown in the "Runtime state details" footer;
  - `predicates` becomes a Set;
  - `capabilities` keeps only entries whose value is exactly `"legal"` or `"illegal"`.
- The capability map is authoritative:
  - `confirm-root-cause` is enabled if and only if its value is `"legal"`, and disabled if `"illegal"`. Predicates never flip it either way.
  - If blockers exist but the capability is legal, the button stays enabled.
  - If the capability is absent (or has a non-conforming value), it is shown as unavailable with the reason "runtime has not supplied legality". I read "not supplied" as "not asserted executable". This is the one place the UI decides something on its own.
- Any other capability in the map is rendered as its own action button (humanized name, `data-percepta-capability`), enabled or disabled exactly as supplied, with a generic unavailable reason when illegal.
- On load, a sample default state is rendered (`blocking-unknown-exists`, `github-write-is-pending`, confirm illegal). Every call to `__perceptaSetState` replaces it completely.

## How the Percepta contract informed the implementation

The contract (`.percepta/contracts/indy-init-investigation-workspace.json`) was used directly as the specification.

- **Regions:**
  - All eight `data-percepta-region` sections exist.
  - `contradictions` is visible only when `contradictions-present`, following the contract's `visibility.when`.
  - `obligations` is visible when `obligations-present`, **or** when `corrective-action-not-verified` holds, because that projection requires a visible verification obligation (see decisions).
  - All other regions are always visible.
- **Capabilities** (`expose`, `disable-when-illegal`, `explain-when-unavailable`, `navigate-to-blocker`):
  - The confirm button is always rendered and uses native `disabled` when illegal.
  - When unavailable, a visible "Why this is unavailable" box (`data-percepta-unavailable-reason-for`) lists every predicate-derived reason, and the button references it with `aria-describedby`.
  - A primary "Go to …" link (`data-percepta-blocker-link-for`) scrolls to the blocking item, focuses it and flashes it. Secondary links cover the other reasons. Blocker priority: blocking unknown, then verification obligation, then evidence obligation, then contradiction, then falsified hypothesis.
- **State projections:** each `data-percepta-observation` hook is set only while its condition is actually true, on the element that carries that meaning:
  - `falsified-hypothesis-retained`: the NODE card, which stays in the grid and becomes "Falsified" with its falsifying evidence E4 named.
  - `falsified-state-noncolor`: the badge. It carries a ✕ symbol and the word "Falsified", and the card uses a strike-through title and a dotted border.
  - `inactive-actions-unavailable`: the falsified card's action group. Its "Plan discriminating test" and "Select as candidate" buttons are disabled, with a visible explanation.
  - `blocker-visible`: the blocking unknown U1, shown with a "⛔ Blocking" badge and a "Blocks: Confirm root cause" link.
  - `dependent-action-unavailable`: the confirm action item. Set only when the capability is actually illegal while the blocker exists.
  - `unavailable-reason-visible`: the reason box.
  - `blocker-navigation-available`: the blocker link, when it targets the blocking unknown.
  - `persistence-pending-visible`: the "Pending" label.
  - `no-false-remote-success`: the GitHub row, which reads "Write in progress — not yet confirmed by GitHub".
  - `unresolved-not-resolved`: the "Unresolved" investigation status.
  - `verification-obligation-visible`: obligation O2.
- **Forbidden patterns avoided:**
  - no chat interface;
  - obligations and blockers are never hidden;
  - falsified hypotheses are never removed;
  - every state carries a text label and symbol, plus a border style or strike-through, not colour alone;
  - there is no "synced" wording unless a synced predicate exists and no pending write;
  - no percentages;
  - no ranked or numbered hypothesis sequence.
- **Breakpoints:** checked against the contract's four viewports (see verification).

## Significant semantic decisions

- **Persistence precedence:** pending > conflicted > failed > synced > local. A pending write therefore always shows as Pending and never as success. Only `github-write-is-pending` is named by the contract. The conflicted, failed and synced predicate names (e.g. `github-write-is-failed`, `persistence-synced`) are assumed aliases. Local is the default.
- **Investigation status:**
  - `corrective-action-not-verified` always gives "Unresolved" and wins over any resolved predicate.
  - "Resolved" appears only for an assumed `investigation-resolved` predicate.
  - Otherwise the status is "Open — investigating".
- **"Actions that require an active hypothesis":** these are the per-hypothesis actions, which the UI owns because they are not in the capability map. "Select as candidate" requires status Active. "Plan discriminating test" requires not falsified. The confirm action targets the selected Active candidate. If none exists, that is listed as a reason, but the button still follows the supplied legality.
- **Contradictions:** `contradictions-present` adds evidence E5, which contradicts LOCK. LOCK keeps the Active status but shows a "Contradicted by evidence" link. A contradiction does not falsify anything by itself.
- **Clicking a legal action** only records a local "requested" notice ("not recorded as confirmed until the runtime reports it"). It never changes runtime state or persistence.

## Assumptions

- Sample domain content (log lines, run numbers, hypothesis wording) is invented but plausible.
- The persistence and resolved predicate aliases listed above are assumptions.
- The obligations region also appears for `corrective-action-not-verified` without `obligations-present`. I judged that "verification obligation remains visible" and "hide-blocking-obligation" (forbidden) require this.

## Limitations

- Forma, Limen, Aegis and Folio components are not used. The brief requires one self-contained static document, so styling is local CSS.
- There is no editing or authoring of hypotheses or evidence; the record is static and shaped by predicates.
- On phones the header and status cards take roughly the first 600px. Hypotheses, unknowns and actions are reachable by scrolling or through the sticky jump nav.
- Dark mode follows `prefers-color-scheme` only.

## Ordinary verification performed

- I opened the page in headless Chromium (Playwright) at 390x844, 820x1180, 1180x820 and 1440x900.
- I called `window.__perceptaSetState` with six states I built myself: empty with confirm legal; falsified; blocking; pending with confirm legal; unverified with extra legal and illegal capabilities; and all six contract predicates at once.
- In every case:
  - there was no horizontal overflow (`scrollWidth` equal to viewport width);
  - there were no duplicate ids;
  - there was exactly one `h1`;
  - every button and link had a non-empty accessible name;
  - there were no page or console errors.
- Hook placement was correct for every state, including no hooks when their predicates were absent, and the confirm button's disabled state always matched the supplied capability.
- Hypotheses, unknowns, legal actions and (on desktop) observation were rendered at every viewport. At 1440x900 and 1180x820 all of them start above the fold.
- I clicked the blocker link at 390px: focus moved to the blocking unknown `unknown-lock-owner`, which scrolled into view.
- I visually reviewed screenshots at 1440 and 390.
- Percepta verify and the experimental evaluator were **not** run.
