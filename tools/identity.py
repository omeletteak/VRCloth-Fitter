#!/usr/bin/env python3
"""同一性シソーラスの解決 (data/identity.json). 採寸で出た messy なプレファブ名を
canonical なアバターへ解決し、creator / mini-stack 等の関係を引く。名前と関係のみ＝
No Cache (形状なし)。stdlib の json だけで動く (依存なし)。

    from identity import Identity
    ids = Identity()                       # data/identity.json を読む
    ids.canonical("MANUKA_lilToon_base_decimated")  -> "Manuka"
    ids.creator("BB_007_unkt_pb BASE")              -> "YOYOGI MORI"
    ids.info(name) -> dict | None
"""
import json
import os

_DEFAULT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "data", "identity.json")


class Identity:
    def __init__(self, path=None):
        self._by_key = {}   # lowercased canonical/alias -> avatar record
        path = path or _DEFAULT
        try:
            data = json.load(open(path, encoding="utf-8"))
        except FileNotFoundError:
            data = {"avatars": []}
        for a in data.get("avatars", []):
            self._by_key[a["canonical"].lower()] = a
            for alias in a.get("aliases", []):
                self._by_key.setdefault(alias.lower(), a)

    def info(self, name):
        """The avatar record for a measured/canonical name, or None if unknown."""
        return self._by_key.get((name or "").lower())

    def canonical(self, name):
        """Canonical avatar name; falls back to the input when unknown."""
        a = self.info(name)
        return a["canonical"] if a else name

    def creator(self, name):
        a = self.info(name)
        return (a.get("creator") or "") if a else ""

    def is_mini_stack(self, name):
        a = self.info(name)
        return bool(a and a.get("miniStack"))


if __name__ == "__main__":
    import sys
    ids = Identity()
    for n in sys.argv[1:]:
        a = ids.info(n)
        print(f"{n!r:40} -> {ids.canonical(n)!r}"
              + (f"  creator={ids.creator(n)!r}  miniStack={ids.is_mini_stack(n)}" if a else "  (unknown)"))
