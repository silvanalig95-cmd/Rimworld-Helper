"""Tests for the portrait catalogue's tools: the look defs and the batch planner.

Run: python3 Tools/PortraitArt/test_portrait_catalogue.py   ("OK" at the end)
Everything is written into temporary folders; the repository is only read.
"""
import collections
import csv
import glob
import itertools
import os
import re
import shutil
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import catalogue_check  # noqa: E402
import catalogue_words as W  # noqa: E402
import character_words as CW  # noqa: E402
import looks_to_defs  # noqa: E402
import plan_batches as P  # noqa: E402

RECORD_WORDS = {
    'hair': {'bald', 'short', 'medium', 'long', 'braids', 'bun', 'tied'},
    'beard': {'none', 'stubble', 'short', 'full', 'moustache'},
    'build': {'average', 'slim', 'heavy', 'broad'},
    'era': {'tribal', 'medieval', 'industrial', 'spacer'},
    'dress': {'rags', 'work', 'fine', 'robes', 'armour'},
    'facing': {'front', 'left', 'right', 'threeQuarterLeft', 'threeQuarterRight'},
    'sex': {'male', 'female'},
}
FEATURES = {'pointedEars', 'longEars', 'horns', 'tusks', 'fur', 'scales', 'gills', 'glowingEyes', 'markings', 'tail', 'snout'}
MARKS = {'lostEye', 'eyePatch', 'scar', 'burn', 'bionicEye', 'missingEar', 'artificialArm'}


def read(path):
    with open(path, encoding='utf-8') as f:
        return f.read()


def rows_of(folder, pattern='*people-*.csv'):
    out = []
    for path in sorted(glob.glob(os.path.join(folder, pattern))):
        with open(path, encoding='utf-8', newline='') as f:
            out += [(os.path.basename(path), r) for r in csv.DictReader(f)]
    return out


class Tmp(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='portrait-test-')
        self.batches = os.path.join(self.dir, 'batches')
        os.makedirs(self.batches)
        for p in glob.glob(os.path.join(P.BATCHES, '*.csv')):
            name = os.path.basename(p)
            if re.match(r'(((wave\d+|needs)-)?(people|characters)-\d+|[a-z]+-test)\.csv$', name):
                continue  # start from the hand-made lists only (the age and style tests re-run other lists' rows)
            shutil.copy(p, self.batches)

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def run_plan(self, batches=None, **kw):
        kw.setdefault('report', os.path.join(self.dir, 'COVERAGE.md'))
        return P.plan(batches=batches or self.batches, **kw)


class LookDefs(Tmp):
    def test_well_formed_and_one_def_per_row(self):
        out = os.path.join(self.dir, 'PortraitLooks.xml')
        n = looks_to_defs.write(out_path=out)
        root = ET.parse(out).getroot()
        defs = root.findall('ColonyCouncil.PortraitLookDef')
        self.assertEqual(n, len(defs))
        names = [d.findtext('defName') for d in defs]
        self.assertEqual(len(names), len(set(names)))
        for d in defs:
            self.assertRegex(d.findtext('defName'), r'^Look_[A-Z][A-Za-z0-9]*$')
            self.assertIn(d.findtext('special'), ('true', 'false'))
            self.assertTrue(d.findtext('prompt'))
            self.assertTrue(any(d.find(f) is not None for f in ('traits', 'genes', 'hediffs', 'bodies')))

    def test_escapes(self):
        text = looks_to_defs.to_xml([{'word': 'test', 'label': 'a <b> & "c"', 'special': 'yes', 'traits': 'A:2;B',
                                      'genes': '', 'hediffs': '', 'bodies': '', 'prompt': 'x < y & z'}])
        root = ET.fromstring(text.encode('utf-8'))
        d = root.find('ColonyCouncil.PortraitLookDef')
        self.assertEqual(d.findtext('label'), 'a <b> & "c"')
        self.assertEqual([li.text for li in d.find('traits')], ['A:2', 'B'])
        self.assertEqual(d.findtext('defName'), 'Look_Test')

    def test_bad_word_refused(self):
        with self.assertRaises(ValueError):
            looks_to_defs.to_xml([{'word': 'Bad word', 'label': '', 'special': 'no', 'prompt': 'x'}])


