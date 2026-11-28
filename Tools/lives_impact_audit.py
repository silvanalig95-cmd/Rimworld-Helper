#!/usr/bin/env python3
"""
The colonists' own lives impact audit: which wishes, life goals, personal stories and ambitions can finish and change the
game, and which can't finish, finish at once or are only a mood and words (docs/review-lives.md; the earlier reviews'
Tools/impact_audit.py, Tools/visitors_impact_audit.py and Tools/peoples_impact_audit.py are its models, and impact_audit's
reading of the game's calls is used here as it is).

It reads seven parts:

  wishes      each WishDef: its worker (in the code, with a check that it can come true), the memory a granted wish gives
              (its mood and how long it lasts), what its grant effects do (impact_audit's reading of their code), whether
              it may inspire, and whether it is a lasting one the council keeps watch over. Every grant also gives trust and
              a few days of working faster (Council_Motivated) and every lapse costs trust and a mood: the frame's own,
              counted once, not per wish.
  goals       each AspirationDef (a life goal): its worker; a record it counts (one of the game's or the player's mods',
              docs/player-mods; one of Rimfolk's own must be counted somewhere in the code), a counter (somewhere told to
              the life goals: aspirations.Notify(p, "key") or a def's <counter>), a skill; the skill a reached goal marks
              (GoalRewardMath.SkillFor: a heart's goal marks none) and the memory for giving it up.
  stories     each ArcDef (a personal story): every stage's gate (a record that exists and is counted, a talk that exists and
              is a story's talk, a custom gate its worker answers, a skill to practise for a Skill, Practice or Made gate,
              the goal a Goal gate waits for named by its worker),
              and what the stages and the ending do (impact_audit's reading of their effects; ArcEffect_Cast's nested ones
              too).
  ambitions   each AmbitionDef: its goals' classes and records, and what reaching it sets going on the map (achievedEffects).
  code        the code of Wishes/, Ambitions/, Aspirations/, Arcs/, Lives/ and LifeVariety/: a def looked up by name without
              SilentFail, a hediff or a wound given outside Harm, a job started on someone with no guard against the
              drafted, the broken and the player's own orders in the method (a hint to read).
  hediffs     the hediffs those give (in code, by name, and in their defs' effects): whether each one ends.
  placeholders  the placeholders each frame's words use against the ones its code fills (a wish's, a story's, a life
              goal's and an ambition's), so none is left as written on the screen.

Rows read by hand and left are in Tools/lives_impact_skips.txt ("Key [list] why"); a row that no longer matches is named, so
the list stays true.

Usage: python3 Tools/lives_impact_audit.py [--print wishes|goals|stories|gates|ambitions|code|hediffs|placeholders] [--write] [--root DIR]
  (no flags: the counts; --write rewrites docs/review-lives.md's measured part between its markers; --root measures another
  copy, an older commit's Source and Defs, for the counts before)
"""
import glob
import gzip
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
OUT = os.path.join(HERE, "docs", "review-lives.md")
SKIPS = os.path.join(HERE, "Tools", "lives_impact_skips.txt")
BEGIN = "<!-- lives_impact_audit: begin -->"
END = "<!-- lives_impact_audit: end -->"


def load_impact():
    spec = importlib.util.spec_from_file_location("impact_audit", os.path.join(HERE, "Tools", "impact_audit.py"))
    mod = importlib.util.module_from_spec(spec)
    saved = sys.argv
    sys.argv = [saved[0]]
    try:
        spec.loader.exec_module(mod)
    finally:
        sys.argv = saved
    mod.ROOT = ROOT
    mod.SRC = os.path.join(ROOT, "Source", "ColonyCouncil")
    return mod


ia = load_impact()
SRC = os.path.join(ROOT, "Source", "ColonyCouncil")

# The life frames' own code: where the code hints and the hediffs are looked for.
CODE_DIRS = ["Wishes", "Ambitions", "Aspirations", "Arcs", "Lives", "LifeVariety"]
# A guard against taking someone out of the player's hands: the player's own order looked at (a check of the drafted and the
# broken alone isn't enough here), or one of the shared guards that look at it with the rest (PlayOuts.CanJoin, CanTakePart and
# Interruptible, OwnHappenings.Held, Round4Lives.Free, the meeting's CanWake, the game's own IsCurrentJobPlayerInterruptible).
GUARD = re.compile(r"playerForced|CanJoin\(|CanTakePart\(|Interruptible\(|\bHeld\(|\bFree\(|CanWake\(|IsCurrentJobPlayerInterruptible")
LOOKUP = re.compile(r"\bGetNamed\(")


def text_of(el, name=None, default=""):
    node = el if name is None else el.find(name)
    return default if node is None or node.text is None else node.text.strip()


def short(s, n=90):
    s = re.sub(r"\s+", " ", s or "").replace("|", "/").strip()
    return s if len(s) <= n else s[:n - 3] + "..."


# ---------------- The defs, with their parents ----------------

