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

// Everyday pressures: the setting, meals, work, missing someone, crowding, half-built work and earned rewards.
Check(PressureMath.Factor(LifePressure.Gentle) == 0.5f && PressureMath.Factor(LifePressure.Normal) == 0.85f && PressureMath.Factor(LifePressure.Demanding) == 1.5f, "life factor");
Check(PressureMath.LetdownFactor(LifePressure.Gentle) < PressureMath.LetdownFactor(LifePressure.Normal) && PressureMath.LetdownFactor(LifePressure.Normal) < PressureMath.LetdownFactor(LifePressure.Demanding) && PressureMath.LetdownFactor(LifePressure.Demanding) == 1f, "letdowns soften with the setting");
Check(PressureMath.HardTimes(0.6f, 0.35f) == 0f && PressureMath.HardTimes(0.35f, 0.35f) == 1f && PressureMath.HardTimes(0.1f, 0.35f) == 1f
      && Math.Abs(PressureMath.HardTimes(0.425f, 0.35f) - 0.5f) < 0.001f, "hard times: none 15 points above the break line, full at it");
Check(PressureMath.HardTimesLetdown(0f) == 1f && PressureMath.HardTimesLetdown(1f) == 0.5f && PressureMath.HardTimesPressure(1f) == 0.75f,
      "hard times halve small letdowns and take a quarter off pressures");
Check(PressureMath.Scale(-1f, 0.5f) == -1f && PressureMath.Scale(-3f, 0.5f) == -1.5f && PressureMath.Scale(-2f, 1.5f) == -3f, "pressure scale, a whole point at least");
Check(PressureMath.Scale(2f, 0.5f) == 2f && PressureMath.Scale(-2f, 0f) == 0f, "scale leaves good moods, and off is off");
List<string> Meals(params string[] k) => new List<string>(k);
var simple6 = Meals("S", "S", "S", "S", "S", "S");
Check(PressureMath.FoodStage(Meals("S", "S", "S", "S", "S"), 3) == 0, "five meals: too soon to tell");
Check(PressureMath.FoodStage(simple6, 3) == 1, "six simple meals in a row: the same meal again");
Check(PressureMath.FoodStage(Meals("S", "F", "S", "S", "B", "S", "S", "S"), 3) == 1, "six of the last eight");
Check(PressureMath.FoodStage(Meals("S", "F", "S", "B", "S", "F", "S", "S"), 3) == 0, "five of eight is varied enough");
var simple12 = Meals("S", "S", "S", "S", "S", "S", "S", "S", "S", "S", "S", "S");
Check(PressureMath.FoodStage(simple12, 3) == 3 && PressureMath.FoodStage(simple12, 2) == 2, "twelve the same: three steps with three choices, two with two");
Check(PressureMath.FoodStage(simple12, 1) == 1 && PressureMath.FoodStage(simple6, 1) == 0, "one choice only: one step, and later");
Check(PressureMath.FoodStage(Meals("F", "S", "S", "S", "S", "S", "S", "S", "S", "S", "S", "S"), 3) == 2, "eleven of twelve: two steps");
Check(PressureMath.Picky(1, 3) == 2 && PressureMath.Picky(3, 3) == 3 && PressureMath.Picky(2, 2) == 2 && PressureMath.Picky(1, 1) == 1 && PressureMath.Picky(0, 3) == 0, "gourmands take it harder");
Check(PressureMath.VariedTable(Meals("S", "F", "B")) && !PressureMath.VariedTable(Meals("S", "F", "S", "F")) && !PressureMath.VariedTable(Meals("S", "F")), "a varied table needs three foods");
string sRice = PressureMath.MealKey("S", new[] { "Rice", "Meat" }), sCorn = PressureMath.MealKey("S", new[] { "Meat", "Corn" }), sRice2 = PressureMath.MealKey("S", new[] { "Meat", "Rice", "Rice" });
Check(sRice == sRice2 && sRice != sCorn && PressureMath.MealKey("S", null) == "S" && PressureMath.MealKey("S", new string[0]) == "S", "a meal is its kind and what went into it, in any order");
Check(PressureMath.KindOf(sRice) == "S" && PressureMath.KindOf("S") == "S" && PressureMath.KindOf(null) == null, "the kind of a remembered meal, old saves included");
Check(PressureMath.FoodStage(Meals(sRice, sCorn, sRice, sCorn, sRice, sCorn, sRice, sCorn), 3) == 0, "simple meals made of different things aren't the same meal again");
Check(!PressureMath.VariedTable(Meals(sRice, sCorn, PressureMath.MealKey("S", new[] { "Berries" }))), "but simple meals alone never make a varied table");
Check(PressureMath.VariedTable(Meals(sRice, PressureMath.MealKey("F", new[] { "Rice" }), "B")), "three kinds of meal do");
Check(PressureMath.Dominant(Meals("A", "B", "A", "C"), 3) == 1 && PressureMath.Dominant(Meals("A", "B", "A", "C"), 4) == 2 && PressureMath.Dominant(null, 8) == 0, "dominant kind in a window");
Check(PressureMath.SameWorkDay(9, 12) && !PressureMath.SameWorkDay(8, 12) && !PressureMath.SameWorkDay(5, 5), "same work all day needs 70% of six hours or more");
int streak = 0;
foreach (bool same in new[] { true, true, true, true, true })
    streak = PressureMath.NextStreak(streak, true, same && streak > 0);
