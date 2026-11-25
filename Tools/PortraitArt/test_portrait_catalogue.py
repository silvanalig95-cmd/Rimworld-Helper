"""Tests for the portrait catalogue's tools kept in this repository: the look defs (looks_to_defs.py) and the catalogue
itself (catalogue_check.py, the words of catalogue_words.py). The batch lists and their planners are kept in Rimfolk
Studio since 24 November 2026, with their tests (docs/portrait-art/STUDIO-HANDOVER.md, section 0).

Run: python3 Tools/PortraitArt/test_portrait_catalogue.py   ("OK" at the end)
Everything is written into temporary folders; the repository is only read.
"""
import os
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


class Tmp(unittest.TestCase):
    def setUp(self):
        self.dir = tempfile.mkdtemp(prefix='portrait-test-')

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)


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


if __name__ == '__main__':
    unittest.main(verbosity=1)
