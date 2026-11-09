// The weapon review: every weapon of the player's active mods rated with the mod's own weapon rating
// (Source/ColonyCouncil/Weapons/WeaponRateMath.cs, linked), from the numbers Tools/weapon_extract.py resolved.
// Run from the repository root:
//   python3 Tools/weapon_extract.py <collection folder> docs/player-mods/weapon_numbers.json.gz
//   dotnet run -c Release --project Tools/WeaponReview
// It writes docs/weapons/README.md and docs/weapons/weapons.json.
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ColonyCouncil;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

string root = Directory.GetCurrentDirectory();
while (!File.Exists(Path.Combine(root, "CLAUDE.md")) && Directory.GetParent(root) != null) root = Directory.GetParent(root).FullName;
string input = args.Length > 0 ? args[0] : Path.Combine(root, "docs/player-mods/weapon_numbers.json.gz");
string outDir = args.Length > 1 ? args[1] : Path.Combine(root, "docs/weapons");

JsonNode data;
using (var fs = File.OpenRead(input))
using (Stream s = input.EndsWith(".gz") ? new GZipStream(fs, CompressionMode.Decompress) : fs)
    data = JsonNode.Parse(s);
Inputs.DamageDefs = data["damageDefs"] as JsonObject;

// Each weapon read, rated, tagged and put in words by the mod's own rules (WeaponRead, linked): the game does the same
// from the live def, so a weapon at normal quality in steel rates the same there.
var all = new List<WeaponReport>();
foreach (var w in (JsonArray)data["weapons"])
{
    var r = WeaponRead.Full(Inputs.Facts(w));
    r.Source = w;
    all.Add(r);
}

var rated = all.Where(r => r.Kind != "left out" && r.Kind != "unrated").ToList();
var unrated = all.Where(r => r.Kind == "unrated").ToList();
var leftOut = all.Where(r => r.Kind == "left out").ToList();

string[] tableOrder = { "close", "all-round", "long", "grenades", "launchers", "light melee", "heavy melee" };
var tableNames = new Dictionary<string, string>
{
    ["close"] = "Guns and bows for close quarters",
    ["all-round"] = "Guns and bows, all-round",
    ["long"] = "Long guns and bows",
    ["grenades"] = "Grenades, throwing weapons and one-use launchers",
    ["launchers"] = "Launchers and specialist weapons",
    ["light melee"] = "Melee: light weapons",
    ["heavy melee"] = "Melee: heavy weapons",
};

Directory.CreateDirectory(outDir);
File.WriteAllText(Path.Combine(outDir, "weapons.json"), Json(), new UTF8Encoding(false));
File.WriteAllText(Path.Combine(outDir, "README.md"), Readme(), new UTF8Encoding(false));
Console.WriteLine($"Rated {rated.Count} weapons ({rated.Count(r => r.Unsure.Count > 0)} marked unsure), left out {leftOut.Count}; wrote {outDir}/README.md and weapons.json.");
foreach (var t in tableOrder)
{
    var list = rated.Where(r => r.Table == t).OrderByDescending(r => r.Rating.Overall).ToList();
    Console.WriteLine($"{tableNames[t]}: {list.Count}");
    Console.WriteLine("  top: " + string.Join(", ", list.Take(10).Select(r => $"{r.Label} ({r.ModName}) {r.Rating.Overall:0.0}")));
    Console.WriteLine("  bottom: " + string.Join(", ", list.AsEnumerable().Reverse().Take(10).Select(r => $"{r.Label} ({r.ModName}) {r.Rating.Overall:0.0}")));
}
return 0;

JsonNode W(WeaponReport r) => (JsonNode)r.Source;

string TagsLine(WeaponReport r) => string.Join(", ", r.Tags.Select(t => WeaponTagMath.Style(t.Category).Symbol + " " + t.Label));

// ---------------------------------------------------------------- the README

