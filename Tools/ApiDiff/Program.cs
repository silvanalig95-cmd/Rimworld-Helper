// ApiDiff: will a mod DLL built against one game version still run on another?
//
// The compiler only checks a build against the references it's given. A DLL built for 1.6 and loaded by 1.7 binds
// every call, field and override by exact name and signature, and fails at run time (MissingMethodException,
// MissingFieldException, TypeLoadException, a Harmony patch that can't find its method) if 1.7 changed any of them.
// This reads the built DLL and checks each of those against the old and the new reference assemblies:
//
//   1. every outside type the DLL uses, and the constants and enum values on them (the DLL holds the numbers);
//   2. every outside method and field it calls or reads (name, static or instance, return and parameter types);
//   3. every override of an outside method, every outside abstract member and interface member it must implement;
//   4. every Harmony patch target ([HarmonyPatch] attributes) and the names of the patch methods' parameters;
//   5. every member looked up by name in code (AccessTools.Method/Field/FieldRefAccess, Type.GetField...).
//
// Usage (from the repository root):
//   dotnet run -c Release --project Tools/ApiDiff -- <mod.dll> <old refs> <new refs> [--all]
// where each "refs" is a folder of reference assemblies, or a version of the Krafs.Rimworld.Ref package in the local
// NuGet cache (e.g. 1.6.4871 or 1.7.4977-alpha.2; restore it once by building the mod with -p:RimRef=...).
// Harmony's reference assembly and, for a game on .NET (Core), the matching runtime reference pack are added
// automatically. --all also lists every patch target and lookup that is fine. Exit code 1 if anything breaks.
//
// What it can't see: method bodies (what a transpiler expects to find inside a game method), other mods' types, and
// def names in the game's XML (those are data, not in the reference assemblies).

using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using ApiDiff;

var positional = args.Where(a => !a.StartsWith("--")).ToList();
bool all = args.Contains("--all");
if (positional.Count < 3)
{
    Console.Error.WriteLine("Usage: ApiDiff <mod.dll> <old refs folder or Krafs.Rimworld.Ref version> <new refs folder or version> [--all]");
    return 2;
}

var modPath = Path.GetFullPath(positional[0]);
var nuget = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");

string RefFolder(string arg)
{
    if (Directory.Exists(arg)) return Path.GetFullPath(arg);
    var pkg = Path.Combine(nuget, "krafs.rimworld.ref", arg, "ref");
    if (Directory.Exists(pkg))
    {
        var tfm = Directory.GetDirectories(pkg).OrderBy(d => d).LastOrDefault();
        if (tfm != null) return tfm;
    }
    throw new DirectoryNotFoundException($"No reference assemblies for '{arg}' (not a folder, and not in {Path.Combine(nuget, "krafs.rimworld.ref")}).");
}

IEnumerable<string> SetPaths(string folder)
{
    var dlls = Directory.GetFiles(folder, "*.dll").ToList();
    var names = dlls.Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
    // Harmony (the mod's patches reference it).
    if (!names.Contains("0Harmony"))
    {
        var hdir = Path.Combine(nuget, "lib.harmony.ref");
        var h = Directory.Exists(hdir) ? Directory.GetFiles(hdir, "0Harmony.dll", SearchOption.AllDirectories).OrderBy(f => f).LastOrDefault() : null;
        if (h != null) dlls.Add(h);
    }
    // The runtime's own assemblies, when the game runs on .NET (Core) and the folder doesn't carry them.
    if (!names.Contains("mscorlib") && !names.Contains("System.Runtime") && !names.Contains("System.Private.CoreLib"))
    {
        int major = 0;
        foreach (var d in dlls)
        {
            var a = Asm.TryOpen(d);
            if (a == null) continue;
            foreach (var rh in a.Md.AssemblyReferences)
            {
                var r = a.Md.GetAssemblyReference(rh);
                if (a.Md.GetString(r.Name) == "System.Runtime") major = Math.Max(major, r.Version.Major);
            }
        }
        if (major >= 5)
        {
            var root = Environment.GetEnvironmentVariable("DOTNET_ROOT")
                       ?? Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));
            var packs = Path.Combine(root, "packs", "Microsoft.NETCore.App.Ref");
            var pack = Directory.Exists(packs)
                ? Directory.GetDirectories(packs, major + ".*").OrderBy(d => Version.TryParse(Path.GetFileName(d).Split('-')[0], out var v) ? v : new Version()).LastOrDefault()
                : null;
            var refDir = pack == null ? null : Path.Combine(pack, "ref", "net" + major + ".0");
            if (refDir != null && Directory.Exists(refDir)) dlls.AddRange(Directory.GetFiles(refDir, "*.dll"));
            else Console.Error.WriteLine($"Warning: the .NET {major} reference pack wasn't found under {packs}; runtime types will show as missing. Install the .NET {major} SDK.");
        }
    }
    return dlls;
}

