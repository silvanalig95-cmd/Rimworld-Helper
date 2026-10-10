using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>How a colonist phrases things. Picked from traits, mood and social skill.</summary>
    public enum Tone
    {
        Plain,
        Warm,
        Blunt,
        Cheerful,
        Gloomy,
        Cold
    }

    /// <summary>
    /// A bundle of alternative lines, one list per tone. Any list may be empty; picking falls back
    /// to plain lines and then to anything at all.
    /// </summary>
    public class ToneLines
    {
        public List<string> plain = new List<string>();
        public List<string> warm = new List<string>();
        public List<string> blunt = new List<string>();
        public List<string> cheerful = new List<string>();
        public List<string> gloomy = new List<string>();
        public List<string> cold = new List<string>();

        public List<string> For(Tone tone)
        {
            switch (tone)
            {
                case Tone.Warm: return warm;
                case Tone.Blunt: return blunt;
                case Tone.Cheerful: return cheerful;
                case Tone.Gloomy: return gloomy;
                case Tone.Cold: return cold;
                default: return plain;
            }
        }

        public bool Any => AllLines().Any();

        public IEnumerable<string> AllLines()
        {
            foreach (var list in new[] { plain, warm, blunt, cheerful, gloomy, cold })
            {
                if (list == null) continue;
                foreach (var s in list) yield return s;
            }
        }

        /// <summary>
        /// A line in this tone that fits the moment (see <see cref="Flavor"/> for tags), falling back to plain
        /// lines and then to any line at all. Tags are stripped from the result.
        /// </summary>
        public string Pick(Tone tone, HashSet<string> context = null)
        {
            var list = For(tone);
            string line = null;
            if (list != null && list.Count > 0 && (tone == Tone.Plain || Rand.Chance(0.8f)))
                line = Flavor.Pick(list, context);
            if (line == null && plain != null && plain.Count > 0) line = Flavor.Pick(plain, context);
            if (line == null) line = Flavor.Pick(AllLines(), context);
            // Nothing fits the tags: better an untagged-looking line than silence.
            if (line == null)
            {
                var all = AllLines().ToList();
                if (all.Count > 0) line = Flavor.Strip(all.RandomElement());
            }
            return line;
        }
    }

    public static class ToneUtility
    {
        private static TraitDef naturalMood;
        private static bool looked;

        private static TraitDef NaturalMood
        {
            get
            {
                if (!looked)
                {
                    naturalMood = DefDatabase<TraitDef>.GetNamedSilentFail("NaturalMood");
                    looked = true;
                }
                return naturalMood;
            }
        }

        public static Tone ToneFor(Pawn pawn)
        {
            var traits = pawn?.story?.traits;
            if (traits != null)
            {
                if (traits.HasTrait(TraitDefOf.Psychopath)) return Tone.Cold;
                if (traits.HasTrait(TraitDefOf.Abrasive)) return Tone.Blunt;
                if (traits.HasTrait(TraitDefOf.Kind)) return Tone.Warm;
                if (NaturalMood != null)
                {
                    int degree = traits.DegreeOfTrait(NaturalMood);
                    if (degree > 0) return Tone.Cheerful;
                    if (degree < 0) return Tone.Gloomy;
                }
                if (TraitDefOf.Joyous != null && traits.HasTrait(TraitDefOf.Joyous)) return Tone.Cheerful;
            }

            float mood = pawn?.needs?.mood?.CurLevelPercentage ?? 0.5f;
            if (mood < 0.3f) return Tone.Gloomy;

            int social = pawn?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 5;
            if (social >= 12) return Tone.Warm;
            if (social <= 2) return Tone.Blunt;

            // Stable per pawn, so the same colonist tends to sound the same.
            int seed = pawn?.thingIDNumber ?? 0;
            switch (seed % 5)
            {
                case 0: return Tone.Warm;
                case 1: return Tone.Cheerful;
                default: return Tone.Plain;
            }
        }

        public static string LabelFor(Tone tone) => ("CC_Tone_" + tone).Translate();
    }

    public static class TextUtil
    {
        /// <summary>Takes the first sentence of a line for floating speech text.</summary>
        public static string ShortSpeech(string line, int maxLen = 90)
        {
            if (line.NullOrEmpty()) return line;
            string s = line.Trim().Trim('"');
            int cut = -1;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if ((c == '.' || c == '!' || c == '?') && i >= 12)
                {
                    cut = i + 1;
                    break;
                }
            }
            if (cut > 0 && cut < s.Length) s = s.Substring(0, cut);
            if (s.Length > maxLen) s = s.Substring(0, maxLen - 1).TrimEnd() + "…";
            return s;
        }

        public static string DaysString(int ticks)
        {
            float days = ticks / (float)GenDate.TicksPerDay;
            if (days < 1f)
            {
                int hours = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(ticks / (float)GenDate.TicksPerHour));
                return "CC_Hours".Translate(hours);
            }
            return "CC_Days".Translate(days.ToString("0.#"));
        }

        public static string JoinAnd(IList<string> parts)
        {
            if (parts == null || parts.Count == 0) return "";
            if (parts.Count == 1) return parts[0];
            return string.Join(", ", parts.Take(parts.Count - 1)) + " " + "CC_And".Translate() + " " + parts[parts.Count - 1];
        }
    }
}
