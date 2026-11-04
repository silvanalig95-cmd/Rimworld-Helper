using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What travellers eat by on the road. The game lets a colonist in a caravan eat only what their food policy allows,
    /// so a colony that keeps its survival meals and jelly for the road (a policy at home that doesn't allow them) sends a
    /// caravan out "out of food" with full packs. The player's journey did exactly that: six travellers on a policy that
    /// allowed none of the survival meals, insect jelly or agave they carried. The player's answer: one of the colony's own
    /// food policies, chosen once (in the journey planner or on a caravan), that the colony's people eat by while they are
    /// in a caravan, and their own again when they are home. A colonist whose policy the player changes on the road keeps
    /// that one. Without a road policy nothing changes; the planner and an alert say when the food carried isn't allowed.
    /// The caravans are looked at once an hour inside the journeys' own pass (<see cref="JourneyBook"/>).
    /// </summary>
    public static class RoadFood
    {
        /// <summary>The road's policy, while it is still in the colony's list (and journeys are on).</summary>
        public static FoodPolicy Policy
        {
            get
            {
                var book = JourneyBook.Instance;
                var p = book?.roadPolicy;
                if (p != null && !Exists(p)) book.roadPolicy = p = null;
                return JourneyBook.On ? p : null;
            }
        }

        public static bool Exists(FoodPolicy p) => p != null && Current.Game?.foodRestrictionDatabase?.AllFoodRestrictions?.Contains(p) == true;

        /// <summary>The road's policy by name, or "their own".</summary>
        public static string Label => Policy?.label ?? "CC_RoadFood_TheirOwn".Translate().Resolve();

        /// <summary>One of the colony's people who eat by a food policy the player sets (not a prisoner or a guest).</summary>
        public static bool Eats(Pawn p) =>
            p != null && !p.Dead && p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer && !p.IsPrisoner && p.needs?.food != null
            && p.foodRestriction != null && p.foodRestriction.Configurable && p.foodRestriction.CurrentFoodPolicy != null;

        /// <summary>
        /// Food a caravan would let them eat, their food policy aside: by the game's own caravan rules (nourishing, more than
        /// food for the desperate, something a caravan may pick, no drug for a teetotaler, and their kind can eat it).
        /// </summary>
        public static bool RoadEdible(ThingDef d, Pawn p)
        {
            var ing = d?.ingestible;
            if (ing == null || p == null || !d.IsNutritionGivingIngestible || !ing.canAutoSelectAsFoodForCaravan) return false;
            if (ing.preferability <= FoodPreferability.NeverForNutrition) return false;
            if (d.IsDrug && p.IsTeetotaler()) return false;
            return p.RaceProps.CanEverEat(d);
        }

        /// <summary>The policy they will eat by on the road: the road's, else their own.</summary>
        public static FoodPolicy OnTheRoad(Pawn p) => Policy ?? p?.foodRestriction?.CurrentFoodPolicy;

        // ---------------- The caravans, once an hour ----------------

        /// <summary>
        /// The colony's people at home again eat by their own policy; the road's policy changed or set back to their own
        /// reaches those on the road; and everyone in a caravan who isn't on the road's policy yet goes onto it.
        /// </summary>
        public static void Hour(JourneyBook book)
        {
            var road = Policy;
            var back = new List<Pawn>();
            for (int i = book.roadFood.Count - 1; i >= 0; i--)
            {
                var e = book.roadFood[i];
                var p = e.pawn;
                var tracker = p?.foodRestriction;
                // Gone, no longer ours, or the player gave them another policy on the road: theirs to keep.
                if (p == null || p.Dead || p.Faction != Faction.OfPlayer || tracker == null || tracker.CurrentFoodPolicy != e.road)
                {
                    book.roadFood.RemoveAt(i);
                    continue;
                }
                bool home = p.Spawned && p.Map != null && p.Map.IsPlayerHome;
                if (home || road == null)
                {
                    if (Exists(e.own)) tracker.CurrentFoodPolicy = e.own;
                    if (home) back.Add(p);
                    book.roadFood.RemoveAt(i);
                }
                else if (road != e.road)
                {
                    tracker.CurrentFoodPolicy = road;
                    e.road = road;
                }
            }
            if (back.Count > 0)
                Notify.Message("CC_RoadFood_Home".Translate(Names(back)).Resolve(), new LookTargets(back), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            if (road == null) return;
            foreach (var c in Find.WorldObjects.Caravans)
            {
                if (c == null || !c.IsPlayerControlled) continue;
                var set = new List<Pawn>();
                foreach (var p in c.PawnsListForReading)
                {
                    if (!Eats(p) || p.foodRestriction.CurrentFoodPolicy == road || book.roadFood.Any(e => e.pawn == p)) continue;
                    book.roadFood.Add(new RoadFoodEntry { pawn = p, own = p.foodRestriction.CurrentFoodPolicy, road = road });
                    p.foodRestriction.CurrentFoodPolicy = road;
                    set.Add(p);
                }
                if (set.Count > 0)
                    Notify.Message("CC_RoadFood_Set".Translate(Names(set), road.label).Resolve(), new LookTargets(c), MessageTypeDefOf.NeutralEvent, Importance.Notable);
            }
        }

        /// <summary>The player chose the road's policy (null: their own): it reaches those on the road at once.</summary>
        public static void Choose(FoodPolicy policy)
        {
            var book = JourneyBook.Instance;
            if (book == null) return;
            book.roadPolicy = policy;
            JourneyBook.Touch();
            try
            {
                Hour(book);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("journeys: the food policy on the road", e);
            }
        }

        /// <summary>The choice: their own, or one of the colony's food policies.</summary>
        public static void Menu()
        {
            var db = Current.Game?.foodRestrictionDatabase;
            if (db == null) return;
            var current = Policy;
            var opts = new List<FloatMenuOption>
            {
                new FloatMenuOption(Chosen("CC_RoadFood_TheirOwn".Translate().Resolve(), current == null), () => Choose(null))
            };
            foreach (var f in db.AllFoodRestrictions)
            {
                var policy = f;
                opts.Add(new FloatMenuOption(Chosen(policy.label, policy == current), () => Choose(policy)));
            }
            Find.WindowStack.Add(new FloatMenu(opts, "CC_RoadFood_MenuTitle".Translate()));
        }

        private static string Chosen(string label, bool chosen) => chosen ? "CC_RoadFood_Chosen".Translate(label).Resolve() : label;

        // ---------------- What they may eat ----------------

        /// <summary>Of the food given, what they could eat on the road (their policy aside), and of that what the policy bars.</summary>
        public static (List<ThingDef> edible, List<ThingDef> barred) Split(Pawn p, IEnumerable<ThingDef> foods, FoodPolicy policy)
        {
            var edible = foods.Where(d => RoadEdible(d, p)).Distinct().ToList();
            var barred = policy == null ? new List<ThingDef>() : edible.Where(d => !policy.Allows(d)).ToList();
            return (edible, barred);
        }

        /// <summary>
        /// The planner's notes on the food on the list: who may not eat it by the policy they'll eat by on the road (none of
        /// it, or only some), and the road's policy when one is chosen. Each with whether it is a warning, and how bad.
        /// </summary>
        public static List<(string text, int level)> PlanNotes(List<Pawn> travellers, List<ThingDef> foods)
        {
            var notes = new List<(string, int)>();
            var road = Policy;
            var groups = new Dictionary<(FoodPolicy, bool), (List<Pawn> who, HashSet<ThingDef> barred)>();
            foreach (var p in travellers.Where(Eats))
            {
                var policy = OnTheRoad(p);
                var (edible, barred) = Split(p, foods, policy);
                if (edible.Count == 0 || barred.Count == 0) continue;
                var key = (policy, barred.Count == edible.Count);
                if (!groups.TryGetValue(key, out var g)) groups[key] = g = (new List<Pawn>(), new HashSet<ThingDef>());
                g.who.Add(p);
                foreach (var d in barred) g.barred.Add(d);
            }
            foreach (var kv in groups)
            {
                string who = Names(kv.Value.who);
                string what = Things(kv.Value.barred);
                string policy = kv.Key.Item1.label;
                if (kv.Key.Item2)
                    notes.Add(((road == null ? "CC_RoadFood_NoneOwn" : "CC_RoadFood_NoneRoad").Translate(who, what, policy).Resolve(), 2));
                else
                    notes.Add(("CC_RoadFood_Some".Translate(who, what, policy).Resolve(), 1));
            }
            if (road != null && travellers.Any(Eats)) notes.Add(("CC_RoadFood_PlanRoad".Translate(road.label).Resolve(), 0));
            return notes;
        }

        /// <summary>A caravan's people who carry food but may eat none of it by their policy, with the food and the policies.</summary>
        public static List<(Pawn pawn, List<ThingDef> barred)> Hungry(Caravan c)
        {
            var list = new List<(Pawn, List<ThingDef>)>();
            var foods = CaravanInventoryUtility.AllInventoryItems(c).Where(t => t.def.IsNutritionGivingIngestible).Select(t => t.def).Distinct().ToList();
            if (foods.Count == 0) return list;
            foreach (var p in c.PawnsListForReading)
            {
                if (!Eats(p)) continue;
                var (edible, barred) = Split(p, foods, p.foodRestriction.CurrentFoodPolicy);
                if (edible.Count > 0 && barred.Count == edible.Count) list.Add((p, barred));
            }
            return list;
        }

        public static string Names(IEnumerable<Pawn> pawns) => TextUtil.JoinAnd(pawns.Select(p => p.LabelShort).ToList());

        /// <summary>Up to three things by name, then "and more".</summary>
        public static string Things(IEnumerable<ThingDef> defs)
        {
            var labels = defs.Select(d => d.label).Distinct().ToList();
            if (labels.Count > 3) labels = labels.Take(3).Concat(new[] { "CC_RoadFood_More".Translate().Resolve() }).ToList();
            return TextUtil.JoinAnd(labels);
        }

        /// <summary>The caravan's button: the road's food policy, chosen there.</summary>
        public static Command Button() => new Command_Action
        {
            defaultLabel = "CC_RoadFood_Button".Translate(Label).Resolve(),
            defaultDesc = "CC_RoadFood_Button_Tip".Translate().Resolve(),
            icon = RoadFoodIcon.Icon,
            action = Menu
        };
    }

    [StaticConstructorOnStartup]
    public static class RoadFoodIcon
    {
        public static readonly Texture2D Icon = ThingDefOf.MealSurvivalPack?.uiIcon ?? BaseContent.BadTex;
    }

    /// <summary>A player caravan has the button for the food policy on the road (Journeys/RoadFood.cs).</summary>
    [HarmonyPatch(typeof(Caravan), nameof(Caravan.GetGizmos))]
    public static class Patch_RoadFoodButton
    {
        public static void Postfix(Caravan __instance, ref IEnumerable<Gizmo> __result)
        {
            if (!RimfolkPatches.On || !JourneyBook.On || __instance == null || !__instance.IsPlayerControlled || !__instance.PawnsListForReading.Any(RoadFood.Eats)) return;
            __result = __result.Concat(new[] { RoadFood.Button() });
        }
    }

    /// <summary>"Food they may not eat": a caravan's people carry food, but their food policy allows none of it.</summary>
    public class Alert_RoadFood : Alert
    {
        private readonly List<Caravan> culprits = new List<Caravan>();
        private string explanation = "";
        private int lookedAt = -1;

        public Alert_RoadFood()
        {
            defaultLabel = "CC_RoadFood_Alert".Translate();
            defaultPriority = AlertPriority.High;
        }

        public override TaggedString GetExplanation() => explanation;

        public override AlertReport GetReport()
        {
            if (!JourneyBook.On || Find.WorldObjects == null) return false;
            int now = Find.TickManager.TicksGame;
            if (now / Sched.RareTicks != lookedAt)
            {
                lookedAt = now / Sched.RareTicks;
                culprits.Clear();
                var parts = new List<string>();
                try
                {
                    foreach (var c in Find.WorldObjects.Caravans)
                    {
                        if (c == null || !c.IsPlayerControlled) continue;
                        var hungry = RoadFood.Hungry(c);
                        if (hungry.Count == 0) continue;
                        culprits.Add(c);
                        foreach (var g in hungry.GroupBy(h => h.pawn.foodRestriction.CurrentFoodPolicy))
                            parts.Add("CC_RoadFood_AlertLine".Translate(c.Label, RoadFood.Names(g.Select(h => h.pawn)), RoadFood.Things(g.SelectMany(h => h.barred)), g.Key.label).Resolve());
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("journeys: food they may not eat", e);
                }
                explanation = parts.Count == 0 ? "" : string.Join("\n\n", parts) + "\n\n" + "CC_RoadFood_AlertWhat".Translate().Resolve();
            }
            return culprits.Count > 0 ? AlertReport.CulpritsAre(culprits) : false;
        }
    }
}
