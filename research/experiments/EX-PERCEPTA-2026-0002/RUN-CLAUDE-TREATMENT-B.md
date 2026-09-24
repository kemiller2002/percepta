# Claude Run: Treatment B

This is a preregistered Percepta-treatment run for EX-PERCEPTA-2026-0002.

Frozen baseline:

```text
cb8b2f4324b9016773d4f5a6b97ec086d5b8d637
```

## Isolation requirement

Start this Claude session from a separate fresh worktree or clone checked out at the frozen baseline.

The session may read:

- root `AGENTS.md`
- `.percepta/contracts/indy-init-investigation-workspace.json`
- `research/experiments/EX-PERCEPTA-2026-0002/frozen-product-brief.md`
- `research/experiments/EX-PERCEPTA-2026-0002/lanes/treatment-b/AGENTS.md`

It may write ONLY:

- `research/experiments/EX-PERCEPTA-2026-0002/lanes/treatment-b/index.html`
- `research/experiments/EX-PERCEPTA-2026-0002/lanes/treatment-b/NOTES.md`

Do NOT inspect:

- control lanes
- treatment-a
- existing fixture implementations
- prior experiment result artifacts
- another agent's output

## Prompt to Claude

You are Treatment B in preregistered experiment EX-PERCEPTA-2026-0002.

Read:
1. `research/experiments/EX-PERCEPTA-2026-0002/lanes/treatment-b/AGENTS.md`
2. `research/experiments/EX-PERCEPTA-2026-0002/frozen-product-brief.md`
3. root `AGENTS.md`
4. `.percepta/contracts/indy-init-investigation-workspace.json`

Implement the Investigation Workspace while treating the Percepta contract as an implementation constraint.

Create only:
- `research/experiments/EX-PERCEPTA-2026-0002/lanes/treatment-b/index.html`
- `research/experiments/EX-PERCEPTA-2026-0002/lanes/treatment-b/NOTES.md`

Do not inspect any control implementation, treatment-a, prior experiment outputs, or the existing canonical fixture implementation.
Do not ask for evaluator results.
Do not revise the implementation after any evaluator result.

Percepta constrains product meaning, not layout. Make your own visual composition and interaction decisions.

When the first implementation is complete, stop and report only that the two deliverables are complete.
