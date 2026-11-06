using System.Globalization;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The ailments and illnesses of the land, the work and the age (6 November 2026; Ailments/AilmentCauseMath.cs): each
/// cause holds to what its description says (still water, small wild game, a bad carcass, a small smithy, cold wet
/// feet, dusk in an arid land, the dry soil, an old limb, fresh mechanites, a wound in the wet heat), the new serious
/// ones lean with the storyteller as the catching clock does, each is named in every age, and from the defs: each goes
/// through the harm service's cap (children only the mildest, babies never: HarmMath.Bar), at the existing ailments'
/// rates, with its words (notes for both kinds of colony, a mood, lines for its journal and its voice).
/// </summary>
public static class AilmentLandTests
{
    private static readonly string[] Everyday =
        { "SwimmersItch", "TrenchFoot", "SandflyFever", "JungleRot", "HuntersFever", "FoundersAgue", "StumpSores", "MechaniteFever" };

    private static readonly string[] Serious = { "BlackBane", "ValleyFever", "BirdFever" };

    private static readonly string[] Ages = { "tribal", "medieval", "industrial", "spacer" };

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "ailments of the land and the age: " + what);
        }

        const int H = 2500, D = 60000;

        // ---------------- The causes ----------------

        C(AilmentCauseMath.StillWater(true, false, false, false) && AilmentCauseMath.StillWater(false, false, false, true), "a lake, a pond or a marsh is still water");
        C(!AilmentCauseMath.StillWater(true, true, false, false) && !AilmentCauseMath.StillWater(true, false, true, false) && !AilmentCauseMath.StillWater(false, false, false, false),
            "a river, the sea or dry land never gives the itch");
        C(AilmentCauseMath.SmallGame(true, true, 0.2f) && AilmentCauseMath.SmallGame(true, true, AilmentCauseMath.SmallGameSize), "a hare or a squirrel is small game");
        C(!AilmentCauseMath.SmallGame(true, false, 0.2f) && !AilmentCauseMath.SmallGame(true, true, 1f) && !AilmentCauseMath.SmallGame(false, true, 0.2f),
            "a tame animal, big game or a person is not");
        C(AilmentCauseMath.BadCarcass(true, true, false) && AilmentCauseMath.BadCarcass(true, false, true) && !AilmentCauseMath.BadCarcass(true, false, false)
          && !AilmentCauseMath.BadCarcass(false, true, true), "a bad carcass is an animal that rotted or died sick, nothing fresh and healthy");
        C(AilmentCauseMath.FumeRoom(true, false, 40) && AilmentCauseMath.FumeRoom(true, false, AilmentCauseMath.FumeRoomCells), "a small closed smithy holds the fumes");
        C(!AilmentCauseMath.FumeRoom(true, false, 61) && !AilmentCauseMath.FumeRoom(true, true, 20) && !AilmentCauseMath.FumeRoom(false, false, 20),
            "a big or open smithy lets them go");
        C(AilmentCauseMath.ColdWet(true, true, 9f, 0f) && !AilmentCauseMath.ColdWet(true, true, 10f, 0f), "cold wet feet below 10 °C");
        C(!AilmentCauseMath.ColdWet(false, true, 0f, 0f) && !AilmentCauseMath.ColdWet(true, false, 0f, 0f), "only outdoors on wet ground");
        C(!AilmentCauseMath.ColdWet(true, true, 0f, -15f) && AilmentCauseMath.ColdWet(true, true, 15f, 10f) && !AilmentCauseMath.ColdWet(true, true, -50f, -BodyMath.Never),
            "the line moves with their own cold shift, and an immunity puts it out of reach");
        for (int h = 0; h < 24; h++)
            C(AilmentCauseMath.DuskOrNight(h) == (h >= 18 || h < 5), $"sandflies bite from dusk to before dawn (hour {h})");
        C(AilmentCauseMath.Sandflies(true, true, 25f, 20) && !AilmentCauseMath.Sandflies(true, true, 19f, 20) && !AilmentCauseMath.Sandflies(true, true, 30f, 12)
          && !AilmentCauseMath.Sandflies(false, true, 30f, 20) && !AilmentCauseMath.Sandflies(true, false, 30f, 20), "sandflies only out of doors in an arid land, warm, after dusk");
        C(AilmentCauseMath.DesertDust(true, true, true, false) && !AilmentCauseMath.DesertDust(true, true, true, true) && !AilmentCauseMath.DesertDust(false, true, true, false)
          && !AilmentCauseMath.DesertDust(true, true, false, false), "the dry soil's dust: digging an arid land with no rain falling");
        C(AilmentCauseMath.LowTechLimb(0.6f, false, 0) && AilmentCauseMath.LowTechLimb(0.85f, false, AilmentCauseMath.IndustrialTech), "a peg leg and a simple prosthetic chafe");
        C(!AilmentCauseMath.LowTechLimb(1.25f, true, AilmentCauseMath.SpacerTech) && !AilmentCauseMath.LowTechLimb(0.9f, false, AilmentCauseMath.SpacerTech),
            "a bionic or archotech limb never does");
        C(AilmentCauseMath.MechanitePart(AilmentCauseMath.SpacerTech, false) && AilmentCauseMath.MechanitePart(0, true) && !AilmentCauseMath.MechanitePart(AilmentCauseMath.IndustrialTech, false),
            "mechanites come with spacer parts, not industrial ones");
        C(AilmentCauseMath.FreshMechanites(D, 10 * D, D) && !AilmentCauseMath.FreshMechanites(4 * D, 10 * D, D), "mechanite fever within three days of the part");
        C(!AilmentCauseMath.FreshMechanites(D, D / 2, D), "nothing for a newcomer who came with an old part");
        C(AilmentCauseMath.RotLand(true, 25f) && !AilmentCauseMath.RotLand(true, 24f) && !AilmentCauseMath.RotLand(false, 35f), "jungle rot needs the wet heat");
        int looks = 0;
        for (int age = 0; age < 12 * H; age += H / 7)
            if (AilmentCauseMath.RotWindow(age, false, H)) looks++;
        C(looks >= 7 && looks <= 8, $"a wound turning six hours old is in the window one hour ({looks} looks of a seventh of an hour)");
        int hourly = 0;
        for (int age = 137; age < 24 * H; age += H)
            if (AilmentCauseMath.RotWindow(age, false, H)) hourly++;
        C(hourly == 1, "an hourly look finds each wound in the window once");
        C(!AilmentCauseMath.RotWindow(AilmentCauseMath.RotWoundHours * H, true, H), "a tended wound never rots");

        // ---------------- The storyteller ----------------

        const float cutoff = 100f;
        C(Math.Abs(AilmentCauseMath.DiseaseLean(1f, cutoff) - 1f) < 1e-4f, "the usual storyteller leans nothing");
        C(Math.Abs(AilmentCauseMath.DiseaseLean(0.5f, cutoff) - 2f) < 1e-4f && Math.Abs(AilmentCauseMath.DiseaseLean(2f, cutoff) - 0.5f) < 1e-4f,
            "diseases twice as often, twice as likely; half as often, half");
        C(AilmentCauseMath.DiseaseLean(0.1f, cutoff) <= 2f && AilmentCauseMath.DiseaseLean(50f, cutoff) >= 0.25f, "never more than double, never less than a quarter");
        C(AilmentCauseMath.DiseaseLean(cutoff, cutoff) == 0f && AilmentCauseMath.DiseaseLean(200f, cutoff) == 0f, "none with the game's diseases off");

        // ---------------- The name by the age ----------------

        var names = new List<string> { "[tribal|medieval] pond itch", "[industrial|spacer] swimmer's itch" };
        C(AilmentCauseMath.NameFor(names, "tribal") == "pond itch" && AilmentCauseMath.NameFor(names, "medieval") == "pond itch", "a tribe and a village call it pond itch");
        C(AilmentCauseMath.NameFor(names, "industrial") == "swimmer's itch" && AilmentCauseMath.NameFor(names, "spacer") == "swimmer's itch", "a town and a spacer colony, swimmer's itch");
        C(AilmentCauseMath.NameFor(names, "archotech") == null && AilmentCauseMath.NameFor(null, "tribal") == null, "no name for the age: the label");
        C(AilmentCauseMath.NameFor(new List<string> { "[tribal] a", "plain b" }, "spacer") == "plain b", "an untagged name is the fallback");

        // ---------------- The defs ----------------

        string hediffs = Path.Combine(root, "Defs", "HediffDefs");
        var defs = new Dictionary<string, XElement>();
        foreach (var f in new[] { "Rimfolk_Hediffs_Ailments_Land.xml", "Rimfolk_Hediffs_Illnesses_Land.xml" })
            foreach (var d in XDocument.Load(Path.Combine(hediffs, f)).Root!.Elements("HediffDef").Where(d => (string)d.Attribute("Abstract") != "True"))
            {
                var comp = d.Element("comps")?.Elements("li").FirstOrDefault(li => ((string)li.Attribute("Class") ?? "").EndsWith("HediffCompProperties_Ailment"));
                if (comp != null) defs[comp.Element("key")?.Value ?? ""] = d;
            }
        C(defs.Count == Everyday.Length + Serious.Length, $"found the {Everyday.Length} everyday ailments and {Serious.Length} serious illnesses ({defs.Count})");

        // The existing counted causes' chances, for "at the existing rates".
        var old = new List<float>();
        foreach (var f in new[] { "Council_Hediffs_Ailments.xml", "Rimfolk_Hediffs_Ailments_New.xml" })
            foreach (var c in XDocument.Load(Path.Combine(hediffs, f)).Descendants("causes").Elements("li"))
                if (c.Element("hours") != null && float.TryParse(c.Element("chance")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ch))
                    old.Add(ch);
        float oldMin = old.Min(), oldMax = old.Max();

        string lines = File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_Ailments_Land.xml"));
        var linesDoc = XDocument.Parse(lines);
        IEnumerable<string> Pool(string pool) => linesDoc.Root!.Elements("ColonyCouncil.FlavorExtensionDef").Where(e => e.Element("pool")?.Value == pool)
            .SelectMany(e => e.Descendants("li")).Select(li => li.Value);

        foreach (var key in Everyday.Concat(Serious))
        {
            if (!defs.TryGetValue(key, out var d)) continue;
            var comp = d.Element("comps")!.Elements("li").First(li => ((string)li.Attribute("Class") ?? "").EndsWith("HediffCompProperties_Ailment"));
            bool serious = (string)comp.Element("serious") == "true";
            C(serious == Serious.Contains(key), $"{key} is in its tier");
            C((string)comp.Element("capped") == "true", $"{key} goes through the harm service's cap");
            var hurt = Enum.TryParse<HurtClass>((string)comp.Element("hurt") ?? "Injury", out var hc) ? hc : HurtClass.Injury;
            float childFactor = float.Parse((string)comp.Element("childFactor") ?? "1", CultureInfo.InvariantCulture);
            // The cap: a child only the mildest, a baby nothing.
            var childBar = HarmMath.Bar(1f, false, true, false, false, false, false, HarmMath.Never, 100 * D, hurt);
            var babyBar = HarmMath.Bar(1f, true, false, false, false, false, false, HarmMath.Never, 100 * D, hurt);
            C(babyBar == HurtBar.Baby, $"{key}: never a baby");
            if (hurt == HurtClass.Mild) C(childBar == HurtBar.None && !serious, $"{key}: mild, so a child may have it");
            else C(childBar == HurtBar.Child && childFactor == 0f, $"{key}: never a child (the cap, and no child factor)");
            var since = HarmMath.Bar(1f, false, false, false, false, false, false, 99 * D, 100 * D, hurt);
            C(since == HurtBar.Lately, $"{key}: not within three days of another hurt");
            // A cause on the map, at the existing rates.
            var causes = comp.Element("causes")?.Elements("li").ToList() ?? new List<XElement>();
            C(causes.Count > 0, $"{key} has a cause");
            foreach (var c in causes)
            {
                string trig = c.Element("trigger")?.Value ?? "";
                float ch = float.Parse(c.Element("chance")?.Value ?? "0.03", CultureInfo.InvariantCulture);
                // A serious one is no likelier a day than the existing ones a wound or a bite (tetanus, rabies), and none is
                // so rare it never comes (the swamp's fever is 0.006 a day of wading).
                if (serious) C(ch >= 0.001f && ch <= 0.05f, $"{key}: {ch} a day is within the serious illnesses' rates (0.001 to 0.05)");
                else if (c.Element("hours") != null) C(ch >= oldMin && ch <= oldMax, $"{key}: {ch} a day is within the everyday ailments' rates ({oldMin} to {oldMax})");
                else C(ch > 0f && ch <= 0.3f, $"{key}: {ch} a time, as rare as the ailments that follow something ({trig})");
            }
            // Its words in every age.
            var nameList = comp.Element("names")?.Elements("li").Select(li => li.Value).ToList();
            if (nameList != null)
            {
                foreach (var age in Ages)
                    C(!string.IsNullOrEmpty(AilmentCauseMath.NameFor(nameList, age)), $"{key} has a name in a {age} colony");
                C(AilmentCauseMath.NameFor(nameList, "industrial") == d.Element("label")?.Value, $"{key}'s label is its name in an industrial colony");
                C(d.Element("hediffClass")?.Value == null || d.Element("hediffClass")!.Value == "ColonyCouncil.Hediff_Ailment", $"{key} shows its name by the age");
            }
            var notes = comp.Element("notes")?.Elements("li").Select(li => li.Value).ToList() ?? new List<string>();
            C(notes.Any(x => x.StartsWith("[lowtech]")) && notes.Any(x => x.StartsWith("[!lowtech]")) && notes.Any(x => !x.StartsWith("[")),
                $"{key} has a doctor's note for every colony");
            C(!string.IsNullOrEmpty((string)comp.Element("gotText")) || serious, $"{key} is told when it comes");
            if (serious) C(((string)comp.Element("letterText") ?? "").Contains("{ILLNESS}"), $"{key}'s letter names it by the age");
            string tag = "[ail:" + key;
            C(Pool(serious ? "Council_Journal_Ail_Serious" : "Council_Journal_Ail_Start").Any(l => l.StartsWith(tag)), $"{key} has journal lines when it comes");
            C(Pool("Council_Journal_Ail_Better").Any(l => l.StartsWith(tag)), $"{key} has a journal line when it's over");
            C(Pool("Council_Pool_SayAilment").Count(l => l.StartsWith(tag)) >= 2, $"{key} has lines to say");
            if (serious)
                C(Pool("Council_Journal_Ail_DoctorSaved").Any(l => l.StartsWith(tag)) && Pool("Council_Journal_Ail_DoctorLost").Any(l => l.StartsWith(tag)),
                    $"{key}: the doctor writes when the patient lives and when they die");
        }
        return n;
    }
}
