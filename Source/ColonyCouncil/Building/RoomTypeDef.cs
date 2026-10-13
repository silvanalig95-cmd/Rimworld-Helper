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

        /// <summary>"cozy, standard, roomy and hut" for tooltips.</summary>
        public string VariantList => TextUtil.JoinAnd(Variants.Select(v => v.label).ToList());
    }

    /// <summary>
    /// What the room menus list: each room type once (its main size, or the first size the colony can build when the
    /// main one isn't available yet), every template that belongs to no type, and the player's saved rooms.
    /// </summary>
    public static class RoomMenu
    {
        public static List<RoomTemplateDef> Entries(Map map, bool onlyAvailable, bool withSaved = true)
        {
            var result = new List<RoomTemplateDef>();
            var seenTypes = new HashSet<RoomTypeDef>();
            foreach (var t in DefDatabase<RoomTemplateDef>.AllDefsListForReading)
            {
                if (!t.architect) continue;
                var type = RoomTypeDef.Of(t);
                if (type == null)
                {
                    if (!onlyAvailable || t.UnavailableReason(map) == null) result.Add(t);
                    continue;
                }
                if (!seenTypes.Add(type)) continue;
                var shown = Representative(type, map);
                if (shown != null && (!onlyAvailable || shown.UnavailableReason(map) == null)) result.Add(shown);
            }
            if (withSaved) result.AddRange(CustomRooms.Templates);
            return result;
        }

        /// <summary>The size a menu shows for a room type: the main one if the colony can plan it, else the first it can.</summary>
        public static RoomTemplateDef Representative(RoomTypeDef type, Map map)
        {
            var main = type.Main;
            if (main == null) return null;
            if (map == null || main.UnavailableReason(map) == null) return main;
            foreach (var (t, _) in type.Variants)
                if (t.UnavailableReason(map) == null) return t;
            return main;
        }

        /// <summary>The menu label: the room type's name for a room with several sizes, else the template's.</summary>
        public static string Label(RoomTemplateDef t)
        {
            var type = RoomTypeDef.Of(t);
            return type != null && type.Variants.Count > 1 && !type.label.NullOrEmpty() ? type.LabelCap.ToString() : t.LabelCap.ToString();
        }

        /// <summary>A line for tooltips: "Comes in these sizes and kinds: cozy, standard, roomy and hut."</summary>
        public static string SizesLine(RoomTemplateDef t)
        {
            var type = RoomTypeDef.Of(t);
            return type == null || type.Variants.Count < 2 ? null : "CC_Rooms_SizesTip".Translate(type.VariantList).Resolve();
        }

        /// <summary>Right-click choices for a room: plan it, find a spot, upgrade a built room, and delete a saved one.</summary>
        public static List<FloatMenuOption> Options(RoomTemplateDef t)
        {
            var opts = new List<FloatMenuOption>
            {
                new FloatMenuOption("CC_Build_Plan".Translate(), () => Designator_PlanRoom.Begin(t, false)),
                new FloatMenuOption("CC_Plan_FindSpot".Translate(), () => Designator_PlanRoom.Begin(t, true)),
                new FloatMenuOption("CC_Upgrade_Button".Translate(), () => Designator_PlanRoom.Begin(t, false, true))
            };
            if (t.custom != null)
            {
                var data = t.custom;
                opts.Add(new FloatMenuOption("CC_Yours_Delete".Translate(), () => CustomRooms.Delete(data)));
            }
            return opts;
        }
    }
}
