namespace Balance
{
    /// <summary>
    /// Rimfolk's own rates, read from its source (see <see cref="SourceConstants"/>): the file and the line each comes from
    /// are in <see cref="Read"/>. The settings' defaults come from CouncilSettings.cs as a whole (<see cref="Settings"/>).
    /// </summary>
    public static class Code
    {
        // Wishes (CouncilGameComponent.cs, CouncilDynamics.cs, Standing.cs, Standards.cs)
        public static float WishSettleDays, TrustGrant, TrustGrantPromised, TrustIgnored, TrustBroken, TrustPromised, TrustDecay;
        public static float StandardsUpHigh, StandardsUp, StandardsDown, StandardsDrift;
        public static float TrustTrusting, TrustDevoted, TrustDoubtful, TrustBitter;
        public static float EventWishChance;
        // Dilemmas (Dilemmas/Dilemmas.cs)
        public static float DilemmaStartDay, DilemmaRaidChance, DilemmaGapDays;
        // Events (Happenings/Happenings.cs)
        public static float EventMaxShare, EventSameGroup;
        // Encounters (Strangers/Encounters.cs)
        public static readonly Dictionary<string, float> EncounterHourly = new Dictionary<string, float>();
        public static readonly Dictionary<string, float> StrangerDilemmaHourly = new Dictionary<string, float>();
        public static float EncounterMaxPerDay;
        // Social events (Social/SocialEvents.cs)
        public static float SocialPerColonist, SocialMin, SocialMax, SocialHourly;
        // Moments (Social/Moments.cs) and nuisances (Strangers/Nuisances.cs)
        public static float MomentGapHours, NuisancePerColonist, NuisanceMax, NuisanceGapHours;
        // Dreams (Dreams/Dreams.cs)
        public static float DreamChance, DreamGapHours;
        // Ailments (Ailments/AilmentTracker.cs)
        public static float EverydayCatchingDays, SeriousCatchingDays, MaxEverydayAtOnce;
        // World events (World/WorldSituations.cs)
        public static float SituationsMaxActive;
        // The gap between Rimfolk's questions (Lean/LetterGap.cs; 0 when the code has none)
        public static float LetterGapHours;
        // Decrees (Decrees/Decrees.cs)
        public static float DecreeRequestChance, DecreeRequestGap;

