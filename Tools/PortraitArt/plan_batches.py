"""The portrait batch planner: writes the next batch lists from the catalogue and what the library already holds.

Reads docs/portrait-art/catalogue (kinds.csv, looks.csv, marks.csv, roles.csv), every batch list already in
docs/portrait-art/batches (their rows are taken: a row is never written twice and a name never means anything else),
and optionally the library's records.xml (the pictures that exist) and missing.csv (what the game needed and lacked).
Writes the next lists into docs/portrait-art/batches (people-NN.csv, needs-people-NN.csv, mixed: see mix(); at most --per-file rows
each, the columns of colonists.csv plus role) and docs/portrait-art/catalogue/COVERAGE.md.

The order (docs/portrait-art/catalogue/README.md has it in plain words):
  0. needs: missing.csv's people first, a plain picture of each kind, sex, age, skin and hair it lacked;
  1. defaults: for each wave's kinds, every sex and age band they come in, plain pictures (no role, no look) across a
     spread of the skins and hair colours they can have: first one each, then the rest of the spread;
  2. roles: one picture for each common kind, sex, age and role (soldier, scholar ...), dressed and posed for it;
  3. looks: the special looks of a kind (wings, conjoined) are in all its rows; then one picture of each other special
     look (ghoul, undead ...) and each ordinary look (a drinker, a fire mage ...) for the common kinds, by how common;
  4. variety: more of everything where the weight is high, in rounds, until each cell has its share.
And only when asked (--phases characters): the characters lists, characters-NN.csv, distinct people of every kind of
all five waves (see Planner.characters and character_words.py), plain ones named p_..._char_..., and for the common
kinds some with a role or a look.
Coverage counts pictures in the library and rows already listed alike, so running the planner again with the same
inputs adds nothing; with new pictures it leaves out what they cover; a grown catalogue adds rows for what is new.

A row's name says what it shows and never changes: p_<kind>_<m|f>_<age>_<role, look or plain>_<skin>_<hair>_<NN>, NN
the next number free for that start (p_baseliner_f_adult_plain_fair_brown_01). The prompt says how old the person looks
(a decade and its signs, "in his late thirties", said at the start and again at the end), never their years; nothing is held or shown that the row's role and looks don't stand for.

Run:
    python3 Tools/PortraitArt/plan_batches.py [--waves 1,2] [--phases defaults,roles,looks,variety] [--max-rows 600]
        [--records library/records.xml] [--missing library/missing.csv] [--dry-run]
    python3 Tools/PortraitArt/plan_batches.py --phases characters      (the characters lists, all five waves)
"""
import argparse
import collections
import csv
import glob
import hashlib
import math
import os
import random
import re
import sys
import xml.etree.ElementTree as ET

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
sys.path.insert(0, HERE)
import catalogue_words as W  # noqa: E402
import catalogue_check  # noqa: E402
import character_words as C  # noqa: E402

CATALOGUE = os.path.join(ROOT, 'docs', 'portrait-art', 'catalogue')
BATCHES = os.path.join(ROOT, 'docs', 'portrait-art', 'batches')
COLUMNS = ['name', 'subject', 'pose', 'count', 'seed', 'kind', 'race', 'sex', 'age', 'skin', 'hairColour', 'hair', 'beard',
           'build', 'era', 'dress', 'features', 'marks', 'looks', 'role', 'facing', 'pawn']
PHASES = ['defaults', 'roles', 'looks', 'variety']
# The characters lists (--phases characters): distinct people of every kind, plain in the game's sense (see characters()).
CHARACTERS = 'characters'
# Plain character rows per kind by wave; wave 1 by the square root of its count in the player's world (baseliners 156,
# dark elves 73), waves 4 and 5 a few each, the rarest kinds of wave 5 (below CHAR_RARE_WEIGHT) two.
CHAR_PLAIN = {1: 156, 2: 10, 3: 6, 4: 4, 5: 3}
CHAR_RARE_WEIGHT = 0.05
# Rows with a role and rows with a look, written with the same range, for the common kinds (wave 1 scaled like the plain).
CHAR_ROLES_LOOKS = {1: (18, 18), 2: (2, 2)}
# The age bands a kind's characters come in, by wave: babies and toddlers only for wave 1, children up to wave 2, teens
# up to wave 3, grown-ups everywhere.
CHAR_LITTLE = {1: {'baby', 'toddler', 'child', 'teen'}, 2: {'child', 'teen'}, 3: {'teen'}}
# The longest a characters prompt may be (a longer one loses its least needed words; the tests hold every row to it).
MAX_SUBJECT = 1200
# The characters lists' little ones: at this share of their flattened one (1: the player found 3.7% right), and made
# last in a kind's turn (mix).
LITTLE_AGES = ('baby', 'toddler', 'child')
LITTLE_SHARE = 1.0
# A woman's looks in the characters rows, drawn per row (the player, 22 November 2026: "I really don't want every
# woman to be pretty by default", then "I don't want them all to look exactly the same ... There should be some
# attractive ones and there should be not attractive ones"): a share says nothing (FLUX's own, often good-looking),
# the rest plain or plainly not good-looking, in words of their own; no beauty words either way.
WOMAN_LOOKS = [
    (0.30, ['']),
    (0.45, ['A plain, ordinary, everyday face.', 'An ordinary, forgettable face you would pass in a crowd.',
            'A homely, lived-in face.', 'A plain, sensible, unadorned face.', 'A weathered, workaday face.',
            'A face more practical than striking, nothing done to it.', 'An unremarkable face nobody looks at twice.',
            'A tired, ordinary face, no care given to looks.']),
    (0.25, ['A coarse, heavy, rather ugly face.', 'A hard, rough, unappealing face.',
            'An awkward, ungainly face nobody would call good-looking.', 'A blunt, heavy-featured, rather ugly face.',
            'A sour, pinched, unappealing face.', 'A lumpy, blotchy, rather ugly face.']),
]
WOMAN_CLOTHES = 'Her clothes practical and closed at the neck.'
# The baseliners' plain characters rows: about half of all the characters rows (the player: "our baseline are still kind
# of a majority, because ultimately in the game, most of them or a lot of them will be baseliners, while still having a
# total mix overall of all races"). The other wave-1 kinds keep CHAR_PLAIN[1] scaled by their count.
CHAR_BASELINER = 355
# The second round's cover: pictures wanted for each kind, sex, age band and skin (counting the characters rows already
# made), and the share of them that are broad (milder, fewer features) rather than as specific as the first round's.
COVER_BROAD = 0.7
# A non-baseliner kind with human skins is covered in these three tones: the game gives a picture to the same tone and
# the next, so they stand for all seven (veryfair to light, light to tan, tan to dark).
COVER_TONES = ('fair', 'medium', 'brown')


def cover_target(k, age, skin):
    main = skin == k.skins.entries[0][0] or len(k.skins.entries) <= 3
    if k.kind == 'Baseliner':
        return {'young': 9, 'adult': 9, 'middle': 9, 'old': 6, 'elder': 3, 'teen': 3, 'child': 1}.get(age, 0)
    if k.wave == 1:
        return {'young': 3, 'adult': 3, 'middle': 3, 'old': 2, 'elder': 1, 'teen': 1}.get(age, 0)
    if k.wave == 2:
        return {'young': 1, 'adult': 1, 'middle': 1, 'old': 1, 'elder': 1 if main else 0, 'teen': 1 if main else 0}.get(age, 0)
    if k.wave == 3:
        return {'young': 1, 'adult': 1, 'middle': 1, 'old': 1 if main else 0}.get(age, 0)
    if k.wave == 4:
        return {'young': 1, 'adult': 1}.get(age, 0) if main else 0
    return 0
# A kind whose look is on the brow (a neanderthal's brow ridge, a genie's skull, horns, a third eye): no fringe hides it
# (the player: the neanderthal women "really look like normal women", their bobs with heavy fringes over the brow).
OPEN_BROW = re.compile(r'brow ridge|forehead|skull|third eye|ridges|\bhorns\b')
FRINGE = re.compile(r'fringe|bangs', re.I)
HUMAN_SKIN_SLUGS = {s for s, _, _ in W.HUMAN_SKINS}
# Words a hardship line leaves out when the face already says them ("sun-browned skin" and "Sun-browned, dry skin").
ECHO_WORDS = ['sun-browned', 'weather', 'hollow', 'wind', 'creased', 'chapped', 'lines', 'grime', 'stare']
MESSY = re.compile(r'wild|messy|tangled|frizzy|straggly|lank|loose strands|shaggy|untidy')
# A look whose words say the face: its picture shows that, not a mood of its own.
FACE_WORDS = re.compile(r'\b(look|stare|smile|scowl|gaze|grin|smirk|glare|eyes|face|expression|bearing)\b')
ROLE_WEALTH = {'destitute': 'filthy, ragged clothes', 'poor': 'worn, mended clothes', 'common': 'well-used clothes',
               'prosperous': 'clean, well-kept clothes', 'noble': 'rich clothes and a little gold jewellery'}
# Looks the characters lists leave out: beauty is said by no picture of theirs (the player, 22 November 2026: no AI face).
CHAR_NO_LOOKS = {'beautiful', 'pretty'}
# A look that says the body: the build its picture takes where the kind has it, and the hair it keeps.
LOOK_FIT = {'gourmand': {'build': 'heavy'}, 'bigBoned': {'build': 'heavy'}, 'runner': {'build': 'slim'}, 'nimble': {'build': 'slim'},
            'tough': {'build': 'broad'}, 'brawler': {'build': 'broad'}, 'sickly': {'build': 'slim'}, 'gaunt': {'build': 'slim'},
            'ascetic': {'build': 'slim', 'hair': ('short', 'bald')}, 'neat': {'tidy': True}, 'slob': {'beard': 'stubble', 'messy': True}}

# How many plain pictures a kind gets in the end, at weight 1 (a kind's share is this times its weight squared).
VARIETY_TOTAL = 300
# The share of an age band's spread a coverage pass makes, by band (the likeliest bands get the whole spread).
AGE_FACTOR = {'young': 1.0, 'adult': 1.0, 'middle': 1.0, 'old': 0.7, 'teen': 0.7, 'child': 0.7, 'elder': 0.5,
              'toddler': 0.5, 'baby': 0.5}
ROLE_MIN_WEIGHT = 0.5      # kinds this common get a picture per common role
ROLE_MIN_SHARE = 0.05      # a role this common among a kind's people is a common one
ROLE_AGE_SHARE = 0.1       # ages this common among a kind's people get role pictures
LOOK_MIN_WEIGHT = 0.5      # kinds this common get a picture of each ordinary look seen in the save
LOOK_ALL_WEIGHT = 0.9      # kinds this common get a picture of every ordinary look, seen or not
LOOK_SEEN_SHARE = 0.02     # an ordinary look this common counts as seen for the common kinds

# ---------------------------------------------------------------- words for the prompts

SKIN_PHRASE = {'veryfair': 'very fair skin', 'fair': 'fair skin', 'light': 'light skin', 'medium': 'medium-brown skin',
               'tan': 'tan, olive-brown skin', 'brown': 'brown skin', 'dark': 'dark brown skin'}
HAIR_STYLE_PHRASE = {
    'bald': ['a shaved, bald head', 'a bald head'],
    'short': ['short cropped {c} hair', 'short, messy {c} hair', 'short {c} hair swept back', 'a short {c} crop with shaved sides'],
    'medium': ['shoulder-length {c} hair', '{c} hair falling to the jaw', 'wavy {c} hair to the collar'],
    'long': ['long, loose {c} hair', 'long straight {c} hair', 'long wavy {c} hair'],
    'braids': ['{c} hair in long braids', '{c} hair in a single thick braid'],
    'bun': ['{c} hair tied up in a bun', '{c} hair in a knot on top of the head'],
    'tied': ['{c} hair tied back in a ponytail', '{c} hair tied back at the neck'],
}
WOMEN_HAIR = {'medium': ['a {c} bob'], 'short': ['a short {c} pixie cut']}
LITTLE_HAIR = {'baby': [('a soft fuzz of {c} hair', 'short'), ('a few wisps of {c} hair', 'short')],
               'toddler': [('short, fine {c} hair', 'short'), ('soft {c} curls', 'short'), ('tufty {c} hair', 'short')],
               'child': [('tousled {c} hair', 'short'), ('short {c} hair', 'short'), ('{c} hair in two little bunches', 'tied'),
                         ('a messy mop of {c} hair', 'medium'), ('long {c} hair', 'long')]}
NOUN_PHRASE = {
    'dreadlocks': 'thick {c} dreadlocks',
    'fur': 'thick {c} fur', 'quills': 'a crest of {c} quills instead of hair', 'leaves': 'a mane of {c} in place of hair',
    'coral fans': 'soft {c} coral fans', 'fungal crest': 'a {c} fungal crest',
}
BEARD_PHRASE = {'none': 'clean-shaven', 'stubble': 'a few days\' stubble', 'short': 'a short {c} beard',
                'full': 'a full {c} beard', 'moustache': 'a {c} moustache'}
