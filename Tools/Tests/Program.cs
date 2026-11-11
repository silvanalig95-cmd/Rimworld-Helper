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
Check(HeartMath.MoodOffset(StrainBand.Steady) == 0f && HeartMath.MoodOffset(StrainBand.Strained) == -4f
      && HeartMath.MoodOffset(StrainBand.Worn) == -8f && HeartMath.MoodOffset(StrainBand.Breaking) == -14f, "strain mood by band (the heart made to count)");
Check(Math.Abs(HeartMath.WorkFactor(StrainBand.Strained) - 0.97f) < 1e-5f && Math.Abs(HeartMath.WorkFactor(StrainBand.Worn) - 0.93f) < 1e-5f
      && Math.Abs(HeartMath.WorkFactor(StrainBand.Breaking) - 0.88f) < 1e-5f, "work speed by band, from strained");
Check(HeartCountMath.Work(StrainBand.Strained, false) == 1f && Math.Abs(HeartCountMath.Work(StrainBand.Worn, false) - 0.95f) < 1e-5f
      && Math.Abs(HeartCountMath.Work(StrainBand.Breaking, false) - 0.9f) < 1e-5f, "work speed by band without the effects beyond mood, as before");
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

// Good things grow ordinary: the one rule's arithmetic is in GoodLifeTests (Moods/GoodLifeMath.cs).

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

    // Each speaker's own (the player's save, 6 November 2026: 24 of 630 journal lines were the writer's own again).
    for (int i = 0; i < LineMemory.SpeakerWindow + 1; i++) memory.RememberSaid(7, "said " + i);
    Check(memory.SaidBy(7) is { } s7 && !s7.Contains("said 0") && s7.Contains("said 1") && s7.Count == LineMemory.SpeakerWindow, "a speaker keeps out their last twenty lines, from any pool");
    Check(memory.SaidBy(8) == null, "speakers keep their own memory");
    string tinted = "With <color=#D09B61FF>Alo</color> on it, I'll be up in no time!";
    Check(LineMemory.Wrote("With {OTHER_nameDef} on it, I'll be up in no time!", tinted), "a pool's line is found in the journal with its name filled in and coloured");
    Check(LineMemory.Wrote("{OTHER_nameDef} fixed me. Good doctor.", "<color=#D09B61FF>Alo</color> fixed me. Good doctor."), "a line that begins with a name");
    Check(LineMemory.Wrote("It's going to be fine! Grumpy, but fine.", "it's going to be fine! Grumpy, but fine."), "a line with no names, case aside");
    Check(LineMemory.Wrote("I'm the {OFFICE}! Practiced my serious face in the washbasin tonight.", "I'm the speaker! Practiced my serious face in the washbasin tonight."),
        "a line with a word filled in mid-sentence");
    Check(!LineMemory.Wrote("With {OTHER_nameDef} on it, I'll be up in no time!", "With Alo on it, I'll be up in no time! Or so I hoped."), "the line's last words must end what was written");
    Check(!LineMemory.Wrote("It's going to be fine! Grumpy, but fine.", "It's going to be fine! Grumpy, but fine. Mostly."), "a line with no names is the whole entry");
    Check(!LineMemory.Wrote("{PAWN_nameDef} smiled.", "Alo smiled."), "a line with too few words of its own is never matched");
    Check(LineMemory.Plain("<color=#FF0000>Red</color> and <b>bold</b>") == "Red and bold" && LineMemory.Plain("a < b") == "a < b", "rich text is passed over, other angle brackets kept");
    var lits = LineMemory.Literals("[x]{A} met {B} at [place].", out bool openStart, out bool openEnd);
    Check(openStart && !openEnd && lits.SequenceEqual(new[] { " met ", " at ", "." }), "a line's own words between its placeholders");
    var pool2 = new object();
    var journal = new List<string> { tinted, "Something else entirely happened today." };
    int asked = 0;
    IEnumerable<string> PoolLines()
    {
        asked++;
        return new[] { "With {OTHER_nameDef} on it, I'll be up in no time!", "A fresh line that nobody has written yet, surely." };
    }
    var wrote = memory.WrittenBy(pool2, 7, 1, PoolLines, () => journal);
    Check(wrote != null && wrote.Count == 1 && wrote.Contains("With {OTHER_nameDef} on it, I'll be up in no time!"), "the pool's lines still in the speaker's journal are kept out");
    memory.WrittenBy(pool2, 7, 1, PoolLines, () => journal);
    Check(asked == 1, "worked out again only when the journal changes");
    Check(memory.WrittenBy(pool2, 7, 2, PoolLines, () => new List<string>()) == null && asked == 1, "an empty journal keeps nothing out");
    Check(LineMemory.Repeats(journal, tinted) && !LineMemory.Repeats(journal, "A new day.") && !LineMemory.Repeats(null, tinted), "the very same words are never written twice while the journal holds them");
    Check(!LineMemory.ToneDrawsAlone(1) && !LineMemory.ToneDrawsAlone(2) && LineMemory.ToneDrawsAlone(3), "a tone of one or two lines draws together with the plain lines");
    memory.Remember(pool, "line 9", 9);
    memory.ForgetSpeakers();
    Check(memory.SaidBy(7) == null && memory.IsRecent(pool, "line 9"), "another game forgets the speakers, not the pools");
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
        Console.WriteLine($"Checked {AilmentLandTests.Run(root, Check)} things about the ailments of the land, the work and the age (their causes, the cap, the storyteller, their names and words).");
        Console.WriteLine($"Checked {LifeRiskTests.Run(root, Check)} things about the realistic ways to die (crowding and sickness, the frail, the end of a long life, wounds left untended).");
        Console.WriteLine($"Checked {IllnessBudgetTests.Run(root, Check)} things about the shared illness budget (which incidents count, the game's own rate, the soft hold, one debt at a time, the pause and the easing, the readiness, two at once, expiry, the years played out, the balance tables against the targets, the hook, the setting, the saved ledgers, the words).");
        int memories = ThoughtClassTests.Run(root, Check);
        Console.WriteLine($"Checked {memories} memories share the budget of good moods.");
        int instead = RewardInsteadTests.Run(root, Check, out int converted, out int small);
        Console.WriteLine($"Checked {instead} things about the {converted} of {small} small good moments that give something other than mood.");
        Console.WriteLine($"Checked {FoundingTests.Run(root, Check)} things about a fresh start in the same world (each one's wish, who goes, the newcomers' price, the seat once a season, the colony limit raised by one, the kit, the colonies' record).");
        Console.WriteLine($"Checked {ArcTests.Run(root, Check)} things about personal stories (their pace, stages and lines).");
        Console.WriteLine($"Checked {VisitorWorkTests.Run(root, Check)} things about visitors doing what their letters say (work, roofs, the wall).");
        Console.WriteLine($"Checked {VisitorActsTests.Run(root, Check)} things about visitors and the colony's things (the instrument, fair game, the dead they carry, guest beds).");
        Console.WriteLine($"Checked {VisitorsMore2Tests.Run(root, Check)} things about visitors for what the colony keeps (the relic, the anima tree, kin, the gravship, books, regulars' lives).");
        Console.WriteLine($"Checked {CampLandTests.Run(root, Check)} things about camps that come for the land (the harvest, the sheaf in ten, the catch, the ruins, the run).");
        Console.WriteLine($"Checked {CampBuildTests.Run(root, Check)} things about camps that build (what is cleared, a bed each, rooms, every kind's layout, the kits, what they take, the day, packing, what they leave).");
        Console.WriteLine($"Checked {CampTimerTests.Run(root, Check)} things about the camps' own clock and diary.");
        Console.WriteLine($"Checked {CampCrowdTests.Run(root, Check)} things about camps side by side, bands against a camp and old camps' sites (the levels, the map's room, keeping apart, who attacks whom, how it ends, moving in, contested spots).");
        Console.WriteLine($"Checked {CampHarmTests.Run(root, Check)} things about what makes a camp go when harmed (real attacks only; beasts, insects and stray shots fought off while the camp stays).");
        Console.WriteLine($"Checked {CampWinterTests.Run(root, Check)} things about camps that come in the coldest winter (winter folk and the floor of the cold, the five kinds, their wardrobe by age and the mod pool, the ice fishers' holes, a night under the colony's roof).");
        Console.WriteLine($"Checked {StoryChainTests.Run(root, Check)} things about story chains told from data and event variants (routes, endings, flags, answers).");
        Console.WriteLine($"Checked {HealthTests.Run(root, Check)} claims about convalescence and healers.");
        Console.WriteLine($"Checked {SettingsTests.Run(root, Check)} things about the settings window (every field on one page, presets, keys, sizes).");
        Console.WriteLine($"Checked {ImpactHintTests.Run(root, Check)} things about the hints in words under each answer (sizes, order, length, no numbers).");
        Console.WriteLine($"Checked {ImpactFlavourTests.Run(root, Check)} things about the flavour line under each answer (what it says, which way, no mechanics, length, variety).");
        Console.WriteLine($"Checked {AlmanacLayoutTests.Run(root, Check)} things about the Almanac's layout (columns, tables, text sizes).");
        Console.WriteLine($"Checked {LeaningTests.Run(Check)} things about leanings (how colonists are drawn to other ideoligions, and how fast).");
        Console.WriteLine($"Checked {PracticeTests.Run(Check)} things about practice (habits that grow from what colonists do, kept while lived).");
        Console.WriteLine($"Checked {BuildPriorityTests.Run(Check)} things about build priorities (the button's steps, what waits for what).");
        Console.WriteLine($"Checked {DecreeMathTests.Run(Check)} things about decrees (when many are hurt, needed and wanted decrees sooner, the work order in a clean sweep).");
        Console.WriteLine($"Checked {FamilyTests.Run(Check)} things about family beyond blood (by marriage, the family tree, where they are, reunions).");
        Console.WriteLine($"Checked {FamilyLifeTests.Run(Check)} things about the family's life together (couples, a child on the way, birthdays, names).");
        Console.WriteLine($"Checked {FamilyYearsTests.Run(Check)} things about families across the years (years asleep, generations in the line, ages the wrong way round, remarks).");
        Console.WriteLine($"Checked {StallCounterTests.Run(Check)} things about RimMarket's sales table, Rimfolk's way (prices as RimMarket keeps them, counts, how a price sells).");
        Console.WriteLine($"Checked {VisitorTradeTests.Run(root, Check)} things about visitors and campers buying at the colony's stalls, and the drovers' milk.");
        Console.WriteLine($"Checked {TradeScreenTests.Run(root, Check)} things about the trade screen (the reserve kept of each good, food by days, the worse spares, the price against the usual, the order of the suggestions, the basket within both sides' silver, the sorts' defs, the hooks, Vanilla Trading Expanded and Trade Helper, the words).");
        Console.WriteLine($"Checked {TradeMarketTests.Run(root, Check)} things about the trade screen's market and the colony's trade record (the market value against the usual and its average, its 7 and 30 days' change, its 60 days' high and low, a notable move, what a trade does to the price by Vanilla Trading Expanded's rule, the marks' reasons, the record's caps and best prices, the hooks, the settings, the save, the words).");
        Console.WriteLine($"Checked {TradeWindowTests.Run(root, Check)} things about Rimfolk's own trade window (each side's grade from the usual price, the market and the record, the caps for need, the shares, the sorts and the kinds' groups, what a deal comes to, the best deals, the two lists side by side or as tabs at 1920, 1600, 1280 and the least with their columns, the rows' heights, the one-way counts, the chips, the kinds, the opening and the switch, the game's own prices, count limits and Accept, rows drawn from what was read, the setting, the words).");
        Console.WriteLine($"Checked {CallerTests.Run(root, Check)} things about visitors who come to do something (prices, cuts, their jobs and lines).");
        Console.WriteLine($"Checked {SetbackTests.Run(root, Check)} things about setbacks (when they come, how big, their settings and hints).");
        Console.WriteLine($"Checked {GoodLifeTests.Run(root, Check)} things about good things growing ordinary and the quiet troubles (the take-back, the line, the chance, the defs).");
        Console.WriteLine($"Checked {SmallMomentTests.Run(root, Check)} things about small letdowns and joys (the moment, what moves to the heart, every small mood's pile).");
        Console.WriteLine($"Checked {GoodHeartTests.Run(root, Check)} things about the heart's good side (the bands, filling and spending, the caps, the fade, the release, the setting, old saves, the words).");
        Console.WriteLine($"Checked {HeartFeltTests.Run(root, Check)} things about the heart, felt (the lasting weight, rest under strain, the two sides at once, weeks of play against the targets, the acts, lines, signs, bar line and messages, the settings and the save).");
        Console.WriteLine($"Checked {HeartMeterTests.Run(root, Check)} things about the heart's meters (the ticks and their labels, the layout at the tab's width and narrower, the trend, the day off beside them and its reasons, the colonist's button behind its setting, RimHUD's strain line fitted to its cell).");
        Console.WriteLine($"Checked {StoryGlanceTests.Run(root, Check)} things about the Story tab at a glance (who is a close friend or a rival and in what order, the portraits that fit, the trust meter, the rows from a narrow card to a wide window, what it shows and from what, the sections in their order and remembered, nothing lost, the words).");
        Console.WriteLine($"Checked {RoleClothesTests.Run(root, Check)} things about role clothes the colony can reach (each age's usual cost, off the usual path, dear for its age, a piece in or out of reach and what keeps it, the role's first draw, the dialog's order, the player's Inquisitor, the patches for the colony's ideoligions only, the mood's advice and button, the dialog editing the role as the game's editor does, the setting, the hint, the words).");
        Console.WriteLine($"Checked {YardTests.Run(Check)} things about where the colony gathers out of doors (terraces and seats, not the machinery).");
        Console.WriteLine($"Checked {CustomYearsTests.Run(root, Check)} things about customs kept year after year (their tags, lines that can come true, what's said at them).");
        Console.WriteLine($"Checked {OutcomeTests.Run(root, Check)} things about how answers are told (the risk's words, variants picked by tags, world situations' answers).");
        Console.WriteLine($"Checked {StrayBeastTests.Run(root, Check)} things about the neighbors' stray (really there, where the letter says, led home or kept).");
        Console.WriteLine($"Checked {ImmersionTests.Run(root, Check)} things about what colonists say and their small habits.");
        Console.WriteLine($"Checked {TogetherTests.Run(root, Check)} things about moments that need people together, and how speech shows.");
        Console.WriteLine($"Checked {ElectionTests.Run(root, Check)} things about elections on the map (the poll, the campaign, the hustings, the count, the planks).");
        Console.WriteLine($"Checked {FairCouncilTests.Run(root, Check)} things about a fairer council (seats named by the age, elections on a record, life goals that last, decrees and groups that mean distinct things).");
        Console.WriteLine($"Checked {LettersPetitionsTests.Run(root, Check)} things about letters that arrive in days and bring something, and shared wants as one petition.");
        Console.WriteLine($"Checked {PeoplesScreenTests.Run(root, Check)} things about the peoples screen (the list's groups, sorts closest first, search and filters; regard, this season's trend, the web; the layout at both screens with the drawer shut and open; letters by occasion; what each act is for and costs; the seams).");
        Console.WriteLine($"Checked {PostTests.Run(root, Check)} things about the post (letters going with whoever is going their way, the regular post by the age and its pace, the mail coming in and the wait before a messenger, the range until radio: the land's cost, the range by age and means, the relay; the reach line, the words, the seams).");
        Console.WriteLine($"Checked {LetterFormTests.Run(root, Check, Environment.GetEnvironmentVariable("RIMFOLK_LETTERS_PRINT") == "1")} things about letters that read like letters (the form's rules: the salutation, closing and P.S. by tone, motive, age and people, the voice, schooling and strange voices; every pool's lines for every purpose, tone and speech; a cool, curt or businesslike letter never warm; each speech only for its writers; medieval English and the plain kept apart; the writers' kinds against the player's mods; outcomes under the letter; calls and words passed on not shaped as letters; whole letters; the seams).");
        Console.WriteLine($"Checked {LetterShapeTests.Run(root, Check, Environment.GetEnvironmentVariable("RIMFOLK_LETTERS_PRINT") == "1")} things about letters of many shapes (each shape only for the writers and situations it fits; the weights, Short and Long and storied, never the same shape twice running; the long pieces remembered across the game; paging and the card's first page; stories about the colony only from true facts, a raid on a town only from its record, else an old tale; medieval and modern never mixed; every shape in medieval, tribal and plain; the pools; whole letters of every shape; the seams).");
        Console.WriteLine($"Checked {LetterThreadTests.Run(root, Check, Environment.GetEnvironmentVariable("RIMFOLK_LETTERS_PRINT") == "1")} things about a correspondence that builds up (its stages by letters, time, regard, good turns and wrongs; cooling after a wrong, a broken word, a war or a fall in regard; names, nicknames and signatures; the threads raised, taken up and followed by what is true now; the running jokes; the pools; whole correspondences; the seams).");
        Console.WriteLine($"Checked {DiplomacyBalanceTests.Run(root, Check)} things about diplomacy's balance and realism (warnings only of a real threat to their town, recent or coming, each worth its own debt once a season; debts called in by their size and the people's means, once a season; the gates of matches, wards, truces they pay, gifts, help, a pen friend's favour and something brought; news heard kept in the history and on the people's page; an older save carried over; the seams).");
        Console.WriteLine($"Checked {DealingTests.Run(root, Check)} things about dealings with a whole people or town (each dealing's terms, what is open and why not, the answer by regard and ways, the road's days), pen friends, the world writing first and the chain of neighbours.");
        Console.WriteLine($"Checked {FellThroughTests.Run(root, Check)} things about answers that fell through in the player's save (the shearing, the sowing, the reasons told).");
        Console.WriteLine($"Checked {SaveWordsTests.Run(root, Check)} things about the words the player's save showed untrue (counts, sparks, wishes, the chronicle, the year's letter, the officers).");
        Console.WriteLine($"Checked {BanishTests.Run(root, Check)} things about banishment by the council (the trouble, a word first, banishment only if it goes on, who dislikes and who stands by, what the colony loses, every age, the player's own case).");
        Console.WriteLine($"Checked {ScribeTests.Run(root, Check)} things about saving and loading (a collection loaded into a local and read after loading comes back empty).");
        Console.WriteLine($"Checked {VarietyTwoTests.Run(root, Check)} things about event variety (the cold snap, blight, the good weapon, the animal left behind, the herd crossing, the region's blight and scarce medicine, woodcutters and herb-gatherers).");
        Console.WriteLine($"Checked {VarietyThreeTests.Run(root, Check)} things about world events and camps for variety (a war between neighbours, a cold year, a great work, a ban on drink, a great caravan, households on the move; stonecutters, shearers, beast-catchers, a teacher, wounded fighters).");
        Console.WriteLine($"Checked {VarietyDangersTests.Run(root, Check)} things about the third round of variety (eight questions, four events, six dangers on the map).");
        Console.WriteLine($"Checked {PeopleFourTests.Run(root, Check)} things about the colony's own people, the fourth round (twelve life goals, seven wishes, eight quirks, eight names, six deeds).");
        Console.WriteLine($"Checked {NeighboursFourTests.Run(root, Check)} things about the fourth round of neighbours (fourteen requests, seven callers at the gate, five callers told more ways, one draw for every mission).");
        Console.WriteLine($"Checked {TogetherFourTests.Run(root, Check)} things about new moments of life together (between colonists, shared memories, the sick as a topic, group habits, dreams, back to it).");
        Console.WriteLine($"Checked {PokeWorldSoundsTests.Run(root, Check)} things about what PokéWorld's Pokémon sound like (an animal's voice by body, type and size, another mod's where it fits better, never more often, the setting).");
        Console.WriteLine($"Checked {PokeWorldReviewTests.Run(root, Check)} things about PokéWorld reviewed against PokéWorld Updated (the wild ones, the slider, eight generations, every name read, the log, the shiny shown).");
        Console.WriteLine($"Checked {FreshCarcassTests.Run(root, Check)} things about the alert for fresh carcasses lying out (fresh, seen, unstored, nobody on it, an hour after).");
        Console.WriteLine($"Checked {HuntTests.Run(root, Check)} things about the colony's animals hunting for themselves (who hunts by kind, family, species and type, the food chain and the prey below it, when, the fed hunts, the alert, the seams).");
        Console.WriteLine($"Checked {BodyWhenItDiesTests.Run(root, Check)} things about taking a bleeding animal's body when it dies (the button, the order dropped and why, the outlook by the game's rules, the haul order, the kill's patch, the setting).");
        Console.WriteLine($"Checked {BiomeAnimalsTests.Run(root, Check)} things about a land whose wild animals the game can't read (repeats merged, missing defs left out, the log's line, every reading through one helper).");
        Console.WriteLine($"Checked {GuestGroupBedsTests.Run(root, Check)} things about Hospitality's guest groups fitting the guest beds (about as many guests as free beds, one or two more now and then, the rest walking past, Hospitality's own rule with no bed at all).");
        Console.WriteLine($"Checked {FieldReportTests.Run(root, Check)} things about the player's reports (a downed beast isn't a nemesis yet; food left out for a beast goes to the beast, never to a courier).");
        Console.WriteLine($"Checked {HudTemperatureTests.Run(root, Check)} things about the vitals' outdoor temperature in colour (the bands around the human comfort range, in order and agreeing with the row's orange and red, the scale's colours, the colonists as dressed, the words).");
        Console.WriteLine($"Checked {InvitedGuestsTests.Run(root, Check)} things about invited visitors as Hospitality's guests (who is invited, the stay's length and who ends it, who leads where they keep, when they're handed over, Hospitality's own way by name, each invited arrival with Rimfolk's own group as the fallback, the feast and the envoy on top, what stays Rimfolk's own, the setting).");
        Console.WriteLine($"Checked {TemperatureScaleTests.Run(root, Check)} things about the temperature in colour (deep blue to white to deep red by the colonists' comfort, smooth and readable on every ground, the game's readout and the vitals on the one scale, the setting, the seams).");
        Console.WriteLine($"Checked {DietMealsTests.Run(root, Check)} things about vegetarian and carnivore simple meals (simple meals in every way, their bills, the game's own foods and benches, their tint, older saves).");
        Console.WriteLine($"Checked {GodsTests.Run(root, Check)} things about the gods (a god's domain from its words and its faith, Rimfolk's makers, the age's forms, natures and kinds, favour in words and its drift, how real they are, the ties, favour spilling over and ties shifting, newborn, fading and dead gods, the first favour sources, the domains and the patrons, the words, swearing by a god, the letters, the seams).");
        Console.WriteLine($"Checked {YearFourTests.Run(root, Check)} things about the council and the colony's year, fourth round (ten questions, four customs, five decrees, seven advisor checks).");
        Console.WriteLine($"Checked {LandingTests.Run(root, Check)} things about choosing another landing site (when it is offered, who comes, the starting kit followed and never counted larger, the map's size and seed, the same settlement, the words).");
        Console.WriteLine($"Checked {VendettaTests.Run(root, Check)} things about a vendetta passed on at its avenger's death (whoever was closest, by how close they really were, never blood kin first; noted at the death; the words).");
        Console.WriteLine($"Checked {PersonaTests.Run(root, Check)} things about characters (nature, marks, drift, words, likeness).");
        Console.WriteLine($"Checked {FaithTests.Run(root, Check)} things about faith in the colony (devout, wavering, swaying, a split colony).");
        Console.WriteLine($"Checked {FaithPeopleTests.Run(root, Check)} things about faith among the people (talks that sway, crises, views beside certainty).");
        Console.WriteLine($"Checked {FaithCouncilTests.Run(root, Check)} things about faith and the council (evenings of faith, the wish, the decree, faiths at odds).");
        Console.WriteLine($"Checked {TalkTests.Run(root, Check)} things about conversations (topics, how a talk goes, persuasion, the lines in turn).");
        Console.WriteLine($"Checked {MarkTests.Run(root, Check)} things about marks (choosing by nature, the limits, soft factors, the defs).");
        Console.WriteLine($"Checked {GroupTests.Run(root, Check)} things about groups (forming, roles, cohesion, splits and merges, newcomers, rivalries).");
        Console.WriteLine($"Checked {ChemistryTests.Run(root, Check)} things about how two colonists get on (not everyone ends up friends).");
        Console.WriteLine($"Checked {BondsTests.Run(root, Check)} things about how bonds come about (knowing each other, limits, comrades, grudges, seats).");
        Console.WriteLine($"Checked {ReputationTests.Run(root, Check)} things about gossip and reputation (slants, belief, fading, caps, the deeds).");
        Console.WriteLine($"Checked {TraitChangeTests.Run(root, Check)} things about traits that change (steps, limits, absorbing, conflicts, the rules).");
        Console.WriteLine($"Checked {CouncilUiTests.Run(root, Check)} things about the council window (This week, the tables, page links, shortcuts).");
        Console.WriteLine($"Checked {PagesTests.Run(root, Check)} things about Rimfolk's four windows (which page is where, views and modes, old addresses, each view's \"?\", the view header, the table beside the drawer, the buttons, the world's history list).");
        Console.WriteLine($"Checked {CardsTests.Run(root, Check)} things about names that lead somewhere (things, links and their layout, no marker in the game's text, the way back, cards' slots, the drawer's place).");
        Console.WriteLine($"Checked {RegionViewsTests.Run(root, Check)} things about the region in four views (six lenses, their marks, lines and lists, the views' parts).");
