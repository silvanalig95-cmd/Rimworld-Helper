# Rimfolk

*Their wishes. Their bonds. Their council. Their story.*

A RimWorld 1.6 and 1.7 mod that turns your colonists into a society: people who want things, remember things, choose
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

**Or: download a ZIP and run the installer.**
1. Download this repository as a ZIP (green **Code** button → **Download ZIP**; pick the branch you want in the
   branch menu first) and extract it anywhere, for example in Downloads.
2. Close RimWorld, then double-click **`Install Rimfolk.bat`** in the extracted folder. It removes the old version
   from `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Rimworld Helper` and copies the mod's
   files there (only what the game needs: `About`, `1.6`, `Defs`, `Languages`, `Patches`, `Textures`). If Windows asks for
   administrator rights, say yes. To install somewhere else, open the file in Notepad and change `MODS` and
   `FOLDER` at the top.
3. Start RimWorld, open **Mods**, enable **Rimfolk** and restart. Repeat steps 1 and 2 to update.

On a Mac, move the extracted folder into `Mods` yourself (right-click RimWorld.app → Show Package Contents →
`Mods/Rimfolk`).

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

The window opens on the page you last looked at, also after a restart (it's kept with the mod settings). A colony's
first session with Rimfolk brings one quiet **welcome letter**: what Rimfolk does, where the council button is, and a
button to the Almanac's **Start here** page. Saves that already had Rimfolk never get it, and it needs no setting.
The **Mood** and **Story** tabs fit the room above the inspect pane: on a small screen, at a big interface scale or
above RimHUD's taller pane they get shorter and scroll instead of running off the top of the screen. A click on a
section's heading (the heart, what weighs on them, their bonds...) folds it away, and each tab remembers that for the
session.

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
- rooms the colony has outgrown, gently: a dining room with fewer seats than people, a barracks that could become
  bedrooms now there's enough wall material in store, a hospital without the vitals monitor, hospital beds or sterile
  tile you've researched (with "Upgrade the room"), a rec room with too little to do for everyone
- doctor, medicine and medical beds; prisoners without a warden, food or bed
- power shortages, jobs nobody is assigned to, items rotting outside, an idle research bench, ideas for what's next

Urgent ones also show up in the alerts on the right.

### Wishes: what your colonists want
Colonists voice wishes from who they are: their traits, passions, mood, relationships and what just happened
to them. A raid makes someone ask for defenses within hours; a cold night, a meal eaten off the floor, a
friend's death or food poisoning all set off matching wishes. Every wish arrives with a little scene and a line
in the colonist's own voice, chosen to fit the season, weather, time of day and their situation.

107 kinds of wish, most with variants (six kinds of work room, a dozen pieces of furniture, any
missing body part), so the same colony rarely hears the same request twice in a row: a wish the colony has
heard several times lately becomes much less likely, and one nobody here has ever made a little more likely.
Every wish asks only for what the colony can do now, in its era: a tribe hears about healroot and pemmican, an
industrial colony about a deep drill or a comms console. The Almanac page "what colonists wish for" lists them all.

| Wish | Who asks |
|---|---|
| **Home:** a room of their own, a bigger, roomier or nicer bedroom, a piece of furniture, a light, a plant, art for the bedroom, a fire in their room for winter, a real floor instead of bare earth, a bed for their pet | barracks sleepers, the greedy and jealous, night owls, rising expectations, the cold, the tidy |
| **Love and family:** to share a bed with their partner, not to sleep next to someone, a wedding, a birthday treat, a crib for the little one, somewhere for children to learn and play | couples, feuding roommates, parents (Biotech) |
| **Children and the old:** lessons from the colony's best, a pet to look after, new stories to read; a soft chair, a biosculpter pod, a resting place for when the time comes | children (Biotech); colonists of 50, 60 or 65 and over |
| **Work:** a workshop, kitchen, laboratory, hospital, greenhouse or barn; to do the work they love; lessons from the colony's best, or an apprentice to teach; somewhere to train; a weapon rack; a smokehouse; a beehive; a tool cabinet; stone blocks put by; a deep drill; a pen for the animals; a fishing spot (Odyssey) | passionate colonists, the old, fighters, cooks, keepers |
| **Stores:** better food, a freezer, a brewery, a little something to drink, firewood for winter, fodder before winter, food that keeps, something fresh to eat, honey in the larder, more books, a stocked infirmary, healroot put by, a proper treasury | gourmands, cooks, the food-poisoned, keepers, readers, healers, the greedy |
| **Illness:** a bed for the sick, a pot of tea (Vanilla Brewing Expanded) | anyone down with a cold, flu or worse |
| **Gear:** new clothes, something warm for winter, a hat, something fine, a piece of jewelry, proper armor, a decent weapon, something a visiting trader has | tattered clothes, the cold, the vain, fighters, anyone who saw the trader's wares |
| **Colony:** stronger defenses, a clean colony, music, art where people gather, another sculpture, flowers, a bath, a fountain, a banner, a place to pray, a bench to sit on outside, a fire to gather around, a table where everyone fits, a telescope, a cooler for the hottest days | anyone shaken by a raid, the tidy, artists, the devout, everyone in spring, the lonely, curious minds, anyone in the heat |
| **People:** a proper burial for a friend, a party, a day off, time outside, help quitting a drug or a steady supply of it, a prisoner recruited, executed or set free, a kidnapped friend brought home, an evening at the campers' fire, a council meeting (promising it calls one), a comms console, a bed kept for guests (Hospitality) | grieving friends, the bored and overworked, addicts, the kind, the vengeful, the sociable |
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

**A room taken back.** Someone whose wish for a room of their own came true minds being moved back into a barracks
or into a bedroom with someone who isn't their partner: "my room was taken from me" (-3, and some trust lost each
day, scaled like the council's other letdowns), with a line in their journal the first time. It ends when they have
a room of their own again, and after about 30 days in shared quarters they accept it for good. Children, slaves and
ascetics don't mind.

**Trust.** Each colonist's trust in the council (bitter, doubtful, neutral, trusting, devoted) rises and falls
with all of this. Trust isn't only mood: loyal colonists work and learn faster and hold together better, while
a grudge slows them down and shortens their temper (both show in the health tab). Bitter colonists grumble, stop
asking, and on Demanding stakes may sulk, go on an insulting spree, or announce they're leaving and walk out
three days later unless you win them back. The Wishes tab shows everyone's trust and its history.

**Rising standards.** Comfort breeds expectations. While a colonist is happy, their standards creep up from
*modest* to *comfortable*, *particular* and finally *spoiled*. The council's mood rewards shrink for them, letdowns
sting more (and so do the colony's own frictions and everyday pressures, up to 40% more), their patience runs shorter, and their wishes come more often and ask for more: the next impressiveness
level after that, fine meals instead of simple ones, luxuries. Hard times bring standards back down, and so does the
austerity decree.

High standards also take the shine off the game's own comforts. A comfortable colonist feels the good moods of the
base game and other mods (a fine meal, an impressive bedroom, a party, a joy activity) at 90%, a particular one at
80% and a spoiled one at 70%. Only the first 10 points of each count for less, so the big moments of a life, like a
wedding or a catharsis, still land almost in full; bad moods are never touched. Rimfolk's own good moods already
follow standards, so they aren't shrunk twice. Hover a good mood in the Needs tab to see what it would be without
standards. This works through one of the deeper game hooks, and the *standards cover the game's comforts* setting
turns it off.

**Good moods add up slowly.** Everything good Rimfolk gives a colonist shares one budget: a wish granted, a wedding,
a dilemma that went their way, a laugh, a good talk, a contest won, an animal asleep on someone's feet, a sweet dream.
The two biggest on a colonist's mind count in full, and each one after that half as much as the one before, so a
dozen good moments lift a colonist about as much as three, and a small joy adds little on top of a wish just granted.
Bad moods aren't part of it, and neither is anything the game and other mods give (high standards take their own
share of those, see above). The reward strength setting and rising standards apply to Rimfolk's good moods too. Hover one in
the Needs tab to see when it counts for less; one that counts for less than half a point is left off the list (its
little bit still counts). The *good moods add up slowly* setting turns this off.

**Getting used to good times.** Someone whose life is already good gets used to good things faster. When a colonist's
mood is above 75%, a new good memory from Rimfolk (a wish granted, a good talk, a contest won, a dilemma that went
their way) fades sooner, smoothly down to half its usual time from 90% mood. It feels just as good while it lasts,
and in hard times good things last their full length. A good memory about someone (a friend who had their back)
keeps its effect on opinion for the full time; only the mood ends sooner. A good memory that comes back renews with
the time it would have now. Its tooltip says when it fades sooner. This works through one of the deeper game hooks, and a setting
turns it off.

None of these is a flat penalty, and nothing is taken away: a colony that lives well still feels it, it just doesn't
float at the top of the mood bar on a pile of small pleasures. A line in the Mood tab says what they take off right
now, for example "Good times: Rimfolk's good moods +11 of +18, 2 fading sooner · the game's comforts +14 of +19
(particular)".

**Everyday frictions.** People who live close together get on each other's nerves. Colonists argue about the dishes,
complain about a bunkmate's snoring, take the last good meal, keep a borrowed knife, correct each other at the table
and give each other the cold shoulder after a spat. Some natures grate: an annoying voice, creepy breathing, a
pyromaniac near the tinder, a nudist at supper, an ascetic lecturing a greedy one. So do the times: someone keeps
away from a sick colonist, two argue about a camp, a temper snaps. A messy or cramped home adds its own (19
nuisances): a door that sticks, a leaking roof, smoke in a small room, a generator droning next to someone's bed, a
filthy room, a cold or stifling one, the dog asleep on the bed, building work right outside, vermin in the stores, a
bare earth floor. Each costs a point or two of mood for half a day to a day and a half. In an ordinary colony they
come about as often as the warm moments; less in a happy, roomy one, more in barracks or between people who dislike
each other. Abrasive, volatile and jealous colonists start more of them, kind ones none. After a spat the two share
no warm moments until it wears off or they clear the air. A setting turns them off.

**Everyday pressures.** A colony that has everything still has its everyday wear. Five small, fixable pressures show
as moods while their cause lasts, each saying what caused it, with what helps in the Mood tab:
- **the same meal again**: 6 of the last 8 meals the same kind, more after 10 or 12 of 12 (-1 to -3). A colony that
  can cook only one kind feels it least; ascetics don't mind, gourmands mind it more. Three different foods in two
  days make **a varied table** (+2).
- **the same work for days**: most of each day on one kind of work, 3, 5 and 8 days running (-1 to -3). The
  industrious and hard-working, children and anyone with a burning passion for that work don't mind.
- **missing someone**: a partner apart for 2, 5 and 10 days, or a close friend for 3 and 8 (-1 to -3). The
  homecoming is already celebrated when they're back.
- **a packed room**: a dining or rec room with too little space per person, or diners short of seats (-1 to -2, for
  half a day). Parties, rituals and meetings don't count.
- **half-built work**: frames left untouched for 2 days and plans waiting 4 days in the home area (-1 to -3). Plans
  alone stay small; lazy colonists and slobs don't care.

None of them is a flat penalty: a second meal bill, a second job in the Work tab or a bigger dining room makes them go
away. They feed the heart's feelings (loneliness, restlessness, frustration, contentment). The **Life's ups and
downs** setting makes them gentle (half strength, no half-built work), normal or demanding (half again as strong).
Meals are noted through one of the deeper game hooks. The Almanac's *everyday pressures* page has the details.

### Decrees: spend the good mood
When the colony is doing well, put that goodwill to work. The Decrees tab lets you declare up to two policies at a
time for a few days: **long hours** (work faster), **rationing** (eat less), **drills** (everyone who can fight
trains shooting and melee), **study hours** (learn and research faster), **austerity** (no new wishes, standards
come back down) or, the other way round, **festival days** (better mood, much slower work). Everyone reacts according
to their nature: the industrious like long hours and the lazy hate them, ascetics love austerity, the bloodthirsty
love drills, gourmands suffer under rationing. The tab shows who will love or hate each one before you decide, and
living under a decree you hate wears down your trust in the council. With feelings and strain on, the hard ones
(long hours, drills, on guard...) also build a little strain in those who resent them, and **days of rest** lets the
colony recover: 10% slower work for three days, while strain drains about twice as fast.

Nineteen decrees in all. The everyday ones each trade a little work for something else: **quiet hours** (deeper
sleep, far fewer quarrels; night owls hate it), **shared meals** (talk and goodwill at one table), **rest when ill**
(wounds and illnesses mend faster while the well cover for the sick), **banked fires** (fires, torches and fueled
heaters burn a quarter less fuel, in colder, darker rooms), **apprenticeships** (everyone practices building,
crafting, cooking and growing every hour) and **a feast day** (one day of eating well and working half). Colonists
petition for them when someone is ill, in winter, or when a newcomer joins.

There's no hard limit on repeating a decree, but there is a price. Enact the same one again within a month, or
**force it through** before its cooldown is over, and those against it lose more trust and resent it more each
time. Someone may even snap: the hot-tempered throw a tantrum, others wander off in a daze. The tab warns you how
many times a decree was used lately and how likely trouble is.

### Our ways: the colony's character
The council's choices add up. Every dilemma answer, decree and many granted wishes nudge the colony's five ways:
**merciful or harsh**, **generous or thrifty**, **traditional or restless**, **open or guarded**, **hard-working or
easygoing**. Lean far enough and word spreads: a letter tells you the colony is now known (and later renowned) for
it. Colonists whose nature fits are proud of it (up to +4 mood, less for the spoiled); those it clashes with chafe
(up to -4): the kind
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

There are 126 of them, and many grow out of everyday life: a sick colonist when the medicine is nearly gone,
whether to keep someone with a fever apart, a cook with a cold, an old family remedy, a visitor's strange cure, the
camp on the colony's land asking for medicine, food or searchers for a lost child, a council meeting someone missed,
a worn-out colonist asking for days off at the worst time, a doctor who just lost a patient, someone convalescing who
wants to be back at work, family who want a say before an operation, a squabble that turned into a feud, an elder who wants to
put down the tools, a child's question, a broken promise, the same meal every day. Many answers call on whoever is
best placed (the doctor, a forager, a tracker, the best talker), and their skill decides how it goes.

### Social: friends, rivals, comrades and mentors
Colonists form lasting bonds out of how they feel about each other and what they live through, and those bonds
grow, sour and end over time:
- **Friends** and **rivals** form when two people keep liking (or loathing) each other, then drift with how
  they feel.
- Everyone who fights in the same battle becomes **comrades**. That fades slowly between battles.
- A skilled, passionate colonist may take a keen learner on as a **student**, teaching them every day until they
  graduate at level 11.

Most days something small happens between two people (67 kinds of social event): a late talk, a shared joke, a prank
that lands badly, a snub at dinner, a squabble over chores, war stories, a lesson, a contest between rivals, a
handmade gift, gossip that gets found out, an unexpected truce. Life brings its own: a vigil at a friend's sickbed,
an evening at a camp on the map, a quarrel over what the council decided, fears confided in the night, grief carried
together, a peace offering after a quarrel. Each one nudges their bond. In conversation they share smaller moments
too (about a hundred kinds, 40 of them frictions), shaped by who they are (night owls keeping each other company,
brawlers comparing scars, an iron-willed colonist steadying a nervous one) and by what's going on (visiting the sick,
talking over a council meeting, watching a camp's fires, confiding a worry). Moments, frictions and events keep to
one story between the same two: no compliment right after an insult, no warm evening the day after a falling-out.
Three to six people held together by strong friendships form a **circle**, and people outside every circle sometimes
feel left out.

Bonds change how people act. Friends and circle members back each other's wishes and petitions and vote for each
other; rivals object to each other's wishes and vote against each other. The council's own decisions feed back
into them: being passed over for a rival, losing an election, a fight, a grudge from a dilemma or arguing over a
decree can start a rivalry, while backing the same petition or making up after a quarrel builds a friendship. Some
dilemmas only come up between friends, rivals or comrades. When someone dies, the people closest to them (and their
rivals) write about it. The **Social** tab shows every bond with its strength and history, and every circle.

### Story: a page for every colonist
Select a colonist and open the **Story** tab in their inspect panel (next to Bio, Social and Gear): their trust,
office and earned names, their life goal and how close they are, their personal story and what it waits for next,
their friends, rivals, comrades, students and the people they've lost, their quirks, vendettas and the secrets the
colony knows about, their last dreams and the latest lines of their journal.

### Life goals
A few days after settling in, each colonist sets their heart on something to do before they die: master a craft,
marry for love, have a child, find three true friends, train a student until they can stand alone, make peace with
a rival, earn a seat on the council or a name the colony remembers, a bedroom fit for a noble, bring down twenty-five
enemies, raise a hundred and fifty structures, tend a hundred wounds, bring in four hundred harvests, or simply live
three more years. Children have goals of their own (Biotech): a best friend, getting good at a skill they love, and
for older children growing up, which comes true when they come of age, so a grown-up's goal follows. The old may
want a grandchild, one more year or two students who can stand alone; believers a role in their faith or the whole
colony sharing it (Ideology); some only want to feel at home or free again, the heart's need well met for ten days
in a row. 63 in all, listed on the Almanac page "every life goal". Goals fit the
person (their passions, traits and situation) and grow out of what happens to them: a battle makes a warrior, a
brush with death a healer, a wedding someone who wants to marry, a birth someone who wants a child, a lost election
someone who wants a seat, loneliness someone who wants friends, and seeing a friend reach their goal can make them
want the same. Something big can change their mind, and when someone dies before reaching theirs, the person who
loved them most may take it up and finish it for them. The Social tab shows everyone's progress and why. Achieving one is a big
moment: a letter, a long good mood, trust, often an inspiration, friends who are glad for them. Dying before it is
remembered: the chronicle notes what they never got to do, and their friends write about it.

### Personal stories
A few times a year, one colonist's own story begins, and plays out in three or four stages over days to a season.
There are 16 of them: the long way home (away for days, missing someone; home; the road told), somewhere to belong (a
homesick newcomer finds a friendly face, a room, roots), a companion (an animal that follows its master about, until
they bond), grudging respect (rivals who are good at the same thing), old wounds (former lovers, family at odds, a
friend who let a secret slip), an old debt (a rescue or a kindness repaid with something made by hand), second chances
(a colonist who gave up on the council), the apprentice's masterwork, a calling (a child or teenager taken with what a
grown-up does well), passing it on (an elder's craft handed down), a long grief, a secret told (to a friend first, then
everyone), a fear faced (an enemy who got away, a dread of the dark or of storms), the recurring dream, a grudge laid
down, and within reach (halfway to a life goal and doubting it). Each starts from what Rimfolk already knows about the
colonist (their losses, rivals, secrets, life goal, feelings, skills, animals, trust) and moves on only when something
happens in play: a skill reached, a talk between the two of them in the social log, a piece of excellent quality made,
a fight at home come through, days with a good mood or a room of their own. Every stage is written in their journal in
their own voice (and often in the other person's), and in the chronicle; the end brings a letter that tells it all,
modest earned moods, and sometimes something that lasts: a friendship, a mentorship, a rivalry ended, an inspiration, a
quirk or a name (the Wanderer, Steadyhand, the Unafraid). Stories can fizzle too, gently, and a death mid-story is
remembered by the one who shared it. At most one per colonist; how often is a setting (off, rare, normal, frequent).
The Almanac page "personal stories" lists them all.

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

Others simply grow out of who someone is: a grower who talks to the plants, a handler who names every animal, a cook
who sings at the stove, an early riser, someone who hums at work, collects pretty stones, can't stand a mess, feeds
the birds or remembers every birthday, and after a night in the cold or under the stars, someone who is always cold
or goes out to watch the stars. Some bring a small knack while they last (slightly faster plant work, fewer food
poisonings from that cook, animals tamed and trained more easily). 29 quirks in all. Picking one up and letting it
fade are written in the journal in the colonist's own voice.

### Dreams
Colonists who sleep (or lie unconscious) for a few hours sometimes wake from a dream (there are 117), and tell each
other about it. Dreams grow out of their lives: the raid two days ago, the friend they buried, the rival who insulted
them, the wish the council ignored, the leg they lost, the skill they're learning, the dead they miss, their partner,
their dog. They grow out of what they feel, too (an argument all night with the one they're angry at, a debt repaid
to the one they're grateful to, dreams of fear, shame, loneliness, pride or hope), out of what ails them (loose teeth
with a toothache, breathing underwater with a cold), and out of what's around them: a camp on the map, a council
meeting, sickness, famine, fighting or a fair in the region, and the seasons. Most are just a passing mood and a line
in their journal (hover the mood to read the dream). Now and then one changes something: an inspiration, a lesson, a
good night's rest, a nightmare that leaves them shaken, a dream of victory that steadies their aim, a softened
rivalry, a sudden wish. And rarely a dream is an **omen**: a few days after someone wakes up talking about bells and
pack animals on the hill, a caravan arrives; after torches in the dark, a raid. Recent dreams are listed in the
Social tab.

### Strangers, beasts and pets
Colonists meet everyone else on the map, too:
- **Visitors and traders** bring news from the road, swap recipes, admire someone's work, flirt, insult, haggle
  and cheat. Warm moments make them like your colonists and can raise goodwill; insults can end in a fistfight.
- **Guests and prisoners:** a noble's airs, a refugee's gratitude, kind words through a cell door that soften a
  prisoner, taunts that sour everyone.
- **Fallen enemies** lying downed after a fight beg, curse and pray; how your people react depends on who they are.
- **Pets and wildlife:** a pet's welcome after a hard day, stolen dinners, a dead rat as a gift, a thrumbo at the
  tree line, wolves at night, a small creature that follows someone home.
- **The land, the seasons and the weather:** shade shared with a wild animal at desert noon, tracks in the tundra
  snow, eyes in the jungle undergrowth, lights over the marsh, an animal on the mountain ridge, the tide line on the
  coast; a traveler's trick against the sand or the cold, the first green of spring, rain waited out with a pet, a
  shape in the fog, a pet frightened by thunder, other names for the stars on a clear night. Rimfolk reads the land
  from the biome and the tile, so other mods' biomes count too. 156 kinds of encounter in all.
- **The council decides** when it gets harder: a visitor who wants to stay, a rude guest, a trader who wants an
  heirloom, beggars at the gate, a wounded raider who's still breathing, a predator's den, a child who wants to
  keep a wild animal, an old acquaintance who knows a secret.
- **Nemeses.** An enemy who strikes down, maims or kills a colonist and gets away is remembered. A raider who
  fled may ride with the next raid from their people; a wild animal that mauled someone gets a name and keeps
  prowling. The victim dreads it or burns for it, may make settling the score their life goal, and feels it
  when the nemesis is captured or finally brought down.
- **Pets as companions:** an animal that fights beside its person is remembered for it, and a named animal that
  dies gets a farewell.
- **From your other mods:** Hospitality's guests, visitors of the Empire and the Vanilla Expanded peoples, traveling
  mages of A RimWorld of Magic and Alpha Animals' creatures each have moments of their own, and with Medieval
  Overhaul visitors notice the colony's cheese, wine and hearth (see "Playing with other mods").

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

### The colony calendar
The days that matter to this colony, written down as its history happens: its **founding day**, its **first good
harvest**, **the first snow** (which comes with the first snow of each winter, not on a date), **a battle it
remembers** (a big raid seen off, ours who fell, a nemesis brought down), **the death of someone it loved**,
**weddings**, the **arrivals** of its first members and the **birthdays** of its long-serving colonists, up to a dozen
of the most meaningful. On the **Calendar** page under History you can also **name days of your own** (up to 8) as a
feast, a day of remembrance, a rest day or a day of games, rename any day, or change how it's kept.

When a day comes round, a letter asks whether to keep it this year: a **feast** is a party at the gathering spot,
**games** an afternoon of recreation, a **rest day** turns the rest of the day's work hours to recreation (the
schedule goes back the next day); a **remembrance** is kept with a quiet, bittersweet mood for those who knew the
person. A birthday comes as a message and a small lift for the colonist. People write about the day in their
journals, talk about it in the social log, and mention it the evening before; the World page shows the days coming
up. All the moods are small and share the budget of good moods. Kept **three years running**, a day becomes a
**tradition**, with the same letter, moods, chronicle and annals as the council's seasonal traditions, and letting it
pass then disappoints everyone. With Ideology, the letter suggests the faith's own rite where one fits (a funeral rite,
a feast or festival), and holding it keeps the day. With Rimfolk events on, founding day is the founding event's to
keep. An older save fills its calendar in from the chronicle, its dead and its nemeses.

