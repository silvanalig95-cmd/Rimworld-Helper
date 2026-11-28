#!/usr/bin/env python3
"""
The gods' impact audit: which of the gods' happenings, the council's questions about them and the gods' events change the
game, and which are only a mood and words (docs/review-gods.md; the earlier reviews' Tools/impact_audit.py and
Tools/world_impact_audit.py are its models).

It reads four parts:

  happenings  every OwnHappening (Gods/OnMap/OwnHappeningsMath.cs): its code (a Between kind's class by the happening's
              name; else the methods of the god's own file that note it as come, and those named for it, followed three
              calls deep by impact_audit's reading of the game's calls), sorted into what the game keeps (items, health,
              needs, skills, work, animals, plants and the land, the weather, buildings, people, goodwill, threats,
              relations), a play-out on the map, Rimfolk's own numbers (a god's hidden favour, a colonist's belief, the
              book) and flavour (a mood, words). What its witness saw (its CC_God*_Saw_ key) adds what follows it: its own
              effect in Gods/Touch/GodImpactMath.Table, an effect of its own code (GodTouchMath.OwnEffectKeys), or else one
              of its god's touch effects (GodTouchMath.Table), which is real but random.
  questions   the council's questions of the gods and the faiths (every DilemmaDef in Defs/GodDefs, and those elsewhere whose
              words are of a god, a shrine, an offering, a prayer or a vow), and the gods' events with answers (an IncidentDef
              with a HappeningExtension in Defs/GodDefs), read answer by answer as impact_audit reads them.
  touch       each god's touch effects (GodTouchMath.Table) by kind: what follows an occurrence with nothing of its own.
  unseen      the gods' words in Languages/English/Keyed: a card's or message's text that names a god ({GOD} outside a title
              or a line tagged named), or words of a god's mind (favour, pleased, wants) outside the book's own and the
              faiths' texts; a hint to read by hand.
  signs       each sign (OmenKind): whose it is (the gods' shapes) and what follows it.

Usage: python3 Tools/gods_impact_audit.py [--print happenings|thin|flavour|signs|questions|touch|unseen] [--write] [--root DIR]
  (no flags: the counts; --write rewrites docs/review-gods.md's measured part between its markers; --root measures another
  copy, an older commit's Source and Defs, for the counts before)
"""
import glob
import importlib.util
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ROOT = HERE
if "--root" in sys.argv:
    ROOT = os.path.abspath(sys.argv[sys.argv.index("--root") + 1])
OUT = os.path.join(HERE, "docs", "review-gods.md")
BEGIN = "<!-- gods_impact_audit: begin -->"
END = "<!-- gods_impact_audit: end -->"


def load_impact():
    spec = importlib.util.spec_from_file_location("impact_audit", os.path.join(HERE, "Tools", "impact_audit.py"))
    mod = importlib.util.module_from_spec(spec)
    saved = sys.argv
    sys.argv = [saved[0]]
    try:
        spec.loader.exec_module(mod)
    finally:
        sys.argv = saved
    # Read another copy where one is named.
    mod.ROOT = ROOT
    mod.SRC = os.path.join(ROOT, "Source", "ColonyCouncil")
    return mod


ia = load_impact()
SRC = os.path.join(ROOT, "Source", "ColonyCouncil")

# ---------------- The gods' own readings, beside impact_audit's ----------------

GODS_CALLS = [
    ("state", r"\bGods\.(AddFavour|Remember|Moment)\(|\bPawnGods\.\w+\(|\bGodNear\.\w+\(|\bGodLore\.\w+\(|\bWorship\.Book\w*\(|\bGodTemper\.\w+\("),
    # How a happening between two ends (OwnBetween.End): their memories of each other in the game's opinions, a quarrel let go.
    ("relations", r"\bOwnBetween\.End\("),
    ("mood", r"\bOwnBetween\.Proud\(|\bOwnHappenings\.Memory\(|\bOwnFeasts\.Thought\(|\bThought\("),
    ("map", r"\bOwnHappenings\.(GoTo|Stand|Carry|Job)\(|\bOwnBetween\.(Send|Face)\(|(?<![.\w])(GoTo|Stand|Carry|Send|Face|Lead)\("),
    ("plants", r"\bPlantDef|ThingDefOf\.Plant_|\.Growth\s*[-+]?=|fertilityGrid|GenSpawn\.Spawn\([^;]*Plant|\bSpawnPlant\w*\("),
    ("weather", r"\bweatherManager\.|gameConditionManager\.Register|snowGrid\.|\bGameConditionMaker\.|mapTemperature"),
    ("animals", r"\bPawnGenerator\.GeneratePawn\([^;]*Animal|\.RaceProps\.Animal[^;]*Tame|\bMakeTame\b"),
    ("buildings", r"\bbreakdownableComp|CompBreakdownable|\.HitPoints\s*[-+]?=|CompRefuelable|CompPowerBattery|\.Refuel\("),
    ("touch", r"\bGodTouch\.(Effect|Follow|Do|Apply)\w*\(|\bGodImpact\.\w+\("),
]
GODS_CALLS = [(k, re.compile(p)) for k, p in GODS_CALLS]

