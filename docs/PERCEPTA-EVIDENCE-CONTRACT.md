# Percepta evidence contract 1.0.0

Percepta semantic CLI releases beginning with `0.2.0` write
`schemas/percepta-evidence.schema.json` with `schemaVersion: "1.0.0"`.
The envelope binds an experiment result to the files it actually read, its
effective contract configuration, the source checkout, the running Percepta
artifact, resolved runtime dependencies, and durable result artifacts.

## Identity and hashing

All digests use SHA-256 and lowercase hexadecimal. File digests cover the raw
file bytes read from disk. Percepta calculates these values; `verify` does not
accept caller-supplied hashes.

Percepta canonical JSON is UTF-8 JSON written with no insignificant whitespace,
the property order shown below, standard `System.Text.Json` escaping, and these
collection rules:

- inputs sort by `(role, path)`;
- dependencies sort case-insensitively by `(dependencyName, dependencyVersion)`;
- evidence references sort by `(kind, path)`;
- evaluation results retain the screen contract's declared order.

`inputDigest` hashes the canonical array of complete input identity objects:
`role`, `path`, `digest`, `digestAlgorithm`. Inputs include the normalized
screen contract, local rendered target, state fixtures, supplied semantic
review, and any visual baselines that existed before execution.

`configurationDigest` hashes this canonical object in order:
`contractReference`, `contractSha256`, `updateBaselines`. `contractSha256`
hashes Percepta's normalized contract serialization, rather than arbitrary
source JSON whitespace.

`evidenceDigest` hashes the canonical stable projection in this order:

1. `schemaVersion`, `experimentId`;
2. `repository`, `sourceRevision`;
3. aggregate input fields and sorted `inputs`;
4. aggregate configuration fields and `configuration`;
5. `artifact`, sorted `dependencies`;
6. `outcome`, `evaluationResults`, sorted `evidenceReferences`;
7. `screen`.

The stable projection excludes `runId`, `createdAt`, `evidenceDigest` itself,
and the legacy compatibility projection. Therefore two executions with
unchanged stable content have the same `evidenceDigest`. The digest establishes
integrity, not producer authenticity.

## Runtime-derived provenance

`verify` derives the source repository and revision from the enclosing Git
checkout. It hashes local input files and the running `Percepta.Cli.dll`, and
reads resolved package versions from the running artifact's `.deps.json`.
Required identities that cannot be established are errors; Percepta does not
emit empty or guessed values.

Contract 1.0.0 requires a local target file. An HTTP(S) target is rejected
because the CLI cannot independently bind it to source bytes with the current
adapter.

Every release archive contains `percepta-artifact.json`. Its artifact and
dependency identities are generated during packaging. Treat that file as
trusted only after verifying the containing release archive against GitHub
release checksums and build-provenance attestation.

## Independent validation

```sh
percepta validate-evidence \
  --evidence evidence.json \
  --source-root /trusted/source/checkout \
  --trusted-artifact-metadata /verified/release/percepta-artifact.json \
  --out validation.json
```

The validator recomputes inputs, normalized configuration, evidence artifacts,
and the stable evidence digest. It compares Git source identity and release
artifact/dependency identities with the supplied trusted boundaries.

| Result | Exit | Meaning |
| --- | ---: | --- |
| `VALID` | 0 | Every required check passed. |
| `INVALID` | 3 | Evidence is missing, malformed, altered, or mismatched. |
| `UNVERIFIABLE` | 4 | A trusted source or release-artifact boundary is unavailable. |

Both `INVALID` and `UNVERIFIABLE` prevent acceptance. `outcome:
"not-satisfied"` is permitted in valid evidence; evidence integrity and
experimental success are separate claims.

## Compatibility

The envelope retains `screen`, `contractSha256`, `generatedAtUtc`, `complete`,
and `results` as a compatibility projection for the 0.1.x status and evidence
reference consumers. Independent validation rejects conflicts between those
properties and `createdAt`, `outcome`, `evaluationResults`, or configuration.

Praxis can continue storing the JSON path as work evidence. Dokimos can bind
that artifact digest to an attestation. Ordo references remain ordinary durable
evidence paths. This contract does not claim those consumers understand the new
fields, and it changes none of their contracts.
