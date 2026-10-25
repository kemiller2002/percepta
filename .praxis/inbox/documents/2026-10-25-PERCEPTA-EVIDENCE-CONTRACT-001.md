# Pending Praxis intake: PERCEPTA-EVIDENCE-CONTRACT-001

**Status:** Unprocessed source material. This is not a canonical requirement,
decision, evidence record, or proof of verification.

- Submitted: 2026-10-25
- Target repository: `kemiller2002/percepta`
- Source: operator-assigned Echelon Engineering Agent Task 3B-1
- Proposed scope: implement, validate, release, and independently verify a
  versioned evidence contract for Percepta experiments

## Required outcome

Extend Percepta's existing evidence format with an explicitly versioned envelope
that cryptographically binds experiment results to the actual inputs, effective
configuration, source revision, running Percepta artifact, dependency versions,
and durable evidence references. Provide independent validation that distinguishes
valid, invalid, and unverifiable evidence, including the focused positive and
negative regression matrix assigned by the operator.

Follow the repository's documented release workflow. A successful outcome requires
a published consumable Percepta artifact whose evidence generation and validation
are independently retested. Do not classify an unreleased source build as a pass.

## Constraints

- Extend the existing schema and pipeline rather than creating a parallel format.
- Use SHA-256 and canonical serialization; exclude volatile metadata and the
  evidence digest itself from the stable digest calculation.
- Do not modify `echelon-registry`, Praxis, Dokimos, Ordo, or Indy-init.
- Do not introduce a signing or authorization layer.
- Do not create GitHub issues or execute the Causa rehearsal.
