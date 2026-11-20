using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The god one of the colony held most when they died (GraveGods), kept until their eulogy is said.</summary>
    public class DeadHeld : IExposable
    {
        public Pawn pawn;
        public int god = -1;
        public int tick;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref god, "god", -1);
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    /// <summary>
    /// The dead's own god at the grave (C15 of docs/content-update-proposals.md). At a eulogy (Secrets/Vendettas.cs, through
    /// GodDeeds.Funeral), the speaker gives the dead to the god they held (PawnGods.Held, as it stood when they died), in one
    /// sentence in the old voice over their head, and the eulogy's letter says so. Then:
    /// <list type="bullet">
    /// <item>Those of the colony who mourn the dead and hold the same god find the grief a little lighter (a small good memory,
    /// Rimfolk_Thought_GivenToOurGod, beside the grief).</item>
    /// <item>The colony's book keeps it as the speaker's belief: "laid to rest in Hush's keeping" by the colony's name for that
    /// god, or, when the colony has no name for it, in the keeping of the god they held; and, named, among its rites.</item>
    /// </list>
    /// No harm.
    /// </summary>
    public static class GraveGods
    {
        public const string Pool = "Rimfolk_Pool_GodsAmong_Grave";

        public static ThoughtDef Lighter => DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Thought_GivenToOurGod");

        /// <summary>One of the colony died: the god they held most is kept for their eulogy (their belief record goes with the day's pruning).</summary>
        internal static void Died(Pawn p)
        {
            var b = GodsAmongBook.Book;
            if (b == null || p == null || p.RaceProps?.Humanlike != true || p.Faction != Faction.OfPlayer) return;
            var g = PawnGods.Held(p);
            if (g == null || PawnGods.HoldOf(p, g) < GodsAmongMath.DeadHeldFrom) return;
            b.deadHeld.RemoveAll(d => d == null || d.pawn == p);
            b.deadHeld.Add(new DeadHeld { pawn = p, god = g.id, tick = GodsAmongBook.Now });
            while (b.deadHeld.Count > GodsAmongBook.MaxDeadHeld) b.deadHeld.RemoveAt(0);
        }

        /// <summary>The god the dead held most, as it stood when they died; null for none.</summary>
        public static God HeldBy(Pawn dead)
        {
            var b = GodsAmongBook.Book;
            var d = b?.deadHeld.LastOrDefault(x => x != null && x.pawn == dead);
            return d != null ? Gods.ById(d.god) : null;
        }

        /// <summary>
        /// The eulogy's speaker gives the dead to the god they held: the line said (for the eulogy's letter), the lighter grief
        /// of those who share that god, and the book. Null when the dead held no god.
        /// </summary>
        public static string Given(Pawn dead, Pawn speaker)
        {
            if (!Gods.On || !Worship.On || dead == null || speaker == null) return null;
            var g = HeldBy(dead);
            if (g == null || g.kind == GodKind.Dead) return null;
            string line = Worship.LineWith(Pool, speaker, g, dead, null, "give");
            if (line.NullOrEmpty()) return null;
            // apart: the speaker's words over the grave, said to nobody in particular (the eulogy is the colony's).
            if (speaker.Spawned) Speech.Say(speaker, line);
            // Those who mourn them and hold the same god.
            var thought = Lighter;
            if (thought != null)
                foreach (var p in PawnsFinder.AllMaps_FreeColonists.Where(x => x != speaker && !x.Dead && Worship.Ours(x)).ToList())
                {
                    if (PawnGods.HoldOf(p, g) < GodsAmongMath.MournerHoldsFrom || PawnGods.Dreads(p, g) || !Mourns(p, dead)) continue;
                    CouncilGameComponent.GiveThought(p, thought, dead);
                }
            if (Mourns(speaker, dead) && thought != null && PawnGods.HoldOf(speaker, g) >= GodsAmongMath.MournerHoldsFrom) CouncilGameComponent.GiveThought(speaker, thought, dead);
            // The book: the speaker's belief, by the colony's name for the god, else unnamed.
            var lore = GodLore.Instance;
            bool named = lore?.PageOf(g) != null;
            string words = named
                ? "CC_GA_Grave_Belief".Translate(dead.LabelShort.Named("DEAD"), Worship.Called(g).Named("GOD")).Resolve()
                : "CC_GA_Grave_BeliefUnnamed".Translate(dead.Named("DEAD")).Resolve();
            GodLore.Believes(speaker, named ? g : null, words);
            if (named) GodLore.Kept(g, "CC_GA_Grave_Rite".Translate().Resolve());
            PawnGodsActs.RiteKept(speaker, g);
            return line;
        }

        /// <summary>They grieve the dead now: a memory about them that weighs on their mood.</summary>
        private static bool Mourns(Pawn p, Pawn dead)
        {
            var mems = p?.needs?.mood?.thoughts?.memories?.Memories;
            if (mems == null) return false;
            foreach (var m in mems)
                if (m != null && m.otherPawn == dead && m.MoodOffset() < 0f)
                    return true;
            return false;
        }

        /// <summary>Dev: a eulogy's words to the god now, for a dead colonist the book remembers (else the first colonist, as if they had died).</summary>
        public static string DevGiven(Map map)
        {
            var b = GodsAmongBook.Book;
            if (b == null || map == null) return "the gods are off.";
            var d = b.deadHeld.LastOrDefault(x => x?.pawn != null);
            var speaker = map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => d == null || p != d.pawn);
            if (d == null || speaker == null) return "nobody of the colony has died holding a god lately.";
            string line = Given(d.pawn, speaker);
            return line ?? "nothing was said.";
        }
    }

    /// <summary>One of the colony died: the god they held is kept for their eulogy (Gods/Among/GraveGods.cs). Off with the deeper game hooks.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_GraveGodsDied
    {
        public static void Postfix(Pawn __instance)
        {
            if (!RimfolkPatches.On || __instance == null || !__instance.Dead || __instance.RaceProps?.Humanlike != true || __instance.Faction != Faction.OfPlayer) return;
            try
            {
                GraveGods.Died(__instance);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("gods among the people: the god of the dead", e);
            }
        }
    }
}