### Journals
Every colonist keeps a journal in their own voice: wishes granted and ignored, promises, decrees, raids, losses,
names earned, friendships made and lost, lessons learned. Ordinary days get written down too: now and then, something
that's really on their mind that day (a meal eaten off their knees, a cold night, a long talk with a friend, a
corpse nobody has buried yet) becomes the evening's entry. Read them on the Journals page under History, or from a
colonist's page in the Roster. The journals of the dead and departed are kept (the thirty written in most lately).
Every kind of entry has lines in each voice (plain, warm, blunt, cheerful, gloomy and cold), and the ones written
most often have many in each.

### Ambition: council meetings
About once a quadrum the council actually **meets**, and it's **announced first**: who called it, where (around
the dining table, in the rec room, by the campfire or in the open), when (six in the evening as a rule; an hour
or two from now when you call one from the Ambition page, or the next evening if that would be at night) and
what's on the table. The World page's coming-up list and the Ambition page count down to it, a reminder comes an
hour before, and the Ambition page can hold it now or put it off a day. No meeting starts at night.

At the set time the chair **calls everyone** with a say. Colonists put down ordinary work and walk over, like
party guests, and sleepers are woken by someone on the way (they come late and grumble a little). No colonist is
pulled from tending, rescues, surgery, firefighting or feeding someone, nobody is called while there's danger, and
children, the sick and the worn out are left to sleep. Drafting someone or giving them an order takes them out of
the meeting at once, and it doesn't call them back. Whoever is away (a caravan, an outpost, another map), drafted,
laid up, too far off to arrive before most of it is over, or too bitter to bother, misses it.

