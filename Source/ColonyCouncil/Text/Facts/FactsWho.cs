using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    // The providers of who is talking: the moment, their people, the two of them, what passed between them.
    public static partial class Facts
    {
        // ---------------- The moment ----------------

        private static readonly HashSet<string> MomentTags = new HashSet<string>
        {
            "spring", "summer", "fall", "winter", "morning", "day", "evening", "night", "rain", "snow", "fog", "storm", "clear", "hot", "cold",
            "tribal", "medieval", "industrial", "spacer", "lowtech", "poor", "rich", "small", "large"
        };

        private static void Moment(FactSet f, Pawn c, Pawn x)
        {
            foreach (var t in Flavor.TagsFor(c))
            {
                if (MomentTags.Contains(t)) f.Tag(t, t == "storm" ? 1f : t == "snow" || t == "hot" || t == "cold" ? 0.5f : 0f);
                else if (t.StartsWith("trait:", StringComparison.Ordinal) || t.StartsWith("skill:", StringComparison.Ordinal) || t == "old" || t == "teen" || t == "hurt"
                         || t == "grieving" || t == "lowmood" || t == "highmood")
                    f.usTags.Add(t);
            }
            var map = c.MapHeld;
            if (map != null)
            {
                f.Value("SEASON", GenLocalDate.Season(map).Label());
                var w = map.weatherManager?.curWeather;
                if (w != null && !w.label.NullOrEmpty()) f.Value("WEATHER", w.label);
            }
            // The outsider's own: their nature, their age, their hurts.
            var traits = x.story?.traits?.allTraits;
            if (traits != null)
                foreach (var t in traits)
                    if (t?.def != null)
                        f.themTags.Add(Flavor.DefTag("trait:", t.def));
            float age = Flavor.HumanAge(x);
            if (age >= 60f) f.themTags.Add("old");
            else if (age < 20f) f.themTags.Add("teen");
            if ((x.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.85f) f.themTags.Add("hurt");
        }

        // ---------------- Their people ----------------

        private static void People(FactSet f, Pawn c, Pawn x, Faction p)
        {
            if (p != null)
            {
                f.Value("PEOPLE", p.Name);
                f.Thing("PEOPLE", Refs.Of(p).ToString());
                var leader = p.leader;
                if (leader != null && !leader.Dead && leader != x)
                {
                    f.Tag("their:leader");
                    f.Value("LEADER", leader.LabelShort, "leader:" + p.loadID + ":" + leader.thingIDNumber);
                    if (!p.LeaderTitle.NullOrEmpty()) f.Value("LEADERTITLE", p.LeaderTitle);
                }
                if (leader == x) f.Tag("them:leader", 1f);
                // Outlaws by the game's own word (a rough outlander union, a fierce tribe), friendly enough now to talk.
                if (p.def.naturalEnemy && !p.def.permanentEnemy) f.Tag("their:rough");
                if (p.PlayerRelationKind == FactionRelationKind.Ally) f.Tag("their:ally");
                // One of Rimfolk's peoples, by what it lives by: their:traders, their:healers, their:herders (Peoples/).
                if (PeoplesMath.Tag(Peoples.CallingOf(p)) is string calling) f.Tag(calling);
            }
            var title = x.royalty?.MostSeniorTitle;
            if (title != null)
            {
                f.Tag("their:noble", 1f);
                f.Value("THEIRTITLE", title.Label);
            }
            var ours = c.royalty?.MostSeniorTitle;
            if (ours != null)
            {
                f.Tag("us:noble");
                f.Value("OURTITLE", ours.Label);
            }
            // Two who both hold a title: who stands higher, by the titles' own seniority.
            if (title?.def != null && ours?.def != null)
            {
                if (title.def.seniority > ours.def.seniority) f.Tag("them:outranks");
                else if (ours.def.seniority > title.def.seniority) f.Tag("us:outranks");
            }
            if (ModsConfig.IdeologyActive && c.Ideo != null && x.Ideo != null)
            {
                if (c.Ideo == x.Ideo) f.Tag("samefaith");
                else f.Tag("otherfaith");
                f.Value("THEIRFAITH", x.Ideo.name);
                f.Value("OURFAITH", c.Ideo.name);
            }
            // What they are here as.
            var n = Outsiders.NotableOf(x);
            if (Strangers.IsHospitalityGuest(x)) f.Tag("role:guest", 1f);
            else if (x.IsQuestLodger()) f.Tag("role:lodger", 1f);
            else if (x.TraderKind != null) f.Tag("role:trader", 1f);
            else f.Tag("role:visitor");
            if (n != null && n.role != NotableRole.Acquaintance)
            {
                f.Tag("role:notable", 1f);
                f.Tag("role:" + n.role.ToString().ToLowerInvariant(), 1f);
                f.Value("THEIRROLE", NotableBook.TitleOf(n));
            }
        }

        // ---------------- The two of them ----------------

        private static void Persons(FactSet f, Pawn c, Pawn x)
        {
            if (x.DevelopmentalStage.Juvenile()) f.Tag("them:young");
            if (c.DevelopmentalStage.Juvenile()) f.Tag("us:young");
            if (Flavor.HumanAge(x) >= 60f) f.Tag("them:old");
            if (Flavor.HumanAge(c) >= 60f) f.Tag("us:old");
            if ((x.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.85f) f.Tag("them:hurt", 1.5f);
            if ((c.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.85f) f.Tag("us:hurt", 1f);
            if (x.story?.traits?.HasTrait(TraitDefOf.Kind) ?? false) f.Tag("them:kind");
            if (x.story?.traits?.HasTrait(TraitDefOf.Abrasive) ?? false) f.Tag("them:abrasive");
            Best(f, x, "them", "THEIRSKILL");
            Best(f, c, "us", "OURSKILL");
            Family(f, x, "them", "THEIRSPOUSE", "THEIRCHILD");
            Family(f, c, "us", "OURSPOUSE", "OURCHILD");
            // Family of theirs in the colony: the colony they are at (all of it, with one colony).
            var at = ColonyState.Several && Homes.IsHome(x.MapHeld) ? x.MapHeld : null;
            foreach (var o in PawnsFinder.AllMaps_FreeColonists)
            {
                if (o == null || o == x || o.Dead || x.relations == null) continue;
                if (at != null && Homes.Of(o) != at) continue;
                var rel = x.GetRelations(o).FirstOrDefault(r => r != null && (r.familyByBloodRelation || r == PawnRelationDefOf.Spouse));
                if (rel == null) continue;
                f.Tag("kin", 2f);
                if (o == c) f.Tag("kin:you", 2.5f);
                f.Value("KIN", o.LabelShort);
                f.Value("KINWORD", rel.GetGenderSpecificLabel(o));
                break;
            }
            int days = (c.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 999999) / GenDate.TicksPerDay;
            if (days < 15) f.Tag("us:newcomer", 1f);
        }

        private static void Best(FactSet f, Pawn p, string who, string key)
        {
            var skills = p.skills?.skills;
            if (skills == null) return;
            SkillRecord best = null;
            foreach (var s in skills)
                if (s != null && !s.TotallyDisabled && s.Level >= 8 && (best == null || s.Level > best.Level))
                    best = s;
            if (best == null) return;
            f.Tag(who + ":best:" + best.def.defName);
            f.Value(key, best.def.label);
        }

        private static void Family(FactSet f, Pawn p, string who, string spouseKey, string childKey)
        {
            var rel = p.relations;
            if (rel == null) return;
            var spouse = rel.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse);
            if (spouse != null)
            {
                f.Tag(spouse.Dead ? who + ":widowed" : who + ":married");
                if (!spouse.Dead) f.Value(spouseKey, spouse.LabelShort);
                // Here with them (on the same map), not waiting at home.
                if (!spouse.Dead && spouse.Spawned && spouse.Map == p.MapHeld) f.Tag(who + ":spousehere");
            }
            var child = rel.Children?.Where(k => k != null && !k.Dead).OrderBy(k => k.ageTracker?.AgeBiologicalYearsFloat ?? 99f).FirstOrDefault();
            if (child != null)
            {
                f.Tag(who + ":parent");
                if (child.DevelopmentalStage.Juvenile()) f.Tag(who + ":smallchild");
                if (child.Spawned && child.Map == p.MapHeld) f.Tag(who + ":childhere");
                f.Value(childKey, child.LabelShort);
            }
        }

        // ---------------- What passed between them ----------------

        private static void Pair(FactSet f, Pawn c, Pawn x)
        {
            var pair = OutsiderBook.Instance?.Pair(c, x, false);
            int now = Now;
            int ofColonist = Outsiders.OfColonist(x, c), ofColony = Outsiders.OfColony(x);
            if (ofColonist >= 25) f.Tag("likesyou");
            else if (ofColonist <= -10) f.Tag("coolyou");
            if (ofColony >= 30) f.Tag("likesus");
            else if (ofColony <= -10) f.Tag("wary");
            if (pair == null || pair.talks == 0 && pair.lastTick < 0)
            {
                f.Tag("first", 1.5f);
                return;
            }
            f.Tag("met");
            if (pair.lastTick >= 0 && OutsiderMath.NewVisit(pair.lastTick, now)) f.Tag("returning", 2f);
            if (pair.fam >= 30f) f.Tag("known");
            if (pair.fam >= 60f) f.Tag("close");
            if (pair.friends) f.Tag("friends", 1f);
            if (pair.court == 1) f.Tag("sweethearts", 1.5f);
            if (pair.court == 2) f.Tag("lovers", 1.5f);
            if (pair.talks >= 3) f.Tag("talks:3");
            if (pair.talks >= 8) f.Tag("talks:8");
            void Did(OutsiderAct a, string tag)
            {
                if (pair.Last(a) >= 0) f.Tag("did:" + tag, Days(pair.Last(a)) < 3f ? 1f : 0f);
            }
            Did(OutsiderAct.Gift, "gift");
            Did(OutsiderAct.Meal, "meal");
            Did(OutsiderAct.Drink, "meal");
            Did(OutsiderAct.Teach, "teach");
            Did(OutsiderAct.Spar, "spar");
            Did(OutsiderAct.Duel, "duel");
            Did(OutsiderAct.Provoke, "provoke");
            Did(OutsiderAct.Faith, "faith");
            Did(OutsiderAct.Court, "court");
            Did(OutsiderAct.Letter, "letter");
            Did(OutsiderAct.Bring, "bring");
            if (pair.topics.Count > 0) f.Tag("last:" + pair.topics[pair.topics.Count - 1]);
            Shared(f, c, x);
        }

        /// <summary>
        /// What the two did together that went through, as the outsider remembers the colonist (the game's mood memories,
        /// Outsiders/OutsiderOutcomes.cs): shared:meal, shared:drink, shared:lesson, shared:gift (taken, and counting for
        /// something), shared:bout (a bout or a duel fought). did:KIND above is any asking, refused or not.
        /// </summary>
        private static void Shared(FactSet f, Pawn c, Pawn x)
        {
            var memories = x.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) return;
            foreach (var m in memories)
            {
                if (m?.def == null || m.otherPawn != c) continue;
                if (m.def == OutsiderDefOf.Rimfolk_OutsiderMeal) f.Tag(m.CurStageIndex == 1 ? "shared:drink" : "shared:meal");
                else if (m.def == OutsiderDefOf.Rimfolk_OutsiderTaught) f.Tag("shared:lesson");
                else if (m.def == OutsiderDefOf.Rimfolk_OutsiderGift) f.Tag("shared:gift");
                else if (m.def == OutsiderDefOf.Rimfolk_OutsiderBout) f.Tag("shared:bout");
            }
        }
    }
}
