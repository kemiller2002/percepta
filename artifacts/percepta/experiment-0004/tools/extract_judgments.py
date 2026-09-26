"""Extract each blinded reviewer's final JSON judgment verbatim from its subagent transcript,
plus an audit log of every tool call the reviewer made (for blinding-compliance checks)."""
import json, sys, os, re

tasks_dir, assignments_path, out_dir = sys.argv[1:4]
assignments = json.load(open(assignments_path))  # [{reviewer, unit, agent}]

def events(path): return [json.loads(l) for l in open(path) if l.strip()]

def blocks(ev):
    m = ev.get("message")
    c = m.get("content") if isinstance(m, dict) else None
    return c if isinstance(c, list) else []

def final_report(evs):
    uses = [b for e in evs if e.get("type") == "assistant" for b in blocks(e) if b.get("type") == "tool_use"]
    handback = [u for u in uses if "handback" in u.get("name", "").lower()]
    texts = [b.get("text", "") for e in evs if e.get("type") == "assistant" for b in blocks(e) if b.get("type") == "text"]
    if handback:
        inp = handback[-1].get("input", {})
        return next((v for v in inp.values() if isinstance(v, str) and "{" in v), json.dumps(inp)), "handback"
    return (texts[-1] if texts else ""), "final-text"

def parse_json(s):
    m = re.search(r"\{.*\}", s, re.S)
    return json.loads(m.group(0)) if m else None

def tool_log(evs):
    return [{"tool": b.get("name"), "input": b.get("input")} for e in evs if e.get("type") == "assistant" for b in blocks(e) if b.get("type") == "tool_use"]

def extract(a):
    evs = events(os.path.join(tasks_dir, f"{a['agent']}.output"))
    raw, source = final_report(evs)
    d = os.path.join(out_dir, a["reviewer"]); os.makedirs(d, exist_ok=True)
    open(os.path.join(d, f"{a['unit']}.raw.txt"), "w").write(raw)
    parsed = parse_json(raw)
    json.dump(parsed, open(os.path.join(d, f"{a['unit']}.json"), "w"), indent=2)
    json.dump(tool_log(evs), open(os.path.join(d, f"{a['unit']}.tool-log.json"), "w"), indent=2)
    return {"reviewer": a["reviewer"], "unit": a["unit"], "source": source, "parsed": parsed is not None,
            "unitMatches": (parsed or {}).get("reviewUnit") == a["unit"], "reviewerMatches": (parsed or {}).get("reviewer") == a["reviewer"],
            "nObligations": len((parsed or {}).get("obligations", []))}

print(json.dumps([extract(a) for a in assignments]))
