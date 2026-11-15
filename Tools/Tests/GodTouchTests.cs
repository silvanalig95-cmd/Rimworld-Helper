using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// The gods' touch (Source/ColonyCouncil/Gods/Touch/; the player, 15 November 2026: "for many of the gods occurences i don't
/// see any real game impact ... pawns seem to take no mood or heart/strain or other gameplay impact either"; "effects can be
/// all kinds of things, be creative"; "the effect can be something more impactful also ... but keep it balanced, not that
/// strong effects come up all the time"): the moods and the heart by belief, each god's own effects in three strengths and how
/// often each comes, the same occurrence not coming back soon, the other gods' turn, an offering taken plainly once a month,
/// the defs and words (the unseen rule: no god named in them), the seams, and nothing that hurts anyone.
/// </summary>
public static class GodTouchTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "the gods' touch: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.ReadAllText(Path.Combine(src, rel));
        string touch = Read("Gods/Touch/GodTouch.cs"), effects = Read("Gods/Touch/GodTouchEffects.cs"), fx = Read("Gods/Touch/GodTouchFx.cs"),
            mathSrc = Read("Gods/Touch/GodTouchMath.cs");
        string all = touch + effects + fx + mathSrc + Read("Gods/Touch/GodTouchDebug.cs");
        var gods = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Hush", "Undertow", "Helios", "Selene", "Mog", "Murmur", "Ducat" };

        // ================= Moods and the heart, by belief =================

        var stances = (TouchStance[])Enum.GetValues(typeof(TouchStance));
        var tones = (TouchTone[])Enum.GetValues(typeof(TouchTone));
        C(tones.All(t => stances.All(s => GodTouchMath.MoodFor(s, TouchTone.None, false).mood == TouchMood.None)), "nothing to feel, no mood");
        C(tones.All(t => GodTouchMath.MoodFor(TouchStance.Scoffer, t, true).mood == TouchMood.None && GodTouchMath.EaseFor(TouchStance.Scoffer, t, true) == 0f
                         && GodTouchMath.StrainFor(TouchStance.Scoffer, t, true) == 0f), "a scoffer is unmoved, even by a god seen plainly");
        C(GodTouchMath.MoodFor(TouchStance.Devoted, TouchTone.Kind, false).mood == TouchMood.Blessed && GodTouchMath.MoodFor(TouchStance.Devoted, TouchTone.Kind, true).stage == 1
          && GodTouchMath.MoodSize(TouchMood.Blessed, 1) > GodTouchMath.MoodSize(TouchMood.Blessed, 0), "a believer glad of their god's kindness, the more seen plainly");
        C(GodTouchMath.MoodFor(TouchStance.Devoted, TouchTone.Sight, false).mood == TouchMood.Acted && GodTouchMath.MoodFor(TouchStance.Devoted, TouchTone.Sight, true).mood == TouchMood.Blessed,
            "a believer's god at work: the belief round's own small mood, its strongest seen plainly");
        C(GodTouchMath.MoodFor(TouchStance.Dreads, TouchTone.Kind, false).mood == TouchMood.None && GodTouchMath.MoodFor(TouchStance.Dreads, TouchTone.Sight, false).mood == TouchMood.Afraid
          && GodTouchMath.MoodFor(TouchStance.Dreads, TouchTone.Dark, true).stage == 1, "one in dread takes no comfort from its kindness, and is afraid when it comes");
        C(GodTouchMath.MoodFor(TouchStance.Ordinary, TouchTone.Sight, false).mood == TouchMood.Wonder && GodTouchMath.MoodFor(TouchStance.Ordinary, TouchTone.Sign, false).mood == TouchMood.Uneasy
          && GodTouchMath.MoodSize(TouchMood.Uneasy, 0) < 0f && GodTouchMath.MoodSize(TouchMood.Uneasy, 0) > GodTouchMath.MoodSize(TouchMood.Uneasy, 1),
            "someone of no strong belief awed by a sight, a little uneasy at a sign, more at an anger");
        C(tones.All(t => stances.All(s => Math.Abs(GodTouchMath.MoodSize(GodTouchMath.MoodFor(s, t, true).mood, GodTouchMath.MoodFor(s, t, true).stage)) <= 6f)),
            "sized like the game's moods for things of that weight: six at most either way");
        C(tones.All(t => stances.All(s => GodTouchMath.EaseFor(s, t, true) <= 2.5f && GodTouchMath.StrainFor(s, t, true) <= 2f)),
            "the heart eased or strained a little at a time, within its own caps (the day's small joys 3, dread 2)");
        C(GodTouchMath.EaseFor(TouchStance.Devoted, TouchTone.Kind, true) > GodTouchMath.EaseFor(TouchStance.Ordinary, TouchTone.Kind, true)
          && GodTouchMath.StrainFor(TouchStance.Dreads, TouchTone.Sight, true) > GodTouchMath.StrainFor(TouchStance.Ordinary, TouchTone.Sight, true)
          && GodTouchMath.EaseFor(TouchStance.Dreads, TouchTone.Kind, true) == 0f, "a believer eased the most, one in dread strained the most");
        C(touch.Contains("HeartLogic.EaseJoys(x, ease, why)") && touch.Contains("HeartLogic.Build(x, StrainSource.Dread, strain, why)"), "through the heart's own API, as other events do");
        C(touch.Contains("RemoveMemoriesOfDef(PawnGodsDefOf.Rimfolk_Belief_GodActed)"), "their god's kindness takes the place of the smaller mood the belief round gave, never both");

        // ================= Which occurrences have their own effect =================

        foreach (var k in new[] { "world", "prayer", "council", "CC_GodM_Saw_Weather_Rain", "CC_GodM_Saw_Return_HealedSeen", "CC_GodO_Saw_Vein", "CC_GodO_Saw_TestStory", "CC_GodO_Saw_Bowl",
                     "CC_GodMurmur_Saw_Rain", "CC_GodUndertow_Saw_Ear", "CC_GodM_Saw_StrangerShown", "CC_GodO_Saw_Burn" })
            C(GodTouchMath.HasOwnEffect(k), k + " has an effect of its own: the moods only");
        foreach (var k in new[] { "taken", "vow", null, "", "CC_GodO_Saw_BowlSet", "CC_GodO_Saw_BurnDream", "CC_GodO_Saw_StarsNight", "CC_GodO_Saw_Stars", "CC_GodO_Saw_Roots",
                     "CC_GodM_Saw_Rising", "CC_GodHelios_Saw_Dawn", "CC_GodSelene_Saw_Water", "CC_GodO_Saw_Mourner", "CC_GodM_Saw_Candle" })
            C(!GodTouchMath.HasOwnEffect(k), (k ?? "null") + " has none of its own: one of its god's follows");
        C(GodTouchMath.WordsOnly("told") && GodTouchMath.WordsOnly("mark") && !GodTouchMath.WordsOnly("taken"), "a letter's tale and a mark talked of are no occurrence at all");

        // ================= Each god's own =================

        foreach (var g in gods)
        {
            var list = GodTouchMath.Of(g);
            C(list.Count >= 5 && list.Select(e => e.effect).Distinct().Count() == list.Count, g + ": five or more of its own, each once");
            C(list.Any(e => e.tier == TouchTier.Small && !e.dark), g + ": something small and kind");
            C(g == "Undertow" ? list.Any(e => e.tier == TouchTier.Great && e.dark) : list.Any(e => e.tier == TouchTier.Great && !e.dark),
                g + ": something great, for the rare times (the Hunger's only dark)");
            C(list.Any(e => e.tier == TouchTier.Notable), g + ": something notable");
            foreach (var e in list)
                C(Regex.IsMatch(effects, @"case TouchEffect\." + e.effect + @"\b[^\n]*(:|\n)") && Regex.Matches(effects, @"case TouchEffect\." + e.effect + @"\b").Count >= 2,
                    g + ": " + e.effect + " can be checked and done");
        }
        var sets = gods.Select(g => string.Join(",", GodTouchMath.Of(g).Where(e => e.effect != TouchEffect.Bless).Select(e => e.effect).OrderBy(x => x))).ToList();
        C(sets.Distinct().Count() == gods.Length, "no two gods have the same effects: each its own");
        C(GodTouchMath.Of("Ennu").Any(e => e.effect == TouchEffect.Visitors) && GodTouchMath.Of("Ennu").Any(e => e.effect == TouchEffect.Goodwill)
          && GodTouchMath.Of("Ennu").Any(e => e.effect == TouchEffect.Wanderer), "the Guest's offering taken: travellers come, a people thinks better of the colony, rarely someone stays");
        C(new[] { "Ennu", "Helios", "Mog" }.All(g => GodTouchMath.Of(g).All(e => !e.dark)), "the Guest, the sun that gives blindly and the keeper at the door have no anger of their own here");
        C(GodTouchMath.Of("Nobody").Count == 1 && GodTouchMath.Of("Nobody")[0].effect == TouchEffect.Bless && GodTouchMath.Of(null).Count == 1, "a god with none here has the shared small blessing");
        C(GodTouchMath.Lingers.All(g => GodTouchMath.LingerDays(g) >= 2f && GodTouchMath.LingerDays(g) <= GodTouchMath.SeasonDays)
          && !GodTouchMath.Lingers.Contains("Helios") && !GodTouchMath.Lingers.Contains("Hush") && !GodTouchMath.Lingers.Contains("Morel") && !GodTouchMath.Lingers.Contains("Mog"),
            "a god's presence stays days, never a long noon, a white silence or a rot for days, nor Tamiu's watch (hers only while she is here)");
        C(gods.Where(g => GodTouchMath.Of(g).Any(e => e.effect == TouchEffect.Linger || e.effect == TouchEffect.Dim)).All(GodTouchMath.Lingers.Contains), "every god whose presence stays may stay");

        // Leaning away: the last effect hardly, so the same one doesn't come twice running when another can.
        var hollin = GodTouchMath.Of("Hollin");
        int same = 0;
        for (int i = 0; i < 200; i++)
            if (GodTouchMath.Pick(hollin, TouchTier.Small, false, new List<string> { "Bless" }, null, i / 200f).effect == TouchEffect.Bless) same++;
        C(same <= 6, $"the effect it left last is leaned away from ({same} of 200)");
        C(GodTouchMath.Pick(hollin, TouchTier.Great, false, new List<string>(), e => e.tier != TouchTier.Great, 0.5f).tier == TouchTier.Notable,
            "with none of a tier that can come now, one of the tier below");
        C(GodTouchMath.Pick(GodTouchMath.Of("Ennu"), TouchTier.Small, true, new List<string>(), null, 0.5f).effect == TouchEffect.None, "an anger for a god with none: the moods only");

        // ================= How strong, how often =================

        C(GodTouchMath.Tier(TouchTone.Sign, true, true, 0f, 0f, 99, 99, 99, 999, 999) == TouchTier.Small, "an omen, at most something small");
        C(GodTouchMath.Tier(TouchTone.Kind, true, true, 0f, 0f, 99, 99, 99, 999, 999) == TouchTier.Great, "a great one when its roll and its waits allow");
        C(GodTouchMath.Tier(TouchTone.Kind, true, true, 0f, 0f, 99, 99, 99, 50, 999) == TouchTier.Notable && GodTouchMath.Tier(TouchTone.Kind, true, true, 0f, 0f, 99, 99, 99, 999, 20) == TouchTier.Notable,
            "a great one's long waits (a god's, the colony's) not past: notable instead");
        C(GodTouchMath.Tier(TouchTone.Kind, false, false, 0f, 0.2f, 99, 2, 99, 999, 999) == TouchTier.Small, "a notable one's wait not past: small instead");
        C(GodTouchMath.Tier(TouchTone.Kind, false, false, 0f, 0.9f, 0.2f, 99, 99, 999, 999) == TouchTier.None, "a god's small ones twice a day at most");
        C(GodTouchMath.Tier(TouchTone.None, true, true, 0f, 0f, 99, 99, 99, 999, 999) == TouchTier.None, "a departure or a mark: nothing follows");
        C(GodTouchMath.GreatChance(TouchTone.Kind, false, false) < GodTouchMath.NotableChance(TouchTone.Kind, false, false) && GodTouchMath.GreatChance(TouchTone.Kind, true, true) <= 0.2f
          && GodTouchMath.GreatChance(TouchTone.Dark, true, true) < GodTouchMath.GreatChance(TouchTone.Kind, true, true), "each rarer the stronger it is, an anger rarer still");
        C(GodTouchMath.GreatGodDays >= 100f && GodTouchMath.GreatColonyDays >= 30f && GodTouchMath.NotableGodDays >= 5f, "long waits between the strong ones, per god and for the colony");

        // A year and more of occurrences at one home: about one a day (as the player's save had), any god, a fifth of them
        // seen plainly and a tenth comings; the waits kept as the code keeps them.
        var rng = new Random(7);
        int small = 0, notable = 0, great = 0, nothing = 0;
        const int days = 600;
        var lastS = gods.ToDictionary(g => g, _ => -9999f);
        var lastN = gods.ToDictionary(g => g, _ => -9999f);
        var lastG = gods.ToDictionary(g => g, _ => -9999f);
        float lastNc = -9999f, lastGc = -9999f;
        var toneRoll = new[] { TouchTone.Sign, TouchTone.Sign, TouchTone.Sign, TouchTone.Sight, TouchTone.Sight, TouchTone.Kind, TouchTone.Kind, TouchTone.Dark };
        for (int d = 0; d < days; d++)
        {
            if (rng.NextDouble() > 0.9) continue;
            string g = gods[rng.Next(gods.Length)];
            var tone = toneRoll[rng.Next(toneRoll.Length)];
            bool plain = rng.NextDouble() < 0.2, coming = rng.NextDouble() < 0.1;
            var t = GodTouchMath.Tier(tone, GodTouchMath.Big(tone, coming, plain), plain, (float)rng.NextDouble(), (float)rng.NextDouble(), d - lastS[g], d - lastN[g], d - lastNc, d - lastG[g], d - lastGc);
            switch (t)
            {
                case TouchTier.Small: small++; lastS[g] = d; break;
                case TouchTier.Notable: notable++; lastN[g] = d; lastNc = d; break;
                case TouchTier.Great: great++; lastG[g] = d; lastGc = d; break;
                default: nothing++; break;
            }
        }
        float years = days / 60f;
        C(small > notable * 1.5f && notable > great * 3, $"most are small, some notable, few great ({small} small, {notable} notable, {great} great in {years:0} years)");
        C(great / years <= 1.6f && great >= 3, $"a great one about once a year at a home ({great / years:0.0} a year)");
        C(notable / years >= 5f && notable / years <= 20f, $"a notable one every few weeks ({notable / years:0.0} a year)");
        C(nothing > 0, "and some leave the moods only");

        // ================= Repetition =================

        C(GodTouchMath.SameKeyGapDays("taken") == 4f && GodTouchMath.SameKeyGapDays("CC_GodO_Saw_Roots") == 10f && GodTouchMath.GapKey("taken", "Ennu") == "taken|Ennu",
            "the same occurrence again within days leaves the moods only (an offering taken, four days a god)");
        C(GodTouchMath.Turn(0) == 1f && GodTouchMath.Turn(1) < 1f && GodTouchMath.Turn(3) < GodTouchMath.Turn(1) && GodTouchMath.Turn(50) >= 0.35f,
            "a god seen often lately gives the others their turn, never shut out");
        C(GodTouchMath.PlainTaking(30f, 8f) && !GodTouchMath.PlainTaking(29f, 99f) && !GodTouchMath.PlainTaking(99f, 7f),
            "an offering taken before everyone's eyes a month apart at most a god, any god's a week and a day: the Guest's three in a short while can't come again");
        C(Read("Gods/Worship/Offerings.cs").Contains("&& GodTouch.PlainTakingAllowed(g, map);") && Read("Gods/Worship/Offerings.cs").Contains("GodTouch.PlainTaken(g, map);")
          && Read("Gods/Worship/Offerings.cs").Contains("GodTouch.Taken(g, shrine.Map, shrine.Position, new[] { p });"),
            "an offering's taking: plainly a month apart, and what follows once someone sees it go or finds the place empty");
        C(Read("Gods/OnMap/OwnHappenings.cs").Contains("* GodTouch.Turn(g, map));"), "each god's own happenings give the others their turn");

        // ================= The seams =================

        string godsSrc = Read("Gods/Gods.cs");
        C(godsSrc.Contains("GodTouch.Note(act, pawns, map, ByKey(taken));") && godsSrc.Contains("GodTouch.NoteMoment(spec, witnesses, plain, taken);")
          && godsSrc.Contains("GodTouch.Note(act, null, Homes.MapOf(act.home) ?? Homes.Seat, null);"),
            "every occurrence someone saw reaches it through the seams already there, a moment and its words as one");
        C(Read("Gods/OnMap/GodShapes.cs").Contains("what = key });") && Read("Gods/World/GodWorld.cs").Contains("what = \"told\"") && Read("Gods/World/GodWorld.cs").Contains("spec.what = \"world\";")
          && Read("Gods/OnMap/GodMarks.cs").Contains("what = \"mark\""), "each occurrence says what it was");
        C(touch.Contains("GodPresence.Busy && now - p.at < LongestWait"), "it follows once the god's moment has played out");
        C(touch.Contains("private static bool resolving;") && Regex.Matches(touch, @"if \(.*resolving").Count >= 3, "what the touch itself does is never noted again");
        // The player, 15 November 2026: "The camera should move for all non-trivial god moments".
        string presenceSrc = Read("Gods/Presence/GodPresence.cs");
        C(!presenceSrc.Contains("quiet") && !Read("Gods/GodPresenceSeam.cs").Contains("public bool quiet;") && !Read("Gods/Worship/Prayers.cs").Contains("\"prayer\", true);")
          && Read("Gods/GodsWiring.cs").Contains("s.god.Key, s.lingerTicks);"),
            "no god's moment is kept from the camera: a prayer's answers too");
        C(Regex.IsMatch(presenceSrc, @"private static void Omen\(Moment m\)\s*\{[^}]*Look\(m\.map, m\.at\);")
          && Read("Gods/Touch/GodTouchEffects.cs").Contains("if (c.tier >= TouchTier.Notable) GodPresence.Look(c.map, at);\n            GodTouchFx.Show(")
          && Read("Gods/OnMap/OwnHappenings.cs").Contains("GodPresence.Look(look);") && Read("Gods/OnMap/OwnSeven.cs").Contains("GodPresence.Look(look);")
          && Read("Gods/OnMap/GodGifts.cs").Contains("GodPresence.Look(map, at);") && Regex.Matches(Read("Gods/OnMap/MogsWays.cs"), @"GodPresence\.Look\(").Count == 4,
            "the camera goes to an omen, the gods' own happenings, a gift left, the great cat's doings and a notable effect, before the god's look plays");
        C(presenceSrc.Contains("if (!target.IsValid || target.Map == null || !Homes.IsHome(target.Map)) return;") && !Read("Gods/Touch/GodTouchEffects.cs").Contains("TouchTier.Small) GodPresence.Look"),
            "only to a home's map, and never for a small touch");
        C(Read("Gods/Presence/GodPresenceCondition.cs").Contains("if (linger > 0) ticks = linger;"),
            "a god's presence may stay for days, its sky only lightly tinted");
        C(Read("Gods/Presence/GodFx.cs").Contains("GodTouchFx.Signature(godKey, map, cell, a, b, big ? TouchTier.Great : TouchTier.Small);"),
            "every god's big moment and omen has its own look");

        // ================= The looks =================

        foreach (var g in gods)
            C(Regex.IsMatch(fx, "\\[\"" + g + "\"\\] = new Look \\{ flecks = new\\[\\] \\{"), g + " has its own look");
        var flecks = Regex.Matches(fx, @"flecks = new\[\] \{([^}]*)\}").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        C(flecks.Count == gods.Length && flecks.Distinct().Count() == gods.Length, "every god's look its own");

        // ================= The defs and words =================

        var defsDoc = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodTouch.xml"));
        var thoughts = defsDoc.Root.Elements("ThoughtDef").ToDictionary(e => (string)e.Element("defName"), e => e.Element("stages").Elements("li").Select(li => float.Parse((string)li.Element("baseMoodEffect"), System.Globalization.CultureInfo.InvariantCulture)).ToList());
        C(thoughts["Rimfolk_GodTouch_Blessed"].SequenceEqual(new[] { GodTouchMath.MoodSize(TouchMood.Blessed, 0), GodTouchMath.MoodSize(TouchMood.Blessed, 1) })
          && thoughts["Rimfolk_GodTouch_Wonder"].SequenceEqual(new[] { GodTouchMath.MoodSize(TouchMood.Wonder, 0), GodTouchMath.MoodSize(TouchMood.Wonder, 1) })
          && thoughts["Rimfolk_GodTouch_Uneasy"].SequenceEqual(new[] { GodTouchMath.MoodSize(TouchMood.Uneasy, 0), GodTouchMath.MoodSize(TouchMood.Uneasy, 1) })
          && thoughts["Rimfolk_GodTouch_Afraid"].SequenceEqual(new[] { GodTouchMath.MoodSize(TouchMood.Afraid, 0), GodTouchMath.MoodSize(TouchMood.Afraid, 1) }),
            "the moods' sizes are the rules' own");
        var hediffs = defsDoc.Root.Elements("HediffDef").Where(e => e.Element("defName") != null).ToDictionary(e => (string)e.Element("defName"), e => e);
        foreach (var g in gods)
            C(hediffs.ContainsKey("Rimfolk_GodTouch_Bless_" + g) && hediffs["Rimfolk_GodTouch_Bless_" + g].Element("stages") != null, g + ": its own short blessing");
        C(new[] { "Rimfolk_GodTouch_Hardy", "Rimfolk_GodTouch_SunWarmed", "Rimfolk_GodTouch_Ward", "Rimfolk_GodTouch_Sleepless" }.All(hediffs.ContainsKey), "the lasting great ones and a bad night");
        var godNames = new[] { "Hollin", "Smoulder", "Bellwether", "Morel", "Ennu", "Pip", "Cirrus", "Hush", "Undertow", "Helios", "Selene", "Tamiu", "Mog", "Murmur", "Ducat", "the Hunger", "the Guest", "the Stranger" };
        string defsText = string.Join("\n", defsDoc.Descendants().Where(e => e.Name == "label" || e.Name == "description").Select(e => (string)e));
        C(!godNames.Any(gn => defsText.Contains(gn)), "no god named in a mood or a blessing: felt, never whose");
        string keyedAll = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_GodTouch.xml"));
        var keyed = Regex.Matches(keyedAll, @"<(CC_GodTouch_[A-Za-z_]+)>(.*?)</\1>").Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        C(!godNames.Any(gn => keyed.Values.Any(v => v.Contains(gn))), "no god named in what is told; a great one's card names it only where the colony knows (Gods.CardName)");
        C(keyed.Values.All(v => !v.Contains("—") && !v.Contains("!")), "plain words: no dashes, no exclamations");
        var used = Regex.Matches(effects, @"K\(""([A-Za-z_]+)""\)").Cast<Match>().Select(m => "CC_GodTouch_" + m.Groups[1].Value).Distinct().ToList();
        C(used.All(keyed.ContainsKey), "every word the effects use is there (" + string.Join(", ", used.Where(k => !keyed.ContainsKey(k))) + ")");
        foreach (var g in gods) C(keyed.ContainsKey("CC_GodTouch_Bless_" + g), g + ": its blessing's words");
        foreach (var g in GodTouchMath.Lingers) C(keyed.ContainsKey(g == "Undertow" ? "CC_GodTouch_Dim_Undertow" : "CC_GodTouch_Linger_" + g), g + ": the words for its presence staying");
        foreach (var w in new[] { "Vein", "Herd", "Wanderer", "Hardy", "SunYear", "ScarGone", "Ward", "Spring", "Hoard", "Fertile", "Linger", "Dim" })
            C(keyed.ContainsKey("CC_GodTouch_" + w + "_Title"), w + ": its card's title");

        // ================= No harm, the mod pool, saved and small =================

        C(!Regex.IsMatch(all, @"Harm\.(Injure|Ail|Fight)|TakeDamage|DamageInfo"), "nothing here hurts anyone: the angers spoil, break, chill and frighten");
        foreach (var name in new[] { "VPEF_Plant_Daisy", "VSIE_Inspired_Planting", "ID_FarmingFrenzy", "Fish_Bass", "Mote_GreenTwinkle", "PsychicDistortionRingContracting" })
            C(all.Contains("\"" + name + "\""), "the player's mods and the DLCs by name, softly: " + name);
        C(touch.Contains("Scribe_Collections.Look(ref last, \"last\", LookMode.Value, LookMode.Value);") && touch.Contains("while (recent.Count > GodTouchMath.RecentKept) recent.RemoveAt(0);")
          && touch.Contains("while (seen.Count > MaxSeen) seen.RemoveAt(0);"), "saved small: a few keys a god and a home, two capped lists");
        return n;
    }
}
