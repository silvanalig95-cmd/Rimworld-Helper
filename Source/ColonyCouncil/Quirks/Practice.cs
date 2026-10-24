using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What one colonist has been doing lately, by the hour: each kind of work (the work tab's), time up and busy in the
    /// small hours and in the early morning, and time out under the open sky. Looked at once an hour (an hour at it counts one), and each day it all fades a little, so it
    /// holds about the last three weeks. Habits come sooner to those who do the matching work and stay while they keep
    /// at it (Quirks.cs); a few trait changes read it too (a night owl after weeks of night work).
    /// </summary>
    public class PracticeLog : IExposable
    {
        public Pawn pawn;
        public List<string> kinds = new List<string>();
        public List<float> hours = new List<float>();
        /// <summary>Hours up and busy (at work or at leisure) between eleven at night and five in the morning.</summary>
        public float night;
        /// <summary>Hours at any work.</summary>
        public float work;
        /// <summary>Hours up and busy between four and six in the morning, before most are up.</summary>
        public float morning;
        /// <summary>Hours awake out under the open sky.</summary>
        public float outdoors;

        public const int MaxKinds = 8;

        public float Of(string kind)
        {
            int i = kinds.IndexOf(kind);
            return i < 0 ? 0f : hours[i];
        }

        /// <param name="asWork">Counted as an hour at work too (putting out fires isn't).</param>
        public void Add(string kind, bool asWork = true)
        {
            if (asWork) work += 1f;
            int i = kinds.IndexOf(kind);
            if (i >= 0)
            {
                hours[i] += 1f;
                return;
            }
            if (kinds.Count >= MaxKinds)
            {
                int low = 0;
                for (int k = 1; k < hours.Count; k++)
                    if (hours[k] < hours[low])
                        low = k;
                kinds.RemoveAt(low);
                hours.RemoveAt(low);
            }
            kinds.Add(kind);
            hours.Add(1f);
        }

        /// <summary>Once a day: what was done longer ago counts for less.</summary>
        public void Fade()
        {
            for (int i = hours.Count - 1; i >= 0; i--)
            {
                hours[i] *= PracticeMath.KeepPerDay;
                if (hours[i] < 0.25f)
                {
                    hours.RemoveAt(i);
                    kinds.RemoveAt(i);
                }
            }
            night *= PracticeMath.KeepPerDay;
            work *= PracticeMath.KeepPerDay;
            morning *= PracticeMath.KeepPerDay;
            outdoors *= PracticeMath.KeepPerDay;
        }

        public bool Empty => kinds.Count == 0 && night < 0.25f && work < 0.25f && morning < 0.25f && outdoors < 0.25f;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Collections.Look(ref kinds, "kinds", LookMode.Value);
            Scribe_Collections.Look(ref hours, "hours", LookMode.Value);
            Scribe_Values.Look(ref night, "night");
            Scribe_Values.Look(ref work, "work");
            Scribe_Values.Look(ref morning, "morning");
            Scribe_Values.Look(ref outdoors, "outdoors");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && (kinds == null || hours == null || kinds.Count != hours.Count))
            {
                kinds = new List<string>();
                hours = new List<float>();
            }
        }
    }

    /// <summary>
    /// Reading the practice logs (kept by the quirks, Quirks.cs): by work type's defName, or "work" (any work), "night"
    /// (the small hours), "morning" (early, up and busy) and "outdoors" (awake under the open sky).
    /// </summary>
    public static class Practice
    {
        public static readonly string[] Special = { "work", "night", "morning", "outdoors" };

        public static bool IsKnown(string what) => !what.NullOrEmpty() && (System.Array.IndexOf(Special, what) >= 0 || DefDatabase<WorkTypeDef>.GetNamedSilentFail(what) != null);

        /// <summary>Recent hours at it; 0 if nothing's known.</summary>
        public static float Hours(Pawn p, string what)
        {
            var log = CouncilGameComponent.Instance?.quirks?.PracticeOf(p, false);
            if (log == null || what.NullOrEmpty()) return 0f;
            switch (what)
            {
                case "work": return log.work;
                case "night": return log.night;
                case "morning": return log.morning;
                case "outdoors": return log.outdoors;
                default: return log.Of(what);
            }
        }

        /// <summary>The most recent hours at any of these (work type defNames, "work" or "night").</summary>
        public static float Hours(Pawn p, List<string> any)
        {
            if (any.NullOrEmpty()) return 0f;
            float best = 0f;
            foreach (var w in any)
            {
                float h = Hours(p, w);
                if (h > best) best = h;
            }
            return best;
        }

        /// <summary>"cleaning", "any work", "the small hours": what a practice key is about, for the player.</summary>
        public static string Words(string what)
        {
            if (System.Array.IndexOf(Special, what) >= 0) return ("CC_Practice_" + what.CapitalizeFirst()).Translate();
            var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(what);
            return wt != null ? wt.gerundLabel ?? wt.labelShort ?? wt.label : what;
        }
    }

    /// <summary>How a quirk came (Quirk.cause): said on its line.</summary>
    public static class QuirkCause
    {
        public const string Practice = "practice", Nature = "nature", Battle = "battle", Downed = "downed", Omen = "omen", Lived = "lived", Story = "story", Passed = "passed", Seen = "seen";
    }
}
