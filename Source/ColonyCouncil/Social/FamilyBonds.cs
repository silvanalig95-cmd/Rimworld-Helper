using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The family's days together, beyond the big moments (the pure part is FamilyLifeMath.cs). Three or more of a
    /// family at home now and then sit down to supper together at one table, the eldest at its head, with a meal from
    /// the stores each (a feast of their own, PlayOut/). Grown brothers and sisters have their own moments, with no
    /// parent in it: close ones share a word about the old days, and rivals get under each other's skin, said when the
    /// two are together. A grown child looks in on an ageing parent now and then, more often when they're ill: they sit
    /// down together a while, at the bedside if the parent is laid up. Nothing is saved: after a reload the family's
    /// times simply come round again.
    /// </summary>
    public static class FamilyBonds
    {
        private static ThoughtDef T(string name) => DefDatabase<ThoughtDef>.GetNamedSilentFail(name);

        private static bool Ours(Pawn p) => p != null && !p.Dead && p.IsColonist && People.HasInnerLife(p) && !p.IsQuestLodger();

        private static bool Grown(Pawn p) => !p.DevelopmentalStage.Juvenile() && Flavor.HumanAge(p) >= FamilyLifeMath.AdultAge;

        /// <summary>When each pair or family last had its moment (this session), by key.</summary>
        private static readonly Dictionary<string, int> last = new Dictionary<string, int>();

        /// <summary>Each map's next look at the day's family times, by map id: once a day each.</summary>
        private static readonly Dictionary<int, int> nextDay = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> nextSupper = new Dictionary<int, int>();

        public static void Reset()
        {
            last.Clear();
            nextDay.Clear();
            nextSupper.Clear();
        }

        private static bool Recent(string key, int days, int now) => last.TryGetValue(key, out int t) && now - t < days * GenDate.TicksPerDay;

        private static void Mark(string key, int now)
        {
            last[key] = now;
            if (last.Count > 300) last.Remove(last.OrderBy(x => x.Value).First().Key);
        }

        /// <summary>Once a day in its hours, per map.</summary>
        private static bool Due(Dictionary<int, int> next, Map map, int hourFrom, int hourTo, int now)
        {
            int hour = GenLocalDate.HourOfDay(map);
            if (hour < hourFrom || hour > hourTo) return false;
            if (next.TryGetValue(map.uniqueID, out int t) && now < t) return false;
            next[map.uniqueID] = now + GenDate.TicksPerHour * 20;
            return true;
        }

        /// <summary>The council's hour: a family supper planned for the evening, the day's moments between grown siblings, and a parent looked in on.</summary>
        public static void Hourly(CouncilGameComponent comp)
        {
            if (comp == null || Current.ProgramState != ProgramState.Playing) return;
            int now = Find.TickManager.TicksGame;
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome).ToList())
            {
                var home = map.mapPawns.FreeColonistsSpawned.Where(Ours).ToList();
                if (home.Count < 2) continue;
                if (Due(nextDay, map, 9, 12, now))
                {
                    Siblings(home, now);
                    Parents(map, home, now);
                }
                if (Due(nextSupper, map, 14, 16, now)) Supper(map, home, now);
            }
        }

        // ---------------- A family supper ----------------

        /// <summary>
        /// A family of three or more at home, with a meal in the stores for each, sits down to supper together this
        /// evening now and then: the eldest at the head of the table, the rest of their family round it (one family a day).
        /// </summary>
        private static void Supper(Map map, List<Pawn> home, int now)
        {
            if (!PlayOuts.On) return;
            var people = home.Where(p => !p.DevelopmentalStage.Baby()).ToList();
            var links = new List<(int, int)>();
            for (int i = 0; i < people.Count; i++)
                for (int j = i + 1; j < people.Count; j++)
                    if (Family.Of(people[i], people[j]) || LovePartnerRelationUtility.LovePartnerRelationExists(people[i], people[j])) links.Add((i, j));
            foreach (var c in FamilyLifeMath.Clusters(people.Count, links).Where(c => c.Count >= FamilyLifeMath.SupperAtLeast).OrderBy(_ => Rand.Value))
            {
                var members = c.Select(i => people[i]).ToList();
                var head = members.Where(Grown).OrderByDescending(p => p.ageTracker.AgeBiologicalTicks).FirstOrDefault();
                if (head == null) continue;
                // At the table: the head of it and their own family, and their partner.
                var table = members.Where(m => m == head || Family.Of(m, head) || LovePartnerRelationUtility.LovePartnerRelationExists(m, head)).ToList();
                string key = "supper|" + head.thingIDNumber;
                if (Recent(key, FamilyLifeMath.SupperGapDays, now) || !FamilyLifeMath.Supper(table.Count, AdvisorFacts.Meals(map))) continue;
                if (!Rand.Chance(FamilyLifeMath.SupperChance)) continue;
                string family = FamilyNames.Plural(head);
                var feast = new DilemmaEffect_Feast
                {
                    target = DilemmaTarget.FamilyOfA, organizer = PlayWho.A, pool = "Council_Pool_PlayFamilySupper", hours = new IntRange(17, 20),
                    duration = new FloatRange(1.5f, 2f), waitHours = 3f, spots = new List<PlaySpot> { PlaySpot.Dining },
                    label = family != null ? "CC_Fam_SupperLabel".Translate(family.Named("FAMILY")).Resolve() : "CC_Fam_SupperLabelPlain".Translate().Resolve()
                };
                var play = feast.Begin(new DilemmaContext { map = map, a = head });
                Mark(key, now);
                if (play == null) return;
                string cause = "CC_Fam_SupperCause".Translate(head.Named("A")).Resolve();
                var thought = T("Council_Fam_FamilySupper");
                if (thought != null)
                    foreach (var m in play.invited) play.moods.Add(new HeldMood { pawn = m, thought = thought, cause = cause });
                return;
            }
        }

        // ---------------- Grown brothers and sisters ----------------

        private static bool Siblings(Pawn a, Pawn b)
        {
            try
            {
                return a.GetRelations(b).Any(r => r == PawnRelationDefOf.Sibling || r == PawnRelationDefOf.HalfSibling);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Grown brothers and sisters at home, now and then: close ones (each thinks well of the other) share a word
        /// about old times; rivals (one of them can't stand the other) have a dig, and it stings. When the two are together.
        /// </summary>
        private static void Siblings(List<Pawn> home, int now)
        {
            var grown = home.Where(Grown).ToList();
            for (int i = 0; i < grown.Count; i++)
                for (int j = i + 1; j < grown.Count; j++)
                {
                    var a = grown[i];
                    var b = grown[j];
                    string key = "sibling|" + FamilyNames.PairKey(a, b);
                    if (Recent(key, FamilyLifeMath.SiblingGapDays, now) || !Siblings(a, b)) continue;
                    var take = FamilyLifeMath.Siblings(a.relations.OpinionOf(b), b.relations.OpinionOf(a));
                    if (take == SiblingTake.None || !Rand.Chance(FamilyLifeMath.SiblingChance)) continue;
                    Mark(key, now);
                    // The one who says it: for rivals, the one who thinks less of the other; for close ones, either.
                    var (speaker, to) = take == SiblingTake.Rival
                        ? a.relations.OpinionOf(b) <= b.relations.OpinionOf(a) ? (a, b) : (b, a)
                        : Rand.Bool ? (a, b) : (b, a);
                    Together.When(key, speaker, to, () => SiblingMoment(speaker, to, take), 18f);
                }
        }

        private static void SiblingMoment(Pawn speaker, Pawn to, SiblingTake take)
        {
            // together: Together.When runs this only when the two are together, and Speech.Say asks again.
            if (speaker.Dead || to.Dead) return;
            // How they get on may have changed while it waited.
            if (FamilyLifeMath.Siblings(speaker.relations.OpinionOf(to), to.relations.OpinionOf(speaker)) != take) return;
            string relation = Family.Relation(speaker, to);
            string line;
            if (take == SiblingTake.Close)
            {
                string cause = "CC_Fam_SiblingCloseCause".Translate(speaker.Named("A"), to.Named("B")).Resolve();
                CouncilGameComponent.GiveThought(speaker, T("Council_Fam_SiblingClose"), to, -1f, cause);
                CouncilGameComponent.GiveThought(to, T("Council_Fam_SiblingClose"), speaker, -1f, cause);
                line = Flavor.FromPool("Council_Pool_SaySiblingClose", speaker);
            }
            else
            {
                CouncilGameComponent.GiveThought(to, T("Council_Fam_SiblingRivalry"), speaker, -1f, "CC_Fam_SiblingRivalCause".Translate(speaker.Named("A")).Resolve());
                line = Flavor.FromPool("Council_Pool_SaySiblingRival", speaker);
            }
            if (!line.NullOrEmpty()) Speech.Say(speaker, line.Formatted(to.Named("OTHER"), relation.Named("RELATION")).Resolve(), to);
        }

        // ---------------- An ageing parent ----------------

        /// <summary>Ill or laid up: sick, wanting bed rest, or down.</summary>
        private static bool Ill(Pawn p)
        {
            try
            {
                return p.Downed || (p.health?.hediffSet?.AnyHediffMakesSickThought ?? false) || HealthAIUtility.ShouldSeekMedicalRest(p);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// A parent of 60 or more at home, now and then (more often when they're ill): the grown child fondest of them
        /// goes to sit with them a while, at the bedside if they're laid up (PlayOut/: a visit). Each parent once a day at most.
        /// </summary>
        private static void Parents(Map map, List<Pawn> home, int now)
        {
            if (!PlayOuts.On) return;
            foreach (var parent in home.Where(Grown).ToList())
            {
                bool ill = Ill(parent);
                float chance = FamilyLifeMath.LookInChance(Flavor.HumanAge(parent), ill);
                if (chance <= 0f) continue;
                int gap = FamilyLifeMath.LookInGapDays(ill);
                var child = Family.ChildrenOf(parent)
                    .Where(c => c != parent && home.Contains(c) && Grown(c) && !c.Downed && c.relations.OpinionOf(parent) >= 0
                                && !Recent("lookin|" + c.thingIDNumber + "|" + parent.thingIDNumber, gap, now))
                    .OrderByDescending(c => c.relations.OpinionOf(parent)).FirstOrDefault();
                if (child == null || !Rand.Chance(chance)) continue;
                Mark("lookin|" + child.thingIDNumber + "|" + parent.thingIDNumber, now);
                var play = new DilemmaEffect_Visit
                {
                    who = PlayWho.A, to = PlaySpot.Person, whom = PlayWho.B, sit = true, label = "CC_Fam_LookInLabel".Translate(), hours = new IntRange(9, 20),
                    duration = new FloatRange(1f, 1.5f), waitHours = 8f
                }.Begin(new DilemmaContext { map = map, a = child, b = parent });
                if (play == null) continue;
                string cause = (ill ? "CC_Fam_LookInIllCause" : "CC_Fam_LookInCause").Translate(child.Named("A"), parent.Named("B")).Resolve();
                play.moods.Add(new HeldMood { pawn = parent, thought = T("Council_Fam_ChildLookedIn"), about = child, cause = cause });
                play.moods.Add(new HeldMood { pawn = child, thought = T("Council_Fam_LookedAfterParent"), about = parent, cause = cause });
                play.moods.RemoveAll(m => m.thought == null);
            }
        }
    }
}
