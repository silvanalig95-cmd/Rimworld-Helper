using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode: force each part of a feast with guests at the home on screen (Debug actions, "Rimfolk"): a feast with a
    /// people's notables who said yes, set for today; the guests arriving now; the contests now; the next contest now; the
    /// feast now; a night gone sour; first light; the steward's suggestion; one of the colony's back from a neighbour's feast
    /// with a win to tell. Where it goes from there plays by the rules.
    /// </summary>
    public static class FeastDebug
    {
        private static Map Home => Homes.IsHome(Find.CurrentMap) ? Find.CurrentMap : Homes.Seat;

        private static int Now => Find.TickManager.TicksGame;

        private static void Say(string text) => Messages.Message("Feasts: " + text, MessageTypeDefOf.SilentInput, false);

        private static GuestFeast Plan => Feasts.At(Home);

        [DebugAction("Rimfolk", "Feasts: a feast with a people's notables, set for today...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSet()
        {
            var home = Home;
            var opts = new List<FloatMenuOption>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading.Where(f => f != null && !f.IsPlayer && Diplomacy.Deals(f) && !Diplomacy.Hostile(f)))
            {
                var people = f;
                var notables = NotableBook.Instance?.notables.Where(n => n != null && n.faction == f && n.Alive && n.pawn != null && !n.pawn.Spawned && LivingEvents.FreeToTravel(n.pawn))
                    .Take(FeastMath.MaxGuests).ToList();
                if (notables == null || notables.Count == 0) continue;
                opts.Add(new FloatMenuOption(f.Name + " (" + notables.Count + ")", () =>
                {
                    var book = DiplomacyBook.Instance;
                    var h = book?.HomeOf(home);
                    if (h == null) return;
                    if (h.feast != null) Feasts.CallOff(h, h.feast, home, null, false);
                    var contests = System.Enum.GetValues(typeof(ContestKind)).Cast<ContestKind>().Where(k => Feasts.ContestWhyNot(k, home) == null).Take(FeastMath.MaxContests).ToList();
                    var p = Feasts.Host(home, people, notables, FeastPurpose.Feast, Now, null, contests, Feasts.Prizes(home).FirstOrDefault());
                    if (p == null)
                    {
                        Say("couldn't set one.");
                        return;
                    }
                    p.writerName = Feasts.Writer(home)?.LabelShort;
                    p.entry = Diplomacy.Write(DiplomacyMath.Feast, new List<HistoryActor> { new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did), new HistoryActor(Refs.Of(people), HistoryRole.For) },
                        Diplomacy.Colony(home), FeastWords.P("CC_Feast_Hist_Invited", FeastWords.Name(home).Named("FEAST"), FeastWords.Guests(p).Named("GUESTS"),
                            FeastWords.Peoples(p).Named("PEOPLES")))?.id ?? -1;
                    p.prize?.SetForbidden(true, false);
                    p.day = Feasts.Today(home);
                    Say("set for today with " + FeastWords.Guests(p) + "; they walk in between 8 and 15 when it's fine and safe (the next action brings them now).");
                }));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("(no people at peace with a notable free to travel)", null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        [DebugAction("Rimfolk", "Feasts: the guests arrive now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugArrive()
        {
            var p = Plan;
            if (p == null || p.stage != FeastStage.Set)
            {
                Say("no feast set at this home (the first action sets one).");
                return;
            }
            p.day = Feasts.Today(Home);
            Feasts.Arrive(DiplomacyBook.Instance?.Existing(Home), p, Home);
            Say(p.OnMap ? "the guests walk in to the gate." : "they couldn't come in (no way in from the edge?).");
        }

        [DebugAction("Rimfolk", "Feasts: begin the contests now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugContests()
        {
            var p = Plan;
            if (p == null || p.stage != FeastStage.Here)
            {
                Say("no guests waiting at the gate.");
                return;
            }
            p.arrived = Now - 2 * GenDate.TicksPerHour;
            p.contest = 0;
            p.contestStart = -1;
            Feasts.Moved(p, Home, FeastStage.Contests);
            Say("the contests begin at the next look (any hour; outdoor ones still want fine weather).");
        }

        [DebugAction("Rimfolk", "Feasts: end the contest under way now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugNextContest()
        {
            var p = Plan;
            if (p == null || p.stage != FeastStage.Contests || p.contestStart < 0)
            {
                Say("no contest under way.");
                return;
            }
            p.contestStart = Now - 20 * GenDate.TicksPerHour;
            Say("it ends at the next look, with what stands.");
        }

        [DebugAction("Rimfolk", "Feasts: the feast now (skip what contests are left)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugFeast()
        {
            var p = Plan;
            if (p == null || !p.OnMap || p.stage == FeastStage.Feasting || p.stage == FeastStage.Night)
            {
                Say("no guests here before the feast.");
                return;
            }
            p.contest = p.contests.Count;
            p.contestStart = -1;
            FeastContests.Stop(p);
            Feasts.Moved(p, Home, FeastStage.Contests);
            Say("the feast is set going at the next look; the gathering waits for its hours (16 to 21) and for people to be free.");
        }

        [DebugAction("Rimfolk", "Feasts: first light now (the guests go home)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugEnd()
        {
            var home = Home;
            var h = DiplomacyBook.Instance?.Existing(home);
            var p = h?.feast;
            if (p == null || !p.OnMap)
            {
                Say("no guests here.");
                return;
            }
            Feasts.End(h, p, home, false);
            Say("the guests go home; the letter says how they were welcomed.");
        }

        [DebugAction("Rimfolk", "Feasts: the steward's suggestion now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugSuggest()
        {
            var home = Home;
            var steward = ColonyState.For(home)?.offices?.HolderOf("Council_Office_Steward") ?? Feasts.Writer(home);
            var friend = Feasts.Candidates(home).FirstOrDefault(n => Feasts.InviteWhyNot(n, home) == null);
            if (steward == null || friend == null)
            {
                Say("nobody to suggest it or nobody to invite.");
                return;
            }
            Feasts.Suggest(home, steward, friend);
        }

        [DebugAction("Rimfolk", "Feasts: a colonist back from a neighbour's feast with a win to tell", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DebugTale()
        {
            var home = Home;
            var p = home.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            var h = DiplomacyBook.Instance?.HomeOf(home);
            var f = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => x != null && !x.IsPlayer && Diplomacy.Deals(x) && !Diplomacy.Hostile(x));
            if (p == null || h == null || f == null) return;
            h.tales.Add(new FeastTale { pawn = p, at = Now, text = FeastWords.T("CC_Feast_Away_Won", p.Named("PAWN"), FeastWords.Contest(ContestKind.Race).Named("CONTEST"),
                FeastWords.People(f).Named("PLACE"), FeastWords.People(f).Named("PEOPLE")) });
            Say("told within the hour (the evening's news, by the colony news setting).");
        }
    }
}
