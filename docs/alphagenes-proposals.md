# Alpha Genes and Rimfolk: fixes and proposals

A list to pick from, as the rules ask: nothing here is built. With the player's mods Alpha Genes loads 398 genes,
read from its 1.7 Defs, its 1.6 AlphaGenes.dll (the same classes) and the Vanilla Expanded Framework source. The
covering for the repulsive and angelic auras shipped in 0.52.1 (`Integrations/CoveredLooks.cs`) and is not repeated.
Everything below matches by defName or type name and does nothing without the mod (`GetNamedSilentFail`,
`AccessTools.TypeByName`, `MayRequire="sarg.alphagenes"`, the gene lists of `TagGroupDef` and `AilmentRisk`).
Effort: S (data or a check), M (a worker or a planning row).

## What the genes hand out

| Theme | Genes | What Rimfolk meets |
|---|---|---|
| Appearance and auras | 130 | auras within 6 cells (disgust, awe, calm); opinions (unsettling -60, eldritch -500, stinky -15); gas every second; a forced weather each day; heat or cold radiating |
| Body changes | 106 | natural armour (0.2 to 0.5); wings and gas bags that glide; deafness; painless; regeneration; tumours |
| Abilities | 48 | 34 abilities (breaths, jumps, pocket planes, mech hacking) |
| Diet and hunger | 33 | hunger near nil (foodless, light sustenance), steel for food (mineral absorber), frail stomach, table resistance, jelly |
| Temperature and sun | 11 | speed by heat or cold; UV sensitivity: minor (-3 mood), vicious (unconscious in sun), lethal (burns to death) |
| Reproduction | 10 | egg laying, fertility by light or place, gestation speed |
| Social | 9 | inner peace (the game's insults nullified), rowdy (fights are fun), raid presence x0 to x4 |
| Work, mental, archite | 28 | mining yield, starting animals; thalassophobia, fire obsession, volatile mood; no mood need, heat and cold immunity, fission |
| Other | 23 | Anomaly's shambler variants; night owl (the trait Rimfolk reads already) |

Already right: `ColonyNeeds` reads each person's hunger with its hediffs; raid points (`DefaultThreatPointsNow`)
carry the raid presence genes; unfeeling colonists fail `People.HasInnerLife`; the temperature wishes read the comfort
stats. Unsettling appearance and eldritch visage are opinions of someone known, not seen, so the covering stays off them.

## (a) Fixes: where Rimfolk is wrong

**F1. Gatherings and errands that walk people into harm (S to M).** AG_Sensitivity_Lethal (lethal UV sensitivity:
15 permanent burn every 500 ticks under open sky by day, dead within minutes), AG_Sensitivity_Vicious
(vicious UV sensitivity: consciousness capped at 10% in sunlight), AG_Thalassophobia (thalassophobia: open water in
sight within 20 cells, or rain on a bare head, gives "afraid of water" -10 and a "panic-fleeing water" break about
every 2.4 hours near water), and the gas producers AG_ToxGasProducer, AG_RotStinkProducer, AG_SmokeProducer and Anomaly's
AG_DeadlifeProducer (a cloud every second; deadlife dust raises corpses). `PlayOuts.CanJoin` and the invitee pick in
`PlayOutTracker` never look at the spot, and `PlaySpots.Ok` reads only the organizer's allowed area. So "Everyone to
the water" (`Council_Dilemma_MidsummerSwim`, 12 to 18 h at a Water spot) takes a lethal-UV colonist into noon sun and
a thalassophobe into the river, then gives both "A whole afternoon in the water". Likewise the evening swim, the
blessing of the waters, "at the bank with the drovers" and the hail's harvest party in the rain; a tox-gas producer
fills an indoor feast or council meeting; a deadlife producer at a wake can raise the body. Fix:
one `PlaySpots.Safe(pawn, spot)` at the invitee pick and in `CanJoin`: no unroofed spot in sunlight for a hediff with
VEF's `HediffComp_HighlyFlammable` (sunlightBurns) or `HediffComp_StageBySunlight`; no Water spot, nor an outdoor one in
rain, for thalassophobia; no indoor spot or laid-out body for a `HediffComp_GasProducer`. A colony-wide thought goes
only to those who could go (an `exceptTags` on `DilemmaEffect`, tag groups "sunburns" and "waterfear"). The player
sees them stay home, and "everyone" means those who went.

**F2. Colonists who barely eat (S).** AG_Foodless (foodless), AG_LightSustenance (light sustenance) and AG_MetalEater
(mineral absorber) cut hunger to almost nothing but keep the food need; every lapis has the first and third.
Rimfolk's `nofood` tag needs the need gone, and only light sustenance is in the `sunfed` group. So
`AilmentTracker.Daily` counts days without fruit or vegetables, and after 20 a lapis gets scurvy (serious tier, 25% a
day, "left for many weeks it kills", cured only by fruit they never grow hungry for); lapis wish for finer food,
honey and seats, and take rationing like anyone; `WishWorker_SeatsForAll.Diners`, `GrowthMath.DiningRow` and the
"long walk to the table" nuisance count them; the Almanac's Alpha Genes article says xenotypes that never eat aren't
asked about meals. Fix: a "fasting" tag in `PeopleTags` when `FoodFallPerTickAssumingCategory(Fed)` is under a tenth of
a baseline adult's (any mod's hunger hediff counts), added to the wish blocks and the rationing reaction; the scurvy
clock counts their days as eaten well; diners, the dining row and the nuisance leave them out.

