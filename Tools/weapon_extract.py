#!/usr/bin/env python3
"""Every weapon of the player's active mods, resolved to the numbers the weapon rating reads (Tools/WeaponReview).

Reads the player's def collection the way Tools/mod_pool.py does (the folders RimWorld 1.7 loads per LoadFolders.xml,
IfModActive, ParentName inheritance, which mods are active, load order: its Pool class) and writes one JSON file with:

  weapons: every ThingDef of an active mod that mod_pool's weapons.tsv lists (weaponTags, or a Weapon* category), with
    its own numbers as the game resolves them: the verbs (warm-up, burst, cooldown, range, accuracy, forced miss) and
    each verb's projectile (damage and armour piercing by the game's rules: damageAmountBase or the damage's default,
    the piercing set, the damage's default or 1.5% of the damage; stopping power; explosion, fuse, extra damages, what
    it leaves behind), the melee tools with each capacity's damage (through the ManeuverDefs), the stat bases, what it
    changes on its wielder (equippedStatOffsets), its comps and mod extensions, the abilities it grants (AbilityDefs:
    their verb, projectile and effect comps, cooldown, charges), the default stuff it is rated in (steel where it takes
    metal) with that stuff's factors, and why it is left out of the main review if it is (a turret's gun, a body's or a
    spell's weapon that vanishes when dropped, not equippable, no attack);
  damageDefs: every damage those name, with its armour category, defaults, harm, stun and extra hediffs;
  patches: the patches of the active mods that touch a listed weapon's or projectile's combat numbers (the review
    doesn't apply patches, as the pool doesn't; it names them).

Run:
  python3 Tools/weapon_extract.py <folder with Data, Mods, mods.txt, ModsConfig.xml> docs/player-mods/weapon_numbers.json.gz
"""
import gzip
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mod_pool  # noqa: E402
from mod_pool import text, lis, comp_names  # noqa: E402


def num(s, default=None):
    try:
        return float(s)
    except (TypeError, ValueError):
        return default


def boolean(s, default=False):
    if s is None or s == "":
        return default
    return s.strip().lower() == "true"


def children_dict(e):
    """A list of <Stat>value</Stat> children as a dict of numbers (statBases, equippedStatOffsets, statFactors)."""
    out = {}
    if e is None:
        return out
    for c in e:
        if isinstance(c.tag, str):
            v = num((c.text or "").strip())
            if v is not None:
                out[c.tag] = v
    return out


LOADED = set()


def loaded(li):
    """Whether a list item loads for the player's mod list (mod_pool.requirement_met: MayRequire all of, MayRequireAnyOf
    any of). The pool's resolved defs are already pruned this way; this keeps a raw read (an extension) the same."""
    return mod_pool.requirement_met(li, LOADED)


def items(e, path):
    x = e.find(path) if e is not None else None
    return [li for li in (x.findall("li") if x is not None else []) if loaded(li)]


def leaves(li):
    """A comp's or extension's simple fields, as written (for the review to read what a mod's own code is told)."""
    out = {}
    for c in li:
        if isinstance(c.tag, str) and len(c) == 0 and c.text and c.text.strip():
            out[c.tag] = c.text.strip()
    return out


def cls_of(li):
    return (li.attrib.get("Class", "") or "").split(".")[-1] or text(li, "compClass").split(".")[-1]


