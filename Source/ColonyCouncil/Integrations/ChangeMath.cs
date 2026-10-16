using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// What changed about someone between two looks (see <see cref="PeopleChanges"/>): their race or body (Big &amp; Small
    /// swaps both), their xenotype (a xenogerm, a gene mod), gender, life stage, traits, name or size (a character editor
    /// can change any of them), and whether they still have an inner life at all (a ghoul, the raised dead). Pure (no
    /// game types): Tools/Tests checks it.
    /// </summary>
    public static class ChangeMath
    {
        [Flags]
        public enum Change
        {
            None = 0,
            Race = 1,
            Xenotype = 2,
            Gender = 4,
            Stage = 8,
            Traits = 16,
            Name = 32,
            Size = 64,
            InnerLife = 128
        }

        /// <summary>Who someone is, as far as Rimfolk's records care.</summary>
        public struct Look
        {
            public string race, xenotype, gender, stage, traits, name;
            /// <summary>Body size in tenths (1.0 is 10), so a gene's small nudge counts and rounding noise doesn't.</summary>
            public int size;
            public bool inner;
        }

        /// <summary>Traits as one key, whatever order the game lists them in.</summary>
        public static string TraitKey(IEnumerable<string> traits) =>
            traits == null ? "" : string.Join(",", traits.Where(t => !string.IsNullOrEmpty(t)).OrderBy(t => t, StringComparer.Ordinal));

        /// <summary>Body size in tenths.</summary>
        public static int SizeKey(float bodySize) => float.IsNaN(bodySize) || float.IsInfinity(bodySize) ? 10 : (int)Math.Round(bodySize * 10f);

        public static Change Diff(Look before, Look now)
        {
            var c = Change.None;
            if (before.race != now.race) c |= Change.Race;
            if (before.xenotype != now.xenotype) c |= Change.Xenotype;
            if (before.gender != now.gender) c |= Change.Gender;
            if (before.stage != now.stage) c |= Change.Stage;
            if (before.traits != now.traits) c |= Change.Traits;
            if (before.name != now.name) c |= Change.Name;
            // Half a size or more: a real change of body, not a passing illness or growth spurt.
            if (Math.Abs(before.size - now.size) >= 5) c |= Change.Size;
            if (before.inner != now.inner) c |= Change.InnerLife;
            return c;
        }

        /// <summary>Their open wishes must be asked again whether they still make sense (a body that no longer eats or sleeps).</summary>
        public static bool RecheckWishes(Change c) => (c & (Change.Race | Change.Xenotype | Change.Stage | Change.Size | Change.Traits)) != 0;

        /// <summary>Who they are (tags, voice, leanings) must be read again at once, not at the next hour.</summary>
        public static bool RereadWho(Change c) => c != Change.None;

        /// <summary>Their own wishes are set aside: they no longer have an inner life to want anything with.</summary>
        public static bool SetAside(Change c, Look now) => (c & Change.InnerLife) != 0 && !now.inner;

        /// <summary>
        /// A change worth a line in their own journal: a new body or a new xenotype (whatever made it), never a mere new
        /// name, trait or gender (a character editor's tidying, which nobody in the colony would notice), and never for
        /// someone who no longer has an inner life. The tag the line asks for, or null.
        /// </summary>
        public static string JournalTag(Change c, Look before, Look now)
        {
            if (!now.inner) return null;
            if ((c & Change.Race) != 0) return "race";
            if ((c & Change.Xenotype) != 0) return "xeno";
            if ((c & Change.Size) != 0) return now.size > before.size ? "bigger" : "smaller";
            return null;
        }
    }
}