Console.WriteLine($"Checked {LayoutTests.Run(Check)} things about Rimfolk's windows across screens (standard and large, the tabs and the corner, the drawer, the region map).");
Console.WriteLine($"Checked {OneLookTests.Run(root, Check)} things about one look (two levels of previews and where they go, the defense window, names in the inspect tabs, the window size).");
Console.WriteLine($"Checked {LookTests.Run(root, Check)} things about one look across every window (the tokens and no colour of a page's own, the grid, the components, names as links in the panels and the council's pages).");
Console.WriteLine($"Checked {RimfolkLookTests.Run(root, Check)} things about Rimfolk's own look (the frame on every screen, the remembered places, text contrast, the kit's shapes, the layouts from the least window to a large one, the seams).");
Console.WriteLine($"Checked {RimfolkLookStageTwoTests.Run(root, Check)} things about Rimfolk's own look, stage 2 (one floating window of the four at a time, every window's key and least size, the layouts as windows grow, every window framed, the kit's controls, the seams).");
Console.WriteLine($"Checked {EventCardTests.Run(root, Check)} things about Rimfolk's event cards (the kind of every letter Rimfolk sends, what each option becomes on the card, closing without an answer, what the council leans to, the layout from the least to 4K made large, the plain letters kept, the seams).");
Console.WriteLine($"Checked {HudLookTests.Run(root, Check)} things about the on-screen panels in Rimfolk's look (text at 4.5:1 on the card over the map, the cards starting clear of one another, a colonist's row at every scale, the bars, the clock, the classic kept, nothing made while drawing, the seams).");
Console.WriteLine($"Checked {ColonistCardTests.Run(root, Check)} things about the colonist card (its columns from the least to 4K, which part goes where, built rarely, the arrows past cards already open, a second card beside the first, the skills' bars and best, the worn pieces' slots, dressed for the season, the foot, every colonist's card opening it, several at once, the ways in that leave the game's clicks alone, nothing made while drawn, the settings and words).");
Console.WriteLine($"Checked {InboxTests.Run(root, Check)} things about one inbox and search (the sections, the one count, the rows' order, steps never a time left, following, the search, the corner).");
        Console.WriteLine($"Checked {ModsTests.Run(root, Check)} things about the player's other mods (their names, people who change).");
        Console.WriteLine($"Checked {PerfTests.Run(root, Check)} things about the caches that keep Rimfolk light (lines, facts, slices).");
        Console.WriteLine($"Checked {PassClockTests.Run(root, Check)} things about Rimfolk's clock (hours and days by game ticks, the worst run, the table, every pass timed).");
        Console.WriteLine($"Checked {TellTests.Run(root, Check)} things about the teller (the colony's day, a busy day, the same thing once, the evening, kinds by folder, what stays direct).");
        Console.WriteLine($"Checked {StageTests.Run(root, Check)} things about the colony's pace (the week's room, the wait, the floor, calendar days together, one custom a season, toasts at the next gathering).");
        Console.WriteLine($"Checked {FestiveYearTests.Run(root, Check)} things about one festive year (customs on the calendar, one tradition rule, an older save's counts, one feast engine).");
        Console.WriteLine($"Checked {HistoryTests.Run(root, Check)} things about the world's history (kinds, indexes, a run in a day, folding by season, bounds, the chronicle as its view, an older save's list taken in once, places of world events).");
        Console.WriteLine($"Checked {OneRecordTests.Run(root, Check)} things about one record (the chronicle as the history's view, every question's answer and what came of it, the year told once, less noise).");
        Console.WriteLine($"Checked {OneNameTests.Run(root, Check)} things about one name (a colonist's one slow \"known as\", never back and forth; the colony's one \"known for\", its card and one sentence when it changes; an older save's names held, nothing told on load).");
        Console.WriteLine($"Checked {WorldMechanicsTests.Run(root, Check)} things about other mods' world mechanics and the ages (the same era tags, every feature's form in every age, what the history keeps, a battle decided, no mod named in the core).");
        Console.WriteLine($"Checked {RegionTests.Run(root, Check)} things about the region (the towns in detail, remoteness, fortunes and what moves them, new towns, the bounds, the two passes).");
        Console.WriteLine($"Checked {TravelTests.Run(root, Check)} things about travelers to the remote (the rates by remoteness, the land's reasons, far news and its bounds, a quarrel's rate, rites and titles in invitations).");
        Console.WriteLine($"Checked {RaidersCampTests.Run(root, Check)} things about raiders' camps (when one is set up, where, found, the raid schedule from the curve, giving up, after an attack, price and word, the endings).");
        Console.WriteLine($"Checked {RiseFallTests.Run(root, Check)} things about towns that rise and fall (the pace, who is given up, ruins and their days, who settles a ruin, new towns and where they stand, caravans, five years of a reach).");
        Console.WriteLine($"Checked {TradingPostTests.Run(root, Check)} things about trading posts (who opens one, when, where it stands and which way, its name and keeper by age, souring day by day, sacks, caravans and the bandit road, the mod's side).");
        Console.WriteLine($"Checked {HamletTests.Run(root, Check)} things about hamlets (who asks and when, where, the escort and the first night, the road's roll, going alone, the harvest and hard seasons, the broken tie, fealty, the second year, small towns' maps, the mod's side).");
        Console.WriteLine($"Checked {DiplomacyTests.Run(root, Check)} things about diplomacy's base and favours owed (debts earned and renewed, honoured by regard, their calls and needs, goods by fortune, the base's frames, the reserved ties, the nature service's acts, the mod's side).");
        Console.WriteLine($"Checked {PactTests.Run(root, Check)} things about pacts and truces (who may offer, their answers, the firebrands, a truce's end, pacts and their calls, the cart, calls to arms, breaches, the terms, the facts, the raid patch, the mod's side, the words).");
        Console.WriteLine($"Checked {WardTests.Run(root, Check)} things about wards and hostages (their offer, the skill and the guardian, lessons, how a ward is, the year's end, the colony's youth abroad, hostages and their terms, the round's three fixes, the facts, the mod's side, the words).");
        Console.WriteLine($"Checked {RansomTests.Run(root, Check)} things about ransom and the envoy (who is wanted back, prices, the haggle, refusals, the game's demand, exchanges, the trap at the game's rate, the stay's score, letters home, ours abroad, the facts, the mod's side).");
        Console.WriteLine($"Checked {FeastTests.Run(root, Check)} things about feasts with guests (one a season, who may come, their answer, the day and the weather, hopes by their ways, each contest's scoring, the real blow and the sour night at the design's rates, the welcome, the steward, the colony's own away, the mod's side, the words and lines).");
        Console.WriteLine($"Checked {HarmTests.Run(root, Check)} things about hurts from everyday life (the setting and the storyteller, the shared cap, a wound that never takes a part off, drills that bruise, tired hands, working through it at the review's rates, the one service, the decrees that name it, the words).");
        Console.WriteLine($"Checked {RenownTests.Run(root, Check)} things about the colony's renown and the round c words' six fixes (where renown comes from, fading, bands and their hold, known-for, each colonist's own name, what it does, the events it draws, the houses gone and an older save, the words and facts, the mod's side, the talk and lines; levies by their outcome, help by what is sent, moot answers under their own tags; the shipped lines asked for more or retired, the fallen band's bubble, the colony's talk, letters and bubbles).");
        Console.WriteLine($"Checked {NatureTests.Run(root, Check)} things about against their nature (the switch, the heart's new kinds, the scaling, one act a day, who each act weighs on, the marks, the words, where each act happens, the heart's page and the council's questions; the round d words: every told line in two wordings or more, the kin had to let them go, a truce by word heard of, a pact's call refused).");
        Console.WriteLine($"Checked {FealtyTests.Run(root, Check)} things about fealty and vassals (who may swear, their answer and offer, who objects, tribute and the owed clock, levies, protection, renouncing, protected towns, tribute by level, dues, a vassal's levies, asks and release, the facts, the mod's side, the words and lines).");
        Console.WriteLine($"Checked {MatchTests.Run(root, Check)} things about marriage alliances (who may marry, the colonist asked first, their answer and their offer, the wedding day, gifts, while it holds, its ends and the bad sides, the codes, the facts, the mod's side, the words and lines).");
        Console.WriteLine($"Checked {FollowUpTests.Run(root, Check)} things about the follow-ups and the memory of single colonists (the warlord's son by his father's end, the town that grew, a camp found another way, marks, fading, friend and foe, a trader's refusal).");
        Console.WriteLine($"Checked {DefenseTests.Run(root, Check)} things about the defense planner (its plans in every turn, reading a map's ways in).");
        Console.WriteLine($"Checked {DefenseLineTests.Run(root, Check)} things about defense lines (the way round, the steps, strength and lessons).");
        Console.WriteLine($"Checked {CoverDragTests.Run(root, Check)} things about taking cover with a drag (cells, order, never off the map).");
        Console.WriteLine($"Checked {WeatherTests.Run(root, Check)} things about the weather record (days, records, seasons and their names, what events ask).");
        Console.WriteLine($"Checked {WeatherDynamicsTests.Run(root, Check)} things about weather that changes like weather (every land and combination, a hundred years on eight lands).");
        Console.WriteLine($"Checked {ColonyTraitTests.Run(root, Check)} things about what a colony grows into and the names of its years.");
        Console.WriteLine($"Checked {WhatsNewTests.Run(root, Check)} things about What's new (the page, CHANGELOG.md and the version).");
        Console.WriteLine($"Checked {LogLinesTests.Run(root, Check, out int logLines)} things about the {logLines} log lines added from separate files.");
        Console.WriteLine($"Checked {MoodSortTests.Run(root, Check)} things about the compact Mood tab (its groups, rows and every hint's short action).");
        Console.WriteLine($"Checked {StyleSurroundingsTests.Run(root, Check)} things about which things have the wrong style (grouped by kind and style, in order, the game's threshold, and N more, the fix line, the button, a thing's line, the seams).");
        Console.WriteLine($"Checked {OutsiderTests.Run(root, Check)} things about talking with outsiders (waits, talks, bonds over visits, gifts, favours, faith, bouts, duels, their lines and keys).");
        Console.WriteLine($"Checked {OutsiderTalkTests.Run(root, Check)} things about conversations with outsiders (patience, asking again, talks of their own, pieces, callbacks, the library).");
        Console.WriteLine($"Checked {OutsiderAutoTests.Run(root, Check)} things about the right-click's things done by the colonists themselves (which on which setting, the moment, the pace, the budget, what each brings, who does it, the seams).");
        Console.WriteLine($"Checked {FactsTests.Run(root, Check)} things about the facts service and the one fill (scenes, providers, the same choices over generated talks, the same text as every fill it replaced).");
        Console.WriteLine($"Checked {ShelfReserveTests.Run(root, Check)} things about reserved shelf slots (making room for them, by order and on their own, and the seams) and where drinks are had.");
        Console.WriteLine($"Checked {GuestHelpTests.Run(root, Check)} things about guests helping out (work they love, who won't, the colony's troubles and who pitches in, the verdict narrowing only in everyday times, thanks paid in work, the day's limits, the regard both ways, every name in the player's mods, the seams).");
        Console.WriteLine($"Checked {LetterCarryTests.Run(root, Check)} things about the letter carried and read (how it leaves and what its message says, the writer's walk to the mailbag and its fallback, the mailbag's sections, the pop-up's click, the player's save, the seams).");
        Console.WriteLine($"Checked {RiderPackTests.Run(root, Check)} things about the colony's own rider's supplies (the trip, the food, medicine and feed by the journey planner's rules, when the send is greyed or sent short knowingly, the words, gathering, the road and home, the seams).");
        Console.WriteLine($"Checked {CarrierTests.Run(root, Check)} things about carriers on the map (the mailbag first, each in their place, the nearest way in, who a people sends and on what beast for every one of Rimfolk's peoples, back in the saddle before leaving, the seams).");
        Console.WriteLine($"Checked {LetterRouteTests.Run(root, Check)} things about whom a letter goes to and the places heard of (every purpose's scope, who answers it for every kind of addressee, passing a town's matter on and the go-between's word, the window's words, an older save's letter, the marks' keep time and fading, Show on map, the seams).");
    }
}