Three colonists pitch an ambition out loud; an idea whose proposer isn't there is read out for them and gets
less support. Others back them, doubt them or wander off topic, and when the meeting ends everyone who came
votes (only they do). The decision letter shows the votes and tells who came, who was late, who missed it and
why. Going with the room's favorite pleases its backers; overruling a clear majority costs a little trust.

- **Thin meetings:** with fewer than half there (and never fewer than two), the meeting is put off once to the
  next evening; if it's thin again it's decided anyway, by few, and earns less trust.
- **Missing it:** through work or travel, a small mood dip (-1 for a day). Having your own idea turned down while
  you were away stings more (-3 for two days, and a little trust). Those who stayed away on purpose shrug it off.
- **No calm evening:** if the council can't sit down for three evenings running (danger, nowhere to meet), the
  chair looks the proposals over alone, and everyone else trusts the council a little less for it.
- With only one colonist, the council "meets" in passing. The setting *Meetings call people from their work*
  (on by default) can be turned off; then colonists come only when they're free, and attendance is still kept.

54 ambitions, from *a full larder* and *hold the line* to *clean hands*, *law and order*, *the brewmasters*,
*stone and mortar*, *a master healer*, *a seat of power* and *school days*. Achieving one lifts everyone's mood
and trust; one that was already more than half done when the colony chose it counts for less (down to 60%).
Only those the colony can achieve now are pitched, and some fit a colony's size or era: *a home, not a camp* and
*nobody is the only one* for a small band, *the great feast* for a crowd, *the healing garden* before electricity,
*word from outside* after it. Some answer the newer systems (*a calm season*, *a healthy season*, *a proper
infirmary*, *friends at the camp*, *a table for everyone*). A few come in stages (*a home, not a camp*, *a proper
infirmary*, *ready for every season*): only the current stage counts, and a message says what comes next. Some take
seasons (*ready for every season*, *a year of steady nerves*). The Almanac page "council projects" lists them all.

