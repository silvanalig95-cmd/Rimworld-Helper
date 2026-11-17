using System;
using System.Collections.Generic;
using System.Linq;

namespace ColonyCouncil
{
    /// <summary>
    /// One occurrence's own effect (<see cref="GodImpactMath.Table"/>): what follows it, how strong (a small one shows where
    /// it happened, a notable one moves the camera there as the gods' touch does), whether it is one of the small harms, and
    /// its size (what the size means is the effect's own: a radius, a count, a share, hours or days).
    /// </summary>
    public struct Impact
    {
        public TouchEffect effect;
        public TouchTier tier;
        public bool dark;
        public float size;

        public Impact(TouchEffect effect, TouchTier tier, bool dark = false, float size = 0f)
        {
            this.effect = effect;
            this.tier = tier;
            this.dark = dark;
            this.size = size;
        }

        public TouchEntry Entry => new TouchEntry(effect, tier, dark);

        public override string ToString() => effect + (dark ? " (dark)" : "") + ", " + tier + (size > 0f ? ", " + size : "");
    }

    /// <summary>
    /// Every god's doing leaves something real (docs/gods-impact-proposals.md; the player, 17 November 2026, after the ring of
    /// mushrooms round an unburied body: "does anything about the body change? like faster decay or something? is there a
    /// gameplay impact from this god event?", then "not every god interaction with the world necessarily needs to have a
    /// strong impact, but it should also not be completely trivial", and "yes, go ahead with all!"). Each occurrence the
    /// proposals cover gets one effect of its own, always, fitted to what was seen: the ring rots the body away and leaves rich
    /// soil, frost on a grave lets the dead one's closest sleep well, thunder under a clear sky brings rain, and so on. These
    /// keys count as having their own effect (GodTouchMath.HasOwnEffect), so the touch's random effect doesn't come on top;
    /// the moods stay as they are. An effect on the place happens whether or not anyone of the colony saw it (it is there to
    /// be found); one on people goes to those it names (the dead one's closest, the dying, the dreamers) or to those who saw
    /// it. Free of game types.
    /// </summary>
    public static class GodImpactMath
    {
        private static Impact S(TouchEffect e, float size = 0f) => new Impact(e, TouchTier.Small, false, size);
        private static Impact N(TouchEffect e, float size = 0f) => new Impact(e, TouchTier.Notable, false, size);
        private static Impact D(TouchEffect e, float size = 0f) => new Impact(e, TouchTier.Small, true, size);

