#!/usr/bin/env python3
"""
Dilemma balance: which answers are easy choices?

Reads every DilemmaDef in Defs/ (with the ThoughtDefs, HediffDefs and ThingDefs they use, Name/ParentName inheritance
as the game does it) and scores each answer on comparable scales, in "points": one point is about one colonist feeling
+1 mood for one day. The conversions are rough on purpose and the same for every dilemma, so answers can be compared
with each other; the numbers are not a prediction of a real game.

  mood       a memory's mood x its days x the people it reaches (a colony of 8: A and B are 1 each, "everyone" 8,
             "everyone else" 6, A's friends 2, those who dislike A 1, A's family 1.5, those close to A 3; a trait filter
             keeps about 1 in 10 per trait listed, a degree filter about 1 in 8; an effect's chance multiplies). Higher
             standards count as -0.6 per point of standards (0.05 = -3) per person: later moods are smaller.
  trust      1.5 points per point of trust per person (it decays slowly and colours every later wish and answer).
  relations  an opinion offset x its days x people x 0.1; a bond in the social web 0.2 per point.
  wellbeing  recreation 1 point per 10% of the bar, strain 0.3 per point eased, an inspiration 8, an illness eased
             0.3 per 1% of severity, a day off +3 (recreation) per person.
  work       a work speed change x its days x 16 waking hours x 0.5 a colonist-hour (so +10% for 3 days = 2.4); a
             faster rest fall 4 per day per 100%; a raised break threshold 0.25 per 1% per day; a mental break -20; a
             fight -15; a day off -6 per person; a work party +3.
  skill      1 point per 100 XP; a mentorship 5; a lesson its XP an hour for 2 hours.
  goods      1 point per 10 silver of market value (silver itself, raw food 1.3 a unit, wood 1.2, medicine 25: more than
             its price, since the last doses can save a life; the rest from the def's MarketValue or the base game's);
             feast meals 0.5 each (the finest are served).
  people     someone joining the colony +60, someone leaving it -60.
  outside    goodwill 1 per point; a camp's tension -0.5 per point raised; a raid later -40.
  ways       the colony's ways (mercy, generosity, tradition, openness, toil) are flavour: listed, never scored.
  play-outs  gatherings, visits, walks, vigils and the like are the words made real; they cost the colony's time (work
             -0.25 a colonist-hour: half a working hour, since evenings overlap recreation) and what they really take or
             give (meals, wood, finds).

Each answer gets benefit (the sum of what it gives), cost (the sum of what it takes) and risk (the expected loss of its
gamble: chance x how much worse the risky outcome is than the plain one), and net = benefit - cost - risk.

Verdicts:
  dominated  another answer of the same dilemma is at least as good on every scale (mood, trust, relations, wellbeing,
             work, skill, goods, people, outside, risk) and better on one, by more than 1 point.
  easy       the best answer's net is ahead of the second best by at least 10 points and half the second's size, with
             no more risk than the others: the choice makes itself.
An answer that needs something (a skill, goods in the stores) is marked "gated": it can't always be given. A dilemma
with an effect the scales don't measure (a faith's certainty, a guardian named, a wedding planned) is "partly scored":
it gets no verdict, and is read by hand.

Usage: python3 Tools/dilemma_balance.py [--print] [--check] [--defs DIR]
  (writes docs/dilemma-balance.md; --print lists the flagged dilemmas; --check exits 1 if any first-pass dilemma is
  flagged; --defs reads another copy's Defs, say an older commit's, and only counts)
"""
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
# --defs DIR reads the defs from another copy of the mod (an older commit, to compare); the report is not written then.
DEFS_ROOT = sys.argv[sys.argv.index("--defs") + 1] if "--defs" in sys.argv else ROOT
OUT = os.path.join(ROOT, "docs", "dilemma-balance.md")

COLONY = 8
PEOPLE = {"A": 1, "B": 1, "AAndB": 2, "Colony": COLONY, "Others": COLONY - 2, "FriendsOfA": 2, "RivalsOfA": 1, "X": 0, "FamilyOfA": 1.5, "CloseToA": 3}
AXES = ["mood", "trust", "relations", "wellbeing", "work", "skill", "goods", "people", "outside"]
TOL = 1.0

