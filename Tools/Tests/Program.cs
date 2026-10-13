using ColonyCouncil;

int failures = 0;
var reported = new HashSet<string>();
void Check(bool ok, string what)
{
    if (ok) return;
    failures++;
    // The same problem in several turns, styles or furnishings is reported once.
    string key = System.Text.RegularExpressions.Regex.Replace(what, @" turn \d( mirrored)?|\((best|fallback) furniture, style \d\)|\(-?\d+,-?\d+\)|facing \d|faces \d|needs \d", "");
    if (reported.Add(key) && reported.Count <= 300) Console.WriteLine("FAIL " + what);
}

// Four quarter turns are the identity; every rotated point stays inside the rotated rect.
for (int w = 1; w <= 9; w++)
for (int h = 1; h <= 9; h++)
for (int x = 0; x < w; x++)
for (int z = 0; z < h; z++)
{
    TemplateMath.RotatePoint(x, z, w, h, 4, out int rx, out int rz);
    Check(rx == x && rz == z, $"4 turns identity ({x},{z}) in {w}x{h}");
    for (int r = 0; r < 4; r++)
    {
        TemplateMath.RotatedSize(w, h, r, out int rw, out int rh);
        TemplateMath.RotatePoint(x, z, w, h, r, out rx, out rz);
        Check(rx >= 0 && rz >= 0 && rx < rw && rz < rh, $"in bounds r={r} ({x},{z}) {w}x{h} -> ({rx},{rz}) in {rw}x{rh}");
    }
}

// Rotation is a bijection: no two cells land on the same spot.
for (int r = 0; r < 4; r++)
{
    var seen = new HashSet<(int, int)>();
    for (int x = 0; x < 7; x++)
    for (int z = 0; z < 5; z++)
    {
        TemplateMath.RotatePoint(x, z, 7, 5, r, out int rx, out int rz);
        Check(seen.Add((rx, rz)), $"bijection r={r}");
    }
}

// One clockwise turn (viewed from above, +z north): the south wall becomes the west wall,
// and the north-east corner becomes the south-east corner.
TemplateMath.RotatePoint(3, 0, 7, 5, 1, out int dx, out int dz);
Check(dx == 0, $"south wall -> west wall, got ({dx},{dz})");
TemplateMath.RotatePoint(6, 4, 7, 5, 1, out int cx, out int cz);
Check(cx == 4 && cz == 0, $"NE corner -> SE corner, got ({cx},{cz})");

// Facings rotate the same way: North -> East -> South -> West.
Check(TemplateMath.RotateFacing(0, 1) == 1 && TemplateMath.RotateFacing(1, 1) == 2 && TemplateMath.RotateFacing(3, 1) == 0, "facing rotation");
Check(TemplateMath.RotateFacing(0, -1) == 3, "counter-clockwise facing");

// Direction vectors agree with facing rotation: a vector pointing north (+z) turns east (+x).
TemplateMath.RotatePoint(2, 2, 5, 5, 1, out int ax, out int az);
TemplateMath.RotatePoint(2, 3, 5, 5, 1, out int bx, out int bz);
Check(bx - ax == 1 && bz - az == 0, $"north vector -> east vector, got ({bx - ax},{bz - az})");

// Rect rotation matches rotating every cell.
for (int r = 0; r < 4; r++)
{
    TemplateMath.RotateRect(1, 2, 3, 2, 7, 6, r, out int mx, out int mz, out int sx, out int sz);
    var cells = new HashSet<(int, int)>();
    for (int x = 1; x < 4; x++)
    for (int z = 2; z < 4; z++)
    {
        TemplateMath.RotatePoint(x, z, 7, 6, r, out int rx, out int rz);
        cells.Add((rx, rz));
    }
    Check(cells.Count == sx * sz && cells.All(c => c.Item1 >= mx && c.Item1 < mx + sx && c.Item2 >= mz && c.Item2 < mz + sz), $"rect r={r}");
}

// Mirroring flips west and east, keeps the rect inside, and mirroring twice is the identity.
for (int x = 0; x < 7; x++)
for (int z = 0; z < 5; z++)
{
    TemplateMath.Transform(x, z, 7, 5, true, 0, out int mx, out int mz);
    Check(mx == 6 - x && mz == z, $"mirror ({x},{z})");
    TemplateMath.Transform(mx, mz, 7, 5, true, 0, out int bx2, out int bz2);
    Check(bx2 == x && bz2 == z, $"mirror twice ({x},{z})");
}
Check(TemplateMath.MirrorFacing(1) == 3 && TemplateMath.MirrorFacing(3) == 1 && TemplateMath.MirrorFacing(0) == 0 && TemplateMath.MirrorFacing(2) == 2, "mirror facing");

