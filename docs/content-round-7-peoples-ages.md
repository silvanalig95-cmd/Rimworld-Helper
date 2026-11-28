# Content round 7: the peoples' older events in their own age (28 November 2026)

The review of the peoples' events (`docs/review-peoples.md`, released 0.324.0) found 96 older events of the neighbouring
peoples, rounds 1 to 3 and releases C to G, told in the colony's age and not in the visiting people's own. Rounds 4 to 10
take each people's own age and ways. Asked whether a content round should give each of them their own people's forms, the
player chose **"Yes, alongside"**. The standing rule of 27 November also holds: no new mechanics.

Built in `Source/ColonyCouncil/Peoples/PplAges.cs` and `PplAgesMath.cs`, the peoples' event defs, and the workers named
below. Kept by `Tools/Tests/PeoplesAgesTests.cs`; measured by `python3 Tools/peoples_impact_audit.py` (`--print ages`).

## How it is done

The same mechanism rounds 4 to 10 use, and the one the review used for the Signal Line's news for sale; nothing new:

- **Texts tagged by age.** Each worker adds the people's age to its tags (`PeopleEvents.AgeTags`: "age0" tribal to "age3"
  spacer, from `Peoples.AgeOf`, so a people that rose or fell back over the years is told in its age now). The texts that
  describe the visitors, what they carry and how they come are tagged `[age0]` to `[age3]` (or `[age0|age1]`,
  `[age2|age3]`). Most of the older texts that described the visitors were tagged by the colony's age (`[lowtech]`,
  `[!lowtech]`, `[tribal]` to `[spacer]`); those now take the people's age. A text tagged by age is preferred over an
  untagged one (Flavor.Pick), and the untagged ones were read and left only where they fit every age of the people.
- **The people's tech level for goods.** What they bring, ask or pay in goods follows `PplAgesMath`, the way NewsForSale's
  masts and the league's means (`PeopleEventsMath.Means`) already did.
- **The three callers told from data** (the order's hospitaller, the league's outrider, the society's night of the fire)
  run on `HappeningWorker_PplAgesSetting`, the setting's own worker with the people's age added to its tags.

## The rules on every item

- **The words.** A tribal people's envoy speaks and acts as a tribe (bark, sinew, spears, hide, on foot or in a dugout), a
  medieval people's as theirs (carts, mules, crossbows, wax seals, lanterns), an industrial people's as theirs (trucks left
  at the road, typed papers, oilskins, barometers), a spacer people's as theirs (slates, landers, kit that hums). No radios,
  guns or shuttles from a tribe; no spears and runners from a spacer enclave unless that is their way (the pilgrims walk in
  every age; the Wardens' lantern and the Bell's bell are theirs in every age).
- **What they bring, ask or pay**, where it is goods, is of their age (the table and "Goods and means" below), picked from
  the player's active mods softly where a mod is involved (the union's dynamite stays Settlers' by name; nothing new is
  required).
