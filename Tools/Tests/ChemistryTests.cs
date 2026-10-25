using System.Linq;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// How two colonists get on (Social/ChemistryMath.cs, Social/Chemistry.cs): time together doesn't make everyone
/// friends. A good word counts for less between two who don't click and a cross word stings more, Rimfolk's warm moments
/// come up less often between them and frictions more, and the lasting feeling deepens with time only as far as their
/// compatibility goes. The defs agree with the stages, and comradeship adds less than it used to.
/// </summary>
public static class ChemistryTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "chemistry: " + what);
        }

        // ---------- What passes between them ----------
        float[] cs = { -3f, -1.5f, -1f, -0.5f, 0f, 0.5f, 1f, 1.5f, 3f };
        bool rising = true, falling = true, momentsRise = true, frictionsFall = true;
        for (int i = 1; i < cs.Length; i++)
        {
            rising &= ChemistryMath.WarmFactor(cs[i]) >= ChemistryMath.WarmFactor(cs[i - 1]);
            falling &= ChemistryMath.SoreFactor(cs[i]) <= ChemistryMath.SoreFactor(cs[i - 1]);
            momentsRise &= ChemistryMath.MomentFactor(cs[i], false) >= ChemistryMath.MomentFactor(cs[i - 1], false);
            frictionsFall &= ChemistryMath.MomentFactor(cs[i], true) <= ChemistryMath.MomentFactor(cs[i - 1], true);
        }
        Check(rising && falling, "a good word counts for more the better two get on, a cross word stings less");
        Check(momentsRise && frictionsFall, "warm moments come up more the better two get on, frictions less");
        Check(Near(ChemistryMath.WarmFactor(0.5f), 1f) && Near(ChemistryMath.SoreFactor(0.5f), 1f) && Near(ChemistryMath.MomentFactor(0.5f, true), 1f),
            "an ordinary pair (compatibility 0.5) is left as the game has it");
        Check(Near(ChemistryMath.WarmFactor(-9f), 0.35f) && Near(ChemistryMath.WarmFactor(9f), 1.15f), "flat beyond the ends: never nothing, never much more");
        Check(Near(ChemistryMath.Scaled(15f, -1.5f), 15f * 0.35f) && Near(ChemistryMath.Scaled(-4f, -1.5f), -4f * 1.35f) && ChemistryMath.Scaled(0f, -1.5f) == 0f,
            "a deep talk between two who don't click is a third of one; a slight stings a third more");
        Check(ChemistryMath.Scaled(10f, -3f) > 0f && ChemistryMath.Scaled(-10f, 3f) < 0f, "a good word stays good and a cross one cross, however two get on");

        // ---------- The lasting feeling ----------
        Check(ChemistryMath.Stage(-3f, ChemistryMath.FirstDays - 1) == ChemistryMath.None && ChemistryMath.Stage(3f, 0) == ChemistryMath.None,
            "nothing in the first days together");
        Check(ChemistryMath.Stage(0f, 100) == ChemistryMath.None && ChemistryMath.Stage(1.4f, 100) == ChemistryMath.None && ChemistryMath.Stage(-0.5f, 100) == ChemistryMath.None,
            "an ordinary pair has no feeling of this kind, however long together: what happens between them decides");
        Check(ChemistryMath.Stage(-0.7f, 5) == ChemistryMath.NoClick && ChemistryMath.Stage(-0.7f, 300) == ChemistryMath.NoClick,
            "two who barely clash only don't click, even after years");
        Check(ChemistryMath.Stage(-2f, 5) == ChemistryMath.NoClick && ChemistryMath.Stage(-2f, 15) == ChemistryMath.WrongWay && ChemistryMath.Stage(-2f, 30) == ChemistryMath.OnNerves,
            "two who really clash grate more the longer they live side by side");
        Check(ChemistryMath.Stage(-1.3f, 300) == ChemistryMath.WrongWay, "time deepens the feeling only as far as their compatibility goes");
        Check(ChemistryMath.Stage(1.6f, 6) == ChemistryMath.EasyCompany && ChemistryMath.Stage(2.6f, 6) == ChemistryMath.EasyCompany && ChemistryMath.Stage(2.6f, 15) == ChemistryMath.SameWavelength
              && ChemistryMath.Stage(1.6f, 300) == ChemistryMath.EasyCompany, "two who get on easily grow closer with time, as far as it goes");

        // ---------- The defs ----------
        var social = XDocument.Load(Path.Combine(root, "Defs/ThoughtDefs/Council_Thoughts_Social.xml"));
        var chem = social.Root.Elements("ThoughtDef").FirstOrDefault(d => (string)d.Element("defName") == "Council_Chemistry");
        var stages = chem?.Element("stages")?.Elements("li").Select(li => (float)li.Element("baseOpinionOffset")).ToList();
        Check(chem != null && (string)chem.Element("workerClass") == "ColonyCouncil.ThoughtWorker_Chemistry" && stages != null && stages.Count == 5,
            "the lasting feeling has its five stages in the defs");
        Check(stages != null && stages.Count == 5 && stages[0] < 0 && stages[1] < stages[0] && stages[2] < stages[1] && stages[3] > 0 && stages[4] > stages[3] && stages[2] >= -10f && stages[4] <= 6f,
            "in ChemistryMath's order: worse and worse, then better and better, and never more than a few points");
        var comrades = social.Root.Elements("ThoughtDef").FirstOrDefault(d => (string)d.Element("defName") == "Council_Tie_Comrades");
        var cstages = comrades?.Element("stages")?.Elements("li").Select(li => (float)li.Element("baseOpinionOffset")).ToList();
        Check(cstages != null && cstages.Count == 3 && cstages.All(v => v > 0f && v <= 8f),
            "fighting side by side is respect, not friendship: comrades add a few points (it once made every pair friends)");
        string code = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Social/Chemistry.cs"));
        Check(code.Contains("LovePartnerRelationExists") && code.Contains("IsColonist"), "partners and people outside the colony are left alone");
        string moments = File.ReadAllText(Path.Combine(root, "Source/ColonyCouncil/Social/Moments.cs"));
        Check(moments.Contains("Chemistry.MomentFactor"), "Rimfolk's moments ask how the two get on");
        return cases;
    }

    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.001f;
}
