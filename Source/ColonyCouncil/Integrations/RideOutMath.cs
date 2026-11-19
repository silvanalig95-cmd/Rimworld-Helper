using System;

namespace ColonyCouncil
{
    /// <summary>What kind of trip a colonist is setting out on, for riding out (<see cref="RideOutMath.Kind"/>).</summary>
    public enum RideTrip
    {
        /// <summary>Not one for Rimfolk: a short trip, or one that stays inside the base (Giddy-Up's own rules only).</summary>
        None,
        /// <summary>To work out past the base: they ride there, the beast waits, and they ride back, so the way back counts too.</summary>
        Out,
        /// <summary>From out past the base to somewhere else (home, most often): one way.</summary>
        Back,
        /// <summary>The player's own movement order to a drafted colonist: one way, and only when riding gets them there sooner.</summary>
        Order
    }

    /// <summary>
    /// Riding out to far work and back with Giddy-Up 2 (Integrations/GiddyUpRideOut.cs; the player, 15 November 2026: "pawns
    /// should really use mounts whenever they walk away out of the base range ... it should mount to go there, and mount
    /// back up aftr finishing work"). Pure: which trips count, and whether riding is worth it against walking, by the same
    /// sums Giddy-Up makes for its own auto-mount (a straight line with its 5% penalty for paths that aren't straight, the
    /// walk to the beast, the time to get on), with the way back counted for work out past the base.
    /// <para>Since 19 November 2026 (the player: "WHY DOES bautista still walk halfway across the map to mine without
    /// mounting up first? then the mount should wait for him until he's done"): out to work they ride whenever a beast is
    /// free no further from them than the work (<see cref="NearEnough"/>), the quickest of them; the sums against walking
    /// stay for the way back and the player's movement orders (<see cref="Rides"/>).</para>
    /// </summary>
    public static class RideOutMath
    {
        /// <summary>
        /// The shortest trip worth riding, in cells in a straight line from the colonist to the work (plus a haul's carry).
        /// Giddy-Up's own default is 120, about half the player's 275-cell map, which is why Avila walked to her steel.
        /// </summary>
        public const float TripMin = 40f;

        /// <summary>Giddy-Up's own default for "Find a mount if trip is longer than..." (its minAutoMountDistance), in cells.</summary>
        public const float GiddyUpTripDefault = 120f;

        /// <summary>Giddy-Up looks for a mount by itself: the trip (and a haul's carry) longer than its setting, never for a drafted colonist.</summary>
        public static bool GiddyUpLooks(float trip, float setting, bool drafted) => !drafted && trip > setting;

        /// <summary>How far past the home area's edge the work (or the colonist) must be to count as out past the base.</summary>
        public const int OutMin = 10;

        /// <summary>Giddy-Up's own penalty for paths that aren't straight (MountUtility.GetBestAnimal).</summary>
        public const float PathFactor = 1.05f;

        /// <summary>
        /// Out to work and back, riding may take up to a quarter longer than walking: the player wants them in the saddle
        /// out past the base, and the beast waiting by the work makes every later trip back and forth quicker.
        /// </summary>
        public const float WorkSlack = 1.25f;

        /// <summary>A movement order rides only when it gets them there sooner: the player wants them where they sent them.</summary>
        public const float OrderSlack = 1f;

        /// <summary>The longest a beast is kept waiting for its rider out past the base after the rider's last job began, in ticks (a day).</summary>
        public const int WaitCap = 60000;

        /// <summary>A beast another rider just tried and failed to get on is passed over for this long, in ticks (an hour).</summary>
        public const int RefusedTicks = 2500;

        /// <summary>Out past the base: this many cells or more past the home area's edge.</summary>
        public static bool FarOut(int cellsOut) => cellsOut >= OutMin;

        /// <summary>
        /// The kind of trip: none when it is shorter than <see cref="TripMin"/> or stays inside the base (neither the work,
        /// nor where a haul goes, nor the colonist out past it); a movement order for a drafted colonist; out to work when the
        /// job ends out past the base (the way back follows); else back from out there.
        /// </summary>
        public static RideTrip Kind(float trip, int riderOut, int targetOut, int finalOut, bool order)
        {
            if (float.IsNaN(trip) || trip < TripMin) return RideTrip.None;
            if (!FarOut(riderOut) && !FarOut(targetOut) && !FarOut(finalOut)) return RideTrip.None;
            if (order) return RideTrip.Order;
            return FarOut(finalOut) ? RideTrip.Out : RideTrip.Back;
        }

