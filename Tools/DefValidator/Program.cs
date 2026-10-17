// Offline checker for the mod's XML. RimWorld only tells you about XML mistakes when you load the game;
// this catches most of them at build time by checking every element against the real compiled types.
//
// Usage: dotnet run -- <modRoot> <rimworldRefDir> <modDll>
//   rimworldRefDir = the folder with Assembly-CSharp.dll from the Krafs.Rimworld.Ref package.

using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var modRoot = Path.GetFullPath(args[0]);
var refDir = Path.GetFullPath(args[1]);
var modDll = Path.GetFullPath(args[2]);

var errors = new List<string>();
var warnings = new List<string>();

var paths = Directory.GetFiles(refDir, "*.dll").Append(modDll).ToList();
// Optional extra reference assemblies (e.g. Harmony) as a 4th argument: a folder or a dll.
if (args.Length > 3)
{
    var extra = Path.GetFullPath(args[3]);
    if (Directory.Exists(extra)) paths.AddRange(Directory.GetFiles(extra, "*.dll", SearchOption.AllDirectories));
    else if (File.Exists(extra)) paths.Add(extra);
}
// Harmony from the local NuGet cache, when present (Rimfolk's patches reference it).
var harmonyRef = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget/packages/lib.harmony.ref");
if (Directory.Exists(harmonyRef) && !paths.Any(p => Path.GetFileName(p) == "0Harmony.dll"))
{
    var dll = Directory.GetFiles(harmonyRef, "0Harmony.dll", SearchOption.AllDirectories).OrderByDescending(f => f).FirstOrDefault();
    if (dll != null) paths.Add(dll);
}
// netstandard facades, which Harmony's reference assembly points at.
var netstd = Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "netstandard.dll");
if (netstd.Length > 0 && !paths.Any(p => Path.GetFileName(p) == "netstandard.dll")) paths.Add(netstd[0]);
using var mlc = new MetadataLoadContext(new PathAssemblyResolver(paths), "mscorlib");
var ac = mlc.LoadFromAssemblyPath(Path.Combine(refDir, "Assembly-CSharp.dll"));
var mod = mlc.LoadFromAssemblyPath(modDll);

List<Type> SafeTypes(Assembly a)
{
    try { return a.GetTypes().ToList(); }
    catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToList()!; }
}

var allTypes = SafeTypes(ac).Concat(SafeTypes(mod)).ToList();
var byFullName = allTypes.Where(t => t.FullName != null).GroupBy(t => t.FullName!).ToDictionary(g => g.Key, g => g.First());
string[] searchNamespaces = { "Verse", "RimWorld", "Verse.AI", "RimWorld.Planet", "Verse.Grammar", "RimWorld.QuestGen" };

Type? ResolveType(string name)
{
    if (byFullName.TryGetValue(name, out var t)) return t;
    foreach (var ns in searchNamespaces)
        if (byFullName.TryGetValue(ns + "." + name, out t)) return t;
    return null;
}

var defType = byFullName["Verse.Def"];
bool IsDef(Type t) => t != null && (t == defType || t.IsSubclassOf(defType));

FieldInfo? FindField(Type t, string name)
{
    for (var cur = t; cur != null; cur = cur.BaseType)
    {
        var f = cur.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (f != null) return f;
    }
    return null;
}

// ---------- Collect all defs ----------
var defFiles = Directory.GetFiles(Path.Combine(modRoot, "Defs"), "*.xml", SearchOption.AllDirectories);
var docs = new List<(string file, XDocument doc)>();
foreach (var f in defFiles)
{
    try { docs.Add((f, XDocument.Load(f, LoadOptions.SetLineInfo))); }
    catch (Exception e) { errors.Add($"{Rel(f)}: XML parse error: {e.Message}"); }
}

string Rel(string p) => Path.GetRelativePath(modRoot, p);

var modDefs = new Dictionary<string, HashSet<string>>(); // def type full name -> defNames
var abstractNames = new HashSet<string>();
var crossRefs = new List<(string file, int line, Type type, string name)>();

