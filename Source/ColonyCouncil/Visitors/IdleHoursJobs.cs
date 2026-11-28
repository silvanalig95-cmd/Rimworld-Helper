using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace ColonyCouncil
{
    /// <summary>On an idle stint's JobDef: which doing it is, how readily they talk with whoever is near meanwhile, and the game's effect shown while it lasts.</summary>
    public class VisitIdleExtension : DefModExtension
    {
        public IdleDoing doing;
        public RandomSocialMode social = RandomSocialMode.Normal;
        public EffecterDef effecter;
        /// <summary>A bar over them for the while it takes (mending, seeing to a weapon): their own work, on their own things.</summary>
        public bool progress;
    }

    /// <summary>
    /// A visitor's idle stint (IdleHours): the walk to the place (the fire's ring, the seat, the game, the art, the shrine,
    /// one of their own or one of the colony's people, their animal, the colony's pet, the table, the target, the water),
    /// then a while there facing it, with the game's own look of it: the horseshoes and stones thrown, the cards, the
    /// screen's glow, the instrument played, the prayer's sign, the food's or the drug's own effect and sound, a pipe's smoke,
    /// chips from a knife; a book carried open as the game's readers carry it, a stick thrown for a dog, blows struck at a
    /// target or at the air, arrows flying at the target with the bow's own sound (Misc. Training's arrow), the fishing line
    /// at the water (Odyssey's), a hand of cards between two of them, notes over a singer. Nothing is reserved; the moment one
    /// of the colony's people sets out for the game, the seat or the place, the visitor stands up and gives it to them. What
    /// they eat, drink or smoke comes out of their own pack at the end, a fish caught goes into it, and a book of the colony's
    /// goes back where it lay.
    /// </summary>
    public class JobDriver_VisitIdle : JobDriver
    {
        private int stint = -1;
        private bool playing;
        [Unsaved] private readonly IdleCheck check = new IdleCheck();
        /// <summary>Sparring with their shadow: when the last blow was struck, and which way.</summary>
        [Unsaved] private int lungeAt = -999;
        [Unsaved] private float lungeAngle;
        [Unsaved] private int nextBlow, throws, nextThrow;
        private static FleckDef musicFleck;
        private static EffecterDef pokerEffecter;
        private static SoundDef catchSound;
        private static bool looked;

        private static readonly VisitIdleExtension Plain = new VisitIdleExtension();

        private VisitIdleExtension Ext => job.def.GetModExtension<VisitIdleExtension>() ?? Plain;

        public IdleDoing Doing => Ext.doing;

        /// <summary>Which form of the doing (IdleHoursMath.Forms): the job's count.</summary>
        private int Form => Math.Max(0, job.count);

        private Pawn Other => job.targetA.Thing as Pawn;

        private Thing Place => job.targetB.Thing;

        private Book TheBook => job.targetA.Thing as Book;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>Nothing reserved: the colony's people find everything free, and the visitor gives way.</summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stint, "stint", -1);
            Scribe_Values.Look(ref playing, "playing");
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            IdleHours.Started(pawn);
        }

        /// <summary>The words for each form: a walk's end, what seeing to the weapon is, which game of their own.</summary>
        private static readonly string[] WalkKeys = { "CC_Idle_Report_Walk0", "CC_Idle_Report_Walk1", "CC_Idle_Report_Walk2", "CC_Idle_Report_Walk3" },
            SharpenKeys = { "CC_Idle_Report_Sharpen0", "CC_Idle_Report_Sharpen1", "CC_Idle_Report_Sharpen2" },
            CardsKeys = { "CC_Idle_Report_Cards0", "CC_Idle_Report_Cards1", "CC_Idle_Report_Cards2" };

        private string FormKey(string[] keys) => keys[Mathf.Clamp(Form, 0, keys.Length - 1)];

        public override string GetReport()
        {
            try
            {
                var d = Doing;
                if ((d == IdleDoing.Game || d == IdleDoing.Music || d == IdleDoing.Stars) && IdleHours.JoyOf(Place?.def)?.job is JobDef j && !j.reportString.NullOrEmpty())
                    return ReportStringProcessed(j.reportString.Replace("TargetA", "TargetB"));
                if (d == IdleDoing.OwnDrug)
                    return (IdleHours.DrugKind(job.thingDefToCarry) == OwnDrugKind.Smoke ? "CC_Idle_Report_Smoke" : "CC_Idle_Report_Drink").Translate().Resolve();
                string key = null;
                string thing = null;
                switch (d)
                {
                    case IdleDoing.Read:
                        key = Form == IdleHoursMath.ReadOwn ? "CC_Idle_Report_ReadOwn" : "CC_Idle_Report_Read";
                        thing = TheBook?.LabelShort;
                        break;
                    case IdleDoing.Walk:
                        key = FormKey(WalkKeys);
                        break;
                    case IdleDoing.Pet:
                        key = Form == 1 ? "CC_Idle_Report_Fetch" : "CC_Idle_Report_Pet";
                        thing = Other?.LabelShort;
                        break;
                    case IdleDoing.Write:
                        key = Form == 1 ? "CC_Idle_Report_Journal" : "CC_Idle_Report_Letter";
                        break;
                    case IdleDoing.Train:
                        key = Form == 1 ? "CC_Idle_Report_Spar" : "CC_Idle_Report_Train";
                        thing = Place?.LabelShort;
                        break;
                    case IdleDoing.Mend:
                        key = "CC_Idle_Report_Mend";
                        thing = job.targetC.Thing?.def?.label;
                        break;
                    case IdleDoing.Sharpen:
                        key = FormKey(SharpenKeys);
                        thing = job.targetC.Thing?.def?.label;
                        break;
                    case IdleDoing.Cards:
                        key = FormKey(CardsKeys);
                        thing = Other?.LabelShort;
                        break;
                }
                if (key != null && key.CanTranslate())
                {
                    // Words that name the thing (the book, the dog, the coat) only when it's known; else the job's own.
                    if (!key.Translate().RawText.Contains("{0}")) return key.Translate().Resolve();
                    if (!thing.NullOrEmpty()) return key.Translate(thing).Resolve();
                }
            }
            catch
            {
                // The job's own words then.
            }
            return base.GetReport();
        }

        private static void Look()
        {
            if (looked) return;
            looked = true;
            musicFleck = DefDatabase<FleckDef>.GetNamedSilentFail("Music");
            pokerEffecter = DefDatabase<EffecterDef>.GetNamedSilentFail("PlayPoker");
            catchSound = DefDatabase<SoundDef>.GetNamedSilentFail("Interact_CatchFish");
        }

        private bool Still() => check.Holds(pawn, Doing, job);

        /// <summary>Sparring with their shadow: the body thrown a little forward with each blow, as a blow at a target shows it.</summary>
        public override Vector3 ForcedBodyOffset
        {
            get
            {
                if (job == null || Doing != IdleDoing.Train || Form != 1) return Vector3.zero;
                int since = Find.TickManager.TicksGame - lungeAt;
                if (since < 0 || since >= 10) return Vector3.zero;
                return Vector3Utility.FromAngleFlat(lungeAngle) * (0.3f * (1f - since / 10f));
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Look();
            var ext = Ext;
            var d = ext.doing;
            if (stint < 0)
            {
                var (lo, hi) = IdleHoursMath.Stint(d);
                stint = Rand.RangeInclusive(lo, hi);
            }
            AddFinishAction(_ =>
            {
                try
                {
                    if (playing && Place is Building_MusicalInstrument m) m.StopPlaying();
                    if (d == IdleDoing.Read) PutBookAway(false);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a visitor's stint ends", e);
                }
                playing = false;
                IdleHours.Ended(pawn);
            });
            this.FailOn(() => !Still());
            if (d == IdleDoing.Read)
            {
                foreach (var t in ReadToils()) yield return t;
                yield break;
            }
            if (d == IdleDoing.Walk)
            {
                foreach (var t in WalkToils()) yield return t;
                yield break;
            }
            bool withSomeone = d == IdleDoing.Chat || d == IdleDoing.Animal || d == IdleDoing.Company || d == IdleDoing.Pet || d == IdleDoing.Cards;
            if (withSomeone)
            {
                this.FailOnDespawnedOrNull(TargetIndex.A);
                this.FailOn(() => Other == null || Other.Dead || Other.Downed || !Other.Awake());
                if (d == IdleDoing.Company) this.FailOn(() => !SmallHabits.CanListen(Other));
                // The colony's animal is let be the moment it is wanted: led on a rope, in a group of the colony's (a caravan
                // forming, a gathering) or on an order the player gave it.
                if (d == IdleDoing.Pet)
                    this.FailOn(() => Other.InMentalState || Other.Drafted || (Other.needs?.food?.CurCategory ?? HungerCategory.Fed) >= HungerCategory.Hungry
                                      || (Other.roping?.IsRoped ?? false) || (Other.CurJob?.playerForced ?? false) || Verse.AI.Group.LordUtility.GetLord(Other) != null);
                var go = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
                if (d == IdleDoing.Pet)
                    go.AddPreTickIntervalAction(delta =>
                    {
                        if (pawn.IsHashIntervalTick(60, delta)) GiveWayIfWanted();
                    });
                yield return go;
            }
            else
            {
                if (job.targetB.HasThing) this.FailOn(() => Place == null || !Place.Spawned || Place.IsBurning());
                if (d == IdleDoing.Mend || d == IdleDoing.Sharpen) this.FailOn(() => !(job.targetC.Thing is Thing own) || own.Destroyed || own.ParentHolder == null);
                // Someone of the colony wants the game, the seat or the place: they give it up before getting there.
                var walk = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
                walk.AddPreTickIntervalAction(delta =>
                {
                    if (pawn.IsHashIntervalTick(60, delta)) GiveWayIfWanted();
                });
                yield return walk;
            }
            Toil stay;
            if (d == IdleDoing.Chat || d == IdleDoing.Animal || d == IdleDoing.Cards || d == IdleDoing.Pet && Form == 0)
            {
                // The one walked over to waits with them, facing them: one of their own, their animal being brushed down, the
                // colony's pet being petted, the other player of the game.
                stay = Toils_General.WaitWith(TargetIndex.A, stint, false, true, false, TargetIndex.A);
            }
            else
            {
                stay = ToilMaker.MakeToil("VisitIdle");
                stay.defaultCompleteMode = ToilCompleteMode.Delay;
                stay.defaultDuration = Mathf.Max(120, stint);
            }
            stay.socialMode = ext.social;
            stay.handlingFacing = true;
            stay.AddPreInitAction(() => Begin(d));
            stay.AddPreTickIntervalAction(delta => During(d, delta));
            var effecter = ext.effecter ?? EffecterOf(d);
            // A game's own effect shows at the game (the cards on the table, the screen's glow), the fishing line at the water,
            // a hand of cards between the two players; the rest where they sit.
            if (effecter != null)
                stay.WithEffect(effecter, d == IdleDoing.Game && job.targetB.HasThing || d == IdleDoing.Fish ? TargetIndex.B : TargetIndex.A);
            if (d == IdleDoing.Snack || d == IdleDoing.OwnDrug)
            {
                var sound = job.thingDefToCarry?.ingestible?.ingestSound;
                if (sound != null) stay.PlaySustainerOrSound(sound);
            }
            if (ext.progress) stay.WithProgressBarToilDelay(TargetIndex.A);
            yield return stay;
            if (d == IdleDoing.Snack || d == IdleDoing.OwnDrug) yield return Toils_General.Do(Consume);
            if (d == IdleDoing.Fish) yield return Toils_General.Do(Catch);
            if (d == IdleDoing.Mend) yield return Toils_General.Do(Mended);
        }

        /// <summary>The game's own effect for it: the food's or the drug's own as it's eaten or had, the cards at a poker table, a screen's glow, the fishing line, a hand of cards.</summary>
        private EffecterDef EffecterOf(IdleDoing d)
        {
            if (d == IdleDoing.Snack || d == IdleDoing.OwnDrug) return job.thingDefToCarry?.ingestible?.ingestEffect;
            if (d == IdleDoing.Fish) return IdleHours.FishingEffect;
            if (d == IdleDoing.Cards) return Form == 2 ? null : pokerEffecter;
            if (d != IdleDoing.Game) return null;
            var place = Place;
            var joy = IdleHours.JoyOf(place?.def);
            if (joy?.job?.driverClass != null && joy.job.driverClass.Name == "JobDriver_PlayPoker") return pokerEffecter;
            return place?.def?.building?.effectWatching;
        }

        private void Begin(IdleDoing d)
        {
            try
            {
                if (d == IdleDoing.Music && Place is Building_MusicalInstrument m)
                {
                    if (m.IsBeingPlayed)
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    m.StartPlaying(pawn);
                    playing = true;
                }
                nextBlow = Now + Rand.RangeInclusive(30, 90);
                nextThrow = Now + 60;
                if (!Rand.Chance(IdleHoursMath.LineChance(d))) return;
                bool drink = d == IdleDoing.OwnDrug && IdleHours.DrugKind(job.thingDefToCarry) == OwnDrugKind.Drink;
                bool watch = d == IdleDoing.Game && (IdleHours.JoyOf(Place?.def)?.watch ?? false);
                // together: one of the colony's people they walked over to, or one of their own they sit down to a game with.
                IdleHours.Say(pawn, IdleHoursMath.Moment(d, drink, watch, Form), d == IdleDoing.Company || d == IdleDoing.Cards ? Other : null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a visitor's idle stint begins", e);
            }
        }

        private void During(IdleDoing d, int delta)
        {
            var map = pawn.Map;
            if (map == null) return;
            if (d == IdleDoing.Company)
            {
                var c = Other;
                if (c != null) pawn.rotationTracker.FaceTarget(c);
                // Not followed about: once they've walked off, the visitor leaves them be.
                if (c == null || !c.Position.InHorDistOf(pawn.Position, IdleHoursMath.CompanyNear))
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }
            }
            else if (d == IdleDoing.Pet || d == IdleDoing.Cards)
            {
                if (Other != null) pawn.rotationTracker.FaceTarget(Other);
            }
            else if (d == IdleDoing.Train && Form == 1)
            {
                // Sparring with their shadow: facing one way, then another, now and then.
                if (pawn.IsHashIntervalTick(400, delta)) pawn.Rotation = Rot4.Random;
            }
            else if (job.targetB.IsValid && d != IdleDoing.Chat && d != IdleDoing.Animal)
            {
                var face = job.targetB.HasThing ? job.targetB.Thing.Position : job.targetB.Cell;
                if (face != pawn.Position) pawn.rotationTracker.FaceCell(face);
            }
            if (pawn.IsHashIntervalTick(60, delta) && GiveWayIfWanted()) return;
            if (d == IdleDoing.Music && playing && Place is Building_MusicalInstrument m && !m.IsBeingPlayed)
            {
                // The game stopped the playing (someone took the instrument over).
                playing = false;
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            if (d == IdleDoing.Pet && Form == 1) Fetch();
            if (d == IdleDoing.Train || d == IdleDoing.Archery) Blows(d);
            if (!VisualTouches.On) return;
            if (d == IdleDoing.Game && pawn.IsHashIntervalTick(400, delta) && Place != null)
            {
                switch (IdleHours.JoyOf(Place.def)?.toss ?? JoyThrow.None)
                {
                    case JoyThrow.Horseshoe:
                        FleckMaker.ThrowHorseshoe(pawn, Place.Position);
                        break;
                    case JoyThrow.Stone:
                        FleckMaker.ThrowStone(pawn, Place.Position);
                        break;
                }
            }
            if (d == IdleDoing.Music && musicFleck != null && Place != null && pawn.IsHashIntervalTick(240, delta)) FleckMaker.ThrowMetaIcon(Place.Position, map, musicFleck);
            if (d == IdleDoing.Sing && musicFleck != null && pawn.IsHashIntervalTick(240, delta)) FleckMaker.ThrowMetaIcon(pawn.Position, map, musicFleck);
            if (d == IdleDoing.Pray && pawn.IsHashIntervalTick(300, delta)) FleckMaker.ThrowMetaIcon(pawn.Position, map, FleckDefOf.Meditating);
            // Brushing an animal down: a little dust off its coat now and then.
            if (d == IdleDoing.Animal && Other != null && pawn.IsHashIntervalTick(300, delta)) FleckMaker.ThrowDustPuff(Other.DrawPos, map, 0.4f);
            // A pet being petted: glad of it.
            if (d == IdleDoing.Pet && Form == 0 && Other != null && pawn.IsHashIntervalTick(360, delta)) FleckMaker.ThrowMetaIcon(Other.Position, map, FleckDefOf.Heart);
            // A game of stones: the stones thrown between the two.
            if (d == IdleDoing.Cards && Form == 2 && Other != null && pawn.IsHashIntervalTick(300, delta)) FleckMaker.ThrowStone(pawn, Other.Position);
            // A blade drawn over a whetstone: a few sparks now and then.
            if (d == IdleDoing.Sharpen && Form == 0 && pawn.IsHashIntervalTick(240, delta)) FleckMaker.ThrowMicroSparks(pawn.DrawPos + pawn.Rotation.FacingCell.ToVector3() * 0.3f, map);
        }

        /// <summary>Blows at the target or at the air, and arrows at the archery target, each in its time.</summary>
        private void Blows(IdleDoing d)
        {
            int now = Now;
            if (now < nextBlow) return;
            var map = pawn.Map;
            if (d == IdleDoing.Archery)
            {
                nextBlow = now + Rand.RangeInclusive(160, 260);
                ShootArrow();
                return;
            }
            nextBlow = now + Rand.RangeInclusive(55, 100);
            if (Form == 1)
            {
                lungeAt = now;
                lungeAngle = pawn.Rotation.AsAngle + Rand.Range(-30f, 30f);
                if (Rand.Chance(0.3f)) SoundDefOf.Pawn_Melee_Punch_Miss?.PlayOneShot(new TargetInfo(pawn.Position, map));
                return;
            }
            if (Place == null) return;
            pawn.Drawer.Notify_MeleeAttackOn(Place);
            SoundDefOf.Pawn_Melee_Punch_HitBuilding_Generic?.PlayOneShot(new TargetInfo(Place.Position, map));
        }

        /// <summary>An arrow from their bow at the target, as Misc. Training's archery shows it: the arrow flies, strikes and stays a moment; the bow's own sound.</summary>
        private void ShootArrow()
        {
            var def = IdleHours.ArrowMote;
            var map = pawn.Map;
            if (def == null || Place == null || map == null) return;
            try
            {
                var from = pawn.DrawPos;
                var to = Place.DrawPos + new Vector3(Rand.Range(-0.25f, 0.25f), 0f, Rand.Range(-0.2f, 0.3f));
                float angle = (to - from).AngleFlat();
                float dist = (to - from).MagnitudeHorizontal();
                const float speed = 18f;
                var mote = (MoteThrown)ThingMaker.MakeThing(def);
                mote.exactPosition = from;
                mote.exactRotation = angle;
                mote.rotationRate = 0f;
                mote.SetVelocity(angle, speed);
                mote.airTimeLeft = dist / speed;
                GenSpawn.Spawn(mote, pawn.Position, map);
                pawn.equipment?.Primary?.def?.Verbs?.FirstOrDefault()?.soundCast?.PlayOneShot(new TargetInfo(pawn.Position, map));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a visitor's arrow", e);
            }
        }

        /// <summary>
        /// A stick thrown for one of the colony's dogs: it runs after it and brings it back, a few times, then the visitor
        /// leaves it be. Only ever the game's own going somewhere; nothing taught or trained.
        /// </summary>
        private void Fetch()
        {
            var dog = Other;
            var map = pawn.Map;
            if (dog == null || map == null || Now < nextThrow) return;
            if (throws >= 4)
            {
                if (dog.CurJobDef != JobDefOf.Goto && dog.Position.InHorDistOf(pawn.Position, 3f)) EndJobWith(JobCondition.Succeeded);
                return;
            }
            if (dog.CurJobDef == JobDefOf.Goto || !dog.Position.InHorDistOf(pawn.Position, 3f)) return;
            nextThrow = Now + 120;
            try
            {
                var dir = pawn.Rotation.FacingCell;
                for (int tries = 0; tries < 8; tries++)
                {
                    var c = pawn.Position + dir * Rand.RangeInclusive(4, 7) + new IntVec3(Rand.RangeInclusive(-2, 2), 0, Rand.RangeInclusive(-2, 2));
                    if (!c.InBounds(map) || !c.Standable(map) || !c.UsesOutdoorTemperature(map) || c.GetEdifice(map) != null) continue;
                    if (!c.InAllowedArea(dog) || !dog.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                    var back = pawn.Position + dir;
                    if (!back.Standable(map)) back = pawn.Position;
                    if (VisualTouches.On) FleckMaker.ThrowStone(pawn, c);
                    var run = JobMaker.MakeJob(JobDefOf.Goto, c);
                    run.locomotionUrgency = LocomotionUrgency.Sprint;
                    run.expiryInterval = 600;
                    var bring = JobMaker.MakeJob(JobDefOf.Goto, back);
                    bring.locomotionUrgency = LocomotionUrgency.Jog;
                    bring.expiryInterval = 600;
                    dog.jobs.StartJob(run, JobCondition.InterruptForced, null, false, true);
                    dog.jobs.jobQueue.EnqueueFirst(bring);
                    throws++;
                    return;
                }
                // Nowhere to throw it: they pet it instead, a moment.
                throws = 4;
            }
            catch (Exception e)
            {
                throws = 4;
                CouncilLog.ErrorOnce("a visitor throws a stick", e);
            }
        }

        /// <summary>
        /// One of the colony's people has set out for the game, the seat or the place the visitor is at or going to: the
        /// visitor gives it up (now and then with a word to them) and leaves it be for a few hours.
        /// </summary>
        private bool GiveWayIfWanted()
        {
            var d = Doing;
            if (d == IdleDoing.Chat || d == IdleDoing.Animal || d == IdleDoing.Company || d == IdleDoing.Cards) return false;
            Pawn who;
            if (d == IdleDoing.Pet)
            {
                // A handler of the colony's on their way to the animal: it's theirs.
                if (!IdleHours.ColonyWho(pawn.Map, Other, IntVec3.Invalid, out who)) return false;
                EndJobWith(JobCondition.InterruptForced);
                return true;
            }
            var cell = job.targetA.Cell;
            // The thing itself counts for a game, an instrument, the telescope, art, a shrine and a training target; at a fire,
            // a seat, a table, the water or under a roof only the place, since the colony's people come to a fire to cook at it
            // or feed it.
            bool thing = d == IdleDoing.Game || d == IdleDoing.Music || d == IdleDoing.Stars || d == IdleDoing.Art || d == IdleDoing.Pray
                         || (d == IdleDoing.Train && Form == 0) || d == IdleDoing.Archery;
            bool found = IdleHours.ColonyWho(pawn.Map, thing ? Place : null, cell, out who);
            // A fisher of the colony's at the same water.
            if (!found && d == IdleDoing.Fish) found = IdleHours.ColonyWho(pawn.Map, null, job.targetB.Cell, out who);
            if (!found) return false;
            IdleHours.GaveWay(pawn, Place);
            if (who != null && Rand.Chance(IdleHoursMath.GiveWayLineChance)) IdleHours.Say(pawn, "giveway", who);
            EndJobWith(JobCondition.InterruptForced);
            return true;
        }

        /// <summary>What they ate, drank or smoked out of their own pack: the game's own taking of it (its food or its effect), one portion.</summary>
        private void Consume()
        {
            var def = job.thingDefToCarry;
            var inv = pawn.inventory?.innerContainer;
            if (def == null || inv == null) return;
            try
            {
                Thing have = null;
                foreach (var t in inv)
                    if (t?.def == def)
                    {
                        have = t;
                        break;
                    }
                if (have == null) return;
                var food = pawn.needs?.food;
                int count = 1;
                if (!def.IsDrug && food != null)
                    count = Mathf.Clamp(FoodUtility.WillIngestStackCountOf(pawn, def, have.GetStatValue(StatDefOf.Nutrition)), 1, have.stackCount);
                var taken = inv.Take(have, Mathf.Min(count, have.stackCount));
                if (taken == null) return;
                float nutrition = taken.Ingested(pawn, def.IsDrug ? 0f : food?.NutritionWanted ?? 0f);
                if (food != null && !pawn.Dead) food.CurLevel += nutrition;
                // A portion not finished goes back into the pack, never onto the colony's ground.
                if (!taken.Destroyed && taken.stackCount > 0) inv.TryAdd(taken);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a visitor eats from their pack", e);
            }
        }

        /// <summary>
        /// A fish or two out of the water, into their own pack: one of the water's own fish with Odyssey (the colony's fish
        /// stock left as it was), the fishing mod's otherwise; the game's own sound of a catch, and now and then a word.
        /// </summary>
        private void Catch()
        {
            var map = pawn.Map;
            var water = job.targetB.Cell;
            if (map == null || !water.IsValid || !pawn.Position.InHorDistOf(water, 2.5f) || !CampLand.Fishable(map, water)) return;
            try
            {
                ThingDef fish = null;
                if (CampLand.Odyssey) fish = water.GetWaterBody(map)?.CommonFishIncludingExtras?.RandomElementWithFallback();
                if (fish == null) fish = CampLand.ModFish().RandomElementWithFallback();
                if (fish == null || pawn.inventory?.innerContainer == null) return;
                var t = ThingMaker.MakeThing(fish);
                int skill = pawn.skills?.GetSkill(SkillDefOf.Animals)?.Level ?? 4;
                t.stackCount = Mathf.Clamp(1 + (skill >= 8 ? 1 : 0) + (Rand.Chance(0.3f) ? 1 : 0), 1, Math.Max(1, fish.stackLimit));
                if (!pawn.inventory.innerContainer.TryAdd(t)) t.Destroy();
                catchSound?.PlayOneShot(new TargetInfo(pawn.Position, map));
                if (Rand.Chance(IdleHoursMath.CatchLineChance)) IdleHours.Say(pawn, "catch");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a visitor's catch", e);
            }
        }

        /// <summary>The worn piece of their own clothes mended a little (IdleHoursMath.MendBy), once the stint is done.</summary>
        private void Mended()
        {
            try
            {
                if (job.targetC.Thing is Apparel a && !a.Destroyed && pawn.apparel != null && pawn.apparel.WornApparel.Contains(a))
                    a.HitPoints += IdleHoursMath.MendBy(a.HitPoints, a.MaxHitPoints);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a visitor mends their clothes", e);
            }
        }

        // ---------------- A book ----------------

        /// <summary>
        /// Reading: a book of their own is taken out of the pack once they've sat down and put back after; one of the colony's
        /// is taken up where it lies (or out of its bookcase), read on a seat close by, carried open as the game's readers
        /// carry it, and put back where it was.
        /// </summary>
        private IEnumerable<Toil> ReadToils()
        {
            bool own = Form == IdleHoursMath.ReadOwn;
            this.FailOn(() => TheBook == null || TheBook.Destroyed);
            if (!own)
            {
                // The book still where it lay, or in hand.
                this.FailOn(() => !Holding && !TheBook.Spawned && !(job.targetC.Thing is Building_Bookcase bc && bc.HeldBooks.Contains(TheBook)));
                var toBook = ToilMaker.MakeToil("VisitToBook");
                toBook.initAction = () =>
                {
                    if (Holding)
                    {
                        ReadyForNextToil();
                        return;
                    }
                    var place = TheBook.Spawned ? (LocalTargetInfo)TheBook : job.targetC;
                    pawn.pather.StartPath(place, PathEndMode.Touch);
                };
                toBook.defaultCompleteMode = ToilCompleteMode.PatherArrival;
                toBook.AddPreTickIntervalAction(delta =>
                {
                    if (!pawn.IsHashIntervalTick(60, delta)) return;
                    // One of the colony's people after the book or its bookcase: it's theirs.
                    var thing = TheBook.Spawned ? (Thing)TheBook : job.targetC.Thing;
                    if (thing != null && IdleHours.ColonyHas(pawn.Map, thing, IntVec3.Invalid)) EndJobWith(JobCondition.InterruptForced);
                });
                yield return toBook;
                yield return Toils_General.Do(() =>
                {
                    if (!Holding && !TakeUp()) EndJobWith(JobCondition.Incompletable);
                });
            }
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            if (own)
                yield return Toils_General.Do(() =>
                {
                    var inv = pawn.inventory?.innerContainer;
                    if (Holding) return;
                    if (inv == null || !inv.Contains(TheBook) || inv.TryTransferToContainer(TheBook, pawn.carryTracker.innerContainer, 1) <= 0) EndJobWith(JobCondition.Incompletable);
                });
            var read = ToilMaker.MakeToil("VisitRead");
            read.defaultCompleteMode = ToilCompleteMode.Delay;
            read.defaultDuration = Mathf.Max(300, stint);
            read.handlingFacing = true;
            read.socialMode = RandomSocialMode.Quiet;
            read.initAction = () =>
            {
                if (TheBook != null) TheBook.IsOpen = true;
                Begin(IdleDoing.Read);
            };
            var back = ToilMaker.MakeToil("VisitBookBack");
            read.tickIntervalAction = delta =>
            {
                // On a seat, the way it faces; else towards where the book lay.
                var seat = pawn.Position.GetEdifice(pawn.Map);
                if (seat?.def.building != null && seat.def.building.isSittable) pawn.rotationTracker.FaceCell(pawn.Position + seat.Rotation.FacingCell);
                else if (job.targetC.IsValid && job.targetC.Cell != pawn.Position) pawn.rotationTracker.FaceCell(job.targetC.Cell);
                if (pawn.IsHashIntervalTick(60, delta) && IdleHours.ColonyWho(pawn.Map, null, job.targetB.Cell, out var who))
                {
                    // Someone of the colony wants the seat: they get up, and the book goes back.
                    IdleHours.GaveWay(pawn, null);
                    if (who != null && Rand.Chance(IdleHoursMath.GiveWayLineChance)) IdleHours.Say(pawn, "giveway", who);
                    JumpToToil(back);
                }
            };
            read.AddFinishAction(() =>
            {
                if (TheBook != null) TheBook.IsOpen = false;
            });
            yield return read;
            // Back where it came from: the pack, the bookcase, the cell it lay on.
            back.initAction = () =>
            {
                if (TheBook != null) TheBook.IsOpen = false;
                if (own || !Holding)
                {
                    ReadyForNextToil();
                    return;
                }
                if (job.targetC.HasThing) pawn.pather.StartPath(job.targetC, PathEndMode.Touch);
                else if (job.targetC.IsValid) pawn.pather.StartPath(job.targetC.Cell, PathEndMode.ClosestTouch);
                else ReadyForNextToil();
            };
            back.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            yield return back;
            yield return Toils_General.Do(() => PutBookAway(true));
        }

        /// <summary>They have the book in hand.</summary>
        private bool Holding => TheBook != null && pawn.carryTracker?.CarriedThing == TheBook;

        /// <summary>The colony's book taken up from where it lies, or out of its bookcase, into their hands (nothing reserved).</summary>
        private bool TakeUp()
        {
            var book = TheBook;
            if (book == null) return false;
            try
            {
                if (book.Spawned) return pawn.carryTracker.TryStartCarry(book, 1, false) > 0;
                if (job.targetC.Thing is Building_Bookcase bc && bc.HeldBooks.Contains(book))
                    return bc.GetDirectlyHeldThings().TryTransferToContainer(book, pawn.carryTracker.innerContainer, 1) > 0;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a visitor takes up a book", e);
            }
            return false;
        }

        /// <summary>
        /// The book put back: their own into their pack; the colony's into its bookcase or onto the cell it lay on, or, cut
        /// short away from there, set down where they stand (still the colony's, for its haulers to put away).
        /// </summary>
        private void PutBookAway(bool arrived)
        {
            var book = TheBook;
            if (book == null || !Holding) return;
            book.IsOpen = false;
            var carried = pawn.carryTracker.innerContainer;
            if (Form == IdleHoursMath.ReadOwn)
            {
                if (pawn.inventory != null && carried.TryTransferToContainer(book, pawn.inventory.innerContainer, 1) > 0) return;
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                return;
            }
            bool there = arrived || job.targetC.IsValid && pawn.Position.InHorDistOf(job.targetC.Cell, 2f);
            if (there && job.targetC.Thing is Building_Bookcase bc && bc.Spawned && bc.Accepts(book)
                && carried.TryTransferToContainer(book, bc.GetDirectlyHeldThings(), 1) > 0) return;
            if (there && job.targetC.IsValid && !job.targetC.HasThing && pawn.carryTracker.TryDropCarriedThing(job.targetC.Cell, ThingPlaceMode.Direct, out _)) return;
            pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
        }

        // ---------------- A walk ----------------

        /// <summary>
        /// A walk round the colony: from place to place on its paths at a stroll, a moment at each, and at the walk's end (a
        /// garden, a grave, a view) a while standing looking at it, a word now and then.
        /// </summary>
        private IEnumerable<Toil> WalkToils()
        {
            var next = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A);
            yield return next;
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var pause = ToilMaker.MakeToil("VisitWalkPause");
            pause.defaultCompleteMode = ToilCompleteMode.Delay;
            pause.defaultDuration = 120;
            pause.handlingFacing = true;
            pause.socialMode = RandomSocialMode.Normal;
            pause.initAction = () =>
            {
                bool last = job.GetTargetQueue(TargetIndex.A).NullOrEmpty();
                ticksLeftThisToil = last ? Rand.RangeInclusive(500, 1000) : Rand.RangeInclusive(60, 150);
                if (last) Begin(IdleDoing.Walk);
            };
            pause.tickIntervalAction = delta =>
            {
                if (job.GetTargetQueue(TargetIndex.A).NullOrEmpty() && job.targetB.IsValid && job.targetB.Cell != pawn.Position) pawn.rotationTracker.FaceCell(job.targetB.Cell);
            };
            yield return pause;
            yield return Toils_Jump.JumpIfHaveTargetInQueue(TargetIndex.A, next);
        }
    }

    /// <summary>A stint still holds (IdleHours.Still), looked at once a second rather than every tick.</summary>
    public sealed class IdleCheck
    {
        private int at = -1;
        private bool holds = true;

        public bool Holds(Pawn p, IdleDoing d, Job job)
        {
            int now = Find.TickManager.TicksGame;
            if (at < 0 || now - at >= 60 || now < at)
            {
                at = now;
                holds = IdleHours.Still(p, d, job);
            }
            return holds;
        }
    }

    /// <summary>
    /// A nap in the shade, or a night's sleep on the ground near their spot: the game's own lying down, for an hour or so
    /// (the job's expiry; a night's sleep till they're rested), in words of its own. It doesn't look for anything else to do
    /// meanwhile: a visitor's duty would have them up again at once to wander about their spot (as with a hosted guest's
    /// sleep, JobDriver_GuestSleep).
    /// </summary>
    public class JobDriver_VisitNap : JobDriver_LayDown
    {
        [Unsaved] private readonly IdleCheck check = new IdleCheck();

        private IdleDoing Doing => job.def.GetModExtension<VisitIdleExtension>()?.doing ?? IdleDoing.Nap;

        public override bool LookForOtherJobs => false;

        public override string GetReport() =>
            (Doing == IdleDoing.Sleep ? asleep ? "CC_Idle_Report_Sleep" : "CC_Idle_Report_LieDown" : asleep ? "CC_Idle_Report_Nap" : "CC_Idle_Report_Rest").Translate().Resolve();

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var d = Doing;
            this.FailOn(() => !check.Holds(pawn, d, job));
            AddFinishAction(_ => IdleHours.Ended(pawn));
            foreach (var t in base.MakeNewToils()) yield return t;
        }
    }

    /// <summary>A child of theirs at play: running from place to place near their people, stopping a moment at each.</summary>
    public class JobDriver_VisitIdlePlay : JobDriver
    {
        [Unsaved] private readonly IdleCheck check = new IdleCheck();

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !check.Holds(pawn, IdleDoing.Play, job));
            AddFinishAction(_ => IdleHours.Ended(pawn));
            var first = ToilMaker.MakeToil("VisitPlayBegin");
            first.initAction = () =>
            {
                if (Rand.Chance(IdleHoursMath.LineChance(IdleDoing.Play))) IdleHours.Say(pawn, IdleHoursMath.Moment(IdleDoing.Play, false));
            };
            first.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return first;
            var next = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A);
            yield return next;
            yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
            var pause = Toils_General.Wait(Rand.RangeInclusive(60, 200));
            pause.socialMode = RandomSocialMode.SuperActive;
            yield return pause;
            yield return Toils_Jump.JumpIfHaveTargetInQueue(TargetIndex.A, next);
        }
    }

    /// <summary>
    /// A visitor at the colony about to wander near their spot for want of anything to do (or, for Hospitality's guests, to
    /// stand about being sociable) may do something of their own instead (IdleHours.JobFor). Only ever in place of that idle
    /// moment, and after the market's turn (Patch_VisitorsShop): fighting, eating, sleeping, Hospitality's own recreation and
    /// whatever their duty has for them come first, and their duty itself is left as it is. The job keeps the lord's own
    /// lord, source and duty tag.
    /// </summary>
    [HarmonyPatch(typeof(ThinkNode_Duty), nameof(ThinkNode_Duty.TryIssueJobPackage))]
    public static class Patch_VisitorsIdle
    {
        [HarmonyPriority(Priority.Low)]
        public static void Postfix(Pawn pawn, ref ThinkResult __result)
        {
            var job = __result.Job;
            if (pawn?.Faction == null || pawn.Faction.IsPlayer || !IdleHours.On || !pawn.RaceProps.Humanlike) return;
            bool wander = job != null && (job.def == JobDefOf.GotoWander || job.def == JobDefOf.Wait_Wander);
            bool standing = job != null && job.def == JobDefOf.StandAndBeSociallyActive;
            try
            {
                if (!wander && !standing)
                {
                    IdleHours.Asked(pawn, false);
                    return;
                }
                var idle = IdleHours.JobFor(pawn, false, standing);
                IdleHours.Asked(pawn, idle == null);
                if (idle == null) return;
                idle.lord = job.lord;
                idle.source = job.source;
                idle.dutyTag = job.dutyTag;
                __result = new ThinkResult(idle, __result.SourceNode, __result.Tag, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("a visitor's idle hour", e);
            }
        }
    }
}
