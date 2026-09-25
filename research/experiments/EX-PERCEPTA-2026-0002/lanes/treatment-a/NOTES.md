# EX-PERCEPTA-2026-0002 Treatment A notes

## Composition

The Investigation Workspace is a single self-contained HTML document. It uses a two-column investigation/reference layout on wider screens and collapses to a single-column flow on narrow screens. The primary question is the page heading. The triggering observation and competing hypotheses lead the main column; unresolved unknowns, obligations, and legal next actions stay prominent in the companion column. Evidence and contradictions remain adjacent to the explanatory work rather than being presented as a chat transcript.

At narrower widths the layout becomes one column, preserving hypotheses, unknowns, and legal actions in normal document flow with no horizontal scrolling requirement.

## Interpretation of the frozen product brief

The workspace treats investigation as comparison among competing explanations, not a funnel toward one preferred hypothesis. Hypotheses therefore render as peers with explicit textual state labels: Active, Weakened, or Falsified. Falsified hypotheses remain visible. Contradictions, unresolved unknowns, blocking unknowns, obligations, evidence relationships, action legality, and persistence are distinct semantic regions.

Unavailable actions remain visible and disabled. Their reason is shown next to the action and, when a blocker is identifiable, the UI provides an in-page navigation link to that blocker. Persistence uses explicit text for Local, Pending, Synced, Conflicted, and Failed. Pending explicitly says the write is not yet synced.

## Percepta contract use

The canonical contract at `.percepta/contracts/indy-init-investigation-workspace.json` was treated as an implementation constraint. The document implements all declared `data-percepta-region` regions, exposes the `confirm-root-cause` capability, its unavailable-reason hook and blocker-navigation hook, and projects the declared observations onto visible or operable interface elements.

`window.__perceptaSetState(state)` replaces the projected workspace state and re-renders the screen. The renderer accepts the contract's domain concepts without manufacturing confidence percentages. The confirm action is disabled by blocking unknowns or an outstanding verification obligation unless an explicit supplied capability projection determines legality. This UI projection does not claim to be the domain authority.

The normal repository instruction to run executable Percepta verification was intentionally not followed because the experiment's nearest lane instructions and frozen brief explicitly prohibit running the Percepta evaluator or using evaluator feedback.

## Significant decisions

- Falsification is a retained state, not deletion.
- Hypotheses use equal peer cards rather than steps or ranks.
- State is always expressed with words; color is supplementary.
- Blocking unknowns receive both a textual Blocking label and structural emphasis.
- Corrective-action verification remains an obligation and prevents the interface from representing confirmation as available by default.
- Evidence displays explicit hypothesis relationships when supplied.
- The static action buttons demonstrate availability without pretending to perform backend effects.
- Persistence never infers remote success from a pending state.
- Responsive CSS uses intrinsic sizing and a 900px/600px collapse rather than fixed content widths.
- User-facing communication separates current state, evidence, uncertainty, blockers, and next actions in accordance with the available Communication Engineering guidance.

## Assumptions

The supplied state fixtures are JSON objects using the domain concepts named by the product brief and contract. The renderer accepts common field variants for those concepts (for example `state` or `status`, `blocking` or `isBlocking`) while keeping the canonical semantic projection stable.

An explicit `capabilities["confirm-root-cause"]` object, when present, is treated as the strongest supplied legality projection for that capability. Otherwise the static demonstration derives availability conservatively from blocking unknowns and verification obligations.

## Known limitations or deviations

The Visual Engineering files referenced by the repository-level `AGENTS.md` were not present at the frozen baseline commit, so they could not be read or applied without violating the prohibition on using post-baseline implementation guidance. No post-baseline replacement was consulted.

The screen is intentionally self-contained, so repository Forma/Limen/Aegis/Folio runtime packages are not imported. The experiment deliverable requires a standalone static HTML document and permits only `index.html` and `NOTES.md` in the lane. The implementation therefore preserves the required semantics directly within that constrained artifact rather than modifying shared application code.

No backend effects occur. Action execution is represented only as local explanatory feedback.

## Ordinary non-evaluator verification

Verification was limited to ordinary first-pass checks and did not include `percepta verify`, equivalent evaluator commands, experiment results, evidence JSON, or evaluator output.

Checks performed before freeze:
- reviewed the generated HTML for one `main` landmark and one top-level `h1`;
- checked that authored DOM IDs are unique;
- checked that visible buttons and links have accessible text names;
- checked that every state label has textual meaning independent of color;
- checked CSS for narrow single-column behavior and absence of fixed-width content that would require horizontal document overflow;
- checked that hypotheses, unknowns, and legal actions remain in document flow at narrow widths;
- checked JavaScript structure and state rendering logic for the required `window.__perceptaSetState` seam;
- checked that pending persistence wording does not claim remote success;
- checked that disabled actions remain visible with reasons and blocker navigation where applicable.

No experimental evaluator was run and no corrective iteration based on experimental evaluation was performed.
