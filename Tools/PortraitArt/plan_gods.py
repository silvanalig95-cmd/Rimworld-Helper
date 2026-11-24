"""
The gods' picture lists (docs/gods-altars-proposals.md, part C; the player, 24 November 2026: "later I want to create
some images as well with the scripts"). Three lists in docs/portrait-art/batches:

- backdrops-gods.csv (C1): a backdrop of a god's place for each god's domain in each age: a shrine of the waters by the
  shore, the hearth god's chapel, the machine god's terminal. Its column "god" names the domain; the game shows it behind
  someone praying at a god's place of that domain, or standing in its temple, and nowhere else.
- gods-01.csv (C2, C3): the gods as they are seen when they walk the map, each in each age's dress, and the beasts that
  follow a god that walks as a beast. Written as creature rows (painted in the creatures' style: the whole figure, cut
  out); the "creature" column names what the picture stands for, as the game looks it up (CreaturePortraitMath.GodKeys):
  god:<Key>:<age> for a god of its own (the Old Horn, the great cat, the Guest ...), god:<domain>:<age> for any other
  god of that domain shown in its look, godbeast:<Key> for the beasts that walk with it.
- priests-01.csv (C4): priests in each age's dress for the common kinds of people (role priest), through the people
  lists' own planner (plan_batches.Planner), so their names, faces and records are made as every person's are. The game
  reads the colony's own priest as a priest too.

Running again writes nothing that is there already (rows are found by name).

    python3 Tools/PortraitArt/plan_gods.py [--dry-run]
"""
import argparse
import csv
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import plan_batches as P  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(HERE))
BATCHES = os.path.join(ROOT, 'docs', 'portrait-art', 'batches')
ERAS = ['tribal', 'medieval', 'industrial', 'spacer']
BACKDROP_COLUMNS = ['name', 'subject', 'count', 'seed', 'place', 'rooms', 'outdoors', 'era', 'contents', 'god']
GOD_COLUMNS = ['name', 'subject', 'pose', 'count', 'seed', 'creature', 'sex', 'age', 'mod']

# ---------------- C1: the gods' places, by domain and age ----------------

