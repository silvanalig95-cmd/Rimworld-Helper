using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// What one god's own presence does in the world (docs/gods-next-proposals.md, section C; round 2): one kind each, and
    /// every kind a different one. A god whose presence names none has the shared presence only (the sky and the stilled
    /// wind), as every god had before.
    /// </summary>
    public enum PresenceEffect
    {
        /// <summary>The shared presence: the sky in the god's colour and the wind dying, nothing else.</summary>
        None,
        /// <summary>Hollin, "the green leaning": plants grow faster, leaves drift toward one place, the dryads stand still.</summary>
        Growth,
        /// <summary>Smoulder, "warm stone": the ground warms near the rock, steam and a faint ash, ore glints, mining yields more.</summary>
        WarmStone,
        /// <summary>Bellwether, "the white beast grazing": the beasts calm, none turns on anyone, quarrels are rarer.</summary>
        Grazing,
        /// <summary>Morel, "spore-fall": pale spores, mushroom rings, food left out and the dead go to earth faster, fungus grows.</summary>
        Rot,
        /// <summary>Ennu, "a warm evening": hearths burn longer and warmer, talk is kinder, visitors are glad of the place.</summary>
        Warmth,
        /// <summary>Pip, "a hum in the wires": the lights pulse, batteries hold, powered benches go faster, one door plays a prank.</summary>
        Hum,
        /// <summary>Cirrus, "a sky too high": the sky clears and the weather holds, far lightning with no rain, the sky watched back.</summary>
        HighSky,
        /// <summary>Hush, "a white silence": nobody talks, the cold deepens, frost by the walls, nothing rots out of doors.</summary>
        WhiteSilence,
        /// <summary>Undertow, "the dark": lamps reach less far, a corner stays dark, minds open to the psychic.</summary>
        Dark,
        /// <summary>Helios, "the long noon": the light of the hour holds, panels give more, crops grow faster, the air warms.</summary>
        LongNoon,
        /// <summary>Selene, "a moon too close": the night as light as dusk, sleepers heal faster, a true dream at the full moon.</summary>
        MoonSleep,
        /// <summary>Murmur, "a croaking in the wells": a deep croak, frogs, water at the well, rain within the hour, wells run clean.</summary>
        Rain,
        /// <summary>Ducat, "a glint": coins glint in the stores, traders pay better, a few coins trail toward the gate.</summary>
        Glint,
        /// <summary>Tamiu (Mog), "the cats keeping watch": from her coming to her going, the rats run off, the cats sit facing the graves.</summary>
        Watch
    }

    /// <summary>A plant as a presence's growth sees it.</summary>
    public enum PresencePlant
    {
        Wild,
        /// <summary>Sown in a field or a pot by the colony.</summary>
        Crop,
        /// <summary>A mushroom or other fungus (nutrifungus, glowstool, the mods' mushrooms).</summary>
        Fungus,
        Tree
    }

    /// <summary>
    /// The pure rules of each god's own presence (Gods/Presence/GodPresenceOwn.cs; docs/gods-next-proposals.md, section C):
    /// which def a god's presence is, how strong a moment makes it (a blessing full, a departure little; a wrath turns its
    /// kindly side off and its hard side up), the size of every effect, how long a presence lasts when its own nature asks
    /// for another length than the moment's, the weather it holds or brings, and the true dream at the full moon. Numbers
    /// only: the code shows them under the condition's own label, never as a god's favour. Free of game types.
    /// </summary>
    public static class GodPresenceOwnMath
    {
        /// <summary>The shared presence every god without its own keeps (Defs/GodDefs/Rimfolk_GodPresence.xml).</summary>
        public const string SharedDef = "Rimfolk_GodPresence";

        /// <summary>A god's own presence's defName by convention: Rimfolk_GodPresence_ and its key.</summary>
        public static string DefNameFor(string key) => SharedDef + "_" + key;

        // ================= How strong =================

        /// <summary>
        /// How strong a moment makes the presence, 0 to 1: a blessing fully, a coming or a reveal nearly, an act less, a
        /// departure a little, a wrath as much as a coming (its hard side, <see cref="Harsh"/>); an omen none.
        /// </summary>
        public static float Strength(PresenceKind k)
        {
            switch (k)
            {
                case PresenceKind.Blessing: return 1f;
                case PresenceKind.Appear:
                case PresenceKind.Reveal:
                case PresenceKind.Wrath:
                    return 0.85f;
                case PresenceKind.Act: return 0.7f;
                case PresenceKind.Depart: return 0.4f;
                default: return 0f;
            }
        }

        /// <summary>A wrath: the presence's kindly side is off and its hard side is up (a colder cold, a faster rot, darker).</summary>
        public static bool Harsh(PresenceKind k) => k == PresenceKind.Wrath;

        /// <summary>A factor of <paramref name="full"/> at full strength, scaled down toward 1 by <paramref name="s"/>.</summary>
        public static float Scaled(float full, float s) => 1f + (full - 1f) * Clamp01(s);

        // ================= Growth =================

        /// <summary>Hollin's plants at full strength: a third faster.</summary>
        public const float GreenGrowth = 1.33f;
        /// <summary>Morel's mushrooms and other fungus: half again as fast.</summary>
        public const float FungusGrowth = 1.5f;
        /// <summary>Helios's crops in the long noon: a quarter faster.</summary>
        public const float NoonCrops = 1.25f;
        /// <summary>Murmur's wet ground: three tenths faster.</summary>
        public const float WetGrowth = 1.3f;

        /// <summary>
        /// How much faster a plant grows under a presence (1: as it would): every plant under Hollin's; fungus under Morel's
        /// (more in her wrath); crops under Helios's, never in his wrath (the heat); plants on wet ground under Murmur's. None
        /// under a wrath of Hollin's or Murmur's.
        /// </summary>
        public static float Growth(PresenceEffect e, float s, bool harsh, PresencePlant plant, bool wetGround)
        {
            switch (e)
            {
                case PresenceEffect.Growth: return harsh ? 1f : Scaled(GreenGrowth, s);
                case PresenceEffect.Rot: return plant == PresencePlant.Fungus ? Scaled(FungusGrowth, s) : 1f;
                case PresenceEffect.LongNoon: return plant == PresencePlant.Crop && !harsh ? Scaled(NoonCrops, s) : 1f;
                case PresenceEffect.Rain: return wetGround && !harsh ? Scaled(WetGrowth, s) : 1f;
                default: return 1f;
            }
        }

        // ================= Heat and cold =================

        /// <summary>
        /// The outdoor temperature's offset, in degrees: Smoulder's warm stone (more in his wrath), Helios's noon (more still
        /// in his), Ennu's warm evening (none when wronged), Hush's cold (deeper in its wrath).
        /// </summary>
        public static float Temperature(PresenceEffect e, float s, bool harsh)
        {
            s = Clamp01(s);
            switch (e)
            {
                case PresenceEffect.WarmStone: return (harsh ? 7f : 4f) * s;
                case PresenceEffect.LongNoon: return (harsh ? 8f : 4f) * s;
                case PresenceEffect.Warmth: return harsh ? 0f : 2f * s;
                case PresenceEffect.WhiteSilence: return -(harsh ? 8f : 5f) * s;
                default: return 0f;
            }
        }

        /// <summary>Heat pushed into each room under the mountain's thick rock at each of Smoulder's passes (his rooms warm more than the open air).</summary>
        public static float UnderRockHeat(PresenceEffect e, float s, bool harsh) => e == PresenceEffect.WarmStone ? (harsh ? 18f : 10f) * Clamp01(s) : 0f;

        /// <summary>How much snow a cell next to the rock loses at each of Smoulder's passes (snow melting off the rock).</summary>
        public static float SnowMelt(PresenceEffect e, float s) => e == PresenceEffect.WarmStone ? 0.08f * Clamp01(s) : 0f;

        /// <summary>
        /// Who feels Hush's cold sooner (the old, the sick, babies): their least comfortable temperature rises by this many
        /// degrees under its presence, more in its wrath. Nobody else.
        /// </summary>
        public static float ColdSooner(PresenceEffect e, float s, bool harsh, bool weak) => e == PresenceEffect.WhiteSilence && weak ? (harsh ? 6f : 4f) * Clamp01(s) : 0f;

        // ================= Rot =================

        /// <summary>Morel's rot at full strength (food left out of the cold, and the dead), and in her wrath.</summary>
        public const float SporeRot = 1.5f, SporeRotWrath = 2f;

        /// <summary>
        /// How fast things rot under a presence (1: as they would): faster under Morel's anywhere it isn't frozen; not at all
        /// out of doors under Hush's (the dead keep); as it would under a roof. Nothing frozen rots either way.
        /// </summary>
        public static float Rot(PresenceEffect e, float s, bool harsh, bool roofed)
        {
            switch (e)
            {
                case PresenceEffect.Rot: return harsh ? SporeRotWrath : Scaled(SporeRot, s);
                case PresenceEffect.WhiteSilence: return roofed ? 1f : 0f;
                default: return 1f;
            }
        }

        /// <summary>Sicknesses pass faster under Morel's (the body's immunity), never in her wrath.</summary>
        public static float Immunity(PresenceEffect e, float s, bool harsh) => e == PresenceEffect.Rot && !harsh ? Scaled(1.15f, s) : 1f;

        // ================= The colony's peace =================

        /// <summary>How likely an insult or a slight is (1: as it would): rarer under the Old Horn's and the Guest's, not when they were wronged.</summary>
        public static float Insults(PresenceEffect e, float s, bool harsh)
        {
            if (harsh) return 1f;
            if (e == PresenceEffect.Grazing || e == PresenceEffect.Warmth) return Scaled(0.5f, s);
            return 1f;
        }

        /// <summary>How likely an insult turns to a fight (1: as it would): far rarer under the Old Horn's, rarer under the Guest's.</summary>
        public static float Fights(PresenceEffect e, float s, bool harsh)
        {
            if (harsh) return 1f;
            if (e == PresenceEffect.Grazing) return Scaled(0.25f, s);
            if (e == PresenceEffect.Warmth) return Scaled(0.6f, s);
            return 1f;
        }

        /// <summary>No animal turns on anyone (the game's manhunter) while the Old Horn grazes in peace.</summary>
        public static bool CalmsBeasts(PresenceEffect e, bool harsh) => e == PresenceEffect.Grazing && !harsh;

        /// <summary>Under Hush's white silence nobody starts a conversation (the game's chats, insults and kind words alike).</summary>
        public static bool Talks(PresenceEffect e) => e != PresenceEffect.WhiteSilence;

        /// <summary>How many of the wild animals near the Old Horn lie down calmly at each pass.</summary>
        public const int CalmPerPass = 3;

        /// <summary>A warm evening's mood: the colony's own, and its visitors more.</summary>
        public static int WarmEveningMood(PresenceEffect e, bool harsh, bool visitor) => e != PresenceEffect.Warmth || harsh ? 0 : visitor ? 5 : 2;

        /// <summary>A hearth's fuel kept at each of the Guest's passes, as a share of what it burns (hearths burn longer).</summary>
        public static float HearthKept(PresenceEffect e, float s, bool harsh) => e == PresenceEffect.Warmth && !harsh ? 0.6f * Clamp01(s) : 0f;

        // ================= Machines =================

        /// <summary>The share of what a battery gives up that it keeps under Pip's hum (batteries hold); none when it sulks.</summary>
        public static float BatteryKept(PresenceEffect e, float s, bool harsh) => e == PresenceEffect.Hum && !harsh ? 0.5f * Clamp01(s) : 0f;

        /// <summary>Work at a powered bench under Pip's hum: a tenth faster.</summary>
        public static float BenchSpeed(PresenceEffect e, float s, bool harsh) => e == PresenceEffect.Hum && !harsh ? Scaled(1.1f, s) : 1f;

        /// <summary>One door plays its prank on one person while Pip is awake and not sulking.</summary>
        public static bool Pranks(PresenceEffect e, bool harsh) => e == PresenceEffect.Hum && !harsh;

        /// <summary>The longest a door's prank lasts, in ticks (an hour), if nobody else opens the door first.</summary>
        public const int PrankTicks = 2500;

        /// <summary>The prank ends: someone else opened the door, its hour is up, or its victim must not be kept (drafted, in need, in danger).</summary>
        public static bool PrankEnds(int now, int until, bool openedByOther, bool victimMustPass) => openedByOther || victimMustPass || now >= until;

        /// <summary>Solar panels in the long noon: a quarter more.</summary>
        public static float Solar(PresenceEffect e, float s) => e == PresenceEffect.LongNoon ? Scaled(1.25f, s) : 1f;

        // ================= Rock and trade =================

        /// <summary>Mining yields more under Smoulder's warm stone: a quarter more, none in his wrath.</summary>
        public static float MiningYield(PresenceEffect e, float s, bool harsh) => e == PresenceEffect.WarmStone && !harsh ? Scaled(1.25f, s) : 1f;

        /// <summary>The negotiator's trade price improvement under Ducat's glint: better, or worse in its wrath.</summary>
        public static float Trade(PresenceEffect e, float s, bool harsh) => e != PresenceEffect.Glint ? 0f : harsh ? -0.05f * Clamp01(s) : 0.08f * Clamp01(s);

        /// <summary>How many single coins trail out of a store toward the gate (none from a small one).</summary>
        public static int CoinTrail(int stack) => stack < 50 ? 0 : Math.Min(5, stack / 50);

        // ================= Light, sky and sleep =================

        /// <summary>How far lamps reach under the dark (their light's radius times this); shorter in its wrath.</summary>
        public static float Lamps(PresenceEffect e, float s, bool harsh) => e == PresenceEffect.Dark ? 1f - (harsh ? 0.5f : 0.35f) * Clamp01(s) : 1f;

        /// <summary>Minds open to the psychic under the dark: the psychic sensitivity's offset.</summary>
        public static float Psychic(PresenceEffect e, float s, bool harsh) => e == PresenceEffect.Dark ? (harsh ? 0.3f : 0.2f) * Clamp01(s) : 0f;

        /// <summary>The sky darker than the shared presence makes it: the dark's own.</summary>
        public static float Darker(PresenceEffect e, bool harsh) => e == PresenceEffect.Dark ? (harsh ? 0.4f : 0.25f) : 0f;

        /// <summary>The sky brighter and higher (Cirrus's): the light's lift.</summary>
        public static float Brighter(PresenceEffect e, bool harsh) => e == PresenceEffect.HighSky && !harsh ? 0.08f : 0f;

        /// <summary>
        /// The light Helios's long noon holds the sky at, at least: the light of the hour it came, if it came by day (half
        /// the sky's light or more); none for one that came after dark (the night stays night).
        /// </summary>
        public static float HeldGlow(PresenceEffect e, float glowAtStart) => e == PresenceEffect.LongNoon && glowAtStart >= 0.5f ? Math.Min(1f, glowAtStart) : 0f;

        /// <summary>
        /// The night's light under Selene's presence, at least, as much as her face is full (0 to 1): from a little at a thin
        /// moon to the light of dusk at the full; none in her wrath (the dark face).
        /// </summary>
        public static float NightGlow(PresenceEffect e, bool harsh, float moonLight) => e != PresenceEffect.MoonSleep || harsh ? 0f : 0.12f + 0.33f * Clamp01(moonLight);

        /// <summary>A sleeper's healing under Selene's presence: half again as fast; nobody else's.</summary>
        public static float SleepHealing(PresenceEffect e, float s, bool harsh, bool asleep) => e == PresenceEffect.MoonSleep && !harsh && asleep ? Scaled(1.5f, s) : 1f;

        /// <summary>How likely someone stops to look at the sky under Cirrus's presence, and how much they enjoy it.</summary>
        public static float GazeChance(PresenceEffect e, bool harsh) => e == PresenceEffect.HighSky && !harsh ? 2f : 1f;

        public static float GazeJoy(PresenceEffect e, bool harsh) => e == PresenceEffect.HighSky && !harsh ? 1.5f : 1f;

        /// <summary>Far lightning with no rain under Cirrus's presence: ticks between flickers (more often in its wrath).</summary>
        public static int FarLightningEvery(PresenceEffect e, bool harsh) => e != PresenceEffect.HighSky ? 0 : harsh ? 700 : 1500;

        // ================= Weather =================

        /// <summary>
        /// The weather a presence brings or holds, best first (the code takes the first that is in the game; empty: the
        /// weather goes its own way): Hollin's petals in spring (Alpha Biomes), Cirrus's clear sky (in its wrath the weather
        /// as it was, held), Hush's fine snow where the land is cold enough (Seasonal Weather's, else the game's) and fog
        /// where it isn't, Murmur's rain from its first hour (Odyssey's torrent in his wrath).
        /// </summary>
        public static List<string> Weather(PresenceEffect e, bool harsh, int season, float outdoorTemp, float hoursIn, string startWeather)
        {
            var list = new List<string>();
            switch (e)
            {
                case PresenceEffect.Growth:
                    if (season == 0 && !harsh) list.Add("AB_PetalStorms");
                    break;
                case PresenceEffect.HighSky:
                    if (harsh)
                    {
                        if (!string.IsNullOrEmpty(startWeather)) list.Add(startWeather);
                    }
                    else list.Add("Clear");
                    break;
                case PresenceEffect.WhiteSilence:
                    if (outdoorTemp < 2f)
                    {
                        list.Add("FogSnow");
                        list.Add("SnowGentle");
                    }
                    else list.Add("Fog");
                    break;
                case PresenceEffect.Rain:
                    if (hoursIn < 1f) break;
                    if (harsh) list.Add("TorrentialRain");
                    list.Add("Rain");
                    break;
            }
            return list;
        }

        // ================= How long =================

        /// <summary>
        /// How long the presence lasts, in ticks (an hour is 2500): the moment's own length (GodPresenceMath.ConditionTicks),
        /// unless the god's own presence asks for another: Selene's, begun at night, until seven in the morning, so the
        /// sleepers have it all night and the dreamer wakes under it; Murmur's at least six hours, so the rain comes and goes on.
        /// </summary>
        public static int Ticks(PresenceEffect e, PresenceKind k, int hourOfDay)
        {
            int ticks = GodPresenceMath.ConditionTicks(k);
            if (ticks <= 0) return 0;
            if (e == PresenceEffect.MoonSleep && (hourOfDay >= 18 || hourOfDay < 6))
            {
                int hours = hourOfDay >= 18 ? 24 - hourOfDay + 7 : 7 - hourOfDay;
                ticks = Math.Max(ticks, hours * 2500);
            }
            if (e == PresenceEffect.Rain) ticks = Math.Max(ticks, 6 * 2500);
            return ticks;
        }

        /// <summary>
        /// The presence stays while the god walks the land (the Old Horn grazing, the Guest under the roof, Tamiu among the
        /// graves): this much left at least while it is here, and begun again on the hour when another's has passed and it is
        /// still here.
        /// </summary>
        public static bool HeldWhileHere(PresenceEffect e) => e == PresenceEffect.Grazing || e == PresenceEffect.Warmth || e == PresenceEffect.Watch;

        /// <summary>Tamiu's watch is hers from her coming to her going: it ends once she has gone (after its first hour), not two hours on.</summary>
        public static bool EndsWhenGone(PresenceEffect e, bool here, int ticksPassed) => e == PresenceEffect.Watch && !here && ticksPassed >= 2500;

        /// <summary>The cats go and sit facing the graves this likely in an hour of Tamiu's watch (and once as she comes).</summary>
        public const float WatchHourChance = 0.2f;

        public const int HeldTicks = 2 * 2500;

        /// <summary>The presence's ticks left kept at least <see cref="HeldTicks"/> while its god is on the land.</summary>
        public static int Held(int ticksLeft, bool here) => here && ticksLeft < HeldTicks ? HeldTicks : ticksLeft;

        // ================= Dreams =================

        /// <summary>Selene's true dream: at the full moon, under her presence (not her wrath), told on waking at six.</summary>
        public static bool TrueDreamNow(PresenceEffect e, bool harsh, bool fullMoon, int hourOfDay) => e == PresenceEffect.MoonSleep && !harsh && fullMoon && hourOfDay == 6;

        /// <summary>Smoulder's warm voice: someone who slept under the mountain wakes at six under his presence and says they dreamt it.</summary>
        public static bool WarmVoiceNow(PresenceEffect e, int hourOfDay) => e == PresenceEffect.WarmStone && hourOfDay == 6;

        /// <summary>
        /// What a true dream at the full moon is of: the first of these that is true now (a raid on its way to the colony,
        /// visitors on theirs, something with teeth on the land, the stores going soft, someone sadder than they let on),
        /// else only the moon. The tag of the dream's own lines (true:raid and the rest).
        /// </summary>
        public static string TrueDream(bool raid, bool visitors, bool predator, bool rot, bool sad)
        {
            if (raid) return "true:raid";
            if (visitors) return "true:visitors";
            if (predator) return "true:predator";
            if (rot) return "true:rot";
            if (sad) return "true:sad";
            return "true:moon";
        }

        /// <summary>A queued event counts for a true dream when it comes to this colony within this many days.</summary>
        public const float TrueDreamDays = 2f;

        // ================= Murmur's croak =================

        /// <summary>The deep croak comes up from the wells this likely in an hour of Murmur's presence.</summary>
        public const float CroakChance = 0.4f;

        /// <summary>The Old Horn calls (a thrumbo's call) this likely in an hour while his presence lasts.</summary>
        public const float CallChance = 0.3f;

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
