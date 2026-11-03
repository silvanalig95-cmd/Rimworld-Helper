using System.Collections.Generic;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A conversation piece between a colonist and someone from outside the colony (Defs/OutsiderTalkDefs/): two to six
    /// lines said in turn over their heads, written for the moment its conditions describe, with placeholders filled
    /// from what is true in the game when it's said. The format is described at the top of
    /// Defs/OutsiderTalkDefs/Rimfolk_OutsiderTalks_Greetings.xml; the rules of choosing are in OutsiderTalkMath.
    /// </summary>
    public class OutsiderTalkDef : Def
    {
        /// <summary>What it's about (greeting, road, home, trade, weather, colony, town, war, situation, faith, family, work, deeds, callback...).</summary>
        public string topic;
        /// <summary>When it can be said: chat (the default), home (asked about home), know (getting to know them); several with commas.</summary>
        public string uses;
        /// <summary>Its conditions, in the lines' own tag syntax without brackets: "their:lowtech,town:war,!first".</summary>
        public string when;
        public float weight = 1f;
        /// <summary>The lines in order, each "[us] ..." (the colonist) or "[them] ..." (the outsider), with line conditions after the speaker.</summary>
        public List<string> lines = new List<string>();
        /// <summary>What goes into the town's "Heard lately" once it's been said, or nothing.</summary>
        [MustTranslate] public string heard;
        /// <summary>The mark it leaves on the two's memory, for callbacks (talked:KEY), and the placeholder whose fact it remembers.</summary>
        public string mark;
        public string about;

        [Unsaved] private TalkPiece piece;
        [Unsaved] private List<string> problems;

        public TalkPiece Piece
        {
            get
            {
                if (piece == null)
                {
                    problems = new List<string>();
                    piece = OutsiderTalkMath.Parse(defName, topic, uses, weight, when, lines, heard, mark, about, problems);
                }
                return piece;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            var p = Piece;
            if (problems != null)
                foreach (var e in problems)
                    yield return e;
            if (p.uses == null || p.uses.Length == 0) yield return "no use";
        }

        private static List<TalkPiece> all;
        private static int allGeneration = -1;

        /// <summary>Every piece loaded (the mod's own and any other mod's), taken apart once.</summary>
        public static List<TalkPiece> All
        {
            get
            {
                if (all == null || allGeneration != DefRefs.Generation)
                {
                    allGeneration = DefRefs.Generation;
                    all = new List<TalkPiece>();
                    foreach (var d in DefDatabase<OutsiderTalkDef>.AllDefsListForReading)
                        if (d?.Piece != null && d.Piece.turns.Count >= OutsiderTalkMath.MinTurns)
                            all.Add(d.Piece);
                }
                return all;
            }
        }

        public static TalkPiece Named(string name)
        {
            if (name.NullOrEmpty()) return null;
            return DefDatabase<OutsiderTalkDef>.GetNamedSilentFail(name)?.Piece;
        }
    }
}