# place, rooms or land, contents, and the place in each age (tribal, medieval, industrial, spacer).
PLACES = {
    'harvest': ('outdoors', 'grassland', 'altar', [
        'a ring of standing stones at the edge of a field of ripe grain, sheaves leaned against the largest stone, a carved wooden figure of the harvest among them',
        'a small stone shrine at the edge of a wheat field, a carved harvest figure in its niche, sheaves and baskets of fruit laid before it',
        'a brick wayside chapel among cornfields, a painted harvest figure over its door, sacks of grain and baskets of apples on its steps',
        'a clean white shrine among bright greenhouse fields, a holographic figure of ripe grain over its altar, crates of produce laid before it']),
    'storm': ('outdoors', 'mountain', 'altar', [
        'a cairn of dark stones on a bare hilltop under heavy storm clouds, a lightning-split tree beside it, feathers tied to poles',
        'a stone shrine on a windswept crag under storm clouds, an iron rod on its roof, a carved storm figure in its niche',
        'an iron lightning mast on a hilltop with a small brick shrine at its foot, storm clouds rolling in, flags snapping',
        'a slender metal spire on a ridge crackling with stored lightning, a weather terminal glowing at its base, storm clouds above']),
    'hunt': ('outdoors', 'temperateForest', 'altar', [
        'a forest clearing with a stone slab under an old tree, antlers and skulls hung from its branches, a fire pit before it',
        'a hunting shrine of rough timber in a forest clearing, antlers over its door, a carved stag figure inside',
        'a hunters\' lodge chapel in the woods, mounted antlers and trophies on its walls, a plain altar with a rifle laid across it',
        'a forest shrine of dark composite with a holographic stag over it, trophies on its walls, a field of tall trees around']),
    'healing': ('room', 'Hospital', 'altar;medicine;window', [
        'inside a hide tent of healing, bundles of herbs hanging, a stone altar with a clay bowl of water, a small fire',
        'a small whitewashed chapel of healing, an altar with candles and a basin of water, jars of herbs on shelves',
        'a hospital chapel with tiled walls, an altar with candles beside a row of clean beds, a window letting in pale light',
        'a bright white sanctuary of healing, a glowing altar beside a medical pod, soft green light']),
    'war': ('room', 'WorshipRoom', 'altar;banners', [
        'a war lodge of timber and hide, spears and shields hung on its posts, an altar of piled stones with a painted war mask',
        'a stone chapel of war, banners and shields on its walls, an altar with a sword laid across it, candles',
        'a garrison chapel with flags and old rifles on its walls, an altar draped in a regimental banner',
        'a dark plasteel war shrine lit with red light, armour displayed in alcoves, an altar with a glowing blade']),
    'craft': ('room', 'Workshop', 'altar;workbench;tools;fire', [
        'a smithing hut with an anvil stone used as an altar, tools hung on its posts, a glowing forge fire',
        'a forge chapel of stone, an anvil before a carved figure of the smith god, tools on the walls, a roaring forge',
        'a foundry chapel of brick and iron, an altar of cast iron beside the furnace, tools in neat racks, sparks',
        'a clean fabrication shrine, a glowing altar beside a fabrication bench, tools in lit racks, cyan light']),
    'way': ('outdoors', 'grassland', 'altar', [
        'a cairn of stones at a crossroads of dirt paths, ribbons tied to a pole on top, offerings of food on the stones',
        'a roadside shrine of stone at a crossroads, a carved traveller in its niche, a well beside it, coins left on the ledge',
        'a small roadside chapel by a railway line, a painted traveller over its door, a bench for those waiting',
        'a waystation shrine by a landing pad, a holographic traveller over its altar, a road of light leading away']),
    'dead': ('room', 'Tomb', 'altar;pillars', [
        'a burial cave lit by a small fire, bones and grave goods set in niches, an altar stone with a skull on it',
        'a stone crypt with pillars and stone coffins, candles on an altar, names carved into the walls',
        'a funeral chapel of dark stone, rows of memorial plaques, an altar with candles and wreaths',
        'a quiet memorial vault of polished stone, rows of glowing name plates, an altar with a pale light']),
    'night': ('outdoors', 'grassland', 'altar', [
        'a ring of pale stones on a hill under a full moon and stars, no fire, a carved moon disc on the centre stone',
        'a small stone shrine on a hill at night under the moon, a silver moon disc over its door, no lamps',
        'an observatory chapel at night, its dome open to the moon and stars, a simple altar under the opening',
        'a dark shrine on a hill under a moon and a starry sky, a softly glowing silver disc over its altar']),
    'hearth': ('room', 'DiningRoom', 'altar;hearth;fire;table', [
        'inside a round hut, a central fire pit with a carved hearth stone, pots and bowls around it, warm firelight',
        'a small chapel with a great stone hearth, a fire burning, an altar with bread and a cup, warm light',
        'a parlour chapel with an iron stove, a family table set with bread, an altar shelf with candles',
        'a warm sanctuary with a glowing heater-hearth, a set table, an altar with a lit lamp']),
    'wild': ('outdoors', 'temperateForest', 'altar', [
        'a moss-covered stone among huge old trees, vines and flowers grown over it, carved spirals on the stone',
        'a grove shrine of living trees bent into an arch, a mossy stone altar, wild flowers',
        'an old greenhouse chapel overgrown with plants, a stone altar under vines, light through broken glass',
        'a sanctuary of living trees under a clear dome, a glowing seed set on a mossy altar']),
    'waters': ('outdoors', 'coast', 'altar', [
        'a shrine of driftwood and shells on a rocky shore, nets hung on poles, waves breaking behind it',
        'a small stone chapel on a sea cliff, a carved wave figure in its niche, candles, boats below',
        'a harbour chapel of brick on a quay, anchors and ropes by its door, a lighthouse beyond',
        'a white shrine on a pier over clear water, a holographic wave over its altar, a sea of light behind']),
    'machine': ('room', 'Laboratory', 'altar', [
        'an ancient metal pillar half buried in a cave, its strange markings touched with paint, offerings laid before it',
        'a chapel around a strange old machine taken from ruins, candles before it, its surface engraved with prayers',
        'a workshop chapel built around a humming old generator, cables like garlands, an altar of steel',
        'a terminal sanctuary, a tall glowing archotech terminal for an altar, cables and cyan light, a clean metal floor']),
    'void': ('room', 'WorshipRoom', 'altar;pillars', [
        'a cave of black rock with a crude altar, ritual masks on its walls, a single dim fire, deep shadows',
        'a dark stone chapel with no windows, black candles on an altar, a carved mask over it',
        'a cellar chapel lit by a single bulb, strange symbols painted on brick walls, an altar of black stone',
        'a sealed chamber of dark metal lit violet, a black monolith for an altar, the air still']),
}

# ---------------- C2: the gods seen walking the map ----------------

