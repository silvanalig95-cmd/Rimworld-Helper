using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>
    /// Context-aware lines. A line may start with tags in square brackets, e.g.
    /// <c>[winter,night] The cold's in my teeth.</c> A tagged line is only used when every tag matches the
    /// moment, and matching lines are preferred over untagged ones, so colonists talk about what is actually
    /// happening to them. Tags:
    /// <list type="bullet">
    /// <item>season: spring, summer, fall, winter; time: morning, day, evening, night</item>
    /// <item>weather: rain, snow, fog, storm, clear, hot, cold, and weather:WeatherDef (weather:DoubleRainbow)</item>
    /// <item>mood: lowmood, highmood; trust: trusting, doubtful, bitter</item>
    /// <item>tech: tribal, medieval, industrial, spacer; wealth: poor, rich; colony: small, large</item>
    /// <item>the speaker: child, teen, old, hurt, trait:DefName, skill:DefName (passionate about it)</item>
    /// <item>events: afterraid, grieving, newcomer, repeat (asked before), promised, petition, forother</item>
    /// <item>right now: eating, inbed, relaxing, working, work:WorkTypeDef (Cooking, Growing, Mining...), outside, indoors, inownroom</item>
    /// <item>who they are (see PeopleTags): meme:MemeDef, xeno:XenotypeDef, tag groups (mage, fighter...), nosleep, nofood, hemogenic, psycaster</item>
    /// </list>
    /// Prefix a tag with ! to negate it, separate alternatives with |, e.g. <c>[night|evening,!child]</c>.
    /// </summary>
    public static class Flavor
    {
        /// <summary>
        /// While set (0..23), the hour a line is about instead of now: an everyday journal entry, written once a day,
        /// about something that happened at another hour. -1 when not set.
        /// </summary>
        public static int AtHour = -1;

        public static HashSet<string> TagsFor(Pawn p, Wish w = null)
        {
            var tags = new HashSet<string>();
            var map = p?.MapHeld ?? Find.CurrentMap;
            try
            {
                if (map != null)
                {
                    switch (GenLocalDate.Season(map))
                    {
                        case Season.Spring: tags.Add("spring"); break;
                        case Season.Summer:
                        case Season.PermanentSummer: tags.Add("summer"); break;
                        case Season.Fall: tags.Add("fall"); break;
                        case Season.Winter:
                        case Season.PermanentWinter: tags.Add("winter"); break;
                    }
                    int hour = AtHour >= 0 ? AtHour : GenLocalDate.HourOfDay(map);
                    tags.Add(hour < 6 || hour >= 22 ? "night" : hour < 11 ? "morning" : hour < 18 ? "day" : "evening");
                    var weather = map.weatherManager?.curWeather;
                    if (weather != null)
                    {
                        if (weather.snowRate > 0.01f) tags.Add("snow");
                        else if (weather.rainRate > 0.01f) tags.Add("rain");
                        if (weather.defName.Contains("Fog")) tags.Add("fog");
                        // The weather by name, for lines and encounters written for one (Seasonal Weather's double rainbow).
                        tags.Add(DefTag("weather:", weather));
                        if (IsStorm(weather)) tags.Add("storm");
                        if (!weather.isBad && weather.rainRate <= 0.01f && weather.snowRate <= 0.01f) tags.Add("clear");
                    }
                    float temp = map.mapTemperature.OutdoorTemp;
                    if (temp >= 32f) tags.Add("hot");
                    else if (temp <= 0f) tags.Add("cold");
                    int sinceThreat = Find.TickManager.TicksGame - map.storyState.LastThreatBigTick;
                    if (map.storyState.LastThreatBigTick > 0 && sinceThreat < GenDate.TicksPerDay * 2) tags.Add("afterraid");
                    float wealth = map.wealthWatcher.WealthTotal;
                    if (wealth < 40000f) tags.Add("poor");
                    else if (wealth > 250000f) tags.Add("rich");
                    int colonists = map.mapPawns.FreeColonistsCount;
                    if (colonists <= 3) tags.Add("small");
                    else if (colonists >= 10) tags.Add("large");
                }

                AddEraTags(tags);

                if (p != null)
                {
                    float mood = p.needs?.mood?.CurLevelPercentage ?? 0.5f;
                    if (mood < 0.3f) tags.Add("lowmood");
                    else if (mood > 0.7f) tags.Add("highmood");
                    if (p.DevelopmentalStage.Child()) tags.Add("child");
                    else if (p.ageTracker != null && HumanAge(p) < 20f) tags.Add("teen");
                    else if (p.ageTracker != null && HumanAge(p) >= 60f) tags.Add("old");
                    if (p.health?.hediffSet != null && p.health.summaryHealth.SummaryHealthPercent < 0.85f) tags.Add("hurt");
                    if (p.story?.traits != null)
                    {
                        foreach (var t in p.story.traits.allTraits)
                        {
                            tags.Add(DefTag("trait:", t.def));
                            // Degree traits also get their degree's name: trait:Lazy, trait:Teetotaler, trait:IronWilled.
                            if (t.def.degreeDatas != null && t.def.degreeDatas.Count > 1)
                            {
                                string name = DegreeTraitTag(t.CurrentData);
                                if (name != null) tags.Add(name);
                            }
                        }
                    }
                    if (p.skills != null)
                    {
                        foreach (var s in p.skills.skills)
                        {
                            if (People.Passionate(s) && !s.TotallyDisabled) tags.Add(DefTag("skill:", s.def));
                        }
                    }
                    // Memes, xenotype, mods' classes and traits by group (mage, fighter...), needs (nosleep): see PeopleTags.
                    PeopleTags.AddTo(p, tags);
                    if (p.records != null && p.records.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) < GenDate.TicksPerDay * 10) tags.Add("newcomer");
                    if (IsGrieving(p)) tags.Add("grieving");

                    // What they're doing and where, so a scene can catch them in the act.
                    var job = p.CurJob;
                    if (p.InBed()) tags.Add("inbed");
                    else if (job != null)
                    {
                        if (job.def == JobDefOf.Ingest) tags.Add("eating");
                        else if (job.def.joyKind != null) tags.Add("relaxing");
                        else if (job.workGiverDef?.workType != null)
                        {
                            tags.Add("working");
                            tags.Add(DefTag("work:", job.workGiverDef.workType));
                        }
                    }
                    if (p.Spawned && p.Map != null)
                    {
                        var room = p.GetRoom();
                        if (room == null || room.PsychologicallyOutdoors) tags.Add("outside");
                        else
                        {
                            tags.Add("indoors");
                            if (RoomUtil.PrivateRoom(p) == room) tags.Add("inownroom");
                        }
                    }

                    var standing = CouncilGameComponent.Instance?.standings?.For(p, false);
                    if (standing != null)
                    {
                        var level = standing.Level;
                        if (level >= TrustLevel.Trusting) tags.Add("trusting");
                        else if (level == TrustLevel.Doubtful) tags.Add("doubtful");
                        else if (level == TrustLevel.Bitter) tags.Add("bitter");
                    }
                }

                if (w != null)
                {
                    if (w.promised) tags.Add("promised");
                    if (w.coSigners.Count > 0) tags.Add("petition");
                    if (w.ForSomeoneElse) tags.Add("forother");
                    if (w.repeat) tags.Add("repeat");
                    if (!w.trigger.NullOrEmpty()) tags.Add("trigger:" + w.trigger);
                }
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("flavour tags", e);
            }
            return tags;
        }

        public static readonly string[] GriefThoughts =
        {
            "KnowColonistDied", "PawnWithGoodOpinionDied", "WitnessedDeathFamily", "BondedAnimalDied",
            "MySonDied", "MyDaughterDied", "MyHusbandDied", "MyWifeDied", "MyFianceDied", "MyFianceeDied",
            "MyLoverDied", "MyBrotherDied", "MySisterDied", "MyFatherDied", "MyMotherDied", "MyFriendDied"
        };

        private static readonly HashSet<string> griefSet = new HashSet<string>(GriefThoughts);

        /// <summary>A grief memory (one of <see cref="GriefThoughts"/>), by a set rather than a search of the list.</summary>
        public static bool IsGriefThought(string defName) => defName != null && griefSet.Contains(defName);

        // Tags made of a def's name ("trait:Kind", "skill:Cooking"), each built once instead of on every line picked.
        private static readonly Dictionary<(string, Def), string> defTags = new Dictionary<(string, Def), string>();
        private static readonly Dictionary<TraitDegreeData, string> degreeTags = new Dictionary<TraitDegreeData, string>();
        private static int tagsGeneration = -1;

        /// <summary>prefix + the def's name, the same string each time.</summary>
        public static string DefTag(string prefix, Def def)
        {
            if (def == null) return prefix;
            if (tagsGeneration != DefRefs.Generation)
            {
                tagsGeneration = DefRefs.Generation;
                defTags.Clear();
                degreeTags.Clear();
            }
            var key = (prefix, def);
            if (!defTags.TryGetValue(key, out var tag))
            {
                tag = prefix + def.defName;
                if (defTags.Count < 20000) defTags[key] = tag;
            }
            return tag;
        }

        /// <summary>"trait:" + the degree's name (<see cref="DegreeTag"/>), or null; built once per degree.</summary>
        internal static string DegreeTraitTag(TraitDegreeData d)
        {
            if (d == null) return null;
            if (!degreeTags.TryGetValue(d, out var tag))
            {
                string name = DegreeTag(d);
                tag = name != null ? "trait:" + name : null;
                if (degreeTags.Count < 20000) degreeTags[d] = tag;
            }
            return tag;
        }

        /// <summary>"hard worker" -> "HardWorker", "iron-willed" -> "IronWilled", from the untranslated label so tags work in any language.</summary>
        internal static string DegreeTag(TraitDegreeData d)
        {
            string label = d?.untranslatedLabel.NullOrEmpty() == false ? d.untranslatedLabel : d?.label;
            if (label.NullOrEmpty()) return null;
            var sb = new System.Text.StringBuilder();
            bool upper = true;
            foreach (char c in label)
            {
                if (!char.IsLetter(c))
                {
                    upper = true;
                    continue;
                }
                sb.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        public static bool IsGrieving(Pawn p)
        {
            var mem = p?.needs?.mood?.thoughts?.memories;
            if (mem == null) return false;
            var list = mem.Memories;
            for (int i = 0; i < list.Count; i++)
                if (IsGriefThought(list[i].def.defName)) return true;
            return false;
        }

        /// <summary>
        /// Splits "[a,b] text" into its tags and the text. Each line is taken apart once (<see cref="LineTags"/>): the
        /// tags array is shared, so callers only read it.
        /// </summary>
        public static string Strip(string line, out string[] tags)
        {
            var p = LineTags.Of(line);
            tags = p?.tags;
            return p?.text;
        }

        public static string Strip(string line) => Strip(line, out _);

        /// <summary>
        /// -1 when the line doesn't fit the moment, else how many of its tags speak to the moment (0 for untagged lines).
        /// Tags that hold for most of a game (the era, the colony's wealth and size, see <see cref="StandingTags"/>) only
        /// decide whether a line fits: they don't count, so a [lowtech] line doesn't take most of a tribal colony's picks
        /// for years the way a [winter] line takes the picks of a winter.
        /// </summary>
        public static int Match(string line, HashSet<string> context) => LineTags.Match(LineTags.Of(line), context);

        /// <summary>
        /// Tags that hold for most of a game rather than a moment: the era, the colony's wealth and its size (and the
        /// land, land:desert, land:coast). See <see cref="LineTags.Parse"/>.
        /// </summary>
        public static readonly HashSet<string> StandingTags = LineTags.Standing;

        /// <summary>The lines each pool gave out lately (see <see cref="LineMemory"/>).</summary>
        public static readonly LineMemory Memory = new LineMemory();

        /// <summary>
        /// Words that don't belong in a colony that has never seen gunpowder or electricity. Words that are only
        /// sometimes high-tech stay out of this list (a passive cooler or a fueled heater is fine in a medieval
        /// colony, an electric one isn't): lines that mean the electric kind are tagged <c>[!lowtech]</c> instead.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex HighTech = new System.Text.RegularExpressions.Regex(
            @"\b(guns?|gunfire|gunshots?|rifles?|pistols?|shotguns?|revolvers?|turrets?|grenades?|bullets?|ammo|sandbags?|electric\w*|batter(y|ies)|power (cell|grid|line)s?|computers?|comms|radios?|cryptosleep|spaceships?|starships?|shuttles?|lasers?|plasma|freezers?|solar"
            + @"|hydroponics?|sun ?lamps?|hospital beds?|vitals monitors?|hi-?tech|high-tech|multi-?analy[sz]ers?|components?|glitterworlds?|urbworlds?|sterile (tiles?|floors?))\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);


        /// <summary>The colony's era as tags: tribal, medieval, industrial or spacer, plus lowtech before electricity.</summary>
        public static void AddEraTags(HashSet<string> tags)
        {
            var tech = Avail.EffectiveTech;
            if (tech <= TechLevel.Neolithic) tags.Add("tribal");
            else if (tech == TechLevel.Medieval) tags.Add("medieval");
            else if (tech == TechLevel.Industrial) tags.Add("industrial");
            else if (tech >= TechLevel.Spacer) tags.Add("spacer");
            if (Avail.IsPreIndustrial) tags.Add("lowtech");
        }

        /// <summary>
        /// False when the line is tagged for another era (<c>[lowtech]</c>, <c>[!lowtech]</c>, <c>[medieval|tribal]</c>...).
        /// Other tags (the weather, the speaker) are left to <see cref="Match"/>.
        /// </summary>
        public static bool EraTagsFit(string line) => EraFits(LineTags.Of(line));

        /// <summary>The colony's era tags, filled afresh for each line that has era tags (the era can change with research).</summary>
        private static readonly HashSet<string> eraNow = new HashSet<string>();

        private static bool EraFits(LineTags.Parsed p)
        {
            if (p?.era == null) return true;
            eraNow.Clear();
            AddEraTags(eraNow);
            return LineTags.EraFits(p, eraNow);
        }

        /// <summary>
        /// One of these tips, the same one for as long as <paramref name="seed"/> stays the same. Never one tagged
        /// for another era, and none about guns or power in a pre-industrial colony unless that's all there is.
        /// Tags are removed. Null when nothing fits.
        /// </summary>
        public static string PickTip(List<string> tips, int seed)
        {
            if (tips.NullOrEmpty()) return null;
            var pool = tips.Where(t => !t.NullOrEmpty() && EraTagsFit(t)).ToList();
            var fitting = pool.Where(TechFits).ToList();
            if (fitting.Count == 0) fitting = pool;
            if (fitting.Count == 0) return null;
            return Strip(fitting[(seed % fitting.Count + fitting.Count) % fitting.Count]);
        }

        /// <summary>Thunderstorms, dust storms, petal storms: any weather with "storm" or "thunder" in its name.</summary>
        public static bool IsStorm(WeatherDef w)
        {
            if (w == null) return false;
            string n = w.defName.ToLowerInvariant();
            return n.Contains("thunder") || n.Contains("storm");
        }

        /// <summary>
        /// Age in human years, for races that live much longer or shorter than people (alien race mods):
        /// a 300-year-old elf with a lifespan of 1000 counts as about 36. Growing up keeps the race's own pace (a
        /// long-lived gene doesn't make a grown dwarf of 25 a teenager); the adult years are stretched to fit.
        /// </summary>
        public static float HumanAge(Pawn p)
        {
            if (p?.ageTracker == null) return 0f;
            float years = p.ageTracker.AgeBiologicalYearsFloat;
            float life = p.RaceProps?.lifeExpectancy ?? 80f;
            // Long-lived genes (Stoneborn, elves and the like) stretch a lifetime without changing the race.
            if (StatDefOf.LifespanFactor != null && p.RaceProps?.Humanlike == true)
            {
                try
                {
                    float factor = p.GetStatValue(StatDefOf.LifespanFactor, true, 2500);
                    if (factor > 0.1f) life *= factor;
                }
                catch
                {
                }
            }
            if (life <= 1f || System.Math.Abs(life - 80f) <= 1f) return years;
            // The race's adult life stage (18 for people), mapped to 18; the rest of a lifetime to 18..80.
            var stages = p.RaceProps?.lifeStageAges;
            float adult = stages != null && stages.Count > 0 ? stages[stages.Count - 1].minAge : 18f;
            if (adult <= 0f || adult >= life - 1f) return years * 80f / life;
            return years <= adult ? years * 18f / adult : 18f + (years - adult) * 62f / (life - adult);
        }

        /// <summary>
        /// False for a line tagged for another era, and for a line about guns, turrets, electricity, computers or
        /// spaceships while the colony is pre-industrial (medieval or tribal, no electricity): those lines are
        /// skipped whenever another one fits.
        /// </summary>
        public static bool TechFits(string line)
        {
            if (line.NullOrEmpty()) return true;
            var p = LineTags.Of(line);
            if (!EraFits(p)) return false;
            if (!Avail.IsPreIndustrial) return true;
            // The text never changes: whether it talks of guns or power is worked out once.
            if (p.highTech < 0) p.highTech = (sbyte)(HighTech.IsMatch(p.text) ? 1 : 0);
            return p.highTech == 0;
        }

        /// <summary>
        /// One of these texts, preferring ones that fit the colony's era (never one tagged for another), and not one this
        /// list gave out lately while another fits. Tags are removed.
        /// </summary>
        public static string PickText(List<string> texts)
        {
            if (texts.NullOrEmpty()) return null;
            var pool = texts.Where(EraTagsFit).ToList();
            if (pool.Count == 0) pool = texts;
            var fitting = pool.Where(TechFits).ToList();
            var from = fitting.Count > 0 ? fitting : pool;
            var recent = Memory.Recent(texts);
            if (recent != null)
            {
                var fresh = from.Where(t => !recent.Contains(Strip(t))).ToList();
                if (fresh.Count > 0) from = fresh;
            }
            string text = Strip(from.RandomElement());
            Memory.Remember(texts, text, texts.Count);
            return text;
        }

        /// <summary>
        /// Picks a line that fits the moment. Lines matching more tags are much more likely, so a colonist
        /// shivering in winter talks about the cold, while untagged lines keep things varied. Tags are removed.
        /// </summary>
        public static string Pick(IEnumerable<string> lines, HashSet<string> context) => Pick(lines, context, null);

        /// <summary>Like <see cref="Pick(IEnumerable{string}, HashSet{string})"/>, never one of the <paramref name="avoid"/> lines (as shown, tags removed).</summary>
        public static string Pick(IEnumerable<string> lines, HashSet<string> context, HashSet<string> avoid)
        {
            if (lines == null) return null;
            var fitting = new List<(string line, float weight)>();
            foreach (var l in lines)
            {
                if (l.NullOrEmpty() || !TechFits(l)) continue;
                int m = Match(l, context);
                if (m < 0) continue;
                if (avoid != null && avoid.Contains(Strip(l))) continue;
                fitting.Add((l, m == 0 ? 1f : 2.5f + m * 1.5f));
            }
            if (fitting.Count == 0) return null;
            return Strip(fitting.RandomElementByWeight(x => x.weight).line);
        }

        /// <summary>
        /// Like <see cref="Pick(IEnumerable{string}, HashSet{string})"/>, and not a line this pool gave out lately while
        /// another one fits. <paramref name="memoryKey"/> stands for the pool: anything that stays the same, the list itself.
        /// </summary>
        public static string PickFresh(IList<string> lines, HashSet<string> context, object memoryKey)
        {
            if (lines == null) return null;
            var recent = Memory.Recent(memoryKey);
            string line = (recent != null ? Pick(lines, context, recent) : null) ?? Pick(lines, context, null);
            if (line != null) Memory.Remember(memoryKey, line, lines.Count);
            return line;
        }

        /// <summary>Like <see cref="Pick(IEnumerable{string}, HashSet{string})"/>, but only lines with at least one matching tag. Null if none.</summary>
        public static string PickTagged(IEnumerable<string> lines, HashSet<string> context) => PickTagged(lines, context, null);

        public static string PickTagged(IEnumerable<string> lines, HashSet<string> context, HashSet<string> avoid)
        {
            if (lines == null || context == null) return null;
            var fitting = new List<(string line, int m)>();
            foreach (var l in lines)
            {
                if (l.NullOrEmpty() || !TechFits(l)) continue;
                int m = Match(l, context);
                if (m <= 0) continue;
                if (avoid != null && avoid.Contains(Strip(l))) continue;
                fitting.Add((l, m));
            }
            if (fitting.Count == 0) return null;
            return Strip(fitting.RandomElementByWeight(x => x.m).line);
        }

        public static bool AnyFits(IEnumerable<string> lines, HashSet<string> context) => lines != null && lines.Any(l => !l.NullOrEmpty() && Match(l, context) >= 0);

        /// <summary>A line from a <see cref="FlavorPoolDef"/>, or null if the pool doesn't exist or nothing fits.</summary>
        public static string FromPool(string poolDefName, Pawn p, Wish w = null, Tone? tone = null, params string[] extraTags)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(poolDefName);
            if (pool == null) return null;
            var tags = TagsFor(p, w);
            if (extraTags != null) foreach (var t in extraTags) if (!t.NullOrEmpty()) tags.Add(t);
            return pool.lines.Pick(tone ?? ToneUtility.ToneFor(p), tags);
        }
    }

    /// <summary>A named set of tagged, toned lines used for scenes, reminders, gossip and the like.</summary>
    public class FlavorPoolDef : Def
    {
        public ToneLines lines = new ToneLines();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (lines == null || !lines.Any) yield return "empty flavour pool";
        }
    }
}