        /// <summary>
        /// Each covered occurrence's words key and its own effect, by the proposal's number. The sizes: RichSoil's radius,
        /// Rain's hours at most before it comes, GameLeaves' days before the game comes back, Hardy's days, Sleepless's how
        /// many, Charge's share of a battery, Inspire 1 for an inspiration to research first.
        /// </summary>
        public static readonly Dictionary<string, Impact> Table = new Dictionary<string, Impact>
        {
            // ---- Morel ----
            // 1. The ring round an unburied body: the body rots away to bones within a day or two, and the ground inside the ring is rich.
            ["CC_GodO_Saw_RingBody"] = S(TouchEffect.RotAway),
            // 2. White threads round rot in the stores: the rotten stack is gone, the floor under it clean.
            ["CC_GodO_Saw_RingStores"] = S(TouchEffect.RotGone),
            // 3. The ring where the mourner stood all night: those who mourn the newly dead feel their grief ease sooner.
            ["CC_GodO_Saw_MournerGone"] = S(TouchEffect.GriefEased),
            // 4. A ring of mushrooms overnight (the sign already brings up the glowstools): the soil inside the ring rich.
            ["CC_GodM_Saw_FairyRing"] = S(TouchEffect.RichSoil, 1.5f),
            // 5. Fine white threads across a floor: the food stored in that room keeps a little less long for a few days.
            ["CC_GodM_Saw_Threads"] = D(TouchEffect.KeepsLess),
            // ---- Hush ----
            // 6. Frost on a grave at dusk: those closest to that dead person sleep well that night and wake rested.
            ["CC_GodHush_Saw_Grave"] = S(TouchEffect.RestKin),
            // 7. A ring where the frost on a grave melted: flowers come up round the grave.
            ["CC_GodHush_Saw_MeltRing"] = S(TouchEffect.Bloom),
            // 8. Something walked through in the night leaving a line of frost: a few tender crops on the line take the cold.
            ["CC_GodHush_Saw_Walk"] = D(TouchEffect.FrostLine),
            ["CC_GodHush_Saw_WalkSeen"] = D(TouchEffect.FrostLine),
            // 9. A word written in the frost: something small lies where the word was written.
            ["CC_GodHush_Saw_Word"] = S(TouchEffect.Find),
            // 10. Frost by a wall in lines like writing: the room behind that wall stays cold for a day.
            ["CC_GodM_Saw_FrostWriting"] = S(TouchEffect.ColdRoom),
            // ---- Helios ----
            // 11. Facing the sunrise after the longest night: those who stood there are fully rested.
            ["CC_GodHelios_Saw_Dawn"] = S(TouchEffect.Rested),
            // 12. The noon light standing white after the eclipse, the sun standing still: the crops in the open grow a day
            // ahead, and solar panels give their most until sundown.
            ["CC_GodHelios_Saw_LookBack"] = S(TouchEffect.LongNoon),
            ["CC_GodM_Saw_StillSun"] = S(TouchEffect.LongNoon),
            // 13. The air shimmering over the open ground at noon: the open ground a few degrees warmer for the afternoon.
            ["CC_GodM_Saw_HeatHaze"] = S(TouchEffect.Warmth),
            // ---- Selene ----
            // 14. The full moon lying in still water: the one who stood by it is inspired.
            ["CC_GodSelene_Saw_Water"] = N(TouchEffect.Inspire),
            // 15. The door kept on a night with no moon: nothing comes to the colony that night.
            ["CC_GodSelene_Saw_DarkFace"] = S(TouchEffect.NightWard),
            // 16. The moon huge and close all night: everyone at the home sleeps deeper that night.
            ["CC_GodM_Saw_BigMoon"] = S(TouchEffect.MoonSleep),
            // 17. A star moving the wrong way: the one who watched it is inspired, to research first.
            ["CC_GodM_Saw_Star"] = N(TouchEffect.Inspire, 1f),
            // ---- Tamiu ----
            // 18. A cat beside someone near death: their pain is less while it stays.
            ["CC_GodTamiu_Saw_Threshold"] = S(TouchEffect.PainEased),
            // 19. The cat stayed to the end: those who mourn that person grieve less long.
            ["CC_GodTamiu_Saw_Died"] = S(TouchEffect.GriefEased),
            // 20. The cat walked away once they came through: the one who lived heals faster for a few days.
            ["CC_GodTamiu_Saw_WentAway"] = S(TouchEffect.HealFaster),
            // 21. Every cat facing the graves, a cat sitting on a grave: the rats leave the land.
            ["CC_GodM_Saw_CatsFacing"] = S(TouchEffect.RatsGone),
            ["CC_GodM_Saw_GraveCat"] = S(TouchEffect.RatsGone),
            // 22. Two gold eyes low by the graves: a stray cat comes to stay.
            ["CC_GodM_Saw_GoldEyes"] = N(TouchEffect.CatComes),
            // ---- Revel ----
            // 23. Someone deep in a binge laughing with an empty seat: the binge ends sooner, and their recreation is full.
            ["CC_GodRevel_Saw_Binge"] = S(TouchEffect.BingeEnds),
            // 24. The horned man walking in with a cup: a drink of his kind left where he stood.
            ["CC_GodRevel_Saw_Satyr"] = S(TouchEffect.Drink),
            // 25. The drink in the stores knocked over: one of it really is spilled and lost (the sign itself spills it).
            ["CC_GodM_Saw_Spilled"] = D(TouchEffect.Spill),
            // 26. A sweet smoke through the colony: those who smell it are cheered for the day.
            ["CC_GodM_Saw_SweetSmoke"] = S(TouchEffect.Cheered),
            // ---- Ducat ----
            // 27. A gold coin at their feet: a real gold coin lies there (the sign itself leaves it).
            ["CC_GodM_Saw_Coin"] = S(TouchEffect.Coin),
            // 28. A glint of gold out of the corner of the eye: a trader comes sooner.
            ["CC_GodM_Saw_Glint"] = N(TouchEffect.Trader),
            // 29. A golden beetle walking across a table: a little silver under that table.
            ["CC_GodM_Saw_Beetle"] = S(TouchEffect.Silver),
            // ---- Pip ----
            // 30. Lights flickering in a pattern: the batteries on that line fill a little.
            ["CC_GodM_Saw_Flicker"] = S(TouchEffect.Charge, 0.25f),
            // 31. A light glowing with no power to it: it keeps glowing all night.
            ["CC_GodM_Saw_Unpowered"] = S(TouchEffect.StaysLit),
            // 32. A voice in the comms console's static: a trade ship passes within reach.
            ["CC_GodM_Saw_Static"] = N(TouchEffect.Trader, 1f),
            // 33. A machine switched off that runs on a whole hour: that hour costs no power and no fuel.
            ["CC_GodO_Saw_WontStop"] = S(TouchEffect.FreeHour),
            // 34. A light going out again and again with no draft: the fires and torches in that room go out.
            ["CC_GodM_Saw_Candle"] = D(TouchEffect.FiresOut),
            // ---- Smoulder ----
            // 35. The rock rumbling overhead in the deep workings: a few chunks of stone come down there, on no one.
            ["CC_GodM_Saw_Rumble"] = S(TouchEffect.StoneChunks),
            // 36. The miners' shared dream of something stretching under the mountain: those miners dig faster for a day.
            ["CC_GodO_Saw_Stretch"] = S(TouchEffect.DigFaster),
            // 37. Steam coming up out of the ground: the ground round it stays warm for a few days, the snow off it and the
            // plants near it growing.
            ["CC_GodM_Saw_Steam"] = S(TouchEffect.WarmGround),
            // ---- Cirrus ----
            // 38. The wind dying and nothing moving: the wind turbines stop and fire doesn't spread for the hours it lasts.
            ["CC_GodM_Saw_Calm"] = S(TouchEffect.StillAir),
            // 39, 40. Thunder over the far hills under a clear sky, the smell of wet earth with no rain: rain within the day.
            ["CC_GodO_Saw_Thunder"] = S(TouchEffect.Rain, 8f),
            ["CC_GodM_Saw_Earth"] = S(TouchEffect.Rain, 8f),
            // 41. A shadow with wings, a shadow too large for any cloud: the wild game leaves the land for a day, and comes back.
            ["CC_GodM_Saw_Shadow"] = D(TouchEffect.GameLeaves, 1f),
            ["CC_GodM_Saw_VastShadow"] = D(TouchEffect.GameLeaves, 1f),
            // ---- Hollin, Bellwether and the beasts ----
            // 42. The grass bowed flat in a long line, a hoofprint the size of a table: game comes onto the land.
            ["CC_GodM_Saw_GrassBowed"] = N(TouchEffect.Game),
            ["CC_GodM_Saw_Hoofprint"] = N(TouchEffect.Game),
            // 43. Every leaf and seed drifting toward one place: wild plants come up there.
            ["CC_GodM_Saw_Drift"] = S(TouchEffect.Bloom),
            // 44. The anima tree humming: its grass spreads a little, and meditating at it is better for a day.
            ["CC_GodM_Saw_AnimaHum"] = S(TouchEffect.AnimaHum),
            // 45. A goat bleating in an empty room, the beast lowering its head toward the young at dusk, the dream of the last
            // white beast: one of the colony's herd animals is with young.
            ["CC_GodM_Saw_Bleat"] = S(TouchEffect.WithYoung),
            ["CC_GodO_Saw_Dusk"] = S(TouchEffect.WithYoung),
            ["CC_GodO_Saw_Last"] = S(TouchEffect.WithYoung),
            // 46. The animals all looking one way, the dogs quiet facing the gate, the thrumbos or dryads standing still, every
            // bird taking off at once: the colony's animals calm and easy to handle for a day.
            ["CC_GodM_Saw_Restless"] = S(TouchEffect.AnimalsCalm),
            ["CC_GodM_Saw_DogsSilent"] = S(TouchEffect.AnimalsCalm),
            ["CC_GodM_Saw_ThrumbosFace"] = S(TouchEffect.AnimalsCalm),
            ["CC_GodM_Saw_DryadsStill"] = S(TouchEffect.AnimalsCalm),
            ["CC_GodM_Saw_Birds"] = S(TouchEffect.AnimalsCalm),
            // 47. Food taken to the god's beast and it ate: the colony's animals hardier for a few days.
            ["CC_GodM_Saw_Fed"] = S(TouchEffect.Hardy, 3f),
            // ---- The eerie signs of any god ----
            // 48. A knock with nobody there, footprints to the door and none away, a tall white figure: the one who saw it
            // sleeps badly that night.
            ["CC_GodM_Saw_Knock"] = D(TouchEffect.Sleepless, 1f),
            ["CC_GodM_Saw_Footprints"] = D(TouchEffect.Sleepless, 1f),
            ["CC_GodM_Saw_WhiteFigure"] = D(TouchEffect.Sleepless, 1f),
            // 49. A sweet voice speaking when nobody was near: the one who heard it is inspired.
            ["CC_GodM_Saw_Whisper"] = N(TouchEffect.Inspire),
            // 50. A dark the light didn't reach: that spot stays dark for the night.
            ["CC_GodM_Saw_Dark"] = D(TouchEffect.StaysDark),
            // 51. A deep croaking from the wet ground: fish by the water.
            ["CC_GodM_Saw_Croak"] = N(TouchEffect.Fish),
            // 52. Pale sand across a grave: the one closest to that dead person dreams of them kindly.
            ["CC_GodM_Saw_Sand"] = S(TouchEffect.KindDream),
            // 53. Two of the colony waking from the same dream: they think better of each other.
            ["CC_GodM_Saw_Dream"] = S(TouchEffect.SameDream),
            // 54. Everything going silent at once: everyone's next hour of work goes better.
            ["CC_GodM_Saw_Silence"] = S(TouchEffect.StillHour),
            // 55. A work named for the god by the one who made it: the piece is admired more.
            ["CC_GodO_Saw_Name"] = S(TouchEffect.Admired)
        };

