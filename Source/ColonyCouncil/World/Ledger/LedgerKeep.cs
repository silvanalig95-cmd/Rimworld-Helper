using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>Why the ledger keeps someone in the world: one reason per part of Rimfolk that needs them.</summary>
    public static class KeepFor
    {
        /// <summary>A notable (World/Notables.cs).</summary>
        public const string Notable = "notable";
        /// <summary>A memo a later event can pick up (Happenings: Memo).</summary>
        public const string Memo = "memo";
        /// <summary>A colonist who walked out or was sent away (Happenings: Departed).</summary>
        public const string Departed = "departed";
        /// <summary>A visitor who comes back (Happenings/Regulars.cs).</summary>
        public const string Regular = "regular";
        /// <summary>A camp that may come back (Camps/CampMemory.cs: setting off, then a past camp's people).</summary>
        public const string Camp = "camp";
        /// <summary>A story chain running (Happenings/Workers_Chains2.cs: StoryChains).</summary>
        public const string Chain = "chain";
        /// <summary>Who led settlers to a town being founded or kept in detail (World/Region/RegionFaces.cs).</summary>
        public const string Founding = "founding";
        /// <summary>Settlers on their way to found a hamlet under the colony's protection (World/Region/Hamlet.cs).</summary>
        public const string Hamlet = "hamlet";
        /// <summary>A people's envoy staying the season at a home of the colony's, and their attendant (Diplomacy/Envoys.cs).</summary>
        public const string Envoy = "envoy";
        /// <summary>One of the colony's away for a term among another people, as the colony's envoy at their town (Diplomacy/EnvoysAbroad.cs).</summary>
        public const string Lent = "lent";
        /// <summary>A ward of theirs agreed and not yet come, or fostered and gone home while the colony remembers them (Diplomacy/Wards.cs).</summary>
        public const string Ward = "ward";
        /// <summary>A hostage of theirs agreed and not yet come to the colony (Diplomacy/Hostages.cs).</summary>
        public const string Hostage = "hostage";

        /// <summary>A world situation running (World/WorldSituation.cs: held), by its id.</summary>
        public static string Situation(int id) => "situation:" + id;

        public static bool IsSituation(string reason) => reason != null && reason.StartsWith("situation:", StringComparison.Ordinal);
    }

    /// <summary>
    /// One way to keep a pawn in the world: <see cref="Keep"/> gives a reason, <see cref="Release"/> takes it back, and a
    /// pawn stays while any reason holds. The game is told the way Rimfolk always told it: its world pawns' forceful
    /// keep (<c>Find.WorldPawns.ForcefullyKeptPawns</c>, the set PawnDiscardDecideMode.KeepForever adds to), added when a
    /// reason is given; each part lets go its own way (back to the world's keeping, or gone for good) once
    /// <see cref="Release"/> says nothing else holds them. The parts of Rimfolk that keep people without a reason of
    /// their own here are asked too (<see cref="Held"/>): a nemesis, a raid one of them leads, an open mission (its
    /// quest reserves them in the game's own way), and a world situation's stranger or other.
    /// </summary>
    public partial class WorldLedger
    {
        /// <summary>The reasons of an older save's keeping were noted (1), once.</summary>
        private int keepsNoted;

        /// <summary>
        /// Keeps a pawn in the world for a reason. With <paramref name="tell"/> the game is told now (its forceful keep);
        /// returns whether that put them there (they weren't kept already). Without a game or the ledger, it only tells.
        /// </summary>
        public static bool Keep(Pawn p, string reason, bool tell = true)
        {
            if (p == null || reason.NullOrEmpty()) return false;
            var r = PersonOf(p);
            var rec = PersonRecord(r);
            if (rec != null && LedgerMath.AddReason(rec.kept, reason)) Changed();
            return tell && Find.WorldPawns != null && Find.WorldPawns.ForcefullyKeptPawns.Add(p);
        }

        /// <summary>
        /// One reason no longer holds. Returns whether nothing keeps them any more (no reason left here, and nothing
        /// else of Rimfolk's holding them): then the caller lets them go its own way. The game isn't told here.
        /// </summary>
        public static bool Release(Pawn p, string reason)
        {
            if (p == null) return false;
            var rec = PersonRecord(Refs.Of(p));
            if (rec != null && LedgerMath.RemoveReason(rec.kept, reason)) Changed();
            return !Held(p);
        }

        /// <summary>Some reason here keeps them, or another part of Rimfolk holds them (see the class).</summary>
        public static bool Held(Pawn p, bool nemesis = true)
        {
            if (p == null) return false;
            var rec = PersonRecord(Refs.Of(p));
            return LedgerMath.Held(rec?.kept, false) || HeldOutside(p, nemesis);
        }

        /// <summary>A reason here other than this one keeps them, or another part of Rimfolk holds them.</summary>
        public static bool HeldBesides(Pawn p, string reason)
        {
            if (p == null) return false;
            var rec = PersonRecord(Refs.Of(p));
            return LedgerMath.HeldBesides(rec?.kept, reason, false) || HeldOutside(p, true);
        }

        /// <summary>Kept for this reason.</summary>
        public static bool KeptFor(Pawn p, string reason)
        {
            if (p == null) return false;
            var rec = PersonRecord(Refs.Of(p));
            return rec?.kept != null && rec.kept.Contains(reason);
        }

        private static bool HeldOutside(Pawn p, bool nemesis)
        {
            var comp = CouncilGameComponent.Instance;
            if (nemesis && comp?.nemeses != null && comp.nemeses.IsNemesis(p)) return true;
            if (comp?.happenings != null && comp.happenings.raids.Any(r => r != null && r.leader == p)) return true;
            if (Missions.Holds(p)) return true;
            return WorldSituations.Holds(p);
        }

        // ---------------- Keeping it true ----------------

        /// <summary>Is this reason still true, by its part's own record? (The day's check, and an older save's.)</summary>
        private static bool StillHolds(Pawn p, string reason)
        {
            var comp = CouncilGameComponent.Instance;
            var t = comp?.happenings;
            switch (reason)
            {
                case KeepFor.Notable: return NotableBook.Instance?.notables.Any(n => n != null && n.pawn == p) ?? false;
                case KeepFor.Memo: return t != null && t.Holds(p);
                case KeepFor.Departed: return t != null && t.departed.Any(d => d != null && d.pawn == p);
                case KeepFor.Regular: return t != null && t.regulars.Any(r => r != null && r.pawn == p);
                case KeepFor.Camp:
                    return CampReturns.Past.Any(c => c?.people != null && c.people.Contains(p))
                           || (CampTracker.Instance?.All.Any(c => c?.members != null && c.members.Contains(p)) ?? false);
                case KeepFor.Chain: return StoryChains.Holds(p);
                case KeepFor.Founding: return FounderNamed(Refs.Of(p));
                // A raiders' camp's leader (World/Region/RaidersCamp.cs), and a follow-up's own person while it waits (World/Region/FollowUps.cs).
                case RaidersCamps.KeepReason: return RaidersCamps.Leads(p);
                case FollowUps.KeepReason: return FollowUps.Holds(p);
                // Settlers on their way to found a hamlet (World/Region/Hamlet.cs).
                case KeepFor.Hamlet: return Hamlets.Holds(p);
                // A people's envoy staying the season, and the colony's own envoy away (Diplomacy/Envoys.cs, EnvoysAbroad.cs).
                case KeepFor.Envoy: return Envoys.Holds(p);
                case KeepFor.Lent: return EnvoysAbroad.Holds(p) || WardsAbroad.Holds(p);
                // Wards and hostages of theirs, not yet come, and a ward fostered and gone home (Diplomacy/Wards.cs, Hostages.cs).
                case KeepFor.Ward: return Wards.Holds(p);
                case KeepFor.Hostage: return Hostages.Holds(p);
            }
            if (KeepFor.IsSituation(reason) && int.TryParse(reason.Substring("situation:".Length), out int id))
                return WorldSituations.Instance?.Running.Any(s => s != null && s.id == id && !s.ended && s.held.Contains(p)) ?? false;
            return false;
        }

        /// <summary>
        /// The day's check: a reason whose part let go of them without saying so (an edited save, a record dropped on
        /// load) is taken back. Nobody is let go of here; they are only no longer held.
        /// </summary>
        private void CheckKeeps()
        {
            foreach (var rec in persons)
            {
                if (rec?.kept == null || rec.kept.Count == 0) continue;
                var p = rec.pawn;
                int before = rec.kept.Count;
                rec.kept.RemoveAll(k => p == null || p.Discarded || !StillHolds(p, k));
                if (rec.kept.Count != before) Changed();
            }
        }

        /// <summary>
        /// An older save, kept by each part on its own: the reasons that hold now are noted (the game's keep is left as
        /// the save had it).
        /// </summary>
        private void NoteOlderKeeps()
        {
            var comp = CouncilGameComponent.Instance;
            var t = comp?.happenings;
            void Note(Pawn p, string reason)
            {
                if (p != null && !p.Discarded) Keep(p, reason, false);
            }
            var book = NotableBook.Instance;
            if (book != null)
                foreach (var n in book.notables)
                    if (n?.pawn != null && !n.pawn.Dead)
                        Note(n.pawn, KeepFor.Notable);
            if (t != null)
            {
                foreach (var m in t.memos)
                    if (m != null && !m.used && m.pawn != null)
                        Note(m.pawn, KeepFor.Memo);
                foreach (var d in t.departed)
                    if (d?.pawn != null)
                        Note(d.pawn, KeepFor.Departed);
                foreach (var r in t.regulars)
                    if (r?.pawn != null)
                        Note(r.pawn, KeepFor.Regular);
            }
            foreach (var c in CampReturns.Past)
                if (c?.people != null)
                    foreach (var p in c.people)
                        Note(p, KeepFor.Camp);
            foreach (var c in StoryChains.All)
                if (c != null && c.Running)
                {
                    Note(c.x, KeepFor.Chain);
                    Note(c.y, KeepFor.Chain);
                }
            var ws = WorldSituations.Instance;
            if (ws != null)
                foreach (var s in ws.Running)
                    if (s != null && !s.ended)
                        foreach (var p in s.held)
                            Note(p, KeepFor.Situation(s.id));
        }

        private void ExposeKeeps()
        {
            Scribe_Values.Look(ref keepsNoted, "keepsNoted");
        }

        /// <summary>Once the save has loaded: an older save's reasons noted, once.</summary>
        private void AfterLoadKeeps()
        {
            if (keepsNoted >= 1) return;
            try
            {
                NoteOlderKeeps();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("world ledger: an older save's people kept in the world", e);
            }
            keepsNoted = 1;
        }
    }
}