# Files left for a second pass (other writers are adding text to them now).
SECOND_PASS = re.compile(r"^(Council_Dilemmas_Customs.*|Council_Dilemmas_Seasons.*|Council_Dilemmas3.*|Council_Dilemmas_Strangers.*|Rimfolk_Dilemmas_Camps|Council_Dilemmas_Health)\.xml$")

# Rough base-game market values for things the mod's defs don't define.
VANILLA_VALUE = {
    "Silver": 1, "WoodLog": 1.2, "Steel": 1.9, "Plasteel": 9, "ComponentIndustrial": 32, "Cloth": 1.5, "Leather_Plain": 2.1,
    "Beer": 12, "Ambrosia": 25, "SmokeleafJoint": 11, "PsychiteTea": 9, "MedicineHerbal": 10, "MedicineIndustrial": 18,
    "MedicineUltratech": 50, "MealSimple": 15, "MealFine": 20, "MealSurvivalPack": 24, "Pemmican": 1.2, "Kibble": 1.1,
    "Hay": 0.14, "Chocolate": 3, "Gold": 10, "Jade": 5, "SculptureSmall": 60, "Apparel_HatTop": 30, "Apparel_BowlerHat": 30,
    "Apparel_CowboyHat": 30, "Sarcophagus": 120, "RawPotatoes": 1.1, "RawRice": 1.1, "RawCorn": 1.1, "Meat_Cow": 2,
}
SPECIAL_VALUE = {"silver": 1, "rawfood": 1.3, "medicine": 25, "meals": 15, "wood": 1.2, "": 5}

# Play-outs that take people from their work: who goes ("target": everyone the effect is for; a number: at most that
# many) and their usual hours (the effect's own duration when given).
PLAY_TIME = {
    "DilemmaEffect_Gathering": ("target", 2.5), "DilemmaEffect_Feast": ("target", 3.0), "DilemmaEffect_Bonfire": ("target", 3.5),
    "DilemmaEffect_Toast": ("target", 1.0), "DilemmaEffect_Words": ("target", 1.0), "DilemmaEffect_FaithWords": ("target", 1.0),
    "DilemmaEffect_Visit": (1, 2.0), "DilemmaEffect_Lesson": (2, 2.0), "DilemmaEffect_Vigil": (3, 5.0), "DilemmaEffect_Procession": (6, 1.75),
    "DilemmaEffect_Search": (1, 2.5), "DilemmaEffect_HuntParty": (1, 3.5), "DilemmaEffect_Rounds": (1, 1.75),
}


def text(el, tag, default=None):
    if el is None:
        return default
    v = el.findtext(tag)
    return v.strip() if v is not None and v.strip() != "" else default


def num(el, tag, default=0.0):
    v = text(el, tag)
    try:
        return float(v) if v is not None else default
    except ValueError:
        return default


def rng(s, default=(0.0, 0.0)):
    if not s:
        return default
    parts = s.split("~")
    try:
        lo = float(parts[0])
        hi = float(parts[1]) if len(parts) > 1 else lo
        return (lo, hi)
    except ValueError:
        return default


# ---------------- Reading the defs ----------------