class Collection:
    def __init__(self, root):
        self.pool = mod_pool.Pool(root)
        LOADED.update(self.pool.loaded)
        self.root = root
        self.names = {}
        for folder, (pid, name) in self.pool.folders.items():
            self.names[pid] = name
        self.by = {}  # (tag, defName) -> (pid, element), the game's: an active mod's last def wins
        for pid, e in self.pool.defs:
            tag = e.tag.split(".")[-1]
            dn = text(e, "defName")
            on = self.pool.on(pid)
            key = (tag, dn)
            if key in self.by and self.pool.on(self.by[key][0]) and not on:
                continue
            self.by[key] = (pid, e)
        self.thinglike = {}
        for (tag, dn), (pid, e) in self.by.items():
            if tag != "ThingDef" and tag.endswith("Def") and (e.find("thingClass") is not None or e.find("projectile") is not None
                                                             or (e.attrib.get("ParentName") or "").startswith("Base")):
                self.thinglike.setdefault(dn, (pid, e))
        self._res = {}

    def get(self, tag, dn):
        """The resolved def of a type and name, or None. A ThingDef may be written as a mod's subclass of it (VEF's
        ExpandableProjectileDef, a framework's own projectile def): those count as ThingDefs of that name."""
        if not dn:
            return None
        hit = self.by.get((tag, dn))
        if hit is None and tag == "ThingDef":
            hit = self.thinglike.get(dn)
        if hit is None:
            return None
        key = id(hit[1])
        if key not in self._res:
            self._res[key] = self.pool.resolved(hit[1])
        return self._res[key]

    def mod_of(self, tag, dn):
        hit = self.by.get((tag, dn)) or (self.thinglike.get(dn) if tag == "ThingDef" else None)
        return hit[0] if hit else ""

    def mod_name(self, pid):
        if pid.startswith("ludeon.rimworld"):
            return {"ludeon.rimworld": "Core", "ludeon.rimworld.royalty": "Royalty", "ludeon.rimworld.ideology": "Ideology",
                    "ludeon.rimworld.biotech": "Biotech", "ludeon.rimworld.anomaly": "Anomaly",
                    "ludeon.rimworld.odyssey": "Odyssey"}.get(pid, pid)
        return self.names.get(pid, pid)


def damage_info(col, name, cache):
    """A damage def's numbers, once each."""
    if not name or name in cache:
        return
    d = col.get("DamageDef", name)
    if d is None:
        cache[name] = {"found": False}
        return
    adds = []
    ah = d.find("additionalHediffs")
    for li in (ah.findall("li") if ah is not None else []):
        adds.append({"hediff": text(li, "hediff"), "severityPerDamage": num(text(li, "severityPerDamageDealt"), 0.0),
                     "severity": num(text(li, "severity"), 0.0)})
    cache[name] = {
        "found": True, "label": text(d, "label"), "mod": col.mod_of("DamageDef", name),
        "workerClass": text(d, "workerClass"), "armorCategory": text(d, "armorCategory"),
        "defaultDamage": num(text(d, "defaultDamage"), -1), "defaultArmorPenetration": num(text(d, "defaultArmorPenetration"), -1),
        "defaultStoppingPower": num(text(d, "defaultStoppingPower"), 0), "harmsHealth": boolean(text(d, "harmsHealth"), True),
        "causeStun": boolean(text(d, "causeStun")), "isExplosive": boolean(text(d, "isExplosive")),
        "hediff": text(d, "hediff"), "additionalHediffs": adds, "stunAdaptationTicks": num(text(d, "stunAdaptationTicks"), 0),
        "externalViolence": boolean(text(d, "externalViolence")), "execution": boolean(text(d, "execution")),
        "constantStunDurationTicks": num(text(d, "constantStunDurationTicks")),
    }


