using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

/// <summary>
/// The review of Rimfolk's map events and the council's questions (docs/review-events-council.md, measured by
/// Tools/impact_audit.py): what was fixed, kept fixed. The Bell's undertakers name the town their grave-garden is at; a
/// named helper chosen for their skill is trained by the work the answer gives them; the answers whose words promised what
/// their effects didn't do (a fine mood all day, a debt forgiven, lying awake, a bowl of stew over the wall) now do it;
/// and the two answers added to questions with only weak ones play out on the map.
/// </summary>
public static class ReviewEventsCouncilTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "review of events and council questions: " + what);
        }

        string Code(string rel) => File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", rel));

        string Between(string text, string from, string to)
        {
            int a = text.IndexOf(from, StringComparison.Ordinal);
            if (a < 0) return "";
            int b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
            return b < 0 ? text.Substring(a) : text.Substring(a, b - a);
        }

        string Cls(XElement e) => ((string)e.Attribute("Class") ?? "").Split('.').Last();

        // Every council question, and every Rimfolk event on the map (not a personal story's or a mission's own card):
        // its answers by def name.
        var questions = new Dictionary<string, XElement>();
        var events = new Dictionary<string, XElement>();
        var eventFile = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories))
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(f);
            }
            catch
            {
                continue;
            }
            foreach (var d in doc.Root!.Elements())
            {
                string name = (string)d.Element("defName");
                if (name == null) continue;
                if (d.Name.LocalName == "ColonyCouncil.DilemmaDef")
                {
                    questions[name] = d;
                    continue;
                }
                if (d.Name.LocalName != "IncidentDef") continue;
                var exts = d.Element("modExtensions")?.Elements("li").ToList() ?? new List<XElement>();
                var ext = exts.FirstOrDefault(x => Cls(x) == "HappeningExtension");
                if (ext == null || exts.Any(x => Cls(x) == "StoryText" || Cls(x) == "MissionText")) continue;
                events[name] = ext;
                eventFile[name] = Path.GetFileName(f);
            }
        }

        XElement Answer(string def, string label)
        {
            var holder = questions.TryGetValue(def, out var q) ? q : events.TryGetValue(def, out var e) ? e : null;
            return holder?.Element("options")?.Elements("li").FirstOrDefault(o => (string)o.Element("label") == label);
        }

        IEnumerable<XElement> Effects(XElement o, string list = "effects") => o?.Element(list)?.Elements("li") ?? Enumerable.Empty<XElement>();

        bool Has(XElement o, string cls, string field = null, string value = null, string list = "effects") =>
            Effects(o, list).Any(e => Cls(e) == cls && (field == null || (string)e.Element(field) == value));

        // ---- The Bell's undertakers: {PLACE} is the town of their grave-garden, set as the Bell's other events set it ----
        var bell = Between(Code("Peoples/PeoplesD.cs"), "public class HappeningWorker_BellForTheDead", "public class DilemmaEffect_BellForTheDead");
        C(bell.Contains("protected override bool Cast(") && bell.Contains("ctx.placeName = LivingEvents.TownOf(ctx.faction, ctx.map) ?? ctx.faction.Name;"),
            "a bell for the dead: the grave-garden's town ({PLACE}) is named, not left empty");
        C(events.TryGetValue("Rimfolk_Evt_BellForTheDead", out var bellEvent) && bellEvent.ToString().Contains("{PLACE}"),
            "a bell for the dead: its words name the grave-garden at {PLACE}, so the worker must fill it");
        // The undertakers' own event, which the Bell's builds on, never names a place (it has none to fill).
        var undertakers = events.Where(kv => kv.Value.Parent?.Parent?.Element("workerClass")?.Value.EndsWith("HappeningWorker_Undertakers") == true).ToList();
        C(undertakers.All(kv => !kv.Value.ToString().Contains("{PLACE}")), "the undertakers' own event names no place");

        // ---- A helper named for their skill (Needs with a skill, asB) is trained by the work the answer gives them ----
        var training = new HashSet<string> { "DilemmaEffect_Learn", "DilemmaEffect_Lesson", "DilemmaEffect_Teach", "DilemmaEffect_Mentor", "DilemmaEffect_Mentorship",
            "DilemmaEffect_Rounds", "DilemmaEffect_VetRounds" };
        int helpers = 0;
        foreach (var (def, holder) in questions.Select(kv => (kv.Key, kv.Value)).Concat(events.Select(kv => (kv.Key, kv.Value))))
            foreach (var o in holder.Element("options")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                var needs = Effects(o).FirstOrDefault(e => Cls(e) == "DilemmaEffect_Needs" && e.Element("skill") != null && (string)e.Element("asB") == "true");
                if (needs == null) continue;
                helpers++;
                bool trained = Effects(o).Any(e => training.Contains(Cls(e)) && (Cls(e) != "DilemmaEffect_Learn" || (string)e.Element("target") == "B"));
                C(trained, $"{def}, \"{(string)o.Element("label")}\": the {(string)needs.Element("skill")} helper is trained by the work");
            }
        C(helpers >= 20, "the helpers named for their skill are found (" + helpers + ")");

        // ---- Words that promised what the effects didn't do ----
        var scout = Answer("Council_Evt_TalentScout", "Not for sale");
        C(Has(scout, "DilemmaEffect_Thought", "thought", "Council_Evt_NotForSale")
          && Effects(scout).First(e => (string)e.Element("thought") == "Council_Evt_NotForSale").Element("onlyIfChosen")?.Value == "true",
            "the talent scout turned down: the fine mood all day its words promise, only when the council chose it");
        var notForSale = XDocument.Load(Path.Combine(root, "Defs", "ThoughtDefs", "Council_Thoughts_Events.xml")).Root!.Elements("ThoughtDef")
            .FirstOrDefault(t => (string)t.Element("defName") == "Council_Evt_NotForSale");
        C(notForSale != null && (string)notForSale.Element("durationDays") == "1" && float.Parse(notForSale.Descendants("baseMoodEffect").First().Value, System.Globalization.CultureInfo.InvariantCulture) > 0,
            "the colony wouldn't sell me: a good mood for the day");
        var creditor = Answer("Council_Evt_Creditor", "Let {A_nameDef} talk them down");
        C(Has(creditor, "DilemmaEffect_Thought", "thought", "Council_Evt_DebtPaid") && Has(creditor, "DilemmaEffect_Learn", "skill", "Social")
          && Has(creditor, "DilemmaEffect_Learn", "skill", "Social", "riskEffects"),
            "the creditor talked down: the debt forgiven is felt as paid, and the talking is practice either way");

        // Lying awake, sitting up, sleeping fourteen hours: the hediff the words describe.
        foreach (var (def, label, hediff) in new[]
                 {
                     ("Council_DlmLv_WorrierWatch", "The walls are fine. Get some sleep.", "Council_Dlm_Sleepless"),
                     ("Council_DlmLv_ChildGrief", "Time will do it", "Council_Dlm_Sleepless"),
                     ("Council_DlmLv_Frightened", "Children get over these things", "Council_Dlm_Sleepless"),
                     ("Council_Dilemma_Omen", "Tell {A_nameDef} to keep it quiet", "Council_Dlm_Sleepless"),
                     ("Rimfolk_DlmR3_CryingNight", "Babies cry", "Council_Dlm_Sleepless"),
                     ("Rimfolk_DlmR3_LightSleeper", "A shared room is a shared room", "Council_Dlm_Sleepless"),
                     ("Rimfolk_DlmR4_NightOwl", "The colony keeps one day", "Council_Dlm_Sleepless"),
                     ("Council_Dilemma_Music", "Ban the night music", "Council_Dream_WellRested"),
                     ("Council_DlmLv_OldWayNewWay", "The new way: let {B_nameDef} try it", "Council_Motivated"),
                 })
            C(Has(Answer(def, label), "DilemmaEffect_Hediff", "hediff", hediff), $"{def}, \"{label}\": {hediff}, as its words say");

        // What the words say is done, done on the map or in the colony.
        C(Has(Answer("Council_DlmR_FriendAtTheGate", "The rule is the rule"), "DilemmaEffect_CarryOut", "to", "Stranger"),
            "the friend at the gate: the bowl of stew is carried out over the wall");
        C(Has(Answer("Council_DlmR_Sanctuary", "A night's rest, then the road"), "DilemmaEffect_CarryOut", "to", "Stranger"),
            "sanctuary refused: the bread for the road goes into their pack");
        C(Has(Answer("Council_DlmLv_ElderDoctor", "Respect {A_nameDef}'s wishes"), "DilemmaEffect_EaseIllness"),
            "the elder's own way: the remedies left where they will find them do a little good");
        C(Has(Answer("Council_DlmLv_Schooling", "They learn by working beside us"), "DilemmaEffect_Learn", "skill", "Construction"),
            "learning by working: a day following the builders teaches building");
        C(Has(Answer("Council_DlmLv_Mischief", "Children break things. Let it go."), "DilemmaEffect_Learn", "target", "B"),
            "the broken window: the grown-up who mends it practises mending");
        C(Has(Answer("Council_DlmS_WantsToStay", "{X_nameDef} leaves with {X_possessive} people"), "DilemmaEffect_WorkParty"),
            "the one who leaves with their people: the fence mended on the far side is real work");
        C(Has(Answer("Council_DlmLv_ElderWalk", "It's too far; stay where it's safe"), "DilemmaEffect_Visit", "to", "Gate")
          && Has(Answer("Council_Dilemma_TreeSinging", "Trees don't listen"), "DilemmaEffect_Visit", "to", "Outdoors"),
            "the elder at the wall and the one alone at the old tree are seen there");
        C(Has(Answer("Rimfolk_Evt_FirstPiece", "{A_nameDef} tells {B_nameDef} so, between the two of them"), "DilemmaEffect_Learn", "target", "B"),
            "a first good piece: being told why it is good teaches its maker");

        // ---- Answers added where a question had only weak ones: real effects, played out on the map ----
        var pet = Answer("Council_Dilemma_PetKitchen", "{A_nameDef} teaches {PET_nameDef} to wait at the door");
        C(pet != null && Has(pet, "DilemmaEffect_Learn", "skill", "Animals") && Has(pet, "DilemmaEffect_Visit", "whom", "Pet")
          && Has(pet, "DilemmaEffect_Visit", "whom", "Pet", "riskEffects") && (string)pet.Element("riskSkill") == "Animals",
            "an animal in the kitchen: a fourth answer, the animal taught to wait at the door, the evenings there played out");
        var meals = Answer("Rimfolk_DlmE_GoodMeals", "Keep them, and {A_nameDef} shows {B_nameDef} how they're made");
        C(meals != null && Has(meals, "DilemmaEffect_Lesson", "skill", "Cooking") && !Has(meals, "DilemmaEffect_Feast"),
            "the good meals: a third answer, kept and the cooking taught at the stove");

        // ---- The audit that measures it stays runnable ----
        C(File.Exists(Path.Combine(root, "Tools", "impact_audit.py")) && File.Exists(Path.Combine(root, "docs", "review-events-council.md")),
            "the impact audit and its report are there");

        return n;
    }
}