Check(streak == 5 && PressureMath.WorkStage(streak) == 1, "five days of one kind of work: one step");
Check(PressureMath.NextStreak(5, true, false) == 1 && PressureMath.NextStreak(5, false, false) == 0, "other work starts again, a varied day or a day off clears it");
Check(PressureMath.WorkStage(3) == 0 && PressureMath.WorkStage(4) == 1 && PressureMath.WorkStage(7) == 2 && PressureMath.WorkStage(10) == 3 && PressureMath.WorkStage(30) == 3, "work steps at 4, 7 and 10 days");
Check(PressureMath.WorkStageWithPassion(1, true) == 0 && PressureMath.WorkStageWithPassion(3, true) == 2 && PressureMath.WorkStageWithPassion(2, false) == 2 && PressureMath.WorkStageWithPassion(0, true) == 0, "a passion for the work takes a step off");
Check(PressureMath.MissingStage(1.9f, true) == 0 && PressureMath.MissingStage(2f, true) == 1 && PressureMath.MissingStage(5f, true) == 2 && PressureMath.MissingStage(12f, true) == 3, "a partner is missed after 2, 5 and 10 days");
Check(PressureMath.MissingStage(2.5f, false) == 0 && PressureMath.MissingStage(3f, false) == 1 && PressureMath.MissingStage(30f, false) == 2, "a friend after 3 and 8 days, two steps at most");
Check(PressureMath.CrowdLevel(3, 4, 3, 0) == 0, "three people are never a crowd");
Check(PressureMath.CrowdLevel(6, 30, 6, 6) == 0 && PressureMath.CrowdLevel(6, 30, 6, 4) == 1 && PressureMath.CrowdLevel(6, 30, 6, 2) == 2, "dining: chairs for everyone, two short, half short");
Check(PressureMath.CrowdLevel(8, 30, 0, -1) == 1 && PressureMath.CrowdLevel(8, 18, 0, -1) == 2 && PressureMath.CrowdLevel(8, 40, 0, -1) == 0, "rec room: space per person");
Check(PressureMath.UnfinishedStage(PressureMath.UnfinishedScore(2)) == 0 && PressureMath.UnfinishedStage(PressureMath.UnfinishedScore(3)) == 1, "a few half-built frames: an eyesore from three");
Check(PressureMath.UnfinishedStage(PressureMath.UnfinishedScore(10)) == 2 && PressureMath.UnfinishedStage(PressureMath.UnfinishedScore(25)) == 3, "frames count in full");
Check(PressureMath.EffortFactor(-1f) == 1f && PressureMath.EffortFactor(0f) == 1f && PressureMath.EffortFactor(0.5f) == 1f, "an ambition with half or more to do is earned in full");
Check(Math.Abs(PressureMath.EffortFactor(0.7f) - 0.8f) < 1e-4f && PressureMath.EffortFactor(0.95f) == 0.6f, "one mostly done already counts for less, 60% at least");

