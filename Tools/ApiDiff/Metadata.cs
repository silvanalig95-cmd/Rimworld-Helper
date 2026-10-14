// Reading assemblies without loading them: type names, member signatures and a by-name index of a whole set of
// reference assemblies (a game version plus its runtime). Types are matched by full name, not by assembly, because
// a game version may move a type to another assembly (1.7 moved UnityEngine.* into UnityStubs) and the game's loader
// still finds it by name.

using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ApiDiff;

/// <summary>Turns signatures into plain strings like "System.Collections.Generic.List`1&lt;Verse.Pawn&gt;".</summary>
/// <remarks>The generic context is the type arguments to put in place of !0, !1... (null keeps them as !0).</remarks>
sealed class Names : ISignatureTypeProvider<string, string[]>, ICustomAttributeTypeProvider<string>
{
    public static readonly Names I = new();

    public string GetPrimitiveType(PrimitiveTypeCode code) => code switch
    {
        PrimitiveTypeCode.Boolean => "System.Boolean",
        PrimitiveTypeCode.Byte => "System.Byte",
        PrimitiveTypeCode.SByte => "System.SByte",
        PrimitiveTypeCode.Char => "System.Char",
        PrimitiveTypeCode.Int16 => "System.Int16",
        PrimitiveTypeCode.UInt16 => "System.UInt16",
        PrimitiveTypeCode.Int32 => "System.Int32",
        PrimitiveTypeCode.UInt32 => "System.UInt32",
        PrimitiveTypeCode.Int64 => "System.Int64",
        PrimitiveTypeCode.UInt64 => "System.UInt64",
        PrimitiveTypeCode.Single => "System.Single",
        PrimitiveTypeCode.Double => "System.Double",
        PrimitiveTypeCode.IntPtr => "System.IntPtr",
        PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
        PrimitiveTypeCode.Object => "System.Object",
        PrimitiveTypeCode.String => "System.String",
        PrimitiveTypeCode.TypedReference => "System.TypedReference",
        PrimitiveTypeCode.Void => "System.Void",
        _ => code.ToString()
    };

    public string GetTypeFromDefinition(MetadataReader md, TypeDefinitionHandle h, byte rawKind) => Meta.FullName(md, h);
    public string GetTypeFromReference(MetadataReader md, TypeReferenceHandle h, byte rawKind) => Meta.FullName(md, h);
    public string GetTypeFromSpecification(MetadataReader md, string[] ctx, TypeSpecificationHandle h, byte rawKind) =>
        md.GetTypeSpecification(h).DecodeSignature(this, ctx);
    public string GetSZArrayType(string e) => e + "[]";
    public string GetArrayType(string e, ArrayShape shape) => e + "[" + new string(',', Math.Max(0, shape.Rank - 1)) + "]";
    public string GetByReferenceType(string e) => e + "&";
    public string GetPointerType(string e) => e + "*";
    public string GetPinnedType(string e) => e;
    public string GetGenericInstantiation(string g, ImmutableArray<string> args) => g + "<" + string.Join(",", args) + ">";
    public string GetGenericTypeParameter(string[] ctx, int i) => ctx != null && i < ctx.Length ? ctx[i] : "!" + i;
    public string GetGenericMethodParameter(string[] ctx, int i) => "!!" + i;
    public string GetModifiedType(string modifier, string unmodified, bool isRequired) =>
        unmodified + (isRequired ? " modreq(" : " modopt(") + modifier + ")";
    public string GetFunctionPointerType(MethodSignature<string> sig) =>
        "method " + sig.ReturnType + "*(" + string.Join(",", sig.ParameterTypes) + ")";

    // Custom attribute arguments (typeof(...) values arrive as serialized names).
    public string GetSystemType() => "System.Type";
    public bool IsSystemType(string t) => t == "System.Type";
    public string GetTypeFromSerializedName(string name) => Meta.FromSerializedName(name);
    public PrimitiveTypeCode GetUnderlyingEnumType(string t) => PrimitiveTypeCode.Int32;
}

static class Meta
{
    public static string FullName(MetadataReader md, TypeDefinitionHandle h)
    {
        var t = md.GetTypeDefinition(h);
        var decl = t.GetDeclaringType();
        if (!decl.IsNil) return FullName(md, decl) + "+" + md.GetString(t.Name);
        var ns = md.GetString(t.Namespace);
        return ns.Length == 0 ? md.GetString(t.Name) : ns + "." + md.GetString(t.Name);
    }

    public static string FullName(MetadataReader md, TypeReferenceHandle h)
    {
        var t = md.GetTypeReference(h);
        if (t.ResolutionScope.Kind == HandleKind.TypeReference)
            return FullName(md, (TypeReferenceHandle)t.ResolutionScope) + "+" + md.GetString(t.Name);
        var ns = md.GetString(t.Namespace);
        return ns.Length == 0 ? md.GetString(t.Name) : ns + "." + md.GetString(t.Name);
    }

