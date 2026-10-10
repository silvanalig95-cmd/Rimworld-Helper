using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// More lines for an existing wish, ambition or flavour pool, kept in a separate file. Merged in at startup,
    /// so writing can grow in content packs without touching the original defs (other mods can add lines too).
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

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            int targets = (pool.NullOrEmpty() ? 0 : 1) + (wish == null ? 0 : 1) + (ambition == null ? 0 : 1) + (decree == null ? 0 : 1);
            if (targets != 1) yield return "a flavor extension needs exactly one target: pool, wish, ambition or decree";
        }
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
                merged++;
            }
            if (merged > 0 && Prefs.DevMode) Log.Message("[Rimfolk] merged " + merged + " flavor extension(s).");
        }
    }
}
