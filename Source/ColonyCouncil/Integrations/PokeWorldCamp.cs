using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Traveling trainers (Council_Camp_Trainers, with PokéWorld): their Pokémon as the camp's animals, the friendly
    /// contest with the colony's Pokémon as a show at their fire (DilemmaEffect_PokemonShow), and the shiny rumour a
    /// trainer at the gate brings (HappeningWorker_ShinyRumour, DilemmaEffect_ShinyRumour) that the beast that stays
    /// (Animals/ResidentBeast.cs) may make true. Everything by name: without PokéWorld none of it comes.
    /// </summary>
    public static class PokeWorldCamp
    {
        /// <summary>A roaming kind of Pokémon for a trainer's belt: the ones the colony hasn't caught likelier, any that stands the weather here.</summary>
        public static PawnKindDef TrainerKind(Map map)
        {
            if (!PokeWorldLink.Active || map == null) return null;
            var owned = PokeWorldLink.OwnedSpecies(map);
            var kinds = PokeWorldLink.Roaming()
                .Select(r => r.race?.AnyPawnKind ?? DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.race == r))
                .Where(k => k?.race?.race != null && k.RaceProps.Animal && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f))
                .ToList();
            return kinds.RandomElementByWeightWithFallback(k => owned.Contains(k.race) ? 0.5f : 1f);
        }

        /// <summary>The colony's Pokémon with a trainer, the highest level first; null when it has none.</summary>
        public static Pawn OurBest(Map map)
        {
            if (!PokeWorldLink.Active || map == null) return null;
            return map.mapPawns.SpawnedColonyAnimals
                .Where(a => PokeWorldLink.IsPokemon(a) && !a.Dead && !a.Downed && PokeWorldLife.TrainerOf(a) is Pawn t && t.Spawned && t.Map == map && !t.Downed)
                .OrderByDescending(PokeWorldLife.Level).FirstOrDefault();
        }

        /// <summary>The camp's Pokémon, the highest level first; null when it brought none that is still here.</summary>
        public static Pawn TheirBest(Camp c)
        {
            if (c == null || !PokeWorldLink.Active) return null;
            return c.herd.Where(a => a != null && !a.Dead && !a.Downed && a.Spawned && a.Map == c.map && PokeWorldLink.IsPokemon(a)).OrderByDescending(PokeWorldLife.Level).FirstOrDefault();
        }
    }

    /// <summary>
    /// A friendly contest between the colony's best Pokémon and the trainers' best, as a show at the camp's fire: a
    /// gathering there this afternoon or evening (PlayOut/, the camp's people as guests), and when it's told, a winner
    /// by the two levels (HerdStoryMath.ShowWinChance), never surely. Nobody is hurt. The camp keeps the score, and brings
    /// it back when it returns (a rematch).
    /// </summary>
    public class DilemmaEffect_PokemonShow : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            if (c == null) return "CC_CampAsk_NotNow".Translate().ToString();
            return PokeWorldCamp.OurBest(c.map) == null || PokeWorldCamp.TheirBest(c) == null ? "CC_CampAsk_NoPokemon".Translate().ToString() : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampOnMap.For(ctx);
            var map = c?.map;
            if (c == null || map == null) return;
            var ours = PokeWorldCamp.OurBest(map);
            var theirs = PokeWorldCamp.TheirBest(c);
            if (ours == null || theirs == null) return;
            var trainer = PokeWorldLife.TrainerOf(ours) ?? ctx.a;
            var theirTrainer = c.leader ?? ctx.stranger;
            if (trainer == null) return;
            bool rematch = c.contests > 0;
            bool won = Rand.Chance(HerdStoryMath.ShowWinChance(PokeWorldLife.Level(ours), PokeWorldLife.Level(theirs)));
            c.contests++;
            if (won) c.contestsWon++;
            var comp = CouncilGameComponent.Instance;
            string kind = AnimalFacts.KindLabel(theirs);
            try
            {
                // apart: the show is the gathering at the camp's fire (PlayOut/); the colony's trainer goes, and whoever wants to watch, with the camp's people as guests.
                var gather = new DilemmaEffect_Gathering
                {
                    label = "CC_PW_ShowLabel".Translate().Resolve(), spots = new List<PlaySpot> { PlaySpot.Camp }, organizer = PlayWho.A,
                    hours = new IntRange(13, 19), duration = new FloatRange(1f, 1.5f), guests = "camp", guestCount = 4
                };
                var gctx = new DilemmaContext { map = map, a = trainer, b = ctx.b, stranger = theirTrainer, pet = ours };
                gather.Apply(gctx);
                string text = (won ? "CC_PW_ShowWon" : "CC_PW_ShowLost").Translate(ours.Named("PET"), trainer.Named("PAWN"), (theirTrainer ?? trainer).Named("OTHER"), kind.Named("KIND"), (theirTrainer ?? trainer).Named("X")).Resolve();
                News.Post(text, new LookTargets(new[] { ours, theirs }), NewsKind.Land, MessageTypeDefOf.PositiveEvent, Importance.Notable);
                comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, (won ? "CC_PW_ShowChronicle_Won" : "CC_PW_ShowChronicle_Lost").Translate(ours.Named("PET"), kind.Named("KIND")).Resolve(), trainer));
                CouncilGameComponent.GiveThought(trainer, won ? PokeWorldCampDefOf.Council_PW_ShowWon : PokeWorldCampDefOf.Council_PW_ShowLost);
                foreach (var p in IncidentWorker_Happening.Colonists(map))
                    if (p != trainer && People.HasInnerLife(p) && Rand.Chance(0.5f)) CouncilGameComponent.GiveThought(p, PokeWorldCampDefOf.Council_PW_WatchedTheShow);
                var tags = new List<string> { won ? "won" : "lost" };
                if (rematch) tags.Add("rematch");
                comp?.journals.WriteTagged(trainer, "Council_Journal_PW_Show", tags.ToArray(), null, ours.Named("PET"), (theirTrainer ?? trainer).Named("OTHER"), kind.Named("KIND"));
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("pokémon show", e);
            }
        }
    }

    /// <summary>A trainer at the gate with word of a shiny Pokémon ({TALE}: its species) seen in the region.</summary>
    public class HappeningWorker_ShinyRumour : HappeningWorker_Visitors
    {
        private static IEnumerable<ThingDef> Species() => PokeWorldLink.Roaming();

        protected override bool CanHappen(Map map, HappeningExtension ext) =>
            PokeWorldLink.Active && PokeWorldLink.PokemonShare() > 0f && Species().Any() && (CouncilGameComponent.Instance?.beast?.rumourUntil ?? -1) < Find.TickManager.TicksGame && base.CanHappen(map, ext);

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = Arrivals.Friendly();
            if (ctx.faction == null) return false;
            var owned = PokeWorldLink.OwnedSpecies(ctx.map);
            ctx.thing = Species().RandomElementByWeightWithFallback(r => owned.Contains(r) ? 0.4f : 1f);
            if (ctx.thing == null) return false;
            ctx.tale = ctx.thing.label;
            MakeGroup(ctx, ext, 1, ext.ages);
            var people = Colonists(ctx.map).Where(p => !p.DevelopmentalStage.Baby()).ToList();
            ctx.a = people.Where(p => PeopleTags.Has(p, "trainer")).RandomElementWithFallback()
                    ?? people.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0).FirstOrDefault();
            return ctx.a != null && ctx.stranger != null;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(new List<Pawn> { ctx.stranger }, ctx.map, ctx.faction, null, ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            base.AddTags(ctx, tags);
            tags.Add(ctx.thing != null && PokeWorldLink.OwnedSpecies(ctx.map).Contains(ctx.thing) ? "known" : "new");
            if (ctx.a != null && PeopleTags.Has(ctx.a, "trainer")) tags.Add("trainer");
        }
    }

    /// <summary>The colony keeps an eye out: the rumour holds for twenty days (Animals/ResidentBeast.cs makes it true, or not).</summary>
    public class DilemmaEffect_ShinyRumour : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var rb = CouncilGameComponent.Instance?.beast;
            if (rb == null || ctx?.thing == null) return;
            rb.Rumour(ctx.thing);
        }
    }

    /// <summary>
    /// The colonist goes out to the Pokémon with a ball at the belt (a visit to it, PlayOut/); the throw itself is the
    /// player's, with PokéWorld's own balls, and the catch is told when its faction changes. Unavailable without a belt.
    /// </summary>
    public class DilemmaEffect_CatchPokemon : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx) =>
            ctx?.map != null && !PokeWorldLink.CanCatch(ctx.map) ? "CC_Beast_NoBelt".Translate().ToString() : null;

        public override void Apply(DilemmaContext ctx)
        {
            if (ctx?.stranger == null || ctx.a == null) return;
            // apart: the colonist walks out to where the Pokémon is and stays a while (PlayOut/); the ball is theirs to throw.
            new DilemmaEffect_Visit { to = PlaySpot.Stranger, duration = new FloatRange(1.5f, 2.5f), hours = new IntRange(6, 18), label = "CC_PW_CatchLabel".Translate(ctx.stranger.Named("PET")).Resolve() }.Apply(ctx);
        }
    }

    [DefOf]
    public static class PokeWorldCampDefOf
    {
        public static ThoughtDef Council_PW_ShowWon;
        public static ThoughtDef Council_PW_ShowLost;
        public static ThoughtDef Council_PW_WatchedTheShow;
        public static ThoughtDef Council_PW_RumourTrue;

        static PokeWorldCampDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PokeWorldCampDefOf));
        }
    }
}