string Readme()
{
    var sb = new StringBuilder();
    void L(string s = "") => sb.AppendLine(s);
    int unsure = rated.Count(r => r.Unsure.Count > 0);
    L("# Weapons, rated");
    L();
    L("Every weapon in the player's active mods, put through a fight by the game's own rules and rated from 1.0 to 10.0, with");
    L("what it is good and weak at, its special effects and a line in plain words. Built by `Tools/WeaponReview` from the");
    L("numbers `Tools/weapon_extract.py` reads out of the player's own def files (the game, its five DLCs and every active mod,");
    L("as RimWorld 1.7 loads them for the player's mod list, `MayRequire` and `MayRequireAnyOf` honoured as the game honours them),");
    L("with the same arithmetic the mod will use in the game (`Source/ColonyCouncil/Weapons/WeaponRateMath.cs`).");
    L("The design and the player's request are in `docs/design/weapons.md`; `weapons.json` beside this file holds everything");
    L("below for a tier-list page and for recommending weapons later.");
    L();
    int rolled = rated.Count(r => r.Unsure.Count > 0 && r.Unsure.All(u => u.StartsWith("a unique weapon")));
    L($"**{all.Count} weapon defs**: {rated.Count} rated, {unrated.Count} a colonist can wield but whose attack is wholly a mod's");
    L($"own code (named below), and {leftOut.Count} left out because a colonist can't wield them (turret guns, mechanoids' and");
    L($"animals' built-in weapons, weapons made by a spell; listed at the end). {unsure} of the rated ones are marked *(rough)*:");
    L($"{rolled} are unique weapons whose traits are rolled when made (rated as the plain weapon), the other {unsure - rolled} do");
    L("something the review can name but not value (see \"Where the numbers are less sure\").");
    L();
    L("## How to read a rating");
    L();
    L("- **The rating** says how much a weapon gets done in the fights it is made for, against the other weapons of its kind:");
    L("  guns, bows and launchers on one scale, melee weapons on another. 5.5 is the middle: about an assault rifle among guns,");
    L("  a steel longsword or warhammer among melee weapons. Near the middle, a point up is about a third more damage dealt for");
    L("  a gun and a fifth more for a melee weapon; the scale flattens toward both ends, so a mod's weapon three or ten times");
    L("  the best of the base game lands at 9-something rather than all of them sitting at 10.");
    L("- **What it is for** (its role) decides what counts: a sniper rifle is judged mostly at 25 and 40 cells and isn't marked");
    L("  down for being poor at five, a shotgun mostly within a dozen cells, a grenade on what it does to a group.");
    L("- **Sub-ratings** use the same scale. For guns: close (5 cells), medium (12), long (25) and far (40); then unarmoured,");
    L("  armoured (flak), heavy (marine armour) and mechanoids at the distances its role cares about; and a crowd. For melee:");
    L("  unarmoured, armoured, heavy and mechanoids.");
    L("- *(rough)* after a rating: part of what the weapon does is a mod's own code the review can't read, so it is rated on");
    L("  what it can read and the rest is named. Trust those numbers less.");
    L();
    L("## How the rating works");
    L();
    L("A colonist with the weapon fights the reference enemies by the game's own rules, read from the game itself:");
    L();
    L("- **Hitting.** A shot's chance to hit is the shooter's accuracy to the power of the distance in cells, times the weapon's");
    L("  accuracy at that distance (its close, short, medium and long accuracy at 3, 12, 25 and 40 cells, in between by a straight");
    L("  line), times the target's size. Open ground, clear weather, daylight, no cover. Melee: the fighter's melee hit chance,");
    L("  then the target's dodge.");
    L("- **Armour.** Each hit meets the armour on the part it strikes: armour minus the weapon's piercing is the chance to be");
    L("  halved, half of it the chance to be stopped outright, and nothing gets through from twice the armour. Sharp, blunt and");
    L("  heat each meet their own armour.");
    L("- **Firing.** A gun aims (its warm-up), fires its burst and rests (its cooldown); damage per second is over that whole cycle.");
    L("  Melee weapons strike with the attack the game picks: its best attacks three times in four, its middling ones (the fists");
    L("  among them) one in four, the weak ones never, exactly as the game weighs them.");
    L("- **Explosions** land within their scatter or near a miss and hurt whoever is inside the blast; a fuse lets a target on the");
    L("  move walk out (a fifth of the throws lost per second of fuse). Against a bunched group, a blast catches one more enemy in");
    L("  every eight cells (four at most), a missed bullet strays into someone else one time in seven, a beam strikes the one");
    L("  behind. A blast near your own line is a risk, so blasts lose a few hundredths per cell of radius.");
    L("- **Beyond damage.** A second an enemy spends stunned counts as 4 damage (8 for a mechanoid); EMP stuns a mechanoid for half a");
    L("  second a point, then it is immune for 37 seconds; a target set alight counts as 10 more damage; toxic buildup counts 40");
    L("  damage per full dose, a tox gas cloud 8. Heavier hits count a little more, up to a third more at 30 damage a hit, for what");
    L("  one blow can take out (a brain, a heart, a neck, a leg). Stopping power that staggers a man adds 3%, one that staggers");
    L("  big beasts 6%.");
    L("- **Reach.** A gun that can't reach as far as a raider's assault rifle (31 cells) has to close in under fire: a quarter off");
    L("  at 12 cells, nothing off from 31. Damage dealt from beyond 31 cells counts double: the usual raider can't answer it.");
    L("- **Each distance on one footing.** Hitting gets harder with distance for everyone; each distance is judged against what a");
    L("  good shot can do there (the reference shooter's own falloff is taken out), so a sniper's 40 cells and a shotgun's 5 compare.");
    L("- **One use.** A launcher spent in one volley spreads that volley over a 30-second fight.");
    L("- **What it does to its wielder.** Stats a weapon changes while held count where they belong: shooting accuracy and melee hit");
    L("  as more skill, aiming time and cooldowns in the cycle, move speed (a slower carrier: half its share for a gunner, all of");
    L("  it for a melee fighter), dodge, damage taken and pain. Stats with no part in a fight (work speed, psychic sensitivity,");
    L("  comfort) are named with their sign, not counted.");
    L("- **Abilities a weapon grants** count when their defs or code say what they do: a launched projectile, a beam or a burst");
    L("  of flame, rated like a shot and spread over a minute's fight (its charges, or as often as its cooldown allows), adding");
    L("  at most half again to the weapon's own worth (a few charges help a fight, they don't carry it). Abilities whose effect");
    L("  can't be read are named and marked, not counted.");
    L();
    L("### What the mods' own code does, and what is counted");
    L();
    L("The player's mods' code (their DLLs, decompiled to read, never run) was read wherever a weapon's effect lives there:");
    L();
    L("- **Vanilla Expanded Framework**: its flame streams and gauss slugs are a line from the gun that strikes whoever stands on");
    L("  it, once (`dealsDamageOnce`) or every so many ticks of its life; its laser capacitor, in quick-fire mode, takes a set");
    L("  share off the aim with each shot from the same spot (counted over a stand of ten shots), and at full heat each shot can");
    L("  blast the shooter (tagged *Can hurt you*); weapons with two firing modes are rated in their best; heavy weapons only go to");
    L("  someone in an exoframe or with the Tough trait (tagged *Restricted*) and lose hit points with each shot.");
    L("- **VFE Medieval 2**: a matchlock fires several balls at once (its `shotCount`), each twice as hard point blank and fading to");
    L("  nothing at its range, and leaves smoke around the shooter; the warbow's arrows arc over cover; the heraldic standard rallies");
    L("  its bearer (named, not counted).");
    L("- **A RimWorld of Magic**: the Staff of Blazing Power's bolt is a blast that ignores armour, and only a mage can fire it; the fire");
    L("  wand's blast burns through armour, the ice wand's shard pierces it, the lightning wand's sparks stun nearby; Light and Dark");
    L("  damage meet only the mod's own alignment armour, which ordinary armour doesn't give; a cleaving weapon strikes the target");
    L("  for 60% more and everyone beside it for 60%; a deflecting weapon turns aside its base chance plus a share per level of");
    L("  Melee (counted as defence). Winter's Reign launches its own flying objects and isn't rated.");
    L("- **VWE Bioferrite**: the heavy incinerator sprays like the game's incinerator, its abilities shoot like guns or spray like");
    L("  the incinerator, all counted. **VFE Settlers**: dynamite's fuse runs 20 to 666 ticks, and a quarter go off as they land.");
    L("  **VFE Tribals**' throwspikes, the Fairycore wands' orbs and the cryptoforge bolter fly as bullets do (the wands need someone");
    L("  who can channel; the orbs' and the cryptoforge's extra effects are named, not valued).");
    L("- **The game's own code**, for weapons whose class it names: the doomsday rocket adds three fire blasts around its impact;");
    L("  the fire arrow spreads fuel and starts a fire where it lands; the incinerator and beam weapons sweep a path across the");
    L("  target, striking it on its share of the burst's ticks with no roll to hit.");
    L();
    L("Not read, so named and marked *(rough)*: Rimsenal Federation's crucible damage, VWE Non-Lethal's tear gas, anaesthetic darts and");
    L("conditional stun, the cryptoforge's slowing, VFE Pirates' gravity hammer's push, VQE Ancients' scalpel, Alpha Genes' black hydra");
    L("bullet and the Fairycore wands' orb effects (their code wasn't in the upload or their effect is a hediff of their own).");
    L();
    L("For step 2, the weapon mods' own defaults (no settings file of theirs in the player's game, so they run on them): Simple");
    L("Sidearms' default preset (Basic) carries melee sidearms up to 1.9 kg and ranged ones up to 2.7 kg, two of each and three in");
    L("all; Dual Wield takes any weapon in the off hand but a bow, a blowgun, one over 3 kg or a melee weapon over 1.5 kg, and counts");
    L("bows, shotguns and anything over 3 kg two-handed. The *Sidearm*, *Offhand* and *Two-handed* tags follow these.");
    L();
    L("Not counted: cover, weather, darkness, quality (all rated at normal), bionics, moving while shooting (RunAndGun), Melee");
    L("Animation's moves, a mod's own code (named instead). Patches from other mods are not applied, as in the mod pool; the ones");
    L("that touch a weapon are listed below.");
    L();
    L("### The reference colonist and enemies");
    L();
    L("| Who | What the game gives them |");
    L("|---|---|");
    L($"| The colonist | Shooting 8 (hits {WeaponRateMath.ShootingAccuracy(8):P0} a cell), Melee 8 ({WeaponRateMath.MeleeHitChance(8):P0} melee hit), no traits, a human's fists; the weapon at normal quality, of steel where it takes metal (wood, else granite, otherwise) |");
    L($"| A raider in plain clothes | no armour, dodges {WeaponRateMath.MeleeDodgeChance(8):P0} in melee |");
    L($"| A raider in flak | flak vest (sharp 100%, blunt 36%, heat 27%) on torso and neck, {WeaponRateMath.VestShare:P0} of hits; flak pants (55/8/10) on the legs, {WeaponRateMath.PantsShare:P0}; a steel simple helmet (45/22/0), {WeaponRateMath.HelmetShare:P0}; the rest bare |");
    L($"| A raider in marine armour | marine armour and helmet (sharp 106%, blunt 45%, heat 54%) on {WeaponRateMath.MarineShare:P0} of hits, hands and feet bare |");
    L("| Mechanoids | half a lancer or scyther (sharp 40%, blunt 20%, heat 200%), half a centipede (72/22/200, so big it is twice as easy to hit); no dodge, immune to fire and toxins, stunned by EMP |");
    L();
    L("Where hits land is the human body's own part coverage. The mix the overall weighs: a quarter plain raiders, two fifths in");
    L("flak, a fifth in marine armour, the rest mechanoids, as a colony meets them after its first year (light melee weapons");
    L("lean a little more on everyday fights: 40/35/15/10). EMP weapons that harm no flesh are judged three fifths on mechanoids.");
    L();
    L("### What each role weighs");
    L();
    L("| Role | 5 cells | 12 | 25 | 40 | Against a crowd | Which weapons |");
    L("|---|---|---|---|---|---|---|");
    foreach (var (role, which) in new[] {
        ("close", "under 20 cells' range, or under 26 when most accurate close and a quarter worse by 25 (pistols, SMGs, shotguns, short bows)"),
        ("all-round", "the rest of the guns and bows"), ("long", "38 cells' range or more, at least as accurate far as near"),
        ("area", "an explosion or a throw that scatters (grenades, launchers)"), ("one-use", "spent in one volley") })
    {
        var dw = WeaponRateMath.DistanceWeights(role);
        L($"| {role} | {dw[0]:P0} | {dw[1]:P0} | {dw[2]:P0} | {dw[3]:P0} | {WeaponRateMath.CrowdWeight(role):P0} of what it adds | {which} |");
    }
    L("| light melee / heavy melee | | | | | | under 1.5 kg / 1.5 kg and more |");
    L();
    L("## Tags");
    L();
    L("Every weapon carries a few tags, given by rules on its own numbers (never by hand), each with a line saying why. A weapon");
    L("shows its most important few, at most seven, keeping its role, its first strength and its first weakness. Six categories,");
    L("each a colour from Rimfolk's own palette (readable at 4.5:1 or better on its window, panel and card grounds) and a symbol,");
    L("so the colour is never the only cue. The tier-list page and the game's weapon card use these same colours and symbols.");
    L();
    L("| Symbol | Category | Colour | What it says |");
    L("|---|---|---|---|");
    var says = new Dictionary<TagCategory, string>
    {
        [TagCategory.Strength] = "what it is good at", [TagCategory.Weakness] = "what it is bad at",
        [TagCategory.Risk] = "what can go wrong with it", [TagCategory.Special] = "what it does besides damage",
        [TagCategory.Role] = "what it is for", [TagCategory.Needs] = "what it asks of its wielder or the colony",
    };
    foreach (var c in WeaponTagMath.Categories)
        L($"| {c.Symbol} | {c.Label} | `{c.Hex}` | {says[c.Category]} |");
    L();
    L("Every tag and its rule:");
    L();
    L("| Tag | Category | Rule |");
    L("|---|---|---|");
    foreach (var d in WeaponTagMath.WeaponTags.OrderBy(d => (int)d.Category).ThenByDescending(d => d.Importance))
        L($"| {WeaponTagMath.Style(d.Category).Symbol} {d.Label} | {WeaponTagMath.Style(d.Category).Label} | {d.Rule} |");
    L();
    L("## The base game's weapons (the anchors)");
    L();
    L("Normal quality, steel where made of stuff, the reference colonist. These are checked in `Tools/Tests/WeaponRateTests.cs`,");
    L("so the scale can't drift: the knife low, the longsword in the middle, plasteel and uranium higher, the monosword and the");
    L("zeushammer near the top; the revolver and the short bow low, the bolt-action and assault rifles in the middle, the charge");
    L("rifle and lance high, the LMG, sniper and minigun by what they are for.");
    L();
    L("| Weapon | Role | Rating | Sub-ratings |");
    L("|---|---|---|---|");
    foreach (var (name, r) in WeaponAnchors.All())
        L($"| {name} | {r.Role} | **{r.Overall:0.0}** | {string.Join(", ", r.Sub.Select(kv => kv.Key + " " + kv.Value.ToString("0.0")))} |");
    L();
    L("## Surprises");
    L();
    L("Weapons rated far from what their age or price suggests (two points or more from the middle of their kind and age).");
    L();
    foreach (var line in Surprises()) L("- " + line);
    L();
    L("## The review");
    L();
    L("Sorted by rating within each table. Distances: close / medium / long / far (5, 12, 25, 40 cells; 1.0 where it can't reach).");
    L("Enemies: unarmoured / armoured / heavy / mechanoids. Specials list every effect; those marked \"not counted\" are named only.");
    L();
    foreach (var t in tableOrder)
    {
        var list = rated.Where(r => r.Table == t).OrderByDescending(r => r.Rating.Overall).ThenBy(r => r.Label).ToList();
        if (list.Count == 0) continue;
        L($"### {tableNames[t]} ({list.Count})");
        L();
        bool melee = t.Contains("melee");
        if (melee)
        {
            L("| Rating | Weapon | Mod | Age | Unarm. / armour / heavy / mech | Tags | In plain words | Specials |");
            L("|---|---|---|---|---|---|---|---|");
        }
        else
        {
            L("| Rating | Weapon | Role | Mod | Age | Close / medium / long / far | Unarm. / armour / heavy / mech | Tags | In plain words | Specials |");
            L("|---|---|---|---|---|---|---|---|---|---|");
        }
        foreach (var r in list)
        {
            string rating = $"**{r.Rating.Overall:0.0}**" + (r.Unsure.Count > 0 ? " *(rough)*" : "");
            string name = Words.Cell(r.Label) + (string.IsNullOrEmpty(r.StuffLabel) ? "" : " (" + Words.Cell(r.StuffLabel) + ")");
            if (melee)
                L($"| {rating} | {name} | {Words.Cell(r.ModName)} | {Words.Tech(r.Tech)} | {Words.Armour(r.Rating)} | {Words.Cell(TagsLine(r))} | {Words.Cell(r.Plain)} | {Words.Cell(WeaponWords.SpecialsLine(r.Rating))} |");
            else
                L($"| {rating} | {name} | {r.Rating.Role} | {Words.Cell(r.ModName)} | {Words.Tech(r.Tech)} | {Words.Subs(r.Rating)} | {Words.Armour(r.Rating)} | {Words.Cell(TagsLine(r))} | {Words.Cell(r.Plain)} | {Words.Cell(WeaponWords.SpecialsLine(r.Rating))} |");
        }
        L();
    }
    L("## Where the numbers are less sure");
    L();
    L($"{unsure} weapons do part of what they do through a mod's own code, a rolled trait or a patch; each is rated on what");
    L("can be read and marked *(rough)*. The reasons, by weapon:");
    L();
    foreach (var r in rated.Where(r => r.Unsure.Count > 0).OrderBy(r => r.ModName).ThenBy(r => r.Label))
        L($"- **{Words.Cell(r.Label)}** ({Words.Cell(r.ModName)}): {string.Join("; ", r.Unsure)}.");
    L();
    L("## Patches the review doesn't apply");
    L();
    L("Patches from the active mods that touch a weapon's combat numbers or what it carries. None of the active mods patches the");
    L("base game's or another mod's damage, accuracy or cooldowns (no Combat Extended); these are the ones there are:");
    L();
    foreach (var g in all.Where(r => Inputs.Arr(W(r), "patches").Any())
                 .SelectMany(r => Inputs.Arr(W(r), "patches").Select(p => (r, mod: Inputs.S(p, "modName"), what: Inputs.S(p, "what"))))
                 .GroupBy(x => (x.mod, part: x.what.Split('/')[0])).OrderBy(g => g.Key.mod))
    {
        string meaning = g.Key.part switch
        {
            "weaponClasses" => "adds the weapon classes Ideology's precepts read (no change to the fight)",
            "comps" => "adds parts (for unique weapons, more kinds of traits they can roll; for persona weapons, their persona)",
            "modExtensions" => "adds who may carry a heavy weapon",
            "stuffCategories" => "changes what it can be made of",
            _ => "changes its " + g.Key.part + " (the rating is from before the patch)"
        };
        var names = g.Select(x => x.r.Label).Distinct().OrderBy(x => x).ToList();
        L($"- **{Words.Cell(g.Key.mod)}** {meaning}: {string.Join(", ", names)}.");
    }
    L();
    if (unrated.Count > 0)
    {
        L("## Wieldable but not rated");
        L();
        foreach (var r in unrated) L($"- **{Words.Cell(r.Label)}** ({Words.Cell(r.ModName)}): {r.Unrated}.");
        L();
    }
    L("## Left out: what a colonist can't wield");
    L();
    foreach (var g in leftOut.GroupBy(r => Inputs.S(W(r), "leftOut") == "" ? "no attack a colonist can use" : Inputs.S(W(r), "leftOut")).OrderByDescending(g => g.Count()))
    {
        L($"- **{g.Key}** ({g.Count()}): " + string.Join(", ", g.GroupBy(r => r.ModName).OrderBy(x => x.Key)
            .Select(m => m.Key + ": " + string.Join(", ", m.Select(r => r.Label).Distinct().OrderBy(x => x)))) + ".");
    }
    L();
    L("## Rebuilding this page");
    L();
    L("```");
    L("python3 Tools/weapon_extract.py <the def collection: Data, Mods, mods.txt, ModsConfig.xml> docs/player-mods/weapon_numbers.json.gz");
    L("dotnet run -c Release --project Tools/WeaponReview");
    L("```");
    return sb.ToString();
}

