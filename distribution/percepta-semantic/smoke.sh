#!/usr/bin/env bash
# Smoke-test a packaged Percepta semantic CLI archive the way a consumer runs it
# (PERCEPTA-SEMCLI-001 acceptance criteria).
#
#   distribution/percepta-semantic/smoke.sh ARCHIVE REPO_ROOT ADVERSARIAL_DIR WORK_DIR
#
# ARCHIVE          a linux-x64 archive produced by package.sh
# REPO_ROOT        a Percepta checkout: supplies the canonical Indy Init contract,
#                  fixtures, evidence schema and version file (inputs only; the
#                  CLI under test is the archive, never the checkout's source)
# ADVERSARIAL_DIR  output of tests/Percepta.Adversarial.Tests at the same commit
#                  (experiment-results.json and mutants/*.html)
# WORK_DIR         empty scratch directory
#
# Requires: a Linux host with NO .NET installed (or only a runtime other than
# net8.0), python3 with the jsonschema module, and network access for
# `percepta install-browser`. Must run as root or with the Chromium system
# dependencies already present, because install-browser uses --with-deps.
set -euo pipefail

if [ "$#" -ne 4 ]; then
  echo "usage: $0 ARCHIVE REPO_ROOT ADVERSARIAL_DIR WORK_DIR" >&2
  exit 2
fi

archive="$1"
repo_root="$(CDPATH= cd -- "$2" && pwd)"
adversarial="$(CDPATH= cd -- "$3" && pwd)"
mkdir -p "$4"
work="$(CDPATH= cd -- "$4" && pwd)"
version="$(tr -d ' \r\n' < "$repo_root/distribution/percepta-semantic/version")"
schema="$repo_root/schemas/percepta-evidence.schema.json"
failures=0

step() { printf '\n== %s\n' "$*"; }
fail() { echo "SMOKE FAILURE: $*" >&2; failures=$((failures + 1)); }

# Runs a command, records its exit code in $code, never aborts the script.
run() {
  set +e
  "$@"
  code=$?
  set -e
  echo "-> exit $code"
}

# Validates an evidence report against the schema and prints its `complete` flag.
evidence_complete() {
  python3 -I - "$schema" "$1" <<'PY'
import json, sys
import jsonschema
schema_path, evidence_path = sys.argv[1], sys.argv[2]
with open(schema_path, encoding="utf-8") as handle:
    schema = json.load(handle)
with open(evidence_path, encoding="utf-8") as handle:
    evidence = json.load(handle)
validator = jsonschema.Draft202012Validator(schema, format_checker=jsonschema.FormatChecker())
errors = sorted(validator.iter_errors(evidence), key=lambda error: list(error.path))
if errors:
    for error in errors:
        print(f"schema violation at {list(error.path)}: {error.message}", file=sys.stderr)
    sys.exit(1)
print("true" if evidence["complete"] else "false")
PY
}

step "Host has no .NET runtime and no roll-forward override"
if command -v dotnet >/dev/null 2>&1; then
  fail "dotnet is on PATH ($(command -v dotnet)); the smoke must prove the archive needs no runtime"
fi
if [ -n "${DOTNET_ROLL_FORWARD:-}" ] || [ -n "${DOTNET_ROOT:-}" ]; then
  fail "DOTNET_ROLL_FORWARD or DOTNET_ROOT is set"
fi

step "Extract $archive"
mkdir -p "$work/install"
tar -xzf "$archive" -C "$work/install"
bin_dir="$work/install/percepta-semantic-$version-linux-x64"
percepta="$bin_dir/percepta"
[ -x "$percepta" ] || { echo "Missing executable $percepta" >&2; exit 1; }
[ -f "$bin_dir/.playwright/package/cli.js" ] || fail "archive lacks the Playwright driver"
[ -f "$bin_dir/percepta-artifact.json" ] || fail "archive lacks trusted artifact metadata"

step "Consumer project outside the Percepta checkout"
consumer="$work/consumer"
mkdir -p "$consumer/.percepta/contracts" "$consumer/fixtures" "$consumer/mutants"
cp "$repo_root/.percepta/contracts/indy-init-investigation-workspace.json" "$consumer/.percepta/contracts/"
cp "$repo_root/tests/fixtures/indy-init/"* "$consumer/fixtures/"
cd "$consumer"
git init -q
git config user.email "percepta-smoke@example.invalid"
git config user.name "Percepta release smoke"
git remote add origin "https://github.com/kemiller2002/percepta.git"
git add .
git commit -qm "fixture: establish trusted source revision"

step "percepta version"
run "$percepta" version
reported="$("$percepta" version 2>/dev/null || true)"
case "$reported" in
  "percepta $version" | "percepta $version+"*) ;;
  *) fail "version reported '$reported', expected percepta $version" ;;
esac

step "doctor without a browser fails closed (PERCEPTA-SEMCLI-001.4)"
mkdir -p "$work/no-browsers"
set +e
PLAYWRIGHT_BROWSERS_PATH="$work/no-browsers" "$percepta" doctor 2> "$work/doctor-no-browser.err"
code=$?
set -e
cat "$work/doctor-no-browser.err" >&2
echo "-> exit $code"
[ "$code" -ne 0 ] || fail "doctor reported success without a browser"
grep -q "Browser FAILED" "$work/doctor-no-browser.err" || fail "doctor did not report the missing browser"

