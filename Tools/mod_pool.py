#!/usr/bin/env python3
"""The player's mod pool: a compact catalogue of what the player's installed mods add, for "draw from the full mod pool".

Reads what Collect-ModDefs.ps1 collects on the player's computer (the game's Data folder, each mod's Defs and Patches in
the folders RimWorld 1.7 loads, mods.txt and ModsConfig.xml), resolves def inheritance (ParentName, Abstract, lists that
add to the parent's unless Inherit="False"), and writes small tab-separated tables, one row per def, each with the
package id of the mod that adds it, whether that mod is active in the player's mod list (ModsConfig.xml) and, given a
save, whether it is in that save's mod list. Only mods that support RimWorld 1.6 or 1.7 (their About.xml's
supportedVersions) are catalogued, active or not; the rest are listed in mods.tsv as such and their defs left out:

  mods.tsv, apparel.tsv, weapons.tsv, items.tsv, races.tsv (humanlike races and animals), xenotypes.tsv, genes.tsv,
  traits.tsv, abilities.tsv, pawnkinds.tsv, factions.tsv, memes.tsv

Patches are not applied (a patch that adds a tag to another mod's apparel isn't seen); defs are as their files write
them. Build anything for the player from the active rows ("active" = yes): the catalogue holds every mod subscribed, many of
them not in the game. Run:
  python3 Tools/mod_pool.py <folder with Data, Mods, mods.txt, ModsConfig.xml> docs/player-mods [save.rws]
"""
import csv
import os
import re
import sys
import xml.etree.ElementTree as ET

DLC = {"Core": "ludeon.rimworld", "Royalty": "ludeon.rimworld.royalty", "Ideology": "ludeon.rimworld.ideology",
       "Biotech": "ludeon.rimworld.biotech", "Anomaly": "ludeon.rimworld.anomaly", "Odyssey": "ludeon.rimworld.odyssey"}


def read_mods(root):
    """folder -> (packageId, name); and the active package ids in load order."""
    folders = {}
    with open(os.path.join(root, "mods.txt"), encoding="utf-8-sig") as f:
        for row in csv.reader(f, delimiter="\t"):
            if not row or row[0] == "folder" or len(row) < 3:
                continue
            folders[row[0]] = (row[1].strip().lower(), row[2].strip())
    # mods.txt's id can be a dependency's (the collector took the first <packageId> in About.xml): the mod's own is the
    # top-level one, and so is its name.
    for folder in list(folders):
        about = parse(os.path.join(root, "Mods", folder, "About", "About.xml"))
        if about is None:
            continue
        pid = about.find("packageId")
        name = about.find("name")
        folders[folder] = ((pid.text or "").strip().lower() if pid is not None and pid.text else folders[folder][0],
                           (name.text or "").strip() if name is not None and name.text else folders[folder][1])
    active = []
    cfg = os.path.join(root, "ModsConfig.xml")
    if os.path.exists(cfg):
        try:
            t = ET.parse(cfg)
            am = t.getroot().find("activeMods")
            if am is not None:
                active = [li.text.strip().lower() for li in am.findall("li") if li.text]
        except ET.ParseError:
            pass
    return folders, active


def versions_of(root, folder):
    """The game versions a mod's About.xml says it supports."""
    about = os.path.join(root, "Mods", folder, "About", "About.xml")
    try:
        t = parse(about)
        sv = t.find("supportedVersions") if t is not None else None
        return [li.text.strip() for li in sv.findall("li") if li.text] if sv is not None else []
    except Exception:
        return []


def save_mods(path):
    """The package ids a save's meta lists (the mods it was played with)."""
    if not path or not os.path.exists(path):
        return None
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        head = f.read(200000)
    m = re.search(r"<modIds>(.*?)</modIds>", head, re.S)
    return set(x.strip().lower() for x in re.findall(r"<li>([^<]+)</li>", m.group(1))) if m else set()


SUPPORTED = ("1.6", "1.7")


def _child(base, name):
    """base/name, matching the name's case as Windows does (LoadFolders.xml may write "mods/odyssey" for "Mods/Odyssey")."""
    for part in [p for p in name.replace("\\", "/").split("/") if p]:
        p = os.path.join(base, part)
        if not os.path.exists(p) and os.path.isdir(base):
            p = next((os.path.join(base, x) for x in os.listdir(base) if x.lower() == part.lower()), p)
        base = p
    return base


