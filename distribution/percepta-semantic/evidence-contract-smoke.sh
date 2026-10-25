#!/usr/bin/env bash
# Focused released-artifact regression matrix for evidence contract v1.0.0.
set -euo pipefail

if [ "$#" -ne 6 ]; then
  echo "usage: $0 PERCEPTA SOURCE_ROOT EVIDENCE REPEATED_EVIDENCE TRUSTED_ARTIFACT_METADATA WORK_DIR" >&2
  exit 2
fi

percepta="$1"
source_root="$2"
evidence="$3"
repeated="$4"
trusted="$5"
work="$6"
mkdir -p "$work/cases" "$work/results"
matrix="$work/matrix.tsv"
: > "$matrix"
failures=0

record() {
  printf '%s\t%s\t%s\t%s\t%s\n' "$1" "$2" "$3" "$4" "$5" >> "$matrix"
  if [ "$4" != "PASS" ]; then
    failures=$((failures + 1))
  fi
}

run_case() {
  name="$1"
  expected_code="$2"
  expected_status="$3"
  candidate="$4"
  trust_mode="${5:-trusted}"
  output="$work/results/$name.json"

  set +e
  if [ "$trust_mode" = "trusted" ]; then
    "$percepta" validate-evidence \
      --evidence "$candidate" \
      --source-root "$source_root" \
      --trusted-artifact-metadata "$trusted" \
      --out "$output"
  else
    "$percepta" validate-evidence \
      --evidence "$candidate" \
      --source-root "$source_root" \
      --out "$output"
  fi
  actual_code=$?
  set -e

  actual_status="$(python3 -I - "$output" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as handle:
    print(json.load(handle)["status"])
PY
)"

  if [ "$actual_code" -eq "$expected_code" ] && [ "$actual_status" = "$expected_status" ]; then
    record "$name" "$expected_status/$expected_code" "$actual_status/$actual_code" PASS "$candidate"
  else
    record "$name" "$expected_status/$expected_code" "$actual_status/$actual_code" FAIL "$candidate"
  fi
}

mutate() {
  name="$1"
  output="$work/cases/$name.json"
  python3 -I - "$evidence" "$output" "$name" <<'PY'
import json, sys
source, output, name = sys.argv[1:]
with open(source, encoding="utf-8") as handle:
    value = json.load(handle)
zeros = "0" * 64
if name == "N1":
    del value["inputDigest"]
elif name == "N2":
    value["inputs"][0]["digest"] = zeros
elif name == "N3":
    del value["configurationDigest"]
elif name == "N4":
    value["configurationDigest"] = zeros
elif name == "N5":
    value["sourceRevision"] = "f" * 40
    for reference in value["evidenceReferences"]:
        reference["sourceRevision"] = "f" * 40
elif name == "N6":
    value["artifact"]["artifactDigest"] = zeros
elif name == "N7":
    value["dependencies"] = value["dependencies"][1:]
elif name == "N8":
    value["schemaVersion"] = "99.0.0"
elif name == "N9":
    for key in ("evaluationResults", "results"):
        value[key][0]["status"] = "Failed"
        value[key][0]["reason"] = "mutated after execution"
elif name == "N11":
    value["dependencies"].append(value["dependencies"][0])
elif name == "N12":
    value = {"outcome": "satisfied", "claim": "the agent says this passed"}
elif name == "N14":
    value["evidenceDigest"] = zeros
else:
    raise SystemExit(f"unknown mutation {name}")
with open(output, "w", encoding="utf-8") as handle:
    json.dump(value, handle, indent=2)
    handle.write("\n")
PY
  printf '%s\n' "$output"
}

run_case P1 0 VALID "$evidence"
run_case P1-repeat 0 VALID "$repeated"

if python3 -I - "$evidence" "$repeated" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as handle:
    first = json.load(handle)
with open(sys.argv[2], encoding="utf-8") as handle:
    second = json.load(handle)
stable = [
    "schemaVersion", "experimentId", "repository", "sourceRevision",
    "inputDigest", "configurationDigest", "artifact", "dependencies",
    "outcome", "evaluationResults", "evidenceReferences", "evidenceDigest",
]
if any(first[key] != second[key] for key in stable):
    raise SystemExit(1)
PY
then
  record P2 "equivalent" "equivalent" PASS "$repeated"
  record P3 "equivalent" "equivalent" PASS "$repeated"
else
  record P2 "equivalent" "different" FAIL "$repeated"
  record P3 "equivalent" "different" FAIL "$repeated"
fi

for name in N1 N2 N3 N4 N5 N6 N7 N8 N9 N11 N12 N14; do
  candidate="$(mutate "$name")"
  run_case "$name" 3 INVALID "$candidate"
done

missing_reference="$(python3 -I - "$evidence" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8") as handle:
    print(json.load(handle)["evidenceReferences"][0]["path"])
PY
)"
reference_path="$source_root/$missing_reference"
reference_backup="$work/missing-reference.backup"
mv "$reference_path" "$reference_backup"
run_case N10 3 INVALID "$evidence"
mv "$reference_backup" "$reference_path"

run_case N13 4 UNVERIFIABLE "$evidence" untrusted

python3 -I - "$matrix" "$work/results.json" <<'PY'
import csv, json, sys
matrix, output = sys.argv[1:]
tests = []
with open(matrix, newline="", encoding="utf-8") as handle:
    for name, expected, actual, result, artifact in csv.reader(handle, delimiter="\t"):
        tests.append({
            "test": name,
            "expected": expected,
            "actual": actual,
            "result": result,
            "artifact": artifact,
        })
with open(output, "w", encoding="utf-8") as handle:
    json.dump({
        "schema": "percepta.evidence-contract-regression/1",
        "tests": tests,
        "passed": sum(test["result"] == "PASS" for test in tests),
        "failed": sum(test["result"] == "FAIL" for test in tests),
    }, handle, indent=2)
    handle.write("\n")
PY

cat "$work/results.json"
if [ "$failures" -ne 0 ]; then
  echo "Evidence contract smoke FAILED ($failures failure(s))." >&2
  exit 1
fi
echo "Evidence contract smoke passed."
