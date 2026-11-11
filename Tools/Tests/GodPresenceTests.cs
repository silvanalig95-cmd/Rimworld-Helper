using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ColonyCouncil;

/// <summary>
/// A god's presence (docs/design/gods.md, "Presence"; Source/ColonyCouncil/Gods/Presence/): the pure rules (the
/// fourteen domains and each one's look in every age, the aura by day and night, the growth at a reveal, the moment's
/// timings, the mix of sounds and the track, who answers a god and how, who notices one in disguise, the trail, the
/// mark); the looks (every domain and age covered, and every robe, crown, mote, effecter, circle, spirit, sound, song
/// and animal they name in the player's mods, A RimWorld of Magic's among them, docs/player-mods); the hediff, jobs,
/// presence, card and motes; the lines said aloud (one sentence each, only a named god named); the words; the settings;
/// the textures and their tool; and the seams (the API the core's seams will be wired to, nothing of the core's needed,
/// the game's threads, the one voice, the soft lookups).
/// </summary>
public static class GodPresenceTests
{
    public static int Run(Action<bool, string> check)
    {
        string root = Root();
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "god presence: " + what);
        }

        // ================= The domains and their looks =================

        C(GodPresenceMath.Domains.Length == 14 && GodPresenceMath.Domains.Distinct().Count() == 14, "fourteen domains, each once");
        C(new[] { "Harvest", "Storm", "Hunt", "Healing", "War", "Craft", "Way", "Dead", "Night", "Hearth", "Wild", "Waters", "Machine", "Void" }
            .All(GodPresenceMath.IsDomain) && !GodPresenceMath.IsDomain("Cheese") && !GodPresenceMath.IsDomain(null), "the design's domains, and no others");
        C(GodPresenceMath.LookDefName("Storm") == "Rimfolk_GodLook_Storm", "a domain's look is Rimfolk_GodLook_<Domain>");
        var looksT = new List<(string, int, int)> { ("War", 0, 4), ("War", 5, 7), ("Storm", 0, 7), ("Machine", 4, 7), ("Machine", 0, 3) };
        C(GodPresenceMath.Choose(looksT, "War", 3) == 0 && GodPresenceMath.Choose(looksT, "War", 5) == 1 && GodPresenceMath.Choose(looksT, "War", 7) == 1,
            "war's look by age: the old one to the industrial age, the spacer one after");
        C(GodPresenceMath.Choose(looksT, "Machine", 2) == 4 && GodPresenceMath.Choose(looksT, "Machine", 4) == 3, "the Iron Spirit before machines, the Broker after");
        C(GodPresenceMath.Choose(looksT, "storm", 2) == 2, "domains match whatever their case");
        C(GodPresenceMath.Choose(looksT, "Hearth", 3) == -1 && GodPresenceMath.Choose(null, "War", 3) == -1, "no look for a domain without one");
        var narrow = new List<(string, int, int)> { ("Craft", 0, 7), ("Craft", 5, 7) };
        C(GodPresenceMath.Choose(narrow, "Craft", 6) == 1 && GodPresenceMath.Choose(narrow, "Craft", 3) == 0, "an age variant beats the look for every age where both hold");
        var gap = new List<(string, int, int)> { ("Way", 2, 3), ("Way", 5, 6) };
        C(GodPresenceMath.Choose(gap, "Way", 7) >= 0, "an age no look holds still gets the domain's widest");

        // ================= The aura =================

        bool pulseOk = true;
        for (float t = 0f; t < 10f; t += 0.13f)
        {
            float p = GodPresenceMath.Pulse(t, GodPresenceMath.PulseSeconds);
            pulseOk &= p >= -0.0001f && p <= 1.0001f;
        }
        C(pulseOk, "the pulse stays between 0 and 1");
        C(Math.Abs(GodPresenceMath.Pulse(0f, 3f) - GodPresenceMath.Pulse(3f, 3f)) < 0.001f, "the pulse repeats each breath");
        C(GodPresenceMath.Pulse(1f, 0f) == 1f, "no period, no pulse");
        for (float sky = 0f; sky <= 1f; sky += 0.25f)
        for (float p = 0f; p <= 1f; p += 0.5f)
        {
            C(GodPresenceMath.HaloAlpha(sky, p) > 0.3f && GodPresenceMath.HaloAlpha(sky, p) <= 0.8f, $"the halo shows by day and night, never solid (sky {sky}, pulse {p})");
            C(GodPresenceMath.GlowAlpha(sky, p) > 0.2f && GodPresenceMath.RingAlpha(sky, p) > 0.25f, $"the glow and the ring show (sky {sky})");
        }
        C(GodPresenceMath.HaloAlpha(0f, 0.5f) > GodPresenceMath.HaloAlpha(1f, 0.5f) && GodPresenceMath.GlowAlpha(0f, 0.5f) > GodPresenceMath.GlowAlpha(1f, 0.5f),
            "the aura is stronger at night");
        C(GodPresenceMath.NightShineAlpha(1f, 1f) == 0f && GodPresenceMath.NightShineAlpha(0.7f, 1f) == 0f && GodPresenceMath.NightShineAlpha(0f, 1f) > 0.3f,
            "the shine over the dark only at night");
        C(GodPresenceMath.RingScale(0f) < GodPresenceMath.RingScale(1f) && GodPresenceMath.RingScale(1f) <= 1.05f, "the ring breathes a little");
        var white = GodPresenceMath.DayTint(new GodRgb(0.95f, 0.95f, 0.95f));
        var silver = GodPresenceMath.DayTint(new GodRgb(0.85f, 0.88f, 1f));
        var red = GodPresenceMath.DayTint(new GodRgb(0.75f, 0.18f, 0.15f));
        C(Math.Abs(white.r - white.b) > 0.1f && silver.b > silver.r + 0.05f, "a white god's light warms, a silver one's cools, so they show on a sunlit field");
        C(red.r == 0.75f && red.g == 0.18f && red.b == 0.15f, "a strong colour stays as it is");

        // ================= The body =================

        C(GodPresenceMath.SizeFactor(0.5f) == 1f && GodPresenceMath.SizeFactor(3f) == GodPresenceMath.MaxSize && GodPresenceMath.SizeFactor(1.3f) == 1.3f,
            "a god is never drawn smaller, nor huge");
        C(GodPresenceMath.SizeAt(1.4f, 0f) == 1f && Math.Abs(GodPresenceMath.SizeAt(1.4f, GodPresenceMath.GrowSeconds) - 1.4f) < 0.0001f
          && GodPresenceMath.SizeAt(1.4f, -1f) == 1.4f, "at a reveal it grows from its mortal size to its own; shown, it is its size");
        bool grows = true;
        float last = 0f;
        for (float t = 0f; t <= GodPresenceMath.GrowSeconds; t += 0.05f)
        {
            float s = GodPresenceMath.SizeAt(1.4f, t);
            grows &= s >= last - 0.0001f;
            last = s;
        }
        C(grows, "it grows steadily, never shrinking on the way");
        C(GodPresenceMath.StatelyWalk < 1f && GodPresenceMath.StatelyWalk >= 0.7f, "a stately walk: slower, not crippled");

        // ================= The moment =================

        foreach (PresenceKind k in Enum.GetValues(typeof(PresenceKind)))
        {
            bool big = GodPresenceMath.Big(k);
            C(big == (k != PresenceKind.Omen), $"{k}: only an omen is small");
            if (big)
            {
                C(Math.Abs(GodPresenceMath.StingAt(k, true) - (GodPresenceMath.FadeSeconds + GodPresenceMath.SilenceSeconds)) < 0.001f,
                    $"{k}: the music fades, a beat of silence, then the sting");
                C(GodPresenceMath.TrackAt(k, true) > GodPresenceMath.StingAt(k, true) && GodPresenceMath.CardAt(k, true) > GodPresenceMath.TrackAt(k, true),
                    $"{k}: the track after the sting, the card after the track");
                C(GodPresenceMath.TrackAt(k, false) < 0f && GodPresenceMath.StingAt(k, false) < 1f && GodPresenceMath.CardAt(k, false) > GodPresenceMath.StingAt(k, false),
                    $"{k}: with the music off, no fade and no track, the sting at once, the card after");
                C(GodPresenceMath.ConditionTicks(k) >= 2500 && GodPresenceMath.ConditionTicks(k) <= 2500 * 6, $"{k}: the presence on the sky lasts an hour to a few");
                C(GodPresenceMath.SilenceFor(true) > GodPresenceMath.TrackAt(k, true) && GodPresenceMath.SilenceFor(false) > GodPresenceMath.CardAt(k, true),
                    $"{k}: the game's music stays quiet until the god's track has begun (or past the card without one)");
            }
            else
                C(GodPresenceMath.TrackAt(k, true) < 0f && GodPresenceMath.CardAt(k, true) < 0f && GodPresenceMath.ConditionTicks(k) == 0, "an omen: no track, no card, no sky");
        }
        C(GodPresenceMath.Darken(PresenceKind.Wrath) > GodPresenceMath.Darken(PresenceKind.Appear) && GodPresenceMath.Darken(PresenceKind.Blessing) == 0f,
            "a wrath darkens the sky, a blessing never");
        C(GodPresenceMath.SkyTint(PresenceKind.Wrath) > GodPresenceMath.SkyTint(PresenceKind.Depart), "a wrath tints the sky most, a departure least");
        var sc = GodPresenceMath.SkyColor(new GodRgb(0.4f, 0.2f, 0.6f), 0.5f);
        C(Math.Abs(sc.r - 0.7f) < 0.001f && Math.Abs(sc.b - 0.8f) < 0.001f, "the sky's colour is the god's from white, as far as the kind says");
        C(GodPresenceMath.WindFactor(1f, PresenceKind.Appear) == 0f && GodPresenceMath.WindFactor(0.8f, PresenceKind.Wrath) > GodPresenceMath.WindFactor(0.8f, PresenceKind.Appear),
            "the wind dies under a presence, a wrath stirs it a little");
        C(GodPresenceMath.SlowsTime(PresenceKind.Appear, 3) && !GodPresenceMath.SlowsTime(PresenceKind.Appear, 1) && !GodPresenceMath.SlowsTime(PresenceKind.Appear, 0)
          && !GodPresenceMath.SlowsTime(PresenceKind.Omen, 3), "the game drops to normal speed for a big moment; a paused game stays paused; an omen changes nothing");

        // ================= Sounds and music =================

        var have = new HashSet<string> { "Thunder_OnMap", "TM_Gong", "GameStartSting", "Sky_Farmer", "Wabi_Sabi", "Blood_Rain_Falling" };
        Func<string, bool> exists = have.Contains;
        var mix = GodPresenceMath.Mix(new[] { "AM_Missing", "Thunder_OnMap", "Thunder_OnMap", "TM_Gong", "GameStartSting" }, exists, 2);
        C(mix.SequenceEqual(new[] { "Thunder_OnMap", "TM_Gong" }), "the mix: the look's sounds in order, a missing mod's skipped, each once, two at most");
        C(GodPresenceMath.Mix(null, exists, 2).Count == 0 && GodPresenceMath.Mix(new[] { "x" }, null, 2).Count == 0 && GodPresenceMath.Mix(new[] { "TM_Gong" }, exists, 0).Count == 0,
            "no sounds, nothing played");
        C(GodPresenceMath.Track(new[] { "own1", "own2" }, new[] { "Sky_Farmer" }, null, exists, PresenceKind.Appear, 3) == "own2", "the player's own track first");
        C(GodPresenceMath.Track(null, new[] { "Missing", "Sky_Farmer", "Wabi_Sabi" }, new[] { "Blood_Rain_Falling" }, exists, PresenceKind.Wrath, 5) == "Blood_Rain_Falling",
            "a wrath plays a dark track");
        C(new[] { "Sky_Farmer", "Wabi_Sabi" }.Contains(GodPresenceMath.Track(null, new[] { "Missing", "Sky_Farmer", "Wabi_Sabi" }, new[] { "Blood_Rain_Falling" }, exists, PresenceKind.Blessing, -7)),
            "a blessing one of the look's own that exists");
        C(GodPresenceMath.Track(null, new[] { "Missing" }, null, exists, PresenceKind.Appear, 1) == null, "no track that exists, none");
        var seen = new HashSet<string>();
        for (int s = 0; s < 8; s++) seen.Add(GodPresenceMath.Track(null, new[] { "Sky_Farmer", "Wabi_Sabi" }, null, exists, PresenceKind.Appear, s));
        C(seen.Count == 2, "the track varies from one coming to the next");
        C(GodPresenceMath.HumVolume > 0.1f && GodPresenceMath.HumVolume < 0.5f && GodPresenceMath.HumHearCells >= 10f, "a god's hum is soft, and heard only near it");

        // ================= Who answers a god =================

        var colonist = new GodWatcher { colonist = true, distance = 5f, sees = true };
        C(GodPresenceMath.ReactionTo(colonist, false) == GodReaction.Face && GodPresenceMath.ReactionTo(colonist, true) == GodReaction.Face, "a colonist stops and turns to it");
        var devout = colonist;
        devout.devout = true;
        C(GodPresenceMath.ReactionTo(devout, true) == GodReaction.Pray, "the devout pray");
        foreach (var (field, who) in new (string, GodWatcher)[]
                 {
                     ("hostile", new GodWatcher { colonist = true, hostile = true, sees = true, distance = 2f }),
                     ("drafted", new GodWatcher { colonist = true, drafted = true, sees = true, distance = 2f }),
                     ("downed", new GodWatcher { colonist = true, downed = true, sees = true, distance = 2f }),
                     ("asleep", new GodWatcher { colonist = true, asleep = true, sees = true, distance = 2f }),
                     ("in a mental state", new GodWatcher { colonist = true, mentalState = true, sees = true, distance = 2f }),
                     ("busy with what mustn't wait", new GodWatcher { colonist = true, busy = true, sees = true, distance = 2f }),
                     ("a predator hunting", new GodWatcher { animal = true, predatorHunting = true, sees = true, distance = 2f })
                 })
            C(GodPresenceMath.ReactionTo(who, true) == GodReaction.None, $"never someone {field}");
        var far = colonist;
        far.distance = 30f;
        var blind = colonist;
        blind.sees = false;
        C(GodPresenceMath.ReactionTo(far, false) == GodReaction.None && GodPresenceMath.ReactionTo(blind, false) == GodReaction.None
          && GodPresenceMath.ReactionTo(far, true) == GodReaction.Face, "a passing god: only those near who see it; its coming: everyone");
        var tame = new GodWatcher { animal = true, tame = true, wildness = 0.9f, distance = 8f, sees = true };
        var shy = new GodWatcher { animal = true, wildness = 0.8f, distance = 10f, sees = true };
        var calm = new GodWatcher { animal = true, wildness = 0.2f, distance = 10f, sees = true };
        var middling = new GodWatcher { animal = true, wildness = 0.5f, distance = 10f, sees = true };
        C(GodPresenceMath.ReactionTo(tame, true) == GodReaction.LieDown && GodPresenceMath.ReactionTo(calm, true) == GodReaction.LieDown, "at its coming the tame and the calm lie down");
        C(GodPresenceMath.ReactionTo(shy, true) == GodReaction.Flee && GodPresenceMath.ReactionTo(middling, true) == GodReaction.Face, "a shy wild one runs, others stop and turn");
        var shyFar = shy;
        shyFar.distance = 35f;
        C(GodPresenceMath.ReactionTo(shyFar, true) == GodReaction.Face, "a shy one far off only turns");
        C(GodPresenceMath.ReactionTo(tame, false) == GodReaction.Face, "a god passing: animals stop and face it");
        C(GodPresenceMath.ReactTicks(GodReaction.Pray, true) > GodPresenceMath.ReactTicks(GodReaction.Face, true) && GodPresenceMath.ReactTicks(GodReaction.Face, true) > GodPresenceMath.ReactTicks(GodReaction.Face, false)
          && GodPresenceMath.ReactTicks(GodReaction.Face, false) >= 120 && GodPresenceMath.ReactTicks(GodReaction.Pray, true) <= 1200, "a few seconds each: the devout longest, a coming longer than a passing");
        C(GodPresenceMath.Speaks(10000, 0, -1, false) && !GodPresenceMath.Speaks(1000, 0, -1, false) && GodPresenceMath.Speaks(1000, 0, -1, true),
            "a passing god draws a line once in two hours; its coming always may");
        C(!GodPresenceMath.Speaks(200000, 0, 150000, true) && GodPresenceMath.Speaks(300000, 0, 150000, true), "the same person once in two days");
        C(GodPresenceMath.PersonCooldown >= 60000 && GodPresenceMath.StopsAPass <= 3 && GodPresenceMath.MomentSpeakers <= 4, "no spam: a person once a day, two at a pass, a few voices at a coming");

        // ================= A god in disguise =================

        C(GodPresenceMath.NoticesTell(1.25f, 1f, false, 5f) && !GodPresenceMath.NoticesTell(1f, 1f, false, 5f) && GodPresenceMath.NoticesTell(1.12f, 1f, true, 5f)
          && !GodPresenceMath.NoticesTell(1.12f, 1f, false, 5f), "a sharp eye notices, better at night");
        C(GodPresenceMath.NoticesTell(0.5f, 1.6f, false, 9f) && !GodPresenceMath.NoticesTell(2f, 2f, false, 11f), "a psychic gift notices; nobody from afar");
        C(GodPresenceMath.TellCooldown >= 60000 && GodPresenceMath.FollowEvery >= 2500 && GodPresenceMath.ShimmerDark > 0f && GodPresenceMath.ShimmerDark < 0.6f,
            "a tell remarked once a day, animals following now and then, the shimmer only in the dark");

        // ================= The trail =================

        var open = new GodPresenceMath.TrailCell { fertility = 1f };
        C(GodPresenceMath.TrailAt(GodTrail.Flowers, open, true) == TrailAct.Plant && GodPresenceMath.TrailAt(GodTrail.Flowers, open, false) == TrailAct.Fleck,
            "flowers on open soil, one step in three; a fleck the others");
        foreach (var (why, cell) in new (string, GodPresenceMath.TrailCell)[]
                 {
                     ("a floor", new GodPresenceMath.TrailCell { fertility = 1f, floor = true }), ("water", new GodPresenceMath.TrailCell { fertility = 1f, water = true }),
                     ("a roof", new GodPresenceMath.TrailCell { fertility = 1f, roofed = true }), ("a field", new GodPresenceMath.TrailCell { fertility = 1f, growZone = true }),
                     ("something on it", new GodPresenceMath.TrailCell { fertility = 1f, occupied = true }), ("a plant", new GodPresenceMath.TrailCell { fertility = 1f, plant = true }),
                     ("poor soil", new GodPresenceMath.TrailCell { fertility = 0.4f })
                 })
            C(GodPresenceMath.TrailAt(GodTrail.Flowers, cell, true) != TrailAct.Plant && GodPresenceMath.TrailAt(GodTrail.Grass, cell, true) != TrailAct.Plant, $"no flower on {why}");
        C(GodPresenceMath.TrailAt(GodTrail.Frost, open, true) == TrailAct.Snow && GodPresenceMath.TrailAt(GodTrail.Frost, new GodPresenceMath.TrailCell { roofed = true }, true) == TrailAct.Fleck
          && GodPresenceMath.TrailAt(GodTrail.Frost, new GodPresenceMath.TrailCell { snow = 0.8f }, true) == TrailAct.Fleck, "snow out of doors, not where it already lies deep");
        foreach (var t in new[] { GodTrail.Scorch, GodTrail.Ash, GodTrail.Water, GodTrail.Rain })
        {
            C(GodPresenceMath.TrailAt(t, open, true) == TrailAct.Filth && GodPresenceMath.TrailAt(t, new GodPresenceMath.TrailCell { home = true }, true) == TrailAct.Fleck
              && GodPresenceMath.TrailAt(t, new GodPresenceMath.TrailCell { floor = true }, true) == TrailAct.Fleck, $"{t}: filth only outside the home area, never on a floor");
        }
        C(GodPresenceMath.TrailAt(GodTrail.Sparks, open, true) == TrailAct.Fleck && GodPresenceMath.TrailAt(GodTrail.Light, open, true) == TrailAct.Fleck
          && GodPresenceMath.TrailAt(GodTrail.Shadow, open, true) == TrailAct.Fleck && GodPresenceMath.TrailAt(GodTrail.None, open, true) == TrailAct.Nothing,
            "sparks, light and shadow only fade");
        C(GodPresenceMath.LastingEvery == 3 && GodPresenceMath.LastingADay <= 60, "what lasts: a step in three, forty a day at most");

        // ================= The mark =================

        C(GodPresenceMath.MarkFor(PresenceKind.Wrath, GodMark.Flowers, GodMark.None) == GodMark.Scorch && GodPresenceMath.MarkFor(PresenceKind.Wrath, GodMark.Flowers, GodMark.Frost) == GodMark.Frost,
            "a wrath scorches unless its look says otherwise");
        C(GodPresenceMath.MarkFor(PresenceKind.Blessing, GodMark.Tree, GodMark.Ash) == GodMark.Tree && GodPresenceMath.MarkFor(PresenceKind.Appear, GodMark.Spring, GodMark.Ash) == GodMark.Spring,
            "a blessing and a coming leave the look's mark");
        C(GodPresenceMath.MarkFor(PresenceKind.Act, GodMark.Tree, GodMark.Ash) == GodMark.None && GodPresenceMath.MarkFor(PresenceKind.Omen, GodMark.Tree, GodMark.Ash) == GodMark.None,
            "an act and an omen leave none");
        var ring = GodPresenceMath.Ring(3);
        C(ring.Count >= 12 && ring.All(c => Math.Abs(Math.Sqrt(c.dx * c.dx + c.dz * c.dz) - 3) <= 0.5) && ring.Distinct().Count() == ring.Count, "a ring of cells at its distance, each once");
        C(GodPresenceMath.Ring(0).SequenceEqual(new[] { (0, 0) }), "a ring of none is the place itself");
        C(GodPresenceMath.MarkRadius(GodMark.Scorch) > GodPresenceMath.MarkRadius(GodMark.Flowers) && GodPresenceMath.MarkRadius(GodMark.Tree) == 0, "a scorch wider than flowers; a tree at the place");
        C(GodPresenceMath.SpiritAngle("Mote_SpiritWolf_East") == 90f && GodPresenceMath.SpiritAngle("Mote_BloodWolfWest") == 270f && GodPresenceMath.SpiritAngle("Mote_SpiritWolf_North") == 0f,
            "spirits run the way their picture faces");

        // ================= The looks =================

        var active = Catalogue(root);
        C(active.Count > 10000, $"the player's mods' defs are read ({active.Count})");
        var looksDoc = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodLooks.xml"));
        var looks = looksDoc.Root!.Elements("ColonyCouncil.GodLookDef").ToList();
        C(looks.Count >= 14, $"a look for each domain and the age variants ({looks.Count})");
        int Tech(string s) => s switch
        {
            "Undefined" => 0, "Animal" => 1, "Neolithic" => 2, "Medieval" => 3, "Industrial" => 4, "Spacer" => 5, "Ultra" => 6, "Archotech" => 7, _ => -1
        };
        var table = looks.Select(l => (l.Element("domain")!.Value, Tech(l.Element("minTech")?.Value ?? "Undefined"), Tech(l.Element("maxTech")?.Value ?? "Archotech"))).ToList();
        foreach (var d in GodPresenceMath.Domains)
        {
            C(looks.Any(l => l.Element("defName")!.Value == GodPresenceMath.LookDefName(d)), $"{d} has its look, {GodPresenceMath.LookDefName(d)}");
            for (int t = 2; t <= 7; t++)
            {
                int i = GodPresenceMath.Choose(table, d, t);
                C(i >= 0 && table[i].Item2 <= t && table[i].Item3 >= t, $"{d} has a look that holds age {t}");
            }
        }
        C(GodPresenceMath.Choose(table, "Machine", 3) != GodPresenceMath.Choose(table, "Machine", 5) && GodPresenceMath.Choose(table, "War", 4) != GodPresenceMath.Choose(table, "War", 5)
          && GodPresenceMath.Choose(table, "Craft", 3) != GodPresenceMath.Choose(table, "Craft", 6), "the Machine, the War and the Craft take another form in another age");
        var kinds = new Dictionary<string, string[]>
        {
            ["body"] = new[] { "ThingDef" }, ["head"] = new[] { "ThingDef" }, ["animals"] = new[] { "ThingDef" }, ["motes"] = new[] { "ThingDef", "FleckDef" },
            ["circle"] = new[] { "ThingDef", "FleckDef" }, ["spirits"] = new[] { "ThingDef", "FleckDef" }, ["flash"] = new[] { "EffecterDef" }, ["sting"] = new[] { "SoundDef" },
            ["wrathSting"] = new[] { "SoundDef" }, ["omen"] = new[] { "SoundDef" }, ["depart"] = new[] { "SoundDef" }, ["hum"] = new[] { "SoundDef" },
            ["tracks"] = new[] { "SongDef" }, ["darkTracks"] = new[] { "SongDef" }
        };
        int named = 0, magic = 0;
        foreach (var l in looks)
        {
            string dn = l.Element("defName")!.Value;
            C(GodPresenceMath.IsDomain(l.Element("domain")?.Value), $"{dn}: its domain is one of the fourteen");
            C(!string.IsNullOrEmpty(l.Element("sampleName")?.Value) && (l.Element("sampleName")!.Value.StartsWith("the ")), $"{dn}: a name for a god nobody has named");
            float size = float.Parse(l.Element("size")?.Value ?? "1.25", System.Globalization.CultureInfo.InvariantCulture);
            C(size > 1.05f && size <= GodPresenceMath.MaxSize, $"{dn}: drawn larger than its kind ({size})");
            foreach (var f in new[] { "colorA", "colorB", "glowColor", "skin", "hair", "eyes", "animalTint", "sky" })
                C(Regex.IsMatch(l.Element(f)?.Value ?? "", @"^\(\s*[0-9.]+\s*,\s*[0-9.]+\s*,\s*[0-9.]+\s*\)$"), $"{dn}: its {f}, three numbers from 0 to 1");
            foreach (var f in new[] { "body", "head", "motes", "flash", "sting", "omen", "hum", "tracks", "animals" })
                C((l.Element(f)?.Elements("li").Count() ?? 0) > 0, $"{dn}: something in {f}");
            C(Enum.TryParse<GodTrail>(l.Element("trail")?.Value, out _) && Enum.TryParse<GodMark>(l.Element("mark")?.Value, out _)
              && Enum.TryParse<GodMark>(l.Element("wrathMark")?.Value ?? "Scorch", out _), $"{dn}: its trail and marks are known");
            foreach (var (field, types) in kinds)
                foreach (var li in l.Element(field)?.Elements("li") ?? Enumerable.Empty<XElement>())
                {
                    string name = li.Value.Trim();
                    named++;
                    C(active.TryGetValue(name, out var has) && types.Any(has.Contains), $"{dn}: {field} '{name}' is in the player's mods as {string.Join(" or ", types)}");
                    if (name.StartsWith("TM_") || name.StartsWith("Mote_") || new[] { "ArchMageRobe", "DarkRobe", "DemonLordCloak", "RoyalHelm", "ParagonHelm", "HoodOfMadness", "ItemEnchanted" }.Contains(name))
                        magic++;
                }
            // The game's own fallback is always there: every look has a vanilla robe and a sting the base game or a DLC has.
            C(l.Element("body")!.Elements("li").Any(li => Mod(active, li.Value).StartsWith("ludeon")), $"{dn}: a robe of the game's own to fall back on");
        }
        C(named >= 250, $"the looks name the player's things ({named})");
        C(magic >= 60, $"A RimWorld of Magic's motes, effecters, sounds and robes are used throughout ({magic})");
        C(looks.Select(l => l.Element("sampleName")!.Value).Distinct().Count() == looks.Count, "every look's god has a name of its own");

        // ================= The hediff, jobs, presence, card and motes =================

        var presDoc = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodPresence.xml")).Root!;
        var hediff = presDoc.Elements("HediffDef").FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_GodAvatar");
        C(hediff != null && hediff.Descendants("li").Any(e => (string)e.Attribute("Class") == "ColonyCouncil.HediffCompProperties_GodAvatar"), "the avatar's hediff with its comp");
        var stages = hediff?.Element("stages")?.Elements("li").ToList() ?? new List<XElement>();
        C(stages.Count == 2 && stages[0].Element("becomeVisible")?.Value == "false", "in disguise nothing shows on its health page");
        C(stages.Count == 2 && Math.Abs(float.Parse(stages[1].Element("statFactors")?.Element("MoveSpeed")?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture) - GodPresenceMath.StatelyWalk) < 0.001f,
            "shown, its walk is stately (the factor is GodPresenceMath.StatelyWalk)");
        C(float.Parse(stages[1].Element("minSeverity")!.Value, System.Globalization.CultureInfo.InvariantCulture) is > 0.5f and <= 1f, "the shown stage begins between the two severities the code sets");
        foreach (var job in new[] { "Rimfolk_BeholdGod", "Rimfolk_PrayToGod", "Rimfolk_LieBeforeGod" })
            C(presDoc.Elements("JobDef").Any(e => e.Element("defName")?.Value == job && e.Element("driverClass")?.Value == "ColonyCouncil.JobDriver_BeholdGod"
                                                   && e.Element("casualInterruptible")?.Value == "true"), $"{job}: a short job anyone can be called from");
        C(presDoc.Elements("GameConditionDef").Any(e => e.Element("defName")?.Value == "Rimfolk_GodPresence" && e.Element("conditionClass")?.Value == "ColonyCouncil.GameCondition_GodPresence"),
            "the presence on the sky");
        var letterDef = presDoc.Elements("LetterDef").FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_GodCard");
        C(letterDef?.Element("letterClass")?.Value == "ColonyCouncil.GodLetter" && letterDef.Element("pauseMode")?.Value == "AnyLetter",
            "the card's letter: its own kind, pausing only for those who pause for every letter (so the track plays on)");
        C(active.ContainsKey(letterDef?.Element("arriveSound")?.Value ?? "-"), "the card's bell is the game's");
        var light = presDoc.Elements("ThingDef").FirstOrDefault(e => e.Element("defName")?.Value == "Rimfolk_GodLight");
        C(light?.Element("isSaveable")?.Value == "false" && light.Element("selectable")?.Value == "false" && light.Descendants("li").Any(e => (string)e.Attribute("Class") == "CompProperties_Glower"),
            "the light that follows a god: the game's glower, never saved or selected");
        foreach (var fleck in new[] { "Rimfolk_GodMote", "Rimfolk_GodSpark", "Rimfolk_GodRays" })
        {
            var f = presDoc.Elements("FleckDef").FirstOrDefault(e => e.Element("defName")?.Value == fleck);
            string tex = f?.Element("graphicData")?.Element("texPath")?.Value ?? "";
            C(f != null && File.Exists(Path.Combine(root, "Textures", tex + ".png")) && f.Element("graphicData")?.Element("shaderType")?.Value == "MoteGlow", $"{fleck}: its texture, glowing");
        }

        // ================= The textures and their tool =================

        string tool = File.Exists(Path.Combine(root, "Tools", "make_god_auras.py")) ? File.ReadAllText(Path.Combine(root, "Tools", "make_god_auras.py")) : "";
        foreach (var t in new[] { "Halo", "Glow", "Ring", "RingRunes", "Rays", "Mote", "Spark" })
        {
            string png = Path.Combine(root, "Textures", "ColonyCouncil", "Gods", "Aura", t + ".png");
            C(File.Exists(png) && new FileInfo(png).Length > 500 && tool.Contains(t + ".png"), $"{t}.png drawn by Tools/make_god_auras.py");
        }
        string aura = Code(root, "GodAura.cs");
        C(Regex.Matches(aura, @"ContentFinder<Texture2D>\.Get\(Dir \+ ""(\w+)""\)").Cast<Match>().All(m => File.Exists(Path.Combine(root, "Textures", "ColonyCouncil", "Gods", "Aura", m.Groups[1].Value + ".png"))),
            "every texture the aura loads exists");

        // ================= The lines said aloud =================

        var linesDoc = XDocument.Load(Path.Combine(root, "Defs", "GodDefs", "Rimfolk_GodPresence_Lines.xml")).Root!;
        string reactions = Code(root, "GodReactions.cs");
        var knownTags = new HashSet<string>(GodPresenceMath.Domains.Select(d => d.ToLowerInvariant()))
            { "named", "night", "appear", "act", "reveal", "depart", "blessing", "wrath", "omen", "snow", "animals", "animal", "psychic" };
        foreach (var pool in new[] { "Rimfolk_Pool_GodAwe", "Rimfolk_Pool_GodMoment", "Rimfolk_Pool_GodPrayer", "Rimfolk_Pool_GodTell", "Rimfolk_Pool_GodOmen" })
        {
            var el = linesDoc.Elements("ColonyCouncil.FlavorPoolDef").FirstOrDefault(e => e.Element("defName")?.Value == pool);
            var lines = el?.Descendants("li").Select(li => li.Value).ToList() ?? new List<string>();
            C(lines.Count >= 10 && reactions.Contains("\"" + pool + "\""), $"{pool}: its lines, and the code says them");
            C(lines.Count(l => !l.StartsWith("[")) >= 3, $"{pool}: lines that fit every god, so a line always fits");
            foreach (var line in lines)
            {
                var m = Regex.Match(line, @"^\[([^\]]+)\]\s*(.*)$");
                var tags = m.Success ? m.Groups[1].Value.Split(',').Select(t => t.Trim().TrimStart('!')).ToList() : new List<string>();
                string body = m.Success ? m.Groups[2].Value : line;
                C(tags.All(knownTags.Contains), $"{pool}: '{line}' has only known tags");
                C(body.Length <= 90 && !body.StartsWith("\"") && body.Count(ch => ch == '!') <= 1 && !body.Contains('—'), $"{pool}: '{body}' short, plain, no quotes or dashes");
                C(Regex.Matches(body, @"[.?!](\s|$)").Count == 1 && Regex.IsMatch(body, @"[.?!]$"), $"{pool}: '{body}' is one sentence");
                C(!body.Contains("{NAME}") || tags.Contains("named"), $"{pool}: '{body}' names the god only where it is named");
                C(!Regex.IsMatch(body, @"\b(colour|grey|traveller|armour|honour)\b"), $"{pool}: '{body}' in American spelling");
            }
        }

        // ================= The words =================

        var keyed = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "Languages", "English", "Keyed"), "*.xml"))
            foreach (var e in XDocument.Load(file).Root!.Elements())
                keyed[e.Name.LocalName] = e.Value;
        foreach (PresenceKind k in Enum.GetValues(typeof(PresenceKind)))
            C(keyed.ContainsKey("CC_GodP_Card_Title_" + k) && keyed.ContainsKey("CC_GodP_Card_Text_" + k) && keyed.ContainsKey("CC_GodP_Card_Text_" + k + "_Veiled"),
                $"{k}: the card's title and words, plain and veiled");
        foreach (var k in keyed.Keys.Where(k => k.StartsWith("CC_GodP_Card_Text_") && k.EndsWith("_Veiled")))
            C(keyed[k].Contains("ome say") || keyed[k].Contains("perhaps"), $"{k}: veiled, it says only what some say");
        string all = string.Join("\n", Directory.GetFiles(Path.Combine(root, "Source", "ColonyCouncil", "Gods", "Presence"), "*.cs").Select(File.ReadAllText));
        // The code without its comments (which may name the core's seams they will be wired to).
        string bare = Regex.Replace(all, @"//[^\n]*", "");
        foreach (Match m in Regex.Matches(all, @"""(CC_[A-Za-z_]+)""\.Translate"))
            C(keyed.ContainsKey(m.Groups[1].Value), $"the key {m.Groups[1].Value} exists");
        foreach (var k in new[] { "CC_GodP_Job_Behold", "CC_GodP_Job_Pray", "CC_GodP_Job_Lie" })
            C(keyed.ContainsKey(k) && keyed.ContainsKey(k + "Place"), $"{k}: the job's words with a named god and without");
        foreach (var kv in keyed.Where(kv => kv.Key.StartsWith("CC_GodP_") || kv.Key.StartsWith("CC_Settings_God") || kv.Key.StartsWith("CC_SetNote_God")))
            C(!kv.Value.Contains('—') && !kv.Value.Contains('!') && !Regex.IsMatch(kv.Value, @"\b(colour|colours|grey|traveller|travellers)\b"),
                $"{kv.Key}: no dashes or exclamations, American spelling");

        // ================= The settings =================

        string settings = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "CouncilSettings.cs"));
        string catalog = File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "SettingsCatalog.cs"));
        foreach (var s in new[] { "godAura", "godMusic" })
        {
            C(settings.Contains("public bool " + s + " = true;") && settings.Contains("Scribe_Values.Look(ref " + s + ", \"" + s + "\", true);"), $"{s}: on by default, kept");
            C(catalog.Contains("Toggle(\"" + s + "\""), $"{s}: in the settings");
        }
        C(keyed.ContainsKey("CC_Settings_GodAura_Tip") && keyed.ContainsKey("CC_Settings_GodMusic_Tip") && keyed["CC_Settings_GodMusic_Tip"].Contains("Sounds/Rimfolk/Gods/"),
            "the music's tip says where the player's own tracks go");
        C(Code(root, "GodAura.cs").Contains("S?.godAura ?? true") && Code(root, "GodPresence.cs").Contains("S?.godMusic ?? true"), "the aura and the music each read their setting");
        C(File.ReadAllText(Path.Combine(root, "Source", "ColonyCouncil", "UI", "FramedWindowsMath.cs")).Contains("new FrameKey(\"GodCard\""), "the card has its frame key");

        // ================= The seams =================

        string presence = Code(root, "GodPresence.cs"), avatar = Code(root, "GodAvatar.cs"), patches = Code(root, "GodPresencePatches.cs"), card = Code(root, "GodCard.cs");
        C(Regex.IsMatch(presence, @"public static bool Manifest\(GodLookDef look, string godName, string epithet, Map map, IntVec3 at, PresenceKind kind, Pawn avatar, string title, string text,\s*bool veiled, Color\? colorA = null, Color\? colorB = null\)"),
            "GodPresence.Manifest: the moment the core's seam calls");
        C(Regex.IsMatch(avatar, @"public static bool Make\(Pawn p, GodLookDef look, string name, bool disguised, Color\? colorA = null, Color\? colorB = null\)")
          && avatar.Contains("public static bool Reveal(Pawn p,") && avatar.Contains("public static void Unmake(Pawn p)"), "GodAvatar.Make, Reveal and Unmake: the avatar the core's seam makes");
        C(Code(root, "GodLookDef.cs").Contains("public static GodLookDef For(string domain, TechLevel age)") && Code(root, "GodLookDef.cs").Contains("public static GodLookDef Named(string defName)"),
            "GodLooks.For and Named: a domain's look in an age, or a look by name");
        C(Enum.GetNames(typeof(PresenceKind)).SequenceEqual(new[] { "Appear", "Act", "Reveal", "Depart", "Blessing", "Wrath", "Omen" }), "the seven kinds, by the design's names");
        C(!bare.Contains("GodPresenceSeam") && !bare.Contains("GodLookSeam") && !bare.Contains("GodDomain"), "the presence builds on its own types: nothing of the core's needed");
        C(!all.Contains("TorannMagic") && !Regex.IsMatch(all, @"DefDatabase<\w+>\.GetNamed\(") && !all.Contains("DefOf.Mote_"), "A RimWorld of Magic and every other mod reached by name only, softly");
        C(Regex.Matches(patches, @"\[HarmonyPatch\(").Count >= 10 && Regex.Matches(patches, @"GodAvatar\.Any").Count >= 9, "every hook looks first whether any god walks");
        var drawPatch = Regex.Match(patches, @"class Patch_GodDrawParms.*?\n    }", RegexOptions.Singleline).Value;
        C(drawPatch.Length > 0 && !drawPatch.Contains("Time.") && !drawPatch.Contains("Find.") && avatar.Contains("public float DrawSize =>") && !Regex.Match(avatar, @"public float DrawSize =>[^;]*;").Value.Contains("Time."),
            "the drawing hook runs on the game's worker threads and touches nothing of the main thread's (the size is worked out each frame before)");
        C(!all.Contains("MoteMaker.ThrowText") && !Regex.IsMatch(all.Replace(Code(root, "GodPresenceDebug.cs"), ""), @"Messages\.Message\("), "speech through Speech.Say, messages through the teller (the dev actions answer directly)");
        C(reactions.Contains("Speech.Say(p, line)") && card.Contains("Tell.Letter(") && presence.Contains("Tell.Message("), "lines said aloud, the card and an omen's line through the one voice");
        // The names the code reaches by itself (motes, filth, plants, chunks, a sting, the devout's traits): each in the player's mods.
        var codeNames = Regex.Matches(bare, @"""((?:Mote_|Filth_|Plant_|Chunk)\w+|GameStartSting)""").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
        C(codeNames.Count >= 15, $"the code's own names found ({codeNames.Count})");
        foreach (var name in codeNames) C(active.ContainsKey(name), $"the code's '{name}' is in the player's mods");
        foreach (Match m in Regex.Matches(reactions, @"t\.def\.defName == ""(\w+)"""))
            C(active.TryGetValue(m.Groups[1].Value, out var tt) && tt.Contains("TraitDef"), $"the devout's trait '{m.Groups[1].Value}' is in the player's mods");
        C(Code(root, "GodSounds.cs").Contains("GetNamedSilentFail") && Code(root, "GodSounds.cs").Contains("OwnFolder") && Code(root, "GodSounds.cs").Contains("\"Rimfolk/Gods/\""),
            "sounds and songs looked up softly, the player's own folder read");
        C(avatar.Contains("IsHashIntervalTick(15)") && avatar.Contains("IsHashIntervalTick(30)") && avatar.Contains("IsHashIntervalTick(250)"), "a god's work is spread over ticks, for the gods only");
        C(aura.Contains("if (!On || !GodAvatar.Any || map == null) return;"), "the aura costs one check with no god about");
        C(Code(root, "GodPresenceDebug.cs").Split("[DebugAction(\"Rimfolk\", \"Gods: ").Length - 1 >= 8, "the dev actions: make, disguise, reveal, unmake, an animal, each moment, and the logs");
        return n;
    }

    private static Dictionary<string, HashSet<string>> catalogue;

    /// <summary>Every def of the player's enabled mods (docs/player-mods/defs.tsv.gz, the active rows), by name: its types.</summary>
    private static Dictionary<string, HashSet<string>> Catalogue(string root)
    {
        if (catalogue != null) return catalogue;
        catalogue = new Dictionary<string, HashSet<string>>();
        mods = new Dictionary<string, string>();
        using var gz = new GZipStream(File.OpenRead(Path.Combine(root, "docs", "player-mods", "defs.tsv.gz")), CompressionMode.Decompress);
        using var r = new StreamReader(gz);
        string line;
        while ((line = r.ReadLine()) != null)
        {
            var t = line.Split('\t');
            if (t.Length < 4 || t[0] == "type" || t[3] != "yes") continue;
            if (!catalogue.TryGetValue(t[1], out var set)) catalogue[t[1]] = set = new HashSet<string>();
            set.Add(t[0]);
            mods[t[1]] = t[2];
        }
        return catalogue;
    }

    private static Dictionary<string, string> mods;

    private static string Mod(Dictionary<string, HashSet<string>> active, string defName) => mods != null && mods.TryGetValue(defName.Trim(), out var m) ? m : "";

    private static string Code(string root, string file)
    {
        string p = Path.Combine(root, "Source", "ColonyCouncil", "Gods", "Presence", file);
        return File.Exists(p) ? File.ReadAllText(p) : "";
    }

    private static string Root()
    {
        string root = Directory.GetCurrentDirectory();
        while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
        return root;
    }
}
