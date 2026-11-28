#!/usr/bin/env python3
"""
The visitors' and strangers' impact audit: which of the callers at the gate, the guests, the questions about a stranger, the
nuisances and the callers' work on the map change the game, and which are only a mood and words (docs/review-visitors.md;
the earlier reviews' Tools/impact_audit.py, Tools/world_impact_audit.py and Tools/gods_impact_audit.py are its models, and
impact_audit's reading of the game's calls is used here as it is).

It reads six parts:

  events      the callers at the gate and the guests: every IncidentDef with a HappeningExtension of the Visitors group whose
              worker is a visitors' worker outside Peoples/ (HappeningWorker_Visitors and those built on it), or a caller
              told from data (HappeningWorker_Setting with a HappeningSetting visitor), leaving out the peoples' own events,
              the missions, the storylines and the long stories (their own frames and reviews). Read answer by answer as
              impact_audit reads every event: what the game keeps, a follow-up, a play-out on the map, Rimfolk's own numbers,
              flavour.
  questions   the council's questions about a stranger (a DilemmaDef with a <stranger> role: a visitor, a trader, a lodger,
              a prisoner, a fallen enemy, a wild animal), answer by answer.
  nuisances   each NuisanceDef: the mood it gives, and an effect of its own when it has one (its <effect>: the rest a bad
              night costs, a garment torn, the food a pest spoiled, the mess scrubbed); and the words of its lines that tell
              of more than a mood (a skinned knuckle, a night awake, a seam torn), a hint to read by hand.
  callers     each piece of work a caller does on the map (Visitors/Callers.cs CallerKind, Happenings/VisitorServices.cs
              ServiceKind): the code that finishes it, read with impact_audit's reading of the game's calls.
  code        the code of Visitors/, Strangers/, Outsiders/ and the visitors' workers: a def looked up by name without
              SilentFail, a hediff or a wound given outside Harm (the shared budget), a job started on one of the colony's
              people without a guard against the drafted, the broken and the player's own orders (a hint to read).
  idle        each thing a visitor does with an idle hour (IdleDoing): by the player's rule of 14 November 2026 they never
              work for the colony, so these are listed, not counted as flavour.

Rows read by hand and left are in Tools/visitors_impact_skips.txt ("Key [list] why", list one of flavour, thin, words,
promise, code); a row that no longer matches is named, so the list stays true.

Usage: python3 Tools/visitors_impact_audit.py [--print events|questions|nuisances|callers|code|idle|promises] [--write] [--root DIR]
  (no flags: the counts; --write rewrites docs/review-visitors.md's measured part between its markers; --root measures
  another copy, an older commit's Source and Defs, for the counts before)
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
OUT = os.path.join(HERE, "docs", "review-visitors.md")
SKIPS = os.path.join(HERE, "Tools", "visitors_impact_skips.txt")
BEGIN = "<!-- visitors_impact_audit: begin -->"
END = "<!-- visitors_impact_audit: end -->"


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

# What a nuisance's own effect is, by the game's kinds (Strangers/Nuisances.cs, NuisanceDef: rest, the rest a bad night
# costs; wear, the hit points the worn piece it names loses).
NUISANCE_EFFECT_KIND = {"rest": "needs", "wear": "items"}

# The callers' work beside impact_audit's reading of the game's calls: a thing mended (its hit points set), what is handed
# into someone's pack (the undertakers' dead loaded on their beast, a piece in the collector's hands).
VIS_CALLS = [
    ("items", r"\.HitPoints\s*[-+]?=|TryTransferToContainer\("),
]
VIS_CALLS = [(k, re.compile(p)) for k, p in VIS_CALLS]

# Words in a nuisance's lines that tell of more than a mood: a hint to read by hand (the effect that would keep them).
NUISANCE_PROMISES = [
    ("a night awake", r"\b(lay awake|awake (all|half) the night|got up tired|slept badly|no sleep|didn't sleep|couldn't sleep)\b", {"needs"}),
    ("a hurt", r"\b(skinned|cut (his|her|their|a) \w+|bled|bleeding|bruised|sprained|burn(ed|t) (his|her|their))\b", {"health"}),
    ("a garment torn", r"\b(tore|torn|split (again|along)|came away)\b", {"items"}),
    ("food spoiled by a pest", r"\b(had been at the food|nibbled through|ate (the|half))\b", {"items"}),
    ("a mess cleaned", r"\b(scrubbing|scrubbed|swept|cleaned (up|off))\b", {"work"}),
]
NUISANCE_PROMISES = [(n, re.compile(p, re.I), k) for n, p, k in NUISANCE_PROMISES]

# The visitors' events: the peoples' own events, missions, storylines and long stories are their own frames.
OTHER_FRAMES = re.compile(r"^(Rimfolk_Events_People|Rimfolk_Events_Peoples|Rimfolk_Missions_|Council_Missions_|Rimfolk_Round5_Missions|Rimfolk_Storyline_|"
                          r"Council_Events_Stories|Rimfolk_GodsOnMap)")


def text_of(el, name=None, default=""):
    node = el if name is None else el.find(name)
    return default if node is None or node.text is None else node.text.strip()


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


# ---------------- Which workers are the visitors' ----------------

def worker_bases(code):
    """{class: base} of every class, and the file it is in."""
    return {n: (c["base"], c["file"].replace(os.sep, "/")) for n, c in code.classes.items()}


# ---------------- Events and questions ----------------

class Group:
    def __init__(self, source, defname, path, label):
        self.source = source
        self.defname = defname
        self.path = path
        self.label = label
        self.answers = []


def measure_answers(code):
    answers, groups = ia.walk(code)
    bases = worker_bases(code)
    vis_events = set()
    for path in sorted(glob.glob(os.path.join(ROOT, "Defs", "**", "*.xml"), recursive=True)):
        base = os.path.basename(path)
        if OTHER_FRAMES.match(base):
            continue
        try:
            tree = ET.parse(path)
        except ET.ParseError:
            continue
        for d in tree.getroot():
            if d.tag != "IncidentDef":
                continue
            ext = setting = None
            for li in d.findall("modExtensions/li"):
                c = (li.get("Class") or "").split(".")[-1]
                if c == "HappeningExtension":
                    ext = li
                elif c == "HappeningSetting":
                    setting = li
            if ext is None or text_of(ext, "group") != "Visitors":
                continue
            worker = text_of(d, "workerClass").split(".")[-1]
            # A caller told from data is a visitor's; any other worker is, unless it is the peoples' own (Peoples/).
            if worker == "HappeningWorker_Setting":
                if setting is None or text_of(setting, "visitor").lower() != "true":
                    continue
            elif worker in bases and "/Peoples/" in bases[worker][1]:
                continue
            vis_events.add(text_of(d, "defName"))
    strangers = set()
    for path in sorted(glob.glob(os.path.join(ROOT, "Defs", "**", "*.xml"), recursive=True)):
        try:
            tree = ET.parse(path)
        except ET.ParseError:
            continue
        for d in tree.getroot():
            if d.tag == "ColonyCouncil.DilemmaDef" and d.find("stranger") is not None:
                strangers.add(text_of(d, "defName"))
    ev = [a for a in answers if a.source == "event" and a.defname in vis_events]
    qs = [a for a in answers if a.source == "question" and a.defname in strangers]
    return ev, qs, groups, vis_events, strangers


def answer_id(a):
    return "%s/%d" % (a.defname, a.index + 1)


def is_refusal(a):
    """Saying no or sending on: meant to cost and give little (the label reads as a refusal)."""
    lab = (a.label or "").lower()
    return bool(re.search(r"^(not |no\b|send |turn |decline|refuse|leave it|let (it|them|him|her) (go|be|pass)|wish |keep (it|them|the)|thank |say no|politely|nothing)", lab)) \
        or "send {x_objective}" in lab or "on (their|his|her) way" in lab or "away" in lab


# ---------------- Nuisances ----------------

class Nuisance:
    def __init__(self, defname, path, thought, effects, lines):
        self.defname = defname
        self.path = path
        self.thought = thought
        self.effects = effects
        self.lines = lines

    @property
    def kinds(self):
        k = {"mood"} if self.thought else set()
        for e in self.effects:
            k.add(NUISANCE_EFFECT_KIND.get(e, "unknown"))
        return k

    @property
    def flavour(self):
        return self.kinds <= {"mood", "words"}

    def promises(self):
        out = []
        for name, rx, need in NUISANCE_PROMISES:
            hit = [l for l in self.lines if rx.search(l)]
            if hit and not (self.kinds & need):
                out.append((name, len(hit), len(self.lines), hit[0]))
        return out


def measure_nuisances():
    out = []
    variants = defaultdict(list)
    for path in sorted(glob.glob(os.path.join(ROOT, "Defs", "**", "*.xml"), recursive=True)):
        try:
            tree = ET.parse(path)
        except ET.ParseError:
            continue
        for d in tree.getroot():
            tag = d.tag.split(".")[-1]
            if tag == "LetterVariantsDef" and d.find("nuisance") is not None:
                variants[text_of(d, "nuisance")] += [text_of(li) for li in d.findall("texts/li")]
    for path in sorted(glob.glob(os.path.join(ROOT, "Defs", "**", "*.xml"), recursive=True)):
        try:
            tree = ET.parse(path)
        except ET.ParseError:
            continue
        for d in tree.getroot():
            if d.tag.split(".")[-1] != "NuisanceDef":
                continue
            dn = text_of(d, "defName")
            effects = [f for f in NUISANCE_EFFECT_KIND if float(text_of(d, f, "0") or 0) > 0]
            lines = [re.sub(r"^\[[^\]]*\]\s*", "", text_of(li)) for li in d.findall("texts/li")] + variants.get(dn, [])
            out.append(Nuisance(dn, os.path.relpath(path, ROOT), text_of(d, "thought"), effects, lines))
    return out


# ---------------- The callers' work on the map ----------------

# The methods that finish each piece of work (read with impact_audit's reading of the game's calls, three calls deep).
CALLER_DONE = {
    "View": ("Callers", ["Viewed"]),
    "Hand": ("Callers", ["Handed"]),
    "Cut": ("Callers", ["Cut", "Restyle"]),
    "Vet": ("Callers", ["Tended", "Trained"]),
    "Graves": ("Callers", ["Buried", "Dug"]),
    "Load": ("Callers", ["Loaded"]),
    "Talk": ("Callers", ["Talked"]),
    "Free": ("Callers", ["StepFree"]),
    "Night": ("Callers", ["StepNight", "Night"]),
}
SERVICE_DONE = {
    "Mend": ("VisitorServices", ["Mended"]),
    "Dose": ("VisitorServices", ["Dosed"]),
    "Lodge": ("VisitorServices", ["StepLodge", "Lodge"]),
    "Ease": ("VisitorServices", ["StepEase", "Ease"]),
}


def enum_names(rel, name):
    s = open(os.path.join(SRC, rel), encoding="utf-8-sig").read() if os.path.exists(os.path.join(SRC, rel)) else ""
    m = re.search(r"public enum " + name + r"\s*\{(.*?)\n    \}", s, re.S)
    if not m:
        return []
    body = re.sub(r"///[^\n]*|//[^\n]*", "", m.group(1))
    return [x.strip().split("=")[0].strip() for x in body.split(",") if x.strip()]


def measure_callers(code):
    rows = []
    for enum, rel, table in (("CallerKind", "Visitors/Callers.cs", CALLER_DONE), ("ServiceKind", "Happenings/VisitorServices.cs", SERVICE_DONE)):
        for kind in enum_names(rel, enum):
            cls, methods = table.get(kind, (None, []))
            kinds = set()
            found = 0
            for m in methods:
                owner, bodies = code.method(cls, m) if cls else (None, [])
                for b in bodies:
                    found += 1
                    kinds |= code.kinds_of_text(owner, b, 3, set())
                    kinds |= {k for k, rx in VIS_CALLS if rx.search(b)}
            rows.append((enum, kind, found, kinds - {"gate", "nothing", "unknown", "journal"}))
    return rows


# ---------------- Code checks ----------------

CODE_DIRS = ["Visitors", "Strangers", "Outsiders"]
CODE_FILES = ["Happenings/Workers_Visitors.cs", "Happenings/Workers_Visitors2.cs", "Happenings/Workers_Callers.cs", "Happenings/VisitorServices.cs",
              "Happenings/Regulars.cs", "Happenings/StrayBeast.cs"]
# A guard against taking someone out of the player's hands: drafted, in a break, on the player's own order, or one of the
# shared guards that check those (PlayOuts.CanJoin/CanTakePart/Interruptible, Callers.CanStand, OwnHappenings.Held, Together.Able).
GUARD = re.compile(r"Drafted|InMentalState|playerForced|CanJoin\(|CanTakePart\(|Interruptible\(|CanStand\(|CanHold\(|\bHeld\(|Together\.Able\(|Able\(|"
                   r"Ready\(|Unwilling\(|ColonistWhyNot\(|CanSit\(|Free\(|CanTalk\(")


def code_files():
    files = []
    for d in CODE_DIRS:
        files += sorted(glob.glob(os.path.join(SRC, d, "*.cs")))
    files += [os.path.join(SRC, f) for f in CODE_FILES if os.path.exists(os.path.join(SRC, f))]
    return files


def method_spans(s):
    """(start, end, name) of each method body in a file's text (for the guard checks, one method at a time)."""
    out = []
    for m in ia.METHOD.finditer(s):
        name = m.group(1)
        if name in ("if", "for", "foreach", "while", "switch", "using", "return", "catch", "lock", "new"):
            continue
        start = m.end() - 1
        if m.group(3) == "{":
            body = ia.block(s, start)
        else:
            body = ia.expr(s, start - 1)
        out.append((m.start(), start + len(body), name))
    return out


