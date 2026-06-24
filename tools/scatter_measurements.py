#!/usr/bin/env python3
"""採寸コーパスの体型散布図を SVG で描く (docs/FAMILY_MODEL.md §5 の軸).
  x = 頭身 (Neck ref)        ＝ 硬い軸 (骨格比率)
  y = build = 平均 radius/length ＝ 柔らかい軸 (周径/スタイル) の代理
依存は標準ライブラリのみ (matplotlib 不要、SVG を手書き)。正規名は data/identity.json
で解決し、unkt と mini-stack を強調する。

    python tools/scatter_measurements.py <body.jsonl> [-o out.svg]
"""
import argparse
import json

try:
    from identity import Identity
except Exception:
    Identity = None

W, H, M = 980, 660, 78          # canvas, margin
DOT, FS = 5.5, 11               # dot radius, font size


def load(path):
    rows = []
    for line in open(path, encoding="utf-8"):
        line = line.strip()
        if not line:
            continue
        d = json.loads(line)
        hc = d.get("headCount_neckRef") or 0.0
        ratios = [c["radius_m"] / c["length_m"] for c in d["capsules"]
                  if c.get("length_m", 0) > 1e-6 and c.get("estimated", True)]
        build = sum(ratios) / len(ratios) if ratios else 0.0
        rows.append({"name": d["avatar"], "hc": hc, "build": build})
    return rows


def esc(s):
    return (s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;"))


def svg(rows, out):
    ids = Identity() if Identity else None
    xs = [r["hc"] for r in rows]
    ys = [r["build"] for r in rows]
    x0, x1 = min(xs), max(xs)
    y0, y1 = min(ys), max(ys)
    xpad = (x1 - x0) * 0.08 or 0.1
    ypad = (y1 - y0) * 0.10 or 0.01
    x0, x1 = x0 - xpad, x1 + xpad
    y0, y1 = y0 - ypad, y1 + ypad

    def px(x):
        return M + (x - x0) / (x1 - x0) * (W - 2 * M)

    def py(y):
        return H - M - (y - y0) / (y1 - y0) * (H - 2 * M)

    s = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" font-family="sans-serif">']
    s.append(f'<rect width="{W}" height="{H}" fill="white"/>')
    s.append(f'<text x="{W/2}" y="34" font-size="19" font-weight="bold" text-anchor="middle">'
             f'VRCloth 採寸コーパス 体型散布図 (n={len(rows)})</text>')
    # axes
    s.append(f'<line x1="{M}" y1="{H-M}" x2="{W-M}" y2="{H-M}" stroke="#333"/>')
    s.append(f'<line x1="{M}" y1="{M}" x2="{M}" y2="{H-M}" stroke="#333"/>')
    s.append(f'<text x="{W/2}" y="{H-26}" font-size="13" text-anchor="middle">頭身 (Neck ref) — 硬い軸 (骨格比率) →</text>')
    s.append(f'<text x="26" y="{H/2}" font-size="13" text-anchor="middle" transform="rotate(-90 26 {H/2})">build = 平均 radius/length — 柔らかい軸 →</text>')
    # ticks
    for i in range(5):
        gx = x0 + (x1 - x0) * i / 4
        s.append(f'<line x1="{px(gx)}" y1="{H-M}" x2="{px(gx)}" y2="{H-M+5}" stroke="#333"/>')
        s.append(f'<text x="{px(gx)}" y="{H-M+18}" font-size="11" text-anchor="middle">{gx:.1f}</text>')
        gy = y0 + (y1 - y0) * i / 4
        s.append(f'<line x1="{M-5}" y1="{py(gy)}" x2="{M}" y2="{py(gy)}" stroke="#333"/>')
        s.append(f'<text x="{M-9}" y="{py(gy)+4}" font-size="11" text-anchor="end">{gy:.3f}</text>')
    # points
    for r in rows:
        name = ids.canonical(r["name"]) if ids else r["name"]
        mini = ids.is_mini_stack(r["name"]) if ids else False
        is_unkt = (ids.canonical(r["name"]) if ids else r["name"]).lower() == "unkt"
        cx, cy = px(r["hc"]), py(r["build"])
        if is_unkt:
            fill, stroke, rad = "#e8112d", "#7a0a18", DOT + 2.5
        elif mini:
            fill, stroke, rad = "#2c7fb8", "#13405e", DOT
        else:
            fill, stroke, rad = "#9aa0a6", "#555", DOT
        s.append(f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="{rad:.1f}" fill="{fill}" stroke="{stroke}" stroke-width="1"/>')
        weight = "bold" if is_unkt else "normal"
        s.append(f'<text x="{cx+rad+3:.1f}" y="{cy+4:.1f}" font-size="{FS}" font-weight="{weight}">{esc(name)}</text>')
    # legend
    lx, ly = W - M - 150, M + 6
    s.append(f'<rect x="{lx-10}" y="{ly-16}" width="160" height="68" fill="white" stroke="#ccc"/>')
    for i, (col, lbl) in enumerate([("#e8112d", "unkt (非対応)"), ("#2c7fb8", "mini-stack 対応素体"), ("#9aa0a6", "その他")]):
        yy = ly + i * 18
        s.append(f'<circle cx="{lx}" cy="{yy}" r="{DOT}" fill="{col}" stroke="#555"/>')
        s.append(f'<text x="{lx+12}" y="{yy+4}" font-size="11">{lbl}</text>')
    s.append('</svg>')
    open(out, "w", encoding="utf-8").write("\n".join(s))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("body_jsonl")
    ap.add_argument("-o", "--out", default="vrcloth-scatter.svg")
    args = ap.parse_args()
    rows = load(args.body_jsonl)
    svg(rows, args.out)
    print(f"wrote {args.out}  ({len(rows)} avatars)")


if __name__ == "__main__":
    main()
