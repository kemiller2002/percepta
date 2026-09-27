"""Build blinded semantic-review packets (one per blind candidate x held-out case).
Packets contain only: blind candidate id, rendered evidence for that state, the single state, its obligations."""
import json, os, sys, shutil, hashlib, secrets
from functools import reduce
from PIL import Image

held_out, raw_dir, out_dir = sys.argv[1:4]
TILE_H, OVERLAP = 1100, 80
VIEWPORTS = ["1440x900", "1180x820", "820x1180", "390x844"]
cases = json.load(open(held_out))["cases"]
units = [(c, k) for c in "KMRV" for k in cases]
order = list(range(len(units))); secrets.SystemRandom().shuffle(order)

def sha(p): return hashlib.sha256(open(p, "rb").read()).hexdigest()

def tiles(src, dst_prefix):
    im = Image.open(src); w, h = im.size
    tops = list(range(0, max(h - OVERLAP, 1), TILE_H - OVERLAP)) if h > TILE_H + 200 else [0]
    def save(i, top):
        p = f"{dst_prefix}-part{i+1}of{len(tops)}.png"
        im.crop((0, top, w, min(top + TILE_H, h))).save(p); return os.path.basename(p)
    return [save(i, t) for i, t in enumerate(tops)] if len(tops) > 1 else []

def strip_trace(step):
    t = step["target"]
    return {"step": t["index"], "element": {"tag": t["tag"], "role": t["role"], "visibleLabel": t["label"], "href": t["href"]},
            "click": step.get("click"), "before": step.get("before"), "after": step.get("after"), "viewportScreenshotAfterClick": step.get("screenshot")}

def build(n, unit):
    cand, kase = unit
    uid = f"unit-{n:02d}"
    d = os.path.join(out_dir, uid); os.makedirs(os.path.join(d, "trace"), exist_ok=True)
    src = os.path.join(raw_dir, f"candidate-{cand}", "probe", kase["id"])
    obs = json.load(open(os.path.join(src, "observations.json")))
    shots = {vp: {"fullPage": f"screenshot-{vp}.png"} for vp in VIEWPORTS}
    for vp in VIEWPORTS:
        shutil.copyfile(os.path.join(src, f"{vp}.png"), os.path.join(d, f"screenshot-{vp}.png"))
        shots[vp]["tiles"] = tiles(os.path.join(d, f"screenshot-{vp}.png"), os.path.join(d, f"screenshot-{vp}"))
    desk = next(v for v in obs["viewports"] if v["viewport"]["name"] == "1440x900")
    open(os.path.join(d, "aria-snapshot-1440x900.txt"), "w").write(desk.get("ariaSnapshot", ""))
    open(os.path.join(d, "rendered-text-1440x900.txt"), "w").write(desk.get("innerText", ""))
    trace = [strip_trace(s) for s in obs["operationTrace"]]
    for s in obs["operationTrace"]:
        if s.get("screenshot"): shutil.copyfile(os.path.join(src, s["screenshot"]), os.path.join(d, s["screenshot"]))
    json.dump(trace, open(os.path.join(d, "operation-trace-1440x900.json"), "w"), indent=2)
    packet = {"reviewUnit": uid, "candidate": f"Candidate {cand}", "heldOutState": kase["state"],
              "semanticObligations": kase["semanticObligations"], "screenshots": shots}
    json.dump(packet, open(os.path.join(d, "packet.json"), "w"), indent=2)
    files = sorted(os.path.relpath(os.path.join(r, f), d) for r, _, fs in os.walk(d) for f in fs)
    return {"reviewUnit": uid, "candidate": f"Candidate {cand}", "caseId": kase["id"], "sourceDir": os.path.relpath(src, raw_dir),
            "files": {f: sha(os.path.join(d, f)) for f in files}}

manifest = [build(i + 1, units[j]) for i, j in enumerate(order)]
json.dump(manifest, open(os.path.join(out_dir, "..", "packet-manifest.json"), "w"), indent=2)
print(len(manifest), "packets")