### Build: room plans
108 ready-made rooms, grouped by purpose. 28 kinds of room come in several sizes and kinds, picked in the
planner: a cozy, standard or roomy bedroom, or a sleeping hut for a tribe; a dining room for 4, 8 or 12; an
infirmary, two hospitals, a ward or a surgery; a prison cell, a furnished cell for a prisoner you hope to recruit, a
shared cell, or a cell block of three cells off a corridor; a kitchen with one stove, two, or a butchery behind an
inner door; a scholar's study, laboratory, large laboratory or research wing; a forge, smithy or armorer's
workshop; a study, library or reading hall; a games corner, rec room or recreation hall; a tomb, crypt or
catacomb; a weapons locker, armory or arsenal; and smaller and larger greenhouses, breweries, barns and kennels,
freezers, storerooms, workshops, tailor's shops, drug labs and galleries. The **council chamber** is a room for
the council's meetings: a council fire for a tribe, or a table for 6, 10 or 20 (the council meets in the biggest
dining room). Rooms of their own: tavern corner, butcher's room, heat shelter (no power needed), stonecutter's and
component workshops, art studio, crematorium, guard post, battery room, and a longhouse and a great hall for
tribal and medieval colonies. With the DLCs: throne rooms from an audience chamber to a grand throne room, and a
meditation room (Royalty); a shrine, chapel or temple (Ideology); a schoolroom, playroom, three sizes of nursery,
a mechanitor's bay and a gene lab (Biotech); a containment cell (Anomaly). With Dubs Bad Hygiene: a small,
standard or family bathroom and a shared washroom with stall doors, a bathhouse, sauna, laundry and a master
suite with its own bathroom; with Hospitality a guest room. A room shows up once your colony can build its main
piece; tribal and medieval colonies get sizes of their own (a council fire, a forge, a scholar's study), and the
research wing waits for the multi-analyzer. Benches people can sit at come with a chair on the work spot.

Pick one from this tab or from the **"rooms"** button in the bottom bar. A color-coded preview
follows the mouse, and a planner panel lets you:
- pick the **size**, and the **style**: *spartan* (the essentials, no floor), *comfortable* (the standard
  furnishing) or *lavish* (more plant pots, pedestals for sculptures and a rug where your mods have them, carpet
  or fine stone tiles; hospitals and kitchens keep their sterile floor)
- build it in a **theme**: *rustic* (wood, benches and stools, straw matting or planks), *stately* (marble or
  granite, polished stone or fine carpet, armchairs, candlelight), *spartan* (steel, simple beds, concrete), *cozy*
  (carpet, standing lamps, soft seats, plant pots from the comfortable style on), *tribal* (fur beds or hide
  bedrolls, cushions, torchlight, straw matting, wood and sandstone) or *spacer* (steel, sterile tiles, an autodoor,
  the most advanced furniture you've researched). The style decides how much goes in, the theme what it looks
  like; the layout stays the same, since a piece is only swapped for one of the same purpose, size and work spot.
  A theme uses only what the colony can build and has enough of, and the furniture of Vanilla Furniture Expanded
  (with its Spacer, Art and Architect modules), Vanilla Factions Expanded Medieval 2 and Empire, Dubs Bad Hygiene
  and Alpha Prefabs when they're there
- **turn** the room (Q / E or buttons), **mirror** it and **move the door**
- choose **materials**, or let the planner do it: it picks from what you have, stored or lying around, something
  there's enough of with a little to spare. Stone blocks come first, then wood, bone (anything but walls) and
  steel; silver, gold, plasteel, uranium and jade only when nothing else will do. Walls get stone
  before wood when there's enough. The walls, door and floor take their share first, so the furniture doesn't
  count on the same blocks. Your own picks are remembered for that room while you still have enough of them, and
  **Use what we have** starts over. A material shows green with some to spare, amber when it only just covers
  the room.
- **swap** furniture for another piece the colony can build; the **cost** of the whole room is shown against what's
  stored and lying around, in green or red
- **clear the site**: cut plants and trees, mine rock (inside, in the doorway and just outside the door),
  replace your own buildings in the way, smooth natural stone. Pieces blocked by rock go down by themselves
  once it's mined.
- build on **uneven ground**: where part of a room is on water, marsh or mud, the cells that can't hold a wall,
  the door or a piece of furniture are outlined in amber and left out, and the rest goes down. The message says
  how many wall segments and pieces were left out, and whether the door was. Only when nothing at all could be
  built does the planner refuse.
- let the council **find a good spot** near your colony and the rooms it belongs next to, and place it there. It
  only suggests spots where all of the room can be built.
- **upgrade a built room**: hover a room you already have and click. The plan turns to the wall with its door,
  counts what's there and adds only the missing furniture; the floor goes only on bare ground. The panel shows
  the room's impressiveness as it is now.

**Your rooms:** "Save a built room as a plan" turns any enclosed, rectangular room with a door into a plan of
your own, with its furniture, wall material and floor. Saved rooms are kept with the mod's settings, so they're
there in every game, and you can delete them from the same list.

Every layout is checked in all four turns, mirrored and not: beds have their heads against a wall, chairs face
their table, every bench has its work spot free, and nothing blocks the door. It's checked again in each of the six
themes, and without the DLC and mod furniture it doesn't need: a missing piece is left out, or the next one on the list is used, and a room
whose main piece is missing says what it waits for. If a modded bench is bigger or its work spot ends up in a
wall, the planner turns that piece or leaves it out and says so.

It places ordinary blueprints, so your builders handle the rest. Every plan comes with good-practice tips.

**The game's build menu** shows your stock too: when you pick a material for a wall, door or piece of furniture,
each one reads like "Steel wall    420 (+85 lying around), enough for 84". You can switch this off in the settings.

Colonists also sometimes add **personal touches**: a plant pot, an end table or a lamp planned for their own
bedroom.

### Almanac
An in-game knowledge base: good practice and the numbers behind it, by topic, searchable, linked from the
advisor's warnings and to room plans. The **Your mods** category explains the mods in your game (over 100 pages
covering a 216-mod medieval list): what each adds, how to get the most out of it in a medieval run, pitfalls and
conflicts, and how it works with Rimfolk. Each page only shows when its mod is active. **Start here** (the first
page) gives Rimfolk in a few lines with links to the main systems' pages, and **Checking that it works** is a short
checklist of where to look to see the newer features at work.

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
A running history of wishes, promises, petitions, meetings and ambitions. It keeps 300 lines; once it is full, the older
everyday ones make way first, so the colony's losses, ambitions and life events are still there years later.

## Fits your world (medieval runs too)

The council only suggests what your colony can actually do right now:
- **Research and tech level:** nothing that needs unresearched tech, and nothing electric before Electricity.
- **Purpose, not names:** buildings are recognized by what they do. A medieval colony gets torch lamps,
  campfires, spike traps and bedrolls; an industrial one gets standing lamps, heaters and turrets. Modded
  furniture that does the same job counts too.
- **Materials:** room plans pick what you have enough of, common stone and wood before anything precious, and
  you can change every material.
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
can't work alongside your other mods, and the "Deeper game hooks" setting turns them all off at once. One vanilla
def gets an addition: the global work speed stat gains the strain part (feelings and strain), which does nothing
while that feature is off; nothing vanilla is changed or removed. A few things it does on purpose:
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
- **Vanilla Social Interactions Expanded:** works alongside, and feeds Rimfolk's bonds (see below). Its
  "aspirations" are short inspiration tasks, not life goals, so nothing doubles up.
- **Guests, prisoners and visitors** (Hospitality and the like) are left out of meetings, votes and council
  talk.
- **Removing a mod mid-game:** room plans that used a material from that mod fall back to a default material,
  and people who vanished with it are quietly forgotten.

### Home life mods
Each of these switches itself on only when its mod is there, and only asks for what the colony can build now:
- **Dubs Bad Hygiene:** bathroom, bathhouse, sauna, laundry and master suite room plans that pick the era's
  fixtures (a latrine and a water tub at first, a basin and a bathtub with Plumbing, a toilet and a shower with
  Modern Fixtures). The advisor
  counts toilets (about one for every six colonists) and notices when nobody can wash or people fall sick from dirty
  water. Colonists ask for a latrine, somewhere to wash, a real bath (a water tub or a sauna isn't one) or, once
  Heating is researched, hot water, and in fall and winter a sauna; their journals mention hot baths, cold water and
  the walk to the bushes. The burn pit and the sauna heater are never offered as a bedroom heater.
- **Hospitality:** guests are visitors with stories of their own: they praise a fine guest room or find fault with
  a poor one, get shown around, tell of home at supper, sit up at night with someone who can't sleep either, lend a
  hand, haggle and say thank you, and most of it goes in the colonist's journal. A guest whose visit goes well may ask
  the council to stay (taken in Hospitality's own way, as its join request does), and an unhappy one may bring a
  complaint. With no guest bed in the colony, a kind or sociable colonist may wish for one, most often while visitors
  are here; once there is one, they may wish for a guest room worth the name, with a guest room plan to build it.
  A guest bed never counts as a free bed for a colonist.
- **Medieval Overhaul:** its pies, stews, slop, cheese, wine, ale, smoked meat and library leave memories that become
  days in the journals, and visitors notice the colony's cheese, wine, hearth, oven, hives and books once it has them.
- **Vanilla Cooking and Brewing Expanded:** grills and gourmet dishes count as fine or lavish meals for birthday
  feasts, better-meal wishes and journals; grills ruined by the cold don't count. Tea, coffee and soda aren't
  "something to drink" for a brewer or a colonist with a taste for chemicals. Cooks may wish for a grill in summer,
  canned goods count as food that keeps, and someone ill may wish for a pot of tea.
- **Vanilla Outposts Expanded:** a wish waits while its colonist is posted at an outpost (up to five days, then it
  lapses and nobody is blamed), and people out there write home in their journals now and then.
- **RimHUD:** seven lines for its pawn panel (trust, open wish, office, life goal, strain, feelings, the heart's
  needs), with strain in its band's colour, each feeling in its own and the heart's needs green when all is well, plus
  five bars: strain (filling and reddening as it builds, marked where it becomes strained, worn down and near
  breaking) and belonging, purpose, safety and freedom (marked where each starts to be missed). In RimHUD's settings,
  Content tab, use "Add third-party". To keep the panel short, put them in the column under the skills.
- **Interaction Bubbles:** social events and encounters with visitors, traders and animals now go into the social
  log too, so they show as bubbles like Rimfolk's moments and wish talk.

### People, beliefs and magic
Rimfolk reads these mods by name while the game runs: with one missing, its part simply never comes up.
- **Vanilla Social Interactions Expanded:** its lessons strengthen a mentorship, and three lessons within 15 days
  from a skilled colonist (10+) to a keen learner (8 or less, with a passion) become one. Venting to a friend,
  being saved in a fight, being tended back to health, childhood friendship and fistfights move the matching bond;
  new best friends are noted in the chronicle and both journals. Where both mods do the same thing (cheering
  someone up, plain lessons), Rimfolk's own version happens half as often, unless that feature is off in its
  options.
- **A RimWorld of Magic:** healers (priest, druid, paladin, shaman, apothecary) are favored for physician, fighters
  for marshal, the commander and entertainer for speaker, the entertainer and empath for steward. Mages like
  study hours, fighters like drills; a mage may ask for a magic circle once you can build one, and dream of
  magic. A mage and a fighter talk shop, two mages compare schools, two fighters compare forms and a healer eases an
  ache, and these become days in the journals. Life goals: grow ten levels in one's class, learn or deepen six
  workings or techniques, become a scholar of the art, or tend sixty wounds as a healer. Traveling mages and fighters
  visit and share a working or a form.
- **Vanilla Traits Expanded:** its traits have opinions on decrees (workaholics love long hours, rebels hate
  being told, the neat love a clean sweep), sway elections (brave marshals, no squeamish physicians), shape how
  people feel about the colony's ways, and voice wishes (academians want books, snobs fine clothes, tycoons
  silver, the wanderlust-struck a caravan). Some speak in their own tone.
- **Memes (Ideology, Vanilla Ideology Expanded, Alpha Memes):** a colony whose leader holds an aristocratic,
  royal or god-emperor creed doesn't vote: the leader names the officers. Collectivists accept rationing and long
  hours, pacifists hate drills, monastic creeds love austerity, party-lovers love festivals, and creeds shape how
  people feel about the colony's ways.
- **Xenotypes (Biotech, Alpha Genes):** people who never sleep don't ask for beds or bedrooms, people who never eat
  don't ask for meals, and long-lived genes no longer make a grown adult read as a teenager.
- **Vanilla Skills Expanded:** a life goal to earn an expertise in a skill they love.
- **Alpha Animals and other animal mods:** the hunting wish only picks ordinary game, nobody goes after a giant
  beast on their own, and the predator by the colony is a wolf or a bear, not a monster. Colonists meet Alpha
  Animals' creatures by name when one is calm and wild on the map (angel moths, the aves, the thunderox before a
  storm, bouldermits with their rocks, sky eels, the four-eyed dusk rat, a gallatross passing), and Alpha Biomes'
  lands have encounters of their own (the gallatross graveyard, idyllic meadows, the mycotic jungle, the ocular
  forest).
- **The Empire and Vanilla Expanded peoples:** world events about a particular people: the Empire's levy (Royalty),
  deserters on the roads (Vanilla Factions Expanded - Empire), the republic votes (Classical) and gold fever
  (Settlers). Their visitors talk about court, the senate and the frontier.
- **Which of these are in your game:** the Almanac's "What Rimfolk does with your mods" page is a table made when the
  game starts, from what Rimfolk finds: each mod it works with, whether it's here, how much is written for it, and
  what Rimfolk does with it. The self-check in the debug actions uses the same list.
- **PokéWorld (any version):** Rimfolk's animal events follow its wild Pokémon slider and generation switches, a
  Pokémon caught with a ball (or kept in the PC) counts as tamed, animal lovers may ask to catch a new species, and
  the first catch of a species goes in the catcher's journal. Four Almanac pages explain the mod.

## Why am I feeling this?
Every mood Rimfolk gives says what caused it, under the description in its tooltip: whose wish came true (or
didn't), which council decision, which decree and how long it has left, which visitor, secret, nemesis or
moment. The trust mood lists what moved it lately.

## Rimfolk events
Every few days, something happens to the colony from outside: Rimfolk's own events. They join your storyteller's own
random events with about the odds of one of the game's own (a setting scales this, and they never make up more than
half of its everyday events, so traders and travelers keep coming; they can also come on Rimfolk's own timer instead,
as often as you choose). Each is written for tribal, medieval, industrial and spacer colonies alike, and finds and
visitors fit your era (a medieval colony meets medieval folk and digs up
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
- **Everyday things.** None of these ends in a fight. Merchants **stranded** on the road (send your best crafter to
  help, or sell them the materials), a neighbor's **order** for goods you keep in your stores (have the whole amount
  ready when they send for it in five days; the World page shows when), a **tinker** who mends every worn coat and
  weapon for silver, **neighbors in need** of medicine (give, sell or keep it), two neighbors asking you to settle a
  **quarrel** (take a side, or let your best talker find a middle way), **growing weather** that puts open fields
  days ahead, a **warm spell** in spring or fall, an **old grave** or a **box of old letters** turned up outside,
  **runaway lovers** asking to be taken in, a colonist's real **family** coming to visit (and maybe staying), and a
  **fortune teller** (the cheerful hear good news more often, the gloomy and nervous bad, the very clever none).
- **The colony's own story.** A colonist known by an **epithet** (the Healer, Greenhand, the Builder) is sought out by
  name for a day of help; a traveler who once did what a colonist has set their heart on (their **life goal**) stops
  for the night; a small band of the people someone swore **vengeance** on camps nearby and can be drawn into a fight
  at home (with battles on, and only if you choose it); and once a year, on **founding day**, an old friend among the
  neighbors comes to mark it, with a line on who has been here since the first days, who was lost, what the colony
  is known for and the customs it keeps.
- **With the seasons, and over days.** A **harvest fair** at a neighbor's town in the fall (send a cart of your crop;
  the silver comes back days later, and your best grower may take a prize), a **traveler on a winter night** who
  asks to share the fire (and leaves something in the morning), **the thaw** where winters are hard, and a **caravan
  to a far market** that takes your surplus and brings back what the colony is short of a week later. The World page
  shows each of these while it's on its way.
