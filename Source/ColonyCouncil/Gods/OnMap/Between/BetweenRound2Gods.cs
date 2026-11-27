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
    /// The guest at the burying (OwnHappening.EnnuBurying, D4): a day or two after one of the colony is laid in a grave, in
    /// the afternoon, a stranger of a people at peace walks in from the edge and stands at the grave, where the two who loved
    /// the dead most come too. The stranger says one true thing about the dead that nobody at the colony knew (where they grew
    /// up, from their own past), and leaves before dark, never coming to the gate. In the morning someone finds a small thing
    /// on the grave: dried herbs for a colony of the early ages, a coin for a later one. The Guest in disguise, with the small
    /// tells of its look (GodAvatar); named on the card only where the colony knows it. Never while another of the gods'
    /// strangers is at the home.
    /// </summary>
    public class EnnuBurying : BetweenKind
    {
        public override OwnHappening H => OwnHappening.EnnuBurying;

        /// <summary>A grave of one of the colony dead lately, not yet answered: the grave, the dead, its key.</summary>
        public static (Building_Grave grave, Pawn dead, string key) Fresh(OwnHappeningsBook b, Map map)
        {
            foreach (var gr in map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>())
            {
                var c = gr.Corpse;
                var dead = c?.InnerPawn;
                if (dead == null || dead.Faction != Faction.OfPlayer || dead.RaceProps?.Humanlike != true || c.Age > OwnHappeningsMath.BuryingDays * GenDate.TicksPerDay) continue;
                string key = "burying|" + dead.thingIDNumber;
                if (!b.between.answered.Contains(key)) return (gr, dead, key);
            }
            return (null, null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.BuryingFrom || hour >= OwnHappeningsMath.BuryingTo) return;
            var (grave, dead, key) = Fresh(b, map);
            if (!OwnHappeningsMath.BuryingMay(grave != null, GodsOnMapBook.Instance?.stranger != null, Arrivals.Friendly() != null)) return;
            b.between.answered.Add(key);
            b.between.Trim();
            if (!OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.BuryingChance)) return;
            Begin(b, g, map, grave, dead);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Building_Grave grave, Pawn dead)
        {
            var x = GodStranger.Make(g, map, out var f);
            if (x == null) return false;
            var spot = OwnHappenings.Beside(map, grave.Position, null);
            if (!spot.IsValid || Arrivals.Visit(new List<Pawn> { x }, map, f, spot, OwnHappeningsMath.BuryingHours) == null)
            {
                Arrivals.Discard(x);
                return false;
            }
            var mourners = OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && OwnBetween.FreeNow(p)).OrderByDescending(p => OwnBetween.Opinion(p, dead)).Take(2).ToList();
            var s = OwnBetween.Begin(b, OwnHappening.EnnuBurying, g, map, grave.Position, mourners.ElementAtOrDefault(0), mourners.ElementAtOrDefault(1), OwnHappeningsMath.BuryingHours + 2f);
            s.c = x;
            s.thing = grave;
            s.word = dead.LabelShort;
            s.stage = 1;
            foreach (var m in mourners) OwnBetween.Send(m, map, grave.Position, 3f, 3);
            return true;
        }

        /// <summary>What the dead was as a child, from their own past, in a few words ("a farm kid"), or null.</summary>
        private static string Past(Building_Grave grave)
        {
            var dead = grave?.Corpse?.InnerPawn;
            var story = dead?.story;
            string title = story?.Childhood?.TitleFor(dead.gender);
            return title.NullOrEmpty() ? null : Find.ActiveLanguageWorker.WithIndefiniteArticle(title.ToLowerInvariant());
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var x = s.c;
            var a = s.a;
            if (s.stage == 1 || s.stage == 2)
            {
                if (x == null || x.Dead || !x.Spawned || x.Map != map)
                {
                    Gone(s, now);
                    return;
                }
                bool there = x.Position.InHorDistOf(s.at, 4f);
                if (s.stage == 1 && there) s.stage = 2;
                bool mourner = a != null && OwnBetween.Still(a, map) && a.Position.InHorDistOf(s.at, 6f);
                if (s.stage == 2 && (mourner || now > s.until - GenDate.TicksPerHour * 3))
                {
                    // At the grave: one true thing about the dead nobody at the colony knew.
                    var grave = s.thing as Building_Grave;
                    string past = Past(grave);
                    if (past != null) OwnBetween.Say(x, H, "true", g, mourner ? a : null, s.word.Named("DEAD"), past.Named("DETAIL"));
                    else OwnBetween.Say(x, H, "remember", g, mourner ? a : null, s.word.Named("DEAD"));
                    if (mourner) OwnBetween.Say(a, H, "who", g, x);
                    OwnBetween.Card(H, g, map, s.at, true, past != null ? null : "TextPlain", x.Named("X"), s.word.Named("DEAD"), (past ?? "").Named("PAST"),
                        (x.Faction?.Name ?? "").Named("PEOPLE"));
                    var seer = mourner ? a : OwnHappenings.Seer(map, s.at, 25f);
                    OwnBetween.Book(H, g, map, s.at, GodActKind.Omen, seer, s.word.Named("OTHER"));
                    var other = s.b;
                    if (mourner && other != null && OwnBetween.Still(other, map) && other.Position.InHorDistOf(s.at, 6f) && Together.Can(a, other))
                        OwnBetween.End(H, BetweenOutcome.Closer, a, other, null, null, s.word.Named("DEAD"));
                    Arrivals.SendAway(new[] { x });
                    s.stage = 3;
                    s.until = now + GenDate.TicksPerHour * 8;
                    OwnBetween.Wait(s, 250);
                    return;
                }
                if (now > s.until)
                {
                    Arrivals.SendAway(new[] { x });
                    s.stage = 3;
                    s.until = now + GenDate.TicksPerHour * 8;
                }
                OwnBetween.Wait(s, 250);
                return;
            }
            if (s.stage == 3)
            {
                // Gone before dark, off the land; the Guest unmade once out of sight.
                if (x == null || !x.Spawned || x.Dead || now > s.until)
                {
                    Gone(s, now);
                    return;
                }
                OwnBetween.Wait(s, 400);
                return;
            }
            if (s.stage == 4)
            {
                // Dawn: a small thing lies on the grave.
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(OwnHappenings.Age <= 1 ? "MedicineHerbal" : "Silver");
                if (def == null)
                {
                    s.stage = -1;
                    return;
                }
                var t = ThingMaker.MakeThing(def);
                t.stackCount = 1;
                if (!GenPlace.TryPlaceThing(t, s.at, map, ThingPlaceMode.Near))
                {
                    s.stage = -1;
                    return;
                }
                s.num = t.thingIDNumber;
                s.stage = 5;
                s.until = now + GenDate.TicksPerHour * 8;
                OwnBetween.Wait(s, 250);
                return;
            }
            // Someone finds it and says so.
            var finder = OwnHappenings.Seer(map, s.at, 15f);
            if (finder != null)
            {
                string what = (OwnHappenings.Age <= 1 ? "CC_GodB_EnnuBurying_Herbs" : "CC_GodB_EnnuBurying_Coin").Translate().Resolve();
                OwnBetween.Say(finder, H, "found", g, null, s.word.Named("DEAD"));
                OwnBetween.Card(H, g, map, s.at, false, "TextFound", s.word.Named("DEAD"), what.Named("THING"));
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 400);
        }

        /// <summary>The stranger gone from the land (unmade, as the Guest's strangers are), and the morning waited for.</summary>
        private static void Gone(BetweenScene s, int now)
        {
            var x = s.c;
            if (x != null && !x.Spawned && !x.Discarded) GodStranger.Vanish(x);
            else if (x != null && x.Spawned) Arrivals.SendAway(new[] { x });
            s.c = null;
            s.stage = 4;
            int wait = OwnBetween.TicksToHour(s.map, 6);
            s.until = now + wait + GenDate.TicksPerHour * 2;
            OwnBetween.Wait(s, wait);
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var (grave, dead, _) = Fresh(b, map);
            if (grave == null)
            {
                grave = map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>().FirstOrDefault(x => x.Corpse?.InnerPawn?.RaceProps?.Humanlike == true);
                dead = grave?.Corpse?.InnerPawn;
            }
            if (grave == null) return "a grave holding one of the colony's dead is needed.";
            if (GodsOnMapBook.Instance?.stranger != null) return "another of the gods' strangers is at this home now.";
            return Begin(b, g, map, grave, dead) ? "a stranger walks in toward " + dead.LabelShort + "'s grave." : "no people at peace to come from, or no way in to the grave.";
        }
    }

    /// <summary>
    /// A machine's wake (OwnHappening.PipWake, D8): a machine that served the colony long is lost (a mechanitor's mech of a
    /// year or more; the colony's oldest turret or generator, the colony a year old; for a colony of the early ages its oldest
    /// workbench or loom, Pip's craft). At dusk the one who kept it (the mech's overseer, else the best hand at building or at
    /// the craft) and those who hold the Hum stand round the wreck; the keeper says one sentence, a believer the Hum's words
    /// (closer); a scoffer laughs at mourning a machine (a good sport takes it well) or comes and stands too (closer). The
    /// keeper carries a piece of the slag to the Hum's place or their own bed, where there is one.
    /// </summary>
    public class PipWake : BetweenKind
    {
        public override OwnHappening H => OwnHappening.PipWake;

        /// <summary>What kind of machine it is, for a wake, or null.</summary>
        public static WakeOf? KindOf(Thing t)
        {
            if (t is Pawn p) return p.RaceProps?.IsMechanoid == true ? WakeOf.Mech : Vehicle(p) ? WakeOf.Vehicle : (WakeOf?)null;
            if (t is Building_Turret) return WakeOf.Turret;
            if (t.TryGetComp<CompPowerPlant>() != null) return WakeOf.Generator;
            if (t is Building_WorkTable) return WakeOf.Bench;
            return null;
        }

        /// <summary>A vehicle of Vanilla Vehicles Expanded's (its pawn class, by name: the mod softly).</summary>
        private static bool Vehicle(Pawn p) => p?.GetType().Name == "VehiclePawn";

        /// <summary>The oldest of its kind at the home (no other of the colony's buildings, or vehicles, of its kind made before it).</summary>
        private static bool Oldest(Thing t, WakeOf k, Map map) =>
            k == WakeOf.Vehicle
                ? !map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).Any(x => x != t && x.thingIDNumber < t.thingIDNumber && Vehicle(x))
                : !map.listerBuildings.allBuildingsColonist.Any(x => x != t && x.thingIDNumber < t.thingIDNumber && KindOf(x) == k);

        /// <summary>A machine of the colony's lost (destroyed, or a mech dead): a wake at dusk for one that served the colony long, at its chance.</summary>
        public static void Lost(Thing t)
        {
            var b = OwnHappenings.Book;
            var map = t?.MapHeld;
            if (b == null || map == null || !Homes.IsHome(map)) return;
            var kind = KindOf(t);
            if (kind == null) return;
            float colonyYears = Find.TickManager.TicksGame / (float)GenDate.TicksPerYear;
            float mechYears = t is Pawn m ? m.ageTracker?.AgeBiologicalYearsFloat ?? 0f : 0f;
            if (!OwnHappeningsMath.Served(kind.Value, mechYears, kind != WakeOf.Mech && Oldest(t, kind.Value, map), colonyYears, OwnHappenings.Age)) return;
            if (b.between.scenes.Any(s => s.map == map && s.stage >= 0)) return;
            var g = OwnHappenings.God(OwnPip.Key);
            if (!OwnBetween.Follows(b, OwnHappening.PipWake, g, map, OwnHappeningsMath.WakeChance)) return;
            Begin(b, g, map, t, kind.Value);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Thing t, WakeOf kind)
        {
            // The one who kept it: the mech's overseer, else the best hand at building or at the craft (busy or drafted as the
            // loss may find them: the wake waits for dusk, and for them to be free).
            Pawn keeper = t is Pawn mech ? MechanitorUtility.GetOverseer(mech) : null;
            if (keeper == null || keeper.Map != map || !OwnHappenings.Ours(keeper))
            {
                var skill = kind == WakeOf.Bench ? SkillDefOf.Crafting : SkillDefOf.Construction;
                keeper = map.mapPawns.FreeColonistsSpawned.Where(p => OwnHappenings.Ours(p) && OwnBetween.Grown(p)).OrderByDescending(p => p.skills?.GetSkill(skill)?.Level ?? 0).FirstOrDefault();
            }
            if (keeper == null) return false;
            int hour = OwnHappenings.Hour(map);
            int wait = hour >= OwnHappeningsMath.WakeFrom && hour < OwnHappeningsMath.WakeTo ? 600 : OwnBetween.TicksToHour(map, OwnHappeningsMath.WakeFrom);
            var s = OwnBetween.Begin(b, OwnHappening.PipWake, g, map, t.PositionHeld, keeper, null, wait / (float)GenDate.TicksPerHour + 5f);
            s.word = t.def.label;
            s.num = (int)kind;
            s.stage = 0;
            OwnBetween.Wait(s, wait);
            return true;
        }

        public override void Hourly(OwnHappeningsBook b, God g, Map map, int now)
        {
            // It follows a machine lost (Patch_BetweenMachineLost, Patch_BetweenMechLost), not the hour.
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var keeper = s.a;
            // Before dusk's walk the keeper may still be drafted or busy (the step below waits for them); after it, they must be there.
            if (keeper == null || keeper.Dead || !keeper.Spawned || keeper.Map != map || s.stage > 0 && !OwnBetween.Still(keeper, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 0)
            {
                // Dusk: the keeper and those who hold the Hum walk to the wreck.
                if (!OwnBetween.FreeNow(keeper))
                {
                    if (now > s.until) s.stage = -1;
                    OwnBetween.Wait(s, 300);
                    return;
                }
                var believers = OwnBetween.People(map).Where(p => p != keeper && OwnBetween.Grown(p) && OwnBetween.FreeNow(p) && OwnBetween.Holds(p, g)).Take(2).ToList();
                OwnBetween.Send(keeper, map, s.at, 2f, 2);
                foreach (var p in believers) OwnBetween.Send(p, map, s.at, 2f, 3);
                s.b = believers.ElementAtOrDefault(0);
                s.c = believers.ElementAtOrDefault(1);
                s.stage = 1;
                s.until = now + GenDate.TicksPerHour * 3;
                OwnBetween.Wait(s, 250);
                return;
            }
            if (!OwnBetween.There(keeper, s.at, 4f))
            {
                if (keeper.CurJobDef != JobDefOf.Goto) OwnBetween.Send(keeper, map, s.at, 1f, 2);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 200);
                return;
            }
            var what = (s.word ?? "").Named("WHAT");
            OwnHappenings.Stand(keeper, s.at, 1500);
            // apart: the keeper's one sentence over the wreck, to whoever stands there.
            OwnBetween.Say(keeper, H, "keeper", g, null, what);
            var believer = new[] { s.b, s.c }.FirstOrDefault(p => p != null && OwnBetween.Still(p, map) && p.Position.InHorDistOf(s.at, 6f) && Together.Can(p, keeper));
            if (believer != null)
            {
                OwnBetween.Say(believer, H, "hum", g, keeper, what);
                OwnBetween.End(H, BetweenOutcome.Closer, keeper, believer, null, null, what);
            }
            // A scoffer near: laughs at mourning a machine, or comes and stands too.
            var scoffer = OwnBetween.People(map).FirstOrDefault(p => p != keeper && p != believer && OwnBetween.Grown(p) && OwnBetween.Doubts(p) && p.Position.InHorDistOf(s.at, 20f)
                                                                     && Together.Can(p, keeper));
            if (scoffer != null)
            {
                if (Rand.Chance(0.5f))
                {
                    OwnBetween.Say(scoffer, H, "laugh", g, keeper, what);
                    OwnBetween.End(H, OwnHappeningsMath.Laugh(OwnBetween.Opinion(keeper, scoffer)), keeper, scoffer, null, null, what);
                }
                else
                {
                    OwnBetween.Say(scoffer, H, "stand", g, keeper, what);
                    OwnBetween.End(H, BetweenOutcome.Closer, scoffer, keeper, null, null, what);
                }
            }
            // A piece of the slag carried to the Hum's place, else to the keeper's bed.
            var slag = GenRadial.RadialDistinctThingsAround(s.at, map, 5f, true).FirstOrDefault(t => t.def.category == ThingCategory.Item && t.def.defName.IndexOf("Slag", StringComparison.OrdinalIgnoreCase) >= 0
                                                                                                && !t.IsForbidden(keeper));
            if (slag != null)
            {
                var place = g != null ? Shrines.Nearest(keeper, g) : null;
                var bed = keeper.ownership?.OwnedBed;
                var to = place != null ? OwnHappenings.Beside(map, place.Position, keeper) : bed != null && bed.Spawned && bed.Map == map ? OwnHappenings.Beside(map, bed.Position, keeper) : IntVec3.Invalid;
                if (to.IsValid) OwnHappenings.Carry(keeper, slag, to, 1);
            }
            OwnBetween.Card(H, g, map, s.at, false, believer != null ? null : "TextAlone", keeper.Named("A"), what);
            OwnBetween.Book(H, g, map, s.at, GodActKind.Act, keeper, what);
            s.stage = -1;
        }

        public override string Fire(OwnHappeningsBook b, God g, Map map)
        {
            var machine = map.listerBuildings.allBuildingsColonist.Where(x => KindOf(x) != null).OrderBy(x => x.thingIDNumber).FirstOrDefault();
            Thing t = machine ?? (Thing)map.mapPawns.SpawnedColonyMechs?.FirstOrDefault();
            if (t == null) return "a turret, a generator, a workbench or a mech of the colony's is needed (nothing is destroyed: the wake is held as if it were lost).";
            b.between.scenes.RemoveAll(s => s.map == map);
            return Begin(b, g, map, t, KindOf(t) ?? WakeOf.Bench) ? "a wake at dusk for the " + t.LabelShort + " (as if it were lost)." : "nobody to keep the wake.";
        }
    }

    /// <summary>A building of the colony's destroyed (not torn down): a machine's wake may follow (PipWake.Lost). Off with the deeper game hooks.</summary>
    [HarmonyPatch(typeof(Building), nameof(Building.Destroy))]
    public static class Patch_BetweenMachineLost
    {
        public static void Prefix(Building __instance, DestroyMode mode)
        {
            if (!RimfolkPatches.On || !Gods.On || mode != DestroyMode.KillFinalize || __instance == null || !__instance.Spawned || __instance.Faction != Faction.OfPlayer) return;
            try
            {
                PipWake.Lost(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods between them: a machine lost", e);
            }
        }
    }

    /// <summary>A mech of the colony's dead (or a vehicle, where Vanilla Vehicles Expanded's die as pawns do): a machine's wake may follow (PipWake.Lost). Off with the deeper game hooks.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_BetweenMechLost
    {
        public static void Prefix(Pawn __instance)
        {
            if (!RimfolkPatches.On || !Gods.On || __instance == null || __instance.Dead || !__instance.Spawned || __instance.Faction != Faction.OfPlayer
                || __instance.RaceProps?.Humanlike == true || PipWake.KindOf(__instance) == null) return;
            try
            {
                PipWake.Lost(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods between them: a mech lost", e);
            }
        }
    }
}
