"""The portrait catalogue's census: who lives in a save, in the catalogue's words.

Reads a RimWorld save (streaming, so a save of any size works) and counts its humanlike people the way the portrait
catalogue (docs/portrait-art/catalogue) and the batch planner see them: their kind (race and xenotype), sex, how old
they look (the age band, as Rimfolk reads it), role, the age of their people's dress, build, hair, beard, the genes
active on them, their traits and the conditions on their body. Then, with --update, it writes the counts into the
catalogue's tables: kinds.csv (save count, weight and wave, the ages, roles and dress ages seen), looks.csv,
marks.csv and roles.csv (how common), and lists the kinds the save holds that the catalogue has no row for.

A save is someone else's data: it is only read as XML, nothing in it is run. Run it isolated:
    python3 -I Tools/PortraitArt/save_census.py <save.rws> [--catalogue docs/portrait-art/catalogue] [--update]
        [--json census.json]
The weights: a kind's weight blends how many of its people the save holds (on a log scale against the commonest kind)
with how likely the game is to spawn it (the hand-set `spawn` column: its own factions, the factions it joins), 0.7 and
0.3; the wave follows the weight (wave 1 from 0.7, 2 from 0.5, 3 from 0.35, 4 from 0.2, else 5).
"""
import argparse
import collections
import csv
import json
import math
import os
import re
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import catalogue_words as W  # noqa: E402

TICKS_PER_YEAR = 3600000.0

# Parts of the human body (BodyDef Human, the game's own order) that a waist-up portrait shows.
HUMAN_PARTS = {
    12: 'neck', 13: 'head', 14: 'skull', 16: 'eye', 17: 'eye', 18: 'ear', 19: 'ear', 20: 'nose', 21: 'jaw',
    23: 'shoulder', 24: 'clavicle', 25: 'arm', 26: 'humerus', 27: 'radius', 28: 'hand',
    34: 'shoulder', 35: 'clavicle', 36: 'arm', 37: 'humerus', 38: 'radius', 39: 'hand',
}
HEAD_PARTS = {'head', 'skull', 'eye', 'ear', 'nose', 'jaw'}
ARM_PARTS = {'shoulder', 'clavicle', 'arm', 'humerus', 'radius', 'hand'}

# Genes that stop ageing (Rimfolk's reader: PortraitReader.Ageless) and guessed life span factors of genes that lengthen
# a life (the game reads the real stat; these only place the save's people in an age band for the counts).
AGELESS_GENES = {'Ageless', 'DevDesigner_Ageless'}
LIFESPAN_GENES = {'DV_LifespanLong': 2.0, 'AG_ExtraordinaryLifespan': 4.0}

SKIP = {
    'records', 'mindState', 'jobs', 'stances', 'verbTracker', 'natives', 'meleeVerbs', 'pather', 'carryTracker',
    'needs', 'workSettings', 'timetable', 'playerSettings', 'training', 'drugs', 'outfits', 'foodRestriction',
    'inventory', 'filth', 'abilities', 'psychicEntropy', 'thinker', 'interactions', 'relations', 'surgeryBills',
    'immunity', 'rememberedWeapons', 'terrainGrid', 'roofGrid', 'fogGrid', 'snowGrid', 'compressedThingMapDeflate',
    'tales', 'playLog', 'battleLog', 'archive', 'history', 'treeSightings', 'connections', 'learning', 'reading',
    'vehicleTrackers', 'equipment', 'inventoryStock', 'questTags', 'MVCF_VerbManager', 'styleObserver', 'ideo',
}


def _text(e, path, default=''):
    x = e.find(path)
    return x.text.strip() if x is not None and x.text else default


def _kids(e, path):
    x = e.find(path)
    return [] if x is None else list(x)


