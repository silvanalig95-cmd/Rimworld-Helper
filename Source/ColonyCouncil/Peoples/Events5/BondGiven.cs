using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    // ===================== 39. The bond freely given (the Circuit of Tamers) =====================

    /// <summary>
    /// The bond freely given (item 39, a gift): a roaming tamer of the Circuit finishes the training of one of the colony's
    /// animals in an afternoon where it stands, with the colony's best hand at beasts beside them (round 1's visitor who works
    /// on a colonist, here on a beast: Peoples/Events/PeopleWorkOn.cs). The beast learns every task it can (the game's own
    /// training, each step), and is bonded to that hand, its master; the hand gets a lesson in handling, and with Vanilla
    /// Skills Expanded the Trainer expertise. With PokéWorld, a Pokémon of the colony's is trained the same way, and chosen
    /// first. Or the tamer only looks the beast over and gives the hand a few words.
    /// </summary>
    public static class BondGiven
    {
        public const string GiftKind = "bond";

        public static JobDef BondJob => DefDatabase<JobDef>.GetNamedSilentFail("Rimfolk_PEv5_Bond");

        /// <summary>What the beast could still learn: the tasks its kind can take that it hasn't learned, prerequisites first.</summary>
        public static List<TrainableDef> Left(Pawn a)
        {
            var list = new List<TrainableDef>();
            if (a?.training == null) return list;
            foreach (var td in DefDatabase<TrainableDef>.AllDefsListForReading.OrderBy(t => t.prerequisites?.Count ?? 0).ThenBy(t => t.listPriority))
            {
                try
                {
                    if (!a.training.HasLearned(td) && a.training.CanAssignToTrain(td, out bool visible).Accepted && visible) list.Add(td);
                }
                catch
                {
                    // A trainable of a mod's own that won't say: left out.
                }
            }
            return list;
        }

        /// <summary>The bond a hand already has with another beast, or another hand with this one, stands in the way of none: one bond is the game's own rule.</summary>
        private static bool BondFree(Pawn a, Pawn hand) =>
            a.relations == null || !a.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Bond && r.otherPawn != hand);

        /// <summary>The colony's beast to train: grown, its own, at home and up, with something left to learn and free to bond with the hand; a Pokémon first.</summary>
        public static Pawn Beast(Map map, Pawn hand)
        {
            if (map == null || hand == null) return null;
            return map.mapPawns.SpawnedColonyAnimals.Where(a => a != null && !a.Dead && !a.Downed && !a.InMentalState && a.ageTracker.CurLifeStage?.developmentalStage != DevelopmentalStage.Baby
                                                               && a.RaceProps.Animal && BondFree(a, hand) && Left(a).Count > 0)
                .OrderByDescending(a => PokeWorldLink.IsPokemon(a) ? 1 : 0).ThenByDescending(a => Left(a).Count).ThenByDescending(a => a.MarketValue).FirstOrDefault();
        }

        /// <summary>The colony's best hand at beasts, up and about, who has not had a gift of this kind.</summary>
        public static Pawn Hand(Map map) =>
            PeoplesD.Best(map, SkillDefOf.Animals, p => PeopleEvents.MayGive(GiftKind, p) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Handling) && !p.DevelopmentalStage.Juvenile());

        /// <summary>The afternoon's work done: every task the beast can take learned, bonded to the hand, the hand's lesson and the expertise.</summary>
        public static void Trained(Pawn tamer, Pawn beast, Pawn hand)
        {
            if (beast == null || beast.Dead || hand == null || hand.Dead) return;
            // together: the tamer has just worked the beast through its tasks with the hand at its side (PeopleWorkOn kept them there).
            var learned = new List<string>();
            // A task opens once the one before it is learned (release after obedience): round after round until none is left.
            for (int round = 0; round < 6; round++)
            {
                var left = Left(beast);
                if (left.Count == 0) break;
                foreach (var td in left)
                    try
                    {
                        beast.training.SetWantedRecursive(td, true);
                        beast.training.Train(td, hand, true);
                        if (beast.training.HasLearned(td) && !learned.Contains(td.label)) learned.Add(td.label);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("peoples' events 5: a beast's training", e);
                    }
            }
            bool bonded = false;
            try
            {
                bonded = beast.relations != null && (beast.relations.DirectRelationExists(PawnRelationDefOf.Bond, hand) || RelationsUtility.TryDevelopBondRelation(hand, beast, 1f));
                if (beast.playerSettings != null && beast.training.HasLearned(TrainableDefOf.Obedience)) beast.playerSettings.Master = hand;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("peoples' events 5: a beast's bond", e);
            }
            hand.skills?.Learn(SkillDefOf.Animals, PeopleEvents5Math.BondXp, true);
            PeopleEventsBook.Gave(GiftKind, hand);
            bool expert = Expertise.Give(hand, "Trainer");
            var f = tamer?.Faction;
            if (f != null && f.HasGoodwill && !f.IsPlayer && !f.HostileTo(Faction.OfPlayer)) WorldSituationWorker.Goodwill(f, PeopleEvents5Math.BondGoodwill, GoodwillWhy.Helped);
            if (f != null) KinMemory.Note(f, "learnedHere", hand);
            if (tamer != null) PeoplesG.SayLine(tamer, DefDatabase<IncidentDef>.GetNamedSilentFail(HappeningWorker_BondGiven.EventDef), "bonded");
            string what = learned.Count > 0 ? TextUtil.JoinAnd(learned) : "CC_PEv5_BondNothingNew".Translate().Resolve();
            Tell.Message(NewsKind.People, Importance.Notable, hand.MapHeld,
                (bonded ? "CC_PEv5_Bonded" : "CC_PEv5_BondTrained").Translate(beast.Named("BEAST"), hand.Named("PAWN"), what.Named("WHAT")).Resolve()
                + (expert ? " " + "CC_PEv5_BondExpert".Translate(hand.Named("PAWN")).Resolve() : ""),
                beast.Spawned ? new LookTargets(beast) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }
    }

    /// <summary>
    /// A roaming tamer of {FACTION} ({X}) at the gate: they would finish {BEAST}'s training this afternoon with {A} beside them,
    /// and leave the two of them bonded ({COUNT} tasks still to learn).
    /// </summary>
    public class HappeningWorker_BondGiven : HappeningWorker_Visitors
    {
        public const string EventDef = "Rimfolk_Evt_BondGiven";

        protected override bool CanHappen(Map map, HappeningExtension ext)
        {
            if (PeoplesD.Friendly(PeoplesMath.Tamers) == null || !PeopleEventsBook.GiftOpen || map.dangerWatcher.DangerRating != StoryDanger.None) return false;
            var hand = BondGiven.Hand(map);
            return hand != null && BondGiven.Beast(map, hand) != null;
        }

        protected override bool Cast(HappeningContext ctx, HappeningExtension ext)
        {
            ctx.faction = PeoplesD.Friendly(PeoplesMath.Tamers);
            ctx.a = BondGiven.Hand(ctx.map);
            ctx.pet = BondGiven.Beast(ctx.map, ctx.a);
            if (ctx.faction == null || ctx.a == null || ctx.pet == null) return false;
            ctx.beast = ctx.pet.LabelShort;
            ctx.count = BondGiven.Left(ctx.pet).Count;
            ctx.stranger = Arrivals.Make(PeoplesD.KindNamed(ctx.faction, "Rimfolk_Tamers_Tamer"), ctx.faction, ext.ages);
            if (ctx.stranger == null) return false;
            StorytellerEvents.Skilled(ctx.stranger, SkillDefOf.Animals, new IntRange(12, 18));
            ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;
            return true;
        }

        protected override bool Begin(HappeningContext ctx, HappeningExtension ext) =>
            Arrivals.Visit(PeoplesG.Party(ctx), ctx.map, ctx.faction, PeoplesD.GateSpot(ctx.map), ext.stayHours.RandomInRange) != null;

        protected override void AddTags(HappeningContext ctx, HashSet<string> tags)
        {
            PeopleEvents.AgeTags(tags, ctx.faction);
            if (ctx.pet != null && PokeWorldLink.IsPokemon(ctx.pet)) tags.Add("pokemon");
            if (ctx.pet != null && ctx.a != null && AnimalFacts.Bonded(ctx.pet, ctx.a)) tags.Add("bonded");
        }

        public override string Unavailable(HappeningContext ctx, int option)
        {
            if (option > 1) return null;
            if (ctx.a == null || ctx.a.Dead || !ctx.a.Spawned || ctx.a.Map != ctx.map) return "CC_PEv5_HandGone".Translate().Resolve();
            if (ctx.pet == null || ctx.pet.Dead || !ctx.pet.Spawned || ctx.pet.Map != ctx.map) return "CC_PEv5_BeastGone".Translate(ctx.beast ?? "?").Resolve();
            return null;
        }

        public override string Resolved(HappeningContext ctx, int option, bool risk, bool byDefault, string outcome)
        {
            if (!byDefault) PeoplesG.Say(ctx.stranger, Ext, ctx, option == 0 ? "train" : option == 1 ? "words" : "later");
            return base.Resolved(ctx, option, risk, byDefault, outcome);
        }
    }

    /// <summary>The afternoon at the beast (PeopleWorkOn, Rimfolk_PEv5_Bond), the hand brought to its side.</summary>
    public class DilemmaEffect_BondGiven : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null || hc.pet == null) return;
            if (PlayOuts.On) sheet.Play("CC_PEv5_Impact_Bond".Translate(hc.stranger?.LabelShort ?? "?", hc.pet.LabelShort, hc.a.LabelShort).Resolve(), "CC_PEv5_Impact_BondBrief".Translate().Resolve());
            sheet.Add(ImpactKind.Skill, 1, (Expertise.CanGive(hc.a, "Trainer") ? "CC_PEv5_Impact_BondExpert" : "CC_PEv5_Impact_BondLesson").Translate(hc.a.LabelShort).Resolve(),
                "CC_PEv5_Impact_BondLessonBrief".Translate().Resolve(), "pev5bond");
            if (hc.faction != null) sheet.Goodwill(hc.faction, PeopleEvents5Math.BondGoodwill);
        }

        public override void Apply(DilemmaContext ctx)
        {
            var hc = ctx as HappeningContext;
            if (hc?.a == null || hc.pet == null) return;
            var job = BondGiven.BondJob;
            if (hc.stranger == null || job == null || !PeopleWorkOn.Begin(hc, hc.stranger, hc.pet, job, LocalTargetInfo.Invalid, hc.a.GetUniqueLoadID(), 0, PeopleEvents5Math.BondTicks, 8f))
            {
                BondGiven.Trained(hc.stranger, hc.pet, hc.a);
                return;
            }
            // The hand at the beast's side for the afternoon.
            PeopleWorkOn.Bring(hc.a, new LocalTargetInfo(hc.pet), "beside", PeopleEvents5Math.BondTicks + 5000);
        }
    }

    /// <summary>Only a look at the beast and a few words for the hand: a short lesson in handling, at the gate.</summary>
    public class DilemmaEffect_BondWords : DilemmaEffect
    {
        public override bool ChanceEach => false;

        public override void Describe(DilemmaContext ctx, ImpactSheet sheet)
        {
            if (ctx?.a != null) sheet.Add(ImpactKind.Skill, 1, "CC_PEv5_Impact_BondWords".Translate(ctx.a.LabelShort).Resolve(), "CC_PEv5_Impact_BondWordsBrief".Translate().Resolve(), "pev5words");
        }

        public override void Apply(DilemmaContext ctx) => ctx?.a?.skills?.Learn(SkillDefOf.Animals, PeopleEvents5Math.WordsXp, true);
    }

    /// <summary>The afternoon's training done: the beast's tasks, the bond, the hand's lesson (BondGiven.Trained).</summary>
    public class BondGivenDone : PeopleWorkDone
    {
        public override void Done(Visit v, Pawn worker, Pawn on)
        {
            var hand = v?.map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.GetUniqueLoadID() == PeopleWorkOn.Note(v))
                       ?? Find.Maps.SelectMany(m => m.mapPawns.FreeColonists).FirstOrDefault(p => p.GetUniqueLoadID() == PeopleWorkOn.Note(v));
            PeopleWorkOn.Release(hand);
            BondGiven.Trained(worker, on, hand);
        }

        public override void GaveUp(Visit v)
        {
            if (v?.map == null) return;
            var hand = v.map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.GetUniqueLoadID() == PeopleWorkOn.Note(v));
            PeopleWorkOn.Release(hand);
            Tell.Message(NewsKind.People, Importance.Notable, v.map, "CC_PEv_WorkGivenUp".Translate((v.x?.LabelShort ?? "?").Named("WORKER"), (v.a?.LabelShort ?? "?").Named("PAWN")).Resolve(),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }
    }
}
