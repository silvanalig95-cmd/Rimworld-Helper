using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The family's life together, as it happens (the pure part is FamilyLifeMath.cs): a new couple, and those close to
    /// them glad (or family who can't stand the one chosen); an engagement and the parents' blessing; a refused proposal,
    /// a walk alone and a friend's word; a child on the way told to the partner and the grandparents; a child lost before
    /// birth, with a quiet day for the parents and someone to sit with the mother; the family's names offered for a baby;
    /// and the birthdays a family marks, the growth moments and coming of age, with a toast in the evening. What is said
    /// is said when the two are together. The only thing saved is which pregnancies have been told of
    /// (<see cref="CouncilGameComponent.expectingTold"/>).
    /// </summary>
    public static class FamilyLife
    {
        private static ThoughtDef T(string name) => DefDatabase<ThoughtDef>.GetNamedSilentFail(name);

        private static bool Ours(Pawn p) => p != null && !p.Dead && p.IsColonist && People.HasInnerLife(p) && !p.IsQuestLodger();

        private static bool Playing => Current.ProgramState == ProgramState.Playing && CouncilGameComponent.Instance != null;

        // ---------------- A new couple, an engagement ----------------

        /// <summary>Lovers that are new between two of the colony: game-set ones only, once.</summary>
        private static readonly HashSet<string> newPairs = new HashSet<string>();

        public static void Notify_RelationAdded(Pawn a, PawnRelationDef def, Pawn b)
        {
            if (!Playing || def == null || (def != PawnRelationDefOf.Lover && def != PawnRelationDefOf.Fiance)) return;
            if (!Ours(a) || !Ours(b) || a.MapHeld == null) return;
            string key = def.defName + "|" + Math.Min(a.thingIDNumber, b.thingIDNumber) + "|" + Math.Max(a.thingIDNumber, b.thingIDNumber) + "|" + GenDate.DaysPassed;
            if (!newPairs.Add(key)) return;
            if (newPairs.Count > 200) newPairs.Clear();
            bool engaged = def == PawnRelationDefOf.Fiance;
            var comp = CouncilGameComponent.Instance;
            string text = (engaged ? "CC_Fam_Chronicle_Engaged" : "CC_Fam_Chronicle_Couple").Translate(a.Named("A"), b.Named("B")).Resolve();
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, text, a));
            foreach (var x in new[] { a, b })
                if (!comp.journals.WroteWithin(x)) comp.journals.Write(x, engaged ? "Council_Journal_Engaged" : "Council_Journal_NewLove", null, (x == a ? b : a).Named("OTHER"));
            Reactions(a, b, engaged);
        }

        /// <summary>
        /// Those close to the couple take it as they take it: family glad (parents blessing an engagement), or doubting
        /// a match with someone they can't stand; friends who think well of one and don't mind the other, glad. Each
        /// says so to the one of the two they're close to, the next time they're together.
        /// </summary>
        private static void Reactions(Pawn a, Pawn b, bool engaged)
        {
            var map = a.MapHeld;
            var near = new List<(Pawn who, Pawn theirs, Pawn partner, MatchTake take, bool parent)>();
            foreach (var f in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Where(f => f != a && f != b && Ours(f)))
            {
                foreach (var (theirs, partner) in new[] { (a, b), (b, a) })
                {
                    bool family = Family.Of(f, theirs);
                    // An engagement is for the family; a new couple, for anyone close.
                    if (engaged && !family) continue;
                    var take = FamilyLifeMath.Take(family, f.relations.OpinionOf(theirs), f.relations.OpinionOf(partner));
                    if (take == MatchTake.None) continue;
                    bool parent = theirs.GetMother() == f || theirs.GetFather() == f;
                    near.Add((f, theirs, partner, take, parent));
                    break;
                }
            }
            foreach (var r in near.OrderByDescending(r => r.parent).ThenByDescending(r => Family.Of(r.who, r.theirs)).ThenByDescending(r => r.who.relations.OpinionOf(r.theirs))
                         .Take(FamilyLifeMath.MaxReacting))
            {
                var (who, theirs, partner, take, parent) = r;
                Together.When((engaged ? "engaged|" : "couple|") + who.thingIDNumber + "|" + theirs.thingIDNumber, who, theirs, () => React(who, theirs, partner, take, parent, engaged), 72f);
            }
        }

        private static void React(Pawn who, Pawn theirs, Pawn partner, MatchTake take, bool parent, bool engaged)
        {
            // together: Together.When runs this only when the two are together; Speech.Say asks again.
            if (who.Dead || theirs.Dead || !LovePartnerRelationUtility.LovePartnerRelationExists(theirs, partner)) return;
            string relation = Family.Relation(theirs, who);
            string cause = "CC_Fam_CoupleCause".Translate(theirs.Named("A"), partner.Named("B")).Resolve();
            string pool;
            if (take == MatchTake.Doubt)
            {
                CouncilGameComponent.GiveThought(who, T("Council_Fam_MatchDoubted"), partner, -1f, cause);
                pool = "Council_Pool_SayMatchDoubt";
            }
            else if (engaged && parent)
            {
                CouncilGameComponent.GiveThought(who, T("Council_Fam_ChildEngaged"), theirs, -1f, cause);
                pool = "Council_Pool_SayEngaged";
            }
            else
            {
                CouncilGameComponent.GiveThought(who, T("Council_Fam_GladForThem"), theirs, -1f, cause);
                pool = "Council_Pool_SayNewCouple";
            }
            string line = Flavor.FromPool(pool, who);
            if (!line.NullOrEmpty())
                Speech.Say(who, line.Formatted(theirs.Named("OTHER"), partner.Named("ABOUT"), relation.Named("RELATION")).Resolve(), theirs);
        }

        // ---------------- A proposal refused ----------------

        public static void Notify_ProposalRefused(Pawn asker, Pawn refuser)
        {
            // apart: the asker goes off alone; the friend's word comes when the two are next together.
            if (!Playing || !Ours(asker) || refuser == null || !asker.Spawned) return;
            var comp = CouncilGameComponent.Instance;
            if (!comp.journals.WroteWithin(asker)) comp.journals.Write(asker, "Council_Journal_ProposalRefused", null, refuser.Named("OTHER"));
            // A long walk on their own, if the answers that play out on the map are on (PlayOut/).
            var ctx = new DilemmaContext { map = asker.Map, a = asker, b = refuser };
            new DilemmaEffect_Visit
            {
                who = PlayWho.A, to = PlaySpot.Walk, label = "CC_Fam_WalkAloneLabel".Translate(), hours = new IntRange(6, 22),
                duration = new FloatRange(1f, 1.5f), waitHours = 4f
            }.Begin(ctx);
            // The friend closest to them (not the one who said no) finds a word for it later.
            var friend = asker.Map.mapPawns.FreeColonistsSpawned.Where(f => f != asker && f != refuser && Ours(f) && f.relations.OpinionOf(asker) >= FamilyLifeMath.FriendOpinion)
                .OrderByDescending(f => Family.Of(f, asker)).ThenByDescending(f => f.relations.OpinionOf(asker)).FirstOrDefault();
            if (friend == null) return;
            Together.When("refused|" + friend.thingIDNumber + "|" + asker.thingIDNumber, friend, asker, () =>
            {
                // together: said when the two are together (Together.When), and Speech.Say asks again.
                if (friend.Dead || asker.Dead) return;
                CouncilGameComponent.GiveThought(asker, T("Council_Fam_Comforted"), friend, -1f, "CC_Fam_ComfortedCause".Translate(friend.Named("PAWN")).Resolve());
                string line = Flavor.FromPool("Council_Pool_SayProposalComfort", friend);
                if (!line.NullOrEmpty()) Speech.Say(friend, line.Formatted(asker.Named("OTHER"), refuser.Named("ABOUT")).Resolve(), asker);
            }, 48f);
        }

        // ---------------- A child on the way ----------------

        private static HediffDef pregnant;
        private static HediffDef Pregnant => pregnant ??= DefDatabase<HediffDef>.GetNamedSilentFail("PregnantHuman");

        /// <summary>Their pregnancy, once it shows (Biotech), or null.</summary>
        public static Hediff_Pregnant ShowingPregnancy(Pawn p)
        {
            if (Pregnant == null || p?.health?.hediffSet == null) return null;
            return p.health.hediffSet.GetFirstHediffOfDef(Pregnant) is Hediff_Pregnant h && h.Visible ? h : null;
        }

        /// <summary>Expecting a child, or their partner is: the crib wish and the like count them a parent already.</summary>
        public static bool Expecting(Pawn p)
        {
            if (p?.relations == null || Pregnant == null) return false;
            if (ShowingPregnancy(p) != null) return true;
            foreach (var r in p.relations.DirectRelations)
                if ((r.def == PawnRelationDefOf.Lover || r.def == PawnRelationDefOf.Spouse || r.def == PawnRelationDefOf.Fiance) && ShowingPregnancy(r.otherPawn) is Hediff_Pregnant h && h.Father == p)
                    return true;
            return false;
        }

        /// <summary>
        /// The council's hour: a pregnancy that has begun to show is told, once. The mother tells her partner (glad, or
        /// worried if the child wasn't wanted), and each tells their own parents, the grandparents to be.
        /// </summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            if (Pregnant == null || comp == null) return;
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
                foreach (var mother in map.mapPawns.FreeColonistsSpawned.Where(Ours).ToList())
                {
                    var h = ShowingPregnancy(mother);
                    if (h == null || comp.expectingTold.Contains(h.loadID)) continue;
                    comp.expectingTold.Add(h.loadID);
                    if (comp.expectingTold.Count > 60) comp.expectingTold.RemoveRange(0, comp.expectingTold.Count - 60);
                    Told(comp, mother, h);
                }
        }

        private static void Told(CouncilGameComponent comp, Pawn mother, Hediff_Pregnant h)
        {
            // apart: what's written and recorded; the telling itself waits for the two to be together (Together.When).
            var father = h.Father;
            var partner = Ours(father) ? father : mother.relations.DirectRelations
                .Where(r => r.def == PawnRelationDefOf.Spouse || r.def == PawnRelationDefOf.Fiance || r.def == PawnRelationDefOf.Lover)
                .Select(r => r.otherPawn).FirstOrDefault(Ours);
            bool avoiding = partner != null && mother.relations.GetPregnancyApproachForPartner(partner) == PregnancyApproach.AvoidPregnancy;
            bool wanted = FamilyLifeMath.Wanted(h.Attitude == PregnancyAttitude.Negative, avoiding);
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Fam_Chronicle_Expecting".Translate(mother.Named("A")).Resolve(), mother));
            if (!comp.journals.WroteWithin(mother))
                comp.journals.WriteTagged(mother, "Council_Journal_Expecting", partner != null ? new[] { wanted ? "glad" : "worried", "partner" } : new[] { wanted ? "glad" : "worried" },
                    null, (partner ?? mother).Named("OTHER"));
            string cause = "CC_Fam_ExpectingCause".Translate(mother.Named("A")).Resolve();
            int id = h.loadID;
            if (partner != null)
                Together.When("expecting|" + id, mother, partner, () =>
                {
                    // together: she tells them face to face (Together.When), and Speech.Say asks again.
                    if (mother.Dead || partner.Dead || ShowingPregnancy(mother)?.loadID != id) return;
                    CouncilGameComponent.GiveThought(partner, T(wanted ? "Council_Fam_BabyComing" : "Council_Fam_BabyComingWorried"), mother, -1f, cause);
                    string line = Flavor.FromPool("Council_Pool_SayExpecting", mother, null, null, wanted ? "glad" : "worried");
                    if (!line.NullOrEmpty()) Speech.Say(mother, line.Formatted(partner.Named("OTHER")).Resolve(), partner);
                }, 72f, 0f);
            // The grandparents to be, told by their own child.
            foreach (var parent in new[] { mother, partner }.Where(x => x != null))
                foreach (var gp in new[] { parent.GetMother(), parent.GetFather() }.Where(g => Ours(g) && g.relations.OpinionOf(parent) > FamilyLifeMath.DoubtOpinion))
                {
                    var (child, grand) = (parent, gp);
                    Together.When("grandchild|" + id + "|" + grand.thingIDNumber, child, grand, () =>
                    {
                        // together: their child tells them in person (Together.When), and Speech.Say asks again.
                        if (child.Dead || grand.Dead || ShowingPregnancy(mother)?.loadID != id) return;
                        CouncilGameComponent.GiveThought(grand, T("Council_Fam_GrandchildComing"), child, -1f, cause);
                        string line = Flavor.FromPool("Council_Pool_SayGrandchild", grand);
                        if (!line.NullOrEmpty()) Speech.Say(grand, line.Formatted(child.Named("OTHER")).Resolve(), child);
                    }, 96f);
                }
        }

        // ---------------- A child lost before birth ----------------

        /// <summary>
        /// A child lost before it was born (a miscarriage or a stillbirth; the parents' grief is the game's own): the
        /// grandparents grieve too, the parents have the rest of the day to themselves, and someone close goes to sit with
        /// the mother.
        /// </summary>
        public static void Notify_Lost(Pawn mother, Pawn father, bool stillborn)
        {
            // apart: grief for news of a loss, wherever they are; the sitting-with is a visit on the map (PlayOut/).
            if (!Playing || !Ours(mother)) return;
            var comp = CouncilGameComponent.Instance;
            var parents = new[] { mother, father }.Where(Ours).ToList();
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Life, (stillborn ? "CC_Fam_Chronicle_Stillborn" : "CC_Fam_Chronicle_Lost").Translate(mother.Named("A")).Resolve(), mother));
            foreach (var p in parents)
            {
                var other = p == mother ? father : mother;
                if (!comp.journals.WroteWithin(p))
                    comp.journals.WriteTagged(p, "Council_Journal_Loss", other != null && other != p ? new[] { "partner" } : null, null, (other ?? p).Named("OTHER"));
            }
            string cause = "CC_Fam_LostCause".Translate(mother.Named("A")).Resolve();
            foreach (var p in parents)
                foreach (var gp in new[] { p.GetMother(), p.GetFather() }.Where(Ours))
                    CouncilGameComponent.GiveThought(gp, T("Council_Fam_LostGrandchild"), p, -1f, cause);
            var map = mother.MapHeld;
            if (map == null || !mother.Spawned) return;
            // The rest of today is theirs (only the timetable changes: the game's sickbed and the drafted are as always).
            var home = parents.Where(p => p.Spawned && p.Map == map).ToList();
            var given = PlayOuts.On ? CalendarTracker.Instance?.RestFor(map, home, false) : null;
            if (!given.NullOrEmpty())
                Messages.Message("CC_Fam_QuietDay".Translate(TextUtil.JoinAnd(given.Select(p => p.LabelShort).ToList())).Resolve(),
                    new LookTargets(given), MessageTypeDefOf.NeutralEvent, false);
            // Someone of her own family, else her closest friend, goes and sits with her a while.
            var sitter = map.mapPawns.FreeColonistsSpawned.Where(s => s != mother && Ours(s) && !s.Downed && s.relations.OpinionOf(mother) >= 0)
                .OrderByDescending(s => Family.Of(s, mother)).ThenByDescending(s => s == father).ThenByDescending(s => s.relations.OpinionOf(mother)).FirstOrDefault();
            if (sitter == null) return;
            new DilemmaEffect_Visit
            {
                who = PlayWho.A, to = PlaySpot.Person, whom = PlayWho.B, label = "CC_Fam_SitWithLabel".Translate(), hours = new IntRange(7, 23),
                duration = new FloatRange(1.5f, 2.5f), waitHours = 12f
            }.Begin(new DilemmaContext { map = map, a = sitter, b = mother });
        }

        // ---------------- A name for the baby ----------------

        private static int NameRank(string relation)
        {
            switch (relation)
            {
                case "Parent": return 0;
                case "Grandparent": return 1;
                case "UncleOrAunt": return 2;
                case "GreatGrandparent": return 3;
                default: return 4;
            }
        }

        /// <summary>The family's first names to offer for this baby (FamilyLifeMath.Suggestions).</summary>
        public static List<string> NameSuggestions(Pawn baby)
        {
            if (baby?.relations == null) return new List<string>();
            var family = new List<NameSource>();
            var taken = new List<string>();
            foreach (var o in baby.relations.FamilyByBlood)
            {
                if (o == null || o == baby || !(o.Name is NameTriple n)) continue;
                var rel = baby.GetMostImportantRelation(o)?.defName;
                if ((rel == "Sibling" || rel == "HalfSibling") && !o.Dead)
                {
                    taken.Add(n.First);
                    continue;
                }
                family.Add(new NameSource { name = n.First, rank = NameRank(rel), dead = o.Dead, fits = baby.gender == Gender.None || o.gender == baby.gender });
            }
            return FamilyLifeMath.Suggestions(family, taken);
        }

        /// <summary>
        /// "Named after their grandmother Toni", for the story: a first name an older relative had. Named after when
        /// they were born here and named by the colony; else they only share it.
        /// </summary>
        public static string NamedAfterLine(Pawn p)
        {
            if (p?.relations == null || !(p.Name is NameTriple me)) return null;
            var rel = new List<(string name, int rank, bool older)>();
            var who = new List<Pawn>();
            foreach (var o in p.relations.FamilyByBlood)
            {
                if (o == null || o == p || !(o.Name is NameTriple n)) continue;
                rel.Add((n.First, NameRank(p.GetMostImportantRelation(o)?.defName), o.ageTracker.BirthAbsTicks < p.ageTracker.BirthAbsTicks));
                who.Add(o);
            }
            int i = FamilyLifeMath.NamedAfter(me.First, rel);
            if (i < 0) return null;
            var after = who[i];
            string relation = Family.Relation(p, after);
            return (p.babyNamingDeadline > 0 ? "CC_Family_NamedAfter" : "CC_Family_SharesName")
                .Translate(p.Named("PAWN"), after.Named("OTHER"), relation.Named("RELATION")).Resolve();
        }

        // ---------------- Birthdays the family marks ----------------

        /// <summary>
        /// A growth moment (7, 10, 13) or coming of age: the parents write of it, and the family raise a glass to them that
        /// evening, a parent leading (PlayOut/), with a good moment for the child and the parents who are there.
        /// </summary>
        public static void Notify_Birthday(Pawn p, int age)
        {
            if (!Playing || !Ours(p) || !p.Spawned || !p.Map.IsPlayerHome) return;
            bool growth;
            try
            {
                growth = GrowthUtility.IsGrowthBirthday(age);
            }
            catch
            {
                growth = false;
            }
            if (!FamilyLifeMath.Marked(age, growth)) return;
            var comp = CouncilGameComponent.Instance;
            var parents = new[] { p.GetMother(), p.GetFather() }.Where(Ours).ToList();
            foreach (var x in parents)
                if (!comp.journals.WroteWithin(x)) comp.journals.Write(x, "Council_Journal_ChildGrew", null, p.Named("OTHER"), age.Named("YEARS"));
            var lead = parents.FirstOrDefault(x => x.Spawned && x.Map == p.Map);
            if (lead == null) return;
            var toast = new DilemmaEffect_Toast
            {
                target = DilemmaTarget.FamilyOfA, organizer = PlayWho.B, label = (age >= FamilyLifeMath.AdultAge ? "CC_Fam_ComingOfAgeLabel" : "CC_Fam_BirthdayToastLabel").Translate(),
                hours = new IntRange(17, 22), duration = new FloatRange(0.75f, 1f), pool = "Council_Pool_PlayGrowth", waitHours = 4f
            };
            var play = toast.Begin(new DilemmaContext { map = p.Map, a = p, b = lead });
            if (play == null) return;
            // The one it's for, so what's said at it is said of them.
            play.other = p;
            string cause = "CC_Fam_BirthdayCause".Translate(p.Named("A"), age).Resolve();
            play.moods.Add(new HeldMood { pawn = p, thought = T("Council_Fam_BirthdayToast"), cause = cause });
            foreach (var x in parents) play.moods.Add(new HeldMood { pawn = x, thought = T("Council_Fam_ChildGrown"), about = p, cause = cause });
            play.moods.RemoveAll(m => m.thought == null);
        }
    }

    /// <summary>A new couple or an engagement between two of the colony (Social/FamilyLife.cs).</summary>
    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.AddDirectRelation))]
    public static class Patch_FamilyCouple
    {
        public static void Postfix(Pawn ___pawn, PawnRelationDef def, Pawn otherPawn)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                FamilyLife.Notify_RelationAdded(___pawn, def, otherPawn);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("family couple hook", e);
            }
        }
    }

    /// <summary>A proposal that was turned down (Social/FamilyLife.cs).</summary>
    [HarmonyPatch(typeof(InteractionWorker_MarriageProposal), nameof(InteractionWorker_MarriageProposal.Interacted))]
    public static class Patch_FamilyProposal
    {
        public static void Postfix(Pawn initiator, Pawn recipient)
        {
            if (!RimfolkPatches.On || initiator?.relations == null || recipient == null) return;
            try
            {
                if (!initiator.relations.DirectRelationExists(PawnRelationDefOf.Fiance, recipient) && !initiator.relations.DirectRelationExists(PawnRelationDefOf.Spouse, recipient))
                    FamilyLife.Notify_ProposalRefused(initiator, recipient);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("family proposal hook", e);
            }
        }
    }

    /// <summary>A miscarriage (the game's own, not a pregnancy ended by surgery: only one the mother grieves as one).</summary>
    [HarmonyPatch(typeof(Hediff_Pregnant), nameof(Hediff_Pregnant.Miscarry))]
    public static class Patch_FamilyMiscarried
    {
        public static void Postfix(Hediff_Pregnant __instance)
        {
            if (!RimfolkPatches.On || __instance?.pawn == null) return;
            try
            {
                var mother = __instance.pawn;
                bool grieved = mother.needs?.mood?.thoughts?.memories?.Memories?.Any(m => m.def.defName == "Miscarried" && m.age < GenDate.TicksPerHour) ?? false;
                if (grieved) FamilyLife.Notify_Lost(mother, __instance.Father, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("family miscarriage hook", e);
            }
        }
    }

    /// <summary>The family's first names offered when a baby is named (Social/FamilyLife.cs), beside the game's own last names.</summary>
    [HarmonyPatch(typeof(Dialog_NamePawn), MethodType.Constructor,
        new[] { typeof(Pawn), typeof(NameFilter), typeof(NameFilter), typeof(Dictionary<NameFilter, List<string>>), typeof(string), typeof(string), typeof(string), typeof(string) })]
    public static class Patch_FamilyBabyNames
    {
        public static void Prefix(Pawn pawn, NameFilter editableNames, ref Dictionary<NameFilter, List<string>> suggestedNames)
        {
            if (!RimfolkPatches.On || pawn == null || (editableNames & NameFilter.First) == 0) return;
            try
            {
                if (!pawn.DevelopmentalStage.Baby() || pawn.babyNamingDeadline < Find.TickManager.TicksGame) return;
                var names = FamilyLife.NameSuggestions(pawn);
                if (names.Count == 0) return;
                suggestedNames ??= new Dictionary<NameFilter, List<string>>();
                if (suggestedNames.TryGetValue(NameFilter.First, out var had) && had != null)
                    names = had.Concat(names).Distinct().ToList();
                suggestedNames[NameFilter.First] = names;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("family baby names", e);
            }
        }
    }

    /// <summary>A birthday the family marks (Social/FamilyLife.cs).</summary>
    [HarmonyPatch(typeof(Pawn_AgeTracker), "BirthdayBiological")]
    public static class Patch_FamilyBirthday
    {
        public static void Postfix(Pawn ___pawn, int birthdayAge)
        {
            if (!RimfolkPatches.On) return;
            try
            {
                FamilyLife.Notify_Birthday(___pawn, birthdayAge);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("family birthday hook", e);
            }
        }
    }
}
