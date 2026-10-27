using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Story" in a colonist's inspect pane: who they are and what they've lived. Under their name, what matters at a
    /// glance (StoryView's overview); under that the longer parts, the lists starting folded. Their social life (bonds,
    /// groups, what people say, shared memories, secrets) is in Rimfolk's column of the game's Social tab
    /// (UI/SocialColumn.cs), and their faith and views in the Culture tab (UI/ITab_Pawn_Culture.cs); with the column
    /// switched off, the social parts stay here. Fits the room above the inspect pane (RimfolkTab); the name stays put,
    /// the rest scrolls, and each section folds away.
    /// </summary>
    public class ITab_Pawn_Story : RimfolkTab
    {
        private Vector2 scroll;
        private float height = 400f;
        private Pawn shown;
        /// <summary>Which of the tab's sections are folded away this session; the long lists start folded.</summary>
        private static readonly Folds folds = new Folds(StoryView.FoldedInTab);

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
            // The name, and under it what they're known as; the button keeps clear of the tab's close button in the top right corner.
            float tx = rect.x + face + 8f, tw = rect.width - face - 8f - 144f;
            string titles = UiCache.Get(p, 41, () => comp.epithets.TitlesOf(p));
            if (titles.NullOrEmpty())
                UiKit.LabelFit(new Rect(tx, rect.y + 4f, tw, 32f), p.LabelShortCap, Color.white, GameFont.Medium);
            else
            {
                UiKit.LabelFit(new Rect(tx, rect.y - 3f, tw, 28f), p.LabelShortCap, Color.white, GameFont.Medium);
                UiKit.LabelFit(new Rect(tx, rect.y + 23f, tw, 18f), titles.CapitalizeFirst(), UiKit.HeaderColor, GameFont.Tiny);
            }
            if (p.IsColonist && Widgets.ButtonText(new Rect(rect.xMax - 138f, rect.y + 7f, 124f, 26f), "CC_Story_OpenCouncil".Translate()))
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
        StoryTab
    }

    /// <summary>
    /// The story of one colonist, drawn in the Story tab and the council window's People roster. First an overview of
    /// what matters at a glance: their character in words; their standing (trust, office, name as a doctor); what they
    /// want from life, with how far they've come; the story they're living and its stage; their quirks, keepsakes,
    /// vendettas and nemeses as tags, with what each one does in its tooltip; and the family who are here, the rest
    /// counted, everyone in the tooltip. Then the sections: their character on its bars, what shaped it and the traits
    /// life changed, their family tree, the years in cryptosleep, their life so far (goals reached, stories lived
    /// through), the social parts while the Social tab's column is off, dreams and the newest lines of their journal.
    /// Each place passes its own folds, so a section folded in the tab stays open in the roster, and its own parts: the
    /// roster shows everything, the Story tab leaves out what the Social tab's column and the Culture tab show.
    /// </summary>
    public static class StoryView
    {
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color HeaderColor = new Color(0.95f, 0.85f, 0.6f);

        /// <summary>The character page's sections the Culture tab shows (its views and faith).</summary>
        public static readonly string[] CultureKeys = { "talk.views", "faith" };

        /// <summary>The Story tab's sections that start folded: the long lists (the overview has the gist of them).</summary>
        public static readonly string[] FoldedInTab = { "character20", "character25", "character26", "family", "years", "life", "dreams" };

        /// <summary>At most this many of the family who are here are shown in the overview; the rest are counted.</summary>
        private const int FamilyShown = 4;

        private const float RowH = 22f;

        /// <summary>
        /// One page's model, built a couple of times a second rather than every frame; one per kind of page, so the tab
        /// and the roster drawn together don't take turns rebuilding it.
        /// </summary>
        private class Cache
        {
            public Pawn pawn;
            public float at = -1f;
            public bool socialElsewhere;
            public Overview overview;
            public List<Section> sections;
        }

        private static readonly Cache[] caches = { new Cache(), new Cache() };

        /// <summary>Forget the last game's colonist (called when a game loads).</summary>
        public static void Reset()
        {
            foreach (var c in caches)
            {
                c.pawn = null;
                c.overview = null;
                c.sections = null;
            }
        }

        public static void Draw(Rect outRect, Pawn p, ref Vector2 scroll, ref float height, Folds folds = null, StoryParts parts = StoryParts.All)
        {
            var comp = CouncilGameComponent.Instance;
            if (p == null || comp == null) return;
            var cache = caches[parts == StoryParts.StoryTab ? 1 : 0];
            // The social parts move to the Social tab's column only while it can show.
            bool socialElsewhere = parts == StoryParts.StoryTab && SocialColumn.Available;
            float time = Time.realtimeSinceStartup;
            if (cache.pawn != p || cache.sections == null || time - cache.at > 0.5f || time < cache.at || cache.socialElsewhere != socialElsewhere)
            {
                cache.sections = Build(comp, p, parts, socialElsewhere, out cache.overview);
                cache.pawn = p;
                cache.at = time;
                cache.socialElsewhere = socialElsewhere;
            }
            var overview = cache.overview;
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            if (!overview.Empty)
            {
                float h = DrawOverview(new Rect(0f, y, view.width, 0f), overview, false);
                UiKit.Card(new Rect(0f, y, view.width, h), UiKit.WithAlpha(UiKit.Gold, 0.6f), false, false);
                DrawOverview(new Rect(0f, y, view.width, 0f), overview, true);
                y += h + 10f;
            }
            else if (cache.sections.Count == 0)
            {
                GUI.color = Faint;
                Widgets.Label(new Rect(0f, y, view.width, 60f), "CC_Story_Empty".Translate(p.LabelShort));
                GUI.color = Color.white;
                y += 60f;
            }
            foreach (var section in cache.sections)
            {
                y += UiKit.FoldTitle(0f, y, view.width, section.header, folds, section.key, out bool open, section.icon, HeaderColor) - 4f;
                if (!open)
                {
                    y += 6f;
                    continue;
                }
                if (section.character != null)
                    y += CharacterView.DrawSection(new Rect(8f, y, view.width - 8f, 0f), p, section.character, section.skip);
                else if (section.tree != null)
                    y += DrawFamily(new Rect(8f, y, view.width - 8f, 0f), section);
                else
                    y += DrawLines(new Rect(8f, y, view.width - 8f, 0f), section);
                if (section.journal != null && Widgets.ButtonText(new Rect(view.width - 160f, y, 160f, 26f), "CC_Story_ReadJournal".Translate()))
                    Find.WindowStack.Add(new Dialog_Journal(section.journal));
                if (section.journal != null) y += 30f;
                y += 8f;
            }
            height = Mathf.Max(y, 100f);
            Widgets.EndScrollView();
        }

        // ---------------- The model ----------------

        /// <summary>What the overview shows (see the class summary); empty parts are left out.</summary>
        private class Overview
        {
            public string words, wordsTip;
            public readonly List<UiKit.PillSpec> standing = new List<UiKit.PillSpec>();
            public string goal, goalTip;
            public float goalProgress;
            public string story, storyTip;
            /// <summary>The stage their own story is at (0 first) of <see cref="stages"/>; -1 for someone else's story.</summary>
            public int stage = -1, stages;
            public readonly List<UiKit.PillSpec> tags = new List<UiKit.PillSpec>();
            public readonly List<(Pawn pawn, string tip)> family = new List<(Pawn, string)>();
            /// <summary>The family not shown (elsewhere, dead, further out, or past the few here), the words for it, and the whole tree for the tooltip.</summary>
            public int familyRest;
            public string familyMore, familyMoreMost, familyTip;

            public bool Empty => words == null && standing.Count == 0 && goal == null && story == null && tags.Count == 0 && family.Count == 0 && familyRest == 0;
        }

        private class Section
        {
            /// <summary>Names the section in the folds, the same in every language.</summary>
            public string key;
            public string header;
            public Texture2D icon;
            public List<(string, Color)> lines = new List<(string, Color)>();
            /// <summary>Lines drawn as quotes (journal entries, dreams): a small heading over each, a stripe beside it.</summary>
            public List<(string head, string text, Color color)> quotes;
            public PawnJournal journal;
            /// <summary>A part of their character page (Persona/CharacterView.cs), which draws itself, less its first <see cref="skip"/> lines.</summary>
            public CharacterSection character;
            public int skip;
            /// <summary>Their family tree, a row each (Social/Family.cs), and how many more are further out.</summary>
            public List<Family.TreeEntry> tree;
            public int treeMore;
        }

        private static List<Section> Build(CouncilGameComponent comp, Pawn p, StoryParts parts, bool socialElsewhere, out Overview o)
        {
            var s = ColonyCouncilMod.Settings;
            bool tab = parts == StoryParts.StoryTab;
            var list = new List<Section>();
            o = new Overview();
            var character = CharacterView.Sections(p);

            // ---- The overview ----
            // Their character in words (the Character section has the bars).
            var words = character.FirstOrDefault(c => c.key == "character");
            if (words != null && words.lines.Count > 0)
            {
                o.words = words.lines[0];
                o.wordsTip = words.tips.Count > 0 ? words.tips[0] : null;
            }
            // Standing: trust, office, their name as a doctor or how they're mending (Health/).
            var st = comp.standings.For(p, false);
            if (st != null)
                o.standing.Add(new UiKit.PillSpec(("CC_Trust_" + PawnStanding.LevelFor(st.trust)).Translate().CapitalizeFirst(), UiKit.TrustColor(st.trust), Icons.Trust,
                    "CC_Trust_Header".Translate() + "\n\n" + "CC_Trust_Tip".Translate(p.LabelShort, st.trust.ToString("0"), st.granted, st.ignored, st.refused, st.promisesKept, st.promisesBroken).Resolve()));
            var office = comp.offices.OfficeOf(p);
            if (office != null)
                o.standing.Add(new UiKit.PillSpec(office.LabelCap, UiKit.Gold, Icons.Council,
                    "CC_Office_Holds".Translate(p.LabelShort, office.label).Resolve() + (office.description.NullOrEmpty() ? "" : "\n\n" + office.description)));
            string health = HealthTracker.StoryTag(p, out var healthColor);
            if (health != null) o.standing.Add(new UiKit.PillSpec(health.CapitalizeFirst(), healthColor, Icons.CatHealth, string.Join("\n", HealthTracker.StoryLines(p))));
            // What they want from life, and how far they've come.
            var life = new Section { key = "life", header = "CC_Story_LifeSoFar".Translate(), icon = Icons.LifeGoal };
            if (s.aspirations)
            {
                var a = comp.aspirations.Active(p);
                if (a != null)
                {
                    string progress;
                    try { progress = a.def.Worker.ProgressText(a); }
                    catch { progress = a.Progress.ToStringPercent(); }
                    o.goal = "CC_Story_Wants".Translate(a.Goal).Resolve();
                    o.goalProgress = a.Progress;
                    o.goalTip = "CC_Story_Goal".Translate(a.Goal, progress).Resolve();
                    string why = a.SparkText;
                    if (!why.NullOrEmpty()) o.goalTip += "\n\n" + "CC_Story_GoalWhy".Translate(why, "CC_DayN".Translate(a.startedTick / GenDate.TicksPerDay + 1)).Resolve();
                }
                foreach (var done in comp.aspirations.Fulfilled(p))
                    life.lines.Add(("CC_Story_GoalDone".Translate(done.Goal, "CC_DayN".Translate(done.doneTick / GenDate.TicksPerDay + 1)).Resolve(), UiKit.Good));
            }
            // The story they're living (Arcs/), and the ones they've lived through.
            o.story = ArcView.Now(p, out o.storyTip, out o.stage, out o.stages);
            life.lines.AddRange(ArcView.Past(p));
            // Quirks, keepsakes, vendettas and nemeses: a tag each, what it means in its tooltip.
            if (s.quirks)
                foreach (var q in comp.quirks.Of(p))
                    o.tags.Add(new UiKit.PillSpec(q.Label.CapitalizeFirst(), UiKit.HeaderColor, null, QuirkEffects.Tip(q)));
            if (s.heirlooms)
                foreach (var h in comp.legacy.heirlooms.Where(h => h.heir == p && h.thing != null && !h.thing.Destroyed))
                    o.tags.Add(new UiKit.PillSpec("CC_Story_HeirloomTag".Translate(h.ofName, h.thing.def.label).Resolve(), UiKit.LossColor, Icons.Heart,
                        "CC_Story_Heirloom".Translate(h.ofName, h.thing.def.label).Resolve()));
            if (s.vendettas)
                foreach (var v in comp.vendettas.vendettas.Where(v => v.avenger == p && v.faction != null))
                    o.tags.Add(new UiKit.PillSpec("CC_Story_VendettaTag".Translate(v.faction.Name).Resolve(), v.settled ? Faint : UiKit.Bad, Icons.Vendetta,
                        v.LineKey.Translate(p.LabelShort, v.faction.Name, v.victim, v.kills, Vendetta.KillsToSettle).Resolve()));
            if (s.nemeses && comp.nemeses != null)
                foreach (var n in comp.nemeses.Of(p).OrderBy(n => n.ended))
                    o.tags.Add(new UiKit.PillSpec("CC_Story_Nemesis".Translate(n.pawn?.LabelShort ?? n.name).Resolve(), n.ended ? Faint : UiKit.Bad, Icons.Nemesis,
                        "CC_Story_Nemesis".Translate(n.Line).Resolve()));
            // The family who are here, closest first; the rest counted, and everyone in the tooltip (Social/Family.cs).
            var tree = Family.Tree(p, out int further);
            foreach (var e in tree.Where(e => !e.pawn.Dead && e.pawn.Spawned && e.pawn.Map == p.MapHeld).Take(FamilyShown))
                o.family.Add((e.pawn, e.line));
            o.familyRest = tree.Count - o.family.Count + further;
            o.familyMore = (o.family.Count > 0 ? "CC_Story_FamilyMore" : "CC_Story_FamilyElsewhere").Translate(o.familyRest).Resolve();
            // The most it can say, if none of the portraits find room: what the row leaves space for.
            o.familyMoreMost = "CC_Story_FamilyMore".Translate(o.familyRest + o.family.Count).Resolve();
            var treeLines = tree.Select(e => e.line).ToList();
            if (further > 0) treeLines.Add("CC_Family_More".Translate(further).Resolve());
            // Named after a grandmother, or sharing a name with one (Social/FamilyLife.cs).
            string named = FamilyLife.NamedAfterLine(p);
            if (named != null) treeLines.Insert(0, named);
            o.familyTip = string.Join("\n", treeLines);

            // ---- The sections ----
            // Their character and what shaped it, with what the other systems add (Persona/CharacterView.cs).
            foreach (var cs in character)
            {
                if (tab && CultureKeys.Contains(cs.key)) continue;
                if (socialElsewhere && SocialColumn.CharacterKeys.Contains(cs.key)) continue;
                int skip = cs == words ? 1 : 0;
                // Their character in words is the overview's: the section keeps the bars and their nature.
                list.Add(new Section { key = "character" + cs.order, header = cs.title, icon = CharacterView.Icon(cs.order), character = cs, skip = skip });
            }

            // Their family, a row each, and where everyone is.
            if (tree.Count > 0 || further > 0)
            {
                var family = new Section { key = "family", header = "CC_Story_Family".Translate(), icon = Icons.Bonds, tree = tree, treeMore = further };
                if (named != null) family.lines.Add((named, Faint));
                list.Add(family);
            }

            // Years in cryptosleep, and relatives whose ages run the wrong way round (Social/FamilyYears.cs).
            var yearLines = FamilyYears.StoryLines(p);
            if (yearLines.Count > 0)
            {
                var years = new Section { key = "years", header = "CC_Story_Years".Translate(), icon = Icons.Birth };
                foreach (var line in yearLines) years.lines.Add((line, Color.white));
                list.Add(years);
            }

            if (life.lines.Count > 0) list.Add(life);

            // Bonds (in the Social tab's column, when it shows).
            if (s.socialWeb && !socialElsewhere)
            {
                var bonds = new Section { key = "bonds", header = "CC_Story_Bonds".Translate(), icon = Icons.Bonds };
                string describe = comp.social.Describe(p);
                if (!describe.NullOrEmpty()) bonds.lines.Add((describe, Color.white));
                foreach (var l in comp.social.lost.Where(l => l.survivor == p).Take(5))
                    bonds.lines.Add(("CC_Story_Lost".Translate(l.name, ("CC_Tie_" + l.kind).Translate()).Resolve(), Faint));
                if (bonds.lines.Count > 0) list.Add(bonds);
            }

            // Secrets (in the Social tab's column, when it shows).
            if (s.secrets && !socialElsewhere)
            {
                var secrets = new Section { key = "secrets", header = "CC_Social_Secrets".Translate(), icon = Icons.Secret };
                var secret = comp.secrets.Of(p);
                if (secret != null && secret.revealed) secrets.lines.Add((secret.Truth, new Color(0.9f, 0.6f, 0.5f)));
                else if (secret != null && secret.knowers.Count > 0)
                    secrets.lines.Add(("CC_Social_SecretKnown".Translate(p.LabelShort, TextUtil.JoinAnd(secret.knowers.Select(k => k.LabelShort).ToList())).Resolve(), Faint));
                foreach (var other in comp.secrets.secrets.Where(x => x.owner != p && !x.revealed && x.knowers.Contains(p) && x.owner != null))
                    secrets.lines.Add(("CC_Story_Keeps".Translate(other.owner.LabelShort).Resolve(), Faint));
                if (secrets.lines.Count > 0) list.Add(secrets);
            }

            // Dreams.
            if (s.dreams)
            {
                var dreams = new Section { key = "dreams", header = "CC_Social_Dreams".Translate(), icon = Icons.Dream, quotes = new List<(string, string, Color)>() };
                foreach (var d in comp.dreams.recent.Where(d => d.pawn == p).Reverse().Take(3))
                    dreams.quotes.Add((d.cameTrue ? "CC_Social_DreamCameTrue".Translate().CapitalizeFirst().ToString() : null, "\"" + d.text + "\"", UiKit.DreamColor));
                if (dreams.quotes.Count > 0) list.Add(dreams);
            }

            // Journal: the newest two entries.
            if (s.journals)
            {
                var j = comp.journals.For(p);
                if (j != null && j.entries.Count > 0)
                {
                    var journal = new Section { key = "journal", header = "CC_Story_Journal".Translate(), icon = Icons.Journal, journal = j, quotes = new List<(string, string, Color)>() };
                    foreach (var e in j.entries.AsEnumerable().Reverse().Take(2))
                        journal.quotes.Add((e.date, e.text, UiKit.Quote));
                    list.Add(journal);
                }
            }
            return list;
        }

        // ---------------- Drawing ----------------

        /// <summary>The overview's rows inside its card; with <paramref name="draw"/> off it only measures. Returns its height.</summary>
        private static float DrawOverview(Rect r, Overview o, bool draw)
        {
            const float pad = 8f, gap = 5f;
            float x = r.x + pad + 3f, w = r.width - pad * 2f - 3f, y = r.y + pad;
            Text.Font = GameFont.Small;
            if (o.words != null)
            {
                float h = Text.CalcHeight(o.words, w);
                if (draw)
                {
                    var lr = new Rect(x, y, w, h);
                    GUI.color = UiKit.Quote;
                    Widgets.Label(lr, o.words);
                    GUI.color = Color.white;
                    if (!o.wordsTip.NullOrEmpty()) TooltipHandler.TipRegion(lr, o.wordsTip);
                }
                y += h + gap;
            }
            if (o.standing.Count > 0) y += UiKit.Pills(x, y, w, o.standing, draw) + gap;
            if (o.goal != null)
            {
                if (draw) GoalRow(new Rect(x, y, w, RowH), o);
                y += RowH + gap;
            }
            if (o.story != null)
            {
                if (draw) StoryRow(new Rect(x, y, w, RowH), o);
                y += RowH + gap;
            }
            if (o.tags.Count > 0) y += UiKit.Pills(x, y, w, o.tags, draw) + gap;
            if (o.family.Count > 0 || o.familyRest > 0)
            {
                if (draw) FamilyRow(new Rect(x, y, w, 24f), o);
                y += 24f + gap;
            }
            return y - gap + pad - r.y;
        }

        /// <summary>"Wants to master cooking", with a bar for how far they've come.</summary>
        private static void GoalRow(Rect r, Overview o)
        {
            if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
            UiKit.Icon(new Rect(r.x, r.center.y - 8f, 16f, 16f), Icons.LifeGoal, UiKit.Gold);
            const float barW = 64f, pctW = 36f;
            float tx = r.x + 22f;
            UiKit.LabelFit(new Rect(tx, r.y, Mathf.Max(0f, r.xMax - tx - barW - pctW - 8f), r.height), o.goal, Color.white);
            UiKit.Bar(new Rect(r.xMax - barW - pctW, r.center.y - 3f, barW, 6f), o.goalProgress, UiKit.Gold);
            UiKit.LabelFit(new Rect(r.xMax - pctW, r.y, pctW, r.height), o.goalProgress.ToStringPercent(), Faint, GameFont.Tiny, TextAnchor.MiddleRight);
            if (!o.goalTip.NullOrEmpty()) TooltipHandler.TipRegion(r, o.goalTip);
        }

        /// <summary>"The long road: second thoughts", with a dot for each stage: those behind them, the one they're at, those to come.</summary>
        private static void StoryRow(Rect r, Overview o)
        {
            if (Mouse.IsOver(r)) Widgets.DrawHighlight(r);
            UiKit.Icon(new Rect(r.x, r.center.y - 8f, 16f, 16f), Icons.Journal, UiKit.Info);
            const float dot = 7f, dotGap = 4f;
            int n = o.stage >= 0 ? Mathf.Clamp(o.stages, 1, 10) : 0;
            float dotsW = n > 0 ? n * dot + (n - 1) * dotGap : 0f;
            float tx = r.x + 22f;
            UiKit.LabelFit(new Rect(tx, r.y, Mathf.Max(0f, r.xMax - tx - dotsW - 8f), r.height), o.story, Color.white);
            for (int i = 0; i < n; i++)
            {
                var d = new Rect(r.xMax - dotsW + i * (dot + dotGap), r.center.y - dot / 2f, dot, dot);
                UiKit.DiscFill(d, i < o.stage ? UiKit.WithAlpha(UiKit.Info, 0.85f) : i == o.stage ? UiKit.Gold : new Color(1f, 1f, 1f, 0.15f));
            }
            if (!o.storyTip.NullOrEmpty()) TooltipHandler.TipRegion(r, o.storyTip);
        }

        /// <summary>The family who are here as small portraits (click one for their page), then how many more, the whole tree in its tooltip.</summary>
        private static void FamilyRow(Rect r, Overview o)
        {
            UiKit.Icon(new Rect(r.x, r.center.y - 8f, 16f, 16f), Icons.Bonds, HeaderColor);
            float x = r.x + 22f;
            int rest = o.familyRest;
            // Room for "+3 more" at the end, so the portraits leave space for it.
            Text.Font = GameFont.Small;
            float moreW = rest > 0 || o.family.Count > 1 ? Text.CalcSize(o.familyMoreMost).x + 10f : 0f;
            for (int i = 0; i < o.family.Count; i++)
            {
                var (pawn, tip) = o.family[i];
                float room = r.xMax - x - moreW;
                if (room < 60f)
                {
                    rest += o.family.Count - i;
                    break;
                }
                bool open = pawn.IsColonist && !pawn.IsQuestLodger();
                string line = tip;
                x += UiKit.PawnChip(x, r.y, pawn, 24f, true, Mathf.Min(120f, room), null,
                    () => open ? line + "\n\n" + "CC_UI_OpenPage".Translate() : line, open) + 8f;
            }
            if (rest <= 0) return;
            // Some portraits found no room after all: the count says so (rarely, so worded here).
            string more = rest == o.familyRest ? o.familyMore : "CC_Story_FamilyMore".Translate(rest).Resolve();
            Text.Font = GameFont.Small;
            var mr = new Rect(x, r.y, Mathf.Min(Text.CalcSize(more).x + 8f, Mathf.Max(0f, r.xMax - x)), r.height);
            if (Mouse.IsOver(mr)) Widgets.DrawHighlight(mr);
            UiKit.LabelFit(mr, more, Faint);
            if (!o.familyTip.NullOrEmpty()) TooltipHandler.TipRegion(mr, o.familyTip);
        }

        /// <summary>The family tree, a row each: their face (or where they are, for those not on a map), name, what they are, age and where.</summary>
        private static float DrawFamily(Rect r, Section s)
        {
            float y = r.y;
            foreach (var (text, color) in s.lines) y += UiKit.Paragraph(r.x, y, r.width, text, color) + 4f;
            const float row = 26f;
            float nameW = Mathf.Min(110f, r.width * 0.27f), relW = Mathf.Min(120f, r.width * 0.29f), ageW = 28f;
            for (int i = 0; i < s.tree.Count; i++)
            {
                var e = s.tree[i];
                var rr = new Rect(r.x, y, r.width, row - 2f);
                UiKit.Zebra(rr, i);
                if (Mouse.IsOver(rr)) Widgets.DrawHighlight(rr);
                var color = WhereColor(e.whereKind);
                var face = new Rect(rr.x + 2f, rr.y + 1f, 22f, 22f);
                if (!e.pawn.Dead && e.pawn.Spawned) UiKit.Portrait(face, e.pawn, color);
                else UiKit.Badge(face, e.pawn.Dead ? Icons.Death : Icons.World, color);
                float x = face.xMax + 6f;
                UiKit.LabelFit(new Rect(x, rr.y, nameW, rr.height), e.pawn.LabelShort, e.pawn.Dead ? UiKit.Dim : Color.white);
                x += nameW + 4f;
                UiKit.LabelFit(new Rect(x, rr.y, relW, rr.height), e.relation.CapitalizeFirst(), Faint);
                x += relW + 4f;
                if (!e.pawn.Dead) UiKit.LabelFit(new Rect(x, rr.y, ageW, rr.height), FamilyYears.Lived(e.pawn).ToString(), Faint);
                x += ageW + 4f;
                UiKit.LabelFit(new Rect(x, rr.y, Mathf.Max(0f, rr.xMax - x - 4f), rr.height), e.where.CapitalizeFirst(), color);
                TooltipHandler.TipRegion(rr, e.line);
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
