using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    public enum AlmanacCategory
    {
        Basics,
        Council,
        Food,
        Rooms,
        Mood,
        Defense,
        Defending,
        Health,
        Management,
        LowTech,
        RoomDesign,
        Mistakes,
        Royalty,
        Ideology,
        Biotech,
        Anomaly,
        Odyssey,
        Numbers,
        Mods
    }

    /// <summary>
    /// One page of the council's almanac: good practice, the numbers behind it, and what to do. Written in the
    /// game's voice, linked to the advisor's warnings and to room plans.
    /// </summary>
    /// <summary>
    /// A part of an almanac page: an optional heading, then any of a paragraph (blank lines start new paragraphs),
    /// points, and a table, in that order, then an optional closing paragraph. Use whichever suits the content.
    /// </summary>
    public class AlmanacSection
    {
        [MustTranslate] public string heading;
        [MustTranslate] public string text;
        [MustTranslate] public List<string> points;
        /// <summary>Rows of cells separated by "|"; the first row is the header.</summary>
        [MustTranslate] public List<string> table;
        [MustTranslate] public string after;
    }

    public class AlmanacArticleDef : Def
    {
        public AlmanacCategory category = AlmanacCategory.Basics;
        public int order = 100;
        /// <summary>One line shown in the article list.</summary>
        [MustTranslate] public string summary;
        /// <summary>A line from the colony's old hands, shown as a quote.</summary>
        [MustTranslate] public string tip;
        /// <summary>The hard facts, with numbers.</summary>
        [MustTranslate] public List<string> facts;
        /// <summary>Practical steps.</summary>
        [MustTranslate] public List<string> advice;
        /// <summary>
        /// The page's own structure, when its subject needs one ("Setting it up", "In a siege"...): shown after the
        /// description, in order. Pages without sections show their facts and advice as one list of points.
        /// </summary>
        public List<AlmanacSection> sections;
        /// <summary>Extra words the search should find this by.</summary>
        public List<string> keywords;
        public List<AlmanacArticleDef> seeAlso;
        /// <summary>A room plan that puts this advice into practice.</summary>
        public RoomTemplateDef template;
        /// <summary>Only relevant with this DLC: Royalty, Ideology, Biotech, Anomaly or Odyssey.</summary>
        public string dlc;
        /// <summary>Some numbers come from community testing or differ between sources.</summary>
        public bool approximate;
        /// <summary>About these mods (package ids): shown only when one of them is active.</summary>
        public List<string> mods;

        /// <summary>Is one of the article's mods active?</summary>
        public bool ModsActive => mods.NullOrEmpty() || mods.Any(People.ModActive);

        /// <summary>The names of the article's mods, as the game knows them (the active ones, or the ids).</summary>
        public string ModNames()
        {
            if (mods.NullOrEmpty()) return null;
            var names = mods.Select(id => People.ActiveMod(id)?.Name).Where(n => !n.NullOrEmpty()).Distinct().ToList();
            return names.Count > 0 ? string.Join(", ", names) : string.Join(", ", mods);
        }

        public bool DlcActive
        {
            get
            {
                if (!ModsActive) return false;
                switch (dlc)
                {
                    case null:
                    case "": return true;
                    case "Royalty": return ModsConfig.RoyaltyActive;
                    case "Ideology": return ModsConfig.IdeologyActive;
                    case "Biotech": return ModsConfig.BiotechActive;
                    case "Anomaly": return ModsConfig.AnomalyActive;
                    case "Odyssey": return ModsConfig.OdysseyActive;
                    default: return true;
                }
            }
        }

        [Unsaved] private string searchText;

        public bool Matches(string query)
        {
            if (query.NullOrEmpty()) return true;
            if (searchText == null)
            {
                var parts = new List<string> { label, summary, description, tip };
                if (facts != null) parts.AddRange(facts);
                if (advice != null) parts.AddRange(advice);
                if (keywords != null) parts.AddRange(keywords);
                if (sections != null)
                    foreach (var sec in sections.Where(x => x != null))
                    {
                        parts.Add(sec.heading);
                        parts.Add(sec.text);
                        parts.Add(sec.after);
                        if (sec.points != null) parts.AddRange(sec.points);
                        if (sec.table != null) parts.AddRange(sec.table.Select(r => r.Replace('|', ' ')));
                    }
                searchText = string.Join(" ", parts.Where(p => !p.NullOrEmpty())).ToLowerInvariant();
            }
            foreach (var word in query.ToLowerInvariant().Split(' '))
            {
                if (word.Length > 0 && !searchText.Contains(word)) return false;
            }
            return true;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (description.NullOrEmpty() && (facts == null || facts.Count == 0) && (sections == null || sections.Count == 0)) yield return "almanac article has no text";
            if (mods != null && mods.Any(m => m.NullOrEmpty() || m != m.Trim().ToLowerInvariant())) yield return "mod package ids must be lowercase";
            if (!dlc.NullOrEmpty() && !new[] { "Royalty", "Ideology", "Biotech", "Anomaly", "Odyssey" }.Contains(dlc)) yield return "unknown dlc " + dlc;
        }
    }
}
