using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A named place brought up at supper (C25, Places/PlaceNames.cs): of an evening, someone eating beside another brings one
    /// of the home's named places up to them in a sentence (Rimfolk_Pool_PlaceSupper, by the place's kind, and [named] when
    /// the speaker named it, who brings that one up first). At most once in PlaceNameMath.SupperGapDays at a home, at a small
    /// chance an evening hour. Only places that stand are spoken of.
    /// </summary>
    public static class PlaceTalk
    {
        public const string SupperPool = "Rimfolk_Pool_PlaceSupper";

        internal static void Supper(Map map, PlaceBook book)
        {
            if (map != Find.CurrentMap || book.places.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < 17 || hour > 21 || !PlaceNameMath.SupperDue(book.lastSupper, now, GenDate.TicksPerDay) || !Rand.Chance(PlaceNameMath.SupperChance)) return;
            var standing = book.places.Where(p => p != null && p.Stands && p.cell.InBounds(map)).ToList();
            if (standing.Count == 0) return;
            foreach (var p in map.mapPawns.FreeColonistsSpawned.InRandomOrder().ToList())
            {
                if (p.CurJobDef != JobDefOf.Ingest || !People.HasInnerLife(p)) continue;
                var other = Together.Company(p);
                if (other == null || other.CurJobDef != JobDefOf.Ingest) continue;
                // Their own place first, when they named one.
                var place = standing.Where(x => x.namerId == p.ThingID).RandomElementWithFallback() ?? standing.RandomElement();
                bool named = place.namerId == p.ThingID;
                string line = Flavor.FromPool(SupperPool, p, null, null, "kind:" + place.kind, named ? "named" : null);
                if (line.NullOrEmpty()) continue;
                // together: said to whoever eats beside them, at the table (Speech.Say checks they are really together).
                if (Speech.Say(p, line.Formatted(p.Named("PAWN"), other.Named("OTHER"), place.name.Named("PLACE")).Resolve().CapitalizeFirst(), other))
                {
                    book.lastSupper = now;
                    return;
                }
            }
        }
    }
}
