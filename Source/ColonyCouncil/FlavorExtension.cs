using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// More lines for an existing wish, ambition, decree, flavour pool, personal story or kind of camp, kept in a separate file. Merged
    /// in at startup, so writing can grow in content packs without touching the original defs (other mods can add lines
    /// too).
    /// </summary>
    public class FlavorExtensionDef : Def
    {
        /// <summary>Target flavour pool (defName of a FlavorPoolDef).</summary>
        public string pool;
        public ToneLines lines;

        public WishDef wish;
        public ToneLines request;
        public ToneLines requestForOther;
        public ToneLines thanks;
        public ToneLines expired;
        public ToneLines reminder;

        public AmbitionDef ambition;
        public ToneLines pitch;
        public ToneLines achieved;

        public DecreeDef decree;
        public ToneLines support;
        public ToneLines oppose;

        /// <summary>A personal story: more words for its stages (in order, as many as it has), its ending early and its eulogy.</summary>
        public ArcDef arc;
        public List<ArcStageWords> stages;
        public ToneLines fizzle;
        [MustTranslate] public List<string> fizzleChronicle;
        public ToneLines eulogy;
        /// <summary>
        /// A kind of camp (Camps/CampKindDef.cs): words joined to its own lists, the same tags and placeholders as there.
        /// <see cref="campLines"/> joins its lines (the short lines over their heads).
        /// </summary>
        public CampKindDef camp;
        [MustTranslate] public List<string> titles;
        [MustTranslate] public List<string> texts;
        [MustTranslate] public List<string> settled;
        [MustTranslate] public List<string> joinTexts;
        [MustTranslate] public List<string> farewell;
        [MustTranslate] public List<string> campLines;
        [MustTranslate] public List<string> momentLines;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            int targets = (pool.NullOrEmpty() ? 0 : 1) + (wish == null ? 0 : 1) + (ambition == null ? 0 : 1) + (decree == null ? 0 : 1) + (arc == null ? 0 : 1) + (camp == null ? 0 : 1);
            if (targets != 1) yield return "a flavor extension needs exactly one target: pool, wish, ambition, decree, arc or camp";
            if (arc != null && stages != null && stages.Count > (arc.stages?.Count ?? 0)) yield return "more stages than the personal story " + arc.defName + " has";
        }
    }

    /// <summary>More words for one stage of a personal story: its journal lines (the colonist's, the other's, the third's) and chronicle lines.</summary>
    public class ArcStageWords
    {
        public ToneLines journal;
        public ToneLines otherJournal;
        public ToneLines thirdJournal;
        [MustTranslate] public List<string> chronicle;
    }

    public static class FlavorExtensions
    {
        public static void Apply()
        {
            int merged = 0;
            foreach (var ext in DefDatabase<FlavorExtensionDef>.AllDefsListForReading)
            {
                if (!ext.pool.NullOrEmpty())
                {
                    var target = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(ext.pool);
                    if (target == null)
                    {
                        CouncilLog.WarnOnce("flavor extension " + ext.defName + " targets unknown pool " + ext.pool);
                        continue;
                    }
                    if (target.lines == null) target.lines = new ToneLines();
                    target.lines.AddFrom(ext.lines);
                }
                if (ext.wish != null)
                {
                    ext.wish.request.AddFrom(ext.request);
                    ext.wish.requestForOther.AddFrom(ext.requestForOther);
                    ext.wish.thanks.AddFrom(ext.thanks);
                    ext.wish.expired.AddFrom(ext.expired);
                    ext.wish.reminder.AddFrom(ext.reminder);
                }
                if (ext.ambition != null)
                {
                    ext.ambition.pitch.AddFrom(ext.pitch);
                    ext.ambition.achieved.AddFrom(ext.achieved);
                }
                if (ext.decree != null)
                {
                    ext.decree.support.AddFrom(ext.support);
                    ext.decree.oppose.AddFrom(ext.oppose);
                }
                if (ext.arc != null) AddToArc(ext);
                if (ext.camp != null) ext.camp.AddWords(ext);
                merged++;
            }
            if (merged > 0 && Prefs.DevMode) Log.Message("[Rimfolk] merged " + merged + " flavor extension(s).");
        }

        /// <summary>
        /// A personal story's more words, stage by stage. The other's and the third's lines go only where the stage has
        /// some already: a stage without them is one where that person writes nothing.
        /// </summary>
        private static void AddToArc(FlavorExtensionDef ext)
        {
            var arc = ext.arc;
            if (ext.stages != null && arc.stages != null)
                for (int i = 0; i < ext.stages.Count && i < arc.stages.Count; i++)
                {
                    var more = ext.stages[i];
                    var st = arc.stages[i];
                    if (more == null || st == null) continue;
                    if (more.journal != null) (st.journal ??= new ToneLines()).AddFrom(more.journal);
                    st.otherJournal?.AddFrom(more.otherJournal);
                    st.thirdJournal?.AddFrom(more.thirdJournal);
                    if (!more.chronicle.NullOrEmpty()) (st.chronicle ??= new List<string>()).AddRange(more.chronicle);
                }
            if (ext.fizzle != null) (arc.fizzle ??= new ToneLines()).AddFrom(ext.fizzle);
            if (!ext.fizzleChronicle.NullOrEmpty()) (arc.fizzleChronicle ??= new List<string>()).AddRange(ext.fizzleChronicle);
            arc.eulogy?.AddFrom(ext.eulogy);
        }
    }
}
