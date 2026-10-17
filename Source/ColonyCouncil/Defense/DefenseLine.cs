using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A defense plan placed for a line: which plan, where, turned and mirrored how. Its progress is read by planning it again.</summary>
    public class DefenseLinePlan : IExposable
    {
        public string template;
        public IntVec3 anchor;
        public int rotation;
        public bool mirror;
        public bool closeGap = true;
        public int tick;

        public DefenseTemplateDef Def => template.NullOrEmpty() ? null : DefDatabase<DefenseTemplateDef>.GetNamedSilentFail(template);

        public void ExposeData()
        {
            Scribe_Values.Look(ref template, "template");
            Scribe_Values.Look(ref anchor, "anchor");
            Scribe_Values.Look(ref rotation, "rotation");
            Scribe_Values.Look(ref mirror, "mirror");
            Scribe_Values.Look(ref closeGap, "closeGap", true);
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    /// <summary>A step marked done, and when it first was.</summary>
    public class DefenseLineMark : IExposable
    {
        public string step;
        public int tick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref step, "step");
            Scribe_Values.Look(ref tick, "tick");
        }
    }

    /// <summary>How one raid met a line: who came through the gate, round it or through the walls, who fell where, and what it teaches.</summary>
    public class DefenseLineRaid : IExposable
    {
        /// <summary>The line it is about (while the raid lasts).</summary>
        public int line = -1;
        public int tick;
        public string faction;
        public int through, roundLeft, roundRight, broke, inside;
        public int downedFront, downedGate, downedCoverLeft, downedCoverRight, downedZone, downedBehind;
        /// <summary>"Hana at the sandbags on the left": who was downed near the line, and where.</summary>
        public List<string> downed = new List<string>();
        /// <summary>The lessons (<see cref="DefenseLineMath.Lesson"/> names), weightiest first.</summary>
        public List<string> lessons = new List<string>();

        public DefenseLineMath.RaidTally Tally => new DefenseLineMath.RaidTally
        {
            through = through, roundLeft = roundLeft, roundRight = roundRight, broke = broke, inside = inside,
            downedFront = downedFront, downedGate = downedGate, downedCoverLeft = downedCoverLeft, downedCoverRight = downedCoverRight,
            downedZone = downedZone, downedBehind = downedBehind
        };

        public bool Empty => Tally.Raiders == 0 && Tally.Downed == 0;

        public void ExposeData()
        {
            Scribe_Values.Look(ref line, "line", -1);
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref faction, "faction");
            Scribe_Values.Look(ref through, "through");
            Scribe_Values.Look(ref roundLeft, "roundLeft");
            Scribe_Values.Look(ref roundRight, "roundRight");
            Scribe_Values.Look(ref broke, "broke");
            Scribe_Values.Look(ref inside, "inside");
            Scribe_Values.Look(ref downedFront, "downedFront");
            Scribe_Values.Look(ref downedGate, "downedGate");
            Scribe_Values.Look(ref downedCoverLeft, "downedCoverLeft");
            Scribe_Values.Look(ref downedCoverRight, "downedCoverRight");
            Scribe_Values.Look(ref downedZone, "downedZone");
            Scribe_Values.Look(ref downedBehind, "downedBehind");
            Scribe_Collections.Look(ref downed, "downed", LookMode.Value);
            Scribe_Collections.Look(ref lessons, "lessons", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                downed ??= new List<string>();
                lessons ??= new List<string>();
            }
        }
    }

    /// <summary>
    /// A defense line the player builds up step by step with the helper: a gap or opening where raiders cross, the side
    /// they come from, what the player wants of it (hold a gate, a killbox, a turret line, an archers' wall), the steps
    /// done or set aside, the plans placed for it, and how the last raids met it. Kept per map (<see cref="DefenseLines"/>).
    /// </summary>
    public class DefenseLine : IExposable
    {
        public int id;
        /// <summary>The player's name for it; null for the one the helper gives.</summary>
        public string name;
        /// <summary>The gate, or the cell of the opening where raiders cross.</summary>
        public IntVec3 gate;
        /// <summary>The side raiders come from (0 north .. 3 west): the line faces it.</summary>
        public int facing;
        /// <summary>The cells across the gap when the line was made, and how wide it was.</summary>
        public List<IntVec3> span = new List<IntVec3>();
        public int width;
        /// <summary>What the gap was when the line was made: pass, bridge, gate, gap or open (for its name).</summary>
        public string place = "gap";
        public int created;
        /// <summary>gate, killbox, turrets or archers (<see cref="Aims"/>).</summary>
        public string aim = "gate";
        public List<DefenseLineMark> done = new List<DefenseLineMark>();
        public List<string> skipped = new List<string>();
        public List<DefenseLinePlan> plans = new List<DefenseLinePlan>();
        public List<DefenseLineRaid> raids = new List<DefenseLineRaid>();
        /// <summary>The card is folded to one row.</summary>
        public bool folded;

        public const int MaxRaids = 5, MaxPlans = 24;

        public static readonly string[] Aims = { "gate", "killbox", "turrets", "archers" };

        /// <summary>Changes whenever the line does (a plan, a skip, a new aim), so its review is worked out again.</summary>
        [Unsaved] public int version;

        public void Changed() => version++;

        public bool Skipped(string step) => skipped.Contains(step);

        public int DoneAt(string step) => done.FirstOrDefault(m => m.step == step)?.tick ?? -1;

        public void MarkDone(string step, int tick)
        {
            if (step.NullOrEmpty() || done.Any(m => m.step == step)) return;
            done.Add(new DefenseLineMark { step = step, tick = tick });
        }

        public DefenseLineRaid LastRaid => raids.Count > 0 ? raids[raids.Count - 1] : null;

        public void AddPlan(DefenseLinePlan p)
        {
            if (p == null) return;
            // The same plan in the same place replaces the old one.
            plans.RemoveAll(o => o.template == p.template && o.anchor == p.anchor && o.rotation == p.rotation && o.mirror == p.mirror);
            plans.Add(p);
            if (plans.Count > MaxPlans) plans.RemoveRange(0, plans.Count - MaxPlans);
            Changed();
        }

        public IntVec3 Outward => IntVec3.North.RotatedBy(new Rot4(DefenseLineMath.Norm(facing)));

        /// <summary>The step to the right along the line, looking out from the gate.</summary>
        public IntVec3 Right
        {
            get
            {
                DefenseLineMath.Side(facing, out int sx, out int sz);
                return new IntVec3(sx, 0, sz);
            }
        }

        public int Ahead(IntVec3 c) => DefenseLineMath.Ahead(c.x - gate.x, c.z - gate.z, facing);

        public int Lateral(IntVec3 c) => DefenseLineMath.Lateral(c.x - gate.x, c.z - gate.z, facing);

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Values.Look(ref gate, "gate");
            Scribe_Values.Look(ref facing, "facing");
            Scribe_Collections.Look(ref span, "span", LookMode.Value);
            Scribe_Values.Look(ref width, "width");
            Scribe_Values.Look(ref place, "place", "gap");
            Scribe_Values.Look(ref created, "created");
            Scribe_Values.Look(ref aim, "aim", "gate");
            Scribe_Collections.Look(ref done, "done", LookMode.Deep);
            Scribe_Collections.Look(ref skipped, "skipped", LookMode.Value);
            Scribe_Collections.Look(ref plans, "plans", LookMode.Deep);
            Scribe_Collections.Look(ref raids, "raids", LookMode.Deep);
            Scribe_Values.Look(ref folded, "folded");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                span ??= new List<IntVec3>();
                done ??= new List<DefenseLineMark>();
                done.RemoveAll(m => m == null || m.step.NullOrEmpty());
                skipped ??= new List<string>();
                plans ??= new List<DefenseLinePlan>();
                plans.RemoveAll(p => p == null || p.template.NullOrEmpty());
                raids ??= new List<DefenseLineRaid>();
                raids.RemoveAll(r => r == null);
                if (!Aims.Contains(aim)) aim = "gate";
                facing = DefenseLineMath.Norm(facing);
            }
        }
    }
}
