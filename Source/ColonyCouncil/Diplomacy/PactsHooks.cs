using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Where pacts and truces meet the game (Truces.cs, Pacts.cs): the deeds the peoples' memory notes are read as
    /// breaches (a deed of the colony's against them is its breach, a raid or a colonist killed is theirs); a raid of a
    /// truce people that falls on its home anyway, or their ambush of a caravan, breaks it; and [SR] Factional War's
    /// battles on a home name a pact or truce partner among the two sides (decision 18), where fighting beside a pact
    /// partner keeps the pact (the deed "sidedWith") and harm done to a truce partner's fighters breaks the truce.
    /// </summary>
    public static class PactsHooks
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>A deed the memory just noted (FactionMemory.Note): a breach of a truce or a pact, either way.</summary>
        public static void Noted(FactionDeed d)
        {
            if (d?.faction == null) return;
            try
            {
                var f = d.faction;
                if (d.side == DeedSide.Wrong)
                {
                    Battles(d);
                    string how = FactionMemory.Text(d);
                    if (PactMath.OurBreach(d.kind) && Truces.AnyWith(f) != null) Truces.BrokenByUs(f, how);
                    if (PactMath.PactBreach(d.kind) && Pacts.With(f).Count > 0) Pacts.BrokenByUs(f, how);
                }
                else if (d.side == DeedSide.TheirWrong && d.kind == "killedOurs")
                {
                    // A colonist killed by them: their raid broke it already, else this does (an ambush, a fight far away).
                    Truces.Killed(f);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pacts and truces: a deed read", e);
            }
        }

        // ================= Faction Territories' invasions =================

        /// <summary>
        /// An invasion of a town ended (Faction Territories' ResolveWithWinner): when the town's people are a pact partner
        /// of the colony's, or the colony owes them, how it ended is told in the news.
        /// </summary>
        public static void InvasionEnded(Faction owner, string town, Faction winner)
        {
            if (owner == null || winner == null || owner.IsPlayer) return;
            try
            {
                if (Pacts.With(owner).Count == 0 && FavoursOwed.OwedTo(owner).Count == 0) return;
                string key = winner == owner ? "CC_War_InvasionHeld" : "CC_War_InvasionLost";
                // apart: news of a fight far away, told in the evening's news.
                Tell.Minor(NewsKind.World, Importance.Notable, Homes.Seat, PactWords.P(key, PactWords.People(owner).Named("PEOPLE"), (town ?? "?").Named("TOWN"),
                    PactWords.People(winner).Named("WINNER")), LookTargets.Invalid);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pacts: an invasion's end", e);
            }
        }

        // ================= Factional War's battles =================

        private sealed class Battle
        {
            public Map home;
            public Faction a, b;
            public int at;
            public bool sided;
        }

        /// <summary>The battles on the homes this session (a day's fighting at most each).</summary>
        private static readonly List<Battle> battles = new List<Battle>();

        /// <summary>
        /// Two peoples came to fight on a home (WorldMechanics.Battle): when one of them is a pact or truce partner of this
        /// home, a message names it and says what fighting there means.
        /// </summary>
        public static void BattleBegan(Faction a, Faction b, Map home)
        {
            if (a == null || b == null || home == null) return;
            battles.RemoveAll(x => x == null || Now - x.at > GenDate.TicksPerDay * 2 || !Find.Maps.Contains(x.home));
            battles.Add(new Battle { home = home, a = a, b = b, at = Now });
            foreach (var (us, them) in new[] { (a, b), (b, a) })
            {
                string key = null;
                if (Pacts.Of(us, home, PactKind.Defence) != null || Pacts.Of(us, home, PactKind.Trade) != null) key = "CC_War_Pact";
                else if (Truces.SwornOf(us, home) != null) key = "CC_War_Truce";
                if (key == null) continue;
                // apart: what the battle on the colony's land means for its pact or truce, told as it begins.
                Tell.Message(NewsKind.World, Importance.Important, home, PactWords.P(key, PactWords.People(us).Named("PEOPLE"), PactWords.People(them).Named("OTHER")),
                    LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
        }

        /// <summary>
        /// One of the colony's wrongs against a side of a battle on a home lately (their fighter fallen at its hands): the
        /// other side, a pact partner, remembers the colony fought beside them (once a battle).
        /// </summary>
        private static void Battles(FactionDeed d)
        {
            if (battles.Count == 0 || (d.kind != "fell" && d.kind != "slain" && d.kind != "killed" && d.kind != "attacked")) return;
            foreach (var x in battles)
            {
                if (x.sided || Now - x.at > GenDate.TicksPerDay * 2) continue;
                var partner = d.faction == x.a ? x.b : d.faction == x.b ? x.a : null;
                if (partner == null || Pacts.With(partner).Count == 0) continue;
                x.sided = true;
                KinMemory.Note(partner, "sidedWith", null, PactWords.P("CC_War_Sided", PactWords.People(d.faction).Named("OTHER")));
                // apart: what their people make of the colony's part in the battle, told in a message.
                Tell.Message(NewsKind.World, Importance.Notable, x.home, PactWords.P("CC_War_SidedMsg", PactWords.People(partner).Named("PEOPLE")), LookTargets.Invalid,
                    MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    /// <summary>
    /// The game's choice of raiders (IncidentWorker_RaidEnemy.FactionCanBeGroupSource): a people holding a sworn truce
    /// with the home being raided is not chosen (decision 1). A set kept for a 250-tick stretch (Truces.KeepsAway), so a
    /// raid roll costs a few lookups. Raids whose people is set beforehand (quests, other mods, Rimfolk's own) never ask
    /// it. With "Truces keep raids away" or the deeper game hooks off, nothing changes.
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), nameof(IncidentWorker_RaidEnemy.FactionCanBeGroupSource))]
    public static class Patch_TruceRaids
    {
        public static void Postfix(Faction f, IncidentParms parms, ref bool __result)
        {
            if (!__result || f == null || !(parms?.target is Map map)) return;
            try
            {
                if (Truces.KeepRaidsAway && Truces.KeepsAway(f, map)) __result = false;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("truces: the choice of raiders", e);
            }
        }
    }

    /// <summary>A raid has arrived at a home: one of a people holding a truce with it breaks the truce (theirs to break).</summary>
    [HarmonyPatch(typeof(IncidentWorker_Raid), "PostProcessSpawnedPawns")]
    public static class Patch_TruceRaided
    {
        public static void Postfix(IncidentParms parms)
        {
            if (!RimfolkPatches.On || !(parms?.target is Map map) || parms.faction == null || !Homes.IsHome(map)) return;
            try
            {
                if (Truces.Of(parms.faction, map) != null) Truces.Raided(parms.faction, map);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("truces: a raid arrived", e);
            }
        }
    }

    /// <summary>An ambush of theirs on a caravan of the colony's (the game's, or one Faction Territories forces in their land): it breaks their truce.</summary>
    [HarmonyPatch(typeof(IncidentWorker_Ambush_EnemyFaction), "CreateLordJob")]
    public static class Patch_TruceAmbush
    {
        public static void Postfix(IncidentParms parms)
        {
            if (!RimfolkPatches.On || parms?.faction == null) return;
            try
            {
                if (Truces.AnyWith(parms.faction) != null) Truces.Ambushed(parms.faction);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("truces: an ambush", e);
            }
        }
    }
}
