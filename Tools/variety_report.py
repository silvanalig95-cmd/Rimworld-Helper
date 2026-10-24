#!/usr/bin/env python3
"""
Variety audit: which lines will a player see again too soon?

Lists every pool of lines in Defs/ (flavor pools and their extensions, toned lines on dreams, wishes, quirks and the
like, plain text lists such as social events and nuisances, and grammar rules in interactions and rule packs with the
log lines other files add to them), counts its lines, estimates how often it is drawn from in a year (60 days) of an
eight-colonist colony, and ranks pools by how often each line comes round again.

"Effective lines" is what a typical speaker actually draws from. For toned pools it replays the pick in Tone.cs and
Flavor.cs (tagged lines win 85% of the time, the speaker's tone first, era filter, plain fallback) over a few hundred
made-up speakers and moments, and takes the inverse Simpson index (1 / sum of p^2) of each pick: a pool of 20 lines
where one [lowtech] line takes most of the picks in a tribal colony counts as about one or two lines for that colony.
"eff10" is the tenth percentile: the unlucky speaker. Grammar pools count the shapes of their root rule (r_logentry),
plain lists count the lines that fit the era.

In the game, Flavor.Memory also keeps a pool from giving out a line it gave out lately while another fits; this
report leaves that out, so a pool with few lines that fit a speaker still shows up as thin (and it is: the memory then
has to reach for lines in another voice or for another moment). "reach" is how many lines a typical speaker can get at
all, which is what the memory has to work with.

The draw rates are estimates: computed for moments, wish talk, dream talk, social events, dreams and nuisances from
their weights, cooldowns and conditions, and set by hand (FREQ below) from reading the callers for the rest. They are
meant for ranking, not as exact numbers.

A pool is flagged thin when it is seen about daily (50+ draws a year) with fewer than 15 lines in a speaker's reach,
or about weekly (8+) with fewer than 8, or with its picks piled onto two or three lines (eff under 3).

Usage: python3 Tools/variety_report.py [--top N] [--filter REGEX] [--all] [--csv]
  --top N       rows to print (default 60)
  --filter RE   only pools whose name matches
  --all         include rarely drawn pools (under 2 draws a year) in the ranking
  --csv         machine-readable output
"""
import functools
import glob
import random
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

TONES = ("plain", "warm", "blunt", "cheerful", "gloomy", "cold")
YEAR_DAYS = 60
COLONISTS = 8

# ---------------------------------------------------------------- the pick, as in Flavor.cs and Tone.cs

HIGH_TECH = re.compile(
    r"\b(guns?|gunfire|gunshots?|rifles?|pistols?|shotguns?|revolvers?|turrets?|grenades?|bullets?|ammo|sandbags?|electric\w*"
    r"|batter(y|ies)|power (cell|grid|line)s?|computers?|comms|radios?|cryptosleep|spaceships?|starships?|shuttles?|lasers?"
    r"|plasma|freezers?|solar|hydroponics?|sun ?lamps?|hospital beds?|vitals monitors?|hi-?tech|high-tech|multi-?analy[sz]ers?"
    r"|components?|glitterworlds?|urbworlds?|sterile (tiles?|floors?))\b", re.I)
ERA_TAGS = {"tribal", "medieval", "industrial", "spacer", "lowtech"}
STANDING = ERA_TAGS | {"poor", "rich", "small", "large"}


@functools.lru_cache(maxsize=None)
def strip(line):
    s = line.lstrip()
    if s.startswith("["):
        end = s.find("]")
        if end >= 0:
            tags = [t.strip() for t in s[1:end].split(",") if t.strip()]
            return tuple(tags), s[end + 1:].lstrip()
    return None, line


def match(line, ctx):
    tags, _ = strip(line)
    if not tags:
        return 0
    for tag in tags:
        ok = False
        for alt in tag.split("|"):
            a = alt.strip()
            neg = a.startswith("!")
            if neg:
                a = a[1:]
            if (a in ctx) != neg:
                ok = True
                break
        if not ok:
            return -1
    # Tags that hold for most of a game (era, wealth, size) only decide whether a line fits (Flavor.Match).
    return sum(1 for tag in tags if not all(a.strip().lstrip("!") in STANDING for a in tag.split("|")))


def era_fits(line, ctx):
    tags, _ = strip(line)
    for tag in tags or []:
        alts = [a.strip() for a in tag.split("|")]
        if not all(a.lstrip("!") in ERA_TAGS for a in alts):
            continue
        if not any((a[1:] not in ctx) if a.startswith("!") else (a in ctx) for a in alts):
            return False
    return True


def tech_fits(line, ctx):
    return _tech_fits(line, tuple(sorted(ERA_TAGS & ctx)))


@functools.lru_cache(maxsize=None)
def _tech_fits(line, era):
    ctx = set(era)
    if not era_fits(line, ctx):
        return False
    if "lowtech" not in ctx:
        return True
    return not HIGH_TECH.search(strip(line)[1])


def dist_pick(lines, ctx):
    """Flavor.Pick: tagged matches weigh 2.5 + 1.5 per tag, untagged 1. {line: p} or None."""
    w = {}
    for l in lines:
        if not l or not tech_fits(l, ctx):
            continue
        m = match(l, ctx)
        if m < 0:
            continue
        w[l] = w.get(l, 0) + (1.0 if m == 0 else 2.5 + 1.5 * m)
    tot = sum(w.values())
    return {k: v / tot for k, v in w.items()} if tot else None


