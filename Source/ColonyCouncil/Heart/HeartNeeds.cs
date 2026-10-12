using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The heart's four long-term needs, each 0 to 100, worked out once an hour from what the colonist has: belonging
    /// (friends, a partner, family, good moments, not being left out), purpose (work they love, a life goal, a seat,
    /// learning), safety (no danger, food, medicine, defenses, their own health) and freedom (time outside, varied work,
    /// travel, few restrictions). Never saved and no mood of their own: they show what a life is missing, nudge wishes
    /// and life goals toward it, and when all four are well met, strain drains a little faster.
    /// </summary>
    public static class HeartNeeds
    {
        private static readonly List<Pawn> companions = new List<Pawn>();

        /// <summary>The colony's other people with them now: on the same map, or in the same caravan.</summary>
        public static List<Pawn> Companions(Pawn p)
        {
            companions.Clear();
            if (p.Spawned)
            {
                foreach (var o in p.Map.mapPawns.FreeColonistsSpawned)
                    if (o != p && !o.Dead) companions.Add(o);
            }
            else
            {
                var caravan = p.GetCaravan();
                if (caravan != null)
                    foreach (var o in caravan.PawnsListForReading)
                        if (o != p && o.IsColonist && o.RaceProps.Humanlike) companions.Add(o);
            }
            return companions;
        }

        public static void Compute(Pawn p, PawnHeart h, HeartMapContext m)
        {
            if (h.needs == null || h.needs.Length != 4) h.needs = new NeedNow[4];
            h.needs[(int)HeartNeed.Belonging] = Safe(() => Belonging(p, h));
            h.needs[(int)HeartNeed.Purpose] = Safe(() => Purpose(p, h));
            h.needs[(int)HeartNeed.Safety] = Safe(() => Safety(p, m));
            h.needs[(int)HeartNeed.Freedom] = Safe(() => Freedom(p, h));
        }

        private static NeedNow Safe(Func<NeedNow> f)
        {
            try
            {
                var n = f();
                n.value = Mathf.Clamp(n.value, 0f, 100f);
                return n;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("heart needs", e);
                return new NeedNow { value = 50f };
            }
        }

        /// <summary>Picks the reason that matters most: the biggest shortfall if it's low, else the biggest help.</summary>
        private struct Why
        {
            public string low, high;
            public float lowAmount, highAmount;

            public void Add(float amount, string text)
            {
                if (amount < 0f && -amount > lowAmount)
                {
                    lowAmount = -amount;
                    low = text;
                }
                else if (amount > 0f && amount > highAmount)
                {
                    highAmount = amount;
                    high = text;
                }
            }

            public NeedNow Result(float v) => new NeedNow { value = v, why = v < 50f ? low ?? high : high ?? low };
        }

        private static NeedNow Belonging(Pawn p, PawnHeart h)
        {
            var why = new Why();
            float v = 15f;
            var others = Companions(p);
            int friends = 0;
            if (p.relations != null)
                foreach (var o in others)
                    if (p.relations.OpinionOf(o) >= 20) friends++;
            if (others.Count == 0) why.Add(-30f, "CC_Heart_Why_Alone".Translate());
            else if (friends == 0) why.Add(-20f, "CC_Heart_Why_NoFriends".Translate());
            float f = Mathf.Min(3, friends) * 15f;
            v += f;
            if (friends > 0) why.Add(f, "CC_Heart_Why_Friends".Translate(friends));
            var partner = p.relations != null ? LovePartnerRelationUtility.ExistingLovePartner(p, false) : null;
            if (partner != null && partner.Faction == Faction.OfPlayer)
            {
                v += 15f;
                why.Add(15f, "CC_Heart_Why_Partner".Translate(partner.LabelShort));
            }
            if (p.relations != null)
                foreach (var o in p.relations.FamilyByBlood)
                    if (o != null && o != p && !o.Dead && o.Faction == Faction.OfPlayer)
                    {
                        v += 8f;
                        why.Add(8f, "CC_Heart_Why_Family".Translate());
                        break;
                    }
            var comp = CouncilGameComponent.Instance;
            if (comp != null && ColonyCouncilMod.Settings.socialWeb && comp.social.CliqueOf(p) != null)
            {
                v += 10f;
                why.Add(10f, "CC_Heart_Why_Circle".Translate());
            }
            int good = 0;
            bool leftOut = false;
            var mems = p.needs?.mood?.thoughts?.memories?.Memories;
            if (mems != null)
                foreach (var mem in mems)
                {
                    if (mem?.def == null) continue;
                    string n = mem.def.defName;
                    if (n == "Council_Soc_LeftOut") leftOut = true;
                    else if (mem.age < GenDate.TicksPerDay * 2 && (n == "DeepTalk" || n == "KindWordsMood" || n.StartsWith("Council_Moment_", StringComparison.Ordinal)
                                                                    || n.StartsWith("Council_Soc_", StringComparison.Ordinal)) && mem.MoodOffset() > 0f)
                        good++;
                }
            if (good > 0)
            {
                v += Mathf.Min(12f, good * 3f);
                why.Add(Mathf.Min(12f, good * 3f), "CC_Heart_Why_GoodMoments".Translate());
            }
            if (leftOut)
            {
                v -= 15f;
                why.Add(-15f, "CC_Heart_Why_LeftOut".Translate());
            }
            return why.Result(v);
        }

        private static NeedNow Purpose(Pawn p, PawnHeart h)
        {
            var why = new Why();
            float v = 25f;
            bool passions = false, passionOn = false, passionNow = false;
            var job = p.CurJob?.workGiverDef?.workType;
            if (p.skills != null && p.workSettings != null && p.workSettings.EverWork)
                foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                {
                    if (wt.relevantSkills.NullOrEmpty() || p.WorkTypeIsDisabled(wt)) continue;
                    foreach (var sk in wt.relevantSkills)
                    {
                        var rec = p.skills.GetSkill(sk);
                        if (rec == null || !People.Passionate(rec)) continue;
                        passions = true;
                        if (p.workSettings.WorkIsActive(wt)) passionOn = true;
                        if (job == wt) passionNow = true;
                    }
                }
            if (passionOn)
            {
                v += 20f;
                why.Add(20f, "CC_Heart_Why_PassionWork".Translate());
            }
            else if (passions) why.Add(-15f, "CC_Heart_Why_PassionOff".Translate());
            if (passionNow)
            {
                v += 10f;
                why.Add(10f, "CC_Heart_Why_DoingPassion".Translate());
            }
            var comp = CouncilGameComponent.Instance;
            if (comp != null)
            {
                var goal = ColonyCouncilMod.Settings.aspirations ? comp.aspirations.Active(p) : null;
                if (goal != null)
                {
                    float g = 10f + 20f * goal.Progress;
                    v += g;
                    why.Add(g, "CC_Heart_Why_Goal".Translate());
                }
                else why.Add(-5f, "CC_Heart_Why_NoGoal".Translate());
                if (ColonyCouncilMod.Settings.offices && comp.offices.OfficeOf(p) != null)
                {
                    v += 15f;
                    why.Add(15f, "CC_Heart_Why_Office".Translate());
                }
            }
            float xp = 0f;
            if (p.skills != null)
                foreach (var s in p.skills.skills)
                    xp += s.xpSinceMidnight;
            if (xp >= 300f)
            {
                v += 10f;
                why.Add(10f, "CC_Heart_Why_Learning".Translate());
            }
            return why.Result(v);
        }

        private static NeedNow Safety(Pawn p, HeartMapContext m)
        {
            var why = new Why();
            float v = 55f;
            if (m.map == null)
            {
                why.Add(-5f, "CC_Heart_Why_OnTheRoad".Translate());
                return why.Result(v - 5f);
            }
            if (m.threat)
            {
                v -= 35f;
                why.Add(-35f, "CC_Heart_Why_Raiders".Translate());
            }
            else if (m.ticksSinceBigThreat < GenDate.TicksPerDay * 3)
            {
                v -= 15f;
                why.Add(-15f, "CC_Heart_Why_RaidedLately".Translate());
            }
            if ((p.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.75f)
            {
                v -= 15f;
                why.Add(-15f, "CC_Heart_Why_Hurt".Translate());
            }
            if (m.home)
            {
                if (m.foodDays < 4f)
                {
                    v -= 20f;
                    why.Add(-20f, "CC_Heart_Why_FoodLow".Translate());
                }
                else if (m.foodDays < 10f)
                {
                    v -= 8f;
                    why.Add(-8f, "CC_Heart_Why_FoodShort".Translate());
                }
                else if (m.foodDays >= 30f)
                {
                    v += 10f;
                    why.Add(10f, "CC_Heart_Why_FoodPlenty".Translate());
                }
                if (m.medicine == 0)
                {
                    v -= 10f;
                    why.Add(-10f, "CC_Heart_Why_NoMedicine".Translate());
                }
                else if (m.medicine >= m.colonists)
                {
                    v += 10f;
                    why.Add(10f, "CC_Heart_Why_Medicine".Translate());
                }
                if (m.defense >= 1.2f)
                {
                    v += 15f;
                    why.Add(15f, "CC_Heart_Why_Defended".Translate());
                }
                else if (m.defense < 0.5f && m.colonyDays > 10)
                {
                    v -= 10f;
                    why.Add(-10f, "CC_Heart_Why_Undefended".Translate());
                }
            }
            return why.Result(v);
        }

        private static NeedNow Freedom(Pawn p, PawnHeart h)
        {
            var why = new Why();
            float v = 40f;
            var needs = p.needs;
            if (needs?.outdoors != null)
            {
                float o = (needs.outdoors.CurLevelPercentage - 0.5f) * 30f;
                v += o;
                why.Add(o, o >= 0f ? "CC_Heart_Why_Outdoors".Translate() : "CC_Heart_Why_Indoors".Translate());
            }
            if (needs?.joy != null)
            {
                float j = (needs.joy.CurLevelPercentage - 0.5f) * 30f;
                v += j;
                why.Add(j, j >= 0f ? "CC_Heart_Why_FreeTime".Translate() : "CC_Heart_Why_NoFreeTime".Translate());
            }
            int variety = HeartLogic.WorkVariety(h, out int working);
            if (working >= 4 && variety >= 3)
            {
                v += 12f;
                why.Add(12f, "CC_Heart_Why_VariedWork".Translate());
            }
            else if (working >= 6 && variety <= 1)
            {
                v -= 10f;
                why.Add(-10f, "CC_Heart_Why_SameWork".Translate());
            }
            if (p.GetCaravan() != null)
            {
                v += 20f;
                why.Add(20f, "CC_Heart_Why_Traveling".Translate());
            }
            var comp = CouncilGameComponent.Instance;
            if (comp?.decrees?.active != null && p.IsFreeColonist)
                foreach (var a in comp.decrees.active)
                {
                    if (a?.def == null || a.def.ReactionOf(p) > DecreeReaction.Dislike) continue;
                    v -= 10f;
                    why.Add(-10f, "CC_Heart_Why_Decree".Translate(a.def.label));
                }
            if (p.Spawned && p.playerSettings?.AreaRestrictionInPawnCurrentMap != null)
            {
                v -= 8f;
                why.Add(-8f, "CC_Heart_Why_Restricted".Translate());
            }
            if (p.IsSlave)
            {
                v -= 30f;
                why.Add(-30f, "CC_Heart_Why_Slave".Translate());
            }
            return why.Result(v);
        }
    }
}
