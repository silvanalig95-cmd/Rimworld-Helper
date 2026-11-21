# The portrait library's matcher: who gets which picture

Written by the tests (`Tools/Tests/PortraitLibraryTests.cs`). There is no art yet, so this table stands in for preview
sheets: the first wave's prompt list (`docs/portrait-art/prompts.csv`) is read as a library, each row a face of its own
with the painted mid-tones of its words (skin s1 #f1d6c2 to s5 #5a3a28), and each sample person, with their skin and
hair in the game's own colours, is matched against the trial's 16 pictures and against all 200. The score is the
distance (0 is their exact look; above 6 the game's own face is kept); the last column gives the nearest three of the 200.

| Person | Trial (16) | All 200 | Nearest of the 200 |
|---|---|---|---|
| a grown man, the game's fairest skin, dark brown hair cut short, a full beard, in a medieval tunic | `m_adult_s1_black_short_beard_tribal_001` (3.6) | `m_adult_s1_black_short_beard_medieval_051` (1.6) | `m_adult_s1_black_short_beard_medieval_051` 1.6, `m_adult_s1_black_short_stubble_medieval_061` 2.6, `m_adult_s1_black_short_beard_industrial_101` 3.6 |
| another grown man who looks just like him | `m_adult_s1_black_short_beard_tribal_001` (3.6) | `m_adult_s1_black_short_stubble_medieval_061` (2.6) | `m_adult_s1_black_short_beard_medieval_051` 1.6, `m_adult_s1_black_short_stubble_medieval_061` 2.6, `m_adult_s1_black_short_beard_industrial_101` 3.6 |
| a grown woman, the game's darkest skin, long black hair, in an industrial jacket | the game's own face (nearest 8.8) | `f_adult_s5_red_long_nobeard_industrial_135` (3.9) | `f_adult_s5_red_long_nobeard_industrial_130` 3.9, `f_adult_s5_red_long_nobeard_industrial_135` 3.9, `f_adult_s5_red_long_nobeard_medieval_080` 5.9 |
| a young woman, very fair, long red hair tied back, medieval | the game's own face (nearest 8.2) | `f_young_s1_blond_long_nobeard_medieval_090` (3.6) | `f_young_s1_blond_long_nobeard_medieval_090` 3.6, `f_young_s2_red_long_nobeard_medieval_091` 4.2, `f_young_s1_blond_long_nobeard_industrial_140` 5.6 |
| a young man, a tan skin, short light brown hair, stubble, in a flak vest | the game's own face (nearest 6.1) | `m_young_s3_black_short_nobeard_industrial_117` (4.1) | `m_young_s3_black_short_nobeard_industrial_117` 4.1, `m_young_s2_red_long_nobeard_industrial_116` 5.6, `m_young_s3_black_short_nobeard_medieval_067` 6.1 |
| an old woman, a brown skin, long grey hair in a bun, tribal | the game's own face (nearest 11.2) | `f_old_s3_grey_short_nobeard_tribal_044` (1.7) | `f_old_s3_grey_short_nobeard_tribal_044` 1.7, `f_old_s4_grey_long_nobeard_tribal_045` 3.5, `f_old_s3_grey_short_nobeard_medieval_094` 3.7 |
| an old man, fair, bald, a white beard, in a spacer suit | `m_old_s2_white_short_beard_industrial_118` (2.8) | `m_old_s2_white_short_beard_spacer_168` (0.8) | `m_old_s2_white_short_beard_spacer_168` 0.8, `m_old_s1_grey_bald_stubble_spacer_172` 2.5, `m_old_s2_white_short_beard_industrial_118` 2.8 |
| an old man who never went grey: brown hair, short, a moustache, medieval | `m_old_s2_white_short_beard_industrial_118` (6.0) | `m_old_s2_white_short_beard_medieval_068` (4.0) | `m_old_s2_white_short_beard_medieval_068` 4.0, `m_old_s1_grey_bald_stubble_medieval_072` 4.2, `m_old_s2_white_short_beard_industrial_118` 6.0 |
| a boy of ten, a dark skin, short black hair, tribal | `m_young_s5_lbrown_long_nobeard_tribal_014` (5.9) | `m_child_s5_blond_short_nobeard_tribal_023` (3.1) | `m_child_s5_blond_short_nobeard_tribal_023` 3.1, `m_child_s5_blond_short_nobeard_medieval_073` 5.1, `m_child_s5_blond_short_nobeard_industrial_123` 7.1 |
| a girl of eight, fair, long blond hair, medieval | the game's own face (nearest 20.4) | `f_child_s1_red_short_nobeard_medieval_099` (5.4) | `f_child_s1_red_short_nobeard_medieval_099` 5.4, `f_child_s2_black_long_nobeard_medieval_100` 6.7, `f_child_s1_red_short_nobeard_industrial_149` 7.4 |
| a grown woman, fair, short blond hair, in a spacer suit | the game's own face (nearest 10.2) | `f_adult_s1_black_short_nobeard_spacer_181` (4.4) | `f_adult_s1_black_short_nobeard_spacer_181` 4.4, `f_adult_s1_black_long_nobeard_spacer_176` 5.4, `f_adult_s1_black_long_nobeard_spacer_186` 5.4 |
| a grown woman, a dark skin, her head shaved, industrial | the game's own face (nearest 6.3) | `f_adult_s5_red_long_nobeard_industrial_130` (4.6) | `f_adult_s5_red_long_nobeard_industrial_130` 4.6, `f_adult_s5_red_long_nobeard_industrial_135` 4.6, `f_adult_s4_blond_long_nobeard_industrial_129` 6.3 |
| a grown man with dyed blue hair, short, industrial | the game's own face (nearest 7.7) | `m_adult_s1_black_short_stubble_industrial_111` (4.9) | `m_adult_s1_black_short_stubble_industrial_111` 4.9, `m_adult_s1_black_long_nobeard_industrial_106` 5.4, `m_adult_s1_black_short_beard_industrial_101` 5.9 |
| a teenage boy of fifteen, very fair, short blond hair, industrial | the game's own face (nearest 12.6) | `m_young_s1_blond_short_nobeard_industrial_115` (0.6) | `m_young_s1_blond_short_nobeard_industrial_115` 0.6, `m_young_s1_blond_short_nobeard_medieval_065` 2.6, `m_young_s1_blond_short_nobeard_spacer_165` 2.6 |
| a grown man, a dark skin, short black hair, a moustache, tribal | the game's own face (nearest 16.5) | `m_adult_s5_red_short_stubble_tribal_005` (4.6) | `m_adult_s5_red_short_stubble_tribal_005` 4.6, `m_adult_s5_red_long_nobeard_tribal_010` 5.6, `m_adult_s5_red_short_stubble_medieval_055` 6.6 |
| a hussar woman, a tan skin, long brown hair, spacer (no hussar pictures yet) | `f_adult_s3_lbrown_long_nobeard_spacer_178` (3.1) | `f_adult_s3_lbrown_long_nobeard_spacer_178` (3.1) | `f_adult_s3_lbrown_long_nobeard_spacer_178` 3.1, `f_adult_s2_brown_long_nobeard_spacer_182` 3.6, `f_adult_s3_lbrown_short_nobeard_spacer_183` 4.1 |
| an impid man, horns and pointed ears (no impid pictures yet) | the game's own face (none of their kind, sex and age) | the game's own face (none of their kind, sex and age) |  |
| a dark elf woman (no dark elf pictures yet) | the game's own face (none of their kind, sex and age) | the game's own face (none of their kind, sex and age) |  |
| a toddler of two | the game's own face | the game's own face (no picture for a baby or a toddler) | |

The two men who look alike get different faces: of the nearest few, each person gets the nearest of those fewest others keep, then
one by their own id.
A kind with no pictures of its own gets a baseliner's where it looks near a human (the hussar),
else the game's own face (the impid's horns, the dark elf's grey skin); once a kind's pictures are in, its people get
only theirs. A person keeps their face for good; when their body changes (a lost eye, a scar, growing old) the face's
variant with that mark is shown where the library has one.
