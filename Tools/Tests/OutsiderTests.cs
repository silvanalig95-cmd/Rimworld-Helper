using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Talking with people from outside the colony (Outsiders/OutsiderMath): the waits, how a talk goes and what it changes,
/// the bond over visits, gifts that suit, a good word, favours, an invitation to stay, faith, practice bouts and their
/// rare real injury, hard words, duels of honour; and that every moment the code says has lines, every key it names
/// exists, and every job and memory it uses is defined.
/// </summary>
public static class OutsiderTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "outsiders: " + what);
        }

        var acts = Enum.GetValues(typeof(OutsiderAct)).Cast<OutsiderAct>().ToList();
        var tones = Enum.GetValues(typeof(TalkTone)).Cast<TalkTone>().ToList();

        // ---------------- Waits ----------------

        foreach (var a in acts) C(OutsiderMath.WaitHours(a) > 0f && OutsiderMath.TalkTicks(a) > 0, $"{a} has a wait and a length");
        int hour = OutsiderMath.TicksPerHour;
        C(OutsiderMath.Waited(-1, 0, OutsiderAct.Chat), "never done: no wait");
        C(!OutsiderMath.Waited(0, 6 * hour - 1, OutsiderAct.Chat) && OutsiderMath.Waited(0, 6 * hour, OutsiderAct.Chat), "a chat waits six hours");
        C(OutsiderMath.WaitHours(OutsiderAct.Duel) > OutsiderMath.WaitHours(OutsiderAct.Spar), "a duel waits longer than a bout");
        C(OutsiderMath.PerOutsider(OutsiderAct.Invite) && OutsiderMath.PerOutsider(OutsiderAct.AskHome) && !OutsiderMath.PerOutsider(OutsiderAct.Chat)
          && !OutsiderMath.PerOutsider(OutsiderAct.Court), "news, favours, invitations and duels wait for the outsider; talk and courting for the pair");
        C(acts.Where(OutsiderMath.Pointed).SequenceEqual(new[] { OutsiderAct.Spar, OutsiderAct.Provoke, OutsiderAct.Duel }), "the pointed ones are the bout, hard words and the duel");

        // ---------------- How a talk goes ----------------

        var plain = new TalkInput { act = OutsiderAct.Chat, social = 6 };
        C(OutsiderMath.Tone(plain, 0.5f) == TalkTone.Plain, "strangers with nothing between them: a plain word");
        C(OutsiderMath.Tone(plain, 1f) < OutsiderMath.Tone(plain, 0f), "luck moves it");
        var friendly = plain with { ofColonist = 60, ofColony = 50, affinity = 2f };
        C(OutsiderMath.Tone(friendly, 0f) == TalkTone.Warm, "two who like each other, whose people like the colony: warm even with bad luck");
        var hostile = plain with { ofColonist = -60, ofColony = -50, affinity = -2f };
        C(OutsiderMath.Tone(hostile, 1f) >= TalkTone.Cool, "two who dislike each other: never better than cool");
        bool monotone = true;
        for (float r = 0f; r < 1f; r += 0.05f)
            if (OutsiderMath.Tone(plain, r + 0.05f) > OutsiderMath.Tone(plain, r)) monotone = false;
        C(monotone, "better luck never makes it worse");
        C(OutsiderMath.Score(plain with { social = 14 }) > OutsiderMath.Score(plain) && OutsiderMath.Score(plain with { warmth = 2 }) > OutsiderMath.Score(plain)
          && OutsiderMath.Score(plain with { familiarity = 80f }) > OutsiderMath.Score(plain), "a way with words, a kind nature and knowing each other help");
        C(OutsiderMath.Score(plain with { affinity = 99f }) == OutsiderMath.Score(plain with { affinity = 3f }), "how they get on counts three steps at most");
        C(OutsiderMath.ToneStage(TalkTone.Plain) == -1 && tones.Where(t => t != TalkTone.Plain).Select(OutsiderMath.ToneStage).Distinct().Count() == 4,
            "a plain talk leaves no memory, the others one stage each");
        C(OutsiderMath.WarmthShift(TalkTone.Warm) > OutsiderMath.WarmthShift(TalkTone.Good) && OutsiderMath.WarmthShift(TalkTone.Good) > 0f
          && OutsiderMath.WarmthShift(TalkTone.Cool) < 0f && OutsiderMath.WarmthShift(TalkTone.Sour) < OutsiderMath.WarmthShift(TalkTone.Cool), "the bond moves with how it went");
        C(OutsiderMath.RegardShift(TalkTone.Warm) > 0f && OutsiderMath.RegardShift(TalkTone.Sour) < 0f, "a notable's regard moves with it");
        C(OutsiderMath.FamiliarityGain(OutsiderAct.Befriend, 0f) > OutsiderMath.FamiliarityGain(OutsiderAct.Chat, 0f)
          && OutsiderMath.FamiliarityGain(OutsiderAct.Chat, 2f) > OutsiderMath.FamiliarityGain(OutsiderAct.Chat, -2f)
          && OutsiderMath.FamiliarityGain(OutsiderAct.Chat, 99f) <= OutsiderMath.FamiliarityGain(OutsiderAct.Chat, 0f) * 1.5f + 0.001f,
            "getting to know them counts most, and comes easier to two who get on, within bounds");
        C(Math.Abs(OutsiderMath.Fade(20f, OutsiderMath.HalfLifeDays) - 10f) < 0.01f && OutsiderMath.Fade(20f, 0f) == 20f, "the bond halves between visits");
        C(OutsiderMath.NewVisit(-1, 0) && !OutsiderMath.NewVisit(0, OutsiderMath.TicksPerDay) && OutsiderMath.NewVisit(0, OutsiderMath.TicksPerDay * 3), "a new visit after two days apart");
        C(OutsiderMath.Shuns(-60, 0) && OutsiderMath.Shuns(0, -40) && !OutsiderMath.Shuns(0, -39) && !OutsiderMath.Shuns(-59, 0), "who won't stop at all");

        // ---------------- News of home ----------------

        C(tones.Select(OutsiderMath.NewsCount).SequenceEqual(new[] { 4, 3, 2, 1, 0 }), "the warmer the talk, the more news; a sour one tells nothing");

        // ---------------- A gift ----------------

        C(OutsiderMath.GiftFit(false, false, false) == 1f && OutsiderMath.GiftFit(true, false, false) == 1.5f && OutsiderMath.GiftFit(true, true, false) == 2f
          && OutsiderMath.GiftFit(true, true, true) == 0f && OutsiderMath.GiftFit(false, true, false) > 1f, "a gift that suits counts more, a fine one a little, one against their beliefs nothing");
        C(OutsiderMath.GiftCount(10f, 100) == 12 && OutsiderMath.GiftCount(500f, 3) == 1 && OutsiderMath.GiftCount(1f, 50) == 50 && OutsiderMath.GiftCount(5f, 1) == 1
          && OutsiderMath.GiftCount(5f, 0) == 0, "a share of a stack worth about 120 silver, never more than there is");
        C(Math.Abs(OutsiderMath.GiftWarmth(120f, 1f, 1f) - 10f) < 0.01f && OutsiderMath.GiftWarmth(9999f, 2f, 1f) == 20f && OutsiderMath.GiftWarmth(1f, 1f, 1f) == 1f
          && OutsiderMath.GiftWarmth(500f, 0f, 1f) == 0f, "warmth by worth and fit, 1 to 20, nothing for an unwelcome one");
        C(OutsiderMath.GiftWarmth(120f, 1f, 2f) < OutsiderMath.GiftWarmth(120f, 1f, 1f), "the same gift means less from a rich colony");
        C(OutsiderMath.GiftStage(2f) == 0 && OutsiderMath.GiftStage(1f) == 1, "the memory says whether it suited them");
        C(OutsiderMath.GiftGoodwill(400f, 0) > OutsiderMath.GiftGoodwill(400f, 80) && OutsiderMath.GiftGoodwill(0f, 0) == 0, "goodwill for a gift, less from a people who already like the colony");

        // ---------------- A good word ----------------

        C(OutsiderMath.WillPutInWord(20, 10) && !OutsiderMath.WillPutInWord(19, 50) && !OutsiderMath.WillPutInWord(60, 9), "a good word needs regard for the colony and the one asking");
        C(OutsiderMath.GoodWordShift(0, 0, 0) == 2f && OutsiderMath.GoodWordShift(100, 100, 20) == 10f && OutsiderMath.GoodWordShift(60, 40, 10) > OutsiderMath.GoodWordShift(20, 10, 10),
            "a good word counts 2 to 10, more the better they regard the colony");

        // ---------------- Favours ----------------

        C(OutsiderMath.TeachPick(new[] { 10, 5, 12 }, new[] { 2, 4, 11 }, null) == 0, "they teach where they lead most");
        C(OutsiderMath.TeachPick(new[] { 10, 5, 12 }, new[] { 2, 4, 11 }, new[] { true, false, false }) == -1, "a skill the learner can't learn is never taught; a small lead isn't worth it");
        C(OutsiderMath.TeachPick(new[] { 5 }, new[] { 0 }, null) == -1 && OutsiderMath.TeachPick(new[] { 6 }, new[] { 3 }, null) == 0, "they need to know it to level 6, three better");
        C(OutsiderMath.TeachXpPerTick(20, 0) == 0.25f && OutsiderMath.TeachXpPerTick(6, 6) == 0.08f && OutsiderMath.TeachXpPerTick(12, 4) > OutsiderMath.TeachXpPerTick(8, 4),
            "a lesson teaches 0.08 to 0.25 a tick, more from a better teacher");
        C(OutsiderMath.TeachXpPerTick(20, 0) * OutsiderMath.TeachTicks <= 650f, "an hour's lesson is worth less than a level for anyone past the first");
        C(OutsiderMath.WillDoFavour(5, 0) && !OutsiderMath.WillDoFavour(4, 50) && !OutsiderMath.WillDoFavour(50, -1), "a favour needs a little regard");
        C(OutsiderMath.WillBring(15, 0) && !OutsiderMath.WillBring(14, 0), "something brought needs a little more");
        C(OutsiderMath.BringCount(2f, 0) == 30 && OutsiderMath.BringCount(2f, 100) == 80 && OutsiderMath.BringCount(1000f, 0) == 1, "they bring 60 to 160 silver's worth, at least one");

        // ---------------- An invitation ----------------

        C(OutsiderMath.InviteChance(0, 0, 100, 100, 20) == 0f, "nobody stays without a reason");
        C(OutsiderMath.InviteChance(2, 0, 20, 20, 8) > OutsiderMath.InviteChance(1, 0, 20, 20, 8) && OutsiderMath.InviteChance(1, 2, 20, 20, 8) < OutsiderMath.InviteChance(1, 0, 20, 20, 8),
            "reasons help, ties at home hold them back");
        C(OutsiderMath.InviteChance(9, 0, 100, 100, 20) == 0.85f && OutsiderMath.InviteChance(1, 9, -100, -100, 0) == 0.02f, "between 2% and 85% with a reason");
        C(OutsiderMath.InviteGoodwill(2) > OutsiderMath.InviteGoodwill(1) && OutsiderMath.InviteGoodwill(1) < 0, "their people mind losing one of theirs, less with good reason");

        // ---------------- A bond over visits ----------------

        C(OutsiderMath.FriendsNow(35f, 30f, 2) && !OutsiderMath.FriendsNow(35f, 30f, 1) && !OutsiderMath.FriendsNow(34f, 99f, 9) && !OutsiderMath.FriendsNow(99f, 29f, 9),
            "friends take warmth, knowing and more than one visit");
        C(OutsiderMath.CourtChance(0.5f, 100f) == 0.5f && OutsiderMath.CourtChance(0.5f, 0f) < OutsiderMath.CourtChance(0.5f, 50f) && OutsiderMath.CourtChance(2f, 100f) == 1f,
            "courting goes by the game's own chance, better the better they know each other");

        // ---------------- Faith ----------------

        C(OutsiderMath.Faith(true, 1f, 0, 20, -3f, 0f) == FaithEnd.Closer, "the same faith always brings them closer");
        C(OutsiderMath.Faith(false, 1f, 0, 20, -3f, 0f) == FaithEnd.Hardened && OutsiderMath.Faith(false, 0.1f, 20, 0, 3f, 0.9f) == FaithEnd.Softened
          && OutsiderMath.Faith(false, 0.6f, 6, 6, 0f, 0.5f) == FaithEnd.Agreed, "different faiths: harden, soften or agree to differ, by words, certainty and luck");
        foreach (FaithEnd e in Enum.GetValues(typeof(FaithEnd)))
            C(Math.Abs(OutsiderMath.CertaintyShift(e, true)) <= 0.05f && Math.Abs(OutsiderMath.CertaintyShift(e, false)) <= 0.05f, $"faith {e}: certainty moves a little, never a conversion");
        C(OutsiderMath.CertaintyShift(FaithEnd.Softened, true) < 0f && OutsiderMath.FaithWarmth(FaithEnd.Closer) > 0f && OutsiderMath.FaithWarmth(FaithEnd.Hardened) < 0f,
            "softened is less sure, closer is warmer, a hard argument cools them");

        // ---------------- Temper and bouts ----------------

        C(OutsiderMath.Temper(true, true, true, true, false, false) == 4 && OutsiderMath.Temper(false, false, false, false, true, true) == -2, "temper from -2 to 4");
        C(OutsiderMath.SparAcceptChance(4, 20, 0) == 0.95f && OutsiderMath.SparAcceptChance(-2, 0, 20) == 0.05f && OutsiderMath.SparAcceptChance(2, 8, 8) > OutsiderMath.SparAcceptChance(0, 8, 8),
            "a fighting temper takes up a bout more readily, within 5% and 95%");
        C(OutsiderMath.HitChance(1f, 0f) == 0.95f && OutsiderMath.HitChance(0f, 1f) == 0.15f && OutsiderMath.HitChance(0.8f, 0.2f) > OutsiderMath.HitChance(0.6f, 0.2f), "a swing lands 15% to 95%");
        C(OutsiderMath.SparHardBlowChance(0, 0) == 0.04f && OutsiderMath.SparHardBlowChance(4, 4) == 0.08f && OutsiderMath.SparHardBlowChance(-2, -2) == 0.04f,
            "a real injury in 4% of bouts, up to 8% between fighting tempers");
        C(OutsiderMath.SparDamage(1f, false) <= 2.5f && OutsiderMath.SparDamage(0f, false) >= 1f && OutsiderMath.SparDamage(0f, true) >= 6f, "practice blows are light, the hard one isn't");
        // Over many bouts, the share with a real injury stays near the game's own rate for a fight that leaves more than bruises.
        var rng = new Random(7);
        int hurtBouts = 0, bouts = 20000;
        for (int i = 0; i < bouts; i++)
            if (rng.NextDouble() < OutsiderMath.SparHardBlowChance(rng.Next(-1, 3), rng.Next(-1, 3))) hurtBouts++;
        C(hurtBouts / (float)bouts is > 0.035f and < 0.07f, $"about one bout in twenty leaves a real injury ({hurtBouts / (float)bouts:P1})");

        // ---------------- Hard words ----------------

        C(OutsiderMath.FightChance(4, 4, -100, 1f) == 0.7f && OutsiderMath.FightChance(-2, -2, 100, 0f) == 0f, "a fistfight 0% to 70%");
        C(OutsiderMath.FightChance(3, 0, 0, 0.02f) > OutsiderMath.FightChance(0, 0, 0, 0.02f) && OutsiderMath.FightChance(0, 0, -50, 0.02f) > OutsiderMath.FightChance(0, 0, 50, 0.02f),
            "a hot temper and a low opinion make a fight likelier");
        C(OutsiderMath.ProvokeWarmth < 0f && OutsiderMath.ProvokeRegard < 0f && OutsiderMath.BrawlGoodwill < 0, "hard words cost the bond, regard and, with a fight, goodwill");

        // ---------------- A duel of honour ----------------

        C(OutsiderMath.DuelFits(OutsiderMath.Neolithic, false, false) && OutsiderMath.DuelFits(OutsiderMath.Medieval, false, false) && !OutsiderMath.DuelFits(4, false, false)
          && OutsiderMath.DuelFits(5, true, true) && !OutsiderMath.DuelFits(5, true, false), "duels for tribes, medieval folk and an empire's nobility only");
        C(OutsiderMath.DuelAcceptChance(0, 8, 8, 0, DuelTerms.FirstBlood) > OutsiderMath.DuelAcceptChance(0, 8, 8, 0, DuelTerms.Yield)
          && OutsiderMath.DuelAcceptChance(4, 20, 0, -100, DuelTerms.FirstBlood) == 0.95f && OutsiderMath.DuelAcceptChance(-2, 0, 20, 100, DuelTerms.Yield) == 0.05f,
            "first blood is taken up more readily than to the yield, within 5% and 95%");
        C(OutsiderMath.YieldPain(true, false, false) > OutsiderMath.YieldPain(false, false, false) && OutsiderMath.YieldPain(false, true, false) < OutsiderMath.YieldPain(false, false, false)
          && OutsiderMath.YieldPain(true, false, true) <= 0.6f && OutsiderMath.YieldPain(false, true, false) >= 0.15f, "the tough hold on longer, a wimp gives in sooner, before shock");
        C(OutsiderMath.HonourGoodwill(true, 0) == 2 && OutsiderMath.HonourGoodwill(true, 9) == 5 && OutsiderMath.HonourGoodwill(false, 9) == -1, "honour: 2 to 5 goodwill for a win, a little lost for a loss");
        C(OutsiderMath.HonourRegard(true) > OutsiderMath.HonourRegard(false) && OutsiderMath.HonourRegard(false) > 0f, "a loser still earns a little regard for standing up");

        // ---------------- Lines, keys, jobs and memories ----------------

        string src = Path.Combine(root, "Source", "ColonyCouncil", "Outsiders");
        string code = string.Join("\n", Directory.GetFiles(src, "*.cs").Select(File.ReadAllText));
        var pool = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Outsiders.xml")).Descendants("li").Select(li => li.Value.Trim()).ToList();
        var tagged = new HashSet<string>();
        foreach (var line in pool)
        {
            if (!line.StartsWith("[")) continue;
            int end = line.IndexOf(']');
            foreach (var t in line.Substring(1, end - 1).Split(',')) tagged.Add(t.Trim());
        }
        var moments = new HashSet<string>();
        foreach (Match m in Regex.Matches(code, @"Say\([^,;]+,\s*""([a-z]+)""")) moments.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(code, @"return ""([a-z]+)"";")) moments.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(code, @"\? ""([a-z]+)"" : ""([a-z]+)"""))
        {
            moments.Add(m.Groups[1].Value);
            moments.Add(m.Groups[2].Value);
        }
        foreach (var t in tones) moments.Add("end" + t.ToString().ToLowerInvariant());
        foreach (FaithEnd e in Enum.GetValues(typeof(FaithEnd))) moments.Add("faith" + e.ToString().ToLowerInvariant());
        moments.RemoveWhere(m => m == "trader" || m == "visitor" || m == "guest");
        C(moments.Count >= 50, $"found the moments the code says ({moments.Count})");
        foreach (var m in moments) C(tagged.Contains(m), $"the pool has lines for '{m}'");

        var keyed = new HashSet<string>(XDocument.Load(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Outsiders.xml")).Root!.Elements().Select(e => e.Name.LocalName));
        var keys = new HashSet<string>();
        foreach (Match m in Regex.Matches(code, @"""(CC_Out_[A-Za-z_]+)""")) if (!m.Groups[1].Value.EndsWith("_")) keys.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(code, @"Why\(""([A-Za-z]+)""")) keys.Add("CC_Out_Why_" + m.Groups[1].Value);
        foreach (Match m in Regex.Matches(code, @"Note\(OutsiderAct\.([A-Za-z]+)")) keys.Add("CC_Out_Note_" + m.Groups[1].Value);
        foreach (Match m in Regex.Matches(code, @"Note\(act,")) foreach (var a in new[] { "Meal", "Drink" }) keys.Add("CC_Out_Note_" + a);
        foreach (var a in acts.Where(a => a != OutsiderAct.GoodWord && a != OutsiderAct.Teach)) keys.Add("CC_Out_Act_" + a);
        foreach (var t in tones)
        {
            keys.Add("CC_Out_Tone_" + t);
            keys.Add("CC_Out_ToneWord_" + t);
        }
        foreach (FaithEnd e in Enum.GetValues(typeof(FaithEnd)))
        {
            keys.Add("CC_Out_Msg_Faith_" + e);
            keys.Add("CC_Out_FaithWord_" + e);
        }
        foreach (DuelTerms t in Enum.GetValues(typeof(DuelTerms))) keys.Add("CC_Out_TermsWord_" + t);
        foreach (var w in new[] { "Won", "Lost", "Draw" }) keys.Add("CC_Out_DuelWord_" + w);
        foreach (var w in new[] { "Won", "Lost" }) keys.Add("CC_Out_Msg_Duel" + w);
        foreach (var w in new[] { "Food", "Medicine", "Hands", "Fuel" }) keys.Add("CC_Out_News_Short_" + w);
        C(keys.Count >= 120, $"found the keys the code names ({keys.Count})");
        foreach (var k in keys) C(keyed.Contains(k), $"{k} exists");
        foreach (var kind in new[] { "gaveGift", "duel", "brawled", "broughtAsked" })
            C(keyed.Contains("CC_Memory_Deed_" + kind) && Regex.IsMatch(File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "World", "FactionMemory.cs")), "\\[\"" + kind + "\"\\]"),
                $"the peoples' memory knows the deed {kind}, with its words");

        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).SelectMany(r => r.Elements())
            .Select(e => (kind: e.Name.LocalName, name: e.Element("defName")?.Value, driver: e.Element("driverClass")?.Value)).ToList();
        var defOf = Regex.Match(code, @"class OutsiderDefOf\s*\{(.*?)static OutsiderDefOf", RegexOptions.Singleline).Groups[1].Value;
        foreach (Match m in Regex.Matches(defOf, @"public static (JobDef|ThoughtDef) (\w+);"))
            C(defs.Any(d => d.kind == m.Groups[1].Value && d.name == m.Groups[2].Value), $"{m.Groups[1].Value} {m.Groups[2].Value} is defined");
        foreach (var d in defs.Where(d => d.kind == "JobDef" && (d.name ?? "").StartsWith("Council_Outsider")))
            C(d.driver != null && code.Contains("class " + d.driver.Replace("ColonyCouncil.", "") + " "), $"{d.name}'s driver {d.driver} exists");
        return n;
    }
}