IEnumerable<string> Surprises()
{
    var groups = rated.GroupBy(r => (kind: r.Rating.Kind, tech: string.IsNullOrEmpty(r.Tech) ? "Undefined" : r.Tech)).Where(g => g.Count() >= 5);
    var lines = new List<(float gap, string line)>();
    foreach (var g in groups)
    {
        var sorted = g.Select(r => r.Rating.Overall).OrderBy(x => x).ToList();
        float median = sorted[sorted.Count / 2];
        foreach (var r in g)
        {
            float gap = r.Rating.Overall - median;
            if (Math.Abs(gap) < 2f || r.Table == "launchers" || r.Rating.Role == "utility") continue;
            string where = gap > 0 ? "above" : "below";
            lines.Add((gap, $"**{Words.Cell(r.Label)}** ({Words.Cell(r.ModName)}, {Words.Tech(r.Tech)} {r.Rating.Kind}) rates {r.Rating.Overall:0.0}, {Math.Abs(gap):0.0} {where} the middle of its age ({median:0.0}): {Words.Cell(r.Plain)}"));
        }
    }
    return lines.OrderByDescending(x => x.gap).Take(12).Concat(lines.OrderBy(x => x.gap).Take(12)).Select(x => x.line).Distinct();
}

// ---------------------------------------------------------------- the JSON

