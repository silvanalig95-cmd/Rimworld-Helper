using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What an answer will do, gathered line by line as each of its effects describes itself for the live moment
    /// (<see cref="DilemmaEffect.Describe"/>): the named colonist and who else, the moods with their real size and length,
    /// trust, goods and silver, what plays out on the map, chances. Nothing here changes the game.
    /// </summary>
    public sealed class ImpactSheet
    {
        /// <summary>Written into a line where an amount that adds up goes (<see cref="ImpactMath.Fill"/>).</summary>
        public const string Mark = ImpactMath.Mark;

        public readonly DilemmaContext ctx;
        public readonly List<ImpactLine> lines = new List<ImpactLine>();

        public ImpactSheet(DilemmaContext ctx)
        {
            this.ctx = ctx;
        }

        public Map Map => ctx?.map ?? ctx?.a?.MapHeld;

        public ImpactLine Add(ImpactKind kind, int sign, string text, string brief = null, string key = null)
        {
            if (text.NullOrEmpty()) return null;
            var l = new ImpactLine { kind = kind, sign = sign, text = text, brief = brief, key = key };
            lines.Add(l);
            return l;
        }

        /// <summary>An amount that adds up with the same key's (trust, goods, silver): <see cref="Mark"/> in the texts is where it goes.</summary>
        public ImpactLine Amount(ImpactKind kind, int sign, float amount, string text, string brief, string key, bool signed = true)
        {
            if (Mathf.Abs(amount) < 0.01f || text.NullOrEmpty()) return null;
            var l = Add(kind, sign, text, brief, key);
            l.amount = amount;
            l.sum = true;
            l.signed = signed;
            return l;
        }

        /// <summary>Something that happens on the map.</summary>
        public ImpactLine Play(string text, string brief = null) => Add(ImpactKind.Map, 0, text, brief);

        /// <summary>Trust in the council for those named.</summary>
        public void Trust(string who, string whoKey, float amount, bool many)
        {
            var l = Amount(ImpactKind.Trust, 1, amount, "CC_Impact_Trust".Translate(who, Mark).Resolve(), "CC_Impact_TrustBrief".Translate(who, Mark).Resolve(), "trust|" + whoKey);
            if (l == null) return;
            l.each = many;
            l.topic = HintTopic.Trust;
            l.who = who;
        }

        /// <summary>
        /// Things the colony gets (sign 1) or gives up (sign -1): <paramref name="count"/> of <paramref name="label"/>, with
        /// how many the stores hold when known (for the hint's "some" or "much").
        /// </summary>
        public void Goods(int sign, string label, int count, string key = null, float stock = -1f)
        {
            if (count <= 0 || label.NullOrEmpty()) return;
            string k = (sign > 0 ? "gain|" : "cost|") + (key ?? label);
            var l = Amount(ImpactKind.Goods, sign, count, (sign > 0 ? "CC_Impact_Gain" : "CC_Impact_Cost").Translate(Mark, label).Resolve(),
                (sign > 0 ? "CC_Impact_GainBrief" : "CC_Impact_CostBrief").Translate(Mark, label).Resolve(), k, false);
            if (l == null) return;
            l.topic = HintTopic.Goods;
            l.about = label;
            l.stock = stock;
        }

        public void Goods(int sign, ThingDef def, int count) => Goods(sign, def?.label, count, def?.defName, Stock(def));

        /// <summary>How many of a thing the colony's stores hold (-1 when the game doesn't count it).</summary>
        public float Stock(ThingDef def)
        {
            var map = Map;
            if (def == null || map?.resourceCounter == null || !def.CountAsResource) return -1f;
            return map.resourceCounter.GetCount(def);
        }

        /// <summary>Silver to the colony (positive) or out of it (negative).</summary>
        public void Silver(int amount, string key = "silver")
        {
            var l = Amount(ImpactKind.Goods, 1, amount, "CC_Impact_Silver".Translate(Mark).Resolve(), "CC_Impact_SilverBrief".Translate(Mark).Resolve(), key);
            if (l != null) l.topic = HintTopic.Silver;
        }

        /// <summary>Goodwill with a people.</summary>
        public void Goodwill(Faction f, float amount)
        {
            if (f == null || f.IsPlayer) return;
            var l = Amount(ImpactKind.Goodwill, 1, amount, "CC_Impact_Goodwill".Translate(f.Name, Mark).Resolve(), "CC_Impact_GoodwillBrief".Translate(f.Name, Mark).Resolve(),
                "goodwill|" + f.loadID);
            if (l == null) return;
            l.topic = HintTopic.Goodwill;
            l.about = f.Name;
        }

        /// <summary>
        /// A memory for those named: its mood (as big as it will really be for each of them: the reward or downside
        /// strength and their standards) for its days, its opinion of the other, or what it gives instead of mood.
        /// </summary>
        public void Thought(List<Pawn> who, string whoText, ThoughtDef t, Func<Pawn, Pawn> about)
        {
            if (t == null || who.NullOrEmpty() || whoText.NullOrEmpty()) return;
            var s = ColonyCouncilMod.Settings;
            if (s == null || !s.MoodEffects) return;
            var stage = t.stages?.FirstOrDefault(x => x != null);
            if (stage == null) return;
            if (t.IsSocial) who = who.Where(p => about(p) != null && about(p) != p).ToList();
            if (who.Count == 0) return;
            string label = (stage.label ?? t.label ?? "").CapitalizeFirst();
            if (label.Contains("{0}")) label = label.Replace("{0}", about(who[0])?.LabelShort ?? "");
            string days = t.durationDays > 0f ? TextUtil.DaysString(Mathf.RoundToInt(t.durationDays * GenDate.TicksPerDay)) : null;
            bool many = who.Count > 1;
            float mood = stage.baseMoodEffect;
            if (Mathf.Abs(mood) > 0.01f && !RewardInstead.On(t))
            {
                bool positive = mood >= 0f;
                float factor = positive ? s.rewardFactor : s.PenaltyScale;
                if (factor > 0f)
                {
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (var p in who)
                    {
                        float v = mood * factor * (positive ? 1f : Standards.PenaltyFactor(p));
                        lo = Mathf.Min(lo, v);
                        hi = Mathf.Max(hi, v);
                    }
                    string range = ImpactMath.SignedRange(lo, hi, "CC_Impact_To".Translate().Resolve());
                    var l = Add(ImpactKind.Mood, positive ? 1 : -1,
                        days != null ? "CC_Impact_Mood".Translate(whoText, range, days, label).Resolve() : "CC_Impact_MoodNow".Translate(whoText, range, label).Resolve(),
                        "CC_Impact_MoodBrief".Translate(whoText, range).Resolve(), "mood|" + t.defName + "|" + whoText);
                    if (l != null)
                    {
                        l.each = many;
                        l.topic = HintTopic.Mood;
                        l.who = whoText;
                        l.size = positive ? hi : lo;
                        l.days = t.durationDays;
                        l.about = label;
                    }
                }
            }
            if (t.IsSocial && Mathf.Abs(stage.baseOpinionOffset) > 0.01f)
            {
                string of = OpinionOf(who, about);
                string v = ImpactMath.Signed(stage.baseOpinionOffset);
                var l = Add(ImpactKind.Opinion, stage.baseOpinionOffset > 0f ? 1 : -1,
                    days != null ? "CC_Impact_Opinion".Translate(whoText, v, of, days, label).Resolve() : "CC_Impact_OpinionNow".Translate(whoText, v, of, label).Resolve(),
                    "CC_Impact_OpinionBrief".Translate(whoText, v, of).Resolve(), "opinion|" + t.defName + "|" + whoText);
                if (l != null)
                {
                    l.each = many;
                    l.topic = HintTopic.Opinion;
                    l.who = whoText;
                    l.size = stage.baseOpinionOffset;
                    l.days = t.durationDays;
                    l.about = of;
                }
            }
            Instead(who, whoText, t);
        }

        /// <summary>Who a social memory is about: one person for all, or "each other" for two about each other.</summary>
        private static string OpinionOf(List<Pawn> who, Func<Pawn, Pawn> about)
        {
            var others = who.Select(about).Where(o => o != null).Distinct().ToList();
            if (who.Count == 2 && about(who[0]) == who[1] && about(who[1]) == who[0]) return "CC_Impact_EachOther".Translate().Resolve();
            return TextUtil.JoinAnd(others.Select(o => o.LabelShort).ToList());
        }

        /// <summary>A small good moment that gives something other than mood (Moods/RewardInstead.cs).</summary>
        private void Instead(List<Pawn> who, string whoText, ThoughtDef t)
        {
            var x = t.GetModExtension<RewardInstead>();
            if (x == null) return;
            float scale = ColonyCouncilMod.Settings.rewardFactor;
            bool many = who.Count > 1;
            ImpactLine Line(ImpactKind kind, string key, params NamedArgument[] args)
            {
                var l = Add(kind, 1, key.Translate(args).Resolve(), (key + "Brief").Translate(args).Resolve(), key + "|" + t.defName + "|" + whoText);
                if (l != null)
                {
                    l.each = many;
                    l.who = whoText;
                }
                return l;
            }
            void Sized(ImpactLine l, HintTopic topic, float size, string what = null)
            {
                if (l == null) return;
                l.topic = topic;
                l.size = size;
                l.about = what;
            }
            string Pct(float v) => "+" + (v * scale * 100f).ToString("0") + "%";
            if (x.joy > 0f) Sized(Line(ImpactKind.Needs, "CC_Impact_InsteadJoy", whoText, Pct(x.joy)), HintTopic.Joy, x.joy * scale);
            if (x.rest > 0f) Sized(Line(ImpactKind.Needs, "CC_Impact_InsteadRest", whoText, Pct(x.rest)), HintTopic.Rest, x.rest * scale);
            if (x.comfort > 0f) Sized(Line(ImpactKind.Needs, "CC_Impact_InsteadComfort", whoText, Pct(x.comfort)), HintTopic.Comfort, x.comfort * scale);
            if (x.xp > 0f)
            {
                string skill = x.skill?.label ?? ("CC_Impact_SkillFrom_" + (x.skillFrom ?? "mine")).Translate().Resolve();
                string hintSkill = x.skill?.label ?? ("CC_ImpactHint_SkillFrom_" + (x.skillFrom ?? "mine")).Translate().Resolve();
                Sized(Line(ImpactKind.Skill, "CC_Impact_InsteadXp", whoText, (x.xp * scale).ToString("0"), skill), HintTopic.Skill, x.xp * scale, hintSkill);
            }
            if (x.trust > 0f) Trust(whoText, whoText, x.trust * scale, many);
            if (x.research > 0f) Line(ImpactKind.Other, "CC_Impact_InsteadResearch", (x.research * scale).ToString("0"));
            if (x.inspiration > 0f)
            {
                var l = Add(ImpactKind.Needs, 1, "CC_Impact_Inspire".Translate(whoText).Resolve(), "CC_Impact_InspireBrief".Translate(whoText).Resolve(),
                    "inspire|" + t.defName + "|" + whoText);
                if (l != null)
                {
                    l.chance = Mathf.Clamp01(x.inspiration * scale);
                    l.each = many;
                    l.topic = HintTopic.Inspire;
                    l.who = whoText;
                }
            }
            if (x.journal) Line(ImpactKind.Other, "CC_Impact_InsteadJournal", whoText);
        }
    }

    /// <summary>
    /// Shows what each answer does, where the player chooses (the letters of dilemmas, Rimfolk's events and world
    /// events) and on the Dilemmas page afterwards: a short line under the answer and the full list in its tooltip,
    /// grouped by kind, the same things merged, from the live moment (who is involved, what the stores hold, how each
    /// colonist will feel it). As flavour (the default) the line under the answer says how it would land without naming a
    /// mechanic (Dilemmas/ImpactFlavourMath.cs) and the hints show on hover; as hints it is said in plain words without
    /// numbers (Dilemmas/ImpactHints.cs); as exact numbers, every size, day, count and chance; off, the letters are as they were.
    /// </summary>
    public static class DilemmaImpact
    {
        /// <summary>The short line's length before the rest is left to the tooltip (two lines under an answer).</summary>
        public const int CompactChars = 190;

        public static ImpactShow Mode => ColonyCouncilMod.Settings?.answerImpact ?? ImpactShow.Flavour;

        public static bool On => Mode != ImpactShow.Off;

        /// <summary>The words hints and the flavour line are made of, from the game's translations.</summary>
        public static readonly HintWords Words = new HintWords(k => k.Translate().RawText, k => k.CanTranslate());

        /// <summary>The lines of some effects, merged.</summary>
        public static List<ImpactLine> Of(DilemmaContext ctx, IEnumerable<DilemmaEffect> effects)
        {
            var sheet = new ImpactSheet(ctx);
            if (effects != null)
                foreach (var e in effects)
                {
                    if (e == null) continue;
                    int before = sheet.lines.Count;
                    try
                    {
                        e.Describe(ctx, sheet);
                    }
                    catch (Exception ex)
                    {
                        CouncilLog.ErrorOnce("answer impact " + e.GetType().Name, ex);
                    }
                    for (int i = before; i < sheet.lines.Count; i++)
                    {
                        var l = sheet.lines[i];
                        if (e.chance < 1f) l.chance = Mathf.Min(l.chance, e.chance);
                        if (!e.ChanceEach) l.each = false;
                    }
                    if (e is DilemmaEffect_PlayOut play) ForHint(ctx, play, sheet.lines, before);
                }
            return ImpactMath.Merge(sheet.lines);
        }

        /// <summary>
        /// What plays out, for the hint in words: its name and when ("The high watch together in the evening"), the hours
        /// it takes from work in the day (not when it is work itself), and a lesson's learning by its size.
        /// </summary>
        private static void ForHint(DilemmaContext ctx, DilemmaEffect_PlayOut play, List<ImpactLine> lines, int from)
        {
            bool work = play is DilemmaEffect_WorkParty || play is DilemmaEffect_Search || play is DilemmaEffect_HuntParty || play is DilemmaEffect_HaulDead;
            bool marked = false;
            for (int i = from; i < lines.Count; i++)
            {
                var l = lines[i];
                if (!marked && l.kind == ImpactKind.Map && !l.brief.NullOrEmpty() && l.text != null && l.text.StartsWith(l.brief, StringComparison.Ordinal))
                {
                    marked = true;
                    l.topic = HintTopic.Play;
                    l.hint ??= (play is DilemmaEffect_Gathering ? "CC_ImpactHint_PlayTogether" : "CC_ImpactHint_Play").Translate(l.brief, WhenHint(play.hours)).Resolve();
                    l.hours = (play.duration.min + play.duration.max) / 2f;
                    l.when = ImpactHints.WhenKind(play.hours.min, play.hours.max);
                    l.workTime = !work && ImpactHints.InWorkHours(l.when);
                    l.together = play is DilemmaEffect_Gathering;
                }
                else if (play is DilemmaEffect_Lesson lesson && l.kind == ImpactKind.Skill && l.key != null && l.key.StartsWith("lesson|", StringComparison.Ordinal))
                {
                    var student = PlayOuts.Who(lesson.student, ctx);
                    var skill = DefDatabase<SkillDef>.GetNamedSilentFail(l.key.Substring(l.key.LastIndexOf('|') + 1));
                    if (student == null || skill == null) continue;
                    l.topic = HintTopic.Skill;
                    l.who = student.LabelShort;
                    l.about = skill.label;
                    l.size = lesson.xpPerHour * lesson.duration.Average;
                }
            }
        }

        /// <summary>Everything one answer does, for the moment it's asked in.</summary>
        public static OptionImpact For(DilemmaContext ctx, DilemmaOption o)
        {
            if (ctx == null || o == null) return new OptionImpact();
            float live = o.riskChance > 0f ? DilemmaChoices.RiskChance(o, ctx, o.riskChance) : 0f;
            var r = Build(ctx, o.effects, o.ways, o.riskChance, live, o.riskEffects, o.riskReplaces, o.riskSkill, new[] { ctx.a, ctx.b });
            Tradition(ctx, o, r.lines);
            return r;
        }

        /// <summary>Everything one answer of a world event does (its risk lowered by A's skill, as the event's worker has it).</summary>
        public static OptionImpact For(SituationContext ctx, WorldSituation s, SituationOption o)
        {
            if (ctx == null || o == null || s?.Worker == null) return new OptionImpact();
            float live = o.riskChance > 0f ? s.Worker.RiskChance(s, ctx, o) : 0f;
            return Build(ctx, o.effects, o.ways, o.riskChance, live, o.riskEffects, o.riskReplaces, o.riskSkill, new[] { ctx.a });
        }

        private static OptionImpact Build(DilemmaContext ctx, List<DilemmaEffect> effects, WayShift ways, float riskBase, float riskLive, List<DilemmaEffect> riskEffects,
            bool riskReplaces, SkillDef riskSkill, Pawn[] helpers)
        {
            var r = new OptionImpact { lines = Of(ctx, effects) };
            if (ColonyCouncilMod.Settings.colonyCharacter && ways != null && ways.Any)
            {
                string shift = ways.Summary();
                if (!shift.NullOrEmpty())
                    r.lines.Add(new ImpactLine
                    {
                        kind = ImpactKind.Ways, text = "CC_Impact_Ways".Translate(shift).Resolve(), brief = "CC_Impact_WaysBrief".Translate(shift).Resolve(),
                        topic = HintTopic.Ways, about = Leaning(ways)
                    });
            }
            if (riskBase <= 0f) return r;
            r.riskChance = riskLive;
            r.riskReplaces = riskReplaces;
            var risky = Of(ctx, riskEffects);
            r.risk = riskReplaces ? risky : ImpactMath.Besides(risky, r.lines);
            if (riskSkill == null) return r;
            var best = helpers.Where(p => p?.skills?.GetSkill(riskSkill) is SkillRecord s && !s.TotallyDisabled)
                .OrderByDescending(p => p.skills.GetSkill(riskSkill).Level).FirstOrDefault();
            bool lowered = best != null && riskLive < riskBase - 0.001f;
            r.riskNote = lowered
                ? "CC_Impact_RiskLowered".Translate(best.LabelShort, riskSkill.label, best.skills.GetSkill(riskSkill).Level, Odds(riskBase)).Resolve()
                : "CC_Impact_RiskSkill".Translate(riskSkill.label).Resolve();
            r.riskSkill = riskSkill.label;
            r.riskHelper = lowered ? best.LabelShort : null;
            return r;
        }

        /// <summary>The way an answer leans the colony most ("traditional"), for the hint.</summary>
        private static string Leaning(WayShift ways)
        {
            Way? most = null;
            foreach (Way w in Enum.GetValues(typeof(Way)))
                if (Mathf.Abs(ways.Get(w)) >= 0.5f && (most == null || Mathf.Abs(ways.Get(w)) > Mathf.Abs(ways.Get(most.Value))))
                    most = w;
            return most == null ? null : CharacterTracker.PoleLabel(most.Value, ways.Get(most.Value) > 0f);
        }

        /// <summary>A custom kept or broken: the tradition's moods and trust (Dilemmas.cs, Tradition).</summary>
        private static void Tradition(DilemmaContext ctx, DilemmaOption o, List<ImpactLine> lines)
        {
            var def = ctx.def;
            // The traditions of the colony it's about.
            var tracker = DilemmaTracker.ForQuestion(ctx);
            if (def == null || tracker == null || def.traditionOption < 0 || def.traditionOption >= def.options.Count) return;
            bool kept = def.options[def.traditionOption] == o;
            string name = def.traditionLabel ?? def.label;
            if (kept)
            {
                if (tracker.IsTradition(def))
                    lines.Add(new ImpactLine
                    {
                        kind = ImpactKind.Other, sign = 1, text = "CC_Impact_TraditionKept".Translate(name).Resolve(), brief = "CC_Impact_TraditionKeptBrief".Translate(name).Resolve(),
                        cue = FlavourCue.CustomKept, about = name
                    });
                else
                {
                    int had = tracker.TraditionCount(def);
                    lines.Add(new ImpactLine
                    {
                        kind = ImpactKind.Other, sign = 1, text = "CC_Impact_TraditionToward".Translate(name, had + 1, CalendarMath.TraditionAfter).Resolve(),
                        hint = "CC_ImpactHint_TraditionToward".Translate(name).Resolve(), hintFull = "CC_ImpactHint_TraditionTowardFull".Translate(name).Resolve(),
                        cue = FlavourCue.CustomKept, about = name
                    });
                }
            }
            else if (tracker.IsTradition(def))
                lines.Add(new ImpactLine
                {
                    kind = ImpactKind.Other, sign = -1, text = "CC_Impact_TraditionBroken".Translate(name).Resolve(), brief = "CC_Impact_TraditionBrokenBrief".Translate(name).Resolve(),
                    hintFull = "CC_ImpactHint_TraditionBroken".Translate(name).Resolve(), cue = FlavourCue.CustomBroken, about = name
                });
            else if (tracker.CustomDay(def) != null)
                // Kept before, not yet a tradition: let pass, it leaves the calendar and its years start again (Calendar/CalendarCustoms.cs).
                lines.Add(new ImpactLine { kind = ImpactKind.Other, sign = -1, text = "CC_Fest_Impact_OffCalendar".Translate(name.Named("CUSTOM")).Resolve(), cue = FlavourCue.CustomBroken, about = name });
        }

        // ---------------- Words ----------------

        /// <summary>"1 in 4", "about 1 in 3".</summary>
        public static string Odds(float p)
        {
            var (n, d, exact) = ImpactMath.Odds(p);
            return (exact ? "CC_Impact_Odds" : "CC_Impact_OddsAbout").Translate(n, d).Resolve();
        }

        private static string WithChance(ImpactLine l, string text)
        {
            if (l.chance >= 1f) return text;
            return (l.each ? "CC_Impact_ChanceEach" : "CC_Impact_Chance").Translate(Odds(l.chance), text).Resolve();
        }

        public static string LineText(ImpactLine l) => WithChance(l, l.Text);

        public static string LineBrief(ImpactLine l) => WithChance(l, l.Brief);

        private static Color ColorOf(ImpactLine l) => l.Sign > 0 ? UiKit.Good : l.Sign < 0 ? UiKit.Bad : Color.white;

        /// <summary>The full list, for the tooltip: grouped by kind under a heading each, then the gamble.</summary>
        public static string Full(OptionImpact r)
        {
            if (r == null || r.Empty) return "CC_Impact_Nothing".Translate().Resolve();
            var sb = new System.Text.StringBuilder();
            sb.Append("CC_Impact_Header".Translate().Resolve().Colorize(ColoredText.TipSectionTitleColor));
            Section(sb, r.lines);
            if (r.riskChance > 0f)
            {
                sb.Append("\n\n");
                string note = r.riskNote.NullOrEmpty() ? "" : " " + r.riskNote;
                string head = (r.riskReplaces ? "CC_Impact_RiskInstead" : "CC_Impact_Risk").Translate(Odds(r.riskChance), note).Resolve();
                sb.Append(head.Colorize(UiKit.Warn));
                if (r.risk.Count == 0) sb.Append("\n  " + (r.riskReplaces ? "CC_Impact_RiskNothingAtAll" : "CC_Impact_RiskNothingMore").Translate().Resolve());
                else Section(sb, r.risk, false);
            }
            return sb.ToString();
        }

        private static void Section(System.Text.StringBuilder sb, List<ImpactLine> lines, bool headings = true)
        {
            ImpactKind? kind = null;
            foreach (var l in lines)
            {
                if (headings && kind != l.kind)
                {
                    kind = l.kind;
                    sb.Append("\n\n" + ("CC_Impact_Kind_" + l.kind).Translate().Resolve().Colorize(ColoredText.SubtleGrayColor));
                }
                sb.Append("\n  " + LineText(l).Colorize(ColorOf(l)));
            }
        }

        /// <summary>The hint under an answer, in words without numbers (Dilemmas/ImpactHints.cs).</summary>
        public static string Hint(OptionImpact r) => ImpactHints.Line(r, Words);

        /// <summary>The flavour line under an answer (Dilemmas/ImpactFlavourMath.cs): its words picked by the letter and the answer's place in it.</summary>
        public static string Flavour(OptionImpact r, uint seed, int answer) => ImpactFlavourMath.Line(r, Words, seed, answer);

        /// <summary>A letter's number for the flavour's words: what and whom it is about and its own text, the same each time it opens.</summary>
        public static uint SeedOf(DilemmaContext ctx)
        {
            if (ctx == null) return 0u;
            string Id(Pawn p) => p == null ? "" : p.thingIDNumber.ToString();
            return ImpactFlavourMath.Seed(ctx.def?.defName ?? (ctx as HappeningContext)?.incident?.defName ?? "", (ctx as SituationContext)?.situationId.ToString() ?? "", Id(ctx.a), Id(ctx.b),
                Id(ctx.stranger), ctx.text ?? "");
        }

        /// <summary>The hint's tooltip: what the answer does in words, grouped by kind, then the gamble, without numbers.</summary>
        public static string HintFull(OptionImpact r)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var row in ImpactHints.Rows(r, Words))
            {
                if (row.heading)
                {
                    if (sb.Length > 0) sb.Append("\n\n");
                    sb.Append(row.text.Colorize(sb.Length == 0 ? ColoredText.TipSectionTitleColor : ColoredText.SubtleGrayColor));
                }
                else if (row.risk) sb.Append("\n\n" + row.text.Colorize(UiKit.Warn));
                else sb.Append((sb.Length > 0 ? "\n  " : "") + row.text.Colorize(row.sign > 0 ? UiKit.Good : row.sign < 0 ? UiKit.Bad : Color.white));
            }
            return sb.ToString();
        }

        /// <summary>"in the evening", "around midday": when something is set for, in words (for the hints).</summary>
        public static string WhenHint(IntRange hours) => ("CC_ImpactHint_When_" + ImpactHints.WhenKind(hours.min, hours.max)).Translate().Resolve();

        /// <summary>What plays out, for the hint: "The high watch in the evening".</summary>
        public static string PlayHint(string label, IntRange hours) => "CC_ImpactHint_Play".Translate(label, WhenHint(hours)).Resolve();

        /// <summary>The short line under an answer: the things it does in order, then the gamble, cut at a whole part.</summary>
        public static string Compact(OptionImpact r)
        {
            if (r == null || r.Empty) return null;
            var parts = r.lines.Select(LineBrief).ToList();
            string risk = null;
            if (r.riskChance > 0f)
            {
                var more = r.risk.Select(LineBrief).ToList();
                risk = more.Count == 0
                    ? "CC_Impact_RiskBriefNothing".Translate(Odds(r.riskChance)).Resolve()
                    : "CC_Impact_RiskBrief".Translate(Odds(r.riskChance), string.Join(", ", more.Take(2))).Resolve();
            }
            int room = CompactChars - (risk == null ? 0 : risk.Length + 2);
            string main = ImpactMath.Compact(parts, Mathf.Max(60, room), "CC_Impact_More".Translate().Resolve());
            if (risk == null) return main;
            return main.NullOrEmpty() ? risk : main + "; " + risk;
        }

        /// <summary>Who a group effect reaches, in words: their names when a few, else the group and how many.</summary>
        public static string Who(DilemmaContext ctx, DilemmaTarget target, List<Pawn> reached, DilemmaEffect e)
        {
            if (reached == null || reached.Count == 0) return null;
            bool group = target == DilemmaTarget.Colony || target == DilemmaTarget.Others || target == DilemmaTarget.FriendsOfA || target == DilemmaTarget.RivalsOfA
                         || target == DilemmaTarget.FamilyOfA || target == DilemmaTarget.CloseToA;
            if (!group || reached.Count <= 3) return TextUtil.JoinAnd(reached.Select(p => p.LabelShort).ToList());
            int n = reached.Count;
            string a = ctx?.a?.LabelShort ?? "?";
            // A group the answer names ("the sick", "the devout"), or the colony's faithful.
            if (!(e?.groupLabel).NullOrEmpty()) return "CC_Impact_Who_The".Translate(ctx != null ? ctx.Format(e.groupLabel) : e.groupLabel, n).Resolve();
            if (e != null && e.faithFilter != FaithFilter.Any)
                return (e.faithFilter == FaithFilter.Faithful ? "CC_Impact_Who_Faithful" : "CC_Impact_Who_NotFaithful").Translate(n).Resolve();
            string traits = TraitWords(e?.onlyTrait, e?.degreeTrait, e?.minDegree ?? -99, e?.maxDegree ?? 99);
            if (traits != null) return "CC_Impact_Who_The".Translate(traits, n).Resolve();
            string text;
            switch (target)
            {
                case DilemmaTarget.Colony: text = "CC_Impact_Who_Everyone".Translate(n).Resolve(); break;
                case DilemmaTarget.Others: text = "CC_Impact_Who_Others".Translate(n).Resolve(); break;
                case DilemmaTarget.FriendsOfA: text = "CC_Impact_Who_FriendsOf".Translate(a, n).Resolve(); break;
                case DilemmaTarget.RivalsOfA: text = "CC_Impact_Who_RivalsOf".Translate(a, n).Resolve(); break;
                case DilemmaTarget.FamilyOfA: text = "CC_Impact_Who_FamilyOf".Translate(a, n).Resolve(); break;
                default: text = "CC_Impact_Who_CloseTo".Translate(a, n).Resolve(); break;
            }
            string but = TraitWords(e?.exceptTrait, null, -99, 99);
            return but == null ? text : "CC_Impact_Who_But".Translate(text, but).Resolve();
        }

        /// <summary>"greedy, jealous or abrasive": the traits a group is limited to.</summary>
        private static string TraitWords(List<TraitDef> traits, TraitDef degree, int min, int max)
        {
            var words = new List<string>();
            if (traits != null)
                foreach (var t in traits)
                {
                    string w = t.degreeDatas?.FirstOrDefault()?.label ?? t.label;
                    if (!w.NullOrEmpty()) words.Add(w);
                }
            if (degree?.degreeDatas != null)
                foreach (var d in degree.degreeDatas)
                    if (d.degree >= min && d.degree <= max && !d.label.NullOrEmpty())
                        words.Add(d.label);
            if (words.Count == 0) return null;
            if (words.Count == 1) return words[0];
            return string.Join(", ", words.Take(words.Count - 1)) + " " + "CC_Impact_Or".Translate().Resolve() + " " + words[words.Count - 1];
        }

        /// <summary>"in the evening", "at night", "in the day", or the hours.</summary>
        public static string When(IntRange hours)
        {
            int from = hours.min, to = hours.max;
            if (from >= 19 && (to < from || to >= 23)) return "CC_Impact_When_Night".Translate().Resolve();
            if (from >= 15 && (to < from || to >= 20)) return "CC_Impact_When_Evening".Translate().Resolve();
            if (from <= 8 && to >= 18 && to > from) return "CC_Impact_When_Day".Translate().Resolve();
            return "CC_Impact_When_Hours".Translate(from, to).Resolve();
        }

        /// <summary>"2 to 3 hours", "about an hour".</summary>
        public static string Hours(FloatRange h)
        {
            string lo = ImpactMath.Number(h.min), hi = ImpactMath.Number(h.max);
            if (h.max <= 1.25f) return "CC_Impact_AboutAnHour".Translate().Resolve();
            return lo == hi ? "CC_Impact_Hours".Translate(lo).Resolve() : "CC_Impact_HoursRange".Translate(lo, hi).Resolve();
        }

        /// <summary>A place a play-out goes to, in words.</summary>
        public static string Spot(PlaySpot s, DilemmaContext ctx, Pawn person = null)
        {
            if (s == PlaySpot.Person && person != null) return "CC_Impact_Spot_PersonOf".Translate(person.LabelShort).Resolve();
            if (s == PlaySpot.Stranger && ctx?.stranger != null) return "CC_Impact_Spot_PersonOf".Translate(ctx.stranger.LabelShort).Resolve();
            // A camp far off meets the colony at the gate (Camps/CampMeet.cs).
            if (s == PlaySpot.Camp && ctx?.map != null && CampMeet.Far(ctx.map, ctx.stranger)) return "CC_Impact_Spot_CampAtGate".Translate().Resolve();
            return ("CC_Impact_Spot_" + s).Translate().Resolve();
        }

        public static string Name(Pawn p) => p?.LabelShort;

        /// <summary>A bond in the social web growing (or fading, negative) between two people.</summary>
        public static void Tie(ImpactSheet sheet, Pawn a, Pawn b, TieKind kind, float amount)
        {
            if (a == null || b == null || a == b || Mathf.Abs(amount) < 0.01f || kind == TieKind.None) return;
            string k = ("CC_Tie_" + kind).Translate().Resolve();
            var l = sheet.Amount(ImpactKind.Opinion, kind == TieKind.Rivals ? -1 : 1, amount, "CC_Impact_Tie".Translate(a.LabelShort, b.LabelShort, k, ImpactSheet.Mark).Resolve(),
                "CC_Impact_TieBrief".Translate(a.LabelShort, b.LabelShort, k).Resolve(), "tie|" + kind + "|" + Math.Min(a.thingIDNumber, b.thingIDNumber) + "|" + Math.Max(a.thingIDNumber, b.thingIDNumber));
            if (l == null) return;
            l.hintFull = (amount > 0f ? "CC_ImpactHint_TieUp" : "CC_ImpactHint_TieDown").Translate(a.LabelShort, b.LabelShort, k).Resolve();
            // A bond growing or a rivalry easing brings them closer; a rivalry growing or a bond fading sets them apart.
            l.cue = (kind == TieKind.Rivals) == (amount < 0f) ? FlavourCue.Bond : FlavourCue.Rift;
            l.who = a.LabelShort;
            l.with = b.LabelShort;
        }

        /// <summary>Strain added (positive) or eased (negative), with feelings and strain on.</summary>
        public static void Strain(ImpactSheet sheet, string who, string whoKey, float amount, bool many)
        {
            if (who.NullOrEmpty() || Mathf.Abs(amount) < 0.01f) return;
            var l = sheet.Amount(ImpactKind.Needs, -1, amount, "CC_Impact_Strain".Translate(who, ImpactSheet.Mark).Resolve(), "CC_Impact_StrainBrief".Translate(who, ImpactSheet.Mark).Resolve(),
                "strain|" + whoKey);
            if (l == null) return;
            l.each = many;
            l.topic = HintTopic.Strain;
            l.who = who;
        }

        /// <summary>Certainty in their faith (Ideology): a share of the bar, steadied or shaken.</summary>
        public static void Certainty(ImpactSheet sheet, string who, float amount)
        {
            if (who.NullOrEmpty() || Mathf.Abs(amount) < 0.0001f) return;
            string pct = (amount >= 0f ? "+" : "") + (amount * 100f).ToString("0.#") + "%";
            var l = sheet.Add(ImpactKind.Other, amount > 0f ? 1 : -1, "CC_Impact_Certainty".Translate(who, pct).Resolve(), "CC_Impact_CertaintyBrief".Translate(who, pct).Resolve(), "certainty|" + who);
            if (l == null) return;
            l.topic = HintTopic.Certainty;
            l.who = who;
            l.size = amount * 100f;
        }

        /// <summary>The camp's tension toward the colony: up (a refusal) or down (a kindness).</summary>
        public static void Tension(ImpactSheet sheet, float amount)
        {
            if (Mathf.Abs(amount) < 0.01f) return;
            var l = sheet.Amount(ImpactKind.Goodwill, -1, amount, "CC_Impact_Tension".Translate(ImpactSheet.Mark).Resolve(), "CC_Impact_TensionBrief".Translate(ImpactSheet.Mark).Resolve(), "tension");
            if (l != null) l.topic = HintTopic.Tension;
        }

        /// <summary>"3", or "3 to 6".</summary>
        public static string Range(int lo, int hi) => lo >= hi ? hi.ToString() : "CC_Impact_RangeInt".Translate(lo, hi).Resolve();

        /// <summary>Game to hunt: "2 elk or deer" (kinds by defName, "Elk|Deer"), else "1 animal of the best eating".</summary>
        public static string Kinds(string kinds, int count)
        {
            var names = kinds.NullOrEmpty() ? new List<string>()
                : kinds.Split('|').Select(k => DefDatabase<ThingDef>.GetNamedSilentFail(k.Trim())?.label).Where(l => !l.NullOrEmpty()).Distinct().ToList();
            if (names.Count == 0) return "CC_Impact_Game".Translate(count).Resolve();
            string list = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " " + "CC_Impact_Or".Translate().Resolve() + " " + names[names.Count - 1];
            return "CC_Impact_GameOf".Translate(count, list).Resolve();
        }

        /// <summary>Experience in a skill.</summary>
        public static void Xp(ImpactSheet sheet, string who, string whoKey, SkillDef skill, float xp, bool many)
        {
            if (skill == null || xp <= 0f || who.NullOrEmpty()) return;
            var l = sheet.Amount(ImpactKind.Skill, 1, xp, "CC_Impact_Xp".Translate(who, ImpactSheet.Mark, skill.label).Resolve(),
                "CC_Impact_XpBrief".Translate(who, ImpactSheet.Mark, skill.label).Resolve(), "xp|" + skill.defName + "|" + whoKey, false);
            if (l == null) return;
            l.each = many;
            l.topic = HintTopic.Skill;
            l.who = who;
            l.about = skill.label;
        }

        /// <summary>What a condition does at a severity, in the game's own words for its stats: "Global work speed +10%".</summary>
        public static string HediffEffects(HediffDef def, float severity)
        {
            if (def?.stages.NullOrEmpty() ?? true) return null;
            var stage = def.stages[Mathf.Clamp(def.StageAtSeverity(severity), 0, def.stages.Count - 1)];
            var parts = new List<string>();
            if (stage.statOffsets != null)
                foreach (var m in stage.statOffsets)
                    if (m?.stat != null && Mathf.Abs(m.value) > 0.0001f) parts.Add(m.stat.LabelCap + " " + m.stat.ValueToString(m.value, ToStringNumberSense.Offset));
            if (stage.statFactors != null)
                foreach (var m in stage.statFactors)
                    if (m?.stat != null && Mathf.Abs(m.value - 1f) > 0.0001f) parts.Add(m.stat.LabelCap + " " + m.stat.ValueToString(m.value, ToStringNumberSense.Factor));
            if (stage.capMods != null)
                foreach (var c in stage.capMods)
                {
                    if (c?.capacity == null) continue;
                    if (Mathf.Abs(c.offset) > 0.0001f) parts.Add(c.capacity.LabelCap + " " + (c.offset * 100f).ToString("+0;-0") + "%");
                    if (Mathf.Abs(c.postFactor - 1f) > 0.0001f) parts.Add(c.capacity.LabelCap + " x" + c.postFactor.ToString("0.##"));
                }
            if (Mathf.Abs(stage.restFallFactor - 1f) > 0.0001f) parts.Add("CC_Impact_RestFall".Translate(stage.restFallFactor.ToString("0.##")).Resolve());
            if (Mathf.Abs(stage.hungerRateFactor - 1f) > 0.0001f) parts.Add("CC_Impact_Hunger".Translate(stage.hungerRateFactor.ToString("0.##")).Resolve());
            if (Mathf.Abs(stage.painOffset) > 0.0001f) parts.Add("CC_Impact_Pain".Translate((stage.painOffset * 100f).ToString("+0;-0") + "%").Resolve());
            return parts.Count == 0 ? null : string.Join(", ", parts);
        }

        /// <summary>How long a condition lasts, when it goes by itself: "2.5 to 3.3 days".</summary>
        public static string HediffDays(HediffDef def)
        {
            var d = def?.CompProps<HediffCompProperties_Disappears>();
            if (d == null || d.disappearsAfterTicks.max <= 0) return null;
            string lo = ImpactMath.Number(d.disappearsAfterTicks.min / (float)GenDate.TicksPerDay), hi = ImpactMath.Number(d.disappearsAfterTicks.max / (float)GenDate.TicksPerDay);
            return lo == hi ? "CC_Impact_Days".Translate(lo).Resolve() : "CC_Impact_DaysRange".Translate(lo, hi).Resolve();
        }

        /// <summary>Good for the colony (faster work), a cost (slower work, a bad condition), or neither.</summary>
        public static int HediffSign(HediffDef def, float severity)
        {
            if (def == null) return 0;
            if (def.isBad) return -1;
            var stage = def.stages.NullOrEmpty() ? null : def.stages[Mathf.Clamp(def.StageAtSeverity(severity), 0, def.stages.Count - 1)];
            float work = stage?.statOffsets?.FirstOrDefault(m => m.stat == StatDefOf.WorkSpeedGlobal)?.value ?? 0f;
            return work > 0f ? 1 : work < 0f ? -1 : 0;
        }

        // ---------------- The letters ----------------

        /// <summary>
        /// The answer as the letter offers it: its label, greyed out with <paramref name="why"/>, and what it does under it
        /// (with the setting on).
        /// </summary>
        public static DiaOption Option(DilemmaContext ctx, DilemmaOption o, int answer, string label, string why, Action action) =>
            Option(ctx == null || o == null ? null : () => For(ctx, o), SeedOf(ctx), answer, label, why, action);

        /// <summary>A world event's answer as its letter offers it, with what it does.</summary>
        public static DiaOption Option(SituationContext ctx, WorldSituation s, SituationOption o, int answer, string label, string why, Action action) =>
            Option(ctx == null || o == null ? null : () => For(ctx, s, o), SeedOf(ctx), answer, label, why, action);

        private static DiaOption Option(Func<OptionImpact> impact, uint seed, int answer, string label, string why, Action action)
        {
            DiaOption opt;
            if (On && impact != null)
            {
                OptionImpact r = null;
                try
                {
                    r = impact();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("answer impact", e);
                }
                opt = r == null || r.Empty ? new DiaOption(label)
                    : Mode == ImpactShow.Exact ? new DiaOption_Impact(label, Compact(r), Full(r))
                    : Mode == ImpactShow.Flavour ? new DiaOption_Impact(label, Flavour(r, seed, answer), HintFull(r))
                    : new DiaOption_Impact(label, Hint(r), HintFull(r));
            }
            else opt = new DiaOption(label);
            if (why != null) opt.Disable(why);
            opt.action = action;
            opt.resolveTree = true;
            return opt;
        }

        /// <summary>
        /// What the answer given did, for the Dilemmas page: the full list as it stood when it was given, in numbers, the
        /// same in words (<paramref name="hint"/>) and the flavour line under it (<paramref name="flavour"/>), so the page can
        /// follow the setting. Never throws.
        /// </summary>
        public static string Record(DilemmaContext ctx, DilemmaOption o, int answer, out string hint, out string flavour)
        {
            hint = null;
            flavour = null;
            try
            {
                var r = For(ctx, o);
                if (r.Empty) return null;
                hint = Cut(HintFull(r), 1200);
                flavour = Cut(Flavour(r, SeedOf(ctx), answer), 200);
                return Cut(Full(r), 1600);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("answer impact (record)", e);
                return null;
            }
        }

        private static string Cut(string text, int max) => text == null || text.Length <= max ? text : text.Substring(0, max);

        /// <summary>What an answer given was to do, for the Dilemmas page, as the setting shows it (null: nothing to show).</summary>
        public static string RecordFor(DilemmaRecord r)
        {
            switch (Mode)
            {
                case ImpactShow.Exact: return r?.impact;
                case ImpactShow.Hints: return r?.impactHint;
                case ImpactShow.Flavour:
                    // The line as it stood under the answer, then the hints, as on hover (an answer given before the flavour came: the hints).
                    if (r == null || r.impactFlavour.NullOrEmpty()) return r?.impactHint;
                    return r.impactHint.NullOrEmpty() ? r.impactFlavour : r.impactFlavour + "\n\n" + r.impactHint;
                default: return null;
            }
        }
    }

    /// <summary>
    /// An answer in a letter with what it does: a short line under its label (flavour, a hint in words, or the numbers),
    /// the hints or the full list on hover. Drawn by
    /// <see cref="Patch_DiaOptionImpact"/>; without that hook the short line is written into the label itself.
    /// </summary>
    public class DiaOption_Impact : DiaOption
    {
        public readonly string compact;
        public readonly string full;
        /// <summary>The hook draws the short line; false when it was written into the label instead.</summary>
        public readonly bool drawn;

        public DiaOption_Impact(string label, string compact, string full)
            : base(Hooked && !compact.NullOrEmpty() ? label : label + (compact.NullOrEmpty() ? "" : "\n" + compact.Colorize(ColoredText.SubtleGrayColor)))
        {
            this.compact = compact;
            this.full = full;
            drawn = Hooked && !compact.NullOrEmpty();
        }

        private static bool Hooked => RimfolkPatches.On && RimfolkPatches.Applied.Contains(nameof(Patch_DiaOptionImpact));

        /// <summary>The short line under the label (wrapped, never cut), and the full list as the tooltip of both: the height it took.</summary>
        public float DrawUnder(Rect rect, float labelHeight)
        {
            float h = 0f;
            var font = Text.Font;
            var anchor = Text.Anchor;
            var color = GUI.color;
            try
            {
                if (drawn)
                {
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperLeft;
                    float w = rect.width - 14f;
                    h = Text.CalcHeight(compact, w);
                    GUI.color = disabled ? new Color(0.55f, 0.55f, 0.55f) : new Color(0.72f, 0.72f, 0.72f);
                    Widgets.Label(new Rect(rect.x + 14f, rect.y + labelHeight, w, h), compact);
                }
                if (!full.NullOrEmpty()) TooltipHandler.TipRegion(new Rect(rect.x, rect.y, rect.width, labelHeight + h), full);
            }
            finally
            {
                Text.Font = font;
                Text.Anchor = anchor;
                GUI.color = color;
            }
            return h;
        }
    }

    /// <summary>
    /// Draws what an answer does under it in a letter (<see cref="DiaOption_Impact"/>), and its full list on hover. Only
    /// Rimfolk's own answers; the game's options are untouched. The letter lays out its answers by the height this
    /// returns, so the short line takes its own room.
    /// </summary>
    [HarmonyPatch(typeof(DiaOption), nameof(DiaOption.OptOnGUI))]
    public static class Patch_DiaOptionImpact
    {
        public static void Postfix(DiaOption __instance, Rect rect, ref float __result)
        {
            if (!(__instance is DiaOption_Impact o) || !RimfolkPatches.On) return;
            try
            {
                __result += o.DrawUnder(rect, __result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("answer impact (draw)", e);
            }
        }
    }
}