def measure_code():
    rows = []
    for path in code_files():
        rel = os.path.relpath(path, SRC).replace(os.sep, "/")
        raw = open(path, encoding="utf-8-sig").read()
        s = ia.strip_code(raw)
        lines_at = [0]
        for i, ch in enumerate(s):
            if ch == "\n":
                lines_at.append(i + 1)

        def line_of(pos):
            lo, hi = 0, len(lines_at) - 1
            while lo < hi:
                mid = (lo + hi + 1) // 2
                if lines_at[mid] <= pos:
                    lo = mid
                else:
                    hi = mid - 1
            return lo + 1

        spans = method_spans(s)

        def method_at(pos):
            best = None
            for a, b, n in spans:
                if a <= pos < b and (best is None or a > best[0]):
                    best = (a, b, n)
            return best

        for m in re.finditer(r"\bGetNamed\(", s):
            meth = method_at(m.start())
            rows.append(("lookup", rel, line_of(m.start()), "a def looked up by name without SilentFail", meth[2] if meth else "?"))
        for m in re.finditer(r"\.TakeDamage\(|HediffMaker\.MakeHediff\(|health\.AddHediff\(", s):
            meth = method_at(m.start())
            body = s[meth[0]:meth[1]] if meth else ""
            if re.search(r"\bHarm\.\w+\(", body):
                continue
            rows.append(("harm", rel, line_of(m.start()), "a hediff or a wound given in %s without Harm in the same method" % (meth[2] if meth else "?"), meth[2] if meth else "?"))
        for m in re.finditer(r"(\w+)\.jobs\.(StartJob|TryTakeOrderedJob)\(|(\w+)\.jobs\?\.(StartJob|TryTakeOrderedJob)\(", s):
            who = m.group(1) or m.group(3)
            meth = method_at(m.start())
            body = s[meth[0]:meth[1]] if meth else ""
            if GUARD.search(body):
                continue
            rows.append(("job", rel, line_of(m.start()), "%s given a job in %s with no guard in the method (a hint: read who it is)" % (who, meth[2] if meth else "?"), (meth[2] if meth else "?") + ":" + who))
    return rows


