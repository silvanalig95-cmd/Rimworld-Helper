using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// How A RimWorld of Magic (Continued) keeps a pawn's magic now, in pure rules (Integrations/MagicModel.cs reads it;
    /// docs/review-magic.md; Tools/Tests/ReviewMagicTests.cs). Two things moved from where the original kept them:
    /// <list type="bullet">
    /// <item>A class. With the continuation's new class progression (a switch in its settings, on in the player's game)
    /// a colonist's class is a vocation trait (mage, warrior, warrior-mage) and one or two disciplines learned from the
    /// class books, kept in its own component, not the class trait the original gave. Each discipline stands for the class
    /// trait of the same art (its own defs name it, ClassDisciplineDef.legacyTrait): Rimfolk reads a discipline as that
    /// class, so everything that asks for a class (the mage, fighter and healer tags, a craft, a god's domain) sees it.</item>
    /// <item>A spell learned. The continuation keeps every spell a pawn knows in one knowledge store (RWoM Core's
    /// PawnSpellKnowledge, entries such as rwom.tm_arcanebolt with learned and a tier) and still writes the old power lists
    /// beside it; its own casting asks the store first and the old list only when the store has no entry. Rimfolk does the
    /// same.</item>
    /// </list>
    /// </summary>
    public static class MagicModelMath
    {
        /// <summary>The continuation's vocation traits: a colonist of its class progression has one of them.</summary>
        public const string VocationMage = "TM_VocationMage", VocationWarrior = "TM_VocationWarrior", VocationWarriorMage = "TM_VocationWarriorMage";

        public static readonly IReadOnlyCollection<string> Vocations = new HashSet<string> { VocationMage, VocationWarrior, VocationWarriorMage };

        /// <summary>
        /// Each discipline of the continuation (its profile id) and the class trait it stands for, from its own defs
        /// (TM_ClassDisciplines.xml and the careers: a magic path names its legacyTrait; a martial discipline is the might
        /// class of the same name). The Shadow's two halves (its magic path and its martial shadow walker) are both the
        /// Shadow. The wanderer is <see cref="Wanderer"/>.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Disciplines = new Dictionary<string, string>
        {
            ["rwom.fire"] = "InnerFire", ["rwom.ice"] = "HeartOfFrost", ["rwom.lightning"] = "StormBorn", ["rwom.priest"] = "Priest",
            ["rwom.necromancer"] = "Necromancer", ["rwom.arcanist"] = "Arcanist", ["rwom.paladin"] = "Paladin", ["rwom.summoner"] = "Summoner",
            ["rwom.druid"] = "Druid", ["rwom.geomancer"] = "Geomancer", ["rwom.bard"] = "TM_Bard", ["rwom.warlock"] = "Warlock",
            ["rwom.succubus"] = "Succubus", ["rwom.technomancer"] = "Technomancer", ["rwom.bloodmage"] = "BloodMage", ["rwom.enchanter"] = "Enchanter",
            ["rwom.chronomancer"] = "Chronomancer", ["rwom.brightmage"] = "TM_Brightmage", ["rwom.shaman"] = "TM_Shaman",
            ["rwom.golemancer"] = "TM_Golemancer", ["rwom.empath"] = "TM_Empath", ["rwom.chaos"] = "ChaosMage", ["rwom.shadow"] = "TM_TheShadow",
            ["rwom.weather"] = "TM_WeatherMage",
            ["rwom.gladiator"] = "Gladiator", ["rwom.sniper"] = "TM_Sniper", ["rwom.bladedancer"] = "Bladedancer", ["rwom.ranger"] = "Ranger",
            ["rwom.faceless"] = "Faceless", ["rwom.psionic"] = "TM_Psionic", ["rwom.deathknight"] = "DeathKnight", ["rwom.monk"] = "TM_Monk",
            ["rwom.commander"] = "TM_Commander", ["rwom.supersoldier"] = "TM_SuperSoldier", ["rwom.apothecary"] = "TM_Apothecary",
            ["rwom.shadowwalker"] = "TM_TheShadow"
        };

        /// <summary>The wanderer's discipline: the magic wanderer (TM_Wanderer), the martial wayfarer (TM_Wayfarer), or both.</summary>
        public const string Wanderer = "rwom.wanderer";

        /// <summary>RWoM Core names a pawn's classic class trait as a discipline too: "legacy.(trait).(degree)".</summary>
        public const string LegacyPrefix = "legacy.";

        /// <summary>
        /// The class traits a discipline stands for, by its id and the domain it is held in (magic, might, both: the
        /// continuation's DisciplineDomain, 1, 2 and 3). A classic class read through RWoM Core ("legacy.InnerFire.0") is its
        /// own trait. Nothing for an id Rimfolk doesn't know.
        /// </summary>
        public static IEnumerable<string> ClassesOf(string id, int domain)
        {
            if (string.IsNullOrEmpty(id)) yield break;
            if (id == Wanderer)
            {
                if ((domain & 1) != 0 || domain == 0) yield return "TM_Wanderer";
                if ((domain & 2) != 0) yield return "TM_Wayfarer";
                yield break;
            }
            if (Disciplines.TryGetValue(id, out var trait))
            {
                yield return trait;
                yield break;
            }
            if (id.StartsWith(LegacyPrefix, StringComparison.Ordinal))
            {
                string rest = id.Substring(LegacyPrefix.Length);
                int dot = rest.LastIndexOf('.');
                string name = dot > 0 ? rest.Substring(0, dot) : rest;
                if (name.Length > 0) yield return name;
            }
        }

        /// <summary>
        /// The continuation's own name for a spell when its def says none (SpellIds.Convention): "rwom." and the def's name
        /// in lower case; the one ability two mods ship under nearly the same name, Kure's TM_Shadowstep, is
        /// "kure.tm_shadowstep".
        /// </summary>
        public static string ConventionSpellId(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            return defName == "TM_Shadowstep" ? "kure.tm_shadowstep" : "rwom." + defName.ToLowerInvariant();
        }

        /// <summary>
        /// Whether a spell counts as learned: the knowledge store's entry when it has one (null when it has none, or can't be
        /// read), else the old power list's mark, as the continuation's own casting decides it.
        /// </summary>
        public static bool Learned(bool? knowledge, bool legacy) => knowledge ?? legacy;

        /// <summary>
        /// RWoM Core's answer for a spell's entry (KnowledgeStatus): an entry was found (Available, or MissingDefinition for
        /// a spell whose def is gone) and its learned mark stands; anything else (no provider, an import still pending, no
        /// record) leaves it to the next way of reading.
        /// </summary>
        public static bool EntryFound(string status) => status == "Available" || status == "MissingDefinition";
    }
}
