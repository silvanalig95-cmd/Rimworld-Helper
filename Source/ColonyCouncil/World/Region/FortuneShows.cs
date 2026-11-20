using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A town's fortune shows on its people (the content update's B12, 20 November 2026): visitors, traders and their
    /// guards, and camps from a people whose town nearest the colony is struggling come in clothes worn thin (their own
    /// hit points lower) with one pack beast fewer, and one of a visiting group asks for a meal once someone of the colony
    /// is near; from a flourishing town they come a quality step better dressed, and one of them more often brings a small
    /// gift, carried in and set down at the colony. The town's card already says "Struggling, since the plague"; the gate
    /// now shows it too. Only the two ends of the fortunes show (FortuneShowsMath.Shows), only with the region's fortunes
    /// on. Who asked for a meal is the session's only: a reload lets an ask lapse.
    /// </summary>
    public static class FortuneShows
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public static bool On => RegionBook.FortunesOn;

        /// <summary>The town of this people a group from them comes from: their nearest to the colony, with its fortune; null when it shows nothing.</summary>
        public static Settlement TownFor(Faction f, Map map, out Fortune fortune)
        {
            fortune = Fortune.Steady;
            if (!On || f == null || f.IsPlayer || map == null || !FactionMemory.Remembers(f)) return null;
            var town = WorldSituationWorker.NearestTown(f, map);
            if (town == null) return null;
            var band = RegionFortunes.Of(town.ID);
            if (band == null || !FortuneShowsMath.Shows(band.Value)) return null;
            fortune = band.Value;
            return town;
        }

        /// <summary>The town's line as its card has it ("struggling, since the plague"), or the band alone.</summary>
        public static string TownLine(Settlement town, Fortune fortune)
        {
            var rec = town != null ? WorldLedger.TownRecord(town.ID) : null;
            string line = rec != null ? RegionFortunes.Line(rec) : null;
            return (line ?? RegionFortunes.Word(fortune, town?.Faction)).UncapitalizeFirst();
        }

        // ================= Dressed by their year =================

        /// <summary>
        /// Their clothes by their town's fortune: worn thin from a struggling town (each garment's hit points down to 40 to
        /// 65% of its most), a quality step better from a flourishing one (never past masterwork). True when something changed.
        /// </summary>
        public static bool Dress(IEnumerable<Pawn> people, Fortune fortune)
        {
            bool any = false;
            foreach (var p in people ?? Enumerable.Empty<Pawn>())
            {
                if (p?.apparel == null || p.RaceProps?.Humanlike != true) continue;
                foreach (var a in p.apparel.WornApparel.ToList())
                {
                    try
                    {
                        if (fortune == Fortune.Struggling)
                        {
                            if (!a.def.useHitPoints) continue;
                            int hp = FortuneShowsMath.WornHitPoints(a.HitPoints, a.MaxHitPoints, Rand.Value);
                            if (hp < a.HitPoints)
                            {
                                a.HitPoints = hp;
                                any = true;
                            }
                        }
                        else if (fortune == Fortune.Flourishing)
                        {
                            var q = a.TryGetComp<CompQuality>();
                            if (q == null) continue;
                            int up = FortuneShowsMath.StepUp((int)q.Quality);
                            if (up == (int)q.Quality) continue;
                            q.SetQuality((QualityCategory)up, ArtGenerationContext.Outsider);
                            any = true;
                        }
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("a town's fortune: their clothes", e);
                    }
                }
            }
            return any;
        }

        /// <summary>One pack beast fewer from a struggling town (never the last): its load goes onto another's back. The one let go, or null.</summary>
        public static Pawn OneBeastFewer(List<Pawn> group)
        {
            var pack = group?.Where(p => p != null && !p.Dead && p.RaceProps?.packAnimal == true && !p.RaceProps.Humanlike).ToList();
            if (pack == null || FortuneShowsMath.PackBeastsKept(pack.Count) >= pack.Count) return null;
            var gone = pack.OrderBy(p => p.inventory?.innerContainer?.Count ?? 0).First();
            var other = pack.First(p => p != gone);
            try
            {
                if (gone.inventory?.innerContainer != null && other.inventory?.innerContainer != null)
                    gone.inventory.innerContainer.TryTransferAllToContainer(other.inventory.innerContainer);
                // Anything that would not go onto the other's back stays with the group: the beast stays too.
                if (gone.inventory?.innerContainer != null && gone.inventory.innerContainer.Count > 0) return null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a town's fortune: a pack beast's load", e);
                return null;
            }
            group.Remove(gone);
            return gone;
        }

        // ================= At the gate =================

        /// <summary>The ask for a meal, by visitor: the town and its line, until when it stands, and whether it was said.</summary>
        private sealed class Ask
        {
            public Pawn pawn;
            public string town, line;
            public int until;
            public bool said, fed;
        }

        private static readonly Dictionary<int, Ask> asks = new Dictionary<int, Ask>();
        private static Game asksGame;

        private static Dictionary<int, Ask> Asks
        {
            get
            {
                if (asksGame != Current.Game)
                {
                    asks.Clear();
                    asksGame = Current.Game;
                }
                return asks;
            }
        }

        /// <summary>
        /// A group of the game's own (visitors, a trader's caravan and its guards, travellers) has just come onto the map:
        /// their town's fortune shows on them. Its pawns are not in a group yet, so a pack beast let go is simply gone.
        /// </summary>
        public static void GroupCame(Faction f, Map map, List<Pawn> pawns, bool caravan)
        {
            if (pawns.NullOrEmpty() || map == null || !map.IsPlayerHome) return;
            var town = TownFor(f, map, out var fortune);
            if (town == null) return;
            var people = pawns.Where(p => p != null && p.RaceProps?.Humanlike == true).ToList();
            if (people.Count == 0) return;
            Dress(people, fortune);
            string line = TownLine(town, fortune);
            if (fortune == Fortune.Struggling)
            {
                if (caravan)
                {
                    var gone = OneBeastFewer(pawns);
                    if (gone != null)
                    {
                        if (gone.Spawned) gone.DeSpawn();
                        Find.WorldPawns.PassToWorld(gone, PawnDiscardDecideMode.Discard);
                    }
                }
                // One grown of theirs, not their trader, will ask for a meal.
                var asker = people.Where(p => !p.DevelopmentalStage.Juvenile() && p.TraderKind == null).RandomElementWithFallback();
                if (asker != null) Asks[asker.thingIDNumber] = new Ask { pawn = asker, town = town.Name, line = line, until = Now + FortuneShowsMath.AskHours * GenDate.TicksPerHour };
                return;
            }
            // Flourishing: a small gift, now and then, carried in by one of them and set down at the colony.
            if (!Rand.Chance(FortuneShowsMath.GiftChance)) return;
            var bearer = people.Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            if (bearer == null) return;
            int silver = FortuneShowsMath.GiftSilver(LivingEvents.WealthFactor(map), Rand.Value);
            var gate = PlaySpots.GateCell(map, bearer);
            if (!gate.IsValid) gate = DropCellFinder.TradeDropSpot(map);
            if (!Couriers.Deliver(map, Couriers.Make(ThingDefOf.Silver, silver), f, gate, bearer)) return;
            Tell.Message(NewsKind.World, Importance.Notable, map,
                "CC_Fortune_Gift".Translate(bearer.Named("PAWN"), f.Name.Named("PEOPLE"), town.Name.Named("TOWN"), line.Named("LINE"), silver.Named("COUNT")).Resolve(),
                bearer, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>A camp has come: their clothes show their town's year (the pack beasts are seen to as they are made: <see cref="CampAnimals"/>).</summary>
        public static void CampCame(Camp c)
        {
            if (c?.map == null) return;
            var town = TownFor(c.faction, c.map, out var fortune);
            if (town != null) Dress(c.members, fortune);
        }

        /// <summary>A camp's animals as made: one pack beast fewer from a struggling town (the one let go never came).</summary>
        public static List<Pawn> CampAnimals(List<Pawn> animals, Faction f, Map map)
        {
            if (animals.NullOrEmpty()) return animals;
            var town = TownFor(f, map, out var fortune);
            if (town == null || fortune != Fortune.Struggling) return animals;
            var list = animals.ToList();
            var gone = OneBeastFewer(list);
            if (gone != null && !gone.Spawned) Find.WorldPawns.PassToWorld(gone, PawnDiscardDecideMode.Discard);
            return list;
        }

        /// <summary>
        /// The hour (Peoples/PeoplesTies.cs): one who means to ask for a meal asks once someone of the colony is near: a word
        /// in a bubble and a line in the news. An ask unanswered lapses when its hours are up or they leave.
        /// </summary>
        public static void Hourly(int now)
        {
            if (Asks.Count == 0) return;
            foreach (var kv in Asks.ToList())
            {
                var a = kv.Value;
                var x = a.pawn;
                if (x == null || x.Dead || !x.Spawned || now > a.until || a.fed || !x.Map.IsPlayerHome)
                {
                    if (x == null || x.Dead || x.Discarded || now > a.until || a.fed && now > a.until) Asks.Remove(kv.Key);
                    continue;
                }
                if (a.said) continue;
                var near = x.Map.mapPawns.FreeColonistsSpawned.Where(c => !c.Downed && c.Position.InHorDistOf(x.Position, FortuneShowsMath.AskRange)).OrderBy(c => c.Position.DistanceToSquared(x.Position)).FirstOrDefault();
                if (near == null) continue;
                a.said = true;
                // together: asked of the colonist nearest them, within a few steps.
                Speech.Say(x, "CC_Fortune_AskLine".Translate(a.town.Named("TOWN")).Resolve(), near);
                Tell.Message(NewsKind.World, Importance.Notable, x.Map,
                    "CC_Fortune_Ask".Translate(x.Named("PAWN"), (x.Faction?.Name ?? "").Named("PEOPLE"), a.town.Named("TOWN"), a.line.Named("LINE")).Resolve(),
                    x, MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>This visitor asked the colony for a meal and has not had one (the colonists see to it first: OutsiderAuto).</summary>
        public static bool Asked(Pawn x) => x != null && Asks.TryGetValue(x.thingIDNumber, out var a) && a.said && !a.fed;

        /// <summary>
        /// Someone of the colony shares a meal with them (Outsiders/OutsiderOutcomes.cs): if they had asked, they thank them
        /// in a bubble, and their people remember the colony's kindness. False when they hadn't asked.
        /// </summary>
        public static bool Fed(Pawn x, Pawn c)
        {
            if (!Asked(x)) return false;
            var a = Asks[x.thingIDNumber];
            a.fed = true;
            try
            {
                // together: said as the meal is shared, face to face (Outsiders/OutsiderOutcomes.cs, Meal).
                Speech.Say(x, "CC_Fortune_Thanks".Translate(a.town.Named("TOWN")).Resolve(), c);
                if (x.Faction != null && FactionMemory.Remembers(x.Faction)) FactionMemory.Note(x.Faction, "gaveGift", x.LabelShort);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a town's fortune: the meal asked for", e);
            }
            return true;
        }

        /// <summary>For the dev tools: the asks standing.</summary>
        public static string Describe() => Asks.Count == 0 ? "none" : string.Join(", ", Asks.Values.Select(a => (a.pawn?.LabelShort ?? "?") + (a.said ? " (asked)" : "") + (a.fed ? " (fed)" : "")));

        /// <summary>For the dev tools: this visitor will ask for a meal now (as one from a struggling town would).</summary>
        public static void WillAsk(Pawn x, Settlement town)
        {
            if (x == null) return;
            Asks[x.thingIDNumber] = new Ask
            {
                pawn = x, town = town?.Name ?? x.Faction?.Name ?? "?", line = town != null ? TownLine(town, Fortune.Struggling) : "struggling",
                until = Now + FortuneShowsMath.AskHours * GenDate.TicksPerHour
            };
        }
    }

    /// <summary>
    /// The game's own groups of a people at peace (visitors, a trader's caravan and its guards, travellers), as they come
    /// onto the map: their town's fortune shows on them (FortuneShows.GroupCame), before they are made a group.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker_NeutralGroup), "SpawnPawns")]
    public static class Patch_FortuneShowsGroup
    {
        public static void Postfix(IncidentWorker_NeutralGroup __instance, IncidentParms parms, List<Pawn> __result)
        {
            if (!RimfolkPatches.On || __result == null || parms?.faction == null) return;
            try
            {
                FortuneShows.GroupCame(parms.faction, parms.target as Map, __result, __instance is IncidentWorker_TraderCaravanArrival);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a town's fortune at the gate", e);
            }
        }
    }
}
