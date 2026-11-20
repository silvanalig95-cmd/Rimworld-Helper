using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// More council questions of faith (C13 of docs/content-update-proposals.md; the player's go of 20 November 2026;
    /// Defs/GodDefs/Rimfolk_Worship_Dilemmas_More.xml, in the flavour style of the council's questions). Each answer plays out
    /// on the map: words at the god's place, a place taken up, built round or taken down, an offering carried by a scoffer,
    /// a festival's eve kept with less, with the rite only, or let pass.
    /// <list type="bullet">
    /// <item>The shrine in the way: a plan of the colony's (a blueprint or a frame) is laid against a god's place.</item>
    /// <item>The feast in a hard season: a god's festival comes tomorrow while food is short or a raid came lately.</item>
    /// <item>The scoffer at the rite: someone laughed at a god before its faithful lately, and the priest (or the most devout)
    /// wants them kept from its rites.</item>
    /// <item>A shrine nobody keeps: a god's place at home whose god nobody of the colony holds any more.</item>
    /// </list>
    /// </summary>
    public static class WorshipCouncilMore
    {
        /// <summary>A scoffer kept from a god's rites this many days (WorshipBook mark "barred"), and a festival's eve answer kept until its day is past.</summary>
        public const int BarredDays = 15;

        /// <summary>A plan of the colony's (a blueprint or frame) against a god's place: the place, or null.</summary>
        public static Thing InTheWay(Map map, God g)
        {
            foreach (var s in Shrines.Of(map, g))
            {
                var t = s.thing;
                if (t == null || !t.Spawned) continue;
                foreach (var c in GenRadial.RadialCellsAround(t.Position, 1.5f, true).Where(c => c.InBounds(map)))
                    if (c.GetThingList(map).Any(x => (x is Blueprint || x is Frame) && x.Faction == Faction.OfPlayer)) return t;
            }
            return null;
        }

        /// <summary>A god's festival day at this home that comes tomorrow, or null.</summary>
        public static CalendarDay FestivalTomorrow(Map map)
        {
            var cal = ColonyCalendar.For(map);
            if (cal == null) return null;
            int tomorrow = CalendarMath.Wrap(ColonyCalendar.DayOf(Find.TickManager.TicksGame, map) + 1);
            return cal.days.FirstOrDefault(d => d != null && d.kind == CalendarKind.GodFestival && d.day == tomorrow && ColonyCalendar.GodOfDay(d) != null);
        }

        /// <summary>A hard season: food short (under five days) or a raid within the last three days.</summary>
        public static bool Hard(Map map)
        {
            int last = map.storyState.LastThreatBigTick;
            return OwnHappenings.FoodDays(map) < 5f || last > 0 && Find.TickManager.TicksGame - last < GenDate.TicksPerDay * 3;
        }

        /// <summary>The council's answer on a festival's eve for this god at this home ("lean", "rite", "pass"), or null.</summary>
        public static string EveAnswer(God g, Map map)
        {
            var m = Worship.Book?.Mark(Gods.HomeKey(map), "eve", g?.id ?? -1);
            if (m == null || m.god != g?.id || Worship.Now > m.until) return null;
            return m.value == 1 ? "lean" : m.value == 2 ? "rite" : m.value == 3 ? "pass" : null;
        }

        /// <summary>Barred from a god's rites lately (the council's answer to a scoffer).</summary>
        public static bool Barred(Pawn p) => Worship.Book?.Count(p, "barred", Worship.Now - BarredDays * GenDate.TicksPerDay) > 0;

        /// <summary>One who laughed at a god before its faithful lately (FaithHeart's mock, a "mocked" mark), the god, or none.</summary>
        public static (Pawn scoffer, God g) Mocker(Map map)
        {
            var b = Worship.Book;
            if (b == null) return (null, null);
            int since = Worship.Now - GenDate.TicksPerDay * 5;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.Where(Worship.Ours))
            {
                var m = b.marks.LastOrDefault(x => x != null && x.pawn == p && x.what == "mocked" && x.tick >= since);
                if (m != null && Gods.ById(m.god) != null && !Barred(p)) return (p, Gods.ById(m.god));
            }
            return (null, null);
        }

        /// <summary>A god's place at home whose god nobody of the colony holds any more, or null.</summary>
        public static (Thing place, God g) Unkept(Map map)
        {
            var b = Worship.Book;
            if (b == null) return (null, null);
            var people = map.mapPawns.FreeColonistsSpawned.Where(Worship.Ours).ToList();
            foreach (var r in b.shrines.Where(s => s != null && !s.pending && s.thing != null && s.thing.Spawned && s.thing.Map == map))
            {
                var g = Gods.ById(r.god);
                if (g == null || g.kind == GodKind.Dead) continue;
                if (people.Any(p => Worship.GodOf(p) == g || PawnGods.HoldOf(p, g) >= PawnGodsMath.LeansFrom)) continue;
                return (r.thing, g);
            }
            return (null, null);
        }
    }

    /// <summary>The shrine in the way: one who holds the god asks; the colony's best builder is the other side.</summary>
    public class DilemmaWorker_ShrineInTheWay : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Worship.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            foreach (var g in Worship.PrayedTo(map))
            {
                var place = WorshipCouncilMore.InTheWay(map, g);
                if (place == null) continue;
                var a = GodSpeaker.Of(map, def.roleA, g);
                var bb = map.mapPawns.FreeColonistsSpawned.Where(p => p != a && Worship.Ours(p) && !p.DevelopmentalStage.Juvenile() && (def.roleB == null || def.roleB.Allows(p, a)))
                    .OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Construction)?.Level ?? 0).FirstOrDefault();
                if (a == null || bb == null) continue;
                ctx = new GodDilemmaContext { def = def, map = map, a = a, b = bb, god1 = g.id };
                return true;
            }
            return false;
        }
    }

    /// <summary>The god's place in the way: taken up to be set down elsewhere, the plan drawn round it, or taken down.</summary>
    public class DilemmaEffect_ShrineInTheWay : DilemmaEffect
    {
        /// <summary>"move", "round" or "down".</summary>
        public string how = "round";

        public override string Unavailable(DilemmaContext ctx)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            var place = g == null || ctx.map == null ? null : WorshipCouncilMore.InTheWay(ctx.map, g);
            if (place == null) return "CC_Wsh_Unavailable_NoShrine".Translate(Worship.Called(g).Named("GOD")).Resolve();
            return how == "move" && !place.def.Minifiable ? "CC_Wsh_Unavailable_NotMovable".Translate(place.LabelShort.Named("PLACE")).Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            if (g == null || !Worship.On) return;
            sheet.Play(("CC_Wsh_Impact_Shrine_" + how).Translate(Worship.ShrineOf(g).Named("SHRINE")).Resolve(), ("CC_Wsh_Impact_Shrine_" + how + "Brief").Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            var map = ctx.map;
            var place = g == null || map == null ? null : WorshipCouncilMore.InTheWay(map, g);
            if (place == null) return;
            switch (how)
            {
                case "move":
                    // Taken up after the words, to be set down where the colony likes.
                    if (place.def.Minifiable && map.designationManager.DesignationOn(place, DesignationDefOf.Uninstall) == null)
                        map.designationManager.AddDesignation(new Designation(place, DesignationDefOf.Uninstall));
                    break;
                case "round":
                    // The plan drawn round it: the blueprints and frames against it cancelled (a frame's goods come back).
                    foreach (var c in GenRadial.RadialCellsAround(place.Position, 1.5f, true).Where(c => c.InBounds(map)).ToList())
                        foreach (var t in c.GetThingList(map).Where(x => (x is Blueprint || x is Frame) && x.Faction == Faction.OfPlayer).ToList())
                            t.Destroy(DestroyMode.Cancel);
                    break;
                case "down":
                    // It is the god's no longer, and it comes down.
                    Shrines.Undedicate(place);
                    if (map.designationManager.DesignationOn(place, DesignationDefOf.Deconstruct) == null && place.def.building != null)
                        map.designationManager.AddDesignation(new Designation(place, DesignationDefOf.Deconstruct));
                    break;
            }
        }
    }

    /// <summary>The feast in a hard season: the god's festival tomorrow, food short or a raid lately; the priest asks.</summary>
    public class DilemmaWorker_FeastHardSeason : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Worship.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists || !WorshipCouncilMore.Hard(map)) return false;
            var day = WorshipCouncilMore.FestivalTomorrow(map);
            var g = ColonyCalendar.GodOfDay(day);
            if (g == null || WorshipCouncilMore.EveAnswer(g, map) != null) return false;
            var a = GodSpeaker.Of(map, def.roleA, g) ?? GodSpeaker.Of(map, def.roleA);
            if (a == null) return false;
            ctx = new GodDilemmaContext { def = def, map = map, a = a, god1 = g.id };
            return true;
        }
    }

    /// <summary>
    /// The council's answer on a festival's eve, kept for its day: kept with less (no offering carried and no guests sent
    /// for: Festivals.Kept), the rite only (the rite held now, and the day itself not kept: ColonyCalendar), or let pass (the day
    /// let pass when it comes).
    /// </summary>
    public class DilemmaEffect_FestivalEve : DilemmaEffect
    {
        /// <summary>"lean", "rite" or "pass".</summary>
        public string how = "lean";

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            if (g == null || !Worship.On) return;
            sheet.Add(ImpactKind.Other, 0, ("CC_Wsh_Impact_Eve_" + how).Translate(Festivals.Name(g).Named("FESTIVAL")).Resolve(), ("CC_Wsh_Impact_Eve_" + how + "Brief").Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's answer for the colony's festival day.
            var g = (ctx as GodDilemmaContext)?.God1;
            var b = Worship.Book;
            if (g == null || b == null || ctx.map == null) return;
            int value = how == "lean" ? 1 : how == "rite" ? 2 : 3;
            b.SetMark(Gods.HomeKey(ctx.map), "eve", g.id, value, Worship.Now + GenDate.TicksPerDay * 2);
        }
    }

    /// <summary>The scoffer at the rite: one who laughed at a god before its faithful lately; the priest (or the most devout of it) asks.</summary>
    public class DilemmaWorker_ScofferAtRite : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Worship.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            var (scoffer, g) = WorshipCouncilMore.Mocker(map);
            if (scoffer == null || g == null || Shrines.Of(map, g).Count == 0) return false;
            var a = GodSpeaker.Of(map, def.roleA, g);
            if (a == null || a == scoffer) return false;
            ctx = new GodDilemmaContext { def = def, map = map, a = a, b = scoffer, god1 = g.id };
            return true;
        }
    }

    /// <summary>The scoffer kept from the god's rites for a while (WorshipBook mark "barred": the festival's gathering leaves them out).</summary>
    public class DilemmaEffect_BarScoffer : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.b == null || !Worship.On) return;
            sheet.Add(ImpactKind.Other, -1, "CC_Wsh_Impact_Barred".Translate(ctx.b.Named("B"), WorshipCouncilMore.BarredDays.Named("DAYS")).Resolve(), "CC_Wsh_Impact_BarredBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's answer about one of the colony.
            var g = (ctx as GodDilemmaContext)?.God1;
            if (ctx?.b == null || g == null) return;
            Worship.Book?.AddMark(ctx.b, "barred", g.id);
        }
    }

    /// <summary>The scoffer carries the god's offering to its place (the offering the place is waiting for, carried by B).</summary>
    public class DilemmaEffect_ScofferCarries : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            return g == null || ctx.map == null || Shrines.Of(ctx.map, g).Count == 0 || !GodWorshipDef.TakesOfferings(g)
                ? "CC_Wsh_Unavailable_NoShrine".Translate(Worship.Called(g).Named("GOD")).Resolve() : null;
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            if (g == null || ctx?.b == null || !Worship.On) return;
            sheet.Play("CC_Wsh_Impact_ScofferCarries".Translate(ctx.b.Named("B"), Worship.ShrineOf(g).Named("SHRINE")).Resolve(), "CC_Wsh_Impact_OfferingBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            var b = Worship.Book;
            if (g == null || b == null || ctx.map == null || ctx.b == null) return;
            var shrine = Shrines.Of(ctx.map, g).Select(s => s.thing).FirstOrDefault();
            if (shrine == null) return;
            // The offering waits at the place; the scoffer is the one who carries it (Offerings: the carrier asked for).
            b.offerings.Add(new OfferingRecord { shrine = shrine, god = g.id, home = Gods.HomeKey(ctx.map), chosen = Worship.Now, asked = ctx.b });
        }
    }

    /// <summary>A shrine nobody keeps: a god's place whose god nobody at home holds; the priest (or the most devout) asks.</summary>
    public class DilemmaWorker_ShrineUnkept : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Worship.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            var (place, g) = WorshipCouncilMore.Unkept(map);
            if (place == null) return false;
            var a = GodSpeaker.Of(map, def.roleA);
            var other = a == null ? null : Worship.GodOf(a);
            if (a == null) return false;
            ctx = new GodDilemmaContext { def = def, map = map, a = a, god1 = g.id, god2 = other != null && other != g ? other.id : -1 };
            return true;
        }
    }

    /// <summary>A shrine nobody keeps given to another god (A's own): someone walks over and says the words (Shrines.Ask).</summary>
    public class DilemmaEffect_ShrineGiven : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx) =>
            (ctx as GodDilemmaContext)?.God2 == null ? "CC_Wsh_Unavailable_NoOtherGod".Translate().Resolve() : null;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = ctx as GodDilemmaContext;
            if (c?.God2 == null || !Worship.On) return;
            sheet.Play("CC_Wsh_Impact_ShrineGiven".Translate(Worship.Called(c.God2).Named("GOD")).Resolve(), "CC_Wsh_Impact_ShrineGivenBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = ctx as GodDilemmaContext;
            var (place, g) = c?.map == null ? (null, null) : WorshipCouncilMore.Unkept(c.map);
            if (place == null || c.God2 == null || g != c.God1) return;
            Shrines.Ask(place, c.God2);
        }
    }

    /// <summary>A shrine nobody keeps let fall: it is the god's no longer, and left as it stands.</summary>
    public class DilemmaEffect_ShrineLetFall : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's answer about a place.
            var c = ctx as GodDilemmaContext;
            var (place, g) = c?.map == null ? (null, null) : WorshipCouncilMore.Unkept(c.map);
            if (place != null && g == c.God1) Shrines.Undedicate(place);
        }
    }
}
