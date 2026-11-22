"""Tests for the portrait catalogue's tools: the look defs and the batch planner.

Run: python3 Tools/PortraitArt/test_portrait_catalogue.py   ("OK" at the end)
Everything is written into temporary folders; the repository is only read.
"""
import csv
import glob
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
MARKS = {'lostEye', 'eyePatch', 'scar', 'burn', 'bionicEye', 'missingEar'}


def read(path):
    with open(path, encoding='utf-8') as f:
        return f.read()


def rows_of(folder, pattern='*-people-*.csv'):
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
            if re.match(r'(wave\d+|needs)-people-\d+\.csv$', name):
                continue  # start from the hand-made lists only
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

    def test_files_at_most_per_file_and_numbered_on(self):
        self.run_plan(waves=[1], phases=['defaults', 'roles'], max_rows=5000, per_file=50)
        sizes = [len(rows_of(self.batches, os.path.basename(p))) for p in glob.glob(os.path.join(self.batches, 'wave1-people-*.csv'))]
        self.assertTrue(sizes and max(sizes) <= 50)
        n_before = len(sizes)
        self.run_plan(waves=[1], phases=['looks'], max_rows=60, per_file=50)
        after = sorted(glob.glob(os.path.join(self.batches, 'wave1-people-*.csv')))
        self.assertGreater(len(after), n_before)
        self.assertTrue(after[-1].endswith('-%02d.csv' % len(after)))

    def test_variety_rounds(self):
        planner, files, _, _ = self.run_plan(waves=[1], phases=['defaults', 'variety'], max_rows=900, dry=True)
        phases = [p for p, _, _ in planner.rows]
        self.assertIn('variety', phases)
        self.assertEqual(len(planner.rows), 900)
        capped, _, _, _ = self.run_plan(waves=[1], max_rows=300, dry=True)
        self.assertEqual(len(capped.rows), 300)


if __name__ == '__main__':
    unittest.main(verbosity=1)