class Defs:
    def __init__(self):
        self.named = defaultdict(dict)
        self.by_name = defaultdict(dict)
        self.all = []
        for path in sorted(glob.glob(os.path.join(ROOT, "Defs", "**", "*.xml"), recursive=True)):
            try:
                r = ET.parse(path).getroot()
            except ET.ParseError:
                continue
            for el in r:
                if not isinstance(el.tag, str):
                    continue
                tag = el.tag.split(".")[-1]
                if el.get("Name"):
                    self.named[tag][el.get("Name")] = el
                if el.get("Abstract", "").lower() == "true":
                    continue
                dn = text_of(el, "defName")
                rel = os.path.relpath(path, ROOT)
                self.all.append((rel, tag, el))
                if dn:
                    self.by_name[tag][dn] = (rel, el)

    def resolve(self, tag, el, depth=0):
        parent = el.get("ParentName")
        if not parent or depth > 8:
            return el
        p = self.named[tag].get(parent)
        if p is None:
            return el
        p = self.resolve(tag, p, depth + 1)
        merged = ET.Element(el.tag, el.attrib)
        have = {c.tag for c in el}
        for c in p:
            if c.tag not in have:
                merged.append(c)
        for c in el:
            merged.append(c)
        return merged

    def of(self, tag):
        for rel, t, el in self.all:
            if t == tag:
                yield rel, self.resolve(tag, el)

    def get(self, tag, name):
        hit = self.by_name[tag].get(name)
        return None if hit is None else self.resolve(tag, hit[1])


def catalogue(kind):
    """The def names of one kind in the player's active mods (docs/player-mods/defs.tsv.gz)."""
    out = set()
    path = os.path.join(HERE, "docs", "player-mods", "defs.tsv.gz")
    if not os.path.exists(path):
        return out
    with gzip.open(path, "rt", encoding="utf-8") as fh:
        fh.readline()
        for line in fh:
            cols = line.rstrip("\n").split("\t")
            if len(cols) >= 4 and cols[0] == kind and cols[3] == "yes":
                out.add(cols[1])
    return out


def code_text():
    """Every .cs file's text with comments taken out (strings kept: the names are in them)."""
    out = {}
    for path in sorted(glob.glob(os.path.join(SRC, "**", "*.cs"), recursive=True)):
        s = open(path, encoding="utf-8-sig").read()
        out[os.path.relpath(path, SRC).replace(os.sep, "/")] = re.sub(r"//[^\n]*|/\*.*?\*/", " ", s, flags=re.S)
    return out


def mood_of(defs, name):
    """(mood of the first stage, days it lasts) of a ThoughtDef, or None when there is no such def."""
    d = defs.get("ThoughtDef", name)
    if d is None:
        return None
    mood = 0.0
    st = d.find("stages")
    if st is not None:
        for li in st.findall("li"):
            v = text_of(li, "baseMoodEffect") or text_of(li, "baseOpinionOffset")
            if v:
                try:
                    mood = float(v)
                except ValueError:
                    pass
                break
    days = text_of(d, "durationDays")
    try:
        days = float(days) if days else 0.0
    except ValueError:
        days = 0.0
    return mood, days


# A memory that gives something instead of mood (Moods/RewardInstead.cs): what each of its fields is, in the game's kinds.
INSTEAD_KINDS = {"joy": "needs", "rest": "needs", "comfort": "needs", "xp": "skill", "inspiration": "needs", "research": "research", "trust": "state"}


def instead_of(defs, name):
    """The kinds a memory's RewardInstead gives (recreation, rest, practice, an inspiration, research, trust), or an empty set."""
    d = defs.get("ThoughtDef", name) if name else None
    out = set()
    if d is None:
        return out
    for li in d.findall("modExtensions/li"):
        if not (li.get("Class") or "").endswith("RewardInstead"):
            continue
        for field, kind in INSTEAD_KINDS.items():
            try:
                if float(text_of(li, field, "0") or 0) > 0:
                    out.add(kind)
            except ValueError:
                pass
    return out


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


# ---------------- What is counted ----------------

class Counted:
    """Which of Rimfolk's own records the code counts, which life goal counters it tells, which story talks exist."""

    def __init__(self, defs, code):
        self.code = code
        joined = "\n".join(code.values())
        self.strings = set(re.findall(r'"([A-Za-z0-9_]+)"', joined))
        # A record counted through a constant (const string X = "Council_..."): the constant's name is used somewhere else.
        consts = re.findall(r'const\s+string\s+(\w+)\s*=\s*"([A-Za-z0-9_]+)"', joined)
        words = Counter(re.findall(r"\w+", joined))
        self.const_used = {value for cname, value in consts if words[cname] > 1}
        # Fields of a DefOf class used in code (HealthDefOf.Council_PatientsPulledThrough).
        self.defof = set(re.findall(r"DefOf\.(\w+)", joined))
        # Counters told to the life goals in code, and by data (a def's <counter>, told by its effect's code).
        self.counters = set(re.findall(r'aspirations\??\.Notify\(\s*[\w.]+\s*,\s*"(\w+)"', joined))
        for m in re.finditer(r'aspirations\??\.Notify\(\s*[\w.]+\s*,\s*(\w+)\.(\w+)\s*\)', joined):
            v = re.search(r'\b' + m.group(2) + r'\s*=\s*"(\w+)"', joined)
            if v:
                self.counters.add(v.group(1))
        for rel, tag, el in defs.all:
            if tag == "AspirationDef":
                continue
            for c in el.iter("counter"):
                if c.text:
                    self.counters.add(c.text.strip())
        self.game_records = catalogue("RecordDef")
        self.own_records = set(defs.by_name["RecordDef"].keys())

    def record(self, name):
        """'game' (the game or a mod counts it), 'counted' (Rimfolk's own, counted in its code), 'never' (Rimfolk's own,
        never counted) or 'missing' (no such record)."""
        if not name:
            return "missing"
        if name in self.own_records:
            if name in self.const_used or name in self.defof or self.strings_outside_defs(name):
                return "counted"
            return "never"
        if name in self.game_records:
            return "game"
        return "missing"

    def strings_outside_defs(self, name):
        return name in self.strings


