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
8. **How to reach them now.** A town's card says it plainly: "by the next caravan north", "by your own rider (about two
   days)", "by radio (Signal Line mast)".

## Step 2: why write (options with a reason)

9. **Letters by occasion.** A town's card shows what could be written now and why: condolences only after a loss heard
   of, a warning when a threat to them is known, an invitation when there is a feast or fair to come, asking for help
   when the colony is short, greetings at the turn of the year or with a gift. Those without a reason are greyed with it.
10. **What each is for**, said with the option: what news brings, what an invitation brings, what a warning earns.
11. **Pen friends.** A steady exchange with one of their notables becomes something: early warnings, better prices,
    introductions to peoples further away, a favour.
12. **The world writes first more often**: news, requests and invitations that want an answer, answered when the next
    carrier passes.
13. **Distance.** Peoples far off are reached through a chain of neighbours (slower, introductions needed) until radio or
    shuttles.

## Order of building

Step 1, then step 2, one after another (same area).
