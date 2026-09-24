# Frozen product brief: Indy Init Investigation Workspace

Build one self-contained browser screen for a root-cause investigation workspace. The screen must work as a static HTML document with embedded CSS and JavaScript and must not require a backend.

The user is investigating an observed failure without prematurely converging on an explanation. The primary question shown prominently to the user is:

"What currently explains the observation, and what should be tested next?"

The workspace must preserve and make understandable all of the following product meaning:

- the triggering observation and source;
- multiple competing hypotheses, including active, weakened, and falsified hypotheses;
- falsified hypotheses remain visible rather than disappearing;
- contradictions are visible when present;
- unresolved unknowns are visible, especially blocking unknowns;
- obligations required before progress are visible when present;
- evidence and its relationship to hypotheses are visible;
- legal next actions are discoverable;
- illegal/unavailable actions remain visible but disabled, explain why they are unavailable, and offer navigation to the blocking information when applicable;
- persistence state distinguishes local, pending, synced, conflicted, and failed states;
- the UI must never claim remote persistence succeeded while a write is still pending;
- state must not rely on color alone;
- do not use chat as the only authoritative primary interface;
- do not invent unsupported confidence percentages;
- do not force competing hypotheses into a false linear sequence.

The screen must remain usable at 390x844, 820x1180, 1180x820, and 1440x900 without horizontal document overflow. At minimum, hypotheses, unknowns, and legal actions remain discoverable at every viewport.

Accessibility floor:

- document language;
- main landmark;
- one top-level heading;
- no duplicate DOM ids;
- visible interactive controls have accessible names;
- images, if any, have appropriate alternative text;
- state labels are perceivable without color.

## Neutral evaluator hooks

These attributes are test instrumentation only. They are not domain authority.

Required regions use:
- data-percepta-region="observation"
- data-percepta-region="hypotheses"
- data-percepta-region="contradictions"
- data-percepta-region="unknowns"
- data-percepta-region="obligations"
- data-percepta-region="evidence"
- data-percepta-region="legal-actions"
- data-percepta-region="persistence-state"

The confirm-root-cause action uses:
- data-percepta-capability="confirm-root-cause"
- data-percepta-unavailable-reason-for="confirm-root-cause"
- data-percepta-blocker-link-for="confirm-root-cause"

The document must expose window.__perceptaSetState(state). It receives the supplied JSON fixtures and updates the screen accordingly.

Observation hooks that may be required by fixtures:
- data-percepta-observation="falsified-hypothesis-retained"
- data-percepta-observation="falsified-state-noncolor"
- data-percepta-observation="inactive-actions-unavailable"
- data-percepta-observation="blocker-visible"
- data-percepta-observation="dependent-action-unavailable"
- data-percepta-observation="unavailable-reason-visible"
- data-percepta-observation="blocker-navigation-available"
- data-percepta-observation="persistence-pending-visible"
- data-percepta-observation="no-false-remote-success"
- data-percepta-observation="unresolved-not-resolved"
- data-percepta-observation="verification-obligation-visible"

The test hooks must correspond to actual visible/user-operable meaning. Do not add hidden pass-markers that are disconnected from the interface.

## Deliverable

Write only:
- index.html
- NOTES.md

inside your assigned lane. NOTES.md briefly explains your composition and any assumptions. Do not modify shared code, requirements, contracts, workflows, sibling lanes, or tests.
