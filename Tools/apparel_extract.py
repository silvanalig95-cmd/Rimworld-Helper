#!/usr/bin/env python3
"""Every apparel def of the player's active mods, resolved to the numbers the apparel rating reads (Tools/ApparelReview).

Reads the player's def collection the way Tools/weapon_extract.py does (through Tools/mod_pool.py's Pool: the folders
RimWorld 1.7 loads per LoadFolders.xml, IfModActive, ParentName inheritance, MayRequire and MayRequireAnyOf as the game
honours them, which mods are active, load order) and writes one JSON file with:

  apparel: every ThingDef of an active mod with an <apparel> block, with its own numbers as the game resolves them: the
    stat bases (armour, insulation and their stuff multipliers, hit points, wear), what it changes on its wearer
    (equippedStatOffsets), its stuff categories and the stuff it is rated in, its layers, body part groups and tags, the
    apparel fields the game and the rating read (nudity, wear per day, gender, ages, slaves, mechanitors, toxic gas), its
    comps and mod extensions (every field, nested lists and tables kept), its verbs (each with its projectile, as the weapon
    review reads them), the abilities it grants (resolved AbilityDefs), the hediffs it gives while worn (their stages'
    stat offsets and factors, capacities and pain), how the player's game comes by it (recipes, research, the pawn kinds
    that wear it), the royal titles and faith roles whose apparel rules it meets, why it is left out if a colonist can't
    wear it, and the active mods' patches that touch it;
  stuffs: every stuff the rating may rate a piece in (its stuff powers for armour and insulation), the default one per
    stuff category and the common ones per category for the per-stuff ratings;
  body: the human body's hit zones (Bodies_Humanlike: each group of body part groups with the share of ranged and melee
    hits that land there, by the game's own choice of the part hit), for the review to check the rating's own copy;
  statDefaults: the base value of each stat the pieces change (StatDef defaultBaseValue, a human's own statBases over it);
  threats: the reference enemies' attacks as their defs give them (guns' projectiles, melee weapons' and animals' and
    mechanoids' tools), for the review to check the rating's own copy;
  requirements: every royal title's, meme's, role's and faith's apparel rule.

Patches are not applied (as in the mod pool); the ones that touch an apparel def are listed with it.

Run:
  python3 Tools/apparel_extract.py <folder with Data, Mods, mods.txt, ModsConfig.xml> docs/player-mods/apparel_numbers.json.gz
"""
import gzip
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mod_pool  # noqa: E402
import weapon_extract as wx  # noqa: E402
from mod_pool import text, lis  # noqa: E402

# The stuff a piece is rated in, by its stuff categories in this order of preference (the player's wish: devilstrand
# where it makes sense), and the common stuffs of each category it is also rated in.
DEFAULT_STUFF = [("Fabric", "DevilstrandCloth"), ("Metallic", "Steel"), ("Leathery", "Leather_Plain"), ("Woody", "WoodLog"),
                 ("Stony", "BlocksGranite")]
COMMON_STUFFS = {
    "Fabric": ["Cloth", "Synthread", "DevilstrandCloth", "Hyperweave"],
    "Leathery": ["Leather_Plain", "Leather_Bear", "Leather_Thrumbo"],
    "Metallic": ["Steel", "Plasteel", "Uranium"],
    "Woody": ["WoodLog"],
    "Stony": ["BlocksGranite"],
}

# The reference enemies' weapons and bodies (ApparelRateMath.Threats names them); their numbers are written for the
# review to check the rating's own copy against.
THREAT_WEAPONS = ["Gun_Autopistol", "Gun_Revolver", "Gun_MachinePistol", "Gun_HeavySMG", "Gun_PumpShotgun",
                  "Gun_BoltActionRifle", "Gun_AssaultRifle", "Gun_LMG", "Gun_SniperRifle", "Gun_ChargeRifle", "Bow_Recurve",
                  "Weapon_GrenadeFrag", "Weapon_GrenadeMolotov", "Gun_IncendiaryLauncher", "Gun_ChargeLance",
                  "Gun_ChargeBlasterHeavy", "Gun_InfernoCannon", "Gun_Needle", "Gun_MiniShotgun", "Gun_ThumpCannon"]
THREAT_RACES = ["Human", "Wolf_Timber", "Warg", "Bear_Grizzly", "Cougar", "Megaspider", "Thrumbo", "Muffalo", "Mech_Scyther"]

