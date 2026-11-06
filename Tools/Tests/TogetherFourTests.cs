using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// New moments of life together (the player's request, 6 November 2026): what happens between colonists from what really
/// happens on the map. Social events (after a fight, a debt remembered, the heavy end, two at the same work, a fine piece
/// shown, a loss, a hurt, an old remedy, a newcomer, a bad meal, the weather, the region's news), shared memories (an
/// illness nursed, a close call on the land, a colony founded, a debt, two in-jokes), a topic (the sick and the hurt),
/// group habits (reading, the sickbed, music, the night sky) and the wardens, dreams of the new moments and of a founding,
/// and a personal story (back to it). The rules without game types (Social/TogetherFourMath.cs), and the defs and code:
/// every age's forms, the conditions read from the map, nothing that asks more often, no staged scene, no harm, nothing
/// saved, the mud's moment tags fixed.
/// </summary>
public static class TogetherFourTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "together four: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));
        XElement Load(string rel) => XDocument.Load(Path.Combine(root, rel)).Root!;
        List<string> Lis(XElement e, string name) => e?.Element(name)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        string Name(XElement e) => (string)e.Element("defName") ?? "";
        var allDefs = Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Select(f => XDocument.Load(f).Root!).ToList();
        var defNames = allDefs.SelectMany(d => d.Elements()).Select(Name).Where(x => x != "").ToHashSet();
        var keyed = Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements()).GroupBy(e => e.Name.LocalName).ToDictionary(g => g.Key, g => g.First().Value);
        var eras = new HashSet<string> { "tribal", "medieval", "industrial", "spacer", "lowtech", "!lowtech" };
        string Strip(string s, out string[] tags)
        {
            var m = Regex.Match(s, @"^\[([^\]]*)\]\s*");
            tags = m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim()).ToArray() : new string[0];
            return m.Success ? s.Substring(m.Length) : s;
        }

        // ================= The rules =================

        C(TogetherFourMath.NeedsMoment("rain|snow") && TogetherFourMath.NeedsMoment("eating,lowmood") && TogetherFourMath.NeedsMoment("!newcomer")
            && TogetherFourMath.NeedsMoment("work:Mining"), "a role written for the weather, the table or a newcomer reads the moment");
        C(!TogetherFourMath.NeedsMoment("quirk:Hums") && !TogetherFourMath.NeedsMoment("trait:Kind|persona:warm") && !TogetherFourMath.NeedsMoment("!trait:Teetotaler")
            && !TogetherFourMath.NeedsMoment("") && !TogetherFourMath.NeedsMoment(null), "a role written for who someone is reads them as before");
        C(TogetherFourMath.SameWork("Mining", "Mining", null) && !TogetherFourMath.SameWork("Mining", "Growing", null) && !TogetherFourMath.SameWork("", "", null)
            && !TogetherFourMath.SameWork("Doctor", "Doctor", null) && TogetherFourMath.SameWork("Cooking", "Cooking", new[] { "Cooking" })
            && !TogetherFourMath.SameWork("Hauling", "Hauling", new[] { "Cooking" }), "two at the same work side by side, never a patient or a lone bench");
        C(TogetherFourMath.FreshWithin(0, 60000, 2f) && TogetherFourMath.FreshWithin(0, 120000, 2f) && !TogetherFourMath.FreshWithin(0, 120001, 2f)
            && !TogetherFourMath.FreshWithin(-1, 10, 2f) && !TogetherFourMath.FreshWithin(100, 50, 2f), "a fine piece is spoken of for two days, never before it was made");
        C(TogetherFourMath.Fine(4) && TogetherFourMath.Fine(6) && !TogetherFourMath.Fine(3), "excellent or better is a fine piece");
        C(TogetherFourMath.HurtLately(1f, 3f) && !TogetherFourMath.HurtLately(4f, 3f) && !TogetherFourMath.HurtLately(9999f, 3f) && !TogetherFourMath.HurtLately(1f, 0f),
            "a hurt of everyday life is spoken of for a few days, and only if there was one");
        var dangers = Code("Happenings/Dangers.cs");
        var keys = Regex.Matches(dangers, @"Wound\([^;]*?""(CC_Danger_[A-Za-z]+)""").Select(m => m.Groups[1].Value).Distinct().ToList();
        C(keys.Count >= 6, $"the dangers' hurts found ({keys.Count})");
        foreach (var k in keys)
        {
            string kind = TogetherFourMath.CloseCallKind(k);
            C(kind != null && TogetherFourMath.CloseCallKinds.Contains(kind), $"{k}: a close call two can remember");
        }
        foreach (var kind in TogetherFourMath.CloseCallKinds)
            C(keyed.ContainsKey("CC_Times_Close_" + kind) && keyed["CC_Times_Close_" + kind].StartsWith("the "), $"a close call with the {kind.ToLowerInvariant()} has a name");
        C(TogetherFourMath.CloseCallKind("CC_Something_Else") == null, "only a danger's hurt is a close call");
        C(TogetherFourMath.CloseCallSkill("Rock") == "Mining" && TogetherFourMath.CloseCallSkill("Tree") == "Plants" && TogetherFourMath.CloseCallSkill("Scrap") == "Crafting"
            && TogetherFourMath.CloseCallSkill("Ground") == null, "back to the work it happened at");
        C(Math.Abs(TogetherFourMath.SocialEventsADay(5) - 1f) < 0.001f && Math.Abs(TogetherFourMath.SocialEventsADay(1) - 0.4f) < 0.001f && Math.Abs(TogetherFourMath.SocialEventsADay(30) - 2f) < 0.001f,
            "social events a day come from the colony's size");

        // ================= The code =================

        var soc = Code("Social/SocialEvents.cs");
        C(Regex.IsMatch(soc, @"float expected = UnityEngine\.Mathf\.Clamp\(pawns\.Count / 5f, 0\.4f, 2f\)") && !Regex.IsMatch(soc, @"expected[^;]*AllDefs"),
            "the day's social events are counted from the colony, never from the defs: new ones take a share");
        C(soc.Contains("bMemoryAboutA ? a : null") && soc.Contains("WorkSideBySide(def, a, b)") && soc.Contains("FineOf(b, def.bMadeFineDays)") && soc.Contains("HurtShows(b, def.bHurtDays)"),
            "the new conditions are read where a social event is cast");
        C(soc.Contains("inj.Visible && !inj.IsPermanent()") && soc.Contains("Harm.DaysSince(p)"), "a hurt spoken of is one of everyday life, and still shows");
        C(soc.Contains("class SocialEventContext") && soc.Contains("gerundLabel") && soc.Contains("ctx.skill = ctx.work.relevantSkills[0]"), "{WORK} names the work, and its skill is the event's");
        C(Code("Arcs/ArcHooks.cs").Contains("SocialEvents.Notify_Made(worker, thing)"), "a fine piece is noticed where the game tells of it");
        var tags = Code("Integrations/PeopleTags.cs");
        C(tags.Contains("public static bool MatchesNow") && tags.Contains("Flavor.TagsFor(p)") && Code("Dilemmas/Dilemmas.cs").Contains("PeopleTags.MatchesNow(p, tags)"),
            "a role's tags of the moment are read from the moment (the mud's in-joke fitted nobody before)");
        var mud = allDefs.SelectMany(d => d.Elements("ColonyCouncil.SocialEventDef")).First(e => Name(e) == "Rimfolk_Soc_Mud");
        C(TogetherFourMath.NeedsMoment((string)mud.Element("roleA")?.Element("tags")), "the mud's wet day is a moment's tag");
        var times = Code("Social/Talk/SharedTimesTogether.cs");
        C(Code("Health/HealthTracker.cs").Contains("SharedTimesTogether.Nursed(doctor, patient, illness)"), "an illness nursed is remembered where the doctor pulled them through");
        C(dangers.Contains("SharedTimesTogether.CloseCall(p, key)"), "a close call is remembered where the danger's wound is given");
        C(Code("Colonies/Foundings.cs").Contains("SharedTimesTogether.Founded(founders, name)"), "a founding is remembered where the new colony stands");
        C(times.Contains("Together.Company(hurt)") && times.Contains("TogetherFourMath.FoundingEach"), "remembered by those who were really there, the founders with the closest of them");
        string mine = string.Join("\n", new[] { "Social/TogetherFourMath.cs", "Social/Talk/SharedTimesTogether.cs", "Social/Talk/TopicWorker_Health.cs", "Arcs/ArcWorkers_Together.cs" }.Select(Code));
        C(!mine.Contains("Scribe_") && !soc.Contains("Scribe_Collections"), "nothing new is saved: the fine pieces and close calls are forgotten at a load");
        C(!mine.Contains("Sched.") && !mine.Contains("Harm.Injure") && !mine.Contains("Harm.Fight") && !mine.Contains("StartSocialFight"), "no new tick, and no harm of their own");
        var habits = Code("Social/Groups/GroupHabits.cs");
        C(habits.Contains("case \"read\":") && habits.Contains("case \"sickbed\":") && habits.Contains("case \"music\":") && habits.Contains("JobDefOf.VisitSickPawn")
            && habits.Contains("Play_MusicalInstrument") && habits.Contains("\"Reading\""), "the new habits are kept from what the game shows they are doing");
        C(Code("Social/Groups/GroupDefs.cs").Contains("\"read\", \"sickbed\", \"music\""), "the new places a habit is kept in are known");
        C(Code("Social/Talk/TopicWorker_Health.cs").Contains("l.sick.Count == 0) return 0f") && Code("Social/Talk/TopicWorker_Health.cs").Contains("NoTease"),
            "the sick are talked of only while someone is, and never teased while something can kill");

        // ================= Social events =================

        var events = Load("Defs/SocialEventDefs/Rimfolk_SocialEvents_Together.xml").Elements("ColonyCouncil.SocialEventDef").ToList();
        C(events.Count >= 20, $"twenty new moments between colonists or more ({events.Count})");
        int ages = 0;
        foreach (var e in events)
        {
            string name = Name(e);
            var texts = Lis(e, "texts");
            C(texts.Count >= 3, $"{name}: three ways to tell it or more");
            C(texts.Any(t => !t.StartsWith("[")), $"{name}: told in any age");
            if (texts.Any(t => t.StartsWith("["))) ages++;
            string tone = (string)e.Element("tone") ?? "neutral";
            C(tone == "warm" || tone == "friction" || tone == "mend" || tone == "neutral", $"{name}: a tone");
            bool work = e.Element("sameWork") != null, fine = e.Element("bMadeFineDays") != null, ill = (string)e.Element("aAiling") == "true";
            foreach (var t in texts)
            {
                string body = Strip(t, out var ts);
                C(ts.All(eras.Contains), $"{name}: only the age's tags in \"{t}\"");
                C(!body.Contains('—') && !body.Contains('!'), $"{name}: no em dash or exclamation in \"{body}\"");
                foreach (Match m in Regex.Matches(body, @"\{([A-Za-z_]+)\}"))
                {
                    string ph = m.Groups[1].Value;
                    bool ok = Regex.IsMatch(ph, "^(A|B)_(nameDef|pronoun|possessive|objective)$") || (ph == "WORK" && work) || (ph == "THING" && fine) || (ph == "ILLNESS" && ill);
                    C(ok, $"{name}: {{{ph}}} is filled in \"{body}\"");
                }
                C(!Regex.IsMatch(body, @"(^|[.?]\s+)\{[AB]_(pronoun|possessive|objective)\}"), $"{name}: no sentence starts with a lowercase word in \"{body}\"");
            }
            foreach (var th in e.Descendants("thought")) C(defNames.Contains(th.Value), $"{name}: its thought {th.Value} exists");
            foreach (var mem in e.Descendants("memory")) C(defNames.Contains(mem.Value), $"{name}: its memory {mem.Value} exists");
            // Read from the map: each names something that happens there, not just two people near each other.
            bool fromMap = e.Element("tie") != null || e.Element("bMemories") != null || work || fine || e.Element("bHurtDays") != null || ill || e.Element("situations") != null
                           || e.Descendants("tags").Any() || e.Descendants("oldInjury").Any() || e.Descendants("minAge").Any();
            C(fromMap, $"{name}: comes from something on the map");
            foreach (var r in e.Elements().Where(x => x.Name.LocalName is "roleA" or "roleB"))
            {
                string t = (string)r.Element("tags");
                if (t != null && Regex.IsMatch(t, @"\b(rain|snow|hot|cold|outside|eating|fall|newcomer|lowmood|working)\b"))
                    C(TogetherFourMath.NeedsMoment(t), $"{name}: \"{t}\" is read from the moment");
            }
            C(!e.Descendants().Any(x => ((string)x.Attribute("Class") ?? "").Contains("Fight") || ((string)x.Attribute("Class") ?? "").Contains("PlayOut")),
                "{name}: no fight and nothing staged".Replace("{name}", name));
        }
        C(ages >= 15, $"most of them take the form of the age ({ages})");
        var thoughts = Load("Defs/ThoughtDefs/Rimfolk_Thoughts_Together.xml").Elements("ThoughtDef").ToList();
        int good = 0, bad = 0;
        foreach (var th in thoughts)
        {
            string name = Name(th);
            float mood = float.Parse(th.Descendants("baseMoodEffect").First().Value, System.Globalization.CultureInfo.InvariantCulture);
            bool instead = th.Descendants("li").Any(x => (string)x.Attribute("Class") == "ColonyCouncil.RewardInstead");
            C(Math.Abs(mood) <= 3f, $"{name}: small, as the other social events' are");
            C(!instead || mood == 0f, $"{name}: something instead of mood gives no mood");
            if (mood > 0f) good++;
            if (mood < 0f) bad++;
        }
        C(good <= bad + 1, $"the new moments don't lift the colony's mood by themselves ({good} warm moods, {bad} sore ones; the rest give belonging instead)");

        // ================= Shared memories and their talk =================

        var memories = Load("Defs/SharedMemoryDefs/Rimfolk_SharedMemories_Together.xml").Elements("ColonyCouncil.SharedMemoryDef").ToList();
        var lines = allDefs.SelectMany(d => d.Elements("ColonyCouncil.TalkLinesDef")).ToList();
        C(memories.Count == 6, $"six shared memories ({memories.Count})");
        foreach (var m in memories)
        {
            string name = Name(m);
            foreach (var turn in new[] { "Open", "Reply", "Close" })
                C(lines.Any(l => (string)l.Element("memory") == name && (string)l.Element("turn") == turn), $"{name}: lines to {turn.ToLowerInvariant()} with");
            int own = lines.Where(l => (string)l.Element("memory") == name).Sum(l => l.Descendants("li").Count());
            C(own >= 30, $"{name}: thirty lines or more of its own ({own})");
        }
        foreach (var name in new[] { "Rimfolk_Mem_Nursed", "Rimfolk_Mem_CloseCall", "Rimfolk_Mem_Founding" })
            C(times.Contains("\"" + name + "\""), $"{name}: given from the colony's own moment");

        // ================= The sick and the hurt =================

        var topic = Load("Defs/TopicDefs/Rimfolk_Topics_Together.xml").Element("ColonyCouncil.TopicDef");
        C(topic != null && (string)topic.Element("workerClass") == "ColonyCouncil.TopicWorker_Health" && (string)topic.Element("interaction") == "Rimfolk_Talk_Health", "the sick and the hurt is a topic");
        C(Lis(Load("Defs/InteractionDefs/Rimfolk_Talk_Overlap.xml").Element("ColonyCouncil.ModOverlapDef"), "moments").Contains("Rimfolk_Talk_Health"), "it steps back with another mod's conversations");
        var topics = allDefs.SelectMany(d => d.Elements("ColonyCouncil.TopicDef")).Count();
        C(topics <= 25, $"the talk's topics share one budget ({topics})");
        foreach (var turn in new[] { "Open", "Reply", "Close", "Last" })
            C(lines.Any(l => (string)l.Element("topic") == "Rimfolk_Topic_Health" && (string)l.Element("turn") == turn), $"the sick: lines to {turn.ToLowerInvariant()} with");
        var healthTags = lines.Where(l => (string)l.Element("topic") == "Rimfolk_Topic_Health").SelectMany(l => l.Descendants("li")).SelectMany(li => { Strip(li.Value, out var t); return t; })
            .Select(t => t.TrimStart('!')).ToHashSet();
        var given = new HashSet<string> { "for", "against", "mixed", "agree", "debate", "argue", "subject", "ill", "wounded", "otherill", "otherwounded", "serious", "subjectserious",
            "doctor", "lowtech", "otherfor", "otheragainst" };
        foreach (var t in healthTags) C(given.Contains(t), $"the sick: [{t}] is set where it's said");
        var worker = Code("Social/Talk/TopicWorker_Health.cs");
        foreach (var t in new[] { "ill", "wounded", "otherill", "otherwounded", "serious", "subjectserious", "doctor" }) C(worker.Contains("\"" + t + "\""), $"the sick: [{t}] is set");

        // ================= Group habits and the wardens =================

        var groups = Load("Defs/GroupDefs/Rimfolk_GroupHabits_Together.xml");
        var newHabits = groups.Elements("ColonyCouncil.GroupHabitDef").ToList();
        C(newHabits.Count == 4, $"four new habits ({newHabits.Count})");
        C(newHabits.All(h => h.Element("playKind") == null), "none of them is called as a gathering: the colony's staged scenes don't rise");
        var kinds = allDefs.SelectMany(d => d.Elements("ColonyCouncil.GroupKindDef")).ToList();
        var habitDefs = allDefs.SelectMany(d => d.Elements("ColonyCouncil.GroupHabitDef")).ToDictionary(Name, h => h);
        foreach (var h in newHabits)
        {
            string name = Name(h);
            int keptBy = kinds.Count(k => Lis(k, "habits").Contains(name));
            C(keptBy >= 2, $"{name}: kept by two kinds of group or more ({keptBy})");
            C(kinds.Where(k => Lis(k, "habits").Contains(name)).All(k => Lis(k, "habits").IndexOf(name) >= 2), $"{name}: after a kind's own, never its first");
        }
        foreach (var k in kinds)
        {
            var called = Lis(k, "habits").FirstOrDefault(h => habitDefs.TryGetValue(h, out var hd) && hd.Element("playKind") != null);
            C(called == null || !newHabits.Any(h => Name(h) == called), $"{Name(k)}: what its anchor calls them to is as it was");
        }
        var wardens = kinds.FirstOrDefault(k => Name(k) == "Rimfolk_Group_Wardens");
        C(wardens != null && Lis(wardens, "workTypes").SequenceEqual(new[] { "Warden" }) && Lis(wardens, "names").Count(x => !x.StartsWith("[")) >= 3, "the wardens: those who keep the prisoners, named in any age");
        C(Lis(wardens, "names").Any(x => x.StartsWith("[tribal]")) && Lis(wardens, "names").Any(x => x.StartsWith("[medieval]")) && Lis(wardens, "names").Any(x => x.StartsWith("[spacer]")),
            "the wardens: named by the colony's age");

        // ================= Dreams and the story =================

        var dreams = Load("Defs/DreamDefs/Rimfolk_Dreams_Together.xml").Elements("ColonyCouncil.DreamDef").ToList();
        C(dreams.Count >= 8, $"dreams of the new moments ({dreams.Count})");
        foreach (var d in dreams)
        {
            string name = Name(d);
            var after = Lis(d, "afterThoughts");
            C(after.Count > 0 && after.All(defNames.Contains), $"{name}: after a memory that exists");
            C(d.Element("lines")?.Elements().Count(t => t.Elements("li").Any()) >= 5, $"{name}: in several voices");
        }
        C(dreams.Count(d => (string)d.Element("kind") == "Nightmare") >= 2, "not every new dream is a sweet one");
        var story = Load("Defs/ArcDefs/Rimfolk_Arcs_Together.xml").Element("ColonyCouncil.ArcDef");
        C(story != null && (string)story.Element("workerClass") == "ColonyCouncil.ArcWorker_BackToIt" && Code("Arcs/ArcWorkers_Together.cs").Contains("RecentCloseCall"),
            "back to it: begins from a close call on the map");
        C(!story.Descendants().Any(x => ((string)x.Attribute("Class") ?? "").Contains("Procession") || ((string)x.Attribute("Class") ?? "").Contains("PlayOut")),
            "back to it: nothing staged");
        C(story.Descendants("gate").All(g => (string)g.Element("timeoutPasses") != "true"), "back to it: a talk or the work it waits on really happened, or it ends");
        return n;
    }
}
