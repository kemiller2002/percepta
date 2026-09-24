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
