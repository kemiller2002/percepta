"""Validate raw reviewer outputs against the review schema and verbatim obligations. Reads only; never edits.

Usage: validate_reviews.py <reviewsDir> <packetsDir>
"""
import json, sys
from pathlib import Path

REVIEWS, PACKETS = map(Path, sys.argv[1:3])
JUDGMENTS = {"pass", "fail"}
FAILURE_TYPES = {None, "missing", "ambiguous", "fabricated", "contradictory", "inaccessible-operation", "not-preserved"}
VIEWPORTS = ("landscape-1180x820", "portrait-820x1180", "phone-390x844")
RESP = {"preserved", "not-preserved"}


def problems_for(reviewer, item):
    path = REVIEWS / reviewer / f"{item}.json"
    if not path.exists():
        return ["missing output file"]
    try:
        review = json.loads(path.read_text())
    except json.JSONDecodeError as e:
        return [f"invalid JSON: {e}"]
    expected = json.loads((PACKETS / item / "obligations.json").read_text())
    obs = review.get("obligations", [])
    texts = [o.get("obligation") for o in obs]
    per_obligation = [
        msg for o in obs for msg in (
            *([f"bad judgment {o.get('judgment')!r}"] if o.get("judgment") not in JUDGMENTS else []),
            *([f"bad failureType {o.get('failureType')!r}"] if o.get("failureType") not in FAILURE_TYPES else []),
            *([f"fail without failureType: {o.get('obligation')}"] if o.get("judgment") == "fail" and not o.get("failureType") else []),
            *([f"empty evidence: {o.get('obligation')}"] if not str(o.get("evidence", "")).strip() else []),
            *([f"bad responsive {vp}: {o.get('obligation')}"] for vp in VIEWPORTS if (o.get("responsive") or {}).get(vp) not in RESP),
        )
    ]
    return [
        *([f"item mismatch {review.get('item')!r}"] if review.get("item") != item else []),
        *([f"reviewer mismatch {review.get('reviewer')!r}"] if review.get("reviewer") != reviewer else []),
        *([f"obligations not verbatim/in order: {texts} != {expected}"] if texts != expected else []),
        *per_obligation,
        *(["fabricatedOrForbiddenMeaning not a list"] if not isinstance(review.get("fabricatedOrForbiddenMeaning"), list) else []),
    ]


if __name__ == "__main__":
    items = sorted(p.name for p in PACKETS.iterdir() if p.name.startswith("item-"))
    report = {f"{r}/{i}": problems_for(r, i) for r in ("R1", "R2") for i in items}
    bad = {k: v for k, v in report.items() if v}
    print(json.dumps({"checked": len(report), "valid": len(report) - len(bad), "problems": bad}, indent=2))
