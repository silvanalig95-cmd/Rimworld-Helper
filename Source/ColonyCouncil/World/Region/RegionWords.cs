using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// The region's words (Languages/English/Keyed/Rimfolk_Region.xml): where a place lies from a home, the news of a town
    /// begun and built (the colony's means of hearing it by its own age, the founding's rite by theirs: Ages.Form), a
    /// family's name, why settlers chose a place, and the founder's first letter in their people's own voice.
    /// </summary>
    public static class RegionWords
    {
        private static int Now => Find.TickManager?.TicksGame ?? 0;

        /// <summary>"less than a day's walk to the east", "two days' walk to the north".</summary>
        public static string Where(Map home, PlanetTile tile, float tiles)
        {
            if (home == null || !tile.Valid) return "CC_Region_Where_Near".Translate().Resolve();
            string dir = NeighbourLetters.Direction(home.Tile, tile);
            float days = RegionMath.WalkDays(tiles);
            int level = RegionMath.DaysLevel(days);
            if (dir.NullOrEmpty()) return "CC_Region_Where_Near".Translate().Resolve();
            return ("CC_Region_Where_" + level).Translate(dir.Named("DIR"), Mathf.RoundToInt(days).ToString().Named("DAYS")).Resolve();
        }

        /// <summary>"Word from a traveler: settlers of the Lenlia under Tamsin Coor, from Ashford, have begun a town two days' walk to the north."</summary>
        public static string Begun(Map home, Faction people, Pawn founder, string mother, string where)
        {
            var form = Ages.Form(AgeFeature.Founding, home, people);
            string key = "CC_Region_Begun" + (founder == null ? "_NoFounder" : "") + (mother.NullOrEmpty() ? "_NoMother" : "");
            return key.Translate(form.word.CapitalizeFirst().Named("MEANS"), (people?.Name ?? "?").Named("PEOPLE"), (founder?.LabelShort ?? "").Named("FOUNDER"),
                (mother ?? "").Named("MOTHER"), where.Named("WHERE")).Resolve();
        }

        /// <summary>"Word from a traveler: Brightwater of the Lenlia stands two days' walk to the north, its new hearth lit before the elders, with Tamsin Coor as its headwoman."</summary>
        public static string Built(Map home, Settlement town, Pawn founder, string where)
        {
            var form = Ages.Form(AgeFeature.Founding, home, town.Faction);
            string key = founder == null ? "CC_Region_Built_NoFounder" : "CC_Region_Built";
            return key.Translate(form.word.CapitalizeFirst().Named("MEANS"), town.Name.Named("TOWN"), (town.Faction?.Name ?? "?").Named("PEOPLE"), where.Named("WHERE"),
                form.rite.Named("RITE"), (founder?.LabelShort ?? "").Named("FOUNDER"), RegionFaces.TitleOf(founder, town.Faction).Named("TITLE")).Resolve();
        }

        /// <summary>"the Coor family", "Yerrik's kin".</summary>
        public static string HouseName(LedgerHouse h) => h == null ? "" : (h.kin ? "CC_Region_House_Kin" : "CC_Region_House_Family").Translate(h.name.Named("NAME")).Resolve();

        /// <summary>A town's founding families, "the Coor family and the Ashby family"; empty with none.</summary>
        public static string Families(int town)
        {
            var names = WorldLedger.HousesOf(town).Where(h => h.founding).Select(HouseName).Where(s => !s.NullOrEmpty()).ToList();
            return names.Count == 0 ? "" : TextUtil.JoinAnd(names);
        }

        /// <summary>Why settlers chose the place, as a short phrase ("for the road through it"); null when not known.</summary>
        public static string Why(LedgerTown rec)
        {
            if (rec?.why == null || rec.why == "mother" && rec.motherName.NullOrEmpty()) return null;
            string key = "CC_Region_Why_" + rec.why;
            return key.CanTranslate() ? key.Translate((rec.motherName ?? "").Named("MOTHER")).Resolve() : null;
        }

        /// <summary>
        /// The founder's first letter to a home near the new town: who they are and where, why they chose the place, what
        /// their people remember of the colony (a good turn, or a wrong they want no part of), the families who came, and
        /// a farewell, each in their people's own voice (their age, or the Empire's). It looks at the town.
        /// </summary>
        public static void FoundersLetter(LedgerTown rec, Settlement town, Pawn founder, Map home)
        {
            if (rec == null || town == null || founder == null || home == null) return;
            try
            {
                var people = town.Faction;
                string voice = Ages.IsEmpire(people) ? "Empire" : Ages.Of(people).ToString();
                float tiles = Find.WorldGrid.ApproxDistanceInTiles(home.Tile, town.Tile);
                string colony = home.Parent is Settlement hs && hs.HasName ? hs.Name : Faction.OfPlayer.Name;
                var args = new List<NamedArgument>
                {
                    founder.LabelShort.Named("FOUNDER"), RegionFaces.TitleOf(founder, people).Named("TITLE"), town.Name.Named("TOWN"), (people?.Name ?? "").Named("PEOPLE"),
                    colony.Named("COLONY"), Where(home, town.Tile, tiles).Named("WHERE"), (rec.motherName ?? "").Named("MOTHER")
                };
                var parts = new List<string>();
                string open = "CC_Region_Letter_Open_" + voice + (rec.motherName.NullOrEmpty() ? "_NoMother" : "");
                parts.Add(open.Translate(args.ToArray()).Resolve());
                string why = "CC_Region_Letter_Why_" + (rec.why ?? RegionMath.AnyLand);
                if (why.CanTranslate() && (rec.why != "mother" || !rec.motherName.NullOrEmpty())) parts.Add(why.Translate(args.ToArray()).Resolve());
                string memory = Memory(people);
                if (!memory.NullOrEmpty()) parts.Add(memory);
                string families = Families(town.ID);
                if (!families.NullOrEmpty()) parts.Add("CC_Region_Letter_Families".Translate(families.Named("FAMILIES")).Resolve());
                parts.Add(("CC_Region_Letter_Close_" + voice).Translate(args.ToArray()).Resolve());
                string label = ("CC_Region_Letter_Label_" + voice).Translate(town.Name.Named("TOWN")).Resolve();
                Tell.Letter(NewsKind.World, Importance.Notable, home, label, string.Join("\n\n", parts), LetterDefOf.NeutralEvent, new LookTargets(town), people);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("region: a founder's letter", e);
            }
        }

        /// <summary>What their people remember of the colony, for the founder's letter: the strongest good turn, else the strongest wrong; null for nothing.</summary>
        private static string Memory(Faction people)
        {
            var mem = FactionMemory.Instance;
            if (people == null || mem == null || !FactionMemory.On || !FactionMemory.Remembers(people)) return null;
            int now = Now;
            var mine = mem.Of(people).Where(d => d != null && d.StrengthAt(now) >= 2f).OrderByDescending(d => d.StrengthAt(now)).ToList();
            var good = mine.FirstOrDefault(d => d.side == DeedSide.Favor);
            if (good != null) return "CC_Region_Letter_Deed".Translate(FactionMemory.Text(good).Named("DEED"), people.Name.Named("PEOPLE")).Resolve();
            var bad = mine.FirstOrDefault(d => d.side == DeedSide.Wrong);
            if (bad != null) return "CC_Region_Letter_Wrong".Translate(FactionMemory.Text(bad).Named("WRONG"), people.Name.Named("PEOPLE")).Resolve();
            return null;
        }
    }
}
