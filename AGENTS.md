# Percepta agent contract

Percepta is an Echelon Foundry UI-system repository. Repository-installed Echelon lifecycle tools may add their own managed regions to this file. Preserve those regions and this Percepta-specific contract.

## Required engineering stack

Before meaningful work:

1. follow Praxis/ROS work lifecycle and evidence requirements;
2. follow Ordo/State-Directed Engineering for domain state, legal transitions, capabilities, obligations, effects, unknown outcomes, and negative knowledge;
3. read and apply the installed Visual Engineering context for UI decisions;
4. read and apply the installed Communication Engineering context for user-facing communication.

## Required application components

- Use **Forma** (`@echelon-foundry/design-system`) for shared UI tokens, foundations, components, patterns, accessibility contracts, branding, and skins. Do not copy Forma CSS into Percepta.
- Use **Limen** (`@echelon-foundry/typescript-wasm-kernel`) as the browser boundary. Application meaning, state, authorization, and legal transitions stay behind the boundary.
- Use **Aegis** for unexpected operational faults and recovery. Do not expose raw faults directly in the UI; map them to safe presentation state.
- Use **Folio** (`@echelon-foundry/print-components`) for printable/report output. Do not create a parallel print primitive system inside Percepta.

## UI authority rules

Percepta may compose and extend Echelon components, but it must not fork their source or silently reimplement their contracts. If a shared primitive is missing, add the requirement to the owning Echelon component repository rather than creating a Percepta-only substitute without an explicit architectural decision.

Accessibility, mobile behavior, reduced motion, keyboard operation, focus behavior, high-contrast behavior, and print behavior are acceptance criteria, not optional polish.


## Percepta semantic contracts

For any consequential UI creation or modification:

1. read the applicable Percepta screen contract before implementation;
2. preserve its purpose, primary user question, semantic regions, hierarchy, state projections, capability projections, forbidden patterns, and verification obligations;
3. treat layout and implementation technique as flexible only where the contract leaves them flexible;
4. do not substitute a visually plausible interface for required product meaning;
5. do not claim UI work complete until the required Percepta evidence exists or a governing policy records an explicit accepted deviation.

The typed contract model in `src/Percepta.Core` defines Percepta semantics. Human-readable contracts and generated artifacts must not silently contradict that model.

Percepta does not decide domain truth or legal transitions. Ordo/application state remains authoritative for legality; Percepta verifies that the user interface faithfully exposes that meaning.

When an implementation passes existing checks but still communicates materially incorrect product meaning, record the discrepancy as evidence of a missing contract invariant rather than treating it as an unstructured styling preference.

See:

- `requirements/PERCEPTA-CONTRACTS.md`
- `docs/CONTRACT-MODEL.md`
- `docs/VERIFICATION-MODEL.md`
- `examples/indy-init-investigation-workspace.yaml`
