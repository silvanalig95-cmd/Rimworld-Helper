using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>An animal's or creature's age as its pictures tell it: the game's first life stage, the last, or one between.</summary>
    public enum CreatureStage
    {
        Baby,
        Juvenile,
        Adult
    }

    /// <summary>
    /// One painted picture of an animal or creature (the player, 23 November 2026: "also please create lists for animals and
    /// creatures and implement image display for them too"): its record's name, the kind it shows (the creature's race, a
    /// ThingDef's name), the sex (any where the sexes look alike), its age, and the face it is of (its name where none is
    /// given).
    /// </summary>
    public sealed class CreatureRecord
    {
        public string name = "", race = "", sex = "any", character = "";
        public CreatureStage stage = CreatureStage.Adult;

        /// <summary>The face it is of: its character where given, else its name.</summary>
        public string Character => character.Length > 0 ? character : name;
    }

    /// <summary>A picture of an animal or creature read from the library folder's records.xml, and its path from the folder.</summary>
    public sealed class FolderCreature
    {
        public CreatureRecord record;
        public string texPath;
    }

    /// <summary>
    /// The painted pictures of animals and creatures (docs: DESIGN.md, "Portraits: animals and creatures";
    /// <c>docs/portrait-art/GUIDE.md</c>), without game types (Tools/Tests try it):
    /// <list type="bullet">
    /// <item>a record from records.xml's <c>ColonyCouncil.CreaturePortraitDef</c> (<see cref="Record"/>): <c>race</c> (the
    /// creature's ThingDef), <c>sex</c> (male, female or any) and <c>age</c> (baby, juvenile or adult);</item>
    /// <item>which pictures may stand for a creature (<see cref="Candidates"/>): of its race and sex (or any), of its age;
    /// a juvenile takes an adult's picture where its kind has none of its own age (the game draws a juvenile as a smaller
    /// adult), a baby only a baby's;</item>
    /// <item>which of them it gets (<see cref="Pick"/>): the same one every time for the same creature, each of its kind's
    /// pictures as often as the others, and a picture added to the library takes over only the creatures it ranks first
    /// for; one chosen by hand (<see cref="Next"/>) kept for it.</item>
    /// </list>
    /// </summary>
    public static class CreaturePortraitMath
    {
        public const string Element = "ColonyCouncil.CreaturePortraitDef";

        /// <summary>A creature's age from the game's life stage: the first of several a baby, the last (or the only one) an adult, any between a juvenile.</summary>
        public static CreatureStage StageOf(int index, int count)
        {
            if (count <= 1 || index >= count - 1) return CreatureStage.Adult;
            return index <= 0 ? CreatureStage.Baby : CreatureStage.Juvenile;
        }

        /// <summary>A stage's word as records.xml and missing.csv write it.</summary>
        public static string Word(CreatureStage s) => s == CreatureStage.Baby ? "baby" : s == CreatureStage.Juvenile ? "juvenile" : "adult";

        /// <summary>A stage from its word (also the people's words: a child or young one is a juvenile, a grown one an adult), or false.</summary>
        public static bool TryStage(string word, out CreatureStage stage)
        {
            stage = CreatureStage.Adult;
            switch ((word ?? "").Trim().ToLowerInvariant())
            {
                case "baby":
                case "newborn":
                    stage = CreatureStage.Baby;
                    return true;
                case "juvenile":
                case "young":
                case "child":
                    stage = CreatureStage.Juvenile;
                    return true;
                case "adult":
                case "grown":
                case "old":
                    stage = CreatureStage.Adult;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The record a <c>ColonyCouncil.CreaturePortraitDef</c> element gives (<paramref name="field"/> its fields' text),
        /// its unknown words told in <paramref name="problems"/> (each left at its default: any sex, an adult). Null where it
        /// names no race: nothing it could stand for.
        /// </summary>
        public static CreatureRecord Record(string name, Func<string, string> field, List<string> problems)
        {
            string F(string f) => (field(f) ?? "").Trim();
            var r = new CreatureRecord { name = name ?? "", race = F("race"), character = F("character") };
            if (r.race.Length == 0)
            {
                problems?.Add("no race: the creature it shows is not named, so it is left out");
                return null;
            }
            string sex = F("sex").ToLowerInvariant();
            if (sex == "male" || sex == "female" || sex == "any" || sex.Length == 0) r.sex = sex.Length == 0 ? "any" : sex;
            else problems?.Add("sex '" + sex + "' is not male, female or any");
            string age = F("age");
            if (age.Length > 0)
            {
                if (TryStage(age, out var st)) r.stage = st;
                else problems?.Add("age '" + age + "' is not baby, juvenile or adult");
            }
            return r;
        }

        /// <summary>
        /// The pictures (indices into <paramref name="records"/>) that may stand for a creature of <paramref name="race"/>
        /// at <paramref name="stage"/> with <paramref name="sex"/> (male, female, or empty for a creature without one): its
        /// race, its sex or any, its own age; for a juvenile without pictures of its age, the adults'. Ordered by name.
        /// </summary>
        public static List<int> Candidates(IList<CreatureRecord> records, string race, CreatureStage stage, string sex)
        {
            var own = new List<int>();
            var grown = new List<int>();
            if (records == null || string.IsNullOrEmpty(race)) return own;
            sex = (sex ?? "").ToLowerInvariant();
            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (r == null || !string.Equals(r.race, race, StringComparison.OrdinalIgnoreCase)) continue;
                if (r.sex != "any" && sex.Length > 0 && r.sex != sex) continue;
                if (r.stage == stage) own.Add(i);
                else if (stage == CreatureStage.Juvenile && r.stage == CreatureStage.Adult) grown.Add(i);
            }
            var list = own.Count > 0 ? own : grown;
            list.Sort((a, b) => string.CompareOrdinal(records[a].name, records[b].name));
            return list;
        }

        /// <summary>
        /// The picture a creature gets among <paramref name="names"/> (its candidates): the one ranked first for its id, so
        /// the same creature always gets the same one, the pictures are shared about evenly, and a picture added later takes
        /// over only the creatures it ranks first for. -1 where there is none.
        /// </summary>
        public static int Pick(int id, IList<string> names)
        {
            int best = -1;
            uint top = 0;
            if (names == null) return best;
            for (int i = 0; i < names.Count; i++)
            {
                uint h = Rank(id, names[i]);
                if (best < 0 || h > top)
                {
                    best = i;
                    top = h;
                }
            }
            return best;
        }

        /// <summary>The next picture after <paramref name="current"/> in <paramref name="names"/> (by their order), the first after the last; -1 where there is none other.</summary>
        public static int Next(IList<string> names, string current)
        {
            if (names == null || names.Count == 0) return -1;
            int at = -1;
            for (int i = 0; i < names.Count; i++)
                if (names[i] == current)
                    at = i;
            if (at < 0) return 0;
            return names.Count > 1 ? (at + 1) % names.Count : -1;
        }

        /// <summary>A number for a creature and a picture, the same every time (FNV-1a over the id and the name, mixed).</summary>
        public static uint Rank(int id, string name)
        {
            unchecked
            {
                uint h = 2166136261;
                for (int s = 0; s < 32; s += 8)
                {
                    h ^= (uint)(id >> s) & 0xFF;
                    h *= 16777619;
                }
                foreach (char c in name ?? "")
                {
                    h ^= c;
                    h *= 16777619;
                }
                h ^= h >> 15;
                h *= 0x2C1B3C6D;
                h ^= h >> 12;
                h *= 0x297A2D39;
                h ^= h >> 15;
                return h;
            }
        }

        /// <summary>The names of the records at these indices, in their order.</summary>
        public static List<string> Names(IList<CreatureRecord> records, IEnumerable<int> at) => at.Select(i => records[i].name).ToList();
    }
}
