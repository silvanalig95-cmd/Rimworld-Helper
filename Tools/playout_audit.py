#!/usr/bin/env python3
"""
Play-out audit: which answers say people do something on the map, but leave it to a mood?

Walks every answer in Defs/ (dilemma options, Rimfolk events and their story stages and missions, world events' beats
and their options, decrees, ambitions) and:

  * counts the answers that play out on the map (an effect of PlayOut/: a feast, a gathering, a bonfire, a toast, words,
    a visit, a lesson, a day off, a hunt, a work party, a walk together, a vigil, a search, a gift carried out), by
    source and by kind, for the README and the ROADMAP; and, on a line of their own, the stages of personal stories and
    the encounters that play out (not answers, so never candidates);
  * lists the answers whose words describe a physical activity (a meal, a vigil, a party, a visit, a lesson, a work
    party, a search, a funeral, a hunt, a procession, a gift carried out, a cleaning, a rebuilding...) and have no
    play-out, as candidates. Not every candidate fits: some happen far from the map ("half a day's walk out"), some are
    told about rather than done, some are refusals, some are done by the event's own code. Those read by hand and left
    are in the skip list (Tools/playout_skips.txt, one "DefName [where] why" a line): they're kept out of the
    candidates, --skip lists them with why, and one that has gone stale (the answer is gone, or now plays out) is
    named after the candidates, so the list stays true.

Usage: python3 Tools/playout_audit.py [--counts] [--candidates] [--skip] [--filter REGEX]
  (no flags: the counts and the candidates)
"""
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SKIPS = os.path.join(ROOT, "Tools", "playout_skips.txt")
SKIP_LINE = re.compile(r"^(\S+) \[([^\]]+)\]\s*(.*)$")

PLAY = {
    "DilemmaEffect_Gathering": "gathering",
    "DilemmaEffect_Feast": "feast",
    "DilemmaEffect_Bonfire": "bonfire",
    "DilemmaEffect_Toast": "toast",
    "DilemmaEffect_Words": "words",
    "DilemmaEffect_Visit": "visit",
    "DilemmaEffect_Lesson": "lesson",
    "DilemmaEffect_Rest": "rest",
    "DilemmaEffect_Hunt": "hunt",
    "DilemmaEffect_WorkParty": "work party",
    "DilemmaEffect_Procession": "walk together",
    "DilemmaEffect_Vigil": "vigil",
    "DilemmaEffect_Search": "search",
    "DilemmaEffect_CarryOut": "gift carried out",
    "DilemmaEffect_HuntParty": "hunt party",
    # Faith and the council (Dilemmas/DilemmaWorkers_Faith.cs): words said for a faith, and an evening of faith called.
    "DilemmaEffect_FaithWords": "words",
    "DilemmaEffect_FaithEvening": "evening of faith",
}

# Words that say people do something together, or go somewhere on the map. Deliberately wide: the list is read by hand.
ACTIVITY = re.compile(
    r"\b(feast\w*|meal|supper|dinner|breakfast|lunch|picnic|ate|eat(s|ing)?|party|parties|celebrat\w+|danc\w+|sang|sing\w*|songs?"
    r"|music|bonfire|fire ?side|campfire|vigil|keep watch|kept watch|sat up|sit(s|ting)? (up )?with|sat with|keep\w* .{0,12}company"
    r"|visit\w*|walk\w*|stroll\w*|lesson\w*|teach\w*|taught|tutor\w*|show\w* .{0,20}how|apprentic\w+"
    r"|clean\w*|scrub\w*|sweep\w*|swept|tidy|tidied|haul\w*|rebuil\w+|repair\w*|mend\w*|patch\w* up|clear\w* (out|up|away)"
    r"|search\w*|look(s|ed|ing)? for|comb(ed|ing)?|scour\w*|hunt\w*|track(s|ed|ing)?|funeral|bur(y|ied|ial)|graves?|mourn\w*"
    r"|procession|parade|march\w*|escort\w*|see .{0,20} off|saw .{0,20} off|to the (gate|edge|road)|gifts?|carr(y|ied|ies)"
    r"|toast\w*|drink\w*|swim\w*|games?|contest|race|stor(y|ies) told|read\w* aloud|pray\w*|bless\w*|gather\w*|together|everyone)\b",
    re.I)

