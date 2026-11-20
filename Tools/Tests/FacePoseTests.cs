using System.Text.RegularExpressions;
using ColonyCouncil;

/// <summary>
/// Portraits, phase 2 (the player's words, 20 November 2026: "also angles and kind of positions that are also different
/// from the one that you have at the top ... you see these characters also a bit moving around a bit and being in a
/// position, a different one that gives them kind of character", and "At least make them also, have the other
/// direction"): the pose by who someone is and how they are, kept steady; where the head sits for each; two people
/// facing each other; breathing, the sway and the turns; the test of a head drawn apart; the dead's grey; and the seams
/// (the game's flags, the hooks, which portraits move, the settings).
/// </summary>
public static class FacePoseTests
{
    public static int Run(string root, Action<bool, string> check)
    {
        int n = 0;
        void C(bool ok, string what)
        {
            n++;
            check(ok, "portraits, phase 2: " + what);
        }
        string src = Path.Combine(root, "Source", "ColonyCouncil");
        string Read(string rel) => File.Exists(Path.Combine(src, rel)) ? File.ReadAllText(Path.Combine(src, rel)) : "";

        // ---------------- The pose, by who they are and how they are ----------------
        var plain = new BearingFacts { mood = 0.6f, minorBreak = 0.35f };
        FaceBearing B(Func<BearingFacts, BearingFacts> set, FaceBearing current = FaceBearing.Plain) => FacePoseMath.Bearing(set(plain), current);
        C(B(f => f) == FaceBearing.Plain, "nothing to show: plain");
        C(B(f => { f.dead = true; f.captive = true; f.angryState = true; return f; }) == FaceBearing.Dead, "the dead first");
        C(B(f => { f.captive = true; f.angryState = true; f.proudStanding = true; return f; }) == FaceBearing.Captive, "a prisoner before a break or a seat");
        C(B(f => { f.angryState = true; f.sadState = true; return f; }) == FaceBearing.Angry, "an angry break before sadness");
        C(B(f => { f.sadState = true; f.proudStanding = true; return f; }) == FaceBearing.Low && B(f => { f.grieving = true; f.proudStanding = true; return f; }) == FaceBearing.Low,
            "a sad break or grief before a seat");
        C(B(f => { f.mood = 0.2f; f.quarrel = true; return f; }) == FaceBearing.Low, "a low mood before a quarrel");
        C(B(f => { f.quarrel = true; f.proudStanding = true; return f; }) == FaceBearing.Angry, "a fresh quarrel before a seat");
        C(B(f => { f.proudStanding = true; f.shyTrait = true; return f; }) == FaceBearing.Proud, "a seat, a title or the head of a people before a shy trait");
        C(B(f => { f.shyTrait = true; f.proudTrait = true; f.old = true; return f; }) == FaceBearing.Shy, "a shy trait before a proud one");
        C(B(f => { f.proudTrait = true; f.old = true; return f; }) == FaceBearing.Proud, "a proud trait before old age");
        C(B(f => { f.old = true; return f; }) == FaceBearing.Old, "old age last");
        C(B(f => { f.mood = -1f; return f; }) == FaceBearing.Plain, "no mood (a visitor without needs): not low");
        // The mood's line, with a margin before it lifts.
        C(B(f => { f.mood = 0.36f; return f; }) == FaceBearing.Plain && B(f => { f.mood = 0.34f; return f; }) == FaceBearing.Low, "low under the minor break line");
        C(B(f => { f.mood = 0.38f; return f; }, FaceBearing.Low) == FaceBearing.Low && B(f => { f.mood = 0.41f; return f; }, FaceBearing.Low) == FaceBearing.Plain,
            "low lifts only past the line and its margin");
        C(FacePoseMath.Old(65f, 80f) && !FacePoseMath.Old(60f, 80f) && FacePoseMath.Old(500f, 600f) && !FacePoseMath.Old(10f, 0f), "old: four fifths of the race's life (64 for a human)");
        // Kept steady.
        C(FacePoseMath.FromState(new BearingFacts { dead = true }, FaceBearing.Dead) && FacePoseMath.FromState(new BearingFacts { captive = true }, FaceBearing.Captive)
          && FacePoseMath.FromState(new BearingFacts { angryState = true }, FaceBearing.Angry) && !FacePoseMath.FromState(new BearingFacts { quarrel = true }, FaceBearing.Angry)
          && FacePoseMath.FromState(new BearingFacts { sadState = true }, FaceBearing.Low) && !FacePoseMath.FromState(new BearingFacts { grieving = true }, FaceBearing.Low),
            "death, captivity and a break are states of the moment; a quarrel, grief and a mood are not");
        C(FacePoseMath.TakeNow(true, false, 0) && FacePoseMath.TakeNow(false, true, 0) && !FacePoseMath.TakeNow(false, false, 2499) && FacePoseMath.TakeNow(false, false, 2500),
            "a state shows at once (and its end); anything else after an hour of the game held");
        // A mood wavering at the line for ten days, looked at every two seconds of play: the pose changes at most once an hour.
        {
            var cur = FaceBearing.Plain;
            bool curState = false;
            int since = 0, changes = 0, ticks = 600000;
            for (int t = 0; t < ticks; t += 120)
            {
                var f = plain;
                f.mood = 0.35f + 0.03f * (float)Math.Sin(t / 300.0);
                var next = FacePoseMath.Bearing(f, cur);
                bool st = FacePoseMath.FromState(f, next);
                if (next != cur && FacePoseMath.TakeNow(st, curState, t - since))
                {
                    cur = next;
                    curState = st;
                    since = t;
                    changes++;
                }
            }
            C(changes <= ticks / FacePoseMath.HoldTicks + 1, $"a wavering mood doesn't flicker ({changes} changes in ten days)");
        }

        // ---------------- The way they face ----------------
        C(FacePoseMath.PairTurn(0, 2) == FaceTurn.Right && FacePoseMath.PairTurn(1, 2) == FaceTurn.Left, "two side by side face each other");
        C(FacePoseMath.PairTurn(0, 1) == FaceTurn.Front && Enumerable.Range(0, 3).All(i => FacePoseMath.PairTurn(i, 3) == FaceTurn.Front), "one alone, or three, face the front");
        C(FacePoseMath.TurnFor(FaceBearing.Plain, FaceTurn.Front, 7) == FaceTurn.Front && FacePoseMath.TurnFor(FaceBearing.Proud, FaceTurn.Front, 7) == FaceTurn.Front,
            "a single portrait faces the front");
        C(FacePoseMath.TurnFor(FaceBearing.Shy, FaceTurn.Front, 2) == FaceTurn.Right && FacePoseMath.TurnFor(FaceBearing.Shy, FaceTurn.Front, 3) == FaceTurn.Left,
            "the shy turned half away, each to a side of their own");
        C(FacePoseMath.TurnFor(FaceBearing.Shy, FaceTurn.Left, 2) == FaceTurn.Left && FacePoseMath.TurnFor(FaceBearing.Captive, FaceTurn.Right, 3) == FaceTurn.Right,
            "the way a place asks (two facing each other) comes first");
        C(FacePoseMath.TurnedTo(FaceTurn.Front, true) == FaceTurn.Right && FacePoseMath.TurnedTo(FaceTurn.Front, false) == FaceTurn.Left && FacePoseMath.TurnedTo(FaceTurn.Left, true) == FaceTurn.Front,
            "a turn now and then: from the front to a side, from a side to the front");

        // ---------------- Where the head sits ----------------
        var all = (FaceBearing[])Enum.GetValues(typeof(FaceBearing));
        foreach (var b in all)
        foreach (var t in new[] { FaceTurn.Front, FaceTurn.Right, FaceTurn.Left })
        foreach (int seed in new[] { 0, 1, 2, 3 })
        {
            var s = FacePoseMath.Shift(b, t, seed);
            C(Math.Abs(s.dx) <= 0.1f && Math.Abs(s.dy) <= 0.1f && Math.Abs(s.tilt) <= 10f && Math.Abs(s.lean) <= 4f, $"{b} {t}: small and readable ({s.dy}, {s.tilt}°, lean {s.lean}°)");
        }
        foreach (var t in new[] { FaceTurn.Front, FaceTurn.Right, FaceTurn.Left })
        {
            C(FacePoseMath.Shift(FaceBearing.Plain, t, 0).None && FacePoseMath.Shift(FaceBearing.Dead, t, 0).None, $"{t}: plain and the dead stand as drawn");
            C(FacePoseMath.Shift(FaceBearing.Proud, t, 0).dy > 0f, $"{t}: the proud hold the head up");
            foreach (var b in new[] { FaceBearing.Low, FaceBearing.Old, FaceBearing.Captive, FaceBearing.Shy })
                C(FacePoseMath.Shift(b, t, 0).dy < 0f, $"{t}: {b} hold the head down");
            C(FacePoseMath.Shift(FaceBearing.Captive, t, 0).dy <= all.Min(b => FacePoseMath.Shift(b, t, 0).dy), $"{t}: a prisoner's head lowest");
            C(FacePoseMath.Shift(FaceBearing.Angry, t, 0).lean != 0f, $"{t}: the angry lean");
        }
        foreach (var b in all)
        {
            var r = FacePoseMath.Shift(b, FaceTurn.Right, 0);
            var l = FacePoseMath.Shift(b, FaceTurn.Left, 0);
            C(r.tilt == -l.tilt && r.lean == -l.lean && r.dy == l.dy, $"{b}: facing left is facing right mirrored");
        }
        foreach (var b in new[] { FaceBearing.Low, FaceBearing.Captive, FaceBearing.Old, FaceBearing.Shy })
            C(FacePoseMath.Shift(b, FaceTurn.Right, 0).tilt > 0f, $"{b}: a nod forward in a side view (clockwise facing right)");
        C(FacePoseMath.Shift(FaceBearing.Proud, FaceTurn.Right, 0).tilt < 0f, "the proud lift the chin in a side view");
        C(FacePoseMath.Shift(FaceBearing.Low, FaceTurn.Front, 0).tilt == -FacePoseMath.Shift(FaceBearing.Low, FaceTurn.Front, 2).tilt, "the sad tip the head to a side of their own");
        C(FacePoseMath.Shift(FaceBearing.Angry, FaceTurn.Front, 0).lean != 0f && !FacePoseMath.Shift(FaceBearing.Angry, FaceTurn.Front, 0).None, "a lean is drawn on the whole picture");
        C(FacePoseMath.PoseFrom == 64f && FacePoseMath.NeckBelow > 0f && FacePoseMath.NeckBelow < 0.4f, "round portraits take a pose from 64 across (an event card's); the neck under the face");

        // ---------------- A little movement ----------------
        float minB = 9f, maxB = 0f, minS = 9f, maxS = -9f, turned = 0f, lastK = 0f, worst = 0f;
        int turns = 0;
        bool? lastSide = null;
        bool alternates = true, wasTurned = false;
        const float step = 1f / 60f;
        for (float t = 0f; t < 600f; t += step)
        {
            float b = FacePoseMath.Breath(t, 1234), s = FacePoseMath.Sway(t, 1234), k = FacePoseMath.Turned(t, 1234, out bool right);
            minB = Math.Min(minB, b);
            maxB = Math.Max(maxB, b);
            minS = Math.Min(minS, s);
            maxS = Math.Max(maxS, s);
            if (k > 0f) turned += step;
            worst = Math.Max(worst, Math.Abs(k - lastK));
            lastK = k;
            if (k > 0.5f && !wasTurned)
            {
                turns++;
                if (lastSide.HasValue && lastSide.Value == right) alternates = false;
                lastSide = right;
            }
            wasTurned = k > 0.5f;
        }
        C(minB >= 1f - 1e-5f && maxB <= 1f + FacePoseMath.BreathDepth + 1e-5f && maxB - minB > FacePoseMath.BreathDepth * 0.9f, $"breathing: the body a little taller and back ({minB:0.0000} to {maxB:0.0000})");
        C(maxS <= FacePoseMath.SwayDegrees + 1e-4f && minS >= -FacePoseMath.SwayDegrees - 1e-4f && maxS > 0.4f, "the sway: half a degree each way");
        C(turns >= 15 && turns <= 26, $"a turn every 24 to 36 seconds ({turns} in ten minutes)");
        C(turned / 600f > 0.05f && turned / 600f < 0.15f, $"turned about a tenth of the time ({turned / 600f:0.00})");
        C(worst <= step / FacePoseMath.TurnFade * 1.6f, $"each turn fades in and out, never a jump ({worst:0.000} a frame)");
        C(alternates, "the turns go to one side, then the other");
        foreach (int seed in new[] { -5, 0, 1, 99999, int.MaxValue })
        {
            float c = FacePoseMath.TurnCycle(seed);
            C(c >= FacePoseMath.TurnEveryLeast && c <= FacePoseMath.TurnEveryMost, $"seed {seed}: its own cycle ({c:0.0} s)");
            C(!float.IsNaN(FacePoseMath.Breath(3f, seed)) && !float.IsNaN(FacePoseMath.Sway(3f, seed)) && !float.IsNaN(FacePoseMath.Turned(3f, seed, out _)), $"seed {seed}: no NaN");
        }
        C(FacePoseMath.TurnCycle(1) != FacePoseMath.TurnCycle(2) || FacePoseMath.TurnCycle(2) != FacePoseMath.TurnCycle(3), "people don't all turn together");

        // ---------------- The head drawn apart: the test picture ----------------
        float[] Rows(params float[] r) => r;
        // 20 rows, 10 wide, the head at the top: the head picture's head, the body picture's body with nothing at the crown.
        var head = Rows(6, 8, 8, 7, 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var body = Rows(0, 0, 1, 2, 6, 8, 8, 8, 8, 8, 7, 7, 7, 6, 6, 6, 5, 5, 5, 5);
        C(FacePoseMath.SplitLooksRight(body, head, 10f), "a head and a body apart: right");
        C(FacePoseMath.SplitLooksRight(body.Reverse().ToArray(), head.Reverse().ToArray(), 10f), "the same with the rows the other way up: right");
        var headWithBody = (float[])head.Clone();
        for (int i = 14; i < 20; i++) headWithBody[i] = 5f;
        C(!FacePoseMath.SplitLooksRight(body, headWithBody, 10f), "the body in the head picture (no NoBody): whole");
        var bodyWithHair = (float[])body.Clone();
        bodyWithHair[0] = 6f;
        C(!FacePoseMath.SplitLooksRight(bodyWithHair, head, 10f), "hair or wings at the crown of the body picture: whole");
        C(!FacePoseMath.SplitLooksRight(body, new float[20], 10f), "an empty head picture: whole");
        C(!FacePoseMath.SplitLooksRight(new float[20], head, 10f), "no body at the feet: whole");
        C(!FacePoseMath.SplitLooksRight(body, head.Take(19).ToArray(), 10f) && !FacePoseMath.SplitLooksRight(null, head, 10f), "pictures that don't match: whole");
        C(FacePoseMath.SplitGiveUpAfter == 3, "after three people who didn't come apart, and none who did, none are drawn apart");

        // ---------------- The dead's grey ----------------
        C(FacePoseMath.GreyOf(255, 255, 255) == 217 && FacePoseMath.GreyOf(0, 0, 0) == 0 && FacePoseMath.GreyOf(255, 0, 0) < FacePoseMath.GreyOf(0, 255, 0),
            "grey by brightness as the eye sees it, a little dimmed");

        // ---------------- The seams ----------------
        string engine = Read("UI/Faces/FaceEngine.cs"), split = Read("UI/Faces/FaceEngineSplit.cs"), poses = Read("UI/Faces/FaceEnginePoses.cs"), kit = Read("UI/UiKit.cs");
        C(split.Contains("[HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.ParallelPreDraw))]") && split.Contains("[HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.Draw))]")
          && Regex.Matches(split, @"if \(only != null && parms\.pawn == only\) parms\.flags \|= PawnRenderFlags\.NoBody;").Count == 2,
            "the head alone: the game's NoBody flag, only for the pawn being drawn so, in both hooks");
        C(split.Contains("renderer.RenderPawn(p, body, at, cam.zoom, 0f, rot, false, Headgear, Clothes, true)") && split.Contains("HeadOnly = p;")
          && split.Contains("renderer.RenderPawn(p, head, at, cam.zoom, 0f, rot, true, Headgear, Clothes, true)") && split.Contains("HeadOnly = null;"),
            "the body: the game's own call without the head (HeadStump); the head: the same call with NoBody; both at one camera");
        C(split.Contains("FacePoseMath.SplitLooksRight(b, h, W)") && split.Contains("splitOff = true;"), "tested once for each pawn, given up for the session when none come apart");
        C(split.Contains("if (pair.dirty && now - pair.drawnAt >= RedrawEvery)") && split.Contains("now - kv.Value.lastUsed > LetGoAfter"),
            "drawn again only when the game marks or redraws the pawn, let go a second after last shown: no new picture each frame");
        C(split.Contains("AccessTools.Inner(typeof(PortraitsCache), \"PortraitParams\")") && split.Contains("\"RenderPortrait\"") && split.Contains("FaceEngine.Forget(pawn)"),
            "the expression comes as the game draws the portrait again, never forced");
        C(engine.Contains("if (!(Find.TickManager?.Paused ?? true)) clock += ") && engine.Contains("bool animate = moving && Moves && !dead;"),
            "the movement holds still while the game is paused, and only where asked, with the setting");
        C(engine.Contains("UI.RotateAroundPivot(lean, new Vector2(r.center.x, r.yMax))") && engine.Contains("FacePoseMath.Breath(clock, seed)") && engine.Contains("FacePoseMath.Turned(clock, seed, out right)"),
            "the lean and sway about the feet, the breath, the turn: transforms of the pictures already made");
        // Only the large portrait at the inspect pane and the colonist card move.
        var moving = new List<string>();
        foreach (var f in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
            if (Regex.IsMatch(File.ReadAllText(f), @"FaceEngine\.Draw\([^;]*,\s*true\)"))
                moving.Add(Path.GetRelativePath(src, f).Replace('\\', '/'));
        C(moving.OrderBy(x => x).SequenceEqual(new[] { "UI/ColonistCard/ColonistCardWindow.cs", "UI/Faces/InspectPortrait.cs" }), $"only the large portraits move ({string.Join(", ", moving)})");
        C(!Regex.IsMatch(kit, @"Clock|Breath|Sway") && kit.Contains("FaceEngine.PoseOf(p, turn, r.width >= FacePoseMath.PoseFrom)"), "the round portraits never move; a pose from 64 across");
        C(Read("UI/EventCards/EventCardWindow.cs").Contains("FacePoseMath.PairTurn(i, m.pictured.Count)"), "a letter's or a card's two people face each other");
        C(Read("UI/SocialPanel.cs").Contains("t.a, color, 1f, FaceTurn.Right") && Read("UI/SocialPanel.cs").Contains("t.b, color, 1f, FaceTurn.Left"), "a tie's two people face each other");
        C(Read("UI/EventCards/EventCardModel.cs").Contains("(!p.Dead || FaceEngine.PicturesDead(p))") && poses.Contains("CurRotDrawMode != RotDrawMode.Dessicated"),
            "the dead pictured grey in a letter, not once they are bones");
        C(poses.Contains("state.category == MentalStateCategory.Aggro") && poses.Contains("r.diedThought") && poses.Contains("OfficeTracker.HeldBy(p)")
          && poses.Contains("p.Faction?.leader == p") && poses.Contains("p.royalty?.MostSeniorTitle") && poses.Contains("p.IsPrisoner") && poses.Contains("RaceProps.lifeExpectancy"),
            "the facts from the game: a break's kind, the family's deaths by the game's relations, a seat, the head of a people, a title, captivity, the race's life");
        string settings = Read("CouncilSettings.cs");
        C(settings.Contains("public bool portraitPoses = true;") && settings.Contains("public bool portraitsMove = true;"), "the settings: poses on, movement on");
        var mv = SettingsCatalog.Find("portraitsMove");
        C(mv != null && mv.section == "CC_SetSec_Portraits" && SettingsCatalog.Find("portraitPoses")?.section == "CC_SetSec_Portraits", "both under Portraits");
        string keyed = File.ReadAllText(Path.Combine(root, "Languages", "English", "Keyed", "Rimfolk_Portraits.xml"));
        C(keyed.Contains("<CC_Settings_PortraitsMove>Portraits move a little</CC_Settings_PortraitsMove>") && keyed.Contains("<CC_Settings_PortraitPoses>"), "the words: \"Portraits move a little\"");
        return n;
    }
}
