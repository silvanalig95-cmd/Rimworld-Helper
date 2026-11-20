using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Dev mode (Debug actions, "Rimfolk", "Colony life 2: ..."): each piece of colony life's variety, part 2, tried at once:
    /// a coming-of-age path asked, the toy wish, an ambition of the colony's people put to work, a seat's life goal, each
    /// of the five marks, the deeds, the pastimes, a social event of the council's seasons, the giving day and the elders'
    /// day, each of the twelve events and each of the six stories.
    /// </summary>
    public static class LifeVarietyDebug
    {
        private static void Say(string text) => Messages.Message("Colony life 2: " + text, MessageTypeDefOf.SilentInput, false);

        private static void Menu(List<FloatMenuOption> options, string empty)
        {
            if (options.Count == 0) options.Add(new FloatMenuOption(empty, null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static List<Pawn> Colonists => Find.CurrentMap?.mapPawns.FreeColonistsSpawned.Where(PlayOuts.OurPerson).ToList() ?? new List<Pawn>();

        private static void ForEachColonist(Action<Pawn> act, bool adults = true)
        {
            var options = Colonists.Where(p => !adults || !p.DevelopmentalStage.Juvenile()).Select(p =>
            {
                var pawn = p;
                return new FloatMenuOption(p.LabelShort, () => act(pawn));
            }).ToList();
            Menu(options, "(nobody at home)");
        }

        // ---------------- A8: coming of age ----------------

        private static readonly string[] Paths = { "Rimfolk_Dilemma_Path_Fields", "Rimfolk_Dilemma_Path_Kitchen", "Rimfolk_Dilemma_Path_Animals", "Rimfolk_Dilemma_Path_Building" };

        [DebugAction("Rimfolk", "Colony life 2: a new coming-of-age path asked for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ComingOfAge()
        {
            var options = new List<FloatMenuOption>();
            foreach (var name in Paths)
            {
                var def = DefDatabase<DilemmaDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                options.Add(new FloatMenuOption(def.label, () => ForEachColonist(p =>
                {
                    var comp = CouncilGameComponent.Instance;
                    if (comp == null) return;
                    // As if they had just come of age (Lives/Legacy.cs).
                    if (!comp.legacy.cameOfAge.Contains(p)) comp.legacy.cameOfAge.Add(p);
                    bool asked = (DilemmaTracker.Of(p) ?? comp.dilemmas).TryFire(true, d => d == def);
                    Say(asked ? def.label + " asked for " + p.LabelShort + "." : "it can't be asked now (nobody best at the work besides them, or the questions are paused).");
                })));
            }
            Menu(options, "(no paths)");
        }

        // ---------------- A11: a toy ----------------

        [DebugAction("Rimfolk", "Colony life 2: a toy for a child, asked now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Toy() => ForEachColonist(p =>
        {
            var comp = CouncilGameComponent.Instance;
            var def = DefDatabase<WishDef>.GetNamedSilentFail("Rimfolk_Wish_Toy");
            if (comp == null || def == null) return;
            if (comp.OpenWishBy(p) is Wish open) comp.Resolve(open, WishState.Cancelled);
            var c = def.Worker.CandidatesFor(p).FirstOrDefault();
            if (c == null)
            {
                Say(p.LabelShort + " can't ask: no child of three to twelve they're fond of at home, a toy given lately, or nothing the colony can make (Biotech needed).");
                return;
            }
            comp.CreateWish(def, p, p, c);
        });

        // ---------------- A15: three ambitions ----------------

        [DebugAction("Rimfolk", "Colony life 2: an ambition of the colony's people under way now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Ambition()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var name in new[] { "Rimfolk_Amb_NobodyOutside", "Rimfolk_Amb_KeptPromises", "Rimfolk_Amb_LighterSeason" })
            {
                var def = DefDatabase<AmbitionDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                options.Add(new FloatMenuOption(def.label + (def.Possible(map) ? "" : " (not pitched now: it answers nothing on the map)"), () =>
                {
                    var tracker = ColonyState.For(map)?.ambitions;
                    var proposer = Colonists.FirstOrDefault(p => !p.DevelopmentalStage.Juvenile());
                    if (tracker == null || proposer == null) return;
                    tracker.Choose(def, proposer, map);
                    Say(def.label + " is the colony's ambition now, proposed by " + proposer.LabelShort + ".");
                }));
            }
            Menu(options, "(none)");
        }

        // ---------------- A16: the seats' life goals ----------------

        [DebugAction("Rimfolk", "Colony life 2: a seat's life goal for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SeatGoal()
        {
            var options = new List<FloatMenuOption>();
            foreach (var def in DefDatabase<AspirationDef>.AllDefsListForReading.Where(d => d.defName.StartsWith("Rimfolk_Asp_Seat_")))
            {
                var goal = def;
                options.Add(new FloatMenuOption(goal.label, () => ForEachColonist(p =>
                {
                    var comp = CouncilGameComponent.Instance;
                    if (comp == null) return;
                    if (!goal.Worker.CanHave(p))
                    {
                        Say(p.LabelShort + " can't want it now (they hold a seat, or the seat isn't open to the colony).");
                        return;
                    }
                    var a = comp.aspirations.DebugBegin(comp, p, goal, goal.sparks?.FirstOrDefault());
                    Say(a != null ? p.LabelShort + " has set their heart on " + goal.label + "." : "it couldn't begin (they have a goal already).");
                })));
            }
            Menu(options, "(none)");
        }

        [DebugAction("Rimfolk", "Colony life 2: the seats' sparks read now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Sparks()
        {
            SeatSparks.Daily(CouncilGameComponent.Instance);
            Say("the records behind the seats' sparks were read (the first read after a load only looks; read again tomorrow).");
        }

        // ---------------- A22: marks ----------------

        [DebugAction("Rimfolk", "Colony life 2: a mark of the colony's own life for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Mark()
        {
            var options = new List<FloatMenuOption>();
            foreach (var family in new[] { "GoalReached", "IdeaBuilt", "CustomTradition", "SeatYearKept", "StoryEndedWell" })
            {
                var f = family;
                options.Add(new FloatMenuOption(f, () => ForEachColonist(p =>
                {
                    var m = PersonaMarks.Give(p, f, null, "the test");
                    Say(m != null ? p.LabelShort + " has the mark " + f + " (their character page shows it)." : "no mark: the character feature is off, or one came lately.");
                })));
            }
            Menu(options, "(none)");
        }

        // ---------------- A23: deeds ----------------

        [DebugAction("Rimfolk", "Colony life 2: a deed of the council's life for...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Deed()
        {
            var seat = DefDatabase<OfficeDef>.GetNamedSilentFail("Council_Office_Reeve");
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("kept a promise in a seat", () => ForEachColonist(p => LifeDeeds.Judged(p, seat, true))),
                new FloatMenuOption("broke a promise in a seat", () => ForEachColonist(p => LifeDeeds.Judged(p, seat, false))),
                new FloatMenuOption("spoke up for someone", () => ForEachColonist(p => LifeDeeds.SpokeUp(p, Colonists.FirstOrDefault(o => o != p))))
            };
            Menu(options, "(none)");
        }

        // ---------------- A25: the council's seasons ----------------

        [DebugAction("Rimfolk", "Colony life 2: a social event of the council's seasons now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CouncilSeason()
        {
            var comp = CouncilGameComponent.Instance;
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var def in DefDatabase<SocialEventDef>.AllDefsListForReading.Where(d => !d.council.NullOrEmpty()))
            {
                var ev = def;
                options.Add(new FloatMenuOption(ev.label + " (" + ev.council + ")", () =>
                {
                    var people = Colonists;
                    Pawn a = null, b = null;
                    foreach (var x in people)
                    {
                        b = people.FirstOrDefault(y => y != x && CouncilSeasons.Fits(ev.council, x, y));
                        if (b == null) continue;
                        a = x;
                        break;
                    }
                    if (a == null)
                    {
                        Say("nobody fits " + ev.council + " now (an election under way, a seat lately handed over, a promise standing, a petition of two, an elder and a young holder).");
                        return;
                    }
                    Say(ev.label + ": " + a.LabelShort + " and " + b.LabelShort + " fit it; it comes on its own when they're together.");
                }));
            }
            Menu(options, "(none)");
        }

        // ---------------- A26 and A28: the customs ----------------

        [DebugAction("Rimfolk", "Colony life 2: the giving day's gifts now (no supper)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Giving() => Say(GivingDay.DevStart(Find.CurrentMap, false));

        [DebugAction("Rimfolk", "Colony life 2: the giving day for the children now (no supper)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GivingChildren() => Say(GivingDay.DevStart(Find.CurrentMap, true));

        [DebugAction("Rimfolk", "Colony life 2: ask a custom now (the giving day, the elders' day)...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AskCustom()
        {
            var options = new List<FloatMenuOption>();
            foreach (var name in new[] { "Rimfolk_Dilemma_GivingDay", "Rimfolk_Dilemma_EldersDay" })
            {
                var def = DefDatabase<DilemmaDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                options.Add(new FloatMenuOption(def.label, () =>
                {
                    var map = Find.CurrentMap;
                    var book = LifeVarietyBook.Instance;
                    if (book != null)
                    {
                        book.givingYear = -99;
                        book.eldersYear = -99;
                        book.giving = null;
                    }
                    bool asked = DilemmaTracker.For(map)?.AskCustom(def) ?? false;
                    Say(asked ? def.label + " asked." : def.label + " can't be asked now: out of its season (winter, autumn), or nobody to ask (the elders' day needs two of sixty or more, or the elder's seat, and someone under thirty).");
                }));
            }
            Menu(options, "(none)");
        }

        [DebugAction("Rimfolk", "Colony life 2: the elders' supper set going now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Elders()
        {
            var map = Find.CurrentMap;
            if (EldersDay.Elders(map).Count == 0)
            {
                Say("no elders at home (sixty or more, or the elder's seat).");
                return;
            }
            var def = DefDatabase<DilemmaDef>.GetNamedSilentFail("Rimfolk_Dilemma_EldersDay");
            var ctx = new DilemmaContext { def = def, map = map, a = Colonists.FirstOrDefault(p => !EldersDay.IsElder(p)) ?? Colonists.FirstOrDefault() };
            new DilemmaEffect_EldersDay { children = true, label = "the elders' supper", hours = new IntRange(0, 23), duration = new FloatRange(1.5f, 2f) }.Apply(ctx);
            Say("the elders' supper is planned (when the colony's week has room), with the children's pieces by day.");
        }

        // ---------------- The twelve events ----------------

        private static readonly string[] Events =
        {
            "Rimfolk_Evt_QuarrelAtSupper", "Rimfolk_Evt_NewcomerAtEdge", "Rimfolk_Evt_FirstPiece", "Rimfolk_Evt_StoresShort", "Rimfolk_Evt_WishWaiting", "Rimfolk_Evt_OldSong",
            "Rimfolk_Evt_StormVow", "Rimfolk_Evt_GroupEveningOff", "Rimfolk_Evt_ChildsQuestion", "Rimfolk_Evt_PromiseSore", "Rimfolk_Evt_LastField", "Rimfolk_Evt_HerdNight"
        };

        [DebugAction("Rimfolk", "Colony life 2: one of the colony's own events now...", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Event()
        {
            var map = Find.CurrentMap;
            var options = new List<FloatMenuOption>();
            foreach (var name in Events)
            {
                var def = DefDatabase<IncidentDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                options.Add(new FloatMenuOption(def.label, () =>
                {
                    var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                    parms.forced = true;
                    bool fired = def.Worker.TryExecute(parms);
                    Say(fired ? def.label + " fired." : def.label + " can't happen now: what it grows out of isn't on the map (see its comment in Rimfolk_Events_ColonyLife.xml).");
                }));
            }
            Menu(options, "(none)");
        }

        // ---------------- The six stories ----------------

        private static readonly string[] Stories =
        {
            "Rimfolk_Arc_CustomOfTheirOwn", "Rimfolk_Arc_PromiseWaited", "Rimfolk_Arc_ReadyForTheSeat", "Rimfolk_Arc_PrayerForAFriend", "Rimfolk_Arc_UnexpectedGift", "Rimfolk_Arc_YearsAim"
        };

        [DebugAction("Rimfolk", "Colony life 2: one of the new personal stories for...", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Story(Pawn p)
        {
            var tracker = ArcTracker.Instance;
            if (tracker == null || p == null) return;
            if (tracker.Of(p) != null)
            {
                Say(p.LabelShort + " already has a story under way.");
                return;
            }
            var home = ArcTracker.HomeColonists();
            var options = new List<FloatMenuOption>();
            foreach (var name in Stories)
            {
                var def = DefDatabase<ArcDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                var c = def.Worker.Candidate(p, home);
                bool fits = c != null;
                options.Add(new FloatMenuOption(def.label + (fits ? "" : " (doesn't fit them now: begun for the test with the closest colonist)"), () =>
                {
                    var cand = c ?? new ArcCandidate
                    {
                        other = ArcUtil.Closest(p, home, -999f), name = "the test", goalText = CouncilGameComponent.Instance?.aspirations.Active(p)?.Goal ?? "do what they set out to do",
                        key = "test", tag = "0", skill = SkillDefOf.Social
                    };
                    cand.pawn = p;
                    var arc = tracker.Begin(def, cand);
                    Say(arc != null ? def.label + " begun for " + p.LabelShort + "." : "it couldn't begin.");
                }));
            }
            Menu(options, "(none)");
        }
    }
}
