using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Rimfolk's own peoples (Defs/PeopleDefs/; docs/design/peoples.md; DESIGN.md "Rimfolk's peoples"): the road league,
    /// the hospitaller order and the herding folk, peaceful callings the world lacked, each a FactionDef on the game's
    /// abstract parents with a <see cref="PeopleExtension"/>. The game makes them at world creation at its usual chances
    /// (each listed once in the world's peoples, never required) and runs their gear, faith, traders, raids and towns by its
    /// own rules. This is where Rimfolk's systems ask about them: what a people lives by and the tag its lines are written
    /// for, its notables' titles, its messengers' word, how much likelier its caravans, camps and asks are, and its towns'
    /// plan (Peoples/PeopleTowns.cs). Nothing here is saved, and nothing runs on a clock of its own.
    /// </summary>
    public static class Peoples
    {
        private static Dictionary<FactionDef, PeopleExtension> byDef;

        private static Dictionary<FactionDef, PeopleExtension> ByDef
        {
            get
            {
                if (byDef == null)
                {
                    byDef = new Dictionary<FactionDef, PeopleExtension>();
                    foreach (var d in DefDatabase<FactionDef>.AllDefsListForReading)
                    {
                        var ext = d?.GetModExtension<PeopleExtension>();
                        if (ext != null && PeoplesMath.IsCalling(ext.calling)) byDef[d] = ext;
                    }
                }
                return byDef;
            }
        }

        /// <summary>Every people template Rimfolk has (release B makes new peoples from these).</summary>
        public static IEnumerable<FactionDef> Templates => ByDef.Keys;

        /// <summary>The template of a people of this def, or null when it isn't one of Rimfolk's peoples.</summary>
        public static PeopleExtension Of(FactionDef d) => d != null && ByDef.TryGetValue(d, out var ext) ? ext : null;

        public static PeopleExtension Of(Faction f) => Of(f?.def);

        public static bool IsOursDef(FactionDef d) => Of(d) != null;

        public static bool IsOurs(Faction f) => Of(f) != null;

        /// <summary>What a people lives by (traders, healers, herders), or null for any other people.</summary>
        public static string CallingOf(Faction f) => Of(f)?.calling;

        /// <summary>A people of this calling that is still in the world and at peace with the colony (it can send someone).</summary>
        public static bool AnyAtPeace(string calling) =>
            !calling.NullOrEmpty() && Find.FactionManager != null && Find.FactionManager.AllFactionsListForReading.Any(f => CallingOf(f) == calling && Arrivals.Usable(f));

        /// <summary>Any of Rimfolk's peoples in this world.</summary>
        public static bool AnyInWorld => Find.FactionManager != null && Find.FactionManager.AllFactionsListForReading.Any(f => f != null && !f.defeated && IsOurs(f));

        // ================= Words =================

        /// <summary>The tag a people gives Rimfolk's lines ("their:healers"), added to <paramref name="tags"/> when it is one of Rimfolk's.</summary>
        public static void AddTags(Faction f, ICollection<string> tags)
        {
            string tag = PeoplesMath.Tag(CallingOf(f));
            if (tag != null && tags != null && !tags.Contains(tag)) tags.Add(tag);
        }

        /// <summary>A notable's title by their people's ways ("head", "heir", "envoy", "trader"), or null for the age's own word.</summary>
        public static string Title(Faction f, string role, bool female)
        {
            var ext = Of(f);
            return ext == null ? null : PeoplesMath.Title(role, female, ext.head, ext.headFemale, ext.heir, ext.heirFemale, ext.envoy, ext.trader);
        }

        /// <summary>What their messengers are called ("road rider", "postulants"), or null for the age's own word.</summary>
        public static string Courier(Faction f, bool plural)
        {
            var ext = Of(f);
            if (ext == null) return null;
            return plural ? ext.couriers.NullOrEmpty() ? null : ext.couriers : ext.courier.NullOrEmpty() ? null : ext.courier;
        }

        /// <summary>One of their towns ("waystation", "sickhouse", "camp"), or null.</summary>
        public static string TownWord(Faction f) => Of(f)?.townWord;

        // ================= How often =================

        /// <summary>How much likelier this people's trader caravan is the one the game sends (the league's: many caravans).</summary>
        public static float CaravanFactor(Faction f)
        {
            var ext = Of(f);
            return ext == null ? 1f : PeoplesMath.CaravanFactor(ext.caravanWeight);
        }

        /// <summary>Whether any people in this world weighs its caravans other than the game's way.</summary>
        public static bool AnyCaravanWeight => Find.FactionManager != null && Find.FactionManager.AllFactionsListForReading.Any(f => f != null && CaravanFactor(f) != 1f);

        /// <summary>A kind of camp this people favours comes from them more often (their drovers, their healers on the road).</summary>
        public static float CampFactor(CampKindDef kind, Faction f)
        {
            var ext = Of(f);
            return PeoplesMath.CampFactor(ext?.camps != null && kind != null && ext.camps.Contains(kind));
        }

        /// <summary>The weight on a people for an ask, a caller or a world event that prefers a calling.</summary>
        public static float Preference(string calling, Faction f) => PeoplesMath.Preference(calling, CallingOf(f));

        /// <summary>
        /// The weight a visitor's people is picked by (Arrivals.Friendly) for an event that prefers a calling
        /// (HappeningExtension.prefers: hungry pilgrims the pilgrims', a travelling scholar the scholars'), times
        /// <paramref name="also"/> when the event weighs its people already; null when it prefers none.
        /// </summary>
        public static Func<Faction, float> Weigh(string calling, Func<Faction, float> also = null)
        {
            if (calling.NullOrEmpty()) return also;
            if (also == null) return f => Preference(calling, f);
            return f => also(f) * Preference(calling, f);
        }

        /// <summary>Something only for a calling: a people of it (or anyone, when it names none).</summary>
        public static bool Fits(string only, Faction f) => PeoplesMath.Fits(only, CallingOf(f));

        /// <summary>A kind of camp only for a calling can come: a people of it is at peace with the colony.</summary>
        public static bool CanSend(CampKindDef kind) => kind == null || kind.calling.NullOrEmpty() || AnyAtPeace(kind.calling);

        /// <summary>Their towns stand by a river where the world has one (the river folk's landings).</summary>
        public static bool ByWater(FactionDef d) => Of(d)?.byWater ?? false;

        /// <summary>A tile of the world's surface a river runs through.</summary>
        public static bool HasRiver(PlanetTile t)
        {
            try
            {
                return t.Valid && Find.WorldGrid[t] is SurfaceTile st && !st.Rivers.NullOrEmpty();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>The hospitallers' sickhouse sees to the colony's travellers who lodge there (Harm/SickHome.cs).</summary>
        public static bool TendsLodgers(Faction f) => PeoplesMath.TendsLodgers(CallingOf(f));

        /// <summary>This people's trader caravan could come to the map now, as the game would send one (an answer that brings it: the league's outrider).</summary>
        public static bool CaravanCanCome(Faction f, Map map)
        {
            if (f == null || map == null || !(IncidentDefOf.TraderCaravanArrival?.Worker is IncidentWorker_TraderCaravanArrival w)) return false;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(IncidentDefOf.TraderCaravanArrival.category, map);
                parms.faction = f;
                return w.FactionCanBeGroupSource(f, parms) && w.CanFireNow(parms);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>A kind of the region's world events running now ("sickness": a plague year).</summary>
        public static bool Running(string kind)
        {
            if (kind.NullOrEmpty()) return true;
            var ws = WorldSituations.Instance;
            return ws != null && ws.Running.Any(s => s?.def?.kind == kind);
        }

        // ================= Making one =================

        /// <summary>
        /// A people of this template made now, the game's way (FactionGenerator.CreateFactionAndAddToManager: its faith, its
        /// leader, its relations with every other people, one town), its town moved within a few days of the home when one
        /// can stand there, clear of the game's nearness rule (a town within four tiles of a home costs goodwill every
        /// quadrum). For the dev tools; the peoples that arise during play are made by Peoples/Arising.cs. Null when it
        /// couldn't be made.
        /// </summary>
        public static Faction Make(FactionDef def, Map near)
        {
            if (!IsOursDef(def) || Find.FactionManager == null) return null;
            var before = new HashSet<Faction>(Find.FactionManager.AllFactionsListForReading);
            FactionGenerator.CreateFactionAndAddToManager(def);
            var f = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => !before.Contains(x) && x.def == def);
            if (f == null) return null;
            var town = Find.WorldObjects.Settlements.FirstOrDefault(s => s.Faction == f);
            // The river folk's landing by a river in reach, where there is one.
            if (town != null && near != null)
            {
                if (ByWater(def) && Arising.TryTownTile(near.Tile, 3, 10, null, null, out var landing, true)) town.Tile = landing;
                else if (Arising.TryTownTile(near.Tile, 3, 10, null, null, out var tile)) town.Tile = tile;
            }
            return f;
        }

        /// <summary>Forgets what was read from the defs (a dev reload).</summary>
        public static void Reset() => byDef = null;
    }
}