        public static void Read(SourceConstants s)
        {
            const string game = "CouncilGameComponent.cs", dyn = "CouncilDynamics.cs";
            WishSettleDays = s.Num("wishes wait for a new colony (days)", game, @"TicksGame < GenDate\.TicksPerDay \* (\d+)\) return null;", 3);
            TrustGrant = s.Num("trust for a wish granted", dyn, @"w\.promised \? [\d.]+f : ([\d.]+)f, ""CC_Log_Granted""", 12);
            TrustGrantPromised = s.Num("trust for a promised wish granted", dyn, @"w\.promised \? ([\d.]+)f : [\d.]+f, ""CC_Log_Granted""", 18);
            TrustIgnored = s.Num("trust lost for a wish let lapse", dyn, @"standings\.Change\(p, -([\d.]+)f \* ent \* g", 9);
            TrustBroken = s.Num("trust lost for a broken promise", dyn, @"standings\.Change\(p, -([\d.]+)f \* g, ""CC_Log_BrokenPromise""", 18);
            TrustPromised = s.Num("trust for a promise made", dyn, @"standings\.Change\(p, ([\d.]+)f, ""CC_Log_Promised""", 4);
            TrustDecay = s.Num("trust kept each day (drift back to neutral)", "Standing.cs", @"s\.trust \*= ([\d.]+)f;", 0.985f);
            TrustDevoted = s.Num("trust: devoted from", "Standing.cs", @"if \(trust < ([\d.]+)f\) return TrustLevel\.Trusting;", 60);
            TrustTrusting = s.Num("trust: trusting from", "Standing.cs", @"if \(trust < ([\d.]+)f\) return TrustLevel\.Neutral;", 25);
            TrustDoubtful = s.Num("trust: doubtful at or below", "Standing.cs", @"if \(trust <= -([\d.]+)f\) return TrustLevel\.Doubtful;", 15);
            TrustBitter = s.Num("trust: bitter at or below", "Standing.cs", @"if \(trust <= -([\d.]+)f\) return TrustLevel\.Bitter;", 45);
            StandardsUpHigh = s.Num("standards rise per day from 85% mood", "Standards.cs", @"mood >= 0\.85f \? ([\d.]+)f", 0.06f);
            StandardsUp = s.Num("standards rise per day from 70% mood", "Standards.cs", @"mood >= 0\.85f \? [\d.]+f : ([\d.]+)f", 0.04f);
            StandardsDown = s.Num("standards fall per day under 40% mood", "Standards.cs", @"mood < 0\.4f\) rate = -([\d.]+)f", 0.06f);
            StandardsDrift = s.Num("standards fall per day otherwise", "Standards.cs", @"else rate = -([\d.]+)f", 0.01f);
            EventWishChance = s.Num("a discomfort becomes a wish, per hour", dyn, @"options\.Count == 0 \|\| !Rand\.Chance\(([\d.]+)f\)\) return;", 0.35f);

            const string dlm = "Dilemmas/Dilemmas.cs";
            DilemmaStartDay = s.Num("dilemmas wait for a new colony (days)", dlm, @"if \(now < GenDate\.TicksPerDay \* (\d+)\) return;", 5);
            DilemmaRaidChance = s.Num("a dilemma after a raid, per hour", dlm, @"GenDate\.TicksPerDay \* 3 && Rand\.Chance\(([\d.]+)f\)\)", 0.08f);
            DilemmaGapDays = 3;

            EventMaxShare = s.Num("Rimfolk's most of the storyteller's Misc pool", "Happenings/Happenings.cs", @"IncidentCategoryDefOf\.ThreatSmall \? [\d.]+f : ([\d.]+)f", 0.5f);
            EventSameGroup = s.Num("the same kind of event twice running", "Happenings/Happenings.cs", @"ext\.group == t\.lastGroup\) f \*= ([\d.]+)f", 0.5f);

            const string enc = "Strangers/Encounters.cs";
            foreach (var (kind, fb, dfb) in new[] { ("Visitor", 0.1f, 0.03f), ("Trader", 0.1f, 0.03f), ("Lodger", 0.05f, 0.02f), ("Prisoner", 0.03f, 0.004f), ("Fallen", 0.25f, 0.08f), ("Pet", 0.02f, 0.002f), ("Wild", 0.015f, 0.003f) })
            {
                EncounterHourly[kind] = s.Table("encounter chance per hour: " + kind, enc, "HourlyChance", kind, fb);
                StrangerDilemmaHourly[kind] = s.Table("stranger question per hour: " + kind, enc, "DilemmaChance", kind, dfb);
            }
            EncounterMaxPerDay = s.Num("encounters per day at most", enc, @"private const int MaxPerDay = (\d+);", 3);

            const string soc = "Social/SocialEvents.cs";
            SocialPerColonist = s.Num("social events per day: colonists divided by", soc, @"Clamp\(pawns\.Count / ([\d.]+)f", 5);
            SocialMin = s.Num("social events per day at least", soc, @"Clamp\(pawns\.Count / [\d.]+f, ([\d.]+)f", 0.4f);
            SocialMax = s.Num("social events per day at most", soc, @"Clamp\(pawns\.Count / [\d.]+f, [\d.]+f, ([\d.]+)f\)", 2);
            SocialHourly = s.Num("a social event, per waking hour", soc, @"hour > 21 \|\| !Rand\.Chance\(([\d.]+)f\)", 0.3f);

            MomentGapHours = s.Num("hours between one colonist's moments", "Social/Moments.cs", @"MinGapTicks = GenDate\.TicksPerHour \* (\d+);", 6);
            NuisancePerColonist = s.Num("nuisance tries per colonist per hour", "Strangers/Nuisances.cs", @"Mathf\.Min\([\d.]+f, ([\d.]+)f \* colonists\.Count\)", 0.04f);
            NuisanceMax = s.Num("nuisance tries per hour at most", "Strangers/Nuisances.cs", @"Mathf\.Min\(([\d.]+)f, [\d.]+f \* colonists\.Count\)", 0.5f);
            NuisanceGapHours = s.Num("hours between one colonist's nuisances", "Strangers/Nuisances.cs", @"GapTicks = GenDate\.TicksPerHour \* (\d+);", 12);

            DreamChance = s.Num("a dream on waking", "Dreams/Dreams.cs", @"ChancePerWaking = ([\d.]+)f;", 0.3f);
            DreamGapHours = s.Num("hours between dreams", "Dreams/Dreams.cs", @"now - last < GenDate\.TicksPerHour \* (\d+)\) continue;", 18);

            const string ail = "Ailments/AilmentTracker.cs";
            EverydayCatchingDays = s.Num("days between catching ailments (a map)", ail, @"EverydayCatchingDays = ([\d.]+)f;", 20);
            SeriousCatchingDays = s.Num("days between serious illnesses (a map)", ail, @"SeriousCatchingDays = ([\d.]+)f;", 300);
            MaxEverydayAtOnce = s.Num("everyday ailments at once", ail, @"MaxEverydayAtOnce = (\d+);", 2);

            SituationsMaxActive = s.Num("world events at once", "World/WorldSituations.cs", @"MaxActive = (\d+);", 2);

            DecreeRequestChance = s.Num("a decree request, per day", "Decrees/Decrees.cs", @"TicksPerDay \* 3 \|\| !Rand\.Chance\(([\d.]+)f\)\) return;", 0.35f);
            DecreeRequestGap = 3;
            LetterGapHours = s.Num("hours between Rimfolk's questions that can wait", "Lean/LetterGap.cs", @"public const int Hours = (\d+);", 0);
        }
    }