def projectile_info(col, name, dmg_cache):
    """A projectile's numbers by the game's own rules (ProjectileProperties.GetDamageAmount and GetArmorPenetration)."""
    p = col.get("ThingDef", name)
    if p is None:
        return {"defName": name, "found": False}
    pr = p.find("projectile")
    if pr is None:
        return {"defName": name, "found": True, "thingClass": text(p, "thingClass"), "noProjectile": True}
    dd = text(pr, "damageDef")
    damage_info(col, dd, dmg_cache)
    di = dmg_cache.get(dd, {})
    base = num(text(pr, "damageAmountBase"))
    apb = num(text(pr, "armorPenetrationBase"))
    damage = base if base is not None else di.get("defaultDamage", -1)
    if not di.get("armorCategory"):
        ap = 0.0
    else:
        ap = apb if (base is not None or (apb is not None and apb >= 0)) else di.get("defaultArmorPenetration", -1)
        if ap is None or ap < 0:
            ap = max(damage, 0) * 0.015
    sp = num(text(pr, "stoppingPower"))
    if sp is None:
        sp = 0.5
    if sp == 0:
        sp = di.get("defaultStoppingPower", 0) or 0
    extras = []
    for li in items(pr, "extraDamages"):
        en = text(li, "def")
        damage_info(col, en, dmg_cache)
        extras.append({"def": en, "amount": num(text(li, "amount"), 0), "armorPenetration": num(text(li, "armorPenetration"), -1),
                       "chance": num(text(li, "chance"), 1)})
    return {
        "defName": name, "found": True, "mod": col.mod_of("ThingDef", name), "thingClass": text(p, "thingClass"),
        "damageDef": dd, "damage": damage, "armorPenetration": ap, "stoppingPower": sp,
        "explosionRadius": num(text(pr, "explosionRadius"), 0), "explosionDamageFalloff": boolean(text(pr, "explosionDamageFalloff")),
        "explosionDelay": num(text(pr, "explosionDelay"), 0), "explosionChanceToStartFire": num(text(pr, "explosionChanceToStartFire"), 0),
        "preExplosionSpawnThingDef": text(pr, "preExplosionSpawnThingDef"), "postExplosionSpawnThingDef": text(pr, "postExplosionSpawnThingDef"),
        "postExplosionGasType": text(pr, "postExplosionGasType"), "flyOverhead": boolean(text(pr, "flyOverhead")),
        "speed": num(text(pr, "speed"), 5), "extraDamages": extras, "numExtraHitCells": num(text(pr, "numExtraHitCells"), 0),
        "spawnsThingDef": text(pr, "spawnsThingDef"), "ai_IsIncendiary": boolean(text(pr, "ai_IsIncendiary")),
        "shotCount": num(text(pr, "shotCount"), 1), "projectileClass": (pr.attrib.get("Class", "") or "").split(".")[-1],
        "lifeTimeDuration": num(text(p, "lifeTimeDuration")), "tickDamageRate": num(text(p, "tickDamageRate")),
        "dealsDamageOnce": boolean(text(p, "dealsDamageOnce")), "minDistanceToAffect": num(text(p, "minDistanceToAffect")),
        "defTag": col.by.get(("ThingDef", name), col.thinglike.get(name, ("", None)))[1].tag.split(".")[-1]
        if (("ThingDef", name) in col.by or name in col.thinglike) else "",
        "extensions": [dict(leaves(li), **{"class": (li.attrib.get("Class", "") or "").split(".")[-1]}) for li in items(p, "modExtensions")],
    }


def verb_info(col, v, dmg_cache):
    proj = text(v, "defaultProjectile")
    out = {
        "verbClass": text(v, "verbClass"), "label": text(v, "label"), "hasStandardCommand": boolean(text(v, "hasStandardCommand")),
        "range": num(text(v, "range"), 1.42), "minRange": num(text(v, "minRange"), 0), "warmupTime": num(text(v, "warmupTime"), 0),
        "burstShotCount": int(num(text(v, "burstShotCount"), 1)), "ticksBetweenBurstShots": int(num(text(v, "ticksBetweenBurstShots"), 15)),
        "forcedMissRadius": num(text(v, "forcedMissRadius"), 0), "canGoWild": boolean(text(v, "canGoWild"), True),
        "beamDamageDef": text(v, "beamDamageDef"), "beamTotalDamage": num(text(v, "beamTotalDamage"), 0),
        "beamChanceToAttachFire": num(text(v, "beamChanceToAttachFire"), 0), "consumeFuelPerShot": num(text(v, "consumeFuelPerShot"), 0),
        "beamWidth": num(text(v, "beamWidth"), 1), "beamMaxDeviation": num(text(v, "beamMaxDeviation"), 0),
        "beamHitsNeighborCells": boolean(text(v, "beamHitsNeighborCells")),
        "onlyManualCast": boolean(text(v, "onlyManualCast")), "violent": boolean(text(v, "violent"), True),
        "defaultCooldownTime": num(text(v, "defaultCooldownTime"), 0), "isPrimary": boolean(text(v, "isPrimary"), True),
        "projectile": projectile_info(col, proj, dmg_cache) if proj else None,
    }
    damage_info(col, out["beamDamageDef"], dmg_cache)
    return out


