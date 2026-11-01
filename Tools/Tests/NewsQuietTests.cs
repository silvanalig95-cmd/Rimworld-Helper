using System;
using ColonyCouncil;

/// <summary>A quiet day's evening news (UI/NewsQuietMath.cs): when a day counts as quiet, and its lines joined into one short digest.</summary>
public static class NewsQuietTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "quiet news: " + what);
        }
        C(NewsQuietMath.Quiet(2, 0, 120) && NewsQuietMath.Quiet(NewsQuietMath.MaxItems, 0, NewsQuietMath.MaxChars), "two to four short pieces, none bad: a quiet day");
        C(!NewsQuietMath.Quiet(1, 0, 40) && !NewsQuietMath.Quiet(0, 0, 0), "a single piece is a message already, and no news is no digest");
        C(!NewsQuietMath.Quiet(NewsQuietMath.MaxItems + 1, 0, 200), "five pieces make a busy day, which keeps its letter");
        C(!NewsQuietMath.Quiet(3, 1, 150), "a day with bad news in it is never folded away");
        C(!NewsQuietMath.Quiet(3, 0, NewsQuietMath.MaxChars + 1), "lines too long for one message keep their letter");
        C(NewsQuietMath.Join(new[] { "Hector carved a spoon.", "  The first snow fell", "", null, "Is it spring yet?" }) == "Hector carved a spoon. The first snow fell. Is it spring yet?",
            "the lines run on as sentences, each with its stop, the empty ones left out");
        C(NewsQuietMath.Join(null) == "" && NewsQuietMath.Join(new string[0]) == "", "nothing to join, nothing said");
        return n;
    }
}