class Defs:
    def __init__(self):
        self.named = defaultdict(dict)
        self.all = []
        for path in sorted(glob.glob(os.path.join(DEFS_ROOT, "Defs", "**", "*.xml"), recursive=True)):
            try:
                root = ET.parse(path).getroot()
            except ET.ParseError:
                continue
            for el in root:
                name = el.get("Name")
                if name:
                    self.named[el.tag][name] = el
                if el.get("Abstract", "").lower() == "true":
                    continue
                self.all.append((os.path.basename(path), el))
        self.thoughts = {}
        self.hediffs = {}
        self.values = dict(VANILLA_VALUE)
        self.dilemmas = []
        for f, el in self.all:
            el = self.resolve(el)
            dn = text(el, "defName")
            if el.tag == "ThoughtDef":
                self.thoughts[dn] = self.thought(el)
            elif el.tag == "HediffDef":
                self.hediffs[dn] = self.hediff(el)
            elif el.tag == "ThingDef":
                mv = el.find("statBases/MarketValue")
                if mv is not None and mv.text:
                    try:
                        self.values[dn] = float(mv.text)
                    except ValueError:
                        pass
            elif el.tag == "ColonyCouncil.DilemmaDef":
                self.dilemmas.append((f, el))

    def resolve(self, el, depth=0):
        """The def with its parents' elements filled in (a child's element replaces the parent's of the same tag)."""
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

    @staticmethod
    def thought(el):
        st = el.find("stages/li")
        t = {"mood": num(st, "baseMoodEffect"), "opinion": num(st, "baseOpinionOffset"), "days": num(el, "durationDays", 1.0),
             "social": "Social" in (text(el, "thoughtClass") or ""), "instead": None}
        for li in el.findall("modExtensions/li"):
            if "RewardInstead" in (li.get("Class") or ""):
                t["instead"] = {k: num(li, k) for k in ("trust", "joy", "rest", "comfort", "xp", "inspiration", "research")}
                t["mood"] = 0.0
        return t

    @staticmethod
    def hediff(el):
        d = el.find("comps")
        days = 0.0
        if d is not None:
            for li in d:
                if "Disappears" in (li.get("Class") or ""):
                    lo, hi = rng(text(li, "disappearsAfterTicks"))
                    days = (lo + hi) / 2 / 60000.0
        st = el.find("stages/li")
        stats = {}
        if st is not None:
            for grp in ("statOffsets",):
                g = st.find(grp)
                if g is not None:
                    for s in g:
                        try:
                            stats[s.tag] = float(s.text)
                        except (TypeError, ValueError):
                            pass
            stats["restFallFactor"] = num(st, "restFallFactor", 1.0)
        return {"days": days or 3.0, "stats": stats, "bad": text(el, "isBad") == "true"}


# ---------------- Scoring ----------------

def people(e, cls):
    target = text(e, "target")
    if target is None:
        target = "Colony" if cls in ("DilemmaEffect_Gathering", "DilemmaEffect_Feast", "DilemmaEffect_Bonfire", "DilemmaEffect_Toast",
                                     "DilemmaEffect_Words", "DilemmaEffect_WorkParty", "DilemmaEffect_Procession", "DilemmaEffect_Faith") else "A"
    n = PEOPLE.get(target, 1)
    only = e.findall("onlyTrait/li")
    if only:
        n *= min(0.6, 0.1 * len(only))
    exc = e.findall("exceptTrait/li")
    if exc:
        n *= 1 - min(0.5, 0.1 * len(exc))
    if text(e, "degreeTrait"):
        n *= 0.125
    return n * num(e, "chance", 1.0)


def value_of(defs, what, ctx_thing=None):
    if what in SPECIAL_VALUE and what != "":
        return SPECIAL_VALUE[what]
    if not what:
        return ctx_thing if ctx_thing is not None else SPECIAL_VALUE[""]
    return defs.values.get(what, 5)