# ---------------- Idle hours ----------------

# ---------------- Balance: the callers told from data, their prices against what they give ----------------

# What a "what" the colony hands over is worth when it isn't a def's name: the cheapest of its kind, as the game takes it.
SELL_WORTH = {"medicine": "MedicineHerbal", "meals": "MealSimple", "rawfood": "RawPotatoes", "wood": "WoodLog"}
# A price this far from what is given is listed (an hour's teaching, a meal or a walk are not priced).
FAIR = (0.5, 2.0)


def market_values():
    out = {}
    path = os.path.join(HERE, "docs", "player-mods", "items.tsv")
    if not os.path.exists(path):
        return out
    with open(path, encoding="utf-8") as fh:
        head = fh.readline().rstrip("\n").split("\t")
        i_name, i_value = head.index("defName"), head.index("marketValue")
        for line in fh:
            cols = line.rstrip("\n").split("\t")
            try:
                out.setdefault(cols[i_name], float(cols[i_value]))
            except (ValueError, IndexError):
                continue
    return out


def measure_balance():
    """Each answer of a caller told from data that trades silver for goods: (id, label, price range, worth, ratio range)."""
    values = market_values()
    rows = []
    for path in sorted(glob.glob(os.path.join(ROOT, "Defs", "IncidentDefs", "*.xml"))):
        try:
            tree = ET.parse(path)
        except ET.ParseError:
            continue
        for d in tree.getroot():
            if d.tag != "IncidentDef" or text_of(d, "workerClass").split(".")[-1] != "HappeningWorker_Setting":
                continue
            ext = next((li for li in d.findall("modExtensions/li") if (li.get("Class") or "").endswith("HappeningExtension")), None)
            setting = next((li for li in d.findall("modExtensions/li") if (li.get("Class") or "").endswith("HappeningSetting")), None)
            if ext is None or setting is None or text_of(setting, "visitor").lower() != "true":
                continue
            amount = text_of(ext, "amount")
            if "~" not in amount:
                continue
            lo, hi = (float(x) for x in amount.split("~"))
            for i, o in enumerate(ext.findall("options/li")):
                price, worth, paid = 0.0, 0.0, 0.0
                for e in o.findall("effects/li"):
                    c = (e.get("Class") or "").split(".")[-1]
                    factor = float(text_of(e, "factor", "1") or 1)
                    what = text_of(e, "what")
                    if c == "DilemmaEffect_Consume" and what == "silver":
                        price += factor
                    elif c == "DilemmaEffect_Consume" and what:
                        worth -= float(text_of(e, "count", "1") or 1) * values.get(SELL_WORTH.get(what, what), 0.0)
                    elif c == "DilemmaEffect_Spawn":
                        worth += float(text_of(e, "count", "1") or 1) * values.get(text_of(e, "thing"), 0.0)
                    elif c == "DilemmaEffect_Paid":
                        paid += factor
                if price > 0 and worth > 0:
                    rows.append(("%s/%d" % (text_of(d, "defName"), i + 1), text_of(o, "label"), (lo * price, hi * price), worth, (lo * price / worth, hi * price / worth)))
                elif paid > 0 and worth < 0:
                    rows.append(("%s/%d" % (text_of(d, "defName"), i + 1), text_of(o, "label"), (lo * paid, hi * paid), worth, (lo * paid / -worth, hi * paid / -worth)))
    return rows


