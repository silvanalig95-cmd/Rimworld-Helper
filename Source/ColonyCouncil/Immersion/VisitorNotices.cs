using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Visitors who notice. A friendly visitor standing in the colony (Rimfolk's own, a caravan's people, a Hospitality
    /// guest) says a word about what's around them: a fine hall, a piece of art, the graves, a shrine, the walls and
    /// turrets, the herds, the fields (Council_Pool_VisitorNotices, by theme). Each such word is counted toward the
    /// colony being known for it (ColonyTraits tallies seen:hall, seen:art, seen:graves, seen:walls: the traits famed
    /// for its hall, artful, keepers of the dead, walled), and what the colony is known for draws the visitors who
    /// come for it (HappeningExtension.drawnBy). Said once per visitor per theme.
    /// </summary>
    public static class VisitorNotices
    {
        public const string Pool = "Council_Pool_VisitorNotices";

        /// <summary>Who already remarked on what (visitor id and theme); cleared now and then.</summary>
        private static readonly HashSet<string> said = new HashSet<string>();

        /// <summary>What a visitor standing here would notice, in the order it strikes them.</summary>
        public static List<string> Themes(Pawn g, Map map)
        {
            var list = new List<string>();
            if (g == null || map == null || !g.Spawned) return list;
            var at = g.Position;
            try
            {
                var room = at.GetRoom(map);
                if (room != null && !room.PsychologicallyOutdoors && room.CellCount >= 20 && RoomUtil.Impressiveness(room) >= 80f) list.Add("hall");
                int art = 0, graves = 0, walls = 0, turrets = 0, shrine = 0;
                foreach (var c in GenRadial.RadialCellsAround(at, 8f, true))
                {
                    if (!c.InBounds(map)) continue;
                    foreach (var t in c.GetThingList(map))
                    {
                        if (t.Faction != Faction.OfPlayer && !(t is Building_Grave)) continue;
                        if (t.TryGetComp<CompArt>() is CompArt a && a.Active && t.TryGetComp<CompQuality>() is CompQuality q && q.Quality >= QualityCategory.Excellent) art++;
                        if (t is Building_Grave grave && grave.HasCorpse) graves++;
                        if (t is Building_Turret) turrets++;
                        else if (t.def.Fillage == FillCategory.Full && t.def.passability == Traversability.Impassable && t.def.building != null && t.def.building.isEdifice && !t.def.building.isNaturalRock) walls++;
                        if (t.def.isAltar) shrine++;
                    }
                }
                if (art > 0) list.Add("art");
                if (graves >= 3) list.Add("graves");
                if (shrine > 0) list.Add("shrine");
                if (turrets >= 2 || walls >= 14) list.Add("walls");
                int beasts = map.mapPawns.SpawnedColonyAnimals.Count(a => a.Position.InHorDistOf(at, 14f));
                if (beasts >= 6) list.Add("beasts");
                int fields = 0;
                foreach (var c in GenRadial.RadialCellsAround(at, 10f, true))
                    if (c.InBounds(map) && map.zoneManager.ZoneAt(c) is Zone_Growing && ++fields >= 40) break;
                if (fields >= 40) list.Add("fields");
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("visitor notices", e);
            }
            return list;
        }

        /// <summary>
        /// A word about something the visitor noticed, not said by them before, or null. Saying it counts toward the
        /// colony's name for the thing.
        /// </summary>
        public static string Line(Pawn g, Map map)
        {
            var themes = Themes(g, map);
            if (themes.Count == 0) return null;
            if (said.Count > 600) said.Clear();
            foreach (var theme in themes.InRandomOrder())
            {
                string mark = g.thingIDNumber + ":" + theme;
                if (said.Contains(mark)) continue;
                string line = Flavor.FromPool(Pool, g, null, null, theme);
                if (line.NullOrEmpty()) continue;
                said.Add(mark);
                // Known for it, in time: the tally the traits famed for its hall, artful, keepers of the dead and walled count.
                ColonyTraits.Tally("seen:" + theme);
                var faction = Strangers.FactionOf(g);
                return line.Formatted(g.Named("PAWN"), (faction?.Name ?? "").Named("FACTION")).Resolve();
            }
            return null;
        }

        /// <summary>For the World page: what the colony is known for among what travellers notice, or null.</summary>
        public static string KnownLine()
        {
            var ch = CouncilGameComponent.Instance?.character;
            if (ch == null) return null;
            var bits = new List<string>();
            foreach (var tag in new[] { "FamedHall", "Artful", "Hallowed", "Walled" })
                if (ch.Is(tag)) bits.Add(("CC_Notice_Known_" + tag).Translate());
            return bits.Count == 0 ? null : "CC_Notice_Known".Translate(TextUtil.JoinAnd(bits)).Resolve();
        }
    }
}
