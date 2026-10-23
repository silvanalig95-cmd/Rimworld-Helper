using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The habits without a free-time moment of their own (<see cref="QuirkHabits"/>) show over a head now and then
    /// instead: a hum at work, a song at the stove, a mumble in the night, a word over the last bite of a meal, a line
    /// checked twice on a building site, a load too big, the small hours kept, a sickbed that won't keep someone still.
    /// </summary>
    /// <remarks>
    /// Once an hour (CouncilGameComponent), with the quirks and small habits settings on and speech showing: a colonist
    /// with one of these habits, in view on the map being looked at and with no danger on it, has about a one in four
    /// chance of a line while the habit fits what they're doing, and says at most one every six hours from this. Lines
    /// come from the <c>Council_Pool_Bubble...</c> pools in their own voice, through <see cref="Speech.Say"/>. Only those
    /// who have a quirk are looked at; nothing is saved.
    /// </remarks>
    public static class HabitBubbles
    {
        private const float Chance = 0.25f;
        private const int RestTicks = GenDate.TicksPerHour * 6;

        private static readonly Color SpeechColor = new Color(0.9f, 0.92f, 0.88f);

        private sealed class Bubble
        {
            public QuirkDef quirk;
            public string pool;
            /// <summary>Whether it fits what they're doing now (the hour of the day given).</summary>
            public Func<Pawn, int, bool> fits;
        }

        private static List<Bubble> bubbles;

        private static List<Bubble> Bubbles
        {
            get
            {
                if (bubbles != null) return bubbles;
                bubbles = new List<Bubble>();
                void Add(string quirk, string pool, Func<Pawn, int, bool> fits)
                {
                    var q = DefDatabase<QuirkDef>.GetNamedSilentFail(quirk);
                    if (q != null) bubbles.Add(new Bubble { quirk = q, pool = pool, fits = fits });
                }
                Add("Council_Quirk_Hums", "Council_Pool_BubbleHums", (p, hour) => p.Awake() && AtWork(p));
                Add("Council_Quirk_SingsAtStove", "Council_Pool_BubbleStove", (p, hour) => p.Awake() && Work(p)?.defName == "Cooking");
                Add("Council_Quirk_SleepTalker", "Council_Pool_BubbleSleepTalk", (p, hour) => !p.Awake() && (hour >= 22 || hour < 6));
                Add("Council_Quirk_BestBiteLast", "Council_Pool_BubbleBestBite", (p, hour) => p.Awake() && p.CurJobDef == JobDefOf.Ingest);
                Add("Council_Quirk_MeasuresTwice", "Council_Pool_BubbleMeasure", (p, hour) => p.Awake() && Work(p) == WorkTypeDefOf.Construction);
                Add("Council_Quirk_StrongBack", "Council_Pool_BubbleHaul", (p, hour) => p.Awake() && Work(p) == WorkTypeDefOf.Hauling);
                Add("Council_Quirk_LateHours", "Council_Pool_BubbleLateHours", (p, hour) => p.Awake() && (hour >= 23 || hour < 5) && (AtWork(p) || p.CurJobDef?.joyKind != null));
                Add("Council_Quirk_Fidgets", "Council_Pool_BubbleFidgets", (p, hour) => p.Awake() && QuirkHabits.StillPatient(p));
                return bubbles;
            }
        }

        private static Bubble Of(QuirkDef def)
        {
            var list = Bubbles;
            for (int i = 0; i < list.Count; i++)
                if (list[i].quirk == def) return list[i];
            return null;
        }

        /// <summary>The kind of work they're at, or null.</summary>
        private static WorkTypeDef Work(Pawn p) => p.CurJob?.workGiverDef?.workType;

        /// <summary>At any work (as the practice log counts it: not putting out fires or lying in a sickbed).</summary>
        private static bool AtWork(Pawn p)
        {
            var wt = Work(p);
            return wt != null && wt.defName != "Firefighter" && wt.defName != "Patient" && wt.defName != "PatientBedRest";
        }

        // ---------------- Who said something lately ----------------

        private static readonly Dictionary<int, int> until = new Dictionary<int, int>();
        private static Game game;

        private static bool Rested(Pawn p, int now)
        {
            if (game != Current.Game)
            {
                game = Current.Game;
                until.Clear();
            }
            return !(until.TryGetValue(p.thingIDNumber, out int t) && t > now);
        }

        private static void Said(Pawn p, int now)
        {
            if (until.Count > 200)
            {
                var gone = new List<int>();
                foreach (var kv in until)
                    if (kv.Value <= now) gone.Add(kv.Key);
                foreach (int k in gone) until.Remove(k);
            }
            until[p.thingIDNumber] = now + RestTicks;
        }

        // ---------------- The hour's look ----------------

        public static void Hourly()
        {
            var s = ColonyCouncilMod.Settings;
            if (s == null || !s.quirks || !s.smallHabits || !Speech.On || Current.Game == null) return;
            var quirks = CouncilGameComponent.Instance?.quirks?.quirks;
            if (quirks == null || quirks.Count == 0) return;
            if (!VisualTouches.InView(out var map, out var view) || map.dangerWatcher.DangerRating != StoryDanger.None) return;
            int now = Find.TickManager.TicksGame;
            int hour = GenLocalDate.HourOfDay(map);
            int count = quirks.Count;
            int start = Rand.Range(0, count);
            for (int i = 0; i < count; i++)
            {
                var q = quirks[(start + i) % count];
                var p = q?.pawn;
                if (q?.def == null || !CanSay(p, map, view) || !Rested(p, now)) continue;
                var b = Of(q.def);
                if (b == null || !b.fits(p, hour) || !Rand.Chance(Chance)) continue;
                string line = Flavor.FromPool(b.pool, p);
                if (line.NullOrEmpty()) continue;
                // apart: said to nobody in particular, over their own head.
                if (Speech.Say(p, line.Formatted(p.Named("PAWN")).Resolve(), null, SpeechColor)) Said(p, now);
            }
        }

        /// <summary>One of the colony's own, in view, able to talk (asleep is fine: the sleep talker), not drafted or in a state.</summary>
        private static bool CanSay(Pawn p, Map map, CellRect view) =>
            p != null && p.Spawned && p.Map == map && view.Contains(p.Position) && !p.Dead && !p.Drafted && !p.InMentalState && p.IsColonist
            && People.HasInnerLife(p) && !p.DevelopmentalStage.Baby() && p.health?.capacities != null && p.health.capacities.CapableOf(PawnCapacityDefOf.Talking);
    }
}
