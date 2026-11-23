"""Tests for the creature catalogue and lists (creature_census.py, plan_creatures.py). Run: python3 test_creatures.py"""
import collections
import csv
import os
import shutil
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import creature_census as CC  # noqa: E402
import plan_creatures as PC  # noqa: E402


def read(path):
    with open(path, encoding='utf-8', newline='') as f:
        return list(csv.DictReader(f))


class Creatures(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='creature-test-')
        self.batches = os.path.join(self.dir, 'batches')
        os.makedirs(self.batches)

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def test_catalogue_is_the_players_enabled_creatures(self):
        # Every creature of the player's enabled mods (the active rows that aren't humanlike), each once, nothing else.
        with open(CC.RACES, encoding='utf-8', newline='') as f:
            races = list(csv.DictReader(f, delimiter='\t'))
        active = {r['defName'] for r in races if r['active'] == 'yes' and r['kind'] != 'humanlike'}
        rows = read(CC.CATALOGUE)
        self.assertEqual([c for c in CC.COLUMNS], list(rows[0].keys()))
        self.assertEqual({r['creature'] for r in rows}, active)
        self.assertEqual(len(rows), len(active))
        for r in rows:
            self.assertIn(r['group'], CC.GROUPS, r['creature'])
            self.assertIn(r['wave'], ('1', '2', '3', '4', '5'), r['creature'])
            self.assertEqual(r['wave'], str(CC.wave_of(r)), r['creature'])
            self.assertTrue(set(r['stages'].split(';')) <= {'baby', 'adult'}, r['creature'])
            self.assertIn(r['sexes'], ('any', 'male;female'), r['creature'])
            if r['sexes'] == 'male;female':
                self.assertTrue(r['maleLook'] and r['femaleLook'], r['creature'])

    def test_waves_and_insects(self):
        rows = {r['creature']: r for r in read(CC.CATALOGUE)}
        # the save's creatures first, then the base game's and the DLCs', with the insects (the player: "also include
        # insects of course"), every one of those with a look
        self.assertTrue(all(r['look'] for r in rows.values() if r['wave'] in ('1', '2')))
        insects = [r for r in rows.values() if r['group'] == 'insect']
        self.assertGreaterEqual(len(insects), 30)
        self.assertTrue(all(r['wave'] in ('1', '2') for r in insects if r['mod'].startswith('ludeon') or r['mod'] in CC.INSECT_MODS))
        for name in ('Megascarab', 'Spelopede', 'Megaspider', 'VFEI2_Queen', 'VFEI2_Megawasp'):
            self.assertEqual(rows[name]['group'], 'insect', name)
        self.assertEqual(rows['Mech_Scyther']['group'], 'mechanoid')
        self.assertEqual(rows['Muffalo']['stages'], 'baby;adult')
        self.assertEqual(rows['Mech_Scyther']['stages'], 'adult')
        self.assertEqual(rows['Horse']['wave'], '1')       # in the save
        self.assertEqual(rows['PW_Oddish']['wave'], '1')   # a Pokemon in the save
        self.assertTrue(all(r['wave'] == '5' for r in rows.values() if r['group'] == 'pokemon' and r['saveCount'] == '0'))

    def test_census_keeps_hand_columns(self):
        path = os.path.join(self.dir, 'creatures.csv')
        shutil.copy(CC.CATALOGUE, path)
        before = {r['creature']: r for r in read(path)}
        CC.build(CC.RACES, path, None)
        after = {r['creature']: r for r in read(path)}
        for name, r in before.items():
            for c in CC.HAND + ['group', 'saveCount', 'wave']:
                self.assertEqual(r[c], after[name][c], (name, c))

    def test_save_counts(self):
        save = os.path.join(self.dir, 'save.rws')
        with open(save, 'w', encoding='utf-8') as f:
            f.write('<savegame><thing Class="Pawn">\n<def>Muffalo</def></thing><thing Class="Pawn"><def>Human</def></thing>'
                    '<worldPawns><li>\n\t<def>Muffalo</def></li><li><def>Megascarab</def></li></worldPawns></savegame>')
        races = {'Muffalo': {}, 'Megascarab': {}}
        self.assertEqual(CC.save_counts(save, races), collections.Counter({'Muffalo': 2, 'Megascarab': 1}))

    def test_plan(self):
        rows, files, creatures = PC.plan(batches=self.batches)
        self.assertTrue(files and all(n <= 180 for _, n in files))
        written = [r for f in sorted(os.listdir(self.batches)) for r in read(os.path.join(self.batches, f))]
        self.assertEqual(len(written), len(rows))
        self.assertEqual(list(written[0].keys()), PC.COLUMNS)
        cat = {c['creature']: c for c in creatures}
        by = collections.Counter((r['creature'], r['age'], r['sex']) for r in written)
        for name, c in cat.items():
            wave = int(c['wave'])
            adults = sum(v for (k, age, _), v in by.items() if k == name and age == 'adult')
            self.assertEqual(adults, {1: 3, 2: 2}.get(wave, 1) if c['sexes'] == 'any' else 2 * {1: 2, 2: 1}.get(wave, 1), name)
            babies = by[(name, 'baby', 'any')]
            self.assertEqual(babies, 1 if wave <= 2 and c['group'] in ('animal', 'hybrid') and 'baby' in c['stages'] else 0, name)
        # every row one picture, its record words what Update-PortraitLibrary and the game read
        for r in written:
            self.assertEqual(r['count'], '')
            self.assertIn(r['sex'], ('any', 'male', 'female'))
            self.assertIn(r['age'], ('adult', 'baby'))
            self.assertTrue(r['name'].startswith('c_'))
            self.assertLess(len(r['subject']), 500)
            self.assertTrue(r['pose'])
        self.assertEqual(len({r['name'] for r in written}), len(written))
        # the dimorphic ones say their sex's look
        deer = [r for r in written if r['creature'] == 'Deer']
        self.assertTrue(any('stag' in r['subject'] for r in deer if r['sex'] == 'male'))
        self.assertTrue(any('doe' in r['subject'] for r in deer if r['sex'] == 'female'))
        # mixed: the save's creatures first, and every list with many kinds
        first = read(os.path.join(self.batches, 'creatures-01.csv'))
        self.assertTrue(all(cat[r['creature']]['wave'] == '1' for r in first[:100]))
        for f in os.listdir(self.batches):
            part = read(os.path.join(self.batches, f))
            self.assertGreaterEqual(len({r['creature'] for r in part}), 40, f)
        # running again adds nothing
        rows, files, _ = PC.plan(batches=self.batches)
        self.assertEqual((rows, files), ([], []))

    def test_committed_lists(self):
        cat = {c['creature']: c for c in read(PC.CATALOGUE)}
        names = set()
        found = False
        for f in sorted(os.listdir(PC.BATCHES)):
            if not f.startswith('creatures-'):
                continue
            found = True
            for r in read(os.path.join(PC.BATCHES, f)):
                self.assertIn(r['creature'], cat, (f, r['name']))
                self.assertTrue(cat[r['creature']]['look'], r['creature'])
                self.assertNotIn(r['name'], names)
                names.add(r['name'])
        if not found:
            self.skipTest('no creature lists yet')
        # no creature row's name in any other list
        for f in os.listdir(PC.BATCHES):
            if f.endswith('.csv') and not f.startswith('creatures-'):
                self.assertFalse({r.get('name') for r in read(os.path.join(PC.BATCHES, f))} & names, f)


if __name__ == '__main__':
    unittest.main(verbosity=1)