foreach (var (file, doc) in docs)
{
    foreach (var el in doc.Root!.Elements())
    {
        var t = ResolveType(el.Name.LocalName);
        // A def of another mod's type (RimHUD's widgets) loads only with that mod (MayRequire): it can't be checked here.
        if (t == null && el.Attribute("MayRequire") != null) { warnings.Add($"{Rel(file)}:{Line(el)}: <{el.Name.LocalName}> is another mod's def type (MayRequire), not checked"); continue; }
        if (t == null) { errors.Add($"{Rel(file)}:{Line(el)}: unknown def type <{el.Name.LocalName}>"); continue; }
        if (!IsDef(t)) { errors.Add($"{Rel(file)}:{Line(el)}: <{el.Name.LocalName}> is not a Def"); continue; }
        var nameAttr = el.Attribute("Name")?.Value;
        if (nameAttr != null) abstractNames.Add(nameAttr);
        if (el.Attribute("Abstract")?.Value == "True") continue;
        var defName = el.Element("defName")?.Value;
        if (string.IsNullOrEmpty(defName)) { errors.Add($"{Rel(file)}:{Line(el)}: def without defName"); continue; }
        var key = RootDefType(t).FullName!;
        if (!modDefs.TryGetValue(key, out var set)) modDefs[key] = set = new HashSet<string>();
        if (!set.Add(defName)) errors.Add($"{Rel(file)}:{Line(el)}: duplicate defName {defName} for {key}");
    }
}

Type RootDefType(Type t)
{
    // DefDatabase is keyed by the most-derived type that directly extends a "database" def; for our
    // purposes the declared type is fine, except subclasses of ThingDef etc. aren't used here.
    return t;
}

int Line(XElement e) => ((System.Xml.IXmlLineInfo)e).LineNumber;

// ---------- Validate structure ----------
var intVec2 = new Regex(@"^\(\s*-?\d+\s*,\s*-?\d+\s*\)$");
var colorRe = new Regex(@"^\(\s*\d+(\.\d+)?\s*,\s*\d+(\.\d+)?\s*,\s*\d+(\.\d+)?\s*(,\s*\d+(\.\d+)?\s*)?\)$");
var ruleRe = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*(\(.*\))?->");
var floatRange = new Regex(@"^-?\d+(\.\d+)?(\s*~\s*-?\d+(\.\d+)?)?$");
var intRange = new Regex(@"^-?\d+(\s*~\s*-?\d+)?$");

void ValidateFields(XElement el, Type t, string file)
{
    foreach (var child in el.Elements())
    {
        var name = child.Name.LocalName;
        var f = FindField(t, name);
        if (f == null)
        {
            errors.Add($"{Rel(file)}:{Line(child)}: {t.Name} has no field '{name}'");
            continue;
        }
        ValidateValue(child, f.FieldType, file);
    }
}

