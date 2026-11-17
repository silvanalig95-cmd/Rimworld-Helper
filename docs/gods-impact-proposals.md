# Every god's doing leaves something real (proposal, 17 November 2026; all 55 built, not released yet)

**Built** (17 November 2026, on the player's "yes, go ahead with all!"): every proposal below, in
`Source/ColonyCouncil/Gods/Touch/GodImpactMath.cs` (the table: each occurrence's words key, its effect and size; the ones
left as they are; the rest untouched) and `GodImpact.cs` (the doing); DESIGN.md "Every god's doing leaves something real"
has each as built. Where the proposal's tag and its words differ, the words were built: 15 holds the night's threats
until dawn (not the touch's season-long calm, "Ward"), 37 keeps the ground warm (not the touch's pool, "Spring"), 45 makes
one of the herd with young (not new animals walking in, "Herd"), 50 keeps that one spot dark for the night (not the dark
god's two days, "Dim"). Two the signs already did: 25 (the drink is lost; now the last one too) and 27 (the coin is real).
The rich soil stays, as the touch's own does, rather than for a year.

The player, after the ring of mushrooms round Kasumi's body (0.264.1): asked whether the body changed or the sign had any
gameplay impact, and on hearing that it had little, "these proposals sound great! not every god interaction with the
world necessarily needs to have a strong impact, but it should also not be completely trivial".

## Where it stands

Of the 189 kinds of god occurrence the colony's book can record, 91 bring an effect of their own (a vein, a gift, rain,
a healed thrumbo, and so on). The other 98 bring only this, from the gods' touch (`Gods/Touch/`, 0.256.x):

- a small mood for whoever saw it (uneasy for most, glad for someone devoted to that god, nothing for a scoffer);
- by chance, one of that god's effects for those who saw it: about one time in three for a sign (always a small one: a
  day's blessing such as fighting off sickness faster, or a little learning), more often for a kindness or a sight.

So about two signs in three leave nothing but a mood, and nothing at all when nobody was near.

## The rule proposed

Each occurrence below gets one effect of its own, always, fitted to what was seen and to the god, and shown on the map
or on the one who saw it. Most are small (a day, a few cells, one thing); a few are a little more. The random effect of
the god stays for the rest as it is. Nothing names a number or a god's standing (the gods are unseen); harm only where
it says so, at the game's own rate. Where an effect of the gods' touch already exists (`TouchEffect`: RichSoil, Rain,
Trader, CatComes, RatsGone, Fish, Herd, Inspire, Charge, Dim, Sleepless, Ward, Refuel, FiresOut, Ripen, Find, Bloom,
Game, GameLeaves, Spring), it is used.

## Proposals

**Morel (rot, the dead, soil)**
1. (Built.) The ring round an unburied body: the body rots away to bones within a day or two (the game's own rot, sped up) and
   the ground inside the ring is rich soil for a year.
2. (Built.) White threads round rot in the stores: by morning the rotten stack is gone and the floor under it is clean.
3. (Built.) The ring where the mourner stood all night: those who mourn the newly dead feel their grief ease sooner.
4. (Built.) A ring of mushrooms come up overnight (the shared sign): a real ring of glowstools comes up there, and the soil
   inside is rich soil for a year.
5. (Built.) Fine white threads across a floor (the shared sign): the food stored in that room keeps a little less long for a few
   days.

**Hush (frost, silence, the dead)**
6. (Built.) Frost on a grave at dusk: those closest to that dead person sleep well that night and wake rested.
7. (Built.) A ring where the frost on a grave melted: flowers come up round the grave (Bloom).
8. (Built.) Something walked through in the night, leaving a line of frost: the plants along the line are touched by frost (the
   game's cold damage on tender crops, a few cells; a small harm to the fields).
9. (Built.) A word written in the frost: something small lies where the word was written (Find).
10. (Built.) Frost by a wall in lines like writing (the shared sign): the room behind that wall stays cold for a day.

**Helios (the sun)**
11. (Built.) Facing the sunrise after the longest night: those who stood there are fully rested and glad.
12. (Built.) The noon light standing white after the eclipse, and the sun standing still (the shared sign): the crops in the open
    grow a day ahead (Ripen) and solar panels give their most all that day.
13. (Built.) The air shimmering over the open ground at noon: the open ground is a few degrees warmer for the afternoon.

**Selene (the moon)**
14. (Built.) The full moon lying in still water: the one who stood by it is inspired (Inspire).
15. (Built.) The door kept on a night with no moon: nothing comes to the colony that night (Ward).
16. (Built.) The moon huge and close all night (the shared sign): everyone sleeps deeper that night (more rest from sleep).
17. (Built.) A star moving the wrong way (the shared sign): the one who watched it is inspired to research (Inspire).

**Tamiu (cats, the threshold of death)**
18. (Built.) A cat comes and stays beside someone near death: their pain is less while it stays.
19. (Built.) The cat stayed to the end: those who mourn that person grieve less long.
20. (Built.) The cat walked away once they came through: the one who lived heals faster for a few days.
21. (Built.) Every cat facing the graves, and a cat sitting on a grave (the shared signs): the rats leave the land (RatsGone).
22. (Built.) Two gold eyes low by the graves (the shared sign): a stray cat comes to stay (CatComes).

**Revel (drink, joy, the night)**
23. (Built.) Someone deep in a binge laughing with an empty seat: the binge ends sooner, and their joy is full.
24. (Built.) The horned man walking in with a cup: a drink of his kind is left where he stood.
25. (Built.) The drink in the stores knocked over (the shared sign): one of them really is spilled and lost.
26. (Built.) A sweet smoke drifting through the colony (the shared sign): those who smell it are cheered for the day.

**Ducat (coin, trade)**
27. (Built.) A gold coin at their feet (the shared sign): a real gold coin lies there.
28. (Built.) A glint of gold out of the corner of the eye (the shared sign): a trader comes sooner (Trader).
29. (Built.) A golden beetle walking across a table (the shared sign): a little silver lies under that table.

**Pip (machines, lights)**
30. (Built.) Lights flickering in a pattern (the shared sign): the batteries on that line fill a little (Charge).
31. (Built.) A light glowing with no power to it (the shared sign): it keeps glowing all night without power.
32. (Built.) A voice in the comms console's static (the shared sign): a trade ship passes within reach soon (Trader).
33. (Built.) A machine switched off that runs on a whole hour: it does an hour's work for free (no fuel, no power).
34. (Built.) A light going out again and again with no draft (the shared sign): the fires and torches in that room go out for the
    night (FiresOut), and have to be lit again.

**Smoulder (fire, the deep rock)**
35. (Built.) The rock rumbling overhead in the deep workings (the shared sign): a few chunks of stone come down there (no harm).
36. (Built.) The miners' shared dream of something stretching under the mountain: those miners dig faster for a day.
37. (Built.) Steam coming up out of the ground, warm as breath (the shared sign): the ground round it stays warm for a few days,
    snow melts and nearby plants grow (Spring).

**Cirrus (wind, sky, weather)**
38. (Built.) The wind dying and nothing moving (the shared sign): the wind turbines really stop, and fire doesn't spread, for
    the hours it lasts.
39. (Built.) Thunder over the far hills under a clear sky: rain comes within the day (Rain).
40. (Built.) The smell of wet earth with no rain (the shared sign): rain comes within the day (Rain).
41. (Built.) A shadow with wings crossing the land, a shadow too large for any cloud (the shared signs): the wild game leaves
    the land for a day (GameLeaves), and comes back after.

**Hollin, Bellwether and the beasts**
42. (Built.) The grass bowed flat in a long line, a hoofprint the size of a table (the shared signs): game comes onto the land
    (Game).
43. (Built.) Every leaf and seed drifting toward one place (the shared sign): wild plants come up there (Bloom).
44. (Built.) The anima tree humming (the shared sign): its grass spreads a little and meditating at it is better for a day.
45. (Built.) A goat bleating in an empty room, the beast lowering its head toward its young at dusk, the dream of the last white
    beast: one of the colony's herd animals is with young (Herd).
46. (Built.) The animals starting and standing all looking one way, the dogs going quiet facing the gate, every thrumbo or dryad
    standing still facing one way, every bird taking off at once (the shared signs): the colony's animals are calm and
    easy to handle for a day (taming and training go better).
47. (Built.) Taking food to the god's beast and it ate: the colony's animals are hardier for a few days (Hardy).

**The eerie signs of any god**
48. (Built.) A knock at the door with nobody there, footprints to the door and none away, a tall white figure at the edge of
    the land (the shared signs): the one who saw it sleeps badly that night (Sleepless).
49. (Built.) A sweet voice speaking when nobody was near (the shared sign): the one who heard it is inspired (Inspire).
50. (Built.) A dark the light didn't reach (the shared sign): that spot stays dark for the night (Dim).
51. (Built.) A deep croaking from the wet ground (the shared sign): more fish in the water (Fish).
52. (Built.) Pale sand across a grave (the shared sign): the one closest to that dead person dreams of them kindly.
53. (Built.) Two of the colony waking from the same dream (the shared sign): they think better of each other.
54. (Built.) Everything going silent at once (the shared sign): everyone's next hour of work goes better.
55. (Built.) A work named for the god by the one who made it: the piece is admired more (its beauty a little higher).

## Left as they are

These already lead to something real, so they keep only their moods: the white threads on the freezer's floor (the
freezer breaks down the next day); roots up through the floor (the tiles are broken); the floor of the deep working
growing hot and the warm voice under the mountain (they warn of Smoulder's burn); the one who touched the white beast's
horn foretelling rain or snow (it comes); the white beast's road, turned back or opened (the colony's own choice); the
half-written message and the console waking at night (they come before the falling star); the bowl set out at dusk (the
colony's own offering); the dryad's word (it points at something already); the true dream under the full moon (it comes
true); roots drawing themselves up into a shape (a god's coming); the wrapped dead turning to dust (the graves are
cleared); the traveller who knew what nobody told it (its judgement brings the effect); the body found where a cat sat
crying (the body is found); someone walking through a ring and hearing her (already a mood of its own).

## How it would be built

One table of the 55 (each occurrence's words key, its effect and its size), read by the gods' touch where it now picks a
random effect; most effects reuse the touch's own, the rest are new and small (a ring of glowstools and rich soil, the
body's rot sped up, a coin, a spilled drink, a cold room, a light that stays lit, a machine running on, stone chunks, the
turbines stopping, a gentle dream). The moods stay as they are. One release, or two (the shared signs, then each god's
own).
