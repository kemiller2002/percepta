# Screen Binding and State Injection

`percepta verify` checks one contract against one page. A real application has many screens, its own
screen names and routes, boots asynchronously, and is served over HTTP. `percepta verify-app` runs
every contract in a contracts directory against a served application through an application-owned
**screen binding**.

The binding is the only place a contract's product screen is joined to an application screen.
Percepta never infers that join.

## Verdicts

Every contract gets exactly one verdict:

| Verdict | Meaning | Completes the run |
|---|---|---|
| `PASS` | Bound, screen identity established, all required evidence acceptable | yes |
| `FAIL` | Bound, but identity, state injection, or a required check failed or was unavailable; or the contract is invalid | never |
| `NOT-BOUND` | The binding does not mention the contract | never |
| `NOT-APPLICABLE` | The binding declares the contract out of scope, with a reason | only with `--allow-not-applicable` |

A binding entry naming a contract that does not exist is reported as `ORPHAN-BINDING` and blocks
completion. `NOT-APPLICABLE` is never reported as `PASS`: whether an exclusion is acceptable is a
scope decision for the governing process, recorded in the binding's reason.

## The binding file

The application commits `.percepta/bindings.json`:

```json
{
  "schema": "percepta.screen-bindings/1",
  "application": "causa",
  "stateInjection": true,
  "screens": [
    {
      "contract": "S02-investigation-workspace",
      "appScreen": "investigation-overview",
      "path": "index.html?percepta-test=1#/investigations/00000000-0000-4000-8000-000000000001",
      "fixtures": ["fixtures/s02-blocked.json", "fixtures/s02-confirmable.json"]
    },
    {
      "contract": "S16-counterfactual-sandbox",
      "notApplicable": "Not in Rehearsal 5 scope (decision R5-DEC-004)."
    }
  ]
}
```

- `contract` is the contract's `screen` value.
- `appScreen` is the application's own screen id (see *Screen identity*).
- `path` is resolved against `--base-url` (path, query and hash).
- `fixtures` are state fixtures, relative to the binding file. Use them for every state the contract
  names, including error states such as a write conflict or a rejected token.
- `stateInjection` declares whether the application implements the state hook below.

## Screen identity

The rendered screen root carries exactly one attribute:

```html
<main data-percepta-screen="investigation-overview"> ... </main>
```

Percepta waits up to `--ready-timeout-ms` (default 30000) for it. If it does not appear, or appears
more than once, every check for that contract fails with `SCREEN-IDENTITY` and no other check runs.
Checking a different screen than the one bound would produce meaningless evidence.

## State injection

When `stateInjection` is `true`, the application exposes, in its test configuration only:

```javascript
window.__perceptaSetState = async state => {
  // state = { name, predicates: [...], capabilities: { "<capability>": "legal" | "illegal" } }
  // Project this explicit domain state through the application's normal rendering path.
  // Resolve only after the DOM shows the state. Throw for a predicate you cannot represent.
};
```

- The hook may be synchronous or return a promise; Percepta awaits it.
- It must drive the same projection code the user sees; it must not toggle hooks directly.
- Throwing is the correct response to an unsupported predicate. Percepta then reports every check for
  that screen as `Unavailable` with the error, instead of checking the default state.

Outcomes:

| Binding | Hook at runtime | State-dependent evidence |
|---|---|---|
| `stateInjection: true` | present | exercised with the bound fixtures |
| `stateInjection: true` | absent | `Failed: STATE-INJECTION-MISSING` |
| `stateInjection: false` | any | `Unavailable: STATE-INJECTION-UNSUPPORTED` (blocks completion) |

State-dependent evidence is `state-projection`, and `interaction` when the contract has legality
obligations (enable when legal, disable/explain when illegal, navigate to blocker). A failure found
by the checks themselves is never downgraded.

## Hooks inside a screen

Unchanged from [EXECUTABLE-VERIFICATION.md](EXECUTABLE-VERIFICATION.md):
`data-percepta-region`, `data-percepta-observation`, `data-percepta-capability`,
`data-percepta-unavailable-reason-for`, `data-percepta-blocker-link-for`, `data-percepta-state`.
Ids are the contract's ids. Put hooks on the existing Forma component markup; do not add wrappers
for Percepta.

## Checks

| Obligation | Check |
|---|---|
| Screen identity | exactly one `data-percepta-screen` equal to `appScreen` |
| State injection | hook declared, present and awaited; see above |
| Required actions | capability exposed; enabled when legal; disabled, explained and linked to its blocker when illegal |
| Error states | state projections for every fixture predicate, including error predicates |
| Keyboard | in every fixture state, each visible enabled capability is reachable with Tab from the start of the document and shows a focus indicator; no positive `tabindex` |
| Responsive | required regions and observations visible at each contract breakpoint; no horizontal overflow |
| Component boundaries | each region is exactly one element; regions do not nest; capability hooks sit on native interactive controls (`button`, `a[href]`, form controls, `summary`, or an interactive ARIA role) |
| Basic accessibility | `html[lang]`, a `main` landmark, one `h1`, unique ids, image alt text, accessible names |

## Running

```bash
percepta verify-app --base-url http://127.0.0.1:4321/ \
  [--bindings .percepta/bindings.json] [--contracts .percepta/contracts] \
  [--out .percepta/evidence/application-verification.json] \
  [--allow-not-applicable] [--ready-timeout-ms 30000]
```

Exit codes: `0` complete, `3` incomplete, `2` invalid input.

The report schema is `percepta.application-verification/1`. A served application is not a
revision-bound input, so the report sets `revisionBoundEvidence: false`; it is not a signed
[evidence contract v1](PERCEPTA-EVIDENCE-CONTRACT.md) envelope. `percepta verify` still produces
those for a committed local page.
