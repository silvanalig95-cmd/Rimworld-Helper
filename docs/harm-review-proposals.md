# Harm review: proposals per system

The player asked on 4 November 2026 to "review the other mechanics … and apply this rule as well and add some that are
also can be negative. Maybe preventable, maybe not preventable. It should be kind of everything should be possible."
This is the list proposed first, as CLAUDE.md asks: nothing in it is built, and the player picks. It follows the harm
rule (since 3 November 2026: hurts when flavorful or interesting, at the game's own rate, never gratuitous, always from
something on the map, and shown) and the every-age rule (since 4 November 2026).

## Summary

This review visits 37 of Rimfolk's systems in four groups and proposes 94 ways they can go badly: 87 preventable,
fully or in part, by a choice or by care the player already controls, and 7 that can only be eased once they happen.
Each comes with a rate set against something the game already does and what is seen on the map. A few systems already
carry harm at the game's rate (the outsiders' bouts and duels, the serious illnesses, couriers waylaid, the events'
battles) and get small additions; the council, the camps, the calendar and social life, which hurt nobody today, get
the most. The ten I would build first: (1) drills that bruise, practice bouts in which one blow in 25 lands for real
(1.2.1); (2) tired hands, work injuries under long hours, a harvest push or a building season (1.2.2); (3) a camp's
quarrel that becomes a brawl on the colony's land (2.4.1); (4) sickness brought home from a journey to a sick town
(3.1.1); (5) the champion's bout that can hurt, wrestling no longer always safe (2.2.1); (6) hunts that can bite back,
game that may turn on its hunters in answers and on the calendar's hunt (2.1.1); (7) a rival's sabotage that, found
out, ends in a fight (3.5.1); (8) hustings that come to blows (1.4.1); (9) frayed tempers, high strain making a fight
likelier (3.3.1); (10) working through it, a sprain or a bad back worked on until it becomes a real injury (3.2.1).

**The player's pick (6 November 2026):** "my top ten, then more". Built so far (harm, part 1): the shared rules below, as
one service every hurt goes through (`Harm/Harm.cs`, DESIGN.md "Harm from everyday life"), and 1.2.1, 1.2.2 and 3.2.1;
(harm, parts 2 and 3, 7 November 2026) the other seven of the ten, 2.4.1, 3.1.1, 2.2.1, 2.1.1, 3.5.1, 1.4.1 and 3.3.1,
each marked built below with what differs. The balance model's harm mode (`Tools/Balance -- --harm`) has their rates.

## How to read it

Each system has three parts: what can already go badly today, with the files it lives in; numbered proposals; and the
pick, the one or two to build first and why. A proposal is named by its section and number: 1.2.2 is the second
proposal under 1.2. Each says what goes wrong and what it adds to play, then:

- *Prevent:* the care or choice that stops or eases it, or "not preventable".
- *Rate:* how often, against something the game already does. The anchors used throughout: the game's own social fight
  after an insult; a sparring bout's real blow, one bout in 25 (`Outsiders/OutsiderOutcomes.cs`); an everyday ailment
  from the day's work, about 1 in 140 to 1 in 200 long days (`Ailments/`); a food poisoning from a middling cook, a few
  meals in a hundred; one of the storyteller's diseases, a few a year; a hunted animal turning on its hunter, the game's
  own chance for its kind.
- *Seen:* what happens on the map: who, where, what is said (through `Speech.Say`), and the game's own fight, wound or
  illness. A message or letter only where it matters, never instead of the map.
- *Ages:* where the form differs by the colony's tech level or the other people's ways (`Lean/Ages.cs`, `Ages.Form`);
  left out where it is the same in every age.

## Rules every proposal keeps

- **From the map, shown.** Hurts come as the game's own: its social fight, its wounds (a cut, a bruise, a burn, a
  crack), its diseases and Rimfolk's ailments, its predators and manhunters. Never from a letter alone.
- **At the game's rate, scaled with the game.** Every rate below is multiplied by one new setting, "Hurts from everyday
  life" (0 to 200%, 100 by default), and leans with the storyteller's own disease and threat numbers, so a peaceful game
  stays gentle.
- **A shared cap.** From all of these together, a colonist takes at most one new Rimfolk-made injury in three days, and
  none while downed, in a sickbed, pregnant or already fighting. Babies never; children only the mildest (a scrape in a
  race, a cold from the rain), never a bout or a fight. Nothing here kills outright; the game's own fights and illnesses
  still may, as they always could.
- **Named before the choice.** Where a choice brings the risk (an answer, a decree, a contest), the impact sheet or the
  decree page says so, the way a fight already shows there as a health line.
- **Switches.** Each proposal sits under its own system's setting; links between systems also go behind "Systems talk
  to each other".
- **No mood on top.** Being hurt gives the game's pain and the setbacks there already are, nothing more; the deed, the
  shared memory and the journal carry the story.

---

## 1. The council

### 1.1 Offices
**Today.** A seat gives its holder a small edge and sometimes the colony one (`Offices/Offices.cs`, `OfficeDefs`); the
peacemaker sits the worst-feuding pair down and cools rivalries; a seat lost early and the weight of office are personal
stories (`Council_Arc_LostSeatEarly`, `Council_Arc_WeightOfOffice`). Nothing physical goes wrong in office.

1. **The physician catches what they tend.** Tending a patient with a catching everyday ailment (head cold, stomach bug,
   pinkeye) or a contagious serious one (tuberculosis, cholera) can pass it to the doctor. Adds a reason to keep the
   sickroom clean and train a second doctor. *Prevent:* yes: a clean sickroom, the rest-when-ill or quarantine decree, a
   second doctor sharing the rounds. *Rate:* about one in 40 tendings for the everyday kind, a tenth of that for the
   serious. *Seen:* the doctor at the bedside, a cough a day or two later, someone else tending the doctor. *Ages:*
   herbs and boiled cloths before industry; gloves and masks halve it from the industrial age, sterile tiles quarter it.
2. **The peacemaker's sit-down goes wrong.** When the peacemaker sits a bitter pair down (`Offices.cs`, the peacemaker's
   day), two hot tempers may come to blows instead, and the peacemaker stepping between them can take a punch. Adds a
   cost to sitting the worst pair down too soon. *Prevent:* yes: a peacemaker with good Social (the chance falls with
   skill), or waiting until the pair has cooled. *Rate:* one sit-down in 10 for the worst pairs, a couple a year in a
   colony with bitter rivals. *Seen:* the three at a table, raised voices, the game's social fight, the peacemaker
   pulling them apart.

**Pick:** 1. It turns the hospital's cleanliness and a second doctor into real choices, and reuses the ailments'
spreading between roommates.

### 1.2 Decrees and petitions
**Today.** Decrees cost mood, strain and trust; forcing one again can start a tantrum in a hot temper or a sad wander
(`Decrees/Decrees.cs`, `Backlash`); long hours tire people faster; drills and the militia only give Shooting and Melee
practice (`Defs/DecreeDefs/Council_Decrees.xml`, `Council_Decrees4.xml`); banked fires make rooms chillier; open gates
bring visitors half again as often; petitions lapse a little sourly (`Decrees.cs`, the daily look). Nobody is hurt by a
decree.

1. **Drills that bruise.** Drills and militia drills become paired bouts and musters on the map rather than practice
   alone; one bout in 25 lands a real blow, as the outsiders' sparring does (a bruise or a cut of 6 to 9). Adds a price
   to drilling besides mood, and makes the marshal's seat matter. *Prevent:* partly: a marshal in office halves it, the
   tough are hurt less, and a doctor at home tends it at once. *Rate:* about one real injury per five-day drills decree
   in a colony of ten; the gentler militia about half that. *Seen:* pairs sparring at the gathering spot or the line
   each evening, the blow, a bubble, the hurt one walking to the doctor. *Ages:* sticks and wrestling (tribal), blunted
   blades and shields (medieval), the rifle range and bayonet drill with a ricochet graze (industrial), stun batons and
   training rounds (spacer, which also gives the militia a form it lacks today).
   *Built (harm, part 1, 6 November 2026):* `Harm/DrillBouts.cs`, with "Answers play out on the map" on; the militia is
   declared in every age now. A doctor tending at once is the game's own.