# Each god of its own that can be seen in a body (Defs/GodDefs/Rimfolk_Gods.xml, howSeen): what is seen, and its dress by age.
OWN = {
    'Bellwether': ('a huge old thrumbo, the first of all thrumbos, with a long curved horn, calm old eyes, and grass growing greener at its feet', False),
    'Mog': ('a great cat the colour of desert sand, larger than any lion, with gold eyes and a collar of lapis and gold', False),
    'Murmur': ('a toad the size of a house, mottled and old, with eyes like wet stones, half risen out of a marsh', False),
    'Hush': ('a tall, thin white figure standing still, frost spreading on the ground around it, its face hard to see', True),
    'Ennu': ('a traveller in a worn cloak, ordinary at first look, who leaves no footprints and whose eyes hold a quiet light', True),
    'Revel': ('a man with goat\'s horns and a cup in his hand, cheerful and a little unsteady, smoke curling about him', True),
    'Hollin': ('a tall figure risen out of the ground, made of roots and earth, flowers opening where the roots touch', True),
    'Smoulder': ('a vast dragon of dark scales with ember-red cracks, coiled half out of a mountain, smoke rising from its nostrils', False),
}

# The domains' looks (Defs/GodDefs/Rimfolk_GodLooks.xml): the figure any other god of the domain is seen as.
LOOKS = {
    'harvest': 'a tall figure in wheat-gold and green, crowned with a wreath, flowers springing where it walks',
    'storm': 'a tall figure in storm grey and silver, its cloak moving in a wind nobody else feels',
    'hunt': 'a tall hunter in forest green and bone, wearing a deer\'s skull, spirit wolves at its side',
    'healing': 'a serene figure in warm white and pale green, a soft light around its hands',
    'war': 'a helmed and cloaked figure in blood red and iron, the ground scorched behind it',
    'craft': 'a figure of the forge in ember orange and bronze, soot-dark skin, fire in its eyes, sparks around it',
    'way': 'a wanderer in a dusty travelling cloak, the open sky in its eyes, a staff in its hand',
    'dead': 'a shrouded, cowled figure in bone white and ash grey, faint ghosts rising off it',
    'night': 'a moon-pale figure in night blue and silver, frost forming where it stands',
    'hearth': 'a flame-haired figure in ember amber and warm red, small flames rising off it',
    'wild': 'a bark-skinned, moss-haired figure crowned like a shaman, grass springing at its feet',
    'waters': 'a veiled figure in sea blue and foam white, leaving wet footprints of light',
    'machine': 'a chrome-pale figure of steel and cyan light, its face calm and blank',
    'void': 'a figure in black and violet with pale eyes and a ritual mask, shadow pooling at its feet',
}
DRESS = {
    'tribal': 'Its dress and ornaments in a tribal style: furs, bone, feathers and painted marks.',
    'medieval': 'Its dress and ornaments in a medieval style: long robes, a crown or circlet, embroidered cloth.',
    'industrial': 'Its dress and ornaments in an industrial style: a long coat, brass and iron fittings.',
    'spacer': 'Its dress and ornaments in a spacer style: smooth fabric with fine glowing lines, a halo of light.',
}
# The machine before the machine age looks like old ruins' iron (Rimfolk_GodLook_Machine_Old).
MACHINE_OLD = 'a figure of rust and bronze like the old ruins, its face a mask'

# ---------------- C3: the beasts that walk with a god ----------------

BEASTS = {
    'Bellwether': ['A thrumbo that follows the Old Horn at a distance like a pilgrim, calm and solemn, a garland of grass caught on its horn.',
                   'A young thrumbo walking behind the Old Horn, its eyes on him, its coat clean and bright.'],
    'Mog': ['A sand-coloured lion that walks with the great cat, gold-eyed and quiet, a thin gold band around its neck.',
            'A lean lioness that keeps by the great cat at the graves, watchful, her coat the colour of desert sand.'],
}


def read_csv(path):
    if not os.path.exists(path):
        return []
    with open(path, encoding='utf-8', newline='') as f:
        return list(csv.DictReader(f))


def write_csv(path, columns, rows):
    with open(path, 'w', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=columns, lineterminator='\n')
        w.writeheader()
        for r in rows:
            w.writerow(r)


def backdrop_rows():
    rows = []
    for domain, (place, where, contents, ages) in PLACES.items():
        for era, text in zip(ERAS, ages):
            subject = ('Indoors: ' if place == 'room' else 'Outdoors: ') + text + '. A place of worship of a god of ' + \
                      {'harvest': 'the harvest', 'storm': 'the storm', 'hunt': 'the hunt', 'healing': 'healing', 'war': 'war',
                       'craft': 'the forge', 'way': 'roads and travellers', 'dead': 'the dead', 'night': 'the night and the moon',
                       'hearth': 'the hearth', 'wild': 'the green and growing things', 'waters': 'the waters',
                       'machine': 'the machine', 'void': 'the void'}[domain] + '. No people.'
            rows.append({
                'name': 'b_god_%s_%s' % (domain, era), 'subject': subject, 'count': '', 'seed': '', 'place': place,
                'rooms': where if place == 'room' else '', 'outdoors': where if place == 'outdoors' else '', 'era': era,
                'contents': contents, 'god': domain,
            })
    return rows


