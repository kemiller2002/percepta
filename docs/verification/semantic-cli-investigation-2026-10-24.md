# Semantic CLI investigation verification — 2026-10-24

Work item: `WI-0009`
Baseline: `main` @ `1eb43a9`. Host: Linux x64, .NET SDK 10.0.402 / runtime 10.0.13 only, Playwright Chromium at `/opt/pw-browsers`.

Commands run and observed results (CLI built with `dotnet build src/Percepta.Cli/Percepta.Cli.fsproj -c Release`, 0 warnings, 0 errors):

| Check | Result |
|---|---|
| `Percepta.Cli.dll doctor` without `DOTNET_ROLL_FORWARD` | does not start: framework `Microsoft.NETCore.App` 8.0.0 not found |
| `doctor` with `DOTNET_ROLL_FORWARD=Major`, `PLAYWRIGHT_BROWSERS_PATH=/opt/pw-browsers` | exit 0; contract OK; Chromium launched |
| canonical Indy Init `verify` with `blocked.json` and `legal.json` fixtures plus semantic review | exit 0; six required obligations Passed; `visual-regression` (supporting) Unavailable; `Complete: true` |
| same `verify` with no fixtures | exit 3; `state-projection` and `interaction` Unavailable; `Complete: false` |
| `dotnet run --project tests/Percepta.Core.Tests` | "all checks passed", exit 0 |
| `dotnet run --project tests/Percepta.Adversarial.Tests` | 13/13 machine-addressable mutations Failed (0 false completions); 2/2 semantic substitutions escaped, as the experiment expects; exit 0 |
| `git tag` / `git ls-remote --tags origin` | only `percepta-repo-v0.1.0` |
| `./praxis sync check --start` | 0 ahead, 0 behind `origin/main` |

Conclusion: semantic UI verification is implemented on `main` but unreleased. The gap is distribution, submitted as `.praxis/inbox/documents/2026-10-24-PERCEPTA-SEMCLI-001.md`. No release, tag, or release metadata was created or changed.
