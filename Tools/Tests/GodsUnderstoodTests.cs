using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The gods made understandable (the player, 14 November 2026: "the god signs are awesome, go ahead! should be seen in the
/// book and on cards. also like the one-line meaning for the chapter"; docs/gods-next-proposals.md, sections A, B and D,
/// rounds 0 and 1): the fixes first (a work of art packed away can be given, so Cirrus and Smoulder get more than cloth;
/// every kind of gift has its words; a prayer and a gift give a small mood), then the signs (each god's, in every age's
/// form, in the book and on a card that names the god, nowhere else), each chapter's plain line in the keeper's hand, and
/// shrines made clear (the card on dedication, the inspect line, the carrier's why, the gift noticed gone by day).
/// </summary>
public static class GodsUnderstoodTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the gods understood: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        var defs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => (f, doc: XDocument.Load(f))).ToList();
        string keyedAll = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        var keys = new HashSet<string>(Regex.Matches(keyedAll, @"<([A-Za-z0-9_]+)>").Cast<Match>().Select(m => m.Groups[1].Value));

        // ================= Round 0: art can be given =================

        C(WorshipMath.ArtValueCap(6) > WorshipMath.OfferingValueCap(6) && WorshipMath.ArtValueCap(1000) == 600f,
            "a work of art may be worth more than another gift, never past 600");
        C(WorshipMath.ArtGiven(2, 150f, 2, 6) && WorshipMath.ArtGiven(3, 150f, 3, 6), "a stored sculpture of normal or good quality may be given while another stays");
        C(!WorshipMath.ArtGiven(2, 150f, 1, 6), "the colony's last stored work of art stays");
        C(!WorshipMath.ArtGiven(4, 150f, 3, 6) && !WorshipMath.ArtGiven(6, 150f, 3, 6), "nothing excellent or finer is given away");
        C(!WorshipMath.ArtGiven(2, WorshipMath.ArtValueCap(6) + 1f, 3, 6) && !WorshipMath.ArtGiven(2, 1f, 3, 6), "not a fortune, not a trifle");
        string offerings = Read("Gods/Worship/Offerings.cs");
        C(offerings.Contains("public static Thing Offered(Thing t)") && offerings.Contains("if (t is MinifiedThing m)") && offerings.Contains("if (!IsArt(inner) || Worship.Book?.ShrineOf(inner) != null) return null;"),
            "of what is packed away only art is given, and never a place given to a god");
        C(offerings.Contains("if (t is MinifiedThing) return true;") && offerings.Contains("var given = Offered(t);") && offerings.Contains("WorshipMath.ArtGiven(quality, given.MarketValue, art, colonists)"),
            "anything else packed away (a table, a bed, a machine) is never offered; the art judged by the art inside");
        C(offerings.Contains("t.IsInAnyStorage()") && offerings.Contains("ThingRequestGroup.MinifiedThing"), "only what the stores keep packed away counts; nothing on show is taken from its place");

        // ================= Round 0: words for every gift =================

        var pools = defs.SelectMany(d => d.doc.Root!.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.FlavorPoolDef"))
            .GroupBy(e => e.Element("defName")!.Value).ToDictionary(g => g.Key, g => g.SelectMany(e => e.Descendants("li")).Select(l => l.Value).ToList());
        var offer = pools.TryGetValue("Rimfolk_Pool_Worship_Offer", out var ol) ? ol : new List<string>();
        string Tags(string l) => Regex.Match(l, @"^\[([^\]]*)\]").Groups[1].Value;
        string Body(string l) => Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
        var sentence = new Regex(@"[.?!][""']?\s+[A-Z{]");
        foreach (var kind in Enum.GetNames(typeof(OfferingKind)))
            C(offer.Any(l => Tags(l) == "offer:" + kind.ToLowerInvariant()), $"a line of its own for a gift of {kind}");
        C(offer.Count > 0 && offer.All(l => Tags(l).Length > 0), "every offering line is for a kind of thing, a god, a demand or the Hunger: no line that fits nothing in particular");
        C(offer.All(l => !sentence.IsMatch(Body(l)) && Body(l).Length <= 90 && Body(l).Contains("{GOD}")), "one short sentence each, spoken to the god");
        var worshipDefs = defs.SelectMany(d => d.doc.Root!.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.GodWorshipDef")).ToList();
        foreach (var l in offer.Where(l => Tags(l).Contains("godkey:") && Tags(l).Contains("offer:")))
        {
            string god = Regex.Match(Tags(l), @"godkey:(\w+)").Groups[1].Value, kind = Regex.Match(Tags(l), @"offer:(\w+)").Groups[1].Value;
            var w = worshipDefs.FirstOrDefault(e => e.Element("gods")?.Elements("li").Any(x => x.Value.ToLowerInvariant() == god) == true);
            C(w != null && w.Element("likes")?.Elements("li").Any(x => x.Element("kind")?.Value.ToLowerInvariant() == kind) == true,
                $"{god}'s own words for {kind}: a kind its people give it");
        }
        var noticed = pools.TryGetValue("Rimfolk_Pool_Worship_Noticed", out var nl) ? nl : new List<string>();
        C(noticed.Count >= 6 && noticed.All(l => !sentence.IsMatch(Body(l)) && Body(l).Length <= 90 && Body(l).Contains("{GOD}")), "a gift noticed gone: one short sentence each, of the god");
        C(noticed.Any(l => Tags(l) == "scoffer") && noticed.Where(l => Tags(l) != "scoffer").All(l => Tags(l).Contains("!scoffer")), "a scoffer has a fox to blame, and only a scoffer");
        C(noticed.Any(l => Tags(l).StartsWith("mine")) && noticed.Any(l => Tags(l).StartsWith("theirs")) && noticed.Any(l => Tags(l).StartsWith("doubt")),
            "their own god's doing, the place's as others hold it, or a doubter's maybe");
        C(offerings.Contains("string words = Worship.Line(\"Rimfolk_Pool_Worship_Offer\", p, g, null, \"offer:\" + o.kind.ToString().ToLowerInvariant()") && offerings.Contains("Speech.Say(p, words);"),
            "the carrier says why they bring it, as they set it down");

        // ================= Round 0: a small mood =================

        var thoughts = defs.SelectMany(d => d.doc.Root!.Elements("ThoughtDef")).Where(e => e.Element("defName") != null).GroupBy(e => e.Element("defName")!.Value)
            .ToDictionary(g => g.Key, g => g.First());
        float Mood(string def) => float.Parse(thoughts[def].Descendants("baseMoodEffect").First().Value, System.Globalization.CultureInfo.InvariantCulture);
        bool Instead(string def) => thoughts[def].Descendants("li").Any(x => (string)x.Attribute("Class") == "ColonyCouncil.RewardInstead");
        foreach (var t in new[] { "Rimfolk_Worship_Prayed", "Rimfolk_Worship_PrayedAtShrine", "Rimfolk_Worship_Offered" })
            C(thoughts.ContainsKey(t) && !Instead(t), t + ": a mood of its own now");
        C(Mood("Rimfolk_Worship_Prayed") == WorshipMath.PrayedMood && Mood("Rimfolk_Worship_PrayedAtShrine") == WorshipMath.PrayedAtShrineMood && Mood("Rimfolk_Worship_Offered") == WorshipMath.OfferedMood,
            "the moods as the rules say: +1 a prayer, +2 at the god's place, +1 a gift carried");
        C(WorshipMath.PrayedAtShrineMood > WorshipMath.PrayedMood && WorshipMath.PrayedAtShrineMood < Mood("Rimfolk_Worship_Answered"), "small: more at the god's place, less than an answer");
        C(thoughts["Rimfolk_Worship_Offered"].Element("durationDays")?.Value == "1", "a gift's mood lasts a day");
        C(Read("Gods/Worship/Prayers.cs").Contains("r.atShrine ? WorshipDefOf.Rimfolk_Worship_PrayedAtShrine : WorshipDefOf.Rimfolk_Worship_Prayed")
          && Read("Gods/Worship/WorshipMore.cs").Contains("public static ThoughtDef Rimfolk_Worship_PrayedAtShrine;"), "the shrine's prayer gives its own");
        C(File.ReadAllText(Path.Combine(root, "Tools", "Balance", "Rates.cs")).Contains("(\"Rimfolk_Worship_PrayedAtShrine\""), "the new moods in the balance model");

        // ================= Round 1: the signs =================

        C(GodSignsMath.PlainPath("Hollin") == "ColonyCouncil/Gods/Signs/Hollin" && GodSignsMath.UnknownPath == "ColonyCouncil/Gods/Signs/Unknown",
            "the sign's texture by the god's key, and the seal of a god nobody can yet say");
        var signDir = Path.Combine(root, "Textures", "ColonyCouncil", "Gods", "Signs");
        C(!Directory.GetFiles(signDir, "*_*.png").Any(), "no ages' grey forms left in the game's textures (the player wanted the coloured signs)");
        foreach (var key in GodLoreMath.Keys)
            foreach (var tex in new[] { key, "Unknown" })
            {
                string png = Path.Combine(root, "Textures", "ColonyCouncil", "Gods", "Signs", tex + ".png");
                bool ok = File.Exists(png);
                if (ok)
                {
                    var b = File.ReadAllBytes(png);
                    int w = b[16] << 24 | b[17] << 16 | b[18] << 8 | b[19], h = b[20] << 24 | b[21] << 16 | b[22] << 8 | b[23];
                    ok = b.Length > 26 && b[1] == 'P' && w == 128 && h == 128 && b[25] == 6;
                }
                C(ok, $"the sign {tex}: 128 by 128, with its own transparency");
            }
        string script = File.ReadAllText(Path.Combine(root, "Tools", "make_god_signs.py"));
        C(GodLoreMath.Keys.All(k => script.Contains("(\"" + k + "\", ")) && script.Contains("UNKNOWN = (\"Unknown\", unknown,"),
            "the script draws every god and the unknown seal under the names the game reads");

        BookVerse V(BookPart part, int god, int tick, bool against = false) => new BookVerse { part = part, god = god, tick = tick, against = against, page = "X" };
        C(GodSignsMath.Believed(new List<BookVerse>()) == -1 && GodSignsMath.Believed(new[] { V(BookPart.Names, 3, 1), V(BookPart.Loves, 3, 2) }) == -1,
            "no sign and no plain line while nobody has told what it is under that name");
        C(GodSignsMath.Believed(new[] { V(BookPart.What, 3, 5), V(BookPart.What, 3, 6), V(BookPart.What, 5, 1) }) == 3, "the god most of what was told speaks of");
        C(GodSignsMath.Believed(new[] { V(BookPart.What, 5, 4), V(BookPart.What, 3, 2) }) == 3, "on a tie, the one told first");
        C(GodSignsMath.Believed(new[] { V(BookPart.What, 5, 1, true), V(BookPart.What, 3, 2) }) == 3, "a doubt counts for nothing");
        var sighted = V(BookPart.Known, 7, 3);
        sighted.key = "plain";
        var told = V(BookPart.Known, 7, 3);
        told.key = "CropBlight";
        C(GodSignsMath.Believed(new[] { sighted }) == 7 && GodSignsMath.Believed(new[] { told }) == -1, "a god seen plainly by the colony tells it too; a sign of its hand alone does not");

        string signs = Read("Gods/GodSigns.cs"), page = Read("Gods/GodLorePage.cs"), card = Read("Gods/Presence/GodCard.cs"), wiring = Read("Gods/GodsWiring.cs");
        C(signs.Contains("ContentFinder<Texture2D>.Get(path, false)") && !signs.Contains("Ages.Ours") && signs.Contains("v.god == g.id && GodSignsMath.Tells(v)"),
            "the sign loaded softly, in its colours, known once the colony was told what the god is");
        C(page.Contains("p.believed = GodSignsMath.Believed(p.verses);") && page.Contains("y = Title(x, y, w, p.title, also, first, SignOf(p));")
          && page.Contains("return key == null ? GodSigns.Unknown : GodSigns.Plain(key);"),
            "the book: the god's coloured sign over the chapter's name and small in the list, the unknown seal until someone has told what it is");
        C(page.Contains("UiKit.SizedLabel(x, y, w, title.CapitalizeFirst(), 26") && page.Contains("UiKit.Sized(38, TextAnchor.UpperLeft, false)")
          && !page.Contains("\"<size=38>\"") && !page.Contains("\"<size=26>\"") && !card.Contains("\"<size=28>\""),
            "the chapter's name and its first capital measured at the size they are drawn at, so neither is cut (the game's measuring strips size tags)");
        C(!page.Contains("Favour") && !page.Contains(".colour") && !page.Contains("ColorA") && !page.Contains("new Color("), "the book still shows no favor and no god's colour of its own");
        C(wiring.Contains("string sign = !veiled && GodSigns.Known(s.god) ? GodSigns.KeyOf(s.god) : null;") && card.Contains("GodSigns.Plain(letter.sign)")
          && card.Contains("sign = veiled ? null : sign") && card.Contains("Scribe_Values.Look(ref sign, \"sign\");"),
            "a card shows the sign only when it names the god and the colony was told what it is; never on a veiled card");
        C(!card.Contains("Symbol") && !card.Contains("Emblem"), "the card's sign is the colony's, not the old symbols");
        var onMap = Directory.GetFiles(Path.Combine(src, "Gods", "OnMap"), "*.cs").Concat(Directory.GetFiles(Path.Combine(src, "Gods", "Presence"), "*.cs"))
            .Where(f => !f.EndsWith("GodCard.cs") && !f.EndsWith("GodPresence.cs")).Concat(Directory.GetFiles(Path.Combine(src, "Gods", "Worship"), "*.cs"));
        C(onMap.All(f => !File.ReadAllText(f).Contains("GodSigns")), "no sign on a shrine and none over a god on the map (the player's pick)");

        // ================= Round 1: the plain lines =================

        var gods = defs.SelectMany(d => d.doc.Root!.Elements("ColonyCouncil.GodDef")).ToList();
        foreach (var g in gods)
        {
            string plain = g.Element("plain")?.Value ?? "";
            string key = g.Element("key")?.Value;
            C(plain.Length > 20 && plain.Length <= 140 && !plain.Contains("—") && !plain.Contains("!") && !plain.Contains("{") && Regex.Matches(plain, @"[.?]").Count <= 2,
                $"{key}: its plain line, one or two short sentences in plain words");
        }
        C(gods.Count == GodLoreMath.Keys.Length, "every god has its plain line");
        C(Read("Gods/GodDef.cs").Contains("[MustTranslate] public string plain;"), "the plain line is the def's, to translate");
        C(keys.Contains("CC_GodBook_Plain") && keys.Contains("CC_GodBook_PlainNobody") && Regex.IsMatch(keyedAll, @"<CC_GodBook_Plain>[^<]*\{KEEPER\}"),
            "in the keeper's hand, as the colony understands it");
        C(page.Contains("string plain = BelievedGod(p)?.def?.plain;") && page.Contains("y = PlainLine(x, y, w, p);"),
            "each chapter opens with its plain line, read only for the god the chapter holds itself to be about");

        // ================= Round 1: shrines made clear =================

        C(WorshipMath.ShrineDoings(true, true).SequenceEqual(new[] { "Gifts", "Pray", "Feast" }) && WorshipMath.ShrineDoings(false, false).SequenceEqual(new[] { "Pray" }),
            "a place's card says what the colony's people do there: gifts where they carry any, prayer always, the feast where they keep it");
        foreach (var d in new[] { "Gifts", "Pray", "Feast" }) C(keys.Contains("CC_Wsh_Card_Do_" + d), "the card's words: " + d);
        for (int s = 0; s < 4; s++) C(keys.Contains("CC_Wsh_Card_Season_" + s), "the feast's season in words: " + s);
        foreach (var f in Enum.GetNames(typeof(OfferingFate)).Where(f => f != "Waiting")) C(keys.Contains("CC_Wsh_Inspect_Fate_" + f), "what became of the last gift, in words: " + f);
        C(keys.Contains("CC_Wsh_Report_Look") && keys.Contains("CC_Wsh_Report_Look_NoGod"), "the look's report");
        string shrines = Read("Gods/Worship/Shrines.cs");
        C(shrines.Contains("Tell.Letter(NewsKind.Colony, Importance.Notable, map, place.CapitalizeFirst(), text + \"\\n\\n\" + Doings(g, map)")
          && shrines.Contains("Festivals.GodsFor(map).Contains(g)") && shrines.Contains("WorshipMath.ShrineDoings(GodWorshipDef.GiftsLeft(g), feast)"),
            "the dedication card says plainly what the colony will do there, only what it really does at this home");
        C(shrines.Contains("\"CC_Wsh_Inspect_Last\"") && shrines.Contains("v.part == BookPart.Loves && !v.against && v.page == chapter") && shrines.Contains("\"CC_Wsh_Inspect_Words\""),
            "the inspect line: the last gift and what became of it, and what someone here holds, as theirs");
        C(offerings.Contains("s.lastWhat = o.label;") && offerings.Contains("s.lastWords = words;") && offerings.Contains("if (s != null && s.lastAt == o.placed) s.lastFate = fate;"),
            "the place keeps its last gift, the words it was left with, and its fate");
        string book = Read("Gods/Worship/WorshipBook.cs");
        foreach (var f in new[] { "lastWhat", "lastBy", "lastWords", "lastAt", "lastFate", "noticed" })
            C(book.Contains("Scribe_Values.Look(ref " + f + ", \"" + f + "\""), "saved with the game: " + f);
        C(WorshipMath.Notices(10, 0.5f) && !WorshipMath.Notices(3, 0.5f) && !WorshipMath.Notices(10, WorshipMath.NoticeDays + 0.5f), "a gift gone in the night is noticed by day, within two days");
        C(offerings.Contains("o.noticed = fate != OfferingFate.Taken || plain || OfferingWayMath.SeenGoing(variant)") && offerings.Contains("WorshipJobs.LookJob") && offerings.Contains("if (!scoffer) Worship.Glow(shrine.Map, shrine.Position, g);"),
            "seen going plainly needs no noticing; else someone passing, or someone who goes to look, says whose doing they take it for, with a small light");
        var looks = defs.SelectMany(d => d.doc.Root!.Elements("JobDef")).FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_Worship_Look");
        C(looks != null && looks.ToString().Contains("<act>Look</act>"), "the look is a worship job of its own");
        C(Read("Gods/Worship/WorshipJobs.cs").Contains("case WorshipAct.Look:") && Read("Gods/Worship/WorshipJobs.cs").Contains("Offerings.Noticed(pawn, job.targetA.Thing);"),
            "arriving to look, they notice");
        string almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Gods.xml"));
        C(almanac.Contains("Dedicate to a god") && almanac.Contains("small good mood") && almanac.Contains("sign"), "the Almanac's page on the gods says plainly what a shrine is for");
        string estimate = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(estimate.Contains("\"Gods/Worship/WorshipBook.cs:shrines\""), "the shrines modelled in the save estimate");
        return n;
    }
}
