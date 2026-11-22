"""The characters lists' words: faces, hair, beards, builds, clothes, moods and poses, for plan_batches.py's characters
phase (docs/portrait-art/catalogue/README.md, "The characters lists").

The player, 22 November 2026: "many of the characters looks rather generic, please create me a new list of characters to
import with all kinds of variety that could appear in the game", and on the faces: "I just don't want this traditional
AI face ... because it has like no character ... everyone should have something individual about them. It doesn't have
to be many things ... Especially on women, this is much more noticeable", "Someone could have one, someone could have
three ... no basic faces ... never a caricature ... especially for other races ... they don't have to be essentially a
human in shape". So every face gets its shape, an ordinary real skin and one to three features of its own, drawn from
wide pools that are the same for women and men; some stronger than life, none a caricature; mild unevenness now and
then; no beauty words anywhere. Nothing here is held in the hands: a plain row's prompt holds nothing a skill would
need, and every hair, beard and build phrase says what the record's word says.
"""
import re

# ---------------------------------------------------------------- no beauty words (the player, 22 November 2026)

BANNED = re.compile(r'\b(beautiful|beauty|pretty|handsome|flawless|perfect|perfectly|smooth|smoothly|symmetrical|symmetric|'
                    r'delicate|elegant|glamorous|model|make-up|makeup|cute|lovely|gorgeous|attractive|alluring|graceful|'
                    r'lustrous|fine-featured|soft features|idealised|idealized)\b', re.I)
# The kinds' look sheets and the looks' and roles' words, said without beauty words in the characters rows (the sheets
# themselves keep the genes' words for the other lists). Tried in order; a word left over is dropped by plain_words.
REWORD = [
    ('strikingly beautiful, smooth flawless skin, soft features and lustrous, well-kept hair',
     'neat, glossy, well-kept hair and a well-groomed look'),
    ('an unnaturally smooth, ageless and attractive face', 'an oddly ageless face'),
    ('a fine-boned face with high, sharp cheekbones', 'a narrow, bony face with high, sharp cheekbones'),
    ('a fine, fair face with high cheekbones', 'a narrow face with high cheekbones'),
    ('a fine face with high cheekbones', 'a narrow face with high cheekbones'),
    ('a delicate, fine-featured woman', 'a slight woman'),
    ('a graceful, fine-featured woman', 'a slight woman'),
    ('a smooth bald head, slender and graceful', 'a bald head, slender and long-limbed'),
    ('a beautiful, delicate face, ', ''),
    ('a beautiful, calm face', 'a calm face'),
    ('a beautiful, ageless face', 'an ageless face'),
    ('an alluring, watchful look', 'a watchful look'),
    ('attractive, with ', ''),
    ('smooth flawless skin', 'synthetic skin'),
    ('smooth skin with faint seam', 'synthetic skin with faint seam'),
    ('a thin delicate frame', 'a thin, narrow-shouldered frame'),
    ('large, delicate translucent', 'large, thin translucent'),
    ('smooth skin with a faint wet sheen', 'skin with a faint wet sheen'),
    ('smooth pale blue skin', 'pale blue skin'),
    ('a smooth hood-like crest', 'a hood-like crest'),
    ('an elegant ', 'a '),
    ('a sly, alluring smile', 'a sly smile'),
    ('strikingly beautiful, with well-proportioned features and clear, even skin', 'a well-groomed look'),
    ('pleasant, even features and clear skin', 'a well-groomed look'),
]


def for_age(text, age):
    """A look sheet for someone who looks middle-aged or older: an ageless kind's "ageless face" left out (the framework:
    the ageless still grow older in looks, slowly)."""
    if age in ('middle', 'old', 'elder'):
        text = re.sub(r'\ban? (oddly )?ageless face(, | and )?', '', text)
    return text


def plain_words(text):
    """The text without beauty words: the known phrases reworded, any word left over dropped."""
    for old, new in REWORD:
        text = text.replace(old, new)
    text = BANNED.sub('', text)
    text = re.sub(r'\s+', ' ', text)
    text = re.sub(r'\b(a|an) ,\s*', r'\1 ', text)
    text = re.sub(r'\s+([,.;:])', r'\1', text).replace(',,', ',').replace(': ,', ':')
    return text.strip()


# ---------------------------------------------------------------- the face
# Each item: (words, stronger words or None, ages). Ages: '' all grown-ups and teens, 'Y' teens and young ones, 'A' from
# 30 (adult and older), 'M' from 45, 'O' from 60. The stronger words are a little larger than life (the player: "an
# especially big chin or something. It doesn't have to be fully realistic all the time"), never a caricature.

AGE_OK = {'': {'teen', 'young', 'adult', 'middle', 'old', 'elder'}, 'Y': {'teen', 'young'},
          'A': {'adult', 'middle', 'old', 'elder'}, 'M': {'middle', 'old', 'elder'}, 'O': {'old', 'elder'}}

