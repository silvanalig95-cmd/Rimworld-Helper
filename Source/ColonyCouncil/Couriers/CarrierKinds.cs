using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Who a people sends to carry, and on what (CarrierMath has the rules; the player, 10 November 2026: "the courier and
    /// mount looks always exactly the same ... the courier should have look variety that makes sense within the faction,
    /// like for the other npcs they create"). Every courier, porter and post rider is made new each time
    /// (<see cref="Arrivals.Make"/>, forceGenerateNewPawn), so the sameness was the kind: one light guard of their caravans,
    /// else always their ordinary member, often a kind with a fixed coat; and the beast: their caravans' pack animals filtered
    /// by the land's pack-animal list, which in most lands leaves one (a muffalo), else the horse every time. Now the kind is
    /// drawn from their whole range of people sent on the road (caravan guards and their visitors, villagers and settlers),
    /// weighted as the game weighs them, and the beast from every beast of theirs big enough to ride that the land and season
    /// allow; the one sent last time counts half as much next time. Their apparel, looks and weapons then vary as the
    /// faction's own visitors' do.
    /// </summary>
    public static class CarrierKinds
    {
        /// <summary>What each people sent last (by the faction's id): not saved, a load starts afresh.</summary>
        private static readonly Dictionary<int, string> lastKind = new Dictionary<int, string>();
        private static readonly Dictionary<int, string> lastMount = new Dictionary<int, string>();

        public static void Reset()
        {
            lastKind.Clear();
            lastMount.Clear();
        }

        /// <summary>The faction's own range of people to send on the road, for the pure rule.</summary>
        public static List<CarrierKindOption> KindOptions(Faction f)
        {
            var list = new List<CarrierKindOption>();
            var makers = f?.def?.pawnGroupMakers;
            if (makers != null)
                for (int g = 0; g < makers.Count; g++)
                {
                    var m = makers[g];
                    if (m == null) continue;
                    if (m.kindDef == PawnGroupKindDefOf.Trader && m.guards != null)
                        foreach (var o in m.guards) Add(list, o, 0, g, m.commonality);
                    else if (m.kindDef == PawnGroupKindDefOf.Peaceful && m.options != null)
                        foreach (var o in m.options) Add(list, o, 1, g, m.commonality);
                }
            var basic = f?.def?.basicMemberKind;
            if (basic != null) list.Add(Option(basic, 2, -1, 1f, 1f));
            return list;
        }

        private static void Add(List<CarrierKindOption> list, PawnGenOption o, int source, int group, float commonality)
        {
            if (o?.kind == null) return;
            list.Add(Option(o.kind, source, group, commonality, o.selectionWeight));
        }

        private static CarrierKindOption Option(PawnKindDef k, int source, int group, float commonality, float weight) =>
            new CarrierKindOption
            {
                name = k.defName, source = source, group = group, groupCommonality = commonality, selectionWeight = weight, power = k.combatPower,
                person = k.RaceProps?.Humanlike ?? false, trader = k.trader,
                adult = !k.pawnGroupDevelopmentStage.HasValue || k.pawnGroupDevelopmentStage.Value == DevelopmentalStage.Adult,
                titled = k.titleRequired != null || k.minTitleRequired != null || !k.titleSelectOne.NullOrEmpty(), leader = k.factionLeader
            };

        /// <summary>
        /// The kind of person this people sends to carry (a courier, a porter, the post rider): drawn fresh each time from
        /// their range (CarrierMath.KindPool, Pick); their ordinary person (<see cref="Arrivals.PersonKind"/>) when the range
        /// gives nobody. Null: nobody to send.
        /// </summary>
        public static PawnKindDef Person(Faction f)
        {
            if (f?.def == null) return null;
            try
            {
                var pool = CarrierMath.KindPool(KindOptions(f));
                lastKind.TryGetValue(f.loadID, out var last);
                var name = CarrierMath.Pick(pool, Rand.Value, last);
                var kind = name != null ? DefDatabase<PawnKindDef>.GetNamedSilentFail(name) : null;
                if (kind != null)
                {
                    lastKind[f.loadID] = kind.defName;
                    return kind;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carriers: who is sent", e);
            }
            return Arrivals.PersonKind(f);
        }

        /// <summary>Someone this people could send at all.</summary>
        public static bool AnyPerson(Faction f) => CarrierMath.KindPool(KindOptions(f)).Count > 0 || Arrivals.PersonKind(f) != null;

        private static readonly Dictionary<ThingDef, bool> wildSomewhere = new Dictionary<ThingDef, bool>();

        /// <summary>The beast lives wild in some land of the game (a farm beast, such as a donkey bred by people, may not).</summary>
        private static bool WildSomewhere(PawnKindDef k)
        {
            if (k?.race == null) return false;
            if (wildSomewhere.TryGetValue(k.race, out var w)) return w;
            bool any = false;
            try
            {
                any = DefDatabase<BiomeDef>.AllDefsListForReading.Any(b => b != null && BiomeAnimals.LivesIn(b, k));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carriers: where a beast lives", e);
            }
            wildSomewhere[k.race] = any;
            return any;
        }

        /// <summary>The beasts this people's caravans use, for the pure rule, judged against this map's land and season.</summary>
        public static List<CarrierMountOption> MountOptions(Faction f, Map map)
        {
            var list = new List<CarrierMountOption>();
            var makers = f?.def?.pawnGroupMakers;
            if (makers == null || map == null) return list;
            var biome = map.Biome;
            foreach (var m in makers)
            {
                if (m?.carriers == null) continue;
                foreach (var o in m.carriers)
                {
                    var k = o?.kind;
                    if (k?.race?.race == null) continue;
                    bool land = biome == null || biome.IsPackAnimalAllowed(k.race) || BiomeAnimals.LivesIn(biome, k) || !WildSomewhere(k);
                    list.Add(new CarrierMountOption
                    {
                        name = k.defName, weight = o.selectionWeight, bodySize = k.RaceProps.baseBodySize, animal = k.RaceProps.Animal, predator = k.RaceProps.predator,
                        ridable = GiddyUpMounts.Ridable(k.race), landOk = land, seasonOk = map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race, 0f)
                    });
                }
            }
            return list;
        }

        /// <summary>
        /// A beast for a carrier to ride in on: drawn fresh each time from the beasts of their caravans big enough to ride that
        /// the land and season allow (CarrierMath.MountPool, Pick), else a horse where the season allows; null: on foot.
        /// </summary>
        public static PawnKindDef Mount(Faction f, Map map)
        {
            if (map == null) return null;
            try
            {
                var pool = CarrierMath.MountPool(MountOptions(f, map));
                int id = f?.loadID ?? -1;
                lastMount.TryGetValue(id, out var last);
                var name = CarrierMath.Pick(pool, Rand.Value, last);
                if (name == null)
                {
                    var horse = DefDatabase<PawnKindDef>.GetNamedSilentFail("Horse");
                    name = CarrierMath.Fallback(horse?.race != null && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(horse.race, 0f), horse?.race != null);
                }
                var kind = name != null ? DefDatabase<PawnKindDef>.GetNamedSilentFail(name) : null;
                if (kind != null) lastMount[id] = kind.defName;
                return kind;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("carriers: a mount", e);
                return null;
            }
        }
    }
}
