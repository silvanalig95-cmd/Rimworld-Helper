using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// No answer depends on a visitor who isn't there (the player, 15 November 2026, of the great caravan's "Guido tells them
/// the hill road · As answered": "just had this event but it didn't play out on the map and also there was no real effect
/// from it?"). In their save the beat's rider was refused at the gate by the cold (-36 °C against Rimfolk's old -25 line),
/// the letter went out anyway and the answer walked out to nobody. What is checked: the cold or the heat no longer keeps
/// Rimfolk's own people away; a world event's beat whose letter says someone is at the gate waits until they can come and
/// goes out only once they have; an answer that walks out to the event's visitor goes where it says instead when they
/// aren't on the map (the hill road taken out to the edge of the land), or can't be given; every world event's answer of
/// the kind is in a beat that brings its visitor; the great caravan's answer shows what it does now and when the train
/// arrives, and the ending's letter says what it changed.
/// </summary>
public static class GateVisitorTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the visitor at the gate: " + what);
        }

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => Regex.Replace(File.ReadAllText(Path.Combine(src, rel)), @"//[^\n]*", "");

        // ---------------- The cause, from their save ----------------

        // League of Toba (15 November 2026): tick 6113704, the hills beat due at 6117214; the base advisor's freezer note at
        // 6111544 read "-36 °C beyond the door". CourierTracker.Safe refused anything below -25, so ComeNow returned false.
        string couriers = Code("Couriers/CourierTracker.cs");
        C(!couriers.Contains("temp < -25f") && Regex.IsMatch(couriers, @"public static bool Safe\(Map map\)\s*\{\s*if \(map == null \|\| MapFacts\.InVacuum\(map\) \|\| PlayOuts\.WhyNotNow\(map, false\) != null\) return false;\s*return !map\.gameConditionManager\.ConditionIsActive\(GameConditionDefOf\.ToxicFallout\);"),
            "the cold or the heat no longer keeps anyone of Rimfolk's away (the save's -36 °C refused the rider); danger and poison in the air still do");
        C(Code("Visitors/Visitors.cs").Contains("public static bool Safe(Map map) => CourierTracker.Safe(map);"), "the visits share that gate");

        // ---------------- The beat waits for them ----------------

        string ws = Code("World/WorldSituations.cs");
        C(ws.Contains("if (can && AtGate(beat, s.ctx).Count > 0 && !HappeningWorker_AtGate2.CanCome(map))"),
            "a beat whose letter says someone is at the gate waits while they can't come (danger, no room for another group)");
        C(Regex.IsMatch(ws, @"foreach \(var g in gates\) HappeningTracker\.Safe\(g, ctx\);\s*if \(VisitorTracker\.Instance\?\.CameWith\(ctx\) != true\)\s*\{\s*wait = true;\s*return false;"),
            "whoever it brings comes first, and if they didn't reach the map, nothing of the beat happens and it tries again");
        C(ws.Contains("if (Postpone(s, bs, true, now)) s.beats.Add(bs);") && ws.Contains("if (!retry || bs.postponed >= 8 || now + GenDate.TicksPerHour * 6 >= s.endTick) return false;"),
            "it tries again every six hours, eight times at most, while the event runs");
        C(ws.Contains("gates.Any(g => g.withStranger && !(WorldSituationWorker.FreeInWorld(x)"), "the event's own person who walks in at their head must be free to come");
        string effects = Code("Visitors/VisitEffects.cs");
        C(effects.Contains("public bool Brings(DilemmaContext ctx)") && effects.Contains("if (chance < 1f || !Could(ctx)) return false;"),
            "only when the visit would come at all (the setting off, or a people at war: the letter alone tells it, as before)");

        // ---------------- An answer without its visitor ----------------

        string play = Code("PlayOut/PlayOutEffects.cs");
        C(play.Contains("public override string Unavailable(DilemmaContext ctx)") && play.Contains("\"CC_Play_Why_VisitorGone\".Translate().Resolve()")
          && play.Contains("if (Here(o, ctx) || Alone().Count > 0) return null;"),
            "an answer that walks out to the event's visitor can't be given while they aren't on the map, unless it says where to go instead");
        C(Regex.IsMatch(play, @"var alone = Alone\(\);\s*if \(alone\.Count == 0\) return false;\s*p\.to = alone\[0\];"), "with a place instead, the visit goes there on its own");
        C(Regex.IsMatch(Code("PlayOut/PlayOuts.cs"), @"Place,\s*Edge\s*\}"), "the edge of the colony's land is a place a visit can go");
        C(play.Contains("s == PlaySpot.Place || s == PlaySpot.Edge") && Code("PlayOut/PlaySpots.cs").Contains("public static IntVec3 EdgeCell(Map map, Pawn pawn)")
          && Code("PlayOut/PlayOutTracker.cs").Contains("case PlaySpot.Edge:"), "out where the road comes in, on their own, as at the gate or the fields");
        C(ws.Contains("if (!DilemmaEffect_Visit.Here(ctx.stranger, ctx)) tags.Add(VisitorGone);"), "and the answer's words are picked for it: \"visitorGone\"");

        // ---------------- Every world event's answer of the kind ----------------

        int answers = 0;
        var problems = new List<string>();
        foreach (var path in Directory.GetFiles(Path.Combine(root, "Defs", "WorldSituationDefs"), "*.xml"))
        {
            var doc = XDocument.Load(path);
            foreach (var d in doc.Root!.Elements("ColonyCouncil.WorldSituationDef"))
            {
                string def = d.Element("defName")?.Value ?? "?";
                foreach (var b in d.Element("beats")?.Elements("li") ?? Enumerable.Empty<XElement>())
                {
                    var beatEffects = b.Element("effects")?.Elements("li").ToList() ?? new List<XElement>();
                    string Cls(XElement li) => (li.Attribute("Class")?.Value ?? "").Replace("ColonyCouncil.", "");
                    bool bringsX = beatEffects.Any(li => Cls(li) == "VisitEffect_AtGate" && (li.Element("stranger")?.Value != "false" || li.Element("withStranger")?.Value == "true"));
                    bool bringsGroup = beatEffects.Any(li => Cls(li) == "VisitEffect_Come" && li.Element("group")?.Value == "true");
                    foreach (var o in b.Element("options")?.Elements("li") ?? Enumerable.Empty<XElement>())
                        foreach (var li in (o.Element("effects")?.Elements("li") ?? Enumerable.Empty<XElement>()).Concat(o.Element("riskEffects")?.Elements("li") ?? Enumerable.Empty<XElement>()))
                        {
                            if (Cls(li) != "DilemmaEffect_Visit") continue;
                            string to = li.Element("to")?.Value ?? "Person", whom = li.Element("whom")?.Value ?? "B";
                            bool x = to == "Stranger" || to == "Person" && whom == "X", group = to == "Person" && whom == "Group";
                            if (!x && !group) continue;
                            answers++;
                            if (li.Element("orTo") != null) continue;
                            if (x && !bringsX || group && !bringsGroup) problems.Add(def + "/" + b.Element("key")?.Value);
                        }
                }
            }
        }
        C(answers >= 5 && problems.Count == 0, $"every world event's answer that walks out to its visitor ({answers}) is in a beat that brings them, or has a place to go instead" +
                                               (problems.Count > 0 ? ": " + string.Join(", ", problems) : ""));
        C(File.ReadAllText(Path.Combine(root, "Tools", "playout_audit.py")).Contains("def strangers():"), "the play-out audit checks it too, every run");
        string world = File.ReadAllText(Path.Combine(root, "Defs", "WorldSituationDefs", "Rimfolk_WS_World.xml"));
        C(world.Contains("<work>hunt</work><group>true</group>") && world.Contains("<to>Person</to><whom>Group</whom>"),
            "the rival colony's hunters are the ones the colony walks out to, not the settlers' leader who isn't there");
        C(Code("PlayOut/PlayOuts.cs").Contains("case PlayWho.Group:") && Code("Visitors/VisitorTracker.cs").Contains("t.groupFor.group = people.Where(p => p != null && p.Spawned).ToList();"),
            "the group is whoever of them is on the map, from when they arrive");

        // ---------------- The great caravan ----------------

        var roads = XDocument.Load(Path.Combine(root, "Defs", "WorldSituationDefs", "Rimfolk_WS_Variety3_Roads.xml"));
        var caravan = roads.Root!.Elements("ColonyCouncil.WorldSituationDef").First(d => d.Element("defName")?.Value == "Rimfolk_WS_GreatCaravan");
        var hills = caravan.Element("beats")!.Elements("li").First(b => b.Element("key")?.Value == "hills");
        C(hills.Element("effects")!.Elements("li").Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.VisitEffect_AtGate"), "the master's rider comes to the gate with the beat");
        var told = hills.Element("options")!.Elements("li").First();
        var visit = told.Element("effects")!.Elements("li").First(li => li.Attribute("Class")?.Value == "ColonyCouncil.DilemmaEffect_Visit");
        C(visit.Element("to")?.Value == "Stranger" && visit.Element("orTo")?.Elements("li").Any(li => li.Value == "Edge") == true,
            "the hill road told: out to the rider at the gate, or, if he has gone, out to the edge of the land where the train's road comes in");
        string plain = told.Element("outcome")?.Value ?? "", alone = told.Element("outcomes")?.Elements("li").FirstOrDefault()?.Value ?? "";
        C(plain.StartsWith("[!visitorGone]") && alone.StartsWith("[visitorGone]") && alone.Contains("edge of the colony's land") && alone.Contains("writes the old hill road down"),
            "and its words say which: the road drawn in the dust for the rider, or written down and taken out to the edge");
        var foretold = told.Element("effects")!.Elements("li").FirstOrDefault(li => li.Attribute("Class")?.Value == "ColonyCouncil.WorldEffect_Foretold");
        C(foretold?.Element("ending")?.Value == "safe" && (foretold.Element("when")?.Value ?? "").Contains("{PLACE}"),
            "the card says what the answer does later, when the train reaches the town: the safe ending's goodwill and prosperity");
        var safe = caravan.Element("endings")!.Elements("li").First(e => e.Element("key")?.Value == "safe");
        C(safe.Element("requires")?.Elements("li").Any(li => li.Value == "warned") == true && safe.Element("chance") == null
          && caravan.Element("endings")!.Elements("li").First().Element("key")?.Value == "safe",
            "the road told makes it certain: the safe ending is the first and needs only the warning");
        var safeEffects = safe.Element("effects")!.Elements("li").ToList();
        C(safeEffects.Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.WorldEffect_Goodwill" && li.Element("amount")?.Value == "4")
          && safeEffects.Any(li => li.Attribute("Class")?.Value == "ColonyCouncil.WorldEffect_Fortune" && li.Element("amount")?.Value == "6"),
            "then +4 goodwill and +6 prosperity at the town, besides the +4 at the answer");
        C((safe.Element("texts")?.Elements("li").First().Value ?? "").Contains("think better of the colony for it"), "the train's letter says what the road did for them and for the colony");
        string situationEffects = Code("World/SituationEffects.cs");
        C(ws.Contains("text += (text.Length > 0 ? \"\\n\\n\" : \"\") + \"CC_WS_Moved\".Translate(") && situationEffects.Contains("WorldSituations.Moved(\"CC_WS_Moved_Goodwill\"")
          && situationEffects.Contains("\"CC_WS_Moved_Fortune\""), "and every world event's ending letter says what it changed, in numbers, as its effects apply");

        // ---------------- The words ----------------

        var keyed = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml").Select(File.ReadAllText));
        foreach (var key in new[] { "CC_Play_Why_VisitorGone", "CC_Courier_End_Cold", "CC_Courier_End_Heat", "CC_WS_Moved" })
        {
            var m = Regex.Match(keyed, "<" + key + ">([^<]*)</" + key + ">");
            C(m.Success && !m.Groups[1].Value.Contains("—") && !m.Groups[1].Value.Contains("!") && Regex.Matches(m.Groups[1].Value, @"[.?]").Count == 1, key + ": one plain sentence");
        }
        foreach (var key in new[] { "CC_Play_Where_Edge", "CC_Play_Report_Edge", "CC_Impact_Spot_Edge", "CC_Impact_Foretold_Goodwill", "CC_Impact_Foretold_Fortune", "CC_WS_Moved_Goodwill", "CC_WS_Moved_Fortune" })
            C(Regex.IsMatch(keyed, "<" + key + ">[^<]+</" + key + ">"), key + " is written");
        return n;
    }
}
