# Pending Praxis intake: PERCEPTA-SEMCLI-001

**Status:** Unprocessed source material. This is **not** a canonical requirement record, work item, approved decision, release, or proof of implementation.

- Submitted for later Praxis ingestion: 2026-10-24
- Target repository: `kemiller2002/percepta`
- Source: Causa / Indy Init Rehearsal 3 (`indy-init-rehearsal-003`), `docs/REHEARSAL-003-REPORT.md` §3 and §"next actions" item 4 ("Release the Percepta semantic CLI"), plus an investigation of `percepta` `main` @ `1eb43a9`
- Proposed scope identifier: `PERCEPTA-SEMCLI-001` (no work-item ID has been allocated)
- Submitted by: agent (Claude Code); not reviewed or approved by an operator

## Ingestion instructions

Use the supported `praxis inbox claim`, `praxis inbox derive`, and `praxis inbox complete` lifecycle when a Praxis runtime is available. Check first for existing or duplicate work (in particular `WI-PERCEPTA-LIFECYCLE-001` and `docs/PERCEPTA-LIFECYCLE-DISTRIBUTION.md`, which deliberately cover only `percepta-repo`). Preserve the numbered requirements and acceptance criteria below, record the open decisions as explicit unknowns/decisions rather than resolving them silently, and preserve the cross-repository dependency on `echelon-registry`. Ingestion is separate from implementation and from cutting any release.

## Observed baseline (2026-10-24, `main` @ `1eb43a9`)

Semantic UI verification is **implemented but unreleased**:

- Requirements PCT-001..PCT-035 (`requirements/PERCEPTA-CONTRACTS.md`) define the contract model, `compile`/`verify`/`status`/`doctor` CLI (PCT-023), Playwright adapter (PCT-025), fixture state seam (PCT-026), hooks (PCT-027), versioned JSON evidence (PCT-028, `schemas/percepta-evidence.schema.json`), fail-closed completion (PCT-029), responsive (PCT-030), screenshots/baselines (PCT-031), and the accessibility floor (PCT-033).
- Source implements them: `src/Percepta.Core`, `src/Percepta.Adapter.Playwright/BrowserVerification.fs`, `src/Percepta.Cli/Program.fs` (added 2026-09-24, `9646457`).
- Locally observed: `percepta doctor` exit 0; canonical Indy Init `verify` with both fixtures exit 0 (`Complete: true`); the same run without fixtures exit 3 (state-projection and interaction `Unavailable`); `tests/Percepta.Adversarial.Tests` detected 13/13 machine-addressable mutations as `Failed` with 0 false completions; `Percepta.Core.Tests` passed. CI (`.github/workflows/percepta-core.yml`) runs the same proof.
- The only tag is `percepta-repo-v0.1.0` (local and `origin`). `.github/workflows/percepta-repository-lifecycle-release.yml` publishes only `percepta-repo-*` binaries; its release notes state "Semantic Percepta verification remains in the separate percepta CLI".
- `echelon-registry/releases/percepta/0.1.0.release.json` registers `systemId: percepta`, `executable: percepta-repo`, `provides: []`.
- The only supported invocation of semantic verification is `dotnet run --project src/Percepta.Cli/...` from a Percepta source checkout. The projects target `net8.0`; on a host with only the .NET 10 runtime (the Causa stack pins SDK 10.0.402) the built CLI does not start ("You must install or update .NET") unless `DOTNET_ROLL_FORWARD=Major` is set.

Consequence: a consuming repository whose `AGENTS.md` carries the `echelon:percepta` region ("do not claim UI work complete while required Percepta evidence is Failed or Unavailable") cannot obtain Percepta evidence from an approved, versioned, checksum-verified artifact, so UI completion is blocked (Rehearsal 3 gate "UI completion under AGENTS.md (Percepta)": BLOCKED).

## Requirements

