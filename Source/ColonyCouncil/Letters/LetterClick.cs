using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The pop-up's click (the player, 10 November 2026: "i should be able to read the letter directly by clicking the
    /// pop-up"). A message about one of the colony's letters written out in full ("has written", "in the mailbag", "handed
    /// to", "waits for a go-between") comes through Notify.LetterMessage with the letter's id; as it is shown (UI/News.cs)
    /// the game's message is kept here with the letter while it is on screen (<see cref="ClickMap{T}"/>), and a left click on
    /// it opens the letter in <see cref="LetterReadWindow"/> instead of jumping the camera (<see cref="Patch_LetterMessageClick"/>).
    /// The text ends "Click to read it." only while the click works; with the patch not applied, broken or the deeper hooks
    /// off, a click jumps the camera as the game's own does and the text says where the letter can be read instead. The
    /// on-screen panels' boxes behind the messages (Hud/HudMessages.cs) are drawn behind the game's own message, so a click
    /// there is the same click.
    /// </summary>
    public static class LetterClick
    {
        private static readonly ClickMap<Message> shown = new ClickMap<Message>();

        /// <summary>The click opens the letter now: the patch is on the game's message and the deeper hooks are on.</summary>
        public static bool Works => RimfolkPatches.On && !Patch_LetterMessageClick.broken && RimfolkPatches.Applied.Contains(nameof(Patch_LetterMessageClick));

        /// <summary>The letter was written out in full and kept, to be read (Letters/LetterBook.cs).</summary>
        public static bool Readable(int letterId) => letterId >= 0 && LetterBook.ForLetter(letterId)?.Ours != null;

        /// <summary>The words that end a message about this letter (a space before them), or "" when it wasn't written out.</summary>
        public static string Hint(int letterId, bool clickable)
        {
            var l = NeighbourLetters.Get(letterId);
            bool inBag = l != null && l.stage == LetterStage.Outbox && !l.bringing;
            string key = LetterClickMath.HintKey(Readable(letterId), clickable, inBag);
            if (key == null) return "";
            var n = l != null ? NotableBook.Instance?.Get(l.notable) : null;
            return " " + key.Translate((n?.Name ?? l?.toName ?? "?").Named("NAME")).Resolve();
        }

        /// <summary>
        /// The game's message for a line about a letter (from News.Show): its words with the hint. <paramref name="clickable"/>:
        /// the click opens it, so <see cref="Shown"/> keeps it with the letter once it is on screen.
        /// </summary>
        public static Message Make(string text, int letterId, MessageTypeDef type, LookTargets look, out bool clickable)
        {
            clickable = Works && Readable(letterId);
            return new Message((text ?? "").CapitalizeFirst() + Hint(letterId, clickable), type ?? MessageTypeDefOf.NeutralEvent, look ?? LookTargets.Invalid);
        }

        /// <summary>The message is on screen: kept with its letter while it stays there.</summary>
        public static void Shown(Message m, int letterId)
        {
            if (m == null || letterId < 0) return;
            shown.Prune(Messages.IsLive);
            shown.Note(m, letterId);
        }

        /// <summary>The letter kept with a message on screen, or -1.</summary>
        public static int LetterOf(Message m) => shown.Count == 0 ? -1 : shown.Of(m);

        /// <summary>Opens the letter's exchange to read; false when it is no longer kept (the click then jumps the camera as the game's does).</summary>
        public static bool Open(int letterId)
        {
            var x = LetterBook.ForLetter(letterId);
            if (x == null || x.pages.Count == 0) return false;
            LetterReadWindow.Open(x);
            return true;
        }
    }

    /// <summary>
    /// A left click on a message about one of the colony's letters opens the letter (<see cref="LetterClick"/>). The game
    /// draws each message on the root of the interface before any window (Messages.MessagesDoGUI) and answers its click in
    /// the message's own small window afterwards (Message.Draw's button, which jumps the camera); this prefix sees the mouse
    /// going down first, opens the letter and uses the event, so the game's button never fires. Right clicks (dismiss) and
    /// every other message are left to the game. Applied on its own through the patch bootstrap (Patches/RimfolkPatches.cs);
    /// if the game's method changes it's skipped with one warning and the click jumps the camera as before.
    /// </summary>
    [HarmonyPatch(typeof(Message), nameof(Message.Draw))]
    public static class Patch_LetterMessageClick
    {
        internal static bool broken;

        public static void Prefix(Message __instance, int xOffset, int yOffset)
        {
            if (broken || !RimfolkPatches.On) return;
            var e = Event.current;
            if (e == null || e.type != EventType.MouseDown) return;
            try
            {
                int id = LetterClick.LetterOf(__instance);
                if (id < 0) return;
                var rect = __instance.CalculateRect(xOffset, yOffset);
                if (!LetterClickMath.Opens(true, e.button, rect.Contains(e.mousePosition), true, Current.ProgramState == ProgramState.Playing)) return;
                if (!LetterClick.Open(id)) return;
                e.Use();
            }
            catch (Exception ex)
            {
                broken = true;
                CouncilLog.ErrorOnce("letters: the message's click", ex);
            }
        }
    }
}