**F3. The armor wish ignores armour grown in the skin (S).** AG_ArmourMinor (bark skin, 0.2 sharp),
AG_ArmourMedium (ferrous skin, 0.3), AG_ArmourMajor (plasteel skin, 0.4), AG_FungalSkin (fungal skin, 0.5), stat
offsets on the pawn. `WishWorker_Armor.BestArmor` reads only worn apparel against 0.35, so a plasteel-skinned fighter
asks for "proper armor", one of the save's two most repeated wishes. Fix: the larger of the apparel and
`pawn.GetStatValue(ArmorRating_Sharp)`, natural armour from any source.

**F4. Rimfolk's copies of the game's moods ignore the genes that silence the originals (S).** Alpha Genes adds
AG_ScalyBody and AG_WaterStriding to SoakingWet (nereids), AG_BioluminescentEyes to EnvironmentDark (nereids; it also
sets `ignoreDarkness`, like Biotech's DarkVision on the forsaken), AG_TableResistance to AteWithoutTable, and
AG_InnerPeace to Slighted, Insulted, InsultedMood and HadAngeringFight (drakonori). Yet "no coat in the rain"
(`NuisanceCheck_NoCoat`) hits nereids, eye strain from dark hand work (`DarkWork`) hits nereids and the forsaken, the
long walk to the table hits the table-resistant, and eight of Rimfolk's own slights (`Council_Friction_Teased`,
`Council_Soc_Snubbed`, `Council_Soc2_InsultedAtDinner`, `Council_EncP_RudeHost` and others) reach a drakonori. Fix: those
checks ask `ThoughtUtility.ThoughtNullified(p, <the game's thought>)`, `DarkWork` skips `ignoreDarkness` genes, and the
slights get `<nullifyingGenes><li MayRequire="sarg.alphagenes">AG_InnerPeace</li>`. Also AG_Rowdy (rowdy): a fight
between two carriers is play (Alpha Genes gives both recreation), so `Deeds.Notify_SocialFight` skips the "started a
fight" gossip when both carry it.

**F5. Fixed temperature lines ignore genes (S).** AG_ColdImmunity (cold immunity, comfortable to -273 °C),
AG_HeatImmunity (heat immunity, +232 °C; efreet), AG_FoxTail (-10 °C), and Biotech's MinTemp/MaxTemp genes in lapis,
efreet and animusen. Chilblains and chapped hands (-10 to 6 °C), frostnip and nosebleed (below -10), heat rash and fume
headache (32 °C), the chilly room (0 to 12 °C), stifling room (over 29) and draughty bed (below 5 outside) use fixed
numbers: a cold-immune colonist gets frostnip, an efreet heat rash, and Flavor's tags give them "[cold] Move in closer,
all of you, it's freezing" and "[hot] Too hot to sleep and too hot to work". `Council_Moment_WarmBlood` ("runs hotter
than any hearth") goes to drakonori, whose only heat gene is heat torpor. Fix: shift each line by the colonist's
active genes' ComfyTemperatureMin/Max `statOffsets` (any mod's genes count; past 100° means never), and the same shift
for the cold and hot tags; the moment's initiator becomes a "warmbody" group (xeno:AG_Efreet, gene AG_HeatRadiating).

**F6. Deaf colonists hear the music (S).** AG_Deaf (deaf: hearing set to 0). Nothing in Rimfolk reads Hearing: the
music wish (`WishWorker_Music`) and six heard-sound memories (`Council_Evt_Music`, `Council_EncC_Birdsong`,
`Council_EncS_HeardSong` and others) reach a deaf colonist, and the work-song decree tells them "I can't hear myself
think". Fix: a small `heard` extension on those thoughts, skipped wherever Rimfolk gives memories for anyone not
`CapableOf(Hearing)`; the music wish skips them. Ears lost in a raid count too.

**F7. Weather makers and the forecast (M).** AG_RainMaker (rainmaker), AG_SnowMaker, AG_Fogger and seven more,
four from Alpha Biomes: custom xenotypes only, which Alpha Genes' two factions also field. VEF's
`HediffComp_ChangeWeather` sets the map's weather once a day. Rimfolk's forecast (`WeatherDynamics.Forecast`, held by
`Patch_WeatherForecastHeld`) foretells the game's next weather, the gene replaces it, and "Hector expects clear skies"
is followed by rain; the year's records credit the land. Fix: when anyone on the map carries that comp, the forecast
names its weather and maker ("Mira's rain will hold"), and those days stay out of the records.

