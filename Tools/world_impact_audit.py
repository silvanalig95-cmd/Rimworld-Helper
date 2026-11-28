#!/usr/bin/env python3
"""
World impact audit: do the camps, the world events, the missions and the encounters change the game, or only tell of it?

Walks the defs of four areas and, for every answer in them, lists the kinds of effect it carries:

  * camps (Defs/CampDefs/, CampKindDef): every ask the colony can make of a camp and every request a camp makes of the
    colony (its effects, its price, the camp's tension and warmth, what its people remember), and, for each kind, what
    a stay brings the colony besides the fee, the hiring and the joining every camp offers (its trader, its good turns,
    what it leaves behind, its work on the land, its asks, a camp's own code);
  * world events (Defs/WorldSituationDefs/, WorldSituationDef): every answer of every beat (its effects and risk), each
    beat's own effects, the start's effects and every ending's; an answer's flags, its next beat and the ending it calls
    are followed, so an answer whose real effect comes later (a flag an ending reads, a beat it brings on) is counted with
    it;
  * missions (Defs/MissionDefs/, IncidentDefs with a mission's worker): what the worker's code gives or takes at the end
    (silver, goodwill, goods, people...), read from the code of its class and the classes it stands on;
  * encounters (Defs/EncounterDefs/, EncounterDef): their effects and whether they play out on the map.

Each effect is sorted into a kind. Real game effects: goods (things the colony gets), silver (silver it gets), spend
(silver or goods it gives or pays), goodwill, skill, health, work (work done on the map), building, animal, people
(joining, leaving, guests, prisoners), trade (a trader, a sale), region (a people's towns and fortunes, another world
event), threat (a fight, a raid), research, camp (a camp comes, stays, settles, guards), event (one of Rimfolk's events
now). Flavour: mood (a memory, recreation, strain), words (a journal, chronicle or life-goal line, a name), scene (a
play-out that only shows the words: a gathering, a visit, a walk, people at the gate), soft (a camp's tension or warmth,
what a people remembers, trust, faith, the colony's ways). "spend" alone is a cost, not something gained, so an answer
whose only real effect is to spend is counted with the flavour-only ones ("pays for words").

It reports:
  * the counts, by area;
  * flavour-only answers (asks, requests, world event answers, encounter outcomes): nothing gained but a mood, words, a
    scene or a soft change, even counting what the answer leads to; a refusal ("Decline", "Not this time") is listed
    apart, since saying no is meant to cost little and give little;
  * camps and world events whose answers are all flavour;
  * words that promise what the effects don't do: an ask's tip or outcome, an answer's label or outcome, an encounter's
    text, that says something is given, paid, taught, tended, mended or someone joins, with no effect of that kind;
  * camp kinds that bring nothing of their own to the colony over a stay.

Reviewed and left rows go in the skip list (Tools/world_impact_skips.txt, one "Key [list] why" a line, list one of
flavour, words, nothing): kept out of the lists, shown with --skip, and a stale one is named.

Usage: python3 Tools/world_impact_audit.py [--print] [--write] [--skip] [--counts] [--area camps|world|missions|encounters]
  (no flags: the counts and the lists printed; --write puts the generated part into docs/review-camps-world.md between
  its audit markers, leaving the rest of the review as written)
"""
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "docs", "review-camps-world.md")
SKIPS = os.path.join(ROOT, "Tools", "world_impact_skips.txt")
START, END = "<!-- world_impact_audit: start -->", "<!-- world_impact_audit: end -->"

REAL = ["goods", "silver", "goodwill", "skill", "health", "work", "building", "animal", "people", "trade", "region",
        "threat", "research", "camp", "event", "spend"]
FLAVOUR = ["mood", "words", "scene", "soft"]
# What counts as something gained (spend is only a cost).
GAIN = set(REAL) - {"spend"}

# ---------------- The effects, by class ----------------
# Each class: a tuple of kinds, or a function of the effect's element that returns them. A class not listed here is
# guessed from its code (see Code.guess) and named in the report, so the table can be kept up.

def _camp_comes(el):
    doing = text(el, "doing") or ""
    kinds = set({"tend": ("health",), "survey": ("goods",), "read": ("research",), "listen": ("skill",), "herbwalk": ("goods",),
                 "dig": ("work",)}.get(doing, ("scene",)))
    # What the errand leaves when it is done: one of the kind's good turns (the herd's milk after the watering), or goods.
    if text(el, "help"):
        kinds.update(HELP_KINDS.get(text(el, "help"), ("work",)))
    if text(el, "give"):
        kinds.add("goods")
    return tuple(kinds)


def _camp_help(el):
    h = text(el, "help") or ""
    return HELP_KINDS.get(h, ("work",))


def _incident(el):
    inc = text(el, "incident") or ""
    if "Trader" in inc:
        return ("trade",)
    if inc in ("WandererJoin", "RefugeePodCrash"):
        return ("people",)
    if inc in ("ResourcePodCrash", "ShipChunkDrop"):
        return ("goods",)
    if inc in ("HerdMigration", "ThrumboPasses", "FarmAnimalsWanderIn"):
        return ("animal",)
    if inc.startswith("Disease") or inc in ("ColdSnap", "HeatWave", "CropBlight", "Flashstorm"):
        return ("threat",)
    if inc.startswith("Council_Evt"):
        return ("event",)
    return ("scene",)


def _away(el):
    kinds = {"people"}
    if (text(el, "wage") or "").lower() == "true":
        kinds.add("silver")
    return tuple(kinds)


def _road_ask(el):
    a = text(el, "ask") or text(el, "what") or ""
    return {"builder": ("skill", "silver"), "food": ("silver",), "spur": ("building",)}.get(a, ("soft",))


HELP_KINDS = {
    "meat": ("goods",), "wood": ("goods",), "medicine": ("goods",), "herbs": ("goods",), "mend": ("work",), "heal": ("health",),
    "milk": ("goods",), "goods": ("goods",), "repair": ("work",), "survey": ("goods",), "tales": ("skill",), "fish": ("goods",),
    "scrap": ("goods",), "book": ("goods",), "show": ("scene",), "song": ("scene",), "blessing": ("scene",),
}

