using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// One colonist's hour: feelings from their mood thoughts and situation, the heart's needs, strain built and drained,
    /// and (rarely) an episode. Called by <see cref="HeartTracker"/> once an hour per colonist.
    /// </summary>
    public static class HeartLogic
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static readonly List<Thought> groups = new List<Thought>();

        public static void Evaluate(Pawn p, PawnHeart h, HeartMapContext m, int now)
        {
            if (p.needs?.mood?.thoughts == null) return;
            LookUpDefs();
            h.EnsureLists();
            bool first = h.lastEvalTick < 0;
            float hours = first ? 1f : Mathf.Clamp((now - h.lastEvalTick) / (float)GenDate.TicksPerHour, 0.25f, 3f);
            int since = first ? now : h.lastEvalTick;
            h.lastEvalTick = now;

            int day = now / GenDate.TicksPerDay;
            if (day != h.day)
            {
                bool newDay = h.day >= 0;
                h.day = day;
                for (int i = 0; i < h.today.Count; i++) h.today[i] = 0f;
                h.todayTotal = 0f;
                h.todayGathering = 0f;
                h.todayFriends = 0f;
                if (newDay) Daily(p, h, now);
                h.strainDayAgo = h.strain;
                h.strainDayAgoTick = now;
            }
            float decay = Mathf.Pow(HeartMath.RecentDecayPerHour, hours);
            for (int i = 0; i < h.recentBuild.Count; i++) h.recentBuild[i] *= decay;
            for (int i = 0; i < h.recentRelief.Count; i++) h.recentRelief[i] *= decay;

            NoteWork(p, h);
            NoteBreak(p, h, now);
            var traits = Heart.TraitsOf(p);

            groups.Clear();
            p.needs.mood.thoughts.GetDistinctMoodThoughtGroups(groups);
            Feelings.Compute(p, h, m, groups, now, traits);
            HeartNeeds.Compute(p, h, m);
            Strain(p, h, m, groups, since, now, hours, traits);
            groups.Clear();

            h.evaluated = true;
            HeartView.Refresh(p, h);
            Episodes.MaybeStart(p, h, m, now);
        }

        /// <summary>Feelings, needs and the inspector line, worked out again without building or draining strain (after a load).</summary>
        public static void Peek(Pawn p, PawnHeart h, HeartMapContext m, int now)
        {
            if (p.needs?.mood?.thoughts == null) return;
            LookUpDefs();
            h.EnsureLists();
            groups.Clear();
            p.needs.mood.thoughts.GetDistinctMoodThoughtGroups(groups);
            Feelings.Compute(p, h, m, groups, now, Heart.TraitsOf(p));
            HeartNeeds.Compute(p, h, m);
            groups.Clear();
            h.evaluated = true;
            HeartView.Refresh(p, h);
        }

        // ---------------- Once a day ----------------

        private static void Daily(Pawn p, PawnHeart h, int now)
        {
            var comp = CouncilGameComponent.Instance;
            foreach (var f in h.feelings)
            {
                var d = f.def;
                // Grateful to the council for what it did: a point of trust, every few days while it lasts.
                if (d.trust > 0f && f.fromCouncil && f.Share >= 0.4f && comp != null && S.enableWishes && p.IsFreeNonSlaveColonist
                    && now - h.lastTrustTick >= GenDate.TicksPerDay * 3)
                {
                    comp.standings.Change(p, d.trust, "CC_Heart_TrustGrateful".Translate());
                    h.lastTrustTick = now;
                }
                // Awe now and then becomes an inspiration: rare, and once a month at most.
                if (d.inspirationPerDay > 0f && now - h.lastInspirationTick > GenDate.TicksPerDay * 30 && Rand.Chance(d.inspirationPerDay * f.Share))
                {
                    var handler = p.mindState?.inspirationHandler;
                    if (handler != null && !handler.Inspired && !p.Downed && !p.InMentalState)
                    {
                        var insp = handler.GetRandomAvailableInspirationDef();
                        if (insp != null && handler.TryStartInspiration(insp, "CC_Heart_AweInspired".Translate(p.Named("PAWN")).Resolve()))
                            h.lastInspirationTick = now;
                    }
                }
            }
        }

        // ---------------- Small observations ----------------

        /// <summary>Notes what kind of work they're doing this hour, for variety (restlessness and freedom).</summary>
        private static void NoteWork(Pawn p, PawnHeart h)
        {
            int kind = 0;
            var wt = p.CurJob?.workGiverDef?.workType;
            if (wt != null) kind = wt.shortHash + 1;
            h.workSamples[h.workSampleIndex] = kind;
            h.workSampleIndex = (h.workSampleIndex + 1) % h.workSamples.Length;
        }

        /// <summary>How many kinds of work they did in the hours sampled (and in how many of them they worked at all).</summary>
        public static int WorkVariety(PawnHeart h, out int working)
        {
            working = 0;
            int distinct = 0;
            for (int i = 0; i < h.workSamples.Length; i++)
            {
                int k = h.workSamples[i];
                if (k == 0) continue;
                working++;
                bool seen = false;
                for (int j = 0; j < i; j++)
                    if (h.workSamples[j] == k)
                    {
                        seen = true;
                        break;
                    }
                if (!seen) distinct++;
            }
            return distinct;
        }

        private static readonly HashSet<string> EmbarrassingBreaks = new HashSet<string>
        {
            "Binging_DrugMajor", "Binging_DrugExtreme", "Binging_Food", "InsultingSpree", "TargetedInsultingSpree", "Tantrum",
            "TargetedTantrum", "BedroomTantrum", "Berserk", "Jailbreaker", "Slaughterer", "MurderousRage", "FireStartingSpree",
            "CorpseObsession", "SadisticRage", "RunWild", "SocialFighting"
        };

        /// <summary>A break they may be ashamed of afterwards (shame reads when it ended).</summary>
        private static void NoteBreak(Pawn p, PawnHeart h, int now)
        {
            var state = p.MentalStateDef;
            if (state == null || state == Episodes.Def) return;
            if (EmbarrassingBreaks.Contains(state.defName) || state.IsAggro)
            {
                h.lastBreak = state.label ?? state.defName;
                h.lastBreakTick = now;
            }
        }

        // ---------------- Strain ----------------

        private static void Strain(Pawn p, PawnHeart h, HeartMapContext m, List<Thought> moodGroups, int since, int now, float hours, StrainTraits traits)
        {
            float speed = Mathf.Clamp(S.strainSpeed, 0.5f, 1.5f);
            float built = 0f;
            float Build(StrainSource s, float raw)
            {
                int i = (int)s;
                float today = h.today[i];
                float added = HeartMath.Add(raw, speed, HeartMath.TraitFactor(s, traits), ref today, HeartMath.SourceCap(s), ref h.todayTotal, HeartMath.DailyCap);
                h.today[i] = today;
                h.recentBuild[i] += added;
                built += added;
                return added;
            }

            var health = p.health;
            bool spawned = p.Spawned;
            bool threatHere = m.threat && spawned && m.map == p.Map;
            var mind = p.mindState;
            bool harmed = mind != null && now - mind.lastHarmTick < GenDate.TicksPerHour;
            if (threatHere && (p.Drafted || harmed || (mind != null && mind.WasRecentlyCombatantTicks(GenDate.TicksPerHour))))
                Build(StrainSource.Combat, 4f * hours);
            float pain = health?.hediffSet?.PainTotal ?? 0f;
            if (pain >= 0.2f) Build(StrainSource.Pain, pain * 5f * hours);
            if ((health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.6f) Build(StrainSource.Pain, 1f * hours);
            if (p.Downed) Build(StrainSource.Downed, 3f * hours);
            bool awake = RestUtility.Awake(p);
            var needs = p.needs;
            if (awake && needs.rest != null && needs.rest.CurLevelPercentage < 0.12f) Build(StrainSource.Exhaustion, 1.5f * hours);
            if (needs.joy != null && needs.joy.CurLevelPercentage < 0.1f) Build(StrainSource.Joyless, 1f * hours);
            if (needs.food != null)
            {
                var hunger = needs.food.CurCategory;
                if (hunger == HungerCategory.Starving) Build(StrainSource.Hunger, 1.5f * hours);
                else if (hunger == HungerCategory.UrgentlyHungry) Build(StrainSource.Hunger, 0.75f * hours);
            }
            float mood = needs.mood.CurLevelPercentage;
            if (mood < 0.2f) Build(StrainSource.LowMood, 1f * hours);
            else if (mood < 0.3f) Build(StrainSource.LowMood, 0.5f * hours);
            h.lowMoodHours = mood < 0.3f ? Mathf.Min(96f, h.lowMoodHours + hours) : h.lowMoodHours * Mathf.Pow(0.96f, hours);
            // A dark home, not the night sky: the game gives "in darkness" to anyone awake outdoors at night too.
            if (darkThought != null && spawned && p.Position.Roofed(p.Map))
                for (int i = 0; i < moodGroups.Count; i++)
                    if (moodGroups[i].def == darkThought)
                    {
                        Build(StrainSource.Dark, 0.5f * hours);
                        break;
                    }
            // Decrees that ask a lot, for those who resent them.
            var comp = CouncilGameComponent.Instance;
            if (comp?.decrees?.active != null && p.IsFreeColonist)
                foreach (var a in comp.decrees.active)
                {
                    if (a?.def == null || a.def.strainPerDay <= 0f) continue;
                    var reaction = a.def.ReactionOf(p);
                    if (reaction > DecreeReaction.Dislike) continue;
                    Build(StrainSource.Council, a.def.strainPerDay / 24f * (reaction == DecreeReaction.Hate ? 1.5f : 1f) * hours);
                }
            // Feelings that wear on them (fear, grief, dread, despair).
            for (int i = 0; i < h.feelings.Count; i++)
            {
                var f = h.feelings[i];
                if (f.def.strainPerHour > 0f) Build(f.def.strainSource, f.def.strainPerHour * f.Share * hours);
            }

            // ---- Relief ----
            float relief = 0f, sleep = 0f, rec = 0f, quiet = 0f;
            bool calm = !threatHere && !p.Downed && pain < 0.3f;
            if (calm) quiet = 0.15f;
            if (!awake)
            {
                var bed = spawned ? RestUtility.CurrentBed(p) : null;
                bool comfy = bed != null && SafeComfortable(p);
                sleep = comfy ? 0.6f : 0.25f;
            }
            else if (p.CurJobDef?.joyKind != null) rec = 0.5f;
            float factor = mood >= 0.85f ? 1.5f : mood >= 0.7f ? 1.25f : mood < 0.3f ? 0.7f : 1f;
            factor *= Heart.FeelingRelief(h) * HeartMath.ReliefTraitFactor(traits);
            if (h.needs != null && h.needs.Length == 4 && h.needs[0].value >= 60f && h.needs[1].value >= 60f && h.needs[2].value >= 60f && h.needs[3].value >= 60f)
                factor *= 1.2f;
            float decree = Heart.DecreeRelief();
            factor *= decree;
            float council = decree > 1f && calm && p.IsFreeColonist ? 0.25f * hours : 0f;
            quiet *= factor * hours;
            sleep *= factor * hours;
            rec *= factor * hours;
            h.recentRelief[(int)StrainRelief.Quiet] += quiet;
            h.recentRelief[(int)StrainRelief.Sleep] += sleep;
            h.recentRelief[(int)StrainRelief.Recreation] += rec;
            h.recentRelief[(int)StrainRelief.Council] += council;
            relief = quiet + sleep + rec + council;

            // ---- New memories: deaths and bad nights build it, gatherings, good talks and a cathartic cry drain it ----
            relief += Memories(p, h, since, now, Build, HeartMath.ReliefTraitFactor(traits));

            h.lastHourBuild = built;
            h.lastHourRelief = relief;
            h.strain = HeartMath.Clamp(h.strain + built - relief);
        }

        private static bool SafeComfortable(Pawn p)
        {
            try
            {
                return GenTemperature.ComfortableTemperatureAtCell(p, p.Position, p.Map);
            }
            catch
            {
                return true;
            }
        }

        private static ThoughtDef darkThought;
        private static bool darkLooked;

        /// <summary>The vanilla "dark environment" thought, looked up once.</summary>
        public static void LookUpDefs()
        {
            if (darkLooked) return;
            darkLooked = true;
            darkThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("EnvironmentDark");
        }

        // ---------------- Memories ----------------

        private enum RuleKind
        {
            Build,
            Gathering,
            Friends,
            Catharsis
        }

        private struct MemoryRule
        {
            public RuleKind kind;
            public StrainSource source;
            public float amount;
            /// <summary>Only while the memory hurts (1) or helps (-1); 0 either way.</summary>
            public int only;
        }

        private static readonly (string pattern, RuleKind kind, StrainSource source, float amount, int only)[] Rules =
        {
            // Deaths and losses (each memory once, when it's new).
            ("KnowColonistDied", RuleKind.Build, StrainSource.Loss, 4f, 0),
            ("WitnessedDeathAlly", RuleKind.Build, StrainSource.Loss, 5f, 0),
            ("WitnessedDeathFamily", RuleKind.Build, StrainSource.Loss, 8f, 0),
            ("WitnessedDeathNonAlly", RuleKind.Build, StrainSource.Loss, 1.5f, 0),
            ("PawnWithGoodOpinionDied", RuleKind.Build, StrainSource.Loss, 4f, 0),
            ("PawnWithGoodOpinionLost", RuleKind.Build, StrainSource.Loss, 2f, 0),
            ("ColonistLost", RuleKind.Build, StrainSource.Loss, 3f, 0),
            ("BondedAnimalDied", RuleKind.Build, StrainSource.Loss, 5f, 0),
            ("BondedAnimalLost", RuleKind.Build, StrainSource.Loss, 3f, 0),
            ("My*Died", RuleKind.Build, StrainSource.Loss, 8f, 0),
            ("My*Lost", RuleKind.Build, StrainSource.Loss, 4f, 0),
            ("PsychicBondTorn", RuleKind.Build, StrainSource.Loss, 8f, 0),
            ("Stillbirth", RuleKind.Build, StrainSource.Loss, 8f, 0),
            ("Miscarried", RuleKind.Build, StrainSource.Loss, 6f, 0),
            ("PartnerMiscarried", RuleKind.Build, StrainSource.Loss, 5f, 0),
            ("OtherTravelerDied", RuleKind.Build, StrainSource.Loss, 3f, 0),
            ("Council_Pet_Lost", RuleKind.Build, StrainSource.Loss, 3f, 0),
            ("Council_FriendQuit", RuleKind.Build, StrainSource.Loss, 2f, 0),
            // Bad nights (once per sleep: the game renews the memory each night).
            ("SleptInCold", RuleKind.Build, StrainSource.BadSleep, 2f, 0),
            ("SleptInHeat", RuleKind.Build, StrainSource.BadSleep, 2f, 0),
            ("SleepDisturbed", RuleKind.Build, StrainSource.BadSleep, 1.5f, 0),
            ("SleptOutside", RuleKind.Build, StrainSource.BadSleep, 1f, 0),
            ("SleptOnGround", RuleKind.Build, StrainSource.BadSleep, 1f, 0),
            // The council's hard choices.
            ("Council_BrokenPromise", RuleKind.Build, StrainSource.Council, 1.5f, 0),
            ("Council_Decree_Again", RuleKind.Build, StrainSource.Council, 1f, 0),
            ("Council_Secret_Exposed", RuleKind.Build, StrainSource.Council, 3f, 0),
            ("Council_Dlm*", RuleKind.Build, StrainSource.Council, 1f, 1),
            ("Council_Secret2_*", RuleKind.Build, StrainSource.Council, 1f, 1),
            // Gatherings and celebrations.
            ("AttendedParty", RuleKind.Gathering, 0, 6f, 0),
            ("AttendedConcert", RuleKind.Gathering, 0, 5f, 0),
            ("AttendedWedding", RuleKind.Gathering, 0, 5f, 0),
            ("HeldConcert", RuleKind.Gathering, 0, 4f, 0),
            ("GotMarried", RuleKind.Gathering, 0, 6f, 0),
            ("FunParty", RuleKind.Gathering, 0, 5f, 0),
            ("UnforgettableParty", RuleKind.Gathering, 0, 8f, 0),
            ("FunFestival", RuleKind.Gathering, 0, 5f, 0),
            ("UnforgettableFestival", RuleKind.Gathering, 0, 8f, 0),
            ("GoodDuel", RuleKind.Gathering, 0, 2f, 0),
            ("UnforgettableDuel", RuleKind.Gathering, 0, 4f, 0),
            ("HeartwarmingFuneral", RuleKind.Gathering, 0, 4f, 0),
            ("GoodFuneral", RuleKind.Gathering, 0, 3f, 0),
            ("InspirationalSpeech", RuleKind.Gathering, 0, 4f, 0),
            ("EncouragingSpeech", RuleKind.Gathering, 0, 3f, 0),
            ("BeautifulSkyLanterns", RuleKind.Gathering, 0, 4f, 0),
            ("UnforgettableSkyLanterns", RuleKind.Gathering, 0, 6f, 0),
            ("Council_Victory", RuleKind.Gathering, 0, 3f, 0),
            ("Council_AmbitionAchieved", RuleKind.Gathering, 0, 5f, 0),
            ("Council_MyAmbitionAchieved", RuleKind.Gathering, 0, 6f, 0),
            ("Council_Asp_Fulfilled", RuleKind.Gathering, 0, 6f, 0),
            ("Council_TraditionKept", RuleKind.Gathering, 0, 4f, 0),
            ("Council_Dlm_Feast", RuleKind.Gathering, 0, 5f, 0),
            ("Council_Dlm_SmallFeast", RuleKind.Gathering, 0, 3f, 0),
            ("Council_Dlm_Bonfire", RuleKind.Gathering, 0, 4f, 0),
            ("Council_Dlm_HarvestHome", RuleKind.Gathering, 0, 4f, 0),
            ("Council_Dlm_VictoryFeast", RuleKind.Gathering, 0, 5f, 0),
            ("Council_Evt_FoundingFeast", RuleKind.Gathering, 0, 5f, 0),
            ("Council_Evt_Music", RuleKind.Gathering, 0, 3f, 0),
            ("Council_Evt_HeardTales", RuleKind.Gathering, 0, 3f, 0),
            ("Council_Granted_Party", RuleKind.Gathering, 0, 5f, 0),
            ("Council_Granted_Music", RuleKind.Gathering, 0, 3f, 0),
            ("Council_Granted_Wedding", RuleKind.Gathering, 0, 5f, 0),
            ("Council_Story_Wedding", RuleKind.Gathering, 0, 4f, 0),
            ("Council_HomeAgain", RuleKind.Gathering, 0, 3f, 0),
            ("Council_GaveEulogy", RuleKind.Gathering, 0, 2f, 0),
            ("Council_EulogyHeard", RuleKind.Gathering, 0, 2f, 0),
            // Deep talks and good moments with friends (once each).
            ("DeepTalk", RuleKind.Friends, 0, 2f, 0),
            ("KindWordsMood", RuleKind.Friends, 0, 1.5f, 0),
            ("Counselled", RuleKind.Friends, 0, 4f, 0),
            ("Counselled_MoodBoost", RuleKind.Friends, 0, 4f, 0),
            ("Council_Soc_GoodTalk", RuleKind.Friends, 0, 2f, 0),
            ("Council_Soc_Laughed", RuleKind.Friends, 0, 1.5f, 0),
            ("Council_Soc_HadMyBack", RuleKind.Friends, 0, 2f, 0),
            ("Council_Soc2_SatWithMe", RuleKind.Friends, 0, 3f, 0),
            ("Council_Soc2_UnexpectedKindness", RuleKind.Friends, 0, 2f, 0),
            ("Council_Soc2_Unburdened", RuleKind.Friends, 0, 3f, 0),
            ("Council_Secret_Unburdened", RuleKind.Friends, 0, 3f, 0),
            ("Council_Moment_*", RuleKind.Friends, 0, 1.5f, -1),
            // Letting it out.
            ("Catharsis", RuleKind.Catharsis, 0, 30f, 0),
            ("VoidCatharsis", RuleKind.Catharsis, 0, 15f, 0),
            ("HadCatharticFight", RuleKind.Catharsis, 0, 8f, 0)
        };

        internal const float GatheringCap = 12f;
        internal const float FriendsCap = 6f;
        /// <summary>The same kind of bad night counts again only this long after the last (once a night).</summary>
        private const int NightTicks = GenDate.TicksPerHour * 14;

        private static readonly Dictionary<ThoughtDef, MemoryRule?> ruleCache = new Dictionary<ThoughtDef, MemoryRule?>();

        private static MemoryRule? RuleFor(ThoughtDef def)
        {
            if (ruleCache.TryGetValue(def, out var r)) return r;
            r = null;
            string name = def.defName;
            // Exact names first, so a feast isn't taken for the pattern that covers the council's hard choices.
            for (int pass = 0; pass < 2 && r == null; pass++)
                foreach (var rule in Rules)
                {
                    string pat = rule.pattern;
                    int star = pat.IndexOf('*');
                    if ((star >= 0) != (pass == 1)) continue;
                    bool match = star < 0 ? name == pat
                        : name.Length >= pat.Length - 1 && name.StartsWith(pat.Substring(0, star), StringComparison.Ordinal) && name.EndsWith(pat.Substring(star + 1), StringComparison.Ordinal);
                    if (!match) continue;
                    r = new MemoryRule { kind = rule.kind, source = rule.source, amount = rule.amount, only = rule.only };
                    break;
                }
            ruleCache[def] = r;
            return r;
        }

        /// <summary>The memories each colonist has already been counted for, and when each was born (this session).</summary>
        private static readonly Dictionary<Pawn, Dictionary<Thought_Memory, int>> counted = new Dictionary<Pawn, Dictionary<Thought_Memory, int>>();
        private static readonly List<Thought_Memory> staleTmp = new List<Thought_Memory>();

        public static void ResetSession() => counted.Clear();

        /// <summary>
        /// Counts each memory once, when it's new (born since the last look), and again only when the game renews it (a
        /// second cold night). Returns the relief; strain built goes through <paramref name="build"/>.
        /// </summary>
        private static float Memories(Pawn p, PawnHeart h, int since, int now, Func<StrainSource, float, float> build, float reliefTraits)
        {
            var list = p.needs?.mood?.thoughts?.memories?.Memories;
            if (list == null) return 0f;
            if (!counted.TryGetValue(p, out var seen))
            {
                if (counted.Count > 200) counted.Clear();
                seen = new Dictionary<Thought_Memory, int>();
                counted[p] = seen;
            }
            float relief = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                var mem = list[i];
                if (mem?.def == null) continue;
                int born = now - mem.age;
                if (born <= since - 150) continue;
                if (seen.TryGetValue(mem, out int was) && Math.Abs(was - born) < 600) continue;
                seen[mem] = born;
                var rule = RuleFor(mem.def);
                if (rule == null) continue;
                var r = rule.Value;
                if (r.only != 0)
                {
                    float off = FeltMood(mem);
                    if (r.only > 0 && off >= 0f) continue;
                    if (r.only < 0 && off <= 0f) continue;
                }
                switch (r.kind)
                {
                    case RuleKind.Build:
                        // A bad night counts once: waking twice, or being disturbed again and again, renews the memory.
                        if (r.source == StrainSource.BadSleep)
                        {
                            h.badNights ??= new Dictionary<ThoughtDef, int>();
                            if (h.badNights.TryGetValue(mem.def, out int lastNight) && born >= lastNight && born - lastNight < NightTicks) break;
                            h.badNights[mem.def] = born;
                        }
                        build(r.source, r.amount);
                        break;
                    case RuleKind.Gathering:
                    {
                        float amount = Mathf.Min(r.amount * reliefTraits, Mathf.Max(0f, GatheringCap - h.todayGathering));
                        h.todayGathering += amount;
                        h.recentRelief[(int)StrainRelief.Gathering] += amount;
                        relief += amount;
                        break;
                    }
                    case RuleKind.Friends:
                    {
                        float amount = Mathf.Min(r.amount * reliefTraits, Mathf.Max(0f, FriendsCap - h.todayFriends));
                        h.todayFriends += amount;
                        h.recentRelief[(int)StrainRelief.Friends] += amount;
                        relief += amount;
                        break;
                    }
                    case RuleKind.Catharsis:
                        h.recentRelief[(int)StrainRelief.Catharsis] += r.amount;
                        relief += r.amount;
                        break;
                }
            }
            // Forget memories that are gone.
            if (seen.Count > list.Count + 8)
            {
                staleTmp.Clear();
                foreach (var k in seen.Keys)
                    if (!list.Contains(k)) staleTmp.Add(k);
                foreach (var k in staleTmp) seen.Remove(k);
                staleTmp.Clear();
            }
            return relief;
        }

        /// <summary>A memory's mood, or 0 if another mod's thought throws.</summary>
        internal static float SafeMood(Thought_Memory m)
        {
            try
            {
                return m.MoodOffset();
            }
            catch
            {
                return 0f;
            }
        }

        /// <summary>
        /// A memory as the heart feels it: its mood, or for a small moment that gives something else instead of mood,
        /// how big a good moment it was (RewardInstead.felt).
        /// </summary>
        internal static float FeltMood(Thought_Memory m)
        {
            float mood = SafeMood(m);
            return mood != 0f ? mood : RewardInstead.Felt(m.def);
        }

        /// <summary>Strain changed from outside (a dilemma, an episode ending): kept within bounds and on the record.</summary>
        public static void Change(Pawn p, float amount, StrainRelief reliefKind = StrainRelief.Council, StrainSource buildSource = StrainSource.Council)
        {
            var h = Heart.Of(p) ?? (Heart.On ? HeartTracker.Instance?.For(p, true) : null);
            if (h == null || amount == 0f) return;
            h.EnsureLists();
            float before = h.strain;
            h.strain = HeartMath.Clamp(h.strain + amount);
            float moved = h.strain - before;
            if (moved > 0f) h.recentBuild[(int)buildSource] += moved;
            else if (moved < 0f) h.recentRelief[(int)reliefKind] += -moved;
            HeartView.Refresh(p, h);
        }
    }
}
