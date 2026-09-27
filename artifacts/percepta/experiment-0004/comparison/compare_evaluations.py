"""Describe where the two independent EX-PERCEPTA-2026-0004 evaluations disagree.

Reads only the frozen outputs of both evaluations; writes nothing unless --json is given.

  Evaluation 1: artifacts/percepta/experiment-0004/                          (PR #16)
  Evaluation 2: artifacts/percepta/experiment-0004/independent-evaluation-2/ (PR #14)

Usage:
  python3 compare_evaluations.py              # summary + every disagreement
  python3 compare_evaluations.py --evidence   # also print each reviewer's cited evidence
  python3 compare_evaluations.py --json out.json
"""
import argparse, json
from collections import Counter
from itertools import groupby
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
EVAL1, EVAL2 = ROOT, ROOT / "independent-evaluation-2"
CASES = ("combined-blocker-falsification", "pending-unverified", "all-constraints",
         "legal-with-history", "illegal-without-known-blocker")
LANES = ("treatment-a", "control-a", "treatment-b", "control-b")
REVIEWERS = ("R1", "R2")

load = lambda p: json.loads(Path(p).read_text())
clip = lambda s, n=400: s if len(s) <= n else s[: n - 1] + "…"


# ---------- normalise each evaluation to: (lane, case, obligation) -> record ----------

def eval1_records():
    lane_of = {m["candidate"]: m["lane"] for m in load(EVAL1 / "candidate-mapping.json")["mapping"]}
    items = load(EVAL1 / "semantic-review/scores.blind.json")["items"]
    return {
        (lane_of[i["candidate"]], i["caseId"], o["obligation"]): {
            "passed": o["passed"],
            "judgments": {r: o["individual"][r]["judgment"] for r in REVIEWERS},
            "failureTypes": {r: o["individual"][r]["failureType"] for r in REVIEWERS},
            "evidence": {r: o["individual"][r]["evidence"] for r in REVIEWERS},
        }
        for i in items for o in i["obligations"]
    }


def eval2_records():
    results = load(EVAL2 / "results.json")
    lane_of = {k: v["lane"] for k, v in results["candidates"].items()}
    judgment = lambda j: j.lower()
    return {
        (lane_of[o["candidate"]], o["caseId"], o["obligation"]): {
            "passed": all(o["primary"][r] == "PASS" for r in REVIEWERS),
            "judgments": {r: judgment(o["primary"][r]) for r in REVIEWERS},
            "failureTypes": {r: None for r in REVIEWERS},
            "evidence": {r: o["primaryEvidence"][r] for r in REVIEWERS},
        }
        for o in results["obligationRecords"]
    }


# ---------- comparison ----------

def fully_correct(records):
    by_case = lambda k: (k[0], k[1])
    keys = sorted(records, key=by_case)
    return {lc: all(records[k]["passed"] for k in ks) for lc, ks in groupby(keys, key=by_case)}


def compare(e1, e2):
    if set(e1) != set(e2):
        raise SystemExit(f"obligation sets differ: only-1={sorted(set(e1) - set(e2))} only-2={sorted(set(e2) - set(e1))}")
    order = lambda k: (LANES.index(k[0]), CASES.index(k[1]))
    keys = sorted(e1, key=order)
    disagreements = [
        {"lane": k[0], "caseId": k[1], "obligation": k[2], "evaluation1": e1[k], "evaluation2": e2[k]}
        for k in keys if e1[k]["passed"] != e2[k]["passed"]
    ]
    fc1, fc2 = fully_correct(e1), fully_correct(e2)
    case_flips = [{"lane": l, "caseId": c, "evaluation1": fc1[(l, c)], "evaluation2": fc2[(l, c)]}
                  for (l, c) in sorted(fc1, key=lambda lc: (LANES.index(lc[0]), CASES.index(lc[1])))
                  if fc1[(l, c)] != fc2[(l, c)]]
    per_lane = {l: {"evaluation1": sum(v for (ll, _), v in fc1.items() if ll == l),
                    "evaluation2": sum(v for (ll, _), v in fc2.items() if ll == l)} for l in LANES}
    reviewer_split = lambda rec: "split" if len(set(rec["judgments"].values())) > 1 else "unanimous"
    return {
        "obligationsCompared": len(keys),
        "obligationAgreement": len(keys) - len(disagreements),
        "disagreements": disagreements,
        "caseFlips": case_flips,
        "fullyCorrectPerLane": per_lane,
        "disagreementsByLane": dict(Counter(d["lane"] for d in disagreements)),
        "disagreementsByCase": dict(Counter(d["caseId"] for d in disagreements)),
        "strictnessDirection": dict(Counter(
            "evaluation1 stricter" if d["evaluation2"]["passed"] else "evaluation2 stricter" for d in disagreements)),
        "reviewerSplitInStricterEvaluation": dict(Counter(
            reviewer_split(d["evaluation1"] if d["evaluation2"]["passed"] else d["evaluation2"]) for d in disagreements)),
    }


# ---------- presentation ----------

def verdict(rec):
    j = rec["judgments"]
    tag = lambda r: j[r] + (f" ({rec['failureTypes'][r]})" if rec["failureTypes"][r] else "")
    return f"{'PASS' if rec['passed'] else 'FAIL'}  [R1 {tag('R1')}, R2 {tag('R2')}]"


def render(report, show_evidence):
    lines = [
        "EX-PERCEPTA-2026-0004: Evaluation 1 (#16) vs Evaluation 2 (#14)",
        "",
        f"Obligations compared: {report['obligationsCompared']}   "
        f"agree: {report['obligationAgreement']}   disagree: {len(report['disagreements'])}",
        "",
        "Fully correct cases per lane (Eval 1 / Eval 2):",
        *(f"  {l:<12} {v['evaluation1']}/5  /  {v['evaluation2']}/5" for l, v in report["fullyCorrectPerLane"].items()),
        "",
        "Cases whose fully-correct outcome flips:",
        *(f"  {f['lane']:<12} {f['caseId']:<31} Eval1 {'correct' if f['evaluation1'] else 'not correct':<11}  "
          f"Eval2 {'correct' if f['evaluation2'] else 'not correct'}" for f in report["caseFlips"]),
        "",
        f"Direction: {report['strictnessDirection']}",
        f"Within the stricter evaluation, its own two reviewers were: {report['reviewerSplitInStricterEvaluation']}",
        "",
        "Obligation-level disagreements:",
    ]
    detail = lambda d: [
        "",
        f"- {d['lane']} / {d['caseId']}",
        f"  obligation: {d['obligation']}",
        f"  Eval 1: {verdict(d['evaluation1'])}",
        f"  Eval 2: {verdict(d['evaluation2'])}",
        *((f"    Eval {n} {r}: {clip(d[k]['evidence'][r])}" for n, k in (("1", "evaluation1"), ("2", "evaluation2")) for r in REVIEWERS)
          if show_evidence else ()),
    ]
    return "\n".join(lines + [l for d in report["disagreements"] for l in detail(d)])


if __name__ == "__main__":
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--evidence", action="store_true", help="print each reviewer's cited evidence")
    ap.add_argument("--json", metavar="PATH", help="also write the full comparison as JSON")
    args = ap.parse_args()
    report = compare(eval1_records(), eval2_records())
    print(render(report, args.evidence))
    if args.json:
        Path(args.json).write_text(json.dumps(report, indent=2) + "\n")
