namespace ColonyCouncil
{
    /// <summary>
    /// Pure integer geometry for rotating room templates. No game types, so it can be unit-tested.
    ///
    /// A template lives in "full" coordinates: (0,0) is the bottom-left wall corner, the full rect is
    /// W x H (interior + walls). Rotation is in clockwise quarter turns, viewed from above with +z north.
    /// </summary>
    public static class TemplateMath
    {
        public static int Norm(int rot) => ((rot % 4) + 4) % 4;

        public static void RotatedSize(int w, int h, int rot, out int rw, out int rh)
        {
            if (Norm(rot) % 2 == 0)
            {
                rw = w;
                rh = h;
            }
            else
            {
                rw = h;
                rh = w;
            }
        }

        /// <summary>Rotates a point in a W x H rect clockwise <paramref name="rot"/> times; result is in the rotated rect.</summary>
        public static void RotatePoint(int x, int z, int w, int h, int rot, out int rx, out int rz)
        {
            rx = x;
            rz = z;
            int cw = w, ch = h;
            for (int i = 0; i < Norm(rot); i++)
            {
                int nx = rz;
                int nz = cw - 1 - rx;
                rx = nx;
                rz = nz;
                int t = cw;
                cw = ch;
                ch = t;
            }
        }

        /// <summary>Rotates an axis-aligned rect (min corner + size) and returns the rotated min corner and size.</summary>
        public static void RotateRect(int minX, int minZ, int sizeX, int sizeZ, int w, int h, int rot,
            out int rMinX, out int rMinZ, out int rSizeX, out int rSizeZ)
        {
            RotatePoint(minX, minZ, w, h, rot, out int ax, out int az);
            RotatePoint(minX + sizeX - 1, minZ + sizeZ - 1, w, h, rot, out int bx, out int bz);
            rMinX = ax < bx ? ax : bx;
            rMinZ = az < bz ? az : bz;
            rSizeX = (ax > bx ? ax - bx : bx - ax) + 1;
            rSizeZ = (az > bz ? az - bz : bz - az) + 1;
        }

        /// <summary>Rotation of a facing (0=N,1=E,2=S,3=W) after turning the template clockwise.</summary>
        public static int RotateFacing(int facing, int rot) => Norm(facing + rot);

        /// <summary>A facing seen in a mirror that flips west and east.</summary>
        public static int MirrorFacing(int facing) => Norm(facing) == 1 ? 3 : Norm(facing) == 3 ? 1 : Norm(facing);

        /// <summary>Optional west-east mirror (in template space), then <paramref name="rot"/> clockwise turns.</summary>
        public static void Transform(int x, int z, int w, int h, bool mirror, int rot, out int rx, out int rz)
        {
            if (mirror) x = w - 1 - x;
            RotatePoint(x, z, w, h, rot, out rx, out rz);
        }

        /// <summary><see cref="Transform"/> for an axis-aligned rect.</summary>
        public static void TransformRect(int minX, int minZ, int sizeX, int sizeZ, int w, int h, bool mirror, int rot,
            out int rMinX, out int rMinZ, out int rSizeX, out int rSizeZ)
        {
            if (mirror) minX = w - (minX + sizeX);
            RotateRect(minX, minZ, sizeX, sizeZ, w, h, rot, out rMinX, out rMinZ, out rSizeX, out rSizeZ);
        }

        public static int TransformFacing(int facing, bool mirror, int rot) => RotateFacing(mirror ? MirrorFacing(facing) : facing, rot);
    }
}
