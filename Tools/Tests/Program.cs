using ColonyCouncil;

int failures = 0;
void Check(bool ok, string what)
{
    if (!ok) { failures++; Console.WriteLine("FAIL " + what); }
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

Console.WriteLine(failures == 0 ? "All template math tests passed." : failures + " failure(s).");
return failures == 0 ? 0 : 1;
