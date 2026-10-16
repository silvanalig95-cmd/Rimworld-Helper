#!/usr/bin/env python3
"""
Interaction audit: every place in Source/ColonyCouncil where Rimfolk says two or more people do something together.

Rimfolk's rule is that an interaction between colonists happens on the map: either only when the two are really
together (same map, awake, not drafted, downed or in a mental state, and within talking range: Together.Can /
SocialLog.Near), or visibly, by one walking over to the other (a play-out Visit or a talk job). This script finds the
code that makes two people interact and says, for each, how it is held to that rule.

What counts as an interaction (a "hit"):
  log       a social log entry (SocialLog.Add, PlayLog.Add)
  speech    a spoken line that names someone else (Speech.Say with another pawn, Named("OTHER") next to Speak/Say)
  memory    an opinion memory about another pawn (GiveThought(p, def, other), TryGainMemory(thought, other))
  tie       a bond made stronger or weaker (social.Bump / social.Weaken)
  secret    a secret confided or revealed (knowers.Add, Reveal)
  talk      a talk or lesson (Learn with a teacher, a talkInteraction, TryInteractWith)
  ease      strain eased by someone's company (Ease with StrainRelief.Friends or .Gathering)
  line      a message, news or log line that names two people (PAWN/A and OTHER/B and the like) as doing
            something together

How a method is held to the rule (its "status"):
  visible   one walks over to the other or people gather for it: a Together.When that may send one to find the
            other (its seekAfter is given), a play-out (PlayOuts.Run, a gathering or visit effect), or a job
  together  it waits for or checks that they're together: Together.When / WhenInCompany / Can / Hears, or
            SocialLog.Near; or a comment "// together: <reason>" says how they already are (the meeting's own
            gathering, a job that brought one to the other's side)
  vanilla   it is an InteractionWorker: the game's own social system already needs them face to face
  apart     a comment "// apart: <reason>" in the method says it happens apart on purpose (a letter, news of the
            council's decision, a memory of someone dead or far away)
  OPEN      none of these: needs a look

Usage: python3 Tools/interaction_audit.py [--open] [--filter REGEX] [--summary]
  --open        only list hits whose method is OPEN
  --filter RE   only files whose path matches
  --summary     counts per status and per file only
"""
import glob
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Source", "ColonyCouncil")

HITS = [
    ("log", re.compile(r"\bSocialLog\.Add\(|\bPlayLog\.Add\(|\bFind\.PlayLog\.Add\(")),
    ("speech", re.compile(r"\bSpeech\.Say\([^;]*,[^;]*,\s*(?!null\b)\w")),
    ("memory", re.compile(r"\bGiveThought\(\s*[^,()]+,\s*[^,()]+(\([^()]*\))?,\s*(?!null\b|otherPawn: null)[A-Za-z_][\w.?]*\s*[,)]")),
    ("memory", re.compile(r"\bTryGainMemory\([^;]*,\s*(?!null\b)[A-Za-z_][\w.?]*\s*\)\s*;")),
    ("tie", re.compile(r"\bsocial\.(Bump|Weaken)\(")),
    ("secret", re.compile(r"\bknowers\.Add\(|\.Reveal\(|\bConfide\(")),
    ("talk", re.compile(r"\btalkInteraction\b|\bTryInteractWith\(")),
    ("ease", re.compile(r"\bEase\([^;]*StrainRelief\.(Friends|Gathering)")),
]
# A message or news line (the call and the next two lines of its statement) naming two people.
LINE_CALL = re.compile(r"\b(News\.Post|Notify\.Message|Messages\.Message|Find\.LetterStack\.ReceiveLetter)\(")
NAMED = re.compile(r'Named\("(PAWN|A|B|OTHER|KNOWER|OWNER|LEAKER|TELLER|FRIEND)"\)')
# A line that speaks (floating text or bubble) and names a second person.
SPEAK = re.compile(r"\b(Speak|Say|Speech\.Say)\(")
OTHER = re.compile(r'Named\("(OTHER|B|FRIEND|TEACHER|STUDENT|RIVAL|PARTNER)"\)')

GATE = re.compile(r"\bTogether\.(Can|When|WhenInCompany|Hears|Company)\(|\bSocialLog\.Near\(|//\s*together:")
VISIBLE = re.compile(r"\bPlayOuts\.Run\(|\bnew DilemmaEffect_(Gathering|Feast|Bonfire|Toast|Words|Visit|Lesson)\b|\bJobMaker\.MakeJob\(")
APART = re.compile(r"//\s*apart:")
WHEN = re.compile(r"\bTogether\.When\(")


def call_args(text, start):
    """The top-level arguments of the call whose '(' is at text[start]."""
    depth, args, cur, i = 0, [], [], start
    in_str = False
    while i < len(text):
        c = text[i]
        if in_str:
            cur.append(c)
            if c == '\\':
                cur.append(text[i + 1])
                i += 2
                continue
            if c == '"':
                in_str = False
        elif c == '"':
            in_str = True
            cur.append(c)
        elif c in "([{":
            depth += 1
            if depth > 1:
                cur.append(c)
        elif c in ")]}":
            depth -= 1
            if depth == 0:
                args.append("".join(cur).strip())
                return args
            cur.append(c)
        elif c == "," and depth == 1:
            args.append("".join(cur).strip())
            cur = []
        else:
            cur.append(c)
        i += 1
    return args


