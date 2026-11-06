using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// A vendetta left open at its avenger's death passes to whoever was closest to them (the player's decision, 6 November
/// 2026; Secrets/VendettaMath.cs): a partner, a friend or a relative, by how close they really were, never to blood kin
/// first. Closeness from what the game and Rimfolk know (opinion, the social web's bonds, a partner or a relative as one
/// part among them), the kind less ready and the bloodthirsty more; who may take it up at all as before; noted at the
/// death while the bonds are on record; and the words follow who really takes it.
/// </summary>
public static class VendettaTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "vendettas passed on: " + what);
        }

        VendettaMath.Bond B(int opinion, float web = 0f, bool partner = false, bool kin = false, bool kind = false, bool bloodlust = false) =>
            new VendettaMath.Bond(opinion, web, partner, kin, kind, bloodlust);
        int Heir(params VendettaMath.Bond[] bonds) => VendettaMath.Heir(bonds);

        // ================= How close =================

        C(VendettaMath.Closeness(B(30, 25f, partner: true, kin: false)) == 30 + 25 + VendettaMath.PartnerWeight, "opinion, the web's bonds and a partner, added");
        C(VendettaMath.Closeness(B(30, 0f, kin: true)) == 30 + VendettaMath.KinWeight, "a relative by blood as one part among them");
        C(VendettaMath.PartnerWeight > VendettaMath.KinWeight && VendettaMath.KinWeight > 0f && VendettaMath.KinWeight < VendettaMath.AtLeast,
            "a partner counts for more than a relative, and being a relative alone is never enough");
        C(VendettaMath.WouldTakeUp(B(60)) && !VendettaMath.WouldTakeUp(B(59)), "a friend by opinion alone at 60, as before");
        C(VendettaMath.WouldTakeUp(B(20, 40f)) && !VendettaMath.WouldTakeUp(B(10, 40f)), "a strong bond in the web brings a friend there sooner");
        C(!VendettaMath.WouldTakeUp(B(0, kin: true)) && !VendettaMath.WouldTakeUp(B(-30, kin: true)) && !VendettaMath.WouldTakeUp(B(30, -50f, kin: true)),
            "blood alone never: a relative who didn't care for them, or a rival, doesn't take it up");
        C(VendettaMath.WouldTakeUp(B(40, kin: true)) && VendettaMath.WouldTakeUp(B(20, partner: true)), "a relative or a partner who was close does");
        C(!VendettaMath.WouldTakeUp(B(70, -30f)), "a rivalry in the web takes away what the opinion gives");

        // ================= Whoever was closest, by how close they really were =================

        C(Heir(B(40, kin: true), B(70, 50f)) == 1, "a close friend before a sister they were less close to: never blood kin first");
        C(Heir(B(70, 50f), B(40, kin: true)) == 0, "and the same in either order");
        C(Heir(B(50, 40f, kin: true), B(60)) == 0, "a relative who was also the closest takes it");
        C(Heir(B(30, partner: true), B(70, 60f)) == 1, "a partner is not first either: a friend closer than the partner takes it");
        C(Heir(B(60, 20f, partner: true), B(70, 20f)) == 0, "a partner who was close takes it before a friend a little less close");
        C(Heir(B(30, 10f), B(-20, kin: true), B(45, -40f)) == -1, "nobody close enough: it ends with them");
        C(Heir() == -1 && VendettaMath.Heir(null) == -1, "nobody at all");
        C(Heir(B(80), B(80)) == 0, "where two are as close, the first");

        // ================= Their nature =================

        C(!VendettaMath.WouldTakeUp(B(100, kind: true)) && VendettaMath.WouldTakeUp(B(120, kind: true)), "the kind take it up only when twice as close");
        C(VendettaMath.WouldTakeUp(B(40, bloodlust: true)) && !VendettaMath.WouldTakeUp(B(39, bloodlust: true)), "the bloodthirsty at two thirds");
        C(!VendettaMath.WouldTakeUp(B(-50, bloodlust: true)) && VendettaMath.Willing(B(-50, bloodlust: true)) < 0f, "never one who disliked them, however bloodthirsty");
        C(Heir(B(110, kind: true), B(70)) == 1, "the closest who would take it up: a kind friend who won't makes way for the next");
        C(Heir(B(150, kind: true), B(70)) == 0, "a kind one close enough is still the closest");

        // ================= The words =================

        C(VendettaMath.Tag(B(80, partner: true, kin: true)) == "partner" && VendettaMath.Tag(B(80, kin: true)) == "kin" && VendettaMath.Tag(B(80, 30f)) == "friend",
            "the told lines say what they were: a partner, a relative, or a friend");
        C(VendettaMath.NotNoted < 0 && VendettaMath.Nobody == 0, "an heir not yet noted, or nobody close enough, are told apart from a colonist's id");

        // ================= The game's side =================

        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Code(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string ends = Code("Secrets/VendettaEnds.cs"), tracker = Code("Secrets/Vendettas.cs");
        C(ends.Contains("VendettaMath.Heir(") && ends.Contains("new VendettaMath.Bond(p.relations.OpinionOf(dead), comp?.social?.Warmth(p, dead)")
          && !ends.Contains("score = 300f") && !Regex.IsMatch(ends, @"familyByBloodRelation\)\)\s*score"),
            "the heir by the rule: the game's opinion, the web's warmth, a partner and a relative; no blood kin first");
        C(ends.Contains("!p.DevelopmentalStage.Juvenile()") && ends.Contains("p.IsColonist && !p.IsQuestLodger()") && ends.Contains("People.HasInnerLife(p)")
          && ends.Contains("!t.vendettas.Any(x => x.avenger == p && x.faction == v.faction && x.Active)"),
            "who may take it up as before: a grown colonist with an inner life, never a child or a lodger, never one already sworn on the same people");
        C(ends.Contains("!v.faction.HostileTo(Faction.OfPlayer) || Vendetta.KillRecord(v.faction) == null) continue;"), "passed on only while the war with the killers goes on");
        int swear = tracker.IndexOf("if (killers != null && S.vendettas) Swear(comp, p, killers, false);", StringComparison.Ordinal);
        int note = tracker.IndexOf("VendettaEnds.NoteHeir(comp, this, p);", StringComparison.Ordinal);
        C(swear > 0 && note > swear, "the closest noted at the death, after those who swear for the dead (sworn on that people already)");
        C(tracker.Contains("Scribe_Values.Look(ref heirId, \"heirId\", VendettaMath.NotNoted);") && ends.Contains("if (v.heirId == VendettaMath.Nobody) return null;"),
            "the one noted is kept in the save; an older save's is worked out at the day's look");
        string comp = Code("CouncilGameComponent.cs"), dynamics = Code("CouncilDynamics.cs");
        int social = dynamics.IndexOf("Safe(\"social web\", () => social.Daily(this));", StringComparison.Ordinal);
        int vendettas = dynamics.IndexOf("Safe(\"vendettas\", () => vendettas.Daily(this));", StringComparison.Ordinal);
        C(comp.Contains("Safe(\"losses\", () => vendettas.Hourly(this));") && social > 0 && vendettas > social,
            "why it is noted at the death: the day's look lets the web's bonds with the dead go before the vendettas pass on");
        C(ends.Contains("VendettaMath.Tag(BondOf(comp, heir, v.avenger))"), "the lines' tag from what they were to the dead");

        // ================= The words follow who really takes it =================

        var pools = Directory.GetFiles(Path.Combine(root, "Defs", "FlavorPoolDefs"), "*.xml").Select(f => XDocument.Load(f)).ToList();
        List<string> LinesOf(string pool) => pools.SelectMany(d => d.Root!.Elements())
            .Where(e => e.Element("defName")?.Value == pool || e.Element("pool")?.Value == pool)
            .SelectMany(e => e.Element("lines")?.Descendants("li") ?? Enumerable.Empty<XElement>()).Select(l => l.Value).ToList();
        foreach (var pool in new[] { "Council_Pool_VendettaHeir", "Council_Journal_VendettaHeir" })
        {
            var lines = LinesOf(pool);
            var said = lines.Where(l => !ShippedLines.Retired.Contains(l)).ToList();
            C(lines.Any(l => ShippedLines.Retired.Contains(l)), $"{pool}: the shipped line that made it the family's is retired, still in the defs");
            C(said.All(l => !Regex.IsMatch(l, @"family's oath|family debt|the family's|blood kin", RegexOptions.IgnoreCase)), $"{pool}: no line said makes the vendetta the family's");
            C(said.Count(l => l.StartsWith("[kin]")) >= 2, $"{pool}: a relative who takes it up has words of their own, kin and close besides");
            C(said.Where(l => Regex.IsMatch(l, @"\b(brother|sister|son|daughter|father|mother|blood)\b", RegexOptions.IgnoreCase)).All(l => l.StartsWith("[kin")),
                $"{pool}: a line naming blood kin is said only when the one who takes it up is kin");
            C(said.Where(l => Regex.IsMatch(l, @"\b(wife|husband|partner|lover)\b", RegexOptions.IgnoreCase)).All(l => l.StartsWith("[partner")),
                $"{pool}: a line naming a partner only for a partner");
        }
        string closeness = File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Lines_VendettaCloseness.xml"));
        C(closeness.Contains("<pool>Council_Pool_VendettaHeir</pool>") && closeness.Contains("<pool>Council_Journal_VendettaHeir</pool>"), "the new words join both pools");
        string almanac = File.ReadAllText(Path.Combine(root, "Defs", "AlmanacArticleDefs", "Almanac_Council.xml"));
        C(!almanac.Contains("blood kin first, then") && almanac.Contains("whoever was closest to them") && almanac.Contains("never blood kin first"), "the Almanac says whoever was closest, never blood kin first");
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        C(!readme.Contains("their kin, their partner or a close friend") && readme.Contains("whoever was closest to them"), "the README says the same");
        return n;
    }
}
