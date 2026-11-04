using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>One earlier mention's mark as a callback reads it now: its tags (talked:KEY, then:over...) and values ({THEN}, {NOW}).</summary>
    public sealed class MarkFacts
    {
        public string key;
        /// <summary>The mark as kept: what it was about then, and the fact behind it.</summary>
        public string subject, fact;
        public readonly HashSet<string> tags = new HashSet<string>();
        public readonly Dictionary<string, string> values = new Dictionary<string, string>();
        public readonly Dictionary<string, float> heat = new Dictionary<string, float>();
    }

    /// <summary>
    /// Everything true for a scene that a text can be about, as the facts service gives it (<see cref="Facts.For"/>): tags
    /// (the conditions lines are written for), values (the placeholders), the fact behind a value (so a later mention
    /// can follow what became of it), how current each tag is (heat), earlier mentions' marks, and the world's news near
    /// the place when the scene asks for it. Pure: the conversations' choosing (OutsiderTalkMath) and Tools/Tests read it.
    /// </summary>
    public sealed class FactSet
    {
        public readonly HashSet<string> tags = new HashSet<string>();
        /// <summary>What only the speaker or the one spoken to brings to their own lines (their traits, their age).</summary>
        public readonly HashSet<string> usTags = new HashSet<string>();
        public readonly HashSet<string> themTags = new HashSet<string>();
        public readonly Dictionary<string, string> values = new Dictionary<string, string>();
        /// <summary>For a placeholder, the fact behind it ("ws:12", "war:5"), so a mark can follow what became of it.</summary>
        public readonly Dictionary<string, string> refs = new Dictionary<string, string>();
        public readonly Dictionary<string, float> heat = new Dictionary<string, float>();
        /// <summary>
        /// For a placeholder that names a thing with a card (a town, a people, a person), the thing in its text form ("t55"),
        /// so text drawn in Rimfolk's own windows can be filled with it as a link (Texts.Fill with links); the game's never is.
        /// </summary>
        public readonly Dictionary<string, string> things = new Dictionary<string, string>();
        public readonly List<MarkFacts> marks = new List<MarkFacts>();
        /// <summary>Real things near the place as someone there would tell of them, finished lines (the provider "news").</summary>
        public readonly List<string> news = new List<string>();

        /// <summary>A tag, with how current it is (the higher heat kept when it comes twice).</summary>
        public void Tag(string tag, float h = 0f)
        {
            if (string.IsNullOrEmpty(tag)) return;
            tags.Add(tag);
            if (h > 0f && (!heat.TryGetValue(tag, out float was) || h > was)) heat[tag] = h;
        }

        /// <summary>A placeholder's value (nothing for an empty one), and the fact behind it.</summary>
        public void Value(string key, string value, string fact = null)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return;
            values[key] = value;
            if (fact != null) refs[key] = fact;
        }

        /// <summary>The thing a placeholder's value names (its text form, "t55"), for a linked fill.</summary>
        public void Thing(string key, string thing)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(thing) || !values.ContainsKey(key)) return;
            things[key] = thing;
        }
    }
}
