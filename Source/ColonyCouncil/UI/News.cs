using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What a piece of news is about: the heading it goes under in the evening's colony news.</summary>
    public enum NewsKind
    {
        /// <summary>The colonists' own lives: bonds, quirks, personal stories, dreams, names, birthdays.</summary>
        People,
        /// <summary>Everyday ailments, recoveries, convalescence, doctors' and midwives' good names.</summary>
        Health,
        /// <summary>The council and the home: wishes, decrees, ambitions, offices, the calendar and its traditions.</summary>
        Colony,
        /// <summary>Strangers, camps, missions, world events and the factions.</summary>
        World,
        /// <summary>The seasons, the weather and the animals.</summary>
        Land
    }

    /// <summary>Where Rimfolk's minor news goes (the "Colony news" setting).</summary>
    public enum NewsMode
    {
        /// <summary>A message for each, the moment it happens (how Rimfolk always did it).</summary>
        AsItHappens,
        /// <summary>Gathered into one "Colony news" letter in the evening (the default).</summary>
        Digest,
        /// <summary>No message: only a line in the message log of the History tab.</summary>
        Off
    }

    /// <summary>One piece of news: waiting for the evening, or kept in the letter that told it.</summary>
    public class NewsItem : IExposable
    {
        public string text;
        public NewsKind kind;
        /// <summary>Where it happened, or null.</summary>
        public LookTargets look;
        /// <summary>How it would have come as a message (its sound and colour).</summary>
        public MessageTypeDef type;

        public void ExposeData()
        {
            // A place on a map that's gone (a quest site left behind) can't be saved or gone to.
            if (Scribe.mode == LoadSaveMode.Saving && look?.targets != null)
                look.targets.RemoveAll(t => t.Map != null && !Find.Maps.Contains(t.Map));
            Scribe_Values.Look(ref text, "text");
            Scribe_Values.Look(ref kind, "kind", NewsKind.People);
            Scribe_Deep.Look(ref look, "look");
            Scribe_Defs.Look(ref type, "type");
        }
    }

    /// <summary>
    /// Where the smaller news of Rimfolk's many systems goes (quirks, personal stories, birthdays, health records,
    /// traditions, dreams, camps, moments, encounters, names...): a message the moment it happens, a line in the
    /// evening's "Colony news" letter (the default), or only the History tab's message log, by the "Colony news"
    /// setting. Decisions (dilemmas, petitions, wishes) and serious news (deaths, raids, serious illness, a camp
    /// arriving) don't come through here: they keep their own letters and messages.
    /// </summary>
    public static class News
    {
        /// <summary>The most news kept waiting for the evening; past it, the oldest gives way.</summary>
        public const int MaxWaiting = 40;

        /// <summary>The local hour the day's news comes in.</summary>
        public const int EveningHour = 18;

        public static readonly NewsKind[] Kinds = (NewsKind[])Enum.GetValues(typeof(NewsKind));

        public static NewsMode Mode => ColonyCouncilMod.Settings?.colonyNews ?? NewsMode.Digest;

        /// <summary>
        /// One piece of minor news. With an importance, the "How many messages" setting first decides whether it's told
        /// at all (as Notify.Message always did); without one it always is, as the plain messages always were.
        /// </summary>
        public static void Post(string text, LookTargets look, NewsKind kind, MessageTypeDef type = null, Importance? importance = null)
        {
            if (text.NullOrEmpty()) return;
            if (importance.HasValue && !Notify.Shows(importance.Value)) return;
            var item = new NewsItem { text = text, kind = kind, look = look != null && look.IsValid ? look : null, type = type ?? MessageTypeDefOf.NeutralEvent };
            try
            {
                switch (Mode)
                {
                    case NewsMode.Digest:
                        var waiting = NewsComponent.Instance;
                        if (waiting == null) break;
                        waiting.Add(item);
                        return;
                    case NewsMode.Off:
                        if (Find.Archive == null) break;
                        Archive(item);
                        return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony news", e);
            }
            Show(item);
        }

        /// <summary>A message on screen. Rimfolk's messages as they happen were never kept in the History tab.</summary>
        internal static void Show(NewsItem item, bool historical = false) =>
            Messages.Message(item.text, item.look ?? LookTargets.Invalid, item.type ?? MessageTypeDefOf.NeutralEvent, historical);

        /// <summary>Straight into the History tab's message log, as the game keeps its own messages, without showing it.</summary>
        internal static void Archive(NewsItem item) =>
            Find.Archive.Add(new Message(item.text, item.type ?? MessageTypeDefOf.NeutralEvent, item.look ?? LookTargets.Invalid));

        public static string KindLabel(NewsKind k) => ("CC_News_Kind_" + k).Translate();

        public static Texture2D KindIcon(NewsKind k)
        {
            switch (k)
            {
                case NewsKind.People: return Icons.People;
                case NewsKind.Health: return Icons.CatHealth;
                case NewsKind.Colony: return Icons.Colony;
                case NewsKind.World: return Icons.World;
                default: return Icons.CatFood;
            }
        }

        /// <summary>The letter's text: the news under its headings, a line each (what the letter stack and History show).</summary>
        public static string LetterText(List<NewsItem> items)
        {
            var sb = new StringBuilder();
            foreach (var k in Kinds)
            {
                bool any = false;
                foreach (var i in items)
                {
                    if (i.kind != k) continue;
                    if (!any)
                    {
                        if (sb.Length > 0) sb.Append("\n\n");
                        sb.Append(KindLabel(k).CapitalizeFirst().Colorize(ColoredText.TipSectionTitleColor));
                        any = true;
                    }
                    sb.Append("\n  - ").Append(i.text);
                }
            }
            return sb.ToString();
        }

        /// <summary>The evening's letter; a single piece of news comes as a message instead.</summary>
        public static void Send(List<NewsItem> items)
        {
            if (items.NullOrEmpty()) return;
            if (items.Count == 1)
            {
                Show(items[0], true);
                return;
            }
            string label = "CC_News_Label".Translate();
            string text = LetterText(items);
            var def = DefDatabase<LetterDef>.GetNamedSilentFail("Council_NewsLetter");
            if (def != null && typeof(ChoiceLetter_News).IsAssignableFrom(def.letterClass))
            {
                var letter = (ChoiceLetter_News)LetterMaker.MakeLetter(label, text, def);
                letter.items = items;
                Find.LetterStack.ReceiveLetter(letter);
                return;
            }
            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NeutralEvent);
        }

        /// <summary>A piece of news whose place can still be gone to: not gone from the world, not on a map that's gone.</summary>
        public static bool CanGo(NewsItem item)
        {
            if (item?.look == null || !item.look.IsValid) return false;
            var t = item.look.TryGetPrimaryTarget();
            if (!t.IsValid || t.HasThing && t.Thing.Destroyed) return false;
            if (t.Map != null && !Find.Maps.Contains(t.Map)) return false;
            return CameraJumper.CanJump(t);
        }

        [DebugAction("Rimfolk", "Colony news: send today's news now", allowedGameStates = AllowedGameStates.Playing)]
        private static void DebugSendNow()
        {
            var comp = NewsComponent.Instance;
            if (comp == null || comp.Waiting == 0)
            {
                Messages.Message("Colony news: nothing is waiting for the evening.", MessageTypeDefOf.SilentInput, false);
                return;
            }
            comp.SendNow();
        }
    }

    /// <summary>
    /// Keeps the day's minor news until the evening (saved with the game, so a save and load mid-day loses nothing) and
    /// sends it around six as one "Colony news" letter. If the setting moves away from the digest, what was waiting
    /// goes out at once the new way.
    /// </summary>
    public class NewsComponent : GameComponent
    {
        private List<NewsItem> waiting = new List<NewsItem>();

        /// <summary>The last local day (year * 60 + day of the year) whose evening has passed, so each day's news comes once.</summary>
        private int lastEvening = -1;

        public NewsComponent(Game game)
        {
        }

        public static NewsComponent Instance => Current.Game?.GetComponent<NewsComponent>();

        public int Waiting => waiting.Count;

        public void Add(NewsItem item)
        {
            // The same line twice in a day says nothing new.
            for (int i = 0; i < waiting.Count; i++)
                if (waiting[i].text == item.text) return;
            waiting.Add(item);
            if (waiting.Count > News.MaxWaiting) waiting.RemoveRange(0, waiting.Count - News.MaxWaiting);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref waiting, "waiting", LookMode.Deep);
            Scribe_Values.Look(ref lastEvening, "lastEvening", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                waiting ??= new List<NewsItem>();
                waiting.RemoveAll(i => i == null || i.text.NullOrEmpty());
            }
        }

        public override void GameComponentTick()
        {
            // A few times an hour is plenty for "around six".
            if (Find.TickManager.TicksGame % 250 != 167) return;
            try
            {
                Check();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("colony news evening", e);
                waiting.Clear();
            }
        }

        private void Check()
        {
            var mode = News.Mode;
            if (mode != NewsMode.Digest)
            {
                if (waiting.Count > 0) Flush(mode);
                return;
            }
            Clock(out int hour, out int day);
            if (hour < News.EveningHour || day == lastEvening) return;
            lastEvening = day;
            if (waiting.Count > 0) SendNow();
        }

        public void SendNow()
        {
            var items = waiting.ToList();
            waiting.Clear();
            News.Send(items);
        }

        /// <summary>The setting changed: what was waiting goes out the new way.</summary>
        private void Flush(NewsMode mode)
        {
            var items = waiting.ToList();
            waiting.Clear();
            foreach (var i in items)
            {
                if (mode == NewsMode.Off && Find.Archive != null) News.Archive(i);
                else News.Show(i);
            }
        }

        /// <summary>The hour and day where the colony lives (else at the world's prime meridian).</summary>
        private static void Clock(out int hour, out int day)
        {
            var map = Find.AnyPlayerHomeMap ?? Find.CurrentMap;
            if (map != null)
            {
                hour = GenLocalDate.HourOfDay(map);
                day = GenLocalDate.Year(map) * GenDate.DaysPerYear + GenLocalDate.DayOfYear(map);
                return;
            }
            long abs = Find.TickManager.TicksAbs;
            hour = GenDate.HourOfDay(abs, 0f);
            day = GenDate.Year(abs, 0f) * GenDate.DaysPerYear + GenDate.DayOfYear(abs, 0f);
        }
    }

    /// <summary>The evening's colony news. Opened, it shows the news under its headings, each line with a way to go there.</summary>
    public class ChoiceLetter_News : ChoiceLetter
    {
        public List<NewsItem> items = new List<NewsItem>();

        public override IEnumerable<DiaOption> Choices
        {
            get { yield return Option_Close; }
        }

        public override void OpenLetter()
        {
            if (items.NullOrEmpty())
            {
                base.OpenLetter();
                return;
            }
            Find.WindowStack.Add(new Dialog_News(this));
        }

        /// <summary>Read and put away: off the letter stack (it stays in the History tab), as the game's own Close does.</summary>
        public void PutAway()
        {
            if (!ArchivedOnly) Find.LetterStack.RemoveLetter(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref items, "newsItems", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                items ??= new List<NewsItem>();
                items.RemoveAll(i => i == null || i.text.NullOrEmpty());
            }
        }
    }

    /// <summary>The colony news, read: a heading for each kind of news and a line for each piece, with "Go there" where it has a place.</summary>
    public class Dialog_News : Window
    {
        private const float GoWidth = 84f;

        private readonly ChoiceLetter_News letter;
        private Vector2 scroll;
        private float viewHeight;

        public Dialog_News(ChoiceLetter_News letter)
        {
            this.letter = letter;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
        }

        public override Vector2 InitialSize => new Vector2(640f, Mathf.Min(660f, UI.screenHeight - 40f));

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, inRect.width - 30f, 36f), letter.Label.CapitalizeFirst());
            Text.Font = GameFont.Small;
            y += 38f;
            y += UiKit.Paragraph(inRect.x, y, inRect.width, "CC_News_Intro".Translate(), UiKit.Faint, GameFont.Tiny) + 6f;

            var outer = new Rect(inRect.x, y, inRect.width, inRect.yMax - y - CloseButSize.y - 12f);
            var view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(viewHeight, outer.height - 1f));
            Widgets.BeginScrollView(outer, ref scroll, view);
            float cy = 0f;
            bool first = true;
            foreach (var k in News.Kinds)
            {
                bool any = false;
                foreach (var item in letter.items)
                {
                    if (item.kind != k) continue;
                    if (!any)
                    {
                        if (!first) cy += 8f;
                        cy += UiKit.SectionTitle(0f, cy, view.width, News.KindLabel(k).CapitalizeFirst(), News.KindIcon(k));
                        any = true;
                        first = false;
                    }
                    cy += Line(item, view.width, cy);
                }
            }
            viewHeight = cy;
            Widgets.EndScrollView();

            var close = new Rect(inRect.x + (inRect.width - CloseButSize.x) / 2f, inRect.yMax - CloseButSize.y, CloseButSize.x, CloseButSize.y);
            if (Widgets.ButtonText(close, "CloseButton".Translate()))
            {
                letter.PutAway();
                Close();
            }
        }

        /// <summary>One piece of news: its line, and "Go there" when its place can still be gone to. Returns the height.</summary>
        private float Line(NewsItem item, float width, float y)
        {
            bool go = News.CanGo(item);
            float textW = width - 12f - (go ? GoWidth + 8f : 0f);
            Text.Font = GameFont.Small;
            float h = Mathf.Max(28f, Text.CalcHeight(item.text, textW) + 6f);
            var row = new Rect(0f, y, width, h);
            if (go && Mouse.IsOver(row))
            {
                Widgets.DrawHighlight(row);
                item.look.TryHighlight();
            }
            Widgets.DrawBoxSolid(new Rect(2f, y + 6f, 3f, h - 12f), Tint(item));
            Widgets.Label(new Rect(12f, y + 3f, textW, h - 3f), item.text);
            if (go)
            {
                var b = new Rect(width - GoWidth, y + (h - 24f) / 2f, GoWidth, 24f);
                if (Widgets.ButtonText(b, "CC_News_Go".Translate()))
                {
                    var target = item.look.TryGetPrimaryTarget();
                    Close();
                    CameraJumper.TryJumpAndSelect(target);
                }
                TooltipHandler.TipRegion(b, "CC_News_GoTip".Translate());
            }
            return h + 2f;
        }

        /// <summary>A faint mark in the colour the message would have had: good news green, bad news red, the rest grey.</summary>
        private static Color Tint(NewsItem item)
        {
            if (item.type == MessageTypeDefOf.PositiveEvent) return UiKit.WithAlpha(UiKit.Good, 0.7f);
            if (item.type == MessageTypeDefOf.NegativeEvent || item.type == MessageTypeDefOf.NegativeHealthEvent) return UiKit.WithAlpha(UiKit.Bad, 0.7f);
            return UiKit.DividerColor;
        }
    }
}