def unfair(row):
    lo, hi = row[4]
    return lo < FAIR[0] or hi > FAIR[1] if row[3] > 0 else hi > FAIR[1] * 2


def measure_idle():
    return enum_names("Visitors/IdleHoursMath.cs", "IdleDoing")


# ---------------- The report ----------------

def short(s, n=90):
    s = re.sub(r"\s+", " ", s or "").replace("|", "/").strip()
    return s if len(s) <= n else s[:n - 3] + "..."


def kinds_text(kinds):
    return ", ".join(sorted(kinds - {"gate", "nothing"})) or "nothing"


def build():
    code = ia.Code()
    ev, qs, groups, vis_events, strangers = measure_answers(code)
    return {
        "code_reader": code,
        "events": ev,
        "questions": qs,
        "groups": groups,
        "event_defs": vis_events,
        "question_defs": strangers,
        "nuisances": measure_nuisances(),
        "callers": measure_callers(code),
        "code": measure_code(),
        "idle": measure_idle(),
        "balance": measure_balance(),
    }


def answer_counts(answers, defs=None):
    groups = set(defs) if defs is not None else {a.defname for a in answers}
    flav = [a for a in answers if a.flavour_only and not a.empty]
    thin = [a for a in answers if a.thin and not a.flavour_only and not a.empty]
    empty = [a for a in answers if a.empty]
    by = defaultdict(list)
    for a in answers:
        by[a.defname].append(a)
    allflav = [k for k, v in by.items() if all(a.flavour_only or a.empty for a in v)]
    proms = [(a, p) for a in answers for p in ia.promises(a)]
    return {"defs": len(groups), "answers": len(answers), "flavour": flav, "thin": thin, "empty": empty, "allflavour": allflav, "promises": proms}