FACE_SHAPES = [
    ('a gaunt face', None, ''), ('a round, fleshy face', 'a very round, moon-like face', ''), ('high cheekbones', None, ''),
    ('a long face', 'a very long, horsey face', ''), ('a broad, flat face', None, ''), ('a narrow face', None, ''),
    ('a square, blocky face', None, ''), ('a pear-shaped face, wide at the jaw', None, ''), ('a short, wide face', None, ''),
    ('hollow cheeks', None, ''), ('full, heavy cheeks', None, ''), ('a bony face', None, ''), ('a thin, pinched face', None, ''),
    ('a wide, open face', None, ''), ('a big, heavy-boned face', None, ''), ('a small face with a big forehead', None, ''),
]
SKINS = [  # 'hc': only on human skin colours (it names a colour or a tan)
    ('weathered skin', 'hc'), ('ruddy cheeks', 'hc'), ('sun-browned skin', 'hc'), ('a sallow complexion', 'hc'),
    ('skin with visible pores', ''), ('a few faint blemishes', ''), ('wind-reddened cheeks', 'hc'), ('dry, slightly flaky skin', ''),
    ('a shiny, oily forehead', ''), ('a weather-beaten complexion', 'hc'),
    ('a pale, indoor complexion', 'hc'), ('a few spots on the chin', 'Y'), ('thin, spotted skin', 'O'), ('coarse skin', ''),
]
FEATURES = {
    'nose': [('a hawk nose', 'a big, curved hawk nose', ''), ('a broken, slightly flattened nose', 'a badly broken, flattened nose', ''),
             ('a big, fleshy nose', 'a very big, fleshy nose', ''), ('a long, thin nose', 'a very long, thin nose', ''),
             ('a slightly crooked nose', 'a crooked nose bent well to one side', ''), ('a snub nose', None, ''),
             ('a wide nose', 'a very wide, flat nose', ''), ('a bump on the bridge of the nose', None, ''),
             ('a sharp, pointed nose', 'a long, sharp, beaky nose', ''), ('a strong, straight nose', 'a large, strong nose', ''),
             ('a short, upturned nose', None, 'Y'), ('a heavy, drooping nose', None, 'M')],
    'jaw': [('a lantern jaw', 'a huge lantern jaw', ''), ('a weak chin', 'almost no chin at all', ''), ('heavy jowls', None, 'M'),
            ('a pointed chin', 'a long, sharply pointed chin', ''), ('a wide, square jaw', 'a very wide, square jaw', ''),
            ('a jutting chin', 'an especially big, jutting chin', ''), ('a cleft chin', 'a deep cleft in a big chin', ''),
            ('a double chin', None, 'A'), ('a narrow jaw', None, ''), ('a long chin', 'a very long chin', '')],
    'mouth': [('thin lips', None, ''), ('a wide mouth', 'a very wide mouth', ''), ('a small, tight mouth', None, ''),
              ('a slight overbite', 'a big overbite', ''), ('crooked teeth', None, ''),
              ('a gap between the front teeth', 'a wide gap between the front teeth', ''), ('uneven teeth', None, ''),
              ('a full lower lip', None, ''), ('a downturned mouth', None, ''), ('a chipped front tooth', None, ''),
              ('a few missing teeth', None, 'O')],
    'eyes': [('small eyes', None, ''), ('deep-set eyes', 'very deep-set eyes under a heavy ridge', ''), ('hooded eyes', None, ''),
             ('heavy-lidded eyes', None, ''), ('close-set eyes', 'very close-set eyes', ''), ('narrow eyes', None, ''),
             ('pouches under the eyes', None, 'A'), ('one eye a little narrower than the other', None, '')],
    'brows': [('heavy brows', 'very heavy, bristling brows', ''), ('sparse, pale brows', None, ''), ('a single thick brow', None, ''),
              ('bushy, untidy brows', 'huge, bushy brows', ''), ('thin, high brows', None, ''), ('one brow higher than the other', None, ''),
              ('a deep crease between the brows', None, 'A')],
    'ears': [('ears that stick out', 'huge ears that stick right out', ''), ('large ears', 'very large ears', ''),
             ('long earlobes', None, ''), ('one ear sticking out more than the other', None, ''), ('hairy ears', None, 'O')],
    'lines': [('laugh lines', None, 'A'), ('deep frown lines', None, 'A'), ('a deep line across the forehead', None, 'A')],
    'marks': [('freckles across the nose', 'thick freckles all over the face', ''), ('a mole on one cheek', None, ''),
              ('a birthmark on the neck', None, ''), ('a few old acne marks on the cheeks', None, ''),
              ('a sunburnt, peeling nose', None, ''), ('a big mole above the lip', None, '')],
}
# Mild unevenness, now and then (the player: "Asymmetry only now and then, mildly").
UNEVEN = [('a slightly lopsided face', None, ''), ('one eye a little lower than the other', None, ''),
          ('a slightly crooked smile', None, ''), ('one side of the mouth set a little higher', None, '')]
UNEVEN_SHARE = 0.12
# A scar through the brow only where the row records the scar mark.
SCAR_BROW = ('an old scar through one brow', None, '')

# Animal heads (a person with the head of a fox, a wolf, an ape): the head's shape and coat, and features of their own.
ANIMAL_SHAPES = [('a broad, flat head', None, ''), ('a long, narrow head', None, ''), ('a round, heavy head', None, ''),
                 ('a small head', None, ''), ('a big, blocky head', None, ''), ('a lumpy, uneven head', None, ''),
                 ('a wide, square head', None, '')]