CLASSES = {
    # The shared answers (Dilemmas/, Happenings/, PlayOut/, Strangers/, Visitors/).
    "DilemmaEffect_Thought": ("mood",), "DilemmaEffect_Joy": ("mood",), "DilemmaEffect_StrainRelief": ("mood",),
    "DilemmaEffect_MentalState": ("threat",), "DilemmaEffect_Fight": ("threat",), "DilemmaEffect_TooClose": ("threat",),
    "DilemmaEffect_HarmRisk": ("words",), "DilemmaEffect_LifeEvent": ("words",), "ArcEffect_Epithet": ("words",),
    "DilemmaEffect_Remember": ("soft",), "DilemmaEffect_Trust": ("soft",), "DilemmaEffect_Faith": ("soft",),
    "DilemmaEffect_Goodwill": ("goodwill",), "DilemmaEffect_EventGoodwill": ("goodwill",),
    "DilemmaEffect_Learn": ("skill",), "DilemmaEffect_Lesson": ("skill",), "DilemmaEffect_Inspiration": ("work",),
    "DilemmaEffect_Research": ("research",), "DilemmaEffect_Hediff": ("health",), "DilemmaEffect_EaseAilment": ("health",),
    "DilemmaEffect_EaseIllness": ("health",), "DilemmaEffect_Spawn": ("goods",), "DilemmaEffect_Loot": ("goods",),
    "DilemmaEffect_Made": ("goods",), "DilemmaEffect_Carving": ("goods",), "DilemmaEffect_Felled": ("goods",),
    "DilemmaEffect_Search": ("goods", "scene"), "DilemmaEffect_Paid": ("silver",), "DilemmaEffect_Consume": ("spend",),
    "DilemmaEffect_CarryOut": ("spend",), "DilemmaEffect_Recruit": ("people",), "DilemmaEffect_Resistance": ("people",),
    "DilemmaEffect_Suppression": ("people",), "DilemmaEffect_RaidLater": ("threat",), "DilemmaEffect_SpawnAnimal": ("animal",),
    "DilemmaEffect_Gathering": ("scene",), "DilemmaEffect_Feast": ("scene", "spend"), "DilemmaEffect_Bonfire": ("scene", "spend"),
    "DilemmaEffect_Toast": ("scene",), "DilemmaEffect_Words": ("scene",), "DilemmaEffect_Visit": ("scene",),
    "DilemmaEffect_Vigil": ("scene",), "DilemmaEffect_Procession": ("scene",), "DilemmaEffect_AnimalScene": ("scene",),
    "DilemmaEffect_HuntParty": ("work", "goods"), "DilemmaEffect_WorkParty": ("work",), "DilemmaEffect_SmudgeFires": ("work",),
    "DilemmaEffect_StorylineChoice": ("event",), "DilemmaEffect_PutBy": ("work",), "DilemmaEffect_CampJoin": ("people",),
    "VisitEffect_AtGate": ("scene",), "VisitEffect_Come": ("scene",), "VisitEffect_Go": ("scene",), "VisitEffect_Stay": ("scene",),
    "VisitEffect_WorkHere": ("work",),
    # The camps (Camps/, and the peoples' camps in Peoples/).
    "DilemmaEffect_CampBrings": ("goods",), "DilemmaEffect_CampBringsAny": ("goods",), "DilemmaEffect_BeeQueen": ("animal",),
    "DilemmaEffect_CampGives": ("goods",), "DilemmaEffect_CampHaulShare": ("goods",), "DilemmaEffect_CampCatchSale": ("goods",),
    "DilemmaEffect_CampMilkSale": ("goods",), "DilemmaEffect_CampStoneSale": ("goods",), "DilemmaEffect_CampComes": _camp_comes,
    "DilemmaEffect_CampHelp": _camp_help, "DilemmaEffect_CampHireHands": ("work",), "DilemmaEffect_CampShearFlock": ("work",),
    "DilemmaEffect_CampClearNear": ("work",), "DilemmaEffect_CampMonolithNotes": ("research", "camp"),
    "DilemmaEffect_CampRuinsOurs": ("silver", "camp"), "DilemmaEffect_CampShelter": ("scene",), "DilemmaEffect_CampFarWater": ("soft",),
    "DilemmaEffect_CampGuards": ("camp",), "DilemmaEffect_CampTakeBeast": ("animal", "silver"),
    "DilemmaEffect_CampTendSick": ("health", "spend", "soft"), "DilemmaEffect_CampTendWounded": ("health", "spend", "soft"),
    "DilemmaEffect_CampChildrenFeed": ("skill",), "DilemmaEffect_CampChildrenLesson": ("skill",),
    "DilemmaEffect_HelpAtHives": ("skill",), "DilemmaEffect_TendTheirHerd": ("skill",), "DilemmaEffect_TowerWatch": ("skill",),
    "DilemmaEffect_SchoolLesson": ("skill",), "DilemmaEffect_RingSit": ("skill",), "DilemmaEffect_WorkBeside": ("skill",),
    "DilemmaEffect_ScoutLesson": ("scene", "soft"), "DilemmaEffect_ScoutOdds": ("goods",), "DilemmaEffect_ScoutSearch": ("scene",),
    "DilemmaEffect_ScoutTalkOut": ("scene",), "DilemmaEffect_SchoolSettle": ("scene",), "DilemmaEffect_SchoolLend": ("soft",),
    "DilemmaEffect_NovicesKneel": ("scene",), "DilemmaEffect_NovicesTale": ("scene",), "DilemmaEffect_PokemonShow": ("scene",),
    "DilemmaEffect_RingChallenge": ("scene",), "DilemmaEffect_RingQuiet": ("soft",), "DilemmaEffect_LanternTrail": ("scene",),
    "DilemmaEffect_DiggersGraves": ("work", "mood"), "DilemmaEffect_StonePlace": ("building",), "DilemmaEffect_KeepMast": ("building",),
    "DilemmaEffect_LordsHuntLeave": ("silver",), "DilemmaEffect_SmugglersGoods": ("goods",), "DilemmaEffect_TellOnSmugglers": ("goodwill", "camp"),
    "DilemmaEffect_R3BuyDog": ("animal",), "DilemmaEffect_R3CampClothes": ("goods",), "DilemmaEffect_R3Dye": ("soft",),
    "DilemmaEffect_R3Play": ("silver", "scene"), "DilemmaEffect_R3Question": ("scene",), "DilemmaEffect_R3Rags": ("silver", "spend"),
    "DilemmaEffect_R3RainFee": ("spend",), "DilemmaEffect_R4KBuyCarving": ("goods",), "DilemmaEffect_R4KBuyInstrument": ("goods",),
    "DilemmaEffect_R4KFirebreak": ("work",), "DilemmaEffect_R4KSit": ("goods",), "DilemmaEffect_R4KSnowFigure": ("scene",),
    "DilemmaEffect_BannerWage": ("camp",), "DilemmaEffect_BoatDinghy": ("goods",), "DilemmaEffect_AfterFed": ("soft",),
    "DilemmaEffect_AfterPlanter": ("soft",), "DilemmaEffect_CalvingHay": ("soft",), "DilemmaEffect_DryAsk": ("soft",),
    "DilemmaEffect_SickhouseAsk": ("soft",), "DilemmaEffect_PicketFed": ("soft",), "DilemmaEffect_PadAsk": ("soft",),
    "DilemmaEffect_RoadAsk": _road_ask, "DilemmaEffect_RoadCarry": ("spend", "silver"), "DilemmaEffect_HostelBread": ("spend",),
    "DilemmaEffect_HostelCough": ("threat",), "DilemmaEffect_FloodGuests": ("people",), "DilemmaEffect_CompanyEnlist": ("people",),
    "DilemmaEffect_LabBuys": ("silver",), "DilemmaEffect_LabSample": ("silver",), "DilemmaEffect_LabGene": ("health",),
    "DilemmaEffect_PartyBuys": ("silver",), "DilemmaEffect_RationSold": ("silver",), "DilemmaEffect_RationCards": ("goods",),
    "DilemmaEffect_YardHire": ("work",), "DilemmaEffect_YardMend": ("work",), "DilemmaEffect_YardScrap": ("silver", "spend"),
    "DilemmaEffect_KinFree": ("people",), "DilemmaEffect_PardonRelease": ("people",), "DilemmaEffect_BuryingTake": ("work",),
    "DilemmaEffect_GamesDay": ("scene", "spend"), "DilemmaEffect_CampChildren": ("skill",),
    # The world events (World/).
    "WorldEffect_Goodwill": ("goodwill",), "WorldEffect_Silver": ("silver",), "WorldEffect_Owe": ("silver",),
    "WorldEffect_Repay": ("silver",), "WorldEffect_Pay": ("spend",), "WorldEffect_Lend": ("spend",), "WorldEffect_Sell": ("trade",),
    "WorldEffect_Loot": ("goods",), "WorldEffect_Join": ("people",), "WorldEffect_Learn": ("skill",), "WorldEffect_Thought": ("mood",),
    "WorldEffect_ThoughtFor": ("mood",), "WorldEffect_Fortune": ("region",), "WorldEffect_Follow": ("region",),
    "WorldEffect_Incident": _incident, "WorldEffect_Event": ("event",), "WorldEffect_Harvest": ("work",),
    "WorldEffect_Camp": ("camp",), "WorldEffect_CampSettle": ("camp",), "WorldEffect_PriestStays": ("camp",),
    "WorldEffect_Hamlet": ("people", "region"), "WorldEffect_BurnDead": ("work",), "WorldEffect_Away": _away,
    "WorldEffect_Flag": (), "WorldEffect_Foretold": ("words",), "WorldEffect_Medicine": ("spend",),
    "WorldEffect_R3Drawings": ("goods",), "WorldEffect_R4WDriftLogs": ("goods",),
    # The gods' camps (Gods/World/GodCamps.cs): the pilgrims' prayers (their god notices, which nothing says), the seer's words.
    "DilemmaEffect_GodPilgrims": ("soft",), "DilemmaEffect_GodSeer": ("words",),
}

