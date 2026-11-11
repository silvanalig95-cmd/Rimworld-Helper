# Gods in Rimfolk: a first brainstorm (11 November 2026)

The player: "the concept of gods within RimWorld ... like a pantheon of gods ... Maybe some nature or animal god could
also be actually on the map, or like a god that is acting like a guest and then reveal himself and have some humanoid
form ... that they actually are and interact and are into the lore and get referred to, and pawns react to gods ... I'm
sure there's many ways to create interactions with all the different systems that we have created so far."

Nothing here is built. It's numbered so the player can pick, cut or redirect.

## A. The big knob: how real are the gods?

1. **A setting, "How real are the gods"**, with three steps:
   - **Believed:** nothing supernatural. People pray, swear, hold rites and read omens into coincidences. The weather is just the weather, but the colony is sure it was the Storm-father.
   - **Felt (the default):** the gods act, but never so plainly that nobody could doubt it. A stranger who might have been a god. Rain that came the night after the offering. A white stag no hunter can find twice.
   - **Walking:** the gods manifest. Avatars reveal themselves, miracles are named on the card, and a god can be spoken with, bargained with and angered.

   Every feature below says what it looks like at each step. The doubt itself is a feature: some colonists believe, some scoff, and they argue at supper.

## B. Where the pantheon comes from

2. **The ideoligions' own deities, made real.** Ideology already names each faith's gods (Rimfolk's letters use them). Each gets a domain from its faith's memes and precepts, for example:
   - nature primacy: the Wild;
   - raider: a war god;
   - transhumanist: the Machine;
   - Anomaly's void memes: the Hunger.

   Every faction's gods are then the gods its people swear by, pray to and fight for.
3. **Rimfolk's peoples each get a patron, by their lore:**
   - the Stormglass Conclave: the Storm-father;
   - the Mossborn: the Green, mother of the old wood;
   - the Quiet Bell: the Ferryman, who walks the dead;
   - the Lantern Wardens: the Hunter;
   - the hospitallers: the Healer;
   - the Kiln Guild: the Smith;
   - the road league: the Way;
   - the Hollow Fleet: the Silent Captain, an archotech.
4. **Gods by the age, as RimWorld's own lore has them:**
   - tribal: spirits of animals, rivers, mountains and the dead;
   - medieval: old gods and saints;
   - industrial: half-forgotten gods, and new ones of the factory and the machine;
   - spacer: the archotechs, which RimWorld itself calls god-like minds. A spacer people's "god" is a real archotech that answers in its own strange way.
5. **Domains** that touch what the game already simulates: harvest and rain, storm, hunt and beasts, sickness and healing, war, craft, trade and roads, the dead, night and the moon, fire and the hearth, the void.
6. **Favour per god:** each god has a standing with the colony, raised by offerings, rites, kept vows and good deeds in its domain. It falls with neglect, sacrilege and harm to its sacred things. It's shown on a pantheon page in the Peoples window, but never as a number in a letter.

## C. Gods on the map, without new art

7. **The stranger at the gate** (theoxenia, the oldest god story there is). A lone traveller asks for shelter and comes as a Hospitality guest. Small oddities hint at what they are:
   - animals follow them;
   - they never seem to eat the meal you serve, yet they thank the cook by name;
   - the rain stops while they're on the map;
   - their bubbles say things a traveller couldn't know.

   When they leave, or if they're harmed, they show who they were. The colony is blessed or cursed by how the stranger was treated: the bed they got, the food, who was kind. The kindest colonist gets a lasting gift. Built on the guest system, `Speech.Say`, the event card and play-out.
8. **The sacred animal.** A god walks as a beast: a white stag, a great wolf, a golden thrumbo, a raven that follows one colonist for days. These are existing animals, tinted and glowing a little, untameable, and never hostile unless harmed. Hunting one is sacrilege, with the game's own revenge and the god's wrath. Leaving it be, or feeding it, brings favour. Built on the legend-beast trigger from round 1 of the peoples' events.
9. **Weather as a voice.** The storm god's thunder on cue, rain after the harvest rite, an eclipse (the game's own) for a dark god's anger, the aurora on a god's feast night. These are the game's own weather and conditions, chosen by favour at the rate the game already allows. A colonist always says so in a bubble.
10. **Dreams and visions.** A god speaks in Rimfolk's dreams: a warning before a raid, a demand, a name. A seer or priest gets clearer dreams.
11. **Shrines.** The game's altars, sculptures and Ideology's shrines become a god's shrine when a colonist dedicates them. Offerings are left there (play-out: they walk to it and set the thing down) and taken by the god, or by a hungry animal in "Believed".
12. **Icons, not portraits.** A small symbol per god (sun, storm, antler, moon, skull, wheel, cog), drawn the way the mailbag and thermometer were. The god's card uses the avatar's own portrait when there is one.

## D. Gods in the systems Rimfolk already has

13. **Prayer.** Colonists pray in hard moments: when sick, after a death, before a raid, during a drought. It's played out at a shrine or where they stand, with a one-sentence bubble to the right god. It gives a small comfort through the heart, and sometimes an answer at the step's strength.
14. **Festivals in the calendar:**
    - the harvest god's thanksgiving at the last harvest;
    - the winter god's longest night;
    - the dead's day for the Ferryman.

    Built on Rimfolk's feasts, with the god's own dishes, rites and the visitors who come.
