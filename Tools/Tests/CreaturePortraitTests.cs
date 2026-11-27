using ColonyCouncil;

/// <summary>
/// The painted pictures of animals and creatures (UI/Faces/CreaturePortraitMath.cs, CreatureLibrary.cs; the player, 23
/// November 2026: "also please create lists for animals and creatures and implement image display for them too", and
/// "also include insects of course"): their records in records.xml, which pictures may stand for a creature, the pick
/// kept steady, and where the game shows them.
/// </summary>
public static class CreaturePortraitTests
{
    private const string Records =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<Defs>\r\n"
        + "  <ColonyCouncil.PortraitDef>\r\n    <defName>P_X</defName>\r\n    <texPath>characters-01/X</texPath>\r\n    <sex>male</sex>\r\n    <age>adult</age>\r\n  </ColonyCouncil.PortraitDef>\r\n"
        + "  <ColonyCouncil.CreaturePortraitDef>\r\n    <defName>C_muffalo_1</defName>\r\n    <texPath>creatures-01/c_muffalo_adult_1</texPath>\r\n    <race>Muffalo</race>\r\n    <sex>any</sex>\r\n    <age>adult</age>\r\n  </ColonyCouncil.CreaturePortraitDef>\r\n"
        + "  <ColonyCouncil.CreaturePortraitDef>\r\n    <defName>C_megaspider_1</defName>\r\n    <texPath>creatures-01/c_megaspider_adult_1</texPath>\r\n    <race>Megaspider</race>\r\n  </ColonyCouncil.CreaturePortraitDef>\r\n"
        + "  <ColonyCouncil.CreaturePortraitDef>\r\n    <defName>C_nothing</defName>\r\n    <texPath>creatures-01/c_nothing</texPath>\r\n    <age>elderly</age>\r\n  </ColonyCouncil.CreaturePortraitDef>\r\n"
        + "  <ColonyCouncil.CreaturePortraitDef>\r\n    <defName>C_deer_odd</defName>\r\n    <texPath>creatures-01/c_deer_odd</texPath>\r\n    <race>Deer</race>\r\n    <sex>both</sex>\r\n    <age>elderly</age>\r\n  </ColonyCouncil.CreaturePortraitDef>\r\n"
        + "</Defs>\r\n";

    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "creature pictures: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";

        // ---------------- Their records ----------------
        {
            var people = new List<FolderPortrait>();
            var creatures = new List<FolderCreature>();
            var problems = new List<string>();
            PortraitFolderMath.Parse(Records, people, null, problems, creatures);
            C(people.Count == 1 && people[0].record.name == "P_X", "records.xml: the person's picture read as before, never a creature's among the people");
            C(creatures.Count == 3 && creatures[0].record.race == "Muffalo" && creatures[0].texPath == "creatures-01/c_muffalo_adult_1"
              && creatures[0].record.sex == "any" && creatures[0].record.stage == CreatureStage.Adult,
                "records.xml: a creature's picture read into its own list, with its race, sex, age and path");
            C(creatures[1].record.sex == "any" && creatures[1].record.stage == CreatureStage.Adult && creatures[1].record.Character == "C_megaspider_1",
                "no sex or age given: any sex, an adult (an insect alike), its face its own name");
            C(creatures.All(c => c.record.name != "C_nothing") && problems.Any(p => p.StartsWith("C_nothing: no race")),
                "a record naming no race is left out and told");
            C(creatures[2].record.sex == "any" && problems.Any(p => p.Contains("sex 'both'")) && problems.Any(p => p.Contains("age 'elderly'")),
                "unknown words are told, each left at its default");
            var older = new List<FolderPortrait>();
            var olderProblems = new List<string>();
            PortraitFolderMath.Parse(Records, older, null, olderProblems);
            C(older.Count == 1 && !olderProblems.Any(p => p.Contains("is not ColonyCouncil")), "read without a creature list, its creatures are passed over quietly");
            var removed = new List<string>();
            string without = PortraitDeleteMath.WithoutRecords(Records, new HashSet<string> { "C_muffalo_1" }, removed);
            C(removed.SequenceEqual(new[] { "C_muffalo_1" }) && !without.Contains("C_muffalo_1") && without.Contains("C_megaspider_1") && without.Contains("P_X"),
                "deleting one for good takes its record out of records.xml and leaves the rest");
        }

