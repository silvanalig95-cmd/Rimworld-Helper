using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// A RimWorld of Magic: a mage's familiar (the player's pick of 7 November 2026). Rimfolk's own idea, read from the mod
    /// by name only: a mage is a colonist with the mod's mana need (TM_Mana, which its magic users have), not one of its
    /// faceless (the trait Faceless, whose mana the mod hides); its arcane weakness is the hediff TM_ArcaneWeakness. All
    /// three checked against its 1.6 source (TorannD/RWoM: TorannMagicDefOf, Need_Mana, Hediff_Def_TMagic_ArcaneWeakness).
    /// <list type="bullet">
    /// <item>Bound on the map, as the council's answer or the mage's own choice: the council promises a mage's wish for a
    /// familiar (Council_Wish_Familiar), or a mage bonded to one of the colony's animals chooses it one day in four. In a
    /// quiet hour of the day the mage walks to the animal and stays with it a while (it stays facing them), and then it is
    /// theirs: news, a word, a journal line and the chronicle.</item>
    /// <item>The familiar glows faintly. Within 12 cells of its mage it lends a little: 0.0004 mana each 250 ticks (about
    /// a tenth of a pool a day; the mage's own gain is about half a pool), and a small ward (the hediff "a familiar near",
    /// a twentieth less harm), shown on the mage's health tab.</item>
    /// <item>Its death hits the mage hard: a mood (-12 for 15 days), the mana emptied and the mod's arcane weakness at 4
    /// (about eight hours), told at once; sold, released or gone off, the bond ends with a word in the news.</item>
    /// </list>
    /// One familiar a mage; never an animal bonded to someone else, a mechanoid, or one of the mod's own summoned creatures.
    /// Saved in <see cref="AnimalPowersBook"/>. Setting: "Familiars" (A RimWorld of Magic's section of the other mods' page).
    /// </summary>
    public static class MagicFamiliars
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static bool looked, ready;
        private static NeedDef mana;
        private static HediffDef weakness, near;
        private static TraitDef faceless;
        private static JobDef bindJob;

        public static bool Ready()
        {
            if (looked) return ready;
            looked = true;
            try
            {
                if (!ModsConfig.IsActive(MagicGolems.PackageId)) return false;
                mana = DefDatabase<NeedDef>.GetNamedSilentFail("TM_Mana");
                weakness = DefDatabase<HediffDef>.GetNamedSilentFail("TM_ArcaneWeakness");
                faceless = DefDatabase<TraitDef>.GetNamedSilentFail("Faceless");
                near = DefDatabase<HediffDef>.GetNamedSilentFail("Rimfolk_FamiliarNear");
                bindJob = DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_BindFamiliar");
                ready = mana != null && bindJob != null;
                if (mana == null) CouncilLog.WarnOnce("A RimWorld of Magic is loaded, but its mana (TM_Mana) wasn't found; Rimfolk's familiars don't come.");
                else if (weakness == null) CouncilLog.WarnOnce("A RimWorld of Magic: its arcane weakness (TM_ArcaneWeakness) wasn't found; a familiar's death only empties its mage's mana.");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("familiars: reading the mod", e);
                ready = false;
            }
            return ready;
        }

        public static bool On => S != null && S.familiars && Ready();

        public static JobDef BindJob => bindJob;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ===================== Who and which =====================

        public static Need ManaOf(Pawn p) => mana == null ? null : p?.needs?.TryGetNeed(mana);

        private static bool Faceless(Pawn p) => faceless != null && (p?.story?.traits?.HasTrait(faceless) ?? false);

        /// <summary>A grown colonist with an inner life and the mod's mana, not one of the faceless.</summary>
        public static bool OurMage(Pawn p) =>
            p != null && !p.Dead && p.IsColonist && p.RaceProps.Humanlike && !p.DevelopmentalStage.Juvenile() && People.HasInnerLife(p) && ManaOf(p) != null && !Faceless(p);

        /// <summary>One of the mod's own creatures (a summoned wolf, an elemental): never a familiar.</summary>
        private static bool OfTheMod(Pawn a) => string.Equals(a?.def?.modContentPack?.PackageId, MagicGolems.PackageId, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The colony's animal that could be this mage's familiar: alive, flesh, of the colony, not the mod's own, not anyone's
        /// familiar already, and bonded to nobody but the mage.
        /// </summary>
        public static bool Bindable(Pawn a, Pawn mage)
        {
            if (!AnimalFacts.Ours(a) || a.Dead || !a.RaceProps.IsFlesh || a.IsQuestLodger() || OfTheMod(a)) return false;
            var book = AnimalPowersBook.Instance;
            if (book != null && book.BondOfAnimal(a) != null) return false;
            var rels = a.relations?.DirectRelations;
            if (rels != null)
                for (int i = 0; i < rels.Count; i++)
                    if (rels[i].def == PawnRelationDefOf.Bond && rels[i].otherPawn != mage && rels[i].otherPawn != null && !rels[i].otherPawn.Dead) return false;
            return true;
        }

        /// <summary>The animal a mage would choose: the one bonded to them, else one they are master of, else a calm grown one with no person.</summary>
        public static Pawn Candidate(Pawn mage, Map map)
        {
            if (mage == null || map == null) return null;
            var bonded = AnimalFacts.BondedAnimal(mage);
            if (bonded != null && Bindable(bonded, mage)) return bonded;
            var animals = map.mapPawns.SpawnedColonyAnimals.Where(a => Bindable(a, mage) && !a.Downed).ToList();
            return animals.FirstOrDefault(a => a.playerSettings?.Master == mage)
                   ?? animals.Where(a => PetTracker.PersonOf(a) == null && !AnimalFacts.Young(a) && !a.InMentalState)
                       .OrderByDescending(AnimalFacts.TrainedShare).FirstOrDefault();
        }

        // ===================== Every 250 ticks: presence, deaths, bindings =====================

        private static readonly Color FamiliarColor = new Color(0.74f, 0.62f, 1f, 0.32f);

        public static void Rare(AnimalPowersBook book, int now)
        {
            if (book == null || !Ready()) return;
            if (book.familiars.Count > 0)
                foreach (var f in book.familiars.ToList())
                {
                    try
                    {
                        Watch(book, f, now);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("familiars: a familiar", e);
                    }
                }
            if (!On || book.bindings.Count == 0) return;
            foreach (var b in book.bindings.ToList())
            {
                try
                {
                    Waiting(book, b, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("familiars: a binding", e);
                }
            }
        }

        private static void Watch(AnimalPowersBook book, FamiliarBond f, int now)
        {
            var mage = f.mage;
            var a = f.animal;
            if (a == null || mage == null || mage.Discarded)
            {
                book.familiars.Remove(f);
                return;
            }
            if (a.Dead)
            {
                book.familiars.Remove(f);
                if (!mage.Dead) Died(f);
                return;
            }
            if (mage.Dead)
            {
                // Its mage gone, it is only an animal again; the mage's own death is told already.
                book.familiars.Remove(f);
                return;
            }
            if (a.Discarded || a.Faction != Faction.OfPlayer)
            {
                book.familiars.Remove(f);
                News.Post("CC_Fam_Gone".Translate(mage.Named("PAWN"), (f.name ?? a.LabelShort).Named("NAME")).Resolve(), new LookTargets(mage), NewsKind.People,
                    MessageTypeDefOf.NeutralEvent, Importance.Notable);
                return;
            }
            if (!On || !a.Spawned) return;
            var map = a.Map;
            // It glows faintly wherever it is.
            if (AnimalPowers.InView(map, a.Position)) AnimalPowers.Glow(map, a.DrawPos, FamiliarColor, 0.9f, 0.5f);
            if (!mage.Spawned || mage.Map != map || a.Downed || !a.Position.InHorDistOf(mage.Position, AnimalPowersMath.FamiliarNear)) return;
            var need = ManaOf(mage);
            if (need != null) need.CurLevel = AnimalPowersMath.ManaAfter(need.CurLevel, need.MaxLevel, AnimalPowersMath.FamiliarManaPerPass);
            if (near != null)
            {
                var h = mage.health.hediffSet.GetFirstHediffOfDef(near);
                if (h == null) mage.health.AddHediff(near);
                else h.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
            }
        }

        /// <summary>The familiar is dead: a mood, the mana emptied and the mod's arcane weakness, told at once.</summary>
        private static void Died(FamiliarBond f)
        {
            var mage = f.mage;
            string name = f.name ?? f.animal?.LabelShort ?? "?";
            using (Causes.Push("CC_Fam_Cause_Died".Translate(name.Named("NAME")).Resolve()))
            {
                CouncilGameComponent.GiveThought(mage, DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Fam_Died"));
                var need = ManaOf(mage);
                if (need != null) need.CurLevel = 0f;
                if (weakness != null && mage.health != null) HealthUtility.AdjustSeverity(mage, weakness, AnimalPowersMath.WeaknessOnDeath);
                if (near != null)
                {
                    var h = mage.health?.hediffSet.GetFirstHediffOfDef(near);
                    if (h != null) mage.health.RemoveHediff(h);
                }
            }
            Tell.Message(NewsKind.People, Importance.Important, mage.MapHeld, "CC_Fam_Died".Translate(mage.Named("PAWN"), name.Named("NAME"), (f.kind ?? "").Named("KIND")).Resolve(),
                new LookTargets(mage), MessageTypeDefOf.NegativeEvent);
            var comp = CouncilGameComponent.Instance;
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Loss, "CC_Fam_Chronicle_Died".Translate(mage.Named("PAWN"), name.Named("NAME")).Resolve(), mage));
            comp?.journals.Write(mage, "Rimfolk_Journal_Fam_Died", null, name.Named("NAME"));
            if (mage.Spawned)
            {
                if (AnimalPowers.InView(mage.Map, mage.Position)) AnimalPowers.Glow(mage.Map, mage.DrawPos, new Color(0.5f, 0.3f, 0.75f, 0.6f), 2.2f, 0.8f);
                if (Speech.On)
                {
                    string line = Interplay.Line("Rimfolk_Pool_Fam_Died", mage, null, name.Named("NAME"));
                    // apart: the mage, to nobody, as it happens.
                    if (!line.NullOrEmpty()) Speech.Say(mage, line);
                }
            }
        }

        // ---------------- A binding waiting ----------------

        private static void Waiting(AnimalPowersBook book, FamiliarBinding b, int now)
        {
            var mage = b.mage;
            var a = b.animal;
            if (!OurMage(mage) || a == null || !Bindable(a, mage) || book.FamiliarOf(mage) != null || AnimalPowersMath.BindingStale(b.since, now) || b.tries > 5)
            {
                book.bindings.Remove(b);
                return;
            }
            if (mage.CurJobDef == bindJob) return;
            var map = mage.Map;
            if (map == null || !mage.Spawned || !a.Spawned || a.Map != map || a.Downed || a.InMentalState || !a.Awake()) return;
            if (!AnimalPowersMath.BindHour(GenLocalDate.HourOfDay(map)) || PlayOuts.WhyNotNow(map, false) != null) return;
            if (!PlayOuts.CanTakePart(mage, map) || !PlayOuts.Interruptible(mage) || PlayOuts.NeedsPress(mage)) return;
            if (!mage.CanReserveAndReach(a, PathEndMode.Touch, Danger.None)) return;
            var job = JobMaker.MakeJob(bindJob, a);
            job.count = AnimalPowersMath.BindingTicks;
            b.tries++;
            mage.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
        }

        /// <summary>The mage has come up to the animal: it stays with them, and they say a word to it.</summary>
        public static void BindingBegins(Pawn mage, Pawn a, int ticks)
        {
            if (mage == null || a == null) return;
            var stay = PokeWorldTypes.StayJob;
            if (stay != null)
            {
                try
                {
                    var job = JobMaker.MakeJob(stay, mage);
                    job.count = ticks + 200;
                    a.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("familiars: the animal stays", e);
                }
            }
            if (Speech.On && mage.Map == Find.CurrentMap)
            {
                string line = Interplay.Line("Rimfolk_Pool_Fam_Begin", mage, null, a.Named("PET"));
                // apart: said to the animal, not to another colonist.
                if (!line.NullOrEmpty()) Speech.Say(mage, line);
            }
        }

        /// <summary>While they stay together: now and then a faint light passes between them.</summary>
        public static void BindingSits(Pawn mage, Pawn a, int sat)
        {
            if (sat % 300 != 0 || mage?.Map == null || a == null || !AnimalPowers.InView(mage.Map, a.Position)) return;
            AnimalPowers.Glow(mage.Map, a.DrawPos, FamiliarColor, 1.1f + sat / 3000f, 0.5f);
            AnimalPowers.Glow(mage.Map, mage.DrawPos, FamiliarColor, 0.8f, 0.4f);
        }

        /// <summary>The binding stayed through: the animal is the mage's familiar.</summary>
        public static void Bound(Pawn mage, Pawn a)
        {
            var book = AnimalPowersBook.Instance;
            if (book == null || !OurMage(mage) || !Bindable(a, mage)) return;
            book.bindings.RemoveAll(x => x.mage == mage || x.animal == a);
            // A familiar has a name: one without is given one by its mage.
            if (a.Name == null || a.Name.Numerical) HerdStory.Name(a);
            book.AddFamiliar(new FamiliarBond { mage = mage, animal = a, since = Now, name = a.LabelShort, kind = AnimalFacts.KindLabel(a) });
            using (Causes.Push("CC_Fam_Cause_Bound".Translate(a.Named("PET")).Resolve()))
                CouncilGameComponent.GiveThought(mage, DefDatabase<ThoughtDef>.GetNamedSilentFail("Rimfolk_Fam_Bound"));
            News.Post("CC_Fam_Bound".Translate(mage.Named("PAWN"), a.Named("PET")).Resolve(), new LookTargets(mage, a), NewsKind.People, MessageTypeDefOf.PositiveEvent,
                Importance.Notable);
            var comp = CouncilGameComponent.Instance;
            comp?.AddChronicle(new ChronicleEntry(ChronicleKind.Life, "CC_Fam_Chronicle_Bound".Translate(mage.Named("PAWN"), a.Named("PET")).Resolve(), mage));
            comp?.journals.Write(mage, "Rimfolk_Journal_Fam_Bound", null, a.Named("PET"));
            if (mage.Map != null && AnimalPowers.InView(mage.Map, a.Position)) AnimalPowers.Glow(mage.Map, a.DrawPos, FamiliarColor, 2.4f, 0.8f);
            if (Speech.On && mage.Map == Find.CurrentMap)
            {
                string line = Interplay.Line("Rimfolk_Pool_Fam_Bound", mage, null, a.Named("PET"));
                // apart: said to the animal, not to another colonist.
                if (!line.NullOrEmpty()) Speech.Say(mage, line);
            }
        }

        // ===================== Hourly and daily =====================

        public static void Hourly(AnimalPowersBook book, int now)
        {
            if (book == null) return;
            book.bindings.RemoveAll(b => b?.mage == null || b.animal == null || AnimalPowersMath.BindingStale(b.since, now));
        }

        /// <summary>Once a day: a mage bonded to one of the colony's animals, with no familiar, may choose it of their own accord.</summary>
        public static void Daily(AnimalPowersBook book, int now)
        {
            if (!On || book == null) return;
            foreach (var map in Find.Maps)
            {
                if (map == null || !map.IsPlayerHome) continue;
                foreach (var mage in map.mapPawns.FreeColonistsSpawned)
                {
                    if (!OurMage(mage) || book.FamiliarOf(mage) != null || book.BindingOf(mage) != null) continue;
                    var a = AnimalFacts.BondedAnimal(mage);
                    if (a == null || !Bindable(a, mage) || !Rand.Chance(AnimalPowersMath.OwnChoiceChance)) continue;
                    Queue(mage, a, false);
                }
            }
        }

        /// <summary>A binding to play out when the mage has a quiet hour (the council's answer, the mage's choice, a dev action).</summary>
        public static bool Queue(Pawn mage, Pawn a, bool byWish)
        {
            var book = AnimalPowersBook.Instance;
            if (book == null || !OurMage(mage) || !Bindable(a, mage) || book.FamiliarOf(mage) != null) return false;
            book.AddBinding(new FamiliarBinding { mage = mage, animal = a, since = Now, byWish = byWish });
            return true;
        }
    }

    // ===================== The binding's job =====================

    /// <summary>The mage walks to the animal and stays with it a while; stayed through, it is their familiar.</summary>
    public class JobDriver_BindFamiliar : JobDriver
    {
        private int sat;

        private Pawn Animal => job.targetA.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref sat, "sat");
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => !MagicFamiliars.On || Animal == null || Animal.Dead || Animal.Downed || Animal.InMentalState || !MagicFamiliars.Bindable(Animal, pawn)
                              || pawn.Map == null || pawn.Map.dangerWatcher.DangerRating != StoryDanger.None);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var sit = ToilMaker.MakeToil("BindFamiliar");
            sit.initAction = () => MagicFamiliars.BindingBegins(pawn, Animal, job.count);
            sit.tickIntervalAction = delta =>
            {
                var a = Animal;
                if (a == null || !pawn.Position.InHorDistOf(a.Position, 2.5f))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                pawn.rotationTracker.FaceTarget(a);
                for (int i = 0; i < delta; i++)
                {
                    sat++;
                    MagicFamiliars.BindingSits(pawn, a, sat);
                }
            };
            sit.defaultCompleteMode = ToilCompleteMode.Delay;
            sit.defaultDuration = Mathf.Max(600, job.count);
            sit.handlingFacing = true;
            sit.socialMode = RandomSocialMode.Off;
            yield return sit;
            var done = ToilMaker.MakeToil("FamiliarBound");
            done.initAction = () => MagicFamiliars.Bound(pawn, Animal);
            done.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return done;
        }
    }

    // ===================== The wish =====================

    /// <summary>
    /// A RimWorld of Magic: a mage with no familiar wants one of the colony's animals bound to them (their bonded one, one
    /// they're master of, else a calm grown one with no person). Promised, the binding plays out when they have a quiet
    /// hour; fulfilled once it is their familiar. Without the mod it never comes up, and an open one lapses quietly.
    /// </summary>
    public class WishWorker_Familiar : WishWorker
    {
        public override IEnumerable<WishCandidate> CandidatesFor(Pawn pawn)
        {
            if (!MagicFamiliars.On) yield break;
            var book = AnimalPowersBook.Instance;
            var map = HomeMap(pawn);
            if (book == null || map == null || !MagicFamiliars.OurMage(pawn) || book.FamiliarOf(pawn) != null || book.BindingOf(pawn) != null) yield break;
            var a = MagicFamiliars.Candidate(pawn, map);
            if (a == null) yield break;
            var c = Candidate(AnimalFacts.Bonded(a, pawn) ? 1.2f : 0.6f, "CC_Fam_Reason".Translate(pawn.Named("PAWN"), a.Named("PET")));
            c.other = a;
            c.subjectLabel = a.LabelShort;
            yield return c;
        }

        public override bool IsFulfilled(Wish wish) => AnimalPowersBook.Instance?.FamiliarOf(wish.Beneficiary) != null;

        public override bool StillValid(Wish wish) => MagicFamiliars.On && MagicFamiliars.OurMage(wish.Beneficiary);

        public override bool StillObtainable(Wish wish) =>
            base.StillObtainable(wish) && ((wish.other != null && MagicFamiliars.Bindable(wish.other, wish.Beneficiary))
                                           || MagicFamiliars.Candidate(wish.Beneficiary, wish.Beneficiary?.MapHeld) != null);

        public override LookTargets LookAt(Wish wish) => wish.other != null ? new LookTargets(wish.Beneficiary, wish.other) : base.LookAt(wish);

        /// <summary>Promised: the binding waits for the mage's quiet hour.</summary>
        public override string OnPromised(Wish wish)
        {
            var mage = wish.Beneficiary;
            var a = wish.other;
            if (a == null || !MagicFamiliars.Bindable(a, mage)) a = MagicFamiliars.Candidate(mage, mage?.MapHeld);
            if (a == null) return null;
            wish.other = a;
            return MagicFamiliars.Queue(mage, a, true) ? "CC_Fam_Promise" : null;
        }
    }
}
