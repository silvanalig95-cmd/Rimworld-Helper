#!/usr/bin/env python3
"""
Impact audit: which answers of Rimfolk's map events and the council's questions change the game, and which are flavour?

Walks every IncidentDef with a HappeningExtension (Rimfolk's events on the map; the personal stories', missions' and
storylines' own cards are left to their frames) and every DilemmaDef (the council's questions), and for each answer lists
the effect classes it carries (its effects, its risk effects, and effects nested in them), sorted into:

  game      what the game itself keeps: items and silver (spawned, spent, carried out, paid), goodwill, skills (xp, a
            lesson, a mentor), health (a hediff, an illness eased, an injury, a fight, a mental state), needs (recreation,
            rest, an inspiration, strain), work (a work party, hours, a day off, bills), research, relations (a bond,
            the social web), people (someone joins or leaves, a prisoner's order), animals, buildings (a designation, a
            blueprint, a stockpile), threats (a raid later).
  later     a follow-up booked by the answer's code (an order the neighbours collect, a visit, a memo another event
            reads): real, though it comes later.
  map       a play-out on the map: people walk somewhere and do what the words say (a gathering, a visit, words, a vigil,
            a visitor sent off; Tools/playout_audit.py's list and any effect whose code gives jobs or makes a lord).
  state     Rimfolk's own numbers about the colony: the council's trust, standards, the colony's ways, a faith's
            certainty, the gods' temper, a camp's tension, a faction's memory of a deed, a storyline's turn.
  flavour   only a mood, only words (a message, a bubble), a journal line, a chronicle line.

An effect class the audit has no fixed reading for is read from its code: the Apply method and what it calls (to a
depth of three), matched against the game calls that do each thing (GenSpawn, TryAffectGoodwillWith, skills.Learn,
health.AddHediff, JobMaker, LordMaker, designationManager, researchManager...). An event's worker that overrides
Resolved adds what its code does to the answers it names (option == N, case N), or to every answer when it names none.
Code that changes something the scan has no word for (a tracker's field, a memo kept) counts as Rimfolk's own state, so a
"thin" answer is a hint to read its code, as an "empty" one is.

It reports:
  * flavour-only answers (only flavour) and thin answers (only flavour and state: moods and trust, nothing the game
    keeps and nothing on the map);
  * events and questions whose every answer is flavour-only, or thin;
  * answers whose words promise something their effects don't do (silver, a gift, a meal, a lesson, goodwill, someone
    joining, a mending, a hunt, a healing...): a word list, so a hint to read by hand, not a verdict;
  * answers that do nothing at all (no effect and no worker code);
  * questions and events where one answer is plainly best or worse in every way (Tools/dilemma_balance.py's measure:
    dominated or easy).

Usage: python3 Tools/impact_audit.py [--write] [--print KIND] [--filter REGEX]
  (no flags: the counts; --write writes docs/review-events-council.md's measured part between its markers, or the
  whole file when it doesn't exist; --print flavour|thin|allflavour|allthin|promises|empty|balance|kinds lists one
  part; --filter keeps the defs whose name matches)
"""
import glob
import importlib.util
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "Source", "ColonyCouncil")
OUT = os.path.join(ROOT, "docs", "review-events-council.md")
BEGIN = "<!-- impact_audit: begin -->"
END = "<!-- impact_audit: end -->"


def load_tool(name):
    spec = importlib.util.spec_from_file_location(name, os.path.join(ROOT, "Tools", name + ".py"))
    mod = importlib.util.module_from_spec(spec)
    saved = sys.argv
    sys.argv = [saved[0]]
    try:
        spec.loader.exec_module(mod)
    finally:
        sys.argv = saved
    return mod


playout = load_tool("playout_audit")
balance = load_tool("dilemma_balance")

# ---------------- What each effect class does ----------------

GAME = ["items", "goodwill", "skill", "health", "needs", "work", "research", "relations", "people", "animals", "buildings", "threat"]
TIERS = {k: "game" for k in GAME}
TIERS.update({"later": "later", "map": "map", "state": "state", "mood": "flavour", "words": "flavour", "journal": "flavour", "chronicle": "flavour",
              "gate": "none", "nothing": "none"})

