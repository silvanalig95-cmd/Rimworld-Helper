using RimWorld;
using Verse;

namespace ColonyCouncil
{
    /// <summary>What a bottom bar button of the council's opens: a section of the council window, or one page, by name.</summary>
    public class PageButtonExtension : DefModExtension
    {
        public string section;
        public string page;
    }

    /// <summary>
    /// A bottom bar button that opens a section of the council window on its last page (the Planning section), or one
    /// page. Clicked again while that section is showing, it closes the window. Shown by the setting "A planning
    /// button in the bottom bar".
    /// </summary>
    public class MainButtonWorker_Page : MainButtonWorker
    {
        private PageButtonExtension Ext => def.GetModExtension<PageButtonExtension>();

        public override bool Visible => def.buttonVisible && base.Visible;

        public override bool Disabled => Find.CurrentMap == null;

        public override void Activate()
        {
            var ext = Ext;
            if (ext == null) return;
            if (!ext.section.NullOrEmpty()) MainTabWindow_Council.OpenOrCloseSection(ext.section);
            else if (!ext.page.NullOrEmpty()) MainTabWindow_Council.OpenOrClose(ext.page);
        }
    }
}
