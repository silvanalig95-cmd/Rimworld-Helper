using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Neighbours for good: a camp of a kind that can settle (a refugee family, a hermit) asks at its parting, after a
    /// good stay, to stay on the colony's land for good. Let settle, it becomes the colony's tiny neighbour: the same
    /// camp, kept (the <see cref="CampTracker.holding"/> slot, so camps still come and go beside it), living off the
    /// land, trading, visiting, asking and being asked, with its own troubles (illness, raiders) and its own ends
    /// (asked to move on, or grief, or things gone sour). One such holding at a time.
    /// </summary>
    public partial class Camp
    {
        public bool holding;
        public int settledTick = -1;
        public int lastStockTick = -1, lastSickDay = -1, lastVisitDay = -1, deathsSeen;

        /// <summary>The neighbours, by their family's name when they have one, else by their kind.</summary>
        public string HoldingName
        {
            get
            {
                var last = (leader?.Name as NameTriple)?.Last;
                return kind != null && kind.family && !last.NullOrEmpty() ? "CC_Holding_FamilyName".Translate(last).Resolve() : Label;
            }
        }

        public float DaysSettled => settledTick < 0 ? 0f : (Now - settledTick) / (float)GenDate.TicksPerDay;

        /// <summary>The settle question is due: at the parting of a kind that can, after a good stay, with the land free.</summary>
        public bool SettleOffered => CampStoryMath.SettleOffered(kind != null && kind.canSettle && !kind.fights, tension, DaysHere, CampTracker.Instance?.holding == null) && !holding;

        /// <summary>They stay for good: the camp becomes the colony's neighbour.</summary>
        public string Settle()
        {
            var tracker = CampTracker.Instance;
            if (tracker == null || holding || !CanTalk) return null;
            holding = true;
            settledTick = Now;
            untilTick = int.MaxValue;
            extendAsked = extended = true;
            termLimit = false;
            asked = false;
            tension = Mathf.Min(tension, 20f);
            provisions = Mathf.Max(provisions, CampStoryMath.HoldingProvisions(Here.Count));
            lastStockTick = Now;
            lastSickDay = GenDate.DaysPassed;
            lastVisitDay = GenDate.DaysPassed;
            deathsSeen = members.Count(p => p == null || p.Dead);
            if (!beats.Contains("parting")) beats.Add("parting");
            tracker.MakeHolding(this);
            FactionMemory.Note(faction, "sheltered", place, 12);
            var comp = CouncilGameComponent.Instance;
            comp?.character.Shift(new WayShift { openness = 3f, generosity = 1f }, HoldingName);
            foreach (var c in map != null ? IncidentWorker_Happening.Colonists(map) : new List<Pawn>()) CouncilGameComponent.GiveThought(c, CampDefOf.Council_Camp_GoodNeighbors);
            Tell(Text("CC_Holding_SettledTitle"), Text("CC_Holding_Settled") + "\n\n" + Text("CC_Holding_Facts"), LetterDefOf.PositiveEvent, true, leader);
            return Text("CC_Holding_SettledReply");
        }

        /// <summary>All of them taken into the colony instead: the camp and its goods come with them.</summary>
        public string TakeIn()
        {
            if (!CanTalk) return null;
            int n = 0;
            foreach (var p in Here.ToList())
                if (Welcome(p)) n++;
            return n > 0 ? Text("CC_Holding_TakenIn", null, null, n) : null;
        }

        /// <summary>Each new day of the neighbours' life: unease eases, the land feeds them, their trader restocks, one falls ill now and then, one comes to visit (or up with the carters' silver).</summary>
        private void HoldingDay()
        {
            AddTension(CampStoryMath.HoldingTensionADay);
            provisions = Mathf.Max(provisions, CampStoryMath.HoldingProvisions(Here.Count));
            int day = GenDate.DaysPassed;
            // Their once-only asks and requests come round again every so often.
            if (day % 20 == 0) asksMade.RemoveAll(m => !m.Contains("@"));
            if (trader != null && Now - lastStockTick > GenDate.TicksPerDay * CampStoryMath.HoldingStockDays)
            {
                lastStockTick = Now;
                try
                {
                    CampTracker.RefreshTrader(this);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("holding stock", e);
                }
            }
            if (day - lastSickDay >= CampStoryMath.HoldingSickDays && Rand.Chance(CampStoryMath.HoldingSickChance) && sick == null)
            {
                lastSickDay = day;
                var ask = CampStory.ByKey(CampStory.SickKey);
                asksMade.Remove(CampStory.SickKey);
                if (ask != null && Here.Count(p => !p.DevelopmentalStage.Juvenile()) >= 2 && FallIll()) Request(ask);
            }
            else if (LoadOnRoad && Now >= owedTick && !ErrandSet)
            {
                // The carters came back through: a warden brings the colony's silver up to the gate (RoadPost.cs), with word of the road.
                var pay = new DilemmaEffect_CampComes { to = "gate", count = 1, hours = new FloatRange(1f, 1.5f), when = new IntRange(8, 18), doing = "pay" };
                pay.lines = RoadWord(ErrandPeople(null, 1).FirstOrDefault());
                if (!StartErrand(pay, null)) PayRoad(null);
            }
            else if (day - lastVisitDay >= CampStoryMath.HoldingVisitDays && !ErrandSet && tension < 50f && Rand.Chance(0.5f))
            {
                lastVisitDay = day;
                var visit = new DilemmaEffect_CampComes { to = "gate", count = 1, hours = new FloatRange(1.5f, 2.5f), when = new IntRange(9, 17), doing = "visit" };
                // Road wardens come up with what the carters told them (RoadPost.cs).
                visit.lines = RoadWord(ErrandPeople(null, 1).FirstOrDefault());
                if (visit.lines != null) visit.doing = "roadword";
                StartErrand(visit, null);
            }
        }

        /// <summary>One of them killed while enemies were on the land: the colony hears, and the neighbours may bury their dead and move on.</summary>
        private void HoldingLosses()
        {
            int dead = members.Count(p => p == null || p.Dead);
            if (dead <= deathsSeen) return;
            int lost = dead - deathsSeen;
            deathsSeen = dead;
            var who = members.Where(p => p != null && p.Dead).OrderByDescending(p => p.Corpse?.Age ?? int.MaxValue).FirstOrDefault();
            if (Here.Count == 0) return;
            AddTension(15f);
            if (Rand.Chance(CampStoryMath.GriefLeaveChance))
            {
                Tell(Text("CC_Holding_LossTitle"), Text("CC_Holding_LossLeaving", who, null, lost), LetterDefOf.NegativeEvent, true, who);
                Leave("grief");
            }
            else Tell(Text("CC_Holding_LossTitle"), Text("CC_Holding_Loss", who, null, lost), LetterDefOf.NegativeEvent, true, who);
        }

        /// <summary>For the World page: who they are, how long settled, how many.</summary>
        public string HoldingLine() =>
            "CC_Holding_Line".Translate(HoldingName.Named("NAME"), (faction?.Name ?? "").Named("FACTION"), (place ?? "").Named("PLACE"),
                TextUtil.DaysString(Mathf.Max(GenDate.TicksPerHour, Now - Mathf.Max(0, settledTick))).Named("DAYS"), Here.Count.Named("COUNT")).Resolve();

        private void ExposeHolding()
        {
            Scribe_Values.Look(ref holding, "holding");
            Scribe_Values.Look(ref settledTick, "settledTick", -1);
            Scribe_Values.Look(ref lastStockTick, "lastStockTick", -1);
            Scribe_Values.Look(ref lastSickDay, "lastSickDay", -1);
            Scribe_Values.Look(ref lastVisitDay, "lastVisitDay", -1);
            Scribe_Values.Look(ref deathsSeen, "deathsSeen");
        }
    }
}
