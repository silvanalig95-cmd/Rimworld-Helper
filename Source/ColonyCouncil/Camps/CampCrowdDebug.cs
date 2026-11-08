using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode (Debug actions, "Rimfolk"): camps side by side, bands against a camp, old camps' sites. A second camp beside
    /// those here (the level's count and the rest after one leaves skipped, its site kept apart), a band sent against a
    /// camp now, an old camp's site left standing here now, a rival sent to an old site, and the log of it all. What
    /// follows plays by the rules.
    /// </summary>
    public static class CampCrowdDebug
    {
        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static void Say(string text) => Messages.Message("Camps: " + text, MessageTypeDefOf.SilentInput, false);

        [DebugAction("Rimfolk", "Camps: another camp here now, beside those here", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAnother()
        {
            var map = Home;
            var tracker = CampTracker.Instance;
            if (map == null || tracker == null || !CampTracker.Enabled)
            {
                Say("camps are off.");
                return;
            }
            var why = new List<string>();
            var setup = tracker.Prepare(map, why);
            if (setup == null)
            {
                Say("none could set up: " + string.Join("; ", why.Distinct()));
                return;
            }
            var c = tracker.Start(setup, true);
            if (c == null)
            {
                CampTracker.Discard(setup);
                Say("they couldn't walk in (no way in from an edge).");
                return;
            }
            CampTracker.AnnounceCamp(c);
            Say(c.Label + " of " + c.faction.Name + " on the way" + (setup.ground != null ? ", into " + setup.ground.name : "") + "; " + CampCrowds.HereAt(map) + " camp(s) here now.");
        }

        [DebugAction("Rimfolk", "Camps: send a band against a camp now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugBand()
        {
            var map = Home;
            var camps = CampTracker.VisitorsAt(map).Where(c => c.Settled && !c.Hostile).ToList();
            if (camps.Count == 0)
            {
                Say("no camp settled here to come against.");
                return;
            }
            var options = new List<FloatMenuOption>();
            foreach (var camp in camps)
            {
                var c = camp;
                var g = CampGrounds.ById(c.groundId);
                foreach (var (f, facts) in CampRaids.Attackers(c, g, true))
                {
                    var people = f;
                    options.Add(new FloatMenuOption(c.Label + " (" + c.faction.Name + "): a band of " + people.Name + (facts.hostileToColony ? ", our enemies too" : ", at peace with us")
                                                    + (facts.heldSiteBefore ? ", who held its site" : ""), () =>
                    {
                        var b = CampRaids.Send(c, people, false, true);
                        Say(b != null ? b.came + " of " + people.Name + " gathering at the edge against the " + c.Label + "." : "they couldn't come (no fighters made, or no way in).");
                    }));
                }
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no people at war with a camp's people can send fighters)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Camps: leave an old camp's site here now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLeaveSite()
        {
            var map = Home;
            var tracker = CampTracker.Instance;
            if (map == null || tracker == null)
            {
                Say("no home.");
                return;
            }
            var options = new List<FloatMenuOption>();
            foreach (var k in DefDatabase<CampKindDef>.AllDefs.Where(k => k.FitsEra() && !k.fights && k.leaves == CampLeaves.Standing).OrderBy(k => k.label))
            {
                var kind = k;
                options.Add(new FloatMenuOption(kind.label, () =>
                {
                    var f = Arrivals.Friendly(x => kind.FitsFaction(x));
                    if (f == null)
                    {
                        Say("no friendly people for it.");
                        return;
                    }
                    if (!CampSite.TryFind(map, out var center))
                    {
                        Say("no spot for a camp.");
                        return;
                    }
                    var plans = CampSite.Layout(map, ref center, kind, kind.people.max, CampSite.ColonyCenter(map), out var stash, out var share, false, f, kind.days.max);
                    if (plans == null)
                    {
                        Say("its layout doesn't fit there.");
                        return;
                    }
                    // What a camp would leave standing as it goes: its constructions, nobody's (CampPack.cs); its beds' places kept.
                    int built = 0;
                    foreach (var p in plans.Where(p => p.def != null && !p.def.Minifiable && p.role != "bed" && p.role != "hutbed" && p.role != "tent"))
                    {
                        if (!GenConstruct.CanPlaceBlueprintAt(p.def, p.cell, p.rot, map, false, null, null, p.stuff).Accepted) continue;
                        var thing = ThingMaker.MakeThing(p.def, p.def.MadeFromStuff ? p.stuff ?? GenStuff.DefaultStuffFor(p.def) : null);
                        thing.SetFaction(null);
                        GenSpawn.Spawn(thing, p.cell, map, p.rot, WipeMode.VanishOrMoveAside);
                        built++;
                    }
                    var home = tracker.HomeOf(map);
                    var g = new CampGround
                    {
                        id = tracker.nextId++, map = map, center = plans.FirstOrDefault(p => p.role == "fire")?.cell ?? center, stash = stash, share = share,
                        name = "CC_CampGround_Name_kind".Translate(kind.label.Named("KIND")).Resolve(), plans = plans, leftTick = Find.TickManager.TicksGame
                    };
                    g.held.Add(new CampHold { faction = f, people = f.Name, kind = kind.label, from = GenDate.DaysPassed - 3, to = GenDate.DaysPassed, how = "left" });
                    home.grounds.Add(g);
                    Say(g.name + " left standing: " + built + " piece(s) of the " + kind.label + " of " + f.Name + ".");
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(no kind of camp fits the era)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Camps: send a rival to an old camp's site...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRival()
        {
            var map = Home;
            var tracker = CampTracker.Instance;
            var sites = CampGrounds.On(map).ToList();
            if (tracker == null || sites.Count == 0)
            {
                Say("no old camp's site here (leave one first).");
                return;
            }
            var options = new List<FloatMenuOption>();
            foreach (var site in sites)
            {
                var g = site;
                var holder = g.holder >= 0 ? tracker.ById(g.holder) : null;
                if (holder != null && holder.Active && holder.Settled)
                {
                    // Held now: a rival of a people at war with the holders comes to claim it.
                    options.Add(new FloatMenuOption(g.name + ": a rival comes for it from the " + holder.Label, () =>
                    {
                        var b = CampRaids.Send(holder, null, true, true);
                        Say(b != null ? b.came + " of " + b.people.Name + " come for " + g.name + "." : "no people at war with the " + holder.faction.Name + " can send fighters.");
                    }));
                    continue;
                }
                // Free: a camp moves into it now, and a second party comes for it the same day.
                options.Add(new FloatMenuOption(g.name + ": a camp moves in, and a second party comes for it", () =>
                {
                    var why = new List<string>();
                    var kind = DefDatabase<CampKindDef>.AllDefs.Where(k => k.FitsEra() && k.Possible && !k.fights && k.leaves == CampLeaves.Standing && k.needs.NullOrEmpty())
                        .RandomElementWithFallback();
                    var f = kind == null ? null : Arrivals.Friendly(x => kind.FitsFaction(x) && CampCrowds.AtPeaceHere(map, x));
                    var setup = f == null ? null : tracker.Make(map, g.center, CampSite.ColonyCenter(map), kind, f, false, why, null, null, g);
                    var c = setup == null ? null : tracker.Start(setup, true);
                    if (c == null)
                    {
                        if (setup != null) CampTracker.Discard(setup);
                        Say("no camp could move in: " + string.Join("; ", why));
                        return;
                    }
                    CampTracker.AnnounceCamp(c);
                    var rival = CampRaids.Attackers(c, g, true).Select(x => x.f).FirstOrDefault() ?? Arrivals.Friendly(x => x != c.faction && CampCrowds.AtPeaceHere(map, x));
                    if (rival == null)
                    {
                        Say(c.Label + " moving into " + g.name + "; no second party could come.");
                        return;
                    }
                    c.rivalPeople = rival;
                    c.rivalAt = Find.TickManager.TicksGame + GenDate.TicksPerHour;
                    Say(c.Label + " of " + f.Name + " moving into " + g.name + "; " + rival.Name + (rival.HostileTo(f) ? " (at war with them) come to blows once they settle." : " come too, and give way."));
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Camps: the camps, old sites and bands (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugLog()
        {
            var map = Home;
            var lines = new List<string>
            {
                "How many camps: " + CampCrowds.Level + "; this map (" + map.Size.x + " by " + map.Size.z + ", " + (CampSite.OpenShare(map) * 100f).ToString("0") + "% open) takes "
                + CampCrowds.CapAt(map) + " at once; daily chance " + (TravelLand.CampChance(map) * 100f).ToString("0.0") + "%, rest after one leaves " + CampCrowdMath.GapDays(CampCrowds.Level) + " days."
            };
            foreach (var c in CampTracker.VisitorsAt(map))
                lines.Add("  " + c.Label + " of " + c.faction?.Name + ": " + c.phase + " at " + c.FireCell + (c.groundId >= 0 ? ", at " + CampGrounds.ById(c.groundId)?.name : "")
                          + (c.raidAt >= 0 ? ", a band fated at tick " + c.raidAt : c.raidRolled ? ", no band fated" : "") + (c.guarding ? ", on guard" : ""));
            foreach (var b in CampRaids.On(map))
                lines.Add("  band of " + b.people?.Name + " (" + b.Here.Count + " of " + b.came + ") against camp " + b.campId + ": " + (b.Moving ? "moving" : "gathering") + ", answer " + b.answer
                          + (b.warned ? ", word reached them" : "") + (b.stood ? ", they stand" : "") + ", helpers " + b.helpers.Count + (b.hostile ? ", our enemies too" : ""));
            foreach (var g in CampGrounds.On(map))
            {
                var f = CampGrounds.Facts(g);
                lines.Add("  " + CampGrounds.HistoryLine(g) + " [" + f.standing + " of " + f.pieces + " standing, " + f.beds + " beds, worth " + CampGroundMath.Worth(f, 1) + "]");
            }
            Log.Message("[Rimfolk] " + string.Join("\n", lines));
            Say("written to the log.");
        }
    }
}
