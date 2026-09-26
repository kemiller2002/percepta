"""EX-PERCEPTA-2026-0004 aggregation. Pure derivation from frozen raw evidence + unsealed mapping.
Usage: python3 aggregate.py <experiment-0004 dir> <held-out-cases.json> <out results.json>"""
import json, os, sys, subprocess
from functools import reduce
from itertools import groupby

ROOT, HELD_OUT, OUT = sys.argv[1:4]
BASELINE = "f474c449c2bc84da5707bb123c36500490875869"
VIEWPORTS = ["1440x900", "1180x820", "820x1180", "390x844"]
OTHER_VPS = VIEWPORTS[1:]
REVIEWERS = ["R1", "R2"]

load = lambda p: json.load(open(os.path.join(ROOT, p)))
cases = json.load(open(HELD_OUT))["cases"]
case_by_id = {c["id"]: c for c in cases}
mapping = {m["blindId"]: m for m in load("candidate-mapping.json")["mapping"]}
manifest = load("semantic-review/packet-manifest.json")
unit_meta = {u["reviewUnit"]: {"candidate": u["candidate"], "caseId": u["caseId"]} for u in manifest}
judgments = {(r, u): load(f"semantic-review/judgments/{r}/{u}.json") for r in REVIEWERS for u in unit_meta}

# ---------- obligation-level records ----------
def obligation_records(unit):
    meta = unit_meta[unit]
    obls = case_by_id[meta["caseId"]]["semanticObligations"]
    per_rev = {r: judgments[(r, unit)]["obligations"] for r in REVIEWERS}
    def rec(i, text):
        js = {r: per_rev[r][i] for r in REVIEWERS}
        assert all(j["obligation"] == text for j in js.values())
        prim = {r: js[r]["primary_1440x900"]["verdict"] for r in REVIEWERS}
        resp = {r: {vp: js[r]["responsive"][vp]["verdict"] for vp in OTHER_VPS} for r in REVIEWERS}
        return {
            "reviewUnit": unit, "candidate": meta["candidate"], "caseId": meta["caseId"], "obligationIndex": i, "obligation": text,
            "primary": prim, "primaryEvidence": {r: js[r]["primary_1440x900"]["evidence"] for r in REVIEWERS},
            "responsive": resp, "responsiveEvidence": {r: {vp: js[r]["responsive"][vp]["evidence"] for vp in OTHER_VPS} for r in REVIEWERS},
            "scored": "PASS" if all(v == "PASS" for v in prim.values()) else "FAIL",
            "reviewerDisagreement": len(set(prim.values())) > 1,
            "responsiveDisagreement": [vp for vp in OTHER_VPS if len({resp[r][vp] for r in REVIEWERS}) > 1],
        }
    return [rec(i, t) for i, t in enumerate(obls)]

records = [r for u in sorted(unit_meta) for r in obligation_records(u)]
cands = sorted(mapping)

def case_full(cand, case_id, verdict_of):
    rs = [r for r in records if r["candidate"] == cand and r["caseId"] == case_id]
    return len(rs) > 0 and all(verdict_of(r) == "PASS" for r in rs)

scored = lambda r: r["scored"]
by_reviewer = lambda rev: (lambda r: r["primary"][rev])

def per_candidate(verdict_of):
    return {c: {"fullyCorrectCases": sum(case_full(c, k["id"], verdict_of) for k in cases), "heldOutCases": len(cases),
                "fullyCorrectCaseIds": [k["id"] for k in cases if case_full(c, k["id"], verdict_of)]} for c in cands}

def per_condition(pc):
    conds = sorted({mapping[c]["condition"] for c in cands})
    def agg(cond):
        cs = [c for c in cands if mapping[c]["condition"] == cond]
        n = sum(pc[c]["fullyCorrectCases"] for c in cs); d = sum(pc[c]["heldOutCases"] for c in cs)
        return {"candidates": cs, "fullyCorrect": n, "total": d, "rate": n / d}
    return {cond: agg(cond) for cond in conds}

def verdict(pcond):
    t, c = pcond["treatment"]["rate"], pcond["control"]["rate"]
    return {"treatmentRate": t, "controlRate": c, "rule": "support only if treatment rate > control rate",
            "result": "descriptive support" if t > c else "no descriptive support"}

