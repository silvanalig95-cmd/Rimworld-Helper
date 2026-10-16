#!/usr/bin/env python3
"""
Play-out audit: which answers say people do something on the map, but leave it to a mood?

Walks every answer in Defs/ (dilemma options, Rimfolk events and their story stages and missions, world events' beats
and their options, decrees, ambitions) and:

  * counts the answers that play out on the map (an effect of PlayOut/: a feast, a gathering, a bonfire, a toast, words,
    a visit, a lesson, a day off, a hunt, a work party, a walk together, a vigil, a search, a gift carried out), by
    source and by kind, for the README and the ROADMAP;
  * lists the answers whose words describe a physical activity (a meal, a vigil, a party, a visit, a lesson, a work
    party, a search, a funeral, a hunt, a procession, a gift carried out, a cleaning, a rebuilding...) and have no
    play-out, as candidates. Not every candidate fits: some happen far from the map ("half a day's walk out"), some are
    told about rather than done, some are done by the event's own code. --skip lists what was looked at and left.

Usage: python3 Tools/playout_audit.py [--counts] [--candidates] [--filter REGEX] [--kinds]
  (no flags: both the counts and the candidates)
"""
import glob
import os
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

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
    return answers


def counts(answers):
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


def candidates(answers, flt):
    rx = re.compile(flt, re.I) if flt else None
    rows = []
    for a in answers:
        if a.kinds or a.source in ("decree", "ambition"):
            continue
        hits = sorted(set(m.group(0).lower() for m in ACTIVITY.finditer(a.words)))
        if not hits:
            continue
        if rx and not (rx.search(a.defname) or rx.search(a.words)):
            continue
        rows.append((a, hits))
    print("\nCandidates (answers whose words describe something done, with no play-out): %d" % len(rows))
    last = None
    for a, hits in rows:
        if a.path != last:
            print("\n== %s" % a.path)
            last = a.path
        print("  %-8s %s [%s] %s\n           words: %s" % (a.source, a.defname, a.where, a.label[:70], ", ".join(hits[:10])))


def main():
    args = sys.argv[1:]
    flt = None
    if "--filter" in args:
        flt = args[args.index("--filter") + 1]
    answers = walk()
    want_counts = "--counts" in args or not any(a in args for a in ("--counts", "--candidates"))
    want_cands = "--candidates" in args or not any(a in args for a in ("--counts", "--candidates"))
    if want_counts:
        counts(answers)
    if want_cands:
        candidates(answers, flt)


if __name__ == "__main__":
    main()
