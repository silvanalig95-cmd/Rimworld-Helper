using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A world event that will follow from one that ended (docs/design/stage1-region.md, item 7): what it follows and how
    /// that ended, its people, its own person (the warlord, the steading's leader) with their name kept for when the game
    /// lets them go, the colonist it remembers, a thing of it (the warlord's weapon, the steading's town), the flags it
    /// ended with, and when it is due.
    /// </summary>
    public class FollowUp : IExposable
    {
        /// <summary>"son" (the warlord's son, WarlordSon.cs) or "town" (the town that grew, GrownTown.cs).</summary>
        public string kind;
        public Faction people;
        /// <summary>The second people of what came before (the warlord's hunters), or null.</summary>
        public Faction other;
        /// <summary>How what came before ended (its ending key; "hamlet" for a hamlet's second year).</summary>
        public string ending;
        public int endTick = -1, due = -1;
        /// <summary>Its own person: the warlord, the steading's leader. Kept by the ledger while it waits (FollowUps.KeepReason); the name for when the game let them go.</summary>
        public Pawn who;
        public string whoName, whoLast;
        public Gender whoGender;
        public float whoAge = -1f;
        /// <summary>The colonist it remembers: who killed the warlord, who dealt with the settlers in their first year.</summary>
        public Pawn by;
        public string byName;
        /// <summary>The warlord's weapon (its thing id) or the steading's town (its world object id), or -1.</summary>
        public int thing = -1;
        /// <summary>What came before, as the texts name it ({TALE}): the warband, the steading.</summary>
        public string tale;
        public List<string> flags = new List<string>();

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (who != null && who.Discarded) who = null;
                if (by != null && by.Discarded) by = null;
            }
            Scribe_Values.Look(ref kind, "kind");
            Scribe_References.Look(ref people, "people");
            Scribe_References.Look(ref other, "other");
            Scribe_Values.Look(ref ending, "ending");
            Scribe_Values.Look(ref endTick, "endTick", -1);
            Scribe_Values.Look(ref due, "due", -1);
            // A dead warlord in a grave still saves as himself (as the world events' own person does).
            Scribe_References.Look(ref who, "who", true);
            Scribe_Values.Look(ref whoName, "whoName");
            Scribe_Values.Look(ref whoLast, "whoLast");
            Scribe_Values.Look(ref whoGender, "whoGender");
            Scribe_Values.Look(ref whoAge, "whoAge", -1f);
            Scribe_References.Look(ref by, "by");
            Scribe_Values.Look(ref byName, "byName");
            Scribe_Values.Look(ref thing, "thing", -1);
            Scribe_Values.Look(ref tale, "tale");
            Scribe_Collections.Look(ref flags, "flags", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) flags ??= new List<string>();
        }
    }

    /// <summary>One home's follow-ups waiting (kept on its <see cref="RegionHome"/>).</summary>
    public class FollowUpsHome : IExposable
    {
        public List<FollowUp> waiting = new List<FollowUp>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref waiting, "waiting", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                waiting ??= new List<FollowUp>();
                waiting.RemoveAll(f => f == null || f.kind.NullOrEmpty() || f.people == null);
            }
        }
    }

    /// <summary>
    /// The follow-ups of world events (docs/design/stage1-region.md, item 7): noted when what they follow ends (the
    /// warlord's story: WarlordSons.Remember; the other settlers' year: GrownTowns.Remember; a hamlet's second year, by
    /// GrownTowns.Note), kept on the home they belong to, and started in the region's day (Pass.DailyRegion) once due,
    /// when a world event's slot is free there: one a day at most. One that waits too long for a slot is let go.
    /// </summary>
    public static class FollowUps
    {
        /// <summary>Why the ledger keeps a follow-up's own person in the world while it waits.</summary>
        public const string KeepReason = "followup";

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>A home's follow-ups (made when it has none); null for a map that isn't a home or without a game.</summary>
        public static FollowUpsHome Of(Map home)
        {
            if (home == null || !Homes.IsHome(home)) return null;
            var rh = RegionBook.Instance?.HomeOf(home);
            if (rh == null) return null;
            return rh.followUps ??= new FollowUpsHome();
        }

        /// <summary>The home a world event happened for: its own map while it's a home, else the seat.</summary>
        public static Map HomeOf(Map map) => map != null && Homes.IsHome(map) ? map : Homes.Seat;

        /// <summary>A follow-up to come at a home; its own person kept in the world meanwhile.</summary>
        public static void Add(Map home, FollowUp f)
        {
            var book = Of(home);
            if (book == null || f == null || f.people == null) return;
            book.waiting.Add(f);
            while (book.waiting.Count > FollowUpsMath.MaxWaiting)
            {
                var old = book.waiting[0];
                book.waiting.RemoveAt(0);
                Release(old);
            }
            var p = f.who;
            if (p != null && !p.Discarded) WorldLedger.Keep(p, KeepReason, !p.Dead && WorldSituationWorker.FreeInWorld(p));
        }

        /// <summary>Some follow-up waiting still needs this person (the ledger's daily check of its reasons).</summary>
        public static bool Holds(Pawn p)
        {
            if (p == null || RegionBook.Instance == null) return false;
            foreach (var home in Homes.All)
            {
                var book = Of(home);
                if (book != null && book.waiting.Any(f => f != null && f.who == p)) return true;
            }
            return false;
        }

        /// <summary>The follow-up no longer waits: its person is no longer kept for it.</summary>
        private static void Release(FollowUp f)
        {
            var p = f?.who;
            if (p == null || p.Discarded) return;
            try
            {
                // Taken by the world event that came of it (it holds them now), or let go to the world's own keeping.
                if (WorldLedger.Release(p, KeepReason) && !p.Dead && !p.Spawned && Find.WorldPawns != null && Find.WorldPawns.Contains(p) && !PawnUtility.IsFactionLeader(p))
                    Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("follow-ups: a person let go", e);
            }
        }

        /// <summary>
        /// The region's day: each home's follow-ups let go when their people are gone or they waited too long, and the
        /// first one due started when a world event's slot is free (one a day over every home).
        /// </summary>
        internal static void Daily(RegionBook book, int now)
        {
            if (book == null) return;
            // What the peoples remember of single colonists fades with the days (World/ColonistMemory.cs).
            ColonistMemory.Daily(now);
            if (!(ColonyCouncilMod.Settings?.worldSituations ?? true)) return;
            bool started = false;
            foreach (var home in Homes.All.ToList())
            {
                var mine = Of(home);
                if (mine == null || mine.waiting.Count == 0) continue;
                foreach (var f in mine.waiting.ToList())
                {
                    try
                    {
                        // Their people gone, the town that was to grow gone from the map, or waited too long: let go.
                        if (f.people == null || f.people.defeated || FollowUpsMath.Lapsed(f.endTick, now) || f.kind == "town" && GrownTowns.TownOf(f) == null)
                        {
                            mine.waiting.Remove(f);
                            Release(f);
                            continue;
                        }
                        if (started || !FollowUpsMath.Due(f.due, now)) continue;
                        var ws = WorldSituations.Instance;
                        if (ws == null || !ws.SlotFree(home)) continue;
                        WorldSituation s = null;
                        if (f.kind == "son") s = WarlordSons.Start(home, f);
                        else if (f.kind == "town") s = GrownTowns.Start(home, f);
                        if (s == null) continue;
                        started = true;
                        mine.waiting.Remove(f);
                        Release(f);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("follow-ups: the day", e);
                        mine.waiting.Remove(f);
                        // Its person is let go too, so a follow-up that failed doesn't keep them in the world for good.
                        try
                        {
                            Release(f);
                        }
                        catch (Exception e2)
                        {
                            CouncilLog.ErrorOnce("follow-ups: letting go after a failed day", e2);
                        }
                    }
                }
            }
        }

        /// <summary>The flags a follow-up's texts read what came before by ("afterEnd:slain", "first:fed").</summary>
        public static void TakeFlags(WorldSituation s, FollowUp f, bool firstYear)
        {
            if (s == null || f == null) return;
            if (!f.ending.NullOrEmpty()) s.flags.AddDistinct("afterEnd:" + f.ending);
            if (!firstYear || f.flags == null) return;
            foreach (var flag in f.flags)
            {
                var t = FollowUpsMath.FirstYear(flag);
                if (t != null) s.flags.AddDistinct(t);
            }
        }
    }
}
