#!/usr/bin/env python3
"""
Checks rewritten text against the committed version (git HEAD), so a prose pass can't break anything:

  - every file still parses, and nothing but text changed: defNames, classes, numbers, references, attributes
    and the order of elements are identical; lines may be added to a list, never removed;
  - each rewritten line keeps its leading tag block ([winter], [trait:Kind], [!lowtech]...) exactly;
  - each rewritten line uses only placeholders it may: the same {ROOT_...} people and values as before (a pronoun
    form may change, {A_nameDef} -> {A_pronoun}), grammar symbols ([INITIATOR_nameDef], [decree]) kept, and numbered
    arguments ({0}, {1}) never dropped;
  - and it reports how often the tics listed in STYLE.md show up, before and after.

Usage: python3 Tools/textcheck.py [--base REF] [files...]
  --base REF  compare against that commit instead of HEAD (e.g. the commit before a long pass began)
  no files: every .xml under Defs/ and Languages/ that differs from the base
Exit code 1 if anything is broken.
"""
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections import Counter

TAG_BLOCK = re.compile(r"^\[([^\]]*)\]\s*")
BRACE = re.compile(r"\{([^{}]+)\}")
SQUARE = re.compile(r"\[([^\[\]]+)\]")
# A pronoun placeholder opening a sentence ("... home. {A_pronoun} said") isn't capitalized by the game.
LOWER_START = re.compile(r"(^(\[[^\]]*\]\s*)?|[.!?]\s+|\\n)\{[A-Z]+_(pronoun|possessive|objective)\}")
KNOWN_SUFFIXES = {
    "nameDef", "nameFull", "nameIndef", "labelShort", "label", "definite", "indefinite", "pronoun", "possessive",
    "objective", "gender", "title", "kind", "labelPlural", "factionName", "nameShortDef", "nameShort",
}


def run(*args):
    return subprocess.run(args, capture_output=True, text=True).stdout


BASE = "HEAD"


def changed_files():
    out = run("git", "diff", "--name-only", BASE) + run("git", "ls-files", "--others", "--exclude-standard")
    return [f for f in out.split() if f.endswith(".xml") and (f.startswith("Defs/") or f.startswith("Languages/"))]


def is_text(s):
    """Prose rather than an identifier, number or reference."""
    return s is not None and " " in s.strip() and re.search(r"[A-Za-z]{2,}", s) is not None


def tag_block(s):
    m = TAG_BLOCK.match(s)
    if not m:
        return ""
    inner = m.group(1)
    # A grammar symbol at the start of a sentence ([INITIATOR_nameDef] laughed) is not a tag block.
    if re.match(r"^[A-Z][A-Za-z]*_", inner) or re.match(r"^[a-z_]+$", inner) and inner in ("jabcount", "decree"):
        return ""
    return m.group(0).strip()


def tokens(s):
    """Placeholders in a line (tag block removed): ({...}, [...])."""
    tb = tag_block(s)
    body = s[len(tb):] if tb and s.startswith(tb) else s
    braces = set(BRACE.findall(body))
    squares = set(SQUARE.findall(body))
    return braces, squares


def root(tok):
    return tok.split("_")[0] if "_" in tok else tok


def walk(el, path, out):
    out.append((path, el))
    counts = Counter()
    for child in el:
        counts[child.tag] += 1
        walk(child, f"{path}/{child.tag}[{counts[child.tag]}]", out)


def parse(text):
    root_el = ET.fromstring(text)
    items = []
    walk(root_el, root_el.tag, items)
    return items


def tic_report(texts):
    joined = [t for t in texts if is_text(t)]
    n = max(1, len(joined))
    sents = [x.strip() for t in joined for x in re.split(r"(?<=[.!?])\s+", t) if x.strip()]
    starts = Counter(" ".join(re.sub(r"\{[^}]*\}", "X", s).split()[:2]) for s in sents)
    top_start, top_count = starts.most_common(1)[0] if starts else ("", 0)
    words = Counter(w for t in joined for w in re.findall(r"[a-z']+", t.lower()))
    return {
        "lines": len(joined),
        "! per 100 lines": round(100 * sum(t.count("!") for t in joined) / n, 1),
        "Nobody/Everyone endings": sum(1 for t in joined if re.search(r"(Nobody|Everyone|No one)\b[^.!?]*[.!?]\s*$", t)),
        "colon reveals": sum(1 for t in joined if re.search(r"[a-z]: [a-z]", t)),
        "I dreamt openings": sum(1 for s in sents if s.startswith("I dreamt")),
        "nobody/everyone/something per 100": round(100 * (words["nobody"] + words["everyone"] + words["something"]) / n, 1),
        "a little/somehow/quietly": words["somehow"] + words["quietly"] + sum(t.lower().count("a little") for t in joined),
        "most common opening": f"{top_start!r} x{top_count}",
    }


