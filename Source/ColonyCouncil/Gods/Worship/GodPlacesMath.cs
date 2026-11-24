using System;

namespace ColonyCouncil
{
    /// <summary>Whether a building can be a god's place (Gods/Worship/GodPlaces.cs, the player's request of 24 November 2026).</summary>
    public enum PlaceSuit
    {
        Yes,
        /// <summary>A sculpture below good quality.</summary>
        ArtTooPoor,
        /// <summary>A grave or a sarcophagus, for a god that isn't a god of the dead.</summary>
        GraveNotDead,
        /// <summary>Not a place of worship at all (a shield generator with "shrine" in its name).</summary>
        No
    }

    /// <summary>What a rite at a god's place is (GodRites.Held).</summary>
    public enum RiteKind
    {
        /// <summary>The place given to its god: the words said with an offering, the believers gathered.</summary>
        Dedication,
        /// <summary>The week's service at the god's temple.</summary>
        Service,
        /// <summary>The god's festival's rite.</summary>
        Festival,
        /// <summary>A rite of the game's own (a wedding, a funeral, a naming) held at the god's place.</summary>
        GameRite
    }

    /// <summary>How a rite went.</summary>
    public enum RiteOutcome
    {
        Poor,
        Plain,
        Good,
        Fine
    }

    /// <summary>
    /// The rules for the gods' places and the rites held at them (the player, 24 November 2026: setting something as a god's
    /// altar should need conditions, "a system and logic a bit behind it", not an altar for each god "for free within a
    /// minute"; and the rites "should not just be for flavor only. They should then always have some game impact ...
    /// depending on the god and how it turns out, and the pawns"). Free of game types; docs/gods-altars-proposals.md.
    /// <list type="bullet">
    /// <item>Who may have a place: a god someone of the colony holds (leaning toward it or more), or one someone has lately
    /// turned toward (<see cref="Seeker"/>).</item>
    /// <item>How many: one, a second once three hold it, a third once six do, never more than hold it (<see cref="PlacesAllowed"/>).</item>
    /// <item>What suits (<see cref="Suits"/>), and a place the believers say the god likes (<see cref="FitFactor"/>).</item>
    /// <item>Upkeep: untended for a season, neglected (<see cref="Neglected"/>); taken from its god, felt the more the longer
    /// it was kept (<see cref="UndedicateFavour"/>).</item>
    /// <item>A rite's outcome (<see cref="RiteScore"/>, <see cref="Outcome"/>) and what it brings: a mood, belief, a short
    /// blessing of the god's own kind, the god's regard, and now and then an answer (<see cref="AnswerChance"/>).</item>
    /// </list>
    /// </summary>
    public static class GodPlacesMath
    {
        // ================= Who, and how many =================

        /// <summary>Someone who has turned toward a god lately (a moment, a dream, a vow) counts as seeking it for this many days.</summary>
        public const int SeekerDays = 10;

        /// <summary>
        /// Seeking a god: some belief in it, not yet enough to lean toward it (<see cref="PawnGodsMath.LeansFrom"/>), grown in
        /// the last <see cref="SeekerDays"/> days.
        /// </summary>
        public static bool Seeker(float hold, float daysSinceGrew) =>
            hold > 0f && hold < PawnGodsMath.LeansFrom && daysSinceGrew >= 0f && daysSinceGrew <= SeekerDays;

        /// <summary>A god has at most this many places at one home.</summary>
        public const int MostPlaces = 3;

        /// <summary>
        /// How many places a god may have at a home: one where someone holds it or seeks it, a second once three hold it, a
        /// third once six do; never more than hold it (a seeker's one aside).
        /// </summary>
        public static int PlacesAllowed(int holders, int seekers)
        {
            if (holders <= 0) return seekers > 0 ? 1 : 0;
            return Math.Max(1, Math.Min(Math.Min(holders, MostPlaces), 1 + holders / 3));
        }

        /// <summary>The believers needed for one more place than <paramref name="places"/> (0 once there are as many as there can be).</summary>
        public static int HoldersFor(int places)
        {
            if (places >= MostPlaces) return 0;
            return places <= 0 ? 1 : 3 * places;
        }

        // ================= What suits =================

        /// <summary>The lowest quality a work of art must be to be a god's place: good (the game's QualityCategory.Good).</summary>
        public const int ArtQualityLeast = 3;

