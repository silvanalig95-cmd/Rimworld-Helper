using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Vanilla Skills Expanded's expertise, read by reflection by type name: VSE.ExpertiseTrackers.Expertise(pawn)
    /// gives a tracker whose AllExpertise lists records with a def (its skill) and a level; VSE.SkillsMod.Settings
    /// holds how many a colonist may have (MaxExpertise, 1 by default) and the skill level that unlocks one
    /// (LevelToGetExpertise, 15). Without the mod, every lookup finds nothing and the life goal never comes up.
    /// </summary>
    public static class Expertise
    {
        private static bool looked;
        private static MethodInfo expertiseOf;
        private static PropertyInfo allExpertise;
        private static FieldInfo recordDef, defSkill;
        private static object settings;
        private static FieldInfo maxField, levelField;
        private static Type defType;
        private static MethodInfo addExpertise;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                var trackers = GenTypes.GetTypeInAnyAssembly("VSE.ExpertiseTrackers");
                expertiseOf = trackers?.GetMethod("Expertise", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Pawn) }, null);
                allExpertise = GenTypes.GetTypeInAnyAssembly("VSE.ExpertiseTracker")?.GetProperty("AllExpertise", BindingFlags.Public | BindingFlags.Instance);
                recordDef = GenTypes.GetTypeInAnyAssembly("VSE.ExpertiseRecord")?.GetField("def", BindingFlags.Public | BindingFlags.Instance);
                defType = GenTypes.GetTypeInAnyAssembly("VSE.Expertise.ExpertiseDef");
                defSkill = defType?.GetField("skill", BindingFlags.Public | BindingFlags.Instance);
                // Giving one (the peoples' events, round 4: a corresponding fellow's Researcher, a page's Hunter): the tracker's
                // own Add... taking the def first, any further arguments optional.
                addExpertise = defType == null ? null : GenTypes.GetTypeInAnyAssembly("VSE.ExpertiseTracker")?.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name.StartsWith("Add", StringComparison.Ordinal) && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(defType)
                                         && m.GetParameters().Skip(1).All(x => x.IsOptional));
                var mod = GenTypes.GetTypeInAnyAssembly("VSE.SkillsMod");
                settings = mod?.GetField("Settings", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                maxField = settings?.GetType().GetField("MaxExpertise", BindingFlags.Public | BindingFlags.Instance);
                levelField = settings?.GetType().GetField("LevelToGetExpertise", BindingFlags.Public | BindingFlags.Instance);
            }
            catch
            {
                expertiseOf = null;
            }
        }

        public static bool Available
        {
            get
            {
                Look();
                return expertiseOf != null && allExpertise != null && recordDef != null && defSkill != null;
            }
        }

        private static IList Records(Pawn p)
        {
            if (!Available || p?.skills == null) return null;
            try
            {
                var tracker = expertiseOf.Invoke(null, new object[] { p });
                return tracker == null ? null : allExpertise.GetValue(tracker) as IList;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>How many expertises they have, or -1 if that can't be read.</summary>
        public static int Count(Pawn p) => Records(p)?.Count ?? -1;

        /// <summary>Whether one of their expertises belongs to this skill.</summary>
        public static bool HasFor(Pawn p, SkillDef skill)
        {
            var list = Records(p);
            if (list == null || skill == null) return false;
            foreach (var r in list)
            {
                var def = r == null ? null : recordDef.GetValue(r);
                if (def != null && defSkill.GetValue(def) as SkillDef == skill) return true;
            }
            return false;
        }

        /// <summary>One of VSE's expertises by name (Researcher, Hunter), or null without the mod.</summary>
        public static Def Named(string defName)
        {
            Look();
            return defType == null || defName == null ? null : GenDefDatabase.GetDefSilentFail(defType, defName, false);
        }

        /// <summary>This expertise could be given to them now: the mod's own way of adding one found, the def there, none for its skill yet, room for one more.</summary>
        public static bool CanGive(Pawn p, string defName)
        {
            if (!Available || addExpertise == null || p?.skills == null) return false;
            var def = Named(defName);
            if (def == null || HasFor(p, defSkill.GetValue(def) as SkillDef)) return false;
            int n = Count(p);
            return n >= 0 && n < Max;
        }

        /// <summary>Gives them this expertise by the mod's own way (a people's gift); true when they hold it afterwards.</summary>
        public static bool Give(Pawn p, string defName)
        {
            if (!CanGive(p, defName)) return false;
            var def = Named(defName);
            try
            {
                var tracker = expertiseOf.Invoke(null, new object[] { p });
                if (tracker == null) return false;
                var args = addExpertise.GetParameters().Select((x, i) => i == 0 ? (object)def : x.DefaultValue).ToArray();
                addExpertise.Invoke(tracker, args);
                return HasFor(p, defSkill.GetValue(def) as SkillDef);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("expertise given", e);
                return false;
            }
        }

        /// <summary>Most expertises a colonist may hold (VSE's setting; 1 if unreadable).</summary>
        public static int Max
        {
            get
            {
                Look();
                return maxField?.GetValue(settings) is int n ? n : 1;
            }
        }

        /// <summary>The skill level that unlocks an expertise (VSE's setting; 15 if unreadable).</summary>
        public static int UnlockLevel
        {
            get
            {
                Look();
                return levelField?.GetValue(settings) is int n ? n : 15;
            }
        }
    }

    /// <summary>
    /// Life goal (Vanilla Skills Expanded): earn an expertise in a skill they love and are getting close to unlocking
    /// one in. Done when they hold an expertise in that skill; the player picks it in VSE's skills tab when it comes up.
    /// </summary>
    public class AspirationWorker_Expertise : AspirationWorker
    {
        private SkillDef Pick(Pawn p)
        {
            if (!Expertise.Available || p.skills == null) return null;
            int count = Expertise.Count(p);
            if (count < 0 || count >= Expertise.Max) return null;
            int unlock = Expertise.UnlockLevel;
            SkillRecord best = null;
            foreach (var s in p.skills.skills)
            {
                if (s.TotallyDisabled || !People.Passionate(s) || s.Level < unlock - 6 || Expertise.HasFor(p, s.def)) continue;
                if (best == null || s.Level > best.Level) best = s;
            }
            return best?.def;
        }

        public override bool CanHave(Pawn p) => Pick(p) != null;

        public override bool Init(Aspiration a)
        {
            a.skill = Pick(a.pawn);
            var rec = a.skill != null ? a.pawn.skills?.GetSkill(a.skill) : null;
            if (rec == null) return false;
            a.baseline = rec.Level;
            a.target = Mathf.Max(rec.Level, Expertise.UnlockLevel);
            return true;
        }

        public override float Progress(Aspiration a)
        {
            if (Expertise.HasFor(a.pawn, a.skill)) return 1f;
            var rec = a.skill != null ? a.pawn.skills?.GetSkill(a.skill) : null;
            if (rec == null) return 0f;
            // The skill gets them most of the way; choosing the expertise is the last step.
            float span = Mathf.Max(1f, a.target - a.baseline);
            return Mathf.Clamp01((rec.Level - a.baseline) / span) * 0.9f;
        }

        public override bool Done(Aspiration a) => Expertise.HasFor(a.pawn, a.skill);

        public override bool StillPossible(Aspiration a)
        {
            if (!Expertise.Available || a.skill == null) return false;
            var rec = a.pawn.skills?.GetSkill(a.skill);
            if (rec == null || rec.TotallyDisabled) return false;
            return Expertise.HasFor(a.pawn, a.skill) || Expertise.Count(a.pawn) < Expertise.Max;
        }

        public override string ProgressText(Aspiration a)
        {
            var rec = a.skill != null ? a.pawn.skills?.GetSkill(a.skill) : null;
            if (rec != null && rec.Level >= a.target) return "CC_PM_Asp_ExpertiseReady".Translate(a.skill.label).Resolve();
            return "CC_Asp_SkillProgress".Translate(a.skill?.label ?? "", rec?.Level ?? 0, a.target.ToString("0")).Resolve();
        }
    }
}
