using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>Who someone on the map is, from the colony's point of view, when they aren't one of us.</summary>
    public enum StrangerKind
    {
        None,
        /// <summary>Friendly outsiders: visitors, caravan guards and carriers, Hospitality guests.</summary>
        Visitor,
        /// <summary>The trader of a caravan (the one you deal with).</summary>
        Trader,
        /// <summary>Quest guests staying with the colony: nobles, refugees, fugitives.</summary>
        Lodger,
        /// <summary>The colony's prisoners.</summary>
        Prisoner,
        /// <summary>Enemies lying downed after a fight.</summary>
        Fallen,
        /// <summary>The colony's animals.</summary>
        Pet,
        /// <summary>Wild animals, calm ones (not manhunting).</summary>
        Wild
    }

    /// <summary>Requirements for the stranger (X) of an encounter or a dilemma. All given conditions must hold.</summary>
    public class StrangerRole
    {
        public StrangerKind kind;
        // ---- People ----
        /// <summary>Holds a royal title.</summary>
        public bool royal;
        /// <summary>Must be a child; <see cref="adult"/> must be an adult. Neither: anyone.</summary>
        public bool child;
        public bool adult;
        /// <summary>Their opinion of A must lie in this range.</summary>
        public int minOpinionOfA = -100;
        public int maxOpinionOfA = 100;
        /// <summary>A's opinion of them must lie in this range.</summary>
        public int minOpinionFromA = -100;
        public int maxOpinionFromA = 100;
        /// <summary>Their faction's goodwill with the colony must lie in this range.</summary>
        public int minGoodwill = -100;
        public int maxGoodwill = 100;
        /// <summary>Any one of these traits.</summary>
        public List<TraitDef> anyTrait;
        // ---- Animals ----
        public bool predator;
        public bool notPredator;
        public float minBodySize;
        public float maxBodySize = 999f;
        /// <summary>A rare and remarkable creature: huge, or as wild as a thrumbo.</summary>
        public bool majestic;
        /// <summary>A's own animal: bonded to A, or A is its master.</summary>
        public bool ofA;
        /// <summary>Bonded to A.</summary>
        public bool bondedToA;
        /// <summary>Not A's animal.</summary>
        public bool notOfA;
        /// <summary>Has a name.</summary>
        public bool named;
        // ---- Anyone ----
        /// <summary>Within this many cells of A (0 = anywhere on the map).</summary>
        public float maxDistance;
        /// <summary>Someone's nemesis (a raider who got away and is back, or is our prisoner; a named beast).</summary>
        public bool nemesis;
        /// <summary>A's own nemesis.</summary>
        public bool nemesisOfA;
    }

    public static class Strangers
    {
        public static bool IsHuman(StrangerKind k) => k == StrangerKind.Visitor || k == StrangerKind.Trader || k == StrangerKind.Lodger || k == StrangerKind.Prisoner || k == StrangerKind.Fallen;

        /// <summary>What kind of stranger this pawn is, or None (a colonist, an active raider, a mechanoid, a corpse...).</summary>
        public static StrangerKind KindOf(Pawn x)
        {
            if (x == null || x.Dead || !x.Spawned || x.RaceProps == null) return StrangerKind.None;
            if (x.RaceProps.Humanlike)
            {
                if (!People.HasInnerLife(x)) return StrangerKind.None;
                if (x.IsQuestLodger()) return x.Downed || x.InMentalState ? StrangerKind.None : StrangerKind.Lodger;
                if (x.IsPrisonerOfColony) return StrangerKind.Prisoner;
                if (x.Faction == null || x.Faction.IsPlayer || x.IsSlave) return StrangerKind.None;
                if (x.Faction.HostileTo(Faction.OfPlayer)) return x.Downed && !x.InMentalState ? StrangerKind.Fallen : StrangerKind.None;
                if (x.Downed || x.InMentalState) return StrangerKind.None;
                // Only people here to visit or trade: not a ceremony's envoy, permit troops or a quest's pawns.
                if (QuestUtility.IsReservedByQuestOrQuestBeingGenerated(x)) return StrangerKind.None;
                var lord = x.GetLord();
                if (lord?.LordJob is LordJob_TradeWithColony)
                {
                    var role = TraderCaravanUtility.GetTraderCaravanRole(x);
                    if (role == TraderCaravanRole.Trader) return StrangerKind.Trader;
                    if (role == TraderCaravanRole.Chattel) return StrangerKind.None;
                    return StrangerKind.Visitor;
                }
                if (lord?.LordJob is LordJob_VisitColony || IsHospitalityGuest(x)) return StrangerKind.Visitor;
                return StrangerKind.None;
            }
            if (x.RaceProps.Animal)
            {
                if (x.Faction == Faction.OfPlayer) return x.Downed || x.InMentalState ? StrangerKind.None : StrangerKind.Pet;
                if (x.Faction == null && !x.Downed && !x.InMentalState) return StrangerKind.Wild;
            }
            return StrangerKind.None;
        }

        /// <summary>Everyone of this kind on the map.</summary>
        public static IEnumerable<Pawn> On(Map map, StrangerKind kind)
        {
            if (map == null || kind == StrangerKind.None) yield break;
            var all = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
                if (KindOf(all[i]) == kind) yield return all[i];
        }

        /// <summary>The kinds present on the map right now.</summary>
        public static HashSet<StrangerKind> Present(Map map)
        {
            var set = new HashSet<StrangerKind>();
            if (map == null) return set;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                var k = KindOf(p);
                if (k != StrangerKind.None) set.Add(k);
            }
            return set;
        }

        public static bool IsMajestic(Pawn x) =>
            x?.RaceProps != null && x.RaceProps.Animal && (x.RaceProps.baseBodySize >= 2.5f || x.GetStatValue(StatDefOf.Wildness) >= 0.95f);

        public static bool OfColonist(Pawn animal, Pawn a) =>
            animal != null && a != null && (animal.playerSettings?.Master == a || (animal.relations?.DirectRelationExists(PawnRelationDefOf.Bond, a) ?? false));

        public static bool Fits(StrangerRole r, Pawn x, Pawn a)
        {
            if (r == null || x == null || x == a) return false;
            if (KindOf(x) != r.kind) return false;
            var nemeses = CouncilGameComponent.Instance?.nemeses;
            if (r.nemesis && !(nemeses?.IsNemesis(x) ?? false)) return false;
            if (r.nemesisOfA && (nemeses?.For(x) is Nemesis n ? !n.announced || n.victim != a : true)) return false;
            if (r.maxDistance > 0f && (a == null || !a.Spawned || a.Map != x.Map || x.Position.DistanceTo(a.Position) > r.maxDistance)) return false;
            if (IsHuman(r.kind))
            {
                if (r.child && !x.DevelopmentalStage.Juvenile()) return false;
                if (r.adult && x.DevelopmentalStage.Juvenile()) return false;
                if (r.royal && x.royalty?.MostSeniorTitle == null) return false;
                if (!r.anyTrait.NullOrEmpty() && (x.story?.traits == null || !r.anyTrait.Any(x.story.traits.HasTrait))) return false;
                if (a != null && x.relations != null && a.relations != null)
                {
                    int of = x.relations.OpinionOf(a), from = a.relations.OpinionOf(x);
                    if (of < r.minOpinionOfA || of > r.maxOpinionOfA || from < r.minOpinionFromA || from > r.maxOpinionFromA) return false;
                }
                if (x.Faction != null && !x.Faction.IsPlayer && x.Faction.HasGoodwill)
                {
                    int g = x.Faction.PlayerGoodwill;
                    if (g < r.minGoodwill || g > r.maxGoodwill) return false;
                }
                return true;
            }
            if (r.predator && !x.RaceProps.predator) return false;
            if (r.notPredator && x.RaceProps.predator) return false;
            float size = x.RaceProps.baseBodySize;
            if (size < r.minBodySize || size > r.maxBodySize) return false;
            if (r.majestic && !IsMajestic(x)) return false;
            if (r.ofA && !OfColonist(x, a)) return false;
            if (r.bondedToA && !(x.relations?.DirectRelationExists(PawnRelationDefOf.Bond, a) ?? false)) return false;
            if (r.notOfA && OfColonist(x, a)) return false;
            if (r.named && x.Name == null) return false;
            return true;
        }

        /// <summary>A stranger fitting the role, the nearer to A the likelier.</summary>
        public static Pawn Pick(Map map, StrangerRole r, Pawn a, List<Pawn> pool = null)
        {
            var options = (pool ?? On(map, r.kind)).Where(x => Fits(r, x, a)).ToList();
            if (options.Count == 0) return null;
            if (a == null || !a.Spawned) return options.RandomElement();
            return options.RandomElementByWeight(x => 1f / (1f + x.Position.DistanceTo(a.Position) / 25f));
        }

        /// <summary>
        /// The faction to deal with over a stranger: theirs, unless it's ours or there is none. Quest guests belong to the
        /// colony while they stay; their own people are their "extra home faction".
        /// </summary>
        public static Faction FactionOf(Pawn x)
        {
            if (x?.RaceProps == null || !x.RaceProps.Humanlike) return null;
            var home = x.IsQuestLodger() ? QuestUtility.GetExtraHomeFaction(x, null) : null;
            var f = home ?? x.Faction;
            return f != null && !f.IsPlayer ? f : null;
        }

        /// <summary>A guest of the Hospitality mod: its own visit (and its own way of recruiting) applies.</summary>
        public static bool IsHospitalityGuest(Pawn x) => x?.GetLord()?.LordJob?.GetType().Namespace == "Hospitality";
    }

    /// <summary>
    /// A dilemma about a stranger (X): a visitor who wants to stay, a prisoner's plea, a trader eyeing an heirloom.
    /// A (and B, if given) are colonists; {X_...} and {FACTION} (X's) in the texts.
    /// </summary>
    public class DilemmaWorker_Stranger : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (def.stranger == null || !ColonyCouncilMod.Settings.strangers || !CanHappenNow(map)) return false;
            var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            if (pawns.Count < def.minColonists) return false;
            var strangers = Strangers.On(map, def.stranger.kind).ToList();
            if (strangers.Count == 0) return false;
            var aOptions = pawns.Where(p => def.roleA.Allows(p, null)).ToList();
            if (def.roleA.bestAt != null) aOptions = aOptions.OrderByDescending(p => p.skills?.GetSkill(def.roleA.bestAt)?.Level ?? 0).Take(1).ToList();
            foreach (var a in aOptions.InRandomOrder())
            {
                var x = Strangers.Pick(map, def.stranger, a, strangers);
                if (x == null) continue;
                Pawn b = null;
                if (def.roleB != null)
                {
                    b = pawns.Where(p => p != x && def.roleB.Allows(p, a)).RandomElementWithFallback();
                    if (b == null) continue;
                }
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b, stranger = x, faction = Strangers.FactionOf(x), count = def.thingCount };
                if (def.petOfA)
                {
                    ctx.pet = map.mapPawns.SpawnedColonyAnimals.Where(p => !p.Dead && !p.Downed && Strangers.OfColonist(p, a)).RandomElementWithFallback();
                    if (ctx.pet == null) continue;
                }
                if (!Extra(ctx)) continue;
                return true;
            }
            ctx = null;
            return false;
        }
    }
}
