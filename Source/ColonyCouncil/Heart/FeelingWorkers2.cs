using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // Six more feelings' workers (see FeelingWorkers.cs for how they work). Each reads only the colonist, their own
    // relations and bonds, and what HeartMapContext already worked out for the hour.

    /// <summary>Long years here, old age, an heirloom carried: the past close at hand.</summary>
    public class FeelingWorker_Nostalgia : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            int asColonist = p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0;
            if (asColonist >= GenDate.TicksPerYear * 2) sink.Add(2f, "CC_Heart_Cause_LongYears".Translate());
            if (p.RaceProps.Humanlike && Flavor.HumanAge(p) >= 60f) sink.Add(2f, "CC_Heart_Cause_OldMemories".Translate());
            var comp = CouncilGameComponent.Instance;
            if (comp != null && ColonyCouncilMod.Settings.heirlooms && comp.legacy.HeirloomCarriedBy(p) is Heirloom h)
                sink.Add(2f, "CC_Heart_Cause_Heirloom".Translate(h.ofName ?? "?"));
        }
    }

    /// <summary>The danger is over and they came through it.</summary>
    public class FeelingWorker_Relief : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            if (map.map == null || map.threat || !p.Spawned || p.Map != map.map || p.Downed) return;
            int since = map.ticksSinceBigThreat;
            if (since >= GenDate.TicksPerHour * 2 && since < GenDate.TicksPerDay)
                sink.Add(Mathf.Lerp(5f, 2f, since / (float)GenDate.TicksPerDay), "CC_Heart_Cause_ThreatOver".Translate());
        }
    }

    /// <summary>Caring for someone: tending a patient, an animal bonded to them close by.</summary>
    public class FeelingWorker_Tenderness : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            if (p.CurJobDef == JobDefOf.TendPatient && p.CurJob?.targetA.Thing is Pawn patient && patient != p)
                sink.Add(4f, "CC_Heart_Cause_Tending".Translate(patient.LabelShort), patient.RaceProps.Humanlike ? patient : null);
            if (p.relations == null || !p.Spawned) return;
            foreach (var r in p.relations.DirectRelations)
            {
                var o = r.otherPawn;
                if (r.def != PawnRelationDefOf.Bond || o == null || o.Dead || !o.Spawned || o.Map != p.Map) continue;
                sink.Add(2f, "CC_Heart_Cause_BondedAnimal".Translate(o.LabelShort));
                break;
            }
        }
    }

    /// <summary>Someone they love is downed, ill, carried off or away on the road; a sickness or hunger in the region.</summary>
    public class FeelingWorker_Worry : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            // The region's troubles, for those they touch most (Heart/FeelingFeeds_World.cs).
            WorldFeeds.RegionTroubles(p, sink);
            if (p.relations == null) return;
            var seen = new HashSet<Pawn>();
            int added = 0;
            foreach (var r in p.relations.DirectRelations)
            {
                if (added >= 3) return;
                if (r.def != PawnRelationDefOf.Spouse && r.def != PawnRelationDefOf.Lover && r.def != PawnRelationDefOf.Fiance
                    && r.def != PawnRelationDefOf.Parent && r.def != PawnRelationDefOf.Child && r.def != PawnRelationDefOf.Sibling) continue;
                if (seen.Add(r.otherPawn) && Check(p, r.otherPawn, 1f, sink)) added++;
            }
            // Family by marriage: a spouse's parent, a child's spouse, a step-parent or step-child.
            foreach (var o in Family.ByMarriageOf(p))
            {
                if (added >= 3) return;
                if (seen.Add(o) && Check(p, o, 0.8f, sink)) added++;
            }
            var comp = CouncilGameComponent.Instance;
            if (comp?.social == null || !ColonyCouncilMod.Settings.socialWeb) return;
            foreach (var t in comp.social.ties)
            {
                if (added >= 3) return;
                if (t.kind != TieKind.Friends || !t.announced || t.strength < 40f || !t.Involves(p)) continue;
                var o = t.Other(p);
                if (o != null && seen.Add(o) && Check(p, o, 0.7f, sink)) added++;
            }
        }

        private static bool Check(Pawn p, Pawn o, float factor, IFeelingSink sink)
        {
            if (o == null || o.Dead || o == p) return false;
            if (People.Kidnapped(o))
            {
                sink.Add(5f * factor, "CC_Heart_Cause_WorryTaken".Translate(o.LabelShort), o);
                return true;
            }
            if (o.Faction != Faction.OfPlayer) return false;
            if (o.Downed)
            {
                sink.Add(5f * factor, "CC_Heart_Cause_WorryDowned".Translate(o.LabelShort), o);
                return true;
            }
            if (LifeChecks.HasIllness(o, null))
            {
                sink.Add(3f * factor, "CC_Heart_Cause_WorryIll".Translate(o.LabelShort), o);
                return true;
            }
            var away = o.GetCaravan();
            if (away != null && p.GetCaravan() != away)
            {
                sink.Add(2f * factor, "CC_Heart_Cause_WorryAway".Translate(o.LabelShort), o);
                return true;
            }
            return false;
        }
    }

    /// <summary>Belonging to a circle, good friends close by, and friends made at a camp.</summary>
    public class FeelingWorker_Fellowship : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp?.social != null && ColonyCouncilMod.Settings.socialWeb && comp.social.CliqueOf(p) != null) sink.Add(3f, "CC_Heart_Cause_Circle".Translate());
            if (HeartNeeds.FriendsHere(p, out _) >= 3) sink.Add(2f, "CC_Heart_Cause_ManyFriends".Translate());
            // Friends made at a camp on the map (Heart/FeelingFeeds_World.cs).
            WorldFeeds.CampFriends(p, sink);
        }
    }

    /// <summary>A score to settle, a goal half won, a steady nerve when the raiders come.</summary>
    public class FeelingWorker_Resolve : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            if (ColonyCouncilMod.Settings.vendettas && comp.vendettas.AnyHostile(p)) sink.Add(3f, "CC_Heart_Cause_Vendetta".Translate());
            var goal = ColonyCouncilMod.Settings.aspirations ? comp.aspirations.Active(p) : null;
            if (goal != null)
            {
                float progress = goal.Progress;
                if (progress >= 0.5f && progress < 0.75f) sink.Add(2f, "CC_Heart_Cause_GoalHalfway".Translate());
            }
            if (map.threat && p.Spawned && p.Map == map.map && Steady(p)) sink.Add(3f, "CC_Heart_Cause_SteadyNerve".Translate());
        }

        private static readonly DefRef<TraitDef> ToughTrait = new DefRef<TraitDef>("Tough");
        private static readonly DefRef<TraitDef> NervesTrait = new DefRef<TraitDef>("Nerves");

        private static bool Steady(Pawn p)
        {
            var t = p.story?.traits;
            if (t == null) return false;
            var tough = ToughTrait.Def;
            if (tough != null && t.HasTrait(tough)) return true;
            var nerves = NervesTrait.Def;
            return nerves != null && t.HasTrait(nerves) && t.DegreeOfTrait(nerves) > 0;
        }
    }
}
