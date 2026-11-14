using System;
using System.Collections.Generic;

namespace ColonyCouncil
{
    /// <summary>
    /// People who come to the colony's map dressed for the weather they will meet (the player, 14 November 2026: "the people
    /// who spawn from events, visitors, caravans, camps etc. just dress appropriately for the weather, so that they don't
    /// have to leave because of it. of course if it's super cold or any other extreme conditions that's fair"; then "for
    /// raiders it would also make sense if they come dressed for the weather, assuming their faction actually fits the
    /// clothing"). The rules of Visitors/WeatherDress.cs: who is dressed as they arrive, how far short of comfortable the
    /// stay's weather may leave them before they put something on, when clothing of their age can't keep them safe (the
    /// extremes, where the game's own leaving stays), what may come off and what may go on, what it may cost, the pick a
    /// layer at a time, and why a group still goes for the weather. Pure (no game types), so Tools/Tests can check it.
    /// </summary>
    public static class WeatherDressMath
    {
        // ---------------- The ages (the game's TechLevel, as numbers) ----------------

        public const int Undefined = 0, Animal = 1, Neolithic = 2, Medieval = 3, Industrial = 4, Spacer = 5;

        // ---------------- Who ----------------

        /// <summary>How someone came onto the map, as the game holds them just before they appear.</summary>
        public enum Came
        {
            /// <summary>Walked in from the edge, rode in, or was made there (the game's visitors, raids, Rimfolk's own).</summary>
            Walked,
            /// <summary>Out of a drop pod or something falling from the sky.</summary>
            Pod,
            /// <summary>Off a shuttle.</summary>
            Shuttle,
            /// <summary>Already on the map: set down by whoever carried them, out of a casket, a bed, a pod built here, a jump.</summary>
            FromHere
        }

        /// <summary>Whether someone appearing on the map is dressed for its weather, and if not, why not.</summary>
        public enum Who
        {
            Dress,
            /// <summary>The game isn't running yet, or the map is still being made.</summary>
            NotNow,
            /// <summary>Not one of the colony's homes.</summary>
            NotHome,
            /// <summary>In space: the game's own vacuum gear is what counts there.</summary>
            Vacuum,
            /// <summary>An animal, an insect, a mechanoid: nothing to dress.</summary>
            NotAPerson,
            /// <summary>Anomaly's entities and the walking dead: not people who dress.</summary>
            Entity,
            /// <summary>Already on the map, or there an hour ago: not arriving.</summary>
            AlreadyHere,
            /// <summary>Down on arrival (a crashed pod's survivor, someone carried in): no time to dress.</summary>
            Downed,
            /// <summary>A prisoner or a slave: they don't choose what they wear.</summary>
            Captive,
            /// <summary>A baby: carried in someone's arms.</summary>
            Baby,
            /// <summary>No people of their own, wild, or no age with clothing: nothing they could own.</summary>
            NoWays,
            /// <summary>The colony's own people coming home, or bought and sent down in a pod: as they are.</summary>
            OwnPeople
        }

        /// <summary>What the game knows of someone just before they appear on a map.</summary>
        public struct Arrival
        {
            public bool playing, generating, home, vacuum;
            public bool humanlike, toolUser, flesh, entity;
            public bool faction, wild, player;
            /// <summary>Their people's age (<see cref="Neolithic"/> and on); below that, no clothing of their own.</summary>
            public int tech;
            /// <summary>Never on any map before (a newly made person); <see cref="recentlyHere"/>: off a map within the hour.</summary>
            public bool fresh, recentlyHere;
            public bool baby, downed, captive;
            public Came came;
        }

        /// <summary>
        /// Who is dressed for the weather as they appear: a person of a people with clothing of their age, arriving on one of
        /// the colony's homes, on their feet, free and grown enough to dress. Friend or foe alike (visitors, traders,
        /// travellers, guests, allies, raiders); the colony's own only when they are new to it and walked or flew in (a
        /// wanderer who joins, a lodger), never its people coming home or someone bought and sent down in a pod.
        /// </summary>
        public static Who Qualifies(Arrival a)
        {
            if (!a.playing || a.generating) return Who.NotNow;
            if (!a.home) return Who.NotHome;
            if (a.vacuum) return Who.Vacuum;
            if (!a.humanlike || !a.toolUser || !a.flesh) return Who.NotAPerson;
            if (a.entity) return Who.Entity;
            if (a.came == Came.FromHere || a.recentlyHere) return Who.AlreadyHere;
            if (a.downed) return Who.Downed;
            if (a.captive) return Who.Captive;
            if (a.baby) return Who.Baby;
            if (!a.faction || a.wild || a.tech < Neolithic) return Who.NoWays;
            if (a.player && (!a.fresh || a.came == Came.Pod)) return Who.OwnPeople;
            return Who.Dress;
        }

