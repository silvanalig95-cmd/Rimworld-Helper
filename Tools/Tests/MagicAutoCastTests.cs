using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Casting on their own (Source/ColonyCouncil/Integrations/MagicAutoCast.cs, MagicAutoCastMath.cs; docs/magic-autocast.md):
/// the spells used and how each lands (fire, reach, over the heads), Shadow Bolt and the area spells left out, the reserve
/// (the player's mage's numbers), safe prey for Shadow Strike, the hunt's and the fight's rules, the choice, the pause
/// between casts, one opening per prey, Shadow Strike's draft let go; every name of the mod in the player's own active
/// mods and in ModDefNames.txt; the words; and the seams (the pass's tick, the button, the mod's own job, its auto-cast
/// marks never touched, the setting, the save's model).
/// </summary>
public static class MagicAutoCastTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "casting on their own: " + what);
        }

        // ================= The spells =================

        var names = MagicAutoCastMath.Spells.Select(s => s.name).ToList();
        C(names.Distinct().Count() == names.Count, "each spell once");
        foreach (string want in new[]
                 {
                     "TM_ArcaneBolt", "TM_Firebolt", "TM_Icebolt", "TM_LightningBolt", "TM_FrostRay", "TM_MagicMissile", "TM_Poison", "TM_Headshot",
                     "TM_ThrowingKnife", "TM_Spite", "TM_ShadowStrike"
                 })
            C(names.Contains(want), $"{want} is used");
        C(!names.Contains("TM_ShadowBolt") && MagicAutoCastMath.Dropped.TryGetValue("TM_ShadowBolt", out var why) && why.Length > 20,
            "Shadow Bolt is left out, and why (its orb hurts those along its way and cleaves past the target)");
        C(MagicAutoCastMath.AreaSpells.Count >= 20 && MagicAutoCastMath.AreaSpells.All(a => MagicAutoCastMath.Of(a) == null && !names.Contains(a)),
            "never a spell that hits an area (storms, blasts, rains, cones, sprays)");
        C(MagicAutoCastMath.Spells.Where(s => s.fire).Select(s => s.name).SequenceEqual(new[] { "TM_Firebolt" }), "Firebolt is the one that sets things alight");
        C(MagicAutoCastMath.Of("TM_FrostRay_II")?.name == "TM_FrostRay" && MagicAutoCastMath.Of("TM_Spite_III")?.name == "TM_Spite"
          && MagicAutoCastMath.Of("TM_MagicMissile_I")?.name == "TM_MagicMissile" && MagicAutoCastMath.Of("TM_ArcaneBolt")?.name == "TM_ArcaneBolt",
            "a spell's levels are the spell");
        C(MagicAutoCastMath.Of(null) == null && MagicAutoCastMath.Of("TM_Heal") == null && MagicAutoCastMath.Of("TM_ShadowBolt_II") == null && MagicAutoCastMath.Of("") == null,
            "nothing else is cast: heals, the left out, nothing");
        var might = new[] { "TM_Headshot", "TM_ThrowingKnife", "TM_Spite", "TM_ShadowStrike" };
        C(MagicAutoCastMath.Spells.All(s => (s.pays == MagicAutoCastMath.Pays.Stamina) == might.Contains(s.name)),
            "the fighters' techniques are paid in stamina, the spells in mana");
        C(MagicAutoCastMath.Spells.All(s => s.reach >= 1 && s.weight > 0), "every spell keeps the target's neighbours clear, and has a weight");
        C(MagicAutoCastMath.Of("TM_Icebolt").reach >= 4 && MagicAutoCastMath.Of("TM_MagicMissile").reach >= 5 && MagicAutoCastMath.Of("TM_ArcaneBolt").reach >= 3
          && MagicAutoCastMath.Of("TM_LightningBolt").reach >= 3, "the scattering ones keep a wider ring clear (shards, missiles, wandering bolts, sparks)");
        C(MagicAutoCastMath.Spells.Where(s => s.overhead).Select(s => s.name).OrderBy(x => x).SequenceEqual(new[] { "TM_Poison", "TM_ShadowStrike" }),
            "only Poison (over the heads) and Shadow Strike (the caster steps there) need no clear line");

        // ================= The reserve: the player's mage, Myst (mana 0.75, stamina 1) =================

        C(MagicAutoCastMath.Reserve == 0.3f, "30% of the pool is kept (the player's answer)");
        C(MagicAutoCastMath.KeepsReserve(0.75f, 0.75f, 0.25f) && MagicAutoCastMath.KeepsReserve(0.5f, 0.75f, 0.25f) && !MagicAutoCastMath.KeepsReserve(0.45f, 0.75f, 0.25f),
            "Arcane Bolt from a full pool, again from two thirds, not a third time");
        C(MagicAutoCastMath.KeepsReserve(1f, 1f, 0.65f) && !MagicAutoCastMath.KeepsReserve(0.9f, 1f, 0.65f), "Shadow Strike's 0.65 stamina only from a nearly full pool");
        C(MagicAutoCastMath.KeepsReserve(0.3f, 1f, 0f) && !MagicAutoCastMath.KeepsReserve(0.29f, 1f, 0f), "a free spell still needs the reserve there");
        C(!MagicAutoCastMath.KeepsReserve(float.NaN, 1f, 0.1f) && !MagicAutoCastMath.KeepsReserve(1f, 0f, 0.1f) && !MagicAutoCastMath.KeepsReserve(1f, 1f, -1f)
          && !MagicAutoCastMath.KeepsReserve(1f, 1f, float.NaN) && !MagicAutoCastMath.KeepsReserve(1f, 1f, float.PositiveInfinity), "a pool or cost not read: no cast");

        // ================= Safe prey for Shadow Strike =================

        C(MagicAutoCastMath.SafePrey(0f, false, false, 0.2f, 1f) && MagicAutoCastMath.SafePrey(0.005f, false, false, 1f, 1f), "a hare, a deer: safe");
        C(!MagicAutoCastMath.SafePrey(MagicAutoCastMath.SafeManhunterChance, false, false, 1f, 1f) && !MagicAutoCastMath.SafePrey(0.2f, false, false, 1f, 1f),
            "an animal the hunt designator warns about (it may turn on the hunter): not safe");
        C(!MagicAutoCastMath.SafePrey(0f, true, false, 0.5f, 1f), "a predator fights back: not safe");
        C(!MagicAutoCastMath.SafePrey(0f, false, true, 1f, 1f), "never a person");
        C(MagicAutoCastMath.SafePrey(0f, false, false, 1.5f, 1f) && !MagicAutoCastMath.SafePrey(0f, false, false, 1.6f, 1f) && !MagicAutoCastMath.SafePrey(0f, false, false, 2.4f, 1f),
            "up to half again the hunter's size (an elk or a muffalo is too big)");
        C(!MagicAutoCastMath.SafePrey(float.NaN, false, false, 1f, 1f) && !MagicAutoCastMath.SafePrey(0f, false, false, 1f, 0f), "not read: not safe");

        // ================= The hunt's and the fight's rules =================

        var fire = MagicAutoCastMath.Of("TM_Firebolt");
        var strike = MagicAutoCastMath.Of("TM_ShadowStrike");
        var arcane = MagicAutoCastMath.Of("TM_ArcaneBolt");
        C(!MagicAutoCastMath.MayOnHunt(fire, true) && MagicAutoCastMath.MayInFight(fire, false), "no fire on a hunt; in a fight it may");
        C(MagicAutoCastMath.MayOnHunt(strike, true) && !MagicAutoCastMath.MayOnHunt(strike, false), "Shadow Strike on a hunt only on safe prey");
        C(MagicAutoCastMath.MayInFight(strike, true) && !MagicAutoCastMath.MayInFight(strike, false), "in a fight only on an enemy with nobody of theirs near");
        C(MagicAutoCastMath.MayOnHunt(arcane, false) && MagicAutoCastMath.MayInFight(arcane, false), "a plain bolt any time");
        C(!MagicAutoCastMath.MayOnHunt(null, true) && !MagicAutoCastMath.MayInFight(null, true), "nothing unknown");

        MagicAutoCastMath.Option O(string name, bool ready = true, bool reserve = true, bool reach = true, bool clear = true) =>
            new MagicAutoCastMath.Option { name = name, ready = ready, reserve = reserve, inReach = reach, clear = clear };
        string Pick(bool hunt, bool safe, bool alone, params MagicAutoCastMath.Option[] o) => MagicAutoCastMath.Pick(o, hunt, safe, alone);
        // Myst: Arcane Bolt and Shadow Strike.
        C(Pick(true, true, false, O("TM_ArcaneBolt"), O("TM_ShadowStrike")) == "TM_ShadowStrike", "Myst opens a hunt on safe prey with Shadow Strike");
        C(Pick(true, false, false, O("TM_ArcaneBolt"), O("TM_ShadowStrike")) == "TM_ArcaneBolt", "on prey that may fight back, with Arcane Bolt");
        C(Pick(true, true, false, O("TM_ArcaneBolt"), O("TM_ShadowStrike", reserve: false)) == "TM_ArcaneBolt", "Shadow Strike below the reserve: Arcane Bolt");
        C(Pick(false, false, true, O("TM_ArcaneBolt"), O("TM_ShadowStrike")) == "TM_ShadowStrike"
          && Pick(false, false, false, O("TM_ArcaneBolt"), O("TM_ShadowStrike")) == "TM_ArcaneBolt", "in a fight Shadow Strike on a lone enemy, else the bolt");
        C(Pick(true, true, false, O("TM_Firebolt"), O("TM_ArcaneBolt")) == "TM_ArcaneBolt" && Pick(true, true, false, O("TM_Firebolt")) == null,
            "a fire mage's hunt: no Firebolt");
        C(Pick(false, false, false, O("TM_Firebolt"), O("TM_ArcaneBolt")) == "TM_Firebolt", "in a fight the harder hit first");
        C(Pick(false, false, false, O("TM_LightningBolt", ready: false), O("TM_Icebolt", reach: false), O("TM_Firebolt", clear: false), O("TM_FrostRay")) == "TM_FrostRay",
            "one on its cooldown, out of reach or with a friend near the target is passed over");
        C(Pick(false, false, false, O("TM_FrostRay_II"), O("TM_Poison")) == "TM_FrostRay_II", "a level is cast by its own name");
        C(Pick(false, false, false) == null && MagicAutoCastMath.Pick(null, false, false, false) == null && Pick(false, false, false, O("TM_Heal"), O("TM_ShadowBolt")) == null,
            "nothing ready, nothing known: nothing cast");
        C(Pick(false, false, false, O("TM_Spite"), O("TM_ThrowingKnife")) == "TM_Spite" && Pick(false, false, false, O("TM_Headshot"), O("TM_LightningBolt")) == "TM_Headshot",
            "the fighters' techniques by their weight too");

        // ================= Pauses, prey, the strike's draft, the list =================

        C(MagicAutoCastMath.GapOver(-1, 0) && !MagicAutoCastMath.GapOver(1000, 1000 + MagicAutoCastMath.FightGapTicks - 1)
          && MagicAutoCastMath.GapOver(1000, 1000 + MagicAutoCastMath.FightGapTicks) && MagicAutoCastMath.GapOver(1000, 900), "a pause between a caster's casts in a fight");
        C(MagicAutoCastMath.FightGapTicks >= Sched.RareTicks, "at least a pass apart");
        var opened = new Dictionary<int, int> { [5] = 1000, [6] = 50000, [7] = 90000 };
        C(MagicAutoCastMath.Opened(opened, 5, 1000) && MagicAutoCastMath.Opened(opened, 5, 1000 + MagicAutoCastMath.OpenedKeepTicks - 1)
          && !MagicAutoCastMath.Opened(opened, 5, 1000 + MagicAutoCastMath.OpenedKeepTicks) && !MagicAutoCastMath.Opened(opened, 8, 1000), "one opening cast per prey, remembered a day");
        C(MagicAutoCastMath.Prune(opened, 70000) == 2 && opened.Count == 1 && opened.ContainsKey(6), "the old (and a future a load took back) forgotten");
        C(MagicAutoCastMath.LetGoOfDraft(true, false, true, true) && !MagicAutoCastMath.LetGoOfDraft(true, true, true, true) && !MagicAutoCastMath.LetGoOfDraft(false, false, true, true)
          && !MagicAutoCastMath.LetGoOfDraft(true, false, true, false) && !MagicAutoCastMath.LetGoOfDraft(true, false, false, true),
            "Shadow Strike's draft let go only for one it drafted, still at its orders");
        C(MagicAutoCastMath.EndTheModsMelee(true, true, true, true, false) && !MagicAutoCastMath.EndTheModsMelee(true, true, true, true, true)
          && !MagicAutoCastMath.EndTheModsMelee(true, false, true, true, false) && !MagicAutoCastMath.EndTheModsMelee(false, true, true, true, false),
            "one drafted before goes back to what they did, unless they were fighting hand to hand");
        C(MagicAutoCastMath.StrikeReturnTicks > 300 && MagicAutoCastMath.CastWaitTicks >= MagicAutoCastMath.StrikeReturnTicks, "after the mod's return of 300 ticks");
        var on = Enumerable.Range(1, 70).ToList();
        C(MagicAutoCastMath.Trim(on) == 10 && on.Count == MagicAutoCastMath.MaxOn && on[0] == 11, "the switched-on list kept to its cap, the oldest first to go");

        // ================= The clock =================

        C(Sched.RareMagicAutoCast == 103 && Sched.RareOffsets.Count(o => o == Sched.RareMagicAutoCast) == 1
          && Sched.HourlyOffsets.All(h => h % Sched.RareTicks != Sched.RareMagicAutoCast) && !Sched.HourlyOnCycle.Contains(Sched.RareMagicAutoCast)
          && !new[] { Sched.RareNews, Sched.RarePlayOut, Sched.RareWeekMemory, Sched.RareCouriers, Sched.RareVisitors, Sched.RareTimed, Sched.RareRearrange,
              Sched.RareNeighbours, Sched.RareTriage, Sched.RareHauling, Sched.RareGuestHelp, Sched.RareOutsiders, Sched.RareJourneys, Sched.RareOutsiderTalk,
              Sched.RareGodPresence }.Contains(Sched.RareMagicAutoCast), "a 250-tick slot of its own (103)");
        C(PassTable.All.Any(p => p.pass == Pass.RareMagicAutoCast && p.offset == Sched.RareMagicAutoCast && p.period == Sched.RareTicks && !p.waiting), "timed by the clock");

        // ================= The words =================

        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var el in XDocument.Load(file).Root!.Elements())
                keyed[el.Name.LocalName] = el.Value;
        C(keyed.TryGetValue("CC_MagicAutoCast_Gizmo", out var label) && label.Length > 0 && label.Length <= 24, "the button's label, short");
        C(keyed.TryGetValue("CC_MagicAutoCast_GizmoDesc", out var desc) && desc.Contains("{PAWN_nameDef}") && desc.Contains("{SPELLS}") && desc.Contains("{RESERVE}"),
            "its description names them, their spells and the reserve");
        foreach (var k in new[] { "CC_MagicAutoCast_Gizmo", "CC_MagicAutoCast_GizmoDesc", "CC_Settings_MagicAutoCast", "CC_Settings_MagicAutoCast_Tip", "CC_SetNote_MagicAutoCast",
                     "CC_Clock_Pass_RareMagicAutoCast" })
            C(keyed.TryGetValue(k, out var t) && t.Length > 0 && !t.Contains('—') && !t.Contains('!'), $"{k}: there, plain (no dash, no exclamation)");
        C(keyed.TryGetValue("CC_Settings_MagicAutoCast_Tip", out var tip) && tip.Contains("30%") && MagicAutoCastMath.Spells.All(s => tip.Contains(Words(s.name))),
            "the setting's tip names the reserve and every spell used");

        // ================= Every name of the mod: in the player's active mods and in ModDefNames.txt =================

        var listed = new HashSet<string>(File.ReadAllLines(Path.Combine(root, "Tools", "Tests", "ModDefNames.txt")).Where(l => !l.StartsWith("#") && l.Trim().Length > 0).Select(l => l.Trim()));
        var active = new HashSet<string>(Catalogue(root).Where(c => c.active && c.mod == "torann.arimworldofmagic").Select(c => c.def));
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string math = File.ReadAllText(Path.Combine(src, "Integrations", "MagicAutoCastMath.cs"));
        string cast = File.ReadAllText(Path.Combine(src, "Integrations", "MagicAutoCast.cs"));
        string rom = File.ReadAllText(Path.Combine(src, "Peoples", "Magic", "PeoplesMagic.cs"));
        var inCode = Regex.Matches(math + cast, @"""((?:TM_|TMCast|Projectile_)[A-Za-z_]+)""").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(inCode.Count >= 35, $"the mod's names in the code read ({inCode.Count})");
        foreach (string name in inCode)
        {
            C(listed.Contains(name), $"{name} is in ModDefNames.txt");
            C(active.Contains(name), $"{name} is in A RimWorld of Magic, active in the player's mods");
        }
        foreach (string level in new[] { "TM_FrostRay_I", "TM_FrostRay_II", "TM_FrostRay_III", "TM_MagicMissile_I", "TM_MagicMissile_II", "TM_MagicMissile_III", "TM_Spite_I", "TM_Spite_II", "TM_Spite_III" })
            C(listed.Contains(level) && active.Contains(level) && MagicAutoCastMath.Of(level) != null, $"{level}: a level the mod has, used as its spell");
        foreach (string job in new[] { "TMCastAbilityVerb", "TMCastAbilitySelf", "CastAbilityVerb", "CastAbilitySelf" })
            C(rom.Contains("\"" + job + "\"") && listed.Contains(job), $"the cast job {job} is read by name and listed");
        C(active.Contains("TMCastAbilityVerb") && active.Contains("TMCastAbilitySelf"), "the mod's own cast jobs are in the player's mods");

        // ================= The seams =================

        C(cast.Contains("Sched.RareMagicAutoCast") && cast.Contains("Pass.RareMagicAutoCast"), "the pass on its tick, timed");
        C(!Regex.IsMatch(cast, @"Messages\.Message|MoteMaker\.ThrowText|Log\.Message"), "no message, mote text or log line from the pass (the cast is seen on the map)");
        C(cast.Contains("RomSpells.JobFor(") && cast.Contains("StartJob(job, JobCondition.InterruptForced, null, resume,"),
            "cast by the mod's own job, what they were doing put back after it");
        C(cast.Contains("HuntAgain(target, cur)") && cast.Contains("EnqueueFirst(again") && cast.Contains("HuntOn(c)"), "a hunt that can't wait is queued again, and goes on after the cast");
        C(cast.Contains("PawnUtility.GetManhunterOnDamageChance(prey, hunter, 1f)"), "safe prey by the game's own reading, from up close");
        C(cast.Contains("drafter.FireAtWill") && cast.Contains("HostilityResponseMode.Attack"), "drafted with fire at will, undrafted with attack as their response");
        C(!Regex.IsMatch(math + cast + rom, @"(?i)""autocast""|\.autocast\s*=|autocasting\s*=") , "the mod's own auto-cast marks are never read or written");
        C(!Regex.IsMatch(cast + math, @"using\s+(TorannMagic|AbilityUser)") && !Regex.IsMatch(cast, @"\bTorannMagic\.\w+\s*[\.(]"), "nothing of the mod's by type: read by name only");
        C(rom.Contains("\"TorannMagic.CompAbilityUserMight\"") && rom.Contains("\"ActualManaCost\"") && rom.Contains("\"ActualStaminaCost\"") && rom.Contains("\"Verb\"")
          && rom.Contains("GetNamedSilentFail(name)"), "the might component, the costs, the verb and the jobs read softly");
        C(Regex.Matches(cast, @"\[HarmonyPatch\(typeof\(Pawn\), nameof\(Pawn\.GetGizmos\)\)\]").Count == 1 && cast.Contains("if (!RimfolkPatches.On) yield break;"),
            "the button is a hook, off with the deeper hooks");
        C(cast.Contains("Scribe_Collections.Look(ref on, \"on\", LookMode.Value)"), "the switched-on list saved");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("\"Integrations/MagicAutoCast.cs:on\""), "and modelled in the save estimate");
        var setting = SettingsCatalog.Find("magicAutoCast");
        C(setting != null && setting.page == SettingsPage.OtherMods && setting.mods != null && setting.mods.Contains(SettingsCatalog.MagicId) && setting.Parent == "deepHooks",
            "the setting under A RimWorld of Magic on the other mods' page, under the deeper hooks");
        C(Regex.IsMatch(File.ReadAllText(Path.Combine(src, "CouncilSettings.cs")), @"public bool magicAutoCast = true;")
          && File.ReadAllText(Path.Combine(src, "CouncilSettings.cs")).Contains("Scribe_Values.Look(ref magicAutoCast, \"magicAutoCast\", true);"), "on by default, saved");
        C(cast.Contains("ColonyCouncilMod.Settings?.magicAutoCast ?? true") && cast.Contains("PeoplesMagic.Loaded"), "off with the setting, and without the mod");
        C(File.Exists(Path.Combine(root, "docs", "magic-autocast.md")), "the notes: docs/magic-autocast.md");

        return n;
    }

    /// <summary>A spell's name in the setting's words ("TM_LightningBolt" is Lightning Bolt).</summary>
    private static string Words(string def) => Regex.Replace(def.Substring(3), "(?<=[a-z])(?=[A-Z])", " ");

    private static List<(string type, string def, string mod, bool active)> catalogue;

    private static List<(string type, string def, string mod, bool active)> Catalogue(string root)
    {
        if (catalogue != null) return catalogue;
        catalogue = new List<(string, string, string, bool)>();
        using var fs = File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz"));
        using var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress);
        using var rd = new StreamReader(gz);
        rd.ReadLine();
        string line;
        while ((line = rd.ReadLine()) != null)
        {
            var c = line.Split('\t');
            if (c.Length >= 4) catalogue.Add((c[0], c[1], c[2], c[3] == "yes"));
        }
        return catalogue;
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
