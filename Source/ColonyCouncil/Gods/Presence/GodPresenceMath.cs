using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// The kinds of a god's moment (docs/design/gods.md, "Presence"): the same seven the core's PresenceSpec names, so the
    /// seam maps one to the other by name.
    /// </summary>
    public enum PresenceKind
    {
        /// <summary>A god comes onto the map: openly, or as one who is plainly more than a traveller.</summary>
        Appear,
        /// <summary>A god does something where it stands.</summary>
        Act,
        /// <summary>A god in disguise shows who it is: it grows, takes its colours and its light.</summary>
        Reveal,
        /// <summary>A god goes.</summary>
        Depart,
        /// <summary>A god gives: warm light, and a mark of growing things where it stood.</summary>
        Blessing,
        /// <summary>A god is angry: the sky darkens, and the ground is scorched where it stood.</summary>
        Wrath,
        /// <summary>The small sign before a coming: a sound, a few lights, someone looks up; no music, no card.</summary>
        Omen
    }

    /// <summary>What a god leaves on the ground where it walks (its look's trail).</summary>
    public enum GodTrail
    {
        None,
        Flowers,
        Grass,
        Frost,
        Scorch,
        Ash,
        Sparks,
        Water,
        Rain,
        Shadow,
        Light
    }

    /// <summary>The mark a god's moment leaves on the land, made of the game's own things.</summary>
    public enum GodMark
    {
        None,
        Flowers,
        Tree,
        Spring,
        Scorch,
        Frost,
        Ash,
        Slag,
        Stone
    }

    /// <summary>What the trail does at one cell.</summary>
    public enum TrailAct
    {
        Nothing,
        /// <summary>A flower or a tuft of grass, grown at once.</summary>
        Plant,
        /// <summary>A little snow.</summary>
        Snow,
        /// <summary>A small filth of the game's (ash, water).</summary>
        Filth,
        /// <summary>Only a fleck that fades (sparks, light, shadow, a splash).</summary>
        Fleck
    }

    /// <summary>How someone near a god answers it.</summary>
    public enum GodReaction
    {
        None,
        /// <summary>Stops and turns to it.</summary>
        Face,
        /// <summary>The devout: stops, turns to it, and prays.</summary>
        Pray,
        /// <summary>A calm animal lies down facing it.</summary>
        LieDown,
        /// <summary>A shy wild animal runs from it.</summary>
        Flee
    }

    /// <summary>A colour as three numbers from 0 to 1 (the pure side; the mod turns it into the game's Color).</summary>
    public struct GodRgb
    {
        public float r, g, b;

        public GodRgb(float r, float g, float b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        public static GodRgb Lerp(GodRgb a, GodRgb b, float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return new GodRgb(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t);
        }

        /// <summary>How bright it looks (the usual weights of the three).</summary>
        public float Luma => 0.2126f * r + 0.7152f * g + 0.0722f * b;

        public float Max => Math.Max(r, Math.Max(g, b));

        public static readonly GodRgb White = new GodRgb(1f, 1f, 1f);
    }

    /// <summary>
    /// Someone near a god, as the rules see them (GodReactions.cs reads it off the pawn): what they are, and what they are
    /// doing that a god's coming may or may not stop.
    /// </summary>
    public struct GodWatcher
    {
        public bool animal, colonist, hostile, drafted, downed, asleep, mentalState, busy, devout, tame, predatorHunting;
        /// <summary>How wild an animal is (the game's wildness, 0 to 1).</summary>
        public float wildness;
        /// <summary>Cells from the god.</summary>
        public float distance;
        /// <summary>It can see the god (a line of sight on the map).</summary>
        public bool sees;
    }

    /// <summary>
    /// The pure rules of a god's presence (Gods/Presence/; docs/design/gods.md, "Presence"): the fourteen domains and which
    /// look a domain takes in an age, the aura's colours and pulse by day and night, the growth at a reveal, the timings of
    /// the moment, the mix of sounds and the track, who answers a god and how, who notices one in disguise, the trail and
    /// the mark. Free of game types so Tools/Tests try them.
    /// </summary>
    public static class GodPresenceMath
    {
        // ================= The domains and their looks =================

        /// <summary>The fourteen domains, in the design's order (docs/design/gods.md, "Domains").</summary>
        public static readonly string[] Domains =
        {
            "Harvest", "Storm", "Hunt", "Healing", "War", "Craft", "Way", "Dead", "Night", "Hearth", "Wild", "Waters", "Machine", "Void"
        };

        public static bool IsDomain(string d) => d != null && Array.IndexOf(Domains, d) >= 0;

        /// <summary>The look of a domain in every age: Rimfolk_GodLook_&lt;Domain&gt;.</summary>
        public static string LookDefName(string domain) => "Rimfolk_GodLook_" + domain;

        /// <summary>
        /// Which of the looks a domain takes in an age (the game's tech levels as numbers: 2 neolithic, 3 medieval, 4
        /// industrial, 5 spacer, 6 ultra, 7 archotech): among the domain's looks whose ages hold it, the one with the
        /// narrowest range (an age variant beats the domain's look for every age); with none that holds it, the domain's
        /// widest. -1 for a domain with no look.
        /// </summary>
        public static int Choose(IList<(string domain, int minTech, int maxTech)> looks, string domain, int tech)
        {
            if (looks == null || domain == null) return -1;
            int best = -1, bestSpan = int.MaxValue, widest = -1, widestSpan = -1;
            for (int i = 0; i < looks.Count; i++)
            {
                var l = looks[i];
                if (!string.Equals(l.domain, domain, StringComparison.OrdinalIgnoreCase)) continue;
                int span = l.maxTech - l.minTech;
                if (span > widestSpan)
                {
                    widestSpan = span;
                    widest = i;
                }
                if (tech < l.minTech || tech > l.maxTech) continue;
                if (span < bestSpan)
                {
                    bestSpan = span;
                    best = i;
                }
            }
            return best >= 0 ? best : widest;
        }

        // ================= The aura =================

        /// <summary>A slow pulse from 0 to 1 and back, <paramref name="period"/> seconds long.</summary>
        public static float Pulse(float seconds, float period)
        {
            if (period <= 0f) return 1f;
            return 0.5f + 0.5f * (float)Math.Sin(seconds / period * 2.0 * Math.PI);
        }

        /// <summary>How long a breath of the aura takes, in seconds.</summary>
        public const float PulseSeconds = 3.2f;

        /// <summary>
        /// The halo on the ground under a god: brighter at night (the light round it makes the ground its colour) and a
        /// little dimmer by day, when the sun washes it out but it still shows; it breathes by a quarter.
        /// <paramref name="skyGlow"/> is the sky's light, 0 at night to 1 at noon.
        /// </summary>
        public static float HaloAlpha(float skyGlow, float pulse)
        {
            skyGlow = Clamp01(skyGlow);
            float baseA = 0.42f + 0.28f * (1f - skyGlow);
            return Clamp01(baseA * (0.82f + 0.18f * Clamp01(pulse)));
        }

        /// <summary>The glow behind a god's body: soft by day, strong at night.</summary>
        public static float GlowAlpha(float skyGlow, float pulse)
        {
            skyGlow = Clamp01(skyGlow);
            return Clamp01((0.30f + 0.40f * (1f - skyGlow)) * (0.85f + 0.15f * Clamp01(pulse)));
        }

        /// <summary>The ring round a god: a steady line that breathes more than the halo.</summary>
        public static float RingAlpha(float skyGlow, float pulse)
        {
            skyGlow = Clamp01(skyGlow);
            return Clamp01((0.45f + 0.25f * (1f - skyGlow)) * (0.65f + 0.35f * Clamp01(pulse)));
        }

        /// <summary>The light drawn over the darkness at night (it isn't dimmed by the night, so a god shines): none in daylight.</summary>
        public static float NightShineAlpha(float skyGlow, float pulse)
        {
            float dark = 1f - Clamp01(skyGlow);
            if (dark < 0.35f) return 0f;
            return Clamp01((dark - 0.35f) / 0.65f * 0.38f * (0.75f + 0.25f * Clamp01(pulse)));
        }

        /// <summary>The ring's size breathes with the pulse, a little.</summary>
        public static float RingScale(float pulse) => 0.96f + 0.08f * Clamp01(pulse);

        /// <summary>Degrees a second the carved ring under a god turns.</summary>
        public const float RunesTurn = 6f;

        /// <summary>
        /// The colour drawn in daylight: a pale colour is pulled toward its own saturated self, so a white or silver god
        /// still shows on a sunlit field (alpha blending can't brighten what's already bright).
        /// </summary>
        public static GodRgb DayTint(GodRgb c)
        {
            float max = c.Max;
            if (max <= 0.001f) return c;
            float min = Math.Min(c.r, Math.Min(c.g, c.b));
            // Pale: little between the strongest and the weakest channel.
            float pale = 1f - (max - min) / max;
            if (pale < 0.7f) return c;
            // Toward a warm gold for a pale god (light of its own); a cool god keeps its blue.
            var gold = c.b > c.r ? new GodRgb(0.62f, 0.78f, 1f) : new GodRgb(1f, 0.86f, 0.55f);
            return GodRgb.Lerp(c, gold, (pale - 0.7f) / 0.3f * 0.5f);
        }

        // ================= The body =================

        /// <summary>A god's body is drawn this much larger at most, and never smaller.</summary>
        public const float MaxSize = 1.6f;

        public static float SizeFactor(float look) => look < 1f ? 1f : look > MaxSize ? MaxSize : look;

        /// <summary>Seconds a revealed god takes to grow to its size.</summary>
        public const float GrowSeconds = 2.2f;

        /// <summary>
        /// A god's size as it is drawn, <paramref name="sinceReveal"/> seconds after it showed itself (negative: it has
        /// always been shown): eased out from its mortal size to its own.
        /// </summary>
        public static float SizeAt(float size, float sinceReveal)
        {
            size = SizeFactor(size);
            if (sinceReveal < 0f || sinceReveal >= GrowSeconds) return size;
            float t = sinceReveal / GrowSeconds;
            float ease = 1f - (1f - t) * (1f - t) * (1f - t);
            return 1f + (size - 1f) * ease;
        }

        /// <summary>A god walks this much slower than it would: stately, never hurried.</summary>
        public const float StatelyWalk = 0.8f;

        // ================= The moment =================

        /// <summary>The music fades over this many seconds.</summary>
        public const float FadeSeconds = 2.5f;

        /// <summary>The beat of silence after the music.</summary>
        public const float SilenceSeconds = 1.6f;

        /// <summary>The god's track begins this long after the sting.</summary>
        public const float TrackAfterSting = 1.2f;

        /// <summary>The card comes this long after the sting, once the camera is there and everyone has turned.</summary>
        public const float CardAfterSting = 4f;

        /// <summary>A moment is big (the music, the sky, the camera, the card) unless it's an omen.</summary>
        public static bool Big(PresenceKind k) => k != PresenceKind.Omen;

        /// <summary>When the sting comes: after the fade and the silence with the music on; at once with it off or for an omen.</summary>
        public static float StingAt(PresenceKind k, bool music) => Big(k) && music ? FadeSeconds + SilenceSeconds : Big(k) ? 0.6f : 0f;

        /// <summary>When the god's track starts (-1: none, for an omen or with the music off).</summary>
        public static float TrackAt(PresenceKind k, bool music) => Big(k) && music ? StingAt(k, true) + TrackAfterSting : -1f;

        /// <summary>When the card comes (-1: none, for an omen).</summary>
        public static float CardAt(PresenceKind k, bool music) => Big(k) ? StingAt(k, music) + CardAfterSting : -1f;

        /// <summary>
        /// How long the music stays silent once the fade begins (the game's music comes back on its own after): long enough
        /// to cover the god's track, and the beat without one.
        /// </summary>
        public static float SilenceFor(bool hasTrack) => hasTrack ? 25f : FadeSeconds + SilenceSeconds + 40f;

        /// <summary>The presence on the sky, in ticks (an hour is 2500): an Act's is short, an Appear's and a Wrath's longest.</summary>
        public static int ConditionTicks(PresenceKind k)
        {
            switch (k)
            {
                case PresenceKind.Appear:
                case PresenceKind.Reveal:
                    return 2500 * 4;
                case PresenceKind.Wrath:
                    return 2500 * 5;
                case PresenceKind.Blessing:
                    return 2500 * 4;
                case PresenceKind.Act:
                    return 2500 * 2;
                case PresenceKind.Depart:
                    return 2500;
                default:
                    return 0;
            }
        }

        /// <summary>How much the sky dims under the presence (0 none): a wrath darkens it, a blessing never.</summary>
        public static float Darken(PresenceKind k) =>
            k == PresenceKind.Wrath ? 0.45f : k == PresenceKind.Blessing ? 0f : k == PresenceKind.Depart ? 0.05f : 0.15f;

        /// <summary>How strongly the sky takes the god's colour (0 none, 1 fully).</summary>
        public static float SkyTint(PresenceKind k) =>
            k == PresenceKind.Wrath ? 0.65f : k == PresenceKind.Blessing ? 0.45f : k == PresenceKind.Depart ? 0.25f : 0.5f;

        /// <summary>
        /// The sky's colour under a presence: the god's colour, as far as <paramref name="tint"/> says, from white (the game
        /// takes the darker of this and the sky as it is, as it does for an eclipse).
        /// </summary>
        public static GodRgb SkyColor(GodRgb god, float tint) => GodRgb.Lerp(GodRgb.White, god, tint);

        /// <summary>How still the wind falls: the wind speed is multiplied by this (a look's stillness, 0 to 1, a wrath stirring it a little).</summary>
        public static float WindFactor(float still, PresenceKind k)
        {
            still = Clamp01(still);
            float f = 1f - still;
            return k == PresenceKind.Wrath ? Math.Min(1f, f + 0.25f) : f;
        }

        /// <summary>The game drops to normal speed for a big moment (from fast or faster; a paused game stays paused).</summary>
        public static bool SlowsTime(PresenceKind k, int speed) => Big(k) && speed > 1;

        // ================= Sounds and music =================

        /// <summary>
        /// The sounds a moment plays: the first <paramref name="max"/> of <paramref name="wanted"/> that exist in the player's
        /// game, in the look's order, each once. The rest is soft: a missing mod's sound is simply skipped.
        /// </summary>
        public static List<string> Mix(IList<string> wanted, Func<string, bool> exists, int max)
        {
            var mix = new List<string>();
            if (wanted == null || exists == null || max <= 0) return mix;
            foreach (var w in wanted)
            {
                if (string.IsNullOrEmpty(w) || mix.Contains(w)) continue;
                if (!exists(w)) continue;
                mix.Add(w);
                if (mix.Count >= max) break;
            }
            return mix;
        }

        /// <summary>
        /// The track for a moment: one the player put in the god's folder first (their own choice), else one of the look's
        /// that exists (a wrath's dark tracks first, when it has them), picked by <paramref name="seed"/> so it varies from
        /// one coming to the next. Null: no track.
        /// </summary>
        public static string Track(IList<string> own, IList<string> tracks, IList<string> darkTracks, Func<string, bool> exists, PresenceKind k, int seed)
        {
            if (own != null && own.Count > 0) return own[Mod(seed, own.Count)];
            if (exists == null) return null;
            if (k == PresenceKind.Wrath && darkTracks != null)
            {
                var dark = Mix(darkTracks, exists, int.MaxValue);
                if (dark.Count > 0) return dark[Mod(seed, dark.Count)];
            }
            var all = Mix(tracks, exists, int.MaxValue);
            return all.Count > 0 ? all[Mod(seed, all.Count)] : null;
        }

        /// <summary>How loud a revealed god's own sound is under the game's, while the camera is near it.</summary>
        public const float HumVolume = 0.32f;

        /// <summary>The hum is heard only this close (cells from the middle of the screen).</summary>
        public const float HumHearCells = 22f;

        private static int Mod(int seed, int n) => n <= 0 ? 0 : ((seed % n) + n) % n;

        // ================= Who answers a god =================

        /// <summary>Cells round a revealed god in which people and animals notice it as they pass.</summary>
        public const float NearCells = 9f;

        /// <summary>Cells round a moment in which a shy wild animal runs.</summary>
        public const float FleeCells = 22f;

        /// <summary>An omen is noticed only this close.</summary>
        public const float OmenCells = 16f;

        /// <summary>The same person answers a god's passing at most once a day (ticks).</summary>
        public const int PersonCooldown = 60000;

        /// <summary>A god's passing draws a spoken line at most once in this many ticks (two hours), so the bubbles don't pile up.</summary>
        public const int SpeechCooldown = 5000;

        /// <summary>The same person says a line about a god at most once in this many ticks (two days).</summary>
        public const int PersonSpeechCooldown = 120000;

        /// <summary>At most this many stop at one look round a passing god.</summary>
        public const int StopsAPass = 2;

        /// <summary>
        /// How someone answers a god: <paramref name="moment"/> is its coming (everyone on the map turns), otherwise its
        /// passing (only those near who see it). Never the hostile, the drafted, the downed, the sleeping, those in a
        /// mental state, or anyone busy with what mustn't wait; a hunting predator keeps hunting. The devout pray; a tame or
        /// calm animal lies down, a shy wild one close to a moment runs, others stop and turn.
        /// </summary>
        public static GodReaction ReactionTo(GodWatcher w, bool moment)
        {
            if (w.hostile || w.drafted || w.downed || w.asleep || w.mentalState || w.busy || w.predatorHunting) return GodReaction.None;
            if (!moment && (!w.sees || w.distance > NearCells)) return GodReaction.None;
            if (w.animal)
            {
                if (!w.tame && w.wildness >= 0.6f && w.distance <= FleeCells) return GodReaction.Flee;
                if (w.tame || w.wildness < 0.35f) return moment ? GodReaction.LieDown : GodReaction.Face;
                return GodReaction.Face;
            }
            return w.devout ? GodReaction.Pray : GodReaction.Face;
        }

        /// <summary>How long an answer lasts, in ticks: a moment's longer than a passing's, the devout's longest.</summary>
        public static int ReactTicks(GodReaction r, bool moment)
        {
            switch (r)
            {
                case GodReaction.Pray:
                    return moment ? 900 : 360;
                case GodReaction.LieDown:
                    return moment ? 900 : 300;
                case GodReaction.Face:
                    return moment ? 420 : 180;
                case GodReaction.Flee:
                    return 0;
                default:
                    return 0;
            }
        }

        /// <summary>One of those who turned speaks at a passing (a moment: the first few always may).</summary>
        public static bool Speaks(int now, int lastSpoken, int personLast, bool moment) =>
            (moment || now - lastSpoken >= SpeechCooldown) && (personLast < 0 || now - personLast >= PersonSpeechCooldown);

        /// <summary>At a moment, at most this many of those who turn say something.</summary>
        public const int MomentSpeakers = 3;

        // ================= A god in disguise =================

        /// <summary>
        /// Someone notices a disguised god's tell: within ten cells, with a sharp eye (sight 120% or more) or a psychic
        /// gift (sensitivity 150% or more); at night the shimmer is easier to see (sight 110%). Nobody else.
        /// </summary>
        public static bool NoticesTell(float sight, float psychic, bool night, float distance)
        {
            if (distance > 10f) return false;
            if (psychic >= 1.5f) return true;
            return sight >= (night ? 1.1f : 1.2f);
        }

        /// <summary>A disguised god's tells are remarked on at most once a day (ticks).</summary>
        public const int TellCooldown = 60000;

        /// <summary>Animals take to following a disguised god at most this often (ticks: about once in two hours).</summary>
        public const int FollowEvery = 5000;

        /// <summary>The shimmer at night shows only where it's this dark (the ground's light, 0 to 1).</summary>
        public const float ShimmerDark = 0.35f;

        // ================= The trail =================

        /// <summary>What lies on a cell where a god steps, as the trail sees it.</summary>
        public struct TrailCell
        {
            public float fertility;
            public bool floor, water, roofed, home, growZone, occupied, plant;
            public float snow;
        }

        /// <summary>Of every this many cells a god walks, its trail marks one with something that lasts (a flower, snow, ash).</summary>
        public const int LastingEvery = 3;

        /// <summary>A god's lasting trail is this many cells a day at most.</summary>
        public const int LastingADay = 40;

        /// <summary>
        /// What a god's trail does at a cell it stepped on. Something that lasts only on open, natural ground: flowers and
        /// grass on fertile soil with nothing on it (never a field, a floor or water, never under a roof); snow out of
        /// doors; ash and water only outside the home area, where nobody has to clean them up. Otherwise a fleck that fades,
        /// or nothing. <paramref name="lasting"/>: this step may leave something that lasts (the every-third rule and the
        /// day's cap allow it).
        /// </summary>
        public static TrailAct TrailAt(GodTrail trail, TrailCell c, bool lasting)
        {
            switch (trail)
            {
                case GodTrail.None:
                    return TrailAct.Nothing;
                case GodTrail.Flowers:
                case GodTrail.Grass:
                    if (lasting && !c.floor && !c.water && !c.roofed && !c.growZone && !c.occupied && !c.plant && c.fertility >= 0.7f) return TrailAct.Plant;
                    return TrailAct.Fleck;
                case GodTrail.Frost:
                    if (lasting && !c.roofed && !c.water && c.snow < 0.5f) return TrailAct.Snow;
                    return TrailAct.Fleck;
                case GodTrail.Scorch:
                case GodTrail.Ash:
                case GodTrail.Water:
                case GodTrail.Rain:
                    if (lasting && !c.home && !c.floor && !c.water && !c.roofed && !c.occupied) return TrailAct.Filth;
                    return TrailAct.Fleck;
                default:
                    return TrailAct.Fleck;
            }
        }

        // ================= The mark =================

        /// <summary>
        /// The mark a moment leaves: a blessing and a coming leave the look's mark, a wrath its wrath's (scorched ground
        /// unless the look says otherwise), a departure the look's mark too; an act and an omen leave none.
        /// </summary>
        public static GodMark MarkFor(PresenceKind k, GodMark look, GodMark wrath)
        {
            switch (k)
            {
                case PresenceKind.Wrath:
                    return wrath == GodMark.None ? GodMark.Scorch : wrath;
                case PresenceKind.Blessing:
                case PresenceKind.Appear:
                case PresenceKind.Reveal:
                case PresenceKind.Depart:
                    return look;
                default:
                    return GodMark.None;
            }
        }

        /// <summary>The cells of a ring round a place: those whose distance rounds to <paramref name="radius"/>, in a fixed order.</summary>
        public static List<(int dx, int dz)> Ring(int radius)
        {
            var cells = new List<(int, int)>();
            if (radius <= 0)
            {
                cells.Add((0, 0));
                return cells;
            }
            for (int dx = -radius - 1; dx <= radius + 1; dx++)
            for (int dz = -radius - 1; dz <= radius + 1; dz++)
            {
                double d = Math.Sqrt(dx * dx + dz * dz);
                if (Math.Abs(d - radius) <= 0.5) cells.Add((dx, dz));
            }
            return cells;
        }

        /// <summary>The ring a mark is laid in: a scorch or a frost wider, flowers a little closer.</summary>
        public static int MarkRadius(GodMark m) => m == GodMark.Scorch || m == GodMark.Frost || m == GodMark.Ash ? 3 : m == GodMark.Flowers ? 2 : 0;

        /// <summary>The way a spirit mote runs, by the way its picture faces (its name's end: East, West, North, South).</summary>
        public static float SpiritAngle(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0f;
            if (name.EndsWith("East", StringComparison.Ordinal)) return 90f;
            if (name.EndsWith("West", StringComparison.Ordinal)) return 270f;
            if (name.EndsWith("South", StringComparison.Ordinal)) return 180f;
            return 0f;
        }

        // ================= Small helpers =================

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