GAME = ["items", "goodwill", "skill", "health", "needs", "work", "research", "relations", "people", "animals", "buildings", "threat", "plants", "weather"]
TIER = {k: "game" for k in GAME}
TIER.update({"touch": "game", "later": "later", "map": "map", "state": "state", "mood": "flavour", "words": "flavour", "journal": "flavour",
             "chronicle": "flavour", "gate": "none", "nothing": "none", "unknown": "none"})

# What each of the gods' touch effects does (Gods/Touch/GodTouchEffects.cs and GodImpact.cs), by the game's kinds.
EFFECT_KIND = {
    "Bless": "health", "Learn": "skill", "Bloom": "plants", "Refuel": "buildings", "Mend": "buildings", "Charge": "buildings",
    "ClearSky": "weather", "Snow": "weather", "Rain": "weather", "RatsGone": "animals", "Inspire": "needs", "Find": "items",
    "Ripen": "plants", "Healroot": "plants", "Game": "animals", "RichSoil": "plants", "Visitors": "people", "Goodwill": "goodwill",
    "Trader": "people", "Aurora": "weather", "SkyGift": "items", "Preserve": "items", "ColdBreak": "weather", "Heal": "health",
    "CatComes": "animals", "Fish": "items", "Linger": "weather", "Vein": "items", "Herd": "animals", "Fertile": "plants",
    "Wanderer": "people", "Hardy": "health", "SunYear": "plants", "ScarGone": "health", "Ward": "health", "Spring": "plants",
    "Hoard": "items", "Brambles": "plants", "FiresOut": "buildings", "GameLeaves": "animals", "Spoil": "items",
    "Breakdown": "buildings", "Fog": "weather", "Vanish": "items", "Sleepless": "needs", "Dim": "weather", "Merry": "needs",
    "Sour": "items", "RotAway": "items", "RotGone": "items", "GriefEased": "needs", "KeepsLess": "items", "RestKin": "needs",
    "FrostLine": "plants", "ColdRoom": "weather", "Rested": "needs", "LongNoon": "plants", "Warmth": "weather",
    "NightWard": "threat", "MoonSleep": "needs", "PainEased": "health", "HealFaster": "health", "BingeEnds": "needs",
    "Drink": "items", "Spill": "items", "Cheered": "needs", "Coin": "items", "Silver": "items", "StaysLit": "buildings",
    "FreeHour": "buildings", "StoneChunks": "items", "DigFaster": "work", "WarmGround": "plants", "StillAir": "weather",
    "AnimaHum": "plants", "WithYoung": "animals", "AnimalsCalm": "animals", "StaysDark": "buildings", "KindDream": "needs",
    "SameDream": "relations", "StillHour": "work", "Admired": "items",
}


def read(rel):
    p = os.path.join(SRC, rel)
    if not os.path.exists(p):
        return ""
    with open(p, encoding="utf-8-sig") as fh:
        return fh.read()


def own_effect_keys():
    s = read("Gods/Touch/GodTouchMath.cs")
    m = re.search(r"OwnEffectKeys\s*=\s*\{(.*?)\};", s, re.S)
    body = re.sub(r"//[^\n]*", "", m.group(1)) if m else ""
    return re.findall(r'"([^"]+)"', body)


def impact_table():
    s = read("Gods/Touch/GodImpactMath.cs")
    return {k: e for k, e in re.findall(r'\["([^"]+)"\]\s*=\s*[SND]\(TouchEffect\.(\w+)', s)}