- **With your mods.** Only when the mod is active: **shards from the sky** that leave magicyte (A RimWorld of
  Magic), a **strange creature** your best handler can try to win over (Alpha Animals), travelers asking to use your
  **bath** or sauna (Dubs Bad Hygiene), and a **guest of note** who writes ahead and judges your guest room when they
  come (Hospitality).
- **More of the world (26 more).** A **rockslide** that closes a way through the hills and bares a seam of ore, a
  **warm spring** or steam vent after a tremor, a **lightning-struck tree**, **wild bees**, a **fallen star** to forge,
  driftwood after **high water**, a **sign at dawn** (tribal and medieval); an **old adventurer** selling their gear, a
  **mapmaker**, a **silent guest**, a **limb-maker**, a **merchant's venture** that pays back later (or never), a
  neighbor who wants your **prisoner**, a **wounded stranger**, an **envoy** offering peace at a price, the **lord's
  tithe** (medieval), a **crashed drone** and a **signal** (industrial and spacer); a past that knocks (**recognized**
  from a wanted poster, an **old commander**, a **former patient**, an **old map**), a **child's find**, a **pet's
  present**, a **sealed crate**, and an **arcane tome** for sale (A RimWorld of Magic). In many, who you send decides
  the odds: a skilled hand makes a gamble likelier to pay.
- **The land, the season, the era (26 more).** Events that only come where and when they fit: a **mushroom flush**
  after rain, **white nights** near the poles, a **desert in bloom**, the **long rains**, an **ice fair** on a frozen
  river, **birds returning** in spring, the **rut** in the fall, the **first snow**, the **longest night** (a fire kept
  all night, wood from the stores) and an **eclipse**. Callers fit the era: a **spirit singer** for a tribal colony, a
  **scribe** for a medieval one, a **photographer** or a **tourist** for an industrial or spacer one, and a
  **wandering cook** for anyone. Some come for the colony's own life: a **bonesetter** while someone nurses an everyday
  ailment, a **fiddler** while someone is strained, **guests who want to watch a council meeting**, a **peacemaker**
  for two colonists at odds, a peddler with a **taste of home**, an old acquaintance come to make **amends**,
  **headstones** for the colony's graves, a neighbor's **bridge** to rebuild, a **sick herd**, and a **feast** or a
  **quarrel** with a camp nearby. Many answers call on someone with the right skill or trait; when nobody fits, the
  answer says why.
- **Strangers who make camp.** Now and then a group that isn't hostile settles part of your map for days to a
  season: a **hunting party** following a herd, a **refugee family**, **prospectors**, **pilgrims** at rest,
  **traders** with a stall, a **traveling smith**, a **hermit** who builds a hut, **traveling healers** who ease an
  ailment, a **procession of the faithful** who bless the colony (naming their creed with Ideology), **traveling
  players** who put on a show, and **surveyors** who may show you a seam of ore. Refugees come more often while a
  famine, plague, war, flood, drought or hard winter runs in the region, and say what they fled; pilgrims follow a
  holy year, surveyors a new road or a rush. They build the camp themselves,
  cook at their fire, hunt and fell trees around it (never near your home area), trade, sometimes help, and may ask
  to join. Let them stay, set terms (a fee, a share of their meat, keeping to their side, a time limit), hire some of
  them, ask them to move on (a gift helps), or drive them off. Tension grows the longer they stay and the more they
  take; trade, gifts and friendships ease it. The World page shows the camp and lets you talk to them. One camp at a
  time, never in a colony's first twenty days, with its own switch.

Some events pick up where an earlier one left off: the **deserter**'s old unit tracks them down; the **lost child**
you walked home comes back years later, grown, with a gift; a **champion** you beat comes back for a rematch (or one
who beat you, to defend the title); the **great beast**'s young turns up a season after it fell; **pilgrims** you fed
send their thanks down the road. A neighbor whose **order** you filled comes back as a **regular buyer**; the
**runaway lovers'** family comes asking after them (talk, a gift, or a grudge, never a fight); a relative who
**visited** writes home news, and can be invited to come and live here; the colonist the **fortune teller** saw bad
things for has a fright that comes to nothing; and the neighbors whose **quarrel** you judged remember it.

**Stories** are longer: eight chains told in three to five stages over days and weeks, where your answer and some luck
decide what comes next and how it ends. **The door in the hill** (open it, study it first, or sell the way to it: an
empty vault with writing on the walls, a sleeper in an old casket who may stay, go or flee, or a hoard whose owners
come for it); **the fugitive's debt** (hide a stranger at night, then lie to their pursuers or give them up: the debt
repaid, a gift with a past, or revenge); **bad blood** between two families of a neighbor town (mediate, back one,
sell to both or stay out: a wedding at the colony, a blood feud, or hired blades on the road); **the preacher** (let
them preach, argue, or send them on: a colonist who follows them, a divided colony, or a pilgrims' hill with the
colony as its last stop); **the lost expedition** (outfit a rescue, send word, or let the explorer go back alone:
survivors and finds, a single survivor with a warning, or silence); **the lost letters** (a satchel of letters gone
astray: send them on, read them first, or burn them; a love letter, a buried cache or a note of credit may be among
them, and a thank-you, a share or a new colonist may come back); **the stranger's seeds** (plant them with care, out
of the way, or turn them down: a rare harvest, a weed that chokes the fields, cuttings to sell, and the stranger
coming back to see what grew); and **the play** a traveling writer makes of the colony (tell them everything, only the
best, or nothing: a hit along the road, an admirer who comes to visit, a flop or a scandal). Each ending gets its own
letter and a line in the chronicle; a path brings at most one fight, and only with battles on.

