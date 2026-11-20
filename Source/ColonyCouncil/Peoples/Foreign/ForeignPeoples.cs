using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The people of another mod's faction, given more than their mod gives them as each is made (the player's request of
    /// 17 November 2026: "bring a little more variety to maybe their gear, how they look like, ... potentially some maybe
    /// can do magic"; DESIGN.md "The Stormriders: their gear, their looks and some magic"; ForeignPeopleDef). For a pawn of
    /// a faction a ForeignPeopleDef names, by its role (its pawn kind): a few more pieces of gear, each a chance, worn only
    /// where they take the place of nothing the kind's own gear requires (unless the role names it) and nothing that
    /// protects more; a sidearm for the unarmed; a share with a class of A RimWorld of Magic, by Rimfolk's peoples' own gifts
    /// (Peoples/PeopleGifts.cs: only classes the mod has switched on and allows that people), or without the mod a psylink
    /// for its casters; a caster's own hood from the mod where it fits (their weapon stays: no wand). By their look: their
    /// cloth dyed its colours, a share with one of its hair styles (by the game's own style gender, never against their
    /// genes) and with a face or body tattoo (Ideology). No letter, nothing saved; their xenotype, race and the pawn kind's
    /// own kit stay. Only fresh pawns, with the deeper game hooks on: those already in a game keep what they have.
    /// Their age changes as Rimfolk's own peoples' do (Peoples/PeopleAges.cs; the player, 20 November 2026): at a pace of
    /// their own ways, no further than their roles are armed for (<see cref="Ways"/>), and a pawn of theirs made in an age
    /// their mod didn't make them for carries its role's arms of that age and nothing of a later age (<see cref="ToAge"/>).
    /// </summary>
    public static class ForeignPeoples
    {
        private static Dictionary<string, ForeignPeopleDef> byFaction;

        private static Dictionary<string, ForeignPeopleDef> Table
        {
            get
            {
                if (byFaction != null) return byFaction;
                var table = new Dictionary<string, ForeignPeopleDef>();
                foreach (var d in DefDatabase<ForeignPeopleDef>.AllDefsListForReading)
                    if (d?.factions != null)
                        foreach (string name in d.factions)
                            if (!name.NullOrEmpty() && !table.ContainsKey(name))
                                table[name] = d;
                return byFaction = table;
            }
        }

        /// <summary>The profile of a faction's people (never one of Rimfolk's own peoples, nor the colony), or null.</summary>
        public static ForeignPeopleDef Of(FactionDef d) => d == null || Peoples.IsOursDef(d) ? null : Table.TryGetValue(d.defName, out var x) ? x : null;

        public static ForeignPeopleDef Of(Faction f) => f == null || f.IsPlayer ? null : Of(f.def);

        public static bool Has(Faction f) => Of(f) != null;

        /// <summary>Their own tag for Rimfolk's lines (folk:darkelves), added to <paramref name="tags"/> (Peoples.AddTags).</summary>
        public static void AddTags(Faction f, ICollection<string> tags)
        {
            string tag = Of(f)?.tag;
            if (!tag.NullOrEmpty() && tags != null && !tags.Contains(tag)) tags.Add(tag);
        }

        /// <summary>The role a pawn kind plays among them (ForeignPeoplesMath.RoleOf), or null.</summary>
        public static ForeignRole RoleOf(ForeignPeopleDef d, PawnKindDef kind)
        {
            if (d?.roles == null || kind == null) return null;
            int i = ForeignPeoplesMath.RoleOf(d.roles.Select(r => (IList<string>)r?.kinds).ToList(), d.roles.Select(r => r?.name).ToList(), kind.defName, kind.trader);
            return i < 0 ? null : d.roles[i];
        }

        /// <summary>The classes their roles may have and each one's share (for their card and letters: PeoplesMagic.Among).</summary>
        public static IEnumerable<(string trait, float share)> ClassShares(Faction f)
        {
            var d = Of(f);
            if (d?.roles == null) yield break;
            foreach (var r in d.roles)
                if (r?.gifts != null)
                    foreach (var g in r.gifts)
                        if (g != null && !g.trait.NullOrEmpty())
                            yield return (g.trait, g.chance);
        }

        // ================= A new pawn of theirs =================

        /// <summary>What a new pawn of theirs is given, as it is made (Patch_ForeignPeoples). Returns the look's label, or null.</summary>
        public static string Fit(Pawn p)
        {
            if (p?.kindDef == null || p.Dead || p.RaceProps?.Humanlike != true || p.IsMutant || !p.DevelopmentalStage.Adult()) return null;
            var d = Of(p.Faction);
            var role = RoleOf(d, p.kindDef);
            if (role == null) return null;
            var look = LookFor(d);
            int age = Peoples.AgeOf(p.Faction);
            // Every age whole: in an age other than the one their mod made them for, the role's arms of the age first.
            if (age != Peoples.BeganIn(p.Faction)) ToAge(p, role, age);
            if (p.apparel != null && role.pieces != null)
                foreach (var piece in role.pieces)
                    if (piece?.apparel != null && ForeignPeoplesMath.Rolls(piece.chance, Rand.Value) && (!piece.ranged || Bow(p.equipment?.Primary?.def)))
                        WearOne(p, piece.apparel, role);
            Sidearm(p, role);
            var gifts = ForeignPeoplesMath.Gifts(PeoplesMagic.Loaded, role.gifts, role.giftsWithoutMagic);
            if (gifts != null && gifts.Count > 0) PeopleGifts.GiveFrom(p, gifts, age);
            Caster(p, role, age);
            if (look == null) return null;
            Style(p, look);
            Dye(p, role, look);
            return look.label;
        }

        /// <summary>A bow or a crossbow (a ranged weapon of the medieval age or before), the kind a quiver goes with.</summary>
        private static bool Bow(ThingDef w) => w != null && w.IsRangedWeapon && !PeopleAgesMath.Later((int)w.techLevel, PeopleAgesMath.Medieval);

        private static ForeignLook LookFor(ForeignPeopleDef d)
        {
            if (d?.looks == null) return null;
            int i = ForeignPeoplesMath.PickLook(d.looks.Select(l => l?.weight ?? 0f).ToList(), Rand.Value);
            return i < 0 ? null : d.looks[i];
        }

        /// <summary>One of the piece's apparel that is loaded and fits, unless they already wear one of them.</summary>
        private static void WearOne(Pawn p, List<string> names, ForeignRole role)
        {
            if (p.apparel.WornApparel.Any(w => w?.def != null && names.Contains(w.def.defName))) return;
            var defs = names.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(def => def?.apparel != null && def.apparel.PawnCanWear(p)).ToList();
            if (defs.Count > 0) Wear(p, defs.RandomElement(), role);
        }

        /// <summary>The piece worn where ForeignPeoplesMath.MayDisplace lets it take the place of whatever it can't be worn with.</summary>
        private static bool Wear(Pawn p, ThingDef def, ForeignRole role)
        {
            if (p.apparel == null || def?.apparel == null || !def.apparel.PawnCanWear(p)) return false;
            var displaced = p.apparel.WornApparel.Where(w => w?.def != null && !ApparelUtility.CanWearTogether(def, w.def, p.RaceProps.body)).ToList();
            ThingDef stuff = null;
            if (def.MadeFromStuff && !GenStuff.TryRandomStuffByCommonalityFor(def, out stuff, Peoples.TechOf(p.Faction))) return false;
            if (!(ThingMaker.MakeThing(def, stuff) is Apparel a)) return false;
            a.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityGeneratingPawn(p.kindDef, def), ArtGenerationContext.Outsider);
            float armour = a.GetStatValue(StatDefOf.ArmorRating_Sharp);
            foreach (var w in displaced)
            {
                bool replaceable = role?.replaceable != null && role.replaceable.Contains(w.def.defName);
                if (ForeignPeoplesMath.MayDisplace(Required(p.kindDef, w.def), replaceable, w.GetStatValue(StatDefOf.ArmorRating_Sharp), armour)) continue;
                a.Destroy();
                return false;
            }
            foreach (var w in displaced)
            {
                p.apparel.Remove(w);
                w.Destroy();
            }
            p.apparel.Wear(a, false);
            return true;
        }

        /// <summary>A piece the pawn kind's own gear requires (its required apparel, or a piece it names with a colour).</summary>
        private static bool Required(PawnKindDef kind, ThingDef def) =>
            kind.apparelRequired != null && kind.apparelRequired.Contains(def) || kind.specificApparelRequirements != null && kind.specificApparelRequirements.Any(r => r?.ApparelDef == def);

        private static void Sidearm(Pawn p, ForeignRole role)
        {
            if (role.sidearms.NullOrEmpty() || p.equipment == null) return;
            if (!ForeignPeoplesMath.Sidearm(p.equipment.Primary != null, !p.WorkTagIsDisabled(WorkTags.Violent), role.sidearmChance, Rand.Value)) return;
            var defs = role.sidearms.Select(n => DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(x => x != null && x.IsWeapon && x.equipmentType == EquipmentType.Primary).ToList();
            if (defs.Count == 0) return;
            var def = defs.RandomElement();
            ThingDef stuff = null;
            if (def.MadeFromStuff && !GenStuff.TryRandomStuffByCommonalityFor(def, out stuff, Peoples.TechOf(p.Faction))) return;
            if (!(ThingMaker.MakeThing(def, stuff) is ThingWithComps weapon)) return;
            weapon.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityGeneratingPawn(p.kindDef, def), ArtGenerationContext.Outsider);
            p.equipment.AddEquipment(weapon);
        }

        /// <summary>
        /// A caster of theirs wears their class's own hood from A RimWorld of Magic (PeoplesMagicMath.Garb), where it takes
        /// the place of nothing their kind requires; never a wand, so their own weapon stays.
        /// </summary>
        private static void Caster(Pawn p, ForeignRole role, int age)
        {
            if (!PeoplesMagic.Loaded || p.apparel == null) return;
            string trait = PeoplesMagic.Craft(p);
            string name = PeoplesMagicMath.Garb(trait, age);
            if (name != null && PeoplesMagicMath.Wand(name)) name = PeoplesMagicMath.Garb(trait, 3);
            if (name == null || PeoplesMagicMath.Wand(name)) return;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(name);
            if (def != null && !p.apparel.WornApparel.Any(w => w?.def == def)) Wear(p, def, role);
        }

        // ================= Their age (Peoples/PeopleAges.cs) =================

        /// <summary>
        /// A pawn of theirs in an age their mod didn't make them for: the role's set of arms of that age (its weapon tags'
        /// weapons from any loaded mod, its kit), then the every-age pass Rimfolk's own kinds go through (AgeArms.ToAge):
        /// nothing of a later age, the age's plain clothes on a bare body, the base game's arms of the age in place of a
        /// weapon of a later age or a fighter's bow in an age of guns.
        /// </summary>
        public static void ToAge(Pawn p, ForeignRole role, int age)
        {
            AgeArms.LastArmed = false;
            AgeArms.LastKit = null;
            if (role?.ageArms != null && role.ageArms.Count > 0) AgeArms.Arm(p, role.ageArms, age);
            AgeArms.ToAge(p, age, AgeArms.LastArmed, AgeArms.LastKit);
        }

        /// <summary>The highest age, the lowest and the pace of a people's ways (PeopleAges.Ways), for one that began in <paramref name="start"/>.</summary>
        public static AgeWays Ways(Faction f, int start)
        {
            var d = Of(f);
            int own = Peoples.BeganIn(f);
            int limit = d == null ? own : ArmedLimit(d, own);
            return new AgeWays { pace = ForeignAgesMath.Pace(d?.agePace ?? -1f), limit = Math.Max(limit, start), floor = PeopleAgesMath.Floor(d?.ageFloor ?? -1, start) };
        }

        private static readonly Dictionary<(ForeignPeopleDef, int), int> armedLimits = new Dictionary<(ForeignPeopleDef, int), int>();

        /// <summary>
        /// The highest age their ways and their arms allow (ForeignAgesMath.ArmedLimit): worked out once for the loaded mods,
        /// from the age their mod made them for.
        /// </summary>
        public static int ArmedLimit(ForeignPeopleDef d, int own)
        {
            if (d == null) return own;
            if (armedLimits.TryGetValue((d, own), out int limit)) return limit;
            var armed = new bool[4];
            for (int a = 0; a < 4; a++) armed[a] = a == own || ArmedFor(d, a);
            return armedLimits[(d, own)] = ForeignAgesMath.ArmedLimit(own, d.ageLimit, armed);
        }

        /// <summary>Every role of theirs is armed for the age (ForeignAgesMath.RoleArmed): its set of that age finds a weapon of the age, or a piece of its kit.</summary>
        public static bool ArmedFor(ForeignPeopleDef d, int age)
        {
            if (d?.roles == null) return false;
            foreach (var r in d.roles)
            {
                if (r == null) continue;
                var set = SetFor(r, age);
                if (!ForeignAgesMath.RoleArmed(CarriesArms(r), set != null, set != null && Finds(set, r, age))) return false;
            }
            return true;
        }

        /// <summary>The role's set of arms for an age, as AgeArms.Arm picks it (the first whose ages hold it), or null.</summary>
        public static PeopleAgeArms SetFor(ForeignRole r, int age) =>
            r?.ageArms?.FirstOrDefault(a => a != null && (!a.weaponTags.NullOrEmpty() || !a.apparel.NullOrEmpty()) && PeoplesDMath.AgeFits(age, a.ages.min, a.ages.max));

        /// <summary>A role carries arms: one of its kinds (loaded) is given weapons by its mod.</summary>
        private static bool CarriesArms(ForeignRole r) => Kinds(r).Any(k => !k.weaponTags.NullOrEmpty() && k.weaponMoney.max > 0f);

        private static IEnumerable<PawnKindDef> Kinds(ForeignRole r) =>
            r?.kinds == null ? Enumerable.Empty<PawnKindDef>() : r.kinds.Select(n => DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(k => k != null);

        /// <summary>
        /// A set finds what it names: a weapon of its tags of no later age than the people's, within its means (as
        /// AgeArms.Arm takes it: the set's own, else the least of its kinds' weapon budgets and a quarter more), or, a kit
        /// alone, a piece of it.
        /// </summary>
        private static bool Finds(PeopleAgeArms set, ForeignRole r, int age)
        {
            if (set.weaponTags.NullOrEmpty()) return set.apparel != null && set.apparel.Any(a => a != null);
            float means = set.money > 0f ? set.money : Kinds(r).Select(k => Math.Max(k.weaponMoney.max, 150f) * 1.25f).DefaultIfEmpty(187.5f).Min();
            return AgeArms.Weapons(set.weaponTags, means).Any(w => !PeopleAgesMath.Later((int)w.techLevel, age));
        }

        /// <summary>Their head's words in a letter of their age (CC_PeopleAge_Voice_DEFNAME_Rise or _Fall), or null.</summary>
        public static string VoiceKey(Faction f, bool rise)
        {
            var d = Of(f);
            return d == null ? null : "CC_PeopleAge_Voice_" + d.defName + (rise ? "_Rise" : "_Fall");
        }

        // ================= Their look =================

        private static void Style(Pawn p, ForeignLook look)
        {
            bool female = p.gender == Gender.Female;
            if (p.story != null && !look.hair.NullOrEmpty() && ForeignPeoplesMath.Rolls(look.hairChance, Rand.Value) && !HairByGenes(p))
            {
                var hairs = look.hair.Select(n => DefDatabase<HairDef>.GetNamedSilentFail(n)).Where(h => h != null && ForeignPeoplesMath.StyleFits(h.styleGender.ToString(), female)).ToList();
                if (hairs.Count > 0) p.story.hairDef = hairs.RandomElement();
            }
            if (!ModsConfig.IdeologyActive || p.style == null) return;
            var face = Tattoo(look.faceTattoos, look.faceTattooChance, TattooType.Face);
            if (face != null) p.style.FaceTattoo = face;
            var body = Tattoo(look.bodyTattoos, look.bodyTattooChance, TattooType.Body);
            if (body != null) p.style.BodyTattoo = body;
        }

        /// <summary>Their genes decide their hair (a gene that sets it or limits it): left as it is.</summary>
        private static bool HairByGenes(Pawn p) =>
            p.genes != null && p.genes.GenesListForReading.Any(g => g?.def != null && g.Active && (g.def.forcedHair != null || g.def.hairTagFilter != null));

        private static TattooDef Tattoo(List<string> names, float chance, TattooType type)
        {
            if (names.NullOrEmpty() || !ForeignPeoplesMath.Rolls(chance, Rand.Value)) return null;
            var defs = names.Select(n => DefDatabase<TattooDef>.GetNamedSilentFail(n)).Where(t => t != null && t.tattooType == type).ToList();
            return defs.Count > 0 ? defs.RandomElement() : null;
        }

        /// <summary>Their cloth in the look's colours: the role's pieces for its main colour and for its second.</summary>
        private static void Dye(Pawn p, ForeignRole role, ForeignLook look)
        {
            if (p.apparel == null) return;
            foreach (var a in p.apparel.WornApparel)
            {
                string n = a?.def?.defName;
                if (n == null) continue;
                if (look.main.a > 0f && role.dyeMain != null && role.dyeMain.Contains(n)) a.SetColor(look.main, false);
                else if (look.accent.a > 0f && role.dyeAccent != null && role.dyeAccent.Contains(n)) a.SetColor(look.accent, false);
            }
        }
    }

    /// <summary>
    /// A new pawn of a foreign people's faction (ForeignPeoples.Fit): a postfix on the game's making of a fresh pawn, after
    /// its traits, gear and hediffs (and A RimWorld of Magic's own roll of a class); a pawn the game takes from the world and
    /// dresses again keeps what it has. Off with the deeper game hooks.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "GenerateNewPawnInternal")]
    public static class Patch_ForeignPeoples
    {
        public static void Postfix(Pawn __result)
        {
            if (__result == null || !RimfolkPatches.On || !ForeignPeoples.Has(__result.Faction)) return;
            try
            {
                ForeignPeoples.Fit(__result);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("foreign peoples' gear and looks", e);
            }
        }
    }
}
