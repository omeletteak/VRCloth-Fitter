#!/usr/bin/env python3
"""採寸照合 = (avatar × garment) マッチング行列 (docs/MEASUREMENT_SPEC.md §7,
ECOSYSTEM_VISION.md §3.1). 着用せずに、body 採寸表(素体の半径)と garment 採寸表
(衣装の内周半径)から、各ペアのクリアランス/貫通を**予測**する。これは予測層であり、
最終判定は保証層(プリフライト診断)が行う(FAMILY_MODEL §9「ラベルは予測、診断が判定」)。

モデルと前提(正直に):
  衣装 G(設計素体 A 上で測定、内周半径 gi・カプセル長 gl)を素体 B(半径 bi・長 bl)に
  着せると、Merge Armature が骨名でマージしカプセル長は B に合う。内周は **B の骨長へ
  等方スケール**すると仮定: scaled_inner = gi * (bl/gl)。
    clearance = scaled_inner - bi   (負 = 素体が衣装内周より太い = 貫通予測)
  ⇔ 貫通条件は scale 不変の比較 gi/gl < bi/bl(衣装の比率 < 素体の比率)。
  ※ MA がスケール調整しない運用では内周は authored のままで、本モデルは近似。検証は
    保証層(プリフライト)で(§8 差分診断)。

使い方:
    python tools/matching_matrix.py <body.jsonl> <garment.jsonl> [--garment NAME]
                                    [--green-mm 10] [--red-mm 30]
"""
import argparse
import json


def load_body(path):
    bodies = {}
    for line in open(path, encoding="utf-8"):
        line = line.strip()
        if not line:
            continue
        d = json.loads(line)
        bodies[d["avatar"]] = {c["label"]: (c["radius_m"], c["length_m"]) for c in d["capsules"]}
    return bodies


def load_garments(path):
    garments = {}
    try:
        f = open(path, encoding="utf-8")
    except FileNotFoundError:
        return garments
    for line in f:
        line = line.strip()
        if not line:
            continue
        d = json.loads(line)
        # only capsules the garment actually spans (estimated=True)
        caps = {c["label"]: (c["radius_m"], c["length_m"]) for c in d["capsules"] if c.get("estimated")}
        garments[d["garment"]] = dict(onAvatar=d.get("onAvatar", ""), caps=caps)
    return garments


def evaluate(body, garment_caps, green_mm, red_mm):
    """Per-pair clearance over the garment's covered capsules. Returns dict or None."""
    worst_pen = 0.0   # mm, most negative clearance as positive depth
    pen_caps = 0
    min_clear = None  # mm
    n = 0
    for label, (gi, gl) in garment_caps.items():
        if label not in body or gl <= 0:
            continue
        bi, bl = body[label]
        scaled_inner = gi * (bl / gl)
        clearance_mm = (scaled_inner - bi) * 1000.0
        n += 1
        if clearance_mm < 0:
            pen_caps += 1
            worst_pen = max(worst_pen, -clearance_mm)
        min_clear = clearance_mm if min_clear is None else min(min_clear, clearance_mm)
    if n == 0:
        return None
    if worst_pen > red_mm:
        verdict = "RED"      # retargeting-class (out of fix envelope)
    elif worst_pen > green_mm:
        verdict = "YELLOW"   # fixable by declipper
    elif min_clear is not None and min_clear > 20.0:
        verdict = "LOOSE"    # no penetration but baggy (TIGHT/FIT/LOOSE §3)
    else:
        verdict = "GREEN"    # fits
    return dict(verdict=verdict, maxPen_mm=worst_pen, penCaps=pen_caps, capsules=n, minClear_mm=min_clear)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("body_jsonl")
    ap.add_argument("garment_jsonl")
    ap.add_argument("--garment", help="focus one garment: rank all bodies by fit")
    ap.add_argument("--green-mm", type=float, default=10.0)
    ap.add_argument("--red-mm", type=float, default=30.0)
    args = ap.parse_args()

    bodies = load_body(args.body_jsonl)
    garments = load_garments(args.garment_jsonl)
    if not garments:
        print(f"no garment measurements in {args.garment_jsonl} "
              f"(measure with the 衣装採寸 button / a garment CLI mode first)")
        return

    names = [args.garment] if args.garment else list(garments)
    rank = {"GREEN": 0, "LOOSE": 1, "YELLOW": 2, "RED": 3}
    for gname in names:
        if gname not in garments:
            print(f"garment '{gname}' not found")
            continue
        g = garments[gname]
        print(f"\n=== garment '{gname}' (designed on '{g['onAvatar']}', spans {len(g['caps'])} capsules) ===")
        rows = []
        for bname, body in bodies.items():
            r = evaluate(body, g["caps"], args.green_mm, args.red_mm)
            if r:
                rows.append((bname, r))
        rows.sort(key=lambda x: (rank[x[1]["verdict"]], x[1]["maxPen_mm"]))
        print(f"  {'body':<30}{'verdict':<8}{'maxPen':>8}{'penCaps':>9}{'minClear':>10}")
        for bname, r in rows:
            print(f"  {bname:<30}{r['verdict']:<8}{r['maxPen_mm']:>7.1f}mm{r['penCaps']:>6}/{r['capsules']:<2}{r['minClear_mm']:>8.1f}mm")


if __name__ == "__main__":
    main()
