using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The events record on Council → Colony → Overview (Story/EventRecord.cs keeps it): a card for each kind of event, with
    /// how many came in the last 30 days and the newest few by name and day, and under them how Rimfolk's events and the
    /// game's quests come now and what was held back and why. Built at most once a second, so drawing asks nothing of the game.
    /// </summary>
    public static class EventRecordPanel
    {
        private sealed class Card
        {
            public string title;
            public int count;
            public readonly List<string> lines = new List<string>();
            public Texture2D icon;
        }

        private sealed class Model
        {
            public readonly List<Card> cards = new List<Card>();
            /// <summary>How Rimfolk's events and the game's quests come now, from the settings and the storyteller's pool.</summary>
            public readonly List<string> how = new List<string>();
            /// <summary>What was held back in the 30 days, and why.</summary>
            public readonly List<string> held = new List<string>();
        }

        private static Model model;
        private static float modelAt = -1f;

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Something came: the next draw builds the section again.</summary>
        public static void Dirty() => modelAt = -1f;

        private static readonly EventKind[] Shown =
        {
            EventKind.Threat, EventKind.Everyday, EventKind.QuestHome, EventKind.QuestAway, EventKind.Rimfolk, EventKind.Question, EventKind.GodCard
        };

        private static Texture2D IconOf(EventKind k)
        {
            switch (k)
            {
                case EventKind.Threat: return Icons.Raid;
                case EventKind.Everyday: return Icons.Visitor;
                case EventKind.QuestHome: return Icons.Colony;
                case EventKind.QuestAway: return Icons.Away;
                case EventKind.Rimfolk: return Icons.WorldEvent;
                case EventKind.Question: return Icons.Dilemmas;
                case EventKind.GodCard: return Icons.Journal;
                default: return Icons.Chronicle;
            }
        }

        private static Model Build()
        {
            var m = new Model();
            var record = EventRecord.Instance;
            int now = Find.TickManager.TicksGame;
            var entries = new List<(int tick, EventKind kind, string what)>();
            if (record != null) entries.AddRange(record.Entries());
            entries.AddRange(EventRecord.Quests(now));
            var sums = EventRecordMath.Sum(entries, now);
            foreach (var k in Shown)
            {
                var c = new Card { title = ("CC_Record_Kind_" + k).Translate(), icon = IconOf(k) };
                if (sums.TryGetValue(k, out var s))
                {
                    c.count = s.count;
                    foreach (var (what, day) in s.last)
                        c.lines.Add("CC_Record_Line".Translate(day.Named("DAY"), (what.NullOrEmpty() ? "CC_Record_Unnamed".Translate().ToString() : what).Named("WHAT")).Resolve());
                    if (s.count > s.last.Count) c.lines.Add("CC_Record_More".Translate((s.count - s.last.Count).Named("COUNT")).Resolve());
                }
                m.cards.Add(c);
            }
            How(m.how);
            Held(m.held, entries, now);
            return m;
        }

        /// <summary>How Rimfolk's events come, and the two settings for the game's quests.</summary>
        private static void How(List<string> how)
        {
            if (S == null) return;
            if (!S.worldEvents) how.Add("CC_Record_How_Off".Translate());
            else if (!S.eventsFromStoryteller)
                how.Add("CC_Record_How_Own".Translate(Mathf.Max(0.5f, S.daysBetweenWorldEvents).ToString("0.#").Named("DAYS")).Resolve());
            else if (EventRecordMath.BesidesOn(S.ownTimerShare))
                how.Add("CC_Record_How_Both".Translate(EventRecordMath.BesidesDays(S.ownTimerShare).ToString("0.#").Named("DAYS")).Resolve());
            else how.Add("CC_Record_How_Storyteller".Translate());
            if (!RimfolkPatches.On)
            {
                how.Add("CC_Record_How_NoHooks".Translate());
                return;
            }
            how.Add(QuestPlaces.Holding
                ? "CC_Record_How_Away".Translate(QuestPlaces.Share.ToStringPercent().Named("PCT")).Resolve()
                : "CC_Record_How_AwayAll".Translate().ToString());
            var range = S.questSiteRange;
            how.Add(range == QuestSiteRange.AsTheGame
                ? "CC_Record_How_SitesGame".Translate(QuestPlaceMath.GameMax.Named("MAX")).Resolve()
                : "CC_Record_How_SitesNearer".Translate(QuestPlaceMath.GameFarthest(range).Named("MAX"), QuestPlaceMath.GameMax.Named("GAME")).Resolve());
        }

        /// <summary>What was held back in the 30 days, by reason, and the half-share cap on the storyteller's everyday pool as it stands.</summary>
        private static void Held(List<string> held, List<(int tick, EventKind kind, string what)> entries, int now)
        {
            var counts = EventRecordMath.HeldCounts(entries, now);
            foreach (var reason in EventRecordMath.HeldReasons)
                if (counts.TryGetValue(reason, out int n) && n > 0)
                    held.Add(("CC_Record_Held_" + reason).Translate(n.Named("COUNT")).Resolve());
            if (S == null || !S.worldEvents || !S.eventsFromStoryteller) return;
            float cut = HappeningTracker.ShareCut(IncidentCategoryDefOf.Misc);
            held.Add(cut < 0.995f
                ? "CC_Record_Held_Cap".Translate(cut.ToStringPercent().Named("PCT")).Resolve()
                : "CC_Record_Held_NoCap".Translate().ToString());
        }

        /// <summary>The section, from <paramref name="y"/> across <paramref name="w"/>; returns where it ends.</summary>
        public static float Draw(float y, float w)
        {
            if (model == null || modelAt < 0f || Time.realtimeSinceStartup - modelAt > 1f || Time.realtimeSinceStartup < modelAt)
            {
                modelAt = Time.realtimeSinceStartup;
                try
                {
                    model = Build();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("events record: the overview", e);
                    model ??= new Model();
                }
            }
            var m = model;
            y += UiKit.SectionTitle(0f, y, w, "CC_Record_Title".Translate(), Icons.Chronicle);
            y += UiKit.Paragraph(0f, y, w, "CC_Record_Intro".Translate(), UiKit.Faint, GameFont.Tiny) + 8f;
            float gw = (w - UiKit.CardGap) / 2f;
            for (int i = 0; i < m.cards.Count; i += 2)
            {
                float left = KindCard(0f, y, gw, m.cards[i]);
                float right = i + 1 < m.cards.Count ? KindCard(gw + UiKit.CardGap, y, gw, m.cards[i + 1]) : y;
                y = Mathf.Max(left, right) + UiKit.CardGap;
            }
            y = Lines(0f, y, w, "CC_Record_HowTitle".Translate(), m.how, Icons.WorldEvent) + UiKit.CardGap;
            y = Lines(0f, y, w, "CC_Record_HeldTitle".Translate(), m.held.Count > 0 ? m.held : new List<string> { "CC_Record_Held_None".Translate().ToString() }, Icons.Warning);
            GUI.color = UiKit.Ink;
            Text.Font = GameFont.Small;
            return y;
        }

        /// <summary>A kind's card: its name with how many came on a plate, then the newest few by day; "none" when none came.</summary>
        private static float KindCard(float x, float y, float w, Card c)
        {
            var lines = c.lines.Count > 0 ? c.lines : new List<string> { "CC_Record_None".Translate().ToString() };
            float textW = w - 24f;
            float h = 32f + 6f;
            foreach (var l in lines) h += LinkText.Height(l, textW, GameFont.Tiny) + 4f;
            UiKit.Card(new Rect(x, y, w, h), null, false, false);
            UiKit.SectionTitle(x + 8f, y + 4f, w - 16f, c.title, c.count, c.icon, UiKit.Faint);
            float ly = y + 32f;
            foreach (var l in lines)
            {
                float lh = LinkText.Height(l, textW, GameFont.Tiny);
                UiKit.DiscFill(new Rect(x + 8f, ly + 5f, 4f, 4f), UiKit.Dim);
                LinkText.Draw(new Rect(x + 16f, ly, textW, lh), l, c.lines.Count > 0 ? UiKit.Ink : UiKit.Faint, GameFont.Tiny);
                ly += lh + 4f;
            }
            return y + h;
        }

        /// <summary>A wide card of lines under a heading.</summary>
        private static float Lines(float x, float y, float w, string title, List<string> lines, Texture2D icon)
        {
            float textW = w - 24f;
            float h = 32f + 6f;
            foreach (var l in lines) h += LinkText.Height(l, textW, GameFont.Tiny) + 4f;
            UiKit.Card(new Rect(x, y, w, h), null, false, false);
            UiKit.SectionTitle(x + 8f, y + 4f, w - 16f, title, icon, UiKit.Faint);
            float ly = y + 32f;
            foreach (var l in lines)
            {
                float lh = LinkText.Height(l, textW, GameFont.Tiny);
                UiKit.DiscFill(new Rect(x + 8f, ly + 5f, 4f, 4f), UiKit.Dim);
                LinkText.Draw(new Rect(x + 16f, ly, textW, lh), l, UiKit.Ink, GameFont.Tiny);
                ly += lh + 4f;
            }
            return y + h;
        }
    }
}
