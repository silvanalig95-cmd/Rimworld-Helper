# Rimfolk

*Their wishes. Their bonds. Their council. Their story.*

A RimWorld 1.6 mod that turns your colonists into a society: people who want things, remember things, choose
sides, keep traditions and write it all down. You lead them through a council, and they let you know what they
think of it. (Formerly "Colony Council: Wishes & Plans". Existing saves and settings carry over.)

![preview](About/Preview.png)

No other mods are required, and it works with any combination of DLCs (or none).

## Installing

**Recommended: install once with GitHub Desktop, then update with a double-click.**
1. Install [GitHub Desktop](https://desktop.github.com) (no account needed for this).
2. If you have an old `Rimfolk` (or `ColonyCouncil`) folder in your RimWorld `Mods` folder, delete it.
3. In GitHub Desktop: **File → Clone repository → URL**, enter `silvanalig95-cmd/Rimworld-Helper`, and set the
   **Local path** to your `Mods` folder plus `\Rimfolk`, for example
   `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Rimfolk`. Click **Clone**.
4. Start RimWorld, open **Mods**, enable **Rimfolk** and restart.

To update later, double-click **`Update Rimfolk.bat`** in the `Rimfolk` folder (or click **Fetch origin**, then
**Pull origin**, in GitHub Desktop). Restart RimWorld afterwards. Updating replaces anything changed by hand inside
the mod folder; your settings and saves are kept elsewhere and are safe.

**Or: download a ZIP each time.**
1. Download this repository as a ZIP (green **Code** button → **Download ZIP**) and extract it.
2. Move the extracted folder into your RimWorld `Mods` folder and name it `Rimfolk` (if you had the old
   `ColonyCouncil` folder, delete it first):
   - Steam on Windows: `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Rimfolk`
   - Mac: right-click RimWorld.app → Show Package Contents → `Mods/Rimfolk`
3. Start RimWorld, open **Mods**, enable **Rimfolk** and restart.

The folder must contain `About/`, `1.6/`, `Defs/`, `Languages/` and `Textures/` directly (not nested
one level deeper). It is safe to add to an existing save. Removing it from a running save logs some
one-time errors about missing classes. They're harmless.

## What you get

A **Council** button at the right end of the bottom bar (you can move it in the settings) opens the council
window, organized in five sections:

| Section | Sub-tabs | What's there |
|---|---|---|
| **Colony** | Overview · Priorities · Moods · World · Room plans | the colony at a glance (numbers, how they moved this week, graphs of mood, trust, wealth and people over the last weeks); the advisor's ranked list of what the colony needs; every mood added up; what's coming and who's out there; ready-made rooms |
| **People** | Roster · Bonds · Wishes | every colonist at a glance with their full story; friendships, rivalries, circles, vendettas, secrets; open wishes and trust |
| **Council** | Decrees · Ambition | decrees, petitions and the council's five offices; the current ambition and meetings |
| **History** | Chronicle · Dreams · Journals | everything that happened (filter by wishes, council, people, strangers, world events, dreams, losses), recent dreams, every colonist's journal |
| **Almanac** | | the knowledge base |

Section names show a count when something there wants your attention (urgent advice, open wishes, petitions).
Each colonist also has a **Story** tab in their inspect panel, and clicking a colonist's name in the council window
opens their page in the roster.

### Priorities: what the colony needs
A ranked list of concerns, each with a short explanation, a rotating **"why it matters"** tip, and buttons to
jump to the problem, plan a fitting room, or read more in the Almanac. It watches:
- food vs. the coming winter (forecast from your map's actual climate), spoiling food, blight, ripe crops before frost
- kitchen hygiene (butchering next to the stove, dirty kitchens), nobody cleaning, dirty hospitals
- cold or hot months ahead, and what to do during heat waves, cold snaps, toxic fallout, solar flares,
  volcanic winter, flashstorms, droughts, eclipses and psychic drones
- defenses vs. the raids your wealth attracts, fire risk from wooden walls around open flames, insects under
  overhead mountain
- beds and barracks, a place to eat, recreation variety, rooms without light, bodies lying around, mood breaks,
  expectations about to rise with your wealth
- doctor, medicine and medical beds; prisoners without a warden, food or bed
- power shortages, jobs nobody is assigned to, items rotting outside, an idle research bench, ideas for what's next

Urgent ones also show up in the alerts on the right.

### Wishes: what your colonists want
Colonists voice wishes from who they are: their traits, passions, mood, relationships and what just happened
to them. A raid makes someone ask for defenses within hours; a cold night, a meal eaten off the floor, a
friend's death or food poisoning all set off matching wishes. Every wish arrives with a little scene and a line
in the colonist's own voice, chosen to fit the season, weather, time of day and their situation.

More than 70 kinds of wish, most with variants (six kinds of work room, a dozen pieces of furniture, any
missing body part), so the same colony rarely hears the same request twice in a row: a wish the colony has
heard several times lately becomes much less likely, and one nobody here has ever made a little more likely.

| Wish | Who asks |
|---|---|
| **Home:** a room of their own, a bigger, roomier or nicer bedroom, a piece of furniture, a light, a plant, art for the bedroom, a fire in their room for winter, a bed for their pet | barracks sleepers, the greedy and jealous, night owls, rising expectations, the cold |
| **Love and family:** to share a bed with their partner, not to sleep next to someone, a wedding, a birthday treat, a crib for the little one, somewhere for children to learn and play | couples, feuding roommates, parents (Biotech) |
| **Work:** a workshop, kitchen, laboratory, hospital, greenhouse or barn; to do the work they love; lessons from the colony's best; somewhere to train; a weapon rack; a smokehouse; a beehive | passionate colonists, fighters, cooks, keepers |
| **Stores:** better food, a freezer, a brewery, a little something to drink, firewood for winter, fodder before winter, honey in the larder, more books, a stocked infirmary, a proper treasury | gourmands, cooks, the food-poisoned, keepers, readers, the greedy |
| **Gear:** new clothes, something warm for winter, a hat, something fine, a piece of jewelry, proper armor, a decent weapon, something a visiting trader has | tattered clothes, the cold, the vain, fighters, anyone who saw the trader's wares |
| **Colony:** stronger defenses, a clean colony, music, art where people gather, another sculpture, flowers, a bath, a fountain, a banner, a place to pray, a bench to sit on outside, a fire to gather around | anyone shaken by a raid, the tidy, artists, the devout, everyone in spring |
| **People:** a proper burial for a friend, a party, a day off, time outside, help quitting a drug or a steady supply of it, a prisoner recruited, executed or set free, a kidnapped friend brought home | grieving friends, the bored and overworked, addicts, the kind, the vengeful |
| **Adventure:** to see something of the world (a caravan), a hunt, the beast that mauled someone hunted down | the restless, hunters, the beast's victims and their friends |
| **Body and status:** a prosthetic, an animal companion, a hurt animal patched up, a room befitting their title | the maimed, animal lovers, Royalty nobles |

**Your answer matters:**
- **Grant it** and they're pleased and **motivated** (they work faster for a few days). The happier they already
  are, the likelier the wish turns into an **inspiration** that fits it: art, a work or shooting frenzy, recruiting.
  Some wishes do more: a burial eases everyone's grief, new defenses make the colony feel safer, a passion space
  gives a burst of practice, petitioners who stood together like each other more.
- **Promise** it: they wait half again as long and trust you a little more. Break the promise and it hurts far
  more than never promising.
- **Refuse** it: it stings now, but it's honest and over.
- **Not now**: set it aside quietly.
- **Let it slide**: they bring it up again halfway through, make a pointed last call near the end, and the
  hard-working ones may sort it out themselves (plan the lamp, switch their own work back on, mark out a
  grave, pick an animal to tame).

**Others are watching.** Friends are happy for the one who got their wish. The jealous, rivals and anyone who
has been waiting longer can feel passed over, and some will want the same thing.

**Trust.** Each colonist's trust in the council (bitter, doubtful, neutral, trusting, devoted) rises and falls
with all of this. Trust isn't only mood: loyal colonists work and learn faster and hold together better, while
a grudge slows them down and shortens their temper (both show in the health tab). Bitter colonists grumble, stop
asking, and on Demanding stakes may sulk, go on an insulting spree, or announce they're leaving and walk out
three days later unless you win them back. The Wishes tab shows everyone's trust and its history.

**Rising standards.** Comfort breeds expectations. While a colonist is happy, their standards creep up from
*modest* to *comfortable*, *particular* and finally *spoiled*. The council's mood rewards shrink for them, letdowns
sting more, their patience runs shorter, and their wishes come more often and ask for more: the next impressiveness
level after that, fine meals instead of simple ones, luxuries. Hard times bring standards back down, and so does the
austerity decree.

### Decrees: spend the good mood
When the colony is doing well, put that goodwill to work. The Decrees tab lets you declare up to two policies at a
time for a few days: **long hours** (work faster), **rationing** (eat less), **drills** (everyone who can fight
trains shooting and melee), **study hours** (learn and research faster), **austerity** (no new wishes, standards
come back down) or, the other way round, **festival days** (better mood, much slower work). Everyone reacts according
to their nature: the industrious like long hours and the lazy hate them, ascetics love austerity, the bloodthirsty
love drills, gourmands suffer under rationing. The tab shows who will love or hate each one before you decide, and
living under a decree you hate wears down your trust in the council.

There's no hard limit on repeating a decree, but there is a price. Enact the same one again within a month, or
**force it through** before its cooldown is over, and those against it lose more trust and resent it more each
time. Someone may even snap: the hot-tempered throw a tantrum, others wander off in a daze. The tab warns you how
many times a decree was used lately and how likely trouble is.

### Our ways: the colony's character
The council's choices add up. Every dilemma answer, decree and many granted wishes nudge the colony's five ways:
**merciful or harsh**, **generous or thrifty**, **traditional or restless**, **open or guarded**, **hard-working or
easygoing**. Lean far enough and word spreads: a letter tells you the colony is now known (and later renowned) for
it. Colonists whose nature fits are proud of it (up to +4 mood); those it clashes with chafe (up to -4): the kind
hate harshness, the greedy love thrift, the lazy love an easygoing colony, the old love tradition. Neighbors warm to
a generous or open colony and cool towards a thrifty or guarded one, and some encounters and dilemmas only happen
for a colony with a certain name. Old deeds fade (about half in a year), so a colony can change. The **Our ways**
page in the council window shows where you stand, who is proud, who chafes, and what shaped it.

### Dilemmas: hard choices
Every week or so (sooner while everyone is content) the council has to settle something, and every answer costs
someone. A thief caught in the stores: forgive, make them work it off, or make an example of them? Two colonists
at each other's throats: take a side, force them to work together, or stay out of it and risk a brawl? A forgotten
stash of beer, a cook who wants to spend the stores on one magnificent feast, a colonist who quietly admits they
can't cope. Kind, greedy, industrious and teetotal colonists all react differently. Leave a dilemma unanswered and
the council's silence decides for you.

### Social: friends, rivals, comrades and mentors
Colonists form lasting bonds out of how they feel about each other and what they live through, and those bonds
grow, sour and end over time:
- **Friends** and **rivals** form when two people keep liking (or loathing) each other, then drift with how
  they feel.
- Everyone who fights in the same battle becomes **comrades**. That fades slowly between battles.
- A skilled, passionate colonist may take a keen learner on as a **student**, teaching them every day until they
  graduate at level 11.

Most days something small happens between two people: a late talk, a shared joke, a prank that lands badly, a
snub at dinner, a squabble over chores, war stories, a lesson, a contest between rivals, a handmade gift, gossip
that gets found out, an unexpected truce. Each one nudges their bond. Three to six people held together by strong
friendships form a **circle**, and people outside every circle sometimes feel left out.

Bonds change how people act. Friends and circle members back each other's wishes and petitions and vote for each
other; rivals object to each other's wishes and vote against each other. The council's own decisions feed back
into them: being passed over for a rival, losing an election, a fight, a grudge from a dilemma or arguing over a
decree can start a rivalry, while backing the same petition or making up after a quarrel builds a friendship. Some
dilemmas only come up between friends, rivals or comrades. When someone dies, the people closest to them (and their
rivals) write about it. The **Social** tab shows every bond with its strength and history, and every circle.

### Story: a page for every colonist
Select a colonist and open the **Story** tab in their inspect panel (next to Bio, Social and Gear): their trust,
office and earned names, their life goal and how close they are, their friends, rivals, comrades, students and the
people they've lost, their quirks, vendettas and the secrets the colony knows about, their last dreams and the latest
lines of their journal.

### Life goals
A few days after settling in, each colonist sets their heart on something to do before they die: master a craft,
marry for love, have a child, find three true friends, train a student until they can stand alone, make peace with
a rival, earn a seat on the council or a name the colony remembers, a bedroom fit for a noble, bring down twenty-five
enemies, raise a hundred and fifty structures, tend a hundred wounds, or simply live three more years. Goals fit the
person (their passions, traits and situation) and grow out of what happens to them: a battle makes a warrior, a
brush with death a healer, a wedding someone who wants to marry, a birth someone who wants a child, a lost election
someone who wants a seat, loneliness someone who wants friends, and seeing a friend reach their goal can make them
want the same. Something big can change their mind, and when someone dies before reaching theirs, the person who
loved them most may take it up and finish it for them. The Social tab shows everyone's progress and why. Achieving one is a big
moment: a letter, a long good mood, trust, often an inspiration, friends who are glad for them. Dying before it is
remembered: the chronicle notes what they never got to do, and their friends write about it.

### Secrets, vendettas and eulogies
Some colonists carry a **secret**, often from the life their backstory hints at: a raider's past, noble blood, a
deserter's shame, a doctor's fatal mistake, a child left behind, a borrowed name, a pirate's debt, a hidden kindness.
You don't get told. You only see when they confide in a close friend. A confidant who later falls out with them is
likely to let it slip; at rock bottom some blurt it out themselves. When it comes out, everyone reacts according to
their nature, betrayed confidences make enemies, and sometimes the council has to decide what to do about it.

When raiders kill or carry off someone, the people who loved them swear a **vendetta** on that faction: a weight they
carry, fury when those colours show up again, satisfaction when enough of them fall, outrage if the council makes
peace with the killers.

When someone dies, the person closest to them speaks a **eulogy** at the grave, about who the dead really were:
the years they gave, the name they earned, the goal they never reached, who killed them.

### Heirlooms and coming of age
A dead colonist's weapon or hat passes to the person who loved them most. Carrying it is a comfort (to anyone,
more to the heir), and losing it hurts. When a child grows up (Biotech), the colony marks it, parents are proud, and
the council is asked to help choose a path: an apprenticeship in the workshop, the infirmary, the study or on the
wall (which starts a real mentorship with the colony's best), or letting them choose for themselves.

### Quirks and superstitions
Colonists pick up beliefs and habits from what they live through: a **lucky helmet** after surviving a raid in it
(happy wearing it, uneasy without), a dread of storms after a death, a ritual before every fight, prayers at night
after a dream came true, a fear of the dark after lying downed in the dirt, grief that comes back every winter, and
harmless habits like knocking on wood, counting steps or talking to the dead. Small moods at the right moments; most
fade after a season or two.

### Dreams
Colonists who sleep (or lie unconscious) for a few hours sometimes wake from a dream, and tell each other about
it. Dreams grow out of their lives: the raid two days ago, the friend they buried, the rival who insulted them, the
wish the council ignored, the leg they lost, the skill they're learning, the dead they miss, their partner, their
dog. Most are just a passing mood and a line in their journal (hover the mood to read the dream). Now and then one
changes something: an inspiration, a lesson, a good night's rest, a nightmare that leaves them shaken, a dream of
victory that steadies their aim, a softened rivalry, a sudden wish. And rarely a dream is an **omen**: a few days
after someone wakes up talking about bells and pack animals on the hill, a caravan arrives; after torches in the
dark, a raid. Recent dreams are listed in the Social tab.

### Strangers, beasts and pets
Colonists meet everyone else on the map, too:
- **Visitors and traders** bring news from the road, swap recipes, admire someone's work, flirt, insult, haggle
  and cheat. Warm moments make them like your colonists and can raise goodwill; insults can end in a fistfight.
- **Guests and prisoners:** a noble's airs, a refugee's gratitude, kind words through a cell door that soften a
  prisoner, taunts that sour everyone.
- **Fallen enemies** lying downed after a fight beg, curse and pray; how your people react depends on who they are.
- **Pets and wildlife:** a pet's welcome after a hard day, stolen dinners, a dead rat as a gift, a thrumbo at the
  tree line, wolves at night, a small creature that follows someone home.
- **The council decides** when it gets harder: a visitor who wants to stay, a rude guest, a trader who wants an
  heirloom, beggars at the gate, a wounded raider who's still breathing, a predator's den, a child who wants to
  keep a wild animal, an old acquaintance who knows a secret.
- **Nemeses.** An enemy who strikes down, maims or kills a colonist and gets away is remembered. A raider who
  fled may ride with the next raid from their people; a wild animal that mauled someone gets a name and keeps
  prowling. The victim dreads it or burns for it, may make settling the score their life goal, and feels it
  when the nemesis is captured or finally brought down.
- **Pets as companions:** an animal that fights beside its person is remembered for it, and a named animal that
  dies gets a farewell.

### Offices and elections
Five council seats: **speaker** (chairs meetings, everyone's trust rises a little each day), **marshal** (drills
count for more, better aim), **quartermaster** (dilemmas cost less, better trade), **physician** (faster healing)
and **steward** (standards rise more slowly). Colonists elect them every year, voting for the skilled, the suited
and the liked, friends and circles voting together. You can appoint and dismiss from the Decrees tab, but whoever
the colony actually voted for will remember being overruled.

### Traditions
Choose the same answer to a seasonal dilemma two years running (a midsummer feast, a harvest celebration, a vigil
on the longest night) and it becomes a tradition. From then on it comes up first every year: keeping it pleases
everyone, breaking it disappoints them.

### Journals
Every colonist keeps a journal in their own voice: wishes granted and ignored, promises, decrees, raids, losses,
names earned, friendships made and lost, lessons learned. Ordinary days get written down too: now and then, something
that's really on their mind that day (a meal eaten off their knees, a cold night, a long talk with a friend, a
corpse nobody has buried yet) becomes the evening's entry. Read them on the Journals page under History, or from a
colonist's page in the Roster. The journals of the dead and departed are kept.

### Ambition: council meetings
About once a quadrum, in the evening, the council actually **meets**: colonists gather around the dining
table, in the rec room, by the campfire or in the open. Three of them pitch an ambition out loud, others back
them, doubt them or wander off topic, and when the meeting ends everyone who came votes. The decision letter
shows the votes. Going with the room's favorite pleases its backers; overruling a clear majority costs a
little trust. 35 ambitions, from *a full larder* and *hold the line* to *clean hands*, *law and order*, *the
brewmasters*, *stone and mortar*, *a master healer*, *a seat of power* and *school days*. Achieving one lifts
everyone's mood and trust.

### Build: room plans
32 ready-made rooms, grouped by purpose: bedrooms of three sizes, barracks, dining room, rec room, kitchen,
butcher's room, freezer, heat shelter (no power needed), storeroom, brewery, greenhouse, barn, workshop, smithy,
tailor's shop, art studio, laboratory, drug lab, hospitals, crypt, crematorium, prison cell and barracks,
armory, battery room, meditation and throne rooms (Royalty), classroom and nursery (Biotech).

Pick one from this tab or from the **"rooms"** button in the bottom bar. A color-coded preview
follows the mouse, and a planner panel lets you:
- **turn** the room (Q / E or buttons), **mirror** it and **move the door**
- pick the **material** for walls, door, floor and each kind of furniture, or **swap** furniture for another
  piece the colony can build; the panel shows the **cost against your stock**
- **clear the site**: cut plants and trees, mine rock (inside, in the doorway and just outside the door),
  replace your own buildings in the way, smooth natural stone. Pieces blocked by rock go down by themselves
  once it's mined.
- let the council **find a good spot** near your colony and the rooms it belongs next to, and place it there.

It places ordinary blueprints, so your builders handle the rest. Every plan comes with good-practice tips.

Colonists also sometimes add **personal touches**: a plant pot, an end table or a lamp planned for their own
bedroom.

### Almanac
An in-game knowledge base: good practice and the numbers behind it, by topic, searchable, linked from the
advisor's warnings and to room plans. The **Your mods** category explains the mods in your game (over 100 pages
covering a 216-mod medieval list): what each adds, how to get the most out of it in a medieval run, pitfalls and
conflicts, and how it works with Rimfolk. Each page only shows when its mod is active.

