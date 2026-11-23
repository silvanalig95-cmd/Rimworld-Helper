"""The portrait catalogue's shared words, for save_census.py and plan_batches.py.

The words are the framework's (docs/portrait-art/FRAMEWORK.md, pillar 2) and Rimfolk's record words
(Source/ColonyCouncil/UI/Faces/PortraitLibraryMath.cs: the age bands, hair, beard, build, era, dress, facing, features,
marks), so what the planner writes is what the game reads. The colour words follow PortraitFolderMath.SkinWord and
HairWord (the words missing.csv uses), ported here line for line. Lists only ever grow: a word once used keeps its
meaning, so batch rows named after it never change what they show.
"""
import csv
import math
import os
import re

# ---------------------------------------------------------------- colours (PortraitLibraryMath, PortraitFolderMath)


def hex_rgb(h):
    h = h.strip().lstrip('#')
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def rgba(text):
    """A colour as the save writes it, RGBA(0.248, 0.198, 0.149, 1.000), as (r, g, b) from 0 to 1."""
    m = re.findall(r'[-\d.]+', text or '')
    if len(m) < 3:
        return (0.2, 0.2, 0.2)
    return tuple(min(1.0, max(0.0, float(x))) for x in m[:3])


def lab(c):
    def lin(v):
        v = min(1.0, max(0.0, v))
        return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4
    r, g, b = (lin(x) for x in c)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883

    def f(t):
        return t ** (1.0 / 3.0) if t > 0.008856 else 7.787 * t + 16.0 / 116.0
    fx, fy, fz = f(x), f(y), f(z)
    return 116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz)


def hsv(c):
    r, g, b = (min(1.0, max(0.0, v)) for v in c)
    mx, mn = max(r, g, b), min(r, g, b)
    d = mx - mn
    v = mx
    s = d / mx if mx > 0 else 0.0
    if d <= 1e-6:
        h = 0.0
    elif mx == r:
        h = 60.0 * (((g - b) / d) % 6.0)
    elif mx == g:
        h = 60.0 * ((b - r) / d + 2.0)
    else:
        h = 60.0 * ((r - g) / d + 4.0)
    if h < 0:
        h += 360.0
    return h, s, v


def unusual_skin(c):
    h, s, v = hsv(c)
    if v < 0.1 or s > 0.72:
        return True
    if s < 0.06 and v < 0.9:
        return True
    if 180 <= h <= 300 and s > 0.02:
        return True
    return s > 0.12 and (h < 5 or h > 50)


def hue_word(c):
    h, s, v = hsv(c)
    if v < 0.15:
        return 'black'
    if s < 0.12:
        return 'white' if v > 0.85 else 'grey'
    if h < 20 or h >= 340:
        return 'red'
    if h < 45:
        return 'orange'
    if h < 70:
        return 'yellow'
    if h < 165:
        return 'green'
    if h < 200:
        return 'teal'
    if h < 255:
        return 'blue'
    if h < 290:
        return 'purple'
    return 'pink'


def skin_word(c):
    if unusual_skin(c):
        return hue_word(c)
    l = lab(c)[0]
    return ('very fair' if l >= 85 else 'fair' if l >= 75 else 'light' if l >= 65 else 'medium' if l >= 55
            else 'tan' if l >= 45 else 'brown' if l >= 33 else 'dark')


def hair_word(c):
    l = lab(c)[0]
    h, s, v = hsv(c)
    if l < 22:
        return 'black'
    if s < 0.15:
        return 'white' if l > 82 else 'grey' if l > 45 else 'black'
    if 150 <= h <= 340 and s > 0.25:
        return hue_word(c)
    if (h < 25 or h > 340) and s > 0.6:
        return 'red'
    if l > 68:
        return 'blond'
    return 'light brown' if l > 45 else 'brown'


def colour_apart(a, b):
    la, lb = lab(a), lab(b)
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(la, lb)))


