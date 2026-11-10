using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ColonyCouncil
{
    /// <summary>
    /// "Speak for a match" face to face with one of theirs at the colony (Outsiders' right-click menu, DiplomacyActs): the
    /// player picks which of the colony's to match with them and where the couple would live, each with how the colonist
    /// feels about it; the colonist selected walks over to say it (two who courted their way to lovers speak for
    /// themselves), and their answer comes at once (Matches.Hear). A colonist against it is heard by the council first.
    /// </summary>
    public static class MatchActs
    {
        public static bool Shown(Pawn c, Pawn x) =>
            Matches.On && c != null && x != null && !c.DevelopmentalStage.Juvenile() && !x.DevelopmentalStage.Juvenile() && Outsiders.PeopleOf(x) != null && !x.IsQuestLodger()
            && !x.IsPrisoner && x.Faction == Outsiders.PeopleOf(x);

        /// <summary>The colony's own at the home who would fit them (the colonist speaking first).</summary>
        private static List<Pawn> Fits(Pawn c, Pawn x)
        {
            var f = Outsiders.PeopleOf(x);
            var list = Matches.Colonists(c.MapHeld).Where(o => Matches.WhyNotPair(o, x, f) == null && Matches.TermsWhyNot(f, c.MapHeld, o, x, null) == null).ToList();
            list.Sort((a, b) => (b == c ? 1 : 0).CompareTo(a == c ? 1 : 0));
            return list;
        }

        public static string WhyNot(Pawn c, Pawn x)
        {
            var f = Outsiders.PeopleOf(x);
            string why = Matches.SeekWhyNot(f, c.MapHeld);
            if (why != null) return why;
            if (Fits(c, x).Count > 0) return null;
            return Matches.WhyNotPair(c, x, f) ?? Matches.TermsWhyNot(f, c.MapHeld, c, x, null) ?? MatchWords.P("CC_Match_Why_NoneFitsThem", x.Named("OTHER"));
        }

        /// <summary>Whom to match with them and where they would live, each with how the colonist feels; a pick sends the colonist over (the code "OURS|Home").</summary>
        public static void Choose(Pawn c, Pawn x, OutsiderAct a, Action<Pawn, Pawn, OutsiderAct, string> pick)
        {
            var opts = new List<FloatMenuOption>();
            string town = Outsiders.TownOf(x)?.Name ?? MatchWords.People(Outsiders.PeopleOf(x));
            foreach (var o in Fits(c, x).Take(6))
                foreach (MatchHome h in Enum.GetValues(typeof(MatchHome)))
                {
                    var consent = Matches.ConsentOf(o, x, h, out var why, out var other);
                    string label = MatchWords.Lives(h, o.LabelShort, x.LabelShort, town).CapitalizeFirst() + " (" + MatchWords.ConsentLine(o, consent, why, other).TrimEnd('.') + ")";
                    string code = o.thingIDNumber + "|" + h;
                    opts.Add(new FloatMenuOption(label, () => pick(c, x, a, code)));
                }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("CC_Out_Nothing".Translate().Resolve(), null));
            Find.WindowStack.Add(new FloatMenu(opts, MatchWords.P("CC_Match_Menu_Title", x.Named("OTHER"))));
        }

        /// <summary>The talk is over (the two face to face): their answer at once, in a word and a message.</summary>
        public static Job Talked(Pawn c, Pawn x, OutsiderAct a, string code)
        {
            var f = Outsiders.PeopleOf(x);
            var home = c.MapHeld;
            var parts = (code ?? "").Split('|');
            if (f == null || parts.Length != 2 || !int.TryParse(parts[0], out int id) || !Enum.TryParse(parts[1], out MatchHome lives)) return null;
            var ours = Matches.Colonists(home).FirstOrDefault(o => o.thingIDNumber == id);
            if (ours == null) return null;
            bool lovers = LovePartnerRelationUtility.LovePartnerRelationExists(ours, x);
            var n = NotableBook.Instance?.notables.FirstOrDefault(o => o != null && o.pawn == x);
            string line = Matches.Hear(f, home, ours, x, n, null, Gender.None, lives, lovers ? MatchWay.Courted : MatchWay.Sought, Rand.Value, out bool yes);
            // together: asked and answered face to face, at the end of the talk that brought them together.
            Outsiders.Say(x, yes ? "matchyes" : "matchno", c);
            Outsiders.Remember(c, x, a, Outsiders.Note(a, c, yes ? MatchWords.P("CC_Match_Note_Yes") : MatchWords.P("CC_Match_Note_No")), yes ? 1f : -0.5f, yes ? 2f : -1f);
            // together: told as the two stand face to face; the message carries what follows on the map.
            Tell.Message(NewsKind.World, Importance.Important, home, line, new LookTargets(x), yes ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent);
            return null;
        }
    }
}
