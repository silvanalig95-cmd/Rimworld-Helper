using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The top of a colonist's story, at a glance (docs/design/rimfolk-look.md, stage 3, point 2; the player, 9 November
    /// 2026, "story tab sounds great", to "the Story tab, done like the new Mood tab: at a glance, life goal with a progress
    /// bar, current wish, quirks, standing in the colony, closest friends and rivals; the full history below in folding
    /// sections"). Without scrolling or opening anything it answers, a row each:
    /// <list type="bullet">
    /// <item>who they are in words (their character; the Story tab's head has who they are in one line above it);</item>
    /// <item>their life goal with a bar for how far along, its progress in words (what's next) and how long they've held it;</item>
    /// <item>the story they're living, with a dot for each stage;</item>
    /// <item>their wish before the council: what it is, how long they've waited, what fulfils it, with "Show me" and
    /// "Promise" (the wish cards' own acts);</item>
    /// <item>their quirks, keepsakes, vendettas and nemeses as tags, what each does in its tooltip;</item>
    /// <item>their standing: trust in the council on a meter with its bands, the office they hold, their name as a doctor or
    /// how they're mending, and how many of the colony think well or ill of them;</item>
    /// <item>their closest friends and their rivals, two or three each, with small portraits and their opinion, a click
    /// selecting them (in the Story tab) or opening their card (elsewhere);</item>
    /// <item>the newest line of their history.</item>
    /// </list>
    /// Built with the story's model (StoryView, twice a second at most) from the same sources the sections below draw;
    /// drawing only lays it out in the kit, in the game's look or Rimfolk's own. The rules are StoryGlanceMath's.
    /// </summary>
    public sealed class StoryGlance
    {
        /// <summary>A friend or a rival: their portrait, name and opinion, and what the tooltip says of the two.</summary>
        public sealed class Person
        {
            public Pawn pawn;
            public string name, opinion, tip;
            public Color color;
            /// <summary>The colonist's opinion of them, and theirs of the colonist.</summary>
            public float of, back;
            /// <summary>Their chip's width in the glance (portrait, name, opinion).</summary>
            public float width;
            /// <summary>What they are to the colonist ("Husband, friends"), and their opinion back ("theirs +30"), for the section's rows.</summary>
            public string what, backWords;
        }

        public Pawn pawn;
        /// <summary>In the Story tab a friend's portrait selects them on the map, as the game's Social tab does; in a card it opens their card.</summary>
        public bool select;

        public string words, wordsTip;

        /// <summary>The goal ("Wants to master cooking"), its progress in words, how long they've held it, why they set their heart on it, the tooltip.</summary>
        public string goal, goalWords, goalHeld, goalWhy, goalTip, noGoal;
        public float goalFill;

        public string story, storyTip;
        public int stage = -1, stages;

        public Wish wish;
        public string wishText, wishWhen, wishWaiting, wishTip, noWish, promiseWhyNot;
        public Color wishColor;

        public readonly List<UiKit.PillSpec> tags = new List<UiKit.PillSpec>();

        public bool hasTrust;
        public float trust;
        public string trustText, trustTip;
        public Color trustColor;
        public readonly List<UiKit.PillSpec> standing = new List<UiKit.PillSpec>();

        /// <summary>Every friend and every rival, closest and worst first (the glance shows the first few, the section all).</summary>
        public readonly List<Person> friends = new List<Person>(), rivals = new List<Person>();
        public string friendsLabel, rivalsLabel, nobody;

        public string recent, recentAgo;

        public bool Empty => words == null && goal == null && noGoal == null && story == null && wishText == null && noWish == null && tags.Count == 0 && !hasTrust
                             && standing.Count == 0 && friends.Count == 0 && rivals.Count == 0 && nobody == null && recent == null;

        private const float Gap = 6f, LineH = 20f, ChipH = 24f, Face = 22f;

        // ---------------- Building it ----------------

        /// <summary>Their life goal: what it is, how far along with its progress in words, how long they've held it and why.</summary>
        public void BuildGoal(CouncilGameComponent comp, Pawn p)
        {
            if (!ColonyCouncilMod.Settings.aspirations) return;
            var a = comp.aspirations.Active(p);
            if (a == null)
            {
                if (p.IsFreeNonSlaveColonist && !p.DevelopmentalStage.Baby()) noGoal = "CC_SGl_NoGoal".Translate(p.Named("PAWN")).Resolve();
                return;
            }
            string progress;
            try
            {
                progress = a.def.Worker.ProgressText(a);
            }
            catch (Exception)
            {
                progress = null;
            }
            string pct = a.Progress.ToStringPercent();
            goal = "CC_Story_Wants".Translate(a.Goal).Resolve();
            goalFill = a.Progress;
            goalWords = progress.NullOrEmpty() || progress == pct ? pct : progress + "  ·  " + pct;
            int held = Find.TickManager.TicksGame - a.startedTick;
            if (held >= GenDate.TicksPerHour) goalHeld = "CC_SGl_Held".Translate(UiKit.Duration(held).Named("TIME")).Resolve();
            string why = a.SparkText;
            string day = "CC_DayN".Translate(a.startedTick / GenDate.TicksPerDay + 1);
            goalWhy = why.NullOrEmpty() ? "CC_SGl_GoalSince".Translate(day.Named("DAY")).Resolve() : "CC_Story_GoalWhy".Translate(why, day).Resolve();
            goalTip = "CC_Story_Goal".Translate(a.Goal, progress.NullOrEmpty() ? pct : progress).Resolve() + "\n\n" + goalWhy;
        }

        /// <summary>
        /// Their wish before the council: their own first (the one nearest its last call, then the oldest), else one asked
        /// for them. What it is, how long they've waited, what fulfils it; its acts are the wish cards' own.
        /// </summary>
        public void BuildWish(CouncilGameComponent comp, Pawn p)
        {
            if (!ColonyCouncilMod.Settings.enableWishes) return;
            Wish best = null;
            int open = 0;
            foreach (var w in comp.wishes)
            {
                if (w == null || w.state != WishState.Open || w.def == null || w.pawn == null || (w.pawn != p && w.Beneficiary != p)) continue;
                open++;
                if (best == null || Before(w, best, p)) best = w;
            }
            if (best == null)
            {
                if (p.IsFreeNonSlaveColonist && !p.DevelopmentalStage.Baby()) noWish = "CC_SGl_NoWish".Translate(p.Named("PAWN")).Resolve();
                return;
            }
            var wsh = best;
            wish = wsh;
            wishText = wsh.pawn == p
                ? "CC_SGl_Wishes".Translate(wsh.LabelInline.Named("WISH")).Resolve()
                : "CC_SGl_WishedFor".Translate(wsh.pawn.LabelShort.Named("ASKER"), wsh.LabelInline.Named("WISH")).Resolve();
            if (open > 1) wishText += "  " + "CC_SGl_MoreWishes".Translate((open - 1).Named("COUNT")).Resolve();
            wishColor = wsh.stage >= 2 ? UiKit.Sour : wsh.promised ? UiKit.Gold : UiKit.Cool;
            var waited = new List<string>(3) { "CC_SGl_Waiting".Translate(UiKit.Duration(Find.TickManager.TicksGame - wsh.createdTick).Named("TIME")).Resolve() };
            if (wsh.promised) waited.Add("CC_PromisedTag".Translate());
            if (wsh.stage >= 2) waited.Add("CC_LastCallTag".Translate());
            wishWaiting = string.Join("  ·  ", waited);
            string when = Safe(() => wsh.Worker.FulfilledWhen(wsh));
            string progress = Safe(() => wsh.Worker.ProgressText(wsh));
            if (!when.NullOrEmpty()) wishWhen = "CC_FulfilledWhen".Translate() + ": " + when;
            else if (!progress.NullOrEmpty()) wishWhen = "CC_Progress".Translate() + ": " + progress;
            // The whole of it in the tooltip: the row's words are cut where the width is short.
            var tip = new List<string> { wishText.Colorize(ColoredText.TipSectionTitleColor) };
            if (!wsh.quote.NullOrEmpty()) tip.Add((wsh.scene.NullOrEmpty() ? "" : wsh.scene + "\n") + "\"" + wsh.quote + "\"");
            var lines = new List<string>();
            if (!wsh.reason.NullOrEmpty()) lines.Add("CC_Why".Translate() + ": " + wsh.reason);
            if (!when.NullOrEmpty()) lines.Add("CC_FulfilledWhen".Translate() + ": " + when);
            if (!progress.NullOrEmpty()) lines.Add("CC_Progress".Translate() + ": " + progress);
            string reward = Safe(() => comp.RewardText(wsh));
            if (!reward.NullOrEmpty()) lines.Add("CC_Reward".Translate() + ": " + reward);
            if (comp.IsPetition(wsh)) lines.Add("CC_Petition_PillTip".Translate(comp.PetitionNames(wsh).Named("NAMES")).Resolve());
            if (lines.Count > 0) tip.Add(string.Join("\n", lines));
            wishTip = string.Join("\n\n", tip);
            promiseWhyNot = wsh.promised ? "CC_SGl_Promised".Translate().Resolve() : null;
        }

        /// <summary>The one to show of two: their own before one asked for them, then the one nearer its last call, then the older.</summary>
        private static bool Before(Wish a, Wish b, Pawn p)
        {
            bool ownA = a.pawn == p, ownB = b.pawn == p;
            if (ownA != ownB) return ownA;
            if (a.stage != b.stage) return a.stage > b.stage;
            return a.createdTick < b.createdTick;
        }

        /// <summary>Their standing: trust in the council on its meter, the office they hold, their health's tag, and how the colony regards them.</summary>
        public void BuildStanding(CouncilGameComponent comp, Pawn p, int liked, int disliked, string likedBy, string dislikedBy)
        {
            var st = p.IsColonist ? comp.standings.For(p, false) : null;
            if (st != null)
            {
                hasTrust = true;
                trust = st.trust;
                trustColor = UiKit.TrustColor(st.trust);
                string level = ("CC_Trust_" + PawnStanding.LevelFor(st.trust)).Translate();
                trustText = "CC_SGl_Trust".Translate(level.Named("LEVEL"), st.trust.ToString("0").Named("VALUE")).Resolve();
                trustTip = "CC_Trust_Header".Translate().Resolve().Colorize(ColoredText.TipSectionTitleColor) + "\n" + trustText + "\n\n"
                           + "CC_Trust_Tip".Translate(p.LabelShort, st.trust.ToString("0"), st.granted, st.ignored, st.refused, st.promisesKept, st.promisesBroken).Resolve();
                string reasons = st.RecentReasons(4);
                if (!reasons.NullOrEmpty()) trustTip += "\n\n" + reasons;
            }
            var office = OfficeTracker.HeldBy(p);
            if (office != null)
                standing.Add(new UiKit.PillSpec(office.NameCap, UiKit.Gold, Icons.Council,
                    "CC_Office_Holds".Translate(p.LabelShort, office.Name).Resolve() + (office.description.NullOrEmpty() ? "" : "\n\n" + office.Description)));
            string health = HealthTracker.StoryTag(p, out var healthColor);
            if (health != null) standing.Add(new UiKit.PillSpec(health.CapitalizeFirst(), healthColor, Icons.CatHealth, string.Join("\n", HealthTracker.StoryLines(p))));
            if (liked > 0)
                standing.Add(new UiKit.PillSpec("CC_SGl_LikedBy".Translate(liked.Named("COUNT")).Resolve(), UiKit.Warm, null,
                    "CC_SGl_LikedByTip".Translate(p.Named("PAWN"), likedBy.Named("NAMES")).Resolve()));
            if (disliked > 0)
                standing.Add(new UiKit.PillSpec("CC_SGl_DislikedBy".Translate(disliked.Named("COUNT")).Resolve(), UiKit.Sour, null,
                    "CC_SGl_DislikedByTip".Translate(p.Named("PAWN"), dislikedBy.Named("NAMES")).Resolve()));
        }

        /// <summary>
        /// Their friends and rivals among the colony's people (free colonists and slaves, not babies or lodgers), by the
        /// game's opinion and Rimfolk's bonds (StoryGlanceMath), with what each is to them; and how many of them think well
        /// or ill of the colonist, for their standing.
        /// </summary>
        public void BuildPeople(CouncilGameComponent comp, Pawn p)
        {
            int liked = 0, disliked = 0;
            var likedNames = new List<string>();
            var dislikedNames = new List<string>();
            var others = new List<StoryGlanceMath.Other>();
            var pawns = new List<Pawn>();
            var backs = new List<float>();
            if (p.relations != null)
            {
                foreach (var o in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists)
                {
                    if (o == null || o == p || o.Dead || o.relations == null || !o.RaceProps.Humanlike || o.DevelopmentalStage.Baby() || o.IsQuestLodger()) continue;
                    float of, back;
                    try
                    {
                        of = p.relations.OpinionOf(o);
                        back = o.relations.OpinionOf(p);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("the story at a glance: an opinion", e);
                        continue;
                    }
                    if (back >= StoryGlanceMath.FriendAt)
                    {
                        liked++;
                        likedNames.Add(o.LabelShort + " " + Signed(back));
                    }
                    else if (back <= StoryGlanceMath.RivalAt)
                    {
                        disliked++;
                        dislikedNames.Add(o.LabelShort + " " + Signed(back));
                    }
                    var friendTie = Tie(comp, p, o, TieKind.Friends) ?? Tie(comp, p, o, TieKind.Comrades) ?? Tie(comp, p, o, TieKind.Mentor);
                    var rivalTie = Tie(comp, p, o, TieKind.Rivals);
                    others.Add(new StoryGlanceMath.Other(o.thingIDNumber, of, friendTie != null, rivalTie != null, Mathf.Max(friendTie?.strength ?? 0f, rivalTie?.strength ?? 0f)));
                    pawns.Add(o);
                    backs.Add(back);
                }
            }
            foreach (int i in StoryGlanceMath.Friends(others)) friends.Add(PersonOf(comp, p, pawns[i], others[i].opinion, backs[i], UiKit.Warm));
            foreach (int i in StoryGlanceMath.Rivals(others)) rivals.Add(PersonOf(comp, p, pawns[i], others[i].opinion, backs[i], UiKit.Sour));
            friendsLabel = "CC_SGl_Friends".Translate();
            rivalsLabel = "CC_SGl_Rivals".Translate();
            if (friends.Count == 0 && rivals.Count == 0 && pawns.Count > 0) nobody = "CC_SGl_Nobody".Translate(p.Named("PAWN")).Resolve();
            BuildStanding(comp, p, liked, disliked, TextUtil.JoinAnd(likedNames), TextUtil.JoinAnd(dislikedNames));
        }

        /// <summary>An announced bond of this kind between the two, or null (with the social web off, none).</summary>
        private static SocialTie Tie(CouncilGameComponent comp, Pawn p, Pawn o, TieKind kind)
        {
            if (!ColonyCouncilMod.Settings.socialWeb || comp.social == null) return null;
            var t = comp.social.Get(p, o, kind);
            return t != null && t.announced ? t : null;
        }

        private static Person PersonOf(CouncilGameComponent comp, Pawn p, Pawn o, float of, float back, Color color)
        {
            var person = new Person { pawn = o, name = o.LabelShort, opinion = Signed(of), color = color, of = of, back = back };
            var tip = new StringBuilder();
            tip.Append(o.LabelShortCap.Colorize(ColoredText.TipSectionTitleColor)).Append("\n");
            tip.Append("CC_SGl_Opinion".Translate(p.Named("PAWN"), o.Named("OTHER"), person.opinion.Named("OPINION"), Signed(back).Named("BACK")).Resolve());
            var whats = new List<string>(2);
            try
            {
                string relation = p.GetMostImportantRelation(o)?.GetGenderSpecificLabel(o);
                if (!relation.NullOrEmpty())
                {
                    whats.Add(relation);
                    tip.Append("\n").Append("CC_SGl_Relation".Translate(p.Named("PAWN"), o.Named("OTHER"), relation.Named("RELATION")).Resolve());
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the story at a glance: a relation", e);
            }
            foreach (var kind in new[] { TieKind.Friends, TieKind.Comrades, TieKind.Mentor, TieKind.Rivals })
            {
                var t = Tie(comp, p, o, kind);
                if (t != null) whats.Add(t.Label);
            }
            person.what = whats.Count == 0 ? null : string.Join(", ", whats).CapitalizeFirst();
            person.backWords = "CC_SGl_Theirs".Translate(Signed(back).Named("BACK")).Resolve();
            person.tip = tip.ToString();
            person.width = Face + 4f + Mathf.Min(UiKit.KeptWidth(person.name, GameFont.Small), 72f) + 4f + UiKit.KeptWidth(person.opinion, GameFont.Tiny) + 8f;
            return person;
        }

        /// <summary>The newest line of their history (a season's summary aside), with how long ago; names in it are links.</summary>
        public void BuildRecent(Pawn p)
        {
            try
            {
                foreach (var e in History.Of(Refs.Of(p), null, int.MinValue, 8))
                {
                    if (e == null || e.folded) continue;
                    string line = CardWords.HistoryLine(e, true);
                    if (line.NullOrEmpty()) continue;
                    recent = line;
                    recentAgo = MainTabWindow_Council.AgoString(e.tick).CapitalizeFirst();
                    return;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the story at a glance: the newest line", e);
            }
        }

        private static string Signed(float v) => Mathf.RoundToInt(v).ToString("+0;-0;0");

        private static string Safe(Func<string> f)
        {
            try
            {
                return f();
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ---------------- Drawing ----------------

        /// <summary>The glance from (x, y) across <paramref name="w"/>; returns the height used.</summary>
        public float Draw(float x, float y, float w)
        {
            float top = y;
            try
            {
                if (words != null)
                {
                    float h = UiKit.Paragraph(x, y, w, words, UiKit.Quote);
                    if (!wordsTip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(x, y, w, h), wordsTip);
                    y += h + Gap;
                }
                if (goal != null) y += GoalRow(x, y, w) + Gap;
                else if (noGoal != null) y += Quiet(x, y, w, Icons.LifeGoal, noGoal) + Gap;
                if (story != null)
                {
                    StoryRow(new Rect(x, y, w, 22f));
                    y += 22f + Gap;
                }
                if (wishText != null) y += WishRow(x, y, w) + Gap;
                else if (noWish != null) y += Quiet(x, y, w, Icons.Wishes, noWish) + Gap;
                if (tags.Count > 0) y += UiKit.Pills(x, y, w, tags) + Gap;
                if (hasTrust || standing.Count > 0) y += StandingRow(x, y, w) + Gap;
                float people = PeopleRows(x, y, w);
                if (people > 0f) y += people + Gap;
                if (recent != null)
                {
                    RecentRow(new Rect(x, y, w, LineH));
                    y += LineH + Gap;
                }
            }
            finally
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
            return y - top;
        }

        /// <summary>
        /// One line cut to its width with no tooltip of its own: for words in a row whose tooltip already says all of it (a
        /// friend's name, the wish, the goal), so one tooltip shows at a time.
        /// </summary>
        internal static void Plain(Rect r, string text, Color color, GameFont font = GameFont.Small, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (text.NullOrEmpty() || r.width <= 4f) return;
            var old = Text.Font;
            Text.Font = font;
            Text.Anchor = anchor;
            GUI.color = color;
            Widgets.Label(r, UiKit.Fit(text, r.width));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = old;
        }

        /// <summary>A quiet line with its icon ("No life goal yet.").</summary>
        private static float Quiet(float x, float y, float w, Texture2D icon, string text)
        {
            UiKit.Icon(new Rect(x, y + 2f, 16f, 16f), icon, UiKit.Dim);
            UiKit.LabelFit(new Rect(x + StoryGlanceMath.IconW, y, Mathf.Max(0f, w - StoryGlanceMath.IconW), LineH), text, UiKit.Faint);
            return LineH;
        }

        /// <summary>"Wants to master cooking" with how long they've held it, then the bar and its progress in words ("cooking 12 of 15  ·  80%").</summary>
        private float GoalRow(float x, float y, float w)
        {
            const float h = 38f;
            var r = new Rect(x, y, w, h);
            UiKit.RowHover(r);
            UiKit.Icon(new Rect(x, y + 2f, 16f, 16f), Icons.LifeGoal, UiKit.Gold);
            float tx = x + StoryGlanceMath.IconW;
            float heldW = goalHeld == null ? 0f : Mathf.Min(UiKit.KeptWidth(goalHeld, GameFont.Tiny) + 4f, w * 0.35f);
            Plain(new Rect(tx, y, Mathf.Max(0f, x + w - tx - heldW - 4f), LineH), goal, UiKit.Ink);
            if (heldW > 0f) UiKit.LabelFit(new Rect(x + w - heldW, y, heldW, LineH), goalHeld, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
            var (bar, wordsW) = StoryGlanceMath.GoalLine(w, goalWords == null ? 0f : UiKit.KeptWidth(goalWords, GameFont.Tiny) + 4f);
            UiKit.Bar(new Rect(tx, y + LineH + 6f, bar, 7f), goalFill, UiKit.Gold);
            if (wordsW > 0f) UiKit.LabelFit(new Rect(x + w - wordsW, y + LineH, wordsW, 18f), goalWords, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleRight);
            if (!goalTip.NullOrEmpty()) TooltipHandler.TipRegion(r, goalTip);
            return h;
        }

        /// <summary>"The long road: second thoughts", with a dot for each stage: those behind them, the one they're at, those to come.</summary>
        private void StoryRow(Rect r)
        {
            UiKit.RowHover(r);
            UiKit.Icon(new Rect(r.x, r.center.y - 8f, 16f, 16f), Icons.Journal, UiKit.Info);
            const float dot = 7f, dotGap = 4f;
            int n = stage >= 0 ? Mathf.Clamp(stages, 1, 10) : 0;
            float dotsW = n > 0 ? n * dot + (n - 1) * dotGap : 0f;
            float tx = r.x + StoryGlanceMath.IconW;
            Plain(new Rect(tx, r.y, Mathf.Max(0f, r.xMax - tx - dotsW - 8f), r.height), story, UiKit.Ink);
            for (int i = 0; i < n; i++)
            {
                var d = new Rect(r.xMax - dotsW + i * (dot + dotGap), r.center.y - dot / 2f, dot, dot);
                UiKit.DiscFill(d, i < stage ? UiKit.Done : i == stage ? UiKit.Gold : UiKit.WithAlpha(UiKit.Ink, 0.15f));
            }
            if (!storyTip.NullOrEmpty()) TooltipHandler.TipRegion(r, storyTip);
        }

        /// <summary>
        /// "Wishes for a bed of their own", its acts beside it (or under it where the width is short), then what fulfils it
        /// and how long they've waited.
        /// </summary>
        private float WishRow(float x, float y, float w)
        {
            string show = ShowWord, promise = PromiseWord;
            float showW = UiKit.RowActWidth(show), promiseW = UiKit.RowActWidth(promise);
            float actsW = showW + 4f + promiseW;
            var (under, textW) = StoryGlanceMath.WishLine(w, actsW);
            float h = LineH + (wishWhen != null || wishWaiting != null ? 18f : 0f) + (under ? LineH + 2f : 0f);
            var r = new Rect(x, y, w, h);
            UiKit.RowHover(r);
            UiKit.Icon(new Rect(x, y + 2f, 16f, 16f), Icons.Wishes, wishColor);
            float tx = x + StoryGlanceMath.IconW;
            var textRect = new Rect(tx, y, textW, LineH);
            Plain(textRect, wishText, UiKit.Ink);
            float sy = y + LineH;
            if (wishWhen != null || wishWaiting != null)
            {
                float waitW = wishWaiting == null ? 0f : Mathf.Min(UiKit.KeptWidth(wishWaiting, GameFont.Tiny) + 4f, w * 0.45f);
                if (wishWhen != null) Plain(new Rect(tx, sy, Mathf.Max(0f, x + w - tx - waitW - 4f), 18f), wishWhen, UiKit.Faint, GameFont.Tiny);
                if (waitW > 0f) UiKit.LabelFit(new Rect(x + w - waitW, sy, waitW, 18f), wishWaiting, wishColor == UiKit.Cool ? UiKit.Faint : wishColor, GameFont.Tiny, TextAnchor.MiddleRight);
                sy += 18f;
            }
            if (!wishTip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(x, y, under ? w : StoryGlanceMath.IconW + textW, sy - y), wishTip);
            // Its acts, the wish cards' own: look at it on the map, and promise it.
            float ay = under ? sy + 2f : y;
            float ax = x + w - actsW;
            var w0 = wish;
            if (w0 != null && UiKit.RowAct(new Rect(ax, ay, showW, LineH), show, null, ShowTip))
            {
                try
                {
                    MainTabWindow_Council.Jump(w0.Worker.LookAt(w0));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("the story at a glance: show a wish", e);
                }
            }
            if (w0 != null && UiKit.RowAct(new Rect(ax + showW + 4f, ay, promiseW, LineH), promise, promiseWhyNot, PromiseTip))
            {
                CouncilGameComponent.Instance?.Promise(w0);
                StoryView.Stale();
            }
            return h;
        }

        /// <summary>Trust in the council on its meter with its bands, then the office, health and regard as tags.</summary>
        private float StandingRow(float x, float y, float w)
        {
            float top = y;
            if (hasTrust)
            {
                var r = new Rect(x, y, w, LineH);
                UiKit.RowHover(r);
                UiKit.Icon(new Rect(x, y + 2f, 16f, 16f), Icons.Trust, trustColor);
                var (wordsW, barW) = StoryGlanceMath.TrustLine(w, UiKit.KeptWidth(trustText, GameFont.Small) + 4f);
                float tx = x + StoryGlanceMath.IconW;
                Plain(new Rect(tx, y, wordsW, LineH), trustText, trustColor);
                if (barW >= 8f)
                {
                    var bar = new Rect(tx + wordsW + 8f, y + 6f, barW, 8f);
                    UiKit.Bar(bar, StoryGlanceMath.TrustFill(trust), trustColor);
                    // Where the words change: bitter, doubtful, neutral, trusting, devoted.
                    foreach (float t in StoryGlanceMath.TrustTicks)
                    {
                        float at = bar.x + 1f + (bar.width - 2f) * StoryGlanceMath.TrustFill(t);
                        Widgets.DrawBoxSolid(new Rect(at - 0.5f, bar.y - 2f, 1f, bar.height + 4f), UiKit.WithAlpha(UiKit.Ink, 0.35f));
                    }
                }
                if (!trustTip.NullOrEmpty()) TooltipHandler.TipRegion(r, trustTip);
                y += LineH + 2f;
            }
            if (standing.Count > 0) y += UiKit.Pills(x + StoryGlanceMath.IconW, y, Mathf.Max(40f, w - StoryGlanceMath.IconW), standing);
            return y - top;
        }

        /// <summary>The closest friends and the rivals, a row each: small portraits with names and opinions, the rest counted.</summary>
        private float PeopleRows(float x, float y, float w)
        {
            if (friends.Count == 0 && rivals.Count == 0)
                return nobody == null ? 0f : Quiet(x, y, w, Icons.Bonds, nobody);
            float top = y;
            if (friends.Count > 0)
            {
                PeopleRow(x, y, w, friendsLabel, friends, UiKit.Warm);
                y += ChipH + 2f;
            }
            if (rivals.Count > 0)
            {
                PeopleRow(x, y, w, rivalsLabel, rivals, UiKit.Sour);
                y += ChipH + 2f;
            }
            return y - top - 2f;
        }

        private static readonly List<float> widths = new List<float>(8);

        private void PeopleRow(float x, float y, float w, string label, List<Person> list, Color color)
        {
            UiKit.Icon(new Rect(x, y + 4f, 16f, 16f), Icons.Bonds, color);
            UiKit.LabelFit(new Rect(x + StoryGlanceMath.IconW, y, StoryGlanceMath.LabelW - 4f, ChipH), label, UiKit.Faint, GameFont.Tiny);
            float cx = x + StoryGlanceMath.IconW + StoryGlanceMath.LabelW;
            float room = x + w - cx;
            widths.Clear();
            for (int i = 0; i < list.Count && i <= StoryGlanceMath.Shown; i++) widths.Add(list[i].width);
            int fit = StoryGlanceMath.Fits(room, widths, 6f, UiKit.KeptWidth(MoreWord(9), GameFont.Tiny) + 8f);
            if (fit == 0 && list.Count > 0 && widths[0] <= room) fit = 1;
            for (int i = 0; i < fit; i++)
            {
                Chip(new Rect(cx, y, Mathf.Min(list[i].width, x + w - cx), ChipH), list[i]);
                cx += list[i].width + 6f;
            }
            int rest = list.Count - fit;
            if (rest <= 0) return;
            string restWord = MoreWord(rest);
            var mr = new Rect(cx, y, Mathf.Max(0f, Mathf.Min(UiKit.KeptWidth(restWord, GameFont.Tiny) + 8f, x + w - cx)), ChipH);
            UiKit.RowHover(mr);
            UiKit.LabelFit(mr, restWord, UiKit.Faint, GameFont.Tiny, TextAnchor.MiddleCenter);
            var shown = list;
            int from = fit;
            TooltipHandler.TipRegion(mr, () => string.Join("\n", shown.Skip(from).Select(o => o.name + " " + o.opinion)), (pawn?.thingIDNumber ?? 0) ^ (color == UiKit.Warm ? 0x5F0E : 0x5F0F));
        }

        /// <summary>One friend or rival: portrait, name, opinion; a click selects them (the Story tab) or opens their card.</summary>
        private void Chip(Rect r, Person o)
        {
            if (r.width < Face) return;
            UiKit.RowHover(r);
            UiKit.Portrait(new Rect(r.x + 1f, r.y + 1f, Face, Face), o.pawn, UiKit.WithAlpha(o.color, 0.8f));
            float nx = r.x + Face + 5f;
            float opW = UiKit.KeptWidth(o.opinion, GameFont.Tiny) + 2f;
            Plain(new Rect(nx, r.y, Mathf.Max(0f, r.xMax - nx - opW - 4f), r.height), o.name, UiKit.Ink);
            Plain(new Rect(r.xMax - opW - 4f, r.y, opW, r.height), o.opinion, o.color, GameFont.Tiny);
            Clickable(r, pawn, o, select);
        }

        /// <summary>A friend's or rival's tooltip (with Rimfolk's line about the two, worked out only on hover) and click, here and in the section below.</summary>
        public static void Clickable(Rect r, Pawn subject, Person o, bool select)
        {
            var who = o;
            TooltipHandler.TipRegion(r, () =>
            {
                string note = subject != null ? SocialColumn.PairNote(subject, who.pawn) : null;
                return who.tip + (note.NullOrEmpty() ? "" : "\n\n" + note) + "\n\n" + (select ? SelectWord : OpenWord);
            }, who.pawn.thingIDNumber ^ 0x57A1);
            if (!Widgets.ButtonInvisible(r, false)) return;
            Cards.Click();
            if (select && (who.pawn.Spawned || who.pawn.GetCaravan() != null)) CameraJumper.TryJumpAndSelect(who.pawn);
            else Cards.Open(who.pawn);
        }

        /// <summary>The newest line of their history: when, then the line with its names as links (cut, the whole of it in a tooltip).</summary>
        private void RecentRow(Rect r)
        {
            UiKit.Icon(new Rect(r.x + 1f, r.y + 3f, 14f, 14f), Icons.Chronicle, UiKit.Faint);
            float tx = r.x + StoryGlanceMath.IconW;
            float agoW = recentAgo == null ? 0f : UiKit.KeptWidth(recentAgo, GameFont.Tiny) + 8f;
            if (agoW > 0f) UiKit.LabelFit(new Rect(tx, r.y, agoW, r.height), recentAgo, UiKit.Faint, GameFont.Tiny);
            LinkText.Line(new Rect(tx + agoW, r.y, Mathf.Max(0f, r.xMax - tx - agoW), r.height), recent, UiKit.Ink);
        }

        // ---------------- Words, translated once ----------------

        private static string showWord, showTip, promiseWord, promiseTip, selectWord, openWord;
        private static readonly Dictionary<int, string> moreWords = new Dictionary<int, string>();

        private static string ShowWord => showWord ??= "CC_Show".Translate();
        private static string ShowTip => showTip ??= "CC_SGl_ShowTip".Translate();
        private static string PromiseWord => promiseWord ??= "CC_Promise".Translate();
        private static string PromiseTip => promiseTip ??= "CC_Promise_Tip".Translate();
        private static string SelectWord => selectWord ??= "CC_SGl_ClickSelect".Translate();
        private static string OpenWord => openWord ??= "CC_UI_OpenPage".Translate();

        private static string MoreWord(int n)
        {
            if (!moreWords.TryGetValue(n, out var s)) moreWords[n] = s = "CC_SGl_More".Translate(n.Named("COUNT")).Resolve();
            return s;
        }
    }
}