# The human skins, lightest first (the game's melanin genes fall into these), and the hair colours. A word's colour never
# changes once rows are named after it; slug is the word as a name part.
HUMAN_SKINS = [
    ('veryfair', 'very fair', '#f6e4d6'), ('fair', 'fair', '#ecc9ae'), ('light', 'light', '#d9aa86'),
    ('medium', 'medium', '#c08c66'), ('tan', 'tan', '#a2704c'), ('brown', 'brown', '#7d5236'),
    ('dark', 'dark', '#4e3322'),
]
HAIR_COLOURS = [
    ('black', 'black', '#1c1a19'), ('darkbrown', 'dark brown', '#3b2a20'), ('brown', 'brown', '#5a3d27'),
    ('auburn', 'auburn', '#7a3b1f'), ('lightbrown', 'light brown', '#8a6a4a'), ('blond', 'blond', '#cfae74'),
    ('red', 'red', '#b23a1a'), ('grey', 'grey', '#8e8c88'), ('white', 'white', '#e2e0dc'),
    ('saltpepper', 'salt-and-pepper', '#77736e'),
]

# ---------------------------------------------------------------- ages

AGES = ['baby', 'toddler', 'child', 'teen', 'young', 'adult', 'middle', 'old', 'elder']
LITTLE = {'baby', 'toddler', 'child'}
GROWN = {'young', 'adult', 'middle', 'old', 'elder'}
# How old someone of each band looks, in years (the prompt says "looks about N", never their years).
LOOKS_ABOUT = {
    'baby': (0.5, 1), 'toddler': (2, 3), 'child': (6, 11), 'teen': (14, 17), 'young': (20, 28), 'adult': (31, 43),
    'middle': (46, 57), 'old': (61, 72), 'elder': (76, 88),
}
# The share of each band among the people of an ordinary kind, from League of Toba's people (day 138), with the young
# ones a colony grows raised a little.
BASE_AGES = {'young': 0.24, 'adult': 0.30, 'middle': 0.24, 'old': 0.09, 'elder': 0.03, 'teen': 0.04, 'child': 0.03,
             'toddler': 0.015, 'baby': 0.015}
TRUST_COUNT = 8

HUMAN_LIFE = 80.0
BAND_ENDS = (30.0, 45.0, 60.0, 75.0)
AGELESS_SCALE = 10.0


def _grown_band(years, k):
    if years < BAND_ENDS[0] * k:
        return 'young'
    if years < BAND_ENDS[1] * k:
        return 'adult'
    if years < BAND_ENDS[2] * k:
        return 'middle'
    if years < BAND_ENDS[3] * k:
        return 'old'
    return 'elder'


def age_of(years, born, life_expectancy=HUMAN_LIFE, factor=1.0, ageless=False):
    """PortraitLibraryMath.AgeOf with the game's stage unknown: a baby under one, a toddler to four, a child to 13, a
    teen to 18, then the grown-up bands stretched by the kind's life span; the ageless by their years since birth on a
    ten times longer scale."""
    years = max(0.0, years)
    life = life_expectancy if 20 < life_expectancy < 100000 else HUMAN_LIFE
    if 0.05 < factor < 1000:
        life *= factor
    k = life / HUMAN_LIFE
    if years < 1:
        return 'baby'
    if years < 4:
        return 'toddler'
    if years < 13:
        return 'child'
    if years < 18:
        return 'teen'
    band = _grown_band(years, k)
    if ageless and born > years:
        by = _grown_band(born, k * AGELESS_SCALE)
        if AGES.index(by) > AGES.index(band):
            band = by
    return band


def parse_shares(text):
    """'young=0.3;adult=0.4' as an ordered list of (word, share)."""
    out = []
    for part in (text or '').split(';'):
        if '=' in part:
            k, v = part.split('=', 1)
            try:
                out.append((k.strip(), float(v)))
            except ValueError:
                pass
        elif part.strip():
            out.append((part.strip(), 0.0))
    return out


def blend_ages(seen, hand, n):
    """A kind's age shares: what the save shows, blended with the hand-set shares (or the ordinary ones) by how many
    of its people the save holds, so a kind seen 8 times leans on the save and still keeps its children and elders."""
    base = dict(parse_shares(hand)) or dict(BASE_AGES)
    total = sum(seen.values()) or 1
    trust = min(0.8, n / 100.0 + 0.3)
    mixed = {}
    for a in AGES:
        if a not in base and not seen.get(a):
            continue
        mixed[a] = trust * seen.get(a, 0) / total + (1 - trust) * base.get(a, 0.0)
    s = sum(mixed.values()) or 1
    items = sorted(((a, v / s) for a, v in mixed.items()), key=lambda kv: (-kv[1], AGES.index(kv[0])))
    return ';'.join('%s=%.3f' % (a, v) for a, v in items if v > 0)


# ---------------------------------------------------------------- weight and waves