void ValidateValue(XElement el, Type ft, string file)
{
    var cls = el.Attribute("Class")?.Value;
    if (cls != null)
    {
        var ct = ResolveType(cls);
        if (ct == null) { errors.Add($"{Rel(file)}:{Line(el)}: unknown Class \"{cls}\""); return; }
        if (!ft.IsAssignableFrom(ct) && !ct.IsSubclassOf(ft)) { errors.Add($"{Rel(file)}:{Line(el)}: Class {cls} is not a {ft.Name}"); return; }
        ft = ct;
    }
    string v = el.HasElements ? "" : el.Value.Trim();
    string fn = ft.FullName ?? ft.Name;

    if (fn == "System.String")
    {
        if (el.HasElements) errors.Add($"{Rel(file)}:{Line(el)}: <{el.Name.LocalName}> should be text, has child elements");
        return;
    }
    if (ft.IsEnum)
    {
        var names = ft.GetFields(BindingFlags.Public | BindingFlags.Static).Select(x => x.Name).ToHashSet();
        foreach (var part in v.Split(',').Select(s => s.Trim()))
            if (!names.Contains(part)) errors.Add($"{Rel(file)}:{Line(el)}: '{part}' is not a {ft.Name} value");
        return;
    }
    switch (fn)
    {
        case "System.Int32":
            if (!int.TryParse(v, out _)) errors.Add($"{Rel(file)}:{Line(el)}: '{v}' is not an int");
            return;
        case "System.Single":
            if (!float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)) errors.Add($"{Rel(file)}:{Line(el)}: '{v}' is not a float");
            return;
        case "System.Boolean":
            if (v != "true" && v != "false" && v != "True" && v != "False") errors.Add($"{Rel(file)}:{Line(el)}: '{v}' is not a bool");
            return;
        case "System.Type":
            if (ResolveType(v) == null) errors.Add($"{Rel(file)}:{Line(el)}: unknown type '{v}'");
            return;
        case "Verse.IntVec2":
            if (!intVec2.IsMatch(v)) errors.Add($"{Rel(file)}:{Line(el)}: '{v}' is not an IntVec2 like (1,2)");
            return;
        case "Verse.Rot4":
            if (v != "North" && v != "East" && v != "South" && v != "West") errors.Add($"{Rel(file)}:{Line(el)}: '{v}' is not a Rot4");
            return;
        case "UnityEngine.Color":
            if (!colorRe.IsMatch(v)) errors.Add($"{Rel(file)}:{Line(el)}: '{v}' is not a color");
            return;
        case "Verse.FloatRange":
            if (!floatRange.IsMatch(v)) errors.Add($"{Rel(file)}:{Line(el)}: '{v}' is not a FloatRange like 1.5~3");
            return;
        case "Verse.IntRange":
            if (!intRange.IsMatch(v)) errors.Add($"{Rel(file)}:{Line(el)}: '{v}' is not an IntRange like 1~3");
            return;
    }
    if (IsDef(ft))
    {
        crossRefs.Add((file, Line(el), ft, v));
        return;
    }
    if (ft.IsGenericType && ft.GetGenericTypeDefinition().FullName == "System.Collections.Generic.List`1")
    {
        var inner = ft.GetGenericArguments()[0];
        // Types with custom XML loading (StatModifier, ThingDefCountClass...) use <DefName>value</DefName> items.
        if (inner.GetMethods().Any(m => m.Name == "LoadDataFromXmlCustom"))
        {
            foreach (var li in el.Elements())
                if (li.Name.LocalName != "li") warnings.Add($"{Rel(file)}:{Line(li)}: {inner.Name} '{li.Name.LocalName}': make sure it exists in vanilla");
            return;
        }
        foreach (var li in el.Elements())
        {
            if (li.Name.LocalName != "li") errors.Add($"{Rel(file)}:{Line(li)}: list items must be <li>, found <{li.Name.LocalName}>");
            ValidateValue(li, inner, file);
        }
        if (fn.Contains("System.String") && el.Name.LocalName == "rulesStrings")
        {
            foreach (var li in el.Elements("li"))
                if (!ruleRe.IsMatch(li.Value.Trim())) errors.Add($"{Rel(file)}:{Line(li)}: bad grammar rule '{li.Value.Trim()}'");
        }
        return;
    }
    if (ft.IsPrimitive || ft.IsValueType)
    {
        warnings.Add($"{Rel(file)}:{Line(el)}: value type {fn} not checked ('{v}')");
        return;
    }
    // A plain class: check its fields.
    ValidateFields(el, ft, file);
}

// Abstract defs from the base game that the mod's defs may inherit from.
var vanillaParents = new HashSet<string> { "ScenarioBase", "PlayerFactionBase", "MealCooked", "CookMealBase" };
foreach (var (file, doc) in docs)
{
    foreach (var el in doc.Root!.Elements())
    {
        var t = ResolveType(el.Name.LocalName);
        if (t == null || !IsDef(t)) continue;
        var parent = el.Attribute("ParentName")?.Value;
        if (parent != null && !abstractNames.Contains(parent) && !vanillaParents.Contains(parent)) errors.Add($"{Rel(file)}:{Line(el)}: ParentName '{parent}' not found in this mod");
        ValidateFields(el, t, file);
    }
}

// ---------- Cross references ----------
// Known vanilla names: every static field of a [DefOf] class.
var knownVanilla = new HashSet<string>();
foreach (var t in allTypes)
{
    if (!t.CustomAttributes.Any(a => a.AttributeType.Name == "DefOf")) continue;
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
        knownVanilla.Add(f.FieldType.FullName + ":" + f.Name);
}

bool ModHas(Type t, string name)
{
    foreach (var kv in modDefs)
    {
        var dt = byFullName[kv.Key];
        if ((dt == t || dt.IsSubclassOf(t)) && kv.Value.Contains(name)) return true;
    }
    return false;
}

foreach (var (file, line, type, name) in crossRefs)
{
    if (ModHas(type, name)) continue;
    if (knownVanilla.Contains(type.FullName + ":" + name)) continue;
    if (name.StartsWith("Council_")) { errors.Add($"{Rel(file)}:{line}: {type.Name} '{name}' is not defined in this mod"); continue; }
    warnings.Add($"{Rel(file)}:{line}: reference to {type.Name} '{name}' is not in this mod or any DefOf; make sure it exists in vanilla");
}

