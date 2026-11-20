# The flavour text restored in eight parts: the lines put right

The player, 20 November 2026: "yes, please restore letters, wishes, ambitions, life goals, personal stories, council questions, camps, and world events to just the previous text, to kind of the flavor style text that you created before, restore it from the backup, and keep that also in mind for additional content to be added."

Each of the eight parts has its text from before its plain-words batch again (letters 0.266.9, wishes, ambitions and life goals 0.268.2, personal stories 0.268.4, council questions 0.268.5 and 0.269.2, camps 0.269.1, world events 0.270.1), with every def setting and code change since kept (DESIGN.md, "The flavour words back in eight parts"). Where an old line claimed what the map doesn't show, the old line came back and then only its false part was changed, in the old voice. This page lists every such line: the file, why, and the words that changed (a few words either side; " / " is a paragraph break). Lines the far-town errands (0.275.1) rewrote are theirs and not listed.

Lines restored per part (lines of the part's files that differ from the plain text): letters 608, wishes 170, ambitions 35, life goals 36, personal stories 161, council questions 509, camps 291, world events 336.

## Letters (2 lines)

**A ransom is paid in silver (`Ransoms.cs`), not gold** (2)

- Rimfolk_Letters_Heart: "you pay. Gold. Real gold, not painted" is now "you pay. Silver. Real silver, not painted"
- Rimfolk_Letters_Heart: "back, send gold." is now "back, send silver."

## Wishes (5 lines)

**A sentence opened with a pronoun placeholder, which the game shows lowercase: the name instead** (4)

- Wishes_Home: "{FOR_possessive} bedroom reaches" is now "{FOR_nameDef}'s bedroom reaches"
- Wishes_Home: "{FOR_possessive} bedroom's space" is now "{FOR_nameDef}'s bedroom's space"
- Wishes_Home: "{FOR_possessive} bedroom has" is now "{FOR_nameDef}'s bedroom has"
- Wishes_Home: "{FOR_possessive} bedroom gets" is now "{FOR_nameDef}'s bedroom gets"

**The wish takes any hostile grown prisoner, not the last raider alive (`WishWorker_Justice`)** (1)

- ColonyCouncil: "raid, and {OTHER_nameDef} is the only raider still alive." is now "raid, and {OTHER_nameDef}, one of the enemy, is still alive."

## Life goals (1 lines)

**The colony's books are not always four** (1)

- Rimfolk_Aspirations_Lives: "colony. There are only four, but some" is now "colony. There aren't many, but some"

## Personal stories (12 lines)

**The student gains two levels, not nearly the elder's (`ArcWorker_Elder`)** (4)

- Council_Arcs_Crafts: "them until they're nearly as good, and hands" is now "them until they've come on two levels, and hands"
- Rimfolk_Arcs_MoreWords: "{OTHER_nameDef} has nearly caught up with" is now "{OTHER_nameDef} is catching up with"
- Rimfolk_Arcs_MoreWords: "{OTHER_nameDef}, is close to {PAWN_possessive}" is now "{OTHER_nameDef}, is getting closer to {PAWN_possessive}"
- Rimfolk_Arcs_MoreWords: "What {PAWN_nameDef} knew of {SKILL}," is now "What {PAWN_nameDef} had to teach of {SKILL},"

**A vendetta can be for someone taken, not only killed (`Vendetta.kidnapped`)** (1)

- Council_Arcs_Heart: "way since {LOST}." is now "way since what happened to {LOST}."

**No children's questions are staged; a colony may have no children** (1)

- Rimfolk_Arcs_Ages: "in the sun with the children's questions." is now "in the sun."

**The story ends well while the animal lives; only its death before then ends it (`ArcTracker`)** (1)

- Rimfolk_Arcs_Animals: "gets up. Its death, when it comes, is the" is now "gets up. If it dies first, its death is the"

**What is staged is the animal running at its colonist's heels and off again (`AnimalScene.Pester`)** (1)

- Rimfolk_Arcs_Animals: "each other: an ambush in the doorway, a stolen lunch, open war." is now "each other: a rush at the heels, a dash away, open war."

**They see the travelers off at the colony's gate (`PlaySpot.Gate`)** (2)

- Rimfolk_Arcs_World: "to the boundary stone and came" is now "to the gate and came"
- Rimfolk_Arcs_World: "to the edge of the land</label><to>Gate</to><waitHours>3</waitHours><duration>0.5~0.75</duration>" is now "to the gate</label><to>Gate</to><waitHours>3</waitHours><duration>0.5~0.75</duration>"

**The news reaches the colony in any way, not only by traders** (1)

- Rimfolk_Arcs_World: "The traders' news this" is now "The news this"

**A story has three to five stages (`ArcDef.ConfigErrors`)** (1)

- Rimfolk_Arcs: "through three or four stages as" is now "through three to five stages as"

## Council questions (49 lines)

**The answer walks the pet out and lets it go alive (`DilemmaEffect_ReleasePet`)** (1)

- Council_Dilemmas3: "one last walk" is now "one last walk, and let {PET_objective} go"

**The bonfire lasts five or six hours and changes no one's hours** (2)

- Council_Dilemmas_Customs_Variants: "yard, with music, and the night shift let off until the sky goes pale. / It would take" is now "yard, with music late into the night. / It would take"
- Council_Dilemmas_Seasons: "and drinking and the night shift let off until dawn. / It would burn" is now "and drinking late into the night. / It would burn"

**The answer gives the rest of today's work hours off (`DilemmaEffect_Rest`), not a season** (1)

- Council_Dilemmas_Goals: "chores for a season" is now "chores for the rest of the day"

**No celebration follows the repairs** (1)

- Council_Dilemmas_Seasons: "Repairs first, celebrations later" is now "Repairs first, no celebrations"

**The answer gives moods only; nothing lasts a season** (1)

- Council_Dilemmas_Secrets: "under watch for a season" is now "under watch"

**The remembrance night is an evening gathering, not a day's work** (1)

- Council_Dilemmas_Social: "and a day of everyone's work. {A_nameDef} says" is now "and a late night for everyone. {A_nameDef} says"; "worth a day. {B_nameDef} says" is now "worth a night. {B_nameDef} says"

**A sentence opened with a pronoun placeholder, which the game shows lowercase: the name instead** (32)

- Rimfolk_Dilemmas_Camps: "colony's side. {A_pronoun} did, mostly," is now "colony's side. {A_nameDef} did, mostly,"
- Rimfolk_Dilemmas_Customs_YearFour: "their backs. {A_pronoun} would like" is now "their backs. {A_nameDef} would like"
- Rimfolk_Dilemmas_Customs_YearFour: "hearths do. {A_pronoun} would make" is now "hearths do. {A_nameDef} would make"
- Rimfolk_Dilemmas_Customs_YearFour: "the dark. {A_pronoun} asks for" is now "the dark. {A_nameDef} asks for"
- Rimfolk_Dilemmas_Customs_YearFour: "burned low. {A_pronoun} would keep" is now "burned low. {A_nameDef} would keep"
- Rimfolk_Dilemmas_Customs_YearFour: "time for. {A_pronoun} asks for" is now "time for. {A_nameDef} asks for"
- Rimfolk_Dilemmas_Customs_YearFour: "it's long. {A_pronoun} would like" is now "it's long. {A_nameDef} would like"
- Rimfolk_Dilemmas_Customs_YearFour: "by watching. {A_pronoun} would start" is now "by watching. {A_nameDef} would start"
- Rimfolk_Dilemmas_Customs_YearFour: "worst night. {A_pronoun} asks for" is now "worst night. {A_nameDef} asks for"
- Rimfolk_Dilemmas_Customs_YearFour: "their own. {A_pronoun} would tell" is now "their own. {A_nameDef} would tell"
- Rimfolk_Dilemmas_Customs_YearFour: "nobody forgot. {A_pronoun} would like" is now "nobody forgot. {A_nameDef} would like"
- Rimfolk_Dilemmas_Variety3: "it's there. {A_pronoun} asks for" is now "it's there. {A_nameDef} asks for"
- Rimfolk_Dilemmas_Variety3: "still there. {A_pronoun} wants the" is now "still there. {A_nameDef} wants the"
- Rimfolk_Dilemmas_Variety3: "can tell. {A_pronoun} has asked" is now "can tell. {A_nameDef} has asked"
- Rimfolk_Dilemmas_Variety3: "touching it. {A_possessive} hands won't" is now "touching it. {A_nameDef}'s hands won't"
- Rimfolk_Dilemmas_Variety3: "day instead. {A_pronoun} would take" is now "day instead. {A_nameDef} would take"
- Rimfolk_Dilemmas_Variety3: "a question. {A_pronoun} says {STUDY1}" is now "a question. {A_nameDef} says {STUDY1}"
- Rimfolk_Dilemmas_YearFour: "growing season. {A_pronoun} wants hands" is now "growing season. {A_nameDef} wants hands"
- Rimfolk_Dilemmas_YearFour: "{THING} gives. {A_pronoun} wants the" is now "{THING} gives. {A_nameDef} wants the"
- Rimfolk_Dilemmas_YearFour: "can eat. {A_pronoun} asks for" is now "can eat. {A_nameDef} asks for"
- Rimfolk_Dilemmas_YearFour: "as long. {A_pronoun} wants it" is now "as long. {A_nameDef} wants it"
- Rimfolk_Dilemmas_YearFour: "this morning. {A_pronoun} asks for" is now "this morning. {A_nameDef} asks for"
- Rimfolk_Dilemmas_YearFour: "this deep. {A_pronoun} wants the" is now "this deep. {A_nameDef} wants the"
- Rimfolk_Dilemmas_YearFour: "no walls. {A_pronoun} asks for" is now "no walls. {A_nameDef} asks for"
- Rimfolk_Dilemmas_YearFour: "every morning. {A_pronoun} could have" is now "every morning. {A_nameDef} could have"
- Rimfolk_Dilemmas_YearFour: "a raid. {A_pronoun} wants the" is now "a raid. {A_nameDef} wants the"
- Rimfolk_Dilemmas_YearFour: "to hide. {A_pronoun} wants clear" is now "to hide. {A_nameDef} wants clear"
- Rimfolk_Dilemmas_YearFour: "to spring. {A_pronoun} would have" is now "to spring. {A_nameDef} would have"
- Rimfolk_Dilemmas_YearFour: "the winter. {A_pronoun} would choose" is now "the winter. {A_nameDef} would choose"
- Rimfolk_Dilemmas_YearFour: "the thaw. {A_pronoun} proposes {COUNT}" is now "the thaw. {A_nameDef} proposes {COUNT}"
- Rimfolk_Dilemmas_YearFour: "{A_pronoun} does. {A_pronoun} asks for" is now "{A_pronoun} does. {A_nameDef} asks for"
- Rimfolk_Dilemmas_YearFour: "their feet. {A_pronoun} asks the" is now "their feet. {A_nameDef} asks the"

**The turns run from 20 h to about 2 h** (1)

- Rimfolk_Dilemmas_Choices: "Take turns through the night" is now "Take turns sitting up half the night"

**Six people clean from 7 h, with a toast after; four clean the morning in the other answer** (2)

- Rimfolk_Dilemmas_Customs_New: "for a day" is now "for a morning, and a toast after"
- Rimfolk_Dilemmas_Customs_New: "The morning only" is now "Four hands, the morning only"

**Nothing sends A anywhere; the answer gives a mood and leave** (1)

- Rimfolk_Dilemmas_Lives: "{A_nameDef} goes with the" is now "Let {A_nameDef} go with the"

**The watch is one night, until dawn (`DilemmaEffect_Vigil`)** (1)

- Rimfolk_Dilemmas_Lives: "watch for a while" is now "watch for the night"

**The animal is marked for the colony's hunters, whoever goes (`DilemmaEffect_MarkBeast`)** (1)

- Rimfolk_Dilemmas_Variety3: "{B_nameDef} ends its" is now "A hunter ends its"

**The lead's fix of 0.269.2, kept: the first hunter free goes out** (1)

- Rimfolk_Dilemmas_Variety3: "hunters, and {B_nameDef} goes out to give it" is now "hunters, and the first hunter free will go out and give it"

**The work party keeps its own hours, not always the afternoon** (2)

- Rimfolk_Dilemmas_YearFour: "sow it this afternoon" is now "sow it"
- Rimfolk_Dilemmas_YearFour: "building party this afternoon" is now "building party"

**The crop is sown as the old one comes out (`DilemmaEffect_SetCrop`), not by a date** (1)

- Rimfolk_Dilemmas_YearFour: "sows it. {A_pronoun} would have" is now "sows it. {A_nameDef} would have"; "the {PLAIN} field by the next moon. {B_nameDef} says" is now "the {PLAIN} field. {B_nameDef} says"

## Camps (127 lines)

**An hour the words can't know** (41)

- Council_CampKinds: "the colony's land this morning, bent low" is now "the colony's land, bent low"
- Council_CampKinds: "week, and tonight {X_pronoun} asked" is now "week, and today {X_pronoun} asked"
- Council_CampKinds: "colony's butchering. Tonight {X_nameDef} lingered" is now "colony's butchering. Today {X_nameDef} lingered"
- Council_CampKinds: "from {FACTION} were gone before dawn, their bedding" is now "from {FACTION} are gone, their bedding"
- Council_CampKinds: "the road this morning have worn" is now "the road have worn"
- Council_CampKinds: "tools, spent the afternoon on it," is now "tools, spent a while on it,"
- Council_CampKinds: "had it right before supper. There was" is now "had it right. There was"
- Council_CampKinds: "{THING} patched up in an afternoon, with a" is now "{THING} patched up, with a"
- Council_CampKinds: "colony's door this evening. After a" is now "colony's door today. After a"
- Council_CampKinds_More: "evening, unasked. Tonight {X_nameDef} asked" is now "evening, unasked. Today {X_nameDef} asked"
- Council_CampKinds_More: "colony's fields this morning, singing." is now "colony's fields today, singing."
- Council_CampKinds_More: "the camp's. Tonight {X_nameDef} asked," is now "the camp's. Today {X_nameDef} asked,"
- Rimfolk_CampKinds_Land: "evening, unasked. Tonight {X_pronoun} asked" is now "evening, unasked. Today {X_pronoun} asked"
- Rimfolk_CampKinds_Peoples: "evenings, unasked. Tonight {X_pronoun} asked" is now "evenings, unasked. Today {X_pronoun} asked"
- Rimfolk_CampKinds_Peoples: "went out at first light, the dog" is now "went out with the dog"; "bells were still heard an hour later." is now "bells were heard a long while after."
- Rimfolk_CampKinds_PeoplesLore: "the caravan's. Tonight {X_pronoun} asked" is now "the caravan's. Today {X_pronoun} asked"
- Rimfolk_CampKinds_PeoplesLore: "went out at first light, chains rattling" is now "went out with chains rattling"
- Rimfolk_CampKinds_Road: "others recognize. Tonight {X_pronoun} asked" is now "others recognize. Today {X_pronoun} asked"
- Rimfolk_CampKinds_Road: "{A_nameDef} sat up half the night at the" is now "{A_nameDef} sat at the"
- Rimfolk_CampKinds_Road: "[tales] {A_nameDef} spent an evening with {X_nameDef}" is now "[tales] {A_nameDef} sat a while with {X_nameDef}"
- Rimfolk_CampKinds_Road: "evening, unasked. Tonight {X_pronoun} asked" is now "evening, unasked. Today {X_pronoun} asked"
- Rimfolk_CampKinds_Road: "colony's land. Tonight {X_pronoun} came" is now "colony's land. Today {X_pronoun} came"
- Rimfolk_CampKinds_Road: "all week. Tonight {X_pronoun} asked" is now "all week. Today {X_pronoun} asked"
- Rimfolk_CampKinds_Road: "through the snow this morning, carrying one" is now "through the snow, carrying one"
- Rimfolk_CampKinds_Road: "start to finish, over a long evening. By the" is now "start to finish. By the"
- Rimfolk_CampKinds_Scholars: "{A_nameDef} sat up late at the" is now "{A_nameDef} sat at the"
- Rimfolk_CampKinds_Variety2: "colony's gate at dusk and asked," is now "colony's gate and asked,"
- Rimfolk_CampKinds_Variety2: "{PLACE} is empty this morning. They left at dawn, gathering as" is now "{PLACE} is empty. They left gathering as"
- Rimfolk_CampKinds_War: "{X_possessive} own. Tonight {X_nameDef} asked," is now "{X_possessive} own. Today {X_nameDef} asked,"
- Rimfolk_CampKinds_Winter: "evening, unasked. Tonight {X_pronoun} asked" is now "evening, unasked. Today {X_pronoun} asked"
- Rimfolk_CampKinds_Winter: "over the snow at first light, the dog" is now "over the snow, the dog"
- Rimfolk_CampKinds_Winter: "their own. Tonight {X_pronoun} asked" is now "their own. Today {X_pronoun} asked"
- Rimfolk_CampKinds_Winter: "beast and left at first light, bound for" is now "beast and left, bound for"
- Rimfolk_CampKinds_Winter: "{A_nameDef} sat up with the" is now "{A_nameDef} sat with the"
- Rimfolk_CampKinds_Winter: "went out at first light, bells on" is now "went out with bells on"
- Rimfolk_CampKinds_Winter: "caravan's fire while the snow came down and heard" is now "caravan's fire and heard"
- Rimfolk_CampKinds_Winter: "packed up before dawn and were" is now "packed up and were"; "the herd's trail by first light." is now "the herd's trail."
- Rimfolk_CampKinds_Winter: "on their way at first light, and {X_nameDef}" is now "on their way, and {X_nameDef}"
- Rimfolk_CampKinds_Winter: "their fire at dawn and walked" is now "their fire and walked"
- Rimfolk_CampKinds_World: "being asked. Tonight {X_pronoun} asked" is now "being asked. Today {X_pronoun} asked"
- Rimfolk_WorldBeyond: "fire {PLACE} until late, while {X_nameDef} told" is now "fire {PLACE} a while, as {X_nameDef} told"

**Who does it** (16)

- Council_CampKinds: "after game with the hunting party for a few hours. Practice with" is now "after game for a few hours, on the hunting party's word. Practice with"
- Council_CampKinds: "gone out with {X_nameDef} and the hunters, keeping low" is now "gone out after game, keeping low"; "the way they showed {A_objective}." is now "the way {X_nameDef} and the hunters showed {A_objective}."
- Council_CampKinds: "[lowtech] {A_nameDef} went out with the hunters at first light, {X_nameDef} beside {A_objective} pointing out the trail" is now "[lowtech] {A_nameDef} has gone out after game, watching for the trail"; "in the grass." is now "in the grass that {X_nameDef} pointed out."
- Council_CampKinds: "{X_nameDef} has taken {A_nameDef} out with the hunting party, and promised to bring {A_objective} back with a carcass" is now "{X_nameDef} has sent {A_nameDef} out where the hunting party has been, and promised {A_objective} a carcass"
- Council_CampKinds: "diggings with their best prospector. Practice in" is now "diggings with the prospector who speaks for them. Practice in"
- Council_CampKinds: "Their eldest comes to" is now "The one who leads them comes to"
- Council_CampKinds: "{X_nameDef}, the eldest of the pilgrims," is now "{X_nameDef}, who leads the pilgrims,"
- Council_CampKinds: "The pilgrims' eldest is coming" is now "The pilgrims' leader is coming"; "pray over {A_nameDef} this afternoon, and has" is now "pray over {A_nameDef}, and has"
- Council_CampKinds: "said thank you at the gate, under {X_possessive}" is now "said thank you, under {X_possessive}"
- Rimfolk_CampKinds_Road: "looks at whoever in the colony is ill" is now "looks at the colonist who is ill"
- Rimfolk_CampKinds_Road: "with the band's best player for a" is now "with the player who speaks for the band for a"
- Rimfolk_CampKinds_Winter: "after game with the trappers for a" is now "after game for a"; "few hours, and learns" is now "few hours, on the trappers' word, and learns"
- Rimfolk_CampKinds_Winter: "going out on the trapline with {X_nameDef} this afternoon, and has" is now "going out along {X_nameDef}'s trapline, and has"
- Rimfolk_CampKinds_Winter: "goes out with the herd followers for a few hours. Practice with" is now "goes out after the herd for a few hours, on the herd followers' word. Practice with"
- Rimfolk_CampKinds_Winter: "after the herd with {X_nameDef} this afternoon." is now "after the herd, the way {X_nameDef} said."
- Rimfolk_CampKinds_World: "them to walk the colony's bounds tonight" is now "them to watch the colony's gate tonight"

**What is given or done** (5)

- Council_CampKinds: "camp and stack the logs for the" is now "camp and leave the logs by the stumps for the"
- Council_CampKinds: "The family cut and stacked forty logs on the colony's side of their camp before noon, and {A_nameDef} carried the food" is now "The family will cut forty logs near their camp, and {A_nameDef} will carry the food"; "over. {X_nameDef} counted it carefully and thanked {A_objective}." is now "over. {X_nameDef} means to count it carefully, and thanked {A_objective} already."
- Council_CampKinds: "axe in turns all morning. The logs are stacked, and the" is now "axe in turns. The logs lie by the stumps, and the"
- Rimfolk_CampKinds_Road: "set out half again as much salted fare for the" is now "set out forty of their pemmican for the"
- Rimfolk_CampKinds_Road: "will help whoever is studying for the colony." is now "will help the colony's current study along."

**A farewell claimed a leftover the map doesn't keep** (20)

- Council_CampKinds: "The smith's camp {PLACE} is gone, and with it the hammering" is now "The smith has left {PLACE}, and with them the hammering"
- Council_CampKinds_More: "The healers' camp {PLACE} is gone, and the" is now "The healers have left {PLACE}, and the"; "herbs with it." is now "herbs with them."
- Council_CampKinds_More: "The surveyors' camp {PLACE} is gone. Somewhere in" is now "The surveyors have left {PLACE}. Somewhere in"
- Rimfolk_CampKinds_Hired: "{PLACE} is empty, the fire raked out. {X_nameDef} raised a hand at the gate: paid to" is now "{PLACE} is empty. {X_nameDef} led them out: paid to"
- Rimfolk_CampKinds_Land: "empty, and where the ruins stood there's mostly open ground now." is now "empty, and their haul went with them."
- Rimfolk_CampKinds_Land: "and the colony's fields are bare and in." is now "and the crew gone on to the next farm."
- Rimfolk_CampKinds_Land: "and the ground by the fence is trampled flat." is now "and the beasts gone with it."
- Rimfolk_CampKinds_Peoples: "high pastures. The far ground is grazed short and well dunged, which the colony's growers will thank them for." is now "high pastures."
- Rimfolk_CampKinds_Peoples: "with the weather, and left a stack of good hay behind for the colony's own beasts." is now "with the weather."
- Rimfolk_CampKinds_PeoplesE: "{PLACE} is empty, the fire raked out and the ground squared. {X_nameDef} saluted at the gate: paid to" is now "{PLACE} is empty. {X_nameDef} led them out: paid to"
- Rimfolk_CampKinds_Road: "{PLACE} is empty. A few grains of salt still glitter in the dirt where the bales stood." is now "{PLACE} is empty, the bales loaded and gone."
- Rimfolk_CampKinds_Road: "and the grass beyond the ridge is cropped short." is now "and the herd gone on to market."
- Rimfolk_CampKinds_Scholars: "The scholars' camp {PLACE} is gone. Somewhere out" is now "The scholars have left {PLACE}. Somewhere out"
- Rimfolk_CampKinds_Variety2: "The woodcutters' camp {PLACE} is gone, the fire raked out and the" is now "The woodcutters have left {PLACE}, and the"; "stumps left behind." is now "stumps left behind them."
- Rimfolk_CampKinds_Winter: "{PLACE} is empty. Out on the ice the holes they cut are already freezing over." is now "{PLACE} is empty, and the ice is left to the colony."
- Rimfolk_CampKinds_Winter: "next frozen water, and the only sign of them out on the ice is a ring of fish scales round each hole." is now "next frozen water."
- Rimfolk_CampKinds_Winter: "empty, and there's a ring of trampled snow round the cold fire and the pegs where the furs were stretched." is now "empty, and the furs gone with them."
- Rimfolk_CampKinds_Winter: "gone from {PLACE}. All that's left is trampled snow, the cold fire and the marks of the picket." is now "gone from {PLACE}, beasts, bells and all."
- Rimfolk_CampKinds_Winter: "is empty, a ring of bedroll hollows in the snow round the cold fire." is now "is empty, and they are gone after the herd."
- Rimfolk_CampKinds_Winter: "and the ring round their fire is trodden flat where they kept the vigil." is now "and the vigil they kept there is over."

**"the eldest", "the youngest", "a lean old drover": nothing picks them** (18)

- Council_CampKinds_More: "seeing to, {X_nameDef}, the eldest, told {A_nameDef}," is now "seeing to, {X_nameDef} told {A_nameDef},"
- Council_CampKinds_More: "days {X_nameDef}, the youngest of the" is now "days {X_nameDef}, one of the"
- Council_CampKinds_More: "[heal,lowtech] {X_nameDef}, the eldest of the" is now "[heal,lowtech] {X_nameDef}, one of the"
- Rimfolk_CampKinds_Land: "{X_nameDef}, the fishers' youngest, says {X_pronoun}" is now "{X_nameDef}, one of the fishers, says {X_pronoun}"
- Rimfolk_CampKinds_Peoples: "{X_nameDef}, the youngest of the" is now "{X_nameDef}, one of the"
- Rimfolk_CampKinds_Road: "{X_nameDef}, a lean old drover with" is now "{X_nameDef}, a drover with"
- Rimfolk_CampKinds_Road: "{X_nameDef}, the youngest of the" is now "{X_nameDef}, one of the"
- Rimfolk_CampKinds_Road: "[heal] {X_nameDef}, the eldest of the" is now "[heal] {X_nameDef}, one of the"
- Rimfolk_CampKinds_Scholars: "{X_nameDef}, the scholars' youngest, has decided" is now "{X_nameDef}, one of the scholars, has decided"
- Rimfolk_CampKinds_Variety2: "{X_nameDef}, the youngest of the" is now "{X_nameDef}, one of the"
- Rimfolk_CampKinds_Variety2: "baskets. {X_nameDef}, the eldest, asked {A_nameDef}" is now "baskets. {X_nameDef}, who leads them, asked {A_nameDef}"
- Rimfolk_CampKinds_Variety2: "{X_nameDef}, the youngest of the" is now "{X_nameDef}, one of the"
- Rimfolk_CampKinds_Variety2: "[their:medieval] The eldest of the" is now "[their:medieval] The leader of the"
- Rimfolk_CampKinds_Variety3: "{X_nameDef}, the youngest of the" is now "{X_nameDef}, one of the"
- Rimfolk_CampKinds_Variety3: "{X_nameDef}, the youngest of the" is now "{X_nameDef}, one of the"
- Rimfolk_CampKinds_Variety3: "{X_nameDef}, the youngest of the" is now "{X_nameDef}, one of the"
- Rimfolk_CampKinds_Winter: "{X_nameDef}, the ice fishers' youngest, says {X_pronoun}" is now "{X_nameDef}, one of the ice fishers, says {X_pronoun}"
- Rimfolk_CampKinds_Winter: "{X_nameDef}, the youngest of the" is now "{X_nameDef}, one of the"

**Told as done, but it is about to happen** (12)

- Council_CampKinds_More: "players {PLACE} put on a" is now "players {PLACE} are putting on a"; "the colony was invited. {X_nameDef} played the lord." is now "the colony is invited. {X_nameDef} plays the lord."
- Council_CampKinds_More: "circus {PLACE} put on its" is now "circus {PLACE} puts on its"; "and a strongman who lifted {A_nameDef} over his head. The colony laughed itself hoarse." is now "and a strongman. The colony is invited."
- Council_CampKinds_More: "dancers {PLACE} danced the story" is now "dancers {PLACE} dance the story"; "dark. {X_nameDef} danced the beast." is now "dark. {X_nameDef} dances the beast."
- Council_CampKinds_More: "mummers {PLACE} played the old" is now "mummers {PLACE} play the old"; "{X_nameDef}'s dragon chased the colony's youngest all round" is now "{X_nameDef}'s dragon will chase the fool all round"
- Council_CampKinds_More: "camped {PLACE} tried their new" is now "camped {PLACE} are trying their new"; "tonight. {X_nameDef} forgot a whole scene and made up another on the spot, and the colony liked it better." is now "tonight. {X_nameDef} has the main part, and has promised to make up any scene that gets forgotten."
- Rimfolk_CampKinds_Land: "and {X_nameDef} had the beasts" is now "and {X_nameDef} will have the beasts"; "the colony watched." is now "the colony watches."
- Rimfolk_CampKinds_Road: "minstrels {PLACE} played until late" is now "minstrels {PLACE} play until late"; "the music carried all over" is now "the music will carry all over"; "{X_nameDef}'s fiddle could be heard" is now "{X_nameDef}'s fiddle will be heard"
- Rimfolk_CampKinds_Road: "The minstrels played the old" is now "The minstrels play the old"; "the colony sang along from" is now "the colony can sing along from"
- Rimfolk_CampKinds_Road: "The minstrels played round their" is now "The minstrels play round their"; "and colonists went out wrapped" is now "and colonists can go out wrapped"
- Rimfolk_CampKinds_World: "the wardens came up to" is now "the wardens will come up to"; "lantern, and stood the colony's watch with it until the small hours." is now "lantern, and stand the colony's watch there a few hours."
- Rimfolk_Camps: "the colony was invited. {X_nameDef} was the star" is now "the colony is invited. {X_nameDef} is the star"
- Rimfolk_WorldBeyond: "There was music at" is now "There is music at"; "and it carried across to" is now "and it will carry across to"

**A sentence opened with a pronoun placeholder, which the game shows lowercase: the name instead** (6)

- Rimfolk_CampKinds_Land: "the colony. {X_pronoun} says the" is now "the colony. {X_nameDef} says the"
- Rimfolk_CampKinds_Land: "stay on. {X_pronoun} says the" is now "stay on. {X_nameDef} says the"
- Rimfolk_CampKinds_Land: "others go. {X_pronoun} says this" is now "others go. {X_nameDef} says this"
- Rimfolk_CampKinds_PeoplesLore: "the colony. {X_pronoun} asks whether" is now "the colony. {X_nameDef} asks whether"
- Rimfolk_CampKinds_Scholars: "an afternoon. {X_pronoun} has offered" is now "an afternoon. {X_nameDef} has offered"
- Rimfolk_CampKinds_Winter: "of it. {X_pronoun} asked {A_nameDef}," is now "of it. {X_nameDef} asked {A_nameDef},"

**The mourners: the colony stands with them a while, not at their going** (5)

- Rimfolk_CampKinds_Road: "the road when they go" is now "the road with them"
- Rimfolk_CampKinds_Road: "the road while the mourners carry their dead past. Among many" is now "the road a while with the mourners and their dead. Among many"
- Rimfolk_CampKinds_Road: "the road when the mourners go on. {X_nameDef} thanked" is now "the road with the mourners a while. {X_nameDef} thanked"
- Rimfolk_CampKinds_Road: "When the mourners leave, the colony will" is now "The colony will"; "road in silence. {X_nameDef} pressed" is now "road in silence with the mourners. {X_nameDef} pressed"
- Rimfolk_CampKinds_Road: "colony will see the mourners off at the road." is now "colony will stand with the mourners by the road."

**Word** (4)

- Rimfolk_Holding: "{X_nameDef} came to the gate at dusk and did not sit down. The {NAME}" is now "{X_nameDef} sent word at dusk. The {NAME}"; "go on in the morning, {X_pronoun} said," is now "go on soon, {X_pronoun} said,"
- Rimfolk_Holding: "{X_nameDef} came to the gate with the children hanging back a few paces. The family" is now "{X_nameDef} sent word at dusk. The family"; "move on in the morning, {X_pronoun} said," is now "move on soon, {X_pronoun} said,"
- Rimfolk_Holding: "{X_nameDef} nodded as if" is now "{X_nameDef} took the answer as if"; "it, and went back to tell the others." is now "it, and told the others."; "go on in the morning." is now "go on soon."
- Rimfolk_Holding: "it. {X_nameDef} shook every hand at the gate twice." is now "it. {X_nameDef} sent thanks back twice over."

## World events (178 lines)

**Nobody at the gate that no effect brings: a message came** (25)

- Council_WS_Hardship: "the first rider {FACTION} sent" is now "the first word {FACTION} sent"; "to the colony and asked at the gate for {FRIEND_nameDef} by" is now "to the colony, asking after {FRIEND_nameDef} by"
- Council_WS_Hardship: "The rider ties a red cord to the colony's gatepost, the sign of a closed gate, and goes" is now "The rider marks the colony down as a closed gate and goes"
- Council_WS_Places: "A merchant's boy came up to the gate this morning to ask, very politely, whether his" is now "Word came from a merchant's boy, asking very politely whether his"; "might water at the colony's well again on" is now "might water on the colony's land again on"
- Council_WS_Powers: "preachers are shouting, by the third their followers are, and both" is now "preachers are shouting at each other, and both"
- Council_WS_Powers: "have sent riders to the" is now "have sent word to the"
- Council_WS_Powers: "Riders came to" is now "Word came to"; "day apart, one from {FACTION} and one from {ENEMY}," is now "day apart, from {FACTION} and from {ENEMY},"
- Council_WS_Powers: "The riders of {FACTION} and" is now "{FACTION} and"; "{ENEMY} are waiting for" is now "{ENEMY} are both waiting for"; "an answer, at opposite ends of the colony's yard. Hosting means" is now "an answer, and neither will speak to the other. Hosting means"
- Council_WS_Powers: "The riders of {FACTION} and {ENEMY} have kept to opposite sides of the yard all morning, waiting for" is now "{FACTION} and {ENEMY} are both waiting for"; "the colony's answer. Hosting the" is now "the colony's answer, and neither will speak to the other. Hosting the"
- Council_WS_Powers: "Both riders leave with the same" is now "Both peoples are sent the same"
- Council_WS_Powers: "The riders are told" is now "Both peoples are told"
- Council_WS_Powers: "paid now," {X_nameDef} said at the gate, with two sergeants standing behind. "For that," is now "paid now," runs the message {X_nameDef} sent from the company's camp. "For that,"
- Council_WS_Powers: "[againstWarlord] {X_nameDef} came to the gate in person. For {COUNT}" is now "[againstWarlord] {X_nameDef} sent word from the company's camp. For {COUNT}"
- Council_WS_Powers: "The company's cook comes for the food with a handcart, and in the morning the company" is now "The food is carried out for the company's cook, and soon the company"
- Council_WS_Powers: "{X_nameDef} shrugs and goes back to camp. There are" is now "{X_nameDef} takes the answer with a shrug. There are"
- Council_WS_Region: "boot out at the gate to show" is now "boot out to show"
- Council_WS_Region: "line. / Their foreman came up to the gate to say" is now "line. / Their foreman sent word to say"
- Council_WS_Region: "have been stopping at the gate to say so." is now "have been saying so all along the road."
- Council_WS_Region: "arm through," says the man at the gate, one of {NAME}'s, and holds up a scrap" is now "arm through," runs the word from one of {NAME}'s old band, who has a scrap"
- Council_WS_Region: "old band is at the gate with a scrap of map in a dirty hand. Only a" is now "old band has sent word, in a dirty hand, of a scrap of map. Only a"
- Council_WS_Region: "Send the map's" is now "Send word of the map's"
- Council_WS_Region: "The bandit is sent on to {PLACE} under guard, map and" is now "Word of the bandit goes on to {PLACE}, map and"
- Council_WS_Region: "bandit is sent off down the road, map and all, to try the next gate." is now "bandit is told no, and can try the next gate, map and all."
- Council_WS_Warlord: "Now another rider is at the gate, talking about a price. / The" is now "Now another rider's message has come, naming a price. / The"
- Rimfolk_WS_Beyond2: "people now. The tinker came back through last week with word that the" is now "people now. Word came through last week that the"
- Rimfolk_WS_FollowUps: "The merchant house's last caravan of the season brought a note" is now "The merchant house has sent a note"

**An hour the words can't know** (67)

- Council_WS_Hardship: "with the rider before dark. In {PLACE}," is now "with the rider. In {PLACE},"
- Council_WS_Hardship: "with the medicine before dark." is now "with the medicine."
- Council_WS_Hardship: "bowl each. Before dark, one of them" is now "bowl each. One of them"
- Council_WS_Hardship: "in {PLACE} by tomorrow." is now "in {PLACE} within a day."
- Council_WS_Hardship: "colony's gate this morning and was" is now "colony's gate and was"
- Council_WS_Hardship: "people away. This morning a girl was" is now "people away. A girl was"
- Council_WS_Hardship: "the stove. By evening she knows" is now "the stove. Before long she knows"
- Council_WS_Hardship: "the frozen river this morning. {FACTION} are" is now "the frozen river. {FACTION} are"
- Council_WS_Hardship: "starts back while there's still light." is now "starts back for home."
- Council_WS_Hardship: "of the snow at dusk, caught between" is now "of the snow, caught between"
- Council_WS_Hardship: "they walked the last of the light to the" is now "they walked to the"
- Council_WS_Hardship: "stop. Locusts. By evening a rider had brought word" is now "stop. Locusts. Since then a rider has brought word"
- Council_WS_Hardship: "swarm came over the hill at noon and settled on the" is now "swarm came down on the"; "brown snow. By evening it had lifted and" is now "brown snow. Now it has lifted and"; "and so had the crops:" is now "and so have the crops:"
- Council_WS_Hardship: "colony's fields at noon and was gone by evening, leaving {COUNT}" is now "colony's fields and was gone again, leaving {COUNT}"
- Council_WS_Hardship: "river path this morning with a" is now "river path with a"
- Council_WS_Land: "[!nothingToMend,taught] {X_nameDef} arrived at noon, looked at" is now "[!nothingToMend,taught] {X_nameDef} has come, looked at"; "best light. By evening it was done. {ITEM} / {B_nameDef}" is now "best light. Now it is done. {ITEM} / {B_nameDef}"
- Council_WS_Land: "[!nothingToMend,!taught] {X_nameDef} arrived at noon, looked at" is now "[!nothingToMend,!taught] {X_nameDef} has come, looked at"; "best light. By evening it was done. {ITEM}" is now "best light. Now it is done. {ITEM}"
- Council_WS_Land: "[nothingToMend] {X_nameDef} arrived at noon and looked" is now "[nothingToMend] {X_nameDef} has come and looked"; "was handed back at the gate." is now "was handed back."
- Council_WS_Land: "[!nothingToMend] {X_nameDef} left the colony at dawn for the" is now "[!nothingToMend] {X_nameDef} has left the colony for the"; "its thanks. {A_nameDef} walked the healer to the edge of the colony's land and stood there a while after." is now "its thanks."
- Council_WS_Land: "[nothingToMend] {X_nameDef} left the colony at dawn for the" is now "[nothingToMend] {X_nameDef} has left the colony for the"
- Council_WS_Land: "the water this morning that don't" is now "the water that don't"
- Council_WS_Mods_Factions: "passed the colony this morning, headed for" is now "passed the colony, headed for"; "{PLACE}. {A_nameDef} sold one of" is now "{PLACE}. {A_nameDef} tried to sell one of"; "worth, and is very pleased with the sale." is now "worth, and nearly managed it."
- Council_WS_Places: "The prospectors in the colony's creek turned up" is now "The prospectors on the colony's land turned up"; "as a thumb this morning, and they're" is now "as a thumb, and they're"
- Council_WS_Powers: "the gate before noon, and one" is now "the gate first, and one"
- Council_WS_Powers: "the same morning and have" is now "the same day and have"; "the gate since noon. One speaks" is now "the gate ever since. One speaks"
- Council_WS_Powers: "at it since morning. / {A_nameDef} says the" is now "at it ever since. / {A_nameDef} says the"
- Council_WS_Powers: "debate runs until the lamps are lit. Neither preacher" is now "debate runs long. Neither preacher"
- Council_WS_Powers: "the same afternoon. {X_nameDef} says" is now "the same day. {X_nameDef} says"
- Council_WS_Powers: "broke camp this morning and rode" is now "broke camp and rode"
- Council_WS_Powers: "the colony this morning in a" is now "the colony in a"
- Council_WS_Powers: "the road this morning with nothing" is now "the road with nothing"
- Council_WS_Region: "shovel and an afternoon turned up" is now "shovel and a little digging turned up"
- Council_WS_Warlord: "gone again before dark. Their hunters" is now "gone again soon after. Their hunters"
- Council_WS_Warlord: "{NAME} scattered before nightfall. / In {PLACE} they" is now "{NAME} scattered soon after. / In {PLACE} they"
- Council_WS_Warlord: "to the colony tonight." is now "to the colony."
- Rimfolk_WS_Beyond: "came from {FACTION} this morning, in a" is now "came from {FACTION}, in a"
- Rimfolk_WS_Beyond: "head down. By evening both families" is now "head down. Before long both families"
- Rimfolk_WS_Beyond: "{A_nameDef} comes back at dusk with a" is now "{A_nameDef} goes out and comes back with a"
- Rimfolk_WS_Beyond: "the colony's hill: a sheaf" is now "the colony's far ground: a sheaf"; "over it all evening, and is" is now "over it since, and is"
- Rimfolk_WS_Beyond2: "the colony this afternoon: a" is now "the colony in the afternoon: a"
- Rimfolk_WS_Beyond2: "the colony at dusk. Among {FACTION}," is now "the colony in the afternoon. Among {FACTION},"
- Rimfolk_WS_Beyond2: "humming it all afternoon." is now "humming it ever since."
- Rimfolk_WS_Mods_Peoples: "to the colony this morning, very upright," is now "to the colony, very upright,"; "fine thing, he said. A" is now "fine thing, the herald said. A"
- Rimfolk_WS_Mods_Peoples: "the colony at midday. One of" is now "the colony today. One of"; "toward the wall as they" is now "toward the colony as they"
- Rimfolk_WS_Mods_Peoples: "road slowly this afternoon, looking hard" is now "road slowly today, looking hard"; "at the walls. The colony is keeping its gate shut and its lamps low, and the night watch has been doubled." is now "at the colony."
- Rimfolk_WS_New: "colony's {THING} since first light. It started" is now "colony's {THING} for a good while. It started"
- Rimfolk_WS_New: "from orbit this morning and walked" is now "from orbit and walked"
- Rimfolk_WS_New: "will be dark before they" is now "will be late before they"
- Rimfolk_WS_New: "go on in the dusk." is now "go on their way."
- Rimfolk_WS_Variety3: "down at {NAME} this morning, and four" is now "down at {NAME}, and four"
- Rimfolk_WS_Wider: "to {ENEMY} by nightfall: the colony" is now "to {ENEMY} before long: the colony"
- Rimfolk_WS_Wider: "the gate after dark with a" is now "the gate with a"
- Rimfolk_WS_Wider: "at the gate after dark, knowing very" is now "at the gate, knowing very"
- Rimfolk_WS_Wider: "goes off into the dark." is now "goes off down the road."
- Rimfolk_WS_Wider: "cart on into the dark." is now "cart on down the road."
- Rimfolk_WS_Wider: "on the road before morning." is now "on the road."
- Rimfolk_WS_Wider: "of {ENEMY} went through the gate this morning. / Both peoples say" is now "of {ENEMY} have gone through the gate already. / Both peoples say"
- Rimfolk_WS_Wider2: "the road-keeper's sledge before dark. By the" is now "the road-keeper's sledge. By the"; "week there are new poles" is now "week there will be new poles"
- Rimfolk_WS_Wider2: "near the shore this morning, where a" is now "near the shore, where a"
- Rimfolk_WS_Wider2: "near the shore at first light. The driver" is now "near the shore. The driver"
- Rimfolk_WS_Wider2: "his way at noon." is now "his way once he is warm."
- Rimfolk_WS_Wider2: "off the ice at dusk, frost on" is now "off the ice, frost on"
- Rimfolk_WS_Wider2: "for {PLACE} in the dark." is now "for {PLACE} without stopping."
- Rimfolk_WS_Wider2: "{NAME}, and night has caught them at the colony's gate: three speakers," is now "{NAME}, and the road has brought them to the colony's gate for the night: three speakers,"
- Rimfolk_WS_World: "and spends the morning with them" is now "and spends a few hours with them"
- Rimfolk_WS_World: "the gate until the light goes, and come" is now "the gate for a few hours, and come"
- Rimfolk_WS_World: "the gate until the light goes, and get" is now "the gate for a few hours, and get"; "walks home in the dark." is now "walks home angry."

**Told as done, but it comes later or not on the map** (13)

- Council_WS_Hardship: "{A_nameDef} filled six pages" is now "{A_nameDef} fills six pages"; "before {A_pronoun} was satisfied. The rider left with the" is now "before {A_pronoun} is satisfied. The rider leaves with the"
- Council_WS_Hardship: "to sleep. The next morning they are up before anyone else, asking what" is now "to sleep. They say they'll be up before anyone else tomorrow, asking what"
- Council_WS_Region: "{A_nameDef} kept a day" is now "{A_nameDef} will keep a day"; "gate and spent the next" is now "gate and spend the next"; "other people's chores, and has been easier company since." is now "other people's chores."
- Council_WS_Region: "meal, and sleeps for a day and a night." is now "meal, and then sleeps like the dead."
- Council_WS_Region: "meal, and sleeps for most of two days. When they" is now "meal, and sleeps. When they"; "wake, they ask what" is now "wake, they mean to ask what"
- Council_WS_Region: "{A_nameDef} spent the day" is now "{A_nameDef} spends the day"; "tea and learned a great" is now "tea and learns a great"; "The salvagers paid as promised when they came back for the core." is now "The salvagers pay as promised."
- Rimfolk_WS_Beyond2: "bed. {A_nameDef} has already started clearing the hall. / No town turns" is now "bed. {A_nameDef} would start clearing the hall at once. / No town turns"
- Rimfolk_WS_Beyond2: "gate, and within the week a few" is now "gate, and soon a few"; "a day are coming down" is now "a day will be coming down"; "old lane past the colony's fields." is now "old lane across the colony's land."
- Rimfolk_WS_Beyond2: "{FACTION}'s tollmen found carts on" is now "{FACTION}'s tollmen will find carts on"; "colony's lane within the week and turned them back" is now "colony's lane before the week is out and turn them back"; "colony's own fence. The colony" is now "colony's own edge. The colony"
- Rimfolk_WS_Variety3_Roads: "paid for. A few days later {FACTION}'s wardens found the bottles" is now "paid for. In a few days {FACTION}'s wardens will find the bottles"; "{PLACE} and asked where they" is now "{PLACE} and ask where they"; "from, and were told it" is now "from, and be told it"
- Rimfolk_WS_Variety3_Roads: "paid for. Two days later {FACTION}'s wardens stopped the cart" is now "paid for. In two days {FACTION}'s wardens will stop the cart"; "road and pulled up the" is now "road and pull up the"; "the carter told them where" is now "the carter will tell them where"
- Rimfolk_WS_Wider: "{FACTION}'s riders came up the road two days later asking questions, and found the cart." is now "{FACTION}'s riders will come up the road in a day or two asking questions, and find the cart."; "The driver got away. The colony's name went back to" is now "The driver will get away, but the colony's name will go back to"
- Rimfolk_WS_Wider: "table, and has the colony's cart axle greased before dark." is now "table, and asks what needs doing before the day is out."

**A thing the map doesn't show** (5)

- Council_WS_Hardship: "and the colony can take the red cord off its gatepost. {FACTION} count" is now "and the colony's gate can open to travelers again. {FACTION} count"
- Council_WS_Powers: "{A_nameDef} as witness. / There is a fresh dent in the table where someone brought a fist down on the second day. The colony has decided to leave it." is now "{A_nameDef} as witness."
- Rimfolk_WS_Beyond2: "The colony's painted token is nailed over the gate now, as a souvenir of the most expensive piece of wood it owns." is now "The colony's name is still on the toll-keeper's list, the most expensive line of ink it has ever paid for."
- Rimfolk_WS_Beyond2: "forgotten it. One of the exiles has planted a row of beans by the wall, from seed brought out of {PLACE}." is now "forgotten it."
- Rimfolk_WS_World: "the custom. {X_pronoun} has brought a jar of something as a gift, and a question:" is now "the custom. {X_nameDef} has brought a question:"

**No well, wall, stream, creek, ridge, hill, stones or trees a colony may not have** (46)

- Council_WS_Hardship: "luck against its walls. Food given" is now "luck against it. Food given"
- Council_WS_Land: "From the colony's wall the dust" is now "From the colony the dust"
- Council_WS_Land: "few nights out at the old stones on the hill, where the ley lines cross, to see what the surge can teach. / It" is now "few nights up with the surge, to see what it can teach. / It"
- Council_WS_Land: "few nights out at the old stones on the hill, where the lines cross, to learn what the surge has to" is now "few nights up with the surge, to learn what it has to"
- Council_WS_Land: "{A_nameDef} comes back from the stones after three nights, red-eyed and" is now "{A_nameDef} sits up nights over the surge, red-eyed and"; "too fast, with a notebook full of diagrams that" is now "too fast, and fills a notebook with diagrams that"
- Council_WS_Land: "{A_nameDef} comes back from the stones pale and" is now "{A_nameDef} comes away from the surge pale and"; "say what happened out there. The notebook" is now "say what happened. The notebook"
- Council_WS_Mods_Factions: "has come out of the trees to the" is now "has come to the"
- Council_WS_Mods_Factions: "work the stream below the colony for a" is now "work the colony's land for a"; "color in that water. / {A_nameDef} has looked at the stream every day" is now "color in this ground. / {A_nameDef} has walked that ground every day"
- Council_WS_Mods_Factions: "panned the stream until dark, singing, and" is now "panned the colony's land, singing, and"
- Council_WS_Mods_Factions: "panned the stream until dark and found" is now "panned the colony's land and found"
- Council_WS_Places: "and drink from the well. / Some at the" is now "and drink the colony's water. / Some at the"
- Council_WS_Places: "Somebody puts a sign on the well asking them" is now "Somebody puts up a sign asking them"
- Council_WS_Places: "colony's own hills. / Most will find" is now "colony's own land. / Most will find"
- Council_WS_Places: "the colony's hills. / Most of them" is now "the colony's land. / Most of them"
- Council_WS_Places: "to work the creek beds on the colony's" is now "to work the colony's"
- Council_WS_Places: "the colony's creek beds, and they'll" is now "the colony's land, and they'll"
- Council_WS_Places: "The colony's hills are quiet again," is now "The colony's land is quiet again,"; "wasn't in them. The rush" is now "wasn't in it. The rush"
- Council_WS_Region: "their animals at the colony's wells for a" is now "their animals on the colony's land for a"
- Council_WS_Region: "down the hill to find" is now "down the road to find"
- Rimfolk_WS_Beyond: "the colony's hill." is now "the colony's far ground."
- Rimfolk_WS_Beyond: "a fire on the hill, blankets, something" is now "a fire outside, blankets, something"
- Rimfolk_WS_Beyond: "a fire on the hill so the" is now "a fire lit outside so the"
- Rimfolk_WS_Beyond: "night watch on the hill" is now "night watch by a fire"
- Rimfolk_WS_Beyond: "is laid on the hill for tonight." is now "is laid outside for tonight."; "can come up and sit" is now "can come and sit"
- Rimfolk_WS_Beyond: "the colony's hill has the" is now "the colony's far ground has the"; "to sit on it" is now "to sit out on it"
- Rimfolk_WS_Beyond: "The colony's hill is above the valley" is now "The colony's far ground is clear of the valley"; "the case up the slope." is now "the case out there."
- Rimfolk_WS_Beyond: "the colony's hill, the darkest and highest for miles," is now "the colony's far ground, the darkest for miles,"
- Rimfolk_WS_Beyond: "{A_nameDef} sits up with" is now "{A_nameDef} will sit up with"; "on the hill her first" is now "on the far ground her first"; "night here, and comes away with names for" is now "night here, to learn names for"
- Rimfolk_WS_Beyond: "told the hill isn't open" is now "told the colony's land isn't open"
- Rimfolk_WS_Beyond: "the colony's hill: her finished" is now "the colony's far ground: her finished"
- Rimfolk_WS_Beyond: "the night on the hill, with the" is now "the night out under {NAME}, with the"
- Rimfolk_WS_Beyond: "bells come over the hill before the" is now "bells come before the"
- Rimfolk_WS_Beyond: "reach the colony's walls before the" is now "reach the colony before the"
- Rimfolk_WS_FollowUps: "at its well. The next" is now "at its gate. The next"
- Rimfolk_WS_FollowUps: "but the colony's well has become" is now "but the colony has become"
- Rimfolk_WS_Mods_Peoples: "it go by from the wall." is now "it go by."
- Rimfolk_WS_Mods_Peoples: "at the walls. They know" is now "at the colony. They know"
- Rimfolk_WS_Wider: "down the hill." is now "down the road."
- Rimfolk_WS_Wider2: "on the ridge above the colony" is now "on the high ground near the colony"; "saw it from the wall and woke" is now "saw it and woke"
- Rimfolk_WS_Wider2: "on the ridge above the colony" is now "on the high ground near the colony"; "likes up the ridge at first" is now "likes up at first"
- Rimfolk_WS_Wider2: "will go up the ridge at first" is now "will go out at first"
- Rimfolk_WS_Wider2: "off the ridge" is now "off the high ground"
- Rimfolk_WS_Wider2: "The colony stays off the ridge. {NAME} grazes there until noon and then is gone over the top." is now "The colony keeps its distance. {NAME} grazes a while longer and then is gone."
- Rimfolk_WS_Wider2: "the morning on the ridge, when it" is now "the morning they went up to see it, when it"
- Rimfolk_WS_World: "the colony's side of the hill in the" is now "the colony's land in the"
- Rimfolk_WS_World: "looks the colony's walls over with" is now "looks the colony over with"

**The creature only bolts** (1)

- Council_WS_Land: "one may regret trying." is now "one may watch it bolt."

**What is given or done** (4)

- Council_WS_Places: "The colony builds a shelter" is now "The colony sends out timber for a shelter"
- Council_WS_Region: "family sends a loaf of bread and their thanks." is now "family sends their thanks."
- Rimfolk_WS_Beyond2: "gate and leaves a painted wooden token, to be shown at the chain." is now "gate and writes the colony's name on his list for the chain."
- Rimfolk_WS_FollowUps: "colony carries bread and soup out to" is now "colony carries food out to"

**?** (1)

- Council_WS_Places: "their way home and left the trough full. The merchants" is now "their way home, and left their thanks. The merchants"

**Silver and prizes still on their way with a courier** (9)

- Council_WS_Places: "fair's carter brought back the" is now "fair's carter is bringing back the"; "contest in {SKILL} and brought the prize home. / The colony's" is now "contest in {SKILL}; the prize is on its way home. / The colony's"
- Council_WS_Places: "colony's takings came back with" is now "colony's takings are coming back with"
- Council_WS_Places: "fair's carter has been around with" is now "fair's carter is coming around with"
- Council_WS_Places: "The prize came home with the news, and so did the bragging." is now "The prize is on its way home, and the bragging came ahead of it."
- Council_WS_Places: "the colony's creeks stopped at the gate with the colony's" is now "the colony's land are sending the colony's"
- Council_WS_Powers: "{X_nameDef} has left the colony's share at the gate and ridden" is now "{X_nameDef} has sent the colony's share and ridden"
- Council_WS_Powers: "{X_nameDef} has left the colony's share at the gate and ridden" is now "{X_nameDef} has sent the colony's share and ridden"
- Rimfolk_WS_Beyond: "their carter has brought the colony's" is now "their carter is bringing the colony's"
- Rimfolk_WS_Wider2: "colony's share has come up the" is now "colony's share is coming up the"

**A herald, envoy or rider who may be a woman is not "he"** (6)

- Rimfolk_WS_Mods_Peoples: "freshly polished, presented himself to {A_nameDef} and" is now "freshly polished, came before {A_nameDef} and"; "also do, he allowed, for" is now "also do, the herald allowed, for"
- Rimfolk_WS_Mods_Peoples: "thanked for his trouble and" is now "thanked for the trouble and"; "watch it from the wall." is now "watch it go."
- Rimfolk_WS_Mods_Peoples: "the envoy said. He said it" is now "the envoy said, and said it"
- Rimfolk_WS_Mods_Peoples: "the gathering. He looked at the walls while he waited for the" is now "the gathering. The rider looked the colony over while waiting for the"; "answer, and at the horses." is now "answer, and its horses."
- Rimfolk_WS_Mods_Peoples: "and before he was out" is now "and before the envoy was out"; "{A_nameDef} had riders going to every" is now "{A_nameDef} had sent word to every"; "within reach with word of how" is now "within reach of how"
- Rimfolk_WS_Mods_Peoples: "made no difference to him, and rode" is now "made no difference, and rode"; "off. Everyone on the wall watched him go." is now "off. Everyone watched the envoy go."

**Nobody else at the gate** (1)

- Rimfolk_WS_Mods_Peoples: "moot's fire". The two orcs behind him said nothing at all, very loudly." is now "moot's fire"."
