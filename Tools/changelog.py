#!/usr/bin/env python3
"""
Writes CHANGELOG.md from the Almanac's "What's new" page (Defs/AlmanacArticleDefs/Almanac_WhatsNew.xml), so the
changelog, the page and the letter after an update all say the same thing. Edit the page, then run this.

Usage: python3 Tools/changelog.py [--check]
  --check   don't write: exit 1 if CHANGELOG.md isn't what the page would write, or if the newest round's version
            isn't About.xml's modVersion
"""
import os
import sys
import xml.etree.ElementTree as ET

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PAGE = os.path.join(ROOT, "Defs", "AlmanacArticleDefs", "Almanac_WhatsNew.xml")
OUT = os.path.join(ROOT, "CHANGELOG.md")
ABOUT = os.path.join(ROOT, "About", "About.xml")

HEADER = """# Rimfolk changelog

What changed in each update of Rimfolk, newest first, written for players. The same list is the "What's new" page at
the top of the Almanac in the game, and a letter shows the newest round once when you load a save after an update.

<!-- Written by Tools/changelog.py from Defs/AlmanacArticleDefs/Almanac_WhatsNew.xml: edit that file, then run the script. -->
"""


def rounds():
    root = ET.parse(PAGE).getroot()
    page = next(el for el in root if el.findtext("defName") == "Council_Alm_WhatsNew")
    out = []
    for li in page.find("rounds"):
        out.append({
            "version": (li.findtext("version") or "").strip(),
            "date": (li.findtext("date") or "").strip(),
            "title": (li.findtext("title") or "").strip(),
            "points": [(p.text or "").strip() for p in li.find("points")],
        })
    return out


def render(rs):
    parts = [HEADER]
    for r in rs:
        when = r["date"] + (f", version {r['version']}" if r["version"] else "")
        parts.append(f"\n## {r['title']}\n\n*{when}*\n\n")
        parts.append("".join(f"- {p}\n" for p in r["points"]))
    return "".join(parts)


def mod_version():
    return (ET.parse(ABOUT).getroot().findtext("modVersion") or "").strip()


def main():
    rs = rounds()
    text = render(rs)
    if "--check" in sys.argv[1:]:
        ok = True
        current = open(OUT, encoding="utf-8").read() if os.path.exists(OUT) else ""
        if current != text:
            print("CHANGELOG.md is out of date: run python3 Tools/changelog.py")
            ok = False
        if not rs or rs[0]["version"] != mod_version():
            print(f"the newest round's version ({rs[0]['version'] if rs else 'none'}) isn't About.xml's modVersion ({mod_version()})")
            ok = False
        print("CHANGELOG.md matches the What's new page." if ok else "")
        return 0 if ok else 1
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(f"wrote CHANGELOG.md: {len(rs)} rounds, {sum(len(r['points']) for r in rs)} points")
    return 0


if __name__ == "__main__":
    sys.exit(main())
