using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The council and the gods' places (docs/gods-altars-proposals.md A8 and B6): a believer asks for a place for their god
    /// (agreed, the player picks the spot with the place's button and the one who asked says the words; refused, or no place
    /// given in time, they are let down), and, with the colony's believers split between two rival gods whose feasts fall
    /// in the same season, whose feast the colony keeps in full (the other kept quietly: no offering carried, no guests).
    /// </summary>
    public static class GodPlacesCouncil
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private const string Asked = "placeasked";

        /// <summary>Someone at a home who asked the council for a place for this god lately and was agreed, or null.</summary>
        public static Pawn AskedFor(Map map, God g)
        {
            var b = Worship.Book;
            if (b == null || map == null || g == null) return null;
            int since = Now - GodPlacesMath.AskedWaitDays * GenDate.TicksPerDay;
            return b.marks.LastOrDefault(m => m != null && m.what == Asked && m.god == g.id && m.tick >= since && m.pawn != null && m.pawn.MapHeld == map && !m.pawn.Dead)?.pawn;
        }

        /// <summary>A place given to its god: the one who asked the council for it is glad, and their asking is done.</summary>
        public static void Given(ShrineRecord r, God g, Map map)
        {
            var b = Worship.Book;
            if (b == null || g == null) return;
            var asked = r?.askedBy ?? AskedFor(map, g);
            if (asked != null && !asked.Dead) CouncilGameComponent.GiveThought(asked, GodRitesDefOf.Rimfolk_Worship_PlaceGiven);
            b.marks.RemoveAll(m => m != null && m.what == Asked && m.god == g.id && (m.pawn == asked || m.pawn?.MapHeld == map));
            if (r != null) r.askedBy = null;
        }

        /// <summary>The region's day: a place agreed by the council and not given in time lets down the one who asked.</summary>
        public static void AskedDaily(WorshipBook b, Map map, int now)
        {
            int late = now - GodPlacesMath.AskedWaitDays * GenDate.TicksPerDay;
            foreach (var m in b.marks.Where(x => x != null && x.what == Asked && x.tick < late && x.pawn?.MapHeld == map).ToList())
            {
                b.marks.Remove(m);
                var g = Gods.ById(m.god);
                if (g == null || m.pawn.Dead || GodPlaces.PlacesOf(map, g) > 0) continue;
                CouncilGameComponent.GiveThought(m.pawn, GodRitesDefOf.Rimfolk_Worship_PlaceRefused);
                Tell.Minor(NewsKind.People, Importance.Flavor, map, "CC_Wsh_PlaceNotGiven".Translate(m.pawn.LabelShort.Named("PAWN"), Worship.Called(g).Named("GOD")).Resolve(), m.pawn);
            }
        }

        // ================= Whose feast this season =================

        /// <summary>The god whose feast the council said would be kept quietly this season, for a rival's (B6), at a home.</summary>
        public static bool KeptQuietly(God g, Map map)
        {
            var m = Worship.Book?.Mark(Gods.HomeKey(map), "rivalfeast");
            return m != null && g != null && m.god == g.id && Now <= m.until;
        }

        /// <summary>The feast kept quietly for a rival's: those who hold its god are put out, and the council's word is spent.</summary>
        public static void KeptQuietlyDone(God g, Map map)
        {
            var b = Worship.Book;
            var m = b?.Mark(Gods.HomeKey(map), "rivalfeast");
            if (m == null || g == null || m.god != g.id) return;
            b.homeMarks.Remove(m);
            foreach (var p in GodPlaces.Holders(map, g)) CouncilGameComponent.GiveThought(p, GodRitesDefOf.Rimfolk_Worship_FeastQuiet);
        }

        /// <summary>Whether a god's feast falls in this season at a home (its quadrum is now).</summary>
        public static bool FeastThisSeason(God g, Map map)
        {
            if (g == null || map == null || !GodWorshipDef.KeepsFestival(g)) return false;
            var (season, _) = Festivals.When(g);
            var ll = Find.WorldGrid.LongLatOf(map.Tile);
            return (int)GenDate.Quadrum(Find.TickManager.TicksAbs, ll.x) == Festivals.QuadrumOf(season, ll.y);
        }
    }

    /// <summary>A believer asks the council for a place for their god: a god they hold that has no place at their home (A8).</summary>
    public class DilemmaWorker_PlaceAsked : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var b = Worship.Book;
            if (b == null || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            int since = Find.TickManager.TicksGame - GodPlacesMath.AskedWaitDays * 2 * GenDate.TicksPerDay;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.InRandomOrder())
            {
                if (!Worship.Ours(p) || p.DevelopmentalStage.Juvenile() || Worship.Scoffer(p) || def.roleA != null && !def.roleA.Allows(p, null)) continue;
                var g = Worship.GodOf(p);
                if (g == null || g.kind == GodKind.Dead || !GodPlaces.HoldsFor(p, g)) continue;
                var w = PawnGods.WordOf(p, g);
                if (w != BeliefWord.Holds && w != BeliefWord.Devoted && w != BeliefWord.Fervent) continue;
                if (GodPlaces.PlacesOf(map, g) > 0 || b.Count(p, "placeasked", since, g.id) > 0) continue;
                ctx = new GodDilemmaContext { def = def, map = map, a = p, god1 = g.id };
                return true;
            }
            return false;
        }
    }

    /// <summary>Agreed: the player picks the spot (the place's button), and the one who asked will say the words; let down if none is given in time.</summary>
    public class DilemmaEffect_PlaceAgreed : DilemmaEffect
    {
        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var g = (ctx as GodDilemmaContext)?.God1;
            if (g == null || ctx.a == null) return;
            sheet.Add(ImpactKind.Other, 1, "CC_Wsh_Impact_PlaceAgreed".Translate(Worship.Called(g).Named("GOD"), ctx.a.Named("A"), GodPlacesMath.AskedWaitDays.Named("DAYS")).Resolve(),
                "CC_Wsh_Impact_PlaceAgreedBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's answer about a place for a god; the spot is the player's to pick.
            var g = (ctx as GodDilemmaContext)?.God1;
            if (g == null || ctx.a == null) return;
            Worship.Book?.AddMark(ctx.a, "placeasked", g.id);
            Tell.Minor(NewsKind.Colony, Importance.Notable, ctx.map, "CC_Wsh_PlaceAgreed_Message".Translate(Worship.Called(g).Named("GOD"), ctx.a.LabelShort.Named("PAWN"),
                GodPlacesMath.AskedWaitDays.Named("DAYS")).Resolve(), ctx.a);
        }
    }

    /// <summary>Two rival gods with believers at home, whose feasts fall this season: the council says whose is kept in full (B6).</summary>
    public class DilemmaWorker_RivalFeasts : DilemmaWorker_Gods
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Worship.On || !CanHappenNow(map) || map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            if (Worship.Book?.Mark(Gods.HomeKey(map), "rivalfeast") is HomeMark m && Find.TickManager.TicksGame <= m.until) return false;
            var gods = Festivals.GodsFor(map).Where(g => GodPlacesCouncil.FeastThisSeason(g, map)).ToList();
            foreach (var g1 in gods.InRandomOrder())
            foreach (var g2 in Gods.Rivals(g1))
            {
                if (!gods.Contains(g2)) continue;
                var a = GodPlaces.Holders(map, g1).Where(p => !p.DevelopmentalStage.Juvenile() && !GodPlaces.HoldsFor(p, g2) && (def.roleA == null || def.roleA.Allows(p, null)))
                    .OrderByDescending(p => PawnGods.HoldOf(p, g1)).FirstOrDefault();
                var bb = GodPlaces.Holders(map, g2).Where(p => p != a && !p.DevelopmentalStage.Juvenile() && !GodPlaces.HoldsFor(p, g1) && (def.roleB == null || def.roleB.Allows(p, a)))
                    .OrderByDescending(p => PawnGods.HoldOf(p, g2)).FirstOrDefault();
                if (a == null || bb == null) continue;
                ctx = new GodDilemmaContext { def = def, map = map, a = a, b = bb, god1 = g1.id, god2 = g2.id };
                return true;
            }
            return false;
        }
    }

    /// <summary>One god's feast kept in full this season; its rival's kept quietly (no offering, no guests), and its believers put out.</summary>
    public class DilemmaEffect_FeastFirst : DilemmaEffect
    {
        /// <summary>The god kept in full: 1 or 2 of the context; the other is kept quietly.</summary>
        public int god = 1;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var c = ctx as GodDilemmaContext;
            var quiet = c?.GodAt(god == 1 ? 2 : 1);
            if (quiet == null) return;
            sheet.Add(ImpactKind.Other, -1, "CC_Wsh_Impact_FeastQuiet".Translate(Festivals.Name(quiet).Named("FESTIVAL"), Worship.Called(quiet).Named("GOD")).Resolve(),
                "CC_Wsh_Impact_FeastQuietBrief".Translate().Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            // apart: the council's answer for the season's two feasts.
            var c = ctx as GodDilemmaContext;
            var quiet = c?.GodAt(god == 1 ? 2 : 1);
            var b = Worship.Book;
            if (quiet == null || b == null || ctx.map == null) return;
            b.SetMark(Gods.HomeKey(ctx.map), "rivalfeast", quiet.id, 1, Find.TickManager.TicksGame + GenDate.TicksPerQuadrum);
        }
    }
}