ANIMAL_FEATURES = {
    'muzzle': [('a short, blunt muzzle', 'a very short, squashed muzzle', ''), ('a long, narrow muzzle', 'a very long, narrow muzzle', ''),
               ('a muzzle with a bump on the bridge', None, ''), ('a broad, heavy muzzle', None, ''), ('a slightly crooked muzzle', None, '')],
    'jaw': [('an underbite', 'a big underbite', ''), ('a heavy lower jaw', None, ''), ('a narrow chin', None, ''),
            ('a jutting lower jaw', None, '')],
    'mouth': [('a crooked tooth poking over the lip', None, ''), ('a wide, downturned mouth', None, ''), ('thin, tight lips', None, ''),
              ('a lopsided mouth', None, '')],
    'brows': [('a heavy ridge over the eyes', None, ''), ('a deep frown line between the eyes', None, ''),
              ('one brow ridge higher than the other', None, '')],
    'ears': [('one ear bent over at the tip', None, ''), ('ears pinned back', None, ''), ('especially big ears', 'huge ears', ''),
             ('ears set a little uneven', None, '')],
    'coat': [('a pale patch of fur over one eye', None, ''), ('a dark streak of fur down the nose', None, ''),
             ('fur sticking out in tufts at the cheeks', None, ''), ('grizzled fur at the muzzle', None, 'M'),
             ('a white blaze on the forehead', None, ''), ('tufted brows', None, '')],
}
# The skin of kinds whose skin isn't a human one, by what it is.
SURFACES = {
    'fur': ['coarse, scruffy fur', 'thick, glossy fur', 'patchy, uneven fur', 'short, bristly fur'],
    'scales': ['dull, dusty scales', 'rough, thick scales', 'blotchy, uneven scales', 'small, tight scales',
               'a few chipped scales at the brow'],
    'chitin': ['dull, scuffed chitin', 'glossy, dark chitin', 'uneven, lumpy chitin plates', 'pale streaks across the chitin'],
    'bark': ['rough, knotted bark', 'deeply furrowed bark', 'pale, peeling bark', 'bark with moss in the cracks'],
    'fungus': ['spongy, mottled skin', 'dry, powdery skin', 'damp, glistening patches on the skin'],
    'stone': ['rough, pitted stony skin', 'stony skin with pale veins', 'chalky, cracked stony skin'],
    'fish': ['dull, blotchy fish skin', 'pale, speckled fish skin', 'skin with a wet, oily gleam'],
    'synthetic': ['matte synthetic skin', 'synthetic skin a shade too even', 'synthetic skin with a scuff on one cheek'],
    'spirit': ['a faint, wavering outline', 'a blurred, smoky edge to the face', 'a cold, pale tint'],
    'slime': ['glistening, slimy skin', 'skin with drips of slime at the chin'],
    'leather': ['tough, creased leathery skin', 'thick, cracked leathery skin'],
    'waxy': ['greasy, waxy skin', 'lumpy, shiny skin'],
}

CHILD_SHAPES = ['a round, plain face', 'a thin, serious face', 'a long face', 'full cheeks', 'a pointed chin', 'a square little jaw',
                'a broad, flat face', 'a narrow face']
CHILD_SKINS = ['freckles', 'chapped red cheeks', 'a smudge of dirt on one cheek', 'a sunburnt nose', 'a few moles', 'pale skin',
               'wind-reddened cheeks']
CHILD_FEATURES = {
    'nose': ['a snub nose', 'a big nose for a child', 'a runny nose', 'a broad, flat nose', 'a long, thin nose'],
    'mouth': ['a gap where a milk tooth fell out', 'crooked new front teeth', 'chapped lips', 'a wide mouth', 'big front teeth',
              'a thin, serious mouth'],
    'eyes': ['small, narrow eyes', 'small, deep-set eyes', 'heavy-lidded eyes', 'close-set eyes'],
    'ears': ['ears that stick out', 'large ears'],
    'brows': ['heavy, straight brows', 'pale, sparse brows'],
}
BABY_SHAPES = ['a squashed, wrinkly little face', 'a round, blotchy face', 'heavy cheeks and a double chin', 'a long, narrow little face']
BABY_FEATURES = {'eyes': ['puffy eyelids', 'small, squinting eyes', 'small, sleepy eyes'],
                 'nose': ['a flat little nose', 'a broad, flat nose'],
                 'mouth': ['a toothless, gummy mouth', 'drool on the chin', 'a pouting lower lip'],
                 'skin': ['red, blotchy skin', 'a few milk spots on the nose', 'a red birthmark on the forehead']}

# A face's features: one to three, mostly one or two (the player: "Someone could have one, someone could have three").
FEATURE_COUNTS = [(1, 0.42), (2, 0.4), (3, 0.18)]

# What a kind's look sheet (or a look's words) already says about the face, so the features don't contradict it.
BLOCKS = [
    ('nose', r'\bnose|snout|muzzle|proboscis|beak|head of|-like head|head and face of|insect-like face|frog-like face|lumpy nose'),
    ('jaw', r'\bjaw|\bchin|mandibles|tentacles hanging|lower face|tusks|throat sac'),
    ('mouth', r'\bmouth|fangs|tusks|mandibles|barbels|tentacles|crooked teeth|\bsmile\b|\bgrin\b|blank expression'),
    ('eyes', r'\beyes?\b'),
    ('brows', r'\bbrows?\b|eyebrows'),
    ('shape', r'\bface\b|\bskull\b|cheek|-like head|\bhead of|brutish head'),
    ('ears', r'\bears?\b'),
    ('skin', r'\bskin\b|complexion|sheen|scales|\bfur\b|chitin|bark|growths|freckle'),
    ('marks', r'\bscars?\b|birthmark|blotchy'),
]
# A feature that names a part the kind's look already sets (a sunburnt nose on a kind with a snout) is left out.
SAID_BY = [('nose', 'nose'), ('mouth', 'lip'), ('mouth', 'teeth'), ('mouth', 'tooth'), ('ears', ' ear'), ('jaw', 'chin'),
           ('brows', 'brow'), ('eyes', 'eye')]
ANIMAL_HEAD = re.compile(r'head of|-like head|head and face of|insect-like face|wolf-like head')


def blocked(text):
    return {g for g, rx in BLOCKS if re.search(rx, text or '', re.I)}


def surface_of(look, features, skin_human):
    """What the kind's skin is: human, or fur, scales, chitin, bark, ... (SURFACES)."""
    t = (look or '').lower()
    for word, rx in [('synthetic', r'synthetic'), ('spirit', r'ghost'), ('slime', r'slim[ey]'),
                     ('bark', r'\bbark'), ('fungus', r'fung|mushroom'), ('stone', r'rocky|stony|stone'),
                     ('chitin', r"chitin|insect-like|insect's shell"), ('fish', r'\bfish'), ('leather', r'leathery'),
                     ('waxy', r'waxy|greasy'), ('scales', r'\bscales\b')]:
        if re.search(rx, t):
            return word
    if 'fur' in features or re.search(r'\bfur\b|furry|furred face', t):
        return 'fur' if (ANIMAL_HEAD.search(t) or 'fur' in features or not skin_human) else 'human'
    return 'human'


