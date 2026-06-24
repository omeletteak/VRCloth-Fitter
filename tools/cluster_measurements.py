#!/usr/bin/env python3
"""採寸 SQLite から体型ファミリーをクラスタリングする (docs/MEASUREMENT_SPEC.md §5,
docs/FAMILY_MODEL.md §7). 形状ベクトル(カプセル半径を L2 正規化＝全体スケール除去)
の平均連結 agglomerative clustering。scipy 不要 (標準ライブラリのみ)。

使い方:
    python tools/cluster_measurements.py <db.sqlite> [--threshold 0.25] [--highlight "<avatar>"]
"""
import argparse
import math
import sqlite3

try:
    from identity import Identity  # tools/ is on sys.path when run as a script
except Exception:
    Identity = None


def load_vectors(db):
    con = sqlite3.connect(db)
    cur = con.cursor()
    meta = {n: (hn, hh, ht) for n, hn, hh, ht in cur.execute(
        "SELECT name, head_count_neck, head_count_head, height_m FROM avatars")}
    vecs = {}
    for name in meta:
        caps = sorted(cur.execute(
            "SELECT label, radius_m FROM capsule_measurements WHERE avatar=? ORDER BY label", (name,)))
        labels = [l for l, _ in caps]
        radii = [r for _, r in caps]
        norm = math.sqrt(sum(r * r for r in radii)) or 1.0
        vecs[name] = (labels, [r / norm for r in radii])
    con.close()
    return meta, vecs


def dist(a, b):
    # Euclidean over the shared, identically-ordered capsule labels.
    la, va = a
    lb, vb = b
    if la == lb:
        return math.sqrt(sum((x - y) ** 2 for x, y in zip(va, vb)))
    common = sorted(set(la) & set(lb))
    da = dict(zip(la, va))
    db_ = dict(zip(lb, vb))
    return math.sqrt(sum((da[l] - db_[l]) ** 2 for l in common))


def cluster(vecs, threshold):
    names = list(vecs)
    clusters = [[n] for n in names]
    pair = {}  # cache element distances
    for i in range(len(names)):
        for j in range(i + 1, len(names)):
            pair[(names[i], names[j])] = dist(vecs[names[i]], vecs[names[j]])

    def cdist(c1, c2):  # average linkage
        ds = [pair[(x, y)] if (x, y) in pair else pair[(y, x)] for x in c1 for y in c2]
        return sum(ds) / len(ds)

    while len(clusters) > 1:
        best = None
        for i in range(len(clusters)):
            for j in range(i + 1, len(clusters)):
                d = cdist(clusters[i], clusters[j])
                if best is None or d < best[0]:
                    best = (d, i, j)
        if best[0] > threshold:
            break
        _, i, j = best
        clusters[i] = clusters[i] + clusters[j]
        del clusters[j]
    return clusters


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("db")
    ap.add_argument("--threshold", type=float, default=0.25,
                    help="average-linkage distance below which avatars join a family (default 0.25)")
    ap.add_argument("--highlight", help="mark this avatar and list its family")
    ap.add_argument("--mark", nargs="*", default=[],
                    help="also mark these raw names (mini-stack is auto-detected from data/identity.json)")
    ap.add_argument("--identity", help="path to identity.json (default: data/identity.json) — resolves canonical names + creator/mini-stack")
    args = ap.parse_args()

    ids = Identity(args.identity) if Identity else None

    meta, vecs = load_vectors(args.db)
    clusters = cluster(vecs, args.threshold)
    clusters.sort(key=lambda c: min(meta[n][0] for n in c))  # by lowest head-count

    print(f"families (avg-linkage shape distance < {args.threshold}; head-count Neck/Head"
          + ("; names canonicalized via identity.json)" if ids else ")") + ":\n")
    for k, c in enumerate(clusters, 1):
        c = sorted(c, key=lambda n: meta[n][0])
        hns = [meta[n][0] for n in c]
        creators = sorted({ids.creator(n) for n in c if ids and ids.creator(n)}) if ids else []
        cre = f"; creators={creators}" if creators else ""
        print(f"  family {k}  (head-count {min(hns):.2f}-{max(hns):.2f}, n={len(c)}{cre}):")
        for n in c:
            hn, hh, ht = meta[n]
            name = ids.canonical(n) if ids else n
            extra = ""
            if ids:
                if ids.is_mini_stack(n):
                    extra += " *mini-stack"
                if ids.creator(n):
                    extra += f" [{ids.creator(n)}]"
            elif n in args.mark:
                extra += " *mini-stack"
            if args.highlight and n == args.highlight:
                extra += "  <<< highlight"
            print(f"       {name:<22} hc {hn:.2f}/{hh:.2f}  h {ht:.3f}m{extra}")
        print()

    if args.highlight:
        fam = next((c for c in clusters if args.highlight in c), None)
        if fam:
            others = [n for n in fam if n != args.highlight]
            print(f"'{args.highlight}' family-mates: {others or '(alone at this threshold)'}")


if __name__ == "__main__":
    main()
