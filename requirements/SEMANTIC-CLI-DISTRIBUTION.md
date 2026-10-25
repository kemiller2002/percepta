---
id: RQ-PERCEPTA-SEMCLI-001
title: Distribute the Percepta semantic verification CLI as a versioned, checksum-verified release
status: accepted
version: 1.0.0
owners: [percepta-maintainers]
created: 2026-10-25
updated: 2026-10-25
priority: high
related_documents:
  - docs/PERCEPTA-SEMANTIC-CLI.md
  - docs/PERCEPTA-LIFECYCLE-DISTRIBUTION.md
  - research/decisions/DF-PERCEPTA-2026-0003--release-percepta-semantic-cli.md
  - docs/verification/semantic-cli-investigation-2026-10-24.md
tags: [distribution, release, cli, verification]
derived_from: [INPUT-d5891827f1d5b11b]
provenance:
  contributions:
    EXE-20261025T011347900Z-13edea17:
      operations: [created]
      at: 2026-10-25T01:20:47.000Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Canonical ingestion of claimed intake PERCEPTA-SEMCLI-001"
---

# Distribute the Percepta semantic verification CLI

This record is the canonical ingestion of intake `PERCEPTA-SEMCLI-001`
(`.praxis/processed/INPUT-d5891827f1d5b11b/source/2026-10-24-PERCEPTA-SEMCLI-001.md`).
It came from Causa / Indy Init Rehearsal 3, where UI completion was blocked
because no released, checksum-verified semantic Percepta CLI existed. The
numbered requirements and acceptance criteria are preserved from the intake.
The intake's open decisions are resolved in `DF-PERCEPTA-2026-0003`.

Duplicate check: `WI-PERCEPTA-LIFECYCLE-001` and
`docs/PERCEPTA-LIFECYCLE-DISTRIBUTION.md` cover only `percepta-repo`. They do
not cover the semantic CLI, so this requirement does not duplicate them.

## Requirements

- **PERCEPTA-SEMCLI-001.1 Distributable semantic CLI.** The `percepta` semantic executable (`compile`, `verify`, `status`, `doctor`, `install-browser`) SHALL be published as an immutable, versioned, checksum-verified release artifact that a consuming repository can run without a Percepta source checkout.
- **PERCEPTA-SEMCLI-001.2 Boundary preserved.** The distribution SHALL keep the separation in `docs/PERCEPTA-LIFECYCLE-DISTRIBUTION.md`: `percepta-repo` lifecycle health SHALL NOT be presented as semantic evidence, and the semantic CLI SHALL NOT mutate repository installation state.
- **PERCEPTA-SEMCLI-001.3 Runtime compatibility.** The released executable SHALL run on the runtime the Echelon stack pins (currently .NET 10) without the consumer setting `DOTNET_ROLL_FORWARD`, either by being self-contained or by targeting a supported runtime.
- **PERCEPTA-SEMCLI-001.4 Browser provisioning fails closed.** The release SHALL define how the Playwright driver and the matching Chromium are obtained (bundled driver plus `install-browser`, or an explicit `PLAYWRIGHT_BROWSERS_PATH`), and `doctor`/`verify` SHALL continue to report a missing or unlaunchable browser as failure/`Unavailable`, never as success (PCT-029, PCT-034).
- **PERCEPTA-SEMCLI-001.5 Registry record.** The release SHALL be registrable in `echelon-registry` such that consumers can tell the semantic verifier apart from `percepta-repo` (for example a distinct `systemId`/`executable` or a non-empty `provides`), so an approved profile can pin it.
- **PERCEPTA-SEMCLI-001.6 Consumer invocation documented.** Documentation SHALL show a consumer outside this repository running `percepta verify --contract … --url … --fixture … --out …` and reading the evidence JSON and exit code as a completion gate.

## Acceptance criteria

- A release workflow produces per-platform artifacts plus `checksums.txt`. The intake proposed an operator-created tag as the only trigger. The owner changed this: see `DF-PERCEPTA-2026-0003` §1, which adds a main-push trigger for unreleased versions and keeps the tag-match check.
- On a clean Linux x64 host with only the pinned .NET runtime (or none, if self-contained), the released artifact runs `doctor` (exit 0 with a launchable browser, non-zero without) and runs the canonical Indy Init verification: exit 0 with both fixtures; non-zero with no fixtures; non-zero for each adversarial machine-addressable mutation.
- Evidence emitted by the released artifact validates against `schemas/percepta-evidence.schema.json`.
- `percepta-repo` behaviour and its `percepta-repo-v*` tag namespace are unchanged.

## Cross-repository dependency

`echelon-registry` must add a release record for `systemId: percepta-semantic`,
`executable: percepta` before an approved profile can pin the semantic CLI
(PERCEPTA-SEMCLI-001.5). That change belongs to `echelon-registry` and is not
made here.

## Implementation map

| Requirement | Where it is met |
| --- | --- |
| .1 | `.github/workflows/percepta-semantic-release.yml`, `.github/workflows/percepta-semantic-build.yml`, `distribution/percepta-semantic/package.sh` |
| .2 | Separate tag namespace, version file and release notes; `src/Percepta.Cli` has no installation-state writes; `percepta-repo` is unchanged |
| .3 | Self-contained publish (`package.sh`); container smoke with no .NET (`smoke.sh`) |
| .4 | Bundled `.playwright/` driver checked by `package.sh`; `install-browser`; smoke asserts `doctor` fails without a browser |
| .5 | Distinct `systemId`/`executable` documented in `docs/PERCEPTA-SEMANTIC-CLI.md` and the release notes |
| .6 | `docs/PERCEPTA-SEMANTIC-CLI.md` |