### Names, anniversaries and petitions
Whoever has done the most of something earns a name for it, until someone overtakes them: *the Healer*, *the
Surgeon*, *Deadeye*, *the Builder*, *Beastfriend*, *Silver-Tongue*, and some less flattering ones (*the Broom*,
*the Mule*, *the Kindling* for whoever keeps catching fire, *the Storm*). Each year since a colonist joined, they
mark the anniversary in their own words, and their friends share it. And when the moment fits (food running low,
right after a raid, while people grieve, when everyone has grown spoiled) a colonist may petition the council for
the decree they'd like.

### Annals
When a new year begins, the colony's best reader or talker writes up the year that ended: how many you were and
are, who joined, who died and who walked out, how often trouble came, whether you grew richer or poorer, what the
council granted, declared and decided, the closest friends and the worst enemies, who trusts the council most and
least, and a last word in the author's own voice. Past years can be reread from the Chronicle tab.

### Chronicle
A running history of wishes, promises, petitions, meetings and ambitions.

## Fits your world (medieval runs too)

The council only suggests what your colony can actually do right now:
- **Research and tech level:** nothing that needs unresearched tech, and nothing electric before Electricity.
- **Purpose, not names:** buildings are recognized by what they do. A medieval colony gets torch lamps,
  campfires, spike traps and bedrolls; an industrial one gets standing lamps, heaters and turrets. Modded
  furniture that does the same job counts too.