        /// <summary>
        /// The occurrences the proposals leave as they are, for they already lead to something real (the freezer breaks down,
        /// the tiles are broken, the burn comes, the rain comes, the colony's own choice or offering, the star falls, the dream
        /// comes true, a god comes, the graves are cleared, the judgement, the body found, a mood of its own): the moods, and
        /// the touch's random effect as before.
        /// </summary>
        public static readonly string[] LeftAsTheyAre =
        {
            "CC_GodO_Saw_Threads", "CC_GodO_Saw_Roots", "CC_GodO_Saw_RootsAgain", "CC_GodO_Saw_Heat", "CC_GodO_Saw_BurnDream", "CC_GodPO_Saw_WarmVoice",
            "CC_GodO_Saw_Horn", "CC_GodO_Saw_HornSnow", "CC_GodO_Saw_RoadBack", "CC_GodO_Saw_RoadOpen", "CC_GodO_Saw_Stars", "CC_GodO_Saw_StarsNight",
            "CC_GodO_Saw_BowlSet", "CC_GodO_Saw_DryadWord", "CC_GodPO_Saw_TrueDream", "CC_GodM_Saw_Rising", "CC_GodM_Saw_WrappedDeadDown",
            "CC_GodM_Saw_StrangerTell", "CC_GodM_Saw_StrangerKnewDead", "CC_GodTamiu_Saw_Lost", "CC_GodO_Saw_Breath"
        };

