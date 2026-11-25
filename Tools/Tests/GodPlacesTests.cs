using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// The gods' places and their rites (Source/ColonyCouncil/Gods/Worship/GodPlaces*.cs, GodRites.cs; the player's request of
/// 24 November 2026, docs/gods-altars-proposals.md): who may have a place, how many, what suits, upkeep, a rite's outcome
/// and what each outcome brings (never nothing), the week's service, pilgrims, oaths, keepsakes; and in the source: the
/// dedication no longer pleases a god by itself, it waits for an offering and gathers the believers, the rites' fruit is
/// shared, the god's answer goes through the gods' touch, and every rite's blessing exists for every god's own.
/// </summary>
public static class GodPlacesTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "gods' places: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil", "Gods");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ================= Who, and how many (A1, A2) =================

        C(GodPlacesMath.Seeker(5f, 3f) && !GodPlacesMath.Seeker(5f, 12f) && !GodPlacesMath.Seeker(0f, 1f) && !GodPlacesMath.Seeker(PawnGodsMath.LeansFrom, 1f)
          && !GodPlacesMath.Seeker(5f, -1f),
            "a seeker: some belief lately grown, not yet leaning");
        C(GodPlacesMath.PlacesAllowed(0, 0) == 0 && GodPlacesMath.PlacesAllowed(0, 2) == 1 && GodPlacesMath.PlacesAllowed(1, 0) == 1 && GodPlacesMath.PlacesAllowed(2, 0) == 1
          && GodPlacesMath.PlacesAllowed(3, 0) == 2 && GodPlacesMath.PlacesAllowed(5, 0) == 2 && GodPlacesMath.PlacesAllowed(6, 0) == 3 && GodPlacesMath.PlacesAllowed(20, 0) == 3,
            "none without a believer or a seeker; one; a second at three who hold it; a third at six; never more");
        for (int h = 1; h <= 12; h++)
            C(GodPlacesMath.PlacesAllowed(h, 0) <= h, "never more places than people who hold it (" + h + ")");
        C(GodPlacesMath.HoldersFor(0) == 1 && GodPlacesMath.HoldersFor(1) == 3 && GodPlacesMath.HoldersFor(2) == 6 && GodPlacesMath.HoldersFor(3) == 0,
            "the believers another place needs, as the menu says it");
        for (int p = 0; p < GodPlacesMath.MostPlaces; p++)
            C(GodPlacesMath.PlacesAllowed(GodPlacesMath.HoldersFor(p), 0) > p, "that many believers do allow one more (" + p + ")");

        // ================= What suits (A3) =================

        C(GodPlacesMath.Suits(true, false, false, false, -1) == PlaceSuit.No, "a shield generator called a shrine is no place");
        C(GodPlacesMath.Suits(false, true, false, true, 4) == PlaceSuit.GraveNotDead && GodPlacesMath.Suits(false, true, true, true, 0) == PlaceSuit.Yes,
            "a grave or a sarcophagus only for a god of the dead, whatever its art");
        C(GodPlacesMath.Suits(false, false, false, true, 2) == PlaceSuit.ArtTooPoor && GodPlacesMath.Suits(false, false, false, true, GodPlacesMath.ArtQualityLeast) == PlaceSuit.Yes
          && GodPlacesMath.Suits(false, false, false, false, -1) == PlaceSuit.Yes,
            "a sculpture only of good quality or better; an altar always");
        C(GodPlacesMath.FitFactor(true) > 1f && GodPlacesMath.FitFactor(false) < 1f && GodPlacesMath.FitFactor(false) > 0.8f && GodPlacesMath.FitFactor(null) == 1f,
            "a place its believers say the god likes counts a little more; one that isn't, a little less; no liking known, as it is");

        // ================= Upkeep (A5, A6) =================

        C(!GodPlacesMath.Neglected(GodPlacesMath.NeglectDays) && GodPlacesMath.Neglected(GodPlacesMath.NeglectDays + 1), "untended for a season: neglected");
        C(GodPlacesMath.UndedicateFavour(0f) == -1f && GodPlacesMath.UndedicateFavour(30f) < GodPlacesMath.UndedicateFavour(5f) && GodPlacesMath.UndedicateFavour(1000f) == -7f,
            "taken from its god: felt the more the longer it was kept, to a limit");
        C(GodPlacesMath.RiteHour(true, 22) && GodPlacesMath.RiteHour(true, 2) && !GodPlacesMath.RiteHour(true, 12) && GodPlacesMath.RiteHour(false, 12),
            "a god of the night or the dead has its rites after dark");

        // ================= A rite's outcome and what it brings =================

        float plain = GodPlacesMath.RiteScore(2, 4, false, 6, false, 0.2f, 1f, true, false, false, 0.5f);
        C(GodPlacesMath.Outcome(plain) == RiteOutcome.Plain, "an ordinary rite with an offering goes as such rites go (" + plain.ToString("0.00") + ")");
        float best = GodPlacesMath.RiteScore(6, 6, true, 15, true, 1f, 1.2f, true, true, false, 0.9f);
        C(GodPlacesMath.Outcome(best) == RiteOutcome.Fine, "everyone there, a priest, a fine temple, a keepsake: a fine rite");
        float worst = GodPlacesMath.RiteScore(1, 6, false, 0, false, 0f, 0.9f, false, false, true, 0.1f);
        C(GodPlacesMath.Outcome(worst) == RiteOutcome.Poor, "alone at a neglected place on a bad day: a poor rite");
        C(GodPlacesMath.RiteScore(3, 6, false, 5, false, 0f, 1f, false, false, true, 0.5f) < GodPlacesMath.RiteScore(3, 6, false, 5, false, 0f, 1f, false, false, false, 0.5f)
          && GodPlacesMath.RiteScore(3, 6, false, 5, true, 0f, 1f, false, false, false, 0.5f) > GodPlacesMath.RiteScore(3, 6, false, 5, false, 0f, 1f, false, false, false, 0.5f)
          && GodPlacesMath.RiteScore(6, 6, false, 5, false, 0f, 1f, false, false, false, 0.5f) > GodPlacesMath.RiteScore(1, 6, false, 5, false, 0f, 1f, false, false, false, 0.5f),
            "neglect lowers it; a temple and more of the believers raise it");
        foreach (RiteOutcome o in Enum.GetValues(typeof(RiteOutcome)))
        {
            // The player: "not just be for flavor only. They should then always have some game impact".
            C(GodPlacesMath.RiteFavour(o, RiteKind.Service) > 0f && GodPlacesMath.BeliefScale(o, 1f) > 0f, "every rite moves belief and the god's regard (" + o + ")");
            C(o == RiteOutcome.Poor ? GodPlacesMath.BlessLevel(o) == 0 && GodPlacesMath.AnswerChance(o, RiteKind.Festival, 1.2f) == 0f
                    : GodPlacesMath.BlessLevel(o) == (int)o && GodPlacesMath.AnswerChance(o, RiteKind.Service, 1f) > 0f,
                "a blessing of the god's kind and a chance of its answer for all but a poor rite (" + o + ")");
            C(GodPlacesMath.MoodStage(o) == (int)o, "the mood's stage follows how it went (" + o + ")");
        }
        C(GodPlacesMath.BlessLevel(RiteOutcome.Fine) > GodPlacesMath.BlessLevel(RiteOutcome.Plain)
          && GodPlacesMath.AnswerChance(RiteOutcome.Fine, RiteKind.Service, 1f) > GodPlacesMath.AnswerChance(RiteOutcome.Good, RiteKind.Service, 1f)
          && GodPlacesMath.AnswerChance(RiteOutcome.Good, RiteKind.Dedication, 1f) > GodPlacesMath.AnswerChance(RiteOutcome.Good, RiteKind.Service, 1f)
          && GodPlacesMath.AnswerChance(RiteOutcome.Fine, RiteKind.Festival, 2f) <= 0.5f,
            "the better it went the more it brings; a dedication or a festival more than a week's service; never more than even odds of an answer");
        C(GodPlacesMath.AnswerLean(RiteOutcome.Fine, 1.2f) > GodPlacesMath.AnswerLean(RiteOutcome.Plain, 1f) && GodPlacesMath.AnswerLean(RiteOutcome.Plain, 0.9f) >= 0.5f,
            "what follows an answered rite leans by how it went");
        C(GodPlacesMath.RoomFine(true, 100f, 0, 0) == 1f && GodPlacesMath.RoomFine(true, 20f, 0, 0) == 0f && GodPlacesMath.RoomFine(false, 0f, 3, 1) > GodPlacesMath.RoomFine(false, 0f, 0, 0)
          && GodPlacesMath.RoomFine(false, 0f, 20, 0) <= 0.6f,
            "the place's room: indoors by its impressiveness, outdoors by what stands round it");

        // ================= Service, pilgrims, oaths, keepsakes =================

        C(GodPlacesMath.ServiceDays == 7 && GodPlacesMath.ServiceHour(18) && !GodPlacesMath.ServiceHour(12), "a temple's service once a week, in the early evening");
        C(GodPlacesMath.PilgrimGift(3) >= 30 && GodPlacesMath.PilgrimGift(100) == 150 && GodPlacesMath.PilgrimGoodwill > 0, "pilgrims leave a gift by the home's size, and their people's goodwill");
        C(GodPlacesMath.OathWeight > 1f && GodPlacesMath.OathKeptFavour > 0f, "an oath sworn at the place weighs more");

        // ================= In the source =================

        string shrines = Read("Worship/Shrines.cs");
        string rites = Read("Worship/GodRites.cs");
        string places = Read("Worship/GodPlaces.cs");
        C(!shrines.Contains("WorshipMath.DedicateFavour"), "the dedication itself pleases no god (A5)");
        C(shrines.Contains("GodPlaces.WhyNot(t, g)") && shrines.Contains("GodPlaces.WhyNot(t, god)"), "asking and the menu both follow who may have a place");
        C(shrines.Contains("OfferingRecord { shrine = r.thing") && shrines.Contains("OwnFeasts.Gather(who, r.thing, g, GodPlacesMath.DedicationTicks"),
            "the dedication waits for an offering and gathers the believers (A4)");
        C(shrines.Contains("GodRites.Held(RiteKind.Dedication"), "the dedication is a rite with its fruit");
        C(Read("Worship/Festivals.cs").Contains("GodRites.Held(RiteKind.Festival") && rites.Contains("Held(RiteKind.Service") && rites.Contains("Held(RiteKind.GameRite"),
            "the festival's rite, the service and the game's rites at the place share the fruit");
        C(rites.Contains("GodTouch.Rite(") && Read("Touch/GodTouch.cs").Contains("public static void Rite("), "the god's answer to a rite goes through the gods' touch, with its own waits");
        C(Read("Worship/Prayers.cs").Contains("GodPlaces.Neglected(shrine)") && Read("Worship/Prayers.cs").Contains("GodPlaces.Tend(shrine)"),
            "a prayer at a neglected place counts for less and tends it");
        C(Read("Worship/Vows.cs").Contains("GodRites.SwearAtPlace(p, g)") && Read("Worship/Vows.cs").Contains("GodRites.OathEnded(r, g, kept)"), "vows are sworn at the place and weigh more");
        C(places.Contains("[HarmonyPatch(typeof(Building), nameof(Building.Destroy))]") && places.Contains("DestroyMode.Deconstruct"), "a place torn down by the colony is felt");
        C(Regex.IsMatch(rites, @"\[HarmonyPatch\(typeof\(LordJob_Ritual\), nameof\(LordJob_Ritual\.ApplyOutcome\)\)\]"), "the game's rites at a god's place carry the god");

        // ================= The defs =================

        string defs = File.ReadAllText(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Worship_Places.xml"));
        string touch = File.ReadAllText(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodTouch.xml"));
        var gods = Regex.Matches(touch, @"<defName>Rimfolk_GodTouch_Bless_(\w+)</defName>").Select(m => m.Groups[1].Value).ToList();
        C(gods.Count >= 14, "the gods' own blessings are there to follow");
        foreach (var g in gods)
            C(defs.Contains("<defName>Rimfolk_Rite_Bless_" + g + "</defName>"), "a rite's blessing of " + g + "'s own kind");
        C(defs.Contains("<defName>Rimfolk_Rite_Bless</defName>"), "a plain blessing for a god without its own");
        foreach (var d in new[] { "Rimfolk_Worship_Service", "Rimfolk_Worship_Swear", "Rimfolk_Worship_Visit", "Rimfolk_Rite_Kept", "Rimfolk_GodKeepsake", "Rimfolk_Dlm_PlaceAsked", "Rimfolk_Dlm_RivalFeasts",
                     "Rimfolk_Worship_PlaceNeglected", "Rimfolk_Worship_RivalPlaces", "Rimfolk_Pool_Worship_Service", "Rimfolk_Pool_Worship_Swear", "Rimfolk_Pool_Worship_Pilgrim" })
            C(defs.Contains("<defName>" + d + "</defName>"), d + " is defined");
        C(Regex.Matches(defs, @"<defName>Rimfolk_Rite_Kept</defName>[\s\S]*?</ThoughtDef>").Cast<Match>().First().Value.Split("<baseMoodEffect>").Length - 1 == 4,
            "the rite's mood has a stage for each outcome");
        C(File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Things", "GodKeepsake.png")), "the keepsake has its picture");

        // ================= Pictures (C1 to C4) =================

        var probs = new System.Collections.Generic.List<string>();
        BackdropRecord Rec(string name, string place, string outdoors, string era, string god)
        {
            var fields = new System.Collections.Generic.Dictionary<string, string> { ["texPath"] = "x/" + name, ["place"] = place, ["outdoors"] = outdoors, ["era"] = era, ["god"] = god };
            return BackdropMath.Record(name, f => fields.TryGetValue(f, out var v) ? v : null, f => new string[0], probs);
        }
        var shore = Rec("shore", "outdoors", "coast", "any", null);
        var seaShrine = Rec("seaShrine", "outdoors", "coast", "medieval", "waters");
        var seaShrineSpacer = Rec("seaShrineSpacer", "outdoors", "coast", "spacer", "waters");
        C(probs.Count == 0 && seaShrine.god == "waters" && shore.god == "", "a backdrop of a god's place names its god's domain");
        Rec("bad", "outdoors", "coast", "any", "sea");
        C(probs.Any(x => x.StartsWith("god:")), "an unknown domain is a problem in the record");
        var recs = new System.Collections.Generic.List<BackdropRecord> { shore, seaShrine, seaShrineSpacer };
        var atShore = new BackdropWhere { place = BackdropPlace.Outdoors, group = "coast", seed = 3 };
        C(BackdropMath.Choose(atShore, recs).index == 0, "an ordinary shore never gets a god's place");
        var atShrine = new BackdropWhere { place = BackdropPlace.Outdoors, group = "coast", seed = 3, god = "waters", era = PortraitEra.Medieval };
        C(BackdropMath.Choose(atShrine, recs).index == 1, "praying at a god's place of the waters: its place, of the colony's age");
        var atOther = new BackdropWhere { place = BackdropPlace.Outdoors, group = "coast", seed = 3, god = "hearth", era = PortraitEra.Medieval };
        C(BackdropMath.Choose(atOther, recs).index == 0, "a god with no place backdrop of its own: the ordinary one");
        C(atShrine.Key != atShore.Key, "being at a god's place changes the backdrop's key");
        C(CreaturePortraitMath.GodKeys("Hollin", "Harvest", "Medieval").SequenceEqual(new[] { "god:Hollin:medieval", "god:Hollin", "god:harvest:medieval", "god:harvest" })
          && CreaturePortraitMath.GodKeys(null, "Night", "").SequenceEqual(new[] { "god:night" }) && CreaturePortraitMath.GodKeys(null, "None", "").Count == 0,
            "a god walking the map: its own pictures first, then its domain's, the colony's age before any");
        C(CreaturePortraitMath.BeastKeys("Bellwether", "Thrumbo").SequenceEqual(new[] { "godbeast:Bellwether", "Thrumbo" }), "a god's beast: its god's pictures, then its kind's");
        var creatures = new System.Collections.Generic.List<CreatureRecord>
        {
            new CreatureRecord { name = "a", race = "god:harvest", sex = "any", stage = CreatureStage.Adult },
            new CreatureRecord { name = "b", race = "Thrumbo", sex = "any", stage = CreatureStage.Adult }
        };
        C(CreaturePortraitMath.FirstCandidates(creatures, CreaturePortraitMath.GodKeys("Hollin", "Harvest", "Medieval"), CreatureStage.Adult, "").SequenceEqual(new[] { 0 })
          && CreaturePortraitMath.FirstCandidates(creatures, CreaturePortraitMath.BeastKeys("Bellwether", "Thrumbo"), CreatureStage.Adult, "").SequenceEqual(new[] { 1 }),
            "the first key with pictures stands");
        string reader = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "Faces", "PortraitReader.cs"));
        C(reader.Contains("Worship.IsPriest(p)) return \"priest\";"), "the colony's own priest is painted as a priest (C4)");
        string backdrops = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "Faces", "Backdrops.cs"));
        C(backdrops.Contains("GodPlaces.DomainAt(p)"), "the backdrop knows when someone is at a god's place (C1)");
        return n;
    }
}