def maneuvers(col):
    """Each melee capacity's verb class and damage (ManeuverDefs: requiredCapacity, verb)."""
    out = {}
    for (tag, dn), (pid, e) in col.by.items():
        if tag != "ManeuverDef" or not col.pool.on(pid):
            continue
        m = col.get(tag, dn)
        cap = text(m, "requiredCapacity")
        if cap and cap not in out:
            out[cap] = {"maneuver": dn, "verbClass": text(m, "verb/verbClass"), "damageDef": text(m, "verb/meleeDamageDef")}
    return out


def tool_info(col, t, mans, dmg_cache):
    caps = []
    for c in lis(t, "capacities"):
        m = mans.get(c, {})
        damage_info(col, m.get("damageDef"), dmg_cache)
        caps.append({"capacity": c, "damageDef": m.get("damageDef", ""), "verbClass": m.get("verbClass", ""), "known": bool(m)})
    extras = []
    for path in ("extraMeleeDamages", "surpriseAttack/extraMeleeDamages"):
        for li in items(t, path):
            en = text(li, "def")
            damage_info(col, en, dmg_cache)
            extras.append({"def": en, "amount": num(text(li, "amount"), 0), "armorPenetration": num(text(li, "armorPenetration"), -1),
                           "chance": num(text(li, "chance"), 1), "surprise": path.startswith("surprise")})
    return {"label": text(t, "label"), "capacities": caps, "power": num(text(t, "power"), 0), "cooldownTime": num(text(t, "cooldownTime"), 2),
            "armorPenetration": num(text(t, "armorPenetration"), -1), "chanceFactor": num(text(t, "chanceFactor"), 1),
            "extraDamages": extras}


def ability_info(col, name, dmg_cache, charges=0, source=""):
    a = col.get("AbilityDef", name)
    if a is None:
        # Some mods name their own ability types (VFE's, the psycasts'); look for any def of that name.
        for (tag, dn), (pid, e) in col.by.items():
            if dn == name and "Ability" in tag:
                a = col.get(tag, dn)
                break
    if a is None:
        return {"defName": name, "found": False, "charges": charges, "source": source}
    v = a.find("verbProperties")
    comps = []
    cc = a.find("comps")
    for li in (cc.findall("li") if cc is not None else []):
        c = {"class": cls_of(li)}
        for k in ("projectileDef", "damAmount", "range", "lineWidthEnd", "numStreams", "coneSizeDegrees", "radius",
                  "hediffDef", "severity", "durationSecondsOverride", "stunTicks", "damageDef", "thingDef", "explosionRadius"):
            val = text(li, k)
            if val:
                c[k] = val
        if c.get("projectileDef"):
            c["projectile"] = projectile_info(col, c["projectileDef"], dmg_cache)
        comps.append(c)
    cd = text(a, "cooldownTicksRange")
    cd_ticks = None
    if cd:
        parts = [num(x) for x in re.split(r"~", cd) if num(x) is not None]
        if parts:
            cd_ticks = sum(parts) / len(parts)
    vi = verb_info(col, v, dmg_cache) if v is not None else None
    return {"defName": name, "found": True, "mod": col.mod_of("AbilityDef", name), "label": text(a, "label"),
            "abilityClass": text(a, "abilityClass"), "cooldownTicks": cd_ticks, "charges": charges, "source": source,
            "verb": vi, "comps": comps, "category": text(a, "category"), "description": re.sub(r"\s+", " ", text(a, "description"))[:240]}


def default_stuff(col, cats, stuffs):
    """The stuff a weapon is rated in: steel where it takes metal, else wood, else granite blocks, else the first that fits."""
    if not cats:
        return None
    for want, cat in (("Steel", "Metallic"), ("WoodLog", "Woody"), ("BlocksGranite", "Stony")):
        if cat in cats and want in stuffs:
            return want
    for dn, scats in sorted(stuffs.items()):
        if set(scats) & set(cats):
            return dn
    return None


def stuff_info(col, dn):
    d = col.get("ThingDef", dn)
    if d is None:
        return None
    return {"defName": dn, "label": text(d, "label"), "statBases": children_dict(d.find("statBases")),
            "statFactors": children_dict(d.find("stuffProps/statFactors")), "statOffsets": children_dict(d.find("stuffProps/statOffsets"))}