def touch_table():
    s = read("Gods/Touch/GodTouchMath.cs")
    m = re.search(r"Dictionary<string, TouchEntry\[\]> Table = new Dictionary<string, TouchEntry\[\]>\s*\{(.*?)\};", s, re.S)
    out = {}
    if not m:
        return out
    for god, body in re.findall(r'\["(\w+)"\]\s*=\s*new\[\]\s*\{([^}]*)\}', m.group(1)):
        out[god] = [(t, e) for t, e in re.findall(r"\b([SNGD])\(TouchEffect\.(\w+)", body)]
    return out


OWN_KEYS = own_effect_keys()
IMPACT = impact_table()
TOUCH = touch_table()


def in_own_keys(key):
    if key in IMPACT:
        return True
    for k in OWN_KEYS:
        if key == k or (k.endswith("_") and key.startswith(k)):
            return True
    return key.startswith("CC_GodO_Saw_Test")


def happenings():
    s = read("Gods/OnMap/OwnHappeningsMath.cs")
    m = re.search(r"public enum OwnHappening\s*\{(.*?)\n    \}", s, re.S)
    body = re.sub(r"///[^\n]*|//[^\n]*", "", m.group(1))
    names = [x.strip() for x in body.split(",") if x.strip()]
    gods = re.search(r'string\[\] Gods = \{([^}]*)\}', s).group(1)
    gods = re.findall(r'"(\w+)"', gods)
    out = []
    for n in names:
        g = next((x for x in gods if n.startswith(x)), None)
        out.append((n, g if g else ("any" if n.startswith("Faith") else None)))
    return out


def words(name):
    return [w for w in re.findall(r"[A-Z][a-z]+", name)]


SKIP_FILES = ("Math.cs", "Debug.cs")
# The words keys of what was seen, where they aren't named for the happening (read by hand): the edge walk's frost, the vigil's
# thaw, the tending, the tests, the moon's returns, the rain sung for, the cup shared.
ALIAS = {"HushEdge": ["Walk"], "HushVigil": ["Thaw"], "BellwetherHurt": ["Tended"], "EnnuTest": ["Test", "Stranger"], "EnnuTales": ["Tale"],
         "SeleneMorning": ["Return"], "MurmurSong": ["Rain", "Song"], "DucatPurse": ["Purse"], "RevelCup": ["Cup", "Shared"],
         "SmoulderBargain": ["Bargain", "Vein"], "MorelRing": ["Ring"], "HeliosFruit": ["Fruit"]}
# The methods that play a happening out, where they aren't named for it: the bargain asked, answered, carried down and paid
# in gold; the god's cup taken and what it does.
METHODS = {"SmoulderBargain": ["Ask", "Answered", "CarryStep", "Vein", "Seam"], "RevelCup": ["Taken", "After", "Give", "Inspire", "Again", "Follow"]}
DISPATCH = {"Hourly", "Daily", "Step", "Seven", "SevenSteps", "Steps", "Fire", "Due", "Free", "Came"}


