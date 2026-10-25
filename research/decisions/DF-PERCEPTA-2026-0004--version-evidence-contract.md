---
id: DF-PERCEPTA-2026-0004
title: Version Percepta experiment evidence and bind it to reproducible inputs and runtime identities
status: accepted
type: decision-record
decision_type: engineering
research_area: percepta-evidence
created: 2026-10-25
updated: 2026-10-25
confidence: high
supporting_evidence:
  - docs/verification/percepta-release-compatibility-2026-10-25.md
related_documents:
  - docs/PERCEPTA-EVIDENCE-CONTRACT.md
  - schemas/percepta-evidence.schema.json
  - docs/PERCEPTA-SEMANTIC-CLI.md
supersedes: []
superseded_by: []
tags:
  - percepta
  - evidence
  - reproducibility
provenance:
  contributions:
    EXE-20261025T181434474Z-443cfc7c:
      operations: [created]
      at: 2026-10-25T22:37:47.000Z
      actor:
        kind: agent
        id: openai/codex
        provider: openai
        model: unknown
        runtime: codex
      reason: "Define Percepta evidence contract 1.0.0 and its independent validation boundary"
derived_from: []
---

# DF-PERCEPTA-2026-0004

## Context

Task 3B demonstrated that Percepta 0.1.0 could produce complete evidence after
a fixture changed because evidence schema version 1 did not record input,
configuration, source, artifact, or dependency identities. An independently
verifiable completion boundary cannot recover identities that the producer
never recorded.

## Authority

The repository owner assigned Task 3B-1 on 2026-10-25 and explicitly required a
published versioned evidence contract binding those identities, with canonical
serialization, independent validation, and no new signing infrastructure.

## Decision

1. Extend the existing semantic evidence envelope as contract `1.0.0`.
2. Use runtime-derived Git, file, assembly, and resolved dependency identities;
   reject evidence generation when a required identity is unavailable.
3. Use SHA-256 over the canonical projections defined in
   `docs/PERCEPTA-EVIDENCE-CONTRACT.md`.
4. Exclude `runId` and `createdAt` from the stable evidence digest, while
   retaining them as execution identity and chronology.
5. Retain the 0.1.x `screen`, `contractSha256`, `generatedAtUtc`, `complete`,
   and `results` projection and reject disagreement with the authoritative
   1.0.0 fields.
6. Publish release artifact metadata inside each existing self-contained
   archive. Authenticity continues to come from existing release checksum and
   GitHub build-provenance verification.
7. Add an independent validator with `VALID`, `INVALID`, and `UNVERIFIABLE`
   results. Only `VALID` permits acceptance.

## Alternatives considered

- **Downstream-only binding.** Rejected because a downstream attestation cannot
  recover missing input or configuration identities.
- **A parallel evidence format.** Rejected because the current envelope and
  consumers can be extended.
- **New signatures or authorization.** Rejected as unnecessary; existing
  release attestations establish the trusted artifact boundary.
- **Caller-supplied hashes.** Rejected because that repeats the unverified
  agent-claim failure.

## Consequences

- Evidence is larger but independently reproducible and inspectable.
- Existing compatibility fields remain readable.
- HTTP(S) targets fail evidence generation until an adapter can bind their
  actual content to a trusted source revision.
- The semantic CLI release advances to `0.2.0`.

## Reversibility

The implementation can evolve through a new schema version. Published evidence
and releases remain immutable and validators continue to reject unsupported
versions.
