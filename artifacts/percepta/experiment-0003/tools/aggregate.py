"""Aggregate frozen EX-PERCEPTA-2026-0003 evidence after unblinding. Reads raw evidence; never rewrites it."""
import hashlib, json, subprocess, sys
from functools import reduce
from pathlib import Path

REPO = Path("/home/user/percepta")
OUT = REPO / "artifacts/percepta/experiment-0003"
S = Path(sys.argv[1])
BASELINE = "ca2fdd3c5657a9617ebc5dff854fc0f362e07ced"

load = lambda p: json.loads(Path(p).read_text())
sha256_file = lambda p: hashlib.sha256(Path(p).read_bytes()).hexdigest()
git = lambda *a: subprocess.run(("git", "-C", str(REPO)) + a, check=True, capture_output=True, text=True).stdout.strip()

mapping = load(OUT / "candidate-mapping.json")["mapping"]
review = load(OUT / "semantic-review/blinded-composition-review.json")
relabel = load(OUT / "semantic-review/review-relabel-key.json")["reviewLabelToCandidate"]
contract = load(REPO / ".percepta/contracts/indy-init-investigation-workspace.json")
required_ids = tuple(r["id"] for r in contract["evidenceRequirements"] if r["required"])
optional_ids = tuple(r["id"] for r in contract["evidenceRequirements"] if not r["required"])

def churn(entry):
    stat = git("diff", "--numstat", BASELINE, entry["headCommit"])
    rows = [l.split("\t") for l in stat.splitlines()]
    return {"changedFileCount": len(rows), "linesAdded": sum(int(a) for a, _, _ in rows), "linesDeleted": sum(int(d) for _, d, _ in rows),
            "files": [{"path": p, "added": int(a), "deleted": int(d)} for a, d, p in rows]}

def candidate_row(entry):
    cid = entry["candidate"]
    ev = load(OUT / cid / "evidence.json")
    run = load(OUT / cid / "run.json")
    by_id = {r["requirement"]: r for r in ev["results"]}
    status = lambda ids: {i: {"status": by_id[i]["status"], **({"reason": by_id[i]["reason"]} if "reason" in by_id[i] else {})} for i in ids}
    req = status(required_ids)
    return {
        **{k: entry[k] for k in ("candidate", "branch", "lane", "provider", "condition", "headCommit", "indexHtmlGitBlobSha", "indexHtmlSha256")},
        "exitCode": run["exitCode"],
        "complete": ev["complete"],
        "requiredCategories": req,
        "requiredPassed": sum(v["status"] == "Passed" for v in req.values()),
        "requiredFailed": sum(v["status"] == "Failed" for v in req.values()),
        "requiredUnavailable": sum(v["status"] == "Unavailable" for v in req.values()),
        "requiredTotal": len(required_ids),
        "optionalCategories": status(optional_ids),
        "failedObservations": [{"requirement": i, "reason": v.get("reason")} for i, v in req.items() if v["status"] != "Passed"],
        "evidenceSha256": sha256_file(OUT / cid / "evidence.json"),
        "stagedByteIdentical": run["byteIdenticalBefore"] and run["byteIdenticalAfter"],
    }

rows = [candidate_row(e) for e in sorted(mapping, key=lambda m: m["candidate"])]

def condition_summary(cond):
    rs = [r for r in rows if r["condition"] == cond]
    failures = reduce(lambda acc, r: {**acc, **{i: acc.get(i, 0) + (v["status"] != "Passed") for i, v in r["requiredCategories"].items()}}, rs, {})
    return {"completedLanes": sum(r["complete"] for r in rs), "lanes": len(rs),
            "wholeLaneCompletionRate": sum(r["complete"] for r in rs) / len(rs),
            "requiredCategoriesPassed": sum(r["requiredPassed"] for r in rs),
            "requiredCategoriesEvaluated": sum(r["requiredTotal"] for r in rs),
            "failuresByCategory": failures,
            "candidates": [r["candidate"] for r in rs]}

control, treatment = condition_summary("control"), condition_summary("treatment")
pick = lambda lane: next(r for r in rows if r["lane"] == lane)
pair = lambda c, t: {"control": {k: pick(c)[k] for k in ("candidate", "complete", "requiredPassed", "requiredTotal", "exitCode")},
                     "treatment": {k: pick(t)[k] for k in ("candidate", "complete", "requiredPassed", "requiredTotal", "exitCode")},
                     "descriptiveDifference": "no difference: both lanes complete with all required categories Passed" if pick(c)["complete"] and pick(t)["complete"] and pick(c)["requiredPassed"] == pick(t)["requiredPassed"] else "differs"}

hy3_support = treatment["wholeLaneCompletionRate"] > control["wholeLaneCompletionRate"]

