using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A social event (Social/SocialEvents.cs) or an encounter with a stranger (Strangers/Encounters.cs) written into
    /// the game's social log, between the two it was about. The log is what Interaction Bubbles reads, so the line also
    /// shows above their heads. The text is already written, so it is kept as it is instead of being generated from
    /// grammar rules.
    /// </summary>
    public class PlayLogEntry_CouncilEvent : PlayLogEntry_Interaction
    {
        private string text;

        public PlayLogEntry_CouncilEvent()
        {
        }

        public PlayLogEntry_CouncilEvent(InteractionDef def, Pawn a, Pawn b, string text) : base(def, a, b, new List<RulePackDef>())
        {
            this.text = text;
        }

        protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog) => text ?? "";

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref text, "text");
        }
    }

    /// <summary>The interaction a social event is logged as. It is never chosen as a conversation of its own.</summary>
    public class InteractionWorker_LogOnly : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient) => 0f;
    }

    public static class SocialLog
    {
        /// <summary>Puts a social event or encounter into the social log, when both are here on the same map.</summary>
        public static void Add(Pawn a, Pawn b, string text)
        {
            if (a == null || b == null || a == b || text.NullOrEmpty() || !a.Spawned || !b.Spawned || a.Map != b.Map) return;
            var def = DefDatabase<InteractionDef>.GetNamedSilentFail("Council_SocialEvent");
            if (def == null || Find.PlayLog == null) return;
            try
            {
                Find.PlayLog.Add(new PlayLogEntry_CouncilEvent(def, a, b, text));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("social log", e);
            }
        }
    }
}
