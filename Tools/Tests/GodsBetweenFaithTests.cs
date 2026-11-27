using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Round 7 of the gods between the colony's people (docs/content-update-proposals-2.md, section 1, D1 to D15; the player's go,
/// 27 November 2026: "just more content to the already existing mechanics"): fourteen more happenings in the frame of
/// 0.281.0, most of whichever god their people hold (each finds its god in the people), the Guest's and the Hum's own. Their
/// pure rules, how each plays out on the map (the dare looked at in the morning, the gift left as the god's own, the offering
/// taken only where the god's way leaves no sign, the lightning through the harm budget and never at a person), the unseen
/// gods (the dark of a bargain never named), every age, and their words. The frame's shared checks (pace, cards in three
/// wordings, book lines, spoken lines, dev actions) are GodsBetweenTests'.
/// </summary>
public static class GodsBetweenFaithTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the gods between them, round 7: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string frame = Read("Gods/OnMap/Between/Between.cs");
        string faith = Read("Gods/OnMap/Between/BetweenFaith.cs");
        string home = Read("Gods/OnMap/Between/BetweenFaithHome.cs");
        string signs = Read("Gods/OnMap/Between/BetweenFaithSigns.cs");
        string gods = Read("Gods/OnMap/Between/BetweenRound2Gods.cs");
        string code = faith + home + signs + gods;
        string debug = Read("Gods/OwnBetweenDebug.cs");
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keyed = Regex.Matches(keyedAll, @"^\s*<([A-Za-z0-9_]+)>(.*?)</\1>\s*$", RegexOptions.Multiline).Cast<Match>()
            .GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        var ext = defs.SelectMany(d => d.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.FlavorExtensionDef" && e.Element("defName")?.Value == "Rimfolk_PoolExt_GodBetween_Round2")).ToList();
        var lines = ext.SelectMany(e => e.Descendants("li")).Select(l => l.Value).ToList();
        string Tags(string l) => Regex.Match(l, @"^\[([^\]]*)\]").Groups[1].Value;
        var T = GodPaceMath.Trigger.None;
        var O = GodPaceMath.Trigger.Ordinary;
        var S = GodPaceMath.Trigger.Strong;

        // ================= The fourteen, after every older one =================

        var round = new[]
        {
            OwnHappening.FaithDare, OwnHappening.FaithCameThrough, OwnHappening.FaithSides, OwnHappening.EnnuBurying, OwnHappening.FaithLittleShrine, OwnHappening.FaithTurned,
            OwnHappening.PipWake, OwnHappening.FaithTakenOffering, OwnHappening.FaithBookRead, OwnHappening.FaithWeddingSign, OwnHappening.FaithRaiderWord, OwnHappening.FaithGift,
            OwnHappening.FaithBargain, OwnHappening.FaithLightning
        };
        C(round.All(h => (int)h > (int)OwnHappening.RevelWager) && (int)round.Last() == (int)OwnHappeningsMath.FirstLand3 - 1,
            "the fourteen at the end of the list before the content round 3's six on the land, so a save keeps its order");
        C(round.All(OwnHappeningsMath.IsBetween), "all between the colony's people (the frame's pace, cards, book and outcomes)");
        C(round.Count(OwnHappeningsMath.OfAnyGod) == 12 && OwnHappeningsMath.GodOf(OwnHappening.EnnuBurying) == "Ennu" && OwnHappeningsMath.GodOf(OwnHappening.PipWake) == "Pip"
          && !OwnHappeningsMath.OfAnyGod(OwnHappening.HollinBound) && !OwnHappeningsMath.OfAnyGod(OwnHappening.SmoulderBargain),
            "twelve of whichever god their people hold; the guest at the burying the Guest's own, the wake the Hum's");
        C(frame.Contains("public bool AnyGod => OwnHappeningsMath.OfAnyGod(H);") && frame.Contains("if (!k.AnyGod && (g == null || !OwnHappenings.Acts(g, map))) continue;")
          && debug.Contains("if (k == null || g == null && !k.AnyGod)"), "the hour's look and the dev actions come to one of any god with none, and it finds its god in the people");
        foreach (var h in round.Where(OwnHappeningsMath.OfAnyGod))
        {
            var cls = Regex.Match(code, @"public class " + h + @" : BetweenKind(.*?)(?=\n    public class |\z)", RegexOptions.Singleline).Groups[1].Value;
            C(cls.Contains("OwnHappenings.Acts(") || cls.Contains("OwnHappenings.Calls("), h + ": only where the god it found acts at the home (held, heard of, or drawn near)");
        }
        C(OwnHappeningsMath.PaceOf(OwnHappening.FaithLightning).gap >= 60f, "the lightning at most once a year at a home");
        C(OwnHappeningsMath.PaceOf(OwnHappening.EnnuBurying).gap >= 60f && gods.Contains("GodsOnMapBook.Instance?.stranger != null"), "the guest at the burying rare, never while another of the gods' strangers is here");

        // ================= Each one's rules =================

        // D1: the dare.
        C(OwnHappeningsMath.Dares(5, false, false, false).SequenceEqual(new[] { GodDare.Rain }) && OwnHappeningsMath.Dares(5, true, false, false).Count == 0
          && OwnHappeningsMath.Dares(1, false, true, true).SequenceEqual(new[] { GodDare.Sick, GodDare.Light }), "the dare: only what the morning settles (rain in a dry spell, the sick in bed, a light at the place)");
        C(OwnHappeningsMath.DareTrigger(false, 3, true) == T && OwnHappeningsMath.DareTrigger(true, 0, true) == T && OwnHappeningsMath.DareTrigger(true, 1, false) == O
          && OwnHappeningsMath.DareTrigger(true, 1, true) == S, "a believer and a scoffer with something to dare, likelier just after the believer's small rite");
        C(OwnHappeningsMath.DareEnd(true, 0f, 0.9f) == BetweenOutcome.None && OwnHappeningsMath.DareEnd(false, 50f, 0.9f) == BetweenOutcome.Closer
          && OwnHappeningsMath.DareEnd(false, -10f, 0.9f) == BetweenOutcome.Laughed && OwnHappeningsMath.DareEnd(true, 0f, 0.1f) == BetweenOutcome.Quarrel,
            "the morning's look: came (proud and shaken, given in the code), didn't (a good sport, else it stings), or an argument over it");
        C(faith.Contains("OwnBetween.Proud(H, believer);") && faith.Contains("OwnBetween.Shaken(scoffer, g);") && faith.Contains("GodWeather.Answer(g, map, 1f, true)")
          && faith.Contains("GodNear.Near(g, map)") && faith.Contains("OwnBetween.Doubted(scoffer, g, doubt);"),
            "the believer proud and the scoffer shaken; a god drawn near may answer the rain in its own way; the scoffer's word set beside it in the book");
        // D2: came through.
        C(OwnHappeningsMath.NearDeath(0.35f, 0f, 0f) && OwnHappeningsMath.NearDeath(0f, 0.5f, 0.9f) && !OwnHappeningsMath.NearDeath(0f, 0.5f, 0.5f) && !OwnHappeningsMath.NearDeath(0.1f, 0.2f, 1f),
            "came through: bled near to death, or a deadly illness past its worst");
        C(OwnHappeningsMath.Doctor(BetweenStance.Holds, -50f) == BetweenOutcome.Closer && OwnHappeningsMath.Doctor(BetweenStance.Neither, 0f) == BetweenOutcome.None
          && OwnHappeningsMath.Doctor(BetweenStance.Doubts, 50f) == BetweenOutcome.Closer && OwnHappeningsMath.Doctor(BetweenStance.Rival, 0f) == BetweenOutcome.Laughed,
            "the doctor: one of the same god stands with them, one who doubts or holds another says it was the stitches");
        C(faith.Contains("t.HitPoints < t.MaxHitPoints * 0.6f && map.areaManager.Home[t.Position]") && faith.Contains("FaithBetween.LeaveAsOffering(p, left, place, g);"),
            "what is left is a damaged weapon or armour from the home's ground, taken in as the god's own gift");
        C(faith.Contains("if (s == null || s.pending || s.god != g.id || !s.Live) return false;") && faith.Contains("o.shrine == place && o.fate == OfferingFate.Waiting")
          && faith.Contains("favour = LeftFavour"), "a gift left only at a place dedicated to that god with nothing else waiting, a small one, then the god's own way takes it (Offerings)");
        // D3: the sides.
        C(OwnHappeningsMath.Sat(0f, -10f) == BetweenOutcome.MadeUp && OwnHappeningsMath.Sat(-30f, 10f) == BetweenOutcome.None, "sat at one table: made up, or a coolness that stays");
        C(faith.Contains("GodWorld.Book?.Of(\"quarrel\")") && faith.Contains("d.stage == 0 ? (a, o) : (o, a)") && faith.Contains("\"Council_Office_Peacemaker\""),
            "the sides of a quarrel of two gods going on at the home, the one whose turn it is speaking first; the peacemaker, else the chaplain, else the best talker");
        // D4: the guest at the burying.
        C(OwnHappeningsMath.BuryingMay(true, false, true) && !OwnHappeningsMath.BuryingMay(true, true, true) && !OwnHappeningsMath.BuryingMay(true, false, false) && !OwnHappeningsMath.BuryingMay(false, false, true),
            "the guest at the burying: a fresh grave, no other stranger here, a people at peace to come from");
        C(gods.Contains("GodStranger.Make(g, map, out var f)") && gods.Contains("Arrivals.Visit(new List<Pawn> { x }") && gods.Contains("GodStranger.Vanish(x)") && gods.Contains("story?.Childhood?.TitleFor(dead.gender)"),
            "the Guest in disguise (its look's small tells), walking in as a visitor, unmade once off the land; the true thing from the dead's own past");
        C(gods.Contains("OwnHappenings.Age <= 1 ? \"MedicineHerbal\" : \"Silver\"") && keyed.ContainsKey("CC_GodB_EnnuBurying_Herbs") && keyed.ContainsKey("CC_GodB_EnnuBurying_Coin"),
            "every age: dried herbs on the grave for an early colony, a coin for a later one");
        // D5: the children's little place.
        C(OwnHappeningsMath.LittleShrineTrigger(2, true) == O && OwnHappeningsMath.LittleShrineTrigger(1, true) == T && OwnHappeningsMath.LittleShrineTrigger(2, false) == T,
            "two children, one leaning toward a god");
        C(OwnHappeningsMath.Visitor(true, true, true, 0.1f) == ShrineVisitor.Helper && OwnHappeningsMath.Visitor(true, true, true, 0.7f) == ShrineVisitor.Keeper
          && OwnHappeningsMath.Visitor(true, true, true, 0.95f) == ShrineVisitor.Scoffer && OwnHappeningsMath.Visitor(false, false, true, 0.1f) == ShrineVisitor.Scoffer
          && OwnHappeningsMath.Visitor(false, false, false, 0.5f) == ShrineVisitor.None, "who comes to it: one who holds the god mostly, the keeper, else a scoffer");
        C(home.Contains("p.DevelopmentalStage.Child()") && home.Contains("FaithBetween.Chunk(a, OwnHappenings.Age >= 2) ?? FaithBetween.Wood(a)") && !home.Contains("ThingDef.Named(\"Rimfolk_"),
            "children only; a stone (slag for a later colony) or sticks, real things and no building of its own");
        C(home.Contains("PawnGods.Told(a, who, g);") && home.Contains("OwnBetween.End(H, BetweenOutcome.Laughed, a, who);") && home.Contains("OwnHappeningsMath.FondFrom"),
            "the keeper's name for the god (the children hold it a little more); a scoffer's kick stings, and one fond of them resents it");
        C(home.Contains("// Only food gone is told of; with none left there, the morning has nothing to say."), "the morning tells of food gone only where food was left");
        // D7: the one who turned.
        C(OwnHappeningsMath.OldFaithful(25f) == BetweenOutcome.Closer && OwnHappeningsMath.OldFaithful(0f) == BetweenOutcome.Quarrel, "the old god's own: a friend understands, others call it a betrayal");
        C(home.Contains("m.what != \"turned\"") && home.Contains("c.what == \"crisis\"") && home.Contains("rite == BeliefRite.Rock ? FaithBetween.Chunk(p, false) : rite == BeliefRite.Tree ? FaithBetween.Wood(p) : null"),
            "a turning from the worship's own marks (the crisis before it the god turned from); a thing of the old rite carried");
        // D8: a machine's wake.
        C(OwnHappeningsMath.Served(WakeOf.Mech, 1.5f, false, 0f, 3) && !OwnHappeningsMath.Served(WakeOf.Mech, 0.5f, true, 5f, 3) && OwnHappeningsMath.Served(WakeOf.Turret, 0f, true, 1.2f, 2)
          && !OwnHappeningsMath.Served(WakeOf.Turret, 0f, false, 3f, 2) && !OwnHappeningsMath.Served(WakeOf.Generator, 0f, true, 0.5f, 2)
          && OwnHappeningsMath.Served(WakeOf.Bench, 0f, true, 1.5f, 1) && !OwnHappeningsMath.Served(WakeOf.Bench, 0f, true, 1.5f, 2),
            "a machine that served long: a mech a year old, the oldest turret or generator of a colony a year old, the oldest bench only for an early colony");
        C(OwnHappeningsMath.Served(WakeOf.Vehicle, 0f, true, 1.5f, 3) && !OwnHappeningsMath.Served(WakeOf.Vehicle, 0f, false, 1.5f, 3) && gods.Contains("p?.GetType().Name == \"VehiclePawn\""),
            "Vanilla Vehicles Expanded's oldest vehicle (active in the player's list) counts too, by name, softly");
        C(gods.Contains("[HarmonyPatch(typeof(Building), nameof(Building.Destroy))]") && gods.Contains("mode != DestroyMode.KillFinalize") && gods.Contains("[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]")
          && Regex.Matches(gods, @"if \(!RimfolkPatches\.On \|\| !Gods\.On").Count == 2 && gods.Contains("MechanitorUtility.GetOverseer(mech)"),
            "a machine destroyed (never torn down) or a mech dead, the hooks standing down with the deeper game hooks; the mech's overseer keeps its wake");
        // D9: the offering nobody's god took.
        C(OwnHappeningsMath.TakenTrigger(true, true, true) == O && OwnHappeningsMath.TakenTrigger(true, true, false) == T && OwnHappeningsMath.TakenTrigger(false, true, true) == T,
            "a gift waiting, one who would take it, one awake to see it");
        C(OwnHappeningsMath.Tells(0f, false, 0.9f) && OwnHappeningsMath.Tells(50f, true, 0.9f) && !OwnHappeningsMath.Tells(50f, false, 0.9f) && OwnHappeningsMath.TakenTo <= 24
          && OwnHappeningsMath.TakenFrom >= 21, "the one who saw it tells unless a friend of the taker who doesn't hold the god; taken in the late hours, before the gods' own");
        C(home.Contains("OfferingWays.Of(g) != OfferingWay.Vanish") && home.Contains("o.fate = OfferingFate.Taken;") && home.Contains("o.noticed = false;"),
            "only where the god's way leaves no sign, gone as the god's would be, for the believers to find empty in the morning (Offerings.Noticed)");
        var secret = defs.SelectMany(d => d.Elements()).FirstOrDefault(e => e.Name.LocalName == "ColonyCouncil.SecretDef" && e.Element("defName")?.Value == "Rimfolk_Secret_OfferingTaken");
        C(secret != null && secret.Element("scenario")?.Value == "true" && home.Contains("comp.secrets.Give(taker, def)") && home.Contains("secret.knowers.Add(witness)"),
            "kept quiet: a secret of the taker's (never given at random), the one who saw it among those who know it");
        // D10: the book read at the fire.
        C(OwnHappeningsMath.ReadTrigger(1, 3) == T && OwnHappeningsMath.ReadTrigger(3, 0) == T && OwnHappeningsMath.ReadTrigger(3, 2) == O && OwnHappeningsMath.ReadTrigger(9, 2) == S,
            "the reading: new entries since the last, and someone to sit and listen");
        C(signs.Contains("PawnGods.Told(p, keeper, g);") && signs.Contains("OwnBetween.Doubted(own, g, line);") && signs.Contains("GodTemper.Line(g, s.word)") && signs.Contains("OwnHappenings.Age >= 3 ? \"TextSlate\" : null"),
            "the listeners hold the god a little more; one whose words were read tells it their way, set beside it; the god's temper in the book's words only; a spacer reads from a slate");
        // D11: the wedding.
        C(OwnHappeningsMath.WeddingSigns(true, false, true, true).Count == 3 && OwnHappeningsMath.WeddingSigns(false, false, true, false).SequenceEqual(new[] { WeddingSign.GodSign })
          && OwnHappeningsMath.FirstBlesses(WeddingSign.Rain, 0.5f) && !OwnHappeningsMath.FirstBlesses(WeddingSign.LightOut, 0.5f),
            "the wedding's signs as the place allows (rain out of doors, a light that can go out), the god's own always; one guest a blessing and one a warning");
        C(signs.Contains("LordJob_Joinable_MarriageCeremony") && signs.Contains("BeliefEvent.SawOmen") && keyed.ContainsKey("CC_GodB_FaithWeddingSign_Journal"),
            "the game's own wedding; each guest holds their own god the harder, the couple write it down");
        // D12: a word over the raider.
        C(OwnHappeningsMath.OverRaider(20f, false) == BetweenOutcome.Closer && OwnHappeningsMath.OverRaider(20f, true) == BetweenOutcome.Quarrel && OwnHappeningsMath.OverRaider(0f, false) == BetweenOutcome.Quarrel,
            "a nod, or a quarrel (always from one who lost a friend in the fight)");
        C(signs.Contains("gods.Contains(g) && OwnBetween.Holds(p, g)") && signs.Contains("f.HostileTo(Faction.OfPlayer)") && signs.Contains("\"Council_Office_Marshal\""),
            "one who holds a god the raiders' people hold; one who lost a friend, else the marshal, answers");
        // D13: a gift at the other's place.
        C(OwnHappeningsMath.Gives(true, -20f, 10f) && OwnHappeningsMath.Gives(false, 10f, -5f) && !OwnHappeningsMath.Gives(false, -10f, 0f) && OwnHappeningsMath.GiftAfterDays >= 1f
          && OwnHappeningsMath.GiftWithinDays > OwnHappeningsMath.GiftAfterDays, "the kinder one, or the one who thinks better of the other, some days after the quarrel");
        C(home.Contains("\"Rimfolk_Between_Quarrel\", \"Rimfolk_Belief_GodsQuarrel\", \"Rimfolk_Worship_Bickered\"") && home.Contains("OwnBetween.End(H, BetweenOutcome.MadeUp, x, y);")
          && home.Contains("OwnBetween.End(H, BetweenOutcome.Wary, zealot, x);"), "after any of the gods' quarrels between two; made up, and a zealot of the giver's own god wary of them");
        // D14: a bargain suspected.
        C(OwnHappeningsMath.BargainTrigger(LuckOf.Bargained) == S && OwnHappeningsMath.BargainTrigger(LuckOf.Shunned) == S && OwnHappeningsMath.BargainTrigger(LuckOf.Lucky) == O
          && OwnHappeningsMath.BargainTrigger(LuckOf.None) == T, "a run of luck someone could notice, the cat's chosen as well as those who bargained");
        C(OwnHappeningsMath.Owns(LuckOf.Bargained, 0.1f) && !OwnHappeningsMath.Owns(LuckOf.Lucky, 0.1f) && !OwnHappeningsMath.Owns(LuckOf.Bargained, 0.9f),
            "only one who bargained may own it; the accusation can fall on one who made none");
        C(signs.Contains("OwnBetween.Card(H, null, map, accused.Position") && lines.Where(l => Tags(l).StartsWith("faithbargain_")).All(l => !l.Contains("{GOD}") && !Tags(l).Contains("named"))
          && keyed.Where(kv => kv.Key.StartsWith("CC_GodB_FaithBargain") || kv.Key == "CC_GodB_Saw_FaithBargain").All(kv => !kv.Value.Contains("{GOD}")),
            "the dark never named: no god on the card's title, none in their words or the book's line");
        C(signs.Contains("GodWays.Shuns(hunger)") && signs.Contains("OwnBetween.End(H, BetweenOutcome.Wary, p, accused);"), "believers of the gods that turn from such bargains keep away from them");
        // D15: lightning on the shrine.
        C(OwnHappeningsMath.LightningTrigger(true, true, true) == O && OwnHappeningsMath.LightningTrigger(false, true, true) == T && OwnHappeningsMath.LightningTrigger(true, false, true) == T
          && OwnHappeningsMath.LightningTrigger(true, true, false) == T, "a thunderstorm, a busy place, and someone at home who holds its god's rival");
        C(OwnHappeningsMath.Rebuild(BetweenStance.Holds, BetweenStance.Holds) == BetweenOutcome.Closer && OwnHappeningsMath.Rebuild(BetweenStance.Holds, BetweenStance.Rival) == BetweenOutcome.Quarrel,
            "the place's own rebuild it together");
        C(signs.Contains("Harm.Roll(HarmKinds.GodLightning, OwnHappeningsMath.LightningChance)") && signs.Contains("new WeatherEvent_LightningStrike(map, cell)")
          && signs.Contains("x.Position.InHorDistOf(c, GodWorldMath.LightningClear)") && signs.Contains("StormSense.Thunder(map)") && signs.Contains("GenConstruct.PlaceBlueprintForBuild"),
            "harm: the game's own strike in a thunderstorm, through the shared budget, never with anyone near the cell; burned down, laid out again for the colony to build");

        // ================= Their words =================

        C(ext.Count == 1 && ext[0].Element("pool")?.Value == "Rimfolk_Pool_GodBetween_Say", "the lines joined to the first round's pool from a file of their own");
        foreach (var h in round)
        {
            string pc = h.ToString().ToLowerInvariant();
            C(lines.Count(l => Tags(l).StartsWith(pc + "_")) >= 6, h + ": its lines (" + lines.Count(l => Tags(l).StartsWith(pc + "_")) + ")");
        }
        C(lines.All(l => Regex.IsMatch(Tags(l), @"^[a-z0-9]+_[a-z]+(,named)?$")), "each line tagged with its part, and named only where the colony is sure");
        // The old voice where the gods' and faiths' own words are said: the new god's welcome, the Hum's words, the book's verses, the words over the raider.
        foreach (var part in new[] { "faithturned_words", "pipwake_hum", "faithbookread_read", "faithraiderword_words" })
            C(lines.Where(l => Tags(l).StartsWith(part)).Any(l => Regex.IsMatch(l, @"\b(thou|thee|thy|thine|eth|hath|keepeth|cometh|stoppeth|receiveth|judgeth|spake)\b|[a-z]eth\b")),
                part + ": in the old voice");
        foreach (var k in new[] { "CC_GodB_FaithDare_Rain", "CC_GodB_FaithDare_Sick", "CC_GodB_FaithDare_Light", "CC_GodB_Faith_RiteSpot", "CC_GodB_Faith_SomeWood", "CC_GodB_FaithLittleShrine_Sticks",
                     "CC_GodB_FaithBookRead_Fire" })
            C(keyed.ContainsKey(k) && !keyed[k].Contains("{GOD}"), k + ": its words");
        C(keyed["CC_GodB_FaithDare_Sick"].Contains("{SICK}") && keyed["CC_GodB_FaithDare_Light"].Contains("{PLACE}"), "the dare's words name the sick one and the place");
        C(keyed.Where(kv => kv.Key.StartsWith("CC_GodB_") && round.Any(h => kv.Key.Contains("_" + h + "_") || kv.Key.EndsWith("_" + h)))
              .All(kv => !kv.Value.Contains("!") && !kv.Value.Contains("—")), "plain words: no exclamation marks, no dashes");
        return n;
    }
}
