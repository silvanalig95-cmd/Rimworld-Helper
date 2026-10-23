# Rimfolk text pass: text that reads handcrafted

Read `docs/briefs/agents.md` first. Its git rules, commit trailers and design rules apply here. Then read `STYLE.md` in the repo root in full. It is the house style, and this brief builds on it.

This brief was written for the first round (all event text, October 2026, round 0.10.0) and is kept for the next. Where it speaks of letters and options, read it for your own kind of text: a wish line, a thought, a dream, a talk line. STYLE.md has notes for each kind.

## Why
The player loves Rimfolk's events (storyteller events, world situations, dilemmas, encounters, story chains, missions, camps, secrets). But much of the text feels machine-written: every letter has the same shape, length, rhythm and kind of opening, and the same tics. The player's words: "really look at each single text line or quest or event individually, think of what text content, what style and length and everything fits, and make it a unique and flavorful pop-up or event... so that it feels handcrafted rather than everything AI-generated following the same style and structure." The text is a large part of what this mod is.

## How to work
Go def by def, in order, through your files. For each def:
1. **Read the whole def first.**
   - when it fires: its worker class, conditions, tags, era;
   - what it actually does: its effects, flags and follow-up beats;
   - every piece of its text: label, titles, texts and their variants, summaries, options, outcomes, deeds, lines, and any keyed strings its worker uses.
   - Open the worker class in `Source/ColonyCouncil` when it isn't obvious how a text is used: whether it's a letter, a message, a tooltip, or a phrase slotted into a sentence.
2. **Ask what this moment is.**
   - Who would tell it?
   - How big is it: a passing sight, a decision with weight, a disaster, a quiet good thing?
   - What one concrete detail makes it this event and nobody else's? It might be a smell, a name, an object, the weather, what someone said, or a number.
3. **Pick the form the moment calls for**, then write it. Keep what is already plain, specific and distinct; rewriting isn't the goal, reading well is. Every line gets a real look, though, and most need at least some work.
4. **Read the def back as the player will see it,** with placeholders filled in your head. It should hold up across the different colonists, factions, places and eras it can fill in.
5. **Commit after each file or two,** so progress is never lost.

## The palette: shape, voice and length
Most letters are still plain third-person narration in RimWorld's dry voice. But they should not all be the same paragraph. Choose per event:
- **Length follows weight.**
  - A passing encounter or small good thing: one to three sentences.
  - A situation opening or a hard decision: a full paragraph or two.
  - Aftermath and outcome lines: often a single sentence.
  - Mix lengths within your files deliberately.
- **Openings vary.** Never let a run of events start the same way ("Word reaches the colony...", "A rider brings word...", "{A_nameDef} has..."). Start in the middle of things, with a sound, a quote, a name, an object, the weather, a number, or what someone is doing.
- **Someone's own voice, where the fiction gives a reason.** Quote them in their own idiom:
  - a note pinned to the gate;
  - a short letter signed by a faction leader;
  - a trader's hurried account;
  - a child's message carried by a runner;
  - a farmer's complaint;
  - an officer's clipped formal summons.

  A tribal speaker, a medieval reeve, an outlander merchant, an imperial envoy and a pirate all sound different. Use this sparingly: perhaps one event in five or six, never as a gimmick.
- **Reported speech or rumour:** "The trader swore it was true, and twice offered to bet on it."
- **A list, tally or bill** when the event is about counting: losses, a manifest, terms of a deal. Rarely.
- **Two lines of dialogue** between colonists, or a visitor and a colonist. Rarely.
- **A sign, a carved line, a verse of a work song, a proverb of the place.** Only when it truly belongs.
- **Tone follows the event.**
  - Threats are terse and serious.
  - Small comedies may be light.
  - Grief is plain.
  - Good fortune is understated.
  - Strange things are told flatly and left strange.

## Options and outcomes
- **Option labels stay short, clear and true to what the button does.** Keep every amount, cost and name in them exactly as now ("Send a gift ({COUNT} silver)", "Sell them timber (100 wood)"); the player decides with them. A label may carry a little voice ("Tell them no", "Let them stay the night") but clarity comes first. Don't make labels longer.
- **An outcome says what happened, specifically, in the event's voice.** Don't restate the option. Vary what it shows: the consequence, someone's reaction, an image, what was said. Never a moral, never a summary of feelings.
- **Variants of one text must differ in substance:** a different detail, angle or voice, not the same sentence reworded.

## Goods and people on the map
A parallel change makes goods move physically. When the colony gives goods to outsiders, or receives silver, goods or gifts from them, real people now come when possible: a runner, porters, a cart driver or a courier with a pack animal walks in to collect or deliver, then leaves. Write outcomes and texts accordingly:
- Good: "Their porters came for the corn at dusk", "The silver will come back with their cart driver."
- Bad: "the corn vanishes", "silver appears in the stockpile".
- Don't promise specifics the code can't guarantee: a named driver, an exact hour, a particular animal.

If the text says someone arrived, write it so it still holds if that person isn't on the map. That is the fallback when it's not possible to send them.

