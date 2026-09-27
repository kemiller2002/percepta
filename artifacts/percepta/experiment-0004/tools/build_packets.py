"""Build blinded review packets: one per candidate x case, relabelled item-01..20 by a random shuffle.

Usage: build_packets.py <scratch>
Packets contain only rendered/operable evidence, the single state object and that case's obligations.
"""
import json, secrets, shutil, subprocess, sys
from pathlib import Path

S = Path(sys.argv[1])
A = Path("/home/user/percepta/artifacts/percepta/experiment-0004")
CAP = A / "capture"
PKT = S / "review-packets"
BASELINE = "f474c449c2bc84da5707bb123c36500490875869"
CASES_PATH = "research/experiments/EX-PERCEPTA-2026-0004/held-out-semantic-cases.json"

cases = json.loads(subprocess.run(("git", "-C", "/home/user/percepta", "show", f"{BASELINE}:{CASES_PATH}"),
                                  check=True, capture_output=True).stdout)["cases"]
case_by_id = {c["id"]: c for c in cases}
items = [(cand, c["id"]) for cand in ("candidate-1", "candidate-2", "candidate-3", "candidate-4") for c in cases]
shuffled = sorted(items, key=lambda _: secrets.randbits(64))
key = [{"item": f"item-{i + 1:02d}", "candidate": cand, "caseId": cid} for i, (cand, cid) in enumerate(shuffled)]

def build(entry):
    src, dst = CAP / entry["candidate"] / entry["caseId"], PKT / entry["item"]
    dst.mkdir(parents=True, exist_ok=True)
    [shutil.copy2(f, dst / f.name) for f in src.iterdir() if f.suffix in (".png", ".txt")]
    cap = json.loads((src / "capture.json").read_text())
    case = case_by_id[entry["caseId"]]
    (dst / "state.json").write_text(json.dumps(case["state"], indent=2) + "\n")
    (dst / "obligations.json").write_text(json.dumps(case["semanticObligations"], indent=2) + "\n")
    (dst / "rendered-evidence.json").write_text(json.dumps({
        "viewports": cap["viewports"],
        "controls": cap["controls"],
        "confirmRootCauseControl": cap["confirmControl"],
        "operabilityProbes": cap["probes"],
    }, indent=2) + "\n")
    return entry

if __name__ == "__main__":
    shutil.rmtree(PKT, ignore_errors=True)
    built = list(map(build, key))
    (S / "sealed").mkdir(exist_ok=True)
    (S / "sealed/item-key.sealed.json").write_text(json.dumps(built, indent=2) + "\n")
    print(len(built), "packets")
