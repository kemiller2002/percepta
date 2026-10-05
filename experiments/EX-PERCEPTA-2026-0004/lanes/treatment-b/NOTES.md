# EX-PERCEPTA-2026-0004 — treatment-b implementation notes

Independent first-pass implementation (Claude, treatment condition).

- Baseline: `f474c449c2bc84da5707bb123c36500490875869`
- Branch: `experiment/percepta-0004-claude-treatment-b`
- Output: `index.html` (single self-contained static page, no external resources) and this file.

## Inputs used

- `lanes/treatment-b/AGENTS.md`
- `frozen-product-brief.md`
- `frozen-runtime-protocol.md`
- `.percepta/contracts/indy-init-investigation-workspace.json` (treatment input)
- Repository-root `AGENTS.md`, read only to interpret the contract (it names the verification hooks
  `data-percepta-observation`, `data-percepta-unavailable-reason-for`, `data-percepta-blocker-link-for`).

Not inspected: held-out semantic cases, evaluator code, semantic-review prompts, evaluation
artifacts/results, sibling lanes, and EX-PERCEPTA-2026-0002/0003 implementations or results.
The experimental evaluator and Percepta verify tooling were not run.

## Runtime protocol handling

- `window.__perceptaSetState(state)` normalises the input immutably, builds a view model with pure
  functions, and re-renders every region. `state.name` is ignored for meaning.
- `predicates` is treated as the complete set of active facts. Any combination is supported; each
  predicate adds its own projection independently, so they compose.
- `capabilities` is authoritative. A control is enabled only when its value is `"legal"`. Predicates
  never enable or disable a control; they only explain *why* an already-illegal action is unavailable.
  A capability missing from the map is shown as "Legality not supplied" and not offered.
  `confirm-root-cause` is always exposed; other capability names in the map are rendered generically.
- Absent predicates do not produce opposite claims. For example, without `github-write-is-pending` the
  remote status reads "Not reported" (never "Synced"); without `hypothesis-is-falsified` hypotheses read
  "Under consideration" with an explicit "this is not a confirmation" note. No state ever shows the
  investigation as closed.
- Unrecognised predicates are listed verbatim under "Other state facts" instead of being interpreted.

## Contract projections

| Predicate | Projection |
|---|---|
| `hypothesis-is-falsified` | H3 is kept in the list, struck through, with a dashed border and a "✕ Falsified — retained for the record" text badge; falsifying evidence E3 appears in Evidence. |
| `blocking-unknown-exists` | Blocking unknown (`#unknown-blocker`) is shown first in Unknowns with a "⛔ Blocking" text badge; if `confirm-root-cause` is illegal, its reason names the unknown and the first reason link (`data-percepta-blocker-link-for`) moves focus to it. |
| `github-write-is-pending` | Persistence shows "⧗ Pending", says the write is not confirmed, and makes no remote-success claim. The local working copy is reported separately from remote persistence. |
| `corrective-action-not-verified` | Status stays "Open / Not closed"; a verification obligation is shown in Obligations (the Obligations region appears for it even without `obligations-present`). |
| `contradictions-present` | Contradictions region appears (visibility `when` per contract); H1 is marked "contradicted" but not removed; conflicting evidence E4 appears. |
| `obligations-present` | Obligations region appears with a required-work item. |

Contradictions and Obligations follow the contract's conditional visibility; all other regions are always
shown. The quick-navigation bar lists regions currently shown and flags blocking, pending, and falsified
states in text.

## Design decisions

- No numeric confidence anywhere; hypotheses are shown side by side and not ranked.
- State is never shown by color alone: every state has a text badge plus a glyph, and borders vary in
  style (dashed, double, solid).
- Layout: one column on phones (observation, status, hypotheses, unknowns, actions first), two columns
  from 800px, three columns from 1360px. The `<nav>` bar is sticky, so every region stays reachable.
- Accessibility: `lang="en"`, one `<h1>`, a `<main>` landmark, labelled sections, unique ids, disabled
  buttons use `aria-describedby` to point at their reason, links move focus to their target, and
  `prefers-reduced-motion` and dark mode are respected.
- No chat interface.
- Code style: pure renderers (view model → HTML string) and frozen data; DOM mutation is confined to
  one `render` effect and one delegated click handler.

## Ordinary checks performed

I used Playwright/Chromium with states I built myself (none taken from evaluation material): no
predicates with an empty capability map; every contract predicate plus one unknown predicate at once;
a blocking unknown with the action illegal; a blocking unknown with the action legal. I ran each state
at 390×844, 820×1180, 1180×820 and 1440×900 and checked:

- no horizontal overflow;
- no duplicate ids;
- exactly one `h1` and one `main`;
- every button and link has a name;
- the button's disabled state matches the capability map;
- the reason and blocker link appear only when the action is unavailable;
- the blocker link moves focus to the blocking unknown;
- there are no page errors.

I also looked at screenshots taken at desktop and phone widths.