// Rimfolk's inspect tabs fit above the inspect pane on small screens, big interface scales and RimHUD's taller pane.
Console.WriteLine($"Checked {UiTests.Run(Check)} tab sizes.");
Console.WriteLine($"Checked the calendar's dates over {CalendarTests.Run(Check)} days.");
Console.WriteLine($"Checked {LeanTests.Run(Check)} things about the clock's spread and the save caps.");
Console.WriteLine($"Checked {HomesTests.Run(Check)} things about what a colony is (the order of the homes, the seat, whose home, a day to make it theirs, where a memo goes).");
Console.WriteLine($"Checked {ColonyStateTests.Run(Check)} things about a council per colony (an old save's for the seat, a home lost, kept, carried or moved to, each colony's own people and guards).");
Console.WriteLine($"Checked {ManyHomesTests.Run(Check)} things about many homes (memos and letters by colony, a calendar per colony, camps per home, names and suggestions per colony).");
Console.WriteLine($"Checked {LedgerTests.Run(Check)} things about the world ledger (refs and their text form, ties' states and ends, bounds, ages).");
Console.WriteLine($"Checked {LedgerMovesTests.Run(Check)} things about the stores moved into the world ledger (persons, towns, grudges, favours, words, claims, debts, fealty, regard, keeping someone in the world).");
Console.WriteLine($"Checked {MemoryTests.Run(Check)} things about what the peoples remember (fading, adding up, making way, tone).");
Console.WriteLine($"Checked {PlayOutTests.Run(Check)} things about answers playing out on the map.");
Console.WriteLine($"Checked {PlayOutGroupTests.Run(Check)} things about work parties, walks, vigils, searches, errands and hunts.");
Console.WriteLine($"Checked {CourierTests.Run(Check)} things about goods and people that come and go on the map (who comes, how many, when, what's left).");
Console.WriteLine($"Checked {NotableTests.Run(Check)} things about the neighbours by name and letters to them (who is in reach, regard, answers, calls, the road, old age).");
Console.WriteLine($"Checked {OnMapTests.Run(Check)} things about what answers make real (felled wood, saplings, the era's things, a visitor's dog, an animal in decline).");
Console.WriteLine($"Checked {VisitorTests.Run(Check)} things about people from outside who are really there (how many, when, a night's stay, the wait at the gate, the way on).");
Console.WriteLine($"Checked {CampLookTests.Run(Check)} things about camps that look lived-in (a handful of animals, fed by the camp, porters, the last walk, the smith's mending, a ring of saplings).");
Console.WriteLine($"Checked {CampWeatherTests.Run(Check)} things about the weather camps come in and stay through.");
Console.WriteLine($"Checked {CampStoryTests.Run(Check)} things about camps that come back, the shape of a stay and war parties.");
Console.WriteLine($"Checked {RegularTests.Run(Check)} things about visitors who come back.");
Console.WriteLine($"Checked {RivalTests.Run(Check)} things about the other settlers' year.");
Console.WriteLine($"Checked {RegionMapTests.Run(Check)} things about the region map (the land laid flat at its true distance and bearing, tiles without gaps, zoom and pan, colours, standings).");
Console.WriteLine($"Checked {JourneyTests.Run(Check)} things about journeys planned ahead (the default dawn, the night before shifted, the last hours, the packing list, travel food, how long the food and feed last, the list's groups and counts, clothes for the road, the way out).");
Console.WriteLine($"Checked {JourneyAdviceTests.Run(Check)} things about who to bring on a journey and what to pack (the danger, the party's size, each colonist's place and reason, the home left behind, pack animals and mounts, medicine, silver and trade goods, the stores or the stall).");
Console.WriteLine($"Checked {RoadPostTests.Run(Check)} things about the wardens' post as a trading stop.");
Console.WriteLine($"Checked {HerdStoryTests.Run(Check)} things about the herd's year and the beast that stays.");
Console.WriteLine($"Checked {MovingOnTests.Run(Check)} things about the price of keeping someone who asked to move on.");
Console.WriteLine($"Checked {GoalRewardTests.Run(Check)} things about what a reached life goal leaves behind.");
Console.WriteLine($"Checked {BarMoreTests.Run(Check)} things about the bottom bar's buttons put away under More.");
Console.WriteLine($"Checked {GuestCheckTests.Run(Check)} things about whether a guest can be recruited, and how.");
Console.WriteLine($"Checked {NextStepsTests.Run(Check)} things about the colony's next steps.");
Console.WriteLine($"Checked {SpotTests.Run(Check)} things about where a room plan should go.");
Console.WriteLine($"Checked {RoomArrangeTests.Run(Check)} things about tidying a built room.");
Console.WriteLine($"Checked {BaseReviewTests.Run(Check)} things about the base looked over.");
Console.WriteLine($"Checked {GroundPlanTests.Run(Check)} things about the ground plan.");
Console.WriteLine($"Checked {StorageReviewTests.Run(Check)} things about the stores looked over.");
Console.WriteLine($"Checked {MakingTests.Run(Check)} things about what the colony could make.");
Console.WriteLine($"Checked {AutomationTests.Run(Check)} things about the council's automations.");
Console.WriteLine($"Checked {ChainTests.Run(Check)} things about production chains followed to the end.");
Console.WriteLine($"Checked {AheadTests.Run(Check, Environment.GetEnvironmentVariable("RIMFOLK_AHEAD_PRINT") == "1")} things about planning ahead (stocks, bills, fields) and the shared suggestions.");
Console.WriteLine($"Checked {GrowthTests.Run(Check, Environment.GetEnvironmentVariable("RIMFOLK_GROWTH_PRINT") == "1")} things about building ahead (rooms the colony will be short of, the builders' days, stages, room to grow).");
Console.WriteLine($"Checked {AheadMoreTests.Run(Check)} things about planning ahead further (Colony Manager's targets, the shopping list, rock to mine, spare clothes, hay, quest joiners).");
Console.WriteLine($"Checked {LifeNewTests.Run(Check)} things about the newer wishes and life moments (a crop from home, the morning cup, books read, the way to the table).");
Console.WriteLine($"Checked {ImpactTests.Run(Check)} things about what each answer does (chances, merging, the short line).");
Console.WriteLine($"Checked {AnswerShiftTests.Run(Check)} things about what answers change for a while (hours, work, a new limb).");
Console.WriteLine($"Checked {ConduitTests.Run(Check)} things about laying a conduit to a piece off the power.");
Console.WriteLine($"Checked {LineRouteTests.Run(Check)} things about lines laid again under the walls.");
Console.WriteLine($"Checked {GuestWillTests.Run(Check)} things about guests who'd stay (ties at home, what they want, the cost, family asking them back).");
Console.WriteLine($"Checked {KeptTests.Run(Check)} things about granted wishes taken back (grace, timing, making it right).");
Console.WriteLine($"Checked {SeasonWishTests.Run(Check)} things about the wishes of the land's year (clothes for the season, a sauna, hot country, the fields opening).");
Console.WriteLine($"Checked {HeartNeedTests.Run(Check)} things about the heart's needs (what follows from them, and a lack that lasts).");
Console.WriteLine($"Checked {BeliefTests.Run(Check)} things about beliefs and diets (precepts, a shared bed, what may be served).");
Console.WriteLine($"Checked {AdvisorTests.Run(Check)} things about the advisor's newer checks (missions, decrees, winter stores, rooms outgrown, light).");
Console.WriteLine($"Checked {AnimalTests.Run(Check)} things about the colony's animals (age, the herd, a cull, the calendar, PokéWorld's levels and Pokédex).");
Console.WriteLine($"Checked {BarBadgeTests.Run(Check)} things about the colonist bar's badges (break risk, what wins, where they sit).");
Console.WriteLine($"Checked {HudTests.Run(Check)} things about the on-screen panels (what matters most first, places on any screen, snapping, rows kept).");
Console.WriteLine($"Checked {HudPanelTests.Run(Check)} things about the alerts, health, and work and idle panels (the health filter, the race, orders, starting places).");
Console.WriteLine($"Checked {HudLayerTests.Run(Check)} things about the panels beneath the game's interface, health details and the inventory (tab row, places, healing, recovery, stock order and change, grid).");
Console.WriteLine($"Checked {BodyTests.Run(Check)} things about what a colonist's own body changes (the cold and the heat, barely eating, armour in the skin, the water a thalassophobe minds).");
Console.WriteLine($"Checked {DrinkBeforeWorkTests.Run(Check)} things about a drink before work for Alpha Skills' drunken passion (how often, how near, which work, tolerance).");
Console.WriteLine($"Checked {SmokeBeforeWorkTests.Run(Check)} things about a smoke before work for Alpha Skills' stoned passion (tolerance, a mild high, the rules both rows share).");
Console.WriteLine($"Checked {MedicalTriageTests.Run(Check)} things about medical triage with Move the Patient (the best bed, the surgery bed, who outranks whom, who may be moved, the cooldown).");
Console.WriteLine($"Checked {MedicineChoiceTests.Run(Check)} things about medicine by what is treated (harmless, minor and serious on clear cases, never above the medical care, the tendings and their medicine, the order, the job's end, Smart Medicine, the seams).");
Console.WriteLine($"Checked {BedRestTests.Run(Check)} things about bed rest only when it makes sense (the thresholds, wounds, the game's and mods' illnesses, the urgent cases, orders and the decree, sleeping a little more, Rimfolk's ailments stage by stage, the seams).");
Console.WriteLine($"Checked {FieldWeatherTests.Run(Check)} things about fields by the weather (growth by the hour, the plants' own cold limits, the 60% rule, sowing again, the frost harvest).");
Console.WriteLine($"Checked {HandOverTests.Run(Check)} things about handing a colony over (the checks, the head and the second, the vote, the people and its relations, the town's fortune, the bond, the peoples' defs, the words, the seams).");
Console.WriteLine($"Checked {TownAfterTests.Run(Check)} things about the town a colony was, after the hand-over (the kept layout and its size, the head's letters, its days, homecomings and moving there, taking it back both ways, the seams).");
Console.WriteLine($"Checked {ScenarioStartTests.Run(Check)} things about a scenario's start for a new colony (which parts come, Bad Blood's families never, the arrival and the gravship, the people and what is required of them, the passage with the kit, Rimfolk's own parts, the seventeen scenarios, the seams).");
Console.WriteLine($"Checked {TownLifeTests.Run(Check)} things about the town's own life (a raid on it, its hard times and its people at the gate, its goodwill cooling, the region's hardships, growth at Faction Territories' pace, the seams).");
Console.WriteLine($"Checked {ManyColoniesTests.Run(Check)} things about many colonies in play (two councils become one, each colony its own character, neighbours and world events near every home, the pages following the colony in view, the small ones, the seams).");
Console.WriteLine($"Checked {HarmTwoTests.Run(Check)} things about harm, parts 2 and 3 (a camp's brawl, sickness brought home, the champion's bout, hunts that can turn, a rival's sabotage, the hustings' blows, frayed tempers, the one service, the words).");
Console.WriteLine($"Checked {HarmBudgetTests.Run(Check)} things about the shared budget of hurts from everyday life (the variety, every kind registered once, the passes sharing the budget, a later kind plugging in, the setting and the storyteller as before, every roll naming its kind, the memory saved).");
Console.WriteLine($"Checked {HarmSevenTests.Run(Check)} things about harm, part 7 (the sit-down, the speaker, what went to someone else, the meeting, friends stand by, to their face, the wronged one, the wrong friend, rivals in love, the altar, the feast's fists, the second round, the soldier's nightmare, the open gate, the gambler, the barber, the guest of rank, the crowd, first blood, the letter's challenge, the heir, the hated people, zeal at the gate, hungry hands, the creek, the nemesis, the worst night, first steps, the working, the lost child, the courier, the one service, the words).");
Console.WriteLine($"Checked {HarmSixTests.Run(Check)} things about harm, part 6 (hot and heavy answers, back too soon, the last push, clumsy when worn, the makeshift spot, taking down, hurried hands, a strong back, sharp habits, the second drink, the hands home, the drovers' bull, too close, the dawn walk, the bold wolf, a catch, the visitor at the stores, the spark, the muster, the spar, the dare, a coward's name, the fury, the one service, the words).");
Console.WriteLine($"Checked {HarmFiveTests.Run(Check)} things about harm, part 5 (the sick work on, the gate, the camp's fire, a courier, the patient, the bad batch, small things, dirty water, the gate's cold, the night watch, weather lovers, sleepwalking, the restless walk, cold water, the forecast, the one service, the words).");
Console.WriteLine($"Checked {HarmFourTests.Run(Check)} things about harm, part 4 (the physician, the guest's cough, the hail, a cold night, felling, a failed build, the hot bench, the rut, the herdmaster's lesson, the saddle, the groups, the sore loser, a guest's night, the rematch, the games, the one service, the words).");
Console.WriteLine($"Checked {StageThreeWordsTests.Run(Check)} things about stage 3's words (the town the colony was, its raid's letters, the founders and the newcomers, the letters' fellows, the shipped lines narrowed, no draw added).");
Console.WriteLine($"Checked {SchemesTests.Run(Check)} things about schemes against the colony (who schemes, how often, the acts by every age, who catches them, unknown until found out, the words).");
Console.WriteLine($"Checked {PeoplesTests.Run(Check)} things about Rimfolk's own peoples (the callings and their tags, the weights, the titles, the towns' plans by the game's footprint rule, the seven defs and their softness, the asks, callers and camp, the patch, the words, the emblems, the seams).");
Console.WriteLine($"Checked {AnimalPowersTests.Run(Check)} things about the colony's animals with powers (the Pokémon types at home and their bounds, familiars, omens and their signs, sacred animals, the clock's slot, the settings, the defs, the saved book, the dev actions).");
Console.WriteLine($"Checked {AnimalNatureTests.Run(Check)} things about the animals' natures (the nine temperaments and their leans, the fixed pick, how often, friends and rivals, grief, an old animal knowing its handler, who speaks, no pop-ups, the pools, the hooks, the book).");
Console.WriteLine($"Checked {ArisingTests.Run(Check)} things about peoples that arise during play (the four causes and when each is due, the pace and its setting, the league's towns, the template in every age, their goodwill, colour and name, the words, the region's day, the one book saved, the leader after the faith, the dev actions).");
Console.WriteLine($"Checked {PeoplesCTests.Run(Check)} things about release C's peoples (the pilgrims, the scholars, the enclave and the river folk: the ages they arise in, the pilgrims' night, the lander, the docks, their defs, plans, callers and asks, the patch, release B's arising with them, their page, the seams).");
Console.WriteLine($"Checked {PeoplesETests.Run(Check)} things about release E's peoples (the Stormglass Conclave, the Mossborn, the Ashen Banner and the Kiln Guild: the watch on the sky, the rod and the glass, the ledger of the colony's trees, the saplings and the grave's tree, the squad's wages and contract, the guild's pay by quality and its bids, their defs, the Banner's one kit, arms for another age, plans, callers and asks, the squad's camp, the patch, release B's arising with them, their page, the seams).");
Console.WriteLine($"Checked {PeoplesFTests.Run(Check)} things about release F's peoples (the Signal Line, the Deepwell Union, the Last Republic and the Hollow Fleet: the relay mast, its letters and its warning, the wire-trader, the union's drill and its share, the assayer's crate, the strike, the census, the papers, the conscript's season, the radio, the shuttle, the salvage, the sleeper woken too late, their defs, one kit where they have soldiers, arms for another age, plans, callers and asks, the patch, release B's arising with them, their one harm, their page, the seams).");
Console.WriteLine($"Checked {PeoplesGTests.Run(Check)} things about release G's peoples (the Helix Works, the Lattice, the Choir of the Spire and the Sunken Vault: the sample and its little draw, gene work, the vat-keeper's case, the batch gone wrong, the mechs mended, the lost mech, the part-trader, the turret, the listening's calm and unease, art for the Spire's goods, the listener's stay, the vault's decline unless supplied, its barter, its lost craft, the child who never saw a sky, their defs, their looks, arms for another age, plans, callers and asks, the patch, release B's arising with them, no harm rolled, their page, the seams).");
Console.WriteLine($"Checked {PeopleEventsTests.Run(Check)} things about the peoples' own events, round 1 (the herders' beast, the order's limb, storm-sense, the mechlink, the Mossborn's seed and its promise, the league's crates in trust and the book's verdict, the scholars' reading of an old place, the pilgrims' hostel, the Wardens' lantern post, the hunt or the taming; gifts once a colonist and spaced out, their defs and words, the player's mods, the shared shapes and the seams).");
Console.WriteLine($"Checked {PeoplesDTests.Run(Check)} things about release D's peoples (the Lantern Wardens, the Quiet Bell, the Circuit of Tamers and the Wheel: the age a people lives in, the hunt and the pelts, the bell and its funerals, the bout, the eggs and the strays, the scrap, the wreck and the towns that move, their defs, arms for another age, plans, callers and asks, the patch, release B's arising with them, the bout's harm, their page, the seams).");
Console.WriteLine($"Checked {PeoplesLoreTests.Run(Check)} things about the seven peoples brought to the level of the new ones (their founding stories, sayings, honour and fear on their card, their voices in every pool, their new touchpoints wired, their faiths soft and sound, their names for peoples that arise, their pages, the seams).");
Console.WriteLine($"Checked {PeoplesGearTests.Run(Check)} things about the peoples' gear, kinds of people and gifts (no fixed colour, one signature each, variety and the soldiers' kits, every mod's name soft, real and in the player's active mods by their catalogue, the signature not taken by a mod's piece, nothing of another calling, the base game's fallback, what traders carry, the mix of kinds of people, the gifts' rules, the seams).");
Console.WriteLine($"Checked {PeopleAgesTests.Run(Check)} things about peoples whose age changes (the one rule: its pace, limits and floor, the colony's trade, gifts, teaching and alliance, being cut off, the Sunken Vault's way, the traders' caps, what counts as a gift of learning; each people's pace and limit; the years played out: {PeopleAgesTests.Summary}; every age a people can reach dressed and armed from the player's mods; the seams).");