def counts(res):
    skips = read_skips()
    ev = answer_counts(res["events"], res["event_defs"])
    qs = answer_counts(res["questions"], res["question_defs"])
    nu = res["nuisances"]
    out = {
        "events": (ev["defs"], ev["answers"], len(ev["flavour"]), len(ev["thin"]), len(ev["empty"]), len(ev["promises"])),
        "questions": (qs["defs"], qs["answers"], len(qs["flavour"]), len(qs["thin"]), len(qs["empty"]), len(qs["promises"])),
        "nuisances": (len(nu), sum(1 for n in nu if not n.flavour), sum(1 for n in nu if n.flavour), sum(len(n.promises()) for n in nu)),
        "callers": (len(res["callers"]), sum(1 for r in res["callers"] if {ia.TIERS.get(k, "game") for k in r[3]} & {"game"})),
        "code": Counter(r[0] for r in res["code"]),
        "balance": (len(res["balance"]), sum(1 for r in res["balance"] if unfair(r))),
    }
    return out


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
    w("Measured by `python3 Tools/visitors_impact_audit.py --write` (this part between the markers is rewritten each run; the")
    w("review's own notes are outside them).")
    w("")
    w("| Part | Defs | Answers | Flavour-only | Thin (moods and Rimfolk's numbers) | Empty | Words that promise more |")
    w("|---|---|---|---|---|---|---|")
    for name, key in (("Callers at the gate and guests", "events"), ("Questions about a stranger", "questions")):
        d, n, f, t, e, p = c[key]
        w("| %s | %d | %d | %d | %d | %d | %d |" % (name, d, n, f, t, e, p))
    n, real, flav, proms = c["nuisances"]
    w("")
    w("Nuisances: %d; with an effect of their own besides the mood: %d; a mood only: %d; lines that tell of more than a mood: %d." % (n, real, flav, proms))
    w("The callers' and the visitors' work on the map: %d kinds; finishing with something the game keeps: %d." % c["callers"])
    cc = c["code"]
    w("Code hints: %d defs looked up by name without SilentFail, %d hurts given outside Harm, %d jobs started with no guard in the method." % (
        cc["lookup"], cc["harm"], cc["job"]))
    w("Balance: %d priced answers of the callers told from data; outside the fair range: %d." % c["balance"])
    w("")

    def answers_section(title, items, lst, fmt):
        before = len(items)
        items = [x for x in items if not skipped(fmt[0](x), lst)]
        read = "; %d more read and left (the skip list)" % (before - len(items)) if before > len(items) else ""
        w("### %s (%d%s)" % (title, len(items), read))
        w("")
        if not items:
            w("None.")
        for x in items:
            w(fmt[1](x))
        w("")

    for name, key in (("Callers at the gate and guests", "events"), ("Questions about a stranger", "questions")):
        ac = answer_counts(res[key])
        answers_section(name + ": flavour-only answers", ac["flavour"], "flavour",
                        (answer_id, lambda a: "- `%s` %s: %s (%s)" % (answer_id(a), short(a.label, 60), ia.kinds_text(a), os.path.basename(a.path))))
        answers_section(name + ": thin answers (moods and Rimfolk's numbers only)", ac["thin"], "thin",
                        (answer_id, lambda a: "- `%s` %s: %s" % (answer_id(a), short(a.label, 60), ia.kinds_text(a))))
        answers_section(name + ": answers with no effect at all (refusals, or their worker's code does it)", ac["empty"], "empty",
                        (answer_id, lambda a: "- `%s` %s: \"%s\"" % (answer_id(a), short(a.label, 60), short(ia.text_of(a.option.find("outcome")), 110))))
        answers_section(name + ": words that promise more than the effects do", ac["promises"], "promise",
                        (lambda x: answer_id(x[0]) + ":" + x[1][0].replace(" ", "_"),
                         lambda x: "- `%s` (%s, has %s): \"%s\"" % (answer_id(x[0]), x[1][0], ia.kinds_text(x[0]), short(x[1][1], 140))))
    nus = res["nuisances"]
    answers_section("Nuisances: a mood only", [n for n in nus if n.flavour], "flavour", (lambda n: n.defname, lambda n: "- `%s` (%s)" % (n.defname, n.thought)))
    answers_section("Nuisances with an effect of their own", [n for n in nus if not n.flavour], "-",
                    (lambda n: n.defname, lambda n: "- `%s`: %s" % (n.defname, ", ".join(n.effects))))
    prom = [(n, p) for n in nus for p in n.promises()]
    answers_section("Nuisances: lines that tell of more than a mood", prom, "words",
                    (lambda x: x[0].defname + ":" + x[1][0].replace(" ", "_"),
                     lambda x: "- `%s` (%s, %d of %d lines): \"%s\"" % (x[0].defname, x[1][0], x[1][1], x[1][2], short(x[1][3], 120))))
    w("### The callers' and the visitors' work on the map")
    w("")
    for enum, kind, found, kinds in res["callers"]:
        w("- %s `%s`: %s" % (enum, kind, kinds_text(kinds) if found else "(no finishing code found: read by hand)"))
    w("")
    rows = res["code"]
    answers_section("Code: hints to read", rows, "code",
                    (lambda r: "%s:%s:%s" % (r[0], r[1], r[4]), lambda r: "- %s `%s:%d`: %s" % (r[0], r[1], r[2], r[3])))
    w("### Balance: the callers told from data, their prices against the game's own worth of what changes hands")
    w("")
    w("Worth from docs/player-mods/items.tsv; a sale to the colony is the price over the worth of the goods, a sale by the colony")
    w("(their silver for the colony's goods) the silver over the worth. Listed when outside %.1f to %.1f (a sale by the colony: above %.1f)." % (FAIR[0], FAIR[1], FAIR[1] * 2))
    w("")
    for r in res["balance"]:
        mark = " **listed**" if unfair(r) and not skipped(r[0], "balance") else ""
        w("- `%s` %s: %d to %d silver for %d worth (%.1f to %.1f)%s" % (r[0], short(r[1], 60), r[2][0], r[2][1], abs(r[3]), r[4][0], r[4][1], mark))
    w("")
    w("### What visitors do with an idle hour (their own, never the colony's work: the player's rule of 14 November 2026)")
    w("")
    w(", ".join(res["idle"][1:]) + ".")
    w("")
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
    old = open(OUT, encoding="utf-8").read() if os.path.exists(OUT) else "# Review of the visitors and strangers\n\n"
    if BEGIN in old and END in old:
        new = old[:old.index(BEGIN)] + body + old[old.index(END) + len(END):]
    else:
        new = old.rstrip() + "\n\n" + body + "\n"
    open(OUT, "w", encoding="utf-8").write(new)


