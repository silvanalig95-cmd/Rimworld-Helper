#!/usr/bin/env python3
"""
Checks restructured Almanac pages against a base commit, so a rewrite into sections can't lose anything:

  - every page is still there, with the same category, order, keywords, see-also links, mods, DLC, template and
    "approximate" flag;
  - every number the old page stated (description, facts, advice, tip, summary, sections) still appears somewhere
    on the new page;
  - no em dashes, and tables have the same number of cells in every row.
It also counts pages still in the old facts/advice shape.

Usage: python3 Tools/almanaccheck.py [--base REF] [files...]   (default base HEAD; default files: all Almanac files)
Exit code 1 if anything is broken.
"""
import glob
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

NUMBER = re.compile(r"\d+(?:[.,]\d+)*")
KEEP = ["category", "order", "keywords", "seeAlso", "mods", "dlc", "template", "approximate", "label"]


def pages(text):
    root = ET.fromstring(text)
    out = {}
    for el in root:
        dn = el.findtext("defName")
        if dn:
            out[dn] = el
    return out


def all_text(el):
    parts = []
    for tag in ("description", "tip", "summary"):
        t = el.findtext(tag)
        if t:
            parts.append(t)
    for tag in ("facts", "advice"):
        node = el.find(tag)
        if node is not None:
            parts += [li.text or "" for li in node]
    secs = el.find("sections")
    if secs is not None:
        for sec in secs:
            for tag in ("heading", "text", "after"):
                t = sec.findtext(tag)
                if t:
                    parts.append(t)
            for tag in ("points", "table"):
                node = sec.find(tag)
                if node is not None:
                    parts += [li.text or "" for li in node]
    return "\n".join(parts)


def canon(el):
    return ET.tostring(el, encoding="unicode").strip() if el is not None else None


def main():
    args = sys.argv[1:]
    base = "HEAD"
    if len(args) >= 2 and args[0] == "--base":
        base, args = args[1], args[2:]
    files = args or sorted(glob.glob("Defs/AlmanacArticleDefs/*.xml"))
    bad = False
    old_shape = 0
    total = 0
    for f in files:
        new = pages(open(f, encoding="utf-8").read())
        old_text = subprocess.run(["git", "show", f"{base}:{f}"], capture_output=True, text=True).stdout
        old = pages(old_text) if old_text else {}
        for dn in old:
            if dn not in new:
                print(f"ERROR {f}: page {dn} was removed")
                bad = True
        for dn, el in new.items():
            total += 1
            if el.find("facts") is not None or el.find("advice") is not None:
                old_shape += 1
            text = all_text(el)
            if "—" in text:
                print(f"ERROR {f}: {dn} has an em dash")
                bad = True
            secs = el.find("sections")
            if secs is not None:
                for sec in secs:
                    tb = sec.find("table")
                    if tb is not None:
                        widths = {len((li.text or "").split("|")) for li in tb}
                        if len(widths) > 1:
                            print(f"ERROR {f}: {dn} table rows have different numbers of cells {sorted(widths)}")
                            bad = True
            o = old.get(dn)
            if o is None:
                continue
            for tag in KEEP:
                if canon(o.find(tag)) != canon(el.find(tag)):
                    print(f"ERROR {f}: {dn} <{tag}> changed")
                    bad = True
            lost = set(NUMBER.findall(all_text(o))) - set(NUMBER.findall(text))
            if lost:
                print(f"ERROR {f}: {dn} lost numbers {sorted(lost)}")
                bad = True
    print(f"{total} pages, {old_shape} still in the old facts/advice shape")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
