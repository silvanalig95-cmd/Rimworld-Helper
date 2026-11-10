using System;
using System.Runtime.CompilerServices;
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
        /// <summary>Hard to miss: something that changes the colony. Never held back by a day's cap (UI/Tell.cs).</summary>
        Important
    }

    public enum MessageLevel
    {
        Everything,
        Notable,
        LettersOnly
    }

    /// <summary>
    /// The "How many messages" setting, and the older way in for the mod's messages: <see cref="Message"/> goes through
    /// <see cref="Tell.Message"/>, Rimfolk's one voice to the player, counted under the kind of news of the folder its
    /// caller sits in (World/ as world news, Social/ as people's, and so on: <see cref="TellMath.KindOfFolder"/>).
    /// </summary>
    public static class Notify
    {
        public static bool Shows(Importance importance)
        {
            switch (ColonyCouncilMod.Settings?.messageLevel ?? MessageLevel.Notable)
            {
                case MessageLevel.Everything: return true;
                case MessageLevel.LettersOnly: return importance >= Importance.Important;
                default: return importance >= Importance.Notable;
            }
        }

        public static void Message(string text, LookTargets look, MessageTypeDef type, Importance importance, [CallerFilePath] string caller = "")
        {
            if (text.NullOrEmpty() || !Shows(importance)) return;
            Tell.Message(KindOf(caller), importance, null, text, look, type);
        }

        /// <summary>
        /// A message about one of the colony's letters (<paramref name="letterId"/>): when the letter was written out in full, the
        /// message ends with how to read it and a click on it opens it (Letters/LetterClick.cs); otherwise as <see cref="Message"/>.
        /// </summary>
        public static void LetterMessage(string text, int letterId, LookTargets look, MessageTypeDef type, Importance importance, [CallerFilePath] string caller = "")
        {
            if (text.NullOrEmpty() || !Shows(importance)) return;
            Tell.Message(KindOf(caller), importance, null, text, look, type, read: LetterClick.Readable(letterId) ? letterId : -1);
        }

        /// <summary>The kind of news a caller's messages count under, from its source folder.</summary>
        private static NewsKind KindOf(string caller) =>
            Enum.TryParse(TellMath.KindOfFolder(TellMath.FolderOf(caller)), out NewsKind k) ? k : NewsKind.Colony;
    }
}
