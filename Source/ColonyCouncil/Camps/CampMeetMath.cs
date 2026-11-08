namespace ColonyCouncil
{
    /// <summary>When the colony meets a camp at the gate (Camps/CampMeet.cs). Pure: Tools/Tests checks it.</summary>
    public static class CampMeetMath
    {
        /// <summary>A camp's fire further than this from the colony's gate meets the colony at the gate.</summary>
        public const float Far = 35f;

        /// <summary>A camper within this of the gate is there to take it.</summary>
        public const float AtGate = 9f;

        /// <summary>How long a camper sent to the gate waits there (ticks): two hours.</summary>
        public const int WaitTicks = 5000;

        public static bool MeetAtGate(float fireToGate) => fireToGate > Far;
    }
}
