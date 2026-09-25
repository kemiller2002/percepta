# EX-PERCEPTA-2026-0003 Treatment A Notes

## Composition / layout

The implementation is a self-contained investigation dashboard with a prominent primary question, a two-column desktop composition, and a single-column responsive composition. The main column preserves the observation, parallel competing hypotheses, conditional contradictions, and evidence. The decision column keeps unknowns, conditional obligations, and next actions close together. Persistence status is visible in the masthead. At phone width, all regions stack vertically without changing the hypotheses' peer relationship.

## Interpretation of the product brief

The workspace treats investigation as maintaining competing explanations rather than selecting a winner early. Active, weakened, and falsified hypotheses are shown simultaneously and use text labels in addition to styling. Evidence states how it relates to hypotheses. Unknowns and obligations are separate because an unresolved fact is not necessarily required work. Unavailable actions remain visible so users can understand the desired action and the boundary preventing it.

## Frozen runtime protocol

`window.__perceptaSetState(state)` consumes only supplied `name`, `predicates[]`, and `capabilities{}`. Predicate membership drives conditional presentation. The `confirm-root-cause` control uses the supplied capability map as authoritative: only exact `legal` enables execution; `illegal` disables it. Predicate inference never overrides capability legality. The state name is displayed as context but not interpreted.

## Percepta contract semantics

The canonical contract directly informed visible behavior. Always-visible regions remain represented. `contradictions-present` exposes contradictions. `obligations-present` exposes obligations. `hypothesis-is-falsified` retains a visibly labeled falsified hypothesis. `blocking-unknown-exists` projects a blocker, an unavailable dependent action when its authoritative capability is not legal, an explanation, and navigation to the blocker. `github-write-is-pending` displays Pending and explicitly states that remote persistence has not succeeded. `corrective-action-not-verified` keeps the investigation unresolved and exposes a verification obligation. The confirm capability remains discoverable whether legal or illegal.

Neutral observation attributes are attached to visible semantic elements only while corresponding predicate-driven meaning is active. They are not hidden pass markers.

## Significant decisions

Hypotheses use equal peer cards to avoid a false linear sequence. No confidence percentages are shown. Persistence uses explicit words and explanatory copy rather than color alone. The primary interface is structured investigation data rather than chat. The action gate explains unavailable confirmation and links to a blocking unknown when applicable.

## Assumptions

The four contract state-projection predicate names are treated as exact identifiers. Additional failed, conflicted, and synced persistence labels accept straightforward predicate names as presentation conveniences; these never affect capability legality. Local is the fallback persistence state.

## Limitations

The runtime carries predicates and capability legality rather than full domain records, so fixed illustrative investigation content receives runtime semantic projections. The static page performs no real remote persistence. Inspect navigates to evidence rather than external data.

## Ordinary verification performed

Before freeze, ordinary checks covered HTML structure, unique IDs, required region and capability hooks, accessible names, document language/main/H1, embedded JavaScript syntax, state projection logic by inspection with constructed runtime states, authoritative capability handling, and responsive CSS intended for all four required viewport classes without document-level fixed-width layout. Percepta verify and the experimental evaluator were not run. No evaluator source, fixtures, evaluator output, sibling lane, prior experiment implementation, PR, or alternative implementation material was inspected.
