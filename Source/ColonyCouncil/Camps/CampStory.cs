using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A camp's stay as a story (CampStoryMath): they arrive and settle; a day and a half in they ask something of the
    /// colony or do it a good turn; midway through a longer stay trouble comes (one of the council's questions about
    /// them, or a trouble every camp can have: one of theirs falls ill, their cart breaks); in the last day whoever
    /// speaks for them comes to say goodbye, and the colony can ask them back or give them provisions for the road.
    /// A camp that is the same people come back (CampMemory.cs) settles in quicker.
    /// </summary>
    public partial class Camp
    {
        /// <summary>The beats of the stay already played: ask, trouble, parting.</summary>
        public List<string> beats = new List<string>();
        /// <summary>The remembered camp this one is the return of (its PastCamp id; 0: a new party), and how many times these people have camped here before.</summary>
        public int returnOf;
        public int returns;
        /// <summary>The colony's parting words: asked back, given provisions.</summary>
        public bool invited, provisioned;
        /// <summary>One of theirs lying ill at the camp (the trouble of the stay).</summary>
        public Pawn sick;
        /// <summary>A war party, fighting again: a new threat came while they rested.</summary>
        public bool fighting;

        /// <summary>A war party's enemy: whom they came to fight.</summary>
        public Faction enemy;

        public bool Familiar => returns > 0;

        /// <summary>The fever one of them takes: the game's flu.</summary>
        public static HediffDef Fever => DefDatabase<HediffDef>.GetNamedSilentFail("Flu");

        private void ExposeStory()
        {
            Scribe_Collections.Look(ref beats, "beats", LookMode.Value);
            Scribe_Values.Look(ref returnOf, "returnOf");
            Scribe_Values.Look(ref returns, "returns");
            Scribe_Values.Look(ref invited, "invited");
            Scribe_Values.Look(ref provisioned, "provisioned");
            Scribe_Values.Look(ref fighting, "fighting");
            Scribe_References.Look(ref sick, "sick");
            Scribe_References.Look(ref enemy, "enemy");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) beats ??= new List<string>();
        }

        /// <summary>The stay as it stands (extended or cut short), from their arrival; the stay they meant to make before that.</summary>
        public float StayDays => (untilTick != int.MaxValue && arrivedTick >= 0 ? untilTick - arrivedTick : stayTicks) / (float)GenDate.TicksPerDay;

        /// <summary>The day's beat, if one is due at the event hour: true when it took the day's event.</summary>
        private bool StoryBeat()
        {
            float here = DaysHere, stay = StayDays;
            if (!beats.Contains("ask") && CampStoryMath.AskDue(here, stay))
            {
                beats.Add("ask");
                if (TryRequest() || Help()) return true;
            }
            if (!beats.Contains("trouble") && CampStoryMath.TroubleDue(here, stay))
            {
                beats.Add("trouble");
                if (Trouble()) return true;
            }
            return false;
        }

        /// <summary>Every hour: the parting word within the last day, when the colony has no other question of theirs open.</summary>
        private void StoryParting()
        {
            if (beats.Contains("parting") || phase != CampPhase.Living || !CampStoryMath.PartingDue(DaysHere, StayDays) || Hostile) return;
            if (Find.LetterStack.LettersListForReading.Any(l => l is ChoiceLetter_Camp c && c.campId == id && !c.resolved)) return;
            beats.Add("parting");
            var speaker = Speaker;
            if (speaker == null) return;
            string key = helps + gifts + tradeDays > 0 ? "CC_Camp_Parting_Warm" : tension >= 40f ? "CC_Camp_Parting_Cool" : "CC_Camp_Parting";
            if (Familiar) key = "CC_Camp_Parting_Again";
            Ask("parting", Text("CC_Camp_PartingTitle"), Text(key, speaker, null, CampStoryMath.Provisions(Here.Count)), speaker, 0.6f);
        }

        /// <summary>Trouble midway: one of the council's questions about this camp, else a trouble every camp can have.</summary>
        private bool Trouble()
        {
            var comp = CouncilGameComponent.Instance;
            try
            {
                if (comp != null && comp.dilemmas.TryFire(false, d => d.workerClass != null && typeof(DilemmaWorker_Camp).IsAssignableFrom(d.workerClass))) return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp trouble", e);
            }
            var open = CampStory.Common.Where(a => CanBefall(a)).ToList();
            if (open.Count == 0) return false;
            var ask = open.RandomElement();
            if (ask.key == CampStory.SickKey && !FallIll()) return false;
            return Request(ask);
        }

        /// <summary>Whether a common trouble fits this camp now.</summary>
        private bool CanBefall(CampAsk a)
        {
            if (a.key == CampStory.SickKey) return Here.Count(p => !p.DevelopmentalStage.Juvenile()) >= 2 && !kind.fights;
            if (a.key == CampStory.CartKey) return HasCart && AskBlocked(a) == null;
            return AskBlocked(a) == null;
        }

        /// <summary>They came with a cart: pack animals, a trader's stock, or goods put out around the camp.</summary>
        public bool HasCart => kind != null && (kind.trader || (kind.animals != null && kind.animals.Any(a => a != null && a.pack)) || !kind.campGoods.NullOrEmpty());

        /// <summary>One of them (not whoever speaks for them) takes a fever at the camp.</summary>
        private bool FallIll()
        {
            var fever = Fever;
            if (fever == null) return false;
            var who = Here.Where(p => p != leader && p != trader && !p.DevelopmentalStage.Juvenile() && !p.Downed && p.RaceProps.Humanlike && p.health != null
                                     && !p.health.hediffSet.HasHediff(fever)).RandomElementWithFallback();
            if (who == null) return false;
            try
            {
                var h = HediffMaker.MakeHediff(fever, who);
                h.Severity = 0.35f;
                who.health.AddHediff(h);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp sickness", e);
                return false;
            }
            sick = who;
            return true;
        }

        /// <summary>The colony's answer to the parting word.</summary>
        public string Parting(string answer)
        {
            switch (answer)
            {
                case "invite":
                    invited = true;
                    AddTension(-3f);
                    FactionMemory.Note(faction, "welcomed", place, 3);
                    return Text(Familiar ? "CC_Camp_PartingInvited_Again" : "CC_Camp_PartingInvited");
                case "provisions":
                {
                    int meals = CampStoryMath.Provisions(Here.Count);
                    int given = TakeMeals(meals);
                    if (given <= 0) return Text("CC_Camp_PartingNoMeals");
                    provisioned = true;
                    gifts++;
                    AddTension(-5f);
                    KinMemory.Note(faction, "gift", null, place);
                    return Text("CC_Camp_PartingProvisioned", null, null, given);
                }
                default:
                    return null;
            }
        }

        /// <summary>Meals in the colony's stores that could go with them.</summary>
        public int MealsInStore()
        {
            if (map == null) return 0;
            int n = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree))
                if (t.Spawned && t.def.IsIngestible && t.def.ingestible.IsMeal && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage()) n += t.stackCount;
            return n;
        }

        /// <summary>Meals from the colony's stores into their packs. Returns how many went.</summary>
        private int TakeMeals(int count)
        {
            if (map == null) return 0;
            var carriers = Here.Where(p => p.inventory != null).ToList();
            if (carriers.Count == 0) return 0;
            int left = count, who = 0;
            foreach (var t in map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                         .Where(t => t.Spawned && t.def.IsIngestible && t.def.ingestible.IsMeal && !t.IsForbidden(Faction.OfPlayer) && t.IsInAnyStorage())
                         .OrderBy(t => t.MarketValue).ToList())
            {
                if (left <= 0) break;
                int n = Mathf.Min(left, t.stackCount);
                var taken = t.SplitOff(n);
                if (!carriers[who++ % carriers.Count].inventory.innerContainer.TryAdd(taken)) taken.Destroy();
                left -= n;
            }
            return count - left;
        }
    }

    /// <summary>The troubles every camp can have, as requests of theirs (CampRequests.cs), with the effects that answer them.</summary>
    public static class CampStory
    {
        public const string SickKey = "sick", CartKey = "cart";

        private static List<CampAsk> common;

        /// <summary>One of theirs is ill and they ask for the colony's doctor; their cart broke and they ask for the makings.</summary>
        public static List<CampAsk> Common => common ??= new List<CampAsk>
        {
            new CampAsk
            {
                key = SickKey, label = "CC_CampTrouble_Sick_Grant".Translate(), skill = SkillDefOf.Medicine, once = true,
                askTexts = Lines("CC_CampTrouble_Sick_Ask"), outcomes = Lines("CC_CampTrouble_Sick_Done"),
                refuseLabel = "CC_CampTrouble_Sick_Refuse".Translate(), refused = Lines("CC_CampTrouble_Sick_Refused"),
                tension = -8f, warm = 2, deed = "campHealed", refuseTension = 8f, refuseGoodwill = 2, theyAsk = true,
                effects = new List<DilemmaEffect> { new DilemmaEffect_CampTendSick() }
            },
            new CampAsk
            {
                key = CartKey, label = "CC_CampTrouble_Cart_Grant".Translate(), skill = SkillDefOf.Construction, once = true, needs = "cart",
                askTexts = Lines("CC_CampTrouble_Cart_Ask"), outcomes = Lines("CC_CampTrouble_Cart_Done"),
                refuseLabel = "CC_CampTrouble_Cart_Refuse".Translate(), refused = Lines("CC_CampTrouble_Cart_Refused"),
                tension = -6f, warm = 1, deed = "gift", refuseTension = 4f, theyAsk = true,
                effects = new List<DilemmaEffect> { new DilemmaEffect_CampCartParts() }
            }
        };

        /// <summary>The lines under a key and its numbered fellows (CC_X, CC_X_2, CC_X_3...).</summary>
        private static List<string> Lines(string key)
        {
            var list = new List<string>();
            if (key.CanTranslate()) list.Add(key.Translate().RawText);
            for (int i = 2; i <= 6; i++)
            {
                string more = key + "_" + i;
                if (more.CanTranslate()) list.Add(more.Translate().RawText);
            }
            return list;
        }

        public static CampAsk ByKey(string key) => key == null ? null : Common.FirstOrDefault(a => a.key == key);
    }

    /// <summary>The colony's doctor (A) sees to the one lying ill at the camp: tended, and well on the way to mending.</summary>
    public class DilemmaEffect_CampTendSick : DilemmaEffect
    {
        public override string Unavailable(DilemmaContext ctx)
        {
            var c = CampTracker.Instance?.camp;
            if (c?.sick == null || c.sick.Dead || !c.sick.Spawned) return "CC_CampTrouble_NobodySick".Translate();
            if (ctx.a == null || ctx.a.WorkTagIsDisabled(WorkTags.Caring)) return "CC_CampTrouble_NoDoctor".Translate();
            return null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.Instance?.camp;
            var sick = c?.sick;
            if (sick == null || sick.Dead || ctx.a == null) return;
            try
            {
                var medicine = DilemmaEffect_Consume.Medicine(c.map).FirstOrDefault();
                Medicine med = null;
                if (medicine != null) med = medicine.SplitOff(1) as Medicine;
                TendUtility.DoTend(ctx.a, sick, med);
                if (med != null && !med.Destroyed) med.Destroy();
                var flu = Camp.Fever != null ? sick.health.hediffSet.GetFirstHediffOfDef(Camp.Fever) : null;
                if (flu != null) flu.Severity = Mathf.Min(flu.Severity, 0.15f);
                KinMemory.Notify_Tended(ctx.a, sick);
                CouncilRecords.Add(ctx.a, CouncilRecords.CampsGreeted);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp sick tended", e);
            }
            c.sick = null;
        }
    }

    /// <summary>The makings of a cart's mending from the colony's stores: wood for a wheel, or steel for an axle in a later age.</summary>
    public class DilemmaEffect_CampCartParts : DilemmaEffect
    {
        public static ThingDef Part => Avail.IsPreIndustrial ? ThingDefOf.WoodLog : ThingDefOf.Steel;
        public static int Count => Avail.IsPreIndustrial ? 25 : 15;

        public override string Unavailable(DilemmaContext ctx)
        {
            if (ctx.map == null) return "CC_CampAsk_NotNow".Translate();
            return ctx.map.resourceCounter.GetCount(Part) < Count ? "CC_Dilemma_NotEnough".Translate(Count, Part.label).ToString() : null;
        }

        public override void Apply(DilemmaContext ctx)
        {
            var c = CampTracker.Instance?.camp;
            if (ctx.map == null || c == null) return;
            int left = Count;
            var carriers = c.Here.Where(p => p.inventory != null).ToList();
            foreach (var t in ctx.map.listerThings.ThingsOfDef(Part).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer)).OrderByDescending(t => t.IsInAnyStorage()).ToList())
            {
                if (left <= 0) break;
                int n = Mathf.Min(left, t.stackCount);
                var taken = t.SplitOff(n);
                if (carriers.Count == 0 || !carriers[0].inventory.innerContainer.TryAdd(taken)) taken.Destroy();
                left -= n;
            }
        }
    }
}
