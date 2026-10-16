using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    [DefOf]
    public static class KeptDefOf
    {
        public static ThoughtDef Council_TakenBack;
        public static ThoughtDef Council_MadeRight;

        static KeptDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(KeptDefOf));
        }
    }

    /// <summary>
    /// One granted lasting wish being watched (or a decree someone asked for): what was given, to whom, until when, and
    /// where it stands. The wish itself is kept, so its worker can still check it.
    /// </summary>
    public class GrantWatch : IExposable
    {
        public int id;
        /// <summary>The granted wish (null for a decree).</summary>
        public Wish wish;
        /// <summary>A decree the colonist asked for and the council declared (null for a wish).</summary>
        public DecreeDef decree;
        /// <summary>Who minds: the one the wish was for, or the one who asked for the decree.</summary>
        public Pawn pawn;
        /// <summary>What was given, as the wish's label ("a bed kept for guests").</summary>
        public string label;
        /// <summary>The map it was given on (its uniqueID).</summary>
        public int mapId = -1;
        public int grantedTick;
        /// <summary>After this, the colony has lived with it, and changing it is just life.</summary>
        public int watchUntil;
        public KeptPhase phase = KeptPhase.Kept;
        public int missingSince = -1;
        public int graceUntil = -1;
        /// <summary>Since when it has only been there as a plan or frame being built (-1: not).</summary>
        public int plannedSince = -1;
        public int takenBackTick = -1;
        /// <summary>The trust it cost when taken back (half comes back if the council makes it right).</summary>
        public float trustLost;
        /// <summary>The "why" of the mood, to find the mood again when it's made right.</summary>
        public string cause;
        /// <summary>When one of the things kept was destroyed by a fire, a raid or a collapse (-1: never).</summary>
        public int misfortuneTick = -1;
        /// <summary>The things that made it hold the last time it did, and where they stood.</summary>
        public List<Thing> things = new List<Thing>();
        public List<IntVec3> cells = new List<IntVec3>();
        [Unsaved] public bool decreeLost;

        public WishDef Def => wish?.def;

        public bool Promised => wish != null && wish.promised;

        /// <summary>room, bed, thing, stock, work, pet or decree: for the journal's lines.</summary>
        public string Kind
        {
            get
            {
                if (decree != null) return "decree";
                try
                {
                    return wish?.def != null ? wish.Worker.KeptKind(wish) : "thing";
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("kept kind " + wish?.def?.defName, e);
                    return "thing";
                }
            }
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // A thing that's gone for good (deconstructed, burned, sold off the map) can't be saved as a reference.
                for (int i = things.Count - 1; i >= 0; i--)
                {
                    var t = things[i];
                    if (t == null || t.Destroyed || t.Discarded) things.RemoveAt(i);
                }
                // Nor can someone the game has let go of: whoever asked on their behalf, a partner, a backer.
                if (wish != null)
                {
                    if (wish.pawn != null && wish.pawn.Discarded)
                    {
                        wish.pawn = wish.beneficiary;
                        wish.beneficiary = null;
                    }
                    if (wish.beneficiary != null && wish.beneficiary.Discarded) wish.beneficiary = null;
                    if (wish.other != null && wish.other.Discarded) wish.other = null;
                    if (wish.envied != null && wish.envied.Discarded) wish.envied = null;
                    if (wish.objector != null && wish.objector.Discarded) wish.objector = null;
                    wish.coSigners?.RemoveAll(p => p == null || p.Discarded);
                }
            }
            Scribe_Values.Look(ref id, "id");
            Scribe_Deep.Look(ref wish, "wish");
            CouncilScribe.LookDef(ref decree, "decree", ref decreeLost);
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Values.Look(ref grantedTick, "grantedTick");
            Scribe_Values.Look(ref watchUntil, "watchUntil");
            Scribe_Values.Look(ref phase, "phase", KeptPhase.Kept);
            Scribe_Values.Look(ref missingSince, "missingSince", -1);
            Scribe_Values.Look(ref graceUntil, "graceUntil", -1);
            Scribe_Values.Look(ref plannedSince, "plannedSince", -1);
            Scribe_Values.Look(ref takenBackTick, "takenBackTick", -1);
            Scribe_Values.Look(ref trustLost, "trustLost");
            Scribe_Values.Look(ref cause, "cause");
            Scribe_Values.Look(ref misfortuneTick, "misfortuneTick", -1);
            Scribe_Collections.Look(ref things, "things", LookMode.Reference);
            Scribe_Collections.Look(ref cells, "cells", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (things == null) things = new List<Thing>();
                things.RemoveAll(t => t == null);
                if (cells == null) cells = new List<IntVec3>();
            }
        }
    }

    /// <summary>
    /// Promises kept. A lasting wish (a bed kept for guests, a room of one's own, a telescope, a pet, work they love)
    /// is watched for a while after it's granted (<see cref="WishDef.watchDays"/>, about 60 days, longer for rooms). If
    /// what was given goes missing, a grace period starts (the setting: none, half a day, a day, three days) and the
    /// player is told at once. Moving it is fine: a building packed up to be carried, stored or reinstalled, a plan or
    /// frame of the same thing being built (for up to a week), or a bed lent to a patient all count as still there.
    /// Back within the grace, nothing happens. Still gone after it, the colonist takes it as the council going back on
    /// its word: a bad mood that's larger the sooner it happened, the trust a broken promise costs, a journal line, a
    /// little trust from their partner and close friends, and they may ask for it again. Put back within a few days,
    /// the council makes it right: the mood ends early and half the trust comes back. A fire, a raid or a collapse
    /// isn't the council's doing: the watch ends quietly, and right after a big threat nothing goes missing for two
    /// days. Checked every two hours, never per tick.
    /// </summary>
    public class KeptTracker : IExposable
    {
        public List<GrantWatch> watches = new List<GrantWatch>();
        private int nextId = 1;
        /// <summary>The old "room taken" records on each colonist's standing have been turned into watches.</summary>
        private bool roomsMoved;

        /// <summary>The most watches kept; past it, the oldest that still holds makes way.</summary>
        public const int MaxWatches = 120;

        /// <summary>The most things remembered for one watch.</summary>
        private const int MaxThings = 12;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>On: wishes, the setting, and downsides (Balanced or Demanding stakes).</summary>
        public static bool On => S.enableWishes && S.wishesTakenBack && S.MoodEffects && S.Negatives;

        /// <summary>Who can mind: free colonists with an inner life and a mood, not guests staying for a quest.</summary>
        public static bool Minds(Pawn p) =>
            p != null && !p.Dead && p.IsFreeNonSlaveColonist && !p.IsQuestLodger() && p.needs?.mood != null && People.HasInnerLife(p);

        // ---------------- The things watched, for the destroy hook ----------------

        private static readonly HashSet<int> ids = new HashSet<int>();

        /// <summary>Any thing is being watched at all (the destroy hook's first, cheapest question).</summary>
        public static bool AnyThings => ids.Count > 0;

        private void RebuildIds()
        {
            ids.Clear();
            foreach (var w in watches)
                foreach (var t in w.things)
                    if (t != null)
                        ids.Add(t.thingIDNumber);
        }

        /// <summary>
        /// A thing is being destroyed (Patch_KeptDestroyed). One a watched wish rests on, lost to fire, a raid or a
        /// collapse, is remembered: losing it that way isn't the council going back on its word.
        /// </summary>
        public static void Notify_Destroyed(Thing t, DestroyMode mode)
        {
            if (t == null || !ids.Contains(t.thingIDNumber)) return;
            if (mode != DestroyMode.KillFinalize && mode != DestroyMode.KillFinalizeLeavingsOnly) return;
            var tracker = CouncilGameComponent.Instance?.kept;
            if (tracker == null) return;
            int now = Find.TickManager.TicksGame;
            foreach (var w in tracker.watches)
                if (w.things.Contains(t))
                    w.misfortuneTick = now;
        }

        // ---------------- Starting a watch ----------------

        /// <summary>A wish just came true: if it's a lasting one, watch it.</summary>
        public void Notify_Granted(CouncilGameComponent comp, Wish w)
        {
            if (!On || w?.def == null) return;
            var worker = w.Worker;
            try
            {
                if (!worker.Lasting(w)) return;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("lasting wish " + w.def.defName, e);
                return;
            }
            var p = w.Beneficiary;
            var map = p?.MapHeld;
            if (!Minds(p) || map == null || !map.IsPlayerHome) return;
            try
            {
                worker.OnGranted(w);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("kept wish granted " + w.def.defName, e);
            }
            int now = Find.TickManager.TicksGame;
            // The same wish granted again replaces the old watch.
            watches.RemoveAll(x => x.pawn == p && x.wish?.def == w.def && x.wish.subjectKey == w.subjectKey);
            var watch = new GrantWatch
            {
                id = nextId++,
                wish = w,
                pawn = p,
                label = w.Label,
                mapId = map.uniqueID,
                grantedTick = now,
                watchUntil = now + KeptMath.WatchTicks(w.def.watchDays),
                cause = "CC_Cause_TakenBack".Translate(p.LabelShort, w.Label).Resolve()
            };
            Remember(watch, worker);
            Add(watch);
        }

        /// <summary>A decree someone asked for was declared: watched until it runs its course.</summary>
        public void Notify_DecreeHeeded(Pawn asker, DecreeDef def, int endTick)
        {
            if (!On || def == null || !Minds(asker)) return;
            int now = Find.TickManager.TicksGame;
            if (endTick <= now) return;
            watches.RemoveAll(x => x.decree == def);
            string label = "CC_Kept_DecreeLabel".Translate(def.label).Resolve();
            Add(new GrantWatch
            {
                id = nextId++,
                decree = def,
                pawn = asker,
                label = label,
                mapId = asker.MapHeld?.uniqueID ?? -1,
                grantedTick = now,
                watchUntil = endTick,
                cause = "CC_Cause_TakenBack".Translate(asker.LabelShort, label).Resolve()
            });
        }

        /// <summary>A decree ended. Run its course: nothing was taken back. Lifted early: it's gone (the next check sees it).</summary>
        public void Notify_DecreeEnded(DecreeDef def, bool early)
        {
            if (!early) watches.RemoveAll(x => x.decree == def);
        }

        private void Add(GrantWatch watch)
        {
            watches.Add(watch);
            while (watches.Count > MaxWatches)
            {
                var oldest = watches.Where(x => x.phase == KeptPhase.Kept).OrderBy(x => x.grantedTick).FirstOrDefault() ?? watches[0];
                watches.Remove(oldest);
            }
            RebuildIds();
        }

        // ---------------- Asking again ----------------

        /// <summary>While it's missing and the grace runs, they don't ask for the same thing again.</summary>
        public bool Blocks(Pawn p, WishDef def)
        {
            for (int i = 0; i < watches.Count; i++)
            {
                var w = watches[i];
                if (w.phase == KeptPhase.Missing && w.pawn == p && w.wish?.def == def) return true;
            }
            return false;
        }

        /// <summary>It was taken back from them: they may ask for it again, and more readily than for something new.</summary>
        public bool Renewable(Pawn p, WishDef def)
        {
            for (int i = 0; i < watches.Count; i++)
            {
                var w = watches[i];
                if (w.phase == KeptPhase.TakenBack && w.pawn == p && w.wish?.def == def) return true;
            }
            return false;
        }

        /// <summary>A wish's freshness for this colonist: a renewed wish isn't stale for having been heard before.</summary>
        public float Fresh(Pawn p, WishDef def, float fresh) => watches.Count > 0 && Renewable(p, def) ? Mathf.Max(fresh, 1f) * 1.5f : fresh;

        /// <summary>
        /// A wish was just voiced. If it asks again for what was taken back, that watch ends here (granting the new wish
        /// is how it's made right now) and the request opens with a line about it. Returns that line, or null.
        /// </summary>
        public string Notify_Voiced(Wish w)
        {
            if (w?.def == null || watches.Count == 0) return null;
            var p = w.Beneficiary;
            var watch = watches.FirstOrDefault(x => x.phase == KeptPhase.TakenBack && x.pawn == p && x.wish?.def == w.def);
            if (watch == null) return null;
            watches.Remove(watch);
            RebuildIds();
            return Flavor.FromPool("Council_Pool_RenewedWish", w.pawn, w, w.tone);
        }

        // ---------------- Every two hours ----------------

        public void Hourly(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            if (!roomsMoved) MoveOldRooms(comp, now);
            if (watches.Count == 0 || !KeptMath.CheckDue(now) || !On) return;
            int interval = KeptMath.CheckHours * KeptMath.TicksPerHour;
            foreach (var w in watches.ToList())
            {
                try
                {
                    Step(comp, w, now, interval);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("promise kept " + (w.Def?.defName ?? w.decree?.defName), e);
                }
            }
            RebuildIds();
        }

        private void Step(CouncilGameComponent comp, GrantWatch w, int now, int interval)
        {
            var check = Check(comp, w, now, out bool moving);
            // Being moved or rebuilt keeps a promise, but doesn't make one right again: that takes the real thing back.
            if (check == KeptCheck.Holds && moving && w.phase == KeptPhase.TakenBack) check = KeptCheck.Unclear;
            // While they can't see it (away, or the colony just fought off a raid), the grace's clock stops.
            if (check == KeptCheck.Unclear && w.phase == KeptPhase.Missing) w.graceUntil += interval;
            switch (KeptMath.Next(w.phase, check, now, w.watchUntil, w.graceUntil, w.takenBackTick))
            {
                case KeptStep.StartGrace:
                    StartGrace(comp, w, now);
                    break;
                case KeptStep.BackInTime:
                    BackInTime(w);
                    break;
                case KeptStep.TakeBack:
                    TakeBack(comp, w, now);
                    break;
                case KeptStep.MadeRight:
                    MadeRight(comp, w);
                    break;
                case KeptStep.Close:
                    watches.Remove(w);
                    break;
            }
        }

        private static Map MapOf(GrantWatch w)
        {
            var maps = Find.Maps;
            for (int i = 0; i < maps.Count; i++)
                if (maps[i].uniqueID == w.mapId)
                    return maps[i];
            return null;
        }

        /// <summary>Is what was given still there, in the council's eyes? <paramref name="moving"/>: only because it's being moved or rebuilt.</summary>
        private KeptCheck Check(CouncilGameComponent comp, GrantWatch w, int now, out bool moving)
        {
            moving = false;
            var p = w.pawn;
            if (p == null || p.Dead || p.Destroyed || !p.IsColonist) return KeptCheck.Over;
            // Enslaved or imprisoned: their claim on the council is gone. Otherwise (a guest's status for a while) wait.
            if (!Minds(p)) return p.IsSlaveOfColony || p.IsPrisoner ? KeptCheck.Over : KeptCheck.Unclear;
            if (w.decree != null) return comp.decrees.IsActive(w.decree) ? KeptCheck.Holds : KeptCheck.Gone;
            if (w.wish?.def == null) return KeptCheck.Over;
            var map = MapOf(w);
            // The place it was given is gone (abandoned, or the colony moved on): nothing left to keep.
            if (map == null) return KeptCheck.Over;
            // Away in a caravan, at an outpost, or living on another of the colony's maps for now.
            if (p.MapHeld != map) return KeptCheck.Unclear;
            var worker = w.wish.Worker;
            var check = worker.Holds(w.wish);
            if (check == KeptCheck.Holds)
            {
                Remember(w, worker);
                w.plannedSince = -1;
                w.misfortuneTick = -1;
                return KeptCheck.Holds;
            }
            if (check != KeptCheck.Gone) return check;
            // Gone. But was it the council?
            if (!worker.StillObtainable(w.wish)) return KeptCheck.Over;
            if (w.misfortuneTick >= 0 && now - w.misfortuneTick <= KeptMath.MisfortuneDays * KeptMath.TicksPerDay) return KeptCheck.Over;
            int threat = map.storyState?.LastThreatBigTick ?? 0;
            if (threat > 0 && now - threat < KeptMath.ThreatDays * KeptMath.TicksPerDay) return KeptCheck.Unclear;
            // Being moved: packed up to be carried, stored or reinstalled, or a bed lent to a patient.
            moving = true;
            if (Moving(w, map) || LentToTheSick(w, map)) return KeptCheck.Holds;
            // Being built again, from a plan or a frame (for a week at most).
            if (BeingBuilt(w, worker, map))
            {
                if (w.plannedSince < 0) w.plannedSince = now;
                return KeptMath.PlanStillCounts(w.plannedSince, now) ? KeptCheck.Holds : KeptCheck.Gone;
            }
            return KeptCheck.Gone;
        }

        /// <summary>The things that make it hold, and where they stand, for the next time it doesn't.</summary>
        private static void Remember(GrantWatch w, WishWorker worker)
        {
            if (w.wish == null) return;
            w.things.Clear();
            w.cells.Clear();
            foreach (var t in worker.KeptThings(w.wish))
            {
                if (t == null || !t.Spawned) continue;
                w.things.Add(t);
                w.cells.Add(t.Position);
                if (w.things.Count >= MaxThings) break;
            }
        }

        /// <summary>One of the things kept is packed up (minified) somewhere the colony keeps it: carried, stored, in a pack.</summary>
        private static bool Moving(GrantWatch w, Map map)
        {
            foreach (var t in w.things)
            {
                if (t == null || t.Destroyed || t.Spawned || !(t.ParentHolder is MinifiedThing mini)) continue;
                if (mini.Destroyed || mini.MapHeld != map) continue;
                if (mini.Spawned) return true;
                var holder = mini.ParentHolder;
                if (holder is Pawn_CarryTracker carry && carry.pawn?.Faction == Faction.OfPlayer) return true;
                if (holder is Pawn_InventoryTracker inv && inv.pawn?.Faction == Faction.OfPlayer) return true;
                if (holder is Thing box && box.Faction == Faction.OfPlayer) return true;
            }
            return false;
        }

        /// <summary>A bed that was given (or stood where it did) is set aside for a patient right now.</summary>
        private static bool LentToTheSick(GrantWatch w, Map map)
        {
            string kind = w.Kind;
            bool beds = kind == "bed" || kind == "ownroom";
            foreach (var t in w.things)
                if (t is Building_Bed)
                {
                    beds = true;
                    break;
                }
            if (!beds) return false;
            foreach (var c in w.cells)
            {
                if (!c.InBounds(map)) continue;
                var list = c.GetThingList(map);
                for (int i = 0; i < list.Count; i++)
                    if (list[i] is Building_Bed bed && bed.Faction == Faction.OfPlayer && bed.Medical && bed.AnyOccupants)
                        return true;
            }
            return false;
        }

        /// <summary>A plan or frame of the same kind of thing, where it belongs (their room, or anywhere in the colony).</summary>
        private static bool BeingBuilt(GrantWatch w, WishWorker worker, Map map)
        {
            List<Room> rooms = null;
            if (worker.InTheirRoom)
            {
                rooms = new List<Room>();
                var own = worker.TheirRoom(w.wish);
                if (own != null && !own.PsychologicallyOutdoors) rooms.Add(own);
                foreach (var c in w.cells)
                {
                    if (!c.InBounds(map)) continue;
                    var r = c.GetRoom(map);
                    if (r != null && !r.PsychologicallyOutdoors && !rooms.Contains(r)) rooms.Add(r);
                }
                if (rooms.Count == 0) return false;
            }
            return Plans(map, ThingRequestGroup.Blueprint, w, worker, rooms) || Plans(map, ThingRequestGroup.BuildingFrame, w, worker, rooms);
        }

        private static bool Plans(Map map, ThingRequestGroup group, GrantWatch w, WishWorker worker, List<Room> rooms)
        {
            var list = map.listerThings.ThingsInGroup(group);
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                if (t.Faction != Faction.OfPlayer) continue;
                var d = t.def.entityDefToBuild;
                if (d == null || !worker.Concerns(w.wish, d)) continue;
                if (rooms == null) return true;
                foreach (var r in rooms)
                    if (r.ContainsCell(t.Position))
                        return true;
            }
            return false;
        }

        // ---------------- What happens ----------------

        /// <summary>The words for the grace period: half a day, a day, three days.</summary>
        private static string GraceWords(int ticks) => ticks <= KeptMath.TicksPerDay / 2 ? "CC_Kept_HalfDay".Translate().ToString()
            : ticks <= KeptMath.TicksPerDay ? "CC_Kept_OneDay".Translate().ToString() : Span(ticks);

        /// <summary>A stretch of time for the list: hours under a day, else days.</summary>
        public static string Span(int ticks)
        {
            if (ticks < KeptMath.TicksPerDay) return TextUtil.DaysString(Math.Max(1, ticks));
            string days = (ticks / (float)KeptMath.TicksPerDay).ToString("0.#");
            return days == "1" ? "CC_Kept_OneDay".Translate().ToString() : "CC_Days".Translate(days).ToString();
        }

        /// <summary>What's gone, in a sentence.</summary>
        public static string LostText(GrantWatch w)
        {
            if (w.decree != null) return "CC_Kept_Lost_Decree".Translate(w.pawn.Named("PAWN"), w.label.Named("DECREE")).Resolve();
            try
            {
                string s = w.wish.Worker.LostText(w.wish);
                if (!s.NullOrEmpty()) return s;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("lost text " + w.Def?.defName, e);
            }
            return "CC_Kept_Lost_Generic".Translate(w.pawn.Named("PAWN"), w.label.Named("WISH")).Resolve();
        }

        private static LookTargets Look(GrantWatch w) => w.pawn != null && w.pawn.Spawned ? new LookTargets(w.pawn) : LookTargets.Invalid;

        /// <summary>It has just gone missing: the grace starts, and the player hears of it at once.</summary>
        private void StartGrace(CouncilGameComponent comp, GrantWatch w, int now)
        {
            int grace = KeptMath.GraceTicks(S.takenBackGrace);
            w.phase = KeptPhase.Missing;
            w.missingSince = now;
            w.graceUntil = now + grace;
            if (grace <= 0)
            {
                TakeBack(comp, w, now);
                return;
            }
            string text = "CC_Kept_Missing".Translate(LostText(w).Named("LOST"), w.pawn.Named("PAWN"), GraceWords(grace).Named("TIME")).Resolve();
            Messages.Message(text, Look(w), MessageTypeDefOf.NeutralEvent, true);
        }

        /// <summary>Back within the grace: nothing comes of it, beyond a line in the colony news.</summary>
        private static void BackInTime(GrantWatch w)
        {
            w.phase = KeptPhase.Kept;
            w.missingSince = -1;
            w.graceUntil = -1;
            News.Post("CC_Kept_BackInTime".Translate(w.pawn.Named("PAWN"), w.label.Named("WISH")).Resolve(), Look(w), NewsKind.Colony, MessageTypeDefOf.SilentInput);
        }

        /// <summary>Still gone after the grace: the council went back on its word.</summary>
        private void TakeBack(CouncilGameComponent comp, GrantWatch w, int now)
        {
            var p = w.pawn;
            using var _cause = Causes.Push(w.cause);
            w.phase = KeptPhase.TakenBack;
            w.takenBackTick = now;
            w.missingSince = -1;

            // Trust: as a broken promise.
            float lost = KeptMath.TrustLoss * CouncilGameComponent.Gentleness(p);
            comp.standings.Change(p, -lost, "CC_Log_TakenBack".Translate(w.label).ToString());
            comp.standings.For(p).promisesBroken++;
            w.trustLost = lost;

            // The mood: larger the sooner it was taken, scaled by the stakes and by their standards (it's a letdown).
            float pen = S.PenaltyScale;
            // A pet sold from under them already has its own mood ("they sold my animal"): the trust and the journal say the rest.
            var sold = w.Kind == "pet" ? DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Setback_AnimalSold") : null;
            bool saidAlready = sold != null && p.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(sold) != null;
            if (pen > 0f && !saidAlready && KeptDefOf.Council_TakenBack != null)
                CouncilGameComponent.GiveThought(p, KeptDefOf.Council_TakenBack, null, pen * KeptMath.MoodShare(w.grantedTick, now, w.watchUntil - w.grantedTick), w.cause);

            // Whoever spoke up for them or backed the wish, and their partner and close friends, mind a little.
            var involved = new HashSet<Pawn> { p };
            if (w.wish != null)
                foreach (var o in w.wish.Supporters)
                    if (involved.Add(o) && o.IsColonist && !o.Dead)
                        comp.standings.Change(o, -3f * CouncilGameComponent.Gentleness(o), "CC_Log_BackedTakenBack".Translate(p.LabelShort).ToString());
            var map = p.MapHeld;
            if (map != null)
            {
                foreach (var f in map.mapPawns.FreeColonistsSpawned)
                {
                    if (involved.Contains(f) || f.relations == null) continue;
                    if (LovePartnerRelationUtility.LovePartnerRelationExists(f, p) || f.relations.OpinionOf(p) >= 40 || comp.social.Warmth(f, p) >= 40f)
                        comp.standings.Change(f, -2f * CouncilGameComponent.Gentleness(f), "CC_Log_FriendTakenBack".Translate(p.LabelShort).ToString());
                }
            }

            WriteJournal(comp, w, "Council_Journal_TakenBack");
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.WishExpired, "CC_Chronicle_TakenBack".Translate(p.LabelShort, w.label).Resolve(), p));
            News.Post("CC_Kept_TakenBack".Translate(p.Named("PAWN"), w.label.Named("WISH")).Resolve(), Look(w), NewsKind.Colony, MessageTypeDefOf.NegativeEvent);
            // They may ask for it again, soon.
            if (w.Def != null) comp.AllowRenewal(p, w.Def);
        }

        /// <summary>Put back within a few days of being taken: the mood ends early and half the trust comes back.</summary>
        private void MadeRight(CouncilGameComponent comp, GrantWatch w)
        {
            var p = w.pawn;
            var memories = p.needs?.mood?.thoughts?.memories;
            if (memories != null && KeptDefOf.Council_TakenBack != null)
            {
                var mine = memories.Memories.Where(m => m.def == KeptDefOf.Council_TakenBack && m is ICausedThought c && c.Cause == w.cause).ToList();
                foreach (var m in mine) memories.RemoveMemory(m);
            }
            comp.standings.Change(p, KeptMath.TrustBack(w.trustLost), "CC_Log_MadeRight".Translate(w.label).ToString());
            // A small relief: it feeds their gratitude rather than their mood (the bad mood ending is the mood's part).
            using (Causes.Push("CC_Cause_MadeRight".Translate(p.LabelShort, w.label).Resolve()))
                CouncilGameComponent.GiveThought(p, KeptDefOf.Council_MadeRight);
            var st = comp.standings.For(p);
            if (st.promisesBroken > 0) st.promisesBroken--;
            WriteJournal(comp, w, "Council_Journal_MadeRight");
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.WishGranted, "CC_Chronicle_MadeRight".Translate(p.LabelShort, w.label).Resolve(), p));
            News.Post("CC_Kept_MadeRight".Translate(p.Named("PAWN"), w.label.Named("WISH")).Resolve(), Look(w), NewsKind.Colony, MessageTypeDefOf.PositiveEvent);
            w.phase = KeptPhase.Kept;
            w.takenBackTick = -1;
            w.graceUntil = -1;
            w.trustLost = 0f;
        }

        /// <summary>A line in their journal: {WISH}, and {DAYS} for how long they had it. Tagged with what kind of thing it was.</summary>
        private static void WriteJournal(CouncilGameComponent comp, GrantWatch w, string pool)
        {
            var tags = new[] { "kept:" + w.Kind };
            var days = Span(Math.Max(1, Find.TickManager.TicksGame - w.grantedTick)).Named("DAYS");
            if (w.wish != null) comp.journals.WriteTagged(w.pawn, pool, tags, w.wish, days);
            else comp.journals.WriteTagged(w.pawn, pool, tags, null, w.label.Named("WISH"), days);
        }

        // ---------------- Older saves ----------------

        /// <summary>
        /// Saves from before this: a colonist given a room of their own was remembered on their standing (the old "room
        /// taken" mood). Those rooms are watched like any other granted wish from now on.
        /// </summary>
        private void MoveOldRooms(CouncilGameComponent comp, int now)
        {
            roomsMoved = true;
            var def = DefDatabase<WishDef>.GetNamedSilentFail("Council_Wish_OwnRoom");
            foreach (var st in comp.standings.standings)
            {
                if (st?.pawn == null || st.ownRoomGranted <= 0) continue;
                int granted = st.ownRoomGranted;
                st.ownRoomGranted = 0;
                st.ownRoomLostHours = 0;
                st.ownRoomLossNoted = false;
                var p = st.pawn;
                var map = p.MapHeld;
                if (def == null || !Minds(p) || map == null || !map.IsPlayerHome) continue;
                int until = granted + KeptMath.WatchTicks(def.watchDays);
                if (until <= now || watches.Any(x => x.pawn == p && x.wish?.def == def)) continue;
                var wish = new Wish { id = -1, def = def, pawn = p, state = WishState.Granted, createdTick = granted, expiresTick = granted };
                var watch = new GrantWatch
                {
                    id = nextId++,
                    wish = wish,
                    pawn = p,
                    label = wish.Label,
                    mapId = map.uniqueID,
                    grantedTick = granted,
                    watchUntil = until,
                    cause = "CC_Cause_TakenBack".Translate(p.LabelShort, wish.Label).Resolve()
                };
                Remember(watch, def.Worker);
                watches.Add(watch);
            }
            RebuildIds();
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) watches.RemoveAll(w => w == null || w.pawn == null || w.pawn.Discarded);
            Scribe_Collections.Look(ref watches, "watches", LookMode.Deep);
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Values.Look(ref roomsMoved, "roomsMoved");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (watches == null) watches = new List<GrantWatch>();
                // A wish or decree whose mod was removed, or someone the game has forgotten: let it go.
                watches.RemoveAll(w => w == null || w.pawn == null || w.decreeLost
                                       || (w.wish == null && w.decree == null)
                                       || (w.wish != null && (w.wish.def == null || w.wish.thingLost)));
                RebuildIds();
            }
        }
    }
}
