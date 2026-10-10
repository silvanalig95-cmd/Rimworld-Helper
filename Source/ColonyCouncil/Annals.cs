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
        public int year;
        public string author;
        public string text;

        public void ExposeData()
        {
            Scribe_Values.Look(ref year, "year");
            Scribe_Values.Look(ref author, "author");
            Scribe_Values.Look(ref text, "text");
        }
    }

    /// <summary>
    /// The colony's year in review. At each new year a colonist (the best reader or talker) writes up the year
    /// that ended: who came and who was lost, raids, what the council granted and declared, friendships and feuds.
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
        public List<string> decrees = new List<string>();
        public int dilemmas;
        public List<string> quit = new List<string>();
        public List<AnnalsEntry> written = new List<AnnalsEntry>();

        private static List<Pawn> Colonists() =>
            PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists_NoLodgers.Where(p => !p.IsQuestLodger()).ToList();

        private static Map Home => Find.Maps.Where(m => m.IsPlayerHome).OrderByDescending(m => m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();

        public void Hourly(CouncilGameComponent comp)
        {
            var map = Home;
            if (map == null) return;
            // Count big threats (raids, sieges, manhunters) as they happen.
            int last = Find.Maps.Where(m => m.IsPlayerHome).Select(m => m.storyState.LastThreatBigTick).DefaultIfEmpty(0).Max();
            if (last > lastThreatSeen)
            {
                if (lastThreatSeen > 0 || year >= 0) raids++;
                lastThreatSeen = last;
            }
            if (year >= 0)
            {
                var current = Colonists();
                foreach (var p in present)
                {
                    if (p == null || current.Contains(p)) continue;
                    string name = p.LabelShort;
                    if (p.Dead) died.Add(name);
                    else if (!quit.Contains(name)) gone.Add(name);
                }
                foreach (var p in current)
                    if (!present.Contains(p)) joined.Add(p.LabelShort);
                present = current;
            }
            int now = GenLocalDate.Year(map);
            if (year < 0)
            {
                Begin(map, now, Find.TickManager.TicksGame > GenDate.TicksPerDay * 2);
                return;
            }
            if (now <= year) return;
            if (ColonyCouncilMod.Settings.annals) Write(comp, map);
            Begin(map, now, false);
        }

        private void Begin(Map map, int newYear, bool midway)
        {
            year = newYear;
            yearStartTick = Find.TickManager.TicksGame;
            startWealth = map.wealthWatcher.WealthTotal;
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
        }

        private void Write(CouncilGameComponent comp, Map map)
        {
            var now = Colonists();
            var author = now.Where(p => !p.DevelopmentalStage.Juvenile())
                .OrderByDescending(p => (p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level ?? 0) + (p.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0))
                .FirstOrDefault();
            if (author == null) return;

            var sb = new StringBuilder();
            void Para(string s)
            {
                if (s.NullOrEmpty()) return;
                if (sb.Length > 0) sb.Append("\n\n");
                sb.Append(s);
            }

            Para((partial ? "CC_Annals_OpeningPartial" : "CC_Annals_Opening").Translate(year, author.Named("PAWN")).Resolve());

            // People
            Para("CC_Annals_Count".Translate(startCount, now.Count).Resolve());
            if (joined.Count > 0) Para("CC_Annals_Joined".Translate(TextUtil.JoinAnd(joined.Distinct().ToList())).Resolve());
            if (died.Count > 0) Para("CC_Annals_Died".Translate(TextUtil.JoinAnd(died)).Resolve());
            if (gone.Count > 0) Para("CC_Annals_Gone".Translate(TextUtil.JoinAnd(gone.Distinct().ToList())).Resolve());
            if (quit.Count > 0) Para("CC_Annals_Quit".Translate(TextUtil.JoinAnd(quit)).Resolve());

            // Danger and fortune
            Para(raids == 0 ? "CC_Annals_NoRaids".Translate().Resolve() : "CC_Annals_Raids".Translate(raids).Resolve());
            float wealth = map.wealthWatcher.WealthTotal;
            if (startWealth > 0f)
            {
                float change = (wealth - startWealth) / startWealth;
                string key = change > 0.15f ? "CC_Annals_Richer" : change < -0.15f ? "CC_Annals_Poorer" : "CC_Annals_Steady";
                Para(key.Translate(startWealth.ToString("N0"), wealth.ToString("N0")).Resolve());
            }

            // The council's year
            var entries = comp.chronicle.Where(e => e.tick >= yearStartTick).ToList();
            int granted = entries.Count(e => e.kind == ChronicleKind.WishGranted);
            int lapsed = entries.Count(e => e.kind == ChronicleKind.WishExpired);
            if (granted + lapsed > 0) Para("CC_Annals_Wishes".Translate(granted, lapsed).Resolve());
            int achieved = entries.Count(e => e.kind == ChronicleKind.AmbitionAchieved);
            int failed = entries.Count(e => e.kind == ChronicleKind.AmbitionFailed || e.kind == ChronicleKind.AmbitionAbandoned);
            if (achieved + failed > 0) Para("CC_Annals_Ambitions".Translate(achieved, failed).Resolve());
            if (decrees.Count > 0)
            {
                var named = decrees.GroupBy(d => d).Select(g => g.Count() > 1 ? "CC_Annals_Times".Translate(g.Key, g.Count()).ToString() : g.Key).ToList();
                Para("CC_Annals_Decrees".Translate(TextUtil.JoinAnd(named)).Resolve());
            }
            if (dilemmas > 0) Para("CC_Annals_Dilemmas".Translate(dilemmas).Resolve());

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
            var titled = now.Select(p => (p, titles: comp.epithets.TitlesOf(p))).Where(x => !x.titles.NullOrEmpty())
                .Select(x => x.p.LabelShort + " " + x.titles).ToList();
            if (titled.Count > 0) Para("CC_Annals_Epithets".Translate(TextUtil.JoinAnd(titled)).Resolve());
            var spoiled = now.Where(p => Standards.LevelOf(p) == StandardsLevel.Spoiled).ToList();
            if (spoiled.Count > 0) Para("CC_Annals_Spoiled".Translate(Names(spoiled)).Resolve());

            // The author's last word
            var tags = new List<string>();
            if (died.Count > 0 || gone.Count > 0) tags.Add("annals:losses");
            if (raids >= 4) tags.Add("annals:violent");
            if (died.Count == 0 && raids <= 1) tags.Add("annals:peaceful");
            if (wealth > startWealth * 1.15f) tags.Add("annals:richer");
            if (joined.Count >= 2) tags.Add("annals:growing");
            string close = Flavor.FromPool("Council_Pool_AnnalsClose", author, null, ToneUtility.ToneFor(author), tags.ToArray());
            if (!close.NullOrEmpty()) Para("\"" + close.Formatted(author.Named("PAWN")).Resolve() + "\"\n- " + author.LabelShort);

            string text = sb.ToString();
            written.Add(new AnnalsEntry { year = year, author = author.LabelShort, text = text });
            Find.LetterStack.ReceiveLetter("CC_Annals_Title".Translate(year).Resolve(), text, LetterDefOf.NeutralEvent, new LookTargets(author));
            comp.AddChronicle(new ChronicleEntry(ChronicleKind.Note, "CC_Chronicle_Annals".Translate(year, author.LabelShort).Resolve(), author));
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
            Scribe_Collections.Look(ref decrees, "decrees", LookMode.Value);
            Scribe_Values.Look(ref dilemmas, "dilemmas");
            Scribe_Collections.Look(ref quit, "quit", LookMode.Value);
            Scribe_Collections.Look(ref written, "written", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (present == null) present = new List<Pawn>();
                present.RemoveAll(p => p == null);
                if (joined == null) joined = new List<string>();
                if (died == null) died = new List<string>();
                if (gone == null) gone = new List<string>();
                if (decrees == null) decrees = new List<string>();
                if (quit == null) quit = new List<string>();
                if (written == null) written = new List<AnnalsEntry>();
            }
        }
    }
}
