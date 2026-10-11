# How Rimfolk writes

Rimfolk's text should read like it shipped with RimWorld: the same dry, plain, observant voice, about the same
colonists, in the same world. It should never read like a writing exercise. This guide is for anyone adding or
editing text (journals, wishes, thoughts, letters, dreams, moments, the Almanac, tooltips).

## The one-line version

**Plain, specific and varied. Say the true thing about this colony in as few words as it takes. A joke is the
exception, never the shape of every line.**

## What RimWorld's own text sounds like

- Thoughts: `ate without table` / "I had to eat food off the floor. I prefer having a table." `slept in the cold` /
  "It was too cold to sleep well." `my room is impressive` / "My bedroom is fantastic. I feel like a king."
- Letters: "A group of raiders from X have arrived from the east. They are going to attack." "X's crop has been
  destroyed by blight."
- Social log: "Ana and Ruth chatted about flowers." "Bob insulted Ruth's mother."
- Backstories: matter-of-fact, a little dark, concrete ("She was a farmer's daughter until the raiders came.").

Mostly flat statements. The humor is in the situation and the occasional dry aside, not in every sentence.

## What makes text read machine-made (and what to do instead)

1. **The punchline reflex.** Setup sentence, then a deadpan twist: "Some people twice." "The trader did not find
   this funny." "Then they got bored and left." "I didn't touch the present." One of these is charming; a pool full
   of them is a tic. **Most lines get no twist.** A second sentence should add a fact or a feeling, not a gag.
2. **Nobody / everyone / something.** "Nobody looks very proud." "Everyone was happy." "Something about it." Use the
   actual person or thing. Don't end lines on "Nobody ...".
3. **Same opening, same shape.** 514 dreams began "I dreamt". Many wishes begin "I'd like" / "I want" / "I'm going
   to". Within one pool, vary how lines start and how long they are: a three-word line next to a two-sentence one.
4. **Exclamation runs.** "I've cleared a spot for the art! I dust it every day! It's a very clean spot!" At most
   one "!" in a line, and only where a person would really raise their voice (cheerful lines, children, a shout).
5. **Colon reveals.** "Their message was short: 400 silver." Fine now and then; not as a habit.
6. **Fragment stacks.** "The ugly chair. The crooked wall. Even them." Once in a while, not as a default rhythm.
7. **Tidy morals and therapy talk.** "It hurt, and then it helped." "That's the whole point." "Turns out it goes
   both ways." Let the event carry the meaning. People in a colony don't summarize their own growth.
8. **Aphorisms.** "Factions forget favors fast and grudges slowly." Sounds clever, says little. Say the concrete
   thing: "The traders from Kett still remember we sold them rotten meat."
9. **Stock tics.** Avoid or use rarely: *in front of everyone, half the colony, the whole colony, at the edge of,
   by the fire, for an hour, a little, somehow, quietly, pretends, fools nobody, not unkindly, to prove a point,
   for a very long time, at once, honestly, turns out, for the first time, it's fine. it's fine.*
10. **Over-explaining.** Trust the player. Don't add a sentence that explains the line before it.

## Doing it well

- **Be specific and physical.** Name the stew, the cooler, the north wall, the muffalo, the cracked window, the
  night shift, the rifle with the bent sight. One concrete detail does more than three adjectives.
- **Let people be people.** Colonists are tired, petty, kind, scared, bored, proud. They talk about food, sleep,
  work, each other, the cold. They don't perform.
- **Make the tones different voices.** Lines are grouped by tone:
  - *plain*: practical, neutral, says what's needed.
  - *warm*: kind and grateful, never syrupy.
  - *blunt*: short, direct, impatient. Often one sentence.
  - *cheerful*: upbeat, a bit silly, may use one "!". Not manic.
  - *gloomy*: tired, fatalistic, dry. Not melodramatic.
  - *cold*: detached, transactional, a little unsettling.
- **Keep the situation.** A line belongs to its wish, thought, event and tags. A `[winter]` line is about winter;
  a `[trait:Greedy]` line sounds greedy; an `[afterraid]` line is said the day after a raid.