var oldDir = RefFolder(positional[1]);
var newDir = RefFolder(positional[2]);
var oldSet = new AsmSet(positional[1], SetPaths(oldDir));
var newSet = new AsmSet(positional[2], SetPaths(newDir));
var mod = new Asm(modPath);
var modSet = new AsmSet("mod", new[] { modPath });
var scan = new ModScan(mod);

Console.WriteLine($"ApiDiff: {Path.GetFileName(modPath)}, from {oldSet.Label} ({oldDir}) to {newSet.Label} ({newDir})");
Console.WriteLine();

string Used(SortedSet<string> where) =>
    where.Count == 0 ? "" : "\n        used in: " + string.Join(", ", where.Take(4)) + (where.Count > 4 ? $" and {where.Count - 4} more" : "");

// ---------- 1. Types ----------
var typeProblems = new List<string>();
int typesChecked = 0, typesUnresolved = 0;
foreach (var t in scan.Types.Values.OrderBy(t => t.Name))
{
    if (!oldSet.Has(t.Name)) { typesUnresolved++; continue; }
    typesChecked++;
    if (!newSet.Has(t.Name)) { typeProblems.Add($"  {t.Name} [{t.Scope}]: missing"); continue; }
    var ov = oldSet.IsValueType(t.Name);
    var nv = newSet.IsValueType(t.Name);
    if (ov != null && nv != null && ov != nv) typeProblems.Add($"  {t.Name} [{t.Scope}]: was a {(ov.Value ? "struct" : "class")}, now a {(nv.Value ? "struct" : "class")}");
    var oldVisible = oldSet.Find(t.Name).Any(x => x.IsVisible);
    if (oldVisible && !newSet.Find(t.Name).Any(x => x.IsVisible)) typeProblems.Add($"  {t.Name} [{t.Scope}]: no longer public");
}

// ---------- 1b. Constants and enum values ----------
// The DLL holds the numbers it compiled with (TextAnchor.MiddleRight is just 5 in it); if the new version gives a
// name another value, the DLL silently means something else. Checked for every type the DLL names.
var constProblems = new List<string>();
var constNotes = new List<string>();
int constsChecked = 0;
foreach (var t in scan.Types.Values.OrderBy(t => t.Name))
{
    var o = oldSet.Find(t.Name).FirstOrDefault();
    var n = newSet.Find(t.Name).FirstOrDefault();
    if (o == null || n == null) continue;
    var ol = o.Literals();
    if (ol.Count == 0) continue;
    var nl = n.Literals();
    bool isEnum = o.BaseName() == "System.Enum";
    scan.ValuesPassed.TryGetValue(t.Name, out var passed);
    var newNames = nl.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => string.Join("/", g.Select(kv => kv.Key)));
    foreach (var (name, value) in ol)
    {
        constsChecked++;
        bool used = passed != null && passed.Contains(value);
        string usedNote = used ? " (the DLL passes this value)" : "";
        if (!nl.TryGetValue(name, out var nv))
        {
            // A removed name whose number still means the same thing elsewhere is harmless; otherwise it matters if used.
            var line = $"  {t.Name}.{name} = {value} [{t.Scope}]: removed{(newNames.TryGetValue(value, out var other) ? $" (the value is now {other})" : "")}{usedNote}";
            if (used && !newNames.ContainsKey(value)) constProblems.Add(line);
            else constNotes.Add(line);
        }
        else if (nv != value)
        {
            var line = $"  {t.Name}.{name} [{t.Scope}]: was {value}, now {nv}{usedNote}";
            // An enum member the DLL never passes is listed, not counted; a changed const always counts.
            if (!isEnum || used) constProblems.Add(line);
            else constNotes.Add(line);
        }
    }
}

