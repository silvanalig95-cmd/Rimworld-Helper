# Defense designs that work in RimWorld 1.5/1.6: research for the Rimfolk defense planner

Compiled 24 October 2026 for the defense planner in `/home/user/Rimworld-Helper` (player on 1.6 with all DLCs). This is research only. No mod files were changed.

## How to read this

Every claim carries a version tag and a source type:

| Tag | Meaning |
|---|---|
| **[code 1.6]** | Read in the decompiled 1.6 `Assembly-CSharp` (`scratchpad/defense_research/src16/rw16`). Strongest evidence. |
| **[code 1.5]** | Read in the decompiled 1.5 assembly (`scratchpad/defense_research/src15/rw15`), used to say what changed. |
| **[def]** | From a vanilla `Data/` XML copy (`scratchpad/defense_research/data_dl/awl`, version not stated, no Odyssey folder, so probably 1.5). Used only for values that 1.6 sources repeat (path costs, blast radii, cover fill). |
| **[sim]** | Computed with `Tools/DefenseSim/kbsim.py`, a line-for-line port of 1.6 `GenSight.LineOfSight`, `ShootLeanUtility` and `CoverUtility`. Use it to re-check any new layout. |
| **[wiki]** | rimworldwiki.com, fetched October 2026. The defense pages carry a "needs a 1.6 rewrite" banner, so read them with care. |
| **[community]** | Steam discussions and guides, RimworldHub, a video summary. Each comes with a date and version. |

**Access gaps.** Reddit (r/RimWorld, r/RimWorldPorn) refuses this environment, both through the search tool and through direct fetches. The Ludeon forums return 403 behind Cloudflare. YouTube rate-limited every watch page (429), so I could read only video titles and a third-party summary. The community side therefore leans on Steam threads and guides plus RimworldHub. Where those disagree with the code, the code wins, and the conflict is noted.

---

## 0. Mechanics that decide every design (read this first)

### 0.1 Why raiders stop where they stop: the 1.6 change

`JobGiver_AIFightEnemy.TryGiveJob` **[code 1.6]**: a ranged raider stops and shoots from its current cell when either of two things is true:

* it has **more than 1% cover against its target**, its cell is **walkable**, and it can hit the target, or
* it is **within 5 cells** of the target and can hit it.

Otherwise it searches for a firing position (`CastPositionFinder`) and walks there.

**What changed from 1.5:**

* In 1.5, both the "stay here" check and the firing-position search required `Standable`. A cell holding sandbags, a barricade, a fence or a column is walkable but not standable, so in 1.5 a raider could never choose to stop on one **[code 1.5]**.
* In 1.6, both checks use `WalkableBy` **[code 1.6]**:
  * A raider who is re-evaluating while standing on a sandbag or barricade will stop and shoot if it has cover and line of sight. Re-evaluation happens when its job expires or when a hit staggers it.
  * The firing-position search now considers such cells too, but multiplies their preference by 0.4. They still come out much less attractive than a bare cell.

This is the patch note "Fix: AI casting on barricades" (1.6.4518) **[wiki: Version/1.6.4518]**.

**Practical result:** a sandbag at a corner now only *discourages* raiders from stopping there. The Steam tester was right that "it has never been 100%" (Jul 2025) **[community]**. The wiki and RimworldHub are right that it is no longer a guarantee. The reliable fix is geometry: no cell a raider can reach before the kill zone may have both cover and a view of your shooters.

**1.5 against 1.6, side by side** (both decompiles, defense_research/src15 and src16):

| Rule | 1.5 | 1.6 |
|---|---|---|
| "Stop and shoot from here" needs the current cell to be | `Standable` (no sandbag, barricade, fence or column on it) | `WalkableBy` (sandbag and barricade cells allowed) |
| Firing-position search accepts a cell if | `Standable` | `WalkableBy`, and a cell holding sandbags, a barricade, a fence or a column scores ×0.4 |
| Cover math (adjacent cells, angle bands, ×1.75 diagonal, ×1/3 and ×2/3 at close range) | same | same |
| Lean-shooting rules | same | same |
| Breach path cost per wall cell | 10 + 0.01 × HP | 20 + 0.015 × HP |
| Smart raids know traps | (patch note: avoidance was buggy) | 30% of traps, fixed per trap; known traps cannot spring on them |

Other parts of the firing-position search **[code 1.6]**:

* It weights cover from the target by 0.55.
* It prefers positions near 0.8 of the weapon's range.
* It halves the value of cells within 5 of the target.
* Mechanoids have `aiAvoidCover` **[def]**, so they actively prefer open cells. Corner-camping is a humanlike problem.

### 0.2 Cover math (identical in 1.5 and 1.6) [code 1.5/1.6]

* Cover comes only from the **8 cells touching the target**.
* Block value by object:

  | Object | Cover |
  |---|---|
  | Wall, closed door, other full-height building | 75% |
  | Sandbag or barricade | 55% |
  | Chunk | 50% |
  | Mini-turret | 40% |
  | Column or fence | 25% |
  | Open door | 0% |

* The value is scaled by the angle between "target → cover cell" and "target → shooter":

  | Angle | Share of the value kept |
  |---|---|
  | under 15° | 100% |
  | 15° to 27° | 80% |
  | 27° to 40° | 60% |
  | 40° to 52° | 40% |
  | 52° to 65° | 20% |
  | 65° or more | nothing |

* For **diagonal** neighbours the angle is multiplied by 1.75.
* Cover shrinks when the shooter is close to the cover cell: ×1/3 under 1.9 cells, ×2/3 under 2.9 cells.
* Several cover cells combine as 1 − Π(1 − b).
* **Cover is computed at the target's real cell, even while it leans out to fire** (`ShotReport.HitReportFor` → `CalculateCoverGiverSet(target, caster.Position)`). The wiki line "walls do not provide their full cover because pawns lean out" is not borne out by the 1.6 code. A colonist directly behind a wall tile keeps its 75% against shots from the front while it fires from a side cell.

**Lean rules** (`ShootLeanUtility`) **[code 1.6]**:

* A pawn whose cell directly toward the target holds a wall leans sideways into the adjacent cell. That side cell must be see-over, and the cell diagonally forward on that side must not be a wall.
* A pawn can also fire from an adjacent see-over cover cell (a sandbag) in the target's direction.
* Shooters can hit a target at the target's lean cells too.

### 0.3 Cover numbers for the patterns players argue about [sim]

**Defender cover** against a raider 15 cells away, by the raider's bearing off straight ahead:

