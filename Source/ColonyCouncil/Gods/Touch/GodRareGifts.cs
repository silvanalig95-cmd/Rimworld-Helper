using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The gods' rarest gifts (C10 of docs/content-update-proposals.md; the player's go of 20 November 2026). After one of a
    /// god's great effects (GodTouch.Follow), now and then and once in two colony-years at most for the whole colony, a named
    /// weapon of Possessed Weapons lies where it happened, in a ring of scorched ground, matched to the god (the glacial
    /// cleaver for Hush, the lightning javelin for Cirrus, the volcanic maul or the craghammer for Smoulder, the verdant bow for
    /// Hollin, the gilded arquebus for Ducat, the shrouded dagger for Tamiu, the sanctified sword for Helios, the automaton
    /// crossbow for Pip). After Smoulder's waking (GodWaking.Wake), a warm drake's egg of Dragons Descent lies among the fallen
    /// rock of his deep, and hatches as the colony's. The words say what lay there, never whose; the colony's book keeps it as
    /// those who saw it saw it. The weapons are the mod's persona weapons with the game's own bonds and traits; Rimfolk adds
    /// no harm. None without the mod.
    /// </summary>
    public static class GodRareGifts
    {
        private const string Key = "rare";

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>A rare gift may be left now (its wait past, at its chance), the dev tools push it.</summary>
        internal static bool forced;

        private static bool Due(GodTouchBook b, float chance) => b != null && (forced || GodRareGiftsMath.Due(b.DaysSince(Key, Now), Rand.Value, chance));

        /// <summary>One of the god's weapons the player's mods have, or null.</summary>
        public static ThingDef WeaponOf(God g) =>
            GodRareGiftsMath.Of(g?.Key).Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d => d != null).RandomElementWithFallback();

        /// <summary>After a great effect at a place (GodTouch.Follow): the god's weapon may be left there.</summary>
        internal static bool AfterGreat(GodTouchBook b, TouchContext c)
        {
            if (c == null || c.map == null || c.dark) return false;
            var def = WeaponOf(c.god);
            if (def == null || !Due(b, GodRareGiftsMath.RareChance)) return false;
            var at = Spot(c.map, c.cell);
            if (!at.IsValid) return false;
            Thing weapon;
            try
            {
                weapon = ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Masterwork, ArtGenerationContext.Outsider);
                if (!GenPlace.TryPlaceThing(weapon, at, c.map, ThingPlaceMode.Near, out var placed)) return false;
                weapon = placed;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a rare gift left", e);
                return false;
            }
            Scorch(c.map, weapon.Position);
            b.Set(Key, Now);
            string words = TextVariants.Vary("CC_GodRare_Weapon").Translate(weapon.LabelNoCount.Named("WEAPON"), Homes.Name(c.map).Named("HOME")).Resolve();
            GodPresence.Look(new LookTargets(weapon));
            Tell.FromGod(NewsKind.Land, c.map, words, new LookTargets(weapon));
            GodTouch.Write(c, GodActKind.Mark, words);
            return true;
        }

        /// <summary>After Smoulder's waking: a warm drake's egg among the fallen rock of his deep, which hatches as the colony's.</summary>
        internal static bool AfterWaking(God g, Map map, IntVec3 at)
        {
            var b = GodTouch.Book;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(GodRareGiftsMath.DrakeEgg);
            if (g?.Key != OwnSmoulder.Key || map == null || def == null || !Due(b, GodRareGiftsMath.EggChance)) return false;
            var spot = Spot(map, at);
            if (!spot.IsValid) return false;
            Thing egg;
            try
            {
                egg = ThingMaker.MakeThing(def);
                var hatcher = egg.TryGetComp<CompHatcher>();
                if (hatcher != null) hatcher.hatcheeFaction = Faction.OfPlayer;
                if (!GenPlace.TryPlaceThing(egg, spot, map, ThingPlaceMode.Near, out var placed)) return false;
                egg = placed;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a drake's egg left", e);
                return false;
            }
            FleckMaker.ThrowHeatGlow(egg.Position, map, 1.8f);
            b.Set(Key, Now);
            string words = TextVariants.Vary("CC_GodRare_Egg").Translate(Homes.Name(map).Named("HOME")).Resolve();
            Tell.FromGod(NewsKind.Land, map, words, new LookTargets(egg));
            Gods.Remember(new GodAct { god = g.id, home = Gods.HomeKey(map), tick = Now, kind = GodActKind.Mark, cell = egg.Position, text = words, what = "mark" });
            return true;
        }

        /// <summary>Dev: a god's rare gift left now by the first of the colony (the god's weapon, past its wait), else why not.</summary>
        public static string DevWeapon(Map map, string godKey)
        {
            var g = Gods.ByKey(godKey);
            var p = map?.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (g == null || p == null) return "no such god, or nobody of the colony here.";
            if (WeaponOf(g) == null) return "no weapon of " + godKey + "'s loaded (Possessed Weapons).";
            forced = true;
            try
            {
                return AfterGreat(GodTouch.Book, new TouchContext(g, map, p.Position, new System.Collections.Generic.List<Pawn> { p }, g, true, TouchTier.Great, false))
                    ? godKey + "'s weapon lies in a ring of scorched ground by " + p.LabelShort + "." : "it couldn't be left.";
            }
            finally
            {
                forced = false;
            }
        }

        /// <summary>Dev: a drake's egg in the deep now, as after Smoulder's waking.</summary>
        public static string DevEgg(Map map)
        {
            var g = Gods.ByKey(OwnSmoulder.Key);
            var p = map?.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (g == null || p == null) return "no Smoulder, or nobody of the colony here.";
            forced = true;
            try
            {
                return AfterWaking(g, map, p.Position) ? "a warm drake's egg lies by " + p.LabelShort + "." : "no Dragons Descent egg loaded, or nowhere to lay it.";
            }
            finally
            {
                forced = false;
            }
        }

        /// <summary>Open ground at or near the place, standable and clear.</summary>
        private static IntVec3 Spot(Map map, IntVec3 at)
        {
            if (map == null) return IntVec3.Invalid;
            if (!at.IsValid || !at.InBounds(map)) at = OwnHappenings.Middle(map);
            return CellFinder.RandomClosewalkCellNear(at, map, 4, c => c.Standable(map) && c.GetFirstItem(map) == null && c.GetFirstBuilding(map) == null);
        }

        /// <summary>A ring of scorched ground (ash) round where it lies, with a little smoke.</summary>
        private static void Scorch(Map map, IntVec3 at)
        {
            var ash = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_Ash");
            foreach (var c in GenRadial.RadialCellsAround(at, GodRareGiftsMath.RingTo, false).Where(c => c.InBounds(map) && !c.InHorDistOf(at, GodRareGiftsMath.RingFrom) && c.Standable(map)))
            {
                if (ash != null) FilthMaker.TryMakeFilth(c, map, ash, 2);
                if (Rand.Chance(0.3f)) FleckMaker.ThrowSmoke(c.ToVector3Shifted(), map, 0.6f);
            }
        }
    }
}
