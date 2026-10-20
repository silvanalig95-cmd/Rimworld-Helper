using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A mark left on a person (<see cref="PersonaMarkDef"/>): when, who else was in it, what it was about.</summary>
    public class PersonaMark : IExposable
    {
        public PersonaMarkDef def;
        public int tick;
        /// <summary>The other person in it (thingIDNumber), or -1; their name is kept in case they're gone.</summary>
        public int other = -1;
        public string otherName;
        public string label;

        public void ExposeData()
        {
            bool lost = false;
            CouncilScribe.LookDef(ref def, "def", ref lost);
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref other, "other", -1);
            Scribe_Values.Look(ref otherName, "otherName");
            Scribe_Values.Look(ref label, "label");
        }
    }

    /// <summary>
    /// What Rimfolk keeps of one person beyond their nature (which is worked out from the game each time): how far
    /// what happened has moved them (lasting and fading), the marks that did it, and where they stand on the things
    /// people talk about. Kept by thingIDNumber with the name, so a record outlives a pawn the game lets go of only as
    /// long as the game still knows them.
    /// </summary>
    public class PawnPersona : IExposable
    {
        public int id;
        public string name;
        public float[] lasting = new float[PersonaMath.Axes];
        public float[] drift = new float[PersonaMath.Axes];
        /// <summary>When the drift was last faded (it's faded lazily, when it's read or added to).</summary>
        public int driftTick;
        public List<PersonaMark> marks = new List<PersonaMark>();
        /// <summary>Where they stand on a topic (a TopicDef's defName, or any key a system uses), -1..1.</summary>
        public Dictionary<string, float> stances = new Dictionary<string, float>();

        public const int MaxMarks = 12;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            var l = lasting?.ToList();
            var d = drift?.ToList();
            Scribe_Collections.Look(ref l, "lasting", LookMode.Value);
            Scribe_Collections.Look(ref d, "drift", LookMode.Value);
            Scribe_Values.Look(ref driftTick, "driftTick");
            Scribe_Collections.Look(ref marks, "marks", LookMode.Deep);
            Scribe_Collections.Look(ref stances, "stances", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                lasting = Fit(l);
                drift = Fit(d);
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lasting ??= new float[PersonaMath.Axes];
                drift ??= new float[PersonaMath.Axes];
                marks ??= new List<PersonaMark>();
                marks.RemoveAll(m => m?.def == null);
                stances ??= new Dictionary<string, float>();
            }
        }

        private static float[] Fit(List<float> list)
        {
            var a = new float[PersonaMath.Axes];
            if (list != null)
                for (int i = 0; i < a.Length && i < list.Count; i++) a[i] = list[i];
            return a;
        }

        /// <summary>Fades the drift up to now.</summary>
        public void Fade(int now)
        {
            if (driftTick <= 0)
            {
                driftTick = now;
                return;
            }
            float days = (now - driftTick) / (float)GenDate.TicksPerDay;
            if (days < 0.25f) return;
            for (int i = 0; i < drift.Length; i++) drift[i] = PersonaMath.Faded(drift[i], days);
            driftTick = now;
        }

        /// <summary>Nothing worth keeping: no move, no mark, no stance.</summary>
        public bool Empty => marks.Count == 0 && stances.Count == 0 && lasting.All(v => v == 0f) && drift.All(v => v == 0f);

        /// <summary>Marks past their keeping are let go; if there are still too many, the oldest minor ones go first.</summary>
        public void PruneMarks(int now)
        {
            marks.RemoveAll(m => m.def.keepDays > 0 && now - m.tick > m.def.keepDays * GenDate.TicksPerDay * (m.def.major ? 3 : 1));
            while (marks.Count > MaxMarks)
            {
                var drop = marks.Where(m => !m.def.major).OrderBy(m => m.tick).FirstOrDefault() ?? marks.OrderBy(m => m.tick).First();
                marks.Remove(drop);
            }
        }
    }

    /// <summary>Something two people went through together, which either of them may bring up (<see cref="SharedMemoryDef"/>).</summary>
    public class SharedMemory : IExposable
    {
        public SharedMemoryDef def;
        public int a, b;
        public string aName, bName;
        /// <summary>What it was ("the raid on the 5th of Aprimay", "the boomalope"): set by whoever made the memory.</summary>
        public string label;
        public int tick;
        public float weight = 1f;
        /// <summary>How often it has come up, and when last.</summary>
        public int recalls, lastRecalled = -1;

        public bool Has(int id) => a == id || b == id;
        public int Other(int id) => a == id ? b : a;
        public string NameOf(int id) => a == id ? aName : bName;

        /// <summary>Its weight now, faded with age (<see cref="SharedMemoryDef.fadeDays"/>); a joke that's been told too often wears out.</summary>
        public float WeightNow(int now)
        {
            float w = weight * (def?.weight ?? 1f);
            if (def != null && def.fadeDays > 0)
                w *= (float)System.Math.Pow(0.5, (now - tick) / (double)(def.fadeDays * GenDate.TicksPerDay));
            if (def != null && def.joke && recalls > 4) w *= 4f / recalls;
            return w;
        }

        public void ExposeData()
        {
            bool lost = false;
            CouncilScribe.LookDef(ref def, "def", ref lost);
            Scribe_Values.Look(ref a, "a");
            Scribe_Values.Look(ref b, "b");
            Scribe_Values.Look(ref aName, "aName");
            Scribe_Values.Look(ref bName, "bName");
            Scribe_Values.Look(ref label, "label");
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref weight, "weight", 1f);
            Scribe_Values.Look(ref recalls, "recalls");
            Scribe_Values.Look(ref lastRecalled, "lastRecalled", -1);
        }
    }
}
