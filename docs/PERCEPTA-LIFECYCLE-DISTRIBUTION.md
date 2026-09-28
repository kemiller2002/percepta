# Percepta repository lifecycle distribution

Work item: `WI-PERCEPTA-LIFECYCLE-001`

## Boundary

Percepta has two distinct executable concerns and they MUST remain distinct:

- `percepta` is the semantic UI contract compiler/verifier. Its existing `compile`, `verify`, `status`, `doctor`, and `install-browser` commands describe UI contracts and rendered evidence.
- `percepta-repo` is the repository lifecycle capability. Its `init`, `status`, `verify`, `doctor`, and `upgrade` commands describe whether Percepta is correctly installed into a repository.

Repository lifecycle commands must never reinterpret semantic verification results, and semantic commands must never mutate repository installation state.

## Lifecycle-owned state

`percepta-repo` owns:

- `.echelon/percepta.json`, a versioned installation manifest;
- only the bounded `echelon:percepta` managed region inside `AGENTS.md`.

It does not own:

- screen contracts under `.percepta/contracts/`;
- application code;
- Ordo/Praxis state;
- Visual Engineering or Communication Engineering regions;
- browser binaries or Percepta semantic evidence.

Contract material is project/governance input and may be supplied independently by Conditor or another requirements source.

## Required lifecycle behavior

### init

`init` brings an uninstalled/current installation to the current valid state.

- Idempotent.
- May create `AGENTS.md` if absent.
- May append the Percepta region when no Percepta markers exist.
- May restore a missing managed region when the installation manifest proves ownership.
- Refuses to overwrite a modified managed region.
- Does not silently upgrade an older installed version.

### status

Read-only projection: `uninstalled | current | upgrade-required | newer-than-cli | invalid`.

### verify

Read-only. Succeeds only when:

- the installation manifest is parseable and current;
- its configuration version is supported;
- its recorded managed-region hash matches the actual region;
- the actual region equals the current CLI's canonical region.

### doctor

Read-only diagnosis with remediation. It never repairs automatically.

### upgrade

Moves an older valid installation to the current version.

- Refuses a newer installed version.
- Refuses when the previously managed region was locally modified.
- Preserves all bytes outside the bounded Percepta region.
- Rewrites the installation manifest only after the managed region is safely reconciled.
- Re-running at the current version is idempotent repair behavior.

## Distribution

The source of lifecycle truth is F# in `src/Percepta.RepositoryLifecycle`.

A tiny Node launcher under `distribution/percepta-repository-lifecycle/` downloads the matching self-contained binary from the immutable GitHub release for the package version, verifies SHA-256, caches it by version/platform, and forwards stdio/exit status.

The launcher contains no repository lifecycle policy.

## Acceptance

1. Fresh temp repository: `init -> verify -> status current`.
2. Second `init`: byte-for-byte no drift.
3. Existing non-Percepta `AGENTS.md` content is preserved.
4. Locally edited managed region blocks `init` and `upgrade`.
5. Deleting the managed region from an otherwise current installation is repairable with `init`.
6. Semantic `percepta doctor` remains unchanged and separate.
7. Native lifecycle binary builds self-contained for supported release RIDs.
8. Packed launcher resolves the exact release version and verifies checksum.
9. Conditor can execute the lifecycle from an immutable Percepta source commit.
