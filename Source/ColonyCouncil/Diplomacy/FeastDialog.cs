using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Planning a feast with guests (Feasts.Plan): whom to invite (up to three notables, each greyed with why not), which
    /// contests (by what the colony has, each greyed with why not and its risk named before the choice), a prize from the
    /// stores, the meals the stores hold against those wanted, and who writes the invitations. Sending sets it going.
    /// </summary>
    public class Dialog_Feast : Window
    {
        private readonly Map home;
        private readonly HashSet<int> picked = new HashSet<int>();
        private readonly List<ContestKind> contests = new List<ContestKind>();
        private Thing prize;
        private Pawn writer;
        private Vector2 scroll;
        private float height = 600f;
        private List<Notable> candidates;
        private float builtAt = -1f;

        public Dialog_Feast(Map home, Faction people = null, Notable notable = null)
        {
            this.home = home ?? Homes.Seat;
            forcePause = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            writer = Feasts.Writer(this.home);
            Build();
            if (notable != null && Feasts.InviteWhyNot(notable, this.home) == null) picked.Add(notable.id);
            if (people != null)
                foreach (var n in candidates.Where(n => n.faction == people && Feasts.InviteWhyNot(n, this.home) == null).Take(FeastMath.MaxGuests - picked.Count))
                    picked.Add(n.id);
            // What each invited people hopes for, offered first when the colony can hold it.
            foreach (var n in candidates.Where(n => picked.Contains(n.id)))
                foreach (ContestKind k in System.Enum.GetValues(typeof(ContestKind)))
                    if (contests.Count < FeastMath.MaxContests && !contests.Contains(k) && FeastMath.Answers(Feasts.HopeOf(n.faction), k) && Feasts.ContestWhyNot(k, this.home) == null)
                        contests.Add(k);
        }

        public override Vector2 InitialSize => new Vector2(760f, Mathf.Min(760f, UI.screenHeight - 60f));

        private void Build()
        {
            candidates = Feasts.Candidates(home);
            builtAt = Time.realtimeSinceStartup;
        }

        private List<Notable> Picked => candidates.Where(n => picked.Contains(n.id)).ToList();

        private string SendWhyNot()
        {
            string why = Feasts.PlanWhyNot(home);
            if (why != null) return why;
            if (picked.Count == 0) return FeastWords.P("CC_Feast_Why_NoGuests");
            if (writer == null || writer.Dead || writer.MapHeld != home) return FeastWords.P("CC_Feast_Why_NoWriter");
            return Picked.Select(n => Feasts.InviteWhyNot(n, home)).FirstOrDefault(w => w != null);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (Time.realtimeSinceStartup - builtAt > 2f) Build();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width - 30f, 34f), FeastWords.P("CC_Feast_Dlg_Title", FeastWords.Name(home).Named("FEAST")).CapitalizeFirst());
            Text.Font = GameFont.Small;
            float top = 38f;
            var outRect = new Rect(0f, top, inRect.width, inRect.height - top - 48f);
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            float w = view.width;
            y += UiKit.Paragraph(0f, y, w, FeastWords.P("CC_Feast_Dlg_Intro", FeastMath.MaxGuests.Named("GUESTS"), FeastMath.MaxContests.Named("CONTESTS")), UiKit.Faint) + 8f;

            // Guests.
            y += UiKit.SectionTitle(0f, y, w, FeastWords.P("CC_Feast_Dlg_Guests"), picked.Count, Icons.Visitor);
            if (candidates.Count == 0)
            {
                UiKit.EmptyLine(new Rect(0f, y, w, UiKit.RowDense + 4f), FeastWords.P("CC_Feast_Dlg_NoCandidates"));
                y += UiKit.RowDense + 8f;
            }
            int index = 0;
            foreach (var n in candidates)
                y += GuestRow(n, 0f, y, w, index++);
            y += UiKit.SectionGap;

            // Contests.
            y += UiKit.SectionTitle(0f, y, w, FeastWords.P("CC_Feast_Dlg_Contests"), contests.Count, Icons.Trust);
            index = 0;
            foreach (ContestKind k in System.Enum.GetValues(typeof(ContestKind)))
                y += ContestRow(k, 0f, y, w, index++);
            y += UiKit.SectionGap;

            // The prize.
            y += UiKit.SectionTitle(0f, y, w, FeastWords.P("CC_Feast_Dlg_Prize"), Icons.Promise);
            if (prize != null && (prize.Destroyed || !prize.Spawned)) prize = null;
            string prizeLabel = prize != null ? FeastWords.P("CC_Feast_Dlg_PrizeIs", PlayGive.Label(prize).Named("PRIZE"), Mathf.RoundToInt(prize.MarketValue * prize.stackCount).Named("VALUE"))
                : FeastWords.P("CC_Feast_Dlg_NoPrize");
            if (Widgets.ButtonText(new Rect(0f, y, Mathf.Min(w, 420f), 28f), prizeLabel.Truncate(400f)))
            {
                var opts = new List<FloatMenuOption> { new FloatMenuOption(FeastWords.P("CC_Feast_Dlg_NoPrize"), () => prize = null) };
                foreach (var t in Feasts.Prizes(home))
                {
                    var thing = t;
                    opts.Add(new FloatMenuOption(FeastWords.P("CC_Feast_Dlg_PrizeIs", PlayGive.Label(thing).Named("PRIZE"), Mathf.RoundToInt(thing.MarketValue * thing.stackCount).Named("VALUE")),
                        () => prize = thing));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            y += 32f;
            y += UiKit.Paragraph(0f, y, w, FeastWords.P("CC_Feast_Dlg_PrizeNote"), UiKit.Faint, GameFont.Tiny) + 8f;

            // The feast: meals, and what the Empire expects.
            y += UiKit.SectionTitle(0f, y, w, FeastWords.P("CC_Feast_Dlg_Feast"), Icons.Wedding ?? Icons.Visitor);
            int colonists = home?.mapPawns.FreeColonistsSpawnedCount ?? 0;
            int guests = picked.Count;
            int wanted = FeastMath.MealsWanted(colonists, guests, guests * FeastMath.MaxRetinue);
            int meals = Feasts.MealsInStores(home);
            y += UiKit.Paragraph(0f, y, w, FeastWords.P("CC_Feast_Dlg_Meals", meals.Named("MEALS"), wanted.Named("WANTED"), colonists.Named("COLONISTS"), guests.Named("GUESTS")),
                meals >= wanted ? UiKit.Ink : UiKit.Soon) + 4f;
            if (meals < wanted) y += UiKit.Paragraph(0f, y, w, FeastWords.P("CC_Feast_Dlg_MealsShort"), UiKit.Soon, GameFont.Tiny) + 4f;
            if (Picked.Any(n => Ages.IsEmpire(n.faction)))
                y += UiKit.Paragraph(0f, y, w, FeastWords.P(Feasts.HasInstrument(home) ? "CC_Feast_Dlg_Empire" : "CC_Feast_Dlg_EmpireNoMusic"), UiKit.Faint, GameFont.Tiny) + 4f;
            y += UiKit.Paragraph(0f, y, w, FeastWords.P("CC_Feast_Dlg_Night"), UiKit.Faint, GameFont.Tiny) + 8f;

            // Who writes.
            string writerLabel = FeastWords.P("CC_Feast_Dlg_Writer", (writer?.LabelShort ?? "?").Named("PAWN"));
            if (Widgets.ButtonText(new Rect(0f, y, Mathf.Min(w, 420f), 28f), writerLabel.Truncate(400f)))
            {
                var opts = NeighbourLetters.Writers(home).Select(p =>
                {
                    var pawn = p;
                    return new FloatMenuOption(pawn.LabelShort + " (" + (pawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0) + ")", () => writer = pawn);
                }).ToList();
                if (opts.Count == 0) opts.Add(new FloatMenuOption(FeastWords.P("CC_Feast_Why_NoWriter"), null));
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            y += 34f;
            height = Mathf.Max(y + 10f, 100f);
            Widgets.EndScrollView();

            string why = SendWhyNot();
            if (UiKit.ActButton(new Rect(inRect.width - 220f, inRect.height - 38f, 220f, 34f), FeastWords.P("CC_Feast_Dlg_Send"), why, FeastWords.P("CC_Feast_Dlg_SendTip")))
                Send();
            if (Widgets.ButtonText(new Rect(0f, inRect.height - 38f, 140f, 34f), "CancelButton".Translate())) Close();
        }

        private float GuestRow(Notable n, float x, float y, float w, int index)
        {
            const float h = 44f;
            var r = new Rect(x, y, w, h);
            UiKit.Zebra(r, index);
            string why = Feasts.InviteWhyNot(n, home);
            bool on = picked.Contains(n.id);
            bool can = why == null && (on || picked.Count < FeastMath.MaxGuests);
            bool now = on;
            Widgets.Checkbox(new Vector2(x + 4f, y + 10f), ref now, 24f, !can && !on);
            if (now != on)
            {
                if (now && can) picked.Add(n.id);
                else picked.Remove(n.id);
            }
            int regard = NotableBook.Opinion(n);
            float days = Feasts.TravelDays(n, home);
            var hope = Feasts.HopeOf(n.faction);
            string name = NotableBook.Called(n) + " (" + FeastWords.People(n.faction) + ")";
            UiKit.LabelFit(new Rect(x + 36f, y + 2f, w - 40f, 22f), name, why == null ? UiKit.Ink : UiKit.Faint);
            string note = why ?? FeastWords.P("CC_Feast_Dlg_GuestNote", NotableBook.RegardWords(regard).Named("REGARD"), days.ToString("0.#").Named("DAYS"),
                FeastWords.Hope(hope).Named("HOPE"));
            UiKit.LabelFit(new Rect(x + 36f, y + 22f, w - 40f, 20f), note, why == null ? NotableBook.RegardColor(regard) : UiKit.Faint, GameFont.Tiny);
            TooltipHandler.TipRegion(r, why == null ? FeastWords.P("CC_Feast_Dlg_GuestTip", n.Name.Named("NAME"), FeastWords.Hope(hope).Named("HOPE")) : why);
            return h;
        }

        private float ContestRow(ContestKind k, float x, float y, float w, int index)
        {
            const float h = 44f;
            var r = new Rect(x, y, w, h);
            UiKit.Zebra(r, index);
            string why = Feasts.ContestWhyNot(k, home);
            bool on = contests.Contains(k);
            bool can = why == null && (on || contests.Count < FeastMath.MaxContests);
            bool now = on;
            Widgets.Checkbox(new Vector2(x + 4f, y + 10f), ref now, 24f, !can && !on);
            if (now != on)
            {
                if (now && can) contests.Add(k);
                else contests.Remove(k);
            }
            bool hoped = Picked.Any(n => FeastMath.Answers(Feasts.HopeOf(n.faction), k));
            string label = FeastWords.ContestThe(k) + (hoped ? " " + FeastWords.P("CC_Feast_Dlg_Hoped") : "");
            UiKit.LabelFit(new Rect(x + 36f, y + 2f, w - 40f, 22f), label, why == null ? (hoped ? UiKit.Gold : UiKit.Ink) : UiKit.Faint);
            string note = why ?? FeastWords.P("CC_Feast_Dlg_Contest_" + k);
            UiKit.LabelFit(new Rect(x + 36f, y + 22f, w - 40f, 20f), note, UiKit.Faint, GameFont.Tiny);
            TooltipHandler.TipRegion(r, why ?? FeastWords.P("CC_Feast_Dlg_ContestTip_" + k));
            return h;
        }

        private void Send()
        {
            var plan = Feasts.Plan(home, Picked, contests, prize, writer, out string why);
            if (plan == null)
            {
                // direct: the answer to the player's own click, saying why it couldn't be sent.
                Messages.Message(why ?? FeastWords.P("CC_Feast_Why_NoGuests"), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (plan.prize != null) plan.prize.SetForbidden(true, false);
            // direct: the dialog's own confirmation of the invitations the player just sent.
            Messages.Message(FeastWords.P("CC_Feast_Msg_Sent", FeastWords.Guests(plan).Named("GUESTS"), (writer?.LabelShort ?? "?").Named("PAWN")), MessageTypeDefOf.PositiveEvent, false);
            Close();
        }
    }
}
