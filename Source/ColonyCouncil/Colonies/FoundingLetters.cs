using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A fresh start's questions (Colonies/Foundings.cs): the party's caravan at the place, "Settle here?"; the colony
    /// standing, "The newcomers are on their way: who are they?" (the game's own window for new people); and someone
    /// sent or kept against their wish asking to move to the other colony. Each does nothing once what it asks about is
    /// gone (the caravan moved on, the founding over, the person moved). A hand-over's too (Colonies/HandOvers.cs): the
    /// meeting's vote told, go ahead or call it off; and those who go ready to leave, their caravan to form. And a town the
    /// colony was (Colonies/TownsAfter.cs, TownTakeBack.cs): their yes to the colony's letter, take it back now or later;
    /// one of its former people asking to come home, welcome them or not now.
    /// </summary>
    public class ChoiceLetter_Founding : ChoiceLetter
    {
        public const int Settle = 0, Newcomers = 1, Move = 2, HandOverVote = 3, HandOverCaravan = 4, TakeBack = 5, Homecoming = 6;

        public int ask;
        public int founding = -1;
        public int handOver = -1;
        public int caravan = -1;
        public Pawn pawn;
        public int fromHome = HomesMath.None, toHome = HomesMath.None;
        /// <summary>A town the colony was (its key), and for a homecoming the one of the colony's they come home to.</summary>
        public int town = -1;
        public Pawn kin;

        private FoundingPlan Plan => Foundings.Of(founding);

        private LedgerFormer Town => WorldLedger.FormerOf(town);

        private HandOverPlan HandOverPlan => HandOvers.Of(handOver);

        private Caravan Caravan => Find.WorldObjects.Caravans.FirstOrDefault(c => c.ID == caravan);

        /// <summary>Still waiting for the player's answer (This week's Needs you counts it).</summary>
        public bool Waiting => !ArchivedOnly && Live;

        private bool Live
        {
            get
            {
                switch (ask)
                {
                    case Settle:
                        var c = Caravan;
                        var plan = Plan;
                        return plan != null && plan.stage == FoundingStage.Going && c != null && !c.Destroyed && !(c.pather?.Moving ?? false);
                    case Newcomers:
                        var p = Plan;
                        return p != null && p.newcomers && !p.newcomersChosen && Homes.MapOf(p.home) != null;
                    case HandOverVote:
                        var v = HandOverPlan;
                        return v != null && v.stage == HandOverStage.Voted && Homes.MapOf(v.home) != null;
                    case HandOverCaravan:
                        var c2 = HandOverPlan;
                        var m = c2 != null ? Homes.MapOf(c2.home) : null;
                        return c2 != null && c2.stage == HandOverStage.Leaving && m != null && c2.going.Any(x => x != null && !x.Dead && x.Spawned && x.Map == m);
                    case TakeBack:
                        var t = Town;
                        return t != null && t.agreed && t.Standing && t.end == TownEnd.None;
                    case Homecoming:
                        return TownsAfter.HomecomingWhyNot(Town, pawn, kin) == null;
                    default:
                        return pawn != null && !pawn.Dead && Homes.MapOf(toHome) != null && Homes.MapOf(fromHome) != null && pawn.MapHeld == Homes.MapOf(fromHome);
                }
            }
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || !Live)
                {
                    yield return Option_Close;
                    yield break;
                }
                switch (ask)
                {
                    case Settle:
                        // Each answer is written in the history's answers' log, as every question's is.
                        yield return Answers.Recorded(new DiaOption("CC_Found_Choice_Settle".Translate())
                        {
                            action = () =>
                            {
                                var plan = Plan;
                                var c = Caravan;
                                Find.LetterStack.RemoveLetter(this);
                                Foundings.Settle(plan, c);
                            },
                            resolveTree = true
                        }, this, "founding");
                        yield return Answers.Recorded(new DiaOption("CC_Found_Choice_NotYet".Translate())
                        {
                            action = () => Find.LetterStack.RemoveLetter(this),
                            resolveTree = true
                        }, this, "founding", minor: true);
                        break;
                    case HandOverVote:
                        yield return Answers.Recorded(new DiaOption("CC_HandOver_Choice_GoAhead".Translate())
                        {
                            action = () =>
                            {
                                var plan = HandOverPlan;
                                Find.LetterStack.RemoveLetter(this);
                                HandOvers.GoAhead(plan);
                            },
                            resolveTree = true
                        }, this, "founding", map: () => Homes.MapOf(HandOverPlan?.home ?? HomesMath.None));
                        yield return Answers.Recorded(new DiaOption("CC_HandOver_Choice_CallOff".Translate())
                        {
                            action = () =>
                            {
                                var plan = HandOverPlan;
                                Find.LetterStack.RemoveLetter(this);
                                HandOvers.CallOff(plan);
                            },
                            resolveTree = true
                        }, this, "founding", map: () => Homes.MapOf(HandOverPlan?.home ?? HomesMath.None));
                        break;
                    case HandOverCaravan:
                        yield return new DiaOption("CC_HandOver_Choice_FormCaravan".Translate())
                        {
                            action = () =>
                            {
                                var plan = HandOverPlan;
                                var map = plan != null ? Homes.MapOf(plan.home) : null;
                                if (map == null) return;
                                Current.Game.CurrentMap = map;
                                Find.WindowStack.Add(new Dialog_FormCaravan(map));
                            },
                            resolveTree = true
                        };
                        yield return Option_Postpone;
                        break;
                    case TakeBack:
                    {
                        var take = new DiaOption("CC_TownAfter_Choice_Take".Translate((TownsAfter.TownName(Town)).Named("TOWN")).Resolve())
                        {
                            action = () =>
                            {
                                var t = Town;
                                Find.LetterStack.RemoveLetter(this);
                                TownTakeBack.Ask(t);
                            },
                            resolveTree = true
                        };
                        string why = TownTakeBack.WhyNot(Town);
                        if (why != null) take.Disable(why);
                        yield return Answers.Recorded(take, this, "founding", Town?.people);
                        yield return Option_Postpone;
                        break;
                    }
                    case Homecoming:
                        yield return Answers.Recorded(new DiaOption("CC_TownAfter_Choice_Welcome".Translate(pawn.Named("PAWN")).Resolve())
                        {
                            action = () =>
                            {
                                var t = Town;
                                Find.LetterStack.RemoveLetter(this);
                                TownsAfter.Homecoming(t, pawn, kin);
                            },
                            resolveTree = true
                        }, this, "founding", Town?.people, pawn);
                        yield return Answers.Recorded(new DiaOption("CC_TownAfter_Choice_NotNow".Translate())
                        {
                            action = () => Find.LetterStack.RemoveLetter(this),
                            resolveTree = true
                        }, this, "founding", Town?.people, pawn, minor: true);
                        break;
                    case Newcomers:
                        yield return new DiaOption("CC_Found_Choice_Choose".Translate())
                        {
                            action = () =>
                            {
                                var plan = Plan;
                                if (plan != null) Find.WindowStack.Add(new Dialog_Newcomers(plan));
                            },
                            resolveTree = true
                        };
                        yield return Option_Postpone;
                        break;
                    default:
                        var from = Homes.MapOf(fromHome);
                        var to = Homes.MapOf(toHome);
                        var go = new DiaOption("CC_Found_Choice_LetGo".Translate())
                        {
                            action = () =>
                            {
                                Find.LetterStack.RemoveLetter(this);
                                Foundings.PlanMove(pawn, from, to);
                            },
                            resolveTree = true
                        };
                        if (!JourneyBook.On) go.Disable("CC_JourneysPg_Off".Translate());
                        else if (JourneyBook.PlanAt(from) != null) go.Disable("CC_Found_JourneyPlanned".Translate());
                        yield return Answers.Recorded(go, this, "founding", map: () => from);
                        yield return Answers.Recorded(new DiaOption("CC_Found_Choice_Stay".Translate())
                        {
                            action = () =>
                            {
                                Find.LetterStack.RemoveLetter(this);
                                CouncilGameComponent.Instance?.standings.Change(pawn, -2f, "CC_Found_StayWhy".Translate(Homes.Name(to).Named("PLACE")).Resolve());
                            },
                            resolveTree = true
                        }, this, "founding", map: () => from);
                        break;
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ask, "ask");
            Scribe_Values.Look(ref founding, "founding", -1);
            Scribe_Values.Look(ref handOver, "handOver", -1);
            Scribe_Values.Look(ref caravan, "caravan", -1);
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref fromHome, "fromHome", HomesMath.None);
            Scribe_Values.Look(ref toHome, "toHome", HomesMath.None);
            Scribe_Values.Look(ref town, "town", -1);
            Scribe_References.Look(ref kin, "kin");
        }
    }

    /// <summary>The fresh start's letters, made and sent through the teller (UI/Tell.cs).</summary>
    public static class FoundingLetters
    {
        private static ChoiceLetter_Founding Make(string label, string text, LookTargets look)
        {
            var l = LetterMaker.MakeLetter(label, text, FoundingDefOf.Rimfolk_FoundingLetter, look) as ChoiceLetter_Founding;
            return l;
        }

        /// <summary>The party's caravan at the place: "Settle here?" (the game's own Settle), asked once for this caravan.</summary>
        public static void AskSettle(FoundingPlan plan, Caravan c)
        {
            string near = c.Tile == plan.Tile ? "CC_Found_AskSettleHere".Translate().Resolve() : "CC_Found_AskSettleNear".Translate().Resolve();
            var l = Make("CC_Found_AskSettleLabel".Translate().Resolve(),
                "CC_Found_AskSettleText".Translate(Foundings.Names(c.PawnsListForReading.Where(p => p.RaceProps.Humanlike)).Named("NAMES"), plan.place.Named("PLACE"), near.Named("NEAR"),
                    (plan.fromName ?? "?").Named("MOTHER")).Resolve(), new LookTargets(c));
            if (l == null) return;
            l.ask = ChoiceLetter_Founding.Settle;
            l.founding = plan.id;
            l.caravan = c.ID;
            Tell.Ask(l);
        }

        /// <summary>The colony stands and newcomers were called: who are they? (the game's own window for new people).</summary>
        public static void AskNewcomers(FoundingPlan plan, Map map)
        {
            string call = Ages.Form(AgeFeature.Call, Ages.Of(map), Ages.Of(map)).word;
            string price = plan.keepOld ? "CC_Found_AskNewcomersPrice".Translate((plan.fromName ?? "?").Named("MOTHER")).Resolve() : "CC_Found_NewcomersFree".Translate().Resolve();
            var l = Make("CC_Found_AskNewcomersLabel".Translate(Homes.Name(map).Named("PLACE")).Resolve(),
                "CC_Found_AskNewcomersText".Translate(Homes.Name(map).Named("PLACE"), call.Named("CALL")).Resolve() + " " + price, new LookTargets(map.Parent));
            if (l == null) return;
            l.ask = ChoiceLetter_Founding.Newcomers;
            l.founding = plan.id;
            Tell.Ask(l, map);
        }

        /// <summary>Someone sent or kept against their wish asks to move to the colony they wanted.</summary>
        public static void AskMove(Pawn p, Map from, Map to)
        {
            var l = Make("CC_Found_AskMoveLabel".Translate(p.Named("PAWN")).Resolve(),
                "CC_Found_AskMoveText".Translate(p.Named("PAWN"), Homes.Name(to).Named("PLACE"), Homes.Name(from).Named("HOME")).Resolve(), new LookTargets(p));
            if (l == null) return;
            l.ask = ChoiceLetter_Founding.Move;
            l.pawn = p;
            l.fromHome = Homes.KeyOf(from);
            l.toHome = Homes.KeyOf(to);
            Tell.Ask(l, from);
        }

        /// <summary>A hand-over's meeting has voted: go ahead (the day set), or call it off.</summary>
        public static void AskHandOver(HandOverPlan plan, Map home, string label, string text)
        {
            var l = Make(label, text, new LookTargets(home.Parent));
            if (l == null) return;
            l.ask = ChoiceLetter_Founding.HandOverVote;
            l.handOver = plan.id;
            Tell.Ask(l, home);
        }

        /// <summary>
        /// A hand-over's ceremony is over and those who go are ready to leave: the caravan to form the game's way (or the
        /// pods or the gravship to send), when the journey planner doesn't take them (<paramref name="to"/>: the home they
        /// were to go to, if any).
        /// </summary>
        public static void AskHandOverCaravan(HandOverPlan plan, Map home, List<Pawn> going, Map to)
        {
            string text = (to != null ? "CC_HandOver_CaravanTextHome" : "CC_HandOver_CaravanText").Translate(Foundings.Names(going).Named("NAMES"), (plan.homeName ?? "?").Named("HOME"),
                (to != null ? Homes.Name(to) : "").Named("PLACE"), (plan.peopleName ?? "?").Named("PEOPLE")).Resolve();
            var l = Make("CC_HandOver_CaravanLabel".Translate((plan.homeName ?? "?").Named("HOME")).Resolve(), text, new LookTargets(going.Where(p => p.Spawned)));
            if (l == null) return;
            l.ask = ChoiceLetter_Founding.HandOverCaravan;
            l.handOver = plan.id;
            Tell.Ask(l, home);
        }

        /// <summary>Their yes to the colony's letter has reached the colony: take the town back now (a long event), or later from its card.</summary>
        public static void AskTakeBack(LedgerFormer rec)
        {
            string town = TownsAfter.TownName(rec);
            var head = rec.people?.leader;
            var form = Ages.Form(AgeFeature.Back, Ages.Ours, rec.age);
            string text = "CC_TownAfter_AskBackText".Translate((head?.LabelShort ?? "?").Named("HEAD"), town.Named("TOWN"), (rec.people?.Name ?? "?").Named("PEOPLE"),
                form.word.Named("FORM")).Resolve();
            var formers = rec.formers.Where(p => TownLayouts.FreeAtHome(p, rec.people)).ToList();
            if (formers.Count > 0) text += " " + "CC_TownAfter_AskBackFormers".Translate(Foundings.Names(formers).Named("NAMES")).Resolve();
            var settlement = rec.Town;
            if (!Find.WorldObjects.Settlements.Any(o => o != null && o != settlement && o.Faction == rec.people))
                text += "\n\n" + "CC_TownAfter_AskBackLast".Translate((rec.people?.Name ?? "?").Named("PEOPLE")).Resolve();
            if (rec.layout == null) text += "\n\n" + "CC_TownAfter_AskBackNoLayout".Translate(town.Named("TOWN")).Resolve();
            text += "\n\n" + "CC_TownAfter_AskBackWhen".Translate().Resolve();
            var l = Make("CC_TownAfter_AskBackLabel".Translate(town.Named("TOWN")).Resolve(), text, TownsAfter.Look(rec));
            if (l == null) return;
            l.ask = ChoiceLetter_Founding.TakeBack;
            l.town = rec.key;
            Tell.Ask(l, Homes.Seat);
        }

        /// <summary>One of its former people asks to come home (their spouse or child here, a close friend, or their town in hard times).</summary>
        public static void AskHomecoming(LedgerFormer rec, Pawn p, Pawn kin, bool close, bool hard)
        {
            string town = TownsAfter.TownName(rec);
            string why = close ? "CC_TownAfter_HomeWhy_Kin" : kin != null ? "CC_TownAfter_HomeWhy_Friend" : "CC_TownAfter_HomeWhy_Hard";
            string text = "CC_TownAfter_HomeText".Translate(p.Named("PAWN"), town.Named("TOWN")).Resolve() + " "
                          + why.Translate(p.Named("PAWN"), (kin?.LabelShort ?? "").Named("KIN"), (kin != null ? Family.Relation(p, kin) : "").Named("RELATION"), town.Named("TOWN")).Resolve()
                          + "\n\n" + "CC_TownAfter_HomeAsk".Translate(p.Named("PAWN")).Resolve();
            var l = Make("CC_TownAfter_HomeLabel".Translate(p.Named("PAWN")).Resolve(), text, TownsAfter.Look(rec));
            if (l == null) return;
            l.ask = ChoiceLetter_Founding.Homecoming;
            l.town = rec.key;
            l.pawn = p;
            l.kin = kin;
            Tell.Ask(l, TownsAfter.HomeFor(kin));
        }

        /// <summary>A hand-over is done or called off: its letters go.</summary>
        public static void RemoveHandOverLetters(HandOverPlan plan)
        {
            if (plan == null || Find.LetterStack == null) return;
            foreach (var l in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Founding>().Where(l => l.handOver == plan.id && l.ask >= ChoiceLetter_Founding.HandOverVote).ToList())
                Find.LetterStack.RemoveLetter(l);
        }

        /// <summary>The newcomers' question is answered (chosen) or let go: its letter goes.</summary>
        public static void RemoveNewcomerLetters(FoundingPlan plan)
        {
            foreach (var l in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Founding>().Where(l => l.founding == plan.id && l.ask == ChoiceLetter_Founding.Newcomers).ToList())
                Find.LetterStack.RemoveLetter(l);
        }
    }
}
