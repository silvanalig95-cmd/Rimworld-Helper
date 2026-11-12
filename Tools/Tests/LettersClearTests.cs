using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Letters and favours made clear (the player, 11 November 2026): "Write back" greyed on the very answer ("i'm a bit
/// confused about when i can reply to a message and when not? this one just came in but i can't reply to it"), answers
/// that came before a letter could arrive ("once the courier is off the map I already get a pop up ... It should always
/// take the necessary time for stuff to actually get delivered, depending on the distance"), and calling in a favour
/// ("this favour menu is also a bit overwhelming and i'm not really sure what many of these options really do. also 4
/// yoghurts is a funny option? ... this guy is a trader"). Letters/LetterWaitMath.cs and Diplomacy/FavourMath.cs: what the
/// colony waits on for a letter and when another can go, the road from when the carrier leaves the map, silence known
/// only once an answer would have come, goods by who owes, a word with the peoples it would help, the short menu; the
/// player's own save; the words; the seams.
/// </summary>
public static class LettersClearTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void C(bool ok, string what)
        {
            cases++;
            check(ok, "letters and favours made clear: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var keys = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(f).Root!.Elements())
                keys[e.Name.LocalName] = e.Value;
        Waits(C);
        Road(C);
        Goods(C);
        Words(C);
        Menu(C);
        Texts(C, keys);
        Seams(root, Code, C);
        return cases;
    }

    // ================= What the colony waits on =================

    private static void Waits(Action<bool, string> C)
    {
        foreach (LetterWait w in Enum.GetValues(typeof(LetterWait)))
            C(LetterWaitMath.Waits(w) == (w != LetterWait.None), "a letter holds one at a time to its reader only while the colony waits on it (" + w + ")");
        C(LetterWaitMath.AfterReading(true, false, false) == LetterWait.NoWord, "read and nothing sent back yet: no word yet, still waited for");
        C(LetterWaitMath.AfterReading(true, true, true) == LetterWait.Back && LetterWaitMath.AfterReading(false, true, true) == LetterWait.Back,
            "an answer in the post, with a messenger or held at their end: on its way back");
        C(LetterWaitMath.AfterReading(true, true, false) == LetterWait.None, "an answer carried and set down: nothing waits");
        // The player's save (League of Toba, 11 November 2026): Guido's invitation to Tocac, its answer "Tocac will come to
        // visit before long" set down at the mailbag, the letter kept open for the visit (stage Visiting, nothing carried
        // any more). It held "Write back" greyed with "One letter at a time"; now it waits on nothing.
        C(LetterWaitMath.AfterReading(false, false, false) == LetterWait.None && !LetterWaitMath.Waits(LetterWaitMath.AfterReading(false, false, false)),
            "the player's save: an invitation taken up, its answer in and the guest still to come, holds nothing");
        C(LetterWaitMath.AfterReading(false, true, true) == LetterWait.Back, "an invitation taken up whose answer is still in the post: waited for until it is in");
        C(LetterWaitMath.OneAtATimeKey(LetterWait.NoWord) == "CC_Letter_Why_OneAtATime_NoWord" && LetterWaitMath.OneAtATimeKey(LetterWait.Road) == "CC_Letter_Why_OneAtATime"
          && LetterWaitMath.OneAtATimeKey(LetterWait.Back) == "CC_Letter_Why_OneAtATime", "one at a time says when another can go: the answer in, or plain that none will come");
        int now = 10 * LetterWaitMath.DayTicks;
        C(LetterWaitMath.SoonestOpen(new[] { -1, now - 2 * LetterWaitMath.DayTicks, now - LetterWaitMath.DayTicks }, new[] { 6f, 5f, 15f }, now) == now + 3 * LetterWaitMath.DayTicks,
            "the soonest a letter back can go: the earliest wait still to end (never written for one, five days after two days ago)");
        C(LetterWaitMath.SoonestOpen(new[] { now - 20 * LetterWaitMath.DayTicks }, new[] { 6f }, now) == -1 && LetterWaitMath.SoonestOpen(new int[0], new float[0], now) == -1,
            "nothing waiting: no date");
    }

    // ================= The road =================

    private static void Road(Action<bool, string> C)
    {
        // While whoever carries it is still on the map, the day it is read moves on with the clock, its days on the road kept.
        int due = 5000, since = 1000;
        foreach (int now in new[] { 1000, 3500, 9000, 120000 })
        {
            int moved = LetterWaitMath.Shifted(due, since, now);
            C(moved - now == due - since, "the road counted from when the carrier leaves: still " + (due - since) + " ticks after " + now);
        }
        C(LetterWaitMath.Shifted(due, since, 500) == due, "never moved back");
        // A carrier staying on (a guest for days): read no sooner than the road after they leave, hour by hour.
        int d = 3 * LetterWaitMath.DayTicks, s = 0;
        for (int hour = 1; hour <= 48; hour++)
        {
            int n = hour * 2500;
            d = LetterWaitMath.Shifted(d, s, n);
            s = n;
        }
        C(d == 48 * 2500 + 3 * LetterWaitMath.DayTicks, "a guest who stays two days with it: read three days after they go, not while they are here");
        C(LetterWaitMath.MessengerArrives(100, 500) == 500 && LetterWaitMath.MessengerArrives(600, 500) == 600,
            "mail sent on by a messenger comes no sooner than it reached the region");
        foreach (float back in new[] { 0.5f, 1f, 2.5f, 5f })
            C(LetterWaitMath.NoWordDays(back) >= back + 2f, "silence is known only after an answer would have come and then some (" + back + " days back)");
        C(Math.Abs(LetterWaitMath.NoWordDays(1f) - 4f) < 0.001f, "a day back: no answer known four days after it is read, as before");
        C(FavourMath.DeedDays(0.1f) == 0.5f && FavourMath.DeedDays(3f) == 3f, "a captive's walk home: the road's days, half a day at least");
        C(FavourMath.LetterDeedFallbackDays >= 20f, "by letter, what is done far away waits for the answer, or a month if it never comes");
        C(FavourMath.DoneLater(FavourAsk.Word) && FavourMath.DoneLater(FavourAsk.Captive) && FavourMath.DoneLater(FavourAsk.Peace)
          && !FavourMath.DoneLater(FavourAsk.Goods) && !FavourMath.DoneLater(FavourAsk.Fighters),
            "a word, a captive and peace wait for the answer; goods come with it, fighters walk in no sooner than it");
    }

    // ================= Goods by who owes =================

    private static void Goods(Action<bool, string> C)
    {
        C(FavourMath.From(false, true, 20, 20, 20, 20) == GoodsFrom.Stores, "their people's debt: from their town's stores, whoever answers");
        // The player's card: "Acraeraes of Karond-Keth 2, a trader who called" owed a small debt and offered four yoghurts.
        C(FavourMath.From(true, true, 2, 3, 1, 4) == GoodsFrom.Trader, "the player's save: a trader who called gives what a trader deals in");
        var kinds = FavourMath.GoodsKindsFrom(GoodsFrom.Trader, WorldAge.Medieval, null);
        C(kinds.Length == 2 && kinds[0] == "trade" && kinds[1] == "silver" && !kinds.Contains("food"), "a trader: their trade goods and silver, no food load");
        C(FavourMath.GoodsKindsFrom(GoodsFrom.Trader, WorldAge.Tribal, null).SequenceEqual(new[] { "trade" }), "a tribe's trader: trade goods, no silver");
        C(FavourMath.From(true, false, 12, 3, 3, 3) == GoodsFrom.Healer && FavourMath.GoodsKindsFrom(GoodsFrom.Healer, WorldAge.Industrial, null).SequenceEqual(new[] { "medicine" }),
            "a healer: medicine");
        C(FavourMath.From(true, false, 3, 12, 3, 3) == GoodsFrom.Farmer && FavourMath.From(true, false, 3, 3, 12, 3) == GoodsFrom.Herder
          && FavourMath.GoodsKindsFrom(GoodsFrom.Farmer, WorldAge.Medieval, null).SequenceEqual(new[] { "food" })
          && FavourMath.GoodsKindsFrom(GoodsFrom.Herder, WorldAge.Tribal, null).SequenceEqual(new[] { "food" }), "a farmer or herder: food");
        C(FavourMath.From(true, false, 3, 3, 3, 12) == GoodsFrom.Smith && FavourMath.GoodsKindsFrom(GoodsFrom.Smith, WorldAge.Medieval, null).SequenceEqual(new[] { "steel" })
          && FavourMath.GoodsKindsFrom(GoodsFrom.Smith, WorldAge.Tribal, null).SequenceEqual(new[] { "wood" }), "a smith: steel; a tribe's crafter: wood");
        C(FavourMath.From(true, false, 7, 7, 7, 7) == GoodsFrom.Person && FavourMath.GoodsKindsFrom(GoodsFrom.Person, WorldAge.Tribal, null).SequenceEqual(new[] { "food" })
          && FavourMath.GoodsKindsFrom(GoodsFrom.Person, WorldAge.Spacer, null).SequenceEqual(new[] { "food", "silver" }), "no trade of note: what a household can spare");
        C(FavourMath.GoodsKindsFrom(GoodsFrom.Stores, WorldAge.Industrial, "healers")[0] == "medicine"
          && FavourMath.GoodsKindsFrom(GoodsFrom.Stores, WorldAge.Medieval, "herders")[0] == "food"
          && FavourMath.GoodsKindsFrom(GoodsFrom.Stores, WorldAge.Industrial, "traders")[0] == "trade"
          && FavourMath.GoodsKindsFrom(GoodsFrom.Stores, WorldAge.Industrial, "salvagers")[0] == "steel", "a people's stores: what they are known for first");
        C(!FavourMath.GoodsKindsFrom(GoodsFrom.Stores, WorldAge.Tribal, "drillers").Contains("steel"), "a tribe's stores never hold steel, whatever they are known for");
        foreach (WorldAge age in Enum.GetValues(typeof(WorldAge)))
            C(FavourMath.GoodsKindsFrom(GoodsFrom.Stores, age, null).SequenceEqual(FavourMath.GoodsKinds(age)), "a people's stores of no calling: their age's goods as before (" + age + ")");
        // Food as a staple load, never a handful of a dear dish.
        C(!FavourMath.Staple(true, 30f, 0.9f, 150f), "the player's yoghurt (a fine dish at 30): no staple");
        C(FavourMath.Staple(false, 15f, 0.9f, 300f), "plain meals: a staple");
        C(FavourMath.Staple(false, 2f, 0.05f, 300f) && FavourMath.Staple(false, 1.1f, 0.05f, 150f), "pemmican and grain: staples");
        C(!FavourMath.Staple(false, 75f, 0.9f, 300f), "four of a dear dish: a handful, no load");
        C(!FavourMath.Staple(false, 24f, 0.9f, 150f), "six survival meals for a small debt: too few, grain instead");
        C(!FavourMath.Staple(false, 10f, 0f, 300f), "nothing that doesn't feed");
        C(FavourMath.TradeGood(1.5f, 75) && FavourMath.TradeGood(10f, 500) && !FavourMath.TradeGood(32f, 25) && !FavourMath.TradeGood(1f, 1),
            "trade goods: a material in loads (cloth, gold), not a component or a single piece");
    }

    // ================= A word with a third people =================

    private static void Words(Action<bool, string> C)
    {
        C(!FavourMath.WordHelps(100) && FavourMath.WordHelps(99) && FavourMath.WordHelps(0), "a word with a people already at full goodwill is never offered");
        C(FavourMath.WordRank(10, 20f) < FavourMath.WordRank(50, 20f), "the coolest toward the colony first");
        C(FavourMath.WordRank(20, 5f) < FavourMath.WordRank(20, 50f), "the nearest first");
        C(FavourMath.WordsShown >= 3 && FavourMath.WordsShown <= 5, "a handful shown, the rest under Others");
        C(FavourMath.WordMuchKey(2) == "Little" && FavourMath.WordMuchKey(6) == "Some" && FavourMath.WordMuchKey(9) == "Much", "how much a word moves them, in words");
    }

    // ================= The menu =================

    private static void Menu(Action<bool, string> C)
    {
        var items = new List<(int group, bool can, bool more)>
        {
            (4, false, false), // fighters: no threat now
            (0, true, false), // goods
            (2, false, false), // a captive: none held
            (1, true, false), (1, true, false), (1, true, false), (1, true, false), (1, true, true), (1, true, true), // words: four shown, two more
            (3, true, false), // peace
            (0, true, false) // more goods
        };
        FavourMath.MenuOrder(items, out var shown, out var others, out var notNow);
        C(shown.SequenceEqual(new[] { 1, 10, 3, 4, 5, 6, 9 }), "what can be asked now first, by kind: goods, a word, peace");
        C(others.SequenceEqual(new[] { 7, 8 }), "the words it would help less under Others");
        C(notNow.SequenceEqual(new[] { 2, 0 }), "what can't be asked now under Not now, by kind");
        C(shown.Count + others.Count + notNow.Count == items.Count, "nothing lost from the list");
        FavourMath.MenuOrder(new List<(int, bool, bool)> { (0, true, false), (0, true, false) }, out var flat, out _, out _);
        C(flat.SequenceEqual(new[] { 0, 1 }), "another purpose's asks keep their own order");
    }

    // ================= The words =================

    private static void Texts(Action<bool, string> C, Dictionary<string, string> keys)
    {
        string K(string k) => keys.TryGetValue(k, out var v) ? v : null;
        // Every greyed letter says what it waits for and when it opens.
        var greyed = new[]
        {
            "CC_Letter_Why_OneAtATime", "CC_Letter_Why_OneAtATime_NoWord", "CC_Letter_Why_TooMany", "CC_Letter_Why_Here", "CC_Letter_Why_Lately", "CC_Letter_Why_NoMessenger",
            "CC_LetterRead_Why_NoWriter", "CC_LetterRead_Why_Lately", "CC_Route_Why_HeadBusy", "CC_Route_Why_HeadLately"
        };
        foreach (var k in greyed)
        {
            string v = K(k);
            C(v != null && (v.Contains("once") || v.Contains(" when ") || v.Contains("from the {DATE}") || v.Contains("on the {DATE}")), k + " says when it opens");
        }
        C(K("CC_Letter_Why_OneAtATime")?.Contains("{WAIT}") == true && K("CC_Letter_Why_OneAtATime_NoWord")?.Contains("{WAIT}") == true
          && K("CC_Route_Why_HeadBusy")?.Contains("{WAIT}") == true, "one at a time says where the last letter is");
        C(K("CC_Letter_Why_OneAtATime")?.Contains("answer reaches the colony") == true, "another can go once their answer reaches the colony");
        C(K("CC_Letter_Wait_NotLeft")?.Contains("{CARRIER}") == true && K("CC_Letter_Wait_NoWord") != null && K("CC_Letter_Msg_CarrierLost")?.Contains("{CARRIER}") == true,
            "a letter not on the road yet, no word yet, a carrier lost: in words");
        foreach (GoodsFrom g in Enum.GetValues(typeof(GoodsFrom)))
        {
            string v = K("CC_Dip_What_Goods_" + g);
            C(v != null && v.Contains("{GOODS}") && v.Contains("{SILVER}"), "goods from " + g + ": what comes and how much");
        }
        C(K("CC_Dip_What_Word")?.Contains("{HOWMUCH}") == true && K("CC_Dip_What_Word")!.Contains("{GOODWILL}") && K("CC_Dip_What_Much_Little") != null && K("CC_Dip_What_Much_Some") != null
          && K("CC_Dip_What_Much_Much") != null, "a word: whose goodwill rises and how much");
        C(K("CC_Dip_What_Captive") != null && K("CC_Dip_What_Peace") != null && K("CC_Dip_What_Fighters") != null && K("CC_Dip_Done_CaptiveRoad") != null,
            "a captive, peace and fighters: what each does");
        C(K("CC_Dip_Menu_Others")?.Contains("{COUNT}") == true && K("CC_Dip_Menu_NotNow")?.Contains("{COUNT}") == true, "Others and Not now say how many");
        C(K("CC_Dip_Opt_Goods") == "Goods: {GOODS}", "a load of goods, short in the list");
        var mine = greyed.Concat(keys.Keys.Where(k => k.StartsWith("CC_Dip_What_") || k.StartsWith("CC_Letter_Wait_") || k.StartsWith("CC_Dip_Menu_")))
            .Concat(new[] { "CC_Letter_Msg_CarrierLost", "CC_Dip_Done_CaptiveRoad", "CC_Dip_Opt_Goods" });
        foreach (var k in mine)
        {
            string v = K(k) ?? "";
            C(!v.Contains("—") && !v.Contains("!"), k + ": no em dash, no exclamation");
        }
    }

    // ================= The seams =================

    private static void Seams(string root, Func<string, string> Code, Action<bool, string> C)
    {
        string letters = Code("World/NeighbourLetters.cs");
        C(letters.Contains("string one = OneAtATimeWhy(n);") && letters.Contains("UnderWay >= MaxUnderWay") && letters.Contains("l.Open && To(l, n) && Awaited(l)"),
            "one at a time and the cap count only letters whose answer the colony still waits for");
        C(!letters.Contains("OpenCount") && !Code("Dealings/DealingActs.cs").Contains("OpenCount") && !Code("Outsiders/Outsiders.cs").Contains("OpenCount")
          && !Code("Diplomacy/FeastDialog.cs").Contains("OpenCount") && !Code("Dealings/Dealings.cs").Contains("OpenCount"), "no cap counts a letter whose answer is in");
        C(Code("World/LetterRoutes.cs").Contains("NeighbourLetters.AwaitedFrom(who)") && Code("Outsiders/Outsiders.cs").Contains("NeighbourLetters.AwaitedFrom(to)")
          && Code("Dealings/DealingActs.cs").Contains("NeighbourLetters.OneAtATimeWhy(n)"), "the head, a visitor carrying it and a dealing: the same rule");
        C(letters.Contains("if (CarrierHere(l) != null)") && letters.Contains("LetterWaitMath.Shifted(l.due, l.stageTick, now)") && letters.Contains("CarrierLost(l, n, now)"),
            "on the road only once the carrier has left the map; lost with them, back at the gate");
        C(!letters.Contains("there + 0.5f + NotableMath.ReplyDays"), "one of theirs taking it home: no guess of half a day");
        C(letters.Contains("Hold(l, title, text, def, chronicle, things, now);") && letters.Contains("if (now >= l.heldDue) Release(l, n);"),
            "an answer with nobody to carry it waits for its road back");
        C(letters.Contains("LetterWaitMath.NoWordDays(l.backDays)"), "silence known only once an answer would have come");
        C(letters.Contains("FavourCalls.AnswerIn(l);"), "a debt's word, captive or peace done when the answer is in");
        C(Code("Post/Outbox.cs").Contains("l.carriedBy = by;"), "whoever took it from the mailbag carries it off the map first");
        C(Code("Post/MailIn.cs").Contains("LetterWaitMath.MessengerArrives(Now, i.arrive)") && Code("Post/MailIn.cs").Contains("if (Now < i.arrive && i.map != null"),
            "mail sent on by a messenger, or with nobody to send, never before it reached the region");
        string reply = Code("Letters/LetterReply.cs");
        C(reply.Contains("NeighbourLetters.OneAtATimeWhy(n)") && reply.Contains("CC_LetterRead_Why_Lately") && reply.Contains("LetterWaitMath.SoonestOpen("),
            "Write back says what it waits for and when it opens");
        string calls = Code("Diplomacy/FavourCalls.cs");
        C(calls.Contains("FavourMath.GoodsKindsFrom(from, Ages.Of(f), Peoples.CallingOf(f))") && calls.Contains("def = StapleFood(f, worth, seed);")
          && calls.Contains("case \"trade\": def = TradeGoods(f, seed) ?? ThingDefOf.Silver;"), "goods by who owes and what they have");
        C(calls.Contains("caravanTraderKinds") && calls.Contains("g.HandlesThingDef(d)"), "a trader's goods from what their caravans carry, the player's mods as well as the game's");
        C(calls.Contains("var friends = WordPeoples(f, home);") && calls.Contains("FavourMath.WordHelps(o.PlayerGoodwill)") && calls.Contains("more = i >= FavourMath.WordsShown"),
            "a word only with the peoples it would help, a handful shown");
        C(calls.Contains("string gate = WhyNot(t);") && calls.Contains("o.why = gate;"), "a debt that can't be called in now offers nothing as if it could");
        C(calls.Contains("MailIn.FromAbroad(home, other, town, label, text, LetterDefOf.PositiveEvent)"), "the other people's letter about the word comes by the road");
        C(calls.Contains("if (letter && FavourMath.DoneLater(ask) || ask == FavourAsk.Captive)") && calls.Contains("if (answerDays > 0f) hours = Mathf.Max(hours, answerDays * 24f);"),
            "by letter, done far away with the answer; fighters never before it; a captive walks the road");
        C(Code("Diplomacy/Diplomacy.cs").Contains("FavourCalls.Hourly(now)") && Code("Diplomacy/DiplomacyLetters.cs").Contains("l.channel == LetterChannel.Courier ? Mathf.Max(0f, l.backDays) : -1f"),
            "the hour does what is due; a letter's answer days passed to the debt");
        C(Code("Diplomacy/DiplomacyLetters.cs").Contains("FavourCalls.Menu(asks, o => Asked = o.Code)") && Code("Diplomacy/DiplomacyLetters.cs").Contains("string what = chosen?.what;")
          && Code("Diplomacy/DiplomacyActs.cs").Contains("FavourCalls.Menu(FavourCalls.Options(t, home)"), "the short menu in the letter and face to face, with what the chosen ask does");
        string est = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(est.Contains("\"World/NeighbourLetters.cs:heldThings\""), "an answer's things held for the road back modelled in the save estimate");
    }
}
