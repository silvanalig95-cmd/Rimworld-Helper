using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Story" in a colonist's inspect pane: who they are and what they've lived. Under their name, who they are in one
    /// line (the name the colony knows them by, their office or the game's title for them, their age, where they come
    /// from; each in full in its tooltip). Then their story at a glance (StoryGlance: their character in words, life goal,
    /// the story they're living, their wish, quirks, standing, closest friends and rivals, the newest line of their
    /// history), and under it the whole of it in sections that fold and are remembered between sessions, in order of use:
    /// their story (the history), wishes, life goals, quirks, friends and rivals, family, dreams, then their character and
    /// the rest (docs/design/rimfolk-look.md, stage 3, point 2). Their social life's groups, what people say and shared
    /// memories are in Rimfolk's column of the game's Social tab (UI/SocialColumn.cs), and their faith and views in the
    /// Culture tab (UI/ITab_Pawn_Culture.cs); with the column switched off, those social parts stay here. Fits the room
    /// above the inspect pane (RimfolkTab); the name stays put, the rest scrolls.
    /// </summary>
    public class ITab_Pawn_Story : RimfolkTab
    {
        private Vector2 scroll;
        private float height = 400f;
        private Pawn shown;
        /// <summary>Which of the tab's sections are folded away, remembered between sessions; all but the story and the wishes start folded.</summary>
        private static readonly Folds folds = Folds.Remembered("story.sections", StoryView.FoldedInTab);

        public ITab_Pawn_Story()
        {
            size = new Vector2(460f, 520f);
            labelKey = "CC_Tab_Story";
        }

        protected override float PreferredHeight => 520f;

        public override bool IsVisible
        {
            get
            {
                var p = SelPawn;
                return p != null && p.RaceProps.Humanlike && (p.IsColonist || p.IsPrisonerOfColony || p.IsSlaveOfColony) && CouncilGameComponent.Instance != null;
            }
        }

        private static string councilWord;
        private static string CouncilWord => councilWord ??= "CC_Story_OpenCouncil".Translate();

        protected override void FillTab()
        {
            var p = SelPawn;
            if (p == null || CouncilGameComponent.Instance == null) return;
            if (shown != p)
            {
                scroll = Vector2.zero;
                shown = p;
            }
            var rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            var comp = CouncilGameComponent.Instance;
            const float face = 40f;
            UiKit.Portrait(new Rect(rect.x, rect.y, face, face), p, p.IsColonist ? UiKit.TrustColor(comp.standings.TrustOf(p)) : UiKit.Neutral);
            // The name, and under it who they are in one line; the button keeps clear of the tab's close button in the top right corner.
            float bw = p.IsColonist ? Mathf.Min(124f, UiKit.KeptWidth(CouncilWord, GameFont.Small) + 24f) : 0f;
            float tx = rect.x + face + 8f, tw = Mathf.Max(0f, rect.width - face - 8f - (bw > 0f ? bw + 20f : 20f));
            var who = UiCache.Get(p, 41, () => StoryView.Who(comp, p), 2f);
            if (who == null || who.line.NullOrEmpty())
                UiKit.LabelFit(new Rect(tx, rect.y + 4f, tw, 32f), p.LabelShortCap, Color.white, GameFont.Medium);
            else
            {
                UiKit.LabelFit(new Rect(tx, rect.y - 3f, tw, 28f), p.LabelShortCap, Color.white, GameFont.Medium);
                var line = new Rect(tx, rect.y + 23f, tw, 18f);
                UiKit.LabelFit(line, who.line, UiKit.HeaderColor, GameFont.Tiny);
                if (!who.tip.NullOrEmpty()) TooltipHandler.TipRegion(line, who.tip);
            }
            if (bw > 0f && Widgets.ButtonText(new Rect(rect.xMax - bw - 14f, rect.y + 7f, bw, 26f), CouncilWord))
                MainTabWindow_Council.OpenPerson(p);
            StoryView.Draw(new Rect(rect.x, rect.y + face + 8f, rect.width, rect.height - face - 8f), p, ref scroll, ref height, folds, StoryParts.StoryTab);
        }
    }

    /// <summary>Which parts of a colonist's story a page shows.</summary>
    public enum StoryParts
    {
        /// <summary>Everything (the council window's People roster).</summary>
        All,
        /// <summary>The Story tab: without what the Culture tab shows, and without what the Social tab's column shows while it's on.</summary>
        StoryTab,
        /// <summary>Everything, as in the roster, for a colonist's card (UI/Cards/), with a model of its own so both can show at once.</summary>
        Card
    }

    /// <summary>
    /// The story of one colonist, drawn in the Story tab and a colonist's card. First their story at a glance
    /// (<see cref="StoryGlance"/>); then the sections, in order of use: their story (the newest lines of their history,
    /// with the way to the whole of it, and the stories they lived through), their wishes (before the council now, and
    /// what came of the earlier ones), their life goals (the one under way in full, those reached and the knacks they
    /// won't lose), their quirks in full (with keepsakes, vendettas and nemeses), their friends and rivals (every one,
    /// with Rimfolk's bonds and the bonds lost while the Social tab's column is off), their family tree, their dreams, then
    /// their character page (the bars, what shaped them, the traits life changed, and the social parts while the column is
    /// off), the years in cryptosleep, secrets (while the column is off) and the newest lines of their journal. Each place
    /// passes its own folds, so a section folded in the tab stays open in a card, and its own parts: the Story tab leaves
    /// out what the Social tab's column and the Culture tab show. A colonist, a relative, a nemesis or a people named in it
    /// is a link with its preview (PaneLinks, UiKit's chips and pills): in the tab it opens the floating card, in a card or
    /// a window the drawer; a friend's or a rival's portrait selects them in the tab.
    /// </summary>
    public static class StoryView
    {
        private static Color Faint => UiKit.Faint;
        private static Color HeaderColor => UiKit.Heading;

        /// <summary>The character page's sections the Culture tab shows (its views and faith).</summary>
        public static readonly string[] CultureKeys = { "talk.views", "faith" };

        /// <summary>
        /// The Story tab's sections that start folded: all but their story and their wishes (the glance has the gist of
        /// the rest). The player's own folds are remembered from then on.
        /// </summary>
        public static readonly string[] FoldedInTab =
        {
            "life", "quirks", "people", "family", "dreams", "character10", "character20", "character25", "character26", "character40", "character50", "character60",
            "years", "secrets", "journal"
        };

        /// <summary>How many lines of their history, and of what came of their earlier wishes, the sections show.</summary>
        private const int HistoryShown = 10, WishesShown = 8;

        /// <summary>The history's kinds for what came of a wish: granted, lapsed or taken back, refused or set aside, and a petition's turns.</summary>
        private static readonly string[] WishKinds =
        {
            HistoryKinds.OfChronicle((int)ChronicleKind.WishGranted), HistoryKinds.OfChronicle((int)ChronicleKind.WishExpired),
            HistoryKinds.OfChronicle((int)ChronicleKind.WishDismissed), HistoryKinds.OfChronicle((int)ChronicleKind.Petition)
        };

        private const float RowH = 26f;

        /// <summary>
        /// One page's model, built a couple of times a second rather than every frame; one per kind of page, so the tab
        /// and the roster drawn together don't take turns rebuilding it.
        /// </summary>
        private class Cache
        {
            public Pawn pawn;
            public float at = -1f;
            public bool socialElsewhere;
            public StoryGlance glance;
            public List<Section> sections;
        }

        private static readonly Cache[] caches = { new Cache(), new Cache(), new Cache() };

        /// <summary>
        /// The cards' models: a window's drawer and the floating card can show two colonists at once, and one model for
        /// both would be built again on every draw of each.
        /// </summary>
        private static readonly Cache[] cardCaches = { new Cache(), new Cache() };

        private static Cache CacheFor(StoryParts parts, Pawn p)
        {
            if (parts != StoryParts.Card) return caches[(int)parts];
            foreach (var c in cardCaches)
                if (c.pawn == p)
                    return c;
            // A colonist not kept yet takes the model built longest ago.
            return cardCaches[0].at <= cardCaches[1].at ? cardCaches[0] : cardCaches[1];
        }

        /// <summary>Forget the last game's colonist (called when a game loads).</summary>
        public static void Reset()
        {
            foreach (var c in caches.Concat(cardCaches))
            {
                c.pawn = null;
                c.glance = null;
                c.sections = null;
            }
        }

        /// <summary>Every model is built again at its next draw (a wish promised from the glance).</summary>
        public static void Stale()
        {
            foreach (var c in caches) c.at = -1f;
            foreach (var c in cardCaches) c.at = -1f;
            Staled++;
        }

        /// <summary>How many times the models were let go: a colonist's card builds its glance again when this moves.</summary>
        public static int Staled { get; private set; }

        // ---------------- Who they are, in one line ----------------

        /// <summary>Who they are in one line, for the Story tab's head, and the whole of it for its tooltip.</summary>
        public sealed class WhoLine
        {
            public string line, tip;
        }

        /// <summary>
        /// "The Healer  ·  Steward  ·  age 34  ·  a founder": the name the colony knows them by, the office they hold (or the
        /// game's title for them), their age and where they come from (a founder, born here, won over by someone while held
        /// here, or the people they lived among; a prisoner or a slave of the colony). The tooltip has each in full, with
        /// their childhood and adulthood, years in cryptosleep and how long they've been with the colony.
        /// </summary>
        public static WhoLine Who(CouncilGameComponent comp, Pawn p)
        {
            var parts = new List<string>(4);
            var tip = new List<string>(8);
            try
            {
                string name = comp.epithets.NameOf(p);
                if (!name.NullOrEmpty())
                {
                    parts.Add(name.CapitalizeFirst());
                    tip.Add(KnownAs.Line(p) ?? "CC_Epithet_Known".Translate(p.LabelShort, name).Resolve());
                }
                var office = OfficeTracker.HeldBy(p);
                string title = p.story?.TitleShortCap;
                if (office != null)
                {
                    parts.Add(office.NameCap);
                    tip.Add("CC_Office_Holds".Translate(p.LabelShort, office.Name).Resolve());
                }
                else if (!title.NullOrEmpty()) parts.Add(title);
                if (p.story?.Childhood != null) tip.Add("CC_Culture_Childhood".Translate(p.story.Childhood.TitleCapFor(p.gender)).Resolve());
                if (p.story?.Adulthood != null) tip.Add("CC_Culture_Adulthood".Translate(p.story.Adulthood.TitleCapFor(p.gender)).Resolve());
                if (p.ageTracker != null)
                {
                    parts.Add("CC_People_Age".Translate(p.ageTracker.AgeBiologicalYears).Resolve());
                    string slept = FamilyYears.SleptNote(p);
                    if (slept != null) tip.Add(slept.CapitalizeFirst() + ".");
                }
                string origin = Origin(comp, p, out string originTip);
                if (origin != null) parts.Add(origin);
                if (originTip != null) tip.Add(originTip);
                if (p.IsColonist && p.records != null)
                {
                    int here = p.records.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal);
                    if (here >= GenDate.TicksPerHour) tip.Add("CC_Culture_Here".Translate(here.ToStringTicksToPeriod(false, false, false)).Resolve());
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("the Story tab: who they are", e);
            }
            return new WhoLine { line = string.Join("  ·  ", parts), tip = string.Join("\n", tip) };
        }

        /// <summary>Where they come from, in a few words, and in a sentence for the tooltip; null when nothing is known.</summary>
        private static string Origin(CouncilGameComponent comp, Pawn p, out string tip)
        {
            tip = null;
            if (p.IsPrisonerOfColony) return "CC_Card_Prisoner".Translate();
            if (p.IsSlaveOfColony) return "CC_Card_Slave".Translate();
            if (comp.epithets != null && comp.epithets.IsFounder(p))
            {
                tip = "CC_Culture_Founder".Translate(p.Named("PAWN")).Resolve();
                return "CC_SGl_Founder".Translate();
            }
            long age = p.ageTracker?.AgeBiologicalTicks ?? long.MaxValue;
            int here = p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0;
            if (here > 0 && age < long.MaxValue && here >= age - GenDate.TicksPerDay * 2)
            {
                tip = "CC_Culture_BornHere".Translate(p.Named("PAWN")).Resolve();
                return "CC_SGl_BornHere".Translate();
            }
            string by = CultureView.RecruitedBy(p, out _);
            if (by != null)
            {
                tip = "CC_Culture_WonOver".Translate(p.Named("PAWN"), by.Named("BY")).Resolve();
                return "CC_SGl_WonOver".Translate(by.Named("BY")).Resolve();
            }
            var home = p.kindDef?.defaultFactionDef;
            if (home == null || home.isPlayer || home.pawnsPlural.NullOrEmpty()) return null;
            tip = "CC_Culture_AmongPeople".Translate(home.pawnsPlural).Resolve();
            return "CC_SGl_Among".Translate(home.pawnsPlural.Named("PEOPLE")).Resolve();
        }

        // ---------------- Drawing the page ----------------

        /// <param name="after">Draws more at the foot of the story, inside its scroll view: (y, width) to the height used (a colonist's card's own sections).</param>
        public static void Draw(Rect outRect, Pawn p, ref Vector2 scroll, ref float height, Folds folds = null, StoryParts parts = StoryParts.All,
            Func<float, float, float> after = null)
        {
            var comp = CouncilGameComponent.Instance;
            if (p == null || comp == null) return;
            var cache = CacheFor(parts, p);
            // The social parts move to the Social tab's column only while it can show.
            bool socialElsewhere = parts == StoryParts.StoryTab && SocialColumn.Available;
            float time = Time.realtimeSinceStartup;
            if (cache.pawn != p || cache.sections == null || time - cache.at > 0.5f || time < cache.at || cache.socialElsewhere != socialElsewhere)
            {
                cache.sections = Build(comp, p, parts, socialElsewhere, out cache.glance);
                cache.pawn = p;
                cache.at = time;
                cache.socialElsewhere = socialElsewhere;
            }
            var glance = cache.glance;
            var sections = cache.sections;
            bool select = parts == StoryParts.StoryTab;
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                float y = 0f;
                if (!glance.Empty)
                {
                    y += glance.Draw(0f, y, view.width);
                    UiKit.Divider(0f, y, view.width);
                    y += 8f;
                }
                else if (sections.Count == 0)
                {
                    GUI.color = Faint;
                    Widgets.Label(new Rect(0f, y, view.width, 60f), "CC_Story_Empty".Translate(p.LabelShort));
                    GUI.color = Color.white;
                    y += 60f;
                }
                foreach (var section in sections)
                {
                    y += UiKit.FoldTitle(0f, y, view.width, section.header, folds, section.key, out bool open, section.icon, HeaderColor, section.count) - 4f;
                    if (!open)
                    {
                        y += 6f;
                        continue;
                    }
                    if (section.character != null)
                        y += CharacterView.DrawSection(new Rect(8f, y, view.width - 8f, 0f), p, section.character, section.skip);
                    else if (section.tree != null)
                        y += DrawFamily(new Rect(8f, y, view.width - 8f, 0f), section);
                    else if (section.friends != null)
                        y += DrawPeople(new Rect(8f, y, view.width - 8f, 0f), p, section, select);
                    else
                        y += DrawLines(new Rect(8f, y, view.width - 8f, 0f), section);
                    if (section.act != null)
                    {
                        float aw = Mathf.Min(UiKit.ActButtonWidth(section.act), view.width);
                        if (UiKit.ActButton(new Rect(view.width - aw, y + 2f, aw, 26f), section.act, null, section.actTip))
                        {
                            try
                            {
                                section.run?.Invoke();
                            }
                            catch (Exception e)
                            {
                                CouncilLog.ErrorOnce("a colonist's story: the act of " + section.key, e);
                            }
                        }
                        y += 32f;
                    }
                    y += 8f;
                }
                if (after != null)
                {
                    try
                    {
                        y += after(y, view.width);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("a colonist's story: what comes after it", e);
                    }
                }
                height = Mathf.Max(y, 100f);
            }
            finally
            {
                Widgets.EndScrollView();
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }
        }

        // ---------------- The model ----------------

        private class Section
        {
            /// <summary>Names the section in the folds, the same in every language.</summary>
            public string key;
            public string header;
            public Texture2D icon;
            /// <summary>A note on the heading's right (how many), or null.</summary>
            public string count;
            public List<(string, Color)> lines = new List<(string, Color)>();
            /// <summary>Lines drawn as quotes (journal entries, dreams): a small heading over each, a stripe beside it.</summary>
            public List<(string head, string text, Color color)> quotes;
            /// <summary>An act at the section's foot ("Its whole history", "Read the journal"), with its tooltip.</summary>
            public string act, actTip;
            public Action run;
            /// <summary>A part of their character page (Persona/CharacterView.cs), which draws itself, less its first <see cref="skip"/> lines.</summary>
            public CharacterSection character;
            public int skip;
            /// <summary>Their family tree, a row each (Social/Family.cs), and how many more are further out.</summary>
            public List<Family.TreeEntry> tree;
            public int treeMore;
            /// <summary>Their friends and rivals, a row each (the glance's people), then the section's lines.</summary>
            public List<StoryGlance.Person> friends, rivals;
            public string friendsLabel, rivalsLabel;
        }

        private static List<Section> Build(CouncilGameComponent comp, Pawn p, StoryParts parts, bool socialElsewhere, out StoryGlance g)
        {
            var s = ColonyCouncilMod.Settings;
            bool tab = parts == StoryParts.StoryTab;
            var list = new List<Section>();
            g = new StoryGlance { pawn = p, select = tab };
            var character = CharacterView.Sections(p);
            var who = Refs.Of(p);

            // ---- At a glance ----
            var quirks = new Section { key = "quirks", header = "CC_SGl_Sec_Quirks".Translate(), icon = Icons.Heart };
            var words = character.FirstOrDefault(c => c.key == "character");
            Glance(comp, p, g, words, quirks);

            // ---- The sections, in order of use ----
            // Their story: the newest lines of their history, the way to the whole of it, and the stories they lived through (Arcs/).
            var story = new Section { key = "story", header = "CC_SGl_Sec_Story".Translate(), icon = Icons.Chronicle };
            var history = History.Of(who, null, int.MinValue, HistoryShown);
            foreach (var e in history) story.lines.Add((CardWords.HistoryDate(e, true) + "  " + CardWords.HistoryLine(e, true), Faint));
            if (history.Count > 0)
            {
                int total = Math.Max(history.Count, History.Count(null, who));
                story.act = "CC_Card_WholeHistory".Translate(total.Named("COUNT")).Resolve();
                story.actTip = "CC_SGl_WholeHistoryTip".Translate(p.Named("PAWN")).Resolve();
                story.run = () => WorldHistoryPage.ShowFor(who, Ref.None);
            }
            story.lines.AddRange(ArcView.Past(p, 5));
            if (story.lines.Count > 0) list.Add(story);

            // Their wishes: before the council now, then what came of the earlier ones.
            if (s.enableWishes)
            {
                var wishes = new Section { key = "wishes", header = "CC_SGl_Sec_Wishes".Translate(), icon = Icons.Wishes };
                int now = Find.TickManager.TicksGame;
                foreach (var w in comp.wishes.Where(w => w != null && w.state == WishState.Open && w.def != null && w.pawn != null && w.Involves(p)).OrderBy(w => w.createdTick))
                    WishLines(wishes, w, p, now);
                var past = History.Of(who, WishKinds, int.MinValue, WishesShown);
                if (past.Count > 0)
                {
                    if (wishes.lines.Count > 0) wishes.lines.Add(("CC_SGl_Earlier".Translate().Resolve(), UiKit.Dim));
                    foreach (var e in past) wishes.lines.Add((CardWords.HistoryDate(e, true) + "  " + CardWords.HistoryLine(e, true), Faint));
                }
                if (wishes.lines.Count > 0)
                {
                    wishes.act = "CC_SGl_WishesPage".Translate().Resolve();
                    wishes.run = () => MainTabWindow_Council.OpenTab("Wishes");
                    list.Add(wishes);
                }
            }

            // Their life goals: the one under way in full, those reached (a title that stays) and the knacks they won't lose (Aspirations/GoalRewards.cs).
            var life = new Section { key = "life", header = "CC_SGl_Sec_Goals".Translate(), icon = Icons.LifeGoal };
            if (s.aspirations)
            {
                if (g.goal != null)
                {
                    life.lines.Add((g.goal + ": " + g.goalWords + (g.goalHeld != null ? "  ·  " + g.goalHeld : ""), UiKit.Ink));
                    if (g.goalWhy != null) life.lines.Add((g.goalWhy, Faint));
                }
                foreach (var done in comp.aspirations.Fulfilled(p))
                    life.lines.Add(("CC_Story_Remembered".Translate(done.Deed, "CC_DayN".Translate(done.doneTick / GenDate.TicksPerDay + 1)).Resolve(), UiKit.Good));
                var marked = GoalRewards.MarksOf(p).Select(x => x.label).ToList();
                if (marked.Count > 0) life.lines.Add(("CC_Story_Marked".Translate(TextUtil.JoinAnd(marked)).Resolve(), UiKit.Good));
            }
            if (life.lines.Count > 0) list.Add(life);

            if (quirks.lines.Count > 0) list.Add(quirks);

            // Their friends and rivals, every one; and Rimfolk's bonds and those lost (in the Social tab's column, when it shows).
            var people = new Section
            {
                key = "people", header = "CC_SGl_Sec_People".Translate(), icon = Icons.Bonds, friends = g.friends, rivals = g.rivals,
                friendsLabel = g.friendsLabel, rivalsLabel = g.rivalsLabel
            };
            if (s.socialWeb && !socialElsewhere)
            {
                string describe = comp.social.Describe(p);
                if (!describe.NullOrEmpty())
                    people.lines.Add((PaneLinks.Mark(describe, comp.social.ties.Where(t => t != null && t.Involves(p)).Select(t => t.Other(p)).Where(o => o != null && !o.Dead)), Color.white));
                foreach (var l in comp.social.lost.Where(l => l.survivor == p).Take(5))
                    people.lines.Add(("CC_Story_Lost".Translate(l.name, ("CC_Tie_" + l.kind).Translate()).Resolve(), Faint));
            }
            if (people.friends.Count + people.rivals.Count > 0)
                people.count = "CC_SGl_PeopleCount".Translate(people.friends.Count.Named("FRIENDS"), people.rivals.Count.Named("RIVALS")).Resolve();
            if (people.friends.Count + people.rivals.Count + people.lines.Count > 0) list.Add(people);

            // Their family, a row each, and where everyone is (Social/Family.cs).
            var tree = Family.Tree(p, out int further);
            // The family alive, whose names in these lines lead to their cards.
            var kin = tree.Where(e => e.pawn != null && !e.pawn.Dead && !e.pawn.Discarded).Select(e => e.pawn).ToList();
            if (tree.Count > 0 || further > 0)
            {
                var family = new Section { key = "family", header = "CC_Story_Family".Translate(), icon = Icons.Bonds, tree = tree, treeMore = further };
                // One of the Boyds and what the family's name stands for (Social/FamilyNames.cs); named after a grandmother (Social/FamilyLife.cs).
                string known = FamilyNames.StoryLine(p);
                if (known != null) family.lines.Add((PaneLinks.Mark(known, kin), Faint));
                string named = FamilyLife.NamedAfterLine(p);
                if (named != null) family.lines.Add((PaneLinks.Mark(named, kin), Faint));
                list.Add(family);
            }

            // Dreams.
            if (s.dreams)
            {
                var dreams = new Section { key = "dreams", header = "CC_Social_Dreams".Translate(), icon = Icons.Dream, quotes = new List<(string, string, Color)>() };
                foreach (var d in comp.dreams.recent.Where(d => d.pawn == p).Reverse().Take(3))
                    dreams.quotes.Add((d.cameTrue ? "CC_Social_DreamCameTrue".Translate().CapitalizeFirst().ToString() : null, "\"" + d.text + "\"", UiKit.DreamColor));
                if (dreams.quotes.Count > 0) list.Add(dreams);
            }

            // Their character and what shaped it, with what the other systems add (Persona/CharacterView.cs).
            foreach (var cs in character)
            {
                if (tab && CultureKeys.Contains(cs.key)) continue;
                if (socialElsewhere && SocialColumn.CharacterKeys.Contains(cs.key)) continue;
                // Their groups, what people say and their shared memories name the colony's people: links.
                if (SocialColumn.CharacterKeys.Contains(cs.key)) PaneLinks.MarkColony(cs, p);
                int skip = cs == words ? 1 : 0;
                // Their character in words is the glance's: the section keeps the bars and their nature.
                list.Add(new Section { key = "character" + cs.order, header = cs.title, icon = CharacterView.Icon(cs.order), character = cs, skip = skip });
            }

            // Years in cryptosleep, and relatives whose ages run the wrong way round (Social/FamilyYears.cs).
            var yearLines = FamilyYears.StoryLines(p);
            if (yearLines.Count > 0)
            {
                var years = new Section { key = "years", header = "CC_Story_Years".Translate(), icon = Icons.Birth };
                foreach (var line in yearLines) years.lines.Add((PaneLinks.Mark(line, kin), Color.white));
                list.Add(years);
            }

            // Secrets (in the Social tab's column, when it shows).
            if (s.secrets && !socialElsewhere)
            {
                var secrets = new Section { key = "secrets", header = "CC_Social_Secrets".Translate(), icon = Icons.Secret };
                var secret = comp.secrets.Of(p);
                if (secret != null && secret.revealed) secrets.lines.Add((secret.Truth, UiKit.Sour));
                else if (secret != null && secret.knowers.Count > 0)
                    secrets.lines.Add((PaneLinks.Mark("CC_Social_SecretKnown".Translate(p.LabelShort, TextUtil.JoinAnd(secret.knowers.Select(k => k.LabelShort).ToList())).Resolve(),
                        secret.knowers), Faint));
                foreach (var other in comp.secrets.secrets.Where(x => x.owner != p && !x.revealed && x.knowers.Contains(p) && x.owner != null))
                    secrets.lines.Add((PaneLinks.Mark("CC_Story_Keeps".Translate(other.owner.LabelShort).Resolve(), other.owner), Faint));
                if (secrets.lines.Count > 0) list.Add(secrets);
            }

            // Journal: the newest two entries, and the way to the whole of it.
            if (s.journals)
            {
                var j = comp.journals.For(p);
                if (j != null && j.entries.Count > 0)
                {
                    var journal = new Section { key = "journal", header = "CC_Story_Journal".Translate(), icon = Icons.Journal, quotes = new List<(string, string, Color)>() };
                    foreach (var e in j.entries.AsEnumerable().Reverse().Take(2))
                        journal.quotes.Add((e.date, e.text, UiKit.Quote));
                    journal.act = "CC_Story_ReadJournal".Translate();
                    journal.run = () => Find.WindowStack.Add(new Dialog_Journal(j));
                    list.Add(journal);
                }
            }
            return list;
        }

        /// <summary>
        /// Their story at a glance alone, without the sections under it (a colonist's card, UI/ColonistCard/): built from the
        /// same sources and drawn the same way. <paramref name="select"/>: a friend's portrait selects them rather than opening
        /// their card.
        /// </summary>
        public static StoryGlance Glance(CouncilGameComponent comp, Pawn p, bool select)
        {
            var g = new StoryGlance { pawn = p, select = select };
            if (comp == null || p == null) return g;
            Glance(comp, p, g, Persona.Eligible(p) ? CharacterView.Character(p) : null, null);
            return g;
        }

        /// <summary>
        /// The glance's parts: their character in words (<paramref name="words"/>, the character page's first section), life
        /// goal, the story they're living, wish, the quirks' tags (each written in full into <paramref name="quirks"/> too when
        /// the page has its section), standing, friends and rivals, and the newest line of their history.
        /// </summary>
        private static void Glance(CouncilGameComponent comp, Pawn p, StoryGlance g, CharacterSection words, Section quirks)
        {
            var s = ColonyCouncilMod.Settings;
            // Their character in words (the Character section has the bars).
            if (words != null && words.lines.Count > 0)
            {
                g.words = words.lines[0];
                g.wordsTip = words.tips.Count > 0 ? words.tips[0] : null;
            }
            // Their life goal, the story they're living (Arcs/) and their wish before the council.
            g.BuildGoal(comp, p);
            g.story = ArcView.Now(p, out g.storyTip, out g.stage, out g.stages);
            g.BuildWish(comp, p);
            // Quirks, keepsakes, vendettas and nemeses: a tag each, what it means in its tooltip; in full in their section.
            if (s.quirks)
                foreach (var q in comp.quirks.Of(p))
                {
                    g.tags.Add(new UiKit.PillSpec(q.Label.CapitalizeFirst(), UiKit.HeaderColor, null, QuirkEffects.Tip(q)));
                    if (quirks == null) continue;
                    string from = QuirkEffects.From(q);
                    quirks.lines.Add(((from.NullOrEmpty() ? q.Label : "CC_QuirkFx_NameFrom".Translate(q.Label, from).Resolve()).CapitalizeFirst(), UiKit.Ink));
                    quirks.lines.Add((QuirkEffects.Effects(q) + ". " + QuirkEffects.Lasts(q).CapitalizeFirst() + ".", Faint));
                }
            if (s.heirlooms)
                foreach (var h in comp.legacy.heirlooms.Where(h => h.heir == p && h.thing != null && !h.thing.Destroyed))
                {
                    string line = "CC_Story_Heirloom".Translate(h.ofName, h.thing.def.label).Resolve();
                    g.tags.Add(new UiKit.PillSpec("CC_Story_HeirloomTag".Translate(h.ofName, h.thing.def.label).Resolve(), UiKit.LossColor, Icons.Heart, line));
                    quirks?.lines.Add((line, Faint));
                }
            if (s.vendettas)
                foreach (var v in comp.vendettas.vendettas.Where(v => v.avenger == p && v.faction != null))
                {
                    string line = v.LineKey.Translate(p.LabelShort, v.faction.Name, v.victim, v.kills, Vendetta.KillsToSettle).Resolve();
                    g.tags.Add(new UiKit.PillSpec("CC_Story_VendettaTag".Translate(v.faction.Name).Resolve(), v.settled ? Faint : UiKit.Bad, Icons.Vendetta, line,
                        ThingRefs.Of(v.faction)));
                    quirks?.lines.Add((PaneLinks.Mark(line, v.faction), v.settled ? Faint : UiKit.Bad));
                }
            if (s.nemeses && comp.nemeses != null)
                foreach (var n in comp.nemeses.Of(p).OrderBy(n => n.ended))
                {
                    string line = "CC_Story_Nemesis".Translate(n.Line).Resolve();
                    bool known = n.pawn != null && !n.pawn.Discarded;
                    g.tags.Add(new UiKit.PillSpec("CC_Story_Nemesis".Translate(n.pawn?.LabelShort ?? n.name).Resolve(), n.ended ? Faint : UiKit.Bad, Icons.Nemesis, line,
                        known ? ThingRefs.Of(n.pawn) : ThingRef.None));
                    quirks?.lines.Add((known ? PaneLinks.Mark(line, n.pawn) : line, n.ended ? Faint : UiKit.Bad));
                }
            // Their standing, closest friends and rivals, and the newest line of their history.
            g.BuildPeople(comp, p);
            g.BuildRecent(p);
        }

        /// <summary>A wish before the council in their wishes' section: what it is, then what fulfils it, how long they've waited and whether it's promised.</summary>
        private static void WishLines(Section sec, Wish w, Pawn p, int now)
        {
            string label = w.pawn == p
                ? "CC_SGl_Wishes".Translate(w.LabelInline.Named("WISH")).Resolve()
                : "CC_SGl_WishedFor".Translate(w.pawn.LabelShort.Named("ASKER"), w.LabelInline.Named("WISH")).Resolve();
            sec.lines.Add((w.pawn != p ? PaneLinks.Mark(label, w.pawn) : label, w.stage >= 2 ? UiKit.Sour : w.promised ? UiKit.Gold : UiKit.Ink));
            var bits = new List<string>(4);
            try
            {
                string when = w.Worker.FulfilledWhen(w);
                string progress = w.Worker.ProgressText(w);
                if (!when.NullOrEmpty()) bits.Add("CC_FulfilledWhen".Translate() + ": " + when);
                if (!progress.NullOrEmpty()) bits.Add("CC_Progress".Translate() + ": " + progress);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a colonist's story: a wish's words", e);
            }
            bits.Add("CC_SGl_Waiting".Translate(UiKit.Duration(now - w.createdTick).Named("TIME")).Resolve());
            if (w.promised) bits.Add("CC_PromisedTag".Translate());
            if (w.stage >= 2) bits.Add("CC_LastCallTag".Translate());
            sec.lines.Add((string.Join("  ·  ", bits).CapitalizeFirst(), Faint));
        }

        // ---------------- Drawing the sections ----------------

        /// <summary>
        /// Their friends, then their rivals, a row each: their face, name, the colonist's opinion of them and theirs back, and
        /// what they are to them (a relation, Rimfolk's bond); a click selects them in the Story tab, opens their card
        /// elsewhere. Then the section's lines (Rimfolk's bonds, those lost).
        /// </summary>
        private static float DrawPeople(Rect r, Pawn p, Section s, bool select)
        {
            float y = r.y;
            int i = 0;
            void Group(string label, List<StoryGlance.Person> group)
            {
                if (group == null || group.Count == 0) return;
                UiKit.LabelFit(new Rect(r.x, y, r.width, 18f), label, UiKit.Dim, GameFont.Tiny);
                y += 18f;
                float nameW = Mathf.Min(120f, r.width * 0.3f), opW = 34f, backW = Mathf.Min(90f, r.width * 0.2f);
                foreach (var o in group)
                {
                    var rr = new Rect(r.x, y, r.width, RowH - 2f);
                    UiKit.Zebra(rr, i++);
                    UiKit.RowHover(rr);
                    UiKit.Portrait(new Rect(rr.x + 2f, rr.y + 1f, 22f, 22f), o.pawn, UiKit.WithAlpha(o.color, 0.8f));
                    float x = rr.x + 30f;
                    // The row's tooltip says all of it (StoryGlance.Clickable), so the words are cut without one of their own.
                    StoryGlance.Plain(new Rect(x, rr.y, nameW, rr.height), o.name, UiKit.Ink);
                    x += nameW + 4f;
                    StoryGlance.Plain(new Rect(x, rr.y, opW, rr.height), o.opinion, o.color, GameFont.Tiny);
                    x += opW + 4f;
                    StoryGlance.Plain(new Rect(x, rr.y, Mathf.Max(0f, rr.xMax - x - backW - 4f), rr.height), o.what, Faint, GameFont.Tiny);
                    StoryGlance.Plain(new Rect(rr.xMax - backW, rr.y, backW, rr.height), o.backWords, Faint, GameFont.Tiny, TextAnchor.MiddleRight);
                    StoryGlance.Clickable(rr, p, o, select);
                    y += RowH;
                }
                y += 4f;
            }
            Group(s.friendsLabel, s.friends);
            Group(s.rivalsLabel, s.rivals);
            if (s.lines.Count > 0) y += DrawLines(new Rect(r.x, y, r.width, 0f), s);
            return y - r.y;
        }

        /// <summary>The family tree, a row each: their face (or where they are, for those not on a map), name, what they are, age and where.</summary>
        private static float DrawFamily(Rect r, Section s)
        {
            float y = r.y;
            foreach (var (text, color) in s.lines) y += LinkText.Draw(new Rect(r.x, y, r.width, 999f), text, color) + 4f;
            const float row = 26f;
            float nameW = Mathf.Min(110f, r.width * 0.27f), relW = Mathf.Min(120f, r.width * 0.29f), ageW = 28f;
            for (int i = 0; i < s.tree.Count; i++)
            {
                var e = s.tree[i];
                var rr = new Rect(r.x, y, r.width, row - 2f);
                UiKit.Zebra(rr, i);
                UiKit.RowHover(rr);
                var color = WhereColor(e.whereKind);
                var face = new Rect(rr.x + 2f, rr.y + 1f, 22f, 22f);
                if (!e.pawn.Dead && e.pawn.Spawned) UiKit.Portrait(face, e.pawn, color);
                else UiKit.Badge(face, e.pawn.Dead ? Icons.Death : Icons.World, color);
                float x = face.xMax + 6f;
                // A relative the game still has is a link to their card.
                if (!e.pawn.Dead && !e.pawn.Discarded) LinkText.Line(new Rect(x, rr.y, nameW, rr.height), ThingRefs.Link(e.pawn.LabelShort, ThingRefs.Of(e.pawn)), Color.white);
                else UiKit.LabelFit(new Rect(x, rr.y, nameW, rr.height), e.pawn.LabelShort, e.pawn.Dead ? UiKit.Dim : Color.white);
                x += nameW + 4f;
                UiKit.LabelFit(new Rect(x, rr.y, relW, rr.height), e.relation.CapitalizeFirst(), Faint);
                x += relW + 4f;
                if (!e.pawn.Dead) UiKit.LabelFit(new Rect(x, rr.y, ageW, rr.height), FamilyYears.Lived(e.pawn).ToString(), Faint);
                x += ageW + 4f;
                UiKit.LabelFit(new Rect(x, rr.y, Mathf.Max(0f, rr.xMax - x - 4f), rr.height), e.where.CapitalizeFirst(), color);
                if (Previews.Shown.IsNone) TooltipHandler.TipRegion(rr, e.line);
                y += row;
            }
            if (s.treeMore > 0) y += 2f + UiKit.Paragraph(r.x, y + 2f, r.width, "CC_Family_More".Translate(s.treeMore).Resolve(), Faint);
            return y - r.y;
        }

        /// <summary>Where a relative is, as a color: here, ours elsewhere, held or taken, visiting, out in the world, gone.</summary>
        private static Color WhereColor(KinWhere where)
        {
            switch (where)
            {
                case KinWhere.Here: return UiKit.Good;
                case KinWhere.Elsewhere:
                case KinWhere.Away: return UiKit.Info;
                case KinWhere.Prisoner:
                case KinWhere.Slave:
                case KinWhere.Taken: return UiKit.Warn;
                case KinWhere.Visiting: return UiKit.StrangerColor;
                case KinWhere.WithFaction:
                case KinWhere.World: return UiKit.WorldColor;
                default: return UiKit.Dim;
            }
        }

        /// <summary>A section's lines, or its quotes: a small heading (the date) over each, a stripe in its color beside it.</summary>
        private static float DrawLines(Rect r, Section s)
        {
            float y = r.y;
            Text.Font = GameFont.Small;
            foreach (var (text, color) in s.lines)
            {
                // A line naming the colony's people or kin has them as links.
                if (LinkTextMath.HasMarks(text))
                {
                    y += LinkText.Draw(new Rect(r.x, y, r.width, 999f), text, color) + 2f;
                    continue;
                }
                float h = Text.CalcHeight(text, r.width);
                GUI.color = color;
                Widgets.Label(new Rect(r.x, y, r.width, h), text);
                GUI.color = Color.white;
                y += h + 2f;
            }
            if (s.quotes == null) return y - r.y;
            float qx = r.x + 10f, qw = r.width - 10f;
            foreach (var (head, text, color) in s.quotes)
            {
                float top = y;
                if (!head.NullOrEmpty())
                {
                    UiKit.LabelFit(new Rect(qx, y, qw, 16f), head, UiKit.Dim, GameFont.Tiny);
                    y += 16f;
                }
                Text.Font = GameFont.Small;
                float h = Text.CalcHeight(text, qw);
                GUI.color = color;
                Widgets.Label(new Rect(qx, y, qw, h), text);
                GUI.color = Color.white;
                y += h;
                Widgets.DrawBoxSolid(new Rect(r.x + 2f, top + 2f, 2f, Mathf.Max(0f, y - top - 4f)), UiKit.WithAlpha(color, 0.5f));
                y += 8f;
            }
            return y - r.y;
        }
    }
}
