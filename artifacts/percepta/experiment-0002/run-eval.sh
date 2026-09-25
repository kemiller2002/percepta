#!/usr/bin/env bash
# Mirrors the "Evaluate all lanes" step of .github/workflows/percepta-experiment-0002.yml at the baseline SHA.
set -u
EXP_ROOT=research/experiments/EX-PERCEPTA-2026-0002
mkdir -p artifacts/percepta/experiment-0002
: > artifacts/percepta/experiment-0002/summary.tsv

run_lane() {
  lane="$1"; provider="$2"; condition="$3"
  target="${EXP_ROOT}/lanes/${lane}/index.html"
  output="artifacts/percepta/experiment-0002/${lane}.evidence.json"
  set +e
  dotnet run --project src/Percepta.Cli/Percepta.Cli.fsproj --configuration Release -- verify --contract .percepta/contracts/indy-init-investigation-workspace.json --url "$target" --fixture tests/fixtures/indy-init/blocked.json --fixture tests/fixtures/indy-init/legal.json --out "$output" > "artifacts/percepta/experiment-0002/${lane}.stdout.log" 2> "artifacts/percepta/experiment-0002/${lane}.stderr.log"
  code=$?
  set -e
  # record-keeping only: snapshot shared screenshot dir (not part of evaluator config)
  if [ -d .percepta/evidence ]; then mkdir -p "artifacts/percepta/experiment-0002/screenshots/${lane}"; cp -r .percepta/evidence/. "artifacts/percepta/experiment-0002/screenshots/${lane}/"; rm -rf .percepta/evidence; fi
  complete="false"
  if [ -f "$output" ]; then
    complete="$(python3 -c 'import json,sys; print(str(json.load(open(sys.argv[1]))["complete"]).lower())' "$output")"
  fi
  printf "%s\t%s\t%s\t%s\t%s\n" "$lane" "$provider" "$condition" "$code" "$complete" >> artifacts/percepta/experiment-0002/summary.tsv
}

run_lane control-a openai control
run_lane treatment-a openai treatment
run_lane control-b claude control
run_lane treatment-b claude treatment
