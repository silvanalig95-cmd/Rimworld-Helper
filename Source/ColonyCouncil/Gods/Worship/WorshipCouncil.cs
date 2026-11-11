using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The priest and the council's questions about the gods (docs/design/gods.md, "Worship": a priest; brainstorm 16;
    /// Defs/GodDefs/Rimfolk_Worship_Dilemmas.xml): which god the colony honours this year, whether to raise a temple, a god
    /// asking for the colony's best beast or the rain stays away, and the colony caught between two rival gods. Each plays out
    /// on the map (words said at the god's place, an offering carried, a beast led away, the rain kept off) and moves the
    /// gods' favour through the core. Here too, the rain a god keeps away (the region's hour) and the festival's rite.
    /// </summary>
    public static class WorshipCouncil
    {
        public static void Hourly(WorshipBook b, Map map, int now)
        {
            Festivals.Hourly(b, map, now);
            DrySpell(b, map, now);
        }

        /// <summary>A god keeps the rain away for a while (a refusal): rain turns to clear skies at the region's hour until then.</summary>
        private static void DrySpell(WorshipBook b, Map map, int now)
        {
            var m = b.Mark(Gods.HomeKey(map), "dry");
            if (m == null || now >= m.until) return;
            var w = map.weatherManager?.curWeather;
            if (w == null || w.rainRate <= 0f) return;
            map.weatherManager.TransitionTo(WeatherDefOf.Clear);
            var g = Gods.ById(m.god);
            if (g != null && Interplay.Ready("worship-dry-told|" + Gods.HomeKey(map), 2f))
                Tell.Minor(NewsKind.Colony, Importance.Flavor, map, ("CC_Wsh_DryPass_" + Worship.Step).Translate(Gods.FullName(g).Named("GOD")).Resolve(), null);
        }

        /// <summary>The god the colony honours at a home for the year (the council's answer): marked, pleased, told.</summary>
        public static void Honour(God g, Map map)
        {
            var b = Worship.Book;
            if (b == null || g == null || map == null) return;
            b.SetMark(Gods.HomeKey(map), "honoured", g.id, Worship.Year(map), Worship.Now);
            Gods.AddFavour(g, map, WorshipMath.HonourFavour, "CC_Wsh_Why_Honoured".Translate(Worship.Year(map).Named("YEAR")).Resolve(), FavourSource.Rite);
        }
    }

    // ================= The context =================

    /// <summary>
    /// A council question about the gods: up to three gods ({GOD}, {GODEPITHET}, {GODOF} the first; {GOD2}, {GOD3}), the
    /// place of the first god ({PLACE}: "Ceren's chapel"), and the beast asked for (the context's pet, {PET_...}).
    /// </summary>
    public class GodDilemmaContext : DilemmaContext
    {
        public int god1 = -1, god2 = -1, god3 = -1;

        public God God1 => Gods.ById(god1);
        public God God2 => Gods.ById(god2);
        public God God3 => Gods.ById(god3);

        public God GodAt(int index) => index == 2 ? God2 : index == 3 ? God3 : God1;

        protected override void AddArgs(List<NamedArgument> args)
        {
            base.AddArgs(args);
            var g = God1;
            args.AddRange(g != null ? Gods.Args(g) : new[] { "".Named(GodsMath.God), "".Named(GodsMath.Epithet), "".Named(GodsMath.Of) });
            args.Add(Gods.Name(God2).Named("GOD2"));
            args.Add(Gods.Name(God3).Named("GOD3"));
            args.Add((g != null ? Worship.ShrineOf(g) : Worship.ShrineWord).Named("PLACE"));
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref god1, "god1", -1);
            Scribe_Values.Look(ref god2, "god2", -1);
            Scribe_Values.Look(ref god3, "god3", -1);
        }
    }

    // ================= The workers =================

    /// <summary>Who speaks for the gods: the priest, else the most devout grown colonist with a god (never a scoffer).</summary>
    public static class GodSpeaker
    {
        public static Pawn Of(Map map, DilemmaRole role, God only = null)
        {
            var priest = Worship.Priest(map);
            if (priest != null && priest.Map == map && (role == null || role.Allows(priest, null)) && (only == null || Worship.GodOf(priest) == only || Worship.IsPriest(priest)))
                return priest;
            return map.mapPawns.FreeColonistsSpawned.Where(p => Worship.Ours(p) && !p.DevelopmentalStage.Juvenile() && !Worship.Scoffer(p) && Worship.GodOf(p) != null
                                                                && (only == null || Worship.GodOf(p) == only) && (role == null || role.Allows(p, null)))
                .OrderByDescending(Worship.Devout).ThenBy(_ => Rand.Value).FirstOrDefault();
        }
    }

    /// <summary>Which god the colony honours this year: once a year, with two gods or more to choose from.</summary>
    public class DilemmaWorker_GodOfTheYear : DilemmaWorker_Gods
    {
        public override bool CanHappenNow(Map map) => Worship.On && base.CanHappenNow(map) && Worship.Honoured(map) == null && Choices(map).Count >= 2;

        public static List<God> Choices(Map map) => Festivals.GodsFor(map).Where(g => g.kind != GodKind.Dead).Take(3).ToList();

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            var gods = Choices(map);
            var a = GodSpeaker.Of(map, def.roleA);
            if (a == null) return false;
            ctx = new GodDilemmaContext { def = def, map = map, a = a, god1 = gods[0].id, god2 = gods[1].id, god3 = gods.Count > 2 ? gods[2].id : -1 };
            return true;
        }
    }

    /// <summary>Whether to raise a temple: a god of the colony's has a place but no temple, and the priest (or the devout) asks.</summary>
    public class DilemmaWorker_Temple : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Worship.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            foreach (var g in Festivals.GodsFor(map))
            {
                if (Shrines.Of(map, g).Count == 0 || Shrines.Temple(map, g) != null) continue;
                if (Worship.Book.vows.Any(v => v != null && !v.done && v.kind == VowKind.Temple && v.god == g.id)) continue;
                var a = GodSpeaker.Of(map, def.roleA, g);
                if (a == null) continue;
                ctx = new GodDilemmaContext { def = def, map = map, a = a, god1 = g.id };
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// A god of the rain asks for the colony's finest beast (a dream of the priest's or the devout's), or the rain stays
    /// away: a god of the harvest, the storm or the waters that takes offerings, and a tame beast of the colony's worth asking for.
    /// </summary>
    public class DilemmaWorker_GodAsks : DilemmaWorker_Gods
    {
        private static bool RainGod(God g) => g != null && WorshipMath.TakesOfferings(g.kind)
                                              && (g.domain == GodDomain.Harvest || g.domain == GodDomain.Storm || g.domain == GodDomain.Waters || g.second == GodDomain.Harvest || g.second == GodDomain.Storm);

        /// <summary>The colony's finest beast: the most valuable grown animal of its own, a mount first.</summary>
        public static Pawn Finest(Map map) => map.mapPawns.SpawnedColonyAnimals
            .Where(a => a?.RaceProps != null && a.RaceProps.Animal && !a.Dead && !a.Downed && a.ageTracker?.Adult == true && !a.health.hediffSet.HasHediff(HediffDefOf.Pregnant))
            .OrderByDescending(a => a.MarketValue * (a.RaceProps.baseBodySize >= 1f ? 1.5f : 1f)).FirstOrDefault();

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Worship.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            var g = Gods.ColonyGods(map).FirstOrDefault(RainGod) ?? (RainGod(Gods.Patron(map)) ? Gods.Patron(map) : null);
            if (g == null) return false;
            var beast = Finest(map);
            if (beast == null || beast.MarketValue < 150f) return false;
            var a = GodSpeaker.Of(map, def.roleA, g) ?? GodSpeaker.Of(map, def.roleA);
            if (a == null) return false;
            ctx = new GodDilemmaContext { def = def, map = map, a = a, god1 = g.id, pet = beast };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            if (ctx.pet != null && ctx.pet.relations != null && ctx.pet.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond)) tags.Add("bonded");
        }
    }

    /// <summary>The gods' questions' tags: the step of how real the gods are ([step:felt]) and whether A is the priest ([priest]).</summary>
    public abstract class DilemmaWorker_Gods : DilemmaWorker
    {
        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            tags.Add("step:" + Worship.Step.ToLowerInvariant());
            if (ctx?.a != null && Worship.IsPriest(ctx.a)) tags.Add("priest");
        }
    }

    /// <summary>Two rival gods of the colony's, each with someone of the colony who prays to it: the colony is caught between them.</summary>
    public class DilemmaWorker_RivalGods : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Worship.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            var people = map.mapPawns.FreeColonistsSpawned.Where(p => Worship.Ours(p) && !p.DevelopmentalStage.Juvenile() && !Worship.Scoffer(p)).ToList();
            var gods = people.Select(p => Worship.GodOf(p)).Where(g => g != null).Distinct().ToList();
            foreach (var g1 in gods.InRandomOrder())
            {
                foreach (var g2 in Gods.Rivals(g1))
                {
                    if (!gods.Contains(g2)) continue;
                    var a = people.Where(p => Worship.GodOf(p) == g1 && def.roleA.Allows(p, null)).OrderByDescending(Worship.Devout).FirstOrDefault();
                    var bb = people.Where(p => p != a && Worship.GodOf(p) == g2 && (def.roleB == null || def.roleB.Allows(p, a))).OrderByDescending(Worship.Devout).FirstOrDefault();
                    if (a == null || bb == null) continue;
                    ctx = new GodDilemmaContext { def = def, map = map, a = a, b = bb, god1 = g1.id, god2 = g2.id };
                    return true;
                }
            }
            return false;
        }
    }

    // ================= The effects =================

    /// <summary>The colony honours a god for the year (the context's first, second or third): marked, pleased, told.</summary>
    public class DilemmaEffect_HonourGod : DilemmaEffect
    {
        public int god = 1;

        public override string Unavailable(DilemmaContext ctx) =>
            (ctx as GodDilemmaContext)?.GodAt(god) == null ? "CC_Wsh_Unavailable_NoGod".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var g = (ctx as GodDilemmaContext)?.GodAt(god);
            if (g == null || !Worship.On) return;
            sheet.Add(ImpactKind.Other, 1, "CC_Wsh_Impact_Honour".Translate(Gods.FullName(g).Named("GOD")).Resolve(), "CC_Wsh_Impact_HonourBrief".Translate(Gods.Name(g).Named("GOD")).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's choice, for the colony as a whole.
            var g = (ctx as GodDilemmaContext)?.GodAt(god);
            if (g != null) WorshipCouncil.Honour(g, ctx.map);
        }
    }

    /// <summary>The gods' favour moved by the council's answer: a god of the context's (1 to 3), by so much, as a rite, a neglect or an offering.</summary>
    public class DilemmaEffect_GodFavour : DilemmaEffect
    {
        public int god = 1;
        public float amount;
        public FavourSource source = FavourSource.Rite;
        public GodOffence offence = GodOffence.None;
        /// <summary>Only a god of a jealous nature (both rivals honoured: a slight to the jealous).</summary>
        public bool onlyJealous;
        [MustTranslate] public string why;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var g = (ctx as GodDilemmaContext)?.GodAt(god);
            if (g == null || !Worship.On || amount == 0f || onlyJealous && g.nature != GodNature.Jealous) return;
            sheet.Add(ImpactKind.Other, amount > 0f ? 1 : -1, (amount > 0f ? "CC_Wsh_Impact_Pleased" : "CC_Wsh_Impact_Displeased").Translate(Gods.FullName(g).Named("GOD")).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the gods hear of the council's answer.
            var g = (ctx as GodDilemmaContext)?.GodAt(god);
            if (g == null || amount == 0f || onlyJealous && g.nature != GodNature.Jealous) return;
            Worship.Favour(g, ctx.map, amount, why.NullOrEmpty() ? null : ctx.Format(why), source, offence);
        }
    }

    /// <summary>A temple vowed by the council: the one who asked vows it to the god (thirty days), kept when it stands (Worship/Vows.cs).</summary>
    public class DilemmaEffect_TempleVow : DilemmaEffect
    {
        public int days = 30;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            if (g == null || ctx.a == null || !Worship.On) return;
            sheet.Add(ImpactKind.Other, 0, "CC_Wsh_Impact_TempleVow".Translate(ctx.a.LabelShort.Named("PAWN"), Gods.Name(g).Named("GOD"), days.Named("DAYS"),
                WorshipMath.TempleImpressiveness.Named("IMPRESSIVE")).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: a vow made before the council.
            var g = (ctx as GodDilemmaContext)?.God1;
            if (g == null || ctx.a == null) return;
            var r = Vows.Make(ctx.a, g, VowKind.Temple);
            if (r != null) r.until = Worship.Now + days * GenDate.TicksPerDay;
        }
    }

    /// <summary>The rain kept away by the god refused (none in Believed), shorter and only half the time for a lesser gift.</summary>
    public class DilemmaEffect_DrySpell : DilemmaEffect
    {
        public bool lesser;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            int days = WorshipMath.DrySpellDays(Gods.Realness, lesser);
            if (days <= 0 || !Worship.On) return;
            sheet.Add(ImpactKind.Other, -1, (lesser ? "CC_Wsh_Impact_DryMaybe" : "CC_Wsh_Impact_Dry").Translate(days.Named("DAYS")).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the weather over the whole colony.
            var g = (ctx as GodDilemmaContext)?.God1;
            int days = WorshipMath.DrySpellDays(Gods.Realness, lesser);
            if (g == null || days <= 0 || ctx.map == null || lesser && !Rand.Chance(0.5f)) return;
            Worship.Book?.SetMark(Gods.HomeKey(ctx.map), "dry", g.id, Worship.Now, Worship.Now + days * GenDate.TicksPerDay);
            string text = ("CC_Wsh_DryBegins_" + Worship.Step).Translate(Gods.FullName(g).Named("GOD")).Resolve();
            Worship.Sign(g, ctx.map, ctx.map.Center, PresenceKind.Omen, "CC_Wsh_DryBegins_Title".Translate(Gods.Name(g).Named("GOD")).Resolve(), text, null, false, GodActKind.Wrath);
        }
    }

    /// <summary>A lesser offering carried to the god's place (the offerings' own carrying, Worship/Offerings.cs), when it has one.</summary>
    public class DilemmaEffect_LesserOffering : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            return g == null || ctx.map == null || Shrines.Of(ctx.map, g).Count == 0 ? "CC_Wsh_Unavailable_NoShrine".Translate(Gods.Name(g).Named("GOD")).Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            if (g == null || !Worship.On) return;
            sheet.Play("CC_Wsh_Impact_Offering".Translate(Worship.ShrineOf(g).Named("SHRINE")).Resolve(), "CC_Wsh_Impact_OfferingBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            var b = Worship.Book;
            if (g == null || b == null || ctx.map == null) return;
            var shrine = Shrines.Of(ctx.map, g).Select(s => s.thing).FirstOrDefault();
            if (shrine == null) return;
            b.offerings.Add(new OfferingRecord { shrine = shrine, god = g.id, home = Gods.HomeKey(ctx.map), chosen = Worship.Now });
        }
    }

    /// <summary>
    /// The beast the god asked for is given: the one who asked walks it out toward the edge of the colony's land, and there it
    /// is let go to walk off into the wild (Walking: the god's own come for it), the god much pleased.
    /// </summary>
    public class DilemmaEffect_GiveBeast : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.pet == null || ctx.pet.Dead || !ctx.pet.Spawned ? "CC_Wsh_Unavailable_NoBeast".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.pet == null || !Worship.On) return;
            sheet.Play("CC_Wsh_Impact_GiveBeast".Translate(ctx.pet.LabelShort.Named("PET")).Resolve(), "CC_Wsh_Impact_GiveBeastBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var beast = ctx?.pet;
            var g = (ctx as GodDilemmaContext)?.God1;
            if (beast == null || g == null || !beast.Spawned || beast.Dead) return;
            var map = beast.Map;
            try
            {
                // Led out by the one who asked, when they can walk with it; then off into the wild, its own way to the edge.
                if (!RCellFinder.TryFindBestExitSpot(beast, out var edge, TraverseMode.ByPawn) && !CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && beast.CanReach(c, PathEndMode.OnCell, Danger.Deadly), map, 0f, out edge))
                    return;
                if (ctx.a != null && ctx.a.Spawned && ctx.a.Map == map && WorshipJobs.Free(ctx.a, false))
                {
                    var walk = JobMaker.MakeJob(JobDefOf.Goto, beast.Position);
                    walk.locomotionUrgency = LocomotionUrgency.Walk;
                    WorshipJobs.Start(ctx.a, walk);
                    Worship.Say(ctx.a, "Rimfolk_Pool_Worship_GiveBeast", g);
                }
                beast.SetFaction(null);
                var go = JobMaker.MakeJob(JobDefOf.Goto, edge);
                go.exitMapOnArrival = true;
                go.locomotionUrgency = LocomotionUrgency.Walk;
                beast.jobs.StartJob(go, JobCondition.InterruptForced, null, false, true);
                string text = ("CC_Wsh_BeastGiven_" + Worship.Step).Translate(beast.LabelShort.Named("PET"), Gods.FullName(g).Named("GOD")).Resolve();
                Gods.AddFavour(g, map, WorshipMath.BeastGiven, "CC_Wsh_Why_BeastGiven".Translate(beast.LabelShort.Named("PET")).Resolve(), FavourSource.Offering);
                if (Gods.Realness == GodRealness.Walking)
                    Worship.Sign(g, map, beast.Position, PresenceKind.Act, "CC_Wsh_BeastGiven_Title".Translate(Gods.Name(g).Named("GOD")).Resolve(), text, beast, false);
                else Gods.Remember(new GodAct { god = g.id, home = Gods.HomeKey(map), kind = GodActKind.Favour, text = text });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("worship: a beast given", e);
            }
        }
    }

    /// <summary>
    /// Words said at a god's place by its priest (else the one the answer names), for those who come: a short gathering
    /// there, the play-out's words (the context's first, second or third god's place; the usual places when it has none).
    /// </summary>
    public class DilemmaEffect_ShrineWords : DilemmaEffect_Words
    {
        public int god = 1;
        /// <summary>The priest leads it when there is one.</summary>
        public bool priest = true;

        protected override bool Fill(PlayOut p, DilemmaContext ctx)
        {
            if (!base.Fill(p, ctx)) return false;
            var g = (ctx as GodDilemmaContext)?.GodAt(god);
            if (g == null || ctx.map == null) return true;
            var lead = priest ? Worship.Priest(ctx.map) : null;
            if (lead != null && lead.Spawned && lead.Map == ctx.map && PlayOuts.CanTakePart(lead, ctx.map))
            {
                p.organizer = lead;
                if (!p.invited.Contains(lead)) p.invited.Add(lead);
            }
            var shrine = Shrines.Of(ctx.map, g).Select(s => s.thing).OrderByDescending(Shrines.IsTemple).FirstOrDefault();
            if (shrine != null && shrine.Spawned && shrine.Map == ctx.map)
            {
                p.place = shrine;
                p.spots.Remove(PlaySpot.Place);
                p.spots.Insert(0, PlaySpot.Place);
            }
            return p.invited.Count > 0;
        }
    }
}