- **How they arrive**, where that shows: on foot, with a cart, a mule or a pony, with a truck or a lander left beyond the
  road (never a vehicle the map doesn't show). The enclave's drop pods are of the industrial age in the game itself
  (transport pods), so an enclave fallen back to it still comes down by pod.
- **The colony's own answer stays in the colony's own means.** The tags that describe the colony stay the colony's: a
  tribal colony is examined aloud and tells its stories to the copyist, the colony hears the enclave's lander over its own
  radio, comms or beacon, a conscript of a low-tech colony walks to the Republic's town, the colony's own word for a wreck
  or a casket stays its own.
- **The Empire keeps its own titles and honour.** None of the 96 is the Empire's; nothing of its was touched.
- **Effects and balance are kept**, unless an age makes an effect impossible; then the age's equal is used at the same
  worth (an old Line buys cloth instead of components, an old union's crate is steel, the Works' case of the medieval age is
  herbal medicine).
- **Checked and left** where the event reads the same in every age, with the reason (the table's last column).

## Counts

| Part | Before | After |
|---|---|---|
| The peoples' events | 170 | 170 |
| Told in their people's own age | 74 | 157 |
| Checked and left (the same in every age) | 0 | 13 |
| In the colony's age only | 96 | 0 |

Of the 96: 83 given their people's forms (texts by age for 82, the Lattice's turret by its age in code only), 13 checked
and left.

## Goods and means changed in code

All in the peoples' own files, each a rule of `PplAgesMath` (pure, tested):

| Event | Before | Now |
|---|---|---|
| The society's copyist (`HappeningWorker_ScholarCopyist.Book(Faction)`) | A schematic to any colony past the medieval age | A schematic only from a society of the industrial age or later (`Schematic`), else a textbook |
| The Wheel's scrap (`Refurbished(price, wheel)`) | A weapon of the colony's age | Of the colony's age and never past the Wheel's (`GoodsAge`) |
| The Line's wire-trader (`HappeningWorker_LineWire.Wanted`) | Components | Components from a Line of the industrial age or later; cloth for the towers' flags from an older one, 80 to 400, at the same rate (`Flags`) |
| The Line's mast (`PplAges.MastFor`, `RelayMast.Of`, `Raise`) | A steel relay mast | A flag tower (`Rimfolk_PplR8_LineTower`: the round-7 crew's tower to look at, the mast's own working) from a Line of the old ages (`Tower`) |
| The union's crate (`HappeningWorker_UnionGoods.Crate(map, union)`) | Steel, components, shells and Settlers' dynamite | The same from a union of the industrial age or later; steel alone from an older one, the components' worth added in steel (`Machined`, `SteelFor`) |
| The Works' case (`HappeningWorker_HelixCase.Case(map, works)`) | Medicine, glitterworld medicine and penoxycyline | Glitterworld medicine only from Works of the spacer age; herbal medicine of the same worth and no drugs from Works of the medieval age (`CaseTier`, `HerbalFor`) |
| The Lattice's turret | From any Lattice | Only from a Lattice of the industrial age or later (`Turret`) |
| The guild's mark (`GuildMark.Takers(map, guild)`) | Any weapon the master has a plan for | Only a weapon of the guild's own age or before (`CanWork`: a tribal guild reworks a spear or a bow, never a gun) |
| The enclave's salvage (`DilemmaEffect_EnclaveSalvage.Make(price, enclave)`) | Goods of the colony's age | Of the colony's age and never past the enclave's: no spacer components from an enclave fallen to the industrial age (`GoodsAge`) |

The new words: `Languages/English/Keyed/Rimfolk_PplR8_PeoplesAges.xml` (the cloth, the flag tower raised, its impact and
its wear); the mast's storm message and the raising job now say "mast", fitting the tower too.

## A bug found on the way

- **The Wheel's scrap-runners named a mended piece they didn't have.** Two of the scrap texts (the plain one and the
  grateful one) named `{THING}`, the piece the Wheel would trade, without asking whether there was one; with no piece
  (`nopiece`) the card could read "or trade a  that the Wheel has repaired". They are now told only with a piece
  (`[!nopiece]`).

## The 96 events

People's ages: the ages the people can reach (its own, the ages it may arise in, its floor and its limit:
`PeopleAgesMath.Reachable`).

