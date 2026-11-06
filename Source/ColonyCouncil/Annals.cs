using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>A finished year, as written down by the colony's chronicler.</summary>
    public class AnnalsEntry : IExposable
    {
        /// <summary>The calendar year (the annals of an older save were written by it).</summary>
        public int year;
        /// <summary>The colony's own year since the landing (the one year letter's: 1 for the first); 0 for an older save's annals.</summary>
        public int landYear;
        public string author;
        public string text;

        /// <summary>Its button on the Chronicle's header and its title: the colony's year, or an older save's calendar year.</summary>
        public string Button => landYear > 0 ? "CC_YearOne_Button".Translate(HerdStory.Ordinal(landYear).Named("ORD")).Resolve() : "CC_Annals_Button".Translate(year).Resolve();

        public string Title => landYear > 0 ? "CC_Year_Title".Translate(HerdStory.Ordinal(landYear).Named("ORD")).Resolve() : "CC_Annals_Title".Translate(year).Resolve();

        public void ExposeData()
        {
            Scribe_Values.Look(ref year, "year");
            Scribe_Values.Look(ref landYear, "landYear");
            Scribe_Values.Look(ref author, "author");
            Scribe_Values.Look(ref text, "text");
        }
    }

    /// <summary>
    /// The colony's year in review, the first part of the one year letter (World/YearLetter.cs, on the anniversary of the
    /// landing): who came and who was lost, raids, wealth, what the council granted and declared, traditions, friendships
    /// and feuds, the council's seats and the names earned. Counted here from one landing day to the next; written out
    /// and begun again by the letter.
    /// </summary>
    public class AnnalsTracker : IExposable
    {
        public int year = -1;
        public int yearStartTick;
        public float startWealth;
        public bool partial;
        public int startCount;
        /// <summary>Who was around at the last check; differences become joins, deaths and departures (kept as names).</summary>
        public List<Pawn> present = new List<Pawn>();
        public List<string> joined = new List<string>();
        public List<string> died = new List<string>();
        public List<string> gone = new List<string>();
        public int raids;
        private int lastThreatSeen;
        /// <summary>Each home's last big threat counted (its key: the tick), so two raids on two homes at once are two raids.</summary>
        private Dictionary<int, int> threatSeen = new Dictionary<int, int>();
        public List<string> decrees = new List<string>();
        public int dilemmas;
        public List<string> quit = new List<string>();
        public List<AnnalsEntry> written = new List<AnnalsEntry>();
        public List<string> traditionsKept = new List<string>();
        public List<string> traditionsBroken = new List<string>();
        public List<string> traditionsBorn = new List<string>();

        public void Notify_Tradition(string label, bool kept) => (kept ? traditionsKept : traditionsBroken).Add(label);

        public void Notify_TraditionBorn(string label) => traditionsBorn.Add(label);

        public List<string> tiesFormed = new List<string>();
        public List<string> omens = new List<string>();
        public List<string> goals = new List<string>();
        public List<string> secretsOut = new List<string>();

        public void Notify_Secret(Pawn p, string what) => secretsOut.Add("CC_Annals_SecretLine".Translate(p.LabelShort, what).Resolve());

        public void Notify_Aspiration(Pawn p, string goal) => goals.Add("CC_Annals_GoalLine".Translate(p.LabelShort, goal).Resolve());

        public void Notify_Omen(Pawn dreamer, string what) => omens.Add("CC_Annals_OmenLine".Translate(dreamer.LabelShort, what).Resolve());

        public void Notify_Tie(SocialTie t)
        {
            if (t?.a == null || t.b == null) return;
            tiesFormed.Add("CC_Annals_TieLine".Translate(t.a.LabelShort, t.b.LabelShort, t.Label).Resolve());
        }

        private static List<Pawn> Colonists() =>
            PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoLodgers.Where(p => !p.IsQuestLodger()).ToList();

        /// <summary>The seat: its date keeps the annals' year (the colony as a whole has one book of annals).</summary>
        private static Map Home => MapFacts.BusiestHome;

        /// <summary>The colony's wealth, all its homes together (with one colony, its home's).</summary>
        private static float Wealth() => Find.Maps.Where(m => m.IsPlayerHome).Sum(m => m.wealthWatcher.WealthTotal);

        public void Hourly(CouncilGameComponent comp)
        {
            var map = Home;
            if (map == null) return;
            // Count big threats (raids, sieges, manhunters) as they happen, at each home: two at once are two. A home not
            // looked at before goes by the last threat the annals counted anywhere (an older save's).
            foreach (var m in Find.Maps.Where(m => m.IsPlayerHome).ToList())
            {
                int key = Homes.KeyOf(m);
                int seen = threatSeen.TryGetValue(key, out int s) ? s : lastThreatSeen;
                int last = m.storyState.LastThreatBigTick;
                if (last <= seen) continue;
                if (seen > 0 || year >= 0) raids++;
                threatSeen[key] = last;
                lastThreatSeen = System.Math.Max(lastThreatSeen, last);
            }
            // Homes no longer ours are forgotten.
            if (threatSeen.Count > 0)
                foreach (int key in threatSeen.Keys.ToList())
                    if (Homes.MapOf(key) == null) threatSeen.Remove(key);
            if (year >= 0)
            {
                var current = Colonists();
                foreach (var p in present)
                {
                    if (p == null || current.Contains(p)) continue;
                    // Away, not gone: keep them on the books.
                    if (People.StillOurs(p))
                    {
                        current.Add(p);
                        continue;
                    }
                    string name = p.LabelShort;
                    if (p.Dead) died.Add(name);
                    else if (!quit.Contains(name)) gone.Add(name);
                }
                foreach (var p in current)
                    if (!present.Contains(p)) joined.Add(p.LabelShort);
                present = current;
            }
            // The year is written and begun again by the one year letter, on the anniversary of the landing.
            if (year < 0) Begin(map, Find.TickManager.TicksGame > GenDate.TicksPerDay * 2);
        }

        private void Begin(Map map, bool midway)
        {
            year = GenLocalDate.Year(map);
            yearStartTick = Find.TickManager.TicksGame;
            startWealth = Wealth();
            partial = midway;
            present = Colonists();
            startCount = present.Count;
            joined.Clear();
            died.Clear();
            gone.Clear();
            raids = 0;
            decrees.Clear();
            dilemmas = 0;
            quit.Clear();
            traditionsKept.Clear();
            traditionsBroken.Clear();
            traditionsBorn.Clear();
            tiesFormed.Clear();
            omens.Clear();
            goals.Clear();
            secretsOut.Clear();
        }

        /// <summary>Who writes the year: the colony's annalist once it has one (a name earned by writing it: Epithets/), else whoever reads and talks best.</summary>
        public Pawn Author(CouncilGameComponent comp, out bool keeps)
        {
            var now = Colonists();
            var annalist = ColonyCouncilMod.Settings.epithets ? comp.epithets.HolderOf("Council_Epithet_Annalist") : null;
            keeps = annalist != null && now.Contains(annalist) && !annalist.DevelopmentalStage.Juvenile();
            return keeps ? annalist : now.Where(p => !p.DevelopmentalStage.Juvenile())
                .OrderByDescending(p => (p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0) + (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0))
                .FirstOrDefault();
        }

        /// <summary>When this year's counting began (the last landing day, or later for an older save or a mod added midway).</summary>
        public int Since => yearStartTick;

        /// <summary>The year is counted from part of the way through only (the mod added midway, or an older save's calendar year).</summary>
        public bool Partial(int landingTick) => YearLetterMath.Partial(partial, yearStartTick, landingTick);

        /// <summary>The year written: kept for the Chronicle's header (its letter, by the colony's year) and counted again from today.</summary>
        public void Keep(Map map, int landYear, Pawn author, string text)
        {
            if (author != null) CouncilRecords.Add(author, NameRecords.AnnalsWritten);
            written.Add(new AnnalsEntry { year = year, landYear = landYear, author = author?.LabelShort, text = text });
            Caps.TrimOldest(written, Caps.AnnalsYears);
            Begin(map, false);
        }

        /// <summary>The author's last word on the year, in quotes and signed, or null.</summary>
        public string LastWord(Pawn author)
        {
            if (author == null) return null;
            float wealth = Wealth();
            var tags = new List<string>();
            if (died.Count > 0 || gone.Count > 0) tags.Add("annals:losses");
            if (raids >= 4) tags.Add("annals:violent");
            if (died.Count == 0 && raids <= 1) tags.Add("annals:peaceful");
            if (wealth > startWealth * 1.15f) tags.Add("annals:richer");
            if (joined.Count >= 2) tags.Add("annals:growing");
            string close = Flavor.FromPool("Council_Pool_AnnalsClose", author, null, ToneUtility.ToneFor(author), tags.ToArray());
            return close.NullOrEmpty() ? null : "\"" + close.Formatted(author.Named("PAWN")).Resolve() + "\"\n- " + author.LabelShort;
        }

        /// <summary>
        /// The year's people, its danger and fortune, the council's year, the traditions, bonds, secrets, lives fulfilled,
        /// dreams come true, the weather, friends and rivals, trust, the council's seats and the names earned: the first part
        /// of the year letter, one paragraph each.
        /// </summary>
        public void PeoplePart(CouncilGameComponent comp, Map map, Action<string> Para)
        {
            var now = Colonists();

            // People
            Para("CC_Annals_Count".Translate(startCount, now.Count).Resolve());
            if (joined.Count > 0) Para("CC_Annals_Joined".Translate(TextUtil.JoinAnd(joined.Distinct().ToList())).Resolve());
            if (died.Count > 0) Para("CC_Annals_Died".Translate(TextUtil.JoinAnd(died)).Resolve());
            if (gone.Count > 0) Para("CC_Annals_Gone".Translate(TextUtil.JoinAnd(gone.Distinct().ToList())).Resolve());
            if (quit.Count > 0) Para("CC_Annals_Quit".Translate(TextUtil.JoinAnd(quit)).Resolve());

            // Danger and fortune
            Para(raids == 0 ? "CC_Annals_NoRaids".Translate().Resolve() : Texts.CountKey("CC_Annals_Raids", raids).Translate(raids).Resolve());
            float wealth = Wealth();
            if (startWealth > 0f)
            {
                float change = (wealth - startWealth) / startWealth;
                string key = change > 0.15f ? "CC_Annals_Richer" : change < -0.15f ? "CC_Annals_Poorer" : "CC_Annals_Steady";
                Para(key.Translate(startWealth.ToString("N0"), wealth.ToString("N0")).Resolve());
            }

            // The council's year, counted from the chronicle (the history's view; a season's summary counts all it stands for).
            var entries = Chronicle.Since(yearStartTick).ToList();
            int Of(params ChronicleKind[] kinds) => entries.Where(e => kinds.Contains(e.kind)).Sum(e => e.entry.count);
            int granted = Of(ChronicleKind.WishGranted);
            int lapsed = Of(ChronicleKind.WishExpired);
            if (granted + lapsed > 0) Para(Texts.CountKey("CC_Annals_Wishes", granted).Translate(granted, lapsed).Resolve());
            int achieved = Of(ChronicleKind.AmbitionAchieved);
            int failed = Of(ChronicleKind.AmbitionFailed, ChronicleKind.AmbitionAbandoned);
            if (achieved + failed > 0) Para("CC_Annals_Ambitions".Translate(achieved, failed).Resolve());
            if (decrees.Count > 0)
            {
                var named = decrees.GroupBy(d => d).Select(g => g.Count() > 1 ? "CC_Annals_Times".Translate(g.Key, g.Count()).ToString() : g.Key).ToList();
                Para("CC_Annals_Decrees".Translate(TextUtil.JoinAnd(named)).Resolve());
            }
            if (dilemmas > 0) Para(Texts.CountKey("CC_Annals_Dilemmas", dilemmas).Translate(dilemmas).Resolve());
            if (traditionsBorn.Count > 0) Para("CC_Annals_TraditionsBorn".Translate(TextUtil.JoinAnd(traditionsBorn.Distinct().ToList())).Resolve());
            if (traditionsKept.Count > 0) Para("CC_Annals_TraditionsKept".Translate(TextUtil.JoinAnd(traditionsKept.Distinct().ToList())).Resolve());
            if (tiesFormed.Count > 0) Para("CC_Annals_Ties".Translate(TextUtil.JoinAnd(tiesFormed.Distinct().Take(8).ToList())).Resolve());
            if (secretsOut.Count > 0) Para("CC_Annals_Secrets".Translate(TextUtil.JoinAnd(secretsOut.Take(6).ToList())).Resolve());
            if (goals.Count > 0) Para("CC_Annals_Goals".Translate(TextUtil.JoinAnd(goals.Take(8).ToList())).Resolve());
            if (omens.Count > 0) Para("CC_Annals_Omens".Translate(TextUtil.JoinAnd(omens.Take(6).ToList())).Resolve());
            // The year's weather, from the colony's record of it (Weather/WeatherBook.cs): the calendar year most of it fell in.
            int mid = Find.TickManager.TicksGame - (Find.TickManager.TicksGame - yearStartTick) / 2;
            Para(WeatherBook.Of(map)?.AnnalsLine(GenDate.Year(GenDate.TickGameToAbs(mid), Find.WorldGrid.LongLatOf(map.Tile).x)));
            if (traditionsBroken.Count > 0) Para("CC_Annals_TraditionsBroken".Translate(TextUtil.JoinAnd(traditionsBroken.Distinct().ToList())).Resolve());

            // People and their feelings
            var adults = now.Where(p => p.relations != null && p.Spawned).ToList();
            Pawn fa = null, fb = null, ra = null, rb = null;
            int best = 39, worst = -29;
            for (int i = 0; i < adults.Count; i++)
            {
                for (int j = i + 1; j < adults.Count; j++)
                {
                    var a = adults[i];
                    var b = adults[j];
                    int ab = a.relations.OpinionOf(b);
                    int ba = b.relations.OpinionOf(a);
                    int low = System.Math.Min(ab, ba);
                    int high = System.Math.Max(ab, ba);
                    if (low > best)
                    {
                        best = low;
                        fa = a;
                        fb = b;
                    }
                    if (high < worst)
                    {
                        worst = high;
                        ra = a;
                        rb = b;
                    }
                }
            }
            if (fa != null) Para("CC_Annals_Friends".Translate(fa.Named("A"), fb.Named("B")).Resolve());
            if (ra != null) Para("CC_Annals_Rivals".Translate(ra.Named("A"), rb.Named("B")).Resolve());
            var devoted = now.Where(p => comp.standings.TrustOf(p) >= 25f).OrderByDescending(p => comp.standings.TrustOf(p)).FirstOrDefault();
            var bitter = now.Where(p => comp.standings.TrustOf(p) <= -15f).OrderBy(p => comp.standings.TrustOf(p)).FirstOrDefault();
            if (devoted != null) Para("CC_Annals_Devoted".Translate(devoted.Named("PAWN")).Resolve());
            if (bitter != null) Para("CC_Annals_Bitter".Translate(bitter.Named("PAWN")).Resolve());
            // Each colony's council (with more than one colony, each named by its settlement).
            foreach (var council in ColonyState.Ticking())
            {
                var seats = OfficeTracker.Offices.SelectMany(o => council.offices.HoldersOf(o).Select(h => (o, h))).Select(x => x.h.LabelShort + " (" + x.o.label + ")").ToList();
                if (seats.Count == 0) continue;
                string line = "CC_Annals_Offices".Translate(TextUtil.JoinAnd(seats)).Resolve();
                Para(ColonyState.Several && council.Map != null ? "CC_Tell_At".Translate(council.Name.Named("PLACE"), line.Named("TEXT")).Resolve() : line);
            }
            var titled = now.Select(p => (p, titles: comp.epithets.TitlesOf(p))).Where(x => !x.titles.NullOrEmpty())
                .Select(x => x.p.LabelShort + " " + x.titles).ToList();
            if (titled.Count > 0) Para("CC_Annals_Epithets".Translate(TextUtil.JoinAnd(titled)).Resolve());
            var spoiled = now.Where(p => Standards.LevelOf(p) == StandardsLevel.Spoiled).ToList();
            if (spoiled.Count > 0) Para("CC_Annals_Spoiled".Translate(Names(spoiled)).Resolve());
        }

        private static string Names(List<Pawn> pawns) => TextUtil.JoinAnd(pawns.Select(p => p.LabelShort).ToList());

        public void Notify_Quit(Pawn p)
        {
            if (p != null) quit.Add(p.LabelShort);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref year, "year", -1);
            Scribe_Values.Look(ref yearStartTick, "yearStartTick");
            Scribe_Values.Look(ref startWealth, "startWealth");
            Scribe_Values.Look(ref partial, "partial");
            Scribe_Values.Look(ref startCount, "startCount");
            Scribe_Collections.Look(ref present, "present", LookMode.Reference);
            Scribe_Collections.Look(ref joined, "joined", LookMode.Value);
            Scribe_Collections.Look(ref died, "died", LookMode.Value);
            Scribe_Collections.Look(ref gone, "gone", LookMode.Value);
            Scribe_Values.Look(ref raids, "raids");
            Scribe_Values.Look(ref lastThreatSeen, "lastThreatSeen");
            Scribe_Collections.Look(ref threatSeen, "threatSeen", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref decrees, "decrees", LookMode.Value);
            Scribe_Values.Look(ref dilemmas, "dilemmas");
            Scribe_Collections.Look(ref quit, "quit", LookMode.Value);
            Scribe_Collections.Look(ref written, "written", LookMode.Deep);
            Scribe_Collections.Look(ref traditionsKept, "traditionsKept", LookMode.Value);
            Scribe_Collections.Look(ref traditionsBroken, "traditionsBroken", LookMode.Value);
            Scribe_Collections.Look(ref traditionsBorn, "traditionsBorn", LookMode.Value);
            Scribe_Collections.Look(ref tiesFormed, "tiesFormed", LookMode.Value);
            Scribe_Collections.Look(ref omens, "omens", LookMode.Value);
            Scribe_Collections.Look(ref goals, "goals", LookMode.Value);
            Scribe_Collections.Look(ref secretsOut, "secretsOut", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (present == null) present = new List<Pawn>();
                present.RemoveAll(p => p == null);
                threatSeen ??= new Dictionary<int, int>();
                if (joined == null) joined = new List<string>();
                if (died == null) died = new List<string>();
                if (gone == null) gone = new List<string>();
                if (decrees == null) decrees = new List<string>();
                if (quit == null) quit = new List<string>();
                if (written == null) written = new List<AnnalsEntry>();
                written.RemoveAll(w => w == null);
                Caps.TrimOldest(written, Caps.AnnalsYears);
                if (traditionsKept == null) traditionsKept = new List<string>();
                if (traditionsBroken == null) traditionsBroken = new List<string>();
                if (traditionsBorn == null) traditionsBorn = new List<string>();
                if (tiesFormed == null) tiesFormed = new List<string>();
                if (omens == null) omens = new List<string>();
                if (goals == null) goals = new List<string>();
                if (secretsOut == null) secretsOut = new List<string>();
            }
        }
    }
}
