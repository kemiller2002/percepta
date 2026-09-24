---
id: EX-PERCEPTA-2026-0002
title: Independent-session Percepta treatment versus natural-language control
research_area: percepta-verification
status: active
created: 2026-09-24
author_agent: chatgpt
tests_hypotheses:
  - HY-PERCEPTA-2026-0003
  - HY-PERCEPTA-2026-0004
related_theories: []
inputs:
  - research/experiments/EX-PERCEPTA-2026-0002/frozen-product-brief.md
  - .percepta/contracts/indy-init-investigation-workspace.json
outputs:
  - research/experiments/EX-PERCEPTA-2026-0002/results.json
---

# Experiment

## Research question

Do independently executed implementation sessions given an explicit Percepta contract produce better first-pass semantic compliance than sessions given the same product requirements in ordinary natural language?

## Design

Four independent GitHub Copilot cloud-agent sessions start from the same frozen repository commit before any implementation output is merged.

- control-a and control-b receive the frozen natural-language product brief plus neutral test-hook requirements and are prohibited from reading Percepta contracts, generated Percepta guidance, sibling lanes, prior implementation artifacts, or this experiment's results;
- treatment-a and treatment-b receive the same frozen product brief and are additionally required to use the canonical Percepta contract and executable verification guidance.

Each session writes only inside its assigned lane.

This is an independent-session, single-agent-platform pilot. It does not establish cross-provider generality.

## Primary outcome

First-pass required Percepta completion before any corrective iteration.

## Secondary outcomes

- required evidence categories that are Failed or Unavailable;
- count of required evidence categories passing;
- number of corrective iterations needed to reach completion;
- semantic-review defects;
- implementation diversity across compliant outputs;
- changed-file count and observable churn;
- elapsed session time when GitHub exposes it.

## Acceptance and interpretation

HY-PERCEPTA-2026-0003 is supported in this pilot when the treatment group has a higher first-pass required-evidence pass rate than the control group. With n=2 per group, the result is descriptive and must not be treated as statistically conclusive.

HY-PERCEPTA-2026-0004 is supported when at least two first-pass treatment implementations differ materially in layout/composition while both satisfy required semantic evidence.

## Falsification

- If controls match or exceed treatment first-pass compliance, HY-PERCEPTA-2026-0003 is not supported by this pilot.
- If all passing treatment implementations are materially the same composition, HY-PERCEPTA-2026-0004 is not supported by this pilot.
- Missing/Unavailable evidence is never silently converted to Passed.

## Blinding

Control agents MUST NOT inspect the Percepta contract or treatment artifacts.

Semantic review, if performed by an agent, must receive anonymized outputs without condition labels and without deterministic Percepta result summaries.

## Frozen baseline

The baseline commit SHALL be recorded before agent assignment. No lane may consume output from another lane.

## Results

Pending.

## Threats to validity

- only two sessions per condition;
- one coding-agent platform;
- model selection may be automatic and may vary;
- all sessions share the same repository-level engineering culture outside the blinded Percepta-specific material;
- control sessions still receive neutral verification-hook requirements so the same deterministic evaluator can observe them;
- agent implementation quality may be affected by platform policies unrelated to Percepta.
