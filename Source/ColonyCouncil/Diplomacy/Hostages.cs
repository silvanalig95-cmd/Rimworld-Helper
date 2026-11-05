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
    /// Hostages for a truce's or a pact's terms (docs/design/stage2-diplomacy.md, round b, item 7, and item 2's "a hostage
    /// from round b"; release 2b.3; the player's decision 10; the rules in WardMath.cs). Adults only: children are never
    /// hostages. Asked as a truce's price (the letter's "a hostage from them", "a hostage from the colony: Ada") or a defence
    /// pact's term, either way: theirs, one of their notable's kin, walks in to the gate once the truce is sworn or the pact
    /// made (a people at war with the colony only where a people at peace with both can vouch for them) and lives at the
    /// colony as the game's lodger for the term (the quest of Wards.cs); the colony's sets out for their town by caravan and
    /// lives there, a lent colonist (WardsAbroad.cs). At the term's end both go home: theirs walks out, and their people
    /// remember the colony returned them (the deed "hostageReturned"). Their side breaking a truce makes theirs the colony's
    /// to decide about (their letter): send them home anyway (the deed "forgave"), keep them (the warden arrests them by the
    /// game's own arrest, and ransom applies), or ask them to stay if they would. The colony breaking its word leaves its own
    /// hostage in their hands: their KidnappedPawnsTracker, the game's own captivity, with its ransom demand (or their
    /// messenger's, Ransoms.cs), its rescues, and in time joining them. The Hostage tie runs from whoever gave them to whoever
    /// holds them, about the hostage, its count the tie it guarantees; called in when that tie is broken.
    /// </summary>
    public static class Hostages
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        public static bool On => Wards.On;

        /// <summary>KeepFor.Hostage still holds: a hostage of theirs agreed and not yet come to the colony.</summary>
        public static bool Holds(Pawn p)
        {
            var w = Wards.Of(p);
            return w != null && w.kind == WardKind.TheirHostage && w.stage == WardStage.Coming;
        }

        /// <summary>The hostages given for this truce or pact, either way.</summary>
        public static List<WardStay> For(Tie t) =>
            t == null ? new List<WardStay>() : Wards.All().Select(x => x.ward).Where(w => w.Hostage && w.guarantees == t.id).ToList();

        // ================= Who could be given =================

        /// <summary>Grown colonists of the home (18 or more) who could go as the colony's hostage: free, not a lodger, not away already.</summary>
        public static List<Pawn> OurCandidates(Map home)
        {
            var list = new List<Pawn>();
            if (home == null) return list;
            foreach (var p in home.mapPawns.FreeColonistsSpawned)
            {
                if (p == null || p.Dead || p.IsQuestLodger() || Wards.Of(p) != null || EnvoysAbroad.Of(p, out _) != null) continue;
                if (!WardMath.MayBeHostage(p.ageTracker.AgeBiologicalYearsFloat, p.DevelopmentalStage.Juvenile())) continue;
                list.Add(p);
            }
            return list.OrderBy(p => p.skills?.skills?.Sum(s => s.Level) ?? 0).ToList();
        }

        /// <summary>Why the colony can't ask them for a hostage of theirs now, or null.</summary>
        public static string TheirWhyNot(Faction f, Map home)
        {
            if (!On) return WardWords.P("CC_Ward_Why_Off");
            if (Diplomacy.Hostile(f) && Truces.ByWord(f)) return WardWords.P("CC_Hostage_Why_NoEscort", WardWords.People(f).Named("PEOPLE"));
            if (Wards.Kin(f, home, out _) == null) return WardWords.P("CC_Ward_Why_NoKin", WardWords.People(f).Named("PEOPLE"));
            return Room(home, WardKind.TheirHostage) ? null : WardWords.P("CC_Ward_Why_Full");
        }

        /// <summary>Why the colony can't give one of its own now, or null.</summary>
        public static string OurWhyNot(Faction f, Map home)
        {
            if (!On) return WardWords.P("CC_Ward_Why_Off");
            if (WardsAbroad.TownFor(f, home) == null) return RansomWords.P("CC_Envoy_Why_NoTown", WardWords.People(f).Named("PEOPLE"));
            if (home != null && home.mapPawns.FreeColonistsSpawnedCount < 2) return RansomWords.P("CC_Envoy_Why_Alone");
            if (OurCandidates(home).Count == 0) return WardWords.P("CC_Hostage_Why_NoAdult");
            return Room(home, WardKind.OurHostage) ? null : WardWords.P("CC_Ward_Why_Full");
        }

        /// <summary>
        /// Room at the home for one more hostage of this kind for this truce or pact (one each way per tie, four records in
        /// all). One whose term is over (going home, or a pact's year out) no longer stands for the tie: a renewed pact may
        /// have a new hostage of its own while the last walks home.
        /// </summary>
        private static bool Fits(Map home, WardKind kind, Tie guaranteed)
        {
            var h = DiplomacyBook.Instance?.Existing(home);
            return h == null || WardMath.Fits(h.wards.Select(x => (x.kind, Served(x) ? -3 : x.guarantees)).ToList(), kind, guaranteed?.id ?? -2);
        }

        /// <summary>A hostage whose term is over: walking (or riding) home, or here or there with the pact's year out.</summary>
        private static bool Served(WardStay w) =>
            w != null && w.Hostage && (w.stage == WardStage.Going || (w.stage == WardStage.Here || w.stage == WardStage.Away) && EndOf(w) == WardMath.HostageEnd.Home);

        /// <summary>Room at the home for one more record of this kind, whatever it guarantees (for the letters' why not).</summary>
        private static bool Room(Map home, WardKind kind)
        {
            var h = DiplomacyBook.Instance?.Existing(home);
            return h == null || WardMath.Fits(h.wards.Select(x => (x.kind, x.guarantees)).ToList(), kind, -2);
        }

        /// <summary>A truce's hostage prices for the letter, each greyed with why not: a hostage from them, and one of the colony's by name.</summary>
        public static List<FavourCalls.Option> TruceOptions(Faction f, Map home, string prefix)
        {
            var list = new List<FavourCalls.Option>();
            list.Add(new FavourCalls.Option
            {
                code = prefix + TrucePrice.TheirHostage + ":0", label = PactWords.Price(TrucePrice.TheirHostage, 0).CapitalizeFirst(), why = TheirWhyNot(f, home)
            });
            string ourWhy = OurWhyNot(f, home);
            if (ourWhy != null)
            {
                list.Add(new FavourCalls.Option { code = prefix + TrucePrice.OurHostage + ":0", label = WardWords.P("CC_Hostage_OptionOurs"), why = ourWhy });
                return list;
            }
            foreach (var p in OurCandidates(home).Take(6))
                list.Add(new FavourCalls.Option { code = prefix + TrucePrice.OurHostage + ":" + p.thingIDNumber, label = PactWords.Price(TrucePrice.OurHostage, p.thingIDNumber).CapitalizeFirst() });
            return list;
        }

        /// <summary>A defence pact's hostage terms for the letter: none, a hostage from them, one of the colony's by name.</summary>
        public static List<FavourCalls.Option> PactOptions(Faction f, Map home)
        {
            var list = new List<FavourCalls.Option> { new FavourCalls.Option { code = WardMath.HostagePrefix + "None", label = WardWords.P("CC_Hostage_OptionNone") } };
            if (!On) return list;
            list.Add(new FavourCalls.Option { code = WardMath.HostageCode(false), label = WardWords.P("CC_Hostage_OptionTheirs"), why = TheirWhyNot(f, home) });
            string ourWhy = OurWhyNot(f, home);
            if (ourWhy != null)
            {
                list.Add(new FavourCalls.Option { code = WardMath.HostageCode(true, 0), label = WardWords.P("CC_Hostage_OptionOurs"), why = ourWhy });
                return list;
            }
            foreach (var p in OurCandidates(home).Take(6))
                list.Add(new FavourCalls.Option { code = WardMath.HostageCode(true, p.thingIDNumber), label = WardWords.P("CC_Hostage_OptionOursNamed", p.Named("PAWN")) });
            return list;
        }

        /// <summary>The colonist named in a price or a term, or the record's name for one gone.</summary>
        public static string NameOf(int id)
        {
            if (id <= 0) return WardWords.P("CC_Hostage_Someone");
            var p = Refs.Pawn(Ref.Person(id));
            return p?.LabelShort ?? WardWords.P("CC_Hostage_Someone");
        }

        // ================= Given =================

        /// <summary>The truce sworn with a hostage of theirs as its price (Truces.Pay): one of their kin walks in to the gate some hours on. Its words for the letter.</summary>
        public static string TruceTheirs(Tie truce, Faction f, Map home) => Theirs(truce, f, home, "truce");

        /// <summary>The truce sworn with one of the colony's as its price: they set out for the town. Its words for the letter.</summary>
        public static string TruceOurs(Tie truce, Faction f, Map home, int colonist) => Ours(truce, f, home, colonist, "truce");

        /// <summary>A defence pact made with a hostage either way among its terms (the letter's ask).</summary>
        public static string ForPact(Faction f, Map home, PactKind k, string code)
        {
            if (!WardMath.ParseHostage(code, out bool ours, out int id)) return null;
            var t = Pacts.With(f).FirstOrDefault(x => Pacts.HomeOf(x) == home && Pacts.KindOf(x) == k);
            if (t == null) return null;
            return ours ? Ours(t, f, home, id, "pact") : Theirs(t, f, home, "pact");
        }

        private static string Theirs(Tie guaranteed, Faction f, Map home, string what)
        {
            var book = DiplomacyBook.Instance;
            if (book == null || guaranteed == null || f == null || home == null || TheirWhyNot(f, home) != null || !Fits(home, WardKind.TheirHostage, guaranteed))
                return WardWords.P("CC_Hostage_Lapsed_Theirs", WardWords.People(f).Named("PEOPLE"));
            var kin = Wards.Kin(f, home, out _);
            var p = MakeHostage(f, kin);
            if (p == null) return WardWords.P("CC_Hostage_Lapsed_Theirs", WardWords.People(f).Named("PEOPLE"));
            var h = book.HomeOf(home);
            var w = new WardStay
            {
                id = book.NextId(), kind = WardKind.TheirHostage, stage = WardStage.Coming, pawn = p, people = f, guarantees = guaranteed.id, agreed = Now,
                due = Now + Mathf.RoundToInt(Rand.Range(3f, 8f) * GenDate.TicksPerHour), notable = kin?.id ?? -1, name = p.LabelShort, until = TermEndOf(guaranteed)
            };
            h.wards.Add(w);
            WorldLedger.Keep(p, KeepFor.Hostage);
            var tie = WorldLedger.AddTie(Tie.Kind.Hostage, Refs.Of(f), Diplomacy.Colony(home), new TieTerms { about = Refs.Of(p), key = what, count = guaranteed.id }, Tie.Never,
                WardMath.HostageOrigin);
            if (tie != null) w.tie = tie.id;
            Diplomacy.Dirty();
            var form = WardWords.HostageForm(home, f);
            return WardWords.P("CC_Hostage_Given_Theirs", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"));
        }

        private static string Ours(Tie guaranteed, Faction f, Map home, int colonist, string what)
        {
            var book = DiplomacyBook.Instance;
            var p = colonist > 0 ? Refs.Pawn(Ref.Person(colonist)) : null;
            var town = WardsAbroad.TownFor(f, home);
            if (book == null || guaranteed == null || f == null || home == null || p == null || p.Dead || !p.Spawned || p.Map != home || town == null
                || !OurCandidates(home).Contains(p) || !Fits(home, WardKind.OurHostage, guaranteed))
                return WardWords.P("CC_Hostage_Lapsed_Ours", WardWords.People(f).Named("PEOPLE"));
            var h = book.HomeOf(home);
            var w = new WardStay
            {
                id = book.NextId(), kind = WardKind.OurHostage, stage = WardStage.Coming, pawn = p, people = f, guarantees = guaranteed.id, agreed = Now, due = Now,
                town = town.ID, townName = town.Name, name = p.LabelShort, until = TermEndOf(guaranteed)
            };
            h.wards.Add(w);
            var tie = WorldLedger.AddTie(Tie.Kind.Hostage, Diplomacy.Colony(home), Refs.Of(f), new TieTerms { about = Refs.Of(p), key = what, count = guaranteed.id }, Tie.Never,
                WardMath.HostageOrigin);
            if (tie != null) w.tie = tie.id;
            Diplomacy.Write(DiplomacyMath.Hostage, new List<HistoryActor>
            {
                new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did), new HistoryActor(Refs.Of(p), HistoryRole.About), new HistoryActor(Refs.Of(f), HistoryRole.For)
            }, Diplomacy.Colony(home), WardWords.P("CC_Hostage_Hist_GivenOurs", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE"),
                WardWords.HostageForm(home, f).word.Named("WORD")), true);
            Diplomacy.Dirty();
            Outsiders.Say(p, "hostagego", null);
            var form = WardWords.HostageForm(home, f);
            return WardWords.P("CC_Hostage_Given_Ours", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE"), town.Name.Named("TOWN"), form.word.Named("WORD"));
        }

        /// <summary>One of their notable's kin, grown (18 to 40): their child where the ages fit, else of their name. Kept in the world until they come.</summary>
        public static Pawn MakeHostage(Faction f, Notable kin)
        {
            try
            {
                var kind = Arrivals.PersonKind(f);
                if (kind == null) return null;
                var parent = kin?.pawn;
                string last = parent?.Name is NameTriple tri ? tri.Last : null;
                var p = Arrivals.Make(kind, f, new FloatRange(WardMath.HostageAge, 40f), null, last, false, parent);
                if (p == null) return null;
                if (parent != null && !parent.Dead && parent.RaceProps?.Humanlike == true
                    && WardMath.CanBeParent(parent.ageTracker.AgeBiologicalYearsFloat, p.ageTracker.AgeBiologicalYearsFloat))
                {
                    try
                    {
                        p.relations?.AddDirectRelation(PawnRelationDefOf.Parent, parent);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("hostages: their kin", e);
                    }
                }
                if (!Find.WorldPawns.Contains(p)) Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hostages: one of theirs", e);
                return null;
            }
        }

        /// <summary>Their hostage at the gate joined the colony as its lodger (Wards.Join): the history and the letter.</summary>
        public static void Arrived(DiplomacyHome h, WardStay w, Map map)
        {
            var p = w.pawn;
            var f = w.people;
            var form = WardWords.HostageForm(map, f);
            var g = WorldLedger.Get(w.guarantees);
            string what = g?.kind == Tie.Kind.Pact ? WardWords.P("CC_Hostage_ForPact") : WardWords.P("CC_Hostage_ForTruce");
            Diplomacy.Write(DiplomacyMath.Hostage, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(Refs.Of(p), HistoryRole.About), new HistoryActor(Diplomacy.Colony(map), HistoryRole.For)
            }, Diplomacy.Colony(map), WardWords.P("CC_Hostage_Hist_Came", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE"), form.word.Named("WORD")), true);
            Tell.Letter(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Hostage_Arrives_Label", p.Named("HOSTAGE")),
                WardWords.T("CC_Hostage_Arrives", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"), what.Named("WHAT"))
                + "\n\n" + WardWords.P("CC_Hostage_Arrives_How", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE")),
                LetterDefOf.NeutralEvent, new LookTargets(p), f);
        }

        // ================= The term =================

        /// <summary>
        /// How the term stands, from the truce or pact the hostage was given for (WardMath.EndOf): a defence pact's hostage
        /// goes home at the end of the pact year they were given for, even when the pact is renewed.
        /// </summary>
        public static WardMath.HostageEnd EndOf(WardStay w)
        {
            var g = WorldLedger.Get(w?.guarantees ?? -1);
            if (g == null) return WardMath.HostageEnd.Home;
            bool brokenByThem = g.kind == Tie.Kind.Truce && g.terms?.goods == Truces.BrokenByThemKey;
            bool yearOver = g.kind == Tie.Kind.Pact && g.Live && WardMath.YearOver(TermEnd(w, g), Now);
            return WardMath.EndOf(g.Live, g.state == Tie.State.Broken, brokenByThem, yearOver);
        }

        /// <summary>A pact's hostage's term: the end of the pact's year as it stands when they are given; none for a truce (its own term is the truce's).</summary>
        private static int TermEndOf(Tie guaranteed) => guaranteed?.kind == Tie.Kind.Pact ? WardMath.YearEndTick(guaranteed.terms?.number ?? 0f) : -1;

        /// <summary>
        /// A pact's hostage's term's end (WardStay.until), noted at the giving; a hostage given before the rule takes the
        /// pact's year as it stands at the first look, and keeps it however the pact is renewed after.
        /// </summary>
        private static int TermEnd(WardStay w, Tie pact)
        {
            if (w.until < 0)
            {
                w.until = TermEndOf(pact);
                if (w.until >= 0) Diplomacy.Dirty();
            }
            return w.until;
        }

        /// <summary>
        /// Their hostage's hour (Wards' hour): the term over, they walk home; their side broke it, they are the colony's to
        /// decide about (the letter); the colony broke it, they go home at once.
        /// </summary>
        public static void TermHour(DiplomacyHome h, WardStay w, Map map)
        {
            switch (EndOf(w))
            {
                case WardMath.HostageEnd.Holds:
                    return;
                case WardMath.HostageEnd.TheyBroke:
                    Forfeit(h, w, map);
                    return;
                default:
                    Wards.StartGoing(h, w, map);
                    return;
            }
        }

        /// <summary>Their side broke the terms: the hostage is the colony's to decide about, by a letter (This week shows it waiting).</summary>
        private static void Forfeit(DiplomacyHome h, WardStay w, Map map)
        {
            w.stage = WardStage.Forfeit;
            w.due = Now;
            var t = WorldLedger.Get(w.tie);
            if (t != null && t.state == Tie.State.Open) WorldLedger.Set(t, Tie.State.CalledIn);
            Diplomacy.Dirty();
            var def = WardDefOf.Rimfolk_HostageForfeit;
            if (def == null) return;
            var p = w.pawn;
            var f = w.people;
            var form = WardWords.HostageForm(map, f);
            string text = WardWords.T("CC_Hostage_Forfeit", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE"), form.word.Named("WORD"))
                          + "\n\n" + WardWords.P("CC_Hostage_Forfeit_How", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE"));
            var letter = LetterMaker.MakeLetter(WardWords.P("CC_Hostage_Forfeit_Label", p.Named("HOSTAGE")), text, def, new LookTargets(p), f);
            if (letter is ChoiceLetter_HostageForfeit l) l.ward = w.id;
            Tell.Ask(letter, map);
        }

        /// <summary>A forfeit hostage's hour: left unanswered a few days, they slip away home.</summary>
        public static void ForfeitHour(DiplomacyHome h, WardStay w, Map map, int now)
        {
            var p = w.pawn;
            if (now - w.due < WardMath.ForfeitDays * WardMath.DayTicks || p.CurJobDef == WardDefOf.Rimfolk_WardLeave || !Together.Able(p)) return;
            if (p.CurJobDef == JobDefOf.Arrest) return;
            Wards.WalkOut(w);
        }

        /// <summary>Gone without an answer (out at the edge, or no longer the colony's): told, the tie ended, the quest over.</summary>
        public static void SlippedAway(DiplomacyHome h, WardStay w, Map map)
        {
            var p = w.pawn;
            var f = w.people;
            try
            {
                // Out at the edge on their own walk (JobDriver_WardLeave): gone from the map; anyone who fled goes their own way.
                if (p.Spawned && p.CurJobDef == WardDefOf.Rimfolk_WardLeave) p.ExitMap(false, CellRect.WholeMap(p.Map).GetClosestEdge(p.Position));
                if (p.Faction == Faction.OfPlayer && f != null && !f.defeated) p.SetFaction(f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hostages: slipped away", e);
            }
            var t = WorldLedger.Get(w.tie);
            if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Ended);
            Wards.EndQuest(w, QuestEndOutcome.Unknown);
            // apart: the hostage is gone; told in a message.
            Tell.Message(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Hostage_Msg_SlippedAway", WardWords.Name(w).Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE")),
                LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            Wards.Clear(h, w, false);
        }

        /// <summary>
        /// Their hostage home (Wards.Home, as they leave): the term kept, their people remember the colony returned them
        /// ("hostageReturned"); sent home anyway after their side broke it, "forgave"; going home because the colony broke
        /// it, nothing to its credit.
        /// </summary>
        public static void Returned(DiplomacyHome h, WardStay w, Map map)
        {
            var p = w.pawn;
            var f = w.people;
            var end = EndOf(w);
            var t = WorldLedger.Get(w.tie);
            string key;
            if (end == WardMath.HostageEnd.TheyBroke)
            {
                if (f != null && !f.defeated) FactionMemory.Note(f, "forgave", p.LabelShort);
                if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Ended);
                key = "CC_Hostage_Home_Forgave";
            }
            else if (end == WardMath.HostageEnd.WeBroke)
            {
                if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Ended);
                key = "CC_Hostage_Home_WeBroke";
            }
            else
            {
                if (f != null && !f.defeated) FactionMemory.Note(f, "hostageReturned", p.LabelShort, WardMath.ReturnedWeight(Wards.DaysHere(w)));
                if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Kept);
                key = "CC_Hostage_Home_Kept";
            }
            var form = WardWords.HostageForm(map, f);
            Diplomacy.Write(DiplomacyMath.Hostage, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(p), HistoryRole.About), new HistoryActor(Diplomacy.Colony(map), HistoryRole.With), new HistoryActor(Refs.Of(f), HistoryRole.For)
            }, Diplomacy.Colony(map), WardWords.P("CC_Hostage_Hist_Home", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE"), form.word.Named("WORD")));
            // apart: the hostage walks home on the map; the message looks at them.
            Tell.Message(NewsKind.World, Importance.Notable, map, WardWords.T(key, p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE"), form.word.Named("WORD")),
                p.Spawned ? new LookTargets(p) : LookTargets.Invalid, end == WardMath.HostageEnd.Home ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NeutralEvent);
        }

        // ================= The colony's decision =================

        /// <summary>Sent home anyway: they walk out (the deed "forgave" as they go).</summary>
        public static void SendHome(WardStay w, Map map)
        {
            if (w == null || map == null || w.stage != WardStage.Forfeit) return;
            Wards.Of(w.pawn, out var h);
            Wards.StartGoing(h, w, map);
            // apart: the colony's answer to the letter; the hostage walks out on the map.
            Tell.Message(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Hostage_Msg_SentHome", w.pawn.Named("HOSTAGE")), new LookTargets(w.pawn),
                MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>A warden who could arrest them: up, able to do warden's work, not the hostage; nearest first.</summary>
        private static Pawn Warden(WardStay w, Map map)
        {
            var p = w.pawn;
            return map.mapPawns.FreeColonistsSpawned.Where(c => c != p && !c.Downed && !c.Dead && !c.InMentalState && !c.IsQuestLodger() && !c.DevelopmentalStage.Juvenile()
                                                                && !c.WorkTypeIsDisabled(WorkTypeDefOf.Warden) && c.CanReach(p, PathEndMode.Touch, Danger.Some))
                .OrderBy(c => c.Position.DistanceToSquared(p.Position)).FirstOrDefault();
        }

        private static Building_Bed PrisonBed(WardStay w, Pawn warden) =>
            warden == null ? null : RestUtility.FindBedFor(w.pawn, warden, false, false, GuestStatus.Prisoner);

        /// <summary>Why the colony can't keep them as a prisoner now, or null: they are here and up, a warden can reach them, a prisoner's bed is free.</summary>
        public static string ArrestWhyNot(WardStay w, Map map)
        {
            if (w?.pawn == null || !w.pawn.Spawned || w.pawn.Map != map) return WardWords.P("CC_Hostage_Why_NotHere");
            var warden = Warden(w, map);
            if (warden == null) return WardWords.P("CC_Hostage_Why_NoWarden");
            return PrisonBed(w, warden) == null ? WardWords.P("CC_Hostage_Why_NoBed") : null;
        }

        /// <summary>Kept: a warden goes to arrest them by the game's own arrest (they may resist, as anyone may); once they are a prisoner, ransom applies.</summary>
        public static void Arrest(WardStay w, Map map)
        {
            if (ArrestWhyNot(w, map) != null) return;
            var warden = Warden(w, map);
            var bed = PrisonBed(w, warden);
            var job = JobMaker.MakeJob(JobDefOf.Arrest, w.pawn, bed);
            job.count = 1;
            // together: the warden walks over and arrests them by the game's own arrest.
            warden.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            // apart: the colony's answer to the letter; the arrest plays out on the map.
            Tell.Message(NewsKind.World, Importance.Important, map, WardWords.P("CC_Hostage_Msg_Arrest", warden.Named("WARDEN"), w.pawn.Named("HOSTAGE")), new LookTargets(warden),
                MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>A forfeit hostage taken prisoner (by the game's arrest): their people's again, held in the colony's cells; ransom applies.</summary>
        public static void Arrested(DiplomacyHome h, WardStay w, Map map)
        {
            var p = w.pawn;
            var f = w.people;
            Wards.EndQuest(w, QuestEndOutcome.Fail);
            try
            {
                if (p.Faction == Faction.OfPlayer && f != null && !f.defeated) p.SetFaction(f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hostages: a prisoner's people", e);
            }
            var t = WorldLedger.Get(w.tie);
            if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Broken);
            Diplomacy.Write(DiplomacyMath.Hostage, new List<HistoryActor>
            {
                new HistoryActor(Diplomacy.Colony(map), HistoryRole.Did), new HistoryActor(Refs.Of(p), HistoryRole.About), new HistoryActor(Refs.Of(f), HistoryRole.With)
            }, Diplomacy.Colony(map), WardWords.P("CC_Hostage_Hist_Kept", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE")), true);
            Tell.Letter(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Hostage_Kept_Label", p.Named("HOSTAGE")),
                WardWords.T("CC_Hostage_Kept", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE")), LetterDefOf.NeutralEvent, new LookTargets(p), f);
            Wards.Clear(h, w, false);
        }

        /// <summary>Why they wouldn't stay with the colony now, or null (content after ten days here).</summary>
        public static string StayWhyNot(WardStay w)
        {
            var p = w?.pawn;
            if (p == null || !p.Spawned) return WardWords.P("CC_Hostage_Why_NotHere");
            return WardMath.WouldJoin(p.needs?.mood?.CurLevelPercentage ?? 0f, Wards.DaysHere(w)) ? null : WardWords.P("CC_Hostage_Why_WontStay", p.Named("HOSTAGE"));
        }

        /// <summary>Asked to stay, they do: no longer anyone's lodger but one of the colony's own (the quest over).</summary>
        public static void TakeIn(WardStay w, Map map)
        {
            if (w == null || StayWhyNot(w) != null) return;
            var p = w.pawn;
            var f = w.people;
            Wards.Of(p, out var h);
            try
            {
                var q = Wards.QuestOf(w);
                var part = q?.GetFirstPartOfType<QuestPart_ExtraFaction>();
                part?.affectedPawns.Remove(p);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hostages: taken in", e);
            }
            Wards.EndQuest(w, QuestEndOutcome.Success);
            var t = WorldLedger.Get(w.tie);
            if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Broken);
            Diplomacy.Write(DiplomacyMath.Hostage, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(p), HistoryRole.Did), new HistoryActor(Diplomacy.Colony(map), HistoryRole.With), new HistoryActor(Refs.Of(f), HistoryRole.For)
            }, Diplomacy.Colony(map), WardWords.P("CC_Hostage_Hist_Stayed", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE")), true);
            Tell.Letter(NewsKind.World, Importance.Notable, map, WardWords.P("CC_Hostage_Stayed_Label", p.Named("HOSTAGE")),
                WardWords.T("CC_Hostage_Stayed", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE")), LetterDefOf.PositiveEvent, new LookTargets(p), f);
            Wards.Clear(h, w, false);
        }

        // ================= The colony's own, held =================

        /// <summary>
        /// The colony broke its word: its hostage is held by their people, in the game's own captivity (their
        /// KidnappedPawnsTracker: its ransom demand, its rescues, in time joining them). The record stays while they are held.
        /// </summary>
        public static void Held(DiplomacyHome h, WardStay w)
        {
            var p = w.pawn;
            var f = w.people;
            var map = h.Map;
            try
            {
                var c = p.GetCaravan();
                if (c != null)
                {
                    c.RemovePawn(p);
                    if (!c.PawnsListForReading.Any(x => x != null && x.IsColonist)) c.Destroy();
                }
                WorldLedger.Release(p, KeepFor.Lent);
                if (f?.kidnapped != null && !People.Kidnapped(p) && p.Faction != f) f.kidnapped.Kidnap(p, null);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("hostages: held", e);
            }
            w.stage = WardStage.Held;
            w.since = Now;
            var t = WorldLedger.Get(w.tie);
            if (t != null && t.state == Tie.State.Open) WorldLedger.Set(t, Tie.State.CalledIn);
            Diplomacy.Write(DiplomacyMath.Hostage, new List<HistoryActor>
            {
                new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(Refs.Of(p), HistoryRole.About), new HistoryActor(Diplomacy.Colony(map), HistoryRole.With)
            }, Diplomacy.Colony(map), WardWords.P("CC_Hostage_Hist_Held", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE")), true);
            Tell.Letter(NewsKind.World, Importance.Important, map, WardWords.P("CC_Hostage_Held_Label", p.Named("HOSTAGE")),
                WardWords.T("CC_Hostage_Held", p.Named("HOSTAGE"), WardWords.People(f).Named("PEOPLE")) + "\n\n" + WardWords.P("CC_Hostage_Held_How", p.Named("HOSTAGE")),
                LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
            Diplomacy.Dirty();
        }

        /// <summary>Held no longer (ransomed, rescued, gone over to them, dead): the record let go, the tie closed.</summary>
        public static void Free(DiplomacyHome h, WardStay w)
        {
            var t = WorldLedger.Get(w.tie);
            if (t != null && t.Live) WorldLedger.Set(t, Tie.State.Broken);
            Wards.Clear(h, w, false);
        }
    }
}