def read_save(path):
    """The humanlike people of a save (alive and dead), each a dict of what the census reads, and the faction defs."""
    ticks_game = start_abs = None
    factions = {}
    pawns = {}
    stack = []
    for ev, el in ET.iterparse(path, events=('start', 'end')):
        tag = el.tag
        if ev == 'start':
            stack.append(tag)
            continue
        stack.pop()
        if tag == 'ticksGame' and ticks_game is None:
            ticks_game = int(el.text or 0)
        elif tag == 'gameStartAbsTick' and start_abs is None:
            start_abs = int(el.text or 0)
        if tag == 'li' and stack and stack[-1] == 'allFactions':
            lid = _text(el, 'loadID')
            if lid:
                factions['Faction_' + lid] = _text(el, 'def')
            el.clear()
            continue
        if tag in ('thing', 'li') and el.find('kindDef') is not None and el.find('def') is not None:
            story = el.find('story')
            if story is not None and story.get('IsNull') != 'True':
                p = {
                    'id': _text(el, 'id'), 'race': _text(el, 'def'), 'kindDef': _text(el, 'kindDef'),
                    'gender': _text(el, 'gender') or 'Male', 'faction': _text(el, 'faction'),
                    'dead': _text(el, 'healthTracker/healthState') == 'Dead',
                    'bio': int(_text(el, 'ageTracker/ageBiologicalTicks', '0') or 0),
                    'birth': int(_text(el, 'ageTracker/birthAbsTicks', '0') or 0),
                    'body': _text(story, 'bodyType'), 'head': _text(story, 'headType'),
                    'hair': _text(story, 'hairDef'), 'hairColor': _text(story, 'hairColor'),
                    'childhood': _text(story, 'childhood'), 'adulthood': _text(story, 'adulthood'),
                    'beard': _text(el, 'style/beardDef'), 'faceTattoo': _text(el, 'style/faceTattoo'),
                    'xenotype': _text(el, 'genes/xenotype'), 'xenotypeName': _text(el, 'genes/xenotypeName'),
                    'genes': [_text(g, 'def') for g in _kids(el, 'genes/endogenes') + _kids(el, 'genes/xenogenes')
                              if _text(g, 'overriddenByGene') in ('', 'null')],
                    'traits': [], 'hediffs': [],
                    'skills': [(_text(k, 'def'), int(_text(k, 'level', '0') or 0), _text(k, 'passion'))
                               for k in _kids(el, 'skills/skills')],
                }
                for t in _kids(story, 'traits/allTraits'):
                    if _text(t, 'suppressedBy') not in ('', 'null'):
                        continue
                    deg = _text(t, 'degree')
                    p['traits'].append(_text(t, 'def') + (':' + deg if deg and deg != '0' else ''))
                for h in _kids(el, 'healthTracker/hediffSet/hediffs'):
                    perm = False
                    if h.get('Class') == 'Hediff_Injury':
                        perm = any(x.tag == 'isPermanent' and (x.text or '').strip() == 'True' for x in h.iter())
                    part = _text(h, 'part/index')
                    p['hediffs'].append((_text(h, 'def'), int(part) if part.isdigit() else -1, h.get('Class') or '', perm))
                if p['id'] and p['id'] not in pawns:
                    pawns[p['id']] = p
            el.clear()
            continue
        if tag in SKIP:
            el.clear()
    now = (ticks_game or 0) + (start_abs or 0)
    for p in pawns.values():
        p['bioYears'] = p['bio'] / TICKS_PER_YEAR
        p['chronoYears'] = (now - p['birth']) / TICKS_PER_YEAR if p['birth'] else p['bioYears']
        p['factionDef'] = factions.get(p['faction'], '')
    return list(pawns.values()), (ticks_game or 0) // 60000


def age_band(p, life_expectancy=80.0):
    """How old a person looks, as Rimfolk reads it (PortraitLibraryMath.AgeOf), from their years and genes."""
    years = p['bioYears']
    factor = 1.0
    for g in p['genes']:
        factor *= LIFESPAN_GENES.get(g, 1.0)
    ageless = any(g in AGELESS_GENES for g in p['genes'])
    return W.age_of(years, p['chronoYears'], life_expectancy, factor, ageless)


def role_of(p, band):
    """A person's role (roles.csv's words): from their adulthood backstory's words, else their best passionate skill;
    none for a child."""
    if band in ('baby', 'toddler', 'child'):
        return ''
    r = W.role_from_backstory(p['adulthood']) if p['adulthood'] else ''
    if r:
        return r
    best = None
    for name, level, passion in p['skills']:
        if passion in ('', 'None') or passion in W.NEGATIVE_PASSIONS:
            continue
        if best is None or level > best[1]:
            best = (name, level)
    return W.ROLE_BY_SKILL.get(best[0], '') if best else ''