def main():
    args = sys.argv[1:]
    res = build()
    c = counts(res)
    print("callers at the gate and guests: %d events, %d answers, %d flavour-only, %d thin, %d empty, %d promises" % c["events"])
    print("questions about a stranger: %d questions, %d answers, %d flavour-only, %d thin, %d empty, %d promises" % c["questions"])
    print("nuisances: %d, %d with an effect of their own, %d a mood only, %d lines telling of more" % c["nuisances"])
    print("callers' and visitors' work: %d kinds, %d finishing with something the game keeps" % c["callers"])
    print("code hints: %s" % dict(c["code"]))
    print("balance: %d priced answers of the callers told from data, %d outside the fair range" % c["balance"])
    if "--print" in args:
        what = args[args.index("--print") + 1]
        if what in ("events", "questions"):
            for a in sorted(res[what], key=lambda a: (a.defname, a.index)):
                print("%-48s %d %-50s %s" % (a.defname, a.index + 1, short(a.label, 50), ia.kinds_text(a)))
        elif what == "promises":
            for key in ("events", "questions"):
                for a, (name, sentence) in answer_counts(res[key])["promises"]:
                    print("%-48s %d %-20s %s [%s]" % (a.defname, a.index + 1, name, short(sentence, 110), ia.kinds_text(a)))
        elif what == "nuisances":
            for n in res["nuisances"]:
                print("%-40s %-30s %s %s" % (n.defname, kinds_text(n.kinds), n.effects, n.promises()))
        elif what == "callers":
            for r in res["callers"]:
                print(r)
        elif what == "code":
            for r in res["code"]:
                print("%-7s %s:%d %s [key %s:%s:%s]" % (r[0], r[1], r[2], r[3], r[0], r[1], r[4]))
        elif what == "balance":
            for r in res["balance"]:
                print("%-44s %-50s %4d-%4d silver, worth %5.0f, %.1f-%.1f%s" % (r[0], short(r[1], 50), r[2][0], r[2][1], abs(r[3]), r[4][0], r[4][1], "  OUT" if unfair(r) else ""))
        elif what == "idle":
            print(res["idle"])
    if "--write" in args:
        write(res)
        print("Wrote", os.path.relpath(OUT, HERE))


if __name__ == "__main__":
    main()