def load_folders(moddir, active):
    """The folders of a mod that RimWorld 1.7 loads for this mod list: those its LoadFolders.xml names for 1.7 (else for
    the newest version before it), each with its IfModActive (any of), IfModActiveAll and IfModNotActive (none of) kept;
    else its root, Common and its newest version folder up to 1.7. A mod's folders for older versions are never read, and
    a mod folder kept for a mod the player hasn't enabled neither (their defs would otherwise stand for the mod's own)."""
    def on(ids):
        return [x.strip().lower() in active for x in (ids or "").split(",") if x.strip()]
    lf = parse(_child(moddir, "LoadFolders.xml")) if os.path.exists(_child(moddir, "LoadFolders.xml")) else None
    if lf is not None:
        best, best_v = None, (0, 0)
        for node in lf:
            m = re.match(r"^v(\d+)\.(\d+)$", node.tag if isinstance(node.tag, str) else "")
            if m and best_v <= (int(m.group(1)), int(m.group(2))) <= (1, 7):
                best, best_v = node, (int(m.group(1)), int(m.group(2)))
        if best is not None:
            out = []
            for li in best.findall("li"):
                a, al, no = li.attrib.get("IfModActive"), li.attrib.get("IfModActiveAll"), li.attrib.get("IfModNotActive")
                if (a and not any(on(a))) or (al and not all(on(al))) or (no and any(on(no))):
                    continue
                out.append((li.text or "").strip().strip("/\\"))
            if out:
                return out
    newest, newest_v = None, (0, 0)
    for d in os.listdir(moddir):
        m = re.match(r"^v?(\d+)\.(\d+)$", d)
        if m and os.path.isdir(os.path.join(moddir, d)) and newest_v <= (int(m.group(1)), int(m.group(2))) <= (1, 7):
            newest, newest_v = d, (int(m.group(1)), int(m.group(2)))
    return ["", "Common"] + ([newest] if newest else [])


def def_files(root, folders, skip=(), active=()):
    """(packageId, path) of every def file: the game's own, then each mod's Defs folders in the folders 1.7 loads, in the
    player's load order (the mods not in their list after it), so that a later def of the same name wins as in the game."""
    data = os.path.join(root, "Data")
    order = {p: i for i, p in enumerate(active)}
    if os.path.isdir(data):
        for d in sorted(os.listdir(data), key=lambda d: (list(DLC).index(d) if d in DLC else len(DLC), d)):
            pid = DLC.get(d, "ludeon.rimworld." + d.lower())
            for dp, _, fs in os.walk(os.path.join(data, d)):
                if "Defs" not in dp.replace("\\", "/").split("/"):
                    continue
                for f in sorted(fs):
                    if f.lower().endswith(".xml"):
                        yield pid, os.path.join(dp, f)
    mods = os.path.join(root, "Mods")
    if os.path.isdir(mods):
        for d in sorted(os.listdir(mods), key=lambda d: (order.get(folders.get(d, (d.lower(),))[0], len(order)), d)):
            if d in skip:
                continue
            pid = folders.get(d, (d.lower(), d))[0]
            moddir = os.path.join(mods, d)
            for folder in load_folders(moddir, set(active)):
                for dp, dirs, fs in os.walk(_child(_child(moddir, folder), "Defs")):
                    dirs.sort()
                    for f in sorted(fs):
                        if f.lower().endswith(".xml"):
                            yield pid, os.path.join(dp, f)


def parse(path):
    try:
        with open(path, "rb") as f:
            raw = f.read()
        return ET.fromstring(raw.decode("utf-8-sig", errors="replace").encode("utf-8"))
    except (ET.ParseError, ValueError, OSError):
        return None


def merge(parent, child):
    """A copy of parent with child's fields over it: lists add unless Inherit="False", other fields replace."""
    out = ET.Element(child.tag, dict(child.attrib))
    seen = {}
    for c in list(parent):
        cc = ET.fromstring(ET.tostring(c))
        out.append(cc)
        seen.setdefault(c.tag, []).append(cc)
    for c in list(child):
        inherit = c.attrib.get("Inherit", "True").lower() != "false"
        if c.tag in seen and inherit and len(c) and all(x.tag == "li" for x in c) and all(
                len(p) == 0 or all(x.tag == "li" for x in p) for p in seen[c.tag]):
            target = seen[c.tag][0]
            for x in list(c):
                target.append(ET.fromstring(ET.tostring(x)))
            continue
        if c.tag in seen and inherit and len(c) and not all(x.tag == "li" for x in c):
            target = seen[c.tag][0]
            merged = merge(target, c)
            out.remove(target)
            out.append(merged)
            seen[c.tag] = [merged]
            continue
        for t in seen.get(c.tag, []):
            if t in list(out):
                out.remove(t)
        cc = ET.fromstring(ET.tostring(c))
        out.append(cc)
        seen[c.tag] = [cc]
    return out