// ---------- 2. Members ----------
(object found, bool isPublic, string hint) FindMember(AsmSet set, MemberUse use)
{
    var chain = set.Chain(use.Parent).ToList();
    if (chain.Count == 0) return (null, false, "type missing");
    if (use.Name is ".ctor" or ".cctor") chain = chain.Take(1).ToList();
    foreach (var (type, ctx) in chain)
    {
        if (use.IsField)
        {
            var f = type.Fields().FirstOrDefault(f => f.Name == use.Name && (f.Type == use.FieldType || f.Type.Contains('!')));
            if (f != null) return (f, f.IsPublic && type.IsVisible, null);
        }
        else
        {
            var m = type.Methods(ctx).FirstOrDefault(m => m.Name == use.Name && m.Sig == use.Sig);
            if (m != null) return (m, (m.IsPublic || m.IsFamily) && type.IsVisible, null);
        }
    }
    // Not there: say what is.
    if (use.IsField)
    {
        var f = chain.SelectMany(c => c.type.Fields()).FirstOrDefault(f => f.Name == use.Name);
        if (f != null) return (null, false, $"now of type {f.Type}");
        if (chain.Any(c => c.type.Properties().Contains(use.Name))) return (null, false, "now a property");
        return (null, false, "removed");
    }
    var same = chain.SelectMany(c => c.type.Methods(c.ctx)).Where(m => m.Name == use.Name).Select(m => m.Sig).Distinct().ToList();
    if (same.Count > 0) return (null, false, "signature changed; now: " + string.Join(" | ", same));
    if (use.Name.StartsWith("get_") || use.Name.StartsWith("set_"))
    {
        var prop = use.Name.Substring(4);
        if (chain.Any(c => c.type.Fields().Any(f => f.Name == prop))) return (null, false, "the property is now a field");
    }
    return (null, false, "removed");
}

var memberProblems = new List<string>();
int membersChecked = 0, membersUnresolved = 0;
var checkedByScope = new SortedDictionary<string, int>();
foreach (var use in scan.Members.Values.OrderBy(u => u.Parent).ThenBy(u => u.Name))
{
    var (oldFound, oldPublic, _) = FindMember(oldSet, use);
    if (oldFound == null) { membersUnresolved++; continue; }
    membersChecked++;
    checkedByScope[use.Scope] = checkedByScope.GetValueOrDefault(use.Scope) + 1;
    var (newFound, newPublic, hint) = FindMember(newSet, use);
    if (newFound == null) memberProblems.Add($"  {use.Display} [{use.Scope}]: {hint}{Used(use.UsedIn)}");
    else if (oldPublic && !newPublic) memberProblems.Add($"  {use.Display} [{use.Scope}]: no longer public{Used(use.UsedIn)}");
}

// ---------- 3. Overrides, abstract members, interfaces ----------
IEnumerable<(TypeRec type, string[] ctx, bool isMod)> MixedChain(AsmSet set, string name)
{
    var seen = new HashSet<string>();
    var cur = name;
    while (cur != null && seen.Add(cur))
    {
        var (open, targs) = Meta.SplitGeneric(cur);
        var t = modSet.Find(open).FirstOrDefault();
        bool isMod = t != null;
        t ??= set.Find(open).FirstOrDefault();
        if (t == null) yield break;
        var ctx = targs.Length > 0 ? targs : null;
        yield return (t, ctx, isMod);
        cur = t.BaseName(ctx);
    }
}

