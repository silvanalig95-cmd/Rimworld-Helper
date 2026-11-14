using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 7. The sick room (the hospitaller order) =====================

    /// <summary>
    /// The sick room (item 7): when a sickness is spreading in the colony (one of Rimfolk's serious illnesses that passes from
    /// bed to bed, AilmentInfo.spreadPerHour), a sister of the order comes and asks for a sick room. The sick are carried
    /// there (or walk), and for three days she doses them on rounds while the well stay out: the sickness spreads at a third
    /// of its rate (AilmentTracker.Spread reads <see cref="SpreadFactor"/>). She may catch it herself, at the physician's
    /// chance through the harm budget and the illness budget, and then the colony nurses her in one of its beds until she is
    /// well. Herbs and boiled linen in the early ages; the colony's medicine and masks from the industrial age.
    /// </summary>
    public static class SickRoom
    {
        public const string Kind = "pev:sickroom";

        private static int Now => Find.TickManager.TicksGame;

        public static HediffDef Nursed => DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_OrderNursed");

        /// <summary>The colonist's spreading sickness (a serious one, showing), or null.</summary>
        public static Hediff Spreading(Pawn p)
        {
            var hs = p?.health?.hediffSet?.hediffs;
            if (hs == null) return null;
            for (int i = 0; i < hs.Count; i++)
            {
                var h = hs[i];
                if (h?.def == null || !h.Visible) continue;
                var info = AilmentTracker.InfoOf(h.def);
                if (info != null && info.serious && info.spreadPerHour > 0f) return h;
            }
            return null;
        }

        /// <summary>The colony's people at home with a spreading sickness, the sickest first.</summary>
        public static List<Pawn> Sick(Map map) =>
            map == null ? new List<Pawn>() : map.mapPawns.FreeColonistsSpawned.Where(p => Spreading(p) != null).OrderByDescending(p => Spreading(p).Severity).Take(PeopleEvents3Math.SickRoomMax).ToList();

        /// <summary>The colony's medical beds in a room (not for prisoners or slaves).</summary>
        public static List<Building_Bed> Beds(Room room) =>
            room == null ? new List<Building_Bed>() : room.ContainedBeds.Where(b => b != null && b.Medical && b.Faction == Faction.OfPlayer && !b.ForPrisoners && !b.ForSlaves).ToList();

        /// <summary>A room to be the sick room: the colony's hospital, else the room with the most of its medical beds (two at least).</summary>
        public static Room RoomFor(Map map)
        {
            if (map == null) return null;
            var rooms = map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().Where(b => b.Medical && !b.ForPrisoners && !b.ForSlaves).Select(b => b.GetRoom())
                .Where(r => r != null && !r.PsychologicallyOutdoors).Distinct().ToList();
            return rooms.OrderByDescending(r => r.Role == RoomRoleDefOf.Hospital).ThenByDescending(r => Beds(r).Count).FirstOrDefault(r => r.Role == RoomRoleDefOf.Hospital || Beds(r).Count >= 2);
        }

        /// <summary>The sick room's room, by the bed cell the visit keeps.</summary>
        public static Room RoomOf(Visit v) => v?.map == null || !v.cell.IsValid ? null : v.cell.GetRoom(v.map);

        /// <summary>The sickness spreads at a third of its rate in a home with a sick room standing.</summary>
        public static float SpreadFactor(Pawn p)
        {
            var b = PeopleEvents3Book.Instance;
            var map = p?.MapHeld;
            if (b == null || map == null || b.sickRoomMap != map.uniqueID || Now > b.sickRoomUntil) return 1f;
            return PeopleEvents3Math.SickRoomSpread;
        }

        public static bool Running(Map map)
        {
            var b = PeopleEvents3Book.Instance;
            return b != null && map != null && b.sickRoomMap == map.uniqueID && Now <= b.sickRoomUntil;
        }

        /// <summary>The sick room set going: the sister keeps it three days.</summary>
        public static Visit Begin(HappeningContext ctx)
        {
            var t = CouncilGameComponent.Instance?.happenings;
            var room = RoomFor(ctx?.map);
            var x = ctx?.stranger;
            var b = PeopleEvents3Book.Instance;
            if (t == null || room == null || x == null || !x.Spawned || b == null) return null;
            var bed = Beds(room).FirstOrDefault();
            if (bed == null) return null;
            int until = Now + PeopleEvents3Math.SickRoomDays * GenDate.TicksPerDay;
            var v = t.AddVisit(Kind, ctx.incident, ctx.map, x, null, null, until);
            v.cell = bed.Position;
            v.text = ctx.faction.GetUniqueLoadID();
            b.sickRoomMap = ctx.map.uniqueID;
            b.sickRoomUntil = until;
            try
            {
                Hosting.Provision(x, PeopleEvents3Math.SickRoomDays + 1f);
                // Her stay stretched to the room's days, in it.
                x.GetLord()?.RemovePawn(x);
                Arrivals.Visit(new List<Pawn> { x }, ctx.map, ctx.faction, bed.Position, PeopleEvents3Math.SickRoomDays * 24f + 4f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the sister's stay", e);
            }
            Gather(v);
            return v;
        }

        /// <summary>The sick to the room: the downed carried by one of the well, the others sent to its beds.</summary>
        private static void Gather(Visit v)
        {
            var map = v.map;
            var room = RoomOf(v);
            if (room == null) return;
            var free = Beds(room).Where(b => b.AnyUnoccupiedSleepingSlot).ToList();
            foreach (var p in Sick(map))
            {
                if (free.Count == 0) return;
                var cur = p.CurrentBed();
                if (cur != null && cur.GetRoom() == room) continue;
                var bed = free[0];
                try
                {
                    if (p.Downed)
                    {
                        var carrier = PeoplesD.Able(map).Where(c => c != p && Spreading(c) == null && !c.Drafted && !c.InMentalState && c.CanReserveAndReach(p, PathEndMode.OnCell, Danger.Some))
                            .OrderBy(c => c.Position.DistanceToSquared(p.Position)).FirstOrDefault();
                        if (carrier == null) continue;
                        var job = JobMaker.MakeJob(JobDefOf.Rescue, p, bed);
                        job.count = 1;
                        carrier.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                    }
                    else if (!p.Drafted && !p.InMentalState && p.CurJobDef != JobDefOf.Ingest && p.CurJobDef != JobDefOf.LayDown)
                    {
                        if (!RestUtility.CanUseBedNow(bed, p, false, true)) continue;
                        p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobCondition.InterruptForced, null, false, true);
                    }
                    free.Remove(bed);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 3: the sick to the sick room", e);
                    return;
                }
            }
        }

        /// <summary>The sick room's hour: the sick gathered, her rounds, her own chance of catching it, and the end.</summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var map = v.map;
            var x = v.x;
            var f = PeoplesF.PeopleOf(v);
            var b = PeopleEvents3Book.Instance;
            if (x == null || x.Dead || x.Discarded || map == null || RoomOf(v) == null)
            {
                End(v, false);
                return false;
            }
            int hour = GenLocalDate.HourOfDay(map);
            var ill = Spreading(x) ?? (x.health?.hediffSet?.hediffs.FirstOrDefault(h => h.def == IllOf(v)));
            // Ill herself: the colony nurses her in one of its beds until she is well.
            if (ill != null && !ill.FullyImmune())
            {
                Nurse(v, x);
                v.until = Mathf.Max(v.until, now + GenDate.TicksPerDay);
                return true;
            }
            if (x.HostFaction == Faction.OfPlayer)
            {
                // Well again.
                End(v, true);
                return false;
            }
            if (now <= (b?.sickRoomUntil ?? now))
            {
                if (hour % 3 == 0) Gather(v);
                if (PeopleEvents3Math.RoundHour(hour) && x.Spawned && !x.Downed) Round(v, x);
                if (hour == 13) Catch(v, x);
                if ((hour >= 23 || hour <= 4) && x.Spawned && !x.Downed && (x.needs?.rest?.CurLevelPercentage ?? 1f) < 0.35f && x.CurJobDef != JobDefOf.LayDown) RestBy(v, x);
                return true;
            }
            End(v, true);
            return false;
        }

        /// <summary>The illness she would catch: the commonest among the sick she sees to.</summary>
        private static HediffDef IllOf(Visit v)
        {
            if (v.text != null && v.text.Contains("|") && DefDatabase<HediffDef>.GetNamedSilentFail(v.text.Split('|')[1]) is HediffDef d) return d;
            return Sick(v.map).Select(p => Spreading(p)?.def).Where(d => d != null).GroupBy(d => d).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key;
        }

        /// <summary>Her round: bed to bed with the order's care (round 1's night nurse's), the sickest first.</summary>
        private static void Round(Visit v, Pawn x)
        {
            var nursed = Nursed;
            var room = RoomOf(v);
            var who = Sick(v.map).Where(p => p.CurrentBed()?.GetRoom() == room).ToList();
            if (nursed == null || who.Count == 0) return;
            PeoplesG.SayLine(x, v.incident, "round");
            string text = "CC_PEv3_SickRound".Translate(x.Named("SISTER"), who.Count.Named("COUNT")).Resolve();
            if (!VisitorServices.Dose(v.map, x, who, nursed, text))
                foreach (var p in who)
                    if (!p.health.hediffSet.HasHediff(nursed))
                        p.health.AddHediff(nursed);
        }

        /// <summary>A day of rounds: she may catch it (the physician's chance, through the harm and illness budgets), shown.</summary>
        private static void Catch(Visit v, Pawn x)
        {
            var def = IllOf(v);
            if (def == null || !Harm.On || !Harm.SystemOn(HarmKinds.Physician.system)) return;
            if (!Harm.Roll(x, HarmKinds.Physician, PeopleEvents3Math.SisterCatch) || !IllnessBudget.Allows(v.map, def, x)) return;
            if (Harm.Ail(x, def, 0.12f, HarmKinds.Physician) == null) return;
            IllnessBudget.Owe(v.map, def, x);
            v.text = (v.text ?? "").Split('|')[0] + "|" + def.defName;
            PeoplesG.SayLine(x, v.incident, "caught");
            Tell.Message(NewsKind.Health, Importance.Important, v.map, "CC_PEv3_SisterCaught".Translate(x.Named("SISTER"), def.label.Named("ILLNESS")).Resolve(),
                new LookTargets(x), MessageTypeDefOf.NegativeHealthEvent);
        }

        /// <summary>Ill: one of the colony's guests now, in one of its medical beds, tended by its doctors as the game tends a guest.</summary>
        private static void Nurse(Visit v, Pawn x)
        {
            if (!x.Spawned) return;
            try
            {
                if (x.HostFaction != Faction.OfPlayer)
                {
                    x.GetLord()?.RemovePawn(x);
                    x.guest?.SetGuestStatus(Faction.OfPlayer);
                }
                if (x.InBed() || x.CurJobDef == JobDefOf.LayDown) return;
                var bed = Beds(RoomOf(v)).FirstOrDefault(b => RestUtility.CanUseBedNow(b, x, false, true)) ?? RestUtility.FindPatientBedFor(x);
                if (bed != null) x.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown, bed), JobCondition.InterruptForced, null, false, true);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the sister nursed", e);
            }
        }

        /// <summary>A few hours' sleep by the door of the room, on her blanket.</summary>
        private static void RestBy(Visit v, Pawn x)
        {
            var map = v.map;
            if (!CellFinder.TryFindRandomCellNear(v.cell, map, 3, c => c.Standable(map) && c.GetRoom(map) == RoomOf(v) && c.GetFirstPawn(map) == null && c.GetEdifice(map) == null, out var cell)) return;
            var job = JobMaker.MakeJob(JobDefOf.LayDown, cell);
            job.forceSleep = true;
            x.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        private static void End(Visit v, bool thanks)
        {
            v.done = true;
            var b = PeopleEvents3Book.Instance;
            if (b != null && b.sickRoomMap == v.map?.uniqueID)
            {
                b.sickRoomMap = -1;
                b.sickRoomUntil = -1;
            }
            var x = v.x;
            var f = PeoplesF.PeopleOf(v);
            if (x == null || x.Dead || !x.Spawned) return;
            try
            {
                if (x.HostFaction == Faction.OfPlayer) x.guest?.SetGuestStatus(null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 3: the sister well again", e);
            }
            if (thanks)
            {
                if (f != null && f.HasGoodwill && !f.IsPlayer) WorldSituationWorker.Goodwill(f, PeopleEvents3Math.SickRoomGoodwill, false);
                FactionMemory.Note(f, "relief", "CC_PEv3_SickRoomDeed".Translate().Resolve());
                PeoplesG.SayLine(x, v.incident, "goes");
                Tell.Message(NewsKind.Health, Importance.Notable, v.map, "CC_PEv3_SickRoomOver".Translate(x.Named("SISTER"), (f?.Name ?? "?").Named("FACTION")).Resolve(),
                    new LookTargets(x), MessageTypeDefOf.PositiveEvent);
            }
            Arrivals.SendAway(new[] { x });
        }

        /// <summary>Dev mode: the sick room's days are over now.</summary>
        public static bool EndNow()
        {
            var b = PeopleEvents3Book.Instance;
            if (b == null || b.sickRoomUntil < 0) return false;
            b.sickRoomUntil = Now;
            return true;
        }
    }

    /// <summary>
    /// A sister of {FACTION} at the gate: a sickness is spreading in the colony ({TALE}: {A} and {COUNT} in all), and she asks
    /// for a sick room ({PLACE}) to keep them in for three days while the well stay out. {B} is the colony's best doctor.
    /// </summary>
    public class HappeningWorker_SickRoom : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PeoplesD.Friendly(PeoplesMath.Healers) != null && SickRoom.Sick(map).Count > 0 && SickRoom.RoomFor(map) != null && !SickRoom.Running(map) && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Healers);
            var sick = SickRoom.Sick(ctx.map);
            var room = SickRoom.RoomFor(ctx.map);
            if (ctx.faction == null || sick.Count == 0 || room == null) return false;
            ctx.a = sick[0];
            ctx.count = sick.Count;
            ctx.tale = SickRoom.Spreading(ctx.a)?.LabelBase ?? "";
            ctx.placeName = room.Role?.label ?? "CC_PEv3_TheRoom".Translate().Resolve();
            ctx.b = PeoplesD.Best(ctx.map, SkillDefOf.Medicine, p => SickRoom.Spreading(p) == null);
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Order_Healer"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Medicine, new IntRange(10, 16));
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(PeopleEvents.AgeOf(ctx.faction) >= 2 ? "masks" : "herbs");
            if (ctx.count > 1) tags.Add("many");
            if (ctx.b == null) tags.Add("nodoctor");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (SickRoom.RoomFor(ctx.map) == null) return "CC_PEv3_NoSickRoom".Translate().Resolve();
            if (option == 1 && (ctx.b == null || ctx.b.Dead || !ctx.b.Spawned || SickRoom.Spreading(ctx.b) != null)) return "CC_PEv3_NoDoctor".Translate().Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "room" : "thanks");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The sick room given: three days of her rounds (SickRoom.Begin); from the industrial age she takes the colony's medicine too.</summary>
    public class DilemmaEffect_SickRoom : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            sheet.Play("CC_PEv3_Impact_SickRoom".Translate(hc.placeName ?? "?", SickRoom.Sick(hc.map).Count, PeopleEvents3Math.SickRoomDays).Resolve(), "CC_PEv3_Impact_SickRoomBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Health, 1, "CC_PEv3_Impact_Spread".Translate().Resolve(), "CC_PEv3_Impact_SpreadBrief".Translate().Resolve(), "pev3spread");
            if (Harm.On && hc.stranger != null)
                sheet.Add(ImpactKind.Health, -1, "CC_PEv3_Impact_SisterRisk".Translate(hc.stranger.LabelShort, Mathf.RoundToInt(Harm.Chance(HarmKinds.Physician, PeopleEvents3Math.SisterCatch) * 100f * PeopleEvents3Math.SickRoomDays)).Resolve(),
                    "CC_PEv3_Impact_SisterRiskBrief".Translate().Resolve(), "pev3sisterrisk");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents3Math.SickRoomGoodwill);
            if (PeopleEvents.AgeOf(hc.faction) >= 2 && PlayCarry.Have(hc.map, "medicine", null) > 0)
                sheet.Goods(-1, PlayCarry.Label("medicine", null), Mathf.Min(PeopleEvents3Math.SickRoomMedicine, PlayCarry.Have(hc.map, "medicine", null)), "medicine");
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.map == null) return;
            if (SickRoom.Begin(hc) == null || PeopleEvents.AgeOf(hc.faction) < 2) return;
            // From the industrial age she works with the colony's own medicine, carried to her.
            int have = PlayCarry.Have(hc.map, "medicine", null);
            if (have <= 0) return;
            var carry = new DilemmaEffect_CarryOut
            {
                what = "medicine", count = Mathf.Min(PeopleEvents3Math.SickRoomMedicine, have), to = PlaySpot.Stranger, who = PlayWho.None, steward = false,
                label = "CC_PEv3_SickRoomMedicine".Translate().Resolve()
            };
            if (carry.Unavailable(hc) == null) carry.Apply(hc);
        }
    }
}
