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
    /// gone (the caravan moved on, the founding over, the person moved).
    /// </summary>
    public class ChoiceLetter_Founding : ChoiceLetter
    {
        public const int Settle = 0, Newcomers = 1, Move = 2;

        public int ask;
        public int founding = -1;
        public int caravan = -1;
        public Pawn pawn;
        public int fromHome = HomesMath.None, toHome = HomesMath.None;

        private FoundingPlan Plan => Foundings.Of(founding);

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
                        yield return new DiaOption("CC_Found_Choice_Settle".Translate())
                        {
                            action = () =>
                            {
                                var plan = Plan;
                                var c = Caravan;
                                Find.LetterStack.RemoveLetter(this);
                                Foundings.Settle(plan, c);
                            },
                            resolveTree = true
                        };
                        yield return new DiaOption("CC_Found_Choice_NotYet".Translate())
                        {
                            action = () => Find.LetterStack.RemoveLetter(this),
                            resolveTree = true
                        };
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
            Scribe_Values.Look(ref caravan, "caravan", -1);
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref fromHome, "fromHome", HomesMath.None);
            Scribe_Values.Look(ref toHome, "toHome", HomesMath.None);
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

        /// <summary>The newcomers' question is answered (chosen) or let go: its letter goes.</summary>
        public static void RemoveNewcomerLetters(FoundingPlan plan)
        {
            foreach (var l in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Founding>().Where(l => l.founding == plan.id && l.ask == ChoiceLetter_Founding.Newcomers).ToList())
                Find.LetterStack.RemoveLetter(l);
        }
    }
}
