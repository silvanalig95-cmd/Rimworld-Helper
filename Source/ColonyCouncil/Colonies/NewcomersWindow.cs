using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The newcomers for a new colony, made with the game's own window for new people (the player's decision 3A): built
    /// as the game's <c>Dialog_ChooseNewWanderers</c>, the one it offers after a game over: one to six people, each
    /// randomized on the character card, with Biotech's xenotype and life stage options, health, relations, possessions
    /// and the skills summary (the game's own <see cref="StartingPawnUtility"/> and its portrait area), made from the
    /// player's people's kind as at a new game. Its passage is named at the foot (decision 4A: free with the old colony
    /// abandoned in the same act, else silver and food worth about their kit from the old colony's stores, at most once a
    /// year), and Confirm is held back when the stores can't pay. The game's start data exists only while it is open and
    /// is cleared when it closes, by the X too: the people not chosen are let go as the game's window lets them go.
    /// <para>
    /// Release 3 (Scenario): a scenario's start parts shape these people for the new colony alone (its count and choice,
    /// required kinds, xenotypes, ages and skills, read from its ConfigPage part's fields) through <see cref="Request"/>.
    /// </para>
    /// </summary>
    public class Dialog_Newcomers : Window
    {
        private readonly FoundingPlan plan;
        private int curPawnIndex;
        private int generationIndex;
        private bool confirmed;

        private const float TitleAreaHeight = 45f;
        private const float PawnEntryHeight = 60f;
        private const float SkillSummaryHeight = 127f;
        private const float PriceHeight = 48f;
        private static readonly Vector2 PawnSelectorPortraitSize = new Vector2(70f, 110f);
        private static readonly Vector2 ConfirmButtonSize = new Vector2(150f, 38f);
        private static readonly FloatRange ExcludeBiologicalAgeRange = new FloatRange(12.1f, 13f);

        public override Vector2 InitialSize => new Vector2(1020f, 790f);

        private static List<Pawn> Pawns => Find.GameInitData.startingAndOptionalPawns;

        public Dialog_Newcomers(FoundingPlan plan)
        {
            this.plan = plan;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        /// <summary>
        /// The game's own request for a starting person, as its window makes them: the player's people's kind, of the
        /// player's people, a new person with relations, any age but the two years Biotech leaves out. Release 3 reads a
        /// scenario's part here.
        /// </summary>
        private static PawnGenerationRequest Request =>
            new PawnGenerationRequest(Faction.OfPlayer.def.basicMemberKind, Faction.OfPlayer, PawnGenerationContext.PlayerStarter, forceGenerateNewPawn: true,
                canGeneratePawnRelations: true, colonistRelationChanceFactor: 50f, allowPregnant: true, developmentalStages: DevelopmentalStage.Adult,
                excludeBiologicalAgeRange: ModsConfig.BiotechActive ? new FloatRange?(ExcludeBiologicalAgeRange) : null);

        public override void PreOpen()
        {
            base.PreOpen();
            Current.Game.InitData = new GameInitData();
            Find.GameInitData.startingPawnCount = 3;
            StartingPawnUtility.ClearAllStartingPawns();
            generationIndex = 0;
            while (Pawns.Count < 3)
            {
                StartingPawnUtility.SetGenerationRequest(generationIndex, Request);
                StartingPawnUtility.AddNewPawn(generationIndex);
                generationIndex++;
            }
            curPawnIndex = 0;
        }

        /// <summary>Their passage: nothing when free, else about the worth of each one's kit (FoundingMath.Passage).</summary>
        private int Price
        {
            get
            {
                bool free = FoundingMath.Free(!plan.keepOld);
                var kits = Pawns.Select(p => Find.GameInitData.startingPossessions.TryGetValue(p, out var list) ? list.Sum(t => t.ThingDef.BaseMarketValue * t.Count) : 0f);
                return FoundingMath.Passage(kits, free);
            }
        }

        private Map PayFrom => Homes.MapOf(plan.from) ?? Homes.Seat;

        public override void DoWindowContents(Rect rect)
        {
            if (!IsOpen) return;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, rect.width, TitleAreaHeight), "CC_Found_NewcomersTitle".Translate(plan.place.Named("PLACE")).Resolve());
            Text.Font = GameFont.Small;
            var body = rect;
            body.yMin += TitleAreaHeight;
            var confirm = new Rect(rect.width - ConfirmButtonSize.x, rect.height - ConfirmButtonSize.y, ConfirmButtonSize.x, ConfirmButtonSize.y);
            // The passage, named before the choice (decision 4A).
            int price = Price;
            string why = null;
            string line;
            if (price <= 0) line = "CC_Found_NewcomersFree".Translate().Resolve();
            else if (Foundings.CanPay(PayFrom, price, out int silver, out float food))
                line = "CC_Found_PriceLine".Translate(price.Named("PRICE"), silver.Named("SILVER"), Mathf.RoundToInt(food).Named("FOOD"), Homes.Name(PayFrom).Named("HOME")).Resolve();
            else
            {
                line = "CC_Found_CantPay".Translate(price.Named("SILVER")).Resolve();
                why = line;
            }
            UiKit.Paragraph(0f, confirm.y - 4f, confirm.x - 16f, line, why != null ? UiKit.Warn : UiKit.Faint, GameFont.Small);
            if (why != null) TooltipHandler.TipRegion(confirm, why);
            if (Widgets.ButtonText(confirm, "Confirm".Translate(), true, true, why == null) && why == null)
            {
                Confirm(price);
                return;
            }
            body.yMax -= confirm.height + 10f + PriceHeight;
            var list = body;
            list.width = 140f;
            DrawPawnList(list);
            var main = body;
            main.xMin += 150f;
            var skills = main.BottomPartPixels(SkillSummaryHeight);
            main.yMax = skills.yMin - 10f;
            StartingPawnUtility.DrawPortraitArea(main, curPawnIndex, true, true);
            StartingPawnUtility.DrawSkillSummaries(skills);
        }

        private void Confirm(int price)
        {
            var chosen = Pawns.ToList();
            var possessions = chosen.ToDictionary(p => p, p => Find.GameInitData.startingPossessions.TryGetValue(p, out var l) ? l.ToList() : new List<ThingDefCount>());
            // The chosen are the colony's now: out of the game's start data, so closing doesn't let them go.
            foreach (var p in chosen) Find.GameInitData.startingPossessions.Remove(p);
            Pawns.Clear();
            confirmed = true;
            Close();
            Foundings.NewcomersCome(plan, chosen, possessions, price);
        }

        private void DrawPawnList(Rect rect)
        {
            var row = rect;
            row.height = PawnEntryHeight;
            row = row.ContractedBy(4f);
            for (int i = 0; i < Pawns.Count; i++)
            {
                DoPawnRow(row, i);
                row.y += PawnEntryHeight;
            }
            row.y += 4f;
            if (Pawns.Count < FoundingMath.MaxNewcomers && Widgets.ButtonText(new Rect(row.x, row.y, row.width, 25f), "+"))
            {
                Find.GameInitData.startingPawnCount++;
                StartingPawnUtility.SetGenerationRequest(generationIndex, Request);
                StartingPawnUtility.AddNewPawn(generationIndex);
                generationIndex++;
            }
        }

        private void DoPawnRow(Rect rect, int index)
        {
            var pawn = Pawns[index];
            Widgets.DrawOptionBackground(rect, curPawnIndex == index);
            MouseoverSounds.DoRegion(rect);
            Widgets.BeginGroup(rect);
            var inner = rect.AtZero().ContractedBy(4f);
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            GUI.DrawTexture(new Rect(110f - PawnSelectorPortraitSize.x / 2f, 40f - PawnSelectorPortraitSize.y / 2f, PawnSelectorPortraitSize.x, PawnSelectorPortraitSize.y),
                PortraitsCache.Get(pawn, PawnSelectorPortraitSize, Rot4.South));
            GUI.color = Color.white;
            string name = pawn.Name is NameTriple t ? (string.IsNullOrEmpty(t.Nick) ? t.First : t.Nick) : pawn.LabelShort;
            Widgets.Label(inner.TopPart(0.5f).Rounded(), name);
            Widgets.Label(inner.BottomPart(0.5f).Rounded(), Text.CalcSize(pawn.story.TitleCap).x > inner.width ? pawn.story.TitleShortCap : pawn.story.TitleCap);
            if (Mouse.IsOver(inner) && Pawns.Count > FoundingMath.MinNewcomers && Widgets.ButtonImage(new Rect(inner.xMax - 15f, inner.y, 15f, 15f), TexButton.Delete))
            {
                Find.GameInitData.startingPawnCount--;
                Pawns.Remove(pawn);
                curPawnIndex = Math.Min(Pawns.Count - 1, curPawnIndex);
            }
            if (Widgets.ButtonInvisible(inner))
            {
                curPawnIndex = index;
                SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            }
            Widgets.EndGroup();
        }

        /// <summary>However it closes (Confirm, the X, Escape): the people not chosen are let go and the game's start data is cleared.</summary>
        public override void PostClose()
        {
            base.PostClose();
            try
            {
                if (Current.Game?.InitData != null) StartingPawnUtility.ClearAllStartingPawns();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("newcomers: clearing the window", e);
            }
            Current.Game.InitData = null;
            // Closed without choosing, with nobody going from the colony: nothing was founded, and nothing comes of it.
            if (!confirmed && plan != null && plan.means == FoundingMeans.Newcomers && plan.stage == FoundingStage.Going) Foundings.CallOff(plan);
        }
    }
}