# Mode effects whose impact sheet tells what each mode does (read from its Describe): the peoples' camps' asks.
DESCRIBE = "describe"
for _c in ("DilemmaEffect_SickhouseAsk", "DilemmaEffect_DryAsk", "DilemmaEffect_PadAsk", "DilemmaEffect_RoadAsk", "DilemmaEffect_AfterFed",
           "DilemmaEffect_AfterPlanter", "DilemmaEffect_CalvingHay", "DilemmaEffect_PicketFed", "DilemmaEffect_SchoolLend", "DilemmaEffect_RingQuiet",
           "DilemmaEffect_HostelBread", "DilemmaEffect_FloodGuests", "DilemmaEffect_ScoutLesson", "DilemmaEffect_ScoutSearch",
           "DilemmaEffect_ScoutTalkOut", "DilemmaEffect_SchoolSettle", "DilemmaEffect_RingChallenge", "DilemmaEffect_LanternTrail",
           "DilemmaEffect_NovicesKneel", "DilemmaEffect_NovicesTale", "DilemmaEffect_R3Question", "DilemmaEffect_R3Dye", "DilemmaEffect_R3RainFee",
           "DilemmaEffect_R4KSnowFigure", "DilemmaEffect_CampShelter", "DilemmaEffect_CampFarWater", "DilemmaEffect_GodPilgrims",
           "DilemmaEffect_GodSeer", "DilemmaEffect_DiggersGraves", "DilemmaEffect_WorkBeside", "DilemmaEffect_TowerWatch", "DilemmaEffect_RingSit",
           "DilemmaEffect_CompanyEnlist", "DilemmaEffect_BannerWage", "DilemmaEffect_KeepMast"):
    CLASSES[_c] = DESCRIBE

