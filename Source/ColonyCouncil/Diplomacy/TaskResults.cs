using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What each step of a council task does (CouncilTasks.cs; the numbers are TaskMath.cs), by kind: a letter or call read
    /// at their end, a visit's day at their town, and what the task comes to once its answer is home. Results show where
    /// the player looks: the notable's regard moves with a line in their memory (their card and the town's), their offer
    /// comes as their letter, a debt of honour is owed, their caravan comes, the colony's own is asked for at the gate.
    /// </summary>
    public static class TaskResults
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static int Skill(Pawn p, SkillDef s) => p?.skills?.GetSkill(s)?.Level ?? 0;

        /// <summary>A titled head of the Empire receives a titled officer more warmly (decision 9).</summary>
        private static float Titled(Notable n, Pawn officer)
        {
            bool head = n?.pawn?.royalty?.MostSeniorTitle != null && Ages.IsEmpire(n.faction);
            bool ours = officer?.royalty?.MostSeniorTitle != null;
            return TaskMath.Titled(head, ours);
        }

        private static void Regard(Notable n, Pawn officer, float up, string note)
        {
            if (n == null || Math.Abs(up) < 0.01f) return;
            n.personal += up;
            if (officer != null) n.RegardOf(officer, true).personal += up * 0.5f;
            if (!note.NullOrEmpty()) n.Remember(note, up);
            NotableBook.Changed();
        }

        // ================= A letter read =================

        /// <summary>Their end read the officer's letter (or heard the call): the kind's step, and its words in their answer (the tone may change).</summary>
        public static string LetterRead(CouncilTask t, NeighbourLetter l, Notable n, ref AnswerTone tone)
        {
            if (t == null || n == null) return null;
            int now = Now;
            t.letters++;
            t.last = l.channel == LetterChannel.Console ? TaskStep.Call : TaskStep.Letter;
            t.lastTick = now;
            t.lastHow = (int)tone;
            float share = TaskMath.ToneShare((int)tone);
            var officer = t.officer;
            int social = Skill(officer, SkillDefOf.Social);
            string name = n.Name, who = t.OfficerName;
            Diplomacy.Dirty();
            switch (t.kind)
            {
                case TaskKind.WinOver:
                {
                    float up = TaskMath.RegardUp(social) * share * Titled(n, officer);
                    Regard(n, officer, up, TaskWords.P("CC_Task_Note_Letter", who.Named("PAWN")));
                    return up > 0f ? TaskWords.T("CC_Task_Ans_WinOver", name.Named("NAME"), who.Named("PAWN")) : null;
                }
                case TaskKind.Trade:
                {
                    float gain = TaskMath.TradeGain(social, Skill(officer, SkillDefOf.Intellectual)) * share;
                    t.progress += gain;
                    if (gain > 0f) Regard(n, officer, 1f, TaskWords.P("CC_Task_Note_Trade", who.Named("PAWN")));
                    return gain > 0f ? TaskWords.T("CC_Task_Ans_Trade", name.Named("NAME"), who.Named("PAWN")) : null;
                }
                case TaskKind.BringHome:
                    return share > 0f ? TaskWords.T("CC_Task_Ans_BringHome", name.Named("NAME"), (t.about?.LabelShort ?? t.targetName ?? "?").Named("OURS")) : null;
                case TaskKind.TheirKin:
                    if (share > 0f) Regard(n, officer, 1f, TaskWords.P("CC_Task_Note_Kin", who.Named("PAWN")));
                    return share > 0f ? TaskWords.T("CC_Task_Ans_TheirKin", name.Named("NAME")) : null;
                case TaskKind.KeepFaith:
                {
                    float up = TaskMath.RegardUp(social) * share;
                    Regard(n, officer, up, TaskWords.P("CC_Task_Note_Faith", who.Named("PAWN")));
                    if (tone <= AnswerTone.Cordial) Steady(l.map ?? CouncilTasks.MapOf(t), 1f);
                    return up > 0f ? TaskWords.T("CC_Task_Ans_KeepFaith", name.Named("NAME")) : null;
                }
                case TaskKind.SpeakFaith:
                    if (share <= 0f) return null;
                    Shake(n, social, 1);
                    return TaskWords.T("CC_Task_Ans_SpeakFaith", name.Named("NAME"));
                case TaskKind.Host:
                    if (tone >= AnswerTone.Curt) return null;
                    return TaskWords.T("CC_Task_Ans_Host", name.Named("NAME"), who.Named("PAWN"));
                default:
                    return null;
            }
        }

        /// <summary>The believers at home a little surer of the faith they share with a neighbour (the chaplain's keeping faith).</summary>
        private static void Steady(Map home, float times)
        {
            var faith = DecreeWork.ColonyFaith;
            if (home == null || faith == null || !ModsConfig.IdeologyActive) return;
            foreach (var p in home.mapPawns.FreeColonistsSpawned.ToList())
                if (p?.ideo != null && p.Ideo == faith && !p.DevelopmentalStage.Baby())
                    p.ideo.OffsetCertainty(TaskMath.CertaintyUp * times);
        }

        /// <summary>A notable of another faith a little less sure of theirs, never below a fifth (never a conversion).</summary>
        private static void Shake(Notable n, int social, int times)
        {
            var ideo = n?.pawn?.ideo;
            if (ideo == null || !ModsConfig.IdeologyActive) return;
            for (int i = 0; i < times; i++)
            {
                float now = ideo.Certainty;
                float to = TaskMath.Shaken(now, social);
                if (to < now) ideo.OffsetCertainty(to - now);
            }
        }

        // ================= A visit's day at their town =================

        /// <summary>The officer's day at their town is over: what it did, in words for the letter of their road home.</summary>
        public static string Visited(DiplomacyHome h, CouncilTask t, Settlement town, Caravan c)
        {
            var home = h?.Map;
            var officer = t.officer;
            var f = CouncilTasks.PeopleOf(t) ?? town?.Faction;
            var n = CouncilTasks.WriteTo(t, home);
            int social = Skill(officer, SkillDefOf.Social);
            string who = t.OfficerName, at = town?.Name ?? t.townName ?? "?";
            t.last = TaskStep.Visit;
            t.lastTick = Now;
            switch (t.kind)
            {
                case TaskKind.WinOver:
                case TaskKind.KeepFaith:
                {
                    float up = TaskMath.RegardUp(social) * TaskMath.VisitTimes * Titled(n, officer);
                    Regard(n, officer, up, TaskWords.P("CC_Task_Note_Visit", who.Named("PAWN")));
                    if (t.kind == TaskKind.KeepFaith) Steady(home, 2f);
                    return TaskWords.P("CC_Task_Visit_" + t.kind, who.Named("PAWN"), (n?.Name ?? "?").Named("NAME"), at.Named("TOWN"));
                }
                case TaskKind.SpeakFaith:
                    Shake(n, social, 2);
                    return TaskWords.P("CC_Task_Visit_SpeakFaith", who.Named("PAWN"), (n?.Name ?? "?").Named("NAME"), at.Named("TOWN"));
                case TaskKind.Trade:
                    t.progress += TaskMath.TradeGain(social, Skill(officer, SkillDefOf.Intellectual)) * TaskMath.VisitTimes;
                    Regard(n, officer, 2f, TaskWords.P("CC_Task_Note_Visit", who.Named("PAWN")));
                    return TaskWords.P("CC_Task_Visit_Trade", who.Named("PAWN"), (n?.Name ?? "?").Named("NAME"), at.Named("TOWN"));
                case TaskKind.Secrets:
                    return KeeperOfSecrets.Step(h, t, home, KeeperMath.VisitBonus, TaskStep.Visit, null);
                case TaskKind.Drill:
                    if (h != null && f != null)
                    {
                        h.drillPeople = f.loadID;
                        h.drillAt = Now;
                    }
                    t.drilledAt = Now;
                    Regard(n, officer, 3f, TaskWords.P("CC_Task_Note_Drill", who.Named("PAWN")));
                    return TaskWords.P("CC_Task_Visit_Drill", who.Named("PAWN"), TaskWords.People(f).Named("PEOPLE"), at.Named("TOWN"), TaskWords.Levy(home, f).Named("LEVY"));
                case TaskKind.Tend:
                    return Tend(t, f, n, c, home, at);
                default:
                    return null;
            }
        }

        /// <summary>
        /// The physician's day at a sick town: the medicine carried is handed over (their people remember it, their head
        /// thinks better of the colony), and one visit in five one of the party catches it (the game's own flu), named
        /// before the choice.
        /// </summary>
        private static string Tend(CouncilTask t, Faction f, Notable n, Caravan c, Map home, string at)
        {
            string who = t.OfficerName;
            int given = 0, gift = Mathf.Clamp((int)t.progress, 0, TaskMath.Medicine);
            if (c != null)
                foreach (var thing in CaravanInventoryUtility.AllInventoryItems(c).Where(x => x.def.IsMedicine).ToList())
                {
                    if (given >= gift) break;
                    int take = Mathf.Min(thing.stackCount, gift - given);
                    var part = thing.SplitOff(take);
                    given += take;
                    part.Destroy();
                }
            if (f != null && given > 0) KinMemory.Note(f, "tended", t.officer, at);
            Regard(n, t.officer, given > 0 ? TaskMath.TendRegard : TaskMath.TendRegard / 3f, TaskWords.P("CC_Task_Note_Tend", who.Named("PAWN")));
            string line = given > 0
                ? TaskWords.P("CC_Task_Visit_Tend", who.Named("PAWN"), at.Named("TOWN"), given.Named("COUNT"))
                : TaskWords.P("CC_Task_Visit_TendNone", who.Named("PAWN"), at.Named("TOWN"));
            // The sickness comes home with them, one visit in five.
            if (c != null && TaskMath.SickBack(Rand.Value))
            {
                var sick = c.PawnsListForReading.Where(p => p != null && p.IsColonist && !p.Dead && p.RaceProps.Humanlike).RandomElementWithFallback();
                var flu = DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
                if (sick != null && flu != null && sick.health?.hediffSet?.HasHediff(flu) == false)
                {
                    sick.health.AddHediff(flu);
                    line += "\n\n" + TaskWords.P("CC_Task_Visit_Caught", sick.Named("PAWN"), at.Named("TOWN"), flu.label.Named("SICKNESS"));
                }
            }
            return line;
        }

        // ================= What a task comes to =================

        /// <summary>
        /// The hour's look for a task's end, once the answer of its last letter is home and nobody is away: won over, the
        /// trade open, the colony's own home, their kin all gone home, the feast held. True when it ended (or a step was taken).
        /// </summary>
        public static bool Finished(DiplomacyHome h, CouncilTask t, Map home, int now)
        {
            bool letterOut = t.letterId >= 0 && (NeighbourLetters.Get(t.letterId)?.Open ?? false);
            if (letterOut || t.visit != VisitStage.None) return false;
            var f = CouncilTasks.PeopleOf(t);
            switch (t.kind)
            {
                case TaskKind.WinOver:
                {
                    var n = CouncilTasks.NotableOf(t);
                    if (n == null || t.Steps == 0 || !TaskMath.Won(NotableBook.Opinion(n))) return false;
                    Won(h, t, n, home);
                    return true;
                }
                case TaskKind.Trade:
                    if (t.progress < TaskMath.TradeGoal) return false;
                    Traded(h, t, home);
                    return true;
                case TaskKind.BringHome:
                {
                    var p = t.about;
                    if (p == null || p.Dead)
                    {
                        CouncilTasks.End(h, t, TaskStage.Off, TaskWords.P("CC_Task_Result_Lost", t.OfficerName.Named("PAWN"), (t.targetName ?? "?").Named("OURS")), true);
                        return true;
                    }
                    if (p.Faction == Faction.OfPlayer && !People.Kidnapped(p) && (p.Spawned || p.GetCaravan() != null))
                    {
                        CouncilTasks.End(h, t, TaskStage.Done, TaskWords.P("CC_Task_Result_Home", t.OfficerName.Named("PAWN"), p.Named("OURS")), true);
                        return true;
                    }
                    // Their word on a price answered or let lapse, and the colonist still held: the warden writes on, and a lower price can come.
                    if (t.asked && Ransoms.For(p) == null) t.asked = false;
                    if (!t.asked && t.letters >= (int)t.progress + TaskMath.DemandAfter && f != null && Ransoms.For(p) == null)
                    {
                        float factor = TaskMath.PriceFactor(Skill(t.officer, SkillDefOf.Social), t.letters);
                        if (Ransoms.DemandFor(home, p, f, factor))
                        {
                            t.asked = true;
                            t.progress = t.letters;
                            t.result = TaskWords.P("CC_Task_Result_Price", t.OfficerName.Named("PAWN"), p.Named("OURS"), TaskWords.People(f).Named("PEOPLE"));
                            Diplomacy.Dirty();
                        }
                    }
                    return false;
                }
                case TaskKind.TheirKin:
                    if (f != null && t.letters > 0 && !home.mapPawns.PrisonersOfColonySpawned.Any(p => p?.Faction == f))
                    {
                        CouncilTasks.End(h, t, TaskStage.Done, TaskWords.P("CC_Task_Result_KinHome", t.OfficerName.Named("PAWN"), TaskWords.People(f).Named("PEOPLE")), true);
                        return true;
                    }
                    return false;
                case TaskKind.Host:
                {
                    var feast = h.feast;
                    if (feast != null && f != null && feast.guests.Any(g => g.people == f))
                    {
                        if (t.feastId != feast.id)
                        {
                            t.feastId = feast.id;
                            t.asked = true;
                            Diplomacy.Dirty();
                        }
                        return false;
                    }
                    if (t.feastId >= 0 && h.lastFeast != null && h.lastFeast.id == t.feastId && feast == null)
                    {
                        t.last = TaskStep.Feast;
                        t.lastTick = now;
                        CouncilTasks.End(h, t, TaskStage.Done, TaskWords.P("CC_Task_Result_Feast", t.OfficerName.Named("PAWN"), TaskWords.People(f).Named("PEOPLE")), true);
                        return true;
                    }
                    return false;
                }
                default:
                    return false;
            }
        }

        /// <summary>Won over: their offer of a pact when one can be made (a defence pact against an enemy of both, else a trade pact), else a debt of honour.</summary>
        private static void Won(DiplomacyHome h, CouncilTask t, Notable n, Map home)
        {
            var f = n.faction;
            var enemy = Pacts.CommonEnemy(f);
            bool defence = enemy != null && Pacts.WhyNot(f, home, PactKind.Defence) == null;
            bool trade = Pacts.WhyNot(f, home, PactKind.Trade) == null;
            string what = TaskMath.WinResult(defence, trade);
            string line;
            if (what == "defence")
            {
                Pacts.Offer(f, home, PactKind.Defence, enemy, null);
                line = TaskWords.P("CC_Task_Result_WonPact", t.OfficerName.Named("PAWN"), n.Name.Named("NAME"));
            }
            else if (what == "trade")
            {
                OfferTrade(f, home, n, "CC_Task_TradeOffer_Won");
                line = TaskWords.P("CC_Task_Result_WonPact", t.OfficerName.Named("PAWN"), n.Name.Named("NAME"));
            }
            else if (!Diplomacy.FavoursOn)
                line = TaskWords.P("CC_Task_Result_Won", t.OfficerName.Named("PAWN"), n.Name.Named("NAME"));
            else
            {
                FavoursOwed.Make(f, home, true, "wonOver");
                line = TaskWords.P("CC_Task_Result_WonFavour", t.OfficerName.Named("PAWN"), n.Name.Named("NAME"), TaskWords.People(f).Named("PEOPLE"),
                    DipWords.Form(home, f).word.Named("WORD"));
            }
            CouncilTasks.End(h, t, TaskStage.Done, line, true);
        }

        /// <summary>The trade open: their trade pact offered when one can be made, else their trader's caravan sent within a few days.</summary>
        private static void Traded(DiplomacyHome h, CouncilTask t, Map home)
        {
            var f = CouncilTasks.PeopleOf(t);
            var n = CouncilTasks.NotableOf(t);
            string line;
            if (f != null && TaskMath.TradeResult(Pacts.WhyNot(f, home, PactKind.Trade) == null) == "pact")
            {
                OfferTrade(f, home, n ?? FavourCalls.HeadOf(f, home), "CC_Task_TradeOffer");
                line = TaskWords.P("CC_Task_Result_TradePact", t.OfficerName.Named("PAWN"), TaskWords.People(f).Named("PEOPLE"));
            }
            else if (f != null && SendCaravan(f, home))
                line = TaskWords.P("CC_Task_Result_Caravan", t.OfficerName.Named("PAWN"), TaskWords.People(f).Named("PEOPLE"));
            else if (f != null && Diplomacy.FavoursOn)
            {
                FavoursOwed.Make(f, home, true, "wonOver");
                line = TaskWords.P("CC_Task_Result_TradeFavour", t.OfficerName.Named("PAWN"), TaskWords.People(f).Named("PEOPLE"));
            }
            else line = TaskWords.P("CC_Task_Result_TradeNone", t.OfficerName.Named("PAWN"), TaskWords.People(f).Named("PEOPLE"));
            CouncilTasks.End(h, t, TaskStage.Done, line, true);
        }

        /// <summary>Their trade pact offered in their own letter (the pacts' letter, its own words: a trade pact needs no common enemy).</summary>
        private static void OfferTrade(Faction f, Map home, Notable who, string key)
        {
            var def = PactsDefOf.Rimfolk_PactOffer;
            if (def == null || f == null || home == null) return;
            var form = PactWords.PactForm(home, f);
            string name = who != null ? NotableBook.Called(who) : TaskWords.People(f);
            string text = TaskWords.T(key, name.Named("WHO"), TaskWords.People(f).Named("PEOPLE"), form.rite.Named("RITE"), PactWords.Kind(PactKind.Trade).Named("KIND"))
                          + "\n\n" + PactWords.P("CC_Pact_What_" + PactKind.Trade, PactWords.People(f).Named("PEOPLE"));
            var town = who != null ? NotableBook.TownOf(who.town) : WorldSituationWorker.NearestTown(f, home);
            var letter = LetterMaker.MakeLetter(PactWords.P("CC_Pact_Offer_Label", PactWords.People(f).Named("PEOPLE")), text, def, town != null ? new LookTargets(town) : LookTargets.Invalid, f);
            if (letter is ChoiceLetter_PactOffer l)
            {
                l.people = f;
                l.home = Homes.KeyOf(home);
                l.kind = (int)PactKind.Trade;
                l.renew = -1;
            }
            Diplomacy.Write(DiplomacyMath.PactOffer, new List<HistoryActor> { new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(Diplomacy.Colony(home), HistoryRole.For) },
                Diplomacy.Colony(home), PactWords.P("CC_Pact_Hist_Offer", PactWords.People(f).Named("PEOPLE"), PactWords.Kind(PactKind.Trade).Named("KIND")));
            Tell.Ask(letter, home);
        }

        /// <summary>Their trader's caravan, on its way in one to three days (the game's own trader caravan, their people's).</summary>
        private static bool SendCaravan(Faction f, Map home)
        {
            try
            {
                if (f?.def?.caravanTraderKinds == null || f.def.caravanTraderKinds.Count == 0 || Diplomacy.Hostile(f) || home == null) return false;
                var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, home);
                parms.faction = f;
                parms.forced = true;
                int at = Now + Mathf.RoundToInt(TaskMath.CaravanDays(Rand.Value) * TaskMath.DayTicks);
                Find.Storyteller.incidentQueue.Add(IncidentDefOf.TraderCaravanArrival, at, parms, TaskMath.DayTicks);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("council tasks: their caravan", e);
                return false;
            }
        }

        // ================= Read by other parts =================

        /// <summary>The steward's guests (Feasts.End): a live hosting task at this home for their people.</summary>
        public static bool Stewards(Map home, Faction f)
        {
            if (home == null || f == null) return false;
            var h = DiplomacyBook.Instance?.Existing(home);
            return h != null && h.tasks.Any(t => t != null && t.kind == TaskKind.Host && t.Live && t.people == f);
        }

        /// <summary>The warden writes to them about their kin in this home's cells (Ransoms: their offers sooner and higher).</summary>
        public static bool KinTask(Faction f, Map home)
        {
            if (home == null || f == null) return false;
            var h = DiplomacyBook.Instance?.Existing(home);
            return h != null && h.tasks.Any(t => t != null && t.kind == TaskKind.TheirKin && t.Live && t.people == f && t.letters > 0);
        }

        /// <summary>Their band's strength when it comes to this home (Pacts.Arrive): a fifth more for a season after the marshal drilled them.</summary>
        public static float LevyStrength(Faction f, Map home)
        {
            var h = DiplomacyBook.Instance?.Existing(home);
            if (h == null || f == null || h.drillPeople != f.loadID) return 1f;
            return TaskMath.LevyFactor(h.drillAt, Now);
        }
    }
}