def text(e, path, default=""):
    x = e.find(path)
    return (x.text or "").strip() if x is not None and x.text else default


def lis(e, path):
    x = e.find(path)
    if x is None:
        return []
    return [(li.text or "").strip() for li in x.findall("li") if li.text and li.text.strip()]


def main(root, out, save=None):
    folders, active = read_mods(root)
    active_set = set(active)
    in_save = save_mods(save)
    versions = {d: versions_of(root, d) for d in folders}
    old_only = {d for d, v in versions.items() if v and not any(x in SUPPORTED for x in v)}
    named = {}   # (tag, Name) -> [(pid, element)], for inheritance
    defs = []    # (pid, element)
    owner = {}   # id(element) -> pid
    for pid, path in def_files(root, folders, old_only, active):
        r = parse(path)
        if r is None:
            continue
        for e in list(r):
            if not isinstance(e.tag, str):
                continue
            owner[id(e)] = pid
            n = e.attrib.get("Name")
            if n:
                named.setdefault((e.tag, n), []).append((pid, e))
                named.setdefault(("*", n), []).append((pid, e))
            if e.attrib.get("Abstract", "").lower() == "true":
                continue
            if e.find("defName") is not None:
                defs.append((pid, e))

    def parent_of(e, p):
        """The parent the game takes: its own mod's of that name, else the last of a mod in the list, else the last."""
        for key in ((e.tag, p), ("*", p)):
            cands = named.get(key)
            if cands:
                mine = [x for q, x in cands if q == owner.get(id(e))]
                on = [x for q, x in cands if q in active_set or q.startswith("ludeon.")]
                return (mine or on or [x for _, x in cands])[-1]
        return None

    cache = {}

    def resolved(e, depth=0):
        key = id(e)
        if key in cache:
            return cache[key]
        p = e.attrib.get("ParentName")
        if p and depth < 30:
            parent = parent_of(e, p)
            r = merge(resolved(parent, depth + 1), e) if parent is not None else e
        else:
            r = e
        cache[key] = r
        return r

    os.makedirs(out, exist_ok=True)
    tables = {k: [] for k in ["apparel", "weapons", "items", "races", "xenotypes", "genes", "traits", "abilities",
                              "pawnkinds", "factions", "memes"]}
    class Last(dict):
        """The last def of a name wins, as in the game; one of a mod not in the player's list never replaces one that is."""
        def __setitem__(self, key, row):
            if key not in self or self[key][2] != "yes" or row[2] == "yes":
                super().__setitem__(key, row)

    last = Last()
    for pid, e in defs:
        d = resolved(e)
        dn = text(d, "defName")
        on = "yes" if pid in active_set or pid.startswith("ludeon.") else "no"
        tag = e.tag.split(".")[-1]
        label = text(d, "label")
        if tag == "ThingDef":
            tech = text(d, "techLevel")
            value = text(d, "statBases/MarketValue")
            cats = ",".join(lis(d, "thingCategories"))
            trade = ",".join(lis(d, "tradeTags"))
            if d.find("apparel") is not None:
                row = [dn, pid, on, label, tech, ",".join(lis(d, "apparel/tags")), ",".join(lis(d, "apparel/layers")),
                       ",".join(lis(d, "apparel/bodyPartGroups")), ",".join(lis(d, "stuffCategories")), value, trade,
                       text(d, "generateCommonality")]
                last[("apparel", dn)] = row
            elif d.find("weaponTags") is not None or "Weapon" in cats:
                kind = "ranged" if d.find("verbs") is not None and any(
                    text(v, "verbClass").endswith("Shoot") or "Shoot" in text(v, "verbClass") or "Launch" in text(v, "verbClass")
                    for v in d.find("verbs").findall("li")) else "melee"
                row = [dn, pid, on, label, tech, ",".join(lis(d, "weaponTags")), kind, ",".join(lis(d, "stuffCategories")),
                       value, trade, text(d, "generateCommonality")]
                last[("weapons", dn)] = row
            elif d.find("race") is not None:
                race = d.find("race")
                intel = text(race, "intelligence")
                humanlike = intel == "Humanlike" or "Alien" in (e.attrib.get("Class", "") or "")
                row = [dn, pid, on, label, "humanlike" if humanlike else ("mech" if text(race, "intelligence") == "" and
                       "Mech" in text(race, "fleshType") else "animal"), text(race, "baseBodySize"), text(race, "foodType"),
                       text(race, "predator"), text(race, "herdAnimal"), text(race, "maxPreyBodySize")]
                last[("races", dn)] = row
            elif text(d, "category") == "Item" or cats:
                row = [dn, pid, on, label, tech, cats, trade, value]
                last[("items", dn)] = row
        elif tag == "XenotypeDef":
            last[("xenotypes", dn)] = [dn, pid, on, label, text(d, "inheritable"), str(len(lis(d, "genes"))),
                                       re.sub(r"\s+", " ", text(d, "description"))[:160]]
        elif tag == "GeneDef":
            last[("genes", dn)] = [dn, pid, on, label, text(d, "displayCategory"), text(d, "biostatMet"),
                                   text(d, "biostatCpx")]
        elif tag == "TraitDef":
            deg = d.find("degreeDatas")
            labels = [text(li, "label") for li in deg.findall("li")] if deg is not None else []
            last[("traits", dn)] = [dn, pid, on, " / ".join(x for x in labels if x)]
        elif "Ability" in tag:
            last[("abilities", dn)] = [dn, pid, on, tag, label, text(d, "category") or text(d, "abilityClass")]
        elif tag == "PawnKindDef":
            last[("pawnkinds", dn)] = [dn, pid, on, label, text(d, "race"), text(d, "defaultFactionDef") or text(d, "defaultFactionType"),
                                       text(d, "combatPower"), ",".join(lis(d, "apparelTags")), ",".join(lis(d, "weaponTags"))]
        elif tag == "FactionDef":
            last[("factions", dn)] = [dn, pid, on, label, text(d, "techLevel"), text(d, "hidden"), text(d, "permanentEnemy"),
                                      text(d, "basicMemberKind")]
        elif tag == "MemeDef":
            last[("memes", dn)] = [dn, pid, on, label, text(d, "category"), ",".join(lis(d, "exclusionTags"))]
    for (k, _), row in last.items():
        tables[k].append(row)
    heads = {
        "apparel": ["defName", "mod", "active", "label", "techLevel", "tags", "layers", "bodyPartGroups", "stuff", "marketValue", "tradeTags",
                    "commonality"],
        "weapons": ["defName", "mod", "active", "label", "techLevel", "weaponTags", "kind", "stuff", "marketValue", "tradeTags",
                    "commonality"],
        "items": ["defName", "mod", "active", "label", "techLevel", "thingCategories", "tradeTags", "marketValue"],
        "races": ["defName", "mod", "active", "label", "kind", "bodySize", "foodType", "predator", "herdAnimal", "maxPreyBodySize"],
        "xenotypes": ["defName", "mod", "active", "label", "inheritable", "genes", "description"],
        "genes": ["defName", "mod", "active", "label", "displayCategory", "biostatMet", "biostatCpx"],
        "traits": ["defName", "mod", "active", "degrees"],
        "abilities": ["defName", "mod", "active", "defType", "label", "category"],
        "pawnkinds": ["defName", "mod", "active", "label", "race", "faction", "combatPower", "apparelTags", "weaponTags"],
        "factions": ["defName", "mod", "active", "label", "techLevel", "hidden", "permanentEnemy", "basicMemberKind"],
        "memes": ["defName", "mod", "active", "label", "category", "exclusionTags"],
    }
    for k, rows in tables.items():
        rows.sort(key=lambda r: (r[1], r[0]))
        with open(os.path.join(out, k + ".tsv"), "w", encoding="utf-8", newline="") as f:
            w = csv.writer(f, delimiter="\t", lineterminator="\n")
            w.writerow(heads[k])
            w.writerows(rows)
    with open(os.path.join(out, "mods.tsv"), "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, delimiter="\t", lineterminator="\n")
        w.writerow(["packageId", "name", "folder", "active", "loadOrder", "inSave", "versions", "catalogued"])
        order = {p: i for i, p in enumerate(active)}
        for folder, (pid, name) in sorted(folders.items(), key=lambda kv: (order.get(kv[1][0], 9999), kv[1][1].lower())):
            w.writerow([pid, name, folder, "yes" if pid in active_set else "no", order.get(pid, ""),
                        "" if in_save is None else ("yes" if pid in in_save else "no"), ",".join(versions.get(folder, [])),
                        "no (not for 1.6 or 1.7)" if folder in old_only else "yes"])
    print(f"mods: {len(folders)} subscribed, {sum(1 for f in folders if folders[f][0] in active_set)} active, "
          f"{len(old_only)} left out as not for 1.6 or 1.7")
    for k, rows in tables.items():
        print(f"{k}: {len(rows)} ({sum(1 for r in rows if r[2] == 'yes')} active)")


if __name__ == "__main__":
    if len(sys.argv) not in (3, 4):
        print(__doc__)
        sys.exit(2)
    main(sys.argv[1], sys.argv[2], sys.argv[3] if len(sys.argv) == 4 else None)
