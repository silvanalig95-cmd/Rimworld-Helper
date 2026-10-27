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
    /// Family as Rimfolk sees it, beyond blood: a spouse's parents, a child's spouse, a step-parent and step-child (the
    /// game's own relations) count as family for worry, grief, belonging and visits. Each colonist's story shows their
    /// family tree, where everyone is. A relative who joins the colony is met by the family already here, a reunion in
    /// earnest: the one here goes to find them. A colonist who sees family of theirs among the raiders says so, and a
    /// relative taken prisoner brings the council a question (Council_DlmS_KinInChains). Grief for family by blood is
    /// the game's own; for family by marriage, Rimfolk's. Nothing here is saved beyond the memories it gives: they are
    /// what keeps a reunion or a recognition from happening twice.
    /// </summary>
    public static class Family
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        // ---------------- Who is family ----------------

        /// <summary>Family by marriage or a parent's partner: a spouse's parent, a child's spouse, a step-parent or step-child.</summary>
        public static bool ByMarriage(Pawn a, Pawn b) =>
            a?.relations != null && b?.relations != null && a != b && a.GetRelations(b).Any(r => FamilyMath.ByMarriage(r.defName));

        /// <summary>Family by adoption: an adoptive parent the council named, or the child they took in (Social/FamilyLife.cs).</summary>
        public static bool Adoptive(Pawn a, Pawn b) =>
            a?.relations != null && b?.relations != null && a != b && a.GetRelations(b).Any(r => FamilyMath.Adoptive(r.defName));

        /// <summary>Family in the wider sense: by blood, by marriage or by adoption (not a partner).</summary>
        public static bool Of(Pawn a, Pawn b) => ArcUtil.Kin(a, b) || ByMarriage(a, b) || Adoptive(a, b);

        /// <summary>Their parents: by blood, and the adoptive parents the council named.</summary>
        public static List<Pawn> ParentsOf(Pawn p)
        {
            var list = new List<Pawn>();
            if (p?.relations == null) return list;
            foreach (var r in p.relations.DirectRelations)
                if ((r.def == PawnRelationDefOf.Parent || FamilyMath.AdoptiveParent(r.def.defName)) && r.otherPawn != null && !list.Contains(r.otherPawn))
                    list.Add(r.otherPawn);
            return list;
        }

        /// <summary>Their children: by blood, and those they took in as adoptive parent.</summary>
        public static List<Pawn> ChildrenOf(Pawn p)
        {
            if (p?.relations == null) return new List<Pawn>();
            return p.relations.Children.Where(c => c != null).Concat(p.relations.RelatedPawns.Where(o => o != null && o != p && ParentsOf(o).Contains(p))).Distinct().ToList();
        }

        /// <summary>Their family by marriage, living or dead.</summary>
        public static List<Pawn> ByMarriageOf(Pawn p)
        {
            if (p?.relations == null) return new List<Pawn>();
            return p.relations.RelatedPawns.Where(o => o != null && o != p && ByMarriage(p, o)).ToList();
        }

        /// <summary>Their family by blood, by marriage and by adoption, living or dead.</summary>
        public static List<Pawn> All(Pawn p)
        {
            if (p?.relations == null) return new List<Pawn>();
            var adoptive = p.relations.RelatedPawns.Where(o => o != null && o != p && Adoptive(p, o));
            return p.relations.FamilyByBlood.Where(o => o != null && o != p).Concat(ByMarriageOf(p)).Concat(adoptive).Distinct().ToList();
        }

        /// <summary>What <paramref name="other"/> is to <paramref name="me"/> ("grandmother", "mother-in-law"), or "".</summary>
        public static string Relation(Pawn me, Pawn other)
        {
            if (me?.relations == null || other == null || me == other) return "";
            try
            {
                return me.GetMostImportantRelation(other)?.GetGenderSpecificLabel(other) ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>The relation of theirs that comes first on a family tree, or null for one further out.</summary>
        private static PawnRelationDef TreeRelation(Pawn me, Pawn other)
        {
            PawnRelationDef best = null;
            int rank = int.MaxValue;
            foreach (var r in me.GetRelations(other))
            {
                int k = FamilyMath.TreeRank(r.defName);
                if (k < 0 || k >= rank) continue;
                rank = k;
                best = r;
            }
            return best;
        }

        // ---------------- The family tree ----------------

        /// <summary>One relative on the family tree, closest first (<see cref="Tree"/>).</summary>
        public class TreeEntry
        {
            public Pawn pawn;
            /// <summary>What they are to the one whose tree it is ("wife", "grandmother").</summary>
            public string relation;
            /// <summary>Where they are ("here", "with the Iron Hearth"), and as a kind.</summary>
            public string where;
            public KinWhere whereKind;
            /// <summary>The whole line: "Stace, his wife, 51: here."</summary>
            public string line;
        }

        /// <summary>
        /// Their family as the story shows it: partner, parents, brothers and sisters, children, grandparents and
        /// grandchildren, step-family and in-laws, closest first, each with their age and where they are (at most
        /// <see cref="FamilyMath.TreeShown"/>); <paramref name="more"/> counts the rest and those further out.
        /// </summary>
        public static List<TreeEntry> Tree(Pawn p, out int more)
        {
            var list = new List<TreeEntry>();
            more = 0;
            if (p?.relations == null) return list;
            var near = new List<(Pawn o, PawnRelationDef rel)>();
            var others = new HashSet<Pawn>();
            foreach (var o in p.relations.RelatedPawns)
            {
                if (o == null || o == p || !o.RaceProps.Humanlike) continue;
                var rel = TreeRelation(p, o);
                if (rel != null) near.Add((o, rel));
                else if (p.GetRelations(o).Any(r => r.familyByBloodRelation)) others.Add(o);
            }
            near = near.OrderBy(x => FamilyMath.TreeRank(x.rel.defName)).ThenBy(x => x.o.Dead).ThenBy(x => x.o.LabelShort).ToList();
            foreach (var (o, rel) in near.Take(FamilyMath.TreeShown))
            {
                var kind = WhereKind(p, o);
                string where = WhereWords(o, kind);
                string relation = rel.GetGenderSpecificLabel(o);
                list.Add(new TreeEntry
                {
                    pawn = o, relation = relation, where = where, whereKind = kind,
                    line = o.Dead
                        ? "CC_Family_LineDead".Translate(p.Named("PAWN"), o.Named("OTHER"), relation.Named("RELATION"), where.Named("WHERE")).Resolve()
                        : "CC_Family_Line".Translate(p.Named("PAWN"), o.Named("OTHER"), relation.Named("RELATION"), FamilyYears.Lived(o).Named("AGE"), where.Named("WHERE")).Resolve()
                });
            }
            more = others.Count + Math.Max(0, near.Count - FamilyMath.TreeShown);
            return list;
        }

        /// <summary>Where a relative is, as a kind (FamilyMath.Where).</summary>
        public static KinWhere WhereKind(Pawn of, Pawn o)
        {
            var corpse = o.Dead ? o.Corpse : null;
            bool buried = corpse != null && corpse.ParentHolder is Building_Grave && (corpse.MapHeld?.IsPlayerHome ?? false);
            bool ours = !o.Dead && o.Faction == Faction.OfPlayer && !o.IsPrisoner && !o.IsSlave;
            return FamilyMath.Where(o.Dead, buried, ours, o.MapHeld != null && o.MapHeld == of?.MapHeld, o.GetCaravan() != null,
                o.IsPrisonerOfColony, o.IsSlaveOfColony, o.Spawned && o.Map.IsPlayerHome, People.Kidnapped(o), o.Faction != null);
        }

        /// <summary>Where a relative is, in a few words ("here", "with the Iron Hearth", "buried here").</summary>
        public static string WhereWords(Pawn of, Pawn o) => WhereWords(o, WhereKind(of, o));

        private static string WhereWords(Pawn o, KinWhere where)
        {
            string key = "CC_Family_Where_" + where;
            return where == KinWhere.WithFaction ? key.Translate(o.Faction.Name.Named("FACTION")).Resolve() : key.Translate().Resolve();
        }

        // ---------------- Reunions and kin among the raiders ----------------

        /// <summary>The council's hour: relatives who've just come to the colony are met, and family among raiders is recognised.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            if (comp == null || S == null) return;
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome).ToList())
            {
                var colonists = map.mapPawns.FreeColonistsSpawned.Where(c => People.HasInnerLife(c) && !c.IsQuestLodger()).ToList();
                if (colonists.Count == 0) continue;
                Reunions(colonists);
                KinAmongRaiders(map, colonists, comp);
            }
            // A child on the way, told to the family (Social/FamilyLife.cs).
            FamilyLife.Hourly(comp);
            // A child orphaned a day ago: who raises them (Social/FamilyGuardians.cs).
            Guardians.Hourly(comp);
            // A room left a day ago goes to family (Social/FamilyInheritance.cs).
            Inheritance.Hourly(comp);
        }

        private static float DaysHere(Pawn p) => (p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 999999) / (float)GenDate.TicksPerDay;

        /// <summary>
        /// Someone who joined in the last day, with family who've been here longer: the closest of them (and one more)
        /// goes to find them in their free time, and when the two are together it's a reunion.
        /// </summary>
        private static void Reunions(List<Pawn> colonists)
        {
            var reunited = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Fam_Reunited");
            if (reunited == null) return;
            foreach (var n in colonists)
            {
                if (n.DevelopmentalStage.Baby() || n.ageTracker.AgeChronologicalTicks < GenDate.TicksPerDay * 2) continue;
                float nd = DaysHere(n);
                if (nd >= 1f) continue;
                var kin = colonists.Where(f => f != n && FamilyMath.ReunionDue(nd, DaysHere(f)) && (Of(f, n) || LovePartnerRelationUtility.LovePartnerRelationExists(f, n)))
                    .Select(f => (f, rel: TreeRelation(f, n)))
                    .OrderBy(x => x.rel != null ? FamilyMath.TreeRank(x.rel.defName) : 99).Take(2).ToList();
                foreach (var (f, _) in kin)
                {
                    if (Met(n, f, reunited) || f.relations.OpinionOf(n) < -20 || n.relations.OpinionOf(f) < -20) continue;
                    var here = f;
                    var newcomer = n;
                    Together.When("reunion|" + f.thingIDNumber + "|" + n.thingIDNumber, f, n, () => Reunion(here, newcomer, reunited), 24f, 0f);
                }
            }
        }

        private static bool Met(Pawn a, Pawn b, ThoughtDef def) =>
            a.needs?.mood?.thoughts?.memories?.Memories?.Any(m => m.def == def && m.otherPawn == b) ?? false;

        /// <summary>The two together again: a word, a moment of joy for both, and a memory they'll share.</summary>
        private static void Reunion(Pawn here, Pawn newcomer, ThoughtDef reunited)
        {
            // together: Together.When runs this only when the two are together, and Speech.Say asks again.
            if (Met(newcomer, here, reunited)) return;
            string relation = Relation(here, newcomer);
            string cause = "CC_Fam_ReunionCause".Translate(here.Named("PAWN"), newcomer.Named("OTHER")).Resolve();
            CouncilGameComponent.GiveThought(here, reunited, newcomer, -1f, cause);
            CouncilGameComponent.GiveThought(newcomer, reunited, here, -1f, cause);
            var memory = DefDatabase<SharedMemoryDef>.GetNamedSilentFail("Rimfolk_Mem_Reunited");
            if (memory != null) SharedMemories.Add(here, newcomer, memory, here.Map?.Parent?.LabelCap ?? "the colony");
            string line = Flavor.FromPool("Council_Pool_SayReunion", here, null, null, ReunionTag(here, newcomer));
            if (!line.NullOrEmpty())
                Speech.Say(here, line.Formatted(newcomer.Named("OTHER"), relation.Named("RELATION")).Resolve(), newcomer);
            foreach (var x in new[] { here, newcomer })
                if (x.Spawned) FleckMaker.ThrowMetaIcon(x.Position, x.Map, FleckDefOf.Heart);
            Messages.Message("CC_Fam_Reunion".Translate(here.Named("PAWN"), newcomer.Named("OTHER"), relation.Named("RELATION")).Resolve(),
                new LookTargets(new[] { here, newcomer }), MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>What the newcomer is to the one who meets them, as a line's tag.</summary>
        private static string ReunionTag(Pawn here, Pawn newcomer)
        {
            var rel = TreeRelation(here, newcomer)?.defName;
            if (FamilyMath.Partner(rel)) return "partner";
            if (rel == "Parent") return "parent";
            if (rel == "Child") return "child";
            if (rel == "Sibling" || rel == "HalfSibling") return "sibling";
            if (rel == "Grandparent" || rel == "Grandchild" || rel == "GreatGrandparent" || rel == "GreatGrandchild") return "grand";
            return FamilyMath.ByMarriage(rel) ? "inlaw" : "kin";
        }

        /// <summary>
        /// One of the colony sees family of theirs among those attacking it (in sight, not in the fog): they say so, the
        /// colony hears of it, and it weighs on them while it lasts. A relative taken prisoner brings the council a
        /// question about them (Council_DlmS_KinInChains), once the cells hold them.
        /// </summary>
        private static void KinAmongRaiders(Map map, List<Pawn> colonists, CouncilGameComponent comp)
        {
            // apart: family seen across the field among the attackers, not met; nothing is said to them.
            var seen = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Fam_KinAmongRaiders");
            if (seen != null)
            {
                var hostiles = map.mapPawns.AllPawnsSpawned.Where(h => h.RaceProps.Humanlike && !h.Dead && h.Faction != null && h.HostileTo(Faction.OfPlayer)
                                                                       && !h.IsPrisoner && !h.Position.Fogged(map)).ToList();
                foreach (var h in hostiles)
                    foreach (var c in colonists)
                    {
                        if (!c.Awake() || c.Downed || !Of(c, h) || Met(c, h, seen)) continue;
                        string relation = Relation(c, h);
                        CouncilGameComponent.GiveThought(c, seen, h, -1f, "CC_Fam_KinRaiderCause".Translate(c.Named("PAWN"), h.Named("OTHER"), relation.Named("RELATION")).Resolve());
                        string line = Flavor.FromPool("Council_Pool_SayKinRaider", c);
                        if (!line.NullOrEmpty()) Speech.Say(c, line.Formatted(h.Named("OTHER"), relation.Named("RELATION")).Resolve());
                        Messages.Message("CC_Fam_KinRaider".Translate(c.Named("PAWN"), h.Named("OTHER"), relation.Named("RELATION"), h.Faction.Name.Named("FACTION")).Resolve(),
                            new LookTargets(new[] { c, h }), MessageTypeDefOf.NeutralEvent, false);
                    }
            }
            // Family in the cells: the council is asked about them when it can be (a question already waiting holds it back).
            foreach (var x in map.mapPawns.PrisonersOfColonySpawned)
            {
                if (!asked.Add(x.thingIDNumber)) continue;
                if (!colonists.Any(c => Of(c, x))) continue;
                comp.dilemmas.TryFire(false, d => d.defName == "Council_DlmS_KinInChains");
            }
            if (asked.Count > 300) asked.Clear();
        }

        /// <summary>Prisoners looked at for family once this session (a question about them is the dilemma's own to time).</summary>
        private static readonly HashSet<int> asked = new HashSet<int>();

        public static void Reset() => asked.Clear();

        // ---------------- A death in the family by marriage ----------------

        /// <summary>
        /// Someone the colony has met dies: those of the colony who were family to them by marriage (a mother-in-law, a
        /// step-son) and didn't dislike them grieve. Family by blood grieve already, with the game's own thoughts.
        /// </summary>
        public static void Notify_Died(Pawn dead)
        {
            // apart: a death heard of, wherever the two of them were.
            if (dead?.relations == null || !dead.relations.everSeenByPlayer || !dead.RaceProps.Humanlike) return;
            var def = DefDatabase<ThoughtDef>.GetNamedSilentFail("Council_Fam_DiedByMarriage");
            if (def == null) return;
            foreach (var c in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())
            {
                if (c == dead || c.relations == null || !People.HasInnerLife(c) || ArcUtil.Kin(c, dead) || !ByMarriage(c, dead)) continue;
                if (c.relations.OpinionOf(dead) < 0) continue;
                CouncilGameComponent.GiveThought(c, def, dead, -1f,
                    "CC_Fam_DiedByMarriageCause".Translate(c.Named("PAWN"), dead.Named("OTHER"), Relation(c, dead).Named("RELATION")).Resolve());
            }
        }
    }

    /// <summary>
    /// A death in the family by marriage (Social/Family.cs), one that leaves a child with nobody of their own, or a room
    /// to pass on, once the death has really happened.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_FamilyDied
    {
        /// <summary>The bed of their own room, while they still own it (Social/FamilyInheritance.cs).</summary>
        public static void Prefix(Pawn __instance, out Building_Bed __state)
        {
            __state = null;
            if (!RimfolkPatches.On || __instance == null || __instance.Dead || !__instance.IsColonist) return;
            try
            {
                __state = __instance.ownership?.Bedroom != null ? __instance.ownership.OwnedBed : null;
            }
            catch (Exception)
            {
                __state = null;
            }
        }

        public static void Postfix(Pawn __instance, Building_Bed __state)
        {
            if (!RimfolkPatches.On || __instance == null || !__instance.Dead) return;
            try
            {
                Family.Notify_Died(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("family death hook", e);
            }
            // A child left with nobody of their own here: the council is asked who raises them (Social/FamilyGuardians.cs);
            // their own room goes to family (Social/FamilyInheritance.cs).
            try
            {
                Guardians.Notify_Died(__instance);
                Inheritance.Notify_Died(__instance, __state);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("guardians and inheritance death hook", e);
            }
        }
    }
}
