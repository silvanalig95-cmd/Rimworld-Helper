using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>What one of the colony's own Pokémon does at home by its type (Integrations/PokeWorldTypes.cs).</summary>
    public enum PokemonPower
    {
        /// <summary>Fire: warms the room it rests in, like a little heater.</summary>
        Warm,
        /// <summary>Ice: cools the room it rests in when the room is hot.</summary>
        Cool,
        /// <summary>Grass: the crops close by grow a quarter faster.</summary>
        Growth,
        /// <summary>Electric: tops up a battery of the colony's it rests beside.</summary>
        Battery,
        /// <summary>Psychic: calms a colonist close by who is near a mental break.</summary>
        Calm,
        /// <summary>Fighting: spars with a colonist at their leisure (melee practice).</summary>
        Spar,
        /// <summary>Water: puts out a fire near it.</summary>
        Douse
    }

    /// <summary>The sign an omen animal was born under (Animals/OmenAnimals.cs).</summary>
    public enum OmenSign
    {
        None,
        /// <summary>Born while the sun was eclipsed over its map.</summary>
        Eclipse,
        /// <summary>Born under an aurora.</summary>
        Aurora,
        /// <summary>A shiny Pokémon born or hatched to the colony.</summary>
        Shiny,
        /// <summary>Born on a day the colony keeps (a tradition, its founding day, a day the player named, one kept before).</summary>
        KeptDay
    }

    /// <summary>
    /// The numbers behind the colony's animals with powers: the Pokémon types at home, the mages' familiars, the omen
    /// animals and the sacred ones. No game types, so the tests can run them; the files that read the game are
    /// Integrations/PokeWorldTypes.cs, Integrations/MagicFamiliars.cs, Animals/OmenAnimals.cs and Animals/SacredAnimals.cs,
    /// all run from Animals/AnimalPowers.cs every 250 ticks (Sched.RarePowers).
    /// </summary>
    public static class AnimalPowersMath
    {
        /// <summary>Ticks between two of the powers' passes (the game's rare tick).</summary>
        public const int PassTicks = 250;

        /// <summary>The seconds of game time one pass stands for (the game pushes heat once a second: 60 ticks).</summary>
        public const float PassSeconds = PassTicks / 60f;

        public const int DayTicks = 60000;
        public const int HourTicks = 2500;

        // ===================== The Pokémon types =====================

        /// <summary>What a PokéWorld type does at home, or null for one that does nothing here (Normal, Flying, Dragon...).</summary>
        public static PokemonPower? PowerOf(string type)
        {
            switch (type)
            {
                case "Fire": return PokemonPower.Warm;
                case "Ice": return PokemonPower.Cool;
                case "Grass": return PokemonPower.Growth;
                case "Electric": return PokemonPower.Battery;
                case "Psychic": return PokemonPower.Calm;
                case "Fighting": return PokemonPower.Spar;
                case "Water": return PokemonPower.Douse;
                default: return null;
            }
        }

        /// <summary>A Pokémon's powers as bits (one per <see cref="PokemonPower"/>): a Water and Ice one both douses and cools.</summary>
        public static int Mask(IEnumerable<string> types)
        {
            int mask = 0;
            if (types == null) return 0;
            foreach (var t in types)
            {
                var p = PowerOf(t);
                if (p != null) mask |= Bit(p.Value);
            }
            return mask;
        }

        public static int Bit(PokemonPower p) => 1 << (int)p;

        public static bool Has(int mask, PokemonPower p) => (mask & Bit(p)) != 0;

        // ---------------- Fire and Ice: the room's warmth ----------------

        /// <summary>
        /// Heat a Fire type gives off while it rests, in the game's heat units a second: about a quarter of a heater's 21,
        /// a little more than a torch lamp. Times its body size, from half to half again.
        /// </summary>
        public const float HeatPerSecond = 6f;

        public const float MinBody = 0.5f, MaxBody = 1.5f;

        /// <summary>A Fire type warms a room only while it is colder than this, and never past it (a heater's own default).</summary>
        public const float WarmTo = 21f;

        /// <summary>An Ice type cools a room only while it is warmer than this, and never below it.</summary>
        public const float CoolTo = 23f;

        /// <summary>Two of them in one room at most: a third adds nothing.</summary>
        public const int MaxPerRoom = 2;

        public static float BodyFactor(float bodySize) => Clamp(bodySize, MinBody, MaxBody);

        /// <summary>
        /// The heat one pass pushes into a room of <paramref name="cells"/> cells at <paramref name="roomTemp"/> °C (the game
        /// raises a room's temperature by the energy over its cells): warming up to <see cref="WarmTo"/> or cooling down to
        /// <see cref="CoolTo"/>, never past it, and nothing at all in a room already there. Negative when cooling.
        /// </summary>
        public static float HeatPush(float roomTemp, int cells, float bodySize, bool warm)
        {
            if (cells <= 0) return 0f;
            float full = HeatPerSecond * BodyFactor(bodySize) * PassSeconds;
            if (warm)
            {
                if (roomTemp >= WarmTo) return 0f;
                return Math.Min(full, (WarmTo - roomTemp) * cells);
            }
            if (roomTemp <= CoolTo) return 0f;
            return -Math.Min(full, (roomTemp - CoolTo) * cells);
        }

        /// <summary>How many degrees a push moves a room of that size (what the game does with it).</summary>
        public static float Degrees(float energy, int cells) => cells <= 0 ? 0f : energy / cells;

        // ---------------- Grass: the crops close by ----------------

        /// <summary>The crops within this radius of a resting Grass type grow this much faster (a quarter).</summary>
        public const float GrowthShare = 0.25f;

        public const float GrowthRadius = 2.9f;

        /// <summary>
        /// The growth one pass adds to a crop (its growth is 0 to 1): a quarter of what the crop grows by itself in that time,
        /// by its own growing days and its growth rate now (fertility, light, warmth). Nothing for a crop not growing now.
        /// </summary>
        public static float GrowthPush(float growDays, float growthRate)
        {
            if (growDays <= 0f || growthRate <= 0f) return 0f;
            return GrowthShare * PassTicks / (DayTicks * growDays) * growthRate;
        }

        /// <summary>Plants rest at night (the game's own hours: before a quarter of the day and after four fifths), so no push then.</summary>
        public static bool PlantsAwake(float dayPercent) => dayPercent >= 0.25f && dayPercent <= 0.8f;

        // ---------------- Electric: a battery beside it ----------------

        /// <summary>What an Electric type gives a battery it rests beside, in watts: a small trickle (a battery holds 600 Wd).</summary>
        public const float BatteryWatts = 100f;

        /// <summary>The watt-days one pass adds: watts times the pass's share of a day.</summary>
        public static float BatteryPush(float watts) => Math.Max(0f, watts) * PassTicks / DayTicks;

        // ---------------- Psychic: a colonist near a break ----------------

        /// <summary>A colonist within this many cells, in its sight.</summary>
        public const float CalmRadius = 6f;

        /// <summary>Near a break: their mood below the minor break threshold, or less than this above it.</summary>
        public const float CalmMargin = 0.05f;

        public static bool NearBreak(float mood, float minorThreshold) => mood < minorThreshold + CalmMargin;

        /// <summary>One calming for a colonist in two days, and one a day for each Psychic type.</summary>
        public const int CalmPersonCooldown = 2 * DayTicks;

        public const int CalmPokemonCooldown = DayTicks;

        /// <summary>Off its cooldown: never used, or long enough ago (a tick from before a load's clock counts as long ago).</summary>
        public static bool Ready(int lastTick, int now, int cooldown) => lastTick < 0 || lastTick > now || now - lastTick >= cooldown;

        // ---------------- Fighting: a spar at leisure ----------------

        /// <summary>A practice swing every so often, each way; melee experience for the colonist's own swings.</summary>
        public const int SparSwingTicks = 110;

        public const float SparXpSwing = 15f;

        /// <summary>How long a spar lasts.</summary>
        public const int SparMinTicks = 900, SparMaxTicks = 1500;

        /// <summary>
        /// One spar in 25 ends with a blow that lands (a bruise of 3 to 6), as a drill's, before the setting "Hurts from
        /// everyday life" and the storyteller scale it (Harm/Harm.cs); never more than the shared cap allows.
        /// </summary>
        public const float SparBlowChance = 1f / 25f;

        public const float SparLow = 3f, SparHigh = 6f;

        /// <summary>A colonist seeks out a Fighting type within this many cells.</summary>
        public const float SparReach = 40f;

        // ---------------- Water: a fire near it ----------------

        /// <summary>A fire within this radius is doused, a bit each pass.</summary>
        public const float DouseRadius = 2.9f;

        /// <summary>The game's extinguishing damage on it each pass (a fire's size falls by a hundredth of it): rain does 10, a beater 32.</summary>
        public const float DouseDamage = 40f;

        /// <summary>A fire on the colony's ground within this many cells: an idle Water type goes over to it.</summary>
        public const float DouseWalk = 10f;

        // ---------------- Lines ----------------

        /// <summary>Once a day at most for each power on a map, a colonist who sees it says a word, by this chance when they could.</summary>
        public const float LineChance = 1f / 8f;

        public const int LineGap = DayTicks;

        // ===================== Familiars =====================

        /// <summary>A familiar lends while it is within this many cells of its mage.</summary>
        public const float FamiliarNear = 12f;

        /// <summary>
        /// Mana a familiar near adds each pass: 0.0004, about a tenth of a full pool a day near it all day; a mage's own
        /// gain is about 0.48 a day (RimWorld of Magic: 0.0012 each 150 ticks).
        /// </summary>
        public const float FamiliarManaPerPass = 0.0004f;

        /// <summary>The mana after a familiar's pass: never above the pool's top, and nothing for one already full.</summary>
        public static float ManaAfter(float current, float max, float add)
        {
            if (current >= max) return current;
            return Math.Min(max, current + Math.Max(0f, add));
        }

        /// <summary>Its death empties the mage's mana and leaves RimWorld of Magic's arcane weakness at this (it falls 12 a day: about eight hours).</summary>
        public const float WeaknessOnDeath = 4f;

        /// <summary>A mage bonded to an animal, with no familiar, chooses it on their own by this chance a day.</summary>
        public const float OwnChoiceChance = 0.25f;

        /// <summary>The binding: the mage stays with the animal this long.</summary>
        public const int BindingTicks = 3000;

        /// <summary>Only by day (from eight in the morning to nine at night), so it is seen.</summary>
        public static bool BindHour(int hour) => hour >= 8 && hour <= 20;

        /// <summary>A binding not begun within this many days is let go.</summary>
        public const int BindingWaitDays = 10;

        public static bool BindingStale(int since, int now) => since >= 0 && since <= now && now - since > BindingWaitDays * DayTicks;

        /// <summary>One familiar a mage; at most this many kept, and this many bindings waiting.</summary>
        public const int MaxFamiliars = 12;

        public const int MaxBindings = 6;

        // ===================== Omen animals =====================

        /// <summary>
        /// The sign a birth came under: an eclipse first, then an aurora, a shiny Pokémon, a day the colony keeps; none
        /// when nothing was in the sky or on the calendar.
        /// </summary>
        public static OmenSign SignOf(bool eclipse, bool aurora, bool shiny, bool keptDay)
        {
            if (eclipse) return OmenSign.Eclipse;
            if (aurora) return OmenSign.Aurora;
            if (shiny) return OmenSign.Shiny;
            if (keptDay) return OmenSign.KeptDay;
            return OmenSign.None;
        }

        /// <summary>A day on the calendar that counts as kept: dated, not someone's own, and a tradition, the founding, the player's own or kept before.</summary>
        public static bool KeptDay(bool dated, bool personal, bool tradition, bool founding, bool byPlayer, int keptYears) =>
            dated && !personal && (tradition || founding || byPlayer || keptYears > 0);

        /// <summary>Twenty days at least between two omens in the colony, three living at most, and half the births on a kept day.</summary>
        public const int OmenGapDays = 20;

        public const int MaxOmens = 3;

        public const float KeptDayChance = 0.5f;

        /// <summary>Records of living omens kept at most (the dead are told and let go).</summary>
        public const int MaxOmenRecords = 6;

        /// <summary>A newborn seen within this long of its birth (the look is hourly): the sign is the one it was born under.</summary>
        public const int BornWithinTicks = 2 * HourTicks;

        /// <summary>A birth under a sign is an omen: a sign, room among the living, the gap since the last, and for a kept day half the time.</summary>
        public static bool OmenDue(OmenSign sign, int lastOmenTick, int now, int living, float roll)
        {
            if (sign == OmenSign.None || living >= MaxOmens) return false;
            if (lastOmenTick >= 0 && lastOmenTick <= now && now - lastOmenTick < OmenGapDays * DayTicks) return false;
            return sign != OmenSign.KeptDay || roll < KeptDayChance;
        }

        /// <summary>It thrives: alive, up, not ill and not starving.</summary>
        public static bool Thrives(bool alive, bool downed, bool ill, bool starving) => alive && !downed && !ill && !starving;

        /// <summary>The sign's key for lines and words ("eclipse", "aurora", "shiny", "keptday").</summary>
        public static string Key(OmenSign sign) => sign == OmenSign.None ? "none" : sign.ToString().ToLowerInvariant();

        // ===================== Sacred animals =====================

        /// <summary>A sacred animal's death is seen within this long (the look is hourly).</summary>
        public const int SacredFreshTicks = 2 * HourTicks;

        /// <summary>One word at the body a map an hour at most, and one about a hunt marked.</summary>
        public const int SacredLineGap = HourTicks;

        /// <summary>
        /// Who mourns a sacred animal that fell on the land: a believer whose own faith holds its kind sacred, not when the
        /// game already mourns it for them (a tame one of the colony's: its own memory).
        /// </summary>
        public static bool Mourns(bool believer, bool tameOfColony) => believer && !tameOfColony;

        // ===================== Helpers =====================

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
