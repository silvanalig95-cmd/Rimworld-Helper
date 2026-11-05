using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The letter purposes of release 2a.3 in the existing machinery (World/NeighbourLetters.cs, through DiplomacyLetters):
    /// Ransom, about one of theirs in the colony's cells (their offer brought sooner; the topic "ransom:ID") or an exchange
    /// of theirs for one of the colony's they hold ("exchange:OURS:THEIRS"); Envoy, asking them to send their envoy for a
    /// season ("envoy:ask"). Their answer is by their own reckoning (Ransoms, Envoys), and the answer's tone follows it.
    /// </summary>
    public static class RansomLetters
    {
        public const string EnvoyAsk = "envoy:ask";

        public static bool Owns(LetterPurpose p) => p == LetterPurpose.Ransom || p == LetterPurpose.Envoy;

        /// <summary>Why it can't be written to them now, or null.</summary>
        public static string WhyNot(Notable n, LetterPurpose p, Map home)
        {
            var f = n?.faction;
            if (p == LetterPurpose.Envoy) return Envoys.AskWhyNot(f, home);
            if (!Ransoms.On) return RansomWords.P("CC_Ransom_Why_Off");
            if (!Ransoms.Deals(f)) return RansomWords.P("CC_Ransom_Why_Never", RansomWords.People(f).Named("PEOPLE"));
            return Topics(n, p, home).Count == 0 ? RansomWords.P("CC_Ransom_Why_Nothing", RansomWords.People(f).Named("PEOPLE")) : null;
        }

        /// <summary>What a letter to them can be about: each of theirs in the home's cells, each exchange that could be made; or asking for their envoy.</summary>
        public static List<LetterTopic> Topics(Notable n, LetterPurpose p, Map home)
        {
            var list = new List<LetterTopic>();
            var f = n?.faction;
            if (f == null || home == null) return list;
            try
            {
                if (p == LetterPurpose.Envoy)
                {
                    if (Envoys.AskWhyNot(f, home) == null)
                        list.Add(new LetterTopic { key = EnvoyAsk, text = RansomWords.P("CC_Envoy_Topic", NotableBook.Called(Envoys.EnvoyOf(f, home)).Named("ENVOY")) });
                    return list;
                }
                if (!Ransoms.On || !Ransoms.Deals(f)) return list;
                foreach (var c in home.mapPawns.PrisonersOfColonySpawned)
                {
                    if (c?.Faction != f || c.DevelopmentalStage.Baby()) continue;
                    var o = Ransoms.For(c);
                    if (o != null && o.stage != RansomStage.Waiting) continue;
                    list.Add(new LetterTopic { key = RansomMath.RansomTopic(c.thingIDNumber), text = RansomWords.P("CC_Ransom_Topic", c.Named("CAPTIVE")) });
                }
                foreach (var (ours, theirs) in Ransoms.Pairs(f, home).Take(8))
                    if (Ransoms.ExchangeWhyNot(ours, theirs, home) == null)
                        list.Add(new LetterTopic
                        {
                            key = RansomMath.ExchangeTopic(ours.thingIDNumber, theirs.thingIDNumber),
                            text = RansomWords.P("CC_Ransom_TopicExchange", ours.Named("OURS"), theirs.Named("THEIRS"))
                        });
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("ransom letters: topics", e);
            }
            return list;
        }

        /// <summary>One of theirs in the home's cells by id, or null.</summary>
        public static Pawn Prisoner(Map home, int id) => home?.mapPawns.PrisonersOfColonySpawned.FirstOrDefault(p => p != null && p.thingIDNumber == id);

        /// <summary>One of the colony's they hold, by id, or null.</summary>
        public static Pawn Held(Faction f, int id) => FavourCalls.Held(f).FirstOrDefault(p => p.thingIDNumber == id);

        /// <summary>
        /// They have read it (or heard the call): their answer by their own reckoning, and the answer's tone follows it (no is
        /// a refusal; yes, never worse than cool). Their messenger or envoy comes after their answer's walk back.
        /// </summary>
        public static string Read(NeighbourLetter l, Notable n, Map home, ref AnswerTone tone)
        {
            var f = n?.faction;
            bool yes = false;
            string line;
            // Their side comes after their answer's way back (a call: some hours).
            float days = l.channel == LetterChannel.Console ? Rand.Range(0.25f, 0.5f) : l.backDays + 0.5f;
            if (l.purpose == LetterPurpose.Envoy)
                line = Envoys.HearAsk(f, home, NotableBook.Opinion(n), days + EnvoyMath.ComeDays(Rand.Value) - 1f, out yes);
            else if (RansomMath.ParseRansom(l.topicKey, out int id))
                line = Ransoms.HearOffer(Prisoner(home, id), home, days, out yes);
            else if (RansomMath.ParseExchange(l.topicKey, out int ours, out int theirs))
                line = Ransoms.HearExchange(Held(f, ours), Prisoner(home, theirs), home, days * 24f, out yes);
            else line = RansomWords.T("CC_Ransom_Ans_Moot", RansomWords.People(f).Named("PEOPLE"));
            if (!yes) tone = AnswerTone.Refusal;
            else if (tone > AnswerTone.Cool) tone = AnswerTone.Cool;
            return line;
        }
    }

    /// <summary>
    /// "Offer an exchange" face to face with one of theirs at the colony (Outsiders' right-click menu, DiplomacyActs): the
    /// player picks which of the colony's they hold for which of theirs in its cells; the colonist walks over to say it,
    /// and their answer comes at once. Agreed, their party brings the colony's own to the gate some hours later.
    /// </summary>
    public static class ExchangeActs
    {
        public static string WhyNot(Pawn c, Pawn x)
        {
            var f = Outsiders.PeopleOf(x);
            var home = c?.MapHeld;
            string first = null;
            foreach (var (ours, theirs) in Ransoms.Pairs(f, home))
            {
                string why = Ransoms.ExchangeWhyNot(ours, theirs, home);
                if (why == null) return null;
                first ??= why;
            }
            return first ?? RansomWords.P("CC_Ransom_Why_Nothing", RansomWords.People(f).Named("PEOPLE"));
        }

        /// <summary>The pairs to offer, each greyed with why not; a pick sends the colonist over (<paramref name="pick"/>, the code "OURS:THEIRS").</summary>
        public static void Choose(Pawn c, Pawn x, OutsiderAct a, Action<Pawn, Pawn, OutsiderAct, string> pick)
        {
            var opts = new List<FloatMenuOption>();
            var home = c.MapHeld;
            foreach (var (ours, theirs) in Ransoms.Pairs(Outsiders.PeopleOf(x), home))
            {
                string label = RansomWords.P("CC_Ransom_TopicExchange", ours.Named("OURS"), theirs.Named("THEIRS"));
                string why = Ransoms.ExchangeWhyNot(ours, theirs, home);
                if (why != null)
                {
                    opts.Add(new FloatMenuOption("CC_Out_Disabled".Translate(label.Named("LABEL"), why.Named("WHY")).Resolve(), null));
                    continue;
                }
                string code = ours.thingIDNumber + ":" + theirs.thingIDNumber;
                opts.Add(new FloatMenuOption(label, () => pick(c, x, a, code)));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("CC_Out_Nothing".Translate().Resolve(), null));
            Find.WindowStack.Add(new FloatMenu(opts, RansomWords.P("CC_Ransom_Menu_Title", x.Named("OTHER"))));
        }

        /// <summary>The talk is over (the two face to face): their answer at once, in a word and a message.</summary>
        public static Verse.AI.Job Talked(Pawn c, Pawn x, OutsiderAct a, string code)
        {
            var f = Outsiders.PeopleOf(x);
            var home = c.MapHeld;
            var parts = (code ?? "").Split(':');
            if (f == null || parts.Length != 2 || !int.TryParse(parts[0], out int oursId) || !int.TryParse(parts[1], out int theirsId)) return null;
            string line = Ransoms.HearExchange(RansomLetters.Held(f, oursId), RansomLetters.Prisoner(home, theirsId), home, RansomMath.PartyHours(Rand.Value), out bool yes);
            // together: asked and answered face to face, at the end of the talk that brought them together.
            Outsiders.Say(x, yes ? "exchangeyes" : "exchangeno", c);
            Outsiders.Remember(c, x, a, Outsiders.Note(a, c, yes ? RansomWords.P("CC_Ransom_Note_Yes") : RansomWords.P("CC_Ransom_Note_No")), yes ? 0.5f : -0.5f, yes ? 1f : -1f);
            // together: told as the two stand face to face; the message carries what follows on the map.
            Tell.Message(NewsKind.World, Importance.Important, home, line, new LookTargets(x), yes ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent);
            return null;
        }
    }
}