def dist_tagged(lines, ctx):
    """Flavor.PickTagged: only lines with a matching tag, weighed by how many match."""
    w = {}
    for l in lines:
        if not l or not tech_fits(l, ctx):
            continue
        m = match(l, ctx)
        if m > 0:
            w[l] = w.get(l, 0) + m
    tot = sum(w.values())
    return {k: v / tot for k, v in w.items()} if tot else None


def add(acc, d, p):
    if d and p > 0:
        for k, v in d.items():
            acc[k] = acc.get(k, 0) + v * p


def dist_tone(pool, tone, ctx):
    """ToneLines.Pick, as probabilities."""
    lst, plain = pool.get(tone, []), pool.get("plain", [])
    alls = [l for t in TONES for l in pool.get(t, [])]
    acc = {}
    rest = 1.0
    tagged = dist_tagged(lst, ctx)
    if tagged:
        add(acc, tagged, 0.85)
        rest -= 0.85
    else:
        tp = dist_tagged(plain, ctx)
        if tp:
            add(acc, tp, 0.85 * 0.75)
            rest -= 0.85 * 0.75
    # The fallback: own list (always for plain, 80% for the others), then plain, then anything.
    use_own = 1.0 if tone == "plain" else 0.8
    own = dist_pick(lst, ctx) if lst else None
    fb_plain = dist_pick(plain, ctx) if plain else None
    fb_all = dist_pick(alls, ctx)
    last = [l for l in alls if tech_fits(l, ctx)] or alls
    last_d = {l: 1.0 / len(last) for l in last} if last else None

    def chain(*ds):
        for d in ds:
            if d:
                return d
        return None

    add(acc, chain(own, fb_plain, fb_all, last_d), rest * use_own)
    add(acc, chain(fb_plain, fb_all, last_d), rest * (1 - use_own))
    return acc


def dist_talk(pool, tone, ctx):
    """Conversations (Social/Talk/Talk.cs PickLine): the speaker's tone list (half again as likely) and the plain list
    together, every line that fits weighed by its tags; any voice only when neither has one."""
    w = {}
    lists = [(pool.get(tone, []), 1.5 if tone != "plain" else 1.0)]
    if tone != "plain":
        lists.append((pool.get("plain", []), 1.0))
    for lst, f in lists:
        for l in lst:
            if not l or not tech_fits(l, ctx):
                continue
            m = match(l, ctx)
            if m < 0:
                continue
            w[l] = w.get(l, 0) + (1.0 if m == 0 else 2.5 + 1.5 * m) * f
    if not w:
        for t in TONES:
            for l in pool.get(t, []):
                if l and tech_fits(l, ctx) and match(l, ctx) >= 0:
                    w[l] = w.get(l, 0) + 0.5
    tot = sum(w.values())
    return {k: v / tot for k, v in w.items()} if tot else None


def effective(d):
    s = sum(v * v for v in d.values())
    return 1.0 / s if s > 0 else 0.0


# ---------------------------------------------------------------- made-up speakers and moments

TRAITS = [
    ("Kind", 5), ("Abrasive", 5), ("Psychopath", 2), ("Greedy", 4), ("Jealous", 4), ("Ascetic", 3), ("Gourmand", 4),
    ("Industriousness:Industrious", 3), ("Industriousness:HardWorker", 3), ("Industriousness:Lazy", 3),
    ("Industriousness:Slothful", 1), ("NaturalMood:Sanguine", 2), ("NaturalMood:Optimist", 3),
    ("NaturalMood:Pessimist", 3), ("NaturalMood:Depressive", 2), ("Nerves:IronWilled", 2), ("Nerves:Steadfast", 3),
    ("Nerves:Nervous", 3), ("Nerves:Volatile", 2), ("TooSmart", 3), ("Bloodlust", 3), ("Brawler", 3), ("Wimp", 2),
    ("Tough", 3), ("NightOwl", 3), ("Undergrounder", 2), ("Recluse", 2), ("Masochist", 1), ("Pyromaniac", 2),
    ("DrugDesire:Teetotaler", 2), ("DrugDesire:ChemicalInterest", 2), ("SpeedOffset:Jogger", 2),
    ("SpeedOffset:Slowpoke", 2), ("Nimble", 2), ("FastLearner", 2), ("GreatMemory", 2), ("Cannibal", 1),
    ("AnnoyingVoice", 2), ("Gay", 3), ("Transhumanist", 2), ("BodyPurist", 2), ("Beauty:Pretty", 2),
    ("Beauty:Ugly", 2), ("Neurotic", 2),
]
SKILLS = ["Shooting", "Melee", "Construction", "Mining", "Cooking", "Plants", "Animals", "Crafting", "Artistic",
          "Medicine", "Social", "Intellectual"]
WORK = ["Cooking", "Growing", "Mining", "Construction", "Hauling", "Cleaning", "Crafting", "Doctor", "Research",
        "Handling", "Hunting", "Smithing", "Tailoring", "Art", "PlantCutting", "Firefighter"]


def wpick(rng, items):
    tot = sum(w for _, w in items)
    r = rng.random() * tot
    for x, w in items:
        r -= w
        if r <= 0:
            return x
    return items[-1][0]