def check(path):
    errors, warnings = [], []
    try:
        new_text = open(path, encoding="utf-8").read()
        new = parse(new_text)
    except Exception as e:
        return [f"{path}: doesn't parse: {e}"], [], None, None
    old_text = run("git", "show", f"{BASE}:{path}")
    if not old_text:
        return [], [f"{path}: new file, not compared"], None, tic_report([e.text for _, e in new])
    old = parse(old_text)
    old_map = dict(old)
    new_map = dict(new)
    keyed = path.startswith("Languages/")

    for p, oe in old:
        ne = new_map.get(p)
        if ne is None:
            errors.append(f"{path}: {p} was removed")
            continue
        if oe.attrib != ne.attrib:
            errors.append(f"{path}: {p} attributes changed")
        ot, nt = (oe.text or ""), (ne.text or "")
        if ot.strip() == nt.strip():
            continue
        if not is_text(ot):
            errors.append(f"{path}: {p} isn't prose and must not change: {ot.strip()!r} -> {nt.strip()!r}")
            continue
        if tag_block(ot) != tag_block(nt):
            errors.append(f"{path}: {p} tag block changed: {tag_block(ot)!r} -> {tag_block(nt)!r}")
        ob, osq = tokens(ot)
        nb, nsq = tokens(nt)
        for t in nb - ob:
            if root(t) not in {root(x) for x in ob} and not t.isdigit():
                errors.append(f"{path}: {p} new placeholder {{{t}}} (nobody of that name was in the line)")
            elif "_" in t and t.split("_", 1)[1] not in KNOWN_SUFFIXES and t not in old_text:
                errors.append(f"{path}: {p} unknown placeholder form {{{t}}}")
        for t in ob - nb:
            if t.isdigit() or root(t) not in {root(x) for x in nb}:
                (errors if (keyed or t.isdigit() or t in ("COUNT", "AMOUNT", "DAYS")) else warnings).append(
                    f"{path}: {p} dropped placeholder {{{t}}}")
        for t in osq - nsq:
            if root(t) not in {root(x) for x in nsq}:
                errors.append(f"{path}: {p} dropped grammar symbol [{t}]")
        for t in nsq - osq:
            if root(t) not in {root(x) for x in osq} and t not in old_text:
                errors.append(f"{path}: {p} new grammar symbol [{t}]")
        if LOWER_START.search(nt) and not LOWER_START.search(ot):
            warnings.append(f"{path}: {p} a sentence starts with a pronoun placeholder (it would show lowercase): {nt.strip()[:80]!r}")
        if nt.count("->") != ot.count("->") or (ot.split("->")[0] if "->" in ot else "") != (nt.split("->")[0] if "->" in nt else ""):
            errors.append(f"{path}: {p} rule key changed")

    # Added lines: only inside an existing list, with tags and placeholders its siblings use.
    for p, ne in new:
        if p in old_map:
            continue
        parent = p.rsplit("/", 1)[0]
        if parent not in old_map or not p.rsplit("/", 1)[1].startswith("li["):
            errors.append(f"{path}: {p} added outside an existing list")
            continue
        sib = [e.text or "" for e in old_map[parent]]
        sib_tags = set(tag_block(s) for s in sib)
        sib_b = set().union(*[tokens(s)[0] for s in sib]) if sib else set()
        sib_s = set().union(*[tokens(s)[1] for s in sib]) if sib else set()
        t = ne.text or ""
        if tag_block(t) not in sib_tags:
            errors.append(f"{path}: {p} added line has a tag block no sibling has: {tag_block(t)!r}")
        b, sq = tokens(t)
        for x in b:
            if root(x) not in {root(y) for y in sib_b}:
                errors.append(f"{path}: {p} added line uses {{{x}}}, which its list never does")
        for x in sq:
            if x not in sib_s and root(x) not in {root(y) for y in sib_s}:
                errors.append(f"{path}: {p} added line uses [{x}], which its list never does")

    before = tic_report([e.text for _, e in old])
    after = tic_report([e.text for _, e in new])
    return errors, warnings, before, after


def main():
    global BASE
    args = sys.argv[1:]
    if len(args) >= 2 and args[0] == "--base":
        BASE = args[1]
        args = args[2:]
    files = args or changed_files()
    bad = False
    for f in files:
        errors, warnings, before, after = check(f)
        print(f"== {f}")
        for e in errors:
            print("  ERROR " + e)
        for w in warnings[:15]:
            print("  warn  " + w)
        if len(warnings) > 15:
            print(f"  warn  ... {len(warnings) - 15} more")
        if before and after:
            for k in after:
                if before.get(k) != after.get(k):
                    print(f"  {k}: {before.get(k)} -> {after.get(k)}")
        bad |= bool(errors)
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
