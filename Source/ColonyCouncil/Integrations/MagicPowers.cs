using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A RimWorld of Magic, read by name: how far someone has gone in their class's craft, counted as every power or
    /// technique they've learned plus every level they've put into one (CompAbilityUserMagic.MagicData.AllMagicPowers,
    /// CompAbilityUserMight.MightData.AllMightPowers; each power's learned and level). -1 when it can't be read, as
    /// without the mod.
    /// </summary>
    public static class MagicCraft
    {
        private static readonly Dictionary<Type, (PropertyInfo data, PropertyInfo powers)> compProps = new Dictionary<Type, (PropertyInfo, PropertyInfo)>();
        private static readonly Dictionary<Type, (FieldInfo learned, FieldInfo level)> powerFields = new Dictionary<Type, (FieldInfo, FieldInfo)>();

        public static int Mastery(Pawn p, bool might)
        {
            if (!(p is ThingWithComps twc) || twc.AllComps == null) return -1;
            string compName = might ? "CompAbilityUserMight" : "CompAbilityUserMagic";
            foreach (var c in twc.AllComps)
            {
                if (c == null || c.GetType().Name != compName) continue;
                try
                {
                    var type = c.GetType();
                    if (!compProps.TryGetValue(type, out var props))
                    {
                        var data = type.GetProperty(might ? "MightData" : "MagicData", BindingFlags.Public | BindingFlags.Instance);
                        var powers = data?.PropertyType.GetProperty(might ? "AllMightPowers" : "AllMagicPowers", BindingFlags.Public | BindingFlags.Instance);
                        props = (data, powers);
                        compProps[type] = props;
                    }
                    if (props.data == null || props.powers == null) return -1;
                    var d = props.data.GetValue(c);
                    if (d == null || !(props.powers.GetValue(d) is IEnumerable list)) return -1;
                    int total = 0;
                    foreach (var power in list)
                    {
                        if (power == null) continue;
                        var pt = power.GetType();
                        if (!powerFields.TryGetValue(pt, out var f))
                        {
                            f = (pt.GetField("learned", BindingFlags.Public | BindingFlags.Instance), pt.GetField("level", BindingFlags.Public | BindingFlags.Instance));
                            if (f.learned?.FieldType != typeof(bool) || f.level?.FieldType != typeof(int)) f = (null, null);
                            powerFields[pt] = f;
                        }
                        if (f.learned == null) return -1;
                        if ((bool)f.learned.GetValue(power)) total += 1 + Mathf.Max(0, (int)f.level.GetValue(power));
                    }
                    return total;
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("magic craft", e);
                    return -1;
                }
            }
            return -1;
        }
    }

    /// <summary>
    /// Life goal: go deeper into one's craft (A RimWorld of Magic): learn new workings or improve the ones they know,
    /// <see cref="AspirationDef.amount"/> steps in all (a new power is one step, each level put into one another).
    /// </summary>
    public class AspirationWorker_MagicCraft : AspirationWorker
    {
        protected virtual bool Might => false;

        public override bool CanHave(Pawn p) => MagicCraft.Mastery(p, Might) >= 0;

        public override bool Init(Aspiration a)
        {
            int now = MagicCraft.Mastery(a.pawn, Might);
            if (now < 0) return false;
            a.baseline = now;
            a.target = now + Mathf.Max(1f, def.amount);
            return true;
        }

        public override float Progress(Aspiration a)
        {
            int now = MagicCraft.Mastery(a.pawn, Might);
            if (now < 0) return 0f;
            return (now - a.baseline) / Mathf.Max(1f, a.target - a.baseline);
        }

        public override bool StillPossible(Aspiration a) => MagicCraft.Mastery(a.pawn, Might) >= 0;

        public override string ProgressText(Aspiration a) =>
            "CC_PM_Asp_CraftProgress".Translate(Mathf.Max(0, MagicCraft.Mastery(a.pawn, Might) - Mathf.RoundToInt(a.baseline)), Mathf.RoundToInt(a.target - a.baseline)).Resolve();
    }

    /// <summary>The same for the combat classes (their techniques).</summary>
    public class AspirationWorker_MightCraft : AspirationWorker_MagicCraft
    {
        protected override bool Might => true;
    }
}
