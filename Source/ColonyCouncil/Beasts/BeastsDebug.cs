using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: each part of the wild's alphas and bosses made to happen now on the map on screen (Debug actions, "Rimfolk",
    /// "Beasts: ..."), the rates and waits skipped; what follows plays by the rules. And what stands now, in the log.
    /// </summary>
    public static class BeastsDebug
    {
        private static void Say(string text) => Messages.Message("Beasts: " + text, MessageTypeDefOf.SilentInput, false);

        private static int Now => Find.TickManager.TicksGame;

        private static Map Here => Find.CurrentMap;

        /// <summary>A few of a kind walked in at the map's edge, wild (or manhunting).</summary>
        private static List<Pawn> SpawnGroup(Map map, PawnKindDef kind, int n, bool manhunting)
        {
            var list = new List<Pawn>();
            if (kind == null || !RCellFinder.TryFindRandomPawnEntryCell(out var start, map, CellFinder.EdgeRoadChance_Animal)) return list;
            for (int i = 0; i < n; i++)
            {
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, map.Tile));
                GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(start, map, 6), map);
                if (manhunting) p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent, null, true);
                list.Add(p);
            }
            return list;
        }

        private static PawnKindDef PackKindHere(Map map) =>
            DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k => k?.race?.race != null && BeastsMath.PackKind(k.RaceProps.Animal, k.RaceProps.predator, k.RaceProps.Insect,
                    k.RaceProps.Dryad, k.RaceProps.baseBodySize) && !PokeWorldLink.IsPokemon(k) && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race))
                .OrderByDescending(k => k.defName == "Wolf_Timber" || k.defName == "Warg" ? 1 : 0).ThenBy(k => k.RaceProps.baseBodySize).FirstOrDefault();

        [DebugAction("Rimfolk", "Beasts: a pack led by an alpha now (wild)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAlpha()
        {
            var book = BeastBook.Instance;
            var map = Here;
            if (book == null || map == null) return;
            var pack = PackAlphas.FindPack(book, map, false) ?? SpawnGroup(map, PackKindHere(map), 4, false);
            var r = PackAlphas.Make(book, map, pack, false, Now);
            Say(r == null ? "no pack of a hunting kind here and none could be made (or nobody of the colony to name it)." : r.name + " leads " + r.pack.Count + " " + r.kind + ".");
        }

        [DebugAction("Rimfolk", "Beasts: manhunters led by an alpha now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAlphaManhunters()
        {
            var book = BeastBook.Instance;
            var map = Here;
            if (book == null || map == null) return;
            var pack = PackAlphas.FindPack(book, map, true) ?? SpawnGroup(map, PackKindHere(map), 5, true);
            var r = PackAlphas.Make(book, map, pack, true, Now);
            Say(r == null ? "no manhunting pack here and none could be made." : r.name + " leads " + r.pack.Count + " manhunting " + r.kind + ".");
        }

        [DebugAction("Rimfolk", "Beasts: the alpha tamed now (its pack follows)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAlphaTamed()
        {
            var r = BeastBook.Instance?.alphas.LastOrDefault(a => a.state == "wild" && BeastBook.Up(a.alpha));
            if (r == null)
            {
                Say("no wild alpha up on any map.");
                return;
            }
            InteractionWorker_RecruitAttempt.DoRecruit(null, r.alpha, false);
            Say(r.name + " is the colony's; the next short pass tells it, and its pack starts to come in within a day (the dev tool's time speed-ups help).");
        }

        [DebugAction("Rimfolk", "Beasts: the alpha's pack follows in now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFollowNow()
        {
            var r = BeastBook.Instance?.alphas.LastOrDefault(a => a.state == "tamed");
            if (r == null)
            {
                Say("no tamed alpha whose pack is still to come.");
                return;
            }
            r.nextFollow = Now;
            Say("the next of " + r.name + "'s pack sets off for it at the next short pass.");
        }

        [DebugAction("Rimfolk", "Beasts: a beast nemesis here now (it maimed someone)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNemesis()
        {
            var map = Here;
            var comp = CouncilGameComponent.Instance;
            var book = BeastBook.Instance;
            if (map == null || comp == null || book == null) return;
            var victim = IncidentWorker_Happening.Colonists(map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
            var x = map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == null && p.RaceProps != null && p.RaceProps.Animal && BeastBook.Up(p) && comp.nemeses.For(p) == null && p.RaceProps.baseBodySize >= 0.8f)
                .OrderByDescending(p => p.RaceProps.predator ? 1 : 0).FirstOrDefault() ?? SpawnGroup(map, PackKindHere(map), 1, false).FirstOrDefault();
            if (victim == null || x == null)
            {
                Say("needs a grown colonist and a wild animal here.");
                return;
            }
            if (x.Name == null) x.Name = new NameSingle(BeastBook.NameFrom("Council_Pool_BeastNames", victim, "predator") ?? "Old Scar");
            var n = comp.nemeses.Add(x, victim, "maimed", false);
            if (n == null)
            {
                Say("nemeses are switched off.");
                return;
            }
            // Scarred and sworn against now, as the hour would (and not again by it).
            book.noted.Add(x.thingIDNumber);
            if (BeastsDefOf.Rimfolk_BeastScars != null && x.health.hediffSet.GetFirstHediffOfDef(BeastsDefOf.Rimfolk_BeastScars) == null) x.health.AddHediff(BeastsDefOf.Rimfolk_BeastScars);
            var o = BeastNemeses.Swear(book, comp, n, Now);
            Say(x.LabelShort + " is " + victim.LabelShort + "'s nemesis (maimed), scarred" + (o != null ? "; " + o.sworn.LabelShort + " swore to bring it down" : "; nobody close enough to swear") + ".");
        }

        [DebugAction("Rimfolk", "Beasts: the beast nemesis here flees now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFlee()
        {
            var comp = CouncilGameComponent.Instance;
            var x = comp?.nemeses.all.Where(n => !n.ended && n.IsBeast && BeastBook.Up(n.pawn) && n.pawn.Map == Here).Select(n => n.pawn).FirstOrDefault();
            if (x == null)
            {
                Say("no beast nemesis up on this map.");
                return;
            }
            // Hurt as a fight would leave it: a few cuts, under the share it runs at.
            for (int i = 0; i < 12 && x.health.summaryHealth.SummaryHealthPercent >= BeastsMath.FleeBelow && !x.Downed && !x.Dead; i++)
                x.TakeDamage(new DamageInfo(DamageDefOf.Cut, 6f));
            Say(x.Dead || x.Downed ? x.LabelShort + " fell instead." : x.LabelShort + " is hurt; it turns to run at the next short pass.");
        }

        [DebugAction("Rimfolk", "Beasts: the beast nemesis away comes back now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReturn()
        {
            var f = BeastBook.Instance?.flights.FirstOrDefault(fl => fl.state == "away");
            if (f == null)
            {
                Say("no beast nemesis away in the wild.");
                return;
            }
            f.returnAt = Now;
            Say((f.beast?.LabelShort ?? "?") + " comes back at the next hour (one time in three for blood).");
        }

        [DebugAction("Rimfolk", "Beasts: a legend of the region now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLegend()
        {
            var map = Homes.IsHome(Here) ? Here : Homes.Seat;
            var s = WorldSituations.Instance?.TryStart(map, d => d.Worker is LegendWorker, true);
            Say(s == null ? "no legend could start (one runs already, no beast fits the region, or no free ground within reach)." : (s.ctx.name ?? "?") + ", a " + (s.ctx.beast ?? "?") + ", roams the region.");
        }

        [DebugAction("Rimfolk", "Beasts: the legend moves on now (a sighting)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLegendMove() => Say(LegendWorker.DebugMove() ? "it moved on; its new ground is on the world map." : "no legend roaming, or it couldn't move (a caravan on its way, no ground).");

        [DebugAction("Rimfolk", "Beasts: the legend walks onto this map now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLegendHere()
        {
            var s = LegendWorker.Running();
            var x = s?.ctx.stranger;
            var map = Here;
            if (x == null || x.Dead || x.Spawned || map == null || !RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Animal))
            {
                Say("no legend out in the world to bring here.");
                return;
            }
            LegendWorker.GroundOf(s)?.Destroy();
            s.SetNum("site", 0);
            if (Find.WorldPawns.Contains(x)) Find.WorldPawns.RemovePawn(x);
            GenSpawn.Spawn(x, cell, map);
            Say(x.LabelShort + " is at the edge of this map (as on its ground): hunt it or tame it to try the endings.");
        }

        [DebugAction("Rimfolk", "Beasts: a great herd crosses here now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHerd()
        {
            var herd = GreatHerds.Start(Here);
            Say(herd == null || herd.Count == 0 ? "no herd animal fits this map now, or no way across." : herd.Count + " " + herd[0].kindDef.GetLabelPlural() + " walk in.");
        }

        [DebugAction("Rimfolk", "Beasts: the herd here stampedes now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugStampede() => Say(GreatHerds.DebugStampede(Here) ? "they run (away from the map's middle)." : "no herd of four or more crossing this map.");

        [DebugAction("Rimfolk", "Beasts: what stands now (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLog()
        {
            var book = BeastBook.Instance;
            var map = Here;
            if (book == null) return;
            var S = ColonyCouncilMod.Settings;
            var sb = new StringBuilder("Rimfolk, the wild's alphas and bosses:\n");
            sb.AppendLine($"Settings: alphas {S.packAlphas:P0}, beast nemeses {S.beastNemeses:P0}, legends {S.regionLegends:P0}, great herds {S.greatHerds:P0}.");
            foreach (var r in book.alphas)
                sb.AppendLine($"Alpha {r.name} ({r.one}), {r.state}, pack {r.pack.Count}, made day {r.madeTick / GenDate.TicksPerDay}" + (r.state == "tamed" ? $", {r.followed} came in, next of the pack #{r.followIndex}" : "")
                              + (r.manhunters ? ", manhunters" : "") + (r.threatTold ? ", letter sent" : ""));
            foreach (var f in book.flights)
                sb.AppendLine($"Flight: {f.beast?.LabelShort ?? "?"} {f.state}, fled {f.flights} times, back {f.returns} times, comes back day {(f.returnAt < 0 ? -1 : f.returnAt / GenDate.TicksPerDay)}");
            foreach (var o in book.oaths)
                sb.AppendLine($"Oath: {o.sworn?.LabelShort ?? "?"} against {o.beastName} for {o.victimName}" + (o.ended ? ", ended: " + o.how : ", open"));
            var bh = map != null ? book.HomeOf(map) : null;
            if (bh != null)
                sb.AppendLine($"This home: last alpha day {(bh.lastAlpha < 0 ? -1 : bh.lastAlpha / GenDate.TicksPerDay)}, great herd this season {(bh.herdAt < 0 ? "no" : "day " + bh.herdAt / GenDate.TicksPerDay)}; pack here: {PackAlphas.FindPack(book, map, false)?.Count ?? 0}; herd kind: {GreatHerds.PickKind(map)?.label ?? "none"}.");
            sb.AppendLine("Herds: " + GreatHerds.Describe());
            var kinds = map != null ? LegendWorker.Kinds(map) : new List<PawnKindDef>();
            sb.AppendLine($"Legends: due {BeastsMath.LegendDue(S.regionLegends, Now, book.legendEnded)}, last ended day {(book.legendEnded < 0 ? -1 : book.legendEnded / GenDate.TicksPerDay)}, PokéWorld's legendaries open {PokeWorldLegends.Open()}; could be: "
                          + string.Join(", ", kinds.Select(k => k.label + " (" + k.RaceProps.baseBodySize.ToString("0.#") + ")")));
            var s = LegendWorker.Running();
            if (s != null) sb.AppendLine($"Legend now: {s.ctx.name}, a {s.ctx.beast}, ground {(LegendWorker.GroundOf(s)?.Tile.ToString() ?? "none")}, seen {s.Num("seen")} times, bounty {s.Num("bounty")}.");
            Log.Message(sb.ToString());
            Say("written to the log.");
        }
    }
}