class Gods:
    def __init__(self):
        self.code = ia.Code()
        # Raw (unstripped) text of each method, for the words keys it names.
        self.raw = {}
        for path in sorted(glob.glob(os.path.join(SRC, "Gods", "**", "*.cs"), recursive=True)):
            if path.endswith(SKIP_FILES):
                continue
            with open(path, encoding="utf-8-sig") as fh:
                raw = fh.read()
            rel = os.path.relpath(path, SRC)
            self.raw[rel] = raw

    def classes_in(self, rel_prefix):
        return {n: c for n, c in self.code.classes.items() if c["file"].replace(os.sep, "/").startswith("Source/ColonyCouncil/" + rel_prefix)}

    def code_of(self, h, god):
        """The methods that are a happening's own: (class, [bodies]) pairs, and the raw text they come from."""
        found = []
        cls = self.code.classes.get(h)
        if cls and cls["base"] == "BetweenKind":
            for mname, bodies in cls["methods"].items():
                if mname not in ("Fire",):
                    found += [(h, b) for b in bodies]
            return found
        name = h[len(god):] if god and god != "any" else h
        stems = [w for w in words(name) if len(w) >= 3] + METHODS.get(h, [])
        files = set()
        for cname, c in self.code.classes.items():
            f = c["file"].replace(os.sep, "/")
            if "/Gods/" not in f or f.endswith(SKIP_FILES):
                continue
            for mname, bodies in c["methods"].items():
                for b in bodies:
                    if re.search(r"\bCame\([^;]*OwnHappening\." + h + r"\b", b):
                        found.append((cname, b))
                        files.add(c["file"])
        # The steps that play it out after: the methods of the same file named for it (Fruit: BeginFruit, FruitStep).
        for cname, c in self.code.classes.items():
            if c["file"] not in files:
                continue
            for mname, bodies in c["methods"].items():
                if mname in DISPATCH or not any(st in mname for st in stems):
                    continue
                for b in bodies:
                    if (cname, b) not in found:
                        found.append((cname, b))
        return found

    def keys_of(self, h, god, bodies):
        """The words keys of what its witness saw (CC_God*_Saw_*), read from the raw file text of its methods."""
        keys = set()
        if self.code.classes.get(h, {}).get("base") == "BetweenKind":
            return {"CC_GodB_Saw_" + h}
        files = {self.code.classes[c]["file"] for c, _ in bodies if c in self.code.classes}
        name = h[len(god):] if god and god != "any" else h
        stems = [w for w in words(name) if len(w) >= 3] + ALIAS.get(h, [])
        for f in files:
            raw = self.raw.get(os.path.relpath(os.path.join(ROOT, f), SRC), "")
            for k in re.findall(r'"(CC_God\w*_Saw_\w+)"', raw):
                stem = k.split("_Saw_")[1]
                if any(w in stem for w in stems) or stem.startswith(name[:5]):
                    keys.add(k)
        return keys

    def kinds(self, bodies):
        kinds = set()
        for cname, b in bodies:
            kinds |= self.code.kinds_of_text(cname, b, 3, set())
            for k, rx in GODS_CALLS:
                if rx.search(b):
                    kinds.add(k)
        # In the gods' code a thing "remembered" is the gods' own memory (Gods.Remember), not a follow-up booked: their state.
        if "later" in kinds:
            kinds.discard("later")
            kinds.add("state")
        return kinds


class Happening:
    def __init__(self, name, god, kinds, keys, nbodies):
        self.name = name
        self.god = god
        self.kinds = kinds
        self.keys = sorted(keys)
        self.nbodies = nbodies
        self.follow = []  # what follows its witness's sight: ("impact", effect) / ("own", key) / ("touch", god)
        for k in self.keys:
            if k in IMPACT:
                self.follow.append(("impact", IMPACT[k]))
            elif in_own_keys(k):
                self.follow.append(("own", k))
            else:
                self.follow.append(("touch", god))

    @property
    def follow_kinds(self):
        out = set()
        for how, what in self.follow:
            if how == "impact":
                out.add(EFFECT_KIND.get(what, "touch"))
            elif how == "touch":
                out.add("touch")
        return out

    @property
    def all_kinds(self):
        return (self.kinds | self.follow_kinds) - {"gate", "nothing", "unknown", "journal"}

    @property
    def tiers(self):
        return {TIER.get(k, "game") for k in self.all_kinds} - {"none"}

    @property
    def thin(self):
        return self.tiers <= {"flavour", "state"}

    @property
    def flavour_only(self):
        return self.tiers <= {"flavour"}

    @property
    def between(self):
        return self.nbodies and self.keys == ["CC_GodB_Saw_" + self.name]


def measure_happenings(g):
    out = []
    for h, god in happenings():
        bodies = g.code_of(h, god)
        kinds = g.kinds(bodies)
        keys = g.keys_of(h, god, bodies)
        out.append(Happening(h, god, kinds, keys, len(bodies)))
    return out


# ---------------- The signs ----------------