def speaker(rng, extra=()):
    """A colonist at some moment: their tags and their tone (Tone.cs ToneFor)."""
    t = set()
    t.add(rng.choice(["spring", "summer", "fall", "winter"]))
    h = rng.randrange(24)
    t.add("night" if h < 6 or h >= 22 else "morning" if h < 11 else "day" if h < 18 else "evening")
    season = next(s for s in ("spring", "summer", "fall", "winter") if s in t)
    r = rng.random()
    if season == "winter":
        if r < 0.3:
            t.add("snow")
        elif r < 0.8:
            t.add("clear")
        if rng.random() < 0.6:
            t.add("cold")
    else:
        if r < 0.2:
            t.add("rain")
        elif r < 0.85:
            t.add("clear")
        if season == "summer" and rng.random() < 0.35:
            t.add("hot")
        if season != "summer" and rng.random() < 0.1:
            t.add("cold")
    if rng.random() < 0.05:
        t.add("fog")
    if rng.random() < 0.05:
        t.add("storm")
    if rng.random() < 0.12:
        t.add("afterraid")
    r = rng.random()
    t.add("poor" if r < 0.4 else "rich" if r > 0.85 else "midwealth")
    r = rng.random()
    if r < 0.25:
        t.add("small")
    elif r > 0.85:
        t.add("large")
    era = wpick(rng, [("tribal", 20), ("medieval", 15), ("industrial", 45), ("spacer", 20)])
    t.add(era)
    if era in ("tribal", "medieval"):
        t.add("lowtech")
    r = rng.random()
    if r < 0.15:
        t.add("lowmood")
    elif r > 0.65:
        t.add("highmood")
    r = rng.random()
    if r < 0.06:
        t.add("child")
    elif r < 0.12:
        t.add("teen")
    elif r < 0.22:
        t.add("old")
    for tag, p in (("hurt", 0.15), ("newcomer", 0.08), ("grieving", 0.08)):
        if rng.random() < p:
            t.add(tag)
    r = rng.random()
    if r < 0.35:
        t.add("trusting")
    elif r < 0.5:
        t.add("doubtful")
    elif r < 0.55:
        t.add("bitter")
    r = rng.random()
    if r < 0.3:
        t.add("inbed")
    elif r < 0.38:
        t.add("eating")
    elif r < 0.53:
        t.add("relaxing")
    elif r < 0.93:
        t.add("working")
        t.add("work:" + rng.choice(WORK))
    if "inbed" in t or rng.random() < 0.6:
        t.add("indoors")
        if rng.random() < 0.5:
            t.add("inownroom")
    else:
        t.add("outside")
    traits = set()
    for _ in range(wpick(rng, [(1, 3), (2, 5), (3, 2)])):
        traits.add(wpick(rng, TRAITS))
    for tr in traits:
        if ":" in tr:
            d, deg = tr.split(":")
            t.add("trait:" + d)
            t.add("trait:" + deg)
        else:
            t.add("trait:" + tr)
    for s in rng.sample(SKILLS, wpick(rng, [(1, 2), (2, 4), (3, 3)])):
        t.add("skill:" + s)
    for tag, p in extra:
        if rng.random() < p:
            t.add(tag)
    # The tone.
    if "trait:Psychopath" in t:
        tone = "cold"
    elif "trait:Abrasive" in t:
        tone = "blunt"
    elif "trait:Kind" in t:
        tone = "warm"
    elif "trait:Sanguine" in t or "trait:Optimist" in t:
        tone = "cheerful"
    elif "trait:Pessimist" in t or "trait:Depressive" in t:
        tone = "gloomy"
    elif "lowmood" in t:
        tone = "gloomy"
    else:
        r = rng.random()
        tone = "warm" if r < 0.1 else "blunt" if r < 0.2 else rng.choice(["warm", "cheerful", "plain", "plain", "plain"])
    return t, tone


# ---------------------------------------------------------------- reading the defs

def text_of(el):
    return (el.text or "").strip()


def is_prose(s):
    return s and " " in s and re.search(r"[A-Za-z]{2,}", s) is not None and "->" not in s.split(" ")[0]


class Pool:
    def __init__(self, name, kind, file, defnode=None, field=""):
        self.name, self.kind, self.file, self.defnode, self.field = name, kind, file, defnode, field
        self.tones = defaultdict(list)   # toned pools
        self.lines = []                  # plain lists
        self.rules = defaultdict(list)   # grammar: keyword -> outputs
        self.extra = ()                  # extra tags the caller adds (with a chance)
        self.draws = None
        self.basis = ""

    def count(self):
        if self.kind == "toned":
            return sum(len(v) for v in self.tones.values())
        if self.kind == "list":
            return len(self.lines)
        return sum(len(v) for v in self.rules.values())


def toned_children(el):
    kids = list(el)
    return kids and all(k.tag in TONES for k in kids)


def parse_defs(root="."):
    pools = {}
    ext_targets = []
    log_targets = []
    for f in sorted(glob.glob(f"{root}/Defs/**/*.xml", recursive=True)):
        try:
            tree = ET.parse(f)
        except ET.ParseError as e:
            print(f"warn: {f} doesn't parse: {e}", file=sys.stderr)
            continue
        rel = f[len(root) + 1:] if f.startswith(root + "/") else f
        for d in tree.getroot():
            if not isinstance(d.tag, str) or d.get("Abstract") == "True" and d.findtext("defName") is None:
                continue
            dtype = d.tag.split(".")[-1]
            dname = d.findtext("defName") or d.get("Name") or "?"
            if dtype == "FlavorExtensionDef":
                ext_targets.append((d, rel))
                continue
            if dtype == "LogLinesDef":
                log_targets.append((d, rel))
                continue
            walk_def(d, dtype, dname, rel, pools)
    # Extensions: lines merged into their targets at startup.
    for d, rel in ext_targets:
        for target_tag in ("pool", "wish", "ambition", "decree"):
            target = d.findtext(target_tag)
            if not target:
                continue
            for child in d:
                if child.tag in ("defName", "pool", "wish", "ambition", "decree") or not toned_children(child):
                    continue
                key = f"{target}" if target_tag == "pool" else f"{target}.{child.tag}"
                p = pools.get(key)
                if p is None:
                    p = pools[key] = Pool(key, "toned", rel, None, child.tag)
                for tone_el in child:
                    p.tones[tone_el.tag].extend(text_of(li) for li in tone_el.findall("li"))
    # Log lines (Social/LogLines.cs): grammar rules joined to an interaction's or a rule pack's at startup.
    for d, rel in log_targets:
        target, sub = d.findtext("interaction"), "logRulesInitiator.rulesStrings"
        if not target:
            target, sub = d.findtext("pack"), "rulePack.rulesStrings"
        p = pools.get(f"{target}.{sub}") if target else None
        if p is None or p.kind != "grammar":
            print(f"warn: {rel}: log lines for {target}, which has no grammar pool", file=sys.stderr)
            continue
        for li in d.findall("rulePack/rulesStrings/li"):
            t = text_of(li)
            if "->" in t:
                k, out = t.split("->", 1)
                p.rules[k.split("(")[0].strip()].append(out)
    return pools


