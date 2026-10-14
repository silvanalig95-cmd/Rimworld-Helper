// Harmony patch targets, read from the mod's [HarmonyPatch] attributes and checked the way Harmony finds them:
// the method must be declared on the named type, one overload only unless argument types are given, and every
// parameter of a prefix, postfix or finalizer must name a parameter of the target (or be one of Harmony's own).

using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;

namespace ApiDiff;

sealed class PatchTarget
{
    public string PatchClass, Type, Method, TypeNameString;
    public int MethodType; // HarmonyLib.MethodType: 0 normal, 1 getter, 2 setter, 3 constructor, 4 static constructor, 5 enumerator, 6 async
    public string[] ArgTypes;
    public int[] Variations;
    public bool Dynamic; // TargetMethod(s): found by code at run time
    public readonly List<(string role, string name, string[] paramNames, string[] paramTypes)> PatchMethods = new();

    public string Display
    {
        get
        {
            if (Dynamic) return PatchClass + ": TargetMethod() (chosen at run time, not checked)";
            string m = MethodType switch
            {
                1 => "get_" + Method,
                2 => "set_" + Method,
                3 => ".ctor",
                4 => ".cctor",
                _ => Method
            };
            var args = ArgTypes == null ? "" : "(" + string.Join(", ", ArgTypesWithVariations()) + ")";
            return $"{Type}.{m}{args}";
        }
    }

    public string[] ArgTypesWithVariations()
    {
        if (ArgTypes == null) return null;
        var r = new string[ArgTypes.Length];
        for (int i = 0; i < r.Length; i++)
        {
            int v = Variations != null && i < Variations.Length ? Variations[i] : 0;
            r[i] = ArgTypes[i] + (v is 1 or 2 ? "&" : v == 3 ? "*" : "");
        }
        return r;
    }
}

static class Patches
{
    private static string AttrType(MetadataReader md, CustomAttribute ca)
    {
        EntityHandle parent = ca.Constructor.Kind switch
        {
            HandleKind.MemberReference => md.GetMemberReference((MemberReferenceHandle)ca.Constructor).Parent,
            HandleKind.MethodDefinition => md.GetMethodDefinition((MethodDefinitionHandle)ca.Constructor).GetDeclaringType(),
            _ => default
        };
        return parent.IsNil ? null : Meta.TypeName(md, parent);
    }

    /// <summary>Fills in what one [HarmonyPatch(...)] says.</summary>
    private static void Apply(MetadataReader md, CustomAttribute ca, PatchTarget t)
    {
        CustomAttributeValue<string> v;
        try { v = ca.DecodeValue(Names.I); }
        catch { return; }
        var strings = v.FixedArguments.Where(a => a.Type == "System.String").Select(a => (string)a.Value).ToList();
        if (strings.Count == 2) { t.TypeNameString = strings[0]; t.Type = strings[0]; t.Method = strings[1]; }
        else if (strings.Count == 1) t.Method = strings[0];
        foreach (var a in v.FixedArguments)
        {
            switch (a.Type)
            {
                case "System.Type": t.Type = (string)a.Value; break;
                case "HarmonyLib.MethodType": t.MethodType = Convert.ToInt32(a.Value); break;
                case "System.Type[]":
                    t.ArgTypes = a.Value is ImmutableArray<CustomAttributeTypedArgument<string>> arr ? arr.Select(x => (string)x.Value).ToArray() : Array.Empty<string>();
                    break;
                case "HarmonyLib.ArgumentType[]":
                    t.Variations = a.Value is ImmutableArray<CustomAttributeTypedArgument<string>> va ? va.Select(x => Convert.ToInt32(x.Value)).ToArray() : null;
                    break;
            }
        }
        foreach (var n in v.NamedArguments)
        {
            switch (n.Name)
            {
                case "methodName": t.Method = (string)n.Value; break;
                case "methodType": t.MethodType = Convert.ToInt32(n.Value); break;
            }
        }
    }

    private static readonly Dictionary<string, string> RoleAttributes = new()
    {
        ["HarmonyLib.HarmonyPrefix"] = "Prefix",
        ["HarmonyLib.HarmonyPostfix"] = "Postfix",
        ["HarmonyLib.HarmonyTranspiler"] = "Transpiler",
        ["HarmonyLib.HarmonyFinalizer"] = "Finalizer",
    };

