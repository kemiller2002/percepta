"""Aggregate frozen raw evidence for EX-PERCEPTA-2026-0004.

Usage:
  aggregate.py blind   -> semantic-review/scores.blind.json (anonymous candidates only; no mapping read)
  aggregate.py unblind -> results.json, secondary-outcomes.json, summary.tsv (reads candidate-mapping.json)
All inputs are the frozen raw files under artifacts/percepta/experiment-0004/. Nothing raw is modified.
"""
import json, subprocess, sys
from collections import Counter
from functools import reduce
from pathlib import Path

A = Path("/home/user/percepta/artifacts/percepta/experiment-0004")
REPO = Path("/home/user/percepta")
BASELINE = "f474c449c2bc84da5707bb123c36500490875869"
CASES = ("combined-blocker-falsification", "pending-unverified", "all-constraints", "legal-with-history", "illegal-without-known-blocker")
CANDIDATES = ("candidate-1", "candidate-2", "candidate-3", "candidate-4")
REVIEWERS = ("R1", "R2")
VIEWPORTS = ("landscape-1180x820", "portrait-820x1180", "phone-390x844")
REQUIRED = ("contract-validation", "structural", "state-projection", "interaction", "accessibility", "responsive")

load = lambda p: json.loads(Path(p).read_text())
dump = lambda p, v: Path(p).write_text(json.dumps(v, indent=2) + "\n")
item_key = load(A / "semantic-review/item-key.json")
review = lambda r, item: load(A / "semantic-review/raw" / r / f"{item}.json")


# ---------- blind phase ----------

def score_obligation(index, per_reviewer):
    judgments = {r: per_reviewer[r]["obligations"][index] for r in REVIEWERS}
    passes = {r: j["judgment"] == "pass" for r, j in judgments.items()}
    responsive = {r: all(j["responsive"][vp] == "preserved" for vp in VIEWPORTS) for r, j in judgments.items()}
    return {
        "obligation": judgments["R1"]["obligation"],
        "individual": {r: {k: judgments[r][k] for k in ("judgment", "failureType", "evidence", "responsive")} for r in REVIEWERS},
        "passed": all(passes.values()),
        "reviewersAgree": len(set(passes.values())) == 1,
        "failureTypes": sorted({j["failureType"] for j in judgments.values() if j["judgment"] == "fail"}),
        "responsivePreserved": all(passes.values()) and all(responsive.values()),
    }


def score_item(entry):
    per_reviewer = {r: review(r, entry["item"]) for r in REVIEWERS}
    n = len(per_reviewer["R1"]["obligations"])
    obligations = [score_obligation(i, per_reviewer) for i in range(n)]
    reviewer_full = {r: all(o["judgment"] == "pass" for o in per_reviewer[r]["obligations"]) for r in REVIEWERS}
    return {
        **entry,
        "obligations": obligations,
        "fullyCorrect": all(o["passed"] for o in obligations),
        "fullyCorrectByReviewer": reviewer_full,
        "responsiveFullyPreserved": all(o["responsivePreserved"] for o in obligations),
        "reportedFabricatedOrForbidden": {r: per_reviewer[r].get("fabricatedOrForbiddenMeaning", []) for r in REVIEWERS},
        "evidenceLimitations": {r: per_reviewer[r].get("evidenceLimitations") for r in REVIEWERS},
    }


def deterministic(candidate):
    def run(rid):
        ev = load(A / "deterministic" / candidate / rid / "evidence.json")
        by = {x["requirement"]: x for x in ev["results"]}
        return {"complete": ev["complete"], "exitCode": load(A / "deterministic" / candidate / rid / "run.json")["exitCode"],
                "categories": {k: {"status": by[k]["status"], "reason": by[k].get("reason")} for k in REQUIRED}}
    combined = run("combined")
    return {
        "combined": combined,
        "requiredCategoriesPassed": sum(v["status"] == "Passed" for v in combined["categories"].values()),
        "requiredCategoriesEvaluated": len(REQUIRED),
        "perCase": {c: run(c) for c in CASES},
    }