        /// <summary>
        /// Whether a building can be a god's place: never a thing that only has the word in its name (a shield); a grave or a
        /// sarcophagus only for a god of the dead; a work of art only of good quality or better (none, -1, is not art); else yes.
        /// </summary>
        public static PlaceSuit Suits(bool shield, bool grave, bool deadGod, bool art, int quality)
        {
            if (shield) return PlaceSuit.No;
            if (grave) return deadGod ? PlaceSuit.Yes : PlaceSuit.GraveNotDead;
            if (art && quality >= 0 && quality < ArtQualityLeast) return PlaceSuit.ArtTooPoor;
            return PlaceSuit.Yes;
        }

        /// <summary>
        /// A place the god's believers say it likes (by water for the waters, by the fields for the harvest ...) is liked
        /// more: prayers there answered a little more often, belief growing a little faster; one that isn't works, a little
        /// less; a god with no such liking known, as it is.
        /// </summary>
        public static float FitFactor(bool? fits) => fits == true ? 1.2f : fits == false ? 0.9f : 1f;

        // ================= Upkeep =================

        /// <summary>A place nobody has prayed at or left anything at for this many days (a season) is neglected.</summary>
        public const int NeglectDays = 15;

        public static bool Neglected(float daysSinceTended) => daysSinceTended > NeglectDays;

        /// <summary>A place taken from its god, or torn down by the colony: felt the more the longer it was its god's (from 1 to 7).</summary>
        public static float UndedicateFavour(float daysKept) => -(1f + Math.Min(6f, Math.Max(0f, daysKept) / 10f));

        // ================= The dedication =================

        /// <summary>The dedication's words and the believers' gathering: about two hours at the place.</summary>
        public const int DedicationTicks = 5000;

        /// <summary>A place asked for waits this many days for its offering and someone to say the words, then is let go.</summary>
        public const int DedicateWaitDays = 4;

        /// <summary>A god of the night or the dead has its rites after dark (from eight in the evening to four in the morning).</summary>
        public static bool RiteHour(bool night, int hour) => !night || hour >= 20 || hour < 4;

        // ================= A rite's outcome =================

        /// <summary>
        /// How well a rite went, about 0 to 1.2: a base, more for the share of the god's believers at home who came and for
        /// numbers, a priest leading it or a good speaker, a temple or a fine room (0 to 1), a place the believers say the god
        /// likes, an offering lying there, a keepsake of the god's kept there; less at a neglected place; and the roll (0 to 1)
        /// for how the day goes.
        /// </summary>
        public static float RiteScore(int attending, int holders, bool priest, int social, bool temple, float roomFine, float fit, bool offering, bool keepsake,
            bool neglected, float roll)
        {
            float s = 0.3f;
            if (holders > 0) s += 0.2f * Math.Min(1f, attending / (float)holders);
            s += 0.03f * Math.Min(5, Math.Max(0, attending - 1));
            if (priest) s += 0.1f;
            s += 0.1f * Math.Min(1f, Math.Max(0, social) / 15f);
            s += temple ? 0.12f : 0f;
            s += 0.08f * Math.Max(0f, Math.Min(1f, roomFine));
            s += (fit - 1f) * 0.5f;
            if (offering) s += 0.08f;
            if (keepsake) s += 0.08f;
            if (neglected) s -= 0.2f;
            s += (Math.Max(0f, Math.Min(1f, roll)) - 0.5f) * 0.4f;
            return s;
        }

        public static RiteOutcome Outcome(float score) =>
            score < 0.35f ? RiteOutcome.Poor : score < 0.62f ? RiteOutcome.Plain : score < 0.85f ? RiteOutcome.Good : RiteOutcome.Fine;

        /// <summary>The chance the god answers a rite (its own act, through the gods' touch, with its own waits): none for a poor one.</summary>
        public static float AnswerChance(RiteOutcome o, RiteKind k, float fit)
        {
            float c = o switch { RiteOutcome.Plain => 0.06f, RiteOutcome.Good => 0.15f, RiteOutcome.Fine => 0.3f, _ => 0f };
            if (k == RiteKind.Dedication || k == RiteKind.Festival) c *= 1.3f;
            return Math.Min(0.5f, c * Math.Max(0.5f, fit));
        }

