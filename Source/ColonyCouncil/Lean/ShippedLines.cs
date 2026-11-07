using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// What a line of a flavour pool needs beyond its own tags, by its text as written (tags and all): lines whose words
    /// proved untrue in a case their tags let through, found after they shipped. A shipped line's text and tags stay as
    /// they are (Tools/textcheck.py), so the condition that makes it true is asked here (in the lines' own tag syntax), and
    /// a line true in no case its pool is drawn for is retired; better words for the other case go in a file of their own.
    /// The flavour pools' counterpart of <see cref="OutsiderTalkMath.AlsoWhen"/> for conversation pieces; Flavor.Match asks
    /// it once per line. Pure (no game types): Tools/Tests checks every line here is still in the defs.
    /// </summary>
    public static class ShippedLines
    {
        /// <summary>A shipped line and the condition it needs besides its own tags.</summary>
        public static readonly Dictionary<string, string> AlsoWhen = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // The renownup bubble (Rimfolk_Lines_Renown.xml) is said as the colony's name first carries to a town nearby too,
            // where it is spoken of in the next town's market, talked about in the next town, told at the nearest camp's
            // fire: a song and the papers are the region's band or wider (colony:renowned; the round d text pass asked
            // house:renowned of them while they were a house's words).
            ["[renownup,us,medieval] I hear there's a song with our name in it now."] = RenownMath.RenownedTag,
            ["[renownup,us,industrial] Somebody said our name made the papers, of all things."] = RenownMath.RenownedTag,
            // The town's own words (Rimfolk_Lines_RenownMore.xml), written for a name known in a town nearby and no further:
            // their !house:renowned asks nothing now the houses are gone, so the colony's band is asked.
            ["[renownup,us,!house:renowned] Our name's going around nearby, apparently."] = "!" + RenownMath.RenownedTag,
            ["[renownup,us,tribal,!house:renowned] Our name has reached the next camp's fire before any of us did."] = "!" + RenownMath.RenownedTag,
            ["[renownup,us,medieval,!house:renowned] They say our name came up at the next town's market."] = "!" + RenownMath.RenownedTag,
            // The renowndown bubble is said as a name fades from the talk, most often from the next town's market, where no
            // singer ever sang it: the singers only for a name that was sung in the region's halls (RenownMath.BubbleTag),
            // the next town's words only for one that was talked of there and no further.
            ["[renowndown,us,medieval] The singers have found someone else to sing about."] = RenownMath.WasRenownedTag,
            ["[renowndown,us,!house:wasrenowned] The next town has stopped talking about us, it seems."] = "!" + RenownMath.WasRenownedTag,
            ["[renowndown,us,tribal,!house:wasrenowned] The nearest camp tells other stories at its fire now."] = "!" + RenownMath.WasRenownedTag,
            ["[renowndown,us,medieval,!house:wasrenowned] Nobody at the next town's market speaks of us now."] = "!" + RenownMath.WasRenownedTag,
            ["[renowndown,us,industrial,!house:wasrenowned] The next town's found something else to talk about."] = "!" + RenownMath.WasRenownedTag,
            ["[renowndown,us,spacer,!house:wasrenowned] Our name's gone quiet on the nearest settlement's channels."] = "!" + RenownMath.WasRenownedTag,
            // The text pass on the world's voices (6 November 2026). A chat's opening (Rimfolk_Outsiders.xml) was said to
            // anyone, however often the two had talked: the two that say they never met only to one never talked with.
            ["[greet,us] Welcome to the colony, I'm glad to finally meet you."] = "first",
            ["[greet,us] I don't think we've talked yet, have we?"] = "first",
            // Getting to know them said "I'm glad you came back" to one met for the first time: only on a visit since the last.
            ["[befriend,us] I'm glad you came back, I hoped you would."] = "returning",
            // A meal shared at the table praised whatever was on the plate, paste too: only for one that isn't awful
            // (Outsiders' meal tags the plate, fare:awful...).
            ["[mealtalk] This is good, who made it?"] = "!fare:awful",
            ["[mealtalk,them,theirLowtech] We'd sing over a meal like this at home."] = "!fare:awful",
            ["[mealend,them] That was a fine meal, thank you."] = "!fare:awful",
            // A war party's line about the enemy's archers (Rimfolk_CampKinds_War.xml) was said after any fight: only after
            // one with a people of bows and spears (the camp's foe tags).
            ["Their archers were a joke."] = "foe:lowtech",
            // A camp's lines over their heads were said at any hour: the three that name one only then (Camp.Bubble).
            ["They're biting this morning."] = "morning",
            ["It's quieter tonight."] = "evening|night",
            ["My back says it's evening."] = "evening",
            // A camp's farewell is told as they set off (CampLife.Leave), at whatever hour that is: the lines that name the
            // hour only then (CampLook.FarewellText tags it).
            ["The pilgrims from {FACTION} broke camp {PLACE} at dawn and walked on toward their holy place."] = "morning",
            ["The pilgrims from {FACTION} left at first light, singing under their breath so as not to wake the colony."] = "morning",
            ["The hermit's hut {PLACE} is empty. {X_nameDef} left in the night without a word, which is how {X_nameDef} arrived."] = "night",
            ["The surveyors from {FACTION} left before breakfast, carrying the colony's hills away with them on paper."] = "night|morning",
            ["The fishers from {FACTION} rolled up their lines at first light and walked on along the water, baskets on their backs."] = "morning",
            ["The beast show from {FACTION} led its beasts out at first light, the keepers walking beside them."] = "morning",
            ["The trainers' camp {PLACE} is empty this morning. They left early, {X_nameDef} said, to be at the next town by market day."] = "morning",
            ["The caravan from {FACTION} loaded up before dawn and went on toward the hills, bells ringing on the lead animal. {X_nameDef} promised to stop again next year."] = "night|morning",
            ["[!lowtech] The convoy from {FACTION} got its part and its engines running, and was gone by mid-morning in a cloud of exhaust and goodwill."] = "morning",
            ["The drovers from {FACTION} whistled the herd together before sunrise and drove it on down the road at an easy pace, the dog working the stragglers."] = "night|morning",
            ["The penitents from {FACTION} went on at dawn, barefoot, without a word. Their camp {PLACE} is swept so clean it's hard to tell anyone was there."] = "morning",
            ["[left:seeds] The tree-singers left at dawn, singing, and stopped at the edge of the woods to bow to the trees. They left a ring of planted seeds where their fire had been."] = "morning",
            ["The minstrels from {FACTION} went on toward the next town at noon, still playing, with the drum keeping time for the walkers."] = "day",
            ["The mourners from {FACTION} went on at first light, one of them carrying their dead and the others walking close around."] = "morning",
            // Stage 3's content pass (7 November 2026). The hand-over's ceremony (Rimfolk_HandOver.xml) is said by its head
            // while they are at it, else by anyone there, going or staying (PlayOutLord: the organizer is [speaker]): the
            // lines in the voice of those who stay only by the head.
            ["Those who go are welcome back here whenever they come."] = "speaker",
            ["We will keep the fields and the walls as you left them."] = "speaker",
            ["Nobody here forgets who built this place."] = "speaker",
            ["Send word when you have settled, and we will send ours."] = "speaker",
            ["[tribal] We keep the fire, and you carry its embers with you."] = "speaker",
            ["[medieval] We hold the keys now, and the gate stays open to you."] = "speaker",
            ["[industrial] We will run the township as carefully as you ran the colony."] = "speaker",
            ["[spacer] The charter is registered and the codes are ours from today."] = "speaker",
            ["[spacer] The systems stay up, and your channel stays open."] = "speaker",
            ["You are our people still, wherever you live."] = "speaker",
            ["It is ours to run now, and we will run it."] = "speaker",
            ["It will be quieter here without you."] = "speaker",
            // The welcome's meal (Rimfolk_ManyColonies.xml) is eaten by both, those who came and those already there: the
            // lines in the host's voice only by its host ([speaker]).
            ["Tell us how you did things back home."] = "speaker",
            ["You'll get used to the way the wind comes in here."] = "speaker",
            ["I'm glad you came to us."] = "speaker",
            ["Mind, we all take a turn at the dishes here."] = "speaker",
            ["I hope they don't miss the old place too much."] = "speaker",
            // A raid on a town the colony was (Rimfolk_WS_TownRaid.xml): a fight on its map can take more than one of the
            // colony's former people ([diedMany]), and these four name one; the ending's other [died] lines say one or many,
            // and its note names them under a line with no word for them (SituationWorker_TownRaid.EndingNote).
            ["[died] The raiders of {ENEMY} are gone from {PLACE}, but not without a price: {DEAD}, one of the colony's own once, fell beside the colony's fighters.\\n\\n{FACTION} will remember who came when they called."] = "!diedMany",
            ["[died] The raiders of {ENEMY} broke into {PLACE} while the colony's fighters were still in the fight, and took what they could. {DEAD}, one of the colony's own once, died in the fight.\\n\\nThe town still stands, poorer than it was."] = "!diedMany",
            ["[died,!others] {ENEMY} were beaten back at the gate of {PLACE}. {DEAD}, one of the colony's own once, was killed holding it.\\n\\nThe town has lost goods and fortune."] = "!diedMany",
            ["[died,others] {ENEMY} were beaten back at the gate of {PLACE}. {DEAD}, one of the colony's own once, was killed holding it, and so was {ITEM}.\\n\\nThe town has lost goods and fortune."] = "!diedMany",
            // The head's letter from a town the colony was (Rimfolk_TownAfter.xml): a tribal town's word is carried by heart
            // (its intro says so), so the lines that speak of writing are another age's.
            ["Write when you can. Letters from {COLONY} are read out at supper, every word of them."] = "!tribal",
            ["[flourishing] I never thought I would write this, but {TOWN} is doing better than {OLDCOLONY} ever did. You would hardly know the place."] = "!tribal",
            ["[asks] {ASKER} asks after {KIN} every time a letter goes out, so: how is {KIN}?"] = "!tribal",
            ["[asks,former] {ASKER} made me promise to write that {KIN} is missed here, more than {KIN} knows."] = "!tribal"
        };

        /// <summary>Shipped lines true in no case they are drawn for, never said again.</summary>
        public static readonly HashSet<string> Retired = new HashSet<string>(StringComparer.Ordinal)
        {
            // The player's decision of 5 November 2026: the colony is the family, never a dynasty. The houses' bubbles that
            // say "our family" as the colony's name moves (Rimfolk_Lines_Renown.xml, _RenownMore.xml): the name is the
            // colony's. Its own words are in Rimfolk_Lines_ColonyRenown.xml.
            "[renownup,us] They say our family's name is known well beyond the colony now.",
            "[renownup,us,tribal] The other camps tell of our family at their fires now, I hear.",
            "[renownup,us,medieval] Our family's name gets a nod in the markets now, I'm told.",
            "[renownup,us,industrial] Our family's getting a reputation out there.",
            "[renownup,us,spacer] Seems our family name carries further than our transmitter does.",
            "[renownup,us,!house:renowned] Seems the next town has heard of our family.",
            "[renownup,us,tribal,!house:renowned] The nearest camp tells of our family at its fire now.",
            "[renownup,us,medieval,!house:renowned] The next town knows our family's name now, I'm told.",
            "[renownup,us,industrial,!house:renowned] People in the next town have started asking about our family.",
            "[renownup,us,industrial,!house:renowned] Our family name gets mentioned in the next town now.",
            "[renownup,us,spacer,!house:renowned] Our family name turned up on the nearest settlement's channel.",
            "[renownup,us,spacer,!house:renowned] The nearest settlement has our family name in its feeds now.",
            // A marriage is a bond between the colony and their people made through one marriage, not two families or houses
            // joined (Rimfolk_Lines_Matches.xml, _MatchesMore.xml): their family or house deciding the match, the colony as
            // their kin, family helping family, and a grudge passed from the old to the young. Better words are in
            // Rimfolk_Lines_ColonyBonds.xml.
            "[matchask,us,lowtech] Our families could be one family, if your people are willing.",
            "[match,warm] We hoped one of you would ask, {WRITER}. The whole house is talking about it.",
            "[match,cordial] We've talked it over as a family, and we agree.",
            "[tie:marriage] We are family now, {WRITER}, and family writes back.",
            "[match:wronged,theirLowtech] The grudge passes from the old to the young here. Remember that.",
            "[match,warm,call] We'd have said yes before you finished asking, {WRITER}. Welcome to the family.",
            "[match,cordial,theirHightech] We have gone over the terms with the family, and we agree. Our good wishes to the two of them.",
            "[match,refusal,letter] We have no wish to tie our family to your colony. Do not take it as an insult, though some here meant it as one.",
            "[match,refusal,theirHightech,call] I'm sorry, {WRITER}. The family put it to a vote, and it was no.",
            "[greetings,cool,tie:marriage] Your letter came. The family is well.",
            "[news,cordial,tie:marriage] Here is our news, from one side of the family to the other.",
            "[help,warm,tie:marriage] Family helps family, {WRITER}. You didn't need to ask twice.",
            "[condolences,warm,tie:marriage] We grieve with you as kin, {WRITER}. Our house mourns too.",
            "[warning,warm,tie:marriage] Thank you for thinking of us. Kin warn kin, and we will do the same for you.",
            "[matchask,us,!lowtech] Our colony would like to propose a marriage between our families.",
            "[matchtalk,them,theirLowtech] Who would stand for each family on the day?",
            // A ward is a youth of their town, never of a notable's house (Rimfolk_Lines_Wards.xml, _WardsMore.xml); the
            // youth's own family is theirs, and stays.
            "[ward,warm,askward,theirLowtech] It is an honor to be asked. One of our house will come to learn from yours.",
            "[wardbye,them,theirLowtech] My house will remember your name.",
            "[ward,warm,askward,letter] Your letter was read to the whole family, and the youth we chose hasn't sat still since. They will come.",
            "[ward,cordial,askward,theirLowtech] A youth of our house will come. Teach them well and feed them well, as we would one of yours.",
            // The player's decision of 6 November 2026: a vendetta left open at its avenger's death passes to whoever was
            // closest to them, never to blood kin first (Secrets/VendettaMath.cs). The lines that made it the family's oath
            // or a family debt (Rimfolk_Pools_VendettaEnds.xml); a relative who takes it up, kin and close besides, has
            // words of their own in Rimfolk_Lines_VendettaCloseness.xml.
            "[kin] With {OTHER_nameDef} gone, {PAWN_nameDef} has taken up the family's oath, and the debt for {LOST} is still {FACTION}'s to pay.",
            "[kin] It's a family debt now: {FACTION}, {LOST}, and {OTHER_nameDef}. I'll see it paid.",
            // The herd's journal (Rimfolk_Pools_HerdStory.xml), found in the player's save (6 November 2026): a chick called
            // the first calf of spring, an old chicken gray at the muzzle, and a sitting at the pens "tonight" written as the
            // letter came, before the keeper had gone. Words by the animal's kind are in Rimfolk_Pools_HerdStory_Kinds.xml.
            "The first calf of spring, {PET_nameDef}, born this morning to {OTHER_nameDef}. I went down to the pens to look and got no work done before noon.",
            "First calf of the year and it's a beauty, {PET_nameDef}! I stood at the pens grinning like a fool.",
            "I sat with {PET_nameDef} at the pens tonight. Gray at the muzzle, slow to rise. A good leader, and not much longer, I think.",
            // The long discipline (Council_Arcs_Mods_Magic.xml), found in the player's save (6 November 2026): the teacher's
            // death read as the cause of the wall ("Dukialgac died before Barr could get past the wall"), and the art taken
            // away (its reason now [taken], as its own line was tagged) was told as practice let slide, the wall waiting for
            // another year. Better words are in Rimfolk_Arcs_MoreWords.xml.
            "[otherdead] {OTHER_nameDef} died before {PAWN_nameDef} could get past the wall in {PAWN_possessive} art.",
            "{PAWN_nameDef} has put {PAWN_possessive} training aside, with the wall still standing.",
            "I've let the practice slide. The wall can wait. Maybe I'll be ready for it another year.",
            // The herd's journal (Rimfolk_Pools_HerdStory.xml), left by the 0.106.1 pass: the first born's lines are written
            // as the letter comes, and the keeper's visit to the newborn is staged later that day (HerdStory.SpringCalf), so
            // "I stood by it", "I held my breath until it stood" and "Went to look" told it before it happened. Words that
            // look forward to it are in Rimfolk_Pools_HerdStory_Kinds.xml and Rimfolk_Pools_Animals_Kinds.xml.
            "Spring's first: {PET_nameDef}, a young {KIND}, on its legs within the hour. I stood by it like it was the first ever born.",
            "{PET_nameDef}, the first of spring. I held my breath until it stood, and then I laughed out loud.",
            "First young of the spring. {PET_nameDef}. Healthy. Went to look, as if I'd never seen one.",
            // Words at a grave (Council_Eulogies.xml) said of anyone, a newcomer of two days or a child as much as an old hand:
            // they hauled the stone for most of the walls, they stayed through the raids and the hungry seasons. Their fellows
            // for one two years or more with the colony ([longtime]) are in Rimfolk_Lines_Eulogies_More.xml.
            "{OTHER_nameDef} hauled the stone for most of these walls. We'll keep them standing.",
            "{OTHER_nameDef} stayed through the raids and the hungry seasons, and never talked about leaving.",
            // The text pass on the world's voices (6 November 2026). A camp's farewell is told as they pack up and set off
            // (CampLife.Leave): what they take down goes with them (the fire too), and only the hermit, the healers and the
            // mourners walk to the colony before they go (their farewellWalk). The lines that told of a wave, a bow or a word
            // at the gate nobody walked to, a sound heard after they were out of sight, or ash and bones left round a fire
            // that is gone. New farewells for each kind are in Rimfolk_CampWords_Voices.xml.
            "The hunters from {FACTION} were gone before dawn. All that's left {PLACE} is a ring of ash and a few cracked bones.",
            "The hunters of {FACTION} broke camp at first light. One of them lifted a hand toward the colony from a long way off, and then they were over the rise and out of sight.",
            "The family from {FACTION} packed up their camp {PLACE} and went on down the road. {X_nameDef} waved until the colony was out of sight.",
            "The procession from {FACTION} lifted its shrine at dawn and went on singing up the road. The colony could hear them long after they'd gone out of sight.",
            "The procession from {FACTION} lifted its shrine and went, bells first. At the edge of the colony's land its leader turned and blessed the place once more, over one shoulder.",
            "The players from {FACTION} took a last bow at the gate and went on to the next town, still arguing about the second act.",
            "The fishers' camp {PLACE} is empty, and there's a ring of fish bones round the cold fire.",
            "The drovers from {FACTION} moved the herd on at first light. The colony could hear the bells and the dogs long after they'd gone over the ridge.",
            "The penitents left before sunrise. {X_nameDef} stopped at the gate to bow, and then they were gone up the road.",
            "The minstrels from {FACTION} left the way they came, singing. The colony heard the last verse from the road.",
            "{X_nameDef} shook every hand at the gate, thanked the colony twice, and led the scholars off with the pack beast complaining behind them.",
            "The fighters of {FACTION} are gone from {PLACE}, leaving a ring of trampled grass and a cold fire. {X_nameDef} said, at the gate: \"Next time, you come to us.\"",
            "{FACTION}'s wardens are gone from {PLACE}, called back down the road. {X_nameDef} said, at the gate: \"Keep the road for us a while.\"",
            "The deserters' camp {PLACE} is empty this morning, the fire cold. They went in the night, as people do who don't want to be followed.",
            "{X_nameDef} and the others have gone on from {PLACE}, looking for somewhere nobody knows their faces. {X_nameDef} left a word of thanks at the gate.",
            "The merchants' camp {PLACE} is gone. {X_nameDef} bowed to the colony's gate before leaving, in some custom of their own.",
            "The merchants from {FACTION} packed up their stall and went on toward the far markets. {X_nameDef} called something from the road in their own tongue, and it sounded like a blessing.",
            // Callers at their work (Rimfolk_Events_Callers.xml): a captive's kin speaking of a sister and a dog the game never
            // made, an undertaker calling every dead raider "he" and "somebody's son", and an animal doctor speaking of a leg
            // whatever was hurt. Their own words beside them are in Rimfolk_Pool_CallerSays.xml.
            "[talk] Your sister sends her love, and your old dog still waits by the door.",
            "[load] Mind his head, he's somebody's son.",
            "[tend] Who's been looking after this leg, then."
        };

        private static Dictionary<string, LineTags.Parsed> parsed;

        /// <summary>
        /// The condition a line needs besides its own tags, taken apart (null for none, <see cref="Never"/> for a line
        /// retired). Asked once per line; the answer is kept with the line (LineTags.Parsed).
        /// </summary>
        public static LineTags.Parsed ConditionOf(string line)
        {
            if (line == null) return null;
            // A line of a letter keeps its paragraphs as the file writes them ("\n"), whichever way it was read.
            string written = line.IndexOf('\n') >= 0 ? line.Replace("\n", "\\n") : line;
            if (Retired.Contains(line) || Retired.Contains(written)) return Never;
            if (parsed == null)
            {
                var d = new Dictionary<string, LineTags.Parsed>(StringComparer.Ordinal);
                foreach (var kv in AlsoWhen) d[kv.Key] = LineTags.Parse("[" + kv.Value + "] x");
                parsed = d;
            }
            return parsed.TryGetValue(line, out var p) || parsed.TryGetValue(written, out p) ? p : null;
        }

        /// <summary>The condition of a retired line: never holds.</summary>
        public static readonly LineTags.Parsed Never = new LineTags.Parsed { text = "" };

        /// <summary>Whether a line's extra condition (<see cref="ConditionOf"/>) holds in this moment: none always does.</summary>
        public static bool Holds(LineTags.Parsed condition, HashSet<string> context) =>
            condition == null || condition != Never && LineTags.Match(condition, context) >= 0;
    }
}