2. **Tired hands.** Under long hours, a harvest push or a building season, a work injury is possible: a cut at the
   bench, a crushed foot under a load, a burn at the stove or forge, rolled like the everyday work ailments but for a
   real wound of 3 to 6, likelier as rest runs low. Adds the honest cost of driving people hard. *Prevent:* yes: shorter
   decrees, never two work decrees at once, people allowed their sleep; tough colonists half as often. *Rate:* about 1
   in 150 long days worked under the decree, twice that below 30% rest; one or two small injuries per decree in a colony
   of ten. *Seen:* the slip at the bench or the dropped load, a line said, the hurt one going to the doctor. *Ages:*
   stone tools and the fire pit (tribal), the saw and the forge (medieval), machines (industrial, a crushed hand), a
   fabricator's burn (spacer, rarer and worse).
   *Built (harm, part 1, 6 November 2026):* `Harm/WorkHurts.cs`, at the anchored rate of one in 150 long days; the "one or
   two per decree in a colony of ten" above was a miscount against it (about a third of one with everyone rested, twice
   that with nobody sleeping enough).
3. **A cold night for the frail.** With banked fires in a hard frost, an elder, a small child or a convalescent whose
   bedroom falls below comfort may catch a head cold, which for the frail is the road to pneumonia (the serious tier's
   3% a day). Adds a real trade between fuel and the old. *Prevent:* yes: a heater kept up in the sickroom and the elders'
   rooms, ending the decree in a cold snap. *Rate:* one chill per cold week of the decree, only for the frail. *Seen:*
   the elder huddled in blankets, a cough, the doctor's word.
4. **Open gates, and who walks in.** While the gate stands open, now and then a stranger takes advantage: a thief at the
   stores, or a drunk who picks a fight in the yard. Adds a reason the nervous hate the decree. *Prevent:* partly: the
   warden of guests, someone on watch, the on-guard decree beside it. *Rate:* at most one such stranger per open-gates
   decree, about half the time. *Seen:* a stranger at the stores with a sack, chased off; a fistfight if caught. *Ages:*
   a wanderer from another clan (tribal), a cutpurse (medieval), a grifter (industrial), a spacer's con artist.
5. **The sick work on.** When someone petitions for rest when ill or a quarantine while a catching ailment goes round
   and the council lets the petition lapse, the sick keep working and the ailment reaches one more person than it would.
   Adds weight to petitions. *Prevent:* yes: heed the petition. *Rate:* only while such an ailment runs; one more case.
   *Seen:* the coughing colonist at the shared table, the next one sneezing a day later, the petitioner's line.

**Pick:** 1 and 2. Both make the council's orders cost something physical, are seen every day the decree holds, and the
player controls them entirely.

### 1.3 Wishes and promises
**Today.** An ignored wish costs trust and mood; a broken promise more, with a chance of being disgruntled
(`CouncilDynamics.cs`, `Expire`); under demanding stakes a bitter, unhappy colonist may storm off (a sad wander, or an
insulting spree for the abrasive, `MaybeSulk`); a wish's own lapse can start a mental state (`Wishes/WishEffects.cs`).

