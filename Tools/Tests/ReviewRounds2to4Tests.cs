using System;
using System.IO;
using System.Text.RegularExpressions;

/// <summary>
/// The review of content rounds 2 to 4 before they meet the player (docs/review-rounds-2-to-4.md): the bugs found in what
/// would show in play, each kept fixed. The ordeal at the hearth only before the colony at its gathering; the clothes from
/// the stores only for those who can walk there; an offering taken only while it lies at the god's place; the stone-raisers'
/// carved stone only on clear ground; the one who hid not left on the land for good; the storm's leavings not held from the
/// haulers once nothing waits for the sea; a beast's madness counted in the shared budget; the arrest asked only of one up
/// to it; the round's caches of one game only.
/// </summary>
public static class ReviewRounds2to4Tests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "review of rounds 2 to 4: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));

        string Between(string text, string from, string to)
        {
            int a = text.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return "";
            int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
        }

        // The ordeal: resolved only while its gathering runs with both of them at it; none, or one over, and nobody is burned.
        var council2 = Code("Dilemmas/DilemmaWorkers_Round2.cs");
        var ordeal = Between(council2, "public static class OrdealFire", "private static void Resolve(");
        C(ordeal.Contains("if (play == null || play.state == PlayState.Done) return true;") && ordeal.Contains("if (play.state == PlayState.Pending) return now > k.until;")
          && ordeal.IndexOf("Resolve(k);", StringComparison.Ordinal) > ordeal.IndexOf("play.invited.Contains(p)", StringComparison.Ordinal),
            "the ordeal is held only at its gathering before the colony, never with no gathering or after it");

        // The clothes from the stores: only those who can walk there now are sent.
        var events3 = Code("Happenings/Workers_Round3.cs");
        var spares = Between(events3, "public static List<(Pawn p, Apparel spare)> Spares(", "private static RecipeDef Recipe(");
        C(spares.Contains("p.Downed || p.Drafted || p.InMentalState"), "the clothes from the stores: nobody downed, drafted or out of their mind is sent to change");

        // The offering taken: only while it lies there on the map.
        var home = Code("Gods/OnMap/Between/BetweenFaithHome.cs");
        var taken = Between(home, "class FaithTakenOffering", "private static void Keep(");
        C(taken.Contains("o.thing == null || !o.thing.Spawned || o.thing.Map != map"), "an offering is taken in the night only while it lies at the god's place");

        // The stone-raisers' carved stone: the ground clear of everything and everyone before their stone is used up.
        var camps2 = Code("Camps/CampRound2.cs");
        var stand = Between(camps2, "public static void Stand(Camp c, StoneState st, Building built)", "private static void Rite(");
        int clear = stand.IndexOf("x.GetFirstPawn(map) == null", StringComparison.Ordinal);
        int used = stand.IndexOf("t.Destroy();", StringComparison.Ordinal);
        C(clear > 0 && used > clear, "the carved stone waits for clear ground (nobody on it) before the stone they brought is used");
        C(camps2.Contains("GenAdj.OccupiedRect(st.site, Rot4.North, st.Def.size).Contains(at)"), "the carvers work beside the stone, never on the ground it will stand on");

        // The one who hid: sent off the land when it ends with them still about, nobody's and free.
        var events2 = Code("Happenings/Workers_Round2.cs");
        var hidden = Between(events2, "public static class Hidden", "// D22. Stuck in the bog");
        var hiddenEnd = Between(hidden, "public static void End(TimedThing k)", "// D22");
        C(hiddenEnd.Contains("Round2Map.Leave(x)") && hiddenEnd.Contains("x.IsPrisoner") && hiddenEnd.Contains("x.Faction != null"), "the one who hid doesn't stay on the land for good when it ends");
        C(events2.Contains("private static bool Arrester(Pawn a, Map map)") && events2.Contains("!Arrester(ctx.a, ctx.map)") && events2.Contains("!Arrester(ctx.a, map)"),
            "the arrest is asked only of one here and up to it");

        // A beast's madness: counted in the shared budget as one that turns on someone.
        var mad = Between(events2, "private static void Mad(TimedThing k, Pawn x)", "public static void End(");
        C(mad.Contains("HarmBook.Instance?.Came(HarmKinds.TooClose"), "a wild animal gone mad is counted in the shared harm budget");

        // The storm's leavings: let go to the haulers when it ends before the sea took them.
        var events4 = Code("Happenings/Workers_Round4.cs");
        var r4End = Between(events4, "public static void End(TimedThing k, bool early)", "public static TimedThing Of(");
        C(r4End.Contains("case TimedKind.R4EShore:") && r4End.Contains("SetForbidden(false, false)"), "what the storm threw up isn't left forbidden when it ends early");

        // The round's caches: of one game only.
        var lives = Code("Arcs/Round4Lives.cs");
        C(lives.Contains("class R4LGameCache<TKey, TValue>") && lives.Contains("if (g != game)") && !Regex.IsMatch(lives, @"static readonly Dictionary<"),
            "the round's caches of maps and wishes are of one game only (map and wish ids start again in another)");

        return n;
    }
}
