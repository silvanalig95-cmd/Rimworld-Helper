using System;
using System.Globalization;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of a colonist a world event sends to a far town (World/FarErrands.cs): how far a town may be, how long
    /// they are away in all, when the event's own letters wait for them, how long they stay on for a question asked while
    /// they are there, and the note the season away keeps of which answer sent them. Shared with the tests
    /// (Tools/Tests/FarErrandTests.cs); nothing here knows the game.
    /// </summary>
    public static class FarErrandMath
    {
        public const int DayTicks = 60000, HourTicks = 2500;

        /// <summary>The season away's kind for a world event's errand (SeasonAway.Kinds).</summary>
        public const string Key = "world";

        /// <summary>The longest road each way to a town a colonist is sent to: farther, the answer is greyed.</summary>
        public const float MaxRoadDays = 8f;

        /// <summary>A town within reach: its road each way no longer than <see cref="MaxRoadDays"/>.</summary>
        public static bool InReach(float roadDays) => !float.IsNaN(roadDays) && roadDays >= 0f && roadDays <= MaxRoadDays;

        /// <summary>The whole time away, in days: the road there and back, and the days there.</summary>
        public static float AwayDays(float roadDays, float daysThere) =>
            2f * (float.IsNaN(roadDays) ? 0f : Math.Max(0f, roadDays)) + (float.IsNaN(daysThere) ? 0f : Math.Max(0f, daysThere));

        /// <summary>The whole days away, rounded up, for the answer's card.</summary>
        public static int AwayDaysShown(float roadDays, float daysThere) => Math.Max(1, (int)Math.Ceiling(AwayDays(roadDays, daysThere) - 0.05f));

        /// <summary>A world event waits at most this many days past its end for the colonist it sent.</summary>
        public const int HoldMaxDays = 30;

        /// <summary>The event doesn't end yet: its colonist is still away, and it hasn't waited too long for them.</summary>
        public static bool HoldsEnd(bool away, int now, int endTick) => away && now < (long)endTick + (long)HoldMaxDays * DayTicks;

        /// <summary>Once they are home from an errand the event waited for, it ends no sooner than this after (its letters that waited come first).</summary>
        public const int AfterHomeTicks = 2 * DayTicks;

        /// <summary>The event's end (or its answer's ending) once its colonist is home: no sooner than <see cref="AfterHomeTicks"/> from now.</summary>
        public static int EndAfterHome(int now, int endTick) => (int)Math.Min(int.MaxValue, Math.Max((long)endTick, (long)now + AfterHomeTicks));

        /// <summary>What a beat of the event does while its errand runs.</summary>
        public enum BeatTurn
        {
            /// <summary>It comes as usual.</summary>
            Go,
            /// <summary>It waits a while and is looked at again (<see cref="WaitTicks"/>).</summary>
            Wait,
            /// <summary>It won't come (a letter from the far town after the colonist has left it).</summary>
            Drop
        }

        /// <summary>A waiting beat is looked at again this often.</summary>
        public const int WaitTicks = 2 * HourTicks;

        /// <summary>A question written from the far town comes on the colonist's last day there.</summary>
        public const int LastDayTicks = DayTicks;

        /// <summary>
        /// A beat while the event's errand runs. One whose words are written from the far town (<paramref name="atPlace"/>)
        /// comes only while the colonist is there, on their last day, and is dropped once they have left or never went. Any
        /// other that would fall to the colonist away (the event's own person, or the best at its skill) waits until they
        /// are home. The rest come as usual.
        /// </summary>
        public static BeatTurn Turn(bool fallsToAway, bool atPlace, bool away, bool there, int ticksLeftThere)
        {
            if (atPlace)
            {
                if (!away) return BeatTurn.Drop;
                return there && ticksLeftThere <= LastDayTicks ? BeatTurn.Go : BeatTurn.Wait;
            }
            return fallsToAway && away ? BeatTurn.Wait : BeatTurn.Go;
        }

        /// <summary>A beat with a skill of its own falls to the colonist away when nobody at home is as good at it.</summary>
        public static bool BestIsAway(int awayLevel, int bestHomeLevel, int minLevel) => awayLevel >= minLevel && awayLevel >= bestHomeLevel;

        /// <summary>They stay on past their days while a question for them there is still to come or unanswered, this many days at most.</summary>
        public const int StayOverDays = 3;

        public static bool StaysOn(bool asked, int now, int due) => asked && now < (long)due + (long)StayOverDays * DayTicks;

        /// <summary>
        /// Which answer sent them: the event, the beat, the answer, which of its lists (the risk's when it replaced the
        /// answer's own) and where in it, whether the risk came true, and the event's {COUNT} then. Kept in the season
        /// away's own words (SeasonAway's extra), so nothing new is saved.
        /// </summary>
        public struct Note
        {
            public int situation;
            public string def;
            public string beat;
            public int option;
            public bool riskList;
            public int index;
            public bool risk;
            public int count;
        }

        private const char Sep = ';';

        /// <summary>The note in words: "12;Council_WS_Bumper;hands;0;e;3;-;180".</summary>
        public static string Write(Note n) =>
            string.Join(Sep.ToString(), n.situation.ToString(CultureInfo.InvariantCulture), Clean(n.def), Clean(n.beat), n.option.ToString(CultureInfo.InvariantCulture),
                n.riskList ? "r" : "e", n.index.ToString(CultureInfo.InvariantCulture), n.risk ? "r" : "-", n.count.ToString(CultureInfo.InvariantCulture));

        /// <summary>The note back from its words; false when they aren't one.</summary>
        public static bool Read(string s, out Note n)
        {
            n = default;
            if (string.IsNullOrEmpty(s)) return false;
            var p = s.Split(Sep);
            if (p.Length < 8 || p[1].Length == 0 || p[2].Length == 0) return false;
            if (!int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out n.situation)) return false;
            if (!int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out n.option) || n.option < 0) return false;
            if (p[4] != "e" && p[4] != "r") return false;
            if (!int.TryParse(p[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out n.index) || n.index < 0) return false;
            if (!int.TryParse(p[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out n.count)) return false;
            n.def = p[1];
            n.beat = p[2];
            n.riskList = p[4] == "r";
            n.risk = p[6] == "r";
            return true;
        }

        /// <summary>Nothing in a name that would break the note or the season away's own words around it.</summary>
        private static string Clean(string s) => (s ?? "").Replace(Sep, '_').Replace('|', '_').Replace('/', '_');
    }
}
