"""EX-PERCEPTA-2026-0003 identity + branch-integrity recorder (read-only against lanes)."""
import hashlib, json, os, secrets, subprocess, sys
from pathlib import Path

REPO = Path("/home/user/percepta")
BASELINE = "ca2fdd3c5657a9617ebc5dff854fc0f362e07ced"
EXP = "research/experiments/EX-PERCEPTA-2026-0003"
LANES = (
    ("experiment/percepta-0003-openai-control-a", "control-a", "openai", "control"),
    ("experiment/percepta-0003-openai-treatment-a", "treatment-a", "openai", "treatment"),
    ("experiment/percepta-0003-claude-control-b", "control-b", "claude", "control"),
    ("experiment/percepta-0003-claude-treatment-b", "treatment-b", "claude", "treatment"),
)
INPUTS = {
    "canonicalContract": ".percepta/contracts/indy-init-investigation-workspace.json",
    "blockedFixture": "tests/fixtures/indy-init/blocked.json",
    "legalFixture": "tests/fixtures/indy-init/legal.json",
    "runtimeProtocol": f"{EXP}/frozen-runtime-protocol.md",
    "productBrief": f"{EXP}/frozen-product-brief.md",
    "preregistration": "research/experiments/EX-PERCEPTA-2026-0003--runtime-protocol-controlled-replication.md",
}
EVALUATOR_TREES = ("src/Percepta.Core", "src/Percepta.Adapter.Playwright", "src/Percepta.Cli")
EVALUATOR_FILES = ("Directory.Packages.props", "global.json")

git = lambda *a: subprocess.run(("git", "-C", str(REPO)) + a, check=True, capture_output=True, text=True).stdout.strip()
blob = lambda rev, path: git("rev-parse", f"{rev}:{path}")
sha256_bytes = lambda b: hashlib.sha256(b).hexdigest()
show_bytes = lambda rev, path: subprocess.run(("git", "-C", str(REPO), "show", f"{rev}:{path}"), check=True, capture_output=True).stdout

def lane_record(spec):
    branch, lane, _provider, _condition = spec
    ref = f"origin/{branch}"
    head = git("rev-parse", ref)
    behind, ahead = map(int, git("rev-list", "--left-right", "--count", f"{BASELINE}...{ref}").split())
    changes = tuple(tuple(l.split("\t")) for l in git("diff", "--name-status", BASELINE, ref).splitlines())
    expected = {("A", f"{EXP}/lanes/{lane}/NOTES.md"), ("A", f"{EXP}/lanes/{lane}/index.html")}
    parent = git("rev-parse", f"{ref}^")
    inputs_unchanged = all(blob(ref, p) == blob(BASELINE, p) for p in INPUTS.values())
    index_path, notes_path = f"{EXP}/lanes/{lane}/index.html", f"{EXP}/lanes/{lane}/NOTES.md"
    checks = {
        "exactlyOneCommitAhead": ahead == 1,
        "zeroCommitsBehind": behind == 0,
        "parentIsBaseline": parent == BASELINE,
        "changesExactlyAssignedIndexAndNotes": set(changes) == expected,
        "experimentalInputsUnmodified": inputs_unchanged,
    }
    return {
        "branch": branch,
        "lane": lane,
        "headCommit": head,
        "parentCommit": parent,
        "commitsAheadOfBaseline": ahead,
        "commitsBehindBaseline": behind,
        "commitSubject": git("log", "-1", "--format=%s", ref),
        "commitAuthor": git("log", "-1", "--format=%an <%ae>", ref),
        "commitDate": git("log", "-1", "--format=%cI", ref),
        "changedFiles": [{"status": s, "path": p} for s, p in changes],
        "indexHtml": {"path": index_path, "gitBlobSha": blob(ref, index_path), "sha256": sha256_bytes(show_bytes(ref, index_path)), "bytes": len(show_bytes(ref, index_path))},
        "notesMd": {"path": notes_path, "gitBlobSha": blob(ref, notes_path), "sha256": sha256_bytes(show_bytes(ref, notes_path))},
        "checks": checks,
        "integrityPassed": all(checks.values()),
    }

def main(out_dir):
    out = Path(out_dir)
    lanes = [lane_record(s) for s in LANES]
    ids = [f"candidate-0{i}" for i in range(1, 5)]
    order = sorted(range(4), key=lambda _: secrets.randbits(64))  # blinded random assignment
    mapping = [{"candidate": ids[pos], "branch": LANES[i][0], "lane": LANES[i][1], "provider": LANES[i][2], "condition": LANES[i][3],
                "headCommit": lanes[i]["headCommit"], "indexHtmlGitBlobSha": lanes[i]["indexHtml"]["gitBlobSha"],
                "indexHtmlSha256": lanes[i]["indexHtml"]["sha256"]}
               for pos, i in enumerate(order)]
    integrity = {
        "schemaVersion": 1,
        "experiment": "EX-PERCEPTA-2026-0003",
        "frozenBaselineSha": BASELINE,
        "frozenBaselineSubject": git("log", "-1", "--format=%s", BASELINE),
        "inputs": {k: {"path": p, "gitBlobSha": blob(BASELINE, p), "sha256": sha256_bytes(show_bytes(BASELINE, p))} for k, p in INPUTS.items()},
        "evaluator": {
            "sourceTrees": {t: git("rev-parse", f"{BASELINE}:{t}") for t in EVALUATOR_TREES},
            "files": {f: blob(BASELINE, f) for f in EVALUATOR_FILES},
            "sourceCommit": BASELINE,
        },
        "lanes": lanes,
        "allLanesPassIntegrity": all(l["integrityPassed"] for l in lanes),
    }
    (out / "integrity.json").write_text(json.dumps(integrity, indent=2) + "\n")
    (out / "candidate-mapping.sealed.json").write_text(json.dumps({"schemaVersion": 1, "note": "Sealed blinding key. Not read until deterministic evidence is frozen and hashed.", "assignmentMethod": "python secrets.randbits shuffle", "mapping": sorted(mapping, key=lambda m: m["candidate"])}, indent=2) + "\n")
    print(json.dumps({"allLanesPassIntegrity": integrity["allLanesPassIntegrity"], "lanes": [(l["lane"], l["integrityPassed"]) for l in lanes]}))

if __name__ == "__main__":
    main(sys.argv[1])