# Not effects: extensions and job givers that sit in the same files.
NOT_EFFECTS = {"CampJobExtension", "HappeningExtension", "JobGiver_CampBeasts", "JobGiver_CampLife", "MissionBeast", "MissionDelivery",
               "MissionEscortWho", "MissionHands", "MissionKeep", "MissionLost", "MissionPeople", "MissionText", "ModFactionsExtension",
               "RewardInstead", "SituationText", "StorylineCard", "StorylinePart", "VisitWorkExtension"}

# A refusal: the "no" of a choice, meant to cost and give little.
REFUSAL = re.compile(r"^(no\b|not\b|decline|refuse|turn (them|it|him|her|\{X_objective\})|let (them|it|him|her) (go|be|pass|walk)|"
                     r"leave (it|them)|let it (lie|go|keep)|ignore|say no|stay out|keep (out|away|to ourselves|it\b|them off|the \w+$|"
                     r"the lane closed|the colony off|\{A_objective\} home|it for|it against|the road at)|"
                     r"send (them|him|her|it|\{X_objective\}|the \w+) (away|on|off)|send nothing|wish them|thank them and send|"
                     r"the (colony|meadow|stubble|land|gate) (can't|cannot|needs|has|doesn't|isn't|is the|knows|sees|will stay|keeps|stays)|"
                     r"nothing\b|we (can't|need)|\{A_nameDef\} is needed|it's their|it isn't|point them|ask (them|her|him) to move|"
                     r"abstain|trust the|he must go|she must go|they'll have to|let them sort|everyone to bed|(she|he) can watch|"
                     r"a few nights' rest, then|give (her|him|them) a bed for the night, no more|hear them out, and promise nothing|"
                     r"let things stay|feed (them|the \w+) and send|a meal, and send|pay him a day's wage and send|"
                     r"no (room|work)|leave (it )?to|not (for|against|our|at|today|now|this))", re.I)

# Words that promise an effect, and the kinds that keep the promise.
PROMISES = [
    ("goods", re.compile(r"\bis the colony's\b|\bare the colony's\b|\bfor the colony to (take|fetch|keep|use)\b|\b(gave|gives|give) the colony\b|"
                         r"\bleft (it |them |a |an |some )?[\w' ]{0,30}\bfor the colony\b|\bin return\b|\bfor the colony's stores\b", re.I),
     {"goods", "silver", "animal", "building", "trade", "work"}),
    ("silver", re.compile(r"\b(\{COUNT\}|\d+) silver\b|\bpaid the colony\b|\bpays the colony\b|\bthe colony is paid\b|\bfor a fee\b", re.I),
     {"silver", "spend", "trade"}),
    ("skill", re.compile(r"\btaught\b|\bteach(es|ing)? \{A|\bshowed \{A_\w+\} how\b|\bshows \{A_\w+\} how\b|\b(picked up|learned|learnt) "
                         r"(a|the|how|some|more)\b|\bpractice (with|at|in|for)\b|^practice\b|\. practice\b|\blearns\b", re.I),
     {"skill"}),
    ("health", re.compile(r"\b(tended|bandaged|stitched|dressed the wound|set the bone|cured|eased)\b", re.I), {"health"}),
    ("mended", re.compile(r"\b(mended|repaired|patched up|fixed)\b(?! (a |the )?(price|date|day|rate|fee|sum))", re.I), {"work", "building", "goods"}),
    ("joins", re.compile(r"\bjoin(s|ed)? the colony\b|\bstays? on (at|with) the colony\b|\btaken in by the colony\b", re.I), {"people"}),
]

# ---------------- Reading ----------------


def text(el, tag, default=None):
    if el is None:
        return default
    v = el.findtext(tag)
    return v.strip() if v is not None and v.strip() != "" else default


def lines(el, tag):
    if el is None:
        return []
    node = el.find(tag)
    if node is None:
        return []
    return [(li.text or "").strip() for li in node.findall("li") if (li.text or "").strip()]


def strip_tags(line):
    return re.sub(r"^\[[^\]]*\]\s*", "", line)


class Defs:
    def __init__(self, root):
        self.named = defaultdict(dict)
        self.all = []
        for path in sorted(glob.glob(os.path.join(root, "Defs", "**", "*.xml"), recursive=True)):
            try:
                r = ET.parse(path).getroot()
            except ET.ParseError:
                continue
            for el in r:
                if not isinstance(el.tag, str):
                    continue
                name = el.get("Name")
                if name:
                    self.named[el.tag][name] = el
                if el.get("Abstract", "").lower() == "true":
                    continue
                self.all.append((os.path.relpath(path, root), el))

    def resolve(self, el, depth=0):
        parent = el.get("ParentName")
        if not parent or depth > 8:
            return el
        p = self.named[el.tag].get(parent)
        if p is None:
            return el
        p = self.resolve(p, depth + 1)
        merged = ET.Element(el.tag, el.attrib)
        have = {c.tag for c in el}
        for c in p:
            if c.tag not in have:
                merged.append(c)
        for c in el:
            merged.append(c)
        return merged

    def of(self, tag):
        for f, el in self.all:
            if el.tag == tag:
                yield f, self.resolve(el)