def score_effects(defs, d, effects):
    """[(axis, points, note)] for a list of effects."""
    out = []
    count = num(d, "thingCount", 0)
    things = [li.text for li in d.findall("things/li")] or ([text(d, "things")] if text(d, "things") else [])
    thing_value = sum(defs.values.get(t.split("|")[0], 5) for t in things) / len(things) if things else None
    for e in effects:
        cls = (e.get("Class") or "").split(".")[-1]
        ch = num(e, "chance", 1.0)
        n = people(e, cls)
        # The colony's time: those a play-out calls away, for its hours (a work party's hours do the colony's work).
        if cls in PLAY_TIME:
            who, hours = PLAY_TIME[cls]
            lo, hi = rng(text(e, "duration"), (hours, hours))
            going = n if who == "target" else min(n, num(e, "count", 0) or who)
            out.append(("work", -going * (lo + hi) / 2 * 0.25 * ch, "time at " + cls.replace("DilemmaEffect_", "").lower()))
        if cls == "DilemmaEffect_Thought":
            t = defs.thoughts.get(text(e, "thought"))
            if not t:
                continue
            if t["instead"]:
                i = t["instead"]
                if i["trust"]:
                    out.append(("trust", i["trust"] * n * 1.5, "trust instead of mood"))
                if i["joy"] or i["rest"] or i["comfort"]:
                    out.append(("wellbeing", (i["joy"] + i["rest"] + i["comfort"]) * 10 * n, "recreation instead of mood"))
                if i["xp"]:
                    out.append(("skill", i["xp"] / 100 * n, "xp instead of mood"))
                if i["inspiration"]:
                    out.append(("wellbeing", 8 * i["inspiration"] * n, "inspiration chance"))
            if t["mood"]:
                out.append(("mood", t["mood"] * t["days"] * n, text(e, "thought")))
            if t["opinion"]:
                out.append(("relations", t["opinion"] * t["days"] * n * 0.1, text(e, "thought")))
        elif cls == "DilemmaEffect_Trust":
            out.append(("trust", num(e, "amount") * n * 1.5, "trust"))
        elif cls == "DilemmaEffect_Standards":
            out.append(("mood", -num(e, "amount") * 100 * 0.6 * n, "standards"))
        elif cls == "DilemmaEffect_Hediff":
            h = defs.hediffs.get(text(e, "hediff"))
            if not h:
                continue
            s = h["stats"]
            w = s.get("WorkSpeedGlobal", 0) * h["days"] * 16 * 0.5
            w -= (s.get("restFallFactor", 1.0) - 1.0) * 4 * h["days"]
            w -= s.get("MentalBreakThreshold", 0) * 100 * 0.25 * h["days"]
            w += s.get("GlobalLearningFactor", 0) * 2 * h["days"]
            out.append(("work", w * n, text(e, "hediff")))
        elif cls == "DilemmaEffect_Inspiration":
            out.append(("wellbeing", 8 * n, "inspiration"))
        elif cls in ("DilemmaEffect_Learn", "WorldEffect_Learn"):
            out.append(("skill", num(e, "xp", 300) / 100 * n, "xp"))
        elif cls == "DilemmaEffect_MentalState":
            out.append(("work", -20 * n, "mental break"))
        elif cls == "DilemmaEffect_Fight":
            out.append(("work", -15 * ch, "fight"))
        elif cls == "DilemmaEffect_Spawn":
            c = num(e, "count", 0) or count
            v = defs.values.get(text(e, "thing"), 5) if text(e, "thing") else (thing_value or 5)
            out.append(("goods", c * v / 10 * ch, "goods gained"))
        elif cls in ("DilemmaEffect_Consume", "DilemmaEffect_CarryOut"):
            c = (num(e, "count", 0) or count) * num(e, "factor", 1.0)
            what = text(e, "what", "")
            out.append(("goods", -c * value_of(defs, what, thing_value) / 10 * ch, "goods spent: " + (what or "the thing")))
        elif cls in ("DilemmaEffect_Goodwill", "DilemmaEffect_EventGoodwill", "DilemmaEffect_FairGoodwill", "DilemmaEffect_RivalGoodwill"):
            out.append(("outside", num(e, "amount") * ch, "goodwill"))
        elif cls == "DilemmaEffect_CampTension":
            out.append(("outside", -num(e, "amount") * 0.5 * ch, "camp tension"))
        elif cls == "DilemmaEffect_ReleasePet":
            out.append(("goods", -10, "pet let go"))
        elif cls == "DilemmaEffect_EaseIllness":
            out.append(("wellbeing", num(e, "severity", 0.1) * 100 * 0.3 * n, "illness eased"))
        elif cls in ("DilemmaEffect_Strain", "ArcEffect_Strain"):
            out.append(("wellbeing", -num(e, "amount") * 0.3 * n, "strain"))
        elif cls == "DilemmaEffect_StrainRelief":
            out.append(("wellbeing", num(e, "amount", 10) * 0.3 * n, "strain eased"))
        elif cls == "DilemmaEffect_Rest":
            out.append(("wellbeing", 3 * n, "day off"))
            out.append(("work", -6 * n, "day off"))
        elif cls == "DilemmaEffect_Joy":
            out.append(("wellbeing", num(e, "amount", 0.2) * 10 * n, "recreation"))
        elif cls in ("DilemmaEffect_Tie", "ArcEffect_Tie"):
            amt = num(e, "amount", 20)
            out.append(("relations", (-1 if text(e, "kind") == "Rivals" else 1) * amt * 0.2 * ch, "bond"))
        elif cls in ("DilemmaEffect_Mentorship", "DilemmaEffect_Teach", "DilemmaEffect_Mentor"):
            out.append(("skill", 5 + num(e, "xp") / 100, "mentorship"))
        elif cls == "DilemmaEffect_Lesson":
            out.append(("skill", num(e, "xpPerHour", 150) * 2 / 100 * ch, "lesson"))
        elif cls == "DilemmaEffect_Feast":
            out.append(("goods", -0.5 * people(e, cls), "feast meals"))
        elif cls == "DilemmaEffect_WorkParty":
            out.append(("work", 3 * ch, "work party"))
        elif cls == "DilemmaEffect_Search":
            a = rng(text(e, "amount"))
            v = rng(text(e, "value"))
            tv = defs.values.get(text(e, "thing"), 5) if text(e, "thing") else 0
            out.append(("goods", ((a[0] + a[1]) / 2 * tv + (v[0] + v[1]) / 2) / 10 * ch, "a find"))
        elif cls == "DilemmaEffect_Felled":
            out.append(("goods", num(e, "count", 20) * 1.2 / 10 * ch, "wood"))
        elif cls in ("DilemmaEffect_Recruit", "DilemmaEffect_Join", "DilemmaEffect_AdoptGuest", "DilemmaEffect_CampJoin", "DilemmaEffect_RegularStays",
                     "DilemmaEffect_GuestRecruit", "DilemmaEffect_BringRelative"):
            out.append(("people", 60 * ch, "someone joins"))
        elif cls in ("DilemmaEffect_LeaveForGood", "DilemmaEffect_LeaveColony"):
            out.append(("people", -60 * ch, "someone leaves"))
        elif cls == "DilemmaEffect_RaidLater":
            out.append(("outside", -40 * ch, "a raid later"))
        elif cls in ("DilemmaEffect_Loot", "DilemmaEffect_Gift"):
            v = rng(text(e, "value"), (300, 600))
            out.append(("goods", (v[0] + v[1]) / 2 / 10 * ch, "goods"))
        elif cls == "DilemmaEffect_Research":
            out.append(("skill", 10, "research"))
        elif cls == "DilemmaEffect_Made" or cls == "DilemmaEffect_Carving":
            out.append(("goods", 3 * ch, "a thing made"))
        elif cls == "DilemmaEffect_BurnFound":
            out.append(("goods", -3 * ch, "drugs burned"))
        elif cls == "DilemmaEffect_Smash":
            out.append(("goods", -5, "a piece broken"))
        elif cls == "DilemmaEffect_Resistance":
            out.append(("outside", -num(e, "resistance") * 0.5 * ch, "prisoner resistance"))
        elif cls == "DilemmaEffect_HoldBack":
            out.append(("mood", -40, "longing while kept"))
            out.append(("trust", -num(e, "trust") * 1.5, "trust"))
        elif cls == "DilemmaEffect_Cull":
            out.append(("goods", 10 * max(1, num(e, "count", 1)), "meat and less to feed"))
        elif cls in ("DilemmaEffect_TurnOut", "DilemmaEffect_LoseAnimal"):
            out.append(("goods", -15 * max(1, num(e, "count", 1)), "an animal gone"))
        elif cls == "DilemmaEffect_GivePet":
            out.append(("goods", -10, "pet given"))
        elif cls == "DilemmaEffect_SellHeirloom":
            out.append(("goods", 10, "heirloom sold above its worth"))
    return out