PLAY_RE = re.compile(r"DilemmaEffect_(\w+)")


def text_of(el):
    return "" if el is None or el.text is None else el.text.strip()


def lis(el, name):
    node = el.find(name)
    return [] if node is None else list(node.findall("li"))


def effect_kinds(option):
    kinds = []
    for name in ("effects", "riskEffects"):
        for li in lis(option, name):
            cls = (li.get("Class") or "").replace("ColonyCouncil.", "")
            if cls in PLAY:
                kinds.append(PLAY[cls])
    return kinds


class Answer:
    def __init__(self, source, defname, where, label, words, kinds, path):
        self.source = source
        self.defname = defname
        self.where = where
        self.label = label
        self.words = words
        self.kinds = kinds
        self.path = path


def option_words(option):
    parts = [text_of(option.find("label")), text_of(option.find("outcome"))]
    for name in ("outcomes", "riskOutcomes"):
        parts += [text_of(li) for li in lis(option, name)]
    parts.append(text_of(option.find("riskOutcome")))
    return " ".join(p for p in parts if p)


def walk():
    answers = []
    for path in sorted(glob.glob(os.path.join(ROOT, "Defs", "**", "*.xml"), recursive=True)):
        try:
            tree = ET.parse(path)
        except ET.ParseError as e:
            print("cannot parse", path, e, file=sys.stderr)
            continue
        rel = os.path.relpath(path, ROOT)
        for d in tree.getroot():
            tag = d.tag
            defname = text_of(d.find("defName"))
            if tag == "ColonyCouncil.DilemmaDef":
                for i, o in enumerate(lis(d, "options")):
                    answers.append(Answer("dilemma", defname, "option %d" % i, text_of(o.find("label")), option_words(o), effect_kinds(o), rel))
            elif tag == "IncidentDef":
                ext = None
                story = mission = False
                for li in lis(d, "modExtensions"):
                    cls = li.get("Class") or ""
                    if cls.endswith("HappeningExtension"):
                        ext = li
                    elif cls.endswith("StoryText"):
                        story = True
                    elif cls.endswith("MissionText"):
                        mission = True
                if ext is None:
                    continue
                source = "mission" if mission else "story" if story else "event"
                options = lis(ext, "options")
                for i, o in enumerate(options):
                    answers.append(Answer(source, defname, "option %d" % i, text_of(o.find("label")), option_words(o), effect_kinds(o), rel))
                if not options:
                    words = " ".join(text_of(t) for t in lis(ext, "texts"))
                    kinds = [PLAY[(li.get("Class") or "").replace("ColonyCouncil.", "")] for li in lis(ext, "effects")
                             if (li.get("Class") or "").replace("ColonyCouncil.", "") in PLAY]
                    answers.append(Answer(source, defname, "letter", text_of(d.find("label")), words, kinds, rel))
            elif tag == "ColonyCouncil.WorldSituationDef":
                for b in lis(d, "beats"):
                    key = text_of(b.find("key"))
                    options = lis(b, "options")
                    for i, o in enumerate(options):
                        answers.append(Answer("world", defname, "%s/%d" % (key, i), text_of(o.find("label")), option_words(o), effect_kinds(o), rel))
                    if not options:
                        words = " ".join(text_of(t) for t in lis(b, "texts"))
                        answers.append(Answer("world", defname, key, key, words, effect_kinds(b), rel))
                for e in lis(d, "endings"):
                    key = text_of(e.find("key"))
                    words = " ".join(text_of(t) for t in lis(e, "texts"))
                    answers.append(Answer("world", defname, "end:" + key, key, words, effect_kinds(e), rel))
            elif tag == "ColonyCouncil.DecreeDef":
                kinds = [PLAY.get((li.get("Class") or "").replace("ColonyCouncil.", "")) for li in lis(d, "onEnact")]
                answers.append(Answer("decree", defname, "enact", text_of(d.find("label")), text_of(d.find("description")), [k for k in kinds if k], rel))
            elif tag == "ColonyCouncil.AmbitionDef":
                kinds = [PLAY.get((li.get("Class") or "").replace("ColonyCouncil.", "")) for li in lis(d, "achievedEffects")]
                answers.append(Answer("ambition", defname, "achieved", text_of(d.find("label")), text_of(d.find("description")), [k for k in kinds if k], rel))
            elif tag == "ColonyCouncil.ArcDef":
                # Not answers: a personal story's stages, counted apart (never candidates).
                for i, st in enumerate(lis(d, "stages")):
                    kinds = []
                    for li in lis(st, "effects"):
                        cls = (li.get("Class") or "").replace("ColonyCouncil.", "")
                        if cls in PLAY:
                            kinds.append(PLAY[cls])
                        elif cls == "ArcEffect_Cast":
                            kinds += [PLAY[c] for c in ((x.get("Class") or "").replace("ColonyCouncil.", "") for x in lis(li, "effects")) if c in PLAY]
                    answers.append(Answer("arc", defname, "stage %d" % i, text_of(st.find("label")), "", kinds, rel))
            elif tag == "ColonyCouncil.EncounterDef":
                answers.append(Answer("encounter", defname, "moment", text_of(d.find("label")), "", effect_kinds(d), rel))
    return answers


