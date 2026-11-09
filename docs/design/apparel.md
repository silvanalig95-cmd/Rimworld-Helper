# Armour and apparel you can judge at a glance

The player (9 November 2026), after the weapon rating: "And maybe after you can do the whole thing, the same thing for
armor and apparel. But that's for to do after." The weapons are done (0.182.0 to 0.184.0, `docs/design/weapons.md`), so
the same three steps follow for every piece of armour and apparel the player's enabled mods add (512 active defs in
`docs/player-mods/apparel.tsv`: the game and its DLCs, Elves, A RimWorld of Magic, Vanilla Apparel and Armour Expanded,
Rimsenal, VFE Medieval 2 and others), with the player's same wishes: nuanced (special abilities, passive effects,
situations), colour-coded tags, a rating from 1.0 to 10.0 with real range.

## Step 1: the rating and the review of every piece

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
   The apparel card shows the piece's own picture large in its head, as the weapon card does since the player's "always
   have an image of the item included ... it can have a prominent spot because they look cool" (9 November 2026), with the
   same piece: `UiKit.ItemPortrait` (`UI/UiKitItems.cs`; the thing as it looks, its stuff, style and graphic by the game's
   `Widgets.ThingIcon`, a kind by `Widgets.DefIcon`, framed in its quality's colour from `UI/ItemPortraitMath.cs`, the tile
   96 to 128 pixels by `ItemPortraitMath.CardTile`), and small ones wherever it lists other pieces. Its suggestions on the
   gear tab can use the weapons' row (`WeaponPickGearTab.Row` with a `SuggestLook`) and its place in Nice Inventory Tab's
   apparel box the same way as the equipment box's (`Weapons/WeaponNiceTab.cs`).

## Step 3: recommendations

6. Better armour and clothes for each colonist from what the colony has, by their role, the season and the raids met,
   their faith's and title's apparel rules, their body; one click to wear it; colonists sometimes ask, rarely.