# Effects score_effects puts a number on.
SCORED = {
    "DilemmaEffect_Thought", "DilemmaEffect_Trust", "DilemmaEffect_Standards", "DilemmaEffect_Hediff", "DilemmaEffect_Inspiration",
    "DilemmaEffect_Learn", "WorldEffect_Learn", "DilemmaEffect_MentalState", "DilemmaEffect_Fight", "DilemmaEffect_Spawn",
    "DilemmaEffect_Consume", "DilemmaEffect_CarryOut", "DilemmaEffect_Goodwill", "DilemmaEffect_EventGoodwill", "DilemmaEffect_FairGoodwill",
    "DilemmaEffect_RivalGoodwill", "DilemmaEffect_CampTension", "DilemmaEffect_ReleasePet", "DilemmaEffect_EaseIllness",
    "DilemmaEffect_Strain", "ArcEffect_Strain", "DilemmaEffect_StrainRelief", "DilemmaEffect_Rest", "DilemmaEffect_Joy",
    "DilemmaEffect_Tie", "ArcEffect_Tie", "DilemmaEffect_Mentorship", "DilemmaEffect_Teach", "DilemmaEffect_Mentor",
    "DilemmaEffect_Lesson", "DilemmaEffect_Feast", "DilemmaEffect_WorkParty", "DilemmaEffect_Search", "DilemmaEffect_Felled",
    "DilemmaEffect_Recruit", "DilemmaEffect_Join", "DilemmaEffect_AdoptGuest", "DilemmaEffect_CampJoin", "DilemmaEffect_RegularStays",
    "DilemmaEffect_GuestRecruit", "DilemmaEffect_BringRelative", "DilemmaEffect_LeaveForGood", "DilemmaEffect_LeaveColony",
    "DilemmaEffect_RaidLater", "DilemmaEffect_Loot", "DilemmaEffect_Gift", "DilemmaEffect_Research", "DilemmaEffect_Made",
    "DilemmaEffect_Carving", "DilemmaEffect_BurnFound", "DilemmaEffect_Smash", "DilemmaEffect_Resistance", "DilemmaEffect_HoldBack",
    "DilemmaEffect_Cull", "DilemmaEffect_TurnOut", "DilemmaEffect_LoseAnimal", "DilemmaEffect_GivePet", "DilemmaEffect_SellHeirloom",
}

