namespace ColonyCouncil
{
    /// <summary>
    /// Why someone of another people comes to the colony: each kind of arrival Rimfolk makes that could be a guest, invited
    /// or not (see <see cref="InvitedGuestsMath"/>).
    /// </summary>
    public enum ArrivalWhy : byte
    {
        // ---- Invited by the colony: Hospitality's guests while it's loaded ----

        /// <summary>A notable (or one of their town who can travel) who took up a letter's invitation: a pen friend, a gathering day, any letter that invites.</summary>
        LetterInvitation,
        /// <summary>A town's folk invited to the colony's feast or fair (a dealing: a town invited).</summary>
        TownInvited,
        /// <summary>A town's folk come to a fair the colony proclaimed (a dealing: a proclamation).</summary>
        TownFair,
        /// <summary>Someone of standing who wrote ahead for the colony's guest room and was told yes.</summary>
        NotableGuest,
        /// <summary>A feast's or a wedding's guests with their retinue.</summary>
        FeastGuests,
        /// <summary>An envoy and their attendant for a season.</summary>
        Envoy,

        // ---- Not invited, or not a stay: Rimfolk's own ----

        /// <summary>A mission's guests (a dignitary, refugees, an apprentice, a choir): their people asked, and the colony's own table and beds are what the mission counts.</summary>
        HostingMission,
        /// <summary>An errand at the gate with its own ending: a ward's party, a match's escort, a parley, an oath, a ransom's messenger, couriers and riders.</summary>
        Errand,
        /// <summary>A stranger an event brings to the gate or past it: nobody asked them.</summary>
        Story,
        /// <summary>Someone asked to live here (kin invited to stay, a visitor asked to join): they join the colony, it isn't a visit.</summary>
        Joining
    }

    /// <summary>
    /// Invited visitors as Hospitality's guests (the player's request, 10 November 2026: "visitors that you invite with
    /// diplomatic actions should also be treated as guests with hospitality"): which arrivals are the colony's invited
    /// guests, who ends their stay, and how long Hospitality's own stay is set for. A guest Rimfolk follows (a feast's, an
    /// envoy) stays as long as Rimfolk's visit with a day to spare, so Hospitality's clock never sends them first; the rest
    /// stay the hours Rimfolk planned and leave by Hospitality's own rule (in daylight). Free of game types, so Tools/Tests
    /// can check it.
    /// </summary>
    public static class InvitedGuestsMath
    {
        /// <summary>A day beyond Rimfolk's own end, for a stay Rimfolk ends itself: Hospitality's end is only the fallback.</summary>
        public const float SpareHours = 24f;

        /// <summary>The shortest stay Hospitality is given (a guest who only came for an hour still settles in).</summary>
        public const float MinHours = 4f;

        /// <summary>The longest (a season and a day): nobody stays as Hospitality's guest past it, whatever is asked.</summary>
        public const float MaxHours = 16f * 24f;

        /// <summary>The colony invited them: their stay is Hospitality's, with Rimfolk's purpose on top.</summary>
        public static bool Invited(ArrivalWhy a)
        {
            switch (a)
            {
                case ArrivalWhy.LetterInvitation:
                case ArrivalWhy.TownInvited:
                case ArrivalWhy.TownFair:
                case ArrivalWhy.NotableGuest:
                case ArrivalWhy.FeastGuests:
                case ArrivalWhy.Envoy:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Rimfolk ends the stay itself, at its own time (the feast's first light, the envoy's season), and keeps its own
        /// count of who is hurt or lost; the others leave when Hospitality's stay runs out.
        /// </summary>
        public static bool RimfolkEnds(ArrivalWhy a) => a == ArrivalWhy.FeastGuests || a == ArrivalWhy.Envoy;

        /// <summary>Rimfolk steers where they keep through the day (the gate, a contest, the feast's table, indoors for the night).</summary>
        public static bool RimfolkSteers(ArrivalWhy a) => a == ArrivalWhy.FeastGuests;

        /// <summary>
        /// The stay Hospitality is given, in ticks: Rimfolk's planned hours (at least <see cref="MinHours"/>), with
        /// <see cref="SpareHours"/> more when Rimfolk ends it itself; never over <see cref="MaxHours"/>. An unknown length
        /// is the shortest.
        /// </summary>
        public static int StayTicks(ArrivalWhy a, float plannedHours, int ticksPerHour = 2500)
        {
            float h = float.IsNaN(plannedHours) || float.IsInfinity(plannedHours) ? MinHours : plannedHours;
            if (h < MinHours) h = MinHours;
            if (RimfolkEnds(a)) h += SpareHours;
            if (h > MaxHours) h = MaxHours;
            return (int)(h * ticksPerHour + 0.5f);
        }

        /// <summary>Hospitality's own displeasure at losing the whole group: only where Rimfolk keeps no count of its own.</summary>
        public static bool UpsetWhenLost(ArrivalWhy a) => !RimfolkEnds(a);

        /// <summary>
        /// Handed to Hospitality now: invited, Hospitality loaded and its way of making guests found, the setting (and the
        /// deeper game hooks) on, their people at peace with the colony, and everyone in the group a person who has come.
        /// </summary>
        public static bool HandOver(ArrivalWhy a, bool hospitality, bool on, bool hostile, int people, int others)
        {
            if (!Invited(a) || !hospitality || !on || hostile) return false;
            return people > 0 && others == 0;
        }

        /// <summary>Who decides where the guests keep, at a look.</summary>
        public enum Lead
        {
            /// <summary>Hospitality's own: its guest area, beds and shops.</summary>
            Hospitality,
            /// <summary>Indoors, out of danger (a raid on the map).</summary>
            Indoors,
            /// <summary>Where Rimfolk's visit wants them (the feast's gate, contest, table, room for the night).</summary>
            Focus
        }

        /// <summary>
        /// Where the guests keep now: indoors in danger when the visit has an indoors place, else at Rimfolk's focus when it
        /// has one, else as Hospitality's guests (also while they walk in or out: only a settled stay is steered).
        /// </summary>
        public static Lead Who(bool staying, bool danger, bool canIndoors, bool hasFocus)
        {
            if (!staying) return Lead.Hospitality;
            if (danger && canIndoors) return Lead.Indoors;
            return hasFocus ? Lead.Focus : Lead.Hospitality;
        }

        /// <summary>Why an arrival stays Rimfolk's own (a word for the Almanac and the notes), or null for an invited one.</summary>
        public static string WhyNot(ArrivalWhy a)
        {
            switch (a)
            {
                case ArrivalWhy.HostingMission: return "asked";
                case ArrivalWhy.Errand: return "errand";
                case ArrivalWhy.Story: return "uninvited";
                case ArrivalWhy.Joining: return "joins";
                default: return null;
            }
        }
    }
}
