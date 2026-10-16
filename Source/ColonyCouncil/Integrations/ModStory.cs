using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A personal story for A RimWorld of Magic's mages and fighters: "the long discipline". Someone of a class (read
    /// by <see cref="MagicClasses"/>) has someone at home further along in the same art (a mage for a mage, a fighter for
    /// a fighter), or, when they're frustrated with themselves, a close friend to talk it over with. The story waits for
    /// a talk, then for a level gained in their class (a Custom gate: <see cref="ArcGate.gain"/> levels above where the
    /// stage began), then for a few days. Without the mod nobody has a class, and the story never begins.
    /// </summary>
    public class ArcWorker_MagicDiscipline : ArcWorker
    {
        /// <summary>How much further along a teacher must be (class levels).</summary>
        public const int TeacherLead = 5;

        /// <summary>Their spellcasting level, or their combat level for a fighter; -1 for neither.</summary>
        public static int ClassLevel(Pawn p, out bool might)
        {
            int magic = MagicClasses.Level(p, false);
            if (magic >= 0)
            {
                might = false;
                return magic;
            }
            might = true;
            return MagicClasses.Level(p, true);
        }

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            int level = ClassLevel(p, out bool might);
            if (level < 0 || p.DevelopmentalStage.Juvenile()) return null;
            // Someone further along in the same art, who doesn't think ill of them (nor they of the teacher).
            Pawn teacher = null;
            int best = level + TeacherLead - 1;
            foreach (var o in home)
            {
                if (o == null || o == p || o.Dead || o.DevelopmentalStage.Juvenile()) continue;
                int theirs = ClassLevel(o, out bool theirMight);
                if (theirs <= best || theirMight != might || ArcUtil.Opinion(p, o) < 0 || ArcUtil.Opinion(o, p) < 0) continue;
                teacher = o;
                best = theirs;
            }
            if (teacher != null) return new ArcCandidate { other = teacher, tag = "teacher", weight = 1.2f };
            // Or frustrated with themselves, and a friend to say so to.
            if (ArcUtil.Feeling(p, "Frustration") == null) return null;
            var friend = ArcUtil.Closest(p, home, 20f);
            return friend == null ? null : new ArcCandidate { other = friend, tag = "friend" };
        }

        /// <summary>The class gone (a magic that was taken away): there's nothing left to work at.</summary>
        public override string Broken(Arc arc) => ClassLevel(arc.pawn, out _) < 0 ? "lost" : null;

        /// <summary>
        /// A level gained since the stage began. The stage's starting level is kept in <see cref="Arc.baseRecord"/> (unused
        /// by a stage without a Record gate, and reset to 0 at every new stage) as the level plus a half, so it's set once.
        /// </summary>
        public override bool CustomGate(Arc arc, ArcGate gate)
        {
            int level = ClassLevel(arc.pawn, out _);
            if (level < 0) return false;
            if (!Marked(arc.baseRecord))
            {
                arc.baseRecord = level + 0.5f;
                return false;
            }
            return level >= Mathf.FloorToInt(arc.baseRecord) + Mathf.Max(1, gate.gain);
        }

        private static bool Marked(float v) => Mathf.Abs(v - Mathf.Floor(v) - 0.5f) < 0.01f;

        /// <summary>For the Story tab: the level they stand at and the one the stage waits for, or null.</summary>
        public static string Progress(Arc arc)
        {
            if (!(arc?.def?.Worker is ArcWorker_MagicDiscipline) || arc.Gate?.kind != ArcGateKind.Custom || !Marked(arc.baseRecord)) return null;
            int level = ClassLevel(arc.pawn, out _);
            if (level < 0) return null;
            return "CC_Arc_Progress_Level".Translate(level, Mathf.FloorToInt(arc.baseRecord) + Mathf.Max(1, arc.Gate.gain)).Resolve();
        }
    }
}