# Effects that are the words made real, or a condition, and change nothing the scales measure: scored as nothing.
NARRATIVE = {
    "DilemmaEffect_Words", "DilemmaEffect_Visit", "DilemmaEffect_Procession", "DilemmaEffect_Vigil", "DilemmaEffect_Gathering",
    "DilemmaEffect_Toast", "DilemmaEffect_Bonfire", "DilemmaEffect_LifeEvent", "DilemmaEffect_Needs", "DilemmaEffect_PlanRoom",
    "DilemmaEffect_MarkGround", "DilemmaEffect_MarkGraves", "DilemmaEffect_Lanterns", "DilemmaEffect_BurnIt", "DilemmaEffect_FindLost",
    "DilemmaEffect_SendAway", "DilemmaEffect_PrisonerOrder", "DilemmaEffect_Designate", "DilemmaEffect_Snowman", "DilemmaEffect_Race",
    "DilemmaEffect_SwapRooms", "DilemmaEffect_Follow", "DilemmaEffect_BoltDoor", "DilemmaEffect_KeepFire", "DilemmaEffect_SowPatch",
    "DilemmaEffect_Plant", "DilemmaEffect_SmudgeFires", "DilemmaEffect_HaulDead", "DilemmaEffect_Hunt", "DilemmaEffect_HuntParty",
    "DilemmaEffect_Rounds", "DilemmaEffect_PlantingWatch", "DilemmaEffect_FaithCorners", "DilemmaEffect_BeastStance",
}


def unscored(o):
    """Effects of an answer the scales don't measure (a faith's certainty, a guardian named): its verdict waits for a reading by hand."""
    names = set()
    for li in o.findall("effects/li") + o.findall("riskEffects/li"):
        cls = (li.get("Class") or "").split(".")[-1]
        if cls and cls not in SCORED and cls not in NARRATIVE and cls not in PLAY_TIME:
            names.add(cls.replace("DilemmaEffect_", ""))
    return names


