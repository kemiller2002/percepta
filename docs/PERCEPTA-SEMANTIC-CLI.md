# Using the released Percepta semantic CLI

Requirement: `RQ-PERCEPTA-SEMCLI-001` (`requirements/SEMANTIC-CLI-DISTRIBUTION.md`).
Decision: `DF-PERCEPTA-2026-0003`.

`percepta` checks a rendered page against a Percepta screen contract and writes
an evidence report. Use the report and the exit code to decide whether UI work
is complete. You do not need a Percepta source checkout or a .NET installation.

`percepta` is not `percepta-repo`. `percepta-repo` only reports whether Percepta
is installed correctly in a repository. A healthy `percepta-repo verify` says
nothing about whether a UI meets its contract, so do not use it as UI evidence.
`percepta` never changes repository installation state.

## 1. Download

Releases are tagged `percepta-semantic-v<version>` on
<https://github.com/kemiller2002/percepta/releases>. Each release has one
archive per platform and a `checksums.txt` file:

| Platform | Asset |
| --- | --- |
| Linux x64 | `percepta-semantic-<version>-linux-x64.tar.gz` |
| Linux arm64 | `percepta-semantic-<version>-linux-arm64.tar.gz` |
| macOS x64 | `percepta-semantic-<version>-osx-x64.tar.gz` |
| macOS arm64 | `percepta-semantic-<version>-osx-arm64.tar.gz` |
| Windows x64 | `percepta-semantic-<version>-win-x64.zip` |

```sh
version=0.1.0
rid=linux-x64
base="https://github.com/kemiller2002/percepta/releases/download/percepta-semantic-v$version"
curl -fsSLO "$base/percepta-semantic-$version-$rid.tar.gz"
curl -fsSLO "$base/checksums.txt"
```

## 2. Verify the checksum

Do not run the archive if this step fails.

```sh
sha256sum --check --ignore-missing checksums.txt         # Linux
shasum -a 256 --check --ignore-missing checksums.txt     # macOS
```

On Windows, compare `(Get-FileHash percepta-semantic-<version>-win-x64.zip -Algorithm SHA256).Hash`
with the matching line in `checksums.txt`.

Each archive also has a GitHub build-provenance attestation. To check it:
`gh attestation verify percepta-semantic-$version-$rid.tar.gz --repo kemiller2002/percepta`.

## 3. Install

Extract the whole archive. The executable needs the `.playwright/` directory
next to it, so do not copy `percepta` out on its own.

```sh
mkdir -p "$HOME/.local/share/percepta-semantic"
tar -xzf "percepta-semantic-$version-$rid.tar.gz" -C "$HOME/.local/share/percepta-semantic"
export PATH="$HOME/.local/share/percepta-semantic/percepta-semantic-$version-$rid:$PATH"
percepta version        # prints: percepta <version>+<source commit>
```

The archive is a self-contained .NET 8 publish, so it runs whether the host
has .NET 10, another .NET version, or no .NET at all. Do not set
`DOTNET_ROLL_FORWARD`.

Host prerequisites:

- Linux: a glibc distribution with ICU (`libicu`) installed, as for any
  self-contained .NET application.
- macOS: the archive is not notarized. If Gatekeeper blocks it, remove the
  quarantine attribute from the extracted directory:
  `xattr -dr com.apple.quarantine <directory>`.

## 4. Provision the browser

```sh
percepta install-browser
percepta doctor --contract .percepta/contracts/<screen>.json
```

`install-browser` downloads the Chromium build that matches the bundled
Playwright driver. On Linux it also installs Chromium's system packages, which
needs root or `sudo`. To keep browsers in a known location, set
`PLAYWRIGHT_BROWSERS_PATH` for both `install-browser` and every later command.

`doctor` exits `0` only when every contract parses and Chromium launches. If
no browser is available, it prints `Browser FAILED` and exits `4`. In that
state `verify` reports the browser checks as `Unavailable` and exits `3`. A
missing browser never counts as a pass.

