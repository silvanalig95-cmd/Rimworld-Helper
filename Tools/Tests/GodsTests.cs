using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The gods, the core (docs/design/gods.md, "Core: the pantheon" and "Drama between the gods, and the ones who don't fit"):
/// the pure rules (a god's domain from a deity's words, its faith's memes and precepts; Rimfolk's makers' gods by age, ways
/// and land; the age's forms; natures and kinds; favour in words, its drift and how a nature weighs it; how real the gods
/// are; the ties, how favour spills along them and how they shift; newborns, fading and dead gods; the first favour
/// sources), the defs (fourteen domains, the patrons of every one of Rimfolk's peoples, the sacred things in the player's
/// active mods), the words, the speech pool, and the seams the presence part and stage 2 call.
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

        // ================= The defs: fourteen domains =================

        var domainsXml = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Pantheon_Domains.xml")).Root!.Elements("ColonyCouncil.GodDomainDef").ToList();
        var byDomain = domainsXml.ToDictionary(e => e.Element("domain")!.Value, e => e);
        C(domainsXml.Count == 14 && GodsMath.Domains.All(d => byDomain.ContainsKey(d.ToString())), "a def for each of the fourteen domains");
        var activeThings = new HashSet<string>();
        using (var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress))
        using (var reader = new StreamReader(gz))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var f = line.Split('\t');
                if (f.Length >= 4 && f[0] == "ThingDef" && f[3] == "yes") activeThings.Add(f[1]);
            }
        }
        var allNames = new List<string>();
        foreach (var d in GodsMath.Domains)
        {
            var e = byDomain[d.ToString()];
            List<string> L(string tag) => e.Element(tag)?.Elements("li").Select(x => x.Value).ToList() ?? new List<string>();
            string sym = e.Element("symbol")?.Value ?? "";
            C(sym == "ColonyCouncil/GodSymbols/" + d && File.Exists(Path.Combine(root, "Textures", sym + ".png")), $"{d}: its symbol, drawn");
            C(L("spiritNames").Count >= 3 && L("oldNames").Count >= 3 && L("newNames").Count >= 3 && L("archotechNames").Count >= 3 && L("machineNames").Count >= 2,
                $"{d}: names for every age");
            C(L("epithets").Count >= 5 && L("epithets").All(x => x.StartsWith("the ")), $"{d}: five epithets, each a noun phrase after the name");
            C(!string.IsNullOrEmpty(e.Element("ofWords")?.Value) && !string.IsNullOrEmpty(e.Element("sacredPlace")?.Value) && !string.IsNullOrEmpty(e.Element("sacredDeed")?.Value)
              && !string.IsNullOrEmpty(e.Element("newbornName")?.Value), $"{d}: what it is of, its sacred place and deed, a newborn's name");
            C(L("sacredAnimals").Concat(L("sacredPlants")).All(activeThings.Contains), $"{d}: its sacred animals and plants are in the player's active mods ({string.Join(", ", L("sacredAnimals").Concat(L("sacredPlants")).Where(x => !activeThings.Contains(x)))})");
            C(e.Element("colour") != null && e.Element("colour2") != null, $"{d}: two colours");
            allNames.AddRange(L("spiritNames").Concat(L("oldNames")).Concat(L("saintNames")).Concat(L("newNames")).Concat(L("machineNames")).Concat(L("archotechNames")));
        }
        C(new[] { "Harvest", "Way", "Healing", "Dead" }.All(d => byDomain[d].Element("sacredAnimals") != null) && byDomain["Hunt"].Element("sacredAnimals")!.Elements("li").All(x => x.Value != "Deer"),
            "sacred animals that aren't everyday livestock or the hunter's daily deer");
        C(File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "GodSymbols", "Pantheon.png")) && File.Exists(Path.Combine(root, "Tools", "make_god_symbols.py")),
            "the pantheon's own symbol, and the script that draws them all");

        // ================= The patrons of Rimfolk's peoples =================

        var patrons = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_Pantheon_Patrons.xml")).Root!.Elements("ColonyCouncil.GodPatronDef").ToList();
        var peopleDefs = Directory.GetFiles(Path.Combine(root, "Defs", "PeopleDefs"), "Rimfolk_People_*.xml")
            .SelectMany(f => XDocument.Load(f).Root!.Elements("FactionDef")).Where(e => e.Descendants("calling").Any()).Select(e => e.Element("defName")!.Value).ToList();
        var patronOf = new Dictionary<string, XElement>();
        foreach (var p in patrons)
            foreach (var li in p.Element("peoples")!.Elements("li"))
                patronOf[li.Value] = p;
        C(peopleDefs.Count >= 23 && peopleDefs.All(patronOf.ContainsKey), $"every one of Rimfolk's peoples has a patron ({string.Join(", ", peopleDefs.Where(x => !patronOf.ContainsKey(x)))})");
        C(patrons.SelectMany(p => p.Element("peoples")!.Elements("li")).Count() == patrons.SelectMany(p => p.Element("peoples")!.Elements("li")).Select(x => x.Value).Distinct().Count(),
            "one patron a people");
        string P(string people, string tag) => patronOf.TryGetValue(people, out var e) ? e.Element(tag)?.Value : null;
        C(P("Rimfolk_Herders", "godName") == "the Herdmother" && P("Rimfolk_Tamers", "godName") == "the Herdmother", "the herders and the tamers share the Herdmother");
        C(P("Rimfolk_Enclave", "kind") == "Archotech" && P("Rimfolk_HollowFleet", "kind") == "Archotech" && P("Rimfolk_HelixWorks", "kind") == "Archotech",
            "the spacer peoples' archotechs: the Broker, the Silent Captain, the Weaver");
        C(P("Rimfolk_Choir", "kind") == "Hunger" && P("Rimfolk_Choir", "domain") == "Void" && P("Rimfolk_SunkenVault", "kind") == "Imprisoned",
            "the Hunger with the Choir of the Spire, the Sleeper imprisoned in the Sunken Vault");
        C(P("Rimfolk_Republic", "form") == "HalfForgotten" && P("Rimfolk_Order", "form") == "Saint", "the Republic half-forgets its Lady; the order's Healer is a saint");
        C(P("Rimfolk_Stormglass", "godName") == "the Storm-father" && P("Rimfolk_QuietBell", "domain") == "Dead" && P("Rimfolk_Mossborn", "domain") == "Wild"
          && P("Rimfolk_AshenBanner", "domain") == "War" && P("Rimfolk_KilnGuild", "domain") == "Craft" && P("Rimfolk_RiverFolk", "domain") == "Waters",
            "the storm, the dead, the wild, war, the forge and the waters by each people's lore");
        C(patrons.All(p => (p.Element("epithet")?.Value ?? "").StartsWith("the ") && (p.Element("lore")?.Value ?? "").Length > 60
                           && Enum.TryParse<GodDomain>(p.Element("domain")!.Value, out var pd) && pd != GodDomain.None
                           && (p.Element("second") == null || p.Element("second")!.Value != p.Element("domain")!.Value)),
            "each patron's epithet, a line of lore and its domains");
        C(patrons.All(p => !allNames.Contains(p.Element("godName")!.Value)), "Rimfolk's makers never give a patron's name to another god");
        C(allNames.Count == allNames.Distinct().Count(), $"no name twice among the domains' ({string.Join(", ", allNames.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key))})");

        // ================= The words =================

        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Gods.xml"));
        bool Has(string key) => keyed.Contains("<" + key + ">");
        var missing = new List<string>();
        void Need(string key)
        {
            if (!Has(key)) missing.Add(key);
        }
        foreach (FavourWord w in Enum.GetValues(typeof(FavourWord)))
        {
            Need("CC_God_Word_" + w);
            Need("CC_God_Stands_" + w);
        }
        foreach (GodTieKind k in Enum.GetValues(typeof(GodTieKind)))
        {
            Need("CC_God_Tie_" + k);
            Need("CC_God_TieAct_" + k);
            Need("CC_God_TieAct_New" + k);
            if (k == GodTieKind.Parent || k == GodTieKind.CastDown || k == GodTieKind.StolePeople)
            {
                Need("CC_God_TieOf_" + k + "_First");
                Need("CC_God_TieOf_" + k + "_Second");
            }
            else Need("CC_God_TieOf_" + k);
        }
        Need("CC_God_TieAct_Gone");
        foreach (var nat in GodsMath.Natures)
        {
            Need("CC_God_Nature_" + nat);
            Need("CC_God_NatureLine_" + nat);
        }
        foreach (var k in GodsMath.Kinds)
        {
            Need("CC_God_Kind_" + k);
            Need("CC_God_KindLine_" + k);
        }
        foreach (GodForm f in Enum.GetValues(typeof(GodForm)))
        {
            Need("CC_God_Form_" + f);
            for (int gender = 0; gender < 3; gender++) Need("CC_God_Noun_" + GodsMath.Noun(f, gender));
        }
        foreach (var r in new[] { GodRealness.Believed, GodRealness.Felt, GodRealness.Walking })
        {
            Need("CC_God_Voice_" + GodsMath.Voice(r));
            Need("CC_GodRealness_" + r);
            Need("CC_GodRealness_" + r + "_Tip");
            Need("CC_SetNote_GodRealness_" + r);
        }
        foreach (var how in new[] { "faith", "neighbours", "letter", "visitors" }) Need("CC_God_KnownFrom_" + how);
        for (int i = 0; i < 5; i++)
        {
            Need("CC_God_Epithet_Dead_" + i);
            Need("CC_God_Epithet_Fading_" + i);
        }
        foreach (var what in new[] { "Fading", "Dead", "Returned" })
        {
            Need("CC_God_KindAct_" + what);
            Need("CC_God_KindNews_" + what);
        }
        foreach (var k in new[] { "CC_God_OldLore_Dead", "CC_God_OldLore_Fading", "CC_SetSec_Gods", "CC_Settings_Gods", "CC_Settings_Gods_Tip", "CC_SetNote_Gods", "CC_Settings_GodRealness",
                     "CC_Settings_GodRealness_Tip", "CC_Mode_Gods", "CC_Mode_Gods_Tip" })
            Need(k);
        C(missing.Count == 0, $"every word the gods say by their kind, nature, tie, form and favour ({string.Join(", ", missing)})");
        C(Read("Gods/Pantheon.cs").Contains("public const int OldEpithets = 5;"), "five old epithets of each kind");
        var keyText = Regex.Matches(keyed, @"<(CC_[A-Za-z_]+)>([^<]*)</").Select(m => m.Groups[2].Value).ToList();
        C(keyText.All(t => !t.Contains("—") && !t.Contains("!")), "no dashes or exclamation marks in the gods' words");

        // ================= The speech pool =================

        var pool = XDocument.Load(Path.Combine(root, "Defs", "FlavorPoolDefs", "Rimfolk_Gods_Speech.xml")).Root!.Elements("ColonyCouncil.FlavorPoolDef")
            .First(e => e.Element("defName")!.Value == "Rimfolk_Pool_SayGod").Descendants("li").Select(x => x.Value).ToList();
        string Body(string l) => Regex.Replace(l, @"^\[[^\]]*\]\s*", "");
        string First(string s)
        {
            for (int i = 12; i < s.Length; i++)
                if (s[i] == '.' || s[i] == '!' || s[i] == '?')
                    return s.Substring(0, i + 1);
            return s;
        }
        C(pool.Count >= 25, $"swearing by a god: {pool.Count} lines");
        foreach (var l in pool)
        {
            string body = Body(l);
            C(First(body) == body && body.Length <= 90 && !body.StartsWith("\""), $"'{body}' is one sentence, said over a head");
            C(Regex.Matches(body, @"\{([A-Z_]+)\}").Select(m => m.Groups[1].Value).All(h => GodsMath.Placeholders.Contains(h)), $"'{body}' names only the god");
        }
        foreach (var tag in new[] { "storm", "cold", "heat", "snow", "hurt", "grieving", "kind:archotech", "kind:trickster", "kind:hunger" })
            C(pool.Any(l => Regex.IsMatch(l, @"^\[[^\]]*\b" + Regex.Escape(tag) + @"\b")), $"lines for {tag}");
        C(pool.Count(l => !l.StartsWith("[")) >= 4, "lines for any moment");
        string speech = Read("Gods/GodSpeech.cs");
        foreach (Match m in Regex.Matches(speech, "\\[\"(Council_Pool_Say[A-Za-z]+)\"\\]"))
            C(Directory.GetFiles(Path.Combine(root, "Defs"), "*.xml", SearchOption.AllDirectories).Any(f => File.ReadAllText(f).Contains("<defName>" + m.Groups[1].Value + "</defName>")),
                $"the weather's pool {m.Groups[1].Value} is there");
        C(Read("Immersion/StateSpeech.cs").Contains("Try(() => God(p), \"god\");") && Read("Immersion/StateSpeech.cs").Contains("GodSpeech.StateTags(p, out var args)"),
            "a word to their god among the things on a colonist's mind");
        C(Read("Immersion/WeatherTalk.cs").Split("GodSpeech.InPlaceOf(pool, p) ?? Flavor.FromPool(pool, p, null, null, tags)").Length == 3, "and when the weather turns");
        C(speech.Contains("Rand.Chance(StateChance)") && speech.Contains("Rand.Chance(WeatherChance)") && GodSpeechChance(speech), "now and then, on top of each moment's own rate");

        // ================= The letters =================

        string shapes = Read("Letters/LetterFormShapes.cs");
        C(shapes.Contains("GodWords.Letter(s.values, pawn, f);") && shapes.Contains("GodWords.Letter(s.values, writer, Faction.OfPlayer);"), "the writer's god in their letters and the colony's own");
        int godLines = 0;
        foreach (var f in new[] { "Rimfolk_Letters_Close.xml", "Rimfolk_Letters_PS.xml", "Rimfolk_Letters_Sermon.xml" })
            godLines += Regex.Matches(File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", f)), @"\{GOD(EPITHET|OF)?\}").Count > 0
                ? File.ReadAllText(Path.Combine(root, "Defs", "FlavorPoolDefs", f)).Split('\n').Count(x => x.Contains("{GOD")) : 0;
        C(godLines >= 6 && godLines <= 14, $"a handful of letter lines name their god ({godLines})");
        C(Read("Gods/GodWords.cs").Contains("Gods.Learn(g, \"letter\")"), "a letter that names a god teaches the colony of it");

        // ================= The seams =================

        string seam = Read("Gods/GodPresenceSeam.cs");
        C(seam.Contains("public static Func<PresenceSpec, bool> Manifest;") && seam.Contains("public static Action<Pawn, God, bool> MakeAvatar;"),
            "the presence part's two seams: Manifest and MakeAvatar, null until it sets them");
        C(new[] { "public God god;", "public Map map;", "public IntVec3 cell", "public PresenceKind kind;", "public Pawn avatar;", "public string title, text;", "public bool veiled;" }.All(seam.Contains),
            "the plain PresenceSpec: god, map, cell, kind, avatar, the card's title and text, plain or veiled");
        C(new[] { "Appear", "Act", "Reveal", "Depart", "Blessing", "Wrath", "Omen" }.All(k => Regex.IsMatch(seam, @"\b" + k + @",?\s")), "its kinds: appear, act, reveal, depart, blessing, wrath, omen");
        C(seam.Contains("if (m == null || spec?.god == null || !Gods.On) return false;") && seam.Contains("if (m == null || p == null || god == null || !Gods.On || !GodsMath.AvatarsCome(Gods.Realness)) return false;"),
            "calls do nothing while the presence part isn't there, the gods are off, or (an avatar) they are only believed");
        C(seam.Contains("veiled = !GodsMath.PlainCard(Gods.Realness)"), "veiled by how real the gods are");
        C(!Directory.Exists(Path.Combine(src, "Gods", "Presence")) || !Directory.GetFiles(Path.Combine(src, "Gods", "Presence")).Any(f => File.ReadAllText(f).Contains("class GodBook")),
            "the core keeps out of the presence part's folder");
        foreach (var api in new[]
                 {
                     "public static IReadOnlyList<God> All", "public static List<God> Of(Faction f)", "public static List<God> OfIdeo(Ideo ideo)", "public static List<God> ColonyGods(Map home)",
                     "public static God Patron(Map home)", "public static FavourWord Favour(God g, Map home)", "public static void AddFavour(God g, Map home, float amount, string why",
                     "public static GodRealness Realness", "public static void Remember(GodAct act)", "public static string Name(God g)", "public static string Epithet(God g)",
                     "public static God SwearBy(Pawn p", "public static List<GodTie> TiesOf(God g)", "public static List<God> Rivals(God g)", "public static List<God> Allies(God g)",
                     "public static GodNature NatureOf(God g)", "public static GodKind KindOf(God g)", "public static float Loudness(God g)", "public static void Deed(GodDomain d, Map home"
                 })
            C(gods.Contains(api), "Gods: " + api.Replace("public static ", ""));
        string godCs = Read("Gods/God.cs");
        C(new[] { "public int id;", "public string name;", "public string epithet;", "public GodDomain domain, second;", "public GodForm form;", "public List<Faction> factions",
                "public List<Ideo> ideos", "public string deityKey, deityType;", "public Color colour = Color.white, colour2", "public string look;" }.All(godCs.Contains),
            "a god's record: id, name, epithet, domains, form, peoples and faiths, the deity it came from, colours, the look's defName as a string");
        C(!Regex.IsMatch(string.Join("\n", Directory.GetFiles(Path.Combine(src, "Gods"), "*.cs").Select(File.ReadAllText)), @"\bclass GodLookDef\b"), "the core never defines GodLookDef");

        // ================= The hooks, the book, the settings, the page =================

        C(Read("World/Region/RegionBook.cs").Contains("if (Gods.On) Safe(\"the gods\", () => GodBook.Daily(now));") && Read("World/Region/RegionBook.cs").Contains("GodBook.Hourly(now)"),
            "kept in the region's day and hour, no pass of its own");
        C(Read("Happenings/Happenings.cs").Contains("GodDeeds.Killed(victim, dinfo, note.beast, note.map);") && Read("Secrets/Vendettas.cs").Contains("GodDeeds.Funeral(dead, speaker);"),
            "a great beast brought down, a sacred animal slaughtered, a funeral's eulogy");
        string book = Read("Gods/GodBook.cs");
        foreach (var coll in new[] { "gods", "ties", "standings", "acts", "vows", "deeds" })
            C(book.Contains("Scribe_Collections.Look(ref " + coll + ", \"" + coll + "\", LookMode.Deep);") && book.Contains(coll + " ??= new List<"), $"the book saves its {coll} and mends them on a load");
        C(book.Contains("if (!built) Pantheon.Build(this);") && book.Contains("public const int MaxActs = 300, MaxActsPerGod = 30;"), "built once per world, an older save on its first load; the acts bounded");
        C(Read("Gods/Pantheon.cs").Contains("GodsMath.Seed(WorldSeed") && Read("Gods/Pantheon.cs").Contains("ModsConfig.IdeologyActive"), "the same for the same world; Ideology soft");
        string settings = Read("CouncilSettings.cs"), catalog = Read("UI/SettingsCatalog.cs");
        C(settings.Contains("public bool gods = true;") && settings.Contains("public GodRealness godRealness = GodRealness.Felt;") && settings.Contains("Scribe_Values.Look(ref gods, \"gods\", true);")
          && settings.Contains("Scribe_Values.Look(ref godRealness, \"godRealness\", GodRealness.Felt);"), "the settings: Gods on, How real Felt, by default");
        C(catalog.Contains("Toggle(\"gods\", \"CC_Settings_Gods\"") && catalog.Contains("Choice(\"godRealness\"") && catalog.Contains("new[] { \"Believed\", \"Felt\", \"Walking\" }, \"gods\")")
          && catalog.Contains("\"CC_SetSec_Gods\", \"CC_SetSec_Neighbours\""), "both on the What's on page, in a section of their own the setups reach");
        C(Read("UI/PagesMath.cs").Contains("[\"Peoples\"] = new[] { \"Peoples\", \"Faces\", \"Gods\" }") && Read("UI/Region/PeoplesView.cs").Contains("if (gods) GodsPage.Draw(body);")
          && Read("UI/Region/RegionView.cs").Contains("gods = a.mode == \"Gods\";"), "the Gods page: the Peoples view's third mode, by address Peoples/Gods");
        string page = Read("Gods/GodsPage.cs");
        C(page.Contains("RumourName(g)") && page.Contains("Gods.Favour(g, map)") && page.Contains("Gods.TiesOf(g)") && page.Contains("GodWords.NatureLine(g.nature)")
          && page.Contains("GodWords.KindLine(g.kind)") && page.Contains("Gods.ActsOf(g)") && page.Contains("UiKit.OwnLook"),
            "the page: rumours, favour in words, ties as a web and in words, the nature, the kind, the acts, in either look");
        C(!page.Contains("FavourValue(g, map).ToString") && !Regex.IsMatch(page, @"FavourValue\([^)]*\)\s*\.ToString"), "favour never shown as a number");
        C(Read("Gods/GodsDebug.cs").Contains("Gods: show the pantheon (log)") && Read("Gods/GodsDebug.cs").Contains("Gods: add favour here...")
          && Read("Gods/GodsDebug.cs").Contains("Gods: set how real the gods are..."), "dev actions: show the pantheon, add favour, set realness");
        C(File.ReadAllText(Path.Combine(root, "Tools", "save_estimate.py")).Contains("Gods/GodBook.cs:gods"), "the saved book modelled in the save estimate");
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
