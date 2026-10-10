using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    public enum WishState
    {
        Open,
        Granted,
        Expired,
        Dismissed,
        Cancelled
    }

    /// <summary>A possible wish found by a worker, before one is picked.</summary>
    public class WishCandidate
    {
        public float weight = 1f;
        public string reason;
        public Pawn other;
        public ThingDef thingDef;
        public string subjectKey = "";
        public string subjectLabel;
        public float targetValue;
        public float baseline;
    }

    /// <summary>A live wish. Saved with the game.</summary>
    public class Wish : IExposable
    {
        public int id;
        public WishDef def;
        /// <summary>The colonist who voiced the wish.</summary>
        public Pawn pawn;
        /// <summary>Who the wish is for. Usually the same as <see cref="pawn"/>.</summary>
        public Pawn beneficiary;
        /// <summary>Another colonist the wish is about (a partner, a dead friend).</summary>
        public Pawn other;
        public List<Pawn> coSigners = new List<Pawn>();
        public ThingDef thingDef;
        public string subjectKey = "";
        public string subjectLabel;
        public float targetValue;
        public float baseline;
        public string reason;
        public string quote;
        public Tone tone;
        public int createdTick;
        public int expiresTick;
        public WishState state = WishState.Open;

        public Pawn Beneficiary => beneficiary ?? pawn;
        public bool ForSomeoneElse => beneficiary != null && beneficiary != pawn;
        public WishWorker Worker => def.Worker;
        public int TicksLeft => expiresTick - Find.TickManager.TicksGame;

        public IEnumerable<Pawn> Supporters
        {
            get
            {
                if (pawn != null) yield return pawn;
                foreach (var p in coSigners)
                    if (p != null) yield return p;
            }
        }

        public bool Involves(Pawn p) => p != null && (p == pawn || p == Beneficiary || coSigners.Contains(p));

        public string Label => Format(def.label).CapitalizeFirst();

        public string Format(string template)
        {
            if (template.NullOrEmpty() || pawn == null) return template ?? "";
            var forPawn = Beneficiary ?? pawn;
            string thing = thingDef != null ? Avail.Indefinite(thingDef) : (subjectLabel ?? "");
            return template.Formatted(
                pawn.Named("PAWN"),
                forPawn.Named("FOR"),
                (other ?? forPawn).Named("OTHER"),
                (subjectLabel ?? thing).Named("SUBJECT"),
                thing.Named("THING"),
                (thingDef?.label ?? subjectLabel ?? "").Named("THINGBARE"),
                targetValue.ToString("0").Named("TARGET"),
                def.label.Named("WISH")).Resolve();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref beneficiary, "beneficiary");
            Scribe_References.Look(ref other, "other");
            Scribe_Collections.Look(ref coSigners, "coSigners", LookMode.Reference);
            Scribe_Defs.Look(ref thingDef, "thingDef");
            Scribe_Values.Look(ref subjectKey, "subjectKey", "");
            Scribe_Values.Look(ref subjectLabel, "subjectLabel");
            Scribe_Values.Look(ref targetValue, "targetValue");
            Scribe_Values.Look(ref baseline, "baseline");
            Scribe_Values.Look(ref reason, "reason");
            Scribe_Values.Look(ref quote, "quote");
            Scribe_Values.Look(ref tone, "tone");
            Scribe_Values.Look(ref createdTick, "createdTick");
            Scribe_Values.Look(ref expiresTick, "expiresTick");
            Scribe_Values.Look(ref state, "state");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (coSigners == null) coSigners = new List<Pawn>();
                coSigners.RemoveAll(p => p == null);
            }
        }
    }
}