# ---------------- Wishes ----------------

class Wish:
    def __init__(self, defname, path, worker, thought, mood, effects, ignore, inspire, lasting, category, kinds, problems):
        self.defname = defname
        self.path = path
        self.worker = worker
        self.thought = thought
        self.mood = mood
        self.effects = effects
        self.ignore = ignore
        self.inspire = inspire
        self.lasting = lasting
        self.category = category
        self.kinds = kinds
        self.problems = problems

    @property
    def thin(self):
        """No memory with a real number when it comes true, and nothing else of its own."""
        return (self.mood is None or self.mood[0] <= 0) and not (self.kinds - {"mood", "words", "state", "journal", "chronicle"}) \
            and self.inspire <= 0 and not self.lasting


def wish_effect_kinds(code, lis):
    kinds = set()
    classes = []
    for li in lis:
        cls = ia.cls_of(li)
        classes.append(cls)
        owner, bodies = code.method(cls, "Run")
        if cls == "WishEffect_Thought":
            kinds.add("mood")
            continue
        if cls == "WishEffect_Hediff":
            kinds.add("health")
            continue
        for b in bodies:
            kinds |= code.kinds_of_text(owner, b, 3, set())
        if not bodies:
            kinds.add("unknown")
    return kinds - {"gate", "nothing"}, classes


def measure_wishes(defs, code):
    out = []
    for rel, d in defs.of("WishDef"):
        dn = text_of(d, "defName")
        worker = text_of(d, "workerClass").split(".")[-1]
        problems = []
        if worker not in code.classes:
            problems.append("its worker %s isn't in the code" % worker)
        else:
            owner, bodies = code.method(worker, "IsFulfilled")
            if not bodies:
                problems.append("its worker never says when it comes true")
            owner, cands = code.method(worker, "CandidatesFor")
            if not cands:
                problems.append("its worker offers it to nobody")
        thought = text_of(d, "grantedThought")
        mood = mood_of(defs, thought) if thought else None
        if thought and mood is None:
            problems.append("its memory %s is no def" % thought)
        effects = [li for li in d.findall("grantEffects/li")]
        ignore = [li for li in d.findall("ignoreEffects/li")]
        kinds, _ = wish_effect_kinds(code, effects)
        # A memory that gives something instead of mood (recreation, practice, trust) counts as what it gives.
        kinds |= instead_of(defs, thought)
        try:
            inspire = float(text_of(d, "inspirationChance", "0") or 0)
        except ValueError:
            inspire = 0.0
        lasting = text_of(d, "lasting").lower() == "true"
        out.append(Wish(dn, rel, worker, thought, mood, effects, ignore, inspire, lasting, text_of(d, "category", "Comfort"), kinds, problems))
    return out


# ---------------- Life goals ----------------

class Goal:
    def __init__(self, defname, path, worker, record, counter, skill, mark, lost, children, problems, note):
        self.defname = defname
        self.path = path
        self.worker = worker
        self.record = record
        self.counter = counter
        self.skill = skill
        self.mark = mark
        self.lost = lost
        self.children = children
        self.problems = problems
        self.note = note


# GoalRewardMath.SkillFor, read from the code so the audit follows it.
def skill_for_tables():
    path = os.path.join(SRC, "Aspirations", "GoalRewardMath.cs")
    s = open(path, encoding="utf-8-sig").read() if os.path.exists(path) else ""
    pair = re.compile(r'\{\s*"(\w+)",\s*(?:"(\w+)"|(\w+))\s*\}')
    head, _, rest = s.partition("ByCounter")
    by_record = {a: b or c for a, b, c in pair.findall(head)}
    by_counter = {a: b or c for a, b, c in pair.findall(rest.split("AnimalWorkers")[0])}
    animal = set(re.findall(r'"(AspirationWorker_\w+)"', s))
    # Whether a seat other than the three named marks its own first skill (the lives review; an older copy has none).
    own_seat = "officeSkill" in s
    return by_record, by_counter, animal, own_seat


