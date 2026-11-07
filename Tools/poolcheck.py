#!/usr/bin/env python3
"""Rimfolk's references to other mods' defs, checked against the player's real mod pool (docs/player-mods/defs.tsv.gz).

Looks at:
- every element of Rimfolk's Defs with a MayRequire (or MayRequireAnyOf) attribute: the def it names (the element's
  tag, as in a xenotype chance <Hussar MayRequire=...>, or its text, as in <li MayRequire=...>Name</li>);
- every string literal in Rimfolk's C# passed to GetNamedSilentFail, GetNamed or Named, or listed in a string array
  next to one (by-name references);
and sorts each name into:
  missing      no def of that name in the game, the DLCs or any mod the player has subscribed to (a typo, an old name,
               or a mod the player doesn't have at all);
  inactive     only in mods the player has subscribed to but not enabled (harmless, soft, but does nothing for them);
  wrong-mod    the MayRequire names package ids none of which adds the def (the soft condition guards the wrong mod);
  ok           in the game, a DLC or an active mod, under the package named.
Run: python3 Tools/poolcheck.py [--all] (prints a summary and the problems, exits 1 if a def reference is missing or
guarded by the wrong package; --all also lists the inactive-only ones and the code names it couldn't place).
"""
import csv
import gzip
import os
import re
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SELF = "silvanalig.colonycouncil"
# Element names that are fields, not defs, when they carry a MayRequire (the def is then in the text).
FIELD_HINT = re.compile(r"^(li|def|defName|thing|thingDef|kind|kindDef|race|xenotype|gene|trait|ability|apparel|weapon|"
                        r"item|meme|precept|hediff|thought|faction|pawnKind|animal|plant|research|recipe|stuff|"
                        r"apparelDef|weaponDef|requiredTag|tag|label|[a-z].*)$")


def load_pool():
    pool = {}
    path = os.path.join(ROOT, "docs", "player-mods", "defs.tsv.gz")
    with gzip.open(path, "rt", encoding="utf-8") as f:
        r = csv.reader(f, delimiter="\t")
        next(r)
        for t, dn, mod, active in r:
            if mod == SELF:
                continue
            pool.setdefault(dn, []).append((t, mod, active == "yes"))
    return pool


def load_tags():
    """Apparel and weapon tags the game, the DLCs and the player's mods give their gear: tag -> [(mod, active)]."""
    tags = {}
    for table, col in (("apparel.tsv", "tags"), ("weapons.tsv", "weaponTags")):
        with open(os.path.join(ROOT, "docs", "player-mods", table), encoding="utf-8") as f:
            r = csv.DictReader(f, delimiter="\t")
            for row in r:
                for t in (row[col] or "").split(","):
                    if t:
                        tags.setdefault(t, set()).add((row["mod"], row["active"] == "yes"))
    return tags


def own_defs():
    """Rimfolk's own defNames, as the repository has them now."""
    names = set()
    for dp, _, fs in os.walk(os.path.join(ROOT, "Defs")):
        for f in fs:
            if f.endswith(".xml"):
                s = open(os.path.join(dp, f), encoding="utf-8", errors="replace").read()
                names.update(re.findall(r"<defName>([^<]+)</defName>", s))
    return names


TAG_LISTS = {"apparelTags", "weaponTags", "alternateTagChoices", "tags", "requiredTag", "tag", "apparelDisallowTags",
             "weaponDisallowTags"}


def own_tags():
    """Tags Rimfolk's own patches give other mods' gear (Patches/: Rimfolk_Cape, Rimfolk_Finery...)."""
    tags = set()
    for dp, _, fs in os.walk(os.path.join(ROOT, "Patches")):
        for f in fs:
            if f.endswith(".xml"):
                s = open(os.path.join(dp, f), encoding="utf-8", errors="replace").read()
                tags.update(re.findall(r"<li>(Rimfolk_[A-Za-z0-9_]+)</li>", s))
    return tags


def xml_refs():
    """(file, name, packages, kind) for every MayRequire'd reference in Rimfolk's Defs; kind is 'tag' or 'def'."""
    out = []
    for dp, _, fs in os.walk(os.path.join(ROOT, "Defs")):
        for f in fs:
            if not f.endswith(".xml"):
                continue
            path = os.path.join(dp, f)
            try:
                root = ET.parse(path).getroot()
            except ET.ParseError:
                continue
            parent = {c: p for p in root.iter() for c in p}
            for e in root.iter():
                req = e.attrib.get("MayRequire") or e.attrib.get("MayRequireAnyOf")
                if not req:
                    continue
                if parent.get(e) is root:
                    continue  # a whole def of Rimfolk's that loads only with that mod
                pkgs = [p.strip().lower() for p in req.split(",") if p.strip()]
                name = None
                kind = "def"
                par = parent.get(e)
                if par is not None and (par.tag in TAG_LISTS or (parent.get(par) is not None and parent[par].tag in TAG_LISTS)):
                    kind = "tag"
                if not FIELD_HINT.match(e.tag) and re.match(r"^[A-Z][A-Za-z0-9_]*$", e.tag):
                    name = e.tag
                elif e.text and re.match(r"^[A-Za-z][A-Za-z0-9_\-]*$", e.text.strip()) and len(e) == 0:
                    name = e.text.strip()
                if name:
                    out.append((os.path.relpath(path, ROOT), name, pkgs, kind))
    return out


