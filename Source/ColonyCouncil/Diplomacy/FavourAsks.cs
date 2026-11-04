using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A people calls in what the colony owes it (docs/design/stage2-diplomacy.md, round a, item 1): a daily look at each
    /// debt of the colony's, called in at a mean of 90 days, but only with a need the world shows and at most once a season
    /// per people. Medicine while a sickness runs among them, food in a famine: their letter, answered by carrying it out to
    /// their messenger at the gate (the errands' play-out), or refused. Hands at their harvest and their homeless taken in:
    /// the existing missions, asked by them, with the debt's line in the letter; answered by the mission's own end. The
    /// colony's word on their side in a feud or a contested seat: the speaker writes it to their head (the letters'
    /// machinery). Unanswered in its days, or refused, the debt is broken: the deed "refused" and 5 goodwill. Kept, they
    /// remember the colony kept its word ("keptWord").
    /// </summary>
    public static class FavourAsks
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        // ================= The daily look =================

        /// <summary>Each debt the colony owes a people at peace, looked at once a day (when one of Rimfolk's questions may come).</summary>
        public static void Daily(int now)
        {
            if (!FavoursOwed.On || !Tell.CanAsk) return;
            foreach (var t in FavoursOwed.Live())
            {
                if (t.state != Tie.State.Open || !FavoursOwed.ColonyOwes(t)) continue;
                var f = Refs.Faction(t.to);
                if (f == null || !Diplomacy.Deals(f) || Diplomacy.Hostile(f)) continue;
                if (!FavourMath.CallsToday(Rand.Value)) continue;
                if (!FavourMath.AskAllowed(LastAsk(f), now)) continue;
                var home = FavoursOwed.HomeOf(t);
                if (home == null) continue;
                foreach (var need in Needs(f, home).InRandomOrder())
                    if (Call(t, f, need, home))
                        return;
            }
        }

        /// <summary>When they last called in a debt of the colony's (the history's "tie:favour:calledIn" with them owed), or -1.</summary>
        public static int LastAsk(Faction f)
        {
            var r = Refs.Of(f);
            foreach (var e in History.Of(r, new[] { HistoryKinds.OfTie(Tie.Kind.Favour, "calledIn") }, Now - (int)(FavourMath.AskGapDays * FavourMath.DayTicks)))
                if (e.actors.Exists(a => a.who == r && a.role == HistoryRole.For))
                    return e.tick;
            return -1;
        }

        /// <summary>What the world shows they need now: a sickness, a famine or a hard time among them, a feud or a contested seat, a harvest standing.</summary>
        public static List<FavourNeed> Needs(Faction f, Map home)
        {
            var list = new List<FavourNeed>();
            var ws = WorldSituations.Instance;
            if (ws != null)
                foreach (var s in ws.Running)
                {
                    if (s?.def == null || s.ctx == null) continue;
                    bool theirs = s.ctx.faction == f;
                    switch (s.def.defName)
                    {
                        case "Council_WS_Plague":
                            if (theirs) Add(list, FavourNeed.Medicine, FavourNeed.Refugees);
                            break;
                        case "Council_WS_Famine":
                        case "Council_WS_Locusts":
                        case "Council_WS_Drought":
                        case "Council_WS_HardWinter":
                            if (theirs) Add(list, FavourNeed.Food, FavourNeed.Refugees);
                            break;
                        case "Council_WS_Flood":
                        case "Council_WS_GreatFire":
                            if (theirs) Add(list, FavourNeed.Refugees);
                            break;
                        case "Council_WS_Feud":
                        case "Council_WS_Claimants":
                            if (theirs || s.ctx.enemy == f) Add(list, FavourNeed.Dispute);
                            break;
                    }
                }
            var season = GenLocalDate.Season(home);
            if (season == Season.Summer || season == Season.Fall || season == Season.PermanentSummer) Add(list, FavourNeed.Harvest);
            // Their town within reach attacked: fighters (release 2a.2; answered far away, CallsToArms.cs).
            if (Pacts.On && CallsToArms.Attacked(f, home, out _, out _)) Add(list, FavourNeed.Fighters);
            return list;
        }

        private static void Add(List<FavourNeed> list, params FavourNeed[] needs)
        {
            foreach (var n in needs)
                if (!list.Contains(n))
                    list.Add(n);
        }

        /// <summary>The other side of a feud they are in, or null (a contested seat is among their own).</summary>
        private static Faction OtherSide(Faction f)
        {
            var ws = WorldSituations.Instance;
            if (ws == null) return null;
            foreach (var s in ws.Running)
            {
                if (s?.def == null || s.ctx == null || s.def.defName != "Council_WS_Feud") continue;
                if (s.ctx.faction == f) return s.ctx.enemy;
                if (s.ctx.enemy == f) return s.ctx.faction;
            }
            return null;
        }

        // ================= Their call =================

        /// <summary>They call it in for this need: a mission of the game's, or their letter. False when it couldn't be asked now.</summary>
        public static bool Call(Tie t, Faction f, FavourNeed need, Map home)
        {
            if (t == null || f == null || home == null || t.state != Tie.State.Open) return false;
            int now = Now;
            if (need == FavourNeed.Fighters)
            {
                // Fighters for their town: their letter asks; answered by the colony's caravan there (CallsToArms.cs).
                if (!CallsToArms.Attacked(f, home, out var town, out var enemy)) return false;
                if (!FavoursOwed.CallIn(t, FavourMath.NeedCode(need), FavourMath.DueDay(need, now))) return false;
                t.terms.about = Refs.Of(town);
                t.terms.count = enemy != null ? enemy.loadID + 1 : 0;
                WorldLedger.Touch(t);
                CallsToArms.Letter(t, f, home, town, enemy, false);
                return true;
            }
            if (FavourMath.ByMission(need))
            {
                // The mission first: when it can't happen here, nothing was asked.
                if (!Mission(f, need, home)) return false;
                if (!FavoursOwed.CallIn(t, FavourMath.NeedCode(need), FavourMath.DueDay(need, now))) return false;
                // The mission's own letter asks it; the debt's line goes in it.
                var form = DipWords.Form(home, f);
                FavoursOwed.SayWithLetter(f, home, DipWords.T("CC_Dip_Line_TheyCall", DipWords.People(f).Named("PEOPLE"), form.word.Named("WORD"),
                    form.rite.Named("RITE"), DipWords.Why(t).Named("WHY")));
                return true;
            }
            var letter = Letter(t, f, need, home);
            if (letter == null || !FavoursOwed.CallIn(t, FavourMath.NeedCode(need), FavourMath.DueDay(need, now))) return false;
            Tell.Ask(letter, home);
            return true;
        }

        /// <summary>The existing mission (hands at the harvest, refugees) asked by this people. False when it can't be now.</summary>
        private static bool Mission(Faction f, FavourNeed need, Map home)
        {
            string name = need == FavourNeed.Harvest ? "Council_Mission_Harvest" : "Council_Mission_Refugees";
            var def = DefDatabase<IncidentDef>.GetNamedSilentFail(name);
            if (def == null) return false;
            MissionPeoples.Only = f;
            try
            {
                var parms = StorytellerUtility.DefaultParmsNow(def.category ?? IncidentCategoryDefOf.Misc, home);
                parms.forced = true;
                parms.faction = f;
                return def.Worker.CanFireNow(parms) && def.Worker.TryExecute(parms);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favours: their call through a mission", e);
                return false;
            }
            finally
            {
                MissionPeoples.Only = null;
            }
        }

        /// <summary>Their letter (or call, when both ends can): who writes, what for, what they need.</summary>
        private static Letter Letter(Tie t, Faction f, FavourNeed need, Map home)
        {
            var def = DiplomacyDefOf.Rimfolk_FavourAsk;
            if (def == null) return null;
            var head = FavourCalls.HeadOf(f, home);
            var form = DipWords.Form(home, f);
            var town = head != null ? NotableBook.TownOf(head.town) : WorldSituationWorker.NearestTown(f, home);
            bool call = NotableMath.Callable((int)f.def.techLevel) && NeighbourLetters.Console(home) != null;
            string who = head != null ? NotableBook.Called(head) : DipWords.People(f);
            string label = DipWords.T("CC_Dip_AskLabel", DipWords.People(f).Named("PEOPLE"));
            var parts = new List<string>
            {
                DipWords.T(call ? "CC_Dip_AskIntro_Call" : "CC_Dip_AskIntro", who.Named("WHO"), DipWords.People(f).Named("PEOPLE"), form.word.Named("WORD"),
                    form.rite.Named("RITE"), DipWords.Why(t).Named("WHY")),
                NeedText(f, need, town)
            };
            parts.Add(DipWords.P("CC_Dip_AskAnswer_" + (need == FavourNeed.Dispute ? "Word" : "Goods"), DipWords.People(f).Named("PEOPLE")));
            var letter = LetterMaker.MakeLetter(label, string.Join("\n\n", parts), def, town != null ? new LookTargets(town) : LookTargets.Invalid, f);
            if (letter is ChoiceLetter_FavourAsk l)
            {
                l.tie = t.id;
                l.need = (int)need;
                l.home = Homes.KeyOf(home);
            }
            return letter;
        }

        private static string NeedText(Faction f, FavourNeed need, Settlement town)
        {
            string place = town?.Name ?? DipWords.People(f);
            switch (need)
            {
                case FavourNeed.Medicine:
                    return DipWords.T("CC_Dip_AskNeed_Medicine", place.Named("TOWN"), Amount(need).Named("GOODS"));
                case FavourNeed.Food:
                    return DipWords.T("CC_Dip_AskNeed_Food", place.Named("TOWN"), Amount(need).Named("GOODS"));
                default:
                {
                    var other = OtherSide(f);
                    return other != null
                        ? DipWords.T("CC_Dip_AskNeed_Feud", DipWords.People(f).Named("PEOPLE"), DipWords.People(other).Named("OTHER"))
                        : DipWords.T("CC_Dip_AskNeed_Seat", DipWords.People(f).Named("PEOPLE"), place.Named("TOWN"));
                }
            }
        }

        /// <summary>What they ask for, by the errand's words: "8 medicine", "75 raw food".</summary>
        private static (string what, int count) Goods(FavourNeed need) =>
            need == FavourNeed.Medicine ? ("medicine", FavourMath.MedicineCount) : ("rawfood", FavourMath.FoodCount);

        private static string Amount(FavourNeed need)
        {
            var (what, count) = Goods(need);
            return Couriers.Amount(count, what, null);
        }

        // ================= The colony's answer =================

        /// <summary>Why the colony can't send it now (too little in the stores), or null.</summary>
        public static string CantSend(Map home, FavourNeed need)
        {
            if (home == null) return "";
            var (what, count) = Goods(need);
            return PlayCarry.Have(home, what, null) < count ? "CC_Dilemma_NotEnough".Translate(count, PlayCarry.Label(what, null)).ToString() : null;
        }

        /// <summary>Sent: carried out to their messenger at the gate (the errands' play-out, their courier waiting there); the debt kept.</summary>
        public static void Send(Tie t, Map home, FavourNeed need)
        {
            var f = t != null ? Refs.Faction(t.to) : null;
            if (t == null || f == null || home == null || t.state != Tie.State.CalledIn) return;
            var (what, count) = Goods(need);
            var ctx = new DilemmaContext { map = home, faction = f };
            // apart: one of the colony carries it out to the gate, where their messenger waits to take it home.
            new DilemmaEffect_CarryOut { what = what, count = count, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = DipWords.P("CC_Dip_CarryLabel", DipWords.People(f).Named("PEOPLE")) }
                .Apply(ctx);
            Kept(t, f);
        }

        /// <summary>The colony gives its word on their side: the speaker writes it to their head (a letter on its way), or, with nobody to carry it, it is said at once.</summary>
        public static void StandWith(Tie t, Map home)
        {
            var f = t != null ? Refs.Faction(t.to) : null;
            if (t == null || f == null || home == null || t.state != Tie.State.CalledIn) return;
            var head = FavourCalls.HeadOf(f, home);
            var writer = NeighbourLetters.DefaultWriter(home);
            NeighbourLetter sent = null;
            if (head != null && writer != null)
            {
                var topic = new LetterTopic { key = DiplomacyLetters.SideKey(t), text = DipWords.P("CC_Dip_Topic_Side", DipWords.People(f).Named("PEOPLE")) };
                var channel = NeighbourLetters.ChannelWhyNot(head, LetterChannel.Courier, home) == null ? LetterChannel.Courier : LetterChannel.Console;
                sent = NeighbourLetters.Send(head, LetterPurpose.Favour, channel, writer, topic, null);
            }
            if (sent != null)
            {
                // The letter has its walk there to make: the call waits for it.
                t.terms.days = Now / (float)FavourMath.DayTicks + FavourMath.DisputeDays + 20f;
                WorldLedger.Touch(t);
                return;
            }
            SideTaken(t, writer);
            // apart: the colony's word, given to their people when no letter could go; told in a message.
            Tell.Message(NewsKind.World, Importance.Notable, home, DipWords.P("CC_Dip_Msg_SideNow", DipWords.People(f).Named("PEOPLE")), LookTargets.Invalid,
                MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>
        /// The colony's word on their side has reached them: they remember it ("sidedWith", which keeps the debt), the other
        /// side of their feud remembers it too ("tookSides"), and the history keeps it.
        /// </summary>
        public static void SideTaken(Tie t, Pawn by)
        {
            var f = t != null ? Refs.Faction(t.to) : null;
            if (t == null || f == null) return;
            var home = FavoursOwed.HomeOf(t);
            var other = OtherSide(f);
            KinMemory.Note(f, "sidedWith", by, DipWords.Need(FavourNeed.Dispute));
            if (t.Live) Kept(t, f);
            if (other != null) FactionMemory.Note(other, "tookSides", DipWords.People(f));
            Diplomacy.Write(DiplomacyMath.Side, new List<HistoryActor>
            {
                new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.For)
            }.Concat(other != null ? new[] { new HistoryActor(Refs.Of(other), HistoryRole.Against) } : new HistoryActor[0]).ToList(), Diplomacy.Colony(home),
                DipWords.P("CC_Dip_Hist_Side", DipWords.People(f).Named("PEOPLE")));
        }

        private static void Kept(Tie t, Faction f)
        {
            if (t.Live) FavoursOwed.Close(t, Tie.State.Kept);
            FactionMemory.Note(f, "keptWord", DipWords.Why(t));
        }

        /// <summary>
        /// Refused (the colony's answer) or left unanswered past its days: the debt broken, the deed "refused", 5 goodwill,
        /// and a word to the player.
        /// </summary>
        public static void Refuse(Tie t, bool unanswered)
        {
            var f = t != null ? Refs.Faction(t.to) : null;
            if (t == null || f == null || !t.Live) return;
            var home = FavoursOwed.HomeOf(t);
            FavoursOwed.Close(t, Tie.State.Broken);
            FactionMemory.Note(f, "refused", DipWords.Why(t));
            WorldSituationWorker.Goodwill(f, FavourMath.RefusedGoodwill, false);
            var form = DipWords.Form(home, f);
            // apart: their call answered (or not) from afar; told in a message.
            Tell.Message(NewsKind.World, Importance.Notable, home,
                DipWords.T(unanswered ? "CC_Dip_Msg_Unanswered" : "CC_Dip_Msg_Refused", DipWords.People(f).Named("PEOPLE"), form.word.Named("WORD")),
                LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
        }

        // ================= Answered on the map, or not at all =================

        /// <summary>
        /// A deed the memory noted answers a call of theirs (hands at the harvest, refugees taken in, the colony's word on
        /// their side): the debt is kept, and the deed makes no new debt of theirs. True when it answered one.
        /// </summary>
        public static bool Answered(FactionDeed d)
        {
            if (d?.faction == null || d.side != DeedSide.Favor) return false;
            var r = Refs.Of(d.faction);
            foreach (var t in FavoursOwed.Live())
            {
                if (t.state != Tie.State.CalledIn || !FavoursOwed.ColonyOwes(t) || t.to != r) continue;
                if (!FavourMath.ParseNeed(t.terms.goods, out var need) || !FavourMath.Answers(need, d.kind)) continue;
                FavoursOwed.Close(t, Tie.State.Kept);
                FactionMemory.Note(d.faction, "keptWord", DipWords.Why(t));
                return true;
            }
            return false;
        }

        /// <summary>
        /// A mission ended (Missions.Finish): one they asked by calling in a debt and the colony didn't see through breaks it;
        /// one called off (their town gone) leaves it open again.
        /// </summary>
        public static void MissionEnded(Mission m, MissionState state)
        {
            if (m?.faction == null || state == MissionState.Done) return;
            var r = Refs.Of(m.faction);
            foreach (var t in FavoursOwed.Live().ToList())
            {
                if (t.state != Tie.State.CalledIn || !FavoursOwed.ColonyOwes(t) || t.to != r) continue;
                if (!FavourMath.ParseNeed(t.terms.goods, out var need) || FavourMath.MissionKind(need) != m.kind) continue;
                if (m.outcome == "cancelled" || m.outcome == "ended") FavoursOwed.Close(t, Tie.State.Open);
                else Refuse(t, false);
            }
        }

        /// <summary>Calls past their days, unanswered: broken.</summary>
        public static void Hourly(int now)
        {
            List<Tie> due = null;
            foreach (var t in FavoursOwed.Live())
                if (t.state == Tie.State.CalledIn && FavoursOwed.ColonyOwes(t) && FavourMath.Overdue(t.terms.days, now)
                    && !(FavourMath.ParseNeed(t.terms.goods, out var need) && need == FavourNeed.Fighters))
                    (due ??= new List<Tie>()).Add(t);
            if (due == null) return;
            foreach (var t in due) Refuse(t, true);
        }

        /// <summary>Their calls waiting for the colony's answer, for This week and the pages.</summary>
        public static List<Tie> Waiting()
        {
            var list = new List<Tie>();
            foreach (var t in FavoursOwed.Live())
                if (t.state == Tie.State.CalledIn && FavoursOwed.ColonyOwes(t))
                    list.Add(t);
            return list;
        }

        /// <summary>Their letter for this call (asking for goods or the colony's word, or for fighters), still in the letter stack, or null.</summary>
        public static Letter AnyLetterFor(Tie t)
        {
            if (t == null) return null;
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_FavourAsk a && a.tie == t.id || l is ChoiceLetter_CallToArms c && c.tie == t.id)
                    return l;
            return null;
        }

        /// <summary>Their letter for this call, still in the letter stack, or null.</summary>
        public static ChoiceLetter_FavourAsk LetterFor(Tie t)
        {
            if (t == null) return null;
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_FavourAsk a && a.tie == t.id)
                    return a;
            return null;
        }
    }

    /// <summary>
    /// Their call on a debt of the colony's, as a letter (see <see cref="FavourAsks"/>): send what they ask (carried out to
    /// their messenger), give the colony's word on their side (a letter to their head), or refuse. Once the call has moved on
    /// (answered another way, broken, their people at war with the colony), only closing is left.
    /// </summary>
    public class ChoiceLetter_FavourAsk : ChoiceLetter
    {
        public int tie = -1;
        public int need = -1;
        public int home = HomesMath.None;

        private Tie Live(out Map map)
        {
            map = Homes.MapOf(home) ?? Homes.Seat;
            var t = FavoursOwed.Get(tie);
            if (t == null || t.state != Tie.State.CalledIn || !FavoursOwed.ColonyOwes(t)) return null;
            if (!FavourMath.ParseNeed(t.terms.goods, out var n) || (int)n != need) return null;
            var f = Refs.Faction(t.to);
            return f == null || Diplomacy.Hostile(f) ? null : t;
        }

        private DiaOption Option(string key, Action<Map, Tie> act, string disabled = null, NamedArgument? arg = null)
        {
            var o = new DiaOption((arg.HasValue ? key.Translate(arg.Value) : key.Translate()).Resolve())
            {
                action = () =>
                {
                    var t = Live(out var map);
                    Find.LetterStack.RemoveLetter(this);
                    if (t != null) act(map, t);
                },
                resolveTree = true
            };
            if (disabled != null) o.Disable(disabled);
            return o;
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                Map map = null;
                var t = ArchivedOnly ? null : Live(out map);
                if (t == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                var n = (FavourNeed)Math.Max(0, need);
                if (n == FavourNeed.Dispute)
                    yield return Option("CC_Dip_AskOpt_Word", (m, x) => FavourAsks.StandWith(x, m), null, DipWords.People(Refs.Faction(t.to)).Named("PEOPLE"));
                else
                    yield return Option("CC_Dip_AskOpt_Send", (m, x) => FavourAsks.Send(x, m, n), FavourAsks.CantSend(map, n), DipWords.Need(n).Named("NEED"));
                yield return Option("CC_Dip_AskOpt_Refuse", (m, x) => FavourAsks.Refuse(x, false));
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tie, "tie", -1);
            Scribe_Values.Look(ref need, "need", -1);
            Scribe_Values.Look(ref home, "home", HomesMath.None);
        }
    }

    [DefOf]
    public static class DiplomacyDefOf
    {
        public static LetterDef Rimfolk_FavourAsk;

        static DiplomacyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DiplomacyDefOf));
        }
    }
}
