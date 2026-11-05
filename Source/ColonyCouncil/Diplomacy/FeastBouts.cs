using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    // A feast's wrestling and a guest's night gone sour (Diplomacy/Feasts.cs, FeastContests.cs) use the outsiders' own bouts
    // and brawls (Outsiders/OutsiderOutcomes.cs): the same jobs, swings, referee and memories, entered here from the feast.
    public static partial class Outsiders
    {
        /// <summary>
        /// A wrestling bout at a feast, by the sparring rules (eight swings each, or until one yields to the pain), at a ring
        /// the feast chose: no asking (the guest came for the contests), and its one hard blow decided by the feast
        /// (<paramref name="hard"/>: FeastMath.HardBlow, under the harm rule's shared cap). Both are sent to the ring at
        /// once. Returns the bout, or null when it can't begin.
        /// </summary>
        public static OutsiderBout FeastBout(Pawn c, Pawn x, IntVec3 ring, bool hard)
        {
            if (c == null || x == null || !c.Spawned || !x.Spawned || c.Map != x.Map || !ring.IsValid) return null;
            var map = c.Map;
            var theirs = IntVec3.Invalid;
            foreach (var cell in GenAdj.CellsAdjacent8Way(new TargetInfo(ring, map)))
                if (cell.InBounds(map) && cell.Standable(map) && x.CanReach(cell, PathEndMode.OnCell, Danger.Some))
                {
                    theirs = cell;
                    break;
                }
            if (!theirs.IsValid) return null;
            var bout = new OutsiderBout { a = c, b = x, kind = BoutKind.Spar, terms = DuelTerms.Yield, ring = ring, start = Now };
            foreach (var h in c.health.hediffSet.hediffs) bout.before.Add(h);
            foreach (var h in x.health.hediffSet.hediffs) bout.before.Add(h);
            if (hard) bout.hardAt = Rand.RangeInclusive(3, OutsiderMath.SparSwings * 2 - 2);
            bouts.RemoveAll(b => b.Has(c) || b.Has(x));
            bouts.Add(bout);
            var jx = JobMaker.MakeJob(OutsiderDefOf.Council_OutsiderBout, c, theirs);
            jx.count = (int)BoutKind.Spar;
            x.jobs.StartJob(jx, JobCondition.InterruptForced, null, false, true);
            var jc = JobMaker.MakeJob(OutsiderDefOf.Council_OutsiderBout, x, ring);
            jc.count = (int)BoutKind.Spar;
            jc.playerForced = true;
            c.jobs.StartJob(jc, JobCondition.InterruptForced, null, false, true);
            if (c.CurJobDef != OutsiderDefOf.Council_OutsiderBout || x.CurJobDef != OutsiderDefOf.Council_OutsiderBout)
            {
                bouts.Remove(bout);
                if (c.CurJobDef == OutsiderDefOf.Council_OutsiderBout) c.jobs.EndCurrentJob(JobCondition.InterruptForced);
                if (x.CurJobDef == OutsiderDefOf.Council_OutsiderBout) x.jobs.EndCurrentJob(JobCondition.InterruptForced);
                return null;
            }
            return bout;
        }

        /// <summary>
        /// A guest's night gone sour: hard words with one of the colony's come to blows, the outsiders' own fistfight (the
        /// game's social fight, counted as a brawl so neither people takes it for an attack; their people remember it).
        /// Returns whether the fight began.
        /// </summary>
        public static bool FeastBrawl(Pawn c, Pawn x)
        {
            if (c == null || x == null || x.interactions == null || Peaceful(x) || Peaceful(c)) return false;
            var bout = new OutsiderBout { a = c, b = x, kind = BoutKind.Brawl, start = Now };
            bouts.Add(bout);
            // together: the two are near each other after the feast (Feasts.SourNights checks it); the words and the fight are on the map.
            Say(x, "feastsour", c);
            x.interactions.StartSocialFight(c);
            if (x.MentalStateDef == MentalStateDefOf.SocialFighting) return true;
            bouts.Remove(bout);
            return false;
        }

        /// <summary>A wrestler new to the ring this bout was hurt by it (a fresh injury beyond the bruises a bout leaves): the hard blow fell on them.</summary>
        public static bool HardBlowOn(OutsiderBout bout, Pawn p) => bout != null && p != null && bout.hurt == p;

        /// <summary>The bout is over, and who won (null: a draw or called off).</summary>
        public static bool Over(OutsiderBout bout, out Pawn winner, out bool off)
        {
            winner = bout?.winner;
            off = bout == null || bout.off;
            return bout == null || bout.over;
        }

        /// <summary>The swings landed by each side so far.</summary>
        public static int Landed(OutsiderBout bout, Pawn p) => bout == null ? 0 : p == bout.a ? bout.hitsA : p == bout.b ? bout.hitsB : 0;

        /// <summary>A bout of the feast's still in the outsiders' list (a reload loses them: they aren't saved).</summary>
        public static bool Known(OutsiderBout bout) => bout != null && bouts.Contains(bout);
    }
}
