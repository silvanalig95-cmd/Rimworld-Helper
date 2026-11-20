using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode (Debug actions, "Rimfolk", "Colony life (council): ..."): each new council question asked now (the step-down
    /// question and two wishes for one pile with their conditions eased), each of the three stories begun now for a chosen
    /// colonist (its situation made true where it can be), the two decrees' day run now, and a deputy's days ended.
    /// </summary>
    public static class CouncilLifeDebug
    {
        private static Map Map => Find.CurrentMap;

        private static void Say(string text) => Messages.Message("Colony life (council): " + text, MessageTypeDefOf.SilentInput, false);

        private static readonly string[] Questions =
        {
            "Rimfolk_Dilemma_StepDown", "Rimfolk_Dilemma_OneSteel",
            "Rimfolk_DlmE_OnlyOneKnows", "Rimfolk_DlmE_Broom", "Rimfolk_DlmE_DawnToDark", "Rimfolk_DlmE_HarvestHands", "Rimfolk_DlmE_GoodMeals",
            "Rimfolk_DlmE_Seconds", "Rimfolk_DlmE_KitchenHelp", "Rimfolk_DlmE_OwnPatch", "Rimfolk_DlmE_BoredAbed", "Rimfolk_DlmE_DoctorsRounds",
            "Rimfolk_DlmE_Sickroom", "Rimfolk_DlmE_ChildAndBeast", "Rimfolk_DlmE_ElderChair", "Rimfolk_DlmE_YoungVoice", "Rimfolk_DlmE_HerdHands",
            "Rimfolk_DlmE_NameNewborn", "Rimfolk_DlmE_WhoseBeast", "Rimfolk_DlmE_VisitorSupper", "Rimfolk_DlmE_TravellersNews",
            "Rimfolk_DlmE_FriendsFellOut", "Rimfolk_DlmE_OldFriendLeftOut", "Rimfolk_DlmE_BehindTheirBack", "Rimfolk_DlmE_WarmDay",
            "Rimfolk_DlmE_ColdBedroom", "Rimfolk_DlmE_StormNight"
        };

        [DebugAction("Rimfolk", "Colony life (council): ask one of its questions now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Ask()
        {
            var tracker = Map == null ? null : DilemmaTracker.For(Map);
            var options = new List<FloatMenuOption>();
            foreach (var name in Questions)
            {
                var def = DefDatabase<DilemmaDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                options.Add(new FloatMenuOption(def.label, () =>
                {
                    // The two council situations' own conditions eased for the test: any holder at home; any two wishes sharing a stuff.
                    DilemmaWorker_StepDown.DebugAny = true;
                    DilemmaWorker_OneSteel.DebugAny = true;
                    bool asked;
                    try
                    {
                        asked = tracker != null && tracker.TryFire(true, d => d == def);
                    }
                    finally
                    {
                        DilemmaWorker_StepDown.DebugAny = false;
                        DilemmaWorker_OneSteel.DebugAny = false;
                    }
                    Say(asked ? "asked \"" + def.label + "\"." : "\"" + def.label + "\" can't be asked now (its own conditions: nobody it fits, the season, the weather, the stores).");
                }));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("(none)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        [DebugAction("Rimfolk", "Colony life (council): begin one of its stories now for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Story()
        {
            var options = new List<FloatMenuOption>();
            var arcs = ArcTracker.Instance;
            if (arcs != null && Map != null)
                foreach (var p in Map.mapPawns.FreeColonistsSpawned.Where(x => !x.IsQuestLodger()))
                    foreach (var name in new[] { "Rimfolk_Arc_AfterTheVote", "Rimfolk_Arc_SentAway", "Rimfolk_Arc_WornThin" })
                    {
                        var def = DefDatabase<ArcDef>.GetNamedSilentFail(name);
                        if (def == null) continue;
                        var who = p;
                        options.Add(new FloatMenuOption(who.LabelShort + ": " + def.label, () => Begin(arcs, def, who)));
                    }
            if (options.Count == 0) options.Add(new FloatMenuOption("(nobody here)", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void Begin(ArcTracker arcs, ArcDef def, Pawn p)
        {
            if (arcs.Of(p) != null)
            {
                Say(p.LabelShort + " is in a story already (\"Personal story: let it fizzle\" ends it).");
                return;
            }
            var home = ArcTracker.HomeColonists();
            if (def.Worker is ArcWorker_AfterVote)
            {
                // Voted out of a seat held a year, to the colonist who thinks least of them.
                var winner = home.Where(o => o != p).OrderBy(o => ArcUtil.Opinion(p, o)).FirstOrDefault();
                var office = OfficeTracker.Offices.FirstOrDefault(o => o.CanHold(p));
                if (winner == null || office == null)
                {
                    Say("nobody else at home, or no seat " + p.LabelShort + " could hold.");
                    return;
                }
                if (OfficeTracker.HeldBy(p) != null)
                {
                    Say(p.LabelShort + " holds a seat now; the story begins for someone without one.");
                    return;
                }
                LifeArcs.VotedOut(OfficeTracker.For(Map), office, p, winner, LifeArcMath.HeldDays + 1f, false);
            }
            else if (def.Worker is ArcWorker_WornThin)
            {
                var h = Heart.Of(p);
                if (h == null)
                {
                    Say("the heart's feelings and strain are off.");
                    return;
                }
                h.lastEpisodeTick = Find.TickManager.TicksGame - GenDate.TicksPerDay;
            }
            var c = def.Worker.Candidate(p, home);
            if (c == null)
            {
                Say(def.Worker is ArcWorker_SentAway
                    ? "nobody the council sent away in the last four days that " + p.LabelShort + " was closest to (banish someone through the council first)."
                    : "\"" + def.label + "\" can't begin for " + p.LabelShort + " (no friend at home, or the winner not here).");
                return;
            }
            c.pawn = p;
            Say(arcs.Begin(def, c) != null ? "\"" + def.label + "\" begins for " + p.LabelShort + "." : "it didn't begin.");
        }

        [DebugAction("Rimfolk", "Colony life (council): the night watch's and the welcome's day, now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Decrees()
        {
            var a = Map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(PlayOuts.OurPerson);
            if (a == null) return;
            var ctx = new DilemmaContext { map = Map, a = a, why = "dev" };
            var watch = DilemmaEffect_WatchRota.Tonight(Map, 0);
            new DilemmaEffect_WatchRota().Apply(ctx);
            new DilemmaEffect_WelcomeEvening().Apply(ctx);
            var newcomers = DilemmaEffect_WelcomeEvening.Newcomers(Map);
            Say("the night watch tonight: " + (watch.Count == 0 ? "nobody who can fight" : string.Join(" and ", watch.Select(x => x.LabelShort))) + ", from nine until one; "
                + (newcomers.Count == 0 ? "no newcomer (here fewer than twenty days) to welcome." : "an evening hour with " + string.Join(", ", newcomers.Select(x => x.LabelShort)) + "."));
        }

        [DebugAction("Rimfolk", "Colony life (council): every deputy's days over, now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DeputiesOver()
        {
            var council = OfficeTracker.For(Map);
            if (council == null) return;
            int n = 0;
            foreach (var d in council.deputies)
            {
                if (d == null) continue;
                d.until = Find.TickManager.TicksGame;
                n++;
            }
            Say(n == 0 ? "no deputy in." : n + " deputy's days end at the council's next day.");
        }
    }
}
