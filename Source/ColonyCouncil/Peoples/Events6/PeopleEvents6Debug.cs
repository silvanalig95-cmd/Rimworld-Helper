using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: the peoples' own events, round 6 ("Rimfolk", "Peoples' events 6: ..."). What the record keeps; one of their
    /// camps here now (the storm tower, the camp after the fire, the company on the march), whatever its own trigger; the
    /// storm-walkers now (a heat wave started for them, if no dry spell is on); a drone held by the watch now; the thorn-guards
    /// to a fire now (one started in the trees, if none burns); the camp after the fire due here now; the company in beside the
    /// colony's people here now (a waiting contract); an old soldier's muster due now; a raid held for the Banner now; the
    /// storm tower or the company turns now. The drawn events are fired with the game's own incident tool
    /// (Rimfolk_Evt_OldWoodSeeds and the rest).
    /// </summary>
    public static class PeopleEvents6Debug
    {
        private static void Say(string text) => Messages.Message("Peoples' events 6: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Peoples' events 6: what the record keeps (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugState()
        {
            var b = PeopleEvents6Book.Instance;
            if (b == null)
            {
                Say("no record in this game.");
                return;
            }
            var sb = new StringBuilder("[Rimfolk] Peoples' events 6: ");
            sb.AppendLine($"last storm-walkers {b.lastRain}, thorn-guards {b.lastCanopy}, camp after the fire {b.lastAfter}, hired sergeant {b.lastHire}; dry spells rolled {string.Join(", ", b.rainRolled)}; "
                          + $"fires rolled {string.Join(", ", b.canopyRolled)}; burns seen {string.Join(", ", b.burnSeen)}; blights rolled {string.Join(", ", b.blightRolled)}.");
            foreach (var r in b.tides)
                sb.AppendLine($"  tide {r.def} on {r.map?.Parent?.Label ?? "?"}: touches {(Gender)r.gender}, due {r.due}{(r.came ? ", came" : "")}{(r.done ? ", done" : "")}.");
            foreach (var r in b.afters)
                sb.AppendLine($"  camp after the fire asked of {r.faction?.Name ?? "?"}: due {r.due}{(r.done ? ", done" : "")}.");
            foreach (var r in b.contracts)
                sb.AppendLine($"  contract {r.id} of {r.faction?.Name ?? "?"} on {r.targetName ?? "?"} ({(r.town ? "town" : "camp")}, {r.enemy?.Name ?? "?"}): {r.stage}, price {r.price}{(r.upFront ? " up front" : "")}, "
                              + $"company {r.size}, due {r.due}, lost {string.Join(", ", r.lost)}, took {r.took}.");
            foreach (var r in b.veterans)
                sb.AppendLine($"  old soldier {r.pawn?.LabelShort ?? "?"} of {r.faction?.Name ?? "?"}: next muster {r.next}, called {r.called}{(r.bought ? ", bought out" : "")}{(r.done ? ", done" : "")}.");
            foreach (var r in b.hires)
                sb.AppendLine($"  raid {r.def} hired by {r.enemy?.Name ?? "?"} ({r.faction?.Name ?? "?"}): {r.choice}, {r.points:0} points, due {r.due}{(r.came ? ", came" : "")}{(r.done ? ", done" : "")}.");
            var kinds = new[] { DroughtBroken.Kind, TideRead.RunnerKind, CanopyFire.Kind };
            var visits = CouncilGameComponent.Instance?.happenings?.visits?.Where(v => v != null && !v.done && kinds.Contains(v.kind)).ToList();
            if (visits != null)
                foreach (var v in visits)
                    sb.AppendLine($"  visit {v.kind}: {v.x?.LabelShort ?? "-"}, stake {v.stake}, due {v.due}, until {v.until}, {v.text}.");
            var map = Find.CurrentMap;
            if (map != null)
            {
                var high = PeopleEvents6.HighGround(map, out bool rocky);
                var grower = PeoplesD.Best(map, SkillDefOf.Plants);
                var banner = PeoplesD.Friendly(PeoplesMath.Mercenaries);
                sb.AppendLine($"  here: dry spell {DroughtBroken.DrySpell(map)?.def.defName ?? "none"}, can rain {DroughtBroken.CanRain(map)}; high ground {high} ({(rocky ? "by the rocks" : "open")}); "
                              + $"stormy land {StormTower.Stormy(map)}; drone {TideRead.Drone(map)?.gender.ToString() ?? "none"}; fires in the trees {CanopyFire.Canopy(map).Count}; "
                              + $"burned trees {AfterFire.Burned(map, IntVec3.Invalid, 0f).Count}, blighted {AfterFire.Blighted(map, IntVec3.Invalid, 0f).Count}; "
                              + $"seeds for {grower?.LabelShort ?? "nobody"}: {OldWoodSeeds.Pick(map, grower)?.defName ?? "none"}; "
                              + $"enemy camp {BannerContract.Target(map, banner, out bool town)?.LabelCap ?? "none"}{(town ? " (town)" : "")}; war-horse {OldSoldier.Mount(banner)?.defName ?? "none"}; "
                              + $"the company would march on {CompanyMarch.Against(banner)?.Name ?? "nobody"}.");
            }
            Log.Message(sb.ToString());
            Say("listed in the log.");
        }

        [DebugAction("Rimfolk", "Peoples' events 6: one of their camps here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCamp()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (string n in new[] { "Rimfolk_Camp_StormTower", "Rimfolk_Camp_AfterFire", "Rimfolk_Camp_CompanyMarch" })
            {
                var kind = DefDatabase<CampKindDef>.GetNamedSilentFail(n);
                if (kind == null) continue;
                options.Add(new FloatMenuOption(kind.label, () =>
                {
                    var f = Arrivals.Friendly(x => kind.FitsFaction(x));
                    if (f == null || !CampTracker.Enabled)
                    {
                        Say("no camp: " + (f == null ? "no people of that calling at peace" : "camps are off"));
                        return;
                    }
                    var ctx = new HappeningContext { map = map, faction = f, count = 10, enemy = CompanyMarch.Against(f) };
                    var c = n == "Rimfolk_Camp_StormTower" ? StormTower.Start(ctx, false) : n == "Rimfolk_Camp_AfterFire" ? AfterFire.Start(ctx, false) : CompanyMarch.Start(ctx);
                    Say(c != null ? kind.label + " of " + f.Name + " on the way." : "no camp: no spot for it here (the camp after the fire needs a burn or a blight).");
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Peoples' events 6: the storm-walkers now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRain() => Say(DroughtBroken.Now_(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 6: a drone held by the watch now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTide() => Say(TideRead.Soon(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 6: the thorn-guards to a fire now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugCanopy() => Say(CanopyFire.Now_(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 6: the camp after the fire due here now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAfter() => Say(AfterFire.Soon(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 6: the contract's company in beside our people here now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugContract() => Say(BannerContract.Here(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 6: an old soldier's muster due now", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugMuster() => Say(OldSoldier.Soon());

        [DebugAction("Rimfolk", "Peoples' events 6: a raid held for the Banner now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugHire() => Say(OtherSideContract.Soon(Find.CurrentMap));

        [DebugAction("Rimfolk", "Peoples' events 6: a camp of theirs turns now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTurn()
        {
            var map = Find.CurrentMap;
            var tower = StormTower.On(map);
            var after = AfterFire.On(map);
            var march = CompanyMarch.On(map);
            var options = new List<FloatMenuOption>();
            if (StormTower.StateOf(tower) is TowerState ts)
            {
                options.Add(new FloatMenuOption("storm tower: a strike past the rods (in a storm, at its chance)", () =>
                {
                    StormTower.Strike(tower, ts);
                    Say(ts.struck ? "a bolt came down past the rods." : "no strike (the shared harm budget said no, or no open ground with something to burn).");
                }));
                options.Add(new FloatMenuOption("storm tower: the apprentice's chill (tonight's roll)", () =>
                {
                    ts.chillRolled = false;
                    Say("rolled at night after the second day, in rain or below ten degrees, at its chance.");
                }));
            }
            if (AfterFire.StateOf(after) is AfterState ast)
                options.Add(new FloatMenuOption("after the fire: the fire starts again (the day's roll)", () =>
                {
                    ast.rekindleRolled = false;
                    Say("rolled by day in dry, warm weather, at its chance.");
                }));
            if (CompanyMarch.StateOf(march) is MarchState ms)
            {
                options.Add(new FloatMenuOption("company: the enemy catches them (at its chance)", () =>
                {
                    CompanyMarch.Caught(march, ms);
                    Say(ms.caught ? "a band of " + (ms.enemy?.Name ?? "?") + " is coming for the camp." : "no band (no fights allowed now, or the dice said no).");
                }));
                options.Add(new FloatMenuOption("company: the bet at the fire (tonight's roll)", () =>
                {
                    ms.betRolled = false;
                    Say("rolled in the evening with one of the colony at their fire.");
                }));
            }
            if (options.Count == 0)
            {
                Say("none of their camps here.");
                return;
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}
