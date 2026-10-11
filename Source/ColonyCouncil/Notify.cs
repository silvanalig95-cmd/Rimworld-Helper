using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>How much a message matters, for the player's notification setting.</summary>
    public enum Importance
    {
        /// <summary>Colour: a dream, a small social moment, a quirk picked up. Journals and tabs have it anyway.</summary>
        Flavor,
        /// <summary>Worth knowing: a bond formed, a vendetta sworn, a secret out.</summary>
        Notable,
        /// <summary>Hard to miss: something that changes the colony.</summary>
        Important
    }

    public enum MessageLevel
    {
        Everything,
        Notable,
        LettersOnly
    }

    /// <summary>One place where the mod's many systems send their messages, so one setting can quiet them all.</summary>
    public static class Notify
    {
        public static bool Shows(Importance importance)
        {
            switch (ColonyCouncilMod.Settings.messageLevel)
            {
                case MessageLevel.Everything: return true;
                case MessageLevel.LettersOnly: return importance >= Importance.Important;
                default: return importance >= Importance.Notable;
            }
        }

        public static void Message(string text, LookTargets look, MessageTypeDef type, Importance importance)
        {
            if (text.NullOrEmpty() || !Shows(importance)) return;
            Messages.Message(text, look ?? LookTargets.Invalid, type, false);
        }
    }
}
