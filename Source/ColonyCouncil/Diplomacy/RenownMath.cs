using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What the colony (or one of its people) is known for, from whichever part of its renown is largest (docs/design/stage2-diplomacy.md, item 12).</summary>
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

    /// <summary>How far the colony's name (or a colonist's own) carries: known for the wrong things, not yet known, in a town, the region, the peoples in reach.</summary>
    public enum RenownBand
    {
        Infamous = -1,
        Unknown = 0,
        Town = 1,
        Region = 2,
        Peoples = 3
    }

    /// <summary>
    /// The colony's renown (docs/design/stage2-diplomacy.md, item 12; built in release 2d.1 as house renown and reshaped by
    /// the player's decision of 5 November 2026: the colony is the family, never a dynasty; decision 17: earned and shown in
    /// words, with a "known for", nothing spent), with no game types so Tools/Tests can check it. Renown is never stored: it
    /// is read when asked from what the world already keeps of the colony's people (the history's deeds, what the peoples
    /// remember of single colonists, the colony's names for them, their offices and titles, the notables' regard) and of the
    /// colony itself (its oaths, pacts and word). Each part is a <see cref="Hit"/> of a <see cref="Source"/>; a deed fades by
    /// its age (full for a year, then a tenth a season down to three tenths); the sum is the score, its band how far the name
    /// is known, its largest kind what the colony is known for. Each colonist's own name is read the same way from their own
    /// deeds alone, and said by the part they are best known for (<see cref="KnownAs"/>: "the one who won the wrestling at the
    /// feast with the Lenlia"). What it does: the notables' regard of the colony's people, their good words, matches, the
    /// events the world sends a colony known for something and a feast's guests; one of its people known by name as a
    /// fighter draws the champion's challenge, and one known by name for something draws its events a little.
    /// </summary>
    public static class RenownMath
    {
        public const int DayTicks = 60000;
        public const float SeasonDays = 15f, YearDays = 60f;

        /// <summary>The colony's band before any day's look has read it (an older save, a new game): set at the first look without a word.</summary>
        public const int NotRead = int.MinValue;

        // ================= Where renown comes from =================

        /// <summary>One kind of renown: its points (a wrong's below zero), what it shows the colony is good at, whether it fades with age, and the most it gives in all.</summary>
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
        /// The kinds of renown, by key. Of the colony's people: a contest won at a feast (the champion), married into another
        /// people, a ward of theirs fostered well as its guardian, a season abroad as the colony's youth or envoy, a term
        /// served as its hostage, a truce sworn as its speaker; what the peoples remember of them by name (a raiders' camp
        /// broken, fighting beside theirs, trading at their post, walking their settlers out, a prisoner of theirs set free,
        /// one of theirs tended; killed at peace, executed, cut open, slighted, brawled with; their raiders brought down count
        /// to a fighter's name, little); the colony's names for them (the Healer, Old Iron), a year in an office, a term as
        /// speaker, the Empire's title, notables who think well of them. The colony's own: an oath or a truce sworn, its word
        /// kept, levies sent, a pact kept; a truce or a pact broken, an oath renounced, the colony's scheme, a call refused,
        /// tribute short, its word broken. Each is held to its cap for the colony as a whole, and for each colonist's own name.
        /// </summary>
        public static readonly Dictionary<string, Source> Sources = new Dictionary<string, Source>
        {
            // The colony's people, from the history.
            ["contest"] = S("contest", 6f, KnownFor.Fighters, true, 24f),
            ["wedding"] = S("wedding", 8f, KnownFor.None, true, 16f),
            ["fostered"] = S("fostered", 6f, KnownFor.Hosts, true, 18f),
            ["abroad"] = S("abroad", 4f, KnownFor.None, true, 8f),
            ["envoy"] = S("envoy", 5f, KnownFor.Hosts, true, 10f),
            ["hostage"] = S("hostage", 3f, KnownFor.None, true, 6f),
            ["parley"] = S("parley", 4f, KnownFor.None, true, 8f),
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
            // The colony's own.
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
            ["wordBroken"] = S("wordBroken", -2f, KnownFor.None, true, 4f)
        };

        /// <summary>The colony's deeds a people remembers (FactionMemory's kinds): the colony's own renown, no one colonist's.</summary>
        public static readonly string[] ColonyDeeds = { "swore", "keptWord", "levies", "brokeTruce", "brokePact", "renounced", "schemed", "refused", "tributeShort", "wordBroken" };

        /// <summary>The history's kinds the colony's people's deeds are read from.</summary>
        public static readonly string[] MemberKinds =
            { DiplomacyMath.Contest, DiplomacyMath.Wedding, DiplomacyMath.Ward, DiplomacyMath.Envoy, DiplomacyMath.Hostage, DiplomacyMath.Parley };

        /// <summary>
        /// The renown an event of the history gives one who took part in it, by its kind and their part (null for none): the
        /// champion (dip:contest, did it), the one married (dip:wedding, did it), a ward's guardian at a good year's end
        /// (dip:ward, did it, the ward named: <paramref name="aboutSomeone"/>) and the colony's youth home with what they
        /// learned (dip:ward, did it, nobody else named), its envoy home (dip:envoy, did it), its hostage home (dip:hostage,
        /// did it for the colony: <paramref name="forColony"/>; a hostage of theirs who stayed did it for nobody's term), the
        /// speaker at a truce sworn (dip:parley, named). An older save's council tasks (dip:task, retired 5 November 2026) are
        /// no source.
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
                default: return null;
            }
        }

        /// <summary>
        /// The one an event names is the colony's own in it: always, but in a contest, whose winner may be a guest, only when
        /// the colony won it (the colony did it). The colony's people are found by the events the colony is a party to, so the
        /// gone count too: what they did is part of its name.
        /// </summary>
        public static bool OursIn(string kind, HistoryRole colonyRole) => kind != DiplomacyMath.Contest || colonyRole == HistoryRole.Did;

        /// <summary>A ward's guardian is named in the year's end as one who did it only for a good year or better (a fair one is no deed to be known by), else as one with it.</summary>
        public static HistoryRole GuardianRole(bool goodYear) => goodYear ? HistoryRole.Did : HistoryRole.With;

        /// <summary>What the peoples remember of a colonist by name (a mark's kind, ColonistMemoryMath), as renown: a source, or null for one that isn't.</summary>
        public static string SourceOfMark(string markKind) => markKind != null && Sources.ContainsKey(markKind) && Array.IndexOf(MarkKinds, markKind) >= 0 ? markKind : null;

        /// <summary>The marks of the peoples' memory that renown reads.</summary>
        public static readonly string[] MarkKinds =
            { "campBroken", "foughtBeside", "killedRaid", "killedField", "tradedPost", "walkedSettlers", "released", "tended", "killedPeace", "executed", "harvested", "slighted", "brawled" };

        /// <summary>
        /// What each of the colony's names for its people (EpithetDefs) shows it is good at; a name not listed here still
        /// counts, with no known-for, and a dubious one (the Kindling) not at all.
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

        /// <summary>A year in office, by the record the game keeps of its days (Council_DaysAsPhysician): what it shows the colonist is good at.</summary>
        public static readonly Dictionary<string, KnownFor> Offices = new Dictionary<string, KnownFor>
        {
            ["Council_DaysAsPhysician"] = KnownFor.Healers, ["Council_DaysAsMarshal"] = KnownFor.Fighters
        };

        /// <summary>Whole years in an office from its days: a year is 60 days.</summary>
        public static int YearsOf(float days) => days <= 0f ? 0 : (int)(days / YearDays);

        /// <summary>The Empire's title as renown points (read, never given): a fiftieth of its seniority, at most twelve (a count's).</summary>
        public static float TitlePoints(int seniority) => Clamp(seniority / 50f, 0f, 12f);

        /// <summary>A notable who thinks this well of one of the colony's people (their own regard of them) carries the colony's name to their town.</summary>
        public const float KnownToRegard = 20f;

        // ================= Reading it =================

        /// <summary>One part of a renown: its source, how old it is (days; 0 for one held now), how many times, and what it shows (None: the source's own).</summary>
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

        /// <summary>A renown as read: the score, each kind's part, and each source's, largest first.</summary>
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
        /// A renown from its parts: each source's points times its count and (for a deed) its fade, summed and held within its
        /// cap either way; the score is their sum, each kind's part the positive points it showed, and the band and known-for
        /// follow (<paramref name="previous"/>: the band last told, for the hold).
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
            // Largest first; a tie by the source's name, so the order (and a colonist's own name) is the same each time.
            r.parts.Sort((a, b) =>
            {
                int c = Math.Abs(b.Value).CompareTo(Math.Abs(a.Value));
                return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
            });
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

        /// <summary>The colony's band as last told, from its saved number: none read yet (<see cref="NotRead"/>) reads as not yet known.</summary>
        public static RenownBand Told(int saved) => saved == NotRead || saved < (int)RenownBand.Infamous || saved > (int)RenownBand.Peoples ? RenownBand.Unknown : (RenownBand)saved;

        /// <summary>The band moved from the one last told: up or down, to be told and written (dip:renown). Never on the first look (<see cref="NotRead"/>): an older save's name or a new game's is set quietly.</summary>
        public static bool Moved(RenownBand told, RenownBand now) => told != now;

        public static bool MovedFrom(int saved, RenownBand now) => saved != NotRead && Moved(Told(saved), now);

        /// <summary>
        /// The word one of the colony says on the map as its band moves (the moment of Rimfolk_Lines_Renown.xml), or null:
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
        /// The bubble's tag for the band a name fell from: one told across the region or wider was sung in the halls, in the
        /// papers, told at every fire, so renowndown's words may say so; one known only in a town nearby was talked of there
        /// and no further. Null otherwise. The bubble's own tags read the band it has now.
        /// </summary>
        public static string BubbleTag(RenownBand was, RenownBand now) => now < was && was >= RenownBand.Region ? WasRenownedTag : null;

        /// <summary>A renowndown bubble's tag: the name fell from the region's band or wider.</summary>
        public const string WasRenownedTag = "colony:wasrenowned";

        // ================= Known for =================

        /// <summary>A kind is what the colony (or a colonist) is known for when it has this many points or more, and this share of what all kinds showed.</summary>
        public const float KnownAt = 6f, KnownShare = 0.3f;

        /// <summary>The kind with the most of a renown behind it (the first on a tie), when it is enough; else none.</summary>
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

        // ================= A colonist's own name =================

        /// <summary>
        /// The part of a colonist's own renown they are known by: the largest of their good deeds when their name is known
        /// somewhere (a town nearby or wider), the largest of their wrongs when it is known for the wrong things; null while
        /// nobody outside speaks of them. The colony's own deeds are no one colonist's.
        /// </summary>
        public static string KnownAs(Reading r)
        {
            if (r == null) return null;
            bool bad = r.band == RenownBand.Infamous;
            if (!bad && r.band < RenownBand.Town) return null;
            foreach (var p in r.parts)
                if (bad ? p.Value < 0f : p.Value > 0f)
                    return p.Key;
            return null;
        }

        /// <summary>The parts said of a colonist with the people they were done with or for (a mark's, a deed's); the rest (the colony's names, an office, a title, a notable's regard) name no people.</summary>
        public static bool NamesPeople(string part) => part != null && part != "epithet" && part != "office" && part != "speaker" && part != "title" && part != "regard";

        /// <summary>
        /// Whether the people of the one spoken to know a colonist's own name: one told across the region or wider is known to
        /// every people; one known in a town nearby to the people their deed was done with or for, and, for a part that names
        /// no people (the colony's name for them, an office), to the colony's neighbours.
        /// </summary>
        public static bool TheyKnow(RenownBand band, bool theirPeopleInIt, bool namesPeople) =>
            band >= RenownBand.Region || band == RenownBand.Town && (theirPeopleInIt || !namesPeople);

        // ================= What it does =================

        /// <summary>A notable thinks this much better of the colony's people for its renown (up to 15, at 60), or worse for an infamous name (down to -10).</summary>
        public static float RegardBonus(float score) => score >= 0f ? Math.Min(15f, score / 4f) : Math.Max(-10f, score / 3f);

        /// <summary>A good word put in for one of a renowned colony counts up to half again (at 60).</summary>
        public static float GoodWordFactor(float score) => 1f + Clamp(score / 120f, 0f, 0.5f);

        /// <summary>A match with one of a renowned colony is offered and accepted up to twice as readily (at 60).</summary>
        public static float MatchFactor(float score) => 1f + Clamp(score / 60f, 0f, 1f);

        /// <summary>The chance of a match taken up by this factor, never certain.</summary>
        public static float MatchChance(float chance, float factor) => Clamp(chance * Math.Max(1f, factor), 0f, 0.95f);

        /// <summary>
        /// The events the world sends a colony known for something: challengers to fighters, the sick and the hurt to healers,
        /// guests to hosts, a caravan's room to traders, someone who remembers its work to makers, pilgrims to the faithful
        /// (the events' defNames).
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

        /// <summary>How much likelier such an event is, by how far the colony's name carries: a quarter in a town, half in the region, three quarters among the peoples.</summary>
        public static float BandFactor(RenownBand b) => b >= RenownBand.Town ? 1f + 0.25f * (int)b : 1f;

        /// <summary>An event's factor for names known for these things this far (the best of them; 1 for an event none draws).</summary>
        public static float EventFactor(string defName, IEnumerable<KeyValuePair<KnownFor, RenownBand>> names)
        {
            float f = 1f;
            if (defName == null || names == null) return f;
            foreach (var h in names)
                if (h.Key != KnownFor.None && Events.TryGetValue(h.Key, out var evs) && Array.IndexOf(evs, defName) >= 0)
                    f = Math.Max(f, BandFactor(h.Value));
            return f;
        }

        /// <summary>
        /// One of the colony known by name for something draws its events a little, whatever the colony's own name: a quarter
        /// more at most (a town's band), the best of the colony's factor and theirs counting, never both.
        /// </summary>
        public const RenownBand OwnEventBand = RenownBand.Town;

        public static float OwnFactor(string defName, IEnumerable<KnownFor> named)
        {
            if (defName == null || named == null) return 1f;
            var list = new List<KeyValuePair<KnownFor, RenownBand>>();
            foreach (var k in named) list.Add(new KeyValuePair<KnownFor, RenownBand>(k, OwnEventBand));
            return EventFactor(defName, list);
        }

        /// <summary>Guests asked to a feast accept a tenth more readily for each band of a colony known for hosting (to three tenths).</summary>
        public static float GuestFactor(RenownBand b) => b >= RenownBand.Town ? 1f + 0.1f * (int)b : 1f;

        /// <summary>The travelling champion picks a colonist known by name for fighting (Shooting or Melee) when one is among the colony's best.</summary>
        public static bool ChallengesFighter(KnownFor k, RenownBand b) => k == KnownFor.Fighters && b >= RenownBand.Town;

        /// <summary>How often the champion's challenge goes to that colonist, when there is one.</summary>
        public const float ChallengeShare = 0.6f;

        // ================= Words =================

        /// <summary>What the colony is known for, in its own age's words ("good swords"): CC_Renown_For_Fighters_Medieval.</summary>
        public static string ForKey(KnownFor k, WorldAge age) => "CC_Renown_For_" + k + "_" + age;

        /// <summary>
        /// How far its name carries, by the ways of the one who speaks of it ("sung in the halls of the region"):
        /// CC_Renown_Band_Region_Medieval; the Empire's people speak of it by the court's ways, whatever their tech level
        /// (CC_Renown_Band_Region_Empire: "spoken of with honor in the region's noble houses").
        /// </summary>
        public static string BandKey(RenownBand b, WorldAge age, bool empire = false) => "CC_Renown_Band_" + b + "_" + (empire ? EmpireWays : age.ToString());

        /// <summary>The Empire's row of the bands' words: the court's ways (it keeps its own titles and honor).</summary>
        public const string EmpireWays = "Empire";

        /// <summary>A colonist's own name by the part they are known by ("the one who won the wrestling at the feast with the Lenlia"): CC_Renown_KnownAs_contest; _Any where no people is named.</summary>
        public static string KnownAsWords(string part, bool withPeople) => "CC_Renown_KnownAs_" + part + (withPeople || !NamesPeople(part) ? "" : "_Any");

        /// <summary>The kinds with words of their own (None has none).</summary>
        public static readonly KnownFor[] Kinds = { KnownFor.Fighters, KnownFor.Healers, KnownFor.Hosts, KnownFor.Traders, KnownFor.Makers, KnownFor.Faithful };

        /// <summary>The bands with words of their own.</summary>
        public static readonly RenownBand[] Bands = { RenownBand.Infamous, RenownBand.Town, RenownBand.Region, RenownBand.Peoples };

        /// <summary>A kind's code in tags (colony:knownfor:healers).</summary>
        public static string Code(KnownFor k) => k.ToString().ToLowerInvariant();

        // ================= The facts =================

        /// <summary>
        /// Talk, letter and bubble tags (Diplomacy/FactsRenown.cs, Outsiders.Line). The colony's name known at least in a town
        /// nearby (colony:known), across the region or wider (colony:renowned), among every people in reach (colony:wide), for
        /// the wrong things (colony:infamous), and known for something (colony:knownfor:KIND; {KNOWNFOR} in the colony's own
        /// age's words); how far, in the ways of the one spoken to ({RENOWN}). The colonist speaking known by their own name to
        /// the people of the one spoken to (us:knownas, us:knownas:KIND; {KNOWNAS} "the one who broke the raiders' camp").
        /// The houses' tags of release 2d.1 (house:..., them:house...) went with the houses (5 November 2026): nothing sets them.
        /// </summary>
        public const string KnownTag = "colony:known", RenownedTag = "colony:renowned", WideTag = "colony:wide", InfamousTag = "colony:infamous",
            ForPrefix = "colony:knownfor:", KnownAsTag = "us:knownas", KnownAsPrefix = "us:knownas:";

        public const string KnownForKey = "KNOWNFOR", RenownKey = "RENOWN", KnownAsKey = "KNOWNAS";

        public static string ForTag(KnownFor k) => k == KnownFor.None ? null : ForPrefix + Code(k);

        public static string KnownAsTagOf(KnownFor k) => k == KnownFor.None ? null : KnownAsPrefix + Code(k);

        public static string[] Tags()
        {
            var list = new List<string> { KnownTag, RenownedTag, WideTag, InfamousTag, KnownAsTag };
            foreach (var k in Kinds) list.Add(ForTag(k));
            foreach (var k in Kinds) list.Add(KnownAsTagOf(k));
            return list.ToArray();
        }

        /// <summary>The colony's tags as its name stands (none while it is not yet known and not infamous).</summary>
        public static List<string> ColonyTags(RenownBand b, KnownFor k)
        {
            var list = new List<string>();
            if (b >= RenownBand.Town) list.Add(KnownTag);
            if (b >= RenownBand.Region) list.Add(RenownedTag);
            if (b >= RenownBand.Peoples) list.Add(WideTag);
            if (b == RenownBand.Infamous) list.Add(InfamousTag);
            // A known-for is said only of a name known somewhere.
            if (k != KnownFor.None && b >= RenownBand.Town) list.Add(ForTag(k));
            return list;
        }

        /// <summary>A colonist's own tags in talk: known by a good name to the people of the one spoken to, and for what (none for a bad name: the peoples' memory has its own words for that, you:foe).</summary>
        public static List<string> OwnTags(Reading r, bool theyKnow)
        {
            var list = new List<string>();
            if (r == null || !theyKnow || r.band < RenownBand.Town || KnownAs(r) == null) return list;
            list.Add(KnownAsTag);
            if (r.known != KnownFor.None) list.Add(KnownAsTagOf(r.known));
            return list;
        }

        // ================= Helpers =================

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
