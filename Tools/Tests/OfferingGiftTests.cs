using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Gifts worth what they cost (the player, 16 November 2026: "i just wonder whether these offering ressources are "wasted",
/// or if there is always some benefit from them"; "sometimes a return in kind sounds great. the book should track the
/// offerings and in this case also the god's favour should be tracked in the book, but the wording should also be kept a bit
/// ominous, so not with a numeric value or so"; "it should please never take very valuable items like components etc. as
/// offering, or only if there is a proper payoff for it. but then it shouldn't take it automatically, but perhaps an event
/// where a pawn asks for approval first"): the favour curve that keeps growing, what is precious and never taken unasked,
/// when an ask comes and how much it asks, how a gift leans what follows, the return in kind and its worth, the keeper's
/// reading of each god's temper (late, sometimes wrong, words only), and the seams, defs and words.
/// </summary>
public static class OfferingGiftTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "gifts worth what they cost: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ================= None wasted: the favour keeps growing with the worth =================

        float F(float v) => WorshipMath.OfferingFavour(v, 1f, 1f, false, false);
        C(F(20f) > 2f && F(20f) < 3f && F(100f) > 4f && F(100f) < 5f, $"an everyday gift about as before ({F(20f):0.0}, {F(100f):0.0})");
        C(F(1000f) - F(500f) > 2.5f && F(500f) - F(250f) > 1.5f, $"no longer flat past a few hundred silver's worth ({F(250f):0.0}, {F(500f):0.0}, {F(1000f):0.0})");
        C(F(100000f) == WorshipMath.OfferingWorthMost && F(1000f) < WorshipMath.OfferingWorthMost, "a fortune stops at the most, a precious gift below it");
        var worths = new[] { 0f, 5f, 20f, 50f, 100f, 250f, 500f, 1000f, 1500f };
        C(worths.Zip(worths.Skip(1), (a, b) => F(b) >= F(a)).All(x => x), "never less for more given");
        C(WorshipMath.OfferingFavour(100f, 0f, 1f, false, false) == 0f && float.IsNaN(F(float.NaN)) == false, "nothing where it isn't taken, nothing broken by a missing worth");

        // ================= Precious, never taken unasked =================

        var plainFood = new PreciousFacts { renewable = true, unitValue = 1.2f };
        C(!OfferingGiftMath.Precious(plainFood), "food, wood, hides, blocks the colony can make or gather again: plain");
        C(OfferingGiftMath.Precious(new PreciousFacts { named = true, renewable = true }), "the player's own list (components, plasteel, uranium, gold, jade, neutroamine): precious");
        C(OfferingGiftMath.Precious(new PreciousFacts { part = true, renewable = true }), "a mod's part for machines: precious");
        C(OfferingGiftMath.Precious(new PreciousFacts { medicine = true, potency = 1f, renewable = true })
          && !OfferingGiftMath.Precious(new PreciousFacts { medicine = true, potency = OfferingGiftMath.HerbalPotency, renewable = true }),
            "medicine past herbal precious, herbs not");
        C(OfferingGiftMath.Precious(new PreciousFacts { exotic = true, renewable = true }), "glitterworld goods: precious");
        C(OfferingGiftMath.Precious(new PreciousFacts { stuff = true, unitValue = 9f, renewable = true })
          && !OfferingGiftMath.Precious(new PreciousFacts { stuff = true, unitValue = 1.9f, renewable = true }),
            "a costly material precious, steel and cloth not");
        C(OfferingGiftMath.Precious(new PreciousFacts { unitValue = 2f, renewable = false }), "anything the colony can't make now or gather again: precious");
        C(OfferingGiftMath.Plenty(30, 10, 0) && !OfferingGiftMath.Plenty(29, 10, 0) && !OfferingGiftMath.Plenty(100, 10, 120) && !OfferingGiftMath.Plenty(5, 0, 0),
            "carried unasked only from plenty: three times the gift and the colony's own store");

        // ================= Asked first =================

        C(OfferingGiftMath.AskNow(12f, 20f, 30f, 0.1f) && !OfferingGiftMath.AskNow(11f, 99f, 99f, 0f) && !OfferingGiftMath.AskNow(99f, 19f, 99f, 0f)
          && !OfferingGiftMath.AskNow(99f, 99f, 29f, 0f) && !OfferingGiftMath.AskNow(99f, 99f, 99f, OfferingGiftMath.AskChance),
            "an ask now and then: weeks apart at a home and for a god, the same thing a month after a no, and the day's chance");
        // About how often, once the waits allow: a fortnight and more apart.
        var rng = new Random(11);
        int askCount = 0;
        float last = -999f;
        for (int day = 0; day < 6000; day++)
            if (OfferingGiftMath.AskNow(day - last, 999f, 999f, (float)rng.NextDouble()))
            {
                askCount++;
                last = day;
            }
        float apart = 6000f / Math.Max(1, askCount);
        C(apart >= 14f && apart <= 24f, $"about one ask every couple of weeks at most ({apart:0} days apart)");
        C(OfferingGiftMath.AskCount(32f, 30, 8) >= 1 && OfferingGiftMath.AskCount(32f, 30, 8) <= 10 && OfferingGiftMath.AskCount(32f, 2, 8) == 0
          && OfferingGiftMath.AskCount(5000f, 30, 8) == 0 && OfferingGiftMath.AskCount(200f, 3, 8) == 1,
            "a precious gift's size: a few of it, never more than a third of what is held, never a fortune");
        C(OfferingGiftMath.Askable(30, 5, 12) && !OfferingGiftMath.Askable(14, 5, 12) && !OfferingGiftMath.Askable(10, 4, 0),
            "asked only where the colony keeps twice the gift and its own store after it");
        C(OfferingGiftMath.AskWorth(1) >= OfferingGiftMath.AskWorthMin && OfferingGiftMath.AskWorth(1000) <= OfferingGiftMath.AskWorthMax
          && OfferingGiftMath.AskWorth(8) > WorshipMath.OfferingValueTarget(8), "worth more than an everyday gift, within its bounds");
        C(OfferingGiftMath.PreciousFavour > 1f, "a precious gift moves more than an everyday one of its worth");

        // ================= What follows: leaned by worth and fit =================

        C(Math.Abs(OfferingGiftMath.Lean(40f, 1f, false) - 1f) < 0.01f, "an everyday gift of forty silver's worth that the god likes leans nothing");
        C(OfferingGiftMath.Lean(10f, 0.4f, false) < 1f && OfferingGiftMath.Lean(200f, 1.5f, false) > 1.5f && OfferingGiftMath.Lean(1f, 0.4f, true) >= 2f
          && OfferingGiftMath.Lean(1e6f, 2f, true) <= 3f && OfferingGiftMath.Lean(0f, 0f, false) >= 0.5f, "a token less, a rich or well-loved one more, a precious one most, within a half and three");
        C(GodTouchMath.GapDays("taken", 1f, false) == GodTouchMath.SameKeyGapDays("taken") && GodTouchMath.GapDays("taken", 3f, false) < 4f
          && GodTouchMath.GapDays("taken", 0.5f, false) > 4f && GodTouchMath.GapDays("taken", 1f, true) == 0f && GodTouchMath.GapDays("CC_GodO_Saw_Roots", 3f, true) == 10f,
            "the four days between takings shorter for a rich gift, longer for a token one, none for a precious one; anything else as before");
        // TierFor with no lean is Tier exactly.
        var rng2 = new Random(5);
        bool same = true;
        var tones = (TouchTone[])Enum.GetValues(typeof(TouchTone));
        for (int i = 0; i < 4000 && same; i++)
        {
            var t = tones[rng2.Next(tones.Length)];
            bool big = rng2.Next(2) == 0, plain = rng2.Next(2) == 0;
            float a = (float)rng2.NextDouble(), b = (float)rng2.NextDouble();
            float d1 = rng2.Next(0, 20), d2 = rng2.Next(0, 20), d3 = rng2.Next(0, 10), d4 = rng2.Next(0, 300), d5 = rng2.Next(0, 100);
            same = GodTouchMath.Tier(t, big, plain, a, b, d1, d2, d3, d4, d5) == GodTouchMath.TierFor(t, big, plain, a, b, d1, d2, d3, d4, d5, 1f);
        }
        C(same, "an occurrence that is no gift follows as before");
        int Count(float lean, TouchTier tier)
        {
            var r = new Random(3);
            int k = 0;
            for (int i = 0; i < 20000; i++)
                if (GodTouchMath.TierFor(TouchTone.Sight, false, false, (float)r.NextDouble(), (float)r.NextDouble(), 99f, 99f, 99f, 999f, 999f, lean) >= tier)
                    k++;
            return k;
        }
        C(Count(2.5f, TouchTier.Small) > Count(1f, TouchTier.Small) && Count(1f, TouchTier.Small) > Count(0.5f, TouchTier.Small), "a richer gift is the likelier followed");
        C(Count(2.5f, TouchTier.Notable) > 2 * Count(1f, TouchTier.Notable) && Count(2.5f, TouchTier.Great) > 4 * Count(1f, TouchTier.Great),
            "and the likelier by something notable or great");
        C(GodTouchMath.TierFor(TouchTone.Sight, false, false, 0f, 0f, 99f, 4f, 99f, 999f, 999f, 2.5f) >= TouchTier.Notable
          && GodTouchMath.TierFor(TouchTone.Sight, false, false, 0f, 0.2f, 99f, 4f, 99f, 999f, 999f, 1f) == TouchTier.Small,
            "a rich gift's notable answer waits less on the god's own last one");
        C(GodTouchMath.TierFor(TouchTone.Sight, false, false, 0f, 0.5f, 99f, 99f, 1f, 999f, 999f, 3f) == TouchTier.Small,
            "the colony's own waits still hold for any gift");
        C(GodTouchMath.TierFor(TouchTone.Sight, true, true, 0f, 0f, 99f, 99f, 99f, 999f, 10f, 3f) != TouchTier.Great, "a great answer still waits on the colony's last great one");

        // ================= A return in kind =================

        C(OfferingGiftMath.ReturnChance(1f, false) > 0.05f && OfferingGiftMath.ReturnChance(1f, false) < 0.15f && OfferingGiftMath.ReturnChance(3f, false) <= 0.3f
          && OfferingGiftMath.ReturnChance(2f, true) >= 0.5f && OfferingGiftMath.ReturnChance(3f, true) <= 0.65f,
            "now and then for an everyday gift, about half the time for a precious one");
        C(!OfferingGiftMath.ReturnAllowed(false, 9f, 99f) && !OfferingGiftMath.ReturnAllowed(false, 99f, 2f) && OfferingGiftMath.ReturnAllowed(false, 10f, 3f)
          && OfferingGiftMath.ReturnAllowed(true, 0f, 0f), "a god once in ten days at a home, any god once in three; a precious gift whenever");
        C(OfferingGiftMath.ReturnWorth(100f, false, 0f) == 75f && OfferingGiftMath.ReturnWorth(100f, false, 0.999f) < 121f
          && OfferingGiftMath.ReturnWorth(100f, true, 0f) == 100f && OfferingGiftMath.ReturnWorth(100f, true, 0.999f) < 151f
          && OfferingGiftMath.ReturnWorth(1f, false, 0.5f) == OfferingGiftMath.ReturnWorthMin, "worth about what was given, more for a precious one, never a crumb");
        C(OfferingGiftMath.ReturnCount(100f, 1f, 75) == 75 && OfferingGiftMath.ReturnCount(100f, 10f, 75) == 10 && OfferingGiftMath.ReturnCount(5f, 32f, 25) == 1
          && OfferingGiftMath.ReturnCount(100f, 0f, 75) == 0, "as many as make that worth, a stack at most");
        var units = new List<float> { 1.9f, 1f, 10f, 200f };
        var weights = new List<float> { 3f, 2f, 1f, 1f };
        var picked = new HashSet<int>();
        for (int i = 0; i < 100; i++) picked.Add(OfferingGiftMath.PickReturn(units, weights, 50f, i / 100f));
        C(picked.SetEquals(new[] { 0, 1, 2 }), "the god's goods by weight, none a single piece of which is worth more than the return");
        C(OfferingGiftMath.PickReturn(units, weights, 0.5f, 0.3f) == -1 && OfferingGiftMath.PickReturn(new List<float>(), new List<float>(), 50f, 0.3f) == -1,
            "nothing when nothing fits");

        // ================= The keeper's reading: late, sometimes wrong, words only =================

        C(Enumerable.Range(0, GodTemperMath.Steps).Select(GodTemperMath.Band).SequenceEqual(new[] { 0, 1, 2, 3, 3, 4, 4 }),
            "seven steps in five bands of words: angry, cold, silent, warm, glad");
        var first = GodTemperMath.Read(-1, 6, true, 0.9f, 0.5f);
        C(first.step == GodTemperMath.Unknown + 1 && !first.unsure, "the first reading starts from nothing known, a step toward the truth");
        bool lag = true;
        var rng3 = new Random(17);
        for (int i = 0; i < 2000; i++)
        {
            int shown = rng3.Next(-1, 7), truth = rng3.Next(0, 7);
            var r = GodTemperMath.Read(shown, truth, rng3.Next(2) == 0, (float)rng3.NextDouble(), (float)rng3.NextDouble());
            int from = shown < 0 ? GodTemperMath.Unknown : shown;
            if (Math.Abs(r.step - from) > 1 || r.step < 0 || r.step >= GodTemperMath.Steps) lag = false;
        }
        C(lag, "never more than a step a reading, always within the words");
        int step = -1, wrongOff = 0;
        var rng4 = new Random(23);
        for (int i = 0; i < 200; i++)
        {
            step = GodTemperMath.Read(step, 5, true, (float)rng4.NextDouble(), (float)rng4.NextDouble()).step;
            if (i > 10 && Math.Abs(step - 5) > 1) wrongOff++;
        }
        C(wrongOff == 0 && Math.Abs(step - 5) <= 1, "with something to read it by it comes to the truth, at most a step off");
        C(GodTemperMath.Read(4, 4, true, 0.01f, 0.2f).step == 3 && GodTemperMath.Read(4, 4, true, 0.99f, 0.2f).step == 4
          && GodTemperMath.Read(6, 6, true, 0.01f, 0.9f).step == 5, "a misreading is a step beside the truth");
        C(GodTemperMath.Read(3, 3, false, 0.9f, 0.5f).unsure && GodTemperMath.WrongUnseen > GodTemperMath.WrongChance, "with nothing seen lately: a guess, more often wrong, said as some say");
        C(GodTemperMath.ReadNow(4f, 0f) && !GodTemperMath.ReadNow(3.9f, 0f) && !GodTemperMath.ReadNow(7f, 0.9f) && GodTemperMath.ReadNow(8f, 1f), "read again some days apart, never daily");
        C(GodTemperMath.SaysSince(3, 2, 3) && GodTemperMath.SaysSince(4, 4, 4) && !GodTemperMath.SaysSince(1, 2, 1) && !GodTemperMath.SaysSince(3, 4, 3),
            "\"since the steel was given\" only for a warm reading that didn't fall");

        // ================= The seams =================

        string offerings = Read("Gods/Worship/Offerings.cs"), gifts = Read("Gods/Worship/OfferingGifts.cs"), touch = Read("Gods/Touch/GodTouch.cs"),
            ret = Read("Gods/Touch/GodTouchReturn.cs"), page = Read("Gods/GodLorePage.cs"), lore = Read("Gods/GodLoreGifts.cs"), temper = Read("Gods/GodTemper.cs"),
            asks = Read("Gods/World/GodAsks.cs"), worship = Read("Gods/Worship/Worship.cs"), book = Read("Gods/Worship/WorshipBook.cs");
        C(offerings.Contains("if (OfferingGifts.Precious(given.def, map)) continue;"), "never anything precious carried unasked");
        C(gifts.Contains("\"ComponentIndustrial\", \"ComponentSpacer\", \"Plasteel\", \"Uranium\", \"Gold\", \"Jade\", \"Neutroamine\""), "the player's own list, by name");
        C(gifts.Contains("r.AvailableNow") && gifts.Contains("harvestedThingDef") && gifts.Contains("mineableThing") && gifts.Contains("CompProperties_Milkable"),
            "what the colony can make now or gather again, by its own recipes, fields, rock and animals");
        C(asks.Contains("[Precious] = OfferingGifts.Answers") && gifts.Contains("GodAsks.Ask(GodAsks.Precious,") && gifts.Contains("Tell.CanAsk"),
            "the ask is one of the gods' questions, a card with Yes and No, when a question may come");
        C(gifts.Contains("Offerings.Carry(b, r, map);") && offerings.Contains("SendPrecious(o, map, g);") && offerings.Contains("* OfferingGiftMath.PreciousFavour"),
            "on Yes they carry it themselves, for a stronger favour");
        C(gifts.Contains("OfferingGiftsDefOf.Rimfolk_Worship_GiftKept") && gifts.Contains("if (Worship.Devout(p))") && gifts.Contains("NoMark + d.defName"),
            "on No a little disappointed if devout, and the same ask waits a while");
        C(Regex.Matches(gifts, @"Speech\.Say\(").Count >= 3 && ret.Contains("Speech.Say(finder, line)"), "what they say goes through Speech.Say");
        C(offerings.Contains("OfferingGifts.AskForDemand(b, map, g, demand);"), "a dream that asks for something precious becomes an ask");
        C(touch.Contains("GodTouchReturn.Try(b, p, ours, home, now)") && touch.Contains("GodTouchMath.TierFor(") && touch.Contains("GodTouchMath.GapDays(k, lean, precious)"),
            "a taking's worth leans what follows it, and may be answered in kind");
        C(offerings.Contains("GodTouch.Coming(g, map, Gift(o, g, map, \"Plain\"") && offerings.Contains("GodTouch.Taken(g, shrine.Map, shrine.Position, new[] { p }, gift);"),
            "the gift's worth goes with what follows, seen plainly or found gone");
        C(ret.Contains("GodTouchFx.Show(p.god, map, cell, TouchTier.Notable, false);") && ret.Contains("GodPresence.Look(map, cell);") && ret.Contains("GodTouch.Write(ctx")
          && ret.Contains("GodLore.Returned(gift.lore, what);"), "a return in kind shown with the god's own look, the camera on it, and in the book");
        foreach (var fate in new[] { "\"Found\"", "\"Unseen\"", "\"Plain\"", "\"Given\" : \"Seen\"", "fate.ToString()" })
            C(offerings.Contains(fate), "the book keeps every fate: " + fate);
        C(lore.Contains("Scribe_Collections.Look(ref gifts, \"gifts\", LookMode.Deep);") && lore.Contains("Scribe_Collections.Look(ref tempers, \"tempers\", LookMode.Deep);")
          && Read("Gods/GodLore.cs").Contains("ExposeGifts();") && lore.Contains("OfferingGiftMath.BookGiftsAGod") && lore.Contains("OfferingGiftMath.BookGifts"),
            "saved with the book, capped");
        C(page.Contains("y = TemperPart(x, y, w, p);") && page.Contains("y = GiftsPart(x, y, w, p);") && !page.Contains("Favour"), "both in the god's chapter, the page showing no favour");
        C(temper.Contains("Gods.Favour(g, seat)") && temper.Contains("GodTemperMath.Read(") && !Regex.IsMatch(temper.Substring(temper.IndexOf("public static string Line")), @"\.step\.ToString|\+ t\.step|\{STEP\}"),
            "drawn from the real standing, through the keeper's reading; words only");
        C(worship.Contains("OfferingGifts.Daily(b, map, now)") && worship.Contains("GodTemper.Daily(now)"), "the asks and the readings on the region's day");
        C(book.Contains("Scribe_Values.Look(ref precious, \"precious\");") && book.Contains("Scribe_Defs.Look(ref want, \"want\");") && book.Contains("OfferingGifts.Spent(m, now)"),
            "an asked gift saved; the asks' marks let go");

        // ================= Defs and words =================

        var worshipDoc = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Worship_Gods.xml"));
        var gods = worshipDoc.Root.Elements("ColonyCouncil.GodWorshipDef").ToList();
        foreach (var g in gods)
        {
            string dn = (string)g.Element("defName");
            var lines = g.Element("temper")?.Elements("li").Select(e => (string)e).ToList() ?? new List<string>();
            C(lines.Count == GodTemperMath.Bands && lines.All(l => l.StartsWith("{NAME}") && !l.TrimEnd().EndsWith(".") && !Regex.IsMatch(l, @"\d") && !l.Contains("—") && !l.Contains("!")),
                dn + ": its temper in five lines of its own, words only");
            bool takes = (string)g.Element("takesNoOfferings") != "true";
            string hope = (string)g.Element("hope");
            C(!takes || !string.IsNullOrEmpty(hope) && char.IsLower(hope[0]) && !hope.EndsWith(".") && !Regex.IsMatch(hope, @"\d"), dn + ": what someone hopes a precious gift brings, as a belief");
        }
        string keyedAll = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Offerings.xml"));
        var keyed = Regex.Matches(keyedAll, @"<(CC_[A-Za-z_0-9]+)>(.*?)</\1>").Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "plain words: no dashes, no exclamations");
        C(!keyed.Where(kv => kv.Key.StartsWith("CC_GodTemper")).Any(kv => Regex.IsMatch(kv.Value, @"\d|favou?r|standing", RegexOptions.IgnoreCase)), "the temper never a number nor the word for it");
        var used = Regex.Matches(gifts + ret + lore + temper + page + offerings, @"""(CC_God(?:Gift|Temper)_[A-Za-z_0-9]*[A-Za-z0-9])""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        C(used.All(keyed.ContainsKey), "every word used is there (" + string.Join(", ", used.Where(k => !keyed.ContainsKey(k))) + ")");
        foreach (var fate in new[] { "Found", "FoundNobody", "Unseen", "Plain", "PlainNobody", "Seen", "SeenNobody", "Given", "Animal", "Passerby", "Returned", "Spoiled" })
            C(keyed.ContainsKey("CC_GodGift_Fate_" + fate), "the book's words for a gift " + fate);
        for (int b = 0; b < GodTemperMath.Bands; b++) C(keyed.ContainsKey("CC_GodTemper_Band_" + b), "the shared words for band " + b);
        var pools = File.ReadAllText(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Worship_Pools.xml"));
        foreach (var pool in new[] { "Rimfolk_Pool_Worship_AskGift", "Rimfolk_Pool_Worship_GiftYes", "Rimfolk_Pool_Worship_GiftNo", "Rimfolk_Pool_Worship_ReturnFound" })
        {
            int at = pools.IndexOf("<defName>" + pool + "</defName>", StringComparison.Ordinal);
            C(at >= 0, pool + " is there");
            if (at < 0) continue;
            string block = pools.Substring(at, pools.IndexOf("</ColonyCouncil.FlavorPoolDef>", at, StringComparison.Ordinal) - at);
            var lines = Regex.Matches(block, @"<li>(?:\[[^\]]*\]\s*)?(.*?)</li>").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            C(lines.Count >= 3 && lines.All(l => Regex.Matches(l, @"[.?](\s|$)").Count == 1 && !l.Contains("!")), pool + ": one sentence a line, said plainly");
        }
        var thoughts = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Worship_Thoughts.xml")).Root.Elements("ThoughtDef")
            .ToDictionary(e => (string)e.Element("defName"), e => float.Parse((string)e.Element("stages").Element("li").Element("baseMoodEffect"), System.Globalization.CultureInfo.InvariantCulture));
        C(thoughts.TryGetValue("Rimfolk_Worship_OfferedPrecious", out float up) && up > thoughts["Rimfolk_Worship_Offered"] && up <= 4f,
            "carrying something precious a larger good mood than an everyday gift, still small");
        C(thoughts.TryGetValue("Rimfolk_Worship_GiftKept", out float down) && down < 0f && down >= -2f, "a no only a little disappointing");
        string rates = File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Rates.cs"));
        C(rates.Contains("(\"Rimfolk_Worship_OfferedPrecious\"") && rates.Contains("(\"Rimfolk_Worship_GiftKept\""), "both moods in the balance model");
        C(File.ReadAllText(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Worship_Thoughts.xml")).Contains("<thoughts><li>Rimfolk_Worship_GiftKept</li></thoughts>"),
            "the setback says what helps");
        return n;
    }
}