def blind():
    items = sorted(map(score_item, item_key), key=lambda i: (i["candidate"], CASES.index(i["caseId"])))
    def candidate_summary(cand):
        mine = [i for i in items if i["candidate"] == cand]
        obs = [o for i in mine for o in i["obligations"]]
        return {
            "candidate": cand,
            "fullyCorrectCases": sum(i["fullyCorrect"] for i in mine),
            "cases": len(mine),
            "fullyCorrectCasesByReviewer": {r: sum(i["fullyCorrectByReviewer"][r] for i in mine) for r in REVIEWERS},
            "obligationsPassed": sum(o["passed"] for o in obs),
            "obligationsEvaluated": len(obs),
            "obligationsPassedByReviewer": {r: sum(o["individual"][r]["judgment"] == "pass" for o in obs) for r in REVIEWERS},
            "disagreements": sum(not o["reviewersAgree"] for o in obs),
            "responsiveFullyPreservedCases": sum(i["responsiveFullyPreserved"] for i in mine),
            "responsivePreservedObligations": sum(o["responsivePreserved"] for o in obs),
            "fabricatedOrContradictoryObligationFailures": sum(bool({"fabricated", "contradictory"} & set(o["failureTypes"])) for o in obs),
            "reviewerReportedFabricatedOrForbidden": {r: sum(len(i["reportedFabricatedOrForbidden"][r]) for i in mine) for r in REVIEWERS},
            "deterministic": deterministic(cand),
        }
    dump(A / "semantic-review/scores.blind.json", {
        "schemaVersion": 1, "phase": "blind (no candidate mapping read)",
        "scoringRule": "obligation Passed iff both R1 and R2 judge pass (EVALUATION-PROTOCOL.md)",
        "items": items, "candidates": list(map(candidate_summary, CANDIDATES)),
    })
    print(json.dumps([{k: c[k] for k in ("candidate", "fullyCorrectCases", "fullyCorrectCasesByReviewer", "obligationsPassed", "disagreements")} for c in map(candidate_summary, CANDIDATES)], indent=1))


# ---------- unblind phase ----------

def churn(m):
    git = lambda *a: subprocess.run(("git", "-C", str(REPO), *a), capture_output=True, text=True, check=True).stdout
    numstat = [l.split("\t") for l in git("diff", "--numstat", BASELINE, m["frozenCommit"]).splitlines() if l]
    return {"commitsSinceBaseline": int(git("rev-list", "--count", f"{BASELINE}..{m['frozenCommit']}")),
            "filesChanged": len(numstat), "linesAdded": sum(int(a) for a, _, _ in numstat),
            "linesDeleted": sum(int(d) for _, d, _ in numstat),
            "indexHtmlLines": next((int(a) for a, _, p in numstat if p.endswith("index.html")), None),
            "correctiveIterations": 0}


def group(rows, key):
    def fold(acc, r):
        g = acc.get(r[key], {"candidates": [], "fullyCorrectCases": 0, "cases": 0, "obligationsPassed": 0, "obligationsEvaluated": 0,
                             "byReviewer": {rv: 0 for rv in REVIEWERS}, "requiredCategoriesPassed": 0, "requiredCategoriesEvaluated": 0,
                             "responsiveFullyPreservedCases": 0, "fabricatedOrContradictoryObligationFailures": 0})
        return {**acc, r[key]: {
            "candidates": g["candidates"] + [r["lane"]],
            "fullyCorrectCases": g["fullyCorrectCases"] + r["fullyCorrectCases"],
            "cases": g["cases"] + r["cases"],
            "obligationsPassed": g["obligationsPassed"] + r["obligationsPassed"],
            "obligationsEvaluated": g["obligationsEvaluated"] + r["obligationsEvaluated"],
            "byReviewer": {rv: g["byReviewer"][rv] + r["fullyCorrectCasesByReviewer"][rv] for rv in REVIEWERS},
            "requiredCategoriesPassed": g["requiredCategoriesPassed"] + r["deterministic"]["requiredCategoriesPassed"],
            "requiredCategoriesEvaluated": g["requiredCategoriesEvaluated"] + r["deterministic"]["requiredCategoriesEvaluated"],
            "responsiveFullyPreservedCases": g["responsiveFullyPreservedCases"] + r["responsiveFullyPreservedCases"],
            "fabricatedOrContradictoryObligationFailures": g["fabricatedOrContradictoryObligationFailures"] + r["fabricatedOrContradictoryObligationFailures"],
        }}
    grouped = reduce(fold, rows, {})
    return {k: {**v, "fullyCorrectRate": v["fullyCorrectCases"] / v["cases"],
                "fullyCorrectRateByReviewer": {rv: n / v["cases"] for rv, n in v["byReviewer"].items()}} for k, v in grouped.items()}