// ---------- C# references to our defs ----------
var srcFiles = Directory.GetFiles(Path.Combine(modRoot, "Source"), "*.cs", SearchOption.AllDirectories);
var src = string.Join("\n", srcFiles.Select(File.ReadAllText));
var allModDefNames = modDefs.Values.SelectMany(x => x).ToHashSet();
foreach (Match m in Regex.Matches(src, "\"(Council_[A-Za-z0-9_]+)\""))
    if (!m.Groups[1].Value.EndsWith("_") && !allModDefNames.Contains(m.Groups[1].Value)) errors.Add($"C#: defName \"{m.Groups[1].Value}\" is used but not defined in XML");
var tieKinds = new[] { "Friends", "Rivals", "Comrades", "Mentor" };
var dynamicDefs = tieKinds.Select(k => "Council_Tie_" + k)
    .Concat(tieKinds.Append("Student").SelectMany(k => new[] { "Council_Journal_TieFormed_" + k, "Council_Journal_TieEnded_" + k }))
    .Concat(new[] { "Council_Journal_TieGraduated_Mentor", "Council_Journal_TieGraduated_Student" });
foreach (var name in dynamicDefs)
    if (!allModDefNames.Contains(name)) errors.Add($"C#: dynamic defName {name} is not defined in XML");
var defOfType = mod.GetType("ColonyCouncil.CouncilDefOf");
foreach (var f in defOfType!.GetFields(BindingFlags.Public | BindingFlags.Static))
    if (!ModHas(f.FieldType, f.Name)) errors.Add($"CouncilDefOf.{f.Name} ({f.FieldType.Name}) has no matching def");

// ---------- Keyed translations ----------
// Every file in the Keyed folder, as the game loads them (one per feature area is fine).
var keyed = new Dictionary<string, string>();
foreach (var keyedFile in Directory.GetFiles(Path.Combine(modRoot, "Languages", "English", "Keyed"), "*.xml").OrderBy(f => f))
{
    var keyedDoc = XDocument.Load(keyedFile, LoadOptions.SetLineInfo);
    foreach (var el in keyedDoc.Root!.Elements())
    {
        if (!keyed.TryAdd(el.Name.LocalName, el.Value)) errors.Add($"Keyed: duplicate key {el.Name.LocalName} ({Path.GetFileName(keyedFile)})");
    }
}
var usedKeys = Regex.Matches(src, "\"(CC_[A-Za-z0-9_]+)\"").Select(m => m.Groups[1].Value).Where(k => !k.EndsWith("_")).ToHashSet();
foreach (var k in usedKeys)
    if (!keyed.ContainsKey(k)) errors.Add($"Keyed: missing key {k}");

