using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Two wishes, one pile of steel (the content update's A4, the player's go of 20 November 2026): two colonists' open
    // wishes each for a thing to be built or made, each needing the same stuff (steel, wood, components, cloth: the wish's
    // own thing and what it costs), and the stores holding enough for either but not both. The quartermaster speaks in it
    // when there is one (A2). A is the one who asked first. Answers: first come, first served (A's), B's first, or neither
    // until the stores hold more. The first is promised as the council's own Promise does it (its promise's own play-out
    // runs: the plan laid in their room, and so on); whoever waits has their deadline moved on three days and is told why
    // face to face, by the quartermaster or the other asker. The rules are OneSteelMath.

    /// <summary>Shared look-ups for the wishes' costs.</summary>
    public static class OneSteel
    {
        /// <summary>
        /// What a wished-for thing costs to make, by the stuff it takes: its fixed costs, and its stuff (the one of those it can
        /// be made of that the stores hold most of). Empty for a thing made of nothing.
        /// </summary>
        public static Dictionary<ThingDef, int> Cost(ThingDef def, Map map)
        {
            var cost = new Dictionary<ThingDef, int>();
            if (def == null || map == null) return cost;
            try
            {
                if (def.costList != null)
                    foreach (var c in def.costList)
                        if (c?.thingDef != null && c.count > 0) cost[c.thingDef] = (cost.TryGetValue(c.thingDef, out int n) ? n : 0) + c.count;
                if (def.MadeFromStuff && def.costStuffCount > 0)
                {
                    var stuff = GenStuff.AllowedStuffsFor(def).OrderByDescending(s => map.resourceCounter.GetCount(s)).FirstOrDefault();
                    if (stuff != null) cost[stuff] = (cost.TryGetValue(stuff, out int n) ? n : 0) + def.costStuffCount;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("one pile of steel: cost", e);
            }
            return cost;
        }

        /// <summary>An open wish of someone at home on this map for a thing that costs something to make, not yet promised.</summary>
        public static bool Fits(Wish w, Map map) =>
            w?.def != null && w.state == WishState.Open && !w.promised && w.thingDef != null && w.pawn != null && w.pawn.Spawned && w.pawn.Map == map
            && !w.pawn.IsQuestLodger() && w.petition < 0;

        public static Wish ById(int id) => id < 0 ? null : CouncilGameComponent.Instance?.wishes.FirstOrDefault(w => w != null && w.id == id);

        /// <summary>Two such wishes of two people, and the stuff only one can have, or false.</summary>
        public static bool Find(Map map, out Wish first, out Wish second, out ThingDef stuff, out int have, bool debug = false)
        {
            first = second = null;
            stuff = null;
            have = 0;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || map == null) return false;
            var open = comp.wishes.Where(w => Fits(w, map)).ToList();
            for (int i = 0; i < open.Count; i++)
            {
                var ca = Cost(open[i].thingDef, map);
                if (ca.Count == 0) continue;
                for (int j = i + 1; j < open.Count; j++)
                {
                    if (open[j].pawn == open[i].pawn) continue;
                    var cb = Cost(open[j].thingDef, map);
                    foreach (var kv in ca)
                    {
                        if (!cb.TryGetValue(kv.Key, out int needB)) continue;
                        int n = map.resourceCounter.GetCount(kv.Key);
                        if (!OneSteelMath.OneOnly(n, kv.Value, needB) && !(debug && kv.Value >= 1 && needB >= 1)) continue;
                        first = open[i];
                        second = open[j];
                        stuff = kv.Key;
                        have = n;
                        return true;
                    }
                }
            }
            return false;
        }
    }

    /// <summary>A the asker of one wish, B the other's; {THING} the stuff, {COUNT} what the stores hold, {WISHA} and {WISHB} the wishes.</summary>
    public class DilemmaWorker_OneSteel : DilemmaWorker
    {
        internal static bool DebugAny;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!ColonyCouncilMod.Settings.enableWishes || !CanHappenNow(map)) return false;
            if (map.mapPawns.FreeColonistsSpawnedCount < def.minColonists) return false;
            if (!OneSteel.Find(map, out var wa, out var wb, out var stuff, out int have, DebugAny)) return false;
            // A is whichever asked first (the letter reads in that order), B the other.
            if (!OneSteelMath.AFirst(wa.createdTick, wb.createdTick)) (wa, wb) = (wb, wa);
            ctx = new DilemmaContextLife
            {
                def = def, map = map, a = wa.pawn, b = wb.pawn, thing = stuff, count = have, wishA = wa.id, wishB = wb.id, labelA = wa.LabelInline, labelB = wb.LabelInline
            };
            return true;
        }
    }

    /// <summary>
    /// Which of the two wishes goes first (A4's answers, <see cref="first"/> "A", the one who asked first, "B", or "none",
    /// both wait for more): the first is promised as the council's Promise does it, and the other's deadline moves on three
    /// days (both, with none); whoever waits is told why face to face by the quartermaster (else the other wish's asker), and
    /// takes it as waiting a turn, not as being passed over.
    /// </summary>
    public class DilemmaEffect_WishFirst : DilemmaEffect
    {
        public string first = "A";

        public override bool ChanceEach => false;

        private static ThoughtDef Thought(string name) => DefDatabase<ThoughtDef>.GetNamedSilentFail(name);

        /// <summary>Whose wish is promised (null: nobody's), and who waits.</summary>
        private void Sides(DilemmaContext ctx, out Pawn winner, out List<Pawn> waits)
        {
            var w = first == "A" ? ctx.a : first == "B" ? ctx.b : null;
            winner = w;
            waits = new[] { ctx.a, ctx.b }.Where(p => p != null && p != w).ToList();
        }

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (!(ctx is DilemmaContextLife l) || ctx.a == null || ctx.b == null) return;
            Sides(ctx, out var winner, out var waits);
            string waiting = TextUtil.JoinAnd(waits.Select(p => p.LabelShort).ToList());
            if (winner != null)
                sheet.Play("CC_Impact_WishFirst".Translate(winner.LabelShort, winner == ctx.a ? l.labelA : l.labelB, waiting, ImpactMath.Number(OneSteelMath.WaitDays)).Resolve(),
                    "CC_Impact_WishFirstBrief".Translate(winner.LabelShort).Resolve());
            else
                sheet.Play("CC_Impact_WishBothWait".Translate(waiting, ImpactMath.Number(OneSteelMath.WaitDays)).Resolve(), "CC_Impact_WishBothWaitBrief".Translate().Resolve());
            var up = Thought("Rimfolk_Life_FirstInLine");
            var down = Thought("Rimfolk_Life_WaitMyTurn");
            if (winner != null && up != null) sheet.Thought(new List<Pawn> { winner }, winner.LabelShort, up, p => null);
            if (down != null) sheet.Thought(waits, waiting, down, p => null);
        }

        public override void Apply(DilemmaContext ctx)
        {
            if (!(ctx is DilemmaContextLife l) || ctx.a == null || ctx.b == null) return;
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            Sides(ctx, out var winner, out var waits);
            if (winner != null && OneSteel.ById(winner == ctx.a ? l.wishA : l.wishB) is Wish w && w.state == WishState.Open && !w.promised) comp.Promise(w);
            if (winner != null) CouncilGameComponent.GiveThought(winner, Thought("Rimfolk_Life_FirstInLine"));
            string thing = ctx.thing?.label ?? "";
            foreach (var p in waits)
            {
                if (OneSteel.ById(p == ctx.a ? l.wishA : l.wishB) is Wish ww && ww.state == WishState.Open)
                    ww.expiresTick += Mathf.RoundToInt(OneSteelMath.WaitDays * GenDate.TicksPerDay);
                CouncilGameComponent.GiveThought(p, Thought("Rimfolk_Life_WaitMyTurn"));
                // Told why, face to face: the quartermaster when there is one at home, else the other wish's asker.
                var teller = ctx.holder != null && ctx.holder != p && !ctx.holder.Dead ? ctx.holder : p == ctx.a ? ctx.b : ctx.a;
                if (teller == null || teller.Dead || p.Dead) continue;
                var who = p;
                var by = teller;
                string pool = winner == null ? "Rimfolk_Pool_LifeBothWait" : "Rimfolk_Pool_LifeYourTurn";
                Together.When("onesteel|" + who.thingIDNumber + "|" + Find.TickManager.TicksGame, by, who, () =>
                {
                    string line = Interplay.Line(pool, by, null, who.Named("OTHER"), thing.Named("THING"));
                    if (!line.NullOrEmpty()) Speech.Say(by, line, who);
                }, 48f, 6f);
            }
        }
    }
}