    /// <summary>The assembly a type reference points into (null for the module itself).</summary>
    public static string Scope(MetadataReader md, TypeReferenceHandle h)
    {
        var t = md.GetTypeReference(h);
        while (t.ResolutionScope.Kind == HandleKind.TypeReference) t = md.GetTypeReference((TypeReferenceHandle)t.ResolutionScope);
        return t.ResolutionScope.Kind == HandleKind.AssemblyReference
            ? md.GetString(md.GetAssemblyReference((AssemblyReferenceHandle)t.ResolutionScope).Name)
            : null;
    }

    /// <summary>Any type handle (definition, reference or specification) as a name.</summary>
    public static string TypeName(MetadataReader md, EntityHandle h, string[] ctx = null) => h.Kind switch
    {
        HandleKind.TypeDefinition => FullName(md, (TypeDefinitionHandle)h),
        HandleKind.TypeReference => FullName(md, (TypeReferenceHandle)h),
        HandleKind.TypeSpecification => md.GetTypeSpecification((TypeSpecificationHandle)h).DecodeSignature(Names.I, ctx),
        _ => null
    };

    /// <summary>"List`1&lt;A,B&lt;C&gt;&gt;" into "List`1" and ["A", "B&lt;C&gt;"]; a plain name has no arguments.</summary>
    public static (string name, string[] args) SplitGeneric(string s)
    {
        if (!s.EndsWith(">")) return (s, Array.Empty<string>());
        // The '<' that matches the final '>' (compiler-made names like "<Module>" or "Foo+<>c" are not generic).
        int lt = -1;
        for (int i = s.Length - 1, d = 0; i >= 0; i--)
        {
            if (s[i] == '>') d++;
            else if (s[i] == '<' && --d == 0) { lt = i; break; }
        }
        if (lt <= 0 || s[lt - 1] == '+' || s[lt - 1] == '.') return (s, Array.Empty<string>());
        var args = new List<string>();
        int depth = 0, start = lt + 1;
        for (int i = lt + 1; i < s.Length - 1; i++)
        {
            if (s[i] == '<') depth++;
            else if (s[i] == '>') depth--;
            else if (s[i] == ',' && depth == 0) { args.Add(s.Substring(start, i - start)); start = i + 1; }
        }
        args.Add(s.Substring(start, s.Length - 1 - start));
        return (s.Substring(0, lt), args.ToArray());
    }

    /// <summary>A reflection-style name ("List`1[[Verse.Pawn, Assembly-CSharp]], mscorlib") in this tool's form.</summary>
    public static string FromSerializedName(string name)
    {
        int pos = 0;
        var r = ParseSerialized(name, ref pos);
        return r;
    }

    private static string ParseSerialized(string s, ref int pos)
    {
        int start = pos;
        while (pos < s.Length && s[pos] != '[' && s[pos] != ',' && s[pos] != ']' && s[pos] != '&' && s[pos] != '*') pos++;
        string name = s.Substring(start, pos - start).Trim();
        // Generic arguments: [[A, asm],[B, asm]] (or [A,B] without assemblies).
        if (pos < s.Length && s[pos] == '[' && pos + 1 < s.Length && s[pos + 1] != ']' && s[pos + 1] != ',')
        {
            pos++; // [
            var args = new List<string>();
            while (pos < s.Length && s[pos] != ']')
            {
                bool bracketed = s[pos] == '[';
                if (bracketed) pos++;
                args.Add(ParseSerialized(s, ref pos));
                if (bracketed)
                {
                    // Skip the assembly part up to the matching ']'.
                    int depth = 0;
                    while (pos < s.Length && !(s[pos] == ']' && depth == 0)) { if (s[pos] == '[') depth++; else if (s[pos] == ']') depth--; pos++; }
                    pos++; // ]
                }
                if (pos < s.Length && s[pos] == ',') pos++;
            }
            pos++; // ]
            name += "<" + string.Join(",", args) + ">";
        }
        // Arrays, by-ref and pointers.
        while (pos < s.Length && (s[pos] == '[' || s[pos] == '&' || s[pos] == '*'))
        {
            if (s[pos] == '[')
            {
                int close = s.IndexOf(']', pos);
                name += s.Substring(pos, close - pos + 1);
                pos = close + 1;
            }
            else name += s[pos++];
        }
        // Skip ", assembly" when this is the outermost name.
        return name;
    }

    public static string Sig(string name, MethodSignature<string> sig) =>
        (sig.Header.IsInstance ? "instance " : "static ") + sig.ReturnType + " " + name +
        (sig.GenericParameterCount > 0 ? "``" + sig.GenericParameterCount : "") +
        "(" + string.Join(",", sig.ParameterTypes) + ")";
}