// Transformed rects match transforming every cell, for every rotation, mirrored or not.
foreach (bool mirror in new[] { false, true })
for (int r = 0; r < 4; r++)
{
    TemplateMath.TransformRect(1, 2, 3, 2, 7, 6, mirror, r, out int mx, out int mz, out int sx, out int sz);
    var cells = new HashSet<(int, int)>();
    for (int x = 1; x < 4; x++)
    for (int z = 2; z < 4; z++)
    {
        TemplateMath.Transform(x, z, 7, 6, mirror, r, out int rx, out int rz);
        cells.Add((rx, rz));
    }
    Check(cells.Count == sx * sz && cells.All(c => c.Item1 >= mx && c.Item1 < mx + sx && c.Item2 >= mz && c.Item2 < mz + sz), $"transform rect m={mirror} r={r}");
}

// A facing vector transforms like the cells do: an east-pointing vector, mirrored, points west.
{
    TemplateMath.Transform(2, 2, 5, 5, true, 0, out int ax2, out int az2);
    TemplateMath.Transform(3, 2, 5, 5, true, 0, out int bx3, out int bz3);
    Check(bx3 - ax2 == -1 && bz3 - az2 == 0 && TemplateMath.TransformFacing(1, true, 0) == 3, "mirrored east vector points west");
}

// ---------------- Strain (Heart/HeartMath.cs) ----------------

// Bands: steady 0-29, strained 30-54, worn 55-74, breaking 75-100.
Check(HeartMath.BandFor(0f) == StrainBand.Steady && HeartMath.BandFor(29.9f) == StrainBand.Steady, "steady band");
Check(HeartMath.BandFor(30f) == StrainBand.Strained && HeartMath.BandFor(54.9f) == StrainBand.Strained, "strained band");
Check(HeartMath.BandFor(55f) == StrainBand.Worn && HeartMath.BandFor(74.9f) == StrainBand.Worn, "worn band");
Check(HeartMath.BandFor(75f) == StrainBand.Breaking && HeartMath.BandFor(100f) == StrainBand.Breaking, "breaking band");
foreach (StrainBand b in Enum.GetValues(typeof(StrainBand)))
    Check(HeartMath.BandFor(HeartMath.BandStart(b)) == b, $"band start {b}");
Check(HeartMath.MoodOffset(StrainBand.Steady) == 0f && HeartMath.MoodOffset(StrainBand.Strained) == -3f
      && HeartMath.MoodOffset(StrainBand.Worn) == -5f && HeartMath.MoodOffset(StrainBand.Breaking) == -8f, "strain mood by band");
Check(HeartMath.WorkFactor(StrainBand.Strained) == 1f && Math.Abs(HeartMath.WorkFactor(StrainBand.Worn) - 0.95f) < 1e-5f
      && Math.Abs(HeartMath.WorkFactor(StrainBand.Breaking) - 0.9f) < 1e-5f, "work speed by band");
Check(HeartMath.Clamp(-5f) == 0f && HeartMath.Clamp(130f) == 100f, "strain stays within 0 to 100");

// Caps: one source can't pass its daily cap, and everything together can't pass 25 a day.
{
    float combat = 0f, total = 0f, added = 0f;
    for (int h = 0; h < 10; h++) added += HeartMath.Add(4f, 1f, 1f, ref combat, HeartMath.SourceCap(StrainSource.Combat), ref total, HeartMath.DailyCap);
    Check(Math.Abs(added - 12f) < 1e-4f && Math.Abs(combat - 12f) < 1e-4f, $"combat capped at 12 a day, got {added}");
    float loss = 0f;
    for (int i = 0; i < 10; i++) HeartMath.Add(8f, 1f, 1f, ref loss, HeartMath.SourceCap(StrainSource.Loss), ref total, HeartMath.DailyCap);
    Check(Math.Abs(total - HeartMath.DailyCap) < 1e-4f, $"daily cap of 25 across sources, got {total}");
    float pain = 0f;
    Check(HeartMath.Add(3f, 1f, 1f, ref pain, HeartMath.SourceCap(StrainSource.Pain), ref total, HeartMath.DailyCap) == 0f, "nothing more once the day is full");
    float slow = 0f, slowTotal = 0f;
    float half = 0f;
    for (int h = 0; h < 10; h++) half += HeartMath.Add(4f, 0.5f, 1f, ref slow, HeartMath.SourceCap(StrainSource.Combat), ref slowTotal, HeartMath.DailyCap);
    Check(Math.Abs(half - 6f) < 1e-4f, $"at 50% strength the cap halves too, got {half}");
    float none = 0f, noneTotal = 0f;
    Check(HeartMath.Add(-3f, 1f, 1f, ref none, 5f, ref noneTotal, 25f) == 0f, "nothing negative goes through Add");
}