    public static List<PatchTarget> Read(Asm mod)
    {
        var md = mod.Md;
        var result = new List<PatchTarget>();
        foreach (var th in md.TypeDefinitions)
        {
            var td = md.GetTypeDefinition(th);
            var attrs = td.GetCustomAttributes().Select(md.GetCustomAttribute).Where(ca => AttrType(md, ca) == "HarmonyLib.HarmonyPatch").ToList();
            if (attrs.Count == 0) continue;
            var t = new PatchTarget { PatchClass = Meta.FullName(md, th) };
            foreach (var ca in attrs) Apply(md, ca, t);
            foreach (var mh in td.GetMethods())
            {
                var m = md.GetMethodDefinition(mh);
                var name = md.GetString(m.Name);
                string role = name is "Prefix" or "Postfix" or "Transpiler" or "Finalizer" ? name : null;
                foreach (var ca in m.GetCustomAttributes().Select(md.GetCustomAttribute))
                {
                    var at = AttrType(md, ca);
                    if (at != null && RoleAttributes.TryGetValue(at, out var r)) role = r;
                    if (at == "HarmonyLib.HarmonyPatch") Apply(md, ca, t);
                }
                if (name is "TargetMethod" or "TargetMethods") t.Dynamic = true;
                if (role == null) continue;
                var sig = m.DecodeSignature(Names.I, null);
                var pnames = new string[sig.ParameterTypes.Length];
                foreach (var ph in m.GetParameters())
                {
                    var p = md.GetParameter(ph);
                    if (p.SequenceNumber > 0 && p.SequenceNumber <= pnames.Length) pnames[p.SequenceNumber - 1] = md.GetString(p.Name);
                }
                t.PatchMethods.Add((role, name, pnames, sig.ParameterTypes.ToArray()));
            }
            result.Add(t);
        }
        return result;
    }

    /// <summary>Finds the patch's target in a set of assemblies; problems (empty when fine) and the target found.</summary>
    public static (List<string> problems, MethodRec target) Check(PatchTarget p, AsmSet set)
    {
        var problems = new List<string>();
        if (p.Dynamic) return (problems, null);
        if (p.Type == null) { problems.Add("no target type in the attribute"); return (problems, null); }
        var type = set.Find(p.Type).FirstOrDefault();
        if (type == null) { problems.Add($"type {p.Type} not found"); return (problems, null); }
        string name = p.MethodType switch
        {
            1 => "get_" + p.Method,
            2 => "set_" + p.Method,
            3 => ".ctor",
            4 => ".cctor",
            _ => p.Method
        };
        if (name == null) { problems.Add("no method name in the attribute"); return (problems, null); }
        var declared = type.Methods().Where(m => m.Name == name).ToList();
        if (p.MethodType == 4) declared = declared.Where(m => m.IsStatic).ToList();
        var args = p.ArgTypesWithVariations();
        if (args == null && p.MethodType == 3) args = Array.Empty<string>(); // Harmony: no argument types means the parameterless constructor
        var matches = args == null ? declared : declared.Where(m => m.ParamTypes.SequenceEqual(args)).ToList();
        if (matches.Count == 0)
        {
            if (declared.Count > 0)
                problems.Add($"{name} has no overload ({string.Join(", ", args ?? Array.Empty<string>())}); it has: " + string.Join("; ", declared.Select(m => "(" + string.Join(", ", m.ParamTypes) + ")")));
            else
            {
                var inherited = set.Chain(type.FullName).Skip(1).SelectMany(c => c.type.Methods()).FirstOrDefault(m => m.Name == name);
                problems.Add(inherited != null
                    ? $"{name} is not declared on {type.FullName} (only inherited from {inherited.Owner.FullName}); Harmony needs the declaring type"
                    : $"{name} not found on {type.FullName}");
            }
            return (problems, null);
        }
        if (matches.Count > 1) { problems.Add($"{name} has {matches.Count} overloads and the patch names no argument types (ambiguous)"); return (problems, null); }
        var target = matches[0];

        // The patch methods' parameters, as Harmony injects them.
        foreach (var (role, mname, pnames, ptypes) in p.PatchMethods)
        {
            if (role == "Transpiler") continue;
            for (int i = 0; i < pnames.Length; i++)
            {
                var pn = pnames[i];
                if (pn == null) continue;
                var pt = ptypes[i].TrimEnd('&');
                switch (pn)
                {
                    case "__instance":
                        if (target.IsStatic) problems.Add($"{mname}: __instance, but the target is static");
                        continue;
                    case "__result":
                    case "__resultRef":
                        if (target.ReturnType == "System.Void" || target.Name == ".ctor") problems.Add($"{mname}: {pn}, but the target returns nothing");
                        else if (pt != target.ReturnType.TrimEnd('&') && pt != "System.Object") problems.Add($"{mname}: {pn} is {pt}, the target returns {target.ReturnType}");
                        continue;
                    case "__state": case "__args": case "__originalMethod": case "__runOriginal": case "__exception":
                        continue;
                }
                if (pn.StartsWith("___"))
                {
                    var field = pn.Substring(3);
                    if (!set.Chain(type.FullName).Any(c => c.type.Fields().Any(f => f.Name == field)))
                        problems.Add($"{mname}: {pn}, but {type.FullName} has no field '{field}'");
                    continue;
                }
                if (pn.StartsWith("__") && int.TryParse(pn.Substring(2), out var idx))
                {
                    if (idx >= target.ParamTypes.Length) problems.Add($"{mname}: {pn}, but the target has {target.ParamTypes.Length} parameters");
                    continue;
                }
                int at = Array.IndexOf(target.ParamNames, pn);
                if (at < 0)
                {
                    problems.Add($"{mname}: parameter '{pn}' is not a parameter of the target ({string.Join(", ", target.ParamNames)})");
                    continue;
                }
                var tt = target.ParamTypes[at].TrimEnd('&');
                if (pt != tt && pt != "System.Object") problems.Add($"{mname}: parameter '{pn}' is {pt}, the target's is {tt}");
            }
        }
        return (problems, target);
    }
}
