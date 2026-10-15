using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony's animals as companions: one that fights beside its person in a battle is remembered for it, and
    /// a named or bonded animal that dies gets a farewell from the one who loved it.
    /// </summary>
    public class PetTracker : IExposable
    {
        private List<Pawn> known = new List<Pawn>();
        private bool inBattle;
        private int battleStart;
        private List<Pawn> fought = new List<Pawn>();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        /// <summary>The colonist the animal belongs to: the one it's bonded to, else its master.</summary>
        public static Pawn PersonOf(Pawn pet)
        {
            if (pet?.relations == null) return null;
            var bonded = pet.relations.DirectRelations.Where(r => r.def == PawnRelationDefOf.Bond).Select(r => r.otherPawn)
                .FirstOrDefault(p => p != null && !p.Dead && p.IsColonist && People.HasInnerLife(p));
            if (bonded != null) return bonded;
            var master = pet.playerSettings?.Master;
            return master != null && !master.Dead && master.IsColonist && People.HasInnerLife(master) ? master : null;
        }

        private static bool Bonded(Pawn pet, Pawn person) => pet?.relations != null && person != null && pet.relations.DirectRelationExists(PawnRelationDefOf.Bond, person);

        public void Hourly(CouncilGameComponent comp)
        {
            if (!S.petMoments) return;
            try
            {
                Watch(comp);
                Battle(comp);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("pets", e);
            }
        }

        private void Watch(CouncilGameComponent comp)
        {
            foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
                foreach (var a in map.mapPawns.SpawnedColonyAnimals)
                    if (!known.Contains(a) && (a.Name != null || PersonOf(a) != null)) known.Add(a);
            foreach (var pet in known.ToList())
            {
                if (pet == null || (pet.Discarded && !pet.Dead))
                {
                    known.Remove(pet);
                    continue;
                }
                if (pet.Dead)
                {
                    known.Remove(pet);
                    Farewell(comp, pet);
                    continue;
                }
                // Sold, released, run off: no farewell.
                if (pet.Faction != Faction.OfPlayer) known.Remove(pet);
            }
        }

        private void Farewell(CouncilGameComponent comp, Pawn pet)
        {
            using var _cause = Causes.Push("CC_Cause_PetDied".Translate(pet.LabelShort).Resolve());
            var person = PersonOf(pet);
            if (person == null || pet.Name == null) return;
            bool bonded = Bonded(pet, person);
            bool hero = inBattle || Find.TickManager.TicksGame - battleStart < GenDate.TicksPerDay;
            // Killed by raiders: the one it was bonded to may swear on them.
            if (bonded && hero) comp.vendettas?.Notify_PetKilled(comp, pet, person);
            float years = (pet.records?.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal) ?? 0f) / GenDate.TicksPerYear;
            string line = Flavor.FromPool("Council_Pool_PetFarewell", person, null, null, hero ? "hero" : "quiet",
                pet.RaceProps.baseBodySize >= 1.5f ? "big" : pet.RaceProps.baseBodySize <= 0.5f ? "small" : "midsize",
                years >= 2f ? "longtime" : years < 0.25f ? "newpet" : null, bonded ? "bonded" : "master");
            if (line.NullOrEmpty()) return;
            string text = line.Formatted(person.Named("PAWN"), pet.Named("PET")).Resolve();
            Notify.Message(text, person.Spawned ? new LookTargets(person) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent, bonded || hero ? Importance.Notable : Importance.Flavor);
            if (bonded || hero || pet.Name != null) comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, person));
            comp.journals.Write(person, "Council_Journal_PetFarewell", null, pet.Named("PET"));
            // Bonded animals already bring their own grief; a master who wasn't bonded still misses them.
            if (!bonded) CouncilGameComponent.GiveThought(person, CouncilDefOf.Council_Pet_Lost);
            else comp.aspirations.Notify_Event(person, "loss");
        }

        private void Battle(CouncilGameComponent comp)
        {
            int now = Find.TickManager.TicksGame;
            bool active = comp.social.battleActive;
            if (active && !inBattle)
            {
                inBattle = true;
                battleStart = now;
                fought.Clear();
            }
            if (inBattle)
            {
                foreach (var map in Find.Maps.Where(m => m.IsPlayerHome))
                {
                    foreach (var pet in map.mapPawns.SpawnedColonyAnimals)
                    {
                        if (fought.Contains(pet) || pet.mindState == null || pet.mindState.lastAttackTargetTick < battleStart) continue;
                        if (pet.mindState.lastAttackedTarget.Thing is Pawn target && target.HostileTo(Faction.OfPlayer)) fought.Add(pet);
                    }
                }
            }
            if (active || !inBattle) return;
            inBattle = false;
            foreach (var pet in fought.Where(p => p != null && !p.Dead && p.Faction == Faction.OfPlayer))
            {
                var person = PersonOf(pet);
                if (person == null) continue;
                bool defended = person.Downed || (person.mindState != null && person.mindState.lastHarmTick >= battleStart);
                string line = Flavor.FromPool("Council_Pool_PetHeroics", person, null, null, defended ? "defended" : "fought",
                    pet.RaceProps.baseBodySize >= 1.5f ? "big" : pet.RaceProps.baseBodySize <= 0.5f ? "small" : "midsize");
                string text = !line.NullOrEmpty()
                    ? line.Formatted(pet.Named("PET"), person.Named("PAWN")).Resolve()
                    : (defended ? "CC_Pet_Defended" : "CC_Pet_Fought").Translate(pet.Named("PET"), person.Named("PAWN")).Resolve();
                Notify.Message(text, new LookTargets(pet, person), MessageTypeDefOf.PositiveEvent, defended ? Importance.Notable : Importance.Flavor);
                if (!defended) continue;
                comp.AddChronicle(new ChronicleEntry(ChronicleKind.Stranger, text, person));
                CouncilGameComponent.GiveThought(person, CouncilDefOf.Council_Pet_Defended, null, -1f, "CC_Cause_PetFought".Translate(pet.LabelShort).Resolve());
                comp.journals.Write(person, "Council_Journal_PetDefended", null, pet.Named("PET"));
            }
            fought.Clear();
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) known.RemoveAll(p => p == null || p.Discarded);
            Scribe_Collections.Look(ref known, "known", LookMode.Reference);
            Scribe_Collections.Look(ref fought, "fought", LookMode.Reference);
            Scribe_Values.Look(ref inBattle, "inBattle");
            Scribe_Values.Look(ref battleStart, "battleStart");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (known == null) known = new List<Pawn>();
                known.RemoveAll(p => p == null);
                if (fought == null) fought = new List<Pawn>();
                fought.RemoveAll(p => p == null);
            }
        }
    }
}
