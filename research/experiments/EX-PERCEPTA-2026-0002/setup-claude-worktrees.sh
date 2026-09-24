#!/usr/bin/env bash
set -euo pipefail

BASELINE="cb8b2f4324b9016773d4f5a6b97ec086d5b8d637"
ROOT="$(git rev-parse --show-toplevel)"
PARENT="$(dirname "$ROOT")"

echo "Frozen baseline: $BASELINE"
echo
echo "Create isolated worktrees:"
echo
echo "  git worktree add --detach \"$PARENT/percepta-exp-control-b\" $BASELINE"
echo "  git worktree add --detach \"$PARENT/percepta-exp-treatment-b\" $BASELINE"
echo
echo "Then run Claude in the control worktree using:"
echo "  research/experiments/EX-PERCEPTA-2026-0002/RUN-CLAUDE-CONTROL-B.md"
echo
echo "Run a separate fresh Claude session in the treatment worktree using:"
echo "  research/experiments/EX-PERCEPTA-2026-0002/RUN-CLAUDE-TREATMENT-B.md"
echo
echo "Do not reuse conversation/session context between the two Claude runs."
