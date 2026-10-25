---
id: DF-PERCEPTA-2026-0003
title: Release the Percepta semantic CLI as self-contained per-platform archives on its own percepta-semantic-v* version line
status: accepted
type: decision-record
decision_type: engineering
research_area: percepta-distribution
created: 2026-10-25
updated: 2026-10-25
confidence: high
supporting_evidence: []
related_documents:
  - requirements/SEMANTIC-CLI-DISTRIBUTION.md
  - docs/PERCEPTA-SEMANTIC-CLI.md
  - docs/PERCEPTA-LIFECYCLE-DISTRIBUTION.md
  - docs/verification/semantic-cli-investigation-2026-10-24.md
supersedes: []
superseded_by: []
tags:
  - percepta
  - distribution
  - release
provenance:
  contributions:
    EXE-20261025T011347900Z-13edea17:
      operations: [created]
      at: 2026-10-25T01:20:43.000Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Record owner decisions (2026-10-24: 'Release it', 'Do what you recommend') for the semantic CLI release"
derived_from: [RQ-PERCEPTA-SEMCLI-001]
---

# DF-PERCEPTA-2026-0003

## Context

Intake `PERCEPTA-SEMCLI-001` (`RQ-PERCEPTA-SEMCLI-001`) found that semantic UI
verification is implemented on `main` but has never been released. Only
`percepta-repo-v0.1.0` exists. The intake gave consumers two options: an
operator waiver (`PERCEPTA-SEMCLI-001-W1`, draft), or a release. It left five
questions open.

## Authority

On 2026-10-24 the repository owner (kemiller2002) told the implementing agent
"Release it", choosing a release over the waiver. For the five open questions
the owner said "Do what you recommend", which approved the recommendations
below. The owner also approved merging the implementing pull requests. The
agent recorded this decision; it did not make it.

## Decision

1. **Version line and tags.** The semantic CLI has its own version line,
   starting at `0.1.0`, held in `distribution/percepta-semantic/version`. Tags
   are `percepta-semantic-v<version>`. It does not share
   `distribution/percepta-repository-lifecycle/package.json` or the
   `percepta-repo-v*` namespace.
   *Trigger:* the intake proposed an operator-created tag as the only
   trigger. Agents cannot push tags from their sandbox, so the owner approved
   the same pattern `percepta-repo` uses. A push to `main` whose version has
   no release publishes it. A pushed `percepta-semantic-v*` tag must match the
   version file. `workflow_dispatch` is also available. An existing release is
   never republished, so a release still needs a deliberate version change on
   `main`.
2. **Artifact form.** One archive per runtime identifier (`.tar.gz` for
   linux/osx, `.zip` for Windows) of a self-contained, non-single-file publish
   of `src/Percepta.Cli`. Each archive includes the Playwright `.playwright/`
   driver directory and renames the app host to `percepta`. A `checksums.txt`
   (SHA-256) and a build-provenance attestation per archive accompany each
   release. RIDs: linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64.
3. **Target framework.** Projects stay on `net8.0`. Self-contained publishing
   removes the host runtime dependency, which meets PERCEPTA-SEMCLI-001.3 for
   .NET 10 hosts and hosts without .NET. Moving to `net10.0` is a separate
   decision.
4. **No Node launcher** in this release. A launcher like the one `percepta-repo`
   has is a follow-up.
5. **Registry modelling.** A new `systemId: percepta-semantic` with
   `executable: percepta`, separate from `systemId: percepta`
   (`executable: percepta-repo`). The record is created in `echelon-registry`,
   not in this repository.

Also: the CLI gains a read-only `percepta version` command so a consumer or
registry check can confirm which release it is running. No existing command
changed.

## Alternatives considered

- **Operator waiver instead of a release.** The owner rejected this.
- **Share the `percepta-repo` version.** Rejected. Lifecycle and semantic
  verification change for different reasons. One version would release one
  whenever the other changed, and would blur the boundary that
  `docs/PERCEPTA-LIFECYCLE-DISTRIBUTION.md` requires.
- **Single-file binary.** Rejected. The Playwright driver is a directory of
  Node files that must be on disk next to the assembly. Bundling it in a
  single file would need extraction logic that does not exist today.
- **Retarget to `net10.0`.** Deferred. This is not needed once the publish is
  self-contained, and it would change the build of every project and the
  experiments that pin `net8.0`.
- **Tag-only trigger.** Not possible from the agent sandbox. The owner
  accepted the main-push path, with the tag-match check kept.

## Consequences

- Consumers download, check the SHA-256, extract, run `install-browser`, and
  use `percepta verify`'s exit code and evidence JSON as the completion gate
  (`docs/PERCEPTA-SEMANTIC-CLI.md`).
- Archives are larger than a framework-dependent build because they include
  the .NET runtime and the Playwright driver.
- End-to-end browser verification of the released artifact is tested in CI
  only for linux-x64, in a container with no .NET. Other RIDs are checked only
  for start-up on native runners. macOS archives are signed ad hoc, not
  notarized.
- `echelon-registry` still needs the `percepta-semantic` record before
  consumers can pin the release through an approved profile.

## Reversibility

Partly reversible. Future versions can change artifact form, framework or
trigger. A published release is immutable, and its tag namespace is
effectively permanent once consumers depend on it.