# A fixed reading for the classes used most, and for those whose code the scan would read wrongly.
FIXED = {
    "DilemmaEffect_Thought": {"mood"},
    "DilemmaEffect_Trust": {"state"},
    "DilemmaEffect_Standards": {"state"},
    "DilemmaEffect_Remember": {"state"},
    "DilemmaEffect_CampTension": {"state"},
    "DilemmaEffect_GodFavour": {"state"},
    "DilemmaEffect_StorylineChoice": {"state"},
    "DilemmaEffect_Faith": {"state"},
    "DilemmaEffect_Needs": {"gate"},
    "DilemmaEffect_HarmRisk": {"gate"},
    "DilemmaEffect_EventGoodwill": {"goodwill"},
    "DilemmaEffect_Goodwill": {"goodwill"},
    "DilemmaEffect_FairGoodwill": {"goodwill"},
    "DilemmaEffect_RivalGoodwill": {"goodwill"},
    "WorldEffect_Goodwill": {"goodwill"},
    "DilemmaEffect_Learn": {"skill"},
    "WorldEffect_Learn": {"skill"},
    "DilemmaEffect_Lesson": {"skill", "map"},
    "DilemmaEffect_Mentorship": {"skill"},
    "DilemmaEffect_Teach": {"skill"},
    "DilemmaEffect_Mentor": {"skill"},
    "DilemmaEffect_CarryOut": {"items", "map"},
    "DilemmaEffect_Consume": {"items"},
    "DilemmaEffect_Spawn": {"items"},
    "DilemmaEffect_Loot": {"items"},
    "DilemmaEffect_Gift": {"items"},
    "DilemmaEffect_Goods": {"items"},
    "DilemmaEffect_Paid": {"items"},
    "WorldEffect_Silver": {"items"},
    "DilemmaEffect_Search": {"items", "map"},
    "DilemmaEffect_Felled": {"items"},
    "DilemmaEffect_Feast": {"items", "map"},
    "DilemmaEffect_Hediff": {"health"},
    "DilemmaEffect_EaseIllness": {"health"},
    "DilemmaEffect_MentalState": {"health"},
    "DilemmaEffect_Fight": {"health", "map"},
    "DilemmaEffect_Joy": {"needs"},
    "DilemmaEffect_Inspiration": {"needs"},
    "DilemmaEffect_Strain": {"needs"},
    "DilemmaEffect_StrainRelief": {"needs"},
    "ArcEffect_Strain": {"needs"},
    "DilemmaEffect_Rest": {"work", "needs", "map"},
    "DilemmaEffect_WorkParty": {"work", "map"},
    "DilemmaEffect_Hours": {"work"},
    "DilemmaEffect_Research": {"research"},
    "DilemmaEffect_Tie": {"relations"},
    "ArcEffect_Tie": {"relations"},
    "DilemmaEffect_Join": {"people"},
    "DilemmaEffect_Recruit": {"people"},
    "DilemmaEffect_LeaveForGood": {"people"},
    "DilemmaEffect_LeaveColony": {"people"},
    "DilemmaEffect_HoldBack": {"people"},
    "DilemmaEffect_PrisonerOrder": {"people"},
    "DilemmaEffect_RaidLater": {"threat"},
    "DilemmaEffect_SendAway": {"map"},
}

# Memories that also make or break a bond in the social web (DilemmaEffect_Thought.Apply).
TIE_THOUGHTS = {"Council_Dlm_MadeUp", "Council_Dlm_Bonded", "Council_Dlm_Grudge", "Council_Dlm_Distrust"}

