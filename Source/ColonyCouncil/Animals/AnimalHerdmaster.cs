using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // The herdmaster at work in the colony's life, face to face (more of Interplay_Offices.cs, behind the same "Systems
    // talk to each other" setting): naming the newborns at the pens, going round to whoever's animal is sick or hurt, and
    // taking someone keen on animals along to learn.
    public static partial class Interplay
    {
        /// <summary>
        /// A newborn in the herd (Animals/AnimalLife.cs): the herdmaster goes down to the pens when they're free (within the
        /// day) and gives it a name, if it has none yet. The name is the game's own for animals.
        /// </summary>
        public static void Notify_Newborn(CouncilGameComponent comp, Pawn baby, Pawn mother)
        {
            if (!On || comp == null || baby == null || !S.offices) return;
            var hm = AnimalFacts.Herdmaster;
            if (hm == null || hm.Dead || hm.MapHeld != baby.MapHeld || hm.DevelopmentalStage.Juvenile()) return;
            Together.When("newborn|" + baby.thingIDNumber, hm, baby, () =>
            {
                if (!On || AnimalFacts.Herdmaster != hm || baby.Dead || !AnimalFacts.Ours(baby)) return;
                bool named = false;
                if (baby.Name == null || baby.Name.Numerical)
                {
                    try
                    {
                        baby.Name = PawnBioAndNameGenerator.GeneratePawnName(baby, NameStyle.Full);
                        named = baby.Name != null;
                    }
                    catch (System.Exception e)
                    {
                        CouncilLog.ErrorOnce("herdmaster: naming a newborn", e);
                    }
                }
                var tags = new[] { named ? "named" : "nonamed" };
                string line = Line("Council_Pool_Herd_SayNewborn", hm, tags, baby.Named("PET"), mother.Named("OTHER"));
                // together: the herdmaster is at the newborn's side (Together.When), and speaks to it.
                if (!line.NullOrEmpty()) Speech.Say(hm, line, null);
                if (named)
                    News.Post("CC_Herd_Named".Translate(hm.Named("PAWN"), baby.Named("PET"), AnimalFacts.KindLabel(mother).Named("KIND")).Resolve(), new LookTargets(baby),
                        NewsKind.Land, MessageTypeDefOf.SilentInput, Importance.Flavor);
                Journal(hm, "Council_Journal_Herd_Newborn", tags, baby.Named("PET"), mother.Named("OTHER"), AnimalFacts.KindLabel(mother).Named("KIND"));
            }, 24f, 2f);
        }

        /// <summary>
        /// The herdmaster's day, the newer half (beside <see cref="HerdmasterDay"/>): someone's animal is sick or hurt, and
        /// the herdmaster goes to find them in their free time and tells them how it's doing (the heart a little calmer,
        /// a friendship a little warmer); and now and then they take someone keen on animals along for the day's work, a
        /// lesson on the map.
        /// </summary>
        public static void HerdmasterDay2(CouncilGameComponent comp)
        {
            if (!On || !S.offices) return;
            var hm = AnimalFacts.Herdmaster;
            if (hm == null || !hm.Spawned || hm.Map == null || !hm.Map.IsPlayerHome) return;
            Safe("herdmaster: rounds", () => Rounds(comp, hm));
            Safe("herdmaster: a lesson", () => Lesson(hm));
        }

        /// <summary>Strain eased when the herdmaster brings word of someone's sick animal.</summary>
        public const float HerdmasterRounds = 3f;

        private static void Rounds(CouncilGameComponent comp, Pawn hm)
        {
            foreach (var a in hm.Map.mapPawns.SpawnedColonyAnimals.ToList())
            {
                if (!AnimalFacts.Hurt(a) && AnimalFacts.Illness(a) == null) continue;
                var person = PetTracker.PersonOf(a);
                if (person == null || person == hm || person.Dead || person.MapHeld != hm.Map || !Ours(person)) continue;
                if (!Ready("rounds|" + person.thingIDNumber, 3f)) continue;
                var pet = a;
                bool sick = AnimalFacts.Illness(a) != null;
                Together.When("rounds|" + person.thingIDNumber, hm, person, () =>
                {
                    if (!On || AnimalFacts.Herdmaster != hm || pet.Dead) return;
                    if (Heart.Eligible(person)) Ease(person, HerdmasterRounds, StrainRelief.Friends);
                    if (S.socialWeb) comp.social.Bump(person, hm, TieKind.Friends, 2f, "CC_TieNote_Herd_Rounds".Translate(pet.LabelShort), true);
                    string text = Line("Council_Pool_Herd_Rounds", hm, new[] { sick ? "sick" : "hurt" }, person.Named("OTHER"), pet.Named("PET"));
                    if (text.NullOrEmpty()) return;
                    News.Post(text, new LookTargets(hm, person), NewsKind.People, MessageTypeDefOf.SilentInput, Importance.Flavor);
                    SocialLog.Add(hm, person, text);
                }, 24f, 4f);
                return;
            }
        }

        /// <summary>
        /// About once a week: someone with a love of animals who has a good deal still to learn (four levels or more below
        /// the herdmaster) follows the herdmaster about their work for a while (a lesson, played out on the map).
        /// </summary>
        private static void Lesson(Pawn hm)
        {
            if (!PlayOuts.On || !Rand.Chance(0.2f)) return;
            int level = hm.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 0;
            var student = hm.Map.mapPawns.FreeColonistsSpawned
                .Where(p => p != hm && Ours(p) && !p.DevelopmentalStage.Baby() && !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling)
                            && p.skills?.GetSkill(SkillDefOf.Animals) is SkillRecord r && !r.TotallyDisabled && People.Passionate(r) && r.Level <= level - 4)
                .OrderBy(p => p.skills.GetSkill(SkillDefOf.Animals).Level).FirstOrDefault();
            if (student == null || !Ready("herdlesson", 6f)) return;
            var ctx = new DilemmaContext { map = hm.Map, a = student, b = hm, skill = SkillDefOf.Animals };
            new DilemmaEffect_Lesson { skill = SkillDefOf.Animals, label = "CC_Herd_LessonLabel".Translate().Resolve() }.Apply(ctx);
        }
    }
}