15. **Vows.** A colonist vows something to a god as a wish or life goal: no meat until the sick child recovers (the new vegetarian meals have a use), a pilgrimage, a temple built, a beast spared. Keeping it brings favour and pride; breaking it brings shame, and sometimes wrath.
16. **The council and a priest.** A priest office (by Social and faith), council decisions such as which god the colony honours this year or whether to build the temple, and dilemmas with divine stakes: the god asks for the best horse, or the rain stays away.
17. **Letters and diplomacy.**
    - Letters: neighbours swear by their gods, tell of miracles and curses in their letters (the shapes already have sermons), invite the colony to their god's festival, and are offended when the colony mocks their god.
    - Shared faith: peoples who share a god get on better, and a holy war can draw the colony in.
    - Envoys: a temple's envoy comes asking for a relic.
18. **Peoples and camps.** Pilgrims camp at a sacred site on your map, such as a spring, an old tree or a crash site that one people calls holy. A hermit seer lives at the map's edge. A war god's champion leads a raid, and beating them is a sign.
19. **Health and harm, at the game's rate and always shown:**
    - **Answered prayers:** a plague eased by the Healer, a fever broken at night.
    - **Wrath:** lightning on a blasphemer's roof (the game's own lightning), a blight after the harvest god is slighted, wolves that come for the one who killed the stag.

    These use Rimfolk's shared harm and illness budgets, so the gods never make the game crueller than it is.
20. **The heart.** Faith gives comfort, an unanswered prayer gives a crisis of faith, and a convert takes another god. An atheist scoffs at the miracle, and a believer is hurt by it. Mixed-faith couples bicker over whose god sent the rain.
21. **Relics and gifts.** A god's relic (Ideology's relics), and lasting gifts to one colonist in the shape of the peoples' gifts: a blessed hand, storm-sense, a tame wolf, a healer's touch. With RimWorld of Magic, a god can grant a class (the Priest, the Paladin) or a spell. With Vanilla Psycasts Expanded, a path that fits the domain.
22. **The dark ones.** With Anomaly, the void as a god that answers anyone desperate enough. Its bargains are always real and always cost something, and it's tied to the Choir of the Spire.

## E. Questions for the player

- Which step of "how real" do you want by default, and should the other two stay as options?
- One shared pantheon for the whole world, or each people with its own gods, some shared?
- Can a god be spoken with in "Walking", for example bargained with at the gate like a visitor?
- Should the colony choose a patron god, or should favour simply follow what it does?
- Which of 1 to 22 excite you most? The stranger at the gate (7) and the sacred animal (8) would make a good first round: they show the gods on the map the most, and reuse the guest and legend-beast work.

## F. Weight: a god's presence is never just another event (the player, the same day)

The player: "they should then always carry some kind of weight and ominosity with them ... like a godly presence ...
imposing sound effects or really kind of epic ones when a god appears or interacts or does something ... I don't
necessarily want to have some, you know, cheap generated soundtrack. So I don't know if you can work with existing game
assets, or if you have some actually solid references or own capabilities in that regard."

23. **The build-up.** Days of omens before a god comes: birds leaving, a strange calm, the same dream in two colonists,
    a candle that won't stay lit. Colonists notice them aloud.
24. **The moment.**
    - The music stops, a beat of silence, then a sting and a chosen track.
    - The sky tints in the god's colour: a short presence condition, like the game's eclipse.
    - The wind dies. Everyone on the map stops and turns toward it, the devout kneel, and animals flee or lie down.
    - The game drops to normal speed, and the camera goes there.
    - Rare: a few times in a long game, so it stays an occasion.
25. **Its own card.** Not "good news" or "bad news" but a presence: dark paper with gold, the god's symbol, its words set
    apart from the narrator's.
26. **What remains.** A mark on the land: a ring of scorched grass, a spring, a tree that wasn't there. Its name goes in
    the colony's history, and it's spoken of at suppers and in letters for years.
27. **Sound and music.** From the game's own assets, which are professional:
    - Anomaly's dark tracks (13 in the player's game, among them "Abandoned by the Light", "Death Pall Rising" and "Blood
      Rain Falling"), Odyssey's tracks (8) and Ideology's solemn archonexus song;
    - Royalty's bestowing sounds (a warm-up hum and a finish), the game's start sting, thunder, and the psychic pulses;
    - Alpha Memes' ritual chants (Kemetic, Mesoamerican, Neolithic, the maddening chant), all in the player's mods.

    Each god gets a mix: for example the storm god is silence, then thunder and a pulse, then a track, while the void is
    an Anomaly track under an agony pulse. Code can start a chosen track and play sounds at a spot.
28. **Beyond the game's assets, with the player's ear as the judge.**
    - Freely licensed recordings (CC0 sound effects, CC-BY music), found and proposed by name and source, bundled with
      credits after the player has listened to them.
    - A folder the player fills themselves (`Sounds/Rimfolk/Gods/<god>/`), whose tracks the mod plays at that god's
      moments.

    No generated music.