class Code:
    """The mod's C# classes: each one's base and body, for the missions' workers and the effects the table doesn't know."""

    def __init__(self, root):
        self.base = {}
        self.body = {}
        for path in glob.glob(os.path.join(root, "Source", "**", "*.cs"), recursive=True):
            src = open(path, encoding="utf-8").read()
            for m in re.finditer(r"\bclass (\w+)\s*(?::\s*([\w.]+))?[^{;]*\{", src):
                self.base[m.group(1)] = (m.group(2) or "").split(".")[-1]
                self.body[m.group(1)] = block(src, m.end() - 1)

    def chain(self, name):
        seen = []
        while name and name in self.body and name not in seen:
            seen.append(name)
            name = self.base.get(name)
        return seen

    def text_of(self, name):
        return "\n".join(self.body.get(n, "") for n in self.chain(name))

    def overrides(self, name, method, stop):
        for n in self.chain(name):
            if n == stop:
                return False
            if re.search(r"override\s+[\w<>\[\], ]+\s+" + method + r"\s*\(", self.body.get(n, "")):
                return True
        return False

    MARKS = [
        ("silver", r"ThingDefOf\.Silver|\bPaySilver\b|WorldEffect_Silver|DilemmaEffect_Paid|\breward\b"),
        ("goodwill", r"Goodwill\("),
        ("goods", r"GenSpawn\.Spawn\(|ThingMaker\.MakeThing\(|DropPodUtility|DilemmaEffect_Loot|TryPlaceThing"),
        ("skill", r"\.Learn\(|DilemmaEffect_Learn|DilemmaEffect_Lesson"),
        ("people", r"SetFaction\(Faction\.OfPlayer|DilemmaEffect_Recruit|WorldEffect_Join"),
        ("health", r"\bTendUtility\b|OutsiderTend|AddHediff\(|Harm\.(Roll|Injure|Ail)"),
        ("threat", r"RaidLater|IncidentDefOf\.RaidEnemy|Harm\.(Roll|Injure)"),
        ("spend", r"\bTake\(|DestroyGoods|Consume|\.Destroy\("),
        ("mood", r"GiveThought|TryGainMemory"),
    ]

    # What an effect's impact sheet says it does (its Describe), the kinds it names.
    SHEET = [
        ("goodwill", r"sheet\.Goodwill\(|ImpactKind\.Goodwill"),
        ("silver", r"sheet\.Silver\("),
        ("spend", r"sheet\.Goods\(\s*-1|ImpactKind\.Goods,\s*-1"),
        ("goods", r"sheet\.Goods\(\s*1\b|ImpactKind\.Goods,\s*[01]\b"),
        ("skill", r"DilemmaImpact\.Xp\(|ImpactKind\.Skill"),
        ("health", r"ImpactKind\.Health,\s*1\b"),
        ("threat", r"ImpactKind\.Health,\s*-1\b"),
        ("people", r"ImpactKind\.People"),
        ("work", r"ImpactKind\.Map"),
        ("scene", r"sheet\.Play\("),
        ("mood", r"ImpactKind\.Mood|sheet\.Thought\("),
        ("soft", r"DilemmaImpact\.Tension\(|ImpactKind\.Trust|DilemmaImpact\.Certainty\("),
    ]

    def describe(self, name):
        """What the effect's Describe names, as {mode: kinds}: "" for what it names whatever its mode, a case's label for the
        rest (an effect with a mode field: "medicine", "food", "ward")."""
        body = ""
        for n in self.chain(name):
            src = self.body.get(n, "")
            m = re.search(r"override void Describe\s*\([^)]*\)\s*(\{|=>)", src)
            if m:
                body = block(src, m.end() - 1) if m.group(1) == "{" else src[m.end():src.find(";", m.end()) + 1]
                break
        if not body:
            return None
        parts = re.split(r'case "([^"]+)":', body)
        out = {"": {k for k, rx in self.SHEET if re.search(rx, parts[0])}}
        labels = []
        for i in range(1, len(parts), 2):
            labels.append(parts[i])
            seg = parts[i + 1]
            if not seg.strip():
                continue
            k = {k for k, rx in self.SHEET if re.search(rx, seg)}
            for lab in labels:
                out[lab] = k
            labels = []
        return out

    def guess(self, name):
        t = self.text_of(name)
        return tuple(k for k, rx in self.MARKS if re.search(rx, t))


def block(src, i):
    """The text of the block whose '{' is at i, skipping strings, characters and comments."""
    depth, j, n = 0, i, len(src)
    while j < n:
        c = src[j]
        if c == "/" and src.startswith("//", j):
            j = src.find("\n", j)
            j = n if j < 0 else j
            continue
        if c == "/" and src.startswith("/*", j):
            j = src.find("*/", j)
            j = n if j < 0 else j + 2
            continue
        if c == '"':
            verbatim = j > 0 and src[j - 1] == "@"
            j += 1
            while j < n:
                if verbatim and src[j] == '"' and j + 1 < n and src[j + 1] == '"':
                    j += 2
                    continue
                if not verbatim and src[j] == "\\":
                    j += 2
                    continue
                if src[j] == '"':
                    break
                j += 1
            j += 1
            continue
        if c == "'":
            k = src.find("'", j + 2 if src[j + 1] == "\\" else j + 1)
            if 0 < k - j <= 4:
                j = k + 1
                continue
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return src[i:j + 1]
        j += 1
    return src[i:]


CODE = None
# ThoughtDefs whose memory gives something else instead of a mood (Moods/RewardInstead.cs): their kinds.
THOUGHTS = {}
GUESSED = Counter()
UNKNOWN = Counter()


def effect_kinds(container):
    """The kinds of every effect in a list element (and those nested in it: an errand away's own effects)."""
    kinds = set()
    if container is None:
        return kinds
    for li in container.findall("li"):
        cls = (li.get("Class") or "").split(".")[-1]
        if not cls or cls in NOT_EFFECTS:
            continue
        k = CLASSES.get(cls)
        if k is None:
            g = CODE.guess(cls) if CODE else ()
            if g:
                GUESSED[cls] += 1
                k = g
            else:
                UNKNOWN[cls] += 1
                k = ()
        if k == DESCRIBE:
            d = CODE.describe(cls) if CODE else None
            if d is None:
                k = ()
            else:
                vals = {(c.text or "").strip() for c in li}
                mode = next((v for v in vals if v in d and v), None)
                k = tuple(d[""] | (d[mode] if mode else set()))
        if callable(k):
            k = k(li)
        if cls in ("DilemmaEffect_Thought", "WorldEffect_Thought", "WorldEffect_ThoughtFor"):
            k = THOUGHTS.get(text(li, "thought") or "", k)
        kinds.update(k)
        for sub in ("effects", "riskEffects"):
            kinds |= effect_kinds(li.find(sub))
    return kinds


def gains(kinds):
    return bool(set(kinds) & GAIN)


