using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// What an errand takes from the stores (DilemmaEffect_CarryOut): the same things an answer's own taking would take
    /// (DilemmaEffect_Consume and the world events' medicine and silver), in the same order: the plainest medicine first,
    /// raw food in storage, silver and other things wherever they lie in the colony, never anything forbidden. A coat is
    /// the plainest outer wear in storage. Several kinds joined by '+' ("meals+coat") are one of each, carried in turn by
    /// the same carrier, so one errand hands them all over: its count is how many kinds there are.
    /// </summary>
    public static class PlayCarry
    {
        public const string Coat = "coat";

        /// <summary>
        /// Scrap the Wheel's runners buy (Peoples/PeoplesD2.cs): slag lying in the colony's stores or on its home ground, and
        /// clothes and weapons in the stores too far gone to wear (PeoplesDMath.Broken), the slag first.
        /// </summary>
        public const string Scrap = "scrap";

        public static bool IsScrap(Thing t, Map map)
        {
            if (t == null || !t.Spawned || t.IsForbidden(Faction.OfPlayer) || t.Map != map) return false;
            if (t.def == ThingDefOf.ChunkSlagSteel) return t.IsInAnyStorage() || map.areaManager.Home[t.Position];
            if (!(t.def.IsApparel || t.def.IsWeapon) || !t.def.useHitPoints || t.MaxHitPoints <= 0 || !t.IsInAnyStorage()) return false;
            var code = t.TryGetComp<CompBiocodable>();
            if (code != null && code.Biocoded) return false;
            return PeoplesDMath.Broken((float)t.HitPoints / t.MaxHitPoints, t is Apparel a && a.WornByCorpse);
        }

        /// <summary>
        /// Salvage the Hollow Fleet buys (Peoples/PeoplesF2.cs): persona cores and advanced components in the colony's
        /// stores, the cores first.
        /// </summary>
        public const string Salvage = "salvage";

        public static bool IsSalvage(Thing t) =>
            t != null && t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage() && (t.def == ThingDefOf.AIPersonaCore || t.def == ThingDefOf.ComponentSpacer);

        /// <summary>
        /// The mech parts the Lattice's part-trader buys (Peoples/PeoplesG.cs): Biotech's subcores and mech chips in the
        /// colony's stores, the dearest first.
        /// </summary>
        public const string MechParts = "mechparts";

        public static readonly string[] MechPartDefs = { "SubcoreHigh", "SubcoreRegular", "SubcoreBasic", "NanostructuringChip", "PowerfocusChip", "SignalChip" };

        public static bool IsMechPart(Thing t) =>
            t != null && t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage() && System.Array.IndexOf(MechPartDefs, t.def.defName) >= 0;

        /// <summary>
        /// Art the Choir of the Spire takes for its goods (Peoples/PeoplesG2.cs): sculptures packed up and ready to carry, as
        /// a mission's art is (Missions.Matching), the dearest first.
        /// </summary>
        public const string Art = "art";

        /// <summary>The kinds of an errand that carries one of each ("meals+coat"), in the order they go; null for one kind.</summary>
        public static string[] Parts(string what) => what != null && what.IndexOf('+') >= 0 ? what.Split('+') : null;

        /// <summary>The thing an errand's word names: silver, wood, a defName; the event's own thing when empty; none for a kind (raw food, medicine, meals, a coat).</summary>
        public static ThingDef DefOf(string what, ThingDef fallback)
        {
            if (Parts(what) != null) return null;
            switch (what)
            {
                case "silver": return ThingDefOf.Silver;
                case "wood": return ThingDefOf.WoodLog;
                case "rawfood":
                case "medicine":
                case "meals":
                case Coat:
                case Scrap:
                case Salvage:
                case MechParts:
                case Art:
                    return null;
            }
            return what.NullOrEmpty() ? fallback : DefDatabase<ThingDef>.GetNamedSilentFail(what);
        }

        /// <summary>
        /// A coat that can be given away: outer wear for the body (a parka, a duster, a cloak) in storage, not forbidden,
        /// not biocoded to someone.
        /// </summary>
        public static bool IsCoat(Thing t)
        {
            var ap = t?.def?.apparel;
            if (ap?.layers == null || ap.bodyPartGroups == null || !ap.layers.Contains(ApparelLayerDefOf.Shell) || !ap.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)) return false;
            if (!t.Spawned || t.IsForbidden(Faction.OfPlayer) || !t.IsInAnyStorage()) return false;
            var code = t.TryGetComp<CompBiocodable>();
            return code == null || !code.Biocoded;
        }

        /// <summary>Raw food people can eat, sitting in storage: meat, vegetables, berries.</summary>
        public static bool IsRawFood(Thing t)
        {
            var ing = t?.def?.ingestible;
            if (ing == null || !t.def.IsNutritionGivingIngestible || t.def.IsCorpse || t.def.IsDrug || !ing.HumanEdible) return false;
            if (ing.preferability != FoodPreferability.RawBad && ing.preferability != FoodPreferability.RawTasty) return false;
            return t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage();
        }

        /// <summary>Food few would thank you for (human meat, insect meat, or meals made with them): handed over last.</summary>
        private static int Unwelcome(Thing t)
        {
            try
            {
                if (FoodUtility.IsHumanlikeCorpseOrHumanlikeMeatOrIngredient(t)) return 2;
                if (FoodUtility.IsInsectCorpseOrInsectMeatIngredient(t)) return 1;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("errand food", e);
            }
            return 0;
        }

        /// <summary>What there is to take, in the order it's taken.</summary>
        public static IEnumerable<Thing> Stock(Map map, string what, ThingDef def)
        {
            if (map == null) return Enumerable.Empty<Thing>();
            var parts = Parts(what);
            if (parts != null) return parts.SelectMany(part => Stock(map, part, null));
            switch (what)
            {
                // Food carried out to someone else: what nobody would mind being given first, human and insect meat last.
                case "rawfood":
                    return map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Where(IsRawFood).OrderBy(Unwelcome);
                case "medicine": return DilemmaEffect_Consume.Medicine(map);
                case "meals": return PlaySpots.Meals(map).OrderBy(Unwelcome);
                // The plainest first: one nobody died in before one somebody did, then the cheapest.
                case Coat:
                    return map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel).Where(IsCoat)
                        .OrderBy(t => t is Apparel a && a.WornByCorpse ? 1 : 0).ThenBy(t => t.MarketValue);
                case Scrap:
                    return map.listerThings.ThingsOfDef(ThingDefOf.ChunkSlagSteel).Concat(map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
                        .Concat(map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon)).Where(t => IsScrap(t, map)).Distinct()
                        .OrderBy(t => t.def == ThingDefOf.ChunkSlagSteel ? 0 : 1);
                case Salvage:
                    return map.listerThings.ThingsOfDef(ThingDefOf.AIPersonaCore).Concat(map.listerThings.ThingsOfDef(ThingDefOf.ComponentSpacer)).Where(IsSalvage);
                case MechParts:
                    return MechPartDefs.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).SelectMany(d => map.listerThings.ThingsOfDef(d)).Where(IsMechPart);
                case Art:
                    return Missions.Matching(map, "art").OrderByDescending(t => t.MarketValue);
            }
            def = def ?? DefOf(what, null);
            if (def == null) return Enumerable.Empty<Thing>();
            return map.listerThings.ThingsOfDef(def).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer));
        }

        /// <summary>How many there are to take; for several kinds, how many of the kinds the stores hold any of.</summary>
        public static int Have(Map map, string what, ThingDef def)
        {
            var parts = Parts(what);
            if (parts != null) return parts.Count(part => Stock(map, part, null).Any());
            return Stock(map, what, def).Sum(t => t.stackCount);
        }

        /// <summary>
        /// Takes so many from the stores (as many as there are, if fewer). How many were taken. Never what someone else is
        /// about to use (an ingredient a cook has claimed, the medicine a doctor is fetching, a stack being hauled);
        /// <paramref name="carrier"/>'s own claim (the rest of the stack they took their load from) doesn't count.
        /// </summary>
        public static int Take(Map map, string what, ThingDef def, int n, Pawn carrier = null)
        {
            if (map == null || n <= 0) return 0;
            // Several kinds: one of each still to go, the last ones (they go in order).
            var parts = Parts(what);
            if (parts != null) return parts.Skip(Mathf.Max(0, parts.Length - n)).Sum(part => Take(map, part, null, 1, carrier));
            int left = n;
            foreach (var t in Stock(map, what, def).ToList())
            {
                if (left <= 0) break;
                if (t == null || t.Destroyed || !t.Spawned) continue;
                if (Claimed(t, map, carrier)) continue;
                int take = Mathf.Min(left, t.stackCount);
                t.SplitOff(take).Destroy();
                left -= take;
            }
            return n - left;
        }

        private static readonly HashSet<Pawn> claimants = new HashSet<Pawn>();

        /// <summary>Reserved for someone's job, or being picked up, by anyone but <paramref name="except"/>.</summary>
        private static bool Claimed(Thing t, Map map, Pawn except)
        {
            claimants.Clear();
            map.reservationManager.ReserversOf(t, claimants);
            claimants.Remove(except);
            bool claimed = claimants.Count > 0;
            claimants.Clear();
            if (claimed) return true;
            map.physicalInteractionReservationManager.ReserversOf(t, claimants);
            claimants.Remove(except);
            claimed = claimants.Count > 0;
            claimants.Clear();
            return claimed;
        }

        /// <summary>The stack the carrier picks up: the first in the order things are taken that they may and can get to.</summary>
        public static Thing StackFor(PlayOut p, Pawn carrier)
        {
            if (p?.map == null || carrier == null) return null;
            // Several kinds: the next one still to go (one of each, so the tally says how far along it is).
            var parts = Parts(p.what);
            var stock = parts != null ? Stock(p.map, parts[Mathf.Clamp(p.tally, 0, parts.Length - 1)], null) : Stock(p.map, p.what, p.thing);
            foreach (var t in stock)
            {
                if (t == null || !t.Spawned || t.IsForbidden(carrier)) continue;
                if (!carrier.CanReserve(t) || !carrier.CanReach(t, PathEndMode.ClosestTouch, Danger.Some)) continue;
                return t;
            }
            return null;
        }

        public static string Label(string what, ThingDef def)
        {
            var parts = Parts(what);
            if (parts != null) return TextUtil.JoinAnd(parts.Select(part => Label(part, null)).ToList());
            switch (what)
            {
                case "rawfood": return "CC_Dilemma_RawFood".Translate();
                case "medicine": return "CC_Dilemma_Medicine".Translate();
                case "meals": return "CC_Play_Meals".Translate();
                case Coat: return "CC_Play_Coat".Translate();
                case Scrap: return "CC_PeoplesD_Scrap".Translate();
                case Salvage: return "CC_PeoplesF_Salvage".Translate();
                case MechParts: return "CC_PeoplesG_MechParts".Translate();
                case Art: return "CC_PeoplesG_Art".Translate();
            }
            return (def ?? DefOf(what, null))?.label ?? what ?? "";
        }

        /// <summary>
        /// An errand that never went (or the setting off meanwhile): what it was to carry leaves the stores now, less
        /// what was already handed over.
        /// </summary>
        public static void Settle(PlayOut p)
        {
            if (p == null || p.kind != PlayKind.Carry || p.map == null || !Find.Maps.Contains(p.map)) return;
            int left = p.amount - p.tally;
            if (left > 0) Take(p.map, p.what, p.thing, left);
            p.tally = p.amount;
        }
    }

    /// <summary>The hunt's game and hunters: only safe game, only hunters the game itself would send.</summary>
    public static class PlayHunt
    {
        private static WorkGiverDef giver;
        private static bool looked;

        /// <summary>
        /// Wild game that won't turn on its hunters: no predator, no insect or dryad, nothing likely to go manhunter (by its
        /// kind, whatever the difficulty makes of it), nothing the size of a thrumbo or an elephant, nobody's animal, no
        /// animal bonded to someone, and none the colony's faith venerates.
        /// </summary>
        public static bool Safe(Pawn a, Map map) =>
            a != null && !a.Dead && a.Spawned && a.Map == map && !a.Downed && a.RaceProps != null && a.RaceProps.Animal && a.Faction == null
            && !a.RaceProps.predator && !a.RaceProps.Insect && !a.RaceProps.Dryad && a.RaceProps.baseBodySize <= MaxGameSize
            && !a.InMentalState && !a.Position.Fogged(map)
            && a.RaceProps.manhunterOnDamageChance <= 0.1f && PawnUtility.GetManhunterOnDamageChance(a) <= 0.1f
            && !Bonded(a) && !Venerated(a);

        /// <summary>The biggest kind of animal a hunt goes after (a muffalo or an elk, not a thrumbo, a rhino or an elephant).</summary>
        public const float MaxGameSize = 2.5f;

        /// <summary>Bonded to someone, or venerated by the colony's faith: never game.</summary>
        public static bool Kept(Pawn a) => Bonded(a) || Venerated(a);

        /// <summary>
        /// Still game for a hunt under way: safe game, or the head that can turn the hunt marked (Harm/HuntsBite.cs) while
        /// it's up and about and hasn't turned.
        /// </summary>
        public static bool Huntable(Pawn a, Map map) =>
            Safe(a, map) || HuntsBite.IsTurning(a) && a.Spawned && a.Map == map && !a.Dead && !a.Downed && !a.InMentalState && a.Faction == null;

        private static bool Bonded(Pawn a)
        {
            var rels = a.relations?.DirectRelations;
            if (rels == null) return false;
            for (int i = 0; i < rels.Count; i++)
                if (rels[i].def == PawnRelationDefOf.Bond) return true;
            return false;
        }

        private static bool Venerated(Pawn a)
        {
            if (!ModsConfig.IdeologyActive) return false;
            var ideos = Faction.OfPlayer?.ideos;
            if (ideos == null) return false;
            foreach (var ideo in ideos.AllIdeos)
                if (ideo != null && ideo.IsVeneratedAnimal(a))
                    return true;
            return false;
        }

        /// <summary>
        /// The game for a hunt: the event's own animal if it is one (and safe), else the kinds wanted first, else the best
        /// eating; never an animal marked for something else. With <paramref name="markedOnly"/>, only game already marked.
        /// </summary>
        public static List<Pawn> ChooseGame(Map map, Pawn x, string kinds, int n, bool markedOnly) => ChooseGame(map, x, kinds, n, markedOnly, false, out _);

        /// <summary>
        /// The same, and with <paramref name="mayTurn"/> (Harm/HuntsBite.cs) at most one head of game that can turn among
        /// it (<paramref name="turning"/>), never for game already marked by someone else.
        /// </summary>
        public static List<Pawn> ChooseGame(Map map, Pawn x, string kinds, int n, bool markedOnly, bool mayTurn, out Pawn turning)
        {
            turning = null;
            if (mayTurn && !markedOnly && map != null && !(x != null && x.RaceProps != null && x.RaceProps.Animal))
            {
                var dm0 = map.designationManager;
                var wanted0 = kinds.NullOrEmpty() ? null : kinds.Split('|').Select(k => k.Trim()).Where(k => k.Length > 0).ToList();
                // Safe game already marked for hunting may join it; a head that can turn only if nobody has marked it yet.
                var ordered = map.mapPawns.AllPawnsSpawned.ToList()
                    .Where(a => Safe(a, map) && (dm0.DesignationOn(a) == null || dm0.DesignationOn(a, DesignationDefOf.Hunt) != null)
                                || dm0.DesignationOn(a) == null && HuntsBite.MayTurn(a, map))
                    .OrderByDescending(a => wanted0 != null && wanted0.Contains(a.def.defName))
                    .ThenByDescending(a => a.GetStatValue(StatDefOf.MeatAmount));
                return HuntsBite.Pick(ordered, map, n, true, out turning);
            }
            var result = new List<Pawn>();
            if (map == null) return result;
            var dm = map.designationManager;
            bool Fits(Pawn a) => Safe(a, map) && (markedOnly ? dm.DesignationOn(a, DesignationDefOf.Hunt) != null
                                                             : dm.DesignationOn(a) == null || dm.DesignationOn(a, DesignationDefOf.Hunt) != null);
            if (x != null && x.RaceProps != null && x.RaceProps.Animal)
            {
                if (Fits(x)) result.Add(x);
                return result;
            }
            var wanted = kinds.NullOrEmpty() ? null : kinds.Split('|').Select(k => k.Trim()).Where(k => k.Length > 0).ToList();
            result.AddRange(map.mapPawns.AllPawnsSpawned.ToList().Where(Fits)
                .OrderByDescending(a => wanted != null && wanted.Contains(a.def.defName))
                .ThenByDescending(a => a.GetStatValue(StatDefOf.MeatAmount))
                .Take(n));
            return result;
        }

        /// <summary>
        /// Someone the game would send hunting: able to, with a hunting weapon, no brawler; and nobody whose beliefs
        /// hold violence abhorrent or killing animals wrong (Wishes/Beliefs.cs).
        /// </summary>
        public static bool CanHunt(Pawn x) =>
            x?.workSettings != null && !x.WorkTypeIsDisabled(WorkTypeDefOf.Hunting) && WorkGiver_HunterHunt.HasHuntingWeapon(x)
            && !(x.story?.traits?.HasTrait(TraitDefOf.Brawler) ?? false)
            && !Beliefs.Forbids(x, Belief.NoHunting) && !Beliefs.Forbids(x, Belief.NoViolence);

        private static WorkGiverDef Giver
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    giver = DefDatabase<WorkGiverDef>.AllDefsListForReading.FirstOrDefault(w => w.giverClass == typeof(WorkGiver_HunterHunt));
                }
                return giver;
            }
        }

        /// <summary>The game's own hunting job for the nearest of the hunt's game this hunter may go after, or null.</summary>
        public static Job JobFor(PlayOut p, Pawn hunter)
        {
            var def = Giver;
            if (def == null || !(def.Worker is WorkGiver_Scanner sc) || p?.map == null) return null;
            if (sc.ShouldSkip(hunter, false)) return null;
            var dm = p.map.designationManager;
            foreach (var a in p.game.Where(a => Huntable(a, p.map) && dm.DesignationOn(a, DesignationDefOf.Hunt) != null)
                         .OrderBy(a => a.Position.DistanceToSquared(hunter.Position)))
            {
                if (!sc.HasJobOnThing(hunter, a, false) || !hunter.CanReach(a, sc.PathEndMode, sc.MaxPathDanger(hunter))) continue;
                var job = sc.JobOnThing(hunter, a, false);
                if (job == null) continue;
                job.workGiverDef = def;
                return job;
            }
            return null;
        }

        /// <summary>Still on the hunt for this one's game.</summary>
        public static bool OnIt(PlayOut p, Pawn hunter)
        {
            var job = hunter?.CurJob;
            return job != null && job.def == JobDefOf.Hunt && job.targetA.Thing is Pawn a && p.game.Contains(a);
        }
    }
}
