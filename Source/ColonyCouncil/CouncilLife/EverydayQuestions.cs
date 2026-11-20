using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // Twenty-five council questions of the colony's everyday life (colony life's variety, part 1; the player's go of 20
    // November 2026: "dilemmas"), in Defs/DilemmaDefs/Rimfolk_Dilemmas_Everyday.xml: work and rest, food and the stores,
    // sickness and care, the young and the old, animals, guests and strangers, quarrels and friendships, the seasons. Each
    // sets two of the colony toward or against each other, comes only while the colony is as its letter says, and each
    // answer's words happen on the map with the play-outs the council's questions already use. The workers here find the
    // people and things the roles can't (a craft only one knows, a dirty sickroom, a newborn animal without a name); the
    // others are role-only. The thresholds are EverydayMath.

    /// <summary>Look-ups the everyday questions share.</summary>
    public static class Everyday
    {
        /// <summary>The colony's grown people at home who can take part in a question now.</summary>
        public static List<Pawn> Home(Map map) =>
            map?.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger() && !p.Downed && !p.InMentalState).ToList() ?? new List<Pawn>();

        public static List<Pawn> Grown(Map map) => Home(map).Where(p => !p.DevelopmentalStage.Juvenile()).ToList();

        public static int Level(Pawn p, SkillDef s) => ArcUtil.Level(p, s);

        /// <summary>The game's cooking work (no WorkTypeDefOf field for it).</summary>
        public static WorkTypeDef Cooking => DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");

        public static bool Passion(Pawn p, SkillDef s) => p?.skills?.GetSkill(s) is SkillRecord r && !r.TotallyDisabled && People.Passionate(r);

        /// <summary>The work is theirs now (a priority set, not disabled).</summary>
        public static bool Does(Pawn p, WorkTypeDef w) => w != null && p?.workSettings != null && p.workSettings.EverWork && !p.WorkTypeIsDisabled(w) && p.workSettings.GetPriority(w) > 0;

        /// <summary>The work could be theirs, but isn't (able for it, no priority set).</summary>
        public static bool CouldDo(Pawn p, WorkTypeDef w) => w != null && p?.workSettings != null && p.workSettings.EverWork && !p.WorkTypeIsDisabled(w) && p.workSettings.GetPriority(w) == 0;

        public static int Degree(Pawn p, TraitDef t) => t != null && (p?.story?.traits?.HasTrait(t) ?? false) ? p.story.traits.DegreeOfTrait(t) : 0;

        /// <summary>Ripe plants in the colony's fields waiting to be brought in (its own crop, not blighted).</summary>
        public static int Ripe(Map map)
        {
            int n = 0;
            foreach (var z in map.zoneManager.AllZones.OfType<Zone_Growing>())
            {
                var crop = z.GetPlantDefToGrow();
                foreach (var c in z.Cells)
                    if (c.GetPlant(map) is Plant p && p.def == crop && p.HarvestableNow && !p.Blighted) n++;
            }
            return n;
        }

        /// <summary>Fine meals and better in the colony's stores.</summary>
        public static int FineMeals(Map map) =>
            PlaySpots.Meals(map).Where(t => t.def.ingestible != null && t.def.ingestible.preferability >= FoodPreferability.MealFine).Sum(t => t.stackCount);

        /// <summary>A room with a patient abed in a medical bed, and how much filth lies in it.</summary>
        public static Room Sickroom(Map map, out int filth)
        {
            filth = 0;
            Room best = null;
            foreach (var bed in map.listerBuildings.allBuildingsColonist.OfType<Building_Bed>())
            {
                if (!bed.Medical || !bed.CurOccupants.Any()) continue;
                var room = bed.GetRoom();
                if (room == null || room.PsychologicallyOutdoors || room == best) continue;
                int f = map.listerFilthInHomeArea.FilthInHomeArea.Count(x => x.Spawned && x.GetRoom() == room);
                if (f <= filth) continue;
                filth = f;
                best = room;
            }
            return best;
        }

        /// <summary>A bed of a sickroom the colony's cleaners would start from (PlayWork.At "sickroom").</summary>
        public static IntVec3 SickroomCell(Map map)
        {
            var room = Sickroom(map, out _);
            var bed = room?.ContainedBeds.FirstOrDefault(b => b.Medical);
            return bed?.Position ?? IntVec3.Invalid;
        }
    }

    /// <summary>The only one who knows how: A far ahead of the rest at a craft, B wanting to learn it ({SKILL}).</summary>
    public class DilemmaWorker_OnlyOneKnows : DilemmaWorker
    {
        private static readonly string[] Crafts = { "Crafting", "Construction", "Cooking", "Medicine", "Artistic", "Plants", "Animals", "Intellectual" };

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var home = Everyday.Grown(map);
            if (home.Count < def.minColonists) return false;
            foreach (var name in Crafts.InRandomOrder())
            {
                var s = DefDatabase<SkillDef>.GetNamedSilentFail(name);
                if (s == null) continue;
                var ranked = home.OrderByDescending(p => Everyday.Level(p, s)).ToList();
                var a = ranked[0];
                int next = ranked.Count > 1 ? Everyday.Level(ranked[1], s) : 0;
                if (!EverydayMath.OnlyOne(Everyday.Level(a, s), next)) continue;
                var b = home.Where(p => p != a && Everyday.Passion(p, s) && def.roleB?.Allows(p, a) != false).RandomElementWithFallback();
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b, skill = s };
                return true;
            }
            return false;
        }
    }

    /// <summary>The broom and the bucket: A does the cleaning, B could and never does, and the home area is dirty.</summary>
    public class DilemmaWorker_Broom : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => base.CanHappenNow(map) && WishWorker_Tidy.Filth(map) >= 30;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var home = Everyday.Grown(map);
            if (home.Count < def.minColonists) return false;
            var a = home.Where(p => Everyday.Does(p, WorkTypeDefOf.Cleaning)).InRandomOrder().FirstOrDefault();
            var b = home.Where(p => p != a && Everyday.CouldDo(p, WorkTypeDefOf.Cleaning)).RandomElementWithFallback();
            if (a == null || b == null) return false;
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b };
            return true;
        }
    }

    /// <summary>Dawn to dark: A, a hard worker strained by it, and B, a friend who wants them to stop.</summary>
    public class DilemmaWorker_DawnToDark : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!Heart.On || !CanHappenNow(map)) return false;
            var home = Everyday.Grown(map);
            if (home.Count < def.minColonists) return false;
            var industrious = DefDatabase<TraitDef>.GetNamedSilentFail("Industriousness");
            foreach (var a in home.Where(p => (Everyday.Degree(p, industrious) >= 1 || PeopleTags.MatchesNow(p, "trait:VTE_Workaholic")) && Heart.StrainOf(p) >= EverydayMath.LongDaysStrain).InRandomOrder())
            {
                var b = home.Where(p => p != a && ArcUtil.Opinion(p, a) >= 20).OrderByDescending(p => ArcUtil.Opinion(p, a)).FirstOrDefault();
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b };
                return true;
            }
            return false;
        }
    }

    /// <summary>Every hand to the harvest: forty ripe plants or more waiting; A a grower, B someone at a bench who doesn't grow.</summary>
    public class DilemmaWorker_HarvestHands : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => base.CanHappenNow(map) && Everyday.Ripe(map) >= EverydayMath.RipeWaiting;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var home = Everyday.Grown(map);
            if (home.Count < def.minColonists) return false;
            var a = home.Where(p => Everyday.Does(p, WorkTypeDefOf.Growing)).OrderByDescending(p => Everyday.Level(p, SkillDefOf.Plants)).FirstOrDefault();
            var b = home.Where(p => p != a && !Everyday.Does(p, WorkTypeDefOf.Growing)
                                         && (Everyday.Passion(p, SkillDefOf.Crafting) || Everyday.Passion(p, SkillDefOf.Intellectual) || Everyday.Passion(p, SkillDefOf.Artistic)))
                .RandomElementWithFallback();
            if (a == null || b == null) return false;
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b, count = Everyday.Ripe(map) };
            return true;
        }
    }

    /// <summary>The good meals: a fine meal or better in store for everyone; A the best cook, B anyone else.</summary>
    public class DilemmaWorker_GoodMeals : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => base.CanHappenNow(map) && EverydayMath.EnoughFine(Everyday.FineMeals(map), Everyday.Home(map).Count);

        protected override bool Extra(DilemmaContext ctx)
        {
            if (!base.Extra(ctx)) return false;
            ctx.count = Everyday.FineMeals(ctx.map);
            return true;
        }
    }

    /// <summary>A second helping: food for under six days, and a gourmand who takes seconds (A, by the role).</summary>
    public class DilemmaWorker_Seconds : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => base.CanHappenNow(map) && MapFacts.Of(map).FoodDays < EverydayMath.FoodShortDays;
    }

    /// <summary>A helper in the kitchen: A the colony's cook, B keen to cook and well behind them.</summary>
    public class DilemmaWorker_KitchenHelp : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var home = Everyday.Grown(map);
            if (home.Count < def.minColonists) return false;
            var a = home.Where(p => Everyday.Does(p, Everyday.Cooking)).OrderByDescending(p => Everyday.Level(p, SkillDefOf.Cooking)).FirstOrDefault();
            if (a == null) return false;
            int lvl = Everyday.Level(a, SkillDefOf.Cooking);
            var b = home.Where(p => p != a && Everyday.Passion(p, SkillDefOf.Cooking) && !p.WorkTypeIsDisabled(Everyday.Cooking) && Everyday.Level(p, SkillDefOf.Cooking) <= lvl - 4)
                .RandomElementWithFallback();
            if (b == null) return false;
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b, skill = SkillDefOf.Cooking };
            return true;
        }
    }

    /// <summary>Laid up and bored: A two days or more in a sickbed and short of recreation, B a friend up and about.</summary>
    public class DilemmaWorker_BoredAbed : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var all = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            if (all.Count < def.minColonists) return false;
            foreach (var a in all.Where(p => Life.LaidUpDays(p) >= EverydayMath.BedDays && (p.needs?.joy?.CurLevelPercentage ?? 1f) < EverydayMath.BoredJoy).InRandomOrder())
            {
                var b = Everyday.Grown(map).Where(p => p != a && ArcUtil.Opinion(p, a) >= 20).OrderByDescending(p => ArcUtil.Opinion(p, a)).FirstOrDefault();
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b };
                return true;
            }
            return false;
        }
    }

    /// <summary>The doctor's rounds: A the colony's doctor, B able to doctor and not doing it, someone at home hurt or ill.</summary>
    public class DilemmaWorker_DoctorsRounds : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) =>
            base.CanHappenNow(map) && map.mapPawns.FreeColonistsSpawned.Any(p => LifeChecks.Illness(p) != null || LifeChecks.WoundSeverity(p) >= LifeChecks.HurtFrom);

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var home = Everyday.Grown(map);
            if (home.Count < def.minColonists) return false;
            var a = home.Where(p => Everyday.Does(p, WorkTypeDefOf.Doctor)).OrderByDescending(p => Everyday.Level(p, SkillDefOf.Medicine)).FirstOrDefault();
            var b = home.Where(p => p != a && Everyday.CouldDo(p, WorkTypeDefOf.Doctor) && Everyday.Level(p, SkillDefOf.Medicine) >= 5).RandomElementWithFallback();
            if (a == null || b == null) return false;
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b, skill = SkillDefOf.Medicine };
            return true;
        }
    }

    /// <summary>The sickroom: filth in a room with a patient abed; A the doctor, B one of the cleaners.</summary>
    public class DilemmaWorker_Sickroom : DilemmaWorker
    {
        public override bool CanHappenNow(Map map)
        {
            if (!base.CanHappenNow(map)) return false;
            Everyday.Sickroom(map, out int filth);
            return filth >= EverydayMath.SickroomFilth;
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var home = Everyday.Grown(map);
            if (home.Count < def.minColonists) return false;
            var a = home.Where(p => Everyday.Does(p, WorkTypeDefOf.Doctor)).OrderByDescending(p => Everyday.Level(p, SkillDefOf.Medicine)).FirstOrDefault();
            var b = home.Where(p => p != a && Everyday.Does(p, WorkTypeDefOf.Cleaning)).RandomElementWithFallback();
            if (a == null || b == null) return false;
            Everyday.Sickroom(map, out int filth);
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b, count = filth };
            return true;
        }
    }

    /// <summary>A child and an animal: A a child, {PET} a tame animal of the colony's that is safe with children, B the best with animals.</summary>
    public class DilemmaWorker_ChildAndBeast : DilemmaWorker
    {
        public static bool Gentle(Pawn x) =>
            x != null && x.Spawned && !x.Dead && !x.Downed && x.RaceProps.Animal && x.Faction == Faction.OfPlayer && !x.RaceProps.predator
            && x.BodySize <= 1.2f && !x.InMentalState && (x.ageTracker?.CurLifeStageIndex ?? 0) > 0;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!ModsConfig.BiotechActive || !CanHappenNow(map)) return false;
            var all = map.mapPawns.FreeColonistsSpawned.Where(p => !p.IsQuestLodger()).ToList();
            if (all.Count < def.minColonists) return false;
            var a = all.Where(p => p.DevelopmentalStage.Child() && !p.Downed && !p.InMentalState).RandomElementWithFallback();
            var pet = map.mapPawns.SpawnedColonyAnimals.Where(Gentle).RandomElementWithFallback();
            var b = Everyday.Grown(map).Where(p => p != a && !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling)).OrderByDescending(p => Everyday.Level(p, SkillDefOf.Animals)).FirstOrDefault();
            if (a == null || pet == null || b == null) return false;
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b, pet = pet, skill = SkillDefOf.Animals };
            return true;
        }
    }

    /// <summary>A chair for old bones: A sixty or more with a room of their own (by the role), B the best builder.</summary>
    public class DilemmaWorker_ElderChair : DilemmaWorker
    {
        protected override bool Extra(DilemmaContext ctx) => base.Extra(ctx) && RoomUtil.PrivateRoom(ctx.a) != null;
    }

    /// <summary>The same hands with the herd: A the colony's handler, B able and not handling, four animals or more.</summary>
    public class DilemmaWorker_HerdHands : DilemmaWorker
    {
        public override bool CanHappenNow(Map map) => base.CanHappenNow(map) && map.mapPawns.SpawnedColonyAnimals.Count >= 4;

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var home = Everyday.Grown(map);
            if (home.Count < def.minColonists) return false;
            var a = home.Where(p => Everyday.Does(p, WorkTypeDefOf.Handling)).OrderByDescending(p => Everyday.Level(p, SkillDefOf.Animals)).FirstOrDefault();
            var b = home.Where(p => p != a && Everyday.CouldDo(p, WorkTypeDefOf.Handling) && Everyday.Level(p, SkillDefOf.Animals) >= 4).RandomElementWithFallback();
            if (a == null || b == null) return false;
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b, skill = SkillDefOf.Animals };
            return true;
        }
    }

    /// <summary>
    /// A name for the newborn: an animal of the colony's born in the last days without a name, and no herdmaster at home to
    /// name it (they name newborns themselves); A the one closest to its mother (her master, else the best with animals), B
    /// someone else; {PET} the newborn, {NAMEA} and {NAMEB} the two names, {KIND} what it is.
    /// </summary>
    public class DilemmaWorker_NameNewborn : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map) || AnimalFacts.HerdmasterAt(map) != null) return false;
            var home = Everyday.Home(map);
            if (home.Count < def.minColonists) return false;
            var baby = map.mapPawns.SpawnedColonyAnimals.Where(x => !x.Dead && (x.Name == null || x.Name.Numerical)
                                                                     && x.ageTracker.AgeBiologicalTicks <= EverydayMath.NewbornDays * GenDate.TicksPerDay).RandomElementWithFallback();
            if (baby == null) return false;
            var mother = baby.GetMother();
            var a = mother?.playerSettings?.Master is Pawn m && home.Contains(m) ? m : home.Where(p => !p.DevelopmentalStage.Juvenile()).OrderByDescending(p => Everyday.Level(p, SkillDefOf.Animals)).FirstOrDefault();
            // A child of the colony first, if there is one: they have opinions about names.
            var b = home.Where(p => p != a && p.DevelopmentalStage.Child()).RandomElementWithFallback() ?? home.Where(p => p != a).RandomElementWithFallback();
            if (a == null || b == null) return false;
            string na = NameFor(baby), nb = NameFor(baby);
            for (int i = 0; i < 4 && nb == na; i++) nb = NameFor(baby);
            if (na.NullOrEmpty() || nb.NullOrEmpty() || na == nb) return false;
            ctx = new DilemmaContextLife { def = def, map = map, a = a, b = b, pet = baby, nameA = na, nameB = nb };
            return true;
        }

        public static string NameFor(Pawn animal)
        {
            try
            {
                return PawnBioAndNameGenerator.GeneratePawnName(animal, NameStyle.Full)?.ToStringShort;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a name for the newborn", e);
                return null;
            }
        }

    }

    /// <summary>The newborn is given the name of A's or B's choosing (<see cref="whose"/>): its name in the game.</summary>
    public class DilemmaEffect_NameAnimal : DilemmaEffect
    {
        public PlayWho whose = PlayWho.A;

        public override bool ChanceEach => false;

        private string Name(DilemmaContext ctx) => !(ctx is DilemmaContextLife l) ? null : whose == PlayWho.B ? l.nameB : l.nameA;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            string n = Name(ctx);
            if (ctx?.pet == null || n.NullOrEmpty()) return;
            sheet.Play("CC_Impact_NameAnimal".Translate(ctx.pet.LabelShort, n).Resolve(), "CC_Impact_NameAnimalBrief".Translate(n).Resolve());
        }

        public override void Apply(DilemmaContext ctx)
        {
            string n = Name(ctx);
            var pet = ctx?.pet;
            if (pet == null || pet.Dead || n.NullOrEmpty() || !(pet.Name == null || pet.Name.Numerical)) return;
            pet.Name = new NameSingle(n);
            if (pet.Spawned)
                Tell.Message(NewsKind.Land, Importance.Notable, pet.Map, "CC_Life_Named".Translate(pet.Named("PET"), AnimalFacts.KindLabel(pet).Named("KIND")).Resolve(), new LookTargets(pet),
                    MessageTypeDefOf.PositiveEvent);
        }
    }

    /// <summary>Two hands for one animal: {PET} a grown animal of the colony's with no master, A and B both able to handle it.</summary>
    public class DilemmaWorker_WhoseBeast : DilemmaWorker
    {
        public static bool Free(Pawn x) =>
            x != null && x.Spawned && !x.Dead && !x.Downed && x.RaceProps.Animal && x.Faction == Faction.OfPlayer && x.playerSettings != null && x.playerSettings.Master == null
            && x.training != null && x.training.CanAssignToTrain(TrainableDefOf.Obedience).Accepted && !x.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond)
            && x.ageTracker.CurLifeStage?.reproductive != false;

        private static bool CanHandle(Pawn p, Pawn x) =>
            p.workSettings != null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling) && Everyday.Level(p, SkillDefOf.Animals) >= TrainableUtility.MinimumHandlingSkill(x);

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var home = Everyday.Grown(map);
            if (home.Count < def.minColonists) return false;
            foreach (var x in map.mapPawns.SpawnedColonyAnimals.Where(Free).InRandomOrder())
            {
                var able = home.Where(p => CanHandle(p, x)).InRandomOrder().Take(2).ToList();
                if (able.Count < 2) continue;
                ctx = new DilemmaContext { def = def, map = map, a = able[0], b = able[1], pet = x };
                return true;
            }
            return false;
        }
    }

    /// <summary>An old friend left out: A jealous, B their friend, who has a closer friend now at home.</summary>
    public class DilemmaWorker_OldFriendLeftOut : DilemmaWorker
    {
        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            var social = CouncilGameComponent.Instance?.social;
            if (social == null || !ColonyCouncilMod.Settings.socialWeb || !CanHappenNow(map)) return false;
            var home = Everyday.Grown(map);
            if (home.Count < def.minColonists) return false;
            foreach (var a in home.Where(p => p.story?.traits?.HasTrait(TraitDefOf.Jealous) ?? false).InRandomOrder())
                foreach (var b in home.Where(p => p != a && social.Strength(p, a, TieKind.Friends) >= 25f).InRandomOrder())
                {
                    float ours = social.Strength(b, a, TieKind.Friends);
                    if (!home.Any(c => c != a && c != b && social.Strength(b, c, TieKind.Friends) > ours)) continue;
                    ctx = new DilemmaContext { def = def, map = map, a = a, b = b };
                    return true;
                }
            return false;
        }
    }

    /// <summary>The first warm day: spring, warm out of doors in the middle of the day; A in good spirits, B a hard worker or a grower.</summary>
    public class DilemmaWorker_WarmDay : DilemmaWorker
    {
        public override bool CanHappenNow(Map map)
        {
            if (!base.CanHappenNow(map) || GenLocalDate.Season(map) != Season.Spring) return false;
            int hour = GenLocalDate.HourOfDay(map);
            return hour >= 9 && hour <= 15 && map.mapTemperature.OutdoorTemp >= EverydayMath.WarmDay && map.weatherManager.RainRate < 0.1f;
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var home = Everyday.Grown(map);
            if (home.Count < def.minColonists) return false;
            var industrious = DefDatabase<TraitDef>.GetNamedSilentFail("Industriousness");
            var b = home.Where(p => Everyday.Degree(p, industrious) >= 1 || Everyday.Does(p, WorkTypeDefOf.Growing)).RandomElementWithFallback();
            var a = home.Where(p => p != b && (p.needs?.mood?.CurLevelPercentage ?? 0f) >= 0.5f).RandomElementWithFallback();
            if (a == null || b == null) return false;
            ctx = new DilemmaContext { def = def, map = map, a = a, b = b };
            return true;
        }
    }

    /// <summary>A cold bedroom: A's own room cold, B's own room clearly warmer, each sleeping alone in their bed.</summary>
    public class DilemmaWorker_ColdBedroom : DilemmaWorker
    {
        private static Room Own(Pawn p)
        {
            var room = RoomUtil.BedRoom(p);
            return room != null && room.ProperRoom && !room.PsychologicallyOutdoors && !room.IsHuge && p.ownership?.OwnedBed is Building_Bed bed && bed.OwnersForReading.Count == 1 ? room : null;
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var home = Everyday.Grown(map);
            if (home.Count < def.minColonists) return false;
            foreach (var a in home.InRandomOrder())
            {
                var ra = Own(a);
                if (ra == null || ra.Map != map) continue;
                foreach (var b in home.Where(p => p != a).InRandomOrder())
                {
                    var rb = Own(b);
                    if (rb == null || rb == ra || !EverydayMath.ColderRoom(ra.Temperature, rb.Temperature)) continue;
                    ctx = new DilemmaContext { def = def, map = map, a = a, b = b };
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>The storm at night: a thunderstorm in the evening; A nervous (or a child), B someone who thinks well of them.</summary>
    public class DilemmaWorker_StormNight : DilemmaWorker
    {
        public override bool CanHappenNow(Map map)
        {
            if (!base.CanHappenNow(map)) return false;
            int hour = GenLocalDate.HourOfDay(map);
            string w = map.weatherManager.curWeather?.defName ?? "";
            return hour >= 18 && hour <= 23 && w.Contains("Thunderstorm");
        }

        public override bool TryCast(Map map, out DilemmaContext ctx)
        {
            ctx = null;
            if (!CanHappenNow(map)) return false;
            var all = Everyday.Home(map);
            if (all.Count < def.minColonists) return false;
            foreach (var a in all.Where(p => p.DevelopmentalStage.Child() || Everyday.Degree(p, DefDatabase<TraitDef>.GetNamedSilentFail("Nerves")) < 0).InRandomOrder())
            {
                var b = all.Where(p => p != a && !p.DevelopmentalStage.Juvenile() && ArcUtil.Opinion(p, a) >= 15).OrderByDescending(p => ArcUtil.Opinion(p, a)).FirstOrDefault();
                if (b == null) continue;
                ctx = new DilemmaContext { def = def, map = map, a = a, b = b };
                return true;
            }
            return false;
        }

        public override void AddTags(DilemmaContext ctx, HashSet<string> tags)
        {
            if (ctx?.a != null && ctx.a.DevelopmentalStage.Child()) tags.Add("achild");
        }
    }
}