// Good times turning ordinary: good memories fade sooner when life is good, standards and the game's comforts, and how
// good moods add up (Moods/GoodTimesMath.cs).
Check(GoodTimesMath.DurationFactor(0f) == 1f && GoodTimesMath.DurationFactor(0.5f) == 1f && GoodTimesMath.DurationFactor(0.75f) == 1f, "good things last in full up to 75% mood");
Check(GoodTimesMath.DurationFactor(0.9f) == 0.5f && GoodTimesMath.DurationFactor(1f) == 0.5f, "half the time from 90% mood");
Check(GoodTimesMath.DurationFactor(float.NaN) == 1f, "no mood known: in full");
{
    float last = 1f;
    bool falls = true, smooth = true;
    for (int i = 0; i <= 100; i++)
    {
        float f = GoodTimesMath.DurationFactor(i / 100f);
        if (f > last + 1e-6f) falls = false;
        if (last - f > 0.08f) smooth = false;
        last = f;
    }
    Check(falls, "the time a good memory lasts never grows with mood");
    Check(smooth, "and shrinks smoothly, no jumps between one percent and the next");
}
float fade80 = GoodTimesMath.DurationFactor(0.8f), fade85 = GoodTimesMath.DurationFactor(0.85f);
Check(fade80 > 0.8f && fade80 < 0.95f && fade85 > 0.55f && fade85 < 0.7f, $"80% mood about 87%, 85% about 63% (got {fade80}, {fade85})");
Check(GoodTimesMath.ShortenedTicks(60000, 0.5f) == 30000 && GoodTimesMath.ShortenedTicks(60000, 1f) == 60000 && GoodTimesMath.ShortenedTicks(3, 0.1f) == 1, "shortened ticks, one at least");
Check(GoodTimesMath.ShortenedTicks(0, 0.5f) == 0 && GoodTimesMath.ShortenedTicks(-1, 0.5f) == -1, "no time of its own: left alone");
Check(GoodTimesMath.ComfortFactor(0) == 1f && GoodTimesMath.ComfortFactor(1) == 0.9f && GoodTimesMath.ComfortFactor(2) == 0.8f && GoodTimesMath.ComfortFactor(3) == 0.7f,
    "comforts: modest 100%, comfortable 90%, particular 80%, spoiled 70%");
Check(GoodTimesMath.ComfortFactor(-1) == 1f && GoodTimesMath.ComfortFactor(9) == 0.7f, "comfort factor out of range");
Check(Math.Abs(GoodTimesMath.Comfort(5f, 0.8f) - 4f) < 1e-4f && Math.Abs(GoodTimesMath.Comfort(10f, 0.7f) - 7f) < 1e-4f, "a small comfort is scaled in full");
Check(Math.Abs(GoodTimesMath.Comfort(40f, 0.7f) - 37f) < 1e-4f && Math.Abs(GoodTimesMath.Comfort(12f, 0.9f) - 11f) < 1e-4f, "a big one only on its first 10 points");
Check(GoodTimesMath.Comfort(-6f, 0.7f) == -6f && GoodTimesMath.Comfort(0f, 0.7f) == 0f && GoodTimesMath.Comfort(8f, 1f) == 8f, "bad moods and the modest are left alone");
{
    float last = 0f;
    bool rises = true;
    for (int i = 1; i <= 60; i++)
    {
        float v = GoodTimesMath.Comfort(i, 0.7f);
        if (v <= last) rises = false;
        last = v;
    }
    Check(rises, "a bigger comfort is always felt as more");
}
Check(GoodTimesMath.RankFactor(0) == 1f && GoodTimesMath.RankFactor(1) == 1f, "the two biggest good moods count in full");
Check(GoodTimesMath.RankFactor(2) == 0.6f && GoodTimesMath.RankFactor(3) == 0.45f && GoodTimesMath.RankFactor(4) == 0.35f, "then 60%, 45% and 35%");
Check(GoodTimesMath.RankFactor(5) == 0.25f && GoodTimesMath.RankFactor(40) == 0.25f && GoodTimesMath.RankFactor(1000) == 0.25f, "every one after that a quarter, however deep");
Check(GoodTimesMath.RankFactor(-1) == 1f, "rank factor below range");
{
    bool never = true;
    for (int r = 1; r < 60; r++) never &= GoodTimesMath.RankFactor(r) <= GoodTimesMath.RankFactor(r - 1) && GoodTimesMath.RankFactor(r) > 0f;
    Check(never, "a later good mood never counts for more than an earlier one, and never for nothing");
    // A +3 deep in the pile still shows on the Needs tab (whole numbers) as +1.
    Check(Math.Round(3f * GoodTimesMath.RankFactor(30), MidpointRounding.AwayFromZero) == 1, "a +3 joy deep in the stack still shows as +1");
    // A dozen good moods of +3 add up to under half their sum: far less than the pile, more than the old curve's +9.
    float sum = 0f;
    for (int r = 0; r < 12; r++) sum += 3f * GoodTimesMath.RankFactor(r);
    Check(Math.Abs(sum - 15.45f) < 0.01f, $"a dozen +3 joys count as {sum}");
}

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

