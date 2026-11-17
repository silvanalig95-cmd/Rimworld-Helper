using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Each effect of the gods' touch, played out on the map (Gods/Touch/GodTouchMath.cs has which god does which): the god's
    /// own short blessing on those who saw it, flowers, fires refuelled, machines mended, the sky cleared, rats gone; an
    /// inspiration, a find, crops ripened, game on the land, rich ground, travellers, a trader, an aurora, the stores kept,
    /// wounds healed, a cat come to stay, fish on the bank; and rarely a god's presence staying on the land for days, a vein
    /// of gold, animals come to stay, the fields made rich, someone come to join, a season without cold, the sun's year, an
    /// old wound gone, a calm kept, a spring, a hoard. The angers: brambles, fires out, the game gone, food going off, a
    /// machine broken, fog, things gone from the stores, a bad night, the dark staying.
    /// Every one is the game's own things and the player's mods' by name (softly, the game's own where a mod's isn't loaded),
    /// by the colony's age; every one shown where it happened (GodTouchFx) and told as what was seen, never naming the god in
    /// its words. None hurts anyone: the angers spoil, break, chill and frighten, never wound.
    /// </summary>
    public static class GodTouchEffects
    {
        internal static ThingDef Thing(string name) => DefDatabase<ThingDef>.GetNamedSilentFail(name);

        private static int Now => Verse.Find.TickManager.TicksGame;

        // ================= Whether it can happen now =================

        /// <summary>What this effect needs at the home now (cheap checks; the doing may still find nothing and say so).</summary>
        public static bool Can(TouchEntry e, TouchContext c)
        {
            var map = c.map;
            if (map == null) return false;
            try
            {
                switch (e.effect)
                {
                    case TouchEffect.Bless: return BlessDef(c.god) != null && c.saw.Count > 0;
                    case TouchEffect.Learn: return SkillOf(c.god) != null && c.saw.Any(p => p.skills != null);
                    case TouchEffect.Refuel:
                    case TouchEffect.FiresOut:
                        return Fires(map, c.cell, e.effect == TouchEffect.Refuel).Any();
                    case TouchEffect.Mend:
                        return Machines(map).Any(b => b.GetComp<CompBreakdownable>()?.BrokenDown == true && b.Position.InHorDistOf(c.cell, GodTouchMath.NearRadius * 2f)) || Worn(map, c.cell).Any();
                    case TouchEffect.Charge: return Batteries(map).Any(b => b.AmountCanAccept > 1f);
                    case TouchEffect.ClearSky: return map.weatherManager.curWeather != null && map.weatherManager.curWeather.defName != "Clear" && !map.weatherManager.curWeather.isBad;
                    case TouchEffect.Snow: return map.mapTemperature.OutdoorTemp < -1f && map.weatherManager.curWeather?.snowRate <= 0f;
                    case TouchEffect.Rain: return map.mapTemperature.OutdoorTemp > 2f && map.weatherManager.curWeather?.rainRate <= 0f;
                    case TouchEffect.Fog: return map.weatherManager.curWeather?.defName != "Fog" && map.weatherManager.curWeather?.isBad != true;
                    case TouchEffect.RatsGone: return Vermin(map).Any();
                    case TouchEffect.Inspire: return Inspirations(c.god).Any() && c.saw.Any(p => p.mindState?.inspirationHandler != null && !p.Inspired);
                    case TouchEffect.Find: return FindOf(c.god, map) != null;
                    case TouchEffect.Ripen: return Crops(map, c.cell, GodTouchMath.NearRadius).Any(p => p.Growth < 0.95f);
                    case TouchEffect.Healroot: return Thing("Plant_HealrootWild") != null;
                    case TouchEffect.Game: return GameKinds(map).Any();
                    case TouchEffect.GameLeaves: return WildGame(map).Any();
                    case TouchEffect.RichSoil: return true;
                    case TouchEffect.Fertile: return GrowingCells(map, c.cell).Any();
                    case TouchEffect.Visitors: return CanIncident(map, "VisitorGroup");
                    case TouchEffect.Trader: return c.god.Key == "Ducat" && Comms(map) && CanIncident(map, "OrbitalTraderArrival") || CanIncident(map, "TraderCaravanArrival");
                    case TouchEffect.Goodwill: return Friends(map).Any();
                    case TouchEffect.Aurora: return Night(map) && !map.roofGrid.Roofed(c.cell) && DefDatabase<GameConditionDef>.GetNamedSilentFail("Aurora") != null
                                                    && !map.gameConditionManager.ConditionIsActive(DefDatabase<GameConditionDef>.GetNamedSilentFail("Aurora"));
                    case TouchEffect.SkyGift: return CanIncident(map, "ShipChunkDrop");
                    case TouchEffect.Preserve:
                    case TouchEffect.Spoil:
                        return Food(map).Any();
                    case TouchEffect.ColdBreak: return ColdSnap(map) != null || Food(map).Any();
                    case TouchEffect.Heal: return Wounded(map, c).Any();
                    case TouchEffect.CatComes: return DefDatabase<PawnKindDef>.GetNamedSilentFail("Cat") != null;
                    case TouchEffect.Fish: return Fishes().Any() && Water(map, c.cell).IsValid;
                    case TouchEffect.Linger:
                    case TouchEffect.Dim:
                        return GodTouchMath.Lingers.Contains(c.god.Key) && GodPresenceOwn.DefOf(c.god) != null;
                    case TouchEffect.Vein: return true;
                    case TouchEffect.Herd: return CanIncident(map, "FarmAnimalsWanderIn");
                    case TouchEffect.Wanderer: return CanIncident(map, "WandererJoin");
                    case TouchEffect.Hardy: return GodTouchDefOf.Rimfolk_GodTouch_Hardy != null && map.mapTemperature.OutdoorTemp < 10f;
                    case TouchEffect.SunYear: return GodTouchDefOf.Rimfolk_GodTouch_SunWarmed != null && Crops(map, IntVec3.Invalid, 0f).Any();
                    case TouchEffect.ScarGone: return Scarred(map, c).Any();
                    case TouchEffect.Ward: return GodTouchDefOf.Rimfolk_GodTouch_Ward != null && c.saw.Count > 0;
                    case TouchEffect.Spring: return true;
                    case TouchEffect.Hoard: return Thing("Gold") != null;
                    case TouchEffect.Bloom:
                    case TouchEffect.Brambles:
                        return true;
                    case TouchEffect.Breakdown: return Machines(map).Any(b => b.GetComp<CompBreakdownable>() is CompBreakdownable br && !br.BrokenDown);
                    case TouchEffect.Vanish: return VanishStack(map, c.god).Any();
                    case TouchEffect.Sleepless: return GodTouchDefOf.Rimfolk_GodTouch_Sleepless != null && c.saw.Count > 0;
                    case TouchEffect.Merry: return c.saw.Any(p => p.needs?.joy != null && p.needs.joy.CurLevelPercentage < 0.95f);
                    case TouchEffect.Sour: return SourStack(map) != null;
                    default: return false;
                }
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("gods: whether an effect can happen (" + e.effect + ")", ex);
                return false;
            }
        }

        // ================= The doing =================

        /// <summary>Plays the effect out, shows it and tells it. False when there was nothing to do after all.</summary>
        public static bool Do(TouchEntry e, TouchContext c)
        {
            try
            {
                var at = c.cell;
                string words;
                switch (e.effect)
                {
                    case TouchEffect.Bless: words = Bless(c); break;
                    case TouchEffect.Learn: words = Learn(c); break;
                    case TouchEffect.Bloom: words = Plants(c, Flowers(), 2.6f, 9, 1f) ? K("Bloom") : null; break;
                    case TouchEffect.Brambles: words = Plants(c, new[] { Thing("Plant_Brambles") }, 2.2f, 6, 0.6f) ? K("Brambles") : null; break;
                    case TouchEffect.Healroot: words = Plants(c, new[] { Thing("Plant_HealrootWild") }, 3.5f, 4, 0.75f) ? K("Healroot") : null; break;
                    case TouchEffect.Refuel: words = Refuel(c, true); break;
                    case TouchEffect.FiresOut: words = Refuel(c, false); break;
                    case TouchEffect.Mend: words = Mend(c); break;
                    case TouchEffect.Charge: words = Charge(c); break;
                    case TouchEffect.ClearSky: words = Weather(c, "Clear", "ClearSky"); break;
                    case TouchEffect.Snow: words = Weather(c, "SnowGentle", "Snow"); break;
                    case TouchEffect.Rain: words = Weather(c, "Rain", "Rain"); break;
                    case TouchEffect.Fog: words = Weather(c, "Fog", "Fog"); break;
                    case TouchEffect.RatsGone: words = RatsGone(c); break;
                    case TouchEffect.Inspire: return Inspire(c);
                    case TouchEffect.Find: words = Leave(c, FindOf(c.god, c.map)); break;
                    case TouchEffect.Hoard:
                    {
                        string found = Leave(c, (Thing("Gold"), Rand.RangeInclusive(15, 30)));
                        return found != null && Great(c, c.cell, "Hoard", found);
                    }
                    case TouchEffect.Ripen: words = Ripen(c, c.cell, GodTouchMath.NearRadius, GodTouchMath.RipenNear) > 0 ? K("Ripen") : null; break;
                    case TouchEffect.Game: words = Game(c); break;
                    case TouchEffect.GameLeaves: words = GameLeaves(c); break;
                    case TouchEffect.RichSoil: words = Soil(c, GenRadial.RadialCellsAround(c.cell, 3.9f, true), 14) ? K("RichSoil") : null; break;
                    case TouchEffect.Fertile: return Fertile(c);
                    case TouchEffect.Visitors: words = Incident(c, "VisitorGroup") ? K("Visitors") : null; break;
                    case TouchEffect.Trader: words = Trader(c); break;
                    case TouchEffect.Goodwill: words = Goodwill(c); break;
                    case TouchEffect.Aurora: words = Aurora(c); break;
                    case TouchEffect.SkyGift: words = Incident(c, "ShipChunkDrop") ? K("SkyGift") : null; break;
                    case TouchEffect.Preserve: words = Food(c, true); break;
                    case TouchEffect.Spoil: words = Food(c, false); break;
                    case TouchEffect.ColdBreak: words = ColdBreak(c); break;
                    case TouchEffect.Heal: words = Heal(c); break;
                    case TouchEffect.CatComes: words = CatComes(c); break;
                    case TouchEffect.Fish: words = Fish(c); break;
                    case TouchEffect.Breakdown: words = Breakdown(c); break;
                    case TouchEffect.Vanish: words = Vanish(c); break;
                    case TouchEffect.Sleepless: words = OnEach(c, GodTouchDefOf.Rimfolk_GodTouch_Sleepless, 4, "Sleepless"); break;
                    case TouchEffect.Merry: words = Merry(c); break;
                    case TouchEffect.Sour: words = Sour(c); break;
                    // The great: a god's moment of its own, the camera on it, its card saying what happened.
                    case TouchEffect.Linger:
                    case TouchEffect.Dim:
                        return Linger(c);
                    case TouchEffect.Vein:
                    {
                        var vein = Vein(c);
                        return vein.IsValid && Great(c, vein, "Vein");
                    }
                    case TouchEffect.Herd: return Incident(c, "FarmAnimalsWanderIn") && Great(c, c.cell, "Herd");
                    case TouchEffect.Wanderer: return Incident(c, "WandererJoin") && Great(c, c.cell, "Wanderer");
                    case TouchEffect.Hardy: return Everyone(c, GodTouchDefOf.Rimfolk_GodTouch_Hardy) && Great(c, c.cell, "Hardy");
                    case TouchEffect.SunYear: return SunYear(c) && Great(c, c.cell, "SunYear");
                    case TouchEffect.ScarGone: return ScarGone(c);
                    case TouchEffect.Ward: return Ward(c);
                    case TouchEffect.Spring:
                    {
                        var spring = Spring(c);
                        return spring.IsValid && Great(c, spring, "Spring");
                    }
                    default: return false;
                }
                if (words.NullOrEmpty()) return false;
                // Shown already where it happened (the animals' entry, the stores, the bank).
                if (words == Shown) return true;
                Show(c, at, words);
                return true;
            }
            catch (Exception ex)
            {
                CouncilLog.ErrorOnce("gods: an effect (" + e.effect + ")", ex);
                return false;
            }
        }

        internal static string K(string what) => "CC_GodTouch_" + what;

        /// <summary>An effect that showed and told itself where it happened.</summary>
        internal const string Shown = "\u0001shown";

        /// <summary>The words of an effect, filled (its key and the usual {HOME}, then <paramref name="more"/>).</summary>
        internal static string Words(TouchContext c, string key, params NamedArgument[] more)
        {
            var args = new List<NamedArgument> { Homes.Name(c.map).Named("HOME") };
            if (more != null) args.AddRange(more);
            return key.Translate(args.ToArray()).Resolve().CapitalizeFirst();
        }

        /// <summary>
        /// A small or notable effect shown: the god's own look at the place (GodTouchFx), the camera and a sound for a notable
        /// one, a message of what happened, and the colony's book keeping it as those who saw it saw it.
        /// </summary>
        internal static void Show(TouchContext c, IntVec3 at, string key, params NamedArgument[] more)
        {
            string text = key.StartsWith("CC_", StringComparison.Ordinal) ? Words(c, key, more) : key;
            if (!at.IsValid || !at.InBounds(c.map)) at = c.cell;
            // The camera there first, so the god's look plays where the player is looking.
            if (c.tier >= TouchTier.Notable) GodPresence.Look(c.map, at);
            GodTouchFx.Show(c.god, c.map, at, c.tier, c.dark);
            if (c.tier >= TouchTier.Notable) GodTouchFx.Sound(c.god, c.map, at);
            Tell.Message(NewsKind.Land, c.tier >= TouchTier.Notable ? Importance.Important : Importance.Notable, c.map, text, new LookTargets(at, c.map),
                c.dark ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.PositiveEvent);
            if (c.tier >= TouchTier.Notable) GodTouch.Write(c, c.dark ? GodActKind.Wrath : GodActKind.Blessing, text);
        }

        /// <summary>
        /// A great effect: the god's own moment (its sound, the sky, the camera on it, its card saying what happened, named only
        /// where the colony knows the god), the colony's book, and everyone of the home who believes at all remembering it.
        /// </summary>
        private static bool Great(TouchContext c, IntVec3 at, string what, string text = null, int linger = 0)
        {
            if (!at.IsValid || !at.InBounds(c.map)) at = c.cell;
            text ??= Words(c, K(what + "_Text"));
            string title = Words(c, K(what + "_Title"));
            var spec = PresenceSpec.For(c.god, c.map, c.dark ? PresenceKind.Wrath : PresenceKind.Blessing, at);
            if (c.plain) spec.veiled = false;
            spec.title = title;
            spec.text = text;
            spec.what = "touch";
            spec.lingerTicks = linger;
            GodTouchFx.Show(c.god, c.map, at, TouchTier.Great, c.dark);
            // The moment writes itself into the colony's book as those who saw it saw it (Gods.Moment); a plain letter's words are written here.
            if (!GodTouch.Moment(spec))
            {
                Tell.Letter(NewsKind.Land, Importance.Important, c.map, title, text, c.dark ? LetterDefOf.NegativeEvent : LetterDefOf.PositiveEvent, new LookTargets(at, c.map));
                GodTouch.Write(c, c.dark ? GodActKind.Wrath : GodActKind.Blessing, text);
            }
            if (!c.dark)
                foreach (var p in c.map.mapPawns.FreeColonistsSpawned.Where(x => Worship.Ours(x) && !Worship.Scoffer(x)).ToList())
                    CouncilGameComponent.GiveThought(p, GodTouchDefOf.Rimfolk_GodTouch_Remembered);
            return true;
        }

        // ================= Small =================

        /// <summary>Rimfolk_GodTouch_Bless_ and the god's key: its own short blessing.</summary>
        internal static HediffDef BlessDef(God g) => g == null ? null : DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_GodTouch_Bless_" + g.Key);

        private static string Bless(TouchContext c)
        {
            var def = BlessDef(c.god);
            var took = Give(c.saw, def, GodTouchMath.BlessAtMost);
            if (took.Count == 0) return null;
            return Words(c, K("Bless_" + c.god.Key), Names(took).Named("WHO"));
        }

        internal static List<Pawn> Give(IEnumerable<Pawn> pawns, HediffDef def, int most)
        {
            var took = new List<Pawn>();
            if (def == null) return took;
            foreach (var p in pawns.Where(x => x?.health != null && !x.Dead).Take(most))
            {
                var had = p.health.hediffSet.GetFirstHediffOfDef(def);
                if (had != null) p.health.RemoveHediff(had);
                p.health.AddHediff(def);
                took.Add(p);
            }
            return took;
        }

        private static string OnEach(TouchContext c, HediffDef def, int most, string key)
        {
            var took = Give(c.saw, def, most);
            return took.Count == 0 ? null : Words(c, K(key), Names(took).Named("WHO"));
        }

        private static bool Everyone(TouchContext c, HediffDef def) => Give(c.map.mapPawns.FreeColonistsSpawned.Where(Worship.Ours).ToList(), def, 99).Count > 0;

        internal static string Names(List<Pawn> pawns) => pawns.Select(p => p.LabelShort).ToCommaList(true);

        /// <summary>The skill each god's people take from it (by key), or null.</summary>
        internal static SkillDef SkillOf(God g)
        {
            string s = g?.Key switch
            {
                "Hollin" => "Plants",
                "Smoulder" => "Crafting",
                "Bellwether" => "Animals",
                "Morel" => "Medicine",
                "Ennu" => "Social",
                _ => null
            };
            return s == null ? null : DefDatabase<SkillDef>.GetNamedSilentFail(s);
        }

        /// <summary>Experience a learner takes from it: a good part of a level for a beginner.</summary>
        public const float LearnXp = 1500f;

        private static string Learn(TouchContext c)
        {
            var skill = SkillOf(c.god);
            if (skill == null) return null;
            var took = new List<Pawn>();
            foreach (var p in c.saw.Where(x => x.skills != null).Take(4))
            {
                var rec = p.skills.GetSkill(skill);
                if (rec == null || rec.TotallyDisabled) continue;
                p.skills.Learn(skill, LearnXp, true);
                took.Add(p);
            }
            return took.Count == 0 ? null : Words(c, K("Learn"), Names(took).Named("WHO"), skill.label.Named("SKILL"));
        }

        /// <summary>Wild flowers, the player's flower mods' first (Vanilla Plants Expanded - Flowers), the game's own beside them.</summary>
        private static IEnumerable<ThingDef> Flowers()
        {
            foreach (var n in new[] { "VPEF_Plant_Daisy", "VPEF_Plant_Marigold", "VPEF_Plant_Crocus", "VPEF_Plant_Aster", "VPEF_Plant_Lavender", "VPEF_Plant_Tulip",
                         "Plant_Dandelion", "Plant_Daylily", "Plant_Rose", "Plant_Astragalus" })
            {
                var d = Thing(n);
                if (d?.plant != null) yield return d;
            }
        }

        /// <summary>Plants come up round the place on open natural ground, grown as far as <paramref name="growth"/>.</summary>
        internal static bool Plants(TouchContext c, IEnumerable<ThingDef> kinds, float radius, int most, float growth)
        {
            var list = kinds.Where(d => d?.plant != null).ToList();
            if (list.Count == 0) return false;
            int n = 0;
            foreach (var cell in GenRadial.RadialCellsAround(c.cell, radius, true).Where(x => x.InBounds(c.map)).InRandomOrder())
            {
                if (n >= most) break;
                var t = cell.GetTerrain(c.map);
                if (t == null || !t.natural || t.IsWater || cell.GetPlant(c.map) != null || cell.GetEdifice(c.map) != null || cell.GetFirstItem(c.map) != null
                    || cell.GetZone(c.map) != null || cell.Fogged(c.map)) continue;
                var def = list.Where(d => d.CanEverPlantAt(cell, c.map)).RandomElementWithFallback();
                if (def == null) continue;
                var p = (Plant)ThingMaker.MakeThing(def);
                p.Growth = Mathf.Clamp01(growth * Rand.Range(0.85f, 1f));
                GenSpawn.Spawn(p, cell, c.map);
                n++;
            }
            return n > 0;
        }

        internal static IEnumerable<CompRefuelable> Fires(Map map, IntVec3 at, bool filling)
        {
            return map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && (!at.IsValid || b.Position.InHorDistOf(at, GodTouchMath.NearRadius * 2f))
                                                                       && (b.GetComp<CompHeatPusher>() != null || b.GetComp<CompFireOverlay>() != null))
                .Select(b => b.GetComp<CompRefuelable>()).Where(r => r != null && (filling ? !r.IsFull : r.HasFuel));
        }

        private static string Refuel(TouchContext c, bool fill)
        {
            int n = 0;
            IntVec3 at = c.cell;
            foreach (var r in Fires(c.map, c.cell, fill).Take(6).ToList())
            {
                if (fill) r.Refuel(r.GetFuelCountToFullyRefuel());
                else r.ConsumeFuel(r.Fuel);
                if (n == 0) at = r.parent.Position;
                GodTouchFx.Show(c.god, c.map, r.parent.Position, TouchTier.Small, !fill);
                n++;
            }
            if (n == 0) return null;
            return Words(c, K(fill ? "Refuel" : "FiresOut"));
        }

        private static IEnumerable<Building> Machines(Map map) =>
            map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && b.GetComp<CompBreakdownable>() != null);

        private static IEnumerable<Building> Worn(Map map, IntVec3 at) =>
            map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned && b.def.useHitPoints && b.HitPoints < b.MaxHitPoints * 0.9f
                                                                && (!at.IsValid || b.Position.InHorDistOf(at, GodTouchMath.NearRadius * 2f)));

        private static string Mend(TouchContext c)
        {
            int n = 0;
            foreach (var b in Machines(c.map).Where(x => x.GetComp<CompBreakdownable>().BrokenDown && x.Position.InHorDistOf(c.cell, GodTouchMath.NearRadius * 2f)).Take(3).ToList())
            {
                b.GetComp<CompBreakdownable>().Notify_Repaired();
                GodTouchFx.Show(c.god, c.map, b.Position, TouchTier.Small, false);
                n++;
            }
            foreach (var b in Worn(c.map, c.cell).Take(8).ToList())
            {
                b.HitPoints = b.MaxHitPoints;
                n++;
            }
            return n == 0 ? null : Words(c, K("Mend"));
        }

        internal static IEnumerable<CompPowerBattery> Batteries(Map map) =>
            map.listerBuildings.allBuildingsColonist.Where(b => b.Spawned).Select(b => b.GetComp<CompPowerBattery>()).Where(b => b != null);

        private static string Charge(TouchContext c)
        {
            int n = 0;
            foreach (var b in Batteries(c.map).Where(x => x.AmountCanAccept > 1f).ToList())
            {
                b.AddEnergy(b.AmountCanAccept);
                GodTouchFx.Show(c.god, c.map, b.parent.Position, TouchTier.Small, false);
                n++;
            }
            return n == 0 ? null : Words(c, K("Charge"));
        }

        internal static string Weather(TouchContext c, string def, string key)
        {
            var w = DefDatabase<WeatherDef>.GetNamedSilentFail(def);
            if (w == null || c.map.weatherManager.curWeather == w) return null;
            c.map.weatherManager.TransitionTo(w);
            return Words(c, K(key));
        }

        private static IEnumerable<Pawn> Vermin(Map map) =>
            map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == null && p.RaceProps?.Animal == true && !p.Dead && !p.Downed && GodPresenceShows.Vermin(p.def));

        private static string RatsGone(TouchContext c)
        {
            int n = 0;
            foreach (var p in Vermin(c.map).ToList())
            {
                p.mindState.exitMapAfterTick = Now + Rand.Range(60, 1200);
                n++;
            }
            if (n == 0) return null;
            MogsWays.CatsWatch(c.map);
            return Words(c, K("RatsGone"));
        }

        // ================= Notable =================

        /// <summary>Each god's inspirations, the game's own and the player's mods' by name, the first that someone who saw it can have.</summary>
        internal static IEnumerable<InspirationDef> Inspirations(God g)
        {
            string[] names = g?.Key switch
            {
                "Hollin" => new[] { "VSIE_Inspired_Planting", "ID_FarmingFrenzy" },
                "Smoulder" => new[] { "Inspired_Creativity", "VSIE_Inspired_Mining", "ID_MiningFrenzy" },
                "Bellwether" => new[] { "Inspired_Taming", "Frenzy_Shoot" },
                "Ennu" => new[] { "Inspired_Recruitment", "VSIE_Party_Frenzy" },
                "Pip" => new[] { "VSIE_Inspired_Research", "Frenzy_Work" },
                "Cirrus" => new[] { "Frenzy_Go", "Frenzy_Shoot" },
                "Undertow" => new[] { "VSIE_Learning_Frenzy", "ID_Enlightened" },
                "Helios" => new[] { "Frenzy_Work", "VSIE_Inspired_Planting" },
                "Selene" => new[] { "Inspired_Surgery" },
                "Mog" => new[] { "Inspired_Taming" },
                "Murmur" => new[] { "VSIE_Inspired_Cooking" },
                "Ducat" => new[] { "Inspired_Trade" },
                "Revel" => new[] { "VSIE_Party_Frenzy", "Inspired_Creativity", "Frenzy_Go" },
                _ => new string[0]
            };
            foreach (var n in names)
            {
                var d = DefDatabase<InspirationDef>.GetNamedSilentFail(n);
                if (d != null) yield return d;
            }
        }

        private static bool Inspire(TouchContext c)
        {
            foreach (var def in Inspirations(c.god))
            foreach (var p in c.saw.Where(x => x.mindState?.inspirationHandler != null && !x.Inspired).InRandomOrder())
            {
                if (def.Worker == null || !def.Worker.InspirationCanOccur(p)) continue;
                string reason = Words(c, K("Inspire_Reason"), p.LabelShort.Named("PAWN"));
                if (!p.mindState.inspirationHandler.TryStartInspiration(def, reason)) continue;
                GodTouchFx.Show(c.god, c.map, p.Position, TouchTier.Notable, false);
                GodTouchFx.Sound(c.god, c.map, p.Position);
                GodTouch.Write(c, GodActKind.Blessing, reason);
                return true;
            }
            return false;
        }

        private static TechLevel Age => Faction.OfPlayer?.def?.techLevel ?? TechLevel.Industrial;

        /// <summary>
        /// What each god leaves where it happened, by the colony's age: Smoulder's ore (steel, silver, a little gold), Morel's
        /// mushrooms, Pip's parts (none for a colony with no machines), Ducat's silver, a few of Revel's drink or smoke of the colony's own kind,
        /// Hush's few pieces of jade or a little silver (where a word was written in the frost; GodImpactMath).
        /// </summary>
        internal static (ThingDef def, int count)? FindOf(God g, Map map)
        {
            switch (g?.Key)
            {
                case "Smoulder":
                {
                    float r = Rand.Value;
                    var d = r < 0.15f && Age >= TechLevel.Medieval ? Thing("Gold") : r < 0.5f ? Thing("Silver") : Thing("Steel");
                    if (d == null) return null;
                    int n = d.defName == "Gold" ? Rand.RangeInclusive(6, 10) : d.defName == "Silver" ? Rand.RangeInclusive(40, 80) : Rand.RangeInclusive(30, 50);
                    return (d, n);
                }
                case "Morel":
                {
                    var d = Thing("RawFungus");
                    return d == null ? ((ThingDef, int)?)null : (d, Rand.RangeInclusive(15, 30));
                }
                case "Pip":
                {
                    if (Age < TechLevel.Industrial || !Machines(map).Any()) return null;
                    var d = Age >= TechLevel.Spacer && Rand.Chance(0.4f) ? Thing("ComponentSpacer") : Thing("ComponentIndustrial");
                    return d == null ? ((ThingDef, int)?)null : (d, d.defName == "ComponentSpacer" ? 1 : Rand.RangeInclusive(2, 4));
                }
                case "Ducat":
                {
                    var d = Thing("Silver");
                    return d == null ? ((ThingDef, int)?)null : (d, Rand.RangeInclusive(30, 80));
                }
                case "Hush":
                {
                    // Something small where the word in the frost was written: a few pieces of jade, else a little silver.
                    var d = Rand.Chance(0.5f) ? Thing("Jade") : null;
                    d ??= Thing("Silver");
                    return d == null ? ((ThingDef, int)?)null : (d, d.defName == "Jade" ? Rand.RangeInclusive(2, 4) : Rand.RangeInclusive(15, 30));
                }
                case "Revel":
                {
                    // A few of the colony's own kind of drink or smoke, by its means and its age (Gods/OnMap/RevelGoods.cs).
                    var d = RevelGoods.Pick(map, false);
                    int n = OwnHappeningsMath.StashCount(d.BaseMarketValue);
                    return n <= 0 ? ((ThingDef, int)?)null : (d, n);
                }
                default:
                    return null;
            }
        }

        internal static string Leave(TouchContext c, (ThingDef def, int count)? what)
        {
            if (what?.def == null || what.Value.count <= 0) return null;
            var t = ThingMaker.MakeThing(what.Value.def);
            t.stackCount = Math.Min(what.Value.count, what.Value.def.stackLimit);
            if (!GenPlace.TryPlaceThing(t, c.cell, c.map, ThingPlaceMode.Near, out var placed) || placed == null) return null;
            GodTouchFx.Show(c.god, c.map, placed.Position, c.tier, false);
            return Words(c, K(c.tier == TouchTier.Great ? "Hoard_Text" : "Find"), placed.LabelCap.Named("WHAT"));
        }

        internal static IEnumerable<Plant> Crops(Map map, IntVec3 at, float radius)
        {
            foreach (var z in map.zoneManager.AllZones.OfType<Zone_Growing>())
            foreach (var cell in z.cells)
            {
                if (at.IsValid && radius > 0f && !cell.InHorDistOf(at, radius)) continue;
                var p = cell.GetPlant(map);
                if (p != null && p.def.plant != null && p.LifeStage != PlantLifeStage.Sowing && p.def == z.GetPlantDefToGrow()) yield return p;
            }
        }

        private static int Ripen(TouchContext c, IntVec3 at, float radius, float by)
        {
            int n = 0;
            foreach (var p in Crops(c.map, at, radius).Where(x => x.Growth < 0.95f).ToList())
            {
                p.Growth = Mathf.Min(1f, p.Growth + by);
                p.DirtyMapMesh(p.Map);
                if (n % 6 == 0) GodTouchFx.Show(c.god, c.map, p.Position, TouchTier.Small, false);
                n++;
            }
            return n;
        }

        private static IEnumerable<PawnKindDef> GameKinds(Map map)
        {
            var biome = map.Biome;
            if (biome == null) return Enumerable.Empty<PawnKindDef>();
            return BiomeAnimals.Of(biome).Where(k => k?.RaceProps != null && !k.RaceProps.predator && k.RaceProps.baseBodySize >= 0.6f && k.RaceProps.baseBodySize <= 3f
                                                     && map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race));
        }

        private static string Game(TouchContext c)
        {
            var biome = c.map.Biome;
            var kind = GameKinds(c.map).RandomElementByWeightWithFallback(k => BiomeAnimals.Commonality(biome, k));
            if (kind == null || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, c.map, CellFinder.EdgeRoadChance_Animal)) return null;
            int n = Rand.RangeInclusive(3, 5);
            Pawn first = null;
            for (int i = 0; i < n; i++)
            {
                var a = PawnGenerator.GeneratePawn(kind);
                GenSpawn.Spawn(a, CellFinder.RandomClosewalkCellNear(entry, c.map, 4), c.map);
                first ??= a;
            }
            Show(c, first.Position, K("Game"), kind.GetLabelPlural().Named("ANIMALS"));
            return Shown;
        }

        internal static IEnumerable<Pawn> WildGame(Map map) =>
            map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == null && p.RaceProps?.Animal == true && !p.Dead && !p.Downed && !p.InMentalState && !p.RaceProps.predator
                                                    && !GodAvatar.Is(p) && p.mindState != null && p.mindState.exitMapAfterTick < 0);

        private static string GameLeaves(TouchContext c)
        {
            int n = 0;
            foreach (var p in WildGame(c.map).InRandomOrder().Take(6).ToList())
            {
                p.mindState.exitMapAfterTick = Now + Rand.Range(60, 2500);
                n++;
            }
            return n == 0 ? null : Words(c, K("GameLeaves"));
        }

        /// <summary>Natural ground (not a floor, not water) turned rich soil, <paramref name="most"/> cells at most. True when any was.</summary>
        internal static bool Soil(TouchContext c, IEnumerable<IntVec3> cells, int most)
        {
            var rich = DefDatabase<TerrainDef>.GetNamedSilentFail("SoilRich");
            if (rich == null) return false;
            int n = 0;
            foreach (var cell in cells.Where(x => x.InBounds(c.map)).ToList())
            {
                if (n >= most) break;
                var t = cell.GetTerrain(c.map);
                if (t == null || !t.natural || t.IsWater || t == rich || t.fertility <= 0.1f || t.fertility >= rich.fertility) continue;
                c.map.terrainGrid.SetTerrain(cell, rich);
                if (n % 5 == 0) GodTouchFx.Show(c.god, c.map, cell, TouchTier.Small, false);
                n++;
            }
            return n > 0;
        }

        private static IEnumerable<IntVec3> GrowingCells(Map map, IntVec3 at) =>
            map.zoneManager.AllZones.OfType<Zone_Growing>().Where(z => !at.IsValid || z.Position.InHorDistOf(at, 40f)).SelectMany(z => z.cells)
                .Where(x => x.GetTerrain(map) is TerrainDef t && t.natural && !t.IsWater && t.defName != "SoilRich" && t.fertility > 0.1f && t.fertility < 1.4f);

        private static bool Fertile(TouchContext c)
        {
            var cells = GrowingCells(c.map, c.cell).ToList();
            var first = cells.FirstOrDefault();
            if (cells.Count == 0 || !Soil(c, cells, 80)) return false;
            return Great(c, first, "Fertile");
        }

        internal static bool CanIncident(Map map, string name)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(name);
            if (def?.Worker == null) return false;
            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            parms.forced = true;
            return def.Worker.CanFireNow(parms);
        }

        /// <summary>The game's own incident, with the game's own letter.</summary>
        internal static bool Incident(TouchContext c, string name)
        {
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(name);
            if (def?.Worker == null) return false;
            var parms = StorytellerUtility.DefaultParmsNow(def.category, c.map);
            parms.forced = true;
            return def.Worker.CanFireNow(parms) && def.Worker.TryExecute(parms);
        }

        internal static bool Comms(Map map) => map.listerBuildings.allBuildingsColonist.Any(b => b is Building_CommsConsole && b.GetComp<CompPowerTrader>()?.PowerOn != false);

        private static string Trader(TouchContext c)
        {
            if (c.god.Key == "Ducat" && Comms(c.map) && Incident(c, "OrbitalTraderArrival")) return K("TraderOrbit");
            return Incident(c, "TraderCaravanArrival") ? K("Trader") : null;
        }

        private static IEnumerable<Faction> Friends(Map map) =>
            Verse.Find.FactionManager.AllFactionsVisible.Where(f => f != null && !f.IsPlayer && !f.defeated && !f.Hidden && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer)
                                                               && f.PlayerGoodwill < 90);

        /// <summary>A people at peace thinks better of the colony: the game's goodwill and its own message.</summary>
        public const int GoodwillUp = 6;

        private static string Goodwill(TouchContext c)
        {
            var f = Friends(c.map).RandomElementWithFallback();
            if (f == null) return null;
            var heard = DefDatabase<HistoryEventDef>.GetNamedSilentFail("Rimfolk_GodTouch_Heard");
            if (!f.TryAffectGoodwillWith(Faction.OfPlayer, GoodwillUp, true, false, heard)) return null;
            return Words(c, K("Goodwill"), f.Name.Named("FACTION"));
        }

        private static bool Night(Map map)
        {
            int h = GenLocalDate.HourOfDay(map);
            return h >= 19 || h < 4;
        }

        private static string Aurora(TouchContext c)
        {
            var def = DefDatabase<GameConditionDef>.GetNamedSilentFail("Aurora");
            if (def == null || c.map.gameConditionManager.ConditionIsActive(def)) return null;
            c.map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(def, GenDate.TicksPerHour * 6));
            return Words(c, K("Aurora"));
        }

        private static IEnumerable<Thing> Food(Map map) =>
            map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Where(t => t.Spawned && t.IsInAnyStorage() && t.TryGetComp<CompRottable>() != null);

        /// <summary>The stores kept (their spoiling set back two days) or going off (a day and a half on, never to rotten).</summary>
        private static string Food(TouchContext c, bool keep)
        {
            int n = 0;
            Thing first = null;
            foreach (var t in Food(c.map).InRandomOrder().Take(keep ? 30 : 3).ToList())
            {
                var r = t.TryGetComp<CompRottable>();
                if (r == null) continue;
                float day = GenDate.TicksPerDay;
                if (keep) r.RotProgress = Mathf.Max(0f, r.RotProgress - 2f * day);
                else r.RotProgress = Mathf.Min(r.PropsRot.TicksToRotStart * 0.85f, r.RotProgress + 1.5f * day);
                first ??= t;
                n++;
            }
            if (n == 0) return null;
            Show(c, first.Position, K(keep ? "Preserve" : "Spoil"));
            return Shown;
        }

        private static GameCondition ColdSnap(Map map) => map.gameConditionManager.GetActiveCondition(GameConditionDefOf.ColdSnap);

        private static string ColdBreak(TouchContext c)
        {
            var snap = ColdSnap(c.map);
            if (snap == null) return Food(c, true);
            snap.End();
            return Words(c, K("ColdBreak"));
        }

        private static IEnumerable<Pawn> Wounded(Map map, TouchContext c) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => Worship.Ours(p) && p.health.hediffSet.hediffs.Any(h => h is Hediff_Injury i && !i.IsPermanent()))
                .OrderByDescending(p => c.saw.Contains(p) ? 1 : 0).ThenByDescending(p => p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(i => i.Severity));

        /// <summary>The worst wounded of the home heal by four tenths in the night.</summary>
        private static string Heal(TouchContext c)
        {
            var p = Wounded(c.map, c).FirstOrDefault();
            if (p == null) return null;
            foreach (var i in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x => !x.IsPermanent()).ToList())
                i.Heal(i.Severity * 0.4f);
            GodTouchFx.Show(c.god, c.map, p.Position, c.tier, false);
            return Words(c, K("Heal"), p.LabelShort.Named("WHO"));
        }

        private static string CatComes(TouchContext c)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Cat");
            if (kind == null || !RCellFinder.TryFindRandomPawnEntryCell(out var entry, c.map, CellFinder.EdgeRoadChance_Animal)) return null;
            var cat = PawnGenerator.GeneratePawn(kind, Faction.OfPlayer);
            GenSpawn.Spawn(cat, entry, c.map);
            Show(c, entry, K("CatComes"), cat.LabelIndefinite().Named("CAT"));
            return Shown;
        }

        /// <summary>Odyssey's fish, by name.</summary>
        private static IEnumerable<ThingDef> Fishes()
        {
            foreach (var n in new[] { "Fish_Bass", "Fish_Catfish", "Fish_Cod", "Fish_Flounder", "Fish_Bluefish" })
            {
                var d = Thing(n);
                if (d != null) yield return d;
            }
        }

        /// <summary>Water near the place, or near the colony.</summary>
        private static IntVec3 Water(Map map, IntVec3 at)
        {
            foreach (var c in GenRadial.RadialCellsAround(at.IsValid ? at : map.Center, 30f, true))
                if (c.InBounds(map) && c.GetTerrain(map)?.IsWater == true && !c.Fogged(map))
                    return c;
            return IntVec3.Invalid;
        }

        private static string Fish(TouchContext c)
        {
            var w = Water(c.map, c.cell);
            var d = Fishes().RandomElementWithFallback();
            if (!w.IsValid || d == null) return null;
            var bank = CellFinder.StandableCellNear(w, c.map, 4f);
            if (!bank.IsValid) return null;
            var t = ThingMaker.MakeThing(d);
            t.stackCount = Math.Min(d.stackLimit, Rand.RangeInclusive(8, 16));
            if (!GenPlace.TryPlaceThing(t, bank, c.map, ThingPlaceMode.Near, out var placed) || placed == null) return null;
            Show(c, placed.Position, K("Fish"), placed.LabelCap.Named("WHAT"));
            return Shown;
        }

        private static string Breakdown(TouchContext c)
        {
            var m = Machines(c.map).Where(b => !b.GetComp<CompBreakdownable>().BrokenDown).OrderBy(b => b.Position.DistanceToSquared(c.cell)).FirstOrDefault();
            if (m == null) return null;
            m.GetComp<CompBreakdownable>().DoBreakdown();
            Show(c, m.Position, K("Breakdown"), m.LabelNoCount.Named("THING"));
            return Shown;
        }

        /// <summary>The stacks something might go missing from: silver for Ducat, any good stack in the stores for the Hunger.</summary>
        private static IEnumerable<Thing> VanishStack(Map map, God g)
        {
            var silver = ThingDefOf.Silver;
            return map.listerThings.AllThings.Where(t => t.Spawned && t.IsInAnyStorage() && t.def.category == ThingCategory.Item && t.stackCount >= 5
                                                         && (g?.Key == "Ducat" ? t.def == silver : t.def.BaseMarketValue >= 1f));
        }

        private static string Vanish(TouchContext c)
        {
            var t = VanishStack(c.map, c.god).RandomElementWithFallback();
            if (t == null) return null;
            int n = GodTouchMath.VanishCount(t.stackCount);
            if (n <= 0) return null;
            var pos = t.Position;
            string label = t.def.label;
            t.SplitOff(n).Destroy(DestroyMode.Vanish);
            Show(c, pos, K("Vanish"), n.Named("COUNT"), label.Named("WHAT"));
            return Shown;
        }

        /// <summary>Those who saw it come away in high spirits (Revel): their recreation filled, a laugh at nothing.</summary>
        private static string Merry(TouchContext c)
        {
            var took = c.saw.Where(p => p.needs?.joy != null && p.needs.joy.CurLevelPercentage < 0.95f).Take(GodTouchMath.BlessAtMost).ToList();
            if (took.Count == 0) return null;
            foreach (var p in took) p.needs.joy.CurLevel = p.needs.joy.MaxLevel;
            return Words(c, K("Merry"), Names(took).Named("WHO"));
        }

        /// <summary>A drink of the colony's in the stores (Revel's anger turns some of it sour), the largest stack first.</summary>
        private static Thing SourStack(Map map) => RevelGoods.Held(map).Where(t => RevelGoods.Drink(t.def) && t.stackCount >= 2).OrderByDescending(t => t.stackCount).FirstOrDefault();

        /// <summary>Some of the drink in the stores turns sour and spills across the floor, never all of it.</summary>
        private static string Sour(TouchContext c)
        {
            var t = SourStack(c.map);
            if (t == null) return null;
            int n = GodTouchMath.VanishCount(t.stackCount);
            if (n <= 0) return null;
            var pos = t.Position;
            string label = t.def.label;
            t.SplitOff(n).Destroy(DestroyMode.Vanish);
            if (ThingDefOf.Filth_Water != null)
                foreach (var cell in GenRadial.RadialCellsAround(pos, 1.5f, true).Where(x => x.InBounds(c.map) && x.Standable(c.map)).Take(3))
                    FilthMaker.TryMakeFilth(cell, c.map, ThingDefOf.Filth_Water);
            Show(c, pos, K("Sour"), n.Named("COUNT"), label.Named("WHAT"));
            return Shown;
        }

        // ================= Great =================

        /// <summary>
        /// The god's own presence stays on the land for days (its condition, GodDef.presence, after its moment): Hollin's
        /// green, Smoulder's warm stone, Bellwether's calm, Ennu's warm evenings, Pip's hum, Cirrus's high sky, Selene's moon,
        /// Tamiu's watch, Murmur's rain, Ducat's glint; the Hunger's dark two days.
        /// </summary>
        private static bool Linger(TouchContext c)
        {
            int ticks = Mathf.RoundToInt(GodTouchMath.LingerDays(c.god.Key) * GenDate.TicksPerDay);
            string key = c.dark ? "Dim" : "Linger";
            return Great(c, c.cell, key, Words(c, K(key + "_" + c.god.Key)), ticks);
        }

        private static IntVec3 Vein(TouchContext c)
        {
            var gold = Thing("MineableGold");
            if (gold == null) return IntVec3.Invalid;
            var at = OwnSmoulder.Seam(c.map, c.cell, gold, 5);
            if (!at.IsValid)
            {
                var centre = CampSite.ColonyCenter(c.map);
                if (centre.IsValid) at = OwnSmoulder.Seam(c.map, centre, gold, 5);
            }
            return at;
        }

        private static bool SunYear(TouchContext c)
        {
            if (Ripen(c, IntVec3.Invalid, 0f, GodTouchMath.SunYearGrowth) == 0) return false;
            return Everyone(c, GodTouchDefOf.Rimfolk_GodTouch_SunWarmed);
        }

        private static IEnumerable<(Pawn pawn, Hediff scar)> Scarred(Map map, TouchContext c) =>
            map.mapPawns.FreeColonistsSpawned.Where(Worship.Ours)
                .Select(p => (p, (Hediff)p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i => i.IsPermanent()).OrderByDescending(i => i.Severity).FirstOrDefault()))
                .Where(x => x.Item2 != null).OrderByDescending(x => c.saw.Contains(x.p) ? 1 : 0);

        private static bool ScarGone(TouchContext c)
        {
            var (p, scar) = Scarred(c.map, c).FirstOrDefault();
            if (p == null || scar == null) return false;
            string what = scar.Part != null ? scar.Part.LabelShort : scar.LabelBase;
            p.health.RemoveHediff(scar);
            return Great(c, p.Position, "ScarGone", Words(c, K("ScarGone_Text"), p.LabelShort.Named("WHO"), what.Named("PART")));
        }

        private static bool Ward(TouchContext c)
        {
            var p = c.saw.Where(x => x.health != null).OrderByDescending(x => c.taken != null ? PawnGods.HoldOf(x, c.taken) : 0f).FirstOrDefault();
            if (p == null || Give(new[] { p }, GodTouchDefOf.Rimfolk_GodTouch_Ward, 1).Count == 0) return false;
            return Great(c, p.Position, "Ward", Words(c, K("Ward_Text"), p.LabelShort.Named("WHO")));
        }

        /// <summary>A spring wells up where it happened: shallow water on open natural ground, away from the colony's buildings and fields.</summary>
        private static IntVec3 Spring(TouchContext c)
        {
            var map = c.map;
            var water = TerrainDefOf.WaterShallow;
            bool Open(IntVec3 x)
            {
                if (!x.InBounds(map) || x.Fogged(map) || x.Roofed(map) || map.areaManager.Home[x] || x.GetZone(map) != null) return false;
                var t = x.GetTerrain(map);
                if (t == null || !t.natural || t.IsWater) return false;
                return x.GetThingList(map).All(th => th.def.category == ThingCategory.Plant || th.def.category == ThingCategory.Filth);
            }
            var centre = GenRadial.RadialCellsAround(c.cell, 20f, true).Where(x => x.InBounds(map) && GenRadial.RadialCellsAround(x, 1.5f, true).All(Open))
                .OrderBy(x => x.DistanceToSquared(c.cell)).FirstOrDefault();
            if (!centre.IsValid || centre == default) return IntVec3.Invalid;
            foreach (var x in GenRadial.RadialCellsAround(centre, 1.5f, true).ToList())
            {
                foreach (var plant in x.GetThingList(map).Where(th => th.def.category == ThingCategory.Plant).ToList()) plant.Destroy(DestroyMode.Vanish);
                map.terrainGrid.SetTerrain(x, water);
                FleckMaker.WaterSplash(x.ToVector3Shifted(), map, 2f, 2f);
            }
            return centre;
        }
    }
}