        /// <summary>
        /// The rest of the 98 with no effect of their own, untouched by the proposals: the book's own wordings of a sighting
        /// (not an occurrence), two marks with nothing to feel (the frost melted, the beast's road), the mourner seen (its ring
        /// at dawn is the one that follows), the beast surfacing as a god rises, the god's beast staring.
        /// </summary>
        public static readonly string[] NotCovered =
        {
            "CC_GodLore_Saw_Bare", "CC_GodLore_Saw_Devout", "CC_GodLore_Saw_Learned", "CC_GodLore_Saw_Plain", "CC_GodHush_Saw_Melt", "CC_GodO_Saw_Road",
            "CC_GodO_Saw_Mourner", "CC_GodM_Saw_Surfaced", "CC_GodM_Saw_Stare"
        };

        /// <summary>The occurrence has an effect of its own in the table.</summary>
        public static bool Has(string key) => !string.IsNullOrEmpty(key) && Table.ContainsKey(key);

        /// <summary>The occurrence's own effect, or none.</summary>
        public static Impact Of(string key) => Has(key) ? Table[key] : default;

        /// <summary>
        /// Effects that are for those who saw it, and need someone of the colony to have seen it: an inspiration, the sweet
        /// smoke's good mood, a bad night. Every other one happens whether or not anyone saw it: on the place, or on those it
        /// names.
        /// </summary>
        public static bool NeedsWitness(TouchEffect e) => e == TouchEffect.Inspire || e == TouchEffect.Cheered || e == TouchEffect.Sleepless;

