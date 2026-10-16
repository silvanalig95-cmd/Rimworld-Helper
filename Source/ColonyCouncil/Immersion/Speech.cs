using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The one way Rimfolk's colonists speak. Every line said over someone's head (a wish, a hum of a remark, a word
    /// about the weather, how they really are, a meeting's back and forth) comes through <see cref="Say"/>, and every
    /// moment between two people written into the social log (a social event, an encounter, a meeting woken for)
    /// through <see cref="Logged"/>, so they all show the same way, as the setting "Colonists' speech shows as" says:
    /// <list type="bullet">
    /// <item>Bubbles (the default, when Interaction Bubbles is loaded): a speech bubble, like the game's own chats.
    /// A line said by one person goes straight to the bubbles and never into the Log tab, so the log isn't filled with
    /// ambience; a moment between two goes into the log, where the bubbles hear of it.</item>
    /// <item>Floating text (always without Interaction Bubbles): a short line floating up over the speaker, the
    /// moments between two included; those are still kept in the social log, quietly.</item>
    /// <item>Off: nothing over anyone's head. The social log still keeps the moments between two.</item>
    /// </list>
    /// How often anyone speaks stays with the callers (the hourly looks of the visual touches, a line per colonist a
    /// day, and so on); this only decides how a line shows.
    /// </summary>
    public static class Speech
    {
        public const string BubblesPackageId = "jaxe.bubbles";

        private static readonly Color DefaultColor = new Color(0.9f, 0.92f, 0.88f);
        private static readonly Color NarrationColor = new Color(0.85f, 0.9f, 1f);

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>Speech shows at all (the setting isn't Off).</summary>
        public static bool On => (S?.speechShows ?? SpeechMode.Bubbles) != SpeechMode.Off;

        private static bool? bubblesLoaded;

        /// <summary>Interaction Bubbles is loaded (its hook on the play log draws what goes into it as bubbles).</summary>
        public static bool BubblesLoaded
        {
            get
            {
                if (bubblesLoaded == null)
                {
                    try
                    {
                        bubblesLoaded = People.ModActive(BubblesPackageId);
                    }
                    catch
                    {
                        bubblesLoaded = false;
                    }
                }
                return bubblesLoaded.Value;
            }
        }

        /// <summary>How speech shows now: the setting, with bubbles only when Interaction Bubbles is there.</summary>
        public static SpeechMode Shows => SpeechMath.Shows(S?.speechShows ?? SpeechMode.Bubbles, BubblesLoaded);

        // ---------------- One person speaking ----------------

        /// <summary>
        /// A short line over <paramref name="p"/>'s head: a bubble or floating text, as the setting says. With
        /// <paramref name="other"/>, it is said to them, and only when the two are really together (otherwise nothing
        /// shows). Lines are never written into the social log; what matters goes to the news, the journals and the
        /// chronicle as before. True when it showed.
        /// </summary>
        public static bool Say(Pawn p, string line, Pawn other = null, Color? color = null)
        {
            if (!On || p == null || !p.Spawned || line.NullOrEmpty()) return false;
            if (other != null && other != p && !Together.Can(p, other)) return false;
            // Only the map being looked at shows anything; nothing is made for the others.
            if (Find.CurrentMap != p.Map) return false;
            string text = TextUtil.ShortSpeech(line);
            if (text.NullOrEmpty()) return false;
            try
            {
                if (Shows == SpeechMode.Bubbles && Bubble(p, other, text)) return true;
                Float(p, text, color ?? DefaultColor);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("speech", e);
                return false;
            }
        }

        // ---------------- A moment between two, in the social log ----------------

        /// <summary>
        /// A moment between two people (already near each other: <see cref="SocialLog.Add"/> checks) written into the
        /// social log, and shown over the first of them as the setting says. <paramref name="show"/> false: kept in the
        /// log only (a line was already said for it).
        /// </summary>
        public static void Logged(Pawn a, Pawn b, string text, bool show = true)
        {
            // together: SocialLog.Add checks the two are near before anything is logged.
            if (a == null || b == null || text.NullOrEmpty() || Find.PlayLog == null) return;
            var def = EventDef;
            if (def == null) return;
            var shows = Shows;
            bool bubbles = BubblesLoaded;
            try
            {
                var entry = new PlayLogEntry_CouncilEvent(def, a, b, text);
                if (SpeechMath.ThroughLog(shows, bubbles, show)) Find.PlayLog.Add(entry);
                else AddQuietly(entry);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("social log", e);
            }
            if (SpeechMath.Floats(shows, show) && a.Spawned && a.Map == Find.CurrentMap)
            {
                try
                {
                    Float(a, TextUtil.ShortSpeech(text, 120), NarrationColor);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("social log text", e);
                }
            }
        }

        // ---------------- How it shows ----------------

        private static void Float(Pawn p, string text, Color color) =>
            MoteMaker.ThrowText(p.DrawPos + new Vector3(0f, 0f, 0.8f), p.Map, text, color, 5f);

        /// <summary>
        /// A bubble for a line, without a line in the Log tab: handed straight to Interaction Bubbles. If its way in
        /// can't be found (a newer version), through the play log and straight out again. False: no bubble (floating
        /// text instead).
        /// </summary>
        private static bool Bubble(Pawn p, Pawn other, string text)
        {
            // apart: one person's line, handed to the bubbles only (never kept in the log).
            var def = SpeechDef;
            if (def == null) return false;
            LogEntry entry = other != null && other != p ? new PlayLogEntry_CouncilEvent(def, p, other, text) : new PlayLogEntry_CouncilSpeech(def, p, text);
            var add = BubblerAdd;
            if (add != null)
            {
                try
                {
                    add(entry);
                    return true;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("interaction bubbles", e);
                    bubblerAdd = null;
                }
            }
            var entries = Find.PlayLog?.AllEntries;
            if (entries == null) return false;
            // The game trims a full log as it adds: an old entry pushed out for a line that isn't kept goes back in.
            var oldest = entries.Count > 0 ? entries[entries.Count - 1] : null;
            Find.PlayLog.Add(entry);
            entries.Remove(entry);
            if (oldest != null && !entries.Contains(oldest)) entries.Add(oldest);
            return true;
        }

        /// <summary>Into the log the way the game puts entries there (newest first), without its door, so nothing draws it.</summary>
        private static void AddQuietly(LogEntry entry)
        {
            var list = Find.PlayLog?.AllEntries;
            if (list == null) return;
            list.Insert(0, entry);
            // The game trims the log to its size the next time it adds anything itself.
        }

        private static Action<LogEntry> bubblerAdd;
        private static bool bubblerLooked;

        /// <summary>Interaction Bubbles' own way in (Bubbles.Core.Bubbler.Add), when it has one we know.</summary>
        private static Action<LogEntry> BubblerAdd
        {
            get
            {
                if (bubblerLooked) return bubblerAdd;
                bubblerLooked = true;
                if (!BubblesLoaded) return null;
                try
                {
                    var type = GenTypes.GetTypeInAnyAssembly("Bubbles.Core.Bubbler");
                    var method = type?.GetMethod("Add", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(LogEntry) }, null);
                    if (method != null && method.ReturnType == typeof(void))
                        bubblerAdd = (Action<LogEntry>)Delegate.CreateDelegate(typeof(Action<LogEntry>), method);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("interaction bubbles lookup", e);
                    bubblerAdd = null;
                }
                return bubblerAdd;
            }
        }

        private static InteractionDef eventDef, speechDef;

        private static InteractionDef EventDef => eventDef ??= DefDatabase<InteractionDef>.GetNamedSilentFail("Council_SocialEvent");

        private static InteractionDef SpeechDef => speechDef ??= DefDatabase<InteractionDef>.GetNamedSilentFail("Council_Speech") ?? EventDef;
    }

    /// <summary>
    /// A line one colonist says (Speech.Say), as Interaction Bubbles draws it: the game's entry for something one person
    /// does, with the line kept as written. Handed to the bubbles only, never kept in the log.
    /// </summary>
    public class PlayLogEntry_CouncilSpeech : PlayLogEntry_InteractionSinglePawn
    {
        private string text;

        public PlayLogEntry_CouncilSpeech()
        {
        }

        public PlayLogEntry_CouncilSpeech(InteractionDef def, Pawn speaker, string text) : base(def, speaker, new List<RulePackDef>())
        {
            this.text = text;
        }

        protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog) => text ?? "";

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref text, "text");
        }
    }
}
