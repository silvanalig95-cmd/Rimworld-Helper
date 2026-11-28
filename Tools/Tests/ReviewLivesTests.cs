using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The review of the colonists' own lives (docs/review-lives.md, Tools/lives_impact_audit.py): the bugs found, each kept
/// fixed, what was given the effect its words already say, and what was added in the frames there are. The year's aim can
/// end well; a seat's story practises a skill they can use; a wish's errand never takes anyone held or busy with what
/// matters more; mended things, deals and masterworks mark the skill a reached goal was about, as do the seats by their own
/// work; the newcomer's friend is a friend in the social web, the friends over the hills are gone to and their people
/// remembered; three more wishes. And, for every frame, the placeholders its words use are the ones its code fills, and
/// every story that waits on a life goal knows which.
/// </summary>
public static class ReviewLivesTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "review of the colonists' own lives: " + what);
        }

        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        if (root == null)
        {
            C(false, "found the mod's folder");
            return n;
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string Between(string text, string from, string to)
        {
            int a = text.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return "";
            int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
        }
        var docs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => (file: f, doc: XDocument.Load(f))).ToList();
        IEnumerable<XElement> All(string type) =>
            docs.SelectMany(d => d.doc.Root!.Elements()).Where(e => (e.Name.LocalName == type || e.Name.LocalName == "ColonyCouncil." + type) && (string)e.Attribute("Abstract") != "True");
        XElement Def(string type, string name) => All(type).FirstOrDefault(e => (string)e.Element("defName") == name);
        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Replace("ColonyCouncil.", "");
        float F(XElement e, string name) =>
            e?.Element(name) == null ? 0f : float.Parse(e.Element(name)!.Value, System.Globalization.CultureInfo.InvariantCulture);
        string Strip(string s) => Regex.Replace(s ?? "", @"^\[[^\]]*\]\s*", "");
        var allCode = string.Join("\n", Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        string ClassBody(string name)
        {
            int at = allCode.IndexOf("class " + name + " ", StringComparison.Ordinal);
            if (at < 0) at = allCode.IndexOf("class " + name + "\n", StringComparison.Ordinal);
            if (at < 0) return "";
            int next = allCode.IndexOf("\n    public class ", at + 10, StringComparison.Ordinal);
            return next < 0 ? allCode.Substring(at) : allCode.Substring(at, next - at);
        }

        // ================= Bugs =================

        // ---- The year's aim can end well ----
        // Its last gate waits for the life goal reached, by the story's goal; the worker kept the goal in the key only, so the
        // gate never opened and a goal reached was told as the year's aim missed.
        var stories = Code("LifeVariety/ColonyStories.cs");
        var aim = Between(stories, "public class ArcWorker_YearsAim", "\n    }\n}");
        C(aim.Contains("key = said.def, goal = said.def"), "the year's aim keeps the goal its last gate waits for");
        C(aim.Contains("if (arc.goal.NullOrEmpty()) arc.goal = arc.key;"), "a year's aim begun before keeps it too");
        C(Between(aim, "public override bool CustomGate(", "public override string Broken(").Contains("return Reached(comp, arc);"),
            "a goal reached in one go is past halfway too, not left waiting forty days");
        // Every story that waits on a life goal knows which (the tracker's Goal gate reads the story's goal).
        foreach (var arc in All("ArcDef").Where(a => a.Descendants("kind").Any(k => k.Value == "Goal")))
        {
            string worker = ((string)arc.Element("workerClass") ?? "").Replace("ColonyCouncil.", "");
            // A local named goal is not it ("var goal = ...").
            C(Regex.IsMatch(ClassBody(worker), @"(?<!var )(?<!string )\bgoal\s*=(?!=)"), $"{(string)arc.Element("defName")}: its worker {worker} names the goal its Goal gate waits for");
        }

        // ---- Ready for the seat: a skill they can use ----
        var seat = Between(stories, "public class ArcWorker_ReadyForTheSeat", "public class ArcWorker_PrayerForAFriend");
        C(seat.Contains("!r.TotallyDisabled") && seat.Contains("if (skill == null) return null;"), "the seat's story practises a skill of the seat they can use, or doesn't begin");

        // ---- A wish's errand never takes anyone held or busy with what matters more ----
        var r4 = Code("Arcs/Round4Lives.cs");
        var free = Between(r4, "public static bool Free(Pawn p)", "public static bool WalkTo(");
        C(free.Contains("!OwnHappenings.Held(p) && PlayOuts.Interruptible(p)"), "free: not drafted, broken, down or on the player's order, and doing nothing that matters more");
        C(Between(r4, "public static bool WalkTo(", "// ---------------- Snow").Contains("!Free(p)"), "a wish's walk to a grave, a shrine or the visitors asks it");
        C(Between(r4, "public class R4LWishWorker_SnowDay", "public class R4LWishWorker_GraveWord").Contains("!Round4Lives.Free(child)"), "the child sent out alone to build a snowman too");
        C(Between(Code("Wishes/WishWorkers_Seasons.cs"), "public override string OnPromised(Wish wish)", "public class WishWorker_SeasonWarm").Contains("!Round4Lives.Free(p)"),
            "the one sent to put on warmer or cooler clothes too");

        // ---- The harvest in before the frost: brought in, not taken by the frost ----
        // It was met once the named crops no longer stood ripe, so a cold night, a blight or a fire that took them was
        // thanked for as the harvest brought in.
        var frost = Between(r4, "public class R4LWishWorker_FrostHarvest", "public class R4LWishWorker_SnowDay");
        C(frost.Contains("w.baseline = Harvested(map);") && frost.Contains("RecordDefOf.PlantsHarvested"), "the colony's harvests are counted from when it is asked (the game's record)");
        C(frost.Contains("=> Round4LivesMath.HarvestIn(StillRipe(wish).Count(), Cells(wish).Count) && BroughtIn(wish);"), "met only when the colony's hands brought them in");
        C(Between(frost, "public override bool StillValid(", "public override float Progress(").Contains("|| BroughtIn(wish)"),
            "crops gone with nobody bringing them in are let go without blame");

        // ---- Something new at the bench: made by them ----
        var make = Between(r4, "public class R4LWishWorker_NewMake", "public class R4LWishWorker_OldFriend");
        C(make.Contains("c.baseline = Made(pawn);") && make.Contains("RecordDefOf.ThingsCrafted") && make.Contains("RecordDefOf.MealsCooked"),
            "what they have made is counted from when they ask");
        C(make.Contains("Have(wish.Beneficiary?.MapHeld, wish.thingDef) && Made(wish.Beneficiary) > wish.baseline"),
            "met when the colony has one and they have made something since, not by a trader's or a raider's left behind");

        // ================= Real effects =================

        // ---- A reached goal marks the skill it was about ----
        C(GoalRewardMath.SkillFor(null, "ThingsRepaired", null, null, null) == "Construction", "a thing-mender's goal marks building");
        C(GoalRewardMath.SkillFor(null, "Council_TradesMade", null, null, null) == "Social", "a haggler's goal marks talk");
        C(GoalRewardMath.SkillFor(null, "Council_Masterworks", null, null, null) == GoalRewardMath.CraftOrArt, "two masterworks: crafted or made as art");
        C(GoalRewardMath.SkillFor(null, null, null, "Council_Office_Reeve", null, "Plants") == "Plants"
          && GoalRewardMath.SkillFor(null, null, null, "Council_Office_Herdmaster", null, "Animals") == "Animals", "a seat marks its own work");
        C(GoalRewardMath.SkillFor(null, null, null, "Council_Office_Physician", null, "Social") == "Medicine" && GoalRewardMath.SkillFor(null, null, null, "Council_Office_Reeve", null) == null,
            "the three old seats as before; a seat with no skill marks none");
        C(GoalRewardMath.SkillFor(null, "CorpsesBuried", null, null, "AspirationWorker_Record") == null && GoalRewardMath.SkillFor(null, null, null, null, "AspirationWorker_Spouse") == null,
            "a burial or a marriage still marks nothing");
        C(Code("Aspirations/GoalRewards.cs").Contains("d.office?.skills?.FirstOrDefault()?.defName"), "the seat's skill is the office's own");
        foreach (var name in new[] { "Rimfolk_Asp_Mender", "Rimfolk_Asp_Haggler", "Rimfolk_R4L_Asp_Masterworks" })
        {
            var g = Def("AspirationDef", name);
            C(g != null && GoalRewardMath.SkillFor((string)g.Element("skill"), (string)g.Element("record"), (string)g.Element("counter"), null, null) != null, $"{name}: marks a skill when reached");
        }

        // ---- The newcomer's friend, and the friends over the hills ----
        var home = Def("ArcDef", "Council_Arc_FindingHome");
        var homeStages = home?.Element("stages")?.Elements("li").ToList() ?? new List<XElement>();
        var tie = homeStages.Count > 1 ? homeStages[1].Element("effects")?.Elements("li").FirstOrDefault(e => Cls(e) == "ArcEffect_Tie") : null;
        C(tie != null && (string)tie.Element("kind") == "Friends" && F(tie, "amount") > 0f && F(tie, "amount") <= 10f,
            "somewhere to belong: the friend its words name is a friend in the social web, a small start");
        var hills = Def("ArcDef", "Rimfolk_R4L_Arc_OverTheHills");
        var hillStages = hills?.Element("stages")?.Elements("li").ToList() ?? new List<XElement>();
        C(hillStages.Count == 3 && hillStages[1].Element("effects")?.Elements("li").Any(e => Cls(e) == "LivesReviewEffect_SeeTheirPeople") == true,
            "friends over the hills: they go over to see the people they got on with, as the words say");
        var hillEnd = hillStages.Count == 3 ? hillStages[2].Element("effects")?.Elements("li").ToList() ?? new List<XElement>() : new List<XElement>();
        var goodwill = hillEnd.FirstOrDefault(e => Cls(e) == "DilemmaEffect_Goodwill");
        var joy = hillEnd.FirstOrDefault(e => Cls(e) == "DilemmaEffect_Joy");
        C(goodwill != null && F(goodwill, "amount") > 0f && F(goodwill, "amount") <= 5f && joy != null && F(joy, "amount") > 0f && F(joy, "amount") <= 0.2f,
            "and the hour of talk and the friendship with that people that holds, small");
        var see = Between(Code("Arcs/LivesReview.cs"), "public class LivesReviewEffect_SeeTheirPeople", "\n    }\n}");
        C(see.Contains(": DilemmaEffect_Visit") && see.Contains("to = PlaySpot.Stranger") && see.Contains("Round4Lives.PeopleHere(p.Map, ctx.faction)") && see.Contains("x.ThingID == arc.key"),
            "the visit is the play-out engine's own, to one of that people here, the one they knew first if they came");

        // ================= Added in the frames there are =================

        foreach (var (name, worker) in new[] { ("Rimfolk_LivR7_Wish_Boots", "WishWorker_Wear"), ("Rimfolk_LivR7_Wish_Gloves", "WishWorker_Wear"), ("Rimfolk_LivR7_Wish_Cloth", "WishWorker_Stock") })
        {
            var w = Def("WishDef", name);
            C(w != null && (string)w.Element("workerClass") == "ColonyCouncil." + worker, $"{name}: one of the XML-driven wishes ({worker})");
            var thought = Def("ThoughtDef", (string)w?.Element("grantedThought") ?? "");
            float mood = thought?.Descendants("baseMoodEffect").Select(x => float.Parse(x.Value, System.Globalization.CultureInfo.InvariantCulture)).FirstOrDefault() ?? 0f;
            C(mood >= 2f && mood <= 4f && F(thought, "durationDays") <= 6f, $"{name}: a mood the size of the hat's or the firewood's");
            var wants = w?.Element("wants");
            C(wants != null && wants.Element("defNames") == null, $"{name}: nothing by a fixed name, the player's mods count by what they are");
            foreach (var part in new[] { "request", "thanks", "expired", "reminder" })
                foreach (var line in w?.Element(part)?.Descendants("li").Select(x => Strip(x.Value)) ?? Enumerable.Empty<string>())
                    C(!line.Contains('—') && Regex.Matches(line, @"[.!?](\s|$)").Count <= 1, $"{name}: {part} '{line}' is one sentence, no dash");
        }
        foreach (var name in new[] { "Rimfolk_LivR7_Wish_Boots", "Rimfolk_LivR7_Wish_Gloves" })
        {
            var w = Def("WishDef", name);
            var words = w?.Element("wants")?.Element("nameContains")?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
            C(words.Count >= 2 && words.All(x => x == x.ToLowerInvariant()), $"{name}: found by its name in lower case (boots, shoes, gloves, mittens)");
            C(w?.Element("forbiddenBy")?.Elements("li").Any(x => x.Value == "Nudity") == true, $"{name}: never asked for by one whose faith keeps them unclothed");
        }
        var cloth = Def("WishDef", "Rimfolk_LivR7_Wish_Cloth")?.Element("wants");
        C(cloth?.Element("categories")?.Elements("li").Any(x => x.Value == "Textiles") == true && cloth.Element("coldComing")?.Value == "true" && (string)cloth.Element("passion") == "Crafting",
            "cloth in store: textiles of every kind, asked by a tailor with the cold coming");

        // ================= Every frame: the placeholders its code fills =================

        var filled = new Dictionary<string, (HashSet<string> names, string[] fields)>
        {
            ["WishDef"] = (new HashSet<string> { "PAWN", "FOR", "OTHER", "SUBJECT", "THING", "THINGBARE", "TARGET", "WISH", "ENVIED" },
                new[] { "label", "request", "requestForOther", "thanks", "expired", "reminder", "fulfilledWhen", "lostText" }),
            ["ArcDef"] = (new HashSet<string> { "PAWN", "OTHER", "PET", "THIRD", "SKILL", "LOST", "NAME", "FACTION", "GOAL", "QUIRK", "DAYS", "CALL" },
                new[] { "fizzle", "fizzleChronicle", "eulogy" }),
            ["AspirationDef"] = (new HashSet<string> { "PAWN", "GOAL", "SKILL", "NAME", "COUNT" }, new[] { "goal", "deed", "chosen", "achieved" }),
            ["AmbitionDef"] = (new HashSet<string> { "PAWN" }, new[] { "pitch", "achieved" }),
        };
        foreach (var (type, (names, fields)) in filled)
        {
            int bad = 0;
            var texts = new List<(string def, string text)>();
            foreach (var d in All(type))
            {
                string dn = (string)d.Element("defName");
                foreach (var f in fields)
                    foreach (var el in d.Elements(f).SelectMany(x => x.DescendantsAndSelf()).Where(x => !x.HasElements))
                        texts.Add((dn, el.Value));
                if (type == "ArcDef")
                    foreach (var st in d.Element("stages")?.Elements("li") ?? Enumerable.Empty<XElement>())
                        foreach (var part in new[] { "label", "waiting", "journal", "otherJournal", "thirdJournal", "chronicle" })
                            foreach (var el in st.Elements(part).SelectMany(x => x.DescendantsAndSelf()).Where(x => !x.HasElements))
                                texts.Add((dn, el.Value));
            }
            foreach (var (dn, text) in texts)
                foreach (Match m in Regex.Matches(text, @"\{([A-Z][A-Z0-9]*)(?:_[A-Za-z]+)?\}"))
                    if (!names.Contains(m.Groups[1].Value))
                    {
                        bad++;
                        C(false, $"{dn}: {{{m.Groups[1].Value}}} is never filled by the {type}'s code");
                    }
            C(bad == 0 && texts.Count > 0, $"every {type}'s words use only the placeholders its code fills");
        }
        return n;
    }
}
