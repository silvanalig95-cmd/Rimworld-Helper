# Rimfolk's own look: windows you can move and size, and a clearer, modern style

The player (8 November 2026): "for the rimfolk uis, please also dont be withheld by the vanilla ui, for example for the
diplomacy screen, i'd love to move the window around, make it larger etc. you really don't have to stick to the rimworld
design theme if you can otherwise make all uis clearer, better to use, better looking, modern etc." Before that they had
seen the diplomacy screen's mockup (https://claude.ai/artifact/HihYZRcRr7jkKdbzasU2Tm) and asked whether the game would
look the same; this is the answer: as near as the game's interface code allows.

## What changes

1. **Every window of Rimfolk's moves and sizes.** The region window (map, peoples, here and away, history), the
   council's windows (Council, Planning, Defenses), the trade window, the Almanac, letters and dialogs of Rimfolk's own:
   - dragged by their title bar, resized from their edges and corner, with a sensible minimum;
   - a button to make one large (most of the screen) and back, and double-clicking the title does the same;
   - each remembers where it was and how big, between openings and between games (the mod's settings);
   - the main tab buttons still open them, but as floating windows rather than panels fixed at the screen's foot;
   - layouts follow the size: more columns and more shown when wider, nothing clipped when narrow.
2. **A clearer, modern style (Rimfolk's own, not the game's).**
   - A calm dark ground, cards with soft rounded corners and a faint edge, more space between things, one accent colour
     for what is chosen or yours, the stance colours (allied, neutral, hostile) with a word or icon beside them always.
   - Buttons, chips, tabs and toggles drawn flat with clear hover and pressed states; tabs that read as tabs; a selected
     row clearly lit; focus on text fields visible.
   - Type: the game's fonts (the only ones the interface can load), but a clear hierarchy by size and ink, never bold
     smeared, numbers right-aligned.
   - Built once in `UiKit` (colours, radii, spacing, the drawn parts), generated at load, nothing made per frame.
3. **A setting**: the look, "Rimfolk's own" (the default) or "the game's", and the windows' movable/resizable behaviour
   on its own switch, so the classic look stays for anyone who wants it.

## Order of building

After the diplomacy screen's step B (it is working in the same windows): first the window frame and the style kit with
the region window and the trade window, then the council's windows, the Almanac and Rimfolk's dialogs.
