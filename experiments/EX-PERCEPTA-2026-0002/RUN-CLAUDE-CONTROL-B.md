# Claude Run: Control B

This is a preregistered blinded run for EX-PERCEPTA-2026-0002.

Frozen baseline:

```text
cb8b2f4324b9016773d4f5a6b97ec086d5b8d637
```

## Isolation requirement

Start this Claude session from a fresh worktree or clone checked out at the frozen baseline.

The session may read ONLY:

- `research/experiments/EX-PERCEPTA-2026-0002/frozen-product-brief.md`
- `research/experiments/EX-PERCEPTA-2026-0002/lanes/control-b/AGENTS.md`

It may write ONLY:

- `research/experiments/EX-PERCEPTA-2026-0002/lanes/control-b/index.html`
- `research/experiments/EX-PERCEPTA-2026-0002/lanes/control-b/NOTES.md`

Do NOT read:

- `.percepta/**`
- root `AGENTS.md`
- treatment lanes
- control-a
- tests
- prior Percepta experiment outputs
- generated Percepta guidance
- existing Indy Init fixture implementations

## Prompt to Claude

You are Control B in preregistered experiment EX-PERCEPTA-2026-0002.

Read only:
1. `research/experiments/EX-PERCEPTA-2026-0002/lanes/control-b/AGENTS.md`
2. `research/experiments/EX-PERCEPTA-2026-0002/frozen-product-brief.md`

Implement the requested Investigation Workspace from those documents.

Create only:
- `research/experiments/EX-PERCEPTA-2026-0002/lanes/control-b/index.html`
- `research/experiments/EX-PERCEPTA-2026-0002/lanes/control-b/NOTES.md`

Do not inspect any other files or repository history.
Do not inspect Percepta contracts or documentation.
Do not inspect another implementation.
Do not run Percepta or ask for evaluator output.
Do not revise the implementation after any evaluator result.

Make your own UI composition and interaction decisions.

When the first implementation is complete, stop and report only that the two deliverables are complete.
