# Percepta Evidence Contract Release Verification

Date: 2026-10-26

Work item: `PERCEPTA-EVIDENCE-CONTRACT-001`

Contract: `percepta.evidence/1.0.0`

Verdict: `PASS`

## Published release

| Field | Verified value |
| --- | --- |
| Repository | `kemiller2002/percepta` |
| Release | `percepta-semantic-v0.2.1` |
| Source SHA | `8b78df803abb401e2e77320203a2b944d919f6c7` |
| Tested archive | `percepta-semantic-0.2.1-osx-arm64.tar.gz` |
| Archive SHA-256 | `66f3db5f61569fff5b76167f683cf83f463e09d51ec632ccb6d07596b74f0135` |
| Runtime artifact SHA-256 | `98a787bbf792b0ee540d2f8b8f982d446889c3cb5f8e362e13b6fe44ff72e565` |
| Release workflow | `40281896075` |
| Release URL | `https://github.com/kemiller2002/percepta/releases/tag/percepta-semantic-v0.2.1` |

The downloaded archive digest matched both GitHub's release-asset digest and
the published `checksums.txt`. The release workflow built five native archives,
created a signed GitHub artifact attestation for each, verified archive
provenance before publication, passed the no-.NET consumer smoke, and created
the immutable release. GitHub's attestation API returned one attestation for
the tested archive digest. The local `gh 2.86.0` Sigstore verifier could not
initialize its public-good verifier; this local client limitation did not
replace or weaken the workflow's successful cryptographic provenance gate.

The downloaded executable reported:

```text
percepta 0.2.1+8b78df803abb401e2e77320203a2b944d919f6c7
```

Its runtime `artifact-metadata` command independently recomputed the CLI DLL
digest above and reported these resolved dependencies:

| Dependency | Version | SHA-256 |
| --- | --- | --- |
| `EchelonFoundry.Aegis.Core` | `1.0.0` | `294fac167ec6fb9ad88c79c16a4893dbae93e951a8cfb27940343a96d6e6b0d7` |
| `FSharp.Core` | `10.1.401` | `39b0b7f06c11bedd93f94b6f101fffd645f1c4437a5a6d3ee79259f855aeb11a` |
| `Microsoft.Playwright` | `1.63.0` | `7e55d0c8a1b76ecf1b94477acec749f89e84a1747b857624ea0e5e57b69b6947` |

## Independent experiment

Two detached worktrees at the release SHA ran the downloaded binary. No result
from the first workspace was copied into or reused by the second.

```text
percepta verify \
  --contract .percepta/contracts/indy-init-investigation-workspace.json \
  --url tests/fixtures/indy-init/index.html \
  --fixture tests/fixtures/indy-init/blocked.json \
  --fixture tests/fixtures/indy-init/legal.json \
  --semantic-review tests/fixtures/indy-init/semantic-review.json \
  --screenshots artifacts/percepta/published-verification/screenshots \
  --out artifacts/percepta/published-verification/evidence.json
```

Both commands exited `0`, emitted contract `1.0.0`, recorded outcome
`satisfied`, and produced independently validated evidence packages.

| Identity | Run A | Run B | Result |
| --- | --- | --- | --- |
| Run ID | `628b4241-ee3c-42c5-b6e1-5884874ecb49` | `97caf94c-013a-4d23-933d-30daa92b4bc3` | intentionally different |
| Source SHA | `8b78df803abb401e2e77320203a2b944d919f6c7` | same | PASS |
| Input digest | `65cf9fe248520bcae7f0a8809d514cb4440c15876a11d8d624762cbc9ad8aa3a` | same | PASS |
| Configuration digest | `ba61e2ca7b1b855836c4916a4f0e874bd9bd37104245ba5439a524bfeca72bdc` | same | PASS |
| Artifact digest | `98a787bbf792b0ee540d2f8b8f982d446889c3cb5f8e362e13b6fe44ff72e565` | same | PASS |
| Dependency identities | three identities above | same | PASS |
| Evaluation results | six required passed; semantic review passed; optional visual regression unavailable | same | PASS |
| Outcome | `satisfied` | `satisfied` | PASS |
| Stable evidence digest | `235203efcaf59bc2371188890f2d6dc69e7c017764247e9eb23806348f5464e6` | same | PASS |

The serialized evidence files differ only in permitted execution metadata
(`runId` and `createdAt`). Their file SHA-256 values were
`8b3d37270853d047c5b8b0d83fd2e0570350c94d99d943125ca58756dac12dae`
and `dc3dd85f1186ba835ea6a19150c53a58ab5ab9868792881bb40adbe257835d68`.

## Independent validation matrix

The released `validate-evidence` command and
`distribution/percepta-semantic/evidence-contract-smoke.sh` executed the
positive, reproducibility, and mutation cases. The matrix command exited `0`;
its generated result document had SHA-256
`6b1c99dd2bb172c4d50c7b0d28011daac73872695233c2b0ec04cc47c1c732b0`.

| Test | Expected | Actual | Exit | Result |
| --- | --- | --- | ---: | --- |
| P1 valid evidence | VALID | VALID | 0 | PASS |
| P2 deterministic evidence | equivalent | equivalent | 0 | PASS |
| P3 reproducible conclusions | equivalent | equivalent | 0 | PASS |
| N1 missing input digest | INVALID | INVALID | 3 | PASS |
| N2 incorrect input digest | INVALID | INVALID | 3 | PASS |
| N3 missing configuration digest | INVALID | INVALID | 3 | PASS |
| N4 incorrect configuration digest | INVALID | INVALID | 3 | PASS |
| N5 wrong source revision | INVALID | INVALID | 3 | PASS |
| N6 wrong artifact digest | INVALID | INVALID | 3 | PASS |
| N7 missing dependency identity | INVALID | INVALID | 3 | PASS |
| N8 unsupported schema version | INVALID | INVALID | 3 | PASS |
| N9 modified evaluation result | INVALID | INVALID | 3 | PASS |
| N10 missing evidence artifact | INVALID | INVALID | 3 | PASS |
| N11 duplicate dependency identity | INVALID | INVALID | 3 | PASS |
| N12 agent success claim only | INVALID | INVALID | 3 | PASS |
| N13 unavailable trusted artifact metadata | UNVERIFIABLE | UNVERIFIABLE | 4 | PASS |
| N14 altered evidence digest | INVALID | INVALID | 3 | PASS |

`INVALID` and `UNVERIFIABLE` both returned nonzero and prevented acceptance.
A valid evidence package may still contain a negative experiment outcome;
validity and experimental success remain separate fields.

## Repair history

Published `0.2.0` exposed a reproducible defect: evaluation results retained
absolute checkout paths, so identical experiments in different workspaces had
different stable evidence digests (`3e34a5f...` and `a023eb60...`). Percepta
owned this failure because it occurred in canonical evidence generation.

The minimal patch normalizes repository-local evaluation references before
canonical hashing and runs the package smoke's second execution in a distinct
Git worktree. Source verification produced the same stable digest in both
workspaces and passed all 18 focused cases before publication. PR `#41` then
passed the complete release matrix and merged as the release SHA above.

No Praxis, Dokimos, Ordo, Indy-init, or Echelon Registry files were modified.

