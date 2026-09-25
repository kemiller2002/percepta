# EX-PERCEPTA-2026-0003 · control-b · Notes

Inputs read: lane `AGENTS.md`, `frozen-product-brief.md`, `frozen-runtime-protocol.md`. No Percepta semantic contract or generated guidance was consulted.

## Composition / layout

- Header: one `h1`. Below it, the primary question in large type, then a one-line "current reading" that answers it: what the current explanation is (settled or not, with counts of active, weakened and falsified hypotheses) and what to test next. Below that, a jump nav to Hypotheses, Unknowns, Next actions, Obligations, Contradictions and Evidence, so these stay easy to find at every viewport.
- `main` is a CSS grid with named areas:
  - **≥1150px (three columns):** observation and unknowns on the left, hypotheses in the centre, next actions on the right. Obligations and contradictions come next, with evidence across the full width at the bottom.
  - **800–1149px (two columns):** observation next to actions, hypotheses full width, unknowns next to obligations, then contradictions and evidence.
  - **<800px (one column):** save status, observation, **actions**, **hypotheses**, **unknowns**, obligations, contradictions, evidence.
- A save-status bar sits above everything, with a legend. A `<details>` shows the raw runtime state name, predicates and capabilities.
- Hypotheses are an unordered, auto-fit card grid with no ranking and no numbering, and they carry no confidence scores. The panel says so.
- Evidence cards list their relationships as tags ("Supports H-A", "Refutes H-C", "Weakens H-B", "Contradicts E1", "Consistent with …"). Each hypothesis card also lists the evidence linked to it.
- No chat interface.

## Interpretation of the brief

- The domain content is illustrative: an `indy init` failure on a CI runner. It has four competing hypotheses (H-A to H-D), unknowns U1 and U2, obligation O1, contradiction C1, and evidence E1 to E5. The runtime predicates decide which statuses and items apply.
- Falsified hypotheses stay in the grid. They get a text badge "✕ Falsified", a strikethrough title, a hatched dashed card and a "retained on record" note, so the state does not rely on colour.
- Every status uses a glyph plus a text label, and border style (dashed, double or dotted) varies as well. Colour is never the only signal.
- Save states:
  - The five states are Local only, Save pending, Synced, Conflict and Save failed.
  - In every state except Synced, the page says explicitly "Remote save: not confirmed".
  - Save pending never shows success wording.
  - With no persistence predicate, the default is "Local only". This is the conservative choice.

## Use of the shared runtime protocol

- `window.__perceptaSetState(state)` normalises the input to `{name, predicates[], capabilities{}}`. From that it builds an immutable view model with pure functions, and then re-renders.
- **Capabilities are authoritative:**
  - Every supplied capability is shown.
  - `legal` becomes an enabled button.
  - `illegal` becomes a visible but `disabled` button. Next to it is the visible reason text (`data-percepta-unavailable-reason-for`), linked with `aria-describedby`. When a blocking item can be identified, there is also a link to it (`data-percepta-blocker-link-for`) that scrolls to the target and focuses it.
  - The UI never changes a supplied legality. Blockers are only used to *explain* an `illegal` value; they never disable a `legal` action.
  - `confirm-root-cause` is always shown. If the map does not include it, it is shown as unavailable ("not made available by the current state"), because it was not granted as executable.
- **Predicates** are matched by keywords to presentation flags:
  - falsif/refut → H-C falsified
  - weaken → H-B weakened
  - contradict → C1 and E5 shown
  - block → U1 blocking
  - obligation/verif → O1 outstanding
  - pending/conflict/failed/synced → save state
  - inactive/closed/locked → inactive notice
  - …resolved/…met → the unknown is resolved or the obligation is discharged

  Every raw predicate is also listed in the runtime-state disclosure, so nothing supplied is hidden.
- Clicking a legal action only adds a local "requested" entry to a log. It does not change the predicates, capabilities or save state. The next state the host supplies stays the authority.
- Observation hooks go only on the visible elements that carry that meaning. Examples: the falsified card and its text badge, the blocking unknown, the disabled action, the reason text, the blocker link, the save label, and the "Remote save: not confirmed" sentence. Each hook is added only when that meaning is actually rendered.
- On load, an illustrative initial state is applied through the same function. After that, the host replaces it.

## Assumptions

- Predicate and capability names beyond `confirm-root-cause` were not available to this lane. Matching is therefore keyword-based, and any capability without a known label is shown with a label generated from its name.
- `inactive-actions-unavailable` is attached to the "Unavailable now" group whenever at least one action is not executable.
- `blocker-visible` goes on the blocking unknown. If no unknown is blocking, it goes on an outstanding obligation.

## Limitations

- If a fixture uses predicate vocabulary that none of the keywords match, it will show only in the raw state list and not in the domain panels.
- The domain content is fixed sample data. The page is a single static document with no persistence of its own.

## Ordinary verification performed

A headless Chromium (Playwright) script checked the page at 390×844, 820×1180, 1180×820 and 1440×900, using four states I built myself: the initial state, all-legal/synced, conflict/contradiction/inactive, and empty. Results:

- no horizontal document overflow;
- no duplicate ids;
- all buttons, links and summaries have accessible names;
- one `h1`, one `main`, `lang="en"`;
- all 8 regions present;
- `confirm-root-cause` is disabled only when it is not `legal`;
- the reason text is present whenever the action is unavailable;
- the blocker link moves focus to U1;
- no console or page errors.

I visually reviewed screenshots at 1440 and 390. The Percepta verifier and the experimental evaluator were not run.
