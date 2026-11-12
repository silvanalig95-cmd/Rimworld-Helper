# Rimfolk's peoples: events, interactions and camps of their own (proposal, 11 November 2026; round 1 built: 10, 5, 47, 83, 51, 1, 19, 17, 34, 95; round 2 built: 14, 22, 26, 59, 12, 25, 3, 20, 40, 63)

All of this was read-only. The to-do entry in `docs/HANDOFF.md` asks for a list per people before anything is built, and for new peoples only after that.

**How to read the list**
- Every item comes only from a people of that calling who is at peace with the colony, as all their touchpoints do now. That includes any people that arose in their ways.
- Each one gives three answers, and the last always sends them on.
- **(gift)** marks a lasting change to one named colonist or their beast, which is the shape of the gene event.
- **(camp)** marks a camp kind of the people's own: `CampKindDef.calling`, built on `Source/ColonyCouncil/Camps/`. No camp repeats a generic camp kind a people already favours, or the winter camps being added now.
- Visitors come by the colony's means. Each item says what changes with the age.
- Harm appears only where an item says so. It is either the game's own (fights, surgery, fire, the roads) or Rimfolk's shared harm and illness budgets, and it is shown on the event card.
- None of it needs the letter content that is on hold.

---

## 1. The event the player means: *Gene work at the gate* (the Helix Works)

Where it lives:
- `Rimfolk_Evt_HelixGeneWork` in `Defs/IncidentDefs/Rimfolk_Events_PeoplesG.xml`
- `HappeningWorker_HelixGeneWork`, `DilemmaEffect_HelixGene` and `HelixGenes` in `Source/ColonyCouncil/Peoples/PeoplesG.cs`
- `PeoplesGMath.Offered` and `GenePrice` in `PeoplesGMath.cs`

**When it can come**
- Biotech is on, and a Helix Works people (calling `editors`) is not hostile to the colony.
- A grown colonist lacks one of the Works' ten gentle genes: strong immunity, fast wound healing, superclotting, strong stomach, tox resistance, robust, dark vision, cold tolerant, heat tolerant, reduced pain.
- That colonist carries nothing that conflicts with the gene, and has no xenogerm still replicating.
- Not before day 15, at most every 45 days (base chance 0.6, Visitors group).

**Who comes:** one sequencer, aged 28 to 60, who waits at the gate for 8 to 12 hours. The Works pick both the colonist and the gene; the card names them.

**What the player decides** (half a day to answer; no answer means sent away):
- **Write it in** for silver. The price is 180, plus 90 for each point of metabolism, plus 40 for complexity. That makes **310 silver** for a one-point gene and **400** for fast wound healing, tox resistance or robust. Goodwill +2.
- **Pay with one of the colony's genepacks.** Goodwill +3.
- **No gene work here.** The sequencer marks it in a little book and leaves.

**What happens on the map**
1. The silver or genepack is carried out to the sequencer.
2. The colonist is held still where they stand (a forced wait).
3. The sequencer walks over and works on them for two hours, with a progress bar and the healing cross.
4. The gene goes in as a xenogene, with the game's own xenogerm-replicating mark, so nothing more can be put in until it wears off.
5. A message names the gene, and the sequencer walks home at an errand's pace.
- If the colonist leaves before then, or the gene no longer fits, the answer is greyed out.

**The cost:** the silver or genepack, plus the gene's own metabolism (the colonist gets a little hungrier, as the game counts it). There is no harm roll.

**Why it works:** the people's motto ("every body is a draft") comes in person, and changes one named colonist for good, at a price the player chooses, where the player can watch.

---

## 2–3. Each people: lore, what it has, and what it could do

### The road league (traders; industrial)
**Lore:** Edda Vane took a caravan through Harrow Ford under nine flags. The league keeps the roads and the Waystone Book: fair dealers in good ink, robbers in red.
**Has:**
- The outrider: their caravan, bidden to the colony.
- Winter quarters, *their own camp* (winter only): trading, wardens if asked, a waystone left at the gate.
- The factor to see home (ask).
- Callers and world events that prefer them: a patrol after robbers, a great caravan, a trading season, market days, the road in use, a wardens' post, a new road.
- Favoured camps: market folk, salt caravan, far merchants, road wardens.

1. **Crates held in trust.**
   - A league caravan with a lame pack beast (a broken truck for an industrial league) asks to leave six to ten sealed crates in the colony's stores until a second caravan fetches them. Its porters carry them in, and the master writes the colony into the book.
   - If every crate is there at the fetch: good ink. They pay a tenth of the crates' worth as a fee, and every league gives the colony a few points better prices for a year.
   - If a crate was opened, sold or is missing: red ink, a grudge, and no league caravans for a season. A raid that burned the stores is told as it happened and forgiven.
   - Ages: beasts and carts, a truck's load, a lander's pods.
   - Reuses: arrivals, the errand lord, play-out carrying, deeds, the Republic's price patch. No mod needed. **Medium.**