# What the game calls in an effect's code mean. Read on the code with comments and strings taken out.
CALLS = [
    ("items", r"GenSpawn\.|GenPlace\.TryPlace|ThingMaker\.MakeThing|\.SplitOff\(|DropPodUtility|Loot\.Make|Couriers\.(Loot|Send|Bring)|"
              r"TradeUtility\.|\.Destroy\(|stackCount\s*[-+]=|DilemmaEffect_(Consume|Spawn|CarryOut|Loot|Gift)\b|Consume\.(Take|Spend|From)|"
              r"Missions\.(Take|Pay)|Silver\.|ThingDefOf\.Silver|\.TakeFrom|Ingest\(|EatCrops\(|PaySilver|\bPay\w*\(|innerContainer\.TryAdd|\.DeSpawn\("),
    ("goodwill", r"TryAffectGoodwillWith|\bGoodwill\.\w+\(|DilemmaEffect_(Event|Fair|Rival)?Goodwill\b"),
    ("skill", r"\.Learn\w*\(|DilemmaEffect_Learn\b|\.Teach\(|xpPerHour"),
    ("health", r"health\.AddHediff|HediffMaker\.|\bHarm\.\w+\(|\.TakeDamage\(|RemoveHediff|TendUtility|HealthUtility|\.Kill\(|"
               r"TryStartMentalState|mentalStateHandler|\bAilments?\.\w+\(|severity\s*[-+]?=|\.Severity\s*[-+]?="),
    ("needs", r"needs\.(joy|rest|food|comfort|beauty|outdoors|mood)|GainJoy|CurLevel\s*[-+]?=|TryStartInspiration|inspirationHandler|HeartLogic\."),
    ("map", r"JobMaker\.MakeJob|TryTakeOrderedJob|jobs\.(StartJob|TryTakeOrderedJob|EndCurrentJob)|LordMaker\.MakeNewLord|\bPlayOuts?\.\w+\(|"
            r"\bOnMap\w*\.\w+\(|PlayDepart\.|DilemmaEffect_(Visit|Words|Gathering|Procession|Vigil|Toast|Bonfire)\b|Arrivals\.|"
            r"\bWalk\w*\.\w+\(|LordJob_|\.AddVisit\(|\.Stay\(|\bCallers\.Hand\(|\bPlay\w+\.\w+\("),
    ("buildings", r"designationManager|new Designation\(|GenConstruct\.|Blueprint|zoneManager|\.SetFaction\(\s*Faction\.OfPlayer\s*\)\s*;\s*//\s*building|"
                  r"Building\w*\.\w+\(|\.Notify_ColorChanged|roofGrid\.SetRoof|terrainGrid\.SetTerrain|ownership\.Claim"),
    ("animals", r"InteractionWorker_RecruitAttempt\.DoRecruit|\.training\.|playerSettings\.Master|PawnRelationDefOf\.Bond|"
                r"Tame\w*\(|MakeTame|animals?\.\w+\(|playerSettings\.\w+\s*=|\.Name\s*=\s*new Name"),
    ("work", r"workSettings\.SetPriority|timetable\.SetAssignment|\.outfits|drugPolicy|foodRestriction|billStack|"
             r"\bBillUtility\.|\bAnswerShifts?\.\w+\(|AddBill\("),
    ("research", r"researchManager|ResearchProjectDef"),
    ("relations", r"relations\.AddDirectRelation|\bsocial\.(Bump|Weaken|Tie)\b|TieKind\.|Ties\.\w+\("),
    ("people", r"SetFaction\(\s*Faction\.OfPlayer|RecruitUtility|guest\.(SetGuestStatus|Recruitable)|ExitMap\(|\bBanish|"
               r"Recruit\w*\(|\bJoin\w*\(|\.SetFaction\(|guest\.(resistance|will)\b|\bg\.(resistance|will)\s*=|Guests\.Adopt"),
    ("later", r"\.Remember\(|\.AddVisit\(|\bdue\s*=|\.due\s*=|\bLater\w*\(|\.Book\w*\(|\.Schedule\w*\(|GenerateQuest\w*\("),
    ("threat", r"incidentQueue|IncidentDefOf\.Raid|\.TryExecute\(|Find\.Storyteller"),
    ("state", r"standings\.Change|CharacterTracker\.\w+|FactionMemory\.\w+\(|\bFaith\w*\.\w*(Certain|Shift)|Favour\w*\(|tension\s*[-+]?=|"
              r"Tension\w*\(|Storyline\w*\.\w+\(|OffsetCertainty|secrets\.\w+\(|aspirations|Worship\.Book|\.(Add|Set)Mark\("),
    ("mood", r"TryGainMemory|GiveThought|memories\.|DilemmaEffect_Thought\b"),
    ("words", r"\bSpeech\.(Say|Together)|Messages\.Message|LetterStack|\bNotify\.\w+\(|\bTell\.\w+\("),
    ("journal", r"journals\.\w+\(|\bJournal\("),
    ("chronicle", r"AddChronicle|Chronicle\.\w+\("),
]
CALLS = [(k, re.compile(p)) for k, p in CALLS]

# Helpers whose methods the scan does not follow: they hold everything and say nothing about one effect.
NO_FOLLOW = {"CouncilGameComponent", "Flavor", "Find", "Rand", "Mathf", "Log", "GenText", "TextUtil", "DilemmaImpact", "ImpactSheet",
             "Translator", "Speech", "Messages", "Causes", "Mathf", "GenDate", "Scribe_Values", "Scribe_Defs", "Scribe_References",
             "Scribe_Collections", "DefDatabase", "Gen", "GenCollection", "Enumerable", "Math", "String", "Notify", "Journal"}


# ---------------- Reading the code ----------------

STRIP = re.compile(r'//[^\n]*|/\*.*?\*/|@"(?:[^"]|"")*"|\$?"(?:\\.|[^"\\\n])*"|\'(?:\\.|[^\'\\])\'', re.S)
CLASS = re.compile(r"\bclass\s+(\w+)(?:<[^>]*>)?\s*(?::\s*([\w.<>, ]+?))?\s*(?:where [^{]*)?\{")
METHOD = re.compile(r"(?:public|private|protected|internal|static|override|virtual|sealed|abstract|new|\s)+[\w<>\[\],.?]+(?:<[^>]*>)?\s+(\w+)\s*\(([^()]*(?:\([^()]*\)[^()]*)*)\)\s*(\{|=>)")
CALL = re.compile(r"\b([A-Z]\w*)\s*\.\s*([A-Z]\w*)\s*\(")
LOCAL_CALL = re.compile(r"(?<![.\w])([A-Z]\w*)\s*\(")


def strip_code(s):
    return STRIP.sub(lambda m: '""' if m.group(0).startswith(('"', '@', '$')) else ("''" if m.group(0).startswith("'") else " "), s)


def block(s, i):
    """The text from the '{' at i to its matching '}'."""
    depth = 0
    for j in range(i, len(s)):
        c = s[j]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return s[i:j + 1]
    return s[i:]


def expr(s, i):
    """An expression-bodied member: from '=>' at i to the ';' at depth zero."""
    depth = 0
    for j in range(i, len(s)):
        c = s[j]
        if c in "({[":
            depth += 1
        elif c in ")}]":
            depth -= 1
        elif c == ";" and depth <= 0:
            return s[i:j + 1]
    return s[i:]