        /// <summary>Someone off a map within this long and on one again is moving about the map, not arriving (an hour).</summary>
        public const int RecentTicks = 2500;

        // ---------------- The weather of their stay ----------------

        /// <summary>The hours of weather they dress for as they arrive: two days, the coming night and the next one.</summary>
        public const int ArrivalHours = 48;

        /// <summary>The longest stay anyone dresses ahead for: a season.</summary>
        public const int MaxHours = 15 * 24;

        /// <summary>
        /// The hours Rimfolk's own visit dresses ahead for: its hours, its nights, and the half day they may wait on (at
        /// least <see cref="ArrivalHours"/>, at most <see cref="MaxHours"/>).
        /// </summary>
        public static int VisitHours(float hours, int nights)
        {
            if (float.IsNaN(hours) || hours < 0f) hours = 0f;
            int h = (int)Math.Round(Math.Min(hours, MaxHours)) + Math.Max(0, nights) * 24 + 12;
            return Math.Max(ArrivalHours, Math.Min(MaxHours, h));
        }

        /// <summary>
        /// Short of comfortable by this much or less, they come as they are (the game's own: it dresses for a season four
        /// degrees past comfortable, PawnApparelGenerator.CalculateNeededWarmth), and they stop putting more on.
        /// </summary>
        public const float Enough = 4f;

        /// <summary>A layer goes on only if it brings them at least this much nearer comfortable.</summary>
        public const float MinGain = 0.5f;

        /// <summary>Two layers this close in what they leave short count as alike, and the one of their own kind wins.</summary>
        public const float Tie = 2f;

        /// <summary>At most this many layers put on (a coat, a hat, gloves, a scarf, a shirt).</summary>
        public const int MaxLayers = 5;

        /// <summary>How far, in degrees, comfortable from <paramref name="comfyMin"/> to <paramref name="comfyMax"/> falls short of the stay.</summary>
        public static float Short(float comfyMin, float comfyMax, float low, float high) => CampWeatherMath.Shortfall(comfyMin, comfyMax, low, high);

        /// <summary>Nothing from <paramref name="low"/> to <paramref name="high"/> would hurt them: the game's own safe range, ten degrees past comfortable.</summary>
        public static bool SafeThrough(float comfyMin, float comfyMax, float low, float high) => CampWeatherMath.WouldStop(low, high, comfyMin, comfyMax);

        /// <summary>What comes of dressing someone for their stay.</summary>
        public enum Dressing
        {
            /// <summary>Dressed well enough for it as they came: nothing changes.</summary>
            AsTheyAre,
            /// <summary>What they put on keeps them safe through it (and as near comfortable as their clothes go).</summary>
            Dressed,
            /// <summary>
            /// Past what any clothing of their age keeps out: the extreme cold or heat. They are left as they came, and the
            /// game's own leaving (and Rimfolk's, on the same line) stays.
            /// </summary>
            PastTheirClothes
        }

        /// <summary>
        /// The verdict, from how comfortable they are now (<paramref name="nowMin"/>, <paramref name="nowMax"/>) and dressed
        /// as well as their age allows (<paramref name="bestMin"/>, <paramref name="bestMax"/>).
        /// </summary>
        public static Dressing Outcome(float nowMin, float nowMax, float bestMin, float bestMax, float low, float high)
        {
            if (Short(nowMin, nowMax, low, high) <= Enough) return Dressing.AsTheyAre;
            return SafeThrough(bestMin, bestMax, low, high) ? Dressing.Dressed : Dressing.PastTheirClothes;
        }

        // ---------------- What may come off, what may go on ----------------

        /// <summary>A piece this armoured by its make (its material's armour times this) is armour, never a plain layer.</summary>
        public const float ArmourFactor = 0.45f;

        /// <summary>A piece made of no material with this much sharp armour of its own is armour (a flak jacket, power armour).</summary>
        public const float ArmourSharp = 0.3f;

        /// <summary>Armour: of the game's armour kinds, or armoured by its make or of its own.</summary>
        public static bool Armour(bool armourKind, float stuffArmourFactor, float ownSharp) =>
            armourKind || stuffArmourFactor >= ArmourFactor || ownSharp >= ArmourSharp;