        // ---------------- Which may stand for a creature ----------------
        {
            C(CreaturePortraitMath.StageOf(0, 3) == CreatureStage.Baby && CreaturePortraitMath.StageOf(1, 3) == CreatureStage.Juvenile && CreaturePortraitMath.StageOf(2, 3) == CreatureStage.Adult
              && CreaturePortraitMath.StageOf(0, 1) == CreatureStage.Adult && CreaturePortraitMath.StageOf(3, 3) == CreatureStage.Adult,
                "the game's life stages: the first a baby, the last an adult, between a juvenile; a mechanoid's only stage an adult");
            CreatureRecord R(string name, string race, string sex, CreatureStage st) => new CreatureRecord { name = name, race = race, sex = sex, stage = st };
            var recs = new List<CreatureRecord>
            {
                R("d3", "Deer", "male", CreatureStage.Adult), R("d1", "Deer", "female", CreatureStage.Adult), R("d2", "Deer", "any", CreatureStage.Adult),
                R("db", "Deer", "any", CreatureStage.Baby), R("m1", "Muffalo", "any", CreatureStage.Adult), R("mj", "Muffalo", "any", CreatureStage.Juvenile)
            };
            var buck = CreaturePortraitMath.Candidates(recs, "Deer", CreatureStage.Adult, "male");
            C(buck.Select(i => recs[i].name).SequenceEqual(new[] { "d2", "d3" }), "an adult male: its own sex's and the any-sex pictures, by name");
            C(CreaturePortraitMath.Candidates(recs, "deer", CreatureStage.Adult, "").Count == 3, "a creature without a sex (a mechanoid) takes any; the race read in any case");
            C(CreaturePortraitMath.Candidates(recs, "Deer", CreatureStage.Juvenile, "female").Select(i => recs[i].name).SequenceEqual(new[] { "d1", "d2" }),
                "a juvenile of a kind with no juvenile pictures takes the adults'");
            C(CreaturePortraitMath.Candidates(recs, "Muffalo", CreatureStage.Juvenile, "male").Select(i => recs[i].name).SequenceEqual(new[] { "mj" }),
                "a juvenile takes its own age's where its kind has them");
            C(CreaturePortraitMath.Candidates(recs, "Deer", CreatureStage.Baby, "male").Select(i => recs[i].name).SequenceEqual(new[] { "db" })
              && CreaturePortraitMath.Candidates(recs, "Muffalo", CreatureStage.Baby, "male").Count == 0,
                "a baby only a baby's picture: none where its kind has no baby's");
            C(CreaturePortraitMath.Candidates(recs, "Megaspider", CreatureStage.Adult, "").Count == 0, "no picture of its kind: none (noted as missing)");
        }

        // ---------------- The pick ----------------
        {
            var names = new List<string> { "a", "b", "c", "d" };
            bool steady = Enumerable.Range(1, 200).All(id => CreaturePortraitMath.Pick(id, names) == CreaturePortraitMath.Pick(id, names));
            var counts = new int[4];
            for (int id = 1; id <= 4000; id++) counts[CreaturePortraitMath.Pick(id, names)]++;
            C(steady && counts.All(c => c > 800 && c < 1200), $"the same creature always the same picture, the pictures shared about evenly ({string.Join(", ", counts)})");
            var more = new List<string>(names) { "e" };
            int moved = 0, wrong = 0;
            for (int id = 1; id <= 2000; id++)
            {
                int before = CreaturePortraitMath.Pick(id, names), after = CreaturePortraitMath.Pick(id, more);
                if (more[after] != names[before])
                {
                    moved++;
                    if (more[after] != "e") wrong++;
                }
            }
            C(wrong == 0 && moved > 250 && moved < 550, $"a picture added takes over only the creatures it ranks first for ({moved} of 2000), the rest keep theirs");
            C(CreaturePortraitMath.Pick(5, new List<string>()) == -1, "no picture: none");
            C(CreaturePortraitMath.Next(names, "b") == 2 && CreaturePortraitMath.Next(names, "d") == 0 && CreaturePortraitMath.Next(names, "x") == 0
              && CreaturePortraitMath.Next(new List<string> { "a" }, "a") == -1,
                "choosing another: the next by name, the first after the last; none where it is the only one");
        }