class Code:
    def __init__(self):
        self.classes = {}  # name -> {"base": str, "body": str, "methods": {name: [body]}, "file": rel}
        for path in sorted(glob.glob(os.path.join(SRC, "**", "*.cs"), recursive=True)):
            with open(path, encoding="utf-8-sig") as fh:
                s = strip_code(fh.read())
            rel = os.path.relpath(path, ROOT)
            for m in CLASS.finditer(s):
                name = m.group(1)
                base = (m.group(2) or "").split(",")[0].strip().split("<")[0].split(".")[-1]
                body = block(s, m.end() - 1)
                methods = defaultdict(list)
                for mm in METHOD.finditer(body):
                    mname = mm.group(1)
                    if mname in ("if", "for", "foreach", "while", "switch", "using", "return", "catch", "lock", "new"):
                        continue
                    start = mm.end() - 1 if mm.group(3) == "{" else mm.end() - 2
                    methods[mname].append(block(body, start) if mm.group(3) == "{" else expr(body, start))
                c = self.classes.setdefault(name, {"base": base, "body": "", "methods": defaultdict(list), "file": rel})
                c["body"] += body
                for k, v in methods.items():
                    c["methods"][k].extend(v)
        self.cache = {}

    def method(self, cls, name, depth=0):
        """A method's bodies, from the class or the nearest base class that has it."""
        seen = 0
        while cls in self.classes and seen < 8:
            c = self.classes[cls]
            if name in c["methods"]:
                return cls, c["methods"][name]
            cls = c["base"]
            seen += 1
        return None, []

    def kinds_of_text(self, cls, text, depth, seen):
        kinds = set()
        for k, rx in CALLS:
            if rx.search(text):
                kinds.add(k)
        # A nested effect used inside (new DilemmaEffect_CarryOut { ... }.Apply): what it does, by its fixed reading or its code.
        for t in set(re.findall(r"new\s+((?:DilemmaEffect|WorldEffect|ArcEffect|VisitEffect)_\w+)", text)):
            if ("new", t) in seen:
                continue
            seen.add(("new", t))
            kinds |= self.effect_kinds(t, max(depth - 1, 0), seen)
        if depth <= 0:
            return kinds
        for t, mname in set(CALL.findall(text)):
            if t in NO_FOLLOW or (t, mname) in seen:
                continue
            seen.add((t, mname))
            owner, bodies = self.method(t, mname)
            for b in bodies:
                kinds |= self.kinds_of_text(owner, b, depth - 1, seen)
        if cls:
            for mname in set(LOCAL_CALL.findall(text)):
                if (cls, mname) in seen:
                    continue
                seen.add((cls, mname))
                owner, bodies = self.method(cls, mname)
                for b in bodies:
                    kinds |= self.kinds_of_text(owner, b, depth - 1, seen)
        return kinds

    def effect_kinds(self, cls, depth=3, seen=None):
        if cls in FIXED:
            return set(FIXED[cls])
        if cls in playout.PLAY:
            # A play-out: what its own Apply does besides (silver paid as the thing is handed over), not what the play-out
            # frame it starts can do for others.
            owner, bodies = self.method(cls, "Apply")
            return {"map"} | {k for b in bodies for k in self.kinds_of_text(None, b, 0, set())}
        key = (cls, depth)
        if seen is None and key in self.cache:
            return self.cache[key]
        seen = seen if seen is not None else set()
        owner, bodies = self.method(cls, "Apply")
        kinds = set()
        for b in bodies:
            kinds |= self.kinds_of_text(owner, b, depth, seen)
        base = self.classes.get(cls, {}).get("base", "")
        if base in FIXED:
            kinds |= FIXED[base]
        elif base in playout.PLAY:
            kinds.add("map")
        if not kinds:
            # Code that changes something the scan has no word for (a tracker's field, a visit booked): Rimfolk's own state.
            busy = any(re.search(r"[^=!<>]=[^=>]|\w\s*\(", re.sub(r"\bif\s*\([^;]*\)\s*return\s*;|return\s*;", "", b)) for b in bodies)
            kinds = ({"state"} if busy else {"nothing"}) if cls in self.classes else {"unknown"}
        self.cache[(cls, depth)] = kinds
        return kinds

    def resolved(self, worker):
        """What an event worker's Resolved does in code, and for which answers: (kinds, {option: kinds} or None for all)."""
        owner, bodies = self.method(worker, "Resolved")
        if not owner or owner in ("IncidentWorker_Happening",):
            return None
        out = {}
        every = set()
        for b in bodies:
            text = re.sub(r"base\.Resolved\([^;]*;|\bJournal\([^;]*;", " ", b)
            kinds = self.kinds_of_text(owner, text, 3, set()) - {"journal"}
            kinds = {k for k in kinds if TIERS.get(k) in ("game", "map", "state", "later")}
            if not kinds:
                # Code the scan has no word for (a memo kept, a tracker's field): Rimfolk's own state.
                rest = re.sub(r"\breturn\s+outcome\s*;|\breturn\s*;|\bvar\s+\w+\s*=[^;]*;", " ", text)
                if not re.search(r"[^=!<>]=[^=>]|\w\s*\(", rest.strip("{} \n\t")):
                    continue
                kinds = {"state"}
            eq = [int(x) for x in re.findall(r"\boption\s*==\s*(\d+)", text)]
            ne = [int(x) for x in re.findall(r"\boption\s*!=\s*(\d+)", text)]
            cases = [int(x) for x in re.findall(r"\bcase\s+(\d+)\s*:", text)] if re.search(r"switch\s*\(\s*option\s*\)", text) else []
            ge = [int(x) + (1 if op == ">" else 0) for op, x in re.findall(r"\boption\s*(>=?)\s*(\d+)", text)]
            le = [int(x) - (1 if op == "<" else 0) for op, x in re.findall(r"\boption\s*(<=?)\s*(\d+)", text)]
            named = eq + cases
            if not named and not ne and not ge and not le:
                every |= kinds
            for i in named:
                out.setdefault(i, set()).update(kinds)
            for i in ne:
                out.setdefault(("not", i), set()).update(kinds)
            for i in ge:
                out.setdefault(("from", i), set()).update(kinds)
            for i in le:
                out.setdefault(("upto", i), set()).update(kinds)
        if not out and not every:
            return None
        return every, out


