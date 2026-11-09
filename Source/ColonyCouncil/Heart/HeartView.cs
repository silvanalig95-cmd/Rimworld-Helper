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
        // The interface's tokens (UI/UiKit.cs): steady warm, strained soon, worn sour, breaking danger.
        public static Color SteadyColor => UiKit.Warm;
        public static Color StrainedColor => UiKit.Soon;
        public static Color WornColor => UiKit.Sour;
        public static Color BreakingColor => UiKit.Danger;
        /// <summary>The heart's good side (in good heart, in high spirits).</summary>
        public static Color GoodColor => UiKit.Good;

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

        /// <summary>A band in words, with what it does by the settings as they are ({EFFECTS}: HeartEffects.DoingStrain).</summary>
        public static string BandDesc(StrainBand b) => ("CC_Heart_Band_" + b + "_Desc").Translate((HeartEffects.DoingStrain(b) ?? "").Named("EFFECTS")).Resolve() + FrayedTempers.BandNote(b);

        public static string SourceLabel(StrainSource s) => ("CC_Heart_Source_" + s).Translate();

        public static string ReliefLabel(StrainRelief r) => ("CC_Heart_Relief_" + r).Translate();

        /// <summary>
        /// A source as this heart's page says it: for an act against their nature, what they did and the side of them it went
        /// against ("swore the colony's oath to the Lenlia, against her pride"), the way a pressure says its cause; else the
        /// source's own words.
        /// </summary>
        public static string SourceText(PawnHeart h, StrainSource s)
        {
            if (s == StrainSource.Nature && h != null && !h.natureEased && !h.natureWhy.NullOrEmpty()) return h.natureWhy;
            // Small letdowns: what they were ("small letdowns: the same meal and my idea turned down").
            if (s == StrainSource.Letdowns && h != null && !h.letdownsWhy.NullOrEmpty()) return "CC_Heart_Source_LetdownsWhat".Translate(h.letdownsWhy).Resolve();
            // Heavy things: what they were ("heavy things: the plague and losing Ana").
            if (s == StrainSource.Heavy && HeartFelt.Why(h) is string heavy) return "CC_Felt_Source_HeavyWhat".Translate(heavy.Named("WHAT")).Resolve();
            return SourceLabel(s);
        }

        /// <summary>A kind of relief as this heart's page says it: for an act with their nature, what they did and the side of them it suited.</summary>
        public static string ReliefText(PawnHeart h, StrainRelief r)
        {
            if (r == StrainRelief.Nature && h != null && h.natureEased && !h.natureWhy.NullOrEmpty()) return h.natureWhy;
            if (r == StrainRelief.Joys && h != null && !h.joysWhy.NullOrEmpty()) return "CC_Heart_Relief_JoysWhat".Translate(h.joysWhy).Resolve();
            return ReliefLabel(r);
        }

        public static string NeedLabel(HeartNeed n) => ("CC_Heart_Need_" + n).Translate();

        public static string IntensityWord(float intensity) =>
            (intensity >= 18f ? "CC_Heart_Intensity_Strong" : intensity >= 10f ? "CC_Heart_Intensity_Clear" : "CC_Heart_Intensity_Mild").Translate();

        // ---------------- Lines of text ----------------

        private static readonly List<FeelingNow> dominantTmp = new List<FeelingNow>(2);

        /// <summary>
        /// "Strain: worn · restless, proud · lacks belonging" (built hourly and kept on the heart): the need comes in only
        /// while one is missed (below 45), the one they lack most. With the good side from steady, both sides at once
        /// ("Heart: worn, in good heart · grieving"), or the good side alone while strain is steady ("Heart: in good heart").
        /// </summary>
        public static string InspectLine(PawnHeart h)
        {
            Heart.Dominant(h, dominantTmp);
            var good = GoodHeart.Band(h);
            bool goodSide = good != GoodBand.None;
            string band = BandsText(h.Band, good);
            string line;
            if (dominantTmp.Count == 0) line = (goodSide ? "CC_Good_Inspect" : "CC_Heart_Inspect").Translate(band).Resolve();
            else line = (goodSide ? "CC_Good_InspectFeelings" : "CC_Heart_InspectFeelings").Translate(band, string.Join(", ", dominantTmp.Select(f => f.def.Adjective))).Resolve();
            dominantTmp.Clear();
            var lack = Heart.LowestNeed(h, out _);
            if (lack != null) line = "CC_Heart_InspectLacks".Translate(line, NeedLabel(lack.need)).Resolve();
            return line;
        }

        /// <summary>
        /// The bands in a few words: strain's alone, the good side's alone while strain is steady, or both at once ("worn, in
        /// good heart").
        /// </summary>
        public static string BandsText(StrainBand band, GoodBand good)
        {
            if (good == GoodBand.None) return BandLabel(band);
            if (band == StrainBand.Steady) return GoodHeart.BandLabel(good);
            return "CC_Felt_BothBands".Translate(BandLabel(band).Named("STRAIN"), GoodHeart.BandLabel(good).Named("GOOD")).Resolve();
        }

        /// <summary>The heart changed: its inspector line and RimHUD values are built again (hourly, not per frame).</summary>
        public static void Refresh(Pawn p, PawnHeart h)
        {
            h.inspectLine = InspectLine(h);
            // RimHUD's values, each in its own colour: strain's band and the good side's, both while both show, each feeling's,
            // and the lacking need's.
            var good = GoodHeart.Band(h);
            string strainValue = "CC_Heart_StrainValue".Translate(BandLabel(h.Band), h.strain.ToString("0")).Resolve().Colorize(BandColor(h.Band));
            string goodValue = good != GoodBand.None ? "CC_Heart_StrainValue".Translate(GoodHeart.BandLabel(good), h.good.ToString("0")).Resolve().Colorize(GoodColor) : null;
            h.hudStrain = goodValue == null ? strainValue : h.Band == StrainBand.Steady ? goodValue : strainValue + " · " + goodValue;
            // The heart, made to count: the top cause while strained, and time off under way, as the Mood tab's summary has them.
            if (h.Band >= StrainBand.Strained)
            {
                string cause = TopCause(h);
                if (!cause.NullOrEmpty()) h.hudStrain += " · " + cause.Colorize(UiKit.Faint);
            }
            var off = HeartTimeOff.KindOf(h);
            if (off != TimeOffKind.None) h.hudStrain += " · " + ("CC_Count_OffShort_" + off).Translate().Resolve().Colorize(UiKit.Info);
            h.hudFeelings = ColoredFeelings(h);
            var lack = Heart.LowestNeed(h, out float v);
            h.hudNeeds = h.needs == null ? null
                : lack == null ? "CC_Hud_NeedsMet".Translate().ToString().Colorize(UiKit.Good)
                : "CC_Hud_NeedsLacking".Translate(NeedLabel(lack.need), v.ToString("0")).Resolve().Colorize(NeedColor(v / 100f));
            Dirty(p);
        }

        /// <summary>The dominant feelings as adjectives, each in its feeling's colour ("Restless, proud"), or null.</summary>
        private static string ColoredFeelings(PawnHeart h)
        {
            Heart.Dominant(h, dominantTmp);
            if (dominantTmp.Count == 0) return null;
            var parts = new List<string>(dominantTmp.Count);
            for (int i = 0; i < dominantTmp.Count; i++)
            {
                string word = dominantTmp[i].def.Adjective;
                parts.Add((i == 0 ? word.CapitalizeFirst() : word).Colorize(dominantTmp[i].def.color));
            }
            dominantTmp.Clear();
            return string.Join(", ", parts);
        }

        /// <summary>The dominant feelings as adjectives ("restless, proud"), or null.</summary>
        public static string DominantText(PawnHeart h)
        {
            Heart.Dominant(h, dominantTmp);
            string s = dominantTmp.Count == 0 ? null : string.Join(", ", dominantTmp.Select(f => f.def.Adjective));
            dominantTmp.Clear();
            return s;
        }

        /// <summary>
        /// What built strain lately, biggest first: "losses, fighting and bad nights". Names only, since the mood ("built up
        /// by ...") and the episode's letter put them in a sentence; the act against their nature is said on the page.
        /// </summary>
        public static string SourcesText(PawnHeart h, int n)
        {
            var top = TopSources(h, n);
            return top.Count == 0 ? null : TextUtil.JoinAnd(top.Select(s => SourceLabel(s.source)).ToList());
        }

        /// <summary>
        /// What weighs on them most, in a few words: the heaviest thing under strain ("losing Ana"), else what built it most
        /// lately ("fighting"); null with neither.
        /// </summary>
        public static string TopCause(PawnHeart h)
        {
            if (h == null) return null;
            string why = HeartFelt.Why(h);
            if (!why.NullOrEmpty())
            {
                // The first of them: what lasts now, else the newest heavy thing.
                if (h.held >= 5f && !h.heldWhy.NullOrEmpty()) return h.heldWhy.Split('|')[0];
                if (h.weight >= 5f && !h.weightWhy.NullOrEmpty()) return h.weightWhy.Split('|')[0];
            }
            var top = TopSources(h, 1);
            return top.Count == 0 ? null : SourceLabel(top[0].source);
        }

        /// <summary>The same with amounts: "losses (+18), fighting (+9)".</summary>
        public static string SourcesDetailed(PawnHeart h, int n)
        {
            var top = TopSources(h, n);
            return top.Count == 0 ? null : string.Join(", ", top.Select(s => SourceText(h, s.source) + " (+" + s.amount.ToString("0") + ")"));
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
            return string.Join(", ", list.Take(n).Select(x => ReliefText(h, x.r) + " (-" + x.amount.ToString("0") + ")"));
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
                case StrainSource.Unmet: return "CC_Heart_Help_Needs";
                case StrainSource.Nature: return "CC_Heart_Help_Nature";
                case StrainSource.Letdowns: return "CC_Heart_Help_Letdowns";
                case StrainSource.Heavy: return "CC_Felt_Help_Heavy";
                case StrainSource.Spread: return "CC_Count_Help_Spread";
                default: return "CC_Heart_Help_Sleep";
            }
        }

        /// <summary>What would help, from what built it: "a few days without fighting, time to grieve and quiet nights in a warm bed".</summary>
        public static string HelpText(Pawn p, PawnHeart h, int n)
        {
            var keys = new List<string>();
            string unmet = null;
            foreach (var (source, _) in TopSources(h, 3))
            {
                string k = HelpFor(source);
                if (!keys.Contains(k)) keys.Add(k);
                // Strain from a need lacked for days: what that need asks for, in place of the general hint.
                if (source == StrainSource.Unmet) unmet = UnmetHelp(p, h);
            }
            foreach (var k in new[] { "CC_Heart_Help_Sleep", "CC_Heart_Help_Party", "CC_Heart_Help_Friends" })
                if (!keys.Contains(k)) keys.Add(k);
            var rest = Heart.RestDecree;
            var decrees = DecreeTracker.Of(p);
            if (rest != null && decrees != null && h.strain >= 30f && !decrees.IsActive(rest) && decrees.CannotEnact(rest) == null)
                keys.Insert(Mathf.Min(2, keys.Count), "CC_Heart_Help_RestDecree");
            return TextUtil.JoinAnd(keys.Take(n).Select(k => k == "CC_Heart_Help_Needs" && unmet != null ? unmet : k.Translate().ToString()).ToList());
        }

        /// <summary>
        /// What eases an act against their nature, said beside it the way a pressure says what helps, while the strain is too
        /// low for the general advice: null unless such an act is among what built it lately.
        /// </summary>
        public static string NatureHelp(PawnHeart h)
        {
            if (h == null || h.natureEased || h.natureWhy.NullOrEmpty()) return null;
            foreach (var (source, _) in TopSources(h, 3))
                if (source == StrainSource.Nature)
                    return "CC_Heart_NatureHelp".Translate();
            return null;
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
            if (AllMet(h)) return "CC_Heart_NeedsAllMet".Translate();
            var need = h.needs[low];
            if (need.value >= HeartNeedMath.MissedBelow) return "CC_Heart_NeedsFine".Translate();
            string label = NeedLabel((HeartNeed)low);
            return need.why.NullOrEmpty() ? "CC_Heart_NeedsMissing".Translate(label).Resolve() : "CC_Heart_NeedsMissingWhy".Translate(label, need.why).Resolve();
        }

        private static bool AllMet(PawnHeart h) =>
            h.needs != null && h.needs.Length == 4 && HeartNeedMath.AllWellMet(h.needs[0].value, h.needs[1].value, h.needs[2].value, h.needs[3].value);

        /// <summary>"badly lacking", "missed", "fair", "well met".</summary>
        public static string LevelLabel(float value) => ("CC_Heart_NeedLevel_" + HeartNeedMath.LevelOf(value)).Translate();

        /// <summary>The heart's needs in a few words, for one-line summaries: "lacks belonging (15)", "nothing missing".</summary>
        public static string NeedsShort(PawnHeart h)
        {
            if (h?.needs == null || h.needs.Length != 4) return null;
            if (AllMet(h)) return "CC_Heart_NeedsShortAllMet".Translate();
            var lack = Heart.LowestNeed(h, out float v);
            return lack == null ? "CC_Hud_NeedsMet".Translate().ToString() : "CC_Hud_NeedsLacking".Translate(NeedLabel(lack.need), v.ToString("0")).Resolve();
        }

        /// <summary>
        /// What would help a need, for the player: the hints for what holds it down now ("time outside", "medicine in
        /// store"), else the need's own ("friends close by, a partner..."). Null when nothing holds it down and it's met.
        /// </summary>
        public static string NeedHelp(PawnHeart h, HeartNeed need, int n = 3)
        {
            if (h?.needs == null || (int)need >= h.needs.Length) return null;
            var v = h.needs[(int)need];
            var list = new List<string>();
            if (v.parts != null)
                foreach (var part in v.parts)
                {
                    if (part.help.NullOrEmpty() || part.amount >= 0f) continue;
                    string text = part.helpArg != null ? part.help.Translate(part.helpArg).Resolve() : part.help.Translate().ToString();
                    if (!text.NullOrEmpty() && !list.Contains(text)) list.Add(text);
                }
            if (list.Count == 0 && v.value < HeartNeedMath.WellMet)
            {
                string general = Heart.NeedDef(need)?.Help;
                if (!general.NullOrEmpty()) list.Add(general);
            }
            return list.Count == 0 ? null : string.Join("; ", list.Take(n));
        }

        /// <summary>What would help the need that wears on them most (for strain built that way).</summary>
        private static string UnmetHelp(Pawn p, PawnHeart h)
        {
            if (h?.needs == null || h.needs.Length != 4) return null;
            int worst = -1;
            float most = 0f;
            for (int i = 0; i < 4; i++)
            {
                float per = HeartLogic.UnmetPerHour(p, h, (HeartNeed)i);
                if (per > most)
                {
                    most = per;
                    worst = i;
                }
            }
            return worst < 0 ? null : NeedHelp(h, (HeartNeed)worst, 2);
        }

        /// <summary>
        /// What follows from one need as it is now, in plain sentences: the wishes and life goals it makes likelier (if
        /// it's the one they lack most), the feeling it deepens, the strain it adds after days, or that it's well met.
        /// In the overview, the line about another need being lacked more is left out.
        /// </summary>
        public static string NeedFollows(Pawn p, PawnHeart h, HeartNeed need, bool overview = false)
        {
            if (h?.needs == null || h.needs.Length != 4) return null;
            float v = h.needs[(int)need].value;
            var def = Heart.NeedDef(need);
            var parts = new List<string>(4);
            if (v < HeartNeedMath.MissedBelow)
            {
                var lowest = Heart.LowestNeed(h, out _);
                if (lowest != null && lowest.need == need)
                {
                    string answers = def?.Answers;
                    string wish = (HeartNeedMath.WishFactor(v) - 1f).ToStringPercent(), goal = (HeartNeedMath.GoalFactor(v) - 1f).ToStringPercent();
                    parts.Add(answers.NullOrEmpty() ? "CC_Heart_NeedFollows_LackedShort".Translate(wish, goal).Resolve()
                        : "CC_Heart_NeedFollows_Lacked".Translate(wish, goal, answers).Resolve());
                }
                else if (lowest != null && !overview) parts.Add("CC_Heart_NeedFollows_NotMost".Translate(NeedLabel(lowest.need)).Resolve());
            }
            if (def?.feeling != null && HeartNeedMath.FeelingAmount(v, def.feelingAmount) > 0f)
                parts.Add("CC_Heart_NeedFollows_Feeling".Translate(def.feeling.Label).Resolve());
            bool slaveFreedom = need == HeartNeed.Freedom && p != null && p.IsSlave;
            float per = HeartLogic.UnmetPerHour(p, h, need);
            if (per > 0f) parts.Add("CC_Heart_NeedFollows_Wearing".Translate((per * 24f).ToString("0.#")).Resolve());
            else if (v < HeartNeedMath.LackingBelow && !slaveFreedom)
            {
                float hoursLeft = Mathf.Max(1f, HeartNeedMath.HoursBeforeStrain - h.LackHours(need));
                parts.Add("CC_Heart_NeedFollows_WillWear".Translate(TextUtil.DaysString(Mathf.RoundToInt(hoursLeft * GenDate.TicksPerHour))).Resolve());
            }
            if (!overview)
            {
                if (v >= HeartNeedMath.WellMet) parts.Add(AllMet(h) ? "CC_Heart_NeedsAllMet".Translate().ToString() : "CC_Heart_NeedFollows_Met".Translate().ToString());
                else if (v >= HeartNeedMath.MissedBelow) parts.Add("CC_Heart_NeedFollows_Fair".Translate());
            }
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        /// <summary>"Raising it: a friend here (+15)." and "Holding it down: no friends here, left out lately (-15)."</summary>
        private static void AppendParts(StringBuilder sb, NeedNow n)
        {
            if (n.parts == null || n.parts.Length == 0) return;
            var up = new List<string>();
            var down = new List<string>();
            foreach (var part in n.parts)
            {
                string t = part.counted ? part.text + " (" + part.amount.ToString("+0;-0") + ")" : part.text;
                (part.amount > 0f ? up : down).Add(t);
            }
            if (up.Count > 0) sb.Append("\n").Append("CC_Heart_NeedRaising".Translate(string.Join(", ", up)).Resolve());
            if (down.Count > 0) sb.Append("\n").Append("CC_Heart_NeedLowering".Translate(string.Join(", ", down)).Resolve());
        }

        /// <summary>
        /// One need in full: its value and what that means, what it's made of, what raised it and what held it down this
        /// hour, what follows from it, and what would help.
        /// </summary>
        public static string NeedTip(Pawn p, PawnHeart h, HeartNeed n)
        {
            if (h?.needs == null || (int)n >= h.needs.Length) return null;
            var v = h.needs[(int)n];
            var sb = new StringBuilder();
            sb.Append("CC_Heart_NeedTipHead".Translate(NeedLabel(n).CapitalizeFirst(), v.value.ToString("0"), LevelLabel(v.value)).Resolve().Colorize(NeedColor(v.value / 100f)));
            sb.Append("\n").Append(("CC_Heart_Need_" + n + "_Desc").Translate());
            if (v.parts != null && v.parts.Length > 0)
            {
                sb.Append("\n");
                AppendParts(sb, v);
            }
            string follows = NeedFollows(p, h, n);
            if (!follows.NullOrEmpty()) sb.Append("\n\n").Append(follows);
            string help = v.value < HeartNeedMath.WellMet ? NeedHelp(h, n) : null;
            if (!help.NullOrEmpty()) sb.Append("\n\n").Append("CC_Heart_WouldHelp".Translate(help).Resolve().Colorize(MoodView.AdviceColor));
            sb.Append("\n\n").Append("CC_Heart_NeedsExplainShort".Translate().ToString().Colorize(ColoredText.SubtleGrayColor));
            return sb.ToString();
        }

        public static string StrainTip(Pawn p, PawnHeart h)
        {
            var sb = new StringBuilder();
            sb.Append("CC_Heart_StrainTipHead".Translate(BandLabel(h.Band), h.strain.ToString("0")).Resolve());
            sb.Append("\n").Append(BandDesc(h.Band));
            // The lasting weight of heavy things, which rest can't ease strain below.
            string weight = HeartFelt.WeightLine(p, h);
            if (weight != null) sb.Append("\n\n").Append(weight);
            string built = SourcesDetailed(h, 4);
            sb.Append("\n\n").Append(built == null ? "CC_Heart_NothingBuilt".Translate().ToString() : "CC_Heart_BuiltBy".Translate(built).Resolve());
            string eased = ReliefDetailed(h, 3);
            if (eased != null) sb.Append("\n").Append("CC_Heart_EasedBy".Translate(eased).Resolve());
            // What the band does to them now, and time off under way (the heart, made to count).
            string doing = HeartEffects.Doing(h, false);
            if (doing != null) sb.Append("\n").Append("CC_Count_Doing".Translate(BandLabel(h.Band).CapitalizeFirst().Named("BAND"), doing.Named("WHAT")).Resolve());
            string off = HeartTimeOff.Line(p, h, Find.TickManager.TicksGame);
            if (off != null) sb.Append("\n").Append(off);
            string advice = h.strain >= 30f && HeartAdvice.On ? HeartAdvice.Tip(p, h) : null;
            if (advice != null) sb.Append("\n\n").Append(advice);
            else if (h.strain >= 30f)
            {
                string help = HelpText(p, h, 3);
                if (!help.NullOrEmpty()) sb.Append("\n\n").Append("CC_Heart_WouldHelp".Translate(help).Resolve());
            }
            else if (NatureHelp(h) is string nature) sb.Append("\n\n").Append(nature);
            // The other side of the one heart.
            if (GoodHeart.On)
                sb.Append("\n\n").Append("CC_Good_InStrainTip".Translate(GoodHeart.BandLabel(GoodHeart.Band(h)).Named("BAND"), h.good.ToString("0").Named("VALUE")).Resolve());
            sb.Append("\n\n").Append("CC_Heart_StrainExplain".Translate().ToString().Colorize(ColoredText.SubtleGrayColor));
            return sb.ToString();
        }

        public static string FeelingsTip(PawnHeart h)
        {
            if (h.feelings.Count == 0) return "CC_Heart_NoFeelings".Translate();
            return string.Join("\n\n", h.feelings.Select(FeelingTip));
        }

        /// <summary>
        /// All four needs at a glance (RimHUD's line, the Mood tab, the inspector, the colony's tables): each value with
        /// what it means and its main reason, the one they lack most and what follows from it, what would help, and how
        /// the values work.
        /// </summary>
        public static string NeedsTip(Pawn p, PawnHeart h)
        {
            if (h?.needs == null || h.needs.Length != 4) return null;
            var sb = new StringBuilder();
            sb.Append("CC_Heart_NeedsHeader".Translate().ToString().Colorize(ColoredText.TipSectionTitleColor));
            for (int i = 0; i < 4; i++)
            {
                var n = h.needs[i];
                string row = "CC_Heart_NeedRow".Translate(NeedLabel((HeartNeed)i).CapitalizeFirst(), n.value.ToString("0"), LevelLabel(n.value)).Resolve();
                sb.Append("\n").Append(row.Colorize(NeedColor(n.value / 100f)));
                if (!n.why.NullOrEmpty()) sb.Append("  (").Append(n.why).Append(")");
            }
            sb.Append("\n\n").Append(NeedsSummary(h));
            // What follows: for the one they lack most first, then any other that deepens a feeling or wears on them.
            var order = new List<int> { 0, 1, 2, 3 };
            order.Sort((a, b) => h.needs[a].value.CompareTo(h.needs[b].value));
            var lowest = Heart.LowestNeed(h, out _);
            foreach (int i in order)
            {
                if (h.needs[i].value >= HeartNeedMath.MissedBelow) continue;
                string follows = NeedFollows(p, h, (HeartNeed)i, true);
                if (follows.NullOrEmpty()) continue;
                bool most = lowest != null && (int)lowest.need == i;
                sb.Append("\n").Append(most ? follows : "CC_Heart_NeedAlso".Translate(NeedLabel((HeartNeed)i).CapitalizeFirst(), follows).Resolve());
            }
            if (lowest != null)
            {
                string help = NeedHelp(h, lowest.need);
                if (!help.NullOrEmpty()) sb.Append("\n\n").Append("CC_Heart_WouldHelp".Translate(help).Resolve().Colorize(MoodView.AdviceColor));
            }
            sb.Append("\n\n").Append("CC_Heart_NeedsExplain".Translate().ToString().Colorize(ColoredText.SubtleGrayColor));
            return sb.ToString();
        }

        /// <summary>
        /// The need they lack most in a few lines, for short tooltips (This week's pinned colonists): what's missing and
        /// why, what follows from it and what would help. Null when nothing is missed.
        /// </summary>
        public static string NeedsBrief(Pawn p, PawnHeart h)
        {
            var lowest = Heart.LowestNeed(h, out _);
            if (lowest == null) return null;
            var sb = new StringBuilder();
            sb.Append(NeedsSummary(h));
            string follows = NeedFollows(p, h, lowest.need, true);
            if (!follows.NullOrEmpty()) sb.Append(" ").Append(follows);
            string help = NeedHelp(h, lowest.need, 2);
            if (!help.NullOrEmpty()) sb.Append("\n").Append("CC_Heart_WouldHelp".Translate(help).Resolve().Colorize(MoodView.AdviceColor));
            return sb.ToString();
        }

        /// <summary>The inspector line's tooltip: strain, the feelings that stand out, and the heart's needs.</summary>
        public static string InspectTip(Pawn p, PawnHeart h)
        {
            if (p == null || h == null) return null;
            var sb = new StringBuilder();
            sb.Append(StrainTip(p, h));
            Heart.Dominant(h, dominantTmp);
            var standing = dominantTmp.ToList();
            dominantTmp.Clear();
            if (standing.Count > 0) sb.Append("\n\n").Append(string.Join("\n\n", standing.Select(FeelingTip)));
            string needs = NeedsTip(p, h);
            if (!needs.NullOrEmpty()) sb.Append("\n\n").Append(needs);
            sb.Append("\n\n").Append("CC_Heart_InspectMore".Translate().ToString().Colorize(ColoredText.SubtleGrayColor));
            return sb.ToString();
        }

        // ---------------- Caches ----------------

        private class PawnModel
        {
            public float builtAt = -1f;
            public int version;
            public string strainLabel, strainValue, strainTip, bandDesc, built, eased, weight, help, needsSummary, needsTip;
            public Color bandColor;
            public float fill;
            /// <summary>The heart's good side, its meter and its line (null while it is off).</summary>
            public string goodLabel, goodValue, goodTip, goodLine;
            public float goodFill;
            /// <summary>The heart, made to count: what each side does to them now, time off under way, who close wore on or lifted them.</summary>
            public string doingStrain, doingGood, timeOff, spread;
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
            string eased = ReliefDetailed(h, 3);
            m.eased = eased == null ? null : "CC_Heart_EasedBy".Translate(eased).Resolve();
            m.weight = HeartFelt.WeightLine(p, h);
            // With the advice on, what would help is its own list with buttons (Heart/HeartAdvice.cs); else the old sentence.
            m.help = HeartAdvice.On ? null : h.strain >= 30f ? "CC_Heart_WouldHelp".Translate(HelpText(p, h, 3)).Resolve() : NatureHelp(h);
            if (HeartAdvice.On && h.strain < 30f) m.help = NatureHelp(h);
            string doing = HeartEffects.Doing(h, false);
            m.doingStrain = doing == null ? null : "CC_Count_Doing".Translate(BandLabel(h.Band).CapitalizeFirst().Named("BAND"), doing.Named("WHAT")).Resolve();
            m.timeOff = HeartTimeOff.Line(p, h, Find.TickManager.TicksGame);
            m.spread = SpreadLine(p, h);
            if (GoodHeart.On)
            {
                var good = GoodHeart.Band(h);
                m.goodLabel = "CC_Good_MeterLabel".Translate();
                m.goodValue = "CC_Heart_StrainValue".Translate(GoodHeart.BandLabel(good), h.good.ToString("0")).Resolve();
                m.goodFill = h.good / HeartMath.Max;
                m.goodTip = GoodHeart.Tip(p, h);
                string filled = GoodHeart.BuiltDetailed(h, 3);
                // Both sides at once: the good side's band or what filled it, and how much slower it grows under strain.
                string slower = GoodHeart.SlowerLine(p, h);
                m.goodLine = (good != GoodBand.None ? GoodHeart.BandDesc(good)
                    : filled != null ? "CC_Good_BuiltBy".Translate(filled.Named("WHAT")).Resolve() : "CC_Good_NothingBuilt".Translate().ToString())
                    + (slower != null ? " " + slower : "");
                string goodDoing = HeartEffects.Doing(h, true);
                m.doingGood = goodDoing == null ? null : "CC_Count_Doing".Translate(GoodHeart.BandLabel(good).CapitalizeFirst().Named("BAND"), goodDoing.Named("WHAT")).Resolve();
            }
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
                    m.needs[i] = (NeedLabel((HeartNeed)i).CapitalizeFirst(), n.value.ToString("0"), n.why, NeedTip(p, h, (HeartNeed)i), n.value / 100f);
                }
                m.needsSummary = NeedsSummary(h);
                m.needsTip = NeedsTip(p, h);
            }
            return m;
        }

        /// <summary>
        /// Who close wore on them or lifted them this hour ("Bautista being worn down wears on her a little."), or null. Hearts
        /// spread a little (HeartCountMath.Spread).
        /// </summary>
        public static string SpreadLine(Pawn p, PawnHeart h)
        {
            if (h == null) return null;
            var parts = new List<string>(2);
            if (!h.spreadFrom.NullOrEmpty()) parts.Add("CC_Count_SpreadFrom".Translate(h.spreadFrom.Named("OTHER"), p.Named("PAWN")).Resolve());
            if (!h.liftedBy.NullOrEmpty()) parts.Add("CC_Count_LiftedBy".Translate(h.liftedBy.Named("OTHER"), p.Named("PAWN")).Resolve());
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        // ---------------- The Mood tab's Heart sections ----------------

        public static Color NeedColor(float fill) => fill >= 0.6f ? UiKit.Good : fill >= 0.45f ? UiKit.Info : fill >= 0.3f ? UiKit.Warn : UiKit.Bad;

        /// <summary>
        /// The heart's three sections, one after another (the heart, its feelings, its needs): see <see cref="DrawHeart"/>,
        /// <see cref="DrawFeelings"/> and <see cref="DrawNeeds"/>. Returns the new y.
        /// </summary>
        public static float DrawSection(Pawn p, float y, float w, Folds folds = null)
        {
            y = DrawHeart(p, y, w, folds);
            y = DrawFeelings(p, y, w, folds);
            return DrawNeeds(p, y, w, folds);
        }

        /// <summary>
        /// The heart: strain's meter, its band, what built it and what eased it, the weight under it, who close wore on or
        /// lifted them, time off under way, what each side does to them now, what would help (with its buttons), and the good
        /// side's meter. Nothing when the feature is off or they have no heart. With folds, a click on the heading folds it.
        /// </summary>
        public static float DrawHeart(Pawn p, float y, float w, Folds folds = null)
        {
            var h = Heart.ForView(p);
            if (h == null) return y;
            y += UiKit.FoldTitle(0f, y, w, "CC_Heart_Section".Translate(), folds, "heart", out bool open, Icons.Heart, UiKit.HeaderColor);
            if (!open) return y + 4f;
            if (!h.evaluated) return y + UiKit.Paragraph(0f, y, w, "CC_Heart_NotYet".Translate(), MoodView.Faint, GameFont.Tiny) + 8f;
            var m = ModelFor(p, h);
            UiKit.Meter(new Rect(0f, y, w, UiKit.MeterHeight), m.fill, m.bandColor, m.strainLabel, m.strainValue, null, m.strainTip);
            y += UiKit.MeterHeight + 2f;
            y += UiKit.Paragraph(0f, y, w, m.bandDesc, MoodView.Faint, GameFont.Tiny) + 2f;
            if (m.doingStrain != null) y += UiKit.Paragraph(0f, y, w, m.doingStrain, m.bandColor, GameFont.Tiny) + 2f;
            y += UiKit.Paragraph(0f, y, w, m.built, Color.white, GameFont.Tiny) + 2f;
            if (m.eased != null) y += UiKit.Paragraph(0f, y, w, m.eased, MoodView.Faint, GameFont.Tiny) + 2f;
            if (m.weight != null) y += UiKit.Paragraph(0f, y, w, m.weight, MoodView.Faint, GameFont.Tiny) + 2f;
            if (m.spread != null) y += UiKit.Paragraph(0f, y, w, m.spread, MoodView.Faint, GameFont.Tiny) + 2f;
            if (m.timeOff != null) y += UiKit.Paragraph(0f, y, w, m.timeOff, UiKit.Info, GameFont.Tiny) + 2f;
            if (m.help != null) y += UiKit.Paragraph(0f, y, w, m.help, MoodView.AdviceColor, GameFont.Tiny) + 2f;
            // What would help, with its buttons (Heart/HeartAdvice.cs).
            var advice = HeartAdvice.For(p, h);
            if (advice.Count > 0)
            {
                y += 2f;
                UiKit.LabelFit(new Rect(0f, y, w, 18f), "CC_Advice_Head".Translate(p.Named("PAWN")).Resolve(), MoodView.AdviceColor, GameFont.Tiny);
                y += 18f;
                y += HeartAdvice.Draw(p, advice, 0f, y, w);
            }
            // The other side of the one heart.
            if (m.goodLabel != null)
            {
                y += 4f;
                UiKit.Meter(new Rect(0f, y, w, UiKit.MeterHeight), m.goodFill, GoodColor, m.goodLabel, m.goodValue, null, m.goodTip);
                y += UiKit.MeterHeight + 2f;
                if (!m.goodLine.NullOrEmpty()) y += UiKit.Paragraph(0f, y, w, m.goodLine, MoodView.Faint, GameFont.Tiny) + 2f;
                if (m.doingGood != null) y += UiKit.Paragraph(0f, y, w, m.doingGood, GoodColor, GameFont.Tiny) + 2f;
            }
            return y + 6f;
        }

        /// <summary>The feelings that stand out, each a meter with what fed it (its heading folds it).</summary>
        public static float DrawFeelings(Pawn p, float y, float w, Folds folds = null)
        {
            var h = Heart.ForView(p);
            if (h == null || !h.evaluated) return y;
            var m = ModelFor(p, h);
            y += UiKit.FoldTitle(0f, y, w, "CC_Heart_FeelingsHeader".Translate(), folds, "feelings", out bool open, null, UiKit.HeaderColor,
                m.feelings.Count > 0 ? m.feelings.Count.ToString() : null);
            if (!open) return y + 4f;
            if (m.feelings.Count == 0) y += UiKit.Paragraph(0f, y, w, "CC_Heart_NoFeelings".Translate(), MoodView.Faint, GameFont.Tiny) + 4f;
            foreach (var f in m.feelings)
            {
                var row = new Rect(0f, y, w, UiKit.MeterHeight);
                UiKit.Meter(row, f.share, f.color, f.label, f.value);
                float cy = y + UiKit.MeterHeight;
                float ch = 0f;
                if (!f.causes.NullOrEmpty()) ch = UiKit.Paragraph(0f, cy, w, f.causes, MoodView.Faint, GameFont.Tiny);
                var all = new Rect(0f, y, w, UiKit.MeterHeight + ch);
                UiKit.RowHover(all);
                TooltipHandler.TipRegion(all, f.tip);
                y += UiKit.MeterHeight + ch + 4f;
            }
            return y + 4f;
        }

        /// <summary>The heart's four needs, each a meter with its reason, and what's missing (its heading folds it).</summary>
        public static float DrawNeeds(Pawn p, float y, float w, Folds folds = null)
        {
            var h = Heart.ForView(p);
            if (h == null || !h.evaluated) return y;
            var m = ModelFor(p, h);
            if (m.needs == null) return y;
            y += UiKit.FoldTitle(0f, y, w, "CC_Heart_NeedsHeader".Translate(), folds, "needs", out bool open, null, UiKit.HeaderColor);
            if (!open) return y + 4f;
            // The summary and each meter (with its reason) explain their need; the summary's tooltip all four.
            float colW = (w - 12f) / 2f;
            for (int i = 0; i < 4; i++)
            {
                var n = m.needs[i];
                float x = (i % 2) * (colW + 12f);
                float ry = y + (i / 2) * (UiKit.MeterHeight + 16f);
                UiKit.Meter(new Rect(x, ry, colW, UiKit.MeterHeight), n.fill, NeedColor(n.fill), n.label, n.value);
                if (!n.why.NullOrEmpty()) UiKit.LabelFit(new Rect(x, ry + UiKit.MeterHeight - 2f, colW, 16f), n.why, MoodView.Faint, GameFont.Tiny);
                var cell = new Rect(x, ry, colW, UiKit.MeterHeight + 14f);
                UiKit.RowHover(cell);
                if (!n.tip.NullOrEmpty()) TooltipHandler.TipRegion(cell, n.tip);
            }
            y += 2 * (UiKit.MeterHeight + 16f);
            if (!m.needsSummary.NullOrEmpty())
            {
                float sh = UiKit.Paragraph(0f, y, w, m.needsSummary, Color.white, GameFont.Tiny);
                if (!m.needsTip.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(0f, y, w, sh), m.needsTip);
                y += sh + 2f;
            }
            return y + 8f;
        }

        // ---------------- The colony's Moods page ----------------

        public class ColonyModel
        {
            public string summary;
            public List<(Pawn pawn, float fill, Color color, string band, string feelings, string tip, string name)> rows =
                new List<(Pawn, float, Color, string, string, string, string)>();
            /// <summary>What would help the colony's strained, each decree and a party once (Heart/HeartAdvice.cs).</summary>
            public List<HeartAdvice.ColonyRow> advice;
        }

        /// <summary>Built with the Moods page's report (about once a second).</summary>
        public static ColonyModel BuildColony(IEnumerable<Pawn> pawns)
        {
            if (!Heart.On) return null;
            var m = new ColonyModel();
            var list = new List<(Pawn p, PawnHeart h)>();
            foreach (var p in pawns)
            {
                var h = Heart.ForView(p);
                if (h != null && h.evaluated) list.Add((p, h));
            }
            if (list.Count == 0) return null;
            float avg = list.Average(x => x.h.strain);
            int strained = list.Count(x => x.h.Band == StrainBand.Strained);
            int worn = list.Count(x => x.h.Band >= StrainBand.Worn);
            m.summary = "CC_Heart_ColonySummary".Translate(avg.ToString("0"), BandLabel(HeartMath.BandFor(avg)), strained, worn).Resolve();
            // And the good side: how many are steady, in good heart and in high spirits.
            if (GoodHeart.On)
            {
                int steady = list.Count(x => GoodHeart.Band(x.h) == GoodBand.Steady);
                int goodHeart = list.Count(x => GoodHeart.Band(x.h) == GoodBand.GoodHeart), high = list.Count(x => GoodHeart.Band(x.h) == GoodBand.HighSpirits);
                if (steady + goodHeart + high > 0)
                    m.summary += " " + "CC_GoodSide_ColonySummary".Translate(steady.Named("STEADY"), goodHeart.Named("GOOD"), high.Named("HIGH")).Resolve();
            }
            foreach (var (p, h) in list.Where(x => x.h.strain >= 30f).OrderByDescending(x => x.h.strain).Take(8))
            {
                string feelings = DominantText(h);
                // Both sides at once: someone worn may still be in good heart; and time off under way.
                var good = GoodHeart.Band(h);
                string band = "CC_Heart_StrainValue".Translate(BandLabel(h.Band), h.strain.ToString("0")).Resolve();
                if (good != GoodBand.None) band += " · " + GoodHeart.BandLabel(good);
                var off = HeartTimeOff.KindOf(h);
                if (off != TimeOffKind.None) band += " · " + ("CC_Count_OffShort_" + off).Translate().Resolve();
                m.rows.Add((p, h.strain / HeartMath.Max, BandColor(h.Band), band, feelings ?? "", StrainTip(p, h), ThingRefs.Link(p.LabelShort, ThingRefs.Of(p))));
            }
            m.advice = HeartAdvice.Colony(list.Select(x => x.p));
            return m;
        }

        /// <summary>Draws the strain list on the Moods page, then what would help the colony; returns the new y.</summary>
        public static float DrawColony(ColonyModel m, float y, float w)
        {
            if (m == null) return y;
            y += UiKit.SectionTitle(0f, y, w, "CC_Heart_ColonyHeader".Translate(), Icons.Heart);
            y += UiKit.Paragraph(0f, y, w, m.summary, MoodView.Faint, GameFont.Tiny) + 4f;
            if (m.rows.Count == 0)
            {
                UiKit.EmptyLine(new Rect(0f, y, w, UiKit.RowDense + 4f), "CC_Heart_ColonyNone".Translate());
                return y + UiKit.RowDense + 12f;
            }
            for (int i = 0; i < m.rows.Count; i++)
            {
                var r = m.rows[i];
                var row = new Rect(0f, y, w, UiKit.RowPortrait);
                UiKit.Zebra(row, i);
                UiKit.RowHover(row);
                // Their portrait and name lead to their card; the rest of the row to their Mood tab.
                var face = new Rect(4f, y + 5f, 30f, 30f);
                UiKit.Portrait(face, r.pawn, r.color);
                var who = ThingRefs.Of(r.pawn);
                if (Mouse.IsOver(face)) Previews.Hover(who, face);
                if (Widgets.ButtonInvisible(face, false))
                {
                    Cards.Click();
                    Cards.Open(who);
                }
                LinkText.Line(new Rect(42f, y, 130f, row.height), r.name, UiKit.Ink);
                UiKit.Bar(new Rect(176f, y + 14f, 120f, 12f), r.fill, r.color);
                UiKit.LabelFit(new Rect(304f, y + 4f, Mathf.Max(0f, w - 308f), 15f), r.band, UiKit.Ink, GameFont.Tiny);
                UiKit.LabelFit(new Rect(304f, y + 20f, Mathf.Max(0f, w - 308f), 15f), r.feelings, MoodView.Faint, GameFont.Tiny);
                var rest = new Rect(176f, y, Mathf.Max(0f, w - 176f), row.height);
                TooltipHandler.TipRegion(rest, r.tip);
                if (Widgets.ButtonInvisible(rest)) MoodView.ShowMoodTab(r.pawn);
                y += UiKit.RowPortrait;
            }
            // What would help, for the whole colony: each with its button where Rimfolk can act.
            if (m.advice != null && m.advice.Count > 0)
            {
                y += 6f;
                UiKit.LabelFit(new Rect(0f, y, w, 20f), "CC_Advice_ColonyHead".Translate(), MoodView.AdviceColor);
                y += 22f;
                foreach (var a in m.advice)
                {
                    var i = a.item;
                    float bw = i.act != null ? UiKit.RowActWidth(i.act) : 0f;
                    float tw = Mathf.Max(40f, w - (bw > 0f ? bw + 6f : 0f));
                    float th = UiKit.Paragraph(0f, y, tw, "· " + a.text, MoodView.AdviceColor, GameFont.Tiny);
                    if (i.act != null && UiKit.RowAct(new Rect(w - bw, y, bw, 20f), i.act, i.whyNot, i.tip))
                    {
                        try
                        {
                            i.run?.Invoke();
                        }
                        catch (System.Exception e)
                        {
                            CouncilLog.ErrorOnce("the heart's advice: a button on the Moods page", e);
                        }
                        HeartAdvice.Stale(Heart.Of(a.pawn));
                    }
                    y += Mathf.Max(th, i.act != null ? 20f : 0f) + 3f;
                }
            }
            return y + 8f;
        }
    }
}
