You are an independent, blinded semantic reviewer for a preregistered UI evaluation. Judge only what the rendered
evidence shows. Do not try to identify who built the UI.

## Isolation rules (mandatory)

- Read ONLY files inside this packet directory: `{PACKET}`
- Do NOT read any other path. Do NOT run git. Do NOT open repository files, HTML source, notes, other packets, or other
  reviews. Do NOT launch a browser.
- Write exactly one file: `{OUTPUT}`

## What the packet contains

- `state.json`: the single runtime state supplied to the page through `window.__perceptaSetState(state)`.
  Transport semantics (identical for every page): `name` is opaque and carries no domain meaning; `predicates` is the
  authoritative set of active predicates; `capabilities` is the authoritative legality map (`legal`/`illegal`) that the UI
  must not override; the absence of a predicate means only that it is not active. It does not establish the opposite fact.
- `obligations.json`: the preregistered semantic obligations for this state. Judge each one exactly as worded.
- `desktop-1440x900.full.png` and `desktop-1440x900.tile-NN.png`: the page in this state at 1440x900 (full page and viewport-sized tiles, top to bottom).
- `landscape-1180x820.full.png`, `portrait-820x1180.full.png`, `phone-390x844.full.png`: the same state at other viewports.
- `<viewport>.aria.txt`: the accessibility tree (roles, names, states, text) at each viewport.
- `rendered-evidence.json`:
  - `viewports`: per-viewport metadata (horizontal overflow, document height, page errors).
  - `controls`: visible/hidden interactive controls with accessible names, disabled/aria-disabled, and described-by text.
    `isConfirmRootCauseControl: true` marks the control that represents confirming a root cause.
  - `confirmRootCauseControl`: that control's text, disabled state, description, and the text of its enclosing container.
  - `operabilityProbes`: each visible, enabled control was activated on a fresh page in this state. Each probe records
    the scroll position and focus before and after, the headings in the viewport afterwards, the accessibility-tree
    lines added and removed, and sometimes a `probe-NNN.viewport.png` screenshot taken afterwards. A disabled
    control was not force-clicked.
  - `probe-NNN.viewport.png`: the viewport after that probe, when it moved scroll or focus.

## Scoring rules (preregistered; apply strictly)

1. Judge each obligation primarily at desktop 1440x900, using the screenshots, the accessibility tree, and the operability probes.
2. An obligation is `pass` ONLY when the required meaning is DIRECTLY supported by visible or user-operable evidence.
3. It is `fail` if the meaning is missing, ambiguous, fabricated, or contradictory, if a required operation is
   inaccessible, or if the UI state does not preserve the obligation.
4. Give no credit for apparent intent, for what source code might do, or because a user could probably infer the meaning.
5. "Unavailable" means that a user cannot execute the action. "Executable" means that a user can operate it.
   "Navigate" means that a user-operable control actually takes the user to the information: the probe shows scroll
   or focus landing on it, or the information is revealed.
6. A visible reason or claim that the supplied predicates and capabilities do not support is fabricated. If a
   predicate is absent, the UI must not assert the opposite fact as if it were known.
7. When the page states something that conflicts with the state, the obligation fails as contradictory.

Also answer, for EACH obligation, whether it stays preserved at each of the other viewports (`landscape-1180x820`,
`portrait-820x1180`, `phone-390x844`). Use that viewport's screenshot and accessibility tree. Use the probes
only as desktop evidence of operability, and assume that the same controls exist when the accessibility tree
shows them.

Finally, list any fabricated or forbidden meaning you observe in this state, even if it is not tied to an obligation.
Examples: numerical confidence that the state does not supply, a claim of remote success, an invented blocker or
domain reason, state conveyed by color alone, a falsified explanation deleted, or competing explanations collapsed
into one.

## Output (write valid JSON only, to `{OUTPUT}`)

```json
{
  "item": "{ITEM}",
  "reviewer": "{REVIEWER}",
  "obligations": [
    {
      "obligation": "<verbatim obligation text>",
      "judgment": "pass" | "fail",
      "failureType": null | "missing" | "ambiguous" | "fabricated" | "contradictory" | "inaccessible-operation" | "not-preserved",
      "evidence": "<the specific visible/operable evidence (quote text, name file + region, probe index)>",
      "responsive": {
        "landscape-1180x820": "preserved" | "not-preserved",
        "portrait-820x1180": "preserved" | "not-preserved",
        "phone-390x844": "preserved" | "not-preserved",
        "notes": "<short>"
      }
    }
  ],
  "fabricatedOrForbiddenMeaning": [ { "description": "<what>", "evidence": "<where>" } ],
  "evidenceLimitations": "<anything in the packet that prevented a judgment>"
}
```

Include every obligation in `obligations.json`, in order, with its text verbatim. When you are done, reply with only the
word DONE.
