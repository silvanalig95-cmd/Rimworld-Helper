using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Better armour and clothes for each colonist (docs/design/apparel.md, step 3, point 6; Apparel/ApparelPickMath.cs): the
/// thresholds; the cold and heat that count, as the game's "Need warm clothes" counts them; the raids met; work; pieces
/// that fit together; Bautista at -25 °C in a ratty shirt (warmth first, the ratty shirt replaced); the outfit (never
/// suggested past it, said with a way to allow it); the rules of title, role and faith (never taken off for a piece that
/// doesn't meet them, a piece they lack counting); locked pieces; lucky items and heirlooms warned of, never blocking;
/// armour and helmets for the fighters of a raid-heavy colony; the coldest and the fighters first when pieces are short;
/// the threshold and worn-out pieces always worth it; a piece in place of several judged in each one's role; never
/// leaving them short of warmth; trades of armour; one piece to one colonist and a colonist's pieces fitting together on
/// random colonies; the asks; who should wear a piece; the real base game's pieces judged in another role; the lines in
/// every age; and the seams (settings, clock, jobs, the gear tab, Nice Inventory Tab, the Defenses page, the card, the
/// inbox, the keys, the save).
/// </summary>
public static class ApparelPickTests
{
    static readonly string[] Torso = { "Torso", "Neck", "Shoulders", "Arms" };
    static readonly string[] Head = { "UpperHead", "FullHead" };

    sealed class Wardrobe
    {
        public readonly WearInput I = new WearInput();

        public WearColonist C(string key, float fights = 1f, float coldShort = 0f, string work = null, bool canFight = true)
        {
            var c = new WearColonist { Key = key, Name = key, Fights = fights, ColdShort = coldShort, ColdSpare = coldShort > 0f ? -coldShort : ApparelPickMath.NoLimit, Work = work, CanFight = canFight };
            I.Colonists.Add(c);
            return c;
        }

        /// <summary>A piece: in storage (the default), worn by someone, or carried in someone's pack.</summary>
        public WearPiece P(string key, string role, string layer, string[] groups, float overall = 5.5f, float warmth = 0f, string wornBy = null, string carriedBy = null,
            int stage = -1, bool dead = false, string def = null, string group = null)
        {
            var p = new WearPiece
            {
                Key = key, Label = key, Def = def ?? key, Group = group ?? "", Role = role, Overall = overall, Layers = new List<string> { layer }, Groups = groups.ToList(),
                Warmth = warmth, Stage = stage, FromDead = dead, WornBy = wornBy, CarriedBy = carriedBy, Stored = wornBy == null && carriedBy == null
            };
            I.Pieces.Add(p);
            if (wornBy != null) I.Colonist(wornBy).Wears.Add(key);
            if (carriedBy != null) I.Colonist(carriedBy).Carries.Add(key);
            return p;
        }

        public WearOption O(string c, string p, float rating, params string[] replaces)
        {
            var o = new WearOption { Rating = rating };
            o.Replaces.AddRange(replaces);
            I.Options[(c, p)] = o;
            return o;
        }

