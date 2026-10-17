using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// When and to whom a mark comes (on a <see cref="PersonaMarkDef"/>, as a mod extension). Marks that answer the same
    /// happening share a <see cref="family"/>: a hook or a memory names the family, and the person's nature picks the
    /// mark among them, in <see cref="order"/>, by <see cref="when"/>. So a raid hardens the bold and shakes the timid.
    /// </summary>
    public class PersonaMarkExtension : DefModExtension
    {
        /// <summary>The happening it answers ("Downed", "LostFriend"); the def's own name when left out.</summary>
        public string family;
        /// <summary>
        /// Which happening it tells, for "one happening, one mark": a death told by three memories (the friend, the
        /// brother, the comrade) leaves one mark of the story "Loss" about that person.
        /// </summary>
        public string story;
        /// <summary>Memories that bring it (ThoughtDef names; one from a DLC or mod that isn't loaded is skipped).</summary>
        public List<string> thoughts;
        /// <summary>The memory must name someone (its other pawn), who becomes {OTHER}.</summary>
        public bool needsOther;
        /// <summary>A mark from a memory is about the day it came ({LABEL} is "5 Aprimay"), not its default label.</summary>
        public bool labelDay;
        /// <summary>From a memory about someone: only if they thought this well of them (a friend, not everyone they liked).</summary>
        public int minOpinion = -101;
        /// <summary>Only for those whose nature fits: "nerve&gt;=10, warmth&lt;=0". Empty: anyone.</summary>
        public string when;
        /// <summary>How their nature scales the move: "nerve:-0.6" (the timid take it harder).</summary>
        public string lean;
        /// <summary>Only children (under 18) or only adults; "any" (or empty) for both.</summary>
        public string ages;
        /// <summary>Within a family, lower is tried first; the last one usually has no condition.</summary>
        public int order;
        /// <summary>The chance it takes once it fits.</summary>
        public float chance = 1f;
        /// <summary>
        /// The same mark doesn't come again for this many days. 0: a turning point waits out its keeping
        /// (<see cref="PersonaMarkDef.keepDays"/>), a small mark <see cref="MarksMath.MinorRepeatDays"/>.
        /// </summary>
        public int repeatDays;
        /// <summary>What they write in their journal when it happens (turning points): {PAWN}, {OTHER}, {LABEL}.</summary>
        public ToneLines journal = new ToneLines();
        /// <summary>A line of news when it happens (third person): {PAWN}, {OTHER}, {LABEL}.</summary>
        [MustTranslate] public string news;
        /// <summary>The news line also goes into the chronicle.</summary>
        public bool chronicle;

        [Unsaved] private List<MarkCond> conds;
        [Unsaved] private List<MarkLean> leans;
        [Unsaved] private string error;
        [Unsaved] private bool parsed;

        private void Parse()
        {
            if (parsed) return;
            parsed = true;
            if (!MarksMath.TryParseWhen(when, out conds, out string e1)) error = "when: " + e1;
            if (!MarksMath.TryParseLean(lean, out leans, out string e2)) error = (error == null ? "" : error + "; ") + "lean: " + e2;
            conds ??= new List<MarkCond>();
            leans ??= new List<MarkLean>();
        }

        public List<MarkCond> Conditions
        {
            get
            {
                Parse();
                return conds;
            }
        }

        public List<MarkLean> Leans
        {
            get
            {
                Parse();
                return leans;
            }
        }

        public bool ForChildren => ages == "child";
        public bool ForAdults => ages == "adult";

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            Parse();
            if (error != null) yield return error;
            if (!ages.NullOrEmpty() && ages != "child" && ages != "adult" && ages != "any") yield return "ages must be child, adult or any";
            if (chance <= 0f || chance > 1f) yield return "chance must be above 0 and at most 1";
        }
    }

    /// <summary>Finding marks by family and by the memory that brings them, built once.</summary>
    public static class MarkDefs
    {
        private static Dictionary<string, List<PersonaMarkDef>> byFamily;
        private static Dictionary<ThoughtDef, List<string>> byThought;
        private static int generation = -1;

        public static PersonaMarkExtension Ext(PersonaMarkDef def) => def?.GetModExtension<PersonaMarkExtension>();

        public static string FamilyOf(PersonaMarkDef def) => Ext(def)?.family.NullOrEmpty() == false ? Ext(def).family : def?.defName;

        public static string StoryOf(PersonaMarkDef def)
        {
            var x = Ext(def);
            return x == null ? null : x.story.NullOrEmpty() ? FamilyOf(def) : x.story;
        }

        private static void Build()
        {
            if (byFamily != null && generation == DefRefs.Generation) return;
            generation = DefRefs.Generation;
            byFamily = new Dictionary<string, List<PersonaMarkDef>>();
            byThought = new Dictionary<ThoughtDef, List<string>>();
            foreach (var def in DefDatabase<PersonaMarkDef>.AllDefsListForReading)
            {
                string fam = FamilyOf(def);
                if (!byFamily.TryGetValue(fam, out var list)) byFamily[fam] = list = new List<PersonaMarkDef>();
                list.Add(def);
                var x = Ext(def);
                if (x?.thoughts == null) continue;
                foreach (var name in x.thoughts)
                {
                    var td = name.NullOrEmpty() ? null : DefDatabase<ThoughtDef>.GetNamedSilentFail(name);
                    if (td == null) continue;
                    if (!byThought.TryGetValue(td, out var fams)) byThought[td] = fams = new List<string>();
                    if (!fams.Contains(fam)) fams.Add(fam);
                }
            }
            foreach (var list in byFamily.Values) list.SortBy(d => Ext(d)?.order ?? 0, d => d.defName);
        }

        /// <summary>The marks of a family, in the order they're tried.</summary>
        public static List<PersonaMarkDef> Family(string family)
        {
            Build();
            return family != null && byFamily.TryGetValue(family, out var list) ? list : null;
        }

        /// <summary>The families a memory brings, or null (asked for every memory anyone gains, so a dictionary).</summary>
        public static List<string> ForThought(ThoughtDef def)
        {
            if (def == null) return null;
            Build();
            return byThought.TryGetValue(def, out var fams) ? fams : null;
        }

        public static IEnumerable<string> Families
        {
            get
            {
                Build();
                return byFamily.Keys.ToList();
            }
        }
    }
}
