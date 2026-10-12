# Modlists

The modlists Rimfolk is played with, and what Rimfolk knows about the mods in them.

- `current-main.txt`: the modlist in use now (RimWorld 1.6, all DLCs): industrial and spacer, with Rimsenal,
  Vanilla Expanded and Alpha mods. One package id per line, in load order. Its 87 mods that aren't in the medieval
  list are researched in `mods/G_NewModlist.md`.
- `medieval-main.txt`: the earlier medieval modlist (RimWorld 1.6), same format.
- `mods/`: one note per theme, a section for every mod with what it adds, how it plays, what Rimfolk already does
  with it, and hook ideas for future features. New Rimfolk features start by checking these notes.
  - `A_MedievalOverhaul.md`: Medieval Overhaul and add-ons, Rimedieval, Medieval Total Rim, Divine Order (34 mods)
  - `B_FactionsWorld.md`: Outland, Forgotten Realms, al9000, faction and diplomacy mods, Outposts, Hospitality,
    Vanilla Events Expanded (31)
  - `C_PeopleMagic.md`: races and xenotypes, A RimWorld of Magic, psycasts, memes, traits and skills (30)
  - `D_NatureAnimals.md`: animal, biome, terrain, weather, plant and mining mods (33)
  - `E_MakingThings.md`: food, drink, furniture, apparel, books, hygiene, storage, floors (45)
  - `F_QualityOfLife.md`: interface, hauling, work, combat helpers, saves, libraries (36)
- `HOOKS.md`: the most promising integration ideas from all of the above, and conflicts in the modlist.

The in-game Almanac has a "Your mods" category built from the same research
(`Defs/AlmanacArticleDefs/Almanac_Mods_*.xml`). Each article lists the mods it is about and only shows when one of
them is active, so the Almanac fits whichever modlist is loaded.

## Adding a new or alternative modlist
1. Save it here as `<name>.txt` (one package id per line; a pasted `ModsConfig.xml` works too).
2. Compare it with the notes in `mods/` to find the mods that are new.
3. Research the new mods, add their sections to the matching theme note (or a new one), and write Almanac articles
   for the ones that change how the game plays.
4. Look for compatibility issues with Rimfolk and for new hook ideas.