def promises(texts, kinds):
    """The promises the words make that no effect keeps: [(what, line)]."""
    out = []
    for t in texts:
        if not t:
            continue
        for what, rx, keeps in PROMISES:
            if rx.search(t) and not (set(kinds) & keeps):
                out.append((what, strip_tags(t)))
                break
    return out


# ---------------- The areas ----------------


class Row:
    """One answer: where it is, its label, its kinds (and what it leads to), and the words that promise."""

    def __init__(self, area, owner, key, label, kinds, leads=(), refusal=False, words=(), note=""):
        self.area, self.owner, self.key, self.label = area, owner, key, label
        self.kinds, self.leads = set(kinds), set(leads)
        self.refusal, self.note = refusal, note
        self.broken = promises(words, self.kinds | self.leads)

    @property
    def id(self):
        return f"{self.owner}:{self.key}" if self.key else self.owner

    @property
    def flavour(self):
        return not gains(self.kinds | self.leads)

    def kinds_text(self):
        k = sorted(self.kinds, key=order)
        s = ", ".join(k) if k else "nothing"
        extra = sorted(self.leads - self.kinds, key=order)
        if extra:
            s += " (leads to " + ", ".join(extra) + ")"
        return s


def order(k):
    return (REAL + FLAVOUR).index(k) if k in REAL + FLAVOUR else 99


def camps(defs):
    rows, kinds_out = [], []
    for f, el in defs.of("ColonyCouncil.CampKindDef"):
        dn = text(el, "defName")
        asks = el.find("asks")
        own = []
        worker = text(el, "workerClass")
        for li in (asks.findall("li") if asks is not None else []):
            key = text(li, "key", "?")
            k = effect_kinds(li.find("effects"))
            silver = int(float(text(li, "silver", "0")))
            price = text(li, "price")
            if silver > 0:
                k.add("spend")
            if price:
                k.update(("spend", "goods"))
            if float(text(li, "tension", "0")) != 0 or int(text(li, "warm", "1")) > 0 or text(li, "deed"):
                k.add("soft")
            they = (text(li, "theyAsk") or "").lower() == "true"
            label = text(li, "label", key)
            words = [text(li, "tip")] + lines(li, "outcomes")
            # A request whose "no" costs goodwill: the yes keeps it.
            leads = {"goodwill"} if they and int(text(li, "refuseGoodwill", "0")) > 0 else set()
            note = ", ".join(x for x in ("their request" if they else "", "its worker may add" if worker else "") if x)
            r = Row("camps", dn, key, label, k, leads, words=words, note=note)
            rows.append(r)
            own.append(r)
        # What a stay brings of its own.
        brings = []
        if (text(el, "trader") or "").lower() == "true":
            brings.append("a trader")
        help_ = lines(el, "help")
        real_help = [h for h in help_ if gains(HELP_KINDS.get(h, ("work",)))]
        if real_help:
            brings.append("good turns (" + ", ".join(real_help) + ")")
        if el.find("leaveBehind") is not None:
            brings.append("a gift left behind")
        work = text(el, "work")
        if work:
            brings.append("work on the land (" + work + ")")
        if (text(el, "canSettle") or "").lower() == "true":
            brings.append("may settle")
        if text(el, "leaves") == "HandedOver":
            brings.append("buildings handed over")
        if any(not r.flavour and "their request" not in r.note for r in own):
            brings.append("asks")
        if any(not r.flavour and "their request" in r.note for r in own):
            brings.append("requests")
        if worker:
            brings.append("ways of its own (" + worker.split(".")[-1] + ")")
        fights = (text(el, "fights") or "").lower() == "true"
        kinds_out.append((dn, f, brings, own, fights, help_))
    return rows, kinds_out


def world(defs):
    rows, events = [], []
    for f, el in defs.of("ColonyCouncil.WorldSituationDef"):
        dn = text(el, "defName")
        worker = (text(el, "workerClass") or "WorldSituationWorker").split(".")[-1]
        codes = worker != "WorldSituationWorker" and CODE is not None and (
            any(CODE.overrides(worker, m, "WorldSituationWorker") for m in ("Chosen", "BeatHappened", "PrepareBeat", "Ended")))
        picks = worker != "WorldSituationWorker" and CODE is not None and CODE.overrides(worker, "TimeoutEnding", "WorldSituationWorker")
        beats = {text(b, "key"): b for b in (el.find("beats").findall("li") if el.find("beats") is not None else [])}
        endings = {text(e, "key"): e for e in (el.find("endings").findall("li") if el.find("endings") is not None else [])}
        ending_kinds = {k: effect_kinds(e.find("effects")) | ({"soft"} if (text(e, "helped") or "").lower() == "true" else set())
                        for k, e in endings.items()}
        beat_own = {k: effect_kinds(b.find("effects")) for k, b in beats.items()}
        # An ending's or a beat's effect that needs a flag (WorldEffect.requires): what an answer setting the flag pays off.
        by_flag = defaultdict(set)
        for holder in list(endings.values()) + list(beats.values()):
            effs = holder.find("effects")
            for li in effs.findall("li") if effs is not None else []:
                req = text(li, "requires")
                if req:
                    wrap = ET.Element("effects")
                    wrap.append(li)
                    by_flag[req] |= effect_kinds(wrap)

        def option_direct(o):
            return effect_kinds(o.find("effects")) | effect_kinds(o.find("riskEffects"))

        def leads_of(o, depth=0):
            out = set()
            for fl in set(lines(o, "flags")) | set(lines(o, "riskFlags")):
                out |= by_flag.get(fl, set())
            nxt = [text(o, "next"), text(o, "riskNext")]
            for n in nxt:
                if n and n in beats:
                    out |= beat_own[n]
                    if depth < 2:
                        for o2 in beats[n].find("options").findall("li") if beats[n].find("options") is not None else []:
                            out |= option_direct(o2) | leads_of(o2, depth + 1)
            end = text(o, "end")
            if end and end in ending_kinds:
                out |= ending_kinds[end]
            flags = set(lines(o, "flags")) | set(lines(o, "riskFlags"))
            for k, e in endings.items():
                if flags & set(lines(e, "requires")):
                    out |= ending_kinds[k]
                # The worker picks the ending from the flags in its own code: any ending it may pick.
                elif flags and picks and (text(e, "timeout") or "").lower() == "false":
                    out |= ending_kinds[k]
            for k, b in beats.items():
                if flags & set(lines(b, "requires")):
                    out |= beat_own[k]
                    if depth < 2 and b.find("options") is not None:
                        for o2 in b.find("options").findall("li"):
                            out |= option_direct(o2)
            for m in el.find("modifiers").findall("li") if el.find("modifiers") is not None else []:
                if flags & {text(m, "whileFlag"), text(m, "unlessFlag")}:
                    out.add("event")
            if int(float(text(o, "help", "0"))) > 0:
                for k, e in endings.items():
                    if int(float(text(e, "minHelp", "-999"))) > 0:
                        out |= ending_kinds[k]
            return out

        mine = []
        for bk, b in beats.items():
            opts = b.find("options")
            if opts is None:
                continue
            for i, o in enumerate(opts.findall("li")):
                label = text(o, "label", "?")
                words = [label, text(o, "outcome")] + lines(o, "outcomes")
                r = Row("world", dn, f"{bk}/{i}", label, option_direct(o), leads_of(o), refusal=bool(REFUSAL.match(label)), words=words,
                        note="worker" if codes else "")
                rows.append(r)
                mine.append(r)
        start = effect_kinds(el.find("effects"))
        beats_real = set().union(*beat_own.values()) if beat_own else set()
        ends_real = set().union(*ending_kinds.values()) if ending_kinds else set()
        prosperity = float(text(el, "prosperity", "0") or 0) != 0
        events.append((dn, f, mine, start, beats_real, ends_real, prosperity, worker, codes))
    return rows, events


