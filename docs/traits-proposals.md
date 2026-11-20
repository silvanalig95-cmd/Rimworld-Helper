# More traits that come and go: proposals (19 November 2026)

The player, 17 November 2026: "while i generally like the idea of traits emerging and/or disappearing (we shouild also
build up on this as to do)"; and 19 November 2026, on the plan to write a list first and let the player pick: "More
trades come and go sounds great". Pick by number (or strike and change any of it), and the picked ones are built
together. The player's go of 20 November 2026, "go ahead with the next tasks, including the traits": the lead's first
batch (below) is built, not released yet, each item marked **Built**; the same day, "go ahead with all the traits": the
other 26 follow in two more builds (the ways out 3, 7 to 9, 11 to 13, 15, 17 to 25; the new traits 28 to 36). DESIGN.md,
"More traits that come and go, the first batch", has every rule as built and the parts the later builds reuse; the
second batch (the ways out, built the same day, not released yet, each marked **Built**) is in "More traits that come
and go, the other ways out".

## Today

Rimfolk's trait changes (`Source/ColonyCouncil/Persona/TraitChanges.cs`, `TraitChangeMath.cs`,
`Defs/PersonaDefs/Rimfolk_TraitChanges_*.xml`; DESIGN.md "Traits that change") have 53 rules: 30 give a trait (9 of them
bad ones), 7 take a bad one away (2 of them vengeful's ways out, 0.265.4) and 16 move a trait one step along its scale.
In the player's save, 3 changes came in 124 days, all of them gains.

A rule asks for some of these, all of which already exist: **marks** (what happened to them, such as "held the line",
"carried someone to safety", "a long peace", "lost the vote"), how far their **sides** (warmth, trust, pride, temper,
nerve, outlook, openness) have moved from their nature, **deeds** counted on separate days, the game's **records** (kills,
times tending others), **shares of their time** over 60 days (at recreation, under a roof), **calm days** without a
breakdown, **recent hours** at a kind of work or at night, a **habit** they have lived with, a **vow** of vengeance.
Once everything holds, the rule rolls its chance each day. Only one change happens in the colony every 10 days, one per
person every 60 days (not for taking away a trait Rimfolk gave), and the same trait in a person waits its rule's gap
(90 to 150 days).

How rare, below, is the chance a day once everything holds: **rare** 2 to 4% (a month or more), **uncommon** 5 to 8%
(two to three weeks), **common** 10 to 15% (about a week). Getting everything to hold takes most of the time.

## The balance aim

- **Many more ways out of bad traits**: a way out of each bad trait Rimfolk can give (today only vengeful has one, and
  the steps along a scale), and of the bad traits colonists often arrive with, the game's and the player's mods'.
- **Good traits from good things**: courage, care, skill and a settled life bring more good traits.
- **A few new traits from the player's mods**, for flavour, mostly neither good nor bad.
- **Harm at the game's own rate**: no new bad trait is proposed. The three new ones that come from hard times
  (desensitized, drunken master, occultist) are neither good nor bad, come only from bad things that already happened
  on the map, and are rare.

Mark names in brackets are the existing families (`Rimfolk_PersonaMarks_*.xml`); "new" says where a rule needs a small
new part.

## A. A way out of each bad trait Rimfolk can give