primary_pc = per_candidate(scored)
primary_cond = per_condition(primary_pc)
sensitivity = {rev: (lambda pc: {"perCandidate": pc, "perCondition": per_condition(pc), "hypothesis": verdict(per_condition(pc))})(per_candidate(by_reviewer(rev))) for rev in REVIEWERS}

# ---------- secondary: obligations passed / evaluated ----------
def obl_counts(pred):
    rs = [r for r in records if pred(r)]
    return {"passed": sum(r["scored"] == "PASS" for r in rs), "evaluated": len(rs)}
obl_by_cand = {c: obl_counts(lambda r, c=c: r["candidate"] == c) for c in cands}
obl_by_cond = {cond: obl_counts(lambda r, cond=cond: mapping[r["candidate"]]["condition"] == cond) for cond in ("control", "treatment")}

# ---------- secondary: failures by obligation ----------
failures_by_obligation = [
    {"caseId": r["caseId"], "obligation": r["obligation"], "candidate": r["candidate"], "condition": mapping[r["candidate"]]["condition"],
     "provider": mapping[r["candidate"]]["provider"], "R1": r["primary"]["R1"], "R2": r["primary"]["R2"],
     "evidence": {rev: r["primaryEvidence"][rev] for rev in REVIEWERS if r["primary"][rev] == "FAIL"}}
    for r in records if r["scored"] == "FAIL"]

# ---------- secondary: deterministic required-category completion ----------
def verifier(c):
    d = load(f"raw/candidate-{mapping[c]['dir']}/verifier/evidence.json")
    req = [x for x in d["results"] if x["required"]]
    return {"complete": d["complete"], "requiredPassed": sum(x["status"] == "Passed" for x in req), "requiredTotal": len(req),
            "categories": {x["requirement"]: x["status"] for x in d["results"]},
            "failedReasons": {x["requirement"]: x.get("reason") for x in d["results"] if x["status"] != "Passed"},
            "exitCode": int(open(os.path.join(ROOT, f"raw/candidate-{mapping[c]['dir']}/verifier/exit-code.txt")).read().strip())}
det_completion = {c: verifier(c) for c in cands}

# ---------- deterministic per-state probe summary ----------
def probe(c, case_id):
    o = load(f"raw/candidate-{mapping[c]['dir']}/probe/{case_id}/observations.json")
    legal = case_by_id[case_id]["state"]["capabilities"]["confirm-root-cause"]
    def vp_summary(v):
        ob = v["observations"]; cap = ob["capability"]
        unavailable = bool(cap["disabledProperty"]) or cap["ariaDisabled"] == "true"
        return {
            "viewport": v["viewport"]["name"], "infrastructureFailure": v["infrastructureFailure"],
            "entryPointExecuted": v["setState"].get("entryPoint") is True and v["setState"].get("threw") is False,
            "regionsVisible": {k: r["visible"] for k, r in ob["regions"].items()},
            "capabilityPresent": cap["count"] > 0, "capabilityVisible": cap["visible"],
            "capabilityUnavailable": unavailable, "capabilityMatchesLegality": unavailable == (legal == "illegal"),
            "unavailableReasonHookVisible": any(x["visible"] and x["text"] for x in ob["unavailableReason"]),
            "blockerLinkHookVisibleWithTarget": any(x["visible"] and x["targetExists"] for x in ob["blockerLinks"]),
            "accessibilityFloor": {"lang": bool(ob["accessibility"]["lang"]), "singleMain": ob["accessibility"]["mainCount"] >= 1,
                                   "singleH1": ob["accessibility"]["h1Count"] == 1, "noDuplicateIds": not ob["accessibility"]["duplicateIds"],
                                   "allControlsNamed": not ob["accessibility"]["unnamedControls"], "noColorOnlyStateHooks": ob["accessibility"]["colorOnlyStateElements"] == 0},
            "horizontalOverflow": ob["overflow"]["horizontalOverflow"],
            "breakpointRequiredRegionsAllVisible": ob["breakpointRequiredRegionsAllVisible"],
            "pageErrors": len(v["errors"]["pageErrors"]), "consoleErrors": len(v["errors"]["consoleErrors"]), "failedRequests": len(v["errors"]["failedRequests"]),
        }
    return {"caseId": case_id, "capabilityAuthority": legal, "viewports": [vp_summary(v) for v in o["viewports"]], "operationTraceSteps": len(o["operationTrace"])}
