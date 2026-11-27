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
    /// The children's little place (OwnHappening.FaithLittleShrine, D5; children come with Biotech): two children, one of
    /// them leaning toward a god, carry a stone (slag for a later colony) or a few sticks and part of a meal out to a corner
    /// at the edge and set them up as a little place for that god. One of three comes to it: a grown-up who holds the god
    /// helps (closer); the keeper of the colony's book tells them the god's name as the book has it (a kindness remembered,
    /// and the children hold the god a little more); or a scoffer kicks it over (the children laughed at, and one fond of
    /// them resents it). By morning the food is gone, and someone who sees the place says what they think. Real things
    /// only: no building of its own.
    /// </summary>
    public class FaithLittleShrine : BetweenKind
    {
        public override OwnHappening H => OwnHappening.FaithLittleShrine;

        /// <summary>Two children free now, the first leaning toward a god (that god).</summary>
        public static (Pawn a, Pawn b, God g) Children(Map map)
        {
            var kids = OwnBetween.People(map).Where(p => p.DevelopmentalStage.Child() && OwnBetween.FreeNow(p)).ToList();
            var a = kids.FirstOrDefault(p => OwnBetween.HeldBy(p) != null && OwnHappenings.Acts(OwnBetween.HeldBy(p), map));
            var b = a == null ? null : kids.Where(p => p != a).OrderBy(p => p.Position.DistanceToSquared(a.Position)).FirstOrDefault();
            return (a, b, OwnBetween.HeldBy(a));
        }

        /// <summary>A corner out at the edge, open to the sky and empty, both children can walk to.</summary>
        private static IntVec3 Corner(Map map, Pawn a, Pawn b)
        {
            var mid = OwnHappenings.Middle(map);
            bool Fits(IntVec3 c) => c.InBounds(map) && c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetEdifice(map) == null && c.GetFirstItem(map) == null
                                    && c.GetSlotGroup(map) == null && !c.InHorDistOf(mid, OwnHappeningsMath.ShrineEdgeMin) && a.CanReach(c, PathEndMode.OnCell, Danger.None)
                                    && b.CanReach(c, PathEndMode.OnCell, Danger.None);
            return CellFinder.TryFindRandomCellNear(mid, map, (int)OwnHappeningsMath.ShrineEdgeMax, Fits, out var cell) ? cell : IntVec3.Invalid;
        }

        public override void Hourly(OwnHappeningsBook b, God none, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.ShrineFrom || hour >= OwnHappeningsMath.ShrineTo) return;
            var (a, k, g) = Children(map);
            var why = OwnHappeningsMath.LittleShrineTrigger(a == null ? 0 : k == null ? 1 : 2, g != null);
            if (!OwnHappenings.Calls(b, H, why, g, map, now)) return;
            Begin(b, g, map, a, k);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn a, Pawn k)
        {
            var at = Corner(map, a, k);
            if (!at.IsValid) return false;
            var s = OwnBetween.Begin(b, OwnHappening.FaithLittleShrine, g, map, at, a, k, 3f);
            // The first child carries a stone (slag for a colony of the later ages) or a few sticks; the second part of a meal.
            var stone = FaithBetween.Chunk(a, OwnHappenings.Age >= 2) ?? FaithBetween.Wood(a);
            if (stone == null || !OwnHappenings.Carry(a, stone, at, stone.def == ThingDefOf.WoodLog ? 3 : 1)) OwnBetween.Send(a, map, at, 2f, 1);
            else s.thing = stone;
            var meal = FaithBetween.Meal(map, k);
            var beside = OwnHappenings.Beside(map, at, k);
            if (meal != null && beside.IsValid && OwnHappenings.Carry(k, meal, beside, 1)) s.word = meal.def.defName;
            else OwnBetween.Send(k, map, at, 2f, 2);
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var a = s.a;
            var k = s.b;
            if (s.stage == 1)
            {
                if (!OwnBetween.Still(a, map) || !OwnBetween.Still(k, map))
                {
                    s.stage = -1;
                    return;
                }
                bool busy = a.CurJobDef == JobDefOf.HaulToCell || a.CurJobDef == JobDefOf.Goto || k.CurJobDef == JobDefOf.HaulToCell || k.CurJobDef == JobDefOf.Goto;
                if (!busy && a.Position.InHorDistOf(s.at, 4f) && k.Position.InHorDistOf(s.at, 4f) && Together.Can(a, k))
                {
                    OwnBetween.Face(a, k, 1800);
                    OwnBetween.Say(a, H, "build", g, k);
                    OwnBetween.Say(k, H, "leave", g, a);
                    var meal = s.word.NullOrEmpty() ? null : FaithBetween.LyingNear(map, s.at, DefDatabase<ThingDef>.GetNamedSilentFail(s.word), 2.5f);
                    string what = s.thing != null ? FaithBetween.AThing(s.thing) : "CC_GodB_FaithLittleShrine_Sticks".Translate().Resolve();
                    OwnBetween.Card(H, g, map, s.at, false, meal != null ? null : "TextNoMeal", a.Named("A"), k.Named("B"), what.Named("WHAT"));
                    // Who comes to it: one who holds the god, the keeper of the colony's book, or a scoffer.
                    var people = OwnBetween.People(map).Where(p => OwnBetween.Grown(p) && OwnBetween.FreeNow(p) && p.Position.InHorDistOf(s.at, 50f)).ToList();
                    var helper = people.Where(p => OwnBetween.Holds(p, g)).OrderBy(p => p.Position.DistanceToSquared(s.at)).FirstOrDefault();
                    var keeper = GodLore.Keeper(out _);
                    if (keeper == null || !people.Contains(keeper) || OwnBetween.Doubts(keeper)) keeper = null;
                    var scoffer = people.Where(OwnBetween.Doubts).OrderBy(p => p.Position.DistanceToSquared(s.at)).FirstOrDefault();
                    var v = OwnHappeningsMath.Visitor(helper != null, keeper != null, scoffer != null, Rand.Value);
                    var who = v == ShrineVisitor.Helper ? helper : v == ShrineVisitor.Keeper ? keeper : v == ShrineVisitor.Scoffer ? scoffer : null;
                    if (who != null && OwnBetween.Send(who, map, s.at, 2f, 2))
                    {
                        s.c = who;
                        s.num = (int)v;
                        s.stage = 2;
                        s.until = now + GenDate.TicksPerHour * 2;
                    }
                    else Morning(s, now);
                    OwnBetween.Wait(s, 200);
                    return;
                }
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 200);
                return;
            }
            if (s.stage == 2)
            {
                var v = (ShrineVisitor)s.num;
                var who = s.c;
                if (!OwnBetween.Still(who, map) || !OwnBetween.Still(a, map))
                {
                    Morning(s, now);
                    return;
                }
                if (who.Position.InHorDistOf(s.at, 4f) && a.Position.InHorDistOf(s.at, 6f) && Together.Can(who, a))
                {
                    OwnBetween.Face(who, a, 600);
                    if (v == ShrineVisitor.Helper)
                    {
                        OwnBetween.Say(who, H, "help", g, a);
                        OwnBetween.End(H, BetweenOutcome.Closer, a, who);
                    }
                    else if (v == ShrineVisitor.Keeper)
                    {
                        OwnBetween.Say(who, H, "name", g, a);
                        PawnGods.Told(a, who, g);
                        if (k != null) PawnGods.Told(k, who, g);
                        OwnBetween.End(H, BetweenOutcome.Kind, a, who);
                    }
                    else
                    {
                        OwnBetween.Say(who, H, "kick", g, a);
                        Scatter(map, s.at);
                        OwnBetween.Say(a, H, "cry", g, who);
                        OwnBetween.End(H, BetweenOutcome.Laughed, a, who);
                        // One fond of the children, near and with the scoffer, thinks the worse of them for it.
                        var fond = OwnBetween.People(map).FirstOrDefault(p => p != who && p != a && p != k && OwnBetween.Grown(p) && OwnBetween.Opinion(p, a) >= OwnHappeningsMath.FondFrom
                                                                             && Together.Can(p, who));
                        if (fond != null) OwnBetween.End(H, BetweenOutcome.Jealous, fond, who);
                    }
                    Morning(s, now);
                    return;
                }
                if (who.CurJobDef != JobDefOf.Goto) OwnBetween.Send(who, map, s.at, 1f, 2);
                if (now > s.until) Morning(s, now);
                OwnBetween.Wait(s, 200);
                return;
            }
            // The morning: the food is gone from the little place, and someone who sees it says what they think.
            if (s.stage == 3)
            {
                var meal = s.word.NullOrEmpty() ? null : FaithBetween.LyingNear(map, s.at, DefDatabase<ThingDef>.GetNamedSilentFail(s.word), 3.5f);
                if (meal != null && !meal.Destroyed) meal.Destroy();
                // Only food gone is told of; with none left there, the morning has nothing to say.
                else s.word = null;
                s.stage = 4;
                s.until = now + GenDate.TicksPerHour * 6;
                OwnBetween.Wait(s, 250);
                return;
            }
            var seer = OwnHappenings.Seer(map, s.at, 20f);
            if (s.word == null)
            {
                if (a != null) OwnBetween.Book(H, g, map, s.at, GodActKind.Act, a, (k?.LabelShort ?? "?").Named("OTHER"));
                s.stage = -1;
                return;
            }
            if (seer != null)
            {
                OwnBetween.Say(seer, H, "gone", g);
                OwnBetween.Card(H, g, map, s.at, false, "TextGone", (a?.LabelShort ?? "?").Named("A"), (k?.LabelShort ?? "?").Named("B"));
                OwnBetween.Book(H, g, map, s.at, GodActKind.Act, seer, (a?.LabelShort ?? "?").Named("OTHER"));
                s.stage = -1;
                return;
            }
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 400);
        }

        /// <summary>The next step at first light.</summary>
        private static void Morning(BetweenScene s, int now)
        {
            s.stage = 3;
            int wait = OwnBetween.TicksToHour(s.map, 6);
            s.until = now + wait + GenDate.TicksPerHour * 8;
            OwnBetween.Wait(s, wait);
        }

        /// <summary>What was set up at the little place, kicked a step or two away.</summary>
        private static void Scatter(Map map, IntVec3 at)
        {
            foreach (var t in GenRadial.RadialDistinctThingsAround(at, map, 1.5f, true).Where(t => t.def.category == ThingCategory.Item).ToList())
            {
                var to = CellFinder.RandomClosewalkCellNear(at, map, 3);
                if (!to.IsValid || to == t.Position) continue;
                try
                {
                    t.DeSpawn();
                    if (!GenPlace.TryPlaceThing(t, to, map, ThingPlaceMode.Near)) GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("gods between them: the little place kicked over", e);
                }
            }
        }

        public override string Fire(OwnHappeningsBook b, God none, Map map)
        {
            var (a, k, g) = Children(map);
            if (a == null || k == null) return "two children of the colony, one leaning toward a god, are needed (children come with Biotech).";
            return Begin(b, g, map, a, k) ? a.LabelShort + " and " + k.LabelShort + " set up a little place at the edge." : "no open corner at the edge both can reach.";
        }
    }

    /// <summary>
    /// The one who turned (OwnHappening.FaithTurned, D7): a day after a crisis of faith turned someone from one god to another
    /// (FaithHeart.TurnAway), they carry a thing of their old rite (a stone from the rock, wood from the tree) to the new god's
    /// place and set it down. One who holds the new god walks the last steps with them and says the god's words (closer); one
    /// who holds the old god meets them there: a friend understands (closer), others call it a betrayal (a quarrel, each held
    /// to their own the harder). A priest who holds the old god asks them, in one sentence, to think again.
    /// </summary>
    public class FaithTurned : BetweenKind
    {
        public override OwnHappening H => OwnHappening.FaithTurned;

        /// <summary>One who turned lately at this home and hasn't walked it out yet: who, from which god, to which, and its key.</summary>
        public static (Pawn p, God from, God to, string key) Turning(OwnHappeningsBook b, Map map, int now)
        {
            var wb = Worship.Book;
            if (wb == null) return (null, null, null, null);
            int since = now - Mathf.RoundToInt(OwnHappeningsMath.TurnedDays * GenDate.TicksPerDay);
            for (int i = wb.marks.Count - 1; i >= 0; i--)
            {
                var m = wb.marks[i];
                if (m == null || m.what != "turned" || m.tick < since || now - m.tick < GenDate.TicksPerDay / 3 || m.pawn == null || m.pawn.Map != map) continue;
                string key = "turned|" + m.pawn.thingIDNumber + "|" + m.tick;
                if (b.between.answered.Contains(key)) continue;
                var to = Gods.ById(m.god);
                God from = null;
                // The god they turned from: the crisis just before it.
                for (int j = i - 1; j >= 0 && from == null; j--)
                {
                    var c = wb.marks[j];
                    if (c != null && c.pawn == m.pawn && c.what == "crisis") from = Gods.ById(c.god);
                }
                if (to != null && from != null && from != to) return (m.pawn, from, to, key);
            }
            return (null, null, null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God none, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.TurnedFrom || hour >= OwnHappeningsMath.TurnedTo) return;
            var (p, from, to, key) = Turning(b, map, now);
            if (p == null || !OwnBetween.FreeNow(p) || !OwnHappenings.Acts(to, map)) return;
            b.between.answered.Add(key);
            b.between.Trim();
            if (!OwnBetween.Follows(b, H, to, map, OwnHappeningsMath.TurnedChance)) return;
            Begin(b, map, p, from, to);
        }

        private static bool Begin(OwnHappeningsBook b, Map map, Pawn p, God from, God to)
        {
            var at = FaithBetween.PlaceOf(p, to, out _);
            if (!at.IsValid) return false;
            var s = OwnBetween.Begin(b, OwnHappening.FaithTurned, to, map, at, p, null, 4f);
            s.num = from.id << 3;
            // A thing of the old rite: a stone from the rock, wood from the tree; else nothing to carry.
            var rite = PawnGodsMath.RiteOf(from.Key);
            var thing = rite == BeliefRite.Rock ? FaithBetween.Chunk(p, false) : rite == BeliefRite.Tree ? FaithBetween.Wood(p) : null;
            var beside = OwnHappenings.Beside(map, at, p);
            if (thing != null && beside.IsValid && OwnHappenings.Carry(p, thing, beside, 1)) s.word = thing.def.defName;
            else OwnBetween.Send(p, map, at, 2f, 2);
            // One who holds the new god and one who holds the old come too.
            var people = OwnBetween.People(map).Where(x => x != p && OwnBetween.Grown(x) && OwnBetween.FreeNow(x)).ToList();
            s.c = OwnBetween.Believer(people, to);
            s.b = people.Where(x => x != s.c && OwnBetween.Holds(x, from)).OrderBy(x => x.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            if (s.c != null) OwnBetween.Send(s.c, map, at, 3f, 3);
            if (s.b != null) OwnBetween.Send(s.b, map, at, 3f, 3);
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var to = s.God;
            var from = From(s);
            var p = s.a;
            if (!OwnBetween.Still(p, map) || from == null)
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                bool walking = p.CurJobDef == JobDefOf.HaulToCell || p.CurJobDef == JobDefOf.Goto;
                if (!walking && p.Position.InHorDistOf(s.at, 3.5f))
                {
                    OwnHappenings.Stand(p, s.at, 2000);
                    var place = FaithBetween.PlaceAt(map, s.at);
                    var left = s.word.NullOrEmpty() ? null : FaithBetween.LyingNear(map, s.at, DefDatabase<ThingDef>.GetNamedSilentFail(s.word), 2.5f);
                    string what = FaithBetween.AThing(left);
                    // apart: alone at the new god's place, said to nobody in particular.
                    if (left != null)
                    {
                        OwnBetween.Say(p, H, "lay", to, null, what.Named("WHAT"));
                        FaithBetween.LeaveAsOffering(p, left, place, to);
                    }
                    else OwnBetween.Say(p, H, "kneel", to);
                    OwnBetween.Card(H, to, map, s.at, false, left != null ? null : "TextNothing", p.Named("A"), what.Named("WHAT"), FaithBetween.PlaceWords(place).Named("PLACE"));
                    OwnBetween.Book(H, to, map, s.at, GodActKind.Act, p);
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
            // At the place: the new god's own walks the last steps, the old god's meets them, the old god's priest asks once.
            var nu = s.c;
            var old = s.b;
            if (nu != null && !Said(s, 1) && OwnBetween.Still(nu, map) && nu.Position.InHorDistOf(p.Position, 4f) && Together.Can(nu, p))
            {
                Mark(s, 1);
                OwnBetween.Face(nu, p, 600);
                OwnBetween.Say(nu, H, "words", to, p);
                OwnBetween.End(H, BetweenOutcome.Closer, p, nu);
            }
            if (old != null && !Said(s, 2) && OwnBetween.Still(old, map) && old.Position.InHorDistOf(p.Position, 4f) && Together.Can(old, p))
            {
                Mark(s, 2);
                var o = OwnHappeningsMath.OldFaithful(OwnBetween.Opinion(old, p));
                OwnBetween.Face(old, p, 600);
                OwnBetween.Say(old, H, o == BetweenOutcome.Closer ? "understand" : "betray", from, p);
                OwnBetween.End(H, o, old, p, from, to);
            }
            var priest = Worship.Priest(map);
            if (!Said(s, 4) && priest != null && priest != p && priest != old && OwnBetween.Holds(priest, from) && Together.Can(priest, p))
            {
                Mark(s, 4);
                OwnBetween.Say(priest, H, "again", from, p);
            }
            bool done = (nu == null || Said(s, 1) || !OwnBetween.Still(nu, map)) && (old == null || Said(s, 2) || !OwnBetween.Still(old, map));
            if (done || now > s.until)
            {
                s.stage = -1;
                return;
            }
            if (nu != null && !Said(s, 1) && nu.CurJobDef != JobDefOf.Goto && OwnBetween.FreeNow(nu)) OwnBetween.Send(nu, map, p.Position, 1f, 2);
            if (old != null && !Said(s, 2) && old.CurJobDef != JobDefOf.Goto && OwnBetween.FreeNow(old)) OwnBetween.Send(old, map, p.Position, 1f, 2);
            OwnBetween.Wait(s, 200);
        }

        /// <summary>The god turned from and who has spoken at the place already, kept together in the scene's count (the god's id, three bits beside it).</summary>
        private static God From(BetweenScene s) => Gods.ById(s.num >> 3);

        private static bool Said(BetweenScene s, int bit) => (s.num & bit) != 0;

        private static void Mark(BetweenScene s, int bit) => s.num |= bit;

        public override string Fire(OwnHappeningsBook b, God none, Map map)
        {
            var (p, from, to, _) = Turning(b, map, OwnBetween.Now + GenDate.TicksPerDay);
            if (p == null)
            {
                p = OwnBetween.People(map).FirstOrDefault(x => OwnBetween.Grown(x) && OwnBetween.HeldBy(x) != null);
                to = OwnBetween.HeldBy(p);
                from = to == null ? null : Gods.Rivals(to).FirstOrDefault(x => x.kind != GodKind.Dead) ?? Gods.All.FirstOrDefault(x => x != to && x.kind != GodKind.Dead);
            }
            if (p == null || from == null || to == null) return "one of the colony who holds a god, and another god, are needed.";
            return Begin(b, map, p, from, to) ? p.LabelShort + " carries a thing of the old rite to the new god's place." : "no place of the new god they can reach.";
        }
    }

    /// <summary>
    /// The offering nobody's god took (OwnHappening.FaithTakenOffering, D9): late at night someone hungry, a child, or one
    /// with a greedy heart walks to a god's place and pockets what was left there, before the god's own small hours; only at
    /// a god whose way leaves no sign (OfferingWay.Vanish), so what the believers find in the morning is what anyone would.
    /// Someone awake sees it. In the morning the believers find the place empty and say the god took it, and the book writes
    /// it so (Offerings.Noticed, the same seam as any taking). The one who saw it tells a believer (the book's entry disputed
    /// beside it, and the believer goes to the taker, who is ashamed), or keeps it, a secret between the two that may come
    /// out later as secrets do (Rimfolk_Secret_OfferingTaken).
    /// </summary>
    public class FaithTakenOffering : BetweenKind
    {
        public override OwnHappening H => OwnHappening.FaithTakenOffering;

        public const string SecretName = "Rimfolk_Secret_OfferingTaken";

        private static bool Food(OfferingKind k) => k == OfferingKind.Meal || k == OfferingKind.Plant || k == OfferingKind.Meat || k == OfferingKind.Fish;

        /// <summary>One who would take it: hungry or a child for food, a greedy heart for anything.</summary>
        private static bool Takes(Pawn p, bool food) =>
            food && ((p.needs?.food?.CurLevelPercentage ?? 1f) < OwnHappeningsMath.HungryBelow || p.DevelopmentalStage.Child()) || p.story?.traits?.HasTrait(TraitDefOf.Greedy) == true;

        /// <summary>A gift waiting at a god's place at this home, one who would take it, and one awake who would see.</summary>
        public static (OfferingRecord o, God g, Pawn taker, Pawn witness) Waiting(Map map)
        {
            var wb = Worship.Book;
            if (wb == null) return (null, null, null, null);
            int home = Gods.HomeKey(map);
            var people = OwnBetween.People(map);
            foreach (var o in wb.offerings.Where(x => x != null && x.home == home && x.set && x.fate == OfferingFate.Waiting && x.thing != null && x.thing.Spawned && x.thing.Map == map))
            {
                var g = Gods.ById(o.god);
                if (g == null || OfferingWays.Of(g) != OfferingWay.Vanish) continue;
                var at = o.thing.Position;
                var taker = people.Where(p => OwnBetween.FreeNow(p) && !OwnBetween.Holds(p, g) && Takes(p, Food(o.kind)) && p.CanReach(at, PathEndMode.Touch, Danger.Some))
                    .OrderBy(p => p.Position.DistanceToSquared(at)).FirstOrDefault();
                if (taker == null) continue;
                var witness = people.Where(p => p != taker && OwnBetween.Grown(p) && !p.InBed() && p.Position.InHorDistOf(at, 40f)).OrderBy(p => p.Position.DistanceToSquared(at)).FirstOrDefault();
                if (witness != null) return (o, g, taker, witness);
            }
            return (null, null, null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God none, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < OwnHappeningsMath.TakenFrom || hour >= OwnHappeningsMath.TakenTo) return;
            var (o, g, taker, witness) = Waiting(map);
            var why = OwnHappeningsMath.TakenTrigger(o != null, taker != null, witness != null);
            if (!OwnHappenings.Calls(b, H, why, g, map, now)) return;
            Begin(b, g, map, o, taker, witness);
        }

        private static void Begin(OwnHappeningsBook b, God g, Map map, OfferingRecord o, Pawn taker, Pawn witness)
        {
            var s = OwnBetween.Begin(b, OwnHappening.FaithTakenOffering, g, map, o.thing.Position, taker, witness, 3f);
            s.thing = o.thing;
            s.word = o.label;
            s.stage = 1;
            OwnBetween.Send(taker, map, o.thing.Position, 1.5f, 1);
        }

        private static OfferingRecord Record(BetweenScene s) => Worship.Book?.offerings.FirstOrDefault(x => x != null && x.thing == s.thing && x.fate == OfferingFate.Waiting);

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var taker = s.a;
            var witness = s.b;
            if (!OwnBetween.Still(taker, map) || !OwnBetween.Still(witness, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                var o = Record(s);
                if (o == null)
                {
                    // The god's own way came first, or someone else took it.
                    s.stage = -1;
                    return;
                }
                if (taker.CurJobDef != JobDefOf.Goto && taker.Position.InHorDistOf(s.at, 1.9f))
                {
                    var t = o.thing;
                    t.SetForbidden(false, false);
                    if (t.Spawned) t.DeSpawn();
                    if (!taker.inventory.innerContainer.TryAdd(t))
                    {
                        GenPlace.TryPlaceThing(t, s.at, map, ThingPlaceMode.Near);
                        s.stage = -1;
                        return;
                    }
                    // Gone as the god's would be, for the believers to find empty in the morning (Offerings.Noticed).
                    o.thing = null;
                    o.fate = OfferingFate.Taken;
                    o.noticed = false;
                    var place = Worship.Book.ShrineOf(o.shrine);
                    if (place != null && place.lastAt == o.placed) place.lastFate = OfferingFate.Taken;
                    // apart: the taker alone at the place in the night; the one who saw it looks on from where they are.
                    OwnBetween.Say(taker, H, "take", g);
                    OwnHappenings.Stand(witness, taker.Position, 400);
                    OwnBetween.Say(witness, H, "saw", g);
                    OwnBetween.Card(H, g, map, s.at, false, null, taker.Named("A"), witness.Named("B"), (s.word ?? "").Named("WHAT"),
                        FaithBetween.PlaceWords(o.shrine).Named("PLACE"));
                    s.num = OwnHappeningsMath.Tells(OwnBetween.Opinion(witness, taker), OwnBetween.Holds(witness, g), Rand.Value) ? 1 : 0;
                    s.stage = 2;
                    int wait = OwnBetween.TicksToHour(map, 11);
                    s.until = now + wait + GenDate.TicksPerHour * 9;
                    OwnBetween.Wait(s, wait);
                    return;
                }
                if (taker.CurJobDef != JobDefOf.Goto) OwnBetween.Send(taker, map, s.at, 1f, 1);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 150);
                return;
            }
            if (s.stage == 2)
            {
                // Late morning, the place found empty by now: the one who saw it goes to a believer, or to the taker.
                if (!OwnBetween.FreeNow(witness))
                {
                    if (now > s.until) s.stage = -1;
                    OwnBetween.Wait(s, 600);
                    return;
                }
                var to = s.num == 1 ? OwnBetween.Believer(OwnBetween.People(map).Where(p => p != taker && p != witness && OwnBetween.Grown(p)), g) : taker;
                if (to == null)
                {
                    s.stage = -1;
                    return;
                }
                s.c = to;
                OwnBetween.Send(witness, map, to.Position, 2f, 2);
                s.stage = 3;
                OwnBetween.Wait(s, 200);
                return;
            }
            var other = s.c;
            if (!OwnBetween.Still(other, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 3)
            {
                if (witness.Position.InHorDistOf(other.Position, 4f) && Together.Can(witness, other))
                {
                    OwnBetween.Face(witness, other, 600);
                    if (s.num == 1)
                    {
                        string line = OwnBetween.Say(witness, H, "tell", g, other, taker.LabelShort.Named("OFFENDER"), (s.word ?? "").Named("WHAT"));
                        OwnBetween.Say(other, H, "angry", g, witness, taker.LabelShort.Named("OFFENDER"));
                        // The book: others hold, with the one who saw it, that it was no god.
                        OwnBetween.Doubted(witness, g, line);
                        OwnBetween.Book(H, g, map, s.at, GodActKind.Act, witness, taker.LabelShort.Named("OTHER"), (s.word ?? "").Named("WHAT"));
                        OwnBetween.Send(other, map, taker.Position, 2f, 2);
                        s.stage = 4;
                    }
                    else
                    {
                        OwnBetween.Say(witness, H, "keep", g, taker);
                        OwnBetween.Say(taker, H, "thanks", g, witness);
                        Keep(taker, witness);
                        OwnBetween.End(H, BetweenOutcome.Kind, taker, witness);
                        s.stage = -1;
                    }
                    OwnBetween.Wait(s, 200);
                    return;
                }
                if (witness.CurJobDef != JobDefOf.Goto) OwnBetween.Send(witness, map, other.Position, 1f, 2);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 200);
                return;
            }
            // The believer goes to the taker.
            if (other.Position.InHorDistOf(taker.Position, 4f) && Together.Can(other, taker))
            {
                OwnBetween.Face(other, taker, 600);
                OwnBetween.Say(other, H, "shame", g, taker);
                OwnBetween.Say(taker, H, "sorry", g, other);
                OwnBetween.End(H, BetweenOutcome.Ashamed, taker, other, null, null, (s.word ?? "").Named("WHAT"));
                s.stage = -1;
                return;
            }
            if (other.CurJobDef != JobDefOf.Goto) OwnBetween.Send(other, map, taker.Position, 1f, 2);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 200);
        }

        /// <summary>Kept between the two: a secret of the taker's, the one who saw it among those who know it (none if the taker already keeps one).</summary>
        private static void Keep(Pawn taker, Pawn witness)
        {
            var comp = CouncilGameComponent.Instance;
            var def = DefDatabase<SecretDef>.GetNamedSilentFail(SecretName);
            if (comp?.secrets == null || def == null || !comp.secrets.Give(taker, def)) return;
            var secret = comp.secrets.Of(taker);
            // together: the one who saw it has just told the taker so, face to face (the step checks Together.Can).
            if (secret != null && !secret.knowers.Contains(witness)) secret.knowers.Add(witness);
        }

        public override string Fire(OwnHappeningsBook b, God none, Map map)
        {
            var (o, g, taker, witness) = Waiting(map);
            if (o == null) return "a gift waiting at the place of a god whose way leaves no sign, one hungry, a child or greedy to take it, and one awake to see it, are needed.";
            Begin(b, g, map, o, taker, witness);
            return taker.LabelShort + " goes to take the " + o.label + "; " + witness.LabelShort + " sees it.";
        }
    }

    /// <summary>
    /// A gift at the other's place (OwnHappening.FaithGift, D13): some days after two of the colony quarrelled over their
    /// gods (any of the gods' quarrels between people), the kinder of them, or the one who thinks better of the other,
    /// carries something to the other's god's place and leaves it there (the god's as any gift, at a dedicated place). The
    /// other is told and comes, and the two make it up; a zealous believer of the giver's own god calls it disloyal (wary).
    /// </summary>
    public class FaithGift : BetweenKind
    {
        public override OwnHappening H => OwnHappening.FaithGift;

        /// <summary>The memories of a quarrel over the gods between two people.</summary>
        private static readonly string[] Quarrels = { "Rimfolk_Between_Quarrel", "Rimfolk_Belief_GodsQuarrel", "Rimfolk_Worship_Bickered" };

        /// <summary>One who quarrelled with another over their gods some days ago, and would give: the giver, the other, the other's god, its key.</summary>
        public static (Pawn x, Pawn y, God g, string key) Pair(OwnHappeningsBook b, Map map, bool anyAge = false)
        {
            var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
            foreach (var x in people.Where(OwnBetween.FreeNow).InRandomOrder())
            {
                var mems = x.needs?.mood?.thoughts?.memories?.Memories;
                if (mems == null) continue;
                foreach (var m in mems)
                {
                    if (m?.def == null || m.otherPawn == null || !Quarrels.Contains(m.def.defName) || !people.Contains(m.otherPawn)) continue;
                    float days = m.age / (float)GenDate.TicksPerDay;
                    if (!anyAge && (days < OwnHappeningsMath.GiftAfterDays || days > OwnHappeningsMath.GiftWithinDays)) continue;
                    var y = m.otherPawn;
                    var gy = OwnBetween.HeldBy(y);
                    if (gy == null || !OwnHappeningsMath.Gives(x.story?.traits?.HasTrait(TraitDefOf.Kind) == true, OwnBetween.Opinion(x, y), OwnBetween.Opinion(y, x))) continue;
                    string key = "gift|" + x.thingIDNumber + "|" + y.thingIDNumber + "|" + (OwnBetween.Now - m.age) / GenDate.TicksPerDay;
                    if (b.between.answered.Contains(key)) continue;
                    return (x, y, gy, key);
                }
            }
            return (null, null, null, null);
        }

        public override void Hourly(OwnHappeningsBook b, God none, Map map, int now)
        {
            int hour = OwnHappenings.Hour(map);
            if (hour < 8 || hour >= 19) return;
            var (x, y, g, key) = Pair(b, map);
            if (x == null || !OwnHappenings.Acts(g, map)) return;
            b.between.answered.Add(key);
            b.between.Trim();
            if (!OwnBetween.Follows(b, H, g, map, OwnHappeningsMath.GiftChance)) return;
            Begin(b, g, map, x, y);
        }

        private static bool Begin(OwnHappeningsBook b, God g, Map map, Pawn x, Pawn y)
        {
            var at = FaithBetween.PlaceOf(x, g, out _);
            if (!at.IsValid) return false;
            var s = OwnBetween.Begin(b, OwnHappening.FaithGift, g, map, at, x, y, 4f);
            s.num = OwnBetween.HeldBy(x)?.id ?? -1;
            var meal = FaithBetween.Meal(map, x);
            var beside = OwnHappenings.Beside(map, at, x);
            if (meal != null && beside.IsValid && OwnHappenings.Carry(x, meal, beside, 1)) s.word = meal.def.defName;
            else OwnBetween.Send(x, map, at, 2f, 2);
            s.stage = 1;
            return true;
        }

        public override void Step(OwnHappeningsBook b, BetweenScene s, int now)
        {
            var map = s.map;
            var g = s.God;
            var x = s.a;
            var y = s.b;
            if (!OwnBetween.Still(x, map) || !OwnBetween.Still(y, map))
            {
                s.stage = -1;
                return;
            }
            if (s.stage == 1)
            {
                bool walking = x.CurJobDef == JobDefOf.HaulToCell || x.CurJobDef == JobDefOf.Goto;
                if (!walking && x.Position.InHorDistOf(s.at, 3.5f))
                {
                    OwnHappenings.Stand(x, s.at, 2000);
                    var place = FaithBetween.PlaceAt(map, s.at);
                    var left = s.word.NullOrEmpty() ? null : FaithBetween.LyingNear(map, s.at, DefDatabase<ThingDef>.GetNamedSilentFail(s.word), 2.5f);
                    string what = FaithBetween.AThing(left);
                    // apart: alone at the other's god's place, said to nobody in particular.
                    if (left != null)
                    {
                        OwnBetween.Say(x, H, "leave", g, null, what.Named("WHAT"), y.LabelShort.Named("FRIEND"));
                        FaithBetween.LeaveAsOffering(x, left, place, g);
                    }
                    else OwnBetween.Say(x, H, "stand", g, null, y.LabelShort.Named("FRIEND"));
                    OwnBetween.Card(H, g, map, s.at, false, left != null ? null : "TextNothing", x.Named("A"), y.Named("B"), what.Named("WHAT"), FaithBetween.PlaceWords(place).Named("PLACE"));
                    // The other is told of it, and comes.
                    OwnBetween.Send(y, map, x.Position, 2f, 2);
                    s.stage = 2;
                    s.until = now + GenDate.TicksPerHour * 3;
                    OwnBetween.Wait(s, 200);
                    return;
                }
                if (!walking && now < s.until) OwnBetween.Send(x, map, s.at, 1f, 2);
                if (now > s.until) s.stage = -1;
                OwnBetween.Wait(s, 200);
                return;
            }
            if (y.Position.InHorDistOf(x.Position, 4f) && Together.Can(y, x))
            {
                OwnBetween.Face(y, x, 600);
                OwnBetween.Say(y, H, "thanks", g, x);
                OwnBetween.Say(x, H, "sorry", g, y);
                OwnBetween.End(H, BetweenOutcome.MadeUp, x, y);
                OwnBetween.Book(H, g, map, s.at, GodActKind.Act, y, x.LabelShort.Named("OTHER"));
                // A zealous believer of the giver's own god, there to see it, calls it disloyal.
                var own = Gods.ById(s.num);
                var zealot = own == null ? null : OwnBetween.People(map).FirstOrDefault(z => z != x && z != y && OwnBetween.Grown(z) && PawnGods.HoldOf(z, own) >= OwnHappeningsMath.ZealousFrom
                                                                                          && Together.Can(z, x));
                if (zealot != null)
                {
                    OwnBetween.Say(zealot, H, "disloyal", own, x);
                    OwnBetween.End(H, BetweenOutcome.Wary, zealot, x);
                }
                s.stage = -1;
                return;
            }
            if (y.CurJobDef != JobDefOf.Goto && OwnBetween.FreeNow(y)) OwnBetween.Send(y, map, x.Position, 1f, 2);
            if (now > s.until) s.stage = -1;
            OwnBetween.Wait(s, 200);
        }

        public override string Fire(OwnHappeningsBook b, God none, Map map)
        {
            var (x, y, g, _) = Pair(b, map, true);
            if (x == null)
            {
                var people = OwnBetween.People(map).Where(OwnBetween.Grown).ToList();
                y = people.FirstOrDefault(p => OwnBetween.HeldBy(p) != null);
                x = people.FirstOrDefault(p => p != y);
                g = OwnBetween.HeldBy(y);
            }
            if (x == null || y == null || g == null) return "two grown of the colony, one of them holding a god, are needed.";
            return Begin(b, g, map, x, y) ? x.LabelShort + " carries something to " + y.LabelShort + "'s god's place." : "no place of that god they can reach.";
        }
    }
}
