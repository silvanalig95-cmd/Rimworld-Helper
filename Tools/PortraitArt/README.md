# Tools/PortraitArt

Since 24 November 2026 the portrait kit's scripts (`*.ps1` and their `.cmd` files) and the list planners
(`plan_batches.py`, `plan_creatures.py`, `plan_gods.py`, `character_words.py`, with their tests) are kept in Rimfolk
Studio (repository silvanalig95-cmd/rimfolk-studio: its folders `kit` and `planning`). Take a copy from the Studio when a
kit zip is built; never plan or change them here. `docs/portrait-art/STUDIO-HANDOVER.md`, section 0, says who keeps what.

Kept here, with the catalogue (`docs/portrait-art/catalogue`) and `docs/player-mods`:
- `catalogue_check.py`: the catalogue checked (every word and def name known).
- `catalogue_words.py`: the catalogue's words (ages, roles, eras, colours) and the save census's readers, which
  `catalogue_check.py` and `save_census.py` read. The Studio's planners have a copy of their own.
- `save_census.py`: the kinds' counts, ages, roles and dress from a save, into `kinds.csv` (hand columns kept).
- `creature_census.py`: `creatures.csv` from the player's enabled mods and a save (hand columns kept).
- `looks_to_defs.py`: `Defs/PortraitLookDefs/PortraitLooks.xml` from `looks.csv`.
- `test_portrait_catalogue.py`, `test_creatures.py`: their tests.
- `make_prompts.py`: the first wave's prompts (`docs/portrait-art/prompts.csv`), the earliest way, kept only because the
  game's matcher tests read that file as a library.