class Catalogue(unittest.TestCase):
    def test_catalogue_is_clean(self):
        self.assertEqual(catalogue_check.check(), [])

    def test_age_bands(self):
        self.assertEqual(W.age_of(0.5, 0.5), 'baby')
        self.assertEqual(W.age_of(2, 2), 'toddler')
        self.assertEqual(W.age_of(15, 15), 'teen')
        self.assertEqual(W.age_of(35, 35), 'adult')
        self.assertEqual(W.age_of(35, 35, factor=2.0), 'young')
        self.assertEqual(W.age_of(25, 500, ageless=True), 'middle')
        self.assertEqual(W.age_of(25, 30, ageless=True), 'young')

    def test_roles_from_backstories(self):
        self.assertEqual(W.role_from_backstory('MilitaryChaplain10'), 'priest')
        self.assertEqual(W.role_from_backstory('GlitterworldSurgeon15'), 'doctor')
        self.assertEqual(W.role_from_backstory('ReconSniper89'), 'soldier')
        self.assertEqual(W.role_from_backstory('Herder33'), 'herder')


class Planner(Tmp):
    def test_rows_valid_unique_and_named_by_what_they_show(self):
        planner, files, _, bad = self.run_plan(waves=[1], phases=['defaults', 'roles', 'looks'], max_rows=5000)
        self.assertEqual(bad, [])
        rows = rows_of(self.batches)
        self.assertTrue(rows)
        kinds, looks, marks, roles = P.load(P.CATALOGUE)
        names = [r['name'] for _, r in rows]
        self.assertEqual(len(names), len(set(names)))
        for f, r in rows:
            self.assertEqual(list(r.keys()), P.COLUMNS)
            self.assertRegex(r['name'], r'^p_[a-z0-9]+_[mf]_[a-z]+_[a-z0-9]+_[a-z0-9]+_[a-z0-9]+_\d{2,}$')
            self.assertNotIn('_style', r['name'])
            self.assertIn(r['age'], W.AGES)
            for col, words in RECORD_WORDS.items():
                self.assertIn(r[col], words, '%s %s=%s' % (r['name'], col, r[col]))
            self.assertTrue(set(P.split(r['features'])) <= FEATURES)
            self.assertTrue(set(P.split(r['marks'])) <= MARKS)
            self.assertTrue(set(P.split(r['looks'])) <= set(looks))
            self.assertTrue(r['role'] in roles or r['role'] == '')
            self.assertRegex(r['skin'], r'^#[0-9a-f]{6}$')
            if r['hair'] != 'bald':
                self.assertRegex(r['hairColour'], r'^#[0-9a-f]{6}$')
            sex, age, tag = r['name'].split('_')[2:5]
            self.assertEqual(sex, r['sex'][0])
            self.assertEqual(age, r['age'])
            own = [lk for lk in P.split(r['looks']) if lk not in kinds[r['race'] or r['kind']].always]
            self.assertEqual(tag, r['role'] or (own[0].lower() if own else 'plain'))
            if r['age'] in W.LITTLE:
                self.assertEqual(r['role'], '')
                self.assertEqual(own, [])
            self.assertNotRegex(r['subject'], r'\d+ years old|aged \d+')
            # The age as it looks: a decade (or a child's age in words) at the start, said again at the end.
            self.assertIn('Clearly', r['subject'])
            if r['age'] not in W.LITTLE and r['age'] != 'child':
                self.assertRegex(r['subject'], r'\b(early|mid-|late) ?(teens|twenties|thirties|forties|fifties|sixties|seventies|eighties|nineties)')
            self.assertLessEqual(len(r['subject']), 900)

    def test_defaults_first_then_roles_then_looks(self):
        planner, files, _, _ = self.run_plan(waves=[1], phases=['defaults', 'roles', 'looks'], max_rows=5000)
        order = [p for p, _, _ in planner.rows]
        self.assertEqual(order, sorted(order, key=['needs', 'defaults', 'roles', 'looks', 'variety'].index))
        firsts = {}
        for p, _, r in planner.rows:
            if p == 'defaults':
                firsts.setdefault((r['kind'], r['race'], r['sex'], r['age']), r['name'])
        kinds, *_ = P.load(P.CATALOGUE)
        for k in kinds.values():
            if k.wave == 1 and k.plannable:
                for a, _ in k.ages:
                    for s in k.sexes:
                        self.assertTrue(planner.ledger.has(('d', k.kind, s, a)), (k.kind, s, a))
        defaults = [r for p, _, r in planner.rows if p == 'defaults']
        self.assertTrue(all(not r['role'] and not [lk for lk in P.split(r['looks']) if lk not in kinds[r['race'] or r['kind']].always]
                            for r in defaults))

    def test_same_inputs_same_rows_and_rerun_adds_nothing(self):
        other = os.path.join(self.dir, 'other')
        shutil.copytree(self.batches, other)
        kw = dict(waves=[1, 2], max_rows=100000)
        self.run_plan(**kw)
        self.run_plan(batches=other, report=os.path.join(self.dir, 'C2.md'), **kw)
        a = {f: read(os.path.join(self.batches, f)) for f in os.listdir(self.batches)}
        b = {f: read(os.path.join(other, f)) for f in os.listdir(other)}
        self.assertEqual(a, b)
        planner, files, _, _ = self.run_plan(**kw)
        self.assertEqual(files, [])
        self.assertEqual(planner.rows, [])

    def test_pictures_cover_and_needs_come_first(self):
        planner, files, _, _ = self.run_plan(waves=[1], phases=['defaults'], max_rows=40, dry=True)
        first = planner.rows[0][2]
        # a library holding a picture of that first cell, under some list's name
        rec = os.path.join(self.dir, 'records.xml')
        hair = first['hairColour'] or '#202020'
        with open(rec, 'w', encoding='utf-8') as f:
            f.write('<Defs><ColonyCouncil.PortraitDef><defName>Portrait_x</defName><texPath>old/%s_style_261122-101010_01_cut_00001_</texPath>'
                    '<xenotypes>%s</xenotypes><sex>%s</sex><age>%s</age><skinColor>%s</skinColor><hairColor>%s</hairColor>'
                    '</ColonyCouncil.PortraitDef></Defs>' % (
                        first['name'], '' if first['kind'] == 'Baseliner' else '<li>%s</li>' % first['kind'],
                        first['sex'], first['age'], first['skin'], hair))
        missing = os.path.join(self.dir, 'missing.csv')
        with open(missing, 'w', encoding='utf-8') as f:
            f.write('# a comment\nwhat,kind,mod,sex,age,skin,hair,era,why,used instead,how many,last seen\n'
                    'person,Human/Hussar,ludeon.rimworld.biotech,female,old,dark,grey,spacer,no picture,the game,3,2026-11-22\n'
                    'person,Human/SomeNewKind,some.mod,male,adult,green,black,tribal,no picture,the game,1,2026-11-22\n'
                    'room,Bedroom,,,,,,medieval,no backdrop,,2,2026-11-22\n')
        planner, files, text, _ = self.run_plan(waves=[1], phases=['defaults'], max_rows=40, records=rec, missing=missing, dry=True)
        self.assertEqual(planner.rows[0][0], 'needs')
        self.assertEqual(planner.rows[0][2]['kind'], 'Hussar')
        self.assertEqual(planner.rows[0][2]['age'], 'old')
        self.assertNotIn((first['kind'], first['race'], first['sex'], first['age']),
                         [(r['kind'], r['race'], r['sex'], r['age']) for p, _, r in planner.rows if p == 'defaults'][:1])
        self.assertIn('SomeNewKind', text)
        self.assertTrue(any(f.endswith('needs-people-01.csv') for f, _ in files))

    def test_mixed_kinds_take_turns(self):
        # The player: not "first white boy, white man, white old man ..."; the kinds and ages take turns from the start.
        self.run_plan(waves=[1, 2], phases=['defaults'], max_rows=400, per_file=400)
        rows = [r for _, r in rows_of(self.batches)]
        self.assertTrue(len(rows) >= 60)
        first = rows[:30]
        self.assertGreaterEqual(len({r['kind'] or r['race'] for r in first}), 4)
        self.assertGreaterEqual(len({r['age'] for r in first}), 4)
        self.assertGreaterEqual(len({r['sex'] for r in first}), 2)
        # While every kind still has rows left, no kind runs on for long (at the very end only the commonest are left).
        kinds_of = [r['kind'] or r['race'] for r in rows]
        cut = min(max(i for i, x in enumerate(kinds_of) if x == k) for k in set(kinds_of))  # the first kind to run out
        stretch = kinds_of[:cut + 1]
        run = max(len(list(g)) for _, g in itertools.groupby(stretch))
        self.assertLessEqual(run, 4)

    def test_files_at_most_per_file_and_numbered_on(self):
        self.run_plan(waves=[1], phases=['defaults', 'roles'], max_rows=5000, per_file=50)
        sizes = [len(rows_of(self.batches, os.path.basename(p))) for p in glob.glob(os.path.join(self.batches, 'people-*.csv'))]
        self.assertTrue(sizes and max(sizes) <= 50)
        n_before = len(sizes)
        self.run_plan(waves=[1], phases=['looks'], max_rows=60, per_file=50)
        after = sorted(glob.glob(os.path.join(self.batches, 'people-*.csv')))
        self.assertGreater(len(after), n_before)
        self.assertTrue(after[-1].endswith('-%02d.csv' % len(after)))

    def test_variety_rounds(self):
        planner, files, _, _ = self.run_plan(waves=[1], phases=['defaults', 'variety'], max_rows=900, dry=True)
        phases = [p for p, _, _ in planner.rows]
        self.assertIn('variety', phases)
        self.assertEqual(len(planner.rows), 900)
        capped, _, _, _ = self.run_plan(waves=[1], max_rows=300, dry=True)
        self.assertEqual(len(capped.rows), 300)