// What the mod's types need from the outside to load: (type, problem) pairs for one set.
int typesWithOutsideBase = 0, overridesChecked = 0;
List<(string type, string problem)> InheritanceProblems(AsmSet set)
{
    typesWithOutsideBase = overridesChecked = 0;
    var result = new List<(string, string)>();
    foreach (var th in mod.Md.TypeDefinitions)
    {
        var name = Meta.FullName(mod.Md, th);
        var self = modSet.Find(name).FirstOrDefault();
        if (self == null || self.IsInterface || self.BaseName() == null) continue;
        var chain = MixedChain(set, name).ToList();
        if (chain.Count < 2 || chain.Skip(1).All(c => c.isMod)) continue;
        bool chainComplete = chain[^1].type.FullName == "System.Object" || chain[^1].type.BaseName() == null;
        typesWithOutsideBase++;

        // Overrides: each must still find a virtual method to override.
        foreach (var m in self.Methods().Where(m => m.IsVirtual && !m.IsNewSlot && !m.IsStatic))
        {
            overridesChecked++;
            bool found = chain.Skip(1).Any(c => c.type.Methods(c.ctx).Any(b => b.IsVirtual && b.Name == m.Name && b.Sig == m.Sig));
            if (!found && chainComplete) result.Add((name, $"{m.Sig} overrides nothing any more (the game won't call it)"));
        }
        if (self.IsAbstract || !chainComplete) continue;

        // Abstract members of outside base classes must be implemented below them.
        var implemented = new HashSet<string>();
        foreach (var (type, ctx, isMod) in chain)
        {
            var ms = type.Methods(ctx);
            if (!isMod)
                foreach (var a in ms.Where(a => a.IsAbstract && !implemented.Contains(a.Sig)))
                    result.Add((name, $"doesn't implement abstract {type.FullName}::{a.Sig} (TypeLoadException)"));
            foreach (var m in ms.Where(m => m.IsVirtual && !m.IsAbstract)) implemented.Add(m.Sig);
        }

        // Members of outside interfaces the mod's types say they implement.
        var explicitImpls = new HashSet<string>();
        var ifaces = new List<string>();
        foreach (var (type, ctx, isMod) in chain.Where(c => c.isMod))
        {
            ifaces.AddRange(type.InterfaceNames(ctx));
            foreach (var mih in type.Def.GetMethodImplementations())
            {
                var mi = mod.Md.GetMethodImplementation(mih);
                if (mi.MethodDeclaration.Kind != HandleKind.MemberReference) continue;
                var mr = mod.Md.GetMemberReference((MemberReferenceHandle)mi.MethodDeclaration);
                try { explicitImpls.Add(Meta.Sig(mod.Md.GetString(mr.Name), mr.DecodeMethodSignature(Names.I, null))); } catch { }
            }
        }
        var allPublic = chain.SelectMany(c => c.type.Methods(c.ctx)).Where(m => m.IsVirtual && !m.IsAbstract && m.IsPublic).Select(m => m.Sig).ToHashSet();
        var seenIfaces = new HashSet<string>();
        while (ifaces.Count > 0)
        {
            var iname = ifaces[^1];
            ifaces.RemoveAt(ifaces.Count - 1);
            if (!seenIfaces.Add(iname)) continue;
            var (open, targs) = Meta.SplitGeneric(iname);
            if (modSet.Has(open)) continue;
            var itype = set.Find(open).FirstOrDefault();
            if (itype == null) continue;
            var ictx = targs.Length > 0 ? targs : null;
            ifaces.AddRange(itype.InterfaceNames(ictx));
            var openSigs = itype.Methods();
            var closed = itype.Methods(ictx);
            for (int i = 0; i < closed.Count; i++)
            {
                var im = closed[i];
                if (!im.IsAbstract || im.IsStatic) continue;
                if (allPublic.Contains(im.Sig) || explicitImpls.Contains(openSigs[i].Sig)) continue;
                result.Add((name, $"doesn't implement {iname}::{im.Sig} (TypeLoadException)"));
            }
        }
    }
    return result;
}

