using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // =====================================================================================================
    // People through their lives: stories of three (a rival for the same affection, a teacher with two students,
    // brothers and sisters at odds, a friend caught between two friends), stories from the camps, the offices and the
    // world's troubles, and stories for children, the young, new parents and the old. Each reads what is really there
    // (relations, the social web, a camp, an office, a world event, dreams, health) and never begins from a roll alone.
    // =====================================================================================================

    /// <summary>Helpers the stories of people's lives share.</summary>
    public static class ArcLives
    {
        /// <summary>Old enough for love in the stories (human years), and not a child.</summary>
        public static bool Grown(Pawn p, float age = 16f) => p != null && !p.DevelopmentalStage.Juvenile() && Flavor.HumanAge(p) >= age;

        /// <summary>Free to fall for someone: no lover, fiancé or spouse living.</summary>
        public static bool Unattached(Pawn p) => p?.relations != null && LovePartnerRelationUtility.ExistingLovePartner(p, false) == null;

        /// <summary>Fond enough of someone, and able to be drawn to them at all (the game's own romance factors).</summary>
        public static bool Smitten(Pawn p, Pawn o, int minOpinion)
        {
            if (p?.relations == null || o == null || p == o || ArcUtil.Kin(p, o)) return false;
            if (ArcUtil.Opinion(p, o) < minOpinion) return false;
            try
            {
                return p.relations.SecondaryRomanceChanceFactor(o) >= 0.15f;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("personal story: romance factor", e);
                return false;
            }
        }

        /// <summary>A sharp word between the two in the last few days.</summary>
        public static bool RecentFriction(Pawn a, Pawn b, float days = 5f)
        {
            int since = Find.TickManager.TicksGame - (int)(days * GenDate.TicksPerDay);
            return ArcUtil.RemembersSince(a, b, ArcUtil.Frictions, since) || ArcUtil.RemembersSince(b, a, ArcUtil.Frictions, since);
        }

        /// <summary>The two are at odds (see <see cref="ArcMath.AtOdds"/>), and not past all words.</summary>
        public static bool AtOdds(Pawn a, Pawn b)
        {
            int ab = ArcUtil.Opinion(a, b), ba = ArcUtil.Opinion(b, a);
            if (ab < -80 || ba < -80) return false;
            bool rivals = CouncilGameComponent.Instance?.social?.Get(a, b, TieKind.Rivals) != null;
            return ArcMath.AtOdds(ab, ba, rivals, RecentFriction(a, b));
        }

        /// <summary>A fight between these two since the story began: what breaks a story of making peace.</summary>
        public static bool FoughtSince(Arc arc, Pawn a, Pawn b) =>
            a != null && b != null && (ArcUtil.RemembersSince(a, b, ArcUtil.Fights, arc.startedTick) || ArcUtil.RemembersSince(b, a, ArcUtil.Fights, arc.startedTick));

        /// <summary>A parent of theirs at home, if any.</summary>
        public static Pawn Parent(Pawn p, List<Pawn> home) =>
            p?.relations?.DirectRelations.Where(r => r.def == PawnRelationDefOf.Parent && r.otherPawn != null && !r.otherPawn.Dead && home.Contains(r.otherPawn))
                .Select(r => r.otherPawn).OrderByDescending(o => ArcUtil.Opinion(p, o)).FirstOrDefault();

        /// <summary>A grown-up who is fond of them (the child's comfort when there is no parent at home).</summary>
        public static Pawn KindGrownUp(Pawn child, List<Pawn> home) =>
            home.Where(o => o != child && Grown(o, 18f) && ArcUtil.Opinion(o, child) >= 10)
                .OrderByDescending(o => ArcUtil.Opinion(o, child) + ArcUtil.Opinion(child, o) + (ArcUtil.HasTrait(o, TraitDefOf.Kind) ? 20 : 0)).FirstOrDefault();

        /// <summary>The world events that make someone fear for family out there.</summary>
        public static readonly List<string> HardTimes = new List<string> { "sickness", "hunger", "war", "drought", "water", "weather", "levy", "sellswords" };
    }

    /// <summary>
    /// A rival in love: two colonists fond of the same unattached person. The person they are both fond of decides, in
    /// the game's own way (a romance), or, if nobody has after weeks, how they feel about each of them settles it
    /// (<see cref="ArcMath.Favoured"/>): the story ends [won], [lost] or [neither].
    /// </summary>
    public class ArcWorker_Suitors : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (ArcUtil.Psychopath(p) || !ArcLives.Grown(p) || !ArcLives.Unattached(p)) return null;
            foreach (var o in home.InRandomOrder())
            {
                if (o == p || !ArcLives.Grown(o) || !ArcLives.Unattached(o) || ArcUtil.Opinion(o, p) < 0 || !ArcLives.Smitten(p, o, 35)) continue;
                // Someone else has an eye on the same person.
                var rival = home.Where(t => t != p && t != o && ArcLives.Grown(t) && ArcLives.Unattached(t) && !ArcUtil.Psychopath(t) && !ArcUtil.Partners(t, p)
                                            && ArcLives.Smitten(t, o, 35))
                    .OrderByDescending(t => ArcUtil.Opinion(t, o)).FirstOrDefault();
                if (rival == null) continue;
                return new ArcCandidate { other = o, third = rival, weight = 1.2f };
            }
            return null;
        }

        /// <summary>The one they're both fond of has chosen: a romance with one of them.</summary>
        public override bool CustomGate(Arc arc, ArcGate gate) =>
            arc.other != null && (ArcUtil.Partners(arc.other, arc.pawn) || (arc.third != null && ArcUtil.Partners(arc.other, arc.third)));

        /// <summary>They took up with someone else entirely.</summary>
        public override string Broken(Arc arc)
        {
            if (arc.other?.relations == null) return null;
            var lover = LovePartnerRelationUtility.ExistingLovePartner(arc.other, false);
            return lover != null && lover != arc.pawn && lover != arc.third ? "elsewhere" : null;
        }

        public override void Settle(Arc arc)
        {
            if (arc.stage == arc.def.LastStage - 1) arc.tag = Outcome(arc);
        }

        /// <summary>[won], [lost] or [neither]: a romance decides it, else how the one they're both fond of feels.</summary>
        public static string Outcome(Arc arc)
        {
            var o = arc.other;
            if (o == null) return "neither";
            if (ArcUtil.Partners(o, arc.pawn)) return "won";
            if (arc.third != null && ArcUtil.Partners(o, arc.third)) return "lost";
            int f = ArcMath.Favoured(ArcUtil.Opinion(o, arc.pawn), arc.third == null ? -100 : ArcUtil.Opinion(o, arc.third), 15);
            return f > 0 ? "won" : f < 0 ? "lost" : "neither";
        }
    }

    /// <summary>
    /// Torn between two students: a teacher with two students in the same craft (two mentorships, or two keen learners of
    /// what they do best), who both want the teacher's eye. Ends [otherahead], [thirdahead] or [even], by who came on
    /// further in the last stage.
    /// </summary>
    public class ArcWorker_TwoStudents : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (p.DevelopmentalStage.Juvenile() || p.skills == null) return null;
            var social = CouncilGameComponent.Instance?.social;
            if (social != null && ColonyCouncilMod.Settings.socialWeb)
            {
                var two = social.ties.Where(t => t.kind == TieKind.Mentor && t.a == p && t.b != null && t.skill != null && home.Contains(t.b))
                    .GroupBy(t => t.skill).FirstOrDefault(g => g.Count() >= 2)?.Take(2).ToList();
                if (two != null) return new ArcCandidate { other = two[0].b, third = two[1].b, skill = two[0].skill, weight = 1.5f, tag = "mentor" };
            }
            var skill = ArcUtil.Best(p, ArcUtil.AllSkills, def.masterLevel);
            if (skill == null) return null;
            int level = ArcUtil.Level(p, skill);
            var keen = home.Where(o => o != p && !o.DevelopmentalStage.Baby() && ArcUtil.Skill(o, skill) is SkillRecord r && !r.TotallyDisabled && People.Passionate(r)
                                       && r.Level <= level - 4 && ArcUtil.Opinion(o, p) >= 0 && ArcUtil.Opinion(p, o) >= 0)
                .OrderByDescending(o => People.PassionLevel(ArcUtil.Skill(o, skill)) * 10 + ArcUtil.Opinion(o, p)).Take(2).ToList();
            return keen.Count < 2 ? null : new ArcCandidate { other = keen[0], third = keen[1], skill = skill };
        }

        /// <summary>The two students came to blows.</summary>
        public override string Broken(Arc arc) => ArcLives.FoughtSince(arc, arc.other, arc.third) ? "fight" : null;

        public override void Settle(Arc arc)
        {
            if (arc.stage != arc.def.LastStage - 1) return;
            float other = ArcUtil.Xp(arc.other, arc.skill) - arc.baseOtherXp;
            float third = ArcUtil.Xp(arc.third, arc.skill) - arc.baseThirdXp;
            int f = ArcMath.Favoured(other, third, Mathf.Max(400f, (other + third) * 0.15f));
            arc.tag = f > 0 ? "otherahead" : f < 0 ? "thirdahead" : "even";
        }
    }

    /// <summary>
    /// Brothers and sisters at odds, and the parent between them: two of their children at home who can't get along.
    /// [young] when either is still a child, [grown] otherwise.
    /// </summary>
    public class ArcWorker_Siblings : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (p.relations == null || ArcUtil.Psychopath(p) || p.DevelopmentalStage.Juvenile()) return null;
            var kids = p.relations.Children.Where(c => c != null && !c.Dead && home.Contains(c) && !c.DevelopmentalStage.Baby()).ToList();
            if (kids.Count < 2) return null;
            Pawn x = null, y = null;
            int worst = int.MaxValue;
            for (int i = 0; i < kids.Count; i++)
                for (int j = i + 1; j < kids.Count; j++)
                {
                    if (!ArcLives.AtOdds(kids[i], kids[j])) continue;
                    int w = Mathf.Min(ArcUtil.Opinion(kids[i], kids[j]), ArcUtil.Opinion(kids[j], kids[i]));
                    if (w >= worst) continue;
                    worst = w;
                    x = kids[i];
                    y = kids[j];
                }
            if (x == null) return null;
            bool young = x.DevelopmentalStage.Juvenile() || y.DevelopmentalStage.Juvenile();
            return new ArcCandidate { other = x, third = y, tag = young ? "young" : "grown", weight = 1.4f };
        }

        public override string Broken(Arc arc) => ArcLives.FoughtSince(arc, arc.other, arc.third) ? "fight" : null;
    }

    /// <summary>
    /// Caught in the middle: two of their friends have fallen out, and they're the one both talk to. Likelier the closer
    /// they are to both and the further apart the two are (<see cref="ArcMath.TriangleWeight"/>).
    /// </summary>
    public class ArcWorker_Between : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var social = CouncilGameComponent.Instance?.social;
            if (social == null || !ColonyCouncilMod.Settings.socialWeb || ArcUtil.Psychopath(p) || p.DevelopmentalStage.Juvenile()) return null;
            var friends = social.ties.Where(t => t.kind == TieKind.Friends && t.announced && t.strength >= 30f && t.Involves(p))
                .Select(t => t.Other(p)).Where(o => o != null && home.Contains(o) && !o.DevelopmentalStage.Juvenile()).Distinct().ToList();
            if (friends.Count < 2) return null;
            ArcCandidate best = null;
            float bestWeight = 0f;
            for (int i = 0; i < friends.Count; i++)
                for (int j = i + 1; j < friends.Count; j++)
                {
                    var o = friends[i];
                    var t = friends[j];
                    if (!ArcLives.AtOdds(o, t)) continue;
                    int between = Mathf.Min(ArcUtil.Opinion(o, t), ArcUtil.Opinion(t, o));
                    // Rivals, or a sharp word lately, count as at odds whatever the opinions say.
                    if (social.Get(o, t, TieKind.Rivals) != null || ArcLives.RecentFriction(o, t)) between = Mathf.Min(between, 5);
                    float w = ArcMath.TriangleWeight(ArcUtil.Opinion(p, o), ArcUtil.Opinion(p, t), between);
                    if (w <= bestWeight) continue;
                    bestWeight = w;
                    best = new ArcCandidate { other = o, third = t, weight = w };
                }
            return best;
        }

        public override string Broken(Arc arc) => ArcLives.FoughtSince(arc, arc.other, arc.third) ? "fight" : null;
    }

    /// <summary>
    /// The road calls: a camp of travelers on the colony's land, and a colonist who has spent evenings at their fire and
    /// half wants to go with them. A friend at home ({OTHER}) would miss them. It moves on when the camp has gone, and
    /// ends with them still here. {NAME} is the camp's leader, {FACTION} their people.
    /// </summary>
    public class ArcWorker_CampRoad : ArcWorker
    {
        private static Camp CampNow => CampTracker.Enabled ? CampTracker.Instance?.camp : null;

        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var camp = CampNow;
            if (camp == null || camp.map != p.Map || (camp.phase != CampPhase.Building && camp.phase != CampPhase.Living)) return null;
            if (p.DevelopmentalStage.Juvenile() || !camp.warmth.TryGetValue(p, out int warm) || warm < 2) return null;
            bool restless = ArcUtil.Feeling(p, "Restless") != null || ArcUtil.Feeling(p, "Curiosity") != null
                            || (ArcUtil.Need(p, HeartNeed.Freedom) is float free && free >= 0f && free < 55f) || ArcUtil.Years(p) < 0.5f || warm >= 5;
            if (!restless) return null;
            var friend = ArcUtil.Closest(p, home, 15f);
            if (friend == null) return null;
            return new ArcCandidate { other = friend, key = camp.id.ToString(), faction = camp.faction, name = camp.leader?.LabelShort ?? camp.faction?.Name ?? "", weight = 1.2f };
        }

        /// <summary>The camp has packed up and gone.</summary>
        public override bool CustomGate(Arc arc, ArcGate gate)
        {
            var camp = CampTracker.Instance?.camp;
            return camp == null || camp.id.ToString() != arc.key || camp.phase == CampPhase.Leaving || camp.phase == CampPhase.Over;
        }

        /// <summary>The camp turned on the colony.</summary>
        public override string Broken(Arc arc)
        {
            var camp = CampTracker.Instance?.camp;
            return camp != null && camp.id.ToString() == arc.key && camp.phase == CampPhase.Fighting ? "campfight" : null;
        }
    }

    /// <summary>
    /// The weight of office: someone newly seated on the council (an office taken in the last election, at most twelve
    /// days ago) who isn't sure they can carry it, and someone to ask. {NAME} is the office. Fizzles [officegone] if
    /// they lose the seat first.
    /// </summary>
    public class ArcWorker_Office : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            var comp = CouncilGameComponent.Instance;
            if (comp == null || !ColonyCouncilMod.Settings.offices || p.DevelopmentalStage.Juvenile()) return null;
            var office = comp.offices.OfficeOf(p);
            if (office == null || Find.TickManager.TicksGame - comp.offices.lastElectionTick > GenDate.TicksPerDay * 12) return null;
            var speaker = comp.offices.Speaker;
            var other = speaker != null && speaker != p && home.Contains(speaker) ? speaker : ArcUtil.Closest(p, home, 10f);
            if (other == null) return null;
            return new ArcCandidate { other = other, key = office.defName, name = office.label, weight = 1.3f };
        }

        public override string Broken(Arc arc)
        {
            if (arc.stage >= arc.def.LastStage) return null;
            var office = CouncilGameComponent.Instance?.offices.OfficeOf(arc.pawn);
            return office == null || office.defName != arc.key ? "officegone" : null;
        }
    }

    /// <summary>
    /// Word from far away: hard times out in the world (a plague, a famine, a war, a flood...) and family of theirs out
    /// there in it. {LOST} is the relative, {NAME} the trouble, {FACTION} the relative's people. It moves on when the
    /// trouble is over; if the relative dies first, it fizzles [kindead], and that is a real loss.
    /// </summary>
    public class ArcWorker_FarNews : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (p.relations == null || ArcUtil.Psychopath(p) || !ColonyCouncilMod.Settings.worldEvents) return null;
            var world = WorldSituations.Instance;
            if (world == null) return null;
            var trouble = world.Running.FirstOrDefault(s => s?.def != null && s.def.kind != null && ArcLives.HardTimes.Contains(s.def.kind));
            if (trouble == null) return null;
            var kin = p.relations.DirectRelations
                .Where(r => r.otherPawn != null && !r.otherPawn.Dead && r.otherPawn.RaceProps.Humanlike && r.otherPawn.Faction != null && !r.otherPawn.Faction.IsPlayer
                            && !r.otherPawn.Spawned && (r.def.familyByBloodRelation || r.def == PawnRelationDefOf.Spouse || r.def == PawnRelationDefOf.Lover || r.def == PawnRelationDefOf.Fiance))
                .Select(r => r.otherPawn).Distinct()
                .OrderByDescending(o => (trouble.ctx?.faction != null && o.Faction == trouble.ctx.faction ? 100 : 0) + ArcUtil.Opinion(p, o)).FirstOrDefault();
            if (kin == null || ArcUtil.Opinion(p, kin) < 0) return null;
            var friend = ArcUtil.Closest(p, home, 10f);
            return new ArcCandidate
            {
                other = friend,
                lostName = kin.LabelShort,
                name = trouble.def.label,
                faction = kin.Faction,
                key = trouble.id + "|" + kin.ThingID,
                weight = 1.2f
            };
        }

        private static string[] Parts(Arc arc) => (arc.key ?? "").Split('|');

        /// <summary>The trouble is over.</summary>
        public override bool CustomGate(Arc arc, ArcGate gate)
        {
            var world = WorldSituations.Instance;
            string id = Parts(arc)[0];
            return world == null || !world.Running.Any(s => s != null && s.id.ToString() == id);
        }

        /// <summary>The relative died out there.</summary>
        public override string Broken(Arc arc)
        {
            var parts = Parts(arc);
            if (parts.Length < 2 || arc.pawn?.relations == null) return null;
            var kin = arc.pawn.relations.DirectRelations.Select(r => r.otherPawn).FirstOrDefault(o => o != null && o.ThingID == parts[1]);
            return kin != null && kin.Dead ? "kindead" : null;
        }
    }

    /// <summary>
    /// Bad nights (Biotech): a child with nightmares or a fright that won't let go, and a parent (or a grown-up fond of
    /// them) who sits up with them until the nights are quiet again.
    /// </summary>
    public class ArcWorker_NightTerrors : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!ModsConfig.BiotechActive || !p.DevelopmentalStage.Child()) return null;
            var comp = CouncilGameComponent.Instance;
            if (comp == null) return null;
            int now = Find.TickManager.TicksGame;
            bool nightmares = ColonyCouncilMod.Settings.dreams && comp.dreams.recent.Any(d => d.pawn == p && d.kind == DreamKind.Nightmare && now - d.tick <= GenDate.TicksPerDay * 6);
            bool afraid = ArcUtil.Feeling(p, "Fear") is FeelingNow fear && fear.intensity >= 10f || ArcUtil.Feeling(p, "Dread") != null;
            if (!nightmares && !afraid) return null;
            var parent = ArcLives.Parent(p, home);
            var other = parent ?? ArcLives.KindGrownUp(p, home);
            return other == null ? null : new ArcCandidate { other = other, tag = nightmares ? "nightmare" : "fear", weight = 1.4f };
        }
    }

    /// <summary>
    /// Not a child any more: a young person (13 to 19, grown by the game's reckoning) at odds with a parent or an older
    /// colonist who still treats them like a child, who sets out to prove otherwise in the work they love.
    /// </summary>
    public class ArcWorker_NotAChild : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            float age = Flavor.HumanAge(p);
            if (p.DevelopmentalStage.Juvenile() || age < 13f || age >= 20f || p.skills == null) return null;
            var parent = ArcLives.Parent(p, home);
            var other = parent != null && ArcLives.AtOdds(p, parent) ? parent
                : home.Where(o => o != p && Flavor.HumanAge(o) >= 35f && !o.DevelopmentalStage.Juvenile() && ArcLives.AtOdds(p, o))
                    .OrderBy(o => ArcUtil.Opinion(o, p)).FirstOrDefault();
            if (other == null) return null;
            var skill = ArcUtil.Best(p, ArcUtil.AllSkills, 0);
            return skill == null ? null : new ArcCandidate { other = other, skill = skill, tag = "teen", weight = 1.3f };
        }

        public override string Broken(Arc arc) => ArcLives.FoughtSince(arc, arc.pawn, arc.other) ? "fight" : null;
    }

    /// <summary>
    /// A new parent (Biotech): a baby of theirs born in the colony in the last few days, sleepless nights, help from
    /// someone, and finding their feet. {NAME} is the baby. If the baby dies, it ends there ([babygone]).
    /// </summary>
    public class ArcWorker_NewParent : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (!ModsConfig.BiotechActive || p.relations == null || p.DevelopmentalStage.Juvenile()) return null;
            var baby = p.relations.Children.FirstOrDefault(c => c != null && !c.Dead && c.Faction == Faction.OfPlayer && c.ageTracker != null
                                                                && c.ageTracker.AgeBiologicalYearsFloat < 0.15f);
            if (baby == null) return null;
            var partner = LovePartnerRelationUtility.ExistingLovePartner(p, false);
            var other = partner != null && home.Contains(partner) ? partner : ArcUtil.Closest(p, home, 10f);
            return other == null ? null : new ArcCandidate { other = other, name = baby.LabelShort, key = baby.ThingID, weight = 1.5f };
        }

        public override string Broken(Arc arc)
        {
            var baby = arc.pawn?.relations?.Children.FirstOrDefault(c => c != null && c.ThingID == arc.key);
            return baby == null || baby.Dead ? "babygone" : null;
        }
    }

    /// <summary>
    /// One more spring: someone old (65 or more) whose body has started to fail (a lasting condition, or poor health),
    /// putting their affairs in order: telling someone their wishes, one last piece of work, and peace. {OTHER} is the
    /// one they tell, who may be gone before the end without ending it.
    /// </summary>
    public class ArcWorker_OneMoreSpring : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (Flavor.HumanAge(p) < 65f || ArcUtil.Psychopath(p) || p.health?.hediffSet == null) return null;
            bool failing = p.health.hediffSet.hediffs.Any(h => h?.def != null && h.def.chronic && h.Visible) || p.health.summaryHealth.SummaryHealthPercent < 0.85f;
            if (!failing) return null;
            var other = ArcUtil.Closest(p, home, 15f);
            var skill = ArcUtil.Best(p, ArcUtil.AllSkills, 4);
            return other == null || skill == null ? null : new ArcCandidate { other = other, skill = skill, tag = "elder", weight = 1.3f };
        }
    }

    /// <summary>The last two of us: two old colonists (55 or more) who have long been at odds, and slowly aren't.</summary>
    public class ArcWorker_OldRivals : ArcWorker
    {
        public override ArcCandidate Candidate(Pawn p, List<Pawn> home)
        {
            if (Flavor.HumanAge(p) < 55f || ArcUtil.Psychopath(p) || ArcUtil.Years(p) < 0.5f) return null;
            var other = home.Where(o => o != p && Flavor.HumanAge(o) >= 55f && !ArcUtil.Psychopath(o) && ArcUtil.Years(o) >= 0.5f && ArcLives.AtOdds(p, o))
                .OrderBy(o => ArcUtil.Opinion(p, o) + ArcUtil.Opinion(o, p)).FirstOrDefault();
            return other == null ? null : new ArcCandidate { other = other, tag = "elder", weight = 1.2f };
        }

        public override string Broken(Arc arc) => ArcLives.FoughtSince(arc, arc.pawn, arc.other) ? "fight" : null;
    }
}
