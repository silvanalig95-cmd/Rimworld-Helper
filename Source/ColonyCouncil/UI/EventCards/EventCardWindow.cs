using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>
    /// The game's own letter window for a letter on Rimfolk's card, never shown: the letter's options belong to it, so the
    /// game's own DiaOption.Activate runs for each as it does in the game's window (its sound, closing the window, its
    /// action, going on to another page). Closing it closes the card; the page it goes on to is shown on the card.
    /// </summary>
    internal sealed class EventCardTree : Dialog_NodeTree
    {
        internal EventCardWindow card;

        public EventCardTree(DiaNode root, bool radioMode, string title) : base(root, false, radioMode, title)
        {
        }

        /// <summary>The page an option led to (the game's own GotoNode sets it).</summary>
        internal DiaNode Node => curNode;

        public override void Close(bool doCloseSound = true)
        {
            if (card != null) card.Close(doCloseSound);
        }
    }

    /// <summary>
    /// Rimfolk's event card (docs/design/rimfolk-look.md, stage 3, point 1; key "EventCard" in Rimfolk's frame): a letter of
    /// Rimfolk's opened while Rimfolk's own look and "Rimfolk's event cards" are on. The letter arrives, sounds, shows and is
    /// dismissed as the game's always do; only its window is Rimfolk's. A header with a picture of who or what it is about
    /// (portraits from the game's cache, the thing, the people's emblem, else the kind's mark), its title, its kind in its
    /// colour and its people with their stance, and its jump to the place; the text; each answer as a card of its own with
    /// what it does (the setting "What each answer does") and what it costs or risks, greyed with why where it can't be
    /// chosen; who is involved, with their mood and the heart's band in the colonist bar's colours, a click to go to them;
    /// what the council leans to where it voted; the letter's links, its quest, postpone and close at the foot. The game's
    /// letter pauses the game and wants an answer, and so does the card. Everything is read when it opens; drawing is all
    /// it does each frame.
    /// </summary>
    public class EventCardWindow : FramedWindow
    {
        private ChoiceLetter letter;
        private EventCardTree tree;
        private EventCardModel m;
        private DiaNode shown;
        private Vector2 scroll;
        // The layout for a width, measured once for it (the text's and the cards' heights).
        private float laidW = -1f;
        private float textH, totalH, headH, titleH;
        private readonly List<float> answerH = new List<float>();
        private readonly List<float> answerRowH = new List<float>();
        private float sideH, leanH, linksH;
        private int cols, peopleCols;
        // An option chosen while drawing, done once the drawing is through (so the window's groups close as they opened).
        private EventCardOption chosen;
        private EventCardPerson goTo;
        private bool lookChosen;

        public override Vector2 InitialSize => new Vector2(EventCardMath.OpenW, Mathf.Min(EventCardMath.OpenHMax, UI.screenHeight - 100f));

        public EventCardWindow() : base("EventCard")
        {
            // As the game's letter window: the game waits, and the map waits for an answer.
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            preventCameraMotion = true;
            draggable = true;
            resizeable = true;
            soundAppear = SoundDefOf.CommsWindow_Open;
            soundClose = SoundDefOf.CommsWindow_Close;
        }

        /// <summary>Opens a letter as a card (one at a time: a card already open gives way). Throws if it can't be read, for the game's own window to take it.</summary>
        internal static void Open(ChoiceLetter letter)
        {
            var w = new EventCardWindow();
            w.Start(letter);
            Find.WindowStack.Add(w);
        }

        private void Start(ChoiceLetter l)
        {
            letter = l;
            // The letter's first page as the game's own window takes it: its text and its choices, made once.
            var root = new DiaNode(l.Text);
            root.options.AddRange(l.Choices);
            string t = l.title.NullOrEmpty() ? l.Label.Resolve() : l.title;
            tree = new EventCardTree(root, l.radioMode, t) { card = this };
            if (l.radioMode) soundAmbient = SoundDefOf.RadioComms_Ambience;
            shown = root;
            m = EventCardModel.Build(l, root);
        }

        protected override string FrameTitle => m?.bar ?? "";

        protected override Texture2D FrameIcon => KindIcon(m?.kind);

        /// <summary>A letter at its last tick, which the game opens for an answer, can't be closed without one; any other can, staying on the stack.</summary>
        protected override bool FrameClosable => letter is not LetterWithTimeout l || EventCardMath.Closable(l.TimeoutActive, l.disappearAtTick, Find.TickManager.TicksGame);

        public override void PreClose()
        {
            base.PreClose();
            shown?.PreClose();
        }

        public override void WindowOnGUI()
        {
            closeOnCancel = FrameClosable;
            base.WindowOnGUI();
        }

        /// <summary>The mark of a letter's kind: the frame's icon, and the header's picture when nothing else stands for it.</summary>
        private static Texture2D KindIcon(string kind) =>
            kind switch
            {
                "Question" or "Answer" => Icons.Dilemmas,
                "Meeting" => Icons.Ambition,
                "CalendarDay" => Icons.Week,
                "Election" => Icons.Office,
                "Event" => Icons.WorldEvent,
                "WorldNews" or "World" => Icons.World,
                "EventThreat" or "CampDanger" or "ThreatBig" or "Threat" => Icons.Raid,
                "WorldNewsBad" or "WorldThreat" or "Bad" => Icons.Warning,
                "Camp" => Icons.Visitor,
                "Peoples" => Icons.People,
                "Region" or "Journey" => Icons.Away,
                "Colonies" => Icons.Colony,
                "News" or "Told" or "Neutral" => Icons.Chronicle,
                "Rimfolk" => Icons.Almanac,
                "Good" => Icons.TrendUp,
                "Death" => Icons.Death,
                _ => Icons.Inbox
            };

        // ---------------------------------------------------------------- drawing

        protected override void DoFramedContents(Rect inRect)
        {
            if (m == null || letter == null)
            {
                Close(false);
                return;
            }
            chosen = null;
            goTo = null;
            lookChosen = false;
            bool scrolling = false;
            try
            {
                var foot = new Rect(inRect.x, inRect.yMax - EventCardMath.FootH - 4f, inRect.width, EventCardMath.FootH);
                var outer = new Rect(inRect.x, inRect.y + 4f, inRect.width, Mathf.Max(40f, foot.y - EventCardMath.FootGap - inRect.y - 4f));
                float w = outer.width - 16f;
                if (Mathf.Abs(w - laidW) > 0.5f) Measure(w);
                var view = new Rect(0f, 0f, w, Mathf.Max(totalH, outer.height - 4f));
                Widgets.BeginScrollView(outer, ref scroll, view);
                scrolling = true;
                Body(w);
                scrolling = false;
                Widgets.EndScrollView();
                Foot(foot);
            }
            catch (Exception e)
            {
                if (scrolling) Widgets.EndScrollView();
                CouncilLog.ErrorOnce("event card: drawing " + letter.GetType().Name, e);
                // The game's own window takes the letter, so it can always be answered.
                Close(false);
                EventCards.OpenInGame(letter);
                return;
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            Act();
        }

        /// <summary>What was clicked, done after the drawing: an option as the game does it, or off to someone or the place on the map.</summary>
        private void Act()
        {
            if (chosen != null)
            {
                var c = chosen;
                chosen = null;
                Choose(c);
                return;
            }
            if (goTo != null)
            {
                var p = goTo.pawn;
                goTo = null;
                Close();
                CameraJumper.TryJumpAndSelect(new GlobalTargetInfo(p));
                return;
            }
            if (lookChosen)
            {
                lookChosen = false;
                var t = m.look;
                Close();
                CameraJumper.TryJumpAndSelect(t);
            }
        }

        // ---------------------------------------------------------------- measuring, once a width

        private const float HeadPad = EventCardMath.HeadPad;

        /// <summary>The width the title has beside the picture and the jump button.</summary>
        private float TitleWidth(float w)
        {
            float jw = JumpWidth();
            float pw = EventCardMath.PictureWidth(m.pictured.Count, EventCardMath.PictureSizeFor(w));
            return Mathf.Max(EventCardMath.TitleLeast, w - HeadPad - (HeadPad + pw + EventCardMath.HeadGap) - (jw > 0f ? jw + 8f : 0f));
        }

        /// <summary>The title's height in its room: two lines of the large type at most.</summary>
        private float TitleHeight(float w)
        {
            Text.Font = GameFont.Medium;
            float th = Mathf.Min(Text.CalcHeight(m.title, TitleWidth(w)), 66f);
            Text.Font = GameFont.Small;
            return th;
        }

        /// <summary>The header's height: the picture, or the title, the kind and the people under it, with its room around.</summary>
        private float HeaderH(float w) => Mathf.Max(EventCardMath.PictureSizeFor(w), TitleHeight(w) + 6f + UiKit.ChipHeight + (m.faction != null ? 8f + 22f : 0f)) + 2f * HeadPad;

        private float JumpWidth()
        {
            if (m.jump != null) return Mathf.Clamp(UiKit.KeptWidth(m.jump.label, GameFont.Small) + 28f, EventCardMath.JumpLeast, EventCardMath.JumpMost);
            if (m.look.IsValid) return Mathf.Clamp(UiKit.KeptWidth(LookWord, GameFont.Small) + 28f, EventCardMath.JumpLeast, EventCardMath.JumpMost);
            return 0f;
        }

        // The card's own words, made once.
        private static string lookWord, lookTip, answersWord, involvedWord, leansHead, seeAlsoWord;
        private static string LookWord => lookWord ??= "CC_EventCard_Look".Translate().Resolve();
        private static string LookTip => lookTip ??= "CC_EventCard_LookTip".Translate().Resolve();
        private static string AnswersWord => answersWord ??= "CC_EventCard_Answers".Translate().Resolve();
        private static string InvolvedWord => involvedWord ??= "CC_EventCard_Involved".Translate().Resolve();
        private static string LeansHead => leansHead ??= "CC_EventCard_Leans".Translate().Resolve();
        private static string SeeAlsoWord => seeAlsoWord ??= "CC_EventCard_SeeAlso".Translate().Resolve();

        /// <summary>The heights of everything for this width: the text, each answer card, the side column; done when the width changes.</summary>
        private void Measure(float w)
        {
            laidW = w;
            float side = EventCardMath.SideWidth(w), main = EventCardMath.MainWidth(w);
            Text.Font = GameFont.Small;
            textH = m.text.NullOrEmpty() ? 0f : Text.CalcHeight(m.text, main);
            cols = EventCardMath.AnswerColumns(main, m.answers.Count);
            float aw = EventCardMath.AnswerWidth(main, cols);
            answerH.Clear();
            foreach (var a in m.answers) answerH.Add(AnswerHeight(a, aw));
            answerRowH.Clear();
            for (int i = 0; i < answerH.Count; i += cols)
            {
                float h = 0f;
                for (int j = i; j < Mathf.Min(answerH.Count, i + cols); j++) h = Mathf.Max(h, answerH[j]);
                answerRowH.Add(h);
            }
            float colW = side > 0f ? side : main;
            peopleCols = side > 0f ? 1 : Mathf.Max(1, Mathf.Min(m.people.Count, Mathf.FloorToInt((main + 10f) / 230f)));
            leanH = m.leanText.NullOrEmpty() ? 0f : UiKit.SectionHeight + Text.CalcHeight(m.leanText, colW) + (m.leanBy != null ? 30f : 0f) + 10f;
            linksH = m.links.Count == 0 ? 0f : UiKit.SectionHeight + LinksHeight(colW) + 10f;
            float peopleH = m.people.Count == 0 ? 0f : UiKit.SectionHeight + Mathf.CeilToInt(m.people.Count / (float)peopleCols) * EventCardMath.PersonRowH + (m.more > 0 ? 22f : 0f) + 8f;
            sideH = peopleH + leanH + linksH;
            headH = HeaderH(w);
            titleH = TitleHeight(w);
            float y = headH + 12f + (textH > 0f ? textH + 14f : 0f);
            if (side <= 0f) y += peopleH + leanH;
            if (m.answers.Count > 0)
            {
                y += UiKit.SectionHeight;
                foreach (var h in answerRowH) y += h + EventCardMath.AnswerGap;
            }
            if (side <= 0f) y += linksH;
            totalH = Mathf.Max(y, side > 0f ? headH + 12f + sideH : 0f) + 8f;
        }

        private float AnswerHeight(EventCardOption a, float w)
        {
            float inner = w - 24f;
            Text.Font = GameFont.Small;
            float h = 10f + Text.CalcHeight(a.label, inner);
            Text.Font = GameFont.Tiny;
            if (!a.line.NullOrEmpty()) h += 3f + Text.CalcHeight(a.line, inner);
            if (a.rows.Count > 0)
            {
                h += 6f;
                foreach (var r in a.rows) h += Text.CalcHeight(r.text, inner - 12f) + 1f;
                if (a.moreRows > 0) h += Text.CalcHeight(a.moreText, inner - 12f);
            }
            if (a.whyText != null) h += 3f + Text.CalcHeight(a.whyText, inner);
            if (a.leaned || a.custom != null || a.backers != null) h += 6f + 22f;
            Text.Font = GameFont.Small;
            return h + 10f;
        }


        // ---------------------------------------------------------------- the body

        private void Body(float w)
        {
            float side = EventCardMath.SideWidth(w), main = EventCardMath.MainWidth(w);
            Header(w);
            float y = headH + 12f;
            float sideTop = y;
            if (textH > 0f)
            {
                GUI.color = UiKit.Ink;
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(0f, y, main, textH), m.text);
                GUI.color = Color.white;
                y += textH + 14f;
            }
            if (side <= 0f)
            {
                y = People(0f, y, main);
                y = Lean(0f, y, main);
            }
            y = AnswerCards(0f, y, main);
            if (side <= 0f) Links(0f, y, main);
            if (side > 0f)
            {
                float x = main + EventCardMath.SideGap;
                float sy = People(x, sideTop, side);
                sy = Lean(x, sy, side);
                Links(x, sy, side);
            }
        }

        /// <summary>The picture, the title, the kind in its colour, the people with their stance, the jump to the place.</summary>
        private void Header(float w)
        {
            UiKit.Section(new Rect(0f, 0f, w, headH));
            float pad = HeadPad, inner = headH - 2f * pad;
            float size = EventCardMath.PictureSizeFor(w);
            var pic = new Rect(pad, pad + (inner - size) / 2f, EventCardMath.PictureWidth(m.pictured.Count, size), size);
            Picture(pic);
            float x = pic.xMax + EventCardMath.HeadGap;
            float jw = JumpWidth();
            float right = TitleWidth(w);
            // The jump to the place: the letter's own, else where it points.
            if (jw > 0f)
            {
                var jr = new Rect(w - pad - jw, pad, jw, 28f);
                if (m.jump != null)
                {
                    if (UiKit.Button(jr, m.jump.label, ButtonKind.Normal, !m.jump.disabled, m.jump.disabled ? m.jump.why : null)) chosen = m.jump;
                }
                else if (UiKit.Button(jr, LookWord, ButtonKind.Normal, true, LookTip)) lookChosen = true;
            }
            Text.Font = GameFont.Medium;
            GUI.color = UiKit.Ink;
            Widgets.Label(new Rect(x, pad, right, titleH), m.title);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            float y = pad + titleH + 6f;
            UiKit.StateChip(x, y, m.kindWord, m.kindColour, Mathf.Max(60f, w - x - pad));
            y += UiKit.ChipHeight + 8f;
            if (m.faction != null)
            {
                float fw = UiKit.FactionChip(x, y, m.faction, 22f, Mathf.Max(80f, (w - x - pad) * 0.6f));
                TooltipHandler.TipRegion(new Rect(x, y, fw, 22f), m.stanceTip);
                if (!m.stance.NullOrEmpty()) UiKit.StateChip(x + fw + 6f, y + 1f, m.stance, DipWords.StanceColor(m.faction), Mathf.Max(40f, w - x - fw - 6f - pad), m.stanceTip);
            }
        }

        /// <summary>The header's picture: the people's portraits overlapping, else the thing, else the people's emblem, else the kind's mark in its colour.</summary>
        private void Picture(Rect r)
        {
            float size = r.height;
            if (m.pictured.Count > 0)
            {
                // The first on top: drawn last.
                for (int i = m.pictured.Count - 1; i >= 0; i--)
                {
                    var pr = new Rect(r.x + i * size * EventCardMath.PictureStep, r.y, size, size);
                    UiKit.Portrait(pr, m.pictured[i], UiKit.WithAlpha(m.kindColour, i == 0 ? 0.95f : 0.6f));
                    TooltipHandler.TipRegion(pr, m.pictured[i].LabelShortCap);
                }
                return;
            }
            var tile = new Rect(r.x, r.y, size, size);
            if (m.thing != null || m.thingDef != null)
            {
                UiKit.ItemPortrait(tile, m.thing, m.thingDef, null, true);
                return;
            }
            UiKit.Picture(tile, null, m.faction, KindIcon(m.kind), m.kindColour, m.kindColour);
        }

        // ---------------------------------------------------------------- the answers

        private float AnswerCards(float x, float y, float w)
        {
            if (m.answers.Count == 0) return y;
            y += UiKit.SectionTitle(x, y, w, AnswersWord);
            float aw = EventCardMath.AnswerWidth(w, cols);
            for (int i = 0; i < m.answers.Count; i++)
            {
                int row = i / cols, col = i % cols;
                if (col == 0 && i > 0) y += answerRowH[row - 1] + EventCardMath.AnswerGap;
                var r = new Rect(x + col * (aw + EventCardMath.AnswerGap), y, aw, answerRowH[row]);
                AnswerCard(r, m.answers[i]);
            }
            return y + answerRowH[answerRowH.Count - 1] + EventCardMath.AnswerGap;
        }

        /// <summary>One answer as a card: its words, the line under it, what it costs or risks, why it can't be chosen now, the council's lean, the custom, who is for it.</summary>
        private void AnswerCard(Rect r, EventCardOption a)
        {
            bool can = !a.disabled;
            bool over = can && Mouse.IsOver(r);
            if (UiKit.OwnLook) UiKit.OwnCard(r, over, a.leaned, a.leaned);
            else UiKit.Card(r, a.leaned ? UiKit.Gold : (Color?)null, a.leaned, can);
            float inner = r.width - 24f, x = r.x + 12f, y = r.y + 10f;
            Text.Font = GameFont.Small;
            float lh = Text.CalcHeight(a.label, inner);
            GUI.color = can ? UiKit.Ink : UiKit.Dim;
            Widgets.Label(new Rect(x, y, inner, lh), a.label);
            y += lh;
            Text.Font = GameFont.Tiny;
            if (!a.line.NullOrEmpty())
            {
                float h = Text.CalcHeight(a.line, inner);
                GUI.color = can ? UiKit.Faint : UiKit.Dim;
                Widgets.Label(new Rect(x, y + 3f, inner, h), a.line);
                y += 3f + h;
            }
            if (a.rows.Count > 0)
            {
                y += 6f;
                foreach (var row in a.rows)
                {
                    float h = Text.CalcHeight(row.text, inner - 12f);
                    var c = row.risk && row.sign == 0 ? UiKit.Soon : row.sign > 0 ? UiKit.Good : row.sign < 0 ? UiKit.Bad : UiKit.Faint;
                    UiKit.DiscFill(new Rect(x + 1f, y + 5f, 6f, 6f), can ? c : UiKit.Dim);
                    GUI.color = can ? (row.risk && row.sign == 0 ? UiKit.Soon : UiKit.Ink) : UiKit.Dim;
                    Widgets.Label(new Rect(x + 12f, y, inner - 12f, h), row.text);
                    y += h + 1f;
                }
                if (a.moreRows > 0)
                {
                    float h = Text.CalcHeight(a.moreText, inner - 12f);
                    GUI.color = UiKit.Dim;
                    Widgets.Label(new Rect(x + 12f, y, inner - 12f, h), a.moreText);
                    y += h;
                }
            }
            if (a.whyText != null)
            {
                float h = Text.CalcHeight(a.whyText, inner);
                GUI.color = UiKit.Soon;
                Widgets.Label(new Rect(x, y + 3f, inner, h), a.whyText);
                y += 3f + h;
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            if (a.leaned || a.custom != null || a.backers != null)
            {
                y += 6f;
                float cx = x;
                if (a.leaned) cx += UiKit.Pill(cx, y, LeansWord, UiKit.Accent, Icons.Council, m.leanText) + 4f;
                if (a.custom != null) cx += UiKit.Pill(cx, y, CustomWord, UiKit.Gold, Icons.Week, a.custom) + 4f;
                if (a.backers != null)
                {
                    for (int i = 0; i < a.backers.Count && cx + 20f <= x + inner; i++)
                    {
                        UiKit.Portrait(new Rect(cx, y, 20f, 20f), a.backers[i], UiKit.Edge);
                        cx += 16f;
                    }
                    TooltipHandler.TipRegion(new Rect(x, y, inner, 22f), a.backersText);
                }
            }
            // The whole card answers; its full list (or why it can't) on hover.
            if (!a.cardTip.NullOrEmpty()) TooltipHandler.TipRegion(r, a.cardTip);
            if (!can) return;
            MouseoverSounds.DoRegion(r);
            if (Widgets.ButtonInvisible(r, false)) chosen = a;
        }

        private static string leansWord, customWord;
        private static string LeansWord => leansWord ??= "CC_EventCard_LeansHere".Translate().Resolve();
        private static string CustomWord => customWord ??= "CC_EventCard_CustomTag".Translate().Resolve();

        // ---------------------------------------------------------------- who is involved, the council's lean, the links

        private float People(float x, float y, float w)
        {
            if (m.people.Count == 0) return y;
            y += UiKit.SectionTitle(x, y, w, InvolvedWord, m.people.Count + m.more);
            float cw = (w - (peopleCols - 1) * 10f) / peopleCols;
            for (int i = 0; i < m.people.Count; i++)
            {
                int row = i / peopleCols, col = i % peopleCols;
                PersonRow(new Rect(x + col * (cw + 10f), y + row * EventCardMath.PersonRowH, cw, EventCardMath.PersonRowH - 4f), m.people[i]);
            }
            y += Mathf.CeilToInt(m.people.Count / (float)peopleCols) * EventCardMath.PersonRowH;
            if (m.more > 0)
            {
                var r = new Rect(x, y, w, 20f);
                UiKit.LabelFit(r, m.moreText, UiKit.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(r, m.moreTip);
                y += 22f;
            }
            return y + 8f;
        }

        /// <summary>Someone involved: their portrait, name, mood on a bar coloured by how near a break, the heart's band as the colonist bar shows it.</summary>
        private void PersonRow(Rect r, EventCardPerson p)
        {
            if (p.canJump) UiKit.RowHover(r);
            float s = EventCardMath.PersonPortrait;
            var moodColour = MoodColour(p.moodLevel);
            UiKit.Portrait(new Rect(r.x + 2f, r.y + (r.height - s) / 2f, s, s), p.pawn, p.mood >= 0f ? UiKit.WithAlpha(moodColour, 0.9f) : UiKit.Edge);
            // One of the colony's own: their card's button at the row's right (UI/ColonistCard/), the rest of the row still going to them.
            float card = ColonistCardWindow.On && ColonistCardWindow.Fits(p.pawn) ? 26f : 0f;
            float x = r.x + s + 10f, w = r.xMax - x - 4f - card, end = r.xMax - card;
            UiKit.LabelFit(new Rect(x, r.y + 2f, w, 20f), p.name, UiKit.Ink);
            float y = r.y + 22f;
            if (p.mood >= 0f)
            {
                float bw = Mathf.Min(90f, w * 0.45f);
                UiKit.Bar(new Rect(x, y + 6f, bw, 6f), p.mood, moodColour);
                float cx = x + bw + 6f;
                if (p.heart) UiKit.StateChip(cx, y, p.heartWord, HeartColour(p), Mathf.Max(30f, end - cx - 4f));
                else if (p.sub != null) UiKit.LabelFit(new Rect(cx, y, end - cx - 4f, 20f), p.sub, UiKit.Faint, GameFont.Tiny);
            }
            else if (p.sub != null) UiKit.LabelFit(new Rect(x, y, w, 20f), p.sub, UiKit.Faint, GameFont.Tiny);
            if (card > 0f) ColonistCardEntry.RowButton(r, p.pawn);
            TooltipHandler.TipRegion(r, p.tip);
            if (p.canJump && Widgets.ButtonInvisible(r, false))
            {
                SoundDefOf.Click.PlayOneShotOnCamera();
                goTo = p;
            }
        }

        /// <summary>A mood by how near a break it is, as the colonist bar marks it: fine, a minor break near, a major, an extreme.</summary>
        private static Color MoodColour(int level) => level >= 3 ? UiKit.Danger : level == 2 ? UiKit.Sour : level == 1 ? UiKit.Soon : UiKit.Good;

        /// <summary>The heart's band in the colonist bar's colours: strain in its band's, the good side in its own.</summary>
        private static Color HeartColour(EventCardPerson p) => p.band == StrainBand.Steady && p.good >= GoodBand.GoodHeart ? HeartView.GoodColor : HeartView.BandColor(p.band);

        private float Lean(float x, float y, float w)
        {
            if (m.leanText.NullOrEmpty()) return y;
            y += UiKit.SectionTitle(x, y, w, LeansHead, Icons.Council);
            Text.Font = GameFont.Small;
            float h = Text.CalcHeight(m.leanText, w);
            GUI.color = UiKit.Ink;
            Widgets.Label(new Rect(x, y, w, h), m.leanText);
            GUI.color = Color.white;
            y += h;
            if (m.leanBy != null)
            {
                float cx = x;
                y += 6f;
                foreach (var p in m.leanBy)
                {
                    if (cx + 24f > x + w) break;
                    var pr = new Rect(cx, y, 24f, 24f);
                    UiKit.Portrait(pr, p, UiKit.WithAlpha(UiKit.Accent, 0.8f));
                    TooltipHandler.TipRegion(pr, p.LabelShortCap);
                    cx += 28f;
                }
                y += 24f;
            }
            return y + 10f;
        }

        /// <summary>A link's button: its icon and its word.</summary>
        private static float LinkWidth(EventCardOption l, float w) => Mathf.Min(w, UiKit.KeptWidth(l.label, GameFont.Small) + 44f);

        private float LinksHeight(float w)
        {
            float cx = 0f, cy = 0f;
            foreach (var l in m.links)
            {
                float bw = LinkWidth(l, w);
                if (cx > 0f && cx + bw > w)
                {
                    cx = 0f;
                    cy += 32f;
                }
                cx += bw + 6f;
            }
            return cy + 28f;
        }

        /// <summary>The letter's links to info cards ("See also"), each as the game's own: a click opens its card.</summary>
        private void Links(float x, float y, float w)
        {
            if (m.links.Count == 0) return;
            y += UiKit.SectionTitle(x, y, w, SeeAlsoWord);
            float cx = x;
            foreach (var l in m.links)
            {
                float bw = LinkWidth(l, w);
                if (cx > x && cx + bw > x + w)
                {
                    cx = x;
                    y += 32f;
                }
                var r = new Rect(cx, y, bw, 28f);
                if (UiKit.Button(r, "", ButtonKind.Quiet, true, l.label)) chosen = l;
                if (l.linkDef is ThingDef td && Widgets.CanDrawIconFor(td)) Widgets.DefIcon(new Rect(r.x + 8f, r.y + 5f, 18f, 18f), td);
                else UiKit.Icon(new Rect(r.x + 8f, r.y + 5f, 18f, 18f), TexButton.Info, UiKit.Faint);
                UiKit.LabelFit(new Rect(r.x + 32f, r.y, r.width - 36f, r.height), l.label, UiKit.Link);
                cx += bw + 6f;
            }
        }

        // ---------------------------------------------------------------- the foot

        /// <summary>What the letter lets you do beside answering, at the left; its quest, postpone and close, at the right.</summary>
        private void Foot(Rect r)
        {
            UiKit.Divider(r.x, r.y - 4f, r.width);
            float x = r.xMax;
            for (int i = m.leave.Count - 1; i >= 0; i--)
            {
                var o = m.leave[i];
                float bw = Mathf.Clamp(UiKit.KeptWidth(o.label, GameFont.Small) + 28f, 90f, 220f);
                x -= bw;
                // A letter with no answers: its last way out (its close) is the card's main button.
                var kind = i == m.leave.Count - 1 && m.answers.Count == 0 ? ButtonKind.Primary : ButtonKind.Normal;
                if (UiKit.Button(new Rect(x, r.y + 2f, bw, r.height - 4f), o.label, kind, !o.disabled, o.disabled ? o.why : null)) chosen = o;
                x -= 8f;
            }
            if (m.quest != null)
            {
                float bw = Mathf.Clamp(UiKit.KeptWidth(m.quest.label, GameFont.Small) + 28f, 110f, 300f);
                x -= bw;
                if (UiKit.Button(new Rect(x, r.y + 2f, bw, r.height - 4f), m.quest.label, ButtonKind.Normal, !m.quest.disabled, m.quest.disabled ? m.quest.why : null)) chosen = m.quest;
                x -= 8f;
            }
            float ax = r.x;
            foreach (var o in m.acts)
            {
                float bw = Mathf.Clamp(UiKit.KeptWidth(o.label, GameFont.Small) + 28f, 90f, 260f);
                if (ax + bw > x) break;
                if (UiKit.Button(new Rect(ax, r.y + 2f, bw, r.height - 4f), o.label, ButtonKind.Quiet, !o.disabled, o.disabled ? o.why : null)) chosen = o;
                ax += bw + 8f;
            }
        }

        // ---------------------------------------------------------------- choosing

        /// <summary>The game's own DiaOption.Activate: found once.</summary>
        private static readonly Action<DiaOption> activate = FindActivate();

        private static Action<DiaOption> FindActivate()
        {
            try
            {
                var method = AccessTools.Method(typeof(DiaOption), "Activate");
                return method == null ? null : AccessTools.MethodDelegate<Action<DiaOption>>(method);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("event card: the game's way to choose an option", e);
                return null;
            }
        }

        /// <summary>
        /// An option chosen, done as the game's own window does it: through the game's own Activate with the hidden window as
        /// its owner (its sound, closing, its action, the page it leads to), or the same steps where Activate can't be found.
        /// A page it leads to is shown on the card.
        /// </summary>
        private void Choose(EventCardOption c)
        {
            var o = c.opt;
            if (o == null || o.disabled) return;
            try
            {
                if (c.role == CardOptionRole.Link)
                {
                    o.hyperlink.ActivateHyperlink();
                    return;
                }
                o.dialog = tree;
                if (activate != null) activate(o);
                else
                {
                    if (o.clickSound != null && !o.resolveTree) o.clickSound.PlayOneShotOnCamera();
                    if (o.resolveTree) Close();
                    o.action?.Invoke();
                    if (o.linkLateBind != null) tree.GotoNode(o.linkLateBind());
                    else if (o.link != null) tree.GotoNode(o.link);
                }
            }
            catch (Exception e)
            {
                Log.Error("[Rimfolk] An option of the letter \"" + m.title + "\" failed: " + e);
            }
            var next = tree.Node;
            if (next != null && next != shown && Find.WindowStack.IsOpen(this))
            {
                shown = next;
                m.Page(next, false);
                laidW = -1f;
                scroll = Vector2.zero;
            }
        }
    }
}
