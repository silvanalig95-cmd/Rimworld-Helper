using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// Stage 2's right-click acts with someone of theirs at the colony ("Talk with X...", Outsiders/OutsiderMenu.cs), after
    /// Invite in the menu: this class says whether each shows, why it can't be now, what it is called, what the player picks
    /// first, and what comes of it at the talk's end; the outsiders' talk does the rest (the walk over, their patience, the
    /// words said in turn). Ask a favour of their people (a debt of honour they owe the colony, or one they owe the colonist
    /// in person, called in face to face); speak of a pact between their people and the colony, a defence or a trade pact
    /// (release 2a.2); offer an exchange of captives, one of theirs in the colony's cells for one of the colony's they hold
    /// (release 2a.3, Ransoms.cs); speak for a match comes with its release (DiplomacyMath.Acts). Their answer is given
    /// face to face, at once.
    /// </summary>
    public static class DiplomacyActs
    {
        /// <summary>The acts this release offers, in the menu's order after Invite.</summary>
        public static readonly OutsiderAct[] Acts = { OutsiderAct.AskFavour, OutsiderAct.SpeakPact, OutsiderAct.SpeakMatch, OutsiderAct.OfferExchange };

        public static bool Owns(OutsiderAct a) =>
            a == OutsiderAct.AskFavour || a == OutsiderAct.SpeakPact || a == OutsiderAct.OfferExchange || a == OutsiderAct.SpeakMatch;

        /// <summary>What the player chose for a colonist's walk over (the debt and the ask), taken when the talk ends. A session's only.</summary>
        private static readonly Dictionary<int, (int tie, string code)> chosen = new Dictionary<int, (int, string)>();

        /// <summary>The debts this colonist can call in face to face with them: their people's to the colony, and theirs to the colonist in person.</summary>
        public static List<Tie> Debts(Pawn c, Pawn x)
        {
            var list = new List<Tie>();
            var f = Outsiders.PeopleOf(x);
            if (f == null) return list;
            var me = c != null ? Refs.Of(c) : Ref.None;
            var them = x != null ? Refs.Of(x) : Ref.None;
            foreach (var t in FavoursOwed.OwedBy(f))
            {
                if (FavoursOwed.IsPersonal(t) && (t.from != them || t.to != me)) continue;
                list.Add(t);
            }
            return list;
        }

        public static bool Shown(Pawn c, Pawn x, OutsiderAct a) =>
            // Speak for a match (release 2b.2, MatchActs.cs): its own rules.
            a == OutsiderAct.SpeakMatch ? MatchActs.Shown(c, x) :
            (a == OutsiderAct.OfferExchange ? Ransoms.On && Ransoms.Pairs(Outsiders.PeopleOf(x), c.MapHeld).Count > 0 : a == OutsiderAct.SpeakPact ? Pacts.On : Diplomacy.FavoursOn) && !c.DevelopmentalStage.Juvenile() && !x.DevelopmentalStage.Juvenile() && Outsiders.PeopleOf(x) != null
            && !x.IsQuestLodger();

        /// <summary>Why it can't be asked of them now, or null (after the outsiders' own reasons: busy, tired of the colony).</summary>
        public static string WhyNot(Pawn c, Pawn x, OutsiderAct a)
        {
            var f = Outsiders.PeopleOf(x);
            if (a == OutsiderAct.SpeakMatch) return MatchActs.WhyNot(c, x);
            if (a == OutsiderAct.SpeakPact) return Pacts.WhyNotAny(f, c.MapHeld);
            if (a == OutsiderAct.OfferExchange) return ExchangeActs.WhyNot(c, x);
            var debts = Debts(c, x);
            if (debts.Count == 0) return DipWords.P("CC_Dip_Why_NothingOwed", DipWords.People(f).Named("PEOPLE"));
            string first = null;
            foreach (var t in debts)
            {
                string why = FavourCalls.WhyNot(t);
                if (why == null) return null;
                first ??= why;
            }
            return first;
        }

        public static string Label(Pawn c, Pawn x, OutsiderAct a) =>
            a == OutsiderAct.SpeakMatch ? MatchWords.P("CC_Out_Act_SpeakMatch") :
            DipWords.P(a == OutsiderAct.OfferExchange ? "CC_Out_Act_OfferExchange" : a == OutsiderAct.SpeakPact ? "CC_Out_Act_SpeakPact" : "CC_Out_Act_AskFavour");

        /// <summary>The player picks what to ask, for which debt; the colonist then walks over to ask it.</summary>
        public static void Choose(Pawn c, Pawn x, OutsiderAct a)
        {
            var opts = new List<FloatMenuOption>();
            var home = c.MapHeld;
            if (a == OutsiderAct.SpeakPact)
            {
                ChoosePact(c, x, a, home);
                return;
            }
            if (a == OutsiderAct.OfferExchange)
            {
                ExchangeActs.Choose(c, x, a, (cc, xx, aa, code) => Pick(cc, xx, aa, code));
                return;
            }
            if (a == OutsiderAct.SpeakMatch)
            {
                MatchActs.Choose(c, x, a, (cc, xx, aa, code) => Pick(cc, xx, aa, code));
                return;
            }
            foreach (var t in Debts(c, x))
            {
                if (FavourCalls.WhyNot(t) != null) continue;
                string about = FavoursOwed.IsPersonal(t) ? DipWords.P("CC_Dip_Menu_Personal", x.Named("OTHER")) : DipWords.P("CC_Dip_Menu_People", DipWords.People(Outsiders.PeopleOf(x)).Named("PEOPLE"));
                foreach (var o in FavourCalls.Options(t, home))
                {
                    string label = about + ": " + o.label;
                    if (o.why != null)
                    {
                        opts.Add(new FloatMenuOption("CC_Out_Disabled".Translate(label.Named("LABEL"), o.why.Named("WHY")).Resolve(), null));
                        continue;
                    }
                    int id = t.id;
                    string code = o.Code;
                    opts.Add(new FloatMenuOption(label, () =>
                    {
                        chosen[c.thingIDNumber] = (id, code);
                        Outsiders.StartTalk(c, x, a);
                    }));
                }
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("CC_Out_Nothing".Translate().Resolve(), null));
            Find.WindowStack.Add(new FloatMenu(opts, DipWords.P("CC_Dip_Menu_Title", x.Named("OTHER"))));
        }

        /// <summary>A pact of a kind (a trade pact with its cart), each greyed with why not; the colonist then walks over to speak of it.</summary>
        private static void ChoosePact(Pawn c, Pawn x, OutsiderAct a, Map home)
        {
            var opts = new List<FloatMenuOption>();
            var f = Outsiders.PeopleOf(x);
            foreach (PactKind k in Enum.GetValues(typeof(PactKind)))
            {
                string why = Pacts.WhyNot(f, home, k);
                string label = PactWords.P("CC_Pact_Topic", PactWords.Kind(k).Named("KIND"));
                if (why != null)
                {
                    opts.Add(new FloatMenuOption("CC_Out_Disabled".Translate(label.Named("LABEL"), why.Named("WHY")).Resolve(), null));
                    continue;
                }
                if (k == PactKind.Defence)
                {
                    opts.Add(new FloatMenuOption(label, () => Pick(c, x, a, PactMath.KindKey(k))));
                    continue;
                }
                foreach (var (def, count) in Pacts.CartGoods(home))
                {
                    string code = PactMath.KindKey(k) + "|" + def.defName + ":" + count;
                    opts.Add(new FloatMenuOption(label + ": " + PactWords.P("CC_Pact_CartOption", Couriers.Amount(count, null, def).Named("GOODS")), () => Pick(c, x, a, code)));
                }
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("CC_Out_Nothing".Translate().Resolve(), null));
            Find.WindowStack.Add(new FloatMenu(opts, PactWords.P("CC_Pact_Menu_Title", x.Named("OTHER"))));
        }

        private static void Pick(Pawn c, Pawn x, OutsiderAct a, string code)
        {
            chosen[c.thingIDNumber] = (-1, code);
            Outsiders.StartTalk(c, x, a);
        }

        /// <summary>The colonist's opening line and the moment for the words while they talk.</summary>
        public static string Opening(OutsiderAct a) =>
            a == OutsiderAct.SpeakMatch ? "matchask" : a == OutsiderAct.OfferExchange ? "exchangeask" : a == OutsiderAct.SpeakPact ? "pactask" : "favourask";

        public static string During(OutsiderAct a) =>
            a == OutsiderAct.SpeakMatch ? "matchtalk" : a == OutsiderAct.OfferExchange ? "exchangetalk" : a == OutsiderAct.SpeakPact ? "pacttalk" : "favourtalk";

        /// <summary>The talk is over (the two face to face): their answer, at once, in a word and a message.</summary>
        public static Job Talked(Pawn c, Pawn x, OutsiderAct a)
        {
            if (!chosen.TryGetValue(c.thingIDNumber, out var pick)) return null;
            chosen.Remove(c.thingIDNumber);
            if (a == OutsiderAct.SpeakPact) return Pact(c, x, a, pick.code);
            if (a == OutsiderAct.OfferExchange) return ExchangeActs.Talked(c, x, a, pick.code);
            if (a == OutsiderAct.SpeakMatch) return MatchActs.Talked(c, x, a, pick.code);
            var t = FavoursOwed.Get(pick.tie);
            if (t == null || FavourCalls.WhyNot(t) != null) return null;
            var home = c.MapHeld;
            var heard = FavourCalls.Hear(t, pick.code, home, c, false, Rand.Value);
            // together: asked and answered face to face, at the end of the talk that brought them together.
            Outsiders.Say(x, heard.honoured ? "favouryes" : "favourno", c);
            Outsiders.Remember(c, x, a, Outsiders.Note(a, c, heard.honoured ? DipWords.P("CC_Dip_Note_Yes") : DipWords.P("CC_Dip_Note_No")),
                heard.honoured ? 0.5f : -1f, heard.honoured ? 1f : -2f);
            // together: told as the two stand face to face; the message carries what follows on the map.
            Tell.Message(NewsKind.World, Importance.Important, home, heard.line, new LookTargets(x), heard.honoured ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent);
            return null;
        }

        /// <summary>A pact spoken of face to face: their answer at once, for their people (Pacts.Hear), in a word and a message.</summary>
        private static Job Pact(Pawn c, Pawn x, OutsiderAct a, string code)
        {
            var f = Outsiders.PeopleOf(x);
            var home = c.MapHeld;
            var parts = (code ?? "").Split('|');
            if (f == null || !PactMath.ParseKind(parts[0], out var k)) return null;
            ThingDef cart = null;
            int count = 0;
            if (parts.Length > 1)
            {
                var cp = parts[1].Split(':');
                cart = DefDatabase<ThingDef>.GetNamedSilentFail(cp[0]);
                if (cp.Length > 1) int.TryParse(cp[1], out count);
            }
            string line = Pacts.Hear(f, home, k, cart, count, Rand.Value, out bool yes);
            // together: asked and answered face to face, at the end of the talk that brought them together.
            Outsiders.Say(x, yes ? "pactyes" : "pactno", c);
            Outsiders.Remember(c, x, a, Outsiders.Note(a, c, yes ? PactWords.P("CC_Pact_Note_Yes") : PactWords.P("CC_Pact_Note_No")), yes ? 0.5f : -0.5f, yes ? 1f : -1f);
            // together: told as the two stand face to face; the message carries what follows.
            Tell.Message(NewsKind.World, Importance.Important, home, line, new LookTargets(x), yes ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent);
            return null;
        }
    }
}