def measure_goals(defs, code, counted):
    by_record, by_counter, animal, own_seat = skill_for_tables()
    skills = set(defs.by_name["SkillDef"].keys()) | catalogue("SkillDef") | {"Shooting", "Melee", "Construction", "Mining", "Cooking", "Plants",
                                                                               "Animals", "Crafting", "Artistic", "Medicine", "Social", "Intellectual"}
    out = []
    for rel, d in defs.of("AspirationDef"):
        dn = text_of(d, "defName")
        worker = text_of(d, "workerClass", "ColonyCouncil.AspirationWorker").split(".")[-1]
        record = text_of(d, "record")
        counter = text_of(d, "counter")
        skill = text_of(d, "skill")
        office = text_of(d, "office")
        problems = []
        if worker not in code.classes:
            problems.append("its worker %s isn't in the code" % worker)
        if record:
            r = counted.record(record)
            if r == "missing":
                problems.append("its record %s is in no active mod" % record)
            elif r == "never":
                problems.append("its record %s is never counted" % record)
        # A goal of a feature's own record names the feature (camps, ideology...) as its counter: a switch, not a count.
        if counter and worker != "AspirationWorker_FeatureRecord" and counter not in counted.counters:
            # A worker that keeps its own count names its counter as a constant.
            body = code.classes.get(worker, {}).get("body", "")
            if counter not in body:
                problems.append("its counter %s is never told" % counter)
        if skill and skill not in skills:
            problems.append("its skill %s is no def" % skill)
        mark = None
        if skill:
            mark = skill
        elif record and record in by_record:
            mark = by_record[record]
        elif counter and counter in by_counter:
            mark = by_counter[counter]
        elif office:
            # The three old seats by name, any other by the seat's own first skill (GoalRewardMath.SkillFor).
            o = defs.get("OfficeDef", office)
            first = text_of(o.find("skills/li")) if own_seat and o is not None and o.find("skills/li") is not None else None
            mark = "Medicine" if "Physician" in office else "combat" if "Marshal" in office else "Social" if "Steward" in office else first
        elif worker in animal:
            mark = "Animals"
        elif worker == "AspirationWorker_Skill":
            mark = "a passion of theirs"
        note = record or counter or skill or office or ""
        out.append(Goal(dn, rel, worker, record, counter, skill, mark, text_of(d, "lostThought"), text_of(d, "children").lower() == "true", problems, note))
    return out


# ---------------- Personal stories ----------------

class Story:
    def __init__(self, defname, path, worker, stages, gates, end_kinds, all_kinds, fizzle_kinds, problems):
        self.defname = defname
        self.path = path
        self.worker = worker
        self.stages = stages
        self.gates = gates
        self.end_kinds = end_kinds
        self.all_kinds = all_kinds
        self.fizzle_kinds = fizzle_kinds
        self.problems = problems

    @property
    def end_thin(self):
        return {ia.TIERS.get(k, "game") for k in self.end_kinds if ia.TIERS.get(k, "game") != "none"} <= {"flavour", "state"}

    @property
    def all_thin(self):
        return {ia.TIERS.get(k, "game") for k in self.all_kinds if ia.TIERS.get(k, "game") != "none"} <= {"flavour", "state"}


# Arc effects the code reading takes wrongly or can't follow (their own code in Arcs/ArcEffects.cs, read by hand).
ARC_FIXED = {
    "ArcEffect_Tie": {"relations"},
    "ArcEffect_Strain": {"needs"},
    "ArcEffect_Quirk": {"state"},
    "ArcEffect_RemoveQuirk": {"state"},
    "ArcEffect_Epithet": {"state"},
    "ArcEffect_Passion": {"skill"},
    "ArcEffect_Mentorship": {"skill"},
    "ArcEffect_Confide": {"state"},
    "ArcEffect_Confess": {"state"},
    "ArcEffect_SettleVendetta": {"state"},
    "ArcEffect_AnimalBond": {"animals"},
}


def arc_effect_kinds(code, lis, defs=None):
    kinds = set()
    for li in lis:
        cls = ia.cls_of(li)
        if cls in ARC_FIXED:
            kinds |= ARC_FIXED[cls]
        elif cls == "ArcEffect_Cast":
            pass
        else:
            k, _ = ia.effect_set(code, [li])
            kinds |= k
            # A memory that gives something instead of mood counts as what it gives.
            if cls == "DilemmaEffect_Thought" and defs is not None:
                kinds |= instead_of(defs, text_of(li, "thought"))
            continue
        nested = li.findall("effects/li")
        if nested:
            kinds |= arc_effect_kinds(code, nested, defs)
    return kinds - {"gate", "nothing"}


def worker_bodies(code, worker):
    """A worker's code with its bases' (a story's worker inherits its candidate and gates)."""
    out = ""
    base = worker
    seen = 0
    while base in code.classes and seen < 6:
        out += code.classes[base]["body"]
        base = code.classes[base]["base"]
        seen += 1
    return out


