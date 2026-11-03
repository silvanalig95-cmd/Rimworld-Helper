using ColonyCouncil;

/// <summary>
/// The neighbours by name and the letters to them (World/NotableMath): which towns are within reach, how a notable
/// regards the colony and its people, how long a letter takes, how answers read and what they change, whom a call
/// reaches and when they call back, the road's dangers, a threat kept, and old age.
/// </summary>
public static class NotableTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "neighbours: " + what);
        }

        // ---------------- Who is within reach ----------------

        var near = NotableMath.InReach(new List<float> { 30f, 5f, 12f, 80f, 20f, 41f, 200f }, new List<bool> { false, false, false, true, false, false, true });
        C(near.Take(4).SequenceEqual(new[] { 1, 2, 4, 0 }), "the nearest four within reach, nearest first");
        C(!near.Contains(5), "a fifth within reach is left out");
        C(near.Contains(3) && near.Contains(6) && near.Count == 6, "towns the colony has dealt with are kept however far");
        var far = NotableMath.InReach(new List<float> { 70f, 50f, 120f }, null);
        C(far.SequenceEqual(new[] { 1, 0 }), "with nobody near, the nearest two within twice the reach");
        C(NotableMath.InReach(new List<float> { 500f }, null).Count == 0, "a lone town at the far side of the world is no neighbour");
        C(NotableMath.InReach(new List<float>(), null).Count == 0 && NotableMath.InReach(null, null).Count == 0, "no towns, no neighbours");
        var many = Enumerable.Range(0, 30).Select(i => (float)i).ToList();
        var dealtAll = Enumerable.Repeat(true, 30).ToList();
        C(NotableMath.InReach(many, dealtAll).Count == NotableMath.MaxTowns, $"never more than {NotableMath.MaxTowns} towns");
        C(NotableMath.MaxNotables == NotableMath.MaxTowns * NotableMath.RolesPerTown + NotableMath.MaxAcquaintances, "the notables' cap adds up");

        // ---------------- How they regard the colony ----------------

        C(NotableMath.Baseline(0, 0f, 0f, 0f, 0, false) == 0, "a stranger people with no history: civil");
        C(NotableMath.Baseline(80, 0f, 0f, 0f, 0, false) == 44, "goodwill carries over at a little over half");
        C(NotableMath.Baseline(0, 20f, 0f, 0f, 0, false) == 12 && NotableMath.Baseline(0, 200f, 0f, 0f, 0, false) == 30, "good turns warm them, up to a point");
        C(NotableMath.Baseline(0, 0f, 20f, 0f, 0, false) == -14 && NotableMath.Baseline(0, 0f, 200f, 0f, 0, false) == -40, "wrongs cool them more, up to a point");
        C(NotableMath.Baseline(0, 0f, 0f, 0f, 2, false) == 12 && NotableMath.Baseline(0, 0f, 0f, 0f, -2, false) == -12, "a kind nature warms, an abrasive one cools");
        C(NotableMath.Baseline(0, 0f, 0f, 0f, 9, false) == 12, "nature counts at most two steps");
        C(NotableMath.Baseline(50, 50f, 0f, 0f, 2, true) == -10, "a people at war is never better than cool");
        C(NotableMath.Baseline(-100, 0f, 200f, 0f, -2, false) == -100 && NotableMath.Baseline(100, 200f, 0f, 0f, 2, false) == 97, "it stays within -100 to 100");
        C(NotableMath.Baseline(0, 0f, 0f, 40f, 0, false) == -8, "a debt the colony owes them weighs a little");
        C(NotableMath.Opinion(90, 30f) == 100 && NotableMath.Opinion(-90, -30f) == -100 && NotableMath.Opinion(10, 5.4f) == 15, "what passed between them adds to it, within bounds");
        C(NotableMath.OfColonist(40, 0f, 0f) == 20, "a colonist they never met: half of how they see the colony");
        C(NotableMath.OfColonist(40, 20f, 0f) == 50 && NotableMath.OfColonist(40, 200f, 0f) == 60, "a colonist who did their people a good turn is liked more, up to a point");
        C(NotableMath.OfColonist(0, 0f, 15f) == 15, "letters between them count");
        C(Math.Abs(NotableMath.Fade(20f, NotableMath.HalfLifeDays) - 10f) < 0.01f && NotableMath.Fade(20f, 0f) == 20f && NotableMath.Fade(-8f, NotableMath.HalfLifeDays * 2) > -2.01f,
            "what passed fades by half each half-life, either way");
        int[] levels = { 100, 60, 59, 30, 29, 10, 9, -9, -10, -29, -30, -59, -60, -100 };
        int[] want = { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6 };
        C(levels.Select(NotableMath.Level).SequenceEqual(want), "regard in seven words, at the right lines");
        C(NotableMath.CanAskHelp(25, false) && !NotableMath.CanAskHelp(24, false) && !NotableMath.CanAskHelp(90, true), "help is asked only of those who think well enough of the colony, never at war");
        C(!NotableMath.CanInvite(true) && NotableMath.CanInvite(false), "nobody at war is invited");

        // ---------------- How the words go ----------------

        C(!NotableMath.Callable(NotableMath.Neolithic) && !NotableMath.Callable(NotableMath.Medieval) && NotableMath.Callable(NotableMath.Industrial)
          && NotableMath.Callable(NotableMath.Spacer), "only peoples with the means to answer can be called");
        C(NotableMath.Traditional(NotableMath.Neolithic, false) && NotableMath.Traditional(NotableMath.Medieval, false) && !NotableMath.Traditional(NotableMath.Industrial, false)
          && NotableMath.Traditional(NotableMath.Spacer, true), "tribes, medieval folk and a feudal empire set store by a letter in a hand");
        foreach (LetterPurpose p in Enum.GetValues(typeof(LetterPurpose)))
        {
            C(NotableMath.ChannelFactor(p, LetterChannel.Console, false) < NotableMath.ChannelFactor(p, LetterChannel.Courier, false), $"{p}: a call counts for less than a letter");
            C(NotableMath.ChannelFactor(p, LetterChannel.Courier, true) >= NotableMath.ChannelFactor(p, LetterChannel.Courier, false), $"{p}: a letter counts for as much or more to a traditional people");
            C(NotableMath.WaitDays(p) >= 3f, $"{p}: a wait before writing for it again");
        }
        C(NotableMath.ChannelFactor(LetterPurpose.Condolences, LetterChannel.Console, false) < NotableMath.ChannelFactor(LetterPurpose.News, LetterChannel.Console, false),
            "a call costs more warmth on a personal matter than on business");
        C(NotableMath.ChannelFactor(LetterPurpose.Condolences, LetterChannel.Courier, true) > NotableMath.ChannelFactor(LetterPurpose.Condolences, LetterChannel.Courier, false),
            "condolences by hand mean most to a traditional people");
        C(NotableMath.TravelDays(10f, NotableMath.Neolithic) > NotableMath.TravelDays(10f, NotableMath.Medieval)
          && NotableMath.TravelDays(10f, NotableMath.Medieval) > NotableMath.TravelDays(10f, NotableMath.Industrial)
          && NotableMath.TravelDays(10f, NotableMath.Industrial) > NotableMath.TravelDays(10f, NotableMath.Spacer), "a runner is slower than a rider, a rider than a truck, a truck than a shuttle");
        C(NotableMath.TravelDays(0f, NotableMath.Spacer) == 0.75f && NotableMath.TravelDays(500f, NotableMath.Neolithic) == 12f, "a letter takes most of a day at least, twelve days at most");
        C(NotableMath.TravelDays(20f, NotableMath.Industrial) > NotableMath.TravelDays(5f, NotableMath.Industrial), "farther takes longer");
        C(NotableMath.ReplyDays(0f) == 0.25f && NotableMath.ReplyDays(1f) == 1.25f && NotableMath.ReplyDays(7f) == 1.25f, "they take a quarter of a day to a day and a quarter to answer");
        C(NotableMath.Personal(LetterPurpose.Greetings) && NotableMath.Personal(LetterPurpose.Condolences) && NotableMath.Personal(LetterPurpose.Invitation)
          && !NotableMath.Personal(LetterPurpose.News) && !NotableMath.Personal(LetterPurpose.Help) && !NotableMath.Personal(LetterPurpose.Warning), "which matters are personal");

        // ---------------- The answer ----------------

        LetterInput I(LetterPurpose p, int op, int writer = 0, int social = 6, int warmth = 0, bool hostile = false, LetterChannel c = LetterChannel.Courier,
            bool traditional = false, bool troubled = false, bool lately = false) =>
            new LetterInput { purpose = p, opinion = op, ofWriter = writer, social = social, warmth = warmth, hostile = hostile, channel = c, traditional = traditional, troubled = troubled, lately = lately };

        C(NotableMath.Answer(I(LetterPurpose.Greetings, 70, 60), 0.5f, 0.5f) == AnswerTone.Warm, "a friend writing to a friend: a warm answer");
        C(NotableMath.Answer(I(LetterPurpose.Greetings, 0), 0.5f, 0.5f) == AnswerTone.Cool, "a stranger's greetings: a cool answer");
        C(NotableMath.Answer(I(LetterPurpose.Greetings, -70, -40), 0.5f, 0.5f) == AnswerTone.Refusal, "a people that loathes the colony refuses even greetings");
        C(NotableMath.Score(I(LetterPurpose.Greetings, 20, 20, 15)) > NotableMath.Score(I(LetterPurpose.Greetings, 20, 20, 2)), "a good talker writes a better letter");
        C(NotableMath.Score(I(LetterPurpose.Greetings, 20, 20, warmth: 2)) > NotableMath.Score(I(LetterPurpose.Greetings, 20, 20, warmth: -2)), "a kind reader takes it better");
        C(NotableMath.Score(I(LetterPurpose.Greetings, 20, 50)) > NotableMath.Score(I(LetterPurpose.Greetings, 20, -20)), "who writes matters: one they like does better");
        C(NotableMath.Score(I(LetterPurpose.Condolences, 20, c: LetterChannel.Courier, traditional: true)) > NotableMath.Score(I(LetterPurpose.Condolences, 20, c: LetterChannel.Courier))
          && NotableMath.Score(I(LetterPurpose.Condolences, 20, c: LetterChannel.Courier)) > NotableMath.Score(I(LetterPurpose.Condolences, 20, c: LetterChannel.Console)),
            "condolences land best by hand to a traditional people, worst by a call");
        C(NotableMath.Score(I(LetterPurpose.Greetings, 20, lately: true)) < NotableMath.Score(I(LetterPurpose.Greetings, 20)), "writing for the same thing again soon counts for less");
        C(NotableMath.Score(I(LetterPurpose.Help, 40, troubled: true)) < NotableMath.Score(I(LetterPurpose.Help, 40)), "a people in hard times has less to spare");
        // Luck moves it, both ways, but only so far.
        var mid = I(LetterPurpose.Greetings, 30, 30);
        C(NotableMath.Answer(mid, 0f, 0.5f) > NotableMath.Answer(mid, 1f, 0.5f), "luck moves the answer (a lower tone is a colder one)");
        C(NotableMath.Answer(I(LetterPurpose.Greetings, 90, 90, 20, 2), 0f, 0.5f) <= AnswerTone.Cordial, "a dear friend never answers coldly, even on a bad day");
        // A people at war.
        var war = I(LetterPurpose.Greetings, 60, 60, 20, 2, true);
        C(NotableMath.SilenceChance(war) >= 0.15f && NotableMath.SilenceChance(war) <= 0.6f && NotableMath.SilenceChance(I(LetterPurpose.Greetings, 60)) == 0f,
            "only a people at war keeps silent, and then not always");
        C(NotableMath.SilenceChance(I(LetterPurpose.Greetings, -100, hostile: true)) > NotableMath.SilenceChance(I(LetterPurpose.Greetings, 0, hostile: true)), "the more they hate the colony, the likelier silence");
        C(NotableMath.Answer(war, 1f, 0f) == AnswerTone.Silence, "a people at war can keep silent");
        C(NotableMath.Answer(war, 1f, 0.99f) == AnswerTone.Cordial, "a people at war never answers warmly");
        bool anyWarm = false;
        for (float r = 0f; r <= 1f; r += 0.05f)
            for (int op = -100; op <= 100; op += 20)
                anyWarm |= NotableMath.Answer(I(LetterPurpose.Greetings, op, op, 20, 2, true), r, 0.99f) == AnswerTone.Warm;
        C(!anyWarm, "no luck makes a people at war answer warmly");
        // Help.
        foreach (float r in new[] { 0f, 0.3f, 0.6f, 1f })
        {
            var t = NotableMath.Answer(I(LetterPurpose.Help, 30), r, 0.5f);
            C(t == AnswerTone.Warm || t == AnswerTone.Cordial || t == AnswerTone.Refusal, $"help asked: given or refused, nothing between (roll {r})");
        }
        C(NotableMath.Grants(I(LetterPurpose.Help, 60), AnswerTone.Cordial) && !NotableMath.Grants(I(LetterPurpose.Help, 60), AnswerTone.Cool)
          && !NotableMath.Grants(I(LetterPurpose.Help, 60, hostile: true), AnswerTone.Warm), "help is given on a warm or kind answer, never by a people at war");
        // What it changes.
        C(NotableMath.OpinionShift(AnswerTone.Warm, LetterPurpose.Greetings, 1f, false) > NotableMath.OpinionShift(AnswerTone.Cordial, LetterPurpose.Greetings, 1f, false)
          && NotableMath.OpinionShift(AnswerTone.Cordial, LetterPurpose.Greetings, 1f, false) > NotableMath.OpinionShift(AnswerTone.Cool, LetterPurpose.Greetings, 1f, false)
          && NotableMath.OpinionShift(AnswerTone.Refusal, LetterPurpose.Greetings, 1f, false) < 0f, "a warm answer moves them most, a refusal sets them back");
        C(NotableMath.OpinionShift(AnswerTone.Warm, LetterPurpose.Condolences, 1.3f, false) > NotableMath.OpinionShift(AnswerTone.Warm, LetterPurpose.Greetings, 1f, false),
            "condolences well taken mean the most");
        C(NotableMath.OpinionShift(AnswerTone.Warm, LetterPurpose.Greetings, 1f, true) < NotableMath.OpinionShift(AnswerTone.Warm, LetterPurpose.Greetings, 1f, false), "the same again soon counts for less");
        C(NotableMath.OpinionShift(AnswerTone.Warm, LetterPurpose.Help, 1f, false) < NotableMath.OpinionShift(AnswerTone.Warm, LetterPurpose.Greetings, 1f, false), "asking for help costs a little");
        C(NotableMath.OpinionShift(AnswerTone.Silence, LetterPurpose.Greetings, 1f, false) == 0f, "silence changes nothing");
        C(NotableMath.WriterShift(4f) == 6f, "the writer is remembered half again as much");
        C(NotableMath.GoodwillShift(AnswerTone.Warm, LetterPurpose.Greetings, false) == 2 && NotableMath.GoodwillShift(AnswerTone.Cool, LetterPurpose.Greetings, false) == 0
          && NotableMath.GoodwillShift(AnswerTone.Refusal, LetterPurpose.Greetings, false) == 0, "goodwill moves a little with a good answer, never down");
        C(NotableMath.GoodwillShift(AnswerTone.Cordial, LetterPurpose.Warning, false) > NotableMath.GoodwillShift(AnswerTone.Cordial, LetterPurpose.Greetings, false), "a warning heeded counts more");
        C(NotableMath.GoodwillShift(AnswerTone.Cordial, LetterPurpose.Greetings, true) > NotableMath.GoodwillShift(AnswerTone.Cordial, LetterPurpose.Greetings, false), "a kind word across a war counts more");
        C(NotableMath.GoodwillShift(AnswerTone.Warm, LetterPurpose.Help, false) <= 1, "help given wins little goodwill: it's a favor owed");
        C(NotableMath.Gift(AnswerTone.Warm, LetterPurpose.Greetings, 60, 0f) && !NotableMath.Gift(AnswerTone.Warm, LetterPurpose.Greetings, 60, 0.9f)
          && !NotableMath.Gift(AnswerTone.Cordial, LetterPurpose.Greetings, 60, 0f) && !NotableMath.Gift(AnswerTone.Warm, LetterPurpose.Greetings, 20, 0f)
          && !NotableMath.Gift(AnswerTone.Warm, LetterPurpose.Help, 90, 0f), "a gift now and then, only with a warm answer from a friend");
        C(NotableMath.HeirSucceeds(NotableRole.Head, true) && !NotableMath.HeirSucceeds(NotableRole.Head, false) && !NotableMath.HeirSucceeds(NotableRole.Envoy, true),
            "the heir takes a head's place, nobody else's");

        // ---------------- The road ----------------

        C(NotableMath.WaylaidChance(true) > NotableMath.WaylaidChance(false) && NotableMath.WaylaidChance(false) > 0f && NotableMath.WaylaidChance(true) <= 0.2f,
            "a messenger is set upon now and then, more on a dangerous road, never often");
        C(NotableMath.ThreatChance(AnswerTone.Refusal, true) > NotableMath.ThreatChance(AnswerTone.Curt, true) && NotableMath.ThreatChance(AnswerTone.Curt, true) > 0f
          && NotableMath.ThreatChance(AnswerTone.Cool, true) == 0f && NotableMath.ThreatChance(AnswerTone.Refusal, false) == 0f && NotableMath.ThreatChance(AnswerTone.Refusal, true) <= 0.25f,
            "a threat comes only from a people at war, on a cold answer, now and then");

        // ---------------- A call ----------------

        int asleep = 0, atOnce = 0, busy = 0;
        for (float r = 0f; r < 1f; r += 0.01f)
        {
            if (NotableMath.Reach(2, false, false, r) == CallReach.Asleep) asleep++;
            if (NotableMath.Reach(14, false, false, r) == CallReach.AtOnce) atOnce++;
            if (NotableMath.Reach(14, true, false, r) == CallReach.Busy) busy++;
        }
        C(asleep >= 80, $"in the small hours they're mostly asleep ({asleep} of 100)");
        C(atOnce >= 80, $"by day, at peace, they mostly answer at once ({atOnce} of 100)");
        C(busy >= 40 && busy <= 60, $"with their people in trouble, half the time an aide answers ({busy} of 100)");
        C(NotableMath.Reach(14, false, true, 0.2f) == CallReach.Away && NotableMath.Reach(14, false, false, 0.2f) == CallReach.AtOnce, "a people's leader is away more often");
        C(NotableMath.CallbackHours(CallReach.Asleep, 2, 0f) == 6f && NotableMath.CallbackHours(CallReach.Asleep, 23, 0f) == 9f, "asleep, they call back after their morning");
        C(NotableMath.CallbackHours(CallReach.Asleep, 8, 0f) >= 1f, "never a callback at once");
        C(NotableMath.CallbackHours(CallReach.Away, 12, 0f) == 3f && NotableMath.CallbackHours(CallReach.Away, 12, 1f) == 10f, "away, a few hours to most of a day");
        C(NotableMath.CallbackHours(CallReach.Busy, 12, 1f) == 20f && NotableMath.CallbackHours(CallReach.AtOnce, 12, 0.5f) == 0f, "busy takes longest; answered at once, no callback");

        // ---------------- Old age ----------------

        C(NotableMath.DeathChancePerDay(40f) == 0f && NotableMath.DeathChancePerDay(59.9f) == 0f, "nobody dies of old age before sixty");
        C(NotableMath.DeathChancePerDay(70f) > NotableMath.DeathChancePerDay(60f) && NotableMath.DeathChancePerDay(80f) > NotableMath.DeathChancePerDay(70f), "the older, the likelier");
        C(Math.Abs(NotableMath.DeathChancePerDay(70f) * 60f - 0.16f) < 0.001f, "about one in six a year at seventy");
        C(NotableMath.DeathChancePerDay(200f) * 60f <= 0.5f, "never more than half a year's chance");
        // Over a long game: a notable of 65 lives about ten more years on average, rarely past ninety.
        var rng = new Random(7);
        var lived = new List<int>();
        for (int k = 0; k < 2000; k++)
        {
            float age = 65f;
            int days = 0;
            while (days < 60 * 60)
            {
                if (rng.NextDouble() < NotableMath.DeathChancePerDay(age)) break;
                days++;
                age += 1f / 60f;
            }
            lived.Add(days);
        }
        double years = lived.Average() / 60.0;
        C(years > 5 && years < 15, $"a head of 65 lives on {years:0.0} years on average");

        return n;
    }
}