string Json()
{
    var arr = new JsonArray();
    foreach (var r in rated.OrderBy(r => r.Table).ThenByDescending(r => r.Rating.Overall))
    {
        var g = r.Rating;
        var o = new JsonObject
        {
            ["defName"] = r.DefName, ["label"] = r.Label, ["mod"] = r.Mod, ["modName"] = r.ModName, ["techLevel"] = r.Tech,
            ["kind"] = g.Kind, ["table"] = r.Table, ["tableLabel"] = tableNames[r.Table], ["role"] = g.Role,
            ["rating"] = g.Overall, ["fightValue"] = Math.Round(g.FightValue, 3),
            ["sub"] = Obj(g.Sub), ["situations"] = Obj(g.Situations), ["dps"] = Obj(g.Dps),
            ["skill"] = g.Skill, ["skillGain"] = Math.Round(g.SkillGain, 3), ["ratingBySkill"] = Obj(r.BySkill), ["ratingByTrait"] = Obj(r.ByTrait),
            ["tags"] = new JsonArray(r.Tags.Select(t => (JsonNode)new JsonObject
            {
                ["id"] = t.Id, ["label"] = t.Label, ["category"] = t.Category.ToString(), ["colour"] = WeaponTagMath.Style(t.Category).Hex,
                ["symbol"] = WeaponTagMath.Style(t.Category).Symbol, ["why"] = t.Why
            }).ToArray()),
            ["traits"] = new JsonArray(g.Tags.Select(t => (JsonNode)t).ToArray()),
            ["bestFor"] = new JsonArray(g.BestFor.Select(t => (JsonNode)t).ToArray()),
            ["weakWhen"] = new JsonArray(g.WeakWhen.Select(t => (JsonNode)t).ToArray()),
            ["specials"] = new JsonArray(g.Specials.Select(s => (JsonNode)new JsonObject { ["key"] = s.Key, ["label"] = s.Label, ["counted"] = s.Counted, ["sign"] = s.Sign }).ToArray()),
            ["plain"] = r.Plain, ["unsure"] = new JsonArray(r.Unsure.Select(t => (JsonNode)t).ToArray()),
            ["crowdFactor"] = Math.Round(g.CrowdFactor, 3), ["reachFactor"] = Math.Round(g.ReachFactor, 3), ["passiveFactor"] = Math.Round(g.PassiveFactor, 3),
            ["abilityValue"] = Math.Round(g.AbilityValue, 3), ["mass"] = r.Mass, ["marketValue"] = r.Market, ["stuff"] = r.StuffLabel,
            ["passives"] = new JsonArray(r.Passives.Select(p => (JsonNode)new JsonObject { ["stat"] = p.Stat, ["offset"] = p.Offset }).ToArray()),
            ["abilities"] = new JsonArray(r.Abilities.Select(a => (JsonNode)new JsonObject { ["label"] = a.Label, ["counted"] = a.Counted, ["charges"] = a.Charges, ["note"] = a.Note }).ToArray()),
            ["patches"] = JsonNode.Parse(W(r)["patches"]?.ToJsonString() ?? "[]"),
        };
        if (r.Ranged != null)
        {
            var w = r.Ranged;
            o["numbers"] = new JsonObject
            {
                ["damage"] = w.Damage, ["armourPen"] = Math.Round(w.ArmourPen, 3), ["armour"] = w.Armour.ToString(), ["harmsHealth"] = w.HarmsHealth,
                ["stoppingPower"] = w.StoppingPower, ["range"] = w.Range, ["minRange"] = w.MinRange, ["warmup"] = w.Warmup, ["cooldown"] = w.Cooldown,
                ["burst"] = w.Burst, ["ticksBetweenBurst"] = w.TicksBetweenBurst, ["accuracy"] = new JsonArray(w.AccTouch, w.AccShort, w.AccMedium, w.AccLong),
                ["explosionRadius"] = w.ExplosionRadius, ["forcedMissRadius"] = w.ForcedMissRadius, ["oneUse"] = w.OneUse,
                ["cycleSeconds"] = Math.Round(WeaponRateMath.CycleSeconds(w, WeaponRateMath.Reference), 3)
            };
            if (r.MeleeRating != null) o["meleeRating"] = r.MeleeRating.Overall;
        }
        if (r.Rating.Kind == "melee")
        {
            var shares = WeaponRateMath.MeleeShares(r.Melee, WeaponRateMath.Reference);
            o["attacks"] = new JsonArray(shares.Select(x =>
            {
                var n = WeaponRateMath.MeleeNumbers(x.attack, r.Melee, WeaponRateMath.Reference);
                return (JsonNode)new JsonObject
                {
                    ["label"] = x.attack.Label, ["natural"] = x.attack.Natural, ["armour"] = x.attack.Armour.ToString(),
                    ["damage"] = Math.Round(n.damage, 2), ["armourPen"] = Math.Round(n.pen, 3), ["cooldown"] = Math.Round(n.cooldown, 2), ["share"] = Math.Round(x.share, 4)
                };
            }).ToArray());
        }
        arr.Add(o);
    }
    var meta = new JsonObject
    {
        ["about"] = "Every weapon of the player's active mods rated by Rimfolk's weapon rating (Source/ColonyCouncil/Weapons/WeaponRateMath.cs); see docs/weapons/README.md.",
        ["referenceFighter"] = "Shooting 8 and Melee 8, no traits, a human's natural attacks; normal quality, steel where made of stuff (wood, else granite, otherwise).",
        ["targets"] = new JsonArray(WeaponRateMath.Targets.Select(t => (JsonNode)new JsonObject { ["key"] = t.Key, ["label"] = t.Label, ["size"] = t.Size, ["dodge"] = t.Dodge, ["mechanoid"] = t.Mechanoid }).ToArray()),
        ["rangedMix"] = new JsonArray(WeaponRateMath.RangedMix.Select(x => (JsonNode)x).ToArray()),
        ["distances"] = new JsonArray(WeaponRateMath.Distances.Select(x => (JsonNode)x).ToArray()),
        ["scale"] = new JsonObject { ["rangedMid"] = WeaponRateMath.RangedMid, ["rangedWidth"] = WeaponRateMath.RangedWidth, ["meleeMid"] = WeaponRateMath.MeleeMid, ["meleeWidth"] = WeaponRateMath.MeleeWidth },
        ["tagCategories"] = new JsonArray(WeaponTagMath.Categories.Select(c => (JsonNode)new JsonObject
        {
            ["category"] = c.Category.ToString(), ["label"] = c.Label, ["colour"] = c.Hex, ["symbol"] = c.Symbol, ["icon"] = c.IconKey
        }).ToArray()),
        ["tagVocabulary"] = new JsonArray(WeaponTagMath.WeaponTags.Select(d => (JsonNode)new JsonObject
        {
            ["id"] = d.Id, ["label"] = d.Label, ["category"] = d.Category.ToString(), ["importance"] = d.Importance, ["rule"] = d.Rule
        }).ToArray()),
        ["counts"] = new JsonObject { ["defs"] = all.Count, ["rated"] = rated.Count, ["unrated"] = unrated.Count, ["leftOut"] = leftOut.Count, ["unsure"] = rated.Count(r => r.Unsure.Count > 0) },
        ["anchors"] = new JsonArray(WeaponAnchors.All().Select(a => (JsonNode)new JsonObject { ["name"] = a.name, ["role"] = a.r.Role, ["rating"] = a.r.Overall, ["sub"] = Obj(a.r.Sub) }).ToArray()),
    };
    var lo = new JsonArray(leftOut.Select(r => (JsonNode)new JsonObject { ["defName"] = r.DefName, ["label"] = r.Label, ["mod"] = r.Mod, ["modName"] = r.ModName, ["reason"] = Inputs.S(W(r), "leftOut") == "" ? "no attack a colonist can use" : Inputs.S(W(r), "leftOut") }).ToArray());
    var ur = new JsonArray(unrated.Select(r => (JsonNode)new JsonObject { ["defName"] = r.DefName, ["label"] = r.Label, ["mod"] = r.Mod, ["modName"] = r.ModName, ["reason"] = r.Unrated }).ToArray());
    // One weapon to a line: small, and a change to one weapon is one line of a diff.
    var pretty = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    var flat = new JsonSerializerOptions { WriteIndented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    var sb = new StringBuilder();
    void List(string name, JsonArray items, bool last)
    {
        sb.Append("  \"").Append(name).Append("\": [\n");
        for (int i = 0; i < items.Count; i++)
            sb.Append("    ").Append(items[i].ToJsonString(flat)).Append(i < items.Count - 1 ? ",\n" : "\n");
        sb.Append("  ]").Append(last ? "\n" : ",\n");
    }
    sb.Append("{\n  \"meta\": ").Append(meta.ToJsonString(pretty).Replace("\n", "\n  ")).Append(",\n");
    List("weapons", arr, false);
    List("unrated", ur, false);
    List("leftOut", lo, true);
    sb.Append("}\n");
    return sb.ToString();
}

static JsonObject Obj(Dictionary<string, float> d)
{
    var o = new JsonObject();
    foreach (var kv in d) o[kv.Key] = Math.Round(kv.Value, 2);
    return o;
}