| Event | People (ages) | What was given per age, or why it was left |
|---|---|---|
| `Rimfolk_Evt_HerdersBeast` | herders (0 to 2) | Barefoot (tribal), on a shaggy horse with a halter of red wool (medieval), in a dusty coat (industrial). |
| `Rimfolk_Evt_OrderLimb` | healers (0 to 2) | Carving knives, sinew and pine resin (tribal), a peg-maker's tools and a lantern (medieval), a doctor's bag and the limb packed in straw (industrial); the limb was already of the order's age. |
| `Rimfolk_Evt_StormSense` | seers (0 to 3) | A gourd of rainwater and a string of shells (tribal), a copper-tipped staff (medieval), an oilskin and a barometer (industrial, spacer). |
| `Rimfolk_Evt_LatticeMechlink` | mechanitors (1 to 3) | A patched robe and the old craft kept (medieval), a case of tools (industrial), a clean robe and a slim case of instruments (spacer). |
| `Rimfolk_Evt_MossSeed` | grovekeepers (0 to 3) | Out of the trees with the seed in the hands (tribal, medieval), the seed in damp moss in a tin box (industrial, spacer). |
| `Rimfolk_Evt_LeagueTrust` | traders (1 to 3) | The lame pack animal (medieval), the broken-down truck (industrial), the overloaded lander (spacer); means and crates were already the league's age, now tagged so. |
| `Rimfolk_Evt_ScholarsReading` | scholars (0 to 3) | A knotted measuring rope and bark (tribal), wax tablets and ink (medieval), a case of instruments and notebooks (industrial, spacer). |
| `Rimfolk_Evt_LanternPost` | hunters (0 to 3) | Boar-spears and lean hounds (tribal, medieval), a post with a lantern on a pole (industrial, spacer). |
| `Rimfolk_Evt_HuntOrTame` | hunters, tamers (0 to 3) | Spears and a lantern against a pouch and rope (tribal, medieval), hounds on leads against a crate of feed (industrial, spacer), by the Wardens' age. |
| `Rimfolk_Evt_Pilgrimage` | pilgrims (0 to 3) | Dusty cloaks (tribal, medieval), packs and walking poles (industrial, spacer); on foot in every age, their way. |
| `Rimfolk_Evt_LeagueGuard` | traders (1 to 3) | A staff and the league's mark on a cloak (medieval), the truck left at the road (industrial), a slate of manifests (spacer). |
| `Rimfolk_Evt_EnclaveBody` | brokers (2 to 3) | Worn spacer kit and the last of a fallen enclave's stock (industrial), a cold case that hums (spacer). |
| `Rimfolk_Evt_EnclaveInstalment` | brokers (2 to 3) | A typed letter, and no trader of theirs until it is paid (industrial); a letter on thin plastic, and no lander (spacer). |
| `Rimfolk_Evt_RiverCrossing` | fishers (0 to 2) | The river scratched on hide and sharpened logs for piles (tribal), poles, rope and iron pile shoes (medieval), a motor launch, a winch and steel cable (industrial). |
| `Rimfolk_Evt_KnotNight` | fishers (0 to 2) | A new net and a knotting pin (tribal, medieval), a lamp (industrial). |
| `Rimfolk_Evt_GuildMark` | artisans (0 to 3) | Flint blades, bone awls and hide glue (tribal), files, chisels and glue (medieval), a tool chest on a barrow (industrial, spacer); a gun taken apart blindfold only by a later guild. In code, only a weapon of the guild's own age. |
| `Rimfolk_Evt_HerdersDrought` | herders (0 to 2) | Barefoot (tribal), on a mule with empty water skins (medieval), a dust-caked coat and empty water barrels (industrial). |
| `Rimfolk_Evt_EnclavePad` | brokers (2 to 3) | A paper ledger in worn spacer kit (industrial), a slate and a marshal (spacer); stock by pod in both. |
| `Rimfolk_Evt_ArchiveTrust` | scholars (0 to 3) | A handcart under an oiled cloth (tribal, medieval), tea chests from a truck at the road (industrial, spacer); the tome's crate bound with iron straps in every age, not wire. |
| `Rimfolk_Evt_LameBeast` | tamers (0 to 3) | Herbs and splints (tribal, medieval), a vet's case and a crate on a barrow (industrial, spacer); the leg was already of the Circuit's age. |
| `Rimfolk_Evt_RedInk` | traders (1 to 3) | The book on the knee in a dusty coat (medieval), the ledger from a truck at the road (industrial: it said "driven up to the gate", where the factor walks in), the book on a slate (spacer). |
| `Rimfolk_Evt_SickbedLitters` | healers (0 to 2) | Poles and blankets (tribal, medieval), the stretcher cart (industrial); already the order's age, now tagged so. |
| `Rimfolk_Evt_SickRoom` | healers (0 to 2) | Herbs, boiled linen, a lamp on a staff (tribal, medieval), a mask and a case (industrial); already the order's age, now tagged so. |
| `Rimfolk_Evt_FieldSickhouse` | healers (0 to 2) | Poles, bedrolls and Aldith's lamp (tribal, medieval), a truck of canvas, folding beds and dressings (industrial). |
| `Rimfolk_Evt_DroveCrossing` | herders (0 to 2) | Barefoot (tribal), a pony and a crook (medieval), a wide hat and the drove's supply wagon (industrial). |
| `Rimfolk_Evt_ThiefTrail` | herders (0 to 2) | On foot with a spear, or riders with a tracking dog (tribal, medieval); on foot or riding in long coats, a dog on a lead (industrial). |
| `Rimfolk_Evt_HardBirth` | herders (0 to 2) | Bloody hands (tribal, medieval), a lamp and a coil of rope (industrial). |
| `Rimfolk_Evt_OldShrine` | pilgrims (0 to 3) | The nearly blind pilgrim and Old Brannoc (tribal, medieval), packs, walking poles and a lamp in cloth (industrial, spacer). |
| `Rimfolk_Evt_TwoFaiths` | pilgrims (0 to 3) | A frayed cloak and a staff of shells (tribal, medieval), a pack and a walking pole (industrial, spacer), for the clash and for the split. |
| `Rimfolk_Evt_FellowExamined` | scholars (0 to 3) | A staff notched for every question (tribal), a slate and chalk (medieval), books and examination papers (industrial), a data slate (spacer); a tribal colony is still examined aloud on its stories. |
| `Rimfolk_Evt_SummerSchool` | scholars (0 to 3) | Counting sticks, slates, schoolbooks, data slates: the school's tools were the colony's age, now the society's. |
| `Rimfolk_Evt_EnclaveContract` | brokers (2 to 3) | A typed contract signed in ink (industrial), a slate of numbered clauses (spacer). |
| `Rimfolk_Evt_ContractDue` | brokers (2 to 3) | A marshal in worn kit by drop pod (industrial), the lander (spacer); the runner on foot to a tribal colony stays. |
| `Rimfolk_Evt_FouledWater` | fishers (0 to 2) | A boat poled along the water, a fishing spear (tribal, medieval), waders and an oilskin (industrial). |
| `Rimfolk_Evt_KnotLanterns` | fishers (0 to 2) | Boats poled to the bank (tribal, medieval), a motor launch (industrial). |
| `Rimfolk_Evt_HiveBurned` | hunters (0 to 3) | Torches (tribal, medieval), firebombs (industrial), an incendiary launcher (spacer); the fire was already the Wardens' age, now tagged so. |
| `Rimfolk_Evt_OrderHealer` | healers (0 to 2) | Herbs and linen (tribal, medieval, beside an industrial colony's medicine too), a doctor's bag (industrial). |
| `Rimfolk_Evt_LeagueOutrider` | traders (1 to 3) | A string of pack beasts (medieval), a road coat with the league's badge (industrial), a slate of the caravan's stock (spacer); the caravan that comes is the game's own. |
| `Rimfolk_Evt_PilgrimShelter` | pilgrims (0 to 3) | Staffs and shell badges (tribal, medieval), packs, as the lamps come on (industrial, spacer). |
| `Rimfolk_Evt_ScholarCopyist` | scholars (0 to 3) | Bark and soot ink, a reed pen, or notebooks and a sound recorder for a tribal colony's stories; a writing case or a camera for a later colony's records. In code, a schematic only from a later society. |
| `Rimfolk_Evt_RiverCatch` | fishers (0 to 2) | A dugout and the fish on a cord (tribal), the basket on the back (medieval), fish in ice in a tin box (industrial). |
| `Rimfolk_Evt_EnclaveLander` | brokers (2 to 3) | An old lander patched many times (industrial), a lander passing over (spacer); how the colony hears it stays the colony's. |
| `Rimfolk_Evt_WardensHunt` | hunters (0 to 3) | A boar-spear (tribal), a crossbow (medieval), hounds on leads and the book in oilcloth (industrial, spacer). |
| `Rimfolk_Evt_PeltBuyer` | hunters (0 to 3) | On foot from the lodge (tribal, medieval), a tally book (industrial, spacer): the colony's tags became the Wardens'. |
| `Rimfolk_Evt_WardensLantern` | hunters (0 to 3) | The lantern carried from the lodge (tribal, medieval), an oil lantern in a tin case (industrial, spacer). |
| `Rimfolk_Evt_BellForTheDead` | sextons (0 to 3) | Grey habits and long spades (tribal, medieval), grey coats, spades and a pack animal (industrial, spacer). |
| `Rimfolk_Evt_BellFuneral` | sextons (0 to 3) | Walked from the grave-garden (tribal, medieval), funerals in the back rooms of towns (industrial, spacer). |
| `Rimfolk_Evt_BellVoidSigns` | sextons (0 to 3) | A muffled bell (tribal, medieval), the bell in grey cloth, not coming in (industrial, spacer). |
| `Rimfolk_Evt_TamerBout` | tamers (0 to 3) | A sash of knotted tokens (tribal, medieval), enamel tokens (industrial, spacer). |
| `Rimfolk_Evt_EggTrade` | tamers (0 to 3) | A basket of warm ashes wrapped in hide (tribal), straw (medieval), an insulated crate (industrial, spacer). |
| `Rimfolk_Evt_WheelMending` | salvagers (0 to 3) | Bone awls, sinew and hide glue (tribal), awls, files and glue (medieval), a grinder and a welding mask (industrial, spacer). |
| `Rimfolk_Evt_WheelScrap` | salvagers (0 to 3) | A handcart (tribal, medieval), a flatbed truck at the road (industrial, spacer). In code, the mended weapon never past the Wheel's age; the bug above fixed. |
| `Rimfolk_Evt_GlassReading` | seers (0 to 3) | A vial wrapped in wool (tribal, medieval), a barometer and a notebook of readings (industrial, spacer). |
| `Rimfolk_Evt_GlassRod` | seers (0 to 3) | Beaten copper and sinew (tribal), brass (medieval), a rod grounded through a coil (industrial, spacer). |
| `Rimfolk_Evt_GlassLesson` | seers (0 to 3) | A vial wrapped in wool (tribal, medieval), a padded case and a pocket barometer (industrial, spacer). |
| `Rimfolk_Evt_MossSaplings` | grovekeepers (0 to 3) | A moss-lined sack (tribal, medieval), a handcart of potted saplings (industrial, spacer). |
| `Rimfolk_Evt_MossGraveTree` | grovekeepers (0 to 3) | Walked from the grove (tribal, medieval), a sapling in a sack of moss by the grave (industrial, spacer). |
| `Rimfolk_Evt_MossTreeTend` | grovekeepers (0 to 3) | Hooks and resin (tribal, medieval), pruning shears and grafting wax (industrial, spacer). |
| `Rimfolk_Evt_BannerHire` | mercenaries (0 to 3) | Marched up in ash-grey (tribal, medieval), arms shouldered and the Roll (industrial, spacer). |
| `Rimfolk_Evt_BannerDeserter` | mercenaries (0 to 3) | A spear carried for the Banner (tribal, medieval), unarmed with the Banner's patch cut off (industrial, spacer). |
| `Rimfolk_Evt_BannerRoll` | mercenaries (0 to 3) | The book from the fort (tribal, medieval), the ledger of every name (industrial, spacer). |
| `Rimfolk_Evt_GuildCommission` | artisans (0 to 3) | Walked from the guildhall (tribal, medieval), the commission typed on a card (industrial, spacer). |
| `Rimfolk_Evt_GuildBid` | artisans (0 to 3) | Walked on a rumour (tribal, medieval), a truck left at the road (industrial, spacer). |
| `Rimfolk_Evt_LineMast` | linesmen (0 to 3) | A flag tower of timbers, flags and a lamp (tribal, medieval), the relay mast (industrial, spacer). In code, the old Line raises its flag tower. |
| `Rimfolk_Evt_LineWire` | linesmen (0 to 3) | The colony's cloth for the towers' flags (tribal, medieval), components for the masts (industrial, spacer), at the same rate. In code. |
| `Rimfolk_Evt_LineStorm` | linesmen (0 to 3) | A waxed cloak and a mallet (tribal, medieval), the oilskin (industrial, spacer); the flag tower climbed when the colony has one. |
| `Rimfolk_Evt_UnionDrill` | drillers (0 to 3) | Picks, a windlass and leather buckets sinking a shaft (tribal, medieval), the rig on a cart (industrial, spacer). |
| `Rimfolk_Evt_UnionGoods` | drillers (0 to 3) | A cart of the pits' steel (tribal, medieval), steel, components and blasting goods (industrial, spacer). In code. |
| `Rimfolk_Evt_RepublicCensus` | citizens (0 to 3) | A great book (tribal, medieval), printed forms on a clipboard (industrial, spacer). |
| `Rimfolk_Evt_RepublicPapers` | citizens (0 to 3) | Papers sealed in wax (tribal, medieval), printed papers and a rubber stamp (industrial, spacer). |
| `Rimfolk_Evt_RepublicConscript` | citizens (0 to 3) | A spear flying the Republic's pennant (tribal, medieval), the Guard's navy uniform (the old text); how the conscript travels stays the colony's. |
| `Rimfolk_Evt_FleetShuttle` | crew (2 to 3, the review's rule) | The ship's last shuttle, patched many times (industrial), the shuttle (spacer); the colony's radio stays the colony's. |
| `Rimfolk_Evt_FleetSalvage` | crew (0 to 3) | Saws and iron wedges, the slow way (tribal, medieval), cutting gear (industrial, spacer). |
| `Rimfolk_Evt_HelixSample` | editors (1 to 3) | A linen coat and a box of stoppered glass (medieval), the lab coat and vials (industrial, spacer). |
| `Rimfolk_Evt_HelixGeneWork` | editors (1 to 3) | One of the Works' last gene tools in oilcloth (medieval), the case of instruments (industrial, spacer). |
| `Rimfolk_Evt_HelixCase` | editors (1 to 3) | Herbal medicine from the Works' old gardens (medieval), medicine and drugs (industrial), with glitterworld medicine (spacer). In code. |
| `Rimfolk_Evt_LatticeTurret` | mechanitors (1 to 3) | In code only: a turret only from a Lattice of the industrial age or later; its words fit both. |
| `Rimfolk_Evt_VaultBarter` | vaultfolk (1 to 3) | The crate that holds less (medieval, industrial: the vault fallen, already by its age), the last spacer goods (spacer). |
| `Rimfolk_Evt_OrderNurse` | healers (0 to 2) | Willow bark, honey and linen (tribal, medieval), a doctor's bag and a thermometer (industrial). |
| `Rimfolk_Evt_LongWalk` | pilgrims (0 to 3) | Staffs and shells (tribal, medieval), packs and walking poles (industrial, spacer). |
| `Rimfolk_Evt_Disputation` | scholars (0 to 3) | A knotted cord (tribal), written notes and a candle stub (medieval), notes and a thermos (industrial, spacer); a tribal colony answers for its stories. |
| `Rimfolk_Evt_EnclaveSalvage` | brokers (2 to 3) | Goods kept since it fell back (industrial), salvage from above (spacer). In code, never past the enclave's age. |
| `Rimfolk_Evt_RiverNets` | fishers (0 to 2) | Nets of twisted fibre (tribal), hemp (medieval), nylon (industrial): the nets were the colony's age, now the river folk's. |

### Checked and left

| Event | People (ages) | Why it was left |
|---|---|---|
| `Rimfolk_Evt_TamerStray` | tamers (0 to 3) | A ranger leads the colony's lost animal home on a lead: the same in every age. |
| `Rimfolk_Evt_WheelWreck` | salvagers (0 to 3) | Runners strip a wrecked machine with what they carry; the [lowtech] text is the colony's own word for the machine. |
| `Rimfolk_Evt_GuildCollects` | artisans (0 to 3) | The journeyman comes back for the piece the colony made and pays by its quality. |
| `Rimfolk_Evt_UnionStrike` | drillers (0 to 3) | A strike at the pits needs food or a fair talker in any age. |
| `Rimfolk_Evt_FleetSleeper` | crew (0 to 3) | A sleeper from the Fleet's caskets, which every Fleet keeps from its founding, asks for shelter; the [lowtech] text is the colony's own words for the casket. |
| `Rimfolk_Evt_HelixBatch` | editors (1 to 3) | The Works' own vat-grown, sick with a gene fault, ask for the colony's doctor or medicine: their people in every age. |
| `Rimfolk_Evt_LatticeRepair` | mechanitors (1 to 3) | A mech-tender mends the colony's mechs with a roll of tools. |
| `Rimfolk_Evt_LatticeLostMech` | mechanitors (1 to 3) | One of the Lattice's mechs, its link lost, wanders onto the land. |
| `Rimfolk_Evt_LatticeParts` | mechanitors (1 to 3) | The Lattice buys every subcore and chip in every age, as it keeps minds. |
| `Rimfolk_Evt_ChoirListening` | listeners (0 to 3) | The listeners sit in the colony and listen. |
| `Rimfolk_Evt_ChoirArt` | listeners (0 to 3) | The object left for the colony's art is the Spire's, not of the Choir's age. |
| `Rimfolk_Evt_VaultCraft` | vaultfolk (1 to 3) | A technician asks to learn a craft the vault lost. |
| `Rimfolk_Evt_VaultChild` | vaultfolk (1 to 3) | A vault-born child sees the sky and asks to stay. |

## Mods that would help

From the player's catalogue first (`docs/player-mods/mods.tsv`, the active column):

- **Already in the list and doing the work.** The peoples dress and arm by their age from the mod pool
  (`PeoplesGearMath`), so the forms these texts describe are what their visitors wear: Vanilla Factions Expanded -
  Tribals ([3079786283](https://steamcommunity.com/sharedfiles/filedetails/?id=3079786283), 1.5 to 1.7), - Medieval 2
  ([3444347874](https://steamcommunity.com/sharedfiles/filedetails/?id=3444347874), 1.5 to 1.7), - Classical
  ([2787850474](https://steamcommunity.com/sharedfiles/filedetails/?id=2787850474), 1.5 to 1.7), - Settlers
  ([2052918119](https://steamcommunity.com/sharedfiles/filedetails/?id=2052918119), 1.5 to 1.7), Vanilla Weapons Expanded -
  Tribal ([2454918552](https://steamcommunity.com/sharedfiles/filedetails/?id=2454918552)) and - Makeshift
  ([2419690698](https://steamcommunity.com/sharedfiles/filedetails/?id=2419690698)), Vanilla Ideology Expanded - Hats and
  Rags ([2567387768](https://steamcommunity.com/sharedfiles/filedetails/?id=2567387768)). Giddy-Up 2 - Continued
  ([3674332861](https://steamcommunity.com/sharedfiles/filedetails/?id=3674332861), 1.4 to 1.7) gives the herders' and the
  league's riders their `[ride]` texts.
- **In the list, not on 1.7 in the copy catalogued.** Vehicle Framework
  ([3014915404](https://steamcommunity.com/sharedfiles/filedetails/?id=3014915404)) and Vanilla Vehicles Expanded
  ([3014906877](https://steamcommunity.com/sharedfiles/filedetails/?id=3014906877)) list 1.4 to 1.6. With them a later
  round could show the trucks these texts leave at the road, but that would be a new mechanic; worth checking on the
  Workshop whether they are updated for 1.7 before relying on them.
- **Subscribed but not active, for 1.6 and 1.7.** Medieval Total Rim: Pre-Industrial Prosthetics
  ([3806277474](https://steamcommunity.com/sharedfiles/filedetails/?id=3806277474)) would give the hospitallers of the
  tribal and medieval age more limbs to fit than the game's peg leg and wooden hand (the order's limb picks by recipe
  name; its names would be added softly). Medieval Total Rim: Animals
  ([3806262056](https://steamcommunity.com/sharedfiles/filedetails/?id=3806262056)) would widen the herders' and drovers'
  beasts of the old ages.
- **Sound and music.** Music Manager (Continued) ([3220120457](https://steamcommunity.com/sharedfiles/filedetails/?id=3220120457))
  is in the list; no tribal or medieval soundtrack in the catalogue is checked for 1.7. Mods like Pure Medieval Soundtrack
  or Kingdom Music exist on the Workshop, but their pages give no current version, so none is recommended until the
  player checks one.