COMBAT_PARTS = {"verbs", "tools", "statBases", "projectile", "equippedStatOffsets", "comps", "modExtensions", "stuffCategories",
                "weaponClasses", "thingClass"}
NOT_COMBAT_STATS = {"MarketValue", "WorkToMake", "Beauty", "Flammability", "SellPriceFactor", "DeteriorationRate", "MaxHitPoints",
                    "WorkToBuild", "Mass"}


def patch_ops(node, names, gate=""):
    """Every patch operation in a file that can run for the player's mod list: nested ones (sequences, conditionals)
    included, a FindMod's match only when its mods are all active (its nomatch otherwise), and an operation of a mod's
    own class marked as running on that mod's own condition (a setting)."""
    for op in node:
        if not isinstance(op.tag, str):
            continue
        cls = (op.attrib.get("Class", "") or "").split(".")[-1]
        if cls == "PatchOperationFindMod":
            mods = [(li.text or "").strip().lower() for li in (op.find("mods") if op.find("mods") is not None else [])]
            branch = op.find("match") if all(m in names for m in mods) else op.find("nomatch")
            if branch is not None:
                yield from patch_ops([branch], names, gate)
            continue
        own = cls and not cls.startswith("PatchOperation")
        xp = op.find("xpath")
        if xp is not None and xp.text:
            yield op, xp.text, gate
        for child in op:
            if isinstance(child.tag, str) and child.tag in ("operations", "match", "nomatch"):
                kids = list(child) if child.tag == "operations" else [child]
                yield from patch_ops(kids, names, gate or ("on its own setting" if own else ""))


def patches(col, wanted, parts=COMBAT_PARTS, skip_stats=NOT_COMBAT_STATS):
    """The active mods' patch operations that touch the numbers of a def in `wanted`: a weapon's or projectile's combat
    parts by default, or the parts given (Tools/apparel_extract.py passes an apparel def's), less the stats given."""
    out = {}
    parts_wanted = parts
    mods = os.path.join(col.root, "Mods")
    active = set(col.pool.active)
    names = {name.strip().lower() for folder, (pid, name) in col.pool.folders.items() if pid in active}
    names |= {"core", "royalty", "ideology", "biotech", "anomaly", "odyssey"}
    for folder in sorted(os.listdir(mods)) if os.path.isdir(mods) else []:
        pid = col.pool.folders.get(folder, (folder.lower(), folder))[0]
        if pid not in active or folder in col.pool.old_only:
            continue
        moddir = os.path.join(mods, folder)
        for lf in mod_pool.load_folders(moddir, active):
            pdir = mod_pool._child(mod_pool._child(moddir, lf), "Patches")
            for dp, dirs, fs in os.walk(pdir):
                dirs.sort()
                for f in sorted(fs):
                    if not f.lower().endswith(".xml"):
                        continue
                    r = mod_pool.parse(os.path.join(dp, f))
                    if r is None:
                        continue
                    for op, xp, gate in patch_ops(list(r), names):
                        named = re.findall(r"defName\s*=\s*[\"']([^\"']+)[\"']", xp)
                        hits = [n for n in named if n in wanted]
                        if not hits:
                            continue
                        after = re.split(r"\]", xp, maxsplit=1)
                        sub = after[1].strip("/") if len(after) > 1 else ""
                        parts = [x for x in re.split(r"/", sub) if x]
                        if parts:
                            first = re.sub(r"\[.*", "", parts[0])
                            if first not in parts_wanted:
                                continue
                            if first == "statBases" and len(parts) > 1 and re.sub(r"\[.*", "", parts[1]) in skip_stats:
                                continue
                            what = "/".join(re.sub(r"\[.*", "", x) for x in parts[:2])
                        else:
                            val = op.find("value")
                            tags = sorted({c.tag for c in (list(val) if val is not None else []) if isinstance(c.tag, str)})
                            tags = [t for t in tags if t in parts_wanted]
                            if not tags:
                                continue
                            what = ",".join(tags)
                        for n in hits:
                            entry = {"mod": pid, "modName": col.mod_name(pid), "file": os.path.relpath(os.path.join(dp, f), mods),
                                     "operation": (op.attrib.get("Class", "") or op.tag).split(".")[-1], "what": what, "when": gate}
                            lst = out.setdefault(n, [])
                            if entry not in lst:
                                lst.append(entry)
    return out