def gate_problems(g, where, d, worker, code, counted, defs):
    out = []
    kind = text_of(g, "kind", "Time")
    if kind == "Record":
        r = counted.record(text_of(g, "record"))
        if r == "missing":
            out.append("%s: its record %s is in no active mod" % (where, text_of(g, "record")))
        elif r == "never":
            out.append("%s: its record %s is never counted" % (where, text_of(g, "record")))
    elif kind == "Talk":
        talk = text_of(g, "talk")
        t = defs.get("InteractionDef", talk)
        if t is None:
            out.append("%s: its talk %s is no def" % (where, talk))
        elif "InteractionWorker_ArcTalk" not in text_of(t, "workerClass"):
            out.append("%s: its talk %s isn't a story's talk" % (where, talk))
    elif kind == "Custom":
        owner, bodies = code.method(worker, "CustomGate")
        if owner in (None, "ArcWorker"):
            out.append("%s: a custom gate its worker %s doesn't answer" % (where, worker))
    elif kind == "Goal":
        # The tracker's Goal gate reads the story's goal (Arcs/ArcTracker.cs): a worker that never names one never opens it.
        # (a local named goal is not it: "var goal = ...")
        if not re.search(r"(?<!var )(?<!string )\bgoal\s*=(?!=)", worker_bodies(code, worker)):
            out.append("%s: a Goal gate, and its worker %s never names the goal" % (where, worker))
    elif kind in ("Skill", "Practice", "Made"):
        if d.find("skills") is None and not re.search(r"\bskill\s*=", worker_bodies(code, worker)):
            out.append("%s: a %s gate with no skill the story is about" % (where, kind))
    for alt in g.findall("or/li"):
        out += gate_problems(alt, where + " (or)", d, worker, code, counted, defs)
    return out


def measure_stories(defs, code, counted):
    out = []
    for rel, d in defs.of("ArcDef"):
        dn = text_of(d, "defName")
        worker = text_of(d, "workerClass", "ColonyCouncil.ArcWorker").split(".")[-1]
        problems = []
        if worker not in code.classes:
            problems.append("its worker %s isn't in the code" % worker)
        else:
            owner, bodies = code.method(worker, "Candidate")
            if owner in (None, "ArcWorker"):
                problems.append("its worker never finds anyone it is about")
        stages = d.findall("stages/li")
        gates = []
        all_kinds = set()
        for i, st in enumerate(stages):
            g = st.find("gate")
            if g is not None:
                gates.append(text_of(g, "kind", "Time") + "".join("|" + text_of(a, "kind", "Time") for a in g.findall("or/li")))
                problems += gate_problems(g, "stage %d" % (i + 1), d, worker, code, counted, defs)
            all_kinds |= arc_effect_kinds(code, st.findall("effects/li"), defs)
        end_kinds = arc_effect_kinds(code, stages[-1].findall("effects/li"), defs) if stages else set()
        fizzle_kinds = arc_effect_kinds(code, d.findall("fizzleEffects/li"), defs)
        out.append(Story(dn, rel, worker, len(stages), gates, end_kinds, all_kinds, fizzle_kinds, problems))
    return out


# ---------------- Ambitions ----------------

class Ambition:
    def __init__(self, defname, path, goals, scene_kinds, eras, problems):
        self.defname = defname
        self.path = path
        self.goals = goals
        self.scene_kinds = scene_kinds
        self.eras = eras
        self.problems = problems


def measure_ambitions(defs, code, counted):
    out = []
    for rel, d in defs.of("AmbitionDef"):
        dn = text_of(d, "defName")
        problems = []
        goals = []
        for li in d.findall("goals/li"):
            cls = ia.cls_of(li)
            goals.append(cls)
            if cls not in code.classes:
                problems.append("its goal %s isn't in the code" % cls)
            if cls == "Goal_Record":
                r = counted.record(text_of(li, "record"))
                if r in ("missing", "never"):
                    problems.append("its record %s is %s" % (text_of(li, "record"), "in no active mod" if r == "missing" else "never counted"))
        k, _ = ia.effect_set(code, d.findall("achievedEffects/li"))
        eras = [text_of(li) for li in d.findall("eras/li")]
        out.append(Ambition(dn, rel, goals, k - {"gate", "nothing"}, eras, problems))
    return out


# ---------------- Code ----------------

def code_files():
    files = []
    for d in CODE_DIRS:
        files += sorted(glob.glob(os.path.join(SRC, d, "*.cs")))
    return files


def method_spans(s):
    out = []
    for m in ia.METHOD.finditer(s):
        name = m.group(1)
        if name in ("if", "for", "foreach", "while", "switch", "using", "return", "catch", "lock", "new"):
            continue
        start = m.end() - 1
        body = ia.block(s, start) if m.group(3) == "{" else ia.expr(s, start - 1)
        out.append((m.start(), start + len(body), name))
    return out