1. **Jealous fades** (any jealous, not only Rimfolk's): given a seat (Elected), or two promises kept to them
   (PromiseKept); no seat or vote lost in the last 120 days; trust back to where it was or higher. Uncommon (8%). It came
   from losing votes, so winning one or being treated fairly takes the sting out. **Built** (20 November 2026, not released yet). Two rules, a seat or two promises kept; "trust back" read as trust moved 0 or more from their nature.
2. **Bloodlust Rimfolk gave fades**: 120 days with no fight mark (HeldTheLine, RaidsHardened, DownedHardened,
   SworeVengeance), a long peace (LongPeace), warmth back near where it was. Uncommon (5%). It came from a run of hard
   fights, so a long quiet lets it go, as vengeful already does. **Built** (20 November 2026, not released yet). "Warmth back near" read as warmth moved -5 or more.
3. **Psychopath Rimfolk gave fades**: 180 days with no execution or organ taken (new: a deed count that must be zero),
   three marks of care (SavedSomeone, TaughtWell, ForgivenBy, Kindness), warmth up 15. Rare (2%), once per person. The
   one trait Rimfolk gives from cruelty gets a slow way back through a long stretch of decent life. **Built** (20 November 2026, the second batch, not released yet). The zero count is the gain's own count of executions and organs taken; "once per person" is once in a colony's life, as the psychopath itself.
4. **Cannibal Rimfolk gave fades**: from famine, after 180 days without eating human flesh (new: a zero count) and no
   hungry season in 120 days, uncommon (6%); from a faith, once they take up a faith that does not call for it
   (Converted), uncommon (8%). What hunger or a faith brought goes when the hunger or the faith does. **Built** (20 November 2026, not released yet). Each way for the cannibal its own rule gave; the zero count is a tally with `noneDays`, from the last meal or from when it came.
5. **World-weary lifts** (Vanilla Traits Expanded, any): two long peaces (LongPeace) and one good mark (MovingRitual,
   Mastered, Legendary, Freed, AddictionOvercome, Elected, TaughtWell), no loss in 60 days, outlook back near where it
   was. Uncommon (6%). It came from losses and hard years; good years bring them back. **Built** (20 November 2026, not released yet). The two long peaces are counted as they come (a long peace leaves the page before the next one comes); "outlook back near" is outlook moved -5 or more.
6. **Rebel settles** (Vanilla Traits Expanded, any): two promises kept (PromiseKept) or a seat given (Elected), no broken
   promise, lost seat or lost vote in 120 days, trust back to where it was. Uncommon (8%). It came from broken promises;
   kept ones win them back. **Built** (20 November 2026, not released yet). Two rules, as jealous.
7. **Workaholic eases** (Vanilla Traits Expanded; neither good nor bad, but it costs them their rest): 60 days with an
   eighth of their time at recreation and a long peace. Uncommon (6%). It came from a season without rest; a season of
   rest undoes it. **Built** (20 November 2026, the second batch, not released yet).
8. **Recluse opens up** (Biotech; neither good nor bad, but they want few people around): a friend on the Bonds page
   (new: a friendship condition) and two of Kindness, SavedBy, NursedBack, ForgivenBy, trust back near where it was.
   Uncommon (6%). It came from heartbreak and betrayal; a friend and some kindness bring them back. **Built** (20 November 2026, the second batch, not released yet). A friend is an announced friendship on the Bonds page with someone living; "trust back near" is trust moved -5 or more.

## B. Ways out of bad traits colonists often arrive with (the game's)

9. **Slothful to lazy** (the hard-work rule from slothful too): 60 days with less than a twelfth of their time at
   recreation and one of Masterwork, Mastered, TaughtWell. Uncommon (6%). The step after that is the existing lazy to
   none. **Built** (20 November 2026, the second batch, not released yet). Its own rule with these numbers (8.3% of their time at most, the mark within 120 days as the hard-work rule asks), rather than slothful added to the hard-work rule (4%, pride, a 120-day gap).
10. **Pyromaniac fades**: 120 calm days, 60 recent hours at the stove or the forge (Cooking or Smithing), and a
    masterwork or a mastered skill. Rare (3%). The urge put to use, with no fire-starting break in a long while. **Built** (20 November 2026, not released yet). The hours at the stove and the forge are added together.
11. **Gourmand eases**: a hungry season shared with the colony (HungrySeason), then 60 days without a food binge, pride
    down 3. Uncommon (5%). Having gone hungry with everyone, they learn to make do. A good outcome of a bad thing that
    happened anyway. **Built** (20 November 2026, the second batch, not released yet). The hungry season is the mark for going hungry (malnourished), counted as it comes; the 60 days without a food binge start at it.
12. **Bloodlust they came with fades** (the slower twin of 2): 240 days with no fight mark, two long peaces, and one of
    TaughtWell, Kindness, NursedBack, warmth up 10. Rare (2%). **Built** (20 November 2026, the second batch, not released yet). Only for a bloodlust Rimfolk didn't give (2 is for the one it gave); the 240 days are counted from the fight marks as they come and on their page, never from before they joined.
13. **Very neurotic to neurotic**: 90 calm days, a long peace, nerve up 5. Uncommon (6%). The worst of the worrying eases
    in a safe stretch; neurotic's quicker work stays. **Built** (20 November 2026, the second batch, not released yet).
14. **Talking it through** (Therapy (Continued), `orion.therapy`, active in the player's list): four therapy sessions
    in 90 days (its `TherapyRelieved` memory, read by name) count as a good mark toward the ways up for nerves (volatile
    to nervous, nervous to none), mood (depressive to pessimist, pessimist to none), anxious (15) and mood swings (25).
    A soft integration: nothing without the mod. **Built** (20 November 2026, not released yet). Four rules beside the ways up for nerves and mood, the sessions in place of one mark; anxious (15) and mood swings (25) come with their own rules.

## C. Ways out of bad traits from the player's mods

15. **Anxious calms** (Vanilla Traits Expanded): 90 calm days and held the line once or two long peaces, nerve up 8.
    Uncommon (8%). **Built** (20 November 2026, the second batch, not released yet). Three rules: held the line, two long peaces, or (14) four therapy sessions in 90 days.
16. **Lush and stoner dry out** (Vanilla Traits Expanded): an addiction overcome in the last 120 days
    (AddictionOvercome), as the existing teetotal rule. Common (15%). **Built** (20 November 2026, not released yet).
17. **Squeamish hardens** (Vanilla Traits Expanded): 40 recent hours as a doctor and 30 times tending others (the game's
    record). Uncommon (8%). **Built** (20 November 2026, the second batch, not released yet). The nursing mark of 28 joins it with the new traits' build.
18. **Kleptomaniac reformed** (Vanilla Traits Expanded): forgiven (ForgivenBy) or given a seat (Elected), 120 calm days,
    trust up 5. Rare (4%). Trusted, they stop taking. **Built** (20 November 2026, the second batch, not released yet). Two rules.
19. **Slob tidies up** (Vanilla Traits Expanded's slob; Dubs Bad Hygiene's slob and unhygienic, one step up): 40 recent
    hours cleaning, or the tidy habit lived with for 60 days. Common (10%). The existing tidy trait can then come from
    the habit. **Built** (20 November 2026, the second batch, not released yet). Four rules (each mod's, cleaning or the habit); the habit held 60 days and lived on 40, and kept; Dubs Bad Hygiene's degrees read as slob -2 and unhygienic -1.
20. **Animal hater softens** (Vanilla Traits Expanded): an animal bonded to them, three animals tamed (the game's
    record), or the habit of naming the animals. Uncommon (6%). **Built** (20 November 2026, the second batch, not released yet). Three rules; the habit as the rule frame holds one (90 days, lived on 60), and kept.
21. **Submissive stands up** (Vanilla Traits Expanded): given a seat or held the line twice, pride up 8. Uncommon (8%). **Built** (20 November 2026, the second batch, not released yet). Two rules.
22. **Snob humbled** (Vanilla Traits Expanded): humbled by the colony's oath (Humbled) or a hungry season, and carried
    to safety or nursed back by someone, pride down 6. Uncommon (6%). **Built** (20 November 2026, the second batch, not released yet). One rule: each "or" is a choice of marks.
23. **Couch potato gets up** (Vanilla Traits Expanded): as the hard-work rule, 60 days with little recreation and a
    masterwork, a mastered skill or a student taught. Uncommon (8%). **Built** (20 November 2026, the second batch, not released yet). "Little" is the hard-work rule's 4%, and the mark within 120 days.
24. **Insomniac sleeps again** (Vanilla Traits Expanded): 60 calm days, a long peace, and few recent hours up at night
    (new: an "at most" on recent hours). Uncommon (6%). **Built** (20 November 2026, the second batch, not released yet). "Few" is at most 10 recent hours up and busy between eleven and five (about half an hour a night).
25. **Mood swings and impulsiveness settle** (Vanilla Anomaly Expanded - Insanity's mood swings and impulsive): 120 calm
    days and a long peace, or the therapy of 14. Rare (4%). **Built** (20 November 2026, the second batch, not released yet). Two rules each.

## D. New good traits from good things

26. **Brave** (Vanilla Traits Expanded): held the line twice and carried someone to safety, nerve up 15; never for a
    wimp or a coward. Uncommon (8%). Courage shown again and again. **Built** (20 November 2026, not released yet). Never a craven either (RimTraits, if loaded).
27. **Jogger** (fast walker to jogger, a trained trait like fast walker): 120 recent hours hauling, or two seasons
    abroad (WorldSeen). Uncommon (6%). **Built** (20 November 2026, not released yet). Two rules; "two seasons abroad" is the mark for a second season abroad within a year.
28. **Nursed someone back, and healer's hands from it**: a new mark for the nurse (the other side of "nursed back to
    health", which already names who did it), counted toward kind and squeamish's way out (17), and a second way to
    Rimfolk's healer's hands beside the habit: 60 recent hours as a doctor and three patients nursed back. Uncommon (8%).
29. **Perfectionist** (Vanilla Traits Expanded): three masterworks and a mastered skill
    within 180 days, pride up 10. Rare (4%), neither good nor bad.
30. **Dog person or cat person** (Vanilla Traits Expanded): a year bonded with one of the colony's dogs or cats. Common
    once it holds (10%).
31. **Fisherman** (Vanilla Fishing Expanded): 80 recent hours fishing (the Fishing work). Uncommon (8%).

## E. A few flavourful ones from the mods' traits

32. **Wanderlust** (Vanilla Traits Expanded): two seasons abroad within a year (WorldSeen twice), openness up 10.
    Uncommon (5%), neither good nor bad.
33. **Desensitized** (Vanilla Traits Expanded): two costly fights (SurvivedLoss) and
    20 burials (the game's record), warmth down 6. Rare (3%), neither good nor bad, from hard things that already
    happened. Its way out: a long peace and two kindness marks.
34. **Mountain lover** (Vanilla Traits Expanded): a year almost always under a mountain's roof, as the undergrounder
    rule. Rare (3%).
35. **Drunken master** (Vanilla Traits Expanded): a brawler who held the line three times while drunk (new: a fight mark
    counted only while drunk). Rare (3%), neither good nor bad.
36. **Occultist** (Anomaly): 60 recent hours of dark study and drawn to the unknown twice (TouchedByTheUnknown).
    Uncommon (5%), neither good nor bad.

## F. How often, and how it shows

37. **A way out isn't held back by the colony's 10 days.** Today any change holds every other one in the colony for 10
    days; a way out of a bad trait would no longer wait for it (gains still would), so the ways out are not crowded out
    by gains. **Built** (20 November 2026, not released yet).
38. **The character page shows every way out from the start.** "Where life is taking them" shows a way out of a trait
    Rimfolk gave from the day it came; every way out of a bad trait would show there from the start, with what is still
    missing in plain words (a long peace, a seat, time at rest), so the player can steer toward it. **Built** (20 November 2026, not released yet).

## The lead's recommended first batch (built, 20 November 2026, not released yet)

**37, 38, 1, 2, 4, 5, 6, 14, 10, 16, 26, 27** (twelve). They close every gap Rimfolk opened itself (jealous, bloodlust,
cannibal, world-weary and rebel, all given today with no way out), make the ways out come often enough to be seen and
show the player what would help, bring in the therapy mod the player already has as a way out of the mental traits, take
on the two arriving traits most players dread (pyromaniac, and the lush and stoner), and add two good traits so the
round is not all repair. Psychopath's way out (3) waits for a second round: it needs the new zero count, and it is the
one the player may want to keep rare or leave out.

## Left out on purpose

- Traits of the body or an inborn gift (ugly, sickly, slow learner, annoying voice and the like) and sexuality stay
  untouched, as today. Misandrist and misogynist are on that list too; a way out (working beside and being saved by
  someone of the other sex) is possible if the player wants it.
- A RimWorld of Magic's class traits (mages, fighters, the bard and the rest) are classes with their own powers, not
  character.
- Humanoid Alien Races' xenophobe and xenophile: the player's list has no other races for them to be about.
- Vanilla Traits Expanded's technophobe, dunce, prude and eccentric, and the insanity mod's inhumanized and void touched:
  either tied to the colony's age or the body, or the game's own Anomaly gives and takes them.
- No new bad trait from Rimfolk.

## Mods that would help

None is needed. Therapy (Continued) (`orion.therapy`, tagged 1.6 and 1.7) is in the player's list and active, and 14
and 25 use it softly; Vanilla Traits Expanded, Vanilla Anomaly Expanded - Insanity, Dubs Bad Hygiene and Vanilla Fishing
Expanded, all active, give the traits in C, D and E.
