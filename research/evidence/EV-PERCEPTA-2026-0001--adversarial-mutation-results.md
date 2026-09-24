---
id: EV-PERCEPTA-2026-0001
title: Adversarial Percepta mutation experiment results
research_area: percepta-verification
evidence_type: test-result
status: accepted
source_title: Percepta Verification run 33
source_author: GitHub Actions / Percepta adversarial harness
source_uri: https://github.com/kemiller2002/percepta/actions/runs/35966872347
source_date: 2026-09-24
retrieved: 2026-09-24
created_by_agent: chatgpt
confidence: high
supports:
  - HY-PERCEPTA-2026-0001
  - HY-PERCEPTA-2026-0002
contradicts: []
related_theories: []
tags:
  - percepta
  - mutation-testing
  - ui-verification
  - semantic-contracts
  - adversarial-testing
---

# Evidence Record

## Evidence summary

The preregistered adversarial mutation experiment executed successfully against the canonical Indy Init Investigation Workspace contract.

The canonical control completed successfully.

All 13 machine-addressable mutations failed the preregistered deterministic evidence category and none reported overall completion.

Both semantic-content substitution mutations preserved all current deterministic hooks and reported overall completion.

## Exact claim supported or contradicted

Supports HY-PERCEPTA-2026-0001:

> For machine-addressable contract obligations, the current executable Percepta verifier detects at least 90% of injected violations and does not report completion when required evidence fails.

Observed: 13/13 detected in the preregistered category, 100.0%, with zero false completions.

Supports HY-PERCEPTA-2026-0002:

> Material semantic substitutions can preserve current deterministic hooks and escape required deterministic verification.

Observed: 2/2 semantic-substitution mutants escaped and reported complete.

## Source provenance

- Repository: `kemiller2002/percepta`
- Commit under test: `6a731c691da5aadc9066097f52ba78ab1b312d0d`
- Workflow: Percepta Verification
- Workflow run: 35966872347
- Browser runtime: Playwright-pinned Chromium installed by the workflow
- Contract: `.percepta/contracts/indy-init-investigation-workspace.json`
- Harness: `tests/Percepta.Adversarial.Tests`

## Relevant excerpt or data

| Mutation | Expected detector | Observed | Complete |
|---|---|---|---|
| missing-observation-region | Structural | Failed | false |
| deleted-falsified-hypothesis | StateProjection | Failed | false |
| hidden-blocking-unknown | StateProjection | Failed | false |
| illegal-action-enabled | Interaction | Failed | false |
| unavailable-reason-missing | Interaction | Failed | false |
| blocker-navigation-missing | Interaction | Failed | false |
| color-only-state | Structural | Failed | false |
| inaccessible-action-name | Accessibility | Failed | false |
| horizontal-overflow | Responsive | Failed | false |
| optimistic-persistence-success | StateProjection | Failed | false |
| chat-only-primary | Structural | Failed | false |
| unsupported-confidence | Structural | Failed | false |
| linearized-competing-hypotheses | Structural | Failed | false |
| wrong-primary-question | no deterministic detector preregistered | Escaped | true |
| misleading-unknowns-content | no deterministic detector preregistered | Escaped | true |

Aggregate results:

- canonical control complete: true
- machine-addressable detection: 13/13, 100.0%
- machine-addressable false completions: 0
- semantic substitutions escaping deterministic checks: 2/2
- HY-PERCEPTA-2026-0001 supported: true
- HY-PERCEPTA-2026-0002 supported: true

## Interpretation

The current Percepta verifier is strong when the product meaning has been translated into explicit machine-addressable obligations.

The experiment also demonstrates a real boundary: correctly marked regions can contain materially wrong language while all required deterministic checks still pass. Therefore semantic-region identity alone is not proof of semantic-content fidelity.

This should not be interpreted as a failure of the deterministic verifier. It identifies the next boundary that requires either stronger contract vocabulary, deterministic content assertions where appropriate, semantic review, or a combination of those mechanisms.

## Limitations

- Mutants were synthetic and intentionally isolated.
- The canonical screen is smaller than a production application.
- Mutation distribution is not representative of actual AI-generated defects.
- Only the pinned Chromium path was exercised.
- The semantic substitution assessment relies on human interpretation of product meaning.
- No false-positive corpus was included beyond the canonical control.

## Counterevidence

An initial pilot run observed 12/13 mutations in the exact predicted category. The blocker-navigation mutant returned `Unavailable` because the mutation removed the DOM element and inadvertently caused the fixture injection function itself to throw on subsequent state applications.

That pilot mutation was invalid as an isolated navigation fault. The mutation was corrected to hide the navigation affordance while preserving the state-injection seam. No preregistered prediction or acceptance threshold changed. The corrected run produced the 13/13 result above.

## Reproduction or verification notes

Run the normal Percepta workflow or execute:

```bash
npm run percepta:install-browser
dotnet run --project tests/Percepta.Adversarial.Tests/Percepta.Adversarial.Tests.fsproj --configuration Release
```

Machine-readable output is generated at:

```text
artifacts/percepta/adversarial/experiment-results.json
```