# Stats whose base value the rating reads to turn an offset into a share (psychic heat and focus, carrying, move speed).
DEFAULT_STATS = ["MoveSpeed", "PsychicEntropyMax", "PsychicEntropyRecoveryRate", "MeditationFocusGain", "CarryingCapacity",
                 "PsychicSensitivity", "PainShockThreshold", "MentalBreakThreshold", "MechBandwidth", "WorkSpeedGlobal",
                 "ToxicResistance", "ToxicEnvironmentResistance", "VacuumResistance", "HuntingStealth", "SocialImpact",
                 "NegotiationAbility", "TradePriceImprovement", "GlobalLearningFactor", "RestRateMultiplier",
                 "ImmunityGainSpeed", "IncomingDamageFactor", "MeleeDodgeChance", "ShootingAccuracyPawn", "MeleeHitChance",
                 "AimingDelayFactor", "RangedCooldownFactor", "MeleeDamageFactor", "SuppressionPower", "ResearchSpeed",
                 "MedicalTendQuality", "MedicalTendSpeed", "MedicalSurgerySuccessChance", "ConstructionSpeed"]


def fields(e, depth=0):
    """A comp's, extension's or verb's fields as written: a leaf's text, a list of li texts, a list of li tables, or a
    table of its children (equippedStatFactors, statOffsets); nested a few levels."""
    out = {}
    for c in e:
        if not isinstance(c.tag, str) or not wx.loaded(c):
            continue
        kids = [k for k in c if isinstance(k.tag, str) and wx.loaded(k)]
        if not kids:
            out[c.tag] = (c.text or "").strip()
        elif all(k.tag == "li" for k in kids):
            if all(len([g for g in k if isinstance(g.tag, str)]) == 0 for k in kids):
                out[c.tag] = [(k.text or "").strip() for k in kids]
            elif depth < 3:
                out[c.tag] = [fields(k, depth + 1) for k in kids]
        elif depth < 3:
            out[c.tag] = fields(c, depth + 1)
    return out


def comp_record(li):
    r = fields(li)
    cls = li.attrib.get("Class", "") or text(li, "compClass")
    r["class"] = cls
    if text(li, "compClass"):
        r["compClass"] = text(li, "compClass")
    return r


def hediff_info(col, name, cache):
    """A hediff's stages as the rating reads them: stat offsets and factors, capacities, pain; and its comps' classes."""
    if not name or name in cache:
        return
    h = col.get("HediffDef", name)
    if h is None:
        cache[name] = {"defName": name, "found": False}
        return
    stages = []
    st = h.find("stages")
    for li in (st.findall("li") if st is not None else []):
        if not wx.loaded(li):
            continue
        caps = []
        for c in wx.items(li, "capMods"):
            caps.append({"capacity": text(c, "capacity"), "offset": wx.num(text(c, "offset"), 0),
                         "postFactor": wx.num(text(c, "postFactor"), 1), "setMax": wx.num(text(c, "setMax"))})
        stages.append({"minSeverity": wx.num(text(li, "minSeverity"), 0), "label": text(li, "label"),
                       "statOffsets": wx.children_dict(li.find("statOffsets")), "statFactors": wx.children_dict(li.find("statFactors")),
                       "capMods": caps, "painOffset": wx.num(text(li, "painOffset"), 0), "painFactor": wx.num(text(li, "painFactor"), 1),
                       "totalBleedFactor": wx.num(text(li, "totalBleedFactor"), 1), "hungerRateFactor": wx.num(text(li, "hungerRateFactor"), 1),
                       "restFallFactor": wx.num(text(li, "restFallFactor"), 1), "socialFightChanceFactor": wx.num(text(li, "socialFightChanceFactor"), 1)})
    cache[name] = {"defName": name, "found": True, "label": text(h, "label"), "mod": col.mod_of("HediffDef", name),
                   "hediffClass": text(h, "hediffClass"), "stages": stages, "comps": [comp_record(li) for li in wx.items(h, "comps")],
                   "description": re.sub(r"\s+", " ", text(h, "description"))[:240]}


