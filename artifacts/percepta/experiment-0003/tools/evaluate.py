"""Stage frozen index.html byte-for-byte per opaque candidate and run the Percepta verifier identically."""
import hashlib, json, os, platform, subprocess, sys
from datetime import datetime, timezone
from pathlib import Path

REPO = Path("/home/user/percepta")
S = Path(sys.argv[1])
OUT = REPO / "artifacts/percepta/experiment-0003"
CLI = S / "build/bin/Percepta.Cli/release/Percepta.Cli.dll"
ENV = {**os.environ, "PLAYWRIGHT_BROWSERS_PATH": str(S / "pw-browsers"), "DOTNET_CLI_TELEMETRY_OPTOUT": "1"}
CONTRACT = ".percepta/contracts/indy-init-investigation-workspace.json"
FIXTURES = ("tests/fixtures/indy-init/blocked.json", "tests/fixtures/indy-init/legal.json")

sha256_file = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
now = lambda: datetime.now(timezone.utc).isoformat()

def stage(entry):
    src = subprocess.run(("git", "-C", str(REPO), "show", f"{entry['headCommit']}:research/experiments/EX-PERCEPTA-2026-0003/lanes/{entry['lane']}/index.html"),
                         check=True, capture_output=True).stdout
    target = S / "staging" / entry["candidate"] / "index.html"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(src)
    return target

def command(candidate, target):
    d = OUT / candidate
    return ("dotnet", str(CLI), "verify", "--contract", CONTRACT, "--url", str(target),
            "--fixture", FIXTURES[0], "--fixture", FIXTURES[1],
            "--out", str(d.relative_to(REPO) / "evidence.json"),
            "--screenshots", str(d.relative_to(REPO) / "screenshots"))

def run(entry):
    cid = entry["candidate"]
    d = OUT / cid
    d.mkdir(parents=True, exist_ok=True)
    target = stage(entry)
    before = sha256_file(target)
    cmd = command(cid, target)
    started = now()
    proc = subprocess.run(cmd, cwd=REPO, env=ENV, capture_output=True)
    finished = now()
    after = sha256_file(target)
    (d / "stdout.txt").write_bytes(proc.stdout)
    (d / "stderr.txt").write_bytes(proc.stderr)
    (d / "exit-code.txt").write_text(f"{proc.returncode}\n")
    evidence = json.loads((d / "evidence.json").read_text()) if (d / "evidence.json").exists() else None
    run_record = {
        "candidate": cid,
        "stagedPath": str(target),
        "stagedSha256Before": before,
        "stagedSha256After": after,
        "sourceSha256": entry["indexHtmlSha256"],
        "byteIdenticalBefore": before == entry["indexHtmlSha256"],
        "byteIdenticalAfter": after == entry["indexHtmlSha256"],
        "command": [c.replace(str(S), "$SCRATCH") for c in cmd],
        "cwd": "<repo root at frozen baseline>",
        "startedAtUtc": started,
        "finishedAtUtc": finished,
        "exitCode": proc.returncode,
        "complete": evidence["complete"] if evidence else None,
    }
    (d / "run.json").write_text(json.dumps(run_record, indent=2) + "\n")
    return run_record

def main():
    mapping = json.loads((S / "sealed/candidate-mapping.sealed.json").read_text())["mapping"]
    records = [run(e) for e in sorted(mapping, key=lambda m: m["candidate"])]
    print(json.dumps([{k: r[k] for k in ("candidate", "exitCode", "complete", "byteIdenticalBefore", "byteIdenticalAfter")} for r in records], indent=1))

if __name__ == "__main__":
    main()
