using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// "Culture" in a colonist's inspect pane, just left of the game's Social tab: what a colonist believes and where they
    /// come from. Their faith (with Ideology: its icon and name, how firmly they hold it, a role, what it asks of them and
    /// what moved it lately), the views they hold, how they sit with the colony's ways, what they think of each decree in
    /// force, and their backstory, xenotype, the people they came from and how long they've been here. Only sections
    /// with something in them show. Fits the room above the inspect pane like the Story tab (RimfolkTab); each section
    /// folds away.
    /// </summary>
    public class ITab_Pawn_Culture : RimfolkTab
    {
        private Vector2 scroll;
        private float height = 400f;
        private Pawn shown;
        /// <summary>Which of the tab's sections are folded away this session.</summary>
        private static readonly Folds folds = new Folds();

        public ITab_Pawn_Culture()
        {
            size = new Vector2(460f, 520f);
            labelKey = "CC_Tab_Culture";
        }

        protected override float PreferredHeight => 520f;

        public override bool IsVisible
        {
            get
            {
                var p = SelPawn;
                return p != null && p.RaceProps.Humanlike && (p.IsColonist || p.IsPrisonerOfColony || p.IsSlaveOfColony) && CouncilGameComponent.Instance != null
                       && CultureView.AnythingFor(p);
            }
        }

        public override void Notify_ClearingAllMapsMemory()
        {
            base.Notify_ClearingAllMapsMemory();
            shown = null;
            CultureView.Reset();
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
            // Clear of the tab's close button in the top right corner.
            UiKit.LabelFit(new Rect(rect.x + 42f, rect.y + 1f, rect.width - 42f - 24f, 32f), p.LabelShortCap, Color.white, GameFont.Medium);
            Text.Font = GameFont.Small;
            CultureView.Draw(new Rect(rect.x, rect.y + 42f, rect.width, rect.height - 42f), p, ref scroll, ref height, folds);
        }
    }

    /// <summary>The Culture tab's sections for one colonist, built a couple of times a second (like the Story tab's).</summary>
    public static class CultureView
    {
        private static Pawn cachedPawn;
        private static float cachedAt = -1f;
        private static List<PaneSection> cached;
        private static string empty;

        /// <summary>Forget the last game's colonist (called when a game loads).</summary>
        public static void Reset()
        {
            cachedPawn = null;
            cached = null;
        }

        /// <summary>
        /// Whether the tab has anything for them, cheaply (the tab button asks every frame): a backstory, a faith or a
        /// xenotype is enough, and nearly everyone has one.
        /// </summary>
        public static bool AnythingFor(Pawn p)
        {
            if (p?.story == null) return false;
            if (p.story.Childhood != null || p.story.Adulthood != null) return true;
            if (IdeologyShown && p.ideo?.Ideo != null) return true;
            if (ModsConfig.BiotechActive && p.genes != null) return true;
            var r = Persona.On ? Persona.Of(p) : null;
            return r != null && r.stances.Count > 0;
        }

        /// <summary>Faiths are shown at all: Ideology, and not the classic mode that hides them.</summary>
        private static bool IdeologyShown => ModsConfig.IdeologyActive && Find.IdeoManager != null && !Find.IdeoManager.classicMode;

        public static void Draw(Rect outRect, Pawn p, ref Vector2 scroll, ref float height, Folds folds)
        {
            var comp = CouncilGameComponent.Instance;
            if (p == null || comp == null) return;
            if (cachedPawn != p || cached == null || Time.realtimeSinceStartup - cachedAt > 0.5f || Time.realtimeSinceStartup < cachedAt)
            {
                try
                {
                    cached = Build(comp, p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("culture tab", e);
                    cached = new List<PaneSection>();
                }
                empty = cached.Count == 0 ? "CC_Culture_Empty".Translate(p.LabelShort).Resolve() : null;
                cachedPawn = p;
                cachedAt = Time.realtimeSinceStartup;
            }
            PawnPane.Draw(outRect, cached, ref scroll, ref height, folds, empty, p);
        }

        private static List<PaneSection> Build(CouncilGameComponent comp, Pawn p)
        {
            var list = new List<PaneSection>();
            Add(list, Faith(p));
            if (Persona.On && Persona.Eligible(p))
            {
                var views = CharacterPage.Make("talk.views", p);
                if (views != null) list.Add(new PaneSection { key = "views", header = views.title, icon = Icons.Council, character = views });
            }
            Add(list, Ways(comp, p));
            Add(list, Decrees(comp, p));
            Add(list, Origin(comp, p));
            return list;
        }

        private static void Add(List<PaneSection> list, PaneSection s)
        {
            if (s != null && !s.Empty) list.Add(s);
        }

        // ---------------- Faith ----------------

        private static PaneSection Faith(Pawn p)
        {
            if (!IdeologyShown) return null;
            var ideo = p.ideo?.Ideo;
            if (ideo == null) return null;
            var s = new PaneSection { key = "faith", header = "CC_FaithP_PageTitle".Translate(), icon = Icons.CatIdeology };
            float certainty = p.ideo.Certainty;
            var role = FaithCharacter.SafeRole(ideo, p);
            string name = ideo.name.CapitalizeFirst();
            string culture = ideo.culture != null ? "CC_Culture_Culture".Translate(ideo.culture.LabelCap).Resolve() : null;
            string roleLabel = role != null ? role.LabelCap.ToString() : null;
            string state = FaithPeople.StateWord(certainty);
            string value = "CC_Culture_CertaintyValue".Translate(certainty.ToStringPercent(), state).Resolve();
            var color = FaithMath.Devout(certainty) ? UiKit.Good : FaithMath.Wavering(certainty) ? UiKit.Warn : UiKit.Info;
            string tip = FaithCharacter.CertaintyTip(p);
            string certaintyLabel = "CC_Culture_Certainty".Translate();
            string roleTip = null;
            if (role != null)
            {
                try
                {
                    roleTip = role.GetTip();
                }
                catch (Exception)
                {
                    roleTip = null;
                }
            }
            Texture2D roleIcon = null;
            try
            {
                roleIcon = role?.Icon;
            }
            catch (Exception)
            {
                roleIcon = null;
            }
            // The ideoligions they're drawn to, strongest first (Faith/FaithLeanings.cs).
            var leanings = FaithLeanings.Shown(p).Select(l => (l.ideo, l.pull,
                label: "CC_Leaning_Label".Translate(l.ideo.name).Resolve(),
                value: "CC_Leaning_Value".Translate(l.pull.ToStringPercent(), ("CC_Leaning_Trend_" + (l.trend > 0 ? "Up" : l.trend < 0 ? "Down" : "Same")).Translate()).Resolve(),
                tip: "CC_Leaning_Tip".Translate(p.Named("PAWN"), l.ideo.name.Named("FAITH")).Resolve())).ToList();
            s.custom = (x, y, w) =>
            {
                const float icon = 34f;
                IdeoUIUtility.DoIdeoIcon(new Rect(x, y + 1f, icon, icon), ideo);
                float tx = x + icon + 8f;
                UiKit.LabelFit(new Rect(tx, y, w - (tx - x), 22f), name, ideo.TextColor, GameFont.Small);
                float lx = tx;
                if (roleLabel != null)
                {
                    if (roleIcon != null && roleIcon != BaseContent.BadTex)
                    {
                        UiKit.Icon(new Rect(lx, y + 21f, 14f, 14f), roleIcon, ideo.Color);
                        lx += 18f;
                    }
                    var rr = new Rect(lx, y + 18f, Mathf.Min(w - (lx - x), 200f), 18f);
                    UiKit.LabelFit(rr, roleLabel, UiKit.Gold, GameFont.Tiny);
                    if (!roleTip.NullOrEmpty()) TooltipHandler.TipRegion(rr, roleTip);
                    Text.Font = GameFont.Tiny;
                    lx += Mathf.Min(Text.CalcSize(roleLabel).x + 10f, 200f);
                    Text.Font = GameFont.Small;
                }
                if (culture != null && x + w - lx > 40f) UiKit.LabelFit(new Rect(lx, y + 18f, x + w - lx, 18f), culture, UiKit.Faint, GameFont.Tiny);
                float my = y + icon + 6f;
                UiKit.Meter(new Rect(x, my, w, UiKit.MeterHeight), certainty, color, certaintyLabel, value, null, tip);
                my += UiKit.MeterHeight + 6f;
                foreach (var l in leanings)
                {
                    const float small = 18f;
                    IdeoUIUtility.DoIdeoIcon(new Rect(x, my + 1f, small, small), l.ideo);
                    UiKit.Meter(new Rect(x + small + 6f, my, w - small - 6f, UiKit.MeterHeight), l.pull, l.ideo.Color, l.label, l.value, null, l.tip);
                    my += UiKit.MeterHeight + 4f;
                }
                return my - y;
            };
            // Faiths they held before this one.
            try
            {
                var before = p.ideo.PreviousIdeos?.Where(i => i != null && i != ideo).Select(i => i.name).Distinct().Take(3).ToList();
                if (before != null && before.Count > 0)
                    s.Add("CC_Culture_FaithBefore".Translate(TextUtil.JoinAnd(before)).Resolve(), UiKit.Faint, "CC_Culture_FaithBeforeTip".Translate());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("culture tab: earlier faiths", e);
            }
            // What it asks of them, what moved it lately, who could sway them (Faith/FaithCharacter.cs).
            s.character = FaithCharacter.Details(p);
            return s;
        }

        // ---------------- The colony's ways ----------------

        private static PaneSection Ways(CouncilGameComponent comp, Pawn p)
        {
            var settings = ColonyCouncilMod.Settings;
            var ch = comp.character;
            if (!settings.colonyCharacter || ch == null || !p.IsColonist || p.IsQuestLodger() || !People.HasInnerLife(p) || p.DevelopmentalStage.Baby()) return null;
            string known = ch.Describe();
            var traits = ch.traits.Known.Select(d => d.label).ToList();
            if (known == null && traits.Count == 0) return null;
            var s = new PaneSection { key = "ways", header = "CC_Culture_Ways".Translate(), icon = Icons.Ways };
            var reasons = new List<string>();
            float approval = ch.Approval(p, reasons);
            string feeling = approval >= ThoughtWorker_ColonyWays.Strong ? "Proud" : approval >= ThoughtWorker_ColonyWays.Mild ? "Like"
                : approval <= -ThoughtWorker_ColonyWays.Strong ? "Ashamed" : approval <= -ThoughtWorker_ColonyWays.Mild ? "Uneasy" : "Even";
            var color = approval >= ThoughtWorker_ColonyWays.Mild ? UiKit.Good : approval <= -ThoughtWorker_ColonyWays.Mild ? UiKit.Bad : UiKit.Faint;
            string word = ("CC_Culture_WaysWord_" + feeling).Translate();
            string line = ("CC_Culture_WaysFeel_" + feeling).Translate(p.Named("PAWN")).Resolve();
            float fill = Mathf.Clamp(approval / 100f, -1f, 1f);
            string barLabel = "CC_Culture_WaysBar".Translate();
            string barTip = "CC_Culture_WaysTip".Translate(approval.ToString("0")).Resolve();
            s.custom = (x, y, w) =>
            {
                UiKit.LabelFit(new Rect(x, y, w * 0.6f, 18f), barLabel, UiKit.Faint, GameFont.Tiny);
                UiKit.LabelFit(new Rect(x + w * 0.4f, y, w * 0.6f, 18f), word, color, GameFont.Tiny, TextAnchor.MiddleRight);
                var bar = new Rect(x, y + 19f, w, 8f);
                Widgets.DrawBoxSolidWithOutline(bar, UiKit.MeterBack, new Color(1f, 1f, 1f, 0.1f), 1);
                float mid = bar.center.x;
                float to = mid + fill * (bar.width / 2f - 1f);
                if (Mathf.Abs(to - mid) >= 1f) Widgets.DrawBoxSolid(new Rect(Mathf.Min(mid, to), bar.y + 1f, Mathf.Abs(to - mid), bar.height - 2f), UiKit.WithAlpha(color, 0.75f));
                Widgets.DrawBoxSolid(new Rect(mid, bar.y - 2f, 1f, bar.height + 4f), new Color(1f, 1f, 1f, 0.3f));
                var area = new Rect(x, y, w, 30f);
                if (Mouse.IsOver(area)) Widgets.DrawHighlight(area);
                TooltipHandler.TipRegion(area, barTip);
                return 32f;
            };
            s.Add(line, color);
            foreach (var r in reasons) s.Add(r.CapitalizeFirst() + ".", UiKit.Faint);
            foreach (var d in ch.traits.PrideOf(p))
                s.Add("CC_Culture_TraitPride".Translate(d.label, d.skill?.label ?? "").Resolve(), UiKit.Good, d.description);
            var all = new List<string>();
            if (known != null) all.Add(known);
            all.AddRange(traits);
            var knownRow = s.Add("CC_Ways_KnownAs".Translate(TextUtil.JoinAnd(all)).Resolve() + ".", UiKit.Faint, "CC_Culture_KnownTip".Translate());
            if (knownRow != null)
            {
                knownRow.icon = Icons.Ways;
                knownRow.accent = UiKit.Faint;
            }
            return s;
        }

        // ---------------- Decrees ----------------

        private static PaneSection Decrees(CouncilGameComponent comp, Pawn p)
        {
            var decrees = comp.decrees;
            if (decrees == null || !p.IsFreeNonSlaveColonist || p.DevelopmentalStage.Baby()) return null;
            var s = new PaneSection { key = "decrees", header = "CC_Culture_Decrees".Translate(), icon = Icons.Decrees };
            int now = Find.TickManager.TicksGame;
            foreach (var a in decrees.active)
            {
                if (a?.def == null || a.def.Hidden) continue;
                var r = a.def.ReactionOf(p);
                var color = UiKit.ReactionColor(r);
                string reaction = r == DecreeReaction.Neutral ? "CC_Culture_Reaction_Neutral".Translate().ToString() : ("CC_UI_Reaction_" + r).Translate().ToString();
                var row = s.Add(a.def.LabelCap, Color.white, DecreeTip(a, r, p, now));
                if (row == null) continue;
                row.icon = Icons.Decree;
                row.accent = color;
                row.right = reaction;
            }
            foreach (var req in decrees.requests.Where(q => q?.def != null && q.pawn == p && !q.def.Hidden))
            {
                string text = (req.repeal ? "CC_Culture_PetitionRepeal" : "CC_Culture_Petition").Translate(req.def.label).Resolve();
                var row = s.Add(text, UiKit.Info, req.line.NullOrEmpty() ? null : "\"" + req.line + "\"");
                if (row == null) continue;
                row.icon = Icons.Promise;
                row.accent = UiKit.Info;
            }
            if (s.rows.Count > 0 && decrees.active.Count > 0) s.right = decrees.active.Count(a => a?.def != null && !a.def.Hidden).ToString();
            return s;
        }

        private static string DecreeTip(ActiveDecree a, DecreeReaction r, Pawn p, int now)
        {
            var parts = new List<string>();
            int stage = r == DecreeReaction.Hate ? 0 : r == DecreeReaction.Dislike ? 1 : r == DecreeReaction.Like ? 2 : r == DecreeReaction.Love ? 3 : -1;
            var stages = a.def.thought?.stages;
            if (stage >= 0 && stages != null && stage < stages.Count && stages[stage] != null)
            {
                var st = stages[stage];
                if (!st.label.NullOrEmpty()) parts.Add(st.label.CapitalizeFirst());
                if (!st.description.NullOrEmpty()) parts.Add("\"" + st.description.Formatted(p.Named("PAWN")).Resolve() + "\"");
            }
            else parts.Add("CC_Culture_DecreeNeutral".Translate(p.Named("PAWN")).Resolve());
            if (a.endTick > now) parts.Add("CC_Culture_DecreeEnds".Translate(TextUtil.DaysString(a.endTick - now)).Resolve());
            if (!a.def.description.NullOrEmpty()) parts.Add(a.def.description);
            return string.Join("\n\n", parts);
        }

        // ---------------- Where they come from ----------------

        private static PaneSection Origin(CouncilGameComponent comp, Pawn p)
        {
            var s = new PaneSection { key = "origin", header = "CC_Culture_Origin".Translate(), icon = Icons.World };
            var story = p.story;
            if (story?.Childhood != null) s.Add("CC_Culture_Childhood".Translate(story.Childhood.TitleCapFor(p.gender)).Resolve(), Color.white, Backstory(story.Childhood, p));
            if (story?.Adulthood != null) s.Add("CC_Culture_Adulthood".Translate(story.Adulthood.TitleCapFor(p.gender)).Resolve(), Color.white, Backstory(story.Adulthood, p));
            if (ModsConfig.BiotechActive && p.genes != null)
            {
                try
                {
                    string label = p.genes.XenotypeLabelCap;
                    if (!label.NullOrEmpty())
                    {
                        var row = s.Add(label, Color.white, p.genes.XenotypeDescShort);
                        if (row != null)
                        {
                            row.icon = p.genes.XenotypeIcon;
                            row.accent = UiKit.WithAlpha(Color.white, 0.85f);
                        }
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("culture tab: xenotype", e);
                }
            }
            PeopleOf(s, p);
            Joined(comp, s, p);
            return s;
        }

        private static string Backstory(BackstoryDef b, Pawn p)
        {
            try
            {
                return b.FullDescriptionFor(p).Resolve();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The people they belong to (a prisoner's own), or the kind of people the game made them among.</summary>
        private static void PeopleOf(PaneSection s, Pawn p)
        {
            var f = p.Faction;
            if (p.IsPrisonerOfColony && f != null && !f.IsPlayer)
            {
                var row = s.Add("CC_Culture_OfFaction".Translate(f.Name).Resolve(), Color.white, "CC_Culture_OfFactionTip".Translate(p.Named("PAWN"), f.Name.Named("FACTION")).Resolve());
                if (row != null)
                {
                    row.icon = FactionIcon(f.def);
                    try
                    {
                        row.accent = f.Color;
                    }
                    catch (Exception)
                    {
                        row.accent = UiKit.Neutral;
                    }
                }
                return;
            }
            var home = p.kindDef?.defaultFactionDef;
            if (home == null || home.isPlayer || home.pawnsPlural.NullOrEmpty()) return;
            var r = s.Add("CC_Culture_AmongPeople".Translate(home.pawnsPlural).Resolve(), UiKit.Faint,
                "CC_Culture_AmongPeopleTip".Translate(p.Named("PAWN"), (p.kindDef.label ?? "").Named("KIND"), (home.label ?? "").Named("FACTION")).Resolve());
            if (r != null)
            {
                r.icon = FactionIcon(home);
                r.accent = UiKit.Faint;
            }
        }

        private static Texture2D FactionIcon(FactionDef def)
        {
            try
            {
                var t = def?.FactionIcon;
                return t == BaseContent.BadTex ? null : t;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How long they've been here, and how they came: a founder, born here, or won over while held here.</summary>
        private static void Joined(CouncilGameComponent comp, PaneSection s, Pawn p)
        {
            if (!p.IsColonist || p.records == null) return;
            int here = p.records.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal);
            long age = p.ageTracker?.AgeBiologicalTicks ?? long.MaxValue;
            string how = null;
            if (comp.epithets != null && comp.epithets.IsFounder(p)) how = "CC_Culture_Founder".Translate(p.Named("PAWN")).Resolve();
            else if (here > 0 && age < long.MaxValue && here >= age - GenDate.TicksPerDay * 2) how = "CC_Culture_BornHere".Translate(p.Named("PAWN")).Resolve();
            else
            {
                string by = RecruitedBy(p);
                if (by != null) how = "CC_Culture_WonOver".Translate(p.Named("PAWN"), by.Named("BY")).Resolve();
            }
            if (here >= GenDate.TicksPerHour)
            {
                string time = here.ToStringTicksToPeriod(false, false, false);
                var row = s.Add("CC_Culture_Here".Translate(time).Resolve() + (how != null ? " " + how : ""), Color.white);
                if (row != null)
                {
                    row.icon = Icons.Colony;
                    row.accent = UiKit.Faint;
                }
            }
            else if (how != null) s.Add(how, Color.white);
        }

        /// <summary>Who won them over while they were held here, if the game's tales still remember it.</summary>
        private static string RecruitedBy(Pawn p)
        {
            try
            {
                var tales = Find.TaleManager?.AllTalesListForReading;
                if (tales == null) return null;
                for (int i = tales.Count - 1; i >= 0; i--)
                {
                    if (!(tales[i] is Tale_DoublePawn t) || t.def != TaleDefOf.Recruited || t.secondPawnData?.pawn != p) continue;
                    var by = t.firstPawnData;
                    return by?.pawn?.LabelShort ?? by?.name?.ToStringShort;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("culture tab: who won them over", e);
            }
            return null;
        }
    }
}