## Avoid
Everything in STYLE.md, especially the punchline reflex, colon reveals, "Nobody ...", tidy morals and aphorisms. Also:
- **Three-item lists as a habit:** "tools, timber, cloth".
- **"…, and nothing else."** endings.
- **"It's not X, it's Y."**
- **Stock words:** "the weight of", "a testament", "echo", "whisper(s)", "tapestry", "palpable", "bustling", "little by little", "for now" as a closer, "a small kindness".
- **Rhetorical questions** to close.
- **A neat summary sentence** at the end of every text.
- **Explaining feelings** instead of showing what people do and say.
- **Monotone rhythm.** Vary sentence length; a fragment is fine now and then.
- **Fancy or purple words, modern slang, real-world references, fourth-wall jokes.**
- **Em dashes** (none, per STYLE.md).
- **More than one "!"**, and only in speech where someone would really shout.
- **American spelling,** as the game uses it.

Keep the era: [lowtech] text never mentions power, radios, steel or other worlds. Keep the biome, season and tags true.

## Hard rules (the code depends on them)
- **Change only text:**
  - label, title(s), text(s), summaries, description, outcome, deed, line(s), letter and message strings;
  - the keyed values your slice lists.

  Never change defNames, Class attributes, effects, flags, numbers, conditions, tags, key names, XML structure or element order.
- **Keep the leading tag block of each line** ([winter], [theirLowtech], [trait:X]) exactly. You may ADD variants to a list, but only with tags that already occur in that list or file. Never remove a variant: lists keep at least as many entries.
- **Keep placeholders.** A line uses only the people and values it used before ({PLACE}, {FACTION}, {COUNT}, {TALE}, {A_nameDef}, {X_indefinite}, {0}, \n). A different form of the same person is fine. Never start a sentence with a lower-case placeholder (pronouns, {X_definite}).
- **Respect grammar frames.**
  - A `<deed>` is slotted into a sentence about the colony ("…{deed}"); check how its worker uses it and keep it a fitting phrase.
  - Def labels are lower case unless a proper noun.
  - Titles are short.
  - Phrases built into a sentence by code must still fit their frame.
- **Stay inside your files** and the keyed prefixes you were given.
  - Don't touch: C#, other Defs, README, ROADMAP, DESIGN, STYLE, the Almanac, Almanac_WhatsNew.xml, CHANGELOG.md, About.xml.
  - The one exception is Tools/playout_skips.txt, if you must (see Validation).

## Validation (all before your final commit)
- `python3 Tools/textcheck.py --base <the commit before the round began>` prints no ERROR line for your files. Read its tic report too: the counts should go down, not up.
- DefValidator: 0 errors. Build it first with `cd Tools/DefValidator && dotnet build -c Release`, then run it as agent_brief.md says.
- `python3 Tools/labelcheck.py` prints 0.
- `cd Tools/Tests && timeout 900 dotnet run -c Release`: all pass. Some tests read text; fix your text, not the tests, unless a test pins a literal wording you changed on purpose.
- `python3 Tools/playout_audit.py --candidates` lists no new candidate from your files. A new wording that describes people doing something on the map without a play-out shows up there. Rephrase it, or if it's right as it is, add a line to Tools/playout_skips.txt in its format and say so in your report.
- `python3 Tools/variety_report.py --top` (and `--filter`) catches openings and phrases repeated across your files. Fix the repeats.
- You change no C#, so no DLL rebuild is needed. Don't commit a rebuilt DLL.

## Report back
- **Counts:** defs reviewed, and lines rewritten, added and kept.
- **Six before/after examples** that show the range of forms you used.
- **Things that could happen on the map but are only told,** each with its defName and what could be shown: a visitor, a messenger, porters, an animal handed over, a burial, a fire, a building. Don't implement these; list them.
- **Text that contradicted the mechanics,** and what you did about it.
- **Your commit hashes.**

## Learned in the first round (apply these too)
- Outcomes show at once, or when a play-out ends: never describe days passing ("a week later", "by morning").
- When time runs out the game prefixes "The council never answered.": a default outcome mustn't say the council ruled.
- Dilemmas never fill in {PLACE}: name the place in words.
- Say where gifts and silver really land (read the effect), or name no place.
- Don't put people, animals, carts or vehicles on the map that the code doesn't spawn; couriers and visitors do come now (Couriers/, Visitors/), so check what an effect really does.
- No sentence may start with a lowercase placeholder or a lowercase name ({TALE} of an illness, a world event's label).
- Tags that can never be true for the event (a season it doesn't fire in) make a line that never shows: report them.
- Keep your helper scripts in your own scratchpad folder; shared names get overwritten by the other writers.

## Learned in round two
- Speech bubbles, and the news line shown beside a quote, are cut at the first sentence (`TextUtil.ShortSpeech`). Find out per pool whether anything after the first sentence is ever shown; if not, say the whole point in that sentence.
- Don't assume a thing is there: most wishes aren't objects, a grave isn't dug yet at the moment of death, and children, a fight or a wall may not exist. Lines must hold for every case the tag allows (an [annals:peaceful] year may still have had one raid).
- Letters arrive at any hour: "woke to" or "this morning" only where the code fires in the morning. Journals and dreams go by the hour of the moment they tell.
- Default day names and ambition labels are lower case: never open a sentence on {NAME} or a quoted "{AMBITION}".
- Write from the side of the one who writes: a memory given only to the recipient is told by the recipient.
