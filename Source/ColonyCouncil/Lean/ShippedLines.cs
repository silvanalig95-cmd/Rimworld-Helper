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
            ["[renowndown,us,spacer,!house:wasrenowned] Our name's gone quiet on the nearest settlement's channels."] = "!" + RenownMath.WasRenownedTag
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
            "[ward,cordial,askward,theirLowtech] A youth of our house will come. Teach them well and feed them well, as we would one of yours."
        };

        private static Dictionary<string, LineTags.Parsed> parsed;

        /// <summary>
        /// The condition a line needs besides its own tags, taken apart (null for none, <see cref="Never"/> for a line
        /// retired). Asked once per line; the answer is kept with the line (LineTags.Parsed).
        /// </summary>
        public static LineTags.Parsed ConditionOf(string line)
        {
            if (line == null) return null;
            if (Retired.Contains(line)) return Never;
            if (parsed == null)
            {
                var d = new Dictionary<string, LineTags.Parsed>(StringComparer.Ordinal);
                foreach (var kv in AlsoWhen) d[kv.Key] = LineTags.Parse("[" + kv.Value + "] x");
                parsed = d;
            }
            return parsed.TryGetValue(line, out var p) ? p : null;
        }

        /// <summary>The condition of a retired line: never holds.</summary>
        public static readonly LineTags.Parsed Never = new LineTags.Parsed { text = "" };

        /// <summary>Whether a line's extra condition (<see cref="ConditionOf"/>) holds in this moment: none always does.</summary>
        public static bool Holds(LineTags.Parsed condition, HashSet<string> context) =>
            condition == null || condition != Never && LineTags.Match(condition, context) >= 0;
    }
}