def _ok(item, age):
    return age in AGE_OK[item[2]]


def _word(rng, item, strong_share):
    return item[1] if item[1] and rng.random() < strong_share else item[0]


def face(rng, age, look, features, skin_human, far, strong_share, marks=(), extra_block=''):
    """The face as (base, [features]): its shape and skin, and one to three features of its own (a scar through the brow
    only with the scar mark). look is the kind's look sheet (with the row's look words, which may say more about the
    face); features the record's feature words; far a kind far from human in looks."""
    text = (look or '') + ' ' + (extra_block or '')
    blocks = blocked(text)
    animal = bool(ANIMAL_HEAD.search(look or ''))
    surface = surface_of(look, features, skin_human)
    if age in ('baby', 'toddler', 'child'):
        shapes = BABY_SHAPES if age == 'baby' else CHILD_SHAPES
        pools = BABY_FEATURES if age == 'baby' else CHILD_FEATURES
        base = [] if 'shape' in blocks or animal else [rng.choice(shapes)]
        if surface == 'human' and 'skin' not in blocks and age != 'baby':
            base.append(rng.choice(CHILD_SKINS))
        elif surface != 'human':
            base.append(rng.choice(SURFACES[surface]))
        groups = [g for g in pools if g not in blocks and not (animal and g in ('nose', 'mouth', 'skin'))
                  and not (far and g not in ('mouth', 'brows'))]
        n = 1 if age == 'baby' else (1 if rng.random() < 0.6 else 2)
        rng.shuffle(groups)
        feats = [rng.choice(pools[g]) for g in groups[:n]]
        if not feats:
            feats = [rng.choice(CHILD_FEATURES['ears' if 'ears' not in blocks else 'brows'])]
        if not base:
            base = [rng.choice(CHILD_SHAPES)]
        return ', '.join(base), feats
    # grown-ups and teens
    if animal:
        shapes = ANIMAL_SHAPES
        pools = {g: v for g, v in ANIMAL_FEATURES.items()
                 if not (g == 'muzzle' and 'snout' not in features) and not (g == 'coat' and surface != 'fur')
                 and not (g == 'ears' and not ({'longEars', 'pointedEars'} & set(features)))
                 and not (g in ('jaw', 'mouth', 'brows') and g in blocks)}
        shape_blocked = bool(re.search(r'\bskull\b|cheek pads', look or ''))
    else:
        shapes = FACE_SHAPES
        pools = {g: v for g, v in FEATURES.items() if g not in blocks}
        if far:
            pools.pop('eyes', None)        # a kind far from human keeps the eyes its kind has
        if surface not in ('human', 'fur', 'leather', 'waxy'):
            pools.pop('marks', None)       # no freckles or moles on scales, chitin, bark or synthetic skin
            if 'brows' in pools:
                pools['brows'] = ANIMAL_FEATURES['brows']
        shape_blocked = 'shape' in blocks
    base = []
    if not shape_blocked:
        base.append(_word(rng, rng.choice([s for s in shapes if _ok(s, age)]), strong_share))
    if surface == 'human' and 'skin' not in blocks:
        base.append(rng.choice([s for s, tag in SKINS if (skin_human or tag != 'hc')
                                and (tag not in AGE_OK or age in AGE_OK[tag])]))
    elif surface != 'human':
        base.append(rng.choice(SURFACES[surface]))
    if not base:
        base.append(_word(rng, rng.choice([s for s in (ANIMAL_SHAPES if animal else FACE_SHAPES) if _ok(s, age)]), strong_share))
    n, x = 1, rng.random()
    for c, w in FEATURE_COUNTS:
        x -= w
        if x <= 0:
            n = c
            break
    groups = [g for g in sorted(pools) if any(_ok(i, age) for i in pools[g])]
    rng.shuffle(groups)
    feats = []
    for g in groups:
        if len(feats) >= n:
            break
        items = [i for i in pools[g] if _ok(i, age) and (skin_human or 'sunburnt' not in i[0])
                 and not (g in ('marks', 'lines') and any(w in i[0] for b, w in SAID_BY if b in blocks))]
        if not items:
            continue
        if g == 'brows' and 'scar' in marks and not animal:
            items = items + [SCAR_BROW]
        feats.append(_word(rng, rng.choice(items), strong_share))
    if not feats:   # every group said by the kind already: one more for the shape or the skin
        feats.append(_word(rng, rng.choice([s for s in shapes if _ok(s, age)]), strong_share))
    if rng.random() < UNEVEN_SHARE and not animal and len(feats) < 3:
        uneven = [u for u in UNEVEN if not ('eye' in u[0] and 'eyes' in blocks) and not ('mouth' in u[0] and 'mouth' in blocks)
                  and not ('smile' in u[0] and 'mouth' in blocks)]
        if uneven:
            feats.append(rng.choice(uneven)[0])
    return ', '.join(base), feats


def face_sentence(base, feats, label='Face'):
    """'Face: a long face, weathered skin; a hawk nose; thin lips.' (the planner's tests read it back by its semicolons)."""
    return '%s: %s; %s.' % (label, base, '; '.join(feats))