# What a plain row must not show (Check-Pictures.ps1's words: weapons, armour, crowns, the tools of a trade, fire or
# magic), and nothing on the head (the pictures are bare-headed).
HELD = re.compile(
    r'\b(swords?|sabres?|blades?|axes?|hatchets?|spears?|pikes?|halberds?|lances?|bows?|crossbows?|arrows?|guns?|rifles?|'
    r'pistols?|revolvers?|muskets?|shotguns?|knife|knives|daggers?|maces?|clubs?|hammers?|flails?|shields?|whips?|slings?|'
    r'grenades?|machetes?|javelins?|armou?r|armoured|chain ?mail|mail shirt|breastplate|gambeson|flak|arm guard|military gear|'
    r'crowns?|tiaras?|circlets?|diadems?|coronets?|books?|tomes?|scrolls?|quills?|pens?|journals?|notebooks?|maps?|ledgers?|'
    r'papers?|slates?|tongs|anvils?|picks?|pickaxes?|saws?|chisels?|trowels?|mallets?|hoes?|rakes?|sickles?|scythes?|'
    r'baskets?|seeds?|pitchforks?|crooks?|staffs?|staves|leash|reins?|ladles?|spoons?|pans?|pots?|cleavers?|bowls?|'
    r'brush|brushes|palettes?|lutes?|flutes?|harps?|drums?|instruments?|scalpels?|bandages?|syringes?|vials?|flasks?|'
    r'potions?|herbs?|needles?|thread|wrench|wrenches|spanners?|tools?|looms?|yarn|shovels?|spades?|buckets?|sacks?|crates?|'
    r'flames?|fire|embers?|sparks?|lightning|glow\w*|runes?|spells?|magic\w*|halo|aura|shimmer\w*|mist|'
    r'hats?|caps?|helmets?|hood up|headscarf|bandana|headband|goggles|turban|beret|bonnet)\b', re.I)
