using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Other mods reaching Rimfolk's heart and health, read by name from what they leave on a colonist (their memories
    /// and conditions), once an hour:
    /// <list type="bullet">
    /// <item>Warm water eases strain: a hot bath, a hot shower or the heated pool (Dubs Bad Hygiene), a hot spring
    /// (ReGrowth), the healing spring (Alpha Biomes). Quiet relief, a little each time, a few points a day at most.</item>
    /// <item>A weight shared eases strain: a session on the therapy couch (Therapy), unbottling to a friend (Vanilla Social
    /// Interactions Expanded). Counted with the heart's own talks with friends, under the same daily cap.</item>
    /// <item>Spent magic wears on them: arcane weakness and mana sickness (A RimWorld of Magic) build strain as
    /// exhaustion, more the deeper they go, a few points a day at most.</item>
    /// <item>Comfort for the convalescing: warm water once a day takes half a day off the recovery, and healing magic on
    /// them (regeneration, mending, inner healing) a whole day.</item>
    /// </list>
    /// Each follows its own Rimfolk switch (feelings and strain, convalescence). With a mod missing its names are never
    /// found and its part never runs; with none of them there, nothing here runs at all.
    /// </summary>
    public static class ModLinks
    {
        /// <summary>Memories of warm water, and how they read in the journal ("bath", "spring").</summary>
        private static readonly (string thought, string kind)[] WarmWaterNames =
        {
            ("HotBath", "bath"), ("HotShower", "bath"), ("HeatedPool", "bath"),
            ("RG_HotSpringBathing", "spring"), ("RG_HotSpringWet", "spring"), ("AB_HealingSpringwaterThought", "spring")
        };

        /// <summary>Memories of a weight shared, and how much strain each lifts (before the daily cap for talks with friends).</summary>
        private static readonly (string thought, float relief)[] UnburdenedNames = { ("TherapyRelieved", 5f), ("VSIE_SharingBurden", 3f) };

        /// <summary>Conditions of spent magic: from what severity each builds how much strain an hour.</summary>
        private static readonly (string hediff, float from, float perHour)[] SpentNames =
        {
            ("TM_ArcaneWeakness", 5f, 0.4f), ("TM_ArcaneWeakness", 10f, 0.8f), ("TM_ArcaneWeakness", 20f, 1.2f),
            ("TM_ManaSickness", 0.1f, 0.3f), ("TM_ManaSickness", 4f, 0.6f)
        };

        /// <summary>Healing magic on someone (regeneration, a mending spell, inner healing).</summary>
        private static readonly string[] MendingNames =
            { "TM_Regeneration", "TM_Regeneration_I", "TM_Regeneration_II", "TM_Regeneration_III", "SpellMendingHD", "TM_HediffInnerHealing" };

        /// <summary>Strain warm water may lift in one day, and in one go.</summary>
        public const float WaterPerDay = 4f;
        public const float WaterEach = 2f;
        /// <summary>The heart's own cap on what talks with friends lift in a day (HeartLogic).</summary>
        public const float FriendsPerDay = 6f;
        /// <summary>Strain spent magic may build in one day.</summary>
        public const float SpentPerDay = 6f;
        /// <summary>What a warm bath or a healing spell takes off a convalescence (a day out of bed is 0.06).</summary>
        public const float WarmWaterComfort = 0.03f;
        public const float MendingComfort = 0.06f;

        private static bool resolved;
        private static Dictionary<ThoughtDef, string> warmWater;
        private static Dictionary<ThoughtDef, float> unburdened;
        private static List<(HediffDef def, float from, float perHour)> spent;
        private static HashSet<HediffDef> mending;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            warmWater = new Dictionary<ThoughtDef, string>();
            unburdened = new Dictionary<ThoughtDef, float>();
            spent = new List<(HediffDef, float, float)>();
            mending = new HashSet<HediffDef>();
            foreach (var (name, kind) in WarmWaterNames)
            {
                var d = DefDatabase<ThoughtDef>.GetNamedSilentFail(name);
                if (d != null) warmWater[d] = kind;
            }
            foreach (var (name, relief) in UnburdenedNames)
            {
                var d = DefDatabase<ThoughtDef>.GetNamedSilentFail(name);
                if (d != null) unburdened[d] = relief;
            }
            foreach (var (name, from, perHour) in SpentNames)
            {
                var d = DefDatabase<HediffDef>.GetNamedSilentFail(name);
                if (d != null) spent.Add((d, from, perHour));
            }
            foreach (var name in MendingNames)
            {
                var d = DefDatabase<HediffDef>.GetNamedSilentFail(name);
                if (d != null) mending.Add(d);
            }
        }

        /// <summary>Whether any of the mods these links read is here.</summary>
        public static bool Any
        {
            get
            {
                Resolve();
                return warmWater.Count > 0 || unburdened.Count > 0 || spent.Count > 0 || mending.Count > 0;
            }
        }

        /// <summary>What the links read in this game, for the self-check: "warm water, magic".</summary>
        public static string Found
        {
            get
            {
                Resolve();
                var parts = new List<string>();
                if (warmWater.Count > 0) parts.Add("warm water (" + warmWater.Count + ")");
                if (unburdened.Count > 0) parts.Add("talks (" + unburdened.Count + ")");
                if (spent.Count > 0) parts.Add("spent magic");
                if (mending.Count > 0) parts.Add("healing magic (" + mending.Count + ")");
                return string.Join(", ", parts);
            }
        }

        // ---------------- One colonist's hour ----------------

        /// <summary>What this hour brought: warm water (and which kind), a weight shared (the most it lifts).</summary>
        public struct Hour
        {
            public string water;
            public float unburdened;
        }

        /// <summary>Memories made (or renewed) within the last hour: each is seen by exactly one hourly look.</summary>
        public static Hour Look(Pawn p)
        {
            Resolve();
            var hour = new Hour();
            if (warmWater.Count == 0 && unburdened.Count == 0) return hour;
            var mems = p.needs?.mood?.thoughts?.memories?.Memories;
            if (mems == null) return hour;
            for (int i = 0; i < mems.Count; i++)
            {
                var m = mems[i];
                if (m?.def == null || m.age >= GenDate.TicksPerHour) continue;
                if (warmWater.TryGetValue(m.def, out string kind)) hour.water ??= kind;
                else if (unburdened.TryGetValue(m.def, out float relief)) hour.unburdened = Mathf.Max(hour.unburdened, relief);
            }
            return hour;
        }

        /// <summary>Strain spent magic builds this hour (before the daily cap), from the deepest stage of each condition.</summary>
        public static float SpentPerHour(Pawn p)
        {
            Resolve();
            if (spent.Count == 0 || p.health?.hediffSet == null) return 0f;
            float total = 0f;
            HediffDef last = null;
            float best = 0f;
            foreach (var (def, from, perHour) in spent)
            {
                if (def != last)
                {
                    total += best;
                    best = 0f;
                    last = def;
                }
                var h = p.health.hediffSet.GetFirstHediffOfDef(def);
                if (h != null && h.Severity >= from) best = Mathf.Max(best, perHour);
            }
            return total + best;
        }

        /// <summary>Healing magic at work on them now.</summary>
        public static bool Mended(Pawn p)
        {
            Resolve();
            if (mending.Count == 0 || p.health?.hediffSet == null) return false;
            var hs = p.health.hediffSet.hediffs;
            for (int i = 0; i < hs.Count; i++)
                if (hs[i]?.def != null && mending.Contains(hs[i].def)) return true;
            return false;
        }

        public static void Reset()
        {
            resolved = false;
        }
    }

    /// <summary>
    /// Runs <see cref="ModLinks"/> once an hour for the colony's people, and remembers the day each convalescent last had
    /// the comfort of warm water or healing magic (once a day each). Does nothing when none of the mods is here.
    /// </summary>
    public class ModLinksTracker : GameComponent
    {
        /// <summary>Colonist ThingID: the day they last had warm water's comfort, and healing magic's, while convalescing.</summary>
        private Dictionary<string, int> lastWater = new Dictionary<string, int>();
        private Dictionary<string, int> lastMend = new Dictionary<string, int>();
        private List<string> tk1, tk2;
        private List<int> tv1, tv2;

        /// <summary>What each colonist has had from the links today (not saved: a reload forgets at most a few points).</summary>
        private readonly Dictionary<int, (int day, float water, float spent)> today = new Dictionary<int, (int, float, float)>();

        /// <summary>The hour's tick within each hour, away from the round number most of Rimfolk's hourly work uses.</summary>
        private const int Offset = 917;

        public ModLinksTracker(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            ModLinks.Reset();
            today.Clear();
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % GenDate.TicksPerHour != Offset) return;
            try
            {
                if (!ModLinks.Any) return;
                Hourly(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("links with other mods", e);
            }
        }

        private void Hourly(int now)
        {
            bool heart = Heart.On;
            bool recovery = HealthTracker.ConvalescenceOn;
            if (!heart && !recovery) return;
            int day = now / GenDate.TicksPerDay;
            var everyone = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists.ToList();
            foreach (var p in everyone)
            {
                if (!HealthTracker.Ours(p)) continue;
                try
                {
                    var hour = ModLinks.Look(p);
                    if (heart) Strain(p, hour, day);
                    if (recovery) Comfort(p, hour, day);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("links with other mods for a colonist", e);
                }
            }
        }

        private void Strain(Pawn p, ModLinks.Hour hour, int day)
        {
            var h = Heart.Of(p);
            if (h == null) return;
            int key = p.thingIDNumber;
            if (!today.TryGetValue(key, out var t) || t.day != day) t = (day, 0f, 0f);
            if (hour.water != null && h.strain > 0f && t.water < ModLinks.WaterPerDay)
            {
                float amount = Mathf.Min(ModLinks.WaterEach, ModLinks.WaterPerDay - t.water);
                t.water += amount;
                HeartLogic.Change(p, -amount, StrainRelief.Quiet);
            }
            if (hour.unburdened > 0f && h.strain > 0f)
            {
                float amount = Mathf.Min(hour.unburdened, Mathf.Max(0f, ModLinks.FriendsPerDay - h.todayFriends));
                if (amount > 0f)
                {
                    h.todayFriends += amount;
                    HeartLogic.Change(p, -amount, StrainRelief.Friends);
                }
            }
            float built = ModLinks.SpentPerHour(p);
            if (built > 0f && t.spent < ModLinks.SpentPerDay)
            {
                built *= HeartMath.TraitFactor(StrainSource.Exhaustion, Heart.TraitsOf(p));
                built = Mathf.Min(built, ModLinks.SpentPerDay - t.spent);
                if (built > 0f)
                {
                    t.spent += built;
                    HeartLogic.Change(p, built, StrainRelief.Council, StrainSource.Exhaustion);
                }
            }
            if (today.Count > 300) today.Clear();
            today[key] = t;
        }

        /// <summary>A convalescent's comfort: warm water, or healing magic, once a day each.</summary>
        private void Comfort(Pawn p, ModLinks.Hour hour, int day)
        {
            var conv = HealthTracker.ConvalescenceOf(p);
            if (conv == null) return;
            string id = p.ThingID;
            if (hour.water != null && (!lastWater.TryGetValue(id, out int w) || w != day))
            {
                lastWater[id] = day;
                conv.Severity = Mathf.Max(0.01f, conv.Severity - ModLinks.WarmWaterComfort);
                if (People.HasInnerLife(p) && Rand.Chance(0.5f)) Journal(p, "Council_Journal_Links_WarmWater", hour.water);
            }
            if (ModLinks.Mended(p) && (!lastMend.TryGetValue(id, out int m) || m != day))
            {
                lastMend[id] = day;
                conv.Severity = Mathf.Max(0.01f, conv.Severity - ModLinks.MendingComfort);
                if (People.HasInnerLife(p) && Rand.Chance(0.5f)) Journal(p, "Council_Journal_Links_Mended", null);
            }
        }

        /// <summary>A line in their journal; lines may be tagged with the kind of water ([bath], [spring]) and [impatient].</summary>
        private static void Journal(Pawn p, string pool, string tag)
        {
            var journals = CouncilGameComponent.Instance?.journals;
            if (journals == null || p.Faction != Faction.OfPlayer) return;
            var tags = new List<string>();
            if (tag != null) tags.Add(tag);
            if (HealthTracker.Impatient(p)) tags.Add("impatient");
            journals.WriteTagged(p, pool, tags.ToArray(), null);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // Only colonists still convalescing need the day remembered.
                int day = Find.TickManager.TicksGame / GenDate.TicksPerDay;
                lastWater = lastWater.Where(kv => day - kv.Value <= 1).ToDictionary(kv => kv.Key, kv => kv.Value);
                lastMend = lastMend.Where(kv => day - kv.Value <= 1).ToDictionary(kv => kv.Key, kv => kv.Value);
            }
            Scribe_Collections.Look(ref lastWater, "lastWater", LookMode.Value, LookMode.Value, ref tk1, ref tv1);
            Scribe_Collections.Look(ref lastMend, "lastMend", LookMode.Value, LookMode.Value, ref tk2, ref tv2);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lastWater ??= new Dictionary<string, int>();
                lastMend ??= new Dictionary<string, int>();
            }
        }
    }
}
