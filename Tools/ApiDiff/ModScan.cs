// What the mod's DLL uses from outside itself: every type and member reference, every override of an outside
// method, and (from the IL) where each is used and every member looked up by name through reflection.

using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace ApiDiff;

sealed class MemberUse
{
    public string Scope, Parent, Name, Sig, FieldType;
    public bool IsField;
    public readonly SortedSet<string> UsedIn = new();
    public string Display => IsField ? $"{Parent}.{Name} (field {FieldType})" : $"{Parent}::{Sig}";
}

sealed class TypeUse
{
    public string Scope, Name;
    public readonly SortedSet<string> UsedIn = new();
}

/// <summary>A member looked up by name at run time (AccessTools, Type.GetField...), as found in the IL.</summary>
sealed class Lookup
{
    public string In, Api, Kind, Type, Name, ValueType;
    public string[] ArgTypes;
    public bool Dynamic; // the type isn't known until run time (another mod's type, or a value)
}

sealed class ModScan
{
    public readonly Asm Mod;
    public readonly Dictionary<EntityHandle, MemberUse> Members = new();
    public readonly Dictionary<string, TypeUse> Types = new();
    public readonly List<Lookup> Lookups = new();
    /// <summary>Numbers the IL hands to a parameter or field of an outside type: the enum values the DLL uses.</summary>
    public readonly Dictionary<string, HashSet<string>> ValuesPassed = new();

    public ModScan(Asm mod)
    {
        Mod = mod;
        var md = mod.Md;
        foreach (var th in md.TypeReferences)
        {
            var scope = Meta.Scope(md, th);
            if (scope == null) continue;
            var name = Meta.FullName(md, th);
            if (!Types.ContainsKey(name)) Types[name] = new TypeUse { Scope = scope, Name = name };
        }
        foreach (var mh in md.MemberReferences)
        {
            var m = md.GetMemberReference(mh);
            string parent, scope;
            switch (m.Parent.Kind)
            {
                case HandleKind.TypeReference:
                    parent = Meta.FullName(md, (TypeReferenceHandle)m.Parent);
                    scope = Meta.Scope(md, (TypeReferenceHandle)m.Parent);
                    break;
                case HandleKind.TypeSpecification:
                    // A generic type's member (List<Pawn>.Add): checked on the open definition, as the signature is.
                    var spec = md.GetTypeSpecification((TypeSpecificationHandle)m.Parent);
                    var blob = md.GetBlobReader(spec.Signature);
                    if (blob.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance) continue;
                    blob.ReadSignatureTypeCode();
                    var gh = blob.ReadTypeHandle();
                    if (gh.Kind != HandleKind.TypeReference) continue;
                    parent = Meta.FullName(md, (TypeReferenceHandle)gh);
                    scope = Meta.Scope(md, (TypeReferenceHandle)gh);
                    break;
                default:
                    continue;
            }
            if (scope == null) continue;
            var use = new MemberUse { Scope = scope, Parent = parent, Name = md.GetString(m.Name) };
            try
            {
                if (m.GetKind() == MemberReferenceKind.Field)
                {
                    use.IsField = true;
                    use.FieldType = m.DecodeFieldSignature(Names.I, null);
                }
                else use.Sig = Meta.Sig(use.Name, m.DecodeMethodSignature(Names.I, null));
            }
            catch { continue; }
            Members[mh] = use;
        }
        ScanBodies();
    }

    public string MethodName(MethodDefinitionHandle h)
    {
        var md = Mod.Md;
        var m = md.GetMethodDefinition(h);
        return Meta.FullName(md, m.GetDeclaringType()) + "." + md.GetString(m.Name);
    }

    // ---------- IL ----------