CALL = re.compile(r'(?:GetNamedSilentFail|GetNamed|NamedSilentFail|DefNamed|[A-Za-z]*Def\.Named)\s*\(\s*"([A-Za-z][A-Za-z0-9_]*)"')
ARRAY = re.compile(r'new\s*(?:string)?\s*\[\s*\]\s*\{([^}]*)\}', re.S)


def code_refs():
    """(file, name) for every by-name def reference in Rimfolk's C#."""
    out = []
    for dp, _, fs in os.walk(os.path.join(ROOT, "Source", "ColonyCouncil")):
        for f in fs:
            if not f.endswith(".cs"):
                continue
            path = os.path.join(dp, f)
            s = open(path, encoding="utf-8", errors="replace").read()
            for m in CALL.finditer(s):
                out.append((os.path.relpath(path, ROOT), m.group(1)))
            for m in ARRAY.finditer(s):
                body = m.group(1)
                # Only arrays that sit near a by-name lookup are def lists.
                start = max(0, m.start() - 600)
                if not re.search(r"GetNamedSilentFail|GetNamed\b|NamedSilentFail|Def\.Named", s[start:m.end() + 600]):
                    continue
                for lit in re.findall(r'"([A-Za-z][A-Za-z0-9_]*)"', body):
                    out.append((os.path.relpath(path, ROOT), lit))
    return out


def main():
    show_all = "--all" in sys.argv
    pool = load_pool()
    tags = load_tags()
    for t in own_tags():
        tags.setdefault(t, set()).add((SELF, True))
    own = own_defs()
    counts = {"ok": 0, "inactive": 0, "missing": 0, "wrong-mod": 0, "vanilla": 0}
    problems = []
    for path, name, pkgs, kind in xml_refs():
        if kind == "tag":
            th = tags.get(name)
            if not th:
                counts["missing"] += 1
                problems.append(("missing", path, name, "tag; " + ",".join(pkgs)))
            elif not any(a for _, a in th):
                counts["inactive"] += 1
                if show_all:
                    problems.append(("inactive", path, name, "tag; " + ",".join(sorted(m for m, _ in th))))
            else:
                counts["ok"] += 1
            continue
        if name in own:
            counts["ok"] += 1
            continue
        hits = pool.get(name)
        if not hits:
            counts["missing"] += 1
            problems.append(("missing", path, name, ",".join(pkgs)))
            continue
        mods = {m for _, m, _ in hits}
        if any(m.startswith("ludeon.") for m in mods) and all(p.startswith("ludeon.") for p in pkgs):
            counts["vanilla"] += 1
            continue
        if not mods & set(pkgs) and not any(p.startswith("ludeon.") for p in pkgs):
            counts["wrong-mod"] += 1
            problems.append(("wrong-mod", path, name, ",".join(pkgs) + " but defined by " + ",".join(sorted(mods))))
            continue
        if not any(a for _, _, a in hits):
            counts["inactive"] += 1
            if show_all:
                problems.append(("inactive", path, name, ",".join(sorted(mods))))
            continue
        counts["ok"] += 1
    code_missing = []
    code_inactive = 0
    code_ok = 0
    seen = set()
    for path, name in code_refs():
        if (path, name) in seen:
            continue
        seen.add((path, name))
        if name in own:
            code_ok += 1
            continue
        hits = pool.get(name)
        if not hits:
            code_missing.append((path, name))
        elif not any(a for _, _, a in hits):
            code_inactive += 1
        else:
            code_ok += 1
    print("Defs, references with MayRequire:", ", ".join(f"{k} {v}" for k, v in counts.items()))
    print(f"Code, by-name references: ok {code_ok}, inactive-only {code_inactive}, not found {len(code_missing)}")
    for kind, path, name, extra in sorted(problems):
        print(f"  {kind:9s} {name:40s} {path}  ({extra})")
    if code_missing and show_all:
        print("Code names found in no def of the game, the DLCs or the player's mods (most are Rimfolk's own keys, word lists or")
        print("mods the player doesn't have; a lookup by name fails softly):")
        for path, name in sorted(code_missing):
            print(f"  {name:40s} {path}")
    bad = counts["missing"] + counts["wrong-mod"]
    print("OK: every reference in the defs names a def of the game, a DLC or the player's mods, under the right package"
          if bad == 0 else f"{bad} reference(s) in the defs to fix")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
