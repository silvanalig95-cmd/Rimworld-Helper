using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Which quests send the colony out on the world map (QuestPlaceMath has the rule), read once per quest script from
    /// the script itself: every node of its tree, the scripts it runs as parts of itself (the game's "Util_GenerateSite"),
    /// each node's type and the types it is built on by name, and the code of each node type (its methods, the closures
    /// inside them, and a mod's own helpers two calls deep) for the calls that make or find a place elsewhere. A quest
    /// the game has made since that did make one (a world object spawned, a trade request at a town) counts too, for a
    /// script whose code hides it. Read the same way for the game's, the DLCs' and every mod's quests, nothing named.
    /// </summary>
    public static class QuestPlaces
    {
        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private const int MaxDepth = 60;

        private static readonly Dictionary<QuestScriptDef, string> scanned = new Dictionary<QuestScriptDef, string>();
        private static readonly Dictionary<Type, string> typeWhy = new Dictionary<Type, string>();
        private static readonly Dictionary<Type, FieldInfo[]> childFields = new Dictionary<Type, FieldInfo[]>();
        private static readonly Dictionary<MethodBase, string> methodWhy = new Dictionary<MethodBase, string>();
        private static readonly FieldInfo SubScriptName = AccessTools.Field(typeof(SlateRef<QuestScriptDef>), "slateRef");
        private static Assembly GameAssembly => typeof(QuestNode).Assembly;

        /// <summary>Scripts seen to make a place elsewhere in this game's own quests, by name, with what they made.</summary>
        private static readonly Dictionary<string, string> learned = new Dictionary<string, string>();
        private static Game learnedGame;
        private static int learnedCount = -1;

        /// <summary>The quest sends the colony out on the world map.</summary>
        public static bool IsAway(QuestScriptDef q) => Why(q) != null;

        /// <summary>Why the quest is out on the world map ("node QuestNode_GetSiteTile", "calls SiteMaker.MakeSite"), or null at home.</summary>
        public static string Why(QuestScriptDef q)
        {
            if (q == null) return null;
            string w = Scanned(q);
            if (w != null) return w;
            Learn();
            return learned.TryGetValue(q.defName, out var l) ? "seen: " + l : null;
        }

        /// <summary>A quest the game made that is out on the world map: by its script, or by what it made.</summary>
        public static bool IsAway(Quest q) => q != null && (Seen(q) != null || IsAway(q.root));

        /// <summary>The setting "Quests out on the world map", within its bounds.</summary>
        public static float Share => QuestPlaceMath.Share(S?.awayQuestShare ?? QuestPlaceMath.DefaultAwayShare);

        /// <summary>The setting holds some back (below 100%).</summary>
        public static bool Holding => Share < QuestPlaceMath.MaxAwayShare - 0.001f;

        /// <summary>The factor on the quest's weight when the storyteller's quest slot picks one.</summary>
        public static float ChooserFactor(QuestScriptDef q)
        {
            if (q == null || !Holding) return 1f;
            return QuestPlaceMath.WeightFactor(IsAway(q), Share);
        }

        // ---------------- The script ----------------

        private static string Scanned(QuestScriptDef q)
        {
            if (scanned.TryGetValue(q, out var w)) return w.Length == 0 ? null : w;
            string why = null;
            try
            {
                why = ScanScript(q, new HashSet<object>(), 0);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests: where " + q.defName + " plays out", e);
            }
            scanned[q] = why ?? "";
            return why;
        }

        private static string ScanScript(QuestScriptDef q, HashSet<object> seen, int depth) => q?.root == null ? null : ScanNode(q.root, seen, depth);

        private static string ScanNode(QuestNode node, HashSet<object> seen, int depth)
        {
            if (node == null || depth > MaxDepth || !seen.Add(node)) return null;
            string w = TypeWhy(node.GetType());
            if (w != null) return w;
            foreach (var f in ChildFields(node.GetType()))
            {
                object v;
                try
                {
                    v = f.GetValue(node);
                }
                catch
                {
                    continue;
                }
                if (v == null) continue;
                if (v is QuestNode child) w = ScanNode(child, seen, depth + 1);
                else if (v is IEnumerable list && !(v is string))
                {
                    foreach (var x in list)
                        if (x is QuestNode c && (w = ScanNode(c, seen, depth + 1)) != null)
                            break;
                }
                else if (SubScriptOf(v) is QuestScriptDef sub && seen.Add(sub))
                {
                    w = ScanScript(sub, seen, depth + 1);
                    if (w != null) w = "in " + sub.defName + ", " + w;
                }
                if (w != null) return w;
            }
            return null;
        }

        /// <summary>A script run as a part of this one (the game's QuestNode_SubScript and any node with such a field), named outright.</summary>
        private static QuestScriptDef SubScriptOf(object v)
        {
            if (!(v is SlateRef<QuestScriptDef>) || SubScriptName == null) return null;
            var name = SubScriptName.GetValue(v) as string;
            if (name.NullOrEmpty() || name.StartsWith("$")) return null;
            return DefDatabase<QuestScriptDef>.GetNamedSilentFail(name);
        }

        /// <summary>A node type's fields that hold more nodes or a script run as a part of it, its own and those it is built on.</summary>
        private static FieldInfo[] ChildFields(Type t)
        {
            if (childFields.TryGetValue(t, out var fs)) return fs;
            var list = new List<FieldInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var x = t; x != null && x != typeof(object); x = x.BaseType)
                foreach (var f in x.GetFields(flags))
                    if (HoldsNodes(f.FieldType))
                        list.Add(f);
            fs = list.ToArray();
            childFields[t] = fs;
            return fs;
        }

        private static bool HoldsNodes(Type ft)
        {
            if (typeof(QuestNode).IsAssignableFrom(ft) || ft == typeof(SlateRef<QuestScriptDef>)) return true;
            if (ft == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(ft)) return false;
            if (ft.IsArray) return typeof(QuestNode).IsAssignableFrom(ft.GetElementType());
            return ft.IsGenericType && ft.GetGenericArguments().Any(a => typeof(QuestNode).IsAssignableFrom(a));
        }

        // ---------------- A node type, by its name and its code ----------------

        private static string TypeWhy(Type t)
        {
            if (typeWhy.TryGetValue(t, out var w)) return w.Length == 0 ? null : w;
            string why = null;
            for (var x = t; x != null && x != typeof(QuestNode) && x != typeof(object); x = x.BaseType)
            {
                if (QuestPlaceMath.AwayNode(x.Name))
                {
                    why = "node " + x.Name;
                    break;
                }
                why = CodeWhy(x);
                if (why != null) break;
            }
            typeWhy[t] = why ?? "";
            return why;
        }

        private static string CodeWhy(Type x)
        {
            foreach (var m in DeclaredMethods(x))
            {
                string w = MethodWhy(m, x.Assembly, 0);
                if (w != null) return w + " in " + x.Name;
            }
            return null;
        }

        /// <summary>The methods a type declares, and those of the closures and iterators the compiler made inside it.</summary>
        private static IEnumerable<MethodBase> DeclaredMethods(Type x)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            MethodInfo[] own;
            Type[] nested;
            try
            {
                own = x.GetMethods(all);
                nested = x.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
            }
            catch
            {
                yield break;
            }
            foreach (var m in own) yield return m;
            foreach (var n in nested)
            {
                MethodInfo[] inner;
                try
                {
                    inner = n.GetMethods(all);
                }
                catch
                {
                    continue;
                }
                foreach (var m in inner) yield return m;
            }
        }

        /// <summary>
        /// A call in the method that makes or finds a place elsewhere, or null. A method of the mod's own (not the game's) that
        /// it calls is read too, two calls deep, so a helper of the mod's is found.
        /// </summary>
        private static string MethodWhy(MethodBase m, Assembly home, int depth)
        {
            if (m == null || m.IsAbstract || m.ContainsGenericParameters) return null;
            if (methodWhy.TryGetValue(m, out var w)) return w.Length == 0 ? null : w;
            methodWhy[m] = "";
            string why = null;
            List<KeyValuePair<OpCode, object>> body = null;
            try
            {
                body = PatchProcessor.ReadMethodBody(m)?.ToList();
            }
            catch
            {
                body = null;
            }
            if (body != null)
                foreach (var ins in body)
                {
                    string call = CallName(ins.Value, out var callee);
                    if (call != null && QuestPlaceMath.AwayCall(call))
                    {
                        why = "calls " + call;
                        break;
                    }
                    if (callee != null && depth < 2 && home != GameAssembly && callee.DeclaringType?.Assembly == home)
                    {
                        why = MethodWhy(callee, home, depth + 1);
                        if (why != null) break;
                    }
                }
            methodWhy[m] = why ?? "";
            return why;
        }

        /// <summary>"Type.Member" for a call or a constructor, "Type." for a type named in the code; a generic call's types as what it makes.</summary>
        private static string CallName(object operand, out MethodBase callee)
        {
            callee = null;
            try
            {
                if (operand is MethodBase mb)
                {
                    callee = mb;
                    if (mb is MethodInfo mi && mi.IsGenericMethod)
                        foreach (var g in mi.GetGenericArguments())
                            if (QuestPlaceMath.AwayCall(g.Name + "."))
                                return g.Name + ".";
                    return (mb.DeclaringType?.Name ?? "") + "." + mb.Name;
                }
                if (operand is Type t) return t.Name + ".";
            }
            catch
            {
                callee = null;
            }
            return null;
        }

        // ---------------- What the game's quests made ----------------

        /// <summary>A quest the game made that has a place out on the world map: a world object it spawns, or a trade request at a town.</summary>
        public static string Seen(Quest q)
        {
            var parts = q?.PartsListForReading;
            if (parts == null) return null;
            foreach (var p in parts)
            {
                if (p is QuestPart_SpawnWorldObject s && s.worldObject != null && s.worldObject.Faction != Faction.OfPlayer)
                    return s.worldObject.def?.defName ?? "a world object";
                if (p is QuestPart_InitiateTradeRequest) return "a trade request";
            }
            return null;
        }

        /// <summary>Notes the scripts whose quests in this game made a place elsewhere, when the game's list of quests has changed.</summary>
        private static void Learn()
        {
            try
            {
                var game = Current.Game;
                var list = game == null ? null : Find.QuestManager?.QuestsListForReading;
                if (list == null) return;
                if (learnedGame != game)
                {
                    learned.Clear();
                    learnedGame = game;
                    learnedCount = -1;
                }
                if (learnedCount == list.Count) return;
                learnedCount = list.Count;
                foreach (var q in list)
                {
                    if (q?.root == null || learned.ContainsKey(q.root.defName)) continue;
                    string w = Seen(q);
                    if (w != null) learned[q.root.defName] = w;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("quests: what the game's quests made", e);
            }
        }

        // ---------------- Dev mode ----------------

        /// <summary>
        /// Every quest the storyteller's quest slot can pick, at home or out on the world map and why, with its weight now and
        /// its mod; then the quests given another way (a story, an item, a ritual, a chain), which the setting leaves as they are.
        /// </summary>
        [DebugAction("Rimfolk", "Quests: where each one plays out (log)", allowedGameStates = AllowedGameStates.Playing)]
        private static void LogAll()
        {
            var sb = new StringBuilder();
            float share = Share;
            sb.AppendLine("[Rimfolk] Where each quest plays out. \"Quests out on the world map\": " + (share * 100f).ToString("0") + "%; \"How far quest sites may be\": "
                          + (S?.questSiteRange ?? QuestSiteRange.Nearer) + ".");
            var all = DefDatabase<QuestScriptDef>.AllDefsListForReading.OrderBy(d => d.modContentPack?.PackageId ?? "").ThenBy(d => d.defName).ToList();
            int away = 0, home = 0;
            sb.AppendLine("Picked by the storyteller's quest slot:");
            foreach (var q in all.Where(d => d.IsRootRandomSelected))
            {
                string why = Why(q);
                if (why != null) away++;
                else home++;
                sb.AppendLine("  " + (why != null ? "AWAY " : "home ") + q.defName + " (" + (q.modContentPack?.PackageId ?? "?") + ", weight " + q.rootSelectionWeight.ToString("0.##")
                              + (why != null ? ", x" + share.ToString("0.##") + "; " + why : "") + ")");
            }
            sb.AppendLine(away + " out on the world map, " + home + " at home.");
            sb.AppendLine("Given another way (a story, an item, a ritual, a decree, a chain), never held back:");
            foreach (var q in all.Where(d => !d.IsRootRandomSelected && d.root != null && !d.defName.StartsWith("Util_")))
                sb.AppendLine("  " + (IsAway(q) ? "away " : "home ") + q.defName + " (" + (q.modContentPack?.PackageId ?? "?") + ")");
            Log.Message(sb.ToString());
            Messages.Message("Quests: where each one plays out is in the log.", MessageTypeDefOf.SilentInput, false);
        }
    }
}