        /// <summary>How much longer than walking riding may take for this kind of trip.</summary>
        public static float Slack(RideTrip kind) => kind == RideTrip.Order ? OrderSlack : WorkSlack;

        /// <summary>
        /// Seconds on foot: the trip (and a haul's carry), and for work out past the base the walk back as well.
        /// <paramref name="back"/> is the distance from where the job ends to where the colonist set out.
        /// </summary>
        public static float WalkSeconds(float trip, float carry, float back, float walkSpeed, RideTrip kind)
        {
            if (walkSpeed <= 0.01f) return float.MaxValue;
            return (trip + carry + (kind == RideTrip.Out ? back : 0f)) / walkSpeed;
        }

        /// <summary>
        /// Seconds riding: the walk to the beast (Giddy-Up's own power of 1.05 for a longer walk), getting on, and the ride
        /// from the beast to the work (and a haul's carry), with the ride back for work out past the base.
        /// </summary>
        public static float RideSeconds(float toAnimal, float animalToTarget, float carry, float back, float walkSpeed, float rideSpeed, float mountSeconds, RideTrip kind)
        {
            if (walkSpeed <= 0.01f || rideSpeed <= 0.01f) return float.MaxValue;
            float walk = (float)Math.Pow(Math.Max(0f, toAnimal), PathFactor) / walkSpeed;
            float ride = (animalToTarget + carry + (kind == RideTrip.Out ? back : 0f)) * PathFactor / rideSpeed;
            return walk + Math.Max(0f, mountSeconds) + ride;
        }

        /// <summary>Riding is worth it: no slower than walking, by the trip's slack.</summary>
        public static bool Worth(float rideSeconds, float walkSeconds, RideTrip kind) =>
            kind != RideTrip.None && rideSeconds < float.MaxValue && rideSeconds <= walkSeconds * Slack(kind);

        /// <summary>
        /// Out to work, a beast counts when the walk to it is no longer than the trip itself (straight lines, in cells): the
        /// pen on the way, beside them or a little the other way, never a beast further off than the work.
        /// </summary>
        public static bool NearEnough(float toAnimal, float trip) => !float.IsNaN(toAnimal) && !float.IsNaN(trip) && toAnimal >= 0f && toAnimal <= trip;

        /// <summary>Out to work, a beast counts only when it carries them at least as fast as they walk (Giddy-Up passes slower ones over too).</summary>
        public static bool FastEnough(float rideSpeed, float walkSpeed) => rideSpeed > 0.01f && rideSpeed >= walkSpeed;

        /// <summary>
        /// They ride on the quickest beast found: out to work whenever there is one (the beast then waits by the work for the
        /// way back and every trip after); back from out there and on a movement order only when it is worth it against
        /// walking (<see cref="Worth"/>).
        /// </summary>
        public static bool Rides(RideTrip kind, float rideSeconds, float walkSeconds) =>
            kind == RideTrip.Out ? rideSeconds < float.MaxValue : Worth(rideSeconds, walkSeconds, kind);

        /// <summary>
        /// Giddy-Up's mark keeping a beast for a rider holds only while the beast waits for that rider, carries them, or they
        /// are getting on it (or have it next in their queue); otherwise the mark is old and left aside.
        /// </summary>
        public static bool KeptFor(bool waitsForThem, bool carriesThem, bool theyGetOn) => waitsForThem || carriesThem || theyGetOn;

        /// <summary>
        /// Seconds to get on, by Giddy-Up's own curves for its "Mount" job (JobDriver_Mount): more for a big or wild beast,
        /// less for a rider good with animals.
        /// </summary>
        public static float MountSeconds(float bodySize, float wildness, int animalsSkill)
        {
            float size = Lerp(bodySize, 2f, 200f, 4f, 300f);
            float wild = Lerp(wildness, 0.35f, 0.8f, 1f, 2f);
            float skill = animalsSkill <= 8 ? Lerp(animalsSkill, 0f, 1.4f, 8f, 1f) : Lerp(animalsSkill, 8f, 1f, 20f, 0.33f);
            return size * wild * skill / 60f;
        }

