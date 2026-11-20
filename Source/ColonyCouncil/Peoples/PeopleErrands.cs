using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The peoples' errands the colony said yes to that take one of theirs to someone or something in a room of the colony: the
    /// guild's master to the piece they judge (round 7), the Line's operator to the radio (round 7), the Fleet's deck-runner to the
    /// casket (round 8), its officers along the ship (round 8), the Choir's listener and warden to the one they sit with (round 9).
    /// Visitors keep out of the colony's bedrooms (Visitors/KeepOut.cs); for these errands the bedroom where their work is lies open
    /// to them, as it does for a doctor's work. A cold store stays shut, and so does a bedroom where one of the colony is asleep (only
    /// those whose bed it is go in): there the errand waits, or one of the colony does it, and the words say so. The Republic's medal
    /// and the Helix Works' work on a colonist don't need this: the colonist walks out to them (PeopleEvents8.Meet).
    /// </summary>
    public static class PeopleErrands
    {
        /// <summary>Who of a visit may go into a bedroom for its errand, and the cell the errand takes them to (KeepOut.Care).</summary>
        public static IEnumerable<(Pawn who, IntVec3 at)> Ways(Visit v)
        {
            if (v == null || v.done || v.x == null) yield break;
            switch (v.kind)
            {
                case MastersMark.Kind:
                {
                    var at = MastersMark.At(v.place);
                    if (at.IsValid) yield return (v.x, at.HasThing ? at.Thing.PositionHeld : at.Cell);
                    break;
                }
                case VoiceForColony.Kind:
                    // Until the call is made (VoiceForColony: due below 0).
                    if (v.due < 0 && v.place != null && v.place.Spawned) yield return (v.x, v.place.Position);
                    break;
                case SleeperOfTheirs.Kind:
                    // Handed over: the runner opens it (kept, one of the colony does).
                    if (v.stake == 0 && v.place != null && v.place.Spawned) yield return (v.x, v.place.Position);
                    break;
                case GravshipLog.Kind:
                    if (v.place is Building engine && engine.Spawned)
                    {
                        var parts = GravshipLog.Parts(engine);
                        foreach (var o in v.group.Prepend(v.x).Where(o => o != null))
                            foreach (var t in parts)
                                yield return (o, t.Position);
                    }
                    break;
                case MindShouts.Kind:
                case VoidListens.Kind:
                    if (v.a != null && v.a.Spawned) yield return (v.x, v.a.Position);
                    break;
            }
        }
    }
}
