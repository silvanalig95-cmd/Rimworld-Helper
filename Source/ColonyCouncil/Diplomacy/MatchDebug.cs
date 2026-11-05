using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force each part of a marriage alliance at the home on screen (Debug actions, "Rimfolk"): their offer now; a
    /// match agreed now with one of a people's notables (either way of living); their kin (or the wedding's guests) due now;
    /// the wedding at their town now; a marriage's day now (a kin's gift, a warning, the pact); a spouse wronged; a death
    /// with no child. Where it goes from there plays by the rules.
    /// </summary>
    public static class MatchDebug
    {
        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static int Now => Find.TickManager.TicksGame;

        private static void Say(string text) => Messages.Message("Matches: " + text, MessageTypeDefOf.SilentInput, false);

        private static IEnumerable<Faction> Peoples() =>
            Find.FactionManager.AllFactionsListForReading.Where(f => f != null && !f.IsPlayer && Diplomacy.Deals(f) && !Diplomacy.Hostile(f));

        [DebugAction("Rimfolk", "Matches: their offer now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugOffer()
        {
            var home = Home;
            var opts = new List<FloatMenuOption>();
            foreach (var f in Peoples())
            {
                var people = f;
                opts.Add(new FloatMenuOption(f.Name, () =>
                {
                    var head = FavourCalls.HeadOf(people, home);
                    if (head == null)
                    {
                        Say("no head of theirs known.");
                        return;
                    }
                    if (!Matches.Pick(people, home, out var ours, out var cand))
                    {
                        Say("nobody of theirs fits anyone of the colony's here.");
                        return;
                    }
                    Matches.Offer(people, home, head, ours, cand);
                    Say("their letter is in the stack.");
                }));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("(no people at peace)", null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        [DebugAction("Rimfolk", "Matches: a match agreed now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAgree()
        {
            var home = Home;
            var opts = new List<FloatMenuOption>();
            foreach (var f in Peoples())
                foreach (var c in Matches.Colonists(home))
                    foreach (var cand in Matches.CandidatesFor(c, f, 2))
                        foreach (var lives in new[] { MatchHome.Colony, MatchHome.Theirs })
                        {
                            var people = f;
                            var ours = c;
                            var x = cand;
                            var h = lives;
                            opts.Add(new FloatMenuOption(c.LabelShort + " + " + LinkTextMath.Plain(cand.Words) + " (" + f.Name + ", " + lives + ")", () =>
                            {
                                var theirs = x.pawn ?? Matches.MakeKin(x.house, x.gender, ours);
                                var p = Matches.Begin(home, people, ours, theirs, (x.notable ?? x.house)?.id ?? -1, x.pawn == null, h, MatchWay.Sought);
                                Say(p == null ? "couldn't begin one." : "begun: " + p.stage + (p.stage == MatchStage.Asking ? " (the council's question)" : ""));
                            }));
                            if (opts.Count >= 30) break;
                        }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("(nobody of a people at peace fits anyone here)", null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        [DebugAction("Rimfolk", "Matches: their kin due now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDue()
        {
            int n = 0;
            foreach (var (h, p) in Matches.All())
            {
                if (p.stage != MatchStage.Agreed) continue;
                p.due = Now;
                if (p.feast >= 0)
                {
                    var g = Feasts.Get(p.feast, out _);
                    if (g != null)
                    {
                        g.day = Feasts.Today(h.Map);
                        foreach (var guest in g.guests) guest.comeBy = Now;
                    }
                }
                n++;
            }
            Matches.Hourly(Now);
            Say(n + " match(es) due now; their kin walk in between 8 and 15 when it is safe (the feasts' \"guests arrive now\" brings a wedding's guests at once).");
        }

        [DebugAction("Rimfolk", "Matches: the wedding at their town now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugAway()
        {
            int n = 0;
            foreach (var (h, p) in Matches.All())
                if (p.stage == MatchStage.Away)
                {
                    p.left = Now - 30 * MatchMath.DayTicks;
                    n++;
                }
            Matches.Hourly(Now);
            Say(n + " wedding(s) at their town told.");
        }

        [DebugAction("Rimfolk", "Matches: each marriage's day now (a gift due)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugDay()
        {
            foreach (var t in Matches.Live()) t.terms.number = -1000f;
            Matches.Daily(Now);
            Say(Matches.Live().Count + " marriage(s) looked at.");
        }

        [DebugAction("Rimfolk", "Matches: a spouse wronged (held in the cells)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugWrong()
        {
            var opts = new List<FloatMenuOption>();
            foreach (var t in Matches.Live())
            {
                var tie = t;
                opts.Add(new FloatMenuOption(LinkTextMath.Plain(MatchPanels.MarriageLine(t, false)), () =>
                {
                    var x = Matches.LivesOf(tie) == MatchHome.Colony ? Matches.TheirsOf(tie) : Matches.OursOf(tie);
                    if (x == null || !x.Spawned)
                    {
                        Say("they aren't on a map of the colony's.");
                        return;
                    }
                    x.guest?.CapturedBy(Faction.OfPlayer);
                    Matches.Daily(Now);
                    Say("taken prisoner; the day's look saw it.");
                }));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("(no marriage)", null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }
    }
}