def resolved_for(res, option):
    if res is None:
        return set()
    every, out = res
    k = set(every)
    for key, kinds in out.items():
        if key == option or (isinstance(key, tuple) and ((key[0] == "not" and key[1] != option) or (key[0] == "from" and option >= key[1]) or (key[0] == "upto" and option <= key[1]))):
            k |= kinds
    return k


# ---------------- Reading the defs ----------------

def text_of(el):
    return "" if el is None or el.text is None else el.text.strip()


def lis(el, name):
    node = el.find(name)
    return [] if node is None else list(node.findall("li"))


def cls_of(li):
    return (li.get("Class") or "").split(".")[-1]


TAGS = re.compile(r"\[[^\]]*\]")


def words_of(option):
    parts = [text_of(option.find("label")), text_of(option.find("outcome"))]
    parts += [text_of(li) for li in lis(option, "outcomes")]
    return TAGS.sub(" ", " ".join(p for p in parts if p))


def risk_words_of(option):
    parts = [text_of(option.find("riskOutcome"))] + [text_of(li) for li in lis(option, "riskOutcomes")]
    return TAGS.sub(" ", " ".join(p for p in parts if p))


class Answer:
    def __init__(self, source, defname, index, label, words, risk_words, effects, risk_effects, worker_kinds, path, option):
        self.source = source
        self.defname = defname
        self.index = index
        self.label = label
        self.words = words
        self.risk_words = risk_words
        self.effects = effects
        self.risk_effects = risk_effects
        self.worker = worker_kinds
        self.path = path
        self.option = option
        self.kinds = set()
        self.risk_kinds = set()
        self.classes = []

    @property
    def tiers(self):
        return {TIERS.get(k, "game") for k in self.kinds | self.worker if TIERS.get(k, "game") != "none"}

    @property
    def flavour_only(self):
        return self.tiers <= {"flavour"}

    @property
    def thin(self):
        return self.tiers <= {"flavour", "state"}

    @property
    def empty(self):
        return not (self.kinds | self.worker) - {"gate", "nothing"} and not self.risk_kinds - {"gate", "nothing"}


def effect_set(code, effects):
    kinds = set()
    classes = []
    for li in effects:
        cls = cls_of(li)
        classes.append(cls)
        k = set(code.effect_kinds(cls))
        if cls == "DilemmaEffect_Thought" and text_of(li.find("thought")) in TIE_THOUGHTS:
            k.add("relations")
        if cls in ("DilemmaEffect_Consume", "DilemmaEffect_CarryOut") and text_of(li.find("what")) == "silver":
            k.add("items")
        kinds |= k
        # Effects nested in this one (by age, by chance, one of): theirs count too.
        for child in li:
            nested = [x for x in child.findall("li") if x.get("Class")]
            if nested:
                nk, nc = effect_set(code, nested)
                kinds |= nk
                classes += nc
    return kinds, classes


def walk(code, flt=None):
    answers = []
    groups = {}  # (source, defname) -> {"path", "label", "letter": kinds, "worker": cls}
    for path in sorted(glob.glob(os.path.join(ROOT, "Defs", "**", "*.xml"), recursive=True)):
        try:
            tree = ET.parse(path)
        except ET.ParseError as e:
            print("cannot parse", path, e, file=sys.stderr)
            continue
        rel = os.path.relpath(path, ROOT)
        for d in tree.getroot():
            defname = text_of(d.find("defName"))
            if flt and not re.search(flt, defname or ""):
                continue
            if d.tag == "ColonyCouncil.DilemmaDef":
                worker = text_of(d.find("workerClass")).split(".")[-1] or "DilemmaWorker"
                groups[("question", defname)] = {"path": rel, "label": text_of(d.find("label")), "letter": set(), "worker": worker, "el": d}
                for i, o in enumerate(lis(d, "options")):
                    answers.append(make_answer(code, "question", defname, i, o, set(), rel))
            elif d.tag == "IncidentDef":
                ext = None
                other = False
                for li in lis(d, "modExtensions"):
                    c = cls_of(li)
                    if c == "HappeningExtension":
                        ext = li
                    elif c in ("StoryText", "MissionText"):
                        other = True
                # The storylines' cards are the quests' frame: their answers act through the storyline's next part.
                if ext is None or other or os.path.basename(path).startswith("Rimfolk_Storyline_"):
                    continue
                worker = text_of(d.find("workerClass")).split(".")[-1]
                res = code.resolved(worker) if worker else None
                letter, _ = effect_set(code, lis(ext, "effects"))
                groups[("event", defname)] = {"path": rel, "label": text_of(d.find("label")), "letter": letter, "worker": worker, "el": ext}
                for i, o in enumerate(lis(ext, "options")):
                    answers.append(make_answer(code, "event", defname, i, o, resolved_for(res, i), rel))
    return answers, groups


