# Control A implementation notes

## Composition

I used a responsive two-column investigation workspace on larger viewports and a single-column flow on narrow screens. The primary column holds the triggering observation, competing hypotheses, evidence, and actions. The supporting column keeps unresolved unknowns, obligations, and contradictions visible as first-class investigation state. Persistence status is positioned beside the primary question.

## Interpretation and decisions

The screen treats hypotheses as peers rather than steps in a sequence. Each hypothesis carries an explicit textual state label, and falsified hypotheses remain rendered. Blocking unknowns and obligations are separate concepts so the user can distinguish missing knowledge from required work.

Actions are rendered whether available or unavailable. Unavailable actions use disabled controls, an explanatory reason, and, when a blocker can be resolved to an unknown, a link to that blocking information. The confirm-root-cause capability is always represented, including when it must be synthesized because a fixture does not supply it.

Persistence uses explicit labels for local, pending, synced, conflicted, and failed. Pending state explicitly says the remote write is not confirmed, so it is not presented as remote success.

The neutral evaluator attributes are attached to the corresponding visible regions, controls, explanations, navigation, and visible status notices. `window.__perceptaSetState(state)` re-renders the workspace from supplied fixture state.

## Assumptions

Fixture collections are expected to use ordinary object/array structures matching the product concepts. The renderer accepts several common property aliases for labels, state, identifiers, reasons, relationships, and blockers without depending on any external contract.

A small representative local initial state is included so the static document is understandable when opened without a fixture. It is illustrative interface data, not a claim about an actual investigation.

## Known limitations

The page intentionally has no backend and action buttons do not perform domain mutations. The fixture API is designed for display-state replacement, not persistence. Evidence relationships are presented textually rather than as a graph to keep the screen compact and usable at the required narrow viewport.

## Ordinary verification

The implementation was reviewed for a single top-level heading, a main landmark, document language, unique authored IDs, accessible button names, non-color state labels, responsive grid collapse, disabled-action semantics, visible blocker navigation, and pending-persistence wording. CSS constrains the document width and collapses the layout below 760px to avoid horizontal document overflow. No Percepta evaluator or verification command was run.