def measure_signs():
    """Each sign (OmenKind): whose it is (the gods' shapes), and what follows it (its own effect, or the touch's)."""
    s = read("Gods/OnMap/GodsOnMapMath.cs")
    m = re.search(r"public enum OmenKind\s*\{(.*?)\n    \}", s, re.S)
    kinds = [x.strip() for x in re.sub(r"///[^\n]*|//[^\n]*", "", m.group(1)).split(",") if x.strip()] if m else []
    owners = defaultdict(list)
    path = os.path.join(ROOT, "Defs", "GodDefs", "Rimfolk_GodsOnMap_Shapes.xml")
    if os.path.exists(path):
        for d in ET.parse(path).getroot():
            if d.tag != "ColonyCouncil.GodShapeDef":
                continue
            god = (d.findtext("defName") or "").replace("Rimfolk_GodShape_", "")
            for li in d.findall("omens/li"):
                owners[li.text].append(god)
    out = []
    for k in kinds:
        key = "CC_GodM_Saw_" + k
        follow = ("impact", IMPACT[key]) if key in IMPACT else ("own", key) if in_own_keys(key) else ("touch", None)
        out.append((k, owners.get(k, []), follow))
    return out


# ---------------- The council's questions and the gods' events ----------------

GOD_WORDS = re.compile(r"\{GOD|\bgods?\b|\bshrine|\baltar|\bofferings?\b|\bprayers?\b|\bpray(s|ed|ing)?\b|\bvows?\b|\bsaints?\b|\bpriest", re.I)


def god_def(path, d):
    if "GodDefs" in path:
        return True
    text = " ".join((x.text or "") for x in d.iter() if x.tag in ("label", "text", "description", "outcome"))
    return bool(GOD_WORDS.search(text)) or bool(re.search(r"God|Faith|Worship|Shrine|Saint|Prayer|Vow|Offering", d.findtext("defName") or ""))


def measure_questions():
    code = ia.Code()
    answers = []
    groups = {}
    for path in sorted(glob.glob(os.path.join(ROOT, "Defs", "**", "*.xml"), recursive=True)):
        try:
            tree = ET.parse(path)
        except ET.ParseError:
            continue
        rel = os.path.relpath(path, ROOT)
        for d in tree.getroot():
            defname = ia.text_of(d.find("defName"))
            if d.tag == "ColonyCouncil.DilemmaDef" and god_def(rel, d):
                groups[("question", defname)] = {"path": rel, "label": ia.text_of(d.find("label")), "letter": set(), "el": d}
                for i, o in enumerate(ia.lis(d, "options")):
                    answers.append(ia.make_answer(code, "question", defname, i, o, set(), rel))
            elif d.tag == "IncidentDef" and "GodDefs" in rel:
                ext = next((li for li in ia.lis(d, "modExtensions") if ia.cls_of(li) == "HappeningExtension"), None)
                if ext is None:
                    continue
                worker = ia.text_of(d.find("workerClass")).split(".")[-1]
                res = code.resolved(worker) if worker else None
                groups[("event", defname)] = {"path": rel, "label": ia.text_of(d.find("label")), "letter": set(), "el": ext}
                for i, o in enumerate(ia.lis(ext, "options")):
                    answers.append(ia.make_answer(code, "event", defname, i, o, ia.resolved_for(res, i), rel))
    by = defaultdict(list)
    for a in answers:
        by[(a.source, a.defname)].append(a)
    res = {"answers": answers, "groups": groups, "by_group": by}
    res["flavour"] = [a for a in answers if a.flavour_only and not a.empty]
    res["thin"] = [a for a in answers if a.thin and not a.flavour_only and not a.empty]
    res["empty"] = [a for a in answers if a.empty]
    res["allthin"] = [k for k, v in by.items() if v and all(a.thin or a.empty for a in v)]
    res["promises"] = [(a, p) for a in answers for p in ia.promises(a)]
    return res


# ---------------- The unseen rule in the words ----------------

MIND = re.compile(r"\b(favou?r(ed|s)?|pleased with (the|us|you)|displeased|wants? (the colony|us|you) to|is angry with|loves the colony|hates the colony)\b", re.I)
# The book's own and the faiths' texts may say what people hold (in their words, so the keeper's); these files and keys are theirs.
MIND_OK = re.compile(r"GodBook|_Book_|_Saying|_Prayer|_Sermon|_Tale|_Verse|_Temper|Rimfolk_GodBook|Journal|_Keeper|_Lore", re.I)


def keyed_files():
    d = os.path.join(ROOT, "Languages", "English", "Keyed")
    pat = re.compile(r"God|Faith|Worship|Saint|Belief|OldPowers|Between", re.I)
    return [p for p in sorted(glob.glob(os.path.join(d, "*.xml"))) if pat.search(os.path.basename(p))]


