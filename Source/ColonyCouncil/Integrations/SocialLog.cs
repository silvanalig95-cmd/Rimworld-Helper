using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A social event (Social/SocialEvents.cs) or an encounter with a stranger (Strangers/Encounters.cs) written into
    /// the game's social log, between the two it was about, and shown over the first of them as the speech setting
    /// says (Immersion/Speech.cs: a bubble with Interaction Bubbles, floating text, or nothing). The text is already
    /// written, so it is kept as it is instead of being generated from grammar rules.
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
        /// <summary>How far apart two people can be and still be talking to each other (a little more than the game's own chats).</summary>
        public const float TalkRange = 10f;

        /// <summary>
        /// Two people close enough to be talking: on the same map, within <see cref="TalkRange"/>, and in the same room
        /// or within sight of each other (not through a wall).
        /// </summary>
        public static bool Near(Pawn a, Pawn b)
        {
            if (a == null || b == null || a == b || !a.Spawned || !b.Spawned || a.Map != b.Map) return false;
            if (!a.Position.InHorDistOf(b.Position, TalkRange)) return false;
            try
            {
                var room = a.GetRoom();
                if (room != null && room == b.GetRoom()) return true;
                return GenSight.LineOfSight(a.Position, b.Position, a.Map, true);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Puts a social event or encounter into the social log, when the two are near each other: two people at
        /// opposite ends of the map are left out (the event still happened; it is in the news, the journals and the
        /// chronicle). How it shows over their heads (a bubble, floating text or nothing) is the speech setting's
        /// (Immersion/Speech.cs); <paramref name="show"/> false keeps it in the log only, for a moment a line was
        /// already said for.
        /// </summary>
        public static void Add(Pawn a, Pawn b, string text, bool show = true)
        {
            if (text.NullOrEmpty() || !Near(a, b)) return;
            Speech.Logged(a, b, text, show);
        }
    }
}
