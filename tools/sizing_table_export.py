#!/usr/bin/env python3
"""採寸 jsonl(生・セグメント半径) を機械可読な採寸表 `vrcloth-sizing-table/1` へ整形する。

エージェント照合用の整形ビュー: 各部位の周径(girth = 2π·radius)＋meshHash(版同定)
＋計測条件。No Cache(周径スカラー＋一方向ハッシュのみ＝形状は復元不可、生 jsonl と
同じ粒度)。プラットフォーム非依存(BOOTH/エージェント市場/自作台帳で同じ表が通用)。

計測点名(bust/waist/hips/thigh/…)への標準化は docs/MEASUREMENT_SPEC.md §2「計測点の
標準化」(軸方向スライス周径＋極値検出)を VRClothBodyRadiusEstimator に実装した後。
現状はセグメントラベル(Hips→Spine 等)のまま出力する。

使い方:
    python tools/sizing_table_export.py <body-measurements.jsonl> [-o out.jsonl]
    -o 省略時は先頭1件を pretty 表示(デモ)。
"""
import argparse
import json
import math


def to_sizing_table(row):
    def girth_mm(rad_m):
        return round(2 * math.pi * rad_m * 1000, 1)

    return {
        "schema": "vrcloth-sizing-table/1",
        "avatar": row.get("avatar"),
        "meshHash": row.get("meshHash"),
        "conditions": row.get("conditions"),
        "heightMm": round(row.get("height_m", 0) * 1000, 1),
        "headCount": {
            "neckRef": row.get("headCount_neckRef"),
            "headRef": row.get("headCount_headRef"),
        },
        "bodyCoverage": row.get("bodyCoverage"),
        # セグメントラベルのまま。計測点名標準化は MEASUREMENT_SPEC §2(極値検出)後の TODO。
        "girths": [
            {
                "point": c["label"],
                "girthMm": girth_mm(c["radius_m"]),
                "lengthMm": round(c["length_m"] * 1000, 1),
                "estimated": c.get("estimated"),
            }
            for c in row.get("capsules", [])
        ],
    }


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("measurements", help="body-measurements.jsonl (生・セグメント半径)")
    ap.add_argument("-o", "--out", help="出力 jsonl(各行=1アバターの採寸表)。省略で先頭をデモ表示")
    args = ap.parse_args()

    with open(args.measurements, encoding="utf-8") as f:
        rows = [json.loads(line) for line in f if line.strip()]
    tables = [to_sizing_table(r) for r in rows]

    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            for t in tables:
                f.write(json.dumps(t, ensure_ascii=False) + "\n")
        print(f"wrote {len(tables)} sizing table(s) -> {args.out}")
    else:
        if tables:
            print(json.dumps(tables[0], ensure_ascii=False, indent=2))
        print(f"... ({len(tables)} avatar(s) total; pass -o <file> to write jsonl)")


if __name__ == "__main__":
    main()
