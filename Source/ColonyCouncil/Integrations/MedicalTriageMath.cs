namespace ColonyCouncil
{
    /// <summary>
    /// The small rules of medical triage with Move the Patient (Integrations/MedicalTriage.cs): which medical bed is the
    /// best, when one patient outranks another for a bed, who may be moved out of one, and how often. Pure (no game
    /// types): Tools/Tests checks them.
    /// <para>
    /// The best bed is the one the player ranked highest with the mod's priority (Low, Normal, High, Critical), then the
    /// one the game treats best in (the bed's tend quality offset and immunity gain, the room's infection chance), then
    /// the nearest. Severity is the mod's own (PatientTriage.CalculatePatientSeverity), by the same four numbers.
    /// </para>
    /// </summary>
    public static class MedicalTriageMath
    {
        /// <summary>Move the Patient's MedicalPriority, for a bed's priority and a patient's severity alike.</summary>
        public const int Low = 1, Normal = 2, High = 3, Critical = 4;

        /// <summary>Two beds this close in quality are equally good: the nearer one wins.</summary>
        public const float SameQuality = 0.005f;

        /// <summary>Someone moved by triage isn't moved again for this long (four hours), so nobody is carried back and forth.</summary>
        public const int CooldownTicks = 4 * 2500;

        /// <summary>A move not done in this long (four hours) is given up: the bed is no longer kept.</summary>
        public const int GiveUpTicks = 4 * 2500;

        /// <summary>How long a patient's inspect line says why they were moved (six hours).</summary>
        public const int LineTicks = 6 * 2500;

        /// <summary>After a patient goes to a lesser bed, the beds are looked at every 250 ticks for this long (an hour).</summary>
        public const int SoonTicks = 2500;

        /// <summary>
        /// How well the game treats someone in a bed: the bed's tend quality offset, what it adds to immunity gain (a
        /// factor of 1.11 adds 0.11), and what the room takes off the infection chance (a factor of 0.7 takes 0.3; a dirty
        /// room above 1 counts against it).
        /// </summary>
        public static float Quality(float tendOffset, float immunityFactor, float roomInfectionFactor) =>
            tendOffset + (immunityFactor - 1f) + (1f - roomInfectionFactor);

        /// <summary>A medical bed as triage ranks it.</summary>
        public struct Rank
        {
            /// <summary>The mod's priority (Normal when its priority system is off).</summary>
            public int priority;
            /// <summary><see cref="Quality"/>.</summary>
            public float quality;
            /// <summary>Squared distance from the patient, for the last tie.</summary>
            public float distance;

            public Rank(int priority, float quality, float distance)
            {
                this.priority = priority;
                this.quality = quality;
                this.distance = distance;
            }
        }

        /// <summary>A is a better bed than B, distance aside: a higher priority, or the same and better quality.</summary>
        public static bool Better(Rank a, Rank b) => a.priority != b.priority ? a.priority > b.priority : a.quality > b.quality + SameQuality;

        /// <summary>Neither is better than the other: equally good beds.</summary>
        public static bool Same(Rank a, Rank b) => !Better(a, b) && !Better(b, a);

        /// <summary>Best first: priority, then quality, then the nearer.</summary>
        public static int Compare(Rank a, Rank b)
        {
            if (Better(a, b)) return -1;
            if (Better(b, a)) return 1;
            return a.distance.CompareTo(b.distance);
        }

        /// <summary>
        /// A surgery bed counts for a patient when they are waiting for an operation themselves, or while nobody waiting for
        /// one could use it.
        /// </summary>
        public static bool SurgeryBedOpen(bool forOperation, bool operationWaiting) => forOperation || !operationWaiting;

        /// <summary>
        /// Whether a patient should have a bed over the one lying in it. In a surgery bed an operation comes first: one
        /// waiting for theirs is never moved out of it for anyone, and one who is waiting for theirs outranks anyone who
        /// isn't. Otherwise, and in any other bed, only the more serious case outranks: Critical over High over Normal over
        /// Low, never the same.
        /// </summary>
        public static bool Outranks(int worse, bool worseOperation, int lighter, bool lighterOperation, bool surgeryBed)
        {
            if (surgeryBed)
            {
                if (lighterOperation) return false;
                if (worseOperation) return true;
            }
            return worse > lighter;
        }

        /// <summary>Why someone may not be moved out of their bed.</summary>
        public enum Hold
        {
            None,
            /// <summary>Someone is operating on them.</summary>
            Operating,
            /// <summary>Someone is tending them, or on the way with medicine.</summary>
            BeingTended,
            MentalState,
            InLabor,
            /// <summary>Already being carried, or the mod's transfer is under way.</summary>
            Carried,
            /// <summary>The only free bed is of the other kind: a prisoner's for a colonist, a colonist's for a prisoner, a slave's.</summary>
            OtherKind,
            /// <summary>No other medical bed is free for them.</summary>
            NoOtherBed,
            /// <summary>Moved by triage within the last four hours.</summary>
            Recently
        }

        /// <summary>The first reason that holds someone where they are, or None.</summary>
        public static Hold MayMove(bool operating, bool tended, bool mental, bool labor, bool carried, bool otherBedFree, bool sameKind, int lastMoved, int now)
        {
            if (operating) return Hold.Operating;
            if (tended) return Hold.BeingTended;
            if (mental) return Hold.MentalState;
            if (labor) return Hold.InLabor;
            if (carried) return Hold.Carried;
            if (!otherBedFree) return Hold.NoOtherBed;
            if (!sameKind) return Hold.OtherKind;
            if (Recently(lastMoved, now)) return Hold.Recently;
            return Hold.None;
        }

        /// <summary>Moved by triage within the cooldown. A time after now (another game's clock) doesn't count.</summary>
        public static bool Recently(int lastMoved, int now) => lastMoved >= 0 && now >= lastMoved && now - lastMoved < CooldownTicks;

        /// <summary>A bed of the patient's own kind: a prisoner's bed for a prisoner, a slave's for a slave, neither for anyone else.</summary>
        public static bool SameKind(bool prisoner, bool slave, bool bedForPrisoners, bool bedForSlaves) => prisoner == bedForPrisoners && slave == bedForSlaves;

        /// <summary>A move started at <paramref name="started"/> is given up by now (or the clock went back).</summary>
        public static bool GiveUp(int started, int now) => now < started || now - started >= GiveUpTicks;
    }
}