        public List<WearSuggestion> Assign() => ApparelPickMath.Assign(I);
    }

    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "better clothes: " + what);
        }
        bool Near(float a, float b, float eps = 0.01f) => Math.Abs(a - b) <= eps;
        string root = Directory.GetCurrentDirectory();
        while (root != null && !File.Exists(Path.Combine(root, "CLAUDE.md"))) root = Path.GetDirectoryName(root);
        string Read(string rel) => root != null && File.Exists(Path.Combine(root, rel)) ? File.ReadAllText(Path.Combine(root, rel)) : "";

        // ---------- The thresholds ----------
        C(Near(ApparelPickMath.Threshold, 0.8f) && ApparelPickMath.Threshold > WeaponPickMath.Threshold, "an upgrade asks 0.8, a little more than a weapon's 0.6");
        C(Near(ApparelPickMath.AskThreshold, 2f * ApparelPickMath.Threshold), "a colonist asks for twice the gear tab's gain");
        C(Near(ApparelPickMath.GapFloor, 2f * ApparelPickMath.Nothing), "a gap is filled with anything rated twice nothing at all");
        C(ApparelPickMath.AddOnAt > 5.5f, "a piece for an empty slot that is no gap must be better than the middle on its own");

        // ---------- The cold and heat that count ----------
        {
            var (s1, sp1) = ApparelPickMath.Margin(16f, -25f, true);
            C(Near(s1, 41f) && Near(sp1, -41f), "-25 °C against 16 °C comfort: 41 short");
            var (s2, sp2) = ApparelPickMath.Margin(-30f, -25f, true);
            C(Near(s2, 0f) && Near(sp2, 5f), "dressed for -30 °C: nothing short, 5 to spare");
            var (s3, sp3) = ApparelPickMath.Margin(16f, 8f, true);
            C(Near(s3, 0f) && Near(sp3, ApparelPickMath.NoLimit), "nights of 8 °C ask nothing of the clothes, as the game's alert only counts under 5 °C");
            var (s4, _) = ApparelPickMath.Margin(26f, 38f, false);
            C(Near(s4, 12f), "a 38 °C day against 26 °C comfort: 12 short for the heat");
            var (s5, sp5) = ApparelPickMath.Margin(26f, 28f, false);
            C(Near(s5, 0f) && Near(sp5, ApparelPickMath.NoLimit), "28 °C days ask nothing of the clothes");
        }

        // ---------- The raids met ----------
        {
            int now = 100 * 60000;
            var raids = new[]
            {
                new RaidSeen { Tick = now - 2 * 60000, Count = 10, Mechs = 10 }, new RaidSeen { Tick = now - 10 * 60000, Count = 6, Tribal = true },
                new RaidSeen { Tick = now - 50 * 60000, Count = 4 }, new RaidSeen { Tick = now - 200 * 60000, Count = 4 },
            };
            var (w, mechs, melee) = ApparelPickMath.Raids(raids, now);
            C(Near(w, 2.5f), $"raids weighed by how long ago, as the weapons weigh them: two this month and one older ({w})");
            C(Near(mechs, 1f / 2.5f) && Near(melee, 1f / 2.5f), "the shares of mechanoids and of crowds at close quarters");
            C(new WearInput { Raids = w }.RaidHeavy && !new WearInput { Raids = 1.5f }.RaidHeavy, "raid-heavy from two raids this month");
            var none = ApparelPickMath.Raids(null, now);
            C(none.weighed == 0f && none.mechs == 0f, "no raids, no weight");
        }

        // ---------- Work ----------
        {
            var apron = new ApparelFacts();
            apron.Offsets["CookSpeed"] = 0.1f;
            apron.Offsets["FoodPoisonChance"] = -0.05f;
            C(Near(ApparelPickMath.WorkLean(apron, "cook"), 0.15f), "a cook's apron: faster cooking and less food poisoning both count");
            C(Near(ApparelPickMath.WorkLean(apron, "doctor"), 0f) && ApparelPickMath.WorkLean(apron, null) == 0f, "nothing for a doctor or for someone with no such work");
            var plate = new ApparelFacts();
            plate.Offsets["WorkSpeedGlobal"] = -0.1f;
            C(ApparelPickMath.WorkLean(plate, "crafter") < -ApparelPickMath.WorkAt, "armour that slows all work slows a crafter's");
            foreach (var work in ApparelPickMath.WorkStats.Keys)
                C(Read("Languages/English/Keyed/Rimfolk_Apparel.xml").Contains("<CC_ApparelPick_Work_" + work + ">"), $"the work {work} has its words");
        }

        // ---------- Pieces that fit together ----------
        {
            var shirt = new WearPiece { Key = "a", Layers = { "OnSkin" }, Groups = { "Torso" } };
            var parka = new WearPiece { Key = "b", Layers = { "Shell" }, Groups = { "Torso" } };
            var duster = new WearPiece { Key = "c", Layers = { "Shell" }, Groups = { "Torso", "Legs" } };
            var hat = new WearPiece { Key = "d", Layers = { "Overhead" }, Groups = { "UpperHead" } };
            C(ApparelPickMath.Fits(shirt, parka) && ApparelPickMath.Fits(parka, hat), "a shirt under a parka, a hat over both");
            C(!ApparelPickMath.Fits(parka, duster), "two shells over the torso don't fit together");
        }

        // ---------- Bautista at -25 °C in a ratty shirt ----------
        {
            var k = new Wardrobe();
            k.C("bautista", 0.6f, 30f);
            k.P("ratty", "everyday clothing", "OnSkin", Torso, 5f, 4f, wornBy: "bautista", stage: 1);
            k.O("bautista", "ratty", 3.2f);
            k.P("parka", "cold-weather gear", "Shell", Torso, 7.1f, 40f);
            k.O("bautista", "parka", 8f);
            k.P("duster", "hot-weather gear", "Shell", Torso, 6.5f, 15f);
            k.O("bautista", "duster", 4f);
            k.P("shirt", "everyday clothing", "OnSkin", Torso, 5.5f, 4f);
            k.O("bautista", "shirt", 5.5f, "ratty");
            var s = k.Assign();
            var warm = s.FirstOrDefault(x => x.Gap == WearGap.Warmth);
            C(warm?.Piece == "parka" && warm.Kind == WearKind.Wear && Near(warm.Degrees, 40f), "warmth first: the parka, the warmest, for the cold ahead");
            C(warm?.Why.FirstOrDefault()?.Kind == WearWhy.Warmth && warm.Why[0].Level == 30, "its line says how short he is first");
            var rag = s.FirstOrDefault(x => x.Gap == WearGap.Worn);
            C(rag?.Piece == "shirt" && rag.Instead.SequenceEqual(new[] { "ratty" }), "the ratty shirt replaced by a shirt, worn under the parka");
            C(rag != null && rag.Why.Any(r => r.Kind == WearWhy.Worn && r.Level == 1), "its line says it is in tatters");
            C(!s.Any(x => x.Piece == "duster"), "the duster isn't worn over the parka");
            C(s.Count(x => x.Colonist == "bautista") == 2, "two suggestions that fit together");
        }

        // ---------- The outfit: never past it, said with a way to allow it ----------
        {
            var k = new Wardrobe();
            k.C("bautista", 0.6f, 30f);
            k.P("parka", "cold-weather gear", "Shell", Torso, 7.1f, 40f);
            k.O("bautista", "parka", 8f).Outfit = "Worker";
            k.P("duster", "hot-weather gear", "Shell", Torso, 6.5f, 15f);
            k.O("bautista", "duster", 4f);
            var s = k.Assign();
            C(!s.Any(x => x.Piece == "parka" && x.Kind != WearKind.Outfit), "a parka the outfit doesn't allow is never suggested to wear");
            var outfit = s.FirstOrDefault(x => x.Kind == WearKind.Outfit);
            C(outfit?.Piece == "parka" && outfit.Outfit == "Worker" && outfit.Gap == WearGap.Warmth && outfit.OutfitFixable, "it is said instead: your outfit Worker doesn't allow it, with a way to allow it");
            C(s.Any(x => x.Piece == "duster" && x.Kind == WearKind.Wear && x.Gap == WearGap.Warmth), "the duster it allows warms him meanwhile");
            C(!ApparelAskMath.Askable(new WearSuggestion { Kind = WearKind.Trade, Gain = 3f }) && ApparelAskMath.Askable(outfit), "he may ask for it; never for a trade");
            var k2 = new Wardrobe();
            k2.C("ana");
            k2.P("hat", "headgear", "Overhead", Head, 6f, wornBy: "ana");
            k2.O("ana", "hat", 5f);
            k2.P("fancy", "headgear", "Overhead", Head, 8f);
            k2.O("ana", "fancy", 6f, "hat").Outfit = "Nudist";
            C(!k2.Assign().Any(), "a small upgrade the outfit doesn't allow isn't worth a word");
            k2.I.Options[("ana", "fancy")].Rating = 7f;
            C(k2.Assign().Any(x => x.Kind == WearKind.Outfit), "a big one is (twice the threshold)");
        }

        // ---------- Title, role and faith ----------
        {
            var k = new Wardrobe();
            k.I.Raids = 3f;
            k.C("ana");
            var hat = k.P("rolehat", "headgear", "Overhead", Head, 4f, wornBy: "ana");
            hat.Rule = "role";
            k.O("ana", "rolehat", 4f).Meets.Add("role");
            k.P("helmet", "helmet", "Overhead", Head, 7f);
            k.O("ana", "helmet", 7f, "rolehat");
            C(!k.Assign().Any(x => x.Piece == "helmet"), "never moved off a piece her role asks for, for a helmet that doesn't meet it");
            k.I.Options[("ana", "helmet")].Meets.Add("role");
            C(k.Assign().Any(x => x.Piece == "helmet"), "one that meets the role too may take its place");
            var k2 = new Wardrobe();
            k2.C("bo");
            k2.P("robe", "ceremonial or psychic", "Shell", Torso, 5f);
            var o = k2.O("bo", "robe", 4.5f);
            C(!k2.Assign().Any(), "a robe of 4.5 for an empty slot is no gap and no add-on");
            o.Unmet = "title";
            o.Meets.Add("title");
            var s = k2.Assign();
            C(s.Count == 1 && s[0].Why.Any(r => r.Kind == WearWhy.Title), "his title asks for it: worth it, and said");
            C(Near(ApparelPickMath.Score(k2.I, k2.I.Colonist("bo"), k2.I.Piece("robe")).Value, 4.5f + ApparelPickMath.RuleBonus), "a title's or role's apparel they lack counts 1.5");
            o.Unmet = "faith";
            C(Near(ApparelPickMath.Score(k2.I, k2.I.Colonist("bo"), k2.I.Piece("robe")).Value, 4.5f + ApparelPickMath.FaithBonus), "their faith's desired apparel 0.6");
        }

        // ---------- Locked, lucky, heirlooms ----------
        {
            var k = new Wardrobe();
            k.C("ana");
            var vest = k.P("vest", "body armour", "Middle", Torso, 3.9f, wornBy: "ana");
            vest.Locked = true;
            k.O("ana", "vest", 3.9f);
            k.P("recon", "body armour", "Middle", Torso, 6.8f);
            k.O("ana", "recon", 6.8f, "vest");
            C(!k.Assign().Any(), "never in place of a piece the player locked");
            vest.Locked = false;
            k.I.Colonist("ana").Keepsakes.Add(new WearKeepsake { Kind = KeepsakeKind.Lucky, Piece = "vest", Def = "vest", Label = "flak vest", MoodWith = 3f, MoodWithout = -4f });
            var s = k.Assign();
            C(s.Count == 1 && s[0].Piece == "recon", "a lucky vest is no block");
            C(s[0].Costs.Any(r => r.Kind == WearWhy.Lucky && r.Text == "ana" && r.Value == 3f && r.Level == -4), "it is a warning, with the moods");
            C(ApparelPickMath.GivesUpKeepsake(s[0], "ana"), "and she never asks for it herself");
            k.I.Colonist("ana").Keepsakes[0].Kind = KeepsakeKind.Heirloom;
            C(k.Assign().Single().Costs.Any(r => r.Kind == WearWhy.Heirloom), "an heirloom the same");
            k.I.Colonist("ana").Keepsakes[0].Kind = KeepsakeKind.Lucky;
            k.I.Piece("recon").Def = "vest";
            C(!k.Assign().Single().Costs.Any(), "another of a lucky item's kind keeps its mood: no warning");
        }

        // ---------- Armour and helmets for the fighters of a raid-heavy colony ----------
        {
            var k = new Wardrobe();
            k.C("ana", 1f);
            k.C("doc", 0.3f);
            k.P("vest", "body armour", "Middle", Torso, 3.9f);
            k.O("ana", "vest", 3.9f);
            k.O("doc", "vest", 3.9f);
            k.P("helmet", "helmet", "Overhead", Head, 3.5f);
            k.O("ana", "helmet", 3.5f);
            k.O("doc", "helmet", 3.5f);
            C(!k.Assign().Any(), "no raids lately: a vest of 3.9 and a helmet of 3.5 are no gap and no add-on");
            k.I.Raids = 3f;
            var s = k.Assign();
            C(s.Any(x => x.Colonist == "ana" && x.Piece == "vest" && x.Gap == WearGap.Armour) && s.Any(x => x.Colonist == "ana" && x.Piece == "helmet" && x.Gap == WearGap.Helmet),
                "raids come often: the fighter without armour gets the vest and the helmet");
            C(!s.Any(x => x.Colonist == "doc"), "the doctor who rarely fights has no gap");
            C(s.First(x => x.Piece == "vest").Why[0].Kind == WearWhy.Armour, "said first: she fights and wears no body armour");
            k.C("bo", 0.8f);
            k.O("bo", "vest", 3.9f);
            k.O("bo", "helmet", 3.5f);
            s = k.Assign();
            C(s.Single(x => x.Piece == "vest").Colonist == "ana" && s.Single(x => x.Piece == "helmet").Colonist == "ana", "short of armour: the one who fights most first");
            C(!ApparelPickMath.Fighter(k.I, new WearColonist { Fights = 1f, CanFight = false }), "someone who can't fight never has an armour gap");
        }

        // ---------- The coldest first ----------
        {
            var k = new Wardrobe();
            k.C("a", 1f, 10f);
            k.C("b", 1f, 30f);
            k.P("parka", "cold-weather gear", "Shell", Torso, 7.1f, 40f);
            k.O("a", "parka", 8f);
            k.O("b", "parka", 7f);
            C(k.Assign().Single().Colonist == "b", "one parka, two cold: the coldest gets it, whoever rates it higher");
            k.P("tuque", "headgear", "Overhead", Head, 5f, 8f);
            k.O("a", "tuque", 5f);
            k.O("b", "tuque", 5f);
            var s = k.Assign();
            C(s.Any(x => x.Colonist == "a" && x.Piece == "tuque" && x.Gap == WearGap.Warmth), "the other gets the tuque: something warm before nothing");
            var k2 = new Wardrobe();
            k2.C("a", 1f, 30f);
            k2.P("jacket", "everyday clothing", "Middle", Torso, 5f, 20f);
            k2.O("a", "jacket", 5f);
            k2.P("tuque", "headgear", "Overhead", Head, 5f, 8f);
            k2.O("a", "tuque", 5f);
            C(k2.Assign().Count(x => x.Gap == WearGap.Warmth) == 2, "a jacket isn't enough: a hat too, filling empty layers and slots");
        }

        // ---------- The threshold, and worn-out pieces ----------
        {
            Wardrobe Up(float now, float better, int stage = -1, bool dead = false)
            {
                var k = new Wardrobe();
                k.C("ana");
                k.P("old", "everyday clothing", "OnSkin", Torso, 5f, wornBy: "ana", stage: stage, dead: dead);
                k.O("ana", "old", now);
                k.P("new", "everyday clothing", "OnSkin", Torso, 5f);
                k.O("ana", "new", better, "old");
                return k;
            }
            C(!Up(5f, 5.7f).Assign().Any(), "+0.7 isn't worth a change");
            var s = Up(5f, 5.9f).Assign();
            C(s.Count == 1 && Near(s[0].Gain, 0.9f) && s[0].Gap == null, "+0.9 is suggested");
            C(s[0].Why[0].Kind == WearWhy.Better && Near(s[0].Why[0].Value, 5.9f) && s[0].Why[0].Level == 50, "simply better: 5.9 against 5.0");
            C(Up(5f, 5.1f, 0).Assign().Single().Gap == WearGap.Worn, "worn below half: any piece no worse is worth it");
            C(Up(5f, 5.1f, -1, true).Assign().Single().Why.Any(r => r.Kind == WearWhy.Dead), "taken from the dead: the same, and said");
            C(!Up(5f, 4.9f, 0).Assign().Any(), "but never a worse one");
            var k = Up(5f, 5.1f, 0);
            k.I.Piece("new").Stage = 0;
            C(!k.Assign().Any(), "nor one as worn");
        }

        // ---------- A piece in place of several, judged in each one's role ----------
        {
            var k = new Wardrobe();
            k.C("ana");
            k.P("vest", "body armour", "Middle", Torso, 3.9f, 1f, wornBy: "ana");
            k.O("ana", "vest", 3.9f);
            k.P("parka", "cold-weather gear", "Shell", Torso, 7.1f, 40f, wornBy: "ana");
            k.O("ana", "parka", 7.5f);
            var marine = k.P("marine", "body armour", "Middle", Torso, 7.1f, 30f);
            marine.Layers.Add("Shell");
            var o = k.O("ana", "marine", 7.1f, "vest", "parka");
            o.As["cold-weather gear"] = 5.5f;
            float? g = ApparelPickMath.Gain(k.I, k.I.Colonist("ana"), marine, out float before, out float deg, out _);
            C(g.HasValue && Near(g.Value, (7.1f - 3.9f) + (5.5f - 7.5f)), $"marine armour in place of a vest and a parka: +3.2 as armour, -2.0 as a coat ({g})");
            C(Near(before, 7.5f) && Near(deg, 30f - 41f), "against the best of what it replaces; the warmth it adds net of theirs");
            C(k.Assign().Single().Piece == "marine", "+1.2 is worth it");
            o.As["cold-weather gear"] = 4f;
            C(!k.Assign().Any(), "as a poorer coat it isn't");
        }

        // ---------- Never leaving them short of warmth ----------
        {
            var k = new Wardrobe();
            var ana = k.C("ana");
            ana.ColdSpare = 5f;
            k.P("parka", "cold-weather gear", "Shell", Torso, 7.1f, 40f, wornBy: "ana");
            k.O("ana", "parka", 6f);
            k.P("armour", "body armour", "Shell", Torso, 8f, 10f);
            k.O("ana", "armour", 9f, "parka");
            C(!k.Assign().Any(), "armour that takes away 30 °C of the 5 she has to spare isn't suggested");
            ana.ColdSpare = 40f;
            C(k.Assign().Any(x => x.Piece == "armour"), "with 40 to spare it is");
            C(ApparelPickMath.KeepsComfort(new WearColonist { ColdSpare = -10f }, 5f, 0f) && !ApparelPickMath.KeepsComfort(new WearColonist { ColdSpare = -10f }, -1f, 0f),
                "someone already short may gain warmth, never lose any");
        }

        // ---------- Trades of armour ----------
        {
            var k = new Wardrobe();
            k.C("ana", 1f);
            k.C("doc", 0.2f, work: "doctor");
            k.P("vest", "body armour", "Middle", Torso, 3.9f, wornBy: "ana");
            k.P("recon", "body armour", "Middle", Torso, 6.8f, wornBy: "doc");
            k.O("ana", "vest", 3.9f);
            k.O("doc", "recon", 6.8f);
            k.O("ana", "recon", 7f, "vest");
            k.O("doc", "vest", 3.8f, "recon");
            var s = k.Assign();
            var t = s.SingleOrDefault(x => x.Kind == WearKind.Trade);
            C(t != null && t.Colonist == "ana" && t.Other == "doc" && t.Piece == "recon" && t.Instead.Single() == "vest", "the fighter in a vest and the doctor in recon armour trade");
            C(t != null && Near(t.Gain, 3.1f) && Near(t.OtherGain, -3f) && Near(t.Worth, 3.1f - 3f * 0.2f), "worth 2.5 to the colony by how much each fights");
            C(t != null && t.Why.Any(r => r.Kind == WearWhy.TradeColony) && t.Why.Any(r => r.Kind == WearWhy.RarelyFights && r.Text == "doc"), "said: the colony gains, and the doctor rarely fights");
            k.I.Colonist("doc").Fights = 1f;
            C(!k.Assign().Any(x => x.Kind == WearKind.Trade), "two who fight alike: shuffling the armour is worth nothing");
            C(ApparelPickMath.TradeWorth(0.4f, 0.5f, 1f, 1f, out _) && !ApparelPickMath.TradeWorth(0.4f, 0.3f, 1f, 1f, out _), "better for both: 0.3 each and 0.8 together, as the weapons");
        }

        // ---------- Pack and spares ----------
        {
            var k = new Wardrobe();
            k.C("ana", 1f, 20f);
            k.C("bo");
            k.P("parka", "cold-weather gear", "Shell", Torso, 7.1f, 40f, carriedBy: "bo");
            k.O("ana", "parka", 8f);
            k.O("bo", "parka", 7f);
            var s = k.Assign().Single();
            C(s.Kind == WearKind.Spare && s.Other == "bo" && s.Colonist == "ana", "the parka Bo carries unworn goes to Ana, who is cold");
            k.I.Colonists.Remove(k.I.Colonist("ana"));
            k.I.Options.Remove(("ana", "parka"));
            k.I.Colonist("bo").ColdShort = 20f;
            k.I.Colonist("bo").ColdSpare = -20f;
            C(k.Assign().Single().Kind == WearKind.Pack, "or Bo wears it from his own pack");
        }

        // ---------- Random colonies: one piece to one colonist, pieces that fit together, the rules kept ----------
        {
            var rng = new Random(7);
            string[] roles = { "body armour", "helmet", "everyday clothing", "headgear", "cold-weather gear", "hot-weather gear", "utility" };
            (string layer, string[] groups)[] slots = { ("OnSkin", Torso), ("Middle", Torso), ("Shell", Torso), ("Overhead", Head), ("Belt", new[] { "Waist" }) };
            int bad = 0, made = 0, cases = 300;
            for (int i = 0; i < cases; i++)
            {
                var k = new Wardrobe();
                k.I.Raids = rng.Next(4);
                int people = rng.Next(1, 6);
                for (int c = 0; c < people; c++) k.C("c" + c, (float)rng.NextDouble(), rng.Next(3) == 0 ? rng.Next(1, 30) : 0f);
                int pieces = rng.Next(1, 12);
                for (int p = 0; p < pieces; p++)
                {
                    var (layer, groups) = slots[rng.Next(slots.Length)];
                    string wornBy = rng.Next(3) == 0 ? "c" + rng.Next(people) : null;
                    if (wornBy != null && k.I.Colonist(wornBy).Wears.Select(k.I.Piece).Any(w => !ApparelPickMath.Fits(w, new WearPiece { Layers = { layer }, Groups = groups.ToList() }))) wornBy = null;
                    var x = k.P("p" + p, roles[rng.Next(roles.Length)], layer, groups, 1f + 9f * (float)rng.NextDouble(), rng.Next(40), wornBy, stage: rng.Next(4) == 0 ? 0 : -1);
                    x.Locked = wornBy != null && rng.Next(6) == 0;
                    if (wornBy != null && rng.Next(6) == 0) x.Rule = "role";
                }
                foreach (var c in k.I.Colonists)
                    foreach (var x in k.I.Pieces)
                    {
                        if (x.WornBy != null && x.WornBy != c.Key && !x.Armour) continue;
                        var replaced = c.Wears.Where(w => w != x.Key && !ApparelPickMath.Fits(k.I.Piece(w), x)).ToArray();
                        var o = k.O(c.Key, x.Key, 1f + 9f * (float)rng.NextDouble(), x.WornBy == c.Key ? new string[0] : replaced);
                        if (rng.Next(8) == 0) o.Cant = "no";
                        if (rng.Next(8) == 0) o.Outfit = "Worker";
                        if (rng.Next(10) == 0) o.Meets.Add("role");
                    }
                var s = k.Assign();
                made += s.Count;
                var given = s.Where(x => x.Kind != WearKind.Outfit && x.Kind != WearKind.Trade).Select(x => x.Piece).ToList();
                if (given.Count != given.Distinct().Count()) bad++;
                foreach (var x in s)
                {
                    var o = k.I.Option(k.I.Colonist(x.Colonist), k.I.Piece(x.Piece));
                    if (o == null || o.Cant != null) bad++;
                    if (x.Kind != WearKind.Outfit && o?.Outfit != null) bad++;
                    if (x.Kind == WearKind.Outfit && o?.Outfit == null) bad++;
                    foreach (var y in x.Instead.Select(k.I.Piece))
                        if (y == null || y.Locked || (y.Rule != null && !o.Meets.Contains(y.Rule))) bad++;
                }
                foreach (var c in k.I.Colonists)
                {
                    var mine = s.Where(x => x.Colonist == c.Key && x.Kind != WearKind.Outfit && x.Kind != WearKind.Trade).ToList();
                    for (int a = 0; a < mine.Count; a++)
                        for (int b = a + 1; b < mine.Count; b++)
                            if (!ApparelPickMath.Fits(k.I.Piece(mine[a].Piece), k.I.Piece(mine[b].Piece)) || mine[a].Instead.Any(mine[b].Instead.Contains)) bad++;
                    if (s.Count(x => x.Colonist == c.Key && x.Kind == WearKind.Outfit) > 1) bad++;
                }
            }
            C(bad == 0, $"random colonies: no piece to two, nothing they can't wear or their outfit doesn't allow, nothing locked or kept for a rule taken off, every colonist's pieces fitting together ({bad})");
            C(made > cases, $"random colonies: suggestions are made ({made})");
        }

        // ---------- The asks ----------
        {
            int day = ApparelAskMath.DayTicks, now = 100 * day;
            C(ApparelAskMath.MayAsk(now, -1, -1, -1, 5, 0), "never asked: may ask");
            C(!ApparelAskMath.MayAsk(now, now - 5 * day, -1, -1, 5, 0) && ApparelAskMath.MayAsk(now, now - 6 * day, -1, 3, 5, 0), "once in six days each");
            C(!ApparelAskMath.MayAsk(now, -1, now - 2000, -1, 5, 0), "once a day in the colony");
            C(!ApparelAskMath.MayAsk(now, now - 10 * day, -1, 5, 5, 0), "the same piece not again for fifteen days");
            C(!ApparelAskMath.MayAsk(now, -1, -1, -1, 5, now + 1), "never while set aside");
            C(ApparelAskMath.Askable(new WearSuggestion { Gap = WearGap.Warmth, Gain = 0.2f }) && ApparelAskMath.Askable(new WearSuggestion { Gain = 1.7f })
              && !ApparelAskMath.Askable(new WearSuggestion { Gain = 1.2f }), "a gap, or clearly better (1.6)");
            var warm = new WearSuggestion { Gap = WearGap.Warmth };
            C(Near(ApparelAskMath.ChanceFor(warm, 5f), 2f * ApparelAskMath.Chance) && Near(ApparelAskMath.ChanceFor(warm, 0f), ApparelAskMath.Chance)
              && Near(ApparelAskMath.ChanceFor(new WearSuggestion(), 5f), ApparelAskMath.Chance), "warmth twice as likely while it's freezing; the rest rarely");
            C(ApparelAskMath.Chance <= 0.12f, "rarely: no more often than the weapons' asks");
            C(ApparelPickMath.AskTag(warm) == "warmth" && ApparelPickMath.AskTag(new WearSuggestion { Gap = WearGap.Helmet }) == "helmet"
              && ApparelPickMath.AskTag(new WearSuggestion { Why = { new WearReason { Kind = WearWhy.Faith } } }) == "faith", "the line's tag by the strongest reason");
        }

        // ---------- Who should wear this ----------
        {
            var k = new Wardrobe();
            k.C("warm", 1f);
            k.C("cold", 1f, 20f);
            var parka = k.P("parka", "cold-weather gear", "Shell", Torso, 7.1f, 40f);
            k.O("warm", "parka", 9f);
            k.O("cold", "parka", 6f).Outfit = "Worker";
            var who = ApparelPickMath.WhoShouldWear(k.I, parka);
            C(who.Count == 2 && who[0].c.Key == "cold" && who[0].gap == WearGap.Warmth, "the cold one first, whom it fills a need for (outfit aside: the card says whom it suits)");
            C(who[1].c.Key == "warm" && Near(who[1].gain, 8f), "then the biggest gain");
        }

        // ---------- The real base game's pieces judged in another role ----------
        {
            var defs = ApparelLiveTests.Vanilla();
            ApparelReport Rep(string d) => ApparelRead.Full(defs.First(x => x.DefName == d));
            var marine = Rep("Apparel_PowerArmor");
            var parka = Rep("Apparel_Parka");
            var duster = Rep("Apparel_Duster");
            var winter = new ApparelWearer { Kind = "shooter", HasClimate = true, OutdoorNow = -25f, SeasonLow = -32f, SeasonHigh = -10f, ComfyMinWithout = 16f, ComfyMaxWithout = 26f };
            C(Near(ApparelForMath.For(marine, winter).Rating, ApparelForMath.For(marine, winter, marine.Rating.Role).Rating), "judged as its own role, the same rating");
            float asCoat = ApparelForMath.For(marine, winter, "cold-weather gear").Rating, asDuster = ApparelForMath.For(marine, winter, "hot-weather gear").Rating;
            C(asCoat > 1f && asCoat != ApparelForMath.For(marine, winter).Rating, $"marine armour judged as a coat in winter: {asCoat}");
            C(ApparelForMath.For(parka, winter).Rating > ApparelForMath.For(duster, winter, "cold-weather gear").Rating, "in winter a parka is a better coat than a duster");
            var summer = new ApparelWearer { Kind = "shooter", HasClimate = true, OutdoorNow = 38f, SeasonLow = 20f, SeasonHigh = 42f, ComfyMinWithout = 16f, ComfyMaxWithout = 26f };
            C(ApparelForMath.For(duster, summer).Rating > ApparelForMath.For(parka, summer, "hot-weather gear").Rating, "in summer a duster beats a parka at its own job");
            C(asDuster > 0f, $"and as a duster in winter: {asDuster}");
        }

        // ---------- The lines, in every age ----------
        {
            var pools = new Dictionary<string, List<string>>();
            foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "*.xml"))
                foreach (var el in XDocument.Load(file).Root!.Elements("ColonyCouncil.FlavorPoolDef"))
                    pools[(string)el.Element("defName")] = el.Element("lines")?.Descendants("li").Select(li => li.Value).ToList() ?? new List<string>();
            var ask = pools.TryGetValue(ApparelAsksPool, out var a) ? a : new List<string>();
            C(ask.Count >= 40, $"colonists asking for clothes have a pool of their own ({ask.Count})");
            bool Fits(HashSet<string> ctx, HashSet<string> eras, string must) =>
                ask.Any(l => LineTags.Of(l) is var p && p != null && LineTags.Match(p, ctx) > 0 && LineTags.EraFits(p, eras) && p.tags.Contains(must));
            foreach (var era in new[] { "tribal", "medieval", "industrial", "spacer" })
            {
                var eras = new HashSet<string> { era };
                if (era == "tribal" || era == "medieval") eras.Add("lowtech");
                foreach (var why in new[] { "warmth", "cooling", "armour", "helmet", "worn", "dead", "faith", "title", "role", "work", "mechanoids", "crowds", "better" })
                    C(Fits(new HashSet<string>(eras) { why }, eras, why), $"[{why}] has a line in the {era} age");
            }
            C(ask.All(l => l.Contains("{APPAREL}") && l.Contains("{PLACE}")), "every line names the piece and where it lies, so its words are true");
            C(ask.Where(l => l.Contains("freezing") || l.Contains("shivering")).All(l => LineTags.Of(l).tags.Contains("cold")), "freezing only when it is cold now");
            C(Read("Tools/Tests/ImmersionTests.cs").Contains("\"Rimfolk_Pool_ApparelAsk\""), "its lines are held to one sentence of 90 characters (ImmersionTests)");
        }

        // ---------- The seams ----------
        string pick = Read("Source/ColonyCouncil/Apparel/ApparelPick.cs");
        string math = Read("Source/ColonyCouncil/Apparel/ApparelPickMath.cs");
        string ui = Read("Source/ColonyCouncil/Apparel/ApparelPickUi.cs");
        string nice = Read("Source/ColonyCouncil/Apparel/ApparelNiceTab.cs");
        string asks = Read("Source/ColonyCouncil/Apparel/ApparelAsks.cs");
        string book = Read("Source/ColonyCouncil/Apparel/ApparelPickBook.cs");
        string swap = Read("Source/ColonyCouncil/Apparel/ApparelSwapJob.cs");
        string card = Read("Source/ColonyCouncil/Apparel/ApparelCardWindow.cs");
        string keys = Read("Languages/English/Keyed/Rimfolk_Apparel.xml");
        string weaponKeys = Read("Languages/English/Keyed/Rimfolk_Weapons.xml");
        string settings = Read("Source/ColonyCouncil/CouncilSettings.cs");
        string catalog = Read("Source/ColonyCouncil/UI/SettingsCatalog.cs");
        foreach (var (field, key, label) in new[] { ("apparelSuggestions", "ApparelSuggestions", "Apparel suggestions"), ("apparelAsks", "ApparelAsks", "Colonists ask for better clothes") })
        {
            C(settings.Contains($"public bool {field} = true;") && settings.Contains($"Scribe_Values.Look(ref {field}, \"{field}\", true);"), $"{field}: on at first, saved");
            C(catalog.Contains($"Toggle(\"{field}\", \"CC_Settings_{key}\", \"CC_SetNote_{key}\", \"apparelRatings,deepHooks\");"), $"{field}: under How you're told, Apparel");
            C(keys.Contains($"<CC_Settings_{key}>{label}</CC_Settings_{key}>") && keys.Contains($"<CC_Settings_{key}_Tip>") && keys.Contains($"<CC_SetNote_{key}>"), $"{field}: its words");
        }
        C(catalog.IndexOf("Toggle(\"apparelSuggestions\"", StringComparison.Ordinal) > catalog.IndexOf("Section(\"CC_SetSec_Apparel\")", StringComparison.Ordinal), "the settings sit in the Apparel section");
        // The clock: a slot of its own, timed, a tenth of the colony each turn.
        C(Sched.RareOffsets.Contains(Sched.RareApparelPicks) && Sched.RareApparelPicks != Sched.RareWeaponPicks, "a 250-tick slot of its own");
        C(PassTable.All.Any(p => p.pass == Pass.RareApparelPicks && p.offset == Sched.RareApparelPicks), "timed on Rimfolk's clock");
        C(book.Contains("Sched.Every(now, Sched.RareTicks, Sched.RareApparelPicks)") && book.Contains("PassClock.Time(Pass.RareApparelPicks)") && book.Contains("Sched.SliceTurn("),
            "its pass, a tenth of the colonists each turn");
        C(Read("Tools/save_estimate.py").Contains("Apparel/ApparelPickBook.cs:notNow") && Read("Tools/save_estimate.py").Contains("Apparel/ApparelPickBook.cs:asks"),
            "what it keeps with the game is modelled in the save estimate");
        C(pick.Contains("GenDate.TicksPerHour") && pick.Contains("Signature(") && pick.Contains("ApparelLive.ForColonist(p, r, t)"), "the choice kept an hour, made again on a change; ratings kept an hour");
        C(pick.Contains("WeaponPickBook.Instance?.Seen()") && Read("Source/ColonyCouncil/Weapons/WeaponPickBook.cs").Contains("public List<RaidSeen> Seen()"), "the raids are the weapons' record");
        C(pick.Contains("GenTemperature.AverageTemperatureAtTileForTwelfth(tile, tw)") && pick.Contains("tw = tw.NextTwelfth();"), "the season ahead: this twelfth and the next two");
        // Respecting the outfit, the rules, the game's own wear.
        C(pick.Contains("policy.filter.Allows(t)") && pick.Contains("policy.filter.SetAllow(t.def, true)") && pick.Contains("Dialog_ManageApparelPolicies"),
            "the outfit read by the game's own filter; Allow allows the kind there, or opens the outfit");
        C(pick.Contains("p.apparel.AllRequirements") && pick.Contains("ApparelUtility.IsRequirementActive") && pick.Contains("Precept_Apparel") && pick.Contains("requiredMinimumApparelQuality"),
            "title and role requirements, the faith's desired apparel and the title's quality, by the game's own");
        C(pick.Contains("p.apparel.WouldReplaceLockedApparel(ap)") && pick.Contains("x.Locked = p.apparel.IsLocked(ap)"), "locked pieces never taken off");
        C(pick.Contains("JobMaker.MakeJob(JobDefOf.Wear, t)") && pick.Contains("TryTakeOrderedJob(job, JobTag.Misc, queue)"), "wearing is the game's own Wear job, an order walked out on the map");
        C(math.Contains("Kind != WearKind.Outfit") || pick.Contains("x.Kind != WearKind.Outfit"), "Wear all suggested never orders what an outfit doesn't allow");
        var jobs = Read("Defs/Misc/Rimfolk_ApparelPicks.xml");
        C(jobs.Contains("<driverClass>ColonyCouncil.JobDriver_TakeSpareApparel</driverClass>") && jobs.Contains("<driverClass>ColonyCouncil.JobDriver_SwapApparel</driverClass>")
          && swap.Contains("Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)") && swap.Contains("p.apparel.Wear(ap, true)"), "a spare handed over and a trade, walked out, what it replaces dropped as the game's Wear drops it");
        // Where it shows.
        C(ui.Contains("[HarmonyPatch(typeof(ITab_Pawn_Gear), \"TryDrawComfyTemperatureRange\")]") && ui.Contains("[HarmonyPriority(Priority.Low)]"), "the game's gear tab, under the suggested weapons");
        foreach (var soft in new[] { "Patch_ApparelNiceSuggestedPlace", "Patch_ApparelNiceSuggestedDraw" })
        {
            int at = nice.IndexOf("class " + soft, StringComparison.Ordinal);
            string body = at >= 0 ? nice.Substring(at, Math.Min(900, nice.Length - at)) : "";
            C(body.Contains("Prepare()") && body.Contains("TargetMethod()") && body.Contains("StandsDownWhy()"), $"{soft}: skipped cleanly without Nice Inventory Tab");
        }
        C(nice.Contains("\"apparel_legacy_groupbox\"") && nice.Contains("\"equipment_alt_groupbox\"") && nice.Contains("WeaponNiceTab.IsSpacer(w)"),
            "Nice Inventory Tab: its apparel box, or in its grid layout its equipment box under the weapons' row");
        C(Read("Source/ColonyCouncil/UI/DefensePanel.cs").Contains("ApparelPickPanel.Draw(s.map, x, y, w, folds)"), "the Defenses page, next to the weapons");
        C(ui.Contains("ApparelPick.DoAll(modelPicks)") && keys.Contains("<CC_ApparelPick_AllAct>Wear all suggested ({COUNT})</CC_ApparelPick_AllAct>"), "Wear all suggested, which only gives the orders");
        C(keys.Contains("<CC_ApparelPick_Section>Clothes and armour for each colonist</CC_ApparelPick_Section>"), "its heading");
        C(card.Contains("ApparelPick.WhoShouldWear(thing)") && keys.Contains("<CC_ApparelPick_Card_Who>Who should wear this</CC_ApparelPick_Card_Who>"), "the card: who should wear this");
        C(Read("Source/ColonyCouncil/UI/InboxProviders.cs").Contains("Inbox.Register(\"apparel-asks\", InboxSection.People, 41f, ApparelAsks.InboxRows);") && asks.Contains("waits = false"),
            "the inbox's quiet row in How people are, counted nowhere");
        C(asks.Contains("WeaponAsks.FreeAndSafe(p)") && asks.Contains("Speech.Say(p, line)") && asks.Contains("ApparelPickMath.GivesUpKeepsake(x, me)"),
            "asked in their free time and safe, through Speech, never giving up their own keepsake");
        C(keys.Contains("No warmer clothes in storage for {PAWN}"), "the quiet line: no warmer clothes in storage");
        // Every key the code names exists.
        var src = pick + math + ui + nice + asks + book + swap + card;
        foreach (Match m in Regex.Matches(src, "\"(CC_(?:ApparelPick|ApparelAsk)_[A-Za-z0-9_]+)\""))
        {
            string key = m.Groups[1].Value;
            if (key.EndsWith("_", StringComparison.Ordinal)) continue;
            C(keys.Contains("<" + key + ">"), $"the key {key} exists");
        }
        foreach (Match m in Regex.Matches(src, "\"(CC_(?:WeaponPick|WeaponAsk)_[A-Za-z0-9_]+)\""))
            C(weaponKeys.Contains("<" + m.Groups[1].Value + ">"), $"the shared key {m.Groups[1].Value} exists");
        // Keys made from a word.
        foreach (var gap in Enum.GetNames(typeof(WearGap)))
        {
            C(keys.Contains("<CC_ApparelPick_Quiet_" + gap + ">"), $"the quiet line for {gap}");
            C(keys.Contains("<CC_ApparelPick_Card_Need_" + gap + ">"), $"the card's need {gap}");
        }
        foreach (var g in new[] { "Warmth", "Cooling", "Armour", "Helmet", "Worn" }) C(keys.Contains("<CC_ApparelPick_Head_" + g + ">") && keys.Contains("<CC_ApparelPick_Row_" + g + ">"), $"the head and row for {g}");
        foreach (var why in new[] { "Armour", "Helmet", "Mechs", "Melee", "Faith", "Title", "Role", "Pack" }) C(keys.Contains("<CC_ApparelPick_Why_" + why + ">"), $"the reason {why} in words");
        foreach (var kind in new[] { "Wear", "Pack", "Spare", "Trade" }) C(keys.Contains("<CC_ApparelPick_Act_" + kind + "_Tip>"), $"the button's tip for {kind}");
        C(!keys.Contains("—") && !Read("Defs/FlavorPoolDefs/Rimfolk_Pool_ApparelAsk.xml").Contains("—"), "no em dashes");
        var almanac = Read("Defs/AlmanacArticleDefs/Almanac_Apparel.xml");
        C(almanac.Contains("<heading>Better clothes for each colonist</heading>"), "the Almanac's page tells it");
        return n;
    }

    const string ApparelAsksPool = "Rimfolk_Pool_ApparelAsk";
}