**Missions** are a neighbor's request with clear terms: goods for a **funeral or a wedding** by a set day (meals,
sculptures, stone blocks, beer), a prize animal that **bolted** onto your land to tame and hand back (or buy, or
keep), a **shrine** for their pilgrims (a sculpture of good enough quality under the open sky), materials to
**rebuild** a town after a fire, a storm or a flood (wood, stone blocks, cloth, and steel or components for an
industrial town), or **provisions** for a hard winter, a dry year or a sickhouse (raw food or pemmican, meals,
medicine, cloth, firewood). The peoples that think best of the colony offer more: an **invitation** to their feast, a
wedding or midwinter (send at least two of your people there by caravan, and come home with gifts), an
**apprenticeship** with one of their masters (a colonist stays at their town for a day and a half and comes back about
two levels better at the craft), a **dignitary** to host for a few days (a bedroom of a set impressiveness with one
bed nobody else uses, checked every hour of the stay), and **refugees** to see through (two meals each a day and a
roof for a few days; they come from a fire, or from whatever runs in the region). Each shows up in the game's
**Quests** tab to accept or let expire (or as a letter, if you switch that off), runs against a deadline once
accepted, and pays a reward, or costs goodwill if it fails. One mission at a time.

The council window's **World** page (Colony section) shows what's coming (fights the colony knows about and how
soon, questions waiting for an answer, caravans, carts and relatives on their way, the next stage of a story),
missions under way with a bar for each thing to deliver and the time left, who's out there with the colony on their
mind (nemeses at large, people who left and might come back, peoples with a grudge, rivals, a great beast and how
long it'll stay), what each people remembers of the colony, and the latest Rimfolk events.

Answers shape the colony's character, and the events feed nemeses, secrets, mentorships, journals and the
chronicle. Fights come only with **battles** switched on in the settings, when the storyteller sends threats at
all, never on top of another fight, and at a share of the storyteller's own raid strength. In dev mode, every
event can be fired from **Debug actions → Incidents** (they're named `Council_Evt_...`; a story's first stage is
`Council_Story_...`, a mission's offer `Council_Mission_...`).

## World events
Now and then, something bigger than the colony happens in the region around it, and lasts: a plague among the
neighbors, a famine, a warlord's rise, a chief's death and the quarrel over the seat, a town founded or abandoned, a
great fair. These are **world events**, on Rimfolk's own slow rhythm, apart from the storyteller: about one every 30
to 60 days (you choose how often), at most two at a time and never two of one kind. Each lasts from about a week to a
month, names real places, peoples and people (the neighbors' towns, the warlord and his band, your own colonists),
and is written for every era and season.

While one lasts, it changes what the colony meets: Rimfolk events that fit it come more or less often (more traveling
doctors and neighbors asking for medicine in a plague year, more hungry pilgrims in a famine, more old enemies while a
warlord rises), and so do some of the game's own (more herds in a migration, more cold snaps in a hard winter, plague
likelier in a plague year; this needs the deeper game hooks, and only matters where your storyteller picks by
weight). It puts two or three questions to the council along the way, sends its own visitors and caravans, and ends in
a way that depends on what the colony did, with a letter that pays off what came before. The region remembers who
helped: a people the colony stood by in a plague says so when the next hard year comes, and a famous healer charges
the colony half.

- **A warlord rises** among a hostile people: a named war leader with a warband (a world pawn, like a nemesis). While
  he rises, his people's raids hit harder and come more often, he leads some of them himself, and his band raids the
  colony now and then (with battles on). It never needs you to storm his town:
  - **The neighbors can end him.** Peoples at war with his hunt him; the chance they bring him down grows every day
    and with their strength, and the letter says where ("riders of Ashwood brought him down at the ford below
    Kettleford"). Silver sent to the hunters, or a **bounty** on his head, makes it likelier (a bounty hunter may
    collect).
  - **Answers in his letter:** pay him off for a truce (it may not hold), post a bounty, send silver to the hunters,
    **ask an ally to march** (costs their goodwill; they may win, or be thrown back), or **wait him out**: a band that
    gets no plunder breaks up, sooner if the colony beats off his raids.
  - **At home:** kill him and it's over (the one who struck the blow, and anyone his raiders hurt, feel it); take him
    alive and choose: keep him, hand him to his enemies for trial, or let his people ransom him.
  - Every ending has its own letter and chronicle line. Colonists his raiders hurt are glad when he falls, and bitter if
    he simply rides away or is bought back.
- **Hard times:** a **plague** (medicine asked for, refugees, your best doctor sought out by name), a **famine** (food
  asked for, hungry families at the gate, famine prices if you want them, a desperate raid now and then), a **hard
  winter** (firewood, snowbound travelers), **locusts** that may strip the open fields (smudge fires and an early
  harvest help; a famine can follow), a **flood** downriver (only by a river), a **long drought** upriver (water for
  thirsty herds, a builder for a new well, food; a famine can follow if the rains fail), and **sickness in the herds**
  (pen your animals or risk it spreading home, lend a handler).
- **The powers:** a neighbor's **leader dies** and the next one takes over (kind, greedy, harsh or steady, and it
  shows), **two claimants** fight over a seat and the colony may back one, a **war of words** between two creeds
  (Ideology), **peace talks** the colony can host to end a war between two neighbors for good, a **mercenary company**
  for hire (against a rising warlord's people, if there is one), **bandits** on the road to a neighbor's town, a
  **bandit lord's fall** that scatters deserters, wounded stragglers and buried loot (a lieutenant may rise as a
  warlord, or the band turn sellswords), and a **holy year** with pilgrims on every road and fewer fights.
- **Places:** a **new town** founded nearby (a real settlement on the world map, with its first traders), a **town
  abandoned** (gone from the map when it ends, unless you have a caravan on its way or a quest there), a **trading
  season**, a **great fair** (send a cart of surplus, enter your best in a contest), a **pilgrimage**, a **silver
  rush**, a **new road** built past the colony (sell timber, lend a builder, have a say in its route; a trading season
  may follow).
- **The land:** a **famous healer** touring the region, who can mend a colonist's worst lasting condition (a lost
  limb, a chronic illness, an old scar) and teach your doctor; the **great herds**; and with your mods, **strange
  herds** (Alpha Animals) and a **season of wild magic** (A RimWorld of Magic). In an industrial or spacer world, a
  **ship comes down** in the hills: salvage going cheap, survivors, scavengers, and pirates who may come for the
  wreck.
