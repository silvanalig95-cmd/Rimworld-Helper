using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A people's memory of single colonists (World/FactionMemory.cs): the one who set one of theirs free, the one who
    /// tended their wounded, the one who found their lost child or walked their pilgrim home. Who did it is noted when it
    /// happens (the warden carrying out a release, a colony doctor tending one of theirs), and when that person goes home
    /// and the game counts the good turn, the deed names the colonist (<see cref="FactionDeed.by"/>). Their people greet
    /// that colonist by name (encounters for [their:freedbyA], [their:tendedbyA], [their:helpedbyA]), their envoys and
    /// camps ask for them at the gate, and letters about them can name the colonist ({FRIEND}, lines tagged
    /// [their:friend], [their:friend:freed], [their:friend:tended], [their:dearfriend]). Saved with the faction memory.
    /// </summary>
    public static class KinMemory
    {
        private static int Now => Find.TickManager.TicksGame;

        /// <summary>One of the colony's own people, still with it.</summary>
        public static bool Ours(Pawn p) => p != null && !p.Dead && !p.Discarded && p.RaceProps != null && p.RaceProps.Humanlike && p.Faction == Faction.OfPlayer && !p.IsQuestLodger();

        /// <summary>One of a people that remembers (a prisoner keeps their own people).</summary>
        private static bool Theirs(Pawn p) => p != null && p.RaceProps != null && p.RaceProps.Humanlike && FactionMemory.Remembers(p.Faction);

        // ---------------- Noting who did it ----------------

        /// <summary>A colony doctor tended one of a people's own (a guest, a wounded visitor, a prisoner).</summary>
        public static void Notify_Tended(Pawn doctor, Pawn patient)
        {
            if (!FactionMemory.On || doctor == null || patient == null || doctor == patient || !Ours(doctor) || !Theirs(patient)) return;
            AddNote(patient, doctor, "tend");
        }

        /// <summary>A prisoner is being let go: the colonist carrying it out is the one their people will remember.</summary>
        public static void Notify_Freeing(Pawn prisoner)
        {
            if (!FactionMemory.On || prisoner == null || !Theirs(prisoner)) return;
            var map = prisoner.MapHeld;
            if (map == null) return;
            var warden = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(c => c.CurJobDef == JobDefOf.ReleasePrisoner && c.CurJob?.targetA.Thing == prisoner);
            if (warden != null) AddNote(prisoner, warden, "free");
        }

        private static void AddNote(Pawn stranger, Pawn colonist, string kind)
        {
            var mem = FactionMemory.Instance;
            if (mem == null) return;
            int now = Now;
            var n = mem.notes.FirstOrDefault(x => x != null && x.stranger == stranger && x.colonist == colonist && x.kind == kind);
            if (n != null)
            {
                n.count++;
                n.tick = now;
                return;
            }
            mem.notes.Add(new KinNote { stranger = stranger, colonist = colonist, kind = kind, tick = now });
            while (mem.notes.Count > MemoryMath.MaxNotes) mem.notes.RemoveAt(0);
        }

        /// <summary>
        /// The game counted a good turn to <paramref name="stranger"/> (gone home freed or tended): the deed names whoever
        /// did it, by <paramref name="kind"/> first ("free"), else <paramref name="fallback"/> ("tend"). A deed that already
        /// names someone keeps them: a people never forgets the first who helped.
        /// </summary>
        public static void Credit(FactionDeed d, Pawn stranger, string kind, string fallback = null)
        {
            var mem = FactionMemory.Instance;
            if (d == null || stranger == null || mem == null) return;
            try
            {
                var mine = mem.notes.Where(x => x != null && x.stranger == stranger && x.colonist != null).ToList();
                if (mine.Count == 0) return;
                var pick = Best(mine, kind) ?? (fallback != null ? Best(mine, fallback) : null);
                mem.notes.RemoveAll(x => x != null && x.stranger == stranger);
                if (pick == null || d.by != null && !d.by.Dead) return;
                Name(d, pick.colonist);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("kin memory credit", e);
            }
        }

        private static KinNote Best(List<KinNote> notes, string kind) =>
            notes.Where(x => x.kind == kind && Ours(x.colonist)).OrderByDescending(x => x.count).ThenByDescending(x => x.tick).FirstOrDefault();

        /// <summary>The deed names this colonist (a mission's finder, an escort): only if it names nobody yet.</summary>
        public static void Name(FactionDeed d, Pawn colonist)
        {
            if (d == null || colonist == null || d.by != null && !d.by.Dead) return;
            d.by = colonist;
            d.byName = colonist.LabelShort;
            FactionMemory.Touch();
        }

        /// <summary>A deed noted, naming the colonist who did it (a mission's finder or escort). Returns the deed.</summary>
        public static FactionDeed Note(Faction f, string kind, Pawn colonist, string name = null, int weight = 0)
        {
            var d = FactionMemory.Note(f, kind, name, weight);
            if (Ours(colonist)) Name(d, colonist);
            return d;
        }

        // ---------------- Who they remember ----------------

        /// <summary>
        /// The colonist a people remembers best for a good turn (still with the colony, the deed still strong enough to name
        /// them), and the deed. Null when there's nobody.
        /// </summary>
        public static Pawn Friend(Faction f, out FactionDeed deed)
        {
            deed = null;
            var mem = FactionMemory.Instance;
            if (!FactionMemory.On || mem == null || f == null || f.IsPlayer) return null;
            int now = Now;
            var list = new List<FactionDeed>();
            var strengths = new List<float>();
            var present = new List<bool>();
            foreach (var d in mem.deeds)
            {
                if (d == null || d.faction != f || d.side != DeedSide.Favor || d.by == null) continue;
                list.Add(d);
                strengths.Add(d.StrengthAt(now));
                present.Add(Ours(d.by));
            }
            int i = MemoryMath.BestFriend(strengths, present);
            if (i < 0) return null;
            deed = list[i];
            return deed.by;
        }

        /// <summary>The strongest good turn this colonist did for this people that they still name them for (or null).</summary>
        public static FactionDeed DeedBy(Faction f, Pawn p)
        {
            var mem = FactionMemory.Instance;
            if (!FactionMemory.On || mem == null || f == null || p == null) return null;
            return mem.Best(f, DeedSide.Favor, MemoryMath.NamedFriend, 0f, false, d => d.by == p);
        }

        /// <summary>The colonist a people remembers, if they're on this map and free to meet their visitors.</summary>
        public static Pawn Greeter(Faction f, Map map)
        {
            var p = Friend(f, out _);
            if (p == null || map == null || !p.Spawned || p.Map != map || p.Downed || p.InMentalState || p.Drafted) return null;
            return p;
        }

        /// <summary>"freed", "tended" or "helped": what a deed naming someone was for, as a tag.</summary>
        public static string KindTag(FactionDeed d) => d?.kind == "released" ? "freed" : d?.kind == "tended" ? "tended" : "helped";

        // ---------------- Words ----------------

        /// <summary>
        /// Tags for the lines of letters and encounters (LetterTags, camps, world events): their:friend (their people
        /// remember one colonist by name: {FRIEND}), their:friend:freed / :tended / :helped (for what), their:dearfriend
        /// (a big debt), and for the colonist A: their:freedbyA, their:tendedbyA, their:helpedbyA and their:dearA.
        /// </summary>
        public static void AddTags(HashSet<string> tags, Faction f, Pawn a)
        {
            if (tags == null || f == null || f.IsPlayer || !FactionMemory.On || !FactionMemory.Remembers(f)) return;
            try
            {
                var friend = Friend(f, out var deed);
                if (friend != null)
                {
                    tags.Add("their:friend");
                    tags.Add("their:friend:" + KindTag(deed));
                    if (MemoryMath.Greeting(deed.Strength) >= 2) tags.Add("their:dearfriend");
                }
                if (a == null) return;
                var mine = DeedBy(f, a);
                if (mine == null) return;
                tags.Add("their:" + KindTag(mine) + "byA");
                if (MemoryMath.Greeting(mine.Strength) >= 2) tags.Add("their:dearA");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("kin memory tags", e);
            }
        }

        /// <summary>{FRIEND}: the colonist this people remembers best ("?" when there's none: lines that use it ask for [their:friend]).</summary>
        public static NamedArgument FriendArg(Faction f)
        {
            Pawn p = null;
            try
            {
                if (f != null && FactionMemory.On) p = Friend(f, out _);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("kin memory friend", e);
            }
            return p != null ? p.Named("FRIEND") : "?".Named("FRIEND");
        }

        /// <summary>For the World page: "They remember Ana for it." (null when the deed names nobody).</summary>
        public static string ByLine(FactionDeed d)
        {
            if (d == null || d.byName.NullOrEmpty()) return null;
            bool gone = d.by == null || !Ours(d.by);
            return (gone ? "CC_Kin_ByGone" : "CC_Kin_By").Translate(d.byName.Named("NAME")).Resolve();
        }
    }

    // ---------------- The game's hooks ----------------

    /// <summary>A colony doctor tended someone: if they're one of a people's own, their people may remember who.</summary>
    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    public static class Patch_KinTended
    {
        public static void Postfix(Pawn doctor, Pawn patient)
        {
            if (!MemoryHooks.Listening || doctor == null || patient == null) return;
            try
            {
                KinMemory.Notify_Tended(doctor, patient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("kin memory tend hook", e);
            }
        }
    }

    /// <summary>A prisoner is let go: the warden carrying it out is noted, for their people to remember.</summary>
    [HarmonyPatch(typeof(GenGuest), nameof(GenGuest.PrisonerRelease))]
    public static class Patch_KinFreed
    {
        public static void Prefix(Pawn p)
        {
            if (!MemoryHooks.Listening || p == null) return;
            try
            {
                KinMemory.Notify_Freeing(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("kin memory release hook", e);
            }
        }
    }
}
