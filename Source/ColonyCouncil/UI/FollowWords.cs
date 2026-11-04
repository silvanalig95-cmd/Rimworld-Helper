using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One thing followed, as This week, the corner's list and the Following panel show it: its name, its newest change in one line.</summary>
    public sealed class FollowLine
    {
        public ThingRef thing;
        /// <summary>The name as a link to its card.</summary>
        public string name;
        /// <summary>Its newest change in one line, with links; plain in <see cref="plainNews"/>.</summary>
        public string news, plainNews;
        public Color newsColor = UiKit.Faint;
        public Pawn pawn;
        public Faction faction;
        public Texture2D icon;
        public Color iconColor = Color.white;
        public bool gone;
    }

    /// <summary>
    /// What is followed (WeekMemory.following, at most twelve, any thing a card shows) with each one's newest change in
    /// one line: for a colonist how they are (strain, the feeling on top, an open wish, their mood); for anyone or
    /// anything else the newest line of the history about it in the last days, else where it stands (a person's regard,
    /// a town's fortune, a people's stance, a tie's state, a letter's step). Worked out at most once a second and only
    /// while something shows it (This week, the corner's list, the Following panel).
    /// </summary>
    public static class FollowWords
    {
        /// <summary>The history's newest line counts as news this many days.</summary>
        public const int NewsDays = 15;

        private static readonly List<FollowLine> lines = new List<FollowLine>();
        private static float builtAt = -1f;
        private static Game builtFor;

        public static void Dirty() => builtAt = -1f;

        /// <summary>Every thing followed, in the order followed, each with its newest change (built again after a second).</summary>
        public static List<FollowLine> Lines()
        {
            float now = Time.realtimeSinceStartup;
            if (builtAt >= 0f && now - builtAt < 1f && now >= builtAt && builtFor == Current.Game) return lines;
            builtAt = now;
            builtFor = Current.Game;
            lines.Clear();
            var memory = WeekMemory.Instance;
            if (memory == null) return lines;
            foreach (var r in memory.Followed())
            {
                try
                {
                    lines.Add(Line(r));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("following: a thing's line", e);
                }
            }
            return lines;
        }

        /// <summary>This week's Following: each thing followed, its newest change under its name; a colonist as their row of how they are.</summary>
        public static void Rows(List<WeekRow> rows)
        {
            var comp = CouncilGameComponent.Instance;
            int i = 0;
            foreach (var l in Lines())
            {
                var r = l.thing;
                var thing = r;
                var pawn = l.pawn;
                bool colonist = pawn != null && !l.gone && ThingRefs.CardOf(r) == CardKind.Colonist;
                var row = new WeekRow
                {
                    pawn = pawn,
                    faction = l.faction,
                    icon = l.icon,
                    color = l.gone ? UiKit.Faint : Color.white,
                    text = l.name,
                    sub = l.news,
                    subColor = l.newsColor,
                    tip = "CC_Week_ClickPerson".Translate(ThingRefs.Name(r)).Resolve(),
                    click = () => Cards.Open(thing),
                    follow = r,
                    pinned = true,
                    order = i++
                };
                if (colonist && comp != null)
                {
                    // Their mood on the right, its colour by the break lines; a click opens their Mood tab, as the pin did.
                    float mood = pawn.needs?.mood?.CurLevelPercentage ?? -1f;
                    if (mood >= 0f)
                    {
                        row.note = "CC_Week_MoodNote".Translate(mood.ToStringPercent()).Resolve();
                        row.noteColor = WeekModel.MoodColor(pawn, mood);
                    }
                    string summary = PeopleTable.Summary(pawn);
                    var h = Heart.ForView(pawn);
                    string brief = h != null ? HeartView.NeedsBrief(pawn, h) : null;
                    if (!brief.NullOrEmpty()) summary = (summary.NullOrEmpty() ? "" : summary + "\n\n") + brief;
                    row.tip = WeekModel.Tip(summary, "CC_Week_ClickMood".Translate(pawn.LabelShort).Resolve());
                    row.click = WeekModel.Inspect(pawn, typeof(ITab_Pawn_Mood));
                }
                rows.Add(row);
            }
        }

        private static FollowLine Line(ThingRef r)
        {
            var l = new FollowLine { thing = r, name = ThingRefs.Link(r) };
            if (!ThingRefs.Exists(r))
            {
                l.gone = true;
                l.name = ThingRefs.Name(r);
                l.news = l.plainNews = "CC_Card_Gone".Translate();
                l.newsColor = UiKit.Dim;
                l.icon = Icons.Pin;
                l.iconColor = UiKit.Dim;
                return l;
            }
            switch (ThingRefs.CardOf(r))
            {
                case CardKind.Colonist:
                    l.pawn = ThingRefs.Pawn(r);
                    l.news = Colonist(l.pawn, out l.newsColor);
                    break;
                case CardKind.Person:
                {
                    l.pawn = ThingRefs.Pawn(r);
                    var n = ThingRefs.Notable(r);
                    l.faction = n?.faction ?? WorldLedger.Person(r.ToRef())?.people;
                    l.news = News(History.Of(r.ToRef(), max: 1));
                    if (l.news == null && n != null)
                    {
                        int op = NotableBook.Opinion(n);
                        l.news = NotableBook.RegardWords(op).CapitalizeFirst();
                        l.newsColor = NotableBook.RegardColor(op);
                    }
                    if (l.pawn == null) l.icon = Icons.People;
                    break;
                }
                case CardKind.Place:
                {
                    var wo = ThingRefs.WorldObject(r);
                    l.icon = Icons.World;
                    l.iconColor = wo?.Faction != null && !wo.Faction.IsPlayer ? wo.Faction.Color : UiKit.Gold;
                    l.news = News(Newest(History.At(r.ToRef(), max: 1), History.Of(r.ToRef(), max: 1)));
                    if (l.news == null && r.kind == ThingKind.Town)
                    {
                        var f = RegionFortunes.Of(r.id);
                        if (f.HasValue) l.news = RegionFortunes.Word(f.Value, wo?.Faction).CapitalizeFirst();
                        else if (wo?.Faction != null)
                        {
                            l.news = RegionMap.StandingText(wo.Faction).CapitalizeFirst();
                            l.newsColor = RegionMap.StandingColor(wo.Faction);
                        }
                    }
                    if (l.news == null && wo != null && r.kind == ThingKind.Site) l.news = wo.def?.LabelCap.ToString();
                    break;
                }
                case CardKind.People:
                {
                    var f = ThingRefs.Faction(r);
                    l.faction = f;
                    l.news = News(History.Of(r.ToRef(), max: 1));
                    if (l.news == null && f != null)
                    {
                        l.news = RegionMap.StandingText(f).CapitalizeFirst();
                        l.newsColor = RegionMap.StandingColor(f);
                    }
                    break;
                }
                case CardKind.House:
                {
                    var h = ThingRefs.House(r);
                    l.faction = h?.people;
                    l.icon = Icons.People;
                    l.news = News(History.Of(r.ToRef(), max: 1));
                    break;
                }
                case CardKind.Tie:
                {
                    var t = ThingRefs.Tie(r);
                    l.icon = Icons.Promise ?? Icons.Trust;
                    l.iconColor = t != null && t.Live ? UiKit.Gold : UiKit.Faint;
                    if (t != null)
                    {
                        l.news = CardWords.TieState(t);
                        l.newsColor = t.Live ? UiKit.Gold : UiKit.Done;
                    }
                    break;
                }
                case CardKind.Letter:
                {
                    var letter = ThingRefs.Letter(r);
                    l.icon = Icons.Journal;
                    if (letter != null)
                    {
                        var steps = CardWords.LetterSteps(letter);
                        int at = CardWords.LetterStep(letter);
                        l.news = at >= 0 && at < steps.Length ? steps[at].CapitalizeFirst() : "CC_LettersPg_CalledOff".Translate().Resolve();
                        l.newsColor = UiKit.Info;
                    }
                    break;
                }
                case CardKind.Event:
                {
                    var e = ThingRefs.Entry(r);
                    l.icon = Icons.WorldEvent;
                    l.iconColor = UiKit.WorldColor;
                    if (e != null) l.news = CardWords.HistoryDate(e, false);
                    break;
                }
            }
            l.plainNews = l.news == null ? null : LinkTextMath.Plain(l.news);
            return l;
        }

        private static List<HistoryEntry> Newest(List<HistoryEntry> a, List<HistoryEntry> b)
        {
            var x = a != null && a.Count > 0 ? a[0] : null;
            var y = b != null && b.Count > 0 ? b[0] : null;
            var pick = x == null ? y : y == null ? x : y.tick > x.tick ? y : x;
            return pick == null ? null : new List<HistoryEntry> { pick };
        }

        /// <summary>The newest entry as news, with how long ago, when it is from the last days; else null.</summary>
        private static string News(List<HistoryEntry> found)
        {
            var e = found != null && found.Count > 0 ? found[0] : null;
            if (e == null || Find.TickManager.TicksGame - e.tick > NewsDays * GenDate.TicksPerDay) return null;
            return "CC_Follow_News".Translate(CardWords.HistoryLine(e, true).Named("LINE"), FactionMemory.AgoText(e.tick).Named("AGO")).Resolve();
        }

        /// <summary>How a colonist is, in a few words: their strain, the feeling on top, an open wish.</summary>
        private static string Colonist(Pawn p, out Color color)
        {
            color = UiKit.Faint;
            if (p == null) return null;
            var parts = new List<string>();
            var h = Heart.ForView(p);
            if (h != null && h.Band != StrainBand.Steady)
            {
                parts.Add(HeartView.BandLabel(h.Band));
                color = h.Band >= StrainBand.Worn ? UiKit.Warn : UiKit.Faint;
            }
            string feel = h != null ? HeartView.DominantText(h) : null;
            if (!feel.NullOrEmpty()) parts.Add(feel);
            var wish = CouncilGameComponent.Instance?.OpenWishes.FirstOrDefault(w => w.pawn == p);
            if (wish != null) parts.Add("CC_Week_PinnedWish".Translate(wish.Label).Resolve());
            if (p.InMentalState)
            {
                parts.Insert(0, "CC_Week_InBreak".Translate(p.MentalStateDef?.label ?? "").Resolve());
                color = UiKit.Bad;
            }
            return parts.Count > 0 ? string.Join(", ", parts).CapitalizeFirst() : "CC_Follow_Steady".Translate().Resolve();
        }
    }
}
