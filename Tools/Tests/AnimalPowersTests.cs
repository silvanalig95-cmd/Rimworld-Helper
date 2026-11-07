using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The colony's animals with powers (the player's pick "magic and powers", 7 November 2026; Animals/AnimalPowersMath.cs):
/// which PokéWorld type does what at home, each effect's size and its bounds (warmth never past its mark and nothing
/// outdoors, the crops a quarter faster by their own rate and never at night, a battery's trickle, a calming's threshold
/// and cooldowns, the spar's blow, a fire doused); the familiar's mana, its binding's hours and waits; the omen's signs,
/// their order, the pace and the kept day; who mourns a sacred animal; and the mod's side: the clock's slot, the settings
/// and their words, the defs the code names, the jobs, the ward, the wish, the custom, the saved book and its models,
/// the dev actions, the soft reads of the mods by name, and the Almanac's sections.
/// </summary>
public static class AnimalPowersTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "animal powers: " + what);
        }

        // ================= Which type does what =================

        C(AnimalPowersMath.PowerOf("Fire") == PokemonPower.Warm && AnimalPowersMath.PowerOf("Ice") == PokemonPower.Cool && AnimalPowersMath.PowerOf("Grass") == PokemonPower.Growth
          && AnimalPowersMath.PowerOf("Electric") == PokemonPower.Battery && AnimalPowersMath.PowerOf("Psychic") == PokemonPower.Calm
          && AnimalPowersMath.PowerOf("Fighting") == PokemonPower.Spar && AnimalPowersMath.PowerOf("Water") == PokemonPower.Douse, "seven types, seven powers");
        C(new[] { "Normal", "Flying", "Dragon", "Ghost", "Dark", "Fairy", "Steel", "Rock", "Ground", "Bug", "Poison", null, "" }.All(t => AnimalPowersMath.PowerOf(t) == null),
            "the other types do nothing at home");
        int lapras = AnimalPowersMath.Mask(new[] { "Water", "Ice" });
        C(AnimalPowersMath.Has(lapras, PokemonPower.Douse) && AnimalPowersMath.Has(lapras, PokemonPower.Cool) && !AnimalPowersMath.Has(lapras, PokemonPower.Warm),
            "a Water and Ice one douses and cools, and doesn't warm");
        C(AnimalPowersMath.Mask(new[] { "Normal", "Flying" }) == 0 && AnimalPowersMath.Mask(null) == 0, "a Normal and Flying one has no power");
        C(((PokemonPower[])Enum.GetValues(typeof(PokemonPower))).Select(AnimalPowersMath.Bit).Distinct().Count() == 7, "each power has a bit of its own");

        // ================= Fire and Ice: a room's warmth =================

        float heat = AnimalPowersMath.HeatPush(10f, 30, 1f, true);
        C(Math.Abs(heat - AnimalPowersMath.HeatPerSecond * 250f / 60f) < 0.001f, $"a Fire type pushes 6 heat a second through a pass ({heat})");
        C(AnimalPowersMath.HeatPerSecond < 21f / 3f, "less than a third of a heater's 21");
        C(AnimalPowersMath.Degrees(heat, 30) < 1f, "a degree a pass at most in a small room");
        C(AnimalPowersMath.HeatPush(21f, 30, 1f, true) == 0f && AnimalPowersMath.HeatPush(30f, 30, 1f, true) == 0f, "no warming at 21 °C or above");
        C(AnimalPowersMath.Degrees(AnimalPowersMath.HeatPush(20.9f, 30, 1.5f, true), 30) <= 0.1001f, "never past 21 °C");
        C(AnimalPowersMath.HeatPush(30f, 30, 1f, false) < 0f && AnimalPowersMath.HeatPush(23f, 30, 1f, false) == 0f && AnimalPowersMath.HeatPush(10f, 30, 1f, false) == 0f,
            "an Ice type cools only a room over 23 °C");
        C(AnimalPowersMath.Degrees(AnimalPowersMath.HeatPush(23.05f, 30, 1.5f, false), 30) >= -0.0501f, "never below 23 °C");
        C(AnimalPowersMath.HeatPush(0f, 30, 0.1f, true) == AnimalPowersMath.HeatPush(0f, 30, 0.5f, true)
          && AnimalPowersMath.HeatPush(0f, 30, 9f, true) == AnimalPowersMath.HeatPush(0f, 30, 1.5f, true), "its body counts from half to half again");
        C(AnimalPowersMath.HeatPush(0f, 0, 1f, true) == 0f, "no room, no heat");
        C(AnimalPowersMath.WarmTo < AnimalPowersMath.CoolTo && AnimalPowersMath.MaxPerRoom == 2, "warm to 21, cool to 23, two a room");

        // ================= Grass: the crops close by =================

        float own = 250f / (60000f * 5.8f);
        C(Math.Abs(AnimalPowersMath.GrowthPush(5.8f, 1f) - own * 0.25f) < 1e-9f, "a quarter of a crop's own growth through a pass");
        C(Math.Abs(AnimalPowersMath.GrowthPush(5.8f, 0.5f) - own * 0.125f) < 1e-9f, "by its growth rate now");
        C(AnimalPowersMath.GrowthPush(5.8f, 0f) == 0f && AnimalPowersMath.GrowthPush(0f, 1f) == 0f, "nothing for a crop not growing");
        C(!AnimalPowersMath.PlantsAwake(0.1f) && AnimalPowersMath.PlantsAwake(0.5f) && !AnimalPowersMath.PlantsAwake(0.9f), "never at night, when the game's plants rest");
        float perDay = AnimalPowersMath.GrowthPush(5.8f, 1f) * (60000f / 250f) * 0.55f;
        C(perDay < 0.03f, $"a day beside a crop takes it a few hundredths further at most ({perDay:0.000})");

        // ================= Electric: a battery =================

        float wd = AnimalPowersMath.BatteryPush(AnimalPowersMath.BatteryWatts);
        C(Math.Abs(wd * 240f - 100f) < 0.01f, $"100 W: 100 Wd a day resting beside it all day ({wd * 240f:0.0})");
        C(AnimalPowersMath.BatteryPush(-5f) == 0f, "never a negative charge");
        C(AnimalPowersMath.BatteryWatts * 1f / 600f < 0.2f, "a battery's 600 Wd fills in no fewer than six days of it");

        // ================= Psychic: near a break =================

        C(AnimalPowersMath.NearBreak(0.3f, 0.35f) && AnimalPowersMath.NearBreak(0.38f, 0.35f) && !AnimalPowersMath.NearBreak(0.41f, 0.35f), "below the minor break threshold, or less than 5 above it");
        C(AnimalPowersMath.Ready(-1, 1000, 5) && !AnimalPowersMath.Ready(100, 200, 500) && AnimalPowersMath.Ready(100, 700, 500), "cooldowns run their time");
        C(AnimalPowersMath.Ready(900000, 100, 500), "a tick from another clock counts as long ago");
        C(AnimalPowersMath.CalmPersonCooldown == 2 * 60000 && AnimalPowersMath.CalmPokemonCooldown == 60000 && AnimalPowersMath.CalmRadius <= 6f, "two days a colonist, one a Pokémon, six cells");

        // ================= Fighting: the spar =================

        C(AnimalPowersMath.SparBlowChance == 1f / 25f && AnimalPowersMath.SparBlowChance == HarmMath.DrillBlowChance, "a blow lands one spar in 25, as a drill's");
        C(AnimalPowersMath.SparLow >= 1f && AnimalPowersMath.SparHigh <= 6f && AnimalPowersMath.SparLow < AnimalPowersMath.SparHigh, "a bruise, small");
        int swings = AnimalPowersMath.SparMaxTicks / AnimalPowersMath.SparSwingTicks / 2;
        C(swings * AnimalPowersMath.SparXpSwing <= 120f, $"a spar's practice no more than an outsider's bout ({swings * AnimalPowersMath.SparXpSwing})");

        // ================= Water: a fire =================

        C(AnimalPowersMath.DouseDamage > 10f && AnimalPowersMath.DouseDamage < 100f && AnimalPowersMath.DouseRadius < AnimalPowersMath.DouseWalk, "more than rain, less than a fire's whole");

        // ================= Familiars =================

        C(AnimalPowersMath.ManaAfter(0.5f, 1f, AnimalPowersMath.FamiliarManaPerPass) > 0.5f && AnimalPowersMath.ManaAfter(0.9999f, 1f, 0.01f) == 1f
          && AnimalPowersMath.ManaAfter(1.2f, 1f, 0.01f) == 1.2f, "mana up to the pool's top, and never takes any");
        float manaDay = AnimalPowersMath.FamiliarManaPerPass * 240f;
        C(manaDay > 0.05f && manaDay < 0.15f, $"about a tenth of a pool a day near it all day ({manaDay:0.000})");
        C(AnimalPowersMath.BindHour(8) && AnimalPowersMath.BindHour(20) && !AnimalPowersMath.BindHour(21) && !AnimalPowersMath.BindHour(3), "bound by day, where it's seen");
        C(!AnimalPowersMath.BindingStale(0, 60000 * 10) && AnimalPowersMath.BindingStale(0, 60000 * 10 + 1) && !AnimalPowersMath.BindingStale(-1, 99999999), "a binding waits ten days");
        C(AnimalPowersMath.WeaknessOnDeath > 0.1f && AnimalPowersMath.WeaknessOnDeath < 5f, "the arcane weakness of its death stays in the mod's first stage, weakened");

        // ================= Omens =================

        C(AnimalPowersMath.SignOf(true, true, true, true) == OmenSign.Eclipse && AnimalPowersMath.SignOf(false, true, true, true) == OmenSign.Aurora
          && AnimalPowersMath.SignOf(false, false, true, true) == OmenSign.Shiny && AnimalPowersMath.SignOf(false, false, false, true) == OmenSign.KeptDay
          && AnimalPowersMath.SignOf(false, false, false, false) == OmenSign.None, "the sky first, then a shine, then the calendar");
        C(AnimalPowersMath.KeptDay(true, false, true, false, false, 0) && AnimalPowersMath.KeptDay(true, false, false, true, false, 0)
          && AnimalPowersMath.KeptDay(true, false, false, false, true, 0) && AnimalPowersMath.KeptDay(true, false, false, false, false, 1),
            "a tradition, the founding, the player's own or one kept before");
        C(!AnimalPowersMath.KeptDay(true, false, false, false, false, 0) && !AnimalPowersMath.KeptDay(false, false, true, false, false, 3)
          && !AnimalPowersMath.KeptDay(true, true, true, false, false, 3), "not one never kept, not the first snow, not a birthday");
        int day = 60000;
        C(AnimalPowersMath.OmenDue(OmenSign.Eclipse, -1, 100, 0, 0.99f), "the first omen comes");
        C(!AnimalPowersMath.OmenDue(OmenSign.Eclipse, 0, 19 * day, 0, 0f) && AnimalPowersMath.OmenDue(OmenSign.Eclipse, 0, 20 * day, 0, 0f), "twenty days between two");
        C(!AnimalPowersMath.OmenDue(OmenSign.Aurora, -1, 100, AnimalPowersMath.MaxOmens, 0f), "three living at most");
        C(AnimalPowersMath.OmenDue(OmenSign.KeptDay, -1, 100, 0, 0.49f) && !AnimalPowersMath.OmenDue(OmenSign.KeptDay, -1, 100, 0, 0.51f), "half the births on a kept day");
        C(!AnimalPowersMath.OmenDue(OmenSign.None, -1, 100, 0, 0f), "no sign, no omen");
        C(AnimalPowersMath.Thrives(true, false, false, false) && !AnimalPowersMath.Thrives(true, true, false, false) && !AnimalPowersMath.Thrives(true, false, true, false)
          && !AnimalPowersMath.Thrives(true, false, false, true) && !AnimalPowersMath.Thrives(false, false, false, false), "thriving: alive, up, well and fed");
        C(new[] { OmenSign.Eclipse, OmenSign.Aurora, OmenSign.Shiny, OmenSign.KeptDay }.Select(AnimalPowersMath.Key).SequenceEqual(new[] { "eclipse", "aurora", "shiny", "keptday" }),
            "each sign's key");
        C(AnimalPowersMath.BornWithinTicks >= 2500 && AnimalPowersMath.MaxOmenRecords >= AnimalPowersMath.MaxOmens, "an hour's look sees each newborn; the records hold the living");

        // ================= Sacred =================

        C(AnimalPowersMath.Mourns(true, false) && !AnimalPowersMath.Mourns(true, true) && !AnimalPowersMath.Mourns(false, false), "a believer mourns one that wasn't the colony's (the game mourns its own)");

        // ================= The clock =================

        C(Sched.RareOffsets.Contains(Sched.RarePowers) && Sched.RareOffsets.Count(o => o == Sched.RarePowers) == 1, "a 250-tick slot of its own");
        C(Sched.HourlyOffsets.All(h => h % Sched.RareTicks != Sched.RarePowers), "off every hourly pass's tick of the cycle");
        C(PassTable.All.Any(p => p.pass == Pass.RarePowers && p.offset == Sched.RarePowers && p.period == Sched.RareTicks && !p.waiting), "timed by Rimfolk's clock");
        C(AnimalPowersMath.PassTicks == Sched.RareTicks && AnimalPowersMath.DayTicks == Sched.DayTicks && AnimalPowersMath.HourTicks == Sched.HourTicks, "the math's clock is Rimfolk's");

        if (root == null)
        {
            C(false, "found the mod's folder");
            return n;
        }

        // ================= The mod's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";
        string powers = Read("Animals/AnimalPowers.cs"), types = Read("Integrations/PokeWorldTypes.cs"), fam = Read("Integrations/MagicFamiliars.cs");
        string omen = Read("Animals/OmenAnimals.cs"), sacred = Read("Animals/SacredAnimals.cs"), dbg = Read("Animals/AnimalPowersDebug.cs");
        C(powers.Length > 0 && types.Length > 0 && fam.Length > 0 && omen.Length > 0 && sacred.Length > 0 && dbg.Length > 0, "every part's file is there");
        C(powers.Contains("Sched.Every(t, Sched.RareTicks, Sched.RarePowers)") && powers.Contains("PassClock.Time(Pass.RarePowers)"), "the book's tick is the clock's slot");
        C(powers.Contains("Sched.Hourly(now, Sched.RarePowers)") && powers.Contains("Sched.FirstHourOfDay(now)"), "its hour and its day ride on the slot");
        C(!Regex.IsMatch(types + fam + omen + sacred + powers, @"\[HarmonyPatch"), "no Harmony patch");

        // Soft reads by name.
        C(types.Contains("PokeWorldLink.TypesOf") && types.Contains("Faction.OfPlayer") && types.Contains("IsPlayerHome"), "the colony's own Pokémon, by PokéWorld's types, at home");
        C(fam.Contains("\"TM_Mana\"") && fam.Contains("\"TM_ArcaneWeakness\"") && fam.Contains("\"Faceless\"") && fam.Contains("GetNamedSilentFail")
          && !Regex.IsMatch(fam, @"using\s+TorannMagic"), "A RimWorld of Magic read by its defs' names, softly");
        C(fam.Contains("ModsConfig.IsActive(MagicGolems.PackageId)"), "familiars only with the mod");
        C(sacred.Contains("ModsConfig.IdeologyActive") && sacred.Contains("VeneratedAnimals") && sacred.Contains("IsVeneratedAnimal"), "the sacred kinds are Ideology's venerated animals");
        C(Read("Animals/AnimalDilemmas.cs").Split("SacredAnimals.HeldSacred").Length - 1 == 2, "the council's cull counts and marks no sacred animal");
        C(types.Contains("Harm.Roll(pawn, AnimalPowersMath.SparBlowChance, HurtClass.Bout)") && types.Contains("Harm.Injure(pawn, HurtKind.Bruise")
          && types.Contains("Importance.Important"), "the spar's blow under the harm cap, told as harm is");
        C(types.Contains("GenTemperature.PushHeat") && types.Contains("UsesOutdoorTemperature") && types.Contains("AddEnergy") && types.Contains("DamageDefOf.Extinguish")
          && types.Contains("plant.Growth +="), "each effect is the game's own: heat, a battery's energy, a fire put out, a crop's growth");
        C(types.Contains("b.Faction != Faction.OfPlayer"), "only a battery the colony built");
        C(Regex.Matches(types + fam + omen + sacred, @"Speech\.Say\(").Count >= 10, "said over heads through Speech.Say");

        // Settings.
        string settings = Read("CouncilSettings.cs"), catalog = Read("UI/SettingsCatalog.cs");
        var keyed = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(f).Root.Elements())
                keyed[el.Name.LocalName] = el.Value;
        foreach (var id in new[] { "pokemonTypes", "pokemonTypeWarmth", "pokemonTypeGrowth", "pokemonTypeBattery", "pokemonTypeCalm", "pokemonTypeSpar", "pokemonTypeDouse", "familiars", "sacredAnimals", "omenAnimals" })
        {
            C(Regex.IsMatch(settings, @"public bool " + id + @" = true;") && settings.Contains("Scribe_Values.Look(ref " + id + ", \"" + id + "\", true)"), $"{id}: on by default, saved under its name");
            var row = Regex.Match(catalog, @"Toggle\(""" + id + @""", ""(\w+)"", ""(\w+)""");
            C(row.Success && keyed.ContainsKey(row.Groups[1].Value) && keyed.ContainsKey(row.Groups[1].Value + "_Tip") && keyed.ContainsKey(row.Groups[2].Value), $"{id}: a row with its label, tooltip and note");
        }
        C(Regex.Matches(catalog, @"Mod\(Toggle\(""pokemonType\w+"", ""\w+"", ""\w+"", ""pokemonTypes""\), PokeWorldId\)").Count == 6, "six parts under the Pokémon types, with PokéWorld");
        C(catalog.Contains("Mod(Toggle(\"familiars\"") && catalog.Contains("\"CC_SetNote_Familiars\"), MagicId)"), "familiars with A RimWorld of Magic");
        C(keyed.TryGetValue("CC_Clock_Pass_RarePowers", out var clock) && clock.Length > 10, "the clock names the pass");

        // The defs the code names.
        var defs = new Dictionary<string, XElement>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(f);
            }
            catch
            {
                continue;
            }
            foreach (var el in doc.Root.Elements())
            {
                string dn = el.Element("defName")?.Value;
                if (!string.IsNullOrEmpty(dn)) defs[dn] = el;
            }
        }
        string code = types + fam + omen + sacred + powers;
        foreach (Match m in Regex.Matches(code, @"""(Rimfolk_[A-Za-z_]+)"""))
        {
            string name = m.Groups[1].Value;
            if (name.EndsWith("_Say") || name == "Rimfolk_Pool_PW_Say") continue;
            C(defs.ContainsKey(name), $"{name}, named in the code, is a def");
        }
        foreach (var p in Enum.GetNames(typeof(PokemonPower)).Where(x => x != "Spar"))
            C(defs.ContainsKey("Rimfolk_Pool_PW_Say" + p), $"the {p} type's word ({"Rimfolk_Pool_PW_Say" + p})");
        foreach (Match m in Regex.Matches(code, @"""(CC_(?:PW|Fam|Omen|Sacred)_[A-Za-z_]+)"""))
        {
            string key = m.Groups[1].Value;
            if (key.EndsWith("_"))
            {
                foreach (var sign in new[] { "eclipse", "aurora", "shiny", "keptday" })
                    C(keyed.ContainsKey(key + sign), $"{key + sign} is keyed");
            }
            else C(keyed.ContainsKey(key), $"{key} is keyed");
        }
        C(defs.TryGetValue("Rimfolk_FamiliarNear", out var ward) && ward.Descendants("IncomingDamageFactor").FirstOrDefault()?.Value == "0.95"
          && ward.Descendants("disappearsAfterTicks").Any(), "the ward: a twentieth less harm, gone a little after the familiar isn't near");
        C(defs.TryGetValue("Rimfolk_PW_SparGiver", out var giver) && giver.Element("jobDef")?.Value == "Rimfolk_PW_Spar" && giver.Element("giverClass")?.Value == "ColonyCouncil.JoyGiver_PokemonSpar",
            "the spar is a leisure of its own");
        C(defs.TryGetValue("Rimfolk_Wish_Familiar", out var wish) && wish.Element("workerClass")?.Value == "ColonyCouncil.WishWorker_Familiar", "the familiar's wish");
        C(defs.TryGetValue("Rimfolk_Dilemma_SacredDay", out var day1) && day1.Element("traditionOption")?.Value == "0" && day1.Element("seasons")?.Elements().Any() == true
          && day1.Element("workerClass")?.Value == "ColonyCouncil.DilemmaWorker_SacredDay", "the sacred animals' day: a custom with a date, so it joins the calendar");
        C(day1 != null && day1.Descendants("li").Any(li => (string)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_Gathering"
                                                         && li.Element("spots")?.Elements().First().Value == "Stranger"), "its day kept gathers round the animal");
        C(defs.TryGetValue("Rimfolk_Fam_Died", out var died) && died.Descendants("baseMoodEffect").First().Value == "-12" && died.Element("durationDays")?.Value == "15", "a familiar's death: -12 for 15 days");
        C(defs.TryGetValue("Rimfolk_Omen_Died", out var bad) && bad.Descendants("baseMoodEffect").First().Value == "-4", "a bad omen: -4");
        C(defs.TryGetValue("Rimfolk_Omen_Thrives", out var good) && good.Descendants("baseMoodEffect").First().Value == "2" && good.Element("workerClass") != null, "a thriving omen: +2 while it lasts");
        C(defs.TryGetValue("Rimfolk_PW_Calmed", out var calm) && calm.Descendants("baseMoodEffect").First().Value == "5" && calm.Element("durationDays")?.Value == "0.5", "a calming: +5 for half a day");
        C(defs.TryGetValue("Rimfolk_Sacred_Fell", out var fell) && fell.Element("stackLimit")?.Value == "3", "a sacred animal fallen: three at most");

        // Saved: the book, each list capped and modelled.
        C(Regex.Matches(powers, @"Scribe_Collections\.Look\(ref (\w+)").Cast<Match>().Select(m => m.Groups[1].Value).OrderBy(x => x)
            .SequenceEqual(new[] { "bindings", "calmTicks", "familiars", "omens" }), "four collections saved");
        string estimate = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        foreach (var f in new[] { "familiars", "bindings", "omens", "calmTicks" })
            C(estimate.Contains("\"Animals/AnimalPowers.cs:" + f + "\""), $"{f} has a model in the save estimate");
        C(powers.Contains("Caps.TrimOldest(familiars, AnimalPowersMath.MaxFamiliars)") && powers.Contains("Caps.TrimOldest(bindings, AnimalPowersMath.MaxBindings)")
          && powers.Contains("Caps.TrimOldest(omens, AnimalPowersMath.MaxOmenRecords)") && powers.Contains("book.PruneCalm(now)"), "each capped, the cooldowns pruned hourly");

        // The dev actions.
        foreach (var action in new[] { "what each part sees now", "the Pokémon types' pass now", "spars with a Fighting type", "bind the selected animal", "familiar dies",
                     "born under a sign", "the sacred animals' day now" })
            C(dbg.Contains(action), $"a dev action: {action}");

        // The Almanac.
        string alm = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Defs", "AlmanacArticleDefs"), "*.xml").Select(File.ReadAllText));
        C(alm.Contains("Pokémon types at home") && alm.Contains("Familiars") && alm.Contains("Omen animals") && alm.Contains("Sacred animals"), "the Almanac tells all four");
        return n;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