1. **Words with the speaker.** Under demanding stakes, a bitter colonist whose promise was broken walks up to the
   speaker, the council's face, and says it to them; with a hot temper it becomes the game's social fight. Adds a face to
   the council's failures. *Prevent:* yes: keep promises; a speaker with good Social talks them down more often; never
   under gentle stakes. *Rate:* a fifth of broken promises among the bitter and hot-tempered, once or twice a year in a
   colony that breaks many. *Seen:* the walk over, the line said, a fight or a turned back. *Ages:* the chief at the fire,
   the lord in the hall, the chairman, the director (the speaker's word by age).
2. **The thing that went to someone else.** A colonist whose wish (a room, a weapon, a seat, an animal) went to another
   may pick a fight with the one who has it, if jealous or abrasive. Adds a social edge to who gets what. *Prevent:* yes:
   grant both, or a word from the peacemaker. *Rate:* the game's own fight chance for a pair at -20 opinion, for a week.
   *Seen:* the passed-over one in the doorway of the room they wanted, a sharp word, sometimes blows.

**Pick:** 1. It turns the demanding stakes into a moment on the map rather than a mood, and needs only a walk and a fight.

### 1.4 Elections
**Today.** An election plays out on the map: campaign visits, hustings on the eve, the vote, the count
(`Offices/OfficesElection.cs`, `ElectionGathering.cs`); danger or bad weather calls a gathering off; losing costs mood
and trust. Nothing physical.

1. **Hustings that come to blows.** At the hustings, heckling between two rivals backing different candidates can become
   the game's social fight, the gathering pausing round it. Adds a reason to calm the colony before polling day.
   *Prevent:* yes: the peacemaker present (half), the best-behavior decree, calm candidates. *Rate:* one hustings in 8
   with rivals in the crowd, once every few years. *Seen:* the heckle, a shove, the fight, the speaker calling for order.
   *Ages:* a moot at the fire (tribal), the hall (medieval), the town meeting (industrial), the assembly (spacer).
   *Built (harm, parts 2 and 3, 7 November 2026):* `Harm/HustingsBrawls.cs`. "Rivals in the crowd" is narrowed to a rival
   of the heckler's among the speaker's own supporters, so the shove answers the heckle; the first such heckle has the
   hustings' one roll, at the rate above, each easing a half and a frayed temper half again at most. The speeches wait
   while it lasts and whoever chairs the hustings calls for order, in the age's words (the gathering itself is the
   colony's hustings in every age). About 0.01 to 0.03 fights a colony-year in the balance model.
2. **The sore loser.** A hot-tempered or bitter candidate who lost a close race may pick a fight with the winner in the
   days after. *Prevent:* yes: a gracious winner (a handshake played out at the count), a calm loser. *Rate:* one loss
   in 10 among the hot-tempered. *Seen:* the loser walking over, words, sometimes blows; a deed for gossip.

**Pick:** 1. The hustings are already a gathering on the map; a fight there is a story the whole colony sees.

### 1.5 Council meetings and projects
**Today.** Meetings gather at the table with pitches, arguments and jokes (`Ambitions/CouncilMeeting.cs`); projects are
checklists, and failing one costs trust and mood (`Ambitions/Ambitions.cs`, `Fail`). Nothing physical.

1. **A meeting that boils over.** When two rivals both pitch and one idea wins, the argument can become a shove at the
   table. *Prevent:* yes: the speaker's Social, the peacemaker. *Rate:* rarer than the hustings, once in a long while.
   *Seen:* chairs pushed back, the fight, the meeting decided by those who stay.
2. **The project's last push.** In a project's last three days, work on its own blueprints counts as hard work: the
   tired-hands rule (1.2.2) applies there. Adds a cost to rushing the great hall. *Prevent:* yes: no rush, rest. *Rate:*
   as tired hands. *Seen:* as tired hands; the chronicle may name the hall "built with a bandaged hand". *Ages:* by the
   project.

**Pick:** 2, built together with tired hands; it costs almost nothing more.

---

## 2. Answers, events and the world beyond the walls

### 2.1 Dilemmas and their answers
**Today.** 45 effects in twelve def files (answers, a social event, two encounters, a secret's reveal) can start a
fight (`DilemmaEffect_Fight` through `HeldRisk`, `PlayOut/PlayOuts.cs`); others start a mental state or leave a hediff (rattled, sleepless, penance;
`Dilemmas/Dilemmas.cs`); the impact sheet shows a fight as a health line. Answers never take anyone where the place
would hurt them (`PlayOut/PlaySafety.cs`), and a hunt never goes after a predator "or anything likely to turn on the
hunters" (`PlayOut/PlayOutEffects.cs`, `DilemmaEffect_Hunt`; `PlayOutGroupEffects.cs`, `DilemmaEffect_HuntParty`).
"Back too soon" (`Council_Dilemmas_Health.xml`) costs only days of convalescence.

1. **Hunts that can bite back.** Answers and the calendar's hunt may send hunters after game that can turn (a boar, an
   elk, a muffalo; predators still never), at the game's own revenge chance for the kind, with the impact sheet naming
   it ("the boar may turn on them"). Adds a real choice between the safe deer and the meat that fights. *Prevent:* yes:
   more hunters, ranged weapons, the safer answer. *Rate:* the game's own chance for the animal. *Seen:* the game's own
   hunt and the animal's charge. *Ages:* spears and bows bring the hunter closer and the risk higher (tribal, medieval);
   rifles keep it at range.
   *Built (harm, parts 2 and 3, 7 November 2026):* `Harm/HuntsBite.cs`, with answers on the map and the deeper hooks
   (a postfix on the game's revenge chance and one around its damage notice). One head a hunt at most, and only a lone
   one (no packmate within the game's own reach), so a turn is never a herd; nobody's, bonded or venerated animal; the
   game's chance times the harm scale, nothing for a hunter the cap holds back (their own shot at it not counted as
   already fighting), the game's own for one the player drafted; a turn counts as the hunter's hurt. Named under the
   answer and the hunting party, in the marking's message and the hunting day's letter. The hunting season decree keeps
   to safe game. The ages are the game's own distance factor on the chance. About 0.4 to 0.5 turns a colony-year.
2. **Hot and heavy answers.** Answers that do heavy or hot work on the map (the dead carried out and the pyre lit,
   felling, a fire laid in a cold room, lanterns hung at night) roll once for a small injury: a burn at the pyre, a bruise
   from a log. *Prevent:* yes: another answer; skilled hands are hurt half as often. *Rate:* one in 40 such play-outs.
   *Seen:* the slip at the moment of the work, a line said.
3. **Back too soon, for real.** Letting a convalescent back to light work can end with a fall at the storeroom, a bruise
   and two more days in bed, beside the longer convalescence it already costs when they overdo it. *Prevent:* yes: keep
   them in bed, or give them work from bed. *Rate:* a third of the times they overdo it. *Seen:* the colonist with the
   sack, sitting down hard, helped back to bed.

**Pick:** 1. It lifts the clearest "nobody is hurt" guard in the play-outs, at exactly the game's own rate.

### 2.2 Rimfolk's events
**Today.** Fights only through the battles setting: a refused tribute's war party, a deserter's pursuers, bounty
hunters, debt collectors (`Council_Events_Battles.xml`, `Council_Events_Challenges.xml`, `Council_Events_Pasts.xml`,
`DilemmaEffect_RaidLater`). A
champion's contests "never hurt anyone": targets, wrestling without weapons, crafts, debate
(`Happenings/Workers_Challenges.cs`); the great beast is never a predator and fights only if provoked; hail damages
crops, never people (DESIGN.md, Layer 5); the neighbors' events say "none ends in a fight"
(`Rimfolk_Events_Neighbors.xml`); a visiting healer's tonic or shot always does good (`Happenings/VisitorServices.cs`).

1. **The champion's bout can hurt.** Wrestling and other bouts against a traveling champion use the sparring rule: one
   bout in 25 has a real blow. Shooting at targets, crafts and debate stay safe. Adds weight to who is sent and to the
   stake. *Prevent:* partly: decline, or send someone tough. *Rate:* one bout in 25. *Seen:* the bout in the yard before
   onlookers, the blow, the champion's apology or sneer. *Ages:* wrestling and stick-fighting (tribal), quarterstaff or
   blunted swords (medieval), boxing (industrial), a stun-baton exhibition (spacer).
   *Built (harm, parts 2 and 3, 7 November 2026):* `Harm/ChampionBouts.cs`: a fighting challenge is the feast's bout at
   a ring beside the colony's champion, the colony's people called round; its winner wins the contest (a draw by skill,
   as before). The ages are the challenger's people's (the bout is theirs), and a real blow on the challenger is granted
   as a fair one. Needs the outsiders' practice bouts allowed; without them, decided on the spot as before. A few in a
   thousand colony-years: fighting challenges are rare.
2. **The gambler's game turns.** Letting the colony play the traveling gambler can end with a cheat caught, and a
   hot-tempered loser starting a fistfight with the gambler (the outsiders' fistfight rule: goodwill lost, never a war).
   *Prevent:* yes: "No gambling at the colony", or only the calm play. *Rate:* one game in 6. *Seen:* the table, the
   accusation, the fight, the gambler leaving early. *Ages:* knucklebones, dice, cards, a slate of odds.
3. **Caught in the hail.** Anyone still outdoors when the hail starts takes a bruise (a blunt hit of 1 to 3), including
   the work party bringing the ripe crops in. *Prevent:* yes: call them in at the first stones; a roofed walk. *Rate:*
   only those still out; one or two bruises a storm. *Seen:* workers running for cover, one limping in. *Ages:* a helmet
   or a hard hat turns it.
4. **The hedge healer's bad batch.** A visiting healer's tonic may be a bad batch: a stomach bug for a few who took it,
   unless the colony's own doctor (Medicine 8 or more) looked it over first. *Prevent:* yes: the doctor's look, or
   declining. *Rate:* one visit in 8. *Seen:* the healer going round with the cup, a few queasy the next morning.
   *Ages:* a bitter tonic before industry; a shot after, whose bad batch is a sore arm and a day's fever.

**Pick:** 1, and 2 second. The champion's bout reuses the outsiders' sparring code as it stands; the gambler gives the
neighbors' events their first fight, and a cheap one.

### 2.3 World events
**Today.** A world event changes the odds of the game's own incidents: a plague raises the game's plague 2.5 times and
the flu 1.5, a quarantine lowers them (`Defs/WorldSituationDefs/Council_WS_Hardship.xml`); some bring raids later
(`DilemmaEffect_RaidLater` in `Council_WS_Hardship.xml`, `Council_WS_Powers.xml`, `Council_WS_Region.xml`); a rising
warlord's raids are stronger (`World/WarlordWorker.cs`). The harm comes as the game's incidents, not from anyone the
colony meets.

1. **The sickness at the gate.** During a regional plague or fever, a visitor, trader or caravan at the gate may carry
   it, and whoever traded or talked with them closest may fall ill days later. It replaces part of the raised incident
   chance, so the total stays as it is. Adds a face to the plague, and a reason to shut the gate. *Prevent:* yes: the
   quarantine decree or the event's "shut the gate" answer; one person meeting the trader. *Rate:* the plague event's
   own extra chance, now given a carrier. *Seen:* a coughing trader, the colonist who haggled with them falling ill.
   *Ages:* the same everywhere; a spacer trader may come masked.
2. **Hungry hands in the fields.** In a famine or after locusts, strangers come by night for the fields or the stores;
   a colonist who meets them may be struck before they flee. *Prevent:* yes: a night watch, lights, alms or famine bread
   given out (fed, they don't come). *Rate:* once or twice in a famine. *Seen:* figures in the field at night, a
   struggle, a sack dropped.
3. **A brawl at the creek.** Gold fever or a silver rush brings prospectors; two parties' quarrel on the colony's land
   becomes a brawl, and a colonist too near can be caught in it. *Prevent:* yes: keep away, send the marshal. *Rate:*
   once per rush, half the time. *Seen:* the prospectors' camp, the shouting, the fight.

**Pick:** 1. It makes the plague's arrival visible and gives the existing quarantine a concrete payoff.

### 2.4 Camps
**Today.** Tension rises from takings and arrests; at 40 and 70 the council is asked, at 100 the camp leaves with 8
goodwill lost (`Camps/Camp.cs`); arresting one of them adds 40 and sends them fleeing (`Camps/CampLife.cs`); one of
them can take a fever that stays theirs (`Camps/CampStory.cs`); a war party fights the colony's attackers
(`Camps/WarCamp.cs`). Only harming or arresting one of them makes enemies.

1. **The quarrel that becomes a brawl.** At tension 70 or more, or after the "old quarrel with another people" dilemma
   answered badly (`Rimfolk_Dilemmas_Camps.xml`), one of the camp and a colonist, or two of the camp's own, come to
   blows on the colony's land, and a few on each side join. Never into a war: their people remember it as a fistfight
   (goodwill lost, not hostility). Adds a reason to watch the tension and to answer the camp's asks. *Prevent:* yes:
   lower tension (trade, gifts, answers), the peacemaker, hot tempers kept away. *Rate:* once in a camp's stay when
   tension is high, a couple of times a year. *Seen:* raised voices at the fire, the brawl, the camp packing up early.
   *Ages:* the same; who backs down follows their people's ways.
   *Built (harm, parts 2 and 3, 7 November 2026):* `Harm/CampBrawls.cs`: an evening's one chance in four at their fire
   while tension is 70 or more or for three days after the shouting at the table or keeping out of it, once a stay; the
   colony's side needs the deeper hooks (so the blows aren't an attack on guests); a brawl among themselves holds nothing
   against the colony. "Who backs down" is the game's social fight's own end. "A couple of times a year" was the rate for a
   camp whose tension runs high; with the camps' own pace (about one and a half a year, few at 70) it is about 0.06 a
   colony-year with the colony in it, 0.11 neglectful.
2. **The fever at their fire.** A camp's sick member can pass the fever to colonists who sit at their fire (a catching
   everyday ailment, or the game's flu). *Prevent:* yes: send the colony's doctor (an ask already), keep away while
   they're sick. *Rate:* one camp sickness in 5 passes to one colonist. *Seen:* colonists at the fire, a cough two days
   later.
3. **The drovers' bull.** A colonist who wanders among the drovers' herd or a camp's pack beasts can be charged by a
   startled animal (a bruise, rarely a crack). *Prevent:* yes: children and the curious kept from the herd. *Rate:* rare,
   one camp stay in 15 with a herd. *Seen:* the herd, a charge, the drover's dog barking.

**Pick:** 1. It is the clearest bad end the camps lack, and every part of it (tension, the dilemma, the game's fight,
the goodwill rule) already exists.

### 2.5 Visitors and callers
**Today.** Visitors "are never a threat", come only from peoples at peace, and leave at once if hurt
(`Visitors/Visitors.cs`); outside hunters never take game that would turn on them, and check that a shot passes nobody
(`Visitors/VisitorHunt.cs`); callers (the barber, the art collector, the scribe, the cook) do their trade
(`Visitors/Callers.cs`). Nothing goes badly.

1. **A guest's night goes sour.** A guest given a bed for the night, after a drink at supper or with a hot temper, can
   quarrel with a colonist, and the outsiders' fistfight rule follows. Adds a cost to hospitality and work for the warden.
   *Prevent:* yes: the warden of guests, no drink served, the best-behavior decree. *Rate:* one guest's night in 15.
   *Seen:* supper, a sharp word, a fistfight, the guest leaving at first light with a bruise.
2. **The guest's cough.** A visitor brings a catching everyday ailment, and one colonist who spent time with them catches
   it. *Prevent:* yes: a guest bed in a room of its own, the quarantine decree. *Rate:* one visit in 20. *Seen:* a
   coughing guest, one colonist sick a day later.
3. **The barber's nick.** The barber's razor leaves a small nick now and then (a cut of 1), more with a poor barber.
   *Prevent:* not preventable, beyond declining the barber. *Rate:* one cut in 30. *Seen:* a dab of blood and a laugh.
   *Ages:* a flint blade, a razor, clippers that never nick (spacer: nothing).

**Pick:** 1. It gives visitors their first bad night in a form the game already handles.

### 2.6 Missions
**Today.** Missions are caravans or hosting (`Missions/Workers_Missions.cs`, `Workers_MissionRoad.cs`): a pilgrim to
escort, word to carry, a lost child to find before the second nightfall, hands for a harvest, an apprentice sent, a guest
of rank, refugees to see through. Failing costs goodwill and a setback; the road's dangers are the game's own ambushes.

1. **The lost child, found hurt.** A child found only after the first night may have frostnip, a sprained ankle or a
   chill; found early, unhurt. Adds urgency the mission's deadline alone lacks. *Prevent:* yes: a search sent out early.
   *Rate:* by how late, about half of late finds. *Seen:* the child limping, carried home on a colonist's back.
2. **The guest of rank's enemy.** A dignitary's visit can draw a rival of their house: a hothead at the gate who
   challenges the guest's guard or the colony's marshal to a duel by the people's ways (the outsiders' duel rule).
   *Prevent:* yes: refuse the challenge (a little regard lost), or the guest's own guard takes it. *Rate:* one visit in
   6. *Seen:* the challenger at the gate, the duel watched by the guest. *Ages:* a rival clansman (tribal), a knight's
   challenge (medieval), an agitator's fists (industrial), a disgraced officer (spacer).
3. **The hands come home sore.** Hands back from a town's harvest bring what a day and a half in the fields would have
   given at home: a strained back, blisters, a sunburn. *Prevent:* partly: send the tough. *Rate:* the work ailments'
   own. *Seen:* the hands walking in, one limping.

**Pick:** 1. A lost child is the mission most players will feel; a found-late cost makes the search a real race.

### 2.7 Couriers
**Today.** A messenger on a dangerous road (enemies near the town) may come in with a cut of 3 to 6 on an arm or a leg,
and the letter says so (`World/NeighbourLetters.cs`, `Waylaid`; `Couriers/CourierTracker.cs`); couriers never come into
danger and leave if hurt.

1. **Set upon at the edge.** Instead of arriving already hurt, a courier on a dangerous road is sometimes set upon on the
   colony's own map near the edge by two or three bandits; the colony can ride out to help, and unhelped the courier is
   hurt and the goods may be taken. Adds a short fight the player can choose to join. *Prevent:* yes: someone on watch,
   people sent out. *Rate:* half of today's waylaid messengers. *Seen:* the struggle at the map's edge, the courier
   limping to the gate. *Ages:* by the people's own means: a runner, a rider, a held-up delivery man.
2. **A courier who arrives sick.** During a regional sickness, a courier may bring it (as 2.3.1, the same carrier rule).
   *Prevent:* yes: the quarantine decree; the goods left at the gate. *Rate:* as 2.3.1. *Seen:* a coughing courier.

**Pick:** 1. The harm already exists in text; this moves it onto the map, which is what the rule asks.

### 2.8 Outsiders and their interactions
**Today.** The most developed: a sparring bout with one real blow in 25 (6 to 9); hard words with a fistfight at twice
the game's chance; a duel of honor to first blood or until one yields, with their own weapons, deaths rare but decided by
the game's own fight (`Outsiders/OutsiderOutcomes.cs`; Almanac, "Bouts, hard words and duels").

1. **The rematch.** Someone who lost a duel or a bout badly, or their kin, may come back on a later visit asking for the
   colonist by name. Adds a thread between visits. *Prevent:* yes: a gracious word after the win, a gift. *Rate:* one
   lost duel in 3 brings a rematch within a year. *Seen:* the challenger at the gate asking for them by name. *Ages:* by
   the people's ways.
2. **A fistfight that draws a crowd.** When hard words come to blows, others of both sides nearby may join, up to four.
   *Prevent:* partly: the warden near, few hot tempers about. *Rate:* one fistfight in 5. *Seen:* the brawl, broken up
   by the marshal.
3. **First blood, not stopped.** A fighter with bloodlust doesn't always stop at first blood: one duel in 20 goes on
   until someone yields, and the onlookers pull them apart. *Prevent:* yes: choose the yield terms from the start, or
   don't duel a bloodthirsty one. *Rate:* one duel in 20 with bloodlust. *Seen:* the duel going on past the wound, the
   shout, the onlookers stepping in.

**Pick:** 1. The harm here is already at the game's rate; the rematch adds story rather than more blood.

### 2.9 Notables and letters
**Today.** Notables age and die (`World/Notables.cs`); a letter's messenger can come in hurt; hard words and duels with a
notable are remembered (`World/NeighbourLetters.cs`). Letters themselves hurt nobody.

1. **A challenge by letter.** A notable whose kin lost a duel, or who was insulted in person, may write a challenge, and
   the challenger comes, unannounced, to fight it at the gate; refusing costs regard. Only for peoples whose ways know
   duels. *Prevent:* yes: an apology letter first, or refusing. *Rate:* rare, once or twice in a long game. *Seen:* the
   challenger at the gate, the duel. *Ages:* a runner with a challenge (tribal), a sealed challenge (medieval), a demand
   by radio (industrial), by comms (spacer).
2. **The heir's grudge.** When a notable the colony wronged dies, their heir carries it on: their caravans' guards are
   short with colonists, and hard words come easier. *Prevent:* yes: condolences and a gift to the heir. *Rate:* one heir
   in 3 of a wronged notable. *Seen:* the guards' words at the trade, a fistfight now and then.

**Pick:** 1. It joins letters to the duels already on the map.

### 2.10 Encounters and nemeses
**Today.** Two encounters can end in a fight with the stranger (`Council_Encounters_People.xml`, chances 0.3 and 0.2);
a nemesis is a raider or beast who struck down one of ours and got away, and comes back in the game's raids
(`Strangers/Nemeses.cs`), with dreams and a story after.

1. **The nemesis seeks the one they hurt.** A returning nemesis goes for the colonist it hurt before, if that one is in
   the fight, rather than the nearest. Adds a personal fight to an ordinary raid. *Prevent:* yes: keep that colonist
   behind cover. *Rate:* only with a nemesis in a raid. *Seen:* the nemesis pushing past others toward them, a taunt.
2. **The wild one too close.** In a creature encounter, a colonist who steps closer may be bitten or kicked, at the
   game's taming-revenge chance; a bite can carry rabies by the serious tier's 3%. *Prevent:* partly: the cautious never
   step closer. *Rate:* one such encounter in 20. *Seen:* the colonist reaching out, the animal lashing out and fleeing.

**Pick:** 1. The nemesis is already a story; making it hunt its old victim pays that story off in the fight.

---

## 3. A colonist's life

### 3.1 Journeys
**Today.** The planner packs food, drugs, beds and tents (`Journeys/JourneyPack.cs`), sets the road food policy
(`Journeys/RoadFood.cs`) and waits at the gate for everyone; the road's ailments (blisters, motion sickness, a sprained
ankle) and the game's own caravan dangers apply. A loved one's caravan in trouble is a setback.

1. **Sickness brought home.** Travelers who stayed at a town with a sickness (a world event's plague, a notable's town
   that is ill) may come home with it: the game's flu or plague, or a catching everyday ailment, showing a few days after
   the homecoming. Adds a cost to a journey into trouble. *Prevent:* yes: not lodging in a sick town, the quarantine
   decree for the returned, medicine packed. *Rate:* about one journey in 6 to such a town. *Seen:* the homecoming, then
   a cough, the news naming where it came from. *Ages:* the same; industrial and spacer medicine halve it.
   *Built (harm, parts 2 and 3, 7 November 2026):* `Harm/SickHome.cs`: a sick town is one the region's world events
   really lie over (a plague, or a famine: hunger, locusts, a long drought, a blight; its people's or within twenty tiles;
   never the herds' murrain), which is also what a notable's town that is ill means here. One in six for a night's stay
   there, less for a shorter one, nothing for a trade at the gate under two hours; a plague's is the game's flu or, one time
   in four, its plague; a famine's the everyday stomach bug. Told in a letter naming the town.
2. **Waiting at the gate in the cold.** When someone isn't ready and the others wait at the gate, a long wait in a hard
   frost gives frostnip or chilblains. *Prevent:* yes: answer the letter (go without them, or put it off), warm clothes.
   *Rate:* the cold ailments' rate for hours outdoors. *Seen:* the travelers stamping at the gate.

**Pick:** 1. Journeys happen off the map; the homecoming is where their harm can honestly show.

### 3.2 Ailments and health stories
**Today.** Already at the game's rate. 55 everyday ailments that never kill or leave a mark; 15 serious illnesses
(tetanus from an untended wound, pneumonia from a cold in the frail, typhus from lice, rabies from a bite;
`Ailments/AilmentTracker.cs`); dirty untended wounds infect more (`Ailments/LifeRisks.cs`); the frail heal slower;
convalescence can set back; outside patients never bring "anything that could kill them" (`Health/HospitalPatient.cs`);
the end of a long life (`Health/LastSeason.cs`).

1. **Working through it.** A strained back or a sprained ankle worked through heavy work (hauling, building, mining)
   instead of rest can turn worse: a torn back (a longer, painful condition) or a fall on the bad ankle (a bruise or a
   crack). Adds a reason to give light duty, and makes the everyday tier matter. *Prevent:* yes: bed rest, the work tab,
   the rest-when-ill decree, a tending. *Rate:* one in 10 such ailments worked hard through. *Seen:* the colonist wincing
   under the load, sitting down hard, the doctor's word.
   *Built (harm, part 1, 6 November 2026):* `Harm/WorkHurts.cs`; "worked hard through" is four hours of heavy work in a
   day with it untended and out of bed; the torn back is Rimfolk's `Council_Ail_TornBack`, in the strain's place.
2. **The patient who brings something worse.** One outside patient in 20 brings a catching serious illness
   (tuberculosis, cholera) that can pass to whoever sleeps in the same room. *Prevent:* yes: a separate ward, the
   quarantine decree, turning them away. *Rate:* one patient in 20. *Seen:* the patient's cough, the doctor's word, a
   case in the ward days later.
3. **Small things that grow.** Worms from raw meat left untended for long become the game's gut worms; insect bites in a
   malaria country can carry the game's malaria. *Prevent:* yes: cooked meat, a tending, nets and sleeves. *Rate:* one in
   20 left untended. *Seen:* the ailment's turn named in the message.

**Pick:** 1. It turns the commonest everyday ailments into a choice about work, which is where the player feels them.

### 3.3 The heart and feelings
**Today.** Strain bands give mood -3, -5 or -8, slower work and darker dreams; the "worn out" episode keeps someone in
their room for 6 to 12 hours (`Heart/Episodes.cs`); feelings tip moment weights and wishes (`Heart/Feelings.cs`). The
heart's safety rule: it never patches mood, the mental breaker or the game's break picker (DESIGN.md, Layer 7).

1. **Frayed tempers.** In the high strain band, a sharp word is likelier to become the game's social fight, by at most a
   beer's 1.5, through Rimfolk's own moments and never through the break picker. Adds what strain is: a short fuse.
   *Prevent:* yes: ease the strain (rest, gatherings, the heart's reliefs). *Rate:* the game's fight chance times 1.5 for
   the strained. *Seen:* the fight, and the heart's page saying they had been strained for days.
   *Built (harm, parts 2 and 3, 7 November 2026):* `Harm/FrayedTempers.cs`: "the high band" is the two upper ones, worn
   down a quarter more and near breaking half again; "the game's fight chance" is Rimfolk's own moments' (a near fight and
   the other moments with a fight's own chance, hard words with an outsider, the camp's quarrel, the hustings' heckle).
   Only the strain's part of a fight goes through the cap, told with the strain named. About 0.06 to 0.09 more fights a
   colony-year when neglected, almost none when well run.
2. **Clumsy when worn.** In the high band, work slips are a little likelier: the tired-hands rule (1.2.2) at half again
   its rate, decree or not. *Prevent:* yes: the worn-out day off. *Rate:* half the tired-hands rate without a decree,
   one and a half times it with one. *Seen:* as tired hands.
3. **A restless walk at dusk.** Someone with the restless feeling at its height walks out alone at dusk, and beyond the
   walls at night the game's predators may find them. *Prevent:* partly: a friend goes along (the walk becomes company);
   walls. *Rate:* rare, once a season for a restless colonist. *Seen:* the walk out past the gate, sometimes a predator.

**Pick:** 1. It gives strain a visible edge without touching the mood number, and the factor is already the one the
drink before work uses.

### 3.4 Setbacks and nuisances
**Today.** 53 setbacks from play: work wrecked, a failed construction, a botched job in one's passion, a makeshift spot,
an operation gone wrong (`Setbacks/SetbackEvents.cs`); quiet troubles (`Setbacks/Troubles.cs`); nuisances such as rain
without a coat (`Setbacks/SetbackNuisances.cs`). All moods.

1. **A failed build that hurts.** A construction that fails on something heavy (a wall, a big piece of furniture, a
   bridge) bruises the builder one time in 10. *Prevent:* yes: skilled builders on heavy work. *Rate:* one failure in 10,
   a few a year with poor builders. *Seen:* the frame coming down, the builder stepping back holding an arm. *Ages:*
   timber and stone, then steel.
2. **The makeshift spot's burn.** Cooking at a campfire or butchering at a spot when a proper bench could be built
   carries a small chance of a burn or a cut. *Prevent:* yes: build the bench. *Rate:* one bill in 200. *Seen:* the cook
   pulling a hand back. *Ages:* never in a colony that can't build better yet, as the setback already rules.

**Pick:** 1. Failed builds already happen and are already noticed; a bruise one time in 10 makes skill on heavy work
matter.

### 3.5 Bonds, rivalries and fights
**Today.** Rivals form from low opinion; "a near fight" ends in blows one time in 4 (`Council_SocialEvents2.xml`);
answers can make rivals of two who fought (`HeldRisk`); frictions sting; the peacemaker cools rivalries
(`Social/SocialWeb.cs`, `Social/SocialEvents.cs`).

1. **The rival's sabotage, found out.** A rival at their worst (a strong rivalry, a petty or abrasive nature) does
   something small against the other: hides their tools, salts their meal, leaves their pen gate open, takes credit for
   their work. Found out (a witness, or gossip), it ends in the game's social fight. Adds the schemes of stage 2 in small,
   between colonists. *Prevent:* yes: the peacemaker, keeping their work apart. *Rate:* once or twice a year per bitter
   rivalry; found out half the time. *Seen:* the act itself (the tool carried off to a corner, the gate open, the animals
   out), the discovery, the fight. *Ages:* a stolen charm (tribal), a broken tool (medieval), a jammed machine
   (industrial), a bench's program tampered with (spacer).
   *Built (harm, parts 2 and 3, 7 November 2026):* `Harm/Sabotage.cs` and the job `Rimfolk_Sabotage`: one act, at the
   other's bench (their best craft's), by the colony's means; a machine is jammed only where the bench has one, else it is
   knocked about. The meal, the pen gate and the credit taken are left for the schemes. "Found out half the time" is half
   with nobody to see; someone who saw it always tells. "Work kept apart" is their best crafts differing. About twice a
   year a bitter rivalry of a petty nature, 0.04 fights a colony-year with six, 0.24 with twelve.
2. **Friends stand by.** When one of a close pair is in a social fight, the friend may step in, to pull them apart or to
   help, and can take a blow. *Prevent:* not preventable. *Rate:* one fight in 6. *Seen:* the friend running over.

**Pick:** 1. It makes rivalry do something on the map, and gives the later schemes a tested base.

### 3.6 Groups
**Today.** Rivalry between groups grows from feuds, opposed views and spats, and reaches votes and petitions
(`Social/Groups/GroupLife.cs`); moments include a rival's dig and a firebrand stirring (`Rimfolk_GroupMoments.xml`);
what a group does against someone is small (-1 to -3).

1. **The groups come to blows.** When two groups' rivalry is high and members meet at a shared table or a gathering, a
   dig can become a fight that others of both groups join, two or three pairs at once. *Prevent:* yes: shared meals, the
   peacemaker, a habit the two groups keep together. *Rate:* once or twice a year where two groups are bitter. *Seen:*
   the dig, the brawl, both groups' names after. *Ages:* clans at the fire, factions in the hall, the night shift against
   the day shift, departments.
2. **Brave to a fault.** A group with that way, or the bold ones, sometimes eggs a member into a dare: a swim in a cold
   river, crossing thin ice, wrestling the strongest. A sprain or a chill. *Prevent:* partly: a cautious anchor talks them
   out of it. *Rate:* once a season for such a group. *Seen:* the dare said aloud, the attempt, a laugh or a limp.

**Pick:** 1. Group rivalry already exists and builds up; this gives it a peak the colony sees.

### 3.7 Reputation and gossip
**Today.** Deeds spread in versions between 40% and 160% of the truth (`Social/Gossip/Gossip.cs`); a name gives a social
thought, tags and moments; a hothead or a rival tells it with its subject there.

1. **Said to their face.** When gossip is told with its subject in earshot, the subject may confront the teller, and with
   a hot temper it becomes a fight. *Prevent:* partly: discreet tellers. *Rate:* one such telling in 10. *Seen:* the
   subject turning round, the line, the fight.
2. **A coward's name.** Someone whose name for courage has gone bad pushes forward in the next fight to prove otherwise,
   out of cover. *Prevent:* yes: draft and hold them; a word from the marshal beforehand. *Rate:* once after the name
   forms. *Seen:* the colonist breaking cover in a raid, a line said.

**Pick:** 1. Gossip told in front of its subject is already allowed; this adds its natural answer.

### 3.8 Vendettas
**Today.** Sworn for a death, a kidnapping or a bonded animal; vengeful fury in battle against that people
(`Council_Vendetta_Fury`: "steady hands, no fear, no mercy"; `Secrets/Vendettas.cs`); eight ways it ends
(`Secrets/VendettaEnds.cs`).

1. **A trader of the hated people.** After a peace made while the vendetta is open, the sworn people's traders and
   visitors come, and the avenger may pick a fight with one of them (the outsiders' fistfight, goodwill lost).
   *Prevent:* yes: send the avenger away during the visit, or the vendetta laid down (its story). *Rate:* one visit in 4.
   *Seen:* the avenger walking straight to the visitor.
2. **Fury that forgets cover.** In a fight against that people, the avenger sometimes breaks from cover toward them.
   *Prevent:* yes: draft and hold them. *Rate:* once a battle at most, one in 3. *Seen:* the run forward, a line said.

**Pick:** 1. Peace with the killers is already a way the vendetta ends; this makes the years before it tense.

### 3.9 Secrets
**Today.** 62 secrets; a reveal has effects, and one (the death pits) ends in a fight one time in 5
(`Council_Secrets2.xml`); confiding is a moment (`Secrets/Secrets.cs`).

1. **Found out by the wronged one.** Secrets that wronged someone in the colony (a theft, a lie, a betrayal), revealed
   with that person there, can bring them to blows. *Prevent:* yes: telling it oneself first (the "a secret told"
   story), the peacemaker. *Rate:* one such reveal in 4. *Seen:* the reveal, the line, the fight.
2. **Told by the wrong friend.** A secret confided that reaches the colony through the friend's gossip brings the keeper
   to the friend: hard words, sometimes blows. *Prevent:* not preventable, beyond whom one confides in. *Rate:* one such
   leak in 5. *Seen:* the keeper finding the friend, the line.

**Pick:** 1. It spreads the one fight the secrets have to the secrets where a fight makes sense.

### 3.10 Personal stories
**Today.** 35 stories in 138 stages, moved on by what happens (`Arcs/`); none ends in harm, though some follow it (a
fear faced, the new leg, getting clean).

1. **A rival in love comes to blows.** In "a rival in love" (`Council_Arc_Suitors`), the stiff words between the two can
   become a fight, and the one they both like thinks less of the one who started it. *Prevent:* not preventable.
   *Rate:* one such story in 4. *Seen:* the stiff words turning into a shove, the one they love watching.
2. **The worst night, alone.** In "getting clean" (`Rimfolk_Arc_GettingClean`), if the friend isn't there on the worst
   night, the colonist may go to the stores for the drug: the game's own binge, with its own overdose risk. *Prevent:*
   yes: the friend at their side (the story's step), the drug forbidden or locked away. *Rate:* half the time the friend
   is absent. *Seen:* the walk to the stores at night, the binge.
3. **First steps alone.** In "the new leg", someone who tries their first steps without the friend may fall. *Prevent:*
   yes: the friend's walk to the gate and back. *Rate:* one in 3 alone. *Seen:* the fall by the gate.

**Pick:** 1, and 2 second. Both stories already build to these moments; a bad end makes the good one mean something.

### 3.11 Quirks
**Today.** 60 quirks with small effects, some touching others (`Quirks/Quirks.cs`, `Defs/QuirkDefs/`); none hurts.

1. **Carries more than they should.** The strained back half again as likely for them, and now and then a dropped load
   bruises a foot. *Prevent:* not preventable, beyond rest. *Rate:* the strain ailment's rate times 1.5. *Seen:* the
   stagger under the load.
2. **Sharp habits.** Always whittling, or sharpens every blade: a small nick once a season or so. *Prevent:* not
   preventable. *Rate:* once a season. *Seen:* whittling at the fire, a nick, a laugh.
3. **Weather lovers.** Stands in the rain, or loves the snow: the chest cold or frostnip a third likelier. *Prevent:*
   partly: a coat or a hood. *Rate:* the ailment's rate times 1.33. *Seen:* standing in the rain, face up.
4. **Walks the fields at dawn.** In a land with predators, the dawn walk can meet one. *Prevent:* partly: walls round
   the fields. *Rate:* rare, the game's own predators. *Seen:* the walk, the predator at the field's edge.

**Pick:** 1. One factor on an existing ailment, true to the quirk's name.

### 3.12 Dreams
**Today.** 231 dreams in six tones; a nightmare can leave someone shaken ("jumpy, tired and slow to focus",
`Council_Dream_Shaken`); dreams of a nemesis (`Dreams/Dreams.cs`).

1. **Sleepwalking after a nightmare.** Someone shaken by a nightmare sometimes walks in their sleep; outside in the cold,
   frostnip, or a stumble. *Prevent:* yes: a bolted door at night (an on-map answer already), a roommate who wakes them.
   *Rate:* one nightmare in 15. *Seen:* the sleepwalker crossing the yard in their nightclothes, someone leading them
   back.
2. **The soldier's nightmare.** Someone with a soldier's or outlaw's past, woken from a nightmare of battle, may strike
   out at whoever woke them. *Prevent:* yes: a bedroom of their own. *Rate:* one such nightmare in 20 in a shared room.
   *Seen:* the shout in the night, the roommate holding a cheek.

**Pick:** 1. Flavorful, rare and preventable with something the colony already has.

### 3.13 Faith in the colony
**Today.** Crises of faith (`Faith/FaithCrisis.cs`); faiths at odds answered in trust and certainty
(`Dilemmas/DilemmaWorkers_Faith.cs`); the one-faith decree; evenings of faith (`Faith/FaithEvenings.cs`).

1. **Words at the altar.** After the council names one faith the colony's, a devout of another may come to blows with a
   zealot of the named one at an evening of faith. *Prevent:* yes: "each its own corner" or "share the altar" instead.
   *Rate:* once after such a decree, one time in 3. *Seen:* the evening, the interruption, the fight.
2. **Zeal at the gate.** A devout of the colony's faith may trade hard words with a visitor of a faith at odds, with the
   outsiders' fistfight rule after. *Prevent:* partly: the best-behavior decree. *Rate:* one such visit in 10. *Seen:*
   the words at the gate.

**Pick:** 1. It gives the strongest faith decision its bad side on the map.

---

## 4. The colony's year, its work, its animals and its mods

### 4.1 The calendar and traditions
**Today.** Days kept as a feast, games, rest, remembrance, a bonfire, gifts, a hunt, a planting day, a walk out or an
evening of faith (`Calendar/CalendarKeeping.cs`); a feast's dish may burn and rivals at its table may quarrel
(`Setbacks/SetbackTimes.cs`); the hunt never marks a predator.

1. **Games with real contests.** A games day gets a footrace, wrestling or a tug of war in the yard; one bout in 25 has a
   real blow, and a race on rough ground can sprain an ankle. *Prevent:* partly: a flat yard, no wrestling. *Rate:* one
   small injury per two games days. *Seen:* the contests, the cheering, the limp. *Ages:* wrestling and races (tribal),
   blunt staves and a melee (medieval), boxing and a three-legged race (industrial), a sim tourney with nothing to fear
   but a sprained wrist (spacer).
2. **The bonfire's spark.** On a dry, windy summer night, a bonfire laid in the open can throw a spark into dry grass (the
   game's own fire), and someone near can be singed. *Prevent:* yes: a cleared spot, a firebreak. *Rate:* one bonfire in
   20 in dry wind, never in rain or snow. *Seen:* the spark, the grass catching, everyone beating it out.
3. **The feast that ends in a fistfight.** Rivals quarreling at the day's table come to blows when drink was poured.
   *Prevent:* yes: no drink for the day. *Rate:* one such quarrel in 4 with drink. *Seen:* the toast, the quarrel, the
   fight.

**Pick:** 1. Games are the day with the least in it now; real contests give it stakes and stories.

### 4.2 The defense planner and lines
**Today.** Advice, plans, a line's checks and lessons from raids (`Defense/DefenseLines.cs`, `DefenseLineReview.cs`); the
night watch on the walls is an on-map answer that tires the watcher (`Council_Dlm_NightWatch`). Nothing hurts.

1. **Musters at the line.** The marshal calls the shooters to the line now and then (with the militia decree or the
   "hold the way in" project), each to their post; one muster in 30 has a mishap: a snapped bowstring, a powder burn, a
   ricochet graze. Adds practice where it counts. *Prevent:* partly: a marshal with Shooting 10 or more. *Rate:* one
   muster in 30. *Seen:* everyone at the sandbags, a shout, the hurt one walking back. *Ages:* bows and slings,
   crossbows, rifles, charge rifles (a burn).
2. **The night watch in the cold.** A watcher on the walls in a hard frost without warm clothes gets frostnip or
   chilblains, and a lone watcher outside the walls may draw a predator. *Prevent:* yes: warm clothes, a heated guard
   post, two on watch. *Rate:* the cold ailments' rate. *Seen:* the watcher stamping on the wall, a cough in the morning.

**Pick:** 2. It is cheap (the watch and the ailments exist) and preventable by plain care.

### 4.3 The advisor's automations
**Today.** Bills, research picks, stockpiles, take-down orders, lamps and chairs planned, trees marked for chopping,
plants sown, all undoable and never acting during a fight (`Building/Automations.cs`); the frost harvest applied by
default (`Fields/FieldWeather.cs`, setting `frostHarvestAuto`).

The automations themselves should stay harmless: harm the player didn't choose would feel unfair. The work they set
carries the game's own risks and the work ailments, and the two below belong to that work, whoever ordered it.

1. **A felling mishap.** Felling trees (by the automation's marks or the player's) can go wrong for an unskilled cutter:
   the tree comes down the wrong way, a bruise or a cracked leg. *Prevent:* partly: skilled planters on felling; the
   automation marks near trees first. *Rate:* one tree in 300 for a cutter under Plants 5. *Seen:* the tree falling the
   wrong way, the cutter scrambling. *Ages:* a stone axe more often, a steel one less.
2. **Taking down hurts too.** Taking down a wall or a door (the take-down orders, or the player's) can drop stone or
   timber on the worker, at the failed build's rate (3.4.1). *Prevent:* partly: skilled builders. *Rate:* one wall in
   100 taken down by someone under Construction 5. *Seen:* the wall coming apart, the worker jumping back.

**Pick:** 1, as part of the work ailments rather than of the automations.

### 4.4 Fields by the weather
**Today.** Sowing paused by the forecast, a frost harvest offered or applied (`Fields/FieldWeather.cs`); "all hands to
the fields" before a killing frost (`Rimfolk_Events_Hardships.xml`). The cold and the heat already bring the work
ailments (chilblains, frostnip, sunburn).

1. **Hurried hands at the harvest.** In a frost harvest, the hardship's "all hands" or a harvest push, hurried harvesting
   by hand can cut a hand on a sickle. *Prevent:* partly: harvest before dark; skilled growers. *Rate:* one in 200 hours of
   hurried harvest. *Seen:* the dark field, a yelp. *Ages:* a flint sickle, a scythe, shears.
2. **The forecast can be wrong.** A field sown by the weather can still lose its crop to a cold snap harsher than the
   land's reckoning (the stray days `FieldWeather` already measures), and the zone's note says the forecast missed.
   Adds honest uncertainty to a helper that today is never wrong. *Prevent:* not preventable. *Rate:* as often as the
   game's own cold snaps beat the reckoning, a few times a year in cold lands. *Seen:* frosted plants in a field the
   zone said would pay.

Its counterpart is not a harm: with no warm clothes in the colony, the automatic frost harvest waits for daylight and the
suggestion says why, so the default stays safe while the rest of the year gets sharper.

**Pick:** 1, if any; this system is a helper and should stay mostly gentle.

### 4.5 Animals
**Today.** Births named by the herdmaster, a sick animal reported, a beloved animal's death on the calendar
(`Animals/AnimalLife.cs`, `Animals/AnimalHerdmaster.cs`); a resident beast (a wolf, a bear, a stag) named and answered:
food left out, kept close, hunted, tamed or driven off (`Animals/ResidentBeast.cs`); an animal in decline
(`Animals/FadingPet.cs`). The game's own taming and training revenge, manhunters and predators apply.

1. **The herdmaster's lesson.** When the herdmaster takes a learner along (milking, shearing, training), a nervous animal
   may kick or bite the learner, at the game's training-revenge chance, halved with the herdmaster there. *Prevent:*
   partly: a good herdmaster. *Rate:* one lesson in 30. *Seen:* the animal lashing out, the herdmaster calming it.
2. **The fed wolf grows bold.** A resident predator the colony leaves food out for grows bold: after some days it may go
   for someone alone near the stores at night (the game's own predator hunt, one animal). *Prevent:* yes: keep close,
   drive it off or tame it; never with grazing animals. *Rate:* one fed predator in 4 over a season. *Seen:* the wolf at
   the stores' door at dusk, a colonist backing away.
3. **The rut in the pens.** In the rut, the colony's own males (bulls, rams, stags) can charge whoever enters their pen.
   *Prevent:* yes: keep the pens shut in the rut; leave it to the herdmaster. *Rate:* one charge per rut per pen of such
   animals. *Seen:* the bull lowering its head.

**Pick:** 2. The resident beast's "leave food out" answer is the most tempting; a consequence makes it a real dilemma.

### 4.6 PokéWorld
**Today.** Evolutions, levels and bonds as colony moments, a fainted Pokémon's trainer worried, rare sightings, a Pokémon
that keeps coming to the stores, the Pokédex project (`Integrations/PokeWorld.cs`, `PokeWorldLife.cs`,
`Rimfolk_Dilemmas_Pokemon.xml`).

1. **A catch that fights back.** "Let them try to catch it": the wild Pokémon fights PokéWorld's own way, and a trainer
   without a partner Pokémon at their side can be hurt. *Prevent:* yes: send a trainer with a partner. *Rate:* one try in
   4 turns into a fight. *Seen:* the chase, the fight, the catch or the escape.
2. **The visitor at the stores turns.** The Pokémon that keeps coming, fed, may lash out at whoever shoos it (as 4.5.2).
   *Prevent:* yes: keep the stores shut, or catch it. *Rate:* one fed visitor in 5. *Seen:* the Pokémon at the stores,
   the swipe.

**Pick:** 1. Catching is the heart of the mod; a risk makes a partner Pokémon worth having.

### 4.7 Drink and smoke before work, and toasts
**Today.** A mild drink or smoke before work for Alpha Skills' drunken or stoned passion, only at a stage that makes a
fight no likelier than one beer (1.5, `DrinkBeforeWorkMath.MaxFightFactor`), never before dangerous work (tending,
surgery, hunting, animals), never for addicts (`Integrations/BeforeWork.cs`); toasts pour one drink each within policy
(`Integrations/ToastDrinks.cs`).

1. **The second drink.** A colonist with the drunken passion who has drunk before work three days running may take a
   second cup one day in 5, reaching the tipsy stage (the game's 2.5 fights) and slower hands, unless their drug policy
   stops it. Adds the habit's slope. *Prevent:* yes: the drug policy, or the setting. *Rate:* one day in 5 after three in
   a row. *Seen:* the second cup at the stores, a stagger at the bench, sometimes a fight.
2. **Burned at the bench.** A drink before smithing or cooking (allowed today) carries a small chance of a burn or a
   cut. *Prevent:* yes: a new switch, no drink before hot work. *Rate:* one in 100 drinks before hot work. *Seen:* the
   hiss at the forge or the stove.
3. **The toast's second round.** With strong drink in plenty (Vanilla Brewing Expanded), a toast pours a second round
   for those who want it, and rivals at the table quarrel likelier (4.1.3). *Prevent:* yes: stock, a toast with tea.
   *Rate:* a second round at one toast in 3. *Seen:* the second cups raised, a quarrel.

**Pick:** 1. The rule's own example; it uses the game's stages as they are, and the drug policy already controls it.

### 4.8 A RimWorld of Magic
**Today.** Classes and levels read by name (`Integrations/Magic.cs`); its needs set right (`Integrations/MagicNeeds.cs`);
"the long discipline" story; a healer's tending counts toward goals.

1. **A working gone wrong.** In "the long discipline", a mage's practice working can slip: a fire mage's burn, an ice
   mage's frostbite, a storm mage's shock (the game's own burn and frostbite), one in 25. *Prevent:* partly: the teacher
   present. *Rate:* one practice in 25. *Seen:* the flash, the yelp, the teacher's word.
2. **Fighters spar.** Fighter classes spar with the sparring rule between colonists. *Prevent:* yes: they spar only
   with consent. *Rate:* one bout in 25. *Seen:* the bout at the training dummy.

**Pick:** 1. The story already has practice stages to hang it on.

### 4.9 Other integrations
**Today.** Hospitality's guest beds and guests (`Integrations/Hospitality.cs`, `Visitors/VisitorBeds.cs`), Dubs Bad
Hygiene's needs and fixtures (`Integrations/Hygiene.cs`), Alpha Bees' stings (an everyday ailment), Giddy-Up mounts and
Vanilla Fishing Expanded in wishes.

1. **A fall from the saddle.** With Giddy-Up, a rider with low Animals skill on a newly tamed mount can be thrown when
   dismounting (a bruise), during the mount's first days with them. *Prevent:* yes: riders with Animals 6 or more.
   *Rate:* one ride in 50 in the first days. *Seen:* the mount bucking at the gate.
2. **A slip into cold water.** With Vanilla Fishing Expanded, fishing below freezing can end with a slip into the water:
   soaked, and the game's own hypothermia if not warmed. *Prevent:* yes: no fishing in a hard frost. *Rate:* one in 200
   hours of cold fishing. *Seen:* the splash, the dash for the fire.
3. **Dirty water.** With Dubs Bad Hygiene, drinking from untreated water makes the stomach bug likelier, only when that
   mod's own illnesses are off (so nothing is counted twice). *Prevent:* yes: a water treatment. *Rate:* as a food
   poisoning. *Seen:* the bug, the message naming the water.

**Pick:** 1. Mounts are wished for already, and the first rides are the natural place for a small risk.

---

## Count

| Group | Systems | Proposals | Preventable (fully or in part) | Not preventable |
|---|---|---|---|---|
| 1. The council | 5 | 13 | 13 | 0 |
| 2. Answers, events and the world beyond the walls | 10 | 28 | 27 | 1 |
| 3. A colonist's life | 13 | 31 | 26 | 5 |
| 4. The colony's year, its work, its animals and its mods | 9 | 22 | 21 | 1 |
| **All** | **37** | **94** | **87** | **7** |