def make_answer(code, source, defname, i, o, worker_kinds, rel):
    a = Answer(source, defname, i, text_of(o.find("label")), words_of(o), risk_words_of(o), lis(o, "effects"), lis(o, "riskEffects"),
               worker_kinds, rel, o)
    a.kinds, a.classes = effect_set(code, a.effects)
    a.risk_kinds, rc = effect_set(code, a.risk_effects)
    a.classes += rc
    return a


# ---------------- Promises in the words ----------------

# A word or phrase in an answer's label or outcome, and the kinds of effect that would keep it.
PROMISES = [
    ("silver", r"\b(\d+|\{COUNT\}|some|the|a little|a few|their|its|of) silver\b|\bpa(id|ys?)\b(?! (no |any )?(attention|heed|mind|respects|a visit|their respects|for it with))",
     {"items"}),
    ("a gift or goods", r"\b(gave|gives|give|handed|hands|brought|bring|sent|left)\b[^.]{0,40}\b(gift|present|goods|bundle|parcel|basket|sack|crate|cask|keg|bolt|bale|share|portion|seed|seeds)\b",
     {"items"}),
    ("a meal", r"\b(feast|supper|dinner|fed|feeds|a meal|the meal|meals|shared (their|the|a) (food|meal|table|bread))\b", {"items", "map", "needs"}),
    ("a lesson", r"\b(taught|teaches|teach|showed [^.]{0,30}\bhow\b|learn(ed|s|t)?\b|practis|practic|lessons?\b|trained)\b", {"skill"}),
    ("goodwill", r"\b(goodwill|speak (more )?(kindly|well) of|think (better|well|worse) of the colony|will remember|won't forget|a friend of the colony|friends of the colony|grateful)\b",
     {"goodwill", "state"}),
    ("someone joins", r"\b(join(s|ed)? the colony|stays? on (at|with) the colony|one of the colony now|takes? (them|him|her) in)\b", {"people"}),
    ("someone leaves", r"\b(leaves? the colony|left the colony|banish|turned out|gone for good)\b", {"people", "map"}),
    ("a mending or building", r"\b(mend(ed|s)?|repair(ed|s)?|rebuil[dt]|patched|shored up|put up a|built a|dug a|cleared the)\b", {"work", "buildings", "map", "items"}),
    ("a hunt", r"\b(hunted|hunt(s)? (it|them|down)|went hunting|the hunters)\b", {"map", "animals", "items"}),
    ("tending", r"\b(tended|healed|nursed|medicine|bandaged|treated (the|his|her|their) wound)\b", {"health", "items", "map"}),
    ("a rest", r"\b(day off|a day's rest|rested|lie in|slept late|the morning off)\b", {"work", "needs", "map"}),
    ("a fight", r"\b(came to blows|fists|brawl|fought it out|knocked (him|her|them) down)\b", {"health", "map"}),
    ("research", r"\b(research|studied|study (it|them))\b", {"research", "skill"}),
    ("an animal", r"\b(tamed|took the (animal|beast|dog|horse|calf|foal|pup) in|let (it|the \w+) go|slaughtered|butchered)\b", {"animals", "items", "people", "map"}),
]
PROMISES = [(n, re.compile(p, re.I), k) for n, p, k in PROMISES]
# Words that tell of what didn't happen, or of what someone else does: not a promise of the answer.
NEGATED = re.compile(r"\b(no|not|never|without|nothing|refus\w*|declin\w*|instead of|n't)\b", re.I)


def promises(a):
    out = []
    have = a.kinds | a.worker
    for sentence in re.split(r"(?<=[.!?])\s+|\\n", a.words):
        for name, rx, need in PROMISES:
            m = rx.search(sentence)
            if not m or have & need:
                continue
            before = sentence[:m.start()]
            if NEGATED.search(before[-40:]):
                continue
            out.append((name, sentence.strip()))
    seen = set()
    return [(n, s) for n, s in out if not (n in seen or seen.add(n))]


# ---------------- Balance ----------------