def measure_unseen():
    named, mind = [], []
    for p in keyed_files():
        try:
            root = ET.parse(p).getroot()
        except ET.ParseError:
            continue
        for e in root:
            key, text = e.tag, (e.text or "")
            # A card's or message's text that names the god: the title may (where the colony knows), the text never.
            if re.search(r"_Text(_\d+)?$|_Message$|_Msg$", key) and re.search(r"\{GOD(NAME)?\}", text):
                named.append((os.path.basename(p), key, text))
            if MIND.search(text) and not MIND_OK.search(key) and not MIND_OK.search(os.path.basename(p)):
                mind.append((os.path.basename(p), key, text))
    return {"named": named, "mind": mind}


# ---------------- The report ----------------

def short(s, n=100):
    s = re.sub(r"\s+", " ", s or "").replace("|", "/").strip()
    return s if len(s) <= n else s[:n - 3] + "..."


def follow_text(h):
    parts = []
    for how, what in h.follow:
        if how == "impact":
            parts.append("its own: " + what)
        elif how == "own":
            parts.append("its code's own")
        else:
            parts.append("a touch of " + str(what))
    return "; ".join(sorted(set(parts)))


def counts(hs, qs, un, ss=None):
    land = [h for h in hs if not h.between]
    betw = [h for h in hs if h.between]
    L = []
    L.append("| Part | Count | With a game effect | Thin (moods, belief, words) | Flavour only |")
    L.append("|---|---|---|---|---|")
    for name, xs in (("The gods' happenings on the land", land), ("The gods between the colony's people", betw)):
        L.append("| %s | %d | %d | %d | %d |" % (name, len(xs), sum(1 for h in xs if not h.thin), sum(1 for h in xs if h.thin and not h.flavour_only),
                                              sum(1 for h in xs if h.flavour_only)))
    if ss is not None:
        # A sign is seen and said; what follows it is its own effect, or else one of its god's touch (real, but random).
        L.append("| The gods' signs (%d with an effect of their own, %d followed by the touch) | %d | %d | 0 | 0 |" % (
            sum(1 for s in ss if s[2][0] != "touch"), sum(1 for s in ss if s[2][0] == "touch"), len(ss), len(ss)))
    qa = [a for a in qs["answers"] if a.source == "question"]
    ea = [a for a in qs["answers"] if a.source == "event"]
    for name, xs, src in (("The council's questions of the gods (answers)", qa, "question"), ("The gods' events with answers (answers)", ea, "event")):
        L.append("| %s | %d | %d | %d | %d |" % (name, len(xs), sum(1 for a in xs if not a.thin and not a.empty),
                                              sum(1 for a in qs["thin"] if a.source == src), sum(1 for a in qs["flavour"] if a.source == src)))
    L.append("")
    L.append("Questions and events: %d; whose every answer is thin: %d. Answers with no effect at all: %d. Words that promise more: %d." % (
        len(qs["groups"]), len(qs["allthin"]), len(qs["empty"]), len(qs["promises"])))
    L.append("The unseen rule in the words: %d card or message texts that name a god; %d lines with words of a god's mind outside the book and the faiths' texts." % (
        len(un["named"]), len(un["mind"])))
    return L


