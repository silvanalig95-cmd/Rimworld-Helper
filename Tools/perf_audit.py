#!/usr/bin/env python3
"""
Static hot-spot audit of Rimfolk's C# (Source/ColonyCouncil): where the mod spends its time in a long, large game.

It finds every entry point the game calls on a clock or often (tick methods and the passes they gate by the tick of
the hour or of the day, per-frame window and overlay code, thought workers' state, thought mood offsets, stat parts,
alerts, conversation weights, job tick lambdas and Harmony patches on hot game methods), follows the calls into the
mod's own code, and counts what runs inside loops: LINQ, def lookups by name, string building, translation, scans of
every pawn, room and region queries, lister scans and new collections. Each entry point gets an estimated cost:
how often it runs in an in-game hour times the weighted work of one run (a loop multiplies what is inside it by how
many things it is likely to go over in a large colony). The numbers are estimates for ranking, not measurements.

    python3 Tools/perf_audit.py                         # writes docs/perf-audit.md
    python3 Tools/perf_audit.py --top 40 --json out.json
    python3 Tools/perf_audit.py --compare before.json   # adds a before/after column

Pure standard library; no game or build needed.
"""
import argparse
import json
import math
import os
import re
import sys
from collections import defaultdict

# ---------------------------------------------------------------- assumptions -------------------------------------
# A large, long colony on speed 3 at 60 frames a second: 180 ticks a second, so an in-game hour (2,500 ticks) is about
# 14 seconds and 833 frames. Immediate-mode GUI runs at least twice a frame (layout and repaint).
TICKS_PER_HOUR = 2500
FRAMES_PER_HOUR = 833
GUI_PER_HOUR = FRAMES_PER_HOUR * 2
COLONISTS = 20

# How many things a loop is likely to go over, by what it loops over (first match wins).
DOMAINS = [
    (r"PawnsFinder\.\w*AllMaps\w*_Colonists|PawnsFinder\.\w*FreeColonists|FreeColonists|Colonists\b|colonists\b|everyone\b|Ours\w*\(|ours\b|ColonistsOf|AllColonists|members\b|attendees\b|people\b|sleepers\b", COLONISTS, "colonists"),
    (r"PawnsFinder\.|WorldPawns|AllPawnsAlive|AllPawns\b|AllPawnsSpawned|AllPawnsUnspawned|AllHumanlike|SpawnedPawnsInFaction|PawnsInFaction|mapPawns\.", 120, "all pawns"),
    (r"(?i)pawns\b|pawn\b|guests|prisoners|animals|pets|visitors|lodgers", 30, "pawns"),
    (r"DefDatabase<ThingDef>|DefDatabase<ThoughtDef>|DefDatabase<RecipeDef>", 3000, "vanilla defs"),
    (r"DefDatabase<(HediffDef|TraitDef|InteractionDef|ResearchProjectDef|PawnKindDef|IncidentDef|JobDef|WorkGiverDef|SoundDef|TerrainDef|BodyPartDef|StatDef|RecordDef|PreceptDef)>", 500, "vanilla defs"),
    (r"DefDatabase<\w+>|AllDefs|AllDefsListForReading|\bDefs\b|\bdefs\b", 60, "defs"),
    (r"listerThings|listerBuildings|AllThings|allBuildings|ThingsInGroup|ThingsOfDef|ThingsMatching|\bthings\b|\bbuildings\b|AllBuildingsColonistOfDef|spawnedThings", 400, "things"),
    (r"(?i)cells\b|\.Cells|CellsAdjacent|GenRadial|AllCells|ActiveAreas|area\b", 200, "cells"),
    (r"[Rr]egions\b|\.Rooms\b|AllRooms|\bRooms\b", 60, "rooms"),
    (r"(?i)memories|thoughts|Memories", 40, "thoughts"),
    (r"(?i)hediffs|Hediffs", 10, "hediffs"),
    (r"(?i)relations|DirectRelations|PotentiallyRelatedPawns|RelatedPawns", 15, "relations"),
    (r"(?i)skills|\.skills\b", 12, "skills"),
    (r"(?i)traits|allTraits", 4, "traits"),
    (r"(?i)\bmaps\b|Find\.Maps|AllMaps", 2, "maps"),
    (r"(?i)entries|chronicle|lines|log\b|records|history|samples|journal", 40, "history"),
]
DEFAULT_DOMAIN = (12, "list")

# Costly things, weighted in rough units of work (a LINQ iterator with its delegate is about 1).
# (category, weight, regex on the masked code). The regexes run on code with comments and string contents blanked.
PATTERNS = [
    ("LINQ", 1.0, r"\.(Where|Select|SelectMany|Concat|Union|Intersect|Except|Distinct|Take|Skip|TakeWhile|SkipWhile|Cast|OfType|Reverse|Zip|DefaultIfEmpty)\s*\("),
    ("LINQ", 0.6, r"\.(Any|All|Count|Sum|Max|Min|Average|First|FirstOrDefault|Last|LastOrDefault|Single|SingleOrDefault|Aggregate|MaxBy|MinBy|Contains)\s*\(\s*\w+\s*=>"),
    ("LINQ", 0.6, r"\.(Any|Count|Sum|Max|Min|Average|First|FirstOrDefault|Last|LastOrDefault)\s*\(\s*\)"),
    ("LINQ", 1.5, r"\.(ToList|ToArray|ToDictionary|ToHashSet|ToLookup)\s*\("),
    ("LINQ", 3.0, r"\.(OrderBy|OrderByDescending|ThenBy|ThenByDescending|GroupBy)\s*\("),
    ("LINQ", 1.0, r"\.(RandomElement|RandomElementByWeight|TryRandomElement|TryRandomElementByWeight|InRandomOrder|RandomElementWithFallback)\s*\("),
    ("def by name", 0.5, r"DefDatabase<\w+>\s*\.\s*(GetNamed|GetNamedSilentFail)\s*\(|\b\w+Def\s*\.\s*Named\s*\(|\.GetNamedSilentFail\s*\("),
    ("def scan", 0.3, r"DefDatabase<\w+>\s*\.\s*(AllDefs|AllDefsListForReading)\b"),
    ("string", 0.5, r"\$@?\"|@\$\"|string\s*\.\s*(Format|Join|Concat)\s*\(|\"\s*\+|\+\s*\"|\.ToStringPercent\(|\.ToStringByStyle\(|\.ToStringTicksToPeriod\(|\.CapitalizeFirst\(|\.Colorize\(|\.Resolve\(\)"),
    ("string", 0.5, r"new\s+StringBuilder\b"),
    ("Translate", 3.0, r"\.Translate\s*\("),
    ("Translate", 1.5, r"\.Formatted\s*\("),
    ("pawn scan", 10.0, r"PawnsFinder\s*\.\s*\w+"),
    ("pawn scan", 20.0, r"Find\s*\.\s*WorldPawns\s*\.\s*\w*AllPawns\w*"),
    ("pawn scan", 0.3, r"mapPawns\s*\.\s*\w+"),
    ("room query", 0.5, r"\.GetRoom\s*\(|\.GetRoomOrAdjacent\s*\(|GetDistrict\s*\(|\.Rooms\b|\.AllRooms\b|\.GetRegion\s*\(|RegionAndRoomQuery|RegionTraverser|\.Neighbors\b|\.ContainedAndAdjacentThings\b|\.Ownership\b|\.OwnedBed\b|\.Role\b|\.PsychologicallyOutdoors\b|\.CellCount\b"),
    ("room query", 2.0, r"\.GetStat\s*\(\s*RoomStatDefOf|\.GetStat\s*\(\s*\w+\s*\)"),
    ("lister scan", 0.5, r"listerThings\s*\.|listerBuildings\s*\.|\.ThingsInGroup\s*\(|\.ThingsOfDef\s*\(|resourceCounter|TotalHumanEdibleNutrition|\.GetThingList\s*\(|\.thingGrid\.|ThingsListAt|ThingsAt\s*\("),
    ("reach or path", 20.0, r"\.CanReach\w*\s*\(|GenClosest\s*\.|\.reachability\s*\.|pathFinder|PathEndMode\.\w+\s*,\s*Danger|RCellFinder\s*\.|CellFinder\s*\.\s*TryFind|GenRadial\s*\.\s*RadialDistinctThingsAround|GenRadial\s*\.\s*RadialCellsAround|TryFindRandomSpot|\.CanReserveAndReach\s*\("),
    ("new collection", 0.5, r"new\s+(List|HashSet|Dictionary|Queue|Stack|SortedList|SortedDictionary)\s*<"),
    ("stat", 0.3, r"\.GetStatValue\s*\("),
]
CATEGORIES = ["LINQ", "def by name", "def scan", "string", "Translate", "pawn scan", "room query", "lister scan", "reach or path", "new collection", "stat"]
LOOP_STEP = 0.05  # a loop iteration's own bookkeeping
LOOP_CAP = 2e4  # nested loops: no more than this many times round in one method, however deep
FILTER_PASS = 0.2  # "if (...) continue;" at the top of a loop: the share of items that get past it

# Names never resolved by name alone (the BCL's and the game's, which the mod also uses as method names).
COMMON = set("""
Add Remove Contains Clear Get Set TryGetValue ContainsKey Count ToString Equals GetHashCode Look Append Insert IndexOf
Sort Reverse Find Exists Any All Where Select First Max Min Sum Format Join Concat Split Trim Replace Substring
StartsWith EndsWith Parse TryParse Clamp Lerp Round Abs Log Message Warning Error ErrorOnce Translate Formatted
Resolve Named GetNamed GetNamedSilentFail Chance Range Value Label LabelCap Notify Init Reset Tick Draw Update
ExposeData ResolveReferences PostLoadInit ConfigErrors Begin End Start Stop Apply Check Fill Get Of For On Off
Invoke ToList ToArray Dispose Copy CopyTo RemoveAll RemoveAt AddRange Distinct Push Pop Peek Enqueue Dequeue
Create Make Do Run Open Close Show Hide Text Label Button Widgets GUI Rect Gap Line Space Begin BeginGroup EndGroup
TipRegion DrawTexture CheckboxLabeled ButtonText ButtonInvisible Highlight DrawHighlightIfMouseover
""".split())