def weight(count, top, spawn):
    s = math.log(1 + count) / math.log(1 + max(1, top)) if count else 0.0
    return round(0.7 * s + 0.3 * max(0.0, min(1.0, spawn)), 2)


def wave_of(w):
    """The wave a kind's pictures are made in, by its weight: 1 from 0.7, 2 from 0.5, 3 from 0.35, 4 from 0.2, else 5."""
    return 1 if w >= 0.7 else 2 if w >= 0.5 else 3 if w >= 0.35 else 4 if w >= 0.2 else 5


# ---------------------------------------------------------------- roles

ROLES = ['scholar', 'doctor', 'soldier', 'warrior', 'hunter', 'scout', 'builder', 'miner', 'crafter', 'smith',
         'farmer', 'herder', 'cook', 'artist', 'trader', 'leader', 'priest', 'labourer']
ROLE_BY_SKILL = {
    'Intellectual': 'scholar', 'Medicine': 'doctor', 'Shooting': 'soldier', 'Melee': 'warrior',
    'Construction': 'builder', 'Mining': 'miner', 'Crafting': 'crafter', 'Plants': 'farmer', 'Animals': 'herder',
    'Cooking': 'cook', 'Artistic': 'artist', 'Social': 'trader',
}
NEGATIVE_PASSIONS = {'VSE_Apathy'}
# Words in an adulthood backstory's name that say its role, tried in this order (the first that fits wins).
ROLE_WORDS = [
    ('priest', ['chaplain', 'priest', 'evangel', 'preacher', 'monk', 'shaman', 'missionary', 'cleric', 'nun', 'cultist',
                'prophet', 'oracle', 'seer', 'pilgrim', 'templar', 'inquisitor', 'propagandist']),
    ('doctor', ['medic', 'surgeon', 'nurse', 'healer', 'doctor', 'physician', 'apothecary', 'herbalist', 'counsel']),
    ('leader', ['regent', 'ruler', 'lord', 'noble', 'baron', 'king', 'queen', 'mayor', 'politician', 'governor',
                'general', 'envoy', 'aristocrat', 'castellan', 'warlord', 'chief', 'captain', 'commander', 'emperor',
                'prince', 'duke', 'count', 'magistrate', 'ceremonies', 'leader', 'diplomat', 'boss', 'warmaster']),
    ('scholar', ['research', 'scientist', 'physicist', 'programmer', 'inventor', 'academ', 'professor', 'scholar',
                 'scribe', 'librarian', 'critic', 'auditor', 'accountant', 'teacher', 'student', 'engineer', 'archivist',
                 'navigator', 'broker', 'analyst', 'philosopher']),
    ('soldier', ['soldier', 'infant', 'sniper', 'marine', 'officer', 'janissary', 'guard', 'mercenary', 'artiller',
                 'bombardier', 'deserter', 'trooper', 'recon', 'standard', 'sheriff', 'explosive', 'veteran',
                 'contractor', 'militia', 'cadet', 'sergeant', 'gunner', 'rifle', 'warmonger']),
    ('warrior', ['warrior', 'gladiator', 'fighter', 'knight', 'brawler', 'tournament', 'bloodgame', 'bandit',
                 'raider', 'barbarian', 'berserk', 'duelist', 'sword', 'champion', 'pirate']),
    ('hunter', ['hunter', 'archer', 'trapper', 'poacher', 'tracker']),
    ('scout', ['scout', 'ranger', 'spy', 'drifter', 'wanderer', 'traveler', 'traveller', 'loner', 'nomad', 'explorer',
               'courier', 'smuggler']),
    ('smith', ['smith', 'armorer', 'armourer', 'forge', 'metalwork']),
    ('crafter', ['crafter', 'weaver', 'carver', 'tailor', 'jeweler', 'locksmith', 'mechanic', 'tool', 'repair',
                 'tinker', 'artisan', 'potter', 'fabricat', 'technician', 'machinist', 'restorer']),
    ('builder', ['builder', 'framer', 'carpenter', 'mason', 'construct', 'architect', 'foreman']),
    ('miner', ['miner', 'digger', 'quarry', 'shipcracker', 'salvag', 'prospector']),
    ('farmer', ['farm', 'landworker', 'gatherer', 'aquaponic', 'grower', 'botanist', 'silo', 'harvest', 'gardener',
                'forager']),
    ('herder', ['herder', 'beast', 'tamer', 'dog', 'shepherd', 'rancher', 'zookeeper', 'stable', 'cowboy']),
    ('cook', ['chef', 'cook', 'butcher', 'sommelier', 'stew', 'galley', 'baker', 'brewer']),
    ('artist', ['artist', 'dancer', 'idol', 'demoscen', 'patron', 'musician', 'singer', 'writer', 'poet', 'actor',
                'cosplay', 'painter', 'sculptor', 'bard', 'performer', 'dilettante']),
    ('trader', ['merchant', 'trader', 'financier', 'salesman', 'shopkeeper', 'dealer', 'peddler', 'banker', 'gigolo',
                'courtesan']),
    ('labourer', ['logger', 'worker', 'labor', 'labour', 'servant', 'hauler', 'sailor', 'mariner', 'colonist',
                  'malingerer', 'subject', 'hermit', 'pariah', 'janitor', 'slave', 'peasant', 'serf', 'refugee',
                  'banished', 'castaway', 'survivor', 'gamer']),
]