| Pattern (shooter `f`) | 0° | 15° | 30° | 45° | 60° | 75° | 90° |
|---|---|---|---|---|---|---|---|
| Plain sandbag line, shooter behind S | 55% | 56% | 55% | 65% | 50% | 22% | 0% |
| W-S-W, shooter behind the S (the mod's FiringLine) | 55% | 61% | 63% | 80% | 64% | 30% | 0% |
| S-W-S, shooter behind the W, leaning out | 75% | 69% | 63% | 68% | 52% | 22% | 0% |
| Sandbag "U" (S in front and both sides) | 55% | 56% | 60% | 73% | 67% | 56% | 55% |
| **Pocket**: W in front, S on both front diagonals and both sides | **75%** | **69%** | **67%** | **75%** | **68%** | **56%** | **55%** |
| Corner peek (behind the end of a wall, gap beside) | 75% | 60% | 45% | 30% | 15% | 0% | 0% |

**Raider cover** in a 1-wide doorway through a wall, with your shooters `D` cells inside at a sideways offset:

| Raider position | Shooter 0–25° off the doorway axis | 34° | 45° | Beside the opening (75°+) |
|---|---|---|---|---|
| In the doorway cell | **0%** | 15% | 30% | 60–75% |
| One cell back in the tunnel (diagonal walls in front of it) | 0% at 6°, 15% at 9–17°, 30% at 17–25°, 45% at 25° | 66% | 82% | — |
| One step out into the box | **0%** | 0% | 0% | 0% |

**Raider cover at the corner of the last turn**, leaning out (the classic corner-camping case): **75%** against every shooter it can see.

### 0.4 Line of sight, interception, friendly fire [code 1.6]

* Line of sight steps cell to cell along the line, stepping either sideways or forward, never diagonally. Any full wall blocks it. Sandbags, barricades, embrasures (part-fill), turrets and pawns do not.
* A projectile can never be intercepted by anything within **5 cells of where it was fired**. The interception chance ramps from 0 at 5 cells to full at 12 cells. Friendly hits are further ×0.4 by default difficulty (`friendlyFireChanceFactor`).
* Consequences:
  * A second rank of shooters, melee blockers, or a mini-turret up to about 4.5 cells in front of a shooter will not be hit by that shooter's misses.
  * Raiders likewise fire over each other within 5 cells.

### 0.5 Movement [code 1.6, def]

* Entering a cell costs the pawn's step time plus the cell's path cost:

  | What is in the cell | Extra ticks |
  |---|---|
  | Sandbags or barricade | +42 |
  | Fence | +80 |
  | Column | +0 (cannot be stopped on in 1.5, slows nobody) |
  | Mud, marshy soil, soft sand, ice | +14 |
  | Shallow water | +30 to +42 |
  | Soil | +2 |
  | Constructed floor | +1 |

* A healthy human takes about 13 ticks per cardinal step.
* Path-cost objects with `pathCostIgnoreRepeat` charge only once in a row, so **adjacent sandbags or fences slow only once. Space them every other cell.**
* Paving the kill zone is irrelevant: floor 1 against soil 2.
* **Darkness:** humanlike move speed ×0.8 at 0% light, rising to ×1.0 at 30% light (`StatPart_Glow`). Darkness has no general accuracy effect in vanilla; only some Ideology precepts change accuracy in the dark.

### 0.6 Pathing and what makes raiders break walls [code 1.6, wiki]

* **Normal assault raiders** path with `TraverseMode.ByPawn` to the nearest reachable colonist or target. The length of the path does not matter. Closed player doors count as walls; a door held open counts as a path. With **no path**, they attack random walls and doors, or furniture they can reach **[wiki: Raider, Defense structures]**.
* **Smart raids, sapper raids and smart breach raids** use an **avoid grid**:
  * Every colony turret marks the cells within (range + 4) that it can see, adding 45 to each (×8 as path cost).
  * This includes **unpowered** turrets: `AvoidGrid.Regenerate` does not check power.
  * Such raids also lower the value of those cells as firing spots, to ×0.1 at most.
  * They "know" **30%** of your traps (fixed per trap and faction). A known trap cannot spring on them, and they path around it (`LordJob_AssaultColony.AvoidTrapRatio` 0.3; `Building_Trap`).
  * The wiki line that "smart raiders don't avoid traps" is outdated (1.6 patch: "Smart raiders now correctly avoid some traps").
* **Breachers** pick a **random owned bed** as the destination (`GenAI.RandomRaidDest`).
  * They build one breach path, where each wall cell costs only 20 + 0.015 × HP (1.6; it was 10 + 0.01 × HP in 1.5). A granite-block wall (510 HP) costs about 28, roughly two cells of walking.
  * **Wall thickness and material barely steer breachers; they only buy time.**
  * Breachers with ranged weapons work from at most **12 cells** of their target wall. Escorts follow them.
* **Sappers** dig toward a bed with `PassAllDestroyableThings` and use the turret avoid grid. The wiki and community add that they avoid high-HP ore and turret fields.

### 0.7 Drop pods [code 1.6, wiki]

* Center-drop raids from non-mechanoid factions land at a **powered, unroofed orbital trade beacon 40% of the time** if one exists (`PawnsArrivalModeWorker_CenterDrop`). Otherwise they land around a colonist who is not under overhead mountain.
* Pods open 520 ticks (about 8.7 s) after landing.
* Nothing drops under overhead mountain.

### 0.8 Explosions and turret numbers [def, wiki, community 1.6]

Bomb damage is 50, ×4 against impassable buildings (a wall takes 200) and ×2 against passable ones (turrets, sandbags, traps) **[def + code 1.6 `DamageWorker.Apply`]**. The wiki's "×8 against walls" does not match the def.

| Thing | Radius / numbers |
|---|---|
| Frag grenade | range 12.9, lands within about 1.9 of the aim point, blast radius 1.9, 100-tick fuse, cannot be thrown over walls |
| EMP grenade | blast 3.5 |
| Molotov | 1.1 (fire) |
| IED (HE) | 50 bomb damage, radius 3.9, 15-tick fuse; sets off other IEDs in its radius |
| Mini-turret | 100 HP (steel); range 28.9; destruction blast 3.9 (50% of turrets can explode, 240-tick fuse once under 20% HP) |
| Autocannon | 380 HP; range 8.9–32.9; blast 5.9 measured from its **lower-left** tile |
| Uranium slug turret | 380 HP; range 11.9–45.9 (most accurate at 35–45); blast 5.9 from lower-left |
| Mortar | destruction blast 4.9; minimum range about 30 |
| Triple rocket / doomsday | range 35.9; blast 3.9 / 7.8 |
| Termite thump cannon | range 24.9; knocks out a 3-wide stretch of wall; stone-block walls in 2 shots, plasteel in 4 **[wiki]** |
| Wall HP | wood 195, steel 300, granite blocks 510 (×1.7), plasteel 840 |

**Big-turret cover bug** **[wiki, verified 1.4.3580]**: 2×2 turrets only benefit from cover touching their lower-left (anchor) tile. A sandbag row in front of the top edge does nothing when the threat is to the north.

### 0.9 Ranges that size a kill zone [wiki/RimworldHub 1.6]

| Weapon | Range |
|---|---|
| Chain shotgun | 12.9 |
| Pump shotgun | 15.9 |
| Heavy SMG | 22.9 |
| LMG | 25.9 |
| Charge rifle | 27.9 |
| Assault rifle, minigun | 30.9 |
| Bolt-action rifle | 36.9 |
| Sniper rifle | 44.9 |
| Short bow / recurve / greatbow / pila | 22.9 / 25.9 / 29.9 / 18.9 |
| Charge lance (lancer) | about 27–30 (sources differ) |
| Needle gun (pikeman) | 44.9 |
| Inferno cannon / heavy charge blaster (centipede) | 26.9 |

### 0.10 Raid sizes [def, derived]

* Pirate pawn combat power runs 35–140 (drifter 35, pirate 65, grenadiers 55–70, heavy and slasher 140). Tribal pawns run 40–85.
* A **3000-point raid is roughly 25–45 pirates or 40–70 tribals**.
* Human raiders flee after losing **40–70%** of their group, or after 10–15 h **[wiki: Raider]**. A box only has to kill about half.

---

## 1. Killboxes that work in 1.5/1.6

### 1.1 Principles (with where each comes from)

1. **One open way in, everything else sealed.** Hold the killbox door open during raids. A closed door is a wall to raiders; with no path they attack random walls **[wiki; code]**.
   * Nothing smashable may be reachable from outside: coolers or vents set in the outer wall, lamps, solar panels. Walls, doors, sandbags, fences and conduits are not targets **[RimworldHub 1.6]**.
   * No colonist or turret may be visible from outside **[RimworldHub 1.6; code: they stop wherever they can hit a target]**.
2. **Entrance: 1 wide, at least two 90° turns, then a straight run into the box** **[wiki; Steam 2020/2022; RimworldHub 1.6]**.
   * One cell wide makes raiders arrive single file, and the doorway becomes a melee-block point.
   * Turns stop raiders seeing in.
   * Make the mouth an **L, not a T**: a T gives two raiders a corner each **[Steam Nov 2022]**.
3. **The last straight must be long enough that the cell before the last turn cannot see any shooter** **[sim, the key 1.6 point]**.
   * A raider at that corner leans into the turn cell and looks down the straight with 75% wall cover. With a straight of length L, measured from the turn cell to the doorway inclusive, it sees a cone about ±0.5·D/L cells wide at distance D.
   * **The mod's killbox has a 1-cell straight. Its corner cell sees all 4 shooters with 75–79% cover.**
   * Tested values:
     * L = 4 with shooters at least 3 cells off the axis behind plain sandbags: safe.
     * L = 5 with shooters at least 3 cells off the axis in wall pockets: safe. Their lean cells reach to 2 off the axis, which L = 4 does not cover.
     * Shooters spread across the axis: corner camper at 75% even with L = 4.
4. **Leave the doorway and the first box cells bare** **[RimworldHub 1.6; wiki banner; code]**.
   * Anything on a cell next to a raider is cover for that raider. That includes sandbags, fences, chunks, turrets, lamps and the old "T-shaped fence line at the exit".
   * A sandbag *inside the tunnel* on the corner-lean cell is still useful insurance: ×0.4 preference, and it gives no cover toward the box. Never put one on the doorway or the tile in front of it.
5. **Shooters straight across the doorway, inside a ±20–25° cone, but not on the axis itself** **[sim; RimworldHub 1.6 gives the 25° rule]**.
   * Inside 25°, the doorway walls give a raider standing in the doorway 0%. Each 15° beyond adds about 15%, and someone beside the opening gives the raider 75%.
   * Do **not** wrap a U around the entrance with shooters. The older (1.4) "U that covers the entrance from 3 sides" **[Steam 2022]** gives the doorway raider 30–75% cover against the side shooters.
   * Put side positions to use as turret decoys or melee posts instead.
   * Keep the centre ±2 cells of the line for a melee or hauler gap. That also keeps the corner camper blind.
6. **Distance = time + grenade safety.**
   * Put shooters **at least 13.5 cells** from the doorway and from the last tunnel cell. Grenades reach 12.9 and need line of sight, so a grenadier must then walk into the open before throwing.
   * Rifle lane: 14–22 cells. Shotgun room: 8–10 cells (chain shotgun 12.9), accepting grenade risk **[RimworldHub 1.6]**.
   * A healthy human crosses 15 bare cells in about 3.3 s (×1.25 in the dark).
7. **Cover type:**
   * The **pocket** (0.3) gives 69–78% against anything coming out of the doorway, and 2–4 of 4 shooters reach every cell of a 15×13 box **[sim]**.
   * A **plain sandbag line** gives 55–62%, but every shooter reaches every cell.
   * The mod's W-S-W gives 61–72% with some blind cells near the line.
8. **Double-wall the receiving end**: box side walls and the wall behind the line. Your own misses and explosives will hole them, and raiders then flood round the traps **[wiki: Defense structures]**.
9. **Hold fire until they are in the box.** Otherwise every gun shoots at the first raider in the doorway and shooting into the tunnel makes them stop in the bends **[wiki; RimworldHub 1.6]**.
10. **Melee-block door**: up to 3 brawlers just *inside* the box around the doorway exit, not in it. This is decisive against melee raids and manhunters **[wiki: Defense tactics]**.

### 1.2 Special threats

* **Grenadiers** (frag, EMP 3.5 radius, smoke): distance (see 6); shoot them first; space turrets at least 4 apart so one EMP or frag does not hit two. Breach-raid grenadiers rush ahead of their group, so pick them off **[wiki: Frag grenades]**.
* **Rocket launchers** (35.9 range): you cannot outrange them.
  * Give them decoys: turrets, animals, mechs or ghouls in front.
  * Use psychic insanity/shock lances on the rocketeer, or skipshield.
  * Space shooters at least 2 cells apart; a doomsday's 7.8 radius still hits several.
  * Each rocket that passes over a pawn has up to a 40% chance to impact early **[wiki: Defense tactics]**.
* **Smokepop packs** (raiders set them off when shot) **and smoke grenadiers**: turrets cannot lock on through any smoke cell, and pawns get ×0.7 accuracy through smoke **[wiki: Smokepop]**. Don't make turrets the main damage; colonists and melee blockers carry the fight.
* **Inferno centipedes**:
  * Stone walls, stone barricades or sandbags (never flammable), no wooden floors near the line.
  * Pre-place firefoam poppers by the line, about 1 per 2–3 shooters.
  * Make centipedes the priority target **[wiki: Defense structures]**.
  * Use high-AP guns: charge rifles, autocannon, slug turret at range.
* **Mechanoids** don't hug cover (`aiAvoidCover`) and never flee. EMP grenades on clumps at the tunnel exit; EMP IEDs in the tunnel (see 6).

### 1.3 Layouts (all checked with `kbsim.py`; top row faces the threat; legend as in the mod)

Legend: `W` wall, `S` sandbag/barricade, `f` firing position, `U` mini-turret, `T` trap, `.` open (keep bare), `_` enclosed pocket or rock, `G` gate held open.

#### K1: standard 1.6 rifle lane (1000–3000-point raids, 4–6 shooters)

```
WWWW.WWWWWWWWWWWW   outer wall, gap
W__W.WWWWW______W
W__W.....W______W   corridor; the cell left of the turn is the corner-lean cell (optional sandbag here)
W__WWWWW.W______W   straight starts (turn cell is just above)
W______W.W______W
W______W.W______W
W______W.W______W
WWWWWWWW.WWWWWWWW   doorway: last of 5 straight cells; build this wall and the box sides double
W...............W   kill zone 15 wide x 13 deep, bare floor
W...............W
W...............W
W...............W
W.......U.......W   optional mini-turret decoy on the axis, 12 cells in front of the line
W...............W
W...............W
W...............W
W.......U.......W   second decoy, 8 in front: 4 apart, at least 8 from any shooter
W...............W
W...............W
W...............W
W...............W
W.SWSWS...SWSWS.W   pockets: wall in front of each shooter, sandbags on the diagonals
W.SfSfS...SfSfS.W   shooters 3 and 5 cells off the axis, 15-16 cells from the doorway; melee gap in the middle
W...............W
```

Why it works **[sim]**:

* No cell before the turn can see a shooter.
* The straight cells see 2 shooters with 15% cover. The cell just behind the doorway sees all 4 with 15–30%. The doorway itself sees them with 0%.
* Shooters have 69–78% cover against the doorway and 2–4 of 4 reach every box cell.
* All shooters are 15+ cells from anything in the tunnel, outside grenade range.
* Decoy turrets draw fire (raiders prefer targets that are shooting at them **[wiki: Raider]**). A raider who stops beside one gets about 25–30% cover; that is the price of the decoy.

Variant: replace the pockets with a plain sandbag line (`W.SSSSS...SSSSS.W` / `W.f.f.....f.f..W`). That gives 55–62% cover, and every shooter reaches every cell.

#### K2: compact box (small raids under about 700 points; tribals; bows or shotguns)

```
WWWW.WWWWWWWWWW
W__W....W_____W
W__WWWW.W_____W
W_____W.W_____W
W_____W.W_____W
WWWWWWW.WWWWWWW   doorway (straight = 4 cells including it)
W.............W
W.............W   (traps, if any, go in the tunnel, not here)
W.............W
W.............W
W.............W
W.............W
W.............W
W.............W
W.SSSSS.SSSSS.W   plain sandbags
W.f.f.....f.f.W   shooters 3 and 5 off the axis, 10-11 cells: every shooter reaches every cell
W.............W
```

Corner and turn cells see nothing; the last tunnel cell sees 2 shooters with 15%; the doorway gives 0% **[sim]**. Shooter cover against the doorway is 56–62%. The shooters are within grenade reach (10–11 cells), which is fine against tribals, who carry no grenades **[wiki]**. Against pirates, step up to K1.

#### K3: mountain tunnel killbox

```
WWWWWW.WWWWWWWWWW   rock face; tunnel mined from the mountain edge
WWWWWWSWWWWWWWWWW   sandbags every other cell in the outer tunnel (slows; never two in a row)
WWWWWW.WWWWWWWWWW
WWWWWWSWWWWWWWWWW
WWWWWW...WWWWWWWW   turn
WWWWWWWW.WWWWWWWW   straight of 5 including the doorway
WWWWWWWW.WWWWWWWW
WWWWWWWW.WWWWWWWW
WWWWWWWW.WWWWWWWW
WWWWWWWW.WWWWWWWW   doorway
W...............W   chamber 15 x 13
...  (13 rows) ...
W.SWSWS...SWSWS.W
W.SfSfS...SfSfS.W   shooters 3 and 5 off the axis, 15-16 cells from the doorway
W...............W
```

**[sim]** Corner blind; shooter cover 69–78%.

* Under overhead mountain, no mortar shell or drop pod reaches the chamber **[wiki]**.
* **Light the chamber.** Infestation score is ×0.18 in full light, ×1 in the dark (`InfestationCellFinder` **[code 1.6]**). Hives also need at least −17 °C and favour cells deep from open sky and within 30 cells of colony buildings.
* Keep the outer tunnel dark: it is close to open sky (low infestation score), and darkness slows humanlike raiders by up to 20% **[code 1.6]**.
* Sappers ignore this box and dig. Put a reaction team plus IEDs or traps behind the rock where they tunnel **[wiki: Defense tactics, sappers]**.

#### K4: large-raid version (3000+ points)

```
WWWWWW.WWWWWWWWWWWWWW
W____W.WWWWWWWWW____W
W____W.........W____W   long approach: alternate sandbags/fences and IED pockets here
W____WWWWWWWWW.W____W
W____________W.W____W
W____________W.W____W   straight of 6 including the doorway
W____________W.W____W
W____________W.W____W
WWWWWWWWWWWWWW.WWWWWW   doorway; all walls on this end double
W...................W   kill zone 19 wide x 20 deep
...      (20 rows; decoy mini-turrets U on the axis, 8 and 12 cells in front of the line) ...
WSWSWSWS.....SWSWSWSW   pockets, 5-cell melee/hauler gap on the axis
WSfSfSfS.....SfSfSfSW   6 shooters at 4, 6 and 8 off the axis, about 22 cells from the doorway
W...................W
```

**[sim]** Only the doorway cell sees the shooters, at 0% cover. Shooter cover is 64–78%. Most cells are reached by 3–5 shooters. Turrets sit 10–14 cells from the nearest shooter.

Scaling levers, all from sources above:

* **Depth.** Seconds under fire: 22 cells is about 4.8 s for a healthy human, 6 s in the dark.
* **More shooters inside ±22°.** A second rank 2 cells behind the first is safe from friendly fire because nothing within 5 cells is intercepted.
* **A longer approach with spaced slowing objects**, so the blob arrives single file ("de-globbing") **[Steam Nov 2022/Jan 2024]**.
* **IEDs at the bunching points** before turns.
* **More decoys** (turrets, animals, mechs) for rocketeers. Psychic lances.
* **Mortars on the staging area.** Raiders that prepare stand in a group **[wiki]**.
* **A fallback line**, because at this size breach and sapper raids also come.

### 1.4 Gates, palisades and castles are small killboxes too

A plain gate in a 1-thick wall **[sim on the mod's gate]**:

* Side shooters 6 cells back at 31–60° are *not* visible from outside. That is the one good thing about the side lines.
* A raider standing in the gate gets 15–30% against them.
* Everyone is within grenade range (6.7–7.8).
* Moving the lines back into the cone exposes them to raiders camping outside the gate corner (lean + 75%).

The robust form is a gate with a baffle:

#### G1: baffled gate (perimeter gate that doubles as a mini killbox)

```
.................   outside
WWWWWW.WWWWWWWWWW   wall 2 thick; gate held open (drawn as .)
WWWWWW...WWWWWWWW   passage turns once
WWWWWWWW.WWWWWWWW   straight of 4 including the yard opening
_______W.W_______
_______W.W_______
_______W.W_______
_W.............W_   yard 13 x 9 (add 3 rows if pirates bring grenades)
_W.............W_
_W.............W_
_W.............W_
_W.............W_
_W.............W_
_W.............W_
_W.............W_
_W.............W_
_W.SSSS...SSSS.W_
_W.f.f.....f.f.W_   shooters 3 and 5 off the axis, 11-12 cells from the opening
_W.............W_
```

**[sim]** Outside corners and the passage turn see nothing; the opening gives 0%; every shooter reaches every yard cell.

#### P1: palisade or castle killing yard (Medieval Overhaul parts)

```
...............
WWWW.WWWWWWWWWW   palisade, outer gate (open)
W__W....W_____W   L passage
W__WWWW.W_____W
W_____W.W_____W
W_____W.W_____W
WWWWWWW.WWWWWWW   yard opening
W.............W   yard 13 x 7
W.............W
W.............W
W.............W
W.............W
W.............W
W.............W
WEEEEEWWWEEEEE.   back wall: runs of embrasures; inner gate in the corner (open), melee blockers behind it
_.f.f.....f.f._   archers 3 and 5 off the axis, 9.5-10 cells from the opening
```

**[sim]** Raiders get 0–30% at most. Archers sit behind embrasures (75% palisade, 85% castle) and reach the whole yard.

**The mod's gatehouse fails this test.** With both gates open, no archer can hit any cell of its yard, because the yard walls are solid. Embrasures face outward only, and raiders walk through the yard unseen **[sim]**.

Embrasures are impassable. Keep the held-open inner gate as the path, or raiders dig instead **[Steam Sep 2022]**.

---

## 2. Defense lines without a full killbox

**Best line cells [sim, table 0.3]:**

* **Pocket** (`SWS` over `SfS`): 75% straight on, 67–75% at 15–60°, 55% from the flank. Best general-purpose cell when the threat is roughly ahead.
* **W-S-W, shooter behind the S**: 55% straight on, 80% at 45°. Best when the threat comes at an angle.
* **Sandbag U**: 55–73% all round. Best for a single post that can be approached from several sides.
* **Plain sandbag line**: 50–65% within ±60°, 0% from the flank. Cheapest, full field of fire.
* **Corner peek** at a wall end: 75% straight on, falling to 30% at 45°. Good only for a narrow approach.

```
SWSWSWS     pocket line (each f has a wall in front, sandbags diagonal and beside)
SfSfSfS

WSWSWSW     mod FiringLine style (f behind S): best against threats from 30-60 degrees
.f.f.f.

 SSS        single U post            WWWW...    corner peek: shooter leans into the gap
 SfS                                    f..
```

Further rules for lines without a box:

* **Zigzag and U lines** let several shooters cover each other **[wiki: Cover]**. Don't run one long straight line that a flanker can enfilade: cap the ends with walls (the mod does this).
* **Space shooters 1 cell apart** so misses at one don't hit the next. Alternatively keep everyone within 5 cells, which avoids friendly fire entirely **[wiki: Defense tactics; code 0.4]**.
* **Clear raider cover in the field** to about 30 cells: chunks give 50%, trees 25%. Or bait it, with traps behind or a roof trap **[wiki]**.
* **Bunkers/pillboxes**: a stone room with sandbag firing holes, roofed **[wiki]**. Cons: you can be surrounded, and raiders can use them **[wiki]**. Community view **[Steam 2025]**: pillboxes are mostly a Combat Extended thing; in vanilla, chokepoints beat them.
* **Where a line faces a corridor end or a gate**, the same ±25° / off-axis rules from section 1 apply. Raiders in the opening get cover from the opening's walls against shooters at wide angles.

---

## 3. Trap corridors and slowing tunnels

* **Lane** **[wiki; RimworldHub 1.6; Steam guides]**: 2 wide. On one side, a spike trap every other cell; that is as dense as allowed, because traps may not touch, diagonals included. On the other side, **a fence beside each trap** with open cells between the fences.
  * Raiders don't know the traps, so the trap side costs them nothing and fences cost 80 → they take the trap side.
  * Colonists know every trap (path cost 800) → they take the fence side.
  * Colonists spring a known trap only 0.5% of the time **[code 1.6]**.
  * Smart, sapper and smart-breach raids know 30% of traps **[code]**. With the fence *beside* the trap, a raider who knows it must vault the fence (+80) to get round.
  * The mod's layout puts the fences beside the *empty* cells instead, so trap-aware raiders sidestep for free.
* **Slowing tunnel**: sandbags, fences or chunks alternated with empty cells, never two in a row (repeat costs are ignored) **[wiki; code]**. Put it in the outer tunnel, never in the last cells before the box **[RimworldHub 1.6]**.
* **Length**: 4–8 traps (8–16 cells) is the practical range **[community]**.
  * A big raid eats about 10 traps in seconds **[RimworldHub 1.6]**.
  * Each re-arm costs 45 material and the full work (steel 3,200; stone about 17,000–19,000).
  * Steel is the best value **[RimworldHub; wiki]**.
  * Traps are attrition for small raids and a slowdown for big ones.
  * The wiki's "too long and they go elsewhere" has no code support for normal raiders, since any path is taken. Long corridors still give them time to trash things on the way and run down the raid timer.
* **Doors for re-arming**:
  * With a fence lane, none are needed: builders work from the fence side.
  * In a 1-wide trap hall, put a door beside every 1–3 traps, opening onto a parallel service corridor **[wiki; RimworldHub]**.
  * Keep haulers out with an allowed area.
* **IEDs** (radius 3.9, 15-tick fuse, set each other off **[wiki; def]**):
  * At least 5 cells apart along the corridor unless you want a chain.
  * Place them where raiders bunch or slow: just past a sandbag, before a turn. "An IED surrounded by sandbags explodes while victims are still close" **[wiki: IED]**.
  * Keep them at least 4 cells from turrets and from walls you care about. Each blast does 200 to walls: wood dies, steel survives 1, granite survives 2.
  * Use double tunnel walls at IED points.
  * EMP IEDs against mechs and shields; incendiary IEDs on stone floors only.
* **Odyssey hunter traps** (LOS trigger within 20 cells; release a homing explosive drone) **[wiki]**: treat them like IEDs placed in the open approach, away from your walls.

---

## 4. Gates and perimeter walls

* **Close the perimeter, leave one planned way in** (the killbox or a baffled gate, held open in a raid). Double-thick walls make raiders almost always pick the open way **[RimworldHub 1.5]**.
* **Layers**: 2 layers **with no gap** against ordinary raiders who pick a random reachable wall section. With no gap, the inner layer is unreachable until the outer one breaks **[wiki; Steam 2019]**. A gap lets you repair the outer layer from inside and soaks explosions better **[wiki]**.
* **Material**: stone first (granite blocks 510 HP, non-flammable). Steel walls are flammable; wood is 195 HP and burns **[wiki; def]**.
* **Doors**:
  * Every 15–25 cells, for sorties against breachers **[wiki]**.
  * Make an airlock from 2 doors with **at least 1 cell between them**, so both are never open together against manhunters or raiders **[wiki]**.
  * A blocked door costs a pathing raider 50 + 0.2 × HP against 70 + 0.2 × HP for a wall, so raiders with no path prefer doors **[code 1.6]**.
  * Make doors stone or plasteel and watch them.
  * Panic rooms: about 4 wall layers in a built base, or at least 6 doors in a mountain, because raiders focus doors **[wiki]**.
* **Breachers** (breach axe ×7.5 against walls; frag grenadiers; termites) **[wiki; code]**:
  * You cannot steer them much with thickness (about 2 cells of walk cost per wall layer).
  * Their target is a random owned bed, so **cluster bedrooms in the core** and make the fallback line the bedroom block's wall.
  * Kill the breachers first: grenadiers work from at most 12 cells; lance or EMP termites.
  * Smart breach raids avoid turret sight, and "will avoid even isolated turrets in a side wall" **[Steam Apr 2024]**. Turrets, even unpowered ones, steer smart breachers and sappers away from walls you want kept.
  * Mechanoid breach defense **[wiki]**: double outer walls, then a 10–15-cell open band, then a wall-barricade inner line.
* **Sappers**: same bed target. Meet them where they tunnel. Unpowered turrets around the bedroom block push them elsewhere (avoid grid). IED behind the rock they are mining **[wiki]**.

---

## 5. Turret placement

* **Mini-turret**: no minimum range.
  * Cheap decoy. Raiders prefer targets that are shooting at them, and a turret that explodes hurts melee attackers around it **[wiki: Mini-turret, Defense tactics]**.
  * Spacing: **at least 4 cells mini to mini and mini to pawn**. Blast radius 3.9; a full-health steel mini dies to a neighbour's blast; plasteel or uranium minis survive one **[wiki; RimworldHub 1.6]**.
  * Sandbags in front help them (fixed in 1.4) **[wiki]**.
* **Autocannon**: cannot fire within 8.9.
  * Put the doorway and the first box cells at least 9 cells from it (ideally 12+).
  * Nothing fragile within 6 cells of its lower-left tile.
  * The cover bug means front sandbags rarely help.
  * Best automated damage beyond about 12 cells; four minis do less damage than one autocannon past 12 cells **[wiki]**.
* **Uranium slug**: 11.9–45.9 range, most accurate at 35–45, 82% armour penetration. Long lanes, open-field overwatch, anti-centipede work; not a shotgun room **[wiki; RimworldHub 1.6]**.
* **Where to put them** (sources disagree on this):
  * *Behind the line* (RimworldHub 1.6: 4 cells behind, 4 apart; no friendly fire because nothing within 5 cells is intercepted). Only works if the line is see-over. A wall-heavy or pocket line blocks most angles **[sim]**.
  * *In front of or on the axis as decoys* (Steam 2022: a "bait line 2–3 tiles in front"; K1/K4 above). They draw the rocket or the grenade, but a raider beside one gets about 25–40% cover.
  * *Wall-embedded, facing the box* (Steam 2022 critique of exactly that): narrow arcs, a hiding hole once one dies. Avoid.
* **Operations**:
  * Hold fire until raiders are inside.
  * Use a power switch or the spare-conduit "reconnect" trick to cut idle draw (80 W mini, 150 W large).
  * Solar flares, EMP and smoke shut turrets down, so arm colonists as if the turrets weren't there **[wiki]**.
  * Repair turrets first after a raid. One under 20% HP lights its fuse on the next hit **[RimworldHub 1.6]**.
* **Mortars**:
  * Separate each with walls or about 5–6 cells (4.9 blast). The wiki's 8-mortar pit has walls between all of them.
  * Shells at least 6 cells away behind a wall.
  * Minimum range about 30; cannot fire from under a roof.
  * Unassign the gunners after use **[wiki]**.
  * Sieges now start at 500 threat points (1.6).

---

## 6. Anti-mechanoid, anti-manhunter, fallback, mountain

* **Manhunters**:
  * They path to colonists and spring every trap.
  * They only attack doors when they see someone go through. Colony animals can open doors for them **[wiki: Defense tactics; Raider/Trap code]**.
  * **Melee block** a 1-wide opening with 3 brawlers and gunners behind. "10 elephants against 3 brawlers + 4 gunners" goes from a total loss to 1 downed colonist **[wiki]**. Ground-targeted grenades 1 cell past the blockers are "obscenely effective" **[wiki]**.
  * Don't melee-block boomalopes or boomrats; shoot them in the open box.
  * Waiting behind an airlock is valid.
* **Mechanoids**:
  * Normal mech raids use the killbox. Breach raids (termites) and clusters do not **[wiki]**.
  * Ranged mechs reach 27+ cells (pikeman 44.9), so tunnel geometry matters more than ever.
  * EMP stuns them (they adapt for a while afterwards). Melee-block scythers plus EMP.
  * Focus centipedes: firefoam against inferno, spread against the heavy charge blaster.
  * High-explosive mortars on slow centipedes before contact **[wiki]**.
* **Drop pods**:
  * Keep a second line inside so pods landing on the far side can't reach the beds **[wiki]**.
  * **Bait courtyard**: a powered, unroofed orbital trade beacon in a walled yard covered by the fallback line takes 40% of center drops (non-mech) **[code; wiki: Orbital trade beacon]**. Trade goods also land there; switch the beacon's power off when buying.
  * Inside, use stone shelves as interior cover, doors held open for shooting, and melee to tie up rocketeers in the 8.7 s before the pods open **[wiki]**.
* **Sappers and breachers** go for **owned beds**. The fallback line or keep belongs around the bedroom cluster, with a reaction squad **[code; wiki]**.
* **Keep/panic room**: food for 1–2 days, medicine, a joy object, materials to wall up the door. Under overhead mountain for immunity to mortars and drop pods. Best builder on the door **[wiki]**.
* **Mountain bases**: see K3.
  * Insects: light lived-in and dug areas.
  * Sappers dig the long way, so keep IED or trap pockets and a draft spot where tunnels end.
  * Drop pods and mortars can't touch overhead-mountain rooms **[wiki; code]**.

---

## 7. Low-tech (tribal/medieval)

* **Embrasures are not vanilla in 1.6.** The 1.6 security and structure list, including Odyssey, has barricade, sandbags, spike trap, IEDs, turrets, the Odyssey hunter trap, wall, doors, column, fence, fence gate and animal flap **[1.6 language-file def list]**.
* The mod's palisade and castle parts come from **Medieval Overhaul** (1.5 defs) **[def]**:

  | Part | HP | Flammability | Fill / cover |
  |---|---|---|---|
  | Palisade | 450 | 1.0 | full |
  | Palisade embrasure | 450 | 1.0 | 0.75, impassable, see-over |
  | Castle wall | 1000 | 0 | full |
  | Castle embrasure | 800 | 0 | 0.85 |
  | Gates | 250 | | |

* **Embrasure cover cuts both ways.** A raider pressed against the outside also gets 75–85% against archers further back, but only about 25–28% against the archer directly behind it (point-blank ×1/3) **[code math]**.
* **Bows**: short 22.9, recurve 25.9, greatbow 29.9, pila 18.9.
  * A compact box (archers 8–12 cells from the doorway) is enough, and tribal raiders bring **no grenades** **[wiki]**.
  * Focus fire: several archers on one raider **[wiki]**.
  * Tribal heavy archers with pila and berserkers are the dangerous ones **[wiki]**.
* **Melee blocking** is the tribal killbox: 3 pawns inside the door, one raider at a time **[Steam 2021 tribal thread; wiki]**. Corner campers shooting the blockers are the known weakness: "no good solution other than pulling back out of LoS" **[Steam 2021]**. Hence the baffle and off-axis rule.
* **Palisades burn** and raiders light wood as targets of opportunity. Keep a firebreak of stone floor or bare ground, at least 2 cells wide **[wiki]**, and move to stone as soon as possible.
* **Tribal breach raids** use breach axes (×7.5 against walls) **[wiki]**. Meet them at the wall with melee.

---

## 8. Common mistakes that make a killbox fail

1. **No open path.** A door not held open, or a gate closed by a colonist, makes raiders hit walls everywhere **[wiki; Steam 2020/2022/2023]**.
2. **A second route or a target outside**: a leaky corner, a cooler in the outer wall, lamps or solar outside the box **[RimworldHub 1.6; Steam 2023]**.
3. **A colonist or turret visible from outside the box.** Raiders stop and shoot from outside **[code; RimworldHub]**.
4. **Straight entrance**, or a last straight that is too short. The corner-lean cell then sees your line with 75% cover. The mod's killbox, small killbox and tunnel killbox all have this flaw **[sim]**.
5. **Shooters wrapped round the sides of the doorway.** This gives the doorway raider 30–75% wall cover **[sim; RimworldHub 1.6]**.
6. **Cover inside the kill zone**: sandbags, fences, chunks, plants, lamps, turrets, stools. All of it is cover for raiders **[wiki; Steam; RimworldHub]**.
7. **Exit sandbag or fence lines kept from 1.5 guides.** In 1.6 they are stop points with cover **[code; wiki banner]**.
8. **Shooters within 12.9 cells of the doorway or the last tunnel cell** while pirates bring grenades **[def; mod's own tip]**.
9. **Turrets packed together or next to colonists.** Blast 3.9 for minis, 5.9 for big turrets measured from the lower-left tile, 50% explosion chance **[def; wiki]**.
10. **Autocannon or slug turrets in a shotgun-sized box** (dead zones of 8.9 and 11.9) **[wiki]**.
11. **Single-thick, wooden or steel walls on the receiving end.** Misses and IEDs hole them **[wiki]**.
12. **Firing into the tunnel early**, which makes raiders stop in the bends **[RimworldHub 1.6]**.
13. **Expecting the box to stop sappers, breachers, drop pods, sieges, infestations or mech clusters.** It doesn't **[wiki; RimworldHub]**.
14. **Over-long trap halls with no service access**, so colonists walk the traps; and continuous sandbag or fence runs that slow only once **[wiki; code]**.

---

## 9. Template-by-template review of `Rimfolk_DefenseTemplates.xml`

| Template | What the check found | What an experienced 1.6 player would change |
|---|---|---|
| **Gate with sandbag lines** | Side lines 6 back are hidden from outside (good). The raider in the gate gets 15–30%; all shooters within grenade range (6.7–7.8). | Turn it into the **G1 baffled gate**: 2-thick wall, one turn, 4-cell straight, bare yard, sandbags straight across at 11–14 cells, shooters 3–5 off the axis, nobody on it. Keep the current version only as a low-tech or early option. |
| **Sandbag line** (behind 5) | Same geometry; 5 cells is inside grenade range and too little crossing time. | Line straight behind the opening at 10–14 cells, a 3–5-cell gap on the axis, shooters at ±3 to ±6. |
| **Wall and sandbag firing line** (behind 8) | 61–72% against the front; good against angled threats. The tip "never stand behind a wall" contradicts 1.6 code: behind a wall with see-over sides, a pawn leans out and keeps 75%. | For straight-on corridor ends, use **pockets** (`SWS`/`SfS`, 69–78%). Keep the corridor's axis cells empty; at least 13 cells back if grenadiers come. |
| **Perimeter wall** | 2-thick, no gap: right. Double gate stacked with no gap: wrong for an airlock. Inner sandbag lines have the same issue as the gate. | Gate–cell–gate airlock; use G1 for the way in; stone. Note that breachers aim at beds, so thickness buys time but does not steer them. |
| **Second wall layer** | Matches the wiki (no gap). | Fine. Optionally mention that a 1-cell gap lets you repair the outer layer from inside. |
| **Small killbox** | 1-cell straight: the corner cell sees 3 of 4 shooters at 75%. Shooters at 7–8 cells. | **K2**: 4-cell straight, plain sandbags at 10–11 cells, shooters 3 and 5 off the axis, traps moved into the tunnel. |
| **Killbox** | Corner cell sees all 4 shooters at 75–79%. 2×2 turrets 1–3 cells from shooters (blast 5.9) and a front sandbag row that cannot cover a big turret. | **K1**: straight of 5, a centred box at least 15 wide, pockets 3 and 5 off the axis at about 15 cells, mini decoys on the axis (or autocannons at least 6 from pawns and at least 9 from the doorway), double walls. |
| **Tunnel killbox** | 2-cell straight; the chamber is only 7 wide. | **K3**: straight of 5, chamber 15 wide, slowing sandbags every other cell in the outer tunnel, light the chamber. |
| **Trap corridor** | Fences sit diagonal to the traps, so trap-aware raiders (30%) sidestep for free. | A fence *beside* each trap with gaps between fences; place it as the killbox's outer tunnel; 4–8 steel traps; IEDs at least 5 apart. |
| **Turret nest** | 2×2 turrets 5 apart with the repairer 2 cells from each: inside the 5.9 blast. Front sandbags don't cover big turrets. | Minis 4 apart and 4 from pawns, or big turrets 6+ apart and 6+ from pawns. Use it as a decoy post in front of or beside the line, on a power switch. |
| **Sniper's post** | Concept fine. | Pocket cover; aim it at staging areas, siege camps and the field approach, not at the box. Make sure the approach corners can't see it at under 45 cells, or accept it, since snipers outrange most raiders. |
| **Fallback line** | Good. | Put it round the **bedroom cluster** (breacher/sapper target). Add a powered, unroofed trade-beacon bait yard it covers (40% of center drops). Stone shelves as interior cover. |
| **Keep** | Good. | About 4 wall layers if built, at least 6 doors if in a mountain; 1–2 days of food plus medicine plus joy; under overhead mountain. |
| **Mortar pit** | Mortars 3 cells apart with one wall; the shell shelf 3–4 cells from a mortar (blast 4.9). | Wall each mortar in or space them 5–6 cells; shells at least 6 away behind a wall; minimum range about 30. |
| **Palisade gatehouse** | No archer can hit any yard cell (yard walls are solid; embrasures face out). | **P1**: L passage, bare yard, back wall of embrasure runs with archers 3–5 off the axis at about 10 cells, inner gate in a corner with melee blockers, firebreak. |
| **Castle gate** | Same flaw as the palisade. | P1 in castle wall (1000 HP, non-flammable) with castle embrasures (85%). |

---

## 10. Conflicts and open questions

* **Sandbags at the exit in 1.6.** The wiki, RimworldHub and a 2025 video comment say raiders now shoot from them. A Steam tester (Jul 2025) says they "still work the same", with only staggered raiders stopping.
  * 1.6 code: both are partly right. Sandbag cells are valid stop and firing cells, but rated ×0.4 as destinations. Raiders mostly walk on, and stop there when they re-think while standing on one.
  * Since sandbags at the exit are also cover for anyone next to them, the consensus "keep the exit bare, fix the geometry" stands.
* **"Lean-out reduces wall cover"** (wiki) against the 1.6 code (cover uses the pawn's real cell). Check in game: hover a raider's shot at a colonist standing behind a wall tile with sandbags beside it. The cover line should read 75% for a straight-on shot.
* **Turrets in front of or behind the line**: genuine disagreement. Both work for different reasons (decoy against free fire from behind a see-over line).
* **"Corridor too long → raiders go elsewhere"** (wiki, ExitLag): no basis in 1.6 code for normal assaults, which take any path. Probably an artefact of trashing or timeouts.
* Exact weapon ranges for the charge rifle and charge lance differ between the def copy (1.5-era: 25.9 and 29.9) and the 1.6 wiki/RimworldHub (27.9 and about 26.9). Use 1.6 sources.
* Not verified: how often a raider walking over a sandbag re-evaluates (job expiry interval). The 0.4 weight and the "stay" check are verified.

---

## 11. Sources (with game version)

**Code and defs (local):**

* `scratchpad/defense_research/src16/rw16/` (RimWorld 1.6, decompiled; already in the folder): `Verse.AI/CastPositionFinder.cs`, `RimWorld/JobGiver_AIFightEnemy.cs`, `Verse/CoverUtility.cs`, `ShootLeanUtility.cs`, `GenSight.cs`, `Verb.cs`, `VerbUtility.cs`, `ShotReport.cs`, `RimWorld/Building_Trap.cs`, `LordJob_AssaultColony.cs`, `Verse.AI/AvoidGrid.cs`, `BreachingGrid.cs`, `RimWorld/LordToil_AssaultColonyBreaching.cs`, `GenAI.cs` (`RandomRaidDest`), `PawnsArrivalModeWorker_CenterDrop.cs`, `DropCellFinder.cs`, `InfestationCellFinder.cs`, `Verse.AI/Pawn_PathFollower.cs`, `PathGrid.cs`, `PathFinderCostTuning.cs`, `Verse/DamageWorker.cs`, `DarknessCombatUtility.cs`.
* `scratchpad/defense_research/src15/rw15/` (RimWorld 1.5, decompiled): the same AI files, for the Standable→WalkableBy comparison.
* `scratchpad/defense_research/data_dl/awl/vanilla/Data` (vanilla defs, version not stated, from GitHub AustenXiyue/RimWorldLibrary): `Buildings_Security.xml`, `Buildings_Security_Turrets.xml`, `Buildings_Structure.xml`, `Damages_Misc.xml`, `RangedIndustrialGrenades.xml`, `RangedIndustrialConsumable.xml`, `TerrainDefs`, `Stats_Pawns_General.xml`, `Races_Mechanoid.xml`, `PawnKindDefs_Humanlikes`.
* `scratchpad/defense_research/data_dl/lude/*/DefInjected/ThingDef/Buildings_Security*.xml` (1.6 including Odyssey language files): the security building list.
* `scratchpad/repos/MedievalOverhaul/1.5/Defs/ThingDefs_Buildings/Buildings_Structure.xml` (Medieval Overhaul 1.5): palisade, embrasure and gate stats.
* My checker: `Tools/DefenseSim/kbsim.py`, `cover.py`, `eval_mod.py`, `coverage.py`, `eval_designs.py`.

**RimWorld wiki** (fetched 24 Oct 2026; pages mix 1.5/1.6, several carry a 1.6-rewrite banner):

* Defense structures (cached copy) — https://rimworldwiki.com/wiki/Defense_structures
* https://rimworldwiki.com/wiki/Defense_tactics
* https://rimworldwiki.com/wiki/Sandbags (states the 1.6 change)
* https://rimworldwiki.com/wiki/Barricade
* https://rimworldwiki.com/wiki/Cover
* https://rimworldwiki.com/wiki/Spike_trap
* https://rimworldwiki.com/wiki/IED_trap
* https://rimworldwiki.com/wiki/Mini-turret
* https://rimworldwiki.com/wiki/Autocannon_turret
* https://rimworldwiki.com/wiki/Uranium_slug_turret
* https://rimworldwiki.com/wiki/Frag_grenades
* https://rimworldwiki.com/wiki/Smokepop_pack
* https://rimworldwiki.com/wiki/Raider
* https://rimworldwiki.com/wiki/Fence
* https://rimworldwiki.com/wiki/Column
* https://rimworldwiki.com/wiki/Hunter_trap (Odyssey)
* https://rimworldwiki.com/wiki/Orbital_trade_beacon
* https://rimworldwiki.com/wiki/Termite
* https://rimworldwiki.com/wiki/Breach_axe
* https://rimworldwiki.com/wiki/Granite_blocks
* https://rimworldwiki.com/wiki/Wood
* https://rimworldwiki.com/wiki/Version/1.6.4518 (1.6 patch notes: "Fix: AI casting on barricades"; "Fix: Smart raiders now correctly avoid some traps"; sieges from 500 points)

**Community:**

* RimworldHub, "Build a RimWorld Killbox That Still Works (1.6)", PC 1.6.4871, updated 7 Oct 2026 — https://rimworldhub.com/post/how-to-make-killbox-rimworld
* RimworldHub, "Stop Dying to Every Raid" (1.5) — https://rimworldhub.com/post/rimworld_raiders_defense_secrets_from_rim_veterans
* RimworldHub, AI pathing (1.5) — https://rimworldhub.com/post/ai_paths_unpacked_rimworld_routing_quirks__tactics
* RimworldHub, endgame guide (1.5) — https://rimworldhub.com/post/rimworld_endgame_got_you_stressed_beat_raiders_like_a_pro
* RimworldHub, combat guide — https://rimworldhub.com/post/combat-defense-guide (low quality; contains non-vanilla claims; not relied on)
* Steam threads:
  * "Do sandbags and barricades at kill box entrances no longer work?" (Jul 2025, 1.6) — https://steamcommunity.com/app/294100/discussions/0/544488820845974082/
  * "How can I improve my killbox?" (Nov 2022, 1.4) — https://steamcommunity.com/app/294100/discussions/0/5855270367475036108/
  * "Dumb question but…" (Jan 2024, 1.4) — https://steamcommunity.com/app/294100/discussions/0/4204741588971983401/
  * "kill box defense question" (Jun 2020, 1.1) — https://steamcommunity.com/app/294100/discussions/0/2573193951020491450/
  * "opinions on killboxes?" (Apr 2025, 1.5) — https://steamcommunity.com/app/294100/discussions/0/600775095761306617/
  * "Raids won't always go into my killbox" (Mar 2023, 1.4) — https://steamcommunity.com/app/294100/discussions/0/3792631782300626510/
  * "Dealing with raids" (Dec 2023, 1.4) — https://steamcommunity.com/app/294100/discussions/0/4032473436329015821/
  * "Kill boxes in 1.5" (Mar 2024) — https://steamcommunity.com/app/294100/discussions/0/4352239547059891849/
  * "Rookie question about mid-late game defense" (Apr 2024, 1.5) — https://steamcommunity.com/app/294100/discussions/0/6361972497307414355/
  * "Tribal melee Killboxes" (Aug 2021, 1.3) — https://steamcommunity.com/app/294100/discussions/0/3052863540904304090/
  * "having trouble with killboxes" (Sep 2022) — https://steamcommunity.com/app/294100/discussions/0/6431726046921674281/
  * "Defensive positions for raiders" (Dec 2022) — https://steamcommunity.com/app/294100/discussions/0/3720566578378647370/
  * "Multi-layered Walls – With or Without Gaps?" (May 2019, 1.0) — https://steamcommunity.com/app/294100/discussions/0/3557193237101677937/
* Steam guides:
  * "Less common Tips & Tricks" (2020–21) — https://steamcommunity.com/sharedfiles/filedetails/?id=2116556090
  * "Everything About Combat in RimWorld" (2024) — https://steamcommunity.com/sharedfiles/filedetails/?id=3154189813
  * "How to defend without a killbox" (2024) — https://steamcommunity.com/sharedfiles/filedetails/?id=3200122135
  * "[1.6] Skel's Guide" (Jul 2025) — https://steamcommunity.com/sharedfiles/filedetails/?id=2268599955
* Vortex Gaming summary of a "RimWorld 1.6: 10 tips for an efficient killbox" video (2025) — https://vortexgaming.io/en/postdetail/532858
* YouTube 1.6 killbox videos found but not readable (429): https://www.youtube.com/watch?v=d29KIYQi-_w (Odyssey 1.6, Sep 2025), https://www.youtube.com/watch?v=P3-c8WQ-uSA ("1.6 Killbox Changes"), https://www.youtube.com/watch?v=7zp8IPsAd8c (RU, "fixing killboxes in 1.6"), https://www.youtube.com/watch?v=BYdORl_BvMc
* ExitLag killbox blog (1.5, promotional; low weight) — https://www.exitlag.com/blog/rimworld-killbox/