sealed class Asm
{
    public string Path, Name;
    public MetadataReader Md;
    private readonly PEReader pe;
    public PEReader Peb => pe;

    public Asm(string path)
    {
        Path = path;
        pe = new PEReader(File.OpenRead(path));
        Md = pe.GetMetadataReader();
        Name = Md.GetString(Md.GetAssemblyDefinition().Name);
    }

    public static Asm TryOpen(string path)
    {
        try
        {
            var a = new Asm(path);
            return a.Md.IsAssembly ? a : null;
        }
        catch { return null; }
    }
}

sealed class MethodRec
{
    public string Name, Sig, ReturnType;
    public string[] ParamTypes, ParamNames;
    public bool IsPublic, IsFamily, IsStatic, IsVirtual, IsAbstract, IsNewSlot, IsSpecialName;
    public TypeRec Owner;
}

sealed class FieldRec
{
    public string Name, Type;
    public bool IsPublic, IsStatic;
    public TypeRec Owner;
}

/// <summary>One type definition in a set of assemblies, with its members read on first use.</summary>
sealed class TypeRec
{
    public Asm Asm;
    public TypeDefinitionHandle H;
    public string FullName;
    private List<MethodRec> methods;
    private List<FieldRec> fields;
    private HashSet<string> properties;

    public TypeDefinition Def => Asm.Md.GetTypeDefinition(H);
    public bool IsInterface => (Def.Attributes & TypeAttributes.Interface) != 0;
    public bool IsAbstract => (Def.Attributes & TypeAttributes.Abstract) != 0;

    /// <summary>Public all the way out (a nested type also needs its declaring types public).</summary>
    public bool IsVisible
    {
        get
        {
            var md = Asm.Md;
            var h = H;
            while (true)
            {
                var t = md.GetTypeDefinition(h);
                var vis = t.Attributes & TypeAttributes.VisibilityMask;
                if (vis != TypeAttributes.Public && vis != TypeAttributes.NestedPublic && vis != TypeAttributes.NestedFamily && vis != TypeAttributes.NestedFamORAssem) return false;
                var d = t.GetDeclaringType();
                if (d.IsNil) return true;
                h = d;
            }
        }
    }

    /// <summary>The base type's name with this type's own arguments filled in (ctx), or null for none.</summary>
    public string BaseName(string[] ctx = null)
    {
        var b = Def.BaseType;
        return b.IsNil ? null : Meta.TypeName(Asm.Md, b, ctx);
    }

    public IEnumerable<string> InterfaceNames(string[] ctx = null)
    {
        foreach (var ih in Def.GetInterfaceImplementations())
            yield return Meta.TypeName(Asm.Md, Asm.Md.GetInterfaceImplementation(ih).Interface, ctx);
    }

    public List<MethodRec> Methods(string[] ctx = null)
    {
        if (ctx == null && methods != null) return methods;
        var md = Asm.Md;
        var list = new List<MethodRec>();
        foreach (var mh in Def.GetMethods())
        {
            var m = md.GetMethodDefinition(mh);
            MethodSignature<string> sig;
            try { sig = m.DecodeSignature(Names.I, ctx); }
            catch { continue; }
            var name = md.GetString(m.Name);
            var names = new string[sig.ParameterTypes.Length];
            foreach (var ph in m.GetParameters())
            {
                var p = md.GetParameter(ph);
                if (p.SequenceNumber > 0 && p.SequenceNumber <= names.Length) names[p.SequenceNumber - 1] = md.GetString(p.Name);
            }
            var a = m.Attributes;
            var access = a & MethodAttributes.MemberAccessMask;
            list.Add(new MethodRec
            {
                Name = name,
                Sig = Meta.Sig(name, sig),
                ReturnType = sig.ReturnType,
                ParamTypes = sig.ParameterTypes.ToArray(),
                ParamNames = names,
                IsPublic = access == MethodAttributes.Public,
                IsFamily = access == MethodAttributes.Family || access == MethodAttributes.FamORAssem,
                IsStatic = (a & MethodAttributes.Static) != 0,
                IsVirtual = (a & MethodAttributes.Virtual) != 0,
                IsAbstract = (a & MethodAttributes.Abstract) != 0,
                IsNewSlot = (a & MethodAttributes.NewSlot) != 0,
                IsSpecialName = (a & MethodAttributes.SpecialName) != 0,
                Owner = this
            });
        }
        if (ctx == null) methods = list;
        return list;
    }

