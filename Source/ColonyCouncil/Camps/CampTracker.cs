using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ColonyCouncil
{
    /// <summary>Everything a camp needs before it arrives: who, from where, to where, and what they'll build.</summary>
    public class CampSetup
    {
        public CampKindDef kind;
        public Faction faction;
        public Map map;
        public IntVec3 center, stash, share, colony;
        public List<CampPlan> plans;
        public List<Pawn> pawns;
        public string place;
        public int stayTicks;
        /// <summary>Its people are on the map already (a war party making camp after the fight): not spawned again.</summary>
        public bool onMap;
        /// <summary>The remembered camp this is the return of (CampMemory.cs).</summary>
        public PastCamp past;
        /// <summary>The weather of their stay (CampWeather), and what they put on and how they pitch for it.</summary>
        public float low, high;
        public CampPitch pitch;
        /// <summary>What brought them, a reason of the land's (World/Region/TravelMath.cs), or null; and the land as it was read.</summary>
        public string seek;
        public LandReasons land;
        /// <summary>The old camp's site they move into (CampGround.cs), or null for a fresh spot.</summary>
        public CampGround ground;
    }

    /// <summary>
    /// What camps one colony has (stage 0, step 7b): the camp visiting its land, the neighbours settled there for good,
    /// a war party come to help it, and the clock of camps coming (each home's own day to try, its gap since the last
    /// camp, its diary). One per colony, kept in the camps' book (<see cref="CampTracker.homes"/>) by the council's rules
    /// (<see cref="ColonyBook"/>): an old save's single camp and clock are the seat's.
    /// </summary>
    public class CampHome : IExposable, IColonyEntry
    {
        /// <summary>The colony's key (<see cref="Homes.KeyOf"/>), or none while it has no home (an old save's, not yet placed).</summary>
        public int home = HomesMath.None;
        /// <summary>When its home stopped being ours; -1 while it is.</summary>
        public int lostAt = ColonyStateMath.NotLost;

        public int Home
        {
            get => home;
            set => home = value;
        }

        public int LostAt
        {
            get => lostAt;
            set => lostAt = value;
        }

        /// <summary>
        /// The camps visiting the colony's land: one at a time at the usual level, up to four side by side on a big map at
        /// the setting's highest (CampCrowds.cs). An older save's one camp, saved as "camp", is read into it.
        /// </summary>
        public List<Camp> camps = new List<Camp>();
        /// <summary>Neighbours settled on the land for good (Holding.cs): a camp kept in a slot of its own, so camps still come and go beside it.</summary>
        public Camp holding;
        /// <summary>Bands come against a camp here, while they last (CampRaid.cs).</summary>
        public List<CampRaid> raids = new List<CampRaid>();
        /// <summary>Old camps' sites, their constructions still standing, and who held them (CampGround.cs), the newest CampGroundMath.MostSites.</summary>
        public List<CampGround> grounds = new List<CampGround>();
        public int lastEndedTick = -9999999;
        /// <summary>Days since the last camp on which one could have come (the way was open and the dice were rolled), for the World page.</summary>
        public int openMornings;
        /// <summary>
        /// The camps' diary, for the World page and for reviewing a save: each day's outcome as "day|hour|code|detail",
        /// newest last, at most <see cref="CampTracker.DiaryMax"/>; codes are the blockers (<see cref="CampTracker.Blocker"/>)
        /// and waiting (a world event's question unanswered), lettergap (a question came in the last hours), open (the way
        /// was open, the dice said not today), checks (the event's own checks said no), failed (it could not be set up,
        /// with why), came.
        /// </summary>
        public List<string> diary = new List<string>();
        /// <summary>How often each outcome came, since <see cref="diarySince"/> (a day).</summary>
        public Dictionary<string, int> diaryCounts = new Dictionary<string, int>();
        public int diarySince = -1;
        /// <summary>When the day's try comes (CampTimer.NextTry), and how many hours a passing reason has put it off.</summary>
        public int nextTryTick = -1, tryHours;
        /// <summary>A war party on the map, come to fight the colony's attacker (WarCamp.cs), and the threat it was rolled for.</summary>
        public WarBand warBand;
        public int threatSince = -1;
        public bool threatRolled;

        /// <summary>The colony's home while it's ours; null while it has none.</summary>
        public Map Map => home == HomesMath.None ? null : Homes.MapOf(home);

        /// <summary>One day's outcome into the diary.</summary>
        public void Record(int day, int hour, string code, string detail)
        {
            if (code.NullOrEmpty()) return;
            if (diarySince < 0) diarySince = day;
            diary.Add(day + "|" + hour + "|" + code + "|" + (detail ?? "").Replace("|", "/"));
            while (diary.Count > CampTracker.DiaryMax) diary.RemoveAt(0);
            diaryCounts.TryGetValue(code, out int n);
            diaryCounts[code] = n + 1;
        }

        /// <summary>The diary in the player's words, one line: days open, camps come, and what kept them away most.</summary>
        public string DiaryLine()
        {
            if (diaryCounts.Count == 0 || diarySince < 0) return null;
            diaryCounts.TryGetValue("open", out int open);
            diaryCounts.TryGetValue("came", out int came);
            var away = diaryCounts.Where(kv => kv.Key != "open" && kv.Key != "came" && kv.Value > 0).OrderByDescending(kv => kv.Value)
                .Select(kv => ("CC_Camp_Diary_" + kv.Key).Translate().ToString() + " (" + kv.Value + ")").ToList();
            string line = "CC_Camp_Diary_Line".Translate((diarySince + 1).Named("DAY"), (open + came).Named("OPEN"), came.Named("CAME")).Resolve();
            if (away.Count > 0) line += " " + "CC_Camp_Diary_Away".Translate(string.Join(", ", away)).Resolve();
            return line;
        }

        /// <summary>Saved under the names the camps' tracker always used, so an old save's are read straight into the seat's.</summary>
        public void ExposeData()
        {
            Scribe_Values.Look(ref home, "home", HomesMath.None);
            Scribe_Values.Look(ref lostAt, "lostAt", ColonyStateMath.NotLost);
            if (Scribe.mode == LoadSaveMode.Saving) camps?.RemoveAll(c => c == null);
            Scribe_Collections.Look(ref camps, "camps", LookMode.Deep);
            // A save from before camps side by side (0.168.0 and older): its one visiting camp, read into the list.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Camp old = null;
                Scribe_Deep.Look(ref old, "camp");
                if (old != null)
                {
                    camps ??= new List<Camp>();
                    camps.Add(old);
                }
            }
            Scribe_Deep.Look(ref holding, "holding");
            Scribe_Values.Look(ref lastEndedTick, "lastEndedTick", -9999999);
            Scribe_Values.Look(ref openMornings, "openMornings", 0);
            Scribe_Collections.Look(ref diary, "diary", LookMode.Value);
            Scribe_Collections.Look(ref diaryCounts, "diaryCounts", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref diarySince, "diarySince", -1);
            Scribe_Values.Look(ref nextTryTick, "nextTryTick", -1);
            Scribe_Values.Look(ref tryHours, "tryHours");
            Scribe_Deep.Look(ref warBand, "warBand");
            Scribe_Values.Look(ref threatSince, "threatSince", -1);
            Scribe_Values.Look(ref threatRolled, "threatRolled");
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                raids?.RemoveAll(r => r == null || r.Over);
                grounds?.RemoveAll(g => g == null);
            }
            Scribe_Collections.Look(ref raids, "campRaids", LookMode.Deep);
            Scribe_Collections.Look(ref grounds, "grounds", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (diary == null) diary = new List<string>();
                if (diaryCounts == null) diaryCounts = new Dictionary<string, int>();
                camps ??= new List<Camp>();
                raids ??= new List<CampRaid>();
                grounds ??= new List<CampGround>();
                // A camp whose kind was removed with its mod, or whose map is gone: nothing left to keep.
                camps.RemoveAll(c => c == null || c.kind == null || c.map == null);
                raids.RemoveAll(r => r == null || r.map == null || r.people == null);
                grounds.RemoveAll(g => g == null || !g.center.IsValid);
                if (holding != null && (holding.kind == null || holding.map == null)) holding = null;
                if (warBand != null && (warBand.map == null || warBand.faction == null)) warBand = null;
            }
        }
    }

    /// <summary>
    /// The camps on the colonies' land, one visiting camp, one holding and one war party per colony (each colony's
    /// <see cref="CampHome"/>, kept in the save), and how a new one is made. Camps live on their own clock: every few
    /// seconds for hunts and the like, every hour for everything else.
    /// </summary>
    public class CampTracker : GameComponent
    {
        /// <summary>One per colony: its camps and its clock of camps coming. An old save's are the seat's.</summary>
        public List<CampHome> homes = new List<CampHome>();
        /// <summary>While an old save loads: its one camp and clock, read from where they always were, for the seat.</summary>
        [Unsaved] private CampHome oldSave;
        public int nextId = 1;
        /// <summary>Why the last Prepare without a list of its own found nothing (for the diary).</summary>
        [Unsaved] public readonly List<string> lastWhy = new List<string>();
        public const int DiaryMax = 30;

        /// <summary>Camps the colony remembers, which may come back (CampMemory.cs).</summary>
        public List<PastCamp> past = new List<PastCamp>();

        /// <summary>A home's camps (the seat's for a map that isn't a home), made when it first needs them.</summary>
        public CampHome HomeOf(Map map)
        {
            homes ??= new List<CampHome>();
            return ColonyBook.Entry(homes, map, () => new CampHome());
        }

        /// <summary>The camps' state of the colony whose camp, holding or war party this is (by its map).</summary>
        public CampHome HomeWith(Camp c) => c?.map == null ? null : homes?.FirstOrDefault(h => h != null && (h.camps.Contains(c) || h.holding == c)) ?? HomeOf(c.map);

        /// <summary>The camps visiting this home, the first come first (none for a map that isn't one).</summary>
        public static List<Camp> VisitorsAt(Map map)
        {
            var t = Instance;
            if (t == null || map == null || !Homes.IsHome(map)) return new List<Camp>();
            return t.HomeOf(map).camps.Where(c => c != null && c.map == map).ToList();
        }

        /// <summary>
        /// A camp visiting this home (null for a map that isn't one, or with none there): the one being dealt with when it is
        /// one of them, else the first come; with <paramref name="fits"/>, the first that fits it (a camp settled and at
        /// peace, a kind of camp). With camps side by side, what asks for "the camp" names the one it means; this is for
        /// what can be about any of them.
        /// </summary>
        public static Camp VisitingAt(Map map, Func<Camp, bool> fits = null)
        {
            var here = VisitorsAt(map);
            if (here.Count == 0) return null;
            var asked = Asked;
            if (asked != null && here.Contains(asked) && (fits == null || fits(asked))) return asked;
            return fits == null ? here[0] : here.FirstOrDefault(fits);
        }

        /// <summary>
        /// The camp a question or its answer is about: the one being dealt with (a letter's answer, a button on its page),
        /// else the one its stranger belongs to, else one visiting its map that fits.
        /// </summary>
        public static Camp About(DilemmaContext ctx, Func<Camp, bool> fits = null)
        {
            var asked = Asked;
            if (asked != null && (ctx?.map == null || asked.map == ctx.map) && (fits == null || fits(asked))) return asked;
            var own = ctx?.stranger != null ? CampOf(ctx.stranger) : null;
            if (own != null && (fits == null || fits(own))) return own;
            return VisitingAt(ctx?.map, fits);
        }

        /// <summary>Another camp could come to this home's map now by their number (CampCrowds: the level's count by the map's room).</summary>
        public static bool RoomFor(Map map) => Enabled && CampCrowds.Room(map);

        /// <summary>The neighbours settled on this home's land (null for a map that isn't one, or with none there).</summary>
        public static Camp HoldingAt(Map map)
        {
            var t = Instance;
            if (t == null || map == null || !Homes.IsHome(map)) return null;
            var c = t.HomeOf(map).holding;
            return c != null && c.map == map ? c : null;
        }

        /// <summary>The war party at this home (null for a map that isn't one, or with none there).</summary>
        public static WarBand WarBandAt(Map map)
        {
            var t = Instance;
            if (t == null || map == null || !Homes.IsHome(map)) return null;
            return t.HomeOf(map).warBand;
        }

        /// <summary>Each colony's visiting camps, in the order the homes were founded.</summary>
        public IEnumerable<Camp> Visiting
        {
            get
            {
                if (homes == null) yield break;
                foreach (var h in homes)
                    if (h?.camps != null)
                        foreach (var c in h.camps)
                            if (c != null) yield return c;
            }
        }

        /// <summary>
        /// Homes lost (Homes.Changed): each one's camps and clock are kept a while, as the council's; the camps by a home no
        /// longer ours are none of the colony's now, and are let be.
        /// </summary>
        internal void HomesLost(IReadOnlyList<int> lost)
        {
            ColonyBook.Lost(homes, lost, h =>
            {
                h.camps.Clear();
                h.holding = null;
                h.warBand = null;
                h.raids.Clear();
                h.grounds.Clear();
            });
        }

        /// <summary>Once a day: the camps' clocks of homes lost long enough ago are let go; old camps' sites claimed or gone are let go (CampGround.cs).</summary>
        internal void Daily()
        {
            ColonyBook.Daily(homes, null);
            foreach (var h in homes.ToList()) CampGrounds.Daily(h);
        }

        /// <summary>A war party is gone from the colony it came to help.</summary>
        public void Forget(WarBand b)
        {
            if (b == null || homes == null) return;
            foreach (var h in homes)
                if (h?.warBand == b) h.warBand = null;
        }

        /// <summary>Each colony's war party.</summary>
        public IEnumerable<WarBand> WarBands
        {
            get
            {
                if (homes == null) yield break;
                foreach (var h in homes)
                    if (h?.warBand != null) yield return h.warBand;
            }
        }

        public CampTracker(Game game)
        {
        }

        public static CampTracker Instance => Current.Game?.GetComponent<CampTracker>();

        private static CouncilSettings S => ColonyCouncilMod.Settings;

        private static int Now => Find.TickManager.TicksGame;

        /// <summary>The camps on the land: at each colony, those visiting (the first come first), then the neighbours settled for good.</summary>
        public IEnumerable<Camp> All
        {
            get
            {
                if (homes == null) yield break;
                foreach (var h in homes)
                {
                    if (h == null) continue;
                    if (h.camps != null)
                        foreach (var c in h.camps)
                            if (c != null) yield return c;
                    if (h.holding != null) yield return h.holding;
                }
            }
        }

        public Camp ById(int id) => All.FirstOrDefault(c => c.id == id);

        /// <summary>The camp whose fire this is, on this map (each colony may have a camp of its own).</summary>
        public Camp CampAt(IntVec3 fire, Map map) => All.FirstOrDefault(c => c.FireCell == fire && (map == null || c.map == map));

        /// <summary>The camp this pawn belongs to (living with the group on the map), or null.</summary>
        public static Camp CampOf(Pawn p)
        {
            var t = Instance;
            if (t == null || p == null) return null;
            var lord = p.GetLord();
            if (lord == null) return null;
            foreach (var c in t.All)
                if (c.lord == lord) return c;
            return null;
        }

        /// <summary>
        /// The camp a play-out "down to the camp" means (PlaySpot.Camp): the one <paramref name="other"/> (the question's
        /// stranger) belongs to, else the visitors, else the neighbours settled for good (Holding.cs); living on this map, at peace.
        /// </summary>
        public static Camp PlayCamp(Map map, Pawn other = null)
        {
            var t = Instance;
            if (t == null || map == null) return null;
            bool Fits(Camp c) => c != null && c.map == map && c.phase == CampPhase.Living && !c.Hostile;
            var own = CampOf(other);
            if (Fits(own)) return own;
            var visiting = VisitingAt(map, Fits);
            var holding = HoldingAt(map);
            return Fits(visiting) ? visiting : Fits(holding) ? holding : null;
        }

        /// <summary>The camp an answer or an ask is about while it's being dealt with (a letter's answer, a button on its page), for the effects that need to know.</summary>
        public static Camp Asked { get; private set; }

        /// <summary>Marks <paramref name="c"/> as the camp dealt with until disposed.</summary>
        public static IDisposable Talking(Camp c) => new TalkingScope(c);

        private sealed class TalkingScope : IDisposable
        {
            private readonly Camp before;

            public TalkingScope(Camp c)
            {
                before = Asked;
                Asked = c;
            }

            public void Dispose() => Asked = before;
        }

        /// <summary>The camp becomes the colony's neighbours for good: out of the visiting slot (a new camp may come) and into its own.</summary>
        public void MakeHolding(Camp c)
        {
            if (c == null) return;
            var h = HomeWith(c);
            if (h == null) return;
            if (h.camps.Remove(c)) h.lastEndedTick = Now;
            h.holding = c;
        }

        public static bool Enabled => S != null && S.worldEvents && S.camps;

        /// <summary>A camp still in one of the colonies' slots (visiting or holding).</summary>
        private bool Kept(Camp c) => homes != null && homes.Any(h => h != null && (h.camps.Contains(c) || h.holding == c));

        public override void GameComponentTick()
        {
            if (homes == null || !homes.Any(h => h != null && (h.camps.Count > 0 || h.holding != null || h.raids.Count > 0))) return;
            int now = Now;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareCamp)) return;
            foreach (var c in All.ToList())
            {
                try
                {
                    // Timed for Rimfolk's clock: the quarter hour, then the camp's hour (the hour's other part is CampTimer's).
                    using (PassClock.Time(Pass.RareCamp)) c.Tick250();
                    if (Sched.Hourly(now, Sched.Camp) && Kept(c))
                        using (PassClock.Time(Pass.Camp))
                            c.Hourly();
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp", e);
                }
            }
            // Bands come against a camp (CampRaid.cs): watched on the camps' quarter hour.
            foreach (var h in homes.ToList())
            {
                if (h?.raids == null || h.raids.Count == 0) continue;
                try
                {
                    using (PassClock.Time(Pass.RareCamp)) CampRaids.Watch(h, now);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("a band against a camp", e);
                }
            }
        }

        public void Ended(Camp c)
        {
            if (c == null || homes == null) return;
            foreach (var h in homes)
            {
                if (h == null) continue;
                if (h.holding == c)
                {
                    h.holding = null;
                    return;
                }
                if (!h.camps.Remove(c)) continue;
                h.lastEndedTick = Now;
                return;
            }
        }

        // ---------------- A new camp ----------------

        /// <summary>A camp could come now: switched on, none here, none just gone, room on the map, the right weather and people.</summary>
        public bool CanStartOn(Map map) => WhyNot(map) == null;

        /// <summary>What keeps a camp from coming now, in plain words (for "Rimfolk: check myself"), or null if one could.</summary>
        public string WhyNot(Map map)
        {
            var (code, a, b) = Blocker(map);
            switch (code)
            {
                case null: return null;
                case "off": return "the camps setting or world events are off";
                case "here": return "a camp is here already";
                case "full": return a + " camps are here, as many as the land takes at \"How many camps\": " + CampCrowds.Level;
                case "nohome": return "there's no home map";
                case "early": return "never in the first twenty days";
                case "gap": return "the last camp left on day " + HomeOf(map).lastEndedTick / GenDate.TicksPerDay + ", and the next comes " + a + " days after";
                case "threat": return "there's danger on the map";
                case "weather": return "it's " + a + " °C outside, and no camp comes above " + CampWeatherMath.ExtremeHeat.ToString("0") + " °C, nor below " + CampWeatherMath.ExtremeCold.ToString("0") + " °C but winter folk, none of whom can come now";
                case "hazard": return "no camp comes while " + a + " lasts";
                case "era": return "no kind of camp fits the colony's era";
                case "nofriends": return "no friendly people could send one";
                default: return "there's no room for a camp on the map";
            }
        }

        /// <summary>The same, for the World page: in the player's words, with how many open mornings have passed.</summary>
        public string WhyNotForPlayer(Map map)
        {
            var (code, a, b) = Blocker(map);
            if (code == null) return "CC_Camp_Why_Open".Translate(HomeOf(map).openMornings, Mathf.RoundToInt(1f / Mathf.Max(0.001f, TravelLand.CampChance(map)))).Resolve();
            return ("CC_Camp_Why_" + code).Translate(a ?? "", b ?? "").Resolve();
        }

        /// <summary>
        /// The first thing that keeps a camp from coming, as a short code with up to two details (null when nothing
        /// does): off, here (the land takes one and one is here), full (it takes more and they are all here), nohome,
        /// early, gap (days between), threat, weather (the temperature), hazard (its name), era, nofriends, noroom.
        /// </summary>
        public (string code, string a, string b) Blocker(Map map)
        {
            if (!Enabled) return ("off", null, null);
            // Each home has its own camps (as many at once as the setting and the map's room take: CampCrowds.cs), and its
            // own gap since the last left.
            var home = HomeOf(map);
            int here = home.camps.Count(c => c != null);
            string full = CampCrowdMath.Full(here, CampCrowds.CapAt(map));
            if (full == "here") return ("here", null, null);
            if (full == "full") return ("full", here.ToString(), null);
            if (map == null || !map.IsPlayerHome) return ("nohome", null, null);
            if (GenDate.DaysPassed < 20) return ("early", (20 - GenDate.DaysPassed).ToString(), null);
            // A colony known for its hospitality hears from the road sooner (ColonyTraitDef.campGap); the setting's level
            // sets the rest itself (a fortnight at the usual level).
            float gap = CampCrowdMath.GapDays(CampCrowds.Level) * (CharacterTracker.For(map)?.traits?.CampGap ?? 1f);
            // And a remote home sooner still: the rest after a camp up to 30% shorter (World/Region/TravelLand.cs).
            gap *= TravelLand.GapFactor(map);
            if (CampStoryMath.InGap(Now, home.lastEndedTick, gap))
                return ("gap", gap.ToString("0"), TextUtil.DaysString(Mathf.RoundToInt(GenDate.TicksPerDay * gap) - (Now - home.lastEndedTick)));
            if (MapFacts.ThreatOn(map)) return ("threat", null, null);
            float temp = map.mapTemperature.OutdoorTemp;
            // Past the flat floor of the cold, only winter folk come (CampKindDef.winter), as the game's limits allow each of them.
            if (CampWeatherMath.TooExtremeToCome(temp) && (CampWeatherMath.TooExtremeToCome(temp, true)
                                                          || !DefDatabase<CampKindDef>.AllDefs.Any(k => k.winter && k.FitsEra() && k.Possible && !k.fights && k.FitsWeather(map))))
                return ("weather", temp.ToString("0"), null);
            var hazard = CampWeather.Hazard(map);
            if (hazard != null) return ("hazard", hazard.Label, null);
            if (!DefDatabase<CampKindDef>.AllDefs.Any(k => k.FitsEra())) return ("era", null, null);
            if (Arrivals.Friendly() == null) return ("nofriends", null, null);
            if (!CampSite.AnyRoom(map)) return ("noroom", null, null);
            return (null, null, null);
        }

        /// <summary>
        /// Picks the kind of camp, its people and its spot; makes the people (not on the map yet). Null if none fits,
        /// with what went wrong added to <paramref name="why"/> if given.
        /// </summary>
        public CampSetup Prepare(Map map, List<string> why = null)
        {
            if (why == null)
            {
                lastWhy.Clear();
                why = lastWhy;
            }
            // What the land near the home has that draws travellers (World/Region/TravelLand.cs): a kind that seeks it
            // weighs more the more remote the home, and says it came for it; one that comes only for it needs it.
            var land = TravelLand.Read(map);
            // A kind of one season, or only from one of Rimfolk's peoples, comes then and while that people is at peace (Peoples/).
            var kinds = DefDatabase<CampKindDef>.AllDefs.Where(k => k.FitsEra() && k.Possible && !k.fights && k.FitsMap(map) && k.FitsSeason(map) && k.FitsWeather(map) && Peoples.CanSend(k)
                                                                    && (!k.seekOnly || TravelMath.AnyHolds(k.seeks, land.holds))).ToList();
            if (kinds.Count == 0)
            {
                why?.Add("no kind of camp can come now");
                return null;
            }
            // Where to pitch: in hot weather, among trees for the shade where there are any. An old camp's site still
            // standing may do instead (CampGround.cs), when no fresh spot is left.
            var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 7);
            bool hot = CampWeatherMath.PitchForHeat(usual.high);
            bool spot = CampSite.TryFind(map, out var center, hot);
            if (!spot && !CampGrounds.On(map).Any())
            {
                why?.Add("no spot for a camp was found");
                return null;
            }
            var colony = CampSite.ColonyCenter(map);
            // People the colony knows, coming back (CampMemory.cs); else a new party. Never a people at war with a camp
            // here already (CampCrowds.AtPeaceHere).
            var past = CampReturns.Pick(map);
            if (past != null && !CampCrowds.AtPeaceHere(map, past.faction)) past = null;
            // People come at the hours that fit them (CampKindDef.arrives): a returning camp out of its hours mostly comes another day.
            int hourNow = GenLocalDate.HourOfDay(map);
            if (past?.kind != null && !CampStoryMath.InHours(hourNow, past.kind.arrives.min, past.kind.arrives.max) && Rand.Chance(0.75f)) past = null;
            if (past != null)
            {
                // Some kinds pitch by what they came for (the water, the ruins, the monolith: CampKindsLand.cs).
                string again = CampSeek.Choose(past.kind, land);
                // Back to the site they held, or another old one still standing, when it is worth it (CampGround.cs).
                var ground = CampGrounds.Choose(map, past.kind, past.faction, again);
                var at = ground != null ? ground.center : spot ? CampSeek.SiteFor(again, land, map, CampLand.SiteFor(past.kind, map, center, hot), hot) : IntVec3.Invalid;
                var back = at.IsValid ? Make(map, at, colony, past.kind, past.faction, hot, why, past, null, ground) : null;
                if (back != null)
                {
                    back.seek = again;
                    back.land = land;
                    return back;
                }
            }
            for (int tries = 0; tries < 6; tries++)
            {
                // A refugee family comes more often while a town in reach struggles, and is likeliest of its people (World/Region/RiseFallEffects.cs).
                var kind = kinds.RandomElementByWeight(k => k.WeightNow(map) * k.HourFit(hourNow) * TravelLand.SeekWeight(k, map, land) * RiseFallEffects.CampFactor(k, map)
                                                            * (k.trader ? StoryDirection.Factor(StoryWaysMath.Traders) : 1f));
                // Peoples the colony did good turns for camp here more often, those with grudges less (Interplay/), and a kind
                // one of Rimfolk's peoples favours (their drovers, their healers on the road) likelier from them (Peoples/).
                // Two peoples at war never camp on the same land (CampCrowds.AtPeaceHere).
                var f = Arrivals.Friendly(x => kind.FitsFaction(x) && CampCrowds.AtPeaceHere(map, x),
                    x => Interplay.CampWeight(x) * RiseFallEffects.CampPeople(kind, map, x) * Peoples.CampFactor(kind, x));
                if (f == null)
                {
                    why?.Add(kind.label + ": no friendly people for it");
                    continue;
                }
                // An old camp's site still standing, when it is worth moving into (CampGround.cs): they take it over.
                string seekHere = CampSeek.Choose(kind, land);
                var old = CampGrounds.Choose(map, kind, f, seekHere);
                if (old != null)
                {
                    var into = Make(map, old.center, colony, kind, f, hot, why, null, null, old);
                    if (into != null)
                    {
                        into.seek = seekHere;
                        into.land = land;
                        return into;
                    }
                }
                if (!spot)
                {
                    why?.Add(kind.label + ": no spot for it, and no old camp's site it could move into");
                    continue;
                }
                var site = CampLand.SiteFor(kind, map, center, hot);
                if (!site.IsValid)
                {
                    why?.Add(kind.label + ": no spot by what they came for");
                    continue;
                }
                // Near the ruins or the ore they came for, where there's room (CampSeekers.cs).
                string seek = CampSeek.Choose(kind, land);
                site = CampSeek.SiteFor(seek, land, map, site, hot);
                var setup = Make(map, site, colony, kind, f, hot, why, null);
                if (setup != null)
                {
                    setup.seek = seek;
                    setup.land = land;
                    return setup;
                }
                if (why != null && why.Count > 0 && why[why.Count - 1].EndsWith(HemmedIn)) return null;
            }
            return null;
        }

        /// <summary>
        /// A camp of one kind, now, for a world event that calls for it (a wardens' post by the new road): from these
        /// people when the kind fits them, else from any friendly people. Null when none can come (another camp is here,
        /// no spot, no people), with why if asked.
        /// </summary>
        public Camp StartKind(Map map, CampKindDef kind, Faction f, List<string> why = null)
        {
            if (map == null || kind == null || !RoomFor(map)) return null;
            if (!kind.FitsEra() || !kind.Possible || !kind.FitsMap(map) || kind.seekOnly && !TravelMath.AnyHolds(kind.seeks, TravelLand.Read(map).holds))
            {
                why?.Add(kind.label + ": not in this era");
                return null;
            }
            var usual = CampWeather.StayRange(map, GenDate.TicksPerDay * 7);
            bool hot = CampWeatherMath.PitchForHeat(usual.high);
            if (!CampSite.TryFind(map, out var center, hot) || !(center = CampLand.SiteFor(kind, map, center, hot)).IsValid)
            {
                why?.Add("no spot for a camp was found");
                return null;
            }
            if (f == null || !kind.FitsFaction(f) || !Arrivals.Usable(f) || Arrivals.PersonKind(f) == null || !CampCrowds.AtPeaceHere(map, f))
                f = Arrivals.Friendly(x => kind.FitsFaction(x) && CampCrowds.AtPeaceHere(map, x), Interplay.CampWeight);
            if (f == null)
            {
                why?.Add(kind.label + ": no friendly people for it");
                return null;
            }
            var setup = Make(map, center, CampSite.ColonyCenter(map), kind, f, hot, why, null);
            if (setup != null)
            {
                setup.land = TravelLand.Read(map);
                setup.seek = CampSeek.Choose(kind, setup.land);
            }
            var c = setup == null ? null : Start(setup);
            if (c != null) Announce(c);
            return c;
        }

        /// <summary>
        /// The letter a camp's coming gets when no event sends one (a camp a world event called for): its kind's title
        /// and text, with the colonist their people remember by name (or anyone grown) as the one they spoke to.
        /// </summary>
        public static void AnnounceCamp(Camp c) => Announce(c);

        private static void Announce(Camp c)
        {
            if (c?.kind == null || c.map == null) return;
            try
            {
                var tags = new HashSet<string>();
                Flavor.AddEraTags(tags);
                CampWords.AddTags(tags, c.kind, c.faction, c.map);
                var a = KinMemory.Greeter(c.faction, c.map) ?? IncidentWorker_Happening.Colonists(c.map).Where(p => !p.DevelopmentalStage.Juvenile()).RandomElementWithFallback();
                string title = c.Format(Flavor.Pick(c.kind.titles, tags) ?? Flavor.PickText(c.kind.titles), c.leader, a);
                string text = c.Format(Flavor.Pick(c.kind.texts, tags) ?? Flavor.PickText(c.kind.texts), c.leader, a);
                c.Tell(title, text, LetterDefOf.NeutralEvent, true, c.leader);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp announced", e);
            }
        }

        /// <summary>Why a camp couldn't be laid out at a spot (for the diary): the colony's land, water or overhead rock hem its fire and beds in.</summary>
        public const string HemmedIn = ": the colony's land, water or rock hem in its fire and beds there";

        /// <summary>
        /// A camp of this kind and people at the spot: its stay, its layout for the weather, its people (those
        /// <paramref name="given"/>, else the remembered ones coming back, filled up with new ones), dressed for the
        /// stay. Null if it can't be, with why. <paramref name="ground"/>: an old camp's site they move into (CampGround.cs),
        /// its standing pieces theirs and what is missing built anew, as many of them as its beds take.
        /// </summary>
        public CampSetup Make(Map map, IntVec3 center, IntVec3 colony, CampKindDef kind, Faction f, bool hot, List<string> why, PastCamp past, List<Pawn> given = null,
            CampGround ground = null)
        {
            var kept = given ?? past?.Living ?? new List<Pawn>();
            int n = given != null ? given.Count : Mathf.Clamp(Mathf.Max(kind.people.RandomInRange, kept.Count), 1, 12);
            if (ground != null && given == null) n = CampGroundMath.People(n, ground.Beds, Mathf.Max(1, kind.people.min));
            int stay = Mathf.RoundToInt(kind.days.RandomInRange * GenDate.TicksPerDay);
            var (low, high) = CampWeather.StayRange(map, stay);
            bool cold = CampWeatherMath.PitchForCold(low);
            IntVec3 stash, share;
            var plans = ground != null
                ? CampGrounds.Adopt(ground, map, kind, f, n, out stash, out share)
                : CampSite.Layout(map, ref center, kind, n, colony, out stash, out share, cold, f, stay / (float)GenDate.TicksPerDay);
            if (plans == null)
            {
                why?.Add(kind.label + (ground != null ? ": " + ground.name + " no longer holds a camp" : HemmedIn));
                return null;
            }
            var pawns = given != null ? given.ToList() : MakePeople(kind, f, n, kept);
            if (pawns.Count == 0)
            {
                why?.Add(kind.label + " of " + f.Name + ": its people couldn't be made");
                return null;
            }
            // Dressed for their stay; and none of them would be hurt by its weather, dressed as they are (CampWeatherMath).
            var dressed = new List<Apparel>();
            foreach (var p in pawns)
            {
                try
                {
                    dressed.AddRange(CampWeather.Dress(p, low, high, f, kind));
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp people dressed for the weather", e);
                }
            }
            int unfit = pawns.Count(p =>
            {
                var comfy = GenTemperature.ComfortableTemperatureRange(p);
                return !CampWeatherMath.WouldStop(low, high, comfy.min, comfy.max);
            });
            if (unfit > 0 && given == null)
            {
                why?.Add(kind.label + " of " + f.Name + ": " + unfit + " of " + pawns.Count + " couldn't dress for " + low.ToString("0") + " to " + high.ToString("0") + " °C");
                // The remembered stay in the world as they were; the new are let go.
                foreach (var p in pawns.Where(p => !kept.Contains(p))) Arrivals.Discard(p);
                return null;
            }
            return new CampSetup
            {
                kind = kind, faction = f, map = map, center = center, stash = stash, share = share, colony = colony, plans = plans, pawns = pawns,
                place = CampSite.Direction(center, colony), stayTicks = stay, low = low, high = high, past = past, ground = ground,
                pitch = new CampPitch
                {
                    clothes = CampWeather.Clothes(dressed),
                    clothesForCold = dressed.Sum(a => a.GetStatValue(StatDefOf.Insulation_Cold)) >= dressed.Sum(a => a.GetStatValue(StatDefOf.Insulation_Heat)),
                    cold = cold && CampSite.BedsClose(plans), windbreak = cold && plans.Any(x => x.role == "wall"), shade = hot && CampSite.Shaded(map, center),
                    shelter = ShelterOf(plans)
                }
            };
        }

        /// <summary>How they will sleep, from the layout: "hut" (one room for all), "tents" (rooms of tent walls), "tentbeds" (a mod's tents), "ring" (round the fire).</summary>
        public static string ShelterOf(List<CampPlan> plans)
        {
            int rooms = CampSite.Rooms(plans).Count();
            if (rooms == 1 && plans.Count(p => p.role == "hutbed") > 2) return "hut";
            if (rooms >= 1) return plans.Any(p => p.role == "hut" && p.def != null && p.def.MadeFromStuff && p.stuff != null && !p.stuff.IsWithinCategory(ThingCategoryDefOf.ResourcesRaw)) ? "tents" : rooms == 1 ? "hut" : "huts";
            if (plans.Any(p => p.role == "bed" && CampSite.IsTent(p.def))) return "tentbeds";
            return "ring";
        }

        private static List<Pawn> MakePeople(CampKindDef kind, Faction f, int n, List<Pawn> kept = null)
        {
            var list = new List<Pawn>();
            var personKind = Arrivals.PersonKind(f);
            if (personKind == null) return list;
            string last = null;
            // The same faces first (a camp coming back), then new ones to make up the number.
            if (kept != null)
                foreach (var p in kept.Where(p => p != null && !p.Dead && !p.Discarded && !p.Spawned))
                {
                    if (list.Count >= n) break;
                    list.Add(p);
                    if (kind.family && last == null) last = (p.Name as NameTriple)?.Last;
                }
            for (int i = list.Count; i < n; i++)
            {
                bool child = kind.family && i >= 2 && ModsConfig.BiotechActive && Rand.Chance(0.5f);
                try
                {
                    var p = Arrivals.Make(personKind, f, child ? (FloatRange?)null : kind.ages, null, kind.family ? last : null, child, kind.family && list.Count > 0 ? list[0] : null);
                    if (p == null) continue;
                    if (kind.family && last == null) last = (p.Name as NameTriple)?.Last;
                    list.Add(p);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp people", e);
                }
            }
            return list;
        }

        /// <summary>Lets go of people made for a camp that never came.</summary>
        public static void Discard(CampSetup s)
        {
            if (s?.pawns == null) return;
            foreach (var p in s.pawns.Where(p => p != null && !p.Spawned)) Arrivals.Discard(p);
        }

        /// <summary>
        /// They walk in from the edge nearest their spot, carrying what they need. Not when the land holds as many camps as
        /// it takes already (CampCrowds), unless <paramref name="force"/> (dev mode's camp beside those here).
        /// </summary>
        public Camp Start(CampSetup s, bool force = false)
        {
            if (s?.map == null) return null;
            var home = HomeOf(s.map);
            home.openMornings = 0;
            if (!force && !CampCrowds.Room(s.map)) return null;
            var map = s.map;
            if (!EntryNear(map, s.center, out var entry)) return null;
            foreach (var p in s.pawns)
            {
                if (p.Spawned)
                {
                    // On the map already (a war party after its fight): out of its old group, into the camp's.
                    Lords.Leave(p);
                    continue;
                }
                // Someone kept in the world to come back (CampMemory.cs) is taken out of it first.
                Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                var cell = CellFinder.RandomClosewalkCellNear(entry, map, 4);
                GenSpawn.Spawn(p, cell, map);
            }
            var c = new Camp
            {
                id = nextId++, kind = s.kind, map = map, faction = s.faction, members = s.pawns.ToList(), leader = s.pawns[0],
                center = s.center, stash = s.stash, share = s.share, colonyCenter = s.colony, place = s.place, plans = s.plans,
                startTick = Now, stayTicks = s.stayTicks, phase = CampPhase.Coming, pitch = s.pitch
            };
            if (s.past != null) CampReturns.Started(c, s.past);
            // What brought them, and where it is (CampSeekers.cs).
            c.SeekStarted(s);
            try
            {
                Equip(c);
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp supplies", e);
            }
            // The animals they bring (pack beasts, a herd, a dog) walk in with them, in their group (CampLook.cs).
            try
            {
                foreach (var a in Camp.MakeAnimals(s.kind, s.faction, map))
                {
                    GenSpawn.Spawn(a, CellFinder.RandomClosewalkCellNear(entry, map, 5), map);
                    c.herd.Add(a);
                }
            }
            catch (Exception e)
            {
                CouncilLog.ErrorOnce("camp animals", e);
            }
            try
            {
                c.lord = LordMaker.MakeNewLord(s.faction, new LordJob_Camp(s.center, s.kind.fights), map, c.members.Concat(c.herd));
            }
            catch (Exception e)
            {
                // No group to live in: they walk off again rather than stand about on the map with nothing to do.
                CouncilLog.ErrorOnce("camp group", e);
                Arrivals.SendAway(c.members);
                try
                {
                    var beasts = c.herd.Where(a => a.Spawned && !a.Dead).ToList();
                    if (beasts.Count > 0) LordMaker.MakeNewLord(s.faction, new LordJob_ExitMapBest(Pace.Errand, false, false), map, beasts);
                }
                catch (Exception e2)
                {
                    CouncilLog.ErrorOnce("camp animals sent off", e2);
                }
                return null;
            }
            // Mourners carry their dead in from the edge (CampDead.cs).
            c.BearDead();
            home.camps.Add(c);
            // An old camp's site they move into (CampGround.cs): held by them from now.
            if (s.ground != null) CampGrounds.Moving(c, s.ground);
            // What the land kinds bring with them: the spacers' wrecked craft (CampKindsLand.cs).
            c.LandStarted();
            return c;
        }

        /// <summary>A cell on the map's edge, open and with a way to <paramref name="center"/>, the nearest of a few tried.</summary>
        public static bool EdgeNear(Map map, IntVec3 center, out IntVec3 entry) => EntryNear(map, center, out entry);

        private static bool EntryNear(Map map, IntVec3 center, out IntVec3 entry)
        {
            entry = IntVec3.Invalid;
            float best = float.MaxValue;
            var tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false);
            for (int i = 0; i < 24; i++)
            {
                if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReach(c, center, PathEndMode.OnCell, tp), map, CellFinder.EdgeRoadChance_Neutral, out var cell)) continue;
                float d = cell.DistanceToSquared(center);
                if (d < best)
                {
                    best = d;
                    entry = cell;
                }
            }
            return entry.IsValid || Arrivals.TryEntryCell(map, out entry);
        }

        /// <summary>
        /// What they bring: the materials for their camp (a little over), two meals each and dried food for their stay,
        /// bows or rifles for hunters, and for a trading camp, a small stock to sell.
        /// </summary>
        private static void Equip(Camp c)
        {
            var adults = c.members.Where(p => !p.DevelopmentalStage.Juvenile() && p.inventory != null).ToList();
            if (adults.Count == 0) adults = c.members.Where(p => p.inventory != null).ToList();
            var totals = new Dictionary<ThingDef, int>();
            // What stands at an old camp's site they take over needs no materials (CampGround.cs).
            foreach (var plan in c.plans.Where(p => !p.reuse))
                foreach (var cost in Camp.Costs(plan))
                    totals[cost.thingDef] = (totals.TryGetValue(cost.thingDef, out var n) ? n : 0) + cost.count;
            int who = 0;
            foreach (var kv in totals)
            {
                int left = Mathf.CeilToInt(kv.Value * 1.1f);
                while (left > 0 && adults.Count > 0)
                {
                    var t = ThingMaker.MakeThing(kv.Key);
                    t.stackCount = Mathf.Min(left, kv.Key.stackLimit);
                    left -= t.stackCount;
                    adults[who++ % adults.Count].inventory.TryAddItemNotForSale(t);
                }
            }
            foreach (var p in c.members.Where(p => p.inventory != null))
            {
                var meals = ThingMaker.MakeThing(ThingDefOf.MealSimple);
                meals.stackCount = 2;
                p.inventory.TryAddItemNotForSale(meals);
            }
            c.provisions = Mathf.RoundToInt(c.members.Count * (c.stayTicks / (float)GenDate.TicksPerDay) * 1.6f);
            if (c.kind.armedHunters) ArmHunters(c, adults);
            foreach (var p in adults)
                if (p != c.leader && Rand.Chance(c.kind.hireChance)) c.willing.Add(p);
            if (c.kind.trader)
            {
                var t = adults.FirstOrDefault(p => p != c.leader) ?? c.leader;
                try
                {
                    if (t != null && MakeTrader(c, t)) c.willing.Remove(t);
                }
                catch (Exception e)
                {
                    CouncilLog.ErrorOnce("camp trader", e);
                }
            }
        }

        private static void ArmHunters(Camp c, List<Pawn> adults)
        {
            bool low = Avail.IsPreIndustrial || Peoples.TechOf(c.faction) <= TechLevel.Medieval;
            var def = (low ? null : DefDatabase<ThingDef>.GetNamedSilentFail("Gun_BoltActionRifle"))
                      ?? DefDatabase<ThingDef>.GetNamedSilentFail("Bow_Recurve") ?? DefDatabase<ThingDef>.GetNamedSilentFail("Bow_Short");
            if (def == null) return;
            int armed = 0;
            foreach (var p in adults.Where(p => p.equipment != null && !p.WorkTagIsDisabled(WorkTags.Violent)))
            {
                if (armed >= 3) break;
                var held = p.equipment.Primary;
                if (held != null && held.def.IsRangedWeapon)
                {
                    armed++;
                    continue;
                }
                if (held != null)
                {
                    p.equipment.Remove(held);
                    p.inventory.innerContainer.TryAdd(held);
                }
                var w = (ThingWithComps)ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
                p.equipment.AddEquipment(w);
                armed++;
            }
        }

        /// <summary>One of them trades from the camp, with the game's own trading: a small stock of their people's goods.</summary>
        /// <summary>Settled neighbours' trader takes on a new stock now and then (Holding.cs).</summary>
        public static void RefreshTrader(Camp c)
        {
            if (c?.trader == null || c.trader.Dead || c.trader.inventory == null) return;
            // What's left of the old stock stays theirs; the new is added to it.
            MakeTrader(c, c.trader);
        }

        private static bool MakeTrader(Camp c, Pawn p)
        {
            var tk = TraderKind(c.faction, c.kind.traderKind);
            if (tk == null) return false;
            if (p.trader == null) p.trader = new Pawn_TraderTracker(p);
            p.trader.traderKind = tk;
            p.mindState.wantsToTradeWithColony = true;
            var parms = new ThingSetMakerParams { traderDef = tk, tile = c.map.Tile, makingFaction = c.faction };
            var stock = ThingSetMakerDefOf.TraderStock.root.Generate(parms);
            float budget = Rand.Range(800f, 1500f) * LivingEvents.WealthFactor(c.map);
            float value = 0f;
            foreach (var t in stock.InRandomOrder().ToList())
            {
                if (t is Pawn animalOrPerson)
                {
                    // A camp has no pens or cages: no living stock.
                    if (!animalOrPerson.Destroyed && !Find.WorldPawns.Contains(animalOrPerson)) Find.WorldPawns.PassToWorld(animalOrPerson, PawnDiscardDecideMode.Discard);
                    continue;
                }
                if (t.def == ThingDefOf.Silver)
                {
                    t.stackCount = Mathf.Min(t.stackCount, Rand.RangeInclusive(250, 600));
                    p.inventory.innerContainer.TryAdd(t);
                    continue;
                }
                float unit = t.MarketValue;
                if (unit <= 0f) continue;
                if (value + unit * t.stackCount > budget)
                {
                    int fit = Mathf.FloorToInt((budget - value) / unit);
                    if (fit <= 0) continue;
                    t.stackCount = Mathf.Min(t.stackCount, fit);
                }
                value += unit * t.stackCount;
                p.inventory.innerContainer.TryAdd(t);
            }
            c.trader = p;
            c.traderSilver = p.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.Silver);
            return true;
        }

        private static TraderKindDef TraderKind(Faction f, string hint)
        {
            var all = new List<TraderKindDef>();
            if (f.def.visitorTraderKinds != null) all.AddRange(f.def.visitorTraderKinds);
            if (f.def.caravanTraderKinds != null) all.AddRange(f.def.caravanTraderKinds);
            all = all.Where(k => k != null && !k.orbital && k.permitRequiredForTrading == null).Distinct().ToList();
            if (!hint.NullOrEmpty())
            {
                var match = all.Where(k => k.defName.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (match.Count > 0) return match.RandomElement();
            }
            return f.def.visitorTraderKinds?.Where(k => k != null && k.permitRequiredForTrading == null).RandomElementWithFallback() ?? all.RandomElementWithFallback();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving) homes?.RemoveAll(h => h == null);
            Scribe_Collections.Look(ref homes, "homes", LookMode.Deep);
            // A save from before there were camps per colony: its one camp, holding, war party and clock are the seat's.
            if (Scribe.mode == LoadSaveMode.LoadingVars && homes == null)
            {
                oldSave = new CampHome();
                homes = new List<CampHome> { oldSave };
            }
            if (oldSave != null && Scribe.mode != LoadSaveMode.Saving) oldSave.ExposeData();
            Scribe_Values.Look(ref nextId, "nextId", 1);
            Scribe_Collections.Look(ref past, "past", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                oldSave = null;
                homes ??= new List<CampHome>();
                homes.RemoveAll(h => h == null);
                past ??= new List<PastCamp>();
                past.RemoveAll(p => p == null || p.kind == null || p.faction == null);
            }
        }
    }
}
