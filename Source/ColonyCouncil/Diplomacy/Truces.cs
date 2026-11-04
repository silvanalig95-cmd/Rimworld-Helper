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
    /// Truces with a people at war with the colony (docs/design/stage2-diplomacy.md, round a, item 2; release 2a.2; the
    /// rules in PactMath.cs). The colony can offer one when it has beaten a raid of theirs in the last 15 days, holds one
    /// of their notables or kin, or a friend of both has called in a debt for peace (DiplomacyLetters: a letter carried by a
    /// messenger of a people at peace with both, or a call); they offer one themselves now and then after a raid of theirs
    /// is beaten (their letter). Agreed, it is the ledger's Truce tie, still to be sworn: their envoy comes to the gate under
    /// their sign of parley (TruceParley.cs), the colony's speaker walks out to them, the price changes hands, and it is
    /// sworn there. A colonist sworn to vengeance on them objects before the council first (TruceObjection.cs).
    /// <para>While it holds, their raids keep away from that home (Patch_TruceRaids on the game's choice of raiders, a set
    /// kept for a 250-tick stretch; Rimfolk's own raids through Raids.CanRaid) and their raiders' camp near it breaks up.
    /// Once per truce their firebrands may raid anyway (one in ten, 15% for a people of bitter ways), and their raid or ambush
    /// breaks it; the colony breaks it by attacking them. At its end it is a peace when their goodwill has come to 0 (kept),
    /// else it runs out. Nothing is saved but the ties.</para>
    /// </summary>
    public static class Truces
    {
        public const string Origin = PactMath.TruceOrigin;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static float Day => Now / (float)PactMath.DayTicks;

        /// <summary>The setting "Pacts and truces": off, nothing new is made, and those live run out by their terms.</summary>
        public static bool On => ColonyCouncilMod.Settings?.diplomacyPacts ?? true;

        /// <summary>"Truces keep raids away" (with the deeper game hooks): the storyteller doesn't choose a truce people for that home.</summary>
        public static bool KeepRaidsAway => (ColonyCouncilMod.Settings?.truceRaids ?? true) && RimfolkPatches.On;

        // ================= Reading =================

        /// <summary>Every truce still live, sworn or still to be sworn, oldest first.</summary>
        public static List<Tie> Live()
        {
            var list = new List<Tie>();
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Truce))
                if (t != null && t.Live && t.origin == Origin)
                    list.Add(t);
            return list;
        }

        public static Tie Get(int id)
        {
            var t = WorldLedger.Get(id);
            return t != null && t.kind == Tie.Kind.Truce && t.origin == Origin ? t : null;
        }

        /// <summary>Sworn at the gate (not one whose envoy is still to come).</summary>
        public static bool Sworn(Tie t) => t != null && PactMath.ParseTerm(t.terms?.key, out _, out bool pending) && !pending;

        public static bool Pending(Tie t) => t != null && PactMath.ParseTerm(t.terms?.key, out _, out bool pending) && pending;

        public static TruceTerm TermOf(Tie t)
        {
            PactMath.ParseTerm(t?.terms?.key, out var term, out _);
            return term;
        }

        public static TrucePrice PriceOf(Tie t) => PactMath.ParsePrice(t?.terms?.goods);

        /// <summary>The people a truce is with (whichever end is theirs).</summary>
        public static Faction PeopleOf(Tie t) => t == null ? null : Refs.Faction(t.from.kind == RefKind.People ? t.from : t.to);

        /// <summary>The home whose truce it is.</summary>
        public static Map HomeOf(Tie t) => t == null ? null : Diplomacy.HomeOf(t.from.kind == RefKind.Colony ? t.from : t.to);

        /// <summary>The colony offered it (else they did).</summary>
        public static bool OursOffered(Tie t) => t != null && t.from.kind == RefKind.Colony;

        /// <summary>The live truce between this home and that people, sworn or still to be sworn, or null.</summary>
        public static Tie Of(Faction f, Map home)
        {
            if (f == null) return null;
            var people = Refs.Of(f);
            var colony = Diplomacy.Colony(home);
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Truce))
                if (t != null && t.Live && t.origin == Origin && t.Between(people, colony))
                    return t;
            return null;
        }

        /// <summary>Any live truce of the colony's with that people, at any home.</summary>
        public static Tie AnyWith(Faction f)
        {
            if (f == null) return null;
            var people = Refs.Of(f);
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Truce))
                if (t != null && t.Live && t.origin == Origin && t.Involves(people))
                    return t;
            return null;
        }

        public static Tie SwornOf(Faction f, Map home)
        {
            var t = Of(f, home);
            return Sworn(t) ? t : null;
        }

        // ================= The raids it keeps away =================

        private static readonly HashSet<long> keeps = new HashSet<long>();
        private static int keepsAt = Fresh.Never;
        private static Game keepsGame;

        private static long Key(Faction f, Map home) => (long)f.loadID << 32 | (uint)Homes.KeyOf(home);

        /// <summary>Built again on the next ask (a truce sworn or ended).</summary>
        public static void Dirty() => keepsAt = Fresh.Never;

        /// <summary>
        /// A sworn truce keeps this people's raids from this home: read from a set built at most once a 250-tick stretch
        /// (a few lookups a raid roll), so the game's choice of raiders costs nothing more.
        /// </summary>
        public static bool KeepsAway(Faction f, Map home)
        {
            if (f == null || home == null || Current.Game == null) return false;
            int now = Now;
            if (keepsGame != Current.Game || !Fresh.Holds(keepsAt, now, Sched.RareTicks))
            {
                keeps.Clear();
                keepsGame = Current.Game;
                keepsAt = now;
                try
                {
                    foreach (var t in Live())
                    {
                        if (!Sworn(t)) continue;
                        var people = PeopleOf(t);
                        var map = HomeOf(t);
                        if (people != null && map != null) keeps.Add(Key(people, map));
                    }
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("truces: the set kept away", e);
                }
            }
            return keeps.Count > 0 && keeps.Contains(Key(f, home));
        }

        /// <summary>A raid of theirs Rimfolk sends anyway, past the truce (their firebrands): set only while it is sent.</summary>
        internal static Faction letting;

        /// <summary>Rimfolk's own raids ask this (Raids.CanRaid): no raid of a people holding a truce with that home, but their firebrands'.</summary>
        public static bool Bars(Faction f, Map home) => f != null && f != letting && KeepsAway(f, home ?? Homes.Seat);

        // ================= Why it can be offered =================

        /// <summary>When a raid of theirs last came at the colony (the memory's "raided"), or -1.</summary>
        public static int LastRaid(Faction f)
        {
            var mem = FactionMemory.Instance;
            if (mem == null || f == null) return -1;
            int last = -1;
            foreach (var d in mem.deeds)
                if (d != null && d.faction == f && d.kind == "raided" && d.tick > last)
                    last = d.tick;
            return last;
        }

        /// <summary>Fighters of theirs still up on the map (a raid not yet beaten).</summary>
        public static bool Present(Faction f, Map map)
        {
            if (f == null || map?.attackTargetsCache == null) return false;
            foreach (var t in map.attackTargetsCache.TargetsHostileToColony)
                if (t.Thing is Pawn p && p.Faction == f && !p.Dead && !p.Downed && p.Spawned)
                    return true;
            return false;
        }

        /// <summary>Days since a raid of theirs was beaten (-1: none, or theirs still on the map).</summary>
        public static float DaysSinceBeaten(Faction f, Map home)
        {
            int last = LastRaid(f);
            if (last < 0 || Present(f, home)) return -1f;
            return (Now - last) / (float)PactMath.DayTicks;
        }

        /// <summary>One of their notables, their leader, or kin of either, held in this home's cells.</summary>
        public static Pawn Held(Faction f, Map home)
        {
            if (f == null || home == null) return null;
            var book = NotableBook.Instance;
            foreach (var p in home.mapPawns.PrisonersOfColonySpawned.ToList())
            {
                if (p?.Faction != f) continue;
                if (f.leader == p) return p;
                if (book != null && book.notables.Any(n => n != null && n.pawn == p)) return p;
                if (p.relations == null) continue;
                if (f.leader != null && p.relations.FamilyByBlood.Contains(f.leader)) return p;
                if (book != null && book.notables.Any(n => n != null && n.faction == f && n.Alive && p.relations.FamilyByBlood.Contains(n.pawn))) return p;
            }
            return null;
        }

        /// <summary>Their captives (anyone of theirs) in this home's cells, for the price "their captives returned".</summary>
        public static List<Pawn> Captives(Faction f, Map home) =>
            f == null || home == null ? new List<Pawn>() : home.mapPawns.PrisonersOfColonySpawned.Where(p => p != null && p.Faction == f && !p.Dead).ToList();

        /// <summary>A friend's promise to speak for peace with them (a debt of honour called in for it), or null.</summary>
        public static Tie FriendPromise(Faction f)
        {
            if (f == null) return null;
            var about = Refs.Of(f);
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Promise))
                if (t != null && t.Live && t.origin == FavourMath.Origin && t.terms?.key == FavourMath.PeaceKey && t.terms.about == about)
                    return t;
            return null;
        }

        public static bool Permanent(Faction f) => f?.def != null && f.def.permanentEnemy;

        public static TruceWhy Why(Faction f, Map home) =>
            PactMath.Why(Diplomacy.Hostile(f), Permanent(f), DaysSinceBeaten(f, home), Held(f, home) != null, FriendPromise(f) != null);

        /// <summary>Why the colony can't offer them a truce now, or null.</summary>
        public static string WhyNot(Faction f, Map home)
        {
            if (!On) return PactWords.P("CC_Truce_Why_Off");
            if (f == null || !Diplomacy.Deals(f)) return DipWords.P("CC_Dip_Why_Gone");
            if (!Diplomacy.Hostile(f)) return PactWords.P("CC_Truce_Why_NotAtWar", PactWords.People(f).Named("PEOPLE"));
            if (Permanent(f) || !f.HasGoodwill) return PactWords.P("CC_Truce_Why_Never", PactWords.People(f).Named("PEOPLE"));
            if (Of(f, home) != null) return PactWords.P("CC_Truce_Why_Holds", PactWords.People(f).Named("PEOPLE"));
            if (Why(f, home) == TruceWhy.None) return PactWords.P("CC_Truce_Why_NoReason", PactWords.People(f).Named("PEOPLE"));
            return null;
        }

        // ================= Their answer to the colony's offer =================

        /// <summary>The goodwill their answer starts from.</summary>
        private static int Goodwill(Faction f) => f != null && f.HasGoodwill ? f.PlayerGoodwill : -100;

        /// <summary>How they take it, before the roll (the letter's outlook and the dev actions).</summary>
        public static int Score(Faction f, Map home, TrucePrice price, TruceTerm term) =>
            PactMath.TruceScore(Goodwill(f), DaysSinceBeaten(f, home) is float d && d >= 0f && d <= PactMath.BeatenDays, Held(f, home) != null,
                FriendPromise(f) != null, price, term);

        /// <summary>
        /// They have the colony's offer (its letter read, or the call heard): sworn or refused, by how they think of the colony
        /// and what it brings them. Agreed, the truce waits for their envoy, due <paramref name="envoyDays"/> from now; a
        /// friend's promise that helped is spent. Returns their answer in words.
        /// </summary>
        public static string Hear(Faction f, Map home, TruceTerm term, TrucePrice price, int silver, float envoyDays, float roll, out bool accepted)
        {
            accepted = false;
            if (f == null || WhyNot(f, home) != null) return PactWords.T("CC_Truce_Ans_Moot", PactWords.People(f).Named("PEOPLE"));
            int score = Score(f, home, price, term);
            var form = PactWords.TruceForm(home, f);
            if (!PactMath.TruceAccepted(score, roll))
                return PactWords.T("CC_Truce_Ans_No", PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"));
            var promise = FriendPromise(f);
            var friend = promise != null ? Refs.Faction(promise.from) : null;
            var t = MakePending(f, home, term, price, silver, true, friend, envoyDays);
            if (t == null) return PactWords.T("CC_Truce_Ans_Moot", PactWords.People(f).Named("PEOPLE"));
            accepted = true;
            if (promise != null) WorldLedger.Set(promise, Tie.State.Kept);
            return PactWords.T("CC_Truce_Ans_Yes", PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"), PactWords.Term(term).Named("TERM"),
                PactWords.Price(price, silver).Named("PRICE"));
        }

        /// <summary>
        /// A truce agreed and still to be sworn: from whoever offered it to the other, its term and price in its terms, the
        /// day their envoy is due (days) and the day they give up coming (number).
        /// </summary>
        public static Tie MakePending(Faction f, Map home, TruceTerm term, TrucePrice price, int silver, bool oursOffered, Faction friend, float envoyDays)
        {
            if (f == null || Of(f, home) != null) return null;
            var people = Refs.Of(f);
            var colony = Diplomacy.Colony(home);
            float due = Day + Mathf.Max(0.05f, envoyDays);
            var terms = new TieTerms
            {
                key = PactMath.TermKey(term, true), days = due, number = due + PactMath.EnvoyGiveUpDays, count = silver, goods = PactMath.PriceKey(price),
                about = friend != null ? Refs.Of(friend) : Ref.None
            };
            var t = WorldLedger.AddTie(Tie.Kind.Truce, oursOffered ? colony : people, oursOffered ? people : colony, terms, Tie.Never, Origin);
            Diplomacy.Dirty();
            return t;
        }

        // ================= Their own offer =================

        /// <summary>When they last offered a truce (the history's dip:truceOffer about them), or -1.</summary>
        public static int LastOffer(Faction f)
        {
            var r = Refs.Of(f);
            foreach (var e in History.Of(r, new[] { DiplomacyMath.TruceOffer }, Now - (int)(PactMath.OfferGapDays * PactMath.DayTicks)))
                return e.tick;
            return -1;
        }

        private static readonly Dictionary<long, bool> onMap = new Dictionary<long, bool>();
        private static Game onMapGame;

        /// <summary>
        /// The hour: each home's hostile peoples on its map, so that when a raid of theirs is beaten (theirs on the map, then
        /// none, with their raid lately in the memory) they may offer a truce themselves.
        /// </summary>
        private static void WatchRaids(int now)
        {
            if (onMapGame != Current.Game)
            {
                onMapGame = Current.Game;
                onMap.Clear();
            }
            foreach (var home in Homes.All)
            {
                if (home == null) continue;
                foreach (var f in Find.FactionManager.AllFactionsListForReading)
                {
                    if (f == null || f.IsPlayer || !Diplomacy.Hostile(f) || !Diplomacy.Deals(f)) continue;
                    long key = Key(f, home);
                    bool here = Present(f, home);
                    bool was = onMap.TryGetValue(key, out bool w) && w;
                    if (here) onMap[key] = true;
                    else if (was)
                    {
                        onMap.Remove(key);
                        int last = LastRaid(f);
                        if (last >= 0 && now - last < 2 * PactMath.DayTicks) Beaten(f, home, now);
                    }
                }
            }
        }

        /// <summary>A raid of theirs beaten: now and then they offer a truce, by their letter (when one of Rimfolk's questions may come).</summary>
        private static void Beaten(Faction f, Map home, int now)
        {
            if (!On || !Tell.CanAsk || Of(f, home) != null) return;
            if (!PactMath.TheyOffer(Diplomacy.Hostile(f), Permanent(f) || !f.HasGoodwill, false, LastOffer(f), now, Rand.Value)) return;
            Offer(f, home, PactMath.TheirPrice(Rand.Value));
        }

        /// <summary>Their offer, as a letter: a truce for a season, with a price of theirs or none.</summary>
        public static void Offer(Faction f, Map home, TrucePrice price)
        {
            var def = PactsDefOf.Rimfolk_TruceOffer;
            if (def == null || f == null || home == null) return;
            int silver = price == TrucePrice.Theirs ? PactMath.PriceSilver(StorytellerUtility.DefaultThreatPointsNow(home)) / 2 : 0;
            var head = FavourCalls.HeadOf(f, home);
            var form = PactWords.TruceForm(home, f);
            string who = head != null ? NotableBook.Called(head) : PactWords.People(f);
            bool call = NotableMath.Callable((int)f.def.techLevel) && NeighbourLetters.Console(home) != null;
            string text = PactWords.T(call ? "CC_Truce_Offer_Call" : "CC_Truce_Offer", who.Named("WHO"), PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"),
                              form.rite.Named("RITE"), PactWords.Term(TruceTerm.Season).Named("TERM"), PactWords.Price(price, silver).Named("PRICE"))
                          + "\n\n" + PactWords.P("CC_Truce_Offer_How", PactWords.People(f).Named("PEOPLE"), FromPeaceLine(f).Named("PEACE"));
            var town = head != null ? NotableBook.TownOf(head.town) : WorldSituationWorker.NearestTown(f, home);
            var letter = LetterMaker.MakeLetter(PactWords.P("CC_Truce_Offer_Label", PactWords.People(f).Named("PEOPLE")), text, def,
                town != null ? new LookTargets(town) : LookTargets.Invalid, f);
            if (letter is ChoiceLetter_TruceOffer l)
            {
                l.people = f;
                l.home = Homes.KeyOf(home);
                l.price = (int)price;
                l.silver = silver;
            }
            Diplomacy.Write(DiplomacyMath.TruceOffer, new List<HistoryActor> { new HistoryActor(Refs.Of(f), HistoryRole.Did), new HistoryActor(Diplomacy.Colony(home), HistoryRole.For) },
                Diplomacy.Colony(home), PactWords.P("CC_Truce_Hist_Offer", PactWords.People(f).Named("PEOPLE")));
            Tell.Ask(letter, home);
        }

        /// <summary>"Their goodwill is 35 short of peace."</summary>
        public static string FromPeaceLine(Faction f)
        {
            int gap = PactMath.FromPeace(Goodwill(f));
            return gap <= 0 ? PactWords.P("CC_Truce_AtPeaceNow") : PactWords.P("CC_Truce_FromPeace", gap.Named("GAP"));
        }

        // ================= The hour =================

        /// <summary>
        /// The hour (Diplomacy.Hourly): raids beaten watched; each truce still to be sworn has its envoy sent, met or let go;
        /// each sworn truce has its firebrands and its end.
        /// </summary>
        public static void Hourly(int now)
        {
            if (On) WatchRaids(now);
            var live = Live();
            if (live.Count == 0) return;
            float day = now / (float)PactMath.DayTicks;
            foreach (var t in live)
            {
                try
                {
                    if (Pending(t)) PendingHour(t, day);
                    else SwornHour(t, day);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("truces: the hour", e);
                }
            }
        }

        private static void PendingHour(Tie t, float day)
        {
            var f = PeopleOf(t);
            var home = HomeOf(t);
            if (f == null || f.defeated || home == null)
            {
                End(t, Tie.State.Ended, null);
                return;
            }
            // At peace already (their goodwill came round by other means): nothing left to swear.
            if (!Diplomacy.Hostile(f))
            {
                End(t, Tie.State.Ended, PactWords.P("CC_Truce_Msg_PeaceFirst", PactWords.People(f).Named("PEOPLE")));
                return;
            }
            var lord = TruceParley.LordOf(t, home);
            if (lord != null)
            {
                TruceParley.Hour(t, lord, home);
                return;
            }
            // The envoy came and went without it sworn: it lapses.
            if (t.terms.days < 0f)
            {
                End(t, Tie.State.Ended, PactWords.P("CC_Truce_Msg_EnvoyLeft", PactWords.People(f).Named("PEOPLE")));
                return;
            }
            if (day > t.terms.number)
            {
                End(t, Tie.State.Ended, PactWords.P("CC_Truce_Msg_EnvoyNever", PactWords.People(f).Named("PEOPLE")));
                return;
            }
            // Due, and the land quiet: the envoy walks in (or, with no people at peace with both to vouch for them, it is sworn by word).
            if (day >= t.terms.days && !MapFacts.ThreatOn(home) && !Present(f, home))
            {
                switch (TruceParley.Send(t, f, home))
                {
                    case TruceParley.Sent.Walking:
                        t.terms.days = -1f;
                        WorldLedger.Touch(t);
                        break;
                    case TruceParley.Sent.NoEscort:
                        Swear(t, null, null);
                        break;
                }
            }
        }

        private static void SwornHour(Tie t, float day)
        {
            var f = PeopleOf(t);
            var home = HomeOf(t);
            if (f == null || f.defeated || home == null)
            {
                End(t, Tie.State.Ended, null);
                return;
            }
            // Peace made while it held (their goodwill came to 0 and the game ended the war): kept.
            if (!Diplomacy.Hostile(f))
            {
                Ended(t, f, home, true);
                return;
            }
            if (day >= t.terms.number)
            {
                Ended(t, f, home, PactMath.Peace(Diplomacy.Hostile(f), Goodwill(f)));
                return;
            }
            if (t.terms.days > 0f && day >= t.terms.days) Firebrands(t, f, home, day);
        }

        // ================= Sworn =================

        /// <summary>
        /// Sworn at the gate (<paramref name="speaker"/> and their <paramref name="envoy"/> face to face), or by word when
        /// no envoy could come (both null): the term starts now, the price changes hands (the colony's silver handed to the
        /// envoy already, theirs set down by them, their captives set to be walked out by the wardens), their firebrands'
        /// roll made once, their raiders' camp near this home breaks up, and the colony is told.
        /// </summary>
        public static void Swear(Tie t, Pawn speaker, Pawn envoy, int handed = 0)
        {
            var f = PeopleOf(t);
            var home = HomeOf(t);
            if (t == null || f == null || home == null || !Pending(t)) return;
            var term = TermOf(t);
            var price = PriceOf(t);
            int silver = t.terms.count;
            float day = Day;
            t.terms.key = PactMath.TermKey(term, false);
            t.since = Now;
            t.terms.number = day + PactMath.TermDays(term);
            t.terms.days = Rand.Chance(PactMath.FirebrandChance(Natural(f))) ? PactMath.FirebrandDay(day, PactMath.TermDays(term), Rand.Value) : 0f;
            WorldLedger.Touch(t);
            Dirty();
            Diplomacy.Dirty();
            string paid = Pay(t, f, home, price, silver, envoy, handed);
            WorldSituationWorker.Goodwill(f, PactMath.SwornGoodwill(price, silver), false);
            FactionMemory.Note(f, "swore", PactWords.P("CC_Truce_Deed"));
            var form = PactWords.TruceForm(home, f);
            Diplomacy.Write(DiplomacyMath.Parley, new List<HistoryActor>
            {
                new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.With)
            }.Concat(KinMemory.Ours(speaker) ? new[] { new HistoryActor(Refs.Of(speaker), HistoryRole.About) } : new HistoryActor[0]).ToList(), Diplomacy.Colony(home),
                PactWords.P("CC_Truce_Hist_Sworn", PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), PactWords.Term(term).Named("TERM")), true);
            RaidersCamps.TruceSworn(home, f);
            string text = PactWords.T(speaker != null ? "CC_Truce_Sworn" : "CC_Truce_SwornByWord", PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"),
                              form.rite.Named("RITE"), (speaker?.LabelShort ?? "").Named("SPEAKER"), (envoy?.LabelShort ?? "").Named("ENVOY"), PactWords.Term(term).Named("TERM"))
                          + (paid.NullOrEmpty() ? "" : "\n\n" + paid)
                          + "\n\n" + PactWords.P("CC_Truce_Sworn_What", PactWords.People(f).Named("PEOPLE"), FromPeaceLine(f).Named("PEACE"));
            Tell.Letter(NewsKind.World, Importance.Important, home, PactWords.P("CC_Truce_Sworn_Label", PactWords.People(f).Named("PEOPLE")), text, LetterDefOf.PositiveEvent,
                envoy != null && envoy.Spawned ? new LookTargets(envoy) : speaker != null && speaker.Spawned ? new LookTargets(speaker) : LookTargets.Invalid, f);
        }

        /// <summary>The price changes hands; its words for the letter.</summary>
        private static string Pay(Tie t, Faction f, Map home, TrucePrice price, int silver, Pawn envoy, int handed)
        {
            try
            {
                switch (price)
                {
                    case TrucePrice.Ours:
                    {
                        // What the speaker carried out was handed over; the rest comes from the stores.
                        int rest = Mathf.Max(0, silver - handed);
                        if (rest > 0) PlayCarry.Take(home, "silver", ThingDefOf.Silver, rest);
                        return PactWords.P("CC_Truce_Paid_Ours", silver.Named("SILVER"), PactWords.People(f).Named("PEOPLE"));
                    }
                    case TrucePrice.Theirs:
                    {
                        var at = envoy != null && envoy.Spawned ? envoy.Position : DropCellFinder.TradeDropSpot(home);
                        // The envoy sets their silver down where they stand; with no envoy, their messenger leaves it at the gate.
                        var made = Couriers.Make(ThingDefOf.Silver, silver);
                        foreach (var s in made) GenPlace.TryPlaceThing(s, at, home, ThingPlaceMode.Near);
                        return PactWords.P("CC_Truce_Paid_Theirs", silver.Named("SILVER"), PactWords.People(f).Named("PEOPLE"));
                    }
                    case TrucePrice.Captives:
                    {
                        int n = 0;
                        foreach (var p in Captives(f, home))
                        {
                            // The wardens walk them out by the game's own release.
                            if (p.guest == null) continue;
                            p.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.Release);
                            n++;
                        }
                        return n > 0 ? PactWords.P("CC_Truce_Paid_Captives", n.Named("COUNT"), PactWords.People(f).Named("PEOPLE")) : null;
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("truces: the price", e);
            }
            return null;
        }

        // ================= Their firebrands =================

        /// <summary>Their natural goodwill with the colony (where it drifts to), 0 when the game can't say.</summary>
        private static int Natural(Faction f)
        {
            try
            {
                return f.NaturalGoodwill;
            }
            catch
            {
                return 0;
            }
        }

        private static void Firebrands(Tie t, Faction f, Map home, float day)
        {
            // Not on top of another fight: they wait some hours.
            if (MapFacts.ThreatOn(home) || !IncidentWorker_Happening.FightsPossible)
            {
                t.terms.days = day + 0.25f;
                return;
            }
            t.terms.days = -1f;
            WorldLedger.Touch(t);
            var form = PactWords.TruceForm(home, f);
            string label = PactWords.P("CC_Truce_Firebrands_Label", PactWords.People(f).Named("PEOPLE"));
            string text = PactWords.T("CC_Truce_Firebrands", PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"));
            letting = f;
            try
            {
                Raids.Send(home, f, StorytellerUtility.DefaultThreatPointsNow(home) * PactMath.FirebrandPoints, label, text);
            }
            finally
            {
                letting = null;
            }
        }

        // ================= Its end =================

        /// <summary>Its term is over, or the war is: a peace (kept) or run out (ended), told in a letter.</summary>
        private static void Ended(Tie t, Faction f, Map home, bool peace)
        {
            var form = PactWords.TruceForm(home, f);
            End(t, peace ? Tie.State.Kept : Tie.State.Ended, null);
            if (peace) FactionMemory.Note(f, "keptWord", PactWords.P("CC_Truce_Deed"));
            string text = PactWords.T(peace ? "CC_Truce_End_Peace" : "CC_Truce_End_RanOut", PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"),
                FromPeaceLine(f).Named("PEACE"));
            Tell.Letter(NewsKind.World, Importance.Notable, home, PactWords.P(peace ? "CC_Truce_End_Peace_Label" : "CC_Truce_End_RanOut_Label", PactWords.People(f).Named("PEOPLE")),
                text, peace ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent, LookTargets.Invalid, f);
        }

        /// <summary>Closed, the raids' set built again, and a message when one is given.</summary>
        public static void End(Tie t, Tie.State state, string message)
        {
            if (t == null || !t.Live) return;
            var home = HomeOf(t);
            WorldLedger.Set(t, state);
            Dirty();
            Diplomacy.Dirty();
            TruceParley.LordOf(t, home)?.ReceiveMemo(LordJob_Parley.MemoLeave);
            if (!message.NullOrEmpty())
                // apart: what became of the truce, told in a message.
                Tell.Message(NewsKind.World, Importance.Notable, home, message, LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        // ================= Breaking it =================

        /// <summary>A raid of theirs fell on a home (the raid's hook): the truce with that home is broken by them.</summary>
        public static void Raided(Faction f, Map map)
        {
            if (f == null || map == null) return;
            var t = Of(f, map);
            if (t == null) return;
            if (!Sworn(t))
            {
                End(t, Tie.State.Ended, PactWords.P("CC_Truce_Msg_RaidedFirst", PactWords.People(f).Named("PEOPLE")));
                return;
            }
            BrokenByThem(t, f, map, PactWords.P("CC_Truce_By_Raid"));
        }

        /// <summary>An ambush of theirs on a caravan of the colony's: every truce with them is broken by them.</summary>
        public static void Ambushed(Faction f)
        {
            foreach (var t in Live().Where(x => PeopleOf(x) == f).ToList())
                if (Sworn(t)) BrokenByThem(t, f, HomeOf(t), PactWords.P("CC_Truce_By_Ambush"));
                else End(t, Tie.State.Ended, null);
        }

        /// <summary>A colonist killed by them while a truce holds: every truce with them broken by them.</summary>
        public static void Killed(Faction f)
        {
            foreach (var t in Live().Where(x => PeopleOf(x) == f && Sworn(x)).ToList()) BrokenByThem(t, f, HomeOf(t), PactWords.P("CC_Truce_By_Killed"));
        }

        private static void BrokenByThem(Tie t, Faction f, Map home, string how)
        {
            WorldLedger.Set(t, Tie.State.Broken);
            Dirty();
            Diplomacy.Dirty();
            FactionMemory.Note(f, "truceBroken", how);
            var form = PactWords.TruceForm(home, f);
            string text = PactWords.T("CC_Truce_BrokenByThem", PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), how.Named("HOW"));
            Tell.Letter(NewsKind.World, Importance.Important, home, PactWords.P("CC_Truce_BrokenByThem_Label", PactWords.People(f).Named("PEOPLE")), text, LetterDefOf.ThreatSmall,
                LookTargets.Invalid, f);
            Broken.Add(t.id);
        }

        /// <summary>
        /// The colony broke it (a deed of its against them, noted by the memory): every live truce with them broken, the deed
        /// "brokeTruce", and its word broken in the ears of every people at peace with a town in reach ("wordBroken").
        /// </summary>
        public static void BrokenByUs(Faction f, string how)
        {
            bool any = false;
            foreach (var t in Live().Where(x => PeopleOf(x) == f).ToList())
            {
                var home = HomeOf(t);
                if (!Sworn(t))
                {
                    End(t, Tie.State.Broken, PactWords.P("CC_Truce_Msg_OffByUs", PactWords.People(f).Named("PEOPLE")));
                    continue;
                }
                WorldLedger.Set(t, Tie.State.Broken);
                any = true;
                var form = PactWords.TruceForm(home, f);
                string text = PactWords.T("CC_Truce_BrokenByUs", PactWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), how.Named("HOW"));
                Tell.Letter(NewsKind.World, Importance.Important, home, PactWords.P("CC_Truce_BrokenByUs_Label", PactWords.People(f).Named("PEOPLE")), text,
                    LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
            }
            Dirty();
            Diplomacy.Dirty();
            if (!any) return;
            FactionMemory.Note(f, "brokeTruce", how);
            WordBroken(f);
        }

        /// <summary>Every people at peace with a town in reach hears the colony broke its word (the deed "wordBroken").</summary>
        public static void WordBroken(Faction about)
        {
            var heard = new HashSet<Faction>();
            foreach (var r in RegionTowns.Reach)
                foreach (var s in r.towns)
                {
                    var o = s?.Faction;
                    if (o == null || o == about || o.IsPlayer || Diplomacy.Hostile(o) || !Diplomacy.Deals(o) || !heard.Add(o)) continue;
                    FactionMemory.Note(o, "wordBroken", PactWords.People(about));
                }
        }

        /// <summary>Truces they broke lately (this session), for This week's row: "a truce broken".</summary>
        internal static readonly List<int> Broken = new List<int>();
    }
}
