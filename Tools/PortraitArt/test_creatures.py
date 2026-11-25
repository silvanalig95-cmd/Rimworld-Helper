"""Tests for the creature catalogue (creature_census.py). The creature lists and their planner are kept in Rimfolk Studio
since 24 November 2026, with their tests. Run: python3 test_creatures.py"""
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


def read(path):
    with open(path, encoding='utf-8', newline='') as f:
        return list(csv.DictReader(f))


class Creatures(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='creature-test-')

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


if __name__ == '__main__':
    unittest.main(verbosity=1)