2. **Red ink.**
   - After the colony robbed or killed traders of any people (that deed in the peoples' memory), the league's factor comes to the gate with the book open at the colony's page.
   - The colony can pay the wronged people back (silver by the deed's size; the deed is settled and part of their goodwill returns), or send its best talker to argue the entry (a Social roll on the card: struck out, or written twice).
   - Or it refuses, and every league trades with it at worse terms until it pays.
   - Ages: the same book in every age.
   - Reuses: deeds, the diplomacy debts, the price patch, the event card. **Small.**

3. **One guard, one wagon.**
   - While a great caravan or a trading season runs within reach, a waymaster asks for one fighter and one pack beast to walk under the colony's own flag, as every waymaster did for Edda Vane.
   - The colonist leaves as a caravan of one and comes home with a share of the profit and a trade lesson. The league gives +8, and its caravans come half again as often for a year.
   - The roads' ambushes and weather are the world's own.
   - Ages: a pack beast in every age; with Giddy-Up 2 the colonist rides.
   - Reuses: the Republic conscript's caravan of one, world events, the caravans' weighting, the ledger. **Medium.**

4. **(camp) The road gang.**
   - When a new road is laid through the region (the world event), a waymaster, labourers and two road wardens camp at the map's edge for 8 to 12 days. They lay the road across the colony's land a stretch a day, so the player watches it grow.
   - They ask leave where it crosses the colony's ground (they route round fields unless allowed through). They offer a spur to the colony's gate for silver or stone, hire the colony's builder by the day, and buy food.
   - The road stays when they go.
   - It can turn: a robber band comes against the camp (bands against a camp; the colony may stand with them). Or the colony refuses the line, and they lay it round its land and think less of the colony.
   - Ages: packed earth, flagstones, concrete, paved tile.
   - Reuses: `calling`, `situationKinds`, a new day's work (laying floor along a line), camp asks, bands against a camp. **Large.**

### The hospitaller order (healers; medieval)
**Lore:** Aldith nursed a fever town alone, and hung its last lamp over the sickhouse door for anyone. "Nobody on a sickbed is anyone's enemy."
**Has:**
- The hospitaller at the gate; the night nurse for fevers.
- Medicine for the sickhouse (ask); a healer lent (ask, by patch).
- Their sickhouse tends the colony's lodgers.
- No camp of its own. Favoured camps: healers, lone healer, herb-gatherers, pilgrims, penitents.

5. **(gift) A limb fitted.**
   - For a colonist missing a hand, foot, arm or leg, a hospitaller surgeon (Medicine 10 to 16) comes with a limb of the order's age. They fit it at the colony's medical bed, with the colony's doctor assisting and learning.
   - It uses the game's own surgery and its chance, so a rare failure is the game's own mishap, shown on the card.
   - No silver ("we were spared, and so we owe"). The colonist's name goes into the Book of the Spared, and the colony owes the order a favour.
   - Ages: a peg leg or wooden hand from the colony's wood; a prosthetic once the order is industrial. Never bionics: those are the enclave's (22).
   - Reuses: arrivals, `DilemmaEffect_Operation` and the game's install recipes, favours owed. **Medium.**

6. **Nobody on a sickbed is anyone's enemy.**
   - After a raid, with raiders still lying downed (not of a permanent enemy), two or three hospitallers come with litters to carry them to the sickhouse.
   - If the colony lets them go: their people think better of the colony by how many were carried, as the Bell's goodwill for the dead works, and the order gives +5.
   - If the colony keeps them as prisoners, the order says nothing but writes it down.
   - Ages: litters, then a stretcher cart.
   - Reuses: the Bell's goodwill rule, play-out carrying, deeds. **Small.**

7. **The sick room.**
   - When a sickness is spreading in the colony (the illness budget's spreading kinds), a sister comes and asks for a sick room. The sick are carried there, and for three days she doses them on rounds while the well stay out.
   - The illness spreads at a third of its rate. She may catch it herself (the budget's own chance), and then the colony nurses her.
   - Ages: herbs and boiled linen, then the colony's medicine and masks.
   - Reuses: the illness budget, `VisitorServices.Dose`, the hospital, play-out carrying. **Medium.**

8. **(camp) A field sickhouse.**
   - In a plague year, or after a war in the region, sisters pitch a long hut of beds by the colony, with a lamp lit at its door all night.
   - Over the days, two to six sick or wounded strangers of the region walk or are carried in, and the sisters dose them on rounds.
   - They ask the colony for medicine, food and clean cloth, and for its doctor to work the ward for a day. The doctor learns a lot and may bring the sickness home, through the illness budget. The colony's own sick may be taken in too.
   - It can turn: the illness crosses to the colony, a patient dies and the Bell comes for them, or a band comes for the medicine.
   - When they go, the hut stays standing, and the colony's name is in the Book of the Spared, with goodwill from the order and from each people whose sick it fed.
   - Reuses: `situationKinds`, visitors walking to a camp, `Dose`, the illness budget, deeds. **Large.**

### The herding folk (herders; tribal)
**Lore:** In the dry year, Tamsa walked the last forty head to grass nobody believed in. They count every beast at dusk; "a guest eats from the same pot."
**Has:**
- The winter herd, *their own camp* (fall and winter, with a young beast at the gate after a good stay).
- Fodder (ask, by patch); a thirsty herd, a sick herd, drovers on the road (callers); the drove roads and the great herds (world events).
- Favoured camps: drovers, shearers, beast-catchers.
- Nothing of their own at the gate: the thinnest of all.

9. **Grazing across the land.**
   - A drove of twenty to forty head asks to cross the colony's land and graze there for two or three days. The herd walks in from one edge, and the drovers' fire burns at the other.
   - If allowed: they leave a young beast, or hides and milk, and the colony's best hand at beasts may ride the drove for a day (a lesson).
   - Unfenced fields lose some crops, shown on the card. Rarely, a spooked beast kicks a colonist (the harm budget).
   - Or the colony charges a toll (they pay, and think less of it), or turns them away (they go round, and think less still).
   - Ages: on foot in every age; with Giddy-Up 2 the drovers ride.
   - Reuses: the great herds' walking lords, arrivals, the harm budget. **Medium.**

10. **(gift) A beast for a colonist.**
    - A rider leads in a young beast the elder chose for one named colonist ("the grass will tell"): a horse, yak, dromedary or muffalo by the land, and with Giddy-Up 2 one that can be ridden.
    - The colony takes it for a fair price, or trades one of its own young beasts.
    - Colonist and beast walk to the pen together. The beast learns its first tasks and is bonded to them (the game's bond). The rider eats at the colony's table that night.
    - Ages: the same beast in every age.
    - Mods: Giddy-Up 2, Alpha Animals, Vanilla Animals Expanded, Megafauna (by the land).
    - Reuses: arrivals, the gene work's shape, the visitors' supper. **Medium.**

11. **The thief on the drove road.**
    - When raiders drive off the colony's animals (Animal Husbandry Raids, which the player has), herders' riders come the next morning on the thieves' trail.
    - The colony can send a rider with them: a short caravan to the thieves' camp, and a fight by the game's own rules.
    - Or the herders go alone: they bring back what they can, and keep one beast as their due.
    - Ages: on foot or mounted.
    - Reuses: the stray's lead home, the calls to arms' fight at a site, arrivals. **Medium.**

12. **(camp) Water in the dry year.**
    - In a drought (Odyssey's, Vanilla Events Expanded's, or the region's dry year), a herders' camp brings up to a dozen head and dogs to the colony's water for five to eight days.
    - They ask for hay carried out, or the colony's grazing. They offer milk, a hand with the colony's beasts, and young beasts at the gate after a good stay. Their people remember it (+10, and the region hears).
    - It can turn: the herd strays into the colony's fields, or a predator follows it in. If turned away, a later news line says how many head they lost on the road.
    - Reuses: the winter herd's animals, milk and young beast; `situationKinds`; deeds. **Medium.**

13. **(camp) Spring calving.**
    - In spring, herders bring in-calf beasts to calve on the colony's sheltered ground for 8 to 12 days, and count every head aloud at dusk.
    - Calves are born at the camp (the game's own births). A hard birth brings a herder to the gate asking for the colony's best hand at beasts: a roll shown on the card decides whether both live (the colony gets the calf) or the mother is lost.
    - They ask for hay and offer milk.
    - It can turn: afterbirth draws a predator in the night (the camp's offal rule, through the budget), or a calf goes missing and they search the colony's land with lanterns.
    - Mods: Alpha Animals, Vanilla Animals Expanded.
    - Reuses: the winter herd camp, the young beast at the gate, `CampAsk.theyAsk`, the offal leftover. **Medium.**

### The pilgrim fellowship (pilgrims; medieval)
**Lore:** Old Brannoc, nearly blind, walked to every holy place and blessed every door at dusk, open or shut: a shell for every shrine.
**Has:**
- A night's shelter (a cough one night in ten); the Long Walk; bread for the hostel (ask).
- Callers: hungry pilgrims, pilgrims to the relic and the anima tree, a traveller on the wrong road.
- World events: a great pilgrimage, a holy year, a prophet.
- Favoured camps: pilgrims, procession, penitents, mourners.

14. **(gift) A pilgrimage.**
    - A file bound for a shrine (a fellowship town, a relic's town, an old shrine site) asks if anyone at the colony would walk with them.
    - The colonist sent is away 10 to 30 days as a caravan of one. The roads, the weather and the pilgrims' cough are the world's and the budget's own.
    - They come home with a shell, a lasting memory of the shrine, steadier faith (Ideology's certainty), and a grief or guilt eased. The fellowship gives +6.
    - Ages: on foot in every age.
    - Mods: Ideology, VE Relics and Artifacts.
    - Reuses: the conscript's caravan, the ledger, the heart, faith. **Large**; it builds the "season away" shape that 3, 67 and 98 reuse.

15. **An old shrine on the colony's ground.**
    - They find something holy on the map (an ancient structure, a great old tree, a grave older than the colony). Two pilgrims work three days setting stones and a lamp, and bless it; it becomes a building of the colony's.
    - From then on, pilgrims stop there in spring and summer and leave a little silver or herbs, and the colony's faithful feel it when they pray there.
    - If the colony lets it fall to ruin, they mourn it.
    - Mods: Ideology, VE Relics (a relic laid there).
    - Reuses: visitors, faith, play-out work. **Medium.**

16. **Two faiths at one table.**
    - When two faiths in the colony quarrel (the faith clash), or its own faith is near a split (Vanilla Ideology Expanded - Splits and Schisms), a palmer comes.
    - That evening the colony gathers and each side's best speaks.
    - The blessing settles it for a season (certainty steadied on both sides), or, by the speakers' Social shown on the card, the split comes anyway and is said plainly.
    - Reuses: the faith evening, the gathering play-out, the council. **Medium.**

17. **(camp) A hostel on the road.**
    - In a holy year or a great pilgrimage, two or three of the fellowship raise a hostel by the road through the colony's land for 10 to 15 days: a long hut of bedrolls, the shrine's flame, a bread table.
    - Travellers of every people at peace walk in for a night or two and walk on, so the colony sees the region's faces and hears their news (places heard of).
    - The hostel asks for bread each morning (food carried out) and invites the colony's people to the evening blessing.
    - It can turn: the cough goes round the crowded hut (through the budget), or two travellers of feuding peoples quarrel (the camp quarrel and its brawl rate).
    - The hut stays as a shelter for the road.
    - Reuses: `situationKinds`, arrivals to a camp, the pilgrims' cough, places heard of. **Large.**

### The learned society (scholars; industrial)
**Lore:** Ilse Varro carried eleven books out of the burning archive at Hesk, and copied them for the rest of her life. "Nothing known should be lost."
**Has:**
- The copyist; the night of the fire.
- Callers: a travelling scholar, a book swapper, a travelling teacher.
- World events: an old vault opened, a comet.
- Favoured camps: scholars, surveyors, the teacher, the monolith's watchers.

18. **(gift) The fellowship examined.**
    - When a colonist reaches Intellectual 12, a fellow comes, and that evening examines them in a disputation at the gathering spot.
    - If they pass (Intellectual and Social; the chance is on the card), they are named a corresponding fellow:
      - a lasting memory and the region's word (a deed);
      - a book sent each season by the post;
      - with Vanilla Skills Expanded, the Researcher expertise.
    - If they fail: "Strictly speaking, that is two questions," and they are stung.
    - Ages: told stories, slates, books, data.
    - Reuses: the night of the fire's gathering, gifts, deeds, the post. **Medium.**

19. **What lies in the old place.**
    - With an ancient danger, or a landmark (Vanilla Landmarks Expanded, Geological Landforms), on the colony's map, two scholars camp beside it for four days, measuring.
    - Then they say exactly what is inside, the game's own contents: sleeping mechs, a hive, how many caskets, or nothing. They also leave research toward a project.
    - If the colony lets them open it themselves, the danger is the game's own, shown on the card.
    - Reuses: the monolith watchers' camp shape, research, the event card. **Medium.**

20. **Books kept from the fire.**
    - When a society town burns or falls (a ruin in the ledger, a great fire), a copyist brings a cart of the archive and asks the colony to keep it for a season.
    - The colony's readers may read the books (the game's books, and VE Books'); Anomaly's tomes stay sealed.
    - A cart fetches them later, with thanks (+8) and one book left behind. A book lost or sold is a lasting wrong.
    - Reuses: the trust shape of item 1, the ledger, deeds. **Medium.**

21. **(camp) The summer school.**
    - In summer, two or three fellows camp for a week and give a lesson each afternoon at their table.
    - The colony may send one or two colonists a day (Intellectual, Medicine or Social), lend the fellows books, and feed them. The best student goes home with a book, and is likelier to be examined (18).
    - It can turn: two fellows fall into a disputation that the colony's talker can end, or it ends in a sulk and unease.
    - Ages: tallies, slates, books, data slates.
    - Reuses: camp asks carrying the lesson effect, the camp's day. **Medium.**

### The spacer enclave (brokers; spacer)
**Lore:** Ines Okoro sold a stranded hauler's cargo bargain by bargain until the crew owned a landing pad. "A bargain struck is kept to the last digit."
**Has:**
- The lander; salvage from above.
- A tourist (caller); busy orbital lanes and a ship come down (world events).
- Favoured camps: far merchants, stranded spacers, salvagers, market folk.

22. **(gift) The body made better, on terms.**
    - The factor offers a colonist a bionic part (eye, arm, leg, spine, or one of Alpha Implants'), paid now or on terms: a fifth down, then four seasonal instalments asked for by letter.
    - Their surgeon drops in by pod, or walks in to a tribal colony, and fits it at the colony's medical bed by the game's surgery, which rarely fails at their skill.
    - A missed instalment is a default: goodwill falls, the debt joins the colony's debts, and no lander comes until it is paid.
    - Mods: Alpha Implants.
    - Reuses: the lander, the operation, the debts by size, `Tell.Ask`. **Medium.**

23. **A contract to the last digit.**
    - A contract for something the colony makes (meals, cloth, beer, drugs, art, components), due in 15 to 30 days, at a fifth over its worth.
    - On the day, their lander comes down at the trade spot and the marshals carry the goods aboard.
    - Short or late is a default: a fee, and cooler terms. Delivered in full, a bigger contract follows.
    - Ages: a runner and porters on foot for a tribal or medieval colony.
    - Reuses: quests (deliveries), the lander. **Medium.**

24. **Stranded with something to sell.**
    - A broker's pod comes down hard on the colony's land (the game's pod crash), with the broker hurt among the cargo.
    - If the colony rescues and tends them, the lander comes for them two or three days later, and they pay a fifth of the cargo (+10).
    - Robbing the pods is the colony's choice, and earns the enclave's lasting grudge.
    - Reuses: arrivals, outsider tending and the hospital, the lander, deeds. **Medium.**

25. **(camp) A pad by the colony.**
    - Two factors and two marshals rent ground at the colony's edge for 10 to 15 days, paying rent every five days at an exact figure, and lay a small pad and stalls.
    - A pod drops fresh stock each morning, so the colony can trade every day (the game's own trading).
    - They ask the colony to keep its people off the pad and to sell them its meals.
    - It can turn: they try to shave the rent (the camp's terms bent), a band comes for the stock, or the colony raises the rent (they pay or go).
    - Ages: their spacer kit whatever the colony's age; a tribal colony sees pods fall from the sky at dawn.
    - Reuses: the camp trader's stall, `CampContract`'s money flowing the other way, drop pods, bands against a camp. **Large.**

### The river folk (fishers; medieval)
**Lore:** When the dam broke, Wenna Reed's boats pulled people off the rooftops for three days; there is a knot in the net for everyone pulled out. "Carry the stranger across." "Never foul the water another drinks."
**Has:**
- The boat-trader's catch; nets on the bank; timber and net cloth (asks).
- A flood downriver and the ice road (world events).
- Favoured camps: fishers, woodcutters, harvest hands.

26. **A crossing on the colony's river.**
    - Where a river or stream cuts the colony's map, two or three boatmen spend a day laying the game's bridges over water that takes them, and planks on the banks, with wood the colony gives. It reuses their own town's dock-laying.
    - The crossing is the colony's. Once a year, on their knot night, they ask to rest at it.
    - Ages: rope and plank, then timber on piles.
    - Reuses: `PeopleTowns.LayDocks`, play-out work with a bar. **Medium.**

27. **Fouled water.**
    - When the colony fouls water (Biotech pollution or wastepacks near a river or lake, or rotting corpses in it), a river warden walks to the spot and asks for it to be cleaned.
    - If the colony cleans it (its hands and the warden, a bar): +5, and their catches come again.
    - If not: a wrong in their memory, and the boat-trader and the nets stop coming.
    - Ages: corpses in any age; pollution from the industrial age.
    - Reuses: deeds, play-out work. **Medium.**

28. **Lanterns on the water.**
    - On their knot night, boatmen set floating lanterns at dusk on the colony's water for everyone it lost or took in, and the colony gathers on the bank.
    - Those who come keep a memory of it.
    - Mods: with Goji's Fantasy Race: Merren, its floating lanterns stay on the water; otherwise candles on the bank.
    - Reuses: the vigil play-out, the calendar. **Small.**

29. **(camp) Flooded out.**
    - When the river rises at a landing (the flood world event), boats of families camp on the colony's bank for 5 to 10 days, nets out under a tarp.
    - They ask for a dry place and food until their catch comes in, and offer fish and net-mending. Two may ask to stay for good.
    - Leaving, they tie a knot in the net for the colony (+12) and leave their drying racks.
    - It can turn: sickness brought home (the illness budget), or they want to stay longer than the colony likes.
    - With Hospitality, they are its guests.
    - Reuses: the refugees' camp shape, the fishers' work, joining. **Small.**

30. **(camp) The boat-builders.**
    - Three or four camp by the colony's water for a week, fell wild trees, and a hull grows on the bank day by day.
    - The colony's crafter may work beside them (a lesson).
    - With Vehicle Framework and Vanilla Vehicles Expanded, they offer the finished dinghy for wood and silver. Otherwise they float it home and leave their boat shed standing.
    - It can turn: a storm lifts the water and costs a day, or the colony refuses them its trees and they go cooler.
    - Reuses: the woodcutters' felling, a camp building that grows, lessons. **Medium.**

### The Lantern Wardens (hunters; medieval)
**Lore:** Tobin Garrow kept a lantern lit at a crossroads for eleven years, until the beast that emptied his village was dead. They keep a book of every beast run down, and a red cord knot for each.
**Has:**
- The lantern hunt, the pelt-buyer, a lantern at the crossroads, the drive (ask).
- The old beast (world event).
- Favoured camps: hunting party, road wardens, beast-catchers.

31. **(gift) A page in the book.**
    - When a colonist kills a legend, a beast nemesis or a pack's alpha, a Warden comes to write it in the book and ties a red cord on the hunter's sleeve at the gate.
    - The hunter gets a lasting memory and the region's word. With Vanilla Skills Expanded they get the Hunter expertise; otherwise a passion in the skill that made the kill.
    - The Wardens give +6, and ask for the pelt for their hall at twice its worth.
    - Mods: Megafauna, Dinosauria, ReGrowth (the beasts that make legends).
    - Reuses: the kill as the happenings see it, gifts, deeds, the pelt-buyer. **Small.**

32. **Wardens at the manhunt.**
    - When a manhunting pack or a mad beast besets the colony, two to four Wardens come at a run and fight beside it under the hunt's lord.
    - Afterwards they skin what they felled and split it as on the hunt. The fight is the game's own; the help is free; the Wardens give +3.
    - Ages: their age's arms, guns from the industrial age.
    - Reuses: the hunt's lord and skinning, arrivals. **Medium.**

33. **The hive burned.**
    - When insects dig in near the colony (the game's infestation, or Vanilla Factions Expanded - Insectoids 2's hives), the Wardens come with fire and ask to go in beside the colony's fighters.
    - Together they burn it out (the game's fight). Alone, they go in while the colony watches. Refused, they wait at the edge in case.
    - Ages: torches, molotovs, incinerators.
    - Reuses: the hunt's lord. **Medium.**

34. **(camp) The lantern post.**
    - While a legend, a nemesis or an alpha roams the land or nearby, three Wardens camp at the edge with two hounds, light the lantern every night, and go out on the trail by day. They stay until the beast falls, or ten days.
    - They ask for meat for the hounds. They offer to take the colony's best shot on the trail (a lesson, and a place in the hunt), and they warn when the beast is near.
    - It can turn: the beast comes to their fire at night (the game's fight; the colony may run to help), or a hound is lost.
    - At the end the lantern goes out and the page is written.
    - Reuses: the beasts system, the hunt's lords, camp animals and asks. **Medium.**

### The Quiet Bell (sextons; medieval)
**Lore:** Ysolde Marr buried the raiders nobody would, and rang a cracked bell over each. "Everyone is owed a bell," and they fear a field that stirs.
**Has:**
- A bell for the enemy's dead (goodwill with their peoples); the Bell's funeral.
- The ground is listening (Anomaly); linen for shrouds (ask).
- The undertakers (callers); mourning (world event).
- Favoured camps: mourners, penitents, procession.

35. **The field that stirs** (Anomaly).
    - When the colony's unburied dead rise as shamblers, three gravewardens come at a run and put them down beside the colony (the game's fight).
    - Then they bury them beyond the wall with the bell rung, and ask the colony to bury its dead from now on.
    - Ages: torches and maces, then molotovs and incinerators.
    - Reuses: the hunt's assault lord, the Bell's burial. **Medium.**

36. **Mourners from the other side.**
    - A season after the Bell buried raiders the colony killed, two mourners of the raiders' people come under the Bell's escort to stand at their graves.
    - Let through for an hour, their people warm a little, and a truce can be talked of.
    - Turned away, the Bell is saddened.
    - Reuses: truces, the Bell's graves, the escort lord. **Medium.**

37. **A grave-garden for the colony.**
    - Two sextons work three days laying stones and roses among the colony's graves, and set up a bell-post, a building of the colony's.
    - From then on, visiting the graves gives "quiet ground" (a mood), and a funeral there is a fuller rite. With Anomaly, the dead buried there do not rise.
    - Reuses: the relay mast's building pattern, funerals. **Medium.**

38. **(camp) The field of the fallen.**
    - After a fight leaves a dozen or more dead on or near the land, bell-keepers and sextons camp three to six days, dig a row of graves beyond the wall, and ring the bell at each dusk.
    - They ask for wood and cloth. Mourners of the dead's peoples come to stand at the graves, and each of those peoples thinks better of the colony.
    - It can turn: the field stirs (35), or the dead's people send a band to take them home (talked down, or a fight against the camp).
    - Reuses: the Bell's burial, mourners as visitors, bands against a camp, deeds. **Medium.**

### The Circuit of Tamers (tamers; industrial)
**Lore:** Nessa Pell walked the region for forty years with what hatched from a star-crater egg, challenging every keeper of beasts. "A bond is not a leash."
**Has:**
- A friendly bout (an egg; one in 25 a bruise); the egg-trader; a stray brought home; a young beast raised for a season (ask).
- The vet (caller); the fair (world event).
- Favoured camps: trainers (PokéWorld), beast show, beast-catchers.

39. **(gift) The bond freely given.**
    - A roaming tamer finishes one of the colony's animals' training in an afternoon at the pen, with the colony's best hand at beasts beside them.
    - The beast learns every task it can, and is bonded to that hand. The hand gets a lesson, and with Vanilla Skills Expanded the Trainer expertise.
    - Mods: PokéWorld (a Pokémon trained the same way).
    - Reuses: the bout's visit, the game's training. **Medium.**

40. **A leg for a lame beast.**
    - For an animal of the colony's that lost a leg, the Circuit's beast-doctor comes.
    - Industrial and spacer: she fits Alpha Implants' animal prosthetic, by the game's surgery and chance.
    - Tribal and medieval: she takes the beast to their stables to live out its days, and leaves a young one of its kind.
    - Reuses: the operation, the stray's lead. **Medium.**

41. **(gift) An egg from the crater.**
    - A tamer brings a rare egg of a beast the land doesn't have (Dinosauria, Megafauna, ReGrowth, Alpha Animals or PokéWorld, else the base game's) and asks the colony to hatch it.
    - It must be kept warm until it hatches. The beast is the colony's, bonded to whoever tended it most.
    - The tamer comes back a season later to see it grown.
    - Reuses: the egg-trader, arrivals. **Medium.**

42. **(camp) The Circuit's ring.**
    - When the fair comes, six to ten tamers pitch a bout-ring by the colony for four to six days, with a bout each afternoon, and the colony's hands are invited to challenge.
    - Bouts use the friendly bout's rules: the winner by skill, a rare bruise through the harm budget, shown.
    - The colony's best wins a token and an egg, and the region hears of it.
    - Reuses: camps, `JobDriver_TamerBout`, the harm budget. **Large.**

### The Wheel (salvagers; industrial)
**Lore:** The night the valley burned, Mab Wainwright put her house on her cart, and nobody has voted to stop since. "Nothing is broken, only waiting."
**Has:**
- The fixer, the scrap-runners, a wreck stripped half and half, the axle (ask), towns that move.
- The tinker (caller); iron scarce (world event).
- Favoured camps: salvagers, travelling smith, prospectors.

43. **Mend it tonight.**
    - The night after a raid, a crew with lamps goes round mending the colony's damaged walls, doors, turrets and furniture (sparks, a bar), while a runner gathers the raiders' broken gear.
    - They are paid in that scrap and a little silver, and are gone by morning.
    - Reuses: the scrap-runners' carrying, queued play-out work. **Medium.**

44. **(gift) Made better.**
    - A fixer takes one colonist's own weapon or armour out to their cart for a day.
    - It comes back a quality higher (masterwork at most) and fully mended, paid in silver and steel.
    - Reuses: the fixer's worker. **Small.**

45. **A wagon of our own.**
    - With Vehicle Framework and Vanilla Vehicles Expanded, a fixer builds the colony a vehicle of its age from its steel and scrap, over a day at the gate: the wagon in the medieval age, a car or truck in the industrial.
    - Only with those mods.
    - Reuses: play-out work. **Large** (Vehicle Framework by name).

46. **(camp) The Wheel stops by.**
    - When a Wheel town's move brings it within a few tiles, its yard camps three days at the map's edge: carts, a mending tent, and a scrap market (the game's trading).
    - The colony buys, sells scrap, and has things mended.
    - It can turn: a fixer and a colonist argue a price (the camp quarrel), or the yard decides to stay a few days longer.
    - Reuses: camps, `WheelTowns`. **Medium.**

### The Stormglass Conclave (seers; industrial)
**Lore:** Hesper Lyle sealed a vial of every storm, and learned to read a frost a week ahead. The weather is the planet's psychic tide; "never wrong, never sure."
**Has:**
- A season's watch, a rod on the roof, reading the glass, the apprentice (ask).
- The fortune teller (caller); a year without summer (world event).
- Favoured camps: monolith watchers, surveyors, hermit.

47. **(gift) Storm-sense.**
    - In a thunderstorm, a reader asks the colony's most psychically sensitive colonist to stand out at the rod with her for an hour.
    - The colonist wakes with a first psylink level (Royalty) on Vanilla Psycasts Expanded's Staticlord or Frostshaper path. Without Royalty, with A RimWorld of Magic, they get the Lightning tome.
    - The price is silver, or a vial of the colony's own storm for their archive.
    - Reuses: gifts' psylink, the rod. **Medium.**

48. **Breaking the drought.**
    - In a drought or a heat wave, two storm-walkers set rods and vials on the colony's high ground for a night, and the next day it rains (the game's weather).
    - One time in four the rain comes as a thunderstorm, fires and all (the game's own; the rod helps).
    - Mods: Odyssey, Vanilla Events Expanded.
    - Reuses: the watch, fields by the weather. **Medium.**

49. **The tide read.**
    - During a watch, a psychic drone or soothe is held back like a sky condition, and the runner's warning names whom it will touch.
    - For a drone, a reader sits with the worst touched.
    - Reuses: `Patch_GlassHold`. **Small.**

50. **(camp) The storm tower.**
    - In the stormy months, two readers and an apprentice camp on the colony's high ground for a week, with a glass mast and rods. Lightning near them strikes the rods without fire, and they write every storm.
    - They ask the colony's best at the fields to sit the night watch (a lesson). They leave a vial and a forecast for the season.
    - It can turn: a strike lands past the rods (the game's fire), or the apprentice takes a chill (the illness budget).
    - Reuses: the rod, the watch, camp asks. **Medium.**

### The Mossborn (grovekeepers; tribal)
**Lore:** Elin Sedge planted an acorn beside every stump of a valley that a war had cut down. "Every tree you fell owes the world a seedling."
**Has:**
- Saplings for the felled, a tree for the dead, the tree-connector's pruning, the grove's ledger, a burned grove (ask).
- A blight (world event).
- Favoured camps: tree-singers, herb-gatherers, healers.

51. **(gift) A Gauranlen seed.**
    - Once the ledger has warmed to the colony, a seed-carrier plants a Gauranlen seed where the colony chooses, with the colonist it names beside her. The tree connects to them, and its dryads come in time.
    - The price is a promise: a seedling for every tree felled, which the ledger watches.
    - Mods: Ideology, Vanilla Ideology Expanded - Dryads. Without Ideology, a healroot grove is sown instead.
    - Reuses: the ledger, the saplings' planting. **Medium.**

52. **Fire in the canopy.**
    - When a wildfire burns in the colony's woods, three to five thorn-guards come at a run and beat it out beside the colony's people (the game's firefighting).
    - Burns are the game's own.
    - Reuses: arrivals, the ledger. **Medium.**

53. **Seeds of the old wood.**
    - An herb-trader brings seeds of plants the colony doesn't grow (Vanilla Plants Expanded and its add-ons, Alpha Biomes) and sows a plot with its grower, who learns them.
    - Reuses: the egg-trader's shape. **Small.**

54. **(camp) After the fire.**
    - After a wildfire or a blight, a grove-warden and seed-carriers camp in the ash for a week. They cut the burned trees (the logs are the colony's), plant saplings across the burn, and sing at night.
    - They ask for food, and that the colony not fell within the planting. The colony's planter may plant beside them, and the ledger warms.
    - It can turn: the colony fells in the planting (a wrong; they leave), or the fire starts again (the game's).
    - Reuses: `MossPlanting`, the ledger. **Medium.**

### The Ashen Banner (mercenaries; medieval)
**Lore:** Corra Mallin kept five hundred unpaid soldiers together under one grey banner. "Paid to the day, held to the day," and the Roll keeps every name.
**Has:**
- The squad for the season, *their own camp* (it fights; wages asked).
- A deserter at dusk, the Roll read at a grave, the muster (ask).
- Mercenaries (world event).
- Favoured camps: deserters, wounded fighters, road wardens.

55. **A contract on the colony's enemy.**
    - The captain offers to march against an enemy camp within reach (a bandit outpost from the game's quests, a hostile town), at a price by its size.
    - The colony sends at least one fighter, and the company meets them there for a fight on the game's map of the site.
    - Loot is split by the contract, and every name lost is read on the Roll.
    - Ages: the Banner's issue.
    - Reuses: the calls to arms' trip to a site, quests, the ledger. **Large.**

56. **An old soldier settles.**
    - A veteran asks to settle with a war-horse and kit.
    - Once a year the Banner calls them to the muster for a week (a caravan of one), or the colony pays a muster fee. Refusing both costs goodwill.
    - Mods: Giddy-Up 2.
    - Reuses: joining, the conscript's caravan. **Medium.**

57. **The other side's contract.**
    - When a people at war with the colony hires the Banner, a sergeant comes under a white flag to say so.
    - The colony can buy out the contract, pay them to stand aside, or let them march with that people's next raid (the storyteller's own size).
    - Reuses: the game's incident queue, as the relay mast holds raids; diplomacy. **Medium.**

58. **(camp) A company on the march.**
    - Eight to twelve soldiers on their way to a contract camp for a night or two at the colony's edge, in a square of bedrolls, and drill at dawn.
    - They buy food in quantity. The sergeant offers to drill the colony's fighters, and asks whether any of them would enlist for a season. A soldier may ask to stay, which the Banner holds against the colony.
    - It can turn: the enemy they march against catches them here (a band against the camp; the colony may stand with them), or a soldier and a colonist brawl over a bet.
    - Reuses: bands against a camp, the deserter, lessons, quarrels. **Small.**

### The Kiln Guild (artisans; medieval)
**Lore:** Oswin Kell rebuilt the Hardwater bridge three times and cut his mark into every stone. "Bad work is a lie told in stone."
**Has:**
- A commission, the guild collects and pays by quality, a bid on a masterwork, the wander-year (ask).
- The art collector (caller); a great work (world event).
- Favoured camps: travelling smith, stonecutters, market folk.

59. **(gift) The guild's mark.**
    - A master-smith reworks one colonist's bow or gun into a unique piece of its kind (Odyssey's and Vanilla Weapons Expanded's unique weapons; their traits are the game's), or a blade into a masterwork in better stuff. The colonist's name goes into its label.
    - The work takes a day at the colony's bench while the colonist watches, paid in silver and the stuff.
    - Ages: a unique greatbow, a medieval crossbow, rifles, charge guns.
    - Reuses: the wander-year's bench. **Medium.**

60. **(gift) The master's mark.**
    - When the colony's crafter makes a masterwork or a legend, a guild master judges it at the gate.
    - A true mark gives a memory and the region's word (and with Vanilla Skills Expanded a quality expertise). A lie found in it stings.
    - Reuses: the fine-piece hook (`Patch_ArcMade`), deeds. **Small.**

61. **Come back better.**
    - A year after a wander-year at the colony ended well, that journeyman returns as a master with a masterwork as a gift.
    - The maker who taught them gets a lasting memory.
    - Reuses: the wander-year's `learnedHere` deed. **Small.**

62. **(camp) A work that stays.**
    - Three journeymen camp five to eight days and build a great work with the colony's stone or wood: a grand sculpture in its hall, a carved bridge over its river (Kell's), or a fountain where it gathers.
    - It carries the guild's mark: beauty, and the colony's renown.
    - It can turn: the colony's stone runs short and they wait, or a journeyman's work is judged bad and torn down.
    - Reuses: camps, colony renown, play-out work. **Large.**

### The Signal Line (linesmen; industrial)
**Lore:** Ennis Calloway walked the hills with copper wire, raising masts until one valley could hear the next. "Silence is the only storm."
**Has:**
- A relay mast (raid warning, faster letters), the wire-trader, a climb in the storm, the fallen span (ask).
- Two peoples who want to talk (world event).
- Favoured camps: surveyors, monolith watchers, travelling smith.

63. **News for sale.**
    - An operator sells what the masts heard, for 60 to 200 silver: a site within reach (an item stash, a downed shuttle's cargo, a bandit outpost).
    - The game's own quest site appears, and the places-heard-of marks show it, with where they heard it.
    - Reuses: quests, places heard of. **Medium.**

64. **A voice for the colony.**
    - One call a season on their masts: ask for a trader of a kind (the game's caravan of that kind comes), ask for hands (a wanderer comes to join), or speak well of a people (a little goodwill).
    - Ages: their runners carry the call where the colony has no radio.
    - Reuses: the outrider's bidden caravan, joining. **Medium.**

65. **A mast at a friend's town.**
    - For a fee they raise a mast at the town of a notable the colony writes to, and letters there take half the days.
    - Reuses: the mast's relaying, the post. **Small.**

66. **(camp) The line crew.**
    - Linesmen camp on the colony's high ground for a week, stringing wire and raising a working mast. The radio plays the region's news at their fire (places heard of), and the colony's letters go out by their wire.
    - They ask for components and food. The colony may keep the mast as its own relay mast, for a fee.
    - It can turn: a lineman falls in a storm (the harm budget), or a storm takes the mast.
    - Ages: flag-and-lamp towers before the industrial age.
    - Reuses: the relay mast, camp asks. **Medium.**

### The Deepwell Union (drillers; industrial)
**Lore:** When the seam ran dry, Marta Grieve put it to the vote, and they walked out together with their picks. "Everyone up at the end of the shift."
**Has:**
- A drill for a share (one shift in twenty an injury), the assayer's crate, the strike, the short shift (ask).
- A silver rush (world event).
- Favoured camps: prospectors, stonecutters, salvagers.

67. **The pit's cave-in.**
    - When a union pit within reach caves in, they ask for the colony's miners and doctor.
    - Two colonists go as a caravan for four to six days and dig out the trapped. One shift in twenty brings a crush, through the harm budget, as at the drill rig.
    - They come home with pay, +12 and skill gained.
    - Reuses: the conscript's caravan, the harm budget. **Large.**

68. **Oil in the ground.**
    - With Rimefeller, the assayer finds the colony's oil by Rimefeller's own deposits and sinks a well for silver and a year's share.
    - Without it, the assayer marks a deep seam for the union's drill.
    - Reuses: the drill's play-out. **Medium.**

69. **The organizer's evening.**
    - When the colony's people are worn down (the heart's strain, long hours by decree), a shift-captain speaks at the colony's fire.
    - Next morning the council meets on a petition for a day of rest a week.
    - Granted: spirits lift, and the union thinks better of the colony. Refused: those under strain are stung.
    - Reuses: the council's petitions and decrees, the heart. **Medium.**

70. **(camp) The picket.**
    - When a union strike is called, strikers camp by the road for its length: a fire in a barrel, signs, songs.
    - The shift-captain who settles it comes from here. They ask for food for the picket, and invite the colony's miners to their evenings.
    - It can turn: strikebreakers of another people come against the camp, or the strike ends by vote and they march home singing.
    - Reuses: the strike, bands against a camp. **Medium.**

### The Last Republic (citizens; industrial)
**Lore:** Clement Ashdown, the last official still answering the radio, read the old laws aloud and called an election. "There is a form for that."
**Has:**
- The census, papers, a conscript, the radio, a witness at the count (ask).
- A republic's vote (world event).
- Favoured camps: refugees, market folk, road wardens.

71. **An observer at the vote.**
    - At the colony's election, a clerk walks the colony with the roll and stands by the count.
    - Hustings come to blows half as often, and the radio speaks of a fair vote.
    - Reuses: elections, the radio, the hustings' harm. **Medium.**

72. **(gift) A medal.**
    - After a colonist's great deed (held the wall, a rescue under fire), a clerk and a guard read a citation at the gate.
    - The colonist gets a lasting memory, the region's word, and the colony's renown rises.
    - Mods: VE Books (a newspaper).
    - Reuses: deeds, renown, the radio. **Small.**

73. **A fugitive of the Republic.**
    - A Civic Guard deserter asks to join, and a Guard detail follows to the gate.
    - Hand them over (+8); shelter them (-10, and the papers are struck; a hostile detail fights by the game's own); or pay their fine.
    - Reuses: joining, the papers. **Medium.**

74. **(camp) A ration post.**
    - In a famine, a clerk and two guards keep a ration post by the road for a week. Hungry travellers walk in, take a ration and go.
    - The post buys food from the colony at a fixed price, offers ration cards to a citizen colony, and some of the hungry ask to join.
    - It can turn: the food runs short and the queue riots (the camp quarrel's rate), or the guards turn away someone the Republic denounced.
    - Reuses: arrivals to a camp, joining, the papers. **Medium.**

### The Hollow Fleet (crew; spacer)
**Lore:** Captain Sallow died before giving the order to disembark, and the crew still keeps its posts in the grounded hull. "Holding position."
**Has:**
- The shuttle (with a casket for sale), salvage, the sleeper woken too late, the lift (ask).
- Exiles (world event).
- Favoured camps: stranded spacers, salvagers, surveyors.

75. **The colony's gravship.**
    - With Odyssey, when the colony has a gravship, officers come by shuttle to walk its decks with the colony's pilot and log it.
    - They offer a part from the hull's stores (Odyssey's, Vanilla Gravship Expanded's) or a lesson. +10.
    - Reuses: the shuttle. **Medium.**

76. **A sleeper of theirs.**
    - Now and then a sleeper in the game's ancient caskets wears the ship's grey, and a deck-runner comes for them.
    - Handed over: they pay (+12). Kept: the sleeper joins.
    - Reuses: the shuttle, deeds. **Medium.**

77. **The order to disembark.**
    - With Royalty: a colonist holding an Empire title of knight or higher is asked to give the order at the hull.
    - They go there by caravan. Three to five of the crew come home with them and join, and the rest found a new people (release B's arising).
    - Reuses: the conscript's caravan, arising. **Large.**

78. **(camp) A landing party.**
    - Four to six crew make a ship-shape camp by their shuttle: bunks in a row, a bell for every watch, everything logged. They survey for a landing site "for when the order comes".
    - They buy food and offer their medic. A deckhand may ask to disembark here and join, against the officer's wish.
    - It can turn: a sleeper they brought wakes sick, or their watch spots raiders first and warns the colony.
    - Reuses: the shuttle, camp asks, joining. **Small.**

### The Helix Works (editors; spacer)
**Lore:** Orla Venn kept the vats warm after the founders fled, and taught the vat-grown to edit. "Every body is a draft; ask before you change it."
**Has:**
- The sample, gene work, the vat-keeper's case, a batch gone wrong, genepacks (ask).
- A famous healer (world event).
- Favoured camps: healers, stranded spacers, herb-gatherers.

79. **(gift) A gene taken out.**
    - They take a harmful gene out of a colonist, where they stand, in two hours, for silver.
    - The colonist loses the metabolism that gene gave, and gets the same mark as gene work.
    - Reuses: the gene work's job. **Small.**

80. **(gift) A whole xenogerm.**
    - A whole xenogerm of one of the Works' kinds, for silver and genepacks. The colonist lies in the game's xenogermination coma afterwards.
    - A faith that looks down on xenotypes says so on the card.
    - Reuses: gene work. **Medium.**

81. **A hybrid beast.**
    - With Vanilla Genetics Expanded, a vat-keeper sells a young hybrid from their vats.
    - Reuses: the egg-trader. **Medium.**

82. **(camp) A field lab.**
    - Two sequencers and a vat-keeper camp for a week, sampling the land's beasts and plants.
    - Gene work (and the sample) are open on the camp's page all week. They buy genepacks.
    - It can turn: a gene-crafted beast of theirs gets loose (the game's manhunter), or a vat-grown falls ill.
    - Reuses: `DilemmaEffect_HelixGene` and `_HelixSample` as camp asks. **Medium.**

### The Lattice (mechanitors; spacer)
**Lore:** Tamsin Arro linked ten abandoned mechs to her own mind and taught three others to take ten each. "A mind alone is a candle; a lattice is a lamp."
**Has:**
- Repairs, a lost mech, the part-trader, a turret for the gate, two crafters for a day (ask).
- A town rebuilding (world event).
- Favoured camps: salvagers, travelling smith, surveyors.

83. **(gift) A mechlink.**
    - A mech-tender installs a mechlink in a colonist over two hours (sparks, a bar), and links a lifter or constructoid to them to start, for silver or subcores.
    - A tribal colony's people find it strange (a mood).
    - Mods: Biotech, Alpha Mechs.
    - Reuses: the gene work's hold-and-work job, the lost mech's linking. **Medium.**

84. **(gift) Linked into the Lattice.**
    - A mechanitor of the colony's is linked in: +2 bandwidth for as long as the Lattice stays at peace with the colony.
    - Now and then they dream another's dream (a small mood, either way).
    - Reuses: gifts. **Small.**

85. **A mech gone feral.**
    - A mech of theirs runs wild near the colony, and link-wardens come to take it down or relink it.
    - Relinked, they pay; destroyed, the colony keeps the parts. The fight is the game's own.
    - Reuses: the lost mech. **Medium.**

86. **(camp) A mech yard.**
    - A mech-tender and two link-hands camp with four to six mechs that mine plain rock and haul round the camp.
    - The colony may hire the mechs for a day to mend or haul.
    - It can turn: a mech goes feral (85), or a hostile mechanitor's band comes after their mechs.
    - Reuses: the camp's work, bands against a camp. **Large.**

### The Choir of the Spire (listeners; beyond spacer)
**Lore:** Ilsabet Ruen woke able to hear the Spire think, and someone has been listening ever since. "The hum is not silence."
**Has:**
- The Choir listens, the keeper of the hum, the listener's stay (ask).
- A season of wild magic (world event).
- Favoured camps: pilgrims, hermit, minstrels.

87. **(gift) The hum deepened.**
    - A cantor sits through a night with a colonist who has a psylink, and by morning the psylink is a level higher (with Vanilla Psycasts Expanded, one more power). A sensitive colonist without one wakes a first level instead.
    - The price is art, or an evening of being listened to; unease is possible by the listening's rule.
    - Reuses: gifts, the listening job. **Medium.**

88. **The void listens back** (Anomaly).
    - A warden of the quiet sits a day with a colonist shaken by the void, and some of their sanity returns (Vanilla Anomaly Expanded - Insanity).
    - They also say what kind of threat the void sends next.
    - Reuses: sitting with, the Bell's warnings. **Medium.**

89. **A mind that shouts.**
    - A listener sits beside a colonist in a long mental break, which ends sooner and calm.
    - **Small.**

90. **(camp) A listening ring.**
    - Three to five of the Choir camp for three to five days at the colony's quietest place and sing each evening, and everyone at home feels it each night.
    - Psycasters who sit with them refill their focus. They ask that loud work be kept away at night.
    - It can turn: with Anomaly the void stirs, their song turns strange, and they leave early.
    - Reuses: `ChoirListening`, `PeoplesGMath.Felt`. **Small.**

### The Sunken Vault (vaultfolk; spacer, falling)
**Lore:** Overseer Hollis Crane broke the seal when the water ran short, and half of them went back below for a week. "Every cell counts."
**Has:**
- The last goods, a craft taught, a child who never saw a sky, power cells (ask).
- A new town (world event); the supply that holds their age.
- Favoured camps: refugees, prospectors, salvagers.

91. **The seal broken.**
    - Insects (Insectoids 2's hives) or mechs got into the vault's lower level, and the overseer asks the colony's fighters to come.
    - A caravan goes to their site and fights beside their security (the game's fight). If it is won, the colony gets a share of the vault's spacer stores, +15.
    - Reuses: quests' sites, calls to arms. **Large.**

92. **(gift) The last part.**
    - When a colonist loses an eye or a limb and the colony has no part, the vault's medic trades one of their last bionics for a large supply of components and food. That supply holds the vault's age for a year.
    - It is fitted by the game's surgery.
    - Reuses: the operation, the vault's supply. **Medium.**

93. **The console that talks to nobody.**
    - The vault's console picks up the colony's radio, and for a few evenings the overseer only wants to talk with its best talker at the comms console.
    - At the end they ask for something, or tell where an old supply cache lies (a site).
    - Ages: a surface-runner with letters for a colony without a radio.
    - Reuses: quests, places heard of. **Medium.**

94. **(camp) Surface scouts.**
    - Three vault scouts camp near the colony for a week to learn the surface. They ask to be taught each afternoon (plants, animals, shooting), which counts as the vault's supply and learning and holds its age.
    - They trade their last odds and ends, and one may ask to stay.
    - It can turn: one is sky-sick and won't come out, they get lost on the map at night, or a beast they don't know to fear mauls one (the game's own).
    - Reuses: hosting's learning, the vault's supply, peoples' ages. **Medium.**

---

## 4. Between peoples

95. **Kill it or win it over** (the Wardens and the Circuit).
    - When a legend beast is on the colony's land, Wardens and a tamer arrive the same day.
    - The colony can side with the hunt (the lantern hunt and its split), or with the taming: the game's own taming tries, with the beast's revenge at the game's rate.
    - Or it can let them settle it in a bout at the gate.
    - The losing side thinks less of the colony, and the region hears of it.
    - Mods: Megafauna, Dinosauria, ReGrowth, PokéWorld.
    - Reuses: the hunt, the bout, the beasts system. **Medium.**

96. **The moot over the old wood** (the Union and the Mossborn).
    - A pit wants to sink a shaft under an old wood near a grove, and both send envoys to the colony as neutral ground.
    - The colony's best talker hears them at its table, and rules:
      - for the shaft: the Union gains, the Mossborn lose, and steel is cheaper for a season;
      - for the wood: the reverse;
      - or a bargain, by the talker's Social.
    - Reuses: the moot and peace-talks world events, envoys. **Medium.**

97. **A toll on the road** (the League and the Wheel).
    - The League tolls the road past the colony, and the Wheel's carts won't pay.
    - The colony can let the carts cross its map (Wheel up, League down), turn them back (the reverse), or host both at the gate to set a fair toll.
    - Reuses: the toll world event, the Wheel's moving towns. **Medium.**

98. **The lamp and the bell in a plague year** (the Order and the Bell).
    - A plague town within reach asks, through the Order, for the colony's doctor, and through the Bell for two diggers.
    - Each colonist sent goes as a caravan, and the sickness may come home with them (the illness budget).
    - Sending both earns a lasting deed in both peoples' memories.
    - **Large.**

99. **Learning traded through the colony** (the Society and the Vault).
    - A society copyist and a vault technician meet at the colony's table, hosted for three days. The copyist copies the vault's records, and the colony's crafter teaches both.
    - The society gets its copy (with a book for the colony), and the vault gets its craft (supply that holds its age).
    - Both peoples' ages move by the teaching rule.
    - **Medium.**

## For every people

100. **(gift) Their art handed on.**
     - Once a game, a people at good terms offers the colonist who did most for them (their deeds) its own art.
     - With A RimWorld of Magic, that is its class tome, read by the mod's own rules: the Order the Priest's, the Herders the Nature tome, the Wardens the Ranger's, the Bell the Earth tome, the Banner the Gladiator's, the Guild the Enchanter's, the Lattice Technomancy, the Choir the Bard's, and so on.
     - Without it, a passion in their calling's skill.
     - **Medium** (a table and one worker).

---

## Recommendation: the first ten

**Build first:** 10, 5, 47, 83, 51, 1, 19, 17 (camp), 34 (camp), 95.

Why these ten:
- **Five are the gene event's own shape** for other peoples: 10, 5, 47, 83 and 51. Each changes one named colonist for good, in a different system each time (a bonded mount, a limb, a psylink, a mechlink, a tree and its dryads). That is exactly what the player asked for.
- **The thinnest peoples come first.** The league, the order, the herders, the pilgrims and the society have only two or three touchpoints of their own, and the herders have none at the gate. Each of them gets something in the ten.
- **Every age is covered:** tribal (herders, Mossborn), medieval (order, Wardens, pilgrims), industrial (league, society, Conclave), spacer (Lattice). Each item also says how it looks in the colony's own age.
- **The player's mods are used:** Giddy-Up 2, Alpha Animals, Royalty with Vanilla Psycasts Expanded or A RimWorld of Magic, Biotech and Alpha Mechs, Ideology with the Dryads, Megafauna, Dinosauria, ReGrowth and PokéWorld.
- **Two camps are in it, and they pull their weight.** The hostel (17) shows a people's way of life over two weeks, with the whole region passing through. The lantern post (34) and the Wardens-versus-Circuit item (95) share the legend-beast trigger and the hunt's lords, so they are cheapest built together.
- **They build shapes the later items reuse:**
  - the gene work's hold-and-work job (47, 83, then 79, 80, 87);
  - a visitor doing a fitting (5, then 22, 40, 92);
  - goods held in trust (1, then 20);
  - a reading that says exactly what the map holds (19, then 63).
- **Size:** all are medium except the hostel, which is large. Two releases' worth.

**Next round:** 14 (it builds the "season away" caravan used by 3, 67 and 98), 22, 26, 59, 12, 25.

---

### Critical Files for Implementation
- `/home/user/Rimworld-Helper/Source/ColonyCouncil/Peoples/PeoplesG.cs` (the gene event's worker, effect, and the hold-and-work job `JobDriver_PeoplesG` to reuse)
- `/home/user/Rimworld-Helper/Defs/IncidentDefs/Rimfolk_Events_PeoplesG.xml` (the event def shape: texts, options, effects, lines)
- `/home/user/Rimworld-Helper/Source/ColonyCouncil/Camps/CampKindDef.cs` (`calling`, `seasons`, `situationKinds`, `asks`, `leaveBehind` and the rest for the camps marked (camp)), with `/home/user/Rimworld-Helper/Defs/CampDefs/Rimfolk_CampKinds_PeoplesLore.xml` as the model of a people's own camp
- `/home/user/Rimworld-Helper/Source/ColonyCouncil/Peoples/PeopleGifts.cs` (psylinks, classes and genes given to a pawn)
- `/home/user/Rimworld-Helper/Source/ColonyCouncil/Peoples/PeoplesF2.cs` (the conscript's caravan of one, for 3, 14, 56, 67, 77, 98) and `/home/user/Rimworld-Helper/Source/ColonyCouncil/Dilemmas/DilemmaWorkers_Variety.cs` (`DilemmaEffect_Operation`, for 5, 22, 40, 92)
