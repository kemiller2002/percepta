# Percepta

Percepta is the Echelon Foundry UI system repository.

This repository is bootstrapped with the Echelon engineering stack and is intended to consume shared Echelon UI/runtime packages rather than copy their implementation.

## Required Echelon stack

- **Praxis** — repository operating system and work protocol
- **Ordo** — State-Directed Engineering methodology and repository lifecycle
- **Aegis** — typed operational fault handling for .NET/F#
- **Forma** — zero-runtime Echelon web design system
- **Folio** — print/document component system
- **Limen** — browser boundary and lifecycle enforcement
- **Visual Engineering** — evidence-bounded UI research context
- **Communication Engineering** — evidence-bounded communication context

The repository bootstrap workflow installs, initializes, verifies, and pins the applicable capabilities. Runtime package versions are locked by npm/NuGet manifests after bootstrap.

## Bootstrap

The repository is designed to bootstrap itself in GitHub Actions. The bootstrap must:

1. install the native Echelon toolchain;
2. initialize Praxis and Ordo;
3. install the application-facing npm packages;
4. initialize Visual Engineering, Communication Engineering, and Limen;
5. restore the Aegis-enabled F# foundation project;
6. run strict verification for every repository capability;
7. commit generated manifests, lockfiles, and managed integration files.

Do not copy Forma, Folio, Limen, Aegis, or engineering-context source into this repository. Consume their versioned packages/lifecycle installers and preserve their ownership boundaries.