        /// <summary>
        /// What someone wears that is never taken off for the weather: armour, a belt or pack, gear that does something (a
        /// shield, a jump pack, a piece with an ability), a piece of its own kind of thing (a mod's special apparel), what is
        /// locked on them, what their title or their role in the faith asks them to wear.
        /// </summary>
        public static bool Kept(bool armour, bool utility, bool gear, bool special, bool locked, bool required) =>
            armour || utility || gear || special || locked || required;

        /// <summary>
        /// What may be put on against the weather: a plain layer that keeps out cold or heat, that the game itself would
        /// hand out for warmth, worn by people of some kind; no armour, no belt or gear, nothing special, and nothing that
        /// slows or hinders (a straitjacket keeps out the cold too).
        /// </summary>
        public static bool Offered(bool insulates, bool forWarmth, bool generated, bool armour, bool utility, bool gear, bool special, bool hinders) =>
            insulates && forWarmth && generated && !armour && !utility && !gear && !special && !hinders;

        /// <summary>
        /// A piece of <paramref name="piece"/>'s age is theirs to wear, for a people of <paramref name="people"/>'s age: nothing
        /// past their own age, nothing past the colony's era in a colony of the old ways; and the game's own rule against the
        /// tribes' hides for those past them and the industrial for the tribes, unless the piece is meant for any age or is
        /// of their own kind's (a parka is tribal work, and an outlander's too).
        /// </summary>
        public static bool AgeFits(int piece, int people, bool anyAge, bool theirKind, bool preIndustrialColony)
        {
            if (piece > people) return false;
            if (preIndustrialColony && piece > Medieval) return false;
            if (anyAge || theirKind) return true;
            if (people >= Industrial && piece == Neolithic) return false;
            if (people == Neolithic && piece >= Industrial) return false;
            return true;
        }

        /// <summary>How well a piece fits them: of their own kind's tags counts most, then a look of their faith's style.</summary>
        public static int Fit(bool theirKind, bool theirStyle) => (theirKind ? 2 : 0) + (theirStyle ? 1 : 0);

        // ---------------- What it may cost ----------------

        /// <summary>The game's own free warm layer: a plain cloth parka's (and tuque's) worth and a little more.</summary>
        public const float FreeShare = 1.3f;

        /// <summary>No single piece dearer than a cloth parka and a half (as the camps always dressed).</summary>
        public const float PieceShare = 1.5f;

        /// <summary>Raiders' gear keeps close to its worth: what they put on, less what comes off, is at most this share of it.</summary>
        public const float HostileShare = 0.15f;

        /// <summary>
        /// How much more their clothes may be worth when they're dressed, in silver (new pieces less those that came off):
        /// for raiders, a tuque's worth or a small share of what they wear, so a raid's loot is what it would have been; for
        /// anyone else, the game's own free parka and tuque, or what their kind would have spent on clothes and didn't.
        /// </summary>
        public static float Room(bool hostile, float wornWorth, float budgetMax, float parka, float tuque)
        {
            wornWorth = Math.Max(0f, wornWorth);
            if (hostile) return Math.Max(tuque * FreeShare, wornWorth * HostileShare);
            return Math.Max((parka + tuque) * FreeShare, budgetMax - wornWorth);
        }

        // ---------------- The pick ----------------

        /// <summary>One piece of apparel as the pick sees it: what it keeps out (degrees), its worth, how well it fits them, kept on.</summary>
        public sealed class Piece
        {
            public readonly float cold, heat, worth;
            public readonly int fit;
            public readonly bool kept;

            public Piece(float cold, float heat, float worth, int fit = 0, bool kept = false)
            {
                this.cold = cold;
                this.heat = heat;
                this.worth = worth;
                this.fit = fit;
                this.kept = kept;
            }
        }

        /// <summary>What the pick chose: the offered pieces to put on (in order), the worn ones that come off, what it cost, how comfortable it leaves them.</summary>
        public sealed class Choice
        {
            public readonly List<int> on = new List<int>();
            public readonly List<int> off = new List<int>();
            public float spent, comfyMin, comfyMax;
        }

