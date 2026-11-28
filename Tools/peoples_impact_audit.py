#!/usr/bin/env python3
"""
The peoples' impact audit: what the events of Rimfolk's peoples carry, which people each comes from and in what form of its
age, and the code that plays them out (docs/review-peoples.md; Tools/impact_audit.py, world_impact_audit.py and
gods_impact_audit.py are its models).

It reads:

  events     every one of the peoples' events (an IncidentDef with a HappeningExtension in Defs/IncidentDefs/
             Rimfolk_Events_People*.xml): its people (the def's callings, else the PeoplesMath callings its worker or its file
             names), whether the storyteller draws it or its own code fires it when its moment comes, the forms of the age its
             texts are written in ([age0] to [age3], [tribal] to [spacer]), and its answers read as impact_audit reads every
             event's: what the game keeps, a play-out on the map, Rimfolk's own numbers, or only a mood and words.
  peoples    each people (by calling): its events, drawn and fired, and its answers by what they carry.
  words      each event's placeholders against what its worker fills ({PLACE} needs a place's name set, {TALE}, {BEAST},
             {DEAD}, {ENEMY}, {RELATION} theirs; {X} a stranger cast; {B} falls back to A): a placeholder no code of the
             event's own file fills reads as empty on the card.
  doors      every job the peoples' code starts (Source/ColonyCouncil/Peoples): the doors that send someone to work at a
             place, and whether each refuses one drafted, in a break, down or on the player's own order (PeoplesReview.Held).
  lookups    defs looked up by name in the peoples' code without SilentFail.
  hediffs    the hediffs of Rimfolk's own the peoples' code gives, and how each ends (a comp that takes it away, a severity
             that falls, a class of its own) or that it is meant for life (a mark, a token).
  magic      the peoples' casters (Peoples/Magic): the spells they cast for the colony in peace, and those kept out of their hands.

  ages       the events not told in their people's own age: checked and left (LEFT, with the reason), or in the colony's age
             only (docs/content-round-7-peoples-ages.md).

Usage: python3 Tools/peoples_impact_audit.py [--print events|peoples|thin|flavour|promises|words|doors|lookups|hediffs|magic|ages]
                                               [--write] [--root DIR]
  (no flags: the counts; --write rewrites docs/review-peoples.md's measured part between its markers; --root measures another
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
OUT = os.path.join(HERE, "docs", "review-peoples.md")
BEGIN = "<!-- peoples_impact_audit: begin -->"
END = "<!-- peoples_impact_audit: end -->"


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
PEOPLES = os.path.join(SRC, "Peoples")

# The peoples by calling, in the order Rimfolk's words know them (PeoplesMath.Callings), with the name of each.
CALLINGS = [
    ("traders", "Traders", "the road league"), ("healers", "Healers", "the hospitaller order"), ("herders", "Herders", "the herding folk"),
    ("pilgrims", "Pilgrims", "the pilgrim fellowship"), ("scholars", "Scholars", "the learned society"), ("brokers", "Brokers", "the spacer enclave"),
    ("fishers", "Fishers", "the river folk"), ("hunters", "Hunters", "the Lantern Wardens"), ("sextons", "Sextons", "the Quiet Bell"),
    ("tamers", "Tamers", "the Circuit of Tamers"), ("salvagers", "Salvagers", "the Wheel"), ("seers", "Seers", "the Stormglass Conclave"),
    ("grovekeepers", "Grovekeepers", "the Mossborn"), ("mercenaries", "Mercenaries", "the Ashen Banner"), ("artisans", "Artisans", "the Kiln Guild"),
    ("linesmen", "Linesmen", "the Signal Line"), ("drillers", "Drillers", "the Deepwell Union"), ("citizens", "Citizens", "the Last Republic"),
    ("crew", "Crew", "the Hollow Fleet"), ("editors", "Editors", "the Helix Works"), ("mechanitors", "Mechanitors", "the Lattice"),
    ("listeners", "Listeners", "the Choir of the Spire"), ("vaultfolk", "Vaultfolk", "the Sunken Vault"),
]
BY_CONST = {const: calling for calling, const, _ in CALLINGS}
NAME = {calling: name for calling, _, name in CALLINGS}
CALLING_RX = re.compile(r"PeoplesMath\.(" + "|".join(BY_CONST) + r")\b|\"(" + "|".join(NAME) + r")\"")

AGE_TAG = re.compile(r"^\[([^\]]*)\]")
# A text's tags for the age: the people's own ([age0] to [age3], PeopleEvents.AgeTags) or the colony's ([lowtech] and [!lowtech],
# [tribal] to [spacer], Flavor.AddEraTags).
AGE_WORDS = {"age0": {0}, "tribal": {0}, "lowtech": {0, 1}, "!lowtech": {2, 3}, "age1": {1}, "medieval": {1}, "age2": {2}, "industrial": {2},
             "age3": {3}, "spacer": {3}, "ultra": {3}}

# The events whose worker doesn't name its people in its own code: those cast by their setting (Happenings/Workers_Setting.cs,
# the setting's regular) and the art handed on, which any of the peoples at good terms may hand on.
HAND = {"Rimfolk_Evt_OrderHealer": ["healers"], "Rimfolk_Evt_LeagueOutrider": ["traders"], "Rimfolk_Evt_ScholarCopyist": ["scholars"],
        "Rimfolk_Evt_RiverCatch": ["fishers"], "Rimfolk_Evt_Disputation": ["scholars"], "Rimfolk_Evt_LongWalk": ["pilgrims"],
        "Rimfolk_Evt_RiverNets": ["fishers"], "Rimfolk_Evt_ArtHandedOn": ["any"]}

# The older events checked and left in the colony's age, because they read the same in every age of their people
# (docs/content-round-7-peoples-ages.md, "Checked and left"): nothing in their words or their goods belongs to one age.
LEFT = {
    "Rimfolk_Evt_TamerStray": "a ranger leads the colony's lost animal home on a lead",
    "Rimfolk_Evt_WheelWreck": "runners strip a wrecked machine with what they carry; the [lowtech] text is the colony's own word for it",
    "Rimfolk_Evt_GuildCollects": "the journeyman comes back for the piece the colony made and pays by its quality",
    "Rimfolk_Evt_UnionStrike": "a strike at the pits needs food or a fair talker in any age",
    "Rimfolk_Evt_FleetSleeper": "a sleeper from the Fleet's caskets, which every Fleet keeps from its founding, asks for shelter",
    "Rimfolk_Evt_HelixBatch": "the Works' own vat-grown, sick with a gene fault, ask for the colony's doctor or medicine",
    "Rimfolk_Evt_LatticeRepair": "a mech-tender mends the colony's mechs with a roll of tools",
    "Rimfolk_Evt_LatticeLostMech": "one of the Lattice's mechs, its link lost, wanders onto the land",
    "Rimfolk_Evt_LatticeParts": "the Lattice buys every subcore and chip in every age, as it keeps minds",
    "Rimfolk_Evt_ChoirListening": "the listeners sit in the colony and listen",
    "Rimfolk_Evt_ChoirArt": "the Spire's object for the colony's art: the Spire's, not of the Choir's age",
    "Rimfolk_Evt_VaultCraft": "a technician asks to learn a craft the vault lost",
    "Rimfolk_Evt_VaultChild": "a vault-born child sees the sky and asks to stay",
}

# A placeholder, and the context's field that fills it (HappeningContext.AddArgs, DilemmaContext's args).
FIELDS = {
    "PLACE": r"placeName", "TALE": r"tale", "BEAST": r"beast", "DEAD": r"dead", "ENEMY": r"enemy", "RELATION": r"relation",
    "HOST": r"host", "RITE": r"rite", "GUEST": r"guest", "X": r"stranger", "THING": r"thing", "PET": r"pet", "B": r"b",
}
PLACEHOLDER = re.compile(r"\{([A-Z]+)(?:_[A-Za-z]+)?\}")


def read(path):
    with open(path, encoding="utf-8-sig") as fh:
        return fh.read()


def short(s, n=100):
    s = re.sub(r"\s+", " ", s or "").replace("|", "/").strip()
    return s if len(s) <= n else s[:n - 3] + "..."


# ---------------- The events ----------------

class Event:
    def __init__(self, defname, path, worker, drawn, callings, ages, answers, unfilled, refs):
        self.defname = defname
        self.path = path
        self.worker = worker
        self.drawn = drawn
        self.callings = callings
        self.ages = ages
        self.answers = answers
        self.unfilled = unfilled
        self.refs = refs
        # Told in their people's own age: a text tagged [age0] to [age3] (PeopleEvents.AgeTags), not only the colony's ([lowtech]),
        # or what they bring, ask or pay is of their age by their worker's own code (Peoples/PplAges.cs).
        self.own_age = False
        # Checked and left in the colony's age: it reads the same in every age of its people (LEFT).
        self.left = defname in LEFT

    @property
    def thin(self):
        return self.answers and all(a.thin or a.empty for a in self.answers)


def event_files():
    d = os.path.join(ROOT, "Defs", "IncidentDefs")
    return sorted(glob.glob(os.path.join(d, "Rimfolk_Events_People*.xml")))


def class_text(code, cls):
    """A class's own text and its bases' (in Rimfolk's code), and the whole of the file it is written in."""
    texts, files = [], set()
    seen = 0
    while cls in code.classes and seen < 6:
        c = code.classes[cls]
        texts.append(c["body"])
        files.add(c["file"])
        cls = c["base"]
        seen += 1
    whole = []
    for f in files:
        p = os.path.join(ROOT, f)
        if os.path.exists(p):
            whole.append(ia.strip_code(read(p)))
    return "\n".join(texts), "\n".join(whole)


def named_callings(text):
    found = []
    for m in CALLING_RX.finditer(text):
        c = BY_CONST.get(m.group(1)) if m.group(1) else m.group(2)
        if c and c not in found:
            found.append(c)
    return found


def callings_of(defname, ext, own, whole):
    """The event's people: the def's callings, else its hand-read one, else those its worker names, else its file's only one."""
    cs = [li.text.strip() for li in ext.findall("callings/li") if li.text]
    if cs:
        return cs
    if defname in HAND:
        return HAND[defname]
    found = named_callings(own)
    if found:
        return found
    found = named_callings(whole)
    return found if len(found) == 1 else []


def ages_of(ext):
    """The ages its texts are written for (0 tribal to 3 spacer), and "any" when one of them fits every age."""
    ages, every = set(), False
    for li in ext.findall("texts/li"):
        m = AGE_TAG.match((li.text or "").strip())
        tags = [t.strip() for t in re.split(r"[,|]", m.group(1))] if m else []
        aged = [AGE_WORDS[t] for t in tags if t in AGE_WORDS]
        if not aged:
            every = True
        for a in aged:
            ages |= a
    return sorted(ages) + (["any"] if every else [])


def words_of_ext(ext):
    parts = []
    for tag in ("titles", "texts", "lines"):
        parts += [li.text or "" for li in ext.findall(tag + "/li")]
    for o in ext.findall("options/li"):
        for tag in ("label", "outcome", "riskOutcome"):
            parts.append(o.findtext(tag) or "")
        for tag in ("outcomes", "riskOutcomes"):
            parts += [li.text or "" for li in o.findall(tag + "/li")]
    return parts


def unfilled_of(ext, worker, own, whole):
    """The placeholders the event's words use that no code of its worker's file (or its bases) fills."""
    used = Counter()
    for t in words_of_ext(ext):
        for m in PLACEHOLDER.finditer(t):
            used[m.group(1)] += 1
    out = []
    code = own + "\n" + whole
    for ph in sorted(used):
        field = FIELDS.get(ph)
        if field is None:
            continue
        if ph == "B" and ext.find("second") is not None:
            continue
        if re.search(r"(?:\.|\b)" + field + r"\s*=(?!=)", code):
            continue
        # {X} in an event whose base casts visitors (MakeGroup) or a guest's story.
        if ph == "X" and re.search(r"\bMakeGroup\(|GuestStories\.TryCast", code):
            continue
        out.append(ph)
    return out


def measure_events():
    code = ia.Code()
    events = []
    for path in event_files():
        try:
            tree = ET.parse(path)
        except ET.ParseError as e:
            print("cannot parse", path, e, file=sys.stderr)
            continue
        rel = os.path.relpath(path, ROOT)
        for d in tree.getroot():
            if d.tag != "IncidentDef":
                continue
            ext = next((li for li in ia.lis(d, "modExtensions") if ia.cls_of(li) == "HappeningExtension"), None)
            if ext is None:
                continue
            defname = ia.text_of(d.find("defName"))
            worker = ia.text_of(d.find("workerClass")).split(".")[-1]
            own, whole = class_text(code, worker)
            res = code.resolved(worker) if worker else None
            answers = [ia.make_answer(code, "event", defname, i, o, ia.resolved_for(res, i), rel) for i, o in enumerate(ia.lis(ext, "options"))]
            try:
                drawn = float(ia.text_of(d.find("baseChance")) or "0") > 0
            except ValueError:
                drawn = True
            refs = sorted(set(re.findall(r"\{([A-Z]+)", " ".join(words_of_ext(ext)))))
            ev = Event(defname, rel, worker, drawn, callings_of(defname, ext, own, whole), ages_of(ext), answers, unfilled_of(ext, worker, own, whole), refs)
            ev.own_age = any(re.match(r"^\[[^\]]*\bage[0-3]", li.text or "") for li in ext.findall("texts/li")) or "PplAges" in own
            events.append(ev)
    return events


# ---------------- The code ----------------

def peoples_files():
    return sorted(glob.glob(os.path.join(PEOPLES, "**", "*.cs"), recursive=True))


METHOD = re.compile(r"\n(\s+)(?:public|private|protected|internal|static|override|virtual|\s)+[\w<>\[\],.?]+\s+(\w+)\s*\([^;{]*\)\s*(?:\{|=>)")
JOB = re.compile(r"([\w.\[\]]+)\.jobs\.(StartJob|TryTakeOrderedJob)\(")
GUARD = re.compile(r"PeoplesReview\.Held\(|\.Drafted\b|playerForced|MayLeave\(")
# The doors that send only theirs, read by hand: the Wardens' warden to the bout, the visitor who works on a colonist, round 8's
# visitor to the meeting at the gate (its Send, which may send the colony's own, is guarded).
THEIRS_ONLY = {("HuntOrTame.cs", "Bout"), ("PeopleWorkOn.cs", "Send"), ("PeopleEvents8.cs", "Start")}


def measure_doors():
    """Every job the peoples' code starts: the method it is in, whom it sends, and whether a guard refuses one held."""
    out = []
    for path in peoples_files():
        raw = read(path)
        s = ia.strip_code(raw)
        starts = [(m.start(), m.group(2)) for m in METHOD.finditer(s)]
        for m in JOB.finditer(s):
            pos = m.start()
            meth = None
            for st, name in starts:
                if st < pos:
                    meth = (st, name)
            if meth is None:
                continue
            body = s[meth[0]:pos]
            door = meth[1] in ("Send", "Start", "Hold", "Errand", "Bout", "StartBout")
            out.append({
                "file": os.path.relpath(path, ROOT), "line": s[:pos].count("\n") + 1, "method": meth[1], "who": m.group(1),
                "how": m.group(2), "guard": bool(GUARD.search(body)), "door": door,
                "theirs": door and (os.path.basename(path), meth[1]) in THEIRS_ONLY,
            })
    return out


LOOKUP = re.compile(r"\.GetNamed\((?!SilentFail)|\b(?:ThingDef|HediffDef|PawnKindDef|JobDef|TraitDef|ThoughtDef|SoundDef|FactionDef)\.Named\(")


def measure_lookups():
    out = []
    for path in peoples_files():
        s = ia.strip_code(read(path))
        for m in LOOKUP.finditer(s):
            out.append((os.path.relpath(path, ROOT), s[:m.start()].count("\n") + 1, short(s[m.start():m.start() + 70], 70)))
    return out


# A mark or a gift for life, read by hand: the Wardens' cord and the Circuit's token are honours; the Lattice's link ends with its class.
FOR_LIFE = {"Rimfolk_RedCord": "an honour, for life", "Rimfolk_CircuitToken": "an honour, for life",
            "Rimfolk_LatticeLink": "its own class ends it when the link is broken", "Council_Legendary": "the world's word on a colonist, for life"}


def measure_hediffs():
    # The names the code gives as hediffs: on a line about a hediff, or one of Rimfolk's ailments (an office or a decree may share a
    # hediff's name and is left out).
    names = set()
    for path in peoples_files():
        for line in read(path).split("\n"):
            for name in re.findall(r'"((?:Rimfolk|Council)_[A-Za-z0-9_]+)"', line):
                if "Hediff" in line or name.startswith("Council_Ail_"):
                    names.add(name)
    out = []
    for path in sorted(glob.glob(os.path.join(ROOT, "Defs", "**", "*.xml"), recursive=True)):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        for d in root:
            if d.tag != "HediffDef":
                continue
            dn = d.findtext("defName")
            if dn not in names:
                continue
            comps = [(c.get("Class") or "").split(".")[-1] for c in d.findall("comps/li")]
            ends = []
            if any("Disappears" in c for c in comps):
                ends.append("goes after a while")
            spd = [c.findtext("severityPerDay") for c in d.findall("comps/li") if c.findtext("severityPerDay")]
            if any(x.strip().startswith("-") for x in spd):
                ends.append("its severity falls")
            if any(c in ("HediffCompProperties_Ailment", "HediffCompProperties_AilmentImmunizable", "HediffCompProperties_Immunizable") for c in comps):
                ends.append("an illness that is fought off")
            cls = (d.findtext("hediffClass") or "").split(".")[-1]
            if dn in FOR_LIFE:
                ends.append(FOR_LIFE[dn])
            elif cls and cls not in ("HediffWithComps", "Hediff"):
                ends.append("its own class (" + cls + ")")
            out.append((dn, os.path.basename(path), ", ".join(ends) or "nothing ends it"))
    return out


def measure_magic():
    s = read(os.path.join(PEOPLES, "Magic", "PeoplesMagicMath.cs"))
    peace = re.findall(r'\["(\w+)"\]\s*=\s*new\s*\[\]\s*\{([^}]*)\}', s)
    spells = []
    for cls, body in peace:
        for need, spell in re.findall(r"\(Need\.(\w+),\s*\"(\w+)\"\)", body):
            spells.append((cls, need, spell))
    kept = re.findall(r'\["(TM_\w+)"\]\s*=\s*"([^"]+)"', s)
    return {"peace": spells, "kept": kept}


# ---------------- The counts ----------------

def tier_of(a):
    if a.empty:
        return "empty"
    if a.flavour_only:
        return "flavour"
    if a.thin:
        return "thin"
    return "game"


def counts(events, doors, lookups, hediffs):
    answers = [a for e in events for a in e.answers]
    t = Counter(tier_of(a) for a in answers)
    promises = [(a, p) for a in answers for p in ia.promises(a)]
    held = [d for d in doors if d["door"] and not d["theirs"]]
    L = []
    L.append("| Part | Count |")
    L.append("|---|---|")
    L.append("| The peoples' events (drawn by the storyteller, fired when their moment comes) | %d (%d, %d) |" % (
        len(events), sum(1 for e in events if e.drawn), sum(1 for e in events if not e.drawn)))
    L.append("| Their answers: with a game effect or a play-out, thin (moods and Rimfolk's numbers), flavour only, empty | %d: %d, %d, %d, %d |" % (
        len(answers), t["game"], t["thin"], t["flavour"], t["empty"]))
    L.append("| Events whose every answer is thin | %d |" % sum(1 for e in events if e.thin))
    L.append("| Words that promise more than the answer's effects (to read by hand) | %d |" % len(promises))
    L.append("| Events with a placeholder no code of theirs fills | %d |" % sum(1 for e in events if e.unfilled))
    L.append("| Events whose people the code doesn't name | %d |" % sum(1 for e in events if not e.callings))
    L.append("| Events told in their people's own age ([age0] to [age3], or their goods by it); checked and left, the same in every age; in the colony's age only | %d; %d; %d |" % (
        sum(1 for e in events if e.own_age), sum(1 for e in events if not e.own_age and e.left), sum(1 for e in events if not e.own_age and not e.left)))
    L.append("| The doors that send someone to work and may send the colony's own, refusing one held | %d of %d |" % (sum(1 for d in held if d["guard"]), len(held)))
    L.append("| Defs looked up by name without SilentFail | %d |" % len(lookups))
    L.append("| Hediffs of Rimfolk's own the peoples give, nothing ending them | %d of %d |" % (sum(1 for h in hediffs if h[2] == "nothing ends it"), len(hediffs)))
    return L


def peoples_table(events):
    rows = []
    for calling, _, name in CALLINGS:
        mine = [e for e in events if calling in e.callings or "any" in e.callings]
        answers = [a for e in mine for a in e.answers]
        t = Counter(tier_of(a) for a in answers)
        rows.append((calling, name, len(mine), sum(1 for e in mine if e.drawn), sum(1 for e in mine if not e.drawn),
                     sum(1 for e in mine if len(e.callings) > 1), len(answers), t["game"], t["thin"] + t["flavour"], t["empty"]))
    return rows


def report(events, doors, lookups, hediffs, magic):
    L = [BEGIN, "", "Measured by `python3 Tools/peoples_impact_audit.py --write` (rewritten each run; the review's own notes are outside the markers).", ""]
    L += counts(events, doors, lookups, hediffs)
    L.append("")
    L.append("### Each people's events")
    L.append("")
    L.append("| People | Calling | Events | Drawn | Fired | Shared with another people | Answers | With a game effect or a play-out | Thin or flavour | Empty |")
    L.append("|---|---|---|---|---|---|---|---|---|---|")
    for r in peoples_table(events):
        L.append("| %s | %s | %d | %d | %d | %d | %d | %d | %d | %d |" % (r[1], r[0], *r[2:]))
    L.append("")
    L.append("An event shared by two peoples (round 10's four, the hunt or the tame) is counted for each, and the art handed on, which any of the peoples may hand on, for every one; the forms of the age are in the table of each event at the end.")
    L.append("")
    L.append("### Thin, flavour-only and empty answers")
    L.append("")
    rows = [a for e in events for a in e.answers if a.thin or a.empty]
    if rows:
        L.append("| Event | # | Answer | Carries |")
        L.append("|---|---|---|---|")
        for a in sorted(rows, key=lambda a: (a.defname, a.index)):
            L.append("| `%s` | %d | %s | %s |" % (a.defname, a.index + 1, short(a.label, 60), ia.kinds_text(a)))
    else:
        L.append("None.")
    L.append("")
    L.append("### Words that promise more (to read by hand)")
    L.append("")
    promises = [(a, p) for e in events for a in e.answers for p in ia.promises(a)]
    if promises:
        L.append("| Event | # | Promise | Words | Carries |")
        L.append("|---|---|---|---|---|")
        for a, (n, s) in sorted(promises, key=lambda x: (x[0].defname, x[0].index)):
            L.append("| `%s` | %d | %s | %s | %s |" % (a.defname, a.index + 1, n, short(s, 100), ia.kinds_text(a)))
    else:
        L.append("None.")
    L.append("")
    L.append("### Placeholders no code of the event's fills")
    L.append("")
    un = [e for e in events if e.unfilled]
    if un:
        for e in un:
            L.append("- `%s` (%s): %s" % (e.defname, e.worker, ", ".join("{" + x + "}" for x in e.unfilled)))
    else:
        L.append("None.")
    L.append("")
    L.append("### The doors that send someone to work, and their guard")
    L.append("")
    L.append("| Door | File | Sends | Refuses one drafted, in a break, down or on the player's order |")
    L.append("|---|---|---|---|")
    for d in sorted((d for d in doors if d["door"]), key=lambda d: (d["file"], d["line"])):
        L.append("| `%s` | %s | %s | %s |" % (d["method"], os.path.basename(d["file"]), "theirs only" if d["theirs"] else "the colony's too", "yes" if d["guard"] else "no"))
    L.append("")
    L.append("### Defs looked up by name without SilentFail")
    L.append("")
    L += ["- %s:%d %s" % x for x in lookups] if lookups else ["None."]
    L.append("")
    L.append("### Hediffs the peoples give, and how each ends")
    L.append("")
    L.append("| Hediff | File | How it ends |")
    L.append("|---|---|---|")
    for dn, f, how in hediffs:
        L.append("| `%s` | %s | %s |" % (dn, f, how))
    L.append("")
    L.append("### The peoples' casters")
    L.append("")
    L.append("Spells cast for the colony in peace: " + "; ".join("%s %s (%s)" % (c, s, n.lower()) for c, n, s in magic["peace"]) + ".")
    L.append("")
    L.append("Kept out of the peoples' hands: " + ", ".join("`%s`" % k for k, _ in magic["kept"]) + ".")
    L.append("")
    L.append("### Each event")
    L.append("")
    L.append("| Event | People | Drawn or fired | Ages in its texts | Answers: game, thin, empty |")
    L.append("|---|---|---|---|---|")
    for e in sorted(events, key=lambda e: (e.path, e.defname)):
        t = Counter(tier_of(a) for a in e.answers)
        L.append("| `%s` | %s | %s | %s | %d, %d, %d |" % (e.defname, ", ".join(e.callings) or "?", "drawn" if e.drawn else "fired",
                                                       " ".join(str(x) for x in e.ages) or "any", t["game"], t["thin"] + t["flavour"], t["empty"]))
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
        new = "# Review of the peoples' events\n\n" + "\n".join(lines) + "\n"
    with open(OUT, "w", encoding="utf-8") as fh:
        fh.write(new)


def main():
    args = sys.argv[1:]
    events = measure_events()
    doors = measure_doors()
    lookups = measure_lookups()
    hediffs = measure_hediffs()
    magic = measure_magic()
    for line in counts(events, doors, lookups, hediffs):
        print(line)
    if "--print" in args:
        what = args[args.index("--print") + 1]
        if what == "events":
            for e in sorted(events, key=lambda e: (e.path, e.defname)):
                t = Counter(tier_of(a) for a in e.answers)
                print("%-38s %-24s %-6s ages %-8s game %d thin %d empty %d %s" % (e.defname, ",".join(e.callings) or "?", "drawn" if e.drawn else "fired",
                                                                                    "".join(str(x) for x in e.ages), t["game"], t["thin"] + t["flavour"], t["empty"],
                                                                                    ("unfilled " + ",".join(e.unfilled)) if e.unfilled else ""))
        elif what == "peoples":
            for r in peoples_table(events):
                print("%-14s %-26s events %2d drawn %2d fired %2d shared %d answers %3d game %3d thin %2d empty %2d" % (r[0], r[1], *r[2:]))
        elif what in ("thin", "flavour"):
            for e in events:
                for a in e.answers:
                    if (what == "thin" and (a.thin or a.empty)) or (what == "flavour" and (a.flavour_only or a.empty)):
                        print("%-38s %d %-50s %s" % (a.defname, a.index + 1, short(a.label, 50), ia.kinds_text(a)))
        elif what == "promises":
            for e in events:
                for a in e.answers:
                    for n, s in ia.promises(a):
                        print("%-38s %d %-18s %s  [%s]" % (a.defname, a.index + 1, n, short(s, 100), ia.kinds_text(a)))
        elif what == "words":
            for e in events:
                if e.unfilled:
                    print(e.defname, e.worker, e.unfilled)
        elif what == "ages":
            for e in sorted(events, key=lambda e: (e.path, e.defname)):
                if not e.own_age:
                    print("%-34s %-14s %s" % (e.defname, ",".join(e.callings) or "?", ("left: " + LEFT[e.defname]) if e.left else "in the colony's age only"))
        elif what == "doors":
            for d in doors:
                print("%s %s:%d %s %s %s" % ("guarded  " if d["guard"] else "unguarded", d["file"], d["line"], d["method"], d["who"], ("door, theirs only" if d["theirs"] else "door") if d["door"] else ""))
        elif what == "lookups":
            for x in lookups:
                print("%s:%d %s" % x)
        elif what == "hediffs":
            for x in hediffs:
                print("%-28s %-36s %s" % x)
        elif what == "magic":
            for c, n, s in magic["peace"]:
                print("peace", c, n, s)
            for k, why in magic["kept"]:
                print("kept", k, short(why, 90))
    if "--write" in args:
        write(report(events, doors, lookups, hediffs, magic))
        print("Wrote", os.path.relpath(OUT, HERE))


if __name__ == "__main__":
    main()