## 5. Verify a screen

Put contracts in `.percepta/contracts/` in your repository. `percepta` uses the
nearest parent directory that contains `.percepta/contracts/` (or `.git`) as
the root, and resolves relative paths against that root.

```sh
percepta verify \
  --contract .percepta/contracts/<screen>.json \
  --url http://localhost:5173/<route>       \
  --fixture tests/percepta/<state-a>.json    \
  --fixture tests/percepta/<state-b>.json    \
  --out artifacts/percepta/<screen>.evidence.json
```

- `--url` accepts an `http(s)://` or `file://` URL, or a path to an HTML file.
- Pass one `--fixture` for each state the contract declares. Without fixtures,
  state-projection and interaction evidence is `Unavailable` and the run
  fails.
- `--semantic-review PATH` attaches external semantic-review evidence. It is
  supporting evidence and never overrides a deterministic failure.
- `--screenshots DIR`, `--baselines DIR` and `--update-baselines` control
  visual-regression evidence.

## 6. Use the result as a completion gate

| Exit code | Meaning |
| --- | --- |
| `0` | Every required evidence category is acceptable. `complete` is `true`. |
| `3` | At least one required category is `Failed` or `Unavailable`. Not complete. |
| `2` | Invalid input, such as a missing `--url` or an invalid contract. |
| `1` | Unknown command (usage was printed). |
| `10` | Unexpected operational fault. The message includes a reference ID. |

Treat any exit code other than `0` as not complete. Also read the report: it
follows `schemas/percepta-evidence.schema.json` (`schemaVersion` 1). The
`complete` field must be `true`, and each entry in `results` has `requirement`,
`kind`, `required`, `status` (`Passed`, `Failed`, `Unavailable`,
`NotApplicable` or `AcceptedDeviation`), `summary`, `source` and
`evidenceReferences`.

```sh
set +e
percepta verify --contract "$contract" --url "$url" \
  --fixture "$fixture_a" --fixture "$fixture_b" --out "$evidence"
code=$?
set -e
if [ "$code" -ne 0 ] || [ "$(jq -r .complete "$evidence")" != "true" ]; then
  echo "Percepta evidence is not acceptable (exit $code):" >&2
  jq -r '.results[] | select(.required and (.status != "Passed" and .status != "NotApplicable" and .status != "AcceptedDeviation"))
         | "  \(.requirement): \(.status) - \(.summary)"' "$evidence" >&2
  exit 1
fi
```

Keep the evidence file, the exit code and the release version
(`percepta version`) with the work item. A source build run with
`dotnet run --project src/Percepta.Cli` is not released evidence.

## Release line and registry

- Tags `percepta-semantic-v<version>`. This version line is separate from
  `percepta-repo` (`percepta-repo-v*`, `distribution/percepta-repository-lifecycle/package.json`).
- The version is set in `distribution/percepta-semantic/version`. The release
  workflow (`.github/workflows/percepta-semantic-release.yml`) publishes when
  a push to `main` carries a version that has no release yet, when an
  operator pushes the matching tag, or when it is run manually.
- Planned `echelon-registry` record: `systemId: percepta-semantic`,
  `executable: percepta`. That record is created in `echelon-registry`, not
  here.
- There is no Node launcher yet. That is a planned follow-up.

## What the release is tested on

On every pull request that changes the CLI, and before every release, CI
builds the linux-x64 archive and runs it in an `ubuntu:24.04` container with no
.NET installed (`distribution/percepta-semantic/smoke.sh`). The run checks:

- `doctor` fails without a browser;
- `install-browser`, then `doctor` succeeds;
- the canonical Indy Init verification exits `0` with both fixtures and fails
  without them;
- every machine-addressable adversarial mutant from
  `tests/Percepta.Adversarial.Tests` fails verification;
- every evidence report validates against the schema.

On the other platforms, CI checks only that `percepta version` starts on a
native runner (linux-arm64, osx-arm64, osx-x64, win-x64). Browser verification
on those platforms has not been tested in CI.
