using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The pure rules of the content round's third part, its camps and world events (docs/content-round-3-camps-world.md,
    /// R3-C1 to R3-W6): the rain-callers' fee and their rain, the dyers' colours by age, whom the witch-finders look for and
    /// how a questioning ends, the rag-and-bone men's price, the gamblers' evening, an expedition's return, a ransom, the rats, and the craft a new way is taught in. No game types here:
    /// Tools/Tests checks them. The parts are Camps/CampRound3.cs and World/Variety/WorldRound3.cs.
    /// </summary>
    public static class Round3WorldMath
    {
        private static int Tens(float x) => (int)Math.Round(x / 10f) * 10;

        // ================= R3-C1. Rain-callers =================

        /// <summary>The game's weather counts as rain at this rate of rain or more (a drizzle is 0.3, a downpour 1).</summary>
        public const float RainMark = 0.25f;

        /// <summary>It rains on the colony's land, after their first rite (rain before it isn't theirs to claim).</summary>
        public static bool Rained(float rainRate, bool riteHeld) => riteHeld && rainRate >= RainMark;

        /// <summary>Their rite is kept each evening, from seven to ten.</summary>
        public static bool RiteHour(int hour) => hour >= 19 && hour <= 21;

        /// <summary>The fee they ask if the rain comes: by the colony's wealth, in tens, 40 to 300.</summary>
        public static int RainFee(float wealthFactor) => Math.Max(40, Math.Min(300, Tens(80f * Math.Max(0.1f, wealthFactor))));

        /// <summary>The callers whose rite sends something up into the sky (a rocket, a mast's charge) rather than dancing round the fire.</summary>
        public static bool SendsUp(string ageTag) => ageTag == "industrial" || ageTag == "spacer";

        /// <summary>After the rain, they pack up and go this many hours later (paid or not).</summary>
        public const int GoAfterRainHours = 6;

        // ================= R3-C2. Dyers =================

        /// <summary>The colours a people of this age dyes in, by key (CC_R3Dye_Colour_KEY): woad, madder and weld before the mills, the mills' aniline colours after.</summary>
        public static string[] Palette(string ageTag)
        {
            switch (ageTag)
            {
                case "tribal": return new[] { "woad", "madder", "ochre" };
                case "medieval": return new[] { "woad", "madder", "weld" };
                case "industrial": return new[] { "aniline", "verdigris", "madder" };
                case "spacer": return new[] { "cobalt", "verdigris", "aniline" };
                default: return new[] { "woad" };
            }
        }

        /// <summary>A colour by its key, as red, green and blue (0 to 1).</summary>
        public static float[] Rgb(string key)
        {
            switch (key)
            {
                case "woad": return new[] { 0.24f, 0.33f, 0.55f };
                case "madder": return new[] { 0.62f, 0.2f, 0.18f };
                case "weld": return new[] { 0.78f, 0.68f, 0.26f };
                case "ochre": return new[] { 0.68f, 0.46f, 0.22f };
                case "aniline": return new[] { 0.45f, 0.2f, 0.52f };
                case "verdigris": return new[] { 0.22f, 0.52f, 0.45f };
                case "cobalt": return new[] { 0.12f, 0.36f, 0.78f };
                default: return new[] { 0.5f, 0.5f, 0.5f };
            }
        }

        /// <summary>The camp's colour: one of their age's, by a roll (0 to 1).</summary>
        public static string ColourOf(string ageTag, float roll)
        {
            var p = Palette(ageTag);
            return p[Math.Max(0, Math.Min(p.Length - 1, (int)(roll * p.Length)))];
        }

        /// <summary>The one sent over sits with the dyers this long (hours) before their clothes come out of the vat.</summary>
        public const float DyeHours = 1f;

        // ================= R3-C3. Witch-finders =================

        /// <summary>Whom they look for, strongest first: one with a psychic link, one of a people not baseline, one psychically sensitive, the healer in an age of herbs.</summary>
        public enum Mark
        {
            None,
            Healer,
            Sensitive,
            Xenotype,
            Psylink
        }

        /// <summary>What marks one of the colony in the finders' eyes (the strongest that holds).</summary>
        public static Mark MarkOf(bool psylink, bool xenotype, bool sensitive, bool healer, bool herbAge) =>
            psylink ? Mark.Psylink : xenotype ? Mark.Xenotype : sensitive ? Mark.Sensitive : healer && herbAge ? Mark.Healer : Mark.None;

        /// <summary>Psychically sensitive enough to be marked: the game's psychic sensitivity at this or more.</summary>
        public const float SensitiveFrom = 1.25f;

        /// <summary>The healer is looked at only if this good at medicine.</summary>
        public const int HealerFrom = 6;

        /// <summary>Questioned an hour at their fire, cleared at this chance: by the suspect's way with words (Social), 45% to 90%.</summary>
        public static float ClearedChance(int social) => Math.Max(0.45f, Math.Min(0.9f, 0.45f + 0.035f * Math.Max(0, social)));

        /// <summary>How long (hours) the questioning takes at their fire before they say what they make of it.</summary>
        public const float QuestionHours = 1f;

        /// <summary>Nobody fits what they look for: they look the colony over this long (days) and go.</summary>
        public const float NobodyDays = 1.5f;

        /// <summary>Their people's goodwill when the suspect is cleared, and when they name a witch.</summary>
        public const int ClearedGoodwill = 2, NamedGoodwill = -3;

        // ================= R3-C4. Rag-and-bone men =================

        /// <summary>At most this many worn-out things go out to them at once.</summary>
        public const int MaxRags = 15;

        /// <summary>What they pay for the worn-out things: four silver a piece, in tens, ten at least.</summary>
        public static int RagPrice(int pieces) => pieces <= 0 ? 0 : Math.Max(10, Tens(pieces * 4f));

        // ================= R3-C5. Gamblers =================

        public enum Play
        {
            Won,
            Lost,
            Caught
        }

        /// <summary>A sharp eye (Intellectual this or more) may catch their loaded dice.</summary>
        public const int SharpEye = 8;

        /// <summary>A sharp eye catches the loaded dice at this chance.</summary>
        public const float CatchChance = 0.35f;

        /// <summary>The colony's player wins at this chance: by their way with people (Social), 30% to 60%, against loaded dice.</summary>
        public static float WinChance(int social) => Math.Max(0.3f, Math.Min(0.6f, 0.3f + 0.02f * Math.Max(0, social)));

        /// <summary>An evening's play: caught at their dice, else won or lost.</summary>
        public static Play Gamble(int social, int intellectual, float catchRoll, float winRoll) =>
            intellectual >= SharpEye && catchRoll < CatchChance ? Play.Caught : winRoll < WinChance(social) ? Play.Won : Play.Lost;

        /// <summary>Won: twice the stake comes back to the gate. Caught: the stake comes back.</summary>
        public static int Back(Play play, int stake) => play == Play.Won ? stake * 2 : play == Play.Caught ? stake : 0;

        /// <summary>Caught at their dice, their camp's unease jumps by this much.</summary>
        public const float CaughtTension = 25f;

        /// <summary>The play takes this long (hours) at their fire before it is settled.</summary>
        public const float PlayHours = 1.5f;

        // ================= R3-C6. Dog-breeders =================

        /// <summary>A dog bought keeps at the heels of the colony's best with animals this many hours.</summary>
        public const float DogHeelHours = 24f;

        // ================= R3-W3. An expedition =================

        /// <summary>An expedition comes back at this chance (settled at its start, out of sight).</summary>
        public const float ReturnChance = 0.6f;

        public static bool Returns(float roll) => roll < ReturnChance;

        // ================= R3-W4. A ransom =================

        /// <summary>The captives are ransomed at this chance: likelier if the colony gave.</summary>
        public static float FreedChance(bool gave) => gave ? 0.75f : 0.5f;

        /// <summary>The colony's share of the ransom asked: a fortieth of its silver, in tens, 30 to 150.</summary>
        public static int RansomShare(int silver) => Math.Max(30, Math.Min(150, Tens(silver / 40f)));

        /// <summary>Whether they are ransomed is settled once this many days have passed (a day before the freed can walk home, whatever the hour of the daily check).</summary>
        public const float SettledDay = 7.5f;

        // ================= R3-W5. The year of the rats =================

        /// <summary>How many rats come onto the colony's land: four to eight, by a roll.</summary>
        public static int Rats(float roll) => 4 + Math.Max(0, Math.Min(4, (int)(roll * 5f)));

        /// <summary>The catcher's dog goes after a rat this near (cells) to where it is.</summary>
        public const float RatRange = 45f;

        // ================= R3-W6. A new way spreads =================

        /// <summary>
        /// The craft a new way is taught in, by the age of the people who bring it: a new way of hafting and binding
        /// (tribal), the heavy plough (medieval), a threshing machine (industrial), a fabrication pattern (spacer).
        /// </summary>
        public static string SkillFor(string ageTag) => ageTag == "medieval" || ageTag == "industrial" ? "Plants" : "Crafting";

        /// <summary>The flag the world event sets for the craft (its lesson answer shows only with it).</summary>
        public static string SkillFlag(string skill) => skill == "Plants" ? "growing" : "crafting";
    }
}
