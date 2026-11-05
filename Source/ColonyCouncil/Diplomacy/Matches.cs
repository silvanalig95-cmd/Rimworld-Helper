using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    [DefOf]
    public static class MatchDefOf
    {
        public static LetterDef Rimfolk_MatchOffer;
        public static JobDef Rimfolk_MatchLeave;
        public static ThoughtDef Rimfolk_MatchPressed;

        static MatchDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(MatchDefOf));
    }

    /// <summary>
    /// Marriage alliances (docs/design/stage2-diplomacy.md, round b, item 6; release 2b.2; the player's decisions 8 and 9;
    /// the rules in MatchMath.cs). A match between one of the colony's and one of theirs (a notable, or a member of a
    /// notable's house made for it) comes three ways: "Speak for a match" face to face with one of theirs at the colony (two
    /// who courted their way to lovers, or anyone who fits: MatchActs.cs), the council's letter to their notable
    /// (MatchLetters.cs; the People card lists who would fit), or their own offer (ChoiceLetter_MatchOffer, once a year at
    /// most when their head thinks 40 or more of the colony). The colonist is asked first: their own feelings say whether
    /// they are glad of it, willing, or against it, and a match they are against comes to the council with their objection
    /// before anything is agreed (MatchObjection.cs). Where the couple lives is chosen in each match: theirs comes to the
    /// colony with their kin as the wedding's guests (Feasts.Host), joins it at the gate, and the game's own ceremony marries
    /// them there; or their kin come to the gate for the colony's own, who walks out and leaves with them for good, and the
    /// wedding at their town is told in their letter (MatchParty.cs). Gifts by their ways go both ways at the gate. The
    /// wedding makes the Marriage tie (from their people to the colony, about the colony's own, key "spouse:" and theirs),
    /// the deed "married", the history's "dip:wedding"; the alliance's days, ends and bad sides are MatchAlliance.cs.
    /// </summary>
    public static partial class Matches
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        private static float Day => Now / (float)MatchMath.DayTicks;

        /// <summary>The setting "Marriage alliances": off, nothing new is matched or offered, a match agreed goes ahead, and a marriage holds.</summary>
        public static bool On => ColonyCouncilMod.Settings?.diplomacyMarriage ?? true;

        // ================= Reading =================

        /// <summary>Every match under way at a home of the colony's, with its home (a new list).</summary>
        public static List<(DiplomacyHome home, MatchPlan plan)> All()
        {
            var list = new List<(DiplomacyHome, MatchPlan)>();
            var book = DiplomacyBook.Instance;
            if (book == null) return list;
            foreach (var h in book.Live())
                foreach (var p in h.matches)
                    if (p != null && p.Live)
                        list.Add((h, p));
            return list;
        }

        public static MatchPlan Get(int id, out DiplomacyHome home)
        {
            home = null;
            foreach (var (h, p) in All())
                if (p.id == id)
                {
                    home = h;
                    return p;
                }
            return null;
        }

        /// <summary>The match under way that this pawn is in, either side, or null.</summary>
        public static MatchPlan PlanOf(Pawn x, out DiplomacyHome home)
        {
            home = null;
            if (x == null) return null;
            foreach (var (h, p) in All())
                if (p.Has(x))
                {
                    home = h;
                    return p;
                }
            return null;
        }

        public static MatchPlan PlanOf(Pawn x) => PlanOf(x, out _);

        /// <summary>The ledger's live Marriage ties made here (not Diplomatic Marriage+'s views), oldest first.</summary>
        public static List<Tie> Live()
        {
            var list = new List<Tie>();
            foreach (var t in WorldLedger.OwnTies(Tie.Kind.Marriage))
                if (t != null && t.Live && t.origin == MatchMath.Origin)
                    list.Add(t);
            return list;
        }

        public static Tie GetTie(int id)
        {
            var t = WorldLedger.Get(id);
            return t != null && t.kind == Tie.Kind.Marriage && t.origin == MatchMath.Origin ? t : null;
        }

        /// <summary>The live marriages with that people.</summary>
        public static List<Tie> With(Faction f) => f == null ? new List<Tie>() : Live().Where(t => PeopleOf(t) == f).ToList();

        /// <summary>Diplomatic Marriage+'s permanent alliance, as the ledger's view of it (read only; never driven).</summary>
        public static List<Tie> Views()
        {
            var list = new List<Tie>();
            try
            {
                foreach (var t in WorldLedger.Today())
                    if (t != null && t.kind == Tie.Kind.Marriage && t.IsView)
                        list.Add(t);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: Diplomatic Marriage+'s alliance", e);
            }
            return list;
        }

        public static Faction PeopleOf(Tie t) => t == null ? null : Refs.Faction(t.from.kind == RefKind.People ? t.from : t.to);

        public static Map HomeOf(Tie t) => t == null ? null : Diplomacy.HomeOf(t.from.kind == RefKind.Colony ? t.from : t.to);

        /// <summary>The colony's own in a marriage (living at the colony, or married into their town).</summary>
        public static Pawn OursOf(Tie t) => t?.terms == null ? null : Refs.Pawn(t.terms.about);

        /// <summary>Theirs in a marriage.</summary>
        public static Pawn TheirsOf(Tie t) => t?.terms != null && MatchMath.ParseSpouse(t.terms.key, out string r) ? Refs.Pawn(Ref.Parse(r)) : null;

        public static MatchHome LivesOf(Tie t) => MatchMath.LivesOf(t?.terms?.goods);

        /// <summary>The live marriage this pawn is in, either side, or null.</summary>
        public static Tie TieOf(Pawn x)
        {
            if (x == null) return null;
            var r = Refs.Of(x);
            foreach (var t in Live())
                if (t.terms.about == r || t.terms.key == MatchMath.SpouseKey(r.ToString()))
                    return t;
            return null;
        }

        /// <summary>
        /// The ledger keeps them in the world for a match (KeepFor.Spouse): one of theirs made for a match and not yet come,
        /// or one of the colony's married into their town while the marriage holds.
        /// </summary>
        public static bool Holds(Pawn x)
        {
            if (x == null || x.Dead) return false;
            if (PlanOf(x) != null) return true;
            var t = TieOf(x);
            return t != null && LivesOf(t) == MatchHome.Theirs && OursOf(t) == x;
        }

        // ================= Who may marry =================

        public static bool Titled(Pawn p) => p?.royalty != null && p.royalty.AllTitlesInEffectForReading.Count > 0;

        private static bool OfAge(Pawn p) => p?.ageTracker != null && MatchMath.OfAge(p.ageTracker.AgeBiologicalYearsFloat, p.DevelopmentalStage.Adult());

        /// <summary>Free to take one more spouse: unmarried, or (with Ideology) their faith allows more.</summary>
        private static bool FreeToWed(Pawn p)
        {
            if (p?.relations == null) return false;
            if (p.GetSpouseCount(false) == 0) return true;
            if (!ModsConfig.IdeologyActive) return false;
            try
            {
                return new HistoryEvent(p.GetHistoryEventForSpouseAndFianceCountPlusOne(), p.Named(HistoryEventArgsNames.Doer)).DoerWillingToDo();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Engaged to someone other than <paramref name="other"/>.</summary>
        private static bool EngagedElsewhere(Pawn p, Pawn other) =>
            p?.relations != null && p.relations.DirectRelations.Any(r => r.def == PawnRelationDefOf.Fiance && r.otherPawn != other && r.otherPawn != null && !r.otherPawn.Dead);

        /// <summary>Kin by the game's own rule for romance (any relation that changes the chance of it).</summary>
        private static bool Kin(Pawn a, Pawn b)
        {
            try
            {
                return a.GetRelations(b).Any(r => r != null && r.romanceChanceFactor != 1f);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>One of the colony's who may be matched: grown, free, not a slave, a lodger or a prisoner, not in a match already.</summary>
        public static string ColonistWhyNot(Pawn ours)
        {
            if (ours == null || !KinMemory.Ours(ours) || ours.IsSlave || ours.IsPrisoner) return MatchWords.P("CC_Match_Why_NotOurs");
            if (!OfAge(ours)) return MatchWords.P("CC_Match_Why_Young_Ours", ours.Named("PAWN"));
            if (PlanOf(ours) != null) return MatchWords.P("CC_Match_Why_InMatch", ours.Named("PAWN"));
            return null;
        }

        /// <summary>
        /// Why these two can't be matched, or null: the colonist's own standing, theirs (alive, of their people, free in the
        /// world or at the colony), then the game's answers (MatchMath.WhyNot).
        /// </summary>
        public static string WhyNotPair(Pawn ours, Pawn theirs, Faction f)
        {
            string why = ColonistWhyNot(ours);
            if (why != null) return why;
            if (theirs == null || theirs.Dead || theirs.Discarded || theirs.Faction != f || theirs.IsPrisoner) return MatchWords.P("CC_Match_Why_Gone");
            if (PlanOf(theirs) != null) return MatchWords.P("CC_Match_Why_InMatch", theirs.Named("PAWN"));
            if (theirs == f.leader) return MatchWords.P("CC_Match_Why_Leader", theirs.Named("PAWN"));
            bool engaged = EngagedElsewhere(ours, theirs) || EngagedElsewhere(theirs, ours);
            string key = MatchMath.WhyNot(OfAge(ours), OfAge(theirs), RelationsUtility.AttractedToGender(ours, theirs.gender), RelationsUtility.AttractedToGender(theirs, ours.gender),
                Kin(ours, theirs), FreeToWed(ours), FreeToWed(theirs), engaged, Ages.IsEmpire(f), Titled(theirs), Titled(ours));
            return key == null ? null : MatchWords.P("CC_Match_Why_" + key, ours.Named("PAWN"), theirs.Named("OTHER"));
        }

        /// <summary>The sex a member of a notable's house would be, for this colonist (drawn to them, and they to the colonist); null when none.</summary>
        public static Gender? GenderFor(Pawn ours)
        {
            if (ours == null) return null;
            bool m = RelationsUtility.AttractedToGender(ours, Gender.Male), w = RelationsUtility.AttractedToGender(ours, Gender.Female);
            if (m && w) return Rand.Bool ? Gender.Male : Gender.Female;
            if (m) return Gender.Male;
            if (w) return Gender.Female;
            return null;
        }

        /// <summary>Why one of a notable's house can't be matched with this colonist, or null.</summary>
        public static string WhyNotHouse(Pawn ours, Notable house)
        {
            string why = ColonistWhyNot(ours);
            if (why != null) return why;
            if (house?.pawn == null || !house.Alive || house.faction == null) return MatchWords.P("CC_Match_Why_Gone");
            if (GenderFor(ours) == null) return MatchWords.P("CC_Match_Why_NotDrawn", ours.Named("PAWN"), "?".Named("OTHER"));
            if (Ages.IsEmpire(house.faction) && !Titled(ours)) return MatchWords.P("CC_Match_Why_TitledHouse", ours.Named("PAWN"));
            if (EngagedElsewhere(ours, null) || !FreeToWed(ours)) return MatchWords.P("CC_Match_Why_Married", ours.Named("PAWN"), "?".Named("OTHER"));
            return null;
        }

        /// <summary>Why a match can't be sought with that people at a home now (the setting, a war, too many), or null.</summary>
        public static string PeopleWhyNot(Faction f, Map home)
        {
            if (!On) return MatchWords.P("CC_Match_Why_Off");
            if (f == null || !Diplomacy.Deals(f)) return DipWords.P("CC_Dip_Why_Gone");
            if (Diplomacy.Hostile(f)) return DipWords.P("CC_Dip_Why_War", MatchWords.People(f).Named("PEOPLE"));
            if (With(f).Count + All().Count(x => x.plan.people == f) >= MatchMath.MaxPerPeople)
                return MatchWords.P("CC_Match_Why_Enough", MatchWords.People(f).Named("PEOPLE"));
            var h = home != null ? DiplomacyBook.Instance?.Existing(home) : null;
            if (h != null && h.matches.Count(m => m.Live) >= MatchMath.MaxPlans) return MatchWords.P("CC_Match_Why_Busy");
            return null;
        }

        // ================= The colonist asked first =================

        /// <summary>How the colonist feels about it, by their own feelings (MatchMath.ConsentOf), and who they love instead.</summary>
        public static Consent ConsentOf(Pawn ours, Pawn theirs, MatchHome lives, out ConsentWhy why, out Pawn other)
        {
            other = null;
            why = ConsentWhy.None;
            if (ours?.relations == null) return Consent.Willing;
            bool lovers = theirs != null && LovePartnerRelationUtility.LovePartnerRelationExists(ours, theirs);
            int opinion = theirs != null ? ours.relations.OpinionOf(theirs) : 0;
            var rival = ours.relations.DirectRelations.FirstOrDefault(r => (r.def == PawnRelationDefOf.Lover || r.def == PawnRelationDefOf.Fiance) && r.otherPawn != null && r.otherPawn != theirs
                                                                           && !r.otherPawn.Dead && KinMemory.Ours(r.otherPawn));
            bool leaves = lives == MatchHome.Theirs && ours.relations.Children.Any(c => c != null && !c.Dead && KinMemory.Ours(c) && c.ageTracker.AgeBiologicalYearsFloat < 18f);
            var c0 = MatchMath.ConsentOf(lovers, opinion, rival != null, leaves, out why);
            if (why == ConsentWhy.Another) other = rival?.otherPawn;
            if (why == ConsentWhy.Dislike) other = theirs;
            return c0;
        }

        // ================= Who would fit =================

        /// <summary>One of theirs who could be matched: a notable of theirs, or one of a notable's house not yet made (the sex the colonist is drawn to).</summary>
        public sealed class Candidate
        {
            public Pawn pawn;
            public Notable notable;
            /// <summary>One of this notable's house, made when they agree.</summary>
            public Notable house;
            public Gender gender;

            public string Words => MatchWords.Candidate(pawn, notable, house, gender);
        }

        /// <summary>The colony's own at a home who may be matched (a new list).</summary>
        public static List<Pawn> Colonists(Map home)
        {
            var list = new List<Pawn>();
            if (home == null) return list;
            foreach (var p in home.mapPawns.FreeColonistsSpawned)
                if (p != null && ColonistWhyNot(p) == null)
                    list.Add(p);
            return list;
        }

        /// <summary>
        /// Theirs who would fit this colonist: their heirs, envoys and the faces the colony knows (never their people's
        /// leader nor a town's head), and one of a head's house for each town the colony knows a head of.
        /// </summary>
        public static List<Candidate> CandidatesFor(Pawn ours, Faction f, int max = 6)
        {
            var list = new List<Candidate>();
            var book = NotableBook.Instance;
            if (ours == null || f == null || book == null) return list;
            foreach (var n in book.notables)
            {
                if (list.Count >= max) break;
                if (n == null || n.faction != f || !n.Alive || n.role == NotableRole.Head || n.role == NotableRole.Trader) continue;
                if (WhyNotPair(ours, n.pawn, f) != null) continue;
                list.Add(new Candidate { pawn = n.pawn, notable = n });
            }
            foreach (var n in book.notables)
            {
                if (list.Count >= max) break;
                if (n == null || n.faction != f || !n.Alive || n.role != NotableRole.Head || WhyNotHouse(ours, n) != null) continue;
                var g = GenderFor(ours);
                if (g == null) continue;
                list.Add(new Candidate { house = n, gender = g.Value });
            }
            return list;
        }

        /// <summary>
        /// One of a notable's house, made for a match: of their people's kind, the house's name, the sex the colonist is drawn to, of
        /// an age near the colonist's, the head's child where the ages allow; kept in the world for the match (KeepFor.Spouse).
        /// </summary>
        public static Pawn MakeKin(Notable house, Gender gender, Pawn ours)
        {
            if (house?.pawn == null || house.faction == null) return null;
            try
            {
                var kind = house.pawn.kindDef?.RaceProps?.Humanlike == true && !house.faction.def.HasRoyalTitles ? house.pawn.kindDef : Arrivals.PersonKind(house.faction);
                if (kind == null) return null;
                float age = ours?.ageTracker != null ? ours.ageTracker.AgeBiologicalYearsFloat : 25f;
                float headAge = house.pawn.ageTracker?.AgeBiologicalYearsFloat ?? 50f;
                float lo = Mathf.Max(MatchMath.MinAge, age - 5f), hi = Mathf.Max(lo + 1f, Mathf.Min(age + 5f, headAge - 16f));
                string last = house.pawn.Name is NameTriple tri ? tri.Last : null;
                var p = Arrivals.Make(kind, house.faction, new FloatRange(lo, hi), gender, last, false, house.pawn);
                if (p == null) return null;
                Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
                // The head's child, when the head is old enough to be.
                if (headAge - p.ageTracker.AgeBiologicalYearsFloat >= 16f && house.pawn.relations != null)
                    p.relations.AddDirectRelation(PawnRelationDefOf.Parent, house.pawn);
                WorldLedger.Keep(p, KeepFor.Spouse);
                return p;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: one of a notable's house", e);
                return null;
            }
        }

        // ================= Their answer to the colony's proposal =================

        /// <summary>Their head's regard of the colony (the notable whose house it is, else their head), and their people's goodwill.</summary>
        public static int Regard(Faction f, Map home, Notable n)
        {
            var who = n ?? FavourCalls.HeadOf(f, home);
            if (who != null) return NotableBook.Opinion(who);
            return f != null && f.HasGoodwill ? f.PlayerGoodwill : 0;
        }

        private static int Goodwill(Faction f) => f != null && f.HasGoodwill ? f.PlayerGoodwill : 0;

        /// <summary>
        /// They hear the colony's proposal (its letter read, or face to face): yes or no by their own reckoning. Yes, the match
        /// begins: the colonist asked first (the council's question if they are against it), else agreed. Returns their words.
        /// </summary>
        public static string Hear(Faction f, Map home, Pawn ours, Pawn theirs, Notable n, Notable house, Gender gender, MatchHome lives, MatchWay way, float roll, out bool yes)
        {
            yes = false;
            var form = MatchWords.Form(home, f);
            string why = PeopleWhyNot(f, home) ?? (theirs != null ? WhyNotPair(ours, theirs, f) : WhyNotHouse(ours, house));
            if (why != null) return MatchWords.T("CC_Match_Ans_Moot", MatchWords.People(f).Named("PEOPLE"), why.Named("WHY"));
            bool lovers = theirs != null && LovePartnerRelationUtility.LovePartnerRelationExists(ours, theirs);
            int regard = Regard(f, home, house ?? n);
            string them = theirs != null ? theirs.LabelShort : MatchWords.Candidate(null, null, house, gender);
            if (!MatchMath.Accepts(regard, Goodwill(f), lovers, lives == MatchHome.Colony, roll))
            {
                var who = house ?? n ?? FavourCalls.HeadOf(f, home);
                if (who != null)
                {
                    who.personal += MatchMath.DeclinedRegard / 2f;
                    who.Remember(MatchWords.P("CC_Match_Note_TheyDeclined", ours.Named("PAWN")), MatchMath.DeclinedRegard / 2f);
                }
                return MatchWords.T(regard < MatchMath.MinRegard ? "CC_Match_Ans_NoNever" : "CC_Match_Ans_No", MatchWords.People(f).Named("PEOPLE"), ours.Named("OURS"),
                    them.Named("THEIRS"), form.word.Named("WORD"), form.rite.Named("RITE"));
            }
            bool made = false;
            if (theirs == null)
            {
                theirs = MakeKin(house, gender, ours);
                made = true;
                if (theirs == null) return MatchWords.T("CC_Match_Ans_Moot", MatchWords.People(f).Named("PEOPLE"), MatchWords.P("CC_Match_Why_Gone").Named("WHY"));
            }
            var p = Begin(home, f, ours, theirs, (house ?? n)?.id ?? -1, made, lives, way);
            if (p == null) return MatchWords.T("CC_Match_Ans_Moot", MatchWords.People(f).Named("PEOPLE"), MatchWords.P("CC_Match_Why_Busy").Named("WHY"));
            yes = true;
            string town = p.townName ?? MatchWords.People(f);
            string line = MatchWords.T("CC_Match_Ans_Yes", MatchWords.People(f).Named("PEOPLE"), ours.Named("OURS"), theirs.Named("THEIRS"), form.word.Named("WORD"),
                form.rite.Named("RITE"), MatchWords.Lives(lives, ours.LabelShort, theirs.LabelShort, town).Named("LIVES"));
            if (p.stage == MatchStage.Asking) line += " " + MatchWords.P("CC_Match_Ans_ButAgainst", ours.Named("OURS"));
            return line;
        }

        // ================= A match begins =================

        /// <summary>
        /// Their yes (to the colony's proposal or the colony's to their offer): a match under way at the home. The colonist
        /// is asked first: against it, the council hears their objection (Asking); else agreed at once.
        /// </summary>
        public static MatchPlan Begin(Map home, Faction f, Pawn ours, Pawn theirs, int notable, bool made, MatchHome lives, MatchWay way)
        {
            var book = DiplomacyBook.Instance;
            if (book == null || home == null || !Homes.IsHome(home) || ours == null || theirs == null || f == null) return null;
            var h = book.HomeOf(home);
            if (h.matches.Count(m => m.Live) >= MatchMath.MaxPlans) return null;
            var n = NotableBook.Instance?.Get(notable);
            var town = n != null ? NotableBook.TownOf(n.town) : WorldSituationWorker.NearestTown(f, home);
            var p = new MatchPlan
            {
                id = book.NextId(), way = way, lives = lives, people = f, ours = ours, theirs = theirs, notable = notable, made = made, town = town?.ID ?? -1,
                townName = town?.Name ?? n?.townName, oursName = ours.LabelShort, theirsName = theirs.LabelShort, stage = MatchStage.Asking
            };
            h.matches.Add(p);
            if (theirs.Faction != Faction.OfPlayer && !theirs.Spawned) WorldLedger.Keep(theirs, KeepFor.Spouse);
            var c = ConsentOf(ours, theirs, lives, out _, out _);
            if (c == Consent.Unwilling) MatchObjection.Ask(h, p);
            else Agree(h, p);
            Diplomacy.Dirty();
            return p;
        }

        /// <summary>
        /// Agreed: the two engaged (the game's fiancé relation), gifts by their ways carried both ways at the gate, and the day
        /// their kin come set: as the wedding's guests, or to take the colony's own home with them.
        /// </summary>
        public static void Agree(DiplomacyHome h, MatchPlan p)
        {
            var map = h?.Map;
            if (p == null || map == null || p.ours == null || p.theirs == null) return;
            try
            {
                p.ours.relations.TryRemoveDirectRelation(PawnRelationDefOf.Lover, p.theirs);
                p.theirs.relations.TryRemoveDirectRelation(PawnRelationDefOf.Lover, p.ours);
                if (!p.ours.relations.DirectRelationExists(PawnRelationDefOf.Fiance, p.theirs)) p.ours.relations.AddDirectRelation(PawnRelationDefOf.Fiance, p.theirs);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: the engagement", e);
            }
            p.stage = MatchStage.Agreed;
            p.agreed = Now;
            p.due = Now + Mathf.RoundToInt(MatchMath.LeadDays(TravelDays(p, map)) * MatchMath.DayTicks);
            string gifts = Gifts(p, map);
            if (p.lives == MatchHome.Colony) Host(h, p, map);
            var form = MatchWords.Form(map, p.people);
            string text = MatchWords.T(p.lives == MatchHome.Colony ? "CC_Match_Agreed_Colony" : "CC_Match_Agreed_Theirs", p.ours.Named("OURS"), p.theirs.Named("THEIRS"),
                MatchWords.People(p.people).Named("PEOPLE"), (p.townName ?? MatchWords.People(p.people)).Named("TOWN"), form.word.Named("WORD"), form.rite.Named("RITE"));
            if (!gifts.NullOrEmpty()) text += "\n\n" + gifts;
            text += "\n\n" + MatchWords.P("CC_Match_Agreed_Risks", MatchWords.People(p.people).Named("PEOPLE"));
            if (p.pressed) text += "\n\n" + MatchWords.P("CC_Match_Agreed_Pressed", p.ours.Named("OURS"));
            // apart: the agreement told in a letter; their kin walk in on the map on the day.
            Tell.Letter(NewsKind.World, Importance.Important, map, MatchWords.P("CC_Match_Agreed_Label", p.ours.Named("OURS"), p.theirs.Named("THEIRS")), text,
                LetterDefOf.PositiveEvent, new LookTargets(p.ours), p.people);
            Diplomacy.Dirty();
        }

        /// <summary>Days for their kin to come to the home, by their people's means.</summary>
        public static float TravelDays(MatchPlan p, Map home)
        {
            var town = p.town >= 0 ? NotableBook.TownOf(p.town) : null;
            if (town == null || home == null) return 2f;
            float tiles = Find.WorldGrid.ApproxDistanceInTiles(home.Tile, town.Tile);
            return NotableMath.TravelDays(tiles, (int)(p.people?.def?.techLevel ?? TechLevel.Industrial));
        }

        /// <summary>Their town's fortune (50 when the region keeps none).</summary>
        public static float Fortune(int town)
        {
            try
            {
                var rec = town >= 0 ? WorldLedger.TownRecord(town) : null;
                return rec != null && rec.Seeded ? rec.prosperity : 50f;
            }
            catch
            {
                return 50f;
            }
        }

        /// <summary>What their people give at a match, by their ways: a tribe's goods, the middle ages' and the Empire's gold, silver for the rest.</summary>
        public static ThingDef GiftDef(Faction f)
        {
            if (Ages.IsEmpire(f) || Ages.Of(f) == WorldAge.Medieval) return ThingDefOf.Gold;
            if (Ages.Of(f) == WorldAge.Tribal) return DefDatabase<ThingDef>.GetNamedSilentFail("Jade") ?? DefDatabase<ThingDef>.GetNamedSilentFail("Pemmican") ?? ThingDefOf.Silver;
            return ThingDefOf.Silver;
        }

        /// <summary>
        /// The gifts carried both ways at the gate: theirs by their ways, set down by their porters, and the colony's silver
        /// (what its stores hold, up to the same worth) carried out to them. Returns the letter's words for it.
        /// </summary>
        private static string Gifts(MatchPlan p, Map map)
        {
            try
            {
                int value = MatchMath.GiftValue(Fortune(p.town));
                var def = GiftDef(p.people);
                int count = MatchMath.GiftCount(value, def.BaseMarketValue);
                var theirs = Couriers.Make(def, count);
                int ours = Mathf.Min(value, PlayCarry.Have(map, "silver", ThingDefOf.Silver));
                string theirWords = Couriers.Amount(count, null, def), ourWords = Couriers.Amount(ours, null, ThingDefOf.Silver);
                if (ours > 0)
                {
                    if (!Couriers.Exchange(map, "silver", ThingDefOf.Silver, ours, theirs, p.people))
                    {
                        VarietyUtil.TakeFromStores(map, ThingDefOf.Silver, ours);
                        Loot.Deliver(theirs, map, PlaySpots.GateCell(map, null), false);
                    }
                    return MatchWords.P("CC_Match_Gifts", MatchWords.People(p.people).Named("PEOPLE"), theirWords.Named("THEIRS"), ourWords.Named("OURS"));
                }
                if (!Couriers.Deliver(map, theirs, p.people, PlaySpots.GateCell(map, null))) Loot.Deliver(theirs, map, PlaySpots.GateCell(map, null), false);
                var who = NotableBook.Instance?.Get(p.notable);
                if (who != null) who.personal += MatchMath.DeclinedRegard;
                return MatchWords.P("CC_Match_Gifts_NoneOurs", MatchWords.People(p.people).Named("PEOPLE"), theirWords.Named("THEIRS"));
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: the gifts", e);
                return null;
            }
        }

        /// <summary>The council pressed it over the colonist's objection: agreed, and it weighs on them (the council's thought; their nature, once round d switches it on).</summary>
        public static void Pressed(int id)
        {
            var p = Get(id, out var h);
            if (p == null || p.stage != MatchStage.Asking) return;
            p.pressed = true;
            Nature.Felt(p.ours, NatureAct.MatchAgainstHeart);
            Agree(h, p);
        }

        /// <summary>The council heard the colonist and withdrew: their people feel it a little, and one made for the match is let go.</summary>
        public static void Withdrawn(int id)
        {
            var p = Get(id, out var h);
            if (p == null || p.stage != MatchStage.Asking) return;
            var who = NotableBook.Instance?.Get(p.notable) ?? FavourCalls.HeadOf(p.people, h.Map);
            if (who != null)
            {
                who.personal += MatchMath.DeclinedRegard;
                who.Remember(MatchWords.P("CC_Match_Note_Withdrawn", MatchWords.Name(p.ours, p.oursName).Named("PAWN")), MatchMath.DeclinedRegard);
            }
            Off(h, p, null, false);
        }

        // ================= Their offer =================

        /// <summary>Who of the colony's they would ask for: the one their head thinks best of who fits one of theirs (a titled one for the Empire's titled), with the one of theirs.</summary>
        public static bool Pick(Faction f, Map home, out Pawn ours, out Candidate theirs)
        {
            ours = null;
            theirs = null;
            var head = FavourCalls.HeadOf(f, home);
            int best = int.MinValue;
            foreach (var c in Colonists(home))
            {
                var cands = CandidatesFor(c, f, 3);
                if (cands.Count == 0) continue;
                var cand = cands.FirstOrDefault(x => x.notable?.role == NotableRole.Heir) ?? cands[0];
                if (ConsentOf(c, cand.pawn, MatchHome.Theirs, out _, out _) == Consent.Unwilling && ConsentOf(c, cand.pawn, MatchHome.Colony, out _, out _) == Consent.Unwilling) continue;
                int score = (head != null ? NotableBook.OpinionOf(head, c) : 0) + (Titled(c) ? 20 : 0) + Rand.Range(0, 10);
                if (score <= best) continue;
                best = score;
                ours = c;
                theirs = cand;
            }
            return ours != null;
        }

        /// <summary>
        /// The day's look: a people at peace whose head thinks 40 or more of the colony, with fewer than three marriages with
        /// it, may offer a match once a year (MatchMath.MayOffer), when one of theirs fits one of the colony's.
        /// </summary>
        public static void Offers(int now)
        {
            if (!On || !Tell.CanAsk) return;
            var home = Homes.Seat;
            if (home == null || PeopleWhyNotHome(home)) return;
            foreach (var r in RegionTowns.Reach)
                foreach (var s in r.towns)
                {
                    var f = s?.Faction;
                    if (f == null || !Diplomacy.Deals(f)) continue;
                    var head = FavourCalls.HeadOf(f, home);
                    if (head == null || PeopleWhyNot(f, home) != null) continue;
                    int live = With(f).Count;
                    if (!MatchMath.MayOffer(NotableBook.Opinion(head), Diplomacy.Hostile(f), head.Last(LetterPurpose.Match), now, live)) continue;
                    if (!Rand.Chance(MatchMath.OfferDaily)) continue;
                    if (!Pick(f, home, out var ours, out var cand)) continue;
                    Offer(f, home, head, ours, cand);
                    return;
                }
        }

        private static bool PeopleWhyNotHome(Map home)
        {
            var h = DiplomacyBook.Instance?.Existing(home);
            return h != null && h.matches.Count(m => m.Live) >= MatchMath.MaxPlans;
        }

        /// <summary>Their offer, as a letter: who of theirs for whom of the colony's, where they would have the couple live, and how the colonist feels about it.</summary>
        public static void Offer(Faction f, Map home, Notable head, Pawn ours, Candidate cand)
        {
            var def = MatchDefOf.Rimfolk_MatchOffer;
            if (def == null || f == null || home == null || ours == null || cand == null) return;
            var lives = MatchMath.TheyPropose(cand.notable?.role == NotableRole.Heir, Rand.Value);
            var form = MatchWords.Form(home, f);
            string them = cand.Words;
            string town = (cand.notable ?? cand.house ?? head)?.townName ?? MatchWords.People(f);
            string text = MatchWords.T("CC_Match_Offer", NotableBook.Called(head).Named("WHO"), MatchWords.People(f).Named("PEOPLE"), ours.Named("OURS"), them.Named("THEIRS"),
                form.word.Named("WORD"), form.rite.Named("RITE"));
            text += "\n\n" + MatchWords.Lives(lives, ours.LabelShort, cand.pawn?.LabelShort ?? them, town).CapitalizeFirst() + ".";
            var c = ConsentOf(ours, cand.pawn, lives, out var why, out var other);
            text += " " + MatchWords.ConsentLine(ours, c, why, other);
            text += "\n\n" + MatchWords.P("CC_Match_What", MatchWords.People(f).Named("PEOPLE"));
            var t = NotableBook.TownOf((cand.notable ?? cand.house ?? head).town);
            var letter = LetterMaker.MakeLetter(MatchWords.P("CC_Match_Offer_Label", MatchWords.People(f).Named("PEOPLE"), ours.Named("OURS")), text, def,
                t != null ? new LookTargets(t) : new LookTargets(ours), f);
            if (letter is ChoiceLetter_MatchOffer l)
            {
                l.people = f;
                l.home = Homes.KeyOf(home);
                l.ours = ours;
                l.theirs = cand.pawn;
                l.notable = (cand.notable ?? cand.house)?.id ?? -1;
                l.gender = (int)cand.gender;
                l.lives = lives;
            }
            head.SetLast(LetterPurpose.Match, Now);
            NotableBook.Changed();
            Tell.Ask(letter, home);
        }

        /// <summary>Their offer taken up (the colony's yes): the match begins (the colonist asked first).</summary>
        public static void Accept(ChoiceLetter_MatchOffer l, Map home)
        {
            var f = l.people;
            var n = NotableBook.Instance?.Get(l.notable);
            var theirs = l.theirs;
            bool made = false;
            if (theirs == null)
            {
                theirs = MakeKin(n, (Gender)l.gender, l.ours);
                made = true;
            }
            string why = theirs == null ? MatchWords.P("CC_Match_Why_Gone") : PeopleWhyNot(f, home) ?? WhyNotPair(l.ours, theirs, f);
            if (why != null)
            {
                if (made && theirs != null) LetGo(theirs);
                // apart: the colony's answer to their letter, told in a message.
                Tell.Message(NewsKind.World, Importance.Notable, home, MatchWords.P("CC_Match_Msg_Moot", MatchWords.People(f).Named("PEOPLE"), why.Named("WHY")), LookTargets.Invalid,
                    MessageTypeDefOf.RejectInput);
                return;
            }
            Begin(home, f, l.ours, theirs, l.notable, made, l.lives, MatchWay.Offered);
        }

        /// <summary>Their offer turned down: their head feels it a little.</summary>
        public static void Decline(ChoiceLetter_MatchOffer l)
        {
            var n = NotableBook.Instance?.Get(l.notable);
            var head = n ?? FavourCalls.HeadOf(l.people, Homes.MapOf(l.home) ?? Homes.Seat);
            if (head == null) return;
            head.personal += MatchMath.DeclinedRegard;
            head.Remember(MatchWords.P("CC_Match_Note_WeDeclined", MatchWords.Name(l.ours, null).Named("PAWN")), MatchMath.DeclinedRegard);
            NotableBook.Changed();
        }

        // ================= Called off =================

        /// <summary>
        /// A match comes to nothing: told (when <paramref name="why"/> is given), one made for it let go, the engagement undone;
        /// <paramref name="blame"/>, the colony broke its word (their regard, "slighted", its word broken in reach).
        /// </summary>
        public static void Off(DiplomacyHome h, MatchPlan p, string why, bool blame)
        {
            if (p == null || !p.Live) return;
            p.stage = MatchStage.Off;
            var map = h?.Map;
            try
            {
                if (p.ours?.relations != null && p.theirs != null && !p.ours.Dead && !p.theirs.Dead)
                    p.ours.relations.TryRemoveDirectRelation(PawnRelationDefOf.Fiance, p.theirs);
                if (p.feast >= 0)
                {
                    var g = Feasts.Get(p.feast, out var fh);
                    if (g != null && g.Live && !g.OnMap) Feasts.CallOff(fh, g, fh.Map, MatchWords.P("CC_Match_Off_Feast"), false);
                }
                if (blame && p.people != null)
                {
                    var who = NotableBook.Instance?.Get(p.notable) ?? FavourCalls.HeadOf(p.people, map);
                    if (who != null)
                    {
                        who.personal += MatchMath.CalledOffRegard;
                        who.Remember(MatchWords.P("CC_Match_Note_CalledOff", MatchWords.Name(p.ours, p.oursName).Named("PAWN")), MatchMath.CalledOffRegard);
                    }
                    FactionMemory.Note(p.people, "slighted", MatchWords.Name(p.theirs, p.theirsName));
                    Truces.WordBroken(p.people);
                }
                if (p.theirs != null && p.theirs.Faction != Faction.OfPlayer)
                {
                    if (p.made && !p.theirs.Spawned && !p.theirs.Dead) LetGo(p.theirs);
                    else WorldLedger.Release(p.theirs, KeepFor.Spouse);
                }
                if (!why.NullOrEmpty())
                    // apart: what became of the match, told in a message; whoever is at the gate walks home on the map.
                    Tell.Message(NewsKind.World, Importance.Notable, map, why, p.ours != null && p.ours.Spawned ? new LookTargets(p.ours) : LookTargets.Invalid,
                        blame ? MessageTypeDefOf.NegativeEvent : MessageTypeDefOf.NeutralEvent);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("matches: called off", e);
            }
            h?.matches.Remove(p);
            NotableBook.Changed();
            Diplomacy.Dirty();
        }

        /// <summary>One made for a match that came to nothing: gone back to nothing when nothing else keeps them.</summary>
        private static void LetGo(Pawn x)
        {
            if (x == null) return;
            if (WorldLedger.Release(x, KeepFor.Spouse) && !x.Spawned && !x.Dead && WorldSituationWorker.FreeInWorld(x)) Arrivals.Discard(x);
        }

        /// <summary>The player calls a match off before their kin come: the colony's word is broken.</summary>
        public static string CallOffWhyNot(MatchPlan p) =>
            p == null || !p.Live ? MatchWords.P("CC_Match_Why_Over") : p.stage != MatchStage.Agreed ? MatchWords.P("CC_Match_Why_AlreadyHere") : null;

        public static void CallOffByColony(MatchPlan p)
        {
            Get(p?.id ?? -1, out var h);
            if (p == null || h == null || CallOffWhyNot(p) != null) return;
            Off(h, p, MatchWords.P("CC_Match_Off_ByColony", MatchWords.Name(p.ours, p.oursName).Named("OURS"), MatchWords.Name(p.theirs, p.theirsName).Named("THEIRS"),
                MatchWords.People(p.people).Named("PEOPLE")), true);
        }

        /// <summary>A home lost: its matches come to nothing, nobody at fault.</summary>
        internal static void HomeLost(DiplomacyHome h)
        {
            if (h == null) return;
            foreach (var p in h.matches.ToList()) Off(h, p, null, false);
        }

        /// <summary>Why someone of theirs is at the colony for a match: come for the colony's own, in words; null otherwise.</summary>
        public static string HereAs(Pawn x)
        {
            if (x?.GetLord()?.LordJob is LordJob_MatchEscort j)
            {
                var p = Get(j.plan, out _);
                if (p != null) return MatchWords.P(p.theirs == x ? "CC_Match_HereAs_Spouse" : "CC_Match_HereAs_Kin", MatchWords.Name(p.ours, p.oursName).Named("OURS"));
            }
            return null;
        }
    }
}