## (b) Proposals, best first

**P1. Steel for the mineral absorbers (M).** AG_MetalEater (mineral absorber), with AG_MineralEfficencyPoor and
AG_MineralEfficencyGood (poor and great mineral absorption); every lapis. The reserve loses 0.25 a day and a steel bar
restores 0.05: about 5 steel a day each (15 with poor absorption, 2.5 with great), before Alpha Genes' metabolism
factor; they eat the cheapest metal, steel. When it runs out, mineral craving multiplies hunger by 5 to 20 and caps
consciousness at 90 to 70%, so a steel shortage becomes a food drain. The player sees a planning row "Steel: Mira eats
about 5 a day; 31 in stock lasts 6 days", building suggestions that keep that reserve, and a mining or smelting step
when short, each with "Not this". How: `AlphaGenes.Gene_Resource_Metal` by type name (`resourceLossPerDay`) in
`ColonyNeeds`, a reserve in `AheadMath`. The same pass counts AG_InsectJellyProduction (jelly sacs; hivelings make 12
insect jelly about once a day) as food coming in.

**P2. Light for the sun-fed (S to M).** AG_LightSustenance. VEF's light condition (`VEF_LightSustenance`) climbs about
0.6 a day below 40% light and falls as fast above it, so a dark bedroom and night work tip it. The light wish
(`WishWorker_Light`) comes to a sun-fed colonist without the dark mood, with "Mira lives on light, and her room is dark
all night"; the planning helper suggests a lamp at their bed or bench while the condition rises. How: the `sunfed`
tag in the worker, the hediff's severity by defName, read hourly.

**P3. What to do about Alpha Genes' moods (S).** Hints for thoughts that have none: AG_Thalassophobia (work, bed and
paths 20 cells from open water, roofed walks in rain; the panic ends a second after the water is out of sight),
AG_CorpseFeeding (a helixan's gas rots bodies within 5 cells and the smell reaches 10: bury or burn the dead, keep
them off grave digging), AG_SunlightSensitivity_Minor (roofed work by day), AG_Mood_Volatile (it changes each
day), and two keep hints: AG_Awe (it needs the face seen, so Rimfolk's covering hides it too) and AG_PsychicDrone
(near the master psychic, AG_PsychicAbility_Masterful). How: entries with `MayRequire` in the existing
`Rimfolk_MoodHints_AlphaGenes.xml`.

**P4. A stinky colonist fresh from the bath (S).** AG_Stinky (stinky: -15 opinion from everyone with a nose who knows
them). Right after a wash, Dubs Bad Hygiene's hygiene at 80% or more, nobody minds; as it falls the smell comes back.
Real cause, real remedy, seen on the map: they go to the tub or shower, and the Social tab's pair note says why. How:
a postfix on `AlphaGenes.ThoughtWorker_Stinky.CurrentSocialStateInternal` and the "Hygiene" need, both by name.

**P5. Ailment risks from the body (S, XML only).** `AilmentRisk` lines: AG_FrailStomach x1.5 and hediff
AG_VFEI_VenomGland ("safely consume even the most dangerous foods") x0 on worms; AG_PackMule x0.5 and AG_FrailBack x2
on strained back; hediffs AG_ArmourMajor and AG_FungalSkin x0.3 on insect bites, leeches and splinters; hediffs
AG_Hover and AG_HoverExploder (wings and gas bags glide, "ignore terrain movement costs") x0 on athlete's foot, leeches,
swamp fever and the sprained ankle; AG_UVSensitivity_Minor x2 on snow blindness and sunburn; AG_LungRotImmunity and
AG_LungRotStrength x0.3 on rimrot and spore cough; AG_ToxicEnvironmentResistance_Frail x2 on pollution cough.

**P6. Births the planner can see coming (M).** AG_EggLaying (egg laying; drakonori: a fertilized human egg every 9
days while expecting, hatching in 15 and "kept between 0 and 50 °C"), AG_AsexualFission (asexual fission; fleetkind: a
newborn every 25 days). A laid egg counts as a pregnancy in `FamilyLife` and the grandchild goal; the planning helper
counts the hatchling and the next budding as joining (cribs, beds, seats), and when an egg lies where the night will
go below 0 °C it suggests carrying it to a warm room, a haul the player clicks. How: `AG_HumanEgg` by defName, VEF's
`HediffComp_AsexualReproduction` counter by field name.

**Smaller, if wanted.** AG_HeatRadiating and AG_ColdRadiating (up to 80 °C, down to -20 °C): the room nuisances and the
comfort wish name the colonist as the cause and offer a roommate another bed, not a heater (M). The defense page names
colonists whose raid presence genes weigh x0 to x4 (S). A smoke producer counts as half a shooter in the defense
survey, as AG_SmokeProducer's own description says its smoke spoils ranged fire both ways (S).
