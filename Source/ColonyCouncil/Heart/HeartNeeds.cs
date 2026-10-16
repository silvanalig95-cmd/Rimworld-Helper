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
    /// travel, few restrictions). The values are never saved and give no mood of their own: they show what a life is
    /// missing, nudge wishes and life goals toward the one lacked most, deepen the feeling that goes with a low one,
    /// and when all four are well met, strain drains a little faster. Only how long each has been badly lacking is
    /// saved (PawnHeart.needLack): after about three days below 25, it adds a little strain (HeartNeedMath).
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

        /// <summary>
        /// Gathers what moved a need this hour (for the tooltips: what raised it, what held it down, what would help) and
        /// picks the reason that matters most: the biggest shortfall if it's low, else the biggest help.
        /// </summary>
        private sealed class Why
        {
            public string low, high;
            public float lowAmount, highAmount;
            private readonly List<NeedPart> parts = new List<NeedPart>(8);

            public Why Start()
            {
                low = high = null;
                lowAmount = highAmount = 0f;
                parts.Clear();
                return this;
            }

            /// <summary>
            /// One thing that moved the need (or, with <paramref name="counted"/> false, a note on what's missing that only
            /// explains a low value). <paramref name="help"/>: a Keyed string saying what would help, when it holds it down.
            /// </summary>
            public void Add(float amount, string text, string help = null, bool counted = true, string helpArg = null)
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
                if (text.NullOrEmpty() || Mathf.Abs(amount) < 0.5f) return;
                parts.Add(new NeedPart { amount = amount, text = text, counted = counted, help = amount < 0f ? help : null, helpArg = helpArg });
            }

            public NeedNow Result(float v)
            {
                parts.Sort((a, b) => Mathf.Abs(b.amount).CompareTo(Mathf.Abs(a.amount)));
                return new NeedNow { value = v, why = v < 50f ? low ?? high : high ?? low, parts = parts.ToArray() };
            }
        }

        private static readonly Why why = new Why();

        private static NeedNow Belonging(Pawn p, PawnHeart h)
        {
            var why = HeartNeeds.why.Start();
            float v = 15f;
            int friends = FriendsHere(p, out int others);
            if (others == 0) why.Add(-30f, "CC_Heart_Why_Alone".Translate(), "CC_Heart_NeedHelp_Company", false);
            else if (friends == 0) why.Add(-20f, "CC_Heart_Why_NoFriends".Translate(), "CC_Heart_NeedHelp_Company", false);
            float f = Mathf.Min(3, friends) * 15f;
            v += f;
            if (friends > 0) why.Add(f, friends == 1 ? "CC_Heart_Why_OneFriend".Translate() : "CC_Heart_Why_Friends".Translate(friends));
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
                    var kind = SocialKindOf(mem.def);
                    if (kind == SocialMemory.LeftOut) leftOut = true;
                    else if (kind == SocialMemory.Good && mem.age < GenDate.TicksPerDay * 2 && HeartLogic.FeltMood(mem) > 0f) good++;
                }
            if (good > 0)
            {
                v += Mathf.Min(12f, good * 3f);
                why.Add(Mathf.Min(12f, good * 3f), "CC_Heart_Why_GoodMoments".Translate());
            }
            if (leftOut)
            {
                v -= 15f;
                why.Add(-15f, "CC_Heart_Why_LeftOut".Translate(), "CC_Heart_NeedHelp_Included");
            }
            return why.Result(v);
        }

        private enum SocialMemory : byte
        {
            None,
            Good,
            LeftOut
        }

        /// <summary>What a memory says about belonging, worked out once per thought (not by name every hour).</summary>
        private static readonly Dictionary<ThoughtDef, SocialMemory> socialKinds = new Dictionary<ThoughtDef, SocialMemory>();

        private static SocialMemory SocialKindOf(ThoughtDef def)
        {
            if (socialKinds.TryGetValue(def, out var kind)) return kind;
            string n = def.defName ?? "";
            kind = n == "Council_Soc_LeftOut" ? SocialMemory.LeftOut
                : n == "DeepTalk" || n == "KindWordsMood" || n.StartsWith("Council_Moment_", StringComparison.Ordinal) || n.StartsWith("Council_Soc_", StringComparison.Ordinal)
                    ? SocialMemory.Good
                    : SocialMemory.None;
            socialKinds[def] = kind;
            return kind;
        }

        private static int friendsTick = -1;
        private static Pawn friendsOf;
        private static int friendsCount, othersCount;

        /// <summary>
        /// How many of the colony's people with them now they'd call a friend (opinion 20 or more), and how many are with
        /// them at all. Worked out once per colonist per hour: belonging and loneliness both ask.
        /// </summary>
        public static int FriendsHere(Pawn p, out int others)
        {
            int now = Find.TickManager.TicksGame;
            if (friendsOf == p && friendsTick == now)
            {
                others = othersCount;
                return friendsCount;
            }
            var list = Companions(p);
            int friends = 0;
            if (p.relations != null)
                foreach (var o in list)
                    if (p.relations.OpinionOf(o) >= 20) friends++;
            friendsOf = p;
            friendsTick = now;
            friendsCount = friends;
            othersCount = others = list.Count;
            return friends;
        }

        private static NeedNow Purpose(Pawn p, PawnHeart h)
        {
            var why = HeartNeeds.why.Start();
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
            else if (passions) why.Add(-15f, "CC_Heart_Why_PassionOff".Translate(), "CC_Heart_NeedHelp_Passion", false);
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
                else if (ColonyCouncilMod.Settings.aspirations) why.Add(-5f, "CC_Heart_Why_NoGoal".Translate(), "CC_Heart_NeedHelp_Goal", false);
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
            var why = HeartNeeds.why.Start();
            float v = 55f;
            if (m.map == null)
            {
                why.Add(-5f, "CC_Heart_Why_OnTheRoad".Translate());
                return why.Result(v - 5f);
            }
            if (m.threat)
            {
                v -= 35f;
                why.Add(-35f, "CC_Heart_Why_Raiders".Translate(), "CC_Heart_Help_Peace");
            }
            else if (m.ticksSinceBigThreat < GenDate.TicksPerDay * 3)
            {
                v -= 15f;
                why.Add(-15f, "CC_Heart_Why_RaidedLately".Translate(), "CC_Heart_Help_Peace");
            }
            if ((p.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.75f)
            {
                v -= 15f;
                why.Add(-15f, "CC_Heart_Why_Hurt".Translate(), "CC_Heart_Help_Healing");
            }
            if (m.home)
            {
                if (m.foodDays < 4f)
                {
                    v -= 20f;
                    why.Add(-20f, "CC_Heart_Why_FoodLow".Translate(), "CC_Heart_NeedHelp_Food");
                }
                else if (m.foodDays < 10f)
                {
                    v -= 8f;
                    why.Add(-8f, "CC_Heart_Why_FoodShort".Translate(), "CC_Heart_NeedHelp_Food");
                }
                else if (m.foodDays >= 30f)
                {
                    v += 10f;
                    why.Add(10f, "CC_Heart_Why_FoodPlenty".Translate());
                }
                if (m.medicine == 0)
                {
                    v -= 10f;
                    why.Add(-10f, "CC_Heart_Why_NoMedicine".Translate(), "CC_Heart_NeedHelp_Medicine");
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
                    why.Add(-10f, "CC_Heart_Why_Undefended".Translate(), "CC_Heart_NeedHelp_Defenses");
                }
            }
            return why.Result(v);
        }

        private static NeedNow Freedom(Pawn p, PawnHeart h)
        {
            var why = HeartNeeds.why.Start();
            float v = 40f;
            var needs = p.needs;
            if (needs?.outdoors != null)
            {
                float o = (needs.outdoors.CurLevelPercentage - 0.5f) * 30f;
                v += o;
                why.Add(o, o >= 0f ? "CC_Heart_Why_Outdoors".Translate() : "CC_Heart_Why_Indoors".Translate(), "CC_Heart_NeedHelp_Outdoors");
            }
            if (needs?.joy != null)
            {
                float j = (needs.joy.CurLevelPercentage - 0.5f) * 30f;
                v += j;
                why.Add(j, j >= 0f ? "CC_Heart_Why_FreeTime".Translate() : "CC_Heart_Why_NoFreeTime".Translate(), "CC_Heart_Help_Recreation");
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
                why.Add(-10f, "CC_Heart_Why_SameWork".Translate(), "CC_Heart_NeedHelp_Variety");
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
                    why.Add(-10f, "CC_Heart_Why_Decree".Translate(a.def.label), "CC_Heart_NeedHelp_Decree", true, a.def.label);
                }
            if (p.Spawned && p.playerSettings?.AreaRestrictionInPawnCurrentMap != null)
            {
                v -= 8f;
                why.Add(-8f, "CC_Heart_Why_Restricted".Translate(), "CC_Heart_NeedHelp_Area");
            }
            if (p.IsSlave)
            {
                v -= 30f;
                why.Add(-30f, "CC_Heart_Why_Slave".Translate(), "CC_Heart_NeedHelp_Slave");
            }
            return why.Result(v);
        }
    }
}
