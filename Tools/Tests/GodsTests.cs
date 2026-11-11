using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The gods (docs/design/gods.md, "the contract"): the pure rules kept from the core (favor and its drift, natures and
/// kinds, how real the gods are, the ties, how favor spills and how ties shift, the first favor sources, the domain table
/// as a fallback), the fourteen hand-made gods as defs (names by age, lore, sayings, believers' and scoffers' lines, stories
/// in pieces, authored ties, who reveres them), their hands in the game's events (the table, the quiet spells, the realness
/// forms), the lore rules (who reveres whom, names, stories in pieces, who believes, what a believer takes it for, names
/// worked out), the colony's own book, the unseen rule (no page, no favor told, no symbols), the words, the speech pool,
/// and the seams the presence part and stage 2 call.
/// </summary>
public static class GodsTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the gods: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));

        // ================= A god's domain from its words =================

        GodDomain DomainOf(string name, string type, string meme = null, params string[] memes)
        {
            GodsMath.DeityDomains(name, type, meme, null, memes.Select(m => (m, (string)null)), null, null, 1, 7, out var d, out _);
            return d;
        }
        C(DomainOf("Aranea", "Goddess of nature") == GodDomain.Wild && DomainOf("Kor", "God of war") == GodDomain.War && DomainOf("Elu", "Spirit of trees") == GodDomain.Wild,
            "the deity's type reads its domain: nature, war, trees");
        C(DomainOf("Zed", "God of technology") == GodDomain.Machine && DomainOf("Om", "Goddess of family") == GodDomain.Hearth && DomainOf("Vey", "God of darkness") == GodDomain.Night
          && DomainOf("Ul", "God of the void") == GodDomain.Void && DomainOf("Tor", "God of storms") == GodDomain.Storm && DomainOf("Ash", "Spirit of ranching") == GodDomain.Hunt,
            "technology, family, darkness, the void, storms, ranching");
        C(DomainOf("Ilu", "Creator of space") == GodDomain.Machine || DomainOf("Ilu", "Creator of space") == GodDomain.Night, "space: the machine or the night");
        C(DomainOf("Nox", "", null, "NaturePrimacy") == GodDomain.Wild && DomainOf("Nox", "", null, "Raider") == GodDomain.War && DomainOf("Nox", "", null, "Transhumanist") == GodDomain.Machine
          && DomainOf("Nox", "", null, "Inhuman") == GodDomain.Void && DomainOf("Nox", "", null, "AM_WaterPrimacy") == GodDomain.Waters && DomainOf("Nox", "", null, "VME_Healthcare") == GodDomain.Healing,
            "a faith's memes, the game's and the player's mods' (Alpha Memes, Vanilla Ideology Expanded)");
        C(DomainOf("Nox", "", "Tunneler", "NaturePrimacy") == GodDomain.Craft || DomainOf("Nox", "", "Tunneler", "NaturePrimacy") == GodDomain.Dead,
            "the meme a deity came from counts more than the faith's others");
        GodsMath.DeityDomains("Mira", "", null, null, new[] { ("X_UnknownMeme", "fishing culture") }, null, null, 1, 3, out var dFish, out _);
        C(dFish == GodDomain.Waters && !GodsMath.KnowsMeme("X_UnknownMeme") && GodsMath.KnowsMeme("Raider"), "a meme Rimfolk doesn't know is read by its label");
        var s1 = GodsMath.NewScores();
        GodsMath.ScorePrecepts(new[] { "Trees_Desired", "Darkness_Preferred", "Nonsense" }, 1f, s1);
        C(s1[(int)GodDomain.Wild] > 0f && s1[(int)GodDomain.Night] > 0f && s1[(int)GodDomain.War] == 0f, "precepts lean a faith a little");
        var taken = new HashSet<GodDomain> { GodDomain.War };
        GodsMath.DeityDomains("Bo", "", null, null, new[] { ("Raider", (string)null), ("NaturePrimacy", (string)null) }, null, taken, 1, 5, out var dTaken, out _);
        C(dTaken == GodDomain.Wild, "a faith's second god takes another of its leanings rather than share the first's");
        GodsMath.DeityDomains("Bo", "God of war and storms", null, null, null, null, null, 1, 5, out var dTwo, out var sTwo);
        C(dTwo == GodDomain.War && sTwo == GodDomain.Storm || dTwo == GodDomain.Storm && sTwo == GodDomain.War, "a second domain when the words name two");
        GodsMath.DeityDomains("Bo", "God of war", null, null, null, null, null, 1, 5, out _, out var sNone);
        C(sNone == GodDomain.None, "no second domain unless it is at least two fifths of the first");
        var nothing = Enumerable.Range(0, 40).Select(i => GodsMath.AgeDefault(0, i, null)).ToList();
        C(nothing.All(d => GodsMath.AgeLean(0, d) > 0f) && nothing.Distinct().Count() >= 3 && GodsMath.AgeDefault(0, 9, null) == GodsMath.AgeDefault(0, 9, null),
            "a god with no words takes one of its age's usual domains, the same for the same seed");
        C(GodsMath.Domains.Length == 14 && GodsMath.Domains.Distinct().Count() == 14 && !GodsMath.Domains.Contains(GodDomain.None), "fourteen domains");

        // ================= Rimfolk's makers =================

        bool Often(Func<int, bool> f, float share) => Enumerable.Range(0, 200).Count(f) >= 200 * share;
        C(Enumerable.Range(0, 200).All(i =>
            {
                var l = GodsMath.MakerDomains(i % 4, i % 3 == 0, null, false, i);
                return l.Count >= 2 && l.Count <= 3 && l.Distinct().Count() == l.Count && !l.Contains(GodDomain.None);
            }), "two or three gods a people, each of its own domain");
        C(Enumerable.Range(0, 200).All(i => GodsMath.MakerDomains(1, true, null, false, i).Contains(GodDomain.War)) && Often(i => GodsMath.MakerDomains(0, false, "TemperateForest", false, i).Contains(GodDomain.Wild), 0.5f)
          && Often(i => GodsMath.MakerDomains(3, false, null, false, i).Contains(GodDomain.Machine), 0.5f) && Often(i => GodsMath.MakerDomains(1, false, "Desert", true, i).Contains(GodDomain.Waters), 0.3f),
            "raiders have a god of war, a forest people the wild, spacers the machine, a river people the waters");
        C(GodsMath.MakerDomains(2, false, "BorealForest", false, 77).SequenceEqual(GodsMath.MakerDomains(2, false, "BorealForest", false, 77)), "the same people, the same gods");
        C(GodsMath.MakerDomains(1, false, null, false, 3, 3).Count == 3, "a count asked for is kept");

        // ================= The age's form, kind and nature =================

        C(GodsMath.FormOf(0, GodDomain.War, GodDomain.None, 1) == GodForm.Spirit && GodsMath.FormOf(3, GodDomain.Harvest, GodDomain.None, 1) == GodForm.Archotech
          && GodsMath.FormOf(2, GodDomain.Machine, GodDomain.None, 1) == GodForm.MachineGod && GodsMath.FormOf(2, GodDomain.Craft, GodDomain.Machine, 1) == GodForm.MachineGod
          && GodsMath.FormOf(2, GodDomain.Craft, GodDomain.None, 1) == GodForm.HalfForgotten, "tribes' spirits, spacers' archotechs, the industrial's machine gods and half-forgotten ones");
        C(Enumerable.Range(0, 200).All(i => GodsMath.FormOf(1, GodDomain.War, GodDomain.None, i) == GodForm.OldGod)
          && Often(i => GodsMath.FormOf(1, GodDomain.Healing, GodDomain.None, i) == GodForm.Saint, 0.15f), "the medieval's old gods, and saints now and then for healing, the dead, the hearth and the road");
        C(GodsMath.KindOfForm(GodForm.Archotech) == GodKind.Archotech && GodsMath.KindOfForm(GodForm.Spirit) == GodKind.Ordinary && GodsMath.AgeOfForm(GodForm.Saint) == 1,
            "an archotech is cold and exact by its form");
        C(Often(i => GodsMath.NatureFor(GodDomain.Machine, GodKind.Archotech, i) == GodNature.Cold, 0.6f) && GodsMath.NatureFor(GodDomain.Way, GodKind.Trickster, 4) == GodNature.Playful
          && GodsMath.NatureFor(GodDomain.Void, GodKind.Hunger, 4) == GodNature.Jealous && GodsMath.NatureFor(GodDomain.War, GodKind.Ordinary, 9) == GodsMath.NatureFor(GodDomain.War, GodKind.Ordinary, 9),
            "natures by domain and kind: archotechs cold, the trickster playful, the Hunger jealous");
        C(GodsMath.Domains.All(d => GodsMath.NatureLeans(d).Length >= 2), "every domain leans to two natures or more");
        C(GodsMath.Natures.All(nn => GodsMath.Offends(nn) != GodOffence.None) && (GodsMath.Offends(GodNature.Jealous) & GodOffence.Rivals) != 0
          && (GodsMath.Offends(GodNature.Grieving) & GodOffence.Unburied) != 0, "every nature has something that offends it");
        C(GodsMath.Natures.All(nn => GodsMath.Kinds.All(k => GodsMath.Loudness(nn, k) >= 0f && GodsMath.Loudness(nn, k) <= 1f))
          && GodsMath.Loudness(GodNature.Wrathful, GodKind.Ordinary) > GodsMath.Loudness(GodNature.Cold, GodKind.Ordinary)
          && GodsMath.Loudness(GodNature.Wrathful, GodKind.Dead) < 0.1f && GodsMath.Loudness(GodNature.Patient, GodKind.Hunger) >= 0.8f,
            "how loudly a god answers: wrathful loud, cold quiet, the dead silent, the Hunger loud");

        // ================= Favour in words, and its drift =================

        C(GodsMath.Word(-100f) == FavourWord.Wrathful && GodsMath.Word(-60f) == FavourWord.Displeased && GodsMath.Word(-15f) == FavourWord.Unknown && GodsMath.Word(0f) == FavourWord.Unknown
          && GodsMath.Word(10f) == FavourWord.Noticed && GodsMath.Word(30f) == FavourWord.Pleased && GodsMath.Word(55f) == FavourWord.Favoured && GodsMath.Word(80f) == FavourWord.Beloved
          && GodsMath.Word(100f) == FavourWord.Beloved && GodsMath.Word(float.NaN) == FavourWord.Unknown, "seven words from wrathful to beloved, unknown in the middle");
        bool rising = true;
        for (float v = -100f; v < 100f; v += 0.5f)
            if (GodsMath.Word(v + 0.5f) < GodsMath.Word(v)) rising = false;
        C(rising && GodsMath.Clamp(250f) == 100f && GodsMath.Clamp(-250f) == -100f && GodsMath.Clamp(float.NaN) == 0f, "the words rise with favour, kept within -100 and 100");
        float half = GodsMath.Drift(80f, GodsMath.HalfLifeDays(GodNature.Proud, true), GodNature.Proud);
        C(half > 35f && half <= 40.5f, $"favour halves back toward unknown in its half-life ({half:0.0})");
        C(GodsMath.Drift(50f, 20f, GodNature.Patient) > GodsMath.Drift(50f, 20f, GodNature.Capricious) && GodsMath.Drift(-50f, 20f, GodNature.Wrathful) < GodsMath.Drift(-50f, 20f, GodNature.Generous),
            "a patient god's favour lasts, a capricious one's fades; a wrathful god's grudge lasts");
        C(GodsMath.Drift(3f, 30f, GodNature.Proud) == 0f && GodsMath.Drift(-3f, 30f, GodNature.Proud) == 0f && GodsMath.Drift(50f, 0f, GodNature.Proud) == 50f
          && GodsMath.Drift(-40f, 10f, GodNature.Proud) < 0f && GodsMath.Drift(-40f, 10f, GodNature.Proud) > -40f, "it comes to rest at unknown, never past it");
        C(GodsMath.Natures.All(nn => GodsMath.HalfLifeDays(nn, true) >= 15f && GodsMath.HalfLifeDays(nn, true) <= 60f), "slowly: a half-life of a fortnight to a year");
        C(GodsMath.PatronWorthy(10f) && !GodsMath.PatronWorthy(9f) && !GodsMath.PatronWorthy(float.NaN), "the colony's patron is a god that has noticed it at least");

        // ================= How a nature and a kind weigh favour =================

        C(GodsMath.Scale(10f, GodNature.Cold, GodKind.Archotech, FavourSource.Offering) == 0f && GodsMath.Scale(10f, GodNature.Cold, GodKind.Archotech, FavourSource.Rite) < GodsMath.Scale(10f, GodNature.Cold, GodKind.Archotech, FavourSource.Deed),
            "an archotech cares nothing for offerings and little for rites: it judges deeds");
        C(GodsMath.Scale(10f, GodNature.Patient, GodKind.Dead, FavourSource.Deed) < GodsMath.Scale(10f, GodNature.Patient, GodKind.Ordinary, FavourSource.Deed)
          && GodsMath.Scale(10f, GodNature.Patient, GodKind.Fading, FavourSource.Deed) > GodsMath.Scale(10f, GodNature.Patient, GodKind.Ordinary, FavourSource.Deed)
          && GodsMath.Scale(10f, GodNature.Jealous, GodKind.Hunger, FavourSource.Offering) > 10f, "the dead hardly hear; a fading god is grateful; the Hunger takes gladly");
        C(GodsMath.Scale(10f, GodNature.Capricious, GodKind.Ordinary, FavourSource.Deed) > GodsMath.Scale(10f, GodNature.Patient, GodKind.Ordinary, FavourSource.Deed)
          && GodsMath.Scale(-10f, GodNature.Wrathful, GodKind.Ordinary, FavourSource.Sacrilege) < GodsMath.Scale(-10f, GodNature.Patient, GodKind.Ordinary, FavourSource.Sacrilege),
            "a capricious god swings further; a wrathful one takes sacrilege harder");
        float grieving = GodsMath.Scale(-10f, GodNature.Grieving, GodKind.Ordinary, FavourSource.Neglect, GodOffence.Unburied);
        float proud = GodsMath.Scale(-10f, GodNature.Proud, GodKind.Ordinary, FavourSource.Neglect, GodOffence.Unburied);
        C(Math.Abs(grieving - -10f * GodsMath.Speed(GodNature.Grieving, false) * GodsMath.OffenceFactor) < 0.001f
          && Math.Abs(proud - -10f * GodsMath.Speed(GodNature.Proud, false)) < 0.001f
          && GodsMath.Scale(-10f, GodNature.Jealous, GodKind.Ordinary, FavourSource.Spill) == -10f * GodsMath.Speed(GodNature.Jealous, false) * GodsMath.OffenceFactor,
            "a loss that offends the god's nature counts half again: the dead left lying a grieving god, a rival honoured a jealous one");
        C(GodsMath.Scale(17f, GodNature.Cold, GodKind.Archotech, FavourSource.Dev) == 17f && GodsMath.Scale(float.NaN, GodNature.Cold, GodKind.Ordinary, FavourSource.Deed) == 0f,
            "the dev tools' favour unscaled; nothing for nothing");

        // ================= How real =================

        C(!GodsMath.AvatarsCome(GodRealness.Believed) && GodsMath.AvatarsCome(GodRealness.Felt) && GodsMath.AvatarsCome(GodRealness.Walking), "no avatars when the gods are only believed");
        C(GodsMath.PlainCard(GodRealness.Walking) && !GodsMath.PlainCard(GodRealness.Felt) && !GodsMath.PlainCard(GodRealness.Believed)
          && GodsMath.CanSpeak(GodRealness.Walking) && !GodsMath.CanSpeak(GodRealness.Felt), "named plainly and spoken with only when they walk");
        C(GodsMath.DisguiseShare(GodRealness.Felt) > GodsMath.DisguiseShare(GodRealness.Walking) && GodsMath.DisguiseShare(GodRealness.Felt) >= 0.8f, "felt: avatars nearly always in disguise");
        C(new[] { GodRealness.Believed, GodRealness.Felt, GodRealness.Walking }.Select(GodsMath.Voice).Distinct().Count() == 3, "each step its own voice in the news");

        // ================= Placeholders =================

        C(GodsMath.Placeholders.SequenceEqual(new[] { "GOD", "GODEPITHET", "GODOF" }), "{GOD}, {GODEPITHET} and {GODOF}");
        string validator = File.ReadAllText(Path.Combine(root, "Tools", "DefValidator", "Program.cs"));
        C(validator.Contains("wishPh.UnionWith(new[] { \"GOD\", \"GODEPITHET\", \"GODOF\" });"), "the def validator knows them");
        string gods = Read("Gods/Gods.cs");
        C(gods.Contains("values[GodsMath.God] = g.name;") && gods.Contains("values[GodsMath.Epithet]") && gods.Contains("values[GodsMath.Of] = GodOf(g);")
          && gods.Contains("g.name.Named(GodsMath.God)"), "filled into a letter's values and a line's arguments alike");

        // ================= Newborn, fading and dead =================

        C(GodsMath.NewbornDue(8, false, true, 0, 999) && !GodsMath.NewbornDue(7, false, true, 0, 999) && !GodsMath.NewbornDue(8, true, true, 0, 999)
          && !GodsMath.NewbornDue(8, false, false, 0, 999) && !GodsMath.NewbornDue(8, false, true, GodsMath.MaxNewborns, 999) && !GodsMath.NewbornDue(8, false, true, 0, 10),
            "a newborn when the colony's deeds keep serving a domain its faith has no god of, a few at most and far apart");
        C(GodsMath.Devotion(30f) == 10f && GodsMath.Devotion(-30f) == 0f && GodsMath.Devotion(float.NaN) == 0f && GodsMath.NewbornGrown > 50f, "a newborn grows with gains, never shrinks");
        C(GodsMath.KindNow(GodKind.Ordinary, GodKind.Ordinary, true, 0) == GodKind.Ordinary && GodsMath.KindNow(GodKind.Ordinary, GodKind.Ordinary, false, 0) == GodKind.Fading
          && GodsMath.KindNow(GodKind.Fading, GodKind.Ordinary, false, GodsMath.FadingToDeadDays) == GodKind.Dead
          && GodsMath.KindNow(GodKind.Fading, GodKind.Ordinary, false, GodsMath.FadingToDeadDays - 1) == GodKind.Fading
          && GodsMath.KindNow(GodKind.Fading, GodKind.Archotech, true, 500) == GodKind.Archotech && GodsMath.KindNow(GodKind.Dead, GodKind.Dead, true, 500) == GodKind.Ordinary
          && GodsMath.KindNow(GodKind.Hunger, GodKind.Hunger, false, 500) == GodKind.Hunger && GodsMath.KindNow(GodKind.Imprisoned, GodKind.Imprisoned, false, 500) == GodKind.Imprisoned
          && GodsMath.KindNow(GodKind.Fading, GodKind.Fading, false, 5000) == GodKind.Fading && GodsMath.KindNow(GodKind.Dead, GodKind.Dead, false, 5000) == GodKind.Dead,
            "a god nobody holds fades and in time falls silent; one taken up again comes back to its own kind; the Hunger and the Sleeper keep theirs");

        // ================= The first favour sources =================

        C(GodsMath.BigHarvest(40, 40) && !GodsMath.BigHarvest(39, 100) && !GodsMath.BigHarvest(100, 39), "a big harvest: many plants brought in and the store rising (felling alone doesn't count)");
        C(GodsMath.DeadLeftLying(4) && !GodsMath.DeadLeftLying(3) && GodsMath.DeadLyingDeed < 0f, "four of the dead left lying displease the gods of the dead");
        C(GodsMath.SacredDeath(GodDomain.Wild, true, false) == GodsMath.SacredSlaughtered && GodsMath.SacredDeath(GodDomain.Wild, false, true) == GodsMath.SacredHunted
          && GodsMath.SacredDeath(GodDomain.Hunt, false, true) == 0f && GodsMath.SacredDeath(GodDomain.Hunt, true, false) < 0f && GodsMath.SacredDeath(GodDomain.Wild, false, false) == 0f,
            "a sacred animal slaughtered or hunted displeases its gods; a hunt is the hunt's own gods' way");
        C(GodsMath.GreatBeastDeed > GodsMath.HarvestDeed && GodsMath.FuneralDeed > GodsMath.FuneralUngravedDeed && GodsMath.ThreatBeatenDeed > 0f, "each source small: a great beast more than a day's harvest, a grave more than none");

        // ================= Ties =================

        GodSketch G(int id, GodDomain d, int faith, int people, int gender = 1, int age = 1, GodDomain s = GodDomain.None) =>
            new GodSketch { id = id, domain = d, second = s, faiths = new[] { faith }, peoples = new[] { people }, age = age, gender = gender, kind = GodKind.Ordinary };
        C(GodTiesMath.Affinity(GodDomain.Storm, GodDomain.Harvest, out bool rom) < 0f && rom && GodTiesMath.Affinity(GodDomain.Hunt, GodDomain.Wild, out _) < 0f
          && GodTiesMath.Affinity(GodDomain.Harvest, GodDomain.Waters, out _) > 0f && GodTiesMath.Affinity(GodDomain.War, GodDomain.Healing, out _) < 0f
          && GodTiesMath.Affinity(GodDomain.Storm, GodDomain.Storm, out _) == 0f && GodTiesMath.Affinity(GodDomain.Harvest, GodDomain.Storm, out _) == GodTiesMath.Affinity(GodDomain.Storm, GodDomain.Harvest, out _),
            "natural rivals (the storm and the harvest over the rain, the hunter and the herd over the beasts) and allies, the same both ways");
        int pantheonRivals = 0, pantheonEstranged = 0, family = 0;
        for (int seed = 0; seed < 100; seed++)
        {
            var sky = G(1, GodDomain.Storm, 10, 100, 1);
            var field = G(2, GodDomain.Harvest, 20, 200, 2);
            var t = GodTiesMath.Pair(sky, field, null, null, seed);
            if (t.HasValue && t.Value.kind == GodTieKind.Rivals) pantheonRivals++;
            if (t.HasValue && t.Value.kind == GodTieKind.Estranged) pantheonEstranged++;
            var a = G(3, GodDomain.War, 30, 300);
            var b = G(4, GodDomain.Craft, 30, 300, 2);
            var f = GodTiesMath.Pair(a, b, null, null, seed);
            if (f.HasValue && (f.Value.kind == GodTieKind.Parent || f.Value.kind == GodTieKind.Siblings || f.Value.kind == GodTieKind.Twins || f.Value.kind == GodTieKind.Lovers)) family++;
        }
        C(pantheonRivals + pantheonEstranged >= 70 && pantheonEstranged > 0, $"the storm and the harvest of two faiths: rivals or lovers estranged ({pantheonRivals}, {pantheonEstranged})");
        C(family >= 60, $"a faith's own gods are a family: parent and child, siblings, twins, lovers ({family})");
        var thief = G(5, GodDomain.War, 50, 500);
        var robbed = G(6, GodDomain.Hearth, 60, 600);
        var stole = GodTiesMath.Pair(thief, robbed, null, (c, p) => c == 500 && p == 600, 1);
        C(stole.HasValue && stole.Value.kind == GodTieKind.StolePeople && stole.Value.a == 5, "a people that arose from another: its god took the other's people");
        int hostileRivals = 0, peaceRivals = 0;
        for (int seed = 0; seed < 300; seed++)
        {
            var x = G(7, GodDomain.Way, 70, 700);
            var y = G(8, GodDomain.Machine, 80, 800);
            var war = GodTiesMath.Pair(x, y, (p, q) => -1, null, seed);
            var peace = GodTiesMath.Pair(x, y, (p, q) => 0, null, seed);
            if (war.HasValue && (war.Value.kind == GodTieKind.Rivals || war.Value.kind == GodTieKind.CastDown)) hostileRivals++;
            if (peace.HasValue && peace.Value.kind == GodTieKind.Rivals) peaceRivals++;
        }
        C(hostileRivals > peaceRivals + 50, $"peoples at war set their gods against each other ({hostileRivals} against {peaceRivals})");
        var world = new List<GodSketch>();
        var rnd = new Random(5);
        for (int i = 1; i <= 40; i++)
            world.Add(G(i, GodsMath.Domains[rnd.Next(14)], rnd.Next(12), rnd.Next(12), rnd.Next(3), rnd.Next(4)));
        var ties = GodTiesMath.Build(world, (p, q) => (p + q) % 5 == 0 ? -1 : 0, null, 99);
        var per = world.ToDictionary(g => g.id, g => ties.Count(t => t.a == g.id || t.b == g.id));
        C(per.Values.All(v => v <= GodTiesMath.MaxTies) && ties.Count >= 30, $"a world of forty gods: {ties.Count} ties, none with more than {GodTiesMath.MaxTies}");
        C(world.All(g => ties.Count(t => t.kind == GodTieKind.Lovers && (t.a == g.id || t.b == g.id)) <= 1), "one lover a god at most");
        C(ties.All(t => t.strength >= 0.1f && t.strength <= 1f && t.a != t.b) && ties.Select(t => (Math.Min(t.a, t.b), Math.Max(t.a, t.b))).Distinct().Count() == ties.Count,
            "one tie a pair, its strength within 0.1 and 1");
        var again = GodTiesMath.Build(world, (p, q) => (p + q) % 5 == 0 ? -1 : 0, null, 99);
        C(again.Count == ties.Count && again.Zip(ties, (p, q) => p.a == q.a && p.b == q.b && p.kind == q.kind).All(x => x), "the same world, the same ties");
        C(Enum.GetValues(typeof(GodTieKind)).Cast<GodTieKind>().Count(k => GodTiesMath.Hostile(k)) == 4 && GodTiesMath.Kin(GodTieKind.Twins) && !GodTiesMath.Kin(GodTieKind.Lovers),
            "rivals, the estranged and the old wrongs set gods against each other; kin are kin");

        // ================= Favour spills over =================

        C(GodTiesMath.Spill(10f, GodTieKind.Rivals, 1f, true, GodNature.Patient) < 0f && GodTiesMath.Spill(10f, GodTieKind.Allies, 1f, true, GodNature.Patient) > 0f
          && GodTiesMath.Spill(10f, GodTieKind.Lovers, 1f, true, GodNature.Patient) > 0f && GodTiesMath.Spill(10f, GodTieKind.Siblings, 1f, true, GodNature.Patient) > 0f,
            "favour with a god a little displeases its rivals and a little pleases its allies, lovers and kin");
        C(Math.Abs(GodTiesMath.Spill(10f, GodTieKind.Rivals, 1f, true, GodNature.Patient)) <= 3f && Math.Abs(GodTiesMath.Spill(10f, GodTieKind.Allies, 1f, true, GodNature.Patient)) <= 2f,
            "a little: a quarter at most for a rival");
        C(GodTiesMath.Spill(10f, GodTieKind.Rivals, 1f, true, GodNature.Jealous) < GodTiesMath.Spill(10f, GodTieKind.Rivals, 1f, true, GodNature.Patient), "a jealous rival takes it harder");
        C(Math.Abs(GodTiesMath.Spill(-10f, GodTieKind.Rivals, 1f, true, GodNature.Patient)) < Math.Abs(GodTiesMath.Spill(10f, GodTieKind.Rivals, 1f, true, GodNature.Patient))
          && GodTiesMath.Spill(-10f, GodTieKind.Rivals, 1f, true, GodNature.Patient) > 0f, "a loss spills half as far, the other way: a rival is glad of it");
        C(GodTiesMath.Spill(10f, GodTieKind.CastDown, 1f, true, GodNature.Patient) < GodTiesMath.Spill(10f, GodTieKind.CastDown, 1f, false, GodNature.Patient),
            "honouring the one who did the old wrong galls the wronged more than the other way round");
        C(GodTiesMath.Spill(10f, GodTieKind.Rivals, 0f, true, GodNature.Patient) == 0f && GodTiesMath.Spill(float.NaN, GodTieKind.Rivals, 1f, true, GodNature.Patient) == 0f
          && GodTiesMath.Spill(10f, GodTieKind.Rivals, 0.5f, true, GodNature.Patient) > GodTiesMath.Spill(10f, GodTieKind.Rivals, 1f, true, GodNature.Patient),
            "by the tie's strength");
        bool spillBounded = true;
        foreach (GodTieKind k in Enum.GetValues(typeof(GodTieKind)))
            foreach (var nat in GodsMath.Natures)
                foreach (float a in new[] { -50f, -5f, 5f, 50f })
                    if (Math.Abs(GodTiesMath.Spill(a, k, 1f, true, nat)) > Math.Abs(a) * 0.5f) spillBounded = false;
        C(spillBounded, "never more than half the change itself");

        // ================= Ties shift slowly =================

        GodTiesMath.Shift(GodTieKind.Lovers, 0.6f, 0.1f, 0.5f, out var k1, out float st1, out bool g1);
        GodTiesMath.Shift(GodTieKind.Rivals, 0.6f, 0.05f, 0.5f, out var k2, out _, out _);
        GodTiesMath.Shift(GodTieKind.Siblings, 0.2f, 0.9f, 0.1f, out var k3, out float st3, out bool g3);
        GodTiesMath.Shift(GodTieKind.Rivals, 0.15f, 0.9f, 0.1f, out _, out _, out bool g4);
        GodTiesMath.Shift(GodTieKind.CastDown, 0.15f, 0.9f, 0.1f, out var k5, out float st5, out bool g5);
        C(k1 == GodTieKind.Estranged && st1 >= 0.4f && !g1 && k2 == GodTieKind.Allies && k3 == GodTieKind.Siblings && st3 >= 0.2f && !g3 && g4 && k5 == GodTieKind.CastDown && !g5 && st5 >= 0.15f,
            "lovers part, rivals make peace, kin stay kin, a rivalry cools to nothing, an old wrong is remembered faintly");
        C(GodTiesMath.ShiftChancePerDay <= 1f / 60f && GodTiesMath.NewTieChancePerDay <= 1f / 10f, "slowly: a tie shifts about once in two years");

        // ================= A gift and its return (Helios and Selene) =================

        C(GodTiesMath.Spill(10f, GodTieKind.Return, 1f, true, GodNature.Cold) > 0f && GodTiesMath.Spill(10f, GodTieKind.Return, 1f, false, GodNature.Patient) == 0f,
            "a gift and its return: what is meant for the giver reaches the one who returns it, and nothing goes back");
        C(GodTiesMath.Kin(GodTieKind.Return) && !GodTiesMath.Hostile(GodTieKind.Return), "the gift and the return are kin for good, never set against each other");

        // ================= The gods as defs =================

        var godDefs = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Gods.xml")).Root!.Elements("ColonyCouncil.GodDef").ToList();
        string V(XElement e, string f) => e.Element(f)?.Value ?? "";
        List<string> Li(XElement e, string f) => e.Element(f)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
        var byKey = godDefs.ToDictionary(e => V(e, "key"));
        C(godDefs.Count == 14 && GodLoreMath.Keys.Length == 14 && GodLoreMath.Keys.All(byKey.ContainsKey) && GodLoreMath.Keys.Distinct().Count() == 14,
            $"fourteen gods, one def each, keyed by their short names ({string.Join(", ", byKey.Keys)})");
        foreach (var k in new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Hush", "Undertow", "Helios", "Selene", "Mog", "Murmur", "Ducat" })
            C(byKey.ContainsKey(k) && V(byKey[k], "defName") == "Rimfolk_God_" + k && V(byKey[k], "label") == k, $"{k}: Rimfolk_God_{k}, its short name its label");
        var domainsWanted = new Dictionary<string, (string, string)>
        {
            ["Hollin"] = ("Wild", "Harvest"), ["Smoulder"] = ("Craft", "Hearth"), ["Bellwether"] = ("Hunt", "Wild"), ["Morel"] = ("Dead", "Harvest"), ["Ennu"] = ("Way", "Hearth"),
            ["Pip"] = ("Machine", "Craft"), ["Cirrus"] = ("Storm", "Night"), ["Hush"] = ("Night", "Dead"), ["Undertow"] = ("Void", ""), ["Helios"] = ("Hearth", "Harvest"),
            ["Selene"] = ("Night", "Healing"), ["Mog"] = ("Hearth", "Hunt"), ["Murmur"] = ("Waters", "Harvest"), ["Ducat"] = ("Way", "Craft")
        };
        foreach (var kv in domainsWanted)
            C(V(byKey[kv.Key], "domain") == kv.Value.Item1 && V(byKey[kv.Key], "second") == kv.Value.Item2, $"{kv.Key}: the fallback domains {kv.Value.Item1} and {kv.Value.Item2}");
        C(godDefs.All(e => V(e, "kind") == (V(e, "key") == "Undertow" ? "Hunger" : "Ordinary")), "Undertow is the Hunger, the rest ordinary");
        C(V(byKey["Helios"], "unmoved") == "true" && godDefs.Count(e => V(e, "unmoved") == "true") == 1, "Helios gives without looking: favor doesn't move him");
        string godsCode = Read("Gods/Gods.cs");
        C(godsCode.Contains("bool unmoved = g.def?.unmoved == true;") && godsCode.Contains("if (!unmoved) b.Move(g, key, a, out _, out _);") && godsCode.Contains("other.def?.unmoved == true) continue;"),
            "an unmoved god's favor goes only along its ties (to Selene)");
        var looks = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodLooks.xml")).Root!.Elements().Select(x => x.Element("defName")?.Value).ToHashSet();
        HashSet<string> activeDefs = new HashSet<string>(), activeThings = new HashSet<string>(), activeFactions = new HashSet<string>(), activeIncidents = new HashSet<string>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var rd = new StreamReader(gz))
        {
            string line;
            while ((line = rd.ReadLine()) != null)
            {
                var c = line.Split('\t');
                if (c.Length < 4 || c[3] != "yes") continue;
                activeDefs.Add(c[1]);
                if (c[0] == "ThingDef") activeThings.Add(c[1]);
                if (c[0] == "FactionDef") activeFactions.Add(c[1]);
                if (c[0] == "IncidentDef") activeIncidents.Add(c[1]);
            }
        }
        var peopleDefs = Directory.GetFiles(Path.Combine(root, "Defs", "PeopleDefs"), "Rimfolk_People_*.xml").SelectMany(f => XDocument.Load(f).Root!.Elements("FactionDef"))
            .Select(e => e.Element("defName")?.Value).Where(x => x != null && !x.StartsWith("Rimfolk_People_")).ToList();
        string First(string s)
        {
            for (int i = 12; i < s.Length; i++)
                if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                    return s.Substring(0, i + 1);
            return s;
        }
        string Body(string l) => Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
        bool Clean(string s) => !s.Contains("—") && !s.Contains("!");
        int Longest(XElement e) => new[] { V(e, "key") }.Concat(new[] { "tribalNames", "medievalNames", "industrialNames", "spacerNames" }.Select(f => Li(e, f).FirstOrDefault() ?? "")).Max(x => x.Length);
        bool Said(string line, XElement e) => First(Body(line)) == Body(line) && Body(line).Replace("{GOD}", new string('x', Longest(e))).Length <= 90 && Clean(line)
                                             && Regex.Matches(line, @"\{([A-Z_]+)\}").Cast<Match>().All(m => GodsMath.Placeholders.Contains(m.Groups[1].Value));
        foreach (var e in godDefs)
        {
            string k = V(e, "key");
            C(V(e, "epithet").Length > 3 && V(e, "godOf").Length > 10 && (V(e, "unheard").StartsWith("a ") || V(e, "unheard").StartsWith("something ")),
                $"{k}: an epithet, what it is, and what is seen of it before anyone has heard of it");
            C(new[] { "tribalNames", "medievalNames", "industrialNames", "spacerNames" }.All(f => Li(e, f).Count >= 1 && Li(e, f).All(n => n.Length <= 22)),
                $"{k}: a name among the peoples of every age");
            C(new[] { "whatItIs", "howSeen", "character", "wants" }.All(f => V(e, f).Length >= 80 && Clean(V(e, f))) && Li(e, "pleases").Count >= 1 && Li(e, "angers").Count >= 1,
                $"{k}: what it is, how it is seen, its character, what it wants, what pleases and angers it");
            C(Li(e, "sayings").Count >= 5 && Li(e, "hands").Count >= 5 && Li(e, "scoffs").Count >= 3 && Li(e, "letterLines").Count >= 2, $"{k}: sayings, believers' lines, scoffers' answers, letters");
            foreach (var l in Li(e, "sayings").Concat(Li(e, "hands")).Concat(Li(e, "scoffs")))
                C(Said(l, e), $"{k}: '{Body(l)}' is one sentence over a head, naming only the god");
            foreach (var l in Li(e, "letterLines")) C(Clean(l) && l.Length > 40, $"{k}: a neighbor's line of its hand reads as a letter's");
            var stories = e.Element("stories")!.Elements("li").ToList();
            C(stories.Count >= 2, $"{k}: at least two old stories ({stories.Count})");
            foreach (var st in stories)
            {
                var pieces = Li(st, "pieces");
                C(V(st, "key").StartsWith(k + "_") && V(st, "title").Length > 3 && pieces.Count >= 2 && V(st, "retelling").Length > 150 && Clean(V(st, "retelling")),
                    $"{V(st, "key")}: a title, two pieces or more, and a retelling for a letter");
                foreach (var p in pieces) C(First(p) == p && p.Length <= 90 && Clean(p) && !p.Contains("{"), $"{V(st, "key")}: '{p}' is one sentence a person can tell");
                foreach (var w in Li(st, "with")) C(byKey.ContainsKey(w) && w != k, $"{V(st, "key")}: of {w}, another of the gods");
            }
            foreach (var t in e.Element("ties")?.Elements("li") ?? Enumerable.Empty<XElement>())
                C(byKey.ContainsKey(V(t, "other")) && V(t, "other") != k && Enum.TryParse<GodTieKind>(V(t, "kind"), out _) && First(V(t, "words")) == V(t, "words") && V(t, "words").Length <= 90,
                    $"{k}: its tie to {V(t, "other")} ({V(t, "kind")}) has words of its own, one sentence");
            C(Li(e, "peoples").All(p => peopleDefs.Contains(p) || activeFactions.Contains(p)), $"{k}: the peoples that revere it are Rimfolk's or the player's active mods' ({string.Join(", ", Li(e, "peoples").Where(p => !peopleDefs.Contains(p) && !activeFactions.Contains(p)))})");
            C(Li(e, "kinds").All(GodLoreMath.Kinds.Contains), $"{k}: the kinds of people that revere it are known kinds");
            C(Li(e, "sacredAnimals").All(activeThings.Contains), $"{k}: its sacred animals are in the player's active mods");
            C(looks.Contains(V(e, "look")), $"{k}: its look ({V(e, "look")}) is one of the presence part's");
            C(e.Element("color") != null && e.Element("color2") != null, $"{k}: its two colors");
        }
        var revered = godDefs.SelectMany(e => Li(e, "peoples")).ToHashSet();
        C(peopleDefs.Count >= 23 && peopleDefs.All(revered.Contains), $"each of Rimfolk's peoples reveres at least one god by its lore ({string.Join(", ", peopleDefs.Where(p => !revered.Contains(p)))})");
        foreach (var kind in new[] { "Tribal", "Medieval", "Industrial", "Spacer", "Raiders", "Empire", "Cannibal" })
            C(godDefs.Any(e => Li(e, "kinds").Contains(kind)), $"the game's faction kinds: some god is revered by the {kind}");
        var tieKinds = godDefs.SelectMany(e => (e.Element("ties")?.Elements("li") ?? Enumerable.Empty<XElement>()).Select(t => (a: V(e, "key"), b: V(t, "other"), kind: V(t, "kind")))).ToList();
        C(tieKinds.Select(t => string.CompareOrdinal(t.a, t.b) < 0 ? t.a + "|" + t.b : t.b + "|" + t.a).Distinct().Count() == tieKinds.Count, "each tie authored once");
        bool Tie(string a, string b, string kind) => tieKinds.Any(t => (t.a == a && t.b == b || t.a == b && t.b == a) && t.kind == kind);
        C(Tie("Hollin", "Morel", "Allies") && Tie("Smoulder", "Hush", "Rivals") && Tie("Pip", "Morel", "Rivals") && Tie("Cirrus", "Bellwether", "Allies")
          && Tie("Undertow", "Hollin", "Rivals") && Tie("Undertow", "Ennu", "Rivals") && Tie("Helios", "Selene", "Return") && Tie("Helios", "Pip", "Rivals")
          && Tie("Mog", "Ennu", "Rivals") && Tie("Murmur", "Hush", "Rivals") && Tie("Ducat", "Smoulder", "Rivals"),
            "the ties as the gods stand with each other (the brainstorm's section B)");
        var allNames = godDefs.SelectMany(e => new[] { V(e, "key") }.Concat(new[] { "tribalNames", "medievalNames", "industrialNames", "spacerNames" }.SelectMany(f => Li(e, f)))).ToList();
        foreach (var old in new[] { "Storm-father", "Ferryman", "Herdmother", "Far Light", "Red Captain", "Voice in the Wire", "Deep Mother", "Lady of the Vote", "Silent Captain", "Lattice-mind", "Sleeper", "Broker", "Tinker" })
            C(!File.ReadAllText(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Gods.xml")).Contains(old) && !File.ReadAllText(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodLooks.xml")).Contains(old)
              && !File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Gods.xml")).Contains(old), $"the old patron {old} is retired from the gods' defs and words");
        C(!File.Exists(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Pantheon_Patrons.xml")) && !Read("Gods/GodDefs.cs").Contains("GodPatronDef"), "the old patrons are retired with the generated pantheon");

        // ================= The domains, a fallback table only =================

        var domainsXml = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Pantheon_Domains.xml")).Root!.Elements("ColonyCouncil.GodDomainDef").ToList();
        C(domainsXml.Count == 14 && domainsXml.All(e => e.Element("symbol") == null && e.Element("oldNames") == null && e.Element("epithets") == null && !string.IsNullOrEmpty(e.Element("ofWords")?.Value)),
            "fourteen domains kept as the fallback table: no symbols, no generated names");
        C(domainsXml.SelectMany(e => (e.Element("sacredAnimals")?.Elements("li") ?? Enumerable.Empty<XElement>()).Concat(e.Element("sacredPlants")?.Elements("li") ?? Enumerable.Empty<XElement>()))
            .All(x => activeThings.Contains(x.Value)), "the domains' sacred animals and plants are in the player's active mods");

        // ================= No symbols =================

        C(!Directory.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "GodSymbols")) && !File.Exists(Path.Combine(root, "Tools", "make_god_symbols.py")) && !File.Exists(Path.Combine(src, "Gods", "GodSymbols.cs"))
          && !File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Gods", "Aura", "Emblem.png")), "the gods' symbols, their script and the card's emblem are gone");
        string card = Read(Path.Combine("Gods", "Presence", "GodCard.cs"));
        C(!card.Contains("Symbol") && !card.Contains("Emblem") && card.Contains("<size=28>"), "the god's card shows the god in words: its name set large in its color");
        C(!Read(Path.Combine("Gods", "Presence", "GodLookDef.cs")).Contains("Texture2D Symbol"), "a look has no symbol");

        // ================= The hands =================

        foreach (var kv in GodHandsMath.ByDef.Concat(GodHandsMath.ByWorker))
        {
            C(byKey.ContainsKey(kv.Value.god) && (kv.Value.with == null || byKey.ContainsKey(kv.Value.with)) && kv.Value.chance > 0f && kv.Value.chance <= 0.8f,
                $"the hand in {kv.Key} is {kv.Value.god}'s{(kv.Value.with != null ? " with " + kv.Value.with : "")}, and people don't always remark on it");
        }
        foreach (var kv in GodHandsMath.ByDef)
            C(activeIncidents.Contains(kv.Key) || activeDefs.Contains(kv.Key), $"the event {kv.Key} is the game's or one of the player's active mods'");
        C(GodHandsMath.Of("AmbrosiaSprout", null).god == "Hollin" && GodHandsMath.Of("ThrumboPasses", null).god == "Bellwether" && GodHandsMath.Of("CropBlight", null).god == "Morel"
          && GodHandsMath.Of("CropBlight", null, true).god == "Hollin" && GodHandsMath.Of("WandererJoin", null).god == "Ennu" && GodHandsMath.Of("ShortCircuit", null).god == "Pip"
          && GodHandsMath.Of("MeteoriteImpact", null).god == "Cirrus" && GodHandsMath.Of("ColdSnap", null).god == "Hush" && GodHandsMath.Of("PsychicDrone", null).god == "Undertow"
          && GodHandsMath.Of("SolarFlare", null).god == "Helios" && GodHandsMath.Of("HeatWave", null).god == "Helios" && GodHandsMath.Of("Eclipse", null).god == "Selene"
          && GodHandsMath.Of("Eclipse", null).with == "Helios" && GodHandsMath.Of("Council_Evt_Stray", null).god == "Mog" && GodHandsMath.Of("SeasonalFlooding", null).god == "Murmur"
          && GodHandsMath.Of("OrbitalTraderArrival", null).god == "Ducat" && GodHandsMath.Of("Council_Evt_RichSeam", null).god == "Smoulder",
            "whose hand people see (the brainstorm's section C): blight on the axe-happy is Hollin's, the eclipse Selene stepping between Helios and the world");
        C(GodHandsMath.Of("RaidEnemy", "IncidentWorker_RaidEnemy").Valid == false && GodHandsMath.Of("Mod_Thing", "IncidentWorker_ThrumboPasses").god == "Bellwether", "a raid is nobody's hand; a mod's event with the game's worker is read by its worker");
        int day = 60000;
        C(!GodHandsMath.Remarked(0, -1, 5 * day, 1f, 0f, day) && GodHandsMath.Remarked(0, -1, GodHandsMath.QuietDays * day, 1f, 0.5f, day)
          && !GodHandsMath.Remarked(-1, 10 * day, 11 * day, 1f, 0f, day) && !GodHandsMath.Remarked(-1, -1, 0, 0.3f, 0.5f, day) && GodHandsMath.Remarked(-1, -1, 0, 0.3f, 0.2f, day),
            "a god keeps quiet a while after its hand is remarked on, no two hands close together at a home, and each event's own chance");
        C(GodHandsMath.QuietDays >= 14 && GodHandsMath.AnyQuietDays >= 3, "rare enough not to nag");
        C(GodHandsMath.Scoffer(GodRealness.Felt) && !GodHandsMath.Scoffer(GodRealness.Believed) && !GodHandsMath.Scoffer(GodRealness.Walking),
            "a scoffer answers only while the gods are felt; believed, the colony is sure; walking, nobody doubts it");
        C(GodHandsMath.Fits("[gift] x", HandKind.Gift, GodRealness.Felt) && !GodHandsMath.Fits("[gift] x", HandKind.Wrath, GodRealness.Felt) && GodHandsMath.Fits("plain", HandKind.Wrath, GodRealness.Felt)
          && !GodHandsMath.Fits("[walking] x", HandKind.Sign, GodRealness.Felt) && GodHandsMath.Fits("[walking] x", HandKind.Sign, GodRealness.Walking)
          && !GodHandsMath.Fits("[meeting] x", HandKind.Sign, GodRealness.Felt) && GodHandsMath.Fits("[meeting] x", HandKind.Sign, GodRealness.Felt, true),
            "a line fits its hand by its tags: the kind, how real the gods are, a meeting");
        C(GodHandsMath.Fitting(new[] { "[gift] a", "b", "[wrath] c" }, HandKind.Gift, GodRealness.Felt).SequenceEqual(new[] { "a" })
          && GodHandsMath.Fitting(new[] { "[gift] a", "b" }, HandKind.Sign, GodRealness.Felt).SequenceEqual(new[] { "b" }), "a line made for the moment beats one for any");
        C(GodHandsMath.NightOnly("Selene") && !GodHandsMath.NightOnly("Helios") && GodHandsMath.Night(22) && GodHandsMath.Night(3) && !GodHandsMath.Night(12), "Selene returns at night what was done by day");
        C(GodHandsMath.Writable(0, 5 * day, false, day) && !GodHandsMath.Writable(0, 1 * day, false, day) && !GodHandsMath.Writable(0, 5 * day, true, day) && !GodHandsMath.Writable(0, 60 * day, false, day),
            "a neighbor writes of a hand a few days after, once, while it is still news");
        foreach (var e in godDefs)
            C(Li(e, "hands").Any(l => !l.StartsWith("[") || l.StartsWith("[sign") || l.StartsWith("[gift") || l.StartsWith("[wrath") || l.StartsWith("[meeting")), $"{V(e, "key")}: a believer's line for its hands");
        C(Li(byKey["Selene"], "hands").Count(l => l.StartsWith("[meeting]")) >= 2, "Selene has lines for stepping between Helios and the world");
        string hands = Read("Gods/GodHands.cs");
        C(hands.Contains("[HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]") && hands.Contains("if (!__result || !RimfolkPatches.On) return;"),
            "a soft postfix on IncidentWorker.TryExecute, after the event has happened, off with the deeper hooks");
        C(!hands.Contains("Tell.") && !hands.Contains("Letter") && !hands.Contains("Messages.") && hands.Contains("Speech.Say(believer, line)") && hands.Contains("Speech.Say(scoffer, answer, believer)")
          && hands.Contains("Together.Can(believer, x)"), "people's words only: a believer's bubble, a scoffer near them, no letter or news line, the game's letter untouched");
        C(hands.Contains("GodLoreMath.Attribute(god.Key, knownOf, Rand.Value)") && hands.Contains("GodLore.Saw(witnesses, taken, label, said, \"SawHand\")"),
            "a believer names the god they take it for (maybe wrongly), and the book has it as they saw and said it");

        // ================= The lore: names, who reveres whom, stories in pieces =================

        C(GodLoreMath.Reveres(new[] { "Rimfolk_Mossborn" }, new[] { "Tribal" }, "Rimfolk_Mossborn", new[] { "Tribal" }, true)
          && !GodLoreMath.Reveres(new[] { "Rimfolk_Order" }, new[] { "Tribal" }, "Rimfolk_Mossborn", new[] { "Tribal" }, true)
          && GodLoreMath.Reveres(new string[0], new[] { "Tribal" }, "TribeCivil", new[] { "Tribal" }, false)
          && !GodLoreMath.Reveres(new string[0], new[] { "Spacer" }, "Empire", new[] { "Spacer", "Empire" }, false)
          && GodLoreMath.Reveres(new string[0], new[] { "Empire" }, "Empire", new[] { "Spacer", "Empire" }, false),
            "a people a god names reveres only those that name it; the rest by kind; the Empire by its own ways");
        C(GodLoreMath.KindsOf(0, false, false, false).SequenceEqual(new[] { "Tribal" }) && GodLoreMath.KindsOf(3, true, false, true).SequenceEqual(new[] { "Spacer", "Raiders", "Cannibal" }),
            "a people's kinds: its age, and raiders, the Empire, cannibals");
        var ageNames = new[] { "Grandmother Root" };
        int shortSaid = Enumerable.Range(0, 400).Count(i => GodLoreMath.NameFor("Hollin", ageNames, i, 1) == "Hollin");
        C(shortSaid > 120 && shortSaid < 280 && GodLoreMath.NameFor("Hollin", ageNames, 7, 1) == GodLoreMath.NameFor("Hollin", ageNames, 7, 1) && GodLoreMath.NameFor("Hollin", null, 7, 1) == "Hollin",
            $"about half say a god's short name and half their age's, each always the same ({shortSaid} of 400)");
        var sketches = new List<TaleSketch> { new TaleSketch("A", 3, "Hollin"), new TaleSketch("B", 2, "Pip"), new TaleSketch("C", 2, "Hollin", "Morel") };
        var told = new Dictionary<string, int> { ["A"] = 1 };
        C(GodLoreMath.Next(sketches, told, new[] { "Hollin" }, 0.9f, out var key1, out int p1) && key1 == "A" && p1 == 1, "a story begun is carried on first, its next piece");
        told["A"] = 3;
        C(GodLoreMath.Next(sketches, told, new[] { "Hollin" }, 0.1f, out var key2, out int p2) && key2 == "C" && p2 == 0, "then a new one of the teller's own gods, from its first piece");
        C(GodLoreMath.Next(sketches, told, new[] { "Undertow" }, 0.1f, out var key3, out _) && (key3 == "B" || key3 == "C"), "a teller with none of their own tells any not yet heard");
        var all = new Dictionary<string, int> { ["A"] = 3, ["B"] = 2, ["C"] = 2 };
        C(GodLoreMath.Next(sketches, all, null, 0.5f, out _, out int p4) && p4 == 0, "all heard: an old one told again from its start");
        C(GodLoreMath.AfterTelling(1, 1, 3) == 2 && GodLoreMath.AfterTelling(3, 0, 3) == 3 && GodLoreMath.AfterTelling(1, 0, 3) == 1, "the colony's count moves on only with a piece it hasn't heard");
        int[] inOrder = new int[4];
        var t2 = new Dictionary<string, int>();
        for (int i = 0; i < 3; i++)
        {
            GodLoreMath.Next(new[] { new TaleSketch("S", 3, "Hush") }, t2, null, 0.5f, out _, out int pc);
            inOrder[i] = pc;
            t2["S"] = GodLoreMath.AfterTelling(t2.TryGetValue("S", out int n0) ? n0 : 0, pc, 3);
        }
        C(inOrder[0] == 0 && inOrder[1] == 1 && inOrder[2] == 2, "a story reaches the colony in pieces, in order, never all at once");
        C(GodLoreMath.TalkNow(19, -1, 1f, 0f) && !GodLoreMath.TalkNow(12, -1, 1f, 0f) && !GodLoreMath.TalkNow(19, 3, 1f, 0f) && GodLoreMath.EveningChance <= 0.1f && GodLoreMath.GapHours >= 12,
            "talk of the gods at the evening fire, now and then, never twice in an evening");
        C(Enum.GetValues(typeof(GodTalkKind)).Length == 3 && GodLoreMath.Talk(0.1f) == GodTalkKind.Piece && GodLoreMath.Talk(0.7f) == GodTalkKind.Saying && GodLoreMath.Talk(0.95f) == GodTalkKind.Tie,
            "mostly the next piece of a story, else a saying, now and then a word on two gods");
        C(Enumerable.Range(0, 300).All(i => Math.Abs(GodLoreMath.Credence(1f, 0.5f, false, false, 0, i)) <= 1f) && GodLoreMath.Credence(1.6f, 0.9f, true, false, 4, 3) > GodLoreMath.Credence(1f, 0.5f, false, true, 0, 3)
          && GodLoreMath.Credence(1f, -1f, false, false, 0, 9) == GodLoreMath.Credence(1f, -1f, false, false, 0, 9),
            "who believes: the psychically sensitive, the sure of their faith, the old and those who have seen for themselves, more than the too clever; each the same");
        var knownOf = new HashSet<string> { "Hollin", "Smoulder" };
        C(GodLoreMath.Attribute("Hollin", knownOf, 0.9f) == "Hollin" && GodLoreMath.Attribute("Pip", knownOf, 0.5f) == "Smoulder" && GodLoreMath.Attribute("Ennu", knownOf, 0.5f) == null
          && GodLoreMath.Attribute("Morel", knownOf, 0.1f) == "Hollin",
            "a believer names the god they know of: the right one mostly, one they confuse it with, or nothing they can name");
        C(GodLoreMath.Keys.All(GodLoreMath.Confused.ContainsKey) && GodLoreMath.Confused.Values.All(v => v.All(GodLoreMath.Keys.Contains)), "every god can be taken for another");
        C(GodLoreMath.ConnectChance(0, 0) > 0f && GodLoreMath.ConnectChance(20, 10) <= 0.12f && GodLoreMath.ConnectChance(15, 5) > GodLoreMath.ConnectChance(2, 5),
            "two names worked out to be one, sooner with a sharp mind on it");
        C(GodLoreMath.VoiceOf(false, true, true, 20) == GodLoreMath.Voice.Bare && GodLoreMath.VoiceOf(true, true, false, 2) == GodLoreMath.Voice.Devout
          && GodLoreMath.VoiceOf(true, false, false, 12) == GodLoreMath.Voice.Learned && GodLoreMath.VoiceOf(true, false, false, 4) == GodLoreMath.Voice.Plain,
            "the book's voice is its keeper's: devout, learned or plain; bare with nobody");

        // ================= The colony's book =================

        string lore = Read("Gods/GodLore.cs"), page = Read("Gods/GodLorePage.cs"), tales = Read("Gods/GodTales.cs");
        foreach (var api in new[] { "public static LoreEntry Saw(IEnumerable<Pawn> pawns, God g, string words", "public static LoreEntry Heard(Pawn teller, God g, string words",
                     "public static LoreEntry Heard(string teller, God g, string words", "public static LoreEntry Believes(Pawn p, God g, string words",
                     "public static LoreEntry Dreamt(Pawn p, God g, string words)", "public static LoreEntry Prayed(Pawn p, God g, string words)", "public static string LabelFor(string key)" })
            C(lore.Contains(api), "GodLore: " + api.Replace("public static ", ""));
        C(lore.Contains("Interplay.Officer(Interplay.Chaplain") && lore.Contains("MoralGuide") && lore.Contains("OrderByDescending(Credence)") && lore.Contains("OrderByDescending(Intellect)"),
            "the keeper: the chaplain, else the moral guide, else the most devout, else the most lettered");
        foreach (var coll in new[] { "entries", "names", "hands", "retold" })
            C(lore.Contains("Scribe_Collections.Look(ref " + coll + ", \"" + coll + "\"") && lore.Contains(coll + " ??= new List<"), $"the book saves its {coll} and mends them on a load");
        C(lore.Contains("public void Connect(LoreName a, LoreName into, Pawn by, string teller)") && lore.Contains("GodLoreMath.TellerConnects"),
            "two names worked out to be one, by a colonist or a teller saying so");
        C(!page.Contains("Favour") && !page.Contains("FavourValue") && !page.Contains("ColorA") && !page.Contains(".colour"), "the book shows no favor, and no god's color that would give away which names are one");
        C(page.Contains("<size=26>") && page.Contains("\"<i>\"") && page.Contains("Rule(") && page.Contains("public const float Column = 600f;") && page.Contains("CC_GodLore_Hand"),
            "type with character: a title set large, italics, rules, a readable column, the keeper's hand marked");
        C(Read("UI/MainTabWindow_World.cs").Contains("Own(RegionView.Gods, Icons.Journal ?? Icons.Chronicle, 40f, GodLorePage.Draw)"), "the book is its own tab of the region window");
        C(tales.Contains("Speech.Say(teller, text, listener)") && tales.Contains("GodLore.Heard(teller, g, text, how, key, piece, name)") && tales.Contains("Together.Can(teller, p)") && tales.Contains("Together.Can(v, c)"),
            "stories told by people near each other, and written as heard");
        C(Read("Letters/LetterForm.cs").Contains("GodTales.Written(s.values, text?.full ?? text?.letter);") && tales.Contains("if (!text.Contains(kv.Key)) continue;"),
            "a letter's tale reaches the book only once its words are in the letter");
        C(validator.Contains("wishPh.UnionWith(new[] { \"GODTALE\", \"GODHAND\" });"), "the def validator knows {GODTALE} and {GODHAND}");
        string ps = File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Letters_PS.xml"));
        C(ps.Contains("{GODTALE}") && ps.Contains("{GODHAND}"), "neighbors' postscripts retell a story, and speak of a god's hand at the colony");

        // ================= Unseen: no page, no favor told =================

        C(!File.Exists(Path.Combine(src, "Gods", "GodsPage.cs")) && !Read("UI/Region/PeoplesView.cs").Contains("Gods") && !Read("UI/Region/RegionView.cs").Contains("bool gods"),
            "no Gods page under Peoples");
        C(!godsCode.Contains("Tell.Minor") && !godsCode.Contains("CC_God_Stands_") && !Read("Gods/Pantheon.cs").Contains("Tell.") && !Read("Gods/GodTales.cs").Contains("Tell."),
            "no news line says what a god thinks: favor, a god known, a tie, a fading god are hidden state");
        C(godsCode.Contains("public static God ByKey(string key)") && godsCode.Contains("public static string NameBy(God g, Pawn speaker)") && godsCode.Contains("public static void Moment(PresenceSpec spec)"),
            "Gods.ByKey, a god as a speaker names it, and a god's moment as its witnesses saw it");
        C(godsCode.Contains("act.witnesses.NullOrEmpty() ? Witnesses(map, act.cell)") && godsCode.Contains("public const float WitnessRange = 20f;") && godsCode.Contains("GenSight.LineOfSight(p.Position, cell, map, true)"),
            "an act remembered without witnesses gets them: those awake in sight within 20 cells, or everyone for a sign with no place");
        C(Read("Gods/GodPresenceSeam.cs").Contains("if (played) Gods.Moment(spec);"), "a god's moment reaches the book as those who saw it saw it");
        C(godsCode.Contains("public static string CardName(God god, Map home, string sign = null)") && godsCode.Contains("lore.KnowsActing(god) && !lore.Knows(god, sign)")
          && godsCode.Contains("if (plain) lore.MarkActing(spec.god);"),
            "Gods.CardName: a god named plainly only when the colony knows it was its doing (seen acting, revealed, a sign learned, or walking openly)");
        C(Read("Gods/GodsWiring.cs").Contains("string known = Gods.CardName(s.god, s.map);") && Read("Gods/GodHands.cs").Contains("bool known = Gods.CardName(god, map, sign) != null;")
          && Read("Gods/GodHands.cs").Contains("if (GodHandsMath.Scoffer(realness) && !known)"), "the card and the hands name a god only when the colony knows; then nobody doubts it");
        C(lore.Contains("public static void LearnSign(God g, string sign)") && Read("Gods/GodTales.cs").Contains("foreach (var sign in story.teaches ?? new List<string>())"),
            "a sign learned as a god's own: from a story heard whole, or any part's call");
        foreach (var e in godDefs)
            foreach (var st in e.Element("stories")!.Elements("li"))
                foreach (var sign in Li(st, "teaches"))
                    C(GodHandsMath.ByDef.TryGetValue(sign, out var h) && (h.god == V(e, "key") || h.with == V(e, "key") || V(e, "key") == "Hollin" && sign == "CropBlight"),
                        $"{V(st, "key")}: the sign it teaches ({sign}) is its god's hand");
        C(godDefs.SelectMany(e => e.Element("stories")!.Elements("li")).Count(st => Li(st, "teaches").Count > 0) >= 14, "stories teach the colony what the gods' signs are");
        C(Read("Gods/GodsWiring.cs").Contains("HediffComp_GodAvatar.Label = GodLore.LabelFor;") && Read(Path.Combine("Gods", "Presence", "GodAvatar.cs")).Contains("return Label?.Invoke(n) ?? n;"),
            "the name over a god on the map is the colony's name for it, or what is seen of it");

        // ================= The hooks, the book, the settings =================

        C(Read("World/Region/RegionBook.cs").Contains("if (Gods.On) Safe(\"the gods\", () => GodBook.Daily(now));") && Read("World/Region/RegionBook.cs").Contains("GodBook.Hourly(now)"),
            "kept in the region's day and hour, no pass of its own");
        C(Read("Happenings/Happenings.cs").Contains("GodDeeds.Killed(victim, dinfo, note.beast, note.map);") && Read("Secrets/Vendettas.cs").Contains("GodDeeds.Funeral(dead, speaker);"),
            "a great beast brought down, a sacred animal slaughtered, a funeral's eulogy");
        string book = Read("Gods/GodBook.cs");
        foreach (var coll in new[] { "gods", "ties", "standings", "acts", "vows", "deeds" })
            C(book.Contains("Scribe_Collections.Look(ref " + coll + ", \"" + coll + "\", LookMode.Deep);") && book.Contains(coll + " ??= new List<"), $"the hidden state saves its {coll} and mends them on a load");
        C(book.Contains("if (!built || version < Pantheon.Nine) Pantheon.Build(this);") && book.Contains("Scribe_Values.Look(ref version, \"version\");"), "the gods from their defs; an older save's generated gods let go on its first load");
        string pantheon = Read("Gods/Pantheon.cs");
        C(pantheon.Contains("b.gods.Clear();") && pantheon.Contains("b.standings.Clear();") && pantheon.Contains("b.acts.Clear();") && pantheon.Contains("if (older) b.retired = true;")
          && !pantheon.Contains("IdeoFoundation_Deity"), "the generated gods dropped with their standings and acts, the line told once; Ideology's deities ignored");
        C(Read("Gods/God.cs").Contains("Scribe_Defs.Look(ref def, \"def\");") && Read("Gods/God.cs").Contains("public void FromDef()"), "a god keeps its def, and takes its words from it on every load");
        string settings = Read("CouncilSettings.cs"), catalog = Read("UI/SettingsCatalog.cs");
        C(settings.Contains("public bool gods = true;") && settings.Contains("public GodRealness godRealness = GodRealness.Felt;"), "the settings: Gods on, How real Felt, by default");
        C(catalog.Contains("Toggle(\"gods\", \"CC_Settings_Gods\"") && catalog.Contains("Choice(\"godRealness\""), "both on the What's on page");
        C(Read("Gods/GodsDebug.cs").Contains("Gods: an event's hand here...") && Read("Gods/GodsDebug.cs").Contains("Gods: hear every story (testing)")
          && Read("Gods/GodsDebug.cs").Contains("Gods: open the book of the gods") && Read("Gods/GodsDebug.cs").Contains("Gods: set how real the gods are..."),
            "dev actions: an event's hand, every story heard, the book opened, how real the gods are");
        string estimate = File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py"));
        C(estimate.Contains("Gods/GodBook.cs:gods") && estimate.Contains("Gods/GodLore.cs:entries"), "the hidden state and the colony's book modelled in the save estimate");

        // ================= The words =================

        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Gods.xml"));
        var missing = new List<string>();
        foreach (var v in Enum.GetNames(typeof(GodLoreMath.Voice)))
            foreach (var what in new[] { "Saw", "SawHand", "Said", "Scoffed", "Heard", "HeardStory", "Believes", "Doubts", "Connected", "ConnectedTold", "Dreamt", "Prayed" })
                if (!keyed.Contains("<CC_GodLore_" + what + "_" + v + ">")) missing.Add(what + "_" + v);
        foreach (var how in new[] { "Fire", "Gate", "Letter", "Supper", "Sermon" })
            if (!keyed.Contains("<CC_GodLore_How_" + how + ">")) missing.Add("How_" + how);
        foreach (var k in new[] { "Appear", "Act", "Reveal", "Depart", "Blessing", "Wrath", "Omen" })
            if (!keyed.Contains("<CC_GodLore_Moment_" + k + ">")) missing.Add("Moment_" + k);
        for (int i = 0; i < 6; i++)
            if (!keyed.Contains("<CC_GodHand_Unnamed_" + i + ">")) missing.Add("Unnamed_" + i);
        for (int i = 0; i < 4; i++)
            if (!keyed.Contains("<CC_GodHand_UnnamedScoff_" + i + ">")) missing.Add("UnnamedScoff_" + i);
        foreach (GodTieKind k in Enum.GetValues(typeof(GodTieKind)))
            if (!keyed.Contains("<CC_God_Tie_" + k + ">")) missing.Add("Tie_" + k);
        C(missing.Count == 0, $"every word the book, the hands and the moments say ({string.Join(", ", missing)})");
        C(Read("Gods/GodHands.cs").Contains("public const int UnnamedLines = 6, UnnamedScoffs = 4;"), "six lines for something nobody can name, four answers to them");
        var keyText = Regex.Matches(keyed, @"<(CC_[A-Za-z_]+)>([^<]*)</").Cast<Match>().Select(m => m.Groups[2].Value).ToList();
        C(keyText.All(t => !t.Contains("—") && !t.Contains("!")), "no dashes or exclamation marks in the gods' words");
        foreach (Match m in Regex.Matches(keyed, @"<CC_GodHand_Unnamed(Scoff)?_\d>([^<]*)</"))
            C(First(m.Groups[2].Value) == m.Groups[2].Value && m.Groups[2].Value.Length <= 90, $"'{m.Groups[2].Value}' is one sentence over a head");
        C(!keyed.Contains("favour") && !Regex.IsMatch(keyed, @"\bcolour"), "American spelling in the gods' words");

        // ================= The speech pool =================

        var pool = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Gods_Speech.xml")).Root!.Elements("ColonyCouncil.FlavorPoolDef")
            .First(e => e.Element("defName")!.Value == "Rimfolk_Pool_SayGod").Descendants("li").Select(x => x.Value).ToList();
        C(pool.Count >= 25, $"swearing by a god: {pool.Count} lines");
        foreach (var l in pool)
        {
            string body = Body(l);
            C(First(body) == body && body.Length <= 90 && !body.StartsWith("\""), $"'{body}' is one sentence, said over a head");
            C(Regex.Matches(body, @"\{([A-Z_]+)\}").Cast<Match>().Select(m => m.Groups[1].Value).All(h => GodsMath.Placeholders.Contains(h)), $"'{body}' names only the god");
        }
        foreach (var tag in new[] { "storm", "cold", "heat", "snow", "hurt", "grieving", "kind:hunger", "god:hush", "god:cirrus" })
            C(pool.Any(l => Regex.IsMatch(l, @"^\[[^\]]*\b" + Regex.Escape(tag) + @"\b")), $"lines for {tag}");
        string speech = Read("Gods/GodSpeech.cs");
        C(speech.Contains("tags.Add(\"god:\" + g.Key.ToLowerInvariant());") && speech.Contains("GodLore.NameOf(g, p).Named(GodsMath.God)"), "a god by its own tag, and by the colony's name for it");
        C(Read("Immersion/StateSpeech.cs").Contains("Try(() => God(p), \"god\");") && Read("Immersion/WeatherTalk.cs").Split("GodSpeech.InPlaceOf(pool, p) ?? Flavor.FromPool(pool, p, null, null, tags)").Length == 3,
            "a word to their god among the things on a colonist's mind, and when the weather turns");
        C(speech.Contains("Rand.Chance(StateChance)") && speech.Contains("Rand.Chance(WeatherChance)") && GodSpeechChance(speech), "now and then, on top of each moment's own rate");

        // ================= The seams =================

        string seam = Read("Gods/GodPresenceSeam.cs");
        C(seam.Contains("public static Func<PresenceSpec, bool> Manifest;") && seam.Contains("public static Action<Pawn, God, bool> MakeAvatar;"), "the presence part's two seams");
        C(new[] { "public God god;", "public Map map;", "public IntVec3 cell", "public PresenceKind kind;", "public Pawn avatar;", "public string title, text;", "public bool veiled;" }.All(seam.Contains),
            "the plain PresenceSpec");
        C(seam.Contains("veiled = !GodsMath.PlainCard(Gods.Realness)"), "veiled by how real the gods are");
        foreach (var api in new[]
                 {
                     "public static IReadOnlyList<God> All", "public static List<God> Of(Faction f)", "public static List<God> OfIdeo(Ideo ideo)", "public static List<God> ColonyGods(Map home)",
                     "public static God Patron(Map home)", "public static FavourWord Favour(God g, Map home)", "public static void AddFavour(God g, Map home, float amount, string why",
                     "public static GodRealness Realness", "public static void Remember(GodAct act)", "public static string Name(God g)", "public static string Epithet(God g)",
                     "public static God SwearBy(Pawn p", "public static List<GodTie> TiesOf(God g)", "public static List<God> Rivals(God g)", "public static List<God> Allies(God g)",
                     "public static GodNature NatureOf(God g)", "public static GodKind KindOf(God g)", "public static float Loudness(God g)", "public static void Deed(GodDomain d, Map home",
                     "public static God ById(int id)", "public static bool Known(God g)", "public static void Learn(God g, string how, bool tell = true)", "public static string GodOf(God g)",
                     "public static string FullName(God g)", "public static NamedArgument[] Args(God g)", "public static GodVow Vow(God g, Pawn p, string what, int days)"
                 })
            C(godsCode.Contains(api), "Gods keeps: " + api.Replace("public static ", ""));
        string godCs = Read("Gods/God.cs");
        C(new[] { "public int id;", "public GodDef def;", "public string name;", "public string epithet;", "public GodDomain domain, second;", "public GodForm form;", "public List<Faction> factions",
                "public List<Ideo> ideos", "public string deityKey, deityType;", "public string patron;", "public Color colour = Color.white, colour2", "public string look;",
                "public List<string> witnesses;", "public string teller;" }.All(godCs.Contains),
            "a god's record keeps every field the parts read, with its def; an act its witnesses and teller");
        C(!Regex.IsMatch(string.Join("\n", Directory.GetFiles(Path.Combine(src, "Gods"), "*.cs").Select(File.ReadAllText)), @"\bclass GodLookDef\b"), "the core never defines GodLookDef");
        string wiring = Read(Path.Combine("Gods", "GodsWiring.cs"));
        C(wiring.Contains("GodPresence.Manifest(LookOf(s.god), known ?? GodLore.LabelFor(Gods.Name(s.god)), null,") && wiring.Contains("s.god.colour, s.god.colour2);")
          && wiring.Contains("bool veiled = s.veiled && known == null && s.kind != PresenceKind.Reveal;"),
            "the moment seam: the god's own look and colors, named only as the colony knows it, no epithet on its card");
        C(wiring.Contains("GodLookSeam.MakeAvatar = (p, god, disguised) => GodAvatar.Make(p, LookOf(god), Gods.Name(god), disguised, god.colour, god.colour2);"), "the avatar seam");
        C(wiring.Contains("GodLooks.Named(god.look) ?? GodLooks.For(god.domain.ToString(), AgeOf(god.age))"), "the god's own look, else its domain's in its people's age");

        return n;
    }

    /// <summary>Both chances are below one: the gods never take every moment.</summary>
    private static bool GodSpeechChance(string speech)
    {
        var m1 = Regex.Match(speech, @"StateChance = ([\d.]+)f");
        var m2 = Regex.Match(speech, @"WeatherChance = ([\d.]+)f");
        return m1.Success && m2.Success && float.Parse(m1.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) <= 0.5f
               && float.Parse(m2.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) <= 0.5f;
    }
}