// Traits: iron will < steadfast < none < nervous < volatile; psychopaths take nothing from deaths, the bloodthirsty
// little from fighting, masochists nothing from pain, undergrounders nothing from the dark.
{
    float iron = HeartMath.TraitFactor(StrainSource.Loss, StrainTraits.IronWilled);
    float steady = HeartMath.TraitFactor(StrainSource.Loss, StrainTraits.Steadfast);
    float plain = HeartMath.TraitFactor(StrainSource.Loss, StrainTraits.None);
    float nervous = HeartMath.TraitFactor(StrainSource.Loss, StrainTraits.Nervous);
    float vol = HeartMath.TraitFactor(StrainSource.Loss, StrainTraits.Volatile);
    Check(iron < steady && steady < plain && plain < nervous && nervous < vol, $"nerves order {iron} {steady} {plain} {nervous} {vol}");
    Check(plain == 1f, "no traits, no change");
    Check(HeartMath.TraitFactor(StrainSource.Loss, StrainTraits.Psychopath) == 0f && HeartMath.TraitFactor(StrainSource.Grief, StrainTraits.Psychopath) == 0f, "psychopaths don't feel losses");
    Check(HeartMath.TraitFactor(StrainSource.Combat, StrainTraits.Psychopath) == 1f, "psychopaths still feel fighting");
    Check(HeartMath.TraitFactor(StrainSource.Combat, StrainTraits.Bloodlust) <= 0.35f, "bloodlust takes little from combat");
    Check(HeartMath.TraitFactor(StrainSource.Pain, StrainTraits.Masochist) == 0f, "masochists take nothing from pain");
    Check(HeartMath.TraitFactor(StrainSource.Dark, StrainTraits.Undergrounder) == 0f, "undergrounders take nothing from the dark");
    Check(HeartMath.TraitFactor(StrainSource.Pain, StrainTraits.Wimp) > 1f && HeartMath.TraitFactor(StrainSource.BadSleep, StrainTraits.Ascetic) < 1f, "wimps and ascetics");
    Check(HeartMath.ReliefTraitFactor(StrainTraits.Sanguine) > 1f && HeartMath.ReliefTraitFactor(StrainTraits.Depressive) < 1f, "relief by natural mood");
    float zero = 0f, zeroTotal = 0f;
    Check(HeartMath.Add(5f, 1f, HeartMath.TraitFactor(StrainSource.Loss, StrainTraits.Psychopath), ref zero, 14f, ref zeroTotal, 25f) == 0f, "a psychopath adds nothing from a death");
}

// Episodes: none below 85, roughly one within a day or two at that level, a little likelier at 100.
Check(HeartMath.EpisodeChancePerHour(84.9f) == 0f, "no episode below 85");
{
    float at85 = HeartMath.EpisodeChancePerHour(85f), at100 = HeartMath.EpisodeChancePerHour(100f);
    Check(at85 > 0f && at100 > at85 && 1f / at85 <= 48f && 1f / at100 >= 12f, $"episode odds {1f / at85}h to {1f / at100}h");
}

// Feelings: share and nudges.
Check(HeartMath.Share(0f) == 0f && HeartMath.Share(HeartMath.FullIntensity) == 1f && HeartMath.Share(100f) == 1f, "feeling share");
Check(HeartMath.Nudge(1.5f, 0f) == 1f && Math.Abs(HeartMath.Nudge(1.5f, 1f) - 1.5f) < 1e-5f && Math.Abs(HeartMath.Nudge(1.5f, 0.5f) - 1.25f) < 1e-5f, "feeling nudge");

