using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using ColonyCouncil;

/// <summary>docs/apparel/README.md: how the rating works, the reference wearer and enemies, the anchors, the tags, every table, what is less sure and left out.</summary>
public static class Readme
{
    static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\n", " ");

    static string Tech(string t) => string.IsNullOrEmpty(t) ? "" : t switch
    {
        "Neolithic" => "neolithic", "Medieval" => "medieval", "Industrial" => "industrial", "Spacer" => "spacer",
        "Ultra" => "ultra", "Archotech" => "archotech", "Animal" => "animal", _ => t.ToLowerInvariant()
    };

    static string P(float v) => Math.Round(v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";

    static string Tags(Piece p) => string.Join(", ", p.Tags.Select(t => WeaponTagMath.Style(t.Category).Symbol + " " + t.Label));

    static string Subs(Piece p, bool combat)
    {
        var s = p.Rating.Sub;
        string F(string k) => s.TryGetValue(k, out var v) ? v.ToString("0.0") : "";
        var parts = new List<string>();
        if (combat) parts.Add($"{F("guns")} / {F("melee")} / {F("fire")} / {F("mechanoids")}");
        var extra = new List<string>();
        if (!combat) extra.Add($"guns {F("guns")}");
        foreach (var k in new[] { "toxins", "vacuum", "psychic", "psychic shelter", "work", "social", "mechanitor", "magic", "hunting", "carrying" })
            if (s.TryGetValue(k, out var v) && v >= 2f) extra.Add(k + " " + v.ToString("0.0"));
        if (extra.Count > 0) parts.Add(string.Join(", ", extra));
        return string.Join("; ", parts);
    }

    static string Stuffs(Piece p) => p.ByStuff.Count <= 1 || p.ByStuff.Values.Max() - p.ByStuff.Values.Min() < 0.1f ? "" : string.Join(", ", p.ByStuff.Select(kv => kv.Key + " " + kv.Value.ToString("0.0")));

    public static string Write(List<Piece> all, List<(JsonNode n, string why)> leftOut, string[] tableOrder, Dictionary<string, string> tableNames, JsonNode data)
    {
        var sb = new StringBuilder();
        void L(string s = "") => sb.AppendLine(s);
        int rough = all.Count(p => p.Facts.Unsure.Count > 0);
        var anchors = ApparelAnchors.All().ToList();
        ApparelRating A(string n) => anchors.First(x => x.name == n).r;

        L("# Armour and apparel, rated");
        L();
        L("Every piece of armour and apparel in the player's active mods, put on a colonist by the game's own rules and rated from 1.0");
        L("to 10.0 for what it is for, with what it is good and weak at, its tags, the situations it suits and a line in plain words.");
        L("Built by `Tools/ApparelReview` from the numbers `Tools/apparel_extract.py` reads out of the player's own def files (the game,");
        L("its five DLCs and every active mod, as RimWorld 1.7 loads them for the player's mod list, `MayRequire` and `MayRequireAnyOf`");
        L("honoured as the game honours them), with the same arithmetic the mod will use in the game");
        L("(`Source/ColonyCouncil/Apparel/ApparelRateMath.cs` and `ApparelTagMath.cs`). It is the apparel side of the weapon review");
        L("(`docs/weapons/README.md`): the same armour roll, the same scale's shape, the same six tag kinds and colours. The design and");
        L("the player's request are in `docs/design/apparel.md`; `apparel.json` beside this file holds everything below for a tier-list");
        L("page and for recommending apparel later.");
        L();
        L($"**{all.Count + leftOut.Count} apparel defs**: {all.Count} rated, {leftOut.Count} left out (worn only by what they were made for:");
        L($"they vanish when taken off; listed at the end). {rough} of the rated ones are marked *(rough)*: part of what they do is a");
        L("mod's own code the review can name but not value (see \"Where the numbers are less sure\").");
        L();
        L("## How to read a rating");
        L();
        L("- **The rating** says how much a piece does for whoever wears it, against the other pieces of its kind: body armour against");
        L("  body armour, helmets against helmets, a parka against cold-weather gear. 5.5 is the middle of each kind; near it, a point up");
        L("  is a good deal more of what the kind is for, and the scale flattens toward both ends, so a mod's super-armour lands at");
        L("  9-something rather than every strong piece sitting at 10. A piece that costs more than it gives is 1.0.");
        L($"- **What it is for** (its role) decides what counts: armour is judged mostly on the fight, a parka on the cold, a hat on");
        L("  warmth, shade, looks and work, a belt on whatever it does. The roles and how they weigh each area are below.");
        L("- **Rated alone**, on a colonist wearing nothing else, at normal quality, in its usual stuff: fabric in devilstrand, metal in");
        L("  steel, leather in plain leather, wood, stone in granite (the tables name it). Stuff matters far more for apparel than for");
        L("  weapons, so each stuffed piece is also rated in the common stuffs of its kind (cloth, synthread, devilstrand and hyperweave;");
        L("  plain leather, bearskin and thrumbofur; steel, plasteel and uranium), shown in the tables and `apparel.json`.");
        L("- **Sub-ratings** are on scales of their own, the same for every piece: against guns, melee (blades, fists and animals),");
        L("  fire and mechanoids, how much longer the wearer lasts; cold and heat, the degrees of insulation; then toxins, vacuum,");
        L("  psychic (for a psycaster), psychic shelter, work, social and trade, mechanitor, magic, hunting and carrying where a piece");
        L("  does any of it. A head piece's protection and insulation are rated against head pieces: a marine helmet's 6.1 against");
        L("  guns means it does for the head what a body piece of 6.1 does for the body.");
        L("- **Situations**: for a shooter, for a melee fighter, in the cold and in the heat (the overall with that climate's");
        L("  insulation counting in full and the other's not at all), while working, for social or trade, and the sub-ratings.");
        L("- *(rough)* after a rating: part of what the piece does is a mod's own code the review can't read, so it is rated on what it");
        L("  can read and the rest is named. Trust those numbers less.");
        L();
        L("## How the rating works");
        L();
        L("### Protection: the game's armour roll on the human body");
        L();
        L("- **Where hits land.** The game picks the part a hit lands on by its own coverage (its share of its parent, less its");
        L("  children's), from the human body (`Bodies_Humanlike`): a bullet or an arrow can land on any part, the heart and the brain");
        L("  among them, since it sets no depth; a melee blow or a blast lands on an outside part; a blunt blow never on an eye. A");
        L("  piece covers a part when one of its body part groups is the part's. A hit on the head counts double: the brain (10 hit");
        L("  points) and the eyes are there, and one hit can kill or blind.");
        L("- **The armour roll** (`ArmorUtility`): each layer that covers the part, outside in, rolls its armour less the attack's");
        L("  piercing: under half of it the hit is deflected, under all of it halved, from twice it nothing gets through. A sharp hit");
        L("  halved goes on as blunt, so the next layer meets it with its blunt armour. Armour is held at 200% as the game holds it.");
        L("  Sharp, blunt and heat each meet their own armour.");
        L($"- **How much longer the wearer lasts** (toughness): the damage a bare colonist takes over the damage they take with the");
        L($"  piece on, against each family of enemies; the families pooled by the damage they deal and how often a colony meets them");
        L($"  after its first year: guns {P(ApparelRateMath.FamilyWeights["guns"])}, melee {P(ApparelRateMath.FamilyWeights["melee"])}, animals {P(ApparelRateMath.FamilyWeights["animals"])}, mechanoids {P(ApparelRateMath.FamilyWeights["mechanoids"])}, fire {P(ApparelRateMath.FamilyWeights["fire"])}.");
        L();
        L("The reference enemies' attacks, from their defs (the guns' projectiles, the melee weapons in steel as the weapon review has");
        L("them, the animals' and mechanoids' tools by the game's own choice of attack, its best three times in four):");
        L();
        L("| Family | Attack | Damage | Piercing | Kind | Lands | Share of the family |");
        L("|---|---|---|---|---|---|---|");
        foreach (var f in ApparelRateMath.Families)
            foreach (var t in ApparelRateMath.Threats.Where(x => x.Family == f))
                L($"| {f} | {t.Label} | {t.Damage:0.#} | {P(t.Pen)} | {t.Armour.ToString().ToLowerInvariant()} | {(t.Ranged ? "anywhere" : "outside")} | {P(t.Weight)} |");
        L();
        L("Where hits land, by the body's zones (a zone is the parts that share their body part groups):");
        L();
        L("| Body part groups | Shots | Blows and blasts | Blunt blows |");
        L("|---|---|---|---|");
        foreach (var z in ApparelRateMath.Human)
            L($"| {string.Join(", ", z.Groups)} | {z.Ranged:0.0000} | {z.Melee:0.0000} | {z.MeleeBlunt:0.0000} |");
        L();
        L("### The fight: toughness, output and feet");
        L();
        L("A piece's worth in a fight is how much longer its wearer lasts, times how much more they deal, times their feet:");
        L();
        L($"- **Output.** The weapon review's reference fighter (`WeaponRateMath`): a shooter with an assault rifle (Shooting 8, hits");
        L($"  {WeaponRateMath.ShootingAccuracy(8):P0} a cell) and a melee fighter with a steel longsword (Melee 8, {WeaponRateMath.MeleeHitChance(8):P0} melee hit). Shooting accuracy and melee hit a piece");
        L("  changes are skill levels, as the game adds them before its curve; aiming time and cooldown go into the rifle's cycle;");
        L("  melee damage into the sword's blows; sight and manipulation a piece's hediffs change scale both.");
        L("- **Feet.** A slower or quicker walk counts half its share for a shooter and all of it for a melee fighter, as in the");
        L("  weapon review (a colonist walks 4.6 cells a second).");
        L("- **Toughness besides armour.** Melee dodge (in skill levels, the game's curve), damage taken, and pain: a higher pain");
        L("  shock threshold keeps the wearer up longer, counted at half (pain is about half of how a colonist falls).");
        L($"- **Shields.** A shield takes shots and blasts (the game's `CompShield` takes ranged and explosive damage): from full, its");
        L($"  capacity, then its recharge each second, until fire of {ApparelRateMath.IncomingRangedDps:0} damage a second for {ApparelRateMath.FightSeconds:0} seconds breaks it, and it is down for its reset;");
        L($"  at most {P(ApparelRateMath.ShieldCap)} of the fight's shots (an EMP shot drops it at once, a heavy volley overwhelms it). The shield belt takes");
        L($"  {1.1f / 0.033f:0} damage and recharges {0.13f / 0.033f:0.0} a second.");
        L("- **Powers** a piece's verbs give: a jump turns a melee fighter's 20-cell charge under fire into a one-second jump, twice a");
        L($"  fight, and puts them to work sooner; smoke cuts shots by 30% (`ShotReport`'s smoke) for {ApparelRateMath.SmokeSeconds:0} seconds; a low-shield stops two");
        L($"  shots in three for its {ApparelRateMath.LowShieldSeconds:0} seconds (its wearer stays inside it most of the time); firefoam halves a fight's fire; grenades and");
        L("  launched shots are rated by the weapon review's own ability rule; a psychic lance takes one enemy out (counted as the");
        L($"  {ApparelRateMath.EnemyDownValue:0} damage it takes to down one, half again for berserk), an orbital strike three, a summoned turret or drone one, a tox");
        L("  pack half. A power with charges is used once a fight (a jump twice); one gone for good when its charges are spent counts");
        L("  at half. Powers add at most half again to the wearer's output, the weapon review's own cap.");
        L($"- **Who it is for.** Three colonists in four fight with guns, so the overall weighs a shooter's fight three to one over a");
        L("  melee fighter's. A piece that blocks its wearer's shots, an off-hand shield, or one that costs a shooter what it gives a");
        L("  fighter is rated for a melee fighter.");
        L();
        L("### The other areas: a share of what a colonist gets done");
        L();
        L("Everything else a piece does is a share of a colonist: ten degrees of insulation is a tenth (in the climate it is worn");
        L("for), and each stat a piece changes counts at a share per point:");
        L();
        L("| Stat | Area | Share per point |");
        L("|---|---|---|");
        foreach (var kv in ApparelRateMath.StatWorth.OrderBy(kv => kv.Value.area).ThenBy(kv => kv.Key))
            L($"| {kv.Key} | {kv.Value.area} | {kv.Value.perUnit.ToString("0.###", CultureInfo.InvariantCulture)} |");
        L();
        L($"Psychic sensitivity counts in full for a psycaster; lower sensitivity shelters the mind from psychic drones and lances at");
        L($"{ApparelRateMath.ShelterWorth:0.##} a point. A slower or quicker walk counts at half in work outside fights. A RimWorld of Magic's enchantments count at half");
        L("for a mage (more mana, regeneration, shorter cooldowns and cheaper spells in full, experience and arcane damage at half,");
        L("arcane resistance at a quarter). Work a piece bars costs 15%, learning nothing 30%.");
        L();
        L("### Roles");
        L();
        L("A piece's role is judged as made in plain stuff (cloth, plain leather, steel, wood, granite), so a duster is hot-weather");
        L("gear whatever it is made of, then rated in its usual stuff:");
        L();
        L("- an off-hand shield (Vanilla Expanded's); a belt, pack or accessory (the belt, hip and artifact layers) is utility;");
        L("- a psychic, magic or royal piece whose psychic, magic or royal worth outweighs its fight is ceremonial or psychic;");
        L("- on the head: combat headgear (a helmet) when its sharp armour is 40% or more, it is made as armour (its stuff's armour");
        L("  counting at 45% or more) or it makes its wearer a tenth better in a fight (a gunlink); else a hat, mask or hood;");
        L("- on the body: armour when its sharp armour is 40% or more, it is made as armour, or its protection alone lasts its wearer");
        L("  a fifth longer; else a vacuum or hazard suit when keeping out toxins and vacuum is worth more than its fight; else");
        L("  cold-weather (or hot-weather) gear when its cold (or heat) insulation is 14 °C or more, a third above the other and worth");
        L("  at least what the piece does for work, talks, the mind, carrying and hunting; else everyday clothing; pieces made only");
        L("  for children have their own table.");
        L();
        L("How much each area counts, by role, and the scale's middle (the worth that rates 5.5) and width:");
        L();
        L("| Role | " + string.Join(" | ", ApparelRateMath.Areas) + " | Middle | Width |");
        L("|---|" + string.Concat(Enumerable.Repeat("---|", ApparelRateMath.Areas.Length)) + "---|---|");
        foreach (var role in ApparelRateMath.Roles)
        {
            var w = ApparelRateMath.RoleWeights(role);
            var (mid, width) = ApparelRateMath.RoleScale(role);
            L($"| {role} | " + string.Join(" | ", ApparelRateMath.Areas.Select(a => w[a].ToString("0.##", CultureInfo.InvariantCulture))) + $" | {mid:0.##} | {width:0.##} |");
        }
        L();
        L("### What the mods' own code does, and what is counted");
        L();
        L("The player's mods' code (their DLLs, decompiled to read, never run) was read wherever a piece's effect lives there:");
        L();
        L("- **Vanilla Expanded Framework**: its off-hand shields (Vanilla Factions Expanded's, the Elves', the Demigryphs') roll their");
        L("  own armour on the groups the shield covers before any apparel, while the wearer holds a weapon under 1.65 kg that Dual");
        L("  Wield allows in the off hand (`ShieldUtility.UsableWithShields`), so they are rated for a fighter with a sword or a");
        L("  pistol; its shield bubble absorbs shots and blasts like a shield belt without stopping its wearer's own (its energy in");
        L("  damage, recharging its rate times its energy each second); its shield field covers its wearer and those beside them");
        L("  (0.033 energy a point of damage, like the game's shield); its apparel extension's stat factors, barred work and no");
        L("  learning count; camouflage counts as hunting stealth; its apparel hediffs count by their stages; quivers' and the ammo");
        L("  pack's ranged cooldown factor shortens the cooldown between shots (a quiver's for bows and other neolithic and medieval");
        L("  weapons only).");
        L("- **A RimWorld of Magic**: enchantments (mana, regeneration, cooldowns, spell cost, experience, arcane damage and");
        L("  resistance) count for a mage; its whole-body armour offsets (amulets) are a layer on every part; its alignment armour");
        L("  meets only its own Light and Dark damage and isn't counted; its arcane spectre and phantom shift, the orbs' abilities and");
        L("  the rings' actions are its own code, named.");
        L("- **Vanilla Apparel Expanded - Accessories**: the resurrector belt brings its wearer back once when they would die (counted");
        L("  as a second life, then gone); the explosive belt blows up when its wearer dies (named).");
        L("- **Field Medic**: the medic bag lets its wearer stop someone's bleeding in the field (counted as a little work).");
        L("- **Dubs Rimatomics**: its radiation suits' own class adds nothing: they protect by their toxic resistance.");
        L("- **Vanilla Gravship Expanded**: oxygen packs keep their wearer alive in vacuum while their oxygen lasts; the astrorig jumps");
        L("  on its oxygen; the gravship bombardment targeter aims the colony's gravship guns (counted as an orbital strike, rough).");
        L();
        L("Not read, so named and marked *(rough)*: Fairycore Apparel 2's quality-scaled bonuses (its extension isn't in the uploaded");
        L("DLL; read at normal quality), PokeWorld's balls (its code isn't in the upload), Vanilla Factions Expanded - Deserters' bomb");
        L("pack, invisibility engulfer and declassifier, Vanilla Factions Expanded - Empire's techfriar crown's research refresh,");
        L("Rimsenal Security's fortification pack, Vanilla Psycasts Expanded's psyring (its psycast is chosen when it is made), Melee");
        L("Animation's lassos, and a few others whose effect is a hediff of their own.");
        L();
        L("Not counted: quality (all at normal), wear, other pieces worn with it (each piece is rated alone; worn together, every");
        L("layer rolls, outside in), the colony's climate (the cold and heat ratings stand for it), a faith's or a title's rules (named:");
        L("which titles and roles a piece meets is in its tags and `apparel.json`), apparel colour moods, dead man's apparel and human");
        L("leather (no piece is rated in it; a colonist minds wearing it). Patches from other mods are not applied, as in the mod pool;");
        L("the ones that touch a piece's numbers are listed below.");
        L();
        L("## Tags");
        L();
        L("Every piece carries a few tags, given by rules on its own numbers (never by hand), each with a line saying why, the most");
        L("important first, at most seven, keeping its role, its first strength and its first weakness. The six kinds, their colours");
        L("and symbols are the weapon review's own (one table: `WeaponTagMath.Categories`), readable at 4.5:1 or better on Rimfolk's");
        L("window, panel and card grounds:");
        L();
        L("| Symbol | Category | Colour | What it says |");
        L("|---|---|---|---|");
        var says = new Dictionary<TagCategory, string>
        {
            [TagCategory.Strength] = "what it is good at", [TagCategory.Weakness] = "what it is bad at",
            [TagCategory.Risk] = "what can go wrong with it", [TagCategory.Special] = "what it does besides protect and insulate",
            [TagCategory.Role] = "what it is for, and who it suits", [TagCategory.Needs] = "what it asks of its wearer or the colony",
        };
        foreach (var c in WeaponTagMath.Categories) L($"| {c.Symbol} | {c.Label} | `{c.Hex}` | {says[c.Category]} |");
        L();
        L("Every tag and its rule:");
        L();
        L("| Tag | Category | Rule |");
        L("|---|---|---|");
        foreach (var d in ApparelTagMath.ApparelTags.OrderBy(d => (int)d.Category).ThenByDescending(d => d.Importance))
            L($"| {WeaponTagMath.Style(d.Category).Symbol} {d.Label} | {WeaponTagMath.Style(d.Category).Label} | {d.Rule} |");
        L();
        L("## The base game's pieces (the anchors)");
        L();
        L("Normal quality, in the stuff named, the reference colonist. These are checked in `Tools/Tests/ApparelRateTests.cs`, so the");
        L("scale can't drift: body armour from the flak pieces through plate, recon and marine to cataphract armour, helmets from the");
        L("simple helmet to the cataphract helmet, the shield belt above the jump pack among utility, the parka and tribalwear low as");
        L("armour and strong for cold.");
        L();
        L("| Piece | Role | Rating | Against guns / melee / fire / mechanoids | Cold / heat | In the cold / in the heat |");
        L("|---|---|---|---|---|---|");
        foreach (var (name, r) in anchors)
        {
            var s = r.Sub;
            L($"| {name} | {r.Role} | **{r.Overall:0.0}** | {s["guns"]:0.0} / {s["melee"]:0.0} / {s["fire"]:0.0} / {s["mechanoids"]:0.0} | {s["cold"]:0.0} / {s["heat"]:0.0} | {r.Situations["in the cold"]:0.0} / {r.Situations["in the heat"]:0.0} |");
        }
        L();
        L($"The flak vest ({A("flak vest").Overall:0.0}) rates above the flak jacket ({A("flak jacket").Overall:0.0}): its 100% sharp armour over the torso stops more");
        L("than the jacket's 55% over the torso and arms; the jacket's warmth and its place on the shell layer are its own gains (a");
        L("flak vest goes under a duster or a parka). Plate armour's steel stops nearly as much as recon armour, but its 0.8 cells a");
        L("second slower walk costs a fifth of a melee fighter's fight.");
        L();
        L("## Surprises");
        L();
        L("Pieces rated far from what their age suggests (two points or more from the middle of their role and age).");
        L();
        foreach (var line in Surprises(all)) L("- " + line);
        L();
        L("## The review");
        L();
        L("Sorted by rating within each table. Protection: against guns / melee / fire / mechanoids (how much longer the wearer");
        L("lasts, on the sub-ratings' scale). In the cold / heat: the overall in that climate. By stuff: the piece in each common stuff");
        L("of its kind. Tags in the order of importance; the line in plain words is the mod's own.");
        L();
        foreach (var t in tableOrder)
        {
            var list = all.Where(p => p.Table == t).OrderByDescending(p => p.Rating.Overall).ThenBy(p => p.Facts.Label).ToList();
            if (list.Count == 0) continue;
            bool combat = t is "body armour" or "helmet" or "off-hand shield";
            L($"### {tableNames[t]} ({list.Count})");
            L();
            L(combat
                ? "| Rating | Piece | Mod | Age | Stuff | Guns / melee / fire / mech | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |"
                : "| Rating | Piece | Mod | Age | Stuff | Sub-ratings | Cold / heat °C | In the cold / heat | By stuff | Tags | In plain words |");
            L("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var p in list)
            {
                var r = p.Rating;
                string rating = $"**{r.Overall:0.0}**" + (p.Facts.Unsure.Count > 0 ? " *(rough)*" : "");
                L($"| {rating} | {Cell(p.Facts.Label)} | {Cell(p.Facts.ModName)} | {Tech(p.Facts.TechLevel)} | {Cell(p.Facts.StuffLabel)} | {Subs(p, combat)} | " +
                  $"{r.Cold:0.#} / {r.HeatInsulation:0.#} | {r.Situations["in the cold"]:0.0} / {r.Situations["in the heat"]:0.0} | {Cell(Stuffs(p))} | {Cell(Tags(p))} | {Cell(p.Plain)} |");
            }
            L();
        }
        L("## Where the numbers are less sure");
        L();
        L($"{rough} pieces do part of what they do through a mod's own code or a patch; each is rated on what can be read and marked");
        L("*(rough)*. The reasons, by piece:");
        L();
        foreach (var p in all.Where(p => p.Facts.Unsure.Count > 0).OrderBy(p => p.Facts.ModName).ThenBy(p => p.Facts.Label))
            L($"- **{Cell(p.Facts.Label)}** ({Cell(p.Facts.ModName)}): {string.Join("; ", p.Facts.Unsure.Distinct())}.");
        L();
        L("## Patches the review doesn't apply");
        L();
        L("Patches from the active mods that touch an apparel def (its stats, what it changes on its wearer, its comps, its tags or");
        L("layers). The ratings are from before them:");
        L();
        foreach (var g in all.Where(p => Inputs.Arr(p.Node, "patches").Any())
                     .SelectMany(p => Inputs.Arr(p.Node, "patches").Select(x => (p, mod: Inputs.S(x, "modName").Replace(" — ", " - "), what: Inputs.S(x, "what"))))
                     .GroupBy(x => (x.mod, part: x.what)).OrderBy(g => g.Key.mod).ThenBy(g => g.Key.part))
        {
            string meaning = g.Key.part.Split('/')[0] switch
            {
                "apparel" => "changes " + (g.Key.part.Contains('/') ? "its " + g.Key.part.Split('/')[1] : "its apparel fields") + " (what tags and titles read; no change to the numbers rated)",
                "equippedStatOffsets" => "changes what it does to its wearer (" + g.Key.part + ")",
                "statBases" => "changes its stats (" + g.Key.part + ")",
                "comps" => "adds or changes its parts",
                "stuffCategories" => "changes what it can be made of",
                var x => "changes its " + x
            };
            var names = g.Select(x => x.p.Facts.Label).Distinct().OrderBy(x => x).ToList();
            L($"- **{Cell(g.Key.mod)}** {meaning}: {string.Join(", ", names)}.");
        }
        L();
        L("## Left out");
        L();
        foreach (var g in leftOut.GroupBy(x => x.why))
            L($"- **{g.Key}** ({g.Count()}): " + string.Join(", ", g.Select(x => Inputs.S(x.n, "label") + " (" + Inputs.S(x.n, "modName").Replace(" — ", " - ") + ")").OrderBy(x => x)) + ".");
        L("- No apparel in the player's active mods is made for animals or other bodies: every piece left is worn by people (the");
        L("  children's pieces have their own table).");
        L();
        L("## Rebuilding this page");
        L();
        L("```");
        L("python3 Tools/apparel_extract.py <the def collection: Data, Mods, mods.txt, ModsConfig.xml> docs/player-mods/apparel_numbers.json.gz");
        L("dotnet run -c Release --project Tools/ApparelReview");
        L("```");
        return sb.ToString();
    }

    static IEnumerable<string> Surprises(List<Piece> all)
    {
        var lines = new List<(float gap, string line)>();
        foreach (var g in all.GroupBy(p => (p.Table, tech: string.IsNullOrEmpty(p.Facts.TechLevel) ? "Undefined" : p.Facts.TechLevel)).Where(g => g.Count() >= 5))
        {
            var sorted = g.Select(p => p.Rating.Overall).OrderBy(x => x).ToList();
            float median = sorted[sorted.Count / 2];
            foreach (var p in g)
            {
                float gap = p.Rating.Overall - median;
                if (Math.Abs(gap) < 2f) continue;
                string where = gap > 0 ? "above" : "below";
                lines.Add((gap, $"**{Cell(p.Facts.Label)}** ({Cell(p.Facts.ModName)}, {Tech(p.Facts.TechLevel)} {p.Rating.Role}) rates {p.Rating.Overall:0.0}, {Math.Abs(gap):0.0} {where} the middle of its role and age ({median:0.0}): {Cell(p.Plain)}"));
            }
        }
        return lines.OrderByDescending(x => x.gap).Take(12).Concat(lines.OrderBy(x => x.gap).Take(12)).Select(x => x.line).Distinct();
    }
}
