using System;
using System.Linq;
using ColonyCouncil;

/// <summary>The wardens' post as a trading stop (Camps/RoadPostMath.cs): the carters' price, and the tags of word from the road.</summary>
public static class RoadPostTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void Check(bool ok, string what)
        {
            n++;
            check(ok, what);
        }
        Check(RoadPostMath.Price(30, 1.1f) == 30, "thirty raw food at a market value of 1.1 fetches about 26 silver, rounded to 30");
        Check(RoadPostMath.Price(20, 2.5f) == 40, "twenty hides at 2.5 fetch 40 silver (eight tenths of their value)");
        Check(RoadPostMath.Price(1, 1f) == 10, "the carters never pay less than ten");
        Check(RoadPostMath.Price(0, 5f) == 0 && RoadPostMath.Price(10, 0f) == 0, "nothing sent, or worth nothing, fetches nothing");
        Check(RoadPostMath.Price(100, 10f) == 800, "a hundred at ten is eight hundred");
        Check(RoadPostMath.PriceShare < 1f && RoadPostMath.PriceShare > 0.5f, "the road price is below the market's but more than half of it");
        Check(RoadPostMath.DueDaysMin >= 2 && RoadPostMath.DueDaysMax >= RoadPostMath.DueDaysMin, "the carters take days to come back through");

        Check(RoadPostMath.WordTags(null).SequenceEqual(new[] { "quiet" }), "no world event running: word of a quiet road");
        Check(RoadPostMath.WordTags("").SequenceEqual(new[] { "quiet" }), "an event with no kind counts as none");
        Check(RoadPostMath.WordTags("war").SequenceEqual(new[] { "ws:war" }), "a war running: the war's lines");
        Check(RoadPostMath.WordLines >= 2 && RoadPostMath.WordLines <= 4, "a few lines of word at a time");
        return n;
    }
}
