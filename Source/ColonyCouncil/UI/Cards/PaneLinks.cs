using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Names in a line Rimfolk writes and draws itself, marked as links to their cards (docs/design/interface.md, "Links
    /// and previews"): a colonist, a person outside, a people, a town or a house, at its first mention in the line
    /// (LinkTextMath.MarkNames). Only for text drawn through LinkText (Rimfolk's windows, its inspect tabs, its column in
    /// the Social tab, the defense page): never for the game's own text (its letters, messages, the play log) and never for
    /// a tooltip, which the game draws plain. Marked once when a model is built, not every frame.
    /// </summary>
    public static class PaneLinks
    {
        private static readonly List<KeyValuePair<string, string>> names = new List<KeyValuePair<string, string>>();

        private static List<KeyValuePair<string, string>> Names()
        {
            names.Clear();
            return names;
        }

        private static void Add(List<KeyValuePair<string, string>> list, string name, ThingRef thing)
        {
            if (name.NullOrEmpty() || thing.IsNone) return;
            list.Add(new KeyValuePair<string, string>(name, thing.ToString()));
        }

        /// <summary>The text with a pawn's short name (as the game writes it in a sentence) marked as a link to their card.</summary>
        public static string Mark(string text, Pawn p)
        {
            if (text.NullOrEmpty() || p == null) return text;
            var list = Names();
            Add(list, p.LabelShort, ThingRefs.Of(p));
            return LinkTextMath.MarkNames(text, list);
        }

        /// <summary>The text with each of these pawns' short names marked as links (the first mention of each).</summary>
        public static string Mark(string text, IEnumerable<Pawn> pawns)
        {
            if (text.NullOrEmpty() || pawns == null) return text;
            var list = Names();
            foreach (var p in pawns)
                if (p != null)
                    Add(list, p.LabelShort, ThingRefs.Of(p));
            return list.Count == 0 ? text : LinkTextMath.MarkNames(text, list);
        }

        /// <summary>The text with a people's name marked as a link to its card (never the colony's own).</summary>
        public static string Mark(string text, Faction f)
        {
            if (text.NullOrEmpty() || f == null || f.IsPlayer) return text;
            var list = Names();
            Add(list, f.Name, ThingRefs.Of(f));
            return LinkTextMath.MarkNames(text, list);
        }

        /// <summary>The text with each of these peoples' names marked as links.</summary>
        public static string Mark(string text, IEnumerable<Faction> factions)
        {
            if (text.NullOrEmpty() || factions == null) return text;
            var list = Names();
            foreach (var f in factions)
                if (f != null && !f.IsPlayer)
                    Add(list, f.Name, ThingRefs.Of(f));
            return list.Count == 0 ? text : LinkTextMath.MarkNames(text, list);
        }

        /// <summary>The text with a name marked as a link to a thing (a notable, a town, a house the ledger keeps).</summary>
        public static string Mark(string text, string name, ThingRef thing)
        {
            if (text.NullOrEmpty() || name.NullOrEmpty() || thing.IsNone) return text;
            var list = Names();
            Add(list, name, thing);
            return LinkTextMath.MarkNames(text, list);
        }
    }
}