// The line memory: a pool keeps out a third of its lines (at least one, at most twelve), the oldest dropping out first.
{
    Check(LineMemory.Window(1) == 0 && LineMemory.Window(2) == 1 && LineMemory.Window(9) == 3 && LineMemory.Window(100) == LineMemory.MaxWindow,
        "line memory window sizes");
    var memory = new LineMemory();
    var pool = new object();
    var other = new object();
    memory.Remember(pool, "a", 1);
    Check(memory.Recent(pool) == null, "a pool of one line remembers nothing");
    for (int i = 0; i < 4; i++) memory.Remember(pool, "line " + i, 9);
    Check(!memory.IsRecent(pool, "line 0") && memory.IsRecent(pool, "line 1") && memory.IsRecent(pool, "line 3"), "the oldest pick drops out");
    Check(memory.Recent(other) == null && !memory.IsRecent(other, "line 3"), "pools keep their own memory");
    memory.Remember(pool, "line 3", 9);
    memory.Remember(pool, "line 3", 9);
    Check(memory.Recent(pool).Count == 1 && !memory.IsRecent(pool, "line 2"), "a line picked again pushes out the others");
    memory.Clear();
    Check(memory.Recent(pool) == null, "cleared");
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
        int ailments = AilmentTests.Run(root, Check);
        Console.WriteLine($"Checked {ailments} ailments and illnesses.");
        int memories = ThoughtClassTests.Run(root, Check);
        Console.WriteLine($"Checked {memories} memories share the budget of good moods.");
        int instead = RewardInsteadTests.Run(root, Check, out int converted, out int small);
        Console.WriteLine($"Checked {instead} things about the {converted} of {small} small good moments that give something other than mood.");
        Console.WriteLine($"Checked {ArcTests.Run(root, Check)} things about personal stories (their pace, stages and lines).");
        Console.WriteLine($"Checked {HealthTests.Run(root, Check)} claims about convalescence and healers.");
        Console.WriteLine($"Checked {SettingsTests.Run(root, Check)} things about the settings window (every field on one page, presets, keys, sizes).");
        Console.WriteLine($"Checked {AlmanacLayoutTests.Run(root, Check)} things about the Almanac's layout (columns, tables, text sizes).");
        Console.WriteLine($"Checked {SetbackTests.Run(root, Check)} things about setbacks (when they come, how big, their settings and hints).");
        Console.WriteLine($"Checked {ImmersionTests.Run(root, Check)} things about what colonists say and their small habits.");
        Console.WriteLine($"Checked {TogetherTests.Run(root, Check)} things about moments that need people together, and how speech shows.");
        Console.WriteLine($"Checked {PersonaTests.Run(root, Check)} things about characters (nature, marks, drift, words, likeness).");
        Console.WriteLine($"Checked {FaithTests.Run(root, Check)} things about faith in the colony (devout, wavering, swaying, a split colony).");
        Console.WriteLine($"Checked {FaithCouncilTests.Run(root, Check)} things about faith and the council (evenings of faith, the wish, the decree, faiths at odds).");
        Console.WriteLine($"Checked {TalkTests.Run(root, Check)} things about conversations (topics, how a talk goes, persuasion, the lines in turn).");
        Console.WriteLine($"Checked {MarkTests.Run(root, Check)} things about marks (choosing by nature, the limits, soft factors, the defs).");
        Console.WriteLine($"Checked {GroupTests.Run(root, Check)} things about groups (forming, roles, cohesion, splits and merges, newcomers, rivalries).");
        Console.WriteLine($"Checked {ReputationTests.Run(root, Check)} things about gossip and reputation (slants, belief, fading, caps, the deeds).");
        Console.WriteLine($"Checked {TraitChangeTests.Run(root, Check)} things about traits that change (steps, limits, absorbing, conflicts, the rules).");
        Console.WriteLine($"Checked {CouncilUiTests.Run(root, Check)} things about the council window (This week, the tables, page links, shortcuts).");
        Console.WriteLine($"Checked {ModsTests.Run(root, Check)} things about the player's other mods (their names, people who change).");
        Console.WriteLine($"Checked {PerfTests.Run(root, Check)} things about the caches that keep Rimfolk light (lines, facts, slices).");
        Console.WriteLine($"Checked {DefenseTests.Run(root, Check)} things about the defense planner (its plans in every turn, reading a map's ways in).");
        Console.WriteLine($"Checked {DefenseLineTests.Run(root, Check)} things about defense lines (the way round, the steps, strength and lessons).");
        Console.WriteLine($"Checked {ColonyTraitTests.Run(root, Check)} things about what a colony grows into and the names of its years.");
        Console.WriteLine($"Checked {WhatsNewTests.Run(root, Check)} things about What's new (the page, CHANGELOG.md and the version).");
        Console.WriteLine($"Checked {LogLinesTests.Run(root, Check, out int logLines)} things about the {logLines} log lines added from separate files.");
    }
}