var tones = new[] { "Plain", "Warm", "Blunt", "Cheerful", "Gloomy", "Cold" };
var dynamic = new Dictionary<string, string[]>
{
    ["CC_Tone_"] = tones,
    ["CC_Tie_"] = new[] { "Friends", "Rivals", "Comrades", "Mentor" },
    ["CC_Tie_Formed_"] = new[] { "Friends", "Rivals", "Comrades", "Mentor" },
    ["CC_Tie_Ended_"] = new[] { "Friends", "Rivals", "Comrades", "Mentor" },
    ["CC_TieKind_"] = new[] { "Friends", "Rivals", "Comrades", "Mentor" },
    ["CC_DreamKind_"] = new[] { "Sweet", "Strange", "Nightmare", "Omen", "Lost" },
    ["CC_Section_"] = new[] { "Colony", "People", "Council", "History", "Almanac" },
    ["CC_Tab_"] = new[] { "Priorities", "Build", "Roster", "Bonds", "Ambition", "Chronicle", "Dreams", "Journals", "Almanac", "Ways", "Moods", "World", "Overview", "Defenses" },
    ["CC_ChronicleFilter_"] = new[] { "All", "Wishes", "Council", "People", "Strangers", "World", "Dreams", "Losses" },
    ["CC_Nemesis_Deed_"] = new[] { "downed", "maimed", "killed" },
    ["CC_Way_"] = new[] { "Mercy_Pos", "Mercy_Neg", "Generosity_Pos", "Generosity_Neg", "Tradition_Pos", "Tradition_Neg", "Openness_Pos", "Openness_Neg", "Toil_Pos", "Toil_Neg" },
    ["CC_Ways_Meaning_"] = new[] { "Mercy_Pos", "Mercy_Neg", "Generosity_Pos", "Generosity_Neg", "Tradition_Pos", "Tradition_Neg", "Openness_Pos", "Openness_Neg", "Toil_Pos", "Toil_Neg" },
    ["CC_SkillNoun_"] = new[] { "Shooting", "Melee", "Construction", "Mining", "Cooking", "Plants", "Animals", "Crafting", "Artistic", "Medicine", "Social", "Intellectual" },
    ["CC_Slow_Level_"] = new[] { "1", "2", "1_Tip", "2_Tip" },
    ["CC_People_Sort_"] = new[] { "Name", "Trust", "Goal", "Mood" },
    ["CC_MsgLevel_"] = new[] { "Everything", "Notable", "LettersOnly" },
    ["CC_Era_"] = new[] { "Auto", "PreIndustrial", "Industrial", "Auto_Tip", "PreIndustrial_Tip", "Industrial_Tip" },
    ["CC_CoSignLine_"] = tones,
    ["CC_Heckle_"] = tones,
    ["CC_Touch_"] = tones,
    ["CC_TouchDone_"] = tones,
    ["CC_Level_"] = new[] { "Idea", "Low", "Medium", "High", "Critical" },
    ["CC_Cat_"] = new[] { "Shelter", "Food", "Temperature", "Defense", "Health", "Power", "Comfort", "Work", "Progress", "Idea" },
    ["CC_Stakes_Short_"] = new[] { "Gentle", "Demanding", "AdvisorOnly", "Balanced" },
    ["CC_Trust_"] = new[] { "Bitter", "Doubtful", "Neutral", "Trusting", "Devoted" },
    ["CC_Reason_Clothes_"] = new[] { "dead", "tattered", "naked" },
    ["CC_When_Passion_"] = new[] { "Crafting", "Artistic", "Cooking", "Intellectual", "Medicine", "Plants", "Animals" },
    ["CC_SkillSpace_"] = new[] { "Crafting", "Artistic", "Cooking", "Intellectual", "Medicine", "Plants", "Animals" },
    ["CC_Stock_"] = new[] { "medicine", "meals", "finemeals", "silver" },
    ["CC_DefKind_"] = new[] { "turret", "trap", "cover" },
    ["CC_ButtonPos_"] = new[] { "BeforeMenu", "AfterResearch", "AfterHistory", "AfterWork" },
    ["CC_BuildGroup_"] = new[] { "Living", "Food", "Work", "Care", "Leisure", "Security" },
    ["CC_AlmanacCat_"] = new[] { "Basics", "Food", "Rooms", "Mood", "Defense", "Defending", "Health", "Management", "LowTech", "RoomDesign", "Mistakes", "Royalty", "Ideology", "Biotech", "Anomaly", "Odyssey", "Numbers", "Mods" },
    ["CC_UI_Reaction_"] = new[] { "Love", "Like", "Dislike", "Hate" },
    // Feelings and strain (Heart/): enum names.
    ["CC_Heart_Band_"] = new[] { "Steady", "Strained", "Worn", "Breaking", "Steady_Desc", "Strained_Desc", "Worn_Desc", "Breaking_Desc" },
    ["CC_Heart_Source_"] = new[] { "Combat", "Pain", "Downed", "Loss", "Exhaustion", "Joyless", "BadSleep", "Dark", "Hunger", "LowMood", "Council", "Fear", "Grief", "Dread", "Despair", "Unmet" },
    ["CC_Heart_Relief_"] = new[] { "Quiet", "Sleep", "Recreation", "Gathering", "Friends", "Catharsis", "Episode", "Council" },
    ["CC_Heart_Need_"] = new[] { "Belonging", "Purpose", "Safety", "Freedom", "Belonging_Desc", "Purpose_Desc", "Safety_Desc", "Freedom_Desc",
        "Belonging_Answers", "Purpose_Answers", "Safety_Answers", "Freedom_Answers", "Belonging_Help", "Purpose_Help", "Safety_Help", "Freedom_Help" },
    ["CC_Heart_NeedLevel_"] = new[] { "Lacking", "Missed", "Fair", "Met" },
    // Lucky things (Quirks/LuckyItems.cs): how each past owner's time with it ended.
    ["CC_Lucky_Desc_"] = new[] { "Faded", "Died", "Gone" },
};
// Each feeling's name, adjective, description and effects (FeelingDef key).
foreach (var (file, doc) in docs)
    foreach (var el in doc.Root!.Elements().Where(e => e.Name.LocalName == "ColonyCouncil.FeelingDef"))
    {
        var key = el.Element("key")?.Value?.Trim();
        if (string.IsNullOrEmpty(key)) continue;
        foreach (var suffix in new[] { "", "_Adj", "_Desc", "_Does" })
            if (!keyed.ContainsKey("CC_Heart_F_" + key + suffix)) errors.Add($"{Rel(file)}:{Line(el)}: Keyed: missing feeling key CC_Heart_F_{key}{suffix}");
    }