def unblind():
    blind_scores = load(A / "semantic-review/scores.blind.json")
    mapping = {m["candidate"]: m for m in load(A / "candidate-mapping.json")["mapping"]}
    rows = [{**c, **{k: mapping[c["candidate"]][k] for k in ("lane", "branch", "provider", "condition", "frozenCommit")},
             "churn": churn(mapping[c["candidate"]])} for c in blind_scores["candidates"]]
    by_condition = group(rows, "condition")
    by_provider_condition = {p: group([r for r in rows if r["provider"] == p], "condition") for p in ("openai", "claude")}
    t, c = by_condition["treatment"]["fullyCorrectRate"], by_condition["control"]["fullyCorrectRate"]
    failures_by_obligation = Counter((i["caseId"], o["obligation"]) for i in blind_scores["items"] for o in i["obligations"] if not o["passed"])
    by_ob = [{"caseId": k[0], "obligation": k[1], "failedCandidates": sorted(mapping[i["candidate"]]["lane"] for i in blind_scores["items"]
              for o in i["obligations"] if i["caseId"] == k[0] and o["obligation"] == k[1] and not o["passed"]), "failures": n}
             for k, n in sorted(failures_by_obligation.items(), key=lambda kv: (CASES.index(kv[0][0]), kv[0][1]))]
    disagreements = [{"lane": mapping[i["candidate"]]["lane"], "candidate": i["candidate"], "caseId": i["caseId"], "obligation": o["obligation"],
                      "R1": o["individual"]["R1"]["judgment"], "R2": o["individual"]["R2"]["judgment"]}
                     for i in blind_scores["items"] for o in i["obligations"] if not o["reviewersAgree"]]
    results = {
        "schemaVersion": 1,
        "experiment": "EX-PERCEPTA-2026-0004",
        "hypothesis": "HY-PERCEPTA-2026-0005",
        "baseline": BASELINE,
        "heldOutCases": len(CASES),
        "obligationsPerCandidate": rows[0]["obligationsEvaluated"],
        "scoringRule": blind_scores["scoringRule"],
        "primaryOutcome": {
            "perCandidate": [{k: r[k] for k in ("candidate", "lane", "provider", "condition", "frozenCommit", "fullyCorrectCases", "cases")} for r in rows],
            "perCondition": {k: {kk: v[kk] for kk in ("candidates", "fullyCorrectCases", "cases", "fullyCorrectRate")} for k, v in by_condition.items()},
            "treatmentRate": t, "controlRate": c,
            "descriptiveSupport": t > c,
            "criterion": "descriptive support iff treatment fully-correct held-out-case rate > control rate (preregistered)",
            "statisticalClaim": "none; small descriptive pilot, not statistically conclusive",
        },
        "sensitivityByReviewer": {rv: {"treatmentRate": by_condition["treatment"]["fullyCorrectRateByReviewer"][rv],
                                       "controlRate": by_condition["control"]["fullyCorrectRateByReviewer"][rv],
                                       "treatmentHigher": by_condition["treatment"]["fullyCorrectRateByReviewer"][rv] > by_condition["control"]["fullyCorrectRateByReviewer"][rv]}
                                  for rv in REVIEWERS},
        "secondaryOutcomes": {
            "obligationsPassedByCondition": {k: {"passed": v["obligationsPassed"], "evaluated": v["obligationsEvaluated"]} for k, v in by_condition.items()},
            "deterministicRequiredCategoriesByCondition": {k: {"passed": v["requiredCategoriesPassed"], "evaluated": v["requiredCategoriesEvaluated"]} for k, v in by_condition.items()},
            "responsiveFullyPreservedCasesByCondition": {k: v["responsiveFullyPreservedCases"] for k, v in by_condition.items()},
            "fabricatedOrContradictoryObligationFailuresByCondition": {k: v["fabricatedOrContradictoryObligationFailures"] for k, v in by_condition.items()},
            "providerStratified": by_provider_condition,
            "failuresByObligation": by_ob,
            "perCandidate": [{k: r[k] for k in ("candidate", "lane", "provider", "condition", "obligationsPassed", "obligationsEvaluated", "obligationsPassedByReviewer",
                                                "fullyCorrectCasesByReviewer", "disagreements", "responsiveFullyPreservedCases", "responsivePreservedObligations",
                                                "fabricatedOrContradictoryObligationFailures", "reviewerReportedFabricatedOrForbidden", "churn")}
                             | {"deterministic": {"complete": r["deterministic"]["combined"]["complete"],
                                                  "requiredCategoriesPassed": r["deterministic"]["requiredCategoriesPassed"],
                                                  "requiredCategoriesEvaluated": r["deterministic"]["requiredCategoriesEvaluated"],
                                                  "categories": {k: v["status"] for k, v in r["deterministic"]["combined"]["categories"].items()}}}
                             for r in rows],
        },
        "reviewerDisagreements": disagreements,
        "excludedBranches": ["experiment/percepta-0004-openai-control-b"],
        "rawEvidence": {
            "integrity": "integrity.json", "protocol": "EVALUATION-PROTOCOL.md", "deterministic": "deterministic/<candidate>/<run>/",
            "capture": "capture/<candidate>/<case>/", "reviewerPrompt": "semantic-review/REVIEWER-PROMPT.md",
            "reviews": "semantic-review/raw/<R1|R2>/<item>.json", "itemKey": "semantic-review/item-key.json",
            "blindScores": "semantic-review/scores.blind.json", "mapping": "candidate-mapping.json", "manifest": "raw-evidence.SHA256SUMS",
        },
    }
    dump(A / "results.json", results)
    dump(A / "secondary-outcomes.json", {"schemaVersion": 1, "byCondition": by_condition, "byProviderAndCondition": by_provider_condition,
                                          "perCandidate": rows})
    header = "candidate\tlane\tprovider\tcondition\tfullyCorrectCases\tcases\tR1FullyCorrect\tR2FullyCorrect\tobligationsPassed\tobligationsEvaluated\tdisagreements\tdetRequiredPassed\tresponsiveFullCases\tfabContraFailures\tlinesAdded"
    lines = [f"{r['candidate']}\t{r['lane']}\t{r['provider']}\t{r['condition']}\t{r['fullyCorrectCases']}\t{r['cases']}\t{r['fullyCorrectCasesByReviewer']['R1']}\t{r['fullyCorrectCasesByReviewer']['R2']}\t{r['obligationsPassed']}\t{r['obligationsEvaluated']}\t{r['disagreements']}\t{r['deterministic']['requiredCategoriesPassed']}/6\t{r['responsiveFullyPreservedCases']}\t{r['fabricatedOrContradictoryObligationFailures']}\t{r['churn']['linesAdded']}" for r in rows]
    (A / "summary.tsv").write_text("\n".join([header, *lines]) + "\n")
    print(json.dumps(results["primaryOutcome"], indent=1))


if __name__ == "__main__":
    {"blind": blind, "unblind": unblind}[sys.argv[1]]()
