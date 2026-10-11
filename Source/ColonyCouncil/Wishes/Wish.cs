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
        Cancelled,
        Refused
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
        /// <summary>A sentence setting the scene: where and how the wish was voiced.</summary>
        public string scene;
        public Tone tone;
        public int createdTick;
        public int expiresTick;
        public WishState state = WishState.Open;
        /// <summary>The player promised to see to it: more patience now, a heavier fall if it doesn't happen.</summary>
        public bool promised;
        /// <summary>0 = voiced, 1 = reminded, 2 = last call (escalated).</summary>
        public int stage;
        /// <summary>The same colonist has made this wish before.</summary>
        public bool repeat;
        /// <summary>What set it off, if an event did (see <see cref="WishTrigger.key"/>).</summary>
        public string trigger;
        /// <summary>The colonist who inspired this one by getting the same thing ("me too").</summary>
        public Pawn envied;
        /// <summary>The colonist took matters into their own hands.</summary>
        public bool selfHelped;
        /// <summary>Someone who spoke up against this wish (an ascetic against luxury, a rival, the jealous).</summary>
        public Pawn objector;
        /// <summary>
        /// The thing it was about came from a mod that has since been removed: the wish is let go quietly on load,
        /// with no penalty (not saved).
        /// </summary>
        [Unsaved] public bool thingLost;

        public float Elapsed => expiresTick <= createdTick ? 1f : (Find.TickManager.TicksGame - createdTick) / (float)(expiresTick - createdTick);

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

        public string Label => Format(def.label, def.label).CapitalizeFirst();

        public string Format(string template) => Format(template, null);

        /// <summary>
        /// Fills in the placeholders. {WISH} gets the finished label (its own placeholders filled in), except
        /// while formatting the label itself.
        /// </summary>
        private string Format(string template, string wishLabel)
        {
            if (template.NullOrEmpty() || pawn == null) return template ?? "";
            if (wishLabel == null) wishLabel = template.Contains("{WISH") ? Label : def.label;
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
                wishLabel.Named("WISH"),
                (envied ?? other ?? forPawn).Named("ENVIED")).Resolve();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Defs.Look(ref def, "def");
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref beneficiary, "beneficiary");
            Scribe_References.Look(ref other, "other");
            Scribe_Collections.Look(ref coSigners, "coSigners", LookMode.Reference);
            // Silently, so a thing from a removed mod doesn't log an error; the wish is dropped after loading.
            CouncilScribe.LookDef(ref thingDef, "thingDef", ref thingLost);
            Scribe_Values.Look(ref subjectKey, "subjectKey", "");
            Scribe_Values.Look(ref subjectLabel, "subjectLabel");
            Scribe_Values.Look(ref targetValue, "targetValue");
            Scribe_Values.Look(ref baseline, "baseline");
            Scribe_Values.Look(ref reason, "reason");
            Scribe_Values.Look(ref quote, "quote");
            Scribe_Values.Look(ref scene, "scene");
            Scribe_Values.Look(ref tone, "tone");
            Scribe_Values.Look(ref createdTick, "createdTick");
            Scribe_Values.Look(ref expiresTick, "expiresTick");
            Scribe_Values.Look(ref state, "state");
            Scribe_Values.Look(ref promised, "promised");
            Scribe_Values.Look(ref stage, "stage");
            Scribe_Values.Look(ref repeat, "repeat");
            Scribe_Values.Look(ref trigger, "trigger");
            Scribe_References.Look(ref envied, "envied");
            Scribe_Values.Look(ref selfHelped, "selfHelped");
            Scribe_References.Look(ref objector, "objector");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (coSigners == null) coSigners = new List<Pawn>();
                coSigners.RemoveAll(p => p == null);
            }
        }
    }
}