def measure_code():
    rows = []
    for path in code_files():
        rel = os.path.relpath(path, SRC).replace(os.sep, "/")
        s = ia.strip_code(open(path, encoding="utf-8-sig").read())
        spans = method_spans(s)

        def method_at(pos):
            best = None
            for a, b, n in spans:
                if a <= pos < b and (best is None or a > best[0]):
                    best = (a, b, n)
            return best

        def line_of(pos):
            return s.count("\n", 0, pos) + 1

        for m in LOOKUP.finditer(s):
            meth = method_at(m.start())
            rows.append(("lookup", rel, line_of(m.start()), "a def looked up by name without SilentFail", meth[2] if meth else "?"))
        for m in re.finditer(r"\.TakeDamage\(|HediffMaker\.MakeHediff\(|health\.AddHediff\(", s):
            meth = method_at(m.start())
            body = s[meth[0]:meth[1]] if meth else ""
            if re.search(r"\bHarm\.\w+\(", body):
                continue
            rows.append(("harm", rel, line_of(m.start()), "a hediff or a wound given in %s without Harm in the same method" % (meth[2] if meth else "?"), meth[2] if meth else "?"))
        for m in re.finditer(r"(\w+)\??\.jobs\??\.(StartJob|TryTakeOrderedJob)\(", s):
            who = m.group(1)
            meth = method_at(m.start())
            body = s[meth[0]:meth[1]] if meth else ""
            if GUARD.search(body):
                continue
            rows.append(("job", rel, line_of(m.start()), "%s given a job in %s with no guard in the method (a hint: read who it is)" % (who, meth[2] if meth else "?"),
                         (meth[2] if meth else "?") + ":" + who))
    return rows


# ---------------- Hediffs ----------------

# A hediff given for good that ends with something else, read by hand (none so far).
FOR_LIFE = {}


def measure_hediffs(defs):
    names = set()
    # The wish frame's own grant and lapse (CouncilDynamics.cs: a few days of working faster, a sour spell) with the frames' code.
    for path in code_files() + [os.path.join(SRC, "CouncilDynamics.cs")]:
        if not os.path.exists(path):
            continue
        for line in open(path, encoding="utf-8-sig").read().split("\n"):
            if "Hediff" not in line:
                continue
            names |= set(re.findall(r'"((?:Rimfolk|Council)_[A-Za-z0-9_]+)"', line))
            names |= set(re.findall(r"CouncilDefOf\.(\w+)", line))
    for tag in ("WishDef", "ArcDef", "AspirationDef", "AmbitionDef"):
        for rel, d in defs.of(tag):
            for h in d.iter("hediff"):
                if h.text:
                    names.add(h.text.strip())
    out = []
    for name in sorted(names):
        d = defs.get("HediffDef", name)
        if d is None:
            continue
        comps = [(c.get("Class") or "").split(".")[-1] for c in d.findall("comps/li")]
        ends = []
        if any("Disappears" in c for c in comps):
            ends.append("goes after a while")
        spd = [text_of(c, "severityPerDay") for c in d.findall("comps/li") if text_of(c, "severityPerDay")]
        if any(x.startswith("-") for x in spd):
            ends.append("its severity falls")
        cls = text_of(d, "hediffClass").split(".")[-1]
        if name in FOR_LIFE:
            ends.append(FOR_LIFE[name])
        elif cls and cls not in ("HediffWithComps", "Hediff"):
            ends.append("its own class (" + cls + ")")
        out.append((name, os.path.basename(defs.by_name["HediffDef"][name][0]), ", ".join(ends) or "nothing ends it"))
    return out


# ---------------- Placeholders ----------------

# What each frame's code fills (Wishes/Wish.cs Format; Arcs/ArcText.cs Args; Aspirations.cs Goal, Deed and Line; the ambition's
# pitch and achieved lines, Ambitions/Ambitions.cs). A placeholder outside these is left as written on the screen.
FILLED = {
    "WishDef": ({"PAWN", "FOR", "OTHER", "SUBJECT", "THING", "THINGBARE", "TARGET", "WISH", "ENVIED"},
                ["label", "request", "requestForOther", "thanks", "expired", "reminder", "fulfilledWhen", "lostText"]),
    "ArcDef": ({"PAWN", "OTHER", "PET", "THIRD", "SKILL", "LOST", "NAME", "FACTION", "GOAL", "QUIRK", "DAYS", "CALL"},
               ["stages", "fizzle", "fizzleChronicle", "eulogy"]),
    "AspirationDef": ({"PAWN", "GOAL", "SKILL", "NAME", "COUNT"}, ["goal", "deed", "chosen", "achieved"]),
    "AmbitionDef": ({"PAWN"}, ["pitch", "achieved"]),
}
PLACEHOLDER = re.compile(r"\{([A-Z][A-Z0-9]*)(?:_[A-Za-z]+)?\}")


def measure_placeholders(defs):
    rows = []
    for tag, (allowed, fields) in FILLED.items():
        for rel, d in defs.of(tag):
            dn = text_of(d, "defName")
            seen = set()
            for f in fields:
                node = d.find(f)
                if node is None:
                    continue
                # A stage's effects are filled by the effects themselves ({A}, {B}, {THING}): only its words are read here.
                texts = [el for el in node.iter() if el.tag not in ("effects",)]
                if f == "stages":
                    texts = [el for st in node.findall("li") for part in ("label", "waiting", "journal", "otherJournal", "thirdJournal", "chronicle")
                             for x in st.findall(part) for el in x.iter()]
                for el in texts:
                    for m in PLACEHOLDER.finditer(el.text or ""):
                        if m.group(1) not in allowed and m.group(1) not in seen:
                            seen.add(m.group(1))
                            rows.append((dn, os.path.basename(rel), m.group(0)))
    return rows