def missions(defs):
    rows = []
    for f, el in defs.of("IncidentDef"):
        if not f.replace("\\", "/").startswith("Defs/MissionDefs/"):
            continue
        dn = text(el, "defName")
        worker = (text(el, "workerClass") or "").split(".")[-1]
        k = set(CODE.guess(worker)) if CODE and worker else set()
        ext = None
        for li in el.findall("modExtensions/li"):
            if (li.get("Class") or "").endswith("HappeningExtension"):
                ext = li
        if ext is not None:
            for o in (ext.find("options").findall("li") if ext.find("options") is not None else []):
                k |= effect_kinds(o.find("effects"))
        for li in el.findall("modExtensions/li"):
            cls = (li.get("Class") or "").split(".")[-1]
            if cls in ("MissionDelivery",):
                k.update(("spend", "silver", "goodwill"))
            if cls in ("MissionHands",):
                k.update(("people", "silver", "goodwill"))
        rows.append(Row("missions", dn, "end", text(el, "label", dn), k, note=worker))
    return rows


def encounters(defs):
    rows = []
    for f, el in defs.of("ColonyCouncil.EncounterDef"):
        dn = text(el, "defName")
        k = effect_kinds(el.find("effects"))
        if text(el, "scene"):
            k.add("scene")
        if text(el, "journal"):
            k.add("words")
        rows.append(Row("encounters", dn, "", text(el, "label", dn), k, words=lines(el, "texts")))
    return rows


# ---------------- The skip list ----------------


def read_skips():
    out = {}
    if not os.path.exists(SKIPS):
        return out
    for line in open(SKIPS, encoding="utf-8"):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        m = re.match(r"^(\S+) \[([^\]]+)\]\s*(.*)$", line)
        if m:
            out[(m.group(1), m.group(2))] = m.group(3)
    return out


# ---------------- The report ----------------


def read_thoughts(defs):
    for f, el in defs.of("ThoughtDef"):
        for li in el.findall("modExtensions/li"):
            if not (li.get("Class") or "").endswith("RewardInstead"):
                continue
            k = set()
            if float(text(li, "xp", "0")) > 0:
                k.add("skill")
            if float(text(li, "research", "0")) > 0:
                k.add("research")
            if float(text(li, "inspiration", "0")) > 0:
                k.add("work")
            if float(text(li, "trust", "0")) != 0:
                k.add("soft")
            if float(text(li, "joy", "0")) > 0 or float(text(li, "rest", "0")) > 0 or float(text(li, "comfort", "0")) > 0 or float(text(li, "felt", "0")) != 0:
                k.add("mood")
            if (text(li, "journal") or "").lower() == "true":
                k.add("words")
            THOUGHTS[text(el, "defName")] = tuple(k) or ("mood",)


def build(root):
    global CODE
    CODE = Code(root)
    defs = Defs(root)
    read_thoughts(defs)
    camp_rows, camp_kinds = camps(defs)
    world_rows, events = world(defs)
    mission_rows = missions(defs)
    enc_rows = encounters(defs)
    return camp_rows, camp_kinds, world_rows, events, mission_rows, enc_rows