BIG_EYES = re.compile(r'\b(big|large|huge|wide|round|enormous)\b[^.,;]{0,12}\beyes\b|wide-eyed|doe-eyed|bright-eyed', re.I)
DECADE = re.compile(r'\b(early|mid-|late) ?(teens|twenties|thirties|forties|fifties|sixties|seventies|eighties|nineties)')


class Characters(Tmp):
    """The characters lists (plan_batches.py --phases characters): every kind, distinct faces, nothing held without its
    role or look, the eye rule and the age wording, no beauty words."""

    def check_rows(self, rows):
        kinds, looks, marks, roles = P.load(P.CATALOGUE)
        names = [r['name'] for r in rows]
        self.assertEqual(len(names), len(set(names)))
        faces = collections.Counter()
        for r in rows:
            k = kinds[r['race'] or r['kind']]
            subject, pose = r['subject'], r['pose']
            self.assertEqual(list(r.keys()), P.COLUMNS)
            self.assertRegex(r['name'], r'^p_[a-z0-9]+_[mf]_[a-z]+_[a-z0-9]+_[a-z0-9]+_[a-z0-9]+_\d{2,}$')
            for col, words in RECORD_WORDS.items():
                self.assertIn(r[col], words, '%s %s=%s' % (r['name'], col, r[col]))
            self.assertEqual(P.split(r['features']), k.features)        # the picture check reads the body from these
            self.assertTrue(set(P.split(r['marks'])) <= MARKS)
            own = [lk for lk in P.split(r['looks']) if lk not in k.always]
            self.assertEqual(P.split(r['looks'])[:len(k.always)], k.always)
            self.assertEqual(r['name'].split('_')[4], r['role'] or (own[0].lower() if own else 'char'))
            for lk in own:
                self.assertFalse(looks[lk].special)
                self.assertNotIn(lk, P.CHAR_NO_LOOKS)
            self.assertTrue(r['role'] in roles or r['role'] == '')
            if r['age'] in W.LITTLE:
                self.assertEqual((r['role'], own), ('', []))
            self.assertLessEqual(len(subject), P.MAX_SUBJECT)
            # the age as it looks, at the start and again at the end
            self.assertIn('Clearly', subject)
            if r['age'] not in W.LITTLE:
                self.assertRegex(subject, DECADE)
            self.assertNotRegex(subject, r'\d+ years old|aged \d+')
            # no beauty words, small eyes in proportion (the kind's own eyes aside)
            self.assertIsNone(CW.BANNED.search(subject + ' ' + pose), r['name'])
            rest = subject.replace(CW.plain_words(k.look), '')
            if k.sign:
                rest = rest.replace(k.sign, '')
            self.assertIsNone(BIG_EYES.search(rest), r['name'])
            if r['age'] in W.LITTLE:
                self.assertIn('small eyes in proportion to the face', subject)
            elif not k.far:
                self.assertIn('the eyes of ordinary size for the face', subject.lower())
            # a face of its own: its shape or skin, and one to three features, never the same face twice
            fs = P.faces_of(subject)
            self.assertEqual(len(fs), 2 if k.kind == 'MultiMind_ConjoinedHuman' else 1, r['name'])
            for base, feats in fs:
                self.assertTrue(base)
                self.assertTrue(1 <= len(feats) <= 3, (r['name'], feats))
            faces[P.face_key(subject)] += 1
            # a body part the prompt names is one the record has, so the picture check expects it
            said = ' '.join(CW.plain_words(looks[lw].prompt) for lw in P.split(r['looks']) if lw in looks)
            for word, feature in (('horns?', 'horns'), ('tails?', 'tail')):
                if re.search(r'\b%s\b' % word, subject):
                    self.assertTrue(feature in k.features or re.search(r'\b%s\b' % word, said), (r['name'], word))
            if re.search(r'\bwings?\b', subject):
                self.assertTrue(any('wing' in lw.lower() for lw in P.split(r['looks'])), r['name'])
            # a plain row holds and shows nothing a skill or a power would need, and wears nothing on the head
            if not r['role'] and not own:
                self.assertIsNone(HELD.search(pose), (r['name'], pose))
                text = rest
                for lw in k.always:
                    text = text.replace(CW.plain_words(looks[lw].prompt)[1:], '')
                for mw in P.split(r['marks']):        # a recorded mark is the row's own (a bionic eye's lens)
                    text = text.replace(marks[mw]['prompt'][1:], '')
                for e in k.skins.entries + k.hairs.entries:
                    if e[2]:
                        text = text.replace(e[2], '')
                text = text.replace(k.shape, '').replace(k.noun, '')       # a saurid's quills are its hair
                m = HELD.search(text)
                self.assertIsNone(m, (r['name'], m and m.group(0), text))
        twice = [f for f, n in faces.items() if n > 1]
        self.assertEqual(twice, [])
        return kinds

    def check_spread(self, rows, kinds):
        """Every plannable kind, by its weight; many kinds, ages and both sexes from the first rows."""
        by = collections.Counter(r['race'] or r['kind'] for r in rows)
        plain = collections.Counter(r['race'] or r['kind'] for r in rows if '_char_' in r['name'])
        for k in kinds.values():
            if not k.plannable:
                continue
            self.assertIn(k.kind, by, k.kind)
            if k.wave == 5:
                self.assertTrue(2 <= plain[k.kind] <= 4, (k.kind, plain[k.kind]))
            if k.wave == 4:     # the second round adds a young and an adult one of each sex (and skin) where missing
                tones = len(k.skins.entries) if len(k.skins.entries) <= 3 else 1
                self.assertTrue(2 <= plain[k.kind] <= 4 + 4 * tones, (k.kind, plain[k.kind]))
            if k.wave == 1:
                self.assertGreaterEqual(plain[k.kind], 50)
                self.assertGreater(by[k.kind] - plain[k.kind], 0)      # some with a role or a look
            sexes = {r['sex'] for r in rows if (r['race'] or r['kind']) == k.kind}
            self.assertEqual(sexes, set(k.sexes), k.kind)
        first = rows[:40]
        self.assertGreaterEqual(len({r['race'] or r['kind'] for r in first}), 15)
        self.assertGreaterEqual(len({r['age'] for r in first}), 4)
        self.assertEqual({r['sex'] for r in first}, {'male', 'female'})
        self.assertEqual({kinds[r['race'] or r['kind']].wave for r in rows[:100]}, {1, 2, 3, 4, 5})

    def test_characters_phase(self):
        planner, files, text, bad = self.run_plan(phases=['characters'], max_rows=5000)
        self.assertEqual(bad, [])
        self.assertTrue(files and all(os.path.basename(f).startswith('characters-') and n <= 200 for f, n in files))
        self.assertTrue(all(p == P.CHARACTERS for p, _, _ in planner.rows))
        rows = [r for _, r in rows_of(self.batches, 'characters-*.csv')]
        self.assertTrue(1100 <= len(rows) <= 1450, len(rows))
        # the second round covers each baseliner sex, grown age band and skin tone with at least nine pictures, and the
        # other common kinds of human skin in three tones (the game gives a picture to the same tone and the next)
        cover = collections.Counter((r['sex'], r['age'], r['skin']) for r in rows if r['kind'] == 'Baseliner'
                                    and not r['role'] and r['looks'] == '')
        for sex in ('male', 'female'):
            for age in ('young', 'adult', 'middle'):
                for slug, _w, hx in W.HUMAN_SKINS:
                    self.assertGreaterEqual(cover[(sex, age, hx)], 9, (sex, age, slug))
        # baseliners about half, of every list (the player: "our baseline are still kind of a majority ... while still
        # having a total mix overall of all races"), the other kinds through all the lists
        base = sum(1 for r in rows if r['kind'] == 'Baseliner')
        self.assertTrue(0.45 <= base / len(rows) <= 0.55, base)
        for f, n in files:
            part = [r for _, r in rows_of(self.batches, os.path.basename(f))]
            if len(part) >= 100:
                self.assertTrue(0.4 <= sum(1 for r in part if r['kind'] == 'Baseliner') / len(part) <= 0.6, f)
                self.assertGreaterEqual(len({r['kind'] for r in part}), 15, f)
        self.check_spread(rows, self.check_rows(rows))
        self.assertIn('## Characters', text)
        # running again adds nothing, and the other phases don't start the characters lists by themselves
        planner, files, _, _ = self.run_plan(phases=['characters'], max_rows=5000)
        self.assertEqual((files, planner.rows), ([], []))
        planner, files, _, _ = self.run_plan(waves=[1], phases=['defaults'], max_rows=50, dry=True)
        self.assertNotIn(P.CHARACTERS, {p for p, _, _ in planner.rows})

    def test_face_counts_vary(self):
        # The player: "Someone could have one, someone could have three ... no basic faces".
        planner, _, _, _ = self.run_plan(phases=['characters'], max_rows=5000, dry=True)
        n = collections.Counter(len(f) for _, _, r in planner.rows for _, f in P.faces_of(r['subject']))
        self.assertTrue(n[1] and n[2] and n[3], n)
        self.assertEqual(set(n) - {1, 2, 3}, set())
        women = [r for _, _, r in planner.rows if r['sex'] == 'female' and r['age'] in W.GROWN]
        strong = [r for r in women if re.search(r'nose|jaw|chin|brows?\b|teeth|ears', r['subject'].split('Face: ')[-1].split('.')[0])]
        self.assertGreater(len(strong), len(women) // 3)    # women get noses, jaws and brows as often as men

    def test_plain_words(self):
        self.assertEqual(CW.plain_words('a sanguophage: an unnaturally smooth, ageless and attractive face, small sharp fangs'),
                         'a sanguophage: an oddly ageless face, small sharp fangs')
        self.assertIsNone(CW.BANNED.search(CW.plain_words('a beautiful, perfect, elegant woman')))
        self.assertEqual(CW.for_age('an oddly ageless face, small sharp fangs', 'old'), 'small sharp fangs')

    def test_committed_lists(self):
        # The lists in the repository: the same rules, and no characters name in any other list (the age test and the
        # colonists' redo list reuse their own lists' names on purpose).
        rows = [r for _, r in rows_of(P.BATCHES, 'characters-*.csv')]
        if not rows:
            self.skipTest('no characters lists yet')
        self.check_spread(rows, self.check_rows(rows))
        others = {r['name'] for f, r in rows_of(P.BATCHES, '*.csv') if 'sex' in r and not f.startswith('characters-')}
        self.assertEqual({r['name'] for r in rows} & others, set())


if __name__ == '__main__':
    unittest.main(verbosity=1)