// Roles: everything RolePredicate knows plus Goal_Build extras, and every role used in XML.
var roles = new HashSet<string> { "bed", "doublebed", "light", "heater", "cooler", "table", "chair", "endtable", "dresser", "joy", "research", "cook", "craft", "art", "animalbed", "hydroponics", "plantpot", "storage", "grave", "turret", "trap", "cover", "fermenter", "battery" };
foreach (var (file, doc) in docs)
    foreach (var e in doc.Descendants().Where(x => x.Name.LocalName == "thingRole"
                                                   || (x.Name.LocalName == "role" && x.Parent?.Parent?.Name.LocalName == "things")))
        roles.Add(e.Value.Trim());
dynamic["CC_Role_"] = roles.ToArray();
foreach (var (prefix, suffixes) in dynamic)
    foreach (var s in suffixes)
        if (!keyed.ContainsKey(prefix + s)) errors.Add($"Keyed: missing dynamic key {prefix + s}");

// Colony character tags (CharacterTracker.TagMap)
var wayTags = new HashSet<string> { "Merciful", "Harsh", "Generous", "Thrifty", "Traditional", "Restless", "Open", "Guarded", "Hardworking", "Easygoing" };
// ...and what a colony grows into (ColonyTraitDef's tag: GreenFingered, Hospitable, Devout...).
foreach (var (file, doc) in docs)
    foreach (var e in doc.Descendants().Where(x => x.Name.LocalName == "tag" && x.Parent?.Name.LocalName == "ColonyCouncil.ColonyTraitDef"))
        wayTags.Add(e.Value.Trim());
foreach (var (file, doc) in docs)
    foreach (var e in doc.Descendants().Where(x => x.Name.LocalName == "li" && x.Parent?.Name.LocalName is "colonyIs" or "colonyIsNot" or "drawnBy"))
        if (!wayTags.Contains(e.Value.Trim())) errors.Add($"{Rel(file)}:{Line(e)}: unknown colony way '{e.Value.Trim()}'");
// Way shifts stay small: one choice is a nudge, not a reputation.
foreach (var (file, doc) in docs)
    foreach (var e in doc.Descendants().Where(x => x.Parent?.Name.LocalName == "ways"))
        if (!float.TryParse(e.Value.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) || Math.Abs(v) > 20f)
            errors.Add($"{Rel(file)}:{Line(e)}: way shift '{e.Value.Trim()}' should be a number between -20 and 20");

// Placeholder sanity in Keyed text
var placeholder = new Regex(@"\{([^{}]*)\}");
foreach (var (k, text) in keyed)
{
    if (text.Count(c => c == '{') != text.Count(c => c == '}')) errors.Add($"Keyed {k}: unbalanced braces");
    foreach (Match m in placeholder.Matches(text))
    {
        var p = m.Groups[1].Value;
        if (!Regex.IsMatch(p, @"^(\d+|[A-Z]+(_[A-Za-z]+)?)$")) errors.Add($"Keyed {k}: odd placeholder {{{p}}}");
        else if (p.Contains('_') && !new[] { "nameDef", "nameFull", "labelShort", "pronoun", "possessive", "objective", "label", "definite", "indefinite", "gender" }.Contains(p.Split('_')[1]))
            errors.Add($"Keyed {k}: unknown pawn placeholder {{{p}}}");
    }
}

