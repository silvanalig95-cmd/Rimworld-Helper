using System;

namespace ColonyCouncil
{
    /// <summary>Which way the warlord's son comes back, by how his father's story ended.</summary>
    public enum SonBranch
    {
        None,
        /// <summary>His father slain by the colony: he raises a band, a raiders' camp near the home, and leads it.</summary>
        Band,
        /// <summary>His father captured, handed over or ransomed: he comes to the gate for his father, his grave, his blade or a peace.</summary>
        Gate,
        /// <summary>His father hunted down by others: an exile asking shelter.</summary>
        Exile,
        /// <summary>His father's band broken up, or gone: a young man with a few riders, looking for work.</summary>
        Riders
    }

    /// <summary>What the son asks at the gate, by how things stand with his father.</summary>
    public enum SonAsk
    {
        /// <summary>His father is the colony's prisoner: let him go.</summary>
        Father,
        /// <summary>His father is one of the colony now: let them meet.</summary>
        Meet,
        /// <summary>His father lies in a grave of the colony's: someone of the colony to lay his token there.</summary>
        Grave,
        /// <summary>His father's weapon is in the colony's stores: give it back.</summary>
        Blade,
        /// <summary>Nothing of his father's is left here: a peace between his house and the colony.</summary>
        Peace
    }

    /// <summary>
    /// The follow-ups of world events (docs/design/stage1-region.md, item 7; decision 12): the warlord's son, 60 to 180 days
    /// after his father's story ended, by how it ended; the town that grew from the settlers' camp, as long after their
    /// first year ended in friendship or as neighbours (or after a hamlet's second year); and the other ways a hidden
    /// raiders' camp is found (a visitor's warning, a neighbour's letter). Pure: Tools/Tests (FollowUpTests) checks it.
    /// </summary>
    public static class FollowUpsMath
    {
        public const int DayTicks = 60000;

        /// <summary>A follow-up comes this many days after what it follows at the soonest, and at the latest...</summary>
        public const int MinDays = 60, MaxDays = 180;
        /// <summary>...and is let go when no world event's slot came free this many days past the latest.</summary>
        public const int GraceDays = 60;
        /// <summary>Follow-ups waiting at one home; past that the oldest is let go.</summary>
        public const int MaxWaiting = 6;

        /// <summary>The son's branch by his father's ending (WarlordWorker's ending keys).</summary>
        public static SonBranch Branch(string ending)
        {
            switch (ending)
            {
                case "slain": return SonBranch.Band;
                case "captured":
                case "handedOver":
                case "ransomed": return SonBranch.Gate;
                case "hunted":
                case "bounty":
                case "allies":
                case "mercenaries": return SonBranch.Exile;
                case "brokeup":
                case "gone":
                case "peace":
                case "vanished": return SonBranch.Riders;
                default: return SonBranch.None;
            }
        }

        /// <summary>
        /// The branch as it can be now: a band needs his people hostile and able to hold a camp near the home (else he comes
        /// to the gate instead, grim); coming to the gate needs a people at peace with the colony he can ride with.
        /// </summary>
        public static SonBranch Now(SonBranch b, bool canCamp, bool anyBanner)
        {
            if (b == SonBranch.Band && !canCamp) b = SonBranch.Gate;
            if (b != SonBranch.Band && b != SonBranch.None && !anyBanner) return SonBranch.None;
            return b;
        }

        /// <summary>When it comes: <see cref="MinDays"/> to <see cref="MaxDays"/> after <paramref name="endTick"/>, by <paramref name="roll"/> (0 to 1).</summary>
        public static int DueTick(int endTick, float roll)
        {
            float r = Math.Max(0f, Math.Min(1f, roll));
            return endTick + (int)Math.Round((MinDays + r * (MaxDays - MinDays)) * DayTicks);
        }

        public static bool Due(int due, int now) => due >= 0 && now >= due;

        /// <summary>Waited too long for a free slot: let go.</summary>
        public static bool Lapsed(int endTick, int now) => endTick >= 0 && now - endTick > (long)(MaxDays + GraceDays) * DayTicks;

        /// <summary>
        /// The son's age range by his father's (or mother's) age then: 16 at the youngest, at most 30 and 17 years younger
        /// than his parent; 17 to 28 when the parent's age isn't known.
        /// </summary>
        public static void SonAge(float parentAge, out float min, out float max)
        {
            if (parentAge <= 0f)
            {
                min = 17f;
                max = 28f;
                return;
            }
            min = 16f;
            max = Math.Max(min + 1f, Math.Min(30f, parentAge - 17f));
        }

        /// <summary>What the son asks at the gate: his father first while the colony holds him, then their meeting, his grave, his weapon, else a peace.</summary>
        public static SonAsk Ask(bool fatherPrisoner, bool fatherOurs, bool grave, bool blade)
        {
            if (fatherPrisoner) return SonAsk.Father;
            if (fatherOurs) return SonAsk.Meet;
            if (grave) return SonAsk.Grave;
            if (blade) return SonAsk.Blade;
            return SonAsk.Peace;
        }

        /// <summary>The flag a gate visit is told by: ask:father, ask:meet, ask:grave, ask:blade, ask:peace.</summary>
        public static string AskFlag(SonAsk a) => "ask:" + a.ToString().ToLowerInvariant();

        // ---------------- The town that grew ----------------

        /// <summary>The history's kind for a steading (or a hamlet) grown into a town: a milestone at its place.</summary>
        public const string KindGrown = "world:grown";
        /// <summary>A town that grew is this much more prosperous (0 to 100) from the day it does, at most <see cref="GrownCap"/>.</summary>
        public const float GrownBoost = 15f, GrownCap = 90f;

        /// <summary>Its prosperity once grown: <see cref="GrownBoost"/> more, never past <see cref="GrownCap"/> (nor lower than it was).</summary>
        public static float Grown(float prosperity) => prosperity < 0f ? prosperity : Math.Max(prosperity, Math.Min(GrownCap, prosperity + GrownBoost));

        /// <summary>The other settlers' year ended in friendship or as neighbours: their steading grows into a town.</summary>
        public static bool TownGrows(string ending) => ending == "alliance" || ending == "neighbours" || ending == "hamlet";

        /// <summary>The flags of their first year that its families remember, as the town's lines ask for them: first:fed, first:gift...</summary>
        public static string FirstYear(string flag) => string.IsNullOrEmpty(flag) || flag.IndexOf(':') >= 0 ? null : "first:" + flag;
    }

    /// <summary>
    /// Other ways a hidden raiders' camp is found (docs/design/stage1-region.md, item 4, "Found"): a visitor of a people at
    /// peace with the colony who saw its fires on the road, or a letter from a neighbouring town near it. Pure.
    /// </summary>
    public static class CampWarnMath
    {
        /// <summary>No warning before the camp has stood this many days (the colony's own people get the first look).</summary>
        public const float FromDays = 2f;
        /// <summary>A visitor at the gate who came past it warns, this likely a day.</summary>
        public const float VisitorChance = 0.35f;
        /// <summary>A neighbour's town within this many tiles of the camp, and its letter this likely a day.</summary>
        public const float LetterTiles = 15f, LetterChance = 0.12f;

        /// <summary>A warning may come today: hidden this long, and the chance by the rate setting.</summary>
        public static bool May(float daysHidden) => daysHidden >= FromDays;

        public static float Visitor(float rate) => Math.Max(0f, Math.Min(1f, VisitorChance * Math.Max(0.5f, rate)));

        public static float Letter(float rate) => Math.Max(0f, Math.Min(1f, LetterChance * Math.Max(0.5f, rate)));
    }
}
