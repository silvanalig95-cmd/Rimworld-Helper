using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // What the world's peoples remember of the colony reaches the camps that come and the strangers met at home.
    public static partial class Interplay
    {
        /// <summary>The colony's good turns that count as a rescue (a prisoner freed, someone tended, refugees taken in...).</summary>
        private static readonly HashSet<string> Rescues = new HashSet<string> { "released", "tended", "hardship", "refugees", "dignitary", "campHelped" };

        /// <summary>
        /// How likely a people is to be the one that makes camp near the colony (CampTracker.Prepare): a people the colony
        /// did good turns for comes up to twice as often, one holding grudges less, one whose camp the colony drove off or
        /// harmed lately hardly at all, and one a colonist has sworn vengeance on only half as often.
        /// </summary>
        public static float CampWeight(Faction f)
        {
            if (!On || f == null) return 1f;
            float w = 1f;
            try
            {
                var mem = FactionMemory.On ? FactionMemory.Instance : null;
                if (mem != null)
                {
                    float favour = mem.Sum(f, DeedSide.Favor);
                    float grudge = mem.Sum(f, DeedSide.Wrong, true);
                    w *= (1f + Mathf.Min(favour, 40f) / 40f) / (1f + Mathf.Min(grudge, 40f) / 20f);
                    if (mem.Best(f, DeedSide.Wrong, 1f, 0f, false, d => (d.kind == "campDriven" || d.kind == "campHarmed") && Now - d.tick < Day * 60) != null) w *= 0.15f;
                }
                var comp = Comp;
                if (S.vendettas && comp?.vendettas?.vendettas != null)
                    foreach (var v in comp.vendettas.vendettas)
                        if (v != null && v.Active && v.faction == f)
                        {
                            w *= 0.5f;
                            break;
                        }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("interplay: camp weight", e);
                return 1f;
            }
            return Mathf.Clamp(w, 0.05f, 2f);
        }

        /// <summary>
        /// What a stranger's people remember, as tags for the lines of encounters, events and dilemmas (LetterTags):
        /// their:grateful (the colony did them good turns), their:rescued (one of them a rescue), their:grudge (the colony
        /// wronged them and it isn't forgotten), their:owed (they did the colony a good turn it hasn't returned).
        /// </summary>
        public static void AddMemoryTags(HashSet<string> tags, Faction f)
        {
            if (!On || tags == null || f == null || f.IsPlayer || !FactionMemory.On || !FactionMemory.Remembers(f)) return;
            try
            {
                var mem = FactionMemory.Instance;
                if (mem == null || mem.deeds.Count == 0) return;
                float favour = mem.Sum(f, DeedSide.Favor);
                float grudge = mem.Sum(f, DeedSide.Wrong, true);
                if (favour >= 8f && favour >= grudge) tags.Add("their:grateful");
                if (mem.Best(f, DeedSide.Favor, 4f, 0f, false, d => Rescues.Contains(d.kind)) != null) tags.Add("their:rescued");
                if (grudge >= 8f && grudge > favour) tags.Add("their:grudge");
                if (mem.Sum(f, DeedSide.TheirFavor, true) >= 8f) tags.Add("their:owed");
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("interplay: memory tags", e);
            }
        }
    }
}
