"""Layer 1: run the baseline-built Percepta verifier identically against every staged candidate.

Usage: evaluate.py <scratch> positive-control | candidates
"""
import hashlib, json, os, subprocess, sys
from datetime import datetime, timezone
from pathlib import Path

REPO = Path("/home/user/percepta")
S = Path(sys.argv[1])
BASE = S / "baseline"  # detached worktree at the frozen baseline
OUT = REPO / "artifacts/percepta/experiment-0004/deterministic"
FIX = REPO / "artifacts/percepta/experiment-0004/held-out-fixtures"
CLI = S / "build/bin/Percepta.Cli/release/Percepta.Cli.dll"
ENV = {**os.environ, "PLAYWRIGHT_BROWSERS_PATH": str(S / "pw-browsers"), "DOTNET_CLI_TELEMETRY_OPTOUT": "1"}
CONTRACT = ".percepta/contracts/indy-init-investigation-workspace.json"
CASES = ("combined-blocker-falsification", "pending-unverified", "all-constraints", "legal-with-history", "illegal-without-known-blocker")
RUNS = (("combined", CASES), *((c, (c,)) for c in CASES))

sha256_file = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
now = lambda: datetime.now(timezone.utc).isoformat()
scrub = lambda s: s.replace(str(S), "$SCRATCH").replace(str(REPO), "$REPO")


def command(target, fixtures, out_dir):
    return ("dotnet", str(CLI), "verify", "--contract", CONTRACT, "--url", str(target),
            *(a for f in fixtures for a in ("--fixture", str(FIX / f"{f}.json"))),
            "--out", str(out_dir / "evidence.json"), "--screenshots", str(out_dir / "screenshots"))


def run_one(subject, target, expected_sha, run_id, fixtures):
    d = OUT / subject / run_id
    d.mkdir(parents=True, exist_ok=True)
    before = sha256_file(target)
    cmd = command(target, fixtures, d)
    started = now()
    try:
        proc = subprocess.run(cmd, cwd=BASE, env=ENV, capture_output=True, timeout=600)
        code, stdout, stderr = proc.returncode, proc.stdout, proc.stderr
    except subprocess.TimeoutExpired as e:
        code, stdout, stderr = "timeout", e.stdout or b"", e.stderr or b""
    finished = now()
    after = sha256_file(target)
    (d / "stdout.txt").write_bytes(stdout)
    (d / "stderr.txt").write_bytes(stderr)
    ev = json.loads((d / "evidence.json").read_text()) if (d / "evidence.json").exists() else None
    record = {
        "subject": subject, "run": run_id, "fixtures": list(fixtures),
        "stagedSha256Before": before, "stagedSha256After": after, "expectedSha256": expected_sha,
        "byteIdentical": before == after == expected_sha if expected_sha else before == after,
        "command": [scrub(c) for c in cmd], "cwd": "$SCRATCH/baseline (detached worktree at f474c449)",
        "startedAtUtc": started, "finishedAtUtc": finished, "exitCode": code,
        "evidencePresent": ev is not None, "complete": ev.get("complete") if ev else None,
    }
    (d / "run.json").write_text(json.dumps(record, indent=2) + "\n")
    return record


def summary(r):
    return {k: r[k] for k in ("subject", "run", "exitCode", "complete", "byteIdentical")}


def positive_control():
    target = BASE / "tests/fixtures/indy-init/index.html"
    return [run_one("positive-control", target, None, rid, fx) for rid, fx in RUNS]


def candidates():
    mapping = json.loads((S / "sealed/candidate-mapping.sealed.json").read_text())["mapping"]
    return [run_one(m["candidate"], S / "staging" / m["candidate"] / "index.html", m["indexHtmlSha256"], rid, fx)
            for m in sorted(mapping, key=lambda m: m["candidate"]) for rid, fx in RUNS]


if __name__ == "__main__":
    records = {"positive-control": positive_control, "candidates": candidates}[sys.argv[2]]()
    print(json.dumps(list(map(summary, records)), indent=1))
