# Control A implementation notes

## Approach
Built a dependency-free, single-file Investigation Workspace. The runtime function treats `predicates` as the authoritative active fact set and `capabilities` as the authoritative legality map. Predicate labels are humanized and classified by neutral lexical cues into the required visible investigation regions. Unrecognized predicates remain visible under Evidence rather than being discarded.

## Design decisions
- Kept competing hypotheses, contradictions, unknowns, obligations, evidence, actions, observation, and persistence simultaneously visible so combined states do not replace one another.
- Rendered every supplied legal/illegal capability as a visible control. Illegal capabilities are disabled and explicitly labeled unavailable. `confirm-root-cause` receives the required instrumentation when supplied.
- Capability legality comes only from the runtime map; predicate interpretation never changes it.
- Avoided numerical confidence and color-only meaning. Text labels accompany status styling.
- Used a responsive CSS grid that collapses to one column on narrow screens.
- Did not use the opaque runtime `name` for domain interpretation.

## Interpretation decisions
Because the frozen protocol does not define predicate semantics, predicate names are interpreted conservatively through lexical categories (for example, terms indicating unknowns, contradictions, verification obligations, evidence, hypotheses, or observations). Absence of a predicate is not rendered as its opposite. Predicates that cannot be categorized safely are retained as active evidence/facts. Persistence is stated cautiously unless an active predicate lexically indicates local/unsaved or persisted/saved state.

## Ordinary local checks
- Reviewed the HTML for one `h1`, a `main` landmark, document language, unique static IDs, and accessible names on generated buttons.
- Checked the responsive grid rules for the four required viewport classes; narrow layouts collapse without fixed-width content.
- Checked empty state, multiple simultaneous predicates, unknown predicate fallback, multiple capabilities, legal and illegal actions, and `confirm-root-cause` instrumentation by inspection of the runtime rendering paths.
- Confirmed the runtime state name is ignored and capability legality is never inferred from predicates.
- Did not run or inspect the experimental evaluator.

## Known limitations
Lexical classification cannot know domain semantics that are not encoded in the permitted inputs. A predicate whose name contains no recognizable conceptual cue is displayed under Evidence so it remains visible, but its more specific relationship may be unknown. Illegal-action explanations can point to visible blocking context but cannot assert a specific blocker unless the runtime predicates themselves expose it.
