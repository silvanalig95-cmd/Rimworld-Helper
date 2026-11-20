using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// What Rimfolk gives the people of another mod's faction as they are made (Peoples/Foreign/ForeignPeoples.cs;
    /// Defs/PeopleDefs/Rimfolk_ForeignPeoples.xml; the player's request of 17 November 2026 for the Stormriders, the dark
    /// elves of Elves (Continued)): by role (its pawn kinds), a few more pieces of gear from that mod and the player's other
    /// mods, a sidearm for the unarmed, and a share with a class of A RimWorld of Magic (or a psylink without it); by look,
    /// the colours of their cloth, a hair style and tattoos. Everything is named by defName, the other mods' with MayRequire:
    /// without the faction's mod nothing matches, and a piece whose mod isn't loaded is never given. Their xenotype, race
    /// and the pawn kind's own kit stay as they are.
    /// </summary>
    public class ForeignPeopleDef : Def
    {
        /// <summary>The FactionDefs (by defName) whose people this is for.</summary>
        public List<string> factions;
        /// <summary>A tag their lines are written for (Peoples.AddTags), or none.</summary>
        public string tag;
        /// <summary>A people whose ways revolve around magic (a coven): more casters, and its card says so.</summary>
        public bool builtOnMagic;
        /// <summary>
        /// Their age over the years (Peoples/PeopleAges.cs, as Rimfolk's own peoples'; the player, 20 November 2026): the
        /// years an age takes at their own pace (unset, ForeignAgesMath.DefaultPace); the highest age their ways would take
        /// (unset, one above the age their mod made them for), lowered to the ages their roles are armed for
        /// (ForeignRole.ageArms); the lowest they fall back to (unset, one below).
        /// </summary>
        public float agePace = -1f;
        public int ageLimit = -1, ageFloor = -1;
        public List<ForeignRole> roles;
        public List<ForeignLook> looks;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (roles.NullOrEmpty()) yield return "a foreign people needs its roles";
            else
            {
                if (roles.Any(r => r == null || r.name.NullOrEmpty())) yield return "each role has a name";
                if (roles.Where(r => r?.name != null).GroupBy(r => r.name).Any(g => g.Count() > 1)) yield return "role names are unique";
                if (!roles.Any(r => r?.name == ForeignPeoplesMath.FolkRole)) yield return "a foreign people needs the role '" + ForeignPeoplesMath.FolkRole + "' for kinds no role names";
                foreach (var r in roles.Where(r => r != null))
                    foreach (var e in r.Errors(builtOnMagic))
                        yield return e;
            }
            if (float.IsNaN(agePace) || agePace <= 0f && agePace != -1f) yield return "agePace is the years an age takes, above 0 (or unset)";
            if (ageLimit < -1 || ageLimit > 3 || ageFloor < -1 || ageFloor > 3) yield return "ageLimit and ageFloor are ages 0 (tribal) to 3 (spacer), or unset";
            if (roles != null)
                foreach (var r in roles.Where(r => r?.ageArms != null))
                    foreach (var set in r.ageArms)
                        if (set == null || set.ages.min < 0 || set.ages.max > 3 || set.ages.min > set.ages.max || set.weaponTags.NullOrEmpty() && set.apparel.NullOrEmpty())
                            yield return "role '" + r.name + "': each set of arms names its ages (0 to 3) and its weapon tags or kit";
            if (looks != null)
                foreach (var l in looks.Where(l => l != null))
                    if (float.IsNaN(l.weight) || l.weight < 0f || !Chance(l.hairChance) || !Chance(l.faceTattooChance) || !Chance(l.bodyTattooChance))
                        yield return "look '" + l.label + "': a weight of 0 or more, chances from 0 to 1";
        }

        internal static bool Chance(float c) => !float.IsNaN(c) && c >= 0f && c <= 1f;
    }

    /// <summary>One role among a foreign people's pawns (a trader, a spearman, a sorceress), by its pawn kinds.</summary>
    public class ForeignRole
    {
        public string name;
        /// <summary>The pawn kinds (by defName) of this role; none for the folk role, which takes every kind no role names.</summary>
        public List<string> kinds;
        /// <summary>Casters by their kind's own name (a sorceress): their gifts may reach PeoplesGearMath.MaxShare.</summary>
        public bool casters;
        /// <summary>Each a chance of one more piece, one of its list that is loaded and fits.</summary>
        public List<ForeignPiece> pieces;
        /// <summary>The kind's own pieces (by defName) this role's pieces may take the place of (a civilian's cape or tunic).</summary>
        public List<string> replaceable;
        /// <summary>Pieces (by defName) dyed the look's main colour, and its second colour.</summary>
        public List<string> dyeMain, dyeAccent;
        /// <summary>One of these for someone who carries no weapon and can fight, by its chance.</summary>
        public List<string> sidearms;
        public float sidearmChance;
        /// <summary>With A RimWorld of Magic: its classes (and any other gift), one roll as Rimfolk's peoples' gifts.</summary>
        public List<PeopleGift> gifts;
        /// <summary>Without A RimWorld of Magic: these instead (a psylink), or nothing.</summary>
        public List<PeopleGift> giftsWithoutMagic;
        /// <summary>
        /// The arms of the ages their mod didn't make them for (PeopleKindExtension.ageArms, the same sets as Rimfolk's own
        /// kinds'): weapon tags of any mod and a kit, by age. A role that carries arms needs one for every age above the one
        /// its mod made it for, or the people rises no further (ForeignAgesMath.ArmedLimit).
        /// </summary>
        public List<PeopleAgeArms> ageArms;

        public IEnumerable<string> Errors(bool builtOnMagic)
        {
            // Written but emptied by MayRequire (the faction's mod isn't loaded) is no error: the role then plays no kind.
            if (name != ForeignPeoplesMath.FolkRole && name != ForeignPeoplesMath.TraderRole && kinds == null)
                yield return "role '" + name + "' names its pawn kinds";
            if (pieces != null && pieces.Any(p => p == null || p.chance <= 0f || !ForeignPeopleDef.Chance(p.chance)))
                yield return "role '" + name + "': each piece has a chance above 0, at most 1";
            if (!ForeignPeopleDef.Chance(sidearmChance)) yield return "role '" + name + "': sidearmChance from 0 to 1";
            foreach (var list in new[] { gifts, giftsWithoutMagic })
            {
                if (list.NullOrEmpty()) continue;
                if (!ForeignPeoplesMath.GiftsFit(list.Select(g => g?.chance ?? 0f).ToList(), casters, builtOnMagic))
                    yield return "role '" + name + "': its gifts' chances are above 0 and together no more than " + ForeignPeoplesMath.GiftCap(casters, builtOnMagic);
                foreach (var g in list.Where(g => g != null))
                {
                    if (PeoplesGearMath.GiftKinds(!g.trait.NullOrEmpty(), g.psylink.max > 0, !g.genes.NullOrEmpty()) != 1)
                        yield return "role '" + name + "': a gift is one of a class (trait), a psylink or genes";
                    if (!g.trait.NullOrEmpty() && PeoplesGearMath.MagicSetting(g.trait) == null) yield return "'" + g.trait + "' isn't one of A RimWorld of Magic's classes Rimfolk gives";
                }
            }
        }
    }

    /// <summary>A chance of one more piece: one of its apparel (by defName), the first loaded ones that fit taken at random.</summary>
    public class ForeignPiece
    {
        public float chance;
        /// <summary>Only for one who carries a ranged weapon (a quiver).</summary>
        public bool ranged;
        public List<string> apparel;
    }

    /// <summary>
    /// One look of a foreign people: the colours of their cloth (main and second), a share with one of its hair styles, and
    /// shares with a face or a body tattoo (Ideology). Each pawn has one look, by the looks' weights.
    /// </summary>
    public class ForeignLook
    {
        public string label;
        public float weight = 1f;
        /// <summary>The colours; unset (clear), nothing is dyed.</summary>
        public Color main = Color.clear, accent = Color.clear;
        public float hairChance;
        public List<string> hair;
        public float faceTattooChance;
        public List<string> faceTattoos;
        public float bodyTattooChance;
        public List<string> bodyTattoos;
    }
}