# ---------------- The report ----------------

def kinds_text(kinds):
    return ", ".join(sorted(kinds - {"gate", "nothing"})) or "nothing"


def build():
    code = ia.Code()
    defs = Defs()
    counted = Counted(defs, code_text())
    return {
        "wishes": measure_wishes(defs, code),
        "goals": measure_goals(defs, code, counted),
        "stories": measure_stories(defs, code, counted),
        "ambitions": measure_ambitions(defs, code, counted),
        "code": measure_code(),
        "hediffs": measure_hediffs(defs),
        "placeholders": measure_placeholders(defs),
    }


def counts(res):
    ws, gs, ss, am = res["wishes"], res["goals"], res["stories"], res["ambitions"]
    cc = Counter(r[0] for r in res["code"])
    return {
        "wishes": (len(ws), sum(1 for w in ws if w.problems), sum(1 for w in ws if w.mood and w.mood[0] > 0),
                   sum(1 for w in ws if w.kinds - {"mood", "words", "state"} or w.inspire > 0), sum(1 for w in ws if w.lasting), sum(1 for w in ws if w.thin)),
        "goals": (len(gs), sum(1 for g in gs if g.problems), sum(1 for g in gs if g.mark), sum(1 for g in gs if not g.mark)),
        "stories": (len(ss), sum(1 for s in ss if s.problems), sum(1 for s in ss if not s.end_thin), sum(1 for s in ss if s.end_thin), sum(1 for s in ss if s.all_thin)),
        "ambitions": (len(am), sum(1 for a in am if a.problems), sum(1 for a in am if a.scene_kinds)),
        "code": (cc["lookup"], cc["harm"], cc["job"]),
        "hediffs": (len(res["hediffs"]), sum(1 for h in res["hediffs"] if h[2] == "nothing ends it")),
        "placeholders": len(res["placeholders"]),
    }


def summary_lines(c):
    return [
        "wishes: %d, %d with a problem, %d with a memory of a real number when granted, %d with more of their own (an effect or an inspiration), "
        "%d lasting (watched), %d thin" % c["wishes"],
        "life goals: %d, %d with a problem, %d marking a skill when reached, %d of the heart (no skill to mark)" % c["goals"],
        "personal stories: %d, %d with a problem, %d ending in something the game keeps, %d ending in moods and words, "
        "%d with no effect beyond moods and words in any stage" % c["stories"],
        "ambitions: %d, %d with a problem, %d setting something going on the map when reached" % c["ambitions"],
        "code hints: %d lookups without SilentFail, %d hurts outside Harm, %d jobs with no guard in the method" % c["code"],
        "hediffs given: %d, %d that never end" % c["hediffs"],
        "placeholders the frames' code never fills: %d" % c["placeholders"],
    ]


