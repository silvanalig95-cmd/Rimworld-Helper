using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What became of one of a marriage's spouses as they died (Patch_MatchKilled): by whose hand, where, tended or not.</summary>
    public sealed class MatchDeath
    {
        public MatchWrong wrong;
        public bool open, untended;
        public Map map;
    }

    // A marriage alliance while it holds, and how it ends (Matches.cs has the whole of it).
    public static partial class Matches
    {
        // ================= Their regard =================

        private static int bonusVersion = -1;
        private static readonly HashSet<int> allied = new HashSet<int>();

        /// <summary>What a marriage of Rimfolk's with them adds to their notables' regard of the colony (MatchMath.RegardBonus; never for Diplomatic Marriage+'s).</summary>
        public static int RegardBonus(Faction f)
        {
            if (f == null || WorldLedger.Instance == null) return 0;
            if (bonusVersion != WorldLedger.Version)
            {
                bonusVersion = WorldLedger.Version;
                allied.Clear();
                foreach (var t in Live())
                {
                    var p = PeopleOf(t);
                    if (p != null) allied.Add(p.loadID);
                }
            }
            return allied.Contains(f.loadID) ? MatchMath.RegardBonus : 0;
        }

        // ================= The day =================

        /// <summary>The day (Diplomacy.Hourly's first hour): their offers; each marriage's day; an older save's grudges made their people's.</summary>
        public static void Daily(int now)
        {
            try
            {
                Offers(now);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: their offers", e);
            }
            foreach (var t in Live())
            {
                try
                {
                    Alliance(t, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("matches: an alliance's day", e);
                }
            }
            try
            {
                PeoplesGrudges();
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: an older save's grudge", e);
            }
        }

        /// <summary>
        /// One marriage's day: a spouse's death (the alliance ends with them: it is a bond between the colony and their people
        /// made through one marriage, and a child of the two keeps none since 5 November 2026), a wrong done to the one living
        /// at the colony or the colony's own held by it, the couple parted; while at peace, their town's gift once a season,
        /// their letter about a raiders' camp set against the home, and the alliance's defence pact kept.
        /// </summary>
        private static void Alliance(Tie t, int now)
        {
            var f = PeopleOf(t);
            var home = HomeOf(t);
            if (f == null || f.defeated)
            {
                End(t, null, null);
                return;
            }
            var ours = OursOf(t);
            var theirs = TheirsOf(t);
            bool oursDead = ours == null || ours.Dead, theirsDead = theirs == null || theirs.Dead;
            if (oursDead || theirsDead)
            {
                // An older save's alliance held through a child (terms.count) ends without a word: its death was told then.
                if (t.terms.count > 0)
                {
                    End(t, null, null);
                    return;
                }
                End(t, MatchWords.P("CC_Match_End_Death_Label", MatchWords.People(f).Named("PEOPLE")),
                    MatchWords.T("CC_Match_End_Death", MatchWords.Name(oursDead ? ours : theirs, NameOf(t, oursDead)).Named("DEAD"), MatchWords.People(f).Named("PEOPLE")));
                return;
            }
            else
            {
                var wrong = WrongNow(t, ours, theirs, f);
                if (MatchMath.Wronged(wrong))
                {
                    Wronged(t, wrong, LivesOf(t) == MatchHome.Colony ? theirs : ours);
                    return;
                }
                if (Departed(t, ours, theirs, f))
                {
                    End(t, MatchWords.P("CC_Match_End_Home_Label", MatchWords.People(f).Named("PEOPLE")),
                        MatchWords.T("CC_Match_End_Home", theirs.Named("THEIRS"), MatchWords.People(f).Named("PEOPLE")));
                    return;
                }
                if (!ours.relations.DirectRelationExists(PawnRelationDefOf.Spouse, theirs))
                {
                    Parted(t, ours, theirs, f, home);
                    return;
                }
            }
            // The two living at their town kept in the world while it holds (the ledger's reason Spouse).
            if (LivesOf(t) == MatchHome.Theirs)
                foreach (var x in new[] { ours, theirs })
                    if (x != null && !x.Dead && !x.Spawned && !WorldLedger.KeptFor(x, KeepFor.Spouse))
                        WorldLedger.Keep(x, KeepFor.Spouse);
            if (home == null || Diplomacy.Hostile(f)) return;
            float day = now / (float)MatchMath.DayTicks;
            if (On && MatchMath.TownGiftDue(t.terms.number, t.since / (float)MatchMath.DayTicks, day)) TownGift(t, f, home, ours, theirs, day);
            if (On) Warn(f, home, now);
            KeepPact(f, home, day);
        }

        private static string NameOf(Tie t, bool ours) => DipWords.NameOf(ours ? t.terms.about : MatchMath.ParseSpouse(t.terms.key, out string r) ? Ref.Parse(r) : Ref.None);

        /// <summary>
        /// A wrong done now (the daily look; a death by the colony's hand is caught as it happens): theirs at the colony held as
        /// a prisoner or a slave, sold away or sent away; the colony's own married to them, held in its cells.
        /// </summary>
        private static MatchWrong WrongNow(Tie t, Pawn ours, Pawn theirs, Faction f)
        {
            if (LivesOf(t) == MatchHome.Colony)
            {
                bool other = theirs.Faction != null && theirs.Faction != Faction.OfPlayer && theirs.Faction != f;
                bool sold = MatchMath.SoldAway(other, theirs.IsPrisoner, other && theirs.Faction.HostileTo(Faction.OfPlayer));
                bool banished = theirs.Faction == null && !theirs.IsPrisoner;
                return MatchMath.WrongOf(false, false, theirs.IsPrisonerOfColony, theirs.IsSlaveOfColony, sold, banished);
            }
            return MatchMath.WrongOf(false, false, ours.IsPrisonerOfColony, ours.IsSlaveOfColony, false, false);
        }

        /// <summary>Theirs, married into the colony, gone back to their own people of their own accord (not the colony's doing).</summary>
        private static bool Departed(Tie t, Pawn ours, Pawn theirs, Faction f) => LivesOf(t) == MatchHome.Colony && theirs.Faction == f && !theirs.IsPrisoner;

        /// <summary>Their town's small gift once a season, by their ways, carried in by their courier.</summary>
        private static void TownGift(Tie t, Faction f, Map home, Pawn ours, Pawn theirs, float day)
        {
            t.terms.number = day;
            WorldLedger.Touch(t);
            var town = WorldSituationWorker.NearestTown(f, home);
            int value = MatchMath.TownGiftValue(Fortune(town?.ID ?? -1));
            var def = GiftDef(f, home);
            var things = Couriers.Make(def, MatchMath.GiftCount(value, def.BaseMarketValue));
            var gate = PlaySpots.GateCell(home, null);
            if (!Couriers.Deliver(home, things, f, gate)) Loot.Deliver(things, home, gate, false);
            bool away = LivesOf(t) == MatchHome.Theirs;
            // apart: their courier sets it down at the gate on the map; the message says who sent it.
            Tell.Message(NewsKind.World, Importance.Notable, home, MatchWords.T(away ? "CC_Match_KinGift_Away" : "CC_Match_KinGift", MatchWords.Name(ours, NameOf(t, true)).Named("OURS"),
                MatchWords.Name(theirs, NameOf(t, false)).Named("THEIRS"), MatchWords.People(f).Named("PEOPLE"), Couriers.Describe(things).Named("GIFT"), (town?.Name ?? "?").Named("TOWN")),
                LookTargets.Invalid, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>A raiders' camp of another people set against the home and still hidden: their letter warns of it (MatchMath.Warns).</summary>
        private static void Warn(Faction f, Map home, int now)
        {
            var c = RaidersCamps.CampOf(home);
            if (c == null || c.stage != CampStage.Hidden || c.people == f || c.setAt < 0) return;
            if (!MatchMath.Warns((now - c.setAt) / (float)MatchMath.DayTicks, Rand.Value)) return;
            var town = WorldSituationWorker.NearestTown(f, home);
            RaidersCamps.WarnedByKin(home, c, f, town?.Name ?? MatchWords.People(f), now);
        }

        /// <summary>
        /// The alliance stands as a defence pact (one, not two; never with the Empire): the live one with them runs on while
        /// the marriage holds; none, it is made again a year after the last one with them closed.
        /// </summary>
        private static void KeepPact(Faction f, Map home, float day)
        {
            if (!Pacts.On || Ages.IsEmpire(f)) return;
            var pact = Pacts.Of(f, home, PactKind.Defence);
            if (pact != null)
            {
                if (pact.terms.number - day < MatchMath.PactRenewWithinDays && !Pacts.CallUnderWay(pact))
                {
                    pact.terms.number = day + PactMath.PactDays;
                    WorldLedger.Touch(pact);
                }
                return;
            }
            float since = float.MaxValue;
            var people = Refs.Of(f);
            var colony = Diplomacy.Colony(home);
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Pact))
                if (t != null && !t.Live && t.origin == PactMath.PactOrigin && t.terms?.key == PactMath.KindKey(PactKind.Defence) && t.Between(people, colony) && t.closed >= 0)
                    since = Math.Min(since, day - t.closed / (float)MatchMath.DayTicks);
            if (!MatchMath.PactAgain(false, false, since)) return;
            if (Pacts.Make(f, home, PactKind.Defence, false, null, 0) == null) return;
            // apart: told in a message; nothing to see until a call is made.
            Tell.Message(NewsKind.World, Importance.Notable, home, MatchWords.P("CC_Match_PactAgain", MatchWords.People(f).Named("PEOPLE")), LookTargets.Invalid,
                MessageTypeDefOf.PositiveEvent);
        }

        // ================= How it ends =================

        /// <summary>Ended with nobody at fault (a death not at the colony's hand, their people gone, theirs gone home): told when there is a letter to send.</summary>
        private static void End(Tie t, string label, string text)
        {
            if (t == null || !t.Live) return;
            var home = HomeOf(t);
            WorldLedger.Set(t, Tie.State.Ended);
            Diplomacy.Dirty();
            if (!text.NullOrEmpty() && home != null)
                // apart: what became of the alliance, told in a letter.
                Tell.Letter(NewsKind.World, Importance.Notable, home, label, text, LetterDefOf.NeutralEvent, LookTargets.Invalid, PeopleOf(t));
        }

        /// <summary>The couple parted (the game's divorce or breakup): the alliance ends, their regard falls.</summary>
        private static void Parted(Tie t, Pawn ours, Pawn theirs, Faction f, Map home)
        {
            foreach (var n in Notables(f, home))
            {
                n.personal += MatchMath.PartedRegard;
                n.Remember(MatchWords.P("CC_Match_Note_Parted", ours.Named("OURS"), theirs.Named("THEIRS")), MatchMath.PartedRegard);
            }
            NotableBook.Changed();
            End(t, MatchWords.P("CC_Match_End_Parted_Label", MatchWords.People(f).Named("PEOPLE")),
                MatchWords.T("CC_Match_End_Parted", ours.Named("OURS"), theirs.Named("THEIRS"), MatchWords.People(f).Named("PEOPLE")));
        }

        /// <summary>Their head and the notables of theirs the colony knows who would take it to heart (at most four).</summary>
        private static List<Notable> Notables(Faction f, Map home)
        {
            var list = new List<Notable>();
            var book = NotableBook.Instance;
            if (book == null || f == null) return list;
            var head = FavourCalls.HeadOf(f, home);
            if (head != null) list.Add(head);
            foreach (var n in book.notables)
                if (list.Count < 4 && n != null && n != head && n.faction == f && n.Alive && (n.role == NotableRole.Heir || n.role == NotableRole.Envoy))
                    list.Add(n);
            return list;
        }

        /// <summary>
        /// The colony wronged a spouse from among them: the marriage broken, "spouseWronged" in their memory, their notables'
        /// regard, the alliance's pact broken, and their people's grudge against the colony (theirs, never one family's: it is
        /// handed down no line).
        /// </summary>
        private static void Wronged(Tie t, MatchWrong wrong, Pawn victim)
        {
            if (t == null || !t.Live) return;
            var f = PeopleOf(t);
            var home = HomeOf(t);
            WorldLedger.Set(t, Tie.State.Broken);
            string name = victim?.LabelShort ?? DipWords.NameOf(t.terms.about);
            bool pactBroken = false;
            if (f != null)
            {
                FactionMemory.Note(f, "spouseWronged", name);
                foreach (var n in Notables(f, home))
                {
                    n.personal += MatchMath.WrongedRegard;
                    n.Remember(MatchWords.P("CC_Match_Note_Wronged", name.Named("NAME"), MatchWords.Wrong(wrong).Named("WRONG")), MatchMath.WrongedRegard);
                }
                NotableBook.Changed();
                var pact = Pacts.Of(f, home, PactKind.Defence);
                pactBroken = pact != null;
                if (pactBroken) Pacts.End(pact, Tie.State.Broken, null);
            }
            var head = FavourCalls.HeadOf(f, home);
            Grudge(Refs.Of(f), f, home, victim != null ? WorldLedger.PersonOf(victim) : t.terms.about);
            Diplomacy.Dirty();
            // apart: told in a letter; the wrong itself happened on the map.
            Tell.Letter(NewsKind.World, Importance.Important, home, MatchWords.P("CC_Match_Wronged_Label", MatchWords.People(f).Named("PEOPLE")),
                // The pact broken with it is said only of one that stood (never the Empire's, none with pacts off).
                MatchWords.T(pactBroken ? "CC_Match_Wronged" : "CC_Match_WrongedNoPact", name.Named("NAME"), MatchWords.Wrong(wrong).Named("WRONG"), MatchWords.People(f).Named("PEOPLE"),
                    (head != null ? NotableBook.Called(head) : MatchWords.People(f)).Named("HEAD")), LetterDefOf.NegativeEvent, LookTargets.Invalid, f);
        }

        /// <summary>A grudge for a spouse wronged, held by <paramref name="holder"/> (their people) against the colony (key "spouse", about the spouse; their people in its count).</summary>
        private static Tie Grudge(Ref holder, Faction f, Map home, Ref about)
        {
            if (holder.IsNone || f == null) return null;
            return WorldLedger.AddTie(Tie.Kind.Grudge, holder, Diplomacy.Colony(home), new TieTerms { key = MatchMath.GrudgeKey, about = about, count = f.loadID }, Tie.Never, MatchMath.Origin);
        }

        /// <summary>The live grudges for a spouse wronged.</summary>
        public static List<Tie> Grudges()
        {
            var list = new List<Tie>();
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Grudge))
                if (t != null && t.Live && t.origin == MatchMath.Origin)
                    list.Add(t);
            return list;
        }

        /// <summary>
        /// Grudges for a spouse wronged are their people's against the colony , handed down no line. An older
        /// save's held by one of their notables (their head took it up, and it passed to the next) is made their people's,
        /// quietly, as the day looks; one whose people are gone ends.
        /// </summary>
        private static void PeoplesGrudges()
        {
            foreach (var t in Grudges())
            {
                var f = Refs.Faction(Ref.People(t.terms.count));
                if (f == null || f.defeated)
                {
                    WorldLedger.Set(t, Tie.State.Ended);
                    continue;
                }
                if (t.from.kind == RefKind.People) continue;
                t.from = Refs.Of(f);
                WorldLedger.Touch(t);
            }
        }

        // ================= A death =================

        /// <summary>A spouse in a marriage or a match under way, as they die (Patch_MatchKilled's prefix).</summary>
        public static bool Concerns(Pawn x) => x != null && (TieOf(x) != null || PlanOf(x) != null);

        /// <summary>What became of them as they die: by the colony's hand (an execution, a colonist's blow), in the open, untended.</summary>
        public static MatchDeath Dying(Pawn x, DamageInfo? dinfo)
        {
            var map = x.MapHeld;
            bool executed = dinfo?.Def == DamageDefOf.ExecutionCut;
            bool ours = dinfo?.Instigator is Pawn i && i != x && i.Faction == Faction.OfPlayer && i.RaceProps.Humanlike;
            bool open = x.Spawned && !x.Position.Roofed(x.Map);
            bool untended = x.health?.hediffSet?.hediffs?.Any(h => h != null && h.TendableNow()) ?? false;
            return new MatchDeath { wrong = MatchMath.WrongOf(ours, executed, false, false, false, false), open = open, untended = untended, map = map };
        }

        /// <summary>
        /// One of a marriage (or a match under way) died: by the colony's hand, a wrong (their people's grudge); else the
        /// alliance ends with no grudge, their regard moved by how the colony kept one of theirs who died at its home.
        /// </summary>
        public static void Died(Pawn x, MatchDeath d)
        {
            if (x == null || d == null) return;
            var p = PlanOf(x, out var h);
            if (p != null)
            {
                if (MatchMath.Wronged(d.wrong) && p.people != null && x == p.theirs)
                {
                    FactionMemory.Note(p.people, "spouseWronged", x.LabelShort);
                    Off(h, p, MatchWords.P("CC_Match_Off_Wronged", x.Named("NAME"), MatchWords.Wrong(d.wrong).Named("WRONG"), MatchWords.People(p.people).Named("PEOPLE")), true);
                }
                else Off(h, p, MatchWords.P("CC_Match_Off_Died", x.Named("NAME"), MatchWords.Name(p.ours, p.oursName).Named("OURS"), MatchWords.Name(p.theirs, p.theirsName).Named("THEIRS")),
                    false);
                return;
            }
            var t = TieOf(x);
            if (t == null) return;
            var f = PeopleOf(t);
            var home = HomeOf(t);
            bool theirs = TheirsOf(t) == x;
            if (MatchMath.Wronged(d.wrong))
            {
                Wronged(t, d.wrong, x);
                return;
            }
            // One of theirs who died at the colony's home: their regard by how the colony kept them.
            int regard = theirs ? MatchMath.DiedRegard(d.map != null && Homes.IsHome(d.map), d.open, d.untended) : 0;
            if (regard != 0 && f != null)
            {
                foreach (var n in Notables(f, home))
                {
                    n.personal += regard;
                    n.Remember(MatchWords.P(regard <= -10 ? "CC_Match_Note_DiedOpen" : "CC_Match_Note_Died", x.Named("NAME")), regard);
                }
                NotableBook.Changed();
            }
            string how = regard <= -10 ? " " + MatchWords.P(d.untended ? "CC_Match_Died_Untended" : "CC_Match_Died_Open", MatchWords.People(f).Named("PEOPLE")) : "";
            End(t, MatchWords.P("CC_Match_End_Death_Label", MatchWords.People(f).Named("PEOPLE")),
                MatchWords.T("CC_Match_End_Death", x.Named("DEAD"), MatchWords.People(f).Named("PEOPLE")) + how);
        }
    }
}