// Council meetings: quorum, the evening slot, walking time, late and too late.
Check(MeetingMath.Quorum(1) == 2 && MeetingMath.Quorum(2) == 2 && MeetingMath.Quorum(3) == 2 && MeetingMath.Quorum(4) == 2
      && MeetingMath.Quorum(5) == 3 && MeetingMath.Quorum(10) == 5 && MeetingMath.Quorum(11) == 6, "quorum is half, at least two");
Check(MeetingMath.Thin(2, 5) && !MeetingMath.Thin(3, 5) && !MeetingMath.Thin(2, 4) && MeetingMath.Thin(1, 2), "thin meetings");
Check(Math.Abs(MeetingMath.HoursToEvening(8f, 2f) - 10f) < 1e-4f, "morning call: six this evening");
Check(Math.Abs(MeetingMath.HoursToEvening(16f, 2f) - 2f) < 1e-4f, "16:00 call: six this evening");
Check(Math.Abs(MeetingMath.HoursToEvening(17.5f, 2f) - 2.5f) < 1e-4f, "17:30 call: eight this evening");
Check(Math.Abs(MeetingMath.HoursToEvening(18.5f, 2f) - 23.5f) < 1e-4f, "18:30 call: six tomorrow evening");
Check(Math.Abs(MeetingMath.HoursToEvening(18.5f, 8f) - 23.5f) < 1e-4f, "put off from an evening: the next evening");
Check(Math.Abs(MeetingMath.HoursToEvening(2f, 8f) - 16f) < 1e-4f, "put off after midnight: that evening");
for (float h = 0f; h < 24f; h += 0.25f)
{
    float until = MeetingMath.HoursToEvening(h, 2f), at = (h + until) % 24f;
    Check(until >= 2f - 1e-4f && until <= 26f && at >= 18f - 1e-4f && at <= 20f + 1e-4f, $"slot from {h}: {until}h later at {at}");
}
Check(MeetingMath.IsNight(22f) && MeetingMath.IsNight(23.5f) && MeetingMath.IsNight(0f) && MeetingMath.IsNight(5.99f)
      && !MeetingMath.IsNight(6f) && !MeetingMath.IsNight(12f) && !MeetingMath.IsNight(21.99f), "night is 22:00 to 6:00");
Check(MeetingMath.IsNight(-1f) && MeetingMath.IsNight(25f) && !MeetingMath.IsNight(30f) && !MeetingMath.IsNight(-6f), "night round the clock");
Check(Math.Abs(MeetingMath.HoursToCalledMeeting(10f, 1.5f) - 1.5f) < 1e-4f, "called in the day: as asked");
Check(Math.Abs(MeetingMath.HoursToCalledMeeting(21f, 1.5f) - 21f) < 1e-4f, "called at 21:00 for 22:30: six tomorrow evening");
Check(Math.Abs(MeetingMath.HoursToCalledMeeting(3f, 1f) - 15f) < 1e-4f, "called at 3:00: six this evening");
for (float h = 0f; h < 24f; h += 0.25f)
{
    for (float lead = 1f; lead <= 2f; lead += 0.25f)
    {
        float until = MeetingMath.HoursToCalledMeeting(h, lead);
        Check(until >= 1f - 1e-4f && until <= 26f && !MeetingMath.IsNight(h + until), $"called at {h} for {lead}h later: {until}h, not at night");
    }
}
Check(MeetingMath.EtaTicks(0f, 13f) == MeetingMath.GetGoingTicks, "no walk when already there");
Check(MeetingMath.EtaTicks(40f, 13f) < MeetingMath.EtaTicks(80f, 13f) && MeetingMath.EtaTicks(40f, 13f) < MeetingMath.EtaTicks(40f, 20f), "farther or slower takes longer");
// A normal walker (13 ticks a cell) from 60 cells off is on time, from 120 late, from 200 too late to be called.
Check(!MeetingMath.Late(MeetingMath.EtaTicks(60f, 13f)) && !MeetingMath.TooFar(MeetingMath.EtaTicks(60f, 13f)), "60 cells: on time");
Check(MeetingMath.Late(MeetingMath.EtaTicks(120f, 13f)) && !MeetingMath.TooFar(MeetingMath.EtaTicks(120f, 13f)), "120 cells: late");
Check(MeetingMath.TooFar(MeetingMath.EtaTicks(200f, 13f)), "200 cells: too far");
Check(!MeetingMath.Late(1500) && MeetingMath.Late(1501) && !MeetingMath.TooFar(3150) && MeetingMath.TooFar(3151), "late after a third, too far after 70%");