// Rimfolk's inspect tabs fit above the inspect pane on small screens, big interface scales and RimHUD's taller pane.
Console.WriteLine($"Checked {UiTests.Run(Check)} tab sizes.");
Console.WriteLine($"Checked the calendar's dates over {CalendarTests.Run(Check)} days.");
Console.WriteLine($"Checked {LeanTests.Run(Check)} things about the clock's spread and the save caps.");
Console.WriteLine($"Checked {MemoryTests.Run(Check)} things about what the peoples remember (fading, adding up, making way, tone).");
Console.WriteLine($"Checked {PlayOutTests.Run(Check)} things about answers playing out on the map.");
Console.WriteLine($"Checked {PlayOutGroupTests.Run(Check)} things about work parties, walks, vigils, searches, errands and hunts.");
Console.WriteLine($"Checked {KeptTests.Run(Check)} things about granted wishes taken back (grace, timing, making it right).");
Console.WriteLine($"Checked {SeasonWishTests.Run(Check)} things about the wishes of the land's year (clothes for the season, a sauna, hot country, the fields opening).");
Console.WriteLine($"Checked {HeartNeedTests.Run(Check)} things about the heart's needs (what follows from them, and a lack that lasts).");
Console.WriteLine($"Checked {BeliefTests.Run(Check)} things about beliefs and diets (precepts, a shared bed, what may be served).");
Console.WriteLine($"Checked {AdvisorTests.Run(Check)} things about the advisor's newer checks (missions, decrees, winter stores, rooms outgrown, light).");
Console.WriteLine($"Checked {AnimalTests.Run(Check)} things about the colony's animals (age, the herd, a cull, the calendar, PokéWorld's levels and Pokédex).");

Console.WriteLine(failures == 0 ? "All tests passed (template math, layout, strain, meetings, pressures, ailments, line memory, good times, tab sizes, calendar, personal stories, clock and caps, faction memory, answers on the map, setbacks, promises kept, what colonists say, the heart's needs, beliefs and diets, animals, other mods)." : failures + " failure(s).");
return failures == 0 ? 0 : 1;