        /// <summary>How strongly what follows an answered rite leans (as an offering's worth does): from 0.8 to 2.</summary>
        public static float AnswerLean(RiteOutcome o, float fit) => Math.Max(0.5f, Math.Min(2.4f, (o == RiteOutcome.Fine ? 2f : o == RiteOutcome.Good ? 1.4f : 0.8f) * Math.Max(0.5f, fit)));

        /// <summary>The god's regard for a rite kept for it (never shown): less for a poor one, more for a fine one and a festival or a dedication.</summary>
        public static float RiteFavour(RiteOutcome o, RiteKind k)
        {
            float f = o switch { RiteOutcome.Poor => 0.5f, RiteOutcome.Plain => 1.5f, RiteOutcome.Good => 2.5f, _ => 3.5f };
            return k == RiteKind.Service || k == RiteKind.GameRite ? f * 0.7f : f;
        }

        /// <summary>How much those who came grow in their belief in the god (times the festival's step).</summary>
        public static float BeliefScale(RiteOutcome o, float fit) => (o switch { RiteOutcome.Poor => 0.3f, RiteOutcome.Plain => 0.8f, RiteOutcome.Good => 1.1f, _ => 1.5f }) * fit;

        /// <summary>The strength of the short blessing of the god's own kind on those who came and hold it: none after a poor rite, 1 to 3.</summary>
        public static int BlessLevel(RiteOutcome o) => o switch { RiteOutcome.Plain => 1, RiteOutcome.Good => 2, RiteOutcome.Fine => 3, _ => 0 };

        /// <summary>The mood's stage for those who came (Rimfolk_Rite_Kept): poor, plain, good, fine.</summary>
        public static int MoodStage(RiteOutcome o) => (int)o;

        // ================= The place's room =================

        /// <summary>
        /// How fine a god's place's surroundings are, 0 to 1: indoors by the room's impressiveness (from 20 to 100), outdoors
        /// by what stands round it (each light, plant in a pot, work of art or bench a little); and what its believers say it
        /// likes there (<paramref name="liked"/>) a little more.
        /// </summary>
        public static float RoomFine(bool indoors, float impressiveness, int around, int liked)
        {
            float f = indoors ? (impressiveness - 20f) / 80f : Math.Min(0.6f, 0.1f * Math.Max(0, around));
            f += 0.1f * Math.Min(3, Math.Max(0, liked));
            return Math.Max(0f, Math.Min(1f, f));
        }

        /// <summary>A place this fine is a fine place to pray at (a better mood).</summary>
        public const float FineFrom = 0.5f;

        // ================= The week's service =================

        /// <summary>A temple holds its service once in this many days, in the early evening.</summary>
        public const int ServiceDays = 7;

        public static bool ServiceHour(int hour) => hour >= 17 && hour < 20;

        /// <summary>The service: about an hour and a half.</summary>
        public const int ServiceTicks = 3750;

        // ================= Pilgrims =================

        /// <summary>Pilgrims come to a temple once in this many days at most, by this chance a day once they may.</summary>
        public const int PilgrimDays = 20;

        public const float PilgrimChance = 0.08f;

        /// <summary>What pilgrims leave at the temple, in silver's worth, by how many live at the home.</summary>
        public static int PilgrimGift(int colonists) => Math.Min(150, 30 + 6 * Math.Max(1, colonists));

        /// <summary>The goodwill of their people for a temple they could pray at.</summary>
        public const int PilgrimGoodwill = 3;

        // ================= Oaths at the place =================

        /// <summary>An oath sworn at the god's place weighs this much more, kept or broken.</summary>
        public const float OathWeight = 1.5f;

        /// <summary>The god's regard for an oath sworn at its place and kept (beside the vow's own).</summary>
        public const float OathKeptFavour = 2f;

        // ================= A believer asks for a place =================

        /// <summary>A place agreed by the council is waited for this many days; none given by then, the one who asked is let down.</summary>
        public const int AskedWaitDays = 15;

        // ================= Keepsakes =================

        /// <summary>A keepsake of a god's moment lies at its place within this distance (cells).</summary>
        public const float KeepsakeNear = 2.9f;

        /// <summary>A god's moment that leaves something leaves a keepsake with it by this chance, once in this many days at a home at most.</summary>
        public const float KeepsakeChance = 0.5f;

        public const int KeepsakeGapDays = 20;
    }
}