def god_rows():
    rows = []
    for key, (look, person) in OWN.items():
        for era in ERAS:
            subject = 'A god walking among people, seen plainly: %s.' % look
            if person:
                subject += ' ' + DRESS[era]
            pose = 'Standing still, turned three-quarters toward the viewer, a faint light around it.' if person else \
                'Its whole body in view, turned three-quarters toward the viewer, a faint light around it.'
            rows.append({'name': 'g_%s_%s' % (key.lower(), era), 'subject': subject, 'pose': pose, 'count': '', 'seed': '',
                         'creature': 'god:%s:%s' % (key, era), 'sex': 'any', 'age': 'adult', 'mod': 'rimfolk'})
    for domain, look in LOOKS.items():
        for era in ERAS:
            figure = MACHINE_OLD if domain == 'machine' and era in ('tribal', 'medieval') else look
            subject = 'A god of %s walking among people, seen plainly: %s. %s' % (domain, figure, DRESS[era])
            rows.append({'name': 'g_%s_%s' % (domain, era), 'subject': subject,
                         'pose': 'Standing still, turned three-quarters toward the viewer, a faint light around it.', 'count': '', 'seed': '',
                         'creature': 'god:%s:%s' % (domain, era), 'sex': 'any', 'age': 'adult', 'mod': 'rimfolk'})
    for key, texts in BEASTS.items():
        for i, text in enumerate(texts, 1):
            rows.append({'name': 'g_beast_%s_%02d' % (key.lower(), i), 'subject': text,
                         'pose': 'Its whole body in view, turned three-quarters toward the viewer.', 'count': '', 'seed': '',
                         'creature': 'godbeast:%s' % key, 'sex': 'any', 'age': 'adult', 'mod': 'rimfolk'})
    return rows


def priest_rows(batches):
    """Priests in each age's dress for the common kinds (each kind, sex and age a priest picture in every age)."""
    kinds, looks, marks, roles = P.load(P.CATALOGUE)
    ledger = P.Ledger(kinds)
    P.read_batches(batches, ledger)
    planner = P.Planner(kinds, looks, marks, roles, ledger, 0)
    have = {r['name'] for f in os.listdir(batches) if f.startswith('priests-') for r in read_csv(os.path.join(batches, f))}
    rows = []
    for k in sorted((k for k in kinds.values() if k.plannable and k.weight >= P.ROLE_MIN_WEIGHT), key=lambda k: (-k.weight, k.kind)):
        ages = planner.role_ages(k)
        if not ages:
            continue
        for sex in k.sexes:
            for i, era in enumerate(ERAS):
                age = ages[i % len(ages)]
                pairs = k.spread(age)
                pair = pairs[(i * 3 + (1 if sex == 'female' else 0)) % len(pairs)]
                tag = 'priest' + era[:3]
                stem = 'p_%s_%s_%s_%s_%s_%s' % (k.slug, 'm' if sex == 'male' else 'f', age, tag, pair[0][0], pair[1][0])
                if any(n.startswith(stem + '_') for n in have):
                    continue
                planner.make('roles', k.wave, k, sex, age, pair, role='priest', era=era, tag=tag)
    for _, _, r in planner.rows:
        rows.append(r)
    return rows


def plan(batches=BATCHES, dry=False):
    out = []
    for path, columns, rows in ((os.path.join(batches, 'backdrops-gods.csv'), BACKDROP_COLUMNS, backdrop_rows()),
                                (os.path.join(batches, 'gods-01.csv'), GOD_COLUMNS, god_rows())):
        old = read_csv(path)
        names = {r['name'] for r in old}
        new = [r for r in rows if r['name'] not in names]
        if new and not dry:
            write_csv(path, columns, old + new)
        out.append((path, len(new)))
    priests = priest_rows(batches)
    if priests:
        n = 1
        while os.path.exists(os.path.join(batches, 'priests-%02d.csv' % n)):
            n += 1
        path = os.path.join(batches, 'priests-%02d.csv' % n)
        if not dry:
            write_csv(path, P.COLUMNS, priests)
        out.append((path, len(priests)))
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--batches', default=BATCHES)
    ap.add_argument('--dry-run', action='store_true')
    a = ap.parse_args(argv)
    for path, n in plan(a.batches, a.dry_run):
        print('%s %s: %d new rows' % ('Would write' if a.dry_run else 'Wrote', os.path.relpath(path, ROOT), n))
    return 0


if __name__ == '__main__':
    sys.exit(main())
