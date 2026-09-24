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


## What Percepta governs

Percepta defines and verifies what a user must be able to perceive about application state, available actions, constraints, and unresolved work.

It exists because an agent can build a UI that is accessible, responsive, visually coherent, and composed entirely from approved Forma components while still building the wrong product.

The core rule is:

> The agent may choose implementation details, but it may not silently redefine product meaning.

Percepta contracts cover:

- screen purpose and primary user question
- required semantic regions
- information hierarchy
- domain-state to UI-state projections
- legal and illegal action presentation
- required explanations for unavailable actions
- forbidden semantic UI patterns
- responsive obligations
- accessibility obligations
- verification evidence

## Architecture

```text
Ordo
  governs what is true and legal

Percepta
  governs what must be perceivable

Forma
  governs reusable presentation primitives

ROS / Praxis
  govern work, evidence, metrics, and completion
```

`src/Percepta.Core` contains the dependency-light semantic contract model.

`src/Percepta.Foundation` contains Echelon integrations such as Aegis and may later host adapters for Ordo, ROS, Praxis, Forma, and Limen.

See:

- [Project Charter](PROJECT-CHARTER.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Contract Model](docs/CONTRACT-MODEL.md)
- [Verification Model](docs/VERIFICATION-MODEL.md)
- [Indy Init Investigation Workspace example](examples/indy-init-investigation-workspace.yaml)


## Contract compiler

Percepta.Core validates a typed `ScreenContract` and compiles valid contracts into deterministic agent implementation guidance plus a typed verification plan.

See [Contract Compiler](docs/COMPILER.md) and the typed [Indy Init example](examples/Percepta.Examples/IndyInit.fs).
