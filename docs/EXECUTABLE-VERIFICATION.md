# Executable Verification

Percepta can now move from a typed UI contract to evidence against a rendered application.

## Repository convention

Serialized contracts live under:

```text
.percepta/
  contracts/
    <screen>.json
  baselines/
    <screen>/
  evidence/
```

The canonical Indy Init contract is:

```text
.percepta/contracts/indy-init-investigation-workspace.json
```

## CLI

From the repository root:

```bash
dotnet run --project src/Percepta.Cli/Percepta.Cli.fsproj -- compile
dotnet run --project src/Percepta.Cli/Percepta.Cli.fsproj -- doctor
dotnet run --project src/Percepta.Cli/Percepta.Cli.fsproj -- status
```

Install the Playwright-matched Chromium once per environment:

```bash
dotnet run --project src/Percepta.Cli/Percepta.Cli.fsproj -- install-browser
```

Verify a rendered page:

```bash
dotnet run --project src/Percepta.Cli/Percepta.Cli.fsproj -- verify \
  --contract .percepta/contracts/indy-init-investigation-workspace.json \
  --url tests/fixtures/indy-init/index.html \
  --fixture tests/fixtures/indy-init/blocked.json \
  --fixture tests/fixtures/indy-init/legal.json
```

## Semantic hooks

Percepta's browser adapter recognizes semantic evidence hooks.

```html
<section data-percepta-region="hypotheses">
  ...
</section>

<p data-percepta-observation="blocker-visible">
  Blocking unknown: delivery photo has not been reviewed.
</p>

<button data-percepta-capability="confirm-root-cause" disabled>
  Confirm root cause
</button>

<p data-percepta-unavailable-reason-for="confirm-root-cause">
  Review the delivery photo first.
</p>

<a data-percepta-blocker-link-for="confirm-root-cause" href="#unknowns">
  Go to blocker
</a>
```

These attributes are verification hooks. They do not make the associated statement true.

The application's domain model remains authoritative.

## State fixtures

A testable application may expose:

```javascript
window.__perceptaSetState = state => {
  // Ask the application to project this explicit test state.
};
```

A fixture contains active domain predicates and known capability legality:

```json
{
  "name": "blocked-investigation",
  "predicates": [
    "blocking-unknown-exists"
  ],
  "capabilities": {
    "confirm-root-cause": "illegal"
  }
}
```

If a declared projection has no fixture coverage, state-projection evidence is `Unavailable`, not `Passed`.

## Evidence

Verification writes a JSON evidence report containing each declared obligation.

Required evidence gates completion.

```text
Passed              acceptable
NotApplicable       acceptable with reason
AcceptedDeviation   acceptable with explicit reason
Failed              blocks completion when required
Unavailable         blocks completion when required
```

Supporting evidence can remain unavailable without making required evidence appear complete.

## Responsive evidence

Each contract declares concrete CSS-pixel viewports and semantic regions that must remain visible.

The Playwright adapter:

- sets the exact viewport;
- reapplies the selected state fixture;
- checks required regions;
- checks required responsive observations;
- detects horizontal document overflow;
- captures a screenshot.

## Visual regression

The initial visual-regression adapter uses exact screenshot baselines.

Use `--update-baselines` to establish them intentionally.

Exact screenshot comparison is deliberately supporting evidence by default because browser rendering can be more brittle than semantic checks.

## Semantic visual review

A human or vision-capable reviewer can provide an external evidence document:

```json
{
  "status": "Passed",
  "summary": "Blocking state is visually prominent.",
  "evidenceReferences": [
    "review:123"
  ]
}
```

Supply it with `--semantic-review`.

External visual review cannot override deterministic failures.

## Completion gate

The CLI exits non-zero when required evidence is missing, unavailable, or failed.

This allows ROS, CI, and other governing systems to use Percepta verification as a completion gate without giving Percepta authority over the underlying domain state.