    private static readonly Dictionary<ushort, OpCode> Ops = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => unchecked((ushort)o.Value));

    private abstract record Val;
    private sealed record Unknown : Val { public static readonly Unknown I = new(); }
    private sealed record TokenVal(string Type) : Val;
    private sealed record TypeVal(string Type, bool Dynamic) : Val;
    private sealed record StrVal(string S) : Val;
    private sealed record IntVal(int N) : Val;
    private sealed record NullVal : Val;
    private sealed record ArrVal(Val[] Items) : Val;

    private void ScanBodies()
    {
        var md = Mod.Md;
        foreach (var th in md.TypeDefinitions)
        foreach (var mh in md.GetTypeDefinition(th).GetMethods())
        {
            var m = md.GetMethodDefinition(mh);
            if (m.RelativeVirtualAddress == 0) continue;
            MethodBodyBlock body;
            try { body = Mod.Peb.GetMethodBody(m.RelativeVirtualAddress); }
            catch { continue; }
            try { ScanBody(MethodName(mh), body.GetILBytes()); }
            catch (Exception e) { Console.Error.WriteLine($"(couldn't read the IL of {MethodName(mh)}: {e.Message})"); }
        }
    }

    private void ScanBody(string where, byte[] il)
    {
        var md = Mod.Md;
        var stack = new List<Val>();
        var atTarget = new Dictionary<int, List<Val>>(); // stack shapes carried to forward branch targets
        int pos = 0;
        bool reachable = true;
        while (pos < il.Length)
        {
            if (atTarget.TryGetValue(pos, out var carried) && (!reachable || stack.Count == 0)) stack = new List<Val>(carried);
            else if (!reachable) stack.Clear();
            reachable = true;
            int start = pos;
            ushort code = il[pos++];
            if (code == 0xFE) code = (ushort)(0xFE00 | il[pos++]);
            if (!Ops.TryGetValue(code, out var op)) return;
            int operand = 0;
            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar: operand = (sbyte)il[pos]; pos += 1; break;
                case OperandType.InlineVar: operand = BitConverter.ToUInt16(il, pos); pos += 2; break;
                case OperandType.InlineI8:
                case OperandType.InlineR: pos += 8; break;
                case OperandType.InlineSwitch:
                    int n = BitConverter.ToInt32(il, pos);
                    pos += 4 + 4 * n;
                    break;
                default: operand = BitConverter.ToInt32(il, pos); pos += 4; break;
            }

            Val Pop() { if (stack.Count == 0) return Unknown.I; var v = stack[^1]; stack.RemoveAt(stack.Count - 1); return v; }
            void Push(Val v) => stack.Add(v);
            void Pops(int k) { for (int i = 0; i < k; i++) Pop(); }

            // Branch targets keep the stack the branch left.
            if (op.OperandType is OperandType.ShortInlineBrTarget or OperandType.InlineBrTarget)
            {
                int popsBr = PopCount(op);
                var after = new List<Val>(stack);
                for (int i = 0; i < popsBr && after.Count > 0; i++) after.RemoveAt(after.Count - 1);
                int target = pos + operand;
                if (target > start) atTarget.TryAdd(target, after);
            }

            if (op == OpCodes.Ldstr) { Push(new StrVal(md.GetUserString(MetadataTokens.UserStringHandle(operand)))); continue; }
            if (op == OpCodes.Ldnull) { Push(new NullVal()); continue; }
            if (op == OpCodes.Ldtoken)
            {
                var h = MetadataTokens.EntityHandle(operand);
                string t = null;
                try { t = Meta.TypeName(md, h); } catch { }
                Push(t != null ? new TokenVal(t) : Unknown.I);
                if (h.Kind == HandleKind.MemberReference && Members.TryGetValue(h, out var mu)) mu.UsedIn.Add(where);
                continue;
            }
            if (op == OpCodes.Ldc_I4_M1) { Push(new IntVal(-1)); continue; }
            if (op.Value >= OpCodes.Ldc_I4_0.Value && op.Value <= OpCodes.Ldc_I4_8.Value) { Push(new IntVal(op.Value - OpCodes.Ldc_I4_0.Value)); continue; }
            if (op == OpCodes.Ldc_I4_S || op == OpCodes.Ldc_I4) { Push(new IntVal(operand)); continue; }
            if (op == OpCodes.Newarr)
            {
                var len = Pop();
                Push(len is IntVal { N: >= 0 and < 64 } iv ? new ArrVal(new Val[iv.N]) : Unknown.I);
                continue;
            }
            if (op == OpCodes.Dup) { var v = Pop(); Push(v); Push(v); continue; }
            if (op == OpCodes.Stelem_Ref || op == OpCodes.Stelem)
            {
                var v = Pop(); var idx = Pop(); var arr = Pop();
                if (arr is ArrVal a && idx is IntVal i && i.N >= 0 && i.N < a.Items.Length) a.Items[i.N] = v;
                continue;
            }
            if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj || op == OpCodes.Ldfld || op == OpCodes.Ldsfld
                || op == OpCodes.Stfld || op == OpCodes.Stsfld || op == OpCodes.Ldflda || op == OpCodes.Ldsflda || op == OpCodes.Ldftn || op == OpCodes.Ldvirtftn)
            {
                var h = MetadataTokens.EntityHandle(operand);
                EntityHandle refH = h;
                string[] genericArgs = null;
                if (h.Kind == HandleKind.MethodSpecification)
                {
                    var ms = md.GetMethodSpecification((MethodSpecificationHandle)h);
                    refH = ms.Method;
                    try { genericArgs = ms.DecodeSignature(Names.I, null).ToArray(); } catch { }
                }
                if (refH.Kind == HandleKind.MemberReference && Members.TryGetValue(refH, out var use)) use.UsedIn.Add(where);

                if (op == OpCodes.Ldfld || op == OpCodes.Ldflda || op == OpCodes.Ldvirtftn) { Pops(1); Push(Unknown.I); continue; }
                if (op == OpCodes.Ldsfld || op == OpCodes.Ldsflda || op == OpCodes.Ldftn) { Push(Unknown.I); continue; }
                if (op == OpCodes.Stfld || op == OpCodes.Stsfld)
                {
                    var stored = Pop();
                    if (op == OpCodes.Stfld) Pop();
                    if (stored is IntVal sv && refH.Kind == HandleKind.MemberReference && Members.TryGetValue(refH, out var fu) && fu.IsField) NoteValue(fu.FieldType, sv.N);
                    continue;
                }

                // A call: its arguments by signature.
                var (owner, name, sig) = CallInfo(refH);
                if (sig == null) { stack.Clear(); continue; }
                var args = new Val[sig.Value.ParameterTypes.Length];
                for (int i = args.Length - 1; i >= 0; i--) args[i] = Pop();
                for (int i = 0; i < args.Length; i++)
                    if (args[i] is IntVal pv) NoteValue(sig.Value.ParameterTypes[i], pv.N);
                Val self = null;
                if (sig.Value.Header.IsInstance && op != OpCodes.Newobj) self = Pop();
                Val result = Unknown.I;
                if (owner == "System.Type" && name == "GetTypeFromHandle" && args.Length == 1 && args[0] is TokenVal tv)
                    result = new TypeVal(tv.Type, false);
                else
                    result = Reflect(where, owner, name, sig.Value, self, args, genericArgs) ?? Unknown.I;
                if (op == OpCodes.Newobj || sig.Value.ReturnType != "System.Void") Push(result);
                continue;
            }
            if (op == OpCodes.Calli) { stack.Clear(); continue; }
            if (op == OpCodes.Ret || op == OpCodes.Throw || op == OpCodes.Rethrow || op == OpCodes.Br || op == OpCodes.Br_S
                || op == OpCodes.Leave || op == OpCodes.Leave_S || op == OpCodes.Endfinally || op == OpCodes.Endfilter || op == OpCodes.Jmp)
            {
                stack.Clear();
                reachable = false;
                continue;
            }
            Pops(PopCount(op));
            for (int i = 0; i < PushCount(op); i++) Push(Unknown.I);
        }
    }

    private void NoteValue(string type, int value)
    {
        if (type == null || type.StartsWith("System.")) return;
        if (!ValuesPassed.TryGetValue(type, out var set)) ValuesPassed[type] = set = new HashSet<string>();
        set.Add(value.ToString());
    }

    private static int PopCount(OpCode op) => op.StackBehaviourPop switch
    {
        StackBehaviour.Pop0 => 0,
        StackBehaviour.Pop1 or StackBehaviour.Popi or StackBehaviour.Popref => 1,
        StackBehaviour.Pop1_pop1 or StackBehaviour.Popi_pop1 or StackBehaviour.Popi_popi or StackBehaviour.Popi_popi8
            or StackBehaviour.Popi_popr4 or StackBehaviour.Popi_popr8 or StackBehaviour.Popref_pop1 or StackBehaviour.Popref_popi => 2,
        StackBehaviour.Popi_popi_popi or StackBehaviour.Popref_popi_popi or StackBehaviour.Popref_popi_popi8 or StackBehaviour.Popref_popi_popr4
            or StackBehaviour.Popref_popi_popr8 or StackBehaviour.Popref_popi_popref or StackBehaviour.Popref_popi_pop1 => 3,
        _ => 0
    };

    private static int PushCount(OpCode op) => op.StackBehaviourPush switch
    {
        StackBehaviour.Push0 => 0,
        StackBehaviour.Push1_push1 => 2,
        StackBehaviour.Varpush => 0,
        _ => 1
    };

    private (string owner, string name, MethodSignature<string>? sig) CallInfo(EntityHandle h)
    {
        var md = Mod.Md;
        try
        {
            if (h.Kind == HandleKind.MemberReference)
            {
                var m = md.GetMemberReference((MemberReferenceHandle)h);
                if (m.GetKind() != MemberReferenceKind.Method) return (null, null, null);
                string owner = m.Parent.Kind switch
                {
                    HandleKind.TypeReference or HandleKind.TypeDefinition or HandleKind.TypeSpecification => Meta.SplitGeneric(Meta.TypeName(md, (EntityHandle)m.Parent)).name,
                    _ => null
                };
                return (owner, md.GetString(m.Name), m.DecodeMethodSignature(Names.I, null));
            }
            if (h.Kind == HandleKind.MethodDefinition)
            {
                var m = md.GetMethodDefinition((MethodDefinitionHandle)h);
                return (Meta.FullName(md, m.GetDeclaringType()), md.GetString(m.Name), m.DecodeSignature(Names.I, null));
            }
        }
        catch { }
        return (null, null, null);
    }

    /// <summary>A reflection call that names a member: note it, and hand back a type value where it returns one.</summary>
    private Val Reflect(string where, string owner, string name, MethodSignature<string> sig, Val self, Val[] args, string[] generics)
    {
        string S(int i) => i < args.Length && args[i] is StrVal s ? s.S : null;
        TypeVal T(int i) => i < args.Length ? args[i] as TypeVal : null;
        string[] Arr(int i) => i < args.Length && args[i] is ArrVal a && a.Items.All(x => x is TypeVal)
            ? a.Items.Select(x => ((TypeVal)x).Type).ToArray()
            : null;
        int ParamIndex(string type, int nth = 0)
        {
            int seen = 0;
            for (int i = 0; i < sig.ParameterTypes.Length; i++)
                if (sig.ParameterTypes[i] == type && seen++ == nth) return i;
            return -1;
        }
        string Kind(string api) =>
            api.Contains("Constructor") ? "constructor"
            : api.Contains("Method") ? "method"
            : api.Contains("Field") ? "field"
            : api.Contains("Property") || api.Contains("Getter") || api.Contains("Setter") || api.Contains("Indexer") ? "property"
            : api is "GetMember" ? "member"
            : api is "GetNestedType" or "Inner" ? "nested type"
            : null;

        if (owner == "HarmonyLib.AccessTools")
        {
            if (name is "TypeByName" or "TypeSearch")
            {
                var tn = S(0);
                if (tn == null) return null;
                Lookups.Add(new Lookup { In = where, Api = "AccessTools." + name, Kind = "type", Type = tn });
                return new TypeVal(tn, true);
            }
            var kind = Kind(name);
            if (kind == null) return null;
            int ti = ParamIndex("System.Type");
            int si = ParamIndex("System.String");
            int ai = ParamIndex("System.Type[]");
            string type = ti >= 0 ? T(ti)?.Type : null;
            bool dyn = ti >= 0 && T(ti) is { Dynamic: true };
            string member = si >= 0 ? S(si) : null;
            string valueType = null;
            // FieldRefAccess<T, F>(name), StaticFieldRefAccess<T, F>(name), FieldRefAccess<F>(type, name).
            if (generics != null && name.Contains("FieldRefAccess"))
            {
                if (ti < 0 && generics.Length >= 1) type = generics[0];
                valueType = generics[^1];
            }
            // "Type:Member" in one string.
            if (ti < 0 && member != null && member.Contains(':') && type == null)
            {
                var parts = member.Split(':');
                type = parts[0];
                member = parts[1];
                dyn = true;
            }
            if (member == null && kind != "constructor") return null;
            Lookups.Add(new Lookup { In = where, Api = "AccessTools." + name, Kind = kind, Type = type, Name = member, ArgTypes = ai >= 0 ? Arr(ai) : null, ValueType = valueType, Dynamic = dyn || type == null });
            return null;
        }
        if (owner == "System.Type" && name is "GetField" or "GetMethod" or "GetProperty" or "GetMember" or "GetNestedType" or "GetConstructor")
        {
            var recv = self as TypeVal;
            string member = name == "GetConstructor" ? ".ctor" : S(0);
            if (member == null) return null;
            int ai = ParamIndex("System.Type[]");
            Lookups.Add(new Lookup { In = where, Api = "Type." + name, Kind = Kind(name), Type = recv?.Type, Name = member, ArgTypes = ai >= 0 ? Arr(ai) : null, Dynamic = recv == null || recv.Dynamic });
            return null;
        }
        if ((owner == "Verse.GenTypes" && name == "GetTypeInAnyAssembly") || (owner == "System.Type" && name == "GetType" && !sig.Header.IsInstance))
        {
            var tn = S(0);
            if (tn == null) return null;
            Lookups.Add(new Lookup { In = where, Api = owner.Split('.').Last() + "." + name, Kind = "type", Type = tn });
            return new TypeVal(tn, true);
        }
        if (owner == "HarmonyLib.Traverse" && name is "Field" or "Property" or "Method" && S(0) != null)
        {
            Lookups.Add(new Lookup { In = where, Api = "Traverse." + name, Kind = name.ToLowerInvariant(), Name = S(0), Dynamic = true });
            return null;
        }
        return null;
    }
}
