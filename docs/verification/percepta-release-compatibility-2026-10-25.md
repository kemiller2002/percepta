# Percepta Release Compatibility Verification

Date: 2026-10-25

Work item: `PERCEPTA-REL-COMPAT-001`

Fixture: `PERCEPTA-REL-COMPAT-001-CANONICAL`

Verdict: `BLOCKED`

## Release set

| Component | Version | Release SHA | Selected artifact SHA-256 |
| --- | --- | --- | --- |
| Percepta semantic CLI | `0.1.0` (`percepta-semantic-v0.1.0`) | `3c3889d037402a5bf84b50267249ce7ead4689d4` | `735e8acf99fa53d3802ea7cfbff7fa955079cbea553b94e74c463e72a2cf3b36` |
| Praxis | `3.20.0` (`v3.20.0`) | `ee6430af3c03aa958c4e5663d65269457269db3d` | `56b61dff5c147464e45dcb4f090ec0cfb827e4bd457692a47b0534faf82a1149` |
| Ordo | `1.17.0` (`v1.17.0`) | `bb43c26c106f4cbe972234636c5c67edc235c188` | `7426f6f31e577ee61a842cec031405c51f9d6dbf58c39a1d59c8a41feedf3d2d` |
| Dokimos | `0.7.0` (`dokimos-v0.7.0`) | `04b5f856367b47a087285109c1fc5923ec351389` | `45d8bb4d3ac0da8b9ba58a1e29d369eee9ef546b73860057488961d447c6ef69` |

The Percepta archive checksum matched both the publisher `checksums.txt` and
the GitHub release asset digest. `gh attestation verify` also accepted the
downloaded archive. The binary reported
`percepta 0.1.0+3c3889d037402a5bf84b50267249ce7ead4689d4`.

Package source: self-contained native archives from the GitHub release. The
semantic CLI release has no NuGet or npm launcher and no `echelon.release/v2`
manifest. Its documented public contracts are Percepta screen-contract
`schemaVersion` 1 and `percepta-evidence.schema.json` `schemaVersion` 1.

Percepta's repository lifecycle manifest pins Praxis `3.7.2` and Ordo `1.5.0`.
The semantic release itself declares no Praxis, Ordo, or Dokimos version range
and does not ingest Praxis work state, Ordo `ecir/1`, or Dokimos completion
attestations. Those systems remain downstream/authority boundaries. Therefore
the latest stack versions above are operational context, not a formally
declared Percepta release dependency set.

## Controlled experiment

Two independent workspaces ran the published Percepta binary with the same
release-source inputs:

```text
percepta verify \
  --contract .percepta/contracts/indy-init-investigation-workspace.json \
  --url fixtures/index.html \
  --fixture fixtures/blocked.json \
  --fixture fixtures/legal.json \
  --out evidence.json
```

Both runs exited `0`, produced `schemaVersion: 1`, reported `complete: true`,
and passed every required category: contract validation, structural,
state-projection, interaction, accessibility, and responsive verification.
Optional visual-regression and semantic-review categories were `Unavailable`,
which is allowed because the contract marks them non-required.

Input identities:

- experiment configuration:
  `sha256:d930347ec55dca9c6de4d88e8ce11cebe1bad8b7bb6f7b19ed1bca918045bd49`
- screen contract:
  `sha256:265a8d05f6f7c3f14938819a18e3d3e6f239f03865b88b18784dab4af7b2516d`
- HTML target:
  `sha256:e4c119ac018f480942caafd7d7ad4c0390ca24ae6983eeef206fdf8c88cb641b`
- blocked-state fixture:
  `sha256:690221af229b8e71cfe5e422a14b78dfd0f7b683134ed1de5d19f13a9438b6f9`
- legal-state fixture:
  `sha256:0f0972e56cfd1acd8d3d6e0da44517846d7ababe24e0e8c49a0828e5b7db22ec`