det_probe = {c: [probe(c, k["id"]) for k in cases] for c in cands}
infra_failures = [(c, p["caseId"], v["viewport"]) for c in cands for p in det_probe[c] for v in p["viewports"] if v["infrastructureFailure"]]
runtime_errors = [(c, p["caseId"], v["viewport"], v["pageErrors"], v["consoleErrors"]) for c in cands for p in det_probe[c] for v in p["viewports"] if v["pageErrors"] or v["consoleErrors"]]

# ---------- secondary: forbidden / fabricated meaning ----------
def defects(unit):
    meta = unit_meta[unit]
    return [{"reviewUnit": unit, "candidate": meta["candidate"], "caseId": meta["caseId"], "reviewer": r, **d}
            for r in REVIEWERS for d in judgments[(r, unit)].get("forbiddenOrFabricatedMeaning", [])]
all_defects = [d for u in sorted(unit_meta) for d in defects(u)]
defect_summary = {c: {"reportedByAnyReviewer": sum(d["candidate"] == c for d in all_defects),
                      "unitsWithAnyDefect": len({d["reviewUnit"] for d in all_defects if d["candidate"] == c}),
                      "unitsWithDefectFromBothReviewers": len({d["reviewUnit"] for d in all_defects if d["candidate"] == c and
                                                               {x["reviewer"] for x in all_defects if x["reviewUnit"] == d["reviewUnit"]} == set(REVIEWERS)})}
                  for c in cands}

# ---------- secondary: responsive preservation ----------
def responsive(pred):
    base = [r for r in records if pred(r) and r["scored"] == "PASS"]
    kept = [r for r in base if all(r["responsive"][rev][vp] == "PASS" for rev in REVIEWERS for vp in OTHER_VPS)]
    return {"desktopPassedObligations": len(base), "preservedAtAllOtherViewportsByBothReviewers": len(kept),
            "notPreserved": [{"caseId": r["caseId"], "obligation": r["obligation"],
                              "failures": {rev: [vp for vp in OTHER_VPS if r["responsive"][rev][vp] == "FAIL"] for rev in REVIEWERS}} for r in base if r not in kept]}
responsive_by_cand = {c: responsive(lambda r, c=c: r["candidate"] == c) for c in cands}
responsive_fail_any = [{"candidate": r["candidate"], "caseId": r["caseId"], "obligation": r["obligation"], "desktopScored": r["scored"],
                        "responsiveFails": {rev: [vp for vp in OTHER_VPS if r["responsive"][rev][vp] == "FAIL"] for rev in REVIEWERS}}
                       for r in records if any(r["responsive"][rev][vp] == "FAIL" for rev in REVIEWERS for vp in OTHER_VPS)]
det_responsive = {c: {"statesXviewports": sum(len(p["viewports"]) for p in det_probe[c]),
                      "noOverflow": sum(not v["horizontalOverflow"] for p in det_probe[c] for v in p["viewports"]),
                      "contractRequiredRegionsVisible": sum(v["breakpointRequiredRegionsAllVisible"] for p in det_probe[c] for v in p["viewports"])} for c in cands}

# ---------- secondary: churn ----------
def churn(c):
    sha = mapping[c]["sha"]
    rows = [l.split("\t") for l in subprocess.check_output(["git", "diff", "--numstat", BASELINE, sha], text=True).strip().splitlines()]
    commits = subprocess.check_output(["git", "rev-list", "--count", f"{BASELINE}..{sha}"], text=True).strip()
    return {"filesChanged": len(rows), "linesAdded": sum(int(a) for a, _, _ in rows), "linesDeleted": sum(int(d) for _, d, _ in rows),
            "commitsFromBaseline": int(commits), "files": [{"path": p, "added": int(a), "deleted": int(d)} for a, d, p in rows]}
churn_by_cand = {c: churn(c) for c in cands}

