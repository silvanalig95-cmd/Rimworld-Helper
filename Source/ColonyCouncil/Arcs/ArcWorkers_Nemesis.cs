using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The fear laid to rest: the enemy who hurt them is in the colony's cells, or gone for good (dead, let go, one of
    /// ours now, never seen again), but the fear hasn't gone with them. They take it in, tell someone the whole of it,
    /// and sleep easy at last. A kind of <see cref="ArcWorker_Fear"/> with the "nemesis" tag, so the fear faced in a fight
    /// with that very enemy (Interplay's FearFacedWatch: a prisoner who breaks out) ends it well at once, as it ends the
    /// other. {NAME} is the enemy; the lines can ask for how it stands: [captured], [dead], [released], [joined], [gone],
    /// and [beast] for an animal.
    /// </summary>
    public class ArcWorker_FearLaid : ArcWorker_Fear
    {
        private static readonly List<string> Haunting = new List<string> { "Council_Nemesis_Dread", "Council_Nemesis_Escaped", "Council_Nemesis_Captured" };
        private static readonly List<string> Shaken = new List<string> { "Council_Ail_BadNights", "Council_Ail_Unease" };

        private const float RecentDays = 40f;

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.nemeses || p.DevelopmentalStage.Juvenile()) return null;
            var n = Laid(comp, p);
            if (n == null) return null;
            // Still carrying it: afraid, haunted by the enemy lately, or sleeping badly since.
            bool afraid = (ArcUtil.Feeling(p, "Fear") is FeelingNow fear && fear.intensity >= 6f)
                          || LifeConditions.Remembered(p, Haunting, 15f) != null || LifeConditions.Has(p, Shaken);
            if (!afraid) return null;
            var friend = ArcUtil.Closest(p, home, 15f);
            if (friend == null) return null;
            return new ArcCandidate { other = friend, tag = "nemesis", name = n.Name, key = State(n), weight = 1.2f };
        }

        /// <summary>The one they hurt has an enemy behind the colony's door, or one gone for good lately.</summary>
        private static Nemesis Laid(CouncilGameComponent comp, Pawn p)
        {
            int now = Find.TickManager.TicksGame;
            return comp.nemeses.Of(p)
                .Where(n => (!n.ended && n.captured) || (n.ended && n.endedTick > 0 && now - n.endedTick <= RecentDays * GenDate.TicksPerDay))
                .OrderByDescending(n => n.ended ? n.endedTick : n.tick).FirstOrDefault();
        }

        /// <summary>How it stands with them, as the story's tags: "captured", "dead", "joined", "released", "gone"; "|beast" for an animal.</summary>
        private static string State(Nemesis n)
        {
            string s;
            if (!n.ended) s = n.captured ? "captured" : "loose";
            else if (n.pawn != null && n.pawn.Dead) s = "dead";
            else if (n.pawn != null && !n.pawn.Discarded && n.pawn.Faction == Faction.OfPlayer) s = "joined";
            else if (n.pawn != null && !n.pawn.Discarded && n.pawn.guest != null && n.pawn.guest.Released) s = "released";
            else s = "gone";
            return n.IsBeast ? s + "|beast" : s;
        }

        private static Nemesis Of(Arc arc)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || arc?.pawn == null) return null;
            return comp.nemeses.Of(arc.pawn).Where(n => n.Name == arc.name).OrderByDescending(n => n.ended ? n.endedTick : n.tick).FirstOrDefault();
        }

        public override IEnumerable<string> LineTags(Arc arc)
        {
            if (arc?.key == null) yield break;
            foreach (var t in arc.key.Split('|')) yield return t;
        }

        /// <summary>A prisoner whose fate has been settled since (executed, let go, recruited): the waiting on them is over.</summary>
        public override bool CustomGate(Arc arc, ArcGate gate)
        {
            if (arc?.key == null || !arc.key.StartsWith("captured")) return false;
            var n = Of(arc);
            return n == null || n.ended;
        }

        /// <summary>Before each stage: how it stands now (the prisoner may have been dealt with since).</summary>
        public override void Settle(Arc arc)
        {
            var n = Of(arc);
            if (n != null) arc.key = State(n);
        }

        /// <summary>The prisoner got out and away: the fear has something to be about again.</summary>
        public override string Broken(Arc arc)
        {
            if (arc.stage >= arc.def.LastStage) return null;
            var n = Of(arc);
            return n != null && !n.ended && !n.captured ? "escaped" : null;
        }
    }
}
