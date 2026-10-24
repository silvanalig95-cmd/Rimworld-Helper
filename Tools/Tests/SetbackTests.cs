using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// Setbacks (SetbackMath, Defs/ThoughtDefs/Council_Thoughts_Setbacks*.xml): when they come and how big, and that each
/// one is moderate, says which setting it answers to, and has a hint for what helps.
/// </summary>
public static class SetbackTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int cases = 0;
        void Check(bool ok, string what)
        {
            cases++;
            check(ok, "setbacks: " + what);
        }

        // Never less than a whole point, whatever the setting.
        Check(Math.Abs(SetbackMath.AtLeastOnePoint(-1f, 0.5f) - 1f) < 0.001f, "gentle -1 is still -1");
        Check(Math.Abs(SetbackMath.AtLeastOnePoint(-4f, 0.5f) - 0.5f) < 0.001f, "gentle -4 is -2");
        Check(Math.Abs(SetbackMath.AtLeastOnePoint(-1f, 1.5f) - 1.5f) < 0.001f, "demanding -1 is -1.5");
        Check(Math.Abs(SetbackMath.AtLeastOnePoint(2f, 0.5f) - 0.5f) < 0.001f, "a good side is left alone");
        Check(SetbackMath.AtLeastOnePoint(-2f, 0f) == 0f, "off stays off");

        // A wish still waiting.
        Check(SetbackMath.WaitStage(2.9f, 5) == -1, "not before three days");
        Check(SetbackMath.WaitStage(3f, 0) == -1, "nobody else granted: nothing to mind");
        Check(SetbackMath.WaitStage(3f, 1) == 0 && SetbackMath.WaitStage(9f, 2) == 1 && SetbackMath.WaitStage(9f, 7) == 1, "one, then two or more");

        // A decree that drags on.
        Check(SetbackMath.DragStage(1.9f, true, true) == -1, "not before two days");
        Check(SetbackMath.DragStage(2f, false, false) == 0 && SetbackMath.DragStage(4f, true, false) == 1 && SetbackMath.DragStage(4f, false, true) == 1, "dislike, hate, ignored");

        // Not everyone agreed: an answer against someone's nature.
        float[] harsh = { -4f, 0f, 0f, 0f, 0f };
        float[] kind = { 1f, 0.6f, 0f, 0.4f, 0f };
        float[] psychopath = { -0.8f, 0f, 0f, -0.3f, 0f };
        Check(Math.Abs(SetbackMath.Clash(harsh, kind) - 4f) < 0.001f && SetbackMath.ClashStage(SetbackMath.Clash(harsh, kind)) == 0, "a harsh answer and a kind colonist");
        Check(SetbackMath.Clash(harsh, psychopath) < 0f && SetbackMath.ClashStage(SetbackMath.Clash(harsh, psychopath)) == -1, "a harsh answer suits a psychopath");
        Check(SetbackMath.ClashStage(6f) == 1 && SetbackMath.ClashStage(2.9f) == -1, "strong and mild clashes");
        Check(SetbackMath.ToldSo(2f, true) && !SetbackMath.ToldSo(2f, false) && !SetbackMath.ToldSo(1f, true), "told them so only when the gamble went wrong");
        Check(SetbackMath.Clash(null, kind) == 0f && SetbackMath.Clash(new float[2], kind) == 0f, "no shift, no clash");

        // Work.
        Check(SetbackMath.WreckStage(500f, false) == 0 && SetbackMath.WreckStage(12000f, false) == 1 && SetbackMath.WreckStage(0f, true) == 1, "a big piece or art is the worst");
        Check(SetbackMath.CropStage(3, true, true) == -1, "a few plants are bad luck");
        Check(SetbackMath.CropStage(4, true, false) == 0 && SetbackMath.CropStage(4, false, false) == 1, "blight and frost");
        Check(SetbackMath.CropStage(30, true, false) == 2 && SetbackMath.CropStage(15, false, true) == 2 && SetbackMath.CropStage(14, false, true) == 1, "a whole field, sooner for a keen grower");
        Check(SetbackMath.LongHaul(60) && !SetbackMath.LongHaul(59), "long hauls");
        Check(SetbackMath.Makeshift(0.5f, 1f) && !SetbackMath.Makeshift(1f, 1f) && !SetbackMath.Makeshift(0.5f, 0.6f), "makeshift benches");

        // Home and worries.
        Check(SetbackMath.Envious(1, 4) && !SetbackMath.Envious(2, 4), "envy from three grades");
        Check(SetbackMath.QuietStage(2, 4, 9.9f) == -1 && SetbackMath.QuietStage(3, 3, 30f) == -1 && SetbackMath.QuietStage(5, 4, 30f) == -1, "a gathering lately, or enough to do");
        Check(SetbackMath.QuietStage(2, 3, 10f) == 0 && SetbackMath.QuietStage(1, 3, 10f) == 1 && SetbackMath.QuietStage(0, 1, 20f) == 0, "quiet days, by what they expect");
        Check(SetbackMath.HardWeek(3) && !SetbackMath.HardWeek(2), "a hard week");
        Check(SetbackMath.SickStage(false, false) == 0 && SetbackMath.SickStage(true, false) == 1 && SetbackMath.SickStage(false, true) == 1, "worry");
        Check(!SetbackMath.NemesisWorry(7f) && SetbackMath.NemesisWorry(8f) && SetbackMath.NemesisWorry(60f) && !SetbackMath.NemesisWorry(61f), "a nemesis still out there");

        // The newer systems: a feast's dish, guests at the table.
        Check(SetbackMath.BurnedChance(2) > SetbackMath.BurnedChance(6) && SetbackMath.BurnedChance(6) > SetbackMath.BurnedChance(10)
              && SetbackMath.BurnedChance(10) > SetbackMath.BurnedChance(16) && SetbackMath.BurnedChance(16) > 0f && SetbackMath.BurnedChance(0) <= 0.25f,
            "a better cook burns the feast less, and a novice not always");
        Check(SetbackMath.QuarrelChance > 0f && SetbackMath.QuarrelChance < 0.5f, "rivals at the table don't always quarrel");
        Check(SetbackMath.GuestsStage(5f) == -1 && SetbackMath.GuestsStage(3.9f) == 0 && SetbackMath.GuestsStage(1.5f) == 1, "guests mind only while the stores run low");

        // The defs: every setback moderate, with its setting and a hint.
        var thoughts = new Dictionary<string, XElement>();
        var hinted = new HashSet<string>();
        var pools = new HashSet<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(file);
            }
            catch
            {
                continue;
            }
            foreach (var el in doc.Root!.Elements())
            {
                string name = el.Element("defName")?.Value;
                if (name == null) continue;
                if (el.Name.LocalName == "ThoughtDef") thoughts[name] = el;
                else if (el.Name.LocalName == "ColonyCouncil.MoodHintDef")
                    foreach (var t in el.Element("thoughts")?.Elements("li") ?? Enumerable.Empty<XElement>())
                        hinted.Add(t.Value.Trim());
                else if (el.Name.LocalName == "ColonyCouncil.FlavorPoolDef") pools.Add(name);
            }
        }
        var setbacks = thoughts.Keys.Where(n => n.StartsWith("Council_Setback_")).ToList();
        Check(setbacks.Count >= 30, $"the setbacks are all there ({setbacks.Count})");
        string[] real = { "Council_Friction_CookingCriticised", "Council_Friction_Mocked", "Council_Friction_CreditTaken", "Council_Friction_ReturnedBroken",
            "Council_Nuisance_SpoiltStores", "Council_Nuisance_NoCoat", "Council_Nuisance_UglyBedroom" };
        foreach (var n in real) Check(thoughts.ContainsKey(n), $"{n} is there");
        foreach (var n in setbacks.Concat(real.Where(thoughts.ContainsKey)))
        {
            var el = thoughts[n];
            Check(hinted.Contains(n), $"{n} has a mood hint");
            var moods = el.Element("stages")?.Elements("li").Select(li => li.Element("baseMoodEffect")?.Value).Where(v => v != null).Select(float.Parse).ToList() ?? new List<float>();
            Check(moods.Count > 0 && moods.All(m => m <= -1f && m >= -6f), $"{n} is moderate (-1 to -6)");
            Check(el.Descendants("label").All(l => !l.Value.Contains("{0}")), $"{n} names nobody in its label by number");
            var days = el.Element("durationDays")?.Value;
            if (days != null) Check(float.Parse(days) <= 5f, $"{n} lasts five days at most");
            if (!n.StartsWith("Council_Setback_")) continue;
            var ext = el.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string)li.Attribute("Class") == "ColonyCouncil.SetbackExtension");
            string group = ext?.Element("group")?.Value;
            Check(group == "Council" || group == "Life" || group == "Friction", $"{n} says which setting it follows ({group ?? "none"})");
            if (el.Element("workerClass") != null)
                Check(el.Element("thoughtClass")?.Value == "ColonyCouncil.Thought_Setback", $"{n}, a lasting one, uses Thought_Setback");
        }
        Check(setbacks.Count(n => thoughts[n].Element("stages")!.Elements("li").Any(li => float.Parse(li.Element("baseMoodEffect")!.Value) <= -5f)) <= 4,
            "only a few are big (-5 or worse)");

        // What they say and write.
        string[] used =
        {
            "Council_Pool_Setback_Wrecked", "Council_Pool_Setback_CropLost", "Council_Pool_Setback_Surgery", "Council_Pool_Setback_IdeaPassed",
            "Council_Pool_Setback_NoPlate", "Council_Pool_Setback_Rained", "Council_Journal_Setback_Wrecked", "Council_Journal_Setback_CropLost",
            "Council_Journal_Setback_Surgery", "Council_Journal_Setback_IdeaPassed", "Council_Journal_Setback_FriendLetDown",
            "Council_Journal_Setback_Outshone", "Council_Journal_Setback_Drifted", "Council_Journal_Setback_Homesick", "Council_Journal_Setback_ArtSold",
            "Rimfolk_Pool_Setback_Burned", "Rimfolk_Journal_Setback_Burned", "Rimfolk_Journal_Setback_EmptyChair", "Rimfolk_Journal_Setback_CampSoured",
            "Rimfolk_Journal_Setback_LetDown", "Rimfolk_Journal_Setback_StoodBy"
        };
        foreach (var p in used) Check(pools.Contains(p), $"pool {p} is there");
        return cases;
    }
}
