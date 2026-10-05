# Frozen product brief: Investigation Workspace semantic generalization

Build a self-contained static browser workspace used by an investigator reasoning about an observed failure.

The investigator must be able to understand what is known, what is uncertain, which explanations remain viable, what evidence conflicts with them, what work is still required, what actions can currently be taken, and whether work has actually been persisted.

The workspace must avoid premature convergence. Multiple explanations may coexist. Evidence can weaken or falsify an explanation without erasing the historical fact that it was considered. Unknown information can block actions. Work may remain unresolved after a corrective action until verification is complete. Local work and remote persistence are distinct facts.

The interface should make important constraints understandable at the point where they matter. When an action cannot be taken, the user should be able to understand why and find the information or work that blocks it.

Do not invent numerical confidence when the domain does not supply it. Do not rely on color alone for state. Do not present chat as the sole authoritative representation of the investigation.

The implementation must respond to the frozen runtime protocol and must remain coherent when several state predicates are simultaneously active. Implementation-time examples are intentionally not supplied.

Required conceptual areas:
- triggering observation and source;
- competing hypotheses and their current status;
- contradictions;
- unresolved unknowns;
- outstanding obligations;
- evidence and its relationship to explanations;
- currently available and unavailable actions;
- persistence status.

Accessibility floor:
- document language;
- main landmark;
- one top-level heading;
- no duplicate DOM ids;
- accessible names for visible controls;
- state meaning not conveyed by color alone.

Responsive floor:
- usable without horizontal document overflow at 390x844, 820x1180, 1180x820, and 1440x900;
- hypotheses, unknowns, and actions remain discoverable at every viewport.

## Neutral instrumentation

The evaluator may locate conceptual areas through:
`data-percepta-region` values:
observation, hypotheses, contradictions, unknowns, obligations, evidence, legal-actions, persistence-state.

The action representing confirmation of a root cause uses:
`data-percepta-capability="confirm-root-cause"`.

Instrumentation attributes are observation points, not domain authority. Hidden markers disconnected from visible/user-operable meaning are invalid.

## Deliverable

Write only `index.html` and `NOTES.md` inside the assigned lane.
