# Colony Council: Wishes & Plans

A RimWorld 1.6 mod. **The colony tells you what it needs. Your colonists tell you what they want. You decide.**

![preview](About/Preview.png)

No other mods are required, and it works with any combination of DLCs (or none).

## Installing

1. Download this repository as a ZIP (green **Code** button → **Download ZIP**) and extract it.
2. Move the extracted folder into your RimWorld `Mods` folder and name it `ColonyCouncil`:
   - Steam on Windows: `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\ColonyCouncil`
   - Mac: right-click RimWorld.app → Show Package Contents → `Mods/ColonyCouncil`
3. Start RimWorld, open **Mods**, enable **Colony Council: Wishes & Plans** and restart.

The folder must contain `About/`, `1.6/`, `Defs/`, `Languages/` and `Textures/` directly (not nested
one level deeper). It is safe to add to an existing save. Removing it from a running save logs some
one-time errors about missing classes. They're harmless.

## What you get

A new **Council** button in the bottom bar opens five tabs:

### Priorities: what the colony needs
A ranked list of concerns, each with a short explanation and a rotating **"why it matters"** tip,
so you learn the game's logic while playing:
- food vs. the coming winter (forecast from your map's actual climate)
- spoiling food, cold or hot months ahead, warm clothes
- defences vs. the raids your wealth attracts (a rough estimate, and it says so)
- beds and barracks, a place to eat, recreation variety, mood breaks coming
- doctor, medicine and medical beds; power shortages, solar-only grids
- jobs nobody is assigned to, items rotting outside, an idle research bench
- ideas for where to head next when nothing's on fire

Urgent ones also show up in the alerts on the right. **Show me** jumps to the problem;
**Plan the room** starts the room planner with a matching template.

### Wishes: what your colonists want
Every couple of days a colonist voices a wish, with a letter, a line above their head, and
grumbling to others in the social log. Wishes come from who they are:

| Wish | Who asks |
|---|---|
| A room of their own, a bigger or more impressive bedroom, furniture, a light, a plant | barracks sleepers, greedy, jealous, night owls, rising expectations |
| To share a bed with their partner | couples who sleep apart (the partner may back it) |
| A workshop, kitchen, laboratory, hospital, greenhouse or barn | colonists with a passion for that skill |
| To do the work they love | passionate colonists whose work type is switched off |
| A proper burial for a friend | when someone they cared about lies unburied |
| A prosthetic | missing a limb or eye, when one can be made or is in stock |
| A decent weapon | fighters, and brawlers stuck holding a gun |
| Better food, a freezer, a brewery | gourmands, cooks, chemical interest |
| New clothes, warmth at night, a table to eat at | tattered clothes, cold nights, eating off the floor |
| Something new to do for fun, art in the common rooms | bored colonists, artists |
| An animal companion | animal lovers, when tameable animals roam nearby |
| Stronger defences | fighters, and colonists shaken by violence |
| A room befitting their title | Royalty nobles |
| Somewhere to learn and play | Biotech children |

Each colonist speaks in their own **tone** (warm, blunt, cheerful, gloomy, cold or plain),
based on traits like Kind, Abrasive, Psychopath, Sanguine/Depressive and their mood.
- **Kind** colonists sometimes ask on someone else's behalf. If granted, the other colonist
  likes them more.
- **Ascetics** rarely ask for luxuries.
- When several colonists want the same thing, it becomes a **petition**.

Granting a wish gives a mood boost (sometimes an inspiration). Wishes check themselves
automatically: build the room, assign the bed, enable the work type, and it's done.
**Not now** sets a wish aside without penalty. Can't wait? **Listen to the colonists** asks
around once a day.

### Ambition: council meetings
About once a quadrum the colonists meet (around the dining table or the campfire) and three
of them each pitch an ambition: *a full larder*, *a real hospital*, *hold the line*, *teeth in
the mud*, *a great hall*, *the menagerie*, and more. Pick one (or none). It shows as a checklist
with progress and a deadline. Achieving it lifts everyone's mood, the proposer most of all.
You can also call a meeting yourself.

### Room plans
13 ready-made rooms: bedroom, couple's bedroom, dining room, rec room, workshop, kitchen,
laboratory, hospital, freezer, storeroom, barn, greenhouse, crypt.
- Click **Plan** and a colour-coded preview follows your mouse:
  - green: new walls
  - blue: existing walls it reuses
  - red: blocked cells
  - yellow: rock to mine
- **Q / E** rotate the room, the panel lets you change the wall material, and
  **Find a good spot** searches near your colony for a cheap, close spot that shares walls.
- It places ordinary blueprints, so your builders handle the rest.

Colonists also sometimes add **personal touches**: a plant pot, an end table or a lamp planned for
their own bedroom. Cancel the blueprint if you don't want it; if you build it, they're pleased.

### Chronicle
A running history of wishes, petitions, meetings and ambitions.

## Fits your world (medieval runs too)

The council only suggests what your colony can actually do right now:
- **Research and tech level:** nothing that needs unresearched tech, and nothing electric before Electricity.
- **Purpose, not names:** buildings are recognised by what they do. A medieval colony gets
  torch lamps, campfires, spike traps and bedrolls; an industrial one gets standing lamps,
  heaters and turrets. Modded furniture that does the same job counts too.
- **Materials:** room plans use the wall material you have the most of.
- **What's on the map:** animals to tame, a body to bury, a prosthetic you can make.
- **Ambitions:** only ones whose every goal is achievable for your colony are offered.

## Settings

**Options → Mod options → Colony Council**:
- turn each part (advisor, wishes, meetings, personal touches) on or off
- **Stakes**:
  - **Gentle** (default): ignoring wishes costs nothing
  - **Demanding**: expired wishes and failed ambitions sting a little
  - **Advisor only**: no mood effects at all
- wish frequency, how many can be open at once, how long they last
- reward strength, meeting interval and ambition deadlines
- letters vs. messages, floating speech text, alerts

## Optional mods that go well with it

- **Interaction Bubbles** by Jaxe (search the Steam Workshop):
  shows social-log conversations as speech bubbles. Your colonists grumbling about their wishes
  then appears above their heads. Purely visual; nothing needs configuring.

## Status and feedback

Version 0.1. It compiles against RimWorld 1.6, and all XML is checked against the real game
types by the included validator. It has **not been play-tested** in a live game yet. If
something misbehaves, open the debug log (Options → Development mode, then the log button):
- lines starting with `[Colony Council]` are from this mod
- please share them so they can be fixed.

## For modders

- `Source/ColonyCouncil/`: the C# code. Build with `dotnet build -c Release` (needs the .NET SDK;
  the RimWorld reference assemblies come from NuGet).
- `Tools/DefValidator/`: checks every XML def and translation key against the compiled game
  types. Run it with:
  ```
  dotnet run --project Tools/DefValidator -- . ~/.nuget/packages/krafs.rimworld.ref/<version>/ref/net472 1.6/Assemblies/ColonyCouncil.dll
  ```
- `Tools/Tests/`: tests for the room-rotation math (`dotnet run --project Tools/Tests`).
- Wishes, advice checks, ambitions and room templates are all data-driven. Add new ones in XML,
  with a small worker class where needed. See [DESIGN.md](DESIGN.md) for the design.