var oldInherit = InheritanceProblems(oldSet).ToHashSet();
var inheritProblems = InheritanceProblems(newSet).Where(p => !oldInherit.Contains(p)).Select(p => $"  {p.type}: {p.problem}").Distinct().ToList();

// ---------- 4. Harmony patches ----------
var patches = Patches.Read(mod);
var patchLines = new List<string>();
int patchBreaks = 0;
foreach (var p in patches.OrderBy(p => p.PatchClass))
{
    if (p.Dynamic) { patchLines.Add($"  ?    {p.Display}"); continue; }
    var (oldProblems, _) = Patches.Check(p, oldSet);
    var (newProblems, _) = Patches.Check(p, newSet);
    bool onlyOld = oldProblems.Count > 0 && newProblems.Count == 0;
    string mark = newProblems.Count == 0 ? (onlyOld ? "OK*" : "OK") : "FAIL";
    if (newProblems.Count > 0) patchBreaks++;
    if (newProblems.Count > 0 || oldProblems.Count > 0 || all)
    {
        patchLines.Add($"  {mark,-4} {p.PatchClass} -> {p.Display}");
        foreach (var x in newProblems) patchLines.Add($"         {newSet.Label}: {x}");
        foreach (var x in oldProblems) patchLines.Add($"         {oldSet.Label}: {x}");
    }
}

// ---------- 5. Lookups by name ----------
string LookupStatus(AsmSet set, Lookup l)
{
    if (l.Type == null) return null;
    var chain = set.Chain(l.Type).ToList();
    if (chain.Count == 0) return "type not found";
    switch (l.Kind)
    {
        case "type":
            return null;
        case "nested type":
            return set.Has(l.Type + "+" + l.Name) ? null : "nested type not found";
        case "field":
        {
            var f = chain.SelectMany(c => c.type.Fields()).FirstOrDefault(f => f.Name == l.Name);
            if (f == null) return "field not found";
            if (l.ValueType != null && f.Type != l.ValueType && !f.Type.Contains('!')) return $"field is {f.Type}, not {l.ValueType}";
            return null;
        }
        case "property":
            return chain.Any(c => c.type.Properties().Contains(l.Name)) ? null : "property not found";
        case "constructor":
        {
            var ctors = chain[0].type.Methods().Where(m => m.Name == ".ctor").ToList();
            if (l.ArgTypes != null && !ctors.Any(m => m.ParamTypes.SequenceEqual(l.ArgTypes))) return "no constructor (" + string.Join(", ", l.ArgTypes) + ")";
            return ctors.Count > 0 ? null : "no constructor";
        }
        case "method":
        {
            var ms = chain.SelectMany(c => c.type.Methods(c.ctx)).Where(m => m.Name == l.Name).ToList();
            if (ms.Count == 0) return "method not found";
            if (l.ArgTypes != null && !ms.Any(m => m.ParamTypes.SequenceEqual(l.ArgTypes))) return "no overload (" + string.Join(", ", l.ArgTypes) + "); has: " + string.Join("; ", ms.Select(m => "(" + string.Join(", ", m.ParamTypes) + ")"));
            return null;
        }
        default:
        {
            bool any = chain.Any(c => c.type.Fields().Any(f => f.Name == l.Name) || c.type.Properties().Contains(l.Name) || c.type.Methods().Any(m => m.Name == l.Name));
            return any ? null : "member not found";
        }
    }
}

