using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Smoulder, the Coil Under the Mountain, on the map (docs/gods-next-proposals.md, C 2): rock, ore, heat under the
    /// mountain and the miners' dreams, his alone.
    /// <list type="bullet">
    /// <item>The bargain at the shaft (his lore: bargains in dreams): a miner wakes and tells of a warm, reasonable voice
    /// offering silver down the deepest shaft for gold back; the colony answers (Rimfolk_Evt_GodBargain). Agreed, they carry
    /// the silver to the deepest cell under the mountain and it goes down with a breath of heat; days later a vein shows in
    /// the rock by it: gold for a bargain kept to the letter, steel with a skin of gold for one bent (half kept back).</item>
    /// <item>His proper name: an artist who holds him (or knows of him) may name a new sculpture for Aurelian the Ninth; the
    /// next day a seam of silver glints in the rock face nearest their bench.</item>
    /// <item>The heat from below: on the turn before stone may first fall in the deep working (GodWaking), its floor grows hot
    /// for hours, a warning anyone down there can feel.</item>
    /// <item>The stretching dream: on the night after his winged shadow crossed the land at noon, every miner dreams the same
    /// dream of something stretching under the mountain.</item>
    /// <item>Silver kept from the deep: a hoard long unpaid, or a bargain bent, and a miner wakes from a warm dream with a
    /// burn across the hand (the shared harm budget, HarmKinds.GodBurn) and says the voice was not pleased.</item>
    /// </list>
    /// </summary>
    public static class OwnSmoulder
    {
        public const string Key = "Smoulder";
        public const string BargainIncident = "Rimfolk_Evt_GodBargain";
        public const string BurnThought = "Rimfolk_Thought_GodOwnBurn";
        public const string ProperName = "Aurelian the Ninth";

        private static God Him => OwnHappenings.God(Key);

        private static int Now => OwnHappenings.Now;

        /// <summary>The colony's silver in storage on a map.</summary>
        internal static int Silver(Map map) => map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned && t.IsInAnyStorage()).Sum(t => t.stackCount);

        /// <summary>Someone of the colony sleeps under the mountain's thick rock, or mines there.</summary>
        internal static bool UnderRock(Map map)
        {
            var thick = RoofDefOf.RoofRockThick;
            if (map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().Any(bd => bd.Spawned && bd.Position.GetRoof(map) == thick)) return true;
            return GodsOnMapBook.Instance?.deepWarnings > 0;
        }

        /// <summary>The colony's miners: Mining at the line, or a passion for it.</summary>
        internal static List<Pawn> Miners(Map map) =>
            map.mapPawns.FreeColonistsSpawned.Where(p => People.HasInnerLife(p) && p.skills?.GetSkill(SkillDefOf.Mining) is SkillRecord r && !r.TotallyDisabled
                                                       && (r.Level >= OwnHappeningsMath.MinerFrom || r.passion != Passion.None)).ToList();

        private static bool Woke(Pawn p) => p != null && CouncilGameComponent.Instance?.dreams?.WokeFromNight(p) == true;

        private static void Dream(Pawn p, string key, params NamedArgument[] args)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || p == null) return;
            GodOmens.Dreamt(comp, p, key.Translate(args).Resolve());
        }

        // ================= The day =================

        internal static void Daily(OwnHappeningsBook b, int now)
        {
            var g = Him;
            if (g == null) return;
            // A gift taken at his place lately (down a crack in the rock, his way) counts as the deep paid.
            var w = Worship.Book;
            if (w?.offerings != null && w.offerings.Any(o => o != null && o.god == g.id && o.fate == OfferingFate.Taken)) b.lastPaid = now;
            foreach (var map in OwnHappenings.HomeMaps)
            {
                if (!OwnHappenings.Acts(g, map) || !OwnHappenings.Quiet(map)) continue;
                int held = Silver(map);
                if (b.bargainStage == 0)
                {
                    var why = OwnHappeningsMath.BargainTrigger(held, UnderRock(map), GodNear.Stir(map, GodStir.DeepMine));
                    if (OwnHappenings.Due(b, OwnHappening.SmoulderBargain, g, map, why, now) && Offer(b, map, held, now)) return;
                }
                if (b.burnBy == null)
                {
                    var why = OwnHappeningsMath.BurnTrigger(held, GodPace.DaysSince(b.lastPaid, now), b.bent);
                    if (OwnHappenings.Due(b, OwnHappening.SmoulderBurn, g, map, why, now))
                    {
                        b.burnBy = Miners(map).Concat(map.mapPawns.FreeColonistsSpawned).FirstOrDefault(p => People.HasInnerLife(p) && !p.DevelopmentalStage.Juvenile());
                        if (b.burnBy != null) OwnHappenings.Came(b, OwnHappening.SmoulderBurn, now);
                    }
                }
            }
        }

        // ================= The bargain at the shaft =================

        /// <summary>The bargain offered: tonight the dreamer dreams it (told at breakfast, then the colony's answer).</summary>
        internal static bool Offer(OwnHappeningsBook b, Map map, int held, int now)
        {
            int want = OwnHappeningsMath.BargainSilver(held);
            var dreamer = Miners(map).OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Mining).Level).FirstOrDefault()
                          ?? map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => People.HasInnerLife(p) && !p.DevelopmentalStage.Juvenile());
            if (want <= 0 || dreamer == null) return false;
            b.bargainMap = map;
            b.dreamer = dreamer;
            b.promised = want;
            b.thrown = 0;
            b.bargainStage = 1;
            OwnHappenings.Came(b, OwnHappening.SmoulderBargain, now, null, dreamer.ThingID);
            return true;
        }

        /// <summary>The bargain's card, waiting for the colony's answer (read by its worker).</summary>
        internal static bool asking;

        /// <summary>At breakfast: the dream told, and the colony's answer asked (the letters' gap allowing).</summary>
        internal static bool Ask(OwnHappeningsBook b, bool anyway = false)
        {
            var map = b.bargainMap;
            var p = b.dreamer;
            if (map == null || p == null || p.Dead || !p.Spawned || p.Map != map)
            {
                b.bargainStage = 0;
                return false;
            }
            if (!anyway && (!Tell.CanAsk || !OwnHappenings.Quiet(map))) return false;
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(BargainIncident);
            if (!(def?.Worker is IncidentWorker_Happening w)) return false;
            Dream(p, "CC_GodO_Bargain_Dream", b.promised.Named("COUNT"));
            asking = true;
            bool asked;
            try
            {
                asked = w.ScenarioCanFire(map) && w.ScenarioFire(map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: the warm voice's bargain", e);
                asked = false;
            }
            finally
            {
                asking = false;
            }
            if (!asked) return false;
            b.bargainStage = 2;
            OwnHappenings.Saw(Him, map, p.Position, GodActKind.Omen, p, "CC_GodO_Saw_BargainDream", b.promised.Named("COUNT"));
            return true;
        }

        /// <summary>The colony's answer: all of it carried down (0), half (1), or none (2).</summary>
        internal static void Answered(Pawn by, int option)
        {
            var b = OwnHappenings.Book;
            var g = Him;
            if (b == null || b.bargainStage != 2) return;
            var map = b.bargainMap;
            if (option >= 2 || by == null || map == null)
            {
                b.bargainStage = 0;
                if (g != null && map != null) Gods.AddFavour(g, map, OwnHappeningsMath.RefusedFavour, null);
                OwnHappenings.Say(b.dreamer ?? by, "bargainrefused", g);
                return;
            }
            int want = option == 0 ? b.promised : b.promised / 2;
            var shaft = Shaft(map, by);
            var stack = map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer))
                .OrderByDescending(t => t.stackCount).FirstOrDefault();
            if (!shaft.IsValid || stack == null || !OwnHappenings.Carry(by, stack, shaft, want))
            {
                b.bargainStage = 0;
                Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_GodO_Bargain_NoWay".Translate(by.Named("PAWN")).Resolve(), new LookTargets(by), MessageTypeDefOf.NeutralEvent);
                return;
            }
            b.carrier = by;
            b.shaft = shaft;
            b.silver = stack;
            b.thrown = 0;
            b.bargainStage = 3;
            b.carryUntil = Now + GenDate.TicksPerHour * 8;
            b.Due(Now + 250);
            OwnHappenings.Say(by, "bargaincarry", g);
        }

        /// <summary>The deepest shaft: a cell under thick rock, as deep in the rock as the colony has dug, that the carrier can reach.</summary>
        internal static IntVec3 Shaft(Map map, Pawn by)
        {
            var thick = RoofDefOf.RoofRockThick;
            var centre = CampSite.ColonyCenter(map);
            IntVec3 best = IntVec3.Invalid;
            float most = -1f;
            for (int i = 0; i < 600; i++)
            {
                var c = CellFinder.RandomCell(map);
                if (c.GetRoof(map) != thick || !c.Standable(map) || c.Fogged(map) || c.GetFirstItem(map) != null || c.GetEdifice(map) != null) continue;
                if (by != null && !by.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                float deep = GenRadial.RadialCellsAround(c, 5f, true).Count(x => x.InBounds(map) && x.GetRoof(map) == thick);
                if (centre.IsValid) deep -= c.DistanceTo(centre) * 0.05f;
                if (deep > most)
                {
                    most = deep;
                    best = c;
                }
            }
            return best;
        }

        /// <summary>The silver's way down: set down at the shaft, it goes down with a breath of heat, and the vein's day is set.</summary>
        private static void CarryStep(OwnHappeningsBook b, int now)
        {
            if (b.bargainStage != 3) return;
            var map = b.bargainMap;
            var g = Him;
            if (map == null || g == null || now > b.carryUntil)
            {
                b.bargainStage = 0;
                return;
            }
            var down = b.shaft.IsValid ? b.shaft.GetThingList(map).FirstOrDefault(t => t.def == ThingDefOf.Silver) : null;
            if (down == null)
            {
                var c = b.carrier;
                // Set down a step short of the shaft (the cell taken): it goes from where it lies.
                if (c != null && c.Spawned && c.CurJobDef != JobDefOf.HaulToCell)
                    down = GenRadial.RadialCellsAround(b.shaft, 1.5f, true).Where(x => x.InBounds(map)).SelectMany(x => x.GetThingList(map)).FirstOrDefault(t => t.def == ThingDefOf.Silver && !t.IsInAnyStorage());
                if (down == null)
                {
                    b.Due(now + 250);
                    return;
                }
            }
            int n = down.stackCount;
            var at = down.Position;
            down.Destroy();
            b.thrown += n;
            try
            {
                FleckMaker.ThrowHeatGlow(at, map, 2f);
                for (int i = 0; i < 6; i++) FleckMaker.ThrowAirPuffUp(at.ToVector3Shifted() + new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.5f, 0.5f)), map);
                var rubble = DefDatabase<ThingDef>.GetNamedSilentFail("Filth_RubbleRock");
                if (rubble != null) FilthMaker.TryMakeFilth(at, map, rubble);
                GenTemperature.PushHeat(at, map, 25f);
                if (Find.CurrentMap == map) Find.CameraDriver.shaker.DoShake(0.25f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: silver down the shaft", e);
            }
            bool kept = OwnHappeningsMath.Kept(b.thrown, b.promised);
            Gods.AddFavour(g, map, kept ? OwnHappeningsMath.KeptFavour : OwnHappeningsMath.BentFavour, null, FavourSource.Offering);
            if (kept) b.lastPaid = now;
            b.bent = !kept;
            b.bargainStage = 4;
            b.veinAt = now + Mathf.RoundToInt(Rand.Range(OwnHappeningsMath.VeinDaysMin, OwnHappeningsMath.VeinDaysMax) * GenDate.TicksPerDay);
            var by = b.carrier ?? OwnHappenings.Nearest(map, at);
            OwnHappenings.Say(by, "bargaindown", g, null, n.Named("COUNT"));
            Tell.Message(NewsKind.Land, Importance.Notable, map, "CC_GodO_Bargain_Down".Translate((by?.LabelShort ?? "").Named("PAWN"), n.Named("COUNT")).Resolve(),
                new LookTargets(at, map), MessageTypeDefOf.NeutralEvent);
            OwnHappenings.Saw(g, map, at, GodActKind.Act, by, "CC_GodO_Saw_BargainDown", n.Named("COUNT"));
        }

        /// <summary>The vein's day: gold up through the rock beside the shaft for a bargain kept, steel with a skin of gold for one bent.</summary>
        internal static bool Vein(OwnHappeningsBook b, int now)
        {
            var map = b.bargainMap;
            var g = Him;
            b.bargainStage = 0;
            if (map == null || g == null || !b.shaft.IsValid) return false;
            bool kept = OwnHappeningsMath.Kept(b.thrown, b.promised);
            var gold = DefDatabase<ThingDef>.GetNamedSilentFail("MineableGold");
            var steel = DefDatabase<ThingDef>.GetNamedSilentFail("MineableSteel");
            var def = kept ? gold : steel;
            if (def?.building?.mineableThing == null) return false;
            float perCell = def.building.mineableYield * def.building.mineableThing.BaseMarketValue;
            int cells = kept ? OwnHappeningsMath.GoldCells(b.thrown, perCell) : OwnHappeningsMath.SteelCells(b.thrown, perCell);
            var first = Seam(map, b.shaft, def, cells);
            if (!first.IsValid) return false;
            OwnHappenings.Card(g, map, kept ? "Vein" : "VeinBent", true, new LookTargets(first, map), kept ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent,
                Importance.Notable, b.thrown.Named("COUNT"));
            var seer = OwnHappenings.Found(b, map, first, kept ? "vein" : "veinbent", g);
            OwnHappenings.Saw(g, map, first, kept ? GodActKind.Blessing : GodActKind.Wrath, seer, kept ? "CC_GodO_Saw_Vein" : "CC_GodO_Saw_VeinBent");
            GodMarks.Note(g, map, b.shaft, (kept ? "CC_GodO_Vein_Act" : "CC_GodO_VeinBent_Act").Translate().Resolve(), kept ? PresenceKind.Blessing : PresenceKind.Wrath, false);
            return true;
        }

        /// <summary>
        /// A seam of <paramref name="def"/> in the natural rock nearest <paramref name="near"/>: rock faces (rock beside open
        /// ground) first, glinting as it comes. Its first cell, or Invalid.
        /// </summary>
        internal static IntVec3 Seam(Map map, IntVec3 near, ThingDef def, int cells)
        {
            var face = GenRadial.RadialCellsAround(near, 14f, true).Where(c => c.InBounds(map) && c.GetEdifice(map) is Building e && e.def.building?.isNaturalRock == true
                                                                              && !e.def.building.isResourceRock && !c.Fogged(map)
                                                                              && GenAdj.CellsAdjacentCardinal(c, Rot4.North, IntVec2.One).Any(n => n.InBounds(map) && n.Standable(map)))
                .OrderBy(c => c.DistanceToSquared(near)).FirstOrDefault();
            if (!face.IsValid || face == default) return IntVec3.Invalid;
            int made = 0;
            foreach (var c in GenRadial.RadialCellsAround(face, 3f, true).Where(c => c.InBounds(map) && c.GetEdifice(map) is Building e && e.def.building?.isNaturalRock == true
                                                                                     && !e.def.building.isResourceRock).OrderBy(c => c.DistanceToSquared(face)))
            {
                if (made >= cells) break;
                try
                {
                    var rock = c.GetEdifice(map);
                    rock.Destroy(DestroyMode.Vanish);
                    GenSpawn.Spawn(ThingMaker.MakeThing(def), c, map);
                    OwnHappenings.Motes(map, c.ToVector3Shifted(), OwnHappenings.Heat, new Color(1f, 0.85f, 0.35f), 2, 0.4f);
                    made++;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods: a seam in the rock", e);
                    break;
                }
            }
            return made > 0 ? face : IntVec3.Invalid;
        }

        // ================= His proper name =================

        private static readonly AccessTools.FieldRef<CompArt, TaggedString> Title = AccessTools.FieldRefAccess<CompArt, TaggedString>("titleInt");

        /// <summary>A work of art just made (Patch_OwnArtMade): its artist may name it for him.</summary>
        internal static void ArtMade(CompArt art, Pawn by)
        {
            var b = OwnHappenings.Book;
            var g = Him;
            var map = by.MapHeld;
            if (b == null || g == null || art?.parent == null || !art.parent.def.IsArt || map == null || !Homes.IsHome(map) || b.nameDue >= 0 || !OwnHappenings.Acts(g, map)) return;
            int now = Now;
            bool holds = Worship.GodOf(by) == g;
            bool good = art.parent.TryGetComp<CompQuality>()?.Quality >= QualityCategory.Good;
            if (OwnHappenings.forced != OwnHappening.SmoulderName
                && (!OwnHappeningsMath.Free(OwnHappening.SmoulderName, OwnHappenings.DaysSince(b, OwnHappening.SmoulderName, now))
                    || !Rand.Chance(OwnHappeningsMath.NameChance(holds, Gods.Known(g), good, GodNear.Of(g, map)))))
                return;
            Name(b, g, map, art, by, now);
        }

        /// <summary>The sculpture named for him, said aloud and kept; the seam the next day.</summary>
        internal static bool Name(OwnHappeningsBook b, God g, Map map, CompArt art, Pawn by, int now)
        {
            try
            {
                if (art != null) Title(art) = ProperName;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a work named for the sleeper", e);
                return false;
            }
            b.nameMap = map;
            b.nameAt = by.PositionHeld;
            b.nameBy = by;
            b.nameDue = now + Mathf.RoundToInt(Rand.Range(18f, 30f) * GenDate.TicksPerHour);
            OwnHappenings.Came(b, OwnHappening.SmoulderName, now);
            OwnHappenings.Say(by, "name", g, null, ProperName.Named("NAME"));
            OwnHappenings.Journal(by, "CC_GodO_Journal_Name", by.Named("PAWN"), ProperName.Named("NAME"));
            OwnHappenings.Saw(g, map, by.PositionHeld, GodActKind.Act, by, "CC_GodO_Saw_Name", ProperName.Named("NAME"));
            return true;
        }

        /// <summary>The next day: a seam of silver in the rock face nearest the artist's bench.</summary>
        private static void NameSeam(OwnHappeningsBook b, int now)
        {
            if (b.nameDue < 0 || now < b.nameDue) return;
            var map = b.nameMap;
            var g = Him;
            b.nameDue = -1;
            var silver = DefDatabase<ThingDef>.GetNamedSilentFail("MineableSilver");
            if (map == null || g == null || silver == null || !Homes.IsHome(map)) return;
            var at = Seam(map, b.nameAt, silver, OwnHappeningsMath.NameSeamCells);
            if (!at.IsValid) return;
            string who = b.nameBy?.LabelShort ?? "";
            OwnHappenings.Card(g, map, "Seam", false, new LookTargets(at, map), null, Importance.Notable, who.Named("PAWN"));
            var seer = OwnHappenings.Found(b, map, at, "seam", g);
            OwnHappenings.Saw(g, map, at, GodActKind.Blessing, seer, "CC_GodO_Saw_Seam", who.Named("ARTIST"));
        }

        // ================= The heat from below =================

        /// <summary>The rock has turned over a miner (GodWaking.Turned): on the turn before stone may first fall, the floor grows hot for hours.</summary>
        public static void Turned(God god, Map map, IntVec3 at, Pawn miner, int warnings)
        {
            var b = OwnHappenings.Book;
            if (b == null || god == null || god.Key != Key || map == null || !at.IsValid) return;
            if (OwnHappenings.forced != OwnHappening.SmoulderHeat && !OwnHappeningsMath.HeatFirst(warnings, GodsOnMapMath.StoneFallsFrom)) return;
            Heat(b, god, map, at, miner, Now);
        }

        internal static void Heat(OwnHappeningsBook b, God g, Map map, IntVec3 at, Pawn miner, int now)
        {
            b.heatMap = map;
            b.heatAt = at;
            b.heatUntil = now + OwnHappeningsMath.HeatHours * GenDate.TicksPerHour;
            b.Due(now + 60);
            OwnHappenings.Came(b, OwnHappening.SmoulderHeat, now);
            OwnHappenings.Card(g, map, "Heat", false, new LookTargets(at, map));
            OwnHappenings.Say(miner ?? OwnHappenings.Witness(map, at, 10f), "heat", g);
            OwnHappenings.Saw(g, map, at, GodActKind.Omen, miner, "CC_GodO_Saw_Heat");
        }

        /// <summary>Each half hour of it: heat pushed up through the floor, the air shimmering over it.</summary>
        private static int nextHeat;

        private static void HeatStep(OwnHappeningsBook b, int now)
        {
            if (b.heatUntil < 0) return;
            var map = b.heatMap;
            if (map == null || now > b.heatUntil || !Homes.IsHome(map))
            {
                b.heatUntil = -1;
                return;
            }
            if (now >= nextHeat)
            {
                nextHeat = now + GenDate.TicksPerHour / 2;
                GenTemperature.PushHeat(b.heatAt, map, OwnHappeningsMath.HeatPush);
                foreach (var c in GenRadial.RadialCellsAround(b.heatAt, 3f, true).Where(c => c.InBounds(map) && c.Standable(map)).InRandomOrder().Take(6))
                {
                    FleckMaker.ThrowHeatGlow(c, map, Rand.Range(0.6f, 1.1f));
                    if (Rand.Chance(0.4f)) FleckMaker.ThrowAirPuffUp(c.ToVector3Shifted(), map);
                }
            }
            b.Due(Math.Min(nextHeat, b.heatUntil + 1));
        }

        // ================= The stretching dream =================

        /// <summary>His shadow crossed the land at noon (GodOmens.Seen): that night the miners dream.</summary>
        public static void Shadow(Map map)
        {
            var b = OwnHappenings.Book;
            int now = Now;
            if (b == null || map == null || OwnHappenings.forced != OwnHappening.SmoulderStretch
                && !OwnHappeningsMath.Free(OwnHappening.SmoulderStretch, OwnHappenings.DaysSince(b, OwnHappening.SmoulderStretch, now)))
                return;
            b.stretchMap = map;
            b.stretchAt = now;
        }

        /// <summary>Every miner who woke from the night has the same dream; one says so, and the colony's book keeps it.</summary>
        internal static bool Stretch(OwnHappeningsBook b, Map map, int now, bool anyway = false)
        {
            var g = Him;
            var miners = Miners(map).Where(p => anyway || Woke(p)).ToList();
            if (g == null || miners.Count == 0) return false;
            b.stretchAt = -1;
            foreach (var p in miners) Dream(p, "CC_GodO_Stretch_Dream");
            OwnHappenings.Came(b, OwnHappening.SmoulderStretch, now);
            var first = miners[0];
            Tell.Message(NewsKind.People, Importance.Notable, map, (miners.Count > 1 ? "CC_GodO_Stretch_Text" : "CC_GodO_Stretch_TextOne").Translate(first.Named("PAWN"), miners.Count.Named("COUNT"))
                .Resolve(), new LookTargets(miners), MessageTypeDefOf.NeutralEvent);
            OwnHappenings.Say(first, "stretch", g);
            OwnHappenings.Saw(g, map, first.Position, GodActKind.Omen, first, "CC_GodO_Saw_Stretch", miners.Count.Named("COUNT"));
            return true;
        }

        // ================= Silver kept from the deep =================

        /// <summary>At dawn: the one chosen wakes from a warm dream, with a burn across the hand when the shared budget allows it.</summary>
        internal static bool Burn(OwnHappeningsBook b, Pawn p, int now, bool anyway = false)
        {
            var g = Him;
            if (g == null || p == null || !p.Spawned || p.Dead) return false;
            if (!anyway && !Woke(p)) return false;
            b.burnBy = null;
            var map = p.Map;
            Dream(p, "CC_GodO_Burn_Dream");
            bool burned = false;
            try
            {
                burned = Harm.Roll(p, HarmKinds.GodBurn, OwnHappeningsMath.BurnChance)
                         && Harm.Injure(p, HurtKind.Burn, HurtWhere.Hand, OwnHappeningsMath.BurnLow, OwnHappeningsMath.BurnHigh, HarmKinds.GodBurn) != null;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods: a warm dream's burn", e);
            }
            b.bent = false;
            if (burned)
            {
                OwnHappenings.Memory(p, BurnThought);
                FleckMaker.ThrowHeatGlow(p.Position, map, 0.8f);
            }
            OwnHappenings.Card(g, map, burned ? "Burn" : "BurnDream", burned, new LookTargets(p), burned ? LetterDefOf.NegativeEvent : null, Importance.Notable, p.Named("PAWN"));
            OwnHappenings.Say(p, burned ? "burn" : "burndream", g);
            OwnHappenings.Journal(p, burned ? "CC_GodO_Journal_Burn" : "CC_GodO_Journal_BurnDream", p.Named("PAWN"));
            OwnHappenings.Saw(g, map, p.Position, burned ? GodActKind.Wrath : GodActKind.Omen, p, burned ? "CC_GodO_Saw_Burn" : "CC_GodO_Saw_BurnDream");
            return true;
        }

        // ================= The hour and the steps =================

        internal static void Hourly(OwnHappeningsBook b, int now)
        {
            // The bargain's dream at breakfast.
            if (b.bargainStage == 1 && b.bargainMap != null)
            {
                int h = OwnHappenings.Hour(b.bargainMap);
                if (h >= 6 && h <= 10 && (Woke(b.dreamer) || h >= 9)) Ask(b);
                else if (h > 12 && b.dreamer != null && !Woke(b.dreamer) && now - b.Last(OwnHappening.SmoulderBargain) > GenDate.TicksPerDay * 2) b.bargainStage = 0;
            }
            // The answer never came (the card let go): the bargain lapses.
            if (b.bargainStage == 2 && now - b.Last(OwnHappening.SmoulderBargain) > GenDate.TicksPerDay * 4) b.bargainStage = 0;
            if (b.bargainStage == 4 && now >= b.veinAt) Vein(b, now);
            NameSeam(b, now);
            if (b.stretchAt >= 0)
            {
                var map = b.stretchMap;
                if (map == null || now - b.stretchAt > GenDate.TicksPerDay * 2) b.stretchAt = -1;
                else if (now - b.stretchAt > GenDate.TicksPerHour * 8 && OwnHappenings.Hour(map) >= 5 && OwnHappenings.Hour(map) <= 9) Stretch(b, map, now);
            }
            if (b.burnBy != null)
            {
                var p = b.burnBy;
                if (p.Dead || !p.Spawned || now - b.Last(OwnHappening.SmoulderBurn) > GenDate.TicksPerDay * 2) b.burnBy = null;
                else if (OwnHappenings.Hour(p.Map) >= 5 && OwnHappenings.Hour(p.Map) <= 9) Burn(b, p, now);
            }
        }

        internal static void Step(OwnHappeningsBook b, int now)
        {
            CarryStep(b, now);
            HeatStep(b, now);
        }
    }

    /// <summary>
    /// The warm voice's bargain (Rimfolk_Evt_GodBargain): fired by Smoulder's own (OwnSmoulder.Ask), never by the
    /// storyteller. A is the one who dreamt it; the answer is played out by the colony's own on the map (OwnSmoulder.Answered):
    /// A carries the silver to the deepest shaft, all of it or half, or nobody carries anything.
    /// </summary>
    public class HappeningWorker_GodBargain : IncidentWorker_Happening
    {
        protected override bool CanHappen(Map map, HappeningExtension ext) => OwnSmoulder.asking && OwnHappeningsBook.Instance?.bargainMap == map;

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            var b = OwnHappeningsBook.Instance;
            if (b?.dreamer == null || !b.dreamer.Spawned) return false;
            ctx.a = b.dreamer;
            ctx.tale = b.promised.ToString();
            ctx.spot = b.dreamer.Position;
            return true;
        }

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags) => tags.Add("age" + Mathf.Clamp(GodStranger.Age, 0, 3));

        protected override LookTargets Look(HappeningContext ctx) => ctx.a != null && ctx.a.Spawned ? new LookTargets(ctx.a) : LookTargets.Invalid;

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            OwnSmoulder.Answered(ctx.a, option);
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }
}