        // ---------------- Where the game shows them ----------------
        {
            string lib = Read("UI/Faces/CreatureLibrary.cs"), sources = Read("UI/Faces/PortraitSources.cs"), inspect = Read("UI/Faces/InspectPortrait.cs");
            string stage = Read("UI/Faces/FaceEngineStage.cs"), people = Read("UI/Faces/PortraitLibrary.cs"), del = Read("UI/Faces/PortraitDelete.cs");
            string folder = Read("UI/Faces/PortraitFolder.cs"), keeper = Read("UI/Faces/PortraitKeeper.cs"), needs = Read("UI/Faces/PortraitNeeds.cs");
            C(lib.Contains("public static bool IsCreature(Pawn p) => p?.RaceProps != null && !p.RaceProps.Humanlike")
              && people.Contains("records.Count == 0 || CreatureLibrary.IsCreature(p)) return null;"),
                "anything that isn't humanlike (an animal, an insect, a mechanoid, an entity) is a creature, and the people's library gives it nothing");
            C(sources.Contains("private static readonly IPortraitSource[] Sources = { PortraitLibrary.Source };") && sources.Contains("public static PortraitPicture ForStage(Pawn p)")
              && !stage.Contains("PortraitSources.For(p)") && stage.Contains("PortraitSources.ForStage(p)"),
                "a creature's picture only on the large portrait's stage (the small portraits frame a person's head), the stage asking for it everywhere");
            C(inspect.Contains("if (p.RaceProps.Humanlike) return p;\n            return PortraitSources.ForStage(p) != null ? p : null;")
              && inspect.Contains("if (creature) CreatureMenu(p);") && inspect.Contains("new Dialog_InfoCard(p)") && inspect.Contains("if (!creature) FaceEngine.DrawMark(mark, p);"),
                "at the inspect pane: a creature with a picture shows, a click opens the game's information, its own menu, no people's mark");
            C(lib.Contains("(ColonyCouncilMod.Settings?.portraitCreatures ?? true)") && Read("UI/SettingsCatalog.cs").Contains("Toggle(\"portraitCreatures\""),
                "a setting switches them off");
            C(folder.Contains("got.creatures);") && folder.Contains("if (got.pictures.Contains(PortraitFolderMath.FileKey(c.texPath))) Creatures.Add(c);")
              && folder.Contains("foreach (var c in Creatures.Where(x => names.Contains(x.record.name)).ToList())"),
                "the folder's reading keeps those whose file is there, and a deletion lets them go");
            C(keeper.Contains("Scribe_Collections.Look(ref creaturePicks, \"creaturePicks\"") && keeper.Contains("while (creaturePicks.Count > MaxCreaturePicks)")
              && lib.Contains("keeper.SetCreaturePick(p.thingIDNumber, names[next]);"),
                "a picture chosen by hand kept in the save, at most a few hundred");
            C(del.Contains("job.creature = true;") && del.Contains("keeper.LetGoCreaturePicture(n);") && del.Contains("foreach (var c in PortraitFolder.Creatures)"),
                "deleting one for good: the same dialog, deleted.csv line and bin, the others' files kept, its hand-picks let go");
            C(needs.Contains("what = \"creature\", kind = p.def.defName") && lib.Contains("PortraitNeeds.NoteCreature(p,"),
                "a creature without a picture of its kind is noted in missing.csv");
            C(lib.Contains("if (PokeWorldLink.IsPokemon(p)) return;") && lib.IndexOf("if (PokeWorldLink.IsPokemon(p)) return;") < lib.IndexOf("PortraitNeeds.NoteCreature(p,"),
                "never a Pokémon: they aren't painted (the player, 27 November 2026)");
            string guard = Read("Social/FamilyGuardians.cs");
            C(guard.Contains("FamilyLifeMath.RaiserCloseness(") && !guard.Contains("Family.Of(b, child) ? 50f"), "(the orphan) the council's question names the closest grown-up, never family first");
        }
        return n;
    }
}
