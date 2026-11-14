using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Invited visitors as Hospitality's guests (the player's request, 10 November 2026: "visitors that you invite with
    /// diplomatic actions should also be treated as guests with hospitality"; the rules are InvitedGuestsMath.cs). Those the
    /// colony invited (a letter's invitation, a town asked to its feast or fair, a guest of note told yes, a feast's or a
    /// wedding's guests, an envoy for a season) are handed, once they're on the map, to Hospitality's own way of making
    /// guests: its incident's CreateLord, with the pockets and wares its visitors carry (GiveItems), no letter of its own
    /// (Rimfolk's says it), and a stay as long as Rimfolk's visit. They then stay as its guests: its guest area and beds,
    /// its guest tab and recruiting rules, Rimfolk's guest help. Rimfolk's purpose stays on top: a feast keeps its guests at
    /// the gate, a contest, the table and indoors for the night (<see cref="Steer"/>, at each of its looks), an envoy and the
    /// feast's guests keep indoors in danger, both are fed and bedded by Rimfolk as before, and when Rimfolk's visit ends
    /// their stay ends as Hospitality's own does (<see cref="Leave"/>). Hospitality read by type name; with it absent, the
    /// setting off, or the handing over failing, Rimfolk's own visitors' group takes them as before.
    /// </summary>
    public static class InvitedGuests
    {
        /// <summary>The setting "Invited visitors are Hospitality's guests", with the deeper game hooks and Hospitality loaded.</summary>
        public static bool On => RimfolkPatches.On && (ColonyCouncilMod.Settings?.invitedAsGuests ?? true) && People.ModActive(Guests.Mod);

        // ---------------- Hospitality, by name ----------------

        private static bool looked;
        private static Type visitJob, visitToil, travelToil, mapComp;
        private static MethodInfo createLord, giveItems, joinLate, joinedLate;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            try
            {
                visitJob = GenTypes.GetTypeInAnyAssembly("Hospitality.LordJob_VisitColony");
                visitToil = GenTypes.GetTypeInAnyAssembly("Hospitality.LordToil_VisitPoint");
                travelToil = GenTypes.GetTypeInAnyAssembly("Hospitality.LordToil_CustomTravel");
                mapComp = GenTypes.GetTypeInAnyAssembly("Hospitality.Hospitality_MapComponent");
                var worker = GenTypes.GetTypeInAnyAssembly("Hospitality.IncidentWorker_VisitorGroup");
                if (worker != null)
                {
                    createLord = AccessTools.Method(worker, "CreateLord",
                        new[] { typeof(Faction), typeof(IntVec3), typeof(List<Pawn>), typeof(Map), typeof(bool), typeof(bool), typeof(int) });
                    giveItems = AccessTools.Method(worker, "GiveItems", new[] { typeof(IEnumerable<Pawn>) });
                }
                if (visitToil != null) joinLate = AccessTools.Method(visitToil, "JoinLate", new[] { typeof(Pawn) });
                if (mapComp != null) joinedLate = AccessTools.Method(mapComp, "OnGuestJoinedLate", new[] { typeof(Pawn) });
                if (createLord != null && !createLord.IsStatic) createLord = null;
                if (giveItems != null && !giveItems.IsStatic) giveItems = null;
            }
            catch (Exception e)
            {
                createLord = null;
                CouncilLog.ErrorOnce("invited guests: Hospitality's way of making guests", e);
            }
        }

        /// <summary>Hospitality's way of making guests, and its guests' group, were found.</summary>
        public static bool Found
        {
            get
            {
                Look();
                return createLord != null && visitJob != null && visitToil != null;
            }
        }

        // ---------------- Handing them over ----------------

        /// <summary>
        /// An invited group, on the map now, handed to Hospitality's way of making guests, to stay <paramref name="hours"/>
        /// (InvitedGuestsMath.StayTicks) around <paramref name="spot"/>. Their new group, or null: not invited, Hospitality
        /// absent or the setting off, or it failed (logged once); the caller then makes Rimfolk's own.
        /// </summary>
        public static Lord HandOver(ArrivalWhy why, List<Pawn> pawns, Map map, Faction f, IntVec3 spot, float hours)
        {
            if (pawns.NullOrEmpty() || map == null || f == null || !InvitedGuestsMath.Invited(why)) return null;
            int people = pawns.Count(p => p != null && !p.Dead && p.Spawned && p.Map == map && p.RaceProps?.Humanlike == true && p.Faction == f);
            bool hostile;
            try
            {
                hostile = f.HostileTo(Faction.OfPlayer);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("invited guests: their people's stance", e);
                hostile = true;
            }
            if (!InvitedGuestsMath.HandOver(why, Found, On, hostile, people, pawns.Count - people)) return null;
            var list = pawns.ToList();
            try
            {
                // What Hospitality's own visitors carry: a little food, money for a bed and a meal, wares to sell.
                if (giveItems != null)
                {
                    try
                    {
                        giveItems.Invoke(null, new object[] { list });
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("invited guests: what Hospitality's guests carry", e);
                    }
                }
                if (!spot.IsValid || !spot.InBounds(map)) spot = list[0].Position;
                createLord.Invoke(null, new object[] { f, spot, list, map, false, InvitedGuestsMath.UpsetWhenLost(why), InvitedGuestsMath.StayTicks(why, hours) });
                var lord = list[0].GetLord();
                if (Hosted(lord) && list.All(p => p.GetLord() == lord)) return lord;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("invited guests: handed to Hospitality", e);
            }
            // Not taken whole: out of whatever group they were put in, for Rimfolk's own to take them.
            foreach (var p in list)
            {
                try
                {
                    Lords.Leave(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("invited guests: let go of a half-made group", e);
                }
            }
            return null;
        }

        // ---------------- Their group ----------------

        /// <summary>A group of Hospitality's guests.</summary>
        public static bool Hosted(Lord l)
        {
            if (l?.LordJob == null) return false;
            Look();
            return visitJob != null && visitJob.IsInstanceOfType(l.LordJob);
        }

        /// <summary>A group of Hospitality's guests at their stay (not walking in, not on their way out).</summary>
        public static bool Staying(Lord l) => Hosted(l) && visitToil != null && l.CurLordToil != null && visitToil.IsInstanceOfType(l.CurLordToil);

        public static bool Staying(Pawn p) => p != null && Staying(p.GetLord());

        // Where a group keeps in danger, found once while the danger lasts (by the group's id; the session's only).
        private static readonly Dictionary<int, IntVec3> inside = new Dictionary<int, IntVec3>();

        /// <summary>
        /// Rimfolk's purpose on top of their stay, at each look: in danger indoors (<paramref name="indoors"/>, when the visit
        /// has such a place), else about <paramref name="focus"/> (the visit's own duty <paramref name="duty"/>, when it
        /// steers them), else Hospitality's own again. Only a settled stay is steered: walking in and out are Hospitality's.
        /// </summary>
        public static void Steer(Lord l, DutyDef duty, IntVec3 focus, float radius, DutyDef indoors)
        {
            if (!Hosted(l) || l.ownedPawns.NullOrEmpty()) return;
            try
            {
                var map = l.Map;
                bool danger = map?.dangerWatcher != null && map.dangerWatcher.DangerRating == StoryDanger.High;
                var lead = InvitedGuestsMath.Who(Staying(l), danger, indoors != null, duty != null && focus.IsValid);
                if (lead != InvitedGuestsMath.Lead.Indoors) inside.Remove(l.loadID);
                if (lead == InvitedGuestsMath.Lead.Hospitality)
                {
                    Release(l);
                    return;
                }
                DutyDef def;
                IntVec3 cell;
                float r;
                if (lead == InvitedGuestsMath.Lead.Indoors)
                {
                    if (!inside.TryGetValue(l.loadID, out cell) || !cell.IsValid)
                    {
                        cell = Visitors.IndoorsCell(map, l.ownedPawns.ToList());
                        inside[l.loadID] = cell;
                    }
                    if (!cell.IsValid) cell = focus.IsValid ? focus : l.ownedPawns[0].Position;
                    def = indoors;
                    r = 4f;
                }
                else
                {
                    def = duty;
                    cell = focus;
                    r = radius;
                }
                foreach (var p in l.ownedPawns)
                {
                    if (p?.mindState == null) continue;
                    var d = p.mindState.duty;
                    if (d != null && d.def == def && d.focus.Cell == cell && Math.Abs(d.radius - r) < 0.1f) continue;
                    p.mindState.duty = new PawnDuty(def, cell, r);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("invited guests: kept where the visit wants them", e);
            }
        }

        /// <summary>Hospitality leads again: a group steered by Rimfolk is given its own guests' duties back.</summary>
        public static void Release(Lord l)
        {
            if (!Staying(l) || !l.ownedPawns.Any(p => Ours(p?.mindState?.duty?.def))) return;
            try
            {
                l.CurLordToil.UpdateAllDuties();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("invited guests: back to Hospitality's own", e);
            }
        }

        /// <summary>One of the duties Rimfolk steers its guests with.</summary>
        private static bool Ours(DutyDef d) =>
            d != null && (d == FeastDefOf.Rimfolk_FeastGuest || d == FeastDefOf.Rimfolk_FeastIndoors || d == RansomDefOf.Rimfolk_EnvoyIndoors || d == RansomDefOf.Rimfolk_EnvoyStay);

        /// <summary>
        /// Their stay ends now, as Hospitality's own ends when its time is up (its message, its reckoning of the visit, its
        /// gift and next visit): the transition out of the stay that the stay's length sets off; still walking in, straight
        /// to its way out. True once they are leaving.
        /// </summary>
        public static bool Leave(Lord l)
        {
            if (!Hosted(l) || l.Graph?.transitions == null) return false;
            try
            {
                var cur = l.CurLordToil;
                Transition end = null;
                foreach (var tr in l.Graph.transitions)
                    if (tr?.triggers != null && tr.triggers.Any(t => t is Trigger_TicksPassed) && tr.sources.Any(s => visitToil.IsInstanceOfType(s)))
                    {
                        end = tr;
                        break;
                    }
                if (end == null) return false;
                inside.Remove(l.loadID);
                if (cur != null && end.sources.Contains(cur))
                {
                    end.Execute(l);
                    return true;
                }
                if (cur != null && travelToil != null && travelToil.IsInstanceOfType(cur) && end.target != null)
                {
                    l.GotoToil(end.target);
                    return true;
                }
                // Already on their way out.
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("invited guests: their stay ended", e);
                return false;
            }
        }

        /// <summary>A visit's word to its group to go home: to Hospitality's guests, the end of their stay; to Rimfolk's own group, its memo.</summary>
        public static void GoHome(Lord l, string memo)
        {
            if (l == null) return;
            if (Hosted(l))
            {
                Leave(l);
                return;
            }
            l.ReceiveMemo(memo);
        }

        /// <summary>One of the group up again with no group: back into Hospitality's the way it takes a guest in late. False if it couldn't.</summary>
        public static bool Rejoin(Lord l, Pawn p)
        {
            if (p == null || !Staying(l) || joinLate == null) return false;
            try
            {
                if (joinedLate != null && mapComp != null)
                {
                    var comp = p.Map?.components?.FirstOrDefault(c => c != null && mapComp.IsInstanceOfType(c));
                    if (comp != null) joinedLate.Invoke(comp, new object[] { p });
                }
                joinLate.Invoke(l.CurLordToil, new object[] { p });
                return l.ownedPawns.Contains(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("invited guests: back into the stay", e);
                return false;
            }
        }

        // ---------------- Hurt by the colony ----------------

        /// <summary>One of the invited in Hospitality's care hurt by the colony: the visit hears of it as from its own group (a feast's guests leave offended, an envoy's stay ends as an insult).</summary>
        public static void Harmed(Pawn victim)
        {
            var feast = Feasts.Of(victim, out var fh, out _);
            var fmap = fh?.Map;
            if (feast != null && fmap != null)
            {
                Feasts.Harmed(Homes.KeyOf(fmap));
                return;
            }
            var stay = Envoys.Of(victim, out var eh);
            var emap = eh?.Map;
            if (stay != null && emap != null) Envoys.Harmed(Homes.KeyOf(emap));
        }

        // ---------------- The look ----------------

        /// <summary>A feast's guests or an envoy at a home now (what the look follows), from the diplomacy book.</summary>
        public static bool Following
        {
            get
            {
                // Without Hospitality's guests' group there is nobody in its care to follow.
                if (!Found) return false;
                var book = DiplomacyBook.Instance;
                if (book == null) return false;
                foreach (var h in book.Live())
                    if (h.feast != null && h.feast.OnMap || h.envoy != null && h.envoy.stage == EnvoyStage.Staying)
                        return true;
                return false;
            }
        }

        /// <summary>
        /// Every 250 ticks while a feast's guests or an envoy are at a home (from the guests' pass, GuestHelpBook): for those
        /// in Hospitality's care, the feast's look their own group would have given, and each group kept where the visit wants it.
        /// </summary>
        public static void Rare()
        {
            var book = DiplomacyBook.Instance;
            if (book == null) return;
            foreach (var h in book.Live())
            {
                try
                {
                    if (h.feast != null && h.feast.OnMap) Feasts.HostedLook(h);
                    if (h.envoy != null && h.envoy.stage == EnvoyStage.Staying) Envoys.HostedLook(h);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("invited guests: the look", e);
                }
            }
        }
    }

    /// <summary>
    /// The colony hurting one of its invited guests while they are in Hospitality's care (Integrations/InvitedGuests.cs):
    /// heard as Rimfolk's own group heard it (the game's harm by the colony's people: a feast's guests leave offended, an
    /// envoy's stay ends as an insult). A blow in a bout they agreed to isn't harm (Outsiders/OutsiderPatches.cs).
    /// </summary>
    [HarmonyPatch(typeof(Lord), nameof(Lord.Notify_PawnDamaged))]
    public static class Patch_InvitedGuestHarmed
    {
        public static void Postfix(Lord __instance, Pawn victim, DamageInfo dinfo)
        {
            if (!RimfolkPatches.On || victim == null || !InvitedGuests.Hosted(__instance)) return;
            try
            {
                var by = dinfo.Instigator;
                if (by?.Faction == null || by.Faction != Faction.OfPlayer || dinfo.Def == null || !dinfo.Def.ExternalViolenceFor(victim)) return;
                if (Outsiders.InBout(victim, by as Pawn)) return;
                InvitedGuests.Harmed(victim);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("invited guests: hurt by the colony", e);
            }
        }
    }
}
