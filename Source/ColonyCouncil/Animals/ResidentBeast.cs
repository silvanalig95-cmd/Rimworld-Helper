using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// A wild beast that stays (HerdStoryMath): a wolf, a bear, a stag that has been on the colony's land for days is
    /// given a name by a colonist, and from then on the colony watches it: whoever sees it says so, and a few days
    /// later the council is asked what to make of it (Council_DlmA_ResidentBeast: leave food out, keep close, hunt it,
    /// tame it, let it be, drive it off), and again every so often while it stays. The story closes when it is hunted,
    /// tamed, driven off, gone, or still here after a season, part of the land. Saved on the council
    /// (CouncilGameComponent.beast); runs from AnimalLife.Daily and Hourly.
    /// </summary>
    public class ResidentBeast : IExposable
    {
        public Pawn animal;
        public Pawn namer;
        public int since = -1, namedTick = -1, fed, asks, lastAskTick = -1, lastSightTick = -1, candidateId, candidateDays, fateTick = -1;
        public bool feared;
        /// <summary>The colony's last answer: watched, fed, feared, hunted, tame, driven.</summary>
        public string stance = "watched";
        /// <summary>Beasts whose story has closed (ids), not taken up again.</summary>
        public List<int> knownIds = new List<int>();
        /// <summary>With PokéWorld: the beast is a Pokémon, shiny, the one a rumour spoke of; what it has taken from the stores; the rumour itself (a species, until when).</summary>
        public bool pokemon, shiny, rumoured;
        public int stolen, lastStealTick = -1, rumourUntil = -1;
        public string rumourKind;

        public const int KnownMax = 20;
        public const string Dilemma = "Council_DlmA_ResidentBeast";

        private static int Now => Find.TickManager.TicksGame;

        public bool Here => animal != null && !animal.Dead && !animal.Destroyed && animal.Spawned;

        public float DaysNamed => namedTick < 0 ? 0f : (Now - namedTick) / (float)GenDate.TicksPerDay;

        public bool Predator => animal?.RaceProps?.predator ?? false;

        public string StanceNow => HerdStoryMath.Stance(fed, feared);

        public static bool On => AnimalLife.On && ColonyCouncilMod.Settings.strangers;

        // ---------------- The days ----------------

        public void Daily(CouncilGameComponent comp)
        {
            if (!On || comp == null) return;
            var map = HappeningTracker.HomeMap();
            if (map == null) return;
            RumourDay(map);
            if (animal != null)
            {
                Check(comp, map);
                return;
            }
            if (fateTick >= 0 && Now - fateTick < HerdStoryMath.RestDays * GenDate.TicksPerDay) return;
            var a = Candidate(map);
            if (a == null)
            {
                candidateId = 0;
                candidateDays = 0;
                return;
            }
            if (a.thingIDNumber == candidateId) candidateDays++;
            else
            {
                candidateId = a.thingIDNumber;
                candidateDays = 1;
            }
            if (candidateDays >= HerdStoryMath.ResidentAfterDays) Become(comp, map, a);
        }

        /// <summary>
        /// A plain wild animal of some size, about on the land and not a story already; with PokéWorld, a wild Pokémon too
        /// (not the rare one a sighting brought, which has its own story).
        /// </summary>
        public static bool Fits(Pawn a, Map map)
        {
            if (a?.RaceProps == null || !a.RaceProps.Animal || a.Faction != null || a.Dead || a.Downed || !a.Spawned || a.InMentalState) return false;
            if (map != null && map.fogGrid.IsFogged(a.Position)) return false;
            if (PokeWorldLink.IsPokemon(a)) return PokeWorldLink.PokemonShare() > 0f && !PokeWorldLife.IsRareOne(a) && HerdStoryMath.PokemonFits(a.RaceProps.baseBodySize);
            return HerdStoryMath.BeastFits(a.RaceProps.baseBodySize, a.RaceProps.predator, a.RaceProps.Insect, Strangers.IsMajestic(a));
        }

        private bool Rumoured(Pawn a) => rumourKind != null && rumourUntil > Now && a?.def?.defName == rumourKind;

        /// <summary>The one watched already; else the rumour's species, then a Pokémon over a plain beast, then the biggest.</summary>
        private Pawn Candidate(Map map)
        {
            var fits = map.mapPawns.AllPawnsSpawned.Where(a => Fits(a, map) && !knownIds.Contains(a.thingIDNumber)).ToList();
            if (fits.Count == 0) return null;
            var same = fits.FirstOrDefault(a => a.thingIDNumber == candidateId);
            if (same != null) return same;
            return fits.OrderByDescending(a => Rumoured(a) ? 3 : PokeWorldLink.IsPokemon(a) ? 2 : 1).ThenByDescending(a => a.RaceProps.baseBodySize).First();
        }

        // ---------------- The shiny rumour (with PokéWorld) ----------------

        /// <summary>A trainer's word of a shiny of this species: the colony keeps an eye out for twenty days.</summary>
        public void Rumour(ThingDef species)
        {
            if (species == null) return;
            rumourKind = species.defName;
            rumourUntil = Now + Mathf.RoundToInt(HerdStoryMath.RumourDays * GenDate.TicksPerDay);
            News.Post("CC_PW_RumourHeld".Translate(species.label.Named("SPECIES"), TextUtil.DaysString(rumourUntil - Now).Named("DAYS")).Resolve(), null, NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
        }

        /// <summary>While the rumour holds: the shiny may show itself on the land; when it runs out, it was a story.</summary>
        private void RumourDay(Map map)
        {
            if (rumourKind == null) return;
            var species = DefDatabase<ThingDef>.GetNamedSilentFail(rumourKind);
            if (rumourUntil <= Now)
            {
                if (species != null) News.Post("CC_PW_RumourFaded".Translate(species.label.Named("SPECIES")).Resolve(), null, NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Flavor);
                rumourKind = null;
                return;
            }
            if (animal != null || species == null || !PokeWorldLink.Active || !Rand.Chance(HerdStoryMath.RumourChance)) return;
            var kind = species.race?.AnyPawnKind ?? DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.race == species);
            if (kind == null || !map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(species, 0f)) return;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Animal)) return;
            try
            {
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, null, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true));
                if (p == null) return;
                GenSpawn.Spawn(p, cell, map);
                PokeWorldLife.SetShiny(p);
                // Seen from today: named tomorrow, if it's still here.
                candidateId = p.thingIDNumber;
                candidateDays = HerdStoryMath.ResidentAfterDays - 1;
                News.Post("CC_PW_RumourShows".Translate(species.label.Named("SPECIES")).Resolve(), new LookTargets(p), NewsKind.Land, MessageTypeDefOf.PositiveEvent, Importance.Notable);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("shiny rumour", e);
            }
        }

        /// <summary>A Pokémon not fed nor caught gets into the stores now and then: a few of what it would eat, told in the morning.</summary>
        private void Steal(CouncilGameComponent comp, Map map)
        {
            lastStealTick = Now;
            string what = DilemmaEffect_FeedBeast.Food(animal, map);
            ThingDef def = what == null ? null : DefDatabase<ThingDef>.GetNamedSilentFail(what);
            if (def == null)
                def = map.listerThings.AllThings.Where(t => t.Spawned && t.def.IsNutritionGivingIngestible && t.def.ingestible.HumanEdible && !t.def.IsMeat && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage())
                    .GroupBy(t => t.def).OrderByDescending(g => g.Sum(t => t.stackCount)).FirstOrDefault()?.Key;
            if (def == null) return;
            int n = Rand.RangeInclusive(5, 8);
            if (!Couriers.TakeFromStores(map, def, n, null)) return;
            stolen++;
            string text = "CC_Beast_Stole".Translate(animal.Named("PET"), n.Named("COUNT"), def.label.Named("THING")).Resolve();
            News.Post(text, new LookTargets(animal), NewsKind.Land, MessageTypeDefOf.NegativeEvent, Importance.Notable);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, namer));
        }

        private void Become(CouncilGameComponent comp, Map map, Pawn a)
        {
            animal = a;
            since = namedTick = Now;
            asks = 0;
            fed = 0;
            feared = false;
            stance = "watched";
            lastAskTick = -1;
            lastSightTick = -1;
            namer = Namer(map);
            pokemon = PokeWorldLink.IsPokemon(a);
            shiny = pokemon && PokeWorldLife.Shiny(a);
            rumoured = pokemon && Rumoured(a);
            stolen = 0;
            lastStealTick = -1;
            if (rumoured) rumourKind = null;
            HerdStory.Name(a);
            string kind = AnimalFacts.KindLabel(a);
            try
            {
                var who = namer ?? HerdStory.Keeper(map, null);
                if (who != null)
                {
                    Find.LetterStack.ReceiveLetter("CC_Beast_NamedTitle".Translate(kind.Named("KIND")).Resolve(),
                        "CC_Beast_Named".Translate(a.Named("PET"), who.Named("PAWN"), kind.Named("KIND")).Resolve(), LetterDefOf.NeutralEvent, new LookTargets(a));
                    comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, "CC_Beast_NamedChronicle".Translate(a.Named("PET"), who.Named("PAWN"), kind.Named("KIND")).Resolve(), who));
                    Interplay.Journal(who, "Council_Journal_Beast_Named", Tags().ToArray(), a.Named("PET"), kind.Named("KIND"));
                    if (rumoured && shiny) CouncilGameComponent.GiveThought(who, PokeWorldCampDefOf.Council_PW_RumourTrue);
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beast named", e);
            }
        }

        /// <summary>Who names it: a child, else someone with a love of animals, else anyone.</summary>
        private static Pawn Namer(Map map)
        {
            var people = IncidentWorker_Happening.Colonists(map).Where(p => People.HasInnerLife(p) && !p.DevelopmentalStage.Baby()).ToList();
            if (people.Count == 0) return null;
            var child = people.Where(p => p.DevelopmentalStage.Child()).RandomElementWithFallback();
            if (child != null && Rand.Chance(0.6f)) return child;
            var lover = people.Where(p => !p.DevelopmentalStage.Juvenile() && p.skills?.GetSkill(SkillDefOf.Animals) is SkillRecord r && People.Passionate(r)).RandomElementWithFallback();
            return lover ?? people.Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback() ?? people.RandomElement();
        }

        private void Check(CouncilGameComponent comp, Map map)
        {
            if (animal.Dead || animal.Destroyed)
            {
                End(comp, stance == "hunted" ? "hunted" : "died");
                return;
            }
            if (animal.Faction == Faction.OfPlayer)
            {
                End(comp, "tamed");
                return;
            }
            if (!animal.Spawned || animal.Map != map)
            {
                End(comp, stance == "driven" ? "driven" : "gone");
                return;
            }
            if (DaysNamed >= HerdStoryMath.FateAfterDays)
            {
                End(comp, "stays");
                return;
            }
            float sinceSteal = lastStealTick < 0 ? 99f : (Now - lastStealTick) / (float)GenDate.TicksPerDay;
            if (pokemon && HerdStoryMath.StealDue(stance, DaysNamed, sinceSteal) && Rand.Chance(HerdStoryMath.StealChance)) Steal(comp, map);
            float sinceAsk = lastAskTick < 0 ? 0f : (Now - lastAskTick) / (float)GenDate.TicksPerDay;
            if (!HerdStoryMath.AskDue(DaysNamed, asks, sinceAsk)) return;
            string def = pokemon ? PokemonDilemma : Dilemma;
            if (comp.dilemmas.TryFire(true, d => d.defName == def))
            {
                asks++;
                lastAskTick = Now;
            }
        }

        public const string PokemonDilemma = "Council_DlmA_ResidentPokemon";

        /// <summary>The beast's tags for its lines: predator or grazer, the stance, pokemon, shiny, rumoured, thief, catch.</summary>
        public List<string> Tags()
        {
            var tags = new List<string> { Predator ? "predator" : "grazer", StanceNow };
            if (pokemon) tags.Add("pokemon");
            if (shiny) tags.Add("shiny");
            if (rumoured) tags.Add("rumoured");
            if (stolen > 0) tags.Add("thief");
            if (stance == "catch") tags.Add("catch");
            return tags;
        }

        private void End(CouncilGameComponent comp, string key)
        {
            var a = animal;
            try
            {
                var who = namer != null && !namer.Dead ? namer : null;
                if (key == "tamed" && pokemon) key = "caught";
                string text = ("CC_Beast_End_" + key).Translate(a.Named("PET"), (who ?? a).Named("PAWN")).Resolve();
                News.Post(text, a.Spawned ? new LookTargets(a) : LookTargets.Invalid, NewsKind.Land, MessageTypeDefOf.NeutralEvent, Importance.Notable);
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, who));
                if (key == "stays" && who != null && who.Spawned) CouncilGameComponent.GiveThought(who, HerdStoryDefOf.Council_Beast_PartOfTheLand);
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beast's end", e);
            }
            if (a != null)
            {
                knownIds.Add(a.thingIDNumber);
                while (knownIds.Count > KnownMax) knownIds.RemoveAt(0);
            }
            animal = null;
            namer = null;
            fateTick = Now;
            candidateId = 0;
            candidateDays = 0;
        }

        // ---------------- Sightings ----------------

        /// <summary>Every hour: someone in sight of the beast says so, now and then.</summary>
        public void Hourly(CouncilGameComponent comp)
        {
            if (!On || !Here || Now - lastSightTick < GenDate.TicksPerHour * 6 || !Rand.Chance(0.5f)) return;
            var map = animal.Map;
            if (map == null) return;
            var p = map.mapPawns.FreeColonistsSpawned
                .Where(c => People.HasInnerLife(c) && !c.Downed && !c.Drafted && c.Awake() && c.Position.InHorDistOf(animal.Position, 16f) && GenSight.LineOfSight(c.Position, animal.Position, map))
                .RandomElementWithFallback();
            if (p == null) return;
            var tags = Tags();
            if (p.DevelopmentalStage.Juvenile()) tags.Add("child");
            string line = Flavor.FromPool("Council_Pool_BeastSighting", p, null, null, tags.ToArray());
            if (line.NullOrEmpty()) return;
            // apart: a remark on seeing the beast, said to nobody in particular; the beast is where they look.
            if (Speech.Say(p, line.Formatted(animal.Named("PET"), p.Named("PAWN")).Resolve())) lastSightTick = Now;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref animal, "animal");
            Scribe_References.Look(ref namer, "namer");
            Scribe_Values.Look(ref since, "since", -1);
            Scribe_Values.Look(ref namedTick, "namedTick", -1);
            Scribe_Values.Look(ref fed, "fed");
            Scribe_Values.Look(ref asks, "asks");
            Scribe_Values.Look(ref lastAskTick, "lastAskTick", -1);
            Scribe_Values.Look(ref lastSightTick, "lastSightTick", -1);
            Scribe_Values.Look(ref candidateId, "candidateId");
            Scribe_Values.Look(ref candidateDays, "candidateDays");
            Scribe_Values.Look(ref fateTick, "fateTick", -1);
            Scribe_Values.Look(ref feared, "feared");
            Scribe_Values.Look(ref stance, "stance", "watched");
            Scribe_Collections.Look(ref knownIds, "knownIds", LookMode.Value);
            Scribe_Values.Look(ref pokemon, "pokemon");
            Scribe_Values.Look(ref shiny, "shiny");
            Scribe_Values.Look(ref rumoured, "rumoured");
            Scribe_Values.Look(ref stolen, "stolen");
            Scribe_Values.Look(ref lastStealTick, "lastStealTick", -1);
            Scribe_Values.Look(ref rumourUntil, "rumourUntil", -1);
            Scribe_Values.Look(ref rumourKind, "rumourKind");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (knownIds == null) knownIds = new List<int>();
                if (stance.NullOrEmpty()) stance = "watched";
            }
        }
    }

    /// <summary>The question about the beast that stays: X the beast, A the one who named it (grown) or whoever is best with animals, B the best shot.</summary>
    public class DilemmaWorker_ResidentBeast : DilemmaWorker
    {
        private static ResidentBeast Beast => CouncilGameComponent.Instance?.beast;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var rb = Beast;
            if (rb == null || !rb.Here || rb.animal.Map != map || !CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && People.HasInnerLife(p) && !p.DevelopmentalStage.Juvenile()).ToList();
            if (pawns.Count < def.minColonists) return false;
            var n = rb.namer;
            var a = n != null && !n.Dead && n.Spawned && n.Map == map && n.IsColonist && !n.DevelopmentalStage.Juvenile() ? n : VarietyUtil.Best(map, SkillDefOf.Animals);
            if (a == null) return false;
            var b = pawns.Where(p => p != a).OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0).FirstOrDefault();
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b, stranger = rb.animal };
            return true;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            var rb = Beast;
            if (rb == null) return;
            foreach (var t in rb.Tags()) tags.Add(t);
            if (rb.asks >= 1) tags.Add("asked" + Mathf.Min(3, rb.asks + 1));
        }
    }

    /// <summary>The colony's answer about the beast, kept for its lines and its end: fed, feared, hunted, tame, watched, driven.</summary>
    public class DilemmaEffect_BeastStance : DilemmaEffect
    {
        public string stance = "watched";

        public override void Apply(DilemmaContext ctx)
        {
            var rb = CouncilGameComponent.Instance?.beast;
            if (rb == null || rb.animal == null) return;
            rb.stance = stance;
            if (stance == "fed")
            {
                rb.fed++;
                rb.feared = false;
            }
            else if (stance == "feared") rb.feared = true;
        }
    }

    /// <summary>Food for the beast, carried out to it and left (PlayOut/): meat for a meat-eater, hay or raw food for a grazer.</summary>
    public class DilemmaEffect_FeedBeast : DilemmaEffect
    {
        public int count = 10;

        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            var map = ctx?.map ?? x?.Map;
            if (x == null || map == null || !x.Spawned) return;
            string what = Food(x, map);
            if (what == null) return;
            // apart: a colonist carries the food out to where the beast is and leaves it (CarryOut to the stranger).
            new DilemmaEffect_CarryOut { what = what, count = count, who = PlayWho.A, to = PlaySpot.Stranger, steward = false, label = "CC_Beast_FeedLabel".Translate(x.Named("PET")).Resolve() }.Apply(ctx);
        }

        /// <summary>What it would eat, of what the colony has: the meat there is most of, hay, or any raw food.</summary>
        public static string Food(Pawn x, Map map)
        {
            bool meat = x.RaceProps?.Eats(FoodTypeFlags.Meat) ?? false;
            if (meat)
            {
                var best = map.listerThings.AllThings.Where(t => t.Spawned && t.def.IsMeat && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage())
                    .GroupBy(t => t.def).OrderByDescending(g => g.Sum(t => t.stackCount)).FirstOrDefault();
                return best?.Key.defName;
            }
            var hay = DefDatabase<ThingDef>.GetNamedSilentFail("Hay");
            if (hay != null && map.listerThings.ThingsOfDef(hay).Any(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer))) return hay.defName;
            return "rawfood";
        }
    }

    /// <summary>The beast is driven off the land: it makes for the nearest edge and goes (a group of its own with LordJob_ExitMapNear).</summary>
    public class DilemmaEffect_DriveOff : DilemmaEffect
    {
        public override void Apply(DilemmaContext ctx)
        {
            var x = ctx?.stranger;
            if (x == null || x.Dead || !x.Spawned || x.Faction != null || x.Map == null) return;
            try
            {
                x.GetLord()?.RemovePawn(x);
                LordMaker.MakeNewLord(null, new LordJob_ExitMapNear(x.Position, LocomotionUrgency.Jog, 14f), x.Map, new List<Pawn> { x });
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("beast driven off", e);
            }
        }
    }
}
