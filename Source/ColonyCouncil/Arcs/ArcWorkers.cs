using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Someone a story could be about right now, and what it would be about.</summary>
    public class ArcCandidate
    {
        public Pawn pawn;
        public Pawn other;
        public SkillDef skill;
        public float weight = 1f;
        public string tag;
        public string lostName;
        public string name;
        public string goal;
        public string goalText;
        public Faction faction;
        public string quirk;
        public int awayDays;
    }

    /// <summary>
    /// What a personal story starts from: a worker looks at one colonist and says whether the story could begin for
    /// them now, and with whom. Each reads one of Rimfolk's systems (or the game's own): never a roll alone.
    /// </summary>
    public class ArcWorker
    {
        public ArcDef def;

        /// <summary>The story for this colonist now, or null. <paramref name="home"/>: everyone at home who could be in it.</summary>
        public virtual ArcCandidate Candidate(Pawn p, List<Pawn> home) => null;

        /// <summary>Whether it can begin for someone away from home (only the long way home).</summary>
        public virtual bool StartsAway => false;

        /// <summary>Whether its gates wait while the colonist is away (most do; a homecoming doesn't).</summary>
        public virtual bool PausesAway => true;

        /// <summary>Why it can't go on (a tag for its fizzle lines), or null.</summary>
        public virtual string Broken(Arc arc) => null;

        /// <summary>A gate of kind Custom.</summary>
        public virtual bool CustomGate(Arc arc, ArcGate gate) => false;
    }

    /// <summary>Helpers the workers and the tracker share.</summary>
    public static class ArcUtil
    {
        public static int Opinion(Pawn a, Pawn b) => a?.relations != null && b != null ? a.relations.OpinionOf(b) : 0;

        public static bool Partners(Pawn a, Pawn b) =>
            a?.relations != null && b != null && (a.relations.DirectRelationExists(PawnRelationDefOf.Spouse, b) || a.relations.DirectRelationExists(PawnRelationDefOf.Lover, b)
                                                  || a.relations.DirectRelationExists(PawnRelationDefOf.Fiance, b));

        public static bool Kin(Pawn a, Pawn b) => a?.relations != null && b != null && a.GetRelations(b).Any(r => r.familyByBloodRelation);

        /// <summary>How close a is to b: opinion, the social web's warmth, and family or partner on top.</summary>
        public static float Closeness(Pawn a, Pawn b)
        {
            if (a == null || b == null || a == b) return -999f;
            float c = Opinion(a, b) + (CouncilGameComponent.Instance?.social?.Warmth(a, b) ?? 0f);
            if (Partners(a, b)) c += 60f;
            else if (Kin(a, b)) c += 30f;
            return c;
        }

        /// <summary>The person among these whom p is closest to, at least this close, or null.</summary>
        public static Pawn Closest(Pawn p, IEnumerable<Pawn> among, float atLeast, Pawn except = null)
        {
            Pawn best = null;
            float bestScore = atLeast;
            foreach (var o in among)
            {
                if (o == null || o == p || o == except || o.Dead) continue;
                float c = Closeness(p, o);
                if (c < bestScore) continue;
                best = o;
                bestScore = c;
            }
            return best;
        }

        public static bool Psychopath(Pawn p) => p?.story?.traits?.HasTrait(TraitDefOf.Psychopath) ?? false;

        public static bool HasTrait(Pawn p, TraitDef t) => t != null && (p?.story?.traits?.HasTrait(t) ?? false);

        public static SkillRecord Skill(Pawn p, SkillDef s) => s != null ? p?.skills?.GetSkill(s) : null;

        public static int Level(Pawn p, SkillDef s) => Skill(p, s) is SkillRecord r && !r.TotallyDisabled ? r.Level : 0;

        public static float Xp(Pawn p, SkillDef s) => Skill(p, s) is SkillRecord r && !r.TotallyDisabled ? r.XpTotalEarned : 0f;

        public static float Years(Pawn p) => (p?.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) / GenDate.TicksPerYear;

        public static float DaysHere(Pawn p) => (p?.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) / GenDate.TicksPerDay;

        public static bool AtHome(Pawn p) => p != null && p.Spawned && p.Map != null && p.Map.IsPlayerHome;

        /// <summary>A recent memory of p about someone, from these (defNames), at most this many days old.</summary>
        public static Thought_Memory MemoryAbout(Pawn p, List<string> names, float days, System.Func<Pawn, bool> otherOk)
        {
            var memories = p?.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null || names.NullOrEmpty()) return null;
            Thought_Memory best = null;
            foreach (var m in memories)
            {
                if (m?.def == null || m.otherPawn == null || m.age > days * GenDate.TicksPerDay || !names.Contains(m.def.defName)) continue;
                if (!otherOk(m.otherPawn)) continue;
                if (best == null || m.age < best.age) best = m;
            }
            return best;
        }

        /// <summary>Whether p remembers one of these about o, given after this tick.</summary>
        public static bool RemembersSince(Pawn p, Pawn o, string[] names, int sinceTick)
        {
            var memories = p?.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null || o == null) return false;
            int now = Find.TickManager.TicksGame;
            foreach (var m in memories)
                if (m?.def != null && m.otherPawn == o && now - m.age >= sinceTick && names.Contains(m.def.defName)) return true;
            return false;
        }

        /// <summary>A fight between them: what makes a mending story fall apart.</summary>
        public static readonly string[] Fights = { "HadAngeringFight", "HadCatharticFight" };

        /// <summary>A sharp word between them: what a few quiet days must be free of.</summary>
        public static readonly string[] Frictions = { "Insulted", "InsultedMood", "Slighted", "HadAngeringFight", "HadCatharticFight", "Council_Friction_Accused", "Council_Friction_TalkedAboutMe", "Council_Friction_SilentTreatment" };

        private static readonly Dictionary<string, FeelingDef> feelings = new Dictionary<string, FeelingDef>();

        /// <summary>A feeling they have now, by its key (Homesick, Gratitude, Fear...); null while feelings are off.</summary>
        public static FeelingNow Feeling(Pawn p, string key)
        {
            var h = Heart.Of(p);
            if (h == null || h.feelings == null) return null;
            if (!feelings.TryGetValue(key, out var def))
            {
                def = DefDatabase<FeelingDef>.AllDefsListForReading.FirstOrDefault(d => d.key == key);
                feelings[key] = def;
            }
            return def == null ? null : h.Feeling(def);
        }

        /// <summary>The heart's need, 0 to 100, or -1 while feelings are off or not worked out yet.</summary>
        public static float Need(Pawn p, HeartNeed need)
        {
            var h = Heart.Of(p);
            if (h?.needs == null || (int)need >= h.needs.Length) return -1f;
            return h.needs[(int)need].value;
        }

        public static void ResetCaches() => feelings.Clear();

        /// <summary>The skill they're best at among these (not disabled), or null.</summary>
        public static SkillDef Best(Pawn p, IEnumerable<SkillDef> among, int minLevel = 0, bool preferPassion = true)
        {
            SkillDef best = null;
            float score = -1f;
            foreach (var s in among)
            {
                var r = Skill(p, s);
                if (r == null || r.TotallyDisabled || r.Level < minLevel) continue;
                float v = r.Level + (preferPassion ? People.PassionLevel(r) * 3f : 0f);
                if (v <= score) continue;
                best = s;
                score = v;
            }
            return best;
        }

        public static IEnumerable<SkillDef> AllSkills => DefDatabase<SkillDef>.AllDefsListForReading;
    }

    // =====================================================================================================
    // The workers
    // =====================================================================================================

    /// <summary>The long way home: away on a journey for days, with someone they love waiting at home.</summary>
    public class ArcWorker_Away : ArcWorker
    {
        public override bool StartsAway => true;

        public override bool PausesAway => false;

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (p.MapHeld != null && p.MapHeld.IsPlayerHome) return null;
            // On a journey (a caravan, or a map that isn't home), not posted at an outpost for good.
            if (p.GetCaravan() == null && !(p.MapHeld != null && !p.MapHeld.IsPlayerHome)) return null;
            int away = CouncilGameComponent.Instance?.AwayTicks(p) ?? -1;
            if (away < GenDate.TicksPerDay * 5) return null;
            var other = ArcUtil.Closest(p, home, 40f);
            if (other == null) return null;
            return new ArcCandidate
            {
                other = other,
                awayDays = away / GenDate.TicksPerDay,
                tag = ArcUtil.Partners(p, other) ? "partner" : ArcUtil.Kin(p, other) ? "kin" : "friend",
                weight = ArcUtil.Partners(p, other) ? 1.5f : 1f
            };
        }
    }

    /// <summary>Somewhere to belong: a newcomer who doesn't feel at home yet.</summary>
    public class ArcWorker_Newcomer : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            float days = ArcUtil.DaysHere(p);
            if (days < 2f || days > 25f) return null;
            bool unsettled;
            if (Heart.On)
            {
                float belonging = ArcUtil.Need(p, HeartNeed.Belonging);
                unsettled = ArcUtil.Feeling(p, "Homesick") != null || ArcUtil.Feeling(p, "Loneliness") != null || (belonging >= 0f && belonging < 45f);
            }
            else
            {
                var social = CouncilGameComponent.Instance?.social;
                bool friend = social != null && social.ties.Any(t => t.kind == TieKind.Friends && t.announced && t.Involves(p));
                unsettled = !friend && (p.needs?.mood?.CurLevelPercentage ?? 1f) < 0.6f;
            }
            if (!unsettled) return null;
            // Someone who's been here a while and likes the look of them.
            Pawn best = null;
            float score = -1f;
            foreach (var o in home)
            {
                if (o == p || ArcUtil.DaysHere(o) < 15f || o.DevelopmentalStage.Juvenile()) continue;
                int opinion = ArcUtil.Opinion(o, p);
                if (opinion < 0) continue;
                float s = opinion + (ArcUtil.HasTrait(o, TraitDefOf.Kind) ? 20f : 0f) + ArcUtil.Level(o, SkillDefOf.Social);
                if (s <= score) continue;
                best = o;
                score = s;
            }
            return best == null ? null : new ArcCandidate { other = best };
        }
    }

    /// <summary>Grudging respect: rivals who are both good at the same thing, or who fought side by side.</summary>
    public class ArcWorker_Rival : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var social = CouncilGameComponent.Instance?.social;
            if (social == null || !ColonyCouncilMod.Settings.socialWeb || ArcUtil.Psychopath(p)) return null;
            foreach (var t in social.ties.Where(t => t.kind == TieKind.Rivals && t.announced && t.Involves(p) && t.strength >= 15f && t.strength <= 80f).InRandomOrder())
            {
                var o = t.Other(p);
                if (o == null || !home.Contains(o) || ArcUtil.Psychopath(o) || o.DevelopmentalStage.Juvenile()) continue;
                // Something to respect: a passion they share, or the other's skill at what p loves.
                SkillDef shared = null;
                foreach (var s in ArcUtil.AllSkills)
                {
                    var mine = ArcUtil.Skill(p, s);
                    var theirs = ArcUtil.Skill(o, s);
                    if (mine == null || theirs == null || mine.TotallyDisabled || theirs.TotallyDisabled || !People.Passionate(mine)) continue;
                    if (People.Passionate(theirs) || theirs.Level >= mine.Level + 3)
                        if (shared == null || theirs.Level > ArcUtil.Level(o, shared)) shared = s;
                }
                bool comrades = social.Get(p, o, TieKind.Comrades) != null;
                if (shared == null && !comrades) continue;
                return new ArcCandidate { other = o, skill = shared, tag = shared == null ? "comrades" : null };
            }
            return null;
        }

        public override string Broken(Arc arc) =>
            arc.other != null && (ArcUtil.RemembersSince(arc.pawn, arc.other, ArcUtil.Fights, arc.startedTick) || ArcUtil.RemembersSince(arc.other, arc.pawn, ArcUtil.Fights, arc.startedTick))
                ? "fight"
                : null;
    }

    /// <summary>Old wounds: an ex, someone who betrayed a secret, or family at odds.</summary>
    public class ArcWorker_Estranged : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (ArcUtil.Psychopath(p) || p.relations == null) return null;
            ArcCandidate best = null;
            int rank = 0;
            foreach (var o in home)
            {
                if (o == p || o.relations == null || ArcUtil.Psychopath(o) || o.DevelopmentalStage.Juvenile()) continue;
                int mine = ArcUtil.Opinion(p, o), theirs = ArcUtil.Opinion(o, p);
                // Too far gone for words, or no quarrel worth the name.
                if (mine < -80 || theirs < -80) continue;
                int r = 0;
                string tag = null;
                if (ArcUtil.MemoryAbout(p, new List<string> { "Council_Secret_Betrayed" }, 60f, x => x == o) != null && mine < 20)
                {
                    r = 3;
                    tag = "betrayed";
                }
                else if ((p.relations.DirectRelationExists(PawnRelationDefOf.ExLover, o) || p.relations.DirectRelationExists(PawnRelationDefOf.ExSpouse, o)) && mine <= 25)
                {
                    r = 2;
                    tag = "ex";
                }
                else if (ArcUtil.Kin(p, o) && Mathf.Min(mine, theirs) <= -10)
                {
                    r = 1;
                    tag = "kin";
                }
                if (r <= rank) continue;
                rank = r;
                best = new ArcCandidate { other = o, tag = tag };
            }
            return best;
        }

        public override string Broken(Arc arc) =>
            arc.other != null && (ArcUtil.RemembersSince(arc.pawn, arc.other, ArcUtil.Fights, arc.startedTick) || ArcUtil.RemembersSince(arc.other, arc.pawn, ArcUtil.Fights, arc.startedTick))
                ? "fight"
                : null;
    }

    /// <summary>The apprentice's masterwork: keen on a making skill, with a master to learn from.</summary>
    public class ArcWorker_Apprentice : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (def.skills.NullOrEmpty()) return null;
            var social = CouncilGameComponent.Instance?.social;
            ArcCandidate best = null;
            foreach (var s in def.skills)
            {
                var mine = ArcUtil.Skill(p, s);
                if (mine == null || mine.TotallyDisabled || !People.Passionate(mine) || mine.Level < def.minLevel || mine.Level > def.maxLevel) continue;
                // A teacher already: the mentor of a mentorship in this skill.
                var tie = social?.ties.FirstOrDefault(t => t.kind == TieKind.Mentor && t.b == p && t.skill == s && t.a != null && home.Contains(t.a));
                Pawn master = tie?.a;
                float weight = tie != null ? 2f : 1f;
                if (master == null)
                    master = home.Where(o => o != p && ArcUtil.Level(o, s) >= Mathf.Max(def.masterLevel, mine.Level + 4) && ArcUtil.Opinion(p, o) >= -10)
                        .OrderByDescending(o => ArcUtil.Level(o, s)).FirstOrDefault();
                if (master == null) continue;
                if (best == null || weight > best.weight || Rand.Chance(0.5f))
                    best = new ArcCandidate { other = master, skill = s, weight = weight * (People.Burning(mine) ? 1.3f : 1f) };
            }
            return best;
        }
    }

    /// <summary>A long grief: someone they were close to died lately.</summary>
    public class ArcWorker_Grief : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (ArcUtil.Psychopath(p)) return null;
            int now = Find.TickManager.TicksGame;
            string lost = null;
            Pawn dead = null;
            var bond = CouncilGameComponent.Instance?.social?.lost.Where(l => l.survivor == p && now - l.tick <= GenDate.TicksPerDay * 25 && l.kind != TieKind.Rivals)
                .OrderByDescending(l => l.tick).FirstOrDefault();
            if (bond != null) lost = bond.name;
            if (lost == null)
            {
                var m = ArcUtil.MemoryAbout(p, def.memories, def.memoryDays, o => o.Dead && o.RaceProps.Humanlike);
                if (m != null)
                {
                    dead = m.otherPawn;
                    lost = dead.LabelShort;
                }
            }
            if (lost == null) return null;
            // Someone to talk about them with: who grieves them too, else their closest friend.
            Pawn other = null;
            if (dead != null)
                other = home.Where(o => o != p && o.needs?.mood?.thoughts?.memories?.Memories.Any(m => m.otherPawn == dead && Flavor.GriefThoughts.Contains(m.def.defName)) == true)
                    .OrderByDescending(o => ArcUtil.Closeness(p, o)).FirstOrDefault();
            other = other ?? ArcUtil.Closest(p, home, 10f);
            if (other == null) return null;
            return new ArcCandidate { other = other, lostName = lost, weight = 1.3f };
        }
    }

    /// <summary>An old debt: someone did them a kindness they haven't forgotten (a rescue, a kind word, a cover).</summary>
    public class ArcWorker_Debt : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (ArcUtil.Psychopath(p) || p.DevelopmentalStage.Juvenile()) return null;
            var m = ArcUtil.MemoryAbout(p, def.memories, def.memoryDays, o => o != p && home.Contains(o) && !o.Dead);
            Pawn other = m?.otherPawn;
            if (other == null)
            {
                var gratitude = ArcUtil.Feeling(p, "Gratitude");
                if (gratitude?.target != null && gratitude.target != p && home.Contains(gratitude.target)) other = gratitude.target;
            }
            if (other == null || ArcUtil.Opinion(p, other) < 0) return null;
            var skill = ArcUtil.Best(p, def.skills ?? ArcUtil.AllSkills.ToList(), 3) ?? ArcUtil.Best(p, ArcUtil.AllSkills, 3);
            if (skill == null) return null;
            return new ArcCandidate { other = other, skill = skill };
        }
    }

    /// <summary>A calling: a child or a teenager taken with something an adult here is good at.</summary>
    public class ArcWorker_Calling : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            bool young = p.DevelopmentalStage.Child() || (!p.DevelopmentalStage.Baby() && Flavor.HumanAge(p) < 18f);
            if (!young || p.skills == null) return null;
            ArcCandidate best = null;
            float bestScore = -1f;
            foreach (var r in p.skills.skills)
            {
                if (r.TotallyDisabled || r.Level > 10) continue;
                var teacher = home.Where(o => o != p && !o.DevelopmentalStage.Juvenile() && Flavor.HumanAge(o) >= 18f && ArcUtil.Level(o, r.def) >= Mathf.Max(8, r.Level + 3)
                                              && ArcUtil.Opinion(p, o) >= 0)
                    .OrderByDescending(o => ArcUtil.Opinion(p, o) + ArcUtil.Level(o, r.def) * 2).FirstOrDefault();
                if (teacher == null) continue;
                float score = r.Level + People.PassionLevel(r) * 4f + Rand.Value * 3f;
                if (score <= bestScore) continue;
                bestScore = score;
                best = new ArcCandidate { other = teacher, skill = r.def, tag = p.DevelopmentalStage.Child() ? "young" : "teen", weight = 1.5f };
            }
            return best;
        }
    }

    /// <summary>Passing it on: an elder (or an old hand of the colony) with a craft, and someone young to give it to.</summary>
    public class ArcWorker_Elder : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            float age = Flavor.HumanAge(p);
            bool elder = age >= 55f;
            if (!elder && ArcUtil.Years(p) < 3f) return null;
            if (p.DevelopmentalStage.Juvenile() || p.skills == null) return null;
            var skill = ArcUtil.Best(p, ArcUtil.AllSkills, def.masterLevel);
            if (skill == null) return null;
            int level = ArcUtil.Level(p, skill);
            var social = CouncilGameComponent.Instance?.social;
            var student = home.Where(o => o != p && Flavor.HumanAge(o) < Mathf.Min(35f, age - 10f) && !o.DevelopmentalStage.Baby()
                                          && ArcUtil.Skill(o, skill) is SkillRecord r && !r.TotallyDisabled && r.Level <= level - 4 && (People.Passionate(r) || r.Level >= 3)
                                          && ArcUtil.Opinion(p, o) >= 0 && !(social?.ties.Any(t => t.kind == TieKind.Mentor && t.b == o) ?? false))
                .OrderByDescending(o => People.PassionLevel(ArcUtil.Skill(o, skill)) * 20 + ArcUtil.Opinion(p, o)).FirstOrDefault();
            if (student == null) return null;
            return new ArcCandidate { other = student, skill = skill, tag = elder ? "elder" : "oldhand" };
        }
    }

    /// <summary>A secret told: a secret they've carried, and a friend they might tell first.</summary>
    public class ArcWorker_Secret : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var comp = CouncilGameComponent.Instance;
            if (!ColonyCouncilMod.Settings.secrets || comp == null || p.DevelopmentalStage.Juvenile()) return null;
            var s = comp.secrets.Of(p);
            if (s == null || s.revealed || (p.needs?.mood?.CurLevelPercentage ?? 0f) < 0.3f) return null;
            var friend = s.knowers.Where(k => k != null && home.Contains(k)).OrderByDescending(k => ArcUtil.Closeness(p, k)).FirstOrDefault()
                         ?? ArcUtil.Closest(p, home.Where(o => !o.DevelopmentalStage.Juvenile()), 45f);
            return friend == null ? null : new ArcCandidate { other = friend, tag = s.def.good ? "goodsecret" : null };
        }

        public override string Broken(Arc arc)
        {
            if (arc.stage >= arc.def.LastStage) return null;
            var s = CouncilGameComponent.Instance?.secrets.Of(arc.pawn);
            return s == null ? "gone" : s.revealed ? "cameout" : null;
        }
    }

    /// <summary>A fear faced: an enemy who hurt them and got away, a dread picked up, or fear that won't let go.</summary>
    public class ArcWorker_Fear : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || p.DevelopmentalStage.Juvenile() || p.WorkTagIsDisabled(WorkTags.Violent)) return null;
            var c = new ArcCandidate { skill = ArcUtil.Best(p, new[] { SkillDefOf.Shooting, SkillDefOf.Melee }, 0, false) };
            var nemesis = ColonyCouncilMod.Settings.nemeses ? comp.nemeses.Of(p).FirstOrDefault(n => !n.ended) : null;
            var quirk = !def.quirks.NullOrEmpty() && ColonyCouncilMod.Settings.quirks ? comp.quirks.Of(p).FirstOrDefault(q => def.quirks.Contains(q.def.defName)) : null;
            if (nemesis != null)
            {
                c.tag = "nemesis";
                c.name = nemesis.Name;
                c.weight = 1.5f;
            }
            else if (quirk != null)
            {
                c.tag = "quirk";
                c.quirk = quirk.def.defName;
            }
            else if (ArcUtil.Feeling(p, "Fear") is FeelingNow fear && fear.intensity >= 12f) c.tag = "fear";
            else return null;
            c.other = ArcUtil.Closest(p, home, 15f);
            return c;
        }

        /// <summary>The enemy who hurt them is gone for good (dead, captured, joined us).</summary>
        public override bool CustomGate(Arc arc, ArcGate gate)
        {
            if (arc.tag != "nemesis") return false;
            var open = CouncilGameComponent.Instance?.nemeses.Of(arc.pawn).Where(n => !n.ended).ToList();
            return open != null && open.Count == 0;
        }
    }

    /// <summary>A companion: an animal that follows them about, for someone who loves animals or could use a friend.</summary>
    public class ArcWorker_Companion : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!p.Spawned || p.WorkTypeIsDisabled(WorkTypeDefOf.Handling)) return null;
            var animals = ArcUtil.Skill(p, SkillDefOf.Animals);
            bool fond = (animals != null && People.Passionate(animals)) || ArcUtil.HasTrait(p, TraitDefOf.Kind) || ArcUtil.Feeling(p, "Loneliness") != null;
            if (!fond) return null;
            // Already bonded to an animal: that story has been told.
            if (p.relations != null && p.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond && r.otherPawn != null && !r.otherPawn.Dead)) return null;
            var pet = p.Map.mapPawns.SpawnedColonyAnimals
                .Where(a => a.RaceProps.Animal && !a.Dead && a.playerSettings?.Master == p && a.relations != null
                            && !a.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond))
                .OrderByDescending(a => (a.Name != null ? 2f : 0f) + Rand.Value).FirstOrDefault();
            return pet == null ? null : new ArcCandidate { other = pet };
        }
    }

    /// <summary>Second chances: someone who has stopped believing the council listens.</summary>
    public class ArcWorker_Doubt : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var s = ColonyCouncilMod.Settings;
            var comp = CouncilGameComponent.Instance;
            if (!s.enableWishes || !s.MoodEffects || comp == null || p.DevelopmentalStage.Juvenile()) return null;
            var level = comp.standings.LevelOf(p);
            if (level > TrustLevel.Doubtful) return null;
            return new ArcCandidate { tag = level == TrustLevel.Bitter ? "bitter" : "doubtful" };
        }
    }

    /// <summary>The recurring dream: the same strange dream, night after night.</summary>
    public class ArcWorker_Dreamer : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var comp = CouncilGameComponent.Instance;
            if (!ColonyCouncilMod.Settings.dreams || comp == null) return null;
            int now = Find.TickManager.TicksGame;
            var mine = comp.dreams.recent.Where(d => d.pawn == p && now - d.tick <= GenDate.TicksPerDay * 12 && d.kind != DreamKind.Sweet).ToList();
            if (mine.Count < 2) return null;
            var kind = mine[mine.Count - 1].kind;
            var skill = ArcUtil.Best(p, def.skills ?? new List<SkillDef>(), 2) ?? ArcUtil.Best(p, ArcUtil.AllSkills, 2);
            if (skill == null) return null;
            return new ArcCandidate
            {
                other = ArcUtil.Closest(p, home, 15f),
                skill = skill,
                tag = kind == DreamKind.Nightmare ? "nightmare" : kind == DreamKind.Lost ? "lostdream" : "strange"
            };
        }
    }

    /// <summary>A grudge laid down: a vendetta carried for weeks by someone who is tired of carrying it.</summary>
    public class ArcWorker_Vendetta : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var comp = CouncilGameComponent.Instance;
            if (!ColonyCouncilMod.Settings.vendettas || comp == null || ArcUtil.Psychopath(p) || ArcUtil.HasTrait(p, TraitDefOf.Bloodlust)) return null;
            int now = Find.TickManager.TicksGame;
            var v = comp.vendettas.Of(p).FirstOrDefault(x => now - x.tick >= GenDate.TicksPerDay * 15);
            if (v == null) return null;
            bool tired = ArcUtil.HasTrait(p, TraitDefOf.Kind) || (p.needs?.mood?.CurLevelPercentage ?? 0f) >= 0.5f || !v.faction.HostileTo(Faction.OfPlayer)
                         || now - v.tick >= GenDate.TicksPerDay * 40;
            if (!tired) return null;
            var other = ArcUtil.Closest(p, home, 15f);
            if (other == null) return null;
            return new ArcCandidate { other = other, lostName = v.victim, faction = v.faction, tag = v.kidnapped ? "taken" : null };
        }

        public override string Broken(Arc arc)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || arc.stage >= arc.def.LastStage) return null;
            if (!comp.vendettas.Of(arc.pawn).Any(v => v.faction == arc.faction)) return "settled";
            // Their banners at the walls again.
            if (comp.social.battleActive && comp.social.battleFaction == arc.faction && arc.pawn.MapHeld == comp.social.battleMap) return "anger";
            return null;
        }
    }

    /// <summary>Within reach: halfway to a life goal, and starting to doubt it.</summary>
    public class ArcWorker_Goal : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var comp = CouncilGameComponent.Instance;
            if (!ColonyCouncilMod.Settings.aspirations || comp == null) return null;
            var a = comp.aspirations.Active(p);
            if (a?.def == null || Find.TickManager.TicksGame - a.startedTick < GenDate.TicksPerDay * 10) return null;
            float progress = a.Progress;
            if (progress < 0.45f || progress > 0.85f) return null;
            var other = ArcUtil.Closest(p, home, 20f);
            if (other == null) return null;
            return new ArcCandidate { other = other, goal = a.def.defName, goalText = a.Goal };
        }

        public override string Broken(Arc arc)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return null;
            if (comp.aspirations.Active(arc.pawn)?.def?.defName == arc.goal) return null;
            return comp.aspirations.Fulfilled(arc.pawn).Any(x => x.def?.defName == arc.goal) ? null : "goalgone";
        }
    }
}
