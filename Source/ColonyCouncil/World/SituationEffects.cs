using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Effects for world situations. They are DilemmaEffects, so they sit in a def's effects lists next to the dilemma
    // and event effects (DilemmaEffect_Thought, _Consume, _Loot, _Learn, _RaidLater, _Inspiration...), which all work
    // in a situation too. These know about the situation they belong to: its flags, its people, the colony's part.

    /// <summary>The effects' shared helpers.</summary>
    public static class SituationEffectUtil
    {
        public static WorldSituation Of(DilemmaContext ctx) => WorldSituations.Instance?.Get((ctx as SituationContext)?.situationId ?? -1);

        public static Map MapOf(DilemmaContext ctx) => ctx?.map ?? HappeningTracker.HomeMap();
    }

    /// <summary>
    /// A world situation's effect: like any DilemmaEffect, plus <see cref="requires"/> and <see cref="forbids"/>, a flag
    /// the situation must have (or must not have) for it to happen, so one ending can pay off what the colony did.
    /// </summary>
    public abstract class WorldEffect : DilemmaEffect
    {
        public string requires;
        public string forbids;

        protected bool Gate(DilemmaContext ctx)
        {
            if (chance < 1f && !Rand.Chance(chance)) return false;
            if (requires.NullOrEmpty() && forbids.NullOrEmpty()) return true;
            var s = SituationEffectUtil.Of(ctx);
            if (!requires.NullOrEmpty() && (s == null || !s.Has(requires))) return false;
            if (!forbids.NullOrEmpty() && s != null && s.Has(forbids)) return false;
            return true;
        }
    }

    /// <summary>Which peoples a goodwill change reaches.</summary>
    public enum SituationPeople
    {
        /// <summary>The people it's about ({FACTION}).</summary>
        Faction,
        /// <summary>The second people ({ENEMY}).</summary>
        Enemy,
        Both,
        /// <summary>Every people at peace with the colony that has a town nearby.</summary>
        Neighbors,
        /// <summary>Every people at war with {FACTION} (the warlord's hunters).</summary>
        Hunters
    }

    /// <summary>Goodwill that never turns a people hostile: a loss stops at -60.</summary>
    public class WorldEffect_Goodwill : WorldEffect
    {
        public int amount;
        public SituationPeople who = SituationPeople.Faction;

        public override void Apply(DilemmaContext ctx)
        {
            if (!Gate(ctx)) return;
            var h = ctx as HappeningContext;
            var list = new List<Faction>();
            switch (who)
            {
                case SituationPeople.Faction: list.Add(ctx.faction); break;
                case SituationPeople.Enemy: list.Add(h?.enemy); break;
                case SituationPeople.Both: list.Add(ctx.faction); list.Add(h?.enemy); break;
                case SituationPeople.Neighbors:
                    list.AddRange(Find.FactionManager.AllFactionsListForReading.Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.HasGoodwill
                                                                                          && !f.HostileTo(Faction.OfPlayer) && WorldSituationWorker.NearestTown(f, SituationEffectUtil.MapOf(ctx)) != null));
                    break;
                case SituationPeople.Hunters:
                    if (ctx.faction != null)
                        list.AddRange(Find.FactionManager.AllFactionsListForReading.Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.HasGoodwill && f != ctx.faction
                                                                                              && f.HostileTo(ctx.faction) && !f.HostileTo(Faction.OfPlayer)));
                    break;
            }
            foreach (var f in list.Where(f => f != null).Distinct()) WorldSituationWorker.Goodwill(f, amount);
        }
    }

    /// <summary>Sets (or clears) a flag on the situation, for the beats, modifiers and endings that read it.</summary>
    public class WorldEffect_Flag : WorldEffect
    {
        public string flag;
        public bool clear;

        public override void Apply(DilemmaContext ctx)
        {
            var s = SituationEffectUtil.Of(ctx);
            if (s == null || flag.NullOrEmpty() || !Gate(ctx)) return;
            if (clear) s.Clear(flag);
            else s.Set(flag);
        }
    }

    /// <summary>Medicine out of the stores: any kind, the plainest first. Unavailable without enough.</summary>
    public class WorldEffect_Medicine : DilemmaEffect
    {
        /// <summary>How many (0: the beat's {COUNT}).</summary>
        public int count;

        private int Need(DilemmaContext ctx) => Mathf.Max(1, count > 0 ? count : ctx.count);

        private static IEnumerable<Thing> Stock(Map map) =>
            map == null ? Enumerable.Empty<Thing>() : map.listerThings.ThingsInGroup(ThingRequestGroup.Medicine)
                .Where(t => t.def.IsMedicine && t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage());

        public static int Have(Map map) => Stock(map).Sum(t => t.stackCount);

        public override string Unavailable(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            return Have(map) < Need(ctx) ? "CC_Dilemma_NotEnough".Translate(Need(ctx), "CC_WS_Medicine".Translate()).ToString() : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            int left = Need(ctx);
            foreach (var t in Stock(map).OrderBy(t => t.MarketValue).ToList())
            {
                if (left <= 0) break;
                int take = Mathf.Min(left, t.stackCount);
                t.SplitOff(take).Destroy();
                left -= take;
            }
        }
    }

    /// <summary>Silver for the colony, left where traders drop their goods ({COUNT} times <see cref="factor"/>, or <see cref="count"/>).</summary>
    public class WorldEffect_Silver : WorldEffect
    {
        public int count;
        public float factor = 1f;

        public override void Apply(DilemmaContext ctx)
        {
            if (!Gate(ctx)) return;
            int n = Mathf.RoundToInt((count > 0 ? count : ctx.count) * factor);
            VarietyUtil.PaySilver(SituationEffectUtil.MapOf(ctx), IntVec3.Invalid, n);
        }
    }

    /// <summary>
    /// People of the situation's people (or of nobody's) join the colony: refugees, a family, a prospector. They walk
    /// in from the edge of the map, as adults of the colony's era where the people's own kind won't do. With
    /// <see cref="family"/> they are a real family, as the event's text says: a couple who share a name, and with three
    /// or more their children. With <see cref="animal"/> the dog or goat the text speaks of comes with them, tame.
    /// </summary>
    public class WorldEffect_Join : WorldEffect
    {
        public IntRange count = new IntRange(1, 1);
        public FloatRange ages = new FloatRange(18f, 50f);
        /// <summary>A family: at least two, a couple sharing a name, any more their children.</summary>
        public bool family;
        /// <summary>An animal that comes with them: "dog", "goat", or a PawnKindDef's defName.</summary>
        public string animal;

        private static readonly string[] Dogs = { "LabradorRetriever", "Husky", "YorkshireTerrier" };

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            if (map == null || !Gate(ctx)) return;
            var f = ctx.faction != null && !ctx.faction.defeated ? ctx.faction : null;
            var kind = f != null ? Arrivals.PersonKind(f) : null;
            if (kind == null || Avail.IsPreIndustrial && f.def.techLevel > TechLevel.Medieval) kind = Arrivals.JoinerKind();
            int n = count.RandomInRange;
            if (family) n = Mathf.Max(2, n);
            Pawn first = null, second = null;
            string lastName = null;
            for (int i = 0; i < n; i++)
            {
                bool child = family && i >= 2;
                Pawn p;
                try
                {
                    // A family that walks together shares a name; parents old enough for the children they bring.
                    var age = !family ? ages : child ? ChildAges() : n > 2 ? new FloatRange(32f, 45f) : new FloatRange(22f, 45f);
                    Gender? gender = family && i == 1 && first != null ? (first.gender == Gender.Male ? Gender.Female : Gender.Male) : (Gender?)null;
                    p = Arrivals.Make(kind, f, age, gender, lastName, child && ModsConfig.BiotechActive, first);
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("world event joiner", e);
                    return;
                }
                if (p == null) continue;
                if (i == 0 && p.Name is NameTriple nt && (family || Rand.Chance(0.6f))) lastName = nt.Last;
                if (!Arrivals.Join(p, map, first != null && first.Spawned ? first.Position : (IntVec3?)null))
                {
                    Arrivals.Discard(p);
                    continue;
                }
                if (family) Relate(p, first, second, i);
                if (first == null) first = p;
                else if (second == null) second = p;
                if (ctx is SituationContext sc && sc.stranger == null) sc.stranger = p;
            }
            if (first != null && !animal.NullOrEmpty()) Bring(first, map);
        }

        /// <summary>Children young enough to be the couple's: real children with Biotech, else teenagers.</summary>
        private static FloatRange ChildAges() => ModsConfig.BiotechActive ? new FloatRange(4f, 12f) : new FloatRange(14f, 17f);

        private static void Relate(Pawn p, Pawn first, Pawn second, int i)
        {
            try
            {
                if (i == 1 && first != null) p.relations?.AddDirectRelation(PawnRelationDefOf.Spouse, first);
                else if (i >= 2)
                {
                    if (first != null) p.relations?.AddDirectRelation(PawnRelationDefOf.Parent, first);
                    if (second != null) p.relations?.AddDirectRelation(PawnRelationDefOf.Parent, second);
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event family", e);
            }
        }

        /// <summary>The animal the text speaks of, tame and the colony's, beside the first of them.</summary>
        private void Bring(Pawn first, Map map)
        {
            try
            {
                PawnKindDef kind = null;
                if (animal == "dog") kind = Dogs.Select(d => DefDatabase<PawnKindDef>.GetNamedSilentFail(d)).FirstOrDefault(k => k != null);
                else if (animal == "goat") kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Goat");
                else kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(animal);
                if (kind?.RaceProps == null || !kind.RaceProps.Animal) return;
                var a = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfPlayer));
                if (a == null) return;
                if (a.Name == null || a.Name.Numerical) a.Name = PawnBioAndNameGenerator.GeneratePawnName(a, NameStyle.Full);
                if (!Arrivals.Spawn(a, map, first.Spawned ? first.Position : (IntVec3?)null)) Arrivals.Discard(a);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event animal", e);
            }
        }
    }

    /// <summary>
    /// One of the game's own incidents, now: a trader caravan or visitors from the situation's people, a herd passing,
    /// a wanderer. Skipped when the game says it can't happen right now.
    /// </summary>
    public class WorldEffect_Incident : WorldEffect
    {
        public string incident;
        /// <summary>From the situation's people (their caravan, their visitors); with <see cref="enemy"/>, the second people.</summary>
        public bool withFaction;
        public bool enemy;
        /// <summary>When that people can't come, anyone may.</summary>
        public bool anyFaction;

        public override void Apply(DilemmaContext ctx)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(incident ?? "");
            var map = SituationEffectUtil.MapOf(ctx);
            if (def == null || map == null || !Gate(ctx)) return;
            var f = !withFaction ? null : enemy ? (ctx as HappeningContext)?.enemy : ctx.faction;
            if (withFaction && (f == null || f.defeated || f.HostileTo(Faction.OfPlayer)))
            {
                if (!anyFaction) return;
                f = null;
            }
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                // Only a people the game would send this way itself (a trader caravan needs caravan traders, and the weather has to suit them).
                if (f != null && def.Worker is IncidentWorker_PawnsArrive arrive && !arrive.FactionCanBeGroupSource(f, parms, false))
                {
                    if (!anyFaction) return;
                    f = null;
                }
                parms.faction = f;
                if (def.Worker.CanFireNow(parms) && def.Worker.TryExecute(parms)) return;
                if (f != null && anyFaction)
                {
                    parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                    if (def.Worker.CanFireNow(parms)) def.Worker.TryExecute(parms);
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event incident " + incident, e);
            }
        }
    }

    /// <summary>One of Rimfolk's own events, now, if it can happen (its own season, people and cooldown rules apply).</summary>
    public class WorldEffect_Event : WorldEffect
    {
        public string incident;

        public override void Apply(DilemmaContext ctx)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(incident ?? "");
            var map = SituationEffectUtil.MapOf(ctx);
            if (def == null || map == null || !def.HasModExtension<HappeningExtension>() || !Gate(ctx)) return;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                if (def.Worker.CanFireNow(parms)) Find.Storyteller.TryFire(new FiringIncident(def, null, parms), false);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("world event follow-up " + incident, e);
            }
        }
    }

    /// <summary>Takes goods out of the stores and pays for them: {COUNT} of {THING} (or the given ones) at <see cref="price"/> times their worth.</summary>
    public class WorldEffect_Sell : DilemmaEffect
    {
        public ThingDef thing;
        public int count;
        public float price = 1f;
        /// <summary>The silver comes when the situation ends, not now (a cart sent to a fair).</summary>
        public bool later;

        private ThingDef Def(DilemmaContext ctx) => thing ?? ctx.thing;

        private int Need(DilemmaContext ctx) => Mathf.Max(1, count > 0 ? count : ctx.count);

        public override string Unavailable(DilemmaContext ctx)
        {
            var d = Def(ctx);
            if (d == null) return "CC_WS_NothingToSell".Translate();
            return VarietyUtil.InStores(SituationEffectUtil.MapOf(ctx), d) < Need(ctx) ? "CC_Dilemma_NotEnough".Translate(Need(ctx), d.label).ToString() : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var d = Def(ctx);
            var map = SituationEffectUtil.MapOf(ctx);
            if (d == null || map == null) return;
            int n = Need(ctx);
            if (!VarietyUtil.TakeFromStores(map, d, n)) return;
            int silver = Mathf.RoundToInt(n * d.BaseMarketValue * price);
            var s = SituationEffectUtil.Of(ctx);
            if (later && s != null) s.AddNum("owed", silver);
            else VarietyUtil.PaySilver(map, IntVec3.Invalid, silver);
        }
    }

    /// <summary>Thoughts for the colonists the situation hurt (the warlord's raids), or for everyone else.</summary>
    public class WorldEffect_Thought : WorldEffect
    {
        public ThoughtDef thought;
        /// <summary>Only those it hurt; with <see cref="others"/>, everyone it didn't.</summary>
        public bool sufferers;
        public bool others;

        public override void Apply(DilemmaContext ctx)
        {
            var s = SituationEffectUtil.Of(ctx);
            var map = SituationEffectUtil.MapOf(ctx);
            if (thought == null || map == null || !Gate(ctx)) return;
            var hurt = s?.sufferers.Where(p => p != null && !p.Dead && p.IsColonist).ToList() ?? new List<Pawn>();
            IEnumerable<Pawn> who;
            if (sufferers) who = hurt;
            else
            {
                who = IncidentWorker_Happening.Colonists(map);
                if (others) who = who.Where(p => !hurt.Contains(p));
            }
            foreach (var p in who.ToList())
            {
                if (!onlyTrait.NullOrEmpty() && (p.story?.traits == null || !onlyTrait.Any(p.story.traits.HasTrait))) continue;
                if (!exceptTrait.NullOrEmpty() && p.story?.traits != null && exceptTrait.Any(p.story.traits.HasTrait)) continue;
                CouncilGameComponent.GiveThought(p, thought);
            }
        }
    }

    /// <summary>Another world situation may follow this one (a locust year can bring a famine).</summary>
    public class WorldEffect_Follow : WorldEffect
    {
        public string situation;

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            var inst = WorldSituations.Instance;
            if (map == null || inst == null || situation.NullOrEmpty() || !Gate(ctx)) return;
            if (inst.Running.Count() >= WorldSituations.MaxActive) return;
            inst.TryStart(map, d => d.defName == situation, false);
        }
    }

    /// <summary>Ripe crops in the open are marked for harvest (before the swarm or the frost gets them).</summary>
    public class WorldEffect_Harvest : WorldEffect
    {
        public float minGrowth = 0.6f;

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            if (map == null || !Gate(ctx)) return;
            foreach (var p in GameCondition_Hail.Crops(map))
            {
                if (p.Growth < minGrowth || !p.HarvestableNow || map.designationManager.DesignationOn(p, DesignationDefOf.HarvestPlant) != null) continue;
                map.designationManager.AddDesignation(new Designation(p, DesignationDefOf.HarvestPlant));
            }
        }
    }

    /// <summary>
    /// Silver paid out of the stores: {COUNT} times <see cref="factor"/>, rounded to tens (no quartermaster's discount,
    /// so the price in the letter is the price paid). Unavailable without enough.
    /// </summary>
    public class WorldEffect_Pay : DilemmaEffect
    {
        public float factor = 1f;

        public static int Amount(int count, float factor) => Mathf.Max(10, Mathf.RoundToInt(count * factor / 10f) * 10);

        public override string Unavailable(DilemmaContext ctx)
        {
            int need = Amount(ctx.count, factor);
            var map = SituationEffectUtil.MapOf(ctx);
            return (map?.resourceCounter.Silver ?? 0) < need ? "CC_Dilemma_NotEnough".Translate(need, ThingDefOf.Silver.label).ToString() : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            if (map == null) return;
            int left = Amount(ctx.count, factor);
            foreach (var t in map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer)).ToList())
            {
                if (left <= 0) break;
                int take = Mathf.Min(left, t.stackCount);
                t.SplitOff(take).Destroy();
                left -= take;
            }
        }
    }

    /// <summary>
    /// More of a situation's text, by key, for what its worker writes beyond the def's own lists: a warlord's
    /// nicknames, the letter for a raid he leads, a line under the start letter. Keys are the worker's to name.
    /// </summary>
    public class SituationText : DefModExtension
    {
        public List<SituationLines> lines = new List<SituationLines>();

        public List<string> Get(string key) => lines.FirstOrDefault(l => l.key == key)?.texts;
    }

    public class SituationLines
    {
        public string key;
        [MustTranslate] public List<string> texts = new List<string>();
    }

    /// <summary>Things of the colony's era worth about <see cref="value"/> silver (scaled by the colony's wealth), left where traders drop their goods.</summary>
    public class WorldEffect_Loot : WorldEffect
    {
        public FloatRange value = new FloatRange(300f, 600f);
        public bool fancy;
        /// <summary>Sent by drop pod in an industrial colony (a gift from afar); else carried to where traders drop their goods.</summary>
        public bool pod;

        public override void Apply(DilemmaContext ctx)
        {
            var map = SituationEffectUtil.MapOf(ctx);
            if (map == null || !Gate(ctx)) return;
            var things = Loot.Make(value.RandomInRange * LivingEvents.WealthFactor(map), fancy);
            Loot.Deliver(things, map, pod ? IntVec3.Invalid : DropCellFinder.TradeDropSpot(map), pod);
        }
    }

    /// <summary>A thought for one of the situation's people: A, B (a doctor, a slayer) or everyone who isn't them.</summary>
    public class WorldEffect_ThoughtFor : WorldEffect
    {
        public ThoughtDef thought;
        public bool b;

        public override void Apply(DilemmaContext ctx)
        {
            var p = b ? ctx.b : ctx.a;
            if (thought == null || p == null || p.Dead || !p.IsColonist || !Gate(ctx)) return;
            CouncilGameComponent.GiveThought(p, thought);
        }
    }

    /// <summary>Experience for A (or B): what they learned from the situation (a healer's teaching, a hard winter's work).</summary>
    public class WorldEffect_Learn : WorldEffect
    {
        public SkillDef skill;
        public float xp = 1000f;
        public bool b;

        public override void Apply(DilemmaContext ctx)
        {
            var p = b ? ctx.b : ctx.a;
            var rec = skill != null ? p?.skills?.GetSkill(skill) : null;
            if (rec == null || rec.TotallyDisabled || !Gate(ctx)) return;
            rec.Learn(xp, true);
        }
    }

    /// <summary>Silver the colony is owed when the situation ends (a share of a silver strike, a cart's takings): paid then, and told as {COUNT}.</summary>
    public class WorldEffect_Owe : WorldEffect
    {
        public IntRange amount = new IntRange(200, 400);
        public bool wealthScaled = true;

        public override void Apply(DilemmaContext ctx)
        {
            var s = SituationEffectUtil.Of(ctx);
            if (s == null || !Gate(ctx)) return;
            float v = amount.RandomInRange * (wealthScaled ? LivingEvents.WealthFactor(SituationEffectUtil.MapOf(ctx)) : 1f);
            s.AddNum("owed", Mathf.Max(10, Mathf.RoundToInt(v / 10f) * 10));
        }
    }
}