# The adjectives for a short character line ("a sly, wiry, hawk-nosed old man"), where a feature has one.
FEATURE_ADJ = {
    'a hawk nose': 'hawk-nosed', 'a big, curved hawk nose': 'hawk-nosed', 'a lantern jaw': 'lantern-jawed',
    'a huge lantern jaw': 'lantern-jawed', 'heavy jowls': 'jowly', 'ears that stick out': 'jug-eared',
    'huge ears that stick right out': 'jug-eared', 'a gap between the front teeth': 'gap-toothed',
    'a wide gap between the front teeth': 'gap-toothed', 'thin lips': 'thin-lipped', 'heavy brows': 'heavy-browed',
    'very heavy, bristling brows': 'heavy-browed', 'freckles across the nose': 'freckled', 'thick freckles all over the face': 'freckled',
    'a long face': 'long-faced', 'a very long, horsey face': 'long-faced', 'a broken, slightly flattened nose': 'broken-nosed',
    'a badly broken, flattened nose': 'broken-nosed', 'a sharp, pointed nose': 'sharp-nosed', 'a long, sharp, beaky nose': 'beak-nosed',
    'a weak chin': 'weak-chinned', 'almost no chin at all': 'chinless', 'crooked teeth': 'crooked-toothed',
    'a big, fleshy nose': 'big-nosed', 'a very big, fleshy nose': 'big-nosed', 'a round, fleshy face': 'round-faced',
    'a very round, moon-like face': 'moon-faced', 'a gaunt face': 'gaunt', 'hollow cheeks': 'hollow-cheeked',
    'a square, blocky face': 'square-faced', 'a broad, flat face': 'flat-faced', 'a jutting chin': 'square-chinned',
    'an especially big, jutting chin': 'big-chinned', 'sun-browned skin': 'sun-browned', 'weathered skin': 'weathered',
    'ruddy cheeks': 'ruddy', 'a weather-beaten complexion': 'weather-beaten', 'deep-set eyes': 'deep-eyed',
}

# ---------------------------------------------------------------- hair, beards, builds (each said by the record's word)
# (phrase, ages) with {c} the colour; ages as AGE_OK. The record words follow PortraitLibraryMath.HairFromWords and
# Synonym: a mohawk, an undercut, cornrows, an afro or a buzz cut are short; dreadlocks long; a topknot a bun.

HAIR = {
    'short': [('shaved sides and a short {c} top', ''), ('an undercut, the {c} top swept over to one side', 'Y'),
              ('a {c} mohawk, the sides shaved', ''), ('a tight {c} crop', ''), ('a {c} buzz cut', ''),
              ('a receding {c} hairline, cut short', 'A'), ('slicked-back {c} hair, shiny with oil', ''),
              ('short {c} hair standing up in spikes', 'Y'), ('a flat-top of {c} hair', ''), ('{c} cornrows close to the scalp', ''),
              ('a big, round {c} afro', ''), ('short, tight {c} curls', ''), ('thinning {c} hair combed straight back', 'M'),
              ('a home-cut {c} fringe, cut crooked', ''), ('a {c} crew cut', ''), ('short {c} hair sticking up at the crown', '')],
    'medium': [('a curly {c} mop', ''), ('a shaggy {c} mop over the ears', ''), ('thick {c} curls to the jaw', ''),
               ('a {c} bob with a blunt, heavy fringe', ''), ('messy, wind-tangled {c} hair to the collar', ''),
               ('{c} hair parted sharply down the middle', ''), ('frizzy {c} hair standing out round the head', ''),
               ('wavy {c} hair tucked behind the ears', '')],
    'long': [('a wild, tangled {c} mane', ''), ('long {c} hair, one side shaved to the scalp', 'Y'),
             ('{c} dreadlocks to the shoulders', ''), ('long, lank {c} hair hanging straight', ''),
             ('long {c} hair with one thin braid at the temple', ''), ('long, frizzy {c} hair', ''),
             ('long {c} hair swept back off the forehead', ''), ('long, straggly {c} hair', 'M')],
    'braids': [('a long single {c} braid over one shoulder', ''), ('two thick {c} braids', ''),
               ('many thin {c} braids gathered at the back', ''), ('a {c} braid wound round the head', '')],
    'tied': [('a messy {c} ponytail', ''), ('a high, tight {c} ponytail', ''), ('{c} hair in two short pigtails', 'Y'),
             ('{c} hair tied back with a strip of cloth, loose strands everywhere', ''), ('a low {c} ponytail', '')],
    'bun': [('a {c} topknot', ''), ('a tight {c} bun at the nape', ''), ('a messy {c} bun with strands falling out', ''),
            ('a {c} half-up knot, the rest loose', '')],
    'bald': [('a bald head, shaved to a shine', ''), ('a bald, sunburnt scalp', ''), ('a shaved head with a day of stubble on the scalp', '')],
}
# Kinds that grow no hair at all (their only hair colour is none): a bald head of their own.
HAIRLESS = ['a bald head', 'a bald, bumpy scalp', 'a bare, lumpy scalp']
LITTLE_HAIR = {
    'baby': [('a soft fuzz of {c} hair', 'short'), ('a single {c} curl on top', 'short'), ('a few wild tufts of {c} hair', 'short')],
    'toddler': [('a {c} cowlick sticking straight up', 'short'), ('fine {c} curls', 'short'), ('a wispy little {c} topknot', 'bun'),
                ('short, fine {c} hair', 'short')],
    'child': [('a {c} mop that falls in the eyes', 'medium'), ('a scruffy {c} crop', 'short'), ('{c} hair in two lopsided bunches', 'tied'),
              ('a long {c} plait', 'braids'), ('a home-cut {c} fringe, cut crooked', 'short'), ('wild {c} curls', 'medium'),
              ('{c} hair sticking up at the back', 'short'), ('long, tangled {c} hair', 'long')],
}
BEARDS = {
    'full': ['a bushy {c} beard', 'a braided {c} beard', 'a long forked {c} beard', 'a thick {c} beard down to the chest',
             'thick {c} mutton chops', 'a wild, untrimmed {c} beard', 'a square-cut {c} beard'],
    'short': ['a pointed {c} goatee', 'a neat {c} chin beard', 'a short, patchy {c} beard', 'a {c} soul patch', 'a short {c} beard trimmed square'],
    'moustache': ['a {c} handlebar moustache', 'a drooping {c} walrus moustache', 'a thin {c} pencil moustache', 'a bristly {c} moustache'],
    'stubble': ["a few days' {c} stubble", 'patchy {c} stubble', 'heavy {c} stubble'],
    'none': ['clean-shaven'],
}
SIDEBURNS = {'full': 'thick {c} sideburns', 'short': 'short {c} sideburns'}
# (phrase, the adjective for the character line)
BUILDS = {
    'slim': [('wiry', 'wiry'), ('bony and long-limbed', 'bony'), ('skinny, all elbows', 'skinny'), ('lanky', 'lanky'),
             ('narrow-shouldered and thin', 'thin'), ('lean and stringy', 'stringy')],
    'average': [('of middling build', ''), ('compact and square-shouldered', 'compact'), ('a little round-shouldered', ''),
                ('sturdy', 'sturdy'), ('soft around the middle', 'soft-bellied')],
    'heavy': [('with a big belly', 'big-bellied'), ('heavy-set and soft', 'heavy-set'), ('barrel-bellied', 'barrel-bellied'),
              ('plump, with a thick waist', 'plump'), ('stout and round', 'stout'), ('big and fleshy', 'fleshy')],
    'broad': [('barrel-chested', 'barrel-chested'), ('stocky and thick-armed', 'stocky'), ('broad-shouldered, with a thick neck', 'broad'),
              ('heavily muscled', 'brawny'), ('big-boned and broad', 'big-boned'), ('thick-set and powerful', 'thick-set')],
}
LITTLE_BUILDS = {'slim': [('small and skinny', 'skinny'), ('gangly', 'gangly')], 'average': [('sturdy', 'sturdy'), ('small for the age', 'small')]}

