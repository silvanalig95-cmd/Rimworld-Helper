"""
The creature catalogue (docs/portrait-art/catalogue/creatures.csv): every animal, insect, mechanoid, entity and other
creature the player's enabled mods add (docs/player-mods/races.tsv, the active rows that aren't humanlike), with how
often the player's save holds each, its wave and what a picture of it shows.

The player, 23 November 2026: "also please create lists for animals and creatures and implement image display for them
too", and "also include insects of course".

Columns written here (kept up to date on every run): creature (the race's def name), label, mod, group (animal, insect,
mechanoid, drone, entity, dryad, hybrid, pokemon; a group written by hand is kept), size (body size), saveCount, wave.
Columns written by hand and kept: stages (baby;adult, or adult for those that are never seen young), sexes (any, or
male;female where the sexes look different), maleLook and femaleLook (what a male or a female shows then), look (what
the picture shows: the words the prompt takes; empty: not planned yet), notes.

Waves (the order the lists come in): 1 the creatures in the player's save; 2 the base game's and the DLCs', and the
insects of Vanilla Factions Expanded - Insectoids 2; 3 the other animal and creature mods; 4 genetic hybrids, magic
summons and other mods' mechanoids and drones; 5 PokeWorld's Pokemon.

    python3 -I Tools/PortraitArt/creature_census.py [--save <save.rws>]

A save is someone else's data: run with -I, it is only read.
"""
import argparse
import collections
import csv
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
RACES = os.path.join(ROOT, 'docs', 'player-mods', 'races.tsv')
CATALOGUE = os.path.join(ROOT, 'docs', 'portrait-art', 'catalogue', 'creatures.csv')

COLUMNS = ['creature', 'label', 'mod', 'group', 'size', 'saveCount', 'wave', 'stages', 'sexes', 'maleLook', 'femaleLook', 'look', 'notes']
HAND = ['stages', 'sexes', 'maleLook', 'femaleLook', 'look', 'notes']
GROUPS = ['animal', 'insect', 'mechanoid', 'drone', 'entity', 'dryad', 'hybrid', 'pokemon']

# The base game's insects (the rest come from their mod).
INSECTS = {'Megascarab', 'Spelopede', 'Megaspider', 'HiveQueen', 'Larva'}
INSECT_MODS = {'oskarpotocki.vfe.insectoid2'}
MECH_MODS = {'sarg.alphamechs', 'vanillaquestsexpanded.dronefactory', 'turkler.advanced.armaments', 'rimsenal.federation',
             'rimsenal.spacer', 'vanillaexpanded.gravship', 'vanillaexpanded.gravship2', 'vanillaquestsexpanded.ancients',
             'vanillaquestsexpanded.cryptoforge'}
WAVE4_MODS = {'vanillaexpanded.vgeneticse', 'torann.arimworldofmagic'} | MECH_MODS
POKEMON_MODS = {'themoonitself.pokeworld'}


def group_of(defname, mod, label):
    d, l = defname.lower(), (label or '').lower()
    if mod in POKEMON_MODS:
        return 'pokemon'
    if mod == 'vanillaexpanded.vgeneticse':
        return 'hybrid'
    if defname in INSECTS or mod in INSECT_MODS:
        return 'insect'
    if d.startswith('drone_') or 'drone' in d:
        return 'drone'
    if d.startswith('mech_') or '_mech_' in d or 'mech' in l.split() or mod in MECH_MODS:
        return 'mechanoid'
    if 'dryad' in d:
        return 'dryad'
    if mod == 'ludeon.rimworld.anomaly':
        return 'entity'
    return 'animal'


def wave_of(row):
    if int(row.get('saveCount') or 0) > 0:
        return 1
    mod = row['mod']
    if mod.startswith('ludeon.') or mod in INSECT_MODS:
        return 2
    if mod in POKEMON_MODS:
        return 5
    if mod in WAVE4_MODS:
        return 4
    return 3


def read_csv(path, delimiter=','):
    if not os.path.exists(path):
        return []
    with open(path, encoding='utf-8', newline='') as f:
        return list(csv.DictReader(f, delimiter=delimiter))


def save_counts(path, races):
    """How many of each creature the save holds (on its maps and among the world's pawns): every pawn's def that is a
    creature of the table."""
    with open(path, encoding='utf-8', errors='replace') as f:
        text = f.read()
    counts = collections.Counter()
    for m in re.finditer(r'<(?:thing Class="Pawn"|li)>\s*<def>([^<]+)</def>', text):
        if m.group(1) in races:
            counts[m.group(1)] += 1
    return counts


def build(races_path=RACES, catalogue=CATALOGUE, save=None):
    races = collections.OrderedDict()
    for r in read_csv(races_path, '\t'):
        if r.get('active') == 'yes' and r.get('kind') != 'humanlike':
            races[r['defName']] = r
    had = {r['creature']: r for r in read_csv(catalogue)}
    counts = save_counts(save, races) if save else None
    rows = []
    for name, r in races.items():
        old = had.get(name, {})
        row = {c: old.get(c, '') for c in COLUMNS}
        row['creature'] = name
        row['label'] = r.get('label', '')
        row['mod'] = r.get('mod', '')
        if (old.get('group') or '') not in GROUPS:
            row['group'] = group_of(name, row['mod'], row['label'])
        row['size'] = r.get('bodySize', '')
        if counts is not None:
            row['saveCount'] = str(counts.get(name, 0))
        row['saveCount'] = row['saveCount'] or '0'
        row['wave'] = str(wave_of(row))
        if not row['stages']:
            row['stages'] = 'baby;adult' if row['group'] in ('animal', 'hybrid', 'pokemon') else 'adult'
        if not row['sexes']:
            row['sexes'] = 'any'
        rows.append(row)
    rows.sort(key=lambda x: (int(x['wave']), x['mod'], x['creature']))
    gone = sorted(set(had) - set(races))
    with open(catalogue, 'w', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=COLUMNS, lineterminator='\n')
        w.writeheader()
        for row in rows:
            w.writerow(row)
    return rows, gone


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--save', help="the player's save (.rws): the creatures' counts")
    ap.add_argument('--races', default=RACES)
    ap.add_argument('--catalogue', default=CATALOGUE)
    a = ap.parse_args(argv)
    rows, gone = build(a.races, a.catalogue, a.save)
    by = collections.Counter(r['wave'] for r in rows)
    looks = collections.Counter(r['wave'] for r in rows if r['look'])
    print('%d creatures (%s); with a look: %s' % (len(rows), ', '.join('wave %s %d' % (k, by[k]) for k in sorted(by)),
                                                 ', '.join('wave %s %d' % (k, looks[k]) for k in sorted(looks))))
    if gone:
        print('No longer in the player\'s enabled mods (left out): ' + ', '.join(gone))
    return 0


if __name__ == '__main__':
    sys.exit(main())
