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

    /// <summary>Moments when a colonist may petition the council for a decree.</summary>
    public enum DecreeMoment
    {
        FoodLow,
        RecentThreat,
        Grief,
        ColonyHappy,
        ColonyMiserable,
        Spoiled,
        Dirty,
        Prisoners,
        Autumn
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
                return s != null && !s.TotallyDisabled && People.Passionate(s);
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
        /// <summary>When someone who likes it may ask the council for it (any one is enough).</summary>
        public List<DecreeMoment> requestWhen;
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

    /// <summary>A colonist asked the council for a decree.</summary>
    public class DecreeRequest : IExposable
    {
        public DecreeDef def;
        public Pawn pawn;
        public int tick;
        public string line;
        /// <summary>Asking for a decree in force to be lifted, rather than declared.</summary>
        public bool repeal;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref line, "line");
            Scribe_Values.Look(ref repeal, "repeal");
        }
    }

    public class DecreeTracker : IExposable
    {
        public const int MaxActive = 2;

        public List<ActiveDecree> active = new List<ActiveDecree>();
        private Dictionary<string, int> readyAt = new Dictionary<string, int>();
        public List<DecreeRequest> requests = new List<DecreeRequest>();
        private int lastRequestTick = -999999;
        public const int RequestDays = 5;

        public DecreeRequest RequestFor(DecreeDef def) => requests.FirstOrDefault(r => r.def == def);

        public bool IsActive(DecreeDef def) => active.Any(a => a.def == def);

        public ActiveDecree Get(DecreeDef def) => active.FirstOrDefault(a => a.def == def);

        public bool SuspendsWishes => active.Any(a => a.def.suspendsWishes);

        public int ReadyAt(DecreeDef def) => readyAt.TryGetValue(def.defName, out int t) ? t : 0;

        /// <summary>Why it can't be enacted right now, or null.</summary>
        public string CannotEnact(DecreeDef def)
        {
            if (IsActive(def)) return null;
            int now = Find.TickManager.TicksGame;
            if (!ColonyCouncilMod.Settings.MoodEffects) return "CC_Decree_NoMood".Translate();
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
            var req = RequestFor(def);
            if (req != null)
            {
                requests.Remove(req);
                if (req.pawn != null && !req.pawn.Dead)
                {
                    comp.standings.Change(req.pawn, 5f, "CC_Log_DecreeHeeded".Translate(def.label));
                    CouncilGameComponent.GiveThought(req.pawn, CouncilDefOf.Council_DecreeHeeded);
                }
            }
        }

        public void Repeal(DecreeDef def, CouncilGameComponent comp, bool early)
        {
            var a = Get(def);
            if (a == null) return;
            active.Remove(a);
            readyAt[def.defName] = Find.TickManager.TicksGame + Mathf.RoundToInt(def.cooldownDays * GenDate.TicksPerDay);
            SyncHediffs();
            comp.Notify_DecreeEnded(def, early);
            var req = RequestFor(def);
            if (req != null && req.repeal)
            {
                requests.Remove(req);
                if (early && req.pawn != null && !req.pawn.Dead)
                {
                    comp.standings.Change(req.pawn, 4f, "CC_Log_RepealHeeded".Translate(def.label));
                    CouncilGameComponent.GiveThought(req.pawn, CouncilDefOf.Council_DecreeHeeded);
                }
            }
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
                        if (rec != null && !rec.TotallyDisabled) rec.Learn(a.def.practiceXpPerHour * (CouncilGameComponent.Instance?.offices.DrillFactor ?? 1f));
                    }
                }
            }
        }

        /// <summary>Once a day: old petitions lapse (a little sourly), and someone may make a new one.</summary>
        public void DailyRequests(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            foreach (var r in requests.Where(r => r.def == null || r.pawn == null || r.pawn.Dead || now - r.tick > RequestDays * GenDate.TicksPerDay).ToList())
            {
                requests.Remove(r);
                if (r.def == null || r.pawn == null || r.pawn.Dead) continue;
                if (!r.repeal && !IsActive(r.def))
                    comp.standings.Change(r.pawn, -2f, "CC_Log_DecreeIgnored".Translate(r.def.label));
                else if (r.repeal && IsActive(r.def))
                    comp.standings.Change(r.pawn, -2f, "CC_Log_RepealIgnored".Translate(r.def.label));
            }
            if (!ColonyCouncilMod.Settings.decreeRequests || requests.Count >= 2 || now - lastRequestTick < GenDate.TicksPerDay * 3 || !Rand.Chance(0.35f)) return;
            var map = Find.Maps.Where(m => m.IsPlayerHome).OrderByDescending(m => m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();
            if (map == null) return;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && CouncilGameComponent.CanVoiceWishes(p)).ToList();
            if (pawns.Count == 0) return;
            // A decree half the colony resents: someone who hates it asks for it to be lifted.
            foreach (var a in active.InRandomOrder())
            {
                if (RequestFor(a.def) != null || now - a.startTick < GenDate.TicksPerDay) continue;
                int against = pawns.Count(p => a.def.ReactionOf(p) <= DecreeReaction.Dislike);
                if (against * 2 < pawns.Count) continue;
                var hater = pawns.Where(p => a.def.ReactionOf(p) == DecreeReaction.Hate).RandomElementWithFallback();
                if (hater == null) continue;
                string gripe = a.def.oppose.Pick(ToneUtility.ToneFor(hater), Flavor.TagsFor(hater));
                gripe = gripe.NullOrEmpty() ? null : gripe.Formatted(hater.Named("PAWN")).Resolve();
                requests.Add(new DecreeRequest { def = a.def, pawn = hater, tick = now, line = gripe, repeal = true });
                lastRequestTick = now;
                Messages.Message("CC_Decree_RepealRequested".Translate(hater.Named("PAWN"), a.def.label.Named("DECREE")).Resolve() + (gripe != null ? " \"" + TextUtil.ShortSpeech(gripe, 120) + "\"" : ""),
                    new LookTargets(hater), MessageTypeDefOf.NeutralEvent, false);
                CouncilGameComponent.Speak(hater, gripe, new Color(0.95f, 0.6f, 0.5f));
                return;
            }
            foreach (var def in DefDatabase<DecreeDef>.AllDefsListForReading.Where(d => !d.requestWhen.NullOrEmpty()).InRandomOrder())
            {
                if (IsActive(def) || RequestFor(def) != null || CannotEnact(def) != null) continue;
                if (!def.requestWhen.Any(m => MomentNow(m, map, pawns))) continue;
                var asker = pawns.Where(p => def.ReactionOf(p) == DecreeReaction.Love).RandomElementWithFallback()
                            ?? pawns.Where(p => def.ReactionOf(p) == DecreeReaction.Like).RandomElementWithFallback();
                if (asker == null) continue;
                string line = def.support.Pick(ToneUtility.ToneFor(asker), Flavor.TagsFor(asker));
                line = line.NullOrEmpty() ? null : line.Formatted(asker.Named("PAWN")).Resolve();
                requests.Add(new DecreeRequest { def = def, pawn = asker, tick = now, line = line });
                lastRequestTick = now;
                Messages.Message("CC_Decree_Requested".Translate(asker.Named("PAWN"), def.label.Named("DECREE")).Resolve() + (line != null ? " \"" + TextUtil.ShortSpeech(line, 120) + "\"" : ""),
                    new LookTargets(asker), MessageTypeDefOf.NeutralEvent, false);
                CouncilGameComponent.Speak(asker, line, new Color(0.9f, 0.85f, 0.6f));
                return;
            }
        }

        private static bool MomentNow(DecreeMoment m, Map map, List<Pawn> pawns)
        {
            switch (m)
            {
                case DecreeMoment.FoodLow:
                    return AdvisorWorker_Food.DaysOfFood(map) < 8f;
                case DecreeMoment.RecentThreat:
                    int last = map.storyState.LastThreatBigTick;
                    return last > 0 && Find.TickManager.TicksGame - last < GenDate.TicksPerDay * 3;
                case DecreeMoment.Grief:
                    return pawns.Any(Flavor.IsGrieving);
                case DecreeMoment.ColonyHappy:
                    return pawns.Average(p => p.needs?.mood?.CurLevelPercentage ?? 0.5f) > 0.75f;
                case DecreeMoment.ColonyMiserable:
                    return pawns.Average(p => p.needs?.mood?.CurLevelPercentage ?? 0.5f) < 0.4f;
                case DecreeMoment.Spoiled:
                    return pawns.Average(p => Standards.Of(p)) > 0.6f;
                case DecreeMoment.Dirty:
                    return WishWorker_Tidy.Filth(map) > 60;
                case DecreeMoment.Prisoners:
                    return map.mapPawns.PrisonersOfColonySpawnedCount > 0;
                case DecreeMoment.Autumn:
                    return GenLocalDate.Season(map) == Season.Fall;
                default:
                    return false;
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
                foreach (var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
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
            Scribe_Collections.Look(ref requests, "requests", LookMode.Deep);
            Scribe_Values.Look(ref lastRequestTick, "lastRequestTick", -999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (active == null) active = new List<ActiveDecree>();
                active.RemoveAll(a => a?.def == null);
                if (readyAt == null) readyAt = new Dictionary<string, int>();
                if (requests == null) requests = new List<DecreeRequest>();
                requests.RemoveAll(r => r?.def == null || r.pawn == null);
            }
        }
    }

    /// <summary>Decree moods: the bad ones follow the stakes setting and the penalty slider.</summary>
    public class Thought_Decree : Thought_Situational
    {
        public override float MoodOffset()
        {
            float m = base.MoodOffset();
            return m < 0f ? m * ColonyCouncilMod.Settings.ChosenPenaltyScale : m;
        }
    }

    /// <summary>How a colonist feels about the decrees in force: the thought's stages are hate, dislike, like, love.</summary>
    public class ThoughtWorker_Decree : ThoughtWorker
    {
        private DecreeDef decree;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var s = ColonyCouncilMod.Settings;
            if (!s.MoodEffects || !p.IsFreeNonSlaveColonist || p.DevelopmentalStage.Baby()) return ThoughtState.Inactive;
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
