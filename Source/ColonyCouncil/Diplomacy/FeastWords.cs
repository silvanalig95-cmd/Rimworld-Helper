using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The words of feasts and contests with guests (Languages/English/Keyed/Rimfolk_Feasts.xml): the feast by both sides'
    /// ages (the colony's word by its own age: a gift feast, a feast in the hall, a banquet, a gala; their way by theirs:
    /// Ages.Form, the feature Feast), their faith's own feast where Ideology gives them one (InviteForms.Rite), the contests,
    /// what each people hopes for, and the guests named together. Lines told often have numbered fellows (CC_X_2...), one
    /// picked at random.
    /// </summary>
    public static class FeastWords
    {
        public static string T(string key, params NamedArgument[] args) => DipWords.T(key, args);

        public static string P(string key, params NamedArgument[] args) => DipWords.P(key, args);

        public static string People(Faction f) => DipWords.People(f);

        /// <summary>The feast's word by the colony's age and their way by theirs.</summary>
        public static AgeForm Form(Map home, Faction f) => Ages.Form(AgeFeature.Feast, home ?? Homes.Seat, f);

        /// <summary>The colony's feast in its own words, with "the": "the feast in the hall", "the gala" ({FEAST}).</summary>
        public static string Name(Map home) => P("CC_Feast_Name_" + Ages.Of(home ?? Homes.Seat));

        /// <summary>How their people keep a feast: their faith's own (Ideology), else their age's way; and whether it is their faith's.</summary>
        public static string Rite(Faction f, Map home, out bool faith)
        {
            faith = false;
            return InviteForms.Rite(f, home ?? Homes.Seat, "feast", out faith) ?? Form(home, f).rite;
        }

        /// <summary>A contest in a word: "wrestling", "a race", "a hunt", "a game at the tables", "a shooting match".</summary>
        public static string Contest(ContestKind k) => P("CC_Feast_Contest_" + k);

        /// <summary>A contest's name to begin a line: "Wrestling", "The race".</summary>
        public static string ContestThe(ContestKind k) => P("CC_Feast_ContestThe_" + k);

        /// <summary>What a people hopes for at the colony's feast, in words.</summary>
        public static string Hope(FeastHope h) => h == FeastHope.None ? "" : P("CC_Feast_Hope_" + h);

        public static string Contests(IEnumerable<ContestKind> list)
        {
            var words = list?.Select(Contest).ToList() ?? new List<string>();
            return words.Count == 0 ? P("CC_Feast_NoContests") : TextUtil.JoinAnd(words);
        }

        /// <summary>The guests by name: "Ula, Bram and Tess".</summary>
        public static string Guests(GuestFeast p, bool onlyComing = false)
        {
            var names = p?.guests.Where(g => g != null && (!onlyComing || g.state == GuestState.Coming || g.state == GuestState.Here || g.state == GuestState.Gone))
                .Select(g => g.pawn != null && !g.pawn.Discarded ? g.pawn.LabelShort : g.name ?? "?").ToList() ?? new List<string>();
            return names.Count == 0 ? P("CC_Feast_NoGuests") : TextUtil.JoinAnd(names);
        }

        /// <summary>Their peoples by name: "the Ashfall Clans and Tallbrook's people".</summary>
        public static string Peoples(GuestFeast p, bool onlyComing = false)
        {
            var names = p?.guests.Where(g => g?.people != null && (!onlyComing || g.state != GuestState.Declined && g.state != GuestState.Invited))
                .Select(g => g.people).Distinct().Select(People).ToList() ?? new List<string>();
            return names.Count == 0 ? P("CC_Feast_NoGuests") : TextUtil.JoinAnd(names);
        }

        /// <summary>A guest's name, or the name kept.</summary>
        public static string Name(FeastGuest g) => g == null ? "?" : g.pawn != null && !g.pawn.Discarded ? g.pawn.LabelShort : g.name ?? "?";

        /// <summary>One contest's outcome in a sentence ("Ana won the race, against five runners.").</summary>
        public static string Result(FeastResult r)
        {
            if (r == null) return "";
            if (r.off) return P("CC_Feast_Result_Off", ContestThe(r.kind).Named("CONTEST"), (r.why ?? "").Named("WHY"));
            if (r.winnerName.NullOrEmpty()) return P("CC_Feast_Result_None_" + r.kind);
            string who = r.ours ? r.winnerName : P("CC_Feast_WhoOf", r.winnerName.Named("NAME"), People(r.people).Named("PEOPLE"));
            return P("CC_Feast_Result_" + r.kind, who.Named("WINNER"), r.count.Named("COUNT"), r.entrants.Named("ENTRANTS"), (r.what ?? Contest(r.kind)).Named("WHAT"));
        }

        /// <summary>Every outcome, one line each.</summary>
        public static string Results(GuestFeast p) =>
            p == null || p.results.Count == 0 ? "" : string.Join("\n", p.results.Select(r => "- " + Result(r)));
    }
}