- **Respect the era.** `[lowtech]` lines (tribal, medieval) never mention power, comms, steel or other worlds.
  Avoid modern idioms everywhere ("vibe", "processing", "waiting room", "okay" in a medieval mouth).
- **Keep what works.** A line that is already plain, specific and true stays. Rewriting isn't the goal; reading
  well is.
- **Don't flatten.** Cut the reflexive gag, not all humor. Specific humor that comes out of the situation stays:
  "I've counted the snores in that barracks. There are eleven distinct snores." About one line in four can carry a
  light touch (an odd concrete detail, a dry aside). A plain line still needs a concrete detail; "I'd show it to
  anyone who asked" is too generic. Avoid stiff connectors in speech ("yet", "thus", "however").

## By kind of text

- **Journal lines** (first person, private): the most personal text. Short entries, concrete, uneven. Not every
  entry has a point. "Stew again. Ate it anyway." is a good journal line.
- **Wish lines** (a colonist asking the council): requests, thanks, disappointment, reminders. Vary the way people
  ask: some hint, some demand, some bargain, some just complain. Thanks can be one word.
- **Thoughts**: `label` lowercase, a short phrase like vanilla's (`slept in the barracks`, `a feast for us`). The
  description is one or two short first-person sentences.
- **Dreams**: told on waking, first person. Dreams are odd and vivid; vary how they're told ("Dreamt about...",
  "In the dream...", "Last night...", a bare image). Don't end each with a joke about waking up.
- **Moments and the social log**: third person, one sentence, vanilla style. "[INITIATOR_nameDef] showed
  [RECIPIENT_nameDef] how to splice a rope." No twist ending.
- **Letters and events** (world events, dilemmas, scenarios): third person, factual. What happened, who it touches,
  what's at stake, what the choices are. Short paragraphs. A dry touch is fine; no gags in threat letters.
  Option labels are short and plain; outcomes say what happened.
- **Explanations** (Almanac, tooltips, settings, advisor tips): clear second person, concrete numbers, the same
  names the interface uses. No quips tacked on ("Most days."), no slogans, no rhetorical questions.

## Before and after

| Before | After |
|---|---|
| I've cleared a spot for the art! I dust the spot every day! It's a very clean spot! | I've cleared a spot on the wall for a painting. It's been empty a while now. |
| {A_nameDef} stood perfectly still in the long grass while {X_indefinite} came right up and sniffed {A_possessive} hair. {A_nameDef} has told everyone. Some people twice. | {X_indefinite} came right up to {A_nameDef} in the long grass and sniffed {A_possessive} hair before wandering off. |
| I dreamt the stars came down to listen to us talk. Then they got bored and left. | The stars came down low in my dream, close enough to touch, and they were listening. |
| The council sided with {B_nameDef}. {A_nameDef} is fuming. | The council sided with {B_nameDef}. {A_nameDef} took it badly. |
| Factions forget favors fast and grudges slowly. Better start piling up favors now. | Gifts and help now will pay off later, when we need someone to answer a call for help. |
| We talked about the ones we lost. It hurt, and then it helped. | We talked about the ones we lost, late into the night. |
| A cabinet full of the right tools beats a shouting foreman. Most days. | A tool cabinet next to a workbench speeds up work there by 6%. |
| Didn't happen! That's fine! It's fine. It's fine. | It didn't happen. I'm trying not to let it bother me. |

## Spelling and mechanics

- American spelling, like the game: color, honor, armor, gray, traveler, realize.
- No em dashes; use commas, colons, parentheses or a new sentence.
- Placeholders stay exactly as they are: `{A_nameDef}`, `{OTHER_pronoun}`, `[INITIATOR_nameDef]`, `{0}`. A line may
  use a different form of a person it already names (`{A_nameDef}` → `{A_pronoun}`), never someone new.
- Leading tags (`[winter]`, `[trait:Kind]`, `[!lowtech]`) stay exactly as they are.
- After editing, run `python3 Tools/textcheck.py` (structure, tags and placeholders) and the DefValidator.