def report(hs, qs, un, ss):
    L = [BEGIN, "", "Measured by `python3 Tools/gods_impact_audit.py --write` (rewritten each run; the review's own notes are outside the markers).", ""]
    L += counts(hs, qs, un, ss)
    L.append("")
    kc = Counter(k for h in hs for k in h.all_kinds)
    L.append("What the happenings carry (happenings with at least one of the kind): " + ", ".join("%s %d" % (k, kc[k]) for k in GAME + ["touch", "map", "state", "mood", "words"] if kc[k]) + ".")
    L.append("")
    L.append("### Happenings that are thin (moods, belief and words only)")
    L.append("")
    thin = [h for h in hs if h.thin]
    if thin:
        L.append("| Happening | God | Carries | What follows its sight |")
        L.append("|---|---|---|---|")
        for h in thin:
            L.append("| `%s` | %s | %s | %s |" % (h.name, h.god, ", ".join(sorted(h.all_kinds)) or "nothing found", follow_text(h) or "nothing"))
    else:
        L.append("None.")
    L.append("")
    L.append("### Each happening")
    L.append("")
    L.append("| Happening | Carries | What follows its sight |")
    L.append("|---|---|---|")
    for h in hs:
        L.append("| `%s` | %s | %s |" % (h.name, ", ".join(sorted(h.all_kinds)) or "nothing found", follow_text(h) or "nothing"))
    L.append("")
    L.append("### The council's questions and the gods' events: thin answers")
    L.append("")
    rows = qs["flavour"] + qs["thin"]
    if rows:
        L.append("| Def | # | Answer | Carries |")
        L.append("|---|---|---|---|")
        for a in sorted(rows, key=lambda a: (a.defname, a.index)):
            L.append("| `%s` | %d | %s | %s |" % (a.defname, a.index + 1, short(a.label, 60), ia.kinds_text(a)))
    else:
        L.append("None.")
    L.append("")
    L.append("### The signs: whose, and what follows each")
    L.append("")
    L.append("| Sign | Whose (the gods' shapes) | What follows its sight |")
    L.append("|---|---|---|")
    for k, owners, (how, what) in ss:
        L.append("| `%s` | %s | %s |" % (k, ", ".join(owners) or "any god's", ("its own: " + what) if how == "impact" else "its code's own" if how == "own" else "a touch of its god"))
    L.append("")
    L.append("### The touch: each god's effects by kind")
    L.append("")
    for god, entries in TOUCH.items():
        L.append("- %s: %s" % (god, ", ".join("%s%s (%s)" % (e, " dark" if t == "D" else "", EFFECT_KIND.get(e, "?")) for t, e in entries)))
    L.append("")
    L.append("### The unseen rule in the words (to read by hand)")
    L.append("")
    if un["named"] or un["mind"]:
        for f, k, t in un["named"]:
            L.append("- names a god in a card's text: `%s` (%s): %s" % (k, f, short(t, 90)))
        for f, k, t in un["mind"]:
            L.append("- a god's mind: `%s` (%s): %s" % (k, f, short(t, 90)))
    else:
        L.append("None.")
    L.append("")
    L.append(END)
    return L


def write(lines):
    if os.path.exists(OUT):
        with open(OUT, encoding="utf-8") as fh:
            old = fh.read()
        if BEGIN in old and END in old:
            new = old[:old.index(BEGIN)] + "\n".join(lines) + old[old.index(END) + len(END):]
        else:
            new = old.rstrip() + "\n\n" + "\n".join(lines) + "\n"
    else:
        new = "# Review of the gods\n\n" + "\n".join(lines) + "\n"
    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write(new)


def main():
    args = sys.argv[1:]
    g = Gods()
    hs = measure_happenings(g)
    qs = measure_questions()
    un = measure_unseen()
    ss = measure_signs()
    for line in counts(hs, qs, un, ss):
        print(line)
    if "--print" in args:
        what = args[args.index("--print") + 1]
        if what in ("happenings", "thin", "flavour"):
            for h in hs:
                if what == "thin" and not h.thin or what == "flavour" and not h.flavour_only:
                    continue
                print("%-22s %-10s %-3d %-60s %s" % (h.name, h.god, h.nbodies, ", ".join(sorted(h.all_kinds)), follow_text(h)))
        elif what == "questions":
            for a in sorted(qs["answers"], key=lambda a: (a.defname, a.index)):
                print("%-8s %-45s %d %-45s %s%s" % (a.source, a.defname, a.index + 1, short(a.label, 45), ia.kinds_text(a), "  THIN" if a.thin else ""))
            for a, (n, s) in qs["promises"]:
                print("promise:", a.defname, a.index + 1, n, short(s, 100))
        elif what == "touch":
            for god, entries in TOUCH.items():
                print(god, entries)
        elif what == "unseen":
            for f, k, t in un["named"]:
                print("named:", f, k, short(t, 120))
            for f, k, t in un["mind"]:
                print("mind:", f, k, short(t, 120))
        elif what == "signs":
            for k, owners, follow in ss:
                print("%-14s %-30s %s" % (k, ",".join(owners), follow))
    if "--write" in args:
        write(report(hs, qs, un, ss))
        print("Wrote", os.path.relpath(OUT, HERE))


if __name__ == "__main__":
    main()