def body_zones(col, name="Human"):
    """The body's hit zones: its parts grouped by their body part groups, each with the share of hits that land there.
    The game picks the part a hit lands on by its own coverage (coverageAbs: its share of its parent less its children's),
    among every part for a bullet or an arrow (no depth set), among the outside parts for a melee blow (Verb_MeleeAttack
    sets the outside), and a blunt blow never lands on an eye (its hitChanceFactors)."""
    b = col.get("BodyDef", name)
    parts = []

    def walk(e, parent_abs, parent_depth, parent_height):
        cov = wx.num(text(e, "coverage"), 1.0)
        ab = parent_abs * cov
        kids = [k for k in (e.find("parts").findall("li") if e.find("parts") is not None else [])]
        rem = 1.0 - sum(wx.num(text(k, "coverage"), 0) for k in kids)
        if abs(rem) < 1e-5:
            rem = 0.0
        depth = text(e, "depth") or parent_depth
        height = text(e, "height") or parent_height
        pdef = text(e, "def")
        pd = col.get("BodyPartDef", pdef)
        blunt0 = False
        conceptual = False
        if pd is not None:
            conceptual = wx.boolean(text(pd, "conceptual"))
            for hf in (pd.find("hitChanceFactors").findall("li") if pd.find("hitChanceFactors") is not None else []):
                if text(hf, "key") == "Blunt" and wx.num(text(hf, "value"), 1) == 0:
                    blunt0 = True
        parts.append({"label": text(e, "customLabel") or pdef, "def": pdef, "coverageAbs": ab * max(rem, 0.0), "depth": depth,
                      "height": height, "groups": lis(e, "groups"), "bluntImmune": blunt0, "conceptual": conceptual})
        for k in kids:
            walk(k, ab, depth, height)

    walk(b.find("corePart"), 1.0, "Outside", "Middle")
    zones = {}
    for mode in ("ranged", "melee", "meleeBlunt"):
        pool = [p for p in parts if not p["conceptual"] and (mode == "ranged" or p["depth"] == "Outside")
                and not (mode == "meleeBlunt" and p["bluntImmune"])]
        total = sum(p["coverageAbs"] for p in pool)
        for p in pool:
            key = ",".join(sorted(set(p["groups"])))
            z = zones.setdefault(key, {"groups": sorted(set(p["groups"])), "parts": [], "ranged": 0.0, "melee": 0.0, "meleeBlunt": 0.0})
            if p["label"] not in z["parts"]:
                z["parts"].append(p["label"])
            z[mode] += p["coverageAbs"] / total
    out = []
    for key in sorted(zones, key=lambda k: -zones[k]["ranged"]):
        z = zones[key]
        out.append({"groups": z["groups"], "parts": z["parts"], "ranged": round(z["ranged"], 5), "melee": round(z["melee"], 5),
                    "meleeBlunt": round(z["meleeBlunt"], 5)})
    return {"body": name, "zones": out, "parts": parts}


def requirement(li):
    """An ApparelRequirement as written: the groups it matches, the tags and defs it names."""
    r = li.find("requirement") if li.find("requirement") is not None else li
    return {"groups": lis(r, "bodyPartGroupsMatchAny"), "requiredTags": lis(r, "requiredTags"),
            "allowedTags": lis(r, "allowedTags"), "requiredDefs": lis(r, "requiredDefs"), "groupLabel": text(r, "groupLabel")}


def requirements(col):
    """Every apparel rule of the active defs: royal titles (requiredApparel), memes (apparelRequirements), roles
    (roleApparelRequirements) and fixed faiths that name apparel."""
    out = []
    for (tag, dn), (pid, e) in sorted(col.by.items()):
        if not col.pool.on(pid):
            continue
        if tag not in ("RoyalTitleDef", "MemeDef", "PreceptDef", "IdeoDef") and not tag.endswith("IdeoDef"):
            continue
        d = col.get(tag, dn)
        for path in ("requiredApparel", "apparelRequirements", "roleApparelRequirements"):
            for li in wx.items(d, path):
                rq = requirement(li)
                rq.update({"source": tag, "defName": dn, "label": text(d, "label"), "mod": pid,
                           "seniority": wx.num(text(d, "seniority"), 0), "path": path})
                out.append(rq)
    return out


def stat_defaults(col):
    out = {}
    human = col.get("ThingDef", "Human")
    hb = wx.children_dict(human.find("statBases")) if human is not None else {}
    for s in DEFAULT_STATS:
        d = col.get("StatDef", s)
        v = wx.num(text(d, "defaultBaseValue"), 0.0) if d is not None else None
        out[s] = hb.get(s, v)
    return out


def threats(col, mans, dmg):
    out = {}
    for n in THREAT_WEAPONS + THREAT_RACES:
        d = col.get("ThingDef", n)
        if d is None:
            out[n] = {"found": False}
            continue
        out[n] = {"found": True, "mod": col.mod_of("ThingDef", n), "label": text(d, "label"),
                  "verbs": [wx.verb_info(col, v, dmg) for v in wx.items(d, "verbs")],
                  "tools": [wx.tool_info(col, t, mans, dmg) for t in wx.items(d, "tools")],
                  "statBases": wx.children_dict(d.find("statBases"))}
    return out


