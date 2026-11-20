using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Revel, the Open Cup, the fifteenth god (the player, 15 and 16 November 2026: "a god of drugs, the god of excess partying
/// ... with the good and bad that they bring to a colony ... the god likes to deceive or kind of play with pawns, minds and
/// bodies", "the god could take shape inside an actual drug", "satyr would be awesome", "Revel sounds great"). The pure rules
/// of his own happenings (the pace each keeps, what calls each), the god in a cup (following his one through the game's own
/// splitting, merging and eating of stacks; the odds of what it does, good two times in three; what the one who took it
/// believes, never the truth marked), and the whole of him as the others are built: his def, worship, shape and look, sign,
/// presence, touch, ways, belief, saints, hands and stir; the unseen rule in his words; the harm only the game's own and the
/// shared budget's; every age by the colony's own drink and drugs, from every mod, never by a list of names; the mods named.
/// </summary>
public static class RevelTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "Revel, the Open Cup: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string own = Read("Gods/OnMap/OwnRevel.cs"), cup = Read("Gods/OnMap/RevelCup.cs"), goods = Read("Gods/OnMap/RevelGoods.cs"), dbg = Read("Gods/OwnRevelDebug.cs");
        string all = own + cup + goods;
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var T = GodPaceMath.Trigger.None;
        var O = GodPaceMath.Trigger.Ordinary;
        var S = GodPaceMath.Trigger.Strong;

        // ================= His happenings and their pace =================

        var mine = OwnHappeningsMath.Of("Revel").Where(h => !OwnHappeningsMath.IsBetween(h)).ToList();
        C(mine.Count == 8 && OwnHappeningsMath.Gods.Last() == "Revel" && mine.All(h => OwnHappeningsMath.GodOf(h) == "Revel"),
            "eight of his own: the long night, a vision, a craving, a stash, a binge egged on, the craving eased, the horned reveller, his cup");
        C(mine.Min(h => (int)h) > (int)OwnHappening.MogLostDead, "added after the others, so a save's list of when each last came keeps its order");
        foreach (var h in mine)
        {
            var p = OwnHappeningsMath.PaceOf(h);
            bool follows = p.mean <= 0f;
            C(follows || p.gap >= 5f && p.ramp > 0f, h + ": its own pace");
            C(OwnHappeningsMath.DayChance(h, 999f, T, 1f) == 0f, h + ": nothing at all without a reason in the world");
            if (follows) continue;
            C(OwnHappeningsMath.DayChance(h, p.gap - 0.5f, S, 1f) == 0f, h + ": never within its gap");
            float first = OwnHappeningsMath.DayChance(h, p.gap, O, 0f), later = OwnHappeningsMath.DayChance(h, p.gap + p.ramp, O, 0f);
            C(first > 0f && first < later, h + ": least likely right after its gap, more over its ramp");
        }
        C(OwnHappeningsMath.PaceOf(OwnHappening.RevelBinge).mean <= 0f, "the binge follows the game's own and has no chance of its own");
        C(OwnHappeningsMath.PaceOf(OwnHappening.RevelSatyr).gap >= 30f && OwnHappeningsMath.PaceOf(OwnHappening.RevelStash).gap >= 20f, "the horned reveller and a stash far apart");

        // ================= What calls each =================

        C(OwnHappeningsMath.NightTrigger(true, false, 2) == S && OwnHappeningsMath.NightTrigger(false, true, 2) == S && OwnHappeningsMath.NightTrigger(false, false, 3) == O
          && OwnHappeningsMath.NightTrigger(false, false, 2) == T && OwnHappeningsMath.NightTrigger(true, true, 1) == T,
            "a night runs long at his feast or a party, else round a late fire with three at their ease; never with one");
        C(OwnHappeningsMath.StaysUp(0.5f, 0.5f) && !OwnHappeningsMath.StaysUp(0.2f, 0.9f) && !OwnHappeningsMath.StaysUp(0.9f, 0.1f), "only the rested and fed stay up");
        C(OwnHappeningsMath.NightRestCost <= 0.25f && OwnHappeningsMath.NightJoyStep < 0.05f && OwnHappeningsMath.LongNightEnds <= 3, "a short night's sleep, the recreation a little at a time, bed by two or three");
        C(OwnHappeningsMath.VisionTrigger(false, true, true) == T && OwnHappeningsMath.VisionTrigger(true, false, false) == O && OwnHappeningsMath.VisionTrigger(true, true, false) == S,
            "a vision only for someone high, strongly at night or under his presence");
        int trues = Enumerable.Range(0, 1000).Count(i => OwnHappeningsMath.VisionTrue(i / 1000f, 0f));
        C(trues > 400 && trues < 500 && OwnHappeningsMath.VisionTrue(0.6f, 1f) && !OwnHappeningsMath.VisionTrue(0.8f, 1f),
            "a vision is true a little under half the time, more with him near, never always: believed either way");
        C(OwnHappeningsMath.FalseSights.All(k => keyed.ContainsKey("CC_GodRevel_Sight_" + k) && keyed["CC_GodRevel_Sight_" + k].Contains("{DIR}")), "every false sight in words, with its direction");
        C(OwnHappeningsMath.CravingTrigger(true, true, true, true) == S && OwnHappeningsMath.CravingTrigger(true, false, true, true) == O
          && OwnHappeningsMath.CravingTrigger(true, false, false, true) == T && OwnHappeningsMath.CravingTrigger(true, true, true, false) == T && OwnHappeningsMath.CravingTrigger(false, true, true, true) == T,
            "a craving gets up only an addict who wants it, with their drug in the stores");
        C(OwnHappeningsMath.StashTrigger(false, true) == T && OwnHappeningsMath.StashTrigger(true, false) == O && OwnHappeningsMath.StashTrigger(true, true) == S, "a stash only for someone far out");
        C(OwnHappeningsMath.StashCount(12f) == 8 && OwnHappeningsMath.StashCount(50f) == 2 && OwnHappeningsMath.StashCount(600f) == 1 && OwnHappeningsMath.StashCount(0f) == 0,
            "a stash is small: a few of the plain, one of the dear, never more than about a hundred silver's worth");
        C(OwnHappeningsMath.BingeTrigger(true, true) == S && OwnHappeningsMath.BingeTrigger(true, false) == T && OwnHappeningsMath.BingeSeenChance <= 0.5f, "a binge seen only by a witness, half of them");
        C(OwnHappeningsMath.LetGoTrigger(true, true, true) == S && OwnHappeningsMath.LetGoTrigger(true, true, false) == O && OwnHappeningsMath.LetGoTrigger(false, true, true) == T
          && OwnHappeningsMath.LetGoTrigger(true, false, true) == T, "the craving eased only for one in withdrawal with someone to sit up with them");
        C(Math.Abs(OwnHappeningsMath.Eased(0.8f) - (0.8f - OwnHappeningsMath.LetGoEase)) < 0.001f && OwnHappeningsMath.Eased(0.1f) > 0f && OwnHappeningsMath.LetGoEase < 0.5f,
            "the addiction lifted a little, the game's own hediff, never cured at once");
        C(OwnHappeningsMath.SatyrTrigger(false, false, 1, true) == T && OwnHappeningsMath.SatyrTrigger(false, false, 2, false) == O && OwnHappeningsMath.SatyrTrigger(true, false, 2, false) == S
          && OwnHappeningsMath.SatyrTrigger(false, false, 2, true) == S, "the horned reveller comes to a party, a feast, a long night or a late fire with people at it");
        C(OwnHappeningsMath.SatyrHours(21) == 7f && OwnHappeningsMath.SatyrHours(23) == 5f && OwnHappeningsMath.SatyrHours(1) == 3f, "gone before dawn, whatever the hour he came");
        C(OwnHappeningsMath.CupTrigger(false, false, true) == T && OwnHappeningsMath.CupTrigger(true, true, true) == T && OwnHappeningsMath.CupTrigger(true, false, false) == O
          && OwnHappeningsMath.CupTrigger(true, false, true) == S, "his cup only with drink or drugs in the stores, one at a time");

        // ================= The god in a cup: his one followed =================

        C(RevelCupMath.Split(3, 5, 10) == (true, 3) && RevelCupMath.Split(7, 5, 10) == (false, 2) && RevelCupMath.Split(4, 10, 10) == (false, 4) && RevelCupMath.Split(1, 1, 2) == (true, 1),
            "a split: he goes with the part taken off the front if he was among it, else his place moves up");
        C(RevelCupMath.Absorb(2, 5, 10) == (true, 12) && RevelCupMath.Absorb(6, 5, 10) == (false, 1) && RevelCupMath.Absorb(1, 0, 10) == (false, 1),
            "a merge: he comes across behind the stack's own if he was among what moved");
        C(RevelCupMath.Eat(1, 1) == (true, 0) && RevelCupMath.Eat(3, 1) == (false, 2) && RevelCupMath.Eat(2, 0) == (false, 2), "eaten: taken only if he was among what was taken");
        // Played out: a stack of 20 with him somewhere, hauled in parts, merged and drunk from one at a time; he is taken exactly once.
        var rng = new Random(15);
        int found = 0;
        for (int run = 0; run < 300; run++)
        {
            var stacks = new List<int> { 20 };
            int at = 0, k = RevelCupMath.Which(20, (float)rng.NextDouble());
            bool taken = false;
            for (int step = 0; step < 400 && !taken && stacks.Sum() > 0; step++)
            {
                int i = rng.Next(stacks.Count);
                if (stacks[i] <= 0) continue;
                int r = rng.Next(3);
                if (r == 0 && stacks[i] > 1)
                {
                    int count = 1 + rng.Next(stacks[i] - 1);
                    var res = i == at ? RevelCupMath.Split(k, count, stacks[i]) : (false, 0);
                    stacks[i] -= count;
                    stacks.Add(count);
                    if (i == at)
                    {
                        if (res.Item1) at = stacks.Count - 1;
                        k = res.Item2;
                    }
                }
                else if (r == 1 && stacks.Count > 1)
                {
                    int j = rng.Next(stacks.Count);
                    if (j == i || stacks[j] <= 0) continue;
                    int moved = stacks[j];
                    if (j == at)
                    {
                        var res = RevelCupMath.Absorb(k, moved, stacks[i]);
                        if (res.moves) at = i;
                        k = res.k;
                    }
                    stacks[i] += moved;
                    stacks[j] = 0;
                }
                else
                {
                    if (i == at)
                    {
                        var e = RevelCupMath.Eat(k, 1);
                        taken = e.taken;
                        k = e.k;
                    }
                    stacks[i]--;
                }
                if (!taken && (k < 1 || k > stacks[at])) break;
            }
            if (taken) found++;
            else if (stacks.Sum() == 0) found = -9999;
        }
        C(found > 250, $"followed through splits, merges and drinking, his one is taken once by whoever drinks it ({found} of 300 within the steps)");
        C(Enumerable.Range(0, 100).Select(i => RevelCupMath.Which(10, i / 100f)).Distinct().Count() == 10 && RevelCupMath.Which(1, 0.9f) == 1 && RevelCupMath.Which(5, 0.999f) == 5,
            "any one of a stack alike");

        // ================= What his cup does =================

        float total(bool a, bool h) => RevelCupMath.Weights(a, h).Sum(x => x.w);
        float deceit(bool a, bool h) => RevelCupMath.Weights(a, h).Where(x => RevelCupMath.Deceit(x.o)).Sum(x => x.w) / total(a, h);
        C(deceit(true, false) > 0.28f && deceit(true, false) < 0.38f && deceit(false, false) > 0.28f && deceit(false, false) < 0.38f,
            "his generosity two times in three, his deceit one in three");
        C(RevelCupMath.Weights(false, true).Single(x => x.o == CupOutcome.Hooked).w == 0f, "no addiction from a drug that has none");
        float hooked = RevelCupMath.Weights(true, false).Single(x => x.o == CupOutcome.Hooked).w / total(true, false);
        C(hooked > 0f && hooked <= 0.06f, $"an addiction at once about one cup in twenty ({hooked:P0}), through the shared harm budget");
        var picks = Enumerable.Range(0, 1000).Select(i => RevelCupMath.Pick(i / 1000f, true, false)).ToList();
        C(Enum.GetValues(typeof(CupOutcome)).Cast<CupOutcome>().Where(o => o != CupOutcome.None).All(picks.Contains) && !picks.Contains(CupOutcome.None),
            "every one of his effects can come, good or bad");
        C(RevelCupMath.HookedOr(false) == CupOutcome.Crash && RevelCupMath.HookedOr(true) == CupOutcome.Hooked, "an addiction the budget won't allow is a high and a crash instead");
        C(!RevelCupMath.SecondDose(true, true) && RevelCupMath.SecondDose(false, true) && !RevelCupMath.SecondDose(false, false), "a craving's second one never of a hard drug, only where the policy allows");
        C(RevelCupMath.CupWaitDays >= 20f && RevelCupMath.NoticeChance <= 0.15f && RevelCupMath.CrashHours > 0f, "he leaves his one after a month; noticed at most in passing, once");

        // ================= What they believe it was =================

        C(Enumerable.Range(0, 20).All(i => RevelCupMath.Read(CupOutcome.CleanHigh, true, false, i / 20f) == CupReading.Strong), "a scoffer believes it was only strong stuff");
        C(RevelCupMath.Read(CupOutcome.Vision, false, false, 0.9f) == CupReading.Shown && RevelCupMath.Read(CupOutcome.Vision, false, true, 0.1f) == CupReading.Shown,
            "a vision is taken for a true sight, true or not");
        C(RevelCupMath.Read(CupOutcome.CleanHigh, false, true, 0.9f) == CupReading.Tricked && RevelCupMath.Read(CupOutcome.Crash, false, false, 0.1f) == CupReading.Cursed
          && RevelCupMath.Read(CupOutcome.Crash, false, false, 0.9f) == CupReading.Tricked && RevelCupMath.Read(CupOutcome.Sharp, false, false, 0.9f) == CupReading.Blessed
          && RevelCupMath.Read(CupOutcome.Sharp, false, false, 0.1f) == CupReading.Strong,
            "one in dread takes even a kindness for a trick; the rest take a kindness for a gift (or only a strong batch) and his deceit for a trick or a curse");
        foreach (CupReading r in Enum.GetValues(typeof(CupReading)))
            if (r != CupReading.None) C(keyed.TryGetValue("CC_GodRevel_Believes_Cup_" + r, out var w) && w.Contains("{DRUG}") && w.Contains("{DATE}") && !w.Contains("Revel"),
                $"the book's words for {r}, as the one who took it would say it, never naming him");
        C(RevelCupMath.Doubts(CupReading.Strong) && !RevelCupMath.Doubts(CupReading.Blessed) && cup.Contains("GodLore.Believes(p, taken, words, true)") && cup.Contains("OwnRevel.TakenFor(g, map)"),
            "a scoffer's reading kept as a doubt; the god they took it for maybe the wrong one");

        // ================= The code: on the map, shown, the game's own =================

        C(cup.Contains("[HarmonyPatch(typeof(Thing), nameof(Thing.SplitOff))]") && cup.Contains("[HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]")
          && cup.Contains("[HarmonyPatch(typeof(Thing), nameof(Thing.Ingested))]") && Regex.Matches(cup, @"RimfolkPatches\.On").Count >= 3,
            "his one followed through the game's own stacks, off with the deeper hooks");
        C(cup.Contains("if (marked == null || t != marked || eating == t) return false;"), "a single compare while he is in no cup");
        C(cup.Contains("Harm.Roll(p, HarmKinds.GodCup, 1f)") && cup.Contains("Harm.Ail(p, addiction,") && HarmKinds.All.Contains(HarmKinds.GodCup) && HarmKinds.GodCup.system == "gods",
            "an addiction only through the shared harm budget, the drug's own (the game's hediff)");
        C(cup.Contains("RevelGoods.Addiction(def)") && goods.Contains("GetCompProperties<CompProperties_Drug>()?.chemical"), "the addiction is the drug's own chemical's");
        C(!Regex.IsMatch(all, @"Harm\.(Injure|Fight)\(|TakeDamage|DamageInfo"), "no wounds and no fights: the harm is only the game's own drugs");
        C(cup.Contains("Tell.Message(") && cup.Contains("GodPresence.Look(") && cup.Contains("\"cupnotice\""), "every effect shown and told as it happens; noticed at most in passing");
        C(!keyed.Where(kv => kv.Key.StartsWith("CC_GodRevel_Cup_")).Any(kv => kv.Value.Contains("god") || kv.Value.Contains("{GOD}")),
            "the cup itself looks like any other, and nothing told names a god");
        C(own.Contains("JobDefOf.Ingest") && own.Contains("RevelGoods.AllowedForAddiction(p, t.def)") && own.Contains("RevelGoods.WontTake(to, Cup(x)?.def)"),
            "the game's own taking of a drug, only as their drug policy allows (the player's choice)");
        C(own.Contains("GodAvatar.Make(x, GodsWiring.LookOf(g), Gods.Name(g), false, g.colour, g.colour2)") && own.Contains("\"AG_ForsakenHornsGoat\"") && own.Contains("\"Headbone_MiniHorns\"")
          && own.Contains("IndexOf(\"satyr\", StringComparison.OrdinalIgnoreCase)") && own.Contains("GodPresenceSeam.Call(spec)") && own.Contains("RevelCup.Mark(OwnHappenings.Book, cup, 1, false)")
          && own.Contains("GodStranger.Vanish(x)"),
            "the horned reveller: shown as the god, a satyr's shape where a mod has one, else goat horns (Alpha Genes', the game's), his moment, his own cup shared, gone before dawn");
        C(own.Contains("addiction.Severity = OwnHappeningsMath.Eased(") && own.Contains("SatUpThought") && own.Contains("SatWithMeThought"), "the craving eased: the game's own addiction lifted a little, both glad of the night");
        C(Read("Gods/OnMap/OwnSeven.cs").Contains("OwnRevel.Hourly(b, map, now)") && Read("Gods/OnMap/OwnSeven.cs").Contains("OwnRevel.Step(b, now)")
          && Read("Gods/OnMap/OwnSeven.cs").Contains("OwnRevel.Festival(b, map)") && Read("Gods/OnMap/OwnSeven.cs").Contains("Scribe_Deep.Look(ref revel, \"revel\")"),
            "in the frame: the gods' hour, the steps, his feast, saved with the book");
        C(!Regex.IsMatch(own + cup, @"Scribe_Collections") && !Regex.IsMatch(all, @"\bSched\."), "saved small (single records only) and on no clock slot of its own");
        foreach (var h in mine)
            C(Regex.IsMatch(own + cup, @"OwnHappenings\.Came\(b, OwnHappening\." + h + @", OwnHappenings\.Now\b"), h + ": marked as come when it comes");
        C(Regex.Matches(dbg, "\\[DebugAction\\(\"Rimfolk\", \"Gods: ").Count >= 10, "a dev action for each, and the cup found and forced");

        // ================= Every age, from the whole of the player's mods =================

        C(goods.Contains("DefDatabase<ThingDef>.AllDefsListForReading.Where(His)") && goods.Contains("OfferingGifts.MadeAgain(d, map)") && goods.Contains("AgeOf(d) <= ColonyAge")
          && goods.Contains("ThingDefOf.Beer"), "his drink and drugs read from every def loaded, the colony's unlocked first, then its age's, beer last");
        C(goods.Contains("drugCategory != DrugCategory.Medical") && goods.Contains("Hard(d)"), "never medicine; no hard drug left as a find");
        C(Read("Gods/Touch/GodTouchReturn.cs").Contains("case \"Revel\":") && Read("Gods/Touch/GodTouchEffects.cs").Contains("case \"Revel\":"), "his return in kind and his finds the colony's own kind");

        // ================= Him, as the others are built =================

        var gods = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Gods.xml")).Root!.Elements("ColonyCouncil.GodDef").ToDictionary(e => e.Element("key")!.Value);
        var revel = gods["Revel"];
        C(revel.Element("label")?.Value == "Revel" && revel.Element("epithet")?.Value == "the Open Cup" && revel.Element("presence")?.Value == "Rimfolk_GodPresence_Revel"
          && revel.Element("look")?.Value == "Rimfolk_GodLook_Revel", "Revel, the Open Cup: the player's pick of name, his presence and his look");
        string lore = string.Join(" ", revel.Descendants().Where(e => !e.HasElements).Select(e => e.Value)).ToLowerInvariant();
        C(!Regex.IsMatch(lore, @"\b(love|lover|loving|kiss|bed with|seduc)"), "no loving: the player left it out");
        C(File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Gods", "Signs", "Revel.png")) && File.ReadAllText(Path.Combine(root, "Tools", "make_god_signs.py")).Contains("def revel():"),
            "his sign, drawn by the signs' tool in the others' style");
        var worship = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Worship_Gods.xml")).Root!.Elements().FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Worship_Revel");
        C(worship?.Element("taking")?.Value == "Cup" && worship.Element("likes")?.Elements("li").First().Element("kind")?.Value == "Drink" && worship.Element("temper")?.Elements("li").Count() == 5
          && worship.Element("festivalName") != null && worship.Element("hope") != null, "his worship: drink first, his own way of taking, his temper in words, his feast");
        C(PawnGodsMath.RiteOf("Revel") == BeliefRite.Cup && PawnGodsMath.RiteHour(BeliefRite.Cup, 20, 15f) && !PawnGodsMath.RiteHour(BeliefRite.Cup, 10, 15f)
          && PawnGodsMath.TraitDraws.Any(x => x.god == "Revel" && x.trait == "DrugDesire") && PawnGodsMath.BackstoryDraws.Any(x => x.god == "Revel" && x.word == "brewer"),
            "belief: the first drop poured in the evening; drawn by chemical interest, a brewer's past");
        C(GodTouchMath.Of("Revel").Any(e => e.effect == TouchEffect.Merry) && GodTouchMath.Of("Revel").Any(e => e.effect == TouchEffect.Sour && e.dark) && GodTouchMath.Lingers.Contains("Revel"),
            "his touch: high spirits, his merry air for days, the drink gone sour in his anger");
        C(GodPresenceOwnMath.JoyGain(PresenceEffect.Revelry, 1f, false) > 1.2f && GodPresenceOwnMath.JoyGain(PresenceEffect.Revelry, 1f, true) < 1f && GodPresenceOwnMath.JoyGain(PresenceEffect.Glint, 1f, false) == 1f,
            "his presence: recreation easier, in his anger slower");
        C(GodHandsMath.Of("Rimfolk_Evt_Brewer", null).god == "Revel" && GodHandsMath.Of("Council_Evt_CampFeast", null).god == "Revel", "his hand in a brewer's cart and a feast at a camp");
        C(GodLoreMath.Confused.ContainsKey("Revel"), "taken for another now and then (the Guest, the moon, the dark)");
        string health = Read("Gods/World/GodHealth.cs");
        C(health.Contains("case GodWrath.Sour: return Sour(g, map);") && health.Contains("case GodBlessing.Cask:") && health.Contains("Building_FermentingBarrel"),
            "in the world: his anger the drink gone sour, his kindness the barrels ready early");
        C(Read("Gods/GodNear.cs").Contains("case GodStir.Revelry:"), "drawn near by people high and a party under way");

        // ================= His words =================

        var happenings = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodHappenings_Revel.xml"));
        var pool = happenings.Root!.Elements("ColonyCouncil.FlavorPoolDef").Single(e => e.Element("defName")?.Value == "Rimfolk_Pool_GodRevel_Say").Descendants("li").Select(l => l.Value).ToList();
        foreach (var t in new[] { "night", "nightend", "morning", "vision", "visiontrue", "visionfalse", "cravingseen", "stash", "binge", "bingesaw", "letgo", "letgoeased", "satyrshare",
                     "cupnotice", "cupclean", "cupsharp", "cupbold", "cuphigh", "cupcrash", "cupcraving", "cuphooked" })
            C(pool.Any(l => Tags(l).Contains(t) && !Tags(l).Contains("named") && !Tags(l).Contains("scoffer")) && (own + cup).Contains("\"" + t + "\""), $"lines for '{t}', said by the code");
        foreach (var l in pool)
        {
            string body = Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
            C(FirstSentence(body) == body && body.Length <= 90 && !body.Contains("!") && !body.Contains("—"), $"'{body}' is one short sentence, plainly said");
            C(!body.Contains("{GOD}") || Tags(l).Contains("named"), $"'{body}' names him only where the colony is sure");
        }
        var used = Regex.Matches(all, "\"(CC_GodRevel_[A-Za-z_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).Distinct().ToList();
        foreach (var k in used.Concat(new[] { "Clean", "Sharp", "Bold", "High", "CrashCame", "Craving", "Hooked" }.Select(x => "CC_GodRevel_Cup_" + x)))
            C(keyed.ContainsKey(k), k + " is written");
        foreach (var kv in keyed.Where(kv => kv.Key.StartsWith("CC_GodRevel_")))
        {
            C(!kv.Value.Contains("—") && !kv.Value.Contains("!"), kv.Key + ": no em dash and no exclamation mark");
            C(kv.Key.EndsWith("TitleNamed") == kv.Value.Contains("{GOD}"), kv.Key + ": the god named only in a named title");
            C(!Regex.IsMatch(kv.Value, @"\bRevel\b"), kv.Key + ": never his own name");
        }
        var thoughts = happenings.Root.Elements("ThoughtDef").ToList();
        foreach (var t in thoughts.Where(t => t.Element("workerClass") == null))
        {
            int mood = int.Parse(t.Descendants("baseMoodEffect").First().Value);
            C(Math.Abs(mood) <= 5 && float.Parse(t.Element("durationDays")!.Value, System.Globalization.CultureInfo.InvariantCulture) <= 3f && all.Contains("\"" + t.Element("defName")!.Value + "\""),
                t.Element("defName")!.Value + ": a small mood for a day or two, given by the code");
        }
        C(thoughts.Count(t => t.Element("workerClass")?.Value == "ThoughtWorker_Hediff") == 3 && thoughts.Where(t => t.Element("workerClass") != null).All(t => t.Descendants("baseMoodEffect").All(m => Math.Abs(int.Parse(m.Value)) <= 16)),
            "the cup's moods through the game's own hediff thoughts, as strong as a drug's high at most");
        string rates = File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Rates.cs"));
        foreach (var t in new[] { "Rimfolk_Thought_Revel_LongNight", "Rimfolk_Thought_Revel_SlowMorning", "Rimfolk_Thought_Revel_SatUp", "Rimfolk_Thought_Revel_SatWithMe" })
            C(rates.Contains("(\"" + t + "\""), t + " in the balance model");

        // ================= The player's mods, by name, softly =================

        string catalogue = Defs(root);
        foreach (var m in SaintsMath.RevelMemes)
            C(Regex.IsMatch(catalogue, @"MemeDef\t" + Regex.Escape(m) + @"\t"), $"the meme {m} whose deities are his is in the player's enabled mods");
        var look = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodsOnMap_Shapes.xml")).Root!.Elements("ColonyCouncil.GodLookDef").Single(e => e.Element("defName")?.Value == "Rimfolk_GodLook_Revel");
        foreach (var name in look.Elements().Where(e => e.HasElements).SelectMany(e => e.Elements("li")).Select(l => l.Value))
            C(Regex.IsMatch(catalogue, @"\t" + Regex.Escape(name) + @"\t"), $"his look's {name} is in the player's enabled mods (or the game)");
        foreach (var name in new[] { "AG_ForsakenHornsGoat", "AG_DevilsheepHorns", "Headbone_MiniHorns", "Headbone_CenterHorn", "Pawn_Goat_Call", "AutobongSmoke", "Smoke_Joint", "Hangover",
                     "Inspired_Creativity", "VSIE_Party_Frenzy", "DrugDesire", "VTE_DrunkenMaster", "Ingest_Beer", "Plant_HealrootWild" })
            C(Regex.IsMatch(catalogue, @"\t" + Regex.Escape(name) + @"\t") && (all + Read("Gods/Touch/GodTouchEffects.cs") + Read("Gods/Belief/PawnGodsMath.cs") + Read("Gods/Presence/GodPresenceShows.cs")
                                                                                + Read("Gods/Worship/OfferingWays.cs") + Read("Gods/Touch/GodTouchFx.cs")).Contains("\"" + name + "\""),
                name + " is in the player's enabled mods (or the game) and named softly");
        return n;
    }

    private static HashSet<string> Tags(string line)
    {
        var m = Regex.Match(line, @"^\[([^\]]*)\]");
        return m.Success ? new HashSet<string>(m.Groups[1].Value.Split(',').Select(t => t.Trim())) : new HashSet<string>();
    }

    private static string FirstSentence(string s)
    {
        for (int i = 12; i < s.Length; i++)
            if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                return s.Substring(0, i + 1);
        return s;
    }

    private static string defsCache;

    private static string Defs(string root)
    {
        if (defsCache != null) return defsCache;
        using var gz = new System.IO.Compression.GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), System.IO.Compression.CompressionMode.Decompress);
        using var r = new StreamReader(gz);
        defsCache = string.Join("\n", r.ReadToEnd().Split('\n').Where(l => l.EndsWith("\tyes") || l.EndsWith("\tyes\r")));
        return defsCache;
    }
}