Raw evidence digests were different because the reports contain timestamps
and workspace-local paths. After removing only those permitted differences,
both reports had the identical semantic digest
`sha256:3bcbe70c62dd50fc40d69890bb791e1d5e0a97eb78852ab7120c0feba01fe3db`.
All four generated screenshots were also byte-identical between runs.
`percepta status --evidence` accepted both persisted reports.

## Negative matrix

| Test | Expected | Actual | Exit | Result |
| --- | --- | --- | ---: | --- |
| D1 valid evidence | ACCEPT | `complete: true`; all required results passed | 0 | PASS |
| D2 missing required evidence | REJECT | fixtures omitted; state-projection and interaction `Unavailable`; `complete: false` | 3 | PASS |
| D3 mismatched source SHA | REJECT | Praxis/Dokimos boundary previously rejected Task 3A B7 with `PRX.COMPLETION.HEAD_MISMATCH` and Dokimos attestation-binding mismatch | 3 / 4 | PASS (reused boundary evidence) |
| D4 mismatched input hash | REJECT | blocked fixture hash changed to `0fc5791eac6aec775ca78fa9edcb7eb1d4f8436800c3439065796c42147a20df`; Percepta still emitted `complete: true` | 0 | **FAIL** |
| D5 invalid experiment configuration | REJECT | required contract property `screen` removed; rejected before execution | 2 | PASS |
| D6 incompatible contract version | REJECT | `schemaVersion: 999`; `Unsupported contract schemaVersion` | 2 | PASS |
| D7 failed mandatory evaluation | REJECT | required region hook removed; structural/responsive `Failed`; `complete: false` | 3 | PASS |
| D8 unsupported success claim | REJECT | Praxis/Dokimos boundary previously rejected Task 3A B9 with `PRX.COMPLETION.RECEIPT_MISSING` and `PRX.COMPLETION.VERIFIED_COMPLETION_REQUIRED` | 3 | PASS (reused boundary evidence) |

The D3 and D8 results reuse the unchanged published-artifact evidence from
Praxis work item `WI-0103`, evidence commit
`e73a0a0ff040c6dcd33de605156f2cc186530467`. Re-executing Task 3A was neither
necessary nor authorized by the focused scope.

## Verified incompatibilities

### 1. Evidence does not bind all reproducibility inputs

`schemas/percepta-evidence.schema.json` version 1 permits only:
`schemaVersion`, `screen`, `contractSha256`, `generatedAtUtc`, `complete`, and
`results`. The implementation's `EvidenceReport` has the same fields. It does
not record or validate:

- the rendered target digest;
- fixture/input digests;
- the declared experiment configuration digest;
- the source revision;
- the Percepta artifact identity; or
- Praxis, Ordo, and Dokimos dependency identities.

Consequently D4 cannot be rejected by the published artifact. A downstream
attestation can bind the produced file to a source revision, but it cannot
recover input identities that the evidence package never recorded. This is an
evidence-packaging/reproducibility contract gap owned by Percepta for the scope
of Task 3B.

Fixing it requires an explicit input/configuration contract, versioned evidence
schema evolution, compatibility rules, tests, and a new published artifact.
Adding those capabilities during this focused task would redesign/expand the
release rather than repair an isolated implementation error. Percepta `main`
and the release tag are the same commit; no existing unreleased fix is present.

### 2. Semantic release is not in the Echelon Registry

The live Registry contains only
`releases/percepta/0.1.0.release.json`, which identifies
`percepta-repo-v0.1.0` and executable `percepta-repo`. No
`percepta-semantic` record exists. Percepta decision
`DF-PERCEPTA-2026-0003` explicitly requires a separate
`systemId: percepta-semantic`, executable `percepta` record before an approved
profile can pin the release. This blocker belongs to `echelon-registry`.

## Changes and repair decision

No Percepta implementation, schema, test, or release was changed. This task
adds only the claimed Praxis intake, this verification report, and its durable
raw evidence artifacts. No release was published.

The selected artifact demonstrates deterministic semantic UI verification, but
it is not a compatible consumable release for the broader Task 3B experiment
contract until the Percepta evidence-binding gap is resolved in a versioned
release and the semantic CLI is admitted to the Echelon Registry.
