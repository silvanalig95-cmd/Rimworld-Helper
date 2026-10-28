using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a reached life goal leaves behind, beside the letter, the mood and the trust (Aspirations.Fulfil; the names
    /// are GoalRewardMath.cs):
    /// <list type="bullet">
    /// <item>A toast: that evening those close to them gather at the table and one of them raises a glass (PlayOut/).</item>
    /// <item>An inspiration that fits the goal: surgery after a healer's, taming after a tamer's, a work frenzy otherwise.</item>
    /// <item>A mark on the skill the goal was about (<see cref="Aspiration.mark"/>): it never slips again, and they learn
    /// it a little faster (<see cref="GoalRewardMath.MarkLearnFactor"/>; Patch_GoalMarkDecay, Patch_GoalMarkLearn).</item>
    /// <item>Passing it on: a younger colonist with a passion for that skill spends some hours learning at their side.</item>
    /// <item>A title that stays: their Story tab names what they did, and it's told round the colony (a deed, Gossip).</item>
    /// </list>
    /// </summary>
    public static class GoalRewards
    {
        private static ThoughtDef T(string name) => DefDatabase<ThoughtDef>.GetNamedSilentFail(name);

        /// <summary>The skill a goal was about, for this person (the better of the two where a goal could be either), or null.</summary>
        public static SkillDef SkillOf(Aspiration a)
        {
            var d = a?.def;
            var p = a?.pawn;
            if (d == null || p?.skills == null) return null;
            string name = GoalRewardMath.SkillFor(a.skill?.defName ?? d.skill?.defName, d.record, d.counter, d.office?.defName, d.workerClass?.Name);
            if (name == GoalRewardMath.Combat) name = Better(p, SkillDefOf.Shooting, SkillDefOf.Melee);
            else if (name == GoalRewardMath.CraftOrArt) name = Better(p, SkillDefOf.Crafting, SkillDefOf.Artistic);
            var skill = name == null ? null : DefDatabase<SkillDef>.GetNamedSilentFail(name);
            var rec = skill == null ? null : p.skills.GetSkill(skill);
            return rec == null || rec.TotallyDisabled ? null : skill;
        }

        private static string Better(Pawn p, SkillDef x, SkillDef y) =>
            (p.skills.GetSkill(y)?.Level ?? 0) > (p.skills.GetSkill(x)?.Level ?? 0) ? y.defName : x.defName;

        // ---------------- The mark on the skill ----------------

        /// <summary>The skills marked by reached goals, per person (worked out once, again after a goal is reached or a load).</summary>
        private static Dictionary<Pawn, HashSet<SkillDef>> marks;

        public static void Reset() => marks = null;

        public static bool Marked(Pawn p, SkillDef skill)
        {
            if (p == null || skill == null) return false;
            if (marks == null) Build();
            return marks.TryGetValue(p, out var set) && set.Contains(skill);
        }

        /// <summary>The skills marked for this person (for their Story tab).</summary>
        public static IEnumerable<SkillDef> MarksOf(Pawn p)
        {
            if (p == null) return Enumerable.Empty<SkillDef>();
            if (marks == null) Build();
            return marks.TryGetValue(p, out var set) ? set : Enumerable.Empty<SkillDef>();
        }

        private static void Build()
        {
            marks = new Dictionary<Pawn, HashSet<SkillDef>>();
            var all = CouncilGameComponent.Instance?.aspirations?.all;
            if (all == null) return;
            foreach (var a in all)
            {
                if (a == null || !a.done || a.pawn == null) continue;
                // A goal reached before marks were kept is marked now, by the skill it was about.
                a.mark ??= SkillOf(a);
                if (a.mark == null) continue;
                if (!marks.TryGetValue(a.pawn, out var set)) marks[a.pawn] = set = new HashSet<SkillDef>();
                set.Add(a.mark);
            }
        }

        // ---------------- When it's reached ----------------

        /// <summary>
        /// The goal is reached: its mark, an inspiration, the evening's toast, a lesson for someone who shares the
        /// passion, and the deed told round the colony. What happens, as lines for the letter.
        /// </summary>
        public static List<string> Reached(CouncilGameComponent comp, Aspiration a)
        {
            var lines = new List<string>();
            var p = a?.pawn;
            if (comp == null || p == null || p.Dead) return lines;
            a.mark = SkillOf(a);
            Reset();
            Safe("life goal inspiration", () => Inspire(a));
            Safe("life goal toast", () =>
            {
                if (Toast(a)) lines.Add("CC_Asp_Reward_Toast".Translate(p.Named("PAWN")).Resolve());
            });
            if (a.mark != null) lines.Add("CC_Asp_Reward_Mark".Translate(p.Named("PAWN"), a.mark.label.Named("SKILL")).Resolve());
            Safe("life goal lesson", () =>
            {
                var student = PassOn(a);
                if (student != null) lines.Add("CC_Asp_Reward_Lesson".Translate(p.Named("PAWN"), student.Named("OTHER"), a.mark.label.Named("SKILL")).Resolve());
            });
            Safe("life goal deed", () =>
            {
                var heard = PawnsFinder.AllMaps_FreeColonists.Where(f => f != p && f.relations != null && f.relations.OpinionOf(p) >= 20).ToList();
                Deeds.Record("Rimfolk_Deed_GoalReached", p, null, -1f, null, a.Deed, heard, false);
                lines.Add("CC_Asp_Reward_Title".Translate(p.Named("PAWN"), a.Deed.Named("DEED")).Resolve());
            });
            return lines;
        }

        /// <summary>Always an inspiration, the one that fits the goal where it can come; else a work frenzy, else any.</summary>
        private static void Inspire(Aspiration a)
        {
            var p = a.pawn;
            var h = p.mindState?.inspirationHandler;
            if (h == null || h.Inspired) return;
            string name = GoalRewardMath.InspirationFor(a.mark?.defName, a.def.record == "PrisonersRecruited");
            foreach (var d in new[] { DefDatabase<InspirationDef>.GetNamedSilentFail(name), DefDatabase<InspirationDef>.GetNamedSilentFail("Frenzy_Work") })
                if (d != null && d.Worker.InspirationCanOccur(p) && h.TryStartInspiration(d, "CC_Asp_InspiredBy".Translate(a.Goal).Resolve(), true)) return;
            var any = h.GetRandomAvailableInspirationDef();
            if (any != null) h.TryStartInspiration(any, null, true);
        }

        /// <summary>That evening, those close to them gather at the table; the one who thinks best of them raises a glass.</summary>
        private static bool Toast(Aspiration a)
        {
            var p = a.pawn;
            if (!PlayOuts.On || !p.Spawned || !p.Map.IsPlayerHome) return false;
            var map = p.Map;
            var close = map.mapPawns.FreeColonistsSpawned.Where(o => o != p && !o.IsQuestLodger() && People.HasInnerLife(o) && o.relations != null
                                                                     && (o.relations.OpinionOf(p) >= 20 || Family.Of(o, p) || LovePartnerRelationUtility.LovePartnerRelationExists(o, p))).ToList();
            if (close.Count == 0) return false;
            var friend = close.OrderByDescending(o => o.relations.OpinionOf(p)).First();
            var lines = new List<string>
            {
                "CC_Asp_Toast_1".Translate(p.Named("A"), a.Deed.Named("DEED")).Resolve(),
                "CC_Asp_Toast_2".Translate(p.Named("A")).Resolve(),
                "CC_Asp_Toast_3".Translate(p.Named("A")).Resolve()
            };
            var play = new DilemmaEffect_Toast
            {
                target = DilemmaTarget.CloseToA, organizer = PlayWho.B, hours = new IntRange(17, 21), waitHours = 4f, lines = lines,
                label = "CC_Asp_ToastLabel".Translate(p.Named("A")).Resolve()
            }.Begin(new DilemmaContext { map = map, a = p, b = friend });
            if (play == null) return false;
            string cause = "CC_Asp_ToastCause".Translate(p.Named("A"), a.Goal.Named("GOAL")).Resolve();
            var honoured = T("Council_Asp_Toasted");
            var glass = T("Council_Asp_RaisedGlass");
            foreach (var m in play.invited)
                if (m == p ? honoured != null : glass != null)
                    play.moods.Add(new HeldMood { pawn = m, thought = m == p ? honoured : glass, about = m == p ? null : p, cause = cause });
            return true;
        }

        /// <summary>
        /// A younger colonist at home with a passion for the marked skill, and less of it, spends some hours learning at
        /// their side (a lesson, PlayOut/): the one who'd learn most, the closest to them first. Null if nobody.
        /// </summary>
        private static Pawn PassOn(Aspiration a)
        {
            var p = a.pawn;
            var skill = a.mark;
            if (skill == null || !PlayOuts.On || !p.Spawned || !p.Map.IsPlayerHome) return null;
            var mine = p.skills.GetSkill(skill);
            if (mine == null) return null;
            var student = p.Map.mapPawns.FreeColonistsSpawned
                .Where(o => o != p && !o.IsQuestLodger() && People.HasInnerLife(o) && !o.DevelopmentalStage.Baby() && !o.Downed
                            && o.ageTracker.AgeBiologicalTicks < p.ageTracker.AgeBiologicalTicks
                            && o.skills?.GetSkill(skill) is SkillRecord r && !r.TotallyDisabled && r.passion != Passion.None && r.Level < mine.Level)
                .OrderByDescending(o => (o.relations?.OpinionOf(p) ?? 0) + (Family.Of(o, p) ? 40 : 0)).FirstOrDefault();
            if (student == null) return null;
            var play = new DilemmaEffect_Lesson
            {
                teacher = PlayWho.B, student = PlayWho.A, skill = skill, hours = new IntRange(9, 17), duration = new FloatRange(2f, 3f), waitHours = 30f,
                label = "CC_Asp_LessonLabel".Translate(p.Named("B"), skill.label.Named("SKILL")).Resolve()
            }.Begin(new DilemmaContext { map = p.Map, a = student, b = p, skill = skill });
            if (play == null) return null;
            string cause = "CC_Asp_LessonCause".Translate(student.Named("A"), p.Named("B"), skill.label.Named("SKILL")).Resolve();
            play.moods.Add(new HeldMood { pawn = student, thought = T("Council_Asp_LearnedFromThem"), about = p, cause = cause });
            play.moods.Add(new HeldMood { pawn = p, thought = T("Council_Asp_PassedItOn"), about = student, cause = cause });
            play.moods.RemoveAll(m => m.thought == null);
            return student;
        }

        private static void Safe(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce(what, e);
            }
        }
    }

    /// <summary>A skill marked by a reached life goal never slips (GoalRewards). Only with the deeper game hooks.</summary>
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Interval))]
    public static class Patch_GoalMarkDecay
    {
        public static bool Prefix(SkillRecord __instance, Pawn ___pawn)
        {
            if (!RimfolkPatches.On || __instance.Level < 10) return true;
            try
            {
                return !GoalRewards.Marked(___pawn, __instance.def);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a marked skill", e);
                return true;
            }
        }
    }

    /// <summary>A skill marked by a reached life goal is learned a little faster (GoalRewards).</summary>
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.LearnRateFactor))]
    public static class Patch_GoalMarkLearn
    {
        public static void Postfix(SkillRecord __instance, Pawn ___pawn, ref float __result)
        {
            if (!RimfolkPatches.On || __result <= 0f) return;
            try
            {
                if (GoalRewards.Marked(___pawn, __instance.def)) __result *= GoalRewardMath.MarkLearnFactor;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a marked skill's learning", e);
            }
        }
    }
}