def report(res):
    skips = read_skips()
    used = set()

    def skipped(key, lst):
        if (key, lst) in skips:
            used.add((key, lst))
            return True
        return False

    L = []
    w = L.append
    c = counts(res)
    w(BEGIN)
    w("")
    w("Measured by `python3 Tools/lives_impact_audit.py --write` (this part between the markers is rewritten each run; the")
    w("review's own notes are outside them).")
    w("")
    w("| Frame | Defs | With a problem found | Changing the game | Moods and words only |")
    w("|---|---|---|---|---|")
    n, p, mood, more, lasting, thin = c["wishes"]
    w("| Wishes | %d | %d | %d with more of their own besides the frame's | %d thin |" % (n, p, more, thin))
    n, p, mark, heart = c["goals"]
    w("| Life goals | %d | %d | %d marking a skill (all inspire and raise a toast) | %d of the heart |" % (n, p, mark, heart))
    n, p, real, endthin, allthin = c["stories"]
    w("| Personal stories | %d | %d | %d ending in something the game keeps | %d ending in moods and words (%d in every stage) |" % (n, p, real, endthin, allthin))
    n, p, scene = c["ambitions"]
    w("| Ambitions | %d | %d | %d with a scene of their own when reached | - |" % (n, p, scene))
    w("")
    w("Wishes: %d give a memory of a real number when granted; %d are lasting ones the council keeps watch over. Every grant also" % (mood, lasting))
    w("gives trust and a few days of working faster, and every lapse costs trust and a mood (the frame's own).")
    w("Code hints: %d defs looked up by name without SilentFail, %d hurts given outside Harm, %d jobs started with no guard in the method." % c["code"])
    w("Hediffs given: %d; that never end: %d." % c["hediffs"])
    w("Placeholders the frames' code never fills: %d." % c["placeholders"])
    w("")

    def section(title, items, lst, key, fmt):
        before = len(items)
        items = [x for x in items if not skipped(key(x), lst)]
        read = "; %d more read and left (the skip list)" % (before - len(items)) if before > len(items) else ""
        w("### %s (%d%s)" % (title, len(items), read))
        w("")
        if not items:
            w("None.")
        for x in items:
            w(fmt(x))
        w("")

    probs = [(x.defname, p) for part in ("wishes", "goals", "stories", "ambitions") for x in res[part] for p in x.problems]
    section("Problems found: can't finish, never counted, missing", probs, "problem", lambda x: x[0] + ":" + re.sub(r"\W+", "_", x[1])[:40],
            lambda x: "- `%s`: %s" % x)
    section("Wishes: thin (no memory of a real number when granted, nothing of their own)", [x for x in res["wishes"] if x.thin], "thin",
            lambda x: x.defname, lambda x: "- `%s` (%s)" % (x.defname, x.worker))
    w("### Wishes with more of their own besides the frame's")
    w("")
    for x in res["wishes"]:
        extra = []
        if x.kinds - {"mood", "words", "state"}:
            extra.append(kinds_text(x.kinds))
        if x.inspire > 0:
            extra.append("an inspiration %d%% more likely" % round(x.inspire * 100))
        if x.lasting:
            extra.append("watched")
        if extra:
            w("- `%s`: %s" % (x.defname, "; ".join(extra)))
    w("")
    section("Personal stories ending in moods and words only", [x for x in res["stories"] if x.end_thin], "thin", lambda x: x.defname,
            lambda x: "- `%s` (ends with %s; on the way %s)" % (x.defname, kinds_text(x.end_kinds), kinds_text(x.all_kinds)))
    w("### Life goals of the heart (no skill to mark when reached)")
    w("")
    w(", ".join("`%s`" % g.defname for g in res["goals"] if not g.mark) + ".")
    w("")
    section("Code: hints to read", res["code"], "code", lambda r: "%s:%s:%s" % (r[0], r[1], r[4]), lambda r: "- %s `%s:%d`: %s" % (r[0], r[1], r[2], r[3]))
    section("Placeholders the frames' code never fills", res["placeholders"], "placeholder", lambda x: x[0] + ":" + x[2],
            lambda x: "- `%s` (%s): `%s`" % x)
    section("Hediffs that never end", [h for h in res["hediffs"] if h[2] == "nothing ends it"], "hediff", lambda h: h[0], lambda h: "- `%s` (%s)" % (h[0], h[1]))
    stale = [k for k in skips if k not in used]
    if stale:
        w("### Skip list rows that no longer match")
        w("")
        for k, l in stale:
            w("- `%s` [%s]" % (k, l))
        w("")
    w(END)
    return L


def write(res):
    body = "\n".join(report(res))
    old = open(OUT, encoding="utf-8").read() if os.path.exists(OUT) else "# Review of the colonists' own lives\n\n"
    if BEGIN in old and END in old:
        new = old[:old.index(BEGIN)] + body + old[old.index(END) + len(END):]
    else:
        new = old.rstrip() + "\n\n" + body + "\n"
    open(OUT, "w", encoding="utf-8").write(new)


def main():
    args = sys.argv[1:]
    res = build()
    for line in summary_lines(counts(res)):
        print(line)
    if "--print" in args:
        what = args[args.index("--print") + 1]
        if what == "wishes":
            for x in res["wishes"]:
                print("%-44s %-36s %-34s mood %-6s eff %-24s insp %.2f %s%s" % (x.defname, x.worker, x.thought or "-", x.mood[0] if x.mood else "-", kinds_text(x.kinds),
                                                                                  x.inspire, "lasting " if x.lasting else "", "; ".join(x.problems)))
        elif what == "goals":
            for x in res["goals"]:
                print("%-40s %-40s %-28s mark %-18s %s" % (x.defname, x.worker, short(x.note, 28), x.mark or "-", "; ".join(x.problems)))
        elif what in ("stories", "gates"):
            for x in res["stories"]:
                print("%-44s %-34s %d stages, gates %s; ends %s; all %s; fizzle %s %s" % (x.defname, x.worker, x.stages, ",".join(x.gates), kinds_text(x.end_kinds),
                                                                                    kinds_text(x.all_kinds), kinds_text(x.fizzle_kinds), "; ".join(x.problems)))
        elif what == "ambitions":
            for x in res["ambitions"]:
                print("%-44s %-60s scene %-30s eras %s %s" % (x.defname, ",".join(x.goals), kinds_text(x.scene_kinds), ",".join(x.eras), "; ".join(x.problems)))
        elif what == "code":
            for r in res["code"]:
                print("%-7s %s:%d %s [key %s:%s:%s]" % (r[0], r[1], r[2], r[3], r[0], r[1], r[4]))
        elif what == "placeholders":
            for x in res["placeholders"]:
                print("%-44s %-40s %s" % x)
        elif what == "hediffs":
            for h in res["hediffs"]:
                print("%-40s %-40s %s" % h)
    if "--write" in args:
        write(res)
        print("Wrote", os.path.relpath(OUT, HERE))


if __name__ == "__main__":
    main()
