# Pending Praxis intake: PERCEPTA-REL-COMPAT-001

**Status:** Unprocessed source material. This is not a canonical requirement,
decision, evidence record, or proof of verification.

- Submitted: 2026-10-25
- Target repository: `kemiller2002/percepta`
- Source: operator-assigned Echelon Engineering Agent Task 3B
- Proposed scope: verify the latest published stable Percepta release against
  the compatible Praxis, Ordo, and Dokimos release baseline

## Required outcome

Execute a deterministic Percepta experiment twice from unchanged inputs using
the published artifact, compare the independently produced evidence, and run
the focused negative-evidence matrix. Preserve release versions, release SHAs,
artifact digests, commands, exit codes, input/configuration hashes, evidence
identifiers, and conclusions.

If every required case behaves correctly, record the verified release baseline
without changing implementation. If a reproducible defect exists, identify the
owning contract and make only the smallest in-scope repair; an unreleased source
fix cannot qualify as a pass.

## Constraints

- Reuse the verified Task 3A Dokimos/Praxis evidence; do not repeat it unless
  Percepta exposes a new failure.
- Use isolated fixtures rather than production experiment or Causa data.
- Do not modify Causa requirements or execute the full rehearsal.
- Do not create GitHub issues or add new frameworks without demonstrated need.
