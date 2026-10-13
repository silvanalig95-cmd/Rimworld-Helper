using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One size or kind of a room type: a template and the short name the planner shows for it ("cozy", "for 12").</summary>
    public class RoomVariant
    {
        /// <summary>A RoomTemplateDef defName, looked up silently (a variant from a DLC or mod that isn't loaded is skipped).</summary>
        public string template;
        [MustTranslate] public string label;

        public RoomTemplateDef Template => template.NullOrEmpty() ? null : DefDatabase<RoomTemplateDef>.GetNamedSilentFail(template);
    }

    /// <summary>
    /// Groups room templates that are the same kind of room in different sizes or styles (a cozy, standard and roomy
    /// bedroom; a dining room for 4, 8 or 12). Menus show one entry per type and the planner lets the player switch
    /// between its variants. A template in no type stands on its own.
    /// </summary>
    public class RoomTypeDef : Def
    {
        public List<RoomVariant> variants = new List<RoomVariant>();
        /// <summary>The variant menus start with (defName); the first one that loads if empty.</summary>
        public string main;

        [Unsaved] private List<(RoomTemplateDef t, string label)> resolved;

        /// <summary>The variants that exist in this game, in the order listed.</summary>
        public List<(RoomTemplateDef t, string label)> Variants
        {
            get
            {
                if (resolved == null)
                {
                    resolved = new List<(RoomTemplateDef, string)>();
                    if (variants != null)
                        foreach (var v in variants)
                        {
                            var t = v?.Template;
                            if (t != null && resolved.All(r => r.t != t)) resolved.Add((t, v.label ?? t.label));
                        }
                }
                return resolved;
            }
        }

        public RoomTemplateDef Main
        {
            get
            {
                var vs = Variants;
                if (!main.NullOrEmpty())
                {
                    var m = vs.FirstOrDefault(v => v.t.defName == main).t;
                    if (m != null) return m;
                }
                return vs.Count > 0 ? vs[0].t : null;
            }
        }

        public string VariantLabel(RoomTemplateDef t)
        {
            foreach (var v in Variants)
                if (v.t == t) return v.label;
            return null;
        }

        private static Dictionary<RoomTemplateDef, RoomTypeDef> byTemplate;

        /// <summary>The room type a template belongs to, or null.</summary>
        public static RoomTypeDef Of(RoomTemplateDef t)
        {
            if (t == null) return null;
            if (byTemplate == null)
            {
                byTemplate = new Dictionary<RoomTemplateDef, RoomTypeDef>();
                foreach (var type in DefDatabase<RoomTypeDef>.AllDefsListForReading)
                    foreach (var v in type.Variants)
                        if (!byTemplate.ContainsKey(v.t)) byTemplate[v.t] = type;
            }
            return byTemplate.TryGetValue(t, out var r) ? r : null;
        }

        /// <summary>The template itself and its siblings, in the type's order.</summary>
        public static List<RoomTemplateDef> SiblingsOf(RoomTemplateDef t)
        {
            var type = Of(t);
            return type == null ? new List<RoomTemplateDef> { t } : type.Variants.Select(v => v.t).ToList();
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (variants == null || variants.Count == 0) yield return "a room type needs at least one variant";
            else
                foreach (var v in variants)
                    if (v == null || v.template.NullOrEmpty()) yield return "a variant has no template";
        }
    }
}