treatment_cands = sorted(r["candidate"] for r in rows if r["condition"] == "treatment" and r["complete"])
label_of = {c: l for l, c in relabel.items()}
tpair_key = "-".join(sorted(label_of[c].split("-")[1] for c in treatment_cands))
tpair_verdict = review["pairs"][tpair_key]["verdict"] if len(treatment_cands) >= 2 else None
hy4_support = len(treatment_cands) >= 2 and tpair_verdict == "materially-different"

results = {
    "schemaVersion": 1,
    "experiment": "EX-PERCEPTA-2026-0003",
    "baselineSha": BASELINE,
    "primaryOutcome": "whole-lane first-pass required Percepta completion",
    "rows": rows,
    "control": control,
    "treatment": treatment,
    "controlFirstPassPasses": control["completedLanes"], "controlCount": control["lanes"],
    "treatmentFirstPassPasses": treatment["completedLanes"], "treatmentCount": treatment["lanes"],
    "pairedDescriptive": {"openai": pair("control-a", "treatment-a"), "claude": pair("control-b", "treatment-b")},
    "hypotheses": {
        "HY-PERCEPTA-2026-0003": {
            "rule": "replication support iff treatment whole-lane first-pass completion rate > control rate",
            "treatmentRate": treatment["wholeLaneCompletionRate"], "controlRate": control["wholeLaneCompletionRate"],
            "replicationSupport": hy3_support,
            "interpretation": "No replication support: control completion (2/2) equals treatment completion (2/2). Descriptive only (n=2 per condition); not statistically conclusive.",
        },
        "HY-PERCEPTA-2026-0004": {
            "rule": ">=2 treatment implementations pass all required evidence AND blinded review finds materially different composition/layout",
            "treatmentLanesPassingAllRequired": len(treatment_cands),
            "reviewPerformed": True,
            "treatmentPairReviewLabels": tpair_key,
            "treatmentPairVerdict": tpair_verdict,
            "reviewerFlaggedBorderline": True,
            "support": hy4_support,
            "interpretation": "Not supported: the blinded reviewer judged the treatment pair not materially different (flagged borderline). Threshold applied as preregistered, not weakened.",
        },
    },
    "rawEvidenceManifestSha256": sha256_file(OUT / "raw-evidence.SHA256SUMS"),
    "semanticReviewSha256": sha256_file(OUT / "semantic-review/blinded-composition-review.json"),
}
(OUT / "results.json").write_text(json.dumps(results, indent=2) + "\n")

secondary = {
    "schemaVersion": 1,
    "experiment": "EX-PERCEPTA-2026-0003",
    "requiredFailedAndUnavailable": {r["candidate"]: {"failed": r["requiredFailed"], "unavailable": r["requiredUnavailable"]} for r in rows},
    "perLaneRequiredPassCount": {r["candidate"]: f'{r["requiredPassed"]}/{r["requiredTotal"]}' for r in rows},
    "stateProjectionFailures": {r["candidate"]: [f for f in r["failedObservations"] if f["requirement"] == "state-projection"] for r in rows},
    "interactionCapabilityFailures": {r["candidate"]: [f for f in r["failedObservations"] if f["requirement"] == "interaction"] for r in rows},
    "responsiveFailures": {r["candidate"]: [f for f in r["failedObservations"] if f["requirement"] == "responsive"] for r in rows},
    "optionalEvidence": {r["candidate"]: r["optionalCategories"] for r in rows},
    "changedFilesAndChurn": {r["candidate"]: churn(r) for r in rows},
    "providerStratified": results["pairedDescriptive"],
    "implementationDiversity": {
        "reviewPerformed": True,
        "reviewInputs": "16 full-page screenshots (4 candidates x 4 viewports, blocked fixture state), relabeled Screen-W..Z with a second random key; no provider, condition, branch, pass/fail, NOTES.md or source given",
        "reviewLabelToCandidate": relabel,
        "pairVerdicts": {k: v["verdict"] for k, v in review["pairs"].items()},
        "treatmentPair": {"labels": tpair_key, "verdict": tpair_verdict},
        "reviewerNotes": review["reviewerNotes"],
    },
    "correctiveIterationsAfterFreeze": 0,
}
(OUT / "secondary-outcomes.json").write_text(json.dumps(secondary, indent=2) + "\n")

header = ("candidate", "branch", "provider", "condition", "exitCode", "complete", "requiredPassed", "requiredTotal", *required_ids, *optional_ids)
line = lambda r: (r["candidate"], r["branch"], r["provider"], r["condition"], str(r["exitCode"]), str(r["complete"]).lower(), str(r["requiredPassed"]), str(r["requiredTotal"]),
                  *(r["requiredCategories"][i]["status"] for i in required_ids), *(r["optionalCategories"][i]["status"] for i in optional_ids))
(OUT / "summary.tsv").write_text("\n".join("\t".join(x) for x in (header, *map(line, rows))) + "\n")
print(json.dumps({"control": control, "treatment": treatment, "hy3": hy3_support, "hy4": hy4_support, "tpair": [tpair_key, tpair_verdict]}, indent=1))
