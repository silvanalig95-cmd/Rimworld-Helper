using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The letter purpose of release 2b.2 in the existing machinery (World/NeighbourLetters.cs, through DiplomacyLetters):
    /// Match, proposing one of the colony's for one of theirs ("match:OURS:THEIRS": the notable written to, or another of
    /// their people the colony knows) or for one of the town of the head written to ("match:OURS:h:NOTABLE:G", the h kept from
    /// when it named a house), each
    /// topic with how the colonist feels about it; what is asked is where the couple would live ("home:Colony",
    /// "home:Theirs"). Their answer is by their own reckoning (Matches.Hear), and the answer's tone follows it.
    /// </summary>
    public static class MatchLetters
    {
        public static bool Owns(LetterPurpose p) => p == LetterPurpose.Match;

        /// <summary>Why a match can't be proposed to them now, or null.</summary>
        public static string WhyNot(Notable n, Map home)
        {
            var f = n?.faction;
            string why = Matches.PeopleWhyNot(f, home);
            if (why != null) return why;
            return Topics(n, home).Count == 0 ? MatchWords.P("CC_Match_Why_NoneFits", MatchWords.People(f).Named("PEOPLE")) : null;
        }

        /// <summary>
        /// What a letter to them can be about: each of the colony's at the home with the notable written to (when they fit),
        /// with another of their town the colony knows, or with one of their town the head speaks for.
        /// </summary>
        public static List<LetterTopic> Topics(Notable n, Map home)
        {
            var list = new List<LetterTopic>();
            var f = n?.faction;
            if (f == null || home == null || Matches.PeopleWhyNot(f, home) != null) return list;
            try
            {
                foreach (var c in Matches.Colonists(home))
                {
                    foreach (var cand in Matches.CandidatesFor(c, f, 4))
                    {
                        if (list.Count >= 8) return list;
                        // The notable written to, another of their town, or one of the town the head speaks for.
                        bool fits = cand.notable == n || cand.head == n || cand.notable != null && cand.notable.town == n.town;
                        if (!fits) continue;
                        string key = cand.pawn != null ? MatchMath.Topic(c.thingIDNumber, cand.pawn.thingIDNumber) : MatchMath.HouseTopic(c.thingIDNumber, cand.head.id, (int)cand.gender);
                        var consent = Matches.ConsentOf(c, cand.pawn, MatchHome.Colony, out var why, out var other);
                        list.Add(new LetterTopic
                        {
                            key = key,
                            text = MatchWords.P("CC_Match_Topic", c.Named("OURS"), cand.Words.Named("THEIRS")) + " (" + MatchWords.ConsentLine(c, consent, why, other).TrimEnd('.') + ")"
                        });
                    }
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("match letters: topics", e);
            }
            return list;
        }

        /// <summary>Where the couple would live, each with how the colonist feels about it then (a colonist who would leave their children is against going).</summary>
        public static List<FavourCalls.Option> Asks(string topicKey, Map home)
        {
            var list = new List<FavourCalls.Option>();
            if (!Resolve(topicKey, home, out var ours, out var theirs, out var head, out var g)) return list;
            string town = (theirs != null ? NotableOf(theirs) : head)?.townName ?? "?";
            foreach (MatchHome h in Enum.GetValues(typeof(MatchHome)))
            {
                var consent = Matches.ConsentOf(ours, theirs, h, out var why, out var other);
                string label = MatchWords.Lives(h, ours.LabelShort, theirs?.LabelShort ?? MatchWords.Candidate(null, null, head, g), town).CapitalizeFirst();
                list.Add(new FavourCalls.Option { code = MatchMath.AskCode(h), label = label + " (" + MatchWords.ConsentLine(ours, consent, why, other).TrimEnd('.') + ")" });
            }
            return list;
        }

        private static Notable NotableOf(Pawn x) => NotableBook.Instance?.notables.FirstOrDefault(n => n != null && n.pawn == x);

        /// <summary>A topic's colonist and one of theirs (or the head who speaks for one of their town, and the sex of one to be made).</summary>
        public static bool Resolve(string key, Map home, out Pawn ours, out Pawn theirs, out Notable head, out Gender g)
        {
            ours = theirs = null;
            head = null;
            g = Gender.None;
            if (!MatchMath.ParseTopic(key, out int o, out int t, out int nId, out int gender)) return false;
            ours = home?.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.thingIDNumber == o) ?? PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.FirstOrDefault(p => p.thingIDNumber == o);
            if (t >= 0) theirs = NotableBook.Instance?.notables.FirstOrDefault(n => n?.pawn != null && n.pawn.thingIDNumber == t)?.pawn
                                 ?? home?.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.thingIDNumber == t);
            else
            {
                head = NotableBook.Instance?.Get(nId);
                g = (Gender)gender;
            }
            return ours != null && (theirs != null || head != null);
        }

        /// <summary>
        /// They have read it (or heard the call): their answer by their own reckoning (Matches.Hear), and the answer's tone
        /// follows it (no is a refusal; yes, never worse than cool).
        /// </summary>
        public static string Read(NeighbourLetter l, Notable n, Map home, Pawn writer, ref AnswerTone tone)
        {
            var f = n?.faction;
            bool yes = false;
            string line;
            if (!Resolve(l.topicKey, home, out var ours, out var theirs, out var head, out var g))
                line = MatchWords.T("CC_Match_Ans_Moot", MatchWords.People(f).Named("PEOPLE"), MatchWords.P("CC_Match_Why_Gone").Named("WHY"));
            else
            {
                MatchMath.ParseAsk(l.help, out var lives);
                var them = theirs != null ? NotableOf(theirs) : null;
                line = Matches.Hear(f, home, ours, theirs, them ?? n, head, g, lives, MatchWay.Sought, Rand.Value, out yes);
            }
            if (!yes) tone = AnswerTone.Refusal;
            else if (tone > AnswerTone.Cool) tone = AnswerTone.Cool;
            return line;
        }
    }

    /// <summary>Their offer of a match, as a letter: agree (the colonist asked first), or decline.</summary>
    public class ChoiceLetter_MatchOffer : ChoiceLetter
    {
        public Faction people;
        public int home = HomesMath.None;
        public Pawn ours, theirs;
        public int notable = -1;
        public int gender;
        public MatchHome lives;

        private bool Valid(out Map map)
        {
            map = Homes.MapOf(home) ?? Homes.Seat;
            if (people == null || people.defeated || Diplomacy.Hostile(people) || map == null || ours == null || ours.Dead) return false;
            if (Matches.PeopleWhyNot(people, map) != null) return false;
            return theirs != null ? Matches.WhyNotPair(ours, theirs, people) == null : Matches.WhyNotOfTown(ours, NotableBook.Instance?.Get(notable)) == null;
        }

        /// <summary>Still waiting for the colony's answer (the inbox's row).</summary>
        public bool Waiting => !ArchivedOnly && Valid(out _);

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                Map map = null;
                if (ArchivedOnly || !Valid(out map))
                {
                    yield return Option_Close;
                    yield break;
                }
                var consent = Matches.ConsentOf(ours, theirs, lives, out _, out _);
                yield return new DiaOption(MatchWords.P(consent == Consent.Unwilling ? "CC_Match_Opt_AcceptAsk" : "CC_Match_Opt_Accept", ours.Named("OURS")))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        if (Valid(out var m)) Matches.Accept(this, m);
                    },
                    resolveTree = true
                };
                yield return new DiaOption(MatchWords.P("CC_Match_Opt_Decline"))
                {
                    action = () =>
                    {
                        Find.LetterStack.RemoveLetter(this);
                        Matches.Decline(this);
                    },
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
            Scribe_References.Look(ref ours, "ours");
            Scribe_References.Look(ref theirs, "theirs");
            Scribe_Values.Look(ref notable, "notable", -1);
            Scribe_Values.Look(ref gender, "gender");
            Scribe_Values.Look(ref lives, "lives");
        }
    }
}