BUILD_PHRASE = {'average': 'of average build', 'slim': 'slim', 'heavy': 'heavy-set', 'broad': 'broad and muscular'}
FACE_SHAPES = ['an oval face', 'a round face', 'a square face', 'a long face', 'a narrow face', 'a heart-shaped face',
               'a diamond-shaped face', 'a broad face', 'a gaunt face']
FACE_PARTS = [
    ['a straight nose', 'a hooked nose', 'a slightly crooked nose', 'a broad nose', 'a flat nose', 'a snub nose',
     'an upturned nose', 'a bulbous nose', 'a long nose'],
    ['deep-set eyes', 'hooded eyes', 'heavy-lidded eyes', 'wide-set eyes', 'close-set eyes', 'narrow eyes', 'small eyes',
     'almond-shaped eyes', 'eyes with dark circles under them', 'eyes crinkled at the corners'],
    ['thick brows', 'sparse brows', 'arched brows', 'straight brows', 'heavy brows', 'brows that meet in the middle'],
    ['thin lips', 'full lips', 'a wide mouth', 'a small mouth', 'a lopsided grin', 'a slight overbite', 'a gap between the front teeth',
     'a weak chin', 'a strong jaw', 'a cleft chin'],
    ['freckles', 'a few moles', 'a faint birthmark on one cheek', 'a dimple in one cheek'],
]
OLDER_FACE = ['deep wrinkles', "crow's feet", 'deep lines across the forehead', 'sagging jowls']
# Face words that read young, left out of the faces of the middle-aged and older.
YOUNG_READING = {'a dimple in one cheek', 'a snub nose', 'an upturned nose'}
# How old a grown-up looks, in the words a painter reads best (the player, 22 November 2026: the ages came out "either
# ... way too old or ... way too young"): a decade, and the signs of that age on the face, said at the start and again at
# the end. Numbers alone were followed loosely, and young-reading words (smooth skin, a dimple) pulled older people young.
AGE_SIGNS = {
    'teen': 'a youthful face still filling out, not yet a grown face',
    'young': 'an adult face, firm skin, no lines yet',
    'adult': 'the first fine lines at the corners of the eyes, a settled adult face',
    'middle': 'lines across the forehead and around the mouth, the skin a little loose at the jaw',
    'old': 'a deeply lined face, loose skin, a few age spots',
    'elder': 'a very wrinkled, sagging face, thin papery skin, a frail look',
}
FAR_AGE_SIGNS = {'middle': 'a worn, older look', 'old': 'an aged look, the skin dull and creased',
                 'elder': 'a very aged, frail look, the skin dull and deeply creased'}


def decade(years):
    """'early twenties', 'mid-thirties', 'late fifties' for a number of years (teens: 'mid-teens', 'late teens')."""
    tens = {1: 'teens', 2: 'twenties', 3: 'thirties', 4: 'forties', 5: 'fifties', 6: 'sixties', 7: 'seventies', 8: 'eighties', 9: 'nineties'}
    t, u = divmod(int(years), 10)
    if t == 1:
        return 'mid-teens' if u <= 15 else 'late teens'
    part = 'early ' if u <= 3 else 'mid-' if u <= 6 else 'late '
    return part + tens.get(t, 'eighties')


NUMBER_WORDS = ['zero', 'one', 'two', 'three', 'four', 'five', 'six', 'seven', 'eight', 'nine', 'ten', 'eleven', 'twelve']
# No big eyes (the player, 22 November 2026: "I don't like it when the people have these huge "pretty face" eyes compared
# to the rest of their body and face ... this eye and face thing is such a classic AI giveaway. it's much more prominent
# on babies, kids and females"): faces are plain and real, the eyes small and in proportion.
CHILD_FACE = ['a round face', 'full cheeks', 'freckles', 'a snub nose', 'small, narrow eyes', 'a gap-toothed smile',
              'rosy cheeks', 'a pointed chin', 'a small upturned nose']
BABY_FACE = ['a round face', 'full cheeks', 'small sleepy eyes', 'rosy cheeks', 'a button nose', 'a tiny mouth']
EXPRESSIONS = ['a calm look', 'a warm look', 'a stern look', 'a weary look', 'a wary look', 'a proud look', 'a cheerful look',
               'a grim look', 'a thoughtful look', 'a determined look', 'a guarded look', 'a curious look', 'a quiet smile',
               'a sceptical look', 'a solemn look', 'a friendly look']
WIDE_EXPRESSIONS = ['an eccentric, lopsided grin', 'a big, toothy, goofy grin', 'an intense, burning stare',
                    'a shy, sidelong glance', 'a wild, staring look', 'a puzzled frown', 'a smug half-smile',
                    'a distant, dreamy look', 'a startled look', 'a crooked, knowing smile', 'a deadpan stare',
                    'a fierce scowl', 'a mournful look', 'a gleeful look', 'a mischievous smirk', 'a bored, half-lidded look']
CHILD_EXPRESSIONS = ['a shy look', 'a big grin', 'a curious look', 'a solemn little frown', 'a cheeky smile', 'a sleepy look',
                     'a frowning, squinting look', 'a pout']
POSES = ['arms crossed', 'arms folded, leaning back slightly', 'hands clasped in front at the waist', 'one hand on the hip',
         'thumbs hooked into the belt', 'standing square, arms at the sides', 'leaning forward a little', 'the shoulders hunched',
         'the chin raised', 'the head tilted to one side', 'one hand resting on the opposite shoulder', 'adjusting the collar',
         'rubbing the back of the neck', 'a hand raised to the chin', 'pulling the coat closer', 'arms loosely folded, one shoulder dropped',
         'turned a little away, looking back over the shoulder', 'hands behind the back']
CHILD_POSES = ['hands clasped behind the back', 'holding a small carved toy', 'fidgeting with a sleeve', 'looking up shyly',
               'arms wrapped around the body', 'one hand waving', 'holding a smooth pebble up to look at it']
TODDLER_POSES = ['clutching a cloth doll', 'a thumb in the mouth', 'pointing at something out of view', 'both hands raised, reaching up',
                 'holding a wooden spoon']
BABY_POSES = ['wrapped in a soft blanket, looking up', 'propped up in a cloth sling, one small hand raised',
              'wrapped in a blanket, a thumb in the mouth', 'reaching a small hand toward the viewer']
FACINGS = [('front', 'Facing front.', 3), ('threeQuarterLeft', 'Facing three-quarter left.', 3),
           ('threeQuarterRight', 'Facing three-quarter right.', 3), ('left', 'Turned a little to the left.', 1),
           ('right', 'Turned a little to the right.', 1)]
WEALTH = {
    'destitute': 'ragged, patched clothing, a gaunt face and grime', 'poor': 'worn, mended clothing',
    'common': 'practical clothing, clean but well used', 'prosperous': 'good, well-fitted clothing, well groomed',
    'noble': 'rich fabrics, a little jewellery and an air of command',
}
HARDSHIP = {'sheltered': 'Smooth skin and rested eyes.', 'ordinary': 'Lightly weathered.',
            'hard': 'Wind-burned, weathered skin and tired eyes.', 'brutal': 'Deep lines and a hard stare.'}
DRESS = {
    'tribal': {'work': ['hide and woven-fibre clothing with bone beads', 'a fur-trimmed leather tunic and a carved pendant'],
               'rags': ['torn, patched hides'], 'fine': ['finely worked furs and rich beadwork'],
               'robes': ['a long woven robe hung with bone charms'], 'armour': ['hide armour with bone plates']},
    'medieval': {'work': ['a wool tunic over a linen shirt', 'a leather jerkin over a linen shirt', 'f:a wool dress with a plain cloak'],
                 'rags': ['a ragged, patched tunic'], 'fine': ['an embroidered doublet and a fur-trimmed cloak', 'f:an embroidered gown and a fine cloak'],
                 'robes': ['a plain hooded robe with the hood down'], 'armour': ['a padded gambeson under a mail shirt']},
    'industrial': {'work': ['a canvas work jacket over a collared shirt', 'a long duster coat and a scarf', 'a buttoned waistcoat with rolled shirt sleeves'],
                   'rags': ['a torn, patched coat'], 'fine': ['a tailored waistcoat under a long coat', 'a well-cut suit jacket'],
                   'robes': ['a long dark coat buttoned to the throat'], 'armour': ['a flak vest over a work shirt']},
    'spacer': {'work': ['a plain synthetic jumpsuit with a high collar', 'a padded flight jacket', 'a clean synthetic jacket with geometric trim'],
               'rags': ['a worn, patched synthetic suit'], 'fine': ['an elegant high-collared coat with fine trim'],
               'robes': ['flowing synthetic robes with soft-glowing seams'], 'armour': ['sleek composite armour with soft-glowing seams']},
}
LITTLE_DRESS = {'baby': ['a soft wool blanket', 'a small knitted blanket'],
                'toddler': ['small, simple clothes', 'a little smock'],
                'child': ['simple, small clothes', 'a little tunic', 'a patched child\'s jacket']}

# ---------------------------------------------------------------- reading


def read_csv(path):
    if not os.path.exists(path):
        return []
    with open(path, encoding='utf-8-sig', newline='') as f:
        return list(csv.DictReader(f))


def split(text):
    return [x.strip() for x in (text or '').split(';') if x.strip()]


def seeded(*parts):
    h = hashlib.sha1('|'.join(parts).encode('utf-8')).hexdigest()
    return random.Random(int(h[:16], 16))


def weighted(rng, items):
    """A pick from [(value, weight)], the same for the same generator."""
    items = [(v, w) for v, w in items if w > 0]
    if not items:
        return None
    total = sum(w for _, w in items)
    x = rng.random() * total
    for v, w in items:
        x -= w
        if x <= 0:
            return v
    return items[-1][0]


class Palette:
    """A kind's skins or hair colours: slug, hex and words, lightest or commonest as listed."""

    def __init__(self, text, human, phrases=None):
        self.entries = []
        phrases = phrases or {}
        for part in split(text):
            if part == 'human':
                for slug, word, hx in human:
                    self.entries.append((slug, hx, phrases.get(slug, word)))
            elif part == 'none':
                self.entries.append(('bald', '', ''))
            elif '=' in part:
                bits = part.split('=', 2)
                self.entries.append((bits[0].strip(), bits[1].strip(), bits[2].strip() if len(bits) > 2 else bits[0]))
            else:
                hit = [h for h in human if h[0] == part]
                if hit:
                    slug, word, hx = hit[0]
                    self.entries.append((slug, hx, phrases.get(slug, word)))
                else:
                    raise ValueError('unknown colour word ' + part)

    def nearest(self, hexcolour):
        if not self.entries:
            return None
        if not hexcolour:
            return next((e for e in self.entries if e[0] == 'bald'), self.entries[0])
        c = W.hex_rgb(hexcolour)
        best = None
        for e in self.entries:
            if not e[1]:
                continue
            d = W.colour_apart(c, W.hex_rgb(e[1]))
            if best is None or d < best[0]:
                best = (d, e)
        return best[1] if best else self.entries[0]


HUMAN_HAIR_TABLE = [(s, w, h) for s, w, h in W.HAIR_COLOURS]
HUMAN_SKIN_TABLE = [(s, w, h) for s, w, h in W.HUMAN_SKINS]
NATURAL_HAIR = {'black', 'darkbrown', 'brown', 'auburn', 'lightbrown', 'blond', 'red', 'sandyblond'}
GREY_HAIR = ['saltpepper', 'grey', 'white']
HAIR_LIGHT_ORDER = ['white', 'blond', 'sandyblond', 'grey', 'red', 'auburn', 'lightbrown', 'saltpepper', 'brown', 'darkbrown',
                    'black', 'inkblack']