        /// <summary>
        /// Dresses someone, comfortable bare from <paramref name="bareMin"/> to <paramref name="bareMax"/> and wearing
        /// <paramref name="worn"/>, for a stay from <paramref name="low"/> to <paramref name="high"/>, a layer at a time from
        /// <paramref name="offered"/>: each layer brings them nearer comfortable, never takes off a kept piece, and stays within
        /// <paramref name="room"/>. Of the layers that would make them comfortable enough, the one of their own kind and then
        /// the cheapest; else the one that leaves them least short (alike within <see cref="Tie"/>: their own kind, then the
        /// cheapest). A layer of the pick's own gives way only to one clearly better (by <see cref="Tie"/>). <paramref name="clashWorn"/>(offered, worn) and
        /// <paramref name="clashOffered"/>(offered, offered): the two can't be worn together.
        /// </summary>
        public static Choice Choose(IList<Piece> worn, IList<Piece> offered, Func<int, int, bool> clashWorn, Func<int, int, bool> clashOffered,
            float bareMin, float bareMax, float low, float high, float room)
        {
            var c = new Choice();
            var wearing = new List<int>();
            for (int i = 0; i < (worn?.Count ?? 0); i++) wearing.Add(i);
            float cold = 0f, heat = 0f;
            foreach (int w in wearing)
            {
                cold += worn[w].cold;
                heat += worn[w].heat;
            }
            int n = offered?.Count ?? 0;
            var gone = new List<int>();
            var replaced = new List<int>();
            for (int step = 0; step < MaxLayers * 2 && c.on.Count < MaxLayers; step++)
            {
                float min = bareMin - cold, max = bareMax + heat;
                float shortNow = Short(min, max, low, high);
                if (shortNow <= Enough) break;
                int best = -1;
                float bestShort = 0f, bestCost = 0f;
                bool bestCovers = false;
                for (int o = 0; o < n; o++)
                {
                    if (c.on.Contains(o)) continue;
                    var p = offered[o];
                    float offCold = 0f, offHeat = 0f, offWorth = 0f;
                    bool blocked = false;
                    foreach (int w in wearing)
                    {
                        if (!clashWorn(o, w)) continue;
                        if (worn[w].kept)
                        {
                            blocked = true;
                            break;
                        }
                        offCold += worn[w].cold;
                        offHeat += worn[w].heat;
                        offWorth += worn[w].worth;
                    }
                    if (blocked) continue;
                    bool ownGoes = false;
                    foreach (int q in c.on)
                    {
                        if (!clashOffered(o, q)) continue;
                        ownGoes = true;
                        offCold += offered[q].cold;
                        offHeat += offered[q].heat;
                        offWorth += offered[q].worth;
                    }
                    float cost = p.worth - offWorth;
                    if (c.spent + cost > room + 0.01f) continue;
                    float s = Short(min + offCold - p.cold, max - offHeat + p.heat, low, high);
                    // A layer of their own put on gives way only to one clearly better, so their kind's coat isn't swapped for a stranger's barely warmer.
                    if (s > shortNow - (ownGoes ? Tie : MinGain)) continue;
                    bool covers = s <= Enough;
                    bool better;
                    if (best < 0) better = true;
                    else if (covers != bestCovers) better = covers;
                    else if (covers) better = p.fit > offered[best].fit || p.fit == offered[best].fit && cost < bestCost;
                    else if (s < bestShort - Tie) better = true;
                    else if (s > bestShort + Tie) better = false;
                    else better = p.fit > offered[best].fit || p.fit == offered[best].fit && (cost < bestCost || cost == bestCost && s < bestShort);
                    if (!better) continue;
                    best = o;
                    bestShort = s;
                    bestCost = cost;
                    bestCovers = covers;
                }
                if (best < 0) break;
                gone.Clear();
                foreach (int w in wearing)
                    if (clashWorn(best, w))
                        gone.Add(w);
                foreach (int w in gone)
                {
                    wearing.Remove(w);
                    c.off.Add(w);
                    cold -= worn[w].cold;
                    heat -= worn[w].heat;
                }
                replaced.Clear();
                foreach (int q in c.on)
                    if (clashOffered(best, q))
                        replaced.Add(q);
                foreach (int q in replaced)
                {
                    c.on.Remove(q);
                    cold -= offered[q].cold;
                    heat -= offered[q].heat;
                }
                c.on.Add(best);
                cold += offered[best].cold;
                heat += offered[best].heat;
                c.spent += bestCost;
            }
            c.comfyMin = bareMin - cold;
            c.comfyMax = bareMax + heat;
            return c;
        }

        // ---------------- A piece that hinders them ----------------