KEYWORDS = set("""
if for foreach while switch catch using lock fixed return new typeof nameof sizeof default checked unchecked
base this throw await yield when else do try finally case get set init add remove var is as in out ref params
stackalloc delegate operator implicit explicit
""".split())

# Harmony targets: how often the game calls the patched method in a large colony, per in-game hour.
HARMONY_FREQ = {
    "Thing.Destroy": 3000, "Building.Destroy": 40, "ThoughtHandler.MoodOffsetOfGroup": 12000,
    "MemoryThoughtHandler.TryGainMemory": 400, "Thing.Ingested": 80, "Pawn_RecordsTracker.Increment": 600,
    "WindManager.WindManagerTick": TICKS_PER_HOUR, "Pawn.GetInspectString": FRAMES_PER_HOUR,
    "NeedsCardUtility.DrawThoughtGroup": GUI_PER_HOUR * 25, "TooltipHandler.TipRegion": GUI_PER_HOUR * 25,
    "IncidentWorker.BaseChanceThisGame": 250, "Plant.MakeLeafless": 30, "Plant.CropBlighted": 2,
    "GenRecipe.PostProcessProduct": 30, "GenRecipe.MakeRecipeProducts": 30, "Frame.CompleteConstruction": 30,
    "Frame.FailConstruction": 3, "FloatMenu..ctor": 5, "LetterStack.ReceiveLetter": 2,
    "MentalStateHandler.TryStartMentalState": 2, "Pawn.Kill": 3, "Faction.TryAffectGoodwillWith": 2,
}
HARMONY_DEFAULT = 2  # a game event (a death, a raid, a trade, a letter)

# Entry points the game calls without a clock of the mod's own: method name -> (calls per hour, what, kind).
ROOTS = {
    "GameComponentTick": (TICKS_PER_HOUR, "every tick", "tick"),
    "WorldComponentTick": (TICKS_PER_HOUR, "every tick", "tick"),
    "MapComponentTick": (TICKS_PER_HOUR, "every tick, per map", "tick"),
    "GameConditionTick": (TICKS_PER_HOUR, "every tick while the condition lasts", "tick"),
    "LordToilTick": (TICKS_PER_HOUR, "every tick while the lord lasts", "tick"),
    "Tick": (TICKS_PER_HOUR, "every tick", "tick"),
    "CompTick": (TICKS_PER_HOUR, "every tick per thing", "tick"),
    "CompPostTick": (TICKS_PER_HOUR, "every tick per hediff", "tick"),
    "CompPostTickInterval": (TICKS_PER_HOUR / 200 * 3, "every 200 ticks per hediff (3 hediffs)", "tick"),
    "MentalStateTick": (TICKS_PER_HOUR / 150, "every 150 ticks while in the state", "tick"),
    "GameComponentUpdate": (FRAMES_PER_HOUR, "every frame", "frame"),
    "MapComponentUpdate": (FRAMES_PER_HOUR, "every frame", "frame"),
    "WindowUpdate": (FRAMES_PER_HOUR, "every frame while open", "frame"),
    "GameComponentOnGUI": (GUI_PER_HOUR, "every GUI event", "frame"),
    "MapComponentOnGUI": (GUI_PER_HOUR, "every GUI event", "frame"),
    "DoWindowContents": (GUI_PER_HOUR, "every GUI event while open", "frame"),
    "ExtraOnGUI": (GUI_PER_HOUR, "every GUI event while open", "frame"),
    "FillTab": (GUI_PER_HOUR, "every GUI event while the tab is open", "frame"),
    "DrawGUIOverlay": (GUI_PER_HOUR, "every GUI event per thing on screen", "frame"),
    "GizmoOnGUI": (GUI_PER_HOUR, "every GUI event while selected", "frame"),
    "DoCell": (GUI_PER_HOUR * 20, "every GUI event per row", "frame"),
    "CurrentStateInternal": (TICKS_PER_HOUR / 100 * COLONISTS, "every 100 ticks per colonist (vanilla)", "mood"),
    "CurrentSocialStateInternal": (TICKS_PER_HOUR / 100 * COLONISTS * (COLONISTS - 1) / 4, "every 100 ticks per pair asked about", "mood"),
    "MoodOffset": (TICKS_PER_HOUR / 150 * COLONISTS * 2, "each mood update per colonist", "mood"),
    "TransformValue": (3000, "each time the stat is worked out", "stat"),
    "RandomSelectionWeight": (TICKS_PER_HOUR / 6600 * COLONISTS * 4, "each social interaction chosen, per partner", "talk"),
    "GetReport": (FRAMES_PER_HOUR / 24, "the alert cycle (every 24 frames)", "alert"),
    "GetLabel": (FRAMES_PER_HOUR, "every frame while the alert shows", "alert"),
    "CanFireNowSub": (2, "storyteller picks", "event"),
    "TryGiveJob": (COLONISTS * 6, "each job search", "job"),
    "NeedInterval": (TICKS_PER_HOUR / 150 * COLONISTS, "every 150 ticks per pawn", "tick"),
    "SeverityChangePerDay": (TICKS_PER_HOUR / 200 * 3, "every 200 ticks per hediff", "tick"),
}
# Job lambdas the game calls every tick while the job runs.
JOB_TICK_RX = re.compile(r"(tickAction|tickIntervalAction)\s*=|\.(AddPreTickAction|AddFailCondition|AddEndCondition|FailOn\w*)\s*\(")

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
SRC = os.path.join(ROOT, "Source", "ColonyCouncil")


# ---------------------------------------------------------------- masking -------------------------------------------
def mask(src):
    """Blanks comments and the text inside string and char literals (keeping quotes, $ and @, and the code inside
    interpolation holes), keeping every offset and line where it was."""
    out = list(src)
    n = len(src)
    holes = []  # interpolated strings whose hole we are in: [verbatim, brace depth]

    def blank(a, b):
        for k in range(a, min(b, n)):
            if out[k] != "\n":
                out[k] = " "

    def scan_string(i, interp, verb):
        start = i
        while i < n:
            c = src[i]
            if verb and c == '"' and i + 1 < n and src[i + 1] == '"':
                i += 2
                continue
            if not verb and c == "\\":
                i += 2
                continue
            if c == '"':
                blank(start, i)
                return i + 1
            if interp and c == "{":
                if i + 1 < n and src[i + 1] == "{":
                    i += 2
                    continue
                blank(start, i)
                holes.append([verb, 0])
                return i + 1
            if not verb and c == "\n":  # unterminated; stop at the line end
                blank(start, i)
                return i
            i += 1
        blank(start, n)
        return n

    i = 0
    while i < n:
        c = src[i]
        nx = src[i + 1] if i + 1 < n else ""
        if c == "/" and nx == "/":
            j = src.find("\n", i)
            j = n if j < 0 else j
            blank(i, j)
            i = j
            continue
        if c == "/" and nx == "*":
            j = src.find("*/", i + 2)
            j = n if j < 0 else j + 2
            blank(i, j)
            i = j
            continue
        if c == "'":
            j = i + 1
            if j < n and src[j] == "\\":
                j += 2
            else:
                j += 1
            while j < n and src[j] != "'" and src[j] != "\n":
                j += 1
            blank(i + 1, j)
            i = j + 1
            continue
        if c in '$@"':
            m = re.match(r'(\$@|@\$|\$|@)?"', src[i:i + 3])
            if m:
                prefix = m.group(1) or ""
                i = scan_string(i + len(m.group(0)), "$" in prefix, "@" in prefix)
                continue
        if holes:
            if c == "{":
                holes[-1][1] += 1
            elif c == "}":
                if holes[-1][1] == 0:
                    verb = holes.pop()[0]
                    i = scan_string(i + 1, True, verb)
                    continue
                holes[-1][1] -= 1
        i += 1
    return "".join(out)


# ---------------------------------------------------------------- structure -----------------------------------------
TYPE_RX = re.compile(r"\b(class|struct|interface|record|enum)\s+(\w+)(\s*<[^>{]*>)?(\s*\([^)]*\))?(\s*:\s*([^{]+?))?\s*(where\b[^{]*)?$", re.S)
METHOD_RX = re.compile(r"([\w<>\[\],\.\?\s]*?)\b(\w+)\s*(<[^<>()]*(?:<[^<>()]*>[^<>()]*)*>)?\s*\(([^()]*(?:\([^()]*\)[^()]*)*)\)\s*(:\s*(base|this)\s*\([^)]*\))?\s*(where\b[^{]*)?$", re.S)
PROP_RX = re.compile(r"([\w<>\[\],\.\?]+(?:\s*<[^{}]*>)?)\s+(\w+|this\s*\[[^\]]*\])\s*$", re.S)
ACCESSOR_RX = re.compile(r"\b(get|set|init|add|remove)\s*$")


class Method:
    __slots__ = ("file", "cls", "name", "params", "ret", "start", "end", "line", "body_start", "kind", "id", "patch", "override")

    def __init__(self, file, cls, name, params, ret, start, end, line, body_start, kind="method"):
        self.file, self.cls, self.name, self.params, self.ret = file, cls, name, params, ret
        self.start, self.end, self.line, self.body_start, self.kind = start, end, line, body_start, kind
        self.id = f"{cls}.{name}@{os.path.basename(file)}:{line}"
        self.patch = None
        self.override = False


class TypeInfo:
    def __init__(self, name, file, bases):
        self.name = name
        self.file = file
        self.bases = bases
        self.fields = {}  # member name -> type text
        self.methods = defaultdict(list)


class Unit:
    def __init__(self, path, text, code, src=SRC):
        self.path = path
        self.rel = "Source/ColonyCouncil/" + os.path.relpath(path, src).replace(os.sep, "/")
        self.text = text
        self.code = code
        self.lines = [0]
        for i, ch in enumerate(code):
            if ch == "\n":
                self.lines.append(i + 1)

    def line_of(self, pos):
        lo, hi = 0, len(self.lines) - 1
        while lo < hi:
            mid = (lo + hi + 1) // 2
            if self.lines[mid] <= pos:
                lo = mid
            else:
                hi = mid - 1
        return lo + 1