# Not answers to a question: counted on their own line, never listed as candidates.
APART = ("arc", "encounter")


def counts(answers):
    apart = [a for a in answers if a.kinds and a.source in APART]
    answers = [a for a in answers if a.source not in APART]
    played = [a for a in answers if a.kinds]
    by_source = defaultdict(lambda: [0, set()])
    for a in played:
        by_source[a.source][0] += 1
        by_source[a.source][1].add(a.defname)
    kinds = Counter(k for a in played for k in set(a.kinds))
    print("Answers that play out on the map: %d" % len(played))
    for src in ("dilemma", "event", "story", "mission", "world", "decree", "ambition"):
        n, defs = by_source.get(src, [0, set()])
        print("  %-9s %4d answers in %3d defs" % (src, n, len(defs)))
    dil_ev = sum(by_source[s][0] for s in ("dilemma", "event", "story", "mission", "world"))
    print("  answers of dilemmas, events, stories, missions and world events: %d" % dil_ev)
    print("  dilemmas with one: %d; events, stories, missions and world events with one: %d" % (
        len(by_source["dilemma"][1]), len(by_source["event"][1] | by_source["story"][1] | by_source["mission"][1] | by_source["world"][1])))
    print("By kind (answers using it):")
    for k, n in kinds.most_common():
        print("  %-16s %4d" % (k, n))
    arcs = [a for a in apart if a.source == "arc"]
    enc = [a for a in apart if a.source == "encounter"]
    print("Besides answers: %d stages of %d personal stories, and %d encounters" % (len(arcs), len({a.defname for a in arcs}), len(enc)))


def load_skips():
    """The skip list: (defName, where) -> why, in the file's order."""
    skips = {}
    if not os.path.exists(SKIPS):
        return skips
    with open(SKIPS, encoding="utf-8") as f:
        for n, line in enumerate(f, 1):
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            m = SKIP_LINE.match(line)
            if not m:
                print("playout_skips.txt:%d: not 'DefName [where] why': %s" % (n, line), file=sys.stderr)
                continue
            skips[(m.group(1), m.group(2))] = m.group(3).strip()
    return skips


