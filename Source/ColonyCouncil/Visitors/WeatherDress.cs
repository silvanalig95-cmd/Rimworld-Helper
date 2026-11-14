using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// People who come to the colony's map come dressed for the weather they will meet there (the rules are
    /// WeatherDressMath.cs). Whoever appears on one of the colony's homes, the game's visitors, traders and travellers,
    /// quest guests and lodgers, allies, raiders, Hospitality's guests, Rimfolk's own visits, envoys, camps and the
    /// peoples' events, and anything another mod brings through the game's spawning, is looked at once, just before they
    /// appear (<see cref="Patch_ArrivalDress"/>): if the next two days' weather (the land's own reckoning, CampWeather.StayRange)
    /// would leave them short of comfortable, they put on warm or cool plain layers of their own people's age and kind,
    /// parkas, tuques, dusters, cowboy hats and whatever the player's mods add, swapping a plainer layer where one is in the
    /// way, in their faith's style and their kind's colours, never taking off armour, belts, gear or what a title or role
    /// asks of them, and within what their clothes may cost (a raider's close to what it was). Only where it keeps them
    /// safe through the stay: past that, the extreme cold or heat, they come as they are and the game's own leaving
    /// stays, and the message says it was the extreme cold or heat (<see cref="Patch_WeatherLeaveWords"/>). Camps dress
    /// the same way for their own stay (CampWeather.Dress). Nothing is shown for it, and nothing is saved.
    /// </summary>
    public static class WeatherDress
    {
        // ---------------- What there is to wear ----------------

        private static List<ThingStuffPair> pairs;
        private static float parka, tuque, maxPrice;

        private static void Prices()
        {
            if (maxPrice > 0f) return;
            parka = StatDefOf.MarketValue.Worker.GetValueAbstract(ThingDefOf.Apparel_Parka, ThingDefOf.Cloth);
            tuque = StatDefOf.MarketValue.Worker.GetValueAbstract(ThingDefOf.Apparel_Tuque, ThingDefOf.Cloth);
            maxPrice = parka * WeatherDressMath.PieceShare;
        }

        /// <summary>
        /// Every plain layer of the loaded mods that keeps out cold or heat and that the game would hand out for warmth, in
        /// a few of what each is made of (the cheapest, the warmest and the coolest, none dearer than a cloth parka and a half).
        /// </summary>
        public static List<ThingStuffPair> Pairs
        {
            get
            {
                if (pairs != null) return pairs;
                Prices();
                var all = ThingStuffPair.AllWith(td => td.IsApparel && td.apparel != null && Offerable(td))
                    .Where(pa => (Cold(pa) > 0f || Heat(pa) > 0f) && pa.Price <= maxPrice && pa.Commonality > 0f && !PawnApparelGenerator.IsDerpApparel(pa.thing, pa.stuff));
                pairs = new List<ThingStuffPair>();
                foreach (var g in all.GroupBy(pa => pa.thing))
                    foreach (var pa in new[] { g.MinBy(x => x.Price), g.MaxBy(x => Cold(x)), g.MaxBy(x => Heat(x)) })
                        if (!pairs.Contains(pa))
                            pairs.Add(pa);
                return pairs;
            }
        }

        private static float Cold(ThingStuffPair pa) => pa.InsulationCold - (pa.thing.equippedStatOffsets?.GetStatOffsetFromList(StatDefOf.ComfyTemperatureMin) ?? 0f);

        private static float Heat(ThingStuffPair pa) => pa.InsulationHeat + (pa.thing.equippedStatOffsets?.GetStatOffsetFromList(StatDefOf.ComfyTemperatureMax) ?? 0f);

        private static float Cold(Apparel a) => a.GetStatValue(StatDefOf.Insulation_Cold) - (a.def.equippedStatOffsets?.GetStatOffsetFromList(StatDefOf.ComfyTemperatureMin) ?? 0f);

        private static float Heat(Apparel a) => a.GetStatValue(StatDefOf.Insulation_Heat) + (a.def.equippedStatOffsets?.GetStatOffsetFromList(StatDefOf.ComfyTemperatureMax) ?? 0f);

        // ---------------- What a piece is ----------------

        /// <summary>Armour: of the game's armour kinds (or a mod's named so), or armoured by its make or of its own.</summary>
        public static bool IsArmour(ThingDef d)
        {
            bool kind = d.thingCategories != null && d.thingCategories.Any(c => c != null && (c == ThingCategoryDefOf.ApparelArmor || c == ThingCategoryDefOf.ArmorHeadgear
                                                                             || c.defName.IndexOf("Armor", StringComparison.OrdinalIgnoreCase) >= 0
                                                                             || c.defName.IndexOf("Armour", StringComparison.OrdinalIgnoreCase) >= 0
                                                                             || c.defName.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0));
            float factor = d.MadeFromStuff ? d.statBases?.GetStatValueFromList(StatDefOf.StuffEffectMultiplierArmor, 0f) ?? 0f : 0f;
            float own = d.statBases?.GetStatValueFromList(StatDefOf.ArmorRating_Sharp, 0f) ?? 0f;
            return WeatherDressMath.Armour(kind, factor, own);
        }

        private static bool IsUtility(ThingDef d) => d.apparel.layers != null && d.apparel.layers.Contains(ApparelLayerDefOf.Belt);

        private static bool IsGear(ThingDef d) => d.Verbs.Count > 0 || !d.apparel.abilities.NullOrEmpty() || d.HasComp(typeof(CompShield))
                                                 || d.HasAssignableCompFrom(typeof(CompApparelVerbOwner));

        private static bool IsSpecial(ThingDef d) => d.thingClass != typeof(Apparel);

        private static readonly StatDef[] Hindered = { StatDefOf.MoveSpeed, StatDefOf.WorkSpeedGlobal, StatDefOf.ShootingAccuracyPawn, StatDefOf.MeleeHitChance, StatDefOf.MeleeDodgeChance };

        private static bool Hinders(ThingDef d) => d.equippedStatOffsets != null && Hindered.Any(s => s != null && d.equippedStatOffsets.GetStatOffsetFromList(s) < 0f);

        private static bool Offerable(ThingDef d) =>
            WeatherDressMath.Offered(true, d.apparel.canBeGeneratedToSatisfyWarmth && !d.apparel.mechanitorApparel && !d.apparel.slaveApparel,
                d.generateAllowChance > 0f && d.generateCommonality > 0f, IsArmour(d), IsUtility(d), IsGear(d), IsSpecial(d), Hinders(d));

        /// <summary>What they keep on whatever the weather: armour, a belt, gear, a special piece, what is locked on them or asked of them.</summary>
        public static bool Kept(Apparel a, Pawn p)
        {
            if (a?.def?.apparel == null) return true;
            return WeatherDressMath.Kept(IsArmour(a.def), IsUtility(a.def), IsGear(a.def), IsSpecial(a.def), p.apparel != null && p.apparel.IsLocked(a), Required(a, p));
        }

        /// <summary>A title's apparel (the Empire keeps its own) or the faith's role apparel.</summary>
        private static bool Required(Apparel a, Pawn p)
        {
            try
            {
                if (p.royalty != null)
                    foreach (var t in p.royalty.AllTitlesInEffectForReading)
                        if (t?.def?.requiredApparel != null && t.def.requiredApparel.Any(r => r != null && r.ApparelMeetsRequirement(a.def, false)))
                            return true;
                var role = ModsConfig.IdeologyActive ? p.Ideo?.GetRole(p) : null;
                if (role?.ApparelRequirements != null && role.ApparelRequirements.Any(r => r?.requirement != null && r.requirement.ApparelMeetsRequirement(a.def, false)))
                    return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("weather dress: required apparel", e);
                return true;
            }
            return false;
        }

        // ---------------- Theirs to wear ----------------

        private static bool TheirKind(ThingDef d, Pawn p) =>
            p.kindDef?.apparelTags != null && d.apparel.tags != null && p.kindDef.apparelTags.Any(t => d.apparel.tags.Contains(t));

        private static bool Disallowed(ThingDef d, Pawn p) =>
            p.kindDef?.apparelDisallowTags != null && d.apparel.tags != null && p.kindDef.apparelDisallowTags.Any(t => d.apparel.tags.Contains(t));

        /// <summary>Theirs to wear: their size, age and shape, of their people's age (and the colony's era), not barred to their kind.</summary>
        public static bool Fits(ThingStuffPair pa, Pawn p, TechLevel tech)
        {
            var t = pa.thing;
            if (!t.apparel.PawnCanWear(p, false) || !t.apparel.CorrectAgeForWearing(p) || Disallowed(t, p) || !ApparelUtility.HasPartsToWear(p, t)) return false;
            return WeatherDressMath.AgeFits((int)t.techLevel, (int)tech, t.apparel.anyTechLevelCanUseForWarmth, TheirKind(t, p), Avail.IsPreIndustrial);
        }

        // ---------------- The weather ----------------

        private static Map stayMap;
        private static int stayHour = -1, stayHours = -1;
        private static (float low, float high) stay;

        /// <summary>The coldest and hottest the next <paramref name="hours"/> on this map are likely to bring (worked out once an hour).</summary>
        public static (float low, float high) Stay(Map map, int hours)
        {
            if (map.Biome?.constantOutdoorTemperature != null)
            {
                float t = map.mapTemperature.OutdoorTemp;
                return (t, t);
            }
            int hour = Find.TickManager.TicksGame / GenDate.TicksPerHour;
            if (map != stayMap || hour != stayHour || hours != stayHours)
            {
                stay = CampWeather.StayRange(map, hours * GenDate.TicksPerHour);
                stayMap = map;
                stayHour = hour;
                stayHours = hours;
            }
            return stay;
        }

        // ---------------- Dressing ----------------

        private sealed class Plan
        {
            public List<Apparel> worn;
            public List<ThingStuffPair> offered;
            public WeatherDressMath.Choice choice;
        }

        private static float Min(Pawn p) => p.GetStatValue(StatDefOf.ComfyTemperatureMin);

        private static float Max(Pawn p) => p.GetStatValue(StatDefOf.ComfyTemperatureMax);

        private static Plan Make(Pawn p, float low, float high, float nowMin, float nowMax, TechLevel tech, bool hostile)
        {
            var body = p.RaceProps.body;
            var worn = p.apparel.WornApparel.ToList();
            var wornPieces = new List<WeatherDressMath.Piece>();
            float bareMin = nowMin, bareMax = nowMax, wornWorth = 0f;
            foreach (var a in worn)
            {
                float c = Cold(a), h = Heat(a);
                bareMin += c;
                bareMax -= h;
                wornWorth += a.MarketValue;
                wornPieces.Add(new WeatherDressMath.Piece(c, h, a.MarketValue, 0, Kept(a, p)));
            }
            bool needCold = low < nowMin, needHeat = high > nowMax;
            var ideo = ModsConfig.IdeologyActive ? p.Ideo : null;
            var offered = Pairs.Where(pa => (needCold && Cold(pa) > 0f || needHeat && Heat(pa) > 0f) && Fits(pa, p, tech)).ToList();
            var pieces = offered.Select(pa => new WeatherDressMath.Piece(Cold(pa), Heat(pa), pa.Price,
                WeatherDressMath.Fit(TheirKind(pa.thing, p), ideo?.GetStyleFor(pa.thing) != null))).ToList();
            var withWorn = new Dictionary<long, bool>();
            var withOffered = new Dictionary<long, bool>();
            bool ClashWorn(int o, int w)
            {
                long k = (long)o * 1024 + w;
                if (!withWorn.TryGetValue(k, out bool v)) withWorn[k] = v = !ApparelUtility.CanWearTogether(offered[o].thing, worn[w].def, body);
                return v;
            }
            bool ClashOffered(int o, int q)
            {
                long k = (long)Math.Min(o, q) * 65536 + Math.Max(o, q);
                if (!withOffered.TryGetValue(k, out bool v)) withOffered[k] = v = offered[o].thing == offered[q].thing || !ApparelUtility.CanWearTogether(offered[o].thing, offered[q].thing, body);
                return v;
            }
            Prices();
            float room = WeatherDressMath.Room(hostile, wornWorth, p.kindDef?.apparelMoney.max ?? 0f, parka, tuque);
            var choice = WeatherDressMath.Choose(wornPieces, pieces, ClashWorn, ClashOffered, bareMin, bareMax, low, high, room);
            return new Plan { worn = worn, offered = offered, choice = choice };
        }

        /// <summary>Puts on what the pick chose, in its order: what each can't be worn with comes off (never a kept piece), in their kind's colours and faith's style.</summary>
        private static List<Apparel> Apply(Pawn p, Plan plan)
        {
            var put = new List<Apparel>();
            var body = p.RaceProps.body;
            foreach (int o in plan.choice.on)
            {
                var pa = plan.offered[o];
                var off = p.apparel.WornApparel.Where(w => !ApparelUtility.CanWearTogether(w.def, pa.thing, body)).ToList();
                if (off.Any(w => Kept(w, p))) continue;
                var ap = (Apparel)ThingMaker.MakeThing(pa.thing, pa.stuff);
                PawnGenerator.PostProcessGeneratedGear(ap, p);
                PawnApparelGenerator.PostProcessApparel(ap, p);
                if (!EquipmentUtility.CanEquip(ap, p))
                {
                    ap.Destroy();
                    continue;
                }
                foreach (var w in off)
                {
                    p.apparel.Remove(w);
                    w.Destroy();
                }
                p.apparel.Wear(ap, false);
                put.Add(ap);
            }
            put.RemoveAll(a => a.Destroyed || a.Wearer != p);
            return put;
        }

        private static bool Dressable(Pawn p) => p?.apparel != null && p.RaceProps?.Humanlike == true && !p.Dead;

        /// <summary>
        /// Dresses one of them for weather from <paramref name="low"/> to <paramref name="high"/>, a layer at a time, as long
        /// as a layer brings them nearer comfortable (a camp's people, who come only if it then keeps them safe). What they put
        /// on, in the order put on.
        /// </summary>
        public static List<Apparel> Layers(Pawn p, float low, float high, TechLevel tech, bool hostile)
        {
            if (!Dressable(p)) return new List<Apparel>();
            float nowMin = Min(p), nowMax = Max(p);
            if (WeatherDressMath.Short(nowMin, nowMax, low, high) <= WeatherDressMath.Enough) return new List<Apparel>();
            var plan = Make(p, low, high, nowMin, nowMax, tech, hostile);
            return plan.choice.on.Count == 0 ? new List<Apparel>() : Apply(p, plan);
        }

        /// <summary>
        /// Dresses someone about to appear on <paramref name="map"/> for its next <paramref name="hours"/>, if clothing of their
        /// people's age keeps them safe through it; past that they come as they are.
        /// </summary>
        public static WeatherDressMath.Dressing ForStay(Pawn p, Map map, int hours)
        {
            if (!Dressable(p) || map == null || p.Faction == null) return WeatherDressMath.Dressing.AsTheyAre;
            var tech = Peoples.TechOf(p.Faction);
            if ((int)tech < WeatherDressMath.Neolithic) return WeatherDressMath.Dressing.AsTheyAre;
            var (low, high) = Stay(map, hours);
            float nowMin = Min(p), nowMax = Max(p);
            if (WeatherDressMath.Short(nowMin, nowMax, low, high) <= WeatherDressMath.Enough) return WeatherDressMath.Dressing.AsTheyAre;
            var plan = Make(p, low, high, nowMin, nowMax, tech, p.Faction.HostileTo(Faction.OfPlayer));
            var outcome = WeatherDressMath.Outcome(nowMin, nowMax, plan.choice.comfyMin, plan.choice.comfyMax, low, high);
            if (outcome == WeatherDressMath.Dressing.Dressed) Apply(p, plan);
            return outcome;
        }

        /// <summary>Those to come exactly as they are made (robbed on the road, down to the shirt): left alone once, as they appear.</summary>
        private static readonly HashSet<int> asTheyCame = new HashSet<int>();

        public static void AsTheyCame(Pawn p)
        {
            if (p != null) asTheyCame.Add(p.thingIDNumber);
        }

        // ---------------- Arriving ----------------

        /// <summary>How the game holds someone just before they appear: carried, in a pod or shuttle, in something on the map, or nothing (walked in).</summary>
        private static WeatherDressMath.Came HowCame(Pawn p)
        {
            if (p.BeingTransportedOnGravship) return WeatherDressMath.Came.FromHere;
            IThingHolder h = p.ParentHolder;
            for (int i = 0; h != null && i < 10; i++)
            {
                switch (h)
                {
                    case Map _:
                        return WeatherDressMath.Came.Walked;
                    case Pawn_CarryTracker _:
                    case Pawn_InventoryTracker _:
                    case Pawn_EquipmentTracker _:
                    case Pawn_ApparelTracker _:
                        return WeatherDressMath.Came.FromHere;
                    case ActiveTransporterInfo _:
                    case ActiveTransporter _:
                    case Skyfaller _:
                        return WeatherDressMath.Came.Pod;
                    case CompTransporter ct:
                        return ct.parent?.TryGetComp<CompShuttle>() != null ? WeatherDressMath.Came.Shuttle : WeatherDressMath.Came.Pod;
                    case Thing t when t.Spawned:
                        return t.TryGetComp<CompShuttle>() != null ? WeatherDressMath.Came.Shuttle : WeatherDressMath.Came.FromHere;
                }
                h = h.ParentHolder;
            }
            return WeatherDressMath.Came.Walked;
        }

        /// <summary>Whether someone about to appear on <paramref name="map"/> is dressed for its weather (WeatherDressMath.Qualifies).</summary>
        public static WeatherDressMath.Who Arriving(Pawn p, Map map)
        {
            var race = p.RaceProps;
            var f = p.Faction;
            int now = Find.TickManager?.TicksGame ?? 0;
            var a = new WeatherDressMath.Arrival
            {
                playing = Current.ProgramState == ProgramState.Playing,
                generating = MapGenerator.mapBeingGenerated == map,
                home = map.IsPlayerHome,
                vacuum = MapFacts.InVacuum(map),
                humanlike = race?.Humanlike == true,
                toolUser = race?.ToolUser == true,
                flesh = race?.IsFlesh == true,
                entity = race != null && race.IsAnomalyEntity || p.IsMutant,
                faction = f != null,
                wild = p.IsWildMan(),
                player = f != null && f.IsPlayer,
                tech = f == null ? WeatherDressMath.Undefined : (int)Peoples.TechOf(f),
                fresh = p.spawnedTick < 0 && p.despawnedTick < 0,
                recentlyHere = p.despawnedTick >= 0 && now - p.despawnedTick < WeatherDressMath.RecentTicks,
                baby = p.DevelopmentalStage.Newborn() || p.DevelopmentalStage.Baby(),
                downed = p.Downed,
                captive = p.IsPrisoner || p.IsSlave,
                came = HowCame(p)
            };
            return WeatherDressMath.Qualifies(a);
        }

        /// <summary>Someone about to appear on a map (<see cref="Patch_ArrivalDress"/>): dressed for its next two days if they arrive and qualify.</summary>
        public static void OnSpawn(Pawn p, Map map)
        {
            try
            {
                if (p?.apparel == null || asTheyCame.Remove(p.thingIDNumber)) return;
                if (Arriving(p, map) != WeatherDressMath.Who.Dress) return;
                ForStay(p, map, WeatherDressMath.ArrivalHours);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("dressed for the weather on arrival", e);
            }
        }

        // ---------------- Leaving for the weather ----------------

        /// <summary>
        /// The game's own groups (visitors, traders, allies, beggars, Hospitality's guests) leaving for the temperature: the
        /// message says it is the extreme cold or the extreme heat, whichever has taken hold of them.
        /// </summary>
        public static void LeaveWords(TransitionAction_Message a, Lord lord)
        {
            var f = lord?.faction;
            if (a == null || f?.def == null || a.message.NullOrEmpty() || lord.ownedPawns == null) return;
            string plural = f.def.pawnsPlural.CapitalizeFirst();
            string game = "MessageVisitorsDangerousTemperature".Translate(plural, f.Name);
            if (a.message != game) return;
            float cold = 0f, heat = 0f;
            foreach (var p in lord.ownedPawns)
            {
                var hs = p?.health?.hediffSet;
                if (hs == null) continue;
                cold = Math.Max(cold, hs.GetFirstHediffOfDef(HediffDefOf.Hypothermia)?.Severity ?? 0f);
                heat = Math.Max(heat, hs.GetFirstHediffOfDef(HediffDefOf.Heatstroke)?.Severity ?? 0f);
            }
            if (cold <= 0f && heat <= 0f) return;
            a.message = WeatherDressMath.GameKey(cold, heat).Translate(plural, f.Name);
        }
    }

    /// <summary>
    /// Whoever is about to appear on a map (the game's one way of putting anything there): dressed for the weather first,
    /// if they are arriving and qualify (WeatherDress.OnSpawn). Not on loading a save.
    /// </summary>
    [HarmonyPatch(typeof(GenSpawn), nameof(GenSpawn.Spawn), new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(Rot4), typeof(WipeMode), typeof(bool), typeof(bool) })]
    public static class Patch_ArrivalDress
    {
        public static void Prefix(Thing newThing, Map map, bool respawningAfterLoad)
        {
            if (respawningAfterLoad || map == null || !(newThing is Pawn p) || p.Spawned || !RimfolkPatches.On) return;
            WeatherDress.OnSpawn(p, map);
        }
    }

    /// <summary>The game's "leaving because of dangerous temperature" for a group: said as the extreme cold or the extreme heat (WeatherDress.LeaveWords).</summary>
    [HarmonyPatch(typeof(TransitionAction_Message), nameof(TransitionAction_Message.DoAction))]
    public static class Patch_WeatherLeaveWords
    {
        public static void Prefix(TransitionAction_Message __instance, Transition trans)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                WeatherDress.LeaveWords(__instance, trans?.target?.lord);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("weather leave words", e);
            }
        }
    }
}
