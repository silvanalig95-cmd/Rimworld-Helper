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
    /// <item>weather: rain, snow, fog, storm, clear, hot, cold</item>
    /// <item>mood: lowmood, highmood; trust: trusting, doubtful, bitter</item>
    /// <item>tech: tribal, medieval, industrial, spacer; wealth: poor, rich; colony: small, large</item>
    /// <item>the speaker: child, teen, old, hurt, trait:DefName, skill:DefName (passionate about it)</item>
    /// <item>events: afterraid, grieving, newcomer, repeat (asked before), promised, petition, forother</item>
    /// </list>
    /// Prefix a tag with ! to negate it, separate alternatives with |, e.g. <c>[night|evening,!child]</c>.
    /// </summary>
    public static class Flavor
    {
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
                    int hour = GenLocalDate.HourOfDay(map);
                    tags.Add(hour < 6 || hour >= 22 ? "night" : hour < 11 ? "morning" : hour < 18 ? "day" : "evening");
                    var weather = map.weatherManager?.curWeather;
                    if (weather != null)
                    {
                        if (weather.snowRate > 0.01f) tags.Add("snow");
                        else if (weather.rainRate > 0.01f) tags.Add("rain");
                        if (weather.defName.Contains("Fog")) tags.Add("fog");
                        if (weather.defName.Contains("Thunder") || weather.defName.Contains("Storm")) tags.Add("storm");
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

                var tech = Avail.EffectiveTech;
                if (tech <= TechLevel.Neolithic) tags.Add("tribal");
                else if (tech == TechLevel.Medieval) tags.Add("medieval");
                else if (tech == TechLevel.Industrial) tags.Add("industrial");
                else if (tech >= TechLevel.Spacer) tags.Add("spacer");
                if (Avail.IsPreIndustrial) tags.Add("lowtech");

                if (p != null)
                {
                    float mood = p.needs?.mood?.CurLevelPercentage ?? 0.5f;
                    if (mood < 0.3f) tags.Add("lowmood");
                    else if (mood > 0.7f) tags.Add("highmood");
                    if (p.DevelopmentalStage.Child()) tags.Add("child");
                    else if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears < 20) tags.Add("teen");
                    else if (p.ageTracker != null && p.ageTracker.AgeBiologicalYears >= 60) tags.Add("old");
                    if (p.health?.hediffSet != null && p.health.summaryHealth.SummaryHealthPercent < 0.85f) tags.Add("hurt");
                    if (p.story?.traits != null)
                    {
                        foreach (var t in p.story.traits.allTraits) tags.Add("trait:" + t.def.defName);
                    }
                    if (p.skills != null)
                    {
                        foreach (var s in p.skills.skills)
                        {
                            if (s.passion != Passion.None && !s.TotallyDisabled) tags.Add("skill:" + s.def.defName);
                        }
                    }
                    if (p.records != null && p.records.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal) < GenDate.TicksPerDay * 10) tags.Add("newcomer");
                    if (IsGrieving(p)) tags.Add("grieving");

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

        public static bool IsGrieving(Pawn p)
        {
            var mem = p?.needs?.mood?.thoughts?.memories;
            if (mem == null) return false;
            foreach (var m in mem.Memories)
            {
                if (GriefThoughts.Contains(m.def.defName)) return true;
            }
            return false;
        }

        /// <summary>Splits "[a,b] text" into its tags and the text.</summary>
        public static string Strip(string line, out string[] tags)
        {
            tags = null;
            if (line == null) return null;
            string trimmed = line.TrimStart();
            if (!trimmed.StartsWith("[")) return line;
            int end = trimmed.IndexOf(']');
            if (end < 0) return line;
            tags = trimmed.Substring(1, end - 1).Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();
            return trimmed.Substring(end + 1).TrimStart();
        }

        public static string Strip(string line) => Strip(line, out _);

        /// <summary>-1 when the line doesn't fit the moment, else how many tags it matched (0 for untagged lines).</summary>
        public static int Match(string line, HashSet<string> context)
        {
            Strip(line, out var tags);
            if (tags == null || tags.Length == 0) return 0;
            int matched = 0;
            foreach (var tag in tags)
            {
                bool ok = false;
                foreach (var alt in tag.Split('|'))
                {
                    string a = alt.Trim();
                    bool neg = a.StartsWith("!");
                    if (neg) a = a.Substring(1);
                    bool has = context != null && context.Contains(a);
                    if (has != neg)
                    {
                        ok = true;
                        break;
                    }
                }
                if (!ok) return -1;
                matched++;
            }
            return matched;
        }

        /// <summary>
        /// Picks a line that fits the moment. Lines matching more tags are much more likely, so a colonist
        /// shivering in winter talks about the cold, while untagged lines keep things varied. Tags are removed.
        /// </summary>
        public static string Pick(IEnumerable<string> lines, HashSet<string> context)
        {
            if (lines == null) return null;
            var fitting = new List<(string line, float weight)>();
            foreach (var l in lines)
            {
                if (l.NullOrEmpty()) continue;
                int m = Match(l, context);
                if (m < 0) continue;
                fitting.Add((l, m == 0 ? 1f : 2.5f + m * 1.5f));
            }
            if (fitting.Count == 0) return null;
            return Strip(fitting.RandomElementByWeight(x => x.weight).line);
        }

        public static bool AnyFits(IEnumerable<string> lines, HashSet<string> context) => lines != null && lines.Any(l => !l.NullOrEmpty() && Match(l, context) >= 0);

        /// <summary>A line from a <see cref="FlavorPoolDef"/>, or null if the pool doesn't exist or nothing fits.</summary>
        public static string FromPool(string poolDefName, Pawn p, Wish w = null, Tone? tone = null)
        {
            var pool = DefDatabase<FlavorPoolDef>.GetNamedSilentFail(poolDefName);
            if (pool == null) return null;
            var tags = TagsFor(p, w);
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
