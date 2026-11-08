using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>How to reach a town now, on its card (docs/design/post-and-letters.md, step 1, point 9).</summary>
    [StaticConstructorOnStartup]
    public static class PostCard
    {
        static PostCard()
        {
            CardSections.TownLines(CardSlot.Who, 5f, s => PostWords.ReachLines(Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat ?? HappeningTracker.HomeMap(), s),
                "CC_Mail_Reach_Header", "post-reach");
        }
    }

    /// <summary>
    /// Dev mode (Debug actions, "Rimfolk"): the post rider now; any group of outsiders on the map leaves now carrying the
    /// letters going their way; every town's reach and the outbox in the log; the mail waiting brought by a messenger now.
    /// </summary>
    public static class PostDebug
    {
        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        [DebugAction("Rimfolk", "Post: the post rider now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugRiderNow()
        {
            bool ok = PostRider.ComeNow(Home);
            Log.Message("[Rimfolk] Post: " + (ok ? "the post rider is on the way to the mailbag." : "no post rider could come (one is here already, nobody at peace, or no way in)."));
        }

        [DebugAction("Rimfolk", "Post: a visitor group leaves now with the mail...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugGroupLeaves()
        {
            var map = Home;
            var opts = new List<FloatMenuOption>();
            foreach (var lord in map.lordManager.lords.ToList())
            {
                if (lord?.faction == null || lord.faction.IsPlayer || lord.faction.HostileTo(Faction.OfPlayer) || lord.LordJob is LordJob_PostRider) continue;
                var people = lord.ownedPawns.Where(MailWatch.Able).ToList();
                if (people.Count == 0) continue;
                var l = lord;
                opts.Add(new FloatMenuOption(lord.faction.Name + ": " + lord.LordJob.GetType().Name + " (" + people.Count + ")", () =>
                {
                    try
                    {
                        var toil = l.Graph.lordToils.FirstOrDefault(t => t is LordToil_ExitMap || t.GetType().Name.Contains("Exit"));
                        if (toil != null) l.GotoToil(toil);
                        else Arrivals.SendAway(l.ownedPawns.ToList());
                        Log.Message("[Rimfolk] Post: " + l.faction.Name + " leave; the watch gives one of them the letters going their way within a few seconds.");
                    }
                    catch (Exception e)
                    {
                        Log.Warning("[Rimfolk] Post: couldn't send them off: " + e.Message);
                    }
                }));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("(no outsiders at peace on the map)", null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        [DebugAction("Rimfolk", "Post: every town's reach and the outbox (log)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugReach()
        {
            var map = Home;
            PostReach.Reset();
            var lines = new List<string> { "[Rimfolk] Post at " + map + ": the post's age " + PostRider.AgeOf(map) + (PostRider.Runs(map) ? ", a post runs" : ", no post")
                                           + "; range " + PostMath.RangeDays(Ages.Ours, PostReach.Mast(map)) + " days by " + PostReach.ColonyMeans(map) };
            foreach (var s in NotableBook.Instance?.AllTowns(map) ?? new List<Settlement>())
            {
                var i = PostReach.Of(map, s);
                lines.Add($"  {s.Name} ({s.Faction?.Name}): {i.tiles:0} tiles, walk {i.walkDays:0.0} days, ours {i.days:0.0} days, {i.band}"
                          + (i.relay != null ? $" by relay through {i.relay.Name} ({i.relayDays:0.0} days)" : "") + (i.radio ? ", radio" : "") + (i.call ? ", call now" : ""));
                foreach (var line in PostWords.ReachLines(map, s)) lines.Add("      " + line);
            }
            lines.Add("  Outbox:");
            foreach (var line in PostWords.OutboxLines(map)) lines.Add("    " + line);
            foreach (var i in PostBook.Instance?.incoming ?? new List<PostItem>())
                lines.Add($"  Mail in: {i.label} from {i.from?.Name}, {(i.carrier != null ? "with " + i.carrier.LabelShort : "waiting")}, {i.urgency}");
            Log.Message(string.Join("\n", lines));
        }

        [DebugAction("Rimfolk", "Post: the mail waiting brought by a messenger now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMessenger()
        {
            foreach (var i in PostBook.Instance?.incoming.Where(x => x.carrier == null).ToList() ?? new List<PostItem>()) MailIn.Messenger(i);
        }

        [DebugAction("Rimfolk", "Post: the mail on the road is here now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugMailHere()
        {
            int now = Find.TickManager.TicksGame;
            foreach (var i in PostBook.Instance?.incoming ?? new List<PostItem>()) i.arrive = Math.Min(i.arrive, now);
            foreach (var l in NotableBook.Instance?.letters.Where(x => x.stage == LetterStage.Travelling).ToList() ?? new List<NeighbourLetter>()) l.due = now;
        }
    }
}