    public List<FieldRec> Fields()
    {
        if (fields != null) return fields;
        var md = Asm.Md;
        fields = new List<FieldRec>();
        foreach (var fh in Def.GetFields())
        {
            var f = md.GetFieldDefinition(fh);
            string type;
            try { type = f.DecodeSignature(Names.I, null); }
            catch { continue; }
            fields.Add(new FieldRec
            {
                Name = md.GetString(f.Name),
                Type = type,
                IsPublic = (f.Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public,
                IsStatic = (f.Attributes & FieldAttributes.Static) != 0,
                Owner = this
            });
        }
        return fields;
    }

    /// <summary>
    /// Constants (const fields and enum members) and their values. A compiled DLL carries the values it used, not the
    /// names, so a constant whose value changed is silently wrong rather than missing.
    /// </summary>
    public Dictionary<string, string> Literals()
    {
        var md = Asm.Md;
        var result = new Dictionary<string, string>();
        foreach (var fh in Def.GetFields())
        {
            var f = md.GetFieldDefinition(fh);
            if ((f.Attributes & FieldAttributes.Literal) == 0) continue;
            // Only constants another assembly can compile against.
            var access = f.Attributes & FieldAttributes.FieldAccessMask;
            if (access != FieldAttributes.Public && access != FieldAttributes.Family && access != FieldAttributes.FamORAssem) continue;
            var ch = f.GetDefaultValue();
            if (ch.IsNil) continue;
            var c = md.GetConstant(ch);
            var blob = md.GetBlobReader(c.Value);
            string value = c.TypeCode switch
            {
                ConstantTypeCode.Boolean => blob.ReadBoolean().ToString(),
                ConstantTypeCode.Char => ((int)blob.ReadChar()).ToString(),
                ConstantTypeCode.SByte => blob.ReadSByte().ToString(),
                ConstantTypeCode.Byte => blob.ReadByte().ToString(),
                ConstantTypeCode.Int16 => blob.ReadInt16().ToString(),
                ConstantTypeCode.UInt16 => blob.ReadUInt16().ToString(),
                ConstantTypeCode.Int32 => blob.ReadInt32().ToString(),
                ConstantTypeCode.UInt32 => blob.ReadUInt32().ToString(),
                ConstantTypeCode.Int64 => blob.ReadInt64().ToString(),
                ConstantTypeCode.UInt64 => blob.ReadUInt64().ToString(),
                ConstantTypeCode.Single => blob.ReadSingle().ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                ConstantTypeCode.Double => blob.ReadDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                ConstantTypeCode.String => "\"" + blob.ReadUTF16(blob.Length) + "\"",
                _ => "null"
            };
            result[md.GetString(f.Name)] = value;
        }
        return result;
    }

    public HashSet<string> Properties()
    {
        if (properties != null) return properties;
        properties = new HashSet<string>();
        foreach (var ph in Def.GetProperties()) properties.Add(Asm.Md.GetString(Asm.Md.GetPropertyDefinition(ph).Name));
        return properties;
    }
}

/// <summary>A set of reference assemblies (one game version and its runtime), indexed by type name.</summary>
sealed class AsmSet
{
    public readonly string Label;
    public readonly List<Asm> Asms = new();
    private readonly Dictionary<string, List<TypeRec>> types = new();

    public AsmSet(string label, IEnumerable<string> paths)
    {
        Label = label;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths)
        {
            var a = Asm.TryOpen(p);
            if (a == null || !seen.Add(a.Name)) continue;
            Asms.Add(a);
            foreach (var th in a.Md.TypeDefinitions)
            {
                var name = Meta.FullName(a.Md, th);
                if (!types.TryGetValue(name, out var list)) types[name] = list = new List<TypeRec>();
                list.Add(new TypeRec { Asm = a, H = th, FullName = name });
            }
        }
    }

    /// <summary>Every definition of a type by full name (generic arguments ignored).</summary>
    public List<TypeRec> Find(string name)
    {
        if (name == null) return new List<TypeRec>();
        var (open, _) = Meta.SplitGeneric(name);
        return types.TryGetValue(open, out var list) ? list : new List<TypeRec>();
    }

    public bool Has(string name) => Find(name).Count > 0;

    /// <summary>The type and its base types, each with its type arguments, nearest first.</summary>
    public IEnumerable<(TypeRec type, string[] ctx)> Chain(string name)
    {
        var seen = new HashSet<string>();
        var cur = name;
        while (cur != null && seen.Add(cur))
        {
            var (open, args) = Meta.SplitGeneric(cur);
            var t = Find(open).FirstOrDefault();
            if (t == null) yield break;
            var ctx = args.Length > 0 ? args : null;
            yield return (t, ctx);
            cur = t.BaseName(ctx);
        }
    }

    /// <summary>Is this a value type (struct or enum)?</summary>
    public bool? IsValueType(string name)
    {
        var t = Find(name).FirstOrDefault();
        if (t == null) return null;
        if (t.FullName is "System.Enum" or "System.ValueType") return false;
        var b = t.BaseName();
        return b is "System.ValueType" or "System.Enum";
    }
}