# ---------- disagreements ----------
disagreements = [{"candidate": r["candidate"], "caseId": r["caseId"], "obligation": r["obligation"], "R1": r["primary"]["R1"], "R2": r["primary"]["R2"],
                  "evidence": r["primaryEvidence"]} for r in records if r["reviewerDisagreement"]]
resp_disagreements = sum(len(r["responsiveDisagreement"]) for r in records)

# ---------- provider stratification ----------
providers = sorted({m["provider"] for m in mapping.values()})
def pair(p):
    get = lambda cond: next(c for c in cands if mapping[c]["provider"] == p and mapping[c]["condition"] == cond)
    ctl, trt = get("control"), get("treatment")
    return {"control": {"candidate": ctl, "branch": mapping[ctl]["branch"], "fullyCorrectCases": primary_pc[ctl]["fullyCorrectCases"], "obligations": obl_by_cand[ctl], "deterministicRequiredPassed": det_completion[ctl]["requiredPassed"]},
            "treatment": {"candidate": trt, "branch": mapping[trt]["branch"], "fullyCorrectCases": primary_pc[trt]["fullyCorrectCases"], "obligations": obl_by_cand[trt], "deterministicRequiredPassed": det_completion[trt]["requiredPassed"]},
            "treatmentMinusControlFullyCorrect": primary_pc[trt]["fullyCorrectCases"] - primary_pc[ctl]["fullyCorrectCases"]}

results = {
    "experimentId": "EX-PERCEPTA-2026-0004",
    "hypothesis": "HY-PERCEPTA-2026-0005",
    "baseline": BASELINE,
    "heldOut": {"path": "research/experiments/EX-PERCEPTA-2026-0004/held-out-semantic-cases.json", "blob": "b702a376bf5b8f3d33a9d348785c5b3b1c50e687",
                "cases": len(cases), "semanticObligations": sum(len(c["semanticObligations"]) for c in cases)},
    "candidates": {c: {k: mapping[c][k] for k in ("branch", "sha", "lane", "provider", "condition", "indexBlob", "indexSha256")} for c in cands},
    "excluded": {"experiment/percepta-0004-openai-control-b": "not a preregistered candidate; never staged, rendered, reviewed or aggregated"},
    "scoringRule": "obligation PASS only if both blinded reviewers judged PASS at 1440x900 (declared in EVALUATION-PROCEDURE.md before rendering)",
    "primary": {"perCandidate": primary_pc, "perCondition": primary_cond, "hypothesis": verdict(primary_cond)},
    "sensitivityPerReviewer": sensitivity,
    "secondary": {
        "semanticObligationsPassed": {"perCandidate": obl_by_cand, "perCondition": obl_by_cond},
        "failuresByObligation": failures_by_obligation,
        "deterministicRequiredCategoryCompletion": det_completion,
        "providerStratified": {p: pair(p) for p in providers},
        "forbiddenOrFabricatedMeaning": {"perCandidate": defect_summary, "defects": all_defects},
        "responsivePreservation": {"semanticPerCandidate": responsive_by_cand, "anyReviewerResponsiveFailure": responsive_fail_any, "deterministicPerCandidate": det_responsive},
        "implementationChurn": churn_by_cand,
    },
    "reviewerDisagreements": {"primary": disagreements, "primaryCount": len(disagreements), "responsiveVerdictDisagreements": resp_disagreements,
                              "obligationJudgmentsPerReviewer": len(records)},
    "deterministicPerState": det_probe,
    "infrastructureFailures": infra_failures,
    "runtimeErrors": runtime_errors,
    "obligationRecords": records,
}
json.dump(results, open(OUT, "w"), indent=2)
print(json.dumps({"primary": {c: primary_pc[c]["fullyCorrectCases"] for c in cands}, "perCondition": primary_cond, "hypothesis": verdict(primary_cond)["result"],
                  "sensitivity": {r: sensitivity[r]["hypothesis"] for r in REVIEWERS}, "obligations": obl_by_cand, "disagreements": len(disagreements),
                  "detCompletion": {c: det_completion[c]["requiredPassed"] for c in cands}, "infra": infra_failures, "runtimeErrors": runtime_errors}, indent=1))