def meets(rq, groups, tags, dn):
    """ApparelRequirement.AllowedForPawn on a def: named or carrying a required or allowed tag, and matching a group."""
    named = dn in rq["requiredDefs"] or any(t in rq["requiredTags"] or t in rq["allowedTags"] for t in tags)
    required = dn in rq["requiredDefs"] or any(t in rq["requiredTags"] for t in tags)
    return named and any(g in groups for g in rq["groups"]), required and any(g in groups for g in rq["groups"])


APPAREL_PARTS = {"statBases", "equippedStatOffsets", "apparel", "comps", "modExtensions", "stuffCategories", "verbs",
                 "costStuffCount", "thingClass"}
NOT_APPAREL_STATS = {"MarketValue", "WorkToMake", "Beauty", "SellPriceFactor", "Mass", "WorkToBuild"}


def main(root, out_path):
    col = wx.Collection(root)
    mans = wx.maneuvers(col)
    dmg = {}
    hediffs = {}
    stuffs = {}
    stuff_cats = {}
    for (tag, dn), (pid, e) in col.by.items():
        if tag != "ThingDef" or not col.pool.on(pid):
            continue
        d = col.get(tag, dn)
        if d.find("stuffProps") is not None:
            stuff_cats[dn] = lis(d, "stuffProps/categories")
    made = set()
    worn_tags = set()
    worn_defs = set()
    research_tech = {}
    for (tag, dn), (pid, e) in col.by.items():
        if not col.pool.on(pid):
            continue
        if tag == "ResearchProjectDef":
            research_tech[dn] = text(col.get(tag, dn), "techLevel") or "Undefined"
        elif tag == "RecipeDef":
            pr = col.get(tag, dn).find("products")
            made.update(c.tag for c in (list(pr) if pr is not None else []) if isinstance(c.tag, str))
        elif tag == "PawnKindDef":
            k = col.get(tag, dn)
            worn_tags.update(lis(k, "apparelTags"))
            worn_defs.update(lis(k, "apparelRequired"))
            for li in wx.items(k, "specificApparelRequirements"):
                if text(li, "apparelDef"):
                    worn_defs.add(text(li, "apparelDef"))
                worn_tags.update(lis(li, "requiredTag") or ([text(li, "requiredTag")] if text(li, "requiredTag") else []))
        elif tag == "ThingDef" and col.get(tag, dn).find("recipeMaker") is not None:
            made.add(dn)
    rules = requirements(col)
    wanted = []
    apparel = []
    for (tag, dn), (pid, e) in sorted(col.by.items(), key=lambda kv: (kv[1][0], kv[0][1])):
        if tag != "ThingDef" or not col.pool.on(pid):
            continue
        d = col.get(tag, dn)
        ap = d.find("apparel")
        if ap is None:
            continue
        wanted.append(dn)
        cats = lis(d, "stuffCategories")
        default = None
        for cat, s in DEFAULT_STUFF:
            if cat in cats and s in stuff_cats:
                default = s
                break
        if cats and default is None:
            default = next((s for s, sc in sorted(stuff_cats.items()) if set(sc) & set(cats)), None)
        common = []
        for cat in cats:
            for s in COMMON_STUFFS.get(cat, []):
                if s in stuff_cats and s not in common:
                    common.append(s)
        if default and default not in common:
            common.insert(0, default)
        for s in common:
            if s not in stuffs:
                stuffs[s] = wx.stuff_info(col, s)
                stuffs[s]["categories"] = stuff_cats.get(s, [])
        comps = [comp_record(li) for li in wx.items(d, "comps")]
        exts = [comp_record(li) for li in wx.items(d, "modExtensions")]
        verbs = [dict(wx.verb_info(col, v, dmg), fields=fields(v)) for v in wx.items(d, "verbs")]
        abilities = []
        charges = 0
        for c in comps:
            if "maxCharges" in c:
                charges = int(wx.num(c.get("maxCharges"), 0) or 0)
        for ab in lis(ap, "abilities"):
            abilities.append(wx.ability_info(col, ab, dmg, 0, "apparel"))
        for c in comps:
            for key in ("abilities", "abilityDefs", "Abilities", "MagicAbilities"):
                for ab in (c.get(key) if isinstance(c.get(key), list) else []):
                    if isinstance(ab, str) and ab:
                        abilities.append(wx.ability_info(col, ab, dmg, charges, c["class"].split(".")[-1]))
            if isinstance(c.get("abilityDef"), str) and c["abilityDef"]:
                abilities.append(wx.ability_info(col, c["abilityDef"], dmg, charges, c["class"].split(".")[-1]))
        names = []
        for c in comps:
            for key in ("hediff", "hediffDef"):
                if isinstance(c.get(key), str) and c[key]:
                    names.append(c[key])
            for key in ("hediffDefnames", "hediffs"):
                for h in (c.get(key) if isinstance(c.get(key), list) else []):
                    if isinstance(h, str) and h:
                        names.append(h)
        for h in names:
            hediff_info(col, h, hediffs)
        for v in verbs:
            for key in ("hediffDef", "hediff"):
                if v["fields"].get(key):
                    hediff_info(col, v["fields"][key], hediffs)
        tags = lis(ap, "tags")
        groups = lis(ap, "bodyPartGroups")
        met = []
        for rq in rules:
            allowed, required = meets(rq, groups, tags, dn)
            if allowed:
                met.append({"source": rq["source"], "defName": rq["defName"], "label": rq["label"], "seniority": rq["seniority"],
                            "required": required, "mod": rq["mod"]})
        research = mod_pool.research_of(d.find("recipeMaker")) if d.find("recipeMaker") is not None else []
        left_out = ""
        if wx.boolean(text(d, "destroyOnDrop")):
            left_out = "worn only by what it was made for: it vanishes when taken off (a mechanoid's, a power's)"
        apparel.append({
            "defName": dn, "label": text(d, "label"), "mod": pid, "modName": col.mod_name(pid), "techLevel": text(d, "techLevel"),
            "description": re.sub(r"\s+", " ", text(d, "description"))[:300], "thingClass": text(d, "thingClass"),
            "statBases": wx.children_dict(d.find("statBases")), "equippedStatOffsets": wx.children_dict(d.find("equippedStatOffsets")),
            "stuffCategories": cats, "costStuffCount": wx.num(text(d, "costStuffCount"), 0), "stuff": default, "stuffs": common,
            "layers": lis(ap, "layers"), "bodyPartGroups": groups, "tags": tags,
            "apparel": {k: text(ap, k) for k in ("countsAsClothingForNudity", "careIfDamaged", "careIfWornByCorpse", "wearPerDay",
                                                 "gender", "developmentalStageFilter", "slaveApparel", "mechanitorApparel",
                                                 "immuneToToxGasExposure", "useDeflectMetalEffect", "canBeDesiredForIdeo",
                                                 "blocksVision", "ignoredByNonViolent", "legsNakedUnlessCoveredBySomethingElse")
                        if text(ap, k)},
            "thingCategories": lis(d, "thingCategories"), "tradeTags": lis(d, "tradeTags"), "tradeability": text(d, "tradeability"),
            "destroyOnDrop": wx.boolean(text(d, "destroyOnDrop")), "generateAllowChance": wx.num(text(d, "generateAllowChance"), 1),
            "comps": comps, "extensions": exts, "verbs": verbs, "abilities": abilities,
            "craftable": d.find("recipeMaker") is not None, "made": dn in made,
            "wornByKinds": dn in worn_defs or bool(set(tags) & worn_tags),
            "research": research,
            "researchTech": max((research_tech.get(x, "Undefined") for x in research), key=lambda t: mod_pool.TECHS.index(t)
                                if t in mod_pool.TECHS else 0, default=""),
            "requirementsMet": met, "leftOut": left_out,
        })
    found = wx.patches(col, set(wanted), APPAREL_PARTS, NOT_APPAREL_STATS)
    for a in apparel:
        a["patches"] = found.get(a["defName"], [])
    data = {"source": "the player's def collection, read as RimWorld 1.7 loads it (Tools/mod_pool.py), patches not applied",
            "apparel": apparel, "stuffs": stuffs, "defaultStuffs": dict(DEFAULT_STUFF), "commonStuffs": COMMON_STUFFS,
            "body": body_zones(col), "statDefaults": stat_defaults(col), "threats": threats(col, mans, dmg),
            "requirements": rules, "hediffs": hediffs, "damageDefs": dmg}
    os.makedirs(os.path.dirname(out_path) or ".", exist_ok=True)
    raw = json.dumps(data, indent=1, sort_keys=True, ensure_ascii=False).encode("utf-8")
    if out_path.endswith(".gz"):
        with gzip.GzipFile(out_path, "wb", mtime=0) as f:
            f.write(raw)
    else:
        with open(out_path, "wb") as f:
            f.write(raw)
    left = sum(1 for a in apparel if a["leftOut"])
    print(f"apparel: {len(apparel)} ({len(apparel) - left} a colonist can wear, {left} left out); stuffs: {len(stuffs)}; "
          f"hediffs: {len(hediffs)}; apparel rules: {len(rules)}; pieces a patch touches: {sum(1 for a in apparel if a['patches'])}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    main(sys.argv[1], sys.argv[2])