def report(root, area=None):
    camp_rows, camp_kinds, world_rows, events, mission_rows, enc_rows = build(root)
    skips = read_skips()
    used = set()

    def skipped(key, lst):
        if (key, lst) in skips:
            used.add((key, lst))
            return True
        return False

    out = []
    w = out.append
    all_rows = {"camps": camp_rows, "world": world_rows, "missions": mission_rows, "encounters": enc_rows}

    # Counts.
    w("### Counts\n")
    w("| Area | Answers | With a real gain | Flavour only | Of them refusals | Words that promise more |")
    w("|---|---|---|---|---|---|")
    counts = {}
    for a, rs in all_rows.items():
        fl = [r for r in rs if r.flavour]
        ref = [r for r in fl if r.refusal]
        br = [r for r in rs if r.broken]
        counts[a] = (len(rs), len(rs) - len(fl), len(fl), len(ref), len(br))
        w(f"| {a} | {len(rs)} | {len(rs) - len(fl)} | {len(fl)} | {len(ref)} | {len(br)} |")
    nothing = [c for c in camp_kinds if not c[2] and not c[4]]
    all_flavour_camps = [c for c in camp_kinds if c[3] and all(r.flavour for r in c[3])]
    all_flavour_events = [e for e in events if e[2] and all(r.flavour for r in e[2])]
    w("")
    w(f"Camp kinds: {len(camp_kinds)}; bringing nothing of their own over a stay: {len(nothing)}; whose asks are all flavour: "
      f"{len(all_flavour_camps)}. World events: {len(events)}; whose answers are all flavour: {len(all_flavour_events)}.")
    kinds_count = Counter(k for rs in all_rows.values() for r in rs for k in r.kinds)
    w("")
    w("Effect kinds over all answers: " + ", ".join(f"{k} {kinds_count[k]}" for k in REAL + FLAVOUR if kinds_count[k]) + ".")
    if GUESSED:
        w("")
        w("Effects guessed from their code (add them to the table): " + ", ".join(sorted(GUESSED)) + ".")
    if UNKNOWN:
        w("")
        w("Effects with no kind (add them to the table): " + ", ".join(sorted(UNKNOWN)) + ".")
    w("")

    left = Counter()

    def section(title, rows, lst, fmt, compact=False):
        before = len(rows)
        rows = [r for r in rows if not skipped(r.id if hasattr(r, "id") else r[0], lst)]
        left[lst] += before - len(rows)
        read = f"; {before - len(rows)} more read and left (the skip list)" if before > len(rows) else ""
        w(f"### {title} ({len(rows)}{read})\n")
        if not rows:
            w("None.\n")
            return
        if compact:
            w("; ".join(fmt(r) for r in rows) + ".\n")
            return
        for r in rows:
            w(fmt(r))
        w("")

    for a in ("camps", "world", "encounters"):
        if area and area != a:
            continue
        rs = all_rows[a]
        if a == "encounters":
            # A moment with a stranger is a mood by design (Defs/EncounterDefs: "Nobody is hurt"); listed by name only.
            section("Encounters: moments with only a mood or words (by design; read for words that promise more)",
                    [r for r in rs if r.flavour], "flavour", lambda r: f"`{r.id}`", compact=True)
        else:
            section(f"{a.capitalize()}: flavour-only answers", [r for r in rs if r.flavour and not r.refusal], "flavour",
                    lambda r: f"- `{r.id}` {r.label}: {r.kinds_text()}" + (f" ({r.note})" if r.note else ""))
            section(f"{a.capitalize()}: flavour-only refusals (saying no is meant to cost and give little)",
                    [r for r in rs if r.flavour and r.refusal], "flavour", lambda r: f"`{r.id}` {r.label}", compact=True)
        section(f"{a.capitalize()}: words that promise more than the effects do", [r for r in rs if r.broken], "words",
                lambda r: f"- `{r.id}` ({r.broken[0][0]}, has {r.kinds_text()}): \"{r.broken[0][1][:160]}\"")
    if not area or area == "camps":
        section("Camp kinds that bring nothing of their own over a stay", nothing, "nothing",
                lambda c: f"- `{c[0]}` ({c[1]}): help {', '.join(c[5]) or 'none'}; asks {len(c[3])}")
        section("Camp kinds whose asks are all flavour", all_flavour_camps, "flavour",
                lambda c: f"- `{c[0]}`: {', '.join(r.key + ' (' + r.kinds_text() + ')' for r in c[3])}")
        w("### What each camp kind brings of its own\n")
        for dn, f, brings, own, fights, help_ in sorted(camp_kinds):
            w(f"- `{dn}`: " + ("; ".join(brings) if brings else ("a war party (it fights)" if fights else "nothing of its own")))
        w("")
    if not area or area == "world":
        section("World events whose answers are all flavour", all_flavour_events, "flavour",
                lambda e: f"- `{e[0]}` ({e[1]}): start {', '.join(sorted(e[3])) or 'nothing'}; endings {', '.join(sorted(e[5], key=order)) or 'nothing'}"
                + (f"; its worker ({e[7]}) adds to answers in code" if e[8] else ""))
    if not area or area == "missions":
        w("### Missions: what they end with (read from their workers' code)\n")
        flat = [r for r in mission_rows if r.flavour]
        w(f"{len(mission_rows)} missions; {len(flat)} with no gain found in their code:\n")
        for r in flat:
            w(f"- `{r.owner}` ({r.note}): {r.kinds_text()}")
        w("")
    stale = [k for k in skips if k not in used]
    if stale and not area:
        w("### Skip list rows that no longer match\n")
        for k, l in stale:
            w(f"- `{k}` [{l}]")
        w("")
    counts["left"] = sum(left.values())
    return "\n".join(out), counts


def main():
    args = sys.argv[1:]
    area = args[args.index("--area") + 1] if "--area" in args else None
    # --root DIR reads another copy of the mod (an older commit's Defs and Source, to compare); only counts then.
    other = args[args.index("--root") + 1] if "--root" in args else None
    text_, counts = report(other or ROOT, area)
    if "--counts" in args or other:
        for a, c in counts.items():
            if a == "left":
                print(f"read and left (the skip list): {c}")
                continue
            print(f"{a}: {c[0]} answers, {c[1]} with a real gain, {c[2]} flavour only ({c[3]} refusals), {c[4]} words that promise more")
        return
    if "--skip" in args:
        for (k, l), why in sorted(read_skips().items()):
            print(f"{k} [{l}] {why}")
        return
    if "--write" in args:
        doc = open(OUT, encoding="utf-8").read() if os.path.exists(OUT) else f"# Review of the camps and the world\n\n{START}\n{END}\n"
        if START not in doc or END not in doc:
            doc += f"\n{START}\n{END}\n"
        a, b = doc.index(START) + len(START), doc.index(END)
        doc = doc[:a] + "\n\n" + text_ + "\n\n" + doc[b:]
        open(OUT, "w", encoding="utf-8").write(doc)
        print("wrote " + os.path.relpath(OUT, ROOT))
        return
    print(text_)


if __name__ == "__main__":
    main()
