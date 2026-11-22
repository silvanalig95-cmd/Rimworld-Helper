using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The events record (the player, 20 November 2026: "now we have also built so much stuff, and I always wonder, does it
    /// even work correctly? And I feel like I have fewer events than I did in the past"): what came to the colony in the last
    /// 30 days by kind, and what Rimfolk held back and why where it knows. It keeps one short line per event as it comes (the
    /// storyteller's threats and everyday events, Rimfolk's own events, the council's questions, the gods' cards as the
    /// colony saw them) and one a day for each reason something was held back; the quests are read from the game's own
    /// quest list when shown, so they cost the save nothing. EventRecordMath has the rules; Council → Colony → Overview
    /// shows it (UI/EventRecordPanel.cs). Its hooks switch off with the deeper game hooks, as the rest do.
    /// </summary>
    public class EventRecord : GameComponent
    {
        /// <summary>"tick|kind|what", oldest first, the last 30 days and at most EventRecordMath.MaxLines.</summary>
        private List<string> lines = new List<string>();

        private static EventRecord cached;
        private static Game cachedGame;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public static EventRecord Instance
        {
            get
            {
                var game = Current.Game;
                if (game == null) return null;
                if (cachedGame != game || cached == null)
                {
                    cached = game.GetComponent<EventRecord>();
                    cachedGame = game;
                }
                return cached;
            }
        }

        public EventRecord(Game game)
        {
        }

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>Something came: kept with its name as the colony saw it.</summary>
        public static void Note(EventKind kind, string what)
        {
            var r = Instance;
            if (r == null || kind == EventKind.Held || kind == EventKind.QuestHome || kind == EventKind.QuestAway) return;
            int now = Now;
            r.lines.Add(EventRecordMath.Line(now, kind, what));
            EventRecordMath.Prune(r.lines, now);
            EventRecordPanel.Dirty();
        }

        /// <summary>Something was held back for this reason (EventRecordMath.HeldWait...): once a day for each reason.</summary>
        public static void Held(string reason)
        {
            var r = Instance;
            if (r == null || reason.NullOrEmpty()) return;
            int now = Now;
            if (EventRecordMath.HeldToday(r.lines, reason, now)) return;
            r.lines.Add(EventRecordMath.Line(now, EventKind.Held, reason));
            EventRecordMath.Prune(r.lines, now);
            EventRecordPanel.Dirty();
        }

        /// <summary>The storyteller fired an incident: which kind it is in the record (quests are read from the quest list instead).</summary>
        public static void Fired(FiringIncident fi)
        {
            var def = fi?.def;
            if (def == null) return;
            var map = fi.parms?.target as Map;
            if (def.HasModExtension<HappeningExtension>())
            {
                Note(EventKind.Rimfolk, def.LabelCap);
                return;
            }
            var cat = def.category;
            if (cat == IncidentCategoryDefOf.GiveQuest) return;
            bool threat = cat == IncidentCategoryDefOf.ThreatBig || cat == IncidentCategoryDefOf.ThreatSmall;
            Note(threat ? EventKind.Threat : EventKind.Everyday, def.LabelCap);
            // The storyteller drew an everyday event while a question of Rimfolk's waited at this home: none of Rimfolk's
            // events could be drawn then (IncidentWorker_Happening.ChanceFactorNow).
            if (!threat && fi.source != null && cat == IncidentCategoryDefOf.Misc && S != null && S.worldEvents && S.eventsFromStoryteller
                && map != null && HappeningTracker.WaitingAt(map))
                Held(EventRecordMath.HeldDrawWait);
        }

        /// <summary>The record's lines read back, oldest first.</summary>
        public IEnumerable<(int tick, EventKind kind, string what)> Entries()
        {
            foreach (var l in lines)
                if (EventRecordMath.TryParse(l, out int t, out var k, out var w))
                    yield return (t, k, w);
        }

        /// <summary>The quests the game offered or started in the last 30 days, at home or out on the world map (not Rimfolk's own, counted as its events).</summary>
        public static IEnumerable<(int tick, EventKind kind, string what)> Quests(int now)
        {
            var list = Find.QuestManager?.QuestsListForReading;
            if (list == null) yield break;
            foreach (var q in list)
            {
                if (q?.root == null || q.hidden || !EventRecordMath.InWindow(q.appearanceTick, now)) continue;
                if (string.Equals(q.root.modContentPack?.PackageId, ColonyCouncilMod.PackageId, StringComparison.OrdinalIgnoreCase)) continue;
                yield return (q.appearanceTick, QuestPlaces.IsAway(q) ? EventKind.QuestAway : EventKind.QuestHome, q.name);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref lines, "lines", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lines == null) lines = new List<string>();
                lines.RemoveAll(l => l == null);
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            cached = this;
            cachedGame = Current.Game;
            EventRecordPanel.Dirty();
        }
    }

    /// <summary>Every incident the storyteller fires (its own picks, its queue, Rimfolk's own timer), for the events record.</summary>
    [HarmonyPatch(typeof(Storyteller), nameof(Storyteller.TryFire))]
    public static class Patch_EventRecordFired
    {
        public static void Postfix(FiringIncident fi, bool __result)
        {
            if (!__result || fi?.def == null || !RimfolkPatches.On) return;
            try
            {
                EventRecord.Fired(fi);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("events record: an event that came", e);
            }
        }
    }

    /// <summary>The council's questions and the gods' cards as they arrive, for the events record.</summary>
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class Patch_EventRecordLetters
    {
        public static void Postfix(Letter let)
        {
            if (let == null || !RimfolkPatches.On) return;
            try
            {
                if (let is ChoiceLetter_Dilemma d) EventRecord.Note(EventKind.Question, d.Label);
                else if (let is GodLetter g) EventRecord.Note(EventKind.GodCard, g.Label);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("events record: a letter", e);
            }
        }
    }
}
