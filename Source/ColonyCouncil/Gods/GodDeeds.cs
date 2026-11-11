using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The first favour sources, from what the game and Rimfolk already know (docs/design/gods.md: favour follows what the
    /// colony does), each small and local; stage 2 brings offerings, rites and vows:
    /// <list type="bullet">
    /// <item>A big harvest (the region's day: the colony's people brought in many plants and the raw food in store rose):
    /// the harvest's gods.</item>
    /// <item>A threat beaten off (the region's day, once the map is quiet again): the gods of war.</item>
    /// <item>The dead left lying (the region's day: four or more of the human dead a day or more on a home's ground): the
    /// gods of the dead displeased, a grieving one most.</item>
    /// <item>A great beast brought down by the colony (the great beast, a legend of the region, a pack's alpha, a beast
    /// nemesis): the gods of the hunt (Happenings' death hook).</item>
    /// <item>A sacred animal of a domain slaughtered, or hunted wild: that domain's gods displeased (the hunt's own gods
    /// don't mind a hunt).</item>
    /// <item>A funeral: a eulogy said over one of the colony's dead (Secrets/Vendettas.cs), more at a grave: the gods of the
    /// dead.</item>
    /// </list>
    /// Nothing here is saved: a load only notes the day's figures again (the first day after it gives nothing).
    /// </summary>
    public static class GodDeeds
    {
        /// <summary>Each colonist's plants harvested at the last day's look, and each home's raw food and last threat.</summary>
        private static readonly Dictionary<int, int> harvested = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> rawFood = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> threats = new Dictionary<int, int>();
        private static Game game;

        private static void Check()
        {
            if (Current.Game == game) return;
            game = Current.Game;
            harvested.Clear();
            rawFood.Clear();
            threats.Clear();
        }

        // ================= The region's day =================

        public static void Daily(GodBook b, int now)
        {
            Check();
            foreach (var map in Homes.All.ToList())
            {
                if (map == null) continue;
                Safe("the harvest", () => Harvest(map));
                Safe("a threat beaten off", () => ThreatBeaten(map, now));
                Safe("the dead left lying", () => DeadLying(map, now));
            }
        }

        private static void Safe(string what, Action a)
        {
            try
            {
                a();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: " + what, e);
            }
        }

        private static void Harvest(Map map)
        {
            int key = Homes.KeyOf(map);
            int plants = 0;
            foreach (var p in Homes.People(map))
            {
                if (p?.records == null) continue;
                int n = p.records.GetAsInt(RecordDefOf.PlantsHarvested);
                if (harvested.TryGetValue(p.thingIDNumber, out int before)) plants += Math.Max(0, n - before);
                harvested[p.thingIDNumber] = n;
            }
            int raw = ThingCategoryDefOf.PlantFoodRaw != null ? map.resourceCounter.GetCountIn(ThingCategoryDefOf.PlantFoodRaw) : 0;
            bool had = rawFood.TryGetValue(key, out int yesterday);
            rawFood[key] = raw;
            if (!had || !GodsMath.BigHarvest(plants, raw - yesterday)) return;
            Gods.Deed(GodDomain.Harvest, map, GodsMath.HarvestDeed, "CC_God_Deed_Harvest".Translate(Homes.Name(map).Named("HOME")).Resolve());
        }

        private static void ThreatBeaten(Map map, int now)
        {
            int key = Homes.KeyOf(map);
            int last = map.storyState?.LastThreatBigTick ?? -1;
            if (!threats.TryGetValue(key, out int noted))
            {
                threats[key] = last;
                return;
            }
            if (last <= 0 || last <= noted || now - last < GenDate.TicksPerDay / 2) return;
            if (GenHostility.AnyHostileActiveThreatToPlayer(map, false, false)) return;
            threats[key] = last;
            Gods.Deed(GodDomain.War, map, GodsMath.ThreatBeatenDeed, "CC_God_Deed_Threat".Translate(Homes.Name(map).Named("HOME")).Resolve());
        }

        private static void DeadLying(Map map, int now)
        {
            int lying = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (!(t is Corpse c) || !c.Spawned || c.InnerPawn?.RaceProps?.Humanlike != true) continue;
                if (c.timeOfDeath > 0 && now - c.timeOfDeath < GenDate.TicksPerDay) continue;
                lying++;
            }
            if (!GodsMath.DeadLeftLying(lying)) return;
            Gods.Deed(GodDomain.Dead, map, GodsMath.DeadLyingDeed, "CC_God_Deed_DeadLying".Translate(Homes.Name(map).Named("HOME"), lying.Named("COUNT")).Resolve(),
                FavourSource.Neglect, GodOffence.Unburied);
        }

        // ================= A death (Happenings' hook) =================

        private static HediffDef legend;

        /// <summary>An animal died (Happenings.Notify_Killed): a great beast brought down by the colony, or a sacred one slaughtered or hunted.</summary>
        public static void Killed(Pawn victim, DamageInfo? dinfo, bool greatBeast, Map map)
        {
            if (!Gods.On || victim?.RaceProps?.Animal != true || map == null || !Homes.IsHome(map)) return;
            try
            {
                var killer = dinfo?.Instigator;
                bool ours = killer != null && killer.Faction == Faction.OfPlayer;
                if (!ours) return;
                string who = killer is Pawn kp ? kp.LabelShort : Faction.OfPlayer.Name;
                legend ??= DefDatabase<HediffDef>.GetNamedSilentFail("Council_Legendary");
                bool great = greatBeast || legend != null && victim.health?.hediffSet?.HasHediff(legend) == true
                             || BeastBook.Instance?.alphas?.Any(a => a != null && a.alpha == victim) == true
                             || CouncilGameComponent.Instance?.nemeses?.IsNemesis(victim) == true;
                if (great)
                    Gods.Deed(GodDomain.Hunt, map, GodsMath.GreatBeastDeed, "CC_God_Deed_GreatBeast".Translate(victim.LabelShort.Named("BEAST"), who.Named("WHO")).Resolve());
                bool slaughtered = victim.Faction == Faction.OfPlayer || dinfo?.Def == DamageDefOf.ExecutionCut;
                bool wild = victim.Faction == null;
                foreach (var def in DefDatabase<GodDomainDef>.AllDefsListForReading)
                {
                    if (def?.Animals == null || !def.Animals.Contains(victim.def)) continue;
                    float by = GodsMath.SacredDeath(def.domain, slaughtered, wild);
                    if (by == 0f) continue;
                    string why = (slaughtered ? "CC_God_Deed_SacredSlaughtered" : "CC_God_Deed_SacredHunted")
                        .Translate(victim.def.label.Named("KIND"), def.ofWords.Named("WHAT"), who.Named("WHO")).Resolve();
                    Gods.Deed(def.domain, map, by, why, FavourSource.Sacrilege);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a death", e);
            }
        }

        // ================= A funeral (the eulogy) =================

        /// <summary>A eulogy said over one of the colony's dead (Secrets/Vendettas.cs): the gods of the dead pleased, more when the body lies in a grave.</summary>
        public static void Funeral(Pawn dead, Pawn speaker)
        {
            if (!Gods.On || dead == null || speaker == null) return;
            try
            {
                var map = speaker.MapHeld;
                bool grave = dead.Corpse?.ParentHolder is Building_Grave;
                Gods.Deed(GodDomain.Dead, map, grave ? GodsMath.FuneralDeed : GodsMath.FuneralUngravedDeed,
                    (grave ? "CC_God_Deed_Funeral" : "CC_God_Deed_FuneralNoGrave").Translate(dead.LabelShort.Named("DEAD"), speaker.LabelShort.Named("SPEAKER")).Resolve());
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a funeral", e);
            }
        }
    }
}