        /// <summary>
        /// The effects that are small harms, only where the proposals say (5 the food keeping less long, 8 a few crops on the
        /// frost line, 25 one drink lost, 34 the fires in one room out, 48 one bad night): none of them hurts anyone's body.
        /// </summary>
        public static bool Harm(TouchEffect e) => e == TouchEffect.KeepsLess || e == TouchEffect.FrostLine || e == TouchEffect.Spill || e == TouchEffect.FiresOut
                                                  || e == TouchEffect.Sleepless;

        // ================= Sizes =================

        /// <summary>The body rots to bones over this many days, and the ground inside the ring (this radius) turns rich.</summary>
        public const float RotAwayDays = 1.5f, RingInside = 1.5f;

        /// <summary>The food in the room of the threads keeps this much less long for each day it lies there, for this many days, never rotting from it.</summary>
        public const float KeepsLessShare = 0.5f, KeepsLessDays = 3f;

        /// <summary>A room behind the frost writing is held this cold (°C) for a day, falling this much at a time.</summary>
        public const float ColdRoomTemp = 5f, ColdRoomStep = 3f, ColdRoomDays = 1f;

        /// <summary>The open ground's warmth for the afternoon (°C).</summary>
        public const float WarmOffset = 5f;

        /// <summary>The warm ground round the steam: this radius, this many days, the plants near it this much grown on (a share of their growth).</summary>
        public const float WarmRadius = 4.9f, WarmDays = 3f, WarmGrowth = 0.15f;

        /// <summary>Tender crops a frost line kills at most (the game's own cold), and how near the line counts.</summary>
        public const int FrostCropsMost = 4;

        public const float FrostLineReach = 1.5f;

        /// <summary>How much of what is left of a mourning memory goes when grief eases (it ends that much sooner).</summary>
        public const float GriefShare = 0.3f;

        /// <summary>The kin a grave's frost reaches at most, and the opinion that makes someone close without kinship.</summary>
        public const int KinMost = 3;

        public const int CloseOpinion = 30;

        /// <summary>Silver under the table, at least and at most.</summary>
        public const int SilverMin = 10, SilverMax = 25;

