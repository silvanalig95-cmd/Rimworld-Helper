using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What a people of one age builds a camp with (Defs/CampDefs/Rimfolk_CampKits.xml), role by role: the fire, a bed, a
    /// tent, the walls and door of a hut and of a tent, the windbreak, a table and its seats, lights, the butcher's and the
    /// crafter's spots, the stall, the picket, the stash. Each role lists the player's mods' pieces first (MayRequire, from
    /// the catalogue's active rows: docs/player-mods/buildings.tsv) and ends with the game's own, which is always there; the
    /// first that exists and that the people know how to build (its research within their age) is used, of the first
    /// listed material it can be made of. A kind's own pieces for a role (CampKindDef.structures) come before the kit's.
    /// </summary>
    public class CampKitDef : Def
    {
        /// <summary>tribal, medieval, industrial or spacer (CampBuildMath.AgeOf).</summary>
        public string age;
        public List<CampStructure> parts = new List<CampStructure>();

        public CampStructure Part(string role) => parts?.FirstOrDefault(p => p.role == role);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (!CampBuildMath.Ages.Contains(age)) yield return "camp kit age must be one of " + string.Join(", ", CampBuildMath.Ages);
            foreach (var role in CampKit.Roles)
                if (Part(role) == null) yield return "camp kit has no " + role;
        }
    }

    /// <summary>The camp kits by age, and the piece a camp of a people builds for each role.</summary>
    public static class CampKit
    {
        /// <summary>Every role a kit covers.</summary>
        public static readonly string[] Roles = { "fire", "bed", "tent", "hut", "tentwall", "door", "tentdoor", "wall", "table", "seat", "light", "butcher", "craft", "stall", "picket", "stash" };

        /// <summary>A people's age by their tech level (an undefined one by the colony's).</summary>
        public static string AgeOf(Faction f)
        {
            var tech = Peoples.TechOf(f, TechLevel.Undefined);
            if (tech == TechLevel.Undefined) tech = Faction.OfPlayer?.def?.techLevel ?? TechLevel.Neolithic;
            return CampBuildMath.AgeOf((int)tech);
        }

        public static CampKitDef For(string age) =>
            DefDatabase<CampKitDef>.AllDefsListForReading.FirstOrDefault(k => k.age == age)
            ?? DefDatabase<CampKitDef>.AllDefsListForReading.FirstOrDefault(k => k.age == "medieval");

        /// <summary>
        /// The piece a camp builds for a role, and of what: the kind's own for that role where it has a building that exists,
        /// else the kit's of the people's age. Null when nothing fits (a role neither has).
        /// </summary>
        public static CampStructure Piece(CampKindDef kind, string role, Faction f)
        {
            var own = kind?.structures?.FirstOrDefault(s => s.role == role && !s.defs.NullOrEmpty());
            if (own != null && Known(own.Def, f)) return own;
            var kit = For(AgeOf(f))?.Part(role);
            if (kit == null) return own?.Def != null ? own : null;
            // The kit's piece, the first the people know how to build.
            var def = kit.defs?.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d => d != null && d.category == ThingCategory.Building && Known(d, f));
            if (def == null) return own?.Def != null ? own : null;
            return new CampStructure { role = role, defs = new List<string> { def.defName }, stuff = kit.stuff, doors = kit.doors, count = kit.count };
        }

        /// <summary>The people know how to build it: what research it needs is within their age (their means).</summary>
        public static bool Known(ThingDef def, Faction f)
        {
            if (def == null) return false;
            int tech = (int)(Peoples.TechOf(f, TechLevel.Medieval));
            if (def.researchPrerequisites != null)
                foreach (var r in def.researchPrerequisites)
                    if (r != null && !CampBuildMath.Within((int)r.techLevel, tech)) return false;
            return true;
        }
    }
}
