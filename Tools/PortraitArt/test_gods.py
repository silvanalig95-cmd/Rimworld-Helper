"""Tests for the gods' picture lists (plan_gods.py). Run: python3 test_gods.py"""
import csv
import os
import re
import shutil
import sys
import tempfile
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import plan_gods as G  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(HERE))
# The domains a backdrop of a god's place may name, as the game reads them (UI/Faces/BackdropMath.cs, GodDomains).
DOMAINS = ['harvest', 'storm', 'hunt', 'healing', 'war', 'craft', 'way', 'dead', 'night', 'hearth', 'wild', 'waters', 'machine', 'void']


def read(path):
    with open(path, encoding='utf-8', newline='') as f:
        return list(csv.DictReader(f))


class Gods(unittest.TestCase):
    def test_game_words(self):
        # The game's list of domains and this one agree.
        src = open(os.path.join(ROOT, 'Source', 'ColonyCouncil', 'UI', 'Faces', 'BackdropMath.cs'), encoding='utf-8').read()
        m = re.search(r'GodDomains =\s*\{(.*?)\}', src, re.S)
        self.assertEqual(re.findall(r'"(\w+)"', m.group(1)), DOMAINS)
        self.assertEqual(sorted(G.PLACES), sorted(DOMAINS))
        self.assertEqual(sorted(G.LOOKS), sorted(DOMAINS))

    def test_committed_lists(self):
        backs = read(os.path.join(G.BATCHES, 'backdrops-gods.csv'))
        self.assertEqual(list(backs[0].keys()), G.BACKDROP_COLUMNS)
        self.assertEqual({(r['god'], r['era']) for r in backs}, {(d, e) for d in DOMAINS for e in G.ERAS})
        for r in backs:
            self.assertIn(r['place'], ('room', 'outdoors'))
            self.assertTrue(r['rooms'] if r['place'] == 'room' else r['outdoors'], r['name'])
            self.assertIn('No people', r['subject'])
        gods = read(os.path.join(G.BATCHES, 'gods-01.csv'))
        self.assertEqual(list(gods[0].keys()), G.GOD_COLUMNS)
        keys = {r['creature'] for r in gods}
        for d in DOMAINS:
            for e in G.ERAS:
                self.assertIn('god:%s:%s' % (d, e), keys)
        for k in G.OWN:
            self.assertIn('god:%s:medieval' % k, keys)
        self.assertIn('godbeast:Bellwether', keys)
        self.assertTrue(all(re.match(r'^(god:\w+(:(tribal|medieval|industrial|spacer))?|godbeast:\w+)$', k) for k in keys))
        self.assertEqual(len({r['name'] for r in gods}), len(gods))
        priests = read(os.path.join(G.BATCHES, 'priests-01.csv'))
        self.assertTrue(priests and all(r['role'] == 'priest' for r in priests))
        self.assertEqual({r['era'] for r in priests}, set(G.ERAS))
        # no name in two lists
        names = [r['name'] for r in backs + gods + priests]
        for f in os.listdir(G.BATCHES):
            if f.endswith('.csv') and f not in ('backdrops-gods.csv', 'gods-01.csv', 'priests-01.csv'):
                self.assertFalse({r.get('name') for r in read(os.path.join(G.BATCHES, f))} & set(names), f)

    def test_again_adds_nothing(self):
        d = tempfile.mkdtemp(prefix='gods-test-')
        try:
            for f in os.listdir(G.BATCHES):
                if f.endswith('.csv'):
                    shutil.copy(os.path.join(G.BATCHES, f), d)
            out = G.plan(d)
            self.assertTrue(all(n == 0 for _, n in out), out)
        finally:
            shutil.rmtree(d, ignore_errors=True)


if __name__ == '__main__':
    unittest.main(verbosity=1)
