using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>
    /// Ransom both ways (docs/design/stage2-diplomacy.md, round a, item 3; release 2a.3; decisions 4 and 5; the rules in
    /// RansomMath.cs). Their kin in the colony's cells: two to six days after one of a people is taken, if their people want
    /// them back, their messenger walks to the gate (a go-between of a people at peace with both when theirs is at war with
    /// the colony), or their call comes in at the console, with an offer by their ways; the answers are the letter's:
    /// accept (the warden walks the captive out by the game's own release while they set the price down, or their pod
    /// brings it after a call), ask for more (the speaker's Social), let them go for nothing (the deed "forgave", and their
    /// debt of honour), keep them (once more within a season; three times, a grudge). The colony may write first, which
    /// brings it sooner. Ours in their hands: the game's own demand where there is no comms console, by their messenger,
    /// at the game's rate and fee, with its day to answer (RansomHooks.cs); paid, the silver is carried out to them, and
    /// the colonist walks in from the edge (or comes by pod from a people of the machine ages) some hours later. An
    /// exchange of theirs for ours: their party brings the colony's own to the gate, the warden walks theirs out, both at
    /// once. With a hostile people an exchange at the gate may be a trap, at the peace talks' rate by the negotiator, named
    /// in the letter before the choice: a band of theirs falls on the gate from its side. The records are each home's
    /// (DiplomacyBook), six at most, dropped once done or the captive is gone.
    /// </summary>
    public static class Ransoms
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>The setting "Ransom": off, nothing new comes, and what is under way plays out.</summary>
        public static bool On => ColonyCouncilMod.Settings?.diplomacyRansom ?? true;

        // ================= Reading =================

        /// <summary>Every offer at every home of the colony's, with its home (a new list).</summary>
        public static List<(DiplomacyHome home, RansomOffer offer)> All()
        {
            var list = new List<(DiplomacyHome, RansomOffer)>();
            var book = DiplomacyBook.Instance;
            if (book == null) return list;
            foreach (var h in book.Live())
                foreach (var o in h.offers)
                    if (o != null)
                        list.Add((h, o));
            return list;
        }

        public static RansomOffer Get(int id, out DiplomacyHome home)
        {
            home = null;
            foreach (var (h, o) in All())
                if (o.id == id)
                {
                    home = h;
                    return o;
                }
            return null;
        }

        /// <summary>The offer naming this captive, either way, or null.</summary>
        public static RansomOffer For(Pawn p, out DiplomacyHome home)
        {
            home = null;
            if (p == null) return null;
            foreach (var (h, o) in All())
                if (o.Has(p))
                {
                    home = h;
                    return o;
                }
            return null;
        }

        public static RansomOffer For(Pawn p) => For(p, out _);

        /// <summary>A people the colony can deal with about captives: real, standing, one that makes peace.</summary>
        public static bool Deals(Faction f) => f != null && Diplomacy.Deals(f) && f.HasGoodwill && f.def != null && !f.def.permanentEnemy;

        /// <summary>Their grudge for refused offers (a Grudge tie of origin "ransom"), or null.</summary>
        public static Tie Grudge(Faction f)
        {
            if (f == null) return null;
            var people = Refs.Of(f);
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Grudge))
                if (t != null && t.Live && t.origin == RansomMath.Origin && t.from == people)
                    return t;
            return null;
        }

        /// <summary>One of their notables (or their leader), or kin by blood of either.</summary>
        public static bool Notable(Pawn p, Faction f, out bool leader, out bool kin)
        {
            leader = f != null && f.leader == p;
            kin = false;
            var book = NotableBook.Instance;
            bool notable = book != null && book.notables.Any(n => n != null && n.pawn == p);
            if (p?.relations != null && f != null)
            {
                if (f.leader != null && f.leader != p && p.relations.FamilyByBlood.Contains(f.leader)) kin = true;
                else if (book != null && book.notables.Any(n => n != null && n.faction == f && n.Alive && n.pawn != p && p.relations.FamilyByBlood.Contains(n.pawn))) kin = true;
            }
            return notable;
        }

        /// <summary>Their people want this captive back (a steady roll: the same answer at every look).</summary>
        public static bool Wanted(Pawn p, Faction f, bool asked)
        {
            if (p == null || f == null) return false;
            bool notable = Notable(p, f, out bool leader, out bool kin);
            return RansomMath.WantBack(leader, notable, kin, Diplomacy.Hostile(f), asked, RansomMath.SteadyRoll(p.thingIDNumber, f.loadID));
        }

        /// <summary>Who carries their word: their own people at peace with the colony, else a go-between of a people at peace with both, else nobody.</summary>
        public static Faction Bearer(Faction f)
        {
            if (f == null) return null;
            if (!Diplomacy.Hostile(f)) return Arrivals.PersonKind(f) != null ? f : null;
            var escort = TruceParley.Escort(f);
            return escort != null && Arrivals.PersonKind(escort) != null ? escort : null;
        }

        /// <summary>Their offer comes by a call: the colony has a working comms console and their people call by machine.</summary>
        public static bool ByCall(Faction f, Map home) => f?.def != null && NotableMath.Callable((int)f.def.techLevel) && NeighbourLetters.Console(home) != null;

        /// <summary>The colony's negotiator: its speaker, else whoever is best with people at home.</summary>
        public static Pawn Negotiator(Map home) => TruceParley.Speaker(home);

        /// <summary>The trap's chance for an exchange with this people now (by the colony's negotiator).</summary>
        public static float TrapChance(Faction f, Map home)
        {
            if (!Diplomacy.Hostile(f)) return 0f;
            var n = Negotiator(home);
            float ability = 0f;
            try
            {
                ability = n != null ? n.GetStatValue(StatDefOf.NegotiationAbility) : 0f;
            }
            catch
            {
            }
            return RansomMath.TrapChance(true, ability);
        }

        // ================= The hour =================

        /// <summary>The hour (Diplomacy.Hourly): new captives noticed, and each offer a step on.</summary>
        public static void Hourly(int now)
        {
            var book = DiplomacyBook.Instance;
            if (book == null) return;
            if (On)
                foreach (var map in Homes.All)
                    if (map?.mapPawns != null && map.mapPawns.PrisonersOfColonySpawnedCount > 0)
                        Notice(book, map, now);
            foreach (var (h, o) in All())
            {
                try
                {
                    Step(h, o, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("ransom: an offer's hour", e);
                }
            }
        }

        /// <summary>One of a people the colony deals with in its cells, not yet with an offer: wanted back, their offer is due in two to six days.</summary>
        private static void Notice(DiplomacyBook book, Map map, int now)
        {
            DiplomacyHome h = null;
            foreach (var p in map.mapPawns.PrisonersOfColonySpawned.ToList())
            {
                var f = p?.Faction;
                if (f == null || p.Dead || !p.RaceProps.Humanlike || p.DevelopmentalStage.Baby() || !Deals(f) || Grudge(f) != null) continue;
                h ??= book.HomeOf(map);
                if (h.offers.Count >= RansomMath.MaxOffers) return;
                if (h.offers.Any(o => o.captive == p) || !Wanted(p, f, false)) continue;
                // Nobody could carry their word (at war, no people at peace with both, no call): they don't try.
                if (Bearer(f) == null && !ByCall(f, map)) continue;
                float delay = RansomMath.DelayDays(Rand.Value);
                Add(book, h, RansomKind.Theirs, p, null, f, now + Mathf.RoundToInt(delay * RansomMath.DayTicks));
            }
        }

        private static RansomOffer Add(DiplomacyBook book, DiplomacyHome h, RansomKind kind, Pawn captive, Pawn ours, Faction f, int due)
        {
            var o = new RansomOffer
            {
                id = book.NextId(), kind = kind, stage = RansomStage.Waiting, captive = captive, ours = ours, people = f, due = due, created = Now,
                captiveName = captive?.LabelShort, oursName = ours?.LabelShort
            };
            if (kind == RansomKind.Theirs && captive != null)
            {
                bool notable = Notable(captive, f, out bool leader, out _);
                o.price = RansomMath.Price(captive.MarketValue, leader, notable, Rand.Value);
                if (RansomMath.InGoods((int)f.def.techLevel)) o.goods = TribeGoods(Rand.Value)?.defName;
            }
            h.offers.Add(o);
            Diplomacy.Dirty();
            return o;
        }

        /// <summary>What a tribe pays in: pemmican or healing herbs.</summary>
        private static ThingDef TribeGoods(float roll) =>
            roll < 0.5f ? DefDatabase<ThingDef>.GetNamedSilentFail("Pemmican") ?? ThingDefOf.MedicineHerbal : ThingDefOf.MedicineHerbal;

        /// <summary>An offer one step on: its captive gone, their messenger setting out, waiting at the gate, the price paid, ours walking in.</summary>
        private static void Step(DiplomacyHome h, RansomOffer o, int now)
        {
            var map = h.Map;
            if (map == null || o.people == null || o.people.defeated)
            {
                Drop(h, o, null);
                return;
            }
            if (!StillThere(o))
            {
                Gone(h, o, map);
                return;
            }
            switch (o.stage)
            {
                case RansomStage.Waiting:
                    if (now < o.due) return;
                    if (!On && o.kind == RansomKind.Theirs && !o.asked)
                    {
                        Drop(h, o, null);
                        return;
                    }
                    if (now - o.due > RansomMath.GiveUpDays * RansomMath.DayTicks)
                    {
                        Drop(h, o, o.asked ? RansomWords.P("CC_Ransom_Msg_NoWord", RansomWords.People(o.people).Named("PEOPLE")) : null);
                        return;
                    }
                    if (MapFacts.ThreatOn(map)) return;
                    Come(h, o, map);
                    return;
                case RansomStage.Coming:
                    // Their messenger is gone from the map without reaching the gate (turned back, hurt): they try again another day.
                    if (LordOf(o, map) == null)
                    {
                        // An exchange whose party turned back after the colony's own walked in with them: the colony keeps its side.
                        if (o.kind == RansomKind.Exchange && o.ours != null && o.ours.Spawned)
                        {
                            Exchange(h, o, map, null);
                            Diplomacy.Dirty();
                            return;
                        }
                        o.stage = RansomStage.Waiting;
                        o.due = now + RansomMath.DayTicks;
                        Diplomacy.Dirty();
                    }
                    return;
                case RansomStage.AtGate:
                    if (o.answerBy >= 0 && now > o.answerBy) Unanswered(h, o, map);
                    return;
                case RansomStage.Paying:
                    Paying(h, o, map, now);
                    return;
                case RansomStage.Returning:
                    if (now >= o.due) WalkIn(h, o, map);
                    return;
            }
        }

        /// <summary>Whoever the offer is about is still where it needs them: theirs in the colony's cells, ours in their hands.</summary>
        private static bool StillThere(RansomOffer o)
        {
            bool theirs = o.captive != null && !o.captive.Dead && !o.captive.Discarded && o.captive.IsPrisonerOfColony;
            bool ours = o.ours != null && !o.ours.Dead && !o.ours.Discarded && (o.people?.kidnapped?.KidnappedPawnsListForReading.Contains(o.ours) ?? false);
            switch (o.kind)
            {
                case RansomKind.Theirs:
                    // Paid for: walking out (released) is where it should be.
                    return o.stage == RansomStage.Paying || theirs;
                case RansomKind.Demand:
                    return o.stage == RansomStage.Returning || ours;
                default:
                    // Once their party walks in, the colony's own walks beside them (no longer theirs to hold).
                    return o.stage == RansomStage.Paying || theirs && (ours || o.stage != RansomStage.Waiting);
            }
        }

        /// <summary>The captive is gone (dead, freed by other means, joined, carried off): the offer ends, their messenger goes home.</summary>
        private static void Gone(DiplomacyHome h, RansomOffer o, Map map)
        {
            string name = o.kind == RansomKind.Demand ? RansomWords.Name(o.ours, o.oursName) : RansomWords.Name(o.captive, o.captiveName);
            string msg = o.stage >= RansomStage.Coming ? RansomWords.P("CC_Ransom_Msg_Gone", name.Named("NAME"), RansomWords.People(o.people).Named("PEOPLE")) : null;
            Drop(h, o, msg);
        }

        /// <summary>The offer is done with: its letter taken back, their messenger sent home, a message when one is given.</summary>
        public static void Drop(DiplomacyHome h, RansomOffer o, string message)
        {
            if (h == null || o == null) return;
            h.offers.Remove(o);
            var map = h.Map;
            LordOf(o, map)?.ReceiveMemo(LordJob_Messenger.MemoLeave);
            foreach (var l in Find.LetterStack.LettersListForReading.ToList())
                if (l is ChoiceLetter_RansomOffer r && r.offer == o.id || l is ChoiceLetter_RansomByHand b && b.offer == o.id)
                    Find.LetterStack.RemoveLetter(l);
            Diplomacy.Dirty();
            if (!message.NullOrEmpty())
                // apart: what became of an offer for a captive, told in a message.
                Tell.Message(NewsKind.World, Importance.Notable, map, message, LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        // ================= Their messenger =================

        /// <summary>The messenger's group of this offer on the home, or null.</summary>
        public static Lord LordOf(RansomOffer o, Map map)
        {
            if (o == null || map?.lordManager == null) return null;
            foreach (var l in map.lordManager.lords)
                if (l?.LordJob is LordJob_Messenger m && m.offer == o.id)
                    return l;
            return null;
        }

        public static Pawn Messenger(Lord l) => l?.ownedPawns.FirstOrDefault(p => p != null && p.Spawned && !p.Dead && !p.Downed);

        /// <summary>
        /// Their word comes: by a call at the console (the letter at once; an offer of theirs only), else their messenger (or
        /// a go-between) walks in to the gate, with the colony's own beside them for an exchange. Nobody can carry it: it
        /// waits a day.
        /// </summary>
        private static void Come(DiplomacyHome h, RansomOffer o, Map map)
        {
            var f = o.people;
            if (o.kind == RansomKind.Theirs && ByCall(f, map))
            {
                o.call = true;
                o.stage = RansomStage.AtGate;
                o.answerBy = Now + Mathf.RoundToInt(RansomMath.WaitHours * RansomMath.HourTicks);
                Diplomacy.Dirty();
                OfferLetter(h, o, map, null);
                return;
            }
            var by = Bearer(f);
            if (by == null || !Arrivals.TryEntryCell(map, out var entry))
            {
                o.due = Now + RansomMath.DayTicks;
                return;
            }
            var m = SendMessenger(o.id, by, map, entry, RansomMath.WaitHours);
            if (m == null)
            {
                o.due = Now + RansomMath.DayTicks;
                return;
            }
            o.stage = RansomStage.Coming;
            Diplomacy.Dirty();
            string form = RansomWords.Form(map, f).rite;
            if (o.kind == RansomKind.Exchange)
            {
                // The colony's own walks in beside them, held until the gate.
                if (!WalkWith(o, map, entry)) return;
                // apart: their party walks in toward the gate; the message looks at them.
                Tell.Message(NewsKind.World, Importance.Important, map, RansomWords.P("CC_Ransom_Msg_ExchangeComes", RansomWords.People(f).Named("PEOPLE"),
                    RansomWords.Name(o.ours, o.oursName).Named("OURS")), new LookTargets(m), MessageTypeDefOf.NeutralEvent);
                return;
            }
            string key = o.kind == RansomKind.Demand ? "CC_Ransom_Msg_DemandComes" : by == f ? "CC_Ransom_Msg_Comes" : "CC_Ransom_Msg_GoBetween";
            // apart: their messenger walks in toward the gate; the message looks at them.
            Tell.Message(NewsKind.World, Importance.Notable, map, RansomWords.P(key, RansomWords.People(f).Named("PEOPLE"), RansomWords.People(by).Named("ESCORT"),
                form.Named("RITE")), new LookTargets(m), MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>A messenger of <paramref name="by"/> walks in from <paramref name="entry"/> to the gate, waiting there so many hours. Returns them, or null.</summary>
        public static Pawn SendMessenger(int offer, Faction by, Map map, IntVec3 entry, float waitHours, int envoyHome = HomesMath.None)
        {
            var kind = Arrivals.PersonKind(by);
            var gate = DropCellFinder.TradeDropSpot(map);
            if (kind == null || !gate.IsValid) return null;
            Pawn p = null;
            try
            {
                p = Arrivals.Make(kind, by);
                GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, map, 3), map);
                LordMaker.MakeNewLord(by, new LordJob_Messenger(offer, envoyHome, gate, waitHours), map, new List<Pawn> { p });
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom: a messenger sent", e);
                if (p != null && p.Spawned) p.DeSpawn();
                Arrivals.Discard(p);
                return null;
            }
        }

        /// <summary>
        /// Their messenger reached the gate (LordJob_Messenger): an offer of theirs and a demand ask the colony (their letter,
        /// a day to answer); a price agreed by a call is set down; an exchange is made at once.
        /// </summary>
        public static void Arrived(LordJob_Messenger job)
        {
            var o = Get(job.offer, out var h);
            var map = job.lord?.Map;
            if (o == null || map == null)
            {
                job.done = true;
                return;
            }
            var m = Messenger(job.lord);
            try
            {
                switch (o.kind)
                {
                    case RansomKind.Theirs:
                        if (o.stage == RansomStage.Paying)
                        {
                            SetDown(o, map, m);
                            job.done = true;
                            return;
                        }
                        o.stage = RansomStage.AtGate;
                        o.answerBy = Now + Mathf.RoundToInt(RansomMath.WaitHours * RansomMath.HourTicks);
                        OfferLetter(h, o, map, m);
                        break;
                    case RansomKind.Demand:
                        o.stage = RansomStage.AtGate;
                        o.answerBy = Now + RansomMath.AnswerTicks;
                        DemandLetter(h, o, map, m);
                        break;
                    case RansomKind.Exchange:
                        Exchange(h, o, map, m);
                        job.done = true;
                        break;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom: their messenger at the gate", e);
            }
            Diplomacy.Dirty();
        }

        /// <summary>Their messenger was hurt by the colony on the way or at the gate: they run, and the offer ends.</summary>
        public static void Harmed(LordJob_Messenger job)
        {
            var o = Get(job.offer, out var h);
            if (o == null) return;
            var f = o.people;
            if (f != null && job.lord?.faction != f) FactionMemory.Note(job.lord?.faction, "attacked", RansomWords.People(f));
            Drop(h, o, RansomWords.P("CC_Ransom_Msg_Harmed", RansomWords.People(f).Named("PEOPLE")));
        }

        // ================= Their offer for one of theirs =================

        /// <summary>Their offer, as a letter: accept, ask for more, let them go for nothing, keep them; the trap named when they are at war with the colony.</summary>
        private static void OfferLetter(DiplomacyHome h, RansomOffer o, Map map, Pawn messenger)
        {
            var def = RansomDefOf.Rimfolk_RansomOffer;
            var f = o.people;
            if (def == null || f == null) return;
            var form = RansomWords.Form(map, f);
            string captive = RansomWords.Name(o.captive, o.captiveName);
            string price = RansomWords.Price(o.price, o.goods);
            var by = messenger?.Faction;
            string open = o.call
                ? RansomWords.T("CC_Ransom_Offer_Call", RansomWords.People(f).Named("PEOPLE"), captive.Named("CAPTIVE"), price.Named("PRICE"), form.word.Named("WORD"))
                : by != null && by != f
                    ? RansomWords.T("CC_Ransom_Offer_GoBetween", (messenger?.LabelShort ?? "?").Named("WHO"), RansomWords.People(by).Named("ESCORT"),
                        RansomWords.People(f).Named("PEOPLE"), captive.Named("CAPTIVE"), price.Named("PRICE"), form.rite.Named("RITE"))
                    : RansomWords.T("CC_Ransom_Offer", (messenger?.LabelShort ?? "?").Named("WHO"), RansomWords.People(f).Named("PEOPLE"), captive.Named("CAPTIVE"),
                        price.Named("PRICE"), form.rite.Named("RITE"));
            var speaker = Negotiator(map);
            string text = open + "\n\n" + RansomWords.P(o.call ? "CC_Ransom_Offer_HowCall" : "CC_Ransom_Offer_How", captive.Named("CAPTIVE"),
                (speaker?.LabelShort ?? RansomWords.P("CC_Ransom_TheSpeaker")).Named("SPEAKER"), RansomWords.People(f).Named("PEOPLE"));
            if (o.refusals > 0) text += "\n\n" + RansomWords.P("CC_Ransom_Offer_Again", o.refusals.Named("COUNT"), RansomMath.MaxRefusals.Named("MAX"));
            if (Diplomacy.Hostile(f))
                text += "\n\n" + RansomWords.P("CC_Ransom_Trap_Named", RansomWords.People(f).Named("PEOPLE"), RansomWords.Odds(TrapChance(f, map)).Named("ODDS"),
                    (speaker?.LabelShort ?? RansomWords.P("CC_Ransom_TheSpeaker")).Named("SPEAKER"));
            var letter = LetterMaker.MakeLetter(RansomWords.P("CC_Ransom_Offer_Label", captive.Named("CAPTIVE")), text, def,
                messenger != null ? new LookTargets(messenger) : o.captive != null && o.captive.Spawned ? new LookTargets(o.captive) : LookTargets.Invalid, f);
            if (letter is ChoiceLetter_RansomOffer l)
            {
                l.offer = o.id;
                l.StartTimeout(Mathf.Max(RansomMath.HourTicks, o.answerBy - Now));
            }
            Tell.Ask(letter, map);
        }

        /// <summary>
        /// Accepted (at their price or a raised one): the warden walks the captive out by the game's own release, the price is
        /// set down at the gate (by their messenger, or their pod after a call), and with a hostile people the trap may spring.
        /// </summary>
        public static void Accept(int id, int price)
        {
            var o = Get(id, out var h);
            var map = h?.Map;
            if (o == null || map == null || o.stage != RansomStage.AtGate || o.captive == null || !o.captive.IsPrisonerOfColony) return;
            var f = o.people;
            o.price = price;
            o.stage = RansomStage.Paying;
            o.answerBy = Now + 10 * RansomMath.HourTicks;
            Release(o.captive);
            var lord = LordOf(o, map);
            var m = Messenger(lord);
            string how;
            if (m != null)
            {
                SetDown(o, map, m);
                lord.ReceiveMemo(LordJob_Messenger.MemoLeave);
                how = RansomWords.P("CC_Ransom_Paid_Gate", RansomWords.Price(o.price, o.goods).Named("PRICE"));
            }
            else
            {
                // After a call: their pod brings it to where traders leave their goods, as the game's own ransom comes.
                Pod(o, map);
                how = RansomWords.P("CC_Ransom_Paid_Pod", RansomWords.Price(o.price, o.goods).Named("PRICE"));
            }
            FactionMemory.Note(f, "ransomed", RansomWords.Name(o.captive, o.captiveName));
            var form = RansomWords.Form(map, f);
            Diplomacy.Write(DiplomacyMath.Ransomed, new List<HistoryActor>
            {
                new HistoryActor(WorldLedger.PersonOf(o.captive), HistoryRole.About), new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(Diplomacy.Colony(map), HistoryRole.With)
            }, Diplomacy.Colony(map), RansomWords.P("CC_Ransom_Hist_Paid", RansomWords.People(f).Named("PEOPLE"), RansomWords.Name(o.captive, o.captiveName).Named("CAPTIVE"),
                RansomWords.Price(o.price, o.goods).Named("PRICE"), form.word.Named("WORD")));
            // together: told as the warden sets out with the captive; what follows plays out at the gate.
            Tell.Message(NewsKind.World, Importance.Important, map, RansomWords.P("CC_Ransom_Msg_Accepted", RansomWords.Name(o.captive, o.captiveName).Named("CAPTIVE"),
                RansomWords.People(f).Named("PEOPLE"), how.Named("HOW")), new LookTargets(o.captive), MessageTypeDefOf.PositiveEvent);
            Trap(o, map);
            Diplomacy.Dirty();
        }

        /// <summary>
        /// Asking for more: the speaker's Social against their patience. Agreed: accepted at a quarter more. Not: their
        /// messenger leaves, and it counts as refused.
        /// </summary>
        public static void AskMore(int id)
        {
            var o = Get(id, out var h);
            var map = h?.Map;
            if (o == null || map == null || o.stage != RansomStage.AtGate) return;
            var speaker = Negotiator(map);
            int social = speaker?.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0;
            // Haggling over a captive whose family waits for them goes against a warm heart: the one who haggles, and the
            // captive's own close kin among the colony's here to hear it (Nature.cs).
            Nature.Felt(speaker, NatureAct.HaggledForKin, o.people);
            foreach (var kin in Nature.CloseKinOf(o.captive, map))
                if (kin != speaker)
                    Nature.Felt(kin, NatureAct.HaggledForKin, o.people, RansomWords.Name(o.captive, o.captiveName), "Kin");
            if (RansomMath.MoreAgreed(social, Rand.Value))
            {
                int more = RansomMath.Raised(o.price);
                // apart: the bargaining was the letter's answer; what was agreed is told before it plays out.
                Tell.Message(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Ransom_Msg_MoreYes", (speaker?.LabelShort ?? "?").Named("SPEAKER"),
                    RansomWords.People(o.people).Named("PEOPLE"), RansomWords.Price(more, o.goods).Named("PRICE")), LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
                Accept(id, more);
                return;
            }
            Refused(h, o, map, RansomWords.P("CC_Ransom_Msg_MoreNo", (speaker?.LabelShort ?? "?").Named("SPEAKER"), RansomWords.People(o.people).Named("PEOPLE")));
        }

        /// <summary>Let go for nothing: the game's release, the deed "forgave" (their debt of honour), and their messenger goes home with thanks.</summary>
        public static void Free(int id)
        {
            var o = Get(id, out var h);
            var map = h?.Map;
            if (o == null || map == null || o.captive == null || !o.captive.IsPrisonerOfColony) return;
            var f = o.people;
            Release(o.captive);
            string name = RansomWords.Name(o.captive, o.captiveName);
            FactionMemory.Note(f, "forgave", name);
            Diplomacy.Write(DiplomacyMath.Ransomed, new List<HistoryActor>
            {
                new HistoryActor(WorldLedger.PersonOf(o.captive), HistoryRole.About), new HistoryActor(Diplomacy.Colony(map), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.For)
            }, Diplomacy.Colony(map), RansomWords.P("CC_Ransom_Hist_Freed", RansomWords.People(f).Named("PEOPLE"), name.Named("CAPTIVE")));
            var captive = o.captive;
            Drop(h, o, null);
            // apart: the colony's answer to their offer; the warden walks the captive out on the map.
            Tell.Message(NewsKind.World, Importance.Important, map, RansomWords.P("CC_Ransom_Msg_Freed", name.Named("CAPTIVE"), RansomWords.People(f).Named("PEOPLE")),
                new LookTargets(captive), MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>Kept: their messenger goes home; they may ask once more within a season; refused three times, a grudge.</summary>
        public static void Keep(int id)
        {
            var o = Get(id, out var h);
            if (o == null || h?.Map == null) return;
            Refused(h, o, h.Map, RansomWords.P("CC_Ransom_Msg_Kept", RansomWords.People(o.people).Named("PEOPLE"), RansomWords.Name(o.captive, o.captiveName).Named("CAPTIVE")));
        }

        /// <summary>Nobody answered in their day: their messenger goes home, as refused; a demand is let go, as the game's.</summary>
        private static void Unanswered(DiplomacyHome h, RansomOffer o, Map map)
        {
            if (o.kind == RansomKind.Demand)
            {
                Drop(h, o, RansomWords.P("CC_Ransom_Msg_DemandLapsed", RansomWords.People(o.people).Named("PEOPLE"), RansomWords.Name(o.ours, o.oursName).Named("OURS")));
                return;
            }
            Refused(h, o, map, RansomWords.P("CC_Ransom_Msg_Unanswered", RansomWords.People(o.people).Named("PEOPLE")));
        }

        private static void Refused(DiplomacyHome h, RansomOffer o, Map map, string message)
        {
            var f = o.people;
            o.refusals++;
            LordOf(o, map)?.ReceiveMemo(LordJob_Messenger.MemoLeave);
            foreach (var l in Find.LetterStack.LettersListForReading.ToList())
                if (l is ChoiceLetter_RansomOffer r && r.offer == o.id)
                    Find.LetterStack.RemoveLetter(l);
            if (RansomMath.Grudge(o.refusals))
            {
                Drop(h, o, null);
                var t = WorldLedger.AddTie(Tie.Kind.Grudge, Refs.Of(f), Diplomacy.Colony(map),
                    new TieTerms { key = RansomMath.GrudgeKey, count = o.refusals, about = o.captive != null ? WorldLedger.PersonOf(o.captive) : Ref.None },
                    Now + Mathf.RoundToInt(RansomMath.GrudgeDays * RansomMath.DayTicks), RansomMath.Origin);
                FactionMemory.Note(f, "slighted", RansomWords.Name(o.captive, o.captiveName));
                Tell.Letter(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Ransom_Grudge_Label", RansomWords.People(f).Named("PEOPLE")),
                    RansomWords.T("CC_Ransom_Grudge", RansomWords.People(f).Named("PEOPLE"), RansomWords.Name(o.captive, o.captiveName).Named("CAPTIVE"),
                        o.refusals.Named("COUNT")), LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
                Diplomacy.Dirty();
                return;
            }
            o.stage = RansomStage.Waiting;
            o.due = Now + Mathf.RoundToInt(RansomMath.AgainDays(Rand.Value) * RansomMath.DayTicks);
            o.answerBy = -1;
            o.call = false;
            Diplomacy.Dirty();
            if (!message.NullOrEmpty())
                // apart: the colony's answer to their offer, told as their messenger turns for home.
                Tell.Message(NewsKind.World, Importance.Notable, map, message, LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The captive walks out by the game's own release: a warden takes them to the edge of the colony's land.</summary>
        private static void Release(Pawn p)
        {
            try
            {
                if (p?.guest != null && p.IsPrisonerOfColony) p.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.Release);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom: the release", e);
            }
        }

        /// <summary>Paid for, or exchanged: once the captive is walked out the offer is done; nobody walked them out in ten hours, the door is opened for them.</summary>
        private static void Paying(DiplomacyHome h, RansomOffer o, Map map, int now)
        {
            if (o.kind == RansomKind.Demand)
            {
                // The silver never reached them (the speaker called away, their messenger gone): they take it from the stores.
                if (now > o.answerBy || LordOf(o, map) == null) Paid(o.id, null, 0);
                return;
            }
            var c = o.captive;
            bool out_ = c == null || c.Dead || !c.Spawned || c.Map != map || c.guest == null || c.guest.Released || !c.IsPrisonerOfColony;
            if (!out_ && now > o.answerBy)
            {
                try
                {
                    GenGuest.PrisonerRelease(c);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("ransom: the door opened", e);
                }
                out_ = true;
            }
            if (!out_) return;
            // Kept until they are off the map, so the game's goodwill for a captive leaving healthy isn't taken for a debt of honour.
            if (c != null && !c.Dead && c.Spawned && c.Map == map && now <= o.answerBy + RansomMath.DayTicks) return;
            Drop(h, o, null);
        }

        /// <summary>A captive paid for, or given in an exchange, is leaving: their going earns no debt of honour (FavoursOwed reads it).</summary>
        public static bool PaidFor(Faction f, string name)
        {
            if (f == null || name.NullOrEmpty()) return false;
            foreach (var (_, o) in All())
                if (o.people == f && o.stage == RansomStage.Paying && o.kind != RansomKind.Demand && (o.captiveName == name || o.captive?.LabelShort == name))
                    return true;
            return false;
        }

        /// <summary>The price set down where their messenger stands (or the gate): silver, or a tribe's goods.</summary>
        private static void SetDown(RansomOffer o, Map map, Pawn m)
        {
            var at = m != null && m.Spawned ? m.Position : DropCellFinder.TradeDropSpot(map);
            foreach (var t in Goods(o))
                if (GenPlace.TryPlaceThing(t, at, map, ThingPlaceMode.Near, out var placed))
                    placed?.SetForbidden(false, false);
            // together: their messenger sets it down at the gate and says so.
            if (m != null) Outsiders.Say(m, "ransomset", Negotiator(map));
        }

        /// <summary>After a call: their pod with the price lands where traders leave their goods, as the game's own ransom does.</summary>
        private static void Pod(RansomOffer o, Map map)
        {
            var spot = DropCellFinder.TradeDropSpot(map);
            try
            {
                DropPodUtility.DropThingsNear(spot, map, Goods(o), 110, false, false, false, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom: their pod", e);
                foreach (var t in Goods(o)) GenPlace.TryPlaceThing(t, spot, map, ThingPlaceMode.Near);
            }
        }

        private static List<Thing> Goods(RansomOffer o)
        {
            var def = o.goods.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(o.goods);
            return def != null ? Couriers.Make(def, RansomMath.GoodsCount(o.price, def.BaseMarketValue)) : Couriers.Make(ThingDefOf.Silver, o.price);
        }

        // ================= The trap =================

        /// <summary>A trap forced by the dev action: the next exchange with a hostile people springs it.</summary>
        internal static bool forceTrap;

        /// <summary>
        /// With a hostile people an exchange at the gate may be a trap (decision 5): at the peace talks' rate by the colony's
        /// negotiator, a band of theirs at 0.35 of the threat points walks in from the gate's side as the captive walks out.
        /// </summary>
        private static void Trap(RansomOffer o, Map map)
        {
            var f = o.people;
            if (!Diplomacy.Hostile(f)) return;
            bool sprung = forceTrap || Rand.Chance(TrapChance(f, map));
            forceTrap = false;
            if (!sprung) return;
            var gate = DropCellFinder.TradeDropSpot(map);
            var from = EdgeNear(map, gate);
            string label = RansomWords.P("CC_Ransom_Trap_Label", RansomWords.People(f).Named("PEOPLE"));
            string text = RansomWords.T("CC_Ransom_Trap", RansomWords.People(f).Named("PEOPLE"), RansomWords.Name(o.captive, o.captiveName).Named("CAPTIVE"));
            if (Raids.Send(map, f, StorytellerUtility.DefaultThreatPointsNow(map) * RansomMath.TrapPoints, label, text, null, from))
                FactionMemory.Note(f, "trapped", RansomWords.P("CC_Ransom_Deed_Trap"));
        }

        /// <summary>The cell on the map's edge nearest the gate that reaches the colony (the trap comes from the gate's side).</summary>
        private static IntVec3? EdgeNear(Map map, IntVec3 gate)
        {
            if (!gate.IsValid) return null;
            IntVec3 best = IntVec3.Invalid;
            float bestD = float.MaxValue;
            foreach (var c in CellRect.WholeMap(map).EdgeCells)
            {
                if (!c.Standable(map) || c.Fogged(map)) continue;
                float d = c.DistanceToSquared(gate);
                if (d >= bestD || !map.reachability.CanReachColony(c)) continue;
                best = c;
                bestD = d;
            }
            return best.IsValid ? best : (IntVec3?)null;
        }

        // ================= Ours in their hands: the game's demand by messenger =================

        /// <summary>One of the colony's held by another people, with no demand of the game's or Rimfolk's open for them, whose people can send word; or null.</summary>
        public static Pawn DemandCandidate(Map map, out Faction by)
        {
            by = null;
            var open = new HashSet<Pawn>();
            foreach (var l in Find.LetterStack.LettersListForReading)
                if (l is ChoiceLetter_RansomDemand d && d.kidnapped != null)
                    open.Add(d.kidnapped);
            foreach (var (_, o) in All())
                if (o.ours != null)
                    open.Add(o.ours);
            var list = new List<(Pawn, Faction)>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f?.kidnapped == null || f.IsPlayer || Bearer(f) == null) continue;
                foreach (var p in f.kidnapped.KidnappedPawnsListForReading)
                    if (p != null && !p.Dead && p.Faction == Faction.OfPlayer && p.RaceProps.Humanlike && !open.Contains(p))
                        list.Add((p, f));
            }
            if (list.Count == 0) return null;
            var pick = list.RandomElement();
            by = pick.Item2;
            return pick.Item1;
        }

        /// <summary>
        /// The game's demand, where the colony has no comms console (decision 4): the same colonist, the game's fee, their
        /// messenger at the gate with the game's day to answer. False: nobody to ask about, or nobody to carry their word.
        /// </summary>
        public static bool Demand(Map map)
        {
            var book = DiplomacyBook.Instance;
            if (book == null || map == null) return false;
            var p = DemandCandidate(map, out var f);
            if (p == null) return false;
            var h = book.HomeOf(map);
            if (h.offers.Count >= RansomMath.MaxOffers) return false;
            var o = Add(book, h, RansomKind.Demand, null, p, f, Now);
            o.price = RansomMath.Fee(p.MarketValue, Rand.Value);
            if (!Arrivals.TryEntryCell(map, out _))
            {
                h.offers.Remove(o);
                return false;
            }
            Come(h, o, map);
            if (o.stage != RansomStage.Coming)
            {
                h.offers.Remove(o);
                return false;
            }
            return true;
        }

        /// <summary>Their demand, as a letter with the game's day to answer: pay (the silver carried out to their messenger), or refuse.</summary>
        private static void DemandLetter(DiplomacyHome h, RansomOffer o, Map map, Pawn messenger)
        {
            var def = RansomDefOf.Rimfolk_RansomByHand;
            var f = o.people;
            if (def == null || f == null) return;
            var form = RansomWords.Form(map, f);
            string ours = RansomWords.Name(o.ours, o.oursName);
            var by = messenger?.Faction;
            string text = RansomWords.T(by != null && by != f ? "CC_Ransom_Demand_GoBetween" : "CC_Ransom_Demand", (messenger?.LabelShort ?? "?").Named("WHO"),
                              RansomWords.People(f).Named("PEOPLE"), RansomWords.People(by).Named("ESCORT"), ours.Named("OURS"), o.price.Named("SILVER"), form.rite.Named("RITE"))
                          + "\n\n" + RansomWords.P((int)f.def.techLevel < (int)TechLevel.Industrial ? "CC_Ransom_Demand_How" : "CC_Ransom_Demand_HowPod", ours.Named("OURS"),
                              o.price.Named("SILVER"));
            var letter = LetterMaker.MakeLetter(RansomWords.P("CC_Ransom_Demand_Label", ours.Named("OURS")), text, def, messenger != null ? new LookTargets(messenger) : LookTargets.Invalid, f);
            if (letter is ChoiceLetter_RansomByHand l)
            {
                l.offer = o.id;
                l.StartTimeout(RansomMath.AnswerTicks);
            }
            Tell.Ask(letter, map);
        }

        /// <summary>The colony's silver, enough for their demand: why not, or null.</summary>
        public static string PayWhyNot(int id)
        {
            var o = Get(id, out var h);
            var map = h?.Map;
            if (o == null || map == null || o.stage != RansomStage.AtGate) return RansomWords.P("CC_Ransom_Why_Gone");
            if (PlayCarry.Have(map, "silver", ThingDefOf.Silver) < o.price) return RansomWords.P("CC_Ransom_Why_NoSilver", o.price.Named("SILVER"));
            return null;
        }

        /// <summary>Paid: the speaker carries the silver out to their messenger (what is short of one stack is fetched from the stores as it is handed over).</summary>
        public static void Pay(int id)
        {
            var o = Get(id, out var h);
            var map = h?.Map;
            if (o == null || map == null || PayWhyNot(id) != null) return;
            o.stage = RansomStage.Paying;
            o.answerBy = Now + 6 * RansomMath.HourTicks;
            Diplomacy.Dirty();
            var m = Messenger(LordOf(o, map));
            var carrier = Negotiator(map);
            var def = RansomDefOf.Rimfolk_RansomPay;
            if (m != null && carrier != null && def != null)
            {
                var silver = map.listerThings.ThingsOfDef(ThingDefOf.Silver)
                    .Where(x => x.Spawned && !x.IsForbidden(carrier) && (x.IsInAnyStorage() || map.areaManager.Home[x.Position]) && carrier.CanReserveAndReach(x, PathEndMode.ClosestTouch, Danger.Some))
                    .OrderByDescending(x => x.stackCount >= o.price).ThenBy(x => x.Position.DistanceToSquared(carrier.Position)).FirstOrDefault();
                var job = JobMaker.MakeJob(def, m, silver);
                job.count = silver != null ? Mathf.Min(o.price, silver.stackCount) : 1;
                // together: the speaker carries the silver out to their messenger and hands it over face to face.
                carrier.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
                if (carrier.CurJob == job)
                {
                    // apart: told as the speaker sets out; the silver changes hands at the gate.
                    Tell.Message(NewsKind.World, Importance.Notable, map, RansomWords.P("CC_Ransom_Msg_Paying", carrier.Named("PAWN"), RansomWords.People(o.people).Named("PEOPLE")),
                        new LookTargets(carrier), MessageTypeDefOf.NeutralEvent);
                    return;
                }
            }
            // Nobody can walk it out: their messenger takes it from the stores.
            PlayCarry.Take(map, "silver", ThingDefOf.Silver, o.price);
            Paid(o.id, null, o.price);
        }

        /// <summary>
        /// The silver is in their messenger's hands (JobDriver_RansomPay; or taken from the stores): the rest of the fee from
        /// the stores, their messenger goes home, and the colonist comes some hours later.
        /// </summary>
        public static void Paid(int id, Pawn by, int handed)
        {
            var o = Get(id, out var h);
            var map = h?.Map;
            if (o == null || map == null || o.kind != RansomKind.Demand || o.stage != RansomStage.Paying) return;
            int rest = Mathf.Max(0, o.price - handed);
            if (rest > 0) PlayCarry.Take(map, "silver", ThingDefOf.Silver, rest);
            var lord = LordOf(o, map);
            var m = Messenger(lord);
            // together: their messenger answers the one who handed it over.
            if (m != null && by != null) Outsiders.Say(m, "ransomthanks", by);
            lord?.ReceiveMemo(LordJob_Messenger.MemoLeave);
            o.stage = RansomStage.Returning;
            o.due = Now + Mathf.RoundToInt(RansomMath.ReturnHours(Rand.Value) * RansomMath.HourTicks);
            FactionMemory.Note(o.people, "ransomed", RansomWords.Name(o.ours, o.oursName));
            Diplomacy.Dirty();
            // apart: told as their messenger turns for home with the silver; the colonist comes back later on the map.
            Tell.Message(NewsKind.World, Importance.Important, map, RansomWords.P("CC_Ransom_Msg_Paid", o.price.Named("SILVER"), RansomWords.People(o.people).Named("PEOPLE"),
                RansomWords.Name(o.ours, o.oursName).Named("OURS")), m != null ? new LookTargets(m) : LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>Their messenger still waits for the colony's silver (a demand agreed, not yet paid).</summary>
        public static bool AwaitsSilver(int id)
        {
            var o = Get(id, out _);
            return o != null && o.kind == RansomKind.Demand && o.stage == RansomStage.Paying;
        }

        /// <summary>Refused: their messenger goes home (the game's own refusal: nothing more).</summary>
        public static void RefuseDemand(int id)
        {
            var o = Get(id, out var h);
            if (o == null) return;
            Drop(h, o, RansomWords.P("CC_Ransom_Msg_DemandRefused", RansomWords.People(o.people).Named("PEOPLE")));
        }

        /// <summary>
        /// Ours comes home: from a people below the machine ages, set down at the map's edge to walk in to the gate; from
        /// the others, by pod where traders leave their goods (as the game's own demand returns them).
        /// </summary>
        private static void WalkIn(DiplomacyHome h, RansomOffer o, Map map)
        {
            var p = o.ours;
            var f = o.people;
            if (p == null || p.Dead || f?.kidnapped == null)
            {
                Drop(h, o, null);
                return;
            }
            try
            {
                f.kidnapped.RemoveKidnappedPawn(p);
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                IntVec3 at;
                if ((int)f.def.techLevel < (int)TechLevel.Industrial)
                {
                    if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && map.reachability.CanReachColony(c), map, CellFinder.EdgeRoadChance_Friendly, out at))
                        at = DropCellFinder.TradeDropSpot(map);
                    GenSpawn.Spawn(p, at, map);
                    var gate = DropCellFinder.TradeDropSpot(map);
                    if (gate.IsValid && p.jobs != null)
                    {
                        var job = JobMaker.MakeJob(JobDefOf.Goto, gate);
                        job.locomotionUrgency = LocomotionUrgency.Walk;
                        p.jobs.StartJob(job, JobCondition.InterruptForced);
                    }
                }
                else
                {
                    at = DropCellFinder.TradeDropSpot(map);
                    TradeUtility.SpawnDropPod(at, map, p);
                }
                if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom: the colony's own home", e);
            }
            var form = RansomWords.Form(map, f);
            Diplomacy.Write(DiplomacyMath.Ransomed, new List<HistoryActor>
            {
                new HistoryActor(WorldLedger.PersonOf(p), HistoryRole.About), new HistoryActor(Diplomacy.Colony(map), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.With)
            }, Diplomacy.Colony(map), RansomWords.P("CC_Ransom_Hist_Ours", p.Named("OURS"), RansomWords.People(f).Named("PEOPLE"), o.price.Named("SILVER"), form.word.Named("WORD")), true);
            Drop(h, o, null);
            // apart: the colonist comes home on the map; the message looks at them.
            Tell.Message(NewsKind.World, Importance.Important, map, RansomWords.P((int)f.def.techLevel < (int)TechLevel.Industrial ? "CC_Ransom_Msg_Home" : "CC_Ransom_Msg_HomePod",
                p.Named("OURS"), RansomWords.People(f).Named("PEOPLE")), new LookTargets(p), MessageTypeDefOf.PositiveEvent);
        }

        // ================= An exchange =================

        /// <summary>Why the colony can't offer this exchange now, or null.</summary>
        public static string ExchangeWhyNot(Pawn ours, Pawn theirs, Map map)
        {
            if (!On) return RansomWords.P("CC_Ransom_Why_Off");
            var f = theirs?.Faction;
            if (ours == null || theirs == null || f == null || !theirs.IsPrisonerOfColony || theirs.MapHeld != map) return RansomWords.P("CC_Ransom_Why_Gone");
            if (!Deals(f)) return RansomWords.P("CC_Ransom_Why_Never", RansomWords.People(f).Named("PEOPLE"));
            if (!(f.kidnapped?.KidnappedPawnsListForReading.Contains(ours) ?? false)) return RansomWords.P("CC_Ransom_Why_NotHeld", ours.Named("OURS"), RansomWords.People(f).Named("PEOPLE"));
            if (For(ours) != null || For(theirs) != null) return RansomWords.P("CC_Ransom_Why_UnderWay");
            if (Bearer(f) == null) return RansomWords.P("CC_Ransom_Why_NoBearer", RansomWords.People(f).Named("PEOPLE"));
            var h = DiplomacyBook.Instance?.Existing(map);
            if (h != null && h.offers.Count >= RansomMath.MaxOffers) return RansomWords.P("CC_Ransom_Why_TooMany");
            return null;
        }

        /// <summary>The exchanges the colony could offer this people from a home: each of ours they hold for each of theirs in its cells.</summary>
        public static List<(Pawn ours, Pawn theirs)> Pairs(Faction f, Map map)
        {
            var list = new List<(Pawn, Pawn)>();
            if (f == null || map == null) return list;
            var held = FavourCalls.Held(f).Where(p => p.Faction == Faction.OfPlayer).ToList();
            if (held.Count == 0) return list;
            foreach (var t in map.mapPawns.PrisonersOfColonySpawned)
                if (t?.Faction == f)
                    foreach (var o in held)
                        list.Add((o, t));
            return list;
        }

        /// <summary>
        /// They hear the colony's offer of an exchange (a letter, or face to face): by who is worth more to them, a notable,
        /// their head's regard. Agreed: their party brings the colony's own to the gate after <paramref name="hours"/>.
        /// Returns their answer in words.
        /// </summary>
        public static string HearExchange(Pawn ours, Pawn theirs, Map map, float hours, out bool agreed)
        {
            agreed = false;
            var f = theirs?.Faction;
            string why = ExchangeWhyNot(ours, theirs, map);
            if (why != null) return RansomWords.T("CC_Ransom_Ans_Moot", RansomWords.People(f).Named("PEOPLE"));
            bool notable = Notable(theirs, f, out bool leader, out bool kin);
            var head = FavourCalls.HeadOf(f, map);
            int regard = head != null ? NotableBook.Opinion(head) : f.PlayerGoodwill;
            var form = RansomWords.Form(map, f);
            if (!RansomMath.ExchangeAccepted(ours.MarketValue, theirs.MarketValue, notable || leader || kin, regard, Rand.Value))
                return RansomWords.T("CC_Ransom_Ans_ExchangeNo", RansomWords.People(f).Named("PEOPLE"), ours.Named("OURS"), theirs.Named("THEIRS"));
            var book = DiplomacyBook.Instance;
            var h = book.HomeOf(map);
            Add(book, h, RansomKind.Exchange, theirs, ours, f, Now + Mathf.RoundToInt(Mathf.Max(0.1f, hours) * RansomMath.HourTicks));
            agreed = true;
            return RansomWords.T("CC_Ransom_Ans_ExchangeYes", RansomWords.People(f).Named("PEOPLE"), ours.Named("OURS"), theirs.Named("THEIRS"), form.rite.Named("RITE"));
        }

        /// <summary>The colony's own walks in beside their party, toward the gate (free once on the colony's land, as the game sets them down).</summary>
        private static bool WalkWith(RansomOffer o, Map map, IntVec3 entry)
        {
            var p = o.ours;
            var f = o.people;
            try
            {
                if (p == null || p.Dead || f?.kidnapped == null) return false;
                if (p.Spawned) return true;
                f.kidnapped.RemoveKidnappedPawn(p);
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                GenSpawn.Spawn(p, CellFinder.RandomClosewalkCellNear(entry, map, 3), map);
                if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
                var gate = DropCellFinder.TradeDropSpot(map);
                if (gate.IsValid && p.jobs != null)
                {
                    var job = JobMaker.MakeJob(JobDefOf.Goto, gate);
                    job.locomotionUrgency = LocomotionUrgency.Walk;
                    p.jobs.StartJob(job, JobCondition.InterruptForced);
                }
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom: the colony's own walked in", e);
                return false;
            }
        }

        /// <summary>Their party at the gate with the colony's own: the warden walks theirs out, and with a hostile people the trap may spring.</summary>
        private static void Exchange(DiplomacyHome h, RansomOffer o, Map map, Pawn m)
        {
            var f = o.people;
            o.stage = RansomStage.Paying;
            o.answerBy = Now + 10 * RansomMath.HourTicks;
            Release(o.captive);
            FactionMemory.Note(f, "ransomed", RansomWords.Name(o.captive, o.captiveName));
            var form = RansomWords.Form(map, f);
            Diplomacy.Write(DiplomacyMath.Exchanged, new List<HistoryActor>
            {
                new HistoryActor(Diplomacy.Colony(map), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.With),
                new HistoryActor(WorldLedger.PersonOf(o.ours), HistoryRole.About), new HistoryActor(WorldLedger.PersonOf(o.captive), HistoryRole.About)
            }, Diplomacy.Colony(map), RansomWords.P("CC_Ransom_Hist_Exchanged", RansomWords.Name(o.ours, o.oursName).Named("OURS"),
                RansomWords.Name(o.captive, o.captiveName).Named("THEIRS"), RansomWords.People(f).Named("PEOPLE"), form.word.Named("WORD")), true);
            // together: their messenger hands the colony's own over at the gate, face to face with them.
            if (m != null && o.ours != null && o.ours.Spawned) Outsiders.Say(m, "exchangegate", o.ours);
            Tell.Letter(NewsKind.World, Importance.Important, map, RansomWords.P("CC_Ransom_Exchanged_Label", RansomWords.Name(o.ours, o.oursName).Named("OURS")),
                RansomWords.T("CC_Ransom_Exchanged", RansomWords.Name(o.ours, o.oursName).Named("OURS"), RansomWords.Name(o.captive, o.captiveName).Named("THEIRS"),
                    RansomWords.People(f).Named("PEOPLE"), form.rite.Named("RITE")), LetterDefOf.PositiveEvent, o.ours != null && o.ours.Spawned ? new LookTargets(o.ours) : LookTargets.Invalid, f);
            Trap(o, map);
        }

        // ================= The colony's letter about a captive =================

        /// <summary>
        /// They have the colony's letter about one of theirs in its cells: wanted back (a second thought), their offer comes
        /// with their messenger soon after their answer; else they will not pay. Returns their words.
        /// </summary>
        public static string HearOffer(Pawn captive, Map map, float days, out bool yes)
        {
            yes = false;
            var f = captive?.Faction;
            if (f == null || !captive.IsPrisonerOfColony || captive.MapHeld != map || !Deals(f) || Grudge(f) != null)
                return RansomWords.T("CC_Ransom_Ans_Moot", RansomWords.People(f).Named("PEOPLE"));
            var book = DiplomacyBook.Instance;
            var h = book.HomeOf(map);
            var o = h.offers.FirstOrDefault(x => x.captive == captive && x.kind == RansomKind.Theirs);
            if (o == null && !Wanted(captive, f, true)) return RansomWords.T("CC_Ransom_Ans_No", RansomWords.People(f).Named("PEOPLE"), captive.Named("CAPTIVE"));
            if (o == null)
            {
                if (h.offers.Count >= RansomMath.MaxOffers) return RansomWords.T("CC_Ransom_Ans_Moot", RansomWords.People(f).Named("PEOPLE"));
                o = Add(book, h, RansomKind.Theirs, captive, null, f, Now);
            }
            o.asked = true;
            if (o.stage == RansomStage.Waiting) o.due = Mathf.Min(o.due, Now + Mathf.RoundToInt(Mathf.Max(0.1f, days) * RansomMath.DayTicks));
            yes = true;
            return RansomWords.T("CC_Ransom_Ans_Yes", RansomWords.People(f).Named("PEOPLE"), captive.Named("CAPTIVE"), RansomWords.Form(map, f).rite.Named("RITE"));
        }

        // ================= A home lost =================

        /// <summary>A home is no longer the colony's: its offers end (a messenger there goes home).</summary>
        internal static void HomeLost(DiplomacyHome h)
        {
            foreach (var o in h.offers.ToList()) Drop(h, o, null);
        }
    }

    /// <summary>
    /// Their offer for one of theirs in the colony's cells: accept their price, ask for more, let them go for nothing, keep
    /// them, or put it off within their day.
    /// </summary>
    public class ChoiceLetter_RansomOffer : ChoiceLetter
    {
        public int offer = -1;

        private RansomOffer Live(out DiplomacyHome h)
        {
            var o = Ransoms.Get(offer, out h);
            return o != null && o.stage == RansomStage.AtGate && o.kind == RansomKind.Theirs ? o : null;
        }

        /// <summary>Still waiting for the colony's answer (the inbox's row).</summary>
        public bool Waiting => !ArchivedOnly && Live(out _) != null;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var o = ArchivedOnly ? null : Live(out _);
                if (o == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                int id = o.id, price = o.price;
                yield return new DiaOption(RansomWords.P("CC_Ransom_Opt_Accept", RansomWords.Price(o.price, o.goods).Named("PRICE")))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        Ransoms.Accept(id, price);
                    },
                    resolveTree = true
                };
                yield return new DiaOption(RansomWords.P("CC_Ransom_Opt_More"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        Ransoms.AskMore(id);
                    },
                    resolveTree = true
                };
                yield return new DiaOption(RansomWords.P("CC_Ransom_Opt_Free"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        Ransoms.Free(id);
                    },
                    resolveTree = true
                };
                yield return new DiaOption(RansomWords.P("CC_Ransom_Opt_Keep"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        Ransoms.Keep(id);
                    },
                    resolveTree = true
                };
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref offer, "offer", -1);
        }
    }

    /// <summary>Their demand for one of the colony's they hold, by their messenger at the gate: pay (greyed without the silver), refuse, or put it off within the game's day.</summary>
    public class ChoiceLetter_RansomByHand : ChoiceLetter
    {
        public int offer = -1;

        private RansomOffer Live()
        {
            var o = Ransoms.Get(offer, out _);
            return o != null && o.stage == RansomStage.AtGate && o.kind == RansomKind.Demand ? o : null;
        }

        public bool Waiting => !ArchivedOnly && Live() != null;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var o = ArchivedOnly ? null : Live();
                if (o == null)
                {
                    yield return Option_Close;
                    yield break;
                }
                int id = o.id;
                var pay = new DiaOption(RansomWords.P("CC_Ransom_Opt_Pay", o.price.Named("SILVER")))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        Ransoms.Pay(id);
                    },
                    resolveTree = true
                };
                string why = Ransoms.PayWhyNot(id);
                if (why != null) pay.Disable(why);
                yield return pay;
                yield return new DiaOption(RansomWords.P("CC_Ransom_Opt_Refuse"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        Ransoms.RefuseDemand(id);
                    },
                    resolveTree = true
                };
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref offer, "offer", -1);
        }
    }

    [DefOf]
    public static class RansomDefOf
    {
        public static LetterDef Rimfolk_RansomOffer;
        public static LetterDef Rimfolk_RansomByHand;
        public static LetterDef Rimfolk_EnvoyOffer;
        public static JobDef Rimfolk_RansomPay;
        public static JobDef Rimfolk_EnvoyLetter;
        public static JobDef Rimfolk_EnvoySitIn;
        public static JobDef Rimfolk_EnvoySetOut;
        public static DutyDef Rimfolk_EnvoyStay;
        public static DutyDef Rimfolk_EnvoyIndoors;

        static RansomDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(RansomDefOf));
        }
    }
}
