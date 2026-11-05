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
            // The renownup bubble (Rimfolk_Lines_Renown.xml) is said as a house's name first carries to a town nearby too,
            // where it is spoken of in the next town's market, talked about in the next town, told at the nearest camp's
            // fire: a song, the papers, the markets, every camp's fire and "well beyond the colony" are the region's band
            // or wider (house:renowned). The round d text pass; Rimfolk_Lines_RenownMore.xml has the town's words.
            ["[renownup,us] They say our family's name is known well beyond the colony now."] = "house:renowned",
            ["[renownup,us,tribal] The other camps tell of our family at their fires now, I hear."] = "house:renowned",
            ["[renownup,us,medieval] I hear there's a song with our name in it now."] = "house:renowned",
            ["[renownup,us,medieval] Our family's name gets a nod in the markets now, I'm told."] = "house:renowned",
            ["[renownup,us,industrial] Somebody said our name made the papers, of all things."] = "house:renowned",
            // The renowndown bubble is said as a name fades from the talk, most often from the next town's market, where no
            // singer ever sang it: the singers only for a name that was sung in the region's halls (RenownMath.BubbleTag).
            ["[renowndown,us,medieval] The singers have found someone else to sing about."] = RenownMath.WasRenownedTag,
            // Their answer to a letter from one of a house in a feud (Rimfolk_Lines_Renown.xml): "our house" only from the
            // head of a house (them:house); the feud's tag is also set for one of their people with no house, who holds the
            // grudge or whose people hold one about one of the writer's house.
            ["[greetings,cool,them:house:feud] We have your letter. There is still a grudge between our house and your colony, and a letter does not settle it."] = "them:house",
            ["[greetings,curt,them:house:feud,theirLowtech] Our house has not forgotten what lies between us. Write if you must."] = "them:house"
        };

        /// <summary>Shipped lines true in no case they are drawn for, never said again.</summary>
        public static readonly HashSet<string> Retired = new HashSet<string>(StringComparer.Ordinal)
        {
            // Their answer to the colony's condolences, from one of a renowned house: the loss is theirs (the letter's
            // topic, NeighbourLetters.Topics), never the writer's house's. Its better words are in Rimfolk_Lines_RenownMore.xml.
            "[condolences,warm,house:renowned] A loss to {HOUSE} is felt here too. Your family's name is honored among us."
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
