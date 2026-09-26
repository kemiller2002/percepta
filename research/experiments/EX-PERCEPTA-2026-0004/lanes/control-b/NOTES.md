# Control B implementation notes

## Approach
Built a single self-contained static investigation workspace from only the lane AGENTS.md, frozen product brief, and frozen runtime protocol. The page keeps observation, hypotheses, contradictions, unknowns, obligations, evidence relationships, actions, and persistence visibly separate.

## Design decisions
- Kept three broad competing explanations visible simultaneously so the interface does not prematurely converge.
- Kept contradiction and evidence history visible rather than treating weakened explanations as erased.
- Treated verification and remote persistence as distinct from taking a corrective or confirmation action.
- Exposed the required `window.__perceptaSetState(state)` entry point and treated `predicates` and `capabilities` as authoritative transport facts.
- Never uses the opaque runtime `name` to infer meaning.
- Uses explicit text labels in addition to visual styling, and responsive CSS collapses the grid without horizontal document layout.
- Runtime predicate names are displayed verbatim in the supplied-state area. A small generic name-based classifier also surfaces active predicates near conceptually related regions; this is an independent product interpretation, not a source of capability legality.
- The root-cause control is enabled only when the supplied `confirm-root-cause` capability is exactly `legal`; `illegal` and missing legality both leave it unavailable.

## Interpretation decisions
The runtime protocol does not define human-facing predicate consequences. I therefore do not invent an opposite fact when a predicate is absent. Active predicates remain visible as supplied facts, while recognizable terms such as contradiction, unknown, verification, persistence, or hypothesis are additionally echoed near the corresponding conceptual area. Capability legality is never inferred from those terms.

## Ordinary local checks
- Reviewed the HTML for a document language, one H1, a main landmark, unique IDs, and accessible button names.
- Checked responsive CSS behavior by construction at narrow and wide widths: all primary grid regions collapse to one column below 900px and controls become full-width below 480px.
- Exercised the runtime logic conceptually with empty state, multiple simultaneous predicates, legal/illegal/missing `confirm-root-cause`, and unrelated predicate names.
- Confirmed the runtime state name is unused for domain inference.

## Known limitations
This is a static workspace, so user actions are represented locally and do not perform real remote persistence. Because no semantic contract or generated guidance is permitted for this control lane, predicate-to-region presentation is intentionally generic and cannot assert domain-specific consequences that the frozen inputs do not define.
