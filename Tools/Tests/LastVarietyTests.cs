using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The content update's last variety items (the player's go of 20 November 2026): A9, a day off for a friend (its rules
/// without game types, Wishes/FriendDayOffMath.cs, the wish's def and words, kept apart from worn thin and the step-down
/// question, the worn one not told of a lapse); C25, the colony's names for its places (Places/PlaceNameMath.cs: what names
/// a place, its size, how many, how close, how a name fades, the place nearest a cell, the gods unseen in the names; the
/// words, the lines said aloud, the seams); C27, the room plans from the player's mods (each loading only with its mod, every
/// mod def name an active one in docs/player-mods).
/// </summary>
public static class LastVarietyTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "last variety: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        XElement Root(string rel) => XDocument.Load(Path.Combine(root, rel)).Root!;
        var keys = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                keys[el.Name.LocalName] = el.Value;
        bool Plain(string s) => !s.Contains('—') && !s.Contains('–') && !s.Contains('!');
        string Body(string line) => Regex.Replace(line ?? "", @"^\[[^\]]*\]\s*", "");
        bool OneSentence(string s)
        {
            s = s.Trim();
            var inner = s.Length > 1 ? s.Substring(0, s.Length - 1) : s;
            return !Regex.IsMatch(inner, @"[.?!]\s+[A-Z{]");
        }
        const int Day = 60000;

        // ================= A9: a day off for a friend =================

        C(!FriendDayOffMath.WornLongEnough(1.9f) && FriendDayOffMath.WornLongEnough(2f), "asked only after two days worn down");
        C(FriendDayOffMath.MayAsk(false, FriendDayOffMath.CloseFriend, -50) && !FriendDayOffMath.MayAsk(false, FriendDayOffMath.CloseFriend - 0.01f, 100),
            "anyone close enough may ask, by closeness, not by opinion alone");
        C(FriendDayOffMath.MayAsk(true, 0f, 0) && !FriendDayOffMath.MayAsk(true, 0f, -1), "a kind colonist asks for anyone they don't think badly of");
        C(FriendDayOffMath.Weight(1f, false, false) > FriendDayOffMath.Weight(0.6f, false, false)
          && FriendDayOffMath.Weight(0.6f, true, false) > FriendDayOffMath.Weight(0.6f, false, false)
          && FriendDayOffMath.Weight(0.6f, false, true) > FriendDayOffMath.Weight(0.6f, false, false), "closer, kinder and nearer breaking are likelier");
        C(FriendDayOffMath.Weight(5f, false, false) == FriendDayOffMath.Weight(1f, false, false) && FriendDayOffMath.Weight(-1f, false, false) > 0f, "the weight stays in bounds");
        C(FriendDayOffMath.Met(true, -999999, 100, false) && FriendDayOffMath.Met(false, 150, 100, false) && FriendDayOffMath.Met(false, -999999, 100, true)
          && !FriendDayOffMath.Met(false, 50, 100, false), "met by a day off under way or given since it was asked, or a rest day; never by an older one");
        C(FriendDayOffMath.Within(100, 100 + Day * 4, FriendDayOffMath.ApartDays, Day) && !FriendDayOffMath.Within(100, 100 + Day * 6, FriendDayOffMath.ApartDays, Day)
          && !FriendDayOffMath.Within(-1, 100, FriendDayOffMath.ApartDays, Day), "kept apart for five days, never for what never happened");
        C(FriendDayOffMath.CompanyChance > 0.5f && FriendDayOffMath.CompanyChance < 1f, "the one who asked often, not always, keeps them company");

        var wishes = Root("Defs/WishDefs/Rimfolk_Wishes_FriendDayOff.xml").Elements("ColonyCouncil.WishDef").ToList();
        var wish = wishes.FirstOrDefault(w => (string)w.Element("defName") == "Rimfolk_Wish_FriendDayOff");
        C(wish != null, "the wish's def is there");
        if (wish != null)
        {
            C((string)wish.Element("workerClass") == "ColonyCouncil.WishWorker_FriendDayOff", "its worker");
            C((string)wish.Element("grantedThought") == "Council_HelpedSomeone", "met: the one who asked is glad they spoke up");
            C(((string)wish.Element("label") ?? "").Contains("{OTHER_nameDef}"), "its label names the one it's for");
            C(wish.Element("allowForOthers") == null && wish.Element("talkInteraction") == null, "the asker's own wish about them, never talked about where they'd hear");
            foreach (var part in new[] { "request", "thanks", "expired" })
            {
                var lines = wish.Element(part)?.Descendants("li").Select(l => l.Value).ToList() ?? new List<string>();
                C(lines.Count >= 4, $"{part}: lines in several tones ({lines.Count})");
                foreach (var l in lines)
                {
                    C(Plain(Body(l)), $"{part}: no dash or exclamation mark: {l}");
                    C(!Regex.IsMatch(Body(l), @"(^|[.?]\s+)\{OTHER_(pronoun|objective|possessive)\}"), $"{part}: no sentence opens with a pronoun: {l}");
                }
            }
            var expired = string.Join(" ", wish.Element("expired")!.Descendants("li").Select(l => l.Value));
            C(!Regex.IsMatch(expired, @"\b(told|tell) (him|her|them) (that|I)\b"), "a lapse never claims the worn one was told");
        }
        foreach (var k in new[] { "CC_FriendDayOff_Reason", "CC_FriendDayOff_TieNote", "CC_FriendDayOff_VisitLabel", "CC_FriendDayOff_Advice" })
            C(keys.ContainsKey(k) && Plain(keys[k]), "a plain word: " + k);
        string fdo = Code("Wishes/FriendDayOff.cs");
        C(fdo.Contains("FriendDayOff.KeptApart") && fdo.Contains("WornThinArc") && fdo.Contains("StepDownQuestion"), "kept apart from worn thin and the step-down question");
        C(Code("CouncilLife/StepDown.cs").Contains("FriendDayOff.AskedFor") && Code("CouncilLife/LifeArcs.cs").Contains("FriendDayOff.AskedFor"),
            "neither begins for them while a friend's request waits");
        C(Code("CouncilDynamics.cs").Contains("w.Worker.OtherNotTold") && fdo.Contains("public override bool OtherNotTold => true"), "the worn one isn't told of a lapse");
        C(fdo.Contains("HeartActs.FriendVisit") && fdo.Contains("DilemmaEffect_Visit"), "company: a visit on the map, else the day off's own time with a friend");
        C(Code("Heart/HeartAdvice.cs").Contains("FriendDayOff.AdviceNote"), "the heart's advice says who asked");

        // ================= C25: the colony's names for its places =================

        foreach (var k in PlaceNameMath.Kinds)
        {
            C(PlaceNameMath.IsKind(k) && PlaceNameMath.Radius(k) >= 3 && PlaceNameMath.Radius(k) < PlaceNameMath.MinGap, $"{k}: a size smaller than the gap between names");
            string named = "CC_Place_Named_" + char.ToUpperInvariant(k[0]) + k.Substring(1);
            C(keys.ContainsKey(named) && keys[named].Contains("{PAWN_nameDef}") && keys[named].Contains("{PLACE}") && Plain(keys[named]), $"{k}: the message says who named it and the name");
        }
        C(!PlaceNameMath.IsKind("house") && !PlaceNameMath.IsKind(null), "only the five kinds");
        C(PlaceNameMath.Room(PlaceNameMath.MaxPlaces - 1) && !PlaceNameMath.Room(PlaceNameMath.MaxPlaces), "at most twelve a home");
        C(PlaceNameMath.TooClose(PlaceNameMath.MinGap * PlaceNameMath.MinGap - 1f) && !PlaceNameMath.TooClose(PlaceNameMath.MinGap * PlaceNameMath.MinGap), "no two names within ten cells");
        C(PlaceNameMath.Near(36f, 4) && !PlaceNameMath.Near(37f, 4) && PlaceNameMath.Near(0f, 1), "a cell is at a place within its size and two cells");
        C(PlaceNameMath.Alpha(-1, 999999, Day) == 1f && PlaceNameMath.Alpha(100, 100, Day) == 1f, "a place that stands has its whole label");
        float half = PlaceNameMath.Alpha(0, (int)(PlaceNameMath.FadeDays * Day / 2), Day);
        C(half < 1f && half > PlaceNameMath.FadedAlpha && PlaceNameMath.Alpha(0, (int)(PlaceNameMath.FadeDays * Day) + 1, Day) == PlaceNameMath.FadedAlpha, "one gone fades over its season");
        C(!PlaceNameMath.Forgotten(-1, 999999999, Day) && !PlaceNameMath.Forgotten(0, (int)(PlaceNameMath.FadeDays * Day) - 1, Day)
          && PlaceNameMath.Forgotten(0, (int)(PlaceNameMath.FadeDays * Day), Day), "forgotten after a season gone, never while it stands");
        C(PlaceNameMath.Spoken(-1) && !PlaceNameMath.Spoken(5), "a place gone isn't spoken of");
        C(PlaceNameMath.Definite("a great white thrumbo") == "the great white thrumbo" && PlaceNameMath.Definite("an old man") == "the old man"
          && PlaceNameMath.Definite("Bellwether") == "Bellwether" && PlaceNameMath.Definite("something in the wires") == "something in the wires"
          && PlaceNameMath.Definite("") == "" && PlaceNameMath.Definite(null) == null, "what was seen made definite, a name left as it is");
        var grounds = new HashSet<string>();
        for (int bits = 0; bits < 128; bits++)
            grounds.Add(PlaceNameMath.Ground((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0, (bits & 8) != 0, (bits & 16) != 0, (bits & 32) != 0, (bits & 64) != 0));
        foreach (var g in grounds) C(keys.ContainsKey("CC_Place_Ground_" + g), "a word for the ground: " + g);
        C(PlaceNameMath.Ground(true, true, true, true, true, true, true) == "shore" && PlaceNameMath.Ground(false, false, false, false, false, false, false) == "field",
            "the water first, a field when nothing else");
        C(PlaceNameMath.FloodWhat(true, true) == "path" && PlaceNameMath.FloodWhat(false, true) == "field" && PlaceNameMath.FloodWhat(false, false) == null,
            "a flood names the path first, then the fields, else nothing");
        C(PlaceNameMath.SupperDue(-1, 0, Day) && !PlaceNameMath.SupperDue(0, Day, Day) && PlaceNameMath.SupperDue(0, (int)(PlaceNameMath.SupperGapDays * Day), Day),
            "supper talk of a place every few days at most");
        foreach (var k in new[] { "CC_Place_Name_God", "CC_Place_Name_Graves", "CC_Place_Name_Tree", "CC_Place_Name_Gate", "CC_Place_Name_Flood_path", "CC_Place_Name_Flood_field",
                     "CC_Place_Chronicle", "CC_Place_Forgotten", "CC_Place_CardAt", "CC_Place_Gathering_Party", "CC_Place_Gathering_Wedding", "CC_Place_Gathering_Council",
                     "CC_Place_Gathering_Gathering", "CC_Settings_PlaceNames", "CC_Settings_PlaceNames_Tip", "CC_SetNote_PlaceNames", "CC_Settings_PlaceLabels",
                     "CC_Settings_PlaceLabels_Tip", "CC_SetNote_PlaceLabels" })
            C(keys.ContainsKey(k) && Plain(keys[k]), "a plain word: " + k);
        foreach (var k in keys.Keys.Where(k => k.StartsWith("CC_Place_Name_")))
            C(keys[k].StartsWith("the ") && keys[k] == keys[k].ToLowerInvariant().Replace("{ground}", "{GROUND}").Replace("{god}", "{GOD}").Replace("{tree}", "{TREE}"),
                "a name starts with 'the', in small letters, to sit inside a sentence: " + k);
        C(keys["CC_Place_Name_God"].Contains("{GOD}") && !keys["CC_Place_Name_God"].Contains("{GODOF}"), "a god's place names the god only as the colony knows it");
        string pn = Code("Places/PlaceNames.cs");
        C(pn.Contains("ArtTales.GodWords") && !pn.Contains("god.def.godOf") && !pn.Contains("ValueNow"), "the gods unseen: the colony's name where it knows, else what was seen, never favour");
        C(pn.Contains("Speech.Say(namer") && pn.Contains("Tell.Message") && pn.Contains("AddChronicle"), "a name is said aloud, told and kept");
        C(pn.Contains("PlaceNameMath.Forgotten") && pn.Contains("CC_Place_Forgotten"), "a place gone is forgotten after a season, in the chronicle");

        var pools = Root("Defs/FlavorPoolDefs/Rimfolk_Pools_PlaceNames.xml").Elements("ColonyCouncil.FlavorPoolDef").ToList();
        foreach (var name in new[] { "Rimfolk_Pool_PlaceNamed", "Rimfolk_Pool_PlaceSupper" })
        {
            var pool = pools.FirstOrDefault(p => (string)p.Element("defName") == name);
            C(pool != null, name + " is there");
            if (pool == null) continue;
            var lines = pool.Descendants("li").Select(l => l.Value).ToList();
            foreach (var l in lines)
            {
                string body = Body(l);
                C(Plain(body) && OneSentence(body) && body.Length <= 90 && !body.StartsWith("\"") && !body.StartsWith("{PLACE}"),
                    $"{name}: one plain sentence of 90 characters at most, not opening with the name: {l}");
                C(body.Contains("{PLACE}"), $"{name}: names the place: {l}");
                var tags = Regex.Match(l, @"^\[([^\]]*)\]").Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var t in tags) C(t == "named" || t.StartsWith("kind:") && PlaceNameMath.IsKind(t.Substring(5)), $"{name}: a known tag {t}");
            }
            foreach (var k in PlaceNameMath.Kinds)
                C(lines.Count(l => l.Contains("kind:" + k)) >= (name.EndsWith("Named") ? 2 : 1), $"{name}: lines for {k}");
        }
        var asides = Root("Defs/FlavorPoolDefs/Rimfolk_Letters_Aside.xml").Descendants("li").Select(l => l.Value).Where(l => l.Contains("colony:place")).ToList();
        C(asides.Count >= 5 && asides.All(l => l.Contains("{OURPLACE}") && l.Contains("[ours")), "the colony's letters name its places by the facts service's colony:place");
        C(asides.Any(l => l.Contains("olde")), "and in the medieval voice too");
        C(Code("Text/Facts/FactsColony.cs").Contains("\"OURPLACE\"") && Code("Text/Facts/FactsColony.cs").Contains("PlaceNames.Newest"), "the facts service gives the newest place that stands");
        C(Code("Gods/Gods.cs").Contains("PlaceNames.GodSeen"), "a god seen may name the place");
        C(Code("Defense/DefenseLinesWatch.cs").Contains("PlaceNames.LineHeld"), "a raid held may name the gate");
        C(Code("Gods/Presence/GodCard.cs").Contains("PlaceNames.WithPlace"), "a god's card names the place when it comes again where a name is");
        C(Code("CouncilGameComponent.cs").Contains("PlaceNames.Hourly"), "the hour looks at the graves, the gatherings, a flood, places gone and supper");
        string settings = Code("CouncilSettings.cs");
        C(settings.Contains("public bool placeNames = true;") && settings.Contains("public bool placeLabels = false;"), "names on, the label off by default");
        C(Code("UI/SettingsCatalog.cs").Contains("\"placeLabels\"") && Code("UI/SettingsCatalog.cs").Contains("\"placeNames\""), "both settings in the window");

        // ================= C27: room plans from the player's mods =================

        var active = new HashSet<string>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var reader = new StreamReader(gz))
        {
            reader.ReadLine();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var f = line.Split('\t');
                if (f.Length >= 4 && f[0] == "ThingDef" && OffForNow.Counts(root, f[3], f[2])) active.Add(f[1]);
            }
        }
        var rooms = Root("Defs/RoomTemplateDefs/Rimfolk_RoomTemplates_Mods.xml").Elements("ColonyCouncil.RoomTemplateDef").ToList();
        var want = new Dictionary<string, (string pkg, string key)>
        {
            ["Rimfolk_Room_WritersStudy"] = ("vanillaexpanded.vbookse", "VBE_WritersTable"),
            ["Rimfolk_Room_CoffeeCorner"] = ("vanillaexpanded.vbrewecandt", "VBE_EspressoMachineBuilding"),
            ["Rimfolk_Room_DryadGrove"] = ("vanillaexpanded.ideo.dryads", "VDE_DryadNest"),
            ["Rimfolk_Room_FestiveHall"] = ("ve.vanillachristmasexpanded", "VCE_FestiveFeastTable"),
            ["Rimfolk_Room_BeekeepersYard"] = ("sarg.rimbees", "RB_Beehouse"),
            ["Rimfolk_Room_FishHouse"] = ("vanillaexpanded.vcef", "Shelf")
        };
        foreach (var (defName, (pkg, key)) in want)
        {
            var r = rooms.FirstOrDefault(x => (string)x.Element("defName") == defName);
            C(r != null, defName + " is there");
            if (r == null) continue;
            C((string)r.Attribute("MayRequire") == pkg, $"{defName}: only with its mod ({pkg})");
            var keyPiece = r.Element("things")?.Elements("li").FirstOrDefault(li => li.Element("optional")?.Value == "false");
            C(keyPiece != null && keyPiece.Element("defs")!.Elements("li").Any(d => d.Value == key), $"{defName}: its key piece is {key}");
            foreach (var d in r.Descendants("li").Where(d => d.Attribute("MayRequire") != null))
                C(active.Contains(d.Value), $"{defName}: {d.Value} is in an active mod of the player's");
            foreach (var t in new[] { "description", "purpose" })
                C(Plain((string)r.Element(t) ?? "") && !string.IsNullOrEmpty((string)r.Element(t)), $"{defName}: a plain {t}");
            foreach (var tip in r.Element("tips")?.Elements("li").Select(x => x.Value) ?? Enumerable.Empty<string>())
                C(Plain(Body(tip)), $"{defName}: a plain tip: {tip}");
        }
        C(rooms.Count == want.Count, "six room plans from the player's mods");
        return n;
    }
}
