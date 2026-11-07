using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The peoples a handed-over colony becomes (the player's decision 6B): Rimfolk's own four, one for each age
    /// (Defs/Misc/Rimfolk_HandOver.xml: tribal and medieval on the game's tribal parent, industrial and spacer on its
    /// outlander parent, so their groups, caravans and town defenders are the game's own), peaceful, human, not hidden,
    /// never a permanent enemy, no fixed name and no royal titles. A save that holds one needs Rimfolk from then on (the
    /// game drops a people whose def is missing, with an error), which the hand-over window says. With a mod of a
    /// medieval (or spacer) people loaded, Medieval Overhaul's first, its pawn groups and traders are lent to Rimfolk's
    /// people of that age at startup, so their gear is of the age (a soft integration: nothing else is touched).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HandOverPeoples
    {
        private static HashSet<FactionDef> ours;

        /// <summary>A people being made right now (its leader is the head, not a stranger the game would make: Patch_HandOverLeader).</summary>
        public static bool Making { get; private set; }

        static HandOverPeoples()
        {
            try
            {
                Lend(DefFor(WorldAge.Medieval), TechLevel.Medieval);
                Lend(DefFor(WorldAge.Spacer), TechLevel.Spacer);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hand-over: the peoples' kinds", e);
            }
        }

        /// <summary>Rimfolk's people of an age, or null when the def isn't there.</summary>
        public static FactionDef DefFor(WorldAge age) => DefDatabase<FactionDef>.GetNamedSilentFail(HandOverMath.DefName(age));

        private static HashSet<FactionDef> Ours
        {
            get
            {
                if (ours == null)
                {
                    ours = new HashSet<FactionDef>();
                    foreach (var a in AgesMath.All)
                    {
                        var d = DefFor(a);
                        if (d != null) ours.Add(d);
                    }
                }
                return ours;
            }
        }

        /// <summary>One of Rimfolk's own peoples (a town the colony was, or one whose record is gone).</summary>
        public static bool IsOurs(Faction f) => f?.def != null && Ours.Contains(f.def);

        public static bool IsOursDef(FactionDef d) => d != null && Ours.Contains(d);

        /// <summary>
        /// A loaded mod's people of that age (Medieval Overhaul's first), peaceful, human and not the Empire's kind, with
        /// fighting and town groups: its pawn groups and traders are Rimfolk's people's of that age too, so their gear is
        /// the age's. None loaded: the game's tribal (or outlander) kinds, as the defs say.
        /// </summary>
        private static void Lend(FactionDef to, TechLevel tech)
        {
            if (to == null) return;
            var donor = DefDatabase<FactionDef>.AllDefsListForReading
                .Where(d => d != null && !IsOursDef(d) && !d.isPlayer && !d.hidden && d.humanlikeFaction && !d.permanentEnemy && !d.HasRoyalTitles && d.techLevel == tech
                            && d.modContentPack != null && !d.modContentPack.IsCoreMod && !d.modContentPack.IsOfficialMod && d.pawnGroupMakers != null
                            && d.pawnGroupMakers.Any(g => g?.kindDef == PawnGroupKindDefOf.Combat) && d.pawnGroupMakers.Any(g => g?.kindDef == PawnGroupKindDefOf.Settlement))
                .OrderByDescending(d => MedievalOverhaul(d) ? 1 : 0).ThenBy(d => d.naturalEnemy ? 1 : 0).ThenBy(d => d.defName, StringComparer.Ordinal).FirstOrDefault();
            if (donor == null) return;
            to.pawnGroupMakers = donor.pawnGroupMakers;
            if (!donor.baseTraderKinds.NullOrEmpty()) to.baseTraderKinds = donor.baseTraderKinds;
            if (!donor.caravanTraderKinds.NullOrEmpty()) to.caravanTraderKinds = donor.caravanTraderKinds;
            if (!donor.visitorTraderKinds.NullOrEmpty()) to.visitorTraderKinds = donor.visitorTraderKinds;
            if (Prefs.DevMode) Log.Message("[Rimfolk] " + to.defName + " takes its people's kinds from " + donor.defName + " (" + donor.modContentPack.Name + ").");
        }

        private static bool MedievalOverhaul(FactionDef d)
        {
            string id = d?.modContentPack?.PackageId?.ToLowerInvariant() ?? "";
            return id.Contains("medieval.overhaul") || id.Contains("medievaloverhaul") || id.Contains("dankpyon");
        }

        // ---------------- Making the people ----------------

        /// <summary>
        /// The new people, made the game's way (<see cref="FactionGenerator.NewGeneratedFaction(FactionGeneratorParms)"/>
        /// with the age's def, hidden at creation so the game places no town of its own, and no leader made: the head
        /// leads), named, its faith the colony's (a faith the game made for it and nobody holds is let go), then shown;
        /// allied with the colony (goodwill 80), and with every other people half the colony's goodwill with them, within
        /// each def's own rules (a permanent or natural enemy stays one); then added to the game's peoples.
        /// </summary>
        public static Faction Make(FactionDef def, string name, Ideo faith)
        {
            if (def == null) return null;
            var before = Find.IdeoManager != null ? new HashSet<Ideo>(Find.IdeoManager.IdeosListForReading) : new HashSet<Ideo>();
            Faction f;
            Making = true;
            try
            {
                f = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def, default(IdeoGenerationParms), true));
            }
            finally
            {
                Making = false;
            }
            if (f == null) return null;
            if (!name.NullOrEmpty()) f.Name = name;
            if (f.ideos != null && faith != null)
            {
                var made = f.ideos.PrimaryIdeo;
                f.ideos.SetPrimary(faith);
                if (made != null && made != faith && !before.Contains(made) && Find.IdeoManager != null && !Find.FactionManager.AllFactions.Any(o => o.ideos != null && o.ideos.AllIdeos.Contains(made)))
                    Find.IdeoManager.Remove(made);
            }
            f.hidden = false;
            foreach (var other in Find.FactionManager.AllFactionsListForReading)
            {
                if (other == null || other == f) continue;
                var mine = f.RelationWith(other, true);
                var theirs = other.RelationWith(f, true);
                if (mine == null || theirs == null) continue;
                int g;
                bool hostile;
                if (other.IsPlayer)
                {
                    g = HandOverMath.AllyGoodwill;
                    hostile = false;
                    mine.kind = theirs.kind = FactionRelationKind.Ally;
                }
                else
                {
                    bool withUs = other.HasGoodwill && !other.def.hidden;
                    g = HandOverMath.OtherGoodwill(mine.baseGoodwill, withUs ? other.PlayerGoodwill : 0, withUs && other.HostileTo(Faction.OfPlayer), out hostile);
                    mine.kind = theirs.kind = hostile ? FactionRelationKind.Hostile : g >= 75 ? FactionRelationKind.Ally : FactionRelationKind.Neutral;
                }
                mine.baseGoodwill = theirs.baseGoodwill = g;
            }
            Find.FactionManager.Add(f);
            return f;
        }
    }

    /// <summary>
    /// The bond in goodwill (the player's decision 8A): "once of [colony]", +30 natural goodwill while one of the
    /// colony's former people heads the town, +15 after, gone after a war between them or a taking by force
    /// (Colonies/TownTakeBack.cs). The game's own drift and same-faith situation do the rest.
    /// </summary>
    public class GoodwillSituationWorker_OnceOf : GoodwillSituationWorker
    {
        public override string GetPostProcessedLabel(Faction other)
        {
            var r = WorldLedger.FormerOf(other);
            return r == null ? base.GetPostProcessedLabel(other) : "CC_HandOver_OnceOf".Translate((r.colonyName ?? "?").Named("COLONY")).Resolve();
        }

        public override int GetNaturalGoodwillOffset(Faction other)
        {
            if (other == null || !HandOverPeoples.IsOurs(other)) return 0;
            var r = WorldLedger.FormerOf(other);
            return r == null ? 0 : HandOverMath.Bond(r.bondEnded, r.FormerHeads);
        }
    }

    [DefOf]
    public static class HandOverDefOf
    {
        public static GoodwillSituationDef Rimfolk_OnceOf;

        static HandOverDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HandOverDefOf));
        }
    }
}