    /// <summary>
    /// The settings a run uses: CouncilSettings' defaults as the source has them, with a preset from the settings
    /// window's catalog laid over them (the same presets the player clicks).
    /// </summary>
    public sealed class Settings
    {
        private readonly Dictionary<string, string> values;
        public readonly string preset;

        public Settings(Dictionary<string, string> defaults, string presetId = null)
        {
            values = new Dictionary<string, string>(defaults);
            preset = presetId ?? "default";
            if (presetId == null) return;
            var p = ColonyCouncil.SettingsCatalog.Preset(presetId);
            if (p == null) throw new ArgumentException("no preset " + presetId);
            foreach (var v in p.values)
            {
                values.TryGetValue(v.field, out var now);
                if (v.onlyIfNow != null && !v.onlyIfNow.Split('|').Contains(now)) continue;
                values[v.field] = v.value == ColonyCouncil.SettingsCatalog.DefaultValue ? defaults.GetValueOrDefault(v.field) : v.value;
            }
        }

        public string S(string f) => values.TryGetValue(f, out var v) ? v : null;
        public float F(string f, float fallback = 0f) => DefSet.F(S(f), fallback);
        public bool B(string f) => S(f) == "true";

        public string Stakes => S("stakes") ?? "Balanced";
        public bool MoodEffects => Stakes != "AdvisorOnly";
        public bool Negatives => Stakes == "Balanced" || Stakes == "Demanding";
        public bool Harsh => Stakes == "Demanding";
        public float PenaltyScale => !Negatives ? 0f : (Harsh ? 1.5f : 1f) * F("penaltyFactor", 1f);
        public float RewardFactor => F("rewardFactor", 1f);

        public ColonyCouncil.LifePressure Life =>
            Enum.TryParse<ColonyCouncil.LifePressure>(S("lifePressure"), out var l) ? l : ColonyCouncil.LifePressure.Normal;

        public int ArcFrequency
        {
            get
            {
                switch (S("arcFrequency"))
                {
                    case "Off": return ColonyCouncil.ArcMath.Off;
                    case "Rare": return ColonyCouncil.ArcMath.Rare;
                    case "Frequent": return ColonyCouncil.ArcMath.Frequent;
                    default: return ColonyCouncil.ArcMath.Normal;
                }
            }
        }
    }
}
