using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

/// <summary>
/// The review of the peoples' events (docs/review-peoples.md, Tools/peoples_impact_audit.py): the bugs found, each kept fixed, the
/// answer given the effect its words already said, and the pilgrim fellowship's event added. The peoples' jobs never move one of the
/// colony drafted, in a break, down or on the player's order; the enclave's instalment letter waits while the enclave is at war;
/// a Signal Line of the old ages speaks of signal towers, not radio masts; the talker who argues the league's red ink learns by
/// it; and a footsore palmer of the fellowship comes to the gate for the colony's doctor.
/// </summary>
public static class ReviewPeoplesTests
{
    public static int Run(Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "review of the peoples' events: " + what);
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
        string Dn(XElement e) => (string)e.Element("defName")!;
        XElement? Ext(XElement e) => e.Element("modExtensions")?.Elements("li").FirstOrDefault(li => (string?)li.Attribute("Class") == "ColonyCouncil.HappeningExtension");

        // ================= Bugs =================

        // ---- The peoples' jobs never take one of the colony out of the player's hands ----
        // Rounds 4 to 9's doors (round 10 goes through round 9's) sent "one of theirs (or one of the colony's)" to work at a place
        // with a forced job, checking only that they were up and not in a break. No scene asked whether the player had set its
        // colonist to something of their own, and some not whether they were drafted (the one who sits with the shouting mind);
        // the Vault's sky (PeoplesE.Start), the Circuit's bout, the broker's rescue and the doctor sent to a visitor
        // (OutsiderTend) the same.
        var review = Code(Path.Combine("Peoples", "PeoplesReview.cs"));
        C(review.Contains("public static bool Held(Pawn p) => p != null && (p.Drafted || p.InMentalState || p.Downed || (p.CurJob?.playerForced ?? false));"),
            "one drafted, in a break, down or on the player's order is held from the peoples' jobs");
        foreach (var round in new[] { 4, 5, 6, 7, 8, 9 })
        {
            var send = Between(Code(Path.Combine("Peoples", "Events" + round, "PeopleEvents" + round + ".cs")), "public static bool Send(Pawn p, JobDef job", "catch (Exception e)");
            C(send.Contains("PeoplesReview.Held(p)") && send.IndexOf("PeoplesReview.Held(p)", StringComparison.Ordinal) < send.IndexOf("StartJob", StringComparison.Ordinal),
                $"round {round}'s door refuses one held before it starts the job");
        }
        C(Code(Path.Combine("Peoples", "Events10", "PeopleEvents10.cs")).Contains("public static bool Send(Pawn p, JobDef job, LocalTargetInfo target, int ticks) => PeopleEvents9.Send(p, job, target, ticks);"),
            "round 10's door is round 9's");
        C(Between(Code(Path.Combine("Peoples", "PeoplesE.cs")), "public static bool Start(Pawn pawn, JobDef def", "pawn.jobs.StartJob(").Contains("PeoplesReview.Held(pawn)"),
            "the Choir's ring and the Vault's sky refuse one held");
        C(Between(Code(Path.Combine("Peoples", "PeoplesD2.cs")), "private static bool StartBout(Visit v)", "TryTakeOrderedJob").Contains("PeoplesReview.Held(trainer)"),
            "the Circuit's bout waits for a trainer who is free");
        var rescue = Between(Code(Path.Combine("Peoples", "Events4", "BrokerPod.cs")), "if (how != \"rescue\") return;", "catch (Exception e)");
        C(rescue.Contains("PeoplesReview.Held(doctor)") && rescue.Contains("!PeoplesReview.Held(p) && !p.WorkTagIsDisabled(WorkTags.Caring)")
          && rescue.IndexOf("PeoplesReview.Held(doctor)", StringComparison.Ordinal) < rescue.IndexOf("TryTakeOrderedJob", StringComparison.Ordinal),
            "the broker is rescued by the one named, else by the nearest other of the colony who is free");
        C(Between(Code(Path.Combine("Health", "OutsiderTend.cs")), "public static bool Send(Pawn doctor", "var jobs = new List<Job>();").Contains("PeoplesReview.Held(doctor)"),
            "a doctor sent to a visitor is never one held");
        // Every job the peoples' code starts in a door that may send the colony's own people goes through the check.
        foreach (var f in Directory.GetFiles(Path.Combine(src, "Peoples"), "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(text, @"public static bool (Send|Start)\(Pawn (\w+), JobDef"))
            {
                // The visitor who works on a colonist (PeopleWorkOn) sends only theirs.
                if (f.EndsWith("PeopleWorkOn.cs")) continue;
                var body = Between(text.Substring(m.Index), m.Value, "\n        }\n");
                C(body.Contains("PeoplesReview.Held(" + m.Groups[2].Value + ")") || body.Contains("=> PeopleEvents9.Send("),
                    $"{Path.GetFileName(f)}: its door {m.Groups[1].Value} refuses one held");
            }
        }

        // ---- The enclave's instalment letter waits for peace ----
        // "A people gone or at war ends them", its note said, but only a people gone did: at war the enclave's letter still came,
        // its courier waiting at the gate, and the default answer wrote a default against the colony.
        var terms = Code(Path.Combine("Peoples", "Events2", "EnclaveBody.cs"));
        C(Between(terms, "public static TermsRecord Due()", "public static TermsRecord Get(").Contains("!t.faction.HostileTo(Faction.OfPlayer)"),
            "no instalment is due from a people at war with the colony");
        var hour = Between(terms, "public static bool Hour(Visit v, int now)", "public static void Paid(");
        C(hour.Contains("if (r.faction.HostileTo(Faction.OfPlayer))") && hour.Contains("v.until = Mathf.Max(v.until, now + 30 * GenDate.TicksPerDay);")
          && hour.IndexOf("HostileTo", StringComparison.Ordinal) < hour.IndexOf("PeopleEvents2.Fire(", StringComparison.Ordinal),
            "at war the letter waits, and the terms are kept for the peace");

        // ---- A Signal Line of the old ages speaks of signal towers ----
        // The Line arises in every age; its news and its crew spoke of radio masts, headsets and a beacon's call to a people of
        // flags and fire.
        var e2 = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents2.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        var e7 = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents7.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        // A text of the later ages: tagged for the industrial or spacer age alone, or for the crew's mast (a Line of those ages:
        // LineCrew.Tower is for the older).
        bool Late(string t)
        {
            var m = Regex.Match(t, @"^\[([^\]]*)\]");
            var tags = m.Success ? m.Groups[1].Value.Split(',', '|') : Array.Empty<string>();
            return !tags.Contains("age0") && !tags.Contains("age1") && (tags.Contains("age2") || tags.Contains("age3") || tags.Contains("mast"));
        }
        C(Code(Path.Combine("Peoples", "Events7", "LineCrew.cs")).Contains("public static bool Tower(Faction line) => PeopleEvents.AgeOf(line) < 2;"),
            "the crew's tower is the older ages', its mast the later ones'");
        foreach (var e in new[] { e2["Rimfolk_Evt_NewsForSale"], e7["Rimfolk_Evt_LineCrew"] })
        {
            var texts = Ext(e)!.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
            C(texts.Where(t => Regex.IsMatch(t, @"\b(radio|headset|beacon|operator|wire)\b")).All(Late),
                $"{Dn(e)}: what is said of radio and wire is said only of a Line of the industrial or spacer age");
        }
        var news = Ext(e2["Rimfolk_Evt_NewsForSale"])!.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        C(news.Any(t => t.StartsWith("[age0|age1] ")) && news.Any(t => t.StartsWith("[outpost,age0|age1] ")) && news.Any(t => t.StartsWith("[wreck,age0|age1] ")),
            "the news of the old ages' Line: from the towers, by flag and fire, a bandits' camp seen, something seen to fall");
        C(Between(Code(Path.Combine("Peoples", "Events2", "NewsForSale.cs")), "protected override void AddTags(", "public override string Unavailable(").Contains("PeopleEvents.AgeTags(tags, ctx.faction);"),
            "the news is told in the Line's own age");

        // ---- The Fleet's shuttle comes only from a Fleet that has one ----
        // The Fleet arises in every age and falls back like any people; its shuttle asked only that the colony knew what comes down
        // out of the sky, so a Fleet of the tribal or medieval age landed one.
        C(Between(Code(Path.Combine("Peoples", "PeoplesF2.cs")), "public class HappeningWorker_FleetShuttle", "public static ThingDef CasketDef").Contains("PeoplesFMath.ShuttleAge(Peoples.AgeOf(f))"),
            "the Fleet's shuttle lands only from a Fleet of the industrial or spacer age");

        // ================= Answers given real effects =================

        // The league's red ink, "Send {A} to argue the entry": {A} disputed it at the gate, and only the page changed.
        var e3 = XDocument.Load(Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeopleEvents3.xml")).Root!.Elements("IncidentDef").ToDictionary(Dn);
        var argue = Ext(e3["Rimfolk_Evt_RedInk"])!.Element("options")!.Elements("li").ElementAt(1);
        bool Learns(XElement? effects, int xp) => effects?.Elements("li").Any(li => (string?)li.Attribute("Class") == "ColonyCouncil.DilemmaEffect_Learn"
                                                                                   && (string?)li.Element("skill") == "Social" && (string?)li.Element("xp") == xp.ToString()) == true;
        C(Learns(argue.Element("effects"), 200) && Learns(argue.Element("riskEffects"), 100), "the talker who argues the red ink learns Social by it, more when it goes well");

        // ================= Added: the footsore palmer =================

        var file = Path.Combine(root, "Defs", "IncidentDefs", "Rimfolk_Events_PeoplesReview.xml");
        C(File.Exists(file), "the review's events have their own file");
        if (!File.Exists(file)) return n;
        var added = XDocument.Load(file).Root!.Elements("IncidentDef").ToDictionary(Dn);
        C(added.Keys.SequenceEqual(new[] { "Rimfolk_Evt_PplR7_Footsore" }), "one event added, named for the review");
        var foot = added["Rimfolk_Evt_PplR7_Footsore"];
        var fx = Ext(foot)!;
        var worker = Between(review, "public class HappeningWorker_PplR7Footsore", "\n    }\n");
        C((string?)foot.Element("workerClass") == "ColonyCouncil.HappeningWorker_PplR7Footsore" && worker.Length > 0, "its worker is the review's own");
        C(fx.Element("callings")?.Elements("li").Select(l => l.Value).SequenceEqual(new[] { "pilgrims" }) == true
          && worker.Contains("PeoplesD.Friendly(PeoplesMath.Pilgrims)") && worker.Contains("\"Rimfolk_Pilgrims_Pilgrim\""),
            "only from a pilgrim people at peace with the colony, and the one who comes is one of their pilgrims");
        float chance = float.Parse((string)foot.Element("baseChance")!, System.Globalization.CultureInfo.InvariantCulture);
        C(chance >= 0.3f && chance <= 0.6f && int.Parse((string)foot.Element("minRefireDays")!) >= 30 && int.Parse((string)foot.Element("earliestDay")!) >= 10
          && (string?)fx.Element("group") == "Visitors", "a small, spread chance among the visitors");
        C(worker.Contains("ctx.placeName =") && worker.Contains("ctx.tale = Hurt(ctx.stranger);") && worker.Contains("ctx.stranger = Arrivals.Make("),
            "every placeholder its words use is filled: the hostel, the hurt, the palmer");
        C(worker.Contains("\"Council_Ail_Blisters\", \"Council_Ail_SprainedAnkle\", \"Council_Ail_SoreKnees\"") && worker.Contains("GetNamedSilentFail(n)"),
            "the palmer comes with one of the road's small ailments, looked up softly");
        C(worker.Contains("!p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor) && !PeoplesReview.Held(p)"), "the doctor asked is one who doctors and is free");
        var texts2 = fx.Element("texts")!.Elements("li").Select(l => l.Value).ToList();
        C(texts2.Count >= 5 && texts2.Any(t => t.StartsWith("[age0|age1] ")) && texts2.Any(t => t.StartsWith("[age2|age3] ")), "five or more texts, in the forms of the fellowship's age");
        C(worker.Contains("PeopleEvents.AgeTags(tags, ctx.faction);"), "told in the fellowship's own age");
        var options = fx.Element("options")!.Elements("li").ToList();
        string Classes(XElement o) => string.Join(",", o.Element("effects")!.Elements("li").Select(li => ((string?)li.Attribute("Class") ?? "").Replace("ColonyCouncil.", "")));
        C(options.Count == 3 && (string?)fx.Element("defaultOption") == "2", "three answers, no answer the last");
        C(Classes(options[0]).Contains("DilemmaEffect_TendStranger") && Classes(options[0]).Contains("DilemmaEffect_Learn") && Classes(options[0]).Contains("DilemmaEffect_Remember"),
            "the doctor sees to the palmer at the gate with a medicine from the stores, learns by it, and the fellowship remembers who did");
        C(Classes(options[1]).Contains("DilemmaEffect_CarryOut") && (string?)options[1].Element("effects")!.Elements("li").First().Element("to") == "Stranger"
          && (string?)options[1].Element("effects")!.Elements("li").First().Element("what") == "medicine",
            "the medicine for the road is carried out to the palmer");
        C(Classes(options[2]) == "DilemmaEffect_SendAway", "the shut gate sends the palmer on");
        var lines = fx.Element("lines")!.Elements("li").Select(l => l.Value).ToList();
        var tags = lines.Select(l => Regex.Match(l, @"^\[([^\],]+)").Groups[1].Value).Distinct().ToList();
        C(tags.Count == 3 && tags.All(t => worker.Contains("\"" + t + "\"")), "every line's moment is one the code says it at");
        C(lines.All(l => Regex.Matches(Regex.Replace(l, @"^\[[^\]]*\]\s*", ""), @"[.?](\s|$)").Count == 1), "every spoken line one sentence");
        // The words as shown: without the comments, the xml line and the lines' tags (a tag may say !lowtech).
        var all = Regex.Replace(File.ReadAllText(file), "<!--.*?-->|<\\?xml[^>]*>|\\[[^\\]]*\\]", "", RegexOptions.Singleline);
        C(!all.Contains('—') && !all.Contains('!') && !Regex.IsMatch(all, @"\bin \d+ (days?|hours?)\b"), "plain words, no countdowns");
        C(!Regex.IsMatch(all, @"\b(she|her|hers|he|him|his)\b"), "nobody of theirs is given a sex the game didn't");
        C(!Regex.IsMatch(all.ToLowerInvariant(), @"\b(dynasty|dynasties|bloodline|heir to the house)\b"), "the colony is the family: no houses");

        // ================= The review's own measure =================

        C(File.Exists(Path.Combine(root, "Tools", "peoples_impact_audit.py")), "the review's measure can be run again");
        string doc = File.ReadAllText(Path.Combine(root, "docs", "review-peoples.md"));
        C(doc.Contains("<!-- peoples_impact_audit: begin -->") && doc.Contains("<!-- peoples_impact_audit: end -->"), "the review keeps the measure's part between its markers");
        return n;
    }
}