// ---------------- Room layouts (Building/TemplateLayout.cs) ----------------

// The root found for a footprint gives back that footprint, for every size and facing.
for (int sx = 1; sx <= 5; sx++)
for (int sz = 1; sz <= 5; sz++)
for (int r = 0; r < 4; r++)
{
    TemplateLayout.OccupiedRect(10, 10, r, sx, sz, out int mx, out int mz, out int w, out int h);
    bool found = TemplateLayout.RootFor(mx, mz, w, h, r, sx, sz, out int rx, out int rz);
    TemplateLayout.OccupiedRect(rx, rz, r, sx, sz, out int mx2, out int mz2, out int w2, out int h2);
    Check(found && mx2 == mx && mz2 == mz && w2 == w && h2 == h, $"root round trip {sx}x{sz} facing {r}");
}

// Undoing a mirrored, turned point gives the template point back.
foreach (bool mirror in new[] { false, true })
for (int r = 0; r < 4; r++)
for (int x = 0; x < 7; x++)
for (int z = 0; z < 5; z++)
{
    TemplateMath.Transform(x, z, 7, 5, mirror, r, out int tx, out int tz);
    TemplateMath.InverseTransform(tx, tz, 7, 5, mirror, r, out int ux, out int uz);
    Check(ux == x && uz == z, $"inverse transform m={mirror} r={r} ({x},{z})");
}

// A one-cell piece lands where its cell lands, facing the transformed way; one that only faces north stays north.
foreach (bool mirror in new[] { false, true })
for (int r = 0; r < 4; r++)
{
    TemplateLayout.ToWorld(7, 6, mirror, r, 2, 3, 1, 1, 1, true, false, 1, 1, out int mx, out int mz, out _, out _, out int facing, out int rx, out int rz);
    TemplateMath.Transform(2, 3, 7, 6, mirror, r, out int tx, out int tz);
    Check(mx == tx && mz == tz && rx == tx && rz == tz && facing == TemplateMath.TransformFacing(1, mirror, r), $"one-cell piece m={mirror} r={r}");
    TemplateLayout.ToWorld(7, 6, mirror, r, 2, 3, 1, 1, 1, false, false, 1, 1, out _, out _, out _, out _, out int still, out _, out _);
    Check(still == 0, $"a piece that can't turn faces north m={mirror} r={r}");
    TemplateLayout.ToWorld(7, 6, mirror, r, 2, 3, 1, 1, 0, false, true, 1, 1, out _, out _, out _, out _, out int door, out _, out _);
    Check(door == TemplateMath.DoorFacing(r), $"an inner door turns with its wall m={mirror} r={r}");
}

// A bench whose work spot would be in the wall is turned around; one that fits nowhere is left out if it can be.
{
    var bench = new TemplateLayout.Piece { x = 1, z = 0, rot = 0, sizeX = 3, sizeZ = 1, role = "bench" };
    bench.spots.Add((0, -1));
    var notes = TemplateLayout.Arrange(5, 4, 4, new List<TemplateLayout.Piece> { bench });
    Check(!bench.dropped && bench.facing == 2 && bench.turned, $"bench turned to face south, got {bench.facing}, {string.Join("; ", notes)}");
    var blocker = new TemplateLayout.Piece { x = 2, z = 0, sizeX = 1, sizeZ = 1, optional = true, role = "pot" };
    var notes2 = TemplateLayout.Arrange(5, 4, 2, new List<TemplateLayout.Piece> { blocker });
    Check(blocker.dropped, "a piece on the cell inside the door is left out");
}

// Every room template and size, in every turn, mirrored and not.
{
    string root = Directory.GetCurrentDirectory();
    while (root != null && !Directory.Exists(Path.Combine(root, "Defs", "RoomTemplateDefs"))) root = Path.GetDirectoryName(root);
    Check(root != null, "found the mod's Defs folder");
    if (root != null)
    {
        int layouts = RoomTemplateTests.Run(root, Check);
        Console.WriteLine($"Checked {layouts} room layouts.");
    }
}

Console.WriteLine(failures == 0 ? "All template math, layout, strain and meeting tests passed." : failures + " failure(s).");
return failures == 0 ? 0 : 1;
