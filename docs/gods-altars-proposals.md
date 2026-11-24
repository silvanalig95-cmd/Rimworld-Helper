# Altars that mean something, and more ways for the gods (proposal, 24 November 2026; not built)

The player, 24 November 2026: "the altars ... now you basically just activate an item as one ... I think I could build
10 graves and assign each grave to a god like that at the same time. So I feel like setting something as an altar to a
god should, yeah. I mean, maybe you should have some conditions fulfilled, for example, god actually known, believed in,
or that someone wants to believe in a god. But there's kind of a system and logic a bit behind it, and realism and actual
gameplay mechanic that makes sense. Not that I, you know, can just again have an altar for each god at the same time for
free within a minute. Because then I feel a bit meaningless in a way", then "other ideas that you have in regards to
further integrate them. And also later I want to create some images as well with the scripts".

Nothing here is built until the player says so. The unseen rule holds throughout: what decides is what the colony's
people believe (the book, each colonist's belief), never the god's favour, wants or rules, and nothing shows a number.

## Where it stands

`Gods/Worship/Shrines.cs`: any of the colony's ritual places (altars, the ideogram, lectern, totemic slabs), any art
(every sculpture, and the sarcophagus, which carries art), the nature shrines, the reliquary, the incense shrine, a
worshipful terminal, and anything whose name says "altar" or "shrine" can be dedicated. The button offers every god the
colony has heard of (up to 16), whoever holds it or not. Anyone free walks over (the priest first) and says the words.
Each dedication pleases the god a little (`WorshipMath.DedicateFavour`, the same as a good offering), with no limit, no
cost and nothing to keep up; taking a place away displeases it a little. So ten sculptures can be ten gods' places within
the hour, and each one is a small gift to its god. Two names slip through that are no shrine at all: Vanilla Psycasts
Expanded's shrine shields (a shield generator).

## A. Altars (the player's request)

A1. **Only for a god someone of the colony holds.** The button still lists every god the colony has heard of, but one can
be given a place only when at least one of the colony's own holds it (leaning toward it or more, `PawnGods`), or someone
is turning toward it: a moment, a dream or a vow raised their belief in it lately (a seeker). The others stay in the list,
greyed: "Nobody here holds Hush." The one who says the words is someone who holds that god, the priest only where the
priest holds it too.

A2. **How many.** One place per god at each home. A second (a wayside shrine, at another spot) once three or more of the
colony hold that god, never more places than people who hold it. So the number of altars follows the colony's own
belief: a colony of mixed faith can have several gods' places, a colony of one faith one or two.

A3. **The place has to suit.** Always: the altars, ideogram, lectern, totemic slabs, reliquary, incense shrine, nature
shrines, and the mods' altars and shrines (Dragons Descent, Alpha Memes, Vanilla Expanded). A sculpture only of good
quality or better. A sarcophagus or a grave only for a god of the dead. Never something that only has the word in its
name (the shrine shields). Beyond that, where the colony's book believes the god likes a kind of place (a god of the
waters by water, of the harvest by the fields, of the hearth by a fire, of the night away from lamps), a place that
suits is liked more (prayers there answered a little more often, belief growing a little faster); one that doesn't still
works, a little less. As the book believes it: it may be wrong.

A4. **The dedication is a rite, not a click.** The believer brings an offering the colony believes the god likes (the
book's "what it loves", the offerings already there), the others who hold the god gather if they are free, and it takes
about two hours at the place. Where the book has a time for the god (dawn, night), it waits for it. Without an offering
at the colony, it waits, and is let go after a few days as now.

A5. **Nothing for nothing.** The dedication itself no longer pleases the god. Its regard comes from what is done at the
place afterwards: prayers, offerings, its feast. Taking a place from its god, or tearing it down, is felt more the longer
it was kept.

A6. **Upkeep.** A place needs someone to pray or leave something there now and then (the believers already do this on
their own). Untended for a season it is neglected: its believers are a little unhappy about it, and prayers there count
for less until someone tends it again. So many altars cost the colony time, every season.

A7. **Rivals side by side.** A god's place in the same room as a rival god's place troubles the believers of both, the way
taking a place from one god for another already does.

A8. **A believer asks for one.** Someone who holds a god that has no place at their home asks the council for one (the
council's questions). Agreed, the player picks the spot (the place's button), and the one who asked says the words.
Refused, they are disappointed for a while.

## B. More ways for the gods (ideas)

B1. **A service at the temple.** Where a god has a temple (its place in a fine room), its keeper (the priest, or the most
devout believer) holds a short service on the god's day each week: the believers gather, it lifts their mood a little,
their belief grows. In the colony's age: a fire circle, a mass, a sermon, a broadcast.

B2. **Pilgrims.** Travellers of a people whose patron has a temple at the colony come now and then to pray there: a small
gift, some goodwill with their people, and they stay a night (the visitors already there).

B3. **Oaths at the altar.** A vow made at the god's place weighs more: kept, belief and regard grow more; broken, the
believers take it harder.

B4. **The colony's rites at the god's place.** A wedding, a funeral or a naming held at a god's place carries that god
into the rite: the colony's book records it, and those who hold the god are glad of it.

B5. **The place's room.** What stands around a god's place (braziers, candles, flowers, the things the book believes the
god likes) makes it finer: its believers like praying there more. Rooms are already judged by what they hold.

B6. **Whose feast this season.** When the colony's believers are split between rival gods, the council is asked whose
feast the colony keeps this season; the others keep theirs quietly, a little put out.

B7. **Something left behind.** What a god's moment leaves (a stone, a feather, a burnt branch) can be kept in a reliquary
at that god's place, and its believers come to look at it.

## C. Pictures (with the portrait kit)

The lists are made here (what to paint, and how the game shows it); the painting and sorting run through the kit and
Rimfolk Studio (`docs/portrait-art/STUDIO-HANDOVER.md`, section 0).

C1. **Each god's place as a backdrop**, in each age: a shrine of the sea god by the shore, the hearth god's chapel, the
machine god's terminal. Someone praying at a god's place, or standing in its temple, gets that backdrop behind their large
portrait. This needs a new field on a backdrop's record (the god's domain), so the Studio would need a prompt once it is
built.

C2. **The gods' shapes when they walk the map** (they are seen then): a painted picture for each god in each age, shown on
the large portrait when one of them is selected.

C3. **The gods' beasts and sacred animals** as creature pictures (the creature lists just made).

C4. **Priests and devout believers** in each age's dress (the people lists already have the priest's work).

## How it would be built

- Gods/Worship/Shrines.cs: who may ask (A1), how many (A2), what suits (A3: a list of what counts, read from every def
  loaded, plus the domain's kinds of place as the book holds them), the rite (A4: a gathering job with an offering, as
  the feasts and offerings already do), no favour on dedicating (A5), neglect (A6), rivals (A7).
- The council's question (A8), the service (B1), pilgrims (B2), oaths (B3), rites (B4), the room (B5), whose feast (B6),
  relics (B7) each through the systems already there (WorshipCouncil, Festivals, the peoples' visitors, Vows, the
  game's rituals, rooms judged by contents, the reliquary).
- Pictures: C1 a backdrop field and its matching in the game (a format change), C2 a library of the gods' shapes, C3 and
  C4 rows in the creature and people lists.
- An older save keeps the places it has; a god beyond the new limits keeps its place until it is taken down.
