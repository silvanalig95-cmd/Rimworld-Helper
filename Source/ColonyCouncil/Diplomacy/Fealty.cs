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
    /// Fealty to a stronger neighbour (docs/design/stage2-diplomacy.md, round c, item 9; decisions 9 and 11; release 2c.2;
    /// the rules in FealtyMath.cs). The colony swears to a people at peace with goodwill 40 or more and two towns or more
    /// within reach (never the Empire, whose titles and permits are its own): by a letter of the purpose Fealty to their
    /// head with their terms shown by their ways (FealtyLetters.cs), or when they offer their protection to a struggling
    /// colony (raided twice in a season with few fighters). Agreed, their lord and retinue come to the gate, a colonist who
    /// objects (one sworn to vengeance on them, the proud, a devout colonist of another faith) has their say before the
    /// council first (FealtyObjection.cs), and the colony's speaker swears by their rite (FealtyOath.cs).
    /// <para>What it brings: each season their collector comes to the gate and the tribute (150 to 600 silver's worth by
    /// the colony's wealth) is carried out to them; short, the rest is owed (the tie called in), and still owed at the next
    /// season's day the oath is broken (20 goodwill and the deed). When their town within reach is attacked their letter
    /// calls the colony's levies (CallsToArms.cs: two fighters set out within five days, the far fight). When a home is
    /// raided their band comes without a call, at most once a season, nine times in ten; a season in which they failed
    /// lets the colony leave once without blame. Renouncing is a council question, at the cost of their grudge (30
    /// goodwill and the deed). Their enemies stay the game's business: nothing here moves goodwill with anyone else
    /// (decision 11). The tie is the ledger's; the oath awaited is its home's part (DiplomacyBook).</para>
    /// </summary>
    public static class Fealty
    {
        public const string Origin = FealtyMath.Origin;

        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static float Day => Now / (float)FealtyMath.DayTicks;

        /// <summary>The setting "Fealty and vassals".</summary>
        public static bool On => (ColonyCouncilMod.Settings?.diplomacyFealty ?? true) && FactionMemory.On;

        /// <summary>A call of the colony's levies, or their band, under way on the oath.</summary>
        public static bool CallUnderWay(Tie t) => t != null && !t.terms.goods.NullOrEmpty();

        // ================= Reading =================

        public static List<Tie> Live()
        {
            var list = new List<Tie>();
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Fealty))
                if (t != null && t.Live && t.origin == Origin)
                    list.Add(t);
            return list;
        }

        public static Tie Get(int id)
        {
            var t = WorldLedger.Get(id);
            return t != null && t.kind == Tie.Kind.Fealty && t.origin == Origin ? t : null;
        }

        public static Faction LiegeOf(Tie t) => t == null ? null : Refs.Faction(t.to);

        public static Map HomeOf(Tie t) => t == null ? null : Diplomacy.HomeOf(t.from);

        /// <summary>The colony's oath at this home, or null.</summary>
        public static Tie Of(Map home)
        {
            if (home == null) return null;
            var colony = Diplomacy.Colony(home);
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Fealty))
                if (t != null && t.Live && t.origin == Origin && t.from == colony)
                    return t;
            return null;
        }

        /// <summary>The people this home is sworn to, or null.</summary>
        public static Faction Liege(Map home) => LiegeOf(Of(home));

        /// <summary>The oath with that people at any home, or null.</summary>
        public static Tie With(Faction f) => f == null ? null : Live().FirstOrDefault(t => LiegeOf(t) == f);

        public static bool Owed(Tie t) => t != null && t.state == Tie.State.CalledIn && t.terms.count > 0;

        /// <summary>They failed to protect the colony within the last season: it may leave without blame (until their band comes again, or the season is out).</summary>
        public static bool FailedSeason(Tie t) => t != null && FealtyMath.Failed(t.terms.key) && FealtyMath.ThisSeason(Mathf.RoundToInt(t.terms.days * FealtyMath.DayTicks), Now);

        /// <summary>Their towns within this home's reach.</summary>
        public static int TownsInReach(Faction f, Map home)
        {
            var reach = home != null ? RegionTowns.Of(home) : null;
            if (f == null || reach == null) return 0;
            int n = 0;
            foreach (var s in reach.towns)
                if (s != null && !s.Destroyed && s.Faction == f)
                    n++;
            return n;
        }

        /// <summary>This season's tribute for a home, by its wealth.</summary>
        public static int TributeValue(Map home)
        {
            float wealth = 0f;
            try
            {
                wealth = home?.wealthWatcher?.WealthTotal ?? 0f;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fealty: the colony's wealth", e);
            }
            return FealtyMath.Tribute(wealth);
        }

        private static int Goodwill(Faction f) => f != null && f.HasGoodwill ? f.PlayerGoodwill : 0;

        // ================= Swearing =================

        /// <summary>Why the colony can't swear fealty to them now, or null.</summary>
        public static string SwearWhyNot(Faction f, Map home)
        {
            if (!On) return FealtyWords.P("CC_Fealty_Why_Off");
            if (f == null || !Diplomacy.Deals(f)) return DipWords.P("CC_Dip_Why_Gone");
            if (Ages.IsEmpire(f)) return FealtyWords.P("CC_Fealty_Why_Empire", FealtyWords.People(f).Named("PEOPLE"));
            if (Diplomacy.Hostile(f)) return DipWords.P("CC_Dip_Why_War", FealtyWords.People(f).Named("PEOPLE"));
            var liege = Liege(home);
            if (liege == f) return FealtyWords.P("CC_Fealty_Why_Sworn", FealtyWords.People(f).Named("PEOPLE"));
            if (liege != null) return FealtyWords.P("CC_Fealty_Why_Other", FealtyWords.People(liege).Named("PEOPLE"));
            var h = DiplomacyBook.Instance?.Existing(home);
            if (h != null && h.oathPeople >= 0) return FealtyWords.P("CC_Fealty_Why_Awaited");
            if (Goodwill(f) < FealtyMath.SwearGoodwill) return FealtyWords.P("CC_Fealty_Why_Goodwill", FealtyWords.People(f).Named("PEOPLE"), FealtyMath.SwearGoodwill.Named("NEED"));
            if (TownsInReach(f, home) < FealtyMath.MinTowns) return FealtyWords.P("CC_Fealty_Why_Towns", FealtyWords.People(f).Named("PEOPLE"), FealtyMath.MinTowns.Named("NEED"));
            return null;
        }

        /// <summary>
        /// They hear the colony's letter (or call) asking to swear fealty: by their goodwill and their head's regard. Agreed,
        /// their lord and retinue come to the gate after the answer's way back. Returns their answer in words.
        /// </summary>
        public static string Hear(Faction f, Map home, float days, float roll, out bool yes)
        {
            yes = false;
            var form = FealtyWords.Form(home, f);
            if (SwearWhyNot(f, home) != null) return FealtyWords.T("CC_Fealty_Ans_Moot", FealtyWords.People(f).Named("PEOPLE"));
            if (!FealtyMath.Accepts(Goodwill(f), Pacts.HeadRegard(f, home), roll))
                return FealtyWords.T("CC_Fealty_Ans_No", FealtyWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"));
            yes = true;
            Agreed(f, home, days, false);
            return FealtyWords.T("CC_Fealty_Ans_Yes", FealtyWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"));
        }

        /// <summary>The oath agreed: their lord and retinue are due at the gate in so many days.</summary>
        public static void Agreed(Faction f, Map home, float days, bool offered)
        {
            var h = DiplomacyBook.Instance?.HomeOf(home);
            if (h == null || f == null) return;
            int now = Now;
            h.oathPeople = f.loadID;
            h.oathAt = now;
            h.oathDue = now + Mathf.RoundToInt(Mathf.Max(0.1f, days) * FealtyMath.DayTicks);
            h.oathOffered = offered;
            Diplomacy.Dirty();
        }

        /// <summary>The people whose oath this home awaits, or null.</summary>
        public static Faction Awaited(Map home)
        {
            var h = DiplomacyBook.Instance?.Existing(home);
            return h == null || h.oathPeople < 0 ? null : Refs.Faction(Ref.People(h.oathPeople));
        }

        private static void ClearOath(DiplomacyHome h)
        {
            h.oathPeople = -1;
            h.oathDue = -1;
            h.oathAt = -1;
            h.oathOffered = false;
            Diplomacy.Dirty();
        }

        /// <summary>The oath awaited, each hour: their retinue sent when due, the hour at the gate, or let go when it can't be.</summary>
        private static void OathHour(DiplomacyHome h, int now)
        {
            var home = h.Map;
            var f = h.oathPeople >= 0 ? Refs.Faction(Ref.People(h.oathPeople)) : null;
            if (home == null || f == null || f.defeated || Diplomacy.Hostile(f) || Of(home) != null)
            {
                FealtyOath.LordOf(home)?.ReceiveMemo(LordJob_FealtyOath.MemoLeave);
                ClearOath(h);
                return;
            }
            var lord = FealtyOath.LordOf(home);
            if (lord != null)
            {
                FealtyOath.Hour(f, lord, home);
                return;
            }
            if (now < h.oathDue) return;
            if (now - h.oathDue > FealtyMath.OathGiveUpDays * FealtyMath.DayTicks)
            {
                ClearOath(h);
                // apart: their word, told in a message; nobody came to the gate.
                Tell.Message(NewsKind.World, Importance.Notable, home, FealtyWords.P("CC_Fealty_Msg_Lapsed", FealtyWords.People(f).Named("PEOPLE")), LookTargets.Invalid,
                    MessageTypeDefOf.NeutralEvent);
                return;
            }
            if (MapFacts.ThreatOn(home)) return;
            int hour = GenLocalDate.HourOfDay(home);
            if (hour < 7 || hour > 16) return;
            FealtyOath.Send(f, home);
        }

        /// <summary>The retinue went home before the oath was sworn (the wait was long, or the council sent them away).</summary>
        internal static void RetinueGone(Map home, Faction f, bool sworn)
        {
            var h = DiplomacyBook.Instance?.Existing(home);
            if (h == null || sworn || h.oathPeople != (f?.loadID ?? -2)) return;
            ClearOath(h);
        }

        /// <summary>
        /// Sworn at the gate: the tie (from the colony to them, the first tribute a season on), their people remember the
        /// oath, the history writes it (the ledger's tie:fealty:made, a milestone), and the letter tells what it brings.
        /// </summary>
        public static Tie Swear(Faction f, Map home, Pawn speaker, Pawn lord)
        {
            if (f == null || home == null) return null;
            var h = DiplomacyBook.Instance?.HomeOf(home);
            if (h != null) ClearOath(h);
            if (Of(home) != null) return null;
            // A scenario's grant of land from them (ScenPart_CouncilBeholden) is sworn anew: the oath takes its place.
            var colony = Diplomacy.Colony(home);
            var people = Refs.Of(f);
            foreach (var old in WorldLedger.OwnTies(Tie.Kind.Fealty).ToList())
                if (old != null && old.Live && old.origin == ScenPart_CouncilBeholden.Origin && old.from == colony && old.to == people)
                    WorldLedger.Set(old, Tie.State.Ended);
            var terms = new TieTerms { key = FealtyMath.LiegeKey, number = Day + FealtyMath.SeasonDays };
            var t = WorldLedger.AddTie(Tie.Kind.Fealty, colony, people, terms, Tie.Never, Origin);
            if (t == null) return null;
            var form = FealtyWords.Form(home, f);
            FactionMemory.Note(f, "swore", form.word);
            // The one who swore it at the gate takes it to heart, if proud (Nature.cs: the heart's strain, and maybe a mark).
            try
            {
                if (speaker != null) Nature.Felt(speaker, NatureAct.SworeFealty, f);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fealty: the oath's weight", e);
            }
            string text = FealtyWords.T("CC_Fealty_Sworn", (speaker?.LabelShort ?? "?").Named("PAWN"), (lord?.LabelShort ?? FealtyWords.People(f)).Named("LORD"),
                              FealtyWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"))
                          + "\n\n" + FealtyWords.Terms(f, home);
            Tell.Letter(NewsKind.World, Importance.Important, home, FealtyWords.P("CC_Fealty_Sworn_Label", FealtyWords.People(f).Named("PEOPLE")), text, LetterDefOf.PositiveEvent,
                speaker != null && speaker.Spawned ? new LookTargets(speaker) : LookTargets.Invalid, f);
            Diplomacy.Dirty();
            return t;
        }

        // ================= Their offer of protection =================

        /// <summary>The raids on this home in the last season (the memory's raids, each counted).</summary>
        public static int RaidsThisSeason(Map home)
        {
            var mem = FactionMemory.Instance;
            if (mem == null) return 0;
            int now = Now, n = 0;
            foreach (var d in mem.deeds)
                if (d != null && d.kind == "raided" && now - d.tick < FealtyMath.SeasonDays * FealtyMath.DayTicks)
                    n += Math.Max(1, d.count);
            return n;
        }

        /// <summary>Grown colonists able to fight at a home.</summary>
        public static int Fighters(Map home) =>
            home == null ? 0 : IncidentWorker_Happening.Colonists(home).Count(p => !p.DevelopmentalStage.Juvenile() && !p.WorkTagIsDisabled(WorkTags.Violent));

        /// <summary>The day's look: a struggling colony may be offered protection (once a year a home); the vassals' day.</summary>
        public static void Daily(int now)
        {
            Vassals.Daily(now);
            if (!On || !Tell.CanAsk) return;
            foreach (var home in Homes.All.ToList())
            {
                var h = DiplomacyBook.Instance?.HomeOf(home);
                if (h == null || Of(home) != null || h.oathPeople >= 0) continue;
                if (!FealtyMath.Struggling(RaidsThisSeason(home), Fighters(home)) || !Rand.Chance(FealtyMath.OfferDaily)) continue;
                var reach = RegionTowns.Of(home);
                if (reach == null) continue;
                var best = reach.towns.Where(s => s?.Faction != null && !s.Faction.IsPlayer).Select(s => s.Faction).Distinct()
                    .Where(f => Diplomacy.Deals(f) && FealtyMath.MayOffer(Goodwill(f), Diplomacy.Hostile(f), Ages.IsEmpire(f), TownsInReach(f, home), false, h.fealtyOfferAt, now))
                    .OrderByDescending(Goodwill).FirstOrDefault();
                if (best == null) continue;
                h.fealtyOfferAt = now;
                Offer(best, home);
                return;
            }
        }

        /// <summary>Their offer, as a letter: their protection for the colony's oath, its terms by their ways: accept, or let it be.</summary>
        public static void Offer(Faction f, Map home)
        {
            var def = FealtyDefOf.Rimfolk_FealtyOffer;
            if (def == null || f == null || home == null) return;
            var head = FavourCalls.HeadOf(f, home);
            var form = FealtyWords.Form(home, f);
            string who = head != null ? NotableBook.Called(head) : FealtyWords.People(f);
            string text = FealtyWords.T("CC_Fealty_Offer", who.Named("WHO"), FealtyWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), form.rite.Named("RITE"))
                          + "\n\n" + FealtyWords.Terms(f, home)
                          + "\n\n" + FealtyWords.Risks(f, home);
            var town = head != null ? NotableBook.TownOf(head.town) : WorldSituationWorker.NearestTown(f, home);
            var letter = LetterMaker.MakeLetter(FealtyWords.P("CC_Fealty_Offer_Label", FealtyWords.People(f).Named("PEOPLE")), text, def,
                town != null ? new LookTargets(town) : LookTargets.Invalid, f);
            if (letter is ChoiceLetter_FealtyOffer l)
            {
                l.people = f;
                l.home = Homes.KeyOf(home);
            }
            Tell.Ask(letter, home);
        }

        // ================= The hour =================

        /// <summary>The hour (Diplomacy.Hourly): oaths awaited at each home; each oath's end, tribute, protection, their band and the levies.</summary>
        public static void Hourly(int now)
        {
            var book = DiplomacyBook.Instance;
            if (book != null)
                foreach (var h in book.Live())
                {
                    if (h.oathPeople < 0) continue;
                    try
                    {
                        OathHour(h, now);
                    }
                    catch (Exception e)
                    {
                        CouncilLog.ErrorOnce("fealty: the oath awaited", e);
                    }
                }
            float day = now / (float)FealtyMath.DayTicks;
            foreach (var t in Live())
            {
                try
                {
                    var f = LiegeOf(t);
                    var home = HomeOf(t);
                    if (f == null || f.defeated || home == null)
                    {
                        End(t, Tie.State.Ended, home != null && f != null ? FealtyWords.P("CC_Fealty_Msg_Gone", FealtyWords.People(f).Named("PEOPLE")) : null);
                        continue;
                    }
                    if (Diplomacy.Hostile(f))
                    {
                        End(t, Tie.State.Ended, FealtyWords.P("CC_Fealty_Msg_War", FealtyWords.People(f).Named("PEOPLE")));
                        continue;
                    }
                    if (BandHour(t, f, home, day)) continue;
                    if (!PactMath.ParseArms(t.terms.goods, out _, out _))
                    {
                        ProtectionHour(t, f, home, day);
                        // Their band set out this hour: nothing else on the oath until it is home.
                        if (PactMath.ParseBand(t.terms.goods, out _)) continue;
                    }
                    CallsToArms.PactHour(t, f, home, day);
                    if (t.Live) TributeHour(t, f, home, day);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("fealty: the hour", e);
                }
            }
            Vassals.Hourly(now);
        }

        // ================= Protection =================

        /// <summary>Raids this session their band was looked for already (home and attacker), so one raid is one roll.</summary>
        private static readonly HashSet<string> looked = new HashSet<string>();

        /// <summary>
        /// A home raided by a people (not wild animals): their band comes without a call, at most once a season, nine times
        /// in ten and never against their friends; else they failed it this season, which lets the colony leave once
        /// without blame.
        /// </summary>
        private static void ProtectionHour(Tie t, Faction f, Map home, float day)
        {
            if (!On || !MapFacts.ThreatOn(home) || CampTracker.WarBandAt(home) != null) return;
            var enemy = WarCamps.Attacker(home);
            if (enemy == null || enemy == f || enemy.def == null || !enemy.def.humanlikeFaction) return;
            if (!FealtyMath.MayProtect(t.terms.days, day)) return;
            string key = t.id + ":" + Homes.KeyOf(home) + ":" + enemy.loadID + ":" + (int)day;
            if (!looked.Add(key)) return;
            var form = FealtyWords.Form(home, f);
            bool friend = !enemy.HostileTo(f);
            if (FealtyMath.Protects(friend, Rand.Value))
            {
                float hours = PactMath.BandHours(TilesTo(f, home));
                t.terms.goods = PactMath.BandCode(day + hours / 24f);
                t.terms.about = Refs.Of(enemy);
                t.terms.days = day;
                t.terms.key = FealtyMath.LiegeKey;
                WorldLedger.Touch(t);
                // apart: their word that their band is on its way, told in a message; the band walks in later, on the map.
                Tell.Message(NewsKind.World, Importance.Important, home, FealtyWords.T("CC_Fealty_Protect_Coming", FealtyWords.People(f).Named("PEOPLE"),
                    FealtyWords.People(enemy).Named("ENEMY"), form.word.Named("WORD")), LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                t.terms.key = FealtyMath.FailedKey;
                t.terms.days = day;
                WorldLedger.Touch(t);
                Tell.Letter(NewsKind.World, Importance.Important, home, FealtyWords.P("CC_Fealty_Failed_Label", FealtyWords.People(f).Named("PEOPLE")),
                    FealtyWords.T(friend ? "CC_Fealty_Failed_Friend" : "CC_Fealty_Failed", FealtyWords.People(f).Named("PEOPLE"), FealtyWords.People(enemy).Named("ENEMY"),
                        form.word.Named("WORD")), LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
            }
            Diplomacy.Dirty();
        }

        /// <summary>Their band on its way: walks in when due (Pacts.Arrive, with the oath's words), or turns back with the fight over. True while it is under way.</summary>
        private static bool BandHour(Tie t, Faction f, Map home, float day)
        {
            if (!PactMath.ParseBand(t.terms.goods, out float due)) return false;
            if (day < due) return true;
            if (Pacts.Arrive(f, home, Refs.Faction(t.terms.about), "fealty", out bool wait, "fealty") || !wait)
            {
                // Walked in to fight (not turned back with the fight over): their people remember they protected the colony.
                if (!wait && CampTracker.WarBandAt(home)?.faction == f) FactionMemory.Note(f, "protected", FealtyWords.People(Refs.Faction(t.terms.about)));
                t.terms.goods = null;
                t.terms.about = Ref.None;
                WorldLedger.Touch(t);
                Diplomacy.Dirty();
            }
            return true;
        }

        private static float TilesTo(Faction f, Map home)
        {
            var town = WorldSituationWorker.NearestTown(f, home);
            return town != null ? WorldSituationWorker.Distance(home, town) : 6f;
        }

        // ================= Tribute =================

        /// <summary>
        /// The season's day: tribute still owed from the last breaks the oath; else their collector comes to the gate by day
        /// and the tribute is carried out to them (CollectTribute), the rest owed when the stores can't cover it.
        /// </summary>
        private static void TributeHour(Tie t, Faction f, Map home, float day)
        {
            if (!FealtyMath.TributeDue(t.terms.number, day) || CallUnderWay(t)) return;
            if (MapFacts.ThreatOn(home) || !FealtyMath.CollectHour(GenLocalDate.HourOfDay(home))) return;
            var form = FealtyWords.Form(home, f);
            if (FealtyMath.BreaksOath(Owed(t)))
            {
                BrokenByTribute(t, f, home);
                return;
            }
            int value = TributeValue(home);
            t.terms.number = day + FealtyMath.SeasonDays;
            string carried = CollectTribute(home, f, value, 2, out int paid);
            int owed = FealtyMath.Owed(value, paid);
            var actors = new List<HistoryActor> { new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.For) };
            if (owed > 0)
            {
                t.terms.count = owed;
                WorldLedger.Touch(t);
                WorldLedger.Set(t, Tie.State.CalledIn);
                FactionMemory.Note(f, "tributeShort", owed.ToString());
                Diplomacy.Write(DiplomacyMath.Tribute, actors, Diplomacy.Colony(home), FealtyWords.P("CC_Fealty_Hist_Short", FealtyWords.People(f).Named("PEOPLE"), owed.Named("OWED")), true);
                Tell.Letter(NewsKind.World, Importance.Important, home, FealtyWords.P("CC_Fealty_Short_Label", FealtyWords.People(f).Named("PEOPLE")),
                    FealtyWords.T("CC_Fealty_Short", FealtyWords.People(f).Named("PEOPLE"), value.Named("VALUE"), paid.Named("PAID"), owed.Named("OWED"), form.word.Named("WORD"))
                    + (carried.NullOrEmpty() ? "" : "\n\n" + carried), LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
            }
            else
            {
                WorldLedger.Touch(t);
                FactionMemory.Note(f, "tribute", value.ToString());
                Diplomacy.Write(DiplomacyMath.Tribute, actors, Diplomacy.Colony(home), FealtyWords.P("CC_Fealty_Hist_Paid", FealtyWords.People(f).Named("PEOPLE"), value.Named("VALUE")));
                Tell.Minor(NewsKind.World, Importance.Notable, home, FealtyWords.T("CC_Fealty_Paid", FealtyWords.People(f).Named("PEOPLE"), carried.Named("GOODS"), form.word.Named("WORD")),
                    LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            }
            Diplomacy.Dirty();
        }

        /// <summary>
        /// The tribute carried out to their collector at the gate (the play-out's errand: someone takes it from the stores
        /// and carries it out; with nobody to carry it, it leaves the stores now): silver when the colony holds enough, else
        /// the most plentiful goods worth it, of so many kinds at most. Returns the goods in words and what they are worth.
        /// </summary>
        public static string CollectTribute(Map home, Faction f, int value, int kinds, out int paid)
        {
            paid = 0;
            var parts = new List<string>();
            try
            {
                int silver = PlayCarry.Have(home, "silver", ThingDefOf.Silver);
                if (silver >= value)
                {
                    Carry(home, f, "silver", ThingDefOf.Silver, value);
                    paid = value;
                    return Couriers.Amount(value, "silver", ThingDefOf.Silver);
                }
                int left = value;
                foreach (var (def, unit, have) in Goods(home))
                {
                    if (left <= 0 || parts.Count >= kinds) break;
                    int n = Mathf.Min(have, Mathf.CeilToInt(left / unit));
                    if (n <= 0) continue;
                    Carry(home, f, def.defName, def, n);
                    int worth = Mathf.RoundToInt(n * unit);
                    paid += worth;
                    left -= worth;
                    parts.Add(Couriers.Amount(n, null, def));
                }
                // Whatever silver there is makes up the rest.
                if (left > 0 && silver > 0)
                {
                    int s = Mathf.Min(silver, left);
                    Carry(home, f, "silver", ThingDefOf.Silver, s);
                    paid += s;
                    parts.Add(Couriers.Amount(s, "silver", ThingDefOf.Silver));
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fealty: the tribute carried out", e);
            }
            paid = Mathf.Min(paid, value);
            return TextUtil.JoinAnd(parts);
        }

        /// <summary>Goods the tribute can be paid in: the colony's most plentiful tradeable goods (not silver), their worth each and how many it holds.</summary>
        private static List<(ThingDef def, float unit, int have)> Goods(Map home)
        {
            var list = new List<(ThingDef, float, int)>();
            if (home?.resourceCounter == null) return list;
            foreach (var kv in home.resourceCounter.AllCountedAmounts.OrderByDescending(kv => kv.Value * (kv.Key?.BaseMarketValue ?? 0f)))
            {
                var d = kv.Key;
                if (d == null || kv.Value <= 0 || d == ThingDefOf.Silver || d.category != ThingCategory.Item || d.stackLimit <= 1 || d.IsCorpse) continue;
                if (d.tradeability != Tradeability.All && d.tradeability != Tradeability.Sellable) continue;
                if (d.BaseMarketValue < 0.3f || d.BaseMarketValue > 60f) continue;
                int have = PlayCarry.Have(home, d.defName, d);
                if (have <= 0) continue;
                list.Add((d, d.BaseMarketValue, have));
                if (list.Count >= 6) break;
            }
            return list;
        }

        private static void Carry(Map home, Faction f, string what, ThingDef def, int count)
        {
            var ctx = new DilemmaContext { map = home, faction = f };
            // apart: one of the colony carries the tribute out to the gate, where their collector waits for it.
            new DilemmaEffect_CarryOut
            {
                what = what, count = count, steward = false, who = PlayWho.None, to = PlaySpot.Gate, label = FealtyWords.P("CC_Fealty_CartLabel", FealtyWords.People(f).Named("PEOPLE"))
            }.Apply(ctx);
        }

        /// <summary>Why what is owed can't be sent now, or null.</summary>
        public static string PayWhyNot(Tie t)
        {
            if (t == null || !t.Live) return FealtyWords.P("CC_Fealty_Why_Gone");
            if (!Owed(t)) return FealtyWords.P("CC_Fealty_Why_NothingOwed");
            var home = HomeOf(t);
            int worth = PlayCarry.Have(home, "silver", ThingDefOf.Silver) + Goods(home).Sum(g => Mathf.RoundToInt(g.unit * g.have));
            if (worth < t.terms.count) return FealtyWords.P("CC_Fealty_Why_CantPay", t.terms.count.Named("OWED"));
            return null;
        }

        /// <summary>What is owed sent now: carried out to their collector at the gate (or by the quartermaster's cart); the oath stands clear again.</summary>
        public static bool PayOwed(Tie t, Pawn by = null)
        {
            if (PayWhyNot(t) != null) return false;
            var f = LiegeOf(t);
            var home = HomeOf(t);
            int owed = t.terms.count;
            string goods = CollectTribute(home, f, owed, 4, out int paid);
            Paid(t, paid);
            // apart: the council's choice, told in a message; the goods are carried out to the gate on the map.
            Tell.Message(NewsKind.World, Importance.Notable, home, FealtyWords.P(Owed(t) ? "CC_Fealty_Msg_PaidPart" : "CC_Fealty_Msg_PaidOwed", FealtyWords.People(f).Named("PEOPLE"),
                goods.Named("GOODS"), t.terms.count.Named("OWED")), by != null && by.Spawned ? new LookTargets(by) : LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
            return true;
        }

        /// <summary>So much of what is owed paid: the rest still owed, or the oath clear again (Open) and their people remember the tribute.</summary>
        public static void Paid(Tie t, int amount)
        {
            if (t == null || !t.Live || amount <= 0) return;
            var f = LiegeOf(t);
            t.terms.count = Mathf.Max(0, t.terms.count - amount);
            WorldLedger.Touch(t);
            if (t.terms.count <= 0 && t.state == Tie.State.CalledIn)
            {
                WorldLedger.Set(t, Tie.State.Open);
                if (f != null) FactionMemory.Note(f, "tribute", amount.ToString());
                var home = HomeOf(t);
                Diplomacy.Write(DiplomacyMath.Tribute, new List<HistoryActor> { new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.For) },
                    Diplomacy.Colony(home), FealtyWords.P("CC_Fealty_Hist_PaidOwed", FealtyWords.People(f).Named("PEOPLE")));
            }
            Diplomacy.Dirty();
        }

        /// <summary>Still owed at the next season's day: the oath is broken, 20 goodwill and the tribute short remembered.</summary>
        private static void BrokenByTribute(Tie t, Faction f, Map home)
        {
            int owed = t.terms.count;
            WorldLedger.Set(t, Tie.State.Broken);
            FactionMemory.Note(f, "tributeShort", owed.ToString());
            Goodwill(f, -FealtyMath.BrokenGoodwill);
            var form = FealtyWords.Form(home, f);
            Tell.Letter(NewsKind.World, Importance.Important, home, FealtyWords.P("CC_Fealty_Broken_Label", FealtyWords.People(f).Named("PEOPLE")),
                FealtyWords.T("CC_Fealty_BrokenTribute", FealtyWords.People(f).Named("PEOPLE"), owed.Named("OWED"), form.word.Named("WORD"), FealtyMath.BrokenGoodwill.Named("GOODWILL")),
                LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
            Diplomacy.Dirty();
        }

        private static void Goodwill(Faction f, int amount)
        {
            if (f == null || amount == 0) return;
            try
            {
                f.TryAffectGoodwillWith(Faction.OfPlayer, amount, false, false);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("fealty: goodwill", e);
            }
        }

        // ================= Levies =================

        /// <summary>The colony's levies came to their town (CallsToArms.Arrived): their people remember, and the history writes it.</summary>
        public static void LeviesCame(Tie t, Faction f, Map home, Settlement town, Faction enemy)
        {
            if (f == null) return;
            FactionMemory.Note(f, "levies", town?.Name);
            var actors = new List<HistoryActor> { new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.For) };
            if (enemy != null) actors.Add(new HistoryActor(Refs.Of(enemy), HistoryRole.Against));
            Diplomacy.Write(DiplomacyMath.Levy, actors, town != null ? Refs.Of(town) : Diplomacy.Colony(home),
                FealtyWords.P("CC_Fealty_Hist_Levies", FealtyWords.People(f).Named("PEOPLE"), (town?.Name ?? "?").Named("TOWN")), true);
        }

        /// <summary>Levies refused, or nobody set out in time: 10 goodwill, and their people remember it. The oath stands.</summary>
        public static void LeviesRefused(Tie t, Faction f, Map home, bool unanswered)
        {
            if (t == null || f == null) return;
            t.terms.goods = null;
            t.terms.about = Ref.None;
            WorldLedger.Touch(t);
            FactionMemory.Note(f, "refused", FealtyWords.LevyForm(home, f).word);
            Goodwill(f, -FealtyMath.LeviesRefusedGoodwill);
            var actors = new List<HistoryActor> { new HistoryActor(Diplomacy.Colony(home), HistoryRole.Did), new HistoryActor(Refs.Of(f), HistoryRole.Against) };
            Diplomacy.Write(DiplomacyMath.Levy, actors, Diplomacy.Colony(home), FealtyWords.P("CC_Fealty_Hist_LeviesRefused", FealtyWords.People(f).Named("PEOPLE")));
            // apart: the colony's answer (or its silence), told in a message.
            Tell.Message(NewsKind.World, Importance.Important, home, FealtyWords.P(unanswered ? "CC_Fealty_Msg_LeviesLate" : "CC_Fealty_Msg_LeviesRefused", FealtyWords.People(f).Named("PEOPLE"),
                FealtyMath.LeviesRefusedGoodwill.Named("GOODWILL")), LookTargets.Invalid, MessageTypeDefOf.NegativeEvent);
            Diplomacy.Dirty();
        }

        // ================= Renouncing =================

        /// <summary>Why the colony can't renounce the oath now, or null.</summary>
        public static string RenounceWhyNot(Tie t)
        {
            if (t == null || !t.Live || t.origin != Origin) return FealtyWords.P("CC_Fealty_Why_Gone");
            if (CallUnderWay(t)) return FealtyWords.P("CC_Fealty_Why_CallUnderWay");
            if (FealtyObjection.RenounceOpen(t.id)) return FealtyWords.P("CC_Fealty_Why_Asked");
            if (FealtyObjection.CouncilBusy(HomeOf(t))) return FealtyWords.P("CC_Fealty_Why_CouncilBusy");
            return null;
        }

        /// <summary>The council's answer: renounced. Ended; their grudge (30 goodwill and the deed), unless they failed to protect the colony in a season.</summary>
        public static void Renounce(Tie t)
        {
            var f = LiegeOf(t);
            var home = HomeOf(t);
            if (t == null || !t.Live || f == null) return;
            bool clean = FailedSeason(t);
            int cost = FealtyMath.RenounceCost(clean);
            WorldLedger.Set(t, Tie.State.Ended);
            if (!clean)
            {
                FactionMemory.Note(f, "renounced", FealtyWords.Form(home, f).word);
                Goodwill(f, -cost);
            }
            var form = FealtyWords.Form(home, f);
            Tell.Letter(NewsKind.World, Importance.Important, home, FealtyWords.P("CC_Fealty_Renounced_Label", FealtyWords.People(f).Named("PEOPLE")),
                FealtyWords.T(clean ? "CC_Fealty_Renounced_Clean" : "CC_Fealty_Renounced", FealtyWords.People(f).Named("PEOPLE"), form.word.Named("WORD"), cost.Named("GOODWILL")),
                clean ? LetterDefOf.NeutralEvent : LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
            Diplomacy.Dirty();
        }

        // ================= Ends =================

        public static void End(Tie t, Tie.State state, string message)
        {
            if (t == null || !t.Live) return;
            var home = HomeOf(t);
            WorldLedger.Set(t, state);
            Diplomacy.Dirty();
            if (!message.NullOrEmpty())
                // apart: what became of the oath, told in a message.
                Tell.Message(NewsKind.World, Importance.Notable, home, message, LookTargets.Invalid, MessageTypeDefOf.NeutralEvent);
        }

        /// <summary>The colony attacked its liege (a deed of its against them, PactsHooks): the oath is broken, and its word broken in every ear in reach.</summary>
        public static bool BrokenByUs(Faction f, string how)
        {
            // Every home sworn to them (one liege a home, so two homes may share one): the colony's people are one people to them.
            var oaths = f == null ? new List<Tie>() : Live().Where(x => LiegeOf(x) == f).ToList();
            if (oaths.Count == 0) return false;
            foreach (var t in oaths)
            {
                var home = HomeOf(t);
                WorldLedger.Set(t, Tie.State.Broken);
                Tell.Letter(NewsKind.World, Importance.Important, home, FealtyWords.P("CC_Fealty_Broken_Label", FealtyWords.People(f).Named("PEOPLE")),
                    FealtyWords.T("CC_Fealty_BrokenByUs", FealtyWords.People(f).Named("PEOPLE"), how.Named("HOW"), FealtyWords.Form(home, f).word.Named("WORD")), LetterDefOf.NegativeEvent,
                    LookTargets.Invalid, f);
            }
            FactionMemory.Note(f, "renounced", how);
            Truces.WordBroken(f);
            Diplomacy.Dirty();
            return true;
        }

        /// <summary>A home lost: its oath awaited is let go (the tie ends in the ledger's own upkeep with the home).</summary>
        internal static void HomeLost(DiplomacyHome h)
        {
            if (h == null) return;
            h.oathPeople = -1;
            h.oathDue = -1;
            foreach (var t in Live())
                if (t.from == Ref.Colony(h.Home))
                    WorldLedger.Set(t, Tie.State.Ended);
        }

        // ================= The Empire's own fealty (decision 9) =================

        /// <summary>A colonist holding one of the Empire's titles: sworn to it by its own ways, its permits their protection (a view only).</summary>
        public static List<(Pawn pawn, RoyalTitleView title)> Titled(Map home)
        {
            var list = new List<(Pawn, RoyalTitleView)>();
            if (home == null || !ModsConfig.RoyaltyActive) return list;
            foreach (var p in IncidentWorker_Happening.Colonists(home))
            {
                var title = WorldMechanics.Title(p);
                if (title != null && Ages.IsEmpire(title.people)) list.Add((p, title));
            }
            return list;
        }
    }

    /// <summary>Their offer of protection for the colony's oath, as a letter: accept (their lord comes to the gate), or let it be.</summary>
    public class ChoiceLetter_FealtyOffer : ChoiceLetter
    {
        public Faction people;
        public int home = HomesMath.None;

        private bool Valid(out Map map)
        {
            map = Homes.MapOf(home) ?? Homes.Seat;
            return people != null && !people.defeated && !Diplomacy.Hostile(people) && map != null && Fealty.Of(map) == null && Fealty.Awaited(map) == null && Fealty.On;
        }

        /// <summary>Still waiting for the colony's answer (the inbox's row).</summary>
        public bool Waiting => !ArchivedOnly && Valid(out _);

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly || !Valid(out _))
                {
                    yield return Option_Close;
                    yield break;
                }
                yield return new DiaOption(FealtyWords.P("CC_Fealty_Opt_Accept"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        if (!Valid(out var map)) return;
                        Fealty.Agreed(people, map, Rand.Range(FealtyMath.OathHoursMin, FealtyMath.OathHoursMax) / 24f, true);
                        // apart: the colony's answer to their letter; their lord comes to the gate later.
                        Tell.Message(NewsKind.World, Importance.Notable, map, FealtyWords.P("CC_Fealty_Msg_Accepted", FealtyWords.People(people).Named("PEOPLE")), LookTargets.Invalid,
                            MessageTypeDefOf.PositiveEvent);
                    },
                    resolveTree = true
                };
                yield return new DiaOption(FealtyWords.P("CC_Fealty_Opt_Decline"))
                {
                    action = () => Find.LetterStack.RemoveLetter(this),
                    resolveTree = true
                };
                yield return Option_Postpone;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref people, "people");
            Scribe_Values.Look(ref home, "home", HomesMath.None);
        }
    }

    [DefOf]
    public static class FealtyDefOf
    {
        public static LetterDef Rimfolk_FealtyOffer;
        public static LetterDef Rimfolk_VassalAsk;
        public static JobDef Rimfolk_FealtyOath;

        static FealtyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FealtyDefOf));
        }
    }
}