def balance_flags(groups, answers):
    """{(source, defname): {index: [flags]}} by dilemma_balance's measure; events read as questions are."""
    defs = balance.Defs()
    out = {}
    for key, g in groups.items():
        el = g["el"]
        opts = el.findall("options/li")
        if len(opts) < 2:
            continue
        scores = [balance.option_score(defs, el, o) for o in opts]
        if any(sc["unscored"] for sc in scores):
            continue
        flags = defaultdict(list)
        for i, x in enumerate(scores):
            for j, y in enumerate(scores):
                if i != j and balance.dominates(y, x):
                    flags[i].append("dominated by answer %d" % (j + 1))
        order = sorted(range(len(scores)), key=lambda i: -scores[i]["net"])
        b, s = scores[order[0]], scores[order[1]]
        min_risk = min(sc["risk"] for k, sc in enumerate(scores) if k != order[0])
        if b["net"] - s["net"] >= max(10.0, 0.5 * abs(s["net"])) and b["risk"] <= min_risk + balance.TOL:
            flags[order[0]].append("easy: best by %.0f" % (b["net"] - s["net"]))
        if flags:
            out[key] = {"flags": dict(flags), "scores": scores}
    return out


# ---------------- The report ----------------

def short(s, n=90):
    s = re.sub(r"\s+", " ", s or "").replace("|", "/").strip()
    return s if len(s) <= n else s[:n - 3] + "..."


def kinds_text(a):
    k = sorted((a.kinds | a.worker) - {"gate", "nothing"})
    return ", ".join(k) or "nothing"


def measure(code, flt=None):
    answers, groups = walk(code, flt)
    by_group = defaultdict(list)
    for a in answers:
        by_group[(a.source, a.defname)].append(a)
    res = {"answers": answers, "groups": groups, "by_group": by_group}
    res["flavour"] = [a for a in answers if a.flavour_only and not a.empty]
    res["thin"] = [a for a in answers if a.thin and not a.flavour_only and not a.empty]
    res["empty"] = [a for a in answers if a.empty]
    res["allflavour"] = [k for k, v in by_group.items() if v and all(a.flavour_only or a.empty for a in v)]
    res["allthin"] = [k for k, v in by_group.items() if v and all(a.thin or a.empty for a in v) and k not in res["allflavour"]]
    res["promises"] = [(a, p) for a in answers for p in promises(a)]
    res["balance"] = balance_flags(groups, answers)
    unknown = Counter()
    for a in answers:
        for c in a.classes:
            if "unknown" in code.effect_kinds(c):
                unknown[c] += 1
    res["unknown"] = unknown
    return res


def counts_lines(res):
    answers = res["answers"]
    out = []
    for source in ("event", "question"):
        mine = [a for a in answers if a.source == source]
        groups = {k for k in res["by_group"] if k[0] == source}
        name = "events" if source == "event" else "council's questions"
        out.append("| %s | %d | %d | %d | %d | %d | %d | %d | %d | %d |" % (
            name, len(groups), len(mine),
            sum(1 for a in res["flavour"] if a.source == source),
            sum(1 for a in res["thin"] if a.source == source),
            sum(1 for a in res["empty"] if a.source == source),
            sum(1 for k in res["allflavour"] if k[0] == source),
            sum(1 for k in res["allthin"] if k[0] == source),
            sum(1 for a, _ in res["promises"] if a.source == source),
            sum(1 for k in res["balance"] if k[0] == source)))
    return out


def tier_counts(res):
    c = Counter()
    for a in res["answers"]:
        for k in (a.kinds | a.worker) - {"gate", "nothing"}:
            c[(a.source, k)] += 1
    return c


