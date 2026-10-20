using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One name for a fight at home, the same wherever it is told: in what shaped someone ("since going down in the raid
    /// on the 5th of Aprimay"), in a shared memory ("fought side by side in the raid on the 5th of Aprimay") and in
    /// what people say about them ("ran from the fight (the raid on the 5th of Aprimay)"). The name says who came (the
    /// people on the map hostile to the colony while it's on, or the faction once it's over) and the day it began, so
    /// two raids by the same people are two memories. It is kept while the fight goes on and half a day after.
    /// </summary>
    public static class FightNames
    {
        private static Game game;
        private static Map map;
        private static int tick = -99999;
        private static string label;

        /// <summary>How long a fight's name is kept after anyone last asked for it.</summary>
        public const int KeepTicks = GenDate.TicksPerHour * 12;

        public static void Reset()
        {
            game = null;
            map = null;
            tick = -99999;
            label = null;
        }

        /// <summary>The date as the lines say it ("5th of Aprimay"), at the map's longitude.</summary>
        public static string DateNow(Map m)
        {
            m ??= Find.AnyPlayerHomeMap;
            float longitude = m != null ? Find.WorldGrid.LongLatOf(m.Tile).x : 0f;
            return GenDate.QuadrumDateStringAt(Find.TickManager.TicksAbs, longitude);
        }

        /// <summary>
        /// The name of the fight on <paramref name="where"/> (or the one just over): "the raid on the 5th of Aprimay",
        /// "the mechanoid attack on...", "the fight with the insects on...", "the fight with the animals on...".
        /// <paramref name="foe"/> helps once the enemy has gone (null: read the map).
        /// </summary>
        public static string Of(Map where, Faction foe = null)
        {
            where ??= Find.AnyPlayerHomeMap;
            int now = Find.TickManager.TicksGame;
            if (game != Current.Game)
            {
                Reset();
                game = Current.Game;
            }
            if (label != null && map == where && now >= tick && now - tick < KeepTicks)
            {
                tick = now;
                return label;
            }
            string kind = KindOnMap(where) ?? KindOf(foe);
            map = where;
            tick = now;
            label = ("CC_Rep_Fight_" + kind).Translate(DateNow(where).Named("DATE")).Resolve();
            return label;
        }

        /// <summary>Who is fighting the colony on the map now (people first), or null when nobody is.</summary>
        private static string KindOnMap(Map m)
        {
            if (m?.mapPawns == null) return null;
            bool people = false, mechs = false, insects = false, beasts = false;
            foreach (var x in m.mapPawns.AllPawnsSpawned.ToList())
            {
                if (x == null || x.Dead || x.Downed || x.Faction == Faction.OfPlayer || !x.HostileTo(Faction.OfPlayer)) continue;
                if (x.RaceProps.Humanlike) people = true;
                else if (x.RaceProps.IsMechanoid) mechs = true;
                else if (x.RaceProps.Insect) insects = true;
                else if (x.RaceProps.Animal) beasts = true;
            }
            return people ? "Raid" : mechs ? "Mechs" : insects ? "Insects" : beasts ? "Beasts" : null;
        }

        private static string KindOf(Faction foe)
        {
            if (foe == null) return "Other";
            if (foe == Faction.OfMechanoids) return "Mechs";
            if (foe == Faction.OfInsects) return "Insects";
            return "Raid";
        }
    }
}