def match_close(code, pos, open_ch, close_ch):
    """Index of the bracket closing the one at pos."""
    depth = 0
    n = len(code)
    i = pos
    while i < n:
        c = code[i]
        if c == open_ch:
            depth += 1
        elif c == close_ch:
            depth -= 1
            if depth == 0:
                return i
        i += 1
    return n - 1


def clean_type(t):
    t = re.sub(r"\b(public|private|protected|internal|static|readonly|override|virtual|abstract|sealed|new|const|volatile|extern|unsafe|partial|async|event|ref|out|in|params|this)\b", " ", t)
    t = re.sub(r"\[[^\]]*\]", " ", t) if "[" in t and not t.strip().endswith("[]") else t
    t = t.strip().rstrip("?")
    t = re.sub(r"\s+", "", t)
    return t


def base_type(t):
    """List<Pawn> -> List; Foo.Bar -> Bar; Pawn[] -> Pawn[]"""
    t = t.strip().rstrip("?")
    m = re.match(r"([\w\.]+)", t)
    if not m:
        return t
    return m.group(1).split(".")[-1]


def element_type(t):
    """List<Pawn> -> Pawn; Pawn[] -> Pawn; Dictionary<A,B> -> None"""
    t = (t or "").strip().rstrip("?")
    m = re.match(r"(?:[\w\.]*(?:List|IEnumerable|HashSet|ICollection|IList|IReadOnlyList|IReadOnlyCollection|Queue|Stack|SortedSet|LinkedList))<\s*([\w\.]+(?:<[^<>]*>)?)\s*>$", t)
    if m:
        return m.group(1)
    m = re.match(r"(?:[\w\.]+)<\s*([\w\.]+)\s*>$", t)
    if m and not re.match(r"(Func|Action|Predicate|Nullable)\b", t):
        return m.group(1).split(".")[-1]
    m = re.match(r"([\w\.]+)\[\]$", t)
    if m:
        return m.group(1).split(".")[-1]
    return None


def parse_unit(unit, types, methods):
    code = unit.code
    stack = []  # frames: (kind, name, open_pos, extra)
    boundary = 0
    i = 0
    n = len(code)
    pending_types = []
    while i < n:
        c = code[i]
        if c not in "{};":
            i += 1
            continue
        parent = stack[-1] if stack else ("file", None, 0, None)
        header = code[boundary:i]
        if c == "{":
            kind, name, extra = "inner", None, None
            if parent[0] in ("file", "ns", "type"):
                h = re.sub(r"\[[^\[\]]*(\[[^\[\]]*\][^\[\]]*)*\]", " ", header)  # attributes
                h = h.strip()
                tm = TYPE_RX.search(h)
                if re.search(r"\bnamespace\s+[\w\.]+\s*$", h):
                    kind = "ns"
                elif tm and not re.search(r"\bnew\b[^;]*$", h[:tm.start()]):
                    kind = "type"
                    name = tm.group(2)
                    bases = [base_type(b) for b in re.split(r",(?![^<]*>)", tm.group(6) or "") if b.strip()]
                    info = types.get(name)
                    if info is None:
                        info = TypeInfo(name, unit.rel, bases)
                        types[name] = info
                    else:
                        info.bases = list(dict.fromkeys(info.bases + bases))
                    extra = info
                    if tm.group(1) == "enum":
                        kind = "data"
                elif parent[0] == "type" and "=" not in re.sub(r"\([^()]*\)", "", h):
                    mm = METHOD_RX.search(h)
                    if mm and mm.group(2) not in KEYWORDS:
                        kind = "method"
                        name = mm.group(2)
                        ret = clean_type(mm.group(1)) if mm.group(1) else ""
                        extra = (mm.group(4), ret)
                    else:
                        pm = PROP_RX.search(h)
                        if pm:
                            kind = "prop"
                            name = pm.group(2)
                            ptype = clean_type(pm.group(1))
                            parent[3].fields[name] = ptype
                            extra = ptype
                        else:
                            kind = "data"
                else:
                    kind = "data"
            elif parent[0] == "prop":
                if ACCESSOR_RX.search(header):
                    kind = "method"
                    acc = ACCESSOR_RX.search(header).group(1)
                    name = parent[1] if acc == "get" else parent[1] + "." + acc
                    extra = ("", parent[3])
                else:
                    kind = "data"
            elif parent[0] in ("method", "inner"):
                kind = "inner"
            stack.append((kind, name, i, extra))
            boundary = i + 1
        elif c == "}":
            if stack:
                frame = stack.pop()
                if frame[0] == "method":
                    owner = next((f for f in reversed(stack) if f[0] == "type"), None)
                    if owner is not None:
                        cls = owner[3]
                        params, ret = frame[3] if frame[3] else ("", "")
                        # Header start: after the previous boundary before the open brace.
                        hstart = max(code.rfind(";", 0, frame[2]), code.rfind("}", 0, frame[2]), code.rfind("{", 0, frame[2])) + 1
                        m = Method(unit.rel, cls.name, frame[1], params, ret, hstart, i, unit.line_of(frame[2]), frame[2] + 1)
                        methods.append(m)
                        cls.methods[frame[1]].append(m)
            boundary = i + 1
        else:  # ;
            if parent[0] == "type":
                h = re.sub(r"\[[^\[\]]*(\[[^\[\]]*\][^\[\]]*)*\]", " ", header).strip()
                if "=>" in h:
                    lhs = h[:h.index("=>")]
                    mm = METHOD_RX.search(lhs.rstrip())
                    if mm and mm.group(2) not in KEYWORDS and "(" in lhs:
                        ret = clean_type(mm.group(1)) if mm.group(1) else ""
                        hstart = boundary
                        m = Method(unit.rel, parent[3].name, mm.group(2), mm.group(4), ret, hstart, i, unit.line_of(hstart + len(header) - len(header.lstrip())), boundary + header.index("=>") + 2, "expr")
                        methods.append(m)
                        parent[3].methods[mm.group(2)].append(m)
                    else:
                        pm = PROP_RX.search(lhs.rstrip())
                        if pm:
                            ptype = clean_type(pm.group(1))
                            parent[3].fields[pm.group(2)] = ptype
                            hstart = boundary
                            m = Method(unit.rel, parent[3].name, pm.group(2), "", ptype, hstart, i, unit.line_of(hstart + len(header) - len(header.lstrip())), boundary + header.index("=>") + 2, "expr")
                            methods.append(m)
                            parent[3].methods[pm.group(2)].append(m)
                elif "(" not in h.split("=")[0]:
                    # A field: "Type name = ...;" or "Type a, b;"
                    decl = h.split("=")[0].strip()
                    fm = re.search(r"([\w<>\[\],\.\?]+(?:<[^;]*>)?)\s+(\w+)\s*$", decl)
                    if fm:
                        parent[3].fields[fm.group(2)] = clean_type(fm.group(1))
            elif parent[0] == "prop":
                acc = ACCESSOR_RX.search(header.split("=>")[0]) if "=>" in header else None
                if acc and acc.group(1) == "get":
                    hstart = boundary
                    m = Method(unit.rel, next((f for f in reversed(stack) if f[0] == "type"))[3].name, parent[1], "", parent[3], hstart, i, unit.line_of(boundary + len(header) - len(header.lstrip())), boundary + header.index("=>") + 2, "expr")
                    methods.append(m)
                    next((f for f in reversed(stack) if f[0] == "type"))[3].methods[parent[1]].append(m)
            boundary = i + 1
        i += 1


# ---------------------------------------------------------------- analysis ------------------------------------------
LOOP_RX = re.compile(r"\b(foreach|for|while)\s*\(")
DO_RX = re.compile(r"\bdo\s*\{")
LINQ_LOOP_RX = re.compile(r"\.(Where|Select|SelectMany|Any|All|Count|Sum|Max|Min|Average|First|FirstOrDefault|Last|LastOrDefault|OrderBy|OrderByDescending|ThenBy|GroupBy|Exists|Find|FindAll|FindIndex|RemoveAll|TrueForAll|ForEach|Aggregate|MaxBy|MinBy|RandomElementByWeight|TryRandomElementByWeight|TryRandomElement|ToDictionary|SkipWhile|TakeWhile)\s*\(")
CALL_RX = re.compile(r"((?:[A-Za-z_]\w*\s*(?:<[\w\s,<>\.\[\]]*>)?\s*(?:\([^()]*\))?\s*\??\.\s*)*)\b([A-Za-z_]\w*)\s*(?:<[\w\s,<>\.\[\]]*>)?\s*\(")
IF_RX = re.compile(r"\b(if|while)\s*\(")
EARLY_RX = re.compile(r"\bif\s*\(")


def statement_end(code, pos, end):
    """End index (inclusive) of the statement starting at pos."""
    n = min(len(code), end)
    i = pos
    while i < n and code[i].isspace():
        i += 1
    if i >= n:
        return n - 1
    if code[i] == "{":
        return match_close(code, i, "{", "}")
    m = re.match(r"(if|for|foreach|while|using|lock|fixed)\s*\(", code[i:i + 12])
    if m:
        p = code.index("(", i)
        q = match_close(code, p, "(", ")")
        e = statement_end(code, q + 1, end)
        if m.group(1) == "if":
            k = e + 1
            while k < n and code[k].isspace():
                k += 1
            if code.startswith("else", k) and (k + 4 >= n or not (code[k + 4].isalnum() or code[k + 4] == "_")):
                e = statement_end(code, k + 4, end)
        return e
    if code.startswith("else", i):
        return statement_end(code, i + 4, end)
    if code.startswith("try", i) and re.match(r"try\s*\{", code[i:i + 10]):
        b = code.index("{", i)
        e = match_close(code, b, "{", "}")
        while True:
            k = e + 1
            while k < n and code[k].isspace():
                k += 1
            if code.startswith("catch", k) or code.startswith("finally", k):
                b = code.index("{", k)
                e = match_close(code, b, "{", "}")
                continue
            return e
    depth = 0
    while i < n:
        c = code[i]
        if c in "({[":
            depth += 1
        elif c in ")}]":
            depth -= 1
            if depth < 0:
                return i - 1
        elif c == ";" and depth == 0:
            return i
        i += 1
    return n - 1