def kind_of(p):
    """The catalogue's kind: a race other than human by its own name, else the xenotype (a player's own by name)."""
    if p['race'] != 'Human':
        return p['race']
    if p['xenotype']:
        return p['xenotype']
    return 'custom:' + (p['xenotypeName'] or '?')


def load_rows(path):
    if not os.path.exists(path):
        return [], []
    with open(path, encoding='utf-8', newline='') as f:
        r = csv.DictReader(f)
        return list(r.fieldnames or []), list(r)


def write_rows(path, fields, rows):
    with open(path, 'w', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=fields, lineterminator='\n')
        w.writeheader()
        for r in rows:
            w.writerow({k: r.get(k, '') for k in fields})


def split_list(text):
    return [x.strip() for x in re.split(r'[;]', text or '') if x.strip()]


def look_hits(p, look, marks_by_part):
    """Whether a person shows a look of looks.csv (a trait with its degree, an active gene, a condition or a body)."""
    traits = set(p['traits'])
    for t in split_list(look.get('traits')):
        if t in traits:
            return True
    genes = set(p['genes'])
    if any(g in genes for g in split_list(look.get('genes'))):
        return True
    hed = {h[0] for h in p['hediffs']}
    if any(h in hed for h in split_list(look.get('hediffs'))):
        return True
    if p['race'] in split_list(look.get('bodies')) or (p['race'] == 'MultiMind_ConjoinedHuman' and 'MultiMind_ConjoinedHumanBody' in split_list(look.get('bodies'))):
        return True
    return False


def marks_of(p):
    """The record marks a person's body shows (as Rimfolk reads them), and the proposed ones (an arm lost, a tattoo)."""
    out = set()
    for d, idx, cls, perm in p['hediffs']:
        part = HUMAN_PARTS.get(idx, '') if p['race'] in ('Human', 'CreepJoiner') else ''
        if d == 'MissingBodyPart':
            if part == 'eye':
                out.add('lostEye')
            elif part == 'ear':
                out.add('missingEar')
            elif part in ('shoulder', 'arm'):
                out.add('missingArm')
        elif part == 'eye' and cls in ('Hediff_AddedPart', 'Hediff_Implant') or d in ('BionicEye', 'ArchotechEye'):
            out.add('bionicEye')
        elif cls == 'Hediff_Injury' and perm and part in HEAD_PARTS:
            out.add('burn' if 'burn' in d.lower() else 'scar')
        if d == 'AM_Eyepatch':
            out.add('eyePatch')
    if p['faceTattoo'] and not p['faceTattoo'].startswith('NoTattoo'):
        out.add('tattoo')
    return out


def census(pawns, catalogue):
    """Every count the catalogue keeps, from every person the save holds, alive or dead (the dead are mostly raiders
    and visitors killed on the map: people the player met)."""
    alive = [p for p in pawns if p['race'] in W.HUMANLIKE_RACES]
    techs = W.faction_tech(os.path.join(catalogue, '..', '..', 'player-mods', 'factions.tsv'))
    _, looks = load_rows(os.path.join(catalogue, 'looks.csv'))
    kinds = collections.defaultdict(lambda: {
        'count': 0, 'sex': collections.Counter(), 'age': collections.Counter(), 'role': collections.Counter(),
        'era': collections.Counter(), 'build': collections.Counter(), 'genes': collections.Counter(),
        'hair': collections.Counter(), 'hairColour': collections.Counter(), 'beard': collections.Counter()})
    roles = collections.Counter()
    looks_n = collections.Counter()
    marks_n = collections.Counter()
    traits_n = collections.Counter()
    for p in alive:
        k = kind_of(p)
        band = age_band(p)
        role = role_of(p, band)
        e = kinds[k]
        e['count'] += 1
        e['sex'][p['gender'].lower()] += 1
        e['age'][band] += 1
        e['era'][W.ERA_BY_TECH.get(techs.get(p['factionDef'], ''), '')] += 1
        e['build'][W.build_of(p['body'])] += 1
        e['hair'][W.hair_style_of(p['hair'])] += 1
        e['hairColour'][W.hair_word(W.rgba(p['hairColor']))] += 1
        e['beard'][W.beard_of(p['beard'])] += 1
        for g in set(p['genes']):
            e['genes'][g] += 1
        if role:
            e['role'][role] += 1
            roles[role] += 1
        for t in p['traits']:
            traits_n[t] += 1
        for lk in looks:
            if look_hits(p, lk, None):
                looks_n[lk['word']] += 1
        for m in marks_of(p):
            marks_n[m] += 1
    out = {'people': len(alive), 'alive': sum(1 for p in alive if not p['dead']), 'dead': sum(1 for p in alive if p['dead']), 'kinds': {}, 'roles': dict(roles),
           'looks': dict(looks_n), 'marks': dict(marks_n), 'traits': dict(traits_n)}
    for k, e in kinds.items():
        out['kinds'][k] = {key: (dict(v) if isinstance(v, collections.Counter) else v) for key, v in e.items()}
    return out