        /// <summary>
        /// A piece they came in that hinders them (it lowers their moving, work, aim, hit or dodge: the game's free warm layer
        /// puts a straitjacket on a wayfarer when it's the cheapest warm thing about), that their kind neither requires nor
        /// asks for by its tags, and that isn't kept (armour, a title's or a role's): swapped for a plain piece of their age,
        /// whatever the weather.
        /// </summary>
        public static bool OutOfPlace(bool hinders, bool kept, bool askedFor) => hinders && !kept && !askedFor;

        /// <summary>
        /// The plain piece to take the place of worn piece <paramref name="old"/> (<see cref="OutOfPlace"/>), or -1 to keep it:
        /// one that takes exactly its place (it can't be worn with it, and can with all else they wear), keeps them as warm
        /// for the stay (no further short of comfortable than half a degree, and still safe if they were), and costs what
        /// came off less than <paramref name="room"/>. Their own kind's first, then the one leaving them least short (alike
        /// within <see cref="Tie"/>), then the cheapest. None that keeps them as warm: it stays on rather than leave them cold.
        /// </summary>
        public static int Swap(IList<Piece> worn, int old, IList<Piece> offered, Func<int, int, bool> clashWorn, float bareMin, float bareMax, float low, float high, float room)
        {
            if (worn == null || old < 0 || old >= worn.Count || offered == null) return -1;
            float cold = 0f, heat = 0f;
            foreach (var w in worn)
            {
                cold += w.cold;
                heat += w.heat;
            }
            float min = bareMin - cold, max = bareMax + heat;
            float before = Short(min, max, low, high);
            bool safe = SafeThrough(min, max, low, high);
            int best = -1;
            float bestShort = 0f, bestCost = 0f;
            for (int o = 0; o < offered.Count; o++)
            {
                if (!clashWorn(o, old)) continue;
                bool more = false;
                for (int w = 0; w < worn.Count && !more; w++)
                    if (w != old && clashWorn(o, w))
                        more = true;
                if (more) continue;
                var p = offered[o];
                float cost = p.worth - worn[old].worth;
                if (cost > room + 0.01f) continue;
                float aMin = min + worn[old].cold - p.cold, aMax = max - worn[old].heat + p.heat;
                float s = Short(aMin, aMax, low, high);
                if (s > before + MinGain || safe && !SafeThrough(aMin, aMax, low, high)) continue;
                bool better = best < 0 || p.fit > offered[best].fit
                              || p.fit == offered[best].fit && (s < bestShort - Tie || s <= bestShort + Tie && cost < bestCost);
                if (!better) continue;
                best = o;
                bestShort = s;
                bestCost = cost;
            }
            return best;
        }

        // ---------------- Why they still go ----------------

        /// <summary>Rimfolk's own visitors go on once the cold or heat starts to hurt them this much (early, before the game's own).</summary>
        public const float Suffering = 0.04f;

        /// <summary>The game's own visitors, traders and guests leave past this much hypothermia or heatstroke (Trigger_PawnExperiencingDangerousTemperatures).</summary>
        public const float GameLeaves = 0.15f;

        public enum Why
        {
            None,
            Cold,
            Heat,
            Hunger
        }

        /// <summary>What is getting to one of them: the cold or the heat by whichever has taken hold more, else hunger.</summary>
        public static Why Suffers(float hypothermia, float heatstroke, bool hungry)
        {
            if (hypothermia >= Suffering && hypothermia >= heatstroke) return Why.Cold;
            if (heatstroke >= Suffering) return Why.Heat;
            return hungry ? Why.Hunger : Why.None;
        }

        /// <summary>A night out from <paramref name="low"/> to <paramref name="high"/> would hurt them: by the cold, the heat, or neither.</summary>
        public static Why NightWhy(float comfyMin, float comfyMax, float low, float high)
        {
            if (low < comfyMin - CampWeatherMath.SafeMargin) return Why.Cold;
            if (high > comfyMax + CampWeatherMath.SafeMargin) return Why.Heat;
            return Why.None;
        }

        /// <summary>The message key for Rimfolk's own visitors gone for the weather: the extreme cold, the extreme heat, or hunger.</summary>
        public static string VisitKey(Why why) => "CC_Visit_Weather_" + (why == Why.Heat ? "heat" : why == Why.Hunger ? "hunger" : "cold");

        /// <summary>The message key for the game's own groups leaving for the temperature: the cold if it has taken hold of them more, else the heat.</summary>
        public static string GameKey(float hypothermia, float heatstroke) => hypothermia >= heatstroke ? "CC_WeatherLeave_Cold" : "CC_WeatherLeave_Heat";
    }
}