def receiver_before(code, pos):
    """The expression just before a '.' at pos (a.b(c).d -> 'a.b(c).d')."""
    i = pos - 1
    while i >= 0:
        c = code[i]
        if c.isalnum() or c in "_.?!":
            i -= 1
        elif c in ")]>":
            o = {")": "(", "]": "[", ">": "<"}[c]
            depth = 0
            while i >= 0:
                if code[i] == c:
                    depth += 1
                elif code[i] == o:
                    depth -= 1
                    if depth == 0:
                        break
                i -= 1
            i -= 1
        elif c.isspace():
            j = i
            while j >= 0 and code[j].isspace():
                j -= 1
            if j >= 0 and code[j] == ".":
                i = j
            else:
                break
        else:
            break
    return code[i + 1:pos].strip()


FOREIGN = "?"
LINQ_ELEMENT = set("First FirstOrDefault Last LastOrDefault Single SingleOrDefault ElementAt ElementAtOrDefault RandomElement RandomElementByWeight RandomElementByWeightWithFallback RandomElementWithFallback MaxBy MinBy Find".split())
LINQ_SEQUENCE = set("Where OrderBy OrderByDescending ThenBy ThenByDescending ToList ToArray Distinct Take Skip Reverse InRandomOrder FindAll Concat Union Except Intersect AsEnumerable ToHashSet".split())


def dict_value(t):
    m = re.match(r"(?:[\w\.]*Dictionary)<\s*([\w\.]+(?:<[^<>]*>)?)\s*,\s*([\w\.]+(?:<[^<>]*>)?)\s*>$", (t or "").strip())
    return m.group(2) if m else None


def dict_key(t):
    m = re.match(r"(?:[\w\.]*Dictionary)<\s*([\w\.]+(?:<[^<>]*>)?)\s*,\s*([\w\.]+(?:<[^<>]*>)?)\s*>$", (t or "").strip())
    return m.group(1) if m else None


def domain_of(expr):
    # What a chain goes over is what its start goes over: the arguments of calls along it don't count.
    flat = expr
    for _ in range(4):
        flat = re.sub(r"\([^()]*\)", "()", flat)
    expr = flat
    for rx, n, what in DOMAINS:
        if re.search(rx, expr):
            return n, what
    return DEFAULT_DOMAIN


PERIOD_NAMES = {
    "GenDate.TicksPerHour": 2500, "TicksPerHour": 2500, "Sched.HourTicks": 2500, "HourTicks": 2500, "GenDate.TicksPerDay": 60000,
    "TicksPerDay": 60000, "Sched.DayTicks": 60000, "Sched.RareTicks": 250, "RareTicks": 250, "GenTicks.TickRareInterval": 250,
    "GenTicks.TickLongInterval": 2000, "Sched.PeopleLogPeriod": 1000, "GenDate.TicksPerQuadrum": 900000, "GenDate.TicksPerYear": 3600000,
    "GenDate.TicksPerTwelfth": 300000,
}


def period_value(tok, consts):
    tok = tok.strip().strip("()")
    if re.fullmatch(r"\d+", tok):
        return int(tok)
    if tok in PERIOD_NAMES:
        return PERIOD_NAMES[tok]
    m = re.fullmatch(r"(\w[\w\.]*)\s*\*\s*(\d+)", tok)
    if m and m.group(1) in PERIOD_NAMES:
        return PERIOD_NAMES[m.group(1)] * int(m.group(2))
    m = re.fullmatch(r"(\d+)\s*\*\s*(\w[\w\.]*)", tok)
    if m and m.group(2) in PERIOD_NAMES:
        return PERIOD_NAMES[m.group(2)] * int(m.group(1))
    short = tok.split(".")[-1]
    if short in consts:
        return consts[short]
    return None


def gate_factor(cond, negated, consts, now_names=("now", "t", "tick", "ticks", "TicksGame")):
    """How much a condition thins out the code it guards (as a factor), and what it is. negated: the code runs when the
    condition is false (an early return)."""
    f = 1.0
    notes = []
    c = re.sub(r"\s+", " ", cond)
    # tick % P == / != K
    for m in re.finditer(r"%\s*([\w\.]+(?:\s*\*\s*\d+)?|\(\s*[\w\.]+\s*\*\s*\d+\s*\))\s*(==|!=)", c):
        p = period_value(m.group(1), consts)
        if p and p > 1:
            eq = m.group(2) == "=="
            if eq != negated:
                f = min(f, 1.0 / p)
                notes.append(f"every {p} ticks")
    if re.search(r"\bhourTick\s*(==|!=)\s*Sched\.", c) or re.search(r"\bSched\.Hourly\s*\(", c):
        runs = (re.search(r"hourTick\s*==", c) or (re.search(r"(?<!!)Sched\.Hourly", c) and not re.search(r"!\s*Sched\.Hourly", c)))
        if bool(runs) != negated:
            f = min(f, 1.0 / TICKS_PER_HOUR)
            notes.append("hourly")
    m = re.search(r"(!?)\s*Sched\.Every\s*\(\s*\w+\s*,\s*([^,]+),", c)
    if m:
        p = period_value(m.group(2), consts)
        if p and (m.group(1) == "!") == negated:
            f = min(f, 1.0 / p)
            notes.append(f"every {p} ticks")
    m = re.search(r"Sched\.SliceTurn\s*\(\s*\w+\s*,\s*([^,]+),\s*([^,]+),", c)
    if m:
        p = period_value(m.group(1), consts)
        if p:
            f = min(f, 1.0 / p)
            notes.append(f"a slice every {p} ticks")
    m = re.search(r"IsHashIntervalTick\s*\(\s*([^,)]+)", c)
    if m:
        p = period_value(m.group(1), consts)
        if p and not re.search(r"!\s*[\w\.]*IsHashIntervalTick", c) != (not negated):
            f = min(f, 1.0 / p)
            notes.append(f"every {p} ticks")
    if re.search(r"Sched\.FirstHourOfDay\s*\(", c):
        neg = bool(re.search(r"!\s*Sched\.FirstHourOfDay", c))
        if neg == negated:
            f *= 1.0 / 24
            notes.append("first hour of the day")
    m = re.search(r"(!?)\s*Rand\.Chance\s*\(\s*([\d\.]+)f?\s*\)", c)
    if m:
        neg = m.group(1) == "!"
        p = float(m.group(2))
        if 0 < p < 1:
            f *= p if neg == negated else 1 - p
            notes.append(f"chance {p:g}")
    # Either way round, these guard code that runs seldom: a click, a hover, dev mode, a cache being rebuilt.
    for rx, factor, note in SELDOM:
        if re.search(rx, c):
            f *= factor
            notes.append(note)
            break
    return f, notes


SELDOM = [
    (r"(?i)\b\w*pending\w*\b|\bjustLoaded\b|\bbuiltSomething\b|\bafterLoad\w*", 1e-3, "once after loading or building"),
    (r"(?i)\b(now|ticks?|TicksGame|t)\s*(>=|>|<=|<)\s*[\w\.]*(deadline|until|due|ends?\w*|expir\w*|starts?\w*|next\w*)\b", 0.05, "when due"),
    (r"(?i)\b(ended|queue\w*|later|toDo|todo|deferred)\s*\.\s*(Count\s*(>|!=)\s*0|Any\s*\(\s*\))", 1e-2, "only when something is queued"),
    (r"Button\w*\s*\(|Clicked|MouseDown|KeyDownEvent|GetKeyDown|\.Pressed\b|ClickedInside|\.IsDownEvent", 1e-4, "on a click"),
    (r"Prefs\.DevMode|DebugSettings\.|godMode|DevMode\b", 1e-3, "dev mode"),
    (r"Mouse\.IsOver\s*\(", 0.05, "on hover"),
    (r"realtimeSinceStartup|Time\.time\b|Time\.frameCount|\bframeCount\b", 1 / 60, "cached for a while"),
    (r"(?i)\bcache\w*|\w+cache\b|\w*cached\w*|\bdirty\b|\w+Dirty\b|\bstale\b|\bbuiltAt\b|\brebuild", 1 / 30, "cached"),
]

# Calls that start, end or announce something: a pass checks every time, and these follow now and then.
EVENT_RX = re.compile(r"(Begin|Start|TryStart|End|Conclude|Finish|Complete|Fail|Fallback|Grant|Fire|TryFire|TryExecute|Send\w*Letter|Letter|Announce|Arrive|Leave|Spawn|Notify_\w+|Hold\w+|Settle\w*|Resolve\w*|Give\w*Thought|GiveMemory|Remember|Tell|Retire|Expire\w*|Break\w*|Kill)(\w*)$")
EVENT_SHARE = 0.05

# An early "return" on these means what follows is worked out once and kept (a lazy build, a memo).
MEMO_HIT_RX = re.compile(r"!=\s*null|TryGetValue\s*\(|\b(built|ready|initialized|inited|resolved|looked|loaded|computed|indexed)\b")

# Lambdas that run later (on a click, on hover, when a job ends) rather than where they are written.
DEFERRED_CALLS = set("""FloatMenuOption TipRegion TipSignal CreateConfirmation Dialog_MessageBox DiaOption TabRecord Command_Action
Command_Toggle AddFinishAction AddPreInitAction TransitionAction_Custom QueueLongEvent ExecuteWhenFinished Later
Dialog_NameRoom StuffMenu Confirm Dialog_Confirm FloatMenu Notify_Later""".split())
DEFERRED_ASSIGN = set("action act click onClick defaultAction initAction finishAction toggleAction mouseoverGuiAction tooltip getter onSelect".split())
IMMEDIATE_CALLS = set("Safe Try Run Do SafeFloat SafeInt Guard Measure Time Exists".split())