def walk_def(d, dtype, dname, rel, pools):
    def visit(el, path):
        for child in el:
            if not isinstance(child.tag, str):
                continue
            sub = f"{path}.{child.tag}" if path else child.tag
            if toned_children(child) and child.find(".//li") is not None:
                key = dname if dtype == "FlavorPoolDef" else f"{dname}.{sub}"
                p = pools.setdefault(key, Pool(key, "toned", rel, d, sub))
                p.dtype = dtype
                for tone_el in child:
                    p.tones[tone_el.tag].extend(text_of(li) for li in tone_el.findall("li"))
                continue
            if child.tag == "rulesStrings":
                key = f"{dname}.{sub}"
                p = pools.setdefault(key, Pool(key, "grammar", rel, d, sub))
                p.dtype = dtype
                for li in child.findall("li"):
                    t = text_of(li)
                    if "->" in t:
                        k, out = t.split("->", 1)
                        p.rules[k.split("(")[0].strip()].append(out)
                continue
            lis = child.findall("li")
            if lis and len(lis) == len(list(child)) and all(len(list(li)) == 0 for li in lis):
                texts = [text_of(li) for li in lis]
                if sum(1 for t in texts if is_prose(t)) >= max(1, len(texts) // 2):
                    key = f"{dname}.{sub}"
                    p = pools.setdefault(key, Pool(key, "list", rel, d, sub))
                    p.dtype = dtype
                    p.lines.extend(texts)
                    continue
            visit(child, sub)

    visit(d, "")


# ---------------------------------------------------------------- how often each pool is drawn

def ftext(d, path, default=None):
    v = d.findtext(path) if d is not None else None
    return v.strip() if v else default


def moment_rate(d):
    """Moments: a colonist talks about 12 times a day; chitchat weighs 1. Capped by the moment's own cooldown."""
    ext = d.find("modExtensions/li")
    if ext is None:
        return None, ""
    w = float(ftext(ext, "weight", "0.03"))
    cd = float(ftext(ext, "cooldownDays", "3"))
    gate = 1.0
    why = []
    rules = [("initiatorMemories", 0.15), ("recipientMemories", 0.15), ("tie", 0.35), ("mutualFriends", 0.4),
             ("sharedPassion", 0.5), ("recipientBestCook", 0.3), ("bondedPet", 0.3), ("nemesisVictim", 0.05),
             ("ways", 0.4), ("sameIdeo", 0.5), ("otherIdeo", 0.3), ("shares", 0.4), ("moreSkilled", 0.3),
             ("need", 0.3), ("stillSore", 0.2), ("mods", 0.15), ("needsDefs", 0.5),
             # What's going on in their lives (LifeConditions): a feeling of the heart, an ailment, a camp, the region.
             ("initiatorFeelings", 0.15), ("recipientFeelings", 0.15), ("feelingAboutRecipient", 0.3),
             ("initiatorAiling", 0.12), ("recipientAiling", 0.12), ("initiatorHediffs", 0.06), ("recipientHediffs", 0.06),
             ("camp", 0.08), ("situations", 0.2)]
    for tag, f in rules:
        if ext.find(tag) is not None:
            gate *= f
            why.append(tag)
    for tag in ("initiatorTags", "recipientTags"):
        expr = ftext(ext, tag, "")
        if expr:
            # How often a colonist fits, from the same made-up colonists the variety uses. A recipient is one of
            # several people nearby, so a rare recipient (a child) is likelier to be found than to be picked.
            odds = sum(1 for c, _ in SPEAKERS if match("[" + expr + "]", c) >= 0) / len(SPEAKERS)
            gate *= odds if tag == "initiatorTags" else min(1.0, odds * 3)
            why.append(tag)
    if ftext(ext, "tone") == "mend":
        gate *= 0.15
        why.append("a friction first")
    if ftext(ext, "maxOpinion"):
        gate *= 0.6
    per_day = min(1.0 / cd, 12 * w / 1.3 * gate)
    return per_day * COLONISTS * YEAR_DAYS, f"w {w}, cooldown {cd}d" + (", needs " + "/".join(why) if why else "")


def social_event_rates(pools_by_def):
    """About one event a day per five colonists (so ~1.6 a day here), shared by weight among those that can happen."""
    evs = []
    for d in pools_by_def:
        w = float(ftext(d, "weight", "1"))
        e = 1.0
        if ftext(d, "tie"):
            e *= 0.45 if ftext(d, "tie") == "Friends" else 0.25
        if d.find("roleA") is not None or d.find("roleB") is not None:
            e *= 0.5
        for flag in ("bKeepsSecretOfA", "sharedVendetta"):
            if ftext(d, flag) == "true":
                e *= 0.1
        for flag in ("aHasQuirk", "aHasGoal"):
            if ftext(d, flag) == "true":
                e *= 0.4
        # What's going on in their lives (LifeConditions), and a mend that needs a spat first.
        for flag, f in (("aAiling", 0.12), ("bAiling", 0.12), ("bSerious", 0.03), ("camp", 0.08), ("aFeelingAboutB", 0.3)):
            if ftext(d, flag) == "true":
                e *= f
        for tag, f in (("sharedAilment", 0.03), ("aFeelings", 0.15), ("bFeelings", 0.3), ("aMemories", 0.15),
                       ("bMemories", 0.3), ("situations", 0.2)):
            if d.find(tag) is not None:
                e *= f
        if ftext(d, "tone") == "mend":
            e *= 0.15
        if ftext(d, "maxOpinion"):
            e *= 0.6
        if ftext(d, "minMood"):
            e *= 0.6
        evs.append((d, w * e))
    tot = sum(x for _, x in evs) or 1
    per_year = 1.6 * YEAR_DAYS
    return {d: per_year * x / tot for d, x in evs}


def dream_rates(defs):
    """A colonist wakes from a dream about one morning in three: ~140 dreams a year here, shared by weight."""
    ws = []
    for d in defs:
        w = float(ftext(d, "weight", "1"))
        if d.find("afterThoughts") is not None:
            w *= 4 * 0.12
        if ftext(d, "afterThreat") == "true":
            w *= 4 * 0.15
        if d.find("hediffs") is not None:
            w *= 4 * 0.05
        if ftext(d, "unconscious") == "true":
            w *= 0.1
        about = ftext(d, "about", "None")
        if about != "None":
            w *= 1.5 * {"Friend": 0.6, "Rival": 0.3, "Comrade": 0.3, "Teacher": 0.15, "Student": 0.15, "Lover": 0.4,
                        "Family": 0.3, "Colonist": 1.0, "Lost": 0.3, "Goal": 0.4, "Pet": 0.3}.get(about, 0.5)
        if d.find("seasons") is not None:
            w *= 0.25 * len(d.findall("seasons/li"))
        if d.find("omen") is not None:
            w *= 0.1
        if d.find("dreamer") is not None:
            w *= 0.35
        if ftext(d, "needsMechanoids") == "true":
            w *= 0.8
        # Feelings, a camp, the region (Dreams.cs: half again as likely, more with a strong feeling).
        if d.find("feelings") is not None:
            w *= 2.2 * 0.15 * (0.4 if ftext(d, "feelingTarget") == "true" else 1)
        if ftext(d, "camp") == "true":
            w *= 1.5 * 0.08
        if d.find("situations") is not None:
            w *= 1.5 * 0.2
        ws.append((d, w))
    tot = sum(x for _, x in ws) or 1
    return {d: COLONISTS * YEAR_DAYS * 0.3 * x / tot for d, x in ws}


def nuisance_rates(defs):
    """Roughly two nuisances a day in a lived-in colony, by weight, capped by each one's cooldown per colonist."""
    common = {"LongWalk": 1.0, "Clutter": 0.8, "ShoddyClothes": 0.7, "HardBed": 0.6, "AnimalFilth": 0.5,
              "StuckDoor": 0.4, "SpoilingFood": 0.4, "CrampedBedroom": 0.4, "Insects": 0.4, "LeakyRoof": 0.2,
              "SmokyRoom": 0.3, "NoisyGenerator": 0.2, "DirtyRoom": 0.3, "ChillyRoom": 0.3, "StuffyRoom": 0.2,
              "PetInBed": 0.15, "BuildingWork": 0.3, "Vermin": 0.15, "BareFloor": 0.2}
    ws = [(d, float(ftext(d, "weight", "1")) * common.get(ftext(d, "check", ""), 0.4)) for d in defs]
    tot = sum(x for _, x in ws) or 1
    out = {}
    for d, x in ws:
        cap = COLONISTS * YEAR_DAYS / float(ftext(d, "cooldownDays", "4"))
        out[d] = min(cap, 2.0 * YEAR_DAYS * x / tot)
    return out


# Draws a year (60 days, eight colonists), set by hand from the callers. First match wins.
FREQ = [
    # Conversations (Social/Talk/): about 0.8 talks a colonist a day, three in four on a topic (23 of them, the raid, the
    # food, the council and newcomers most), the rest remembering, in-jokes and comfort. Generic pools serve every topic.
    (r"^Rimfolk_TL_Any_Reply\.", 75, "conversation: any topic's reply"),
    (r"^Rimfolk_TL_Any_Close\.", 45, "conversation: any topic's closing word"),
    (r"^Rimfolk_TL_Any_Last\.", 75, "conversation: the other's last word, any talk"),
    (r"^Rimfolk_TL_Any_Tease", 15, "conversation: teasing about any topic"),
    (r"^Rimfolk_TL_(Raid|Food|Council|Newcomer)_(Open|Reply)\.", 22, "conversation: a common topic"),
    # (Comfort, remembering and in-jokes before the topics, whose pattern would take their pools too.)
    (r"^Rimfolk_TL_Comfort_", 25, "conversation: comfort"),
    # Faith talks that move someone (Faith/FaithTalk.cs): about half of a colony's faith talks, a dozen a year.
    (r"^Rimfolk_TL_FaithMoves_(Open|Reply)\.", 7, "conversation: a faith talk that moves someone"),
    (r"^Rimfolk_TL_FaithMoves_Close\.", 5, "conversation: a faith talk that moves someone, the closing word"),
    (r"^Rimfolk_TL_FaithMoves_Last\.", 3, "conversation: a faith talk that moves someone, the last word"),
    (r"^Rimfolk_TL_(Mem|Joke)_(Open|Reply|Close)\.", 8, "conversation: remembering or a joke, any memory"),
    (r"^Rimfolk_TL_(Mem|Joke)_", 4, "conversation: one kind of shared memory"),
    (r"^Rimfolk_TL_\w+_(Open|Reply)\.", 12, "conversation: a topic"),
    (r"^Rimfolk_TL_\w+_Close\.", 7, "conversation: a topic's closing word"),
    (r"^Rimfolk_TL_\w+_Tease\.", 3, "conversation: teasing about a topic"),
    # Everyday journal (Lives/EverydayJournal.cs): one colonist in four writes a day; ~110 entries a year, spread by
    # how often the moment is on someone's mind (weight x how common the thought is), at most once in 12 days each.
    (r"^Council_Journal_Day_(Bedroom|FineMeal|Beauty|DeepTalk)$", 16, "everyday journal, very common thought"),
    (r"^Council_Journal_Day_(Cold|Heat|Tired|Hungry|Bored|KindWords|Disturbed|Barracks|Dining|Dark|Ragged|NoTable|Wet|Pain|RecRoom|Optimism)$", 9, "everyday journal, common thought"),
    (r"^Council_Journal_Day_(Insulted|Lovin|Fight|Taught|Lesson|Party|Paste|RawFood|Outside|CabinFever|Corpse|LavishMeal|Speech|Chores|BadNight|Gossip|ColdShoulder|Teased|Concert|Catharsis|CryingBaby|Nuzzled|NotReturned)$", 5, "everyday journal, sometimes"),
    (r"^Council_Journal_Day_", 2, "everyday journal, rare or mod-only"),
    # Wishes: one new wish every two days or so (30 a year).
    (r"^Council_Pool_Scene$", 25, "every new wish without a trigger"),
    (r"^Council_Pool_Scene_Trigger$", 6, "wishes set off by an event"),
    (r"^Council_Pool_Reminder$", 30, "a reminder halfway through most wishes"),
    (r"^Council_Pool_LastCall$", 20, "the last call on unanswered wishes"),
    (r"^Council_Pool_Scene_Thanks$", 14, "granted wishes"),
    (r"^Council_Pool_(MeToo|CoSign)$", 10, "envy and co-signing on wishes"),
    (r"^Council_Pool_(Promised|Refused|PassedOver|SelfHelp|BrokenPromise|Bitter)$", 6, "answers to wishes"),
    (r"^Council_Journal_(Granted)$", 14, "journal: a wish granted"),
    (r"^Council_Journal_(Lapsed|Refused|PromiseKept|PromiseBroken)$", 6, "journal: a wish answered"),
    (r"^Council_Journal_SelfHelp$", 3, "journal: helped themselves"),
    (r"^Council_Journal_(Arrival|Begin)$", 4, "journal: first entry"),
    (r"^Council_Journal_Raid$", 10, "journal: two writers after each big threat"),
    (r"^Council_Journal_Grief$", 6, "journal: each loss, per mourner"),
    (r"^Council_Journal_(DecreeLiked|DecreeHated)$", 4, "journal: decrees"),
    (r"^Council_Journal_Tie", 3, "journal: bonds formed and ended"),
    (r"^Council_Pool_Touch(Done)?$", 12, "personal touches in bedrooms"),
    # Council meetings: four a year, a line or two from each of eight colonists.
    (r"^Council_Pool_MeetingSupport$", 18, "meeting: support"),
    (r"^Council_Pool_MeetingDoubt$", 8, "meeting: doubt"),
    (r"^Council_Pool_MeetingAside$", 10, "meeting: asides"),
    (r"^Council_Pool_(MeetingOpen|MeetingWake|MeetingOnBehalf)$", 4, "meeting: opening, waking, reading for"),
    (r"^Council_Pool_Heckle$", 8, "meeting heckles"),
    (r"^Council_Pool_Objection", 5, "objections to decisions"),
    (r"^Council_Pool_Ambition", 4, "ambition halfway and deadline"),
    (r"^Council_Eulogy$", 3, "funerals"),
    (r"^Council_Pool_(Quitting|Staying|Quit)$", 2, "someone thinking of leaving"),
    (r"^Council_Journal_Outpost$", 6, "outposts write home"),
    (r"^Council_Journal_Camp$", 4, "camps"),
    (r"^Council_Journal_EncC_", 2, "journal after a creature encounter"),
    (r"^Council_Journal_EncP_", 2, "journal after a people encounter"),
    (r"^Council_Journal_EncR_", 1.5, "journal after a reputation encounter"),
    (r"^Council_Journal_", 1, "journal after a rare event"),
    (r"^Council_Pool_", 3, "other council pools"),
    (r"\.(request|thanks)$", 0.6, "one wish among many"),
    (r"\.(reminder|expired|requestForOther)$", 0.4, "one wish among many"),
    (r"\.(pitch|achieved)$", 0.3, "one ambition among many"),
    (r"\.(support|oppose)$", 1.0, "decree arguments"),
    (r"\.(gained|faded)$", 0.3, "a quirk gained or lost"),
]

WISH_TALK_PER_YEAR = 170  # four open wishes at a time, each talked about about once in two days


SPEAKERS = []


def make_speakers(n=300, seed=7):
    rng = random.Random(seed)
    SPEAKERS[:] = [speaker(rng) for _ in range(n)]


def estimate(pools):
    defs_by_type = defaultdict(list)
    seen = set()
    for p in pools.values():
        if p.defnode is not None and id(p.defnode) not in seen:
            seen.add(id(p.defnode))
            defs_by_type[getattr(p, "dtype", "")].append(p.defnode)
    se = social_event_rates(defs_by_type.get("SocialEventDef", []))
    dr = dream_rates(defs_by_type.get("DreamDef", []))
    nu = nuisance_rates(defs_by_type.get("NuisanceDef", []))
    # Wish talk: shared by how many wishes use each talk.
    talk_uses = defaultdict(int)
    for d in defs_by_type.get("WishDef", []):
        t = ftext(d, "talkInteraction")
        if t:
            talk_uses[t] += 1
    talk_total = sum(talk_uses.values()) or 1
    # Dream talk: told about four times in five, split by the kind of dream.
    kind_share = defaultdict(float)
    for d, x in dr.items():
        kind_share[ftext(d, "kind", "Strange")] += x
    for p in pools.values():
        dtype = getattr(p, "dtype", "")
        d = p.defnode
        if dtype == "SocialEventDef" and p.field == "texts":
            p.draws, p.basis = se.get(d, 0), "social event, by weight and who can take part"
        elif dtype == "DreamDef" and p.field == "lines":
            p.draws, p.basis = dr.get(d, 0), "dream, by weight and conditions"
            p.extra = (("dream", 1.0), ("again", 0.35), ("fever", 0.05))
        elif dtype == "NuisanceDef" and p.field == "texts":
            p.draws, p.basis = nu.get(d, 0), "nuisance, by weight, how common and cooldown"
        elif dtype == "InteractionDef" and p.kind == "grammar":
            name = ftext(d, "defName", "")
            wc = ftext(d, "workerClass", "") or ""
            if d.find("modExtensions/li") is not None and "Moment" in (ftext(d.find("modExtensions/li"), ".", "") or "") or "Council_Moment" in name or "Council_Friction" in name or "Council_Mend" in name:
                p.draws, p.basis = moment_rate(d)
            elif name.startswith("Council_Talk_"):
                p.draws = WISH_TALK_PER_YEAR * talk_uses.get(name, 0) / talk_total
                p.basis = f"wish talk, {talk_uses.get(name, 0)} wishes use it"
            elif name.startswith("Council_DreamTalk_"):
                kind = name.rsplit("_", 1)[1]
                p.draws = 0.8 * (kind_share.get(kind, 0) if kind != "AboutYou" else 0.25 * sum(kind_share.values()) * 0.3)
                p.basis = "dream told over breakfast"
            elif name.startswith("Council_Gossip_"):
                p.draws, p.basis = (12 if "Bitter" in name else 10), "council gossip by doubtful or devoted colonists"
            elif name.startswith("Council_Decree_"):
                p.draws, p.basis = 10, "decree talk while decrees are in force"
            else:
                p.draws, p.basis = 2, "other interaction"
        elif dtype == "RulePackDef":
            p.draws, p.basis = 5, "rule pack (contest results and the like)"
        else:
            for rx, n, why in FREQ:
                if re.search(rx, p.name):
                    p.draws, p.basis = n, why
                    break
            else:
                p.draws, p.basis = 0.5, "not estimated (rare or event text)"
        # Conversations: the caller adds the speaker's side, the other's, how it goes, what kind of memory it is.
        if p.name.startswith("Rimfolk_TL_"):
            # The other's last word (a fourth line) only comes in a debate, an argument or remembering (TalkMath.Turns).
            how = ("debate|argue|reminisce", 1.0) if "_Last." in p.name else ("agree|debate|argue", 1.0)
            p.extra = (("for|against|mixed", 1.0), ("otherfor|otheragainst|othermixed", 1.0), how,
                       ("friends", 0.3), ("good|bad|mixed", 1.0), ("byme|byother", 1.0), ("fresh|told|worn", 1.0),
                       ("grief|despair|lonely|fear|worry|lowmood", 1.0))
            # Faith talks that matter (Faith/FaithTalk.cs), drawn from in the talks where one of the two sways, is swayed,
            # digs in or is steadied; the other's faith as the speaker sees it, whose faith it's about, one faith or two.
            if p.name.startswith("Rimfolk_TL_FaithMoves_"):
                p.extra += (("persuading|persuaded|unmoved|digin|steadied", 1.0), ("sameideo|otherideo", 1.0),
                            ("otherfaith:devout|otherfaith:wavering", 0.5), ("myfaith|theirfaith", 0.6),
                            ("faith:devout|faith:wavering", 0.5), ("faith:alone|faith:minority|faith:majority", 0.4),
                            ("preachedat", 0.15), ("nopreaching", 0.15))
        if p.name.startswith("Council_Journal_Day_"):
            p.extra = (("good|bad", 1.0),)
        if p.field in ("request", "requestForOther", "thanks", "expired", "reminder") or p.name.startswith("Council_Pool_"):
            p.extra = (("promised", 0.1), ("petition", 0.1), ("forother", 0.1), ("repeat", 0.15))
        # The caller always adds the kind of thing: the item planned for a personal touch, the kind of outpost.
        if p.name in ("Council_Pool_Touch", "Council_Pool_TouchDone"):
            p.extra += (("touch:plantpot|touch:light|touch:endtable|touch:dresser", 1.0),)
        if p.name == "Council_Journal_Outpost":
            p.extra += (("outpost:Mining|outpost:Logging|outpost:Farming|outpost:Hunting|outpost:Defensive"
                         "|outpost:Trading|outpost:Science|outpost:Town|outpost:Scavenging|outpost:Encampment", 1.0),)
    # A colonist starts at most one moment in six hours, and most of the day they're asleep or alone: about two and a
    # half a day at the very most, shared out among the moments in proportion.
    moments = [p for p in pools.values() if p.kind == "grammar" and p.basis.startswith("w ")]
    total = sum(p.draws for p in moments) / (COLONISTS * YEAR_DAYS)
    if total > 2.5:
        for p in moments:
            p.draws *= 2.5 / total


# ---------------------------------------------------------------- variety

def grammar_variety(p):
    refs = set()
    for outs in p.rules.values():
        for o in outs:
            refs.update(re.findall(r"\[([a-z_]+)\]", o))
    roots = [k for k in p.rules if k not in refs]
    root = "r_logentry" if "r_logentry" in p.rules else (roots[0] if roots else next(iter(p.rules), None))
    shapes = len(p.rules.get(root, []))
    subs = sum(len(v) for k, v in p.rules.items() if k != root)
    return root, shapes, subs


def variety(p, samples, rng):
    """(effective lines for a typical speaker, tenth percentile, lines a typical speaker can get at all)."""
    if p.kind == "grammar":
        _, shapes, _ = grammar_variety(p)
        return float(shapes), float(shapes), float(shapes)
    effs = []
    reach = []
    pool = {t: [l for l in p.tones.get(t, []) if l] for t in TONES} if p.kind == "toned" else None
    for base, tone in SPEAKERS[:samples]:
        ctx = set(base)
        for tag, chance in p.extra:
            if rng.random() < chance:
                ctx.add(rng.choice(tag.split("|")))
        if p.kind == "toned" and p.name.startswith("Rimfolk_TL_"):
            d = dist_talk(pool, tone, ctx)
        elif p.kind == "toned":
            d = dist_tone(pool, tone, ctx)
        elif getattr(p, "dtype", "") == "NuisanceDef":
            d = dist_pick(p.lines, ctx)
        else:
            fit = [l for l in p.lines if l and era_fits(l, ctx)] or p.lines
            tf = [l for l in fit if tech_fits(l, ctx)] or fit
            d = {l: 1.0 / len(tf) for l in tf} if tf else {}
        effs.append(effective(d) if d else 0.0)
        reach.append(sum(1 for v in d.values() if v > 0.001) if d else 0)
    effs.sort()
    return sum(effs) / len(effs), effs[len(effs) // 10], sum(reach) / len(reach)


def main():
    args = sys.argv[1:]
    top, rx, show_all, csv = 60, None, False, False
    i = 0
    while i < len(args):
        if args[i] == "--top":
            top = int(args[i + 1])
            i += 1
        elif args[i] == "--filter":
            rx = re.compile(args[i + 1])
            i += 1
        elif args[i] == "--all":
            show_all = True
        elif args[i] == "--csv":
            csv = True
        i += 1
    pools = parse_defs(".")
    make_speakers()
    estimate(pools)
    rng = random.Random(7)
    rows = []
    for p in pools.values():
        if rx and not rx.search(p.name):
            continue
        if p.count() == 0:
            continue
        samples = 150 if p.kind != "grammar" else 1
        eff, eff10, reach = variety(p, samples, rng)
        draws = p.draws or 0
        per_line = draws / eff if eff > 0 else float("inf")
        tones = ""
        if p.kind == "toned":
            tones = "/".join(str(len(p.tones.get(t, []))) for t in TONES)
        elif p.kind == "grammar":
            root, shapes, subs = grammar_variety(p)
            tones = f"{root} {shapes} + {subs} sub"
        rows.append((per_line, p, eff, eff10, reach, draws, tones))
    rows.sort(key=lambda r: -r[0])
    if not show_all:
        rows = [r for r in rows if r[5] >= 2]
    if csv:
        print("pool,kind,file,lines,detail,effective,eff10,reach,draws_per_year,seen_per_line_per_year,basis")
        for per_line, p, eff, eff10, reach, draws, tones in rows:
            print(f"{p.name},{p.kind},{p.file},{p.count()},{tones},{eff:.1f},{eff10:.1f},{reach:.1f},{draws:.1f},{per_line:.2f},\"{p.basis}\"")
        return
    print(f"{len(pools)} pools, {sum(p.count() for p in pools.values())} lines. A year is {YEAR_DAYS} days, {COLONISTS} colonists.")
    print("seen/line = draws a year / effective lines: how many times a year a typical line comes round again.")
    print("toned detail = lines per tone (plain/warm/blunt/cheerful/gloomy/cold); grammar detail = root shapes + sub-rule lines.\n")
    print(f"{'seen/line':>9} {'draws/yr':>8} {'eff':>5} {'eff10':>5} {'reach':>5} {'lines':>5}  {'pool':52} detail")
    for per_line, p, eff, eff10, reach, draws, tones in rows[:top]:
        # Seen about daily (50+ a year) wants 15 or more lines within a speaker's reach, about weekly (8+) wants 8 or
        # more, and either wants picks that aren't piled onto one or two lines.
        flag = " <- thin" if (draws >= 50 and reach < 15) or (draws >= 8 and (reach < 8 or eff < 3)) else ""
        name = p.name.replace(".logRulesInitiator.rulesStrings", "").replace(".rulePack.rulesStrings", "")
        print(f"{per_line:9.2f} {draws:8.1f} {eff:5.1f} {eff10:5.1f} {reach:5.1f} {p.count():5d}  {name[:52]:52} {tones}{flag}")


if __name__ == "__main__":
    main()
