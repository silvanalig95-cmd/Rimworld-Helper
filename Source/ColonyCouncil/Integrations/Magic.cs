using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A RimWorld of Magic, read by name: a class is a trait (the "mage" and "fighter" tag groups list them), and a
    /// class's level lives on the pawn's magic or might component (TorannMagic.CompAbilityUserMagic.MagicUserLevel,
    /// CompAbilityUserMight.MightUserLevel). Without the mod there are no such traits or components, and nothing here
    /// finds anything.
    /// </summary>
    public static class MagicClasses
    {
        private static readonly Dictionary<System.Type, PropertyInfo> levelProps = new Dictionary<System.Type, PropertyInfo>();

        /// <summary>Their class level (spellcasting or combat), or -1 if they have none we can read.</summary>
        public static int Level(Pawn p, bool might)
        {
            if (!(p is ThingWithComps twc) || twc.AllComps == null) return -1;
            string compName = might ? "CompAbilityUserMight" : "CompAbilityUserMagic";
            string propName = might ? "MightUserLevel" : "MagicUserLevel";
            foreach (var c in twc.AllComps)
            {
                if (c == null) continue;
                var type = c.GetType();
                if (type.Name != compName) continue;
                if (!levelProps.TryGetValue(type, out var prop))
                {
                    prop = type.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null && prop.PropertyType != typeof(int)) prop = null;
                    levelProps[type] = prop;
                }
                if (prop == null) return -1;
                try
                {
                    return (int)prop.GetValue(c);
                }
                catch
                {
                    return -1;
                }
            }
            return -1;
        }
    }

    /// <summary>
    /// Life goal: grow in one's spellcasting class (A RimWorld of Magic), <see cref="AspirationDef.amount"/> levels
    /// beyond where they stand. Who can have it is up to the def (its who tags, e.g. mage).
    /// </summary>
    public class AspirationWorker_MagicLevel : AspirationWorker
    {
        protected virtual bool Might => false;

        public override bool CanHave(Pawn p) => MagicClasses.Level(p, Might) >= 0;

        public override bool Init(Aspiration a)
        {
            int level = MagicClasses.Level(a.pawn, Might);
            if (level < 0) return false;
            a.baseline = level;
            a.target = level + Mathf.Max(1f, def.amount);
            return true;
        }

        public override float Progress(Aspiration a)
        {
            int level = MagicClasses.Level(a.pawn, Might);
            if (level < 0) return 0f;
            return (level - a.baseline) / Mathf.Max(1f, a.target - a.baseline);
        }

        public override bool StillPossible(Aspiration a) => MagicClasses.Level(a.pawn, Might) >= 0;

        public override string ProgressText(Aspiration a) =>
            "CC_PM_Asp_ClassProgress".Translate(Mathf.Max(0, MagicClasses.Level(a.pawn, Might)), a.target.ToString("0")).Resolve();
    }

    /// <summary>The same for the combat classes (their might level).</summary>
    public class AspirationWorker_MightLevel : AspirationWorker_MagicLevel
    {
        protected override bool Might => true;
    }
}
