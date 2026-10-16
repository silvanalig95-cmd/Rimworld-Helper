using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// More ways for a Rimfolk event, a dilemma or a stranger encounter to read, kept in a file of their own and merged
    /// in at startup (as <see cref="FlavorExtensionDef"/> does for wishes): more letter texts and titles, and more ways to
    /// tell how an answer turned out. Lines use the placeholders and tags of the def they join, plus those of
    /// <see cref="LetterTags"/>.
    /// </summary>
    public class LetterVariantsDef : Def
    {
        public IncidentDef incident;
        public DilemmaDef dilemma;
        public EncounterDef encounter;
        [MustTranslate] public List<string> titles;
        [MustTranslate] public List<string> texts;
        /// <summary>More ways to tell an answer's outcome, by the answer's place in the list (0 = the first).</summary>
        public List<OutcomeVariants> outcomes;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            int targets = (incident == null ? 0 : 1) + (dilemma == null ? 0 : 1) + (encounter == null ? 0 : 1);
            if (targets != 1) yield return "letter variants need exactly one target: incident, dilemma or encounter";
            if (encounter != null && !outcomes.NullOrEmpty()) yield return "an encounter has no answers to add outcomes to";
        }
    }

    public class OutcomeVariants
    {
        public int option;
        [MustTranslate] public List<string> lines;
        [MustTranslate] public List<string> riskLines;
    }

    public static class LetterVariants
    {
        public static void Apply()
        {
            int merged = 0;
            foreach (var v in DefDatabase<LetterVariantsDef>.AllDefsListForReading)
            {
                try
                {
                    List<DilemmaOption> options = null;
                    if (v.incident != null)
                    {
                        var ext = v.incident.GetModExtension<HappeningExtension>();
                        if (ext == null)
                        {
                            CouncilLog.WarnOnce("letter variants " + v.defName + ": " + v.incident.defName + " is not a Rimfolk event");
                            continue;
                        }
                        Add(ref ext.titles, v.titles);
                        Add(ref ext.texts, v.texts);
                        options = ext.options;
                    }
                    else if (v.dilemma != null)
                    {
                        Add(ref v.dilemma.texts, v.texts);
                        options = v.dilemma.options;
                    }
                    else if (v.encounter != null) Add(ref v.encounter.texts, v.texts);
                    if (!v.outcomes.NullOrEmpty())
                    {
                        foreach (var o in v.outcomes)
                        {
                            if (options == null || o.option < 0 || o.option >= options.Count)
                            {
                                CouncilLog.WarnOnce("letter variants " + v.defName + ": no answer " + o.option);
                                continue;
                            }
                            Add(ref options[o.option].outcomes, o.lines);
                            Add(ref options[o.option].riskOutcomes, o.riskLines);
                        }
                    }
                    merged++;
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("letter variants " + v.defName, e);
                }
            }
            if (merged > 0 && Prefs.DevMode) Log.Message("[Rimfolk] merged " + merged + " letter variant set(s).");
        }

        private static void Add(ref List<string> into, List<string> lines)
        {
            if (lines.NullOrEmpty()) return;
            if (into == null) into = new List<string>();
            into.AddRange(lines.Where(l => !l.NullOrEmpty()));
        }
    }

    /// <summary>
    /// Tags every event, dilemma and encounter line can ask for, on top of its own (the era, the season, and whatever the
    /// event's worker adds). Who A is: their traits, passions, memes and xenotype as anywhere (trait:Kind, skill:Medicine,
    /// meme:Charity), their past (past:soldier, past:outlaw, past:noble, past:healer, past:scholar, past:farmer,
    /// past:trader, past:artisan) and office (office:Speaker, office:Marshal, office:Quartermaster, office:Physician,
    /// office:Steward). Who B is: the same with b: in front (b:trait:Kind, b:past:healer). The stranger (X): x:child,
    /// x:old, x:animal. Their people: theirLowtech or theirHightech. What the colony is known for: colony:Merciful,
    /// colony:Harsh, colony:Generous, colony:Thrifty, colony:Traditional, colony:Restless, colony:Open, colony:Guarded,
    /// colony:Hardworking, colony:Easygoing. Dilemmas and encounters also get the moment as A lives it (weather, time
    /// of day, mood: see <see cref="Flavor.TagsFor"/>).
    /// </summary>
    public static class LetterTags
    {
        /// <summary>A dilemma's or an encounter's tags: the moment as A lives it, and who everyone is.</summary>
        public static HashSet<string> For(DilemmaContext ctx)
        {
            var tags = Flavor.TagsFor(ctx?.a);
            if (ctx != null)
            {
                Add(tags, ctx.a, ctx.b, ctx.stranger, ctx.faction);
                Moment(tags, ctx.map ?? ctx.a?.MapHeld, ctx.a, ctx.stranger);
            }
            return tags;
        }

        /// <summary>
        /// The land (land:desert, land:coast...: see <see cref="Lands"/>) and who is ill: "ill" and "ail:Key" (a Rimfolk
        /// ailment) or "ill:DefName" for A, "x:ill" for the stranger.
        /// </summary>
        private static void Moment(HashSet<string> tags, Map map, Pawn a, Pawn x)
        {
            try
            {
                Lands.AddTags(map, tags);
                var h = LifeChecks.Illness(a);
                if (h != null)
                {
                    tags.Add("ill");
                    string key = AilmentTracker.InfoOf(h.def)?.key;
                    tags.Add(key.NullOrEmpty() ? "ill:" + h.def.defName : "ail:" + key);
                }
                if (x != null && x.RaceProps.Humanlike && LifeChecks.Illness(x) != null) tags.Add("x:ill");
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("letter tags (moment)", e);
            }
        }

        /// <summary>
        /// Who A, B and X are, their people, and the colony's name. <paramref name="groups"/>: A's one-word tag groups too
        /// (mage, healer, fighter); off for events, whose own tags may use the same words.
        /// </summary>
        public static void Add(HashSet<string> tags, Pawn a, Pawn b, Pawn x = null, Faction faction = null, bool groups = true)
        {
            if (tags == null) return;
            try
            {
                if (a != null) Who(a, tags, "", groups);
                if (b != null && b != a) Who(b, tags, "b:", true);
                if (x != null)
                {
                    if (x.RaceProps.Animal) tags.Add("x:animal");
                    else if (x.DevelopmentalStage.Juvenile()) tags.Add("x:child");
                    else if (Flavor.HumanAge(x) >= 60f) tags.Add("x:old");
                    // Other mods: x:guest, x:faction:Empire, x:race:AA_MeadowAve, x:mage (Integrations/ModStrangers.cs).
                    ModStrangers.AddTags(tags, x);
                }
                var f = faction ?? (x != null && x.RaceProps.Humanlike ? x.Faction : null);
                if (f != null && !f.IsPlayer) tags.Add(f.def.techLevel <= TechLevel.Medieval ? "theirLowtech" : "theirHightech");
                // What their people remember of the colony: their:grateful, their:rescued, their:grudge, their:owed (Interplay/).
                Interplay.AddMemoryTags(tags, f);
                var character = CouncilGameComponent.Instance?.character;
                if (character != null)
                    foreach (var t in CharacterTracker.TagNames)
                        if (character.Is(t)) tags.Add("colony:" + t);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("letter tags", e);
            }
        }

        private static void Who(Pawn p, HashSet<string> tags, string prefix, bool groups = true)
        {
            foreach (var t in PeopleTags.Identity(p))
                if (groups || t.IndexOf(':') >= 0) tags.Add(prefix + t);
            foreach (var k in Pasts.Of(p)) tags.Add(prefix + "past:" + Pasts.Tag(k));
            var office = ColonyCouncilMod.Settings.offices ? CouncilGameComponent.Instance?.offices.OfficeOf(p) : null;
            if (office != null) tags.Add(prefix + "office:" + office.defName.Replace("Council_Office_", ""));
        }

        /// <summary>Who someone is, as <see cref="Add"/> tells it, for <see cref="DilemmaEffect_Needs.tags"/>.</summary>
        public static HashSet<string> Of(Pawn p)
        {
            var tags = new HashSet<string>();
            if (p == null) return tags;
            try
            {
                Who(p, tags, "");
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("letter tags", e);
            }
            return tags;
        }
    }

    /// <summary>How an answer turns out: its risk, and which way of telling the outcome fits.</summary>
    public static class DilemmaChoices
    {
        /// <summary>The option's risk, lowered by the best of A and B at its <see cref="DilemmaOption.riskSkill"/>.</summary>
        public static float RiskChance(DilemmaOption o, DilemmaContext ctx, float chance)
        {
            if (o?.riskSkill == null || ctx == null) return chance;
            int level = BestLevel(o.riskSkill, ctx.a, ctx.b);
            // A helper only ever lowers the risk (never below 5%, and never above what it was without them).
            return level <= 0 ? chance : Mathf.Min(chance, Mathf.Clamp(chance - level * 0.035f, 0.05f, 0.95f));
        }

        /// <summary>Lets the answer's <see cref="DilemmaEffect_Needs"/> pick their helper (B) before the risk is weighed.</summary>
        public static void PickHelpers(DilemmaOption o, DilemmaContext ctx)
        {
            if (o?.effects == null || ctx == null) return;
            foreach (var e in o.effects)
            {
                if (!(e is DilemmaEffect_Needs n) || !n.asB) continue;
                try
                {
                    n.Apply(ctx);
                }
                catch (System.Exception ex)
                {
                    CouncilLog.ErrorOnce("answer helper", ex);
                }
            }
        }

        public static int BestLevel(SkillDef skill, params Pawn[] pawns)
        {
            int best = 0;
            foreach (var p in pawns)
            {
                var s = p?.skills?.GetSkill(skill);
                if (s != null && !s.TotallyDisabled && s.Level > best) best = s.Level;
            }
            return best;
        }

        /// <summary>
        /// The outcome to tell: the risk's when it came true (if it has one), else the plain one, either way picked among
        /// its variants by tags. Null or empty when the option has nothing to say.
        /// </summary>
        public static string Outcome(DilemmaOption o, bool risk, HashSet<string> tags)
        {
            if (o == null) return null;
            bool riskText = risk && (!o.riskOutcome.NullOrEmpty() || !o.riskOutcomes.NullOrEmpty());
            string plain = riskText ? o.riskOutcome : o.outcome;
            var more = riskText ? o.riskOutcomes : o.outcomes;
            if (more.NullOrEmpty()) return plain;
            try
            {
                var lines = new List<string>();
                if (!plain.NullOrEmpty()) lines.Add(plain);
                lines.AddRange(more);
                return Flavor.Pick(lines, tags) ?? plain ?? Flavor.PickText(more);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("outcome variants", e);
                return plain;
            }
        }
    }

    /// <summary>
    /// A condition rather than an effect: the answer can only be given when the colony has what it takes, and this does
    /// nothing itself. Someone among the targets (A by default; Colony for anyone at home who is up and about) with
    /// <see cref="skill"/> at <see cref="minLevel"/> or better, who fits <see cref="tags"/> (as a line's tags without the
    /// brackets, with <see cref="LetterTags"/>' past: and office: too: "trait:Kind", "past:healer|skill:Medicine",
    /// "office:Physician"), and so much of <see cref="what"/> in storage (as for Consume, without taking it). With
    /// <see cref="asB"/>, whoever else fits best becomes B when the answer is given, so effects after this one can reach
    /// them (target B) and the outcome can name them ({B_nameDef}); only where the dilemma or event has no B of its own
    /// (an existing B is never replaced). Never put it on the default answer: that one is always possible.
    /// </summary>
    public class DilemmaEffect_Needs : DilemmaEffect
    {
        public SkillDef skill;
        public int minLevel;
        public string tags;
        public string what;
        public int count;
        public bool asB;
        /// <summary>What the greyed-out answer says when nobody fits (a default is made from the skill).</summary>
        [MustTranslate] public string reason;

        private bool Fits(Pawn p)
        {
            if (p == null || p.Dead || p.Downed || p.InMentalState || p.DevelopmentalStage.Baby()) return false;
            if (skill != null)
            {
                var s = p.skills?.GetSkill(skill);
                if (s == null || s.TotallyDisabled || s.Level < minLevel) return false;
            }
            if (!tags.NullOrEmpty() && Flavor.Match("[" + tags + "]", LetterTags.Of(p)) <= 0) return false;
            return true;
        }

        private List<Pawn> Candidates(DilemmaContext ctx)
        {
            float was = chance;
            chance = 1f;
            try
            {
                return Targets(ctx).Where(Fits).ToList();
            }
            finally
            {
                chance = was;
            }
        }

        private bool NeedsSomeone => skill != null || !tags.NullOrEmpty();

        private int Have(DilemmaContext ctx)
        {
            if (ctx.map == null) return 0;
            if (what == "silver") return ctx.map.resourceCounter.Silver;
            if (what == "rawfood") return DilemmaEffect_Consume.RawFood(ctx.map);
            if (what == "medicine") return DilemmaEffect_Consume.Medicine(ctx.map).Sum(t => t.stackCount);
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(what);
            return def == null ? 0 : ctx.map.resourceCounter.GetCount(def);
        }

        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx == null) return null;
            if (NeedsSomeone && Candidates(ctx).Count == 0)
            {
                if (!reason.NullOrEmpty()) return ctx.Format(reason);
                var one = target == DilemmaTarget.A ? ctx.a : target == DilemmaTarget.B ? ctx.b : null;
                if (skill != null)
                    return one != null
                        ? "CC_Needs_SkillOf".Translate(one.LabelShort, skill.LabelCap, minLevel).Resolve()
                        : "CC_Needs_Skill".Translate(skill.LabelCap, minLevel).Resolve();
                return "CC_Needs_Someone".Translate().Resolve();
            }
            if (!what.NullOrEmpty() && Have(ctx) < Mathf.Max(1, count))
            {
                string label = what == "silver" ? ThingDefOf.Silver.label
                    : what == "rawfood" ? "CC_Dilemma_RawFood".Translate().Resolve()
                    : what == "medicine" ? "CC_Dilemma_Medicine".Translate().Resolve()
                    : DefDatabase<ThingDef>.GetNamedSilentFail(what)?.label ?? what;
                return "CC_Dilemma_NotEnough".Translate(Mathf.Max(1, count), label).Resolve();
            }
            return null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            // B is taken (or A is the one asked for): nothing to pick. Without anyone else, {B_...} reads as A.
            if (!asB || ctx == null || ctx.b != null || target == DilemmaTarget.A || !NeedsSomeone) return;
            var helper = Candidates(ctx).Where(p => p != ctx.a)
                .OrderByDescending(p => skill != null ? p.skills.GetSkill(skill).Level : 0).ThenBy(_ => Rand.Value).FirstOrDefault();
            if (helper != null) ctx.b = helper;
        }
    }
}
