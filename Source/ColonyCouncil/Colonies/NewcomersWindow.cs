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
    /// Release 3, a scenario's start (Colonies/ScenarioStarts.cs): the chosen scenario's people part shapes them for the
    /// new colony alone, read from its fields (its count and the candidates to choose them from, required kinds,
    /// xenotypes and mutants, ages and skills; never through its PostIdeoChosen), its shaping parts reach them through
    /// their own pawn hooks while the window is open, the count is the scenario's (those left behind are let go), the
    /// game's own checks hold Confirm back until the chosen make the start, and its kit is made as they are confirmed.
    /// </para>
    /// </summary>
    public class Dialog_Newcomers : Window
    {
        private readonly FoundingPlan plan;
        private int curPawnIndex;
        private int generationIndex;
        private bool confirmed;
        /// <summary>A scenario's start shaping them, or null for a new game's group; the scenario chosen but no longer loaded.</summary>
        private ScenarioStart start;
        private bool scenarioMissing;
        private Vector2 listScroll;

        private const float TitleAreaHeight = 45f;
        private const float PawnEntryHeight = 60f;
        private const float SkillSummaryHeight = 127f;
        private const float PriceHeight = 48f;
        private static readonly Vector2 PawnSelectorPortraitSize = new Vector2(70f, 110f);
        private static readonly Vector2 ConfirmButtonSize = new Vector2(150f, 38f);
        private static readonly FloatRange ExcludeBiologicalAgeRange = new FloatRange(12.1f, 13f);

        public override Vector2 InitialSize => new Vector2(1020f, 790f);

        private static List<Pawn> Pawns => Find.GameInitData.startingAndOptionalPawns;

        /// <summary>Those coming: the first of the list, as many as the start data counts (all of them as a new game's group).</summary>
        private static IEnumerable<Pawn> Chosen => Pawns.Take(Math.Max(0, Math.Min(Pawns.Count, Find.GameInitData.startingPawnCount)));

        /// <summary>The count is the scenario's own: none added or taken away, only chosen among the candidates.</summary>
        private bool Fixed => start?.people != null;

        public Dialog_Newcomers(FoundingPlan plan)
        {
            this.plan = plan;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        /// <summary>
        /// The game's own request for a starting person, as its window makes them: the player's people's kind, of the
        /// player's people, a new person with relations, any age but the two years Biotech leaves out. A scenario's people
        /// part makes its own (ScenarioStarts.Generate).
        /// </summary>
        private static PawnGenerationRequest Request =>
            new PawnGenerationRequest(Faction.OfPlayer.def.basicMemberKind, Faction.OfPlayer, PawnGenerationContext.PlayerStarter, forceGenerateNewPawn: true,
                canGeneratePawnRelations: true, colonistRelationChanceFactor: 50f, allowPregnant: true, developmentalStages: DevelopmentalStage.Adult,
                excludeBiologicalAgeRange: ModsConfig.BiotechActive ? new FloatRange?(ExcludeBiologicalAgeRange) : null);

        public override void PreOpen()
        {
            base.PreOpen();
            Current.Game.InitData = new GameInitData();
            start = null;
            scenarioMissing = false;
            if (!plan.scenario.NullOrEmpty())
            {
                try
                {
                    var scen = ScenarioStarts.Resolve(plan.scenario);
                    scenarioMissing = scen == null;
                    if (scen != null)
                    {
                        start = ScenarioStarts.Read(scen, plan.scenario);
                        ScenarioStarts.InitData(start);
                        if (start.hooks) ScenarioStarts.Shaping = scen;
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("newcomers: the scenario's start", e);
                    start = null;
                    ScenarioStarts.Shaping = null;
                }
            }
            StartingPawnUtility.ClearAllStartingPawns();
            generationIndex = 0;
            if (Fixed)
            {
                try
                {
                    ScenarioStarts.Generate(start);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("newcomers: making the scenario's people", e);
                }
                generationIndex = Pawns.Count;
                // Nobody could be made the scenario's way: a new game's group, with its other parts still carried.
                if (Pawns.Count < Find.GameInitData.startingPawnCount)
                {
                    StartingPawnUtility.ClearAllStartingPawns();
                    start.people = null;
                    generationIndex = 0;
                }
            }
            if (!Fixed)
            {
                Find.GameInitData.startingPawnCount = 3;
                while (Pawns.Count < 3)
                {
                    StartingPawnUtility.SetGenerationRequest(generationIndex, Request);
                    StartingPawnUtility.AddNewPawn(generationIndex);
                    generationIndex++;
                }
            }
            ScenarioStarts.NoPossessions(start);
            curPawnIndex = 0;
        }

        /// <summary>Their passage: nothing when free, else about the worth of each one's kit (FoundingMath.Passage), and a scenario's own kit on top (ScenarioStartMath.Passage).</summary>
        private int Price
        {
            get
            {
                bool free = FoundingMath.Free(!plan.keepOld);
                var kits = Chosen.Select(p => Find.GameInitData.startingPossessions.TryGetValue(p, out var list) ? list.Sum(t => t.ThingDef.BaseMarketValue * t.Count) : 0f);
                return start == null ? FoundingMath.Passage(kits, free) : ScenarioStartMath.Passage(kits, start.kitWorth, start.kitPeople, free);
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
            // A scenario's start: as which scenario has them, or that it isn't loaded any more.
            string scenarioLine = start != null ? "CC_ScenStart_WindowLine".Translate(start.Name.Named("SCENARIO")).Resolve()
                : scenarioMissing ? "CC_ScenStart_Missing".Translate((plan.scenarioName ?? "?").Named("SCENARIO")).Resolve() : null;
            if (scenarioLine != null)
            {
                UiKit.LabelFit(new Rect(0f, TitleAreaHeight - 12f, rect.width - 40f, 22f), scenarioLine, scenarioMissing ? UiKit.Warn : UiKit.Faint, GameFont.Tiny);
                body.yMin += 10f;
            }
            ScenarioStarts.NoPossessions(start);
            var confirm = new Rect(rect.width - ConfirmButtonSize.x, rect.height - ConfirmButtonSize.y, ConfirmButtonSize.x, ConfirmButtonSize.y);
            // The passage, named before the choice (decision 4A); a scenario's start, once the chosen make it.
            int price = Price;
            string need = Fixed ? ScenarioStarts.WhyNot(start, Chosen.ToList()) : null;
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
            if (need != null)
            {
                why ??= need;
                line = need + "\n" + line;
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
            var chosen = Chosen.ToList();
            ScenarioKit kit = null;
            if (start != null)
            {
                // Those left behind let go first, then the kit made while the start data holds the chosen alone.
                ScenarioStarts.LetGoLeftBehind(chosen);
                kit = ScenarioStarts.MakeKit(start, !plan.keepOld, Homes.MapOf(plan.home) == null);
            }
            var possessions = chosen.ToDictionary(p => p, p => Find.GameInitData.startingPossessions.TryGetValue(p, out var l) ? l.ToList() : new List<ThingDefCount>());
            // The chosen are the colony's now: out of the game's start data, so closing doesn't let them go.
            foreach (var p in chosen) Find.GameInitData.startingPossessions.Remove(p);
            Pawns.Clear();
            confirmed = true;
            Close();
            Foundings.NewcomersCome(plan, chosen, possessions, price, kit);
        }

        /// <summary>Two of them change places (the chosen and those left behind), with the requests that made them, as the game's page reorders both.</summary>
        private void Swap(int a, int b)
        {
            if (a == b || a < 0 || b < 0 || a >= Pawns.Count || b >= Pawns.Count) return;
            (Pawns[a], Pawns[b]) = (Pawns[b], Pawns[a]);
            var ra = StartingPawnUtility.GetGenerationRequest(a);
            StartingPawnUtility.SetGenerationRequest(a, StartingPawnUtility.GetGenerationRequest(b));
            StartingPawnUtility.SetGenerationRequest(b, ra);
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }

        private void DrawPawnList(Rect rect)
        {
            if (Fixed)
            {
                DrawCandidates(rect);
                return;
            }
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

        /// <summary>
        /// A scenario's people: those coming first (its count), then those left behind to choose from. An arrow takes one
        /// left behind in place of the one in view (or the last coming), or leaves one coming behind.
        /// </summary>
        private void DrawCandidates(Rect rect)
        {
            int count = Find.GameInitData.startingPawnCount;
            float view = Pawns.Count * PawnEntryHeight + (Pawns.Count > count ? 26f : 0f) + 8f;
            var inner = new Rect(0f, 0f, rect.width - (view > rect.height ? 16f : 0f), Mathf.Max(view, rect.height));
            Widgets.BeginScrollView(rect, ref listScroll, inner);
            float y = 0f;
            for (int i = 0; i < Pawns.Count; i++)
            {
                if (i == count && Pawns.Count > count)
                {
                    UiKit.LabelFit(new Rect(4f, y + 2f, inner.width - 8f, 22f), "CC_ScenStart_LeftBehind".Translate().Resolve(), UiKit.Faint, GameFont.Tiny);
                    y += 26f;
                }
                var row = new Rect(0f, y, inner.width, PawnEntryHeight).ContractedBy(4f);
                y += PawnEntryHeight;
                // The arrow's click is taken first, so it is its own and not the row's; it is drawn over the row after.
                bool swaps = Pawns.Count > count;
                var arrow = new Rect(row.xMax - 20f, row.yMax - 20f, 16f, 16f);
                if (swaps && Widgets.ButtonInvisible(arrow))
                {
                    if (i < count)
                    {
                        Swap(i, count);
                        curPawnIndex = i;
                    }
                    else
                    {
                        int with = curPawnIndex < count ? curPawnIndex : count - 1;
                        Swap(i, with);
                        curPawnIndex = with;
                    }
                    continue;
                }
                DoPawnRow(row, i);
                if (!swaps) continue;
                GUI.color = Mouse.IsOver(arrow) ? GenUI.MouseoverColor : Color.white;
                GUI.DrawTexture(arrow, i < count ? TexButton.ReorderDown : TexButton.ReorderUp);
                GUI.color = Color.white;
                TooltipHandler.TipRegion(arrow, (i < count ? "CC_ScenStart_LeaveTip" : "CC_ScenStart_TakeTip").Translate());
            }
            Widgets.EndScrollView();
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
            if (!Fixed && Mouse.IsOver(inner) && Pawns.Count > FoundingMath.MinNewcomers && Widgets.ButtonImage(new Rect(inner.xMax - 15f, inner.y, 15f, 15f), TexButton.Delete))
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
            // The scenario's parts shape nobody else (Patch_ScenarioStartShape*).
            ScenarioStarts.Shaping = null;
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