def main(root, out_path):
    col = Collection(root)
    mans = maneuvers(col)
    dmg = {}
    stuffs = {}
    turret_guns = set()
    for (tag, dn), (pid, e) in col.by.items():
        if tag != "ThingDef" or not col.pool.on(pid):
            continue
        d = col.get(tag, dn)
        if d.find("stuffProps") is not None:
            stuffs[dn] = lis(d, "stuffProps/categories")
        g = text(d, "building/turretGunDef")
        if g:
            turret_guns.add(g)
    research_tech = {}
    made = set()       # what the active mods' recipes make (a RecipeDef's products, a recipe maker's thing)
    kind_tags = set()  # the weapon tags the active mods' pawn kinds carry
    for (tag, dn), (pid, e) in col.by.items():
        if not col.pool.on(pid):
            continue
        if tag == "ResearchProjectDef":
            research_tech[dn] = text(col.get(tag, dn), "techLevel") or "Undefined"
        elif tag == "RecipeDef":
            pr = col.get(tag, dn).find("products")
            made.update(c.tag for c in (list(pr) if pr is not None else []) if isinstance(c.tag, str))
        elif tag == "PawnKindDef":
            kind_tags.update(lis(col.get(tag, dn), "weaponTags"))
        elif tag == "ThingDef" and col.get(tag, dn).find("recipeMaker") is not None:
            made.add(dn)
    weapons = []
    for (tag, dn), (pid, e) in sorted(col.by.items(), key=lambda kv: (kv[1][0], kv[0][1])):
        if tag != "ThingDef" or not col.pool.on(pid):
            continue
        d = col.get(tag, dn)
        cats = lis(d, "thingCategories")
        if d.find("apparel") is not None or d.find("race") is not None:
            continue
        if not (d.find("weaponTags") is not None or "Weapon" in ",".join(cats)):
            continue
        verbs = [verb_info(col, v, dmg) for v in items(d, "verbs")]
        tools = [tool_info(col, t, mans, dmg) for t in items(d, "tools")]
        comps = []
        abilities = []
        for li in items(d, "comps"):
            c = dict(leaves(li))
            c["class"] = cls_of(li)
            for k in ("abilityDef", "maxCharges", "explosiveRadius", "explosiveDamageType", "biocodeOnEquip", "weaponCategories"):
                val = text(li, k)
                if val:
                    c[k] = val
            for k in ("abilities", "abilityDefs", "weaponCategories", "traits"):
                if li.find(k) is not None:
                    c[k] = lis(li, k)
            if c["class"] == "CompProperties_UniqueWeapon":
                c["weaponCategories"] = lis(li, "weaponCategories")
            comps.append(c)
            charges = int(num(text(li, "maxCharges"), 0) or 0)
            for ab in ([text(li, "abilityDef")] if text(li, "abilityDef") else []) + c.get("abilities", []) + c.get("abilityDefs", []):
                abilities.append(ability_info(col, ab, dmg, charges, c["class"]))
        exts = []
        for li in items(d, "modExtensions"):
            x = dict(leaves(li))
            x["class"] = (li.attrib.get("Class", "") or "").split(".")[-1]
            if x["class"] == "HeavyWeapon":
                x["isHeavy"] = boolean(text(li, "isHeavy"))
                x["hpPerShot"] = num(text(li, "weaponHitPointsDeductionOnShot"), 0)
                x["supportedArmors"] = lis(li, "supportedArmors")
                x["supportedTraits"] = lis(li, "supportedTraits")
                x["supportedGenes"] = lis(li, "supportedGenes")
            if x["class"] == "MakeshiftProperties":
                x["shots"] = text(li, "shots")
            if x["class"] == "ThingDefExtension" and text(li, "usableWithShields"):
                x["usableWithShields"] = boolean(text(li, "usableWithShields"))
            exts.append(x)
        stuff_cats = lis(d, "stuffCategories")
        sd = default_stuff(col, stuff_cats, stuffs)
        tags = lis(d, "weaponTags")
        left_out = ""
        if dn in turret_guns or any("Turret" in t for t in tags):
            left_out = "a turret's gun"
        elif boolean(text(d, "destroyOnDrop")):
            if any(t.startswith("Mechanoid") for t in tags):
                left_out = "a mechanoid's built-in gun (vanishes when dropped)"
            elif pid == "torann.arimworldofmagic":
                left_out = "made by a spell or a class power (vanishes when dropped)"
            else:
                left_out = "a body's or a power's weapon (vanishes when dropped)"
        elif text(d, "equipmentType") == "None":
            left_out = "not equippable"
        elif not verbs and not tools:
            left_out = "no attack of its own"
        weapons.append({
            "defName": dn, "label": text(d, "label"), "mod": pid, "modName": col.mod_name(pid), "techLevel": text(d, "techLevel"),
            "description": re.sub(r"\s+", " ", text(d, "description"))[:300],
            "statBases": children_dict(d.find("statBases")), "equippedStatOffsets": children_dict(d.find("equippedStatOffsets")),
            "weaponTags": tags, "weaponClasses": lis(d, "weaponClasses"), "thingCategories": cats,
            "stuffCategories": stuff_cats, "costStuffCount": num(text(d, "costStuffCount"), 0), "stuff": stuff_info(col, sd) if sd else None,
            "tradeability": text(d, "tradeability"), "destroyOnDrop": boolean(text(d, "destroyOnDrop")),
            "equipmentType": text(d, "equipmentType"), "thingClass": text(d, "thingClass"), "relicChance": num(text(d, "relicChance"), 0),
            "comps": comps, "compClasses": comp_names(d), "modExtensions": exts, "verbs": verbs, "tools": tools, "abilities": abilities,
            "craftable": d.find("recipeMaker") is not None, "leftOut": left_out,
            "made": dn in made, "carriedByKinds": bool(set(tags) & kind_tags),
            "research": mod_pool.research_of(d.find("recipeMaker")) if d.find("recipeMaker") is not None else [],
            "researchTech": max((research_tech.get(x, "Undefined") for x in (mod_pool.research_of(d.find("recipeMaker"))
                                 if d.find("recipeMaker") is not None else [])), key=lambda t: mod_pool.TECHS.index(t)
                                if t in mod_pool.TECHS else 0, default=""),
        })
    wanted = {w["defName"] for w in weapons}
    proj_of = {}
    for w in weapons:
        for v in w["verbs"]:
            if v.get("projectile") and v["projectile"].get("defName"):
                proj_of.setdefault(v["projectile"]["defName"], []).append(w["defName"])
    found = patches(col, wanted | set(proj_of))
    by_weapon = {}
    for name, lst in found.items():
        for target in ([name] if name in wanted else []) + proj_of.get(name, []):
            for entry in lst:
                e2 = dict(entry, target=name)
                by_weapon.setdefault(target, []).append(e2)
    for w in weapons:
        w["patches"] = by_weapon.get(w["defName"], [])
    data = {"source": "the player's def collection, read as RimWorld 1.7 loads it (Tools/mod_pool.py), patches not applied",
            "weapons": weapons, "damageDefs": dmg, "maneuvers": mans}
    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
    raw = json.dumps(data, indent=1, sort_keys=True, ensure_ascii=False).encode("utf-8")
    if out_path.endswith(".gz"):
        with gzip.GzipFile(out_path, "wb", mtime=0) as f:
            f.write(raw)
    else:
        with open(out_path, "wb") as f:
            f.write(raw)
    left = sum(1 for w in weapons if w["leftOut"])
    print(f"weapons: {len(weapons)} ({len(weapons) - left} a colonist can wield, {left} left out); "
          f"damage defs: {len(dmg)}; weapons a patch touches: {sum(1 for w in weapons if w['patches'])}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    main(sys.argv[1], sys.argv[2])
