using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Murmur and the fishers (C7 of docs/content-update-proposals.md). A fisher who holds to Murmur, Old Croak (at least
    /// PawnGodsMath.HoldsFrom, never one in dread of him), gives the first fish of the morning back to the water: one fish of
    /// the catch goes back with a splash at the spot, and someone near who sees it may say so. That day the fishing goes
    /// better for them (a day's hediff, Rimfolk_MurmurFishBiting: Odyssey's fishing yield a tenth up, and Vanilla Fishing
    /// Expanded's own fishing luck a little up where it is loaded). It is their own small rite (belief, PawnGodsActs.RiteKept);
    /// the colony's book keeps it once in a while (GodsAmongMath.BookGapDays). Also: Vanilla Fishing Expanded's fisherman
    /// trait draws its holder toward Murmur (PawnGodsMath.TraitDraws), and what the water gives back
    /// (OwnMurmur.GiveBack) may leave a few fish of the mod's on the mud, else the game's own. No harm of its own: the cold
    /// water's slip already counts fishing.
    /// </summary>
    public static class MurmurFishers
    {
        public const string Pool = "Rimfolk_Pool_GodsAmong_Fisher";

        public static HediffDef Biting => DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_MurmurFishBiting");

        /// <summary>Vanilla Fishing Expanded's fish (by name), else the game's own common ones (Odyssey).</summary>
        private static readonly string[] ModFish =
        {
            "VCEF_RawTrout", "VCEF_RawPerch", "VCEF_RawHerring", "VCEF_RawMackerel", "VCEF_RawEel", "VCEF_RawMinnow", "VCEF_RawSprat", "VCEF_RawHaddock"
        };

        private static readonly string[] GameFish = { "Fish_Bass", "Fish_Catfish", "Fish_Cod", "Fish_Flounder", "Fish_Salmon", "Fish_Tilapia" };

        /// <summary>The last day each fisher gave a fish back (a load forgets it; the day's hediff keeps a second from the same day).</summary>
        private static readonly Dictionary<int, int> gaveOn = new Dictionary<int, int>();

        private static int lastBook = -999999;

        /// <summary>The catch is made (FishingUtility.GetCatchesFor): a fisher who holds to Murmur gives the first fish of the morning back.</summary>
        internal static void Caught(Pawn p, IntVec3 cell, List<Thing> catches)
        {
            if (!Gods.On || !Worship.On || p == null || catches == null || catches.Count == 0 || !p.Spawned || p.Faction != Faction.OfPlayer || !p.IsColonist) return;
            var map = p.Map;
            var g = OwnMurmur.God;
            if (g == null || !Homes.IsHome(map) || !GodsAmongMath.Morning(GenLocalDate.HourOfDay(map))) return;
            if (PawnGods.HoldOf(p, g) < PawnGodsMath.HoldsFrom || PawnGods.Dreads(p, g) || Worship.Scoffer(p)) return;
            int today = GenDate.DaysPassed;
            var biting = Biting;
            if (gaveOn.TryGetValue(p.thingIDNumber, out int day) && day == today || biting != null && p.health.hediffSet.HasHediff(biting)) return;
            var fish = catches.FirstOrDefault(t => t != null && t.def.IsIngestible && t.stackCount > 0);
            if (fish == null) return;
            if (gaveOn.Count > 200) gaveOn.Clear();
            gaveOn[p.thingIDNumber] = today;
            string label = fish.def.label;
            fish.stackCount = GodsAmongMath.AfterGiving(fish.stackCount);
            if (fish.stackCount <= 0) catches.Remove(fish);
            // The splash at the spot, seen by anyone looking.
            var at = cell.IsValid && cell.InBounds(map) ? cell : p.Position;
            FleckMaker.WaterSplash(at.ToVector3Shifted(), map, 1.2f, 4f);
            if (biting != null) p.health.AddHediff(biting);
            PawnGodsActs.RiteKept(p, g);
            int now = Find.TickManager.TicksGame;
            if (Interplay.Ready("murmur-fisher-word|" + p.thingIDNumber, GodsAmongMath.WordGapDays))
                Worship.Say(p, Pool, g, null, "giveback");
            // Someone near who saw it splash.
            var seer = map.mapPawns.FreeColonistsSpawned.Where(x => x != p && GodOmens.Awake(x) && x.Position.InHorDistOf(at, GodsAmongMath.SplashSeen)
                                                                       && GenSight.LineOfSight(x.Position, at, map, true)).OrderBy(x => x.Position.DistanceToSquared(at)).FirstOrDefault();
            if (seer != null && Interplay.Ready("murmur-fisher-seen|" + Homes.KeyOf(map), GodsAmongMath.WordGapDays))
                Worship.Say(seer, Pool, g, Together.Can(seer, p) ? p : null, PawnGods.HoldOf(seer, g) >= PawnGodsMath.LeansFrom ? "seen" : "seenother");
            if (now - lastBook < GodsAmongMath.BookGapDays * GenDate.TicksPerDay && now >= lastBook) return;
            lastBook = now;
            var who = new List<Pawn> { p };
            if (seer != null) who.Add(seer);
            // They know it was their own gift to Murmur: written under his name, as theirs.
            GodLore.Saw(who, g, "CC_GA_Fisher_Book".Translate(p.Named("PAWN"), label.Named("FISH")).Resolve());
            CouncilGameComponent.Instance?.journals.WriteText(p, "CC_GA_Fisher_Journal".Translate(p.Named("PAWN"), Worship.Called(g, p).Named("GOD")).Resolve());
        }

        /// <summary>What the water gives back (OwnMurmur.GiveBack) may leave a few fish on the mud: the mods' first, else the game's own; none without either.</summary>
        public static Thing MudFish()
        {
            if (!Rand.Chance(GodsAmongMath.MudFishChance)) return null;
            var defs = ModFish.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();
            if (defs.Count == 0) defs = GameFish.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();
            var def = defs.RandomElementWithFallback();
            if (def == null) return null;
            try
            {
                var t = ThingMaker.MakeThing(def);
                t.stackCount = Rand.RangeInclusive(GodsAmongMath.MudFishMin, GodsAmongMath.MudFishMax);
                return t;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: fish on the mud", e);
                return null;
            }
        }

        /// <summary>Dev: someone gives a fish back now, as if their morning's first catch (a fish of the game's made for it).</summary>
        public static string DevGiveBack(Pawn p)
        {
            var g = OwnMurmur.God;
            if (g == null) return "no Murmur in this pantheon.";
            if (PawnGods.HoldOf(p, g) < PawnGodsMath.HoldsFrom) PawnGods.DevSet(p, g, PawnGodsMath.HoldsFrom + 5f, 0f);
            gaveOn.Remove(p.thingIDNumber);
            var b = Biting;
            var h = b == null ? null : p.health.hediffSet.GetFirstHediffOfDef(b);
            if (h != null) p.health.RemoveHediff(h);
            var def = GameFish.Concat(ModFish).Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null) ?? ThingDefOf.Meat_Human;
            var catches = new List<Thing> { ThingMaker.MakeThing(def) };
            catches[0].stackCount = 4;
            var water = CellFinder.RandomClosewalkCellNear(p.Position, p.Map, 6);
            lastBook = -999999;
            bool was = GodsAmongMath.Morning(GenLocalDate.HourOfDay(p.Map));
            if (!was) return "it is not morning at the colony (4 to 12 o'clock).";
            Caught(p, water, catches);
            return p.LabelShort + " gave one back; " + catches.Sum(t => t.stackCount) + " left of 4.";
        }
    }

    /// <summary>
    /// The game's fishing makes its catch (Odyssey's FishingUtility.GetCatchesFor): a fisher who holds to Murmur gives the
    /// first fish of the morning back to the water (Gods/Among/MurmurFishers.cs). Off with the deeper game hooks.
    /// </summary>
    [HarmonyPatch(typeof(FishingUtility), nameof(FishingUtility.GetCatchesFor))]
    public static class Patch_MurmurFisherCatch
    {
        public static void Postfix(Pawn pawn, IntVec3 cell, bool animalFishing, List<Thing> __result)
        {
            if (!RimfolkPatches.On || animalFishing || __result == null || __result.Count == 0) return;
            try
            {
                MurmurFishers.Caught(pawn, cell, __result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: a fisher's catch", e);
            }
        }
    }
}
