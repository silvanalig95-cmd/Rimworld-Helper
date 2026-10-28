using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Someone who asked to move on and was kept (Social/MovingOn.cs; the numbers are MovingOnMath.cs): why, how much
    /// they wanted to go, since when, what the council owes them (a room promised), whether they've asked a second
    /// time, and a relative on the way here. Saved on the council (CouncilGameComponent.movingOnHeld).
    /// </summary>
    public class HeldBack : IExposable
    {
        public Pawn pawn;
        /// <summary>"family", "love" or "room".</summary>
        public string kind;
        /// <summary>0 to 1: how much they wanted to go.</summary>
        public float strength;
        public int since;
        public bool promised;
        public bool askedAgain;
        /// <summary>The relative or partner they wanted to go to (none for a room), by name too, and their people.</summary>
        public Pawn other;
        public string otherName;
        public Faction faction;
        /// <summary>A relative sent for, on the way here: when they arrive (-1: nobody coming).</summary>
        public int comingAt = -1;

        public float Days => (Find.TickManager.TicksGame - since) / (float)GenDate.TicksPerDay;

        public bool PromiseOpen => MovingOnMath.PromiseOpen(promised, Days);

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref strength, "strength");
            Scribe_Values.Look(ref since, "since");
            Scribe_Values.Look(ref promised, "promised");
            Scribe_Values.Look(ref askedAgain, "askedAgain");
            Scribe_References.Look(ref other, "other");
            Scribe_Values.Look(ref otherName, "otherName");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref comingAt, "comingAt", -1);
        }
    }

    /// <summary>
    /// Keeping someone who wanted to move on, always at a price (MovingOnMath.cs). Kept, they lose trust in the council by
    /// how much they wanted to go, and carry a longing (Council_Leave_Long*) that grows over the first weeks and lasts
    /// until what they wanted comes to them: their family here, their partner joining the colony, a room of their own.
    /// After a season they make their peace with it. A promised room has to be theirs within a fortnight, or the promise
    /// is broken and costs trust again. Unhappy or wanting it badly, they ask a second time (Council_DlmMove_Again*), and
    /// keeping them then costs more: a relative sent for (silver and goodwill), a partner asked to join (goodwill),
    /// someone's room given up, or the council's word alone and a bitter memory. Nothing here with "movingOn" off.
    /// </summary>
    public static class MovingOnHeld
    {
        private static List<HeldBack> List => CouncilGameComponent.Instance?.movingOnHeld;

        public static HeldBack For(Pawn p, string kind = null) => p == null ? null : List?.FirstOrDefault(h => h.pawn == p && (kind == null || h.kind == kind));

        private static int Now => Find.TickManager.TicksGame;

        private static ThoughtDef T(string name) => DefDatabase<ThoughtDef>.GetNamedSilentFail(name);

        /// <summary>Kept, or kept again: their record (one per person and reason).</summary>
        public static HeldBack Hold(Pawn p, string kind, float strength, Pawn other, string otherName, Faction faction, bool promised)
        {
            var list = List;
            if (list == null || p == null) return null;
            var h = For(p, kind);
            if (h == null) list.Add(h = new HeldBack { pawn = p, kind = kind });
            h.strength = Mathf.Clamp01(strength);
            h.since = Now;
            h.promised = promised;
            h.other = other;
            h.otherName = otherName;
            h.faction = faction;
            h.comingAt = -1;
            return h;
        }

        /// <summary>Someone of theirs who can be sent for: alive in the world, of a people at peace with the colony, free to come.</summary>
        public static bool CanCome(Pawn x, Faction f)
        {
            try
            {
                return x != null && !x.Dead && !x.Discarded && !x.Spawned && x.MapHeld == null && Find.WorldPawns.Contains(x) && x.GetCaravan() == null
                       && f != null && x.Faction == f && MovingOn.Friendly(f) && f.leader != x && !x.IsPrisoner && !x.IsSlave
                       && !QuestUtility.IsReservedByQuestOrQuestBeingGenerated(x);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A bed of their own, alone in a room.</summary>
        public static bool HasOwnRoom(Pawn p) => p?.ownership?.Bedroom != null;

        /// <summary>Due to ask a second time (a partner only while they're here on a visit).</summary>
        public static bool Due(HeldBack h)
        {
            var p = h?.pawn;
            if (p == null || h.comingAt >= 0 || !MovingOn.Could(p) || !MovingOn.Spare(p.Map)) return false;
            if (h.kind == "love" && (h.other == null || !h.other.Spawned || h.other.Map != p.Map)) return false;
            return MovingOnMath.AskAgain(h.Days, h.kind == "love", p.needs?.mood?.CurLevelPercentage ?? 0.5f, h.strength, h.askedAgain, h.PromiseOpen);
        }

        /// <summary>Once a day (CouncilDynamics): what they wanted came, a promise fell due, peace made; a second asking.</summary>
        public static void Daily(CouncilGameComponent comp)
        {
            var list = comp?.movingOnHeld;
            if (list == null || list.Count == 0) return;
            foreach (var h in list.ToList())
            {
                try
                {
                    Day(list, h);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("kept from moving on", e);
                    list.Remove(h);
                }
            }
            if (MovingOn.On && list.Any(h => h.kind != "love" && Due(h)))
                comp.dilemmas.TryFire(true, d => d.Worker is DilemmaWorker_AskAgain w && w.Kind != "love");
        }

        /// <summary>Each hour (MovingOn.Hourly): a relative sent for arrives by day; a partner back on a visit, for a second asking.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            var list = comp?.movingOnHeld;
            if (list == null || list.Count == 0) return;
            foreach (var h in list.Where(h => h.comingAt >= 0 && Now >= h.comingAt).ToList())
            {
                var map = h.pawn?.MapHeld;
                if (map == null || !map.IsPlayerHome || GenLocalDate.HourOfDay(map) < 8 || GenLocalDate.HourOfDay(map) > 16) continue;
                try
                {
                    Arrive(list, h, map);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a relative sent for", e);
                    list.Remove(h);
                }
            }
            if (MovingOn.On && list.Any(h => h.kind == "love" && Due(h)))
                comp.dilemmas.TryFire(true, d => d.Worker is DilemmaWorker_AskAgain w && w.Kind == "love");
        }

        private static void Day(List<HeldBack> list, HeldBack h)
        {
            var p = h.pawn;
            if (p == null || p.Dead || !p.IsColonist)
            {
                if (h.comingAt >= 0) Arrivals.Release(h.other);
                list.Remove(h);
                return;
            }
            if (h.comingAt >= 0) return;
            if (Resolved(h))
            {
                list.Remove(h);
                return;
            }
            if (MovingOnMath.PromiseBroken(h.promised, h.Days))
            {
                // apart: a broken promise, told to the colony; the room was the colony's to build.
                h.promised = false;
                CouncilGameComponent.Instance?.standings.Change(p, -MovingOnMath.BrokenPromiseTrust, "CC_Log_Leave_PromiseBroken".Translate().Resolve());
                Messages.Message("CC_Leave_PromiseBroken".Translate(p.Named("PAWN")).Resolve(), new LookTargets(p), MessageTypeDefOf.NegativeEvent, false);
                return;
            }
            if (MovingOnMath.MadePeace(h.Days))
            {
                list.Remove(h);
                Messages.Message(("CC_Leave_Peace_" + h.kind).Translate(p.Named("PAWN"), (h.otherName ?? "").Named("OTHER")).Resolve(), new LookTargets(p), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        /// <summary>What they wanted came to them, or can't any more: a room of their own, their family or partner here, or gone.</summary>
        private static bool Resolved(HeldBack h)
        {
            // apart: what they wanted has come (a room, their partner one of the colony now); the gladness of it, not a meeting.
            var p = h.pawn;
            switch (h.kind)
            {
                case "room":
                    if (!HasOwnRoom(p)) return false;
                    bool kept = h.PromiseOpen;
                    CouncilGameComponent.GiveThought(p, T(kept ? "Council_Leave_PromiseKept" : "Council_Leave_RoomAtLast"), null, -1f, null);
                    if (kept) CouncilGameComponent.Instance?.standings.Change(p, MovingOnMath.KeptPromiseTrust, "CC_Log_Leave_PromiseKept".Translate().Resolve());
                    return true;
                case "love":
                    var x = h.other;
                    if (x == null || x.Dead || !LovePartnerRelationUtility.LovePartnerRelationExists(p, x)) return true;
                    if (!x.IsColonist) return false;
                    CouncilGameComponent.GiveThought(p, T("Council_Leave_TogetherHere"), x, -1f, null);
                    return true;
                default:
                    var r = h.other;
                    if (r == null || r.Dead) return true;
                    return r.IsColonist;
            }
        }

        /// <summary>The relative sent for walks in from the edge, joins the colony, and the one who asked goes to meet them.</summary>
        private static void Arrive(List<HeldBack> list, HeldBack h, Map map)
        {
            list.Remove(h);
            var p = h.pawn;
            var r = h.other;
            if (p == null || p.Dead || !p.IsColonist) return;
            if (!CanCome(r, h.faction) || !Arrivals.Spawn(r, map))
            {
                // Kept after all, as if asked to stay: they can't come now.
                Arrivals.Release(r);
                Messages.Message("CC_Leave_CouldntCome".Translate(p.Named("PAWN"), (h.otherName ?? "?").Named("OTHER")).Resolve(), new LookTargets(p), MessageTypeDefOf.NegativeEvent, false);
                Hold(p, "family", h.strength, r, h.otherName, h.faction, false).askedAgain = true;
                return;
            }
            RecruitUtility.Recruit(r, Faction.OfPlayer, p);
            string relation = Family.Relation(p, r);
            // They walk in from the edge, and the one who asked goes to meet them (a visit, PlayOut/).
            Find.LetterStack.ReceiveLetter("CC_Leave_RelativeCame_Label".Translate(r.Named("OTHER")).Resolve(),
                "CC_Leave_RelativeCame".Translate(r.Named("OTHER"), p.Named("PAWN"), relation.Named("RELATION"), (h.faction?.Name ?? "").Named("FACTION")).Resolve(),
                LetterDefOf.PositiveEvent, new LookTargets(r));
            var play = PlayOuts.On && p.Spawned && p.Map == map
                ? new DilemmaEffect_Visit
                {
                    who = PlayWho.A, to = PlaySpot.Person, whom = PlayWho.B, label = "CC_Leave_MeetLabel".Translate(r.Named("OTHER")).Resolve(), hours = new IntRange(0, 23),
                    duration = new FloatRange(1f, 1.5f), waitHours = 6f
                }.Begin(new DilemmaContext { map = map, a = p, b = r })
                : null;
            string cause = "CC_Leave_RelativeCameCause".Translate(r.Named("OTHER")).Resolve();
            var came = T("Council_Leave_FamilyCame");
            if (play != null && came != null) play.moods.Add(new HeldMood { pawn = p, thought = came, about = r, cause = cause });
            else CouncilGameComponent.GiveThought(p, came, r, -1f, cause);
        }

        /// <summary>The longing's stage for a thought worker, or -1 for none.</summary>
        public static int Stage(Pawn p, string kind)
        {
            if (!MovingOn.On) return -1;
            var h = For(p, kind);
            if (h == null || h.comingAt >= 0) return -1;
            return MovingOnMath.Stage(h.strength, h.Days, h.PromiseOpen);
        }
    }

    /// <summary>Kept from moving on (MovingOnHeld): a longing in three stages, growing over the first weeks.</summary>
    public class ThoughtWorker_HeldBack : ThoughtWorker
    {
        protected virtual string Kind => "family";

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            int stage = MovingOnHeld.Stage(p, Kind);
            return stage < 0 ? ThoughtState.Inactive : ThoughtState.ActiveAtStage(Mathf.Min(stage, def.stages.Count - 1));
        }
    }

    public class ThoughtWorker_HeldBackLove : ThoughtWorker_HeldBack
    {
        protected override string Kind => "love";
    }

    public class ThoughtWorker_HeldBackRoom : ThoughtWorker_HeldBack
    {
        protected override string Kind => "room";
    }

    /// <summary>
    /// The council keeps A (MovingOnHeld): their record, and trust lost by how much they wanted to go. <see cref="share"/>
    /// of their wish (less when the colony sent silver instead); <see cref="promised"/> a room promised; <see cref="again"/>
    /// the second asking, kept on the same record.
    /// </summary>
    public class DilemmaEffect_HoldBack : DilemmaEffect
    {
        public string kind = "family";
        public float trust;
        public float share = 1f;
        public bool promised;
        public bool again;

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's word; what it costs them shows in their mood and their trust.
            var p = ctx?.a;
            var m = ctx as MovingOnContext;
            if (p == null || p.Dead) return;
            float strength = (m?.strength ?? 0.5f) * share;
            HeldBack h = again ? MovingOnHeld.For(p, kind) : null;
            if (h == null)
                h = MovingOnHeld.Hold(p, kind, strength, kind == "family" ? m?.relative : kind == "love" ? ctx.stranger : null, m?.relativeName, ctx.faction, promised);
            if (h == null) return;
            if (again)
            {
                h.askedAgain = true;
                strength = h.strength;
            }
            if (trust > 0f)
            {
                string why = ctx.def != null ? "CC_Log_Dilemma".Translate(ctx.def.label).ToString() : ctx.why;
                CouncilGameComponent.Instance?.standings.Change(p, -MovingOnMath.TrustLoss(trust, strength), why);
            }
        }
    }

    /// <summary>
    /// The colony sends for A's relative (MovingOnContext.relative): they come in a few days and join the colony. Not
    /// while they can't come (MovingOnHeld.CanCome).
    /// </summary>
    public class DilemmaEffect_BringRelative : DilemmaEffect
    {
        public IntRange days = new IntRange(2, 4);

        public override string Unavailable(DilemmaContext ctx)
        {
            var m = ctx as MovingOnContext;
            return MovingOnHeld.CanCome(m?.relative, ctx.faction) ? null : "CC_Leave_CantCome".Translate((m?.relativeName ?? "?").Named("OTHER")).Resolve();
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: word and silver sent; the relative walks in and joins later (MovingOnHeld.Arrive), with a letter.
            var m = ctx as MovingOnContext;
            var p = ctx?.a;
            if (p == null || m == null || !MovingOnHeld.CanCome(m.relative, ctx.faction)) return;
            var h = MovingOnHeld.Hold(p, "family", m.strength, m.relative, m.relativeName, ctx.faction, false);
            if (h == null) return;
            h.askedAgain = true;
            h.comingAt = Find.TickManager.TicksGame + days.RandomInRange * GenDate.TicksPerDay;
            Arrivals.Hold(m.relative);
        }
    }

    /// <summary>
    /// B gives A their room: A takes B's bed, alone in its room, and B takes A's bed, or none if A had none (B sleeps
    /// where there is room). Not if B has no room of their own any more.
    /// </summary>
    public class DilemmaEffect_GiveRoom : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.b == null || ctx.b.Dead || !MovingOnHeld.HasOwnRoom(ctx.b) ? "CC_Leave_NoRoomToGive".Translate().Resolve() : null;

        public override void Apply(DilemmaContext ctx)
        {
            var a = ctx?.a;
            var b = ctx?.b;
            if (a == null || b == null || a.Dead || b.Dead || !MovingOnHeld.HasOwnRoom(b)) return;
            try
            {
                var bedB = b.ownership.OwnedBed;
                var bedA = a.ownership?.OwnedBed;
                if (bedB == null || bedB.Medical || bedB.OwnersForReading.Count != 1) return;
                a.ownership.UnclaimBed();
                b.ownership.UnclaimBed();
                a.ownership.ClaimBedIfNonMedical(bedB);
                if (bedA != null && bedA != bedB) b.ownership.ClaimBedIfNonMedical(bedA);
                // apart: the council's ruling on whose bed is whose; it shows on the beds, and they sleep in them from tonight.
                Messages.Message("CC_Leave_GaveRoom".Translate(a.Named("PAWN"), b.Named("OTHER")).Resolve(), new LookTargets(bedB), MessageTypeDefOf.NeutralEvent, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a room given up", e);
            }
        }
    }

    /// <summary>
    /// The second asking (Council_DlmMove_Again*): someone kept who is still unhappy, or wanted it badly, comes back to
    /// the council. Family: the relative may be sent for ({COUNT} silver). Love: while the partner is here again. Room:
    /// the colonist with a room of their own who thinks best of A may give it up (B); "broken" when a promised room never came.
    /// </summary>
    public class DilemmaWorker_AskAgain : DilemmaWorker
    {
        public virtual string Kind => "family";

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!MovingOn.On || !CanHappenNow(map)) return false;
            var h = CouncilGameComponent.Instance?.movingOnHeld?.Where(x => x.kind == Kind && x.pawn != null && x.pawn.Map == map && MovingOnHeld.Due(x))
                .OrderByDescending(x => x.strength).FirstOrDefault();
            if (h == null) return false;
            var p = h.pawn;
            var m = new MovingOnContext
            {
                def = def, map = map, a = p, faction = h.faction, relativeName = h.otherName, relation = h.other != null ? Family.Relation(p, h.other) : "",
                kind = Kind, strength = h.strength, relative = Kind == "family" ? h.other : null, count = def.thingCount, broken = h.promised
            };
            if (Kind == "love") m.stranger = h.other;
            if (Kind == "room")
                m.b = map.mapPawns.FreeColonistsSpawned.Where(o => o != p && !o.IsQuestLodger() && MovingOnHeld.HasOwnRoom(o) && !LovePartnerRelationUtility.LovePartnerRelationExists(o, p))
                    .OrderByDescending(o => o.relations?.OpinionOf(p) ?? 0).FirstOrDefault();
            h.askedAgain = true;
            ctx = m;
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (!(ctx is MovingOnContext m) || ctx.a == null) return;
            if (m.broken) tags.Add("broken");
            if (Kind == "family" && m.relative != null) tags.Add(m.relative.DevelopmentalStage.Juvenile() ? "child" : "old");
            if (Kind == "love" && ctx.stranger != null && ctx.a.relations != null)
            {
                if (Strangers.IsHospitalityGuest(ctx.stranger)) tags.Add("guest");
                if (ctx.a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, ctx.stranger)) tags.Add("married");
            }
            if (Kind == "room" && ctx.b == null) tags.Add("noroom");
        }
    }

    public class DilemmaWorker_AskAgainLove : DilemmaWorker_AskAgain
    {
        public override string Kind => "love";
    }

    public class DilemmaWorker_AskAgainRoom : DilemmaWorker_AskAgain
    {
        public override string Kind => "room";
    }
}
