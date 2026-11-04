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
    /// words said in turn). Ask a favour of their people now (a debt of honour they owe the colony, or one they owe the
    /// colonist in person, called in face to face); speak of a pact, speak for a match, offer an exchange and the keeper's
    /// sounding out come with their releases (DiplomacyMath.Acts). Their answer is given face to face, at once.
    /// </summary>
    public static class DiplomacyActs
    {
        /// <summary>The acts this release offers, in the menu's order after Invite.</summary>
        public static readonly OutsiderAct[] Acts = { OutsiderAct.AskFavour };

        public static bool Owns(OutsiderAct a) => a == OutsiderAct.AskFavour;

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
            Diplomacy.FavoursOn && !c.DevelopmentalStage.Juvenile() && !x.DevelopmentalStage.Juvenile() && Outsiders.PeopleOf(x) != null && !x.IsQuestLodger();

        /// <summary>Why it can't be asked of them now, or null (after the outsiders' own reasons: busy, tired of the colony).</summary>
        public static string WhyNot(Pawn c, Pawn x, OutsiderAct a)
        {
            var f = Outsiders.PeopleOf(x);
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

        public static string Label(Pawn c, Pawn x, OutsiderAct a) => DipWords.P("CC_Out_Act_AskFavour");

        /// <summary>The player picks what to ask, for which debt; the colonist then walks over to ask it.</summary>
        public static void Choose(Pawn c, Pawn x, OutsiderAct a)
        {
            var opts = new List<FloatMenuOption>();
            var home = c.MapHeld;
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

        /// <summary>The colonist's opening line and the moment for the words while they talk.</summary>
        public static string Opening(OutsiderAct a) => "favourask";

        public static string During(OutsiderAct a) => "favourtalk";

        /// <summary>The talk is over (the two face to face): their answer, at once, in a word and a message.</summary>
        public static Job Talked(Pawn c, Pawn x, OutsiderAct a)
        {
            if (!chosen.TryGetValue(c.thingIDNumber, out var pick)) return null;
            chosen.Remove(c.thingIDNumber);
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
    }
}
