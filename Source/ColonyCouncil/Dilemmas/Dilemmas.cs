using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A hard choice put to the council: a thief caught at night, a feud, a stash of liquor, a confession.
    /// Every answer costs someone something. Most of a dilemma is data; the worker finds the people involved.
    /// </summary>
    public class DilemmaDef : Def
    {
        public Type workerClass = typeof(DilemmaWorker);
        public float weight = 1f;
        public float cooldownDays = 40f;
        public int minColonists = 3;
        /// <summary>Who's involved. A is always needed; B only if given.</summary>
        public DilemmaRole roleA = new DilemmaRole();
        public DilemmaRole roleB;
        /// <summary>For workers that pick a thing (a stash): candidates in order of preference, by defName.</summary>
        public List<string> things;
        public int thingCount;
        /// <summary>The situation, one picked at random. {A_...}, {B_...}, {THING}, {COUNT}, {FACTION}.</summary>
        [MustTranslate] public List<string> texts = new List<string>();
        public List<DilemmaOption> options = new List<DilemmaOption>();
        /// <summary>Only in these seasons (at the home map). Empty = any time.</summary>
        public List<Season> seasons;
        /// <summary>Only within this many days after a raid or other big threat (0 = any time).</summary>
        public float withinDaysOfThreat;
        /// <summary>Only while someone is grieving.</summary>
        public bool needsGrief;
        /// <summary>Choosing this option often enough makes it a tradition (index; -1 = never).</summary>
        public int traditionOption = -1;
        public int traditionAfter = 2;
        /// <summary>The tradition's name: "the midsummer bonfire".</summary>
        [MustTranslate] public string traditionLabel;
        /// <summary>What happens if the council doesn't answer in time.</summary>
        public int defaultOption;
        /// <summary>For DilemmaWorker_Secret: only after one of these secrets (SecretDef names) comes out; empty = any.</summary>
        public List<string> secrets;
        public float answerDays = 2f;

        [Unsaved] private DilemmaWorker worker;

        public DilemmaWorker Worker
        {
            get
            {
                if (worker == null)
                {
                    worker = (DilemmaWorker)Activator.CreateInstance(workerClass ?? typeof(DilemmaWorker));
                    worker.def = this;
                }
                return worker;
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (texts.NullOrEmpty()) yield return "dilemma has no texts";
            if (options.NullOrEmpty()) yield return "dilemma has no options";
            else if (defaultOption < 0 || defaultOption >= options.Count) yield return "defaultOption out of range";
        }
    }

    /// <summary>Requirements for one person in a dilemma. All given conditions must hold.</summary>
    public class DilemmaRole
    {
        /// <summary>Any one of these traits (empty = no requirement).</summary>
        public List<TraitDef> anyTrait;
        public List<TraitDef> notTrait;
        /// <summary>Needs a passion for this skill.</summary>
        public SkillDef passion;
        /// <summary>Prefer whoever is best at this skill.</summary>
        public SkillDef bestAt;
        public float minMood = 0f;
        public float maxMood = 1f;
        public bool adult = true;
        /// <summary>Must be a child (Biotech). Overrides <see cref="adult"/>.</summary>
        public bool child;
        /// <summary>Role B only: opinion of A at most this (and A of B), for feuds.</summary>
        public int maxOpinionOfA = 100;
        /// <summary>Role B only: opinion of A at least this.</summary>
        public int minOpinionOfA = -100;
        public bool canFight;
        /// <summary>Must hold this council office (scandals, complaints about the marshal...).</summary>
        public OfficeDef office;
        /// <summary>Role B only: must share a bond of this kind with A, at least <see cref="minTie"/> strong.</summary>
        public TieKind tie = TieKind.None;
        public float minTie = 20f;
        /// <summary>Role B only: must not be bonded to A in this way (no rivals for a favour between friends...).</summary>
        public TieKind notTie = TieKind.None;
        /// <summary>Must be pursuing a life goal ({GOAL} in texts is A's goal).</summary>
        public bool goal;

        /// <summary>
        /// The parts that describe who someone is (traits, passions, mood, age, office, violence), ignoring where they
        /// are and what they're doing: for dreams, life goals and secrets. <paramref name="childrenOk"/>: children
        /// qualify unless the role asks for adults explicitly.
        /// </summary>
        public static bool Fits(DilemmaRole r, Pawn p, bool childrenOk)
        {
            if (p == null) return false;
            bool juvenile = p.DevelopmentalStage.Juvenile();
            if (r != null && r.child) { if (!p.DevelopmentalStage.Child()) return false; }
            else if (juvenile && !childrenOk && (r == null || r.adult)) return false;
            if (r == null) return true;
            var traits = p.story?.traits;
            if (!r.anyTrait.NullOrEmpty() && (traits == null || !r.anyTrait.Any(traits.HasTrait))) return false;
            if (!r.notTrait.NullOrEmpty() && traits != null && r.notTrait.Any(traits.HasTrait)) return false;
            if (r.passion != null)
            {
                var s = p.skills?.GetSkill(r.passion);
                if (s == null || s.TotallyDisabled || s.passion == Passion.None) return false;
            }
            float mood = p.needs?.mood?.CurLevelPercentage ?? 0.5f;
            if (mood < r.minMood || mood > r.maxMood) return false;
            if (r.office != null && (!ColonyCouncilMod.Settings.offices || CouncilGameComponent.Instance?.offices.HolderOf(r.office) != p)) return false;
            if (r.canFight && p.WorkTagIsDisabled(WorkTags.Violent)) return false;
            if (r.goal && CouncilGameComponent.Instance?.aspirations.Active(p) == null) return false;
            return true;
        }

        public bool Allows(Pawn p, Pawn a)
        {
            if (goal && CouncilGameComponent.Instance?.aspirations.Active(p) == null) return false;
            if (office != null && (!ColonyCouncilMod.Settings.offices || CouncilGameComponent.Instance?.offices.HolderOf(office) != p)) return false;
            if (tie != TieKind.None && (a == null || !ColonyCouncilMod.Settings.socialWeb || (CouncilGameComponent.Instance?.social.Strength(p, a, tie) ?? 0f) < minTie)) return false;
            if (notTie != TieKind.None && a != null && (CouncilGameComponent.Instance?.social.Strength(p, a, notTie) ?? 0f) >= 20f) return false;
            if (p == null || p == a || !p.Spawned || p.Downed || p.InMentalState || p.IsQuestLodger()) return false;
            var traits = p.story?.traits;
            if (child) { if (!p.DevelopmentalStage.Child()) return false; }
            else if (adult && p.DevelopmentalStage.Juvenile()) return false;
            if (!anyTrait.NullOrEmpty() && (traits == null || !anyTrait.Any(traits.HasTrait))) return false;
            if (!notTrait.NullOrEmpty() && traits != null && notTrait.Any(traits.HasTrait)) return false;
            if (passion != null)
            {
                var s = p.skills?.GetSkill(passion);
                if (s == null || s.TotallyDisabled || s.passion == Passion.None) return false;
            }
            float mood = p.needs?.mood?.CurLevelPercentage ?? 0.5f;
            if (mood < minMood || mood > maxMood) return false;
            if (canFight && p.WorkTagIsDisabled(WorkTags.Violent)) return false;
            if (a != null && p.relations != null && a.relations != null)
            {
                int o = p.relations.OpinionOf(a);
                int back = a.relations.OpinionOf(p);
                if (o > maxOpinionOfA || back > maxOpinionOfA) return false;
                if (o < minOpinionOfA) return false;
            }
            return true;
        }
    }

    public class DilemmaOption
    {
        [MustTranslate] public string label;
        /// <summary>What happens, for the follow-up message.</summary>
        [MustTranslate] public string outcome;
        public List<DilemmaEffect> effects = new List<DilemmaEffect>();
        /// <summary>A gamble: with this chance the risk effects happen too, and riskOutcome is told instead.</summary>
        public float riskChance;
        [MustTranslate] public string riskOutcome;
        public List<DilemmaEffect> riskEffects;
        /// <summary>When the risk comes true, it happens instead of the normal effects rather than as well.</summary>
        public bool riskReplaces;
    }

    /// <summary>The people and things of one dilemma as it happens.</summary>
    public class DilemmaContext : IExposable
    {
        public DilemmaDef def;
        public Map map;
        public Pawn a;
        public Pawn b;
        public ThingDef thing;
        public int count;
        public Faction faction;
        /// <summary>A's animal, for dilemmas about pets.</summary>
        public Pawn pet;
        /// <summary>The skill being taught, for social events between a mentor and a student.</summary>
        public SkillDef skill;
        public string text;
        /// <summary>Outside dilemmas (social events, dreams): what to call the cause in trust logs.</summary>
        [Unsaved] public string why;

        public string Format(string s)
        {
            if (s.NullOrEmpty()) return s ?? "";
            var args = new List<NamedArgument>
            {
                (a != null ? a.Named("A") : "?".Named("A")),
                (b != null ? b.Named("B") : (a != null ? a.Named("B") : "?".Named("B"))),
                (thing != null ? thing.label : "").Named("THING"),
                count.Named("COUNT"),
                (faction != null ? faction.Name : "").Named("FACTION"),
                (pet != null ? pet.Named("PET") : (a != null ? a.Named("PET") : "?".Named("PET"))),
                (skill != null ? skill.label : "").Named("SKILL"),
                (CouncilGameComponent.Instance?.aspirations.Active(a)?.Goal ?? "").Named("GOAL"),
                (CouncilGameComponent.Instance?.secrets.Of(a)?.def.label ?? "").Named("SECRET"),
                (CouncilGameComponent.Instance?.quirks.Of(a).FirstOrDefault()?.Label ?? "").Named("QUIRK")
            };
            return s.Formatted(args.ToArray()).Resolve();
        }

        public IEnumerable<Pawn> Colonists => map == null ? Enumerable.Empty<Pawn>() : map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger());

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref map, "map");
            Scribe_References.Look(ref a, "a");
            Scribe_References.Look(ref b, "b");
            Scribe_Defs.Look(ref thing, "thing");
            Scribe_Values.Look(ref count, "count");
            Scribe_References.Look(ref faction, "faction");
            Scribe_References.Look(ref pet, "pet");
            Scribe_Defs.Look(ref skill, "skill");
            Scribe_Values.Look(ref text, "text");
        }
    }

    /// <summary>Finds the people (and things) a dilemma needs. Subclass for anything the roles can't express.</summary>
    public class DilemmaWorker
    {
        public DilemmaDef def;

        /// <summary>The time and circumstances are right (season, a recent raid, grief).</summary>
        public virtual bool CanHappenNow(Map map)
        {
            if (!def.seasons.NullOrEmpty() && !def.seasons.Contains(GenLocalDate.Season(map))) return false;
            if (def.withinDaysOfThreat > 0f)
            {
                int last = map.storyState.LastThreatBigTick;
                if (last <= 0 || Find.TickManager.TicksGame - last > def.withinDaysOfThreat * GenDate.TicksPerDay) return false;
            }
            if (def.needsGrief && !map.mapPawns.FreeColonistsSpawned.Any(Flavor.IsGrieving)) return false;
            return true;
        }

        public virtual bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            if (pawns.Count < def.minColonists) return false;
            var aOptions = pawns.Where(p => def.roleA.Allows(p, null)).ToList();
            if (def.roleA.bestAt != null) aOptions = aOptions.OrderByDescending(p => p.skills?.GetSkill(def.roleA.bestAt)?.Level ?? 0).Take(1).ToList();
            foreach (var a in aOptions.InRandomOrder())
            {
                Pawn b = null;
                if (def.roleB != null)
                {
                    var bOptions = pawns.Where(p => def.roleB.Allows(p, a)).ToList();
                    if (def.roleB.bestAt != null) bOptions = bOptions.OrderByDescending(p => p.skills?.GetSkill(def.roleB.bestAt)?.Level ?? 0).Take(1).ToList();
                    b = bOptions.RandomElementWithFallback();
                    if (b == null) continue;
                }
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b, count = def.thingCount };
                if (!Extra(ctx)) continue;
                return true;
            }
            ctx = null;
            return false;
        }

        /// <summary>Anything else the dilemma needs (a thing, a faction). False = can't happen now.</summary>
        protected virtual bool Extra(DilemmaContext ctx)
        {
            if (!def.things.NullOrEmpty())
            {
                ctx.thing = def.things.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(t => t != null).RandomElementWithFallback();
                if (ctx.thing == null) return false;
            }
            return true;
        }
    }

    /// <summary>Needs a faction that isn't hostile, to send something to.</summary>
    public class DilemmaWorker_Faction : DilemmaWorker
    {
        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            ctx.faction = Find.FactionManager.AllFactionsVisible
                .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.HasGoodwill && !f.HostileTo(Faction.OfPlayer) && f.PlayerGoodwill < 80)
                .RandomElementWithFallback();
            return ctx.faction != null;
        }
    }

    /// <summary>Needs an animal of A's: bonded first, else one A is master of.</summary>
    public class DilemmaWorker_Pet : DilemmaWorker
    {
        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            var animals = ctx.map.mapPawns.SpawnedColonyAnimals.Where(a => !a.Dead && !a.Downed).ToList();
            ctx.pet = animals.FirstOrDefault(a => a.relations != null && a.relations.DirectRelationExists(PawnRelationDefOf.Bond, ctx.a))
                      ?? animals.FirstOrDefault(a => a.playerSettings?.Master == ctx.a);
            return ctx.pet != null;
        }
    }

    /// <summary>Needs enough raw food for a feast.</summary>
    public class DilemmaWorker_Feast : DilemmaWorker
    {
        protected override bool Extra(DilemmaContext ctx) => base.Extra(ctx) && DilemmaEffect_Consume.RawFood(ctx.map) >= ctx.count * 2;
    }

    // ===================== Effects =====================

    public enum DilemmaTarget
    {
        A,
        B,
        AAndB,
        /// <summary>Everyone at home.</summary>
        Colony,
        /// <summary>Everyone except A and B.</summary>
        Others,
        /// <summary>Those who like A (opinion 20+).</summary>
        FriendsOfA,
        /// <summary>Those who dislike A (opinion -20 or less).</summary>
        RivalsOfA
    }

    public abstract class DilemmaEffect
    {
        public DilemmaTarget target = DilemmaTarget.A;
        /// <summary>Only colonists with one of these traits (for Colony/Others targets).</summary>
        public List<TraitDef> onlyTrait;
        public List<TraitDef> exceptTrait;
        /// <summary>Only colonists with this trait at a degree in [minDegree, maxDegree] (e.g. teetotalers, chemical fascination).</summary>
        public TraitDef degreeTrait;
        public int minDegree = -99;
        public int maxDegree = 99;
        public float chance = 1f;

        /// <summary>Why the option can't be picked (not enough silver), or null.</summary>
        public virtual string Unavailable(DilemmaContext ctx) => null;

        public abstract void Apply(DilemmaContext ctx);

        protected IEnumerable<Pawn> Targets(DilemmaContext ctx)
        {
            IEnumerable<Pawn> list;
            switch (target)
            {
                case DilemmaTarget.A: list = ctx.a != null ? new[] { ctx.a } : new Pawn[0]; break;
                case DilemmaTarget.B: list = ctx.b != null ? new[] { ctx.b } : new Pawn[0]; break;
                case DilemmaTarget.AAndB: list = new[] { ctx.a, ctx.b }.Where(p => p != null); break;
                case DilemmaTarget.Colony: list = ctx.Colonists; break;
                case DilemmaTarget.Others: list = ctx.Colonists.Where(p => p != ctx.a && p != ctx.b); break;
                case DilemmaTarget.FriendsOfA: list = ctx.Colonists.Where(p => p != ctx.a && p != ctx.b && p.relations != null && ctx.a != null && p.relations.OpinionOf(ctx.a) >= 20); break;
                case DilemmaTarget.RivalsOfA: list = ctx.Colonists.Where(p => p != ctx.a && p != ctx.b && p.relations != null && ctx.a != null && p.relations.OpinionOf(ctx.a) <= -20); break;
                default: list = new Pawn[0]; break;
            }
            foreach (var p in list.ToList())
            {
                if (p == null || p.Dead) continue;
                var traits = p.story?.traits;
                if (!onlyTrait.NullOrEmpty() && (traits == null || !onlyTrait.Any(traits.HasTrait))) continue;
                if (!exceptTrait.NullOrEmpty() && traits != null && exceptTrait.Any(traits.HasTrait)) continue;
                if (degreeTrait != null)
                {
                    if (traits == null || !traits.HasTrait(degreeTrait)) continue;
                    int d = traits.DegreeOfTrait(degreeTrait);
                    if (d < minDegree || d > maxDegree) continue;
                }
                if (chance < 1f && !Rand.Chance(chance)) continue;
                yield return p;
            }
        }
    }

    public enum DilemmaAbout
    {
        Auto,
        A,
        B
    }

    /// <summary>A memory. Social thoughts are about B when given to A, and about A otherwise (or <see cref="about"/>).</summary>
    public class DilemmaEffect_Thought : DilemmaEffect
    {
        public ThoughtDef thought;
        public DilemmaAbout about = DilemmaAbout.Auto;

        public override void Apply(DilemmaContext ctx)
        {
            var pairs = new HashSet<(Pawn, Pawn)>();
            foreach (var p in Targets(ctx))
            {
                Pawn other = about == DilemmaAbout.A ? ctx.a : about == DilemmaAbout.B ? ctx.b : (p == ctx.a ? ctx.b : ctx.a);
                CouncilGameComponent.GiveThought(p, thought, other == p ? null : other);
                // Some memories are bonds being made or broken (once per pair, even when both get the memory).
                var social = CouncilGameComponent.Instance?.social;
                if (social != null && other != null && other != p && thought != null && pairs.Add(p.thingIDNumber < other.thingIDNumber ? (p, other) : (other, p)))
                {
                    switch (thought.defName)
                    {
                        case "Council_Dlm_MadeUp":
                            social.Weaken(p, other, TieKind.Rivals, 20f, "CC_TieNote_MadeUp".Translate());
                            social.Bump(p, other, TieKind.Friends, 8f, "CC_TieNote_MadeUp".Translate());
                            break;
                        case "Council_Dlm_Bonded":
                            social.Bump(p, other, TieKind.Friends, 15f, "CC_TieNote_Bonded".Translate());
                            break;
                        case "Council_Dlm_Grudge":
                        case "Council_Dlm_Distrust":
                            social.Bump(p, other, TieKind.Rivals, 8f, "CC_TieNote_Grudge".Translate());
                            break;
                    }
                }
            }
        }
    }

    public class DilemmaEffect_Trust : DilemmaEffect
    {
        public float amount;

        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            string why = ctx.def != null ? "CC_Log_Dilemma".Translate(ctx.def.label).ToString() : ctx.why;
            foreach (var p in Targets(ctx)) comp.standings.Change(p, amount, why);
        }
    }

    public class DilemmaEffect_Standards : DilemmaEffect
    {
        public float amount;

        public override void Apply(DilemmaContext ctx)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            foreach (var p in Targets(ctx))
            {
                var st = comp.standings.For(p);
                st.standards = Mathf.Clamp01(st.standards + amount);
            }
        }
    }

    public class DilemmaEffect_Hediff : DilemmaEffect
    {
        public HediffDef hediff;
        public float severity = 0.5f;

        public override void Apply(DilemmaContext ctx)
        {
            if (hediff == null) return;
            foreach (var p in Targets(ctx))
            {
                var existing = p.health?.hediffSet?.GetFirstHediffOfDef(hediff);
                if (existing != null)
                {
                    existing.Severity = Mathf.Max(existing.Severity, severity);
                    var timer = existing.TryGetComp<HediffComp_Disappears>();
                    if (timer != null) timer.ticksToDisappear = Mathf.Max(timer.ticksToDisappear, timer.Props.disappearsAfterTicks.RandomInRange);
                    continue;
                }
                var h = HediffMaker.MakeHediff(hediff, p);
                h.Severity = severity;
                p.health?.AddHediff(h);
            }
        }
    }

    public class DilemmaEffect_Inspiration : DilemmaEffect
    {
        public InspirationDef inspiration;

        public override void Apply(DilemmaContext ctx)
        {
            foreach (var p in Targets(ctx))
            {
                var handler = p.mindState?.inspirationHandler;
                if (handler == null || handler.Inspired) continue;
                var def = inspiration ?? handler.GetRandomAvailableInspirationDef();
                if (def != null) handler.TryStartInspiration(def, null, true);
            }
        }
    }

    /// <summary>Practice: experience in a skill (the one being taught, when none is given).</summary>
    public class DilemmaEffect_Learn : DilemmaEffect
    {
        public SkillDef skill;
        public float xp = 300f;

        public override void Apply(DilemmaContext ctx)
        {
            var sk = skill ?? ctx.skill;
            if (sk == null) return;
            foreach (var p in Targets(ctx))
            {
                var rec = p.skills?.GetSkill(sk);
                if (rec != null && !rec.TotallyDisabled) rec.Learn(xp, true);
            }
        }
    }

    /// <summary>Someone breaks: a sad wander, a tantrum, hiding in their room.</summary>
    public class DilemmaEffect_MentalState : DilemmaEffect
    {
        public MentalStateDef mentalState;

        public override void Apply(DilemmaContext ctx)
        {
            if (mentalState == null) return;
            foreach (var p in Targets(ctx))
            {
                if (!p.Spawned || p.Downed || p.InMentalState) continue;
                p.mindState?.mentalStateHandler?.TryStartMentalState(mentalState, "CC_Dilemma_BreakReason".Translate(), true);
            }
        }
    }

    /// <summary>A and B come to blows.</summary>
    public class DilemmaEffect_Fight : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            if (ctx.a == null || ctx.b == null || !ctx.a.Spawned || !ctx.b.Spawned || ctx.a.Downed || ctx.b.Downed || ctx.a.Map != ctx.b.Map) return;
            if (ctx.a.WorkTagIsDisabled(WorkTags.Violent) && ctx.b.WorkTagIsDisabled(WorkTags.Violent)) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            if (ctx.a.WorkTagIsDisabled(WorkTags.Violent)) ctx.b.interactions?.StartSocialFight(ctx.a);
            else ctx.a.interactions?.StartSocialFight(ctx.b);
            CouncilGameComponent.Instance?.social.Bump(ctx.a, ctx.b, TieKind.Rivals, 10f, "CC_TieNote_Fight".Translate());
        }
    }

    /// <summary>Puts the dilemma's thing (or a given one) on the ground next to A.</summary>
    public class DilemmaEffect_Spawn : DilemmaEffect
    {
        public ThingDef thing;
        public int count;
        public bool forbidden;

        public override void Apply(DilemmaContext ctx)
        {
            var def = thing ?? ctx.thing;
            int n = count > 0 ? count : ctx.count;
            var at = ctx.a != null && ctx.a.Spawned ? ctx.a : ctx.Colonists.FirstOrDefault();
            if (def == null || n <= 0 || at == null) return;
            while (n > 0)
            {
                var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                t.stackCount = Mathf.Min(n, def.stackLimit);
                n -= t.stackCount;
                if (forbidden) t.SetForbidden(true, false);
                GenPlace.TryPlaceThing(t, at.Position, at.Map, ThingPlaceMode.Near);
            }
        }
    }

    /// <summary>Takes something from the stockpiles: silver, raw food, or a named thing. Disables the option if there isn't enough.</summary>
    public class DilemmaEffect_Consume : DilemmaEffect
    {
        /// <summary>"silver", "rawfood", or a ThingDef defName; empty = the dilemma's thing.</summary>
        public string what;
        public int count;

        /// <summary>A quartermaster stretches the stores.</summary>
        private int Need(DilemmaContext ctx) => UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt((count > 0 ? count : ctx.count) * (CouncilGameComponent.Instance?.offices.CostFactor ?? 1f)));

        public static int RawFood(Map map)
        {
            int n = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
                if (IsRawFood(t, map)) n += t.stackCount;
            return n;
        }

        /// <summary>Raw food people can eat, sitting in storage: meat, vegetables, berries.</summary>
        private static bool IsRawFood(Thing t, Map map)
        {
            var ing = t.def.ingestible;
            if (ing == null || !t.def.IsNutritionGivingIngestible || t.def.IsCorpse || t.def.IsDrug || !ing.HumanEdible) return false;
            if (ing.preferability != FoodPreferability.RawBad && ing.preferability != FoodPreferability.RawTasty) return false;
            return t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage();
        }

        private int Have(DilemmaContext ctx)
        {
            if (ctx.map == null) return 0;
            if (what == "silver") return ctx.map.resourceCounter.Silver;
            if (what == "rawfood") return RawFood(ctx.map);
            var def = what.NullOrEmpty() ? ctx.thing : DefDatabase<ThingDef>.GetNamedSilentFail(what);
            return def == null ? 0 : ctx.map.resourceCounter.GetCount(def);
        }

        public override string Unavailable(DilemmaContext ctx) =>
            Have(ctx) < Need(ctx) ? "CC_Dilemma_NotEnough".Translate(Need(ctx), Label(ctx)).ToString() : null;

        private string Label(DilemmaContext ctx)
        {
            if (what == "silver") return ThingDefOf.Silver.label;
            if (what == "rawfood") return "CC_Dilemma_RawFood".Translate();
            return (what.NullOrEmpty() ? ctx.thing : DefDatabase<ThingDef>.GetNamedSilentFail(what))?.label ?? what;
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx.map == null) return;
            int left = Need(ctx);
            IEnumerable<Thing> things;
            if (what == "silver") things = ctx.map.listerThings.ThingsOfDef(ThingDefOf.Silver);
            else if (what == "rawfood") things = ctx.map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Where(t => IsRawFood(t, ctx.map));
            else
            {
                var def = what.NullOrEmpty() ? ctx.thing : DefDatabase<ThingDef>.GetNamedSilentFail(what);
                things = def == null ? Enumerable.Empty<Thing>() : ctx.map.listerThings.ThingsOfDef(def);
            }
            foreach (var t in things.Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer)).ToList())
            {
                if (left <= 0) break;
                int take = Mathf.Min(left, t.stackCount);
                t.SplitOff(take).Destroy();
                left -= take;
            }
        }
    }

    /// <summary>The pet is turned loose into the wild. Its person will not take it well (give them a thought separately).</summary>
    public class DilemmaEffect_ReleasePet : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var pet = ctx.pet;
            if (pet == null || pet.Dead || pet.Faction != Faction.OfPlayer) return;
            if (pet.Spawned) pet.jobs?.StopAll();
            pet.SetFaction(null);
        }
    }

    /// <summary>Goodwill with the dilemma's faction.</summary>
    public class DilemmaEffect_Goodwill : DilemmaEffect
    {
        public int amount;

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx.faction == null || ctx.faction.defeated) return;
            if (chance < 1f && !Rand.Chance(chance)) return;
            ctx.faction.TryAffectGoodwillWith(Faction.OfPlayer, amount, true, false);
        }
    }

    // ===================== The letter =====================

    public class ChoiceLetter_Dilemma : ChoiceLetter
    {
        public DilemmaContext ctx;
        public bool resolved;

        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || resolved || ctx?.def == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                for (int i = 0; i < ctx.def.options.Count; i++)
                {
                    int index = i;
                    var o = ctx.def.options[i];
                    var opt = new DiaOption(ctx.Format(o.label));
                    // The default answer is always possible, so the letter can never trap the player.
                    string why = i == ctx.def.defaultOption ? null : o.effects.Select(e => e.Unavailable(ctx)).FirstOrDefault(r => r != null);
                    if (why != null) opt.Disable(why);
                    opt.action = () =>
                    {
                        CouncilGameComponent.Instance?.dilemmas.Resolve(this, index, false);
                        Find.LetterStack.RemoveLetter(this);
                    };
                    opt.resolveTree = true;
                    yield return opt;
                }
                yield return Option_Postpone;
            }
        }

        /// <summary>Unanswered when the time runs out: the default happens.</summary>
        public override void Removed()
        {
            base.Removed();
            if (!resolved && ctx?.def != null) CouncilGameComponent.Instance?.dilemmas.Resolve(this, ctx.def.defaultOption, true);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref ctx, "ctx");
            Scribe_Values.Look(ref resolved, "resolved");
        }
    }

    // ===================== Scheduling =====================

    public class DilemmaTracker : IExposable
    {
        public int nextTick = -1;
        private Dictionary<string, int> lastTick = new Dictionary<string, int>();
        /// <summary>Traditions: how often the tradition answer was chosen, which are established, and breaks in a row.</summary>
        private Dictionary<string, int> traditionCount = new Dictionary<string, int>();
        private Dictionary<string, int> traditionBreaks = new Dictionary<string, int>();
        public List<string> traditions = new List<string>();

        public bool IsTradition(DilemmaDef def) => def != null && traditions.Contains(def.defName);

        public IEnumerable<DilemmaDef> Traditions => traditions.Select(n => DefDatabase<DilemmaDef>.GetNamedSilentFail(n)).Where(d => d != null);

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        public int lastFiredTick = -999999;

        public void Hourly()
        {
            if (!S.enableDilemmas) return;
            int now = Find.TickManager.TicksGame;
            if (nextTick < 0) nextTick = now + Interval();
            if (now < GenDate.TicksPerDay * 5) return;
            // The aftermath of a raid brings its own questions, outside the usual rhythm.
            int threat = Find.Maps.Where(m => m.IsPlayerHome).Select(m => m.storyState.LastThreatBigTick).DefaultIfEmpty(0).Max();
            if (threat > 0 && now - threat > GenDate.TicksPerHour * 6 && now - threat < GenDate.TicksPerDay * 1.5f
                && now - lastFiredTick > GenDate.TicksPerDay * 3 && Rand.Chance(0.08f))
            {
                if (TryFire(false, d => d.withinDaysOfThreat > 0f)) return;
            }
            // An established tradition comes round when its time does, whatever the schedule says.
            foreach (var t in Traditions.ToList())
            {
                if (lastTick.TryGetValue(t.defName, out int last) && now - last < GenDate.TicksPerDay * 30) continue;
                var home = Find.Maps.Where(m => m.IsPlayerHome).OrderByDescending(m => m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();
                if (home == null || !t.Worker.CanHappenNow(home)) continue;
                if (TryFire(true, d => d == t)) return;
            }
            if (now < nextTick) return;
            if (TryFire(false)) nextTick = now + Interval();
            else nextTick = now + GenDate.TicksPerDay;
        }

        /// <summary>Roughly every few days; sooner when everyone is in a good mood, because that's when things get comfortable.</summary>
        private static int Interval()
        {
            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned.Where(p => p.needs?.mood != null).ToList();
            float mood = pawns.Count > 0 ? pawns.Average(p => p.needs.mood.CurLevelPercentage) : 0.5f;
            float factor = Mathf.Clamp(1.6f - mood, 0.6f, 1.3f);
            return Mathf.RoundToInt(S.daysBetweenDilemmas * factor * Rand.Range(0.7f, 1.3f) * GenDate.TicksPerDay);
        }

        public bool TryFire(bool force, Func<DilemmaDef, bool> only = null)
        {
            var map = Find.Maps.Where(m => m.IsPlayerHome).OrderByDescending(m => m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();
            if (map == null) return false;
            if (Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Dilemma d && !d.resolved)) return false;
            int now = Find.TickManager.TicksGame;
            var pool = DefDatabase<DilemmaDef>.AllDefsListForReading
                .Where(d => only == null || only(d))
                .Where(d => force || !lastTick.TryGetValue(d.defName, out int t) || now - t > d.cooldownDays * GenDate.TicksPerDay)
                .ToList();
            foreach (var def in pool.InRandomOrder().OrderByDescending(d => d.weight * Rand.Value * (d.seasons.NullOrEmpty() && d.withinDaysOfThreat <= 0f ? 1f : 3f)))
            {
                DilemmaContext ctx;
                try
                {
                    if (!def.Worker.TryCast(map, out ctx)) continue;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("dilemma " + def.defName, e);
                    continue;
                }
                Send(ctx);
                lastTick[def.defName] = now;
                lastFiredTick = now;
                return true;
            }
            return false;
        }

        private void Send(DilemmaContext ctx)
        {
            ctx.text = Flavor.PickText(ctx.def.texts);
            var letterDef = DefDatabase<LetterDef>.GetNamedSilentFail("Council_DilemmaLetter");
            var look = new LookTargets(new[] { ctx.a, ctx.b }.Where(p => p != null));
            string tradition = IsTradition(ctx.def)
                ? "\n\n" + "CC_Tradition_Note".Translate(ctx.def.traditionLabel, ctx.Format(ctx.def.options[ctx.def.traditionOption].label)).Resolve()
                : "";
            var letter = (ChoiceLetter_Dilemma)LetterMaker.MakeLetter("CC_Dilemma_Title".Translate(ctx.def.LabelCap), ctx.Format(ctx.text) + tradition + "\n\n" + "CC_Dilemma_Deadline".Translate(TextUtil.DaysString(Mathf.RoundToInt(ctx.def.answerDays * GenDate.TicksPerDay)), ctx.Format(ctx.def.options[ctx.def.defaultOption].label)), letterDef, look);
            letter.ctx = ctx;
            letter.StartTimeout(Mathf.RoundToInt(ctx.def.answerDays * GenDate.TicksPerDay));
            Find.LetterStack.ReceiveLetter(letter);
        }

        public void Resolve(ChoiceLetter_Dilemma letter, int index, bool byDefault)
        {
            if (letter == null || letter.resolved || letter.ctx?.def == null) return;
            letter.resolved = true;
            var ctx = letter.ctx;
            var comp = CouncilGameComponent.Instance;
            if (comp != null) comp.annals.dilemmas++;
            var option = ctx.def.options[Mathf.Clamp(index, 0, ctx.def.options.Count - 1)];
            // Someone may have left or died in the meantime; the dilemma happens to whoever is still here.
            if (ctx.a == null || ctx.a.Dead) return;
            if (ctx.def.traditionOption >= 0 && comp != null) Tradition(ctx, index == ctx.def.traditionOption, comp);
            bool risk = option.riskChance > 0f && Rand.Chance(option.riskChance);
            if (!risk || !option.riskReplaces)
                foreach (var e in option.effects) Safe(e, ctx);
            string outcome = option.outcome;
            if (risk)
            {
                if (option.riskEffects != null) foreach (var e in option.riskEffects) Safe(e, ctx);
                if (!option.riskOutcome.NullOrEmpty()) outcome = option.riskOutcome;
            }
            string told = ctx.Format(outcome);
            if (byDefault) told = "CC_Dilemma_NoAnswer".Translate() + " " + told;
            if (!told.NullOrEmpty())
                Messages.Message(told, new LookTargets(new[] { ctx.a, ctx.b }.Where(p => p != null && p.Spawned)), MessageTypeDefOf.NeutralEvent, false);
            CouncilGameComponent.Instance?.AddChronicle(new ChronicleEntry(ChronicleKind.Council,
                "CC_Chronicle_Dilemma".Translate(ctx.def.label, ctx.Format(option.label)).Resolve(), ctx.a));
            // The letter's text is already written; let go of the people and places so old saves don't hold them.
            ctx.a = ctx.b = ctx.pet = null;
            ctx.map = null;
            ctx.faction = null;
        }

        /// <summary>Keeping, founding or breaking a tradition.</summary>
        private void Tradition(DilemmaContext ctx, bool kept, CouncilGameComponent comp)
        {
            var def = ctx.def;
            string key = def.defName;
            var colonists = ctx.Colonists.ToList();
            if (kept)
            {
                traditionBreaks[key] = 0;
                traditionCount[key] = (traditionCount.TryGetValue(key, out int n) ? n : 0) + 1;
                if (IsTradition(def))
                {
                    foreach (var p in colonists) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_TraditionKept);
                    comp.annals.Notify_Tradition(def.traditionLabel, true);
                }
                else if (traditionCount[key] >= def.traditionAfter)
                {
                    traditions.Add(key);
                    foreach (var p in colonists) CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_TraditionKept);
                    Find.LetterStack.ReceiveLetter("CC_Tradition_BornTitle".Translate(def.traditionLabel.CapitalizeFirst()).Resolve(),
                        "CC_Tradition_BornText".Translate(def.traditionLabel).Resolve(), LetterDefOf.PositiveEvent);
                    comp.AddChronicle(new ChronicleEntry(ChronicleKind.Council, "CC_Chronicle_TraditionBorn".Translate(def.traditionLabel).Resolve(), null));
                    comp.annals.Notify_TraditionBorn(def.traditionLabel);
                    foreach (var p in colonists.InRandomOrder().Take(2))
                        comp.journals.Write(p, "Council_Journal_TraditionBorn", null, def.traditionLabel.Named("TRADITION"));
                }
                return;
            }
            if (!IsTradition(def)) return;
            int breaks = (traditionBreaks.TryGetValue(key, out int b) ? b : 0) + 1;
            traditionBreaks[key] = breaks;
            foreach (var p in colonists)
            {
                if (p.story?.traits?.HasTrait(TraitDefOf.Ascetic) ?? false) continue;
                CouncilGameComponent.GiveThought(p, CouncilDefOf.Council_TraditionBroken);
                comp.standings.Change(p, -2f, null);
            }
            comp.annals.Notify_Tradition(def.traditionLabel, false);
            if (breaks >= 2)
            {
                traditions.Remove(key);
                traditionCount[key] = 0;
                traditionBreaks[key] = 0;
                Messages.Message("CC_Tradition_Lapsed".Translate(def.traditionLabel).Resolve(), MessageTypeDefOf.NegativeEvent, false);
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Council, "CC_Chronicle_TraditionLapsed".Translate(def.traditionLabel).Resolve(), null));
            }
            else
            {
                Messages.Message("CC_Tradition_Broken".Translate(def.traditionLabel).Resolve(), MessageTypeDefOf.NegativeEvent, false);
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Council, "CC_Chronicle_TraditionBroken".Translate(def.traditionLabel).Resolve(), null));
            }
        }

        private static void Safe(DilemmaEffect e, DilemmaContext ctx)
        {
            try
            {
                e.Apply(ctx);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("dilemma effect " + e.GetType().Name, ex);
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref nextTick, "nextTick", -1);
            Scribe_Values.Look(ref lastFiredTick, "lastFiredTick", -999999);
            Scribe_Collections.Look(ref traditionCount, "traditionCount", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref traditionBreaks, "traditionBreaks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref traditions, "traditions", LookMode.Value);
            Scribe_Collections.Look(ref lastTick, "lastTick", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lastTick == null) lastTick = new Dictionary<string, int>();
                if (traditionCount == null) traditionCount = new Dictionary<string, int>();
                if (traditionBreaks == null) traditionBreaks = new Dictionary<string, int>();
                if (traditions == null) traditions = new List<string>();
            }
        }
    }
}
