# Modlists

The modlists Rimfolk is played with, and what Rimfolk knows about the mods in them.

- `medieval-main.txt`: the main medieval modlist (RimWorld 1.6), one package id per line, in load order.
- `mods/`: one note per theme. Each mod gets a section with what it adds, how it plays, what Rimfolk already does
  with it, and hook ideas for future features. New Rimfolk features start by checking these notes.

The in-game Almanac has a "Your mods" category built from the same research
(`Defs/AlmanacArticleDefs/Almanac_Mods_*.xml`). Each article lists the mods it is about and only shows when one of
them is active, so the Almanac fits whichever modlist is loaded.

## Adding a new or alternative modlist
1. Save it here as `<name>.txt` (one package id per line; a pasted `ModsConfig.xml` works too).
2. Compare it with the notes in `mods/` to find the mods that are new.
3. Research the new mods, add their sections to the matching theme note (or a new one), and write Almanac articles
   for the ones that change how the game plays.
4. Look for compatibility issues with Rimfolk and for new hook ideas.
