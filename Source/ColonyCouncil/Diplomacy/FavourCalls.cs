using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// The colony calls in a debt of honour (docs/design/stage2-diplomacy.md, round a, item 1): from a letter or a call to
    /// their notable (DiplomacyLetters.cs; the Diplomacy page and the town panel open that letter), or face to face with
    /// one of theirs at the colony (DiplomacyActs.cs). What they can do now, each greyed with why not: a load of goods by
    /// their town's fortune, brought by their porters; a word for the colony with a third people (10 goodwill, said in that
    /// people's letter); a colonist of the colony they hold, set down at the edge of the colony's land to walk home; their
    /// help toward peace with a people at war with the colony (a promise the truce's answer reads: stage 2, release 2a.2).
    /// Fighters come with that release (decision 3). They honour it while their head (or the notable who owes) thinks well
    /// of the colony (20 or more), seven times in ten at a lower regard, four in ten below 0; a refusal is their letter (or
    /// their word face to face) and the deed "brokeWord".
    /// </summary>
    public static class FavourCalls
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>One thing they could do now: what, in words, and why not when it can't be.</summary>
        public sealed class Option
        {
            public FavourAsk ask;
            public string arg;
            public string label;
            public string why;

            public string Code => FavourMath.AskCode(ask, arg);
        }

        // ================= Whether it can be called in =================

        /// <summary>Why this debt of theirs can't be called in now, or null.</summary>
        public static string WhyNot(Tie t)
        {
            if (t == null || !t.Live || FavoursOwed.ColonyOwes(t)) return DipWords.P("CC_Dip_Why_Nothing");
            if (!Diplomacy.FavoursOn) return DipWords.P("CC_Dip_Why_Off");
            if (t.state == Tie.State.CalledIn) return DipWords.P("CC_Dip_Why_Called");
            var f = FavoursOwed.PeopleOf(t);
            if (f == null || f.defeated) return DipWords.P("CC_Dip_Why_Gone");
            if (Diplomacy.Hostile(f)) return DipWords.P("CC_Dip_Why_War", DipWords.People(f).Named("PEOPLE"));
            if (FavoursOwed.IsPersonal(t))
            {
                var n = FavoursOwed.NotableOf(t);
                if (n == null || !n.Alive) return DipWords.P("CC_Dip_Why_Gone");
                if (!KinMemory.Ours(WorldLedger.PawnOf(t.to))) return DipWords.P("CC_Dip_Why_OwedGone");
            }
            return null;
        }

        /// <summary>What they could do now for this debt, each with why not.</summary>
        public static List<Option> Options(Tie t, Map home)
        {
            var list = new List<Option>();
            var f = FavoursOwed.PeopleOf(t);
            if (f == null) return list;
            home ??= FavoursOwed.HomeOf(t);
            // A load of goods, by their ways.
            foreach (var kind in FavourMath.GoodsKinds(Ages.Of(f)))
            {
                var (def, count) = Goods(kind, f, t);
                list.Add(new Option
                {
                    ask = FavourAsk.Goods, arg = kind, why = def == null ? DipWords.P("CC_Dip_Why_NoGoods") : null,
                    label = DipWords.P("CC_Dip_Opt_Goods", Couriers.Amount(count, null, def).Named("GOODS"), DipWords.People(f).Named("PEOPLE"))
                });
            }
            // A word with a third people they are on good terms with.
            var friends = ThirdPeoples(f).ToList();
            if (friends.Count == 0)
                list.Add(new Option { ask = FavourAsk.Word, label = DipWords.P("CC_Dip_Opt_WordAny"), why = DipWords.P("CC_Dip_Why_NoFriends", DipWords.People(f).Named("PEOPLE")) });
            foreach (var o in friends)
                list.Add(new Option
                {
                    ask = FavourAsk.Word, arg = Refs.Of(o).ToString(), label = DipWords.P("CC_Dip_Opt_Word", DipWords.People(o).Named("PEOPLE")),
                    why = o.HasGoodwill && o.PlayerGoodwill >= 100 ? DipWords.P("CC_Dip_Why_FullGoodwill", DipWords.People(o).Named("PEOPLE")) : null
                });
            // A colonist of the colony they hold.
            var held = Held(f);
            if (held.Count == 0)
                list.Add(new Option { ask = FavourAsk.Captive, label = DipWords.P("CC_Dip_Opt_CaptiveAny"), why = DipWords.P("CC_Dip_Why_NoCaptive", DipWords.People(f).Named("PEOPLE")) });
            foreach (var p in held)
                list.Add(new Option
                {
                    ask = FavourAsk.Captive, arg = Refs.Of(p).ToString(), label = DipWords.P("CC_Dip_Opt_Captive", p.Named("PAWN")),
                    why = home == null ? DipWords.P("CC_Dip_Why_NoHome") : null
                });
            // Their help toward peace with a people at war with the colony.
            var enemies = Enemies(f).ToList();
            if (enemies.Count == 0)
                list.Add(new Option { ask = FavourAsk.Peace, label = DipWords.P("CC_Dip_Opt_PeaceAny"), why = DipWords.P("CC_Dip_Why_NoEnemy", DipWords.People(f).Named("PEOPLE")) });
            foreach (var e in enemies)
                list.Add(new Option
                {
                    ask = FavourAsk.Peace, arg = Refs.Of(e).ToString(), label = DipWords.P("CC_Dip_Opt_Peace", DipWords.People(e).Named("PEOPLE")),
                    why = PeacePromised(f, e) != null ? DipWords.P("CC_Dip_Why_Promised", DipWords.People(f).Named("PEOPLE")) : null
                });
            return list;
        }

        /// <summary>The goods a kind means for them, and how many make the load (by their nearest town's fortune).</summary>
        public static (ThingDef def, int count) Goods(string kind, Faction f, Tie t)
        {
            ThingDef def;
            var age = Ages.Of(f);
            switch (kind)
            {
                case "medicine":
                    def = age <= WorldAge.Medieval ? ThingDefOf.MedicineHerbal : ThingDefOf.MedicineIndustrial;
                    break;
                case "wood": def = ThingDefOf.WoodLog; break;
                case "steel": def = ThingDefOf.Steel; break;
                case "silver": def = ThingDefOf.Silver; break;
                default:
                    def = DefDatabase<ThingDef>.GetNamedSilentFail("Pemmican") ?? ThingDefOf.RawPotatoes;
                    break;
            }
            if (def == null) return (null, 0);
            float worth = FavourMath.GoodsWorth(Prosperity(f, t));
            return (def, FavourMath.GoodsCount(worth, def.BaseMarketValue));
        }

        /// <summary>Their nearest town's fortune (the notable's own town for a personal debt), 50 when the region keeps none.</summary>
        private static float Prosperity(Faction f, Tie t)
        {
            try
            {
                var n = FavoursOwed.NotableOf(t);
                var town = n != null ? NotableBook.TownOf(n.town) : WorldSituationWorker.NearestTown(f, FavoursOwed.HomeOf(t));
                var rec = town != null ? WorldLedger.TownRecord(town.ID) : null;
                return rec != null && rec.Seeded ? rec.prosperity : 50f;
            }
            catch
            {
                return 50f;
            }
        }

        /// <summary>Peoples they are on good terms with (not at war with them), whom the colony isn't at war with either.</summary>
        public static IEnumerable<Faction> ThirdPeoples(Faction f)
        {
            if (f == null) yield break;
            foreach (var o in Find.FactionManager.AllFactionsListForReading)
                if (o != f && Diplomacy.Deals(o) && o.HasGoodwill && !Diplomacy.Hostile(o) && !o.HostileTo(f))
                    yield return o;
        }

        /// <summary>Peoples at war with the colony that they still speak with (not at war with them).</summary>
        public static IEnumerable<Faction> Enemies(Faction f)
        {
            if (f == null) yield break;
            foreach (var o in Find.FactionManager.AllFactionsListForReading)
                if (o != f && Diplomacy.Deals(o) && o.HasGoodwill && Diplomacy.Hostile(o) && !o.HostileTo(f))
                    yield return o;
        }

        /// <summary>The colony's own people they hold (carried off, and not yet brought home).</summary>
        public static List<Pawn> Held(Faction f)
        {
            var list = new List<Pawn>();
            try
            {
                var k = f?.kidnapped?.KidnappedPawnsListForReading;
                if (k != null)
                    foreach (var p in k)
                        if (p != null && !p.Dead && p.RaceProps.Humanlike)
                            list.Add(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favours: who they hold", e);
            }
            return list;
        }

        /// <summary>Their open promise to speak for peace with that people, or null.</summary>
        public static Tie PeacePromised(Faction f, Faction enemy)
        {
            if (f == null || enemy == null) return null;
            var from = Refs.Of(f);
            var about = Refs.Of(enemy);
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Promise))
                if (t != null && t.Live && t.origin == FavourMath.Origin && t.from == from && t.terms.key == FavourMath.PeaceKey && t.terms.about == about)
                    return t;
            return null;
        }

        /// <summary>How well whoever answers for the debt thinks of the colony: the notable who owes (of the colonist owed), else their head, else the people's stance.</summary>
        public static int Regard(Tie t)
        {
            var f = FavoursOwed.PeopleOf(t);
            var n = FavoursOwed.NotableOf(t);
            if (n != null)
            {
                var owed = WorldLedger.PawnOf(t.to);
                return owed != null ? NotableBook.OpinionOf(n, owed) : NotableBook.Opinion(n);
            }
            var head = HeadOf(f, FavoursOwed.HomeOf(t));
            if (head != null) return NotableBook.Opinion(head);
            return f != null && f.HasGoodwill ? f.PlayerGoodwill : 0;
        }

        /// <summary>Their head the colony knows: of the town nearest it, else their people's leader's.</summary>
        public static Notable HeadOf(Faction f, Map home)
        {
            var book = NotableBook.Instance;
            if (f == null || book == null) return null;
            var town = home != null ? WorldSituationWorker.NearestTown(f, home) : null;
            return book.notables.Where(n => n != null && n.faction == f && n.role == NotableRole.Head && n.Alive)
                .OrderBy(n => town != null && n.town == town.ID ? 0 : 1).FirstOrDefault();
        }

        // ================= The answer =================

        /// <summary>How they answered: honoured or refused, and their words for it (for their letter, or a message face to face).</summary>
        public sealed class Answer
        {
            public bool honoured;
            public string line;
            public List<Thing> things = new List<Thing>();
        }

        /// <summary>
        /// They hear the ask (their letter is read, or one of theirs hears it face to face) and answer it. Honoured: it is
        /// done (goods are in <see cref="Answer.things"/> for the letter's messenger to bring, or brought by their porters
        /// when <paramref name="byLetter"/> is false). Refused: the deed "brokeWord".
        /// </summary>
        public static Answer Hear(Tie t, string code, Map home, Pawn asker, bool byLetter, float roll)
        {
            var a = new Answer();
            var f = FavoursOwed.PeopleOf(t);
            if (t == null || f == null || !FavourMath.ParseAsk(code, out var ask, out string arg) || WhyNot(t) != null)
            {
                a.line = DipWords.T("CC_Dip_Ans_Moot");
                return a;
            }
            home ??= FavoursOwed.HomeOf(t);
            string who = FavoursOwed.NotableOf(t)?.Name ?? DipWords.People(f);
            FavoursOwed.CallIn(t, code);
            a.honoured = FavourMath.Honours(Regard(t), roll);
            if (!a.honoured)
            {
                FavoursOwed.Close(t, Tie.State.Broken);
                FactionMemory.Note(f, "brokeWord", DipWords.Ask(ask, arg));
                a.line = DipWords.T("CC_Dip_Ans_Refused", who.Named("WHO"), DipWords.People(f).Named("PEOPLE"), DipWords.Ask(ask, arg).Named("ASK"));
                return a;
            }
            string done;
            try
            {
                done = Do(t, f, ask, arg, home, asker, byLetter, a.things);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favours: honoured", e);
                done = null;
            }
            FavoursOwed.Close(t, Tie.State.Kept);
            a.line = DipWords.T("CC_Dip_Ans_Honoured", who.Named("WHO"), DipWords.People(f).Named("PEOPLE"), DipWords.Ask(ask, arg).Named("ASK"))
                     + (done.NullOrEmpty() ? "" : " " + done);
            return a;
        }

        /// <summary>What honouring it does now; returns a line for their answer about what follows on the map.</summary>
        private static string Do(Tie t, Faction f, FavourAsk ask, string arg, Map home, Pawn asker, bool byLetter, List<Thing> things)
        {
            switch (ask)
            {
                case FavourAsk.Goods:
                {
                    var (def, count) = Goods(arg, f, t);
                    if (def == null || count <= 0) return null;
                    var made = Couriers.Make(def, count);
                    string goods = Couriers.Describe(made);
                    if (byLetter)
                    {
                        // Their messenger brings it with the answer (NeighbourLetters: the answer's courier sets it down).
                        things.AddRange(made);
                        return DipWords.P("CC_Dip_Done_GoodsLetter", goods.Named("GOODS"));
                    }
                    if (home == null) return null;
                    // Their porters bring it some hours later, by day, and set it down by the gate (Couriers/); nobody to
                    // send, it is set down the old way.
                    Couriers.Loot(made, home, DropCellFinder.TradeDropSpot(home), f);
                    return DipWords.P("CC_Dip_Done_Goods", goods.Named("GOODS"), DipWords.People(f).Named("PEOPLE"));
                }
                case FavourAsk.Word:
                {
                    var other = DipWords.FactionOf(arg);
                    if (other == null) return null;
                    PutInWord(f, other, home);
                    return DipWords.P("CC_Dip_Done_Word", DipWords.People(other).Named("PEOPLE"));
                }
                case FavourAsk.Captive:
                {
                    var p = DipWords.PawnOf(arg);
                    if (p == null || home == null) return null;
                    return WalkHome(f, p, home) ? DipWords.P("CC_Dip_Done_Captive", p.Named("PAWN")) : null;
                }
                default:
                {
                    var enemy = DipWords.FactionOf(arg);
                    if (enemy == null) return null;
                    PromisePeace(f, enemy, home);
                    return DipWords.P("CC_Dip_Done_Peace", DipWords.People(enemy).Named("PEOPLE"));
                }
            }
        }

        /// <summary>
        /// Their word with a third people: that people thinks better of the colony (10 goodwill), and says so in a letter of
        /// its own; the history keeps it (dip:word).
        /// </summary>
        public static void PutInWord(Faction f, Faction other, Map home)
        {
            WorldSituationWorker.Goodwill(other, FavourMath.WordGoodwill, false);
            string text = DipWords.T("CC_Dip_WordLetter", DipWords.People(other).Named("PEOPLE"), DipWords.People(f).Named("FRIEND"));
            Tell.Letter(NewsKind.World, Importance.Notable, home, DipWords.P("CC_Dip_WordLetter_Label", DipWords.People(other).Named("PEOPLE")), text,
                LetterDefOf.PositiveEvent, LookTargets.Invalid, other);
            Diplomacy.Write(DiplomacyMath.Word, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(Diplomacy.Colony(home), HistoryRole.For), new HistoryActor(Refs.Of(other), HistoryRole.With)
            }, Diplomacy.Colony(home), DipWords.P("CC_Dip_Hist_Word", DipWords.People(f).Named("FRIEND"), DipWords.People(other).Named("PEOPLE")));
        }

        /// <summary>
        /// Their help toward peace: a Promise from them to the colony, about the people at war with it (key "peace", 20 in
        /// its number), standing a year. The truce's answer (release 2a.2) reads it as that people thinking 20 better of the
        /// colony; until then it is kept and shown.
        /// </summary>
        public static void PromisePeace(Faction f, Faction enemy, Map home)
        {
            var colony = Diplomacy.Colony(home);
            var terms = new TieTerms { key = FavourMath.PeaceKey, number = FavourMath.PeaceRegard, about = Refs.Of(enemy) };
            WorldLedger.AddTie(Tie.Kind.Promise, Refs.Of(f), colony, terms, Now + (int)(FavourMath.PeaceDays * FavourMath.DayTicks), FavourMath.Origin);
            Diplomacy.Write(DiplomacyMath.Peace, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(colony, HistoryRole.For), new HistoryActor(Refs.Of(enemy), HistoryRole.About)
            }, colony, DipWords.P("CC_Dip_Hist_Peace", DipWords.People(f).Named("FRIEND"), DipWords.People(enemy).Named("PEOPLE")), true);
        }

        /// <summary>
        /// A colonist they hold, let go: taken off their list of captives and set down at the edge of the colony's land,
        /// from where they walk to the gate (the game's own way back for a ransomed colonist of a people of the old ways).
        /// </summary>
        public static bool WalkHome(Faction f, Pawn p, Map home)
        {
            try
            {
                if (p == null || p.Dead || home == null || f?.kidnapped == null) return false;
                if (!CellFinder.TryFindRandomEdgeCellWith(c => home.reachability.CanReachColony(c) && !c.Fogged(home), home, CellFinder.EdgeRoadChance_Neutral, out var cell))
                    return false;
                f.kidnapped.RemoveKidnappedPawn(p);
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                GenSpawn.Spawn(p, cell, home);
                if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
                var gate = DropCellFinder.TradeDropSpot(home);
                if (gate.IsValid && p.jobs != null)
                {
                    var job = JobMaker.MakeJob(JobDefOf.Goto, gate);
                    job.locomotionUrgency = LocomotionUrgency.Walk;
                    p.jobs.StartJob(job, JobCondition.InterruptForced);
                }
                Diplomacy.Write(DiplomacyMath.Freed, new List<HistoryActor>
                {
                    new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(WorldLedger.PersonOf(p), HistoryRole.For)
                }, Diplomacy.Colony(home), DipWords.P("CC_Dip_Hist_Freed", DipWords.People(f).Named("PEOPLE"), p.Named("PAWN")), true);
                // apart: the colonist is set down at the edge of the colony's land and walks in; the message looks at them.
                Tell.Message(NewsKind.World, Importance.Important, home, DipWords.P("CC_Dip_Msg_Freed", p.Named("PAWN"), DipWords.People(f).Named("PEOPLE")),
                    new LookTargets(p), MessageTypeDefOf.PositiveEvent);
                return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("favours: a colonist walked home", e);
                return false;
            }
        }
    }
}
