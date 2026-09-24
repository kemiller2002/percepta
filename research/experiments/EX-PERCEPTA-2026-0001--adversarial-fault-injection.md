---
id: EX-PERCEPTA-2026-0001
title: Adversarial fault injection against Percepta verification
research_area: percepta-verification
status: completed
created: 2026-09-24
author_agent: chatgpt
tests_hypotheses:
  - HY-PERCEPTA-2026-0001
  - HY-PERCEPTA-2026-0002
related_theories: []
inputs:
  - .percepta/contracts/indy-init-investigation-workspace.json
  - tests/fixtures/indy-init/index.html
  - tests/fixtures/indy-init/blocked.json
  - tests/fixtures/indy-init/legal.json
outputs:
  - artifacts/percepta/adversarial/experiment-results.json
  - EV-PERCEPTA-2026-0001
---

# Experiment

## Research question

How reliably does the current executable Percepta verifier distinguish a correct rendered implementation from deliberately wrong implementations of the same Indy Init Investigation Workspace contract?

## Hypotheses tested

- HY-PERCEPTA-2026-0001: deterministic checks catch machine-addressable violations.
- HY-PERCEPTA-2026-0002: semantic substitutions can escape current deterministic hooks.

## Variables

Independent variable: one deliberately injected UI mutation.

Dependent variables:

- verification status by evidence category;
- overall `Complete` state;
- whether the predicted evidence category detected the mutation;
- whether the mutation escaped all required deterministic evidence.

Controlled variables:

- same serialized Percepta contract;
- same blocked and legal domain fixtures;
- same Playwright/Chromium runtime;
- same viewport set;
- same verification code;
- one mutation per test case.

## Method

Generate mutated copies of the canonical rendered fixture. Each mutation changes one UI property while preserving all unrelated behavior. Run the normal Playwright adapter against each mutant using the canonical contract and fixtures.

The experiment records the expected detector and observed detector instead of merely asserting that "something failed."

Two semantic-substitution mutations are intentionally designed to preserve deterministic hooks. These are allowed to escape; an escape is experimental evidence rather than a test-harness failure.

## Acceptance criteria

HY-PERCEPTA-2026-0001 is supported when:

1. the unmodified control passes all required verification;
2. at least 90% of machine-addressable mutants fail the expected required category;
3. no machine-addressable mutant that violates a required obligation reports overall completion;
4. semantic visual review cannot override a deterministic required failure.

HY-PERCEPTA-2026-0002 is supported when at least one semantic-substitution mutant preserves overall completion under deterministic-only verification.

## Falsification criteria

HY-PERCEPTA-2026-0001 is weakened or rejected if the detection rate is below 90%, a required failure completes, or visual review overrides deterministic evidence.

HY-PERCEPTA-2026-0002 is rejected if every semantic-substitution mutant is deterministically rejected.

## Controls

Positive control: canonical Indy Init fixture with blocked and legal state fixtures must pass.

Fail-closed control: verification without state fixtures must remain incomplete.

Visual-review precedence control: a passing semantic-review record plus missing deterministic state evidence must remain incomplete.

## Procedure

1. Install the Playwright-matched Chromium.
2. Load the canonical screen contract.
3. Verify the canonical rendered fixture.
4. For each mutation:
   - create an isolated mutated HTML file;
   - run the ordinary Playwright adapter;
   - capture every evidence category;
   - calculate whether required completion would succeed;
   - compare the observed detector with the pre-registered expected detector.
5. Emit a machine-readable experiment-results artifact.
6. Calculate detection and escape rates.
7. Treat every escaped mutation as evidence for a missing invariant, not as permission to change the expected result.
8. Update this experiment and the two hypotheses after the run.

## Pre-registered mutation matrix

| Mutation | Class | Expected result |
|---|---|---|
| missing-observation-region | machine-addressable | Structural fails |
| deleted-falsified-hypothesis | machine-addressable | StateProjection fails |
| hidden-blocking-unknown | machine-addressable | StateProjection fails |
| illegal-action-enabled | machine-addressable | Interaction fails |
| unavailable-reason-missing | machine-addressable | Interaction fails |
| blocker-navigation-missing | machine-addressable | Interaction fails |
| color-only-state | machine-addressable | Structural fails |
| inaccessible-action-name | machine-addressable | Accessibility fails |
| horizontal-overflow | machine-addressable | Responsive fails |
| optimistic-persistence-success | machine-addressable | StateProjection fails |
| chat-only-primary | machine-addressable | Structural fails |
| unsupported-confidence | machine-addressable | Structural fails |
| linearized-competing-hypotheses | machine-addressable | Structural fails |
| wrong-primary-question | semantic-substitution | may escape deterministic checks |
| misleading-unknowns-content | semantic-substitution | may escape deterministic checks |

## Results

Pending.

## Analysis

Pending.

## Threats to validity

- Mutants are synthetic and may not represent the distribution of agent mistakes.
- Marker-aware mutations are easier to target than organically generated incorrect interfaces.
- The canonical fixture itself is simpler than a production application.
- Exact browser behavior is tested only in the pinned Chromium runtime for this experiment.
- Semantic-substitution classification depends on human judgment until a stronger semantic review protocol exists.

## Replication notes

The experiment must be runnable from CI without modifying production contract semantics. The mutation harness belongs under tests and writes results under `artifacts/percepta/adversarial`.

## Conclusion

Pending.

## Registry updates required

- hypotheses registry
- experiments registry
- evidence registry after completion
