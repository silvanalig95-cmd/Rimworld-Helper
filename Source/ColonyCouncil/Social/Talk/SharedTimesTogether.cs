using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>The shared memories the newest content leaves (Defs/SharedMemoryDefs/Rimfolk_SharedMemories_Together.xml).</summary>
    public static class TogetherDefs
    {
        public static SharedMemoryDef Nursed => TalkDefs.Mem("Rimfolk_Mem_Nursed");
        public static SharedMemoryDef CloseCall => TalkDefs.Mem("Rimfolk_Mem_CloseCall");
        public static SharedMemoryDef Founding => TalkDefs.Mem("Rimfolk_Mem_Founding");
    }

    /// <summary>
    /// What the newest content leaves between the colony's people, from one line where the colony already says the thing
    /// happened (as <see cref="SharedTimes"/> does for the camps, missions and calendar days): an illness nursed through
    /// (HealthTracker.PulledThrough), a close call with one of the dangers on the map (MapDangers.Wound), a new colony
    /// founded together (Foundings.Founded). Nothing here ticks or is saved; the memories follow the social-life setting.
    /// </summary>
    public static class SharedTimesTogether
    {
        private static void Safe(string what, Action act)
        {
            try
            {
                act();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("shared times together: " + what, e);
            }
        }

        /// <summary>
        /// A doctor pulled a patient through a dangerous illness (Rimfolk's serious ones and the game's own, tended twice or
        /// more by them): the two remember it, the doctor first ([byme]).
        /// </summary>
        public static void Nursed(Pawn doctor, Pawn patient, string illness)
        {
            if (!Persona.On || doctor == null || patient == null || doctor == patient || illness.NullOrEmpty()) return;
            if (!doctor.RaceProps.Humanlike || !patient.RaceProps.Humanlike) return;
            Safe("nursed", () => TalkHooks.AddOnce(doctor, patient, TogetherDefs.Nursed, SharedTimes.The(illness), 1.3f));
        }

        /// <summary>
        /// One of the dangers on the map hurt someone (the harm service gave the wound): they and whoever of the colony is
        /// right there with them (in talking range, up and about) remember it, the one it hurt first ([byme]).
        /// </summary>
        public static void CloseCall(Pawn hurt, string key)
        {
            if (!Persona.On || hurt == null || !hurt.Spawned) return;
            string kind = TogetherFourMath.CloseCallKind(key);
            if (kind == null) return;
            Safe("close call", () =>
            {
                var other = Together.Company(hurt);
                if (other == null || !Talk.Eligible(other) || !Talk.Eligible(hurt)) return;
                TalkHooks.AddOnce(hurt, other, TogetherDefs.CloseCall, ("CC_Times_Close_" + kind).Translate().Resolve(), 1f);
            });
        }

        /// <summary>
        /// A new colony stands (Foundings.Founded): the founding party remember it together, each with the closest of the
        /// others (<see cref="TogetherFourMath.FoundingEach"/>), under the colony's name.
        /// </summary>
        public static void Founded(List<Pawn> founders, string place)
        {
            if (!Persona.On || founders == null || founders.Count < 2 || place.NullOrEmpty()) return;
            Safe("founded", () => SharedTimes.AmongThem(founders, TogetherDefs.Founding, place, 1.2f, TogetherFourMath.FoundingEach));
        }
    }
}
