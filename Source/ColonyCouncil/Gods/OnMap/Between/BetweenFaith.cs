using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// What round 7's happenings between the colony's people share (docs/content-update-proposals-2.md, D1 to D15): a god's
    /// place for someone (its dedicated place, else where its small rite is kept), the place in words for a card, something
    /// left there taken in as an offering (so the god's own way takes it in time, as any gift), and the things carried.
    /// </summary>
    public static class FaithBetween
    {
        /// <summary>What is left at a god's place by one of these happenings moves its favour this much when the god takes it (a small gift).</summary>
        public const float LeftFavour = 1f;

        /// <summary>The god's place for someone: its dedicated place nearest them, else where its small rite is kept near them; Invalid where neither.</summary>
        public static IntVec3 PlaceOf(Pawn p, God g, out Thing place)
        {
            place = null;
            if (p?.Map == null || g == null) return IntVec3.Invalid;
            var shrine = Shrines.Nearest(p, g);
            if (shrine != null)
            {
                place = shrine;
                return shrine.Position;
            }
            var rite = PawnGodsMath.RiteOf(g.Key);
            if (rite == null) return IntVec3.Invalid;
            var t = PawnGodsActs.Target(p, rite.Value);
            if (!t.IsValid) return IntVec3.Invalid;
            place = t.HasThing ? t.Thing : null;
            return t.Cell;
        }

        /// <summary>The god's dedicated place standing at a cell, else the building or tree there, else null.</summary>
        public static Thing PlaceAt(Map map, IntVec3 at)
        {
            if (map == null || !at.IsValid || !at.InBounds(map)) return null;
            var things = at.GetThingList(map);
            return things.FirstOrDefault(t => Worship.Book?.ShrineOf(t) != null) ?? things.FirstOrDefault(t => t is Building) ?? things.FirstOrDefault(t => t is Plant pl && pl.def.plant?.IsTree == true);
        }

        /// <summary>A thing carried, in words for a card ("a granite chunk", "some wood").</summary>
        public static string AThing(Thing t) =>
            t == null ? "" : t.def == ThingDefOf.WoodLog ? "CC_GodB_Faith_SomeWood".Translate().Resolve() : Find.ActiveLanguageWorker.WithIndefiniteArticle(t.def.label);

        /// <summary>The place in words for a card ("shrine", "oak"), else the spot where the small rite is kept.</summary>
        public static string PlaceWords(Thing place) => place != null ? place.def.label : "CC_GodB_Faith_RiteSpot".Translate().Resolve();

        /// <summary>
        /// Something left at a god's dedicated place, taken in as its offering (Gods/Worship/Offerings.cs): the god's own way
        /// takes it in time, as any gift, or it stays. Only at a place dedicated to that god, with nothing else waiting there.
        /// </summary>
        public static bool LeaveAsOffering(Pawn carrier, Thing lying, Thing place, God g)
        {
            var b = Worship.Book;
            if (b == null || lying == null || !lying.Spawned || place == null || g == null) return false;
            var s = b.ShrineOf(place);
            if (s == null || s.pending || s.god != g.id || !s.Live) return false;
            if (b.offerings.Any(o => o != null && o.shrine == place && o.fate == OfferingFate.Waiting)) return false;
            int now = Find.TickManager.TicksGame;
            b.offerings.Add(new OfferingRecord
            {
                thing = lying, shrine = place, god = g.id, home = Gods.HomeKey(place.Map), carrier = carrier, label = lying.def.label, kind = Offerings.Kind(lying.def),
                favour = LeftFavour, set = true, chosen = now, placed = now, worth = lying.MarketValue * lying.stackCount, fit = 0.5f, plain = lying.def.label
            });
            lying.SetForbidden(true, false);
            GodPlaces.Tend(place);
            s.lastWhat = lying.def.label;
            s.lastBy = carrier?.LabelShort;
            s.lastAt = now;
            s.lastFate = OfferingFate.Waiting;
            return true;
        }

        /// <summary>A thing of this def lying loose near a cell (just set down there), or null.</summary>
        public static Thing LyingNear(Map map, IntVec3 at, ThingDef def, float radius = 2.5f)
        {
            if (map == null || def == null || !at.IsValid) return null;
            foreach (var c in GenRadial.RadialCellsAround(at, radius, true))
            {
                if (!c.InBounds(map)) continue;
                var t = c.GetThingList(map).FirstOrDefault(x => x.def == def && x.Spawned && !x.IsInAnyStorage());
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>A meal in the stores, the plainest first.</summary>
        public static Thing Meal(Map map, Pawn p) =>
            map?.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.Spawned && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer) && t.def.IsNutritionGivingIngestible && t.def.ingestible.IsMeal
                            && (p == null || p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some)))
                .OrderBy(t => t.def.ingestible.preferability).FirstOrDefault();

        /// <summary>A stone chunk (or slag first, for a colony of the later ages) someone can reach near them, else null.</summary>
        public static Thing Chunk(Pawn p, bool slagFirst)
        {
            var map = p?.Map;
            if (map == null) return null;
            bool Slag(Thing t) => t.def.defName.IndexOf("Slag", StringComparison.OrdinalIgnoreCase) >= 0;
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Chunk)
                .Where(t => t.Spawned && !t.IsForbidden(p) && t.Position.InHorDistOf(p.Position, 40f) && p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(t => Slag(t) == slagFirst ? 0 : 1).ThenBy(t => t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
        }

        /// <summary>Wood from the stores someone can reach, or null.</summary>
        public static Thing Wood(Pawn p) =>
            p?.Map?.listerThings.ThingsOfDef(ThingDefOf.WoodLog).Where(t => t.Spawned && !t.IsForbidden(p) && p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(t => t.Position.DistanceToSquared(p.Position)).FirstOrDefault();

        /// <summary>The one of the colony at a home best at a skill, grown and free now, none of these.</summary>
        public static Pawn Best(Map map, SkillDef skill, params Pawn[] not) =>
            OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && OwnBetween.FreeNow(p) && (not == null || !not.Contains(p)))
                .OrderByDescending(p => p.skills?.GetSkill(skill)?.Level ?? 0).FirstOrDefault();

        /// <summary>They hold a god that is one of this god's rivals by the stories.</summary>
        public static bool HoldsRival(Pawn p, God g)
        {
            var h = OwnBetween.HeldBy(p);
            return h != null && g != null && h != g && Gods.Rivals(g).Contains(h);
        }

        /// <summary>An open cell under the sky near the colony's middle that someone can walk to, else Invalid.</summary>
        public static IntVec3 Outdoors(Map map, Pawn p)
        {
            var mid = OwnHappenings.Middle(map);
            if (CellFinder.TryFindRandomCellNear(mid, map, 14, c => c.Standable(map) && !c.Roofed(map) && (p == null || p.CanReach(c, PathEndMode.OnCell, Danger.Some)), out var cell))
                return cell;
            return IntVec3.Invalid;
        }

        /// <summary>A light of the colony's burning near a cell (a torch, a candle, a lamp), the nearest, or null.</summary>
        public static Thing LightNear(Map map, IntVec3 at, float radius)
        {
            if (map == null || !at.IsValid) return null;
            return map.listerBuildings.allBuildingsColonist.Where(x => x.Spawned && x.Position.InHorDistOf(at, radius) && x.GetComp<CompGlower>() is CompGlower gl && gl.Glows)
                .OrderBy(x => x.Position.DistanceToSquared(at)).FirstOrDefault();
        }

        /// <summary>Still burning.</summary>
        public static bool Lit(Thing t) => t != null && t.Spawned && t.TryGetComp<CompGlower>() is CompGlower gl && gl.Glows;

        /// <summary>A colonist sick in bed with an illness they are not yet over.</summary>
        public static bool SickInBed(Pawn p) =>
            p?.health?.hediffSet != null && !p.Dead && p.InBed() && p.health.hediffSet.hediffs.Any(h => h.Visible && h.def.makesSickThought
                                                                                                && (p.health.immunity?.GetImmunityRecord(h.def)?.immunity ?? 0f) < OwnHappeningsMath.OverItFrom);
    }

    /// <summary>
    /// The dare (OwnHappening.FaithDare, D1): in the evening a scoffer and a believer are together (the believer just seen at
    /// their small rite, likelier), and the scoffer dares the believer's god to show itself by something the morning settles:
    /// rain by morning in a dry spell, the sick one up by morning, the light at the god's place still burning at dawn. In the
    /// morning the two walk out together to look. It came: the believer is proud and the scoffer shaken a little; it didn't:
    /// the scoffer is pleased and the believer takes it well or not; or the two argue what it means. Nothing says a god
    /// answered or didn't; a god drawn near may answer the rain in its own way in the night (GodWeather.Answer).
    /// </summary>
    public class FaithDare : BetweenKind
    {
        public override OwnHappening H => OwnHappening.FaithDare;

        /// <summary>A believer (free, or at their small rite) and a scoffer together, and the believer's god.</summary>
        public static (Pawn believer, Pawn scoffer, God god) Pair(Map map)
        {
            var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
            foreach (var s in people.Where(p => OwnBetween.Doubts(p) && OwnBetween.FreeNow(p)).InRandomOrder())
            {
                var b = people.Where(p => p != s && !OwnBetween.Doubts(p) && OwnBetween.HeldBy(p) != null && (OwnBetween.FreeNow(p) || AtRite(p)) && Together.Can(s, p))
                    .OrderByDescending(p => AtRite(p) ? 1 : 0).FirstOrDefault();
                if (b != null) return (b, s, OwnBetween.HeldBy(b));
            }
            return (null, null, null);
        }

        private static bool AtRite(Pawn p) => p?.CurJobDef != null && p.CurJobDef == PawnGodsDefOf.Rimfolk_Belief_Rite;

        /// <summary>What can be dared now, with the sick one in bed and the light at the god's place where there are.</summary>
        public static List<GodDare> Dares(Map map, Pawn believer, God g, out Pawn sick, out Thing light)
        {
            sick = map.mapPawns.FreeColonistsSpawned.Where(FaithBetween.SickInBed).FirstOrDefault();
            var at = FaithBetween.PlaceOf(believer, g, out _);
            light = at.IsValid ? FaithBetween.LightNear(map, at, 6f) : null;
            return OwnHappeningsMath.Dares(OwnHappenings.DryRun(map), OwnHappenings.Raining(map), sick != null, light != null);
        }

        public override void Hourly(OwnHappeningsBook b, God none, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.DareFrom || hour >= OwnHappeningsMath.DareTo) return;
            var (believer, scoffer, g) = Pair(map);
            if (believer == null || !OwnHappenings.Acts(g, map)) return;
            var dares = Dares(map, believer, g, out var sick, out var light);
            var why = OwnHappeningsMath.DareTrigger(true, dares.Count, AtRite(believer));
            if (!OwnHappenings.Calls(b, H, why, g, map, now)) return;
            Begin(b, g, map, believer, scoffer, dares.RandomElement(), sick, light);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, Pawn believer, Pawn scoffer, GodDare dare, Pawn sick, Thing light)
        {
            var s = OwnBetween.Begin(b, OwnHappening.FaithDare, g, map, believer.Position, believer, scoffer, 3f);
            s.word = dare.ToString();
            s.thing = dare == GodDare.Sick ? sick : dare == GodDare.Light ? light : null;
            s.stage = 1;
            OwnBetween.Send(scoffer, map, believer.Position, 1f, 2);
        }

        private static GodDare Dare(BetweenScene s) => Enum.TryParse(s.word, out GodDare d) ? d : GodDare.Rain;

        /// <summary>What was dared, in words ("it would rain before morning").</summary>
        private static string Words(BetweenScene s, GodDare d)
        {
            if (d == GodDare.Sick) return "CC_GodB_FaithDare_Sick".Translate((s.thing?.LabelShort ?? "?").Named("SICK")).Resolve();
            if (d == GodDare.Light) return "CC_GodB_FaithDare_Light".Translate(FaithBetween.PlaceWords(FaithBetween.PlaceAt(s.map, s.thing?.Position ?? s.at)).Named("PLACE")).Resolve();
            return "CC_GodB_FaithDare_Rain".Translate().Resolve();
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var believer = s.a;
            var scoffer = s.b;
            if (!OwnBetween.Still(believer, map) || !OwnBetween.Still(scoffer, map))
            {
                s.stage = -1;
                return;
            }
            var dare = Dare(s);
            int hour = OwnHappenings.Hour(map);
            if (s.stage == 1)
            {
                // The dare, face to face in the evening.
                if (believer.Position.InHorDistOf(scoffer.Position, 5f) && Together.Can(scoffer, believer))
                {
                    OwnBetween.Face(scoffer, believer, 600);
                    if (dare == GodDare.Rain) OwnBetween.Say(scoffer, H, "darerain", g, believer);
                    else if (dare == GodDare.Sick) OwnBetween.Say(scoffer, H, "daresick", g, believer, (s.thing?.LabelShort ?? "").Named("SICK"));
                    else OwnBetween.Say(scoffer, H, "darelight", g, believer);
                    OwnBetween.Say(believer, H, "take", g, scoffer);
                    s.stage = 2;
                    s.until = now + OwnBetween.TicksToHour(map, OwnHappeningsMath.DareMorningTo);
                    OwnBetween.Wait(s, 1000);
                    return;
                }
                if (scoffer.CurJobDef != JobDefOf.Goto) OwnBetween.Send(scoffer, map, believer.Position, 1f, 2);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 150);
                return;
            }
            if (s.stage == 2)
            {
                // The night: the rain looked for, and a god drawn near may answer it in its own way (nothing says it did).
                if (OwnHappenings.Raining(map)) s.num |= 1;
                if (dare == GodDare.Rain && (s.num & 2) == 0 && (hour >= 23 || hour < 4))
                {
                    s.num |= 2;
                    if (g != null && GodNear.Near(g, map) && Rand.Chance(0.35f)) GodWeather.Answer(g, map, 1f, true);
                }
                if (hour >= OwnHappeningsMath.DareMorningFrom && hour < OwnHappeningsMath.DareMorningTo && OwnBetween.FreeNow(believer) && OwnBetween.FreeNow(scoffer))
                {
                    // The morning: the two walk out together to look.
                    var at = dare == GodDare.Rain ? FaithBetween.Outdoors(map, believer) : s.thing != null && s.thing.Spawned ? s.thing.Position : IntVec3.Invalid;
                    if (!at.IsValid)
                    {
                        s.stage = -1;
                        return;
                    }
                    s.at = at;
                    OwnBetween.Send(believer, map, at, 2f, 2);
                    OwnBetween.Send(scoffer, map, at, 2f, 3);
                    s.stage = 3;
                    s.until = now + GenDate.TicksPerHour * 2;
                }
                OwnBetween.Wait(s, 600);
                return;
            }
            // Out together: when both are there, what came of it.
            if (OwnHappenings.Raining(map)) s.num |= 1;
            if (OwnBetween.There(believer, s.at, 5f) && OwnBetween.There(scoffer, s.at, 5f) && Together.Can(believer, scoffer))
            {
                bool came = dare == GodDare.Rain ? (s.num & 1) != 0
                    : dare == GodDare.Sick ? s.thing is Pawn sick && !sick.Dead && sick.Spawned && !sick.InBed() && !sick.Downed
                    : FaithBetween.Lit(s.thing);
                var o = OwnHappeningsMath.DareEnd(came, OwnBetween.Opinion(believer, scoffer), Rand.Value);
                string what = Words(s, dare);
                string doubt = null;
                OwnBetween.Face(believer, scoffer, 600);
                if (o == BetweenOutcome.Quarrel)
                {
                    OwnBetween.Say(believer, H, "argue", g, scoffer);
                    doubt = OwnBetween.Say(scoffer, H, "scoff", g, believer);
                    OwnBetween.End(H, BetweenOutcome.Quarrel, believer, scoffer, g, null);
                }
                else if (came)
                {
                    OwnBetween.Say(believer, H, "proud", g, scoffer);
                    OwnBetween.Say(scoffer, H, "shaken", g, believer);
                    OwnBetween.Proud(H, believer);
                    OwnBetween.Shaken(scoffer, g);
                }
                else
                {
                    OwnBetween.Say(believer, H, "quiet", g, scoffer);
                    doubt = OwnBetween.Say(scoffer, H, "pleased", g, believer);
                    OwnBetween.End(H, o, believer, scoffer);
                }
                OwnBetween.Card(H, g, map, s.at, false, came ? null : "TextNot", believer.Named("A"), scoffer.Named("B"), what.Named("WHAT"));
                OwnBetween.Book(H, g, map, s.at, came ? GodActKind.Omen : GodActKind.Act, believer, scoffer.LabelShort.Named("OTHER"), what.Named("WHAT"));
                OwnBetween.Doubted(scoffer, g, doubt);
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 200);
        }

        public override string Fire(OwnHappeningsBook b, God none, Map map)
        {
            var (believer, scoffer, g) = Pair(map);
            if (believer == null)
            {
                var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
                believer = people.FirstOrDefault(p => OwnBetween.HeldBy(p) != null);
                scoffer = people.Where(p => p != believer).OrderBy(GodLore.Credence).FirstOrDefault();
                g = OwnBetween.HeldBy(believer);
            }
            if (believer == null || scoffer == null || g == null) return "a believer (one who holds a god) and another grown of the colony are needed.";
            var dares = Dares(map, believer, g, out var sick, out var light);
            var dare = dares.Count > 0 ? dares.RandomElement() : GodDare.Rain;
            Begin(b, g, map, believer, scoffer, dare, sick, light);
            return scoffer.LabelShort + " dares " + believer.LabelShort + "'s god (" + dare + "); the two look in the morning.";
        }
    }

    /// <summary>
    /// Came through (OwnHappening.FaithCameThrough, D2): one of the colony who came through a near death (bled near to it, or
    /// a deadly illness past its worst) walks, once on their feet, to the place of the god they hold and leaves something
    /// there: a damaged weapon or a dented piece of armour from the home's ground, where there is one. What is left at a
    /// dedicated place is the god's as any gift (Offerings: its own way takes it in time, or it stays). The colony's doctor
    /// walks over: one who holds the same god stands with them (closer); one who doubts or holds another says it was the
    /// stitches (a good sport laughs along, a sore one minds it).
    /// </summary>
    public class FaithCameThrough : BetweenKind
    {
        public override OwnHappening H => OwnHappening.FaithCameThrough;

        /// <summary>The near death someone came through, as a key kept once answered (their id and the hediff's), or null.</summary>
        public static string NearDeath(Pawn p)
        {
            var hs = p?.health?.hediffSet;
            if (hs == null) return null;
            foreach (var h in hs.hediffs)
            {
                if (h?.def == null) continue;
                float blood = h.def == HediffDefOf.BloodLoss ? h.Severity : 0f;
                float share = h.def.makesSickThought && h.def.lethalSeverity > 0f ? h.Severity / h.def.lethalSeverity : 0f;
                float immunity = share > 0f ? p.health.immunity?.GetImmunityRecord(h.def)?.immunity ?? 0f : 0f;
                if (OwnHappeningsMath.NearDeath(blood, share, immunity)) return "came|" + p.thingIDNumber + "|" + h.loadID;
            }
            return null;
        }

        /// <summary>One on their feet after a near death they haven't walked out for yet, and the god they hold.</summary>
        public static (Pawn p, God g, string key) Through(OwnHappeningsBook b, Map map)
        {
            foreach (var p in OwnBetween.People(map).Where(x => OwnBetween.Grown(x) && OwnBetween.FreeNow(x) && !x.InBed()))
            {
                var g = OwnBetween.HeldBy(p);
                if (g == null) continue;
                string key = NearDeath(p);
                if (key != null && !b.between.answered.Contains(key)) return (p, g, key);
            }
            return (null, null, null);
        }

        /// <summary>A damaged weapon or piece of armour lying on the home's ground near them (the fight's), or null.</summary>
        private static Thing Keepsake(Pawn p)
        {
            var map = p.Map;
            return map.listerThings.ThingsInGroup(ThingRequestGroup.Weapon).Concat(map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
                .Where(t => t.Spawned && t.def.useHitPoints && t.HitPoints < t.MaxHitPoints * 0.6f && map.areaManager.Home[t.Position] && !t.IsForbidden(p)
                            && t.Position.InHorDistOf(p.Position, 40f) && p.CanReserveAndReach(t, PathEndMode.ClosestTouch, Danger.Some))
                .OrderBy(t => t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
        }

        public override void Hourly(OwnHappeningsBook b, God none, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 8 || hour >= 19) return;
            var (p, g, key) = Through(b, map);
            if (p == null || !OwnHappenings.Acts(g, map)) return;
            b.between.answered.Add(key);
            b.between.Trim();
            if (!OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.CameChance)) return;
            Begin(b, g, map, p);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn p)
        {
            var at = FaithBetween.PlaceOf(p, g, out var place);
            if (!at.IsValid) return false;
            var s = OwnBetween.Begin(b, OwnHappening.FaithCameThrough, g, map, at, p, null, 5f);
            var thing = Keepsake(p);
            var beside = OwnHappenings.Beside(map, at, p);
            if (thing != null && beside.IsValid && OwnHappenings.Carry(p, thing, beside, 1))
            {
                s.thing = thing;
                s.num = 1;
            }
            else OwnBetween.Send(p, map, at, 2f, 2);
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var p = s.a;
            if (!OwnBetween.Still(p, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                bool walking = p.CurJobDef == JobDefOf.HaulToCell || p.CurJobDef == JobDefOf.Goto;
                if (!walking && p.Position.InHorDistOf(s.at, 3.5f))
                {
                    OwnHappenings.Stand(p, s.at, 1200);
                    var place = FaithBetween.PlaceAt(map, s.at);
                    var left = s.num == 1 && s.thing != null && s.thing.Spawned && s.thing.Position.InHorDistOf(s.at, 2.5f) ? s.thing : null;
                    string what = left?.def.label ?? "";
                    // apart: alone at the god's place, said to nobody in particular.
                    if (left != null)
                    {
                        OwnBetween.Say(p, H, "leave", g, null, what.Named("WHAT"));
                        FaithBetween.LeaveAsOffering(p, left, place, g);
                    }
                    else OwnBetween.Say(p, H, "thanks", g);
                    OwnBetween.Card(H, g, map, s.at, false, left != null ? null : "TextNothing", p.Named("A"), what.Named("WHAT"), FaithBetween.PlaceWords(place).Named("PLACE"));
                    OwnBetween.Book(H, g, map, s.at, GodActKind.Blessing, p);
                    // The doctor who tended them hears of it and walks over.
                    var doctor = FaithBetween.Best(map, SkillDefOf.Medicine, p);
                    if (doctor == null || (doctor.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0) < 3)
                    {
                        s.stage = -1;
                        return;
                    }
                    s.b = doctor;
                    OwnBetween.Send(doctor, map, p.Position, 2f, 2);
                    s.stage = 2;
                    s.until = now + GenDate.TicksPerHour * 2;
                    OwnBetween.Wait(s, 200);
                    return;
                }
                if (!walking && now < s.until) OwnBetween.Send(p, map, s.at, 1f, 2);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 200);
                return;
            }
            var doc = s.b;
            if (!OwnBetween.Still(doc, map))
            {
                s.stage = -1;
                return;
            }
            if (doc.Position.InHorDistOf(p.Position, 4f) && Together.Can(doc, p))
            {
                OwnBetween.Face(doc, p, 600);
                var stance = OwnBetween.StanceOf(doc, g);
                var o = OwnHappeningsMath.Doctor(stance, OwnBetween.Opinion(p, doc));
                if (stance == BetweenStance.Holds) OwnBetween.Say(doc, H, "stand", g, p);
                else if (stance == BetweenStance.Neither) OwnBetween.Say(doc, H, "glad", g, p);
                else
                {
                    OwnBetween.Say(doc, H, "stitches", g, p);
                    OwnBetween.Say(p, H, "answer", g, doc);
                }
                OwnBetween.End(H, o, p, doc);
                s.stage = -1;
                return;
            }
            if (doc.CurJobDef != JobDefOf.Goto) OwnBetween.Send(doc, map, p.Position, 1f, 2);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 200);
        }

        public override string Fire(OwnHappeningsBook b, God none, Map map)
        {
            var (p, g, _) = Through(b, map);
            if (p == null)
            {
                p = OwnBetween.People(map).FirstOrDefault(x => OwnBetween.Grown(x) && OwnBetween.HeldBy(x) != null);
                g = OwnBetween.HeldBy(p);
            }
            if (p == null) return "one of the colony who holds a god is needed.";
            return Begin(b, g, map, p) ? p.LabelShort + " walks to their god's place." : "no place of their god (nor of its small rite) they can reach.";
        }
    }

    /// <summary>
    /// The believers take sides (OwnHappening.FaithSides, D3): while two gods' quarrel shows in the weather or on the ground
    /// (GodDrama), at supper one who holds the god whose turn it is now and one who holds the other argue whose it was (a
    /// quarrel, each held to their own the harder). The peacemaker (else the chaplain, else the best talker) walks over and
    /// sits the two at one table: they make it up, or a coolness stays between them.
    /// </summary>
    public class FaithSides : BetweenKind
    {
        public override OwnHappening H => OwnHappening.FaithSides;

        /// <summary>A quarrel of two gods going on at this home: the god whose turn it is now, and the other.</summary>
        public static (God turn, God other) Quarrel(Map map)
        {
            var d = GodWorld.Book?.Of("quarrel").FirstOrDefault(x => x.Map == map);
            var a = d == null ? null : Gods.ById(d.god);
            var o = d == null ? null : Gods.ById(d.other);
            if (a == null || o == null) return (null, null);
            return d.stage == 0 ? (a, o) : (o, a);
        }

        /// <summary>One who holds the god whose turn it is and one who holds the other, near each other.</summary>
        public static (Pawn x, Pawn y) Sides(Map map, God turn, God other)
        {
            var people = OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && OwnBetween.FreeNow(p)).ToList();
            foreach (var x in people.Where(p => OwnBetween.Holds(p, turn)).InRandomOrder())
            {
                var y = people.Where(p => p != x && OwnBetween.Holds(p, other) && p.Position.InHorDistOf(x.Position, 30f)).OrderBy(p => p.Position.DistanceToSquared(x.Position)).FirstOrDefault();
                if (y != null) return (x, y);
            }
            return (null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God none, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.SupperFrom || hour >= OwnHappeningsMath.SupperTo) return;
            var (turn, other) = Quarrel(map);
            if (turn == null || !OwnHappenings.Acts(turn, map)) return;
            var (x, y) = Sides(map, turn, other);
            if (x == null || !OwnBetween.Follows(b, H, turn, map, OwnHappeningsMath.SidesChance)) return;
            Begin(b, turn, other, map, x, y);
        }

        private static void Begin(OwnHappeningsBook b, God turn, God other, Map map, Pawn x, Pawn y)
        {
            var s = OwnBetween.Begin(b, OwnHappening.FaithSides, turn, map, y.Position, x, y, 3f);
            s.num = other.id;
            s.stage = 1;
            OwnBetween.Send(x, map, y.Position, 1f, 2);
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var other = Gods.ById(s.num);
            var x = s.a;
            var y = s.b;
            if (!OwnBetween.Still(x, map) || !OwnBetween.Still(y, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                if (x.Position.InHorDistOf(y.Position, 4f) && Together.Can(x, y))
                {
                    OwnBetween.Face(x, y, 1800);
                    OwnBetween.Say(x, H, "turn", g, y);
                    OwnBetween.Say(y, H, "answer", g, x);
                    OwnBetween.End(H, BetweenOutcome.Quarrel, x, y, g, other);
                    OwnBetween.Card(H, g, map, x.Position, false, null, x.Named("A"), y.Named("B"));
                    OwnBetween.Book(H, g, map, x.Position, GodActKind.Omen, x, y.LabelShort.Named("OTHER"));
                    // The peacemaker, else the chaplain, else the best talker, walks over to sit them at one table.
                    var pm = Interplay.Officer("Council_Office_Peacemaker", map) ?? Worship.Priest(map);
                    if (pm == null || pm == x || pm == y || !OwnBetween.FreeNow(pm)) pm = FaithBetween.Best(map, SkillDefOf.Social, x, y);
                    if (pm == null)
                    {
                        s.stage = -1;
                        return;
                    }
                    s.c = pm;
                    OwnBetween.Send(pm, map, x.Position, 2f, 2);
                    s.stage = 2;
                    s.until = now + GenDate.TicksPerHour * 2;
                    OwnBetween.Wait(s, 200);
                    return;
                }
                if (x.CurJobDef != JobDefOf.Goto) OwnBetween.Send(x, map, y.Position, 1f, 2);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 150);
                return;
            }
            var peace = s.c;
            if (!OwnBetween.Still(peace, map))
            {
                s.stage = -1;
                return;
            }
            if (peace.Position.InHorDistOf(x.Position, 4f) && x.Position.InHorDistOf(y.Position, 6f) && Together.Can(peace, x) && Together.Can(x, y))
            {
                OwnBetween.Face(peace, x, 600);
                OwnBetween.Say(peace, H, "table", g, x);
                var o = OwnHappeningsMath.Sat(OwnBetween.Opinion(x, y), OwnBetween.Opinion(y, x));
                if (o == BetweenOutcome.MadeUp)
                {
                    OwnBetween.Say(x, H, "mend", g, y);
                    OwnBetween.End(H, BetweenOutcome.MadeUp, x, y);
                }
                else OwnBetween.Say(y, H, "cool", g, x);
                s.stage = -1;
                return;
            }
            if (peace.CurJobDef != JobDefOf.Goto) OwnBetween.Send(peace, map, x.Position, 1f, 2);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 200);
        }

        public override string Fire(OwnHappeningsBook b, God none, Map map)
        {
            var (turn, other) = Quarrel(map);
            if (turn == null) return "no two gods' quarrel going on at this home (the world's dev actions can start one).";
            var (x, y) = Sides(map, turn, other);
            if (x == null) return "nobody here holds each of the two gods in the quarrel.";
            Begin(b, turn, other, map, x, y);
            return x.LabelShort + " and " + y.LabelShort + " argue at supper over whose turn it was.";
        }
    }
}
