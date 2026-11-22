"""Checks the portrait catalogue (docs/portrait-art/catalogue) against the player's mods (docs/player-mods).

Every xenotype, race, gene, trait, condition (hediff) and body the catalogue names must be in an active row of
docs/player-mods (the mods the player has enabled), every word must be unique, and every kind must say how it looks.
Run: python3 Tools/PortraitArt/catalogue_check.py   (prints "OK", or the problems and exits with 1)
plan_batches.py runs the same check before it plans.
"""
import csv
import gzip
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
CATALOGUE = os.path.join(ROOT, 'docs', 'portrait-art', 'catalogue')
POOL = os.path.join(ROOT, 'docs', 'player-mods')
sys.path.insert(0, HERE)
import catalogue_words as W  # noqa: E402

GENE_TYPES = {'GeneDef', 'AndroidGeneDef'}
RECORD_FEATURES = {'pointedEars', 'longEars', 'horns', 'tusks', 'fur', 'scales', 'gills', 'glowingEyes', 'markings',
                   'tail', 'snout'}


def split(text):
    return [x.strip() for x in (text or '').split(';') if x.strip()]


def read_csv(path):
    if not os.path.exists(path):
        return []
    with open(path, encoding='utf-8', newline='') as f:
        return list(csv.DictReader(f))


def pool(pool_dir=POOL):
    """The active defs of the player's mods: {type: set of defNames}, and the traits' degree counts."""
    active = {}
    with gzip.open(os.path.join(pool_dir, 'defs.tsv.gz'), 'rt', encoding='utf-8') as f:
        for r in csv.DictReader(f, delimiter='\t'):
            if r['active'] == 'yes':
                active.setdefault(r['type'], set()).add(r['defName'])
    degrees = {}
    with open(os.path.join(pool_dir, 'traits.tsv'), encoding='utf-8') as f:
        for r in csv.DictReader(f, delimiter='\t'):
            if r['active'] == 'yes':
                degrees[r['defName']] = len([d for d in r['degrees'].split(' / ') if d.strip()])
    races = set()
    with open(os.path.join(pool_dir, 'races.tsv'), encoding='utf-8') as f:
        for r in csv.DictReader(f, delimiter='\t'):
            if r['active'] == 'yes' and r['kind'] == 'humanlike':
                races.add(r['defName'])
    return active, degrees, races


def check(catalogue=CATALOGUE, pool_dir=POOL):
    """The problems found, as plain lines (an empty list when all is well)."""
    bad = []
    active, degrees, races = pool(pool_dir)
    genes = set().union(*(active.get(t, set()) for t in GENE_TYPES))
    hediffs = active.get('HediffDef', set())
    bodies = active.get('BodyDef', set())
    xenos = active.get('XenotypeDef', set())

    def gene_ok(where, g):
        if g not in genes:
            bad.append('%s: gene %s is not in an active mod' % (where, g))

    def trait_ok(where, t):
        name, _, deg = t.partition(':')
        if name not in degrees:
            bad.append('%s: trait %s is not in an active mod' % (where, name))
        elif deg and not re.fullmatch(r'-?\d', deg):
            bad.append('%s: trait %s has a degree that is no number' % (where, t))

    kinds = read_csv(os.path.join(catalogue, 'kinds.csv'))
    looks = read_csv(os.path.join(catalogue, 'looks.csv'))
    marks = read_csv(os.path.join(catalogue, 'marks.csv'))
    roles = read_csv(os.path.join(catalogue, 'roles.csv'))
    look_words = {r['word'] for r in looks}
    slugs, names = set(), set()
    for r in kinds:
        k, where = r['kind'], 'kinds.csv ' + r['kind']
        if k in names:
            bad.append(where + ': listed twice')
        names.add(k)
        if not re.fullmatch(r'[a-z0-9]+', r['slug']) or r['slug'] in slugs:
            bad.append(where + ': slug "%s" is not unique small letters and digits' % r['slug'])
        slugs.add(r['slug'])
        if r['race'] == 'Human':
            if k not in xenos:
                bad.append(where + ': no active xenotype of that name')
        elif r['race'] not in races or k != r['race']:
            bad.append(where + ': a race kind must be an active humanlike race, named as its race')
        for g in split(r['genes']) + split(r['ageingGenes']):
            gene_ok(where, g)
        for f in split(r['features']):
            if f not in RECORD_FEATURES:
                bad.append(where + ': feature %s is not a record word' % f)
        for lk in split(r['alwaysLooks']):
            if lk not in look_words:
                bad.append(where + ': always look %s is not in looks.csv' % lk)
        for s in split(r['sexes']):
            if s not in ('male', 'female'):
                bad.append(where + ': sex %s' % s)
        for a, _ in W.parse_shares(r['ages']):
            if a not in W.AGES:
                bad.append(where + ': age %s is not one of the nine' % a)
        for ro, _ in W.parse_shares(r['roles']):
            if ro not in W.ROLES:
                bad.append(where + ': role %s is not one of roles.csv' % ro)
        for e, _ in W.parse_shares(r['eras']):
            if e not in W.ERAS:
                bad.append(where + ': era %s' % e)
        if r['look'] and not r['skins']:
            bad.append(where + ': no skins')
    words = set()
    for r in looks:
        w, where = r['word'], 'looks.csv ' + r['word']
        if not re.fullmatch(r'[a-z][A-Za-z0-9]*', w) or w in words:
            bad.append(where + ': the word must be unique, a letter first, letters and digits')
        words.add(w)
        if r['special'] not in ('yes', 'no'):
            bad.append(where + ': special must be yes or no')
        for t in split(r['traits']):
            trait_ok(where, t)
        for g in split(r['genes']):
            gene_ok(where, g)
        for h in split(r['hediffs']):
            if h not in hediffs:
                bad.append(where + ': hediff %s is not in an active mod' % h)
        for b in split(r['bodies']):
            if b not in bodies:
                bad.append(where + ': body %s is not in an active mod' % b)
        if not (split(r['traits']) or split(r['genes']) or split(r['hediffs']) or split(r['bodies'])):
            bad.append(where + ': nothing the game can find it by (traits, genes, hediffs or bodies)')
        for k in split(r.get('kinds', '')):
            if k not in names:
                bad.append(where + ': kind %s is not in kinds.csv' % k)
        if not r['prompt']:
            bad.append(where + ': no prompt')
    for r in marks:
        for h in split(r['hediffs']):
            if h not in hediffs:
                bad.append('marks.csv %s: hediff %s is not in an active mod' % (r['word'], h))
        if r['word'] in words:
            bad.append('marks.csv %s: also a look word' % r['word'])
    for r in roles:
        if r['role'] not in W.ROLES:
            bad.append('roles.csv %s: not one of the game\'s role words' % r['role'])
    if roles and {r['role'] for r in roles} != set(W.ROLES):
        bad.append('roles.csv: the roles differ from the game\'s words (%s)' % ', '.join(W.ROLES))
    return bad


def main():
    bad = check()
    for b in bad:
        print(b)
    print('OK' if not bad else '%d problems' % len(bad))
    return 1 if bad else 0


if __name__ == '__main__':
    sys.exit(main())