export PLAYWRIGHT_BROWSERS_PATH="$work/browsers"

step "install-browser"
run "$percepta" install-browser
[ "$code" -eq 0 ] || { echo "install-browser failed; cannot continue" >&2; exit 1; }

step "doctor with a launchable browser"
run "$percepta" doctor
[ "$code" -eq 0 ] || fail "doctor failed with an installed browser"

step "Canonical verification with both fixtures"
run "$percepta" verify \
  --contract .percepta/contracts/indy-init-investigation-workspace.json \
  --url fixtures/index.html \
  --fixture fixtures/blocked.json \
  --fixture fixtures/legal.json \
  --semantic-review fixtures/semantic-review.json \
  --out evidence/pass.json
[ "$code" -eq 0 ] || fail "canonical verification exited $code"
complete="$(evidence_complete evidence/pass.json)" || fail "evidence/pass.json does not validate against the schema"
[ "$complete" = "true" ] || fail "canonical evidence is not complete"

step "Repeat canonical verification in an independent workspace"
repeat_consumer="$work/consumer-repeat"
git -C "$consumer" worktree add -q --detach "$repeat_consumer" HEAD
cd "$repeat_consumer"
run "$percepta" verify \
  --contract .percepta/contracts/indy-init-investigation-workspace.json \
  --url fixtures/index.html \
  --fixture fixtures/blocked.json \
  --fixture fixtures/legal.json \
  --semantic-review fixtures/semantic-review.json \
  --out evidence/repeat.json
[ "$code" -eq 0 ] || fail "repeated canonical verification exited $code"
complete="$(evidence_complete evidence/repeat.json)" || fail "evidence/repeat.json does not validate against the schema"
[ "$complete" = "true" ] || fail "repeated canonical evidence is not complete"
cd "$consumer"

step "Evidence contract v1.0.0 positive, reproducibility and negative matrix"
"$repo_root/distribution/percepta-semantic/evidence-contract-smoke.sh" \
  "$percepta" \
  "$consumer" \
  "$consumer/evidence/pass.json" \
  "$repeat_consumer/evidence/repeat.json" \
  "$bin_dir/percepta-artifact.json" \
  "$work/evidence-contract"

step "Canonical verification without fixtures fails closed"
run "$percepta" verify \
  --contract .percepta/contracts/indy-init-investigation-workspace.json \
  --url fixtures/index.html \
  --semantic-review fixtures/semantic-review.json \
  --out evidence/no-fixtures.json
[ "$code" -ne 0 ] || fail "verification without fixtures exited 0"
complete="$(evidence_complete evidence/no-fixtures.json)" || fail "evidence/no-fixtures.json does not validate against the schema"
[ "$complete" = "false" ] || fail "evidence without fixtures claims completion"

step "status reads the evidence"
run "$percepta" status --evidence evidence/pass.json
[ "$code" -eq 0 ] || fail "status failed"

step "Adversarial mutants (same expectations as tests/Percepta.Adversarial.Tests)"
python3 -I - "$adversarial/experiment-results.json" > "$work/mutants.tsv" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as handle:
    results = json.load(handle)
if not results.get("canonicalControlComplete"):
    sys.exit("adversarial positive control was not complete")
for result in results["results"]:
    print(f'{result["mutation"]}\t{result["class"]}\t{"true" if result["complete"] else "false"}')
PY
cp "$adversarial/mutants/"*.html "$consumer/mutants/"
git add mutants
git commit -qm "fixture: bind adversarial mutants to a source revision"
machine=0
while IFS="$(printf '\t')" read -r mutation class harness_complete <&3; do
  run "$percepta" verify \
    --contract .percepta/contracts/indy-init-investigation-workspace.json \
    --url "mutants/$mutation.html" \
    --fixture fixtures/blocked.json \
    --fixture fixtures/legal.json \
    --semantic-review fixtures/semantic-review.json \
    --out "evidence/mutants/$mutation.json"
  complete="$(evidence_complete "evidence/mutants/$mutation.json")" || { fail "$mutation evidence does not validate"; continue; }
  if [ "$class" = "machine-addressable" ]; then
    machine=$((machine + 1))
    [ "$code" -ne 0 ] || fail "$mutation (machine-addressable) exited 0"
    [ "$complete" = "false" ] || fail "$mutation (machine-addressable) claims completion"
  fi
  # The released CLI must reach the same completion verdict as the in-process harness.
  [ "$complete" = "$harness_complete" ] || fail "$mutation: CLI complete=$complete, harness complete=$harness_complete"
done 3< "$work/mutants.tsv"
[ "$machine" -gt 0 ] || fail "no machine-addressable mutants were exercised"
echo "Machine-addressable mutants exercised: $machine"

mkdir -p "$work/evidence"
cp -R "$consumer/evidence/." "$work/evidence/"

if [ "$failures" -ne 0 ]; then
  echo "Semantic CLI smoke FAILED ($failures failure(s))." >&2
  exit 1
fi
echo "Semantic CLI smoke passed for percepta-semantic $version (linux-x64)."
