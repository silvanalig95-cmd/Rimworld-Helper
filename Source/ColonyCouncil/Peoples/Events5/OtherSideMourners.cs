using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    // ===================== 36. Mourners from the other side (the Quiet Bell) =====================

    /// <summary>
    /// Mourners from the other side (item 36): a season after the Quiet Bell buried raiders the colony killed in graves beyond
    /// its wall (their bell for the dead, the first answer), two mourners of the raiders' people come under the Bell's escort
    /// to stand at their graves: a bell-keeper walks in with them, and a people at war with the colony comes as the Bell's
    /// guests, as a parley's envoy does (Diplomacy/TruceParley.cs), so neither side raises a hand. Let through, they stand an
    /// hour at the graves while the bell is rung; their people warm a little, more with one of the colony standing with them,
    /// and a people at war may then offer a truce by their own letter (Diplomacy/Truces.cs). Turned away, the Bell is saddened.
    /// </summary>
    public static class OtherSideMourners
    {
        public const string Kind = "pev:mourn";
        public const string EventDef = "Rimfolk_Evt_OtherSideMourners";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The Bell buried the raiders beyond the wall (HappeningWorker_BellForTheDead, the first answer): their peoples' mourners are due a season on.</summary>
        public static void Note(HappeningContext ctx)
        {
            var b = PeopleEvents5Book.Instance;
            if (b == null || ctx?.map == null || ctx.faction == null) return;
            foreach (var (f, n) in DilemmaEffect_BellForTheDead.Peoples(ctx.map))
                b.AddMourn(new MournRecord { people = f, bell = ctx.faction, map = ctx.map, buried = n, due = Now + PeopleEvents5Math.MournAfterDays * GenDate.TicksPerDay });
        }

        /// <summary>The colony's graves (dug by the Bell beyond its wall) holding the dead of this people.</summary>
        public static List<Building_Grave> Graves(Map map, Faction f) =>
            map == null || f == null ? new List<Building_Grave>()
                : map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Where(g => g.Spawned && g.Corpse?.InnerPawn?.Faction == f).ToList();

        /// <summary>A people that may come to its dead: still in the world, one whose goodwill can move, never the colony's enemy for good.</summary>
        public static bool MayCome(Faction f) => f != null && !f.IsPlayer && !f.defeated && !f.Hidden && f.HasGoodwill && f.def.humanlikeFaction && !f.def.permanentEnemy
                                                 && Arrivals.PersonKind(f) != null;

        /// <summary>The Bell can bring them: at peace with the colony, and not at war with the mourners' people.</summary>
        public static Faction Escort(Faction people) => PeoplesD.Friendly(PeoplesMath.Sextons) is Faction bell && (people == null || !bell.HostileTo(people)) ? bell : null;

        /// <summary>The mourners due at this home now: their day come, their graves still there.</summary>
        public static MournRecord Due(Map map)
        {
            var b = PeopleEvents5Book.Instance;
            int now = Now;
            return b?.mourns.FirstOrDefault(r => r != null && !r.done && r.map == map && now >= r.due && MayCome(r.people) && Escort(r.people) != null && Graves(map, r.people).Count > 0);
        }

        /// <summary>The events' hour: a record past its time let go; one due, the mourners come (once the colony can be asked).</summary>
        public static void Watch(int now)
        {
            var b = PeopleEvents5Book.Instance;
            if (b == null) return;
            foreach (var r in b.mourns.Where(r => r != null && !r.done))
                if (r.map == null || !Homes.IsHome(r.map) || !MayCome(r.people) || now > r.due + PeopleEvents5Math.MournExpireDays * GenDate.TicksPerDay
                    || now >= r.due && Graves(r.map, r.people).Count == 0)
                    r.done = true;
            foreach (var map in Homes.All)
            {
                var r = Due(map);
                if (r == null || MapFacts.ThreatOn(map) || !Tell.CanAsk) continue;
                if (PeopleEvents2.Fire(EventDef, map, Escort(r.people))) r.done = true;
            }
        }

        /// <summary>Dev mode: the mourners of a people buried here come within the hour (a record made for the first people whose dead lie in the colony's graves).</summary>
        public static bool Soon(Map map)
        {
            var b = PeopleEvents5Book.Instance;
            if (b == null || map == null) return false;
            var r = b.mourns.FirstOrDefault(x => x != null && !x.done && x.map == map);
            if (r == null)
            {
                var f = map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().Select(g => g.Corpse?.InnerPawn?.Faction).FirstOrDefault(MayCome);
                var bell = Escort(f);
                if (f == null || bell == null) return false;
                b.AddMourn(r = new MournRecord { people = f, bell = bell, map = map, buried = Graves(map, f).Count });
            }
            r.due = Math.Min(r.due < 0 ? Now : r.due, Now);
            return true;
        }

        /// <summary>
        /// The bell-keeper and the mourners walk in from the edge to the gate: the mourners of a people at war with the colony
        /// as the Bell's guests while they are here (so neither side raises a hand), their own lord beside the bell-keeper's.
        /// </summary>
        public static bool Arrive(HappeningContext ctx, float hours)
        {
            var map = ctx?.map;
            var keeper = ctx?.stranger;
            var mourners = ctx?.group.Where(p => p != null).ToList();
            var people = ctx?.enemy;
            if (map == null || keeper == null || mourners.NullOrEmpty() || people == null || ctx.faction == null) return false;
            if (!Arrivals.TryEntryCell(map, out var entry)) return false;
            var spot = PeoplesD.GateSpot(map) ?? entry;
            try
            {
                foreach (var p in mourners.Prepend(keeper))
                    if (!Arrivals.Spawn(p, map, entry)) return false;
                // Vouched for by the Bell: its guests while they are here, as a parley's envoy is (TruceParley.Send).
                if (people.HostileTo(Faction.OfPlayer))
                    foreach (var p in mourners)
                        p.guest?.SetGuestStatus(ctx.faction);
                int ticks = Mathf.RoundToInt(hours * GenDate.TicksPerHour);
                LordMaker.MakeNewLord(ctx.faction, new LordJob_VisitOnErrand(ctx.faction, spot, ticks), map, new List<Pawn> { keeper });
                LordMaker.MakeNewLord(people, new LordJob_VisitOnErrand(people, spot, ticks), map, mourners);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: the mourners' arrival", e);
                return false;
            }
        }

        /// <summary>Let through: everyone walks to the graves, the visit waiting for them there; <paramref name="with"/>, one of the colony walks out to stand with them.</summary>
        public static void Through(HappeningContext ctx, bool with)
        {
            var map = ctx?.map;
            var grave = ctx?.place as Building_Grave;
            var t = CouncilGameComponent.Instance?.happenings;
            if (map == null || grave == null || !grave.Spawned || t == null) return;
            var all = PeoplesG.Party(ctx).Where(p => p.Spawned && !p.Dead && !p.Downed).ToList();
            if (all.Count == 0) return;
            var v = t.AddVisit(Kind, ctx.incident, map, ctx.stranger, with ? ctx.a : null, ctx.group.ToList(), Now + 10 * GenDate.TicksPerHour);
            v.place = grave;
            v.due = -1;
            v.text = ctx.enemy?.GetUniqueLoadID() ?? "";
            foreach (var g in all.GroupBy(p => p.Faction))
            {
                Lords.Leave(g.ToList());
                LordMaker.MakeNewLord(g.Key, new LordJob_VisitOnErrand(g.Key, grave.Position, 10 * GenDate.TicksPerHour), map, g.ToList());
            }
        }

        /// <summary>Turned away: the Bell is saddened, and everyone goes.</summary>
        public static void TurnedAway(HappeningContext ctx)
        {
            var bell = ctx?.faction;
            if (bell != null && bell.HasGoodwill && !bell.IsPlayer && !bell.HostileTo(Faction.OfPlayer)) WorldSituationWorker.Goodwill(bell, PeopleEvents5Math.MournTurnedBellGoodwill, GoodwillWhy.TurnedAway);
            PeoplesG.Home(PeoplesG.Party(ctx));
        }

        private static Faction PeopleOf(Visit v) => Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.GetUniqueLoadID() == v.text);

        /// <summary>
        /// The visit's hour: the mourners reach the graves (the bell rung, the colonist sent out to them), stand an hour, then
        /// their people warm, perhaps a truce is talked of, and everyone goes.
        /// </summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var grave = v.place;
            var mourners = v.group.Where(p => PeoplesG.Up(p, v.map)).ToList();
            if (grave == null || !grave.Spawned || mourners.Count == 0 || now > v.until)
            {
                Finish(v, false);
                return false;
            }
            if (v.due < 0)
            {
                if (!mourners.Any(p => p.Position.InHorDistOf(grave.Position, 6f))) return true;
                v.due = now;
                PeopleEvents5.Bell(v.map, grave.Position);
                if (PeoplesG.Up(v.x, v.map)) PeoplesG.SayLine(v.x, v.incident, "ring");
                if (v.a != null && PeoplesG.Up(v.a, v.map)) PeopleWorkOn.Bring(v.a, new LocalTargetInfo(grave), "beside", (PeopleEvents5Math.MournHours + 1) * GenDate.TicksPerHour);
                return true;
            }
            if (now < v.due + PeopleEvents5Math.MournHours * GenDate.TicksPerHour) return true;
            Finish(v, true);
            return false;
        }

        private static void Finish(Visit v, bool stood)
        {
            v.done = true;
            var people = PeopleOf(v);
            bool with = v.a != null && PeoplesG.Up(v.a, v.map);
            PeopleWorkOn.Release(v.a);
            if (stood && people != null && MayCome(people))
            {
                WorldSituationWorker.Goodwill(people, with ? PeopleEvents5Math.MournWithGoodwill : PeopleEvents5Math.MournGoodwill, GoodwillWhy.TheirDead);
                if (with) KinMemory.Note(people, "mourned", v.a);
                else FactionMemory.Note(people, "mourned");
                var mourner = v.group.FirstOrDefault(p => PeoplesG.Up(p, v.map));
                // together: the mourner speaks at the grave, the colonist (if any) standing beside them.
                if (mourner != null) PeoplesG.SayLine(mourner, v.incident, with ? "thanks" : "done");
                Tell.Message(NewsKind.World, Importance.Notable, v.map, "CC_PEv5_Mourned".Translate(people.Name.Named("FACTION")).Resolve(),
                    v.place != null && v.place.Spawned ? new LookTargets(v.place) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                // At war with the colony: a truce may be talked of, their own letter offering it.
                if (people.HostileTo(Faction.OfPlayer) && Truces.On && !Truces.Permanent(people) && Truces.Of(people, v.map) == null
                    && Rand.Chance(PeopleEvents5Math.TruceChance(with)))
                    Truces.Offer(people, v.map, PactMath.TheirPrice(Rand.Value));
            }
            PeoplesG.Home(v.group.Prepend(v.x));
        }
    }

    /// <summary>
    /// A bell-keeper of {FACTION} ({X}) at the gate with two mourners of {ENEMY}, whose dead the Bell buried beyond the colony's
    /// wall a season ago ({COUNT} graves).
    /// </summary>
    public class HappeningWorker_OtherSideMourners : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Sextons) != null && OtherSideMourners.Due(map) != null && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var r = OtherSideMourners.Due(ctx.map);
            ctx.faction = OtherSideMourners.Escort(r?.people);
            if (r == null || ctx.faction == null) return false;
            ctx.enemy = r.people;
            var graves = OtherSideMourners.Graves(ctx.map, r.people);
            ctx.place = graves.OrderBy(g => g.Position.DistanceToSquared(PeoplesD.GateSpot(ctx.map) ?? g.Position)).FirstOrDefault();
            ctx.count = graves.Count;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_QuietBell_Keeper"), ctx.faction, ext.ages);
            if (ctx.stranger == null || ctx.place == null) return false;
            for (int i = 0; i < PeopleEvents5Math.MournersCount; i++)
            {
                var m = Arrivals.Make(Arrivals.PersonKind(r.people), r.people, new FloatRange(30f, 70f));
                if (m != null) ctx.group.Add(m);
            }
            if (ctx.group.Count == 0) return false;
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Social) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) => OtherSideMourners.Arrive(ctx, ext.stayHours.RandomInRange);

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.enemy != null && ctx.enemy.HostileTo(Faction.OfPlayer)) tags.Add("war");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && (ctx.place == null || ctx.place.Destroyed || !ctx.place.Spawned) ? "CC_PEv5_GravesGone".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option <= 1 ? "through" : "turned");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The mourners let through to the graves (OtherSideMourners.Through), <see cref="with"/> one of the colony standing with them.</summary>
    public class DilemmaEffect_MournersThrough : DilemmaEffect
    {
        public bool with;

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.enemy == null) return;
            sheet.Play((with ? "CC_PEv5_Impact_MournWith" : "CC_PEv5_Impact_Mourn").Translate(hc.a?.LabelShort ?? "?").Resolve(), "CC_PEv5_Impact_MournBrief".Translate().Resolve());
            sheet.Goodwill(hc.enemy, with ? PeopleEvents5Math.MournWithGoodwill : PeopleEvents5Math.MournGoodwill);
            if (hc.enemy.HostileTo(Faction.OfPlayer) && Truces.On && !Truces.Permanent(hc.enemy))
                sheet.Add(ImpactKind.Other, 0, "CC_PEv5_Impact_Truce".Translate(hc.enemy.Name).Resolve(), "CC_PEv5_Impact_TruceBrief".Translate().Resolve(), "pev5truce");
        }

        public override void Apply(DilemmaContext ctx) => OtherSideMourners.Through(ctx as HappeningContext, with);
    }

    /// <summary>The mourners turned away at the gate: the Bell saddened (OtherSideMourners.TurnedAway).</summary>
    public class DilemmaEffect_MournersTurned : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.faction != null) sheet.Goodwill(ctx.faction, PeopleEvents5Math.MournTurnedBellGoodwill);
        }

        public override void Apply(DilemmaContext ctx) => OtherSideMourners.TurnedAway(ctx as HappeningContext);
    }
}
