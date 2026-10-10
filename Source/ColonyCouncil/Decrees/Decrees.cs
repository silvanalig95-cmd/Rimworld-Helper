using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    public enum DecreeReaction
    {
        Hate,
        Dislike,
        Neutral,
        Like,
        Love
    }

    /// <summary>Who feels how about a decree: by trait (and degree), by passion for a skill, or by being unable to fight.</summary>
    public class DecreeReactionRule
    {
        public TraitDef trait;
        /// <summary>Trait degree to match; leave out to match any degree.</summary>
        public int degree = int.MinValue;
        public SkillDef passion;
        public bool nonViolent;
        /// <summary>Someone who lost a friend or family member lately.</summary>
        public bool grieving;
        public DecreeReaction reaction = DecreeReaction.Neutral;

        public bool Matches(Pawn p)
        {
            if (trait != null)
            {
                var traits = p.story?.traits;
                if (traits == null || !traits.HasTrait(trait)) return false;
                if (degree != int.MinValue && traits.DegreeOfTrait(trait) != degree) return false;
                return true;
            }
            if (passion != null)
            {
                var s = p.skills?.GetSkill(passion);
                return s != null && !s.TotallyDisabled && s.passion != Passion.None;
            }
            if (nonViolent) return p.WorkTagIsDisabled(WorkTags.Violent);
            if (grieving) return Flavor.IsGrieving(p);
            return false;
        }
    }

    /// <summary>
    /// A policy the council can put in force for a few days: trade the colony's good mood for work, food,
    /// training or learning (or the reverse). Each colonist reacts according to their nature.
    /// </summary>
    public class DecreeDef : Def
    {
        public float durationDays = 5f;
        public float cooldownDays = 7f;
        /// <summary>Applied to every free colonist while the decree holds.</summary>
        public HediffDef hediff;
        /// <summary>Situational thought; its stages are used as Hate, Dislike, Like, Love.</summary>
        public ThoughtDef thought;
        public DecreeReaction defaultReaction = DecreeReaction.Dislike;
        /// <summary>First match wins.</summary>
        public List<DecreeReactionRule> reactions = new List<DecreeReactionRule>();
        /// <summary>One line per effect, for the decrees tab.</summary>
        [MustTranslate] public List<string> effects = new List<string>();
        /// <summary>No new wishes while it holds.</summary>
        public bool suspendsWishes;
        /// <summary>Change to everyone's standards per day (austerity lowers them, festivals raise them).</summary>
        public float standardsPerDay;
        /// <summary>Hourly skill practice for everyone who can (drills, lessons).</summary>
        public List<SkillDef> practiceSkills;
        public float practiceXpPerHour;
        public bool practiceNeedsViolence;
        /// <summary>Only when someone in the colony is grieving (a day of mourning).</summary>
        public bool needsGrief;
        /// <summary>Fraction of grief memories' duration that passes each day the decree holds.</summary>
        public float griefFadePerDay;
        public ToneLines support = new ToneLines();
        public ToneLines oppose = new ToneLines();
        public int order;

        public DecreeReaction ReactionOf(Pawn p)
        {
            foreach (var r in reactions)
                if (r.Matches(p)) return r.reaction;
            return defaultReaction;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (thought != null && (thought.stages == null || thought.stages.Count < 4)) yield return "decree thought needs 4 stages (hate, dislike, like, love)";
        }
    }

    public class ActiveDecree : IExposable
    {
        public DecreeDef def;
        public int startTick;
        public int endTick;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref endTick, "endTick");
        }
    }

    public class DecreeTracker : IExposable
    {
        public const int MaxActive = 2;

        public List<ActiveDecree> active = new List<ActiveDecree>();
        private Dictionary<string, int> readyAt = new Dictionary<string, int>();

        public bool IsActive(DecreeDef def) => active.Any(a => a.def == def);

        public ActiveDecree Get(DecreeDef def) => active.FirstOrDefault(a => a.def == def);

        public bool SuspendsWishes => active.Any(a => a.def.suspendsWishes);

        public int ReadyAt(DecreeDef def) => readyAt.TryGetValue(def.defName, out int t) ? t : 0;

        /// <summary>Why it can't be enacted right now, or null.</summary>
        public string CannotEnact(DecreeDef def)
        {
            if (IsActive(def)) return null;
            int now = Find.TickManager.TicksGame;
            if (ReadyAt(def) > now) return "CC_Decree_Cooldown".Translate(TextUtil.DaysString(ReadyAt(def) - now));
            if (active.Count >= MaxActive) return "CC_Decree_TooMany".Translate(MaxActive);
            if (def.needsGrief && !PawnsFinder.AllMaps_FreeColonists.Any(Flavor.IsGrieving)) return "CC_Decree_NeedsGrief".Translate();
            return null;
        }

        public void Enact(DecreeDef def, CouncilGameComponent comp)
        {
            if (def == null || CannotEnact(def) != null || IsActive(def)) return;
            int now = Find.TickManager.TicksGame;
            active.Add(new ActiveDecree { def = def, startTick = now, endTick = now + Mathf.RoundToInt(def.durationDays * GenDate.TicksPerDay) });
            SyncHediffs();
            comp.Notify_DecreeEnacted(def);
        }

        public void Repeal(DecreeDef def, CouncilGameComponent comp, bool early)
        {
            var a = Get(def);
            if (a == null) return;
            active.Remove(a);
            readyAt[def.defName] = Find.TickManager.TicksGame + Mathf.RoundToInt(def.cooldownDays * GenDate.TicksPerDay);
            SyncHediffs();
            comp.Notify_DecreeEnded(def, early);
        }

        public void Hourly(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var a in active.Where(x => now >= x.endTick).ToList()) Repeal(a.def, comp, false);
            SyncHediffs();
            foreach (var a in active)
            {
                if (a.def.practiceSkills == null || a.def.practiceXpPerHour <= 0f) continue;
                foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned)
                {
                    if (p.skills == null || p.Downed || p.InBed() || p.InMentalState) continue;
                    if (a.def.practiceNeedsViolence && p.WorkTagIsDisabled(WorkTags.Violent)) continue;
                    foreach (var sk in a.def.practiceSkills)
                    {
                        var rec = p.skills.GetSkill(sk);
                        if (rec != null && !rec.TotallyDisabled) rec.Learn(a.def.practiceXpPerHour);
                    }
                }
            }
        }

        /// <summary>Once a day: grief fades faster under a day of mourning.</summary>
        public void Daily()
        {
            foreach (var a in active)
            {
                if (a.def.griefFadePerDay <= 0f) continue;
                foreach (var p in PawnsFinder.AllMaps_FreeColonists)
                {
                    var mem = p.needs?.mood?.thoughts?.memories;
                    if (mem == null) continue;
                    foreach (var m in mem.Memories)
                    {
                        if (!Flavor.GriefThoughts.Contains(m.def.defName)) continue;
                        int duration = m.DurationTicks;
                        if (duration > 0) m.age = Mathf.Min(duration - 1, m.age + (int)(duration * a.def.griefFadePerDay));
                    }
                }
            }
        }

        /// <summary>Puts the decrees' hediffs on everyone they apply to, and takes off the ones that ended.</summary>
        public void SyncHediffs()
        {
            var wanted = new HashSet<HediffDef>(active.Where(a => a.def.hediff != null).Select(a => a.def.hediff));
            foreach (var def in DefDatabase<DecreeDef>.AllDefsListForReading)
            {
                var h = def.hediff;
                if (h == null) continue;
                bool on = wanted.Contains(h);
                foreach (var p in PawnsFinder.AllMaps_FreeColonists)
                {
                    var set = p.health?.hediffSet;
                    if (set == null) continue;
                    var existing = set.GetFirstHediffOfDef(h);
                    bool applies = on && !p.IsQuestLodger();
                    if (applies && existing == null) p.health.AddHediff(HediffMaker.MakeHediff(h, p));
                    else if (!applies && existing != null) p.health.RemoveHediff(existing);
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref active, "active", LookMode.Deep);
            Scribe_Collections.Look(ref readyAt, "readyAt", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (active == null) active = new List<ActiveDecree>();
                active.RemoveAll(a => a?.def == null);
                if (readyAt == null) readyAt = new Dictionary<string, int>();
            }
        }
    }

    /// <summary>How a colonist feels about the decrees in force: the thought's stages are hate, dislike, like, love.</summary>
    public class ThoughtWorker_Decree : ThoughtWorker
    {
        private DecreeDef decree;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var s = ColonyCouncilMod.Settings;
            if (!s.MoodEffects || !p.IsFreeNonSlaveColonist) return ThoughtState.Inactive;
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return ThoughtState.Inactive;
            if (decree == null) decree = DefDatabase<DecreeDef>.AllDefsListForReading.FirstOrDefault(d => d.thought == def);
            if (decree == null || !comp.decrees.IsActive(decree)) return ThoughtState.Inactive;
            switch (decree.ReactionOf(p))
            {
                case DecreeReaction.Hate: return ThoughtState.ActiveAtStage(0);
                case DecreeReaction.Dislike: return ThoughtState.ActiveAtStage(1);
                case DecreeReaction.Like: return ThoughtState.ActiveAtStage(2);
                case DecreeReaction.Love: return ThoughtState.ActiveAtStage(3);
                default: return ThoughtState.Inactive;
            }
        }
    }
}