        /// <summary>Stone chunks that come down, at least and at most.</summary>
        public const int ChunksMin = 2, ChunksMax = 3;

        /// <summary>The colony's people a long noon, a still hour or a calm among the animals reaches at most.</summary>
        public const int EveryoneMost = 40;

        /// <summary>The anima grass that spreads at most, and how much better meditating at the tree is for a day.</summary>
        public const int AnimaGrassMost = 5;

        public const float AnimaFocus = 0.06f, AnimaDays = 1f;

        /// <summary>A work named for the god is this much more beautiful; the named works the colony keeps track of.</summary>
        public const float AdmiredFactor = 1.2f;

        public const int AdmiredKept = 30;

        /// <summary>Rain comes at least this many hours after (at most the impact's size).</summary>
        public const float RainSoonest = 2f;

        /// <summary>How many effects may run on at once (a body rotting, a room cold, the warm ground ...): old ones give way.</summary>
        public const int RunningKept = 24;

        /// <summary>The share of a mourning memory still to run that is taken off, as ticks off its age; never all of it.</summary>
        public static int GriefAge(int age, int duration)
        {
            if (duration <= 0 || age >= duration) return age;
            int left = duration - age;
            int off = (int)Math.Round(left * GriefShare);
            return Math.Min(duration - 1, age + Math.Max(0, off));
        }

        /// <summary>The body's rot progress at <paramref name="now"/> on its way from where it was to bones, over the days; never back.</summary>
        public static float RotAt(float start, float bones, int since, int until, int now)
        {
            if (until <= since || now >= until) return Math.Max(start, bones);
            if (now <= since) return start;
            float t = (now - since) / (float)(until - since);
            return Math.Max(start, start + (bones - start) * t);
        }

        /// <summary>The food's rot moved on by <paramref name="ticks"/> of keeping less long, stopping short of rotting.</summary>
        public static float KeepsLess(float rot, float rotsAt, int ticks, float margin)
        {
            if (ticks <= 0 || rot >= rotsAt - margin) return rot;
            return Math.Min(rotsAt - margin, rot + ticks * KeepsLessShare);
        }

        /// <summary>A cold room's temperature after one step: down toward the cold it is held at, never warmer than it was.</summary>
        public static float ColdStep(float temp) => temp <= ColdRoomTemp ? temp : Math.Max(ColdRoomTemp, temp - ColdRoomStep);

        /// <summary>Hours from <paramref name="hour"/> until <paramref name="until"/> o'clock (the next one: dawn tomorrow at night), at least one.</summary>
        public static int HoursUntil(int hour, int until)
        {
            int h = ((until - hour) % 24 + 24) % 24;
            return h == 0 ? 24 : h;
        }

        /// <summary>The day's light until sundown (18 o'clock), at least an hour; past sundown nothing.</summary>
        public static int HoursOfLight(int hour) => hour >= 18 ? 0 : Math.Max(1, 18 - hour);

        /// <summary>Silver under a table, by a roll in [0, 1).</summary>
        public static int Silver(float roll) => SilverMin + (int)Math.Floor(Clamp01(roll) * (SilverMax - SilverMin + 1 - 1e-4f));

        /// <summary>Stone chunks that come down, by a roll in [0, 1).</summary>
        public static int Chunks(float roll) => ChunksMin + (int)Math.Floor(Clamp01(roll) * (ChunksMax - ChunksMin + 1 - 1e-4f));

        /// <summary>When the rain comes, in hours after, by a roll: soonest a couple of hours, at most <paramref name="most"/>.</summary>
        public static float RainHours(float most, float roll) => most <= RainSoonest ? RainSoonest : RainSoonest + Clamp01(roll) * (most - RainSoonest);

        /// <summary>What a battery takes from a flicker: a share of what it holds at most, no more than it can take.</summary>
        public static float Charge(float capacity, float canAccept, float share) => Math.Max(0f, Math.Min(canAccept, capacity * share));

        private static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