def option_score(defs, d, o):
    plain = score_effects(defs, d, o.findall("effects/li"))
    risky = score_effects(defs, d, o.findall("riskEffects/li"))
    p = num(o, "riskChance")
    replaces = text(o, "riskReplaces") == "true"
    ax = {a: 0.0 for a in AXES}
    for a, v, _ in plain:
        ax[a] += v * ((1 - p) if replaces else 1)
    for a, v, _ in risky:
        ax[a] += v * p
    benefit = sum(v for _, v, _ in plain if v > 0)
    cost = -sum(v for _, v, _ in plain if v < 0)
    net_plain = benefit - cost
    net_risky = sum(v for _, v, _ in risky)
    risk = max(0.0, p * (net_plain - net_risky)) if replaces else max(0.0, -p * net_risky)
    gated = any((li.get("Class") or "").endswith("DilemmaEffect_Needs") for li in o.findall("effects/li"))
    ways = o.find("ways")
    shift = ", ".join(f"{c.tag} {c.text}" for c in ways) if ways is not None else ""
    return {"axes": ax, "benefit": benefit, "cost": cost, "risk": risk, "net": benefit - cost - risk, "gated": gated, "ways": shift,
            "riskChance": p, "unscored": unscored(o)}


def dominates(x, y):
    better = False
    for a in AXES:
        if x["axes"][a] < y["axes"][a] - TOL:
            return False
        if x["axes"][a] > y["axes"][a] + TOL:
            better = True
    if x["risk"] > y["risk"] + TOL:
        return False
    if x["risk"] < y["risk"] - TOL:
        better = True
    return better


def label(s):
    s = re.sub(r"\{([A-Z])_\w+\}", r"\1", s or "")
    return s.replace("|", "/")


def analyse(defs):
    rows = []
    for f, d in defs.dilemmas:
        opts = d.findall("options/li")
        scores = [option_score(defs, d, o) for o in opts]
        flags = [[] for _ in opts]
        partly = sorted(set().union(*(sc["unscored"] for sc in scores))) if scores else []
        if partly:
            rows.append({"file": f, "def": text(d, "defName"), "label": text(d, "label"), "options": opts, "scores": scores, "flags": flags,
                         "flagged": False, "partly": partly, "second": bool(SECOND_PASS.match(f))})
            continue
        for i, x in enumerate(scores):
            for j, y in enumerate(scores):
                if i != j and dominates(y, x):
                    flags[i].append(f"dominated by answer {j + 1}")
        easy = None
        if len(scores) > 1:
            order = sorted(range(len(scores)), key=lambda i: -scores[i]["net"])
            b, s = scores[order[0]], scores[order[1]]
            min_risk = min(sc["risk"] for k, sc in enumerate(scores) if k != order[0])
            if b["net"] - s["net"] >= max(10.0, 0.5 * abs(s["net"])) and b["risk"] <= min_risk + TOL:
                easy = order[0]
                flags[order[0]].append(f"easy: best by {b['net'] - s['net']:.0f}")
        flagged = any(fl for fl in flags)
        rows.append({"file": f, "def": text(d, "defName"), "label": text(d, "label"), "options": opts, "scores": scores, "flags": flags,
                     "flagged": flagged, "partly": [], "second": bool(SECOND_PASS.match(f))})
    return rows


def brief(sc):
    parts = [f"{a} {sc['axes'][a]:+.0f}" for a in AXES if abs(sc["axes"][a]) >= 0.5]
    return ", ".join(parts) or "nothing scored"


