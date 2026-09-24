# Echelon baseline requirements

## Purpose

Percepta is the UI-system consumer and composition layer for the Echelon Foundry application family. Its baseline must remain mechanically verifiable and must use the shared Echelon capabilities rather than reimplement them locally.

## Repository capabilities

Percepta SHALL install and verify:

- Praxis 3.4.0 through the native Echelon toolchain;
- Ordo 1.4.0 through the native Echelon toolchain;
- Visual Engineering lifecycle context;
- Communication Engineering lifecycle context;
- Limen lifecycle enforcement.

The native versions SHALL be pinned in `.echelon/toolchain.json`. Installed lifecycle manifests and generated integration state SHALL be committed when the owning tool marks them committable.

## Runtime and application packages

Percepta SHALL consume:

- Forma / `@echelon-foundry/design-system`;
- Folio / `@echelon-foundry/print-components`;
- Limen / `@echelon-foundry/typescript-wasm-kernel`;
- `EchelonFoundry.Aegis.Core`;
- `EchelonFoundry.Aegis.Integration.GitHub`;
- `EchelonFoundry.Aegis.Store.GitHub`.

Versions SHALL be pinned by package manifests and lockfiles. Source files from those repositories SHALL NOT be copied into Percepta as a substitute for package consumption.

## Verification

A baseline change is complete only when all applicable lifecycle verification commands succeed and the Aegis-enabled F# foundation project restores and builds.

The bootstrap process SHALL fail rather than silently omit a component whose package or lifecycle installer is unavailable.

## Ownership boundaries

Forma owns shared visual primitives and design tokens. Folio owns shared print primitives. Limen owns browser capability boundaries. Aegis owns unexpected operational-fault modelling and recovery contracts. Ordo owns engineering state/transition methodology. Praxis owns repository work protocol and durable execution evidence. Visual Engineering and Communication Engineering own their evidence-bounded implementation contexts.

Percepta owns composition, UI-system-specific domain models, demonstrations, integration, and any Percepta-specific components that are not candidates for the shared libraries.