class Kind:
    def __init__(self, r):
        self.row = r
        self.kind = r['kind']
        self.slug = r['slug']
        self.race = r['race']
        self.label = r['label']
        self.weight = float(r['weight'] or 0)
        self.wave = int(r['wave'] or 5)
        self.sexes = split(r['sexes']) or ['male', 'female']
        self.ages = [(a, s) for a, s in W.parse_shares(r['ages']) if a in W.AGES]
        self.age_share = dict(self.ages)
        self.skins = Palette(r['skins'], HUMAN_SKIN_TABLE, SKIN_PHRASE)
        self.hairs = Palette(r['hairColours'], HUMAN_HAIR_TABLE)
        self.noun = r['hairNoun'] or 'hair'
        self.hair = W.parse_shares(r['hair']) or [('short', 1.0)]
        beards = split(r['beards'])
        self.women_beards = '+women' in beards
        self.beards = W.parse_shares(';'.join(b for b in beards if b != '+women')) or [('none', 1.0)]
        self.builds = W.parse_shares(r['builds']) or [('average', 1.0)]
        self.eras = W.parse_shares(r['eras']) or [('industrial', 1.0)]
        self.roles = [(ro, s) for ro, s in W.parse_shares(r['roles']) if ro in W.ROLES]
        self.genes = set(split(r['genes']))
        self.always = split(r['alwaysLooks'])
        self.features = split(r['features'])
        self.far = r['farFromHuman'] == 'yes'
        look = r['look'] or ''
        self.has_look = bool(look.strip())
        # The prompt's words for the kind: what follows "a dark elf:" (the opening already names the kind); a look with no
        # name before it ("a person with the head of a fox-like animal ...") whole; nothing for the baseliner.
        self.look = look.split(':', 1)[1].strip() if ':' in look else ('' if self.kind == 'Baseliner' else look.strip())
        self.ageless = r['ageless'] == 'yes'
        # How the head and body differ from a human's, from the genes (the characters lists say it; may be empty).
        self.shape = (r.get('shape') or '').strip()
        # What marks the kind out at a glance, said again at the end of a characters row (may be empty).
        self.sign = (r.get('sign') or '').strip()
        self.greys = ('Hair_Grayless' not in self.genes and not self.ageless
                      and any(e[0] in NATURAL_HAIR for e in self.hairs.entries))

    @property
    def plannable(self):
        return self.has_look and self.weight > 0

    @property
    def xenotype(self):
        return 'Baseliner' if self.race != 'Human' else self.kind

    @property
    def race_column(self):
        return '' if self.race == 'Human' else self.race

    def hair_entries(self, age):
        """The hair colours of an age band: the young ones and grown-ups their own, greying from the middle years."""
        es = [e for e in self.hairs.entries]
        if not self.greys:
            return es
        natural = [e for e in es if e[0] not in GREY_HAIR]
        table = {s: (s, h, w) for s, w, h in HUMAN_HAIR_TABLE}
        greys = [table[s] for s in GREY_HAIR]
        if age in ('old', 'elder'):
            return greys
        if age == 'middle':
            return natural + [table['saltpepper']]
        return natural

    def spread(self, age):
        """(skin, hair) pairs that cover every skin and hair colour of an age band, lightest with lightest."""
        skins = list(self.skins.entries)
        hairs = self.hair_entries(age)
        light = {s: i for i, s in enumerate(HAIR_LIGHT_ORDER)}
        hairs = sorted(hairs, key=lambda e: (light.get(e[0], 50), e[0])) if all(e[0] in light or e[0] in GREY_HAIR for e in hairs) else hairs
        n = max(len(skins), len(hairs), 1)
        pairs = []
        for i in range(n):
            s = skins[min(len(skins) - 1, i * len(skins) // n)] if skins else ('none', '', '')
            h = hairs[min(len(hairs) - 1, i * len(hairs) // n)] if hairs else ('bald', '', '')
            pairs.append((s, h))
        return pairs

    def spread_size(self, age):
        full = len(self.spread(age))
        return max(1, min(full, math.ceil(full * self.weight * AGE_FACTOR.get(age, 0.5))))

    def pick_spread(self, age, n):
        """n pairs spread evenly over the full list, the likeliest (middle of the human skins) first."""
        pairs = self.spread(age)
        if n >= len(pairs):
            order = list(range(len(pairs)))
        elif n == 1:
            order = [len(pairs) // 2 if len(self.skins.entries) > 3 else 0]
        else:
            order = sorted({round(i * (len(pairs) - 1) / (n - 1)) for i in range(n)})
        mid = len(pairs) // 2
        order.sort(key=lambda i: (abs(i - mid) if len(self.skins.entries) > 3 else i, i))
        return [pairs[i] for i in order[:n]]


class Look:
    def __init__(self, r):
        self.word = r['word']
        self.label = r['label']
        self.special = r['special'] == 'yes'
        self.traits = split(r['traits'])
        self.genes = split(r['genes'])
        self.hediffs = split(r['hediffs'])
        self.bodies = split(r['bodies'])
        self.kinds = split(r.get('kinds', ''))
        self.prompt = r['prompt']
        self.pose = r.get('pose', '')
        m = re.match(r'\s*(\d+)', r.get('howCommon') or '')
        self.count = int(m.group(1)) if m else 0
        tot = re.search(r'of (\d+)', r.get('howCommon') or '')
        self.share = self.count / float(tot.group(1)) if tot and int(tot.group(1)) else 0.0

    @property
    def by_genes_only(self):
        return bool(self.genes) and not (self.traits or self.hediffs or self.bodies)


# ---------------------------------------------------------------- what exists


class Ledger:
    """Every row already listed and every picture in the library, as cells the planner counts."""

    def __init__(self, kinds):
        self.kinds = kinds
        self.by_xeno = {k.kind.lower(): k for k in kinds.values() if k.race == 'Human'}
        self.by_race = {k.race.lower(): k for k in kinds.values() if k.race != 'Human'}
        self.names = set()
        self.made = set()
        self.listed = {}          # name -> batch
        self.cells = collections.Counter()
        self.made_cells = collections.Counter()
        self.lists = collections.Counter()
        self.lists_made = collections.Counter()
        self.faces = set()        # the characters rows' faces, so no face is written twice

    def kind_of(self, xenos, races):
        for r in races:
            if r.lower() in self.by_race:
                return self.by_race[r.lower()]
        for x in xenos:
            if x.lower() in self.by_xeno:
                return self.by_xeno[x.lower()]
        if not [x for x in xenos if x.lower() != 'baseliner']:
            return self.by_xeno.get('baseliner')
        return None

    def cells_of(self, k, sex, age, skin_hex, hair_hex, role, looks):
        out = []
        if k is None:
            return out
        own = [lk for lk in looks if lk not in k.always]
        if role:
            out.append(('r', k.kind, sex, age, role))
        for lk in own:
            out.append(('l', k.kind, sex, lk))
            out.append(('la', k.kind, sex, age, lk))
        if not role and not own:
            s = k.skins.nearest(skin_hex)
            h = k.hairs.nearest(hair_hex) if hair_hex or k.hairs.entries else None
            if age in ('old', 'elder', 'middle') and hair_hex and k.greys:
                table = {sl: (sl, hx, w) for sl, w, hx in HUMAN_HAIR_TABLE}
                cand = [e for e in k.hair_entries(age)]
                c = W.hex_rgb(hair_hex)
                h = min(cand, key=lambda e: W.colour_apart(c, W.hex_rgb(e[1])) if e[1] else 999) if cand else h
            out.append(('d', k.kind, sex, age))
            out.append(('ds', k.kind, sex, age, s[0] if s else '', h[0] if h else 'bald'))
        return out

    def add_row(self, batch, r, made=False):
        name = r.get('name', '').strip()
        if not name:
            return
        self.names.add(name)
        self.listed[name] = batch
        self.lists[batch] += 1
        if (r.get('pawn') or '').strip():
            return
        k = self.kind_of([r.get('kind', '')], split(r.get('race', '')))
        for c in self.cells_of(k, r.get('sex', ''), W_age(r.get('age', '')), r.get('skin', ''), r.get('hairColour', ''),
                               (r.get('role') or '').strip(), split(r.get('looks', ''))):
            self.cells[c] += 1
        if batch.startswith(CHARACTERS) and k is not None:
            own = [lk for lk in split(r.get('looks', '')) if lk not in k.always]
            typ = 'r' if (r.get('role') or '').strip() else 'l' if own else 'c'
            self.cells[('char', typ, k.kind, r.get('sex', ''), W_age(r.get('age', '')))] += 1
            if typ == 'c':
                sk = k.skins.nearest(r.get('skin', ''))
                self.cells[('chs', k.kind, r.get('sex', ''), W_age(r.get('age', '')), sk[0] if sk else '')] += 1
            self.faces.add(face_key(r.get('subject', '')))

    def add_picture(self, rec, listed):
        """A picture's cells; one of a row already listed counts once (its row is already counted)."""
        if rec.get('pawn'):
            return
        k = self.kind_of(rec['xenotypes'], rec['races'])
        for c in self.cells_of(k, rec['sex'], rec['age'], rec['skin'], rec['hair'], rec['role'], rec['looks']):
            self.made_cells[c] += 1
            if not listed:
                self.cells[c] += 1

    def has(self, cell):
        return self.cells[cell] > 0


FACE_RE = re.compile(r'(?:^|(?<=\. ))(?:Left |Right )?[Ff]ace: (.+?)\.(?= |$)')


def faces_of(subject):
    """The faces a characters row's prompt describes: [(base, [features])] from its 'Face: base; feature; feature.'"""
    out = []
    for m in FACE_RE.finditer(subject or ''):
        parts = m.group(1).split('; ')
        out.append((parts[0], parts[1:]))
    return out


def face_key(subject):
    return ' | '.join('%s; %s' % (b, '; '.join(sorted(f))) for b, f in faces_of(subject))


def said_in(text, other):
    """Whether a look's words are already in another text (three words in a row of it), so they are said once."""
    words = re.findall(r"[a-z'-]+", text.lower())
    other = ' '.join(re.findall(r"[a-z'-]+", other.lower()))
    return any(' '.join(words[i:i + 3]) in other for i in range(len(words) - 2))


def even(shares):
    """Shares as kinds.csv writes them, a word alone (a genie's build "slim") counting as that kind's only one."""
    return shares if any(s > 0 for _, s in shares) else [(w, 1.0) for w, _ in shares]


def W_age(text):
    t = (text or '').strip().lower().replace('-', '').replace(' ', '')
    return {'teenager': 'teen', 'middleaged': 'middle', 'elderly': 'elder', 'infant': 'baby', 'newborn': 'baby'}.get(t, t)


def read_batches(folder, ledger):
    """Every people list in the batches folder (one with a sex column), its rows taken."""
    for path in sorted(glob.glob(os.path.join(folder, '*.csv'))):
        rows = read_csv(path)
        if not rows or 'sex' not in rows[0]:
            continue
        batch = os.path.splitext(os.path.basename(path))[0]
        for r in rows:
            ledger.add_row(batch, r)


ROW_RE = re.compile(r'^(?P<name>.+?)_(?P<style>style[^_]*)_(?:(?P<run>\d{6}-\d{4,6})_)?(?P<n>\d+)(?P<cut>_cut)?(_\d+_?)?$')


def read_records(path, ledger):
    """The library's records.xml: each picture's cells counted, and its row (by its file's name) marked made."""
    if not path or not os.path.exists(path):
        return 0
    root = ET.parse(path).getroot()
    n = 0
    for e in root:
        if e.tag != 'ColonyCouncil.PortraitDef':
            continue
        n += 1

        def F(f):
            x = e.find(f)
            return (x.text or '').strip() if x is not None and x.text else ''

        def L(f):
            x = e.find(f)
            return [(li.text or '').strip() for li in x.findall('li')] if x is not None else []
        rec = {'xenotypes': L('xenotypes'), 'races': L('races'), 'sex': F('sex').lower(), 'age': W_age(F('age')),
               'skin': hexify(F('skinColor')), 'hair': hexify(F('hairColor')), 'role': F('role'), 'looks': L('looks'),
               'pawn': F('pawn')}
        tex = F('texPath')
        name = None
        if '/' in tex:
            batch, file = tex.split('/', 1)
            base = re.sub(r'\s*\(\d+\)$', '', file)
            base = re.sub(r'\s*-\s*Copy( \(\d+\))?$', '', base)
            m = ROW_RE.match(base)
            if m:
                name = m.group('name')
                if name not in ledger.made:
                    ledger.lists_made[batch] += 1
                ledger.made.add(name)
        ledger.add_picture(rec, name in ledger.names)
    return n


def hexify(text):
    t = (text or '').strip()
    if re.fullmatch(r'#?[0-9a-fA-F]{6}', t):
        return '#' + t.lstrip('#').lower()
    nums = re.findall(r'[\d.]+', t)
    if len(nums) >= 3:
        v = [float(x) for x in nums[:3]]
        scale = 255.0 if any(x > 1 for x in v) else 1.0
        return '#' + ''.join('%02x' % int(round(min(1.0, x / scale) * 255)) for x in v)
    return ''


def read_missing(path):
    """The people lines of missing.csv, commonest first."""
    if not path or not os.path.exists(path):
        return []
    out = []
    with open(path, encoding='utf-8-sig') as f:
        lines = [ln for ln in f.read().splitlines() if ln.strip() and not ln.startswith('#')]
    for r in csv.DictReader(lines):
        if (r.get('what') or '').strip() == 'person':
            try:
                r['_n'] = int(r.get('how many') or 0)
            except ValueError:
                r['_n'] = 0
            out.append(r)
    out.sort(key=lambda r: -r['_n'])
    return out


# ---------------------------------------------------------------- writing rows


SKIN_WORD_HEX = {'very fair': '#f6e4d6', 'fair': '#ecc9ae', 'light': '#d9aa86', 'medium': '#c08c66', 'tan': '#a2704c',
                 'brown': '#7d5236', 'dark': '#4e3322', 'red': '#a3302a', 'orange': '#d9732a', 'yellow': '#e8da86',
                 'green': '#6f9a5b', 'teal': '#4fa39a', 'blue': '#5d7fa8', 'purple': '#7a5a9a', 'pink': '#e0a0b8',
                 'grey': '#8a8d90', 'black': '#1c1c20', 'white': '#efefef'}
HAIR_WORD_HEX = {'black': '#1c1a19', 'brown': '#5a3d27', 'light brown': '#8a6a4a', 'blond': '#cfae74', 'red': '#b23a1a',
                 'grey': '#8e8c88', 'white': '#e2e0dc', 'green': '#6fbf5a', 'teal': '#2fb3a8', 'blue': '#7fb6e6',
                 'purple': '#9a6ad0', 'pink': '#e48ab8', 'orange': '#d97a2b', 'yellow': '#e8d070'}


GROWN_ONLY = re.compile(r'stare|build|frame|beard|powerful neck|\bwoman\b|\bman\b|wrinkled|muscled|regal|bearing|intimidating|fierce|brutish|'
                        r'stocky|powerful|alluring|tall|hard soldier')


def child_look(text):
    """A kind's look for its little ones: the parts that grow with age (a stare, a build, a beard) left out."""
    parts = [p.strip() for p in re.split(r',\s+(?=(?:a|an|the|two|one|small|long|large)\s)|\s+and\s+(?=(?:a|an|the|two|one|small|long|large)\s)', text) if p.strip()]
    kept = [p for p in parts if not GROWN_ONLY.search(p)]
    if not kept:
        return ''
    return ', '.join(kept[:-1]) + (' and ' if len(kept) > 1 else '') + kept[-1]


def age_words(rng, k, sex, age):
    """(person, opening, again): who the picture shows ('middle-aged hussar woman'), the prompt's opening with how old
    they look ('A middle-aged hussar woman in her late forties'), and the closing line that says it again."""
    little = age in W.LITTLE
    his = 'his' if sex == 'male' else 'her'
    about = rng.randint(*[int(x) for x in W.LOOKS_ABOUT[age]]) if age != 'baby' else None
    noun = {'baby': 'baby', 'toddler': 'toddler', 'child': 'boy' if sex == 'male' else 'girl'}.get(age, 'man' if sex == 'male' else 'woman')
    if age == 'teen':
        noun = 'boy' if sex == 'male' else 'girl'
    adj = {'teen': 'teenage', 'young': 'young', 'middle': 'middle-aged', 'old': 'old', 'elder': 'very old'}.get(age, '')
    kind_word = '' if k.kind == 'Baseliner' else k.label
    if k.kind == 'MultiMind_ConjoinedHuman':
        kind_word = 'conjoined twin'
        noun = {'baby': 'babies', 'toddler': 'toddlers', 'child': 'boys' if sex == 'male' else 'girls'}.get(age, 'men' if sex == 'male' else 'women')
        if age == 'teen':
            noun = 'boys' if sex == 'male' else 'girls'
    person = ' '.join(x for x in (adj, kind_word, noun) if x)
    if age == 'baby':
        months = rng.choice(['six', 'eight', 'ten'])
        looks_line = 'about %s months old' % months
        again = 'Clearly a baby of about %s months.' % months
    elif little or age == 'child':
        looks_line = 'about %s years old' % NUMBER_WORDS[min(about, 12)]
        word = NUMBER_WORDS[min(about, 12)]
        again = 'Clearly %s %s-year-old %s.' % ('an' if word[0] in 'aeiou' else 'a', word, 'toddler' if age == 'toddler' else noun)
    else:
        looks_line = 'in %s %s' % (his, decade(about))
        again = 'Clearly in %s %s.' % (his, decade(about))
        if age == 'teen':
            again = 'Clearly a teenager in %s %s, not yet grown.' % (his, decade(about))
    art = 'An' if person[:1].lower() in 'aeiou' else 'A'
    if k.kind == 'MultiMind_ConjoinedHuman':
        art, looks_line = 'Two', looks_line.replace(' his ', ' their ').replace(' her ', ' their ')
        again = again.replace(' his ', ' their ').replace(' her ', ' their ')
    opening = '%s %s %s' % (art, person, looks_line)
    if age == 'young':
        opening += ', clearly a grown adult'
    return person, opening, again


class Full(Exception):
    """The run's rows are all planned (--max-rows)."""


class Planner:
    def __init__(self, kinds, looks, marks, roles, ledger, total_people, cap=10 ** 9):
        self.kinds = kinds
        self.looks = looks
        self.marks = marks
        self.roles = roles
        self.ledger = ledger
        self.total_people = total_people
        self.rows = []            # (phase, wave, row)
        self.cap = cap
        self.taken = set(ledger.names)
        self.stems = collections.Counter()
        for n in self.taken:
            m = re.match(r'^(.*)_(\d+)$', n)
            if m:
                self.stems[m.group(1)] = max(self.stems[m.group(1)], int(m.group(2)))

    def name(self, stem):
        n = self.stems[stem] + 1
        while '%s_%02d' % (stem, n) in self.taken:
            n += 1
        self.stems[stem] = n
        name = '%s_%02d' % (stem, n)
        self.taken.add(name)
        return name

    def make(self, phase, wave, k, sex, age, pair, role='', look=None, marks=(), era=None, wide=False, tag=None):
        """One row: its record columns and its prompt, every choice seeded by the row's name."""
        if len(self.rows) >= self.cap and phase != 'needs':
            raise Full()
        skin, hair = pair
        sexl = 'm' if sex == 'male' else 'f'
        tag = tag or (role or (look.word.lower() if look else 'plain'))
        stem = 'p_%s_%s_%s_%s_%s_%s' % (k.slug, sexl, age, tag, skin[0], hair[0])
        name = self.name(stem)
        rng = seeded(name)
        little = age in W.LITTLE
        looks = list(k.always) + ([look.word] if look else [])
        # hair, beard, build
        hair_word = 'bald' if hair[0] == 'bald' or not hair[1] else (weighted(rng, k.hair) or 'short')
        if age == 'baby' and hair_word != 'bald':
            hair_word = 'short'
        if little or age == 'teen' or (sex == 'female' and not k.women_beards):
            beard = 'none'
        else:
            beard = weighted(rng, k.beards) or 'none'
        build = 'average' if age in ('baby', 'toddler') else (weighted(rng, k.builds) or 'average')
        if age in ('child', 'teen') and build in ('heavy', 'broad'):
            build = weighted(rng, [('average', .6), ('slim', .4)])
        era = era or weighted(rng, k.eras) or 'industrial'
        # role and wealth decide the clothes
        rrow = self.roles.get(role) if role else None
        wealth = weighted(rng, [('destitute', .05), ('poor', .25), ('common', .45), ('prosperous', .17), ('noble', .08)])
        if role in ('leader',):
            wealth = weighted(rng, [('prosperous', .5), ('noble', .5)])
        elif role in ('trader', 'scholar', 'doctor', 'priest'):
            wealth = weighted(rng, [('common', .5), ('prosperous', .4), ('poor', .1)])
        elif role in ('labourer', 'miner', 'farmer', 'herder'):
            wealth = weighted(rng, [('poor', .5), ('common', .45), ('destitute', .05)])
        if wealth == 'noble' and (era == 'tribal' or role not in ('', 'leader', 'trader')):
            wealth = 'prosperous'
        hardship = weighted(rng, [('sheltered', .1), ('ordinary', .45), ('hard', .35), ('brutal', .1)])
        if hardship == 'sheltered' and age not in ('young', 'teen'):
            hardship = 'ordinary'   # smooth skin and rested eyes read young
        if age == 'teen' and hardship in ('hard', 'brutal'):
            hardship = 'ordinary'   # a weathered, tired face reads older
        if role in ('soldier', 'warrior'):
            hardship = weighted(rng, [('ordinary', .3), ('hard', .45), ('brutal', .25)])
        if wealth in ('noble', 'prosperous') and hardship == 'brutal':
            hardship = 'hard'
        if rrow:
            dress = rrow['dress'] or 'work'
        elif wealth == 'destitute':
            dress = 'rags'
        elif wealth == 'noble':
            dress = 'fine'
        else:
            dress = weighted(rng, [('work', .8), ('robes', .1), ('fine', .1 if wealth == 'prosperous' else 0)])
        facing, facing_text = weighted(rng, [((f, t), w) for f, t, w in FACINGS])
        # the words
        he, his = ('he', 'his') if sex == 'male' else ('she', 'her')
        person, opening, again = age_words(rng, k, sex, age)
        parts = []
        if k.look:
            parts.append(child_look(k.look) if little else k.look)
        parts.append(skin[2] if skin[2] else '')
        colour = hair[2] if hair[2] else ''
        if k.noun != 'hair':
            if colour:
                parts.append(NOUN_PHRASE.get(k.noun, '{c} ' + k.noun).format(c=colour))
            if k.noun == 'fur':
                pass
        elif hair_word != 'bald' and little:
            phrase, hair_word = rng.choice(LITTLE_HAIR[age])
            parts.append(phrase.format(c=colour or 'dark'))
        elif hair_word != 'bald':
            choices = HAIR_STYLE_PHRASE[hair_word] + (WOMEN_HAIR.get(hair_word, []) if sex == 'female' else [])
            parts.append(rng.choice(choices).format(c=colour or 'dark'))
        elif hair[0] == 'bald' or not colour:
            pass
        else:
            parts.append(rng.choice(HAIR_STYLE_PHRASE['bald']))
        if beard != 'none':
            parts.append(BEARD_PHRASE[beard].format(c=colour or 'dark'))
        elif sex == 'male' and age in W.GROWN and any(b != 'none' for b, _ in k.beards):
            parts.append('clean-shaven')
        if not little:
            parts.append(BUILD_PHRASE[build])
        body = ', '.join(p for p in parts if p)
        if little:
            faces = rng.sample(BABY_FACE if age == 'baby' else CHILD_FACE, 2)
            face = ' and '.join(faces)
            expr = rng.choice(CHILD_EXPRESSIONS)
            soft = ('A plain, real ' + ('baby' if age == 'baby' else 'toddler' if age == 'toddler' else 'child') +
                    "'s face with small eyes in proportion to the face, an ordinary, homely look.")
        else:
            n_face = 1 if k.far else rng.choice([2, 3])
            face_bits = [rng.choice(FACE_SHAPES)] if not k.far else []
            pool = list(FACE_PARTS) + ([OLDER_FACE] if age in ('middle', 'old', 'elder') else [])
            rng.shuffle(pool)
            for group in pool[:n_face]:
                choices = [g for g in group if age in ('young', 'teen') or g not in YOUNG_READING] or group
                face_bits.append(rng.choice(choices))
            face = ', '.join(face_bits[:-1]) + (' and ' if len(face_bits) > 1 else '') + face_bits[-1]
            exprs = EXPRESSIONS + (WIDE_EXPRESSIONS if (wide or k.far) else [])
            expr = rng.choice(exprs) if not look else ''
            soft = ''
        sentences = [opening + ': ' + body + '.', face[:1].upper() + face[1:] + (', ' + expr if expr else '') + '.']
        if not little and not k.far:
            sentences.append('The eyes of ordinary size for the face.')
        signs = (FAR_AGE_SIGNS if k.far else AGE_SIGNS).get(age, '')
        if signs and not little:
            sentences.append(signs[:1].upper() + signs[1:] + '.')
        if soft:
            sentences.append(soft)
        for lw in looks:
            lk = self.looks.get(lw)
            if lk:
                sentences.append(lk.prompt[:1].upper() + lk.prompt[1:] + '.')
        for mw in marks:
            m = self.marks.get(mw)
            if m:
                sentences.append(m['prompt'][:1].upper() + m['prompt'][1:] + '.')
        if rrow:
            sentences.append('%s: %s.' % (rrow['label'][:1].upper() + rrow['label'][1:], rrow[era]))
        clothed = any(re.search(r'\b(robe|robes|armour|gear|clothes|clothing|cloak|coat|wrap)\b', self.looks[lw].prompt)
                      for lw in looks if lw in self.looks)
        if little:
            sentences.append('%s wears %s.' % (he.capitalize(), rng.choice(LITTLE_DRESS[age])) if age != 'baby'
                             else 'Wrapped in %s.' % rng.choice(LITTLE_DRESS['baby']))
        elif clothed:
            text = ' '.join(self.looks[lw].prompt for lw in looks if lw in self.looks)
            dress = ('robes' if re.search(r'\brobes?\b', text) else 'armour' if re.search(r'\b(armour|gear)\b', text)
                     else 'rags' if re.search(r'stained|rumpled|very little', text) else 'work')
            sentences.append(HARDSHIP[hardship])
        else:
            options = [d[2:] if d.startswith('f:') else d for d in DRESS[era][dress] if sex == 'female' or not d.startswith('f:')]
            clothes = rng.choice(options) if not rrow else ''
            wear = '%s wears %s' % (he.capitalize(), clothes + ', ' if clothes else '') if clothes else '%s wears' % he.capitalize()
            sentences.append('%s %s.' % (wear, 'a gaunt, grimy face' if dress == 'rags' else WEALTH[wealth]))
            sentences.append(HARDSHIP[hardship])
        sentences.append(facing_text)
        sentences.append(again)
        subject = ' '.join(sentences)
        subject = re.sub(r'\s+', ' ', subject).replace(' ,', ',').replace(',.', '.').replace('..', '.')
        # the pose: from the row's look or role, else a varied one that holds nothing a skill would need
        if age == 'baby':
            pose = rng.choice(BABY_POSES)
        elif age == 'toddler':
            pose = rng.choice(TODDLER_POSES)
        elif age == 'child':
            pose = rng.choice(CHILD_POSES)
        elif look and look.pose:
            pose = look.pose
        elif rrow and rrow['pose']:
            pose = rrow['pose']
        else:
            pose = rng.choice(POSES)
        pose = pose[:1].upper() + pose[1:] + '.'
        row = {
            'name': name, 'subject': subject, 'pose': pose, 'count': '', 'seed': '', 'kind': k.xenotype,
            'race': k.race_column, 'sex': sex, 'age': age, 'skin': skin[1], 'hairColour': hair[1],
            'hair': hair_word, 'beard': beard, 'build': build, 'era': era, 'dress': dress if not little else 'work',
            'features': ';'.join(k.features), 'marks': ';'.join(marks), 'looks': ';'.join(looks), 'role': role,
            'facing': facing, 'pawn': '',
        }
        if not row['hairColour'] and hair_word != 'bald':
            row['hairColour'] = '#3b2a20'
        self.rows.append((phase, wave, row))
        for c in self.ledger.cells_of(k, sex, age, skin[1], hair[1], role, looks):
            self.ledger.cells[c] += 1
        return row

    # ------------------------------------------------------------ the phases

    def needs(self, missing):
        """missing.csv's people first: a plain picture of each kind, sex, age, skin and hair the game lacked."""
        unknown = []
        for r in missing:
            kind = (r.get('kind') or '').strip()
            race, _, xeno = kind.partition('/')
            k = None
            if race and race != 'Human':
                k = self.kinds.get(race)
            elif xeno and xeno != 'custom':
                k = self.kinds.get(xeno)
            if k is None or not k.plannable:
                unknown.append((kind, r['_n']))
                continue
            sex = (r.get('sex') or '').strip()
            age = W_age(r.get('age'))
            if sex not in ('male', 'female') or age not in W.AGES:
                continue
            skin = k.skins.nearest(SKIN_WORD_HEX.get((r.get('skin') or '').strip(), ''))
            hw = (r.get('hair') or '').strip()
            hair = ('bald', '', '') if hw == 'bald' and not k.hairs.entries else (k.hairs.nearest(HAIR_WORD_HEX.get(hw, '')) if k.hairs.entries else ('bald', '', ''))
            if hw == 'bald':
                hair = next((e for e in k.hairs.entries if e[0] == 'bald'), hair)
            if self.ledger.has(('ds', k.kind, sex, age, skin[0], hair[0])):
                continue
            era = (r.get('era') or '').strip()
            self.make('needs', 0, k, sex, age, (skin, hair), era=era if era in W.ERAS else None)
        return unknown

    def defaults(self, ks):
        """Plain pictures: one of every kind, sex and age band first, then the rest of each spread."""
        for first in (True, False):
            for k in ks:
                for age, _ in k.ages:
                    for sex in k.sexes:
                        pairs = k.pick_spread(age, k.spread_size(age))
                        todo = pairs[:1] if first else pairs[1:]
                        for pair in todo:
                            if first and self.ledger.has(('d', k.kind, sex, age)):
                                continue
                            if self.ledger.has(('ds', k.kind, sex, age, pair[0][0], pair[1][0])):
                                continue
                            self.make('defaults', k.wave, k, sex, age, pair)

    def role_list(self, k):
        n = max(1, min(len(k.roles), round(k.weight * 12)))
        return [ro for ro, s in k.roles[:n] if s >= ROLE_MIN_SHARE]

    def role_ages(self, k):
        return [a for a, s in k.ages if s >= ROLE_AGE_SHARE and a in W.GROWN]

    def roles_phase(self, ks):
        """One picture of each common kind, sex, age and role."""
        for k in ks:
            if k.weight < ROLE_MIN_WEIGHT:
                continue
            for age in self.role_ages(k):
                pairs = k.spread(age)
                for sex in k.sexes:
                    for i, role in enumerate(self.role_list(k)):
                        if self.ledger.has(('r', k.kind, sex, age, role)):
                            continue
                        pair = pairs[(i * 3 + (1 if sex == 'female' else 0)) % len(pairs)]
                        self.make('roles', k.wave, k, sex, age, pair, role=role)

    def looks_for(self, k):
        """The looks a kind's people can show that its own look sheet doesn't already have, commonest first."""
        out = []
        for lk in sorted(self.looks.values(), key=lambda l: (-l.count, l.word)):
            if lk.word in k.always:
                continue
            if lk.kinds and k.kind not in lk.kinds:
                continue
            if set(lk.genes) & k.genes:
                continue  # the kind's own look has it: every one of them shows it
            if lk.genes and not lk.traits and not lk.kinds:
                continue  # it comes with genes (its conditions too): only on kinds the catalogue doesn't plan yet
            if lk.word == 'womanBeard' and k.women_beards:
                continue
            if lk.word == 'conjoined':
                continue
            out.append(lk)
        return out

    def looks_phase(self, ks):
        """One picture of each special look for the kinds it occurs in, and of each ordinary look for the common kinds."""
        for k in ks:
            ages = [a for a, s in k.ages if a in W.GROWN] or [a for a, _ in k.ages]
            if not ages:
                continue
            for lk in self.looks_for(k):
                seen = lk.share >= LOOK_SEEN_SHARE
                if lk.special:
                    if k.weight < LOOK_MIN_WEIGHT and not lk.kinds:
                        continue
                elif not (k.weight >= LOOK_ALL_WEIGHT or (k.weight >= LOOK_MIN_WEIGHT and seen)):
                    continue
                for si, sex in enumerate(k.sexes):
                    if lk.word == 'womanBeard' and sex != 'female':
                        continue
                    if self.ledger.has(('l', k.kind, sex, lk.word)):
                        continue
                    rng = seeded(k.kind, sex, lk.word)
                    age = weighted(rng, [(a, s) for a, s in k.ages if a in ages])
                    pairs = k.spread(age)
                    pair = pairs[rng.randrange(len(pairs))]
                    self.make('looks', k.wave, k, sex, age, pair, look=lk, wide=True)

    def variety(self, ks, budget):
        """More plain, role and look pictures in rounds, each cell up to its share of the kind's pictures."""
        cells = []
        for k in ks:
            total = VARIETY_TOTAL * k.weight * k.weight
            for age, share in k.ages:
                for sex in k.sexes:
                    target = math.ceil(total * share / len(k.sexes))
                    cells.append(('d', k, sex, age, target, None))
                    if k.weight >= ROLE_MIN_WEIGHT and age in self.role_ages(k):
                        for role, rs in k.roles:
                            if role in self.role_list(k):
                                cells.append(('r', k, sex, age, math.ceil(total * share * rs * 0.6 / len(k.sexes)), role))
            if k.weight >= LOOK_MIN_WEIGHT:
                for lk in self.looks_for(k):
                    if lk.share >= LOOK_SEEN_SHARE and not lk.special:
                        for sex in k.sexes:
                            cells.append(('l', k, sex, None, math.ceil(lk.share * k.weight * 40 / len(k.sexes)), lk))
        written = 0
        rnd = 0
        last = max([c[4] for c in cells] + [0])
        while written < budget and rnd < last:
            rnd += 1
            for typ, k, sex, age, target, extra in cells:
                if written >= budget:
                    break
                if typ == 'd':
                    have = self.ledger.cells[('d', k.kind, sex, age)]
                elif typ == 'r':
                    have = self.ledger.cells[('r', k.kind, sex, age, extra)]
                else:
                    have = self.ledger.cells[('l', k.kind, sex, extra.word)]
                if have >= target or have >= rnd:
                    continue
                rng = seeded(k.kind, sex, str(age), typ, str(rnd), extra.word if typ == 'l' else (extra or ''))
                a = age or weighted(rng, [(x, s) for x, s in k.ages if x in W.GROWN] or k.ages)
                pairs = k.spread(a)
                pair = pairs[rng.randrange(len(pairs))]
                marks = ()
                if typ == 'd' and a in W.GROWN and rng.random() < 0.08:
                    marks = (weighted(rng, [('scar', 4), ('lostEye', 2), ('eyePatch', 1), ('bionicEye', 1), ('burn', 1)]),)
                self.make('variety', k.wave, k, sex, a, pair, role=extra if typ == 'r' else '',
                          look=extra if typ == 'l' else None, marks=marks, wide=True)
                written += 1

    # ------------------------------------------------------------ the characters lists

    def char_ages(self, k):
        allowed = set(W.GROWN) | CHAR_LITTLE.get(k.wave, set())
        return [(a, s) for a, s in k.ages if a in allowed and s > 0]

    def char_plain_count(self, k, top):
        if k.kind == 'Baseliner':
            return CHAR_BASELINER
        if k.wave == 1:
            n = float(k.row.get('saveCount') or 0)
            return max(4, int(round(CHAR_PLAIN[1] * math.sqrt(n / top)))) if top else CHAR_PLAIN[1]
        if k.wave == 5 and k.weight < CHAR_RARE_WEIGHT:
            return 2
        return CHAR_PLAIN.get(k.wave, 3)

    def char_cells(self, k, n):
        """(sex, age) for each of a kind's n plain character rows. Many rows: every band by its share flattened (so the
        rare ages still come up). A few: the sexes take turns, the bands drawn by share, at least one grown-up of 18 to
        44 and, from three rows, one of 45 or more where the kind has them."""
        # Little ones (babies, toddlers, children: those who can't work yet) at half their flattened share: the player,
        # 22 November 2026: "kids are not that common in the game ... there should be a bunch of kids and toddler images
        # available, they are not common in the game and not much of a priority overall".
        ages = [(a, s ** 0.6 * (LITTLE_SHARE if a in LITTLE_AGES else 1.0)) for a, s in self.char_ages(k)]
        if not ages or n <= 0:
            return []
        rng = seeded('characters', k.kind)
        sexes = list(k.sexes)
        if n >= 12:
            total = sum(w for _, w in ages)
            quota = [((sex, a), n * w / total / len(sexes)) for a, w in ages for sex in sexes]
            counts = {c: int(q) for c, q in quota}
            left = n - sum(counts.values())
            order = sorted(quota, key=lambda cq: (-(cq[1] - int(cq[1])), sexes.index(cq[0][0]), W.AGES.index(cq[0][1])))
            for c, _ in order[:left]:
                counts[c] += 1
            return [c for c, _ in quota for _ in range(counts[c])]
        pool = dict(ages)
        picks = []
        for _ in range(n):
            a = weighted(rng, sorted(pool.items(), key=lambda x: W.AGES.index(x[0])))
            pool[a] *= 0.35
            picks.append(a)
        share = dict(ages)
        older = [a for a in ('middle', 'old', 'elder') if a in share]
        younger = [a for a in ('young', 'adult') if a in share]
        if n >= 3 and older and not set(picks) & set(older):
            picks[-1] = max(older, key=lambda a: share[a])
        if younger and not set(picks) & set(younger):
            picks[0] = max(younger, key=lambda a: share[a])
        start = rng.randrange(len(sexes))
        return [(sexes[(start + i) % len(sexes)], a) for i, a in enumerate(picks)]

    def char_extras(self, k, n, typ):
        """(sex, age, role or look word) for a kind's n characters with a role ('r') or a look ('l'): its roles by share,
        or the looks its people can show (no beauty, no special look) by how common, each once before any comes again."""
        rng = seeded('characters', typ, k.kind)
        if typ == 'r':
            items = [(ro, math.sqrt(s)) for ro, s in k.roles if s > 0 and ro in self.roles]
            ages = [(a, s) for a, s in k.ages if a in self.role_ages(k)]
        else:
            items = [(lk.word, math.sqrt(lk.count) if lk.count else 0.3) for lk in self.looks_for(k)   # unseen ones now and then
                     if not lk.special and lk.word not in CHAR_NO_LOOKS and not C.BANNED.search(C.plain_words(lk.prompt))
                     and not re.search(C.LOOK_CONFLICTS.get(lk.word, '^$'), k.row.get('look') or '')]
            ages = [(a, s) for a, s in k.ages if a in W.GROWN and s > 0]
        if not items or not ages:
            return []
        out = []
        pool = dict(items)
        for i in range(n):
            if not pool:
                pool = dict(items)
            word = weighted(rng, sorted(pool.items()))
            del pool[word]
            out.append((k.sexes[i % len(k.sexes)], weighted(rng, ages), word))
        return out

    def char_pair(self, k, sex, age, j):
        """The skin and hair of a cell's j-th character: the kind's spread in steps, so a cell's rows differ, and now
        and then another of its hair colours."""
        pairs = k.spread(age)
        rng = seeded('characters', 'pair', k.kind, sex, age)
        off = rng.randrange(len(pairs))
        stride = next(s for s in (3, 2, 1) if math.gcd(s, len(pairs)) == 1)
        skin, hair = pairs[(off + (j - 1) * stride) % len(pairs)]
        hairs = k.hair_entries(age)
        pick = seeded('characters', 'hair', k.kind, sex, age, str(j))
        if len(hairs) > 1 and pick.random() < 0.25:
            hair = hairs[pick.randrange(len(hairs))]
        return skin, hair

    def characters(self, ks):
        """
        The characters lists (docs/portrait-art/catalogue/README.md, "The characters lists"; the player, 22 November 2026:
        "many of the characters looks rather generic, please create me a new list of characters to import with all
        kinds of variety that could appear in the game"): for every kind of the waves, people who are plain in the game's
        sense (no role, no look and no mark but what the row records, so anyone of the kind, sex and age can get them),
        each a distinct person: a face of their own, hair, a beard and a build with shape, clothes with character in
        their age of dress and wealth, a mood and a pose to match. For the common kinds also some with a role or a look,
        written the same way and recorded so the game gives them only to those who have it. The rows are planned per
        kind from the catalogue alone, so running again adds only what a cap left out.
        """
        top = max([float(k.row.get('saveCount') or 0) for k in ks if k.wave == 1] + [0])
        specs = []
        for ki, k in enumerate(ks):
            mine = []
            seen = collections.Counter()
            for sex, age in self.char_cells(k, self.char_plain_count(k, top)):
                seen[(sex, age)] += 1
                j = seen[(sex, age)]
                if j <= self.ledger.cells[('char', 'c', k.kind, sex, age)]:
                    continue
                marks = ()
                rng = seeded('characters', 'mark', k.kind, sex, age, str(j))
                if age in W.GROWN and rng.random() < 0.06:
                    marks = (weighted(rng, [('scar', 4), ('lostEye', 2), ('eyePatch', 2), ('bionicEye', 1), ('burn', 1), ('missingEar', 1)]),)
                mine.append((k, sex, age, self.char_pair(k, sex, age, j), '', None, marks))
            nr, nl = CHAR_ROLES_LOOKS.get(k.wave, (0, 0))
            if k.wave == 1 and top:
                f = math.sqrt(float(k.row.get('saveCount') or 0) / top)
                nr, nl = int(round(nr * f)), int(round(nl * f))
            for typ, n in (('r', nr), ('l', nl)):
                have = sum(v for c, v in self.ledger.cells.items() if c[:3] == ('char', typ, k.kind))
                for i, (sex, age, word) in enumerate(self.char_extras(k, n, typ)[have:]):
                    pairs = k.spread(age)
                    pair = pairs[seeded('characters', typ, k.kind, word, str(i)).randrange(len(pairs))]
                    mine.append((k, sex, age, pair, word if typ == 'r' else '', self.looks[word] if typ == 'l' else None, ()))
            specs += [(((i + 0.5) / len(mine), ki), s) for i, s in enumerate(mine)]
        for _, (k, sex, age, pair, role, look, marks) in sorted(specs, key=lambda x: x[0]):
            self.make_character(k, sex, age, pair, role=role, look=look, marks=marks)
        # The second round: every common combination covered (the player, 23 November 2026: "there should again be a
        # lot of variety, but it should also cover more of the general combinations ... each Pwn should ultimately look
        # a bit individual in some way, not too basic ... no pawn just looks fully generic"). The game gives a picture
        # only to a person of its kind, sex, age band and skin tone, so each such cell gets a few, counted with the rows
        # already in the characters lists; most of them broad (fewer, milder features), some as specific as the first
        # round's.
        specs = []
        for ki, k in enumerate(ks):
            mine = []
            for sex in k.sexes:
                for age, _share in self.char_ages(k):
                    human = all(e[0] in HUMAN_SKIN_SLUGS for e in k.skins.entries) and len(k.skins.entries) > 3
                    for sk in k.skins.entries:
                        if human and k.kind != 'Baseliner' and sk[0] not in COVER_TONES:
                            continue    # another kind of human skin: three tones, each standing for its neighbours too
                        want = cover_target(k, age, sk[0])
                        have = self.ledger.cells[('chs', k.kind, sex, age, sk[0])]
                        hairs = k.hair_entries(age) or [('bald', '', '')]
                        for j in range(have, want):
                            hr = seeded('characters', 'cover', k.kind, sex, age, sk[0], str(j))
                            hair = hairs[hr.randrange(len(hairs))]
                            marks = ()
                            if age in W.GROWN and hr.random() < 0.05:
                                marks = (weighted(hr, [('scar', 4), ('lostEye', 2), ('eyePatch', 2), ('bionicEye', 1), ('burn', 1), ('missingEar', 1)]),)
                            mine.append((k, sex, age, (sk, hair), marks))
            specs += [(((i + 0.5) / len(mine), ki), x) for i, x in enumerate(mine)]
        for _, (k, sex, age, pair, marks) in sorted(specs, key=lambda x: x[0]):
            self.make_character(k, sex, age, pair, marks=marks, broad=True)

    def make_character(self, k, sex, age, pair, role='', look=None, marks=(), broad=False):
        """One characters row: its record columns and its prompt, every choice seeded by the row's name."""
        if len(self.rows) >= self.cap:
            raise Full()
        skin, hair = pair
        tag = role or (look.word.lower() if look else 'char')
        name = self.name('p_%s_%s_%s_%s_%s_%s' % (k.slug, 'm' if sex == 'male' else 'f', age, tag, skin[0], hair[0]))
        rng = seeded('character', name)
        little = age in W.LITTLE
        twins = k.kind == 'MultiMind_ConjoinedHuman'
        looks = list(k.always) + ([look.word] if look else [])
        kind_look = C.for_age(C.plain_words(k.look), age)
        body_text = kind_look + ' ' + k.shape
        own_text = C.plain_words(look.prompt) if look else ''
        looks_text = ' '.join(C.plain_words(self.looks[lw].prompt) for lw in looks if lw in self.looks)
        fit = LOOK_FIT.get(look.word, {}) if look else {}
        skin_human = skin[0] in HUMAN_SKIN_SLUGS
        surface = C.surface_of(body_text + ' ' + skin[2], k.features, skin_human)
        rrow = self.roles.get(role) if role else None
        # the record's words (a word listed alone in kinds.csv, such as a genie's build slim, is that kind's only one)
        hair_word = 'bald' if hair[0] == 'bald' or not hair[1] else (weighted(rng, even(k.hair)) or 'short')
        if fit.get('hair') and hair_word not in fit['hair']:
            hair_word = 'short'
        if age == 'baby' and hair_word != 'bald':
            hair_word = 'short'
        if little or age == 'teen' or (sex == 'female' and not k.women_beards):
            beard = 'none'
        else:
            beard = weighted(rng, [(b, s * (0.7 if b == 'none' else 1.0)) for b, s in even(k.beards)]) or 'none'
            if fit.get('beard') and sex == 'male' and fit['beard'] in dict(k.beards):
                beard = fit['beard']
        if age in ('baby', 'toddler'):
            build = 'average'
        elif age in ('child', 'teen'):
            build = weighted(rng, [('average', .6), ('slim', .4)])
        else:
            build = weighted(rng, even(k.builds)) or 'average'
            if fit.get('build') in dict(k.builds):
                build = fit['build']
        era = weighted(rng, [(e, s ** 0.6) for e, s in even(k.eras)]) or 'industrial'
        wealth = weighted(rng, [('destitute', .07), ('poor', .26), ('common', .37), ('prosperous', .19), ('noble', .11)])
        if role == 'leader':
            wealth = weighted(rng, [('prosperous', .5), ('noble', .5)])
        elif role in ('trader', 'scholar', 'doctor', 'priest'):
            wealth = weighted(rng, [('common', .5), ('prosperous', .4), ('poor', .1)])
        elif role in ('labourer', 'miner', 'farmer', 'herder'):
            wealth = weighted(rng, [('poor', .5), ('common', .45), ('destitute', .05)])
        if wealth == 'noble' and (era == 'tribal' or role not in ('', 'leader', 'trader')):
            wealth = 'prosperous'
        hardship = weighted(rng, [('sheltered', .1), ('ordinary', .4), ('hard', .37), ('brutal', .13)])
        if hardship == 'sheltered' and age not in ('young', 'teen'):
            hardship = 'ordinary'
        if age == 'teen' and hardship in ('hard', 'brutal'):
            hardship = 'ordinary'
        if role in ('soldier', 'warrior'):
            hardship = weighted(rng, [('ordinary', .3), ('hard', .45), ('brutal', .25)])
        if wealth in ('noble', 'prosperous') and hardship == 'brutal':
            hardship = 'hard'
        if rrow:
            dress = rrow['dress'] or 'work'
        elif wealth == 'destitute':
            dress = 'rags'
        elif wealth == 'noble':
            dress = 'fine'
        elif wealth == 'prosperous':
            dress = weighted(rng, [('fine', .45), ('work', .55)])
        else:
            dress = weighted(rng, [('work', .88), ('robes', .12)])
        facing, facing_text = weighted(rng, [((f, t), w) for f, t, w in FACINGS])
        person, opening, again = age_words(rng, k, sex, age)
        he = 'They' if twins else 'He' if sex == 'male' else 'She'
        wears = 'wear' if twins else 'wears'
        # the body: the kind, its shape, skin, hair, beard and build
        parts = []
        for text in (kind_look, k.shape if age in W.GROWN else ''):   # the body's shape is a grown body's
            if text:
                parts.append(child_look(text) if little else text)
        parts.append(skin[2])
        colour = hair[2] or ''
        both = ' on both heads' if twins else ''
        open_brow = bool(OPEN_BROW.search(kind_look))
        if k.noun != 'hair':
            if colour:   # fur as a colour only: the face says what the fur is like
                parts.append(('{c} fur' if k.noun == 'fur' else NOUN_PHRASE.get(k.noun, '{c} ' + k.noun)).format(c=colour))
        elif hair_word != 'bald' and little:
            phrase, hair_word = rng.choice([h for h in C.LITTLE_HAIR[age] if not (open_brow and FRINGE.search(h[0]))]
                                           or C.LITTLE_HAIR[age])
            parts.append(phrase.format(c=colour or 'dark') + both)
        elif hair_word != 'bald':
            if re.search(r'\bhair\b', kind_look):
                parts.append('%s hair' % (colour or 'dark'))
            else:
                opts = [p for p, t in C.HAIR[hair_word] if age in C.AGE_OK[t]]
                if open_brow:       # no fringe over a brow ridge, a low forehead, horns or a third eye
                    opts = [p for p in opts if not FRINGE.search(p)] or opts
                if fit.get('tidy'):
                    opts = [p for p in opts if not MESSY.search(p)] or opts
                if fit.get('messy'):
                    opts = [p for p in opts if MESSY.search(p)] or opts
                parts.append(rng.choice(opts).format(c=colour or 'dark') + both)
        elif not re.search(r'\bbald\b|\bhair\b|\bhairless\b', kind_look) and not little:
            if all(e[0] == 'bald' for e in k.hairs.entries):
                if not re.search(r'crest|scalp|chitin|insect|mushroom|fung|tentacle|head of|-like head', body_text):
                    parts.append(rng.choice(C.HAIRLESS) + both)
            else:
                parts.append(rng.choice([p for p, _ in C.HAIR['bald'] if skin_human or 'sunburnt' not in p]) + both)
        if beard != 'none':
            if 'sideburns' in kind_look:
                parts.append(C.SIDEBURNS.get(beard, C.SIDEBURNS['full']).format(c=colour or 'dark'))
            else:
                parts.append(('both with ' if twins else '') + rng.choice(C.BEARDS[beard]).format(c=colour or 'dark'))
        elif sex == 'male' and age in W.GROWN and any(b != 'none' for b, _ in k.beards):
            parts.append('both clean-shaven' if twins else 'clean-shaven')
        build_adj = ''
        if age in ('child', 'teen'):
            phrase, build_adj = rng.choice(C.LITTLE_BUILDS[build])
            parts.append(phrase)
        elif not little and not re.search(r'\b(frame|build|body)\b', body_text + ' ' + own_text):
            phrase, build_adj = rng.choice(C.BUILDS[build])
            parts.append(phrase)
        # the face: its shape and skin and one to three features of its own, never twice the same
        strong = 0.2 if k.kind == 'Baseliner' else 0.35
        mild = broad and seeded('characters', 'broad', name).random() < COVER_BROAD
        if mild:
            strong = 0.08
        faces = []
        for _ in range(2 if twins else 1):
            for _try in range(60):
                base, feats = C.face(rng, age, body_text, k.features, skin_human, k.far, strong, marks, own_text,
                                     light=skin[0] in ('veryfair', 'fair', 'light'), build=build, skin_words=skin[2])
                if k.sign:          # a kind of its own look: one feature of the face (a woman two), so the kind's look leads
                    feats = feats[:2 if sex == 'female' else 1]
                elif mild:          # a broad picture: one or two features of its own, never none
                    feats = feats[:2]
                key = '%s; %s' % (base, '; '.join(sorted(feats)))
                if key not in self.ledger.faces and key not in [f[2] for f in faces]:
                    break
            faces.append((base, feats, key))
        # the mood: an expression and a pose that fit it, in the range the kind's own look leaves
        if age == 'baby':
            expr, mood_adj, mpose = rng.choice(C.BABY_EXPRESSIONS), '', ''
        elif age == 'toddler':
            expr, mood_adj, mpose = rng.choice(C.TODDLER_EXPRESSIONS), '', ''
        elif age == 'child':
            expr, mood_adj = rng.choice(C.CHILD_EXPRESSIONS)
            mpose = ''
        else:
            allowed = next((ms for rx, ms in C.KIND_MOODS if re.search(rx, kind_look)), None)
            weights = C.MOOD_WEIGHTS_COMMON if k.wave <= 2 else C.MOOD_WEIGHTS_RARE
            tweak = {'noble': {'haughty': 2.0, 'proud': 1.5}, 'prosperous': {'haughty': 1.4, 'proud': 1.3},
                     'destitute': {'sullen': 1.5, 'sly': 1.3, 'weary': 1.3}, 'poor': {'sullen': 1.2, 'weary': 1.2}}.get(wealth, {})
            moods = [(m, w * tweak.get(m, 1.0)) for m, w in sorted(weights.items()) if allowed is None or m in allowed]
            mood = weighted(rng, moods)
            exprs, poses, mood_adj = C.MOODS[mood]
            expr, mpose = rng.choice(exprs), rng.choice(poses)
            if twins:
                other = weighted(rng, [(m, w) for m, w in moods if m != mood] or moods)
                expr = 'the left face with %s, the right with %s' % (expr, rng.choice(C.MOODS[other][0]))
            if look and FACE_WORDS.search(own_text):
                expr, mood_adj = '', ''     # the look says the face (a drinker's tired eyes, a psychopath's cold stare)
            elif re.search(r'blank expression', kind_look):
                expr = ''                   # an android's blank face, its mood only in the pose
        # the clothes
        acc = rng.choice(C.ACCESSORIES[era]) if rng.random() < 0.4 else ''
        bare = ''
        clothed = bool(re.search(r'\b(robe|robes|armour|gear|clothes|clothing|cloak|coat|wrap)\b', looks_text))
        if age == 'baby':
            clothes = 'Wrapped in %s.' % rng.choice(C.BABY_WRAPS[era])
        elif little:
            opts = [x[2:] if x.startswith('f:') else x for x in C.LITTLE_CLOTHES[era] if sex == 'female' or not x.startswith('f:')]
            clothes = '%s %s %s.' % (he, wears, rng.choice(opts))
        elif clothed:
            clothes = ''
        elif rrow:
            clothes = '%s %s %s.' % (he, wears, ', '.join(x for x in (ROLE_WEALTH[wealth], acc) if x))
            if acc:
                bare = '%s %s %s.' % (he, wears, ROLE_WEALTH[wealth])
        else:
            pool = 'poor' if wealth == 'poor' and dress == 'work' and rng.random() < 0.6 else dress
            opts = [x[2:] if x.startswith('f:') else x for x in C.CLOTHES[era][pool]
                    if (sex == 'female' or not x.startswith('f:')) and (skin_human or 'sunburnt' not in x)]
            garment = rng.choice(opts)
            worn = '' if pool in ('poor', 'rags') else C.WEALTH_WORDS[wealth]     # those clothes say it already
            clothes = '%s %s %s.' % (he, wears, ', '.join(x for x in (garment, acc, worn) if x))
            if acc:
                bare = '%s %s %s.' % (he, wears, ', '.join(x for x in (garment, worn) if x))
        # the sentences
        S = lambda t: t[:1].upper() + t[1:]
        sentences = [opening + ': ' + ', '.join(p for p in parts if p) + '.']
        for i, (base, feats, _) in enumerate(faces):
            sentences.append(C.face_sentence(base, feats, ('Left face', 'Right face')[i] if twins else 'Face'))
        if expr:
            sentences.append(S(expr) + '.')
        if little:
            sentences.append("A plain, real %s's face with small eyes in proportion to the face, an ordinary, homely look."
                             % {'baby': 'baby', 'toddler': 'toddler'}.get(age, 'child'))
        elif not k.far:
            sentences.append('The eyes of ordinary size for the face.')
        if sex == 'female' and not little:
            # FLUX draws a woman pretty unless told otherwise: her looks drawn per row (WOMAN_LOOKS), by her name, so
            # the rest of the row's choices stay as they were.
            wr = seeded('characters', 'woman', name)
            x, line = wr.random(), ''
            for share, lines in WOMAN_LOOKS:
                x -= share
                if x <= 0:
                    line = wr.choice(lines)
                    break
            if line:
                sentences.append(line)
            if wr.random() < 0.4:
                sentences.append(WOMAN_CLOTHES)
        signs = (FAR_AGE_SIGNS if k.far else AGE_SIGNS).get(age, '')
        if signs and not little:
            sentences.append(S(signs) + '.')
        for lw in looks:
            if lw in self.looks and not (lw in k.always and said_in(self.looks[lw].prompt, kind_look)):  # said once
                sentences.append(S(C.plain_words(self.looks[lw].prompt)) + '.')
        for mw in marks:
            if mw in self.marks:
                sentences.append(S(self.marks[mw]['prompt']) + '.')
        if rrow:
            sentences.append('%s: %s.' % (S(rrow['label']), C.plain_words(rrow[era])))
        if clothes:
            sentences.append(clothes)
        hard = ''
        if not little:
            said = ' '.join(sentences).lower()
            table = C.FAR_HARDSHIP if k.far or surface != 'human' else C.HARDSHIP
            opts = [h for h in table[hardship] if not any(w in said and w in h.lower() for w in ECHO_WORDS)
                    and (skin_human or not C.SKIN_COLOUR_WORDS.search(h.lower()))]
            hard = rng.choice(opts) if opts else ''
            sentences.append(hard)
        line = ''
        if not twins and age not in ('baby', 'toddler'):
            words = [f for f in faces[0][1]] + faces[0][0].split(', ')
            face_adj = next((C.FEATURE_ADJ[w] for w in words if w in C.FEATURE_ADJ), '')
            adjs = []
            for a in (mood_adj, build_adj, face_adj):
                if a and a not in adjs:
                    adjs.append(a)
            if adjs:
                sep = ', ' if person.split()[0] in ('young', 'old', 'middle-aged', 'very', 'teenage') else ' '
                line = 'In short, %s %s%s%s.' % ('an' if adjs[0][0] in 'aeiou' else 'a', ', '.join(adjs), sep, person)
                sentences.append(line)
        sentences += [facing_text, again]
        if k.sign:          # the kind's look said again at the end, so it holds (the player: "really describe ... what the race looks like")
            sign = child_look(k.sign) if little else k.sign
            if sign:
                sentences.append('Clearly %s %s, with %s.' % ('an' if k.label[:1].lower() in 'aeiou' else 'a', k.label, sign))

        def tidy(ss):
            return re.sub(r'\s+', ' ', ' '.join(x for x in ss if x)).replace(' ,', ',').replace(',.', '.').replace('..', '.')
        # A long prompt loses its least needed words first: the short character line, the trinket, the hardship line.
        for old, new in ((line, ''), (clothes, bare or clothes), (hard, '')):
            if len(tidy(sentences)) <= MAX_SUBJECT:
                break
            if old and new != old and old in sentences:
                sentences[sentences.index(old)] = new
        subject = tidy(sentences)
        if C.BANNED.search(subject):
            subject = C.plain_words(subject)
        if age == 'baby':
            pose = rng.choice(C.BABY_POSES)
        elif age == 'toddler':
            pose = rng.choice(C.TODDLER_POSES)
        elif age == 'child':
            pose = rng.choice(C.CHILD_POSES)
        elif look and look.pose:
            pose = look.pose
        elif rrow and rrow['pose']:
            pose = rrow['pose']
        else:
            pose = mpose
        row = {
            'name': name, 'subject': subject, 'pose': pose[:1].upper() + pose[1:] + '.', 'count': '', 'seed': '',
            'kind': k.xenotype, 'race': k.race_column, 'sex': sex, 'age': age, 'skin': skin[1], 'hairColour': hair[1],
            'hair': hair_word, 'beard': beard, 'build': build, 'era': era, 'dress': dress if not little else 'work',
            'features': ';'.join(k.features), 'marks': ';'.join(marks), 'looks': ';'.join(looks), 'role': role,
            'facing': facing, 'pawn': '',
        }
        if not row['hairColour'] and hair_word != 'bald':
            row['hairColour'] = '#3b2a20'
        self.rows.append((CHARACTERS, k.wave, row))
        for c in self.ledger.cells_of(k, sex, age, skin[1], hair[1], role, looks):
            self.ledger.cells[c] += 1
        own = [lk for lk in looks if lk not in k.always]
        self.ledger.cells[('char', 'r' if role else 'l' if own else 'c', k.kind, sex, age)] += 1
        if not role and not own:
            self.ledger.cells[('chs', k.kind, sex, age, skin[0])] += 1
        self.ledger.faces.add(face_key(subject))
        return row


# ---------------------------------------------------------------- the report


def coverage_report(kinds, looks, ledger, planner, written_files, unknown, records_n, catalogue_bad):
    """COVERAGE.md: what the library and the lists hold against the catalogue, the gaps first."""
    L = ['# Portrait coverage', '',
         'Written by `Tools/PortraitArt/plan_batches.py` (do not edit by hand). "Pictures" are those in the library\'s',
         'records.xml the planner was given; "listed" are rows in the batch lists, made or not; a gap is a cell with',
         'neither. Cells: every sex and age band a kind comes in (plain pictures), its common roles, and the looks.', '']
    L.append('Library pictures read: %d. Rows in the batch lists before this run: %d (with at least one picture: %d). '
             'Rows written by this run: %d.' % (records_n, len(ledger.names), len(ledger.made), len(planner.rows)))
    L.append('')
    L += ['## By wave', '', '| wave | kinds | plain cells covered (sex and age) | with a picture | plain rows | role rows | look rows |',
          '|---|---|---|---|---|---|---|']
    for w in range(1, 6):
        ks = [k for k in kinds.values() if k.plannable and k.wave == w]
        if not ks:
            continue
        cells = [(k, s, a) for k in ks for a, _ in k.ages for s in k.sexes]
        cov = sum(1 for k, s, a in cells if ledger.has(('d', k.kind, s, a)))
        pic = sum(1 for k, s, a in cells if ledger.made_cells[('d', k.kind, s, a)])
        names = {k.kind for k in ks}
        rows = collections.Counter()
        for c, v in ledger.cells.items():
            if c[1] in names and c[0] in ('d', 'r', 'l'):
                rows[c[0]] += v
        L.append('| %d | %d | %d of %d | %d | %d | %d | %d |' % (w, len(ks), cov, len(cells), pic, rows['d'], rows['r'], rows['l']))
    L.append('')
    if catalogue_bad:
        L += ['## Catalogue problems', ''] + ['- ' + b for b in catalogue_bad] + ['']
    gaps = []
    for k in sorted(kinds.values(), key=lambda k: (k.wave, -k.weight, k.kind)):
        if not k.plannable:
            continue
        miss = [(sex, a) for a, _ in k.ages for sex in k.sexes if not ledger.has(('d', k.kind, sex, a))]
        if miss:
            gaps.append((k, miss))
    L += ['## Gaps: kinds with ages that have no picture and no row yet', '']
    if not gaps:
        L += ['None: every kind, sex and age band has a picture or a listed row.', '']
    else:
        L += ['| wave | kind | weight | missing (sex, age) |', '|---|---|---|---|']
        for k, miss in gaps:
            shown = ', '.join('%s %s' % ('m' if s == 'male' else 'f', a) for s, a in miss[:10]) + (' ...' if len(miss) > 10 else '')
            L.append('| %d | %s (%s) | %.2f | %d: %s |' % (k.wave, k.label, k.kind, k.weight, len(miss), shown))
        L.append('')
    if unknown:
        L += ['## Needed in the game, not in the catalogue', '',
              'missing.csv names these; each needs a row in kinds.csv (a look sheet) before rows can be planned.', '']
        for kind, n in unknown:
            L.append('- %s (%d)' % (kind, n))
        L.append('')
    L += ['## By wave and kind', '',
          '| wave | kind | weight | save | plain cells | plain pictures | plain rows | role cells | role rows | look rows |',
          '|---|---|---|---|---|---|---|---|---|---|']
    for k in sorted(kinds.values(), key=lambda k: (k.wave, -k.weight, k.kind)):
        if not k.plannable:
            continue
        cells = [(sex, a) for a, _ in k.ages for sex in k.sexes]
        covered = sum(1 for s, a in cells if ledger.has(('d', k.kind, s, a)))
        pics = sum(ledger.made_cells[('d', k.kind, s, a)] for s, a in cells)
        plain = sum(ledger.cells[('d', k.kind, s, a)] for s, a in cells)
        rcells = [(s, a, r) for a in planner.role_ages(k) for s in k.sexes for r in planner.role_list(k)] if k.weight >= ROLE_MIN_WEIGHT else []
        rcov = sum(1 for s, a, r in rcells if ledger.has(('r', k.kind, s, a, r)))
        rrows = sum(v for c, v in ledger.cells.items() if c[0] == 'r' and c[1] == k.kind)
        lrows = sum(v for c, v in ledger.cells.items() if c[0] == 'l' and c[1] == k.kind)
        L.append('| %d | %s | %.2f | %s | %d of %d | %d | %d | %s | %d | %d |' % (
            k.wave, k.label, k.weight, k.row.get('saveCount', '0'), covered, len(cells), pics, plain,
            ('%d of %d' % (rcov, len(rcells))) if rcells else '-', rrows, lrows))
    L.append('')
    not_planned = [k for k in kinds.values() if not k.plannable]
    if not_planned:
        L += ['Not planned (no look sheet or never shown): ' + ', '.join('%s (%s)' % (k.label, k.kind) for k in not_planned) + '.', '']
    chars = collections.Counter()
    for c, v in ledger.cells.items():
        if c[0] == 'char' and c[2] in kinds:
            chars[(kinds[c[2]].wave, c[1])] += v
            chars[(kinds[c[2]].wave, 'kind', c[2])] = 1
    L += ['## Characters', '',
          'The characters lists (`characters-NN`, `plan_batches.py --phases characters`): distinct people of every kind, '
          'plain ones (no role, no look; anyone of the kind, sex and age can get them) and for the common kinds some with '
          'a role or a look, written the same way.', '',
          '| wave | kinds | with characters | plain rows | role rows | look rows |', '|---|---|---|---|---|---|']
    for w in range(1, 6):
        ks = [k for k in kinds.values() if k.plannable and k.wave == w]
        if ks:
            L.append('| %d | %d | %d | %d | %d | %d |' % (w, len(ks), sum(1 for k in ks if chars[(w, 'kind', k.kind)]),
                                                          chars[(w, 'c')], chars[(w, 'r')], chars[(w, 'l')]))
    L.append('')
    lk_rows = collections.Counter()
    for c, v in ledger.cells.items():
        if c[0] == 'l':
            lk_rows[c[3]] += v
    waiting = [lk for lk in looks.values() if lk_rows[lk.word] == 0]
    L += ['## Looks', '',
          'Rows listed per look (all kinds): ' + ', '.join('%s %d' % (w, n) for w, n in sorted(lk_rows.items(), key=lambda x: (-x[1], x[0]))) + '.', '']
    if waiting:
        L += ['No row yet (rarer, or waiting for a kind that has them): ' + ', '.join(sorted(lk.word for lk in waiting)) + '.', '']
    L += ['## The lists', '', '| list | rows | with a picture |', '|---|---|---|']
    for b in sorted(ledger.lists):
        L.append('| %s | %d | %d |' % (b, ledger.lists[b], ledger.lists_made.get(b, 0)))
    for path, n in written_files:
        L.append('| %s (new) | %d | 0 |' % (os.path.splitext(os.path.basename(path))[0], n))
    L.append('')
    return '\n'.join(L)


# ---------------------------------------------------------------- main


def next_number(folder, prefix):
    n = 0
    for p in glob.glob(os.path.join(folder, prefix + '-*.csv')):
        m = re.search(r'-(\d+)\.csv$', p)
        if m:
            n = max(n, int(m.group(1)))
    return n + 1


PHASE_ORDER = ['needs', 'defaults', 'roles', 'looks', 'variety']


def mix(rows, kinds, even=False):
    """
    The rows in the order they are made, mixed (the player, 22 November 2026: "I don't want it to be like in order, like
    first white boy, white man, white old man ... it should be shuffled a bit, so that like the additions to the game
    are still varied. Not that for the first few hours I only have like white humans"): the kinds take turns, each as
    often as the square root of its count in the player's world (so a rarer kind still comes up early), and within a kind
    its sexes and ages
    take turns, plain pictures before roles and looks. The same rows always give the same order.
    """
    def key_of(r):
        for k in (r.get('kind', ''), r.get('race', '')):
            if k in kinds:
                return k
        return r.get('kind', '') or r.get('race', '') or '?'
    by_kind = collections.OrderedDict()
    for item in rows:
        by_kind.setdefault(key_of(item[2]), []).append(item)
    queues = {}
    for key, items in by_kind.items():
        rng = seeded('mix', key)
        q = []
        phases = collections.OrderedDict()
        for it in sorted(items, key=lambda it: it[2]['name']):
            phases.setdefault(it[0], []).append(it)
        for ph in sorted(phases, key=lambda p: PHASE_ORDER.index(p) if p in PHASE_ORDER else len(PHASE_ORDER)):
            cells = collections.OrderedDict()
            for it in phases[ph]:
                cells.setdefault((it[2].get('sex', ''), it[2].get('age', '')), []).append(it)
            order = list(cells)
            rng.shuffle(order)
            for c in order:
                rng.shuffle(cells[c])
            # sexes and ages take turns; the little ones (babies, toddlers, children) after the rest, so a list's first
            # hours are grown people (the player: kids "not much of a priority overall")
            for group in ([c for c in order if c[1] not in LITTLE_AGES], [c for c in order if c[1] in LITTLE_AGES]):
                while any(cells[c] for c in group):
                    for c in group:
                        if cells[c]:
                            q.append(cells[c].pop(0))
        queues[key] = q
    def turns(k):
        # The square root of how many the player's world has: the commonest kinds come up most, the rare ones still early.
        n = 0.0
        if k in kinds:
            try:
                n = float(kinds[k].row.get('saveCount') or 0)
            except ValueError:
                n = 0.0
        return math.sqrt(n) if n >= 1 else max(0.5, 3.0 * (kinds[k].weight if k in kinds else 0.2))
    # even: each kind's rows spread evenly over the whole run (the characters lists: baseliners about half of every list,
    # the other kinds through all of them, the player: "still having a total mix overall of all races").
    weight = {k: (float(len(queues[k])) if even else turns(k)) for k in queues}
    credit = {k: 0.0 for k in queues}
    out = []
    while queues:                                     # smooth weighted turns
        total = sum(weight[k] for k in queues)
        for k in queues:
            credit[k] += weight[k]
        best = max(sorted(queues), key=lambda k: credit[k])
        credit[best] -= total
        out.append(queues[best].pop(0))
        if not queues[best]:
            del queues[best], credit[best]
    return out


def write_lists(folder, rows, per_file, dry, kinds=None):
    """The planned rows into new lists, each mixed (see mix): needs-people-NN for missing.csv's, characters-NN for the
    characters phase, people-NN for the rest."""
    files = []
    groups = collections.OrderedDict()
    needs = [x for x in rows if x[0] == 'needs']
    chars = [x for x in rows if x[0] == CHARACTERS]
    rest = [x for x in rows if x[0] not in ('needs', CHARACTERS)]
    if kinds is not None:
        needs, rest, chars = mix(needs, kinds), mix(rest, kinds), mix(chars, kinds, even=True)
    for phase, wave, row in needs + rest + chars:
        prefix = {'needs': 'needs-people', CHARACTERS: CHARACTERS}.get(phase, 'people')
        groups.setdefault(prefix, []).append(row)
    for prefix, rs in groups.items():
        n = next_number(folder, prefix)
        for i in range(0, len(rs), per_file):
            chunk = rs[i:i + per_file]
            path = os.path.join(folder, '%s-%02d.csv' % (prefix, n))
            n += 1
            if not dry:
                with open(path, 'w', encoding='utf-8', newline='') as f:
                    w = csv.DictWriter(f, fieldnames=COLUMNS, lineterminator='\n')
                    w.writeheader()
                    for r in chunk:
                        w.writerow(r)
            files.append((path, len(chunk)))
    return files


def load(catalogue):
    kinds = collections.OrderedDict((r['kind'], Kind(r)) for r in read_csv(os.path.join(catalogue, 'kinds.csv')))
    looks = collections.OrderedDict((r['word'], Look(r)) for r in read_csv(os.path.join(catalogue, 'looks.csv')))
    marks = {r['word']: r for r in read_csv(os.path.join(catalogue, 'marks.csv')) if r.get('game') == 'yes'}
    roles = {r['role']: r for r in read_csv(os.path.join(catalogue, 'roles.csv'))}
    return kinds, looks, marks, roles


def plan(catalogue=CATALOGUE, batches=BATCHES, records=None, missing=None, waves=None, phases=None, max_rows=600,
         per_file=200, dry=False, report=None, check=True):
    bad = catalogue_check.check(catalogue) if check else []
    kinds, looks, marks, roles = load(catalogue)
    ledger = Ledger(kinds)
    read_batches(batches, ledger)
    records_n = read_records(records, ledger)
    planner = Planner(kinds, looks, marks, roles, ledger, 0, cap=max_rows)
    unknown = planner.needs(read_missing(missing))
    usable = [k for k in kinds.values() if k.plannable]
    char_waves = list(waves) if waves else [1, 2, 3, 4, 5]
    if not waves:
        gap_waves = sorted({k.wave for k in usable if any(not ledger.has(('d', k.kind, s, a)) for a, _ in k.ages for s in k.sexes)})
        first = gap_waves[0] if gap_waves else min(k.wave for k in usable)
        waves = [first, first + 1]
    phases = phases or PHASES
    order = lambda w: sorted([k for k in usable if k.wave == w], key=lambda k: (-k.weight, k.kind))
    try:
        for phase in ('defaults', 'roles', 'looks'):
            if phase not in phases:
                continue
            for w in waves:
                {'defaults': planner.defaults, 'roles': planner.roles_phase, 'looks': planner.looks_phase}[phase](order(w))
        if 'variety' in phases:
            planner.variety([k for w in waves for k in order(w)], max_rows - len(planner.rows))
        if CHARACTERS in phases:
            planner.characters([k for w in char_waves for k in order(w)])
    except Full:
        pass
    files = write_lists(batches, [r for r in planner.rows], per_file, dry, kinds)
    text = coverage_report(kinds, looks, ledger, planner, files, unknown, records_n, bad)
    if report and not dry:
        with open(report, 'w', encoding='utf-8', newline='\n') as f:
            f.write(text)
    return planner, files, text, bad


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--catalogue', default=CATALOGUE)
    ap.add_argument('--batches', default=BATCHES)
    ap.add_argument('--records', help="the library's records.xml (what pictures exist)")
    ap.add_argument('--missing', help="the library's missing.csv (what the game needed and lacked)")
    ap.add_argument('--waves', help='the waves to plan, e.g. 1,2 (default: the first with a gap and the next; '
                                    'for the characters phase all five)')
    ap.add_argument('--phases', help='which of defaults,roles,looks,variety (default: these four), or characters '
                                     '(the characters lists, only when asked)')
    ap.add_argument('--max-rows', type=int, default=600)
    ap.add_argument('--per-file', type=int, default=200)
    ap.add_argument('--report', default=os.path.join(CATALOGUE, 'COVERAGE.md'))
    ap.add_argument('--dry-run', action='store_true', help='write nothing, say what would be written')
    a = ap.parse_args(argv)
    waves = [int(x) for x in a.waves.split(',')] if a.waves else None
    phases = [p.strip() for p in a.phases.split(',')] if a.phases else None
    planner, files, text, bad = plan(a.catalogue, a.batches, a.records, a.missing, waves, phases, a.max_rows, a.per_file,
                                     a.dry_run, a.report)
    for b in bad:
        print('Catalogue: ' + b)
    by = collections.Counter(p for p, _, _ in planner.rows)
    print('Planned %d rows (%s).' % (len(planner.rows), ', '.join('%s %d' % (p, by[p]) for p in ['needs'] + PHASES + [CHARACTERS] if by[p])))
    for path, n in files:
        print('  %s %s: %d rows' % ('Would write' if a.dry_run else 'Wrote', os.path.relpath(path, ROOT), n))
    if not a.dry_run:
        print('Coverage report: %s' % os.path.relpath(a.report, ROOT))
    return 0


if __name__ == '__main__':
    sys.exit(main())