# ---------------------------------------------------------------- moods: an expression and a pose that fit it
# Poses hold nothing (a plain row's hands are empty) and fit the waist-up frame. (expressions, poses, the adjective)

MOODS = {
    'sly': (['a sly, sidelong grin', 'a crooked, knowing smile', 'a smug half-smile', 'one eyebrow cocked and a sly smirk'],
            ['leaning in a little, as if sharing a secret', 'turned half away, glancing back over the shoulder',
             'tapping the side of the nose with one finger', 'arms loosely folded, the head cocked'], 'sly'),
    'gleeful': (['a gleeful, toothy grin', 'a cheeky wink', 'a big, goofy grin', 'a wide, open-mouthed laugh'],
                ['a hand pressed to the chest, laughing', 'leaning back, laughing', 'a thumb jabbed back over the shoulder',
                 'both thumbs hooked in the belt, grinning'], 'cheeky'),
    'sullen': (['a sullen pout', 'a sulky glare from under the brows', 'a sour, down-turned mouth'],
               ['slouching, hands jammed in the pockets', 'arms folded tight, the shoulders hunched',
                'the head lowered, looking up from under the brows'], 'sullen'),
    'haughty': (['a haughty look down the nose', 'pursed lips and a cold, disapproving look', 'one eyebrow raised in disdain'],
                ['the chin raised high', 'hands clasped behind the back, very upright', 'turned a little away, looking back coolly'], 'haughty'),
    'shy': (['a shy half-smile', 'a timid look from under the brows', 'a nervous, toothy smile', 'a sheepish look, the eyes cast aside'],
            ['fiddling with a sleeve', 'the shoulders drawn in, hands clasped at the waist', 'half turned away, peeking back'], 'shy'),
    'intense': (['an intense, burning stare', 'a fierce glare', 'a hard, narrow-eyed look', 'a stubborn set to the jaw'],
                ['leaning forward, staring straight out', 'standing square, fists on the hips', 'arms crossed, the chin lowered'], 'intense'),
    'weary': (['a tired, faraway look', 'a wry, tired smile', 'a dazed, sleepy look', 'a long-suffering look'],
              ['rubbing the back of the neck', 'the shoulders sagging, one hand on the hip', 'a hand half covering a yawn'], 'weary'),
    'eccentric': (['an eccentric, lopsided grin', 'a dreamy, distracted look', 'a wild, delighted look', 'a puzzled frown, the head tilted'],
                  ['arms spread wide mid-sentence', 'a finger raised as if about to make a point', 'the head tilted far to one side',
                   'counting something off on the fingers'], 'odd'),
    'gruff': (['a deep scowl', 'a gruff, unimpressed look', 'a scowling squint', 'a flat, bored stare'],
              ['arms folded across the chest', 'thumbs hooked in the belt', 'scratching the jaw', 'hands jammed in the pockets'], 'gruff'),
    'warm': (['a broad, friendly grin', 'an amused smile that crinkles the eyes', 'a warm, easy smile'],
             ['one hand raised in a wave', 'a hand resting on the opposite shoulder', 'leaning in a little, smiling'], 'friendly'),
    'proud': (['a proud, defiant look', 'a self-satisfied smirk', 'a confident grin'],
              ['chest puffed out, thumbs in the belt', 'the chin up, hands on the hips'], 'proud'),
    'worried': (['a worried frown', 'a fretful, pinched look', 'chewing the lower lip'],
                ['hands clasped tight at the chest', 'one hand at the collar, fidgeting'], 'anxious'),
    'stoic': (['a solemn, steady look', 'a calm, unreadable face', 'a stony, patient look'],
              ['standing very straight, hands at the sides', 'hands folded at the waist'], 'stoic'),
    'droll': (['a deadpan stare', 'a sheepish, guilty grin', 'a mock-serious frown'],
              ['one finger raised, about to object', 'a shrug, one shoulder up'], 'droll'),
    'suspicious': (['a suspicious squint', 'a narrow, doubtful look', 'a wary sidelong look'],
                   ['leaning back, arms crossed', 'peering sidelong, the head drawn back'], 'wary'),
}
# How often each mood comes up: the commonest kinds a little more often calm, the rarer ones more often odd or intense
# (the framework: "the rarer kinds and the later rounds get the widest range").
MOOD_WEIGHTS_COMMON = {'warm': 1.1, 'stoic': 1.0, 'weary': 1.2, 'gruff': 1.2, 'sly': 1.0, 'gleeful': 1.0, 'sullen': 1.0,
                       'haughty': 0.8, 'shy': 1.0, 'intense': 1.0, 'eccentric': 1.0, 'proud': 0.8, 'worried': 0.8, 'droll': 1.0,
                       'suspicious': 1.0}