def report(res):
    L = []
    L.append(BEGIN)
    L.append("")
    L.append("Measured by `python3 Tools/impact_audit.py --write` (this part between the markers is rewritten each run; the review's")
    L.append("own notes are outside them).")
    L.append("")
    L.append("| | Defs | Answers | Flavour-only | Thin (moods and trust) | Empty | All flavour-only | All thin | Promises to read | Easy or dominated |")
    L.append("|---|---|---|---|---|---|---|---|---|---|")
    L += counts_lines(res)
    L.append("")
    L.append("Flavour-only: only a mood, words, a journal or chronicle line. Thin: only those and Rimfolk's own numbers (trust,")
    L.append("standards, the colony's ways, a faction's memory, a camp's tension). Empty: no effect and no worker code at all (a refusal")
    L.append("that does nothing, or an answer whose work the event's code does elsewhere). Promises: a word list's hint, read by hand.")
    L.append("")
    tc = tier_counts(res)
    L.append("What the answers carry (answers with at least one effect of the kind):")
    L.append("")
    L.append("| Kind | Events | Questions |")
    L.append("|---|---|---|")
    for k in GAME + ["later", "map", "state", "mood", "words", "journal", "chronicle", "unknown"]:
        L.append("| %s | %d | %d |" % (k, tc[("event", k)], tc[("question", k)]))
    L.append("")
    if res["unknown"]:
        L.append("Effect classes the scan could not find in the code: " + ", ".join("`%s` (%d)" % kv for kv in sorted(res["unknown"].items())) + ".")
        L.append("")

    def answer_rows(items, extra=None):
        rows = ["| Def | # | Answer | Carries | File |", "|---|---|---|---|---|"]
        for a in sorted(items, key=lambda a: (a.source, a.path, a.defname, a.index)):
            rows.append("| `%s` | %d | %s | %s | %s |" % (a.defname, a.index + 1, short(a.label, 60), kinds_text(a) + (extra(a) if extra else ""),
                                                   os.path.basename(a.path)))
        return rows

    L.append("### Events and questions whose every answer is flavour-only")
    L.append("")
    if res["allflavour"]:
        for k in sorted(res["allflavour"]):
            g = res["groups"][k]
            letter = sorted(g["letter"] - {"gate", "nothing"})
            L.append("- %s `%s` (%s)%s" % (k[0], k[1], os.path.basename(g["path"]), (": the letter itself " + ", ".join(letter)) if letter else ""))
    else:
        L.append("None.")
    L.append("")
    L.append("### Events and questions whose every answer is thin")
    L.append("")
    if res["allthin"]:
        for k in sorted(res["allthin"]):
            g = res["groups"][k]
            letter = sorted(g["letter"] - {"gate", "nothing"})
            L.append("- %s `%s` (%s)%s" % (k[0], k[1], os.path.basename(g["path"]), (": the letter itself " + ", ".join(letter)) if letter else ""))
    else:
        L.append("None.")
    L.append("")
    L.append("### Flavour-only answers")
    L.append("")
    L += answer_rows(res["flavour"]) if res["flavour"] else ["None."]
    L.append("")
    L.append("### Thin answers (moods and Rimfolk's numbers only)")
    L.append("")
    L += answer_rows(res["thin"]) if res["thin"] else ["None."]
    L.append("")
    L.append("### Answers with no effect at all")
    L.append("")
    L += answer_rows(res["empty"]) if res["empty"] else ["None."]
    L.append("")
    L.append("### Words that promise what the effects don't do (to read by hand)")
    L.append("")
    if res["promises"]:
        L.append("| Def | # | Promise | Words | Carries |")
        L.append("|---|---|---|---|---|")
        for a, (name, sentence) in sorted(res["promises"], key=lambda x: (x[0].source, x[0].defname, x[0].index)):
            L.append("| `%s` | %d | %s | %s | %s |" % (a.defname, a.index + 1, name, short(sentence, 110), kinds_text(a)))
    else:
        L.append("None.")
    L.append("")
    L.append("### One answer plainly best or worse (dilemma_balance's measure)")
    L.append("")
    if res["balance"]:
        for k in sorted(res["balance"]):
            v = res["balance"][k]
            opts = res["by_group"][k]
            what = "; ".join("%d %s: %s" % (i + 1, short(opts[i].label, 50), ", ".join(fl)) for i, fl in sorted(v["flags"].items()) if i < len(opts))
            L.append("- %s `%s`: %s" % (k[0], k[1], what))
    else:
        L.append("None.")
    L.append("")
    L.append(END)
    return L


def write(res):
    body = report(res)
    if os.path.exists(OUT):
        with open(OUT, encoding="utf-8") as fh:
            old = fh.read()
        if BEGIN in old and END in old:
            new = old[:old.index(BEGIN)] + "\n".join(body) + old[old.index(END) + len(END):]
        else:
            new = old.rstrip() + "\n\n" + "\n".join(body) + "\n"
    else:
        new = "# Review of Rimfolk's events and the council's questions\n\n" + "\n".join(body) + "\n"
    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write(new)


def main():
    args = sys.argv[1:]
    flt = args[args.index("--filter") + 1] if "--filter" in args else None
    code = Code()
    res = measure(code, flt)
    print("| | Defs | Answers | Flavour-only | Thin | Empty | All flavour-only | All thin | Promises | Easy or dominated |")
    for line in counts_lines(res):
        print(line)
    if res["unknown"]:
        print("Effect classes not found in the code:", ", ".join(sorted(res["unknown"])))
    if "--print" in args:
        what = args[args.index("--print") + 1]
        if what in ("flavour", "thin", "empty"):
            for a in sorted(res[what], key=lambda a: (a.source, a.defname, a.index)):
                print("%-8s %-50s %d %-50s %s" % (a.source, a.defname, a.index + 1, short(a.label, 50), kinds_text(a)))
        elif what in ("allflavour", "allthin"):
            for k in sorted(res[what]):
                print(k[0], k[1], res["groups"][k]["path"])
        elif what == "promises":
            for a, (name, sentence) in res["promises"]:
                print("%-8s %-50s %d %-22s %s  [%s]" % (a.source, a.defname, a.index + 1, name, short(sentence, 100), kinds_text(a)))
        elif what == "balance":
            for k, v in sorted(res["balance"].items()):
                print(k[0], k[1], v["flags"])
        elif what == "kinds":
            for a in sorted(res["answers"], key=lambda a: (a.source, a.defname, a.index)):
                print("%-8s %-50s %d %-40s %s | %s" % (a.source, a.defname, a.index + 1, short(a.label, 40), kinds_text(a), ",".join(a.classes)))
    if "--write" in args:
        write(res)
        print("Wrote", os.path.relpath(OUT, ROOT))


if __name__ == "__main__":
    main()