class Analyzer:
    def __init__(self, units, types, methods):
        self.units = {u.rel: u for u in units}
        self.types = types
        self.methods = methods
        for m in methods:
            m.override = re.search(r"\boverride\b", self.units[m.file].code[m.start:m.body_start]) is not None
        self.by_name = defaultdict(list)
        for m in methods:
            self.by_name[m.name].append(m)
        self.children = defaultdict(list)
        for t in types.values():
            for b in t.bases:
                self.children[b].append(t.name)
        self.consts = {}
        for u in units:
            for mm in re.finditer(r"\bconst\s+int\s+(\w+)\s*=\s*([^;]+);", u.code):
                v = mm.group(2).strip()
                if re.fullmatch(r"\d+", v):
                    self.consts[mm.group(1)] = int(v)
                else:
                    pv = period_value(v, {})
                    if pv:
                        self.consts[mm.group(1)] = pv
        self.local_cache = {}
        self.incl_cache = {}
        self.stack = set()

    # ---- types ----
    def base_chain(self, tname):
        """Every base type name (the mod's and the first game type it derives from)."""
        out = []
        todo = [tname]
        while todo:
            t = todo.pop(0)
            if t in out:
                continue
            out.append(t)
            if t in self.types:
                todo.extend(self.types[t].bases)
        return out[1:]

    def type_chain(self, tname):
        seen = []
        todo = [tname]
        while todo:
            t = todo.pop(0)
            if t in seen or t not in self.types:
                continue
            seen.append(t)
            todo.extend(self.types[t].bases)
        return seen

    def subtypes(self, tname):
        out = []
        todo = [tname]
        while todo:
            t = todo.pop()
            for c in self.children.get(t, []):
                if c not in out:
                    out.append(c)
                    todo.append(c)
        return out

    def member_type(self, tname, member):
        for t in self.type_chain(tname):
            ft = self.types[t].fields.get(member)
            if ft:
                return ft
            ms = self.types[t].methods.get(member)
            if ms and ms[0].ret:
                return ms[0].ret
        return None

    def methods_in(self, tname, name, virtual=True):
        for t in self.type_chain(tname):
            ms = self.types[t].methods.get(name)
            if ms:
                found = list(ms)
                if virtual:
                    for s in self.subtypes(tname):
                        found.extend(self.types[s].methods.get(name, []))
                return found
        if virtual:
            found = []
            for s in self.subtypes(tname):
                found.extend(self.types[s].methods.get(name, []))
            return found
        return []

    def local_types(self, m):
        """Names in scope in a method and their declared types (full, with generic arguments): parameters, locals,
        foreach variables and lambda parameters over a typed collection. A name whose type can't be told maps to FOREIGN."""
        u = self.units[m.file]
        body = u.code[m.body_start:m.end]
        env = {}
        for p in re.split(r",(?![^<]*>)", m.params or ""):
            p = re.sub(r"\[[^\]]*\]", "", p).strip()
            p = re.sub(r"\b(this|ref|out|in|params)\b", "", p).split("=")[0].strip()
            pm = re.match(r"([\w<>\[\],\.\?\s]+?)\s+(\w+)$", p)
            if pm:
                env[pm.group(2)] = clean_type(pm.group(1))
        for lm in re.finditer(r"(?<![\w\.])([A-Za-z_][\w\.]*(?:<[\w\s,\.<>\(\)\[\]]*>)?(?:\[\])?\??)\s+(\w+)\s*(=(?!=)|;|\bin\b|\))", body):
            tname = lm.group(1)
            if tname in KEYWORDS or tname in ("return", "else", "new", "await", "yield", "case", "goto", "throw") or tname == "var":
                continue
            if not (tname[0].isupper() or tname in ("int", "float", "bool", "string", "long", "double", "object")):
                continue
            env.setdefault(lm.group(2), clean_type(tname))
        for lm in re.finditer(r"\bvar\s+(\w+)\s*=\s*([^;]+);", body):
            if lm.group(1) in env:
                continue
            rhs = lm.group(2).strip()
            nm = re.match(r"new\s+([\w\.]+(?:<[^()]*>)?)", rhs)
            if nm:
                env[lm.group(1)] = clean_type(nm.group(1))
                continue
            rhs = re.split(r"\s(\?\?|\?|&&|\|\||==|!=)\s", rhs)[0]
            t = self.chain_type(rhs, m, env)
            env[lm.group(1)] = t or FOREIGN
        for lm in re.finditer(r"\bforeach\s*\(\s*var\s+(\w+)\s+in\s+", body):
            p = body.index("(", lm.start())
            q = match_close(body, p, "(", ")")
            src = body[lm.end():q].strip()
            st = self.chain_type(src, m, env)
            et = element_type(st) if st else None
            env.setdefault(lm.group(1), et or FOREIGN)
        for lm in LINQ_LOOP_RX.finditer(body):
            pm = re.match(r"\s*\(\s*(\w+)\s*=>", body[lm.end() - 1:])
            if not pm or pm.group(1) in env:
                continue
            recv = receiver_before(body, lm.start())
            st = self.chain_type(recv, m, env)
            et = element_type(st) if st else None
            env[pm.group(1)] = et or FOREIGN
        return env

    def chain_type(self, recv, m, env):
        """The type (full text) a receiver expression has, as far as it can be told; None when it can't."""
        recv = re.sub(r"\s+", "", recv).replace("?.", ".").replace("!.", ".").rstrip(".").strip("()")
        if not recv:
            return m.cls
        parts = []
        depth = 0
        cur = ""
        for ch in recv:
            if ch in "(<[":
                depth += 1
            elif ch in ")>]":
                depth -= 1
            if ch == "." and depth == 0:
                parts.append(cur)
                cur = ""
            else:
                cur += ch
        parts.append(cur)
        t = None
        for k, part in enumerate(parts):
            call = "(" in part
            index = part.endswith("]") and not call
            nm = re.match(r"(\w+)", part)
            if not nm:
                return None
            name = nm.group(1)
            if k == 0:
                if name == "this":
                    t = m.cls
                elif name in env:
                    t = env[name]
                elif call and self.methods_in(m.cls, name, False):
                    t = self.methods_in(m.cls, name, False)[0].ret or None
                elif self.member_type(m.cls, name):
                    t = self.member_type(m.cls, name)
                elif name in self.types:
                    t = name
                else:
                    return None
            else:
                if t is None or t == FOREIGN:
                    return None
                bt = base_type(t)
                if bt in self.types:
                    if call:
                        ms = self.methods_in(bt, name, False)
                        t = (ms[0].ret or None) if ms else None
                    else:
                        t = self.member_type(bt, name)
                    if t is None:
                        return None
                else:
                    et = element_type(t)
                    if call and name in LINQ_ELEMENT and et:
                        t = et
                    elif call and name in LINQ_SEQUENCE and et:
                        t = f"IEnumerable<{et}>"
                    elif not call and name in ("Values",) and dict_value(t):
                        t = f"IEnumerable<{dict_value(t)}>"
                    elif not call and name in ("Keys",) and dict_key(t):
                        t = f"IEnumerable<{dict_key(t)}>"
                    else:
                        return None
            if index and t:
                t = element_type(t) or dict_value(t) or None
                if t is None:
                    return None
        return t

    def resolve_chain(self, recv, m, env):
        t = self.chain_type(recv, m, env)
        if t is None or t == FOREIGN:
            return None
        return base_type(t)

    def resolve_call(self, m, recv, name, env):
        recv = recv.strip()
        if name in KEYWORDS:
            return []
        if not recv or recv in ("this.", "base."):
            ms = self.methods_in(m.cls, name, recv != "base.")
            if ms:
                return ms
            # an outer class's member (nested types share a file)
            cands = [x for x in self.by_name.get(name, []) if x.file == m.file]
            if cands:
                return cands
            return [] if name in COMMON else self.unique(name)
        t = self.resolve_chain(recv, m, env)
        if t and t in self.types:
            return self.methods_in(t, name, True)
        # A game or library type, or one that can't be told: only the mod's own extension methods, by name.
        ext = [x for x in self.by_name.get(name, []) if x.params.strip().startswith("this ")]
        return ext

    def unique(self, name):
        cands = self.by_name.get(name, [])
        if 0 < len(cands) <= 3:
            return cands
        return []

    # ---- loops and gates inside one method ----
    def regions(self, m):
        """Loop regions (start, end, factor, what) and guarded regions (start, end, factor, notes)."""
        u = self.units[m.file]
        code = u.code
        b, e = m.body_start, m.end
        loops = []
        for lm in LOOP_RX.finditer(code, b, e):
            p = code.index("(", lm.start())
            q = match_close(code, p, "(", ")")
            header = code[p + 1:q]
            if lm.group(1) == "while":
                k = q + 1
                while k < e and code[k].isspace():
                    k += 1
                if k < e and code[k] == ";":
                    continue  # do ... while (...);
            body_end = statement_end(code, q + 1, e)
            if lm.group(1) == "foreach":
                src = header.split(" in ", 1)[1] if " in " in header else header
                n, what = domain_of(src)
            elif lm.group(1) == "for":
                parts = header.split(";")
                cond = parts[1] if len(parts) > 1 else header
                lim = re.search(r"<=?\s*([^;]+)", cond)
                lim = lim.group(1).strip() if lim else cond
                if re.fullmatch(r"\d+", lim):
                    n, what = int(lim), "times"
                elif lim.split(".")[-1] in self.consts:
                    n, what = self.consts[lim.split(".")[-1]], "times"
                else:
                    n, what = domain_of(lim)
            else:
                n, what = 8, "while"
            loops.append((q + 1, body_end, n, what, lm.start()))
        for lm in DO_RX.finditer(code, b, e):
            bb = code.index("{", lm.start())
            loops.append((bb, match_close(code, bb, "{", "}"), 8, "do", lm.start()))
        for lm in LINQ_LOOP_RX.finditer(code, b, e):
            p = code.index("(", lm.end() - 1)
            q = match_close(code, p, "(", ")")
            inner = code[p + 1:q]
            if "=>" not in inner and not re.match(r"\s*\w+\s*\)?$", inner):
                continue
            recv = receiver_before(code, lm.start())
            n, what = domain_of(recv)
            loops.append((p, q, n, what + " (lambda)", lm.start()))
        gates = []
        # Lambdas: deferred ones (run on a click or a hover) and the UI cache's makers (run once a second or so).
        for am in re.finditer(r"=>", code[b:e]):
            pos = b + am.start()
            i = pos - 1
            d = 0
            while i >= b:
                ch = code[i]
                if ch in ")}]":
                    d += 1
                elif ch in "({[":
                    if d == 0:
                        break
                    d -= 1
                elif ch == ";" and d == 0:
                    i = b - 1
                    break
                i -= 1
            params = code[max(b, pos - 40):pos]
            empty = re.search(r"\(\s*\)\s*$", params) is not None
            kind = None
            if i >= b and code[i] == "(":
                nm = re.search(r"([\w\.]+)\s*(<[^()]*>)?\s*$", code[b:i])
                full = nm.group(1) if nm else ""
                call = full.split(".")[-1]
                if call in DEFERRED_CALLS or re.match(r"Dialog_|Command_", call):
                    kind = (1e-4, "on a click")
                elif call == "Get" and full.endswith("UiCache.Get"):
                    kind = (1 / 60, "cached a second")
                elif empty and call not in IMMEDIATE_CALLS and not LINQ_LOOP_RX.match("." + call + "("):
                    kind = (1e-2, "a callback")
            else:
                pre = code[(i + 1 if i >= b else b):pos]
                asg = re.search(r"(\w+)\s*=\s*[^=;]*$", pre)
                if asg and asg.group(1) in DEFERRED_ASSIGN:
                    kind = (1e-4, "on a click")
                elif re.search(r"\bdelegate\b", pre):
                    kind = (1e-2, "a callback")
            if kind is None:
                continue
            k = pos + 2
            while k < e and code[k].isspace():
                k += 1
            if k < e and code[k] == "{":
                lend = match_close(code, k, "{", "}")
            else:
                lend = k
                dd = 0
                while lend < e:
                    ch = code[lend]
                    if ch in "({[":
                        dd += 1
                    elif ch in ")}]":
                        if dd == 0:
                            break
                        dd -= 1
                    elif ch in ",;" and dd == 0:
                        break
                    lend += 1
            gates.append((k, lend, kind[0], [kind[1]]))
        slice_vars = {}
        for sm in re.finditer(r"\b(\w+)\s*=\s*Sched\.SliceTurn\s*\(\s*\w+\s*,\s*([^,]+),\s*([^,]+),", code[b:e]):
            period = period_value(sm.group(2), self.consts)
            slices = period_value(sm.group(3), self.consts)
            if period and slices:
                slice_vars[sm.group(1)] = max(1, period // slices)
        for im in IF_RX.finditer(code, b, e):
            if im.group(1) != "if":
                continue
            p = code.index("(", im.start())
            q = match_close(code, p, "(", ")")
            cond = code[p + 1:q]
            k = q + 1
            then_end = statement_end(code, k, e)
            stmt = code[k:then_end + 1].strip()
            sv = slice_vars.get(re.sub(r"[^\w]", "", cond.split("<")[0])) if re.search(r"\w+\s*<\s*0\s*$", cond.strip()) else None
            if sv and re.fullmatch(r"return\b[^;]*;", stmt):
                gates.append((then_end + 1, self.enclosing_block_end(code, im.start(), b, e), 1.0 / sv, [f"a slice every {sv} ticks"]))
                continue
            memo = None
            if re.fullmatch(r"return\b[^;]*;", stmt) and (MEMO_HIT_RX.search(cond) and "==" not in cond):
                memo = (1e-3, ["built once"])
            nm = re.search(r"\b([\w\.]+)\s*==\s*null\b", cond)
            if nm and re.search(r"(?<![\w\.])" + re.escape(nm.group(1).split(".")[-1]) + r"\s*(=(?!=)|\?\?=)", stmt) and not stmt.startswith("return"):
                memo = (1e-3, ["built once"])
            if memo:
                if stmt.startswith("return"):
                    gates.append((then_end + 1, self.enclosing_block_end(code, im.start(), b, e), memo[0], memo[1]))
                else:
                    gates.append((k, then_end, memo[0], memo[1]))
                continue
            if stmt == "continue;" and not re.fullmatch(r"[\s\w\.\?!|&()]*(==|!=)\s*null[\s|&()!\w\.=]*|\s*!?\s*[\w\.\?]+\s*", cond):
                # A filter at the top of a loop: what follows runs for some of the items only.
                f, notes = gate_factor(cond, True, self.consts)
                blk_end = self.enclosing_block_end(code, im.start(), b, e)
                gates.append((then_end + 1, blk_end, f * FILTER_PASS, notes + ["filtered"]))
                continue
            if re.fullmatch(r"(return\b[^;]*|continue|break);", stmt):
                f, notes = gate_factor(cond, True, self.consts)
                if f < 1.0:
                    # the rest of the enclosing block
                    blk_end = self.enclosing_block_end(code, im.start(), b, e)
                    gates.append((then_end + 1, blk_end, f, notes))
            else:
                f, notes = gate_factor(cond, False, self.consts)
                if f < 1.0:
                    gates.append((k, then_end, f, notes))
        # catch blocks run on an error only.
        for cm in re.finditer(r"\bcatch\b\s*(\([^)]*\))?\s*(when\s*\([^)]*\)\s*)?\{", code[b:e]):
            k = b + cm.end() - 1
            gates.append((k, match_close(code, k, "{", "}"), 1e-4, ["on an error"]))
        # x ??= Build(): built once.
        for qm in re.finditer(r"\?\?=", code[b:e]):
            k = b + qm.end()
            gates.append((k, statement_end(code, k, e), 1e-3, ["built once"]))
        # A call guarded by "&&" in an expression: a ? b : c and cond && Call() are left alone.
        return loops, gates

    @staticmethod
    def enclosing_block_end(code, pos, b, e):
        depth = 0
        i = pos
        while i < e:
            c = code[i]
            if c == "{":
                depth += 1
            elif c == "}":
                if depth == 0:
                    return i
                depth -= 1
            i += 1
        return e

    @staticmethod
    def factor_at(pos, loops, gates):
        f = 1.0
        what = []
        for s, t, n, w, _ in loops:
            if s <= pos <= t:
                f *= n
                what.append(w)
        f = min(f, LOOP_CAP)
        g = 1.0
        notes = []
        for s, t, gf, gn in gates:
            if s <= pos <= t:
                g *= gf
                notes.extend(gn)
        return f, what, g, notes

    def local(self, m):
        """The method's own work: (total weighted, {category: weighted}, [(category, line, multiplier)], calls)."""
        if m.id in self.local_cache:
            return self.local_cache[m.id]
        u = self.units[m.file]
        code = u.code
        loops, gates = self.regions(m)
        total = 0.0
        cats = defaultdict(float)
        sites = []
        for cat, w, rx in PATTERNS:
            for pm in re.finditer(rx, code[m.body_start:m.end]):
                pos = m.body_start + pm.start()
                f, what, g, _ = self.factor_at(pos, loops, gates)
                v = w * f * g
                total += v
                cats[cat] += v
                sites.append((cat, u.line_of(pos), f, v))
        for s, t, n, w, start in loops:
            f, _, g, _ = self.factor_at(start, loops, gates)
            total += LOOP_STEP * n * f * g
        env = self.local_types(m)
        locals_ = set(lf.group(1) for lf in re.finditer(r"(?:^|[;{}]\s*)(?:static\s+)?(?:[\w<>\[\],\.\?]+)\s+(\w+)\s*\([^()]*\)\s*(?:\{|=>)", code[m.body_start:m.end]))
        calls = []
        seen_pos = set()
        body = code[m.body_start:m.end]
        for cm in CALL_RX.finditer(body):
            name = cm.group(2)
            if name in KEYWORDS:
                continue
            pos = m.body_start + cm.start(2)
            if pos in seen_pos:
                continue
            seen_pos.add(pos)
            before = body[:cm.start()].rstrip()
            if before.endswith("new") or re.search(r"\b(new|class|void|static)\s*$", before):
                continue
            recv = cm.group(1) or ""
            if not recv.strip() and name in locals_:
                continue
            # a method declared inside (a local function) is part of this one
            targets = [x for x in self.resolve_call(m, recv, name, env) if x is not m]
            if not targets:
                continue
            f, what, g, notes = self.factor_at(pos, loops, gates)
            if EVENT_RX.match(name):
                # Starting, ending or announcing something happens once in a while, not every time the pass runs.
                g *= EVENT_SHARE
                notes = notes + ["an event"]
            calls.append((targets, f * g, u.line_of(pos), name, what, notes, f, g))
        # Method groups passed along (Safe("calendar", Hourly), Safe("dilemmas", dilemmas.Hourly)) run as calls.
        for gm in re.finditer(r"[(,]\s*((?:[A-Za-z_]\w*\s*\.\s*)*)([A-Za-z_]\w*)\s*(?=[,)])", body):
            name = gm.group(2)
            recv = gm.group(1) or ""
            if name in KEYWORDS or name in env or name in locals_ or name in ("null", "true", "false") or name[0].isdigit():
                continue
            if not recv and self.member_type(m.cls, name) and not self.methods_in(m.cls, name, False):
                continue
            pos = m.body_start + gm.start(2)
            if not recv:
                targets = [x for x in self.methods_in(m.cls, name, True) if x is not m]
            else:
                t = self.resolve_chain(recv, m, env)
                targets = self.methods_in(t, name, True) if t and t in self.types else []
            if not targets or all(t.kind == "expr" and not t.params.strip() and t.ret and "(" not in self.units[t.file].code[t.start:t.body_start] for t in targets):
                continue
            f, what, g, notes = self.factor_at(pos, loops, gates)
            calls.append((targets, f * g, u.line_of(pos), name, what, notes + ["passed along"], f, g))
        res = (total, dict(cats), sites, calls)
        self.local_cache[m.id] = res
        return res

    def build_graph(self):
        """The call graph, its strongly connected parts (calls inside one are left out, so what remains has no cycles) and
        an order in which every method comes before the methods it calls."""
        ids = {m.id: m for m in self.methods}
        self.edges = {}
        for m in self.methods:
            self.edges[m.id] = [(targets, k) for targets, k, *_ in self.local(m)[3] if k > 0]
        # Tarjan, iteratively.
        index, low, on, stack, scc = {}, {}, set(), [], {}
        counter = [0]
        for root in ids:
            if root in index:
                continue
            work = [(root, 0)]
            while work:
                v, i = work.pop()
                if i == 0:
                    index[v] = low[v] = counter[0]
                    counter[0] += 1
                    stack.append(v)
                    on.add(v)
                succ = [t.id for targets, _ in self.edges[v] for t in targets]
                recurse = False
                while i < len(succ):
                    w = succ[i]
                    i += 1
                    if w not in index:
                        work.append((v, i))
                        work.append((w, 0))
                        recurse = True
                        break
                    if w in on:
                        low[v] = min(low[v], index[w])
                if recurse:
                    continue
                if low[v] == index[v]:
                    while True:
                        w = stack.pop()
                        on.discard(w)
                        scc[w] = v
                        if w == v:
                            break
                if work:
                    u = work[-1][0]
                    low[u] = min(low[u], low[v])
        self.scc = scc
        # Topological order of the graph without the calls inside a strongly connected part.
        indeg = defaultdict(int)
        for v in ids:
            for targets, _ in self.edges[v]:
                for t in targets:
                    if scc[t.id] != scc[v]:
                        indeg[t.id] += 1
        order = [v for v in ids if indeg[v] == 0]
        k = 0
        while k < len(order):
            v = order[k]
            k += 1
            for targets, _ in self.edges[v]:
                for t in targets:
                    if scc[t.id] != scc[v]:
                        indeg[t.id] -= 1
                        if indeg[t.id] == 0:
                            order.append(t.id)
        self.order = order
        self.ids = ids
        # Inclusive costs, callees first.
        self.incl_cache = {}
        for v in reversed(order):
            m = ids[v]
            total, cats, _, _ = self.local(m)
            cats = defaultdict(float, cats)
            for targets, kk in self.edges[v]:
                inner = [t for t in targets if scc[t.id] != scc[v]]
                if not inner:
                    continue
                n = len(targets)
                for t in inner:
                    tt, tc = self.incl_cache[t.id]
                    total += kk * tt / n
                    for c, val in tc.items():
                        cats[c] += kk * val / n
            self.incl_cache[v] = (total, dict(cats))

    def inclusive(self, m, depth=0):
        """The method's work with everything it calls (calls into virtual families take their mean)."""
        return self.incl_cache[m.id]

    def spread(self, seeds):
        """How often each method runs, given how often some run (seeds: {method id: runs}), following the calls."""
        w = defaultdict(float, seeds)
        for v in self.order:
            wv = w.get(v, 0.0)
            if wv <= 0:
                continue
            for targets, kk in self.edges[v]:
                n = len(targets)
                for t in targets:
                    if self.scc[t.id] != self.scc[v]:
                        w[t.id] += wv * kk / n
        return w

    def attribute(self, m, weight, sink, depth=0, path=None):
        """Spreads weight x the method's cost over the methods where it lands (for the hot-method list)."""
        runs = self.spread({m.id: weight})
        for v, wv in runs.items():
            if wv <= 0:
                continue
            mm = self.ids[v]
            total, cats, _, _ = self.local(mm)
            if total <= 0:
                continue
            entry = sink.setdefault(v, [mm, 0.0, defaultdict(float)])
            entry[1] += wv * total
            for c, val in cats.items():
                entry[2][c] += wv * val


# ---------------------------------------------------------------- roots ---------------------------------------------
def find_patches(units, methods):
    """Harmony patch classes and their targets, from the attributes."""
    targets = {}
    for u in units:
        for am in re.finditer(r"\[HarmonyPatch\(\s*typeof\((\w+)\)\s*,\s*(?:nameof\(\s*\w+\.(\w+)\s*\)|\"(\w+)\"|MethodType\.(\w+))", u.text):
            cm = re.compile(r"\bclass\s+(\w+)").search(u.text, am.end())
            if cm:
                member = am.group(2) or am.group(3) or (".ctor" if am.group(4) == "Constructor" else am.group(4))
                targets[cm.group(1)] = f"{am.group(1)}.{member}"
    # The Needs tab's transpiler swaps the game's tooltip calls for its own.
    targets.setdefault("Patch_NeedsTabMoodHints", "NeedsCardUtility.DrawThoughtGroup")
    return targets


def harmony_freq(target):
    if target in HARMONY_FREQ:
        return HARMONY_FREQ[target]
    return HARMONY_DEFAULT


def collect_rows(an, units, methods):
    rows = []
    patches = find_patches(units, methods)
    for m in methods:
        if m.cls in patches and m.name in ("Prefix", "Postfix", "TipRegion", "TipRegionGetter", "Finalizer"):
            tgt = patches[m.cls]
            freq = harmony_freq(tgt)
            if m.name.startswith("TipRegion"):
                freq = HARMONY_FREQ["TooltipHandler.TipRegion"]
            rows.append(dict(root=m, call=None, freq=freq, what=f"Harmony {m.name.lower()} on {tgt}", kind="patch", gate=[]))
            continue
        spec = ROOTS.get(m.name)
        if not spec:
            continue
        if m.name in ROOTS and not m.override:
            continue
        if m.name in ("Tick",) and not any(re.match(r"ScenPart|ThingComp$|Building|Thing$|ThingWithComps$|Comp", b) for b in an.base_chain(m.cls)):
            continue
        freq, what, kind = spec
        if kind == "tick":
            # Split a tick method into the passes it gates.
            total, cats, sites, calls = an.local(m)
            own_left = total
            for targets, k, line, name, loopwhat, notes, f, g in calls:
                if not targets:
                    continue
                rows.append(dict(root=m, call=(targets, k, line, name), freq=freq * g, what=what, kind=kind, gate=notes, mult=f))
            rows.append(dict(root=m, call=None, freq=freq, what=what, kind=kind, gate=[], own_only=True))
        else:
            rows.append(dict(root=m, call=None, freq=freq, what=what, kind=kind, gate=[]))
    # Job lambdas run every tick while the job runs.
    for u in units:
        for jm in JOB_TICK_RX.finditer(u.code):
            pass  # counted inside MakeNewToils below
    return rows


def row_cost(an, r):
    if r.get("own_only"):
        total, cats, sites, calls = an.local(r["root"])
        return total, cats
    if r["call"] is None:
        return an.inclusive(r["root"])
    targets, k, line, name = r["call"]
    total = 0.0
    cats = defaultdict(float)
    for t in targets:
        tt, tc = an.inclusive(t)
        total += tt
        for c, v in tc.items():
            cats[c] += v
    n = len(targets)
    mult = r.get("mult", 1.0)
    return total * mult / n, {c: v * mult / n for c, v in cats.items()}


def job_tick_rows(an, units, methods):
    """Lambdas the game calls every tick while a job of the mod's runs (tick actions and fail conditions)."""
    rows = []
    for m in methods:
        if m.name != "MakeNewToils" and "Toil" not in (m.ret or ""):
            continue
        u = an.units[m.file]
        code = u.code
        for jm in JOB_TICK_RX.finditer(code, m.body_start, m.end):
            arrow = code.find("=>", jm.end(), m.end)
            if arrow < 0:
                continue
            k = arrow + 2
            while k < m.end and code[k].isspace():
                k += 1
            if k < m.end and code[k] == "{":
                end = match_close(code, k, "{", "}")
            else:
                end = statement_end(code, k, m.end)
            text = code[k:end + 1]
            work = 0.0
            cats = defaultdict(float)
            for cat, w, rx in PATTERNS:
                for pm in re.finditer(rx, text):
                    work += w
                    cats[cat] += w
            rows.append(dict(root=m, call=None, freq=TICKS_PER_HOUR, what="every tick while the job runs", kind="job tick",
                             gate=[], fixed=(work + 0.05, dict(cats)), line=u.line_of(k)))
    return rows


# ---------------------------------------------------------------- report --------------------------------------------
def describe_row(r):
    m = r["root"]
    if r["call"] is not None:
        targets, k, line, name = r["call"]
        t = targets[0]
        more = f" (+{len(targets) - 1} like it)" if len(targets) > 1 else ""
        return f"{m.cls}.{m.name} → {t.cls}.{t.name}{more}", f"{t.file}:{t.line}"
    if r.get("own_only"):
        return f"{m.cls}.{m.name} (its own work)", f"{m.file}:{m.line}"
    if r["kind"] == "job tick":
        return f"{m.cls} job tick lambda", f"{m.file}:{r['line']}"
    return f"{m.cls}.{m.name}", f"{m.file}:{m.line}"


def fmt_freq(f):
    if f >= 100:
        return f"{f:,.0f}"
    if f >= 1:
        return f"{f:.1f}".rstrip("0").rstrip(".")
    if f > 0:
        return f"1/{1 / f:.0f}" if 1 / f < 1000 else f"{f:.4f}"
    return "0"


def fmt_cost(c):
    if c >= 100:
        return f"{c:,.0f}"
    if c >= 10:
        return f"{c:.1f}"
    return f"{c:.2f}"


def cat_summary(cats, top=4):
    items = sorted(((c, v) for c, v in cats.items() if v > 0.001), key=lambda x: -x[1])[:top]
    total = sum(cats.values()) or 1
    return ", ".join(f"{c} {100 * v / total:.0f}%" for c, v in items)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--out", default=os.path.join(ROOT, "docs", "perf-audit.md"))
    ap.add_argument("--json", default=None)
    ap.add_argument("--compare", default=None, help="a --json file from an earlier run, for a before/after column")
    ap.add_argument("--top", type=int, default=40)
    ap.add_argument("--quiet", action="store_true")
    ap.add_argument("--src", default=SRC, help="the source folder to read (default: Source/ColonyCouncil)")
    args = ap.parse_args()

    units = []
    for dirpath, _, files in os.walk(args.src):
        if os.sep + "bin" in dirpath or os.sep + "obj" in dirpath:
            continue
        for f in sorted(files):
            if f.endswith(".cs"):
                p = os.path.join(dirpath, f)
                text = open(p, encoding="utf-8-sig").read()
                units.append(Unit(p, text, mask(text), args.src))
    units.sort(key=lambda u: u.rel)
    types, methods = {}, []
    for u in units:
        parse_unit(u, types, methods)
    an = Analyzer(units, types, methods)
    an.build_graph()
    rows = collect_rows(an, units, methods) + job_tick_rows(an, units, methods)

    out_rows = []
    for r in rows:
        if "fixed" in r:
            work, cats = r["fixed"]
        else:
            work, cats = row_cost(an, r)
        if work <= 0.0001:
            continue
        name, where = describe_row(r)
        out_rows.append(dict(name=name, where=where, freq=r["freq"], work=work, cost=r["freq"] * work, cats=cats,
                             what=r["what"], kind=r["kind"], gate=r.get("gate", []), r=r))
    out_rows.sort(key=lambda x: -x["cost"])

    # Where the cost lands: every method, weighted by how often its roots run.
    sink = {}
    for x in out_rows:
        r = x["r"]
        if "fixed" in r:
            continue
        if r.get("own_only"):
            m = r["root"]
            total, cats, _, _ = an.local(m)
            e = sink.setdefault(m.id, [m, 0.0, defaultdict(float)])
            e[1] += r["freq"] * total
            for c, v in cats.items():
                e[2][c] += r["freq"] * v
        elif r["call"] is None:
            an.attribute(r["root"], r["freq"], sink)
        else:
            targets, k, line, name = r["call"]
            for t in targets:
                an.attribute(t, r["freq"] * r.get("mult", 1.0) / len(targets), sink)
    hot = sorted(sink.values(), key=lambda e: -e[1])

    before = None
    if args.compare and os.path.exists(args.compare):
        before = json.load(open(args.compare))
    bmap = {b["name"]: b for b in before["rows"]} if before else {}
    hmap = {b["method"]: b for b in before["methods"]} if before else {}

    total_cost = sum(x["cost"] for x in out_rows)
    by_kind = defaultdict(float)
    for x in out_rows:
        by_kind[x["kind"]] += x["cost"]

    L = []
    L.append("# Performance audit (static)")
    L.append("")
    L.append("Written by `Tools/perf_audit.py` (run `python3 Tools/perf_audit.py` from the repository root; `--json` saves the")
    L.append("numbers, `--compare` adds an earlier run's). It reads the mod's C# without the game: it finds every entry point the")
    L.append("game calls on a clock or often, follows the calls into Rimfolk's own code, and weighs what runs inside loops. The")
    L.append("cost of an entry point is how often it runs in an in-game hour times the weighted work of one run. These are")
    L.append("estimates for ranking where to look, not measurements; a profiler in a real save has the last word.")
    L.append("")
    L.append("**Assumptions.** A large colony: %d colonists, 120 pawns on the map, a few thousand things; speed 3 at 60 frames a" % COLONISTS)
    L.append("second (an in-game hour is about %d frames; GUI code runs at least twice a frame). Vanilla asks situational moods" % FRAMES_PER_HOUR)
    L.append("every 100 ticks per colonist, checks alerts every 24 frames and works out moods every 150 ticks. A loop multiplies")
    L.append("what is in it by what it goes over (colonists %d, all pawns 120, things 400, cells 200, rooms 60, Rimfolk's defs 60," % COLONISTS)
    L.append("the game's big def lists 500 to 3,000, other lists 12). A pass gated by the tick of the hour runs once an hour, by")
    L.append("the first hour of the day once a day, by `Rand.Chance(p)` p of the time. Weights (rough units, a LINQ iterator with its")
    L.append("delegate is 1): " + "; ".join(f"{c}" for c in CATEGORIES) + " — see `PATTERNS` in the script.")
    L.append("")
    L.append("Per-frame code (windows, tabs, overlays) only costs while it is on screen; it is ranked by its cost while open.")
    L.append("")
    L.append(f"Entry points found: {len(out_rows)}. Estimated total: {fmt_cost(total_cost)} units an hour"
             + (f" (before: {fmt_cost(before['total'])})" if before else "") + ".")
    L.append("")
    L.append("| Kind | Units an hour | Share |" + (" Before |" if before else ""))
    L.append("|---|---|---|" + ("---|" if before else ""))
    for k, v in sorted(by_kind.items(), key=lambda kv: -kv[1]):
        L.append(f"| {k} | {fmt_cost(v)} | {100 * v / (total_cost or 1):.0f}% |" + (f" {fmt_cost(before['kinds'].get(k, 0))} |" if before else ""))
    L.append("")
    L.append(f"## Entry points, by estimated cost (top {args.top})")
    L.append("")
    L.append("Runs an hour × work a run = units an hour. \"Work\" lists the biggest kinds of work inside (inclusive of what it calls).")
    L.append("")
    head = "| # | Entry point | Where | Runs an hour | Work a run | Units an hour | Work |"
    sep = "|---|---|---|---|---|---|---|"
    if before:
        head += " Before |"
        sep += "---|"
    L.append(head)
    L.append(sep)
    for i, x in enumerate(out_rows[:args.top], 1):
        gate = (" (" + ", ".join(dict.fromkeys(x["gate"])) + ")") if x["gate"] else ""
        line = f"| {i} | {x['name']}{gate} | `{x['where']}` | {fmt_freq(x['freq'])} | {fmt_cost(x['work'])} | {fmt_cost(x['cost'])} | {cat_summary(x['cats'])} |"
        if before:
            b = bmap.get(x["name"])
            line += f" {fmt_cost(b['cost']) if b else '–'} |"
        L.append(line)
    if before:
        gone = [b for b in before["rows"][:args.top] if b["name"] not in {x["name"] for x in out_rows[:args.top * 2]}]
        if gone:
            L.append("")
            L.append("Left the list since the earlier run: " + "; ".join(f"{b['name']} ({fmt_cost(b['cost'])})" for b in gone[:15]) + ".")
    L.append("")
    L.append(f"## Where the cost lands: methods by their own work (top {args.top})")
    L.append("")
    L.append("Each method's own work (not what it calls), times how often the entry points above reach it.")
    L.append("")
    head = "| # | Method | Where | Units an hour | Work |"
    sep = "|---|---|---|---|---|"
    if before:
        head += " Before |"
        sep += "---|"
    L.append(head)
    L.append(sep)
    for i, (m, cost, cats) in enumerate(hot[:args.top], 1):
        line = f"| {i} | {m.cls}.{m.name} | `{m.file}:{m.line}` | {fmt_cost(cost)} | {cat_summary(cats)} |"
        if before:
            b = hmap.get(f"{m.cls}.{m.name}")
            line += f" {fmt_cost(b['cost']) if b else '–'} |"
        L.append(line)
    L.append("")
    L.append("## What runs inside loops, by kind (the top entry points)")
    L.append("")
    L.append("For each of the top 15 entry points, the costly calls inside loops in the methods it reaches most, with the line.")
    L.append("")
    for x in out_rows[:15]:
        r = x["r"]
        if "fixed" in r:
            continue
        local_sink = {}
        if r.get("own_only"):
            m = r["root"]
            total, cats, _, _ = an.local(m)
            local_sink[m.id] = [m, total, cats]
        elif r["call"] is None:
            an.attribute(r["root"], 1.0, local_sink)
        else:
            for t in r["call"][0]:
                an.attribute(t, 1.0 / len(r["call"][0]), local_sink)
        tops = sorted(local_sink.values(), key=lambda e: -e[1])[:4]
        L.append(f"- **{x['name']}**")
        for m, cost, cats in tops:
            if cost <= 0.0001:
                continue
            _, _, sites, _ = an.local(m)
            inloops = defaultdict(list)
            for cat, line, f, v in sites:
                if f > 1:
                    inloops[cat].append(line)
            what = "; ".join(f"{c} ×{len(ls)} (lines {', '.join(str(l) for l in sorted(set(ls))[:6])})" for c, ls in sorted(inloops.items(), key=lambda kv: -len(kv[1])))
            L.append(f"  - {m.cls}.{m.name} (`{m.file}:{m.line}`): {fmt_cost(cost)} a run" + (f"; in loops: {what}" if what else "; nothing costly in loops"))
    L.append("")
    text = "\n".join(L) + "\n"
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        f.write(text)
    if args.json:
        data = dict(total=total_cost, kinds=dict(by_kind),
                    rows=[dict(name=x["name"], where=x["where"], freq=x["freq"], work=x["work"], cost=x["cost"], cats=x["cats"]) for x in out_rows],
                    methods=[dict(method=f"{m.cls}.{m.name}", where=f"{m.file}:{m.line}", cost=c) for m, c, _ in hot])
        with open(args.json, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=1)
    if not args.quiet:
        print(f"{len(units)} files, {len(types)} types, {len(methods)} methods, {len(out_rows)} entry points; total {fmt_cost(total_cost)} units an hour")
        for i, x in enumerate(out_rows[:args.top], 1):
            print(f"{i:3} {fmt_cost(x['cost']):>12}  {fmt_freq(x['freq']):>8} x {fmt_cost(x['work']):>9}  {x['name']}  [{cat_summary(x['cats'], 3)}]")
        print("wrote " + os.path.relpath(args.out, ROOT))


if __name__ == "__main__":
    main()


def debug_tree(an, m, depth=0, maxdepth=4, k=1.0, seen=None, minshare=0.01):
    seen = seen or set()
    total, cats = an.inclusive(m)
    own, ocats, sites, calls = an.local(m)
    print("  " * depth + f"{m.cls}.{m.name} ({m.file}:{m.line}) x{k:g}: incl {total:.3g} own {own:.3g} {dict((c, round(v, 2)) for c, v in ocats.items())}")
    if depth >= maxdepth or m.id in seen:
        return
    seen = seen | {m.id}
    for targets, kk, line, name, what, notes, f, g in calls:
        sub = sum(an.inclusive(t)[0] for t in targets) / len(targets)
        if kk * sub < minshare * total:
            continue
        print("  " * depth + f"  -> line {line} {name} x{kk:g} loops={what} gates={notes} targets={len(targets)} [{', '.join(t.cls for t in targets[:4])}]")
        for t in targets[:2]:
            debug_tree(an, t, depth + 2, maxdepth, kk, seen, minshare)