- **PERCEPTA-SEMCLI-001.1 Distributable semantic CLI.** The `percepta` semantic executable (`compile`, `verify`, `status`, `doctor`, `install-browser`) SHALL be published as an immutable, versioned, checksum-verified release artifact that a consuming repository can run without a Percepta source checkout.
- **PERCEPTA-SEMCLI-001.2 Boundary preserved.** The distribution SHALL keep the separation in `docs/PERCEPTA-LIFECYCLE-DISTRIBUTION.md`: `percepta-repo` lifecycle health SHALL NOT be presented as semantic evidence, and the semantic CLI SHALL NOT mutate repository installation state.
- **PERCEPTA-SEMCLI-001.3 Runtime compatibility.** The released executable SHALL run on the runtime the Echelon stack pins (currently .NET 10) without the consumer setting `DOTNET_ROLL_FORWARD`, either by being self-contained or by targeting a supported runtime.
- **PERCEPTA-SEMCLI-001.4 Browser provisioning fails closed.** The release SHALL define how the Playwright driver and the matching Chromium are obtained (bundled driver plus `install-browser`, or an explicit `PLAYWRIGHT_BROWSERS_PATH`), and `doctor`/`verify` SHALL continue to report a missing or unlaunchable browser as failure/`Unavailable`, never as success (PCT-029, PCT-034).
- **PERCEPTA-SEMCLI-001.5 Registry record.** The release SHALL be registrable in `echelon-registry` such that consumers can tell the semantic verifier apart from `percepta-repo` (for example a distinct `systemId`/`executable` or a non-empty `provides`), so an approved profile can pin it.
- **PERCEPTA-SEMCLI-001.6 Consumer invocation documented.** Documentation SHALL show a consumer outside this repository running `percepta verify --contract … --url … --fixture … --out …` and reading the evidence JSON and exit code as a completion gate.

## Acceptance criteria

- A release workflow, triggered only by an explicit operator-created tag, produces per-platform artifacts plus `checksums.txt`; no release is cut as part of ingesting this input.
- On a clean Linux x64 host with only the pinned .NET runtime (or none, if self-contained), the released artifact runs `doctor` (exit 0 with a launchable browser, non-zero without) and runs the canonical Indy Init verification: exit 0 with both fixtures; non-zero with no fixtures; non-zero for each adversarial machine-addressable mutation.
- Evidence emitted by the released artifact validates against `schemas/percepta-evidence.schema.json`.
- `percepta-repo` behaviour and its `percepta-repo-v*` tag namespace are unchanged.

## Open decisions (not made by this repository as of `1eb43a9`)

1. Tag namespace and version line for the semantic CLI (e.g. `percepta-v<version>`), and whether it shares `package.json` `version` with `percepta-repo`.
2. Artifact form: single-file self-contained binary versus archive (the Playwright .NET driver ships as a `.playwright/` directory beside the assembly, which affects single-file publishing).
3. Target framework: keep `net8.0` and publish self-contained, or move projects to `net10.0`.
4. Whether a Node launcher (as for `percepta-repo`) is provided.
5. Registry modelling: new `systemId` versus a second executable under `percepta`.

## Draft operator waiver (for consuming repositories until a release exists)

This text is a draft for an operator to accept, amend, or reject. An agent cannot grant it.

> **Waiver PERCEPTA-SEMCLI-001-W1 (draft).** Scope: the `echelon:percepta` rule "do not claim UI work complete while required Percepta evidence is Failed or Unavailable" in `<consumer repository>`, for UI work items `<list>` only. Reason: no released, registry-approved `percepta` semantic CLI exists; only `percepta-repo` 0.1.0 (lifecycle) is released. Accepted substitute: either (a) evidence produced by the unreleased `percepta verify` built from `kemiller2002/percepta` at a pinned commit `<sha>`, with the commit, build command, contract files, fixtures, `evidence.json` and exit code retained, explicitly labelled "unreleased Percepta source build, not released evidence"; or (b) the repository's own Playwright and axe browser evidence, explicitly labelled "not Percepta evidence". Not waived: any required obligation that is Failed; authoring of screen contracts where a contract is required; any claim that Percepta evidence is released or Passed. Expiry: the earlier of the first approved `percepta` semantic CLI release in the consumer's pinned registry profile, or `<date>`. On expiry, the waived work items must be re-verified with the released CLI before release acceptance. Granted by: `<operator>`, `<date>`.
