using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ColonyCouncil
{
    public partial class CouncilGameComponent
    {
        /// <summary>
        /// The first-run welcome letter has been sent, or isn't wanted. Saved with a default of true, which is what
        /// skips it without a setting: a save that already had Rimfolk's data from before the letter existed has no
        /// such entry and loads as welcomed. A new game, or an older save Rimfolk was just added to, gets a fresh
        /// component that nothing is loaded into, so it starts at false and is welcomed once.
        /// </summary>
        private bool welcomed;

        private void ExposeWelcome() => Scribe_Values.Look(ref welcomed, "welcomed", true);

        public bool Welcomed => welcomed;

        public void MarkWelcomed() => welcomed = true;
    }

    /// <summary>
    /// Sends the welcome letter a little while into a colony's first session with Rimfolk: what Rimfolk does, where
    /// the council button is, and the Almanac's "Start here" page. Once per colony (CouncilGameComponent.Welcomed).
    /// </summary>
    public class WelcomeLetter : GameComponent
    {
        /// <summary>Game ticks to wait after the game is first seen running (about 20 seconds at normal speed).</summary>
        private const int Delay = 1250;

        private int firstSeen = -1;

        public WelcomeLetter(Game game)
        {
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            firstSeen = -1;
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 250 != 71) return;
            var comp = CouncilGameComponent.Instance;
            if (comp == null || comp.Welcomed) return;
            if (firstSeen < 0) firstSeen = now;
            if (now - firstSeen < Delay) return;
            comp.MarkWelcomed();
            try
            {
                Send();
            }
            catch (System.Exception e)
            {
                CouncilLog.ErrorOnce("welcome letter", e);
            }
        }

        /// <summary>The page the letter leads to.</summary>
        public static AlmanacArticleDef StartHere => DefDatabase<AlmanacArticleDef>.GetNamedSilentFail("Council_Alm_StartHere");

        public static void Send()
        {
            string where = ("CC_Welcome_Where_" + ColonyCouncilMod.Settings.buttonPosition).Translate();
            var def = DefDatabase<LetterDef>.GetNamedSilentFail("Council_WelcomeLetter");
            string label = "CC_Welcome_Label".Translate();
            string text = "CC_Welcome_Text".Translate(where).Resolve();
            if (def == null)
            {
                Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NeutralEvent);
                return;
            }
            Find.LetterStack.ReceiveLetter(LetterMaker.MakeLetter(label, text, def));
        }
    }

    /// <summary>The welcome letter, with a button that opens the "Start here" page.</summary>
    public class ChoiceLetter_Welcome : ChoiceLetter
    {
        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                var open = new DiaOption("CC_Welcome_Open".Translate());
                open.action = () =>
                {
                    var page = WelcomeLetter.StartHere;
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
