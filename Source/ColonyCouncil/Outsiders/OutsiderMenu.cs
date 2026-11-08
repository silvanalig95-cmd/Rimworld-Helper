using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Right-clicking someone from outside the colony with a colonist selected: one line, "Talk with X...", that opens
    /// the small menu of what the two can do (Outsiders.Options). Hostiles aren't offered it: right-clicking them stays
    /// the game's attack. The game finds this provider by itself (every FloatMenuOptionProvider in a loaded assembly).
    /// </summary>
    public class FloatMenuOptionProvider_RimfolkOutsiders : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;

        protected override bool Undrafted => true;

        protected override bool Multiselect => false;

        protected override bool RequiresManipulation => false;

        protected override bool AppliesInt(FloatMenuContext context) => Outsiders.On && Current.ProgramState == ProgramState.Playing;

        public override bool TargetPawnValid(Pawn pawn, FloatMenuContext context) =>
            base.TargetPawnValid(pawn, context) && Outsiders.IsOutsider(pawn) && pawn != context.FirstSelectedPawn;

        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            var c = context.FirstSelectedPawn;
            if (c == null || !c.IsColonistPlayerControlled || c.IsQuestLodger() || c.DevelopmentalStage.Baby() || clickedPawn == null) return null;
            try
            {
                string label = "CC_Out_Menu".Translate(clickedPawn.Named("OTHER")).Resolve();
                string why = Outsiders.ColonistWhyNot(c, clickedPawn) ?? Outsiders.Unwilling(clickedPawn, c, OutsiderAct.Meal);
                if (why != null) return new FloatMenuOption("CC_Out_Disabled".Translate(label.Named("LABEL"), why.Named("WHY")).Resolve(), null);
                var x = clickedPawn;
                // Had enough of the colony for now: said plainly here (hard words and a challenge are still in the menu).
                if (OutsiderTalk.Left(x) <= 0) label = "CC_Out_Disabled".Translate(label.Named("LABEL"), "CC_Out_Why_Enough".Translate().Named("WHY")).Resolve();
                return new FloatMenuOption(label, () => Find.WindowStack.Add(new FloatMenu(Outsiders.Options(c, x),
                    "CC_Out_MenuTitlePatience".Translate(c.Named("PAWN"), x.Named("OTHER"), OutsiderTalk.PatienceWords(x).Named("PATIENCE")).Resolve())));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider menu", e);
                return null;
            }
        }
    }

    public static partial class Outsiders
    {
        /// <summary>The order things are offered in: everyday first, favours and bonds, then the pointed ones.</summary>
        private static readonly OutsiderAct[] MenuOrder =
        {
            OutsiderAct.Chat, OutsiderAct.AskHome, OutsiderAct.Meal, OutsiderAct.Drink, OutsiderAct.Gift, OutsiderAct.Befriend, OutsiderAct.Court, OutsiderAct.Faith,
            OutsiderAct.GoodWord, OutsiderAct.Letter, OutsiderAct.Teach, OutsiderAct.Bring, OutsiderAct.Invite,
            // Stage 2's acts after Invite (Diplomacy/DiplomacyActs.cs): ask a favour of their people, speak of a pact, later ones as they come.
            OutsiderAct.AskFavour, OutsiderAct.SpeakPact, OutsiderAct.SpeakMatch, OutsiderAct.OfferExchange,
            OutsiderAct.Spar, OutsiderAct.Provoke, OutsiderAct.Duel
        };

        /// <summary>What the two can do, each with why not when it can't be now (greyed).</summary>
        public static List<FloatMenuOption> Options(Pawn c, Pawn x)
        {
            var list = new List<FloatMenuOption>();
            foreach (var act in MenuOrder)
            {
                try
                {
                    if (!Shown(c, x, act)) continue;
                    string label = Label(c, x, act);
                    string why = WhyNot(c, x, act);
                    if (why != null)
                    {
                        list.Add(new FloatMenuOption("CC_Out_Disabled".Translate(label.Named("LABEL"), why.Named("WHY")).Resolve(), null));
                        continue;
                    }
                    var a = act;
                    // Asked before within its wait: they'll only say so (OutsiderTalk.Begin), and the menu says it first.
                    if (OutsiderTalk.Repeat(c, x, act, out _)) label = "CC_Out_Again".Translate(label.Named("LABEL")).Resolve();
                    list.Add(new FloatMenuOption(label, () => Choose(c, x, a)));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("outsider option " + act, e);
                }
            }
            if (list.Count == 0) list.Add(new FloatMenuOption("CC_Out_Nothing".Translate().Resolve(), null));
            return list;
        }

        private static string Label(Pawn c, Pawn x, OutsiderAct act)
        {
            switch (act)
            {
                case OutsiderAct.GoodWord:
                {
                    var to = WordTo(x);
                    return "CC_Out_Act_GoodWord".Translate((to != null ? NotableBook.TitleOf(to) + " " + to.Name : "CC_Out_TheirPeople".Translate().Resolve()).Named("NAME")).Resolve();
                }
                case OutsiderAct.Teach:
                {
                    var skill = TeachSkill(c, x);
                    return skill != null ? "CC_Out_Act_TeachSkill".Translate(skill.label.Named("SKILL")).Resolve() : "CC_Out_Act_Teach".Translate().Resolve();
                }
                default:
                    if (DiplomacyActs.Owns(act)) return DiplomacyActs.Label(c, x, act);
                    return ("CC_Out_Act_" + act).Translate().Resolve();
            }
        }

        /// <summary>The player picked it: some open a picker first (a gift, a letter, what to bring, a duel's terms); the rest set the colonist off.</summary>
        private static void Choose(Pawn c, Pawn x, OutsiderAct act)
        {
            switch (act)
            {
                case OutsiderAct.Gift:
                    Find.WindowStack.Add(new Dialog_OutsiderGift(c, x));
                    return;
                case OutsiderAct.Letter:
                {
                    var to = LetterTo(x);
                    if (to != null) Find.WindowStack.Add(new Dialog_NeighbourLetter(to, x, c));
                    return;
                }
                case OutsiderAct.Bring:
                {
                    int ofColony = OfColony(x);
                    var opts = BringChoices(x).Select(d => new FloatMenuOption(
                        "CC_Out_BringItem".Translate(d.LabelCap.Named("THING"), OutsiderMath.BringCount(d.BaseMarketValue, ofColony).Named("COUNT")).Resolve(),
                        () => StartTalk(c, x, OutsiderAct.Bring, 0, d), d)).ToList();
                    if (opts.Count > 0) Find.WindowStack.Add(new FloatMenu(opts, "CC_Out_BringTitle".Translate(x.Named("OTHER")).Resolve()));
                    return;
                }
                case OutsiderAct.Duel:
                {
                    var opts = new List<FloatMenuOption>
                    {
                        new FloatMenuOption("CC_Out_Terms_FirstBlood".Translate().Resolve(), () => StartTalk(c, x, OutsiderAct.Duel, (int)DuelTerms.FirstBlood)),
                        new FloatMenuOption("CC_Out_Terms_Yield".Translate().Resolve(), () => StartTalk(c, x, OutsiderAct.Duel, (int)DuelTerms.Yield))
                    };
                    Find.WindowStack.Add(new FloatMenu(opts, "CC_Out_TermsTitle".Translate().Resolve()));
                    return;
                }
                default:
                    // Stage 2's acts pick what to ask first (Diplomacy/DiplomacyActs.cs).
                    if (DiplomacyActs.Owns(act))
                    {
                        DiplomacyActs.Choose(c, x, act);
                        return;
                    }
                    StartTalk(c, x, act);
                    return;
            }
        }
    }

    /// <summary>
    /// A gift for an outsider: what's in the stores, one row a kind, the things that would suit them first, with what
    /// it's worth and how it would be taken; a click sends the colonist off to fetch it and hand it over. In Rimfolk's frame
    /// (key "Gift"), its title in the title bar.
    /// </summary>
    public class Dialog_OutsiderGift : FramedWindow
    {
        private readonly Pawn c, x;
        private List<Outsiders.GiftChoice> choices;
        private Vector2 scroll;
        private const float RowH = 32f;

        protected override string FrameTitle => x == null ? "" : "CC_Out_GiftTitle".Translate(x.Named("OTHER")).Resolve();

        protected override Texture2D FrameIcon => Icons.Promise;

        public Dialog_OutsiderGift(Pawn c, Pawn x) : base("Gift")
        {
            this.c = c;
            this.x = x;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            forcePause = true;
            choices = Outsiders.GiftChoices(c, x);
        }

        public override Vector2 InitialSize => new Vector2(Mathf.Min(560f, UI.screenWidth - 40f), Mathf.Min(620f, UI.screenHeight - 40f));

        protected override void DoFramedContents(Rect inRect)
        {
            try
            {
                Contents(inRect);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("outsider gift dialog", e);
                Close();
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        private void Contents(Rect inRect)
        {
            if (c == null || x == null || !x.Spawned || !c.Spawned)
            {
                Close();
                return;
            }
            float y = TitleTop(inRect, FrameTitle, 0f, 36f);
            string sub = "CC_Out_GiftNote".Translate(c.Named("PAWN"), x.Named("OTHER")).Resolve();
            float sh = Text.CalcHeight(sub, inRect.width);
            GUI.color = UiKit.Faint;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, sh), sub);
            GUI.color = Color.white;
            y += sh + 6f;
            var outer = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            var view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(outer.height, choices.Count * RowH));
            Widgets.BeginScrollView(outer, ref scroll, view);
            float ry = 0f;
            foreach (var g in choices)
            {
                var row = new Rect(0f, ry, view.width, RowH - 2f);
                if (g.thing == null || g.thing.Destroyed || !g.thing.Spawned)
                {
                    ry += RowH;
                    continue;
                }
                UiKit.RowHover(row);
                Widgets.ThingIcon(new Rect(row.x + 2f, row.y + 2f, 26f, 26f), g.thing);
                string label = g.count > 1 ? Couriers.Amount(g.count, null, g.thing.def) : g.thing.LabelNoCount;
                Widgets.Label(new Rect(row.x + 34f, row.y + 4f, row.width * 0.5f - 34f, 24f), label.CapitalizeFirst().Truncate(row.width * 0.5f - 34f));
                string fit = g.against ? "CC_Out_GiftFit_Against".Translate().Resolve()
                    : g.suits ? (g.fine ? "CC_Out_GiftFit_SuitsFine" : "CC_Out_GiftFit_Suits").Translate().Resolve()
                    : g.fine ? "CC_Out_GiftFit_Fine".Translate().Resolve() : "";
                string worth = "CC_Out_GiftWorth".Translate(Mathf.RoundToInt(g.value).Named("VALUE")).Resolve();
                GUI.color = g.against ? UiKit.Bad : g.suits ? UiKit.Good : UiKit.Faint;
                UiKit.LabelFit(new Rect(row.x + row.width * 0.5f, row.y + 4f, row.width * 0.3f, 24f), fit, GUI.color, GameFont.Tiny);
                GUI.color = UiKit.Faint;
                UiKit.LabelFit(new Rect(row.x + row.width * 0.8f, row.y + 4f, row.width * 0.2f, 24f), worth, UiKit.Faint, GameFont.Tiny);
                GUI.color = Color.white;
                if (!g.against && Widgets.ButtonInvisible(row))
                {
                    Outsiders.StartGift(c, x, g.thing, g.count);
                    Close();
                }
                ry += RowH;
            }
            if (choices.Count == 0) Widgets.Label(new Rect(0f, 0f, view.width, 30f), "CC_Out_Why_NoGoods".Translate().Resolve());
            Widgets.EndScrollView();
        }
    }
}
