"""Writes Rimfolk's look defs from the portrait catalogue's looks.csv.

One ColonyCouncil.PortraitLookDef per row of docs/portrait-art/catalogue/looks.csv: its defName is "Look_" and the word
with a capital first letter (ironWilled: Look_IronWilled), then the word, the label, whether it is special (a look only
its own pictures fit, both ways), the traits (defName or defName:degree; without a degree any degree counts), genes,
hediffs and bodies the game finds it by, and the prompt words. The written file is parsed again to be sure it is
well-formed XML. The lead runs it into the mod after a catalogue change:
    python3 Tools/PortraitArt/looks_to_defs.py [--looks docs/portrait-art/catalogue/looks.csv]
        [--out Defs/PortraitLookDefs/PortraitLooks.xml]
"""
import argparse
import csv
import os
import re
import sys
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
LOOKS = os.path.join(ROOT, 'docs', 'portrait-art', 'catalogue', 'looks.csv')
OUT = os.path.join(ROOT, 'Defs', 'PortraitLookDefs', 'PortraitLooks.xml')


def def_name(word):
    return 'Look_' + word[:1].upper() + word[1:]


def split(text):
    return [x.strip() for x in (text or '').split(';') if x.strip()]


def read_looks(path):
    with open(path, encoding='utf-8', newline='') as f:
        return list(csv.DictReader(f))


def to_xml(rows):
    """The defs file's text: a header comment and one def per row, in the CSV's order."""
    names = set()
    out = ['<?xml version="1.0" encoding="utf-8"?>',
           '<!-- Written by Tools/PortraitArt/looks_to_defs.py from docs/portrait-art/catalogue/looks.csv: change the CSV, not this file. -->',
           '<Defs>']
    for r in rows:
        word = (r.get('word') or '').strip()
        if not re.fullmatch(r'[a-z][A-Za-z0-9]*', word):
            raise ValueError('looks.csv: the word "%s" must be letters and digits, starting with a small letter' % word)
        name = def_name(word)
        if name in names:
            raise ValueError('looks.csv: the word "%s" is there twice' % word)
        names.add(name)
        special = (r.get('special') or '').strip().lower() == 'yes'
        out.append('  <ColonyCouncil.PortraitLookDef>')
        out.append('    <defName>%s</defName>' % name)
        out.append('    <word>%s</word>' % escape(word))
        out.append('    <label>%s</label>' % escape((r.get('label') or word).strip()))
        out.append('    <special>%s</special>' % ('true' if special else 'false'))
        for field in ('traits', 'genes', 'hediffs', 'bodies'):
            items = split(r.get(field))
            if items:
                out.append('    <%s>%s</%s>' % (field, ''.join('<li>%s</li>' % escape(i) for i in items), field))
        out.append('    <prompt>%s</prompt>' % escape((r.get('prompt') or '').strip()))
        out.append('  </ColonyCouncil.PortraitLookDef>')
    out.append('</Defs>')
    return '\n'.join(out) + '\n'


def write(looks_path=LOOKS, out_path=OUT):
    rows = read_looks(looks_path)
    text = to_xml(rows)
    ET.fromstring(text.encode('utf-8'))  # well-formed, or this raises
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)
    return len(rows)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--looks', default=LOOKS)
    ap.add_argument('--out', default=OUT)
    a = ap.parse_args(argv)
    n = write(a.looks, a.out)
    print('Wrote %d look defs to %s.' % (n, os.path.relpath(a.out, ROOT) if a.out.startswith(ROOT) else a.out))
    return 0


if __name__ == '__main__':
    sys.exit(main())