def candidates(answers, flt, skips):
    rx = re.compile(flt, re.I) if flt else None
    rows = []
    skipped = 0
    for a in answers:
        if a.kinds or a.source in ("decree", "ambition") + APART:
            continue
        hits = sorted(set(m.group(0).lower() for m in ACTIVITY.finditer(a.words)))
        if not hits:
            continue
        if rx and not (rx.search(a.defname) or rx.search(a.words)):
            continue
        if (a.defname, a.where) in skips:
            skipped += 1
            continue
        rows.append((a, hits))
    print("\nCandidates (answers whose words describe something done, with no play-out): %d (and %d looked at and left: --skip)" % (len(rows), skipped))
    last = None
    for a, hits in rows:
        if a.path != last:
            print("\n== %s" % a.path)
            last = a.path
        print("  %-8s %s [%s] %s\n           words: %s" % (a.source, a.defname, a.where, a.label[:70], ", ".join(hits[:10])))
    stale(answers, skips)


def stale(answers, skips):
    """Skip entries that no longer say anything true: the answer is gone, or it plays out now."""
    by_key = {(a.defname, a.where): a for a in answers}
    gone = [k for k in skips if k not in by_key]
    played = [k for k in skips if k in by_key and by_key[k].kinds]
    if not gone and not played:
        return
    print("\nStale entries in playout_skips.txt: %d" % (len(gone) + len(played)))
    for d, w in gone:
        print("  gone:       %s [%s]" % (d, w))
    for d, w in played:
        print("  plays out:  %s [%s]" % (d, w))


def list_skips(answers, flt, skips):
    rx = re.compile(flt, re.I) if flt else None
    by_key = {(a.defname, a.where): a for a in answers}
    rows = [(k, why) for k, why in skips.items() if not rx or rx.search(k[0]) or rx.search(why)]
    print("\nLooked at and left (Tools/playout_skips.txt): %d" % len(rows))
    last = None
    for (d, w), why in rows:
        a = by_key.get((d, w))
        path = a.path if a else "(no such answer)"
        if path != last:
            print("\n== %s" % path)
            last = path
        print("  %s [%s]: %s" % (d, w, why))
    heads = Counter(reason_kind(why) for _, why in rows)
    print("\nBy reason:")
    for h, n in heads.most_common():
        print("  %4d  %s" % (n, h))


# The kinds of reason, for the summary: the first that fits.
REASONS = [
    ("a refusal, a ruling, or nothing done", r"refusal|nothing done|ruling|not answered|an order|a ban|a promise made|a wager|an office|a rule for"),
    ("a habit, or it takes days", r"habit|takes days|for days|over a week|two days|every night|a rota|a roster"),
    ("far away or off the map", r"far away|off the map|news|comms|not on the map|aren't on the map|nobody waiting|away from the colony|where they were found"),
    ("told after it happened", r"told after|already happened|already over|last night|already been out|a find already|came and went|arrives|already played out"),
    ("someone else's part (a stranger, a guest, an animal)", r"part\b|the camp's own|the strangers|the visitors|pass by|passes|watching"),
    ("already real, or the event's own code", r"already real|own code|already its|sends them away|the game's own|a trade"),
    ("would hurt someone or take the player's place", r"hurt|player's"),
    ("no play-out says it honestly", r"no play-out kind|no kind|not a meal|a moment|not a thing on the map|not an activity|tomorrow|a letter written|by the game's doctor"),
]


def reason_kind(why):
    for name, rx in REASONS:
        if re.search(rx, why, re.I):
            return name
    return "other: " + why


def main():
    args = sys.argv[1:]
    flt = None
    if "--filter" in args:
        flt = args[args.index("--filter") + 1]
    answers = walk()
    skips = load_skips()
    modes = ("--counts", "--candidates", "--skip")
    want_counts = "--counts" in args or not any(a in args for a in modes)
    want_cands = "--candidates" in args or not any(a in args for a in modes)
    if want_counts:
        counts(answers)
    if want_cands:
        candidates(answers, flt, skips)
    if "--skip" in args:
        list_skips(answers, flt, skips)
        stale(answers, skips)


if __name__ == "__main__":
    main()
