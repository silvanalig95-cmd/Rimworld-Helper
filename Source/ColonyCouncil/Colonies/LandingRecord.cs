using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// One piece of the scenario's starting kit, as the game made it: the thing itself (a reference: it is carried, never
    /// copied), what it is and how many there were, and where it came from (<see cref="LandingKind"/>). The thing may be
    /// gone (used, built, merged into another stack); its def and count stay, so the kit is never counted larger.
    /// </summary>
    public class LandingPiece : IExposable
    {
        public Thing thing;
        public ThingDef def;
        public ThingDef stuff;
        public int count;
        public LandingKind kind;

        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_Defs.Look(ref def, "def");
            Scribe_Defs.Look(ref stuff, "stuff");
            Scribe_Values.Look(ref count, "count");
            Scribe_Values.Look(ref kind, "kind");
        }

        /// <summary>What a thing is for the kit's count: a minified thing counts as what it holds.</summary>
        public static ThingDef DefOf(Thing t) => (t as MinifiedThing)?.InnerThing?.def ?? t?.def;

        public static ThingDef StuffOf(Thing t) => (t as MinifiedThing)?.InnerThing?.Stuff ?? t?.Stuff;

        public static LandingPiece Of(Thing t, LandingKind kind) => new LandingPiece { thing = t, def = DefOf(t), stuff = StuffOf(t), count = t.stackCount, kind = kind };

        /// <summary>The group a piece counts in: the same thing of the same stuff, going the same way.</summary>
        public string Group => (def?.defName ?? "?") + "/" + (stuff?.defName ?? "") + "/" + LandingMath.PlaceOf(kind);
    }

    /// <summary>
    /// What Rimfolk saw of the game's start, for choosing another landing site (Colonies/Landing.cs): the home it landed on,
    /// when, the start spot, how the people arrived, the map's generator, the starting kit (<see cref="LandingPiece"/>)
    /// and the map conditions the scenario set. Kept on the council's game component while the offer is open, and let go
    /// once the first days are over (it is saved only while it holds).
    /// </summary>
    public class LandingRecord : IExposable
    {
        /// <summary>The home the colony landed on (its settlement's id, <see cref="Homes.KeyOf"/>).</summary>
        public int home = HomesMath.None;
        /// <summary>When it landed: the game's start, or the last landing chosen.</summary>
        public int landedAt = -1;
        public IntVec3 start = IntVec3.Invalid;
        public PlayerPawnsArriveMethod method = PlayerPawnsArriveMethod.Standing;
        public MapGeneratorDef generator;
        public List<LandingPiece> kit = new List<LandingPiece>();
        public List<GameConditionDef> conditions = new List<GameConditionDef>();
        /// <summary>How many times another site was chosen.</summary>
        public int landings;

        public bool ByPods => method == PlayerPawnsArriveMethod.DropPods;

        /// <summary>The pieces still to be found: those used, built or merged away are let go (their count stays in <see cref="Totals"/>).</summary>
        public void Prune()
        {
            kit.RemoveAll(p => p == null || p.def == null);
            foreach (var p in kit)
                if (p.thing != null && p.thing.Destroyed)
                    p.thing = null;
        }

        /// <summary>The kit's count in each group, as the game made it.</summary>
        public Dictionary<string, int> Totals()
        {
            var d = new Dictionary<string, int>();
            foreach (var p in kit)
            {
                if (p?.def == null) continue;
                d.TryGetValue(p.Group, out int n);
                d[p.Group] = n + Math.Max(0, p.count);
            }
            return d;
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) Prune();
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref landedAt, "landedAt", -1);
            Scribe_Values.Look(ref start, "start", IntVec3.Invalid);
            Scribe_Values.Look(ref method, "method", PlayerPawnsArriveMethod.Standing);
            Scribe_Defs.Look(ref generator, "generator");
            Scribe_Collections.Look(ref kit, "kit", LookMode.Deep);
            Scribe_Collections.Look(ref conditions, "conditions", LookMode.Def);
            Scribe_Values.Look(ref landings, "landings");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                kit ??= new List<LandingPiece>();
                kit.RemoveAll(p => p == null || p.def == null);
                conditions ??= new List<GameConditionDef>();
                conditions.RemoveAll(c => c == null);
            }
        }
    }

    /// <summary>The record of the start, on the council's own component: written only while the offer is open.</summary>
    public partial class CouncilGameComponent
    {
        public LandingRecord landing;

        private void ExposeLanding()
        {
            if (Scribe.mode == LoadSaveMode.Saving) Landing.Expire(this);
            if (Scribe.mode != LoadSaveMode.Saving || landing != null) Scribe_Deep.Look(ref landing, "landing");
        }
    }

    /// <summary>
    /// Records the start as the game makes it (Colonies/LandingPatches.cs). The game drops the scenario's things only on
    /// the first map: <c>Scenario.GenerateIntoMap</c> runs the parts, and the arrival and the scatter parts do nothing
    /// unless <c>Find.GameInitData</c> is set. So the things on the map before and after give the kit; a scatter part's own
    /// things are told apart by where it scattered them; what each starting colonist wears, wields and carries is theirs.
    /// </summary>
    public static class LandingRecorder
    {
        private static HashSet<Thing> before;
        private static List<GameCondition> conditionsBefore;
        private static HashSet<Thing> scatterBefore;
        private static readonly Dictionary<Thing, bool> scattered = new Dictionary<Thing, bool>();
        private static Map recording;

        private static bool Starting => Find.GameInitData != null && Current.Game != null;

        private static HashSet<Thing> AllOn(Map map)
        {
            var list = new List<Thing>();
            ThingOwnerUtility.GetAllThingsRecursively(map, list, false);
            return new HashSet<Thing>(list);
        }

        /// <summary>Before the scenario's parts make their things on the start map.</summary>
        public static void Begin(Map map)
        {
            recording = null;
            if (!Starting || map == null || map.Parent == null) return;
            before = AllOn(map);
            conditionsBefore = map.gameConditionManager.ActiveConditions.ToList();
            scattered.Clear();
            recording = map;
        }

        /// <summary>Before one scatter part: the things on the ground now.</summary>
        public static void BeginScatter(Map map)
        {
            scatterBefore = recording != null && map == recording ? new HashSet<Thing>(map.listerThings.AllThings) : null;
        }

        /// <summary>After one scatter part: what it put down, near the start or anywhere.</summary>
        public static void EndScatter(Map map, bool nearStart)
        {
            if (scatterBefore == null || recording == null || map != recording) return;
            foreach (var t in map.listerThings.AllThings)
                if (!scatterBefore.Contains(t))
                    scattered[t] = nearStart;
            scatterBefore = null;
        }

        /// <summary>After every part: the record, on the council's component.</summary>
        public static void End(Map map)
        {
            if (recording == null || map != recording) return;
            recording = null;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !Starting) return;
            var rec = new LandingRecord
            {
                home = Homes.KeyOf(map), landedAt = Find.TickManager?.TicksGame ?? 0,
                start = MapGenerator.PlayerStartSpotValid ? MapGenerator.PlayerStartSpot : IntVec3.Invalid, method = ArrivalMethod(), generator = map.generatorDef
            };
            // Each starting colonist's own: what they wear, wield and carry.
            var gear = new HashSet<Thing>();
            foreach (var p in Find.GameInitData.startingAndOptionalPawns)
            {
                if (p == null) continue;
                var theirs = new List<Thing>();
                if (p.apparel != null) theirs.AddRange(p.apparel.WornApparel);
                if (p.equipment != null) theirs.AddRange(p.equipment.AllEquipmentListForReading);
                if (p.inventory != null) theirs.AddRange(p.inventory.innerContainer);
                foreach (var t in theirs)
                    if (t != null && gear.Add(t))
                        rec.kit.Add(LandingPiece.Of(t, LandingKind.Gear));
            }
            var after = AllOn(map);
            foreach (var t in after)
            {
                if (t == null || before.Contains(t) || gear.Contains(t) || t is Pawn || t is Skyfaller || t is IActiveTransporter || HeldByPawn(t)) continue;
                bool scatter = scattered.TryGetValue(t, out bool near);
                var kind = LandingMath.KindOf(false, scatter, near);
                if (!LandingMath.Recorded(t.def.category == ThingCategory.Item, t.def.category == ThingCategory.Building, kind)) continue;
                rec.kit.Add(LandingPiece.Of(t, kind));
            }
            // The map conditions the scenario set (a start in a cold snap, a toxic fallout).
            foreach (var c in map.gameConditionManager.ActiveConditions)
                if (c?.def != null && !conditionsBefore.Contains(c) && !rec.conditions.Contains(c.def))
                    rec.conditions.Add(c.def);
            comp.landing = rec;
            before = null;
            conditionsBefore = null;
            scattered.Clear();
        }

        private static bool HeldByPawn(Thing t)
        {
            for (var h = t.ParentHolder; h != null; h = h.ParentHolder)
                if (h is Pawn || h is Pawn_InventoryTracker || h is Pawn_ApparelTracker || h is Pawn_EquipmentTracker || h is Pawn_CarryTracker)
                    return true;
            return false;
        }

        /// <summary>How the scenario brings the people: drop pods, standing, or a gravship (Odyssey).</summary>
        public static PlayerPawnsArriveMethod ArrivalMethod()
        {
            try
            {
                var part = Find.Scenario?.AllParts.OfType<ScenPart_PlayerPawnsArriveMethod>().FirstOrDefault();
                var field = AccessTools.Field(typeof(ScenPart_PlayerPawnsArriveMethod), "method");
                if (part != null && field != null && field.GetValue(part) is PlayerPawnsArriveMethod m) return m;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("landing: how the people arrived", e);
            }
            return PlayerPawnsArriveMethod.Standing;
        }
    }
}
