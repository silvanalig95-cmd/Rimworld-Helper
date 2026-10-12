using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The heart in words and on screen: band names, what built strain and what would help, feelings with their causes,
    /// the heart's needs; the inspector line, RimHUD's lines, the Heart section of the Mood tab and the strain list on
    /// the colony's Moods page. Text is built when the heart changes (hourly) or at most once a second, never per frame.
    /// </summary>
    public static class HeartView
    {
        public static readonly Color SteadyColor = new Color(0.55f, 0.75f, 0.6f);
        public static readonly Color StrainedColor = new Color(0.95f, 0.8f, 0.35f);
        public static readonly Color WornColor = new Color(0.95f, 0.58f, 0.25f);
        public static readonly Color BreakingColor = new Color(0.92f, 0.3f, 0.3f);

        public static Color BandColor(StrainBand b)
        {
            switch (b)
            {
                case StrainBand.Strained: return StrainedColor;
                case StrainBand.Worn: return WornColor;
                case StrainBand.Breaking: return BreakingColor;
                default: return SteadyColor;
            }
        }

        public static string BandLabel(StrainBand b) => ("CC_Heart_Band_" + b).Translate();

        public static string BandDesc(StrainBand b) => ("CC_Heart_Band_" + b + "_Desc").Translate();

        public static string SourceLabel(StrainSource s) => ("CC_Heart_Source_" + s).Translate();

        public static string ReliefLabel(StrainRelief r) => ("CC_Heart_Relief_" + r).Translate();

        public static string NeedLabel(HeartNeed n) => ("CC_Heart_Need_" + n).Translate();

        public static string IntensityWord(float intensity) =>
            (intensity >= 18f ? "CC_Heart_Intensity_Strong" : intensity >= 10f ? "CC_Heart_Intensity_Clear" : "CC_Heart_Intensity_Mild").Translate();

        // ---------------- Lines of text ----------------

        private static readonly List<FeelingNow> dominantTmp = new List<FeelingNow>(2);

        /// <summary>"Strain: worn · restless, proud" (built hourly and kept on the heart).</summary>
        public static string InspectLine(PawnHeart h)
        {
            Heart.Dominant(h, dominantTmp);
            string band = BandLabel(h.Band);
            if (dominantTmp.Count == 0) return "CC_Heart_Inspect".Translate(band).Resolve();
            string feelings = string.Join(", ", dominantTmp.Select(f => f.def.Adjective));
            dominantTmp.Clear();
            return "CC_Heart_InspectFeelings".Translate(band, feelings).Resolve();
        }

        /// <summary>The heart changed: its inspector line and RimHUD values are built again (hourly, not per frame).</summary>
        public static void Refresh(Pawn p, PawnHeart h)
        {
            h.inspectLine = InspectLine(h);
            h.hudStrain = "CC_Heart_StrainValue".Translate(BandLabel(h.Band), h.strain.ToString("0")).Resolve();
            string feelings = DominantText(h);
            h.hudFeelings = feelings.NullOrEmpty() ? null : feelings.CapitalizeFirst();
            var lack = Heart.LowestNeed(h, out float v);
            h.hudNeeds = h.needs == null ? null
                : lack == null ? "CC_Hud_NeedsMet".Translate().ToString() : "CC_Hud_NeedsLacking".Translate(NeedLabel(lack.need), v.ToString("0")).Resolve();
            Dirty(p);
        }

        /// <summary>The dominant feelings as adjectives ("restless, proud"), or null.</summary>
        public static string DominantText(PawnHeart h)
        {
            Heart.Dominant(h, dominantTmp);
            string s = dominantTmp.Count == 0 ? null : string.Join(", ", dominantTmp.Select(f => f.def.Adjective));
            dominantTmp.Clear();
            return s;
        }

        /// <summary>What built strain lately, biggest first: "losses, fighting and bad nights".</summary>
        public static string SourcesText(PawnHeart h, int n)
        {
            var top = TopSources(h, n);
            return top.Count == 0 ? null : TextUtil.JoinAnd(top.Select(s => SourceLabel(s.source)).ToList());
        }

        /// <summary>The same with amounts: "losses (+18), fighting (+9)".</summary>
        public static string SourcesDetailed(PawnHeart h, int n)
        {
            var top = TopSources(h, n);
            return top.Count == 0 ? null : string.Join(", ", top.Select(s => SourceLabel(s.source) + " (+" + s.amount.ToString("0") + ")"));
        }

        public static List<(StrainSource source, float amount)> TopSources(PawnHeart h, int n)
        {
            var list = new List<(StrainSource, float)>();
            for (int i = 0; i < h.recentBuild.Count && i < HeartMath.SourceCount; i++)
                if (h.recentBuild[i] >= 1f) list.Add(((StrainSource)i, h.recentBuild[i]));
            list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            if (list.Count > n) list.RemoveRange(n, list.Count - n);
            return list;
        }

        /// <summary>What eased it lately: "sleep (-9), a party (-6)".</summary>
        public static string ReliefDetailed(PawnHeart h, int n)
        {
            var list = new List<(StrainRelief r, float amount)>();
            for (int i = 0; i < h.recentRelief.Count && i < HeartMath.ReliefCount; i++)
                if (h.recentRelief[i] >= 1f) list.Add(((StrainRelief)i, h.recentRelief[i]));
            if (list.Count == 0) return null;
            list.Sort((a, b) => b.amount.CompareTo(a.amount));
            return string.Join(", ", list.Take(n).Select(x => ReliefLabel(x.r) + " (-" + x.amount.ToString("0") + ")"));
        }

        private static string HelpFor(StrainSource s)
        {
            switch (s)
            {
                case StrainSource.Combat:
                case StrainSource.Fear: return "CC_Heart_Help_Peace";
                case StrainSource.Pain:
                case StrainSource.Downed: return "CC_Heart_Help_Healing";
                case StrainSource.Loss:
                case StrainSource.Grief: return "CC_Heart_Help_Grieve";
                case StrainSource.Exhaustion: return "CC_Heart_Help_Schedule";
                case StrainSource.Joyless: return "CC_Heart_Help_Recreation";
                case StrainSource.BadSleep: return "CC_Heart_Help_Bedroom";
                case StrainSource.Dark: return "CC_Heart_Help_Light";
                case StrainSource.Hunger: return "CC_Heart_Help_Meals";
                case StrainSource.LowMood: return "CC_Heart_Help_Mood";
                case StrainSource.Council: return "CC_Heart_Help_Decree";
                case StrainSource.Dread: return "CC_Heart_Help_Prepare";
                case StrainSource.Despair: return "CC_Heart_Help_Friends";
                default: return "CC_Heart_Help_Sleep";
            }
        }

        /// <summary>What would help, from what built it: "a few days without fighting, time to grieve and quiet nights in a warm bed".</summary>
        public static string HelpText(Pawn p, PawnHeart h, int n)
        {
            var keys = new List<string>();
            foreach (var (source, _) in TopSources(h, 3))
            {
                string k = HelpFor(source);
                if (!keys.Contains(k)) keys.Add(k);
            }
            foreach (var k in new[] { "CC_Heart_Help_Sleep", "CC_Heart_Help_Party", "CC_Heart_Help_Friends" })
                if (!keys.Contains(k)) keys.Add(k);
            var rest = Heart.RestDecree;
            var comp = CouncilGameComponent.Instance;
            if (rest != null && comp != null && h.strain >= 30f && !comp.decrees.IsActive(rest) && comp.decrees.CannotEnact(rest) == null)
                keys.Insert(Mathf.Min(2, keys.Count), "CC_Heart_Help_RestDecree");
            return TextUtil.JoinAnd(keys.Take(n).Select(k => k.Translate().ToString()).ToList());
        }

        /// <summary>"For 2 days", "for 5 hours", "since just now".</summary>
        public static string SinceText(int since)
        {
            int ago = Find.TickManager.TicksGame - since;
            if (ago < GenDate.TicksPerHour) return "CC_Heart_SinceNow".Translate();
            return "CC_Heart_Since".Translate(TextUtil.DaysString(ago)).Resolve();
        }

        public static string FeelingCauses(FeelingNow f) => f.causes.Count == 0 ? null : string.Join(", ", f.causes);

        /// <summary>One feeling in full: name, how strong and since when, what fed it, what it is and what it does.</summary>
        public static string FeelingTip(FeelingNow f)
        {
            var sb = new StringBuilder();
            sb.Append(f.def.Label.CapitalizeFirst().Colorize(f.def.color)).Append("  ·  ").Append(IntensityWord(f.intensity)).Append(", ").Append(SinceText(f.since));
            string causes = FeelingCauses(f);
            if (causes != null) sb.Append("\n").Append("CC_Heart_FedBy".Translate(causes).Resolve());
            if (!f.def.Explanation.NullOrEmpty()) sb.Append("\n\n").Append(f.def.Explanation);
            if (!f.def.Does.NullOrEmpty()) sb.Append("\n\n").Append(f.def.Does.Colorize(ColoredText.SubtleGrayColor));
            return sb.ToString();
        }

        /// <summary>The need they lack most, or that all four are met: "Missing most: safety (raided lately)."</summary>
        public static string NeedsSummary(PawnHeart h)
        {
            if (h.needs == null || h.needs.Length != 4) return null;
            int low = 0;
            for (int i = 1; i < 4; i++)
                if (h.needs[i].value < h.needs[low].value) low = i;
            if (h.needs.All(n => n.value >= 60f)) return "CC_Heart_NeedsAllMet".Translate();
            var need = h.needs[low];
            if (need.value >= 45f) return "CC_Heart_NeedsFine".Translate();
            string label = NeedLabel((HeartNeed)low);
            return need.why.NullOrEmpty() ? "CC_Heart_NeedsMissing".Translate(label).Resolve() : "CC_Heart_NeedsMissingWhy".Translate(label, need.why).Resolve();
        }

        public static string NeedTip(PawnHeart h, HeartNeed n)
        {
            var v = h.needs[(int)n];
            string tip = NeedLabel(n).CapitalizeFirst() + ": " + v.value.ToString("0") + "\n\n" + ("CC_Heart_Need_" + n + "_Desc").Translate();
            if (!v.why.NullOrEmpty()) tip += "\n\n" + "CC_Heart_NeedMostly".Translate(v.why).Resolve();
            return tip;
        }

        public static string StrainTip(Pawn p, PawnHeart h)
        {
            var sb = new StringBuilder();
            sb.Append("CC_Heart_StrainTipHead".Translate(BandLabel(h.Band), h.strain.ToString("0")).Resolve());
            sb.Append("\n").Append(BandDesc(h.Band));
            string built = SourcesDetailed(h, 4);
            sb.Append("\n\n").Append(built == null ? "CC_Heart_NothingBuilt".Translate().ToString() : "CC_Heart_BuiltBy".Translate(built).Resolve());
            string eased = ReliefDetailed(h, 3);
            if (eased != null) sb.Append("\n").Append("CC_Heart_EasedBy".Translate(eased).Resolve());
            if (h.strain >= 30f)
            {
                string help = HelpText(p, h, 3);
                if (!help.NullOrEmpty()) sb.Append("\n\n").Append("CC_Heart_WouldHelp".Translate(help).Resolve());
            }
            sb.Append("\n\n").Append("CC_Heart_StrainExplain".Translate().ToString().Colorize(ColoredText.SubtleGrayColor));
            return sb.ToString();
        }

        public static string FeelingsTip(PawnHeart h)
        {
            if (h.feelings.Count == 0) return "CC_Heart_NoFeelings".Translate();
            return string.Join("\n\n", h.feelings.Select(FeelingTip));
        }

        public static string NeedsTip(PawnHeart h)
        {
            if (h.needs == null || h.needs.Length != 4) return null;
            var sb = new StringBuilder();
            for (int i = 0; i < 4; i++)
            {
                var n = h.needs[i];
                sb.Append(NeedLabel((HeartNeed)i).CapitalizeFirst()).Append(": ").Append(n.value.ToString("0"));
                if (!n.why.NullOrEmpty()) sb.Append("  (").Append(n.why).Append(")");
                sb.Append("\n");
            }
            sb.Append("\n").Append(NeedsSummary(h)).Append("\n\n").Append("CC_Heart_NeedsExplain".Translate().ToString().Colorize(ColoredText.SubtleGrayColor));
            return sb.ToString();
        }

        // ---------------- Caches ----------------

        private class PawnModel
        {
            public float builtAt = -1f;
            public int version;
            public string strainLabel, strainValue, strainTip, bandDesc, built, help, needsSummary;
            public Color bandColor;
            public float fill;
            public List<(string label, string value, string causes, string tip, Color color, float share)> feelings = new List<(string, string, string, string, Color, float)>();
            public (string label, string value, string why, string tip, float fill)[] needs;
        }

        private static readonly Dictionary<Pawn, PawnModel> models = new Dictionary<Pawn, PawnModel>();
        private static readonly Dictionary<Pawn, int> versions = new Dictionary<Pawn, int>();

        public static void Reset()
        {
            models.Clear();
            versions.Clear();
        }

        /// <summary>The heart changed: the next draw builds its text again.</summary>
        public static void Dirty(Pawn p)
        {
            if (p == null) return;
            versions[p] = (versions.TryGetValue(p, out int v) ? v : 0) + 1;
            if (versions.Count > 400) versions.Clear();
        }

        private static PawnModel ModelFor(Pawn p, PawnHeart h)
        {
            float now = Time.realtimeSinceStartup;
            int version = versions.TryGetValue(p, out int v) ? v : 0;
            if (models.TryGetValue(p, out var m) && m.version == version && now - m.builtAt < 1f) return m;
            if (models.Count > 60) models.Clear();
            m = new PawnModel { builtAt = now, version = version };
            models[p] = m;
            m.bandColor = BandColor(h.Band);
            m.fill = h.strain / HeartMath.Max;
            m.strainLabel = "CC_Heart_StrainLabel".Translate();
            m.strainValue = "CC_Heart_StrainValue".Translate(BandLabel(h.Band), h.strain.ToString("0")).Resolve();
            m.strainTip = StrainTip(p, h);
            m.bandDesc = BandDesc(h.Band);
            string built = SourcesDetailed(h, 3);
            m.built = built == null ? "CC_Heart_NothingBuilt".Translate().ToString() : "CC_Heart_BuiltBy".Translate(built).Resolve();
            m.help = h.strain >= 30f ? "CC_Heart_WouldHelp".Translate(HelpText(p, h, 3)).Resolve() : null;
            foreach (var f in h.feelings)
            {
                string label = f.def.Label.CapitalizeFirst() + "  ·  " + SinceText(f.since);
                string causes = FeelingCauses(f);
                m.feelings.Add((label, IntensityWord(f.intensity), causes, FeelingTip(f), f.def.color, f.Share));
            }
            if (h.needs != null && h.needs.Length == 4)
            {
                m.needs = new (string, string, string, string, float)[4];
                for (int i = 0; i < 4; i++)
                {
                    var n = h.needs[i];
                    m.needs[i] = (NeedLabel((HeartNeed)i).CapitalizeFirst(), n.value.ToString("0"), n.why, NeedTip(h, (HeartNeed)i), n.value / 100f);
                }
                m.needsSummary = NeedsSummary(h);
            }
            return m;
        }

        // ---------------- The Mood tab's Heart section ----------------

        private static Color NeedColor(float fill) => fill >= 0.6f ? UiKit.Good : fill >= 0.45f ? UiKit.Info : fill >= 0.3f ? UiKit.Warn : UiKit.Bad;

        /// <summary>Draws the Heart section at y; returns the new y. Nothing when the feature is off or they have no heart.</summary>
        public static float DrawSection(Pawn p, float y, float w)
        {
            var h = Heart.Of(p);
            if (h == null) return y;
            if (!h.evaluated)
            {
                y += UiKit.SectionTitle(0f, y, w, "CC_Heart_Section".Translate(), Icons.Heart, UiKit.HeaderColor);
                return y + UiKit.Paragraph(0f, y, w, "CC_Heart_NotYet".Translate(), MoodView.Faint, GameFont.Tiny) + 8f;
            }
            var m = ModelFor(p, h);
            y += UiKit.SectionTitle(0f, y, w, "CC_Heart_Section".Translate(), Icons.Heart, UiKit.HeaderColor);
            UiKit.Meter(new Rect(0f, y, w, UiKit.MeterHeight), m.fill, m.bandColor, m.strainLabel, m.strainValue, null, m.strainTip);
            y += UiKit.MeterHeight + 2f;
            y += UiKit.Paragraph(0f, y, w, m.bandDesc, MoodView.Faint, GameFont.Tiny) + 2f;
            y += UiKit.Paragraph(0f, y, w, m.built, Color.white, GameFont.Tiny) + 2f;
            if (m.help != null) y += UiKit.Paragraph(0f, y, w, m.help, MoodView.AdviceColor, GameFont.Tiny) + 2f;
            y += 6f;

            UiKit.LabelFit(new Rect(0f, y, w, 22f), "CC_Heart_FeelingsHeader".Translate(), UiKit.HeaderColor);
            y += 22f;
            if (m.feelings.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_Heart_NoFeelings".Translate(), MoodView.Faint, GameFont.Tiny) + 4f;
            foreach (var f in m.feelings)
            {
                var row = new Rect(0f, y, w, UiKit.MeterHeight);
                UiKit.Meter(row, f.share, f.color, f.label, f.value);
                float cy = y + UiKit.MeterHeight;
                float ch = 0f;
                if (!f.causes.NullOrEmpty()) ch = UiKit.Paragraph(0f, cy, w, f.causes, MoodView.Faint, GameFont.Tiny);
                var all = new Rect(0f, y, w, UiKit.MeterHeight + ch);
                Widgets.DrawHighlightIfMouseover(all);
                TooltipHandler.TipRegion(all, f.tip);
                y += UiKit.MeterHeight + ch + 4f;
            }
            y += 4f;

            if (m.needs != null)
            {
                UiKit.LabelFit(new Rect(0f, y, w, 22f), "CC_Heart_NeedsHeader".Translate(), UiKit.HeaderColor);
                y += 22f;
                float colW = (w - 12f) / 2f;
                for (int i = 0; i < 4; i++)
                {
                    var n = m.needs[i];
                    float x = (i % 2) * (colW + 12f);
                    float ry = y + (i / 2) * (UiKit.MeterHeight + 16f);
                    UiKit.Meter(new Rect(x, ry, colW, UiKit.MeterHeight), n.fill, NeedColor(n.fill), n.label, n.value, null, n.tip);
                    if (!n.why.NullOrEmpty()) UiKit.LabelFit(new Rect(x, ry + UiKit.MeterHeight - 2f, colW, 16f), n.why, MoodView.Faint, GameFont.Tiny);
                }
                y += 2 * (UiKit.MeterHeight + 16f);
                if (!m.needsSummary.NullOrEmpty()) y += UiKit.Paragraph(0f, y, w, m.needsSummary, Color.white, GameFont.Tiny) + 2f;
            }
            return y + 8f;
        }

        // ---------------- The colony's Moods page ----------------

        public class ColonyModel
        {
            public string summary;
            public List<(Pawn pawn, float fill, Color color, string band, string feelings, string tip)> rows = new List<(Pawn, float, Color, string, string, string)>();
        }

        /// <summary>Built with the Moods page's report (about once a second).</summary>
        public static ColonyModel BuildColony(IEnumerable<Pawn> pawns)
        {
            if (!Heart.On) return null;
            var m = new ColonyModel();
            var list = new List<(Pawn p, PawnHeart h)>();
            foreach (var p in pawns)
            {
                var h = Heart.Of(p);
                if (h != null && h.evaluated) list.Add((p, h));
            }
            if (list.Count == 0) return null;
            float avg = list.Average(x => x.h.strain);
            int strained = list.Count(x => x.h.Band == StrainBand.Strained);
            int worn = list.Count(x => x.h.Band >= StrainBand.Worn);
            m.summary = "CC_Heart_ColonySummary".Translate(avg.ToString("0"), BandLabel(HeartMath.BandFor(avg)), strained, worn).Resolve();
            foreach (var (p, h) in list.Where(x => x.h.strain >= 30f).OrderByDescending(x => x.h.strain).Take(8))
            {
                string feelings = DominantText(h);
                m.rows.Add((p, h.strain / HeartMath.Max, BandColor(h.Band), "CC_Heart_StrainValue".Translate(BandLabel(h.Band), h.strain.ToString("0")).Resolve(),
                    feelings ?? "", StrainTip(p, h)));
            }
            return m;
        }

        /// <summary>Draws the strain list on the Moods page; returns the new y.</summary>
        public static float DrawColony(ColonyModel m, float y, float w)
        {
            if (m == null) return y;
            y += UiKit.SectionTitle(0f, y, w, "CC_Heart_ColonyHeader".Translate(), Icons.Heart, UiKit.HeaderColor);
            y += UiKit.Paragraph(0f, y, w, m.summary, MoodView.Faint, GameFont.Tiny) + 4f;
            if (m.rows.Count == 0)
            {
                y += UiKit.Paragraph(0f, y, w, "CC_Heart_ColonyNone".Translate(), MoodView.Faint) + 4f;
                return y + 8f;
            }
            for (int i = 0; i < m.rows.Count; i++)
            {
                var r = m.rows[i];
                var row = new Rect(0f, y, w, 34f);
                UiKit.Zebra(row, i);
                Widgets.DrawHighlightIfMouseover(row);
                UiKit.Portrait(new Rect(4f, y + 3f, 28f, 28f), r.pawn, r.color);
                UiKit.LabelFit(new Rect(40f, y + 2f, 130f, 30f), r.pawn.LabelShort, Color.white);
                UiKit.Bar(new Rect(176f, y + 10f, 120f, 12f), r.fill, r.color);
                UiKit.LabelFit(new Rect(304f, y + 2f, Mathf.Max(0f, w - 308f), 15f), r.band, r.color, GameFont.Tiny);
                UiKit.LabelFit(new Rect(304f, y + 17f, Mathf.Max(0f, w - 308f), 15f), r.feelings, MoodView.Faint, GameFont.Tiny);
                TooltipHandler.TipRegion(row, r.tip);
                if (Widgets.ButtonInvisible(row)) MoodView.ShowMoodTab(r.pawn);
                y += 36f;
            }
            return y + 8f;
        }
    }
}
