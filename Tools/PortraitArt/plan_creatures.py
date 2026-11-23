"""
The creature lists (docs/portrait-art/batches/creatures-NN.csv): rows for the painted pictures of animals, insects,
mechanoids, entities and other creatures, from the creature catalogue (catalogue/creatures.csv, kept by
creature_census.py). The player, 23 November 2026: "also please create lists for animals and creatures and implement
image display for them too", and "also include insects of course".

Each row is one picture of one creature (the style style-rimworld-creatures paints one a row): the whole creature, cut
out, in the game's look. For every creature of the waves asked (by default those with a look) that has a look:
- adults: three in wave 1 (the creatures the player's save holds), two in wave 2 and one from wave 3 on; where the sexes
  look different (sexes male;female), that many of each sex, each with its sex's look;
- a baby for an animal or hybrid of waves 1 and 2 that is born young (stages with baby): the game gives a baby only a
  baby's picture; a juvenile takes an adult's;
- each a little different: its condition (well fed, scruffy, old, battle-worn ...) and its stance, by the row's name.
Rows already in a creatures list are counted, so running again adds only what is missing. The rows are mixed so every
list holds many kinds, wave by wave (the save's creatures first).

Columns: name, subject, pose, count, seed, creature (the race's def name), sex (male, female or any), age (baby or
adult), mod. Update-PortraitLibrary.ps1 writes each picture's record from creature, sex and age.

    python3 Tools/PortraitArt/plan_creatures.py [--waves 1,2] [--per-file 180] [--dry-run]
"""
import argparse
import collections
import csv
import glob
import hashlib
import os
import random
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
CATALOGUE = os.path.join(ROOT, 'docs', 'portrait-art', 'catalogue', 'creatures.csv')
BATCHES = os.path.join(ROOT, 'docs', 'portrait-art', 'batches')
PREFIX = 'creatures'
COLUMNS = ['name', 'subject', 'pose', 'count', 'seed', 'creature', 'sex', 'age', 'mod']

# Adults per creature by wave (per sex where the sexes look different), and the waves whose animals get a baby.
ADULTS = {1: 3, 2: 2}
ADULTS_LATER = 1
ADULTS_PER_SEX = {1: 2, 2: 1}
BABY_WAVES = (1, 2)
BABY_GROUPS = ('animal', 'hybrid')

CONDITION = {
    'animal': ['A healthy, well-fed one with a clean coat.', 'A scruffy one with a weathered, dusty coat.',
               'An older one, a little grey around the face.', 'A young grown one, lean and alert.',
               'A big, heavy one.', 'A smaller, wiry one.', 'One with a few old scars.'],
    'hybrid': ['A healthy, well-fed one.', 'A scruffy one with a weathered coat.', 'A big, heavy one.',
               'A young grown one, lean and alert.'],
    'insect': ['Its chitin glossy and clean.', 'Its chitin scuffed and scarred.', 'A large, heavy one.',
               'A battle-worn one with a chipped carapace.'],
    'mechanoid': ['New, its plates clean and bright.', 'Battered, its plates scorched and dented.',
                  'Its plates scratched and dusty from long use.'],
    'drone': ['New, its metal clean and bright.', 'Battered and scorched.', 'Scratched and dusty from long use.'],
    'entity': ['Glistening, as if wet.', 'Its flesh dark and bruised-looking.', 'Raw and fresh.'],
    'dryad': ['Its leaves fresh and green.', 'Its leaves turning autumn gold.', 'A sturdy, well-grown one.'],
    'pokemon': ['A cheerful one.', 'A wary, watchful one.', 'A calm one.'],
}
STANCE = {
    'walker': ['Standing side-on, its head turned toward the viewer.', 'Walking forward at a calm pace.',
               'Standing still and alert, its head raised.', 'Its head lowered, sniffing the ground.',
               'Standing square, turned three-quarters toward the viewer.'],
    'swimmer': ['Turned three-quarters toward the viewer.', 'Its whole body in view, turned a little toward the viewer.'],
    'machine': ['Standing ready, turned three-quarters toward the viewer.', 'Striding forward.',
                'Standing still, its sensor glowing.'],
    'crawler': ['Crawling forward.', 'Standing still, its mandibles open.', 'Turned three-quarters toward the viewer.'],
    'thing': ['Turned three-quarters toward the viewer.', 'Its whole body in view.'],
}
SWIMMERS = re.compile(r'\b(snake|cobra|fish|eel|snail|turtle|seal|walrus|sea lion|seahorse|kelp|floating|sphere|hovering|flying)\b', re.I)


def read_csv(path):
    if not os.path.exists(path):
        return []
    with open(path, encoding='utf-8', newline='') as f:
        return list(csv.DictReader(f))


def seeded(*parts):
    return random.Random(int(hashlib.sha256('|'.join(parts).encode('utf-8')).hexdigest()[:16], 16))


def slug(name):
    return re.sub(r'[^a-z0-9]+', '', name.lower())


def stance_kind(c):
    g, look = c['group'], c['look']
    if g in ('mechanoid', 'drone'):
        return 'machine'
    if g == 'insect':
        return 'crawler'
    if g == 'entity':
        return 'thing'
    return 'swimmer' if SWIMMERS.search(look) else 'walker'


def wanted(c):
    """The rows a creature wants: (age, sex) for each picture."""
    wave = int(c['wave'] or 5)
    sexes = [s for s in (c.get('sexes') or 'any').split(';') if s in ('male', 'female')]
    out = []
    if len(sexes) == 2:
        n = ADULTS_PER_SEX.get(wave, ADULTS_LATER)
        for s in sexes:
            out += [('adult', s)] * n
    else:
        out += [('adult', 'any')] * ADULTS.get(wave, ADULTS_LATER)
    if wave in BABY_WAVES and c['group'] in BABY_GROUPS and 'baby' in (c.get('stages') or '').split(';'):
        out.append(('baby', 'any'))
    return out