def seeks(body):
    """A Together.When in the body that lets one go and find the other (a seekAfter that isn't -1)."""
    for m in WHEN.finditer(body):
        args = call_args(body, m.end() - 1)
        if len(args) >= 6 and not args[5].replace(" ", "").startswith("-"):
            return True
    return False
METHOD = re.compile(
    r"^\s*(public|private|protected|internal|static|override|virtual|\s)+[\w<>\[\],.? ]+\s+(\w+)\s*\([^;]*$")
CLASS = re.compile(r"\bclass\s+(\w+)\s*(:\s*([\w.<>]+))?")


def methods(lines):
    """Rough method spans: (name, start, end, class name, base class)."""
    out = []
    cls, base = None, None
    i = 0
    n = len(lines)
    while i < n:
        line = lines[i]
        m = CLASS.search(line)
        if m and not line.strip().startswith("//"):
            cls, base = m.group(1), m.group(3)
        m = METHOD.match(line)
        if m and not line.strip().startswith("//") and " new " not in line and "=" not in line.split("(")[0]:
            name = m.group(2)
            # find the opening brace (or an expression body)
            j = i
            while j < n and "{" not in lines[j] and "=>" not in lines[j]:
                j += 1
            if j >= n:
                break
            if "=>" in lines[j] and "{" not in lines[j].split("=>")[0]:
                k = j
                while k < n and not lines[k].rstrip().endswith(";"):
                    k += 1
                out.append((name, i, k, cls, base))
                i = k + 1
                continue
            depth = 0
            k = j
            started = False
            while k < n:
                s = strip_strings(lines[k])
                depth += s.count("{") - s.count("}")
                if s.count("{"):
                    started = True
                if started and depth <= 0:
                    break
                k += 1
            out.append((name, i, k, cls, base))
            i = k + 1
            continue
        i += 1
    return out


def strip_strings(s):
    s = re.sub(r'"([^"\\]|\\.)*"', '""', s)
    s = re.sub(r"'([^'\\]|\\.)'", "''", s)
    return s.split("//")[0]


def hits_in(lines, start, end):
    found = []
    for i in range(start, end + 1):
        code = lines[i]
        if code.strip().startswith("//") or code.strip().startswith("///") or code.strip().startswith("*"):
            continue
        for kind, rx in HITS:
            if rx.search(code):
                found.append((i, kind))
                break
        else:
            if SPEAK.search(code) and OTHER.search(code):
                found.append((i, "speech"))
            elif LINE_CALL.search(code):
                stmt = " ".join(lines[i:min(end + 1, i + 3)])
                stmt = stmt.split(";")[0]
                if len(set(m.group(1) for m in NAMED.finditer(stmt))) >= 2:
                    found.append((i, "line"))
    return found


def status(lines, start, end, base):
    body = "\n".join(lines[start:end + 1])
    if base and "InteractionWorker" in base:
        return "vanilla"
    if VISIBLE.search(body) or seeks(body):
        return "visible"
    if GATE.search(body):
        return "together"
    if APART.search(body):
        return "apart"
    return "OPEN"


def main(argv):
    only_open = "--open" in argv
    summary = "--summary" in argv
    flt = None
    if "--filter" in argv:
        flt = re.compile(argv[argv.index("--filter") + 1])
    files = sorted(glob.glob(os.path.join(ROOT, "**", "*.cs"), recursive=True))
    counts = {}
    per_file = {}
    rows = []
    for path in files:
        rel = os.path.relpath(path, ROOT)
        if flt and not flt.search(rel):
            continue
        with open(path, encoding="utf-8") as f:
            lines = f.read().split("\n")
        for name, s, e, cls, base in methods(lines):
            found = hits_in(lines, s, e)
            if not found:
                continue
            st = status(lines, s, e, base)
            reason = ""
            m = re.search(r"//\s*(apart|together):\s*(.*)", "\n".join(lines[s:e + 1]))
            if m and st in ("apart", "together"):
                reason = m.group(2).strip()
            kinds = sorted(set(k for _, k in found))
            counts[st] = counts.get(st, 0) + 1
            per_file.setdefault(rel, {}).setdefault(st, 0)
            per_file[rel][st] += 1
            rows.append((rel, cls, name, s + 1, st, kinds, [i + 1 for i, _ in found], reason))
    if summary:
        for rel in sorted(per_file):
            print(f"{rel}: " + ", ".join(f"{k} {v}" for k, v in sorted(per_file[rel].items())))
    else:
        for rel, cls, name, line, st, kinds, at, reason in rows:
            if only_open and st != "OPEN":
                continue
            extra = f"  ({reason})" if reason else ""
            print(f"{st:8} {rel}:{line} {cls}.{name} [{','.join(kinds)}] lines {','.join(map(str, at))}{extra}")
    print()
    print("methods: " + ", ".join(f"{k} {v}" for k, v in sorted(counts.items())) + f"  (total {sum(counts.values())})")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
