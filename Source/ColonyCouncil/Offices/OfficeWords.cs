using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a seat has beyond its own def, kept in a file of its own (Defs/OfficeDefs/Rimfolk_OfficeWords.xml): its name in
    /// each age (decision 10 of the review: a spacer colony doesn't elect a reeve), more promises to run on (decision 9:
    /// three physicians made the same one), what each of its promises is and how the next election tells whether it was
    /// kept (<see cref="PromiseTest"/>), and the seat's work as the game counts it, for a candidate's record.
    /// </summary>
    public class OfficeWordsDef : Def
    {
        public OfficeDef office;

        /// <summary>The seat's name in each age. A missing one is the nearest age's (OfficeNamesMath.NameIn).</summary>
        [MustTranslate] public string tribal;
        [MustTranslate] public string medieval;
        [MustTranslate] public string industrial;
        [MustTranslate] public string spacer;

        /// <summary>More promises a candidate can run on, added to the office's own at startup.</summary>
        public List<OfficePlank> planks;

        /// <summary>For the office's own planks (by key): what each promises and how it is judged.</summary>
        public List<OfficePlank> promises;

        /// <summary>The seat's work as the game counts it (record defNames, added up): wounds dressed, plants sown.</summary>
        public List<string> measure;

        /// <summary>That work in words, with {COUNT} ("dressed {COUNT} wounds"), and for one ("dressed one wound").</summary>
        [MustTranslate] public string measured;
        [MustTranslate] public string measuredOne;

        public IList<string> Names => new[] { tribal, medieval, industrial, spacer };

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (office == null) yield return "office words with no office";
            if (!measure.NullOrEmpty() && (measured.NullOrEmpty() || measuredOne.NullOrEmpty())) yield return "a measure needs its words for many and for one";
            if (planks != null)
                foreach (var p in planks)
                    if (p == null || p.key.NullOrEmpty() || p.text.NullOrEmpty() || p.promise.NullOrEmpty()) yield return "a plank needs a key, its letter's sentence and what it promises";
            if (promises != null)
                foreach (var p in promises)
                    if (p == null || p.key.NullOrEmpty() || p.promise.NullOrEmpty()) yield return "a promise needs its plank's key and what it promises";
        }
    }

    /// <summary>
    /// The seats' names in the colony's age, and the office words read once at startup: more planks into each office,
    /// and what the office's own planks promise. Every place a seat is named asks <see cref="OfficeDef.Name"/>; a shipped
    /// line that names a seat by its old name goes through <see cref="InAge"/>.
    /// </summary>
    public static class OfficeNames
    {
        private static Dictionary<OfficeDef, OfficeWordsDef> words;

        private static Dictionary<OfficeDef, OfficeWordsDef> Words
        {
            get
            {
                if (words != null) return words;
                var map = new Dictionary<OfficeDef, OfficeWordsDef>();
                foreach (var w in DefDatabase<OfficeWordsDef>.AllDefsListForReading)
                    if (w.office != null && !map.ContainsKey(w.office))
                        map[w.office] = w;
                words = map;
                return words;
            }
        }

        public static OfficeWordsDef Of(OfficeDef office) => office != null && Words.TryGetValue(office, out var w) ? w : null;

        /// <summary>The seat's name in an age (the office's own label when it has none).</summary>
        public static string Of(OfficeDef office, WorldAge age) => office == null ? "" : OfficeNamesMath.NameIn(Of(office)?.Names, age, office.label);

        private static WorldAge cachedAge = (WorldAge)(-1);
        private static List<(string from, string to)> cachedPairs;

        /// <summary>Each seat's old name (its label) and its name in the colony's age now.</summary>
        private static List<(string from, string to)> Pairs()
        {
            var age = Ages.Ours;
            if (cachedPairs != null && cachedAge == age) return cachedPairs;
            cachedPairs = DefDatabase<OfficeDef>.AllDefsListForReading.Select(o => (o.label, Of(o, age))).ToList();
            cachedAge = age;
            return cachedPairs;
        }

        /// <summary>A shipped line that names a seat by its old name, said with the seat's name in the colony's age.</summary>
        public static string InAge(string text)
        {
            if (text.NullOrEmpty()) return text;
            try
            {
                return OfficeNamesMath.Rename(text, Pairs());
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("office names", e);
                return text;
            }
        }

        /// <summary>
        /// A keyed line about one of the colony's seats, its seat words said in the colony's age before anything is filled
        /// in, so a name that happens to be a seat's (a dog called Marshal) is left alone.
        /// </summary>
        public static string Key(string key, params NamedArgument[] args) => InAge(key.Translate().RawText).Formatted(args).Resolve();

        /// <summary>At startup: the office words' planks join their offices, and the offices' own planks learn what they promise.</summary>
        public static void Apply()
        {
            words = null;
            foreach (var w in DefDatabase<OfficeWordsDef>.AllDefsListForReading)
            {
                var o = w.office;
                if (o == null) continue;
                o.planks ??= new List<OfficePlank>();
                if (w.promises != null)
                    foreach (var p in w.promises)
                    {
                        var own = o.planks.FirstOrDefault(x => x != null && x.key == p.key);
                        if (own == null) continue;
                        own.promise = p.promise;
                        own.test = p.test;
                    }
                if (w.planks == null) continue;
                foreach (var p in w.planks)
                {
                    if (p == null || p.key.NullOrEmpty() || o.planks.Any(x => x != null && x.key == p.key)) continue;
                    o.planks.Add(p);
                }
            }
        }
    }
}