MOOD_WEIGHTS_RARE = {'warm': 0.7, 'stoic': 0.7, 'weary': 0.8, 'gruff': 1.1, 'sly': 1.4, 'gleeful': 1.2, 'sullen': 1.0,
                     'haughty': 1.2, 'shy': 0.9, 'intense': 1.5, 'eccentric': 1.6, 'proud': 1.0, 'worried': 0.8, 'droll': 1.2,
                     'suspicious': 1.1}
# A kind whose look sheet already sets its face keeps to moods that fit it (a blank android never grins).
KIND_MOODS = [
    (r'blank expression', ['stoic']),
    (r'miserable', ['sullen', 'weary', 'worried', 'shy', 'suspicious']),
    (r'sly grin', ['sly', 'gleeful', 'eccentric', 'suspicious']),
    (r'regal', ['haughty', 'proud', 'stoic', 'intense', 'weary']),
    (r'calm|gentle', ['warm', 'stoic', 'weary', 'shy', 'eccentric', 'droll']),
    (r'hard|fierce|intimidating|unflinching|wild stare|soldier', ['intense', 'gruff', 'sly', 'sullen', 'proud', 'suspicious',
                                                                 'stoic', 'haughty', 'droll']),
    (r'watchful', ['sly', 'haughty', 'proud', 'stoic', 'intense', 'warm']),
]
# Looks a kind's own look sheet says the opposite of (a gourmand's full belly on a waster's gaunt face): not given it.
LOOK_CONFLICTS = {'gourmand': r'gaunt|thin|slight|slender', 'bigBoned': r'thin|slight|slender|wiry', 'runner': r'heavy|massive|brutish|stocky',
                  'nimble': r'heavy|massive|brutish|stocky', 'tough': r'gaunt|slight|thin', 'gaunt': r'heavy|stocky|massive',
                  'sickly': r'heavy|massive', 'neat': r'filthy|matted', 'kind': r'blank|miserable|hard,|fierce',
                  'cheerful': r'miserable|blank|hard,|fierce', 'gloomy': r'blank', 'ugly': r'beautiful|attractive'}
CHILD_EXPRESSIONS = [('a gap-toothed grin', 'grinning'), ('a sulky pout', 'sulky'), ('a cheeky, crooked grin', 'cheeky'),
                     ('a fierce little scowl', 'scowling'), ('a solemn, suspicious stare', 'solemn'), ('a shy look from under the brows', 'shy'),
                     ('a mischievous smirk', 'mischievous'), ('the tongue stuck out', 'cheeky'), ('a wobbly lower lip, close to tears', 'tearful'),
                     ('a proud, chin-up look', 'proud'), ('a bored, sleepy look', 'bored'), ('a big, delighted grin', 'delighted')]
CHILD_POSES = ['hands stuffed in the pockets', 'arms folded in a sulk', 'hands on the hips like a grown-up', 'fists balled at the sides',
               'hands behind the back', 'twisting the hem of the shirt', 'leaning forward, about to run off', 'rubbing one eye with a fist',
               'chest puffed out', 'slumped, arms hanging', 'clapping the hands together']
TODDLER_EXPRESSIONS = ['a gummy grin', 'a grumpy scowl', 'a solemn stare', 'a wail, the mouth wide open', 'a puzzled frown', 'a delighted shriek']
TODDLER_POSES = ['reaching both arms up to be lifted', 'a thumb in the mouth', 'clutching the hem of the shirt', 'pointing at something out of view',
                 'arms out for balance, mid-wobble', 'both fists raised in delight']
BABY_EXPRESSIONS = ['a gummy grin', 'a grumpy, scrunched-up face', 'a wide yawn', 'a solemn stare', 'a red-faced wail', 'a dribbly smile']
BABY_POSES = ['wrapped up, one small fist free', 'propped up in a blanket, reaching toward the viewer', 'chewing on one fist',
              'both small fists raised']

# ---------------------------------------------------------------- clothes with character, by the age of dress and wealth
# 'f:' only for women and girls. Nothing a skill needs (no tools, weapons, books or armour), nothing on the head.