// Placeholders in def flavour text
var wishPh = new HashSet<string> { "SUBJECT", "THING", "THINGBARE", "TARGET", "WISH", "MINE", "AMBITION", "VENUE", "COUNT", "FACTION", "TITLE", "DECREE", "YEARS", "DILEMMA", "TRADITION", "OFFICE", "SKILL", "LOST", "DREAM", "GOAL", "SECRET", "LEAKER", "TELLER", "SPEECH", "ITEM", "QUIRK", "OLDGOAL", "NAME", "VICTIM", "DEED", "DAYS", "RELATION", "DEAD", "BEAST", "ENEMY", "TALE", "PLACE", "FOOD", "FOODS", "WORK", "ROOM", "ILLNESS", "THEIRILLNESS" };
// Camps: the faith a procession follows, the world event refugees fled (CampWords.Fill).
wishPh.UnionWith(new[] { "IDEO", "SITUATION" });
// Beliefs: the feast someone sat through with nothing fit to eat (Beliefs.NoteNothingFit).
wishPh.Add("GATHERING");
// Animals and PokéWorld (Animals/, Integrations/PokeWorldLife.cs): an animal's kind, a species, and an evolution's
// species before and after, and the level a Pokémon reached.
wishPh.UnionWith(new[] { "KIND", "SPECIES", "OLD", "NEW", "LEVEL" });
var pawnPrefixes = new HashSet<string> { "PAWN", "FOR", "OTHER", "ENVIED", "ABOUT", "A", "B", "PET", "LEAKER", "TELLER", "X", "SLAYER", "BABY", "THIRD" };
// The colonist a people remembers by name (World/KinMemory.cs: events, world events, camps).
pawnPrefixes.Add("FRIEND");
// Gossip and reputation (Social/Gossip/): the one a story is about, a deed's detail, and the date in a deed's name.
pawnPrefixes.Add("SUBJECT");
wishPh.UnionWith(new[] { "DETAIL", "DATE" });
var pawnSuffixes = new HashSet<string> { "nameDef", "nameFull", "labelShort", "pronoun", "possessive", "objective", "label", "definite", "indefinite", "gender" };
foreach (var (file, doc) in docs)
{
    foreach (var el in doc.Descendants())
    {
        if (el.HasElements) continue;
        var root = el.AncestorsAndSelf().Last(a => a.Parent == doc.Root);
        bool isAmbition = root.Name.LocalName.EndsWith("AmbitionDef");
        bool isRule = el.Parent?.Name.LocalName == "rulesStrings";
        if (isRule) continue;
        foreach (Match m in placeholder.Matches(el.Value))
        {
            var p = m.Groups[1].Value;
            bool ok;
            if (p.Contains('_'))
            {
                var parts = p.Split('_');
                ok = parts.Length == 2 && pawnPrefixes.Contains(parts[0]) && pawnSuffixes.Contains(parts[1]) && (!isAmbition || parts[0] == "PAWN");
            }
            else ok = !isAmbition && wishPh.Contains(p);
            if (!ok) errors.Add($"{Rel(file)}:{Line(el)}: unexpected placeholder {{{p}}}");
        }
    }
}

// ---------- Textures ----------
foreach (var (file, doc) in docs)
{
    foreach (var el in doc.Descendants().Where(x => x.Name.LocalName is "symbol" or "iconPath"))
    {
        var tex = Path.Combine(modRoot, "Textures", el.Value.Trim() + ".png");
        if (!File.Exists(tex)) errors.Add($"{Rel(file)}:{Line(el)}: texture {el.Value} not found in Textures/");
    }
}

// Interface icons, loaded by name in UI/UiKit.cs (Icons.Load); made by Tools/make_icons.py.
foreach (Match m in Regex.Matches(src, "\\bLoad\\(\"([A-Za-z0-9]+)\"\\)"))
{
    var tex = Path.Combine(modRoot, "Textures", "ColonyCouncil", "Icons", m.Groups[1].Value + ".png");
    if (!File.Exists(tex)) errors.Add($"C#: icon {m.Groups[1].Value} not found in Textures/ColonyCouncil/Icons/ (run Tools/make_icons.py)");
}

// ---------- Report ----------
foreach (var w in warnings.Distinct()) Console.WriteLine("WARN  " + w);
foreach (var e in errors.Distinct()) Console.WriteLine("ERROR " + e);
Console.WriteLine($"\n{docs.Count} def files, {modDefs.Values.Sum(s => s.Count)} defs, {keyed.Count} keys. {errors.Distinct().Count()} error(s), {warnings.Distinct().Count()} warning(s).");
return errors.Count == 0 ? 0 : 1;