def sentence(text):
    text = text.strip()
    return text[0].upper() + text[1:] + ('' if text.endswith('.') else '.')


def make_row(c, age, sex, n):
    name = 'c_%s_%s_%s_%02d' % (slug(c['creature']), age, {'male': 'm', 'female': 'f'}.get(sex, 'a'), n)
    rng = seeded('creature', name)
    look = c['look'].strip()
    parts = []
    if age == 'baby':
        # "a hare: a lean brown hare ..." -> "A newborn baby hare, ... Its kind: a lean brown hare ..."
        kind, _, what = look.partition(':')
        kind = re.sub(r'^(a|an|the)\s+', '', kind.strip(), flags=re.I)
        parts.append('A newborn baby %s, small and young, with a big head, big eyes, short legs and soft baby fur, down or skin.' % kind)
        parts.append(sentence('Its kind when grown: ' + (what.strip() or look)))
        parts.append('This one is a tiny baby, in the same colours as a grown one.')
    else:
        parts.append(sentence(look))
        own = (c.get('maleLook') if sex == 'male' else c.get('femaleLook') if sex == 'female' else '') or ''
        if own:
            parts.append(sentence('This one is ' + own))
        pool = CONDITION.get(c['group'], CONDITION['animal'])
        parts.append(pool[rng.randrange(len(pool))])
    stances = STANCE[stance_kind(c)]
    return {
        'name': name, 'subject': ' '.join(parts), 'pose': stances[rng.randrange(len(stances))], 'count': '', 'seed': '',
        'creature': c['creature'], 'sex': sex, 'age': age, 'mod': c['mod'],
    }


def listed(folder):
    """The rows already in creature lists: their names, and how many each (creature, age, sex) has."""
    names, have = set(), collections.Counter()
    for path in glob.glob(os.path.join(folder, PREFIX + '-*.csv')):
        for r in read_csv(path):
            names.add(r.get('name', ''))
            have[(r.get('creature', ''), r.get('age', ''), r.get('sex', ''))] += 1
    return names, have


def next_number(folder):
    n = 0
    for p in glob.glob(os.path.join(folder, PREFIX + '-*.csv')):
        m = re.search(r'-(\d+)\.csv$', p)
        if m:
            n = max(n, int(m.group(1)))
    return n + 1


def mix(rows):
    """Wave by wave, the creatures take turns (in an order of their own, the same every time), so a list holds many kinds."""
    out = []
    by_wave = collections.OrderedDict()
    for wave, row in sorted(rows, key=lambda x: x[0]):
        by_wave.setdefault(wave, collections.OrderedDict()).setdefault(row['creature'], []).append(row)
    for wave, kinds in by_wave.items():
        order = sorted(kinds, key=lambda k: hashlib.sha256(('mix|' + k).encode('utf-8')).hexdigest())
        queues = [kinds[k] for k in order]
        while any(queues):
            for q in queues:
                if q:
                    out.append(q.pop(0))
    return out


def plan(catalogue=CATALOGUE, batches=BATCHES, waves=None, per_file=180, dry=False):
    creatures = [c for c in read_csv(catalogue) if (c.get('look') or '').strip()]
    if waves:
        creatures = [c for c in creatures if int(c['wave'] or 5) in waves]
    names, have = listed(batches)
    rows = []
    for c in creatures:
        seen = collections.Counter()
        for age, sex in wanted(c):
            seen[(age, sex)] += 1
            n = seen[(age, sex)]
            if n <= have[(c['creature'], age, sex)]:
                continue
            row = make_row(c, age, sex, n)
            if row['name'] in names:
                continue
            rows.append((int(c['wave'] or 5), row))
    rows = mix(rows)
    files = []
    if rows:
        lists = -(-len(rows) // per_file)
        size = -(-len(rows) // lists)
        n = next_number(batches)
        for i in range(0, len(rows), size):
            chunk = rows[i:i + size]
            path = os.path.join(batches, '%s-%02d.csv' % (PREFIX, n))
            n += 1
            if not dry:
                with open(path, 'w', encoding='utf-8', newline='') as f:
                    w = csv.DictWriter(f, fieldnames=COLUMNS, lineterminator='\n')
                    w.writeheader()
                    for r in chunk:
                        w.writerow(r)
            files.append((path, len(chunk)))
    return rows, files, creatures


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--catalogue', default=CATALOGUE)
    ap.add_argument('--batches', default=BATCHES)
    ap.add_argument('--waves', help='the waves to plan, e.g. 1,2 (default: every creature with a look)')
    ap.add_argument('--per-file', type=int, default=180)
    ap.add_argument('--dry-run', action='store_true')
    a = ap.parse_args(argv)
    waves = [int(x) for x in a.waves.split(',')] if a.waves else None
    rows, files, creatures = plan(a.catalogue, a.batches, waves, a.per_file, a.dry_run)
    groups = collections.Counter(c['group'] for c in creatures)
    print('Planned %d rows for %d creatures (%s).' % (len(rows), len({r['creature'] for r in rows}),
                                                     ', '.join('%s %d' % (g, groups[g]) for g in sorted(groups))))
    for path, n in files:
        print('  %s %s: %d rows' % ('Would write' if a.dry_run else 'Wrote', os.path.relpath(path, ROOT), n))
    return 0


if __name__ == '__main__':
    sys.exit(main())
