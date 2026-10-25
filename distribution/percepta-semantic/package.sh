#!/usr/bin/env bash
# Publish the Percepta semantic CLI (`percepta`) for one runtime identifier and
# archive it (PERCEPTA-SEMCLI-001, DF-PERCEPTA-2026-0003).
#
#   distribution/percepta-semantic/package.sh RID OUTPUT_DIR
#
# The archive holds a self-contained (no .NET runtime required) publish of
# src/Percepta.Cli, including the Playwright driver directory `.playwright/`
# beside the assembly. It is deliberately not a single-file publish: the
# Playwright driver must stay on disk next to the executable.
#
# Both the release workflow and the pull-request smoke job call this script,
# so the smoke job exercises the same archive layout that is released.
# It never touches percepta-repo or repository installation state.
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "usage: $0 RID OUTPUT_DIR" >&2
  exit 2
fi

rid="$1"
output_dir="$2"
script_dir="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
repo_root="$(CDPATH= cd -- "$script_dir/../.." && pwd)"
version="$(tr -d ' \r\n' < "$script_dir/version")"

case "$version" in
  *[!0-9A-Za-z.+-]* | "") echo "Invalid version in $script_dir/version: '$version'" >&2; exit 2 ;;
esac
if ! printf '%s\n' "$version" | grep -Eq '^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z.-]+)?$'; then
  echo "Version '$version' is not a semantic version." >&2
  exit 2
fi

case "$rid" in
  linux-x64 | linux-arm64 | osx-x64 | osx-arm64) format="tar.gz"; exe_suffix="" ;;
  win-x64 | win-arm64) format="zip"; exe_suffix=".exe" ;;
  *) echo "Unsupported runtime identifier: $rid" >&2; exit 2 ;;
esac

name="percepta-semantic-$version-$rid"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
stage="$work/$name"

dotnet publish "$repo_root/src/Percepta.Cli/Percepta.Cli.fsproj" \
  --configuration Release \
  --runtime "$rid" \
  --self-contained true \
  --artifacts-path "$work/artifacts" \
  -p:PublishSingleFile=false \
  -p:Version="$version" \
  -p:DebugType=None \
  -p:DebugSymbols=false \
  --output "$stage"

# The app host is named after the project assembly; the released command is
# `percepta`. Renaming an app host is safe: it locates Percepta.Cli.dll by the
# relative path embedded at build time, not by its own file name.
mv "$stage/Percepta.Cli$exe_suffix" "$stage/percepta$exe_suffix"
if [ -z "$exe_suffix" ]; then
  chmod +x "$stage/percepta"
fi

# Fail closed if the Playwright driver did not land beside the assembly: an
# archive without it could never launch a browser (PERCEPTA-SEMCLI-001.4).
if [ ! -f "$stage/.playwright/package/cli.js" ]; then
  echo "Playwright driver package missing from publish output ($stage/.playwright/package/cli.js)." >&2
  exit 1
fi
if ! find "$stage/.playwright/node" -type f \( -name node -o -name node.exe \) | grep -q .; then
  echo "Playwright Node driver binary missing from publish output ($stage/.playwright/node)." >&2
  exit 1
fi

source_revision="$(git -C "$repo_root" rev-parse HEAD)"
python3 -I - "$stage" "$version" "$source_revision" <<'PY'
import hashlib, json, pathlib, sys
stage = pathlib.Path(sys.argv[1])
version = sys.argv[2]
source_revision = sys.argv[3]
with (stage / "Percepta.Cli.deps.json").open(encoding="utf-8") as handle:
    libraries = json.load(handle)["libraries"]
resolved = {key.rsplit("/", 1)[0].lower(): key.rsplit("/", 1)[1] for key in libraries}
files = {
    "EchelonFoundry.Aegis.Core": "Aegis.Core.dll",
    "FSharp.Core": "FSharp.Core.dll",
    "Microsoft.Playwright": "Microsoft.Playwright.dll",
}
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
dependencies = []
for name, file_name in sorted(files.items()):
    dependencies.append({
        "dependencyName": name,
        "dependencyVersion": resolved[name.lower()],
        "dependencyDigest": digest(stage / file_name),
        "dependencyDigestAlgorithm": "sha-256",
    })
metadata = {
    "schemaVersion": "1.0.0",
    "artifact": {
        "artifactName": "percepta-semantic",
        "artifactVersion": version,
        "artifactSourceRevision": source_revision,
        "artifactDigest": digest(stage / "Percepta.Cli.dll"),
        "artifactDigestAlgorithm": "sha-256",
    },
    "dependencies": dependencies,
}
with (stage / "percepta-artifact.json").open("w", encoding="utf-8") as handle:
    json.dump(metadata, handle, indent=2)
    handle.write("\n")
PY

printf '%s\n' "$version" > "$stage/VERSION"
cp "$repo_root/LICENSE" "$repo_root/NOTICE" "$stage/"
cp "$script_dir/README.md" "$stage/README.md"

mkdir -p "$output_dir"
output_dir="$(CDPATH= cd -- "$output_dir" && pwd)"
archive="$output_dir/$name.$format"
rm -f "$archive"

if [ "$format" = "zip" ]; then
  (cd "$work" && zip -q -r -X "$archive" "$name")
else
  (cd "$work" && tar -czf "$archive" "$name")
fi

echo "$archive"
