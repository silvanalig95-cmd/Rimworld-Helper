using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Story chains told from data alone. Every stage is an ordinary story stage (Workers_Chains2.cs) with
    // HappeningWorker_StoryFlow as its worker and a StoryFlow beside its StoryText: the first stage's says who the story
    // is about and how each ending turns out; every stage's says who comes for it and what each answer leads to. The
    // texts are where they always are: the stage's HappeningExtension, and the first stage's StoryText (endings, World
    // page lines, names). Chains made this way: the lost letters, the stranger's seeds, and the play about the colony
    // (Defs/IncidentDefs/Council_Events_Stories_More.xml).

    /// <summary>What a data-told story stage needs beyond its texts. See <see cref="HappeningWorker_StoryFlow"/>.</summary>
    public class StoryFlow : DefModExtension
    {
        // ---- The first stage: who the story is about ----
        /// <summary>The colony's era, as a tag expression ("lowtech", "!lowtech").</summary>
        public string era;
        /// <summary>X: someone of a friendly people the story is about ({FACTION} is their people).</summary>
        public bool stranger;
        public FloatRange strangerAges = new FloatRange(20f, 60f);
        public SkillDef strangerSkill;
        public IntRange strangerLevel = new IntRange(8, 14);
        /// <summary>A, the colonist the story follows: the best at this skill (else the event's colonist role, else the best talker).</summary>
        public SkillDef heroSkill;
        /// <summary>{COUNT}: a sum of silver, larger for a richer colony.</summary>
        public FloatRange silver = FloatRange.Zero;
        /// <summary>What a gift or a find at the end is worth, before the colony's wealth is counted.</summary>
        public FloatRange value = new FloatRange(300f, 600f);
        /// <summary>Luck settled at the start: one of these flags, by weight.</summary>
        public List<StoryRoll> rolls;
        /// <summary>What the colony is known for goes into the story's flags (colony:Merciful...), for the endings to read.</summary>
        public bool colonyFlags;

        // ---- Every stage: who comes ----
        /// <summary>X comes back to the colony for this stage and stays this many hours (0: X isn't here). The first stage too.</summary>
        public float visitHours;
        /// <summary>Someone new of the story's people comes for this stage instead, as X (a messenger, an admirer).</summary>
        public bool newcomer;
        public FloatRange newcomerAges = new FloatRange(18f, 60f);
        /// <summary>Visitors stay at the colony's gathering spot rather than just outside.</summary>
        public bool gathering = true;

        // ---- Every stage: what follows ----
        /// <summary>What each answer leads to: the first that fits.</summary>
        public List<StoryRoute> routes;
        /// <summary>The ending told when the next stage can't be (its people are gone), unless a route names its own.</summary>
        public string fallback;

        // ---- The first stage: how it ends ----
        public List<StoryEnding> endings;

        public StoryEnding Ending(string key) => endings?.FirstOrDefault(e => e.key == key);
    }

    /// <summary>One way the luck can fall: a flag, and its weight.</summary>
    public class StoryRoll
    {
        public string flag;
        public float weight = 1f;
    }

    /// <summary>
    /// Where an answer leads: a later stage (its defName) or an ending ("end:key"), after <see cref="days"/>. Fits when
    /// it is the given answer (or any, with -1), the gamble went the given way (<see cref="when"/>: risk, safe, chosen,
    /// default), and the story has (or hasn't) the given flag. A stage without a question uses its first route that fits.
    /// </summary>
    public class StoryRoute
    {
        public int option = -1;
        public string when;
        public string requires;
        public string forbids;
        public float chance = 1f;
        public List<string> flags;
        public string next;
        public FloatRange days = new FloatRange(3f, 6f);
        public string fallback;

        public bool Fits(StoryChain c, int answer, bool risk, bool byDefault)
        {
            if (option >= 0 && option != answer) return false;
            switch (when)
            {
                case "risk": if (!risk) return false; break;
                case "safe": if (risk) return false; break;
                case "chosen": if (byDefault) return false; break;
                case "default": if (!byDefault) return false; break;
            }
            if (!requires.NullOrEmpty() && !requires.Split('|').Any(c.Has)) return false;
            if (!forbids.NullOrEmpty() && forbids.Split('|').Any(c.Has)) return false;
            return chance >= 1f || Rand.Chance(chance);
        }
    }

    /// <summary>
    /// What an ending does before its letter is told: its effects (with the story's people: A, X, {FACTION}), goodwill,
    /// a gift worth the story's value (named by {ITEM}), silver ({COUNT} times <see cref="silver"/>), and X joining the
    /// colony if they're still free to.
    /// </summary>
    public class StoryEnding
    {
        public string key;
        public List<DilemmaEffect> effects;
        public int goodwill;
        public float gift;
        public bool fancy;
        public float silver;
        public bool xJoins;
    }

    /// <summary>A stage of a story chain told from its <see cref="StoryFlow"/>.</summary>
    public class HappeningWorker_StoryFlow : HappeningWorker_Story
    {
        private StoryFlow Flow => def.GetModExtension<StoryFlow>();

        private static StoryFlow HomeFlow(StoryChain c) => StoryChains.HomeDef(c?.kind)?.GetModExtension<StoryFlow>();

        // ---------------- Can it happen? ----------------

        protected override bool CanBegin(Map map, HappeningExtension ext)
        {
            var f = Flow;
            if (f == null || !HappeningWorker_Setting.EraFits(f.era)) return false;
            if ((f.stranger || f.newcomer) && Arrivals.Friendly() == null) return false;
            if (f.heroSkill != null && VarietyUtil.Best(map, f.heroSkill) == null) return false;
            if (ext.colonist != null && !Colonists(map).Any(p => ext.colonist.Allows(p, null))) return false;
            return true;
        }

        protected override bool CanGoOn(StoryChain c, Map map)
        {
            var f = Flow;
            if (f == null) return true;
            if (f.newcomer) return LivingEvents.Friendly(c.faction) || Arrivals.Friendly() != null;
            if (f.visitHours > 0f) return StoryChains.CanVisit(c.x);
            return true;
        }

        // ---------------- Who ----------------

        protected override bool Setup(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var f = Flow ?? new StoryFlow();
            var map = ctx.map;
            ctx.faction = Arrivals.Friendly();
            if (f.stranger)
            {
                if (ctx.faction == null) return false;
                ctx.stranger = Make(ctx.faction, f.strangerAges);
                if (ctx.stranger == null) return false;
                if (f.strangerSkill != null) StorytellerEvents.Skilled(ctx.stranger, f.strangerSkill, f.strangerLevel);
            }
            if (f.heroSkill != null) ctx.a = VarietyUtil.Best(map, f.heroSkill);
            else if (ext.colonist != null)
                ctx.a = Colonists(map).Where(p => ext.colonist.Allows(p, null))
                    .OrderByDescending(p => ext.colonist.bestAt != null ? p.skills?.GetSkill(ext.colonist.bestAt)?.Level ?? 0 : Rand.Value).FirstOrDefault();
            else ctx.a = VarietyUtil.Best(map, SkillDefOf.Social) ?? StorytellerEvents.Adults(map).RandomElementWithFallback();
            if (ctx.a == null) return false;
            if (f.silver.max > 0f) ctx.count = StoryChains.Silver(map, f.silver.min, f.silver.max);
            c.value = Mathf.RoundToInt(f.value.RandomInRange * LivingEvents.WealthFactor(map));
            if (!f.rolls.NullOrEmpty())
            {
                var roll = f.rolls.Where(r => !r.flag.NullOrEmpty()).RandomElementByWeightWithFallback(r => Mathf.Max(0f, r.weight));
                if (roll != null) c.Set(roll.flag);
            }
            if (f.colonyFlags)
            {
                var character = CouncilGameComponent.Instance?.character;
                if (character != null)
                    foreach (var t in CharacterTracker.TagNames)
                        if (character.Is(t))
                            c.Set("colony:" + t);
            }
            if (ctx.faction != null) ctx.placeName = LivingEvents.TownOf(ctx.faction, map);
            string named = StoryChains.Line(Story?.names, ctx, null, c.flags);
            if (!named.NullOrEmpty()) ctx.placeName = named;
            if (ctx.placeName == null) ctx.placeName = "";
            return true;
        }

        protected override bool Arrive(StoryChain c, HappeningContext ctx, HappeningExtension ext)
        {
            var f = Flow;
            var map = ctx.map;
            if (f == null || map == null) return true;
            if (f.newcomer)
            {
                var people = LivingEvents.Friendly(c.faction) ? c.faction : Arrivals.Friendly();
                var p = people != null ? Make(people, f.newcomerAges) : null;
                if (p == null) return false;
                ctx.stranger = p;
                ctx.faction = people;
                return StoryChains.Visit(new[] { p }, map, people, f.gathering ? GatheringSpot(map) : null, Mathf.Max(8f, f.visitHours));
            }
            if (f.visitHours > 0f && ctx.stranger != null)
            {
                var people = ctx.stranger.Faction ?? ctx.faction;
                return StoryChains.Visit(new[] { ctx.stranger }, map, people, f.gathering ? GatheringSpot(map) : null, f.visitHours);
            }
            return true;
        }

        // ---------------- What follows ----------------

        protected override string Answer(StoryChain c, HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            Follow(c, Flow?.routes, option, risk, byDefault);
            return outcome;
        }

        protected override void Told(StoryChain c, HappeningContext ctx) => Follow(c, Flow?.routes, -1, false, false);

        private void Follow(StoryChain c, List<StoryRoute> routes, int option, bool risk, bool byDefault)
        {
            if (c == null || routes.NullOrEmpty()) return;
            var r = routes.FirstOrDefault(x => x != null && !x.next.NullOrEmpty() && x.Fits(c, option, risk, byDefault));
            if (r == null) return;
            if (r.flags != null) c.Set(r.flags.ToArray());
            StoryChains.Next(c, r.next, Mathf.Max(0.2f, r.days.min), Mathf.Max(0.2f, r.days.max), r.fallback ?? Flow?.fallback ?? HomeFlow(c)?.fallback);
        }

        // ---------------- How it ends ----------------

        public override void Ending(StoryChain c, string key, Map map)
        {
            var home = StoryChains.HomeDef(c?.kind);
            var e = HomeFlow(c)?.Ending(key);
            if (home == null || e == null || map == null)
            {
                StoryChains.End(c, key);
                return;
            }
            var ctx = StoryChains.Context(c, home, map);
            string item = null;
            LookTargets look = null;
            try
            {
                using (Causes.Push(LivingEvents.Cause(home)))
                using (PlayOuts.Collect())
                {
                    // What the ending sets going on the map (PlayOut/) comes first, so it can hold the moods of those taking part.
                    if (e.effects != null)
                        foreach (var fx in PlayOuts.Ordered(e.effects))
                            HappeningTracker.Safe(fx, ctx);
                }
                if (e.goodwill != 0) StoryChains.Goodwill(c.faction, e.goodwill);
                if (e.gift > 0f) item = StoryChains.Gift(map, IntVec3.Invalid, c.value * e.gift, e.fancy, !Avail.IsPreIndustrial);
                if (e.silver > 0f)
                {
                    int n = Mathf.Max(10, Mathf.RoundToInt(c.amount * e.silver / 10f) * 10);
                    VarietyUtil.PaySilver(map, DropCellFinder.TradeDropSpot(map), n);
                    ctx.count = n;
                    if (item == null) item = n.ToString();
                }
                // Still their own person and nobody's captive: they walk in from the edge (or stay, if they're here).
                var x = c.x;
                if (e.xJoins && StoryChains.Free(x) && Arrivals.Join(x, map)) look = new LookTargets(x);
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("story ending " + c.kind + " " + key, ex);
            }
            StoryChains.End(c, key, ctx, item, look);
        }
    }
}
