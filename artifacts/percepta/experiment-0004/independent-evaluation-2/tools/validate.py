"""Independent recomputation of every primary/secondary aggregate from raw judgment files (separate code path)."""
import json, glob, os, sys
R = json.load(open("results.json")); mp = {m["blindId"]: m for m in json.load(open("candidate-mapping.json"))["mapping"]}
man = {u["reviewUnit"]: u for u in json.load(open("semantic-review/packet-manifest.json"))}
ho = json.load(open(sys.argv[1]))["cases"]
checks = []
def ok(name, a, b): checks.append((name, a == b, a, b))
# reviewer verdict table
tab = {}
for f in glob.glob("semantic-review/judgments/R?/unit-*.json"):
    if f.endswith("tool-log.json"): continue
    rv = f.split("/")[2]; u = os.path.basename(f)[:-5]; j = json.load(open(f))
    for i, o in enumerate(j["obligations"]): tab[(man[u]["candidate"], man[u]["caseId"], i, rv)] = o["primary_1440x900"]["verdict"]
ok("judgment cells", len(tab), 2 * 4 * 25)
ok("case count", len(ho), 5); ok("obligation count", sum(len(c["semanticObligations"]) for c in ho), 25)
for c in mp:
    full = 0; passed = 0
    for k in ho:
        n = len(k["semanticObligations"])
        cons = [all(tab[(c, k["id"], i, r)] == "PASS" for r in ("R1", "R2")) for i in range(n)]
        passed += sum(cons); full += all(cons)
    ok(f"{c} fully-correct", R["primary"]["perCandidate"][c]["fullyCorrectCases"], full)
    ok(f"{c} obligations passed", R["secondary"]["semanticObligationsPassed"]["perCandidate"][c]["passed"], passed)
for cond in ("control", "treatment"):
    cs = [c for c in mp if mp[c]["condition"] == cond]
    n = sum(R["primary"]["perCandidate"][c]["fullyCorrectCases"] for c in cs)
    ok(f"{cond} numerator", R["primary"]["perCondition"][cond]["fullyCorrect"], n); ok(f"{cond} denominator", R["primary"]["perCondition"][cond]["total"], 10)
    ok(f"{cond} rate", R["primary"]["perCondition"][cond]["rate"], n / 10)
t, c = R["primary"]["perCondition"]["treatment"]["rate"], R["primary"]["perCondition"]["control"]["rate"]
ok("hypothesis verdict", R["primary"]["hypothesis"]["result"], "descriptive support" if t > c else "no descriptive support")
ok("disagreement count", R["reviewerDisagreements"]["primaryCount"], sum(1 for (cc, k, i, r) in tab if r == "R1" and tab[(cc, k, i, "R1")] != tab[(cc, k, i, "R2")]))
for cc in mp:
    ev = json.load(open(f"raw/candidate-{mp[cc]['dir']}/verifier/evidence.json"))
    ok(f"{cc} det required passed", R["secondary"]["deterministicRequiredCategoryCompletion"][cc]["requiredPassed"], sum(1 for x in ev["results"] if x["required"] and x["status"] == "Passed"))
    ok(f"{cc} sha in mapping", R["candidates"][cc]["sha"], mp[cc]["sha"])
ok("excluded branch absent", any("openai-control-b" in m["branch"] for m in mp.values()), False)
bad = [x for x in checks if not x[1]]
print(f"{len(checks)} checks, {len(bad)} failed"); [print("FAIL", x) for x in bad]
json.dump({"checks": [{"name": n, "ok": o, "results": a, "recomputed": b} for n, o, a, b in checks]}, open("validation.json", "w"), indent=1)
sys.exit(1 if bad else 0)
