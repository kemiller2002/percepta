# Treatment A notes

## Implementation approach
Built a self-contained static investigation workspace with explicit semantic regions for the observation, hypotheses, contradictions, unknowns, obligations, evidence, legal actions, and persistence. Runtime state is projected additively from the supplied predicate set so simultaneous predicates remain coherent rather than selecting a single screen mode.

## Percepta contract influence
The permitted semantic contract directly determined region hierarchy, conditional visibility, the exposed `confirm-root-cause` capability, blocker navigation, retention of falsified hypotheses, non-color state labels, persistence wording, and verification obligations. Capability legality from the runtime map is treated as authoritative and is never overridden by predicate-derived UI inference.

## Semantic/state decisions
- `hypothesis-is-falsified` retains the hypothesis visibly with a textual FALSIFIED state and removes the active presentation.
- `blocking-unknown-exists` exposes a visible blocker; when confirmation is illegal it explains the blocker and offers navigation to it.
- `github-write-is-pending` says Pending and explicitly avoids claiming remote success.
- `corrective-action-not-verified` keeps the investigation open and exposes a verification obligation even when the general obligations predicate is absent.
- `contradictions-present` and `obligations-present` independently control their contract-defined conditional regions.
- Missing predicates do not generate opposite domain claims. Missing capability authorization leaves confirmation unavailable rather than inventing legality.
- Multiple active predicates are rendered together.

## Ordinary local checks
Reviewed the static implementation for a single `h1`, document language and main landmark, unique IDs, named controls, non-color status text, responsive grid collapse, required instrumentation, and `window.__perceptaSetState`. Reasoned through locally constructed combinations including falsified + blocker + pending + unverified + contradictions + obligations, with confirmation both legal and illegal. The experimental evaluator and held-out cases were not used.

## Known limitations
The frozen protocol supplies predicates and capability legality but no concrete observation text, evidence payloads, hypothesis identities, or persistence states beyond the defined predicate semantics. The interface therefore uses deliberately generic records and does not invent missing domain facts. Confirmation has no persistence side effect because the protocol defines legality and projection, not a write transport.