- **Particular peoples** (with their DLC or mod): **the Empire's levy** (Royalty: pay in full and be named in the
  dispatches, pay half, or decline), **deserters on the roads** (Vanilla Factions Expanded - Empire: shelter a
  deserter, hand them over for the reward, or feed them and send them on), **the republic votes** (Classical: back
  the old families or the reformers with silver) and **gold fever** (Settlers: prospectors on every road, one who
  wants to work the colony's stream for a share).

The **World** page shows what's going on at the top, under "In the world": each world event with the people it's
about, what it means for the colony, what the colony has done so far, and how long it may last (hover for what it
makes likelier or rarer). The chronicle records how each began and ended, and the colony's part in it. Settings →
**World events** turns them off or changes how often they come; in dev mode, **Debug actions → Rimfolk** can start,
hurry or end any of them. Modders can add more with a def and no code (see `WorldSituationDef` in
`Source/ColonyCouncil/World/`).

### What the world remembers
Every people out in the world keeps a memory of the colony over the years: a prisoner set free, a wounded guest tended
and sent home, gifts, fair trades, quests and missions done or failed, a camp helped or driven off, help in their hard
year, and on the other side traders robbed, caravans and towns attacked, their raiders killed on your land, their
people dead in your keeping. It remembers what it did in turn, too: fighters sent when you called, relief in a hard
time, raids, colonists killed. Most of it comes from the game's own goodwill reasons, so it works with any modlist.
Each memory fades over the years, the big ones slowest (a gift is mostly forgotten in a few years, a town burned in
a generation), and a people keeps a dozen at most.

What follows from it: an **envoy repays an old debt** (a gift; feast them, tell them they owe nothing, or let a good
talker bargain), a people **calls in a favor** the colony owes (silver, medicine, or a builder's plans), **old
friends send food or medicine** while times are hard (a plague, famine or hard winter in the region, or empty stores
and the sick at home), an **old enemy offers peace** once the feud has gone quiet for a year and a half (a talker can
ask weregild for a colonist they killed), and two peoples ask the colony to **settle their quarrel**. Raiders'
letters say what they remember ("Some of these raiders lost kin on the colony's land, last year."). The **World**
page shows what each people remembers, how it stands with the colony (grateful, owed, wary, holding a grudge), and
its biggest memories; hover for everything. Settings → **Peoples remember the colony** turns it off.

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
friends, or a master and apprentice), *an event around a set day* (someone from the past, a visitor, a great
beast...), *a reputation* and *a secret*. Enemies are nemeses from the first day; debts and hunts show on the
**World** page. A hunt ends when its attacks run out, or when the one leading it dies, joins you, is sold off, or
their people make peace or are wiped out, and the colony gets to feel that. Settings → **Fights in Rimfolk's
scenarios** makes those attacks weaker or stronger (25% to 200%). They come even with battles in Rimfolk events
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
The game's own **Needs** tab gets the same hints: hover a mood and its usual tooltip ends with which mod it comes
from and what to do about it (a setting turns this off).

## Feelings and strain
Mood says how a colonist feels today. **Strain** says how worn down they are, and a fine meal or a nice room can't buy
it off in an afternoon. It's a layer under mood, kept in Rimfolk's own save data: the game still decides mental breaks
from vanilla mood alone, exactly as before, and every other mod sees the same mood it always did.

**How strain builds** (0 to 100, looked at every hour): fighting with enemies on the map (4 an hour, up to 12 a day),
pain and serious injuries, lying downed, deaths and losses (4 when a colonist dies, 5 for seeing an ally die, 8 for
family, each memory once), long days without sleep or free time, bad nights (cold, heat, noise, outside, on the
ground, each once a night), a dark home (not the night sky), hunger, very low mood, decrees they resent and dilemma
outcomes that hurt, and lingering fear, grief, worry and despair. Each source has its own daily limit, and all of them
together add at most about 25 a day. Iron-willed and steadfast colonists shrug off part of it, nervous and volatile
ones take more; psychopaths take nothing from deaths, the bloodthirsty little from fighting, masochists nothing from
pain.

**How it drains:** slowly by itself in quiet hours, faster asleep in a warm bed and during recreation, faster still
when their mood is high or they feel joy, pride, hope or calm. Parties, concerts, weddings, feasts and celebrations
take a chunk off, and so do deep talks and good moments with friends; a good cry (catharsis) takes off 30. A colonist
with a warm bed, free time and a decent mood sheds about 10 a day, so one hard day takes two or three to undo.

| Strain | Band | What it does |
|---|---|---|
| 0 to 29 | steady | nothing |
| 30 to 54 | strained | -3 mood, darker dreams |
| 55 to 74 | worn down | -5 mood, 5% slower work, more nightmares |
| 75 to 100 | near breaking | -8 mood, 10% slower work, many nightmares; from 85, a day off may come |

**A day off.** At 85 or more, a colonist may stop and keep to their room for six to twelve hours ("worn out, needs a
day"): they eat and sleep but don't work, and a letter says what wore them down and what would help. Never in danger,
while drafted or asleep, while hurt or sick, in the middle of caring for or carrying someone or fighting a fire, for
children, nursing mothers, slaves, prisoners or guests, or when fewer than two others could keep working; at most once
in 2 days in the colony and once in 10 days for each colonist. A threat ends it early, and so does needing a doctor or a room getting
too cold or too hot. Afterwards strain drops by about 40 and they feel lighter for a day and a half. To head it off,
declare **days of rest**: everyone works 10% slower for three days, and strain drains about twice as fast.

**Feelings.** Each colonist has one or two feelings that stand out, out of twenty: grief, fear, dread (worry about
what's coming), anger, shame, envy, disgust, loneliness, homesickness, frustration and despair; joy, pride, love,
gratitude, hope, awe, contentment, curiosity and restlessness. They come from the moods a colonist has right now (the
game's, Rimfolk's and other mods') and from what they can see: enemies on the map, a fight the World page says is
coming, a newcomer, quiet days. Feelings add no mood, since that would count the same thing twice. They nudge
instead: a wish that fits becomes up to half again as likely (fear asks for defenses, grief for a grave,
restlessness for a journey, pride for art), moments between colonists go warmer or pricklier, dreams change color,
fear, grief and despair add a little strain, and joy, pride and hope help it drain.

**The heart's needs.** Four values from 0 to 100: **belonging** (friends, a partner, family, good moments, not being
left out), **purpose** (work they love, a life goal, a seat, learning), **safety** (no danger, food, medicine,
defenses, their own health) and **freedom** (time outdoors, free time, varied work, travel, few restrictions). They
give no mood. The one a colonist lacks most makes the wishes and life goals that answer it up to 35% likelier, and
when all four are at 60 or more, strain drains 20% faster.

**Where to see it:** the Heart section of the Mood tab (strain and its band, what built it, what would help, every
feeling with its causes, the four needs), the strain list on the council's Moods page, a line in the inspector
("Strain: worn down · restless, proud"), the strain mood's tooltip and RimHUD. Settings (Heart & health page): feelings and
strain on or off, the day off, the inspector line, and how quickly strain builds (50% to 150%). Switched off,
everything stops and the strain mood and slowdown are gone.

**Safe to add and remove.** The strain mood and the work speed part store nothing, and strain itself is in
Rimfolk's own save data. The only trace in a save is a colonist in the middle of a day off: with Rimfolk removed,
the game can't load that mental state, logs it once and lets them go back to work.

## Ailments and illnesses
Colony life wears on the body too. **Everyday ailments** (32 of them) are small and temporary: they never kill, never
leave a mark, last from a night to two weeks, and say in their description what helps. Doctors tend them like any
illness.

- **Going round** (the game's own disease events, so immunity, genes and the medical tab work as usual; never babies):
  a head cold, a stomach bug, a chest cold in cold weather, hay fever in spring and summer (back to the same people
  each year), pinkeye, and dreamfever, an invented rim fever of vivid dreams. About one every 20 days, more where the
  game's diseases are common, less on gentle storyteller settings.
- **From the day's work**, once a colonist's day holds enough hours of the cause: a strained back after long hauling,
  mining or building, sore hands after long bench work, blisters after a day's walking or a caravan trip, chilblains
  after cold outdoor work, sunburn working bareheaded in the heat, a smoke cough from a wood fire in a small room,
  toothache from sweets and the years, a stiff neck after a night on the ground, a sprained ankle on marsh, mud or
  snow, eye strain from bench work in the dark, a rimrot cough from sleeping in a damp room and ash lung after fighting
  a fire. Also a splinter from felling trees, a rash from a plant's sap, insect bites in swamps and jungles, heat rash
  in sticky weather, frostnip and nosebleeds in bitter cold, athlete's foot from wading, motion sickness on a caravan,
  lice in dirty crowded rooms (they go round, and make typhus likelier), insomnia from worry or late nights, migraines
  after missed meals or worry, cold sores, and heartburn and hiccups after a rich meal or a drink. Never more than two
  at once, never the same one again within days, and never on guests, prisoners or babies.

**Serious illnesses** (14 of them) are rare, behind their own switch, and most can kill: tetanus from a wound left
untended, pneumonia after a cold in someone old, frail or starving, tuberculosis, typhus in dirty crowded quarters,
meningitis, scurvy after weeks without fruit or vegetables, cholera in hot weather, appendicitis, rabies from an
animal's bite, and two invented ones: glassblood from the dust of ancient ruins and hive fever from insectoid stings.
Measles in children, kidney stones, and shingles in people over fifty are hard going but never kill. One kind at a
time in the colony, none within 30 days of the last, and each comes with a letter and a doctor's note saying what it
is and what helps. They follow the real diseases: a quiet incubation where there is one, a course of days to weeks,
after-effects like a lingering cough, nerve pain or some days of weakness, and treatment that counts the medicine the
way the real illness would (industrial medicine matters far more than herbs against tetanus, pneumonia, typhus,
meningitis and rabies; cholera needs someone to keep the patient drinking more than any medicine; tuberculosis needs
weeks of daily treatment and comes back if it stops early; rabies can only be stopped before its symptoms show; only
fresh food cures scurvy). Appendicitis can be cut out with a "remove appendix" operation. Steady tending beats every
one of them. The Almanac's **ailments and illnesses** page has the tables.

With Rimfolk: ailments feed the heart's frustration and serious illnesses its fear (and so a little strain); doctors
and patients write about them in their journals; dreamfever, serious illnesses and many everyday ailments color their
dreams; friends visit the sick; a doctor with a serious case is more likely to ask for a stocked infirmary when
medicine is short, and pulling a patient through lifts both. Settings (Heart & health page): everyday ailments on or off,
serious illnesses on or off, and how often (25% to 200%). Off, nothing new starts; what's running finishes its
course.

### Recovery, doctors and midwives
Getting better takes a while. After a dangerous illness that got serious (the game's own flu, plague or wound
infection as well as Rimfolk's), a major operation (an organ, a limb, the brain), four days or more laid up in bed, or
losing an arm or a leg, a colonist is **convalescing** for a few days to two weeks: a little slower at work and on
their feet, less in bed. Most are glad to be alive (a good mood that fades); the industrious and hard workers are weak
and frustrated instead. A friend who sits by the sickbed (the game's own visit) lifts both their moods, friends walk
slowly with a convalescent or take the load out of their hands, and someone laid up may wish for a visit from a
particular friend (promising it sends them over). Back at work while still weak, a setback can come now and then, and
a setback only makes the recovery longer.

Each **doctor** builds a record: patients pulled through (dangerous illnesses, major operations done well) and
patients lost, shown on their Story tab and in the game's Records. A doctor the colony trusts puts patients in bed at
ease (a small good mood when they tend them) and others write "I'd trust no one else with a knife" in their journals; a
run of losses weighs on the doctor (the heart's grief and shame) and can cost them the trust. The best is called
**Goodhands**. With Biotech, whoever attends a birth as the doctor is its **midwife**: a good mood, the mother's
lasting good opinion and, for the one with the most births, the name **the Midwife**. Two life goals (pull ten patients
through, deliver five babies), a wish (a visit from a friend) and four dilemmas (a doctor's day away after a loss, back
to work too soon, family who want a say in an operation, a midwife's apprentice) come with it. Two switches on the
Heart & health page: convalescence and sickbed visits, and doctors' and midwives' reputations.

## Seen and heard
Rimfolk adds no art of its own, so it uses the game's own effects, sky and sounds to show what its events say is
happening. Under a **comet** the night sky takes a faint cool cast, and a **warm spell** warms the daylight a little
(only the colors: the light level, and everything that depends on it, stays the same). On **firefly nights** small
green-gold blinks drift around colonists outside after dark; **hail** pings off open ground and the crops it hits; a
**camp's fire** sends up a thin line of smoke, with sparks at night; a **warlord's band** raises dust as it marches
in. A **wedding** gets hearts over the couple and the friends nearby, a kept **tradition** a few notes of song, a
clean **victory** a word or two from those who fought, and a **council meeting** opens with a small bell. Now and
then a content colonist hums at work, someone outside says something about the comet or the weather, and on a comet
night an idle colonist in good spirits may lie down to watch the sky. All of it happens only on the map you're
looking at and only on screen, a handful at a time, and one setting (**Visual and sound touches**) turns it off.

## Colony news
Rimfolk's many systems each have small news to tell: a quirk picked up, a personal story moving on, a birthday, a
doctor's good name, a tradition broken, a dream, a day at the camp, an encounter, a name earned. Rather than a
message in the corner for each, the day's smaller news waits for the evening: around six a quiet **Colony news**
letter brings it all, a line each under **people**, **health**, **the colony**, **the world** and **the land**, with a
**Go there** button on every line that has a place. A day with a single piece of news brings a message instead, and
what's waiting is saved with the game. Letters and messages that ask you to decide something (dilemmas, wishes,
petitions, decree requests, meetings) and serious news (a death and its remembrance, raids and battles, nemeses, a
serious illness, a camp arriving) still come at once. The **Colony news** setting can bring back a message for each,
**as it happens**, or turn it **off**, so the smaller news goes only to the message log in the History tab; how much
of it there is at all is still set by **How many messages**.

## Slow speeds
Two speeds slower than normal, **½×** and **¼×**, for planning without pausing: the colony keeps living, gently.
Small buttons sit next to the time controls (drag them by their grip to put them wherever you like; on a
Steam Deck too). The game's own "slower" and "faster" keys also step below normal speed. Picking normal or any
faster speed ends it; pausing and unpausing keeps it. Hide the buttons in the settings if you only want the keys.

## Settings

**Options → Mod options → Rimfolk.** A search box sits at the top: type a word (dreams, raids, letters, illness)
and every matching setting from every page shows under its page and heading, ready to change; click the heading to
open that page there. The list on the left has the seven pages, each with a small number when some of its settings
differ from the defaults. Every switch has a grey line under it saying what you'll notice with it on (hover it for
the full tooltip and its default), and a setting that only matters with another one on is grayed out, with the
reason, until it does. Everything scrolls, and on a small screen at a big interface scale the window shrinks to fit.

**Presets**, under the page list. One click shows the list of what would change, and nothing changes until you
apply it. Each sets only the values listed here and leaves everything else as you set it:
- **Light touch**: Rimfolk in the background. Gentle stakes and everyday pressures, strain builds at 75%; a wish
  about every 4 days (3 open at most), a dilemma about every 15 days, a meeting every 20, rare personal stories;
  moments at 75%, encounters, social events and dreams at 50%; Rimfolk's events half as likely (every 8 days on its
  own timer), world events about every 75 days; important messages only, and new wishes as messages; no frictions,
  no colonist leaving or taking a day off, no serious illnesses, ailments at 50%, no fights from Rimfolk's events.
- **Story-rich**: every system that tells a story switched on (the council's, the people systems but frictions,
  the colony's memory, camps, wishes after events, wishes for others, petitions, speech and visual touches), frequent
  personal stories, a dilemma about every 5 days, moments, social events, dreams and encounters at 150%, Rimfolk's
  events at 150% (every 3 days on its own timer), world events about every 30 days, and a message for everything.
  Moods, pressures and stakes are left alone.
- **Demanding**: harder moods, more frictions and pressures, rarer rewards. Demanding stakes, downsides at 125% and
  rewards at 75%, wishes and ambitions with 75% of the time, demanding everyday pressures, frictions on and moments
  between colonists (frictions among them) at 125%, jealousy, leaving, rising standards (also over the game's
  comforts), good moods adding up slowly and fading sooner in good times, feelings and strain with the day off, and
  strain building at 125%.
- **Default**: everything the three presets above can change, back to how Rimfolk ships. Nothing else.

The pages:
- **Council & wishes**: a quick setup (*Everything on*, *Council only*, *Advisor only*, also with a list of what
  changes); the advisor, wishes and council meetings on or off; **Stakes**:
  - **Gentle**: only upsides (decrees still cost some mood, at half strength, because you chose them)
  - **Balanced** (default): upsides and downsides in proportion
  - **Demanding**: downsides 50% stronger; bitter colonists may sulk
  - **Advisor only**: no mood effects at all

  with the strength of downsides and of rewards; wish frequency, how many can be open at once and how long they
  last, jealousy and favoritism, wishes after events, reminders, self-help, leaving, wishes for others and
  petitions; dilemmas (and how often), decree requests, the colony's character and offices; the meeting interval,
  ambition deadlines, and whether meetings call people from their work.
- **People & moods**: **life's ups and downs** (how hard the everyday pressures press: **gentle**, half strength, no
  half-built work, and high standards don't make frictions sting more; **normal**, the default; or **demanding**,
  half again as strong), rising standards (and whether they also cover the game's comforts), whether Rimfolk's good
  moods add up slowly, getting used to good times, everyday frictions; the social web and social events, secrets,
  vendettas; life goals, personal stories (off, rare, normal or frequent), quirks, heirlooms, coming of age,
  colonists fixing what bothers them (and placing small plans), dreams and dream omens; moments between colonists
  and the colony noticing what happens; journals, names, anniversaries, annals and eulogies; and how often moments,
  social events and dreams come (a quarter as often to three times as often).
- **Heart & health**: feelings and strain, the day off at the end of one's rope, the inspector line, how quickly
  strain builds (50% to 150%); everyday ailments, the serious (possibly fatal) illnesses on their own switch, how
  often ailments come (25% to 200%); convalescence and sickbed visits, and doctors' and midwives' reputations.
- **Events & world**: encounters with strangers and animals (and how often), nemeses (and whether they return),
  pets as companions; **Rimfolk events** on or off, whether they may bring fights, camps on the map, missions in the
  Quests tab, and whether the storyteller draws them (and how likely each is) or Rimfolk's own timer does (and how
  often); the strength of fights in Rimfolk's scenarios; **world events** on or off and how often (about one every
  45 days by default); whether the world's **peoples remember the colony**; and the colony calendar's days.
- **Building**: personal touches and how often; what the room planner does by default (plan the furniture, cut
  plants, mine rock, replace your own buildings, smooth stone; the planner shows the same switches); and **material
  counts in build menus** (the game's own material list shows what the colony has; one of the deeper game hooks).
- **Interface & integrations**: how chatty Rimfolk is: **Everything**, **Notable only** (default: messages for what
  changes something; small moments stay in the journals, the Story tab and History) or **Important only**; where the
  smaller news goes (**Colony news**: one letter each evening by default, as it happens, or off, to the History tab
  only); a message for every dream, letters or messages for new and granted wishes, floating speech text, the visual and sound
  touches; the two alerts; mood insight (and its hints in the Needs tab), the slow speed buttons (and putting them
  back), where the council button sits in the bottom bar, the rooms button; the deeper game hooks and the notes in
  the game's letters.
- **Advanced**: this colony's era (detected, pre-industrial or industrial; set per colony, so it needs a loaded game),
  **Check what's on and working** (the same report as the dev action "Rimfolk: check myself"), and a full reset to
  the defaults (with the list of what changes; saved rooms are kept).

Saved settings carry over unchanged: every setting keeps its name in the settings file. For modders: the window is
drawn from one list, `Source/ColonyCouncil/UI/SettingsCatalog.cs` (each setting's page, heading, label, tooltip, line
and what it needs), and `Tools/Tests` checks that every public field of `CouncilSettings` is on exactly one page, so a
new setting needs a row there.

## Optional mods that go well with it

- **Interaction Bubbles** by Jaxe (search the Steam Workshop): shows social-log conversations as speech
  bubbles, so colonists grumbling about their wishes, Rimfolk's moments and social events appear above their heads.
- **RimHUD** by Jaxe: can show trust, the open wish, office, life goal, strain, feelings and the heart's needs in
  its pawn panel, with bars for strain and the heart's needs (see above).

## Status and feedback

Version 0.4. It compiles against RimWorld 1.6 and is checked against 1.7 (the same DLL runs on both), and all XML
is checked against the real game types by the included validator. It runs in a live game with a 200+ mod medieval
list. If something misbehaves, open the debug log (Options → Development mode, then the log button): lines starting
with `[Rimfolk]` are from this mod.
Please share them so they can be fixed. Quicker still: with development mode on, open the debug actions and pick
**Rimfolk: check myself** (under Rimfolk). One window shows the game, Rimfolk and Harmony versions, which game hooks
applied and which didn't (and why), which parts are switched off or changed from their defaults, which of the mods
Rimfolk works with it found, and its own errors and warnings this session; a screenshot of it (or the "Copy to
clipboard" button) says most of what a bug report needs.

## For modders

- `Source/ColonyCouncil/`: the C# code. Build with `dotnet build -c Release` (needs the .NET SDK;
  the RimWorld reference assemblies come from NuGet).
- `Tools/DefValidator/`: checks every XML def and translation key against the compiled game types:
  ```
  dotnet run --project Tools/DefValidator -- . ~/.nuget/packages/krafs.rimworld.ref/<version>/ref/net472 1.6/Assemblies/ColonyCouncil.dll
  ```
- `Tools/ApiDiff/`: checks that the built DLL still runs on another game version (every game member, override,
  constant, Harmony target and by-name lookup it uses). `-p:RimRef=1.7` builds against the 1.7 references. See
  "Checking a new game version" in [DESIGN.md](DESIGN.md).
- `Tools/Tests/`: tests for the room rotation and mirroring math (`dotnet run --project Tools/Tests`).
- `Source/ColonyCouncil/Lean/`: on which tick of the hour each periodic pass runs (`Sched.cs`, so none share a tick)
  and how much history each part keeps in the save (`Caps.cs`). DESIGN.md lists them all under "Ticking" and
  "Save size"; a new hourly pass gets its own tick there.
- `Source/ColonyCouncil/UI/UiKit.cs`: the small interface kit every council page is drawn with (colors, spacing,
  page headers, cards, tags, meters, portraits). New pages should use it so they look like the rest.
- `Tools/make_icons.py`: draws the interface icons into `Textures/ColonyCouncil/Icons/` and a contact sheet of
  them, `Tools/icons_preview.png` (needs Pillow: `pip install pillow`).
- Feelings are data-driven too (`Defs/HeartDefs/Council_Feelings.xml`): each lists the thoughts that feed it and what
  it nudges. Another mod's thought can join a feeling with a `ColonyCouncil.HeartTags` mod extension.
- Wishes, advice checks, ambitions, room templates, flavor pools and almanac pages are all data-driven XML.
  Lines can carry tags (`[winter,trigger:raid]`) so they're used when they fit. See [DESIGN.md](DESIGN.md).
  `python3 Tools/variety_report.py` ranks the pools by how often a line comes round in a year of an eight-colonist
  colony (`--top N`, `--filter REGEX`, `--all`); new lines do most good at the top of that list. A tagged line wins
  most picks for whoever it fits, so give a common tag (a season, trust, the night) a few lines rather than one.
