using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>One update of Rimfolk as the player sees it: when it came, what to call it, and its highlights.</summary>
    public class UpdateRound
    {
        /// <summary>The version it shipped as (About.xml's modVersion); empty for the rounds from before versions were kept.</summary>
        public string version;
        [MustTranslate] public string date;
        [MustTranslate] public string title;
        [MustTranslate] public List<string> points;
    }

    /// <summary>
    /// The Almanac's "What's new" page, and the one list of what changed in each update, newest first: the page's sections
    /// are made from its rounds, the letter after an update shows the newest round, and Tools/changelog.py writes
    /// CHANGELOG.md from the same rounds (a test keeps the two in step, and the newest round's version in step with
    /// About.xml).
    /// </summary>
    public class WhatsNewPageDef : AlmanacArticleDef
    {
        public List<UpdateRound> rounds = new List<UpdateRound>();

        public UpdateRound Latest => rounds?.FirstOrDefault(r => r != null);

        /// <summary>The page's sections, one per round (made once the game's text has loaded, see <see cref="WhatsNew"/>).</summary>
        public void MakeSections()
        {
            sections = new List<AlmanacSection>();
            foreach (var r in rounds ?? new List<UpdateRound>())
            {
                if (r == null) continue;
                sections.Add(new AlmanacSection { heading = r.title, text = WhatsNew.When(r), points = r.points });
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (rounds.NullOrEmpty()) yield return "the What's new page has no rounds";
            else
            {
                if (Latest?.version.NullOrEmpty() ?? true) yield return "the newest round needs a version";
                foreach (var r in rounds.Where(r => r != null && (r.title.NullOrEmpty() || r.date.NullOrEmpty() || r.points.NullOrEmpty())))
                    yield return "a round without a title, date or points: " + (r.title ?? r.date ?? "?");
            }
        }
    }

    /// <summary>
    /// What's new in Rimfolk: the version the player last saw (kept in the mod settings) against the one running now,
    /// and the letter that says it has been updated.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class WhatsNew
    {
        public const string PageName = "Council_Alm_WhatsNew";

        static WhatsNew()
        {
            try
            {
                Page?.MakeSections();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("what's new page", e);
            }
        }

        public static WhatsNewPageDef Page => DefDatabase<AlmanacArticleDef>.GetNamedSilentFail(PageName) as WhatsNewPageDef;

        /// <summary>Rimfolk's version: About.xml's modVersion, or the newest round's when the game doesn't give one.</summary>
        public static string Version
        {
            get
            {
                string v = null;
                try
                {
                    v = ColonyCouncilMod.Pack?.ModMetaData?.ModVersion;
                }
                catch (System.Exception e)
                {
                    CouncilLog.ErrorOnce("mod version", e);
                }
                if (v.NullOrEmpty()) v = Page?.Latest?.version;
                return v?.Trim() ?? "";
            }
        }

        /// <summary>A round's date, with its version where it has one.</summary>
        public static string When(UpdateRound r) =>
            r.version.NullOrEmpty() ? r.date : "CC_WhatsNew_DateVersion".Translate(r.date, r.version).Resolve();

        /// <summary>Has Rimfolk changed since the player last saw it?</summary>
        public static bool Changed
        {
            get
            {
                string now = Version;
                return !now.NullOrEmpty() && now != (ColonyCouncilMod.Settings?.lastSeenVersion ?? "");
            }
        }

        /// <summary>Remember the running version as seen (written to the settings file only when it changes).</summary>
        public static void MarkSeen()
        {
            var s = ColonyCouncilMod.Settings;
            string now = Version;
            if (s == null || now.NullOrEmpty() || s.lastSeenVersion == now) return;
            s.lastSeenVersion = now;
            CouncilSettings.SaveNow();
        }

        public static void Send()
        {
            var page = Page;
            var round = page?.Latest;
            string label = "CC_WhatsNew_Label".Translate();
            var text = new System.Text.StringBuilder();
            text.Append("CC_WhatsNew_Text".Translate(Version).Resolve());
            if (round != null)
            {
                text.Append("\n\n").Append(round.title).Append(" (").Append(When(round)).Append(")");
                foreach (var p in round.points ?? new List<string>()) text.Append("\n  - ").Append(p);
            }
            text.Append("\n\n").Append("CC_WhatsNew_More".Translate());
            var def = DefDatabase<LetterDef>.GetNamedSilentFail("Council_UpdateLetter");
            if (def == null)
            {
                Find.LetterStack.ReceiveLetter(label, text.ToString(), LetterDefOf.NeutralEvent);
                return;
            }
            Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter(label, text.ToString(), def));
        }
    }

    /// <summary>
    /// Sends "Rimfolk has been updated" once, a little while after a save is loaded with a newer Rimfolk than the player
    /// last saw. A new colony only marks the version as seen (the welcome letter covers it), and so does a save Rimfolk
    /// was just added to; the setting "What's new letter" turns it off. Apart from the colonists: a letter to the player.
    /// </summary>
    public class WhatsNewLetter : GameComponent
    {
        /// <summary>Game ticks to wait after the game is first seen running (a little after the welcome letter's wait).</summary>
        private const int Delay = 1500;

        private bool due;
        private int firstSeen = -1;

        public WhatsNewLetter(Game game)
        {
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            due = false;
            try
            {
                WhatsNew.MarkSeen();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("what's new", e);
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            due = false;
            firstSeen = -1;
            try
            {
                if (!WhatsNew.Changed) return;
                var comp = CouncilGameComponent.Instance;
                // A save Rimfolk was just added to is welcomed instead; so is a colony with the letter switched off.
                if (comp == null || !comp.Welcomed || !(ColonyCouncilMod.Settings?.whatsNewLetter ?? true)) WhatsNew.MarkSeen();
                else due = true;
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("what's new", e);
            }
        }

        public override void GameComponentTick()
        {
            if (!due) return;
            int now = Find.TickManager.TicksGame;
            if (!Sched.Every(now, Sched.RareTicks, Sched.RareWelcome)) return;
            if (firstSeen < 0) firstSeen = now;
            if (now - firstSeen < Delay) return;
            due = false;
            try
            {
                WhatsNew.MarkSeen();
                WhatsNew.Send();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("what's new letter", e);
            }
        }
    }

    /// <summary>The update letter, with a button that opens the Almanac's "What's new" page.</summary>
    public class ChoiceLetter_WhatsNew : ChoiceLetter
    {
        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var open = new DiaOption("CC_WhatsNew_Open".Translate());
                open.action = () =>
                {
                    var page = WhatsNew.Page;
                    if (page != null) MainTabWindow_Council.OpenAlmanac(page);
                    else MainTabWindow_Council.OpenTab("Almanac");
                    if (!ArchivedOnly) Find.LetterStack.RemoveLetter(this);
                };
                open.resolveTree = true;
                yield return open;
                yield return Option_Close;
            }
        }
    }
}