Console.WriteLine($"Checked {HaulTests.Run(Check)} things about carrying and hauling (what fits, worth a trip, the long material run and its build area, the others' cover, things on the way, what is kept, what bills, production buildings and other work make and the meals left in reach, the standalone Harvest and Haul's settings and loads, the hooks that stand down, the job, the words).");
Console.WriteLine($"Checked {ThingRolesTests.Run(Check)} things about things by what they are for (a drink for a toast, a feast's dish, provisions, produce, medicine, precious things, carvings, gifts and blades: the rules, the ages and the giver's ways, the pick and the count by worth, several of each for every age in the player's own mods, what the colony would find wrong left out, the seams).");
Console.WriteLine($"Checked {WeaponRateTests.Run(Check)} things about the weapon rating and its tags (the game's rules for hitting, armour, explosions and the melee choice, the reference fighter and enemies, the roles, the scale, the base game's weapons in the order players know them, specials, passives, abilities, the mods' own rules read from their code, the tag vocabulary, its colours and its rules).");
Console.WriteLine($"Checked {WeaponLiveTests.Run(Check)} things about the weapon card and the weapon in hand (the game's reading giving the review's ratings for the base game's weapons, quality and stuff, unique and persona traits, biocoding, the game's curves read backwards, the rating for a colonist and why it differs, Simple Sidearms' pair, Dual Wield's off hand, RunAndGun on the move, the chart and the colours, the seams).");
Console.WriteLine($"Checked {WeaponPickTests.Run(Check)} things about the best weapon for each colonist and colonists who ask (the threshold, one assignment for the whole colony near its best total, what stops a suggestion, trades better for both or worth more to the colony by how much each fights, a lucky weapon or an heirloom said and counted a little, sidearms and off hands, the colony's fights from its raids, a hunter, passions and faith, the reasons, when a colonist asks or proposes a trade, who should carry a weapon, the pools' lines in every age, Rimfolk's card first, the seams).");
Console.WriteLine($"Checked {WeaponKitTests.Run(Check)} things about a full kit for every colonist (one ranged and one melee weapon with Simple Sidearms, the off hand with Dual Wield: the kit as it stands, the restrictions that decide it, an empty slot filled under the usual threshold but never with something nearly useless, by its part in the kit, from storage, their own pack or another's spare, the kit before anyone's upgrade, forbidden weapons only asked for, what was looked at and why, the lines in every age, the seams).");
Console.WriteLine($"Checked {WeaponShowTests.Run(Check)} things about the weapon you can see (the item's picture and its quality's colours, the suggestions in Nice Inventory Tab's equipment box at every width it allows, the room it makes and where the row goes, the seams).");
Console.WriteLine($"Checked {ApparelLiveTests.Run(Check)} things about the apparel card and the piece in hand (the game's reading giving the review's ratings for the base game's armour, helmets, belts, packs, weather gear, hats and clothes, quality, wear and the moods it gives, a dead wearer, biocoding, the rating for a colonist and why it differs by how they fight, their body, their weather and the moods it adds, protection by region, the seams).");
Console.WriteLine($"Checked {ApparelPickTests.Run(Check)} things about better armour and clothes for each colonist (the thresholds, the cold and heat that count, the raids met, work, pieces that fit together, Bautista at -25 °C in a ratty shirt, the outfit never passed and said with a way to allow it, the rules of title, role and faith, locked pieces, lucky items and heirlooms warned of, armour and helmets for the fighters of a raid-heavy colony, the coldest and the fighters first, the threshold and worn-out pieces, a piece judged in each replaced one's role, never leaving them short of warmth, trades of armour, pack and spares, random colonies, the asks, who should wear a piece, the base game's pieces in another role, the lines in every age, the seams).");
Console.WriteLine($"Checked {ItemCardTests.Run(Check)} things about Rimfolk's item card for food, drink, drugs and medicine (the kind of a thing, its tags in the shared six categories with why, the line in plain words, colonist-days, suppers and a meal's worth, tending with and without medicine, the colony short or with spare, who won't have it, the seams: the window hook and its pass-through, the button on the game's card, the setting, the frame, the keys, the model refreshed rarely).");
Console.WriteLine($"Checked {ItemCardStepTwoTests.Run(Check)} things about Rimfolk's item card for materials and art (the kinds, a stuff against steel and wood or the colony's most held of its kind, the worth of what is made of it, repairs, fuel, short and spare, the tags and the line for the base game's metals, wood, stone, cloth, leather, chunks, components and chemfuel, the game's room sums and a grand sculpture at several qualities with it and without it, placed, outdoors and in storage, a noble's room, the seams).");
Console.WriteLine($"Checked {ItemCardStepThreeTests.Run(Check)} things about Rimfolk's item card for everything else (the kinds and their order, the game's experience curve and a neurotrainer's levels, a book's experience an hour, hunger by metabolism, a surgery's chance, who at home gains most, the tags and the line for a novel, a textbook, a schematic, a tome, a bionic arm, a natural kidney, a peg leg, a techprint, a shooting neurotrainer, a psytrainer, a genepack, a xenogerm, a psychic shock lance, a psychic animal pulser, a healer mech serum, a high-explosive shell, a minified armchair and bed, an unfinished thing and a plain item, the seams).");
Console.WriteLine($"Checked {ApparelRateTests.Run(Check)} things about the apparel rating and its tags (the game's armour roll layer by layer, the human body's zones, the reference enemies, shields and powers, the roles and their scales, the base game's armour, helmets, utility, weather gear and clothes in the order players know them, stuff, a shooter and a melee fighter, the tag vocabulary, its colours and its rules).");
Console.WriteLine($"Checked {BeastsTests.Run(Check)} things about the wild's alphas and bosses (the settings' rates, a pack's alpha, a beast nemesis that flees and comes back and who swears against it, legends of the region, great herds and their stampedes, the defs and words, the downed rule, the land's animals, the clock, the dev actions).");
Console.WriteLine($"Checked {GodPresenceTests.Run(Check)} things about a god's presence (the fourteen domains and their looks in every age, the aura by day and night, the growth at a reveal, the moment's timings, the sounds and the track, who answers a god and how, the tells of one in disguise, the trail and the mark, every name in the player's mods, the hediff, jobs, sky and card, the lines said aloud, the words, the settings, the textures, the seams).");
Console.WriteLine($"Checked {GodsOnMapTests.Run(Check)} things about the gods on the map (how often a god comes and as what, the omens before, the stranger's stay judged, the kindest and their gift, the sacred animal and where it leads, the weather a god answers in, the marks remembered and visited, the shapes, the defs, the lines said aloud, the words, the seams, the player's mods).");

Console.WriteLine(failures == 0 ? "All tests passed (template math, layout, strain, meetings, pressures, ailments, line memory, good times, tab sizes, calendar, personal stories, clock and caps, faction memory, answers on the map, setbacks, promises kept, what colonists say, the heart's needs, beliefs and diets, animals, other mods)." : failures + " failure(s).");
return failures == 0 ? 0 : 1;
