using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's eight ways to play (the player's request, 12 November 2026: "i'd like you to create new and interesting
    /// storytellers. that can enhance existing or make up new ways to play the game & directions"; docs/storytellers-proposals.md),
    /// as a storyteller each and as the story direction over any storyteller. None is a character: each is named for what it
    /// does. Small Hours and The Frontier were left out at the player's word.
    /// </summary>
    public enum StoryWay
    {
        None,
        LongRoad,
        Neighbours,
        TurningYear,
        WhatYouSow,
        OldPowers,
        LeanYears,
        RiseAndFall,
        Crossroads
    }

    /// <summary>How a way's levers move with the world: not at all, with the season, with the colony's deeds, with the long arc.</summary>
    public enum StoryShape
    {
        None,
        Seasons,
        Deeds,
        Arc
    }

    /// <summary>Where Rise and Fall's long arc stands: years of growth, the omens of a trouble, the trouble, the rebuilding.</summary>
    public enum ArcPhase
    {
        Growth,
        Omen,
        Trouble,
        Rebuild
    }

    /// <summary>The great trouble of one of Rise and Fall's arcs.</summary>
    public enum StoryTrouble
    {
        War,
        Sickness,
        Hunger,
        Winter,
        Void
    }

    /// <summary>
    /// The arithmetic of the ways to play, with no game types so Tools/Tests and Tools/Balance read it: the levers (a factor
    /// on each kind of Rimfolk's happenings), the rule that combines the storyteller's and the direction's, how the season,
    /// the colony's deeds and the long arc shape them, and the shapes the ways' own storyteller parts take. Two rules bind
    /// every one of them:
    /// <list type="bullet">
    /// <item>A way leans toward something, never away from anything (the player: "but the storytellers will still always
    /// include a chance for any kind of event, right? not that i lock myself out of some of them"): no lever, shape or
    /// share of a storyteller part goes below <see cref="Floor"/>, and a raid's reasons never take every raid.</item>
    /// <item>Harm stays at the game's own rate: a lever on something that hurts is never above 1, and a shape that moves it
    /// about over the year or the arc averages 1 or less.</item>
    /// </list>
    /// </summary>
    public static class StoryWaysMath
    {
        // ================= The ways =================

        /// <summary>The eight, in the order the storyteller screen and the setting list them.</summary>
        public static readonly StoryWay[] Ways =
        {
            StoryWay.LongRoad, StoryWay.Neighbours, StoryWay.TurningYear, StoryWay.WhatYouSow, StoryWay.OldPowers, StoryWay.LeanYears,
            StoryWay.RiseAndFall, StoryWay.Crossroads
        };

        public const string DefPrefix = "Rimfolk_Story_";

        /// <summary>A way's StorytellerDef ("Rimfolk_Story_LongRoad"); null for none.</summary>
        public static string DefName(StoryWay w) => w == StoryWay.None ? null : DefPrefix + w;

        /// <summary>The way a StorytellerDef is, or none.</summary>
        public static StoryWay WayOf(string defName)
        {
            if (defName == null || !defName.StartsWith(DefPrefix, StringComparison.Ordinal)) return StoryWay.None;
            return Enum.TryParse(defName.Substring(DefPrefix.Length), out StoryWay w) ? w : StoryWay.None;
        }

        /// <summary>The way that leans the game now: the direction when one is chosen, else the storyteller's own, else none.</summary>
        public static StoryWay Active(StoryWay direction, StoryWay storyteller) => direction != StoryWay.None ? direction : storyteller;

        // ================= The levers =================

        /// <summary>Rimfolk's visitor events: callers at the gate, travellers, the peoples' own visits.</summary>
        public const string Visitors = "visitors";
        /// <summary>Rimfolk's challenges and gambles the colony can take up or leave.</summary>
        public const string Challenges = "challenges";
        /// <summary>Rimfolk's events where a colonist's past catches up with them.</summary>
        public const string Pasts = "pasts";
        /// <summary>Rimfolk's events of the land and the sky.</summary>
        public const string Nature = "nature";
        /// <summary>Rimfolk's events that may end in a fight (harm: never above 1).</summary>
        public const string Fights = "fights";
        /// <summary>Camps on the land: the chance one comes on an open morning.</summary>
        public const string Camps = "camps";
        /// <summary>Traders: trading camps among the camps, and the market caravans of a town's market rights.</summary>
        public const string Traders = "traders";
        /// <summary>The world writes first: letters from the region's notables.</summary>
        public const string Letters = "letters";
        /// <summary>Favours asked of the colony: debts called in, a people's call to arms.</summary>
        public const string Favours = "favours";
        /// <summary>Missions offered from the region (quests of Rimfolk's own).</summary>
        public const string Missions = "missions";
        /// <summary>The region's world situations: how soon the next one comes.</summary>
        public const string Situations = "situations";
        /// <summary>The gods: how near they draw, and how often a god's own doing in the world comes (never a hurt).</summary>
        public const string Gods = "gods";
        /// <summary>Personal stories: how often one begins.</summary>
        public const string Arcs = "arcs";
        /// <summary>How strongly the peoples remember what the colony did to them, good and bad.</summary>
        public const string Memory = "memory";
        /// <summary>Friends in a fight: the chance a people at peace sends fighters when the colony is attacked.</summary>
        public const string Allies = "allies";
        /// <summary>Schemes against the colony (harm: never above 1).</summary>
        public const string Schemes = "schemes";
        /// <summary>
        /// The share of the raids the storyteller sends without saying from whom that come from the people with the most
        /// reason (a wrong the colony did them, an old enmity): 0 none, 1 every one that can.
        /// </summary>
        public const string RaidReasons = "raidReasons";

        public static readonly string[] Keys =
        {
            Visitors, Challenges, Pasts, Nature, Fights, Camps, Traders, Letters, Favours, Missions, Situations, Gods, Arcs, Memory, Allies, Schemes,
            RaidReasons
        };

        /// <summary>The levers on something that hurts: never above 1 on their own.</summary>
        public static readonly string[] HarmKeys = { Fights, Schemes };

        public static bool IsKey(string key) => Array.IndexOf(Keys, key) >= 0;

        public static bool IsHarm(string key) => Array.IndexOf(HarmKeys, key) >= 0;

        /// <summary>A lever's value when no way leans the game: 1 (as it is), and no raid aimed.</summary>
        public static float Neutral(string key) => key == RaidReasons ? 0f : 1f;

        /// <summary>No lever moves anything past three times its own rate.</summary>
        public const float MaxLever = 3f;

        /// <summary>No lever, shape or share takes anything below a quarter of its own rate: every kind of event keeps its chance.</summary>
        public const float Floor = 0.25f;

        /// <summary>The most of the raids a way aims by reason: the rest stay the game's own choice, so every people may still come.</summary>
        public const float MaxRaidReasons = 0.85f;

        /// <summary>
        /// A way's own value for a lever, made safe: never below <see cref="Floor"/>, harm never above 1, the raids' reasons
        /// a share of them, never past <see cref="MaxRaidReasons"/> (that one is not how often anything comes, only who).
        /// </summary>
        public static float Clamp(string key, float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return Neutral(key);
            if (key == RaidReasons) return Math.Max(0f, Math.Min(MaxRaidReasons, v));
            if (IsHarm(key)) return Math.Max(Floor, Math.Min(1f, v));
            return Math.Max(Floor, Math.Min(MaxLever, v));
        }

        /// <summary>
        /// A lever now: the way's value (<see cref="Clamp"/>) times its shape's factor (the season, the deeds, the arc), within
        /// bounds. A harm lever may rise above 1 in one season or phase only where its shape averages 1 or less over the
        /// year or the arc (the tests check every table).
        /// </summary>
        public static float Combine(string key, float value, StoryShape shape, int quarter, float deeds, ArcPhase phase)
        {
            float v = Clamp(key, value) * ShapeFactor(key, shape, quarter, deeds, phase);
            if (key == RaidReasons) return Math.Max(0f, Math.Min(MaxRaidReasons, v));
            return Math.Max(Floor, Math.Min(MaxLever, v));
        }

        /// <summary>The shape's factor on a lever: 1 with no shape.</summary>
        public static float ShapeFactor(string key, StoryShape shape, int quarter, float deeds, ArcPhase phase)
        {
            switch (shape)
            {
                case StoryShape.Seasons: return SeasonFactor(key, quarter);
                case StoryShape.Deeds: return DeedFactor(key, deeds);
                case StoryShape.Arc: return ArcFactor(key, phase);
                default: return 1f;
            }
        }

        // ================= The Turning Year: the seasons =================

        /// <summary>The quarters of the year: 0 spring, 1 summer, 2 autumn, 3 winter.</summary>
        public const int Spring = 0, Summer = 1, Autumn = 2, Winter = 3;

        private static readonly Dictionary<string, float[]> seasons = new Dictionary<string, float[]>
        {
            // Camps come with the good weather and stay away in the deep of winter.
            [Camps] = new[] { 1.0f, 1.4f, 1.3f, 0.3f },
            // Trade in autumn, when the harvest is in.
            [Traders] = new[] { 0.7f, 1.0f, 1.9f, 0.4f },
            [Visitors] = new[] { 1.0f, 1.3f, 1.1f, 0.6f },
            [Missions] = new[] { 1.1f, 1.2f, 1.1f, 0.6f },
            // People write by the fire in winter.
            [Letters] = new[] { 0.8f, 0.6f, 1.0f, 1.6f },
            [Nature] = new[] { 1.2f, 1.0f, 0.8f, 1.0f },
            [Gods] = new[] { 0.9f, 0.8f, 1.0f, 1.3f },
            // The hungry fights of late winter, quiet springs (harm: averages 1 over the year).
            [Fights] = new[] { 0.5f, 0.9f, 1.1f, 1.5f },
            [Schemes] = new[] { 0.8f, 1.0f, 1.0f, 1.2f }
        };

        /// <summary>The season's factor on a lever (1 for one the seasons don't move).</summary>
        public static float SeasonFactor(string key, int quarter)
        {
            if (key == null || !seasons.TryGetValue(key, out var t)) return 1f;
            return t[Mod(quarter, 4)];
        }

        /// <summary>The levers the seasons move, for the tests.</summary>
        public static IEnumerable<string> SeasonKeys => seasons.Keys;

        /// <summary>
        /// The Turning Year's threats: the share of their peak each season lets through. Quiet springs, a growing summer, a
        /// harder autumn, the hungry raids of winter.
        /// </summary>
        public static float SeasonThreatAccept(int quarter) => new[] { 0.3f, 0.55f, 0.75f, 1.0f }[Mod(quarter, 4)];

        /// <summary>The Turning Year's sickness with the thaw: as often over the year as the game's own, most of it in spring.</summary>
        public static float SeasonDisease(int quarter) => new[] { 2.2f, 0.6f, 0.6f, 0.6f }[Mod(quarter, 4)];

        /// <summary>The Turning Year's traders and visitors: the share of their peak each season lets through.</summary>
        public static float SeasonVisitAccept(string key, int quarter)
        {
            switch (key)
            {
                case Traders: return new[] { 0.35f, 0.5f, 1.0f, 0.25f }[Mod(quarter, 4)];
                case Allies: return 1f;
                default: return new[] { 0.7f, 1.0f, 0.8f, 0.4f }[Mod(quarter, 4)];
            }
        }

        /// <summary>The world situations the season makes likelier: floods and herds in spring, drought and fairs in summer, the harvest in autumn, the hard winter.</summary>
        public static string[] SeasonSituations(int quarter)
        {
            switch (Mod(quarter, 4))
            {
                case Spring: return new[] { "water", "herds", "faith" };
                case Summer: return new[] { "drought", "fair", "rush" };
                case Autumn: return new[] { "harvest", "trade", "drove", "fair" };
                default: return new[] { "weather", "hunger", "mourning" };
            }
        }

        // ================= What You Sow: the colony's deeds =================

        /// <summary>How much of the peoples' memory it takes before the balance moves far: a few small deeds hardly move it.</summary>
        public const float DeedsFloor = 20f;

        /// <summary>
        /// The colony's deeds as one balance, from what the peoples remember: the good turns it did them against the wrongs,
        /// -1 cruel to 1 kind, 0 for a colony nobody remembers anything of.
        /// </summary>
        public static float DeedsBalance(float favours, float wrongs)
        {
            float f = Math.Max(0f, Nan0(favours)), w = Math.Max(0f, Nan0(wrongs));
            return (f - w) / (f + w + DeedsFloor);
        }

        // The deeds' factor on a lever at the kind end and at the cruel end (1 between, for a colony of no name).
        private static readonly Dictionary<string, (float kind, float cruel)> deeds = new Dictionary<string, (float, float)>
        {
            [Visitors] = (1.5f, 0.5f),
            [Traders] = (1.5f, 0.4f),
            [Camps] = (1.3f, 0.6f),
            [Allies] = (2.0f, 0.3f),
            [Favours] = (1.3f, 0.6f),
            [Letters] = (1.3f, 0.7f),
            [Missions] = (1.3f, 0.7f),
            // What hurts never comes more for cruelty than the game's own rate: kindness only spares the colony some of it.
            [Fights] = (0.6f, 1.0f),
            [Schemes] = (0.4f, 1.0f),
            [RaidReasons] = (0.6f, 1.3f)
        };

        /// <summary>The deeds' factor on a lever, from 1 at no balance toward its kind or its cruel end.</summary>
        public static float DeedFactor(string key, float balance)
        {
            if (key == null || !deeds.TryGetValue(key, out var e)) return 1f;
            float b = Math.Max(-1f, Math.Min(1f, Nan0(balance)));
            return b >= 0f ? 1f + (e.kind - 1f) * b : 1f + (e.cruel - 1f) * -b;
        }

        /// <summary>The levers the deeds move, for the tests.</summary>
        public static IEnumerable<string> DeedKeys => deeds.Keys;

        /// <summary>
        /// What You Sow's threats: the share of the game's own pace that comes, 1 for a cruel colony (the game's rate, never
        /// more), 0.85 for one of no name, 0.65 for a kind one.
        /// </summary>
        public static float DeedThreatAccept(float balance)
        {
            float b = Math.Max(-1f, Math.Min(1f, Nan0(balance)));
            return b >= 0f ? 0.85f - 0.2f * b : 0.85f + 0.15f * -b;
        }

        /// <summary>What You Sow's traders, visitors and friends in a fight: the share of their peak that comes by the deeds.</summary>
        public static float DeedVisitAccept(string key, float balance)
        {
            float b = Math.Max(-1f, Math.Min(1f, Nan0(balance)));
            // Even a cruel colony keeps a quarter of them: the roads empty, they never close.
            const float cruel = Floor, none = 0.55f;
            return b >= 0f ? none + (1f - none) * b : none + (cruel - none) * -b;
        }

        // ================= Rise and Fall: the long arc =================

        /// <summary>One arc: two years (a year of growth, the omens, the trouble, the rebuilding).</summary>
        public const float ArcDays = 120f;
        public const float GrowthEnd = 55f, OmenEnd = 80f, TroubleEnd = 100f;

        /// <summary>Which arc it is, from the days since the colony settled (the first is 0).</summary>
        public static int ArcCycle(float days) => float.IsNaN(days) || days < 0f ? 0 : (int)Math.Floor(days / ArcDays);

        /// <summary>The day of the arc (0 to 120).</summary>
        public static float ArcDay(float days) => float.IsNaN(days) || days < 0f ? 0f : days - ArcCycle(days) * ArcDays;

        public static ArcPhase PhaseAt(float days)
        {
            float d = ArcDay(days);
            return d < GrowthEnd ? ArcPhase.Growth : d < OmenEnd ? ArcPhase.Omen : d < TroubleEnd ? ArcPhase.Trouble : ArcPhase.Rebuild;
        }

        /// <summary>The day of the arc its phase begins.</summary>
        public static float PhaseStart(ArcPhase p) => p == ArcPhase.Growth ? 0f : p == ArcPhase.Omen ? GrowthEnd : p == ArcPhase.Trouble ? OmenEnd : TroubleEnd;

        /// <summary>How long a phase lasts in days.</summary>
        public static float PhaseDays(ArcPhase p) => p == ArcPhase.Growth ? GrowthEnd : p == ArcPhase.Omen ? OmenEnd - GrowthEnd : p == ArcPhase.Trouble ? TroubleEnd - OmenEnd : ArcDays - TroubleEnd;

        private static readonly StoryTrouble[] troubleOrder = { StoryTrouble.War, StoryTrouble.Sickness, StoryTrouble.Hunger, StoryTrouble.Void, StoryTrouble.Winter };

        /// <summary>The great trouble of an arc: set by the world (its seed), never the same as the arc before.</summary>
        public static StoryTrouble TroubleOf(int cycle, int seed) => troubleOrder[Mod(cycle + Mod(seed, troubleOrder.Length), troubleOrder.Length)];

        /// <summary>The kinds of world situation that foreshadow and carry a trouble.</summary>
        public static string[] TroubleKinds(StoryTrouble t)
        {
            switch (t)
            {
                case StoryTrouble.War: return new[] { "war", "feud", "sellswords", "creed" };
                case StoryTrouble.Sickness: return new[] { "sickness" };
                case StoryTrouble.Hunger: return new[] { "hunger", "drought", "scarcity" };
                case StoryTrouble.Winter: return new[] { "weather" };
                default: return new[] { "sky", "comet" };
            }
        }

        /// <summary>The world situations a phase makes likelier: the good years' trade and fairs, the trouble's own, the rebuilding.</summary>
        public static string[] ArcSituations(ArcPhase p, StoryTrouble t)
        {
            switch (p)
            {
                case ArcPhase.Growth: return new[] { "trade", "fair", "harvest", "town", "songs", "road", "rush" };
                case ArcPhase.Rebuild: return new[] { "rebuild", "peace", "healer", "drove" };
                default: return TroubleKinds(t);
            }
        }

        /// <summary>
        /// The day into the omens the trouble's first word may come (2 to 10, set by the world for each arc, so nobody can
        /// count the days to it).
        /// </summary>
        public static int OmenDay(int cycle, int seed) => 2 + Mod(seed / 7 + cycle * 5, 9);

        // The arc's factor on a lever in each phase (growth, omens, trouble, rebuilding).
        private static readonly Dictionary<string, float[]> arc = new Dictionary<string, float[]>
        {
            [Visitors] = new[] { 1.4f, 1.0f, 0.6f, 1.2f },
            [Traders] = new[] { 1.5f, 1.0f, 0.5f, 1.0f },
            [Camps] = new[] { 1.4f, 1.0f, 0.4f, 1.0f },
            [Missions] = new[] { 1.2f, 1.0f, 0.8f, 1.3f },
            [Letters] = new[] { 1.0f, 1.4f, 1.2f, 1.1f },
            [Gods] = new[] { 1.0f, 1.3f, 1.2f, 0.9f },
            [Nature] = new[] { 1.0f, 1.3f, 1.0f, 1.0f },
            [Allies] = new[] { 1.0f, 1.0f, 1.3f, 1.5f },
            [Favours] = new[] { 1.0f, 1.1f, 1.4f, 1.2f },
            [Situations] = new[] { 1.0f, 1.0f, 0.7f, 1.0f },
            // Harm: lighter in the good years, harder in the trouble, under 1 over the whole arc.
            [Fights] = new[] { 0.6f, 0.9f, 1.4f, 0.8f },
            [Schemes] = new[] { 0.7f, 1.0f, 1.3f, 0.8f },
            [RaidReasons] = new[] { 0.5f, 1.0f, 1.6f, 0.5f }
        };

        public static float ArcFactor(string key, ArcPhase p)
        {
            if (key == null || !arc.TryGetValue(key, out var t)) return 1f;
            return t[(int)p];
        }

        /// <summary>The levers the arc moves, for the tests.</summary>
        public static IEnumerable<string> ArcKeys => arc.Keys;

        /// <summary>A lever's mean over a whole arc, each phase by its length (for the tests: harm's is 1 or less).</summary>
        public static float ArcMean(string key)
        {
            float sum = 0f;
            foreach (ArcPhase p in Enum.GetValues(typeof(ArcPhase))) sum += ArcFactor(key, p) * PhaseDays(p);
            return sum / ArcDays;
        }

        /// <summary>A lever's mean over the year (for the tests).</summary>
        public static float SeasonMean(string key)
        {
            float sum = 0f;
            for (int q = 0; q < 4; q++) sum += SeasonFactor(key, q);
            return sum / 4f;
        }

        /// <summary>
        /// Rise and Fall's threats: the share of their peak each phase lets through. The trouble of war brings the raids;
        /// the others come harder than the good years but are carried by their own happenings.
        /// </summary>
        public static float ArcThreatAccept(ArcPhase p, StoryTrouble t)
        {
            switch (p)
            {
                case ArcPhase.Growth: return 0.45f;
                case ArcPhase.Omen: return 0.6f;
                case ArcPhase.Trouble: return t == StoryTrouble.War ? 1.0f : 0.7f;
                default: return 0.3f;
            }
        }

        /// <summary>Rise and Fall's sickness: about the game's own over the arc, most of it in a plague's trouble.</summary>
        public static float ArcDisease(ArcPhase p, StoryTrouble t)
        {
            switch (p)
            {
                case ArcPhase.Growth: return 0.75f;
                case ArcPhase.Omen: return 1.0f;
                case ArcPhase.Trouble: return t == StoryTrouble.Sickness ? 2.6f : 1.2f;
                default: return 0.85f;
            }
        }

        /// <summary>Rise and Fall's traders and visitors: plenty in the good years, few while the trouble lasts.</summary>
        public static float ArcVisitAccept(string key, ArcPhase p)
        {
            if (key == Allies) return p == ArcPhase.Trouble || p == ArcPhase.Rebuild ? 1f : 0.6f;
            return new[] { 1.0f, 0.6f, 0.25f, 0.5f }[(int)p];
        }

        /// <summary>A shape over an arc averaged over the five troubles, each phase by its length (for the tests and the balance tool).</summary>
        public static float ArcMeanOver(Func<ArcPhase, StoryTrouble, float> f)
        {
            float sum = 0f;
            foreach (StoryTrouble t in Enum.GetValues(typeof(StoryTrouble)))
            foreach (ArcPhase p in Enum.GetValues(typeof(ArcPhase)))
                sum += f(p, t) * PhaseDays(p);
            return sum / (ArcDays * Enum.GetValues(typeof(StoryTrouble)).Length);
        }

        // ================= The ways' own storyteller parts =================

        /// <summary>The share of a threat cycle's peak that comes now, by the part's shape (1 with no shape).</summary>
        public static float ThreatAccept(StoryShape shape, int quarter, float deeds, ArcPhase phase, StoryTrouble trouble)
        {
            switch (shape)
            {
                case StoryShape.Seasons: return SeasonThreatAccept(quarter);
                case StoryShape.Deeds: return DeedThreatAccept(deeds);
                case StoryShape.Arc: return ArcThreatAccept(phase, trouble);
                default: return 1f;
            }
        }

        /// <summary>The factor on the game's own sickness interval's chance now, by the shape (about 1 over a year or an arc).</summary>
        public static float DiseaseFactor(StoryShape shape, int quarter, ArcPhase phase, StoryTrouble trouble)
        {
            switch (shape)
            {
                case StoryShape.Seasons: return SeasonDisease(quarter);
                case StoryShape.Arc: return ArcDisease(phase, trouble);
                default: return 1f;
            }
        }

        /// <summary>The share of a visit's peak (traders, visitors, friends in a fight) that comes now, by the shape.</summary>
        public static float VisitAccept(StoryShape shape, string key, int quarter, float deeds, ArcPhase phase)
        {
            switch (shape)
            {
                case StoryShape.Seasons: return SeasonVisitAccept(key, quarter);
                case StoryShape.Deeds: return DeedVisitAccept(key, deeds);
                case StoryShape.Arc: return ArcVisitAccept(key, phase);
                default: return 1f;
            }
        }

        /// <summary>
        /// A named happening's weight among a way's picks by when it last came: a tenth just after, all of it
        /// <see cref="PickVarietyDays"/> later, so the same thing never comes twice running while another could.
        /// </summary>
        public static float PickRecency(float daysSince)
        {
            if (float.IsNaN(daysSince) || daysSince < 0f || daysSince >= PickVarietyDays) return 1f;
            return 0.1f + 0.9f * daysSince / PickVarietyDays;
        }

        public const float PickVarietyDays = 20f;

        // ================= The world situations and the hurts =================

        /// <summary>A world situation of a kind the way leans to weighs this much more in the draw.</summary>
        public const float SituationLean = 3f;

        public static float SituationFactor(string kind, IList<string> leaned, IList<string> leanedNow)
        {
            if (string.IsNullOrEmpty(kind)) return 1f;
            if (leaned != null && leaned.Contains(kind)) return SituationLean;
            if (leanedNow != null && leanedNow.Contains(kind)) return SituationLean;
            return 1f;
        }

        /// <summary>
        /// A kind of everyday hurt's share of the budget, tilted toward the kinds a way leans to (Harm.Reckon): the tilt moves
        /// the hurts between kinds and the budget stays the same, so it shapes what kind of harm comes, never how much.
        /// </summary>
        public static float HarmTilt(string kind, IList<string> lean, float factor)
        {
            if (kind == null || lean == null || !lean.Contains(kind)) return 1f;
            return Math.Max(0.25f, Math.Min(3f, float.IsNaN(factor) ? 1f : factor));
        }

        // ================= Raids with a reason =================

        /// <summary>
        /// How much reason a hostile people has to raid the colony: the wrongs the colony did them (as their memory holds
        /// them), a quarter of the colony's own old grievance against them (an enmity both sides keep), and a cold goodwill
        /// past -40. Nothing for a people with no reason.
        /// </summary>
        public static float Grievance(float wrongs, float theirWrongs, int goodwill)
        {
            float g = Math.Max(0f, Nan0(wrongs)) + 0.25f * Math.Max(0f, Nan0(theirWrongs));
            if (goodwill < -40) g += (-40 - goodwill) / 20f;
            return g;
        }

        /// <summary>A people needs this much reason before a raid is aimed at it; with none that has, the game chooses as ever.</summary>
        public const float GrievanceFloor = 2f;

        // ================= Small helpers =================

        public static int Mod(int a, int b)
        {
            int r = a % b;
            return r < 0 ? r + b : r;
        }

        private static float Nan0(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
    }
}
