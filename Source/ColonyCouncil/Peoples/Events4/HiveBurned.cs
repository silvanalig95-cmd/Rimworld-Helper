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
    // ===================== 33. The hive burned (the Lantern Wardens) =====================

    /// <summary>
    /// The hive burned (item 33): when insects dig in near the colony (the game's hives, or Vanilla Factions Expanded -
    /// Insectoids 2's), Wardens come with fire and ask to go in beside the colony's fighters. Together, they wait at the edge
    /// a while for the colony's drafted fighters to come up, then go in; alone, they go in at once while the colony watches;
    /// refused, they keep watch at the edge for a day in case, then go. The fight is the game's own (its assault on the
    /// hives, the insects defending them). Their fire is their age's: torches held to the hives, molotovs, an incendiary
    /// launcher.
    /// </summary>
    public static class HiveBurned
    {
        public const string Kind = "pev:hive";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The hives on the map the colony knows of (seen), wild, the game's and the mod's by name.</summary>
        public static List<Thing> Hives(Map map)
        {
            var list = new List<Thing>();
            if (map == null) return list;
            foreach (var n in PeopleEvents4Math.HiveDefs)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(n);
                if (def == null) continue;
                list.AddRange(map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && t.Faction != Faction.OfPlayer && !t.Position.Fogged(map)));
            }
            return list;
        }

        /// <summary>The insects about the hives, up.</summary>
        public static List<Pawn> Insects(Map map, IntVec3 near) =>
            map?.mapPawns.AllPawnsSpawned.Where(p => p != null && !p.Dead && !p.Downed && p.RaceProps.Insect && p.Faction != Faction.OfPlayer && p.Position.InHorDistOf(near, 25f)).ToList() ?? new List<Pawn>();

        /// <summary>Their fire by their age: each given the molotovs or the launcher of it; with torches, they keep their own arms.</summary>
        public static void Arm(List<Pawn> wardens, int age)
        {
            string weapon = PeopleEvents4Math.FireWeapon(age);
            var def = weapon == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(weapon);
            if (def == null) return;
            for (int i = 0; i < wardens.Count; i++)
            {
                var p = wardens[i];
                // With the launcher, one carries it and the others keep their own arms.
                if (p?.equipment == null || (age >= 3 && i > 0)) continue;
                try
                {
                    var eq = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null) as ThingWithComps;
                    if (eq == null) continue;
                    p.equipment.DestroyAllEquipment();
                    p.equipment.AddEquipment(eq);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 4: the Wardens' fire", e);
                }
            }
        }

        /// <summary>
        /// The answer: <paramref name="how"/> "together" (they wait at the edge for the colony's fighters, then go in), "alone"
        /// (they go in now), "watch" (they keep watch at the edge, then go).
        /// </summary>
        public static Visit Begin(HappeningContext ctx, string how)
        {
            var map = ctx?.map;
            var t = CouncilGameComponent.Instance?.happenings;
            var party = PeoplesG.Party(ctx).Where(p => PeoplesG.Up(p, map)).ToList();
            var hives = Hives(map);
            if (t == null || map == null || party.Count == 0 || ctx.faction == null || hives.Count == 0) return null;
            int hours = how == "watch" ? PeopleEvents4Math.HiveWatchHours : PeopleEvents4Math.HiveDays * 24;
            var v = t.AddVisit(Kind, ctx.incident, map, party[0], null, party.Skip(1).ToList(), Now + hours * GenDate.TicksPerHour);
            v.place = hives.OrderBy(h => h.Position.DistanceToSquared(party[0].Position)).First();
            v.text = ctx.faction.GetUniqueLoadID() + "|" + how + "|" + PeopleEvents4Math.Fire(PeopleEvents.AgeOf(ctx.faction));
            v.due = how == "alone" ? Now : Now + PeopleEvents4Math.HiveWaitHours * GenDate.TicksPerHour;
            v.cell = ctx.spot;
            if (how == "alone") GoIn(v, party);
            else
                try
                {
                    Lords.Leave(party);
                    LordMaker.MakeNewLord(ctx.faction, new LordJob_VisitOnErrand(ctx.faction, ctx.spot, hours * GenDate.TicksPerHour), map, party);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("peoples' events 4: the Wardens at the edge", e);
                }
            return v;
        }

        private static string Part(Visit v, int i) => (v.text ?? "").Split('|').ElementAtOrDefault(i) ?? "";

        private static List<Pawn> Wardens(Visit v) => v.group.Prepend(v.x).Where(p => p != null && p.Spawned && !p.Dead && !p.Downed && p.Map == v.map).ToList();

        /// <summary>In: the game's assault on the hives and the insects about them.</summary>
        private static void GoIn(Visit v, List<Pawn> wardens)
        {
            var hives = Hives(v.map);
            if (hives.Count == 0 || wardens.Count == 0) return;
            var targets = hives.Concat(Insects(v.map, hives[0].Position).Cast<Thing>()).ToList();
            try
            {
                Lords.Leave(wardens);
                LordMaker.MakeNewLord(wardens[0].Faction, new LordJob_AssaultThings(wardens[0].Faction, targets, 1f, false), v.map, wardens);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 4: the Wardens go in", e);
                return;
            }
            v.due = -1;
            PeoplesG.SayLine(wardens[0], v.incident, "in");
            Tell.Message(NewsKind.Land, Importance.Notable, v.map, "CC_PEv4_HiveIn".Translate((wardens[0].Faction?.Name ?? "?").Named("FACTION")).Resolve(), new LookTargets(wardens), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>
        /// The hive's hour: waiting at the edge (for the colony's drafted fighters by the hives, or the wait's end), in and at
        /// the hives (a torch held to one when a Warden stands by it), done when the hives are gone or the Wardens are.
        /// </summary>
        public static bool Hour(Visit v, int now)
        {
            if (v.done) return false;
            var wardens = Wardens(v);
            var hives = Hives(v.map);
            string how = Part(v, 1);
            if (wardens.Count == 0 || now > v.until || (hives.Count == 0 && how != "watch"))
            {
                Finish(v, wardens, hives.Count == 0);
                return false;
            }
            if (how == "watch") return true;
            if (v.due >= 0)
            {
                bool ours = hives.Count > 0 && v.map.mapPawns.FreeColonistsSpawned.Any(p => p.Drafted && p.Position.InHorDistOf(hives[0].Position, 15f));
                if (ours || now >= v.due) GoIn(v, wardens);
                return true;
            }
            // Torches: a Warden by a hive and not fighting holds the torch to it.
            if (Part(v, 2) != "torch") return true;
            foreach (var w in wardens.Where(p => p.CurJobDef != PeopleEvents4.BurnJob && (p.mindState?.enemyTarget == null)))
            {
                var hive = hives.Where(h => h.Position.InHorDistOf(w.Position, 8f)).OrderBy(h => h.Position.DistanceToSquared(w.Position)).FirstOrDefault();
                if (hive != null) PeopleEvents4.Send(w, PeopleEvents4.BurnJob, hive, PeopleEvents4Math.BurnTicks);
            }
            return true;
        }

        /// <summary>The torch held to a hive (the job done): it catches.</summary>
        public static void Burned(Pawn warden, Thing hive)
        {
            if (hive == null || hive.Destroyed || !hive.Spawned) return;
            FireUtility.TryStartFireIn(hive.Position, hive.Map, 0.6f, warden);
            if (Rand.Chance(0.4f)) PeoplesG.SayLine(warden, DefDatabase<IncidentDef>.GetNamedSilentFail("Rimfolk_Evt_HiveBurned"), "burn");
        }

        private static void Finish(Visit v, List<Pawn> wardens, bool burned)
        {
            v.done = true;
            var f = PeoplesF.PeopleOf(v);
            if (burned && f != null && f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer))
            {
                WorldSituationWorker.Goodwill(f, PeopleEvents4Math.HiveGoodwill, GoodwillWhy.Helped);
                FactionMemory.Note(f, "campHelped", "CC_PEv4_HiveName".Translate().Resolve());
                if (wardens.Count > 0) PeoplesG.SayLine(wardens[0], v.incident, "done");
                Tell.Message(NewsKind.Land, Importance.Notable, v.map, "CC_PEv4_HiveDone".Translate(f.Name.Named("FACTION")).Resolve(), wardens.Count > 0 ? new LookTargets(wardens) : LookTargets.Invalid,
                    MessageTypeDefOf.PositiveEvent);
            }
            PeoplesG.Home(wardens);
        }
    }

    /// <summary>Wardens of {FACTION} ({X}, {COUNT} of them) at the edge with fire ({TALE}), asking to go into the hive ({BEAST}) beside the colony's fighters; {A} its best shot.</summary>
    public class HappeningWorker_HiveBurned : HappeningWorker_Visitors
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => PeoplesD.Friendly(PeoplesMath.Hunters) != null && HiveBurned.Hives(map).Count > 0 && Colonists(map).Count > 0;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Hunters);
            var hives = HiveBurned.Hives(ctx.map);
            if (ctx.faction == null || hives.Count == 0) return false;
            int n = PeopleEvents4Math.HiveWardens(hives.Count);
            var party = new List<Pawn>();
            for (int i = 0; i < n; i++)
            {
                var p = Arrivals.Make(PeoplesD.FighterKind(ctx.faction), ctx.faction, ext.ages);
                if (p != null) party.Add(p);
            }
            if (party.Count == 0) return false;
            int age = PeopleEvents.AgeOf(ctx.faction);
            HiveBurned.Arm(party, age);
            ctx.stranger = party[0];
            ctx.group = party.Skip(1).ToList();
            ctx.count = party.Count;
            ctx.variant = hives.Count;
            ctx.beast = hives[0].LabelShort;
            ctx.tale = ("CC_PEv4_Fire_" + PeopleEvents4Math.Fire(age)).Translate().Resolve();
            ctx.place = hives[0];
            ctx.spot = LegendBeasts.EdgeToward(ctx.map, hives[0].Position);
            ctx.a = PeoplesD.Best(ctx.map, SkillDefOf.Shooting, p => !p.WorkTagIsDisabled(WorkTags.Violent)) ?? PeoplesD.Able(ctx.map).FirstOrDefault();
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return ctx.a != null && ctx.spot.IsValid;
        }

        /// <summary>They come in at the edge nearest the hive and wait there for the answer.</summary>
        protected override bool Begin(HappeningContext ctx, HappeningExtension ext)
        {
            var party = PeoplesG.Party(ctx);
            foreach (var p in party)
                if (!Arrivals.Spawn(p, ctx.map, ctx.spot)) return false;
            LordMaker.MakeNewLord(ctx.faction, new LordJob_VisitOnErrand(ctx.faction, ctx.spot, Mathf.RoundToInt(ext.stayHours.RandomInRange * GenDate.TicksPerHour)), ctx.map, party);
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            tags.Add(PeopleEvents4Math.Fire(PeopleEvents.AgeOf(ctx.faction)));
            if (ctx.variant > 1) tags.Add("hives");
        }

        public override string Unavailable(HappeningContext ctx, int option) =>
            option <= 1 && HiveBurned.Hives(ctx.map).Count == 0 ? "CC_PEv4_HivesGone".Translate().Resolve() : null;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "together" : option == 1 ? "alone" : "watch");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The Wardens' answer at the hive (HiveBurned.Begin): <see cref="how"/> "together", "alone", "watch".</summary>
    public class DilemmaEffect_HiveBurned : DilemmaEffect
    {
        public string how = "watch";

        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc == null) return;
            string key = how == "together" ? "CC_PEv4_Impact_HiveTogether" : how == "alone" ? "CC_PEv4_Impact_HiveAlone" : "CC_PEv4_Impact_HiveWatch";
            sheet.Play(key.Translate(hc.count, hc.beast ?? "?", PeopleEvents4Math.HiveWaitHours, PeopleEvents4Math.HiveWatchHours).Resolve(), "CC_PEv4_Impact_HiveBrief".Translate().Resolve());
            if (how != "watch")
            {
                sheet.Add(ImpactKind.Health, -1, "CC_PEv4_Impact_HiveFight".Translate().Resolve(), "CC_PEv4_Impact_HiveFightBrief".Translate().Resolve(), "pev4hivefight");
                if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents4Math.HiveGoodwill);
            }
        }

        public override void Apply(DilemmaContext ctx) => HiveBurned.Begin(ctx as HappeningContext, how);
    }
}
