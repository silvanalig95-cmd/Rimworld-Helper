using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A town the colony was asks (Colonies/TownLife.cs; release 5): in hard times, to come back under the colony (take it
    /// back now, or not now: a refusal); refused twice, its people at the colony's gate asking to come home (take them all
    /// in, only the colony's own, or send them on). Each does nothing once what it asks about is gone (the town taken back
    /// or no longer theirs, those at the gate gone on).
    /// </summary>
    public class ChoiceLetter_TownLife : ChoiceLetter
    {
        public const int Back = 0, Gate = 1;

        public int ask;
        /// <summary>The town the colony was (its key).</summary>
        public int town = -1;

        private LedgerFormer Town => WorldLedger.FormerOf(town);

        /// <summary>Still waiting for the player's answer.</summary>
        public bool Waiting => !ArchivedOnly && Live;

        private bool Live
        {
            get
            {
                var t = Town;
                if (t == null) return false;
                if (ask == Back) return t.askOpen && t.Standing && t.end == TownEnd.None;
                return t.gateCame >= 0 && TownLife.AtGate(t, out _).Count > 0;
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
                var rec = Town;
                string name = TownsAfter.TownName(rec);
                if (ask == Back)
                {
                    var take = new DiaOption("CC_TownLife_Choice_Take".Translate(name.Named("TOWN")).Resolve())
                    {
                        action = () =>
                        {
                            var t = Town;
                            Find.LetterStack.RemoveLetter(this);
                            TownLife.TakeBack(t);
                        },
                        resolveTree = true
                    };
                    string why = TownTakeBack.WhyNot(rec);
                    if (why != null) take.Disable(why);
                    yield return Answers.Recorded(take, this, "townLife", rec?.people);
                    yield return Answers.Recorded(new DiaOption("CC_TownLife_Choice_NotNow".Translate())
                    {
                        action = () =>
                        {
                            var t = Town;
                            Find.LetterStack.RemoveLetter(this);
                            TownLife.Refused(t, false);
                        },
                        resolveTree = true
                    }, this, "townLife", rec?.people);
                    yield return Option_Postpone;
                    yield break;
                }
                var here = TownLife.AtGate(rec, out var map);
                bool others = here.Any(p => !rec.formers.Contains(p));
                bool formers = here.Any(p => rec.formers.Contains(p));
                yield return Answers.Recorded(new DiaOption("CC_TownLife_Choice_All".Translate())
                {
                    action = () =>
                    {
                        var t = Town;
                        Find.LetterStack.RemoveLetter(this);
                        TownLife.TakeIn(t, false);
                    },
                    resolveTree = true
                }, this, "townLife", rec?.people, map: () => map);
                if (others && formers)
                    yield return Answers.Recorded(new DiaOption("CC_TownLife_Choice_Ours".Translate((rec.colonyName ?? name).Named("COLONY")).Resolve())
                    {
                        action = () =>
                        {
                            var t = Town;
                            Find.LetterStack.RemoveLetter(this);
                            TownLife.TakeIn(t, true);
                        },
                        resolveTree = true
                    }, this, "townLife", rec?.people, map: () => map);
                yield return Answers.Recorded(new DiaOption("CC_TownLife_Choice_On".Translate())
                {
                    action = () =>
                    {
                        var t = Town;
                        Find.LetterStack.RemoveLetter(this);
                        TownLife.SendOn(t);
                    },
                    resolveTree = true
                }, this, "townLife", rec?.people, map: () => map, minor: true);
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ask, "ask");
            Scribe_Values.Look(ref town, "town", -1);
        }
    }

    /// <summary>The town's own letters, made and sent through the teller (UI/Tell.cs).</summary>
    public static class TownLifeLetters
    {
        private static ChoiceLetter_TownLife Make(string label, string text, LookTargets look) =>
            LetterMaker.MakeLetter(label, text, TownLifeDefOf.Rimfolk_TownLifeLetter, look) as ChoiceLetter_TownLife;

        /// <summary>The town's head asks, in hard times, to come back under the colony: their letter by the town's own means.</summary>
        public static void AskBack(LedgerFormer rec)
        {
            string town = TownsAfter.TownName(rec);
            var head = rec.people?.leader;
            var form = Ages.Form(AgeFeature.Back, Ages.Ours, rec.age);
            string text = "CC_TownLife_AskBackText".Translate((head?.LabelShort ?? "?").Named("HEAD"), town.Named("TOWN"), (rec.people?.Name ?? "?").Named("PEOPLE"),
                TownLife.Means(rec).Named("MEANS"), (rec.colonyName ?? town).Named("COLONY")).Resolve();
            string fortune = RegionBook.FortunesOn ? RegionFortunes.Line(WorldLedger.TownRecord(rec.key)) : null;
            if (!fortune.NullOrEmpty()) text += " " + "CC_TownAfter_LetterFortune".Translate(town.Named("TOWN"), fortune.UncapitalizeFirst().Named("FORTUNE")).Resolve();
            var formers = rec.formers.Where(p => TownLayouts.FreeAtHome(p, rec.people)).ToList();
            if (formers.Count > 0) text += "\n\n" + "CC_TownAfter_AskBackFormers".Translate(Foundings.Names(formers).Named("NAMES")).Resolve();
            text += "\n\n" + "CC_TownLife_AskBackForm".Translate(form.word.Named("FORM")).Resolve();
            if (rec.refusals > 0) text += "\n\n" + "CC_TownLife_AskBackAgain".Translate(town.Named("TOWN")).Resolve();
            if (rec.layout == null) text += "\n\n" + "CC_TownAfter_AskBackNoLayout".Translate(town.Named("TOWN")).Resolve();
            var l = Make("CC_TownLife_AskBackLabel".Translate(town.Named("TOWN")).Resolve(), text, TownsAfter.Look(rec));
            if (l == null) return;
            l.ask = ChoiceLetter_TownLife.Back;
            l.town = rec.key;
            Tell.Ask(l, Homes.Seat);
        }

        /// <summary>Refused twice: the town is given up, and its people at the gate ask to come home (or, with nobody to come, the town's end told).</summary>
        public static void AskGate(LedgerFormer rec, Map home, List<Pawn> came, List<Pawn> formers, bool last, Pawn oldHead, WorldObject ruin)
        {
            string town = TownsAfter.TownName(rec);
            string people = rec.people?.Name ?? "?";
            string text = "CC_TownLife_GateText".Translate(town.Named("TOWN"), people.Named("PEOPLE"), (rec.colonyName ?? town).Named("COLONY")).Resolve();
            var ours = came.Where(formers.Contains).ToList();
            var others = came.Where(p => !formers.Contains(p)).ToList();
            if (ours.Count > 0) text += "\n\n" + "CC_TownLife_GateFormers".Translate(Foundings.Names(ours).Named("NAMES"), (rec.colonyName ?? town).Named("COLONY")).Resolve();
            if (others.Count > 0) text += "\n\n" + "CC_TownLife_GateOthers".Translate(Foundings.Names(others).Named("NAMES"), people.Named("PEOPLE")).Resolve();
            text += "\n\n" + (last ? "CC_TownLife_GateLast" : "CC_TownLife_GateNotLast").Translate(people.Named("PEOPLE"), town.Named("TOWN")).Resolve();
            string label = "CC_TownLife_GateLabel".Translate(town.Named("TOWN")).Resolve();
            if (came.Count == 0)
            {
                // apart: the town's end, with nobody of theirs at the gate.
                Tell.Letter(NewsKind.World, Importance.Important, home, label, text, LetterDefOf.NegativeEvent, ruin != null ? new LookTargets(ruin) : LookTargets.Invalid);
                return;
            }
            text += "\n\n" + "CC_TownLife_GateAsk".Translate().Resolve();
            var l = Make(label, text, new LookTargets(came.Where(p => p.Spawned)));
            if (l == null) return;
            l.ask = ChoiceLetter_TownLife.Gate;
            l.town = rec.key;
            Tell.Ask(l, home);
        }

        /// <summary>A question of the town's that is answered or let go: its letter goes.</summary>
        public static void Remove(LedgerFormer rec, int ask)
        {
            if (rec == null || Find.LetterStack == null) return;
            foreach (var l in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_TownLife>().Where(l => l.town == rec.key && l.ask == ask).ToList())
                Find.LetterStack.RemoveLetter(l);
        }
    }
}
