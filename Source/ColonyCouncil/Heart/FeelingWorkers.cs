using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // What each feeling sees beyond mood thoughts. Each adds an amount (about the size of a mood thought: 2 is a small
    // thing, 8 a big one) with a cause the Mood tab shows. They run once an hour per colonist, so they stay cheap: no
    // map scans beyond what HeartMapContext already worked out for the hour.

    /// <summary>Danger here and now: raiders on the map, being downed, pain, a recent wound.</summary>
    public class FeelingWorker_Fear : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            int now = Find.TickManager.TicksGame;
            bool here = map.map != null && p.Spawned && p.Map == map.map;
            if (here && map.threat) sink.Add(p.Drafted ? 8f : 6f, "CC_Heart_Cause_Hostiles".Translate());
            if (p.Downed) sink.Add(8f, "CC_Heart_Cause_Downed".Translate());
            float pain = p.health?.hediffSet?.PainTotal ?? 0f;
            if (pain >= 0.15f) sink.Add(pain * 20f, "CC_Heart_Cause_Pain".Translate());
            var mind = p.mindState;
            if (mind != null && !map.threat && now - mind.lastHarmTick < GenDate.TicksPerDay) sink.Add(3f, "CC_Heart_Cause_Hurt".Translate());
            if (here && !map.threat && map.ticksSinceBigThreat < GenDate.TicksPerDay) sink.Add(3f, "CC_Heart_Cause_AfterRaid".Translate());
        }
    }

    /// <summary>What the colony knows is coming: promised fights, debts, bad times in the region, a lean winter.</summary>
    public class FeelingWorker_Dread : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            foreach (var (cause, amount) in map.dread) sink.Add(amount, cause);
            var nemeses = CouncilGameComponent.Instance?.nemeses;
            if (nemeses != null && ColonyCouncilMod.Settings.nemeses)
            {
                var n = nemeses.Of(p).FirstOrDefault(x => !x.ended && !x.captured);
                if (n != null) sink.Add(3f, "CC_Heart_Cause_Nemesis".Translate(n.name ?? n.pawn?.LabelShort ?? "?"));
            }
        }
    }

    /// <summary>A grudge against the council and a rival close by keep anger simmering.</summary>
    public class FeelingWorker_Anger : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            if (ColonyCouncilMod.Settings.enableWishes && comp.standings.LevelOf(p) == TrustLevel.Bitter) sink.Add(4f, "CC_Heart_Cause_Grudge".Translate());
            if (!ColonyCouncilMod.Settings.socialWeb || !p.Spawned) return;
            foreach (var t in comp.social.ties)
            {
                if (t.kind != TieKind.Rivals || !t.announced || !t.Involves(p)) continue;
                var other = t.Other(p);
                if (other == null || other.Dead || !other.Spawned || other.Map != p.Map) continue;
                sink.Add(Mathf.Clamp(t.strength / 20f, 1f, 4f), "CC_Heart_Cause_Rival".Translate(other.LabelShort), other);
            }
        }
    }

    /// <summary>A mental break that makes them wince afterwards (a binge, a tantrum, an insulting spree), for two days.</summary>
    public class FeelingWorker_Shame : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            if (heart.lastBreak.NullOrEmpty() || p.InMentalState) return;
            int ago = Find.TickManager.TicksGame - heart.lastBreakTick;
            if (ago < 0 || ago > GenDate.TicksPerDay * 2) return;
            sink.Add(Mathf.Lerp(8f, 3f, ago / (float)(GenDate.TicksPerDay * 2)), "CC_Heart_Cause_Break".Translate(heart.lastBreak));
        }
    }

    /// <summary>A filthy home.</summary>
    public class FeelingWorker_Disgust : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            if (map.home && p.Spawned && p.Map == map.map && map.filth > 60) sink.Add(Mathf.Min(4f, map.filth / 40f), "CC_Heart_Cause_Filth".Translate());
        }
    }

    /// <summary>Nobody here they'd call a friend, or nobody here at all.</summary>
    public class FeelingWorker_Loneliness : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            if (p.relations == null) return;
            var others = HeartNeeds.Companions(p);
            if (others.Count == 0)
            {
                sink.Add(7f, "CC_Heart_Cause_Alone".Translate());
                return;
            }
            int friends = 0;
            foreach (var o in others)
                if (p.relations.OpinionOf(o) >= 20) friends++;
            if (friends == 0) sink.Add(5f, "CC_Heart_Cause_NoFriends".Translate());
        }
    }

    /// <summary>New here, or with family somewhere else; it fades as they come to belong.</summary>
    public class FeelingWorker_Homesick : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            float belonging = heart.needs != null && heart.needs.Length > 0 ? heart.needs[(int)HeartNeed.Belonging].value : 50f;
            float fade = 1f - 0.8f * Mathf.Clamp01(belonging / 100f);
            int asColonist = p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 999999;
            float days = asColonist / (float)GenDate.TicksPerDay;
            if (days < 15f) sink.Add(8f * (1f - days / 15f) * fade, "CC_Heart_Cause_NewHere".Translate());
            if (p.relations == null) return;
            int away = 0;
            foreach (var r in p.relations.DirectRelations)
            {
                var o = r.otherPawn;
                if (o == null || o.Dead || o.Faction == Faction.OfPlayer || o.Faction == null || !o.RaceProps.Humanlike) continue;
                if (r.def != PawnRelationDefOf.Parent && r.def != PawnRelationDefOf.Child && r.def != PawnRelationDefOf.Sibling && r.def != PawnRelationDefOf.Spouse) continue;
                away++;
            }
            if (away > 0) sink.Add(Mathf.Min(4f, 2f * away) * fade, "CC_Heart_Cause_FamilyAway".Translate());
        }
    }

    /// <summary>Kept from the work they love.</summary>
    public class FeelingWorker_Frustration : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            if (p.skills == null || p.workSettings == null || !p.workSettings.EverWork) return;
            foreach (var wt in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (wt.relevantSkills.NullOrEmpty() || p.WorkTypeIsDisabled(wt) || p.workSettings.WorkIsActive(wt)) continue;
                foreach (var sk in wt.relevantSkills)
                {
                    var rec = p.skills.GetSkill(sk);
                    if (rec == null || !People.Burning(rec)) continue;
                    sink.Add(3f, "CC_Heart_Cause_PassionOff".Translate((wt.gerundLabel ?? wt.label ?? "").ToLower()));
                    return;
                }
            }
        }
    }

    /// <summary>Long low spirits, loss after loss, a colony that's losing, strain at breaking.</summary>
    public class FeelingWorker_Despair : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            if (heart.lowMoodHours >= 8f) sink.Add(Mathf.Min(8f, heart.lowMoodHours / 4f), "CC_Heart_Cause_LongLow".Translate());
            float losses = heart.Recent(StrainSource.Loss);
            if (losses >= 8f) sink.Add(Mathf.Min(6f, losses / 3f), "CC_Heart_Cause_Losses".Translate());
            if (map.home && map.foodDays < 2f) sink.Add(3f, "CC_Heart_Cause_Starving".Translate());
            if (heart.strain >= 75f) sink.Add(4f, "CC_Heart_Cause_Breaking".Translate());
        }
    }

    /// <summary>An inspiration lifts them.</summary>
    public class FeelingWorker_Joy : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            if (p.mindState?.inspirationHandler?.Inspired ?? false) sink.Add(4f, "CC_Heart_Cause_Inspired".Translate());
        }
    }

    /// <summary>A seat on the council, a name the colony gave them, a title, a life goal nearly reached.</summary>
    public class FeelingWorker_Pride : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return;
            var office = ColonyCouncilMod.Settings.offices ? comp.offices.OfficeOf(p) : null;
            if (office != null) sink.Add(3f, "CC_Heart_Cause_Office".Translate(office.label));
            if (ColonyCouncilMod.Settings.epithets && comp.epithets.Of(p).Any()) sink.Add(2f, "CC_Heart_Cause_Epithet".Translate());
            var title = p.royalty?.MostSeniorTitle;
            if (title != null) sink.Add(2f, "CC_Heart_Cause_Title".Translate(title.Label));
            var goal = ColonyCouncilMod.Settings.aspirations ? comp.aspirations.Active(p) : null;
            if (goal != null && goal.Progress >= 0.75f) sink.Add(2f, "CC_Heart_Cause_GoalNear".Translate());
        }
    }

    /// <summary>A partner and children close by.</summary>
    public class FeelingWorker_Love : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            if (p.relations == null) return;
            var partner = LovePartnerRelationUtility.ExistingLovePartner(p, false);
            if (partner != null && Near(p, partner)) sink.Add(4f, "CC_Heart_Cause_Partner".Translate(partner.LabelShort), partner);
            foreach (var r in p.relations.DirectRelations)
            {
                if (r.def != PawnRelationDefOf.Child || r.otherPawn == null || r.otherPawn.Dead || !Near(p, r.otherPawn)) continue;
                sink.Add(2f, "CC_Heart_Cause_Children".Translate());
                break;
            }
        }

        private static bool Near(Pawn p, Pawn other) =>
            other.Faction == Faction.OfPlayer && (p.Spawned && other.Spawned && other.Map == p.Map || p.GetCaravan() != null && p.GetCaravan() == other.GetCaravan());
    }

    /// <summary>Spring, a new face, a wound healing, strain lifting, a new land.</summary>
    public class FeelingWorker_Hope : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            if (map.map != null && map.season == Season.Spring) sink.Add(2f, "CC_Heart_Cause_Spring".Translate());
            int asColonist = p.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 999999;
            if (map.newcomers > 0 && asColonist >= GenDate.TicksPerDay * 3) sink.Add(3f, "CC_Heart_Cause_Newcomer".Translate());
            if (!p.Downed && (p.health?.hediffSet?.HasTendedAndHealingInjury() ?? false)) sink.Add(3f, "CC_Heart_Cause_Healing".Translate());
            if (heart.strainDayAgoTick >= 0 && heart.strainDayAgo - heart.strain >= 10f) sink.Add(3f, "CC_Heart_Cause_Lifting".Translate());
            if (map.home && map.colonyDays < 10) sink.Add(2f, "CC_Heart_Cause_NewStart".Translate());
        }
    }

    /// <summary>A comet in the sky (Rimfolk's), on top of the auroras, eclipses and art the thoughts already bring.</summary>
    public class FeelingWorker_Awe : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            if (map.comet && p.Spawned && p.Map == map.map) sink.Add(5f, "CC_Heart_Cause_Comet".Translate());
        }
    }

    /// <summary>Quiet days: no danger, needs met, a decent mood. The calm baseline.</summary>
    public class FeelingWorker_Contentment : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            var needs = p.needs;
            if (needs?.mood == null || p.Downed || p.InMentalState) return;
            if (map.threat || map.ticksSinceBigThreat < GenDate.TicksPerDay * 3) return;
            float mood = needs.mood.CurLevelPercentage;
            if (mood < 0.55f || heart.strain >= 55f) return;
            if (needs.food != null && needs.food.CurLevelPercentage < 0.3f) return;
            if (needs.rest != null && needs.rest.CurLevelPercentage < 0.3f) return;
            if (needs.joy != null && needs.joy.CurLevelPercentage < 0.3f) return;
            sink.Add(3f + 8f * Mathf.Clamp01((mood - 0.55f) / 0.4f), "CC_Heart_Cause_QuietDays".Translate());
        }
    }

    /// <summary>Research, a passion for thinking, strangers to talk to, new land.</summary>
    public class FeelingWorker_Curiosity : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            if (p.CurJobDef == JobDefOf.Research) sink.Add(4f, "CC_Heart_Cause_Research".Translate());
            var intellect = p.skills?.GetSkill(SkillDefOf.Intellectual);
            if (intellect != null && People.Passionate(intellect) && Find.ResearchManager?.GetProject() != null) sink.Add(2f, "CC_Heart_Cause_Project".Translate());
            if (map.visitors && p.Spawned && p.Map == map.map) sink.Add(2f, "CC_Heart_Cause_Visitors".Translate());
            if (map.home && map.colonyDays < 10) sink.Add(2f, "CC_Heart_Cause_NewLand".Translate());
        }
    }

    /// <summary>The same work day after day, and too little fun.</summary>
    public class FeelingWorker_Restless : FeelingWorker
    {
        public override void Feed(Pawn p, PawnHeart heart, HeartMapContext map, IFeelingSink sink)
        {
            int variety = HeartLogic.WorkVariety(heart, out int working);
            if (working >= 6 && variety <= 1) sink.Add(4f, "CC_Heart_Cause_SameWork".Translate());
            var joy = p.needs?.joy;
            if (joy != null && joy.CurLevelPercentage < 0.25f) sink.Add(3f, "CC_Heart_Cause_Bored".Translate());
        }
    }
}
