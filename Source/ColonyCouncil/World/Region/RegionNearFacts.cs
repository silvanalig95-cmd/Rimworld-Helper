using System;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The region's provider (Text/Facts/FactsRegion.cs) asks here for what stands near the colony now and what their own
    // town has been through: a raiders' camp the colony knows of (World/Region/RaidersCamp.cs), a ruin within reach (the
    // ruins rise and fall keeps, RiseFallLedger.cs), what brought a traveller (Camps/CampSeekers.cs), their town raised on
    // old walls (NewTowns.cs), a town of theirs given up lately, and a world event at their own town. Each reads the
    // records as they stand; one that fails leaves the others' facts.
    public static partial class Facts
    {
        /// <summary>The region's facts of what stands near the colony and what their town has been through, each on its own.</summary>
        private static void Near(FactScene s, FactSet f)
        {
            var home = s.colony ?? s.speaker?.MapHeld;
            int now = Now;
            void Each(string what, Action a)
            {
                try
                {
                    a();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("facts: the region, " + what, e);
                }
            }
            Each("a raiders' camp near", () => CampNear(s, f, home));
            Each("a ruin near", () => RuinNear(s, f, home));
            Each("what brought a traveller", () => Seeking(s, f));
            Each("a town on old walls", () => Resettled(s, f, now));
            Each("a town given up", () => GivenUp(s, f));
            Each("a world event at their town", () => TownSituation(s, f));
        }

        /// <summary>
        /// A raiders' camp standing near the colony that it knows of (found, or being paid off), not their own people's:
        /// region:camp, with {RAIDCAMP} its kind by its people's age ("brigands' hold") and {RAIDCAMPPEOPLE} its people;
        /// region:camp:theirfoe when their people are at war with the camp's.
        /// </summary>
        private static void CampNear(FactScene s, FactSet f, Map home)
        {
            var c = home != null ? RaidersCamps.CampOf(home) : null;
            if (c == null || !c.Shown || c.people == null || c.kind.NullOrEmpty() || s.people != null && s.people == c.people) return;
            f.Tag("region:camp", Fresh(Days(c.foundAt), 15f, 2f));
            f.Value("RAIDCAMP", c.kind);
            f.Value("RAIDCAMPPEOPLE", c.people.Name);
            if (s.people != null && s.people.HostileTo(c.people)) f.Tag("region:camp:theirfoe", 1.5f);
        }

        /// <summary>
        /// A ruin standing within the colony's reach (a town given up, or one a fight destroyed, that nobody has settled
        /// again): region:ruin, with {RUINNAME} the town it was and {RUINPEOPLE} whose it was; region:ruin:theirs when it was
        /// a town of their people (theirs first, then the newest), else region:ruin:other; region:ruin:fight when a fight
        /// left it.
        /// </summary>
        private static void RuinNear(FactScene s, FactSet f, Map home)
        {
            var l = WorldLedger.Instance;
            if (l == null || l.Ruins.Count == 0 || home == null || !home.Tile.Valid) return;
            LedgerRuin best = null;
            bool bestTheirs = false;
            foreach (var r in l.Ruins)
            {
                // A fight's ruin still waiting for the game's destroyed town to go isn't on the map yet.
                if (r == null || r.Waiting || r.name.NullOrEmpty()) continue;
                var t = RiseFall.TileOf(r.tile);
                if (!t.Valid || t.Layer != home.Tile.Layer || Find.WorldGrid.ApproxDistanceInTiles(home.Tile, t) > RegionTowns.ReachTiles) continue;
                bool theirs = s.people != null && r.people == s.people;
                if (best != null && (bestTheirs && !theirs || theirs == bestTheirs && r.since <= best.since)) continue;
                best = r;
                bestTheirs = theirs;
            }
            if (best == null) return;
            f.Tag("region:ruin", Fresh(Days(best.since), 60f, 1.2f));
            f.Tag(bestTheirs ? "region:ruin:theirs" : "region:ruin:other");
            if (best.why == LedgerRuin.Fight) f.Tag("region:ruin:fight");
            f.Value("RUINNAME", best.name);
            if (best.people != null) f.Value("RUINPEOPLE", best.people.Name);
        }

        /// <summary>
        /// What brought a traveller of a camp at the colony (Camps/CampSeekers.cs): them:seeking and them:seeking:REASON
        /// (TravelMath.Reasons: relic, wonder, trees, ruins, ruinRegion, library, history, ore, rush, grazing, road, far), with
        /// {SEEK} what they came for in words, as it was when they came ("the old ruins to the north").
        /// </summary>
        private static void Seeking(FactScene s, FactSet f)
        {
            var x = s.listener;
            if (x == null || !x.Spawned) return;
            var c = CampTracker.CampOf(x);
            if (c == null || c.seekWhat.NullOrEmpty() || !TravelMath.Known(c.seeking)) return;
            f.Tag("them:seeking", 1.5f);
            f.Tag("them:seeking:" + c.seeking, 1.5f);
            f.Value("SEEK", c.seekWhat);
        }

        /// <summary>
        /// Their town in its first year, raised on a ruin (World/Region/NewTowns.cs): town:resettled:own when their own people
        /// came back to their old walls, town:resettled:newcomers when they took over another people's, as the history's
        /// entry of its resettling tells (the old town's people named beside the settlers when they differ).
        /// </summary>
        private static void Resettled(FactScene s, FactSet f, int now)
        {
            if (s.place == null) return;
            var e = History.Latest(RiseFallWords.ResettledKind, Ref.None, Refs.Of(s.place));
            if (e?.actors == null || !RegionMath.IsNew(e.tick, now)) return;
            var settlers = e.actors.FirstOrDefault(a => a.role == HistoryRole.Did).who;
            bool others = e.actors.Any(a => a.role == HistoryRole.About && a.who.kind == RefKind.People && a.who != settlers);
            f.Tag(others ? "town:resettled:newcomers" : "town:resettled:own", FactsMath.NewTownHeat(Days(e.tick)));
        }

        /// <summary>
        /// A town of their people given up lately (its ruin made in the last 30 days, the region keeping it): their:givenup
        /// with {GIVENUP} its name, for after the world event that gave it up has ended and its {SITUATIONPLACE} with it.
        /// </summary>
        private static void GivenUp(FactScene s, FactSet f)
        {
            var l = WorldLedger.Instance;
            if (l == null || s.people == null) return;
            LedgerRuin best = null;
            foreach (var r in l.Ruins)
                if (r != null && r.people == s.people && r.why == LedgerRuin.GivenUp && !r.name.NullOrEmpty() && r.since >= 0 && Days(r.since) <= FactsMath.LatelyDays
                    && (best == null || r.since > best.since))
                    best = r;
            if (best == null) return;
            f.Tag("their:givenup", Fresh(Days(best.since), FactsMath.LatelyDays, 2f));
            f.Value("GIVENUP", best.name);
        }

        /// <summary>
        /// A world event running at their own town (its place is the town), whichever is their people's newest ({SITUATION}
        /// names that one, wherever it is): town:situation, town:situation:bad or town:situation:good, with {TOWNSITUATION}
        /// its name ("the gray cough", "the harvest fair"). Not the giving up of the town itself.
        /// </summary>
        private static void TownSituation(FactScene s, FactSet f)
        {
            var ws = WorldSituations.Instance;
            if (ws == null || s.place == null) return;
            var place = Refs.Of(s.place);
            WorldSituation best = null;
            foreach (var x in ws.Running)
                if (x?.def != null && x.ctx != null && x.place == place && !(x.def.Worker is SituationWorker_Abandoned) && (best == null || x.startTick > best.startTick))
                    best = x;
            if (best == null) return;
            float h = Fresh(best.DaysIn, 12f, 2.5f);
            f.Tag("town:situation", h);
            f.Tag(best.def.bad ? "town:situation:bad" : "town:situation:good", h);
            f.Value("TOWNSITUATION", Texts.WithThe(best.ctx.name, best.def.label));
        }
    }
}