var lookupLines = new List<string>();
int lookupBreaks = 0, lookupsChecked = 0, lookupsSkipped = 0;
foreach (var g in scan.Lookups.GroupBy(l => (l.Api, l.Kind, l.Type, l.Name, Args: l.ArgTypes == null ? null : string.Join(",", l.ArgTypes))).OrderBy(g => g.Key.Type).ThenBy(g => g.Key.Name))
{
    var l = g.First();
    var where = string.Join(", ", g.Select(x => x.In).Distinct().Take(3));
    var what = l.Kind == "type" ? $"type {l.Type}" : $"{l.Kind} {l.Type ?? "(a type known at run time)"}{(l.Name != null && l.Name != ".ctor" ? "." + l.Name : "")}{(l.ArgTypes != null ? "(" + string.Join(", ", l.ArgTypes) + ")" : "")}";
    if (l.Type == null || (!oldSet.Has(l.Type) && !newSet.Has(l.Type)))
    {
        lookupsSkipped++;
        var why = l.Type != null && modSet.Has(l.Type) ? "the mod's own type" : "another mod's type or one known at run time";
        if (all) lookupLines.Add($"  -    {l.Api}: {what}  ({why}; not checked)\n         in {where}");
        continue;
    }
    lookupsChecked++;
    var o = LookupStatus(oldSet, l);
    var n = LookupStatus(newSet, l);
    if (n != null) lookupBreaks++;
    if (n != null || o != null || all)
    {
        lookupLines.Add($"  {(n == null ? "OK" : "FAIL"),-4} {l.Api}: {what}\n         in {where}");
        if (n != null) lookupLines.Add($"         {newSet.Label}: {n}");
        if (o != null) lookupLines.Add($"         {oldSet.Label}: {o}");
    }
}

// ---------- Report ----------
void Section(string title, List<string> lines, string none)
{
    Console.WriteLine("== " + title);
    if (lines.Count == 0) Console.WriteLine("  " + none);
    foreach (var l in lines) Console.WriteLine(l);
    Console.WriteLine();
}

Section($"Types the DLL uses that {newSet.Label} lacks or changed ({typesChecked} checked)", typeProblems, "none");
Section($"Constants and enum values that differ in {newSet.Label} ({constsChecked} checked on the types the DLL names)", constProblems, "none");
if (constNotes.Count > 0 && (all || constNotes.Count <= 40))
    Section("Constants and enum members that differ but the DLL doesn't seem to use (it can only be seen where a value is passed or stored)", constNotes, "none");
else if (constNotes.Count > 0)
    Console.WriteLine($"({constNotes.Count} other constants differ but the DLL doesn't seem to use them; --all lists them.)\n");
Section($"Methods and fields the DLL uses that {newSet.Label} lacks or changed ({membersChecked} checked: "
        + string.Join(", ", checkedByScope.Select(kv => $"{kv.Key} {kv.Value}")) + ")", memberProblems, "none");
Section($"Overrides and implementations that break on {newSet.Label} ({overridesChecked} overrides in {typesWithOutsideBase} types checked)", inheritProblems, "none");
Section($"Harmony patches ({patches.Count}; OK* = fails on {oldSet.Label} but works on {newSet.Label})", patchLines, all ? "none" : "all targets found on both");
Section($"Members looked up by name ({lookupsChecked} on game or runtime types; {lookupsSkipped} on other mods' or the mod's own types, not checked)", lookupLines, all ? "none" : "all found on both");

var breaks = typeProblems.Count + constProblems.Count + memberProblems.Count + inheritProblems.Count + patchBreaks + lookupBreaks;
if (typesUnresolved + membersUnresolved > 0)
    Console.WriteLine($"({typesUnresolved} types and {membersUnresolved} members weren't found in {oldSet.Label} either, so they weren't compared: other mods' types or a reference folder that's incomplete.)");
Console.WriteLine(breaks == 0
    ? $"Nothing the DLL uses has changed in {newSet.Label}."
    : $"{breaks} thing(s) the DLL uses changed in {newSet.Label}.");
Console.WriteLine("Not checked: method bodies (what a transpiler expects to find inside a game method) and other mods' types.");
return breaks == 0 ? 0 : 1;