        /// <summary>
        /// The waiting beast's job lasts until this many ticks after it began: Giddy-Up's own timer from now (the colonist
        /// still at work out there), but never past <see cref="WaitCap"/> after the colonist's last job began
        /// (<paramref name="lastJob"/>), and never shorter than it was. A wait with no end of its own, or a clock that went
        /// back, is left as it is.
        /// </summary>
        public static int WaitExpiry(int startTick, int now, int expiry, int timer, int lastJob)
        {
            if (expiry <= 0 || now < startTick) return expiry;
            if (lastJob > now) lastJob = now;
            long end = Math.Min((long)now + Math.Max(0, timer), (long)lastJob + WaitCap);
            long wanted = end - startTick;
            // Never set at or below the time already waited: the game ends a job once that time reaches its length.
            if (wanted <= now - startTick) return expiry;
            return (int)Math.Max(expiry, Math.Min(wanted, int.MaxValue));
        }

        /// <summary>A straight piece of one of Giddy-Up's curves, held flat past its ends (as the game's SimpleCurve does).</summary>
        private static float Lerp(float x, float x0, float y0, float x1, float y1)
        {
            if (x <= x0) return y0;
            if (x >= x1) return y1;
            return y0 + (y1 - y0) * (x - x0) / (x1 - x0);
        }
    }

    /// <summary>What came of a decision to ride (for the dev action): asked of Giddy-Up, in the saddle or not.</summary>
    public enum RideMount
    {
        /// <summary>No ride was decided.</summary>
        None,
        /// <summary>Giddy-Up didn't take the order to get on (its Inject gave nothing back).</summary>
        NotTaken,
        /// <summary>Sent to get on; not yet known whether they did.</summary>
        Sent,
        /// <summary>They were in the saddle when they next chose a job.</summary>
        InSaddle,
        /// <summary>They weren't in the saddle when they next chose a job (that beast is passed over by them for an hour).</summary>
        DidNotGetOn
    }

    /// <summary>
    /// A colonist's last decision about riding out on a trip of 40 cells or more, kept in memory for the dev action "Riding
    /// out: why a colonist rides or not (log)" (not saved): when, the job, the kind of trip and its length, why not (null:
    /// they ride), the quickest beast found with its seconds against walking, and what came of it.
    /// </summary>
    public sealed class RideOutNote
    {
        public int tick;
        public string job;
        public RideTrip kind;
        public float cells;
        public string why;
        public string beast;
        public float rideSeconds = float.MaxValue, walkSeconds;
        public RideMount mount;

        /// <summary>The note in plain words, with how long ago it was.</summary>
        public string Describe(int now)
        {
            string when = now >= tick ? ((now - tick) / 2500f).ToString("0.0") + " hours ago" : "later than now (the clock went back)";
            string trip = kind == RideTrip.Out ? "out to work" : kind == RideTrip.Back ? "back from out past the base"
                : kind == RideTrip.Order ? "a movement order" : "not a trip Rimfolk rides";
            string text = "Last decision, " + when + " (tick " + tick + "): " + (job ?? "a job") + ", " + trip + ", " + (int)cells + " cells: ";
            string sums = beast == null ? ""
                : " (" + beast + ": " + (rideSeconds < float.MaxValue ? rideSeconds.ToString("0") + " s riding" : "no sum")
                  + (walkSeconds > 0f && walkSeconds < float.MaxValue ? " against " + walkSeconds.ToString("0") + " s on foot" : "") + ")";
            // "Walking is quicker" already names the beast and both sums.
            if (why != null) return text + "no ride, " + why + (beast != null && why.Contains(beast) ? "" : sums) + ".";
            return text + "they ride" + sums + "; " + MountWords(mount) + ".";
        }

        /// <summary>What came of the order to get on, in plain words.</summary>
        public static string MountWords(RideMount m)
        {
            switch (m)
            {
                case RideMount.NotTaken: return "Giddy-Up didn't take the order to get on";
                case RideMount.Sent: return "sent to get on, not yet known whether they did";
                case RideMount.InSaddle: return "they got in the saddle";
                case RideMount.DidNotGetOn: return "they didn't get on, so that beast is passed over by them for an hour";
                default: return "nothing was asked of Giddy-Up";
            }
        }
    }
}
