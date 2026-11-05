using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What a house is known for, from whichever part of its renown is largest (docs/design/stage2-diplomacy.md, item 12).</summary>
    public enum KnownFor
    {
        None,
        Fighters,
        Healers,
        Hosts,
        Traders,
        Makers,
        Faithful
    }

    /// <summary>How far a house's name carries: known for the wrong things, not yet known, in a town, the region, the peoples in reach.</summary>
    public enum RenownBand
    {
        Infamous = -1,
        Unknown = 0,
        Town = 1,
        Region = 2,
        Peoples = 3
    }

    /// <summary>
    /// House renown (docs/design/stage2-diplomacy.md, item 12; release 2d.1; the player's decision 17: earned and shown in
    /// words, with a "known for", nothing spent), with no game types so Tools/Tests can check it. Renown is never stored:
    /// it is read when asked from what the world already keeps of a house's people (the history's deeds and ties, what the
    /// peoples remember of single colonists, the colony's names for them, their offices and titles, the notables' regard)
    /// and, for theirs, from their place and their ties with the colony. Each part is a <see cref="Hit"/> of a
    /// <see cref="Source"/>; a deed fades by its age (full for a year, then a tenth a season down to three tenths); the sum
    /// is the score, its band how far the name is known, its largest kind what the house is known for. What it does: the
    /// notables' regard of the house's people, their good words, matches, and the events the world sends a house known for
    /// something.
    /// </summary>
    public static class RenownMath
    {
        public const int DayTicks = 60000;
        public const float SeasonDays = 15f, YearDays = 60f;

        // ================= The colony's houses =================

        /// <summary>The colony's houses kept at most (the design's twelve).</summary>
        public const int MaxOurs = 12;

        /// <summary>A house of the colony is two or more of its people of one family name (FamilyLifeMath.FamilyAtLeast).</summary>
        public const int AtLeast = 2;

        /// <summary>The members a house remembers (the living and the gone), so its renown keeps what they did: at most this many, the newest kept.</summary>
        public const int MembersKept = 12;

        /// <summary>A house with nobody left at the colony is kept two years (unless a tie still names one of its people), then let go.</summary>
        public const float KeepDays = 2f * YearDays;

        /// <summary>Kept: someone of it is still at the colony, a tie names one of its people, or it is less than two years since one was.</summary>
        public static bool Kept(bool memberHere, bool tieNames, int lastSeen, int now) =>
            memberHere || tieNames || lastSeen >= 0 && now - lastSeen < (long)(KeepDays * DayTicks);

        /// <summary>A house's key: its family name, the same however it is written.</summary>
        public static string Key(string familyName) => string.IsNullOrEmpty(familyName) ? null : familyName.Trim().ToLowerInvariant();

        /// <summary>
        /// Which of the houses that remember someone (by their names, oldest first) they are of now: the one of the family
        /// name they carry (one who took another's name on marrying is remembered by both), else the newest; -1 for none.
        /// </summary>
        public static int HouseFor(IList<string> houseNames, string lastName)
        {
            if (houseNames == null || houseNames.Count == 0) return -1;
            string key = Key(lastName);
            if (key != null)
                for (int i = 0; i < houseNames.Count; i++)
                    if (Key(houseNames[i]) == key)
                        return i;
            return houseNames.Count - 1;
        }

        // ================= Where renown comes from =================

        /// <summary>One kind of renown: its points (a wrong's below zero), what it shows the house is good at, whether it fades with age, and the most it gives in all.</summary>
        public sealed class Source
        {
            public string key;
            public float points;
            public KnownFor known;
            public bool fades;
            public float cap;
        }

        private static Source S(string key, float points, KnownFor known, bool fades, float cap) =>
            new Source { key = key, points = points, known = known, fades = fades, cap = cap };

        /// <summary>
        /// The kinds of renown, by key. Of a house's own people (ours): a contest won at a feast (the champion), married into a
        /// house of theirs, a ward of theirs fostered well as its guardian, a season abroad as the colony's youth or envoy, a
        /// term served as its hostage, a truce sworn as its speaker, a task of the council's done, a secret pressed and told;
        /// what the peoples remember of them by name (a raiders' camp broken, fighting beside theirs, trading at their post,
        /// walking their settlers out, a prisoner of theirs set free, one of theirs tended; killed at peace, executed, cut
        /// open, slighted, brawled with; their raiders brought down count to a fighter's name, little); the colony's names for
        /// them (the Healer, Old Iron), a year in an office, a term as speaker, the Empire's title, notables who think well of
        /// them. The colony's own, shared by each of its houses: an oath or a truce sworn, its word kept, levies sent, a pact
        /// kept; a truce or a pact broken, an oath renounced, a secret told, a call refused, tribute short, its word broken.
        /// Theirs: a town's founding family, its head's house, their people's leader's, their ties with the colony, the town's
        /// fortune, and what its head is best at.
        /// </summary>
        public static readonly Dictionary<string, Source> Sources = new Dictionary<string, Source>
        {
            // A house's own people, from the history.
            ["contest"] = S("contest", 6f, KnownFor.Fighters, true, 24f),
            ["wedding"] = S("wedding", 8f, KnownFor.None, true, 16f),
            ["fostered"] = S("fostered", 6f, KnownFor.Hosts, true, 18f),
            ["abroad"] = S("abroad", 4f, KnownFor.None, true, 8f),
            ["envoy"] = S("envoy", 5f, KnownFor.Hosts, true, 10f),
            ["hostage"] = S("hostage", 3f, KnownFor.None, true, 6f),
            ["parley"] = S("parley", 4f, KnownFor.None, true, 8f),
            ["task"] = S("task", 3f, KnownFor.None, true, 12f),
            ["told"] = S("told", -6f, KnownFor.None, true, 18f),
            // What the peoples remember of them by name (World/ColonistMemory.cs, World/KinMemory.cs).
            ["campBroken"] = S("campBroken", 6f, KnownFor.Fighters, true, 18f),
            ["foughtBeside"] = S("foughtBeside", 4f, KnownFor.Fighters, true, 12f),
            ["killedRaid"] = S("killedRaid", 1f, KnownFor.Fighters, true, 6f),
            ["killedField"] = S("killedField", 2f, KnownFor.Fighters, true, 6f),
            ["tradedPost"] = S("tradedPost", 2f, KnownFor.Traders, true, 10f),
            ["walkedSettlers"] = S("walkedSettlers", 5f, KnownFor.Hosts, true, 10f),
            ["released"] = S("released", 3f, KnownFor.Hosts, true, 9f),
            ["tended"] = S("tended", 3f, KnownFor.Healers, true, 12f),
            ["killedPeace"] = S("killedPeace", -6f, KnownFor.None, true, 18f),
            ["executed"] = S("executed", -10f, KnownFor.None, true, 20f),
            ["harvested"] = S("harvested", -10f, KnownFor.None, true, 20f),
            ["slighted"] = S("slighted", -2f, KnownFor.None, true, 6f),
            ["brawled"] = S("brawled", -2f, KnownFor.None, true, 6f),
            // Held now: never fades while held.
            ["epithet"] = S("epithet", 4f, KnownFor.None, false, 12f),
            ["office"] = S("office", 5f, KnownFor.None, false, 10f),
            ["speaker"] = S("speaker", 3f, KnownFor.None, false, 9f),
            ["title"] = S("title", 1f, KnownFor.None, false, 12f),
            ["regard"] = S("regard", 2f, KnownFor.None, false, 8f),
            // The colony's, shared by each of its houses.
            ["swore"] = S("swore", 2f, KnownFor.None, true, 6f),
            ["keptWord"] = S("keptWord", 2f, KnownFor.None, true, 6f),
            ["levies"] = S("levies", 3f, KnownFor.Fighters, true, 6f),
            ["pactKept"] = S("pactKept", 3f, KnownFor.None, true, 6f),
            ["brokeTruce"] = S("brokeTruce", -8f, KnownFor.None, true, 16f),
            ["brokePact"] = S("brokePact", -6f, KnownFor.None, true, 12f),
            ["renounced"] = S("renounced", -5f, KnownFor.None, true, 10f),
            ["schemed"] = S("schemed", -4f, KnownFor.None, true, 8f),
            ["refused"] = S("refused", -3f, KnownFor.None, true, 6f),
            ["tributeShort"] = S("tributeShort", -2f, KnownFor.None, true, 4f),
            ["wordBroken"] = S("wordBroken", -2f, KnownFor.None, true, 4f),
            // Theirs.
            ["founding"] = S("founding", 12f, KnownFor.None, false, 12f),
            ["head"] = S("head", 8f, KnownFor.None, false, 8f),
            ["leader"] = S("leader", 16f, KnownFor.None, false, 16f),
            ["colonyTie"] = S("colonyTie", 3f, KnownFor.None, true, 15f),
            ["thriving"] = S("thriving", 5f, KnownFor.None, false, 5f),
            ["struggling"] = S("struggling", -4f, KnownFor.None, false, 4f),
            ["skill"] = S("skill", 4f, KnownFor.None, false, 4f)
        };

        /// <summary>The colony's deeds a people remembers (FactionMemory's kinds) that each of the colony's houses shares in.</summary>
        public static readonly string[] ColonyDeeds = { "swore", "keptWord", "levies", "brokeTruce", "brokePact", "renounced", "schemed", "refused", "tributeShort", "wordBroken" };

        /// <summary>The history's kinds a house's own people's deeds are read from.</summary>
        public static readonly string[] MemberKinds =
            { DiplomacyMath.Contest, DiplomacyMath.Wedding, DiplomacyMath.Ward, DiplomacyMath.Envoy, DiplomacyMath.Hostage, DiplomacyMath.Parley, DiplomacyMath.Task };

        /// <summary>
        /// The renown an event of the history gives the house of one who took part in it, by its kind and their part
        /// (null for none): the champion (dip:contest, did it), the one married (dip:wedding, did it), a ward's guardian at
        /// a good year's end (dip:ward, did it, the ward named: <paramref name="aboutSomeone"/>) and the colony's youth home
        /// with what they learned (dip:ward, did it, nobody else named), its envoy home (dip:envoy, did it), its hostage home
        /// (dip:hostage, did it for the colony: <paramref name="forColony"/>; a hostage of theirs who stayed did it for
        /// nobody's term), the speaker at a truce sworn (dip:parley, named), the officer who did a task (dip:task, did
        /// it, a milestone: <paramref name="weighty"/>; a task set, a step of it or one called off is nothing yet); a secret
        /// pressed is a task against its notable (<paramref name="againstSomeone"/>): told, a milestone, it is the keeper's
        /// house's wrong; paid, nobody hears of it.
        /// </summary>
        public static string SourceOf(string kind, HistoryRole role, bool againstSomeone, bool aboutSomeone, bool weighty, bool forColony = true)
        {
            if (role == HistoryRole.About) return kind == DiplomacyMath.Parley ? "parley" : null;
            if (role != HistoryRole.Did) return null;
            switch (kind)
            {
                case DiplomacyMath.Contest: return "contest";
                case DiplomacyMath.Wedding: return "wedding";
                case DiplomacyMath.Ward: return aboutSomeone ? "fostered" : "abroad";
                case DiplomacyMath.Envoy: return "envoy";
                case DiplomacyMath.Hostage: return forColony ? "hostage" : null;
                case DiplomacyMath.Task: return !weighty ? null : againstSomeone ? "told" : "task";
                default: return null;
            }
        }

        /// <summary>A ward's guardian is named in the year's end as one who did it only for a good year or better (a fair one is no deed to be known by), else as one with it.</summary>
        public static HistoryRole GuardianRole(bool goodYear) => goodYear ? HistoryRole.Did : HistoryRole.With;

        /// <summary>
        /// A council task's officer is named in its history as one who did it, except for a task failed (the keeper found
        /// out: no deed to be known by) and the keeper's secret watch (a secret learned is heard of by nobody, as a secret
        /// pressed and paid), where they are one with it.
        /// </summary>
        public static HistoryRole OfficerRole(bool failed, bool secretWatch) => failed || secretWatch ? HistoryRole.With : HistoryRole.Did;

        /// <summary>What the peoples remember of a colonist by name (a mark's kind, ColonistMemoryMath), as renown: a source, or null for one that isn't.</summary>
        public static string SourceOfMark(string markKind) => markKind != null && Sources.ContainsKey(markKind) && Array.IndexOf(MarkKinds, markKind) >= 0 ? markKind : null;

        /// <summary>The marks of the peoples' memory that renown reads.</summary>
        public static readonly string[] MarkKinds =
            { "campBroken", "foughtBeside", "killedRaid", "killedField", "tradedPost", "walkedSettlers", "released", "tended", "killedPeace", "executed", "harvested", "slighted", "brawled" };

        /// <summary>
        /// What each of the colony's names for its people (EpithetDefs) shows the house is good at; a name not listed here
        /// still counts, with no known-for, and a dubious one (the Kindling) not at all.
        /// </summary>
        public static readonly Dictionary<string, KnownFor> Epithets = new Dictionary<string, KnownFor>
        {
            ["Council_Epithet_Defender"] = KnownFor.Fighters, ["Council_Epithet_Deadeye"] = KnownFor.Fighters, ["Council_Epithet_Hunter"] = KnownFor.Fighters,
            ["Council_Epithet_Scrapper"] = KnownFor.Fighters, ["Council_Epithet_OldIron"] = KnownFor.Fighters, ["Council_Epithet_RoadWarden"] = KnownFor.Fighters,
            ["Council_Epithet_Healer"] = KnownFor.Healers, ["Council_Epithet_Surgeon"] = KnownFor.Healers, ["Council_Epithet_Goodhands"] = KnownFor.Healers,
            ["Council_Epithet_Midwife"] = KnownFor.Healers, ["Council_Epithet_OldDoc"] = KnownFor.Healers,
            ["Council_Epithet_Cook"] = KnownFor.Hosts, ["Council_Epithet_CampFriend"] = KnownFor.Hosts, ["Council_Epithet_Gatekeeper"] = KnownFor.Hosts,
            ["Council_Epithet_GoodNeighbour"] = KnownFor.Hosts, ["Council_Epithet_Mender"] = KnownFor.Hosts, ["Council_Epithet_Feudbreaker"] = KnownFor.Hosts,
            ["Council_Epithet_BridgeBuilder"] = KnownFor.Hosts,
            ["Council_Epithet_Haggler"] = KnownFor.Traders, ["Council_Epithet_SilverTongue"] = KnownFor.Traders,
            ["Council_Epithet_Builder"] = KnownFor.Makers, ["Council_Epithet_Maker"] = KnownFor.Makers, ["Council_Epithet_RoofRaiser"] = KnownFor.Makers,
            ["Council_Epithet_Tinker"] = KnownFor.Makers,
            ["Council_Epithet_Celebrant"] = KnownFor.Faithful, ["Council_Epithet_Mourner"] = KnownFor.Faithful
        };

        public static KnownFor EpithetFor(string defName) => defName != null && Epithets.TryGetValue(defName, out var k) ? k : KnownFor.None;

        /// <summary>A year in office, by the record the game keeps of its days (Council_DaysAsPhysician): what it shows the house is good at.</summary>
        public static readonly Dictionary<string, KnownFor> Offices = new Dictionary<string, KnownFor>
        {
            ["Council_DaysAsPhysician"] = KnownFor.Healers, ["Council_DaysAsMarshal"] = KnownFor.Fighters
        };

        /// <summary>Whole years in an office from its days: a year is 60 days.</summary>
        public static int YearsOf(float days) => days <= 0f ? 0 : (int)(days / YearDays);

        /// <summary>The Empire's title as renown points (read, never given): a fiftieth of its seniority, at most twelve (a count's).</summary>
        public static float TitlePoints(int seniority) => Clamp(seniority / 50f, 0f, 12f);

        /// <summary>A notable who thinks this well of one of a house's people (their own regard of them) carries the house's name to their town.</summary>
        public const float KnownToRegard = 20f;

        /// <summary>What their head is best at, by a skill's name, as what their house is known for (Social is the trader's gift here, Cooking the host's).</summary>
        public static KnownFor SkillFor(string skill)
        {
            switch (skill)
            {
                case "Shooting":
                case "Melee": return KnownFor.Fighters;
                case "Medicine": return KnownFor.Healers;
                case "Cooking": return KnownFor.Hosts;
                case "Social": return KnownFor.Traders;
                case "Crafting":
                case "Construction":
                case "Artistic": return KnownFor.Makers;
                default: return KnownFor.None;
            }
        }

        /// <summary>A town's fortune (RegionMath's prosperity, 0 to 100) as their house's: thriving at 70 and up, struggling under 20.</summary>
        public static string FortuneSource(int prosperity) => prosperity >= 70 ? "thriving" : prosperity >= 0 && prosperity < 20 ? "struggling" : null;

        // ================= Reading it =================

        /// <summary>One part of a house's renown: its source, how old it is (days; 0 for one held now), how many times, and what it shows (None: the source's own).</summary>
        public struct Hit
        {
            public string key;
            public float ageDays;
            public float count;
            public KnownFor known;

            public Hit(string key, float ageDays = 0f, float count = 1f, KnownFor known = KnownFor.None)
            {
                this.key = key;
                this.ageDays = ageDays;
                this.count = count;
                this.known = known;
            }
        }

        /// <summary>A house's renown as read: the score, each kind's part, and each source's, largest first.</summary>
        public sealed class Reading
        {
            public float score;
            public readonly float[] byKind = new float[KindCount];
            public readonly List<KeyValuePair<string, float>> parts = new List<KeyValuePair<string, float>>();
            public RenownBand band;
            public KnownFor known;
        }

        public static readonly int KindCount = Enum.GetValues(typeof(KnownFor)).Length;

        /// <summary>A deed counts in full for a year.</summary>
        public const float FullDays = YearDays;

        /// <summary>Then a tenth less each season, to three tenths.</summary>
        public const float FadePerSeason = 0.1f, Floor = 0.3f;

        /// <summary>A deed of this age, as a share of its points.</summary>
        public static float Fade(float ageDays)
        {
            if (ageDays <= FullDays) return 1f;
            int seasons = (int)Math.Ceiling((ageDays - FullDays) / SeasonDays);
            return Math.Max(Floor, 1f - FadePerSeason * seasons);
        }

        /// <summary>
        /// The renown of a house from its parts: each source's points times its count and (for a deed) its fade, summed and
        /// held within its cap either way; the score is their sum, each kind's part the positive points it showed, and the
        /// band and known-for follow (<paramref name="previous"/>: the band last told, for the hold).
        /// </summary>
        public static Reading Read(IEnumerable<Hit> hits, RenownBand previous = RenownBand.Unknown)
        {
            var r = new Reading();
            var sums = new Dictionary<string, float>();
            var kinds = new Dictionary<string, float[]>();
            if (hits != null)
                foreach (var h in hits)
                {
                    if (h.key == null || !Sources.TryGetValue(h.key, out var s) || h.count <= 0f) continue;
                    float v = s.points * h.count * (s.fades ? Fade(h.ageDays) : 1f);
                    sums[h.key] = (sums.TryGetValue(h.key, out var was) ? was : 0f) + v;
                    var k = h.known != KnownFor.None ? h.known : s.known;
                    if (k != KnownFor.None && v > 0f)
                    {
                        if (!kinds.TryGetValue(h.key, out var arr)) kinds[h.key] = arr = new float[KindCount];
                        arr[(int)k] += v;
                    }
                }
            foreach (var kv in sums)
            {
                var s = Sources[kv.Key];
                float v = Clamp(kv.Value, -s.cap, s.cap);
                if (Math.Abs(v) < 0.001f) continue;
                r.score += v;
                r.parts.Add(new KeyValuePair<string, float>(kv.Key, v));
                // Each kind's part, scaled as its source was held to its cap.
                if (kinds.TryGetValue(kv.Key, out var arr) && kv.Value > 0f)
                {
                    float scale = v / kv.Value;
                    for (int i = 0; i < KindCount; i++) r.byKind[i] += arr[i] * scale;
                }
            }
            r.parts.Sort((a, b) => Math.Abs(b.Value).CompareTo(Math.Abs(a.Value)));
            r.band = Band(r.score, previous);
            r.known = KnownForOf(r.byKind);
            return r;
        }

        // ================= Bands =================

        /// <summary>The scores where each band begins: known in a town at 10, the region at 30, the peoples in reach at 60; known for the wrong things at -15 and below.</summary>
        public const float TownAt = 10f, RegionAt = 30f, PeoplesAt = 60f, InfamousAt = -15f;

        /// <summary>A band is kept until the score falls this far under where it begins, so a name doesn't flicker between two.</summary>
        public const float Hold = 3f;

        public static RenownBand BandOf(float score) =>
            score <= InfamousAt ? RenownBand.Infamous : score >= PeoplesAt ? RenownBand.Peoples : score >= RegionAt ? RenownBand.Region : score >= TownAt ? RenownBand.Town : RenownBand.Unknown;

        /// <summary>Where a band begins (for the hold).</summary>
        public static float StartOf(RenownBand b)
        {
            switch (b)
            {
                case RenownBand.Town: return TownAt;
                case RenownBand.Region: return RegionAt;
                case RenownBand.Peoples: return PeoplesAt;
                default: return 0f;
            }
        }

        /// <summary>The band, holding the one last told while the score is within <see cref="Hold"/> under its start (infamy holds until -12).</summary>
        public static RenownBand Band(float score, RenownBand previous)
        {
            var b = BandOf(score);
            if (previous == RenownBand.Infamous && b == RenownBand.Unknown && score <= InfamousAt + Hold) return RenownBand.Infamous;
            if (b >= RenownBand.Unknown && previous > b && previous > RenownBand.Unknown && score >= StartOf(previous) - Hold) return previous;
            return b;
        }

        /// <summary>The band moved from the one last told: up or down, to be told and written (dip:renown).</summary>
        public static bool Moved(RenownBand told, RenownBand now) => told != now;

        /// <summary>
        /// The word one of the house says on the map as its band moves (the moment of Rimfolk_Lines_Renown.xml), or null:
        /// renownup as its name carries further (its words say strangers know it), renowndown as it fades from the talk
        /// (its words say nobody out there speaks of it); nothing for a name gone bad or mending, nor for one that is still
        /// known, only less widely, where those words would not be true.
        /// </summary>
        public static string Bubble(RenownBand was, RenownBand now)
        {
            if (now > was && now >= RenownBand.Town) return "renownup";
            if (now < was && now == RenownBand.Unknown) return "renowndown";
            return null;
        }

        /// <summary>
        /// The bubble's tag for the band a name fell from (the round d text pass): one told across the region or wider was
        /// sung in the halls, in the papers, told at every fire, so renowndown's words may say so; one known only in a town
        /// nearby was talked of there and no further. Null otherwise. The bubble's own house tags read the band it has now.
        /// </summary>
        public static string BubbleTag(RenownBand was, RenownBand now) => now < was && was >= RenownBand.Region ? WasRenownedTag : null;

        /// <summary>A renowndown bubble's tag: the name fell from the region's band or wider.</summary>
        public const string WasRenownedTag = "house:wasrenowned";

        // ================= Known for =================

        /// <summary>A kind is what a house is known for when it has this many points or more, and this share of what all kinds showed.</summary>
        public const float KnownAt = 6f, KnownShare = 0.3f;

        /// <summary>The kind with the most of a house's renown behind it (the first on a tie), when it is enough; else none.</summary>
        public static KnownFor KnownForOf(float[] byKind)
        {
            if (byKind == null) return KnownFor.None;
            float all = 0f, best = 0f;
            int at = 0;
            for (int i = 1; i < byKind.Length; i++)
            {
                if (byKind[i] <= 0f) continue;
                all += byKind[i];
                if (byKind[i] > best)
                {
                    best = byKind[i];
                    at = i;
                }
            }
            return at > 0 && best >= KnownAt && best >= KnownShare * all ? (KnownFor)at : KnownFor.None;
        }

        // ================= What it does =================

        /// <summary>A notable thinks this much better of one of a renowned house's people (up to 15, at 60), or worse of an infamous one's (down to -10).</summary>
        public static float RegardBonus(float score) => score >= 0f ? Math.Min(15f, score / 4f) : Math.Max(-10f, score / 3f);

        /// <summary>A good word put in for one of a renowned house's people counts up to half again (at 60).</summary>
        public static float GoodWordFactor(float score) => 1f + Clamp(score / 120f, 0f, 0.5f);

        /// <summary>A match with one of a renowned house's people is offered and accepted up to twice as readily (at 60).</summary>
        public static float MatchFactor(float score) => 1f + Clamp(score / 60f, 0f, 1f);

        /// <summary>The chance of a match taken up by this factor, never certain.</summary>
        public static float MatchChance(float chance, float factor) => Clamp(chance * Math.Max(1f, factor), 0f, 0.95f);

        /// <summary>
        /// The events the world sends a colony with a house known for something: challengers to fighters, the sick and the
        /// hurt to healers, guests to hosts, a caravan's room to traders, someone who remembers its work to makers, pilgrims
        /// to the faithful (the events' defNames).
        /// </summary>
        public static readonly Dictionary<KnownFor, string[]> Events = new Dictionary<KnownFor, string[]>
        {
            [KnownFor.Fighters] = new[] { "Council_Evt_Champion" },
            [KnownFor.Healers] = new[] { "Council_Evt_NeighborsSick", "Council_Evt_WoundedStranger" },
            [KnownFor.Hosts] = new[] { "Council_Evt_NotableGuest", "Council_Evt_Tourist" },
            [KnownFor.Traders] = new[] { "Council_Evt_FarMarket" },
            [KnownFor.Makers] = new[] { "Council_Evt_OldTrade" },
            [KnownFor.Faithful] = new[] { "Council_Evt_Pilgrims", "Rimfolk_Evt_RelicPilgrims" }
        };

        /// <summary>How much likelier such an event is, by how far the house's name carries: a quarter in a town, half in the region, three quarters among the peoples.</summary>
        public static float BandFactor(RenownBand b) => b >= RenownBand.Town ? 1f + 0.25f * (int)b : 1f;

        /// <summary>An event's factor at a colony whose houses are known for these things this far (the best of them; 1 for an event none draws).</summary>
        public static float EventFactor(string defName, IEnumerable<KeyValuePair<KnownFor, RenownBand>> houses)
        {
            float f = 1f;
            if (defName == null || houses == null) return f;
            foreach (var h in houses)
                if (h.Key != KnownFor.None && Events.TryGetValue(h.Key, out var evs) && Array.IndexOf(evs, defName) >= 0)
                    f = Math.Max(f, BandFactor(h.Value));
            return f;
        }

        /// <summary>Guests asked to a feast accept a tenth more readily for each band of a house known for hosting (to three tenths).</summary>
        public static float GuestFactor(RenownBand b) => b >= RenownBand.Town ? 1f + 0.1f * (int)b : 1f;

        /// <summary>The travelling champion picks a fighter of a house known for fighters (Shooting or Melee) when one is among the colony's best.</summary>
        public static bool ChallengesFighter(KnownFor k, RenownBand b) => k == KnownFor.Fighters && b >= RenownBand.Town;

        /// <summary>How often the champion's challenge goes to that house's fighter, when there is one.</summary>
        public const float ChallengeShare = 0.6f;

        // ================= Words =================

        /// <summary>What a house is known for, in its own age's words ("good swords"): CC_Renown_For_Fighters_Medieval.</summary>
        public static string ForKey(KnownFor k, WorldAge age) => "CC_Renown_For_" + k + "_" + age;

        /// <summary>
        /// How far its name carries, by the ways of the one who speaks of it ("sung in the halls of the region"):
        /// CC_Renown_Band_Region_Medieval; the Empire's people speak of it by the court's ways, whatever their tech level
        /// (CC_Renown_Band_Region_Empire: "spoken of with honor in the region's noble houses").
        /// </summary>
        public static string BandKey(RenownBand b, WorldAge age, bool empire = false) => "CC_Renown_Band_" + b + "_" + (empire ? EmpireWays : age.ToString());

        /// <summary>The Empire's row of the bands' words: the court's ways (it keeps its own titles and honor).</summary>
        public const string EmpireWays = "Empire";

        /// <summary>The kinds with words of their own (None has none).</summary>
        public static readonly KnownFor[] Kinds = { KnownFor.Fighters, KnownFor.Healers, KnownFor.Hosts, KnownFor.Traders, KnownFor.Makers, KnownFor.Faithful };

        /// <summary>The bands with words of their own.</summary>
        public static readonly RenownBand[] Bands = { RenownBand.Infamous, RenownBand.Town, RenownBand.Region, RenownBand.Peoples };

        /// <summary>A kind's code in tags (house:knownfor:healers).</summary>
        public static string Code(KnownFor k) => k.ToString().ToLowerInvariant();

        // ================= The facts =================

        /// <summary>
        /// Talk and letter tags (Diplomacy/FactsRenown.cs). Of the colonist speaking (or writing): one of a house of the colony
        /// (house:ours, {HOUSE}), its name known at least in a town (house:known), in the region or wider (house:renowned),
        /// among the peoples in reach (house:wide), known for the wrong things (house:infamous), known for something
        /// (house:knownfor:KIND, {KNOWNFOR} in the house's own age's words), how far it is known by the ways of the one
        /// spoken to ({RENOWN}). Of the one spoken to: one of a house the colony knows (them:house, {THEIRHOUSE}), renowned
        /// (them:house:renowned), holding a grudge against the colony or the speaker's house (them:house:feud).
        /// </summary>
        public const string OursTag = "house:ours", KnownTag = "house:known", RenownedTag = "house:renowned", WideTag = "house:wide", InfamousTag = "house:infamous",
            ForPrefix = "house:knownfor:", TheirsTag = "them:house", TheirsRenownedTag = "them:house:renowned", FeudTag = "them:house:feud";

        public const string HouseKey = "HOUSE", KnownForKey = "KNOWNFOR", RenownKey = "RENOWN", TheirHouseKey = "THEIRHOUSE";

        public static string ForTag(KnownFor k) => k == KnownFor.None ? null : ForPrefix + Code(k);

        public static string[] Tags()
        {
            var list = new List<string> { OursTag, KnownTag, RenownedTag, WideTag, InfamousTag, TheirsTag, TheirsRenownedTag, FeudTag };
            foreach (var k in Kinds) list.Add(ForTag(k));
            return list.ToArray();
        }

        /// <summary>The tags of a colonist's house as it stands (null for none of a house).</summary>
        public static List<string> OurTags(RenownBand b, KnownFor k)
        {
            var list = new List<string> { OursTag };
            if (b >= RenownBand.Town) list.Add(KnownTag);
            if (b >= RenownBand.Region) list.Add(RenownedTag);
            if (b >= RenownBand.Peoples) list.Add(WideTag);
            if (b == RenownBand.Infamous) list.Add(InfamousTag);
            // A known-for is said only of a name known somewhere.
            if (k != KnownFor.None && b >= RenownBand.Town) list.Add(ForTag(k));
            return list;
        }

        // ================= Helpers =================

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
