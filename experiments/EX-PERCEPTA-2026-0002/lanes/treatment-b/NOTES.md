# Treatment B — composition notes

## Composition

- **Header:** the page title, the primary question in large type, and a short derived answer: which hypotheses are still standing (no ranking, no percentages) and what to test next. It also shows two status chips: investigation state and save state. Each chip has a glyph, a text label, and a border style, so neither depends on color.
- **Board, three columns at 1100px and wider:** observation and unknowns on the left; the competing hypotheses and contradictions in the center; next actions and required-before-progress obligations on the right. Evidence spans the bottom at the medium width and sits at the bottom left at the wide width.
- **Two columns at 760–1099px:** the hypothesis side sits on the left and the blocking and next-action side on the right.
- **Single column below 760px:** the DOM order is observation, then next actions, unknowns, obligations, hypotheses, contradictions and evidence. A jump list at the top links to actions, unknowns (with the blocking count), hypotheses and evidence.
- **Hypotheses:** an unordered comparison grid (`<ul>`, `auto-fit` columns) with no ordinal numbering, labeled "not ranked". Status uses a text badge plus a glyph plus a border style (Active solid, Weakened dashed, Falsified dotted with a struck-through title). Falsified hypotheses stay in the grid and their test action is shown disabled with a reason.
- **Evidence:** each item lists typed relations to named hypotheses: supports (+), weakens (−), conflicts with (≠) and falsifies (✕). Hypothesis cards show the matching counts and cross-links.
- **Unavailable actions:** they stay listed, use the native `disabled` state with `aria-describedby` pointing to a visible reason, and have one or more links to the blocker. A link scrolls to the blocking item and focuses it (`tabindex="-1"` targets; smooth scrolling only when reduced motion is not requested).
- **Save state:** Local, Pending, Synced, Conflicted and Failed each have their own wording. Pending says explicitly that the latest changes are not on GitHub. No UI transition can set Synced; only a supplied state can. Actions taken in the page count as local changes, and Retry after a failure only moves the state to Pending.

## Contract mapping

- `window.__perceptaSetState(state)` accepts the fixture shape `{ name, predicates[], capabilities{} }` (as an object or a JSON string) and resets local UI state. `persistence` (a string or `{ state }`) is optional. Without it, the save state is Pending when `github-write-is-pending` is present and Synced otherwise.
- Legality of `confirm-root-cause` comes only from `capabilities["confirm-root-cause"]`. The page adds explanations (blocking unknown, contradiction, outstanding obligations) but never grants legality on its own. If the capability is illegal and no known predicate explains why, a generic reason is shown and the link goes to the unknowns.
- The `data-percepta-observation` hooks sit on the visible element that carries the meaning, for example the falsified card, its status badge, the reason text and the blocker link. They are emitted only while the matching predicate holds.
- The contradictions and obligations regions are "visible when present" in the contract. They are always in the DOM, and they show an explicit empty state ("none recorded" or "nothing outstanding") when the predicate is absent, so the absence is itself stated. They get an alert border only when items exist.
- "Mark investigation resolved" stays unavailable until a root cause is confirmed and the corrective action is verified. The header never shows "Resolved" while `corrective-action-not-verified` holds.

## Assumptions and deviations

- **Scenario content is invented** to make the brief concrete: `indy init` fails with a lock-file ENOENT on CI, three competing hypotheses, and E1–E5 evidence. Which facts exist and what status each has are derived from the fixture predicates.
- **Forma, Limen, Aegis and Folio are not used.** The brief requires one self-contained static HTML file with no backend, and these packages are not installed in this checkout. `.visual-engineering/` is also absent, so its verify step could not run. The page carries its own tokens, light and dark themes, `forced-colors`, `prefers-reduced-motion` and print rules instead.
- **Code style:** a pure state → view-model projection, pure string renderers and a pure reducer. The only impure code is the DOM paint, the click delegation and focus handling.
- **Output path:** the prompt names `research/experiments/EX-PERCEPTA-2026-0002/lanes/treatment-b/`. At this branch's head, the lane (including its `AGENTS.md`) lives at `experiments/EX-PERCEPTA-2026-0002/lanes/treatment-b/`, after commit 907ba67 moved the execution files, so both deliverables were written into that existing lane directory.
- **Sources read:** only the four listed files, the `.communication-engineering` anti-patterns, and the fixture JSON files (`blocked.json`, `legal.json`) to learn the `__perceptaSetState` input shape. The fixture `index.html`, the other lanes and the verifier implementation were not opened.
- **Checks run:** a local Playwright smoke check of both fixtures at 390×844, 820×1180, 1180×820 and 1440×900. It found no horizontal overflow, no duplicate ids, one `h1`, all 8 regions, and no unnamed controls. The blocker link moved focus to the blocking unknown. This is not the Percepta evaluator.