def role_from_backstory(name):
    t = (name or '').lower()
    t = re.sub(r'^(vbe|vfem|vqe|vfee|vfe|stagz|ar|vbe)_', '', t)
    for role, words in ROLE_WORDS:
        for w in words:
            if w in t:
                return role
    return ''


# ---------------------------------------------------------------- dress

ERAS = ['tribal', 'medieval', 'industrial', 'spacer']
ERA_BY_TECH = {'Animal': 'tribal', 'Neolithic': 'tribal', 'Medieval': 'medieval', 'Industrial': 'industrial',
               'Spacer': 'spacer', 'Ultra': 'spacer', 'Archotech': 'spacer'}


def faction_tech(path):
    out = {}
    if not os.path.exists(path):
        return out
    with open(path, encoding='utf-8') as f:
        for r in csv.DictReader(f, delimiter='\t'):
            out[r['defName']] = r['techLevel']
    return out


def tokens(*texts):
    out = []
    for t in texts:
        for part in re.findall(r'[A-Z]?[a-z]+|[A-Z]+(?![a-z])', t or ''):
            out.append(part.lower())
    return out


def _starts(tok, *stems):
    return any(t.startswith(s) for t in tok for s in stems)


def hair_style_of(def_name):
    """The hair's record word from its def's name (PortraitLibraryMath.HairFromWords; the game also measures the
    texture, so this is only for the counts); medium where the name doesn't say."""
    if not def_name:
        return ''
    tok = tokens(def_name)
    if _starts(tok, 'braid', 'plait'):
        return 'braids'
    if _starts(tok, 'pony', 'pigtail', 'bunches', 'tied'):
        return 'tied'
    if any(t in ('bun', 'buns', 'knot', 'topknot') for t in tok) or _starts(tok, 'bun'):
        return 'bun'
    if _starts(tok, 'long', 'dread', 'locks', 'flowy', 'mane'):
        return 'long'
    if any('hawk' in t for t in tok) or _starts(tok, 'undercut'):
        return 'short'
    if _starts(tok, 'bald', 'shave'):
        return 'bald'
    if _starts(tok, 'short', 'crop', 'buzz', 'spike', 'tuft', 'bowl', 'pixie', 'fringe', 'afro', 'crew', 'flattop'):
        return 'short'
    if _starts(tok, 'bob', 'medium', 'shoulder'):
        return 'medium'
    return 'medium'


def beard_of(def_name):
    if not def_name or def_name.startswith('NoBeard'):
        return 'none'
    tok = tokens(def_name)
    if _starts(tok, 'stubble', 'shadow', 'scruff'):
        return 'stubble'
    stache = _starts(tok, 'moustache', 'mustache', 'stache', 'handlebar', 'walrus')
    if stache and not _starts(tok, 'chop', 'beard', 'full', 'goatee', 'dutch', 'braid', 'fork', 'side'):
        return 'moustache'
    if _starts(tok, 'goatee', 'soul', 'chin', 'circle', 'anchor', 'short', 'patch') or 'vandyke' in ''.join(tok):
        return 'short'
    return 'full'


def build_of(body_type):
    tok = tokens(body_type)
    if _starts(tok, 'thin', 'slim', 'lean'):
        return 'slim'
    if _starts(tok, 'fat', 'heavy', 'stout'):
        return 'heavy'
    if _starts(tok, 'hulk', 'broad', 'muscl'):
        return 'broad'
    return 'average'


HUMANLIKE_RACES = {'Human', 'CreepJoiner', 'MultiMind_ConjoinedHuman', 'TM_SpiritTD'}
