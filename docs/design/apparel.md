# Armour and apparel you can judge at a glance

The player (9 November 2026), after the weapon rating: "And maybe after you can do the whole thing, the same thing for
armor and apparel. But that's for to do after." The weapons are done (0.182.0 to 0.184.0, `docs/design/weapons.md`), so
the same three steps follow for every piece of armour and apparel the player's enabled mods add (512 active defs in
`docs/player-mods/apparel.tsv`: the game and its DLCs, Elves, A RimWorld of Magic, Vanilla Apparel and Armour Expanded,
Rimsenal, VFE Medieval 2 and others), with the player's same wishes: nuanced (special abilities, passive effects,
situations), colour-coded tags, a rating from 1.0 to 10.0 with real range.

## Step 1: the rating and the review of every piece

**Built (9 November 2026, not released).** `Source/ColonyCouncil/Apparel/ApparelRateMath.cs` (the rating, pure math) and
`ApparelTagMath.cs` (the tags, on the weapons' one table of kinds and colours), tested in
`Tools/Tests/ApparelRateTests.cs` against the base game's pieces; `Tools/apparel_extract.py` reads every active piece's
numbers (`docs/player-mods/apparel_numbers.json.gz`, with the human body's zones, the reference enemies' weapons and
races, the apparel rules of titles and faiths) and `Tools/ApparelReview` rates them all into `docs/apparel/README.md`
(how it works, the reference wearer and enemies, the anchors, the tag legend, every table) and `docs/apparel/apparel.json`
(everything, for the tier-list page and step 3). 509 of the 512 active pieces are rated, 3 left out (they vanish when
taken off), 63 marked rough (part of what they do lives in a mod's own code, read from it where it could be). Every
piece is rated for a shooter and for a melee fighter (`ratingShooter`, `ratingMelee`); one that clearly suits one of them
(it blocks shooting, is an off-hand shield, or does a tenth more for one) is rated for that one and tagged so. The
tier-list page isn't made yet.

1. **One rating from 1.0 to 10.0** for what a piece does for whoever wears it, by the game's own rules: armour (sharp,
   blunt, heat) against the reference enemies' weapons through the game's armour roll, by the body parts it covers and
   its layer; insulation against cold and heat; what it costs the wearer (move speed, aiming, work, social, beauty and
   the mood of wearing it); its abilities and what it adds (shield belts, jump packs, smokepop, psychic and apparel
   stat offsets, Royalty's psylink and title apparel, Biotech's mechanitor gear, Anomaly's gear, Odyssey's vacuum
   suits). Rated by what it is for (combat armour, helmets, utility, everyday clothes, cold or hot weather, ceremonial),
   normal quality, in a sensible stuff where it takes one.
2. **Sub-ratings and situations**: against guns, against blades and fists, against fire, cold, heat, toxins, vacuum,
   psychic attacks, for work, for trade and social, for hunting; "best for" and "weak when".
3. **Tags** in the weapons' six kinds and colours (strength, weakness, risk, special, role, needs), each from the
   piece's own numbers with a reason.
4. **A review of all of them** (`docs/apparel/README.md`, `docs/apparel/apparel.json`) and a tier-list page.

## Step 2: in the game

**Built (9 November 2026, not released),** after the player's screenshot of the game's plain card for a "Bluefur parka
(awful)": "i don't see the gear cards in game by default, is this implemented at all?" One reading for the review and the
game (`Apparel/ApparelReadMath.cs`: the review's `Inputs.Facts` moved into the mod, so regenerating the review gives the
same files byte for byte); the live def and the piece in hand as facts (`Apparel/ApparelLive.cs`: quality's factors by the
game's own stats, the mood of worn clothes and of clothes taken from the dead, a rolled enchantment, biocoding); the rating
for a colonist (`Apparel/ApparelForMath.cs`: how they fight, their body, their weather against what they keep wearing, the
moods it adds); the info card's section (`ApparelInfoCard.cs`); Rimfolk's apparel card on a new item-card base
(`ApparelCardWindow.cs`, `UI/ItemCardWindow.cs`: protection by body part as a figure, warmth against the weather, the piece
as it is, other materials, for a colonist and what it would replace); opening first through the weapons' one window-stack
hook (`ApparelCardFirst.cs`); the badge on the game's gear tab and Nice Inventory Tab's rows, cells and apparel slots
(`ApparelGearTab.cs`); settings under How you're told, Apparel. DESIGN.md "Armour and apparel you can judge at a glance"
has all of it; the Almanac's "judging armour and clothes". The suggestions' rows (below) are step 3's.

5. The rating, tags and line at the top of the game's info card for apparel, Rimfolk's own apparel card (opened first,
   as the weapon card is), a badge on the gear tab and Nice Inventory Tab, the piece in hand with its quality, stuff,
   wear and any traits, and the rating for a chosen colonist (their body, temperature and work).
   The apparel card shows the piece's own picture large in its head, as the weapon card does since the player's "always
   have an image of the item included ... it can have a prominent spot because they look cool" (9 November 2026), with the
   same piece: `UiKit.ItemPortrait` (`UI/UiKitItems.cs`; the thing as it looks, its stuff, style and graphic by the game's
   `Widgets.ThingIcon`, a kind by `Widgets.DefIcon`, framed in its quality's colour from `UI/ItemPortraitMath.cs`, the tile
   96 to 128 pixels by `ItemPortraitMath.CardTile`), and small ones wherever it lists other pieces. Its suggestions on the
   gear tab can use the weapons' row (`WeaponPickGearTab.Row` with a `SuggestLook`) and its place in Nice Inventory Tab's
   apparel box the same way as the equipment box's (`Weapons/WeaponNiceTab.cs`).

## Step 3: recommendations

**Built (9 November 2026, not released),** on the colonist's own rating (`ApparelForMath.For`, `ApparelLive.ForColonist`),
the way the weapons' step 3 was. The pure rules are `Apparel/ApparelPickMath.cs` (tested in `Tools/Tests/ApparelPickTests.cs`),
the game's side `ApparelPick.cs` (the colony, the season ahead, title, role and faith, the outfit, the orders),
`ApparelPickUi.cs` (the gear tab's rows and quiet line, the Defenses page), `ApparelNiceTab.cs` (Nice Inventory Tab),
`ApparelSwapJob.cs` (a spare handed over, armour traded), `ApparelAsks.cs` and `ApparelPickBook.cs` (asks, "not now", the
clock). DESIGN.md "Step 3: better armour and clothes for each colonist, and colonists who ask" has all of it.

6. Better armour and clothes for each colonist from what the colony has, by their role, the season and the raids met,
   their faith's and title's apparel rules, their body; one click to wear it; colonists sometimes ask, rarely.
   - Gaps first, each filled with anything rated 2.0 or more: warmth for anyone short for the season ahead (this twelfth
     and the next two, under 5 °C as the game's "Need warm clothes" counts it), the most lacking first, a coat then a hat;
     the heat past 30 °C; body armour and a helmet for those who fight when raids come often (two this month); worn-out
     pieces and those from the dead. Then upgrades of 0.8 or more (a piece for an empty slot only at 6.0 for them, or for
     a rule they lack), armour's gains by how much each fights, a piece in place of several judged in each one's role,
     nothing that leaves them short of warmth. One assignment for the colony; trades of armour as the weapons trade.
   - Their outfit: never suggested past it; said with **Allow** (its kind in that outfit) or **Outfit** (its limits). Never
     moved off what their title, role or faith asks them to wear; a title's least quality kept; a lucky piece or an
     heirloom a warning, never a block.
   - Shown on the gear tab (the game's, Nice Inventory Tab's apparel box, or its equipment box in the grid layout), the
     apparel card ("Who should wear this") and the Defenses page ("Clothes and armour for each colonist", "Wear all
     suggested"); **Wear** is the game's own Wear job. Colonists ask, rarely ("It's freezing; that parka in the stockpile
     would do me good."). Settings "Apparel suggestions" and "Colonists ask for better clothes"; no "put on missing warm
     clothes themselves", as the game's own apparel optimizer already does that for what an outfit allows.
