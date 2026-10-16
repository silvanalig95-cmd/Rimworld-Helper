using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// How Rimfolk's systems talk to each other: the offices doing their jobs in the colony's life, the heart shaping
    /// choices, personal stories hearing what happens around them, the world's memory reaching camps and strangers,
    /// setbacks turning into wishes and stories, health meeting the colony's gatherings, traditions and the colony's
    /// ways, secrets at the council table, quirks in social life, promises in the elections, and the evening news
    /// telling a colonist's day.
    /// <para>
    /// Every link is small and rides on a pass or a hook that was already there: the council's day and hour, a
    /// gathering ending, a spat, a day kept, a deed settled. Nothing here ticks of its own, and nothing it keeps is
    /// saved (cooldowns and counts that a load forgets cost nothing but a second chance). Each link answers to the
    /// "systems talk to each other" setting and to the settings of the systems it joins: with either off, it does
    /// nothing. Effects stay small and prefer what isn't mood: strain eased, a bond, trust, a story moved on, a wish
    /// more likely, a recovery a little shorter.
    /// </para>
    /// See DESIGN.md ("How the systems talk to each other") and the Almanac page "How it fits together".
    /// </summary>
    public static partial class Interplay
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The setting is on and a game is running.</summary>
        public static bool On => S != null && S.interplay && Current.Game != null;

        private static CouncilGameComponent Comp => CouncilGameComponent.Instance;

        private static int Now => Find.TickManager.TicksGame;

        private const int Day = GenDate.TicksPerDay;

        private static readonly Color SpeechColor = new Color(0.85f, 0.95f, 0.8f);

        // ---------------- The passes it rides on ----------------

        /// <summary>The council's day, second half (CouncilDynamics.DailySecond).</summary>
        public static void Daily(CouncilGameComponent comp)
        {
            if (!On || comp == null) return;
            Safe("interplay: physician's rounds", () => PhysicianRounds(comp));
            Safe("interplay: herdmaster", () => HerdmasterDay(comp));
            Safe("interplay: chaplain", () => ChaplainDay(comp));
            Safe("interplay: warden of guests", () => WardenDay(comp));
            Safe("interplay: elders", () => EldersDay(comp));
            Safe("interplay: setbacks counted", () => CountSetbacks());
            PruneCooldowns();
        }

        /// <summary>The council's hour, the people's part (CouncilGameComponent.HourlyPeople), after the pets.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            if (!On || comp == null) return;
            Safe("interplay: pets and grief", () => PetsComfort(comp));
            Safe("interplay: marshal", () => MarshalWatch(comp));
            Safe("interplay: fear faced", () => FearFacedWatch(comp));
        }

        internal static void Safe(string context, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(context, e);
            }
        }

        // ---------------- Who ----------------

        /// <summary>One of the colony's own people with an inner life, alive.</summary>
        internal static bool Ours(Pawn p) => p != null && !p.Dead && p.IsColonist && !p.IsQuestLodger() && People.HasInnerLife(p);

        /// <summary>
        /// Grieving: with the heart on and worked out, a grief that stands out (a third of full strength or more);
        /// otherwise a fresh loss among their memories, as the flavour lines read it.
        /// </summary>
        public static bool Grieving(Pawn p)
        {
            if (p == null) return false;
            var h = Heart.Of(p);
            if (h != null && h.evaluated)
            {
                var grief = ArcUtil.Feeling(p, "Grief");
                return grief != null && grief.Share >= 0.33f;
            }
            return Flavor.IsGrieving(p);
        }

        /// <summary>The holder of an office, while offices are on; null otherwise.</summary>
        internal static Pawn Officer(string defName) => S.offices ? Comp?.offices.HolderOf(defName) : null;

        /// <summary>Up, about, and able to step in: spawned, awake, not downed, drafted or in a mental state.</summary>
        internal static bool Able(Pawn p) => p != null && p.Spawned && !p.Downed && !p.Drafted && !p.InMentalState && p.Awake();

        /// <summary>The same room, or a few steps apart outdoors.</summary>
        internal static bool Near(Pawn a, Pawn b, float cells = 12f)
        {
            if (a == null || b == null || !a.Spawned || !b.Spawned || a.Map != b.Map) return false;
            if (a.Position.InHorDistOf(b.Position, cells)) return true;
            var room = a.GetRoom();
            return room != null && !room.PsychologicallyOutdoors && room == b.GetRoom();
        }

        /// <summary>
        /// Strain eased by company (a gathering, a friend, the herdmaster, the marshal), counted in the heart's own daily
        /// cap for that kind of relief, beside what the memories of the day already eased. The count is saved with the
        /// heart, so links that fire again after a reload, or several in one day, never ease more than the cap.
        /// </summary>
        internal static void Ease(Pawn p, float amount, StrainRelief kind)
        {
            var h = Heart.Of(p);
            if (h == null || amount <= 0f || h.strain <= 0f) return;
            bool gathering = kind == StrainRelief.Gathering;
            float used = gathering ? h.todayGathering : h.todayFriends;
            float cap = gathering ? HeartLogic.GatheringCap : HeartLogic.FriendsCap;
            float eased = Mathf.Min(Mathf.Min(amount, h.strain), Mathf.Max(0f, cap - used));
            if (eased <= 0f) return;
            if (gathering) h.todayGathering += eased;
            else h.todayFriends += eased;
            HeartLogic.Change(p, -eased, kind);
        }

        // ---------------- Cooldowns (not saved: a load only gives a second chance) ----------------

        private static readonly Dictionary<string, int> cooldowns = new Dictionary<string, int>();

        /// <summary>True (and starts the wait) when this key hasn't fired within <paramref name="days"/>.</summary>
        internal static bool Ready(string key, float days)
        {
            int now = Now;
            if (cooldowns.TryGetValue(key, out int t) && t <= now && now - t < days * Day) return false;
            cooldowns[key] = now;
            return true;
        }

        private static void PruneCooldowns()
        {
            if (cooldowns.Count < 200) return;
            int now = Now;
            var old = new List<string>();
            foreach (var kv in cooldowns)
                if (kv.Value > now || now - kv.Value > Day * 30)
                    old.Add(kv.Key);
            foreach (var k in old) cooldowns.Remove(k);
        }

        /// <summary>A new game or a load: forget what the last one was counting.</summary>
        public static void Reset()
        {
            cooldowns.Clear();
            setbackCounts.Clear();
            minded.Clear();
            meetingsHinted.Clear();
            battleStart = -1;
        }

        // ---------------- Lines ----------------

        /// <summary>A line from a pool in this colonist's voice, formatted with them as PAWN and the rest; null if none fits.</summary>
        internal static string Line(string pool, Pawn p, string[] tags, params NamedArgument[] args)
        {
            try
            {
                string line = Flavor.FromPool(pool, p, null, null, tags ?? new string[0]);
                if (line.NullOrEmpty()) return null;
                var all = new List<NamedArgument> { p.Named("PAWN") };
                if (args != null) all.AddRange(args);
                return line.Formatted(all.ToArray()).Resolve();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("interplay line " + pool, e);
                return null;
            }
        }

        /// <summary>A journal line from a pool, with tags.</summary>
        internal static void Journal(Pawn p, string pool, string[] tags, params NamedArgument[] args)
        {
            if (p == null || !S.journals) return;
            Comp?.journals.WriteTagged(p, pool, tags, null, args);
        }
    }
}