CLOTHES = {
    'tribal': {
        'work': ['a patched hide vest over bare arms', 'a fur-lined hide coat with the sleeves cut off', 'a woven-grass poncho tied with cord',
                 'a beaded hide vest', 'a wolf-fur cloak over a plain hide tunic', 'a hide tunic with mismatched patches of fur',
                 'a long fringed buckskin shirt', 'a woven shawl wound high round the neck over a hide tunic'],
        'poor': ['a frayed hide wrap held together with cord', 'a hide tunic stitched up many times with sinew'],
        'rags': ['torn hides held on with knotted cord', 'a moth-eaten fur cape far too big'],
        'fine': ['a cloak of glossy black fur and strings of coloured beads', 'a wide beaded collar over a fine hide tunic',
                 'a dyed hide tunic with a mantle of thick fur'],
        'robes': ['a long woven robe dyed in faded ochre stripes'],
    },
    'medieval': {
        'work': ['a patched wool coat with mismatched buttons', 'a too-big wool coat with the sleeves rolled back',
                 'a linen shirt with the sleeves rolled to the elbow under a leather vest', 'a quilted wool doublet gone shiny at the elbows',
                 'a cloak with the hood down over a plain tunic', 'a scarf wound high round the neck over a wool tunic',
                 'a striped wool tunic', 'a sheepskin vest over a linen shirt', 'f:a laced bodice over a linen shift',
                 'f:a wool dress with a knitted shawl'],
        'poor': ['a darned wool tunic out at the elbows', 'a patched linen shirt and a frayed vest'],
        'rags': ['a threadbare tunic in tatters', 'a sackcloth smock tied with rope'],
        'fine': ['a slashed velvet doublet with puffed sleeves', 'a fur-lined cloak pinned with a silver brooch',
                 'f:a brocade gown with long hanging sleeves', 'a green velvet coat with brass buttons'],
        'robes': ['a long undyed wool robe tied with a cord'],
    },
    'industrial': {
        'work': ['a patched canvas coat with mismatched buttons', 'a hand-knitted jumper with holes at the elbows',
                 'a waistcoat over a collarless shirt, the sleeves rolled over bare forearms', 'a long oilskin coat and a scarf wound high',
                 'a sheepskin-lined jacket with the collar turned up', 'a loud checked shirt under braces',
                 'a corduroy jacket with leather elbow patches', 'a hand-knitted cardigan buttoned wrong', 'f:a print dress under a cardigan',
                 'a denim jacket covered in sewn-on patches', 'a sleeveless vest over bare, sunburnt arms', 'a short leather jacket with a fur collar'],
        'poor': ['a stained, patched overcoat', 'a darned jumper and a frayed collar'],
        'rags': ['a holed overcoat tied shut with string', 'layers of torn shirts'],
        'fine': ['a three-piece suit with a watch chain', 'a long camel coat over a silk scarf', 'a velvet smoking jacket',
                 'f:a tailored coat dress with a fur collar'],
        'robes': ['a long dark coat buttoned to the throat'],
    },
    'spacer': {
        'work': ['a sleeveless synthetic vest over bare arms', 'a padded jumpsuit unzipped to the waist, the sleeves knotted round the hips',
                 'a high-collared synthetic jacket in two clashing colours', 'a quilted flight jacket covered in patches',
                 'a mesh-sleeved top under an open vest', 'a slick rain-shell jacket with the collar up',
                 'a utility jumpsuit with too many pockets', 'an oversized synthetic jumper', 'f:a cropped synthetic jacket over a ribbed top'],
        'poor': ['a scuffed jumpsuit patched at the knees and elbows', 'a faded synthetic jacket with a split seam'],
        'rags': ['a scuffed, patched-up synthetic suit with mismatched sleeves', 'a torn thermal suit and a frayed blanket'],
        'fine': ['a sharply cut asymmetric coat with a stiff high collar', 'a pearl-grey suit with fine geometric trim',
                 'f:a long fitted coat in deep blue with silver trim'],
        'robes': ['flowing synthetic robes in a muted colour'],
    },
}
ACCESSORIES = {
    'tribal': ['a string of carved wooden beads', 'a necklace of shells', 'a copper armband', 'a strip of red cloth tied round one arm'],
    'medieval': ['a small brass ring in one ear', 'a wooden bead necklace', 'a copper bangle', 'a chipped pewter brooch'],
    'industrial': ['a small ring in one ear', 'a silver chain at the neck', 'a battered wristwatch', 'a row of small rings along one ear'],
    'spacer': ['a row of small metal studs along one ear', 'a thin metal ring at the throat', 'mismatched earrings', 'a small hoop through one brow'],
}
WEALTH_WORDS = {'destitute': 'filthy and ragged', 'poor': 'worn and mended', 'common': '', 'prosperous': 'clean and well kept',
                'noble': 'with a little gold jewellery'}
LITTLE_CLOTHES = {
    'tribal': ['a little hide tunic tied with cord', 'a small fur-trimmed hide coat', 'a hide smock with a bead at the collar'],
    'medieval': ['a hand-me-down wool tunic far too big', 'a little linen shirt with a patched collar', 'f:a little wool dress with a torn hem'],
    'industrial': ['a hand-knitted jumper with a hole in one elbow', 'a too-big flannel shirt with the sleeves rolled', 'a muddy little coat'],
    'spacer': ['a small padded jumpsuit in a loud colour', 'a little synthetic jacket two sizes too big', 'a scuffed child\'s jumpsuit'],
}
BABY_WRAPS = {'tribal': ['a soft fur wrap'], 'medieval': ['a patched wool blanket'], 'industrial': ['a knitted blanket with a lopsided pattern'],
              'spacer': ['a padded synthetic sleep sack']}

# How life has treated them, in a line (no beauty words; 'sheltered' only for the young).
HARDSHIP = {
    'sheltered': ['Unlined skin and a rested, well-fed look.', 'A clean, rested look.'],
    'ordinary': ['Lightly weathered.', 'A little sun-browned.', 'A plain, everyday look.'],
    'hard': ['Wind-burned, weathered skin and tired eyes.', 'Sun-browned, dry, creased skin.', 'Chapped lips and wind-reddened cheeks.',
             'Grime in the creases of the face.'],
    'brutal': ['Deep lines and a hard stare.', 'A hard-worn, weather-beaten face.', 'Hollow cheeks and cracked lips.'],
}
FAR_HARDSHIP = {'sheltered': ['A well-kept, well-fed look.'], 'ordinary': ['A little travel-worn.'],
                'hard': ['A worn, weather-beaten look.', 'Dust in every crease.'], 'brutal': ['A hard, hungry look.', 'A rough, half-starved look.']}
