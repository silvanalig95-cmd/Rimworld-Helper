using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Who stood beside whom in the fight going on at home, looked at every 250 ticks while one is on
    /// (Sched.RareFightSides): pairs of the colony's fighters seen side by side (close, and in the same room or in sight
    /// of each other), and what one did for another under fire (carried them out, tended them, stood over them while
    /// they were down). The talk's memories of a fight (Social/Talk/TalkHooks.cs) and comradeship (SocialWeb) both read
    /// it when the fight is over. Not saved: a fight loaded halfway is looked at from the load on.
    /// </summary>
    public static class FightSides
    {
        /// <summary>How often each pair (PersonaMath.PairKey) was seen side by side, and how many looks there were.</summary>
        private static readonly Dictionary<long, int> near = new Dictionary<long, int>();
        /// <summary>What a helper did for someone hurt (BondsMath.Deed* flags), by helper and helped (<see cref="DeedKey"/>).</summary>
        private static readonly Dictionary<long, int> deeds = new Dictionary<long, int>();

        public static int Samples { get; private set; }

        /// <summary>Within this many cells of someone of ours who is down, a fighter is standing over them.</summary>
        public const float GuardRange = 5f;

        private static long DeedKey(Pawn helper, Pawn helped) => ((long)helper.thingIDNumber << 32) | (uint)helped.thingIDNumber;

        /// <summary>Looks at every home map with a fight on: who is fighting side by side, and who is helping whom.</summary>
        public static void Sample()
        {
            int now = Find.TickManager.TicksGame;
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome || !MapFacts.ThreatOn(map)) continue;
                var fighting = new List<Pawn>();
                var down = new List<Pawn>();
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (p.DevelopmentalStage.Baby() || !People.HasInnerLife(p)) continue;
                    if (p.Downed)
                    {
                        down.Add(p);
                        continue;
                    }
                    var ms = p.mindState;
                    if (p.Drafted || p.CurJobDef == JobDefOf.ManTurret || (ms != null && now - ms.lastAttackTargetTick < 600)) fighting.Add(p);
                    // Under fire: carrying someone of ours out, or tending them while the fight goes on.
                    if (p.CurJob?.targetA.Thing is Pawn hurt && hurt != p && hurt.IsColonist)
                    {
                        if (p.CurJobDef == JobDefOf.Rescue) Note(p, hurt, BondsMath.DeedRescued);
                        else if (p.CurJobDef == JobDefOf.TendPatient) Note(p, hurt, BondsMath.DeedTended);
                    }
                }
                foreach (var hurt in down)
                    foreach (var f in fighting)
                        // together: a fighter standing over someone of ours who is down, in sight of them.
                        if (f.Position.InHorDistOf(hurt.Position, GuardRange) && SocialLog.Near(f, hurt)) Note(f, hurt, BondsMath.DeedGuarded);
                if (fighting.Count < 2) continue;
                Samples++;
                for (int i = 0; i < fighting.Count; i++)
                    for (int j = i + 1; j < fighting.Count; j++)
                    {
                        // together: two in the same fight, close and in sight of each other.
                        if (!SocialLog.Near(fighting[i], fighting[j])) continue;
                        long key = PersonaMath.PairKey(fighting[i].thingIDNumber, fighting[j].thingIDNumber);
                        near.TryGetValue(key, out int n);
                        near[key] = n + 1;
                    }
                if (near.Count > 2000) near.Clear();
                if (deeds.Count > 500) deeds.Clear();
            }
        }

        private static void Note(Pawn helper, Pawn helped, int deed)
        {
            long key = DeedKey(helper, helped);
            deeds[key] = (deeds.TryGetValue(key, out int d) ? d : 0) | deed;
        }

        /// <summary>How many looks saw the two side by side in this fight.</summary>
        public static int Near(Pawn a, Pawn b) =>
            a == null || b == null ? 0 : near.TryGetValue(PersonaMath.PairKey(a.thingIDNumber, b.thingIDNumber), out int n) ? n : 0;

        /// <summary>The pairs seen side by side, by pair key, for the talk's memories.</summary>
        public static IReadOnlyDictionary<long, int> NearCounts => near;

        /// <summary>What <paramref name="helper"/> did for <paramref name="helped"/> under fire (BondsMath.Deed* flags).</summary>
        public static int Deeds(Pawn helper, Pawn helped) =>
            helper == null || helped == null ? 0 : deeds.TryGetValue(DeedKey(helper, helped), out int d) ? d : 0;

        /// <summary>The fight is over and read: the next one starts afresh.</summary>
        public static void Clear()
        {
            near.Clear();
            deeds.Clear();
            Samples = 0;
        }
    }
}