def shares(counter, keys=None, digits=2):
    total = sum(counter.values()) or 1
    items = sorted(counter.items(), key=lambda kv: (-kv[1], kv[0]))
    return ';'.join('%s=%s' % (k, round(v / total, digits)) for k, v in items if k and (keys is None or k in keys))


def update(catalogue, c):
    """Writes the census into the catalogue's tables, keeping every hand-written column."""
    fields, kinds = load_rows(os.path.join(catalogue, 'kinds.csv'))
    top = max([e['count'] for k, e in c['kinds'].items() if not k.startswith('custom:')] + [1])
    known = set()
    for row in kinds:
        known.add(row['kind'])
        e = c['kinds'].get(row['kind'])
        n = e['count'] if e else 0
        row['saveCount'] = str(n)
        spawn = float(row.get('spawn') or 0)
        row['weight'] = '%.2f' % W.weight(n, top, spawn)
        row['wave'] = str(W.wave_of(float(row['weight'])))
        if e and n >= W.TRUST_COUNT:
            row['ages'] = W.blend_ages(e['age'], row.get('ages', ''), n)
            if e.get('role'):
                row['roles'] = shares(collections.Counter(e['role']))
            era = collections.Counter({k: v for k, v in e['era'].items() if k})
            if era:
                row['eras'] = shares(era)
    write_rows(os.path.join(catalogue, 'kinds.csv'), fields, kinds)
    for name, key in (('looks.csv', 'looks'), ('marks.csv', 'marks'), ('roles.csv', 'roles')):
        path = os.path.join(catalogue, name)
        f2, rows = load_rows(path)
        if not rows:
            continue
        id_col = 'word' if name != 'roles.csv' else 'role'
        for row in rows:
            n = c[key].get(row[id_col], 0)
            row['howCommon'] = '%d of %d (%.1f%%)' % (n, c['people'], 100.0 * n / max(1, c['people']))
        write_rows(path, f2, rows)
    missing = sorted(k for k in c['kinds'] if k not in known)
    return missing


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('save')
    ap.add_argument('--catalogue', default=os.path.join(HERE, '..', '..', 'docs', 'portrait-art', 'catalogue'))
    ap.add_argument('--update', action='store_true', help='write the counts into the catalogue tables')
    ap.add_argument('--json', help='also write the whole census as JSON here')
    a = ap.parse_args(argv)
    pawns, day = read_save(a.save)
    c = census(pawns, a.catalogue)
    c['day'] = day
    print('Day %d: %d people (%d alive, %d dead).' % (day, c['people'], c['alive'], c['dead']))
    for k, e in sorted(c['kinds'].items(), key=lambda kv: -kv[1]['count'])[:30]:
        print('  %-36s %4d  %s' % (k, e['count'], shares(collections.Counter(e['age']))))
    if a.json:
        with open(a.json, 'w', encoding='utf-8') as f:
            json.dump(c, f, indent=1, sort_keys=True)
    if a.update:
        missing = update(a.catalogue, c)
        print('Updated the counts in %s.' % os.path.normpath(a.catalogue))
        for k in missing:
            print('  Not in kinds.csv yet (needs a look sheet): %s (%d)' % (k, c['kinds'][k]['count']))
    return 0


if __name__ == '__main__':
    sys.exit(main())
