# The post and letters: messages that travel with someone

The player (8 November 2026), in their words: "the couriers it feels a bit weird being able to ... send a letter to some
civilization and then a courier just randomly appears out of nowhere ... the couriers should also be on mounts ...
normally maybe the courier is just coming by on a regular basis or randomly. Couriers drop by or travelers or visitors
where I can then for example, pass on messages until I have like a comms console and can do it on demand ... Could I
pause and send a message to every single guy in the whole map? It should be more opportunistic, I guess, dynamic ...
when am I going to send a letter to another guy on the other side of the world just asking like to chit chat? ... the
options should have some motivation to do them". On the brainstorm: "this all sounds really good! visitors and other
random characters should perhaps also be able to take letters when going their direction".

Where things stood: from a town's card a colonist writes greetings, condolences, a warning, asking for news, an
invitation or asking for help (`World/NeighbourLetters.cs`, `LetterMath.cs`); a messenger of their people comes to the
gate for it on demand (`Couriers/`), one letter to each people and three under way at most; with a powered comms console
a call does the same at once. The diplomacy acts (pacts, envoys, favours, matches, feasts, fealty, ransoms) sit in menus
beside them.

## Step 1: the post (how messages travel)

*Built (8 November 2026), points 1 to 9, in `Source/ColonyCouncil/Post/` (DESIGN.md "The post: letters that travel with
someone"; the handoff's entry has what was verified and what wasn't). Giddy-Up's riding is tried by its own "Mount" job
by name and falls back to leading the beast; Vehicle Framework is not used (the mail run comes mounted or on foot).*

1. **The outbox.** Letters written wait in a mailbag at the gate (a spot the colony places, with a plain fallback by the
   gate when it has none). Its inspect pane lists each letter and how it is likely to leave ("with the next rider on the
   east road", "with the traders camped by the river").
2. **Anyone going that way can take them.** Every outsider who leaves the map is a possible carrier for letters to places
   on their way: post riders, trade caravans, visitors and guests, camps breaking up, pilgrims, wanderers, refugees,
   travellers passing through, a people's messengers, the colony's own caravans setting out. The carrier walks to the
   mailbag, takes what goes their way, and leaves with it, on the map. Whether a place is "on their way" is read from the
   world: their destination or their people's towns, and the direction they leave in. So a colony can't pause and write to
   the whole world at once: mail leaves when someone is going.
3. **A regular post, by age.** Tribal: no post, only travellers, camps and the colony's own runners. Medieval: a post rider
   on the nearest road every week or two. Industrial: a mail run on a schedule (a motorbike or truck with the player's
   vehicle mods). Spacer: shuttles. Riders come mounted on their people's animals (Giddy-Up 2): horses, camels, muffalo,
   by region and age.
4. **The colony's own rider.** For something urgent, a colonist rides out (a mount if the colony has one) straight to them:
   fast and sure, but away for the trip, with the road's dangers on the shared harm budget; the answer comes back with
   them.
5. **Mail comes in the same way.** Answers and the world's letters arrive with whoever is coming: a rider hands the bag to
   a colonist at the gate, a trader or visitor brings a letter. Nothing appears from nowhere.
6. **The comms console stays special.** On-demand calls, to peoples with radios (industrial and later) or near a Signal
   Line mast; they may not answer at once ("an aide takes a message").
7. **Writing takes a writer.** A colonist who can write sits at a table for an hour to write; a tribal colony gives a
   spoken message to the carrier instead.
8. **Diplomatic range until comms** (the player: "i should also have some sort of diplomatic range, right? until
   comms. where it will be more difficult having communications and interactions with bases further away or in
   difficult terrain"). Reach is travel time over the world's own terrain, not straight distance: hills, mountains,
   swamps and ice slow it, roads and rivers speed it, winter and snow count, and the carrier's means sets the pace. Within
   range the post, passing carriers and the colony's rider reach; beyond it only a relay through a nearer people's town
   (slower, on good terms) or a long ride; out of reach until radio. Mounts, roads, vehicles and the Signal Line's masts
   widen it; a comms console removes it for peoples with radio, shuttles in the spacer age. Interactions by letter or
   envoy follow it too. The map shows it as a soft shading where that is simple.
9. **How to reach them now.** A town's card says it plainly: "by the next caravan north", "by your own rider (about two
   days)", "by radio (Signal Line mast)".

## Step 2: why write (options with a reason)

*Points 10 and 11 built (8 November 2026) with the diplomacy screen's step A: the occasions in
`World/LetterOccasionMath.cs` (pure, tested) and `LetterOccasions.cs`, read by the letter's window and by "Do something
with..." on the Peoples view, the setting "Letters by occasion"; what each act is for and costs in
`UI/Region/PeopleActMath.cs` and `Rimfolk_PeoplesScreen.xml` (DESIGN.md "The diplomacy screen: the peoples beside their
page"). The invitation's "the colony would like visitors" is guest beds (Hospitality's) standing empty with nobody from
outside staying; "a fair" is a day of the colony calendar kept with a gathering. Points 12 to 14 built (8 November 2026)
with the diplomacy screen's step B, in `Source/ColonyCouncil/Dealings/` (`PenFriends`, `WorldWrites`, the chain in
`Post/PostReach.cs`; the rules in `DealingMath.cs`; DESIGN.md "Dealings with a whole people or town, pen friends and the
world writing first"): three letters answered warmly or courteously in 60 days with regard 30 make a pen friend (word of
raids near them, 2% better terms, introductions, a favor after a season); a notable who thinks well of the colony writes
first about once in two or three weeks, news, a small request or an invitation, answered by a letter that leaves with
the next carrier; a town out of reach goes by introduction through a people at peace with both (goodwill 40, or a pen
friend among them), slower than a relay. Settings "Pen friends" and "The world writes first".*

10. **Letters by occasion.** A town's card shows what could be written now and why: condolences only after a loss heard
   of, a warning when a threat to them is known, an invitation when there is a feast or fair to come, asking for help
   when the colony is short, greetings at the turn of the year or with a gift. Those without a reason are greyed with it.
11. **What each is for**, said with the option: what news brings, what an invitation brings, what a warning earns.
12. **Pen friends.** A steady exchange with one of their notables becomes something: early warnings, better prices,
    introductions to peoples further away, a favour.
13. **The world writes first more often**: news, requests and invitations that want an answer, answered when the next
    carrier passes.
14. **Distance.** Peoples far off are reached through a chain of neighbours (slower, introductions needed) until radio or
    shuttles.

## Step 3: letters that read like letters

The player (9 November 2026): "letters exchanged from and to pawns should also read like actual letters, make a varied
selection of interesting letters that read well and can also crack a joke sometimes, or be serious"; of their tone, "they
don't necessarily have to be warm, just according to the situation and motivation"; the writer's race and culture shape
the tone and style; medieval writers in medieval English; "how a stupid bandit could not spell words correctly etc"; and
"you can also be experimentive with grammar, writing style, formatting, phrasing etc if it makes sense, like strange races
or cultures".

*Built (9 November 2026), not released yet, in `Source/ColonyCouncil/Letters/` (`LetterFormMath`, `LetterCultureMath`,
`LetterForm`, `LetterBook`, `LetterReadWindow`; the pools `Rimfolk_Letters_*.xml`; DESIGN.md "Letters that read like
letters"). Setting "Letters written out in full" (on).*

15. **A letter's form**, both ways: a heading now and then (the place, the date in the form of the writer's age), a
    salutation by who writes to whom and how they stand, a few short paragraphs in the writer's own voice, a closing and a
    signature in kind, now and then a P.S. What the game does with it goes under it as the narrator's plain lines.
16. **The tone from the situation and the motive**: their regard and the answer's tone, at war, what the writer wants (a
    friend, a trader angling, an official's duty, a wary neighbour, a proud one), hard times. A cool, curt or businesslike
    letter is never warm; jokes where the writer and situation allow them, a dry remark in a cool letter.
17. **Who writes shapes how**: their age and people, their culture and kind (memes, xenotype), a medieval writer's
    English (thee and thou), an unschooled hand (deliberate misspellings, a mark for a signature), the truly strange in a
    voice of their own (an android's record, a hive's "we", a fairy's formula, the ancient's long postscript).
18. **The colony's own letters written out** as the writer finishes them, in their voice, for its purpose and topic,
    true to what is so at home; kept, and read at the mailbag, from the answer's card and on the Diplomacy screen's People.
19. **A call reads as a call, a spoken message as words passed on**, never as a letter.

## Order of building

Step 1, then step 2, one after another (same area); step 3 after both.