- **Materials:** room plans default to what you have the most of, and you can change every material.
- **What's on the map:** animals to tame, a body to bury, a prosthetic you can make, winter that actually comes.
- **Ambitions:** only ones whose every goal is achievable for your colony are offered.
- **Era setting:** the mod works out your era from finished research and from what's built (a generator
  counts as electricity). If a modded tech tree confuses it, set **General → Era** to *Pre-industrial* or
  *Industrial or later*. In a pre-industrial colony, text about guns, turrets, freezers, power or
  spaceships is skipped whenever another line fits, solar flare advice and omens disappear, and defense
  advice suggests barricades and traps.

## Playing with other mods

Rimfolk needs **Harmony** (load it first; most modlists already have it). Almost everything Rimfolk does
adds its own things and reads the game; a few small, careful Harmony patches change how the game itself behaves
(for example Rimfolk's hints in the game's own Needs tab). Each patch switches itself off with one warning if it
can't work alongside your other mods, and the "Deeper game hooks" setting turns them all off at once. No vanilla
defs are edited. A few things it does on purpose:
- **Medieval Overhaul, Vanilla Brewing Expanded and other barrel mods:** fermenting barrels, kegs and stills
  made with the Vanilla Expanded framework or Processor Framework (Medieval Overhaul's rustic fermenting barrel)
  count for brewing wishes and room plans, and a stock of social drinks (ale, mead, cider, wine) fulfils the
  brewery wish.
- **Alien races (Humanoid Alien Races):** every humanlike race gets the Story tab, and ages are read against the
  race's lifespan, so a 300-year-old elf isn't treated as elderly.
- **Simple Sidearms:** an heirloom weapon carried as a sidearm still counts as carried.
- **No mechanoids:** mechanoid dreams and goals only show up in games where mechanoids are around and hostile
  (Rimedieval's default, which turns them friendly, counts as none).
- **Rimedieval:** events it removes stay removed (dream omens won't bring them back), and hidden buildings are
  never suggested.
- **Vanilla Skills Expanded, Alpha Skills:** their passions are judged by how fast they make a pawn learn, so
  apathy-type passions don't count as passions and natural/critical ones count as burning.
- **Vanilla Outposts, gravships, vehicles (Vehicle Framework):** someone away at an outpost, in flight or aboard a
  vehicle is still one of the colony: no grief, no "gone" in the annals, and their records are kept.
- **Anomaly ghouls, A RimWorld of Magic's raised undead and spirits:** not treated as people, so no life goals,
  quirks, dreams or friendships for them.
- **Vanilla Social Interactions Expanded:** works alongside. Its best friends and teaching sit next to Rimfolk's
  bonds and mentorships; its "aspirations" are short inspiration tasks, not life goals, so nothing doubles up.
- **Guests, prisoners and visitors** (Hospitality and the like) are left out of meetings, votes and council
  talk.
- **Removing a mod mid-game:** room plans that used a material from that mod fall back to a default material,
  and people who vanished with it are quietly forgotten.

## Why am I feeling this?
Every mood Rimfolk gives says what caused it, under the description in its tooltip: whose wish came true (or
didn't), which council decision, which decree and how long it has left, which visitor, secret, nemesis or
moment. The trust mood lists what moved it lately.

## World events
Every few days (you choose how often), something happens out in the world: Rimfolk's own events, on their own
rhythm, so they never take a slot from your storyteller's events. Each is written for tribal, medieval, industrial
and spacer colonies alike, and finds and visitors fit your era (a medieval colony meets medieval folk and digs up
medieval things). Many ask the council what to do, with a day or so to answer.
- **Pasts catch up.** Who a colonist used to be (read from their backstory: soldier, outlaw, noble, trader...)
  decides what finds them: an **old comrade** or partner in crime asking to stay, **lost family** (a sibling, a
  parent, a grown child) who thought them dead, an **old flame** (with their current partner watching), a
  **creditor** with an old debt (pay, let them talk their way out, or refuse and maybe meet the thugs), **bounty
  hunters** for a colonist with a violent past (stand by them and fight, pay, or hand them over), an
  **inheritance**, a letter from an **old teacher**, good or bad **news from home**. Someone who **walked out or
  was banished** may come back one day and ask to be let in.
- **Challenges.** A **travelling champion** challenges your best at shooting targets, wrestling (no weapons),
  crafting, cooking, art, debate, animals, riddles or farming, and you can bet silver on them. A **great beast**
  with a name roams your land for a few days: bring it down or tame it for glory, or let it pass. A visitor sells a
  **treasure map** (maybe true). A hostile people **demands tribute**: pay, haggle, or refuse and fight.
- **Old enemies come back.** A **nemesis** who got away returns at the head of a raid. The people of a prisoner who
  **died in the colony's hands** come for revenge. A **deserter** from a hostile people asks for shelter, and
  their old comrades may come looking.
- **The land and the sky.** A **comet** for days (the cheerful call it a good omen, the gloomy and nervous a bad
  one), **firefly nights** in summer, **hail** that knocks back crops in open fields (roofs and greenhouses are
  safe), **vermin** in warm stores (food kept cold is safe, and a cat or dog may catch them first), a **wild
  harvest** of berries or healroot, a **buried cache** turned up by someone working outside.
- **Visitors with a purpose.** A **storyteller** for an evening of tales (who may already know a story about your
  colony), **mourners** at a colonist's grave, **hungry pilgrims**, a **scholar** who can push your current research
  along, a young **apprentice** who wants to learn from your master of a craft (a real mentorship), a **quiet
  stranger** with a secret, a **lost child** (Biotech), a **stray** that follows someone home.
- **A wider world.** **Lost cargo** lands on your land (send it back, or keep it and hope nobody checks), a
  **talent scout** wants to buy out your best colonist's contract (or a lord wants them in service), a
  **journalist** or **chronicler** writes about the colony (what they write depends on its character), a
  **travelling doctor** or **herbalist** sells a month of better resistance to sickness, **music** on the radio or
  minstrels on the hill, and a **famous visitor** stopping for the night.

Some events pick up where an earlier one left off: the **deserter**'s old unit tracks them down; the **lost child**
you walked home comes back years later, grown, with a gift; a **champion** you beat comes back for a rematch (or one
who beat you, to defend the title); the **great beast**'s young turns up a season after it fell; **pilgrims** you fed
send their thanks down the road.

The council window's **World** page (Colony section) shows what's coming (fights the colony knows about and how
soon, questions waiting for an answer), who's out there with the colony on their mind (nemeses at large, people who
left and might come back, peoples with a grudge, rivals, a great beast and how long it'll stay), and the latest world
events.

Answers shape the colony's character, and the events feed nemeses, secrets, mentorships, journals and the
chronicle. Fights come only with **battles** switched on in the settings, when the storyteller sends threats at
all, never on top of another fight, and at a share of the storyteller's own raid strength. In dev mode, every
event can be fired from **Debug actions → Incidents** (they're named `Council_Evt_...`).

## Scenarios with stakes
Ten scenarios, marked "(Rimfolk)" on the scenario page with how hard they are. Each starts the game with
something already in motion, and a few seconds in, a letter says how things stand.

**Small, gentle starts** (one or two colonists, no attacks of their own):
- **The Inheritance.** One colonist and a dog in a valley left to them by a great-aunt. Within a week or two someone
  from their past finds them and asks to stay; a few weeks later someone else may follow.
- **Master and Apprentice.** Two colonists: a master teaching the apprentice their best craft (a real mentorship
  from day one). A travelling scholar visits in a week or two, and later someone else comes wanting to learn.
- **Runaway Hearts.** Two lovers who ran away together. In a couple of weeks an old flame of one of them turns
  up asking to stay; later, family may come looking.
- **The Lone Hunter.** One hunter, a rifle and a dog, on the trail of a legendary beast that passes through in a
  couple of weeks (hunt it, tame it, or let it go: it only fights if provoked). Someone may join them before that.

**With stakes** (fights come later, smaller and a limited number of times; your storyteller's difficulty scales them):
- **Hot Money** (moderate). Four thieves with 2,500 silver that belongs to a pirate boss, who asks for it back in
  about two weeks: pay it back (and it's over), pay half and ask for time, or keep it and fend off the boss's
  people a few times. One of the four skimmed from the take.
- **Marooned** (moderate). A loyal few thrown off their ship by mutineers, whose pirate friends come three times,
  a month or so apart, starting in about three weeks (sooner over if the ringleader falls). One of them was in on
  the mutiny.
- **Deserters** (moderate). Five well-armed, badly fed soldiers who walked away from a war. In a month or so their
  old unit comes for one of them: hand them over, buy their freedom, or fight.
- **Bad Blood** (gentle fights, social drama). Six survivors from two families who have never got on. Each family
  takes one name, stands by its own, and doesn't think much of the other (the grudge fades over a month or two).
- **Borrowed Time** (moderate). The best start money can buy, bought with a 3,000-silver loan due in about a month
  and a half. Refusing to pay brings the collectors twice.
- **The Fallen Tyrant** (challenging). Five servants who drugged their warlord at a feast. The warlord starts as your
  prisoner, out cold for a few hours (build a cell fast), and every colonist loathes them. Their loyalists come in
  a week or so, and twice more after that unless the warlord is dead or one of you by then.

The pieces are **scenario parts** you can add to any scenario in the scenario editor: *a sworn enemy* (who hunts,
was robbed by, betrayed or held the colonists in chains, can start as your prisoner, and attacks a set number of
times or until they fall), *a debt*, *deserters*, *two feuding families*, *a bond from the start* (a couple, old
friends, or a master and apprentice), *a world event around a set day* (someone from the past, a visitor, a great
beast...), *a reputation* and *a secret*. Enemies are nemeses from the first day; debts and hunts show on the
**World** page. A hunt ends when its attacks run out, or when the one leading it dies, joins you, is sold off, or
their people make peace or are wiped out, and the colony gets to feel that. Settings → **Fights in Rimfolk's
scenarios** makes those attacks weaker or stronger (25% to 200%). They come even with Rimfolk's world-event battles
switched off (you chose the scenario for them), but only when your storyteller sends threats at all.

## The colony notices what happens
The moment it happens: a **wedding** is written into the chronicle and the journals of the couple and their friends
(and nudges the colony a little toward tradition); a **birth** is recorded by the parents; colonists **coming
home** from a journey of two days or more are glad to be home, their friends are glad to see them, and long
journeys make the chronicle; when someone **breaks down** from their mood, their close friends worry in their
journals, and the worst breaks are remembered. When a real fight at home is over, the colony tells itself the story
of its **victory**: how many of them fell, who of ours didn't make it, and who brought down the most (a small
"we held" mood for everyone, a little pride for the hero, and journal lines). **Quests** the colony took on are
noted in the chronicle when they end, with a small "job well done" when they went well. Nemeses are now caught the instant someone is downed or killed.

Rimfolk also adds a line to some of the game's own letters when it knows something true to add. A raid letter names
the nemesis riding with it (they now arrive with the raid instead of an hour later), or the colonist sworn to make
these people pay. A mood break's letter says what was weighing on them from the council's side: a wish that came to
nothing, a promise broken, a decree they hate, trust long gone. A death letter ends with how long they'd been with
the colony, the life goal they never reached, and the council seat or name they held. A setting under "Deeper game
hooks" turns these notes off.

## Colonists who help themselves
Now and then a colonist fixes a small thing that hurts their mood, on their own and in their own time:
- tidies their own room when it's filthy, or visits a grave while they grieve
- takes a short break when they're starved of recreation (even in work hours, but never from fighting fires or
  tending the sick)
- places a **plan** for one cheap thing in their own room: a light when it's too dark, a plant pot when it's ugly, a
  passive cooler when it's too hot to sleep, or a free sleeping spot when there's no bed at all

Only things the colony can already build, with the materials in stock, and cheap; at most two such plans at a time.
Never during a raid, never anything that harms or bothers anyone else. The industrious do it more, the lazy less.
You get a message each time, and a plan is only a plan: cancel it if you'd rather not. Both parts have their own
switch in the settings.

## Mood tab and Moods page: what to do about it
Select a colonist and open their **Mood** tab: every mood weighing on them or lifting them, with its size, why it's
there, when it fades, which mod it comes from, and **what you can do about it** ("Build a table with chairs near
where they eat"). The mood bar shows where they are, where they're heading and the lines where breaks begin.
The council window's **Moods** page (Colony section) adds it up for the whole colony: what weighs on everyone,
who feels it, who is closest to breaking, and how much comes from the base game, Rimfolk and each of your mods.
Buttons lead straight to the advisor's matching warning, the council page that deals with it, or an Almanac page.
Hints cover the base game, the DLCs and Rimfolk; moods from other mods get general advice and their mod's name.

## Slow speeds
Two speeds slower than normal, **½×** and **¼×**, for planning without pausing: the colony keeps living, gently.
Small buttons sit next to the time controls (drag them by their grip to put them wherever you like; on a
Steam Deck too). The game's own "slower" and "faster" keys also step below normal speed. Picking normal or any
faster speed ends it; pausing and unpausing keeps it. Hide the buttons in the settings if you only want the keys.

## Settings

**Options → Mod options → Rimfolk**, on four pages (General, Wishes and council, People, Messages and interface),
with quick setups (*Everything on*, *Council only*, *Advisor only*) and one switch for how chatty the mod is:
**Everything**, **Notable only** (default: messages for what changes something; small moments stay in the
journals, the Story tab and History) or **Important only**.
- turn each part (advisor, wishes, meetings, personal touches) on or off
- **Stakes**:
  - **Gentle**: only upsides (decrees still cost some mood, at half strength, because you chose them)
  - **Balanced** (default): upsides and downsides in proportion
  - **Demanding**: downsides 50% stronger; bitter colonists may sulk
  - **Advisor only**: no mood effects at all
- strength of downsides, jealousy and favoritism, wishes after events, reminders, self-help
- wish frequency, how many can be open at once, how long they last, reward strength
- meeting interval and ambition deadlines
- letters vs. messages, floating speech text, alerts
- where the council button sits in the bottom bar
- **council life**: decrees, dilemmas, petitions, annals, names, journals, offices, the social web, social
  events, life goals, secrets, vendettas, eulogies, heirlooms, coming of age, quirks, dreams and dream omens, encounters with strangers, nemeses (and whether they return), pets, the
  colony's character, each on its own switch
- **world events**: on or off, whether they may bring fights, and how often (about every 4 days by default)
- **how often** the smaller things happen: moments with strangers and animals, moments between colonists, social
  events and dreams, each from a quarter as often to three times as often

## Optional mods that go well with it

- **Interaction Bubbles** by Jaxe (search the Steam Workshop): shows social-log conversations as speech
  bubbles, so colonists grumbling about their wishes appears above their heads.

## Status and feedback

Version 0.4. It compiles against RimWorld 1.6, and all XML is checked against the real game types by the
included validator. It runs in a live game with a 200+ mod medieval list. If something misbehaves, open the debug
log (Options → Development mode, then the log button): lines starting with `[Rimfolk]` are from this mod.
Please share them so they can be fixed.

## For modders

- `Source/ColonyCouncil/`: the C# code. Build with `dotnet build -c Release` (needs the .NET SDK;
  the RimWorld reference assemblies come from NuGet).
- `Tools/DefValidator/`: checks every XML def and translation key against the compiled game types:
  ```
  dotnet run --project Tools/DefValidator -- . ~/.nuget/packages/krafs.rimworld.ref/<version>/ref/net472 1.6/Assemblies/ColonyCouncil.dll
  ```
- `Tools/Tests/`: tests for the room rotation and mirroring math (`dotnet run --project Tools/Tests`).
- Wishes, advice checks, ambitions, room templates, flavor pools and almanac pages are all data-driven XML.
  Lines can carry tags (`[winter,trigger:raid]`) so they're used when they fit. See [DESIGN.md](DESIGN.md).
