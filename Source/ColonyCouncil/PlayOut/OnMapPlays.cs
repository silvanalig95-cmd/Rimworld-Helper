using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // Plays of one particular thing, or one round, on the groups' engine (PlayGroups.cs): a gift carried over by the one
    // who made it, a stash carried out to the yard and burned, a doctor's or a cook's rounds, the enemy dead carried out
    // to a pyre or the tree line. Like the other groups they wait for their hours, for danger to pass and for their
    // people to be free; anyone drafted, downed, hungry or tired is let go, and none of it is ever forced on anyone.

    /// <summary>
    /// A thing carried by hand (PlayKind.Give): the giver takes it from where it lies to the one it's for and sets it
    /// down at their feet (<see cref="Give"/>); or carries it out to the yard and burns it (<see cref="Burn"/>); or, for a
    /// thing a visitor made, the one it's for collects it from beside them and takes it in to the table (<see cref="Fetch"/>).
    /// The thing is forbidden while it waits, so nobody else carries it off or drinks it first.
    /// </summary>
    public static class PlayGive
    {
        public const string Give = "give", Burn = "burn", Fetch = "fetch";

        public static Thing ThingOf(PlayOut p) => p?.things.FirstOrDefault(t => t != null && !t.Destroyed);

        /// <summary>The words for a thing in a message: a packed sculpture by what's inside it.</summary>
        public static string Label(Thing t)
        {
            if (t == null) return "";
            var inner = (t as MinifiedThing)?.InnerThing ?? t;
            // A book by what it is (its own title would read oddly in "the ...").
            return inner is Book ? inner.def.label : inner.LabelNoCount;
        }

        /// <summary>Sets the carrier off: to the person, the yard or the table. False (with a reason) when it can't start yet.</summary>
        public static bool Start(PlayOutTracker t, PlayOut p, out string where)
        {
            where = null;
            var carrier = p.hands[0].pawn;
            var thing = ThingOf(p);
            if (thing == null || !thing.Spawned || thing.Map != p.map)
            {
                // Gone meanwhile (sold, burned, put away in a container): nothing left to carry.
                t.Fallback(p, "CC_Play_Why_GiftGone".Translate(), false);
                return false;
            }
            if (!carrier.CanReserveAndReach(thing, PathEndMode.ClosestTouch, Danger.Some))
            {
                p.waitReason = "CC_Play_Why_Place".Translate();
                return false;
            }
            LocalTargetInfo to;
            if (p.mode == Give)
            {
                var o = p.other;
                if (o == null || o.Dead)
                {
                    t.Fallback(p, null, true);
                    return false;
                }
                if (!o.Spawned || o.Map != p.map)
                {
                    p.waitReason = "CC_Play_Why_Away".Translate(o.Named("OTHER")).Resolve();
                    return false;
                }
                // Handed over to someone awake and about, not to a sleeper or someone in a state.
                if (!o.Awake() || o.InMentalState || o.Drafted || o.Downed)
                {
                    p.waitReason = "CC_Play_Why_NotFree".Translate(o.Named("PAWN")).Resolve();
                    return false;
                }
                if (!carrier.CanReach(o, PathEndMode.Touch, Danger.Some))
                {
                    p.waitReason = "CC_Play_Why_Place".Translate();
                    return false;
                }
                to = o;
                where = o.LabelShort;
            }
            else
            {
                var c = p.mode == Burn ? PlaySpots.OpenCell(p.map, carrier) : TableCell(p, carrier);
                if (!c.IsValid)
                {
                    p.waitReason = "CC_Play_Why_Place".Translate();
                    return false;
                }
                p.cell = c;
                to = c;
                where = (p.mode == Burn ? "CC_Play_To_Yard" : "CC_Play_To_Table").Translate();
            }
            var job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutGive, thing, to);
            job.count = thing.stackCount;
            carrier.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return true;
        }

        /// <summary>Where a thing collected from a visitor is taken: the dining table, the rec room, a gather spot, the open air.</summary>
        private static IntVec3 TableCell(PlayOut p, Pawn carrier)
        {
            foreach (var s in new[] { PlaySpot.Dining, PlaySpot.Rec, PlaySpot.Gather })
            {
                var c = PlaySpots.FocusCell(p, s, carrier, out _);
                if (c.IsValid) return c;
            }
            return PlaySpots.OpenCell(p.map, carrier);
        }

        /// <summary>
        /// The carrier has come with it: it's set down at the feet of the one it's for, or on the table, or burned in the
        /// yard. The plan is over (the held moods go to the giver and the one it was for).
        /// </summary>
        public static bool Arrive(PlayOut p, Pawn carrier, Pawn to)
        {
            var t = PlayOutTracker.Instance;
            if (p == null || t == null || p.kind != PlayKind.Give || p.state != PlayState.Running || carrier == null) return false;
            var carried = carrier.carryTracker?.CarriedThing;
            if (carried == null) return false;
            string what = Label(carried);
            var map = carrier.Map;
            if (p.mode == Burn)
            {
                var at = carrier.Position;
                carrier.carryTracker.DestroyCarriedThing();
                Burned(map, at);
            }
            else
            {
                var cell = to != null && to.Spawned && to.Map == map ? to.Position : carrier.Position;
                if (carrier.carryTracker.TryDropCarriedThing(cell, ThingPlaceMode.Near, out var lying) && lying != null)
                    lying.SetForbidden(false, false);
            }
            p.tally = 1;
            var h = p.Hand(carrier);
            if (h != null) h.counted = true;
            PlayWords.During(p, carrier, to);
            PlayGroups.Finish(t, p, false, null, to, what);
            return true;
        }

        /// <summary>Ash and smoke where something was burned.</summary>
        public static void Burned(Map map, IntVec3 at)
        {
            if (map == null || !at.IsValid) return;
            if (ThingDefOf.Filth_Ash != null) FilthMaker.TryMakeFilth(at, map, ThingDefOf.Filth_Ash, 2);
            var v = at.ToVector3Shifted();
            FleckMaker.ThrowFireGlow(v, map, 1f);
            FleckMaker.ThrowMicroSparks(v, map);
            for (int i = 0; i < 3; i++) FleckMaker.ThrowSmoke(v, map, 1.2f);
        }

        /// <summary>
        /// It never came about: a gift is set down beside the one it was for after all (as answers always did), a stash
        /// burned where it lies, and a thing waiting by a visitor is left there for the colony's haulers.
        /// </summary>
        public static void FellThrough(PlayOut p)
        {
            var thing = ThingOf(p);
            if (thing == null || !thing.Spawned) return;
            var map = thing.Map;
            var pos = thing.Position;
            switch (p.mode)
            {
                case Burn:
                    thing.Destroy();
                    Burned(map, pos);
                    return;
                case Give:
                {
                    thing.SetForbidden(false, false);
                    var o = p.other;
                    if (o == null || o.Dead || !o.Spawned || o.Map != map || o.Position.InHorDistOf(pos, 3f)) return;
                    thing.DeSpawn();
                    if (!GenPlace.TryPlaceThing(thing, o.Position, map, ThingPlaceMode.Near)) GenPlace.TryPlaceThing(thing, pos, map, ThingPlaceMode.Near);
                    return;
                }
                default:
                    thing.SetForbidden(false, false);
                    return;
            }
        }
    }

    /// <summary>
    /// Something carried over by hand: the one it's from takes it up where it lies, walks it to the one it's for (or out
    /// to the yard, or in to the table) and sets it down. Danger, drafting or need ends it, and whatever they carry is set
    /// down where they stand, still the colony's.
    /// </summary>
    public class JobDriver_PlayOutGive : JobDriver
    {
        private bool done;

        private PlayOut Play => PlayOutTracker.Instance?.GroupFor(pawn);

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);

        public override string GetReport()
        {
            var p = Play;
            string what = PlayGive.Label(job.targetA.Thing ?? pawn.carryTracker?.CarriedThing);
            if (p == null || what.NullOrEmpty()) return base.GetReport();
            if (job.targetB.Thing is Pawn x) return "CC_Play_Report_Give".Translate(what, x.Named("OTHER")).Resolve();
            return (p.mode == PlayGive.Burn ? "CC_Play_Report_Burn" : "CC_Play_Report_Fetch").Translate(what).Resolve();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            this.FailOn(() => !PlayOuts.CanJoin(pawn));
            AddFinishAction(cond =>
            {
                try
                {
                    // Not handed over: what they carry stays the colony's, set down where they stand.
                    if (!done && pawn.carryTracker?.CarriedThing != null && pawn.Spawned)
                        pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gift end", e);
                }
            });
            // The thing waits forbidden (so nobody else takes it), so only its being gone ends the job.
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false, true);
            if (job.targetB.HasThing)
            {
                this.FailOn(() => job.targetB.Thing is Pawn x && (x.Dead || !x.Spawned || x.Map != pawn.Map));
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            }
            else yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            var hand = ToilMaker.MakeToil("PlayOutGive");
            hand.initAction = () =>
            {
                try
                {
                    // together: the giver has just walked over to the one it's for (or out to the yard on their own).
                    done = PlayGive.Arrive(Play, pawn, job.targetB.Thing as Pawn);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gift hand over", e);
                }
            };
            hand.defaultCompleteMode = ToilCompleteMode.Delay;
            hand.defaultDuration = 120;
            hand.handlingFacing = true;
            hand.tickIntervalAction = _ =>
            {
                if (job.targetB.HasThing) pawn.rotationTracker.FaceTarget(job.targetB);
            };
            yield return hand;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref done, "done");
        }
    }

    /// <summary>
    /// Something the answer says is gotten rid of, carried out to the yard and burned there by A (or <see cref="who"/>):
    /// the event's own thing (a stash of {COUNT} {THING}), made where A stands, forbidden so nobody helps themselves, and
    /// burned in the open with ash and smoke to show it. Nothing set going (the setting off, A not here): the words alone.
    /// </summary>
    public class DilemmaEffect_BurnIt : DilemmaEffect_PlayOut
    {
        public PlayWho who = PlayWho.A;

        [Unsaved] private Thing pending;
        [Unsaved] private Pawn pendingCarrier;

        public DilemmaEffect_BurnIt()
        {
            hours = new IntRange(7, 21);
            duration = new FloatRange(1f, 1.5f);
            waitHours = 8f;
        }

        protected override PlayKind Kind => PlayKind.Give;

        public override void Apply(DilemmaContext ctx)
        {
            var map = ctx?.map;
            if (!PlayOuts.On || map == null || ctx.thing == null || ctx.count <= 0) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            var carrier = PlayOuts.Who(who, ctx);
            if (!PlayOuts.OurPerson(carrier) || !carrier.Spawned || carrier.Map != map) return;
            Thing thing = null;
            try
            {
                thing = ThingMaker.MakeThing(ctx.thing, ctx.thing.MadeFromStuff ? GenStuff.DefaultStuffFor(ctx.thing) : null);
                thing.stackCount = Math.Max(1, Math.Min(ctx.count, ctx.thing.stackLimit));
                if (!GenPlace.TryPlaceThing(thing, carrier.Position, map, ThingPlaceMode.Near, out var lying))
                {
                    thing.Destroy();
                    return;
                }
                thing = lying ?? thing;
                thing.SetForbidden(true, false);
                pending = thing;
                pendingCarrier = carrier;
                float was = chance;
                chance = 1f;
                PlayOut p;
                try
                {
                    p = Create(ctx);
                }
                finally
                {
                    chance = was;
                }
                if (p == null && !thing.Destroyed) thing.Destroy();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("burn it", e);
                if (thing != null && !thing.Destroyed && thing.Spawned) thing.Destroy();
            }
            finally
            {
                pending = null;
                pendingCarrier = null;
            }
        }

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (pending == null || pendingCarrier == null) return false;
            p.things = new List<Thing> { pending };
            p.visitor = pendingCarrier;
            p.organizer = pendingCarrier;
            p.invited = new List<Pawn> { pendingCarrier };
            p.named = new List<Pawn> { pendingCarrier };
            p.mode = PlayGive.Burn;
            p.count = 1;
            p.durationTicks = PlayOutMath.DurationTicks(PlayOutMath.CapHours(duration.RandomInRange, false));
            return true;
        }

        protected override string DefaultLabel(DilemmaContext ctx) => "CC_Play_Label_Burn".Translate().Resolve();
    }

    /// <summary>
    /// Rounds (PlayKind.Rounds): one colonist (<see cref="who"/>, A unless given) goes round to each of a few others in
    /// turn, the nearest first, and stops a moment with each: a doctor looking everyone over, a cook taking a pot out to
    /// those working outside (<see cref="outdoors"/>: only those out in the open when it starts). Up to
    /// <see cref="count"/> people. What's said comes from <see cref="lines"/>, then <see cref="pool"/>.
    /// </summary>
    public class DilemmaEffect_Rounds : DilemmaEffect_PlayOut
    {
        public PlayWho who = PlayWho.A;
        public bool outdoors;
        public int count = 6;
        public string pool;
        [MustTranslate] public List<string> lines;

        public DilemmaEffect_Rounds()
        {
            hours = new IntRange(8, 18);
            duration = new FloatRange(1.5f, 2f);
            waitHours = 8f;
        }

        protected override PlayKind Kind => PlayKind.Rounds;

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            var v = PlayOuts.Who(who, ctx);
            if (!PlayOuts.OurPerson(v)) return false;
            p.visitor = v;
            p.organizer = v;
            p.invited = new List<Pawn> { v };
            // The one going round, and the answer's own people (A, B), hold their good moods until it's done.
            p.named = new List<Pawn> { v };
            foreach (var x in new[] { ctx.a, ctx.b })
                if (x != null && !p.named.Contains(x) && PlayOuts.OurPerson(x)) p.named.Add(x);
            p.count = 1;
            p.amount = Mathf.Clamp(count, 1, OnMapMath.MaxRounds);
            p.mode = outdoors ? PlayRounds.Outdoors : null;
            p.pool = pool;
            p.lines = lines?.Where(l => !l.NullOrEmpty()).Select(ctx.Format).ToList() ?? new List<string>();
            p.durationTicks = PlayOutMath.DurationTicks(PlayOutMath.CapHours(duration.RandomInRange, false));
            return true;
        }

        protected override string DefaultLabel(DilemmaContext ctx) => "CC_Play_Label_Rounds".Translate().Resolve();
    }

    /// <summary>The rounds' way: who to go round to, where to stand by each, and how long to stop.</summary>
    public static class PlayRounds
    {
        public const string Outdoors = "outdoors";

        /// <summary>Someone to stop by: here, up, awake, not under arms or in a state.</summary>
        public static bool Visitable(Pawn x, Map map) =>
            x != null && !x.Dead && x.Spawned && x.Map == map && !x.Downed && x.Awake() && !x.Drafted && !x.InMentalState && PlayOuts.OurPerson(x);

        private static bool Outside(Pawn x, Map map) => x != null && x.Spawned && !x.Position.Roofed(map);

        /// <summary>Who to go round to, nearest first, and the walk begins. False (with a reason) when there's nobody yet.</summary>
        public static bool Start(PlayOut p, out string where)
        {
            where = null;
            var v = p.hands[0].pawn;
            var map = p.map;
            var pool = map.mapPawns.FreeColonistsSpawned.Where(x => x != v && Visitable(x, map) && (p.mode != Outdoors || Outside(x, map))).ToList();
            if (pool.Count == 0)
            {
                p.waitReason = (p.mode == Outdoors ? "CC_Play_Why_NobodyOut" : "CC_Play_Why_Busy").Translate();
                return false;
            }
            var order = new List<Pawn>();
            var at = v.Position;
            while (pool.Count > 0 && order.Count < Math.Max(1, p.amount))
            {
                var next = pool.MinBy(x => x.Position.DistanceToSquared(at));
                order.Add(next);
                pool.Remove(next);
                at = next.Position;
            }
            p.round = order;
            p.tally = 0;
            var h = p.hands[0];
            h.done = 0;
            h.waited = 0;
            h.cell = IntVec3.Invalid;
            var job = JobMaker.MakeJob(PlayOutDefOf.Council_PlayOutRounds);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            v.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            where = PlayWords.Names(order);
            return true;
        }

        /// <summary>The one they're with now (or on their way to), or null when the round is done.</summary>
        public static Pawn Current(PlayOut p, PlayHand h) => h != null && h.done >= 0 && h.done < p.round.Count ? p.round[h.done] : null;

        /// <summary>Where to stand now: beside the next person on the round (following them if they move on), facing them.</summary>
        public static bool Where(PlayOut p, Pawn v, PlayHand h, out IntVec3 cell, out LocalTargetInfo face)
        {
            cell = IntVec3.Invalid;
            face = LocalTargetInfo.Invalid;
            var map = p.map;
            int guard = 0;
            while (h.done < p.round.Count && guard++ < 20)
            {
                var x = p.round[h.done];
                // Gone indoors before the pot reached them, asleep, called away: on to the next.
                if (!Visitable(x, map) || (p.mode == Outdoors && h.waited == 0 && !Outside(x, map)))
                {
                    Next(p, h, false);
                    continue;
                }
                if (!h.cell.IsValid || !h.cell.InHorDistOf(x.Position, 2.5f) || !h.cell.Standable(map))
                    h.cell = Beside(x, v, map);
                if (!h.cell.IsValid)
                {
                    Next(p, h, false);
                    continue;
                }
                cell = h.cell;
                face = x;
                return true;
            }
            return false;
        }

        private static IntVec3 Beside(Pawn x, Pawn v, Map map)
        {
            foreach (var c in GenAdj.CellsAdjacent8Way(new TargetInfo(x.Position, map)).OrderBy(c => c.DistanceToSquared(v.Position)))
                if (c.InBounds(map) && c.Standable(map) && (c == v.Position || c.GetFirstPawn(map) == null) && v.CanReach(c, PathEndMode.OnCell, Danger.Some))
                    return c;
            return IntVec3.Invalid;
        }

        /// <summary>Beside them: a word as they come up, a few minutes with them, then on to the next.</summary>
        public static void Arrived(PlayOut p, Pawn v, PlayHand h, int delta)
        {
            var x = Current(p, h);
            if (x == null || !x.Spawned || !x.Position.InHorDistOf(v.Position, 2.9f)) return;
            bool first = h.waited <= 0;
            h.waited += delta;
            h.ticks += delta;
            // together: said beside them, a step away (the rounds' job brought the one going round to them).
            if (first) PlayWords.During(p, v, x);
            if (h.waited >= OnMapMath.RoundsStop(p.durationTicks, p.round.Count)) Next(p, h, true);
        }

        /// <summary>On to the next person: this one seen (or passed over).</summary>
        public static void Next(PlayOut p, PlayHand h, bool seen)
        {
            if (seen) p.tally++;
            h.done++;
            h.waited = 0;
            h.cell = IntVec3.Invalid;
            if (OnMapMath.RoundsCount(p.tally, p.round.Count)) h.counted = true;
        }
    }

    /// <summary>
    /// The enemy dead dealt with as the answer says (PlayKind.Haul): a few of the colony (three unless the count says)
    /// each take one of the raiders' bodies at a time, with the game's own hauling, to where <see cref="to"/> says: a
    /// pyre beyond the gate ("pyre": lit at dusk by one of them, the bodies burning on it until morning, OnMapTimed.cs),
    /// the tree line ("treeline": left there), graves dug outside the wall ("graves", a grave each, or "trench", side by
    /// side: PlayDead.cs) or the colony's crematorium ("crematorium": burned in it; the answer can't be given without a
    /// working one). Only the bodies of those who came against the colony, never anyone of its own or a friend's; at
    /// most <see cref="most"/>. No bodies left (buried, hauled off, rotted away): nothing is set going.
    /// </summary>
    public class DilemmaEffect_HaulDead : DilemmaEffect_Group
    {
        public string to = PlayHaul.Pyre;
        public int most = OnMapMath.MaxDead;

        public DilemmaEffect_HaulDead()
        {
            target = DilemmaTarget.Colony;
            duration = new FloatRange(2f, 3f);
            hours = new IntRange(7, 17);
        }

        protected override PlayKind Kind => PlayKind.Haul;

        protected override int DefaultCount => 3;

        private string Mode => to == PlayHaul.Treeline || to == PlayHaul.Graves || to == PlayHaul.Trench || to == PlayHaul.Crematorium ? to : PlayHaul.Pyre;

        public override string Unavailable(DilemmaContext ctx)
        {
            if (Mode == PlayHaul.Crematorium && PlayCremation.Find(ctx?.map) == null) return "CC_Haul_NoCrematorium".Translate().Resolve();
            return null;
        }

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (!base.Fill(p, ctx)) return false;
            var dead = PlayHaul.EnemyDead(ctx.map, Mathf.Clamp(most, 1, OnMapMath.MaxDead));
            if (dead.Count == 0) return false;
            p.things = dead;
            p.mode = Mode;
            if (p.mode == PlayHaul.Crematorium)
            {
                p.place = PlayCremation.Find(ctx.map);
                if (p.place == null) return false;
            }
            if (PlayHaul.Burial(p) && !PlayBurial.CanDig(ctx.map)) return false;
            p.count = Math.Min(p.count, PlayGroups.Cap(PlayKind.Haul));
            return true;
        }

        protected override string DefaultLabel(DilemmaContext ctx) => ("CC_Play_Label_" + PlayHaul.Key(Mode)).Translate().Resolve();
    }

    /// <summary>The bodies' way out: which bodies, where they go, who carries which.</summary>
    public static class PlayHaul
    {
        public const string Pyre = "pyre", Treeline = "treeline", Graves = "graves", Trench = "trench", Crematorium = "crematorium";

        /// <summary>The mode's part of its text keys (CC_Play_Label_Pyre, CC_Play_Where_Graves...).</summary>
        public static string Key(string mode) =>
            mode == Treeline ? "Treeline" : mode == Graves ? "DeadGraves" : mode == Trench ? "Trench" : mode == Crematorium ? "Crematorium" : "Pyre";

        /// <summary>The dead go into graves dug for them.</summary>
        public static bool Burial(PlayOut p) => p.mode == Graves || p.mode == Trench;

        /// <summary>The body of someone who came against the colony, lying on the open map (not buried, not in the fog).</summary>
        public static bool EnemyCorpse(Thing t)
        {
            if (!(t is Corpse c) || c.Destroyed || !c.Spawned || c.Map == null) return false;
            var x = c.InnerPawn;
            if (x?.RaceProps == null || !x.RaceProps.Humanlike) return false;
            var f = x.Faction;
            if (f == Faction.OfPlayer || (f != null && !f.HostileTo(Faction.OfPlayer))) return false;
            return !c.Position.Fogged(c.Map);
        }

        /// <summary>The enemy dead on the map, nearest the middle of the colony first.</summary>
        public static List<Thing> EnemyDead(Map map, int most)
        {
            if (map == null) return new List<Thing>();
            var center = TemplatePlanner.ColonyCenter(map);
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Where(EnemyCorpse)
                .OrderBy(c => c.Position.DistanceToSquared(center)).Take(Math.Max(1, most)).ToList();
        }

        public static bool AtPile(PlayOut p, Thing c) => c != null && c.Spawned && c.Map == p.map && p.cell.IsValid && c.Position.InHorDistOf(p.cell, 3.9f);

        /// <summary>Where the answer meant this body to go, it went: at the pile, in one of the graves (burned bodies leave the list).</summary>
        public static bool Delivered(PlayOut p, Thing c) =>
            Burial(p) ? PlayBurial.Buried(p, c) : p.mode != Crematorium && AtPile(p, c);

        /// <summary>Still to see to: one of the dead, not yet where they're going.</summary>
        private static bool ToCarry(PlayOut p, Thing c) => EnemyCorpse(c) && !Delivered(p, c);

        /// <summary>On this plan's work: carrying one of its dead (or digging one of its graves) with the game's own jobs.</summary>
        public static bool OnIt(PlayOut p, Pawn x)
        {
            if (Burial(p)) return PlayBurial.OnIt(p, x);
            if (p.mode == Crematorium) return PlayCremation.OnIt(p, x);
            var j = x?.CurJob;
            return j != null && j.def == JobDefOf.HaulToCell && j.targetA.Thing != null && p.things.Contains(j.targetA.Thing);
        }

        /// <summary>Where it all goes, and the first ones set off. False (with a reason) when it can't start yet.</summary>
        public static bool Start(PlayOutTracker t, PlayOut p, out string where)
        {
            where = null;
            var lead = p.hands[0].pawn;
            p.things.RemoveAll(c => !EnemyCorpse(c));
            if (p.things.Count == 0)
            {
                t.Fallback(p, "CC_Play_Why_NoDead".Translate(), false);
                return false;
            }
            if (p.mode == Crematorium)
            {
                // The crematorium the answer meant, or another of the colony's that can burn now; none: it waits.
                if (!PlayCremation.Usable(p.place)) p.place = PlayCremation.Find(p.map);
                if (p.place == null)
                {
                    p.waitReason = "CC_Haul_NoCrematorium".Translate();
                    return false;
                }
            }
            else if (Burial(p))
            {
                if (p.plots.Count == 0)
                {
                    var plots = PlayBurial.Plot(p.map, lead, p.things.Count, p.mode == Graves, out var rot);
                    // No room for a grave each with ground between: side by side, as for a trench.
                    if (plots.Count == 0 && p.mode == Graves) plots = PlayBurial.Plot(p.map, lead, p.things.Count, false, out rot);
                    p.plots = plots;
                    p.plotRot = rot.AsInt;
                }
                if (p.plots.Count == 0)
                {
                    p.waitReason = "CC_Play_Why_Place".Translate();
                    return false;
                }
                PlayBurial.Lay(p);
            }
            else if (!p.cell.IsValid) p.cell = p.mode == Treeline ? TreelineCell(p.map, lead) : PyreCell(p.map, lead);
            if (!Burial(p) && p.mode != Crematorium && !p.cell.IsValid)
            {
                p.waitReason = "CC_Play_Why_Place".Translate();
                return false;
            }
            where = ("CC_Play_Where_" + Key(p.mode)).Translate();
            foreach (var h in p.hands) Give(p, h);
            if (p.hands.Any(h => OnIt(p, h.pawn))) return true;
            p.waitReason = "CC_Play_Why_Place".Translate();
            return false;
        }

        /// <summary>The next thing for this one to do. False when there's nothing they can get to.</summary>
        public static bool Give(PlayOut p, PlayHand h)
        {
            if (Burial(p)) return PlayBurial.Give(p, h);
            if (p.mode == Crematorium) return PlayCremation.Give(p, h);
            var x = h?.pawn;
            if (x == null || !x.Spawned || x.Map != p.map) return false;
            var others = p.hands.Where(o => o != h && !o.left && OnIt(p, o.pawn)).ToList();
            var taken = new HashSet<Thing>(others.Select(o => o.pawn.CurJob.targetA.Thing));
            var cells = new HashSet<IntVec3>(others.Select(o => o.cell));
            var corpse = p.things.Where(c => ToCarry(p, c) && !taken.Contains(c) && x.CanReserveAndReach(c, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(c => c.Position.DistanceToSquared(x.Position)).FirstOrDefault();
            if (corpse == null) return false;
            var drop = DropCell(p, x, cells);
            if (!drop.IsValid) return false;
            var job = JobMaker.MakeJob(JobDefOf.HaulToCell, corpse, drop);
            job.count = 1;
            job.haulMode = HaulMode.ToCellNonStorage;
            job.haulOpportunisticDuplicates = false;
            x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            if (x.CurJob != job) return false;
            h.phase = p.things.IndexOf(corpse);
            h.cell = drop;
            return true;
        }

        /// <summary>
        /// A free place at the pile for the next body, apart from where the others are taking theirs. A pyre's middle is
        /// kept clear for its fire: the dead are laid round it.
        /// </summary>
        private static IntVec3 DropCell(PlayOut p, Pawn x, HashSet<IntVec3> taken)
        {
            var map = p.map;
            foreach (var c in GenRadial.RadialCellsAround(p.cell, 3.5f, true))
            {
                if (p.mode == Pyre && c == p.cell) continue;
                if (!c.InBounds(map) || taken.Contains(c) || !c.Standable(map) || c.GetEdifice(map) != null || c.GetFirstItem(map) != null) continue;
                if (c.GetTerrain(map)?.IsWater ?? false) continue;
                if (!x.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                return c;
            }
            return IntVec3.Invalid;
        }

        /// <summary>Counts what this one did last, if it came to pass (a body burned is counted as it burns).</summary>
        public static void Settle(PlayOut p, PlayHand h)
        {
            if (h == null) return;
            if (Burial(p))
            {
                PlayBurial.Settle(p, h);
                return;
            }
            if (h.phase >= 0 && p.mode != Crematorium && h.phase < p.things.Count && AtPile(p, p.things[h.phase])) h.done++;
            h.phase = -1;
        }

        /// <summary>Under way: each takes the next body as they set the last one down, until none are left or the time is up.</summary>
        public static void Step(PlayOutTracker t, PlayOut p, int elapsed)
        {
            bool timeUp = elapsed >= p.durationTicks;
            int on = 0;
            bool noneFor = false;
            foreach (var h in p.hands)
            {
                if (h.left) continue;
                var x = h.pawn;
                if (OnIt(p, x))
                {
                    on++;
                    h.ticks += 250;
                    continue;
                }
                Settle(p, h);
                if (timeUp || !PlayGroups.Free(t, p, x)) continue;
                if (Give(p, h)) on++;
                else noneFor = true;
            }
            if (on > 0 && Rand.Chance(0.06f)) PlayWords.During(p, p.hands.Where(h => !h.left && OnIt(p, h.pawn)).Select(h => h.pawn).RandomElementWithFallback());
            bool allOut = !p.things.Any(c => ToCarry(p, c));
            if (on == 0 && (timeUp || allOut || (noneFor && elapsed > PlayOutMath.TicksPerHour / 4))) PlayGroups.Finish(t, p, false, null);
        }

        /// <summary>How many of the dead went where they were meant to (the crematorium counts as each is burned).</summary>
        public static void Count(PlayOut p)
        {
            if (p.mode == Crematorium) return;
            p.tally = p.things.Count(c => Delivered(p, c));
        }

        /// <summary>
        /// It's over and it happened. At a pile, the dead are left be (forbidden, so nobody carries them back to a dump),
        /// and for a pyre the fire is laid for dusk (OnMapTimed.cs), to be lit by the first of those who carried them;
        /// the answer's outcome goes with it, told once the pyre has burned down rather than now, hours before it's lit.
        /// A burial's leftover frames are taken up when nobody is left to bury.
        /// </summary>
        public static void Finished(PlayOut p)
        {
            if (Burial(p))
            {
                p.tally = p.things.Count(c => PlayBurial.Buried(p, c));
                PlayBurial.Done(p);
                return;
            }
            if (p.mode == Crematorium) return;
            var piled = p.things.Where(c => AtPile(p, c)).ToList();
            p.tally = piled.Count;
            foreach (var c in piled) c.SetForbidden(true, false);
            if (p.mode != Pyre || piled.Count == 0 || OnMapTimed.Instance == null) return;
            var by = p.hands.Where(h => h.done > 0).Select(h => h.pawn).FirstOrDefault(x => x != null && !x.Dead);
            OnMapTimed.Instance.AddPyre(p.map, p.cell, piled, p.outcome, by);
            p.outcome = null;
        }

        /// <summary>
        /// Open ground out past the gate, nearest first: along the line from the middle of the colony out through the
        /// gate, then all round the gate (the middle of the colony with no gate). Where a pyre goes, and graves.
        /// </summary>
        public static IEnumerable<IntVec3> OutsideCells(Map map, Pawn pawn)
        {
            if (map == null) yield break;
            var gate = PlaySpots.GateCell(map, pawn);
            var center = TemplatePlanner.ColonyCenter(map);
            var from = gate.IsValid ? gate : center;
            int dx = Math.Sign(from.x - center.x), dz = Math.Sign(from.z - center.z);
            if (gate.IsValid && (dx != 0 || dz != 0))
                for (int k = 4; k <= 16; k++)
                {
                    var c = new IntVec3(from.x + dx * k, 0, from.z + dz * k);
                    if (!c.InBounds(map)) break;
                    yield return c;
                }
            foreach (var c in GenRadial.RadialCellsAround(from, 20f, false))
                if (c.InBounds(map)) yield return c;
        }

        /// <summary>
        /// Where a pyre is built: out past the gate, on open ground off the home area with room round it, where a fire
        /// can be laid. Else the nearest such ground beyond the home area.
        /// </summary>
        public static IntVec3 PyreCell(Map map, Pawn pawn)
        {
            foreach (var c in OutsideCells(map, pawn))
                if (PyreGround(c, map, pawn)) return c;
            return IntVec3.Invalid;
        }

        private static bool PyreGround(IntVec3 c, Map map, Pawn pawn)
        {
            if (!c.InBounds(map) || c.Roofed(map) || c.Fogged(map) || !c.Standable(map) || c.GetEdifice(map) != null || c.GetZone(map) != null) return false;
            if (map.areaManager.Home[c] || (c.GetTerrain(map)?.IsWater ?? false)) return false;
            if (GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)).Count(x => x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null) < 6) return false;
            var fire = ThingDefOf.Campfire;
            if (fire != null && !GenConstruct.CanPlaceBlueprintAt(fire, c, Rot4.North, map).Accepted) return false;
            return PlaySpots.Ok(c, map, pawn);
        }

        /// <summary>
        /// The tree line: the nearest place off the home area, well out from the middle of the colony, with a few trees
        /// close together. No trees anywhere near: out past the gate, as for a pyre.
        /// </summary>
        public static IntVec3 TreelineCell(Map map, Pawn pawn)
        {
            if (map == null) return IntVec3.Invalid;
            var center = TemplatePlanner.ColonyCenter(map);
            var home = map.areaManager.Home;
            // Out from fifteen steps to fifty, nearest first.
            int from = GenRadial.NumCellsInRadius(15f), to = GenRadial.NumCellsInRadius(Mathf.Min(50f, GenRadial.MaxRadialPatternRadius - 1f));
            for (int i = from; i < to; i++)
            {
                var c = center + GenRadial.RadialPattern[i];
                if (!c.InBounds(map) || home[c] || c.Fogged(map)) continue;
                if (!(c.GetPlant(map) is Plant tree) || !tree.def.plant.IsTree) continue;
                int trees = 0;
                foreach (var x in GenRadial.RadialCellsAround(c, 2.9f, true))
                    if (x.InBounds(map) && x.GetPlant(map) is Plant t2 && t2.def.plant.IsTree && ++trees >= 3) break;
                if (trees < 3) continue;
                foreach (var x in GenAdj.CellsAdjacent8Way(new TargetInfo(c, map)))
                    if (x.InBounds(map) && !home[x] && x.Standable(map) && x.GetEdifice(map) == null && x.GetFirstItem(map) == null
                        && !(x.GetTerrain(map)?.IsWater ?? false) && PlaySpots.Ok(x, map, pawn))
                        return x;
            }
            return PyreCell(map, pawn);
        }
    }

    /// <summary>
    /// A watch kept on the move (DilemmaEffect_Vigil with patrol): each watcher walks the colony's edge, from one
    /// look-out to the next round the home ground, stopping a while at each to look out; kept until dawn when the answer
    /// says so. Drops out like any vigil: at danger, drafted, hungry or tired.
    /// </summary>
    public static class PlayVigils
    {
        /// <summary>A vigil kept until first light (PlayOut.mode).</summary>
        public const string Dawn = "dawn";

        /// <summary>How long a watcher stops at each look-out.</summary>
        public const int LookOut = 500;

        /// <summary>
        /// The round of look-outs: going out from the middle of the colony in eight directions, the last place on the
        /// home ground that can be reached safely, in order round the compass, starting from the watcher's own.
        /// </summary>
        public static List<IntVec3> Route(Map map, Pawn pawn, int watcher, int watchers)
        {
            var points = new List<IntVec3>();
            if (map == null) return points;
            var center = TemplatePlanner.ColonyCenter(map);
            var home = map.areaManager.Home;
            for (int k = 0; k < 8; k++)
            {
                float a = (k * 45f + 22.5f) * Mathf.Deg2Rad;
                // The edge of the home ground this way (cheap), then back in to the first place that can be reached.
                var ray = new List<IntVec3>();
                for (int step = 4; step <= 60; step++)
                {
                    var c = new IntVec3(center.x + Mathf.RoundToInt(Mathf.Cos(a) * step), 0, center.z + Mathf.RoundToInt(Mathf.Sin(a) * step));
                    if (!c.InBounds(map)) break;
                    if (!(home?[c] ?? false) && step > 10) break;
                    if (c.Standable(map) && c.GetEdifice(map) == null && !(c.GetTerrain(map)?.IsWater ?? false) && !c.Fogged(map)) ray.Add(c);
                }
                for (int i = ray.Count - 1; i >= 0 && i >= ray.Count - 8; i--)
                    if (PlaySpots.Ok(ray[i], map, pawn))
                    {
                        if (!points.Any(x => x.InHorDistOf(ray[i], 5f))) points.Add(ray[i]);
                        break;
                    }
            }
            if (points.Count == 0) return points;
            int start = watchers <= 1 ? 0 : watcher * points.Count / watchers;
            return points.Skip(start).Concat(points.Take(start)).ToList();
        }

        public static bool Where(PlayOut p, PlayHand h, out IntVec3 cell, out LocalTargetInfo face)
        {
            cell = IntVec3.Invalid;
            face = LocalTargetInfo.Invalid;
            if (h.route.Count == 0) return false;
            cell = h.route[h.done % h.route.Count];
            // Looking outward, away from the middle of the colony.
            var center = TemplatePlanner.ColonyCenter(p.map);
            var out3 = new IntVec3(cell.x + Math.Sign(cell.x - center.x) * 3, 0, cell.z + Math.Sign(cell.z - center.z) * 3);
            face = out3.InBounds(p.map) && out3 != cell ? new LocalTargetInfo(out3) : LocalTargetInfo.Invalid;
            return true;
        }

        public static void Arrived(PlayOut p, PlayHand h, int delta)
        {
            h.phase = 0;
            h.waited += delta;
            if (h.waited < LookOut) return;
            h.waited = 0;
            h.done++;
        }

        /// <summary>A look-out they can't get to safely is passed by; when none can be reached, they've done what they can.</summary>
        public static void CantReach(PlayHand h)
        {
            h.waited = 0;
            h.done++;
            h.phase = h.phase < 0 ? 1 : h.phase + 1;
            if (h.phase >= Math.Max(1, h.route.Count)) h.left = true;
        }
    }

    /// <summary>
    /// What follows a plan once it's over, whether or not it came about (PlayOut.afterward): a secret said out loud at
    /// the end of the gathering called to hear it (or, if nobody gathered, to whoever is near, as before).
    /// </summary>
    public static class PlayAfter
    {
        public const string Confess = "confess";

        public static void Run(PlayOut p)
        {
            if (p == null || p.afterward.NullOrEmpty()) return;
            string what = p.afterward;
            p.afterward = null;
            try
            {
                if (what == Confess)
                {
                    var owner = p.named.FirstOrDefault();
                    var comp = CouncilGameComponent.Instance;
                    var s = owner != null && !owner.Dead ? comp?.secrets.Of(owner) : null;
                    // together: said to whoever is near the moment the gathering ends (Together.WhenInCompany inside).
                    if (s != null && !s.revealed) comp.secrets.ConfessInCompany(comp, s);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("play out afterward " + what, e);
            }
        }
    }
}
