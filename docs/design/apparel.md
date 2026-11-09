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

5. The rating, tags and line at the top of the game's info card for apparel, Rimfolk's own apparel card (opened first,
   as the weapon card is), a badge on the gear tab and Nice Inventory Tab, the piece in hand with its quality, stuff,
   wear and any traits, and the rating for a chosen colonist (their body, temperature and work).

## Step 3: recommendations

6. Better armour and clothes for each colonist from what the colony has, by their role, the season and the raids met,
   their faith's and title's apparel rules, their body; one click to wear it; colonists sometimes ask, rarely.
