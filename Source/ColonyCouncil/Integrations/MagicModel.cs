using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// A RimWorld of Magic (Continued) read where it keeps a pawn's magic now (the rules in MagicModelMath;
    /// docs/review-magic.md), everything by name and softly:
    /// <list type="bullet">
    /// <item>A pawn's class through the continuation's class progression: RWoM Core's public PawnDisciplines.TryRead first,
    /// else the continuation's own ClassIdentity.TryGetState and its disciplines; each discipline read as the class trait it
    /// stands for. Classic class traits are traits and are read as before.</item>
    /// <item>Whether a spell is learned: RWoM Core's PawnSpellKnowledge.Read first, else the continuation's
    /// AbilityKnowledgeComp, by the spell's id (the continuation's SpellIds.Of, else its convention); null when neither
    /// has an entry, and the caller falls back to the old power list.</item>
    /// <item>Another system casting for the pawn now (RWoM Core's CastOwnership.IsCasting: Vanilla Psycasts Expanded's
    /// warmups, Ability AI), so Rimfolk's own casts don't cut in.</item>
    /// </list>
    /// Without the continuation (the original, or no magic mod) none of these is found: no disciplines, no knowledge entry,
    /// nobody owned, and every caller keeps to what it read before. A member not found is logged once.
    /// </summary>
    public static class MagicModel
    {
        private static bool looked;
        private static MethodInfo coreDisciplines, coreKnowledge, coreOwned, spellIdOf, tryGetState, knowledgeFind;
        private static Type knowledgeComp;
        private static PropertyInfo knowledgeReady;
        private static FieldInfo learnedField;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            if (!PeoplesMagic.Loaded) return;
            try
            {
                var disciplines = AccessTools.TypeByName("TorannMagic.Core.PawnDisciplines");
                coreDisciplines = disciplines == null ? null : AccessTools.Method(disciplines, "TryRead");
                if (coreDisciplines != null && (coreDisciplines.ReturnType != typeof(bool) || coreDisciplines.GetParameters().Length != 2)) coreDisciplines = null;
                var knowledge = AccessTools.TypeByName("TorannMagic.Core.PawnSpellKnowledge");
                coreKnowledge = knowledge == null ? null : AccessTools.Method(knowledge, "Read");
                if (coreKnowledge != null && coreKnowledge.GetParameters().Length != 3) coreKnowledge = null;
                var owned = AccessTools.TypeByName("TorannMagic.Core.CastOwnership");
                coreOwned = owned == null ? null : AccessTools.Method(owned, "IsCasting", new[] { typeof(Pawn) });
                if (coreOwned != null && coreOwned.ReturnType != typeof(bool)) coreOwned = null;
                var ids = AccessTools.TypeByName("TorannMagic.SpellIds");
                spellIdOf = ids == null ? null : AccessTools.Method(ids, "Of", new[] { typeof(Def) });
                if (spellIdOf != null && spellIdOf.ReturnType != typeof(string)) spellIdOf = null;
                var identity = AccessTools.TypeByName("TorannMagic.ClassProgression.ClassIdentity");
                tryGetState = identity == null ? null : AccessTools.Method(identity, "TryGetState");
                if (tryGetState != null && (tryGetState.ReturnType != typeof(bool) || tryGetState.GetParameters().Length != 2)) tryGetState = null;
                knowledgeComp = AccessTools.TypeByName("TorannMagic.AbilityKnowledgeComp");
                knowledgeFind = knowledgeComp == null ? null : AccessTools.Method(knowledgeComp, "Find", new[] { typeof(string) });
                knowledgeReady = knowledgeComp == null ? null : AccessTools.Property(knowledgeComp, "ReadyForQuery");
                if (knowledgeReady != null && knowledgeReady.PropertyType != typeof(bool)) knowledgeReady = null;
                // The continuation is there (its class progression or its knowledge store), but RWoM Core's way in isn't.
                if ((tryGetState != null || knowledgeComp != null) && (coreDisciplines == null || coreKnowledge == null))
                    CouncilLog.WarnOnce("A RimWorld of Magic (Continued) is loaded, but RWoM Core's PawnDisciplines or PawnSpellKnowledge wasn't found; Rimfolk reads the continuation's own components instead.");
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic: reading its class progression and spell knowledge", e);
            }
        }

        public static void Reset() => classes.Clear();

        // ================= Classes through disciplines =================

        private static readonly Dictionary<int, (int tick, List<string> list)> classes = new Dictionary<int, (int, List<string>)>();
        private static readonly List<string> none = new List<string>();

        /// <summary>
        /// The class traits this pawn holds through the continuation's disciplines (not their traits), worked out at most
        /// every 250 ticks. Empty without the continuation, for a pawn of the classic classes, and when nothing can be read.
        /// </summary>
        public static List<string> DisciplineClasses(Pawn p)
        {
            if (p?.RaceProps?.Humanlike != true || !PeoplesMagic.Loaded) return none;
            Look();
            if (coreDisciplines == null && tryGetState == null) return none;
            int now = Find.TickManager?.TicksGame ?? 0;
            if (classes.TryGetValue(p.thingIDNumber, out var kept) && now >= kept.tick && now - kept.tick < Sched.RareTicks) return kept.list;
            var list = new List<string>();
            try
            {
                foreach (var (id, domain) in Read(p))
                    foreach (string trait in MagicModelMath.ClassesOf(id, domain))
                        if (!list.Contains(trait) && p.story?.traits?.allTraits?.Exists(t => t?.def?.defName == trait) != true)
                            list.Add(trait);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic: a pawn's disciplines", e);
            }
            if (classes.Count > 600) classes.Clear();
            classes[p.thingIDNumber] = (now, list);
            return list;
        }

        private static readonly Dictionary<(Type, string), PropertyInfo> props = new Dictionary<(Type, string), PropertyInfo>();

        private static object Get(object o, string name)
        {
            if (o == null) return null;
            var key = (o.GetType(), name);
            if (!props.TryGetValue(key, out var prop)) props[key] = prop = AccessTools.Property(key.Item1, name);
            return prop?.GetValue(o);
        }

        /// <summary>The pawn's disciplines (id and domain): RWoM Core's reading first, else the continuation's own.</summary>
        private static List<(string id, int domain)> Read(Pawn p)
        {
            var rows = new List<(string, int)>();
            if (coreDisciplines != null)
            {
                var args = new object[] { p, null };
                if (coreDisciplines.Invoke(null, args) is bool ok && ok && args[1] is IEnumerable read)
                {
                    foreach (var row in read)
                        if (Get(row, "Id") is string id)
                            rows.Add((id, Get(row, "Kind") is object kind ? Convert.ToInt32(kind) : 0));
                    return rows;
                }
            }
            if (tryGetState != null)
            {
                var args = new object[] { p, null };
                if (tryGetState.Invoke(null, args) is bool ok && ok && Get(args[1], "Disciplines") is IEnumerable paths)
                    foreach (var path in paths)
                        if (Get(path, "ClassId") is string id)
                            rows.Add((id, Get(path, "Domain") is object domain ? Convert.ToInt32(domain) : 0));
            }
            return rows;
        }

        /// <summary>They follow this discipline of the continuation's class progression (its id, such as rwom.deathknight).</summary>
        public static bool InDiscipline(Pawn p, string id)
        {
            if (p?.RaceProps?.Humanlike != true || id == null || !PeoplesMagic.Loaded) return false;
            Look();
            if (coreDisciplines == null && tryGetState == null) return false;
            try
            {
                foreach (var (have, _) in Read(p))
                    if (have == id)
                        return true;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic: a pawn's disciplines", e);
            }
            return false;
        }

        /// <summary>They hold this class: its trait, or a discipline of the continuation that stands for it.</summary>
        public static bool HasClass(Pawn p, string trait)
        {
            if (p == null || trait == null) return false;
            var traits = p.story?.traits?.allTraits;
            if (traits != null)
                foreach (var t in traits)
                    if (t?.def?.defName == trait)
                        return true;
            return DisciplineClasses(p).Contains(trait);
        }

        /// <summary>One of the continuation's vocations (its class progression's mage, warrior or warrior-mage).</summary>
        public static bool HasVocation(Pawn p)
        {
            var traits = p?.story?.traits?.allTraits;
            if (traits == null) return false;
            foreach (var t in traits)
                if (t?.def != null && MagicModelMath.Vocations.Contains(t.def.defName))
                    return true;
            return false;
        }

        /// <summary>A magic user by the mod's own mark: its mana need (TM_Mana), which every caster of a magic class has.</summary>
        public static bool HasMana(Pawn p)
        {
            var needs = p?.needs?.AllNeeds;
            if (needs == null) return false;
            foreach (var n in needs)
                if (n?.def?.defName == "TM_Mana")
                    return true;
            return false;
        }

        // ================= Spells learned =================

        private static readonly Dictionary<Def, string> ids = new Dictionary<Def, string>();

        /// <summary>The continuation's id for a spell (its SpellIds.Of, else its convention), or null.</summary>
        public static string SpellId(Def def)
        {
            if (def?.defName == null) return null;
            if (ids.TryGetValue(def, out var id)) return id;
            Look();
            try
            {
                id = spellIdOf?.Invoke(null, new object[] { def }) as string;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic: a spell's id", e);
                id = null;
            }
            id ??= MagicModelMath.ConventionSpellId(def.defName);
            ids[def] = id;
            return id;
        }

        /// <summary>
        /// The knowledge store's learned mark for this spell on this pawn: RWoM Core's reading first, else the continuation's
        /// own store; null when neither has an entry (the original mod, no store yet, an import still pending).
        /// </summary>
        public static bool? Learned(Pawn p, Def def)
        {
            if (p == null || def == null || !PeoplesMagic.Loaded) return null;
            Look();
            if (coreKnowledge == null && knowledgeFind == null) return null;
            string id = SpellId(def);
            if (id == null) return null;
            try
            {
                if (coreKnowledge != null)
                {
                    var args = new object[] { p, id, null };
                    string status = coreKnowledge.Invoke(null, args)?.ToString();
                    if (MagicModelMath.EntryFound(status) && Get(args[2], "Learned") is bool learned)
                        return learned;
                    // RWoM Core answered for the store (no entry, or still importing): nothing more to read there.
                    if (status == "NoRecord" || status == "PendingImport") return null;
                }
                if (knowledgeFind != null && p.AllComps != null)
                    foreach (var c in p.AllComps)
                    {
                        if (c == null || !knowledgeComp.IsInstanceOfType(c)) continue;
                        if (knowledgeReady != null && knowledgeReady.GetValue(c) is bool ready && !ready) return null;
                        var entry = knowledgeFind.Invoke(c, new object[] { id });
                        if (entry == null) return null;
                        learnedField ??= AccessTools.Field(entry.GetType(), "learned");
                        return learnedField?.GetValue(entry) is bool l ? l : (bool?)null;
                    }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic: a spell's knowledge", e);
            }
            return null;
        }

        private static PropertyInfo spellsProp;
        private static FieldInfo idField;

        /// <summary>
        /// Every entry of the continuation's knowledge store on this pawn at once (its AbilityKnowledgeComp.Spells: the
        /// spell's id and its learned mark), for a count over all their powers; null without the store, or while its import
        /// is still pending.
        /// </summary>
        public static Dictionary<string, bool> KnowledgeOf(Pawn p)
        {
            if (p?.AllComps == null || !PeoplesMagic.Loaded) return null;
            Look();
            if (knowledgeComp == null) return null;
            try
            {
                foreach (var c in p.AllComps)
                {
                    if (c == null || !knowledgeComp.IsInstanceOfType(c)) continue;
                    if (knowledgeReady != null && knowledgeReady.GetValue(c) is bool ready && !ready) return null;
                    spellsProp ??= AccessTools.Property(knowledgeComp, "Spells");
                    if (!(spellsProp?.GetValue(c) is IEnumerable spells)) return null;
                    var all = new Dictionary<string, bool>();
                    foreach (var s in spells)
                    {
                        if (s == null) continue;
                        idField ??= AccessTools.Field(s.GetType(), "spellId");
                        learnedField ??= AccessTools.Field(s.GetType(), "learned");
                        if (idField?.GetValue(s) is string id && learnedField?.GetValue(s) is bool l) all[id] = l;
                    }
                    return all;
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic: a pawn's spell knowledge", e);
            }
            return null;
        }

        // ================= Someone else casting =================

        /// <summary>Another system registered with RWoM Core is casting for this pawn now (false without RWoM Core).</summary>
        public static bool CastOwned(Pawn p)
        {
            if (p == null || !PeoplesMagic.Loaded) return false;
            Look();
            if (coreOwned == null) return false;
            try
            {
                return coreOwned.Invoke(null, new object[] { p }) is bool b && b;
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("A RimWorld of Magic: RWoM Core's cast ownership", e);
                return false;
            }
        }
    }
}