def write(rows):
    first = [r for r in rows if not r["second"]]
    second = [r for r in rows if r["second"]]
    n_opts = sum(len(r["options"]) for r in rows)
    flagged_opts = sum(1 for r in rows for fl in r["flags"] if fl)
    lines = []
    doc = __doc__.strip().split("\n\nUsage:")[0]
    lines.append("# Dilemma balance")
    lines.append("")
    lines.append("Written by `python3 Tools/dilemma_balance.py` (do not edit by hand). How the answers are scored:")
    lines.append("")
    lines.append("```")
    lines.extend(doc.split("\n")[2:])
    lines.append("```")
    lines.append("")
    lines.append(f"{len(rows)} dilemmas, {n_opts} answers. Flagged: {sum(1 for r in rows if r['flagged'])} dilemmas, {flagged_opts} answers "
                 f"(first pass: {sum(1 for r in first if r['flagged'])} dilemmas; second pass: {sum(1 for r in second if r['flagged'])}).")
    lines.append("")

    def table(rs):
        out = ["| Dilemma | # | Answer | Benefit | Cost | Risk | Net | Scales | Verdict |", "|---|---|---|---|---|---|---|---|---|"]
        for r in rs:
            for i, (o, sc) in enumerate(zip(r["options"], r["scores"])):
                verdict = "; ".join(r["flags"][i]) or ("partly scored (" + ", ".join(r["partly"]) + "): read by hand" if r["partly"] else "trade-off")
                if sc["gated"]:
                    verdict += " (gated)"
                name = f"{r['label']} (`{r['def']}`)" if i == 0 else ""
                risk = f"{sc['risk']:.0f}" + (f" ({sc['riskChance']:.0%})" if sc["riskChance"] else "")
                out.append(f"| {name} | {i + 1} | {label(text(o, 'label'))} | {sc['benefit']:.0f} | {sc['cost']:.0f} | {risk} | {sc['net']:.0f} | {brief(sc)} | {verdict} |")
        return out

    lines.append("## First pass: the dilemmas rebalanced in this round")
    lines.append("")
    by_file = defaultdict(list)
    for r in first:
        by_file[r["file"]].append(r)
    for f in sorted(by_file):
        lines.append(f"### {f}")
        lines.append("")
        lines.extend(table(by_file[f]))
        lines.append("")
    lines.append("## Second pass")
    lines.append("")
    lines.append("These files were being written to by others in the same round; their findings wait for a second pass after the merge.")
    lines.append("")
    flagged2 = [r for r in second if r["flagged"]]
    if flagged2:
        lines.append("Flagged:")
        lines.append("")
        for r in flagged2:
            what = "; ".join(f"{i + 1} {label(text(o, 'label'))}: {', '.join(fl)}" for i, (o, fl) in enumerate(zip(r["options"], r["flags"])) if fl)
            lines.append(f"- `{r['def']}` ({r['file']}): {what}")
        lines.append("")
    by_file = defaultdict(list)
    for r in second:
        by_file[r["file"]].append(r)
    for f in sorted(by_file):
        lines.append(f"### {f}")
        lines.append("")
        lines.extend(table(by_file[f]))
        lines.append("")
    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines))


def main():
    defs = Defs()
    rows = analyse(defs)
    if DEFS_ROOT == ROOT:
        write(rows)
    flagged_first = [r for r in rows if r["flagged"] and not r["second"]]
    flagged_second = [r for r in rows if r["flagged"] and r["second"]]
    n_opts = sum(1 for r in rows for fl in r["flags"] if fl)
    print(f"{len(rows)} dilemmas; flagged: {len(flagged_first)} in the first pass, {len(flagged_second)} in the second; {n_opts} answers flagged."
          + (f" Wrote {os.path.relpath(OUT, ROOT)}." if DEFS_ROOT == ROOT else ""))
    if "--print" in sys.argv:
        for r in flagged_first + flagged_second:
            print(f"{'2nd ' if r['second'] else ''}{r['file']} {r['def']}:")
            for i, (o, sc, fl) in enumerate(zip(r["options"], r["scores"], r["flags"])):
                print(f"   {i + 1}. {label(text(o, 'label'))[:60]:60} b{sc['benefit']:6.0f} c{sc['cost']:6.0f} r{sc['risk']:5.0f} n{sc['net']:6.0f} {brief(sc)} {'; '.join(fl)}")
    if "--check" in sys.argv and flagged_first:
        sys.exit(1)


if __name__ == "__main__":
    main()
