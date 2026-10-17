using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// A person's character (Persona/PersonaMath.cs): nature, lasting marks and fading drift add up within bounds, drift
/// fades by its half-life, a person already moved one way moves less with each new push, words and tags appear only
/// once a side shows, the same person is described the same way, and the trait table names only real sides.
/// </summary>
public static class PersonaTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "persona: " + what);
        }

        // ---------- Adding up ----------
        Check(PersonaMath.Total(90f, 40f, 50f) == 100f && PersonaMath.Total(-90f, -40f, -50f) == -100f, "nature, marks and drift never go past -100..100");
        Check(PersonaMath.Total(0f, 999f, 0f) == PersonaMath.MaxLasting && PersonaMath.Total(0f, 0f, -999f) == -PersonaMath.MaxDrift, "lasting marks and drift each have their cap");
        Check(Math.Abs(PersonaMath.Faded(40f, PersonaMath.DriftHalfLifeDays) - 20f) < 0.01f, "drift halves in its half-life");
        Check(PersonaMath.Faded(40f, 0f) == 40f && PersonaMath.Faded(0.04f, 10f) == 0f, "no time, no fading; a sliver is let go");
        float once = PersonaMath.Add(0f, 10f, 50f), again = PersonaMath.Add(40f, 10f, 50f) - 40f;
        Check(once == 10f && again > 0f && again < once, "someone already moved one way moves less with the next push");
        Check(PersonaMath.Add(40f, -10f, 50f) == 30f, "a push back the other way counts in full");
        Check(PersonaMath.Add(49f, 100f, 50f) == 50f, "never past the cap");

        // ---------- Words and tags ----------
        Check(PersonaMath.Degree(29f) == 0 && PersonaMath.Degree(30f) == 1 && PersonaMath.Degree(60f) == 2 && PersonaMath.Degree(-61f) == -2, "a side shows from 30 and is strong from 60");
        Check(PersonaMath.Tag(PersonaAxis.Temper, 45f) == "persona:hothead" && PersonaMath.Tag(PersonaAxis.Temper, -45f) == "persona:calm"
              && PersonaMath.Tag(PersonaAxis.Warmth, 10f) == null, "tags only once a side shows");
        var tags = new HashSet<string>();
        foreach (var a in PersonaMath.All)
        {
            tags.Add(PersonaMath.Tag(a, 50f));
            tags.Add(PersonaMath.Tag(a, -50f));
        }
        Check(tags.Count == PersonaMath.Axes * 2, "every side has two tags of its own");
        var v = new float[] { 40f, -70f, 10f, 40f, 0f, 35f, -20f, 90f };
        var top = PersonaMath.Strongest(v, 3);
        Check(top.Count == 3 && top[0] == 7 && top[1] == 1 && top[2] == 0, "the sides that show most, strongest first, ties by order");
        Check(PersonaMath.Strongest(new float[8], 3).Count == 0, "a plain person has no side that shows");

        // ---------- Seeds and likeness ----------
        bool stable = true, inRange = true, differ = false;
        for (int id = 1; id < 400; id++)
            foreach (var a in PersonaMath.All)
            {
                float s = PersonaMath.Seed(id, a, 12f);
                stable &= s == PersonaMath.Seed(id, a, 12f);
                inRange &= s >= -12f && s <= 12f;
                differ |= Math.Abs(s - PersonaMath.Seed(id + 1, a, 12f)) > 1f;
            }
        Check(stable && inRange && differ, "a personal lean is the same each time, small, and differs between people");
        var same = new float[] { 10, 20, 30, 40, 50, 60, 70, 80 };
        var opposite = same.Select(x => -x).ToArray();
        Check(PersonaMath.Likeness(same, same) == 1f && PersonaMath.Likeness(same, opposite) < 0.7f, "the same person is fully alike; opposites much less");
        Check(Math.Abs(PersonaMath.Persuaded(-1f, 1f, 0.1f) - (-0.8f)) < 0.001f && PersonaMath.Persuaded(0f, 1f, 5f) == 0.5f, "a view moves toward another's, never more than halfway at once");
        Check(PersonaMath.PairKey(3, 9) == PersonaMath.PairKey(9, 3) && PersonaMath.PairKey(3, 9) != PersonaMath.PairKey(3, 8), "a pair is the same pair either way round");

        // ---------- The trait table ----------
        var axes = new HashSet<string>(PersonaMath.All.Select(a => a.ToString().ToLowerInvariant()));
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
        {
            var doc = XDocument.Load(file);
            foreach (var def in doc.Root!.Elements("ColonyCouncil.PersonaNatureDef"))
                foreach (var li in def.Element("traits")?.Elements("li") ?? Enumerable.Empty<XElement>())
                {
                    string trait = li.Element("trait")?.Value ?? "?";
                    var moves = li.Elements().Where(e => e.Name != "trait" && e.Name != "degree").ToList();
                    Check(moves.Count > 0 && moves.All(e => axes.Contains(e.Name.LocalName) && float.TryParse(e.Value, out float f) && Math.Abs(f) <= 100f),
                        "trait " + trait + " moves only real sides, within -100..100");
                }
            foreach (var def in doc.Root!.Elements("ColonyCouncil.PersonaMarkDef"))
            {
                var moves = def.Element("shifts")?.Elements().ToList() ?? new List<XElement>();
                Check(moves.Count > 0 && moves.All(e => axes.Contains(e.Name.LocalName)), "mark " + def.Element("defName")?.Value + " moves only real sides");
            }
        }
        return cases;
    }
}
