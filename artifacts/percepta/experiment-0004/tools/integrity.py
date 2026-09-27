"""Record the frozen-candidate gate for EX-PERCEPTA-2026-0004 before any held-out-driven evaluation."""
import hashlib, json, subprocess, sys
from pathlib import Path

REPO = Path("/home/user/percepta")
BASELINE = "f474c449c2bc84da5707bb123c36500490875869"
EXP = "research/experiments/EX-PERCEPTA-2026-0004"
CANDIDATES = (
    {"branch": "experiment/percepta-0004-openai-control-a", "lane": "control-a", "provider": "openai", "condition": "control", "frozenCommit": "c99950ba1cf79a8f590e0f050b3b4efadecc0ad1"},
    {"branch": "experiment/percepta-0004-openai-treatment-a", "lane": "treatment-a", "provider": "openai", "condition": "treatment", "frozenCommit": "d104371f69ae4b184769e85296fd7d4c1cd85c76"},
    {"branch": "experiment/percepta-0004-claude-control-b", "lane": "control-b", "provider": "claude", "condition": "control", "frozenCommit": "6b3ef30b75aaab4812919b7f56e8085dbb0fe442"},
    {"branch": "experiment/percepta-0004-claude-treatment-b", "lane": "treatment-b", "provider": "claude", "condition": "treatment", "frozenCommit": "ed5e15fd28d7c33dd4bd51014d1c1f4d62bdbb4b"},
)
EXCLUDED = ("experiment/percepta-0004-openai-control-b",)
FROZEN_INPUTS = (
    f"research/experiments/EX-PERCEPTA-2026-0004--held-out-semantic-generalization.md",
    f"{EXP}/frozen-product-brief.md",
    f"{EXP}/frozen-runtime-protocol.md",
    f"{EXP}/held-out-semantic-cases.json",
    ".percepta/contracts/indy-init-investigation-workspace.json",
    *(f"{EXP}/lanes/{l}/AGENTS.md" for l in ("control-a", "treatment-a", "control-b", "treatment-b")),
)

git = lambda *a: subprocess.run(("git", "-C", str(REPO), *a), capture_output=True, text=True)
out = lambda *a: git(*a).stdout.strip()
ok = lambda *a: git(*a).returncode == 0
blob = lambda rev, path: out("rev-parse", f"{rev}:{path}") or None
raw = lambda rev, path: subprocess.run(("git", "-C", str(REPO), "show", f"{rev}:{path}"), capture_output=True).stdout
sha256 = lambda b: hashlib.sha256(b).hexdigest()

def lane_record(c):
    sha, lane = c["frozenCommit"], c["lane"]
    expected = sorted(f"A\t{EXP}/lanes/{lane}/{f}" for f in ("NOTES.md", "index.html"))
    changed = sorted(filter(None, out("diff", "--name-status", BASELINE, sha).splitlines()))
    head = out("rev-parse", f"origin/{c['branch']}")
    return {
        **c,
        "commitExists": out("cat-file", "-t", sha) == "commit",
        "descendsFromBaseline": ok("merge-base", "--is-ancestor", BASELINE, sha),
        "branchHeadAtGate": head,
        "frozenCommitEqualsBranchHead": head == sha,
        "commitsSinceBaseline": int(out("rev-list", "--count", f"{BASELINE}..{sha}")),
        "commitChain": out("rev-list", "--reverse", f"{BASELINE}..{sha}").splitlines(),
        "changedFiles": changed,
        "changesExactlyLaneOutputs": changed == expected,
        "indexHtmlBlob": blob(sha, f"{EXP}/lanes/{lane}/index.html"),
        "indexHtmlSha256": sha256(raw(sha, f"{EXP}/lanes/{lane}/index.html")),
        "notesMdBlob": blob(sha, f"{EXP}/lanes/{lane}/NOTES.md"),
        "frozenInputsBlobIdenticalToBaseline": all(blob(sha, p) == blob(BASELINE, p) for p in FROZEN_INPUTS),
    }

def main(verified_at):
    lanes = [lane_record(c) for c in CANDIDATES]
    checks = ("commitExists", "descendsFromBaseline", "frozenCommitEqualsBranchHead", "changesExactlyLaneOutputs", "frozenInputsBlobIdenticalToBaseline")
    record = {
        "schemaVersion": 1,
        "experiment": "EX-PERCEPTA-2026-0004",
        "baseline": BASELINE,
        "baselineSubject": out("log", "-1", "--format=%s", BASELINE),
        "gateVerifiedAtUtc": verified_at,
        "gateVerifiedBeforeHeldOutAccess": True,
        "frozenInputBlobs": {p: blob(BASELINE, p) for p in FROZEN_INPUTS},
        "lanes": lanes,
        "excludedBranches": [{"branch": b, "headAtGate": out("rev-parse", f"origin/{b}"), "reason": "not one of the four preregistered candidates; excluded from all evaluation"} for b in EXCLUDED],
        "gatePassed": all(l[k] for l in lanes for k in checks),
    }
    print(json.dumps(record, indent=2))

if __name__ == "__main__":
    main(sys.argv[1])
