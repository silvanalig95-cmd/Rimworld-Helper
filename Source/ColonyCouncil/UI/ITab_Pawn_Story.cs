using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Story" in a colonist's inspect pane: everything Rimfolk knows about them on one page. Their life goal,
    /// bonds and circle, quirks, names, office, trust, vendettas, secrets (only what the colony knows), recent
    /// dreams and the latest lines of their journal. Fits the room above the inspect pane (RimfolkTab); the name stays
    /// put, the rest scrolls, and each section folds away.
    /// </summary>
    public class ITab_Pawn_Story : RimfolkTab
    {
        private Vector2 scroll;
        private float height = 400f;
        private Pawn shown;
        /// <summary>Which of the tab's sections are folded away this session.</summary>
        private static readonly Folds folds = new Folds();

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
            UiKit.Portrait(new Rect(rect.x, rect.y, 34f, 34f), p, p.IsColonist ? UiKit.TrustColor(comp.standings.TrustOf(p)) : UiKit.Neutral);
            // The button keeps clear of the tab's close button in the top right corner.
            UiKit.LabelFit(new Rect(rect.x + 42f, rect.y + 1f, rect.width - 42f - 144f, 32f), p.LabelShortCap, Color.white, GameFont.Medium);
            if (p.IsColonist && Widgets.ButtonText(new Rect(rect.xMax - 138f, rect.y + 4f, 124f, 26f), "CC_Story_OpenCouncil".Translate()))
                MainTabWindow_Council.OpenPerson(p);
            StoryView.Draw(new Rect(rect.x, rect.y + 42f, rect.width, rect.height - 42f), p, ref scroll, ref height, folds);
        }
    }

    /// <summary>
    /// The story of one colonist, drawn in the Story tab and the council window's People roster. Each place passes its
    /// own folds, so a section folded in the tab stays open in the roster.
    /// </summary>
    public static class StoryView
    {
        private static readonly Color Faint = new Color(0.7f, 0.7f, 0.7f);
        private static readonly Color HeaderColor = new Color(0.95f, 0.85f, 0.6f);
        private static readonly Color Quote = new Color(1f, 0.93f, 0.75f);

        // Built a couple of times a second rather than every frame.
        private static Pawn cachedPawn;
        private static float cachedAt = -1f;
        private static List<Section> cached;

        /// <summary>Forget the last game's colonist (called when a game loads).</summary>
        public static void Reset()
        {
            cachedPawn = null;
            cached = null;
        }

        public static void Draw(Rect outRect, Pawn p, ref Vector2 scroll, ref float height, Folds folds = null)
        {
            var comp = CouncilGameComponent.Instance;
            if (p == null || comp == null) return;
            if (cachedPawn != p || cached == null || Time.realtimeSinceStartup - cachedAt > 0.5f)
            {
                cached = Sections(comp, p).ToList();
                cachedPawn = p;
                cachedAt = Time.realtimeSinceStartup;
            }
            var view = new Rect(0f, 0f, outRect.width - 16f, height);
            Widgets.BeginScrollView(outRect, ref scroll, view);
            float y = 0f;
            if (cached.Count == 0)
            {
                GUI.color = Faint;
                Widgets.Label(new Rect(0f, y, view.width, 60f), "CC_Story_Empty".Translate(p.LabelShort));
                GUI.color = Color.white;
                y += 60f;
            }
            foreach (var section in cached)
            {
                y += UiKit.FoldTitle(0f, y, view.width, section.header, folds, section.key, out bool open, section.icon, HeaderColor) - 4f;
                if (!open)
                {
                    y += 6f;
                    continue;
                }
                foreach (var (text, color) in section.lines)
                {
                    float h = Text.CalcHeight(text, view.width - 8f);
                    GUI.color = color;
                    Widgets.Label(new Rect(8f, y, view.width - 8f, h), text);
                    GUI.color = Color.white;
                    y += h + 2f;
                }
                if (section.journal != null && Widgets.ButtonText(new Rect(view.width - 160f, y, 160f, 26f), "CC_Story_ReadJournal".Translate()))
                    Find.WindowStack.Add(new Dialog_Journal(section.journal));
                if (section.journal != null) y += 30f;
                y += 8f;
            }
            height = Mathf.Max(y, 100f);
            Widgets.EndScrollView();
        }

        private class Section
        {
            /// <summary>Names the section in the folds, the same in every language.</summary>
            public string key;
            public string header;
            public Texture2D icon;
            public List<(string, Color)> lines = new List<(string, Color)>();
            public PawnJournal journal;
        }

        private static IEnumerable<Section> Sections(CouncilGameComponent comp, Pawn p)
        {
            var s = ColonyCouncilMod.Settings;

            // Standing: trust, office, names.
            var standing = new Section { key = "standing", header = "CC_Story_Standing".Translate(), icon = Icons.Trust };
            var st = comp.standings.For(p, false);
            if (st != null) standing.lines.Add(("CC_Story_Trust".Translate(("CC_Trust_" + PawnStanding.LevelFor(st.trust)).Translate()).Resolve(), Color.white));
            var office = comp.offices.OfficeOf(p);
            if (office != null) standing.lines.Add(("CC_Office_Holds".Translate(p.LabelShort, office.label).Resolve(), Color.white));
            string titles = comp.epithets.TitlesOf(p);
            if (!titles.NullOrEmpty()) standing.lines.Add(("CC_Epithet_Known".Translate(p.LabelShort, titles).Resolve(), Color.white));
            if (standing.lines.Count > 0) yield return standing;

            // Life goal.
            if (s.aspirations)
            {
                var goal = new Section { key = "goal", header = "CC_Social_Goals".Translate(), icon = Icons.LifeGoal };
                var a = comp.aspirations.Active(p);
                if (a != null)
                {
                    string progress;
                    try { progress = a.def.Worker.ProgressText(a); }
                    catch { progress = a.Progress.ToStringPercent(); }
                    goal.lines.Add(("CC_Story_Goal".Translate(a.Goal, progress).Resolve(), Color.white));
                    string why = a.SparkText;
                    if (!why.NullOrEmpty()) goal.lines.Add(("CC_Story_GoalWhy".Translate(why, "CC_DayN".Translate(a.startedTick / GenDate.TicksPerDay + 1)).Resolve(), Faint));
                }
                foreach (var done in comp.aspirations.Fulfilled(p))
                    goal.lines.Add(("CC_Story_GoalDone".Translate(done.Goal, "CC_DayN".Translate(done.doneTick / GenDate.TicksPerDay + 1)).Resolve(), new Color(0.6f, 0.9f, 0.6f)));
                if (goal.lines.Count > 0) yield return goal;
            }

            // Personal story (Arcs/).
            var arcLines = ArcView.Lines(p);
            if (arcLines.Count > 0)
            {
                var arc = new Section { key = "arc", header = "CC_Story_Arc".Translate(), icon = Icons.Journal };
                arc.lines.AddRange(arcLines);
                yield return arc;
            }

            // Bonds.
            if (s.socialWeb)
            {
                var bonds = new Section { key = "bonds", header = "CC_Story_Bonds".Translate(), icon = Icons.Bonds };
                string describe = comp.social.Describe(p);
                if (!describe.NullOrEmpty()) bonds.lines.Add((describe, Color.white));
                foreach (var l in comp.social.lost.Where(l => l.survivor == p).Take(5))
                    bonds.lines.Add(("CC_Story_Lost".Translate(l.name, ("CC_Tie_" + l.kind).Translate()).Resolve(), Faint));
                if (bonds.lines.Count > 0) yield return bonds;
            }

            // Quirks, vendettas, secrets.
            var heart = new Section { key = "heart", header = "CC_Story_Heart".Translate(), icon = Icons.Heart };
            if (s.quirks)
                foreach (var q in comp.quirks.Of(p))
                    heart.lines.Add(("CC_Story_Quirk".Translate(q.Label).Resolve(), Color.white));
            if (s.heirlooms)
                foreach (var h in comp.legacy.heirlooms.Where(h => h.heir == p && h.thing != null && !h.thing.Destroyed))
                    heart.lines.Add(("CC_Story_Heirloom".Translate(h.ofName, h.thing.def.label).Resolve(), Color.white));
            if (s.vendettas)
                foreach (var v in comp.vendettas.vendettas.Where(v => v.avenger == p && v.faction != null))
                    heart.lines.Add(((v.settled ? "CC_Social_VendettaSettled" : v.kidnapped ? "CC_Social_VendettaTaken" : "CC_Social_Vendetta")
                        .Translate(p.LabelShort, v.faction.Name, v.victim, v.kills, Vendetta.KillsToSettle).Resolve(), v.settled ? Faint : new Color(0.9f, 0.55f, 0.45f)));
            if (s.nemeses && comp.nemeses != null)
                foreach (var n in comp.nemeses.Of(p).OrderBy(n => n.ended))
                    heart.lines.Add(("CC_Story_Nemesis".Translate(n.Line).Resolve(), n.ended ? Faint : new Color(0.9f, 0.5f, 0.4f)));
            if (s.secrets)
            {
                var secret = comp.secrets.Of(p);
                if (secret != null && secret.revealed) heart.lines.Add((secret.Truth, new Color(0.9f, 0.6f, 0.5f)));
                else if (secret != null && secret.knowers.Count > 0)
                    heart.lines.Add(("CC_Social_SecretKnown".Translate(p.LabelShort, TextUtil.JoinAnd(secret.knowers.Select(k => k.LabelShort).ToList())).Resolve(), Faint));
                foreach (var other in comp.secrets.secrets.Where(x => x.owner != p && !x.revealed && x.knowers.Contains(p) && x.owner != null))
                    heart.lines.Add(("CC_Story_Keeps".Translate(other.owner.LabelShort).Resolve(), Faint));
            }
            if (heart.lines.Count > 0) yield return heart;

            // Dreams.
            if (s.dreams)
            {
                var dreams = new Section { key = "dreams", header = "CC_Social_Dreams".Translate(), icon = Icons.Dream };
                foreach (var d in comp.dreams.recent.Where(d => d.pawn == p).Reverse().Take(3))
                    dreams.lines.Add(("\"" + d.text + "\"" + (d.cameTrue ? "  (" + "CC_Social_DreamCameTrue".Translate() + ")" : ""), Quote));
                if (dreams.lines.Count > 0) yield return dreams;
            }

            // Journal.
            if (s.journals)
            {
                var j = comp.journals.For(p);
                if (j != null && j.entries.Count > 0)
                {
                    var journal = new Section { key = "journal", header = "CC_Story_Journal".Translate(), icon = Icons.Journal, journal = j };
                    foreach (var e in j.entries.AsEnumerable().Reverse().Take(4))
                        journal.lines.Add((e.date + ": " + e.text, Quote));
                    yield return journal;
                }
            }
        }
    }
}
